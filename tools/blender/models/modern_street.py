# NOTE:
#   Building blocks of the modern town (assets/models/building/), the street group of the
#   building designer: a radio mast for a rooftop, a factory chimney, a street lamp and a
#   parked car. Same frame as models/buildings.py: +Y up, +X is the front of a block, the
#   origin is the center of its footprint on the ground, one grid cell is 0.5 units, a block
#   stays inside its box. Cleaner than the village blocks (straight edges, no wobble), but
#   just as chunky. Materials: Paint (what the player repaints), PaintDark (its trim), Metal,
#   Glass, Rubber, Stone (concrete), Door (dark openings), Ember (red lights), Light (glows,
#   its objects are named Light...), Leaf and FruitRed/FruitGold (flowers).
#   Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only building/
#   ./tools/blender/Show-Model.ps1 -Module modern_street -Call 'build_car(scene)' -Name car

#
#   Chapters:
#     SHAPES   Rings and lofted solids the primitives of buildings.py do not cover.
#     ROOF     Antenna (1 x 4 x 1 cells).
#     WORKS    Smokestack (2 x 4 x 2).
#     STREET   Street lamp (1 x 3 x 1) and car (2 x 1 x 4).
#

import math

import bmesh
import bpy

from models.buildings import beam, curve, fruit, lathe, plate, pole, trim_height
from models.helpers import G, ellipsoid, link_mesh

AXIS_X = G(1.0, 0.0, 0.0)                             # Godot axes as Blender vectors
AXIS_Y = G(0.0, 1.0, 0.0)
AXIS_Z = G(0.0, 0.0, 1.0)
CORNERS_AROUND = ((1, 1), (1, -1), (-1, -1), (-1, 1)) # signs of the four corners of a footprint, once around


# ===========================================================================
# ===============================   SHAPES   ================================
# ===========================================================================
#
#   squircle   A ring of points on a rounded rectangle (superellipse) in any plane.
#   circle     A ring of points on a circle around an axis.
#   loft       A closed solid through rings of points, flat caps on both ends.
#
#   Points are Blender vectors built with G (Godot axes). Every ring of one solid has the
#   same number of points and starts in the same direction, so the skin does not twist.
#

def squircle(center, along, up, half_along, half_up, count=16, power=4.0):
    """
    count points on a rounded rectangle around center in the plane of along and up.
    power 2 is an ellipse, higher powers push the corners out towards a rectangle.
    """
    ring = []
    for k in range(count):
        angle = k / count * math.tau
        cos, sin = math.cos(angle), math.sin(angle)
        reach_along = math.copysign(abs(cos) ** (2.0 / power), cos) * half_along
        reach_up = math.copysign(abs(sin) ** (2.0 / power), sin) * half_up
        ring.append(center + along * reach_along + up * reach_up)
    return ring


def circle(center, axis, radius, count=10):
    """count points on a circle of radius around center, standing across axis."""
    axis = axis.normalized()
    across = axis.cross(AXIS_Y if abs(axis.dot(AXIS_Y)) < 0.9 else AXIS_X).normalized()
    out = axis.cross(across)
    return [center + (across * math.cos(k / count * math.tau) + out * math.sin(k / count * math.tau)) * radius
            for k in range(count)]


def loft(scene, name, rings, material_name, is_smooth=True):
    """A closed solid through rings (lists of points of the same length), its two ends are flat hard caps."""
    bm = bmesh.new()
    loops = [[bm.verts.new(point) for point in ring] for ring in rings]
    count = len(loops[0])
    for lower, upper in zip(loops, loops[1:]):
        for k in range(count):
            following = (k + 1) % count
            bm.faces.new((lower[k], lower[following], upper[following], upper[k])).smooth = is_smooth
    # Caps share the vertices of their ring (one closed solid) and shade flat
    for loop in (loops[0], loops[-1]):
        bm.faces.new(loop).smooth = False
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if bm.calc_volume(signed=True) < 0.0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return link_mesh(scene, name, mesh, material_name)


# ===========================================================================
# ================================   ROOF   =================================
# ===========================================================================
#
#   mast_reach      Half width of the lattice mast at a height.
#   build_antenna   A radio mast for a rooftop: a square base plate, four legs leaning
#                   together, girts and zigzag braces, a dish looking out of the front, a
#                   relay drum at the back, a deck with a spire, two rod antennas, two
#                   panels and a red warning light on the tip.
#
#   Flat below (y = 0), it stands on roofs. 0.5 x 0.5 footprint, 2 high.
#

MAST_FOOT, MAST_KINK, MAST_DECK = 0.05, 0.86, 1.56    # heights where the legs start, turn steeper and meet the deck
MAST_WIDE, MAST_WAIST, MAST_NARROW = 0.2, 0.095, 0.06 # half width of the mast at those heights
MAST_LEVELS = (0.05, 0.47, 0.86, 1.22, 1.56)          # heights of the girts, a brace crosses every bay between
DISH_HEIGHT = 1.25                                    # height of the dish on the front
DISH_TILT = math.radians(25.0)                        # how far it looks up into the sky
DISH_RADIUS, DISH_DEPTH = 0.16, 0.085
DRUM_HEIGHT = 0.63                                    # height of the relay drum at the back


def mast_reach(y):
    """Half width of the mast at height y: the legs splay below the kink and run steep above it."""
    if y < MAST_KINK:
        return MAST_WIDE + (MAST_WAIST - MAST_WIDE) * (y - MAST_FOOT) / (MAST_KINK - MAST_FOOT)
    return MAST_WAIST + (MAST_NARROW - MAST_WAIST) * (y - MAST_KINK) / (MAST_DECK - MAST_KINK)


def build_antenna(scene):
    # Base plate, flat on the roof
    beam(scene, 'Plate', G(0, 0.0, 0), G(0, 0.05, 0), 0.47, 0.47, 'Paint')
    beam(scene, 'Plinth', G(0, 0.05, 0), G(0, 0.075, 0), 0.43, 0.43, 'PaintDark')
    # Four legs: splayed below the kink, steep above it
    for i, (sx, sz) in enumerate(CORNERS_AROUND):
        points = [G(sx * mast_reach(y), y, sz * mast_reach(y)) for y in (MAST_FOOT, MAST_KINK, MAST_DECK)]
        pole(scene, f'Leg{i}', points, [0.026, 0.021, 0.018], 'Metal', sides=4)
    # Girts around every level above the plate, one zigzag brace per bay and face
    for level, y in enumerate(MAST_LEVELS):
        reach = mast_reach(y)
        for i, ((sx, sz), (nx, nz)) in enumerate(zip(CORNERS_AROUND, CORNERS_AROUND[1:] + CORNERS_AROUND[:1])):
            if 0 < level:
                pole(scene, f'Girt{level}{i}', [G(sx * reach, y, sz * reach), G(nx * reach, y, nz * reach)], [0.014, 0.014], 'Metal', sides=4)
            if level < len(MAST_LEVELS) - 1:
                above = MAST_LEVELS[level + 1]
                high = mast_reach(above)
                # Every other bay leans the other way
                start, end = ((sx, sz), (nx, nz)) if 0 == level % 2 else ((nx, nz), (sx, sz))
                pole(scene, f'Brace{level}{i}', [G(start[0] * reach, y, start[1] * reach), G(end[0] * high, above, end[1] * high)],
                     [0.013, 0.012], 'Metal', sides=4)
    # Work platform at the kink: a plate standing out over the legs, a rail around it
    beam(scene, 'Platform', G(0, MAST_KINK - 0.012, 0), G(0, MAST_KINK + 0.012, 0), 0.31, 0.31, 'Metal')
    for i, ((sx, sz), (nx, nz)) in enumerate(zip(CORNERS_AROUND, CORNERS_AROUND[1:] + CORNERS_AROUND[:1])):
        pole(scene, f'RailPost{i}', [G(sx * 0.14, MAST_KINK, sz * 0.14), G(sx * 0.14, MAST_KINK + 0.11, sz * 0.14)], [0.012, 0.012], 'Metal', sides=4, is_level=True)
        pole(scene, f'Rail{i}', [G(sx * 0.14, MAST_KINK + 0.1, sz * 0.14), G(nx * 0.14, MAST_KINK + 0.1, nz * 0.14)], [0.012, 0.012], 'Metal', sides=4)
    # Dish on a mount bar across the front, looking forward and up
    reach = mast_reach(DISH_HEIGHT)
    pole(scene, 'DishMount', [G(reach, DISH_HEIGHT, -reach), G(reach, DISH_HEIGHT, reach)], [0.02, 0.02], 'Metal', sides=4)
    aim = G(math.cos(DISH_TILT), math.sin(DISH_TILT), 0.0)
    back = G(reach - 0.01, DISH_HEIGHT, 0.0)
    # Outside from the neck to the rim, then the rim and the hollow inside
    shell = [circle(back, aim, 0.04), circle(back + aim * (DISH_DEPTH * 0.55), aim, DISH_RADIUS * 0.8),
             circle(back + aim * DISH_DEPTH, aim, DISH_RADIUS), circle(back + aim * DISH_DEPTH, aim, DISH_RADIUS - 0.018),
             circle(back + aim * (DISH_DEPTH * 0.6), aim, DISH_RADIUS * 0.62), circle(back + aim * (DISH_DEPTH * 0.4), aim, 0.03)]
    loft(scene, 'Dish', shell, 'Paint')
    pole(scene, 'Feed', [back + aim * (DISH_DEPTH * 0.35), back + aim * (DISH_DEPTH + 0.035)], [0.012, 0.01], 'Metal', sides=4)
    ellipsoid(scene, 'FeedHorn', back + aim * (DISH_DEPTH + 0.035), (0.022, 0.022, 0.022), 'PaintDark', subdivisions=1)
    # Relay drum on a mount bar across the back
    reach = mast_reach(DRUM_HEIGHT)
    pole(scene, 'DrumMount', [G(-reach, DRUM_HEIGHT, -reach), G(-reach, DRUM_HEIGHT, reach)], [0.02, 0.02], 'Metal', sides=4)
    pole(scene, 'Drum', [G(-reach + 0.015, DRUM_HEIGHT, 0), G(-reach - 0.085, DRUM_HEIGHT, 0)], [0.07, 0.08], 'PaintDark', sides=10)
    pole(scene, 'DrumFace', [G(-reach - 0.085, DRUM_HEIGHT, 0), G(-reach - 0.105, DRUM_HEIGHT, 0)], [0.065, 0.04], 'Paint', sides=10)
    # Deck on the legs, a spire with two panels, two rod antennas of different length
    beam(scene, 'Deck', G(0, MAST_DECK - 0.01, 0), G(0, MAST_DECK + 0.03, 0), 0.19, 0.19, 'Metal')
    pole(scene, 'Spire', [G(0, MAST_DECK + 0.03, 0), G(0, 1.93, 0)], [0.024, 0.018], 'Metal', sides=6, is_level=True)
    for side in (-1, 1):
        beam(scene, f'Panel{side}', G(side * 0.04, 1.66, 0), G(side * 0.04, 1.85, 0), 0.075, 0.04, 'Paint')
    pole(scene, 'RodLong', [G(0.065, MAST_DECK + 0.03, -0.065), G(0.065, 1.9, -0.065)], [0.014, 0.01], 'Metal', sides=4, is_level=True)
    pole(scene, 'RodShort', [G(-0.065, MAST_DECK + 0.03, 0.065), G(-0.065, 1.78, 0.065)], [0.013, 0.009], 'Metal', sides=4, is_level=True)
    # Warning light on the tip
    lathe(scene, 'Socket', [(0.03, 1.9), (0.036, 1.915), (0.03, 1.93)], 'PaintDark', segments=6)
    ellipsoid(scene, 'Beacon', G(0, 1.958, 0), (0.04, 0.04, 0.042), 'Ember', subdivisions=2)
    trim_height(scene, 2.0)


# ===========================================================================
# ================================   WORKS   ================================
# ===========================================================================
#
#   stack_radius       Radius of the brick shaft at a height.
#   build_smokestack   A factory chimney: a square brick base on a plinth with a soot door,
#                      a round tapering shaft of brick courses with dark joints, two metal
#                      bands, a ladder up the front, a flared rim around a dark opening.
#
#   1 x 1 footprint, 2 high. No smoke.
#

STACK_FOOT, STACK_NECK = 0.44, 1.76                   # heights where the round shaft starts and where the rim takes over
STACK_WIDE, STACK_NARROW = 0.27, 0.165                 # its radius at those heights
STACK_BASE = 0.39                                     # half width of the square brick base
STACK_COURSES = 8                                     # bands of brick between foot and neck
STACK_JOINT = 0.014                                   # half height and depth of the dark joint between two bands
STACK_SEGMENTS = 14
STACK_BANDS = (0.94, 1.43)                            # heights of the two metal bands, each on a joint
LADDER_GAP = 0.03                                     # how far the ladder stands off the bricks


def stack_radius(y):
    """Radius of the shaft at height y: it tapers evenly from foot to neck."""
    share = (y - STACK_FOOT) / (STACK_NECK - STACK_FOOT)
    return STACK_WIDE + (STACK_NARROW - STACK_WIDE) * share


def build_smokestack(scene):
    # Base: a plinth, three thick courses of brick over a dark core, a cornice the shaft stands on
    beam(scene, 'Plinth', G(0, 0.0, 0), G(0, 0.09, 0), 0.9, 0.9, 'PaintDark')
    beam(scene, 'BaseCore', G(0, 0.09, 0), G(0, 0.36, 0), 2.0 * STACK_BASE - 0.04, 2.0 * STACK_BASE - 0.04, 'PaintDark')
    for i, (low, high) in enumerate(((0.09, 0.172), (0.188, 0.27), (0.286, 0.36))):
        beam(scene, f'BaseCourse{i}', G(0, low, 0), G(0, high, 0), 2.0 * STACK_BASE, 2.0 * STACK_BASE, 'Paint')
    beam(scene, 'Cornice', G(0, 0.36, 0), G(0, 0.4, 0), 0.86, 0.86, 'PaintDark')
    beam(scene, 'Shoulder', G(0, 0.4, 0), G(0, STACK_FOOT, 0), 0.7, 0.7, 'Paint')
    # Soot door on the right flank (+Z): a dark hatch in a metal frame
    beam(scene, 'SootFrame', G(0, 0.11, STACK_BASE), G(0, 0.31, STACK_BASE), 0.03, 0.24, 'Metal')
    beam(scene, 'SootDoor', G(0, 0.125, STACK_BASE + 0.005), G(0, 0.295, STACK_BASE + 0.005), 0.03, 0.18, 'Door')
    # Shaft: a dark core, bands of brick standing off it
    lathe(scene, 'Joints', [(stack_radius(STACK_FOOT) - STACK_JOINT, STACK_FOOT), (stack_radius(STACK_NECK) - STACK_JOINT, STACK_NECK)],
          'PaintDark', segments=STACK_SEGMENTS)
    course = (STACK_NECK - STACK_FOOT) / STACK_COURSES
    for i in range(STACK_COURSES):
        low, high = STACK_FOOT + i * course + STACK_JOINT, STACK_FOOT + (i + 1) * course - STACK_JOINT
        under, lower = (stack_radius(low) - STACK_JOINT, low), (stack_radius(low), low)
        upper, over = (stack_radius(high), high), (stack_radius(high) - STACK_JOINT, high)
        lathe(scene, f'Course{i}', [under, lower, lower, upper, upper, over], 'Paint', segments=STACK_SEGMENTS)
    # Two metal bands clamped around the bricks
    for i, y in enumerate(STACK_BANDS):
        radius = stack_radius(y)
        lathe(scene, f'Band{i}', [(radius - 0.005, y - 0.045), (radius + 0.035, y - 0.035), (radius + 0.03, y + 0.035), (radius - 0.01, y + 0.045)],
              'Metal', segments=STACK_SEGMENTS)
    # Ladder up the front: two rails from the base to the rim, a rung every eighth of a unit
    ladder_low, ladder_high = STACK_FOOT - 0.01, STACK_NECK + 0.05
    for side in (-1, 1):
        z = side * 0.065
        pole(scene, f'Rail{side}', [G(stack_radius(ladder_low) + LADDER_GAP, ladder_low, z), G(stack_radius(ladder_high) + LADDER_GAP, ladder_high, z)],
             [0.014, 0.012], 'Metal', sides=4)
    rung_count = 10
    for i in range(rung_count):
        y = ladder_low + 0.08 + i * (ladder_high - ladder_low - 0.12) / (rung_count - 1)
        x = stack_radius(y) + LADDER_GAP
        pole(scene, f'Rung{i}', [G(x, y, -0.075), G(x, y, 0.075)], [0.012, 0.012], 'Metal', sides=4)
    # Rim: flaring out over a corbel ring, a flat lip, the dark opening sunk into it
    neck = stack_radius(STACK_NECK)
    collar = (neck + 0.045, 1.82)
    lathe(scene, 'Corbel', [(neck - 0.01, STACK_NECK - 0.02), collar, collar, (neck + 0.045, 1.86), (neck + 0.045, 1.86), (neck, 1.86)],
          'PaintDark', segments=STACK_SEGMENTS)
    lip, mouth = (neck + 0.08, 2.0), (neck - 0.015, 2.0)
    lathe(scene, 'Rim', [(neck, 1.84), (neck + 0.065, 1.865), (neck + 0.08, 1.89), lip, lip, mouth, mouth, (neck - 0.015, 1.93)],
          'Paint', segments=STACK_SEGMENTS)
    lathe(scene, 'Opening', [(neck - 0.015, 1.93), (0.0, 1.93)], 'Door', segments=STACK_SEGMENTS)
    trim_height(scene, 2.0)


# ===========================================================================
# ===============================   STREET   ================================
# ===========================================================================
#
#   build_street_lamp   A street lamp: a concrete foot, a painted sleeve, a slim metal pole
#                       bending into an arm towards +X, a lamp head with a glowing underside,
#                       a flower basket hanging from a bracket.
#   wheel               A fat tyre with a hubcap and a mudguard arching over it.
#   build_car           A chunky little car parked along Z, its nose towards +Z: a rounded
#                       body with bumpers and sills, a glass cabin with pillars under a
#                       rounded roof, four big wheels, two headlights, two tail lights.
#
#   The lamp stands off center (x = LAMP_POLE), so its arm stays inside the footprint.
#

LAMP_POLE = -0.1                                      # x of the pole, the arm reaches forward from there
LAMP_HEAD = 0.135                                     # x of the middle of the lamp head
LAMP_BASKET = 0.09                                    # x of the flower basket


def build_street_lamp(scene):
    # Concrete foot with a slanted shoulder, closed on top
    lathe(scene, 'Foot', [(0.125, 0.0), (0.125, 0.07), (0.125, 0.07), (0.075, 0.13), (0.075, 0.13), (0.0, 0.13)], 'Stone',
          segments=8, center=(LAMP_POLE, 0.0), is_smooth=False, has_bottom_cap=True)
    # Painted sleeve around the lower pole, a ring where it ends
    lathe(scene, 'Sleeve', [(0.055, 0.13), (0.05, 0.42), (0.05, 0.42), (0.03, 0.46)], 'Paint', segments=8, center=(LAMP_POLE, 0.0))
    lathe(scene, 'SleeveRing', [(0.045, 0.37), (0.066, 0.385), (0.066, 0.415), (0.045, 0.43)], 'PaintDark', segments=8, center=(LAMP_POLE, 0.0))
    # Pole: straight up, then bending forward into the arm
    bend = curve([G(LAMP_POLE, 1.05, 0), G(LAMP_POLE + 0.02, 1.28, 0), G(LAMP_POLE + 0.1, 1.43, 0), G(LAMP_HEAD - 0.03, 1.462, 0)], 8)
    points = [G(LAMP_POLE, 0.13, 0), G(LAMP_POLE, 0.7, 0)] + bend
    pole(scene, 'Pole', points, [0.036, 0.033] + [0.03] * 4 + [0.026] * 4, 'Metal', sides=6)
    # Lamp head: a bell shade at the end of the arm, the glowing lens bulging out below it
    brim = (0.11, 1.385)
    lathe(scene, 'Shade', [(0.0, 1.49), (0.05, 1.48), (0.095, 1.43), brim, brim, (0.09, 1.385)], 'Metal', segments=10, center=(LAMP_HEAD, 0.0))
    lathe(scene, 'LightLens', [(0.09, 1.385), (0.07, 1.345), (0.0, 1.325)], 'Light', segments=10, center=(LAMP_HEAD, 0.0))
    # Bracket with a curl, the basket hangs from its end
    pole(scene, 'Bracket', [G(LAMP_POLE, 0.98, 0), G(LAMP_BASKET - 0.04, 0.98, 0), G(LAMP_BASKET, 0.965, 0), G(LAMP_BASKET + 0.02, 0.93, 0)],
         [0.016, 0.015, 0.014, 0.012], 'Metal', sides=4)
    pole(scene, 'BracketStay', [G(LAMP_POLE, 0.86, 0), G(LAMP_BASKET - 0.06, 0.975, 0)], [0.012, 0.012], 'Metal', sides=4)
    for i, side in enumerate((-1, 1)):
        pole(scene, f'Chain{i}', [G(LAMP_BASKET, 0.965, 0), G(LAMP_BASKET, 0.8, side * 0.085)], [0.011, 0.011], 'Metal', sides=4)
    # Flower basket: a painted bowl, leaves spilling over, three blossoms
    rim, inner = (0.105, 0.8), (0.085, 0.8)
    lathe(scene, 'Basket', [(0.0, 0.67), (0.05, 0.68), (0.095, 0.73), rim, rim, inner, inner, (0.0, 0.79)], 'Paint',
          segments=10, center=(LAMP_BASKET, 0.0))
    lathe(scene, 'BasketRim', [(0.1, 0.775), (0.115, 0.785), (0.115, 0.805), (0.1, 0.812)], 'PaintDark', segments=10, center=(LAMP_BASKET, 0.0))
    ellipsoid(scene, 'Leaves', G(LAMP_BASKET, 0.815, 0), (0.095, 0.095, 0.05), 'Leaf', subdivisions=2)
    for i, (x, z, material_name) in enumerate(((0.05, 0.03, 'FruitRed'), (-0.035, -0.045, 'FruitGold'), (-0.02, 0.055, 'FruitRed'))):
        fruit(scene, f'Blossom{i}', (LAMP_BASKET + x, 0.86, z), 0.032, material_name)
    trim_height(scene, 1.5)


CAR_WIDTH = 0.44                                      # half width of the body, the wheels stand out of it
CAR_SILL, CAR_BELT = 0.07, 0.29                       # heights of the underside and of the edge below the windows
CAR_ROOF = 0.445                                      # height where the glass ends under the roof
# Body from tail to nose: (z, half width, underside, top). The bonnet is short and drops to the nose.
CAR_BODY = ((-0.97, 0.33, 0.13, 0.26), (-0.93, 0.41, 0.09, 0.285), (-0.6, CAR_WIDTH, CAR_SILL, CAR_BELT),
            (0.42, CAR_WIDTH, CAR_SILL, CAR_BELT), (0.75, 0.43, 0.08, 0.275), (0.93, 0.39, 0.1, 0.255), (0.97, 0.31, 0.13, 0.225))
# Cabin: (y, middle z, half width, half length) of the glass at the belt and under the roof
CABIN_LOW = (CAR_BELT - 0.01, -0.175, 0.375, 0.625)
CABIN_HIGH = (CAR_ROOF + 0.005, -0.25, 0.31, 0.46)
CABIN_CORNERS = (2, 6, 10, 14)                        # points of a ring of 16 that sit on the four corners
WHEEL_RADIUS = 0.15
WHEEL_FRONT, WHEEL_REAR = 0.6, -0.6                   # z of the two axles
WHEEL_INSIDE, WHEEL_OUTSIDE = 0.3, 0.47               # x of the two faces of a tyre


GUARD_INSIDE = 0.38                                   # x where a mudguard starts inside the body, clear of the glass


def wheel(scene, name, side, z):
    """A wheel on the flank side (-1 or 1) at z: tyre on the ground, hubcap, a mudguard arching over it."""
    y = WHEEL_RADIUS
    tyre = [G(side * x, y, z) for x in (WHEEL_INSIDE, WHEEL_INSIDE + 0.025, WHEEL_OUTSIDE - 0.025, WHEEL_OUTSIDE)]
    pole(scene, f'{name}Tyre', tyre, [WHEEL_RADIUS * 0.84, WHEEL_RADIUS, WHEEL_RADIUS, WHEEL_RADIUS * 0.84], 'Rubber', sides=12)
    pole(scene, f'{name}Hub', [G(side * (WHEEL_OUTSIDE - 0.02), y, z), G(side * (WHEEL_OUTSIDE + 0.006), y, z), G(side * (WHEEL_OUTSIDE + 0.016), y, z)],
         [0.085, 0.078, 0.045], 'Metal', sides=8)
    # Mudguard: half a ring over the tyre, pushed out from the body to just over the tyre
    inner, outer, steps = WHEEL_RADIUS + 0.012, WHEEL_RADIUS + 0.042, 8
    arch = [(math.cos(k / steps * math.pi), math.sin(k / steps * math.pi)) for k in range(steps + 1)]
    outline = [G(side * GUARD_INSIDE, y + sin * outer, z + cos * outer) for cos, sin in arch]
    outline += [G(side * GUARD_INSIDE, y + sin * inner, z + cos * inner) for cos, sin in reversed(arch)]
    plate(scene, f'{name}Guard', outline, G(side * (WHEEL_OUTSIDE + 0.015 - GUARD_INSIDE), 0.0, 0.0), 'PaintDark')


def build_car(scene):
    # Body: one rounded hull from tail to nose
    hull = [squircle(G(0.0, (low + high) / 2, z), AXIS_X, AXIS_Y, half_width, (high - low) / 2) for z, half_width, low, high in CAR_BODY]
    loft(scene, 'Body', hull, 'Paint')
    # Cabin: glass all around, leaning in towards the roof, the windscreen leaning most
    glass = [squircle(G(0.0, y, z), AXIS_X, AXIS_Z, half_width, half_length, power=5.0) for y, z, half_width, half_length in (CABIN_LOW, CABIN_HIGH)]
    loft(scene, 'Cabin', glass, 'Glass')
    # Pillars on the four corners and one behind each door
    for i, corner in enumerate(CABIN_CORNERS):
        beam(scene, f'Pillar{i}', glass[0][corner], glass[1][corner], 0.05, 0.05, 'Paint')
    for side in (-1, 1):
        beam(scene, f'DoorPillar{side}', G(side * CABIN_LOW[2], CABIN_LOW[0], -0.24), G(side * CABIN_HIGH[2], CABIN_HIGH[0], -0.27), 0.07, 0.035, 'Paint')
    # Roof: a rounded slab standing over the glass
    y, z, half_width, half_length = CABIN_HIGH
    roof = [squircle(G(0.0, height, z), AXIS_X, AXIS_Z, half_width + grow, half_length + grow, power=5.0)
            for height, grow in ((CAR_ROOF, 0.005), (CAR_ROOF + 0.012, 0.02), (0.486, 0.015), (0.5, -0.04))]
    loft(scene, 'Roof', roof, 'Paint')
    # Bumpers at both ends, a sill along each flank between the wheels
    beam(scene, 'BumperFront', G(-0.39, 0.115, 0.95), G(0.39, 0.115, 0.95), 0.07, 0.055, 'PaintDark')
    beam(scene, 'BumperRear', G(-0.4, 0.125, -0.95), G(0.4, 0.125, -0.95), 0.07, 0.055, 'PaintDark')
    for side in (-1, 1):
        beam(scene, f'Sill{side}', G(side * (CAR_WIDTH - 0.005), 0.1, WHEEL_REAR + 0.16), G(side * (CAR_WIDTH - 0.005), 0.1, WHEEL_FRONT - 0.16), 0.04, 0.06, 'PaintDark')
    # Wheels
    for side in (-1, 1):
        wheel(scene, f'WheelFront{side}', side, WHEEL_FRONT)
        wheel(scene, f'WheelRear{side}', side, WHEEL_REAR)
    # Face: two round headlights standing out of the bonnet, a grille between them
    for side in (-1, 1):
        pole(scene, f'LightHead{side}', [G(side * 0.25, 0.19, 0.92), G(side * 0.25, 0.19, 0.972), G(side * 0.25, 0.19, 0.986)],
             [0.06, 0.06, 0.04], 'Light', sides=10)
        pole(scene, f'LampRing{side}', [G(side * 0.25, 0.19, 0.9), G(side * 0.25, 0.19, 0.962)], [0.074, 0.074], 'Metal', sides=10)
    beam(scene, 'Grille', G(-0.11, 0.185, 0.955), G(0.11, 0.185, 0.955), 0.04, 0.045, 'Metal')
    # Tail lights
    for side in (-1, 1):
        beam(scene, f'Tail{side}', G(side * 0.17, 0.215, -0.955), G(side * 0.31, 0.215, -0.955), 0.045, 0.06, 'Ember')
    # Wing mirrors at the foot of the windscreen
    for side in (-1, 1):
        ellipsoid(scene, f'Mirror{side}', G(side * 0.44, 0.31, 0.38), (0.05, 0.03, 0.035), 'Paint', subdivisions=1)
    trim_height(scene, 0.5)
