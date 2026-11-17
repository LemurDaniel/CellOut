# NOTE:
#   Building blocks of the village (assets/models/building/), the home group of the
#   building designer: a field stone room, a small timber annex, three more roofs (dome,
#   hip, flat), an attached window with a flower box and a village well.
#   Same frame as models/buildings.py: +Y up, +X is the front of a block, the origin is the
#   center of its footprint on the ground, one grid cell is 0.5 units, a block stays inside
#   its box and ends flat where the next one stacks. No rig. Materials Paint (the surface
#   the player repaints) and PaintDark (its trim), the rest are fixed colors.
#   Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only building/
#   ./tools/blender/Show-Model.ps1 -Module new_blocks_home -Call 'build_well(scene)' -Name well

#
#   Chapters:
#     SHAPES   Field stones and the square outline for lathed slabs and pyramids.
#     ROOMS    Stone room (4 x 2 x 4 cells) and small timber room (2 x 2 x 2).
#     ROOFS    Dome roof (4 x 2 x 4), hip roof (4 x 2 x 4) and flat earth roof (4 x 1 x 4).
#     EXTRAS   Window with shutters and flower box (1 x 2 x 2) and the well (2 x 2 x 2).
#

import math
import random

import bmesh
import bpy
from mathutils import Vector

from models.buildings import beam, fruit, lathe, plate, pole, trim_height
from models.helpers import G, ellipsoid, link_mesh

SIDES = ((1, 0), (-1, 0), (0, 1), (0, -1))            # (x, z) directions out of the four walls of a square block
CORNERS = ((1, 1), (1, -1), (-1, 1), (-1, -1))        # signs of the four corners of a footprint


# ===========================================================================
# ===============================   SHAPES   ================================
# ===========================================================================
#
#   fieldstone   A rough block lying on a wall: uneven outline, sunk into the wall, bevelled face.
#   stone_row    One course of long and short field stones along a straight wall, some split in two slabs.
#   squared      A bulge for lathe that pulls every ring out to a square.
#
#   A field stone has a quarter of the triangles of buildings.rock, a whole wall of them
#   stays low poly.
#

STONE_OUTLINE = ((-1, -1), (0, -1), (1, -1), (1, 1), (0, 1), (-1, 1))   # a block: four corners, a kink in its bed and its back
STONE_SUNK = 0.03                                     # how deep a stone sits in the wall behind it
STONE_FACE = 0.8                                      # size of its front face against its outline
STONE_GAP = 0.011                                     # half the mortar joint between two stones of a course
STONE_SPLIT = 0.3                                     # share of the stones of a course that are two thin slabs instead


def fieldstone(scene, name, center, out, half_width, half_height, depth, material_name, rng):
    """
    A flat shaded stone on a wall. center (Godot x, y, z) lies on the wall surface, out
    is the level direction (x, z) away from the wall. The uneven outline stands
    half_width x half_height in the wall plane, the stone reaches depth out of the
    wall and ends in a smaller, uneven face.
    """
    outline = []
    for across, above in STONE_OUTLINE:
        # Corners are knocked off unevenly, the kinks wander along bed and back
        slide = across * rng.uniform(0.84, 1.0) if 0 != across else rng.uniform(-0.35, 0.35)
        outline.append((slide * half_width, above * half_height * rng.uniform(0.86, 1.0)))
    bm = bmesh.new()
    rings = []
    for reach, share in ((-STONE_SUNK, 1.0), (depth, STONE_FACE)):
        ring = []
        for slide, lift in outline:
            # Only the front face is uneven in depth
            off = reach + (rng.uniform(-0.3, 0.1) * depth if share < 1.0 else 0.0)
            ring.append(bm.verts.new(G(center[0] + out[0] * off - out[1] * slide * share,
                                       center[1] + lift * share,
                                       center[2] + out[1] * off + out[0] * slide * share)))
        rings.append(ring)
    for lower, upper in zip(rings, rings[1:]):
        for k in range(len(outline)):
            following = (k + 1) % len(outline)
            bm.faces.new((lower[k], lower[following], upper[following], upper[k]))
    bm.faces.new(rings[0])
    bm.faces.new(rings[-1])
    # Closed solid, faces outwards
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if bm.calc_volume(signed=True) < 0.0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return link_mesh(scene, name, mesh, material_name)


def stone_row(scene, name, out, reach, low, high, y, half_height, stone_width, material_name, rng):
    """
    Field stones side by side on the wall that looks along out (x, z) and stands reach
    away from the middle, from slide low to high across the wall at height y. A span
    shorter than a third of a stone stays empty. Every stone is half_height high.
    """
    length = high - low
    if length < stone_width / 3:
        return
    count = max(1, round(length / stone_width))
    # Long and short stones: every stone gets its share of the span
    shares = [rng.uniform(0.6, 1.5) for _ in range(count)]
    cuts = [low + length * sum(shares[:i]) / sum(shares) for i in range(count + 1)]
    for i, (near, far) in enumerate(zip(cuts, cuts[1:])):
        slide = (near + far) / 2
        half_width = (far - near) / 2 - STONE_GAP
        # Now and then two thin slabs lie where one stone would
        if rng.random() < STONE_SPLIT:
            cut = rng.uniform(-0.2, 0.2) * half_height
            layers = ((y - (half_height - cut) / 2 - STONE_GAP / 2, (half_height + cut) / 2 - STONE_GAP / 2),
                      (y + (half_height + cut) / 2 + STONE_GAP / 2, (half_height - cut) / 2 - STONE_GAP / 2))
        else:
            layers = ((y, half_height),)
        for layer, (height, half) in enumerate(layers):
            center = (out[0] * reach - out[1] * slide, height, out[1] * reach + out[0] * slide)
            fieldstone(scene, f'{name}{i}{layer}', center, out, half_width * rng.uniform(0.94, 1.0), half, rng.uniform(0.045, 0.095), material_name, rng)


def squared(profile, segments, power=None):
    """
    A bulge for lathe with that profile and segment count: every ring becomes a square
    with its sides across X and Z, the profile radius is half its side. power rounds
    the corners (superellipse, 4 soft .. 10 tight), without it they are sharp (needs a
    segment count that is a multiple of 8, so a vertex lands on every corner).
    """
    def bulge(k, j):
        angle = k / segments * math.tau
        cos, sin = abs(math.cos(angle)), abs(math.sin(angle))
        if power is None:
            return profile[j][0] * (1.0 / max(cos, sin) - 1.0)
        return profile[j][0] * ((cos ** power + sin ** power) ** (-1.0 / power) - 1.0)
    return bulge


# ===========================================================================
# ================================   ROOMS   ================================
# ===========================================================================
#
#   build_stone_room   Dry field stone: staggered courses of rough stones over a plastered
#                      core, corner blocks, a timber ring on top, a small deep window on
#                      each flank.
#   build_small_room   A timber annex half as wide: level planks between corner posts, a
#                      shuttered window on the right flank, a brace across the left one and the back.
#
#   Both end flat and closed at y = 1, the next block stands on them. The front (+X) of
#   the stone room has no opening: the door block stands there.
#

STONE_COURSES = ((0.1, 0.085), (0.3, 0.1), (0.52, 0.1), (0.74, 0.095))   # middle and half height of the four stone courses
STONE_WINDOW_COURSE = 2                               # the course the windows sit in
STONE_WINDOWS = {(0, 1): -0.3, (0, -1): 0.28}         # wall direction -> where its window sits across the wall


def build_stone_room(scene):
    rng = random.Random(401)
    core, face = 0.885, 0.97             # half side of the plastered core, how far the stones stand out
    # Plastered core between the stones, its top is the flat top of the room
    beam(scene, 'Core', G(0, 0.0, 0), G(0, 1.0, 0), core * 2.0, core * 2.0, 'Paint')
    # Corner blocks: long and short side swap from course to course, like bonded masonry
    long, short = 0.36, 0.23
    for course, (y, half) in enumerate(STONE_COURSES):
        along_x, along_z = (long, short) if 0 == course % 2 else (short, long)
        for i, (sx, sz) in enumerate(CORNERS):
            x, z = sx * (face - along_x / 2), sz * (face - along_z / 2)
            beam(scene, f'Corner{course}{i}', G(x, y - half + 0.005, z), G(x, y + half - 0.005, z), along_z, along_x, 'PaintDark', rng, 0.016)
    # Courses between the corner blocks, a gap where a window sits
    for side, out in enumerate(SIDES):
        window = STONE_WINDOWS.get(out)
        for course, (y, half) in enumerate(STONE_COURSES):
            # The corner block takes its long side from the walls along X on even courses
            is_long = (0 == course % 2) == (0 == out[0])
            inset = (long if is_long else short) + 0.012
            low, high = -face + inset, face - inset
            name = f'Stone{side}{course}'
            if window is not None and STONE_WINDOW_COURSE == course:
                stone_row(scene, f'{name}A', out, core, low, window - 0.18, y, half, 0.36, 'PaintDark', rng)
                stone_row(scene, f'{name}B', out, core, window + 0.18, high, y, half, 0.36, 'PaintDark', rng)
            else:
                stone_row(scene, name, out, core, low, high, y, half, 0.36, 'PaintDark', rng)
    # Windows: a dark recess, a timber lintel above and a stone sill below
    for (ox, oz), slide in STONE_WINDOWS.items():
        y, half = STONE_COURSES[STONE_WINDOW_COURSE]
        x, z = -oz * slide, ox * slide

        def at(reach, across, height):
            return G(x + ox * reach - oz * across, height, z + oz * reach + ox * across)
        beam(scene, f'Window{oz}', at(core + 0.012, 0.0, y - half), at(core + 0.012, 0.0, y + half), 0.05, 0.34, 'Door')
        beam(scene, f'Lintel{oz}', at(core + 0.045, -0.27, y + half + 0.01), at(core + 0.045, 0.27, y + half + 0.01), 0.12, 0.07, 'Wood', rng, 0.007)
        beam(scene, f'WindowSill{oz}', at(core + 0.05, -0.2, y - half - 0.005), at(core + 0.05, 0.2, y - half - 0.005), 0.13, 0.045, 'PaintDark', rng, 0.01)
    # Heavy timber ring on the stones: the long beams along Z, the short ones fit between them
    ring_y, ring_tall, ring_reach, ring_thick = 0.93, 0.14, 0.94, 0.1
    for side in (-1, 1):
        beam(scene, f'RingZ{side}', G(side * ring_reach, ring_y, -0.995), G(side * ring_reach, ring_y, 0.995), ring_thick, ring_tall, 'Wood', rng, 0.007)
        beam(scene, f'RingX{side}', G(-0.89, ring_y, side * ring_reach), G(0.89, ring_y, side * ring_reach), ring_thick, ring_tall, 'Wood', rng, 0.007)
    trim_height(scene, 1.0)


def build_small_room(scene):
    rng = random.Random(402)
    # Core behind the planks, its top is the flat top of the room
    beam(scene, 'Core', G(0, 0.0, 0), G(0, 1.0, 0), 0.84, 0.84, 'Paint')
    # Five level planks per side, uneven in height and depth
    plank_count = 5
    spacing = 0.8 / plank_count
    for side_x, side_z in SIDES:
        for i in range(plank_count):
            y = 0.1 + spacing * (i + 0.5)
            # Every other plank stands a little proud, like overlapping weather boards
            depth = 0.432 + 0.01 * (i % 2) + rng.uniform(-0.005, 0.005)
            tall = spacing - rng.uniform(0.012, 0.03)
            start = G(side_x * depth - abs(side_z) * 0.42, y, side_z * depth - abs(side_x) * 0.42)
            end = G(side_x * depth + abs(side_z) * 0.42, y, side_z * depth + abs(side_x) * 0.42)
            beam(scene, f'Plank{side_x}{side_z}{i}', start, end, 0.045, tall, 'Paint', rng, 0.006)
    # Corner posts
    for i, (sx, sz) in enumerate(CORNERS):
        points = [G(sx * 0.44, 0.0, sz * 0.44), G(sx * 0.44 + rng.uniform(-0.008, 0.008), 0.5, sz * 0.44), G(sx * 0.44, 1.0, sz * 0.44)]
        pole(scene, f'Post{i}', points, [0.058, 0.052, 0.055], 'PaintDark', sides=6, is_level=True)
    # Sill and top plate around the planks
    for name, y, reach, thickness, tall in (('Sill', 0.05, 0.45, 0.06, 0.1), ('Plate', 0.955, 0.455, 0.07, 0.09)):
        for side in (-1, 1):
            beam(scene, f'{name}X{side}', G(side * reach, y, -0.44), G(side * reach, y, 0.44), thickness, tall, 'PaintDark', rng, 0.005)
            beam(scene, f'{name}Z{side}', G(-0.44, y, side * reach), G(0.44, y, side * reach), thickness, tall, 'PaintDark', rng, 0.005)
    # Window on the right flank (+Z): dark opening, frame, one shutter flat on the wall, one ajar
    left, right, low, high, wall = -0.2, 0.04, 0.44, 0.76, 0.46
    middle, mid_height = (left + right) / 2, (low + high) / 2
    beam(scene, 'Opening', G(middle, low, wall - 0.004), G(middle, high, wall - 0.004), 0.03, right - left, 'Door')
    for name, x in (('JambLeft', left), ('JambRight', right)):
        beam(scene, name, G(x, low - 0.03, wall + 0.01), G(x, high + 0.03, wall + 0.01), 0.06, 0.045, 'PaintDark')
    for name, y in (('Head', high + 0.025), ('WindowSill', low - 0.03)):
        beam(scene, name, G(left - 0.05, y, wall + 0.018), G(right + 0.05, y, wall + 0.018), 0.075, 0.05, 'PaintDark', rng, 0.004)
    shutters = (('ShutterOpen', (right + 0.03, wall + 0.012), (right + 0.03 + 0.24, wall + 0.012)),
                ('ShutterAjar', (left + 0.025, wall + 0.03), (left + 0.025 + 0.13, wall + 0.06)))
    for name, (x0, z0), (x1, z1) in shutters:
        beam(scene, name, G(x0, mid_height, z0), G(x1, mid_height, z1), 0.028, high - low - 0.02, 'Paint', rng, 0.004)
        for y in (low + 0.07, high - 0.07):
            beam(scene, f'{name}Batten{y}', G(x0 + 0.01, y, z0 + 0.016), G(x1 - 0.01, y, z1 + 0.016), 0.02, 0.045, 'PaintDark')
    # Braces across the planks of the left flank and the back
    pole(scene, 'BraceFlank', [G(-0.37, 0.14, -0.482), G(0.0, 0.5, -0.488), G(0.37, 0.86, -0.482)], [0.03, 0.028, 0.026], 'PaintDark', sides=5)
    pole(scene, 'BraceBack', [G(-0.482, 0.14, 0.37), G(-0.488, 0.5, 0.0), G(-0.482, 0.86, -0.37)], [0.03, 0.028, 0.026], 'PaintDark', sides=5)
    trim_height(scene, 1.0)


# ===========================================================================
# ================================   ROOFS   ================================
# ===========================================================================
#
#   build_dome_roof   A hide dome over bent ribs for the round room: sagging skin, a rope
#                     ring and a zigzag net, a smoke ring with pole ends on top.
#   build_hip_roof    A four-sided pyramid of shingle courses for the square rooms, hip poles
#                     on the four ridges, a carved finial on the tip.
#   build_flat_roof   A thick earth slab with a rolled rim on round log beams that stick out
#                     of both flanks, a wooden spout at the front.
#
#   The dome overhangs its footprint to radius 1.25, the hip roof by 0.2 on each side, both
#   are 1 high. The flat roof is 0.5 high, its top is flat and closed: blocks stand on it.
#

DOME_RIBS = 12
DOME_RADIUS = 1.17                                    # radius of the skin at the eave
DOME_FOOT, DOME_RISE = 0.05, 0.85                     # height of the eave, how far the dome rises above it
DOME_ANGLES = (0, 13, 26, 39, 52, 64, 74, 79.5)       # degrees up the dome, one ring of the skin each
DOME_ROPE_LOW, DOME_ROPE_HIGH = 26, 56                # degrees up the dome the rope net hangs between


def dome_point(degrees, lift=0.0):
    """(radius, y) on the dome that many degrees up from the eave, lift stands off the skin."""
    angle = math.radians(degrees)
    return ((DOME_RADIUS + lift) * math.cos(angle), DOME_FOOT + (DOME_RISE + lift) * math.sin(angle))


def build_dome_roof(scene):
    rng = random.Random(403)
    segments = DOME_RIBS * 2             # every other segment lies on a rib
    # Skin: closed below, sagging between the ribs, uneven where it is stretched
    skin = [(DOME_RADIUS - 0.03, 0.0)] + [dome_point(degrees) for degrees in DOME_ANGLES]
    sag = [0.0 if 0 == k % 2 else rng.uniform(-0.04, -0.022) for k in range(segments)]
    lathe(scene, 'Skin', skin, 'Paint', segments=segments, has_bottom_cap=True,
          bulge=lambda k, j: sag[k] * math.sin(math.pi * min(1.0, (j - 1) / 6)) if j >= 1 else 0.0)
    # Hem rolled around the eave
    lathe(scene, 'Hem', [(DOME_RADIUS - 0.01, 0.0), (DOME_RADIUS + 0.035, 0.03), (DOME_RADIUS + 0.03, 0.085), (DOME_RADIUS - 0.03, 0.12)],
          'PaintDark', segments=segments)
    # Bent ribs over the skin, every other one ends as a pole above the smoke ring
    for i in range(DOME_RIBS):
        angle = i / DOME_RIBS * math.tau
        bend = rng.uniform(-0.02, 0.02)
        profile = [(DOME_RADIUS + 0.04, 0.0)] + [dome_point(degrees, 0.012) for degrees in DOME_ANGLES[1:]]
        if 0 == i % 2:
            profile.append((0.31, 1.0))
        points = []
        for j, (radius, y) in enumerate(profile):
            turn = angle + bend * math.sin(math.pi * j / (len(profile) - 1))
            points.append(G(math.cos(turn) * radius, y, math.sin(turn) * radius))
        pole(scene, f'Rib{i}', points, [0.036] + [0.032] * (len(points) - 2) + [0.03], 'PaintDark', sides=5)
    # Rope ring from rib to rib low on the dome
    radius, y = dome_point(DOME_ROPE_LOW, 0.05)
    lathe(scene, 'RopeRing', [(radius - 0.02, y - 0.028), (radius + 0.012, y), (radius - 0.02, y + 0.028)], 'Rope', segments=DOME_RIBS)
    # Rope net above it: zigzag from rib to rib, every leg lies on the skin in its middle
    points = []
    for i in range(DOME_RIBS + 1):
        degrees = DOME_ROPE_LOW if 0 == i % 2 else DOME_ROPE_HIGH
        for part, (step, height, lift) in enumerate(((0.0, degrees, 0.052), (0.5, (DOME_ROPE_LOW + DOME_ROPE_HIGH) / 2, 0.03))):
            if DOME_RIBS == i and 1 == part:
                break
            angle = (i + step) / DOME_RIBS * math.tau
            radius, y = dome_point(height, lift)
            points.append(G(math.cos(angle) * radius, y, math.sin(angle) * radius))
    pole(scene, 'RopeNet', points, [0.015] * len(points), 'Rope', sides=4)
    # Smoke ring: the ribs meet in a wooden hoop, dark inside
    lathe(scene, 'SmokeRing', [(0.25, 0.84), (0.33, 0.89), (0.3, 0.95), (0.22, 0.96), (0.2, 0.89)], 'PaintDark', segments=DOME_RIBS)
    lathe(scene, 'SmokeHole', [(0.21, 0.88), (0.21, 0.905)], 'Door', segments=DOME_RIBS, has_top_cap=True)
    trim_height(scene, 1.0)


HIP_EAVE = 1.13                                       # half side of the roof at the eave
HIP_EAVE_Y, HIP_TIP_Y = 0.04, 0.82                    # height of the eave and of the point the four slopes meet in
HIP_SHINGLES = (6, 4, 3, 1)                           # shingles per course from the eave up, they never line up
HIP_OVERLAP = 0.06                                    # how far a course reaches under the next one (share of the slope)


def build_hip_roof(scene):
    rng = random.Random(404)
    # Body below the shingles, closed below
    body = [(1.07, 0.0), (0.0, 0.77)]
    lathe(scene, 'Body', body, 'PaintDark', segments=8, bulge=squared(body, 8), is_smooth=False, has_bottom_cap=True)
    # The slope in (reach, y) and the direction standing on it
    rise = HIP_TIP_Y - HIP_EAVE_Y
    normal = Vector((rise, HIP_EAVE)).normalized()
    course_count = len(HIP_SHINGLES)
    for side, (ox, oz) in enumerate(SIDES):
        def at(slide, share, lift=0.0):
            reach = HIP_EAVE * (1.0 - share) + normal.x * lift
            return G(ox * reach - oz * slide, HIP_EAVE_Y + rise * share + normal.y * lift, oz * reach + ox * slide)
        thickness = G(ox * normal.x, normal.y, oz * normal.x) * -0.045
        for course, count in enumerate(HIP_SHINGLES):
            # A course is a trapezoid between the two hips, its upper end lies under the next course
            low = course / course_count
            high = min(0.97, (course + 1) / course_count + HIP_OVERLAP)
            half_low, half_high = HIP_EAVE * (1.0 - low), HIP_EAVE * (1.0 - high)
            cuts = [-half_low + 2.0 * half_low * i / count + (rng.uniform(-0.02, 0.02) if 0 < i < count else 0.0) for i in range(count + 1)]
            for i, (near, far) in enumerate(zip(cuts, cuts[1:])):
                # The joint between two shingles, none at the hips
                near += 0.008 if i > 0 else 0.0
                far -= 0.008 if i < count - 1 else 0.0
                drop = low - rng.uniform(0.0, 0.02)
                top_near, top_far = max(near, -half_high), min(far, half_high)
                outline = [at(near, drop, 0.035), at(far, drop, 0.035), at(top_far, high), at(top_near, high)]
                plate(scene, f'Shingle{side}{course}{i}', outline, thickness, 'Paint')
    # Hip poles on the four ridges, a little bent
    for i, (sx, sz) in enumerate(CORNERS):
        points = []
        for share in (-0.02, 0.5, 0.96):
            reach = HIP_EAVE * (1.0 - share) + 0.012 + (rng.uniform(-0.012, 0.012) if 0.5 == share else 0.0)
            points.append(G(sx * reach, HIP_EAVE_Y + rise * share + 0.045, sz * reach))
        pole(scene, f'Hip{i}', points, [0.055, 0.05, 0.042], 'PaintDark', sides=6)
    # Carved finial over the ends of the hip poles: collar, neck, knob and a point
    lathe(scene, 'Finial', [(0.17, 0.69), (0.2, 0.745), (0.11, 0.785), (0.075, 0.835), (0.14, 0.885), (0.125, 0.93), (0.045, 0.96), (0.0, 1.0)],
          'PaintDark', segments=8)
    trim_height(scene, 1.0)


FLAT_VIGAS = (-0.66, -0.33, 0.0, 0.33, 0.66)          # where the five log beams lie along X
FLAT_CORNER = 14.0                                    # how tight the corners of the slab are (superellipse power)


def build_flat_roof(scene):
    rng = random.Random(405)
    segments = 32
    # Earth slab: flat below, rolled rim, a groove inside the rim, flat and closed on top
    # (one connected shell without hard edges: its faces turn the right way only as one piece)
    slab = [(0.885, 0.0), (0.895, 0.03), (0.895, 0.33), (0.915, 0.38), (0.915, 0.46), (0.885, 0.5), (0.8, 0.5), (0.775, 0.472), (0.75, 0.5)]
    square = squared(slab, segments, FLAT_CORNER)
    # Hand-smoothed clay: the walls wander a little, the top stays level
    dent = [[rng.uniform(-0.008, 0.008) for _ in range(segments)] for _ in range(2)]
    lathe(scene, 'Slab', slab, 'Paint', segments=segments, has_top_cap=True, has_bottom_cap=True,
          bulge=lambda k, j: square(k, j) + (dent[j - 2][k] if j in (2, 3) else 0.0))
    # Round log beams through the slab, their cut ends stick out of both flanks unevenly
    for i, x in enumerate(FLAT_VIGAS):
        near, far = -rng.uniform(1.0, 1.045), rng.uniform(1.0, 1.045)
        radius = rng.uniform(0.064, 0.074)
        points = [G(x, 0.24, near), G(x + rng.uniform(-0.01, 0.01), 0.235, 0.0), G(x, 0.24, far)]
        pole(scene, f'Viga{i}', points, [radius, radius * 1.04, radius * 0.96], 'Wood', sides=7)
    # Spout draining the roof to the front: a hollowed half log through the rim
    for name, z, width, y in (('SpoutSide0', 0.345, 0.03, 0.44), ('SpoutSide1', 0.455, 0.03, 0.44), ('SpoutFloor', 0.4, 0.13, 0.415)):
        beam(scene, name, G(0.8, y, z), G(1.045, y - 0.03, z), width, 0.04 if 'SpoutFloor' != name else 0.03, 'Wood')
    trim_height(scene, 0.5)


# ===========================================================================
# ===============================   EXTRAS   ================================
# ===========================================================================
#
#   build_window   Leans on the wall behind (-X) like the door: a framed dark opening, two
#                  open shutters, a sill, a flower box on brackets, a weather board above.
#   build_well     A round wall of field stones with cap stones, two posts under a small
#                  gable roof, a crank roller with rope and bucket, dark water in the shaft.
#

WINDOW_LOW, WINDOW_HIGH = 0.41, 0.85                  # the opening of the attached window (y)
WINDOW_HALF = 0.2                                     # half its width (z)
WINDOW_FLOWERS = (('FruitRed', -0.23, 0.03, 0.0), ('FruitGold', -0.16, -0.03, 0.03), ('FruitRed', -0.09, 0.05, -0.01), ('FruitGold', -0.02, 0.0, 0.035),
                  ('FruitRed', 0.05, -0.03, 0.02), ('FruitGold', 0.11, 0.05, -0.005), ('FruitRed', 0.18, 0.0, 0.03), ('FruitGold', 0.24, 0.04, -0.01),
                  ('FruitGold', -0.15, 0.115, -0.1), ('FruitRed', 0.17, 0.115, -0.11))   # blossoms: material, z, x and y off the top of the leaves


def build_window(scene):
    rng = random.Random(406)
    middle = (WINDOW_LOW + WINDOW_HIGH) / 2
    # Dark opening on the -X face (upright beam: width along Z, height along X), a cross of bars in it
    beam(scene, 'Opening', G(-0.225, WINDOW_LOW - 0.01, 0), G(-0.225, WINDOW_HIGH + 0.01, 0), WINDOW_HALF * 2.0, 0.05, 'Door')
    beam(scene, 'BarUp', G(-0.197, WINDOW_LOW, 0), G(-0.197, WINDOW_HIGH, 0), 0.03, 0.03, 'PaintDark')
    beam(scene, 'BarAcross', G(-0.197, middle + 0.03, -WINDOW_HALF), G(-0.197, middle + 0.03, WINDOW_HALF), 0.03, 0.03, 'PaintDark')
    # Frame: two jambs, a long head carrying the weather board
    for side in (-1, 1):
        z = side * (WINDOW_HALF + 0.02)
        beam(scene, f'Jamb{side}', G(-0.2, WINDOW_LOW - 0.03, z), G(-0.2, WINDOW_HIGH + 0.01, z), 0.06, 0.09, 'PaintDark', rng, 0.004)
    beam(scene, 'Head', G(-0.19, WINDOW_HIGH + 0.04, -0.44), G(-0.19, WINDOW_HIGH + 0.04, 0.44), 0.11, 0.07, 'PaintDark', rng, 0.005)
    # Open shutters: boards swung out to both sides, two battens on each
    for side in (-1, 1):
        hinge, edge = (-0.178, side * (WINDOW_HALF + 0.06)), (-0.158, side * 0.485)
        beam(scene, f'Shutter{side}', G(hinge[0], middle, hinge[1]), G(edge[0], middle, edge[1]), 0.03, WINDOW_HIGH - WINDOW_LOW, 'Paint', rng, 0.004)
        for row, y in enumerate((WINDOW_LOW + 0.09, WINDOW_HIGH - 0.09)):
            beam(scene, f'Batten{side}{row}', G(hinge[0] + 0.024, y, hinge[1] + side * 0.012), G(edge[0] + 0.024, y, edge[1] - side * 0.012),
                 0.026, 0.055, 'PaintDark')
    # Sill under the opening
    beam(scene, 'Sill', G(-0.165, WINDOW_LOW - 0.045, -0.3), G(-0.165, WINDOW_LOW - 0.045, 0.3), 0.17, 0.045, 'PaintDark', rng, 0.005)
    # Flower box below the sill: a trough of boards full of earth, on two brackets
    box_low, box_high, box_middle, box_half = 0.2, 0.33, -0.1, 0.29
    beam(scene, 'Earth', G(box_middle, box_low + 0.02, 0), G(box_middle, box_high - 0.012, 0), box_half * 2.0 - 0.03, 0.27, 'Door')
    for name, x in (('BoxFront', 0.04), ('BoxBack', -0.235)):
        beam(scene, name, G(x, (box_low + box_high) / 2, -box_half), G(x, (box_low + box_high) / 2, box_half), 0.03, box_high - box_low, 'Wood', rng, 0.005)
    for side in (-1, 1):
        beam(scene, f'BoxEnd{side}', G(-0.245, (box_low + box_high) / 2, side * (box_half - 0.012)), G(0.05, (box_low + box_high) / 2, side * (box_half - 0.012)),
             0.03, box_high - box_low, 'Wood', rng, 0.005)
        pole(scene, f'Bracket{side}', [G(-0.24, 0.04, side * 0.19), G(0.02, box_low + 0.01, side * 0.19)], [0.024, 0.02], 'PaintDark', sides=5)
    beam(scene, 'BoxFloor', G(-0.245, box_low + 0.012, 0), G(0.05, box_low + 0.012, 0), box_half * 2.0 - 0.02, 0.025, 'Wood')
    # Leaves: a row of clumps in the box, two hanging over its front
    for i in range(5):
        z = -0.23 + 0.115 * i
        x = -0.04 + rng.uniform(-0.02, 0.02)
        ellipsoid(scene, f'Leaves{i}', G(x, box_high + 0.02, z), (0.09, 0.075, rng.uniform(0.045, 0.065)), 'Leaf', subdivisions=2)
    for i, z in enumerate((-0.15, 0.17)):
        ellipsoid(scene, f'Trail{i}', G(0.05, box_high - 0.05, z), (0.035, 0.07, 0.085), 'Leaf', subdivisions=2)
    # Blossoms on the leaves, two on the trailing ones
    for i, (material_name, z, off, lift) in enumerate(WINDOW_FLOWERS):
        fruit(scene, f'Blossom{i}', (-0.04 + off, box_high + 0.075 + lift, z), 0.03, material_name)
    # Weather board: four short boards slanting down to the front
    for i in range(4):
        z = -0.33 + 0.22 * i
        lift = 0.006 * (i % 2)
        beam(scene, f'Board{i}', G(-0.245, 0.975 + lift, z), G(0.07 - rng.uniform(0.0, 0.025), 0.915 + lift, z), 0.21, 0.03, 'PaintDark')
    trim_height(scene, 1.0)


WELL_RADIUS = 0.35                                    # outside of the plastered core of the well wall
WELL_SHAFT = 0.24                                     # radius of the shaft
WELL_COURSES = (0.095, 0.265)                         # heights of the middle of the two stone courses
WELL_STONES = 7                                       # stones per course
WELL_POST = 0.39                                      # where the two posts stand on the Z axis


def build_well(scene):
    rng = random.Random(407)
    # Wall and shaft in one connected shell (its faces turn the right way only as one piece):
    # up the outside, over the top, down to the floor of the shaft
    lathe(scene, 'Wall', [(WELL_RADIUS, 0.0), (WELL_RADIUS, 0.36), (WELL_SHAFT, 0.36), (WELL_SHAFT, 0.1), (0.0, 0.1)], 'Paint', segments=14, is_smooth=False)
    # Water down in the shaft, dark because the shaft is dark below it
    lathe(scene, 'Deep', [(WELL_SHAFT, 0.1), (WELL_SHAFT, 0.15)], 'Door', segments=14, has_top_cap=True)
    lathe(scene, 'Water', [(WELL_SHAFT - 0.002, 0.15), (WELL_SHAFT - 0.002, 0.165)], 'Water', segments=14, has_top_cap=True)
    # Two courses of field stones around the wall, the upper one turned against the lower
    for course, y in enumerate(WELL_COURSES):
        for i in range(WELL_STONES):
            angle = (i + 0.5 * course) / WELL_STONES * math.tau + rng.uniform(-0.06, 0.06)
            out = (math.cos(angle), math.sin(angle))
            fieldstone(scene, f'Stone{course}{i}', (out[0] * WELL_RADIUS, y, out[1] * WELL_RADIUS), out,
                       rng.uniform(0.125, 0.145), rng.uniform(0.07, 0.08), rng.uniform(0.05, 0.075), 'PaintDark', rng)
    # Cap stones: a flat faceted ring lying on the wall
    lathe(scene, 'Cap', [(WELL_SHAFT - 0.015, 0.345), (0.43, 0.345), (0.445, 0.385), (0.42, 0.42), (WELL_SHAFT - 0.015, 0.42),
                         (WELL_SHAFT - 0.015, 0.345)], 'Stone', segments=9, is_smooth=False,
          bulge=lambda k, j: rng.uniform(-0.012, 0.012) if 1 <= j <= 3 else 0.0)
    # Posts in the wall carrying a small gable roof, its ridge along Z
    for side in (-1, 1):
        z = side * WELL_POST
        pole(scene, f'Post{side}', [G(0, 0.0, z), G(rng.uniform(-0.01, 0.01), 0.45, z), G(0, 0.93, z)], [0.05, 0.043, 0.04], 'Wood', sides=6, is_level=True)
        for slope in (-1, 1):
            beam(scene, f'Rafter{side}{slope}', G(slope * 0.3, 0.765, z), G(0.0, 0.925, z), 0.05, 0.045, 'PaintDark')
    for slope in (-1, 1):
        for i in range(4):
            z = -0.36 + 0.24 * i
            lift = 0.006 * (i % 2)
            beam(scene, f'Board{slope}{i}', G(slope * (0.36 - rng.uniform(0.0, 0.025)), 0.775 + lift, z), G(slope * 0.012, 0.955 + lift, z),
                 0.225, 0.035, 'Paint')
    pole(scene, 'Ridge', [G(0, 0.972, -0.5), G(0, 0.972, 0.5)], [0.034, 0.03], 'PaintDark', sides=6)
    # Crank roller between the posts: axle bent to a handle outside the right post, rope wound around
    roller = 0.69
    pole(scene, 'Roller', [G(0, roller, -WELL_POST + 0.04), G(0, roller, WELL_POST - 0.04)], [0.05, 0.05], 'Wood', sides=8)
    pole(scene, 'Crank', [G(0, roller, -WELL_POST - 0.06), G(0, roller, WELL_POST + 0.075), G(0.1, roller - 0.12, WELL_POST + 0.075),
                          G(0.1, roller - 0.12, WELL_POST + 0.155)], [0.022] * 4, 'PaintDark', sides=5)
    pole(scene, 'Coil', [G(0, roller, -0.13), G(0, roller, 0.1)], [0.064, 0.064], 'Rope', sides=8)
    # Rope down to the bucket hanging over the shaft
    bucket_x, bucket_low, bucket_high = 0.06, 0.44, 0.58
    pole(scene, 'Rope', [G(bucket_x, roller, 0.0), G(bucket_x, bucket_high + 0.055, 0.0)], [0.013, 0.013], 'Rope', sides=4)
    pole(scene, 'Bail', [G(bucket_x, bucket_high - 0.01, -0.09), G(bucket_x, bucket_high + 0.06, 0.0), G(bucket_x, bucket_high - 0.01, 0.09)],
         [0.011] * 3, 'Rope', sides=4)
    lathe(scene, 'Bucket', [(0.075, bucket_low), (0.1, bucket_high)], 'Wood', segments=8, center=(bucket_x, 0.0), has_top_cap=True, has_bottom_cap=True)
    lathe(scene, 'Hoop', [(0.084, bucket_low + 0.04), (0.096, bucket_low + 0.058), (0.09, bucket_low + 0.076)], 'PaintDark', segments=8, center=(bucket_x, 0.0))
    trim_height(scene, 1.0)
