# NOTE:
#   New parts of the sea turtle (assets/models/creature/): the long front flipper and the
#   short hind rudder, fleshy blades instead of the ray fin of a fish, and the calm eye
#   under heavy lids. Registered in build.models.py.
#   Model space "FlankPair" (flippers): origin on the skin of the right flank, +Z out of
#   the body, +X forward, mirrored for the left flank; 1 unit = the height of the body there.
#   Model space "Eye": an eyeball of radius 1 at the origin on the right flank, +Z out of
#   the head, +X forward; the game mirrors it, scales it to the eye size and blinks it.
#
#   ./tools/blender/Show-Model.ps1 -Module new_turtle -Call 'build_flipper(scene)' -Name flipper

#
#   Chapters:
#     TURTLE PARTS   The flippers with their stroke and the lidded eye.
#

import math

from models.helpers import (
    AXIS_FORWARD,
    AXIS_UP,
    FPS,
    G,
    animate,
    bind,
    ellipsoid,
    rig,
    tube,
    wave,
)

FLIPPER_FLATNESS = 0.26              # thickness of the blade, share of its chord
SCALE_STATIONS = 9                   # rows of scales from the elbow to the tip
HIND_SCALE_STATIONS = 4              # rows of scales on the hind rudder
STROKE_SECONDS = 2
STROKE_DEGREES = 16.0                # swing of the shoulder, every bone further out adds its own


# ===========================================================================
# =============================   TURTLE PARTS   ============================
# ===========================================================================
#
#   blade              A flat fleshy blade along an arm line, tiled with dark scales.
#   build_flipper      The long front flipper sweeping out and back.
#   build_hindpaddle   The short broad hind rudder.
#   build_lideye       A dark calm eye between a heavy upper and a lower lid, wrinkles around it.
#

def blade(scene, line, chords, stations):
    """
    A thick round tube along the arm line (Blender points, see G), pressed flat into a
    blade; chords holds the half chord at every point. Dark flat plates (the paint color
    of the back) tile it on top and below, three across the chord in the given number of
    rows, the skin shows as pale seams between them. Returns the meshes for the rig.
    """
    skin = tube(scene, 'Blade', line, chords, [(i, i + 1) for i in range(len(line) - 1)], 'Skin')

    # Pressed flat: Blender z is the up axis of Godot, the blade lies in the XZ plane of Godot
    for vertex in skin.data.vertices:
        vertex.co.z *= FLIPPER_FLATNESS
    meshes = [skin]

    plates = 0
    for station in range(stations):
        along = 0.22 + station / (stations - 1) * 0.74
        segment = min(int(along * (len(line) - 1)), len(line) - 2)
        share = along * (len(line) - 1) - segment
        center = line[segment].lerp(line[segment + 1], share)
        chord = chords[segment] + (chords[segment + 1] - chords[segment]) * share
        across = (line[segment + 1] - line[segment]).normalized().cross(AXIS_UP)
        for row in (-1, 0, 1):
            # Every second station is shifted half a plate, like bricks
            spot = center + across * chord * (row * 0.56 + (0.14 if 0 == station % 2 else -0.14))
            size = chord * (0.36 if 0 == row else 0.29)
            for up in (-1, 1):
                at = spot + AXIS_UP * (chord * FLIPPER_FLATNESS * (0.95 if 0 == row else 0.7) * up)
                meshes.append(ellipsoid(scene, f'Scale{plates}', at, (size, size, 0.022), 'BackSkin', subdivisions=2))
                plates += 1
    return meshes


def build_flipper(scene):
    """
    The front flipper of a sea turtle: it leaves the flank, sweeps out and bends back
    like a wing, widest behind the wrist, ending in a blunt tip. "Stroke" (2 s): it beats
    up and down from the shoulder, the tip trailing behind.
    """
    # Arm line from the shoulder (inside the body) to the tip
    line = [G(0.0, 0, -0.12), G(0.1, 0, 0.3), G(0.1, 0, 0.72), G(-0.06, 0, 1.1), G(-0.36, 0, 1.42), G(-0.74, 0, 1.62)]
    meshes = blade(scene, line, (0.17, 0.2, 0.3, 0.31, 0.22, 0.09), SCALE_STATIONS)

    # Stroke: three bones along the arm swing about the forward axis, each a little later than the one before
    bones = [
        ('Shoulder', line[0], line[2], None),
        ('Wrist', line[2], line[4], 'Shoulder'),
        ('Tip', line[4], line[5], 'Wrist'),
    ]
    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes)
    animate(armature, 'Stroke', FPS * STROKE_SECONDS, {
        'Shoulder': (AXIS_FORWARD, lambda share: STROKE_DEGREES * wave(share)),
        'Wrist':    (AXIS_FORWARD, lambda share: STROKE_DEGREES * 0.7 * wave(share, -0.12)),
        'Tip':      (AXIS_FORWARD, lambda share: STROKE_DEGREES * 0.7 * wave(share, -0.24)),
    })


def build_hindpaddle(scene):
    """
    The hind flipper of a sea turtle: a short broad rudder pointing out and back, round
    at its end. "Stroke" (2 s): a small lazy swing, the hind flippers only steer.
    """
    # Leg line from the hip (inside the body) to the round end
    line = [G(0.0, 0, -0.1), G(-0.06, 0, 0.26), G(-0.22, 0, 0.56), G(-0.42, 0, 0.8)]
    meshes = blade(scene, line, (0.17, 0.24, 0.34, 0.25), HIND_SCALE_STATIONS)

    bones = [
        ('Hip', line[0], line[2], None),
        ('Tip', line[2], line[3], 'Hip'),
    ]
    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes)
    animate(armature, 'Stroke', FPS * STROKE_SECONDS, {
        'Hip': (AXIS_FORWARD, lambda share: STROKE_DEGREES * 0.4 * wave(share, 0.5)),
        'Tip': (AXIS_FORWARD, lambda share: STROKE_DEGREES * 0.4 * wave(share, 0.38)),
    })


def build_lideye(scene):
    """
    The eye of an old sea turtle, seen from the flank (+Z):

            ___________      brow fold, higher at the front: a kind, wise look
          /  upper lid  \\    heavy, hangs over the top third of the eye
         |  ( (  o  ) )  |   dark eye: amber ring, big pupil, one glint
          \\__lower lid_/     a pouch below
            '-------'        crease under the pouch

    The eyeball is almost all iris, like the dark eye of the real animal; only a thin
    white shows at the corners.
    """
    ellipsoid(scene, 'Ball', G(0, 0, 0), (0.96, 0.96, 0.96), 'EyeWhite')

    # Iris, pupil and glint lie as flat lenses on the ball, looking out and a little forward
    ellipsoid(scene, 'Iris', G(0.08, -0.08, 0.74), (0.66, 0.28, 0.66), 'Beak', subdivisions=3)
    ellipsoid(scene, 'Pupil', G(0.1, -0.08, 0.84), (0.5, 0.22, 0.5), 'Pupil', subdivisions=3)
    ellipsoid(scene, 'Glint', G(0.3, 0.1, 1.0), (0.13, 0.07, 0.13), 'Glint', subdivisions=2)

    # Lids: two fat cushions of skin closing over the ball from above and below
    ellipsoid(scene, 'UpperLid', G(-0.02, 0.52, 0.1), (1.16, 1.02, 0.6), 'Skin')
    ellipsoid(scene, 'LowerLid', G(0.0, -0.72, 0.08), (1.1, 0.98, 0.42), 'Skin')

    # Wrinkles: a brow fold above the upper lid and a crease under the lower one
    brow = [G(-0.95, 0.78, 0.5), G(-0.45, 1.06, 0.62), G(0.2, 1.16, 0.62), G(0.8, 1.08, 0.5), G(1.12, 0.86, 0.3)]
    tube(scene, 'Brow', brow, (0.1, 0.15, 0.17, 0.14, 0.08), [(i, i + 1) for i in range(len(brow) - 1)], 'Skin')
    crease = [G(-0.9, -0.82, 0.42), G(-0.35, -1.12, 0.52), G(0.3, -1.14, 0.52), G(0.9, -0.86, 0.4)]
    tube(scene, 'Crease', crease, (0.06, 0.09, 0.09, 0.06), [(i, i + 1) for i in range(len(crease) - 1)], 'ShellRim')
