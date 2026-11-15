# NOTE:
#   New parts for the puffer fish of the organism stage: the pursed mouth with its two
#   fused tooth plates and a small round fan of a tail fin. Not registered in
#   build.models.py yet, preview with Show-Model.ps1.
#   Model space of the mouth: "Snout", like models/mouths.py (the back ends of a mouth
#   sit at x = 0, z = -1 and 1, the nose tip at x = 1). The puffer mouth only covers the
#   very tip of the nose, so it stays small on every head.
#   Model space of the fan tail: "Tail", like the tail fin of models/fins.py (root at
#   the origin at the tail tip, upright, reaching back along -X).
#
#   ./tools/blender/Show-Model.ps1 -Module new_puffer -Call 'build_puffermouth(scene)' -Name puffermouth
#   ./tools/blender/Show-Model.ps1 -Module new_puffer -Call 'build_fantail(scene)' -Name fantail

#
#   Chapters:
#     PUFFER PARTS   Pursed mouth with tooth plates, fan tail.
#

import math

from models.fins import fin
from models.helpers import AXIS_OUT, G, ellipsoid, tube

MOUTH_X = 0.89                       # the lips sit just behind the nose tip, sunk a little into the head
MOUTH_Y = -0.2                       # center of the mouth: low on the face, well below the eyes
LIP_WIDTH = 0.27                     # half width of the lip ring (z), share of the half width of the head
LIP_HEIGHT = 0.22                    # half height of the lip ring (y): the game squeezes y, the mouth ends up wider than tall
LIP_RADIUS = 0.07
LIP_POINTS = 14

FAN_RAYS = 7
FAN_SPREAD = 62.0                    # degrees the outer rays stand above and below the middle one
FAN_LENGTH = 0.42


# ===========================================================================
# ============================   PUFFER PARTS   =============================
# ===========================================================================
#
#   build_puffermouth   A small round "o" of fat lips, a dark throat and two tooth plates.
#   build_fantail       A small round tail fin like a hand fan, flutters like the other fins.
#

def build_puffermouth(scene):
    # Lips: a closed ring of clay facing forward, pouting a little at the top and bottom
    points = []
    for i in range(LIP_POINTS):
        angle = i / LIP_POINTS * math.tau
        pout = 0.05 * abs(math.sin(angle))
        points.append(G(MOUTH_X + pout, MOUTH_Y + math.sin(angle) * LIP_HEIGHT, math.cos(angle) * LIP_WIDTH))
    ring = [(i, (i + 1) % LIP_POINTS) for i in range(LIP_POINTS)]
    tube(scene, 'Lips', points, [LIP_RADIUS] * LIP_POINTS, ring, 'Pad', max_pieces=2)

    # Throat: a dark disc filling the ring
    ellipsoid(scene, 'Throat', G(MOUTH_X - 0.03, MOUTH_Y, 0), (0.06, LIP_WIDTH * 0.95, LIP_HEIGHT * 0.95), 'Dark', subdivisions=2)

    # The fused beak: one tooth plate above, one below, a dark gap between them
    for name, side in (('UpperPlate', 1), ('LowerPlate', -1)):
        ellipsoid(scene, name, G(MOUTH_X + 0.015, MOUTH_Y + side * LIP_HEIGHT * 0.5, 0),
                  (0.07, LIP_WIDTH * 0.82, LIP_HEIGHT * 0.44), 'Claw', subdivisions=2)


def build_fantail(scene):
    roots, tips = [], []
    for i in range(FAN_RAYS):
        share = i / (FAN_RAYS - 1) * 2.0 - 1.0                         # 1 top ray .. -1 bottom ray
        angle = math.radians(share * FAN_SPREAD)
        length = FAN_LENGTH * (1.0 - 0.12 * share * share)             # the outer rays a little shorter: a round edge
        roots.append(G(0.0, share * 0.06, 0))
        tips.append(G(-math.cos(angle) * length, math.sin(angle) * length, 0))
    fin(scene, roots, tips, AXIS_OUT, G(0.0, 0.0, 0), (0.09, 0.03, 0.09), 12.0)
