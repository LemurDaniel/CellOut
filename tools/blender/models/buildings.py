# NOTE:
#   Building blocks of the village (assets/models/building/): walls, roofs, frames,
#   defense, trade and decoration, one model per block of the building designer.
#   Every block stands in its own frame: +Y up, +X is its front, the origin is the center
#   of its footprint on the ground. One grid cell is 0.5 units, a block stays inside its
#   box (width along X x height x depth along Z) and ends flat where the next one stacks.
#   No rig. The game recolors by material name: Paint (the surface the player repaints),
#   PaintDark (its trim), Wood, Stone, Ember, Bone, Door, Flame, Rope, Bronze, FruitRed,
#   FruitGold, FruitGreen. Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only building/

#
#   Chapters:
#     PRIMITIVES   Hand-hewn poles, beams, lathed rings, plates, cloth and rocks.
#     WALLS        Wattle roundhouse, log house and masonry tower, with their windows.
#     ROOFS        Shaggy thatched cones and the shingled gable roof.
#     FRAMES       Stilts, platform and ladder.
#     DEFENSE      Palisade, torch and banner.
#     TRADE        Stall, awning and crate.
#     DECORATION   Plank door in its porch, totem segment and horned skull.
#

import math
import random

import bmesh
import bpy
from mathutils import Vector

from models.helpers import G, ellipsoid, link_mesh

FORWARD = Vector((1.0, 0.0, 0.0))                     # Blender axes of the ring frames
ACROSS = Vector((0.0, 1.0, 0.0))
UP = Vector((0.0, 0.0, 1.0))
CORNERS = ((1, 1), (1, -1), (-1, 1), (-1, -1))        # signs of the four corners of a footprint


# ===========================================================================
# =============================   PRIMITIVES   ==============================
# ===========================================================================
#
#   pole          A tube with few sides through points, flat cut ends or a sharp tip.
#   beam          A box from one point to another, optionally with wobbling corners.
#   lathe         A profile turned around the Y axis: walls, rims, thatch, bowls.
#   plate         A flat outline pushed out to a slab: gables, wings, emblems.
#   sheet         A grid of points as thin cloth with two faces and a rim.
#   rock          A rough flat shaded stone.
#   fruit         A small round fruit.
#   curve         Points on a smooth curve through a few control points.
#   trim_height   Cuts every model off at the ground and at the top of its box.
#
#   Points are Blender vectors built with G (Godot axes). Shells get their normals
#   recalculated, caps have their own vertices and stay hard edges.
#

def _bridge(bm, lower, upper, is_smooth):
    """Faces between two rings of vertices, a ring of one vertex is a tip."""
    count = max(len(lower), len(upper))
    for k in range(count):
        following = (k + 1) % count
        if 1 == len(upper):
            face = bm.faces.new((lower[k], lower[following], upper[0]))
        elif 1 == len(lower):
            face = bm.faces.new((lower[0], upper[following], upper[k]))
        else:
            face = bm.faces.new((lower[k], lower[following], upper[following], upper[k]))
        face.smooth = is_smooth


def _cap(bm, ring, facing):
    """Closes a ring with a flat face of its own vertices that looks along facing."""
    if len(ring) < 3:
        return
    face = bm.faces.new([bm.verts.new(vertex.co) for vertex in ring])
    face.normal_update()
    if face.normal.dot(facing) < 0.0:
        face.normal_flip()


def _outward(bm):
    """Turns the faces of a closed solid outwards."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if bm.calc_volume(signed=True) < 0.0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])


def _link(scene, bm, name, material_name):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return link_mesh(scene, name, mesh, material_name)


def pole(scene, name, points, radii, material_name, sides=6, is_smooth=True, is_level=False):
    """
    A tube through points with one ring of sides vertices per point. A radius of 0 ends
    in a sharp tip. is_level keeps every ring horizontal (upright posts with a flat top),
    otherwise the rings stand across the tube.
    """
    bm = bmesh.new()
    last = len(points) - 1
    rings = []
    across = None
    for i, (point, radius) in enumerate(zip(points, radii)):
        if radius <= 0.0:
            rings.append([bm.verts.new(point)])
            continue
        tangent = (points[min(i + 1, last)] - points[max(i - 1, 0)]).normalized()
        if is_level:
            across, out = FORWARD, ACROSS
        else:
            # The first frame hangs on the up axis, the next ones carry it along the tube (no twist)
            if across is None:
                across = tangent.cross(UP if abs(tangent.z) < 0.9 else FORWARD).normalized()
            else:
                across = (across - tangent * across.dot(tangent)).normalized()
            out = tangent.cross(across)
        rings.append([
            bm.verts.new(point + (across * math.cos(k / sides * math.tau) + out * math.sin(k / sides * math.tau)) * radius)
            for k in range(sides)
        ])
    for lower, upper in zip(rings, rings[1:]):
        _bridge(bm, lower, upper, is_smooth)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    _cap(bm, rings[0], points[0] - points[1])
    _cap(bm, rings[-1], points[-1] - points[-2])
    return _link(scene, bm, name, material_name)


def beam(scene, name, start, end, width, height, material_name, rng=None, wobble=0.0):
    """
    A box from start to end. width lies level across the beam, height stands on it.
    An upright beam has its width along Z and its height along X. wobble moves every
    corner by up to that much (needs rng).
    """
    axis = (end - start).normalized()
    side = axis.cross(UP if abs(axis.z) < 0.95 else FORWARD).normalized()
    top = side.cross(axis).normalized()
    bm = bmesh.new()
    ends = []
    for point in (start, end):
        ring = []
        for across, above in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            corner = point + side * (across * width / 2) + top * (above * height / 2)
            if rng is not None:
                corner += Vector([rng.uniform(-wobble, wobble) for _ in range(3)])
            ring.append(bm.verts.new(corner))
        ends.append(ring)
    _bridge(bm, ends[0], ends[1], False)
    bm.faces.new(ends[0])
    bm.faces.new(ends[1])
    _outward(bm)
    return _link(scene, bm, name, material_name)


def lathe(scene, name, profile, material_name, segments=16, center=(0.0, 0.0), bulge=None,
          is_smooth=True, has_top_cap=False, has_bottom_cap=False):
    """
    Turns a profile of (radius, y) points around the upright axis through center (x, z).
    A radius of 0 is a tip, the same point twice in a row is a hard edge. bulge(k, j)
    adds to the radius of segment k in ring j: weaving, scallops, uneven daub.
    """
    bm = bmesh.new()
    rings = []
    for j, (radius, y) in enumerate(profile):
        if radius <= 0.0:
            rings.append([bm.verts.new(G(center[0], y, center[1]))])
            continue
        ring = []
        for k in range(segments):
            angle = k / segments * math.tau
            reach = radius + (bulge(k, j) if bulge is not None else 0.0)
            ring.append(bm.verts.new(G(center[0] + math.cos(angle) * reach, y, center[1] + math.sin(angle) * reach)))
        rings.append(ring)
    for j, (lower, upper) in enumerate(zip(rings, rings[1:])):
        if profile[j] != profile[j + 1]:
            _bridge(bm, lower, upper, is_smooth)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if has_bottom_cap:
        _cap(bm, rings[0], -UP)
    if has_top_cap:
        _cap(bm, rings[-1], UP)
    return _link(scene, bm, name, material_name)


def plate(scene, name, outline, offset, material_name):
    """A flat outline (points in one plane, may be concave) pushed out by offset to a slab."""
    bm = bmesh.new()
    front = [bm.verts.new(point) for point in outline]
    back = [bm.verts.new(point + offset) for point in outline]
    _bridge(bm, front, back, False)
    bm.faces.new(front)
    bm.faces.new(back)
    _outward(bm)
    return _link(scene, bm, name, material_name)


def sheet(scene, name, rows, offset, material_name):
    """
    Cloth through rows of points (every row the same length). offset is its thickness
    as a vector: the rows are one face of the cloth, rows + offset the other, the rim
    between them has its own vertices and stays a hard edge.
    """
    bm = bmesh.new()
    count = len(rows[0])
    middle = sum((point for row in rows for point in row), Vector()) / (len(rows) * count) + offset / 2
    for shift, facing in ((Vector(), -offset), (offset, offset)):
        grid = [[bm.verts.new(point + shift) for point in row] for row in rows]
        for lower, upper in zip(grid, grid[1:]):
            for k in range(count - 1):
                face = bm.faces.new((lower[k], lower[k + 1], upper[k + 1], upper[k]))
                face.smooth = True
                face.normal_update()
                if face.normal.dot(facing) < 0.0:
                    face.normal_flip()
    # Rim: once around the border, every quad looking away from the middle
    border = rows[0] + [row[-1] for row in rows[1:]] + rows[-1][-2::-1] + [row[0] for row in rows[-2:0:-1]]
    for start, end in zip(border, border[1:] + border[:1]):
        face = bm.faces.new([bm.verts.new(point) for point in (start, end, end + offset, start + offset)])
        face.normal_update()
        if face.normal.dot(face.calc_center_median() - middle) < 0.0:
            face.normal_flip()
    return _link(scene, bm, name, material_name)


def rock(scene, name, center, size, material_name, rng, turn=0.0, roughness=0.16):
    """
    A flat shaded stone around center (Godot x, y, z). size holds its three half
    axes (along, up, across), turn (radians about Y) points "along" away from +X.
    """
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    cos, sin = math.cos(turn), math.sin(turn)
    for vertex in bm.verts:
        rough = vertex.co * rng.uniform(1.0 - roughness, 1.0 + roughness)
        along, up, across = rough.x * size[0], rough.z * size[1], rough.y * size[2]
        vertex.co = G(center[0] + along * cos - across * sin, center[1] + up, center[2] + along * sin + across * cos)
    _outward(bm)
    return _link(scene, bm, name, material_name)


def fruit(scene, name, center, radius, material_name):
    """A slightly squashed ball around center (Godot x, y, z)."""
    return ellipsoid(scene, name, G(*center), (radius, radius, radius * 0.92), material_name, subdivisions=2)


def curve(points, count):
    """count points on a Catmull-Rom curve through points: horns."""
    padded = [points[0]] + list(points) + [points[-1]]
    spans = len(points) - 1
    samples = []
    for i in range(count):
        position = i / (count - 1) * spans
        span = min(int(position), spans - 1)
        t = position - span
        p0, p1, p2, p3 = padded[span:span + 4]
        samples.append(0.5 * (2.0 * p1 + (p2 - p0) * t + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t * t
                              + (3.0 * p1 - p0 - 3.0 * p2 + p3) * t ** 3))
    return samples


def trim_height(scene, height):
    """No vertex below the ground or above the box: wobble and tilted cuts end flat there."""
    for obj in scene.objects:
        if 'MESH' != obj.type:
            continue
        for vertex in obj.data.vertices:
            vertex.co.z = min(max(vertex.co.z, 0.0), height)


# ===========================================================================
# ================================   WALLS   ================================
# ===========================================================================
#
#   panel               An upright board lying flat on a wall that looks any way.
#   window              A dark opening in a wall: frame, sill, lintel, bars, shutters.
#   arc_block           A hewn stone block bent around the upright axis: masonry of a tower.
#   build_round_room    Wattle and daub roundhouse: field stones at the foot, upright posts,
#                       woven withies, three small windows, a wooden wall plate on top.
#   build_square_room   Log house: stone foundation, round logs crossing at the corners,
#                       shuttered windows on both flanks, a small one at the back, joist
#                       ends under the top plate.
#   build_round_tower   Masonry: a strong foot, staggered courses of rough blocks with dark
#                       joints, two arrow slits, a crown ring on corbels.
#
#   All three end flat at y = 1, the next block stands on them. Their front (+X) stays
#   free of windows: the door block stands there. The tower stacks onto itself.
#

WINDOW_FRAME = 0.045                                  # width of the jambs, lintel and sill of a window
WINDOW_SINK = 0.06                                    # how deep a window starts inside its wall


def panel(scene, name, center, across, out, width, height, depth, material_name):
    """
    An upright board: center is the middle of its back face (Blender vector), across
    runs level along the wall, out is the way the wall looks. depth pushes it out.
    """
    half_across, half_up = across * (width / 2), UP * (height / 2)
    outline = [center - half_across - half_up, center + half_across - half_up,
               center + half_across + half_up, center - half_across + half_up]
    return plate(scene, name, outline, out * depth, material_name)


def window(scene, name, angle, reach, y, width, height, depth, has_cross=True, has_shutters=False, ledge=1.0):
    """
    A window on a wall that looks along angle (radians about Y, 0 = +X) and stands reach
    away from the middle. width x height is the dark opening around height y, depth is
    how far the frame stands off the wall. Shutters hang open on both sides. ledge is
    how far lintel and sill stand over the jambs (0 = flush, the slit of a tower).
    """
    out, across = G(math.cos(angle), 0.0, math.sin(angle)), G(-math.sin(angle), 0.0, math.cos(angle))
    back = out * (reach - WINDOW_SINK) + UP * y
    # Dark opening, a little proud of the wall
    panel(scene, f'{name}Dark', back, across, out, width, height, WINDOW_SINK + 0.02, 'Door')
    # Frame: jambs, lintel and a sill that sticks out further
    for side in (-1, 1):
        jamb = back + across * (side * (width + WINDOW_FRAME) / 2)
        panel(scene, f'{name}Jamb{side}', jamb, across, out, WINDOW_FRAME, height, WINDOW_SINK + depth, 'PaintDark')
    rim = height / 2 + WINDOW_FRAME / 2
    panel(scene, f'{name}Lintel', back + UP * rim, across, out, width + WINDOW_FRAME * (2.0 + 1.2 * ledge), WINDOW_FRAME * 1.15, WINDOW_SINK + depth + 0.01 * ledge, 'PaintDark')
    panel(scene, f'{name}Sill', back - UP * rim, across, out, width + WINDOW_FRAME * (2.0 + 2.0 * ledge), WINDOW_FRAME, WINDOW_SINK + depth + 0.03 * ledge, 'PaintDark')
    if has_cross:
        # Bars of the opening, thinner than the frame and set back behind it
        panel(scene, f'{name}Post', back, across, out, 0.03, height, WINDOW_SINK + depth * 0.55, 'PaintDark')
        panel(scene, f'{name}Bar', back + UP * (height * 0.12), across, out, width, 0.03, WINDOW_SINK + depth * 0.5, 'PaintDark')
    if not has_shutters:
        return
    # Shutters: a board on each side with two battens
    leaf = width * 0.5
    for side in (-1, 1):
        middle = back + across * (side * (width / 2 + WINDOW_FRAME + leaf / 2 + 0.01))
        panel(scene, f'{name}Shutter{side}', middle, across, out, leaf, height * 1.04, WINDOW_SINK + depth * 0.45, 'PaintDark')
        for row in (-1, 1):
            panel(scene, f'{name}Batten{side}{row}', middle + UP * (row * height * 0.3), across, out, leaf * 0.9, 0.04, WINDOW_SINK + depth * 0.75, 'Wood')


def arc_block(scene, name, angle, span, inner, outer, low, high, material_name, rng, wobble=0.0):
    """
    A stone block bent around the Y axis: span radians wide around angle, from radius
    inner to outer and height low to high. wobble moves every corner by up to that much.
    """
    steps = max(1, round(span / 0.42))                # one flat face per 24 degrees keeps the wall round
    bm = bmesh.new()
    rings = []
    for step in range(steps + 1):
        at = angle - span / 2 + span * step / steps
        ring = []
        for radius, y in ((inner, low), (outer, low), (outer, high), (inner, high)):
            reach = radius + rng.uniform(-wobble, wobble)
            ring.append(bm.verts.new(G(math.cos(at) * reach, y + rng.uniform(-wobble, wobble), math.sin(at) * reach)))
        rings.append(ring)
    for lower, upper in zip(rings, rings[1:]):
        _bridge(bm, lower, upper, False)
    bm.faces.new(rings[0])
    bm.faces.new(rings[-1])
    _outward(bm)
    return _link(scene, bm, name, material_name)


def build_round_room(scene):
    rng = random.Random(101)
    post_count = 12
    segments = post_count * 2            # every other segment sits on a post
    # Daub wall, uneven below its flat top
    lathe(scene, 'Wall', [(0.9, 0.0), (0.912, 0.5), (0.9, 1.0)], 'Paint', segments=segments,
          bulge=lambda k, j: 0.0 if 2 == j else rng.uniform(-0.01, 0.01), has_top_cap=True)
    # Field stones around the foot, each one different
    stone_count = 13
    for i in range(stone_count):
        angle = (i + rng.uniform(-0.15, 0.15)) / stone_count * math.tau
        size = (rng.uniform(0.06, 0.075), rng.uniform(0.075, 0.115), rng.uniform(0.17, 0.23))
        rock(scene, f'Stone{i}', (math.cos(angle) * 0.915, 0.06, math.sin(angle) * 0.915), size, 'Stone', rng, turn=angle)
    # Upright posts, each a little bent
    for i in range(post_count):
        angle = (i + 0.5) / post_count * math.tau
        bend = angle + rng.uniform(-0.03, 0.03)
        points = [G(math.cos(angle) * 0.925, 0.0, math.sin(angle) * 0.925),
                  G(math.cos(bend) * 0.93, 0.5, math.sin(bend) * 0.93),
                  G(math.cos(angle) * 0.925, 1.0, math.sin(angle) * 0.925)]
        pole(scene, f'Post{i}', points, [0.055, 0.047, 0.05], 'PaintDark', sides=5, is_level=True)
    # Withies woven around the posts: in front of one post, behind the next, row against row
    for row, y in enumerate((0.215, 0.28, 0.345, 0.41, 0.82)):
        def weave(k, j, row=row):
            if 0 == k % 2:
                return 0.0
            return 0.05 if 0 == (k // 2 + row) % 2 else -0.04
        lathe(scene, f'Withy{row}', [(0.905, y - 0.028), (0.942, y), (0.905, y + 0.028)], 'PaintDark', segments=segments, bulge=weave)
    # Windows between two posts: both flanks and the back, the front belongs to the door
    for i, angle in enumerate((math.pi / 2, math.pi, -math.pi / 2)):
        window(scene, f'Window{i}', angle, 0.9, 0.63, 0.21, 0.23, 0.065)
    # Wall plate: a wooden ring lying on the posts
    lathe(scene, 'Plate', [(0.9, 0.885), (0.985, 0.89), (0.995, 0.945), (0.985, 1.0), (0.985, 1.0), (0.9, 1.0)], 'PaintDark', segments=segments)
    trim_height(scene, 1.0)


LOG_RADIUS = 0.08                                     # logs of the square room
LOG_DEPTH = 0.82                                      # their middle line, seen from the middle of the room
LOG_COURSES_X = (0.21, 0.36, 0.51, 0.66, 0.81)        # heights of the logs on the +X and -X faces
LOG_COURSES_Z = (0.15, 0.285, 0.435, 0.585, 0.735)    # half a log higher on +Z and -Z: they cross at the corners


def build_square_room(scene):
    rng = random.Random(102)
    # Core behind the logs, its top is the flat top of the room
    beam(scene, 'Core', G(0, 0.0, 0), G(0, 1.0, 0), LOG_DEPTH * 2.0, LOG_DEPTH * 2.0, 'Paint')
    # Foundation: hewn stone blocks along every side, a boulder under every corner
    for side_x, side_z in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        cuts = [-0.84, rng.uniform(-0.5, -0.3), rng.uniform(-0.1, 0.1), rng.uniform(0.3, 0.5), 0.84]
        for i, (near, far) in enumerate(zip(cuts, cuts[1:])):
            depth = 0.88 + rng.uniform(-0.015, 0.015)
            start = G(side_x * depth + abs(side_z) * (near + 0.012), 0.065, side_z * depth + abs(side_x) * (near + 0.012))
            end = G(side_x * depth + abs(side_z) * (far - 0.012), 0.065, side_z * depth + abs(side_x) * (far - 0.012))
            beam(scene, f'Block{side_x}{side_z}{i}', start, end, 0.16, rng.uniform(0.13, 0.15), 'Stone', rng, 0.014)
    for i, (sx, sz) in enumerate(CORNERS):
        rock(scene, f'Boulder{i}', (sx * 0.89, 0.07, sz * 0.89), (0.1, 0.095, 0.1), 'Stone', rng, roughness=0.12)
    # Logs: along Z on the X faces, along X on the Z faces, their ends stick out at the corners
    for side in (-1, 1):
        for i, y in enumerate(LOG_COURSES_X):
            reach, bow = side * LOG_DEPTH, side * rng.uniform(0.0, 0.012)
            points = [G(reach, y, -rng.uniform(0.95, 1.0)), G(reach + bow, y, 0.0), G(reach, y, rng.uniform(0.95, 1.0))]
            pole(scene, f'LogX{side}{i}', points, [LOG_RADIUS, LOG_RADIUS * 0.96, LOG_RADIUS * rng.uniform(0.9, 1.0)], 'Paint', sides=7)
        for i, y in enumerate(LOG_COURSES_Z):
            reach, bow = side * LOG_DEPTH, side * rng.uniform(0.0, 0.012)
            radius = LOG_RADIUS * (0.85 if 0 == i else 1.0)     # the lowest one lies half in the foundation
            points = [G(-rng.uniform(0.95, 1.0), y, reach), G(0.0, y, reach + bow), G(rng.uniform(0.95, 1.0), y, reach)]
            pole(scene, f'LogZ{side}{i}', points, [radius, radius * 0.96, radius * rng.uniform(0.9, 1.0)], 'Paint', sides=7)
    # Windows: shuttered on both flanks, a small one at the back
    for side in (-1, 1):
        window(scene, f'Window{side}', side * math.pi / 2, LOG_DEPTH + LOG_RADIUS, 0.5, 0.32, 0.3, 0.07, has_shutters=True)
    window(scene, 'WindowBack', math.pi, LOG_DEPTH + LOG_RADIUS, 0.53, 0.24, 0.24, 0.07)
    # Joist ends sticking out of both flanks under the top plate
    for side in (-1, 1):
        for i in range(5):
            x = -0.62 + 0.31 * i + rng.uniform(-0.02, 0.02)
            beam(scene, f'Joist{side}{i}', G(x, 0.857, side * 0.7), G(x, 0.857, side * rng.uniform(0.96, 1.0)), 0.1, 0.085, 'PaintDark', rng, 0.006)
    # Top plate: four beams crossing at the corners like the logs
    for side in (-1, 1):
        beam(scene, f'PlateX{side}', G(side * 0.895, 0.95, -1.0), G(side * 0.895, 0.95, 1.0), 0.12, 0.1, 'PaintDark', rng, 0.006)
        beam(scene, f'PlateZ{side}', G(-1.0, 0.95, side * 0.895), G(1.0, 0.95, side * 0.895), 0.12, 0.1, 'PaintDark', rng, 0.006)
    trim_height(scene, 1.0)


TOWER_CORE = 0.415                                    # radius of the dark joints behind the blocks
TOWER_COURSES = 5                                     # courses of blocks between foot and crown
TOWER_FOOT, TOWER_CROWN = 0.15, 0.85                  # heights where the foot ends and the crown starts


def build_round_tower(scene):
    rng = random.Random(103)
    # Core: the dark joints between the blocks, closed by a painted top
    lathe(scene, 'Joints', [(TOWER_CORE, 0.0), (TOWER_CORE, 1.0)], 'PaintDark', segments=16)
    lathe(scene, 'Top', [(TOWER_CORE, 1.0), (0.0, 1.0)], 'Paint', segments=16)
    # Foot: one course of big blocks standing further out
    foot_count = 6
    for i in range(foot_count):
        span = math.tau / foot_count
        arc_block(scene, f'Foot{i}', (i + 0.3) * span, span - 0.07, 0.36, rng.uniform(0.475, 0.49), 0.0, TOWER_FOOT - 0.012, 'Paint', rng, 0.008)
    # Staggered courses: every course split into blocks of its own lengths, turned against the one below
    course_height = (TOWER_CROWN - TOWER_FOOT) / TOWER_COURSES
    for course in range(TOWER_COURSES):
        low = TOWER_FOOT + course * course_height
        shares = [rng.uniform(0.7, 1.3) for _ in range(7)]
        at = course * 0.47 + rng.uniform(0.0, 0.2)
        for i, share in enumerate(shares):
            span = share / sum(shares) * math.tau
            arc_block(scene, f'Block{course}{i}', at + span / 2, span - 0.06, 0.36, rng.uniform(0.445, 0.468),
                      low + 0.012, low + course_height - 0.012, 'Paint', rng, 0.007)
            at += span
    # Arrow slits on both flanks, framed by trim stones
    for side in (-1, 1):
        window(scene, f'Slit{side}', side * math.pi / 2, 0.46, 0.52, 0.055, 0.3, 0.035, has_cross=False, ledge=0.0)
    # Crown: corbels carrying a ring that stands out over the wall
    corbel_count = 10
    for i in range(corbel_count):
        angle = (i + 0.5) / corbel_count * math.tau
        arc_block(scene, f'Corbel{i}', angle, 0.26, 0.36, 0.478, TOWER_CROWN, 0.925, 'PaintDark', rng, 0.004)
    lathe(scene, 'Crown', [(TOWER_CORE, 0.915), (0.49, 0.92), (0.495, 0.96), (0.49, 1.0), (0.49, 1.0), (TOWER_CORE, 1.0)], 'PaintDark',
          segments=16, is_smooth=False)
    trim_height(scene, 1.0)


# ===========================================================================
# ================================   ROOFS   ================================
# ===========================================================================
#
#   thatch_cone             Shaggy tiers of furrowed thatch with thick ragged lips, a bound
#                           sway, straw bundles at the eaves, a bound knot with crossed poles.
#   build_cone_roof         The wide cone over a room, three tiers.
#   build_small_cone_roof   The small cone over a tower, two tiers.
#   build_gable_roof        Courses of shingles on a prism, planked gables with a vent, crossed
#                           barge boards with horns, rafter ends at the eaves, a sagging ridge log.
#
#   The roofs overhang their footprint: the cones to radius 1.3 and 0.72, the gable
#   by 0.2 on each side. All are 1 high and flat below.
#

THATCH_TOP = 0.83                                     # height where the thatch is bound to the knot
THATCH_OVERLAP = 0.06                                 # how far a tier hangs over the one below (height)
THATCH_FLARE = 0.13                                   # how far its lip stands off the tier below
THATCH_LIP = 0.1                                      # thickness of the cut edge of a tier
THATCH_STEEPNESS = 1.3                                # above 1 the cone gets steeper towards the top
THATCH_FLUTE = 0.04                                   # depth of the furrows between two bundles of straw


def thatch_cone(scene, rng, radius, tiers, segments, poles=4):
    """
    A cone from radius at y = 0 to a bound knot below y = 1, closed below. Every tier is
    its own skirt: a thick ragged lip below (bundles of straw), tucked under the next
    above. poles cross in the knot and stick out of the top like on a roundhouse.
    """
    neck = 0.05 + radius * 0.04                       # radius of the thatch where it is bound

    def body(y):
        """Radius of the cone at height y: steeper towards the top."""
        return neck + (radius - neck) * (1.0 - y / THATCH_TOP) ** THATCH_STEEPNESS

    tier_height = THATCH_TOP / tiers
    for tier in range(tiers):
        is_first, is_last = 0 == tier, tiers - 1 == tier
        low = tier * tier_height - (0.0 if is_first else THATCH_OVERLAP)
        edge = body(tier * tier_height) + (0.0 if is_first else THATCH_FLARE)
        # Every bundle of straw runs down the whole tier: a ridge, a furrow, each its own length
        ragged = [THATCH_FLUTE * (1 if 0 == k % 2 else -1) + rng.uniform(-0.02, 0.02) for k in range(segments)]
        # Lip: under side, lower edge, upper edge
        under, lower, upper = (edge - 0.16, low), (edge, low + 0.012), (edge - 0.03, low + THATCH_LIP)
        end = (neck, THATCH_TOP) if is_last else (body((tier + 1) * tier_height) - 0.03, (tier + 1) * tier_height + 0.01)
        fade = 0.25 if is_last else 0.6               # the furrows run out where the tier is tucked in or bound
        lathe(scene, f'Thatch{tier}', [under, lower, lower, upper, upper, end], 'Paint', segments=segments, is_smooth=False,
              bulge=lambda k, j, ragged=ragged, fade=fade: ragged[k] * (1.0 if j <= 4 else fade), has_bottom_cap=is_first)
    # Bound sway on the lowest tier, lying on its slope
    start, stop = Vector((radius - 0.03, THATCH_LIP)), Vector((body(tier_height), tier_height))
    along = (stop - start).normalized()
    outward = Vector((along.y, -along.x))
    middle = start.lerp(stop, 0.42)
    sway = [middle - along * 0.035 - outward * 0.03, middle + outward * 0.05, middle + along * 0.035 - outward * 0.03]
    lathe(scene, 'Sway', [tuple(point) for point in sway], 'PaintDark', segments=segments)
    # Straw bundles sticking out of the eaves, one under every ridge
    for k in range(0, segments, 2):
        angle = (k + rng.uniform(-0.15, 0.15)) / segments * math.tau
        reach = radius + rng.uniform(0.025, 0.065)
        cos, sin = math.cos(angle), math.sin(angle)
        pole(scene, f'Bundle{k}', [G(cos * (reach - 0.3), 0.19, sin * (reach - 0.3)), G(cos * reach, 0.04, sin * reach)],
             [0.045, 0.062], 'Paint', sides=5, is_smooth=False)
    # Knot: a rope around the neck, the thatch flaring out above it, poles crossing through
    lathe(scene, 'Rope', [(neck, THATCH_TOP - 0.05), (neck + 0.03, THATCH_TOP - 0.025), (neck + 0.03, THATCH_TOP + 0.025), (neck - 0.01, THATCH_TOP + 0.045)],
          'Rope', segments=8)
    lathe(scene, 'Tuft', [(neck - 0.01, THATCH_TOP), (neck + 0.075, THATCH_TOP + 0.06), (neck + 0.02, THATCH_TOP + 0.11), (0.0, 1.0)],
          'Paint', segments=8, is_smooth=False)
    for i in range(poles):
        angle = (i + rng.uniform(-0.2, 0.2)) / poles * math.pi
        cos, sin = math.cos(angle), math.sin(angle)
        foot, tip = neck * 1.6, neck * 3.4            # the pole leans from one side of the neck out over the other
        pole(scene, f'Pole{i}', [G(-cos * foot, THATCH_TOP - 0.16, -sin * foot), G(cos * tip, 0.995, sin * tip)], [0.032, 0.022], 'Wood', sides=5)
    trim_height(scene, 1.0)


def build_cone_roof(scene):
    thatch_cone(scene, random.Random(104), radius=1.2, tiers=3, segments=28)


def build_small_cone_roof(scene):
    thatch_cone(scene, random.Random(106), radius=0.63, tiers=2, segments=16, poles=3)


GABLE_EAVE = Vector((1.115, 0.115))                    # (x, y) of the slope of the gable roof: lower edge
GABLE_RIDGE = Vector((0.02, 0.84))                    # and upper edge, the horns of the barge boards need the rest
GABLE_WALL = 1.0                                      # half width of the gable wall below the shingles


def gable_top(x):
    """Height of the gable wall at x: just below the shingles."""
    return 0.14 + (1.0 - abs(x) / GABLE_WALL) * 0.65


def build_gable_roof(scene):
    rng = random.Random(105)
    # Body below the shingles, as wide as the wall it stands on
    body = [G(-GABLE_WALL, 0.0, -1.0), G(GABLE_WALL, 0.0, -1.0), G(GABLE_WALL, gable_top(GABLE_WALL), -1.0),
            G(0.0, gable_top(0.0), -1.0), G(-GABLE_WALL, gable_top(GABLE_WALL), -1.0)]
    plate(scene, 'Body', body, G(0.0, 0.0, 2.0), 'PaintDark')
    # The slope in (x, y): from the eave up to the ridge
    along = (GABLE_RIDGE - GABLE_EAVE).normalized()
    outward = Vector((along.y, -along.x))
    course_count, shingle_count, half_depth = 4, 6, 1.1
    step = 2.0 * half_depth / shingle_count
    for course in range(course_count):
        # Every course lies with its lower end on the upper end of the one below
        low = GABLE_EAVE.lerp(GABLE_RIDGE, course / course_count) + outward * 0.035
        high = GABLE_EAVE.lerp(GABLE_RIDGE, min(1.0, (course + 1) / course_count + 0.05))
        # Every other course is shifted by half a shingle
        if 0 == course % 2:
            cuts = [-half_depth + step * i for i in range(shingle_count + 1)]
        else:
            cuts = [-half_depth] + [-half_depth + step * (i + 0.5) for i in range(shingle_count)] + [half_depth]
        for side in (1, -1):
            for i, (near, far) in enumerate(zip(cuts, cuts[1:])):
                start = low - along * rng.uniform(0.0, 0.045)
                z = (near + far) / 2
                beam(scene, f'Shingle{course}{side}{i}', G(side * start.x, start.y, z), G(side * high.x, high.y, z),
                     far - near - 0.018, rng.uniform(0.04, 0.055), 'Paint', rng, 0.004)
    # Rafter ends looking out from under the eaves
    for side in (1, -1):
        for i in range(5):
            z = -0.86 + 0.43 * i
            inner = GABLE_EAVE + along * 0.34 - outward * 0.052
            outer = GABLE_EAVE - along * rng.uniform(0.035, 0.06) - outward * 0.052
            beam(scene, f'Rafter{side}{i}', G(side * inner.x, inner.y, z), G(side * outer.x, outer.y, z), 0.075, 0.055, 'PaintDark')
    for end in (-1, 1):
        # Gable: upright planks, every other one standing further out, on a tie beam
        plank_count = 8
        width = 2.0 * GABLE_WALL / plank_count
        for i in range(plank_count):
            near, far = -GABLE_WALL + width * i + 0.008, -GABLE_WALL + width * (i + 1) - 0.008
            outline = [G(near, 0.0, end), G(far, 0.0, end), G(far, gable_top(far) - 0.02, end), G(near, gable_top(near) - 0.02, end)]
            plate(scene, f'Plank{end}{i}', outline, G(0.0, 0.0, end * (0.045 if 0 == i % 2 else 0.025)), 'PaintDark')
        beam(scene, f'Tie{end}', G(-GABLE_WALL - 0.06, 0.06, end * 1.04), G(GABLE_WALL + 0.06, 0.06, end * 1.04), 0.08, 0.1, 'PaintDark', rng, 0.005)
        # Vent: a small dark triangle in a frame
        face = end * 1.05
        vent = [G(-0.15, 0.36, face), G(0.15, 0.36, face), G(0.0, 0.57, face)]
        plate(scene, f'Vent{end}', vent, G(0.0, 0.0, end * 0.012), 'Door')
        for i, (start, stop) in enumerate(zip(vent, vent[1:] + vent[:1])):
            beam(scene, f'VentFrame{end}{i}', start.lerp(stop, -0.12), stop.lerp(start, -0.12), 0.05, 0.04, 'PaintDark')
        # Barge boards over the shingle ends: they cross at the ridge and stick out as horns
        for side in (1, -1):
            z = end * (1.14 + side * 0.022)           # one board lies in front of the other where they cross
            foot = GABLE_EAVE - along * 0.05 + outward * 0.03
            horn = GABLE_RIDGE + along * 0.215 + outward * 0.03
            beam(scene, f'Barge{end}{side}', G(side * foot.x, foot.y, z), G(side * horn.x, horn.y, z), 0.044, 0.09, 'PaintDark')
    # Ridge log, sagging in the middle
    pole(scene, 'Ridge', [G(0.0, 0.878, -1.09), G(0.0, 0.852, 0.0), G(0.0, 0.878, 1.09)], [0.056, 0.05, 0.056], 'PaintDark', sides=6)
    trim_height(scene, 1.0)


# ===========================================================================
# ===============================   FRAMES   ================================
# ===========================================================================
#
#   lashing          A rope wound around an upright post.
#   build_stilts     Four posts, a beam frame on top, cross braces on three sides.
#   build_platform   A plank deck on joists and six short legs.
#   build_ladder     Two bent rails with five rungs, leaning back.
#
#   Stilts end flat at y = 1 and the deck at y = 0.5, the next block stands on them.
#

def lashing(scene, name, x, y, z, radius):
    """A rope ring around a post of that radius at (x, z), centered on height y."""
    profile = [(radius * 1.05, y - 0.035), (radius + 0.016, y - 0.012), (radius + 0.016, y + 0.012), (radius * 1.05, y + 0.035)]
    return lathe(scene, name, profile, 'Rope', segments=6, center=(x, z))


def build_stilts(scene):
    rng = random.Random(107)
    for i, (sx, sz) in enumerate(CORNERS):
        x, z = sx * 0.4, sz * 0.4
        points = [G(x, 0.0, z), G(x + rng.uniform(-0.015, 0.015), 0.5, z + rng.uniform(-0.015, 0.015)), G(x, 0.95, z)]
        pole(scene, f'Post{i}', points, [0.06, 0.052, 0.055], 'Paint', sides=6, is_level=True)
        lashing(scene, f'Rope{i}', x, 0.84, z, 0.055)
    # Beam frame: the long beams along X, the short ones fit between them
    for side in (-0.4, 0.4):
        beam(scene, f'BeamX{side}', G(-0.47, 0.955, side), G(0.47, 0.955, side), 0.09, 0.09, 'PaintDark')
        beam(scene, f'BeamZ{side}', G(side, 0.955, -0.355), G(side, 0.955, 0.355), 0.09, 0.09, 'PaintDark')
    # Cross braces on both flanks, one brace at the back, the front stays open
    for side in (-0.4, 0.4):
        pole(scene, f'BraceUp{side}', [G(-0.4, 0.14, side * 1.06), G(0.4, 0.78, side * 1.06)], [0.026, 0.022], 'PaintDark', sides=5)
        pole(scene, f'BraceDown{side}', [G(-0.4, 0.78, side * 0.94), G(0.4, 0.14, side * 0.94)], [0.022, 0.026], 'PaintDark', sides=5)
    pole(scene, 'BraceBack', [G(-0.4, 0.14, -0.4), G(-0.4, 0.78, 0.4)], [0.026, 0.022], 'PaintDark', sides=5)
    trim_height(scene, 1.0)


def build_platform(scene):
    rng = random.Random(108)
    # Eight planks along X, their ends uneven, the top flat at 0.5
    plank_count = 8
    spacing = 2.0 / plank_count
    for i in range(plank_count):
        z = -1.0 + spacing * (i + 0.5)
        beam(scene, f'Plank{i}', G(-1.0 + rng.uniform(0.0, 0.035), 0.465, z), G(1.0 - rng.uniform(0.0, 0.035), 0.465, z),
             spacing - rng.uniform(0.012, 0.022), 0.07, 'Paint')
    # Three joists across the planks, a leg under each end
    for i, x in enumerate((-0.8, 0.0, 0.8)):
        beam(scene, f'Joist{i}', G(x, 0.385, -0.97), G(x, 0.385, 0.97), 0.09, 0.09, 'PaintDark', rng, 0.005)
        for side in (-1, 1):
            z = side * 0.86
            points = [G(x, 0.0, z), G(x + rng.uniform(-0.012, 0.012), 0.2, z), G(x, 0.4, z)]
            pole(scene, f'Leg{i}{side}', points, [0.065, 0.056, 0.058], 'PaintDark', sides=6, is_level=True)
            lashing(scene, f'Rope{i}{side}', x, 0.3, z, 0.057)
    trim_height(scene, 0.5)


def build_ladder(scene):
    rng = random.Random(109)
    foot, top = Vector((0.17, 0.0)), Vector((-0.2, 1.0))      # (x, y): leaning back towards -X
    for side in (-1, 1):
        z = side * 0.16
        bow = rng.uniform(0.005, 0.02)
        points = [G(foot.x, foot.y, z), G((foot.x + top.x) / 2 + bow, 0.5, z * 1.04), G(top.x, top.y, z)]
        pole(scene, f'Rail{side}', points, [0.028, 0.025, 0.023], 'Paint', sides=6)
    for i in range(5):
        at = foot.lerp(top, 0.14 + 0.18 * i)
        ends = [G(at.x + 0.008, at.y + rng.uniform(-0.012, 0.012), side * 0.205) for side in (-1, 1)]
        pole(scene, f'Rung{i}', ends, [0.019, 0.017], 'PaintDark', sides=5)
    trim_height(scene, 1.0)


# ===========================================================================
# ===============================   DEFENSE   ===============================
# ===========================================================================
#
#   build_palisade   Seven sharpened stakes along X, a rail in front, a rope wound around.
#   build_torch      A pole with a rope grip, a bowl of embers and a flame.
#   build_banner     A tall pole wedged with stones, a swallowtail cloth with an emblem.
#
#   The flame and the cloth are meshes of their own material (Flame, Paint), the
#   game can make them glow or sway.
#

def build_palisade(scene):
    rng = random.Random(110)
    stake_count, spacing = 7, 0.285
    stakes = []
    for i in range(stake_count):
        x = -spacing * (stake_count - 1) / 2 + spacing * i
        z = rng.uniform(-0.02, 0.02)
        radius = rng.uniform(0.118, 0.13)
        height = 1.0 if 3 == i else rng.uniform(0.9, 0.99)
        lean = rng.uniform(-0.012, 0.012)
        shoulder = G(x + lean, height - 0.2, z)
        pole(scene, f'Stake{i}', [G(x, 0.0, z), G(x + lean * 0.5, (height - 0.2) / 2, z), shoulder],
             [radius, radius * 0.96, radius * 0.92], 'Paint', sides=7, is_level=True)
        # Hewn point, a little off the middle
        tip = G(x + lean + rng.uniform(-0.025, 0.025), height, z + rng.uniform(-0.025, 0.025))
        pole(scene, f'Point{i}', [shoulder, tip], [radius * 0.92, 0.0], 'Paint', sides=7, is_smooth=False, is_level=True)
        stakes.append((x, z, radius))
    # Rail in front (+Z)
    pole(scene, 'Rail', [G(-0.97, 0.57, 0.155), G(0.0, 0.55, 0.162), G(0.97, 0.58, 0.155)], [0.034, 0.038, 0.034], 'PaintDark', sides=6)
    # Rope: over every stake, into the gap between two, on both faces
    for side in (-1, 1):
        points = [G(stakes[0][0], 0.3, 0.0)]
        for i, (x, z, radius) in enumerate(stakes):
            points.append(G(x, 0.3 + rng.uniform(-0.015, 0.015), z + side * (radius + 0.008)))
            if i < stake_count - 1:
                points.append(G(x + spacing / 2, 0.3, side * 0.075))
        points.append(G(stakes[-1][0], 0.3, 0.0))
        pole(scene, f'Rope{side}', points, [0.017] * len(points), 'Rope', sides=4)
    trim_height(scene, 1.0)


def build_torch(scene):
    pole(scene, 'Pole', [G(0, 0.0, 0), G(0.012, 0.4, -0.008), G(0, 0.76, 0)], [0.04, 0.033, 0.036], 'Paint', sides=6, is_level=True)
    lathe(scene, 'Grip', [(0.034, 0.4), (0.046, 0.42), (0.046, 0.54), (0.034, 0.56)], 'Rope', segments=6, center=(0.008, -0.005))
    # Bowl: outside, rim, inside
    rim, inner = (0.115, 0.875), (0.09, 0.875)
    lathe(scene, 'Bowl', [(0.035, 0.72), (0.08, 0.765), (0.11, 0.83), rim, rim, inner, inner, (0.07, 0.84)], 'PaintDark', segments=9)
    lathe(scene, 'Embers', [(0.09, 0.855), (0.0, 0.87)], 'Ember', segments=9)
    # A big flame licking sideways and a small tongue next to it
    pole(scene, 'Flame', [G(0, 0.85, 0), G(0.012, 0.895, 0.005), G(-0.01, 0.945, -0.006), G(0.014, 1.0, 0.01)],
         [0.05, 0.062, 0.04, 0.0], 'Flame', sides=7, is_level=True)
    pole(scene, 'Tongue', [G(-0.045, 0.855, 0.03), G(-0.058, 0.895, 0.04), G(-0.042, 0.95, 0.05)],
         [0.028, 0.03, 0.0], 'Flame', sides=5, is_level=True)
    trim_height(scene, 1.0)


def build_banner(scene):
    rng = random.Random(112)
    pole(scene, 'Pole', [G(0, 0.0, 0), G(0.008, 0.75, 0.005), G(0, 1.455, 0)], [0.038, 0.033, 0.028], 'Wood', sides=6, is_level=True)
    ellipsoid(scene, 'Knob', G(0, 1.458, 0), (0.042, 0.042, 0.042), 'Wood', subdivisions=2)
    pole(scene, 'Bar', [G(-0.05, 1.385, 0), G(0.47, 1.385, 0)], [0.018, 0.015], 'Wood', sides=5)
    # Stones wedging the pole
    for i, angle in enumerate((0.6, 2.7, 4.6)):
        rock(scene, f'Wedge{i}', (math.cos(angle) * 0.085, 0.03, math.sin(angle) * 0.085),
             (0.07, rng.uniform(0.045, 0.06), 0.085), 'Stone', rng, turn=angle)
    # Swallowtail cloth hanging from the bar towards +X, waving more the lower it hangs
    column_count, row_count = 5, 5
    rows = []
    for row in range(row_count):
        share = row / (row_count - 1)
        points = []
        for column in range(column_count):
            x = 0.07 + 0.38 * column / (column_count - 1)
            length = 0.36 + 0.14 * abs(column - 2) / 2
            wave = 0.022 * share * math.sin(column * 1.3 + row * 0.9)
            points.append(G(x, 1.375 - length * share, wave - 0.007))
        rows.append(points)
    sheet(scene, 'Cloth', rows, G(0.0, 0.0, 0.014), 'Paint')
    # Emblem: a diamond with a dot, showing on both faces of the cloth
    for name, x, y, size in (('Emblem', 0.26, 1.215, 0.085), ('Dot', 0.26, 1.085, 0.03)):
        outline = [G(x, y + size, -0.022), G(x + size * 0.8, y, -0.022), G(x, y - size, -0.022), G(x - size * 0.8, y, -0.022)]
        plate(scene, name, outline, G(0.0, 0.0, 0.044), 'Bone')
    trim_height(scene, 1.5)


# ===========================================================================
# ================================   TRADE   ================================
# ===========================================================================
#
#   build_stall    A plank table with a heap of fruit and a small box of more.
#   build_awning   Four thin poles under a sagging striped cloth with a scalloped edge.
#   build_crate    Slats over a dark core, corner posts and a plank lid.
#
#   The awning usually stands on top of a stall: nothing on the table is higher than 0.6.
#

def build_stall(scene):
    rng = random.Random(113)
    # Table top: four planks along Z, flat at 0.42
    plank_count = 4
    spacing = 0.94 / plank_count
    for i in range(plank_count):
        x = -0.47 + spacing * (i + 0.5)
        beam(scene, f'Plank{i}', G(x, 0.395, -0.47 + rng.uniform(0.0, 0.02)), G(x, 0.395, 0.47 - rng.uniform(0.0, 0.02)),
             spacing - 0.012, 0.05, 'Paint')
    for i, (sx, sz) in enumerate(CORNERS):
        pole(scene, f'Leg{i}', [G(sx * 0.4, 0.0, sz * 0.4), G(sx * 0.39, 0.375, sz * 0.39)], [0.042, 0.038], 'PaintDark', sides=5, is_level=True)
    for side in (-0.39, 0.39):
        beam(scene, f'Apron{side}', G(-0.39, 0.33, side), G(0.39, 0.33, side), 0.03, 0.07, 'PaintDark')
    # Heap of fruit on the front right, one on top
    heap = [('FruitRed', 0.2, 0.22, 0.055), ('FruitGold', 0.07, 0.3, 0.052), ('FruitGreen', 0.23, 0.08, 0.052),
            ('FruitGold', 0.33, 0.3, 0.05), ('FruitRed', 0.34, 0.17, 0.05)]
    for i, (material_name, x, z, radius) in enumerate(heap):
        fruit(scene, f'Fruit{i}', (x, 0.42 + radius * 0.9, z), radius, material_name)
    fruit(scene, 'FruitTop', (0.21, 0.545, 0.2), 0.05, 'FruitGreen')
    # Small box at the back left with fruit in it
    beam(scene, 'Box', G(-0.2, 0.42, -0.2), G(-0.2, 0.52, -0.2), 0.26, 0.26, 'Wood', rng, 0.006)
    for side in (-1, 1):
        beam(scene, f'BoxRimX{side}', G(-0.2 + side * 0.12, 0.535, -0.33), G(-0.2 + side * 0.12, 0.535, -0.07), 0.025, 0.04, 'Wood')
        beam(scene, f'BoxRimZ{side}', G(-0.33, 0.535, -0.2 + side * 0.12), G(-0.07, 0.535, -0.2 + side * 0.12), 0.025, 0.04, 'Wood')
    fruit(scene, 'BoxFruit0', (-0.24, 0.535, -0.16), 0.052, 'FruitGold')
    fruit(scene, 'BoxFruit1', (-0.15, 0.53, -0.24), 0.05, 'FruitRed')
    trim_height(scene, 0.6)


def awning_height(x, z):
    """Height of the cloth: high at the back (-X), low at the front, sagging in the middle."""
    return 0.955 - (x + 0.5) * 0.21 - 0.035 * math.cos(math.pi * z) * math.sin(math.pi * (x + 0.5))


def build_awning(scene):
    for i, (sx, sz) in enumerate(CORNERS):
        x, z = sx * 0.42, sz * 0.42
        pole(scene, f'Pole{i}', [G(x, 0.0, z), G(x, awning_height(x, z) - 0.004, z)], [0.027, 0.022], 'Wood', sides=5, is_level=True)
    # Cloth in five strips from back to front: paint, stripe, paint, stripe, paint
    depth_steps = 6
    for strip in range(5):
        rows = []
        for step in range(depth_steps + 1):
            z = -0.5 + step / depth_steps
            rows.append([G(x, awning_height(x, z), z) for x in (-0.5 + 0.2 * (strip + share) for share in (0.0, 0.5, 1.0))])
        sheet(scene, f'Cloth{strip}', rows, G(0.0, -0.012, 0.0), 'Bone' if 1 == strip % 2 else 'Paint')
    # Hanging edge at the front, cut into scallops
    scallop_count = 6
    top, bottom = [], []
    for i in range(scallop_count * 2 + 1):
        z = -0.5 + i / (scallop_count * 2)
        edge = awning_height(0.5, z)
        top.append(G(0.5, edge, z))
        bottom.append(G(0.5, edge - (0.14 if 1 == i % 2 else 0.07), z))
    sheet(scene, 'Valance', [bottom, top], G(-0.012, 0.0, 0.0), 'Paint')
    trim_height(scene, 1.0)


def build_crate(scene):
    rng = random.Random(115)
    beam(scene, 'Core', G(0, 0.0, 0), G(0, 0.4, 0), 0.36, 0.36, 'PaintDark')
    # Three slats per side with dark gaps between them
    for row, y in enumerate((0.07, 0.2, 0.33)):
        for side in (-1, 1):
            reach = 0.19 + rng.uniform(-0.004, 0.004)
            beam(scene, f'SlatX{row}{side}', G(side * reach, y, -0.17), G(side * reach, y, 0.17), 0.025, 0.115, 'Paint', rng, 0.004)
            beam(scene, f'SlatZ{row}{side}', G(-0.17, y, side * reach), G(0.17, y, side * reach), 0.025, 0.115, 'Paint', rng, 0.004)
    for i, (sx, sz) in enumerate(CORNERS):
        beam(scene, f'Post{i}', G(sx * 0.188, 0.0, sz * 0.188), G(sx * 0.188, 0.415, sz * 0.188), 0.055, 0.055, 'PaintDark')
    # Lid of three planks, flat on top
    for i in range(3):
        z = -0.14 + 0.14 * i
        beam(scene, f'Lid{i}', G(-0.21 + rng.uniform(0.0, 0.01), 0.415, z), G(0.21 - rng.uniform(0.0, 0.01), 0.415, z), 0.13, 0.03, 'Paint')
    trim_height(scene, 0.5)


# ===========================================================================
# =============================   DECORATION   ==============================
# ===========================================================================
#
#   build_door    A porch leaning on the wall behind: a plank door with battens, a brace and a
#                 bronze ring, posts, lintel, roof, stone step.
#   build_totem   A carved pole segment: face to the front, wings to the sides, rings.
#   build_horns   A horned animal skull as crest of a roof or a totem.
#
#   The totem ends flat at y = 0 and y = 1, its segments stack to a pole.
#

def build_door(scene):
    rng = random.Random(116)
    # Dark opening on the -X face (upright beam: width along Z, height along X)
    beam(scene, 'Door', G(-0.22, 0.0, 0), G(-0.22, 0.84, 0), 0.54, 0.06, 'Door')
    # Door leaf: four uneven planks, the dark opening shows around and between them
    plank_count = 4
    spacing = 0.46 / plank_count
    for i in range(plank_count):
        z = -0.23 + spacing * (i + 0.5)
        beam(scene, f'Plank{i}', G(-0.175, 0.025, z), G(-0.175, 0.79 - rng.uniform(0.0, 0.03), z), spacing - 0.014, 0.035, 'Paint', rng, 0.004)
    # Two battens across the planks and a brace between them
    for name, y in (('BattenLow', 0.2), ('BattenHigh', 0.62)):
        beam(scene, name, G(-0.148, y, -0.225), G(-0.148, y, 0.225), 0.03, 0.075, 'PaintDark', rng, 0.004)
    beam(scene, 'DoorBrace', G(-0.15, 0.245, -0.19), G(-0.15, 0.575, 0.19), 0.024, 0.065, 'PaintDark')
    # Ring handle hanging from a boss
    pole(scene, 'Boss', [G(-0.16, 0.43, 0.15), G(-0.125, 0.43, 0.15)], [0.03, 0.022], 'Bronze', sides=6)
    ring = [G(-0.122, 0.385 + math.cos(k / 8 * math.tau) * 0.046, 0.15 + math.sin(k / 8 * math.tau) * 0.046) for k in range(9)]
    pole(scene, 'Ring', ring, [0.013] * len(ring), 'Bronze', sides=5)
    for side in (-1, 1):
        z = side * 0.31
        pole(scene, f'Post{side}', [G(-0.16, 0.0, z), G(-0.155, 0.45, z * 1.02), G(-0.16, 0.87, z)], [0.052, 0.045, 0.048], 'Paint', sides=6, is_level=True)
        pole(scene, f'Brace{side}', [G(-0.15, 0.55, z), G(0.15, 0.85, z)], [0.024, 0.02], 'Paint', sides=5)
    beam(scene, 'Lintel', G(-0.16, 0.895, -0.44), G(-0.16, 0.895, 0.44), 0.12, 0.1, 'Paint', rng, 0.005)
    # Porch roof: five boards slanting down to the front
    for i in range(5):
        z = -0.36 + 0.18 * i
        lift = 0.006 * (i % 2)
        beam(scene, f'Board{i}', G(-0.245, 0.972 + lift, z), G(0.235 - rng.uniform(0.0, 0.035), 0.856 + lift, z), 0.17, 0.03, 'PaintDark')
    # Stone step in front, flat on the ground
    rock(scene, 'Step', (0.03, 0.03, 0.0), (0.185, 0.05, 0.3), 'Stone', rng, roughness=0.12)
    trim_height(scene, 1.0)


TOTEM_WING = [(0.2, 0.76), (0.5, 0.9), (0.5, 0.76), (0.43, 0.72), (0.48, 0.62), (0.41, 0.58), (0.44, 0.48), (0.36, 0.44), (0.2, 0.4)]   # (z, y)


def build_totem(scene):
    lathe(scene, 'Pole', [(0.29, 0.0), (0.3, 0.3), (0.3, 0.7), (0.29, 1.0)], 'Paint', segments=14, has_top_cap=True)
    lathe(scene, 'FootRing', [(0.335, 0.0), (0.34, 0.06), (0.3, 0.1)], 'PaintDark', segments=14)
    lathe(scene, 'TopRing', [(0.3, 0.9), (0.34, 0.94), (0.335, 1.0), (0.335, 1.0), (0.29, 1.0)], 'PaintDark', segments=14)
    for side in (-1, 1):
        z = side * 0.125
        # Eye: a carved disc with a dark pupil, a slanted brow above
        pole(scene, f'Eye{side}', [G(0.2, 0.66, z), G(0.325, 0.66, z)], [0.085, 0.07], 'Bone', sides=10)
        pole(scene, f'Pupil{side}', [G(0.3, 0.655, z * 0.95), G(0.345, 0.655, z * 0.95)], [0.036, 0.03], 'Door', sides=8)
        beam(scene, f'Brow{side}', G(0.275, 0.82, side * 0.23), G(0.335, 0.755, side * 0.01), 0.05, 0.05, 'PaintDark')
        # Wing: a notched board
        plate(scene, f'Wing{side}', [G(-0.035, y, side * reach) for reach, y in TOTEM_WING], G(0.07, 0.0, 0.0), 'PaintDark')
    # Hooked beak, a dark mouth with teeth below
    pole(scene, 'Beak', [G(0.25, 0.6, 0), G(0.42, 0.5, 0), G(0.44, 0.36, 0)], [0.08, 0.06, 0.0], 'PaintDark', sides=4, is_smooth=False)
    beam(scene, 'Mouth', G(0.285, 0.24, -0.14), G(0.285, 0.24, 0.14), 0.05, 0.075, 'Door')
    for i in range(4):
        z = -0.09 + 0.06 * i
        beam(scene, f'Tooth{i}', G(0.305, 0.215, z), G(0.305, 0.268, z), 0.036, 0.03, 'Bone')
    trim_height(scene, 1.0)


def build_horns(scene):
    ellipsoid(scene, 'Cranium', G(-0.03, 0.145, 0), (0.16, 0.13, 0.14), 'Paint', subdivisions=3)
    # Snout tapering down to the front
    pole(scene, 'Snout', [G(0.02, 0.15, 0), G(0.14, 0.115, 0), G(0.25, 0.082, 0), G(0.29, 0.072, 0)], [0.105, 0.085, 0.062, 0.032], 'Paint', sides=8)
    for side in (-1, 1):
        ellipsoid(scene, f'Socket{side}', G(0.08, 0.185, side * 0.075), (0.045, 0.035, 0.04), 'Door', subdivisions=2)
        # Horn: sweeping out along Z, then up and to the front
        bends = [G(-0.04, 0.2, side * 0.08), G(-0.06, 0.24, side * 0.26), G(-0.04, 0.31, side * 0.4),
                 G(0.02, 0.41, side * 0.46), G(0.08, 0.49, side * 0.44)]
        pole(scene, f'Horn{side}', curve(bends, 7), [0.062, 0.058, 0.052, 0.044, 0.034, 0.02, 0.0], 'Paint', sides=6)
    trim_height(scene, 0.5)
