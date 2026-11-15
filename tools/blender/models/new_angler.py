# NOTE:
#   New parts for the angler fish of the organism stage (assets/models/creature/): the
#   needle maw (a huge frowning underbite full of needle fangs) and the ghost eye (a tiny
#   pale deep sea eye under a mean brow). Registered in build.models.py.
#   The needle maw is built in the model space Snout: x 0 at the mouth corners .. 1 at
#   the nose tip, z -1 and 1 at the flanks, y 0 on the mouth line at the nose. The game
#   carves the opening and stands the fangs on its lips itself (data/mouths.json, line
#   NeedleMaw), the model only adds a dark throat.
#   The ghost eye is built in the model space Eye: an eyeball of radius 1 at the origin
#   on the right flank, +Z out of the head, +X forward.
#
#   ./tools/blender/Show-Model.ps1 -Module new_angler -Call 'build_needlemaw(scene)' -Name needlemaw
#   ./tools/blender/Show-Model.ps1 -Module new_angler -Call 'build_ghosteye(scene)' -Name ghosteye

#
#   Chapters:
#     ANGLER PARTS   The needle maw and the ghost eye.
#

from models.helpers import (
    G,
    ellipsoid,
    tube,
)


# ===========================================================================
# ============================   ANGLER PARTS   =============================
# ===========================================================================
#
#   build_needlemaw   The angler maw: only a dark throat plug, opening and fangs come from the game.
#   build_ghosteye    A tiny pale eye with a glowing iris in a socket, under a mean brow.
#

def build_needlemaw(scene):
    # Throat: a dark plug that just breaks through the back wall of the cavity, so a wide
    # gape ends in a black hole. On a mouth with a small gape it stays hidden in the head.
    ellipsoid(scene, 'Throat', G(0.32, 0.0, 0), (0.42, 0.68, 0.26), 'Dark')

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