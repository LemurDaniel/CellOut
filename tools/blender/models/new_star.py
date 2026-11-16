# NOTE:
#   New parts of the star family of templates: the arm of a starfish and the needles
#   of a sea urchin. Registered in build.models.py.
#   Requires: Blender 5.2 (bpy, bmesh, mathutils).
#
#   ./tools/blender/Show-Model.ps1 -Module new_star -Call 'build_stararm(scene)' -Name stararm
#   ./tools/blender/Show-Model.ps1 -Module new_star -Call 'build_urchinspines(scene)' -Name urchinspines

#
#   Chapters:
#     STARFISH PARTS   The arm of a starfish.
#     URCHIN PARTS     The needles of a sea urchin.
#

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

from models.helpers import (
    AXIS_FORWARD,
    FPS,
    G,
    animate,
    bind,
    ellipsoid,
    join_by_material,
    link_mesh,
    rig,
    wave,
)


# ===========================================================================
# ===========================   STARFISH PARTS   ============================
# ===========================================================================
#
#   arm_width       Half width of the arm at a share of its length.
#   arm_height      Height of the back over the edge at a share of its length.
#   arm_level       Height of the edge at a share of its length (the droop).
#   arm_shell       One half (back or underside) of the arm as a mesh.
#   build_stararm   Arm of a starfish (model space FlankPair): flat, pointed, knobbly.
#
#   The arm lies like a flipper: it reaches out of the right flank (+Z), is wide along
#   the body (x) and flat (y), the knobbly back is up (+Y). For a star seen from above
#   a template stands the body upright ("pitch": 90), turns the arm a quarter around
#   its length ("rotation": [0, 0, -1.5708], the back looks along the spine then) and
#   repeats it with "radial": 5.
#

ARM_LENGTH = 1.75                    # from the skin of the body to the tip, in body heights
ARM_ROOT = 0.75                      # the arm starts this deep inside the body, neighbours merge there
ARM_HALF_WIDTH = 0.74                # at the skin: tan(36 degrees) of the body radius, so five arms close the ring
ARM_TIP_WIDTH = 0.2                  # half width just before the round tip
ARM_BACK = 0.4                       # height of the back at the skin
ARM_BELLY = 0.1                      # depth of the underside
ARM_DROOP = 0.3                      # the tip lies this far below the root
ARM_ROWS = 28
ARM_ACROSS = 12                      # columns from edge to edge of one half
KNOB_COUNT = 6                       # knobs of the row down the middle
EDGE_KNOB_COUNT = 8                  # small knobs along each edge


def arm_width(share):
    """Half width at a share of the whole length (0 root inside the body, 1 tip): a long taper that ends in a round tip."""
    taper = ARM_HALF_WIDTH * 1.25 + (ARM_TIP_WIDTH - ARM_HALF_WIDTH * 1.25) * share ** 0.8
    tip_start = 0.93
    if share <= tip_start:
        return taper
    return taper * math.sqrt(max(0.0, 1.0 - ((share - tip_start) / (1.0 - tip_start)) ** 2))


def arm_height(share):
    """Height of the back over the edge: a plump ridge that flattens towards the tip."""
    return ARM_BACK * (1.15 - 0.6 * share) * (arm_width(share) / max(arm_width(min(share, 0.93)), 1e-6)) ** 0.5


def arm_level(share):
    """Height of the edge: the arm slopes down from the disc to the ground."""
    return -ARM_DROOP * max(0.0, share - 0.2) ** 1.4


def arm_z(share):
    return -ARM_ROOT + (ARM_LENGTH + ARM_ROOT) * share


def arm_shell(scene, name, material_name, is_back):
    """
    One half of the arm: rows from the root to the tip, every row an arc from the rear
    edge over the back (or below the underside) to the front edge. The root stays open,
    it hides in the body; both halves share the points of the edge.
    """
    bm = bmesh.new()
    rows = []
    for row in range(ARM_ROWS + 1):
        share = row / ARM_ROWS
        width = arm_width(share)
        bulge = arm_height(share) if is_back else -ARM_BELLY * (1.0 - 0.5 * share)
        line = []
        for column in range(ARM_ACROSS + 1):
            angle = math.pi * column / ARM_ACROSS
            # The back is a round ridge, the underside a flat sole (a higher power flattens the arc)
            arc = math.sin(angle) ** (0.8 if is_back else 0.4)
            line.append(bm.verts.new(G(-math.cos(angle) * width, arm_level(share) + bulge * arc, arm_z(share))))
        rows.append(line)
    for row in range(ARM_ROWS):
        for column in range(ARM_ACROSS):
            corners = [rows[row][column], rows[row][column + 1], rows[row + 1][column + 1], rows[row + 1][column]]
            bm.faces.new(corners if is_back else corners[::-1])
    # The last row is one point: the tip
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, name, mesh, material_name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def build_stararm(scene):
    """
    One arm of a knobbed starfish: origin on the skin of the right flank, reaching out
    (+Z) about two body heights. As wide as the body at its root, so five of them close
    to a star without a waist, tapering to a blunt tip that slopes down. The back
    carries a row of pale round knobs down the middle and small ones along both edges,
    the pale underside a groove of tube feet. The tip curls slowly ("Curl", 1 s).
    """
    pieces = [
        arm_shell(scene, 'Back', 'Skin', True),
        arm_shell(scene, 'Sole', 'Pad', False),
    ]

    # Row down the middle: big knobs at the disc, small ones at the tip
    for i in range(KNOB_COUNT):
        share = 0.3 + 0.6 * i / (KNOB_COUNT - 1)
        radius = 0.125 - 0.07 * i / (KNOB_COUNT - 1)
        y = arm_level(share) + arm_height(share) - radius * 0.2
        pieces.append(ellipsoid(scene, f'Knob{i}', G(0, y, arm_z(share)), (radius, radius, radius * 1.05), 'Starch', subdivisions=3))

    # Both edges: a string of small beads
    for i in range(EDGE_KNOB_COUNT):
        share = 0.36 + 0.55 * i / (EDGE_KNOB_COUNT - 1)
        radius = 0.065 - 0.025 * i / (EDGE_KNOB_COUNT - 1)
        for side in (-1.0, 1.0):
            x = side * (arm_width(share) - radius * 0.6)
            y = arm_level(share) + arm_height(share) * 0.22 + radius * 0.4
            pieces.append(ellipsoid(scene, f'Bead{i}{"a" if side < 0 else "b"}', G(x, y, arm_z(share)), (radius,) * 3, 'Starch', subdivisions=2))

    # Underside: the tube feet in two rows along the groove
    for i in range(10):
        share = 0.3 + 0.62 * i / 9
        radius = 0.05 - 0.02 * i / 9
        for side in (-1.0, 1.0):
            y = arm_level(share) - ARM_BELLY * (1.0 - 0.5 * share)
            pieces.append(ellipsoid(scene, f'Foot{i}{"a" if side < 0 else "b"}', G(side * radius * 1.1, y, arm_z(share)), (radius,) * 3, 'Starch', subdivisions=2))

    for piece in pieces:
        for polygon in piece.data.polygons:
            polygon.use_smooth = True
    join_by_material(pieces)
    meshes = [obj for obj in scene.objects if 'MESH' == obj.type]

    # Curl: the outer half lifts and sinks slowly, the tip a little behind
    joints = [G(0, arm_level(share), arm_z(share)) for share in (0.0, 0.5, 0.75, 1.0)]
    armature = rig(scene, 'Rig', [
        ('Base', joints[0], joints[1], None),
        ('Mid', joints[1], joints[2], 'Base'),
        ('Tip', joints[2], joints[3], 'Mid'),
    ])
    bind(armature, meshes)
    animate(armature, 'Curl', FPS, {
        'Mid': (AXIS_FORWARD, lambda share: 3.0 * wave(share)),
        'Tip': (AXIS_FORWARD, lambda share: 6.0 * wave(share, -0.15)),
    })


# ===========================================================================
# ============================   URCHIN PARTS   =============================
# ===========================================================================
#
#   build_urchinspines   Needles of a sea urchin (model space Hug).
#

NEEDLE_COUNT = 240                   # spots of the spiral, the ones in the face are left out
NEEDLE_LENGTH = 1.05                 # in body radii, every needle varies around it
NEEDLE_RADIUS = 0.042                # at the foot
NEEDLE_ROOT = 0.25                   # the foot starts this deep inside the ball
NEEDLE_FACE = 0.62                   # no needles further forward than this share: eyes and mouth stay free
NEEDLE_CHEEK = 0.05                  # the cheeks are free from this share forward
NEEDLE_SEGMENTS = 6
MOUND_RADIUS = 0.07


def build_urchinspines(scene):
    """
    The needles of a sea urchin (model space Hug): long thin cones all around a unit
    ball, straight away from its middle, of mixed length and shorter below, every one
    on a small mound of skin. The face (+X) stays free. Laid out round in the unit
    space: the game stretches x by the reach of the part, a template evens that out with
    "stretch" (urchin.json) so the needles stay a ball.
    """
    golden = math.pi * (3.0 - math.sqrt(5.0))
    needles = bmesh.new()
    mounds = []
    for i in range(NEEDLE_COUNT):
        # Evenly spread directions: a golden angle spiral around the x axis, so both flanks match
        forward = 1.0 - 2.0 * (i + 0.5) / NEEDLE_COUNT
        ring = math.sqrt(1.0 - forward * forward)
        up, out = math.cos(i * golden) * ring, math.sin(i * golden) * ring
        # The cheeks stay free too: no needle in front of an eye
        is_cheek = forward > NEEDLE_CHEEK and abs(out) > 0.45 and -0.35 < up < 0.75
        if forward > NEEDLE_FACE or is_cheek:
            continue
        normal = G(forward, up, out)

        # Mixed lengths, the ones it sits on are short
        length = NEEDLE_LENGTH * (0.75 + 0.5 * ((i * 7) % 5) / 4.0) * (0.55 if up < -0.6 else 1.0)
        base, tip = normal * (1.0 - NEEDLE_ROOT), normal * (1.0 + length)
        turn = Vector((0.0, 0.0, 1.0)).rotation_difference(normal).to_matrix().to_4x4()
        reach = (tip - base).length
        made = bmesh.ops.create_cone(needles, cap_ends=False, segments=NEEDLE_SEGMENTS, radius1=NEEDLE_RADIUS, radius2=0.004, depth=reach)
        bmesh.ops.transform(needles, matrix=Matrix.Translation(base) @ turn @ Matrix.Translation((0.0, 0.0, reach / 2)), verts=made['verts'])

        mounds.append(ellipsoid(scene, f'Mound{i}', normal * 0.97, (MOUND_RADIUS,) * 3, 'Skin', subdivisions=2))

    mesh = bpy.data.meshes.new('Needles')
    needles.to_mesh(mesh)
    needles.free()
    link_mesh(scene, 'Needles', mesh, 'BackSkin')
    join_by_material(mounds)

    for obj in scene.objects:
        if 'MESH' == obj.type:
            for polygon in obj.data.polygons:
                polygon.use_smooth = True
