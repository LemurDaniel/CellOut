# NOTE:
#   New parts for the cetaceans of the organism stage (whale, orca), not yet known to
#   the game: the long clay flipper of a humpback and the tall back fin of an orca. Both
#   are solid clay instead of rays and webbing, so they read from far away.
#   Not registered in build.models.py yet, preview with Show-Model.ps1:
#
#   ./tools/blender/Show-Model.ps1 -Module new_whale -Call 'build_flipper(scene)' -Name flipper
#   ./tools/blender/Show-Model.ps1 -Module new_whale -Call 'build_tallfin(scene)' -Name tallfin

#
#   Chapters:
#     CETACEAN PARTS   Flipper and tall back fin.
#

import math

from mathutils import Matrix

from models.mouths import mouth_stations, on_mouth, squash
from models.helpers import (
    AXIS_FORWARD,
    FPS,
    G,
    MATERIAL_COLORS,
    animate,
    bind,
    cone,
    ellipsoid,
    rig,
    tube,
    wave,
)

# The paint of the back: the pattern color of the body when it has one, else the body color.
# Flipper and back fin of an orca stay black on a white body that way.
MATERIAL_COLORS.setdefault('BackSkin', (0.2, 0.22, 0.27, 1.0))

FLIPPER_FLAT = 0.3                   # thickness of the flipper, share of its chord
FLIPPER_DROOP = 28.0                 # degrees the flipper hangs down from the flank
TALLFIN_FLAT = 0.2                   # thickness of the back fin, share of its chord


# ===========================================================================
# ===========================   CETACEAN PARTS   ============================
# ===========================================================================
#
#   flat_blade      A clay blade: a tube along a line, pressed flat, then turned.
#   build_flipper   Long flipper of a humpback: model space FlankPair, knobs on the leading edge. "Flutter", 2 s.
#   build_tallfin   Tall back fin of an orca: model space Top, a thick triangle. "Flutter", 2 s.
#   build_roundflipper   Round paddle of an orca: model space FlankPair. "Flutter", 2 s.
#   build_orcamaw   Peg teeth in the carved grin of an orca: model space Snout.
#   build_patcheye  Small dark eye with the white eye patch of an orca: model space Eye.
#

def flat_blade(scene, name, points, radii, flatten, turn, material_name):
    """
    A tube through points (Blender vectors), pressed flat by the Blender scale flatten
    and turned by the matrix turn afterwards. Returns the mesh.
    """
    blade = tube(scene, name, points, radii, [(i, i + 1) for i in range(len(points) - 1)], material_name)
    blade.data.transform(turn @ Matrix.Diagonal((*flatten, 1.0)))
    return blade


def build_flipper(scene):
    """
    Origin on the skin of the right flank, the flipper reaches out (+Z), sweeps back
    and hangs down. About 1.5 body heights long, like the flipper of a humpback.
    """
    # Built straight along +Z with a banana sweep to the back, flat in Y, then hung down
    turn = Matrix.Rotation(math.radians(FLIPPER_DROOP), 4, 'X')
    line = [G(0.0, 0, -0.08), G(0.0, 0, 0.3), G(-0.05, 0, 0.65), G(-0.15, 0, 1.0), G(-0.3, 0, 1.3), G(-0.46, 0, 1.52)]
    radii = (0.15, 0.2, 0.21, 0.18, 0.13, 0.07)
    blade = flat_blade(scene, 'Blade', line, radii, (1.0, 1.0, FLIPPER_FLAT), turn, 'BackSkin')
    meshes = [blade]

    # Knobs on the leading edge, the tubercles of a humpback
    for i in range(1, len(line)):
        for share in (0.25, 0.75):
            center = line[i - 1].lerp(line[i], share)
            radius = radii[i - 1] + (radii[i] - radii[i - 1]) * share
            knob = ellipsoid(scene, f'Knob{i}_{share}', turn @ (center + G(radius * 1.12, 0, 0)), (0.055, 0.05, 0.04), 'BackSkin', subdivisions=2)
            meshes.append(knob)

    # Shoulder: a fleshy lump where the flipper leaves the flank
    meshes.append(ellipsoid(scene, 'Shoulder', G(0.0, 0.0, 0.02), (0.2, 0.1, 0.1), 'BackSkin', subdivisions=3))

    # Three bones along the blade, it waves slowly up and down, the tip the most
    joints = [turn @ point for point in (line[0], line[2], line[4], line[5])]
    armature = rig(scene, 'Rig', [
        ('Blade0', joints[0], joints[1], None),
        ('Blade1', joints[1], joints[2], 'Blade0'),
        ('Blade2', joints[2], joints[3], 'Blade1'),
    ])
    bind(armature, meshes)
    animate(armature, 'Flutter', FPS * 2, {
        'Blade0': (AXIS_FORWARD, lambda share: 5.0 * wave(share)),
        'Blade1': (AXIS_FORWARD, lambda share: 7.0 * wave(share, -0.12)),
        'Blade2': (AXIS_FORWARD, lambda share: 9.0 * wave(share, -0.24)),
    })


def build_tallfin(scene):
    """
    Base at the origin on the back, rising (+Y) about 1.3 units, leaning back a little:
    the straight fin of an orca bull, thick clay with a rounded tip.
    """
    line = [G(0.02, -0.08, 0), G(0.0, 0.25, 0), G(-0.04, 0.6, 0), G(-0.1, 0.92, 0), G(-0.17, 1.16, 0), G(-0.23, 1.3, 0)]
    radii = (0.36, 0.3, 0.23, 0.16, 0.1, 0.055)
    blade = flat_blade(scene, 'Blade', line, radii, (1.0, TALLFIN_FLAT, 1.0), Matrix.Identity(4), 'BackSkin')
    # Foot: the fin grows out of a low ridge along the back
    foot = ellipsoid(scene, 'Foot', G(0.0, 0.0, 0), (0.42, 0.11, 0.07), 'BackSkin', subdivisions=3)

    armature = rig(scene, 'Rig', [
        ('Fin0', line[0], line[2], None),
        ('Fin1', line[2], line[4], 'Fin0'),
        ('Fin2', line[4], line[5], 'Fin1'),
    ])
    bind(armature, [blade, foot], rigid={'Foot': 'Fin0'})
    # The tip wobbles a little from side to side
    animate(armature, 'Flutter', FPS * 2, {
        'Fin1': (AXIS_FORWARD, lambda share: 4.0 * wave(share)),
        'Fin2': (AXIS_FORWARD, lambda share: 6.0 * wave(share, -0.15)),
    })


def build_roundflipper(scene):
    """
    Origin on the skin of the right flank: the broad round paddle of an orca, smooth,
    reaching out (+Z), swept back a little and hanging down. About 0.9 body heights long.
    """
    turn = Matrix.Rotation(math.radians(FLIPPER_DROOP), 4, 'X')
    line = [G(0.02, 0, -0.08), G(0.0, 0, 0.2), G(-0.06, 0, 0.48), G(-0.15, 0, 0.72), G(-0.24, 0, 0.88)]
    radii = (0.15, 0.25, 0.3, 0.24, 0.12)
    blade = flat_blade(scene, 'Blade', line, radii, (1.0, 1.0, 0.22), turn, 'BackSkin')
    shoulder = ellipsoid(scene, 'Shoulder', G(0.0, 0.0, 0.02), (0.2, 0.1, 0.09), 'BackSkin', subdivisions=3)

    # Two bones, the paddle waves slowly up and down
    joints = [turn @ point for point in (line[0], line[2], line[4])]
    armature = rig(scene, 'Rig', [
        ('Blade0', joints[0], joints[1], None),
        ('Blade1', joints[1], joints[2], 'Blade0'),
    ])
    bind(armature, [blade, shoulder])
    animate(armature, 'Flutter', FPS * 2, {
        'Blade0': (AXIS_FORWARD, lambda share: 5.0 * wave(share)),
        'Blade1': (AXIS_FORWARD, lambda share: 8.0 * wave(share, -0.15)),
    })


# The opening the game carves for the kind OrcaMaw: must match its line in data/mouths.json
ORCA_MAW = {'length': 1.7, 'level': -0.32, 'smile': 0.2, 'gape': 0.1}


def build_orcamaw(scene):
    """
    The teeth in the carved grin of an orca or dolphin: two rows of blunt pegs that
    mesh like a zip, the upper ones hang, the lower ones stand in the gaps.
    """
    shape = ORCA_MAW
    stations = mouth_stations(shape, 8, 23)
    for i, (degrees, back) in enumerate(stations):
        is_upper = 0 == i % 2
        edge = 1.0 if is_upper else -1.0
        base, s = on_mouth(shape, degrees, back, edge * 0.92, 0.0, 0.955)
        tip, _ = on_mouth(shape, degrees, back, -edge * 0.3, 0.0, 0.965)
        if s < 0.3:
            continue
        cone(scene, f'Tooth{i}', base, tip, 0.065, 'Claw')
    squash(scene, shape)


def build_patcheye(scene):
    """
    Model space Eye (eyeball of radius 1 at the origin on the right flank, +Z out, +X
    forward): a small dark eye, and behind and above it the white oval eye patch of an orca.
    """
    # Patch: a flat white dome on the skin, long along the body, tilted up towards the back
    patch = ellipsoid(scene, 'Patch', G(0, 0, 0), (1.55, 0.75, 0.62), 'EyeWhite', subdivisions=3)
    patch.data.transform(Matrix.Translation(G(-0.75, 0.42, -0.22)) @ Matrix.Rotation(math.radians(-12.0), 4, 'Y'))
    # Eye: a dark bead below the front end of the patch, with a lid in the color of the back and a glint
    ellipsoid(scene, 'Ball', G(0.75, -0.38, 0.0), (0.5, 0.5, 0.5), 'EyeWhite', subdivisions=3)
    ellipsoid(scene, 'Pupil', G(0.82, -0.38, 0.14), (0.42, 0.42, 0.42), 'Pupil', subdivisions=3)
    ellipsoid(scene, 'Lid', G(0.72, -0.2, -0.04), (0.6, 0.5, 0.48), 'BackSkin', subdivisions=3)
    ellipsoid(scene, 'Glint', G(0.95, -0.28, 0.5), (0.12, 0.12, 0.12), 'Glint', subdivisions=1)
