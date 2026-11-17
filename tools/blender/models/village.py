# NOTE:
#   Props of pack and tribe (assets/models/village/): the woven nest of the pack, the
#   fire pit of the tribe, the marker of a free building plot and the low wall around the
#   settlement. Nest
#   and fire stand on the origin, about 1 unit across, the game scales them
#   (Tribe/Pack/Nest.cs); the plot is 3.2 units across in the scale of the building blocks
#   (Tribe/Village/SettlementPlots.cs). No rig, the flames of the fire are game primitives that
#   flicker and glow. Registered in build.models.py.

#
#   Chapters:
#     VILLAGE PARTS   Twigs, the nest and the fire pit.
#     PLOT            The staked out building plot.
#     WALL            The low wall around the settlement and its gate.
#     CITY PLOT       The building plot of a city: cones, tape and painted marks.
#

import math
import random

from mathutils import Vector

from models.buildings import beam, lashing, pole, rock
from models.helpers import G, ellipsoid, tube
from models.trees import clouds_texture


# ===========================================================================
# ============================   VILLAGE PARTS   ============================
# ===========================================================================
#
#   twig_ring     A ring of short bent twigs lying tangentially around the nest.
#   build_nest    A mossy mound with a hollow, two rings of woven twigs, down and leaves.
#   build_fire    A ring of stones around crossed logs on a bed of embers.
#
#   Coordinates in Godot axes through G (+Y up), ellipsoid scales in Blender axes
#   (across, out, up) like the trees. Fixed seeds: the same model every build.
#

LOW_POLY = {'subdivisions': 1, 'max_pieces': 3}       # tube settings like the trees


def twig_ring(scene, prefix, rng, count, radius, height, thickness, arc):
    """count twigs around the ring at about radius and height, each a bent arc of 4 points."""
    for i in range(count):
        start = i / count * math.tau + rng.uniform(-0.1, 0.1)
        span = rng.uniform(arc * 0.7, arc * 1.3)
        lift = rng.uniform(-0.03, 0.03)
        points = []
        for k in range(4):
            share = k / 3
            angle = start + span * share
            r = radius + rng.uniform(-0.02, 0.02) + 0.02 * math.sin(share * math.pi)
            y = height + lift + 0.03 * math.sin(share * math.pi + rng.uniform(0, 1))
            points.append(G(math.cos(angle) * r, y, math.sin(angle) * r))
        radii = [thickness * 0.7, thickness, thickness, thickness * 0.6]
        tube(scene, f'{prefix}{i}', points, radii, [(0, 1), (1, 2), (2, 3)], 'Twig', **LOW_POLY)


def build_nest(scene):
    rng = random.Random(7)
    texture = clouds_texture()
    # Mossy mound with a flattened hollow of dark earth
    ellipsoid(scene, 'Mound', G(0, 0.0, 0), (0.44, 0.44, 0.11), 'Moss', subdivisions=3, texture=texture, strength=0.03)
    ellipsoid(scene, 'Hollow', G(0, 0.085, 0), (0.27, 0.27, 0.03), 'Earth', subdivisions=2)
    # Two rings of woven twigs: an upper tight ring and a lower looser one outside it
    twig_ring(scene, 'Twig', rng, count=26, radius=0.36, height=0.13, thickness=0.016, arc=0.5)
    twig_ring(scene, 'Outer', rng, count=18, radius=0.43, height=0.06, thickness=0.02, arc=0.42)
    # A few twig ends sticking out
    for i in range(7):
        angle = rng.uniform(0, math.tau)
        base = G(math.cos(angle) * 0.38, 0.12, math.sin(angle) * 0.38)
        tip = G(math.cos(angle + 0.3) * 0.55, 0.16 + rng.uniform(0, 0.08), math.sin(angle + 0.3) * 0.55)
        tube(scene, f'End{i}', [base, tip], [0.016, 0.008], [(0, 1)], 'Twig', **LOW_POLY)
    # Down feathers in the hollow, leaves on the rim
    for i in range(6):
        angle = rng.uniform(0, math.tau)
        r = rng.uniform(0.05, 0.2)
        ellipsoid(scene, f'Down{i}', G(math.cos(angle) * r, 0.11, math.sin(angle) * r), (0.05, 0.03, 0.012), 'Down', subdivisions=1)
    for i in range(5):
        angle = i / 5 * math.tau + 0.5
        ellipsoid(scene, f'Leaf{i}', G(math.cos(angle) * 0.4, 0.15, math.sin(angle) * 0.4), (0.07, 0.045, 0.01), 'Leaf', subdivisions=1)


def build_fire(scene):
    rng = random.Random(11)
    # Ring of stones
    for i in range(9):
        angle = i / 9 * math.tau + rng.uniform(-0.12, 0.12)
        size = rng.uniform(0.08, 0.12)
        ellipsoid(scene, f'Stone{i}', G(math.cos(angle) * 0.42, size * 0.5, math.sin(angle) * 0.42),
                  (size, size * rng.uniform(0.8, 1.1), size * rng.uniform(0.6, 0.85)), 'Stone', subdivisions=1)
    # Bed of embers, three logs crossing over it
    ellipsoid(scene, 'Embers', G(0, 0.02, 0), (0.24, 0.24, 0.035), 'Ember', subdivisions=2)
    for i in range(3):
        angle = i / 3 * math.tau + 0.4
        start = G(math.cos(angle) * 0.27, 0.05, math.sin(angle) * 0.27)
        end = G(-math.cos(angle) * 0.22, 0.11 + i * 0.03, -math.sin(angle) * 0.22)
        tube(scene, f'Log{i}', [start, end], [0.045, 0.038], [(0, 1)], 'Log', **LOW_POLY)


# ===========================================================================
# =================================   PLOT   ================================
# ===========================================================================
#
#   build_plot   Four corner pegs with a sagging rope around them and a few stones at the
#                corners: here a building may stand.
#
#   In the scale of the building blocks (one grid cell 0.5 units), the origin in the
#   middle of the plot on the ground. Low: the preview of a building stands in it.
#

PLOT_HALF = 1.6                                       # half the side of the staked out square
PLOT_PEG_HEIGHT = 0.5
PLOT_ROPE_HEIGHT = 0.36                               # where the rope is tied to a peg
PLOT_ROPE_SAG = 0.16                                  # how far it hangs down in the middle of a side


def build_plot(scene):
    rng = random.Random(401)
    corners = [(1, 1), (1, -1), (-1, -1), (-1, 1)]
    for i, (sx, sz) in enumerate(corners):
        x, z = sx * PLOT_HALF, sz * PLOT_HALF
        lean = (rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03))
        # Peg with a hewn point, driven in a little crooked
        pole(scene, f"Peg{i}", [G(x, 0.0, z), G(x + lean[0], PLOT_PEG_HEIGHT * 0.7, z + lean[1]), G(x + lean[0] * 1.4, PLOT_PEG_HEIGHT, z + lean[1] * 1.4)],
             [0.1, 0.09, 0.0], "Wood", sides=6, is_level=True)
        lashing(scene, f"Knot{i}", x + lean[0] * 0.6, PLOT_ROPE_HEIGHT, z + lean[1] * 0.6, 0.09)
        rock(scene, f"Stone{i}", (x * 0.93 + rng.uniform(-0.05, 0.05), 0.04, z * 0.93 + rng.uniform(-0.05, 0.05)),
             (rng.uniform(0.1, 0.14), 0.06, rng.uniform(0.08, 0.11)), "Stone", rng, turn=rng.uniform(0.0, 3.0))
        # Rope to the next corner, sagging in the middle
        nx, nz = corners[(i + 1) % 4][0] * PLOT_HALF, corners[(i + 1) % 4][1] * PLOT_HALF
        points = []
        for step in range(7):
            share = step / 6
            sag = PLOT_ROPE_SAG * math.sin(share * math.pi)
            points.append(G(x + (nx - x) * share, PLOT_ROPE_HEIGHT - sag, z + (nz - z) * share))
        pole(scene, f"Rope{i}", points, [0.032] * len(points), "Rope", sides=5)


# ===========================================================================
# =================================   WALL   ================================
# ===========================================================================
#
#   build_wall        One straight piece of the low wall: two courses of field stones
#                     with a few sharpened stakes leaning outward.
#   build_wall_gate   An opening of the same length: two tall posts on stone feet under a
#                     lashed crossbeam.
#
#   In the scale of the building blocks, the origin in the middle of the piece on the
#   ground, the piece runs along Z, +X looks out of the settlement. The game sets the
#   pieces as a ring around the foundation (Tribe/Village/SettlementWall.cs). Low enough
#   to step over: it marks the edge, it blocks nobody.
#

WALL_LENGTH = 2.6                                     # along Z, SettlementWall.PIECE_LENGTH holds the same number
WALL_STONES = 7                                       # stones of the lower course
WALL_HEIGHT = 0.42                                    # top of the upper course
WALL_STAKES = 4
GATE_HEIGHT = 1.7                                     # top of the posts
GATE_HALF = 0.95                                      # posts left and right of the opening


def build_wall(scene):
    rng = random.Random(402)
    half = WALL_LENGTH / 2
    step = WALL_LENGTH / WALL_STONES
    # Lower course: big stones side by side, a little over the ends so two pieces close up
    for i in range(WALL_STONES):
        z = -half + step * (i + 0.5) + rng.uniform(-0.03, 0.03)
        rock(scene, f"Base{i}", (rng.uniform(-0.04, 0.04), 0.13, z), (rng.uniform(0.2, 0.26), rng.uniform(0.15, 0.19), step * 0.6),
             "Stone", rng, turn=rng.uniform(-0.25, 0.25))
    # Upper course: smaller stones on the joints
    for i in range(WALL_STONES - 1):
        z = -half + step * (i + 1.0) + rng.uniform(-0.04, 0.04)
        rock(scene, f"Cap{i}", (rng.uniform(-0.05, 0.05), WALL_HEIGHT - 0.12, z), (rng.uniform(0.14, 0.19), rng.uniform(0.1, 0.13), step * 0.5),
             "Stone", rng, turn=rng.uniform(-0.4, 0.4))
    # Sharpened stakes between the stones, leaning outward
    for i in range(WALL_STAKES):
        z = -half + WALL_LENGTH * (i + 0.5) / WALL_STAKES + rng.uniform(-0.08, 0.08)
        lean = rng.uniform(0.22, 0.34)
        top = rng.uniform(0.74, 0.92)
        pole(scene, f"Stake{i}", [G(0.02, 0.0, z), G(0.02 + lean * 0.75, top * 0.78, z + rng.uniform(-0.03, 0.03)), G(0.02 + lean, top, z)],
             [0.07, 0.06, 0.0], "Wood", sides=6)


def build_wall_gate(scene):
    rng = random.Random(403)
    for i, side in enumerate((-1, 1)):
        z = side * GATE_HALF
        bend = rng.uniform(-0.04, 0.04)
        # Post, slightly bent, with a flat top
        pole(scene, f"Post{i}", [G(0.0, 0.0, z), G(bend, GATE_HEIGHT * 0.5, z), G(bend * 0.4, GATE_HEIGHT, z)],
             [0.15, 0.13, 0.12], "Wood", sides=7, is_level=True)
        lashing(scene, f"Knot{i}", bend * 0.5, GATE_HEIGHT - 0.2, z, 0.15)
        # Stones heaped against its foot, towards the wall that ends here
        for k in range(3):
            rock(scene, f"Foot{i}{k}", (rng.uniform(-0.08, 0.08), 0.12 + 0.03 * (2 - k), z + side * (0.12 + 0.14 * k)),
                 (rng.uniform(0.17, 0.23), rng.uniform(0.13, 0.2) * (1.25 - 0.2 * k), 0.15), "Stone", rng, turn=rng.uniform(-0.4, 0.4))
    # Crossbeam through the lashings, a little crooked and longer than the opening
    pole(scene, "Beam", [G(0.0, GATE_HEIGHT - 0.24, -GATE_HALF - 0.3), G(0.03, GATE_HEIGHT - 0.14, 0.0), G(0.0, GATE_HEIGHT - 0.2, GATE_HALF + 0.3)],
         [0.09, 0.1, 0.085], "Wood", sides=6)


# ===========================================================================
# ===============================   CITY PLOT   =============================
# ===========================================================================
#
#   build_city_plot   The free building plot of a city: white corner marks painted on the
#                     asphalt, a traffic cone on every corner and red and white barrier
#                     tape between them.
#
#   The same frame and size as build_plot, straight and clean like the modern blocks.
#

CONE_HEIGHT = 0.42
TAPE_HEIGHT = 0.3                                     # where the tape is tied around a cone
TAPE_PIECES = 8                                       # red and white in turns along one side
MARK_LENGTH = 0.7                                     # legs of a painted corner mark


def build_city_plot(scene):
    corners = [(1, 1), (1, -1), (-1, -1), (-1, 1)]
    for i, (sx, sz) in enumerate(corners):
        x, z = sx * PLOT_HALF, sz * PLOT_HALF
        # Cone: black foot plate, red body with a white band, a flat tip
        beam(scene, f"Foot{i}", G(x, 0.0, z), G(x, 0.03, z), 0.3, 0.3, "Rubber")
        pole(scene, f"Cone{i}", [G(x, 0.03, z), G(x, 0.18, z)], [0.12, 0.085], "FruitRed", sides=10, is_level=True)
        pole(scene, f"Band{i}", [G(x, 0.18, z), G(x, 0.27, z)], [0.087, 0.066], "Bone", sides=10, is_level=True)
        pole(scene, f"Tip{i}", [G(x, 0.27, z), G(x, CONE_HEIGHT, z)], [0.064, 0.03], "FruitRed", sides=10, is_level=True)
        # Corner mark painted on the ground, inside the cones
        inner_x, inner_z = x - sx * 0.2, z - sz * 0.2
        beam(scene, f"MarkX{i}", G(inner_x, 0.006, inner_z), G(inner_x - sx * MARK_LENGTH, 0.006, inner_z), 0.09, 0.012, "Bone")
        beam(scene, f"MarkZ{i}", G(inner_x, 0.006, inner_z), G(inner_x, 0.006, inner_z - sz * MARK_LENGTH), 0.09, 0.012, "Bone")
        # Barrier tape to the next cone: flat pieces, red and white in turns, hanging a little
        nx, nz = corners[(i + 1) % 4][0] * PLOT_HALF, corners[(i + 1) % 4][1] * PLOT_HALF
        for piece in range(TAPE_PIECES):
            ends = []
            for share in (piece / TAPE_PIECES, (piece + 1) / TAPE_PIECES):
                sag = 0.06 * math.sin(share * math.pi)
                ends.append(G(x + (nx - x) * share, TAPE_HEIGHT - sag, z + (nz - z) * share))
            beam(scene, f"Tape{i}{piece}", ends[0], ends[1], 0.014, 0.07, "FruitRed" if 0 == piece % 2 else "Bone")
