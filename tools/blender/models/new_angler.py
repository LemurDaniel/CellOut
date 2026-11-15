# NOTE:
#   New parts for the angler fish of the organism stage (assets/models/creature/): the
#   needle maw, a huge underbite full of long thin fangs. Registered in build.models.py.
#   Model space (Snout, the same as mouths.py): the mouth wraps around the snout as a
#   horseshoe that opens to the back. Its back ends sit at x = 0, z = -1 and 1 (the
#   flanks), its front at x = 1 (the nose tip). The game stretches x to the length of
#   the snout and z to its width.
#
#   ./tools/blender/Show-Model.ps1 -Module new_angler -Call 'build_needlemaw(scene)' -Name needlemaw

#
#   Chapters:
#     ANGLER PARTS   The needle maw.
#

import math

from models.helpers import (
    AXIS_OUT,
    FPS,
    G,
    animate,
    bind,
    cone,
    ellipsoid,
    rig,
    tube,
)

UPPER_Y = 0.65                       # height of the upper lip at the nose
CORNER_DROOP = 0.72                  # the upper lip sags this much towards the corners: a grumpy frown
LOWER_Y = -0.75                      # height of the lower lip at the chin
CORNER_RISE = 0.66                   # the lower lip climbs this much towards the corners of the mouth
UNDERBITE = 0.2                      # the chin juts this far past the nose tip
LOWER_FANGS = (0.25, 0.5, 0.4, 0.75, 0.5, 1.05, 0.7, 1.15, 0.6, 1.15, 0.7, 1.05, 0.5, 0.75, 0.4, 0.5, 0.25)   # lengths from flank to flank, ragged on purpose
UPPER_FANGS = (0.15, 0.3, 0.22, 0.45, 0.3, 0.6, 0.4, 0.7, 0.7, 0.4, 0.6, 0.3, 0.45, 0.22, 0.3, 0.15)


# ===========================================================================
# ============================   ANGLER PARTS   =============================
# ===========================================================================
#
#   around            A point on the horseshoe around the snout.
#   upper_lip         A point on the upper lip: sagging at the corners.
#   lower_lip         A point on the lower lip: jutting out at the chin, climbing at the corners.
#   build_needlemaw   Angler maw: a thin upper lip, a heavy jutting chin, long needle fangs, the chin chomps.
#   preview_on_head   Preview only: the maw stretched like in the game, on a stand-in for a fat head.
#

def around(degrees, height, reach=1.0):
    """0 degrees is the nose tip, -90 and 90 the flanks."""
    angle = math.radians(degrees)
    return G(math.cos(angle) * reach, height, math.sin(angle) * reach)


def upper_lip(degrees, drop=0.0, reach=1.0):
    """The upper lip at an angle, drop lowers the point below the lip."""
    return around(degrees, UPPER_Y - CORNER_DROOP * (abs(degrees) / 90.0) ** 2 - drop, reach)


def lower_lip(degrees, lift=0.0, pull=0.0):
    """
    The lower lip at an angle: lift raises the point above the lip, pull moves it
    towards the middle of the maw (for fang tips leaning inwards).
    """
    share = abs(degrees) / 90.0
    reach = 1.0 + UNDERBITE * math.cos(math.radians(degrees)) ** 2
    return around(degrees, LOWER_Y + CORNER_RISE * share ** 2 + lift, reach - pull)


def build_needlemaw(scene):
    angles = [-86 + i * 14.33 for i in range(13)]
    chain = [(i, i + 1) for i in range(len(angles) - 1)]

    # Lips: the upper one a thin dark rim, the lower one a fat sausage that sticks out
    rim = tube(scene, 'UpperLip', [upper_lip(a, 0.0, 1.01) for a in angles], [0.07] * len(angles), chain, 'Shell')
    chin_lip = tube(scene, 'LowerLip', [lower_lip(a) for a in angles],
                    [0.12 + 0.09 * math.cos(math.radians(a)) for a in angles], chain, 'Skin')
    # Chin: a bowl of skin closing the jaw from below, the maw a dark pit between the lips
    chin = ellipsoid(scene, 'Chin', G(0.3, LOWER_Y - 0.06, 0), (0.9, 1.0, 0.32), 'Skin')
    maw = ellipsoid(scene, 'Maw', G(0.3, (UPPER_Y + LOWER_Y) / 2 + 0.02, 0), (0.76, 0.95, 0.66), 'Dark')
    meshes = [rim, chin_lip, chin, maw]
    rigid = {'UpperLip': 'Skull', 'Maw': 'Skull', 'LowerLip': 'Jaw', 'Chin': 'Jaw'}

    # Lower fangs: long needles standing on the jutting chin in front of the upper lip
    # (the underbite), leaning back towards the face, the longest ones next to the middle
    for i, length in enumerate(LOWER_FANGS):
        degrees = -76 + i * 152 / (len(LOWER_FANGS) - 1)
        fang = cone(scene, f'LowerFang{i}', lower_lip(degrees, 0.04, 0.02), lower_lip(degrees, 0.04 + length, 0.05 + length * 0.12), 0.06)
        meshes.append(fang)
        rigid[fang.name] = 'Jaw'
    # Upper fangs: shorter, hanging into the gaps between the lower ones
    for i, length in enumerate(UPPER_FANGS):
        degrees = -70 + i * 140 / (len(UPPER_FANGS) - 1)
        fang = cone(scene, f'UpperFang{i}', upper_lip(degrees, 0.03, 1.03), upper_lip(degrees, 0.03 + length, 1.04), 0.05)
        meshes.append(fang)
        rigid[fang.name] = 'Skull'

    hinge = G(-0.1, LOWER_Y + CORNER_RISE, 0)
    armature = rig(scene, 'Rig', [
        ('Skull', G(-0.1, UPPER_Y, 0), G(0.9, UPPER_Y, 0), None),
        ('Jaw', hinge, G(1.1, LOWER_Y, 0), 'Skull'),
    ])
    bind(armature, meshes, rigid=rigid)

    # Chomp: the chin drops wide open, hangs a moment and snaps shut
    animate(armature, 'Chomp', FPS * 2, {
        'Jaw': (AXIS_OUT, lambda share: -16.0 * max(0.0, math.sin(share * math.tau)) ** 2),
    })


def preview_on_head(scene):
    """
    Preview only, never exported: the maw as the game stretches it (BuildMouth of
    CreatureParts3D.cs) on the head cap of a body with girth 1 at the last vertebra.
    """
    build_needlemaw(scene)
    armature = scene.objects['Rig']
    armature.scale = (0.57, 0.6, 0.41)
    armature.location = G(0.0, -0.1, 0.0)
    ellipsoid(scene, 'Head', G(-0.07, 0.0, 0.0), (0.62, 0.59, 0.55), 'Skin')