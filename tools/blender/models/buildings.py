# NOTE:
#   Building blocks of the village (assets/models/building/): walls, roofs, frames,
#   defense, trade and decoration, one model per block of the building designer.
#   Every block stands in its own frame: +Y up, +X is its front, the origin is the center
#   of its footprint on the ground. One grid cell is 0.5 units, a block stays inside its
#   box (width along X x height x depth along Z) and ends flat where the next one stacks.
#   No rig. The game recolors by material name: Paint (the surface the player repaints),
#   PaintDark (its trim), Wood, Stone, Ember, Bone, Door, Flame, Rope, FruitRed, FruitGold,
#   FruitGreen. Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only building/

#
#   Chapters:
#     PRIMITIVES   Hand-hewn poles, beams, lathed rings, plates, cloth and rocks.
#     WALLS        Round room, square room and round tower.
#     ROOFS        Thatched cones and the gable roof.
#     FRAMES       Stilts, platform and ladder.
#     DEFENSE      Palisade, torch and banner.
#     TRADE        Stall, awning and crate.
#     DECORATION   Door porch, totem segment and horned skull.
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
#   build_round_room    Wattle and daub: a round wall, upright poles, woven withies, rims.
#   build_square_room   Upright planks between four corner logs, sill, band and top plate.
#   build_round_tower   A slim tower with courses of rough stones bulging out of the wall.
#
#   All three end flat at y = 1, the next block stands on them.
#

def build_round_room(scene):
    rng = random.Random(101)
    pole_count = 12
    segments = pole_count * 2            # every other segment sits on a pole
    # Daub wall, uneven below its flat top
    lathe(scene, 'Wall', [(0.9, 0.0), (0.915, 0.5), (0.9, 1.0)], 'Paint', segments=segments,
          bulge=lambda k, j: 0.0 if 2 == j else rng.uniform(-0.012, 0.012), has_top_cap=True)
    # Upright poles, each a little bent
    for i in range(pole_count):
        angle = (i + 0.5) / pole_count * math.tau
        bend = angle + rng.uniform(-0.025, 0.025)
        points = [G(math.cos(angle) * 0.925, 0.0, math.sin(angle) * 0.925),
                  G(math.cos(bend) * 0.925, 0.5, math.sin(bend) * 0.925),
                  G(math.cos(angle) * 0.925, 1.0, math.sin(angle) * 0.925)]
        pole(scene, f'Pole{i}', points, [0.045, 0.04, 0.043], 'PaintDark', sides=5, is_level=True)
    # Withies woven around the poles: in front of one pole, behind the next
    for row, y in enumerate((0.36, 0.66)):
        def weave(k, j, row=row):
            if 0 == k % 2:
                return 0.0
            return 0.045 if 0 == (k // 2 + row) % 2 else -0.035
        lathe(scene, f'Withy{row}', [(0.915, y - 0.03), (0.94, y), (0.915, y + 0.03)], 'PaintDark', segments=segments, bulge=weave)
    # Rims at foot and top
    lathe(scene, 'FootRim', [(0.985, 0.0), (0.995, 0.05), (0.97, 0.11), (0.9, 0.12)], 'PaintDark', segments=segments)
    lathe(scene, 'TopRim', [(0.9, 0.88), (0.97, 0.89), (0.995, 0.95), (0.985, 1.0), (0.985, 1.0), (0.9, 1.0)], 'PaintDark', segments=segments)
    trim_height(scene, 1.0)


def build_square_room(scene):
    rng = random.Random(102)
    # Core behind the planks, its top is the flat top of the room
    beam(scene, 'Core', G(0, 0.0, 0), G(0, 1.0, 0), 1.74, 1.74, 'Paint')
    # Six upright planks per side, uneven in width and depth
    plank_count = 6
    spacing = 1.72 / plank_count
    for side_x, side_z in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        for i in range(plank_count):
            slide = -0.86 + spacing * (i + 0.5)
            depth = 0.885 + rng.uniform(-0.012, 0.012)
            width = spacing - rng.uniform(0.015, 0.035)
            x = side_x * depth + abs(side_z) * slide
            z = side_z * depth + abs(side_x) * slide
            # Upright beam: width along Z, height along X
            size = (width, 0.05) if 0 != side_x else (0.05, width)
            beam(scene, f'Plank{side_x}{side_z}{i}', G(x, 0.0, z), G(x, 1.0, z), size[0], size[1], 'Paint')
    # Corner logs
    for i, (sx, sz) in enumerate(CORNERS):
        points = [G(sx * 0.92, 0.0, sz * 0.92), G(sx * 0.92, 0.5, sz * 0.92), G(sx * 0.92, 1.0, sz * 0.92)]
        pole(scene, f'Post{i}', points, [0.078, 0.07, 0.075], 'PaintDark', sides=6, is_level=True)
    # Sill, band and top plate around the planks
    for name, y, reach, thickness, tall in (('Sill', 0.05, 0.925, 0.06, 0.1), ('Band', 0.56, 0.925, 0.05, 0.08), ('Plate', 0.955, 0.93, 0.08, 0.09)):
        for side in (-1, 1):
            beam(scene, f'{name}X{side}', G(side * reach, y, -0.9), G(side * reach, y, 0.9), thickness, tall, 'PaintDark', rng, 0.006)
            beam(scene, f'{name}Z{side}', G(-0.9, y, side * reach), G(0.9, y, side * reach), thickness, tall, 'PaintDark', rng, 0.006)
    trim_height(scene, 1.0)


def build_round_tower(scene):
    rng = random.Random(103)
    lathe(scene, 'Wall', [(0.42, 0.0), (0.42, 1.0)], 'Paint', segments=12, has_top_cap=True)
    # Four courses of three stones, every course turned against the one below
    for course, y in enumerate((0.2, 0.39, 0.58, 0.77)):
        for i in range(3):
            angle = (i + 0.5 * course) / 3 * math.tau + rng.uniform(-0.25, 0.25)
            center = (math.cos(angle) * 0.425, y + rng.uniform(-0.03, 0.03), math.sin(angle) * 0.425)
            size = (rng.uniform(0.045, 0.055), rng.uniform(0.065, 0.085), rng.uniform(0.12, 0.17))
            rock(scene, f'Stone{course}{i}', center, size, 'PaintDark', rng, turn=angle)
    # Foot course and crown
    lathe(scene, 'Foot', [(0.475, 0.0), (0.475, 0.07), (0.42, 0.11)], 'PaintDark', segments=12)
    lathe(scene, 'Crown', [(0.42, 0.87), (0.475, 0.9), (0.475, 1.0), (0.475, 1.0), (0.42, 1.0)], 'PaintDark', segments=12)
    trim_height(scene, 1.0)


# ===========================================================================
# ================================   ROOFS   ================================
# ===========================================================================
#
#   thatch_cone             Tiers of thatch with scalloped lips, a bound band per tier, a tuft.
#   build_cone_roof         The wide cone over a room, three tiers.
#   build_small_cone_roof   The small cone over a tower, two tiers.
#   build_gable_roof        Courses of shingles on a prism, barge boards and a ridge log.
#
#   The roofs overhang their footprint: the cones to radius 1.3 and 0.72, the gable
#   by 0.2 on each side. All are 1 high.
#

THATCH_OVERLAP = 0.08                                 # how far a tier hangs over the one below (height)
THATCH_FLARE = 0.07                                   # how far its lip stands off the tier below
THATCH_TUFT = [(0.06, 0.9), (0.09, 0.965), (0.05, 1.0), (0.0, 1.0)]


def thatch_cone(scene, rng, radius, tiers, segments):
    """
    A cone from radius at y = 0 to a bound tuft at y = 1, closed below. Every tier is
    its own skirt: lip below (scalloped, bundles of straw), tucked under the next above.
    """
    scallop = [0.03 * (1 if 0 == k % 2 else -1) + rng.uniform(-0.01, 0.01) for k in range(segments)]
    for tier in range(tiers):
        is_first, is_last = 0 == tier, tiers - 1 == tier
        low = tier / tiers - (0.0 if is_first else THATCH_OVERLAP)
        edge = radius * (1.0 - low) + (0.0 if is_first else THATCH_FLARE)
        lip = (edge, low + 0.03)
        rest = THATCH_TUFT if is_last else [(radius * (1.0 - (tier + 1) / tiers) - 0.02, (tier + 1) / tiers)]
        lathe(scene, f'Thatch{tier}', [(edge - 0.1, low), lip, lip] + rest, 'Paint', segments=segments,
              bulge=lambda k, j: scallop[k] if j <= 2 else 0.0, has_bottom_cap=is_first)
        # Bound band a third up the tier, lying on its slope
        start, end = Vector(lip), Vector(rest[0])
        along = (end - start).normalized()
        outward = Vector((along.y, -along.x))
        middle = start.lerp(end, 0.35)
        band = [middle - along * 0.035 - outward * 0.005, middle + outward * 0.03, middle + along * 0.035 - outward * 0.005]
        lathe(scene, f'Band{tier}', [tuple(point) for point in band], 'PaintDark', segments=segments)
    lathe(scene, 'Knot', [(0.055, 0.87), (0.088, 0.9), (0.055, 0.93)], 'PaintDark', segments=8)
    trim_height(scene, 1.0)


def build_cone_roof(scene):
    thatch_cone(scene, random.Random(104), radius=1.255, tiers=3, segments=24)


def build_small_cone_roof(scene):
    thatch_cone(scene, random.Random(106), radius=0.675, tiers=2, segments=14)


def build_gable_roof(scene):
    rng = random.Random(105)
    # Body below the shingles, its ends are the gables
    plate(scene, 'Body', [G(-1.1, 0.0, -1.0), G(1.1, 0.0, -1.0), G(0.0, 0.9, -1.0)], G(0.0, 0.0, 2.0), 'PaintDark')
    # The slope in (x, y): from the eave up to the ridge
    eave, ridge = Vector((1.14, 0.04)), Vector((0.02, 0.93))
    along = (ridge - eave).normalized()
    outward = Vector((along.y, -along.x))
    course_count, shingle_count, half_depth = 4, 6, 1.1
    step = 2.0 * half_depth / shingle_count
    for course in range(course_count):
        # Every course lies with its lower end on the upper end of the one below
        low = eave.lerp(ridge, course / course_count) + outward * 0.03
        high = eave.lerp(ridge, min(1.0, (course + 1) / course_count + 0.05))
        # Every other course is shifted by half a shingle
        if 0 == course % 2:
            cuts = [-half_depth + step * i for i in range(shingle_count + 1)]
        else:
            cuts = [-half_depth] + [-half_depth + step * (i + 0.5) for i in range(shingle_count)] + [half_depth]
        for side in (1, -1):
            for i, (near, far) in enumerate(zip(cuts, cuts[1:])):
                start = low - along * rng.uniform(0.0, 0.03)
                z = (near + far) / 2
                beam(scene, f'Shingle{course}{side}{i}', G(side * start.x, start.y, z), G(side * high.x, high.y, z),
                     far - near - 0.015, rng.uniform(0.035, 0.045), 'Paint')
    # Barge boards over the shingle ends, ridge log on top
    for side in (1, -1):
        for z in (-1.125, 1.125):
            beam(scene, f'Barge{side}{z}', G(side * 1.15, 0.055, z), G(0.0, 0.94, z), 0.05, 0.08, 'PaintDark')
    pole(scene, 'Ridge', [G(0.0, 0.953, -1.18), G(0.0, 0.953, 1.18)], [0.054, 0.05], 'PaintDark', sides=6)
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
#   build_door    A porch leaning on the wall behind: door, posts, lintel, roof, stone step.
#   build_totem   A carved pole segment: face to the front, wings to the sides, rings.
#   build_horns   A horned animal skull as crest of a roof or a totem.
#
#   The totem ends flat at y = 0 and y = 1, its segments stack to a pole.
#

def build_door(scene):
    rng = random.Random(116)
    # Dark door on the -X face (upright beam: width along Z, height along X)
    beam(scene, 'Door', G(-0.22, 0.0, 0), G(-0.22, 0.82, 0), 0.52, 0.06, 'Door')
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
