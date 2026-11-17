# NOTE:
#   The scenery a city gets from its techs (assets/models/civ/, placed by Civ/CityScenery.cs):
#   a modern field with neat rows of grain, a wire fence with a gate, wrapped bales and a
#   tractor (Farming) and a concrete harbor pier on piles with bollards, tyre fenders,
#   containers, a crane and a lamp post (Sailing). +X is the front of a model, +Y up, the
#   origin is the middle of its footprint on the ground (the field, its gate looks along +X)
#   or where the pier meets the shore (the pier runs out along +X, the sea level is y = 0).
#   No rig, no animation. Materials: Paint (body of the tractor, cab of the crane, a
#   container: the game tints them), PaintDark (chassis, a container), Earth, FruitGold
#   (grain, rims, the yellow edge of the pier and the jib of the crane), Bone (wrapped
#   bales, railing), Wood, Log (mooring piles), Metal, Glass, Rubber, Stone (concrete), Leaf (a
#   green container), Rope,
#   Light (head lights, the lamp: objects named Light...). Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only civ/

#
#   Chapters:
#     SHAPES   Boxes, wheels and rings the field and the pier are built from.
#     FIELD    Soil, rows of grain, a wire fence with a gate, wrapped bales and a tractor.
#     PIER     Concrete deck on piles, bollards, tyre fenders, containers, a crane, a lamp post.
#

import math
import random

from models.buildings import beam, lathe, plate, pole, rock
from models.helpers import G, join_by_material

# ===========================================================================
# ================================   SHAPES   ===============================
# ===========================================================================
#
#   box     A box between two corners, its edges along the axes.
#   wheel   A cylinder with its axis across the model (along Z): wheels, hubs, bales.
#   ring    A closed ring whose hole looks along Z: the tyres along the pier.
#

def box(scene, name, low, high, material_name):
    """A box from the corner low to the corner high (Godot x, y, z)."""
    y, z = (low[1] + high[1]) / 2, (low[2] + high[2]) / 2
    return beam(scene, name, G(low[0], y, z), G(high[0], y, z), high[2] - low[2], high[1] - low[1], material_name)


def wheel(scene, name, center, radius, width, material_name, sides=12, bevel=0.0):
    """A cylinder around center (Godot x, y, z) with its axis along Z, bevel rounds its shoulders."""
    x, y, z = center
    if bevel <= 0.0:
        return pole(scene, name, [G(x, y, z - width / 2), G(x, y, z + width / 2)], [radius, radius], material_name, sides=sides)
    offsets = (-width / 2, -width / 2 + bevel, width / 2 - bevel, width / 2)
    radii = [radius - bevel, radius, radius, radius - bevel]
    return pole(scene, name, [G(x, y, z + offset) for offset in offsets], radii, material_name, sides=sides)


def ring(scene, name, center, radius, thickness, material_name, count=8, sides=5):
    """A ring around center (Godot x, y, z) whose hole looks along Z."""
    x, y, z = center
    points = [G(x + math.cos(a) * radius, y + math.sin(a) * radius, z) for a in (i / count * math.tau for i in range(count + 1))]
    return pole(scene, name, points, [thickness] * len(points), material_name, sides=sides)


# ===========================================================================
# ================================   FIELD   ================================
# ===========================================================================
#
#   crop_row        One neat row of grain on its ridge, a bumpy golden strip.
#   build_tractor   A small tractor: big rear wheels, hood, glass cab, exhaust, head lights.
#   build_field     A plot of 2.4 x 1.6: soil, seven ridges with grain, two of them already
#                   harvested behind the tractor that stands in them, three wrapped bales, a
#                   fence of posts and two wires around it with an open gate at the front.
#

FIELD_X, FIELD_Z = 1.2, 0.8
FENCE = 0.08                 # the fence stands this far outside the soil
ROWS = 7
ROW_STEP = 0.22
RIDGE_Y = 0.05               # top of the soil the wheels and bales stand on
TRACTOR_X, TRACTOR_Z = -0.05, 0.11
TRACTOR_SIZE = 1.25
CUT_ROWS = (3, 4)            # rows the tractor already harvested behind it
CUT_UNTIL = 0.46             # x where the grain of those rows still stands


def crop_row(scene, name, start, end, z):
    points, radii = [], []
    count = max(2, round((end - start) / 0.1))
    for k in range(count + 1):
        points.append(G(start + (end - start) * k / count, RIDGE_Y + 0.03, z))
        radii.append(0.07 if 1 == k % 2 else 0.056)
    radii[0] = radii[-1] = 0.04
    pole(scene, name, points, radii, 'FruitGold', sides=5)


def build_tractor(scene, x, z, size):
    """A tractor that stands on the soil around (x, z) and looks along +X, size scales it (1 = 0.63 long)."""
    def at(dx, dy, dz):
        return (x + dx * size, RIDGE_Y + dy * size, z + dz * size)

    axle = 0.16              # height of the rear axle = radius of the rear wheels
    # Wheels: two big ones under the cab, two small ones under the hood, yellow rims
    for side in (-1, 1):
        wheel(scene, f'RearWheel{side}', at(-0.14, axle, side * 0.17), 0.16 * size, 0.1 * size, 'Rubber', bevel=0.025 * size)
        wheel(scene, f'RearRim{side}', at(-0.14, axle, side * 0.17), 0.08 * size, 0.115 * size, 'FruitGold', sides=8)
        wheel(scene, f'FrontWheel{side}', at(0.22, 0.09, side * 0.14), 0.09 * size, 0.07 * size, 'Rubber', sides=10, bevel=0.02 * size)
        wheel(scene, f'FrontRim{side}', at(0.22, 0.09, side * 0.14), 0.045 * size, 0.082 * size, 'FruitGold', sides=8)
        # Mudguard over the rear wheel
        box(scene, f'Mudguard{side}', at(-0.27, axle + 0.15, side * 0.17 - 0.06), at(-0.01, axle + 0.19, side * 0.17 + 0.06), 'Paint')
    # Chassis and hood with its grill and head lights
    box(scene, 'Chassis', at(-0.24, 0.1, -0.07), at(0.3, axle + 0.04, 0.07), 'PaintDark')
    box(scene, 'Hood', at(-0.02, axle + 0.04, -0.085), at(0.31, axle + 0.2, 0.085), 'Paint')
    box(scene, 'Grill', at(0.31, axle + 0.06, -0.06), at(0.325, axle + 0.17, 0.06), 'Metal')
    for side in (-1, 1):
        wheel(scene, f'LightTractor{side}', at(0.322, axle + 0.15, side * 0.062), 0.022 * size, 0.03 * size, 'Light', sides=6)
    low, high = at(0.16, axle + 0.2, 0.05), at(0.16, axle + 0.42, 0.05)
    pole(scene, 'Exhaust', [G(*low), G(*high)], [0.016 * size, 0.024 * size], 'Metal', sides=6, is_level=True)
    # Cab: a seat box, glass all around between four posts, a roof
    box(scene, 'CabFoot', at(-0.26, axle + 0.04, -0.11), at(-0.02, axle + 0.19, 0.11), 'Paint')
    box(scene, 'CabGlass', at(-0.245, axle + 0.19, -0.1), at(-0.03, axle + 0.43, 0.1), 'Glass')
    for i, (dx, dz) in enumerate(((-0.25, -0.105), (-0.25, 0.105), (-0.025, -0.105), (-0.025, 0.105))):
        box(scene, f'CabPost{i}', at(dx - 0.014, axle + 0.19, dz - 0.014), at(dx + 0.014, axle + 0.43, dz + 0.014), 'PaintDark')
    box(scene, 'CabRoof', at(-0.285, axle + 0.43, -0.13), at(0.01, axle + 0.47, 0.13), 'Paint')
    # Hitch with a draw bar behind the cab
    box(scene, 'Hitch', at(-0.34, 0.1, -0.02), at(-0.24, 0.14, 0.02), 'Metal')

def build_field(scene):
    plate(scene, 'Soil', [G(-FIELD_X, 0.0, -FIELD_Z), G(FIELD_X, 0.0, -FIELD_Z), G(FIELD_X, 0.0, FIELD_Z), G(-FIELD_X, 0.0, FIELD_Z)], G(0.0, 0.03, 0.0), 'Earth')
    for i in range(ROWS):
        z = (i - (ROWS - 1) / 2) * ROW_STEP
        beam(scene, f'Ridge{i}', G(-FIELD_X + 0.06, 0.025, z), G(FIELD_X - 0.06, 0.025, z), 0.15, 0.05, 'Earth')
        crop_row(scene, f'Grain{i}', CUT_UNTIL if i in CUT_ROWS else -FIELD_X + 0.1, FIELD_X - 0.1, z)
    build_tractor(scene, TRACTOR_X, TRACTOR_Z, TRACTOR_SIZE)
    # Bales wrapped in white where the grain is already cut
    for i, (x, z) in enumerate(((-1.02, -0.02), (-1.0, 0.25), (-0.68, 0.11))):
        wheel(scene, f'Bale{i}', (x, RIDGE_Y + 0.135, z), 0.14, 0.22, 'Bone', sides=10, bevel=0.03)
    # Fence: square posts, two wires, a gap at the front for the gate
    edge_x, edge_z = FIELD_X + FENCE, FIELD_Z + FENCE
    corners = [(-edge_x, -edge_z), (edge_x, -edge_z), (edge_x, edge_z), (-edge_x, edge_z)]
    gate = (0.34, 0.66)      # share of the front fence the gate takes
    for side, ((x0, z0), (x1, z1)) in enumerate(zip(corners, corners[1:] + corners[:1])):
        count = 7 if 0 == side % 2 else 5
        for i in range(count):
            s = i / count
            if 1 == side and gate[0] < s < gate[1]:
                continue
            x, z = x0 + (x1 - x0) * s, z0 + (z1 - z0) * s
            box(scene, f'Post{side}{i}', (x - 0.022, 0.0, z - 0.022), (x + 0.022, 0.27, z + 0.022), 'Wood')
        spans = ((0.0, gate[0]), (gate[1], 1.0)) if 1 == side else ((0.0, 1.0),)
        for height in (0.12, 0.22):
            for s0, s1 in spans:
                beam(scene, f'Wire{side}{height}{s0}', G(x0 + (x1 - x0) * s0, height, z0 + (z1 - z0) * s0), G(x0 + (x1 - x0) * s1, height, z0 + (z1 - z0) * s1), 0.018, 0.018, 'Metal')
    # Gate: two posts and a leaf of steel tube that stands open towards the city
    span = 2.0 * edge_z
    hinge, latch = -edge_z + span * gate[0], -edge_z + span * gate[1]
    for i, z in enumerate((hinge, latch)):
        box(scene, f'GatePost{i}', (edge_x - 0.028, 0.0, z - 0.028), (edge_x + 0.028, 0.32, z + 0.028), 'Wood')
    swing = math.radians(20.0)      # only ajar: the leaf stays inside the footprint the game clears
    reach = (latch - hinge) * 0.94
    tip = (edge_x + math.sin(swing) * reach, hinge + math.cos(swing) * reach)
    for i, height in enumerate((0.07, 0.17, 0.27)):
        beam(scene, f'GateBar{i}', G(edge_x, height, hinge), G(tip[0], height, tip[1]), 0.022, 0.022, 'Metal')
    for i, s in enumerate((0.03, 0.5, 0.98)):
        x, z = edge_x + (tip[0] - edge_x) * s, hinge + (tip[1] - hinge) * s
        beam(scene, f'GateStile{i}', G(x, 0.06, z), G(x, 0.28, z), 0.022, 0.022, 'Metal')
    join_by_material([obj for obj in scene.collection.objects if obj.type == 'MESH'])


# ===========================================================================
# =================================   PIER   ================================
# ===========================================================================
#
#   container     A shipping container with ribs on its long sides and a door at one end.
#   build_crane   A small harbor crane: four legs, slewing cab with windows, yellow lattice
#                 jib with a stay, counterweight, a crate on the hook.
#   build_pier    A concrete deck of 3 x 0.8 on five pairs of round piles with cross heads,
#                 an abutment and stones at the shore, a yellow edge, four bollards, tyres on
#                 chains along both sides, a white railing at the shore, three containers, the
#                 crane, two mooring piles and a lamp post at the end.
#

PIER_LENGTH = 3.0
PIER_HALF = 0.4
DECK_Y = 0.32
DECK_THICK = 0.12
CONTAINER = (0.52, 0.2, 0.2)     # length, height, width of a container


def container(scene, name, x, y, z, material_name):
    """A container whose low corner towards the shore and the middle of the pier is (x, y, z)."""
    length, height, width = CONTAINER
    box(scene, name, (x, y, z), (x + length, y + height, z + width), material_name)
    # Ribs of the corrugated walls on both long sides, a little proud
    for i in range(6):
        rib_x = x + 0.05 + i * (length - 0.1 - 0.03) / 5
        box(scene, f'{name}Rib{i}', (rib_x, y + 0.02, z - 0.012), (rib_x + 0.03, y + height - 0.02, z + width + 0.012), material_name)
    # Door at the far end: two leaves with their locking bars
    for i, side in enumerate((-1, 1)):
        middle = z + width / 2 + side * width / 4
        box(scene, f'{name}Door{i}', (x + length, y + 0.015, middle - width / 4 + 0.012), (x + length + 0.012, y + height - 0.015, middle + width / 4 - 0.012), material_name)
        box(scene, f'{name}Bar{i}', (x + length + 0.012, y + 0.02, middle - 0.012), (x + length + 0.026, y + height - 0.02, middle + 0.012), 'Metal')


def build_crane(scene, x, z):
    # Pedestal: four splayed legs under a slewing ring
    ring_y = DECK_Y + 0.4
    for i, (dx, dz) in enumerate(((-1, -1), (-1, 1), (1, -1), (1, 1))):
        beam(scene, f'CraneLeg{i}', G(x + dx * 0.15, DECK_Y, z + dz * 0.15), G(x + dx * 0.07, ring_y, z + dz * 0.07), 0.06, 0.06, 'Metal')
        box(scene, f'CraneShoe{i}', (x + dx * 0.15 - 0.04, DECK_Y, z + dz * 0.15 - 0.04), (x + dx * 0.15 + 0.04, DECK_Y + 0.025, z + dz * 0.15 + 0.04), 'Metal')
    lathe(scene, 'CraneTurn', [(0.12, ring_y - 0.03), (0.12, ring_y + 0.03), (0.0, ring_y + 0.03)], 'Rubber', segments=10, center=(x, z), has_bottom_cap=True)
    # Cab that slews on it, its window looks out along the pier
    low, high = ring_y + 0.03, ring_y + 0.33
    box(scene, 'CraneCab', (x - 0.17, low, z - 0.13), (x + 0.14, high, z + 0.13), 'Paint')
    box(scene, 'CraneRoof', (x - 0.19, high, z - 0.15), (x + 0.16, high + 0.035, z + 0.15), 'PaintDark')
    box(scene, 'CraneWindow', (x + 0.14, low + 0.1, z - 0.1), (x + 0.152, high - 0.04, z + 0.1), 'Glass')
    for side in (-1, 1):
        box(scene, f'CraneSide{side}', (x - 0.02, low + 0.12, z + side * 0.13 - 0.006), (x + 0.11, high - 0.04, z + side * 0.13 + 0.006), 'Glass')
    box(scene, 'CraneWeight', (x - 0.3, low + 0.02, z - 0.11), (x - 0.17, low + 0.2, z + 0.11), 'Stone')
    # Jib rising out over the end of the pier: two chords that meet at the tip, struts between them
    foot, tip = G(x + 0.12, low + 0.04, z), G(x + 0.64, DECK_Y + 1.3, z)
    head = G(x - 0.02, high + 0.035, z)
    beam(scene, 'JibLow', foot, tip, 0.05, 0.035, 'FruitGold')
    beam(scene, 'JibHigh', head, tip, 0.05, 0.035, 'FruitGold')
    for i in range(4):
        lower = foot + (tip - foot) * (0.12 + 0.2 * i)
        upper = head + (tip - head) * (0.02 + 0.2 * i)
        beam(scene, f'JibStrut{i}', lower, upper, 0.035, 0.022, 'FruitGold')
    # A stay from a short mast on the roof, tied back to the counterweight
    mast = G(x - 0.12, high + 0.3, z)
    beam(scene, 'JibMast', G(x - 0.12, high + 0.035, z), mast, 0.04, 0.04, 'FruitGold')
    beam(scene, 'JibStay', mast, head + (tip - head) * 0.75, 0.02, 0.02, 'Metal')
    beam(scene, 'JibBack', mast, G(x - 0.27, low + 0.2, z), 0.02, 0.02, 'Metal')
    # Cable, hook block and a crate on the hook
    hang = DECK_Y + 0.66
    beam(scene, 'Cable', tip, G(tip.x, hang + 0.1, z), 0.018, 0.018, 'Metal')
    box(scene, 'HookBlock', (tip.x - 0.035, hang + 0.04, z - 0.035), (tip.x + 0.035, hang + 0.1, z + 0.035), 'FruitGold')
    box(scene, 'HookCrate', (tip.x - 0.085, hang - 0.13, z - 0.085), (tip.x + 0.085, hang + 0.04, z + 0.085), 'Wood')
    for i, (dx, dz) in enumerate(((-1, 0), (1, 0), (0, -1), (0, 1))):
        strap = (0.012, 0.09) if 0 == dz else (0.09, 0.012)
        box(scene, f'HookStrap{i}', (tip.x + dx * 0.085 - strap[0], hang - 0.13, z + dz * 0.085 - strap[1]), (tip.x + dx * 0.085 + strap[0], hang + 0.045, z + dz * 0.085 + strap[1]), 'Rope')


def build_pier(scene):
    rng = random.Random(402)
    # Deck: one concrete slab, a yellow edge along both sides and the end
    box(scene, 'Deck', (0.0, DECK_Y - DECK_THICK, -PIER_HALF), (PIER_LENGTH, DECK_Y, PIER_HALF), 'Stone')
    for side in (-1, 1):
        box(scene, f'Edge{side}', (0.0, DECK_Y, side * PIER_HALF - 0.035), (PIER_LENGTH, DECK_Y + 0.03, side * PIER_HALF + 0.035), 'FruitGold')
    box(scene, 'EdgeEnd', (PIER_LENGTH - 0.07, DECK_Y, -PIER_HALF + 0.035), (PIER_LENGTH, DECK_Y + 0.03, PIER_HALF - 0.035), 'FruitGold')
    # Piles deep into the sea bed, a cross head on every pair
    for i in range(5):
        x = 0.4 + i * 0.6
        for side in (-1, 1):
            pole(scene, f'Pile{side}{i}', [G(x, -0.9, side * 0.28), G(x, DECK_Y - DECK_THICK - 0.06, side * 0.28)], [0.075, 0.075], 'Stone', sides=8, is_level=True)
        box(scene, f'Head{i}', (x - 0.09, DECK_Y - DECK_THICK - 0.07, -PIER_HALF + 0.03), (x + 0.09, DECK_Y - DECK_THICK, PIER_HALF - 0.03), 'Stone')
    # Abutment where the pier meets the shore, a few stones around it
    box(scene, 'Abutment', (-0.22, -0.5, -PIER_HALF - 0.06), (0.1, DECK_Y - 0.001, PIER_HALF + 0.06), 'Stone')
    for i in range(4):
        s = rng.uniform(0.1, 0.16)
        side = -1 if i < 2 else 1
        rock(scene, f'Stone{i}', (rng.uniform(-0.2, 0.1), 0.02, side * rng.uniform(0.52, 0.66)), (s, s * 0.6, s * 0.9), 'Stone', rng)
    # Bollards: iron mushrooms near the edge
    for i, x in enumerate((1.55, 2.8)):
        for side in (-1, 1):
            lathe(scene, f'Bollard{i}{side}', [(0.05, DECK_Y), (0.04, DECK_Y + 0.09), (0.07, DECK_Y + 0.11), (0.07, DECK_Y + 0.15), (0.0, DECK_Y + 0.16)], 'Metal', segments=8,
                  center=(x, side * (PIER_HALF - 0.12)))
    # Tyres on chains along both sides
    for i in range(4):
        x = 0.7 + i * 0.6
        for side in (-1, 1):
            z = side * (PIER_HALF + 0.04)
            ring(scene, f'Tyre{i}{side}', (x, DECK_Y - 0.14, z), 0.085, 0.04, 'Rubber')
            beam(scene, f'Chain{i}{side}', G(x, DECK_Y - 0.08, z), G(x, DECK_Y + 0.03, z), 0.02, 0.02, 'Metal')
    # Containers near the shore: two on the deck, one on top
    container(scene, 'BoxLow', 0.22, DECK_Y, 0.08, 'Paint')
    container(scene, 'BoxNext', 0.8, DECK_Y, 0.08, 'Leaf')
    container(scene, 'BoxTop', 0.42, DECK_Y + CONTAINER[1], 0.08, 'PaintDark')
    build_crane(scene, 2.05, 0.08)
    # White railing along the walkway beside the containers
    for i in range(4):
        x = 0.16 + i * 0.36
        box(scene, f'RailPost{i}', (x - 0.018, DECK_Y + 0.03, -PIER_HALF + 0.017), (x + 0.018, DECK_Y + 0.25, -PIER_HALF + 0.053), 'Bone')
    for i, height in enumerate((0.15, 0.25)):
        box(scene, f'Rail{i}', (0.12, DECK_Y + height - 0.016, -PIER_HALF + 0.019), (1.28, DECK_Y + height + 0.016, -PIER_HALF + 0.051), 'Bone')
    # Mooring piles beside the end of the pier, dark timber with a yellow cap
    for side in (-1, 1):
        pole(scene, f'Mooring{side}', [G(PIER_LENGTH - 0.3, -0.9, side * (PIER_HALF + 0.11)), G(PIER_LENGTH - 0.3, DECK_Y + 0.2, side * (PIER_HALF + 0.11))], [0.085, 0.08], 'Log', sides=8, is_level=True)
        lathe(scene, f'MooringCap{side}', [(0.09, DECK_Y + 0.2), (0.09, DECK_Y + 0.25), (0.0, DECK_Y + 0.27)], 'FruitGold', segments=8, center=(PIER_LENGTH - 0.3, side * (PIER_HALF + 0.11)), has_bottom_cap=True)
    # Lamp post at the very end, its head reaches in over the deck
    at_x, at_z = PIER_LENGTH - 0.14, -PIER_HALF + 0.12
    pole(scene, 'LampPost', [G(at_x, DECK_Y, at_z), G(at_x, DECK_Y + 0.8, at_z), G(at_x, DECK_Y + 0.9, at_z + 0.08), G(at_x, DECK_Y + 0.92, at_z + 0.16)], [0.03, 0.024, 0.022, 0.02], 'Metal', sides=6)
    box(scene, 'LampHead', (at_x - 0.045, DECK_Y + 0.9, at_z + 0.1), (at_x + 0.045, DECK_Y + 0.94, at_z + 0.28), 'Metal')
    box(scene, 'LightLamp', (at_x - 0.035, DECK_Y + 0.875, at_z + 0.12), (at_x + 0.035, DECK_Y + 0.9, at_z + 0.26), 'Light')
    join_by_material([obj for obj in scene.collection.objects if obj.type == 'MESH'])
