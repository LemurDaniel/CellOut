# NOTE:
#   New parts for the puffer fish of the organism stage: the pursed mouth with its two
#   fused tooth plates, the bulging eye with its golden iris and a small round fan of a
#   tail fin. Registered in build.models.py.
#   Model space of the mouth: "Snout" (the corners of the opening the head carves sit at
#   x = 0, z = -1 and 1, the nose tip at x = 1, the mouth line at y = 0). The opening of
#   the puffer is tiny (data/mouths.json), so x is squeezed hard: the model is deep in x.
#   Model space of the eye: "Eye" (eyeball of radius 1 at the origin on the right flank,
#   +Z out of the head, +X forward).
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

from mathutils import Matrix, Vector

from models.fins import fin
from models.helpers import AXIS_OUT, G, ellipsoid, tube

LIP_X = 0.2                          # the ring lies on the skin between the corners (x 0) and the nose tip (x 1), times LIP_DEPTH
LIP_WIDTH = 0.8                      # half width of the lip ring (z): the corners of the opening sit at 1
LIP_HEIGHT = 0.55                    # half height of the lip ring (y) around the mouth line at y 0
LIP_RADIUS = 0.2
LIP_DEPTH = 3.0                      # the game squeezes x to the short stretch from corners to nose: the lips stay fat
LIP_POINTS = 14
PLATE_X = 0.0                        # the tooth plates sit behind the lips in the opening
PLATE_DEPTH = 1.6
PLATE_HEIGHT = 0.2
PLATE_GAP = 0.05                     # half the dark gap between the two plates

EYE_LOOK_FORWARD = 0.75              # the pupil looks out of the flank and this far forward
EYE_LOOK_UP = 0.08
SOCKET_RING = 0.93                   # radius of the skin ring around the eyeball
SOCKET_OUT = 0.12                    # how far out of the head the ring sits
SOCKET_RADIUS = 0.2
SOCKET_POINTS = 16

FAN_RAYS = 7
FAN_SPREAD = 62.0                    # degrees the outer rays stand above and below the middle one
FAN_LENGTH = 0.42


# ===========================================================================
# ============================   PUFFER PARTS   =============================
# ===========================================================================
#
#   build_puffermouth   A small pout of fat lips around the carved opening and two tooth plates in it.
#   build_fantail       A small round tail fin like a hand fan, flutters like the other fins.
#   build_bulgeeye      A big eyeball in a socket of skin, golden iris dome, wide pupil.
#

def build_puffermouth(scene):
    # Lips: a fat ring of clay around the opening, wider than tall, the pout of a puffer
    points = []
    for i in range(LIP_POINTS):
        angle = i / LIP_POINTS * math.tau
        points.append(G(LIP_X, math.sin(angle) * LIP_HEIGHT, math.cos(angle) * LIP_WIDTH))
    ring = [(i, (i + 1) % LIP_POINTS) for i in range(LIP_POINTS)]
    lips = tube(scene, 'Lips', points, [LIP_RADIUS] * LIP_POINTS, ring, 'Pad', max_pieces=2)
    lips.data.transform(Matrix.Diagonal((LIP_DEPTH, 1.0, 1.0, 1.0)))

    # The fused beak: one tooth plate above, one below, a dark gap between them
    for name, side in (('UpperPlate', 1), ('LowerPlate', -1)):
        ellipsoid(scene, name, G(PLATE_X, side * (PLATE_GAP + PLATE_HEIGHT), 0),
                  (PLATE_DEPTH, LIP_WIDTH * 0.8, PLATE_HEIGHT), 'Claw', subdivisions=2)


def build_fantail(scene):
    roots, tips = [], []
    for i in range(FAN_RAYS):
        share = i / (FAN_RAYS - 1) * 2.0 - 1.0                         # 1 top ray .. -1 bottom ray
        angle = math.radians(share * FAN_SPREAD)
        length = FAN_LENGTH * (1.0 - 0.12 * share * share)             # the outer rays a little shorter: a round edge
        roots.append(G(0.0, share * 0.06, 0))
        tips.append(G(-math.cos(angle) * length, math.sin(angle) * length, 0))
    fin(scene, roots, tips, AXIS_OUT, G(0.0, 0.0, 0), (0.09, 0.03, 0.09), 12.0)


def build_bulgeeye(scene):
    """
    The eye of a porcupinefish: a big ball in a fleshy socket, a golden iris bulging out
    of it like a dome and a wide dark pupil that looks out and a little forward.
    Iris and pupil are balls sunk into the eyeball, only their caps show.
    """
    look = Vector((EYE_LOOK_FORWARD, EYE_LOOK_UP, 1.0)).normalized()        # Godot axes: +Z out of the head

    def ball(name, distance, radius, material_name, subdivisions=3):
        return ellipsoid(scene, name, G(look.x * distance, look.y * distance, look.z * distance), (radius, radius, radius), material_name, subdivisions=subdivisions)

    ball('Eyeball', 0.0, 1.0, 'EyeWhite', subdivisions=4)
    ball('Iris', 0.3, 0.8, 'Beak', subdivisions=4)
    ball('Pupil', 0.62, 0.55, 'Pupil', subdivisions=4)

    # Glint: a white dot on the upper front edge of the pupil
    glint = (look + Vector((0.14, 0.2, 0.0))).normalized() * 1.14
    ellipsoid(scene, 'Glint', G(glint.x, glint.y, glint.z), (0.11, 0.11, 0.11), 'Glint', subdivisions=2)

    # Socket: a ring of skin in the color of the back, the eyeball sits in it where it meets the head
    points = []
    for i in range(SOCKET_POINTS):
        angle = i / SOCKET_POINTS * math.tau
        points.append(G(math.cos(angle) * SOCKET_RING, math.sin(angle) * SOCKET_RING, SOCKET_OUT))
    tube(scene, 'Socket', points, [SOCKET_RADIUS] * SOCKET_POINTS, [(i, (i + 1) % SOCKET_POINTS) for i in range(SOCKET_POINTS)], 'BackSkin', max_pieces=2)
