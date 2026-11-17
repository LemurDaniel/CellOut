# NOTE:
#   Building blocks that guard and adorn a village (assets/models/building/): a gateway and
#   a dry-stone wall in line with the palisade and a spiked barrier.
#   Same frame as models/buildings.py: +Y up, +X is the front of a block, the origin is the
#   center of its footprint on the ground, one grid cell is 0.5 units, a block stays inside
#   its box and ends flat where the next one stacks. Materials Paint (the surface the player
#   repaints) and PaintDark (its trim), fixed colors Wood, Stone, Rope, Bone, Door, Hide,
#   and Bronze. Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only building/
#   ./tools/blender/Show-Model.ps1 -Module new_blocks_guard -Call 'build_gate(scene)' -Name gate

#
#   Chapters:
#     HELPERS     Hewn stone blocks and rope bands.
#     DEFENSE     Gate (4 x 3 x 1 cells), stone wall (4 x 2 x 1) and spikes (2 x 1 x 1).
#

import math
import random

import bmesh
import bpy
from mathutils import Vector

from models.buildings import beam, curve, lashing, lathe, plate, pole, rock, trim_height
from models.helpers import G, ellipsoid, link_mesh


# ===========================================================================
# ===============================   HELPERS   ===============================
# ===========================================================================
#
#   block         A hewn stone: a box with wobbling, chamfered corners, flat shaded.
#   band          A rope wound around a log that lies along X.
#   trim_length   Cuts every model off at both ends of its box along X.
#

def block(scene, name, center, size, material_name, rng, wobble=0.015, chamfer=0.022):
    """
    A hewn stone around center (Godot x, y, z). size holds its three half axes (along X,
    up, across Z). Every corner moves by up to wobble, then all edges are chamfered.
    """
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=2.0)
    for vertex in bm.verts:
        jitter = Vector([rng.uniform(-wobble, wobble) for _ in range(3)])
        # Blender axes of the cube: x along, y across, z up
        vertex.co = G(*center) + Vector((vertex.co.x * size[0], vertex.co.y * size[2], vertex.co.z * size[1])) + jitter
    bmesh.ops.bevel(bm, geom=bm.edges[:], offset=chamfer, segments=1, affect='EDGES')
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return link_mesh(scene, name, mesh, material_name)


def band(scene, name, x, y, z, radius, length=0.07):
    """A rope ring around a log of that radius lying along X, centered on (x, y, z)."""
    half = length / 2
    points = [G(x - half, y, z), G(x - half * 0.5, y, z), G(x + half * 0.5, y, z), G(x + half, y, z)]
    return pole(scene, name, points, [radius * 1.02, radius + 0.016, radius + 0.016, radius * 1.02], 'Rope', sides=7)


def trim_length(scene, half_length):
    """No vertex beyond the two ends of the box along X: a row of the same block closes without a gap."""
    for obj in scene.objects:
        if 'MESH' != obj.type:
            continue
        for vertex in obj.data.vertices:
            vertex.co.x = min(max(vertex.co.x, -half_length), half_length)


# ===========================================================================
# ===============================   DEFENSE   ===============================
# ===========================================================================
#
#   gate_leaf          One gate leaf of upright planks on two battens and a brace, swung open.
#   build_gate         Two carved posts wedged with stones, a heavy log across, two open
#                      leaves, a horned skull and two shields on the log.
#   build_stone_wall   Four staggered courses of hewn stones on both faces of a core, coping
#                      slabs on top. Tiles end to end along X.
#   build_spikes       A log off the ground, eight sharpened stakes crossing through it.
#
#   Gate and wall stand in a line with the palisade: they run along X and are looked at
#   from +Z (outside) and -Z. The wall ends flat at y = 1, the next block stands on it.
#

GATE_POST_X = 0.82                                    # the posts stand where the outer stakes of a palisade would
GATE_POST_RADIUS = 0.15
GATE_BEAM_Y = 1.3                                     # axis of the log lying across the posts
GATE_BEAM_RADIUS = 0.115
GATE_LEAF_WIDTH = 0.36
GATE_LEAF_SWING = math.radians(30)                    # wider open the leaves would leave the box (0.5 deep)
GATE_POST_PROFILE = [(0.15, 0.0), (0.158, 0.1), (0.128, 0.15), (0.128, 0.23), (0.152, 0.28), (0.146, 0.62), (0.143, 0.84),
                     (0.12, 0.89), (0.12, 0.99), (0.15, 1.04), (0.138, 1.19)]


def gate_leaf(scene, name, rng, hinge_x, side):
    """
    A leaf hinged at (hinge_x, z just behind the posts), swung open towards -Z (into the
    village). side is the sign of the post it hangs on: the leaf points away from it.
    """
    hinge = Vector((hinge_x, -0.03))                                                    # (x, z)
    along = Vector((-side * math.cos(GATE_LEAF_SWING), -math.sin(GATE_LEAF_SWING)))     # from the hinge to the free edge
    normal = Vector((-along.y, along.x)) * side                                         # out of the face that looks at the passage
    thickness = 0.04

    def at(reach, y, lift=0.0):
        point = hinge + along * reach + normal * lift
        return G(point.x, y, point.y)

    # Upright planks, uneven in width and height
    plank_count = 4
    spacing = GATE_LEAF_WIDTH / plank_count
    for i in range(plank_count):
        near, far = spacing * i + 0.006, spacing * (i + 1) - 0.006
        low, high = rng.uniform(0.03, 0.06), rng.uniform(0.92, 1.0)
        outline = [at(near, low, -thickness / 2), at(far, low, -thickness / 2), at(far, high, -thickness / 2), at(near, high, -thickness / 2)]
        plate(scene, f'{name}Plank{i}', outline, G(normal.x * thickness, 0.0, normal.y * thickness), 'Paint')
    # Two battens and a brace between them on both faces
    for face in (-1, 1):
        lift = face * (thickness / 2 + 0.012)
        for row, y in enumerate((0.24, 0.76)):
            beam(scene, f'{name}Batten{face}{row}', at(0.0, y, lift), at(GATE_LEAF_WIDTH, y, lift), 0.03, 0.075, 'PaintDark', rng, 0.004)
        beam(scene, f'{name}Brace{face}', at(0.03, 0.27, lift), at(GATE_LEAF_WIDTH - 0.03, 0.73, lift), 0.026, 0.06, 'PaintDark')
    # Rope hinges tying the leaf to its post
    for row, y in enumerate((0.24, 0.76)):
        pole(scene, f'{name}Hinge{row}', [at(-0.09, y + 0.01), at(-0.02, y), at(0.05, y + 0.01)], [0.03, 0.034, 0.03], 'Rope', sides=5)


def build_gate(scene):
    rng = random.Random(401)
    for side in (-1, 1):
        x = side * GATE_POST_X
        # Carved post: two deep grooves, a darker ring in each
        lathe(scene, f'Post{side}', GATE_POST_PROFILE, 'Paint', segments=12, center=(x, 0.0),
              bulge=lambda k, j: 0.0 if j in (0, len(GATE_POST_PROFILE) - 1) else rng.uniform(-0.005, 0.005), has_top_cap=True)
        for row, y in enumerate((0.19, 0.94)):
            lathe(scene, f'Groove{side}{row}', [(0.115, y - 0.05), (0.135, y - 0.03), (0.135, y + 0.03), (0.115, y + 0.05)], 'PaintDark',
                  segments=12, center=(x, 0.0))
        # Carved diamonds one above the other on both faces of the post: slabs from inside the
        # round post to just out of its surface, so only a low relief shows
        for face in (-1, 1):
            for row, (y, size) in enumerate(((0.58, 0.055), (0.42, 0.035), (0.74, 0.035))):
                z = face * 0.09
                outline = [G(x, y + size * 1.7, z), G(x + size, y, z), G(x, y - size * 1.7, z), G(x - size, y, z)]
                plate(scene, f'Diamond{side}{face}{row}', outline, G(0.0, 0.0, face * (GATE_POST_RADIUS - 0.09 + 0.006)), 'PaintDark')
        # Stones wedging the foot
        for face in (-1, 1):
            rock(scene, f'Wedge{side}{face}', (x - side * 0.03, 0.035, face * 0.16), (0.1, rng.uniform(0.05, 0.065), 0.07), 'Stone', rng,
                 turn=rng.uniform(-0.4, 0.4))
        # Rope: around the post below the log, around the log on both sides of the post
        lashing(scene, f'PostRope{side}', x, 1.115, 0.0, GATE_POST_RADIUS)
        for step in (-1, 1):
            band(scene, f'BeamRope{side}{step}', x + step * 0.13 - side * 0.03, GATE_BEAM_Y, 0.0, GATE_BEAM_RADIUS)
        gate_leaf(scene, f'Leaf{side}', rng, side * (GATE_POST_X - GATE_POST_RADIUS + 0.02), side)
    # Heavy log across both posts, a little bowed, its ends cut at the ends of the box
    pole(scene, 'Beam', [G(-1.0, GATE_BEAM_Y - 0.005, 0.0), G(-0.3, GATE_BEAM_Y + 0.008, 0.01), G(0.3, GATE_BEAM_Y + 0.005, -0.008), G(1.0, GATE_BEAM_Y - 0.005, 0.0)],
         [0.11, GATE_BEAM_RADIUS, GATE_BEAM_RADIUS, 0.108], 'PaintDark', sides=8)
    # Horned skull on the outer face (+Z) of the log, the snout hanging down
    ellipsoid(scene, 'Cranium', G(0.0, 1.355, 0.1), (0.105, 0.1, 0.1), 'Bone', subdivisions=2)
    pole(scene, 'Snout', [G(0.0, 1.34, 0.13), G(0.0, 1.25, 0.19), G(0.0, 1.16, 0.215), G(0.0, 1.13, 0.215)], [0.085, 0.068, 0.045, 0.025], 'Bone', sides=8)
    for side in (-1, 1):
        ellipsoid(scene, f'Socket{side}', G(side * 0.055, 1.33, 0.185), (0.032, 0.025, 0.032), 'Door', subdivisions=1)
        bends = [G(side * 0.06, 1.39, 0.09), G(side * 0.2, 1.385, 0.1), G(side * 0.33, 1.41, 0.12), G(side * 0.4, 1.46, 0.14), G(side * 0.41, 1.5, 0.15)]
        pole(scene, f'Horn{side}', curve(bends, 7), [0.05, 0.047, 0.042, 0.035, 0.027, 0.016, 0.0], 'Bone', sides=6)
    # Round hide shields hanging from the log on both faces, a bronze boss in the middle
    for side in (-1, 1):
        for face in (-1, 1):
            x, y = side * 0.47, 1.19
            pole(scene, f'Shield{side}{face}', [G(x, y, face * 0.09), G(x, y, face * 0.13), G(x, y, face * 0.15)], [0.13, 0.13, 0.095], 'Hide', sides=10)
            ellipsoid(scene, f'Boss{side}{face}', G(x, y, face * 0.15), (0.045, 0.035, 0.045), 'Bronze', subdivisions=1)
            pole(scene, f'Strap{side}{face}', [G(x, y + 0.1, face * 0.14), G(x, GATE_BEAM_Y + 0.06, face * 0.115), G(x, GATE_BEAM_Y + 0.125, 0.0)],
                 [0.016, 0.016, 0.016], 'Rope', sides=4)
    trim_height(scene, 1.5)


WALL_HALF_LENGTH = 1.0
WALL_COURSES = (0.25, 0.19, 0.25, 0.21)               # height of every course, together 0.9: the cope stones lie on top
WALL_STONE_Z = 0.2                                    # middle of the stones of one face, they reach out to 0.245
WALL_STONE_DEPTH = 0.045
WALL_JOINT = 0.014                                    # half the open joint between two stones


def build_stone_wall(scene):
    rng = random.Random(402)
    # Core between the two faces, showing in the joints
    beam(scene, 'Core', G(-WALL_HALF_LENGTH, 0.475, 0.0), G(WALL_HALF_LENGTH, 0.475, 0.0), 0.38, 0.95, 'Paint')
    # Courses: every other one is shifted by half a stone, its end stone runs over the end of the
    # block. That stone is built twice with the same seed, two units apart, and cut at the end:
    # the half at the left end of one wall is the missing half at the right end of the next
    for face in (-1, 1):
        low = 0.0
        for course, height in enumerate(WALL_COURSES):
            is_shifted = 1 == course % 2
            inner = [(-0.75 if is_shifted else -0.5) + 0.5 * i + rng.uniform(-0.11, 0.11) for i in range(4 if is_shifted else 3)]
            cuts = inner if is_shifted else [-WALL_HALF_LENGTH - WALL_JOINT] + inner + [WALL_HALF_LENGTH + WALL_JOINT]
            spans = [(near, far, None) for near, far in zip(cuts, cuts[1:])]
            if is_shifted:
                seed = rng.randrange(1 << 30)
                spans += [(inner[-1], inner[0] + 2.0, seed), (inner[-1] - 2.0, inner[0], seed)]
            for i, (near, far, seed) in enumerate(spans):
                stone_rng = rng if seed is None else random.Random(seed)
                # A tall course now and then holds two thin stones instead of one
                layers = 2 if height > 0.22 and 0 == stone_rng.randrange(3) else 1
                for layer in range(layers):
                    size = ((far - near) / 2 - WALL_JOINT, height / layers / 2 - WALL_JOINT * 0.8, WALL_STONE_DEPTH)
                    center = ((near + far) / 2, low + height / layers * (layer + 0.5), face * (WALL_STONE_Z + stone_rng.uniform(-0.012, 0.0)))
                    material_name = 'Stone' if 0 == stone_rng.randrange(6) else 'PaintDark'
                    block(scene, f'Stone{face}{course}{i}{layer}', center, size, material_name, stone_rng, wobble=0.02)
            low += height
    # Cope stones on edge across the whole wall: built a little too high, trim_height cuts them
    # to one flat top at y = 1
    near, i = -WALL_HALF_LENGTH, 0
    while near < WALL_HALF_LENGTH:
        far = near + rng.uniform(0.15, 0.26)
        if far > WALL_HALF_LENGTH - 0.12:
            far = WALL_HALF_LENGTH
        block(scene, f'Cope{i}', ((near + far) / 2, 0.955, 0.0), ((far - near) / 2 - 0.005, rng.uniform(0.065, 0.075), 0.232), 'Paint', rng, wobble=0.01)
        near, i = far, i + 1
    trim_length(scene, WALL_HALF_LENGTH)
    trim_height(scene, 1.0)

SPIKES_LOG_Y = 0.2                                    # axis of the log, the stakes carry it
SPIKES_LOG_RADIUS = 0.06


def build_spikes(scene):
    rng = random.Random(403)
    pole(scene, 'Log', [G(-0.47, SPIKES_LOG_Y, 0.0), G(0.0, SPIKES_LOG_Y + 0.008, 0.005), G(0.47, SPIKES_LOG_Y, 0.0)],
         [SPIKES_LOG_RADIUS, SPIKES_LOG_RADIUS * 1.05, SPIKES_LOG_RADIUS * 0.95], 'PaintDark', sides=7)
    # Stakes through the log, leaning to one side and the other in turn: an X seen from the end.
    # The foot starts below the ground and is cut flat there, the upper end is hewn to a point
    stake_count = 8
    for i in range(stake_count):
        x = -0.41 + 0.82 * i / (stake_count - 1)
        lean = 1 if 0 == i % 2 else -1
        foot = G(x + rng.uniform(-0.015, 0.015), -0.03, -lean * 0.205)
        tip = G(x + rng.uniform(-0.02, 0.02), 0.49, lean * 0.235)
        shoulder = foot.lerp(tip, 0.78)
        radius = rng.uniform(0.024, 0.029)
        pole(scene, f'Stake{i}', [foot, foot.lerp(tip, 0.4), shoulder], [radius, radius * 0.97, radius * 0.92], 'Paint', sides=6)
        pole(scene, f'Point{i}', [shoulder, tip], [radius * 0.92, 0.0], 'Paint', sides=6, is_smooth=False)
        # A turn of rope beside the stake, where it goes through the log
        band(scene, f'Rope{i}', x - lean * 0.038, SPIKES_LOG_Y, 0.0, SPIKES_LOG_RADIUS - 0.004, length=0.035)
    trim_height(scene, 0.5)
