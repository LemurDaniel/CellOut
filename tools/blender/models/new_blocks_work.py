# NOTE:
#   Building blocks of the working village (assets/models/building/): the ways up and the
#   edges of a platform, the stores next to a house and the yard where the tribe works.
#   Same frame as models/buildings.py: +Y up, +X is the front of a block, the origin is the
#   center of its footprint on the ground, one grid cell is 0.5 units, a block stays inside
#   its box (width along X x height x depth along Z). No rig. Materials Paint (the surface
#   the player repaints), PaintDark (its trim), Wood, Stone, Rope, Bone, Door, Ember, Flame,
#   Bronze, Clay, Hide, FruitRed, FruitGold, FruitGreen. Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only building/
#   ./tools/blender/Show-Model.ps1 -Module new_blocks_work -Call 'build_cart(scene)' -Name cart

#
#   Chapters:
#     PRIMITIVES   A point on a circle.
#     WAYS         Stairs (2 x 2 x 2 cells), railing (4 x 1 x 1) and wattle fence (4 x 1 x 1).
#     STORES       Barrel (1 x 1 x 1) and clay pots (1 x 1 x 1).
#     YARD         Drying rack (2 x 2 x 1), hand cart (2 x 1 x 2) and cooking fire (2 x 1 x 2).
#

import math
import random

from models.buildings import beam, curve, fruit, lashing, lathe, plate, pole, rock, sheet, trim_height
from models.helpers import G, ellipsoid


# ===========================================================================
# =============================   PRIMITIVES   ==============================
# ===========================================================================
#
#   ring     A point on a circle around the upright axis.
#

def ring(center_x, center_z, radius, angle, y):
    """The point at angle (radians) on a level circle of that radius around (center_x, center_z)."""
    return G(center_x + math.cos(angle) * radius, y, center_z + math.sin(angle) * radius)


# ===========================================================================
# =================================   WAYS   ================================
# ===========================================================================
#
#   build_stairs    Two stringers, six plank treads up to y = 1 at the back, a handrail
#                   on two posts along both sides of the lower half.
#   build_railing   Five short posts along X, a top rail lashed on, crossed laths between.
#   build_fence     Seven crooked stakes along X with four withies woven in and out.
#
#   The stairs end flat at y = 1 at their back edge (-X), level with stilts behind them.
#   The railing stands on platforms and flat roofs: its posts end flat at y = 0.
#

STAIR_TREADS = 6
STAIR_RAIL_LIFT = 0.43                                # the handrail above the line of the tread noses
STAIR_RAIL_GAP = 0.2                                  # the second rail below the handrail


def build_stairs(scene):
    rng = random.Random(401)
    rise = 1.0 / STAIR_TREADS                         # every tread climbs as far as it is deep
    # Stringers: slanted boards with a level foot and an upright cut at the back
    for side in (-1, 1):
        z = side * 0.32 - 0.035
        outline = [G(0.27, 0.0, z), G(0.47, 0.0, z), G(-0.5, 0.97, z), G(-0.5, 0.77, z)]
        plate(scene, f'Stringer{side}', outline, G(0.0, 0.0, 0.07), 'PaintDark')
    # Treads: planks across the stringers, the last one is the flat top at y = 1
    for i in range(STAIR_TREADS):
        is_top = STAIR_TREADS - 1 == i
        x = 0.5 - (i + 0.5) * rise + (0.0 if is_top else rng.uniform(-0.008, 0.008))
        y = (i + 1) * rise - 0.03
        beam(scene, f'Tread{i}', G(x, y, -0.43 + rng.uniform(0.0, 0.02)), G(x, y, 0.43 - rng.uniform(0.0, 0.02)),
             rise + 0.012, 0.06, 'Paint', None if is_top else rng, 0.005)
    # Handrail on both sides: a post on the ground, a taller one half way up, two rails between
    for side in (-1, 1):
        z = side * 0.445
        low, high = G(0.43, 0.07 + STAIR_RAIL_LIFT, z), G(-0.07, 0.57 + STAIR_RAIL_LIFT - 0.045, z)
        pole(scene, f'PostLow{side}', [G(0.43, 0.0, z), G(0.435, 0.28, z), G(0.43, 0.55, z)], [0.042, 0.037, 0.038], 'PaintDark', sides=6, is_level=True)
        pole(scene, f'PostHigh{side}', [G(-0.07, 0.6, z), G(-0.075, 0.8, z), G(-0.07, 0.995, z)], [0.042, 0.037, 0.038], 'PaintDark', sides=6, is_level=True)
        bow = G(0.0, rng.uniform(0.008, 0.02), 0.0)
        pole(scene, f'Rail{side}', [low + G(0.055, -0.055, 0.0), low.lerp(high, 0.5) + bow, high + G(-0.05, 0.03, 0.0)],
             [0.031, 0.034, 0.031], 'Paint', sides=6)
        drop = G(0.0, -STAIR_RAIL_GAP, 0.0)
        pole(scene, f'RailLow{side}', [low + drop + G(0.04, -0.04, 0.0), high + drop + G(-0.04, 0.04, 0.0)], [0.024, 0.024], 'PaintDark', sides=5)
        lashing(scene, f'RopeLow{side}', 0.43, 0.49, z, 0.038)
        lashing(scene, f'RopeHigh{side}', -0.07, 0.935, z, 0.038)
    trim_height(scene, 1.0)


def build_railing(scene):
    rng = random.Random(402)
    post_spots = [-0.93, -0.465, 0.0, 0.465, 0.93]
    for i, x in enumerate(post_spots):
        bend = rng.uniform(-0.012, 0.012)
        pole(scene, f'Post{i}', [G(x, 0.0, 0.0), G(x + bend, 0.22, bend), G(x, 0.43, 0.0)], [0.052, 0.045, 0.047], 'Paint', sides=6, is_level=True)
        lashing(scene, f'Rope{i}', x, 0.375, 0.0, 0.05)
    # Top rail lying on the posts, a little bowed
    pole(scene, 'Rail', [G(-0.99, 0.452, 0.0), G(-0.33, 0.458, 0.008), G(0.33, 0.45, -0.008), G(0.99, 0.455, 0.0)],
         [0.04, 0.043, 0.041, 0.04], 'Paint', sides=6)
    # Crossed laths in every bay: one in front of the posts, one behind
    for i, (near, far) in enumerate(zip(post_spots, post_spots[1:])):
        sag = rng.uniform(-0.02, 0.02)
        pole(scene, f'LathUp{i}', [G(near, 0.07 + sag, 0.03), G(far, 0.36 - sag, 0.03)], [0.029, 0.026], 'PaintDark', sides=5)
        pole(scene, f'LathDown{i}', [G(near, 0.36, -0.03), G(far, 0.07, -0.03)], [0.026, 0.029], 'PaintDark', sides=5)
    trim_height(scene, 0.5)


def build_fence(scene):
    rng = random.Random(403)
    stake_count, spacing = 7, 0.3
    stakes = []
    for i in range(stake_count):
        x = -spacing * (stake_count - 1) / 2 + spacing * i + rng.uniform(-0.02, 0.02)
        height = rng.uniform(0.39, 0.5)
        lean = rng.uniform(-0.04, 0.04)
        thick = rng.uniform(0.038, 0.048)
        # A stake with a hewn blunt point
        points = [G(x, 0.0, 0.0), G(x + lean * 0.5, height * 0.5, 0.0), G(x + lean, height - 0.07, 0.0), G(x + lean, height, 0.0)]
        pole(scene, f'Stake{i}', points, [thick, thick * 0.9, thick * 0.85, 0.014], 'PaintDark', sides=5, is_level=True)
        stakes.append(x)
    # Withies: in front of one stake, behind the next, every row the other way round
    for row, y in enumerate((0.07, 0.155, 0.24, 0.325)):
        start = 1 if 0 == row % 2 else -1
        points = [G(-0.985 + rng.uniform(0.0, 0.04), y, -start * 0.03)]
        side = start
        for i, x in enumerate(stakes):
            side = start if 0 == i % 2 else -start
            points.append(G(x, y + rng.uniform(-0.012, 0.012), side * 0.06))
        points.append(G(0.985 - rng.uniform(0.0, 0.04), y, -side * 0.03))
        radius = rng.uniform(0.036, 0.042)
        samples = curve(points, 4 * (len(points) - 1) + 1)
        # Thin at both cut ends
        radii = [radius * 0.55] + [radius] * (len(samples) - 2) + [radius * 0.55]
        pole(scene, f'Withy{row}', samples, radii, 'Paint', sides=5)
    trim_height(scene, 0.5)


# ===========================================================================
# ================================   STORES   ===============================
# ===========================================================================
#
#   barrel_radius   Radius of the barrel at a height: widest at the bilge.
#   build_barrel    Twelve staves with gaps over a dark core, four hoops, a lid with a
#                   batten and a bung.
#   build_pots      A glazed two-handled jar, a bowl of fruit and a small jug.
#

BARREL_HEIGHT = 0.45
BARREL_STAVES = 12
BARREL_GAP = 0.05                                     # share of a stave left open as the gap to the next
BARREL_LEVELS = (0.0, 0.11, 0.225, 0.34, 0.45)        # heights of the rings of a stave


def barrel_radius(y):
    """Radius of the barrel at height y: 0.2 at the bilge, 0.16 at both ends."""
    share = (y / BARREL_HEIGHT - 0.5) * 2.0
    return 0.2 - 0.04 * share * share


def build_barrel(scene):
    rng = random.Random(404)
    # Dark core showing through the gaps, its corners stand in the gaps between the staves
    core = [(barrel_radius(y) - 0.011, y) for y in BARREL_LEVELS[:-1]] + [(barrel_radius(0.41) - 0.008, 0.41)]
    lathe(scene, 'Core', core, 'PaintDark', segments=BARREL_STAVES, has_top_cap=True, has_bottom_cap=True)
    # Staves: bent boards, each a little shorter or thicker than its neighbour
    for i in range(BARREL_STAVES):
        middle = (i + 0.5) / BARREL_STAVES * math.tau
        half = (0.5 - BARREL_GAP) / BARREL_STAVES * math.tau
        swell = rng.uniform(-0.003, 0.004)
        top = BARREL_HEIGHT - rng.uniform(0.0, 0.012)
        rows = []
        for y in BARREL_LEVELS[:-1] + (top,):
            radius = barrel_radius(y) + swell
            rows.append([ring(0.0, 0.0, radius, middle - half, y), ring(0.0, 0.0, radius, middle + half, y)])
        sheet(scene, f'Stave{i}', rows, G(-math.cos(middle) * 0.022, 0.0, -math.sin(middle) * 0.022), 'Paint')
    # Hoops: two at the ends, two next to the bilge
    for i, y in enumerate((0.055, 0.15, 0.3, 0.395)):
        low, high = barrel_radius(y - 0.025), barrel_radius(y + 0.025)
        outer_low, outer_high = (low + 0.011, y - 0.025), (high + 0.011, y + 0.025)     # a flat band with hard edges
        lathe(scene, f'Hoop{i}', [(low + 0.003, y - 0.027), outer_low, outer_low, outer_high, outer_high, (high + 0.003, y + 0.027)],
              'Bronze', segments=BARREL_STAVES * 2)
    # Lid sunk below the rim, a batten across and a bung
    lathe(scene, 'Lid', [(0.152, 0.4), (0.152, 0.424)], 'Paint', segments=BARREL_STAVES, has_top_cap=True)
    beam(scene, 'Batten', G(-0.03, 0.432, -0.13), G(-0.03, 0.432, 0.13), 0.06, 0.02, 'PaintDark', rng, 0.004)
    pole(scene, 'Bung', [G(0.07, 0.42, 0.03), G(0.07, 0.475, 0.03)], [0.034, 0.03], 'Wood', sides=6, is_level=True)
    trim_height(scene, 0.5)


JAR_SPOT = (-0.1, 0.02)                               # (x, z) of the three vessels on the footprint
BOWL_SPOT = (0.125, 0.125)
JUG_SPOT = (0.115, -0.14)


def build_pots(scene):
    # Jar: a foot, a wide belly, a slim neck and a thick lip, the mouth dark
    lip, inner = (0.07, 0.492), (0.046, 0.492)
    jar = [(0.055, 0.0), (0.062, 0.025), (0.095, 0.08), (0.122, 0.15), (0.136, 0.22), (0.13, 0.28), (0.105, 0.335), (0.065, 0.38), (0.046, 0.43), (0.07, 0.465), lip, lip, inner, inner, (0.044, 0.47)]
    lathe(scene, 'Jar', jar, 'Paint', segments=14, center=JAR_SPOT, has_bottom_cap=True)
    lathe(scene, 'JarMouth', [(0.044, 0.47), (0.0, 0.47)], 'Door', segments=14, center=JAR_SPOT)
    lathe(scene, 'JarBand', [(0.131, 0.188), (0.143, 0.2), (0.143, 0.25), (0.131, 0.264)], 'PaintDark', segments=14, center=JAR_SPOT)
    lathe(scene, 'JarCollar', [(0.056, 0.375), (0.072, 0.39), (0.056, 0.405)], 'PaintDark', segments=14, center=JAR_SPOT)
    # Handles from the neck out and down to the shoulder, on both sides across the block
    for side in (-1, 1):
        bends = [G(JAR_SPOT[0], 0.445, JAR_SPOT[1] + side * 0.045), G(JAR_SPOT[0], 0.47, JAR_SPOT[1] + side * 0.12),
                 G(JAR_SPOT[0], 0.4, JAR_SPOT[1] + side * 0.155), G(JAR_SPOT[0], 0.31, JAR_SPOT[1] + side * 0.115)]
        pole(scene, f'JarHandle{side}', curve(bends, 7), [0.022] * 7, 'Paint', sides=5)
    # Bowl: outside, rim, inside, heaped with fruit
    rim, edge = (0.115, 0.115), (0.098, 0.115)
    lathe(scene, 'Bowl', [(0.05, 0.0), (0.058, 0.015), (0.095, 0.06), rim, rim, edge, edge, (0.0, 0.07)], 'Clay', segments=12, center=BOWL_SPOT, has_bottom_cap=True)
    heap = [('FruitRed', 0.045, 0.0, 0.1), ('FruitGold', -0.03, 0.04, 0.1), ('FruitGreen', -0.025, -0.045, 0.1), ('FruitGold', 0.005, 0.0, 0.15)]
    for i, (material_name, x, z, y) in enumerate(heap):
        fruit(scene, f'Fruit{i}', (BOWL_SPOT[0] + x, y, BOWL_SPOT[1] + z), 0.045, material_name)
    # Jug: a belly with a flaring mouth, one handle to the front
    brim, hole = (0.052, 0.225), (0.036, 0.225)
    lathe(scene, 'Jug', [(0.045, 0.0), (0.078, 0.07), (0.075, 0.13), (0.038, 0.185), brim, brim, hole, hole, (0.034, 0.21)], 'Clay', segments=10, center=JUG_SPOT, has_bottom_cap=True)
    lathe(scene, 'JugMouth', [(0.034, 0.21), (0.0, 0.21)], 'Door', segments=10, center=JUG_SPOT)
    bends = [G(JUG_SPOT[0] + 0.035, 0.205, JUG_SPOT[1]), G(JUG_SPOT[0] + 0.1, 0.2, JUG_SPOT[1]), G(JUG_SPOT[0] + 0.115, 0.13, JUG_SPOT[1]), G(JUG_SPOT[0] + 0.07, 0.085, JUG_SPOT[1])]
    pole(scene, 'JugHandle', curve(bends, 6), [0.017] * 6, 'Clay', sides=5)
    pole(scene, 'JugSpout', [G(JUG_SPOT[0] - 0.035, 0.205, JUG_SPOT[1]), G(JUG_SPOT[0] - 0.08, 0.235, JUG_SPOT[1])], [0.026, 0.013], 'Clay', sides=5)
    trim_height(scene, 0.5)


# ===========================================================================
# =================================   YARD   ================================
# ===========================================================================
#
#   hanging_fish        A gutted fish hanging head down by its tail fin.
#   build_drying_rack   Two crossed pairs of legs carrying a pole: four fish on one half,
#                       a hide laced into a frame on the other.
#   build_cart          Two plank wheels on an axle, a plank box on two shafts that rest
#                       on the ground in front, loaded with sacks and fruit.
#   build_firepit       A ring of stones around embers, crossed logs and flames, two forked
#                       sticks carrying a spit with a roast.
#
#   The flames are meshes of the material Flame with names starting with Flame, the game
#   makes them glow.
#

RACK_POLE_HEIGHT = 0.845                              # the cross pole lies in the forks of the legs
RACK_FRAME_DEPTH = 0.05                               # the hide frame leans on the pole from the front (z)


def hanging_fish(scene, name, x, top, length):
    """A fish below the height top: tail fin up and tied with a cord, flat body, head down."""
    ellipsoid(scene, f'{name}Body', G(x, top - 0.06 - length / 2, 0.0), (0.054, 0.021, length / 2), 'Bone', subdivisions=2)
    fin = [G(x, top - 0.1, -0.008), G(x - 0.05, top - 0.005, -0.008), G(x, top - 0.035, -0.008), G(x + 0.05, top - 0.005, -0.008)]
    plate(scene, f'{name}Tail', fin, G(0.0, 0.0, 0.016), 'Paint')
    pole(scene, f'{name}Cord', [G(x, top - 0.05, 0.0), G(x, top + 0.058, 0.0)], [0.011, 0.011], 'Rope', sides=4, is_level=True)


def build_drying_rack(scene):
    rng = random.Random(406)
    # Two pairs of legs crossing below their tops, lashed where they cross
    for i, x in enumerate((-0.455, 0.455)):
        for side in (-1, 1):
            foot, top = G(x, 0.0, side * 0.2), G(x, 0.985, -side * 0.045)
            bend = G(rng.uniform(-0.01, 0.01), 0.0, rng.uniform(-0.01, 0.01))
            pole(scene, f'Leg{i}{side}', [foot, foot.lerp(top, 0.5) + bend, top], [0.034, 0.03, 0.026], 'Paint', sides=6)
        lathe(scene, f'Rope{i}', [(0.03, 0.75), (0.052, 0.775), (0.052, 0.82), (0.03, 0.845)], 'Rope', segments=6, center=(x, 0.0))
    pole(scene, 'CrossPole', [G(-0.5, RACK_POLE_HEIGHT, 0.0), G(0.0, RACK_POLE_HEIGHT - 0.012, 0.0), G(0.5, RACK_POLE_HEIGHT, 0.0)],
         [0.027, 0.03, 0.027], 'Paint', sides=6)
    # Four fish of different length on the back half
    for i, (x, length) in enumerate(((-0.36, 0.3), (-0.25, 0.24), (-0.14, 0.34), (-0.03, 0.27))):
        hanging_fish(scene, f'Fish{i}', x, RACK_POLE_HEIGHT - 0.035, length)
    # Frame for the hide: two uprights on the ground leaning on the pole, two bars across
    left, right, low, high = 0.08, 0.4, 0.24, 0.77
    for name, x in (('FrameLeft', left), ('FrameRight', right)):
        pole(scene, name, [G(x, 0.0, RACK_FRAME_DEPTH), G(x + rng.uniform(-0.01, 0.01), 0.45, RACK_FRAME_DEPTH), G(x, 0.9, RACK_FRAME_DEPTH)],
             [0.026, 0.023, 0.021], 'PaintDark', sides=5, is_level=True)
    for name, y in (('FrameLow', low), ('FrameHigh', high)):
        pole(scene, name, [G(left - 0.04, y, RACK_FRAME_DEPTH), G(right + 0.04, y, RACK_FRAME_DEPTH)], [0.021, 0.021], 'PaintDark', sides=5)
    # Hide: pulled out at its four corners, drawn in between them
    center_x, center_y, half_wide, half_tall = (left + right) / 2, (low + high) / 2, 0.132, 0.22
    steps = 4
    rows = []
    for row in range(steps + 1):
        v = row / steps * 2.0 - 1.0
        points = []
        for column in range(steps + 1):
            u = column / steps * 2.0 - 1.0
            x = center_x + half_wide * u * (1.0 - 0.3 * (1.0 - v * v))
            y = center_y + half_tall * v * (1.0 - 0.2 * (1.0 - u * u))
            points.append(G(x, y, RACK_FRAME_DEPTH - 0.007))
        rows.append(points)
    sheet(scene, 'Hide', rows, G(0.0, 0.0, 0.014), 'Hide')
    # Lacing from the corners and the middle of every edge to the frame
    laces = [(u, v) for u in (-1, 0, 1) for v in (-1, 0, 1) if (u, v) != (0, 0)]
    for i, (u, v) in enumerate(laces):
        drawn_u, drawn_v = (1.0 if 0 != v else 0.7) * u, (1.0 if 0 != u else 0.8) * v
        on_hide = G(center_x + half_wide * drawn_u * 0.95, center_y + half_tall * drawn_v * 0.95, RACK_FRAME_DEPTH)
        on_frame = G(center_x + (right - left) / 2 * u, center_y + (high - low) / 2 * v, RACK_FRAME_DEPTH)
        pole(scene, f'Lace{i}', [on_hide, on_frame], [0.01, 0.01], 'Rope', sides=4)
    trim_height(scene, 1.0)


CART_AXLE = (-0.16, 0.21)                              # (x, y) of the axle, y is the radius of the wheels
CART_FLOOR = 0.25                                     # underside of the box


def build_cart(scene):
    rng = random.Random(407)
    axle_x, axle_y = CART_AXLE
    # Wheels: solid discs of three planks held by two battens, a hub in the middle
    pole(scene, 'Axle', [G(axle_x, axle_y, -0.47), G(axle_x, axle_y, 0.47)], [0.028, 0.028], 'Wood', sides=6)
    for side in (-1, 1):
        z = side * 0.4
        pole(scene, f'Wheel{side}', [G(axle_x, axle_y, z - 0.03), G(axle_x, axle_y, z + 0.03)], [axle_y + 0.006, axle_y + 0.006], 'PaintDark', sides=14, is_smooth=False)     # a little more than the axle height: stands flat on the ground
        pole(scene, f'Hub{side}', [G(axle_x, axle_y, z - 0.05), G(axle_x, axle_y, z + side * 0.085)], [0.06, 0.05], 'Wood', sides=8)
        for name, x in (('BattenFront', 0.085), ('BattenBack', -0.085)):
            beam(scene, f'{name}{side}', G(axle_x + x, axle_y - 0.15, z + side * 0.036), G(axle_x + x, axle_y + 0.15, z + side * 0.036), 0.022, 0.045, 'Wood')
    # Shafts: under the box from the back to the front, then down to the ground, a grip across
    for side in (-1, 1):
        z = side * 0.22
        points = [G(-0.46, CART_FLOOR - 0.025, z), G(0.14, CART_FLOOR - 0.025, z), G(0.35, 0.12, z * 1.05), G(0.475, 0.03, z * 1.08)]
        pole(scene, f'Shaft{side}', points, [0.03, 0.03, 0.027, 0.025], 'Wood', sides=6)
    pole(scene, 'Grip', [G(0.44, 0.065, -0.29), G(0.44, 0.065, 0.29)], [0.022, 0.022], 'Wood', sides=5)
    # Box: a floor, two planks on every side with a gap, four corner posts
    back, front, half_depth, wall = -0.47, 0.14, 0.31, 0.09
    middle_x = (back + front) / 2
    beam(scene, 'Floor', G(middle_x, CART_FLOOR, 0.0), G(middle_x, CART_FLOOR + 0.03, 0.0), half_depth * 2.0, front - back, 'PaintDark')
    beam(scene, 'Shade', G(middle_x, CART_FLOOR + 0.03, 0.0), G(middle_x, CART_FLOOR + 0.11, 0.0), half_depth * 2.0 - 0.04, front - back - 0.04, 'Door')
    for row in range(2):
        y = CART_FLOOR + 0.03 + wall * (row + 0.5)
        for side in (-1, 1):
            reach = half_depth + rng.uniform(-0.004, 0.004)
            beam(scene, f'SideX{row}{side}', G(back + rng.uniform(0.0, 0.015), y, side * reach), G(front - rng.uniform(0.0, 0.015), y, side * reach),
                 0.03, wall - 0.014, 'Paint', rng, 0.004)
        beam(scene, f'Back{row}', G(back + 0.015, y, -half_depth), G(back + 0.015, y, half_depth), 0.03, wall - 0.014, 'Paint', rng, 0.004)
        beam(scene, f'Front{row}', G(front - 0.015, y, -half_depth), G(front - 0.015, y, half_depth), 0.03, wall - 0.014, 'Paint', rng, 0.004)
    for i, (x, z) in enumerate(((back, -1), (back, 1), (front, -1), (front, 1))):
        inset = 0.012 if x < 0.0 else -0.012
        beam(scene, f'Post{i}', G(x + inset, CART_FLOOR - 0.01, z * half_depth), G(x + inset, CART_FLOOR + 0.225, z * half_depth), 0.05, 0.05, 'PaintDark')
    # Load: three sacks tied at the top, fruit in the gaps
    sacks = [(-0.32, -0.15, 0.125), (-0.31, 0.13, 0.13), (-0.07, -0.13, 0.13)]       # (x, z, size), every other one lies across
    for i, (x, z, size) in enumerate(sacks):
        ellipsoid(scene, f'Sack{i}', G(x, CART_FLOOR + 0.135, z), (size * (1.0 if 0 == i % 2 else 0.85), size * (0.85 if 0 == i % 2 else 1.0), 0.11), 'Hide', subdivisions=2)
        lathe(scene, f'SackTie{i}', [(0.03, 0.47), (0.042, 0.482), (0.03, 0.494)], 'Rope', segments=6, center=(x, z))
        pole(scene, f'SackNeck{i}', [G(x, 0.45, z), G(x, 0.5, z)], [0.028, 0.045], 'Hide', sides=6, is_level=True)
    for i, (material_name, x, z) in enumerate((('FruitGold', 0.05, 0.21), ('FruitRed', -0.05, 0.22), ('FruitGold', 0.06, 0.09), ('FruitGreen', -0.14, 0.22),
                                               ('FruitRed', 0.06, -0.03), ('FruitGreen', -0.03, 0.11), ('FruitRed', 0.0, 0.16))):
        # The last fruit lies on top of the heap
        fruit(scene, f'Fruit{i}', (x, CART_FLOOR + (0.19 if 6 == i else 0.15), z), 0.058, material_name)
    trim_height(scene, 0.5)


FIRE_RING = 0.33                                      # radius of the ring of stones
FIRE_SPIT_HEIGHT = 0.4


def build_firepit(scene):
    rng = random.Random(408)
    # Ring of stones, every one a different size
    stone_count = 8
    for i in range(stone_count):
        angle = (i + rng.uniform(-0.15, 0.15)) / stone_count * math.tau
        size = (rng.uniform(0.07, 0.09), rng.uniform(0.06, 0.085), rng.uniform(0.1, 0.125))
        rock(scene, f'Stone{i}', (math.cos(angle) * FIRE_RING, size[1] * 0.6, math.sin(angle) * FIRE_RING), size, 'Stone', rng, turn=angle)
    # Bed of embers and ash inside the ring
    lathe(scene, 'Ash', [(FIRE_RING - 0.01, 0.0), (FIRE_RING - 0.04, 0.03), (0.0, 0.045)], 'Door', segments=10, has_bottom_cap=True)
    lathe(scene, 'Embers', [(0.17, 0.03), (0.13, 0.065), (0.0, 0.08)], 'Ember', segments=8)
    # Logs leaning together over the embers
    for i in range(4):
        angle = (i + 0.3) / 4 * math.tau + rng.uniform(-0.2, 0.2)
        foot, head = ring(0.0, 0.0, 0.26, angle, 0.05), ring(0.0, 0.0, 0.09, angle + math.pi, 0.2)
        pole(scene, f'Log{i}', [foot, head], [0.056, 0.046], 'Wood', sides=6)
    # Flames: a big one below the roast, two tongues licking up in front of and behind it
    pole(scene, 'Flame0', [G(0.0, 0.07, 0.0), G(0.02, 0.15, 0.01), G(-0.015, 0.24, -0.01), G(0.015, 0.325, 0.015)], [0.09, 0.1, 0.055, 0.0], 'Flame', sides=7, is_level=True)
    pole(scene, 'Flame1', [G(0.13, 0.07, -0.05), G(0.15, 0.17, -0.065), G(0.125, 0.3, -0.05), G(0.15, 0.43, -0.06)], [0.055, 0.065, 0.04, 0.0], 'Flame', sides=6, is_level=True)
    pole(scene, 'Flame2', [G(-0.12, 0.07, 0.08), G(-0.14, 0.15, 0.09), G(-0.12, 0.26, 0.075), G(-0.135, 0.37, 0.085)], [0.05, 0.06, 0.035, 0.0], 'Flame', sides=6, is_level=True)
    # Forked sticks outside the ring, across the block (z), the spit lies in the forks
    for side in (-1, 1):
        z = side * 0.445
        pole(scene, f'Stick{side}', [G(0.0, 0.0, z), G(0.01, 0.2, z), G(0.0, FIRE_SPIT_HEIGHT - 0.04, z)], [0.034, 0.03, 0.03], 'Paint', sides=6, is_level=True)
        for name, x in (('ProngFront', 0.055), ('ProngBack', -0.055)):
            pole(scene, f'{name}{side}', [G(0.0, FIRE_SPIT_HEIGHT - 0.06, z), G(x, FIRE_SPIT_HEIGHT + 0.09, z)], [0.026, 0.02], 'Paint', sides=5)
    pole(scene, 'Spit', [G(0.0, FIRE_SPIT_HEIGHT, -0.495), G(0.0, FIRE_SPIT_HEIGHT - 0.008, 0.0), G(0.0, FIRE_SPIT_HEIGHT, 0.495)], [0.017, 0.02, 0.017], 'PaintDark', sides=5)
    # Roast on the spit: a plump body, two drumsticks with bone ends
    ellipsoid(scene, 'Roast', G(0.0, FIRE_SPIT_HEIGHT, 0.0), (0.085, 0.15, 0.075), 'Clay', subdivisions=2)
    for side in (-1, 1):
        pole(scene, f'Drumstick{side}', [G(side * 0.05, FIRE_SPIT_HEIGHT + 0.02, 0.1), G(side * 0.085, FIRE_SPIT_HEIGHT + 0.045, 0.19), G(side * 0.09, FIRE_SPIT_HEIGHT + 0.05, 0.235)],
             [0.045, 0.03, 0.012], 'Clay', sides=6)
        ellipsoid(scene, f'Knuckle{side}', G(side * 0.091, FIRE_SPIT_HEIGHT + 0.051, 0.245), (0.02, 0.02, 0.02), 'Bone', subdivisions=1)
    trim_height(scene, 0.5)
