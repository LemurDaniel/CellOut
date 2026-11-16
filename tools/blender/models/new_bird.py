# NOTE:
#   New parts of the bird family of templates (parrot, penguin): the hooked beak of a
#   macaw with its bare face, long tail feathers, a crest, the gripping foot of a parrot
#   and the stiff flipper of a penguin. Registered in build.models.py.
#   Requires: Blender 5.2 (bpy, bmesh, mathutils).
#
#   ./tools/blender/Show-Model.ps1 -Module new_bird -Call 'build_parrotbeak(scene)' -Name parrotbeak
#   ./tools/blender/Show-Model.ps1 -Module new_bird -Call 'build_tailfeathers(scene)' -Name tailfeathers

#
#   Chapters:
#     FEATHERS         The blade every feather is cut from.
#     PARROT PARTS     Beak with the bare face, tail feathers, crest and foot.
#     PENGUIN PARTS    The flipper.
#

import math

import bmesh
import bpy

from models.helpers import AXIS_OUT, AXIS_UP, FPS, G, animate, bind, digit, ellipsoid, link_mesh, material, rig, tube, wave
from models.new_shark import blade, round_tip

FEATHER_STEPS = 10                   # rows of a feather from the quill to the tip
FEATHER_QUILL = 0.3                  # width at the root as share of the full width
FEATHER_FULL_AT = 0.3                # the vane is full width from this share of the length ...
FEATHER_TAPER_FROM = 0.55            # ... and runs into the point from here

# The beak is drawn in head radii around the center of the head (the nose vertebra): x forward, y up, z out.
# The snout space of the game is stretched differently per axis, HEAD_TO_SNOUT turns one into the other
# (default mouth of MouthShapes.cs on a marine mammal head: corners 0.12 radii in front of the center, 0.158 below it)
HEAD_TO_SNOUT = ((0.12, 0.906), (-0.158, 0.655), (0.0, 0.97))        # per axis: origin of the snout space, length of its unit
HEAD_HEIGHT = 0.88                   # half height and half width of the head in radii
HEAD_WIDTH = 0.95

BEAK_CENTER = (0.5, -0.2)            # the upper beak is an arc around this point of the face
BEAK_ARC = 0.56                      # radius of its middle line
BEAK_FROM = 118.0                    # degrees: the root at the forehead ...
BEAK_DARK_FROM = -28.0               # ... the dark tip of the hook starts here ...
BEAK_TO = -62.0                      # ... and ends here, pointing down and a little back
BEAK_STEPS = 18
BEAK_AROUND = 14                     # points of a cross-section
BEAK_THICK = 0.36                    # half thickness of the arc at the root (towards and away from its center)
BEAK_WIDE = 0.47                     # half width at the root

FACE_CENTER = (0.2, 0.08)            # the bare face patch on the cheek: forward, up
FACE_SIZE = (0.44, 0.43)              # half length, half height
FACE_RINGS = 6
FACE_SPOKES = 20
FACE_PUFF = 0.035                    # the patch lies this far above the skin in its middle, its rim sinks in

# Tail feathers: angle out of the middle in degrees, length, width, material. The long middle ones lie on top
TAIL_FEATHERS = ((0.0, 2.35, 0.2, 'Skin'), (4.5, 2.05, 0.2, 'Skin'), (9.0, 1.6, 0.21, 'Skin'), (14.0, 1.2, 0.22, 'BackSkin'), (20.0, 0.85, 0.22, 'BackSkin'))
TAIL_ROOF = 13.0                     # degrees every feather tilts more than its inner neighbor
TAIL_SWAY = (0.0, 3.0, 5.0)          # degrees per bone of the Loop

CREST_FEATHERS = ((62.0, 0.5), (80.0, 0.72), (98.0, 0.9), (116.0, 0.95), (134.0, 0.8), (152.0, 0.55))     # angle from forward in degrees, length

FOOT_TOES = ((-20.0, 0.25), (17.0, 0.28), (158.0, 0.21), (-163.0, 0.19))              # angle in degrees (0 = forward), length: two forward, two back

FLIPPER_SPAN = 1.45                  # body heights from the shoulder to the tip
FLIPPER_LINING_DEPTH = 0.012         # faces this far on the body side of the middle of the blade are the pale underside


# ===========================================================================
# ==============================   FEATHERS   ===============================
# ===========================================================================
#
#   feather   One stiff feather: a narrow quill, a full vane, a pointed tip.
#

def feather(scene, name, base, direction, length, width, normal, material_name, thickness=0.03):
    """
    One stiff feather as a solid blade (new_shark.blade) from base along direction, its
    flat sides facing normal: narrow at the quill, full width after a third, running
    into a point. Points and vectors in Blender axes (use G).
    """
    direction = direction.normalized()
    side = direction.cross(normal).normalized()
    rows = []
    for step in range(FEATHER_STEPS + 1):
        share = step / FEATHER_STEPS
        grown = FEATHER_QUILL + (1.0 - FEATHER_QUILL) * min(1.0, share / FEATHER_FULL_AT)
        pointed = 1.0 - max(0.0, (share - FEATHER_TAPER_FROM) / (1.0 - FEATHER_TAPER_FROM)) ** 1.7
        half = side * (width * 0.5 * grown * pointed)
        center = base + direction * (length * share)
        rows.append((center + half, center - half, thickness * (1.0 - 0.6 * share)))
    return blade(scene, name, rows, material_name, normal=normal)


# ===========================================================================
# ============================   PARROT PARTS   =============================
# ===========================================================================
#
#   to_snout             Turns a mesh drawn around the head into the snout space.
#   sheet                A smooth skin through rows of points.
#   beak_rows            Cross-sections of the upper beak along its arc.
#   build_parrotbeak    The hooked beak of a macaw with the bare pale face at its root.
#   build_tailfeathers   Long stiff tail feathers, the middle ones the longest.
#   build_crestfeathers  A fan of crest feathers on the head.
#   build_birdfoot       A gripping foot: two toes forward, two back.
#

def to_snout(mesh):
    """Turns a mesh drawn in head radii around the head center into the snout space of the game (HEAD_TO_SNOUT)."""
    (x0, x_unit), (y0, y_unit), (z0, z_unit) = HEAD_TO_SNOUT
    for vertex in mesh.data.vertices:
        # Blender axes: x forward, -y out of the right flank, z up
        vertex.co = ((vertex.co.x - x0) / x_unit, (vertex.co.y + z0) / z_unit, (vertex.co.z - y0) / y_unit)
    return mesh


def sheet(scene, name, rows, material_name, is_closed=True):
    """
    A smooth skin through rows of points (Blender vectors, every row the same count).
    Closed: every row is a ring, the last row may be one point given count times (a tip).
    """
    bm = bmesh.new()
    grid = [[bm.verts.new(point) for point in row] for row in rows]
    count = len(rows[0])
    for row, following in zip(grid, grid[1:]):
        for i in range(count if is_closed else count - 1):
            j = (i + 1) % count
            bm.faces.new((row[i], row[j], following[j], following[i]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, name, mesh, material_name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def beak_rows(start, end, steps):
    """
    Cross-sections of the upper beak between two angles of its arc: an ellipse across
    the arc, thick and wide at the forehead, running into the point of the hook.
    """
    rows = []
    for step in range(steps + 1):
        angle = start + (end - start) * step / steps
        share = (BEAK_FROM - angle) / (BEAK_FROM - BEAK_TO)                  # 0 at the forehead .. 1 at the tip
        taper = max(0.0, 1.0 - share ** 2.2)
        radial = G(math.cos(math.radians(angle)), math.sin(math.radians(angle)), 0)
        # The hook leaves the arc at its end: it hangs straight down instead of curling back
        center = G(*BEAK_CENTER, 0) + radial * (BEAK_ARC * (1.0 + 0.3 * share ** 3))
        thick = BEAK_THICK * taper + 0.008
        wide = BEAK_WIDE * (0.25 + 0.75 * taper) * taper ** 0.35 + 0.006
        rows.append([center + radial * (thick * math.cos(turn)) + G(0, 0, wide * math.sin(turn))
                     for turn in (math.tau * i / BEAK_AROUND for i in range(BEAK_AROUND))])
    return rows


def build_parrotbeak(scene):
    """
    The beak of a macaw (Snout: x 0 at the mouth corners .. 1 at the nose tip, z the
    width of the head), drawn around the face of the head:

            .-''''-.            pale upper beak: a deep arc out of the forehead,
          /  red    .--.        as deep as it is long, close to the face
         |  o     /     \\
         | face  |  pale |
          \\     /\\      |      dark tip of the hook, hanging over
           '---' dark\\   /      the short dark lower beak
                 '---' v

    On each cheek lies the bare pale skin of the face as a thin patch on the curve of
    the head, from the beak root back past the eye: the eye of the animal sits on it.
    """
    to_snout(sheet(scene, 'Upper', beak_rows(BEAK_FROM, BEAK_DARK_FROM, BEAK_STEPS), 'Claw'))
    to_snout(sheet(scene, 'Hook', beak_rows(BEAK_DARK_FROM, BEAK_TO, BEAK_STEPS // 2), 'Dark'))
    # Lower beak: a short dark scoop under the root of the upper one, tucked behind the hook
    to_snout(ellipsoid(scene, 'Lower', G(0.6, -0.45, 0), (0.36, 0.42, 0.34), 'Dark', subdivisions=3))

    # Face: a thin patch on the skin of each cheek, it follows the curve of the head
    for side in (-1, 1):
        rows = []
        for ring in range(FACE_RINGS + 1):
            share = ring / FACE_RINGS
            row = []
            for spoke in range(FACE_SPOKES):
                turn = math.tau * spoke / FACE_SPOKES
                x = FACE_CENTER[0] + FACE_SIZE[0] * share * math.cos(turn)
                y = FACE_CENTER[1] + FACE_SIZE[1] * share * math.sin(turn)
                # The head: a ball in front of its center, a barrel behind it
                skin = HEAD_WIDTH * math.sqrt(max(0.0, 1.0 - max(0.0, x) ** 2 - (y / HEAD_HEIGHT) ** 2))
                row.append(G(x, y, side * skin * (1.0 + FACE_PUFF * (1.0 - 1.6 * share * share))))
            rows.append(row)
        face = sheet(scene, f'Face{side}', rows, 'Down')
        # An open sheet has no inside: turn its faces out of the head
        if face.data.polygons[0].normal.dot(G(0, 0, side)) < 0.0:
            face.data.flip_normals()
        to_snout(face)


def build_tailfeathers(scene):
    """
    The tail of a macaw (Tail): root at the origin on the tail tip of the body,
    reaching back (-X), flat. Long narrow pointed feathers, the middle ones the
    longest and on top, shorter ones fanned out below them in the pattern color.
    It sways a little from side to side ("Loop", 3 s).
    """
    meshes = [ellipsoid(scene, 'Rump', G(0.05, 0, 0), (0.24, 0.16, 0.11), 'Skin', subdivisions=3)]
    for index, (angle, length, width, material_name) in enumerate(TAIL_FEATHERS):
        for side in ((0,) if 0 == index else (-1, 1)):
            turn = math.radians(angle) * side
            direction = G(-math.cos(turn), -0.03, math.sin(turn))
            base = G(0.12, 0.05 - index * 0.022, side * index * 0.012)
            # The outer feathers tilt down at their outer edge: the tail is a shallow roof, not a sheet without a side view
            roof = math.radians(TAIL_ROOF * index) * side
            normal = G(0, math.cos(roof), math.sin(roof))
            meshes.append(feather(scene, f'Feather{index}_{side}', base, direction, length, width, normal, material_name))

    count = len(TAIL_SWAY)
    ends = [0.2 - 2.4 * i / count for i in range(count + 1)]
    armature = rig(scene, 'Rig', [(f'Tail{i}', G(ends[i], 0, 0), G(ends[i + 1], 0, 0), f'Tail{i - 1}' if i > 0 else None) for i in range(count)])
    bind(armature, meshes)
    animate(armature, 'Loop', FPS * 3, {
        f'Tail{i}': (AXIS_UP, lambda share, phase=-i * 0.12, degrees=TAIL_SWAY[i]: degrees * wave(share, phase)) for i in range(count)
    })


def build_crestfeathers(scene):
    """
    The crest of a cockatoo (Top): root at the origin on the head, +X forward. A fan of
    pointed feathers standing up in the middle of the head, the front ones upright, the
    back ones swept back, every second one leaning a little to its side.
    """
    for index, (angle, length) in enumerate(CREST_FEATHERS):
        lean = (index % 2 * 2 - 1) * 0.1
        direction = G(math.cos(math.radians(angle)), math.sin(math.radians(angle)), lean)
        feather(scene, f'Crest{index}', G(0.12 - index * 0.05, -0.08, 0), direction, length, 0.2, AXIS_OUT, 'Fin', thickness=0.04)


def build_birdfoot(scene):
    """
    The foot of a parrot, the end of a sculpted limb: the ankle at the origin, the sole
    below it, forward is +X. Thin scaly toes for gripping, two forward and two back,
    every toe with a knuckle and a dark curved claw.
    """
    center = G(0.0, -0.07, 0)
    tube(scene, 'Foot', [G(0, 0, 0), center], (0.05, 0.06), [(0, 1)])
    for index, (angle, length) in enumerate(FOOT_TOES):
        direction = G(math.cos(math.radians(angle)), -0.06, math.sin(math.radians(angle)))
        _, tip, tip_direction = digit(scene, f'Toe{index}', center, direction, length, (0.045, 0.036, 0.04, 0.032), curl=0.12)
        # Claw: a dark hook curling down from the toe tip
        points = [tip + tip_direction * (0.09 * share) + G(0, -0.07 * share * share, 0) for share in (0.0, 0.35, 0.7, 1.0)]
        tube(scene, f'Claw{index}', points, (0.028, 0.022, 0.013, 0.004), [(0, 1), (1, 2), (2, 3)], 'Dark', subdivisions=1)


# ===========================================================================
# ============================   PENGUIN PARTS   ============================
# ===========================================================================
#
#   build_penguinflipper   The stiff narrow wing of a penguin.
#

def build_penguinflipper(scene):
    """
    The flipper of a penguin (FlankPair: origin on the skin of the right flank, out of
    the body is +Z, the game scales it to the height of the body there). A stiff
    narrow blade without feathers to see, dark outside and pale inside: a strong shoulder, the leading edge bows
    back to a round tip, flat like a paddle. It lies back along the flank (-X) with
    its flat side out, the tip a little off the body: on an upright body it hangs
    down like the wing of a standing penguin. It lifts off the flank and back ("Loop", 2 s).
    """
    out = G(-0.93, -0.12, 0.3).normalized()                 # along the flipper, from the shoulder to the tip
    flat = G(0.3, 0.0, 0.95).normalized()                   # across its flat sides
    chord = out.cross(flat).normalized()                    # towards the leading edge
    rows = []
    steps = 14
    for step in range(steps + 1):
        share = step / steps
        lead = 0.2 + 0.06 * math.sin(share * math.pi) - 0.1 * share
        trail = -0.2 - 0.05 * math.sin(share * math.pi) + 0.02 * share
        middle = (lead + trail) / 2
        half = (lead - trail) / 2 * round_tip(share, 0.72)
        center = out * (-0.1 + (FLIPPER_SPAN + 0.1) * share)
        rows.append((center + chord * (middle + half), center + chord * (middle - half), 0.11 - 0.06 * share))
    fin = blade(scene, 'Flipper', rows, 'BackSkin', normal=flat)

    # The blade must face out of itself, else the game shows its far side through the near one
    probe = max(fin.data.polygons, key=lambda polygon: abs(polygon.center.dot(flat)))
    if probe.normal.dot(flat) * probe.center.dot(flat) < 0.0:
        fin.data.flip_normals()
    # Underside: the faces towards the body are pale, the outside and both edges stay dark
    fin.data.materials.append(material('Down'))
    for polygon in fin.data.polygons:
        if polygon.center.dot(flat) < -FLIPPER_LINING_DEPTH:
            polygon.material_index = 1

    armature = rig(scene, 'Rig', [
        ('Base', out * -0.1, out * 0.5, None),
        ('Tip', out * 0.5, out * FLIPPER_SPAN, 'Base'),
    ])
    bind(armature, [fin])
    animate(armature, 'Loop', FPS * 2, {
        'Base': (AXIS_UP, lambda share: 4.0 + 4.0 * wave(share)),
        'Tip': (AXIS_UP, lambda share: 3.0 * wave(share, -0.15)),
    })
