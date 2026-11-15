# NOTE:
#   New parts for the angler fish of the organism stage (assets/models/creature/): the
#   needle maw (the fangs of a huge frowning underbite) and the ghost eye (a tiny pale
#   deep sea eye under a mean brow). Registered in build.models.py.
#   The needle maw is built in the model space Snout: x 0 at the mouth corners .. 1 at
#   the nose tip, z -1 and 1 at the flanks, y 0 on the mouth line at the nose. The game
#   carves the opening itself (data/mouths.json, line NeedleMaw), the model only adds the
#   fangs on its edges, so MOUTH below must repeat that line.
#   The ghost eye is built in the model space Eye: an eyeball of radius 1 at the origin
#   on the right flank, +Z out of the head, +X forward.
#
#   ./tools/blender/Show-Model.ps1 -Module new_angler -Call 'build_needlemaw(scene)' -Name needlemaw
#   ./tools/blender/Show-Model.ps1 -Module new_angler -Call 'build_ghosteye(scene)' -Name ghosteye

#
#   Chapters:
#     ANGLER PARTS   The needle maw and the ghost eye.
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

# The opening the game carves for the kind NeedleMaw: a copy of its line in data/mouths.json
MOUTH = {'level': -0.1, 'smile': -0.3, 'gape': 0.28, 'jut': 0.8}
HEAD_PER_CAP = 0.85                  # height of a round head in lengths of its head cap (x unit of the model)
MODEL_HEIGHT = 0.75                  # the game scales y of the model to this share of the head height
LOWER_FANGS = (0.2, 0.45, 0.3, 0.7, 0.45, 0.95, 0.5, 0.95, 0.45, 0.7, 0.3, 0.45, 0.2)   # lengths from flank to flank, ragged on purpose
UPPER_FANGS = (0.15, 0.3, 0.22, 0.5, 0.3, 0.65, 0.65, 0.3, 0.5, 0.22, 0.3, 0.15)


# ===========================================================================
# ============================   ANGLER PARTS   =============================
# ===========================================================================
#
#   smoothstep        The smoothstep of the game (Mathf.SmoothStep).
#   lip_point         A point on the upper or lower edge of the carved opening.
#   build_needlemaw   The angler maw: fat lips of skin and long needles on both edges of the opening.
#   build_ghosteye    A tiny pale eye with a glowing iris in a socket, under a mean brow.
#

def smoothstep(low, high, value):
    share = max(0.0, min(1.0, (value - low) / (high - low)))
    return share * share * (3.0 - 2.0 * share)


def lip_point(degrees, edge, lift=0.0, sink=0.03):
    """
    A point on an edge of the opening as the game carves it (CreatureClay.CarveMouth).
    degrees: 0 is the nose tip, -90 and 90 the mouth corners. edge: 1 the upper lip,
    -1 the lower one. lift moves the point up in model heights, sink into the head.
    """
    angle = math.radians(degrees)
    s = math.cos(angle)
    gape = MOUTH['gape'] * smoothstep(0.0, 0.3, s)
    line = MOUTH['smile'] * (1.0 - s) ** 2
    # The head is a ball: above and below its middle the skin sits further in
    height = MOUTH['level'] + line + edge * gape
    reach = math.sqrt(max(0.05, 1.0 - height * height)) - sink
    x = s * reach
    if edge < 0:
        # The lower jaw is pushed forward, the most at the nose
        x += MOUTH['jut'] * HEAD_PER_CAP * smoothstep(0.0, 1.0, s) * min(1.0, gape / 0.6)
    return G(x, (line + edge * gape) / MODEL_HEIGHT + lift, math.sin(angle) * reach)


def build_needlemaw(scene):
    # Lips: a rim of skin on both edges of the opening, the lower one fat like the scoop of a real angler
    angles = [-82 + i * 164 / 14 for i in range(15)]
    chain = [(i, i + 1) for i in range(len(angles) - 1)]
    upper = tube(scene, 'UpperLip', [lip_point(a, 1, 0.1, 0.0) for a in angles],
                 [0.05 + 0.05 * math.cos(math.radians(a)) for a in angles], chain, 'Skin')
    lower = tube(scene, 'LowerLip', [lip_point(a, -1, -0.12, 0.0) for a in angles],
                 [0.05 + 0.1 * math.cos(math.radians(a)) for a in angles], chain, 'Skin')
    # Maw: a dark plug deep in the opening, so the throat is black behind the fangs
    maw = ellipsoid(scene, 'Maw', G(0.3, 0.0, 0), (0.5, 0.72, MOUTH['gape'] / MODEL_HEIGHT * 0.9), 'Dark')
    meshes = [upper, lower, maw]
    rigid = {'UpperLip': 'Skull', 'LowerLip': 'Skull', 'Maw': 'Skull'}

    # Lower fangs: long needles standing on the jutting lower lip, leaning back towards
    # the face, the longest ones next to the middle
    for i, length in enumerate(LOWER_FANGS):
        degrees = -74 + i * 148 / (len(LOWER_FANGS) - 1)
        fang = cone(scene, f'LowerFang{i}', lip_point(degrees, -1, -0.05, 0.06), lip_point(degrees, -1, length, 0.06 + length * 0.1), 0.055)
        meshes.append(fang)
        rigid[fang.name] = 'Jaw'
    # Upper fangs: shorter, hanging into the gaps between the lower ones
    for i, length in enumerate(UPPER_FANGS):
        degrees = -68 + i * 136 / (len(UPPER_FANGS) - 1)
        fang = cone(scene, f'UpperFang{i}', lip_point(degrees, 1, 0.05, 0.07), lip_point(degrees, 1, -length, 0.1), 0.05)
        meshes.append(fang)
        rigid[fang.name] = 'Skull'

    armature = rig(scene, 'Rig', [
        ('Skull', G(-0.1, 0.4, 0), G(0.9, 0.4, 0), None),
        ('Jaw', G(-0.1, -0.4, 0), G(1.1, -0.4, 0), 'Skull'),
    ])
    bind(armature, meshes, rigid=rigid)

    # Chomp: the carved jaw does not move, so the lower fangs only twitch a little
    animate(armature, 'Chomp', FPS * 2, {
        'Jaw': (AXIS_OUT, lambda share: 3.0 * max(0.0, math.sin(share * math.tau)) ** 2),
    })


def build_ghosteye(scene):
    # Socket: a low mound of skin the small eye sits in
    ellipsoid(scene, 'Socket', G(0, 0, 0.0), (1.2, 0.45, 1.2), 'Skin')
    # Eye: a small pale ball, a glowing iris like the lantern, a pinhole pupil looking forward
    ellipsoid(scene, 'Ball', G(0.05, -0.05, 0.3), (0.72, 0.5, 0.72), 'EyeWhite')
    ellipsoid(scene, 'Iris', G(0.14, -0.07, 0.52), (0.5, 0.36, 0.5), 'Lantern', subdivisions=2)
    ellipsoid(scene, 'Pupil', G(0.22, -0.08, 0.76), (0.24, 0.16, 0.24), 'Pupil', subdivisions=2)
    ellipsoid(scene, 'Glint', G(0.34, 0.1, 0.84), (0.09, 0.06, 0.09), 'Glint', subdivisions=1)
    # Brow: a fat ridge of skin over the eye, dropping towards the nose: a mean frown
    tube(scene, 'Brow', [G(-0.95, 0.8, 0.15), G(-0.2, 0.7, 0.42), G(0.55, 0.42, 0.48), G(1.05, 0.14, 0.25)],
         (0.26, 0.3, 0.27, 0.18), [(0, 1), (1, 2), (2, 3)], 'Skin')