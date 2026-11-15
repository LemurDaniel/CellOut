# NOTE:
#   New parts of the sea turtle (assets/models/creature/): the flipper, a fleshy blade
#   instead of the ray fin of a fish. Not registered in build.models.py yet.
#   Model space "FlankPair": origin on the skin of the right flank, +Z out of the body,
#   +X forward, mirrored for the left flank; 1 unit = the height of the body there.
#
#   ./tools/blender/Show-Model.ps1 -Module new_turtle -Call 'build_flipper(scene)' -Name flipper

#
#   Chapters:
#     TURTLE PARTS   The flipper with its stroke.
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
STROKE_SECONDS = 2
STROKE_DEGREES = 16.0                # swing of the shoulder, every bone further out adds its own


# ===========================================================================
# =============================   TURTLE PARTS   ============================
# ===========================================================================
#
#   build_flipper   A long flat blade sweeping out and back, big scales along its front edge.
#

def build_flipper(scene):
    """
    The flipper of a sea turtle: a thick round tube along the arm, pressed flat into a
    blade. It leaves the flank, sweeps out and bends back like a wing, widest behind
    the wrist, ending in a blunt tip. "Stroke" (2 s): it beats up and down from the
    shoulder, the tip trailing behind.
    """
    # Arm line from the shoulder (inside the body) to the tip, with the half chord at every point
    line = [G(0.0, 0, -0.12), G(0.1, 0, 0.3), G(0.1, 0, 0.72), G(-0.06, 0, 1.1), G(-0.36, 0, 1.42), G(-0.74, 0, 1.62)]
    chords = (0.17, 0.2, 0.3, 0.31, 0.22, 0.09)
    blade = tube(scene, 'Blade', line, chords, [(i, i + 1) for i in range(len(line) - 1)], 'Skin')

    # Pressed flat: Blender z is the up axis of Godot, the blade lies in the XZ plane of Godot
    for vertex in blade.data.vertices:
        vertex.co.z *= FLIPPER_FLATNESS
    meshes = [blade]

    # Scales: dark flat plates (the paint color of the back) tile the blade on top and below,
    # three across the chord, the skin shows as pale seams between them
    plates = 0
    for station in range(SCALE_STATIONS):
        along = 0.22 + station / (SCALE_STATIONS - 1) * 0.74
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
