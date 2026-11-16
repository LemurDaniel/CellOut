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

from models.helpers import AXIS_OUT, AXIS_UP, FPS, G, animate, bind, digit, ellipsoid, rig, tube, wave
from models.new_shark import blade, round_tip

FEATHER_STEPS = 10                   # rows of a feather from the quill to the tip
FEATHER_QUILL = 0.3                  # width at the root as share of the full width
FEATHER_FULL_AT = 0.3                # the vane is full width from this share of the length ...
FEATHER_TAPER_FROM = 0.55            # ... and runs into the point from here

BEAK_SQUEEZE = 0.7                   # the upper beak is this much narrower than tall
# The ridge of the upper beak in snout space: forward, up, radius. Out of the forehead, over the nose, down into the hook
BEAK_RIDGE = ((-0.3, 0.45, 0.9), (0.38, 0.6, 0.96), (0.93, 0.63, 0.84), (1.42, 0.4, 0.68), (1.72, -0.08, 0.5),
              (1.8, -0.63, 0.32), (1.68, -1.17, 0.17))
BEAK_HOOK = ((1.68, -1.17, 0.17), (1.55, -1.5, 0.09), (1.43, -1.71, 0.03))            # the dark tip of the hook
BEAK_LOWER = ((-0.15, -0.63, 0.66), (0.45, -0.93, 0.6), (0.93, -0.93, 0.45), (1.26, -0.63, 0.21))

# Tail feathers: angle out of the middle in degrees, length, width, material. The long middle ones lie on top
TAIL_FEATHERS = ((0.0, 2.35, 0.2, 'Skin'), (4.5, 2.05, 0.2, 'Skin'), (9.0, 1.6, 0.21, 'Skin'), (14.0, 1.2, 0.22, 'BackSkin'), (20.0, 0.85, 0.22, 'BackSkin'))
TAIL_ROOF = 13.0                     # degrees every feather tilts more than its inner neighbor
TAIL_SWAY = (0.0, 3.0, 5.0)          # degrees per bone of the Loop

CREST_FEATHERS = ((62.0, 0.5), (80.0, 0.72), (98.0, 0.9), (116.0, 0.95), (134.0, 0.8), (152.0, 0.55))     # angle from forward in degrees, length

FOOT_TOES = ((-20.0, 0.25), (17.0, 0.28), (158.0, 0.21), (-163.0, 0.19))              # angle in degrees (0 = forward), length: two forward, two back

FLIPPER_SPAN = 1.45                  # body heights from the shoulder to the tip


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
#   squeeze              Narrows a mesh across the head.
#   build_parrotbeak     The hooked beak of a macaw with the bare pale face at its root.
#   build_tailfeathers   Long stiff tail feathers, the middle ones the longest.
#   build_crestfeathers  A fan of crest feathers on the head.
#   build_birdfoot       A gripping foot: two toes forward, two back.
#

def squeeze(mesh, factor):
    """Narrows a mesh across the head (Godot z) by factor."""
    for vertex in mesh.data.vertices:
        vertex.co.y *= factor


def build_parrotbeak(scene):
    """
    The beak of a macaw (Snout: x 0 at the mouth corners .. 1 at the nose tip, z the
    width of the head). A deep pale upper beak grows out of the forehead, bulges over
    the nose and curves down into a dark hook that hangs far below the small black
    lower beak. At its root lies the bare pale skin of the face, one patch on each
    cheek: the eye of the animal sits on it.
    """
    def line(stations):
        return [G(x, y, 0) for x, y, _ in stations], [radius for _, _, radius in stations], [(i, i + 1) for i in range(len(stations) - 1)]

    squeeze(tube(scene, 'Upper', *line(BEAK_RIDGE), 'Claw'), BEAK_SQUEEZE)
    squeeze(tube(scene, 'Hook', *line(BEAK_HOOK), 'Dark'), BEAK_SQUEEZE)
    squeeze(tube(scene, 'Lower', *line(BEAK_LOWER), 'Dark'), 0.8)

    # Face: a soft dome of bare skin on each cheek, from the beak root back past the eye
    for side in (-1, 1):
        ellipsoid(scene, f'Cheek{side}', G(-0.45, 0.5, side * 0.5), (0.98, 0.8, 0.8), 'Down', subdivisions=3)


def build_tailfeathers(scene):
    """
    The tail of a macaw (Tail): root at the origin on the tail tip of the body,
    reaching back (-X), flat. Long narrow pointed feathers, the middle ones the
    longest and on top, shorter ones fanned out below them in the pattern color.
    It sways a little from side to side ("Loop", 3 s).
    """
    meshes = [ellipsoid(scene, 'Rump', G(0.05, 0, 0), (0.3, 0.2, 0.16), 'Skin', subdivisions=3)]
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
    narrow blade without feathers to see: a strong shoulder, the leading edge bows
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

    armature = rig(scene, 'Rig', [
        ('Base', out * -0.1, out * 0.5, None),
        ('Tip', out * 0.5, out * FLIPPER_SPAN, 'Base'),
    ])
    bind(armature, [fin])
    animate(armature, 'Loop', FPS * 2, {
        'Base': (AXIS_UP, lambda share: 4.0 + 4.0 * wave(share)),
        'Tip': (AXIS_UP, lambda share: 3.0 * wave(share, -0.15)),
    })
