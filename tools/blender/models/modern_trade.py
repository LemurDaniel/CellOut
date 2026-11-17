# NOTE:
#   Building blocks of the modern town (assets/models/building/), the trade and street
#   furniture of today: a street kiosk, a vending machine, a shipping container, a park
#   bench with a litter bin and a traffic light. Same frame as models/buildings.py: +Y up,
#   +X is the front of a block, the origin is the center of its footprint on the ground, one
#   grid cell is 0.5 units, a block stays inside its box. Clean and straight like the other
#   modern blocks (models/modern.py, models/modern_street.py), but just as chunky.
#   Materials: Paint (what the player repaints), PaintDark (its trim), Metal, Glass (opaque
#   pale blue: goods stand in front of it), Rubber, Stone (concrete), Wood, Bone (paper),
#   Door (dark openings), Light (glows, its objects are named Light...) and
#   FruitRed/FruitGold/FruitGreen (goods, cans, lamps). Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module modern_trade -Call 'build_kiosk(scene)' -Name kiosk

#
#   Chapters:
#     SHAPES   Tilted boards, discs looking along X and ribs of corrugated sheet metal.
#     TRADE    Kiosk (2 x 2 x 2 cells), vending machine (1 x 2 x 1) and container (2 x 2 x 4).
#     STREET   Bench (1 x 1 x 2) and traffic light (1 x 3 x 1).
#

import math

from models.buildings import beam, curve, fruit, lathe, plate, pole, trim_height
from models.helpers import G
from models.modern import box
from models.modern_street import loft

GOODS = ('FruitRed', 'FruitGold', 'FruitGreen')       # colors of everything for sale, in the order of the shelves


# ===========================================================================
# ================================   SHAPES   ===============================
# ===========================================================================
#
#   board   A board along Z whose cross section is a rectangle tilted in the X/Y plane.
#   disc    A short cylinder looking along +X: lamps, buttons, cans lying down.
#   rib     One rib of corrugated sheet metal: an upright trapezoid standing off a wall.
#
#   All three take Godot coordinates and build on plate and pole of models/buildings.py.
#

def board(scene, name, center, tilt, width, thickness, start, end, material_name):
    """
    A board from z = start to z = end. center (x, y) is the middle of its cross section,
    width lies along the direction tilt (radians, 0 = +X, a quarter turn = upright).
    """
    along = (math.cos(tilt) * width / 2, math.sin(tilt) * width / 2)
    across = (-math.sin(tilt) * thickness / 2, math.cos(tilt) * thickness / 2)
    corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))
    outline = [G(center[0] + a * along[0] + b * across[0], center[1] + a * along[1] + b * across[1], start) for a, b in corners]
    return plate(scene, name, outline, G(0.0, 0.0, end - start), material_name)


def disc(scene, name, x_from, x_to, y, z, radius, material_name, sides=10, dome=0.0):
    """A cylinder around the line from x_from to x_to at (y, z). dome adds a rounded front of that depth."""
    points, radii = [G(x_from, y, z), G(x_to, y, z)], [radius, radius]
    if 0.0 < dome:
        points.append(G(x_to + dome, y, z))
        radii.append(radius * 0.6)
    return pole(scene, name, points, radii, material_name, sides=sides)


def rib(scene, name, foot, out, across, width, depth, low, high, material_name):
    """
    A rib around foot (x, z) on a wall that looks along out (x, z), across (x, z) runs
    level along the wall. width at its foot, 55 % of it at its crest, depth off the wall.
    """
    crest = width * 0.55
    shape = ((-width / 2, 0.0), (-crest / 2, depth), (crest / 2, depth), (width / 2, 0.0))
    outline = [G(foot[0] + across[0] * side + out[0] * reach, low, foot[1] + across[1] * side + out[1] * reach) for side, reach in shape]
    return plate(scene, name, outline, G(0.0, high - low, 0.0), material_name)


# ===========================================================================
# ================================   TRADE   ================================
# ===========================================================================
#
#   build_kiosk             A street kiosk: a box on a concrete plinth, a wide serving window
#                           over a metal counter, a striped canopy with a hanging edge, a lit
#                           sign board on the roof, magazines on the front, papers and fruit
#                           on the counter, lit shelves of goods inside.
#   build_vending_machine   A drinks machine: a display of three rows of bottles and cans on
#                           shelves in front of a glass pane, a lit panel over the coin unit,
#                           a delivery slot, metal feet.
#   build_container         A shipping container along Z: corrugated sides and back, a dark
#                           frame of rails and corner posts with metal corner castings, fork
#                           pockets, double doors with four locking bars at the +Z end.
#
#   The container ends flat and closed at y = 1 and stacks onto itself. Kiosk and vending
#   machine carry nothing.
#

KIOSK_FRONT, KIOSK_BACK = 0.31, -0.45                 # x of the front and back wall of the kiosk
KIOSK_HALF = 0.45                                     # half its width along Z
KIOSK_WALL = 0.08                                     # thickness of the walls around the room
KIOSK_COUNTER = 0.4                                   # height where the serving window starts
KIOSK_EAVE = 0.82                                     # underside of the roof slab
KIOSK_ROOF = 0.87                                     # its flat top, the sign stands on it
CANOPY_STRIPES = 6                                    # stripes of the canopy, paint and trim in turn
CANOPY_REACH = 0.5                                    # x of its hanging edge: the front of the box


def build_kiosk(scene):
    inside = KIOSK_HALF - KIOSK_WALL                  # half width of the room and of the serving window
    # Concrete plinth, the closed lower body up to the counter
    box(scene, 'Plinth', (KIOSK_BACK - 0.02, 0.0, -KIOSK_HALF - 0.02), (KIOSK_FRONT + 0.02, 0.05, KIOSK_HALF + 0.02), 'Stone')
    box(scene, 'Body', (KIOSK_BACK, 0.05, -KIOSK_HALF), (KIOSK_FRONT, KIOSK_COUNTER, KIOSK_HALF), 'Paint')
    box(scene, 'Kick', (KIOSK_FRONT, 0.05, -KIOSK_HALF + 0.03), (KIOSK_FRONT + 0.012, 0.11, KIOSK_HALF - 0.03), 'PaintDark')
    # Walls around the room: the back, both flanks, a header over the serving window
    box(scene, 'WallBack', (KIOSK_BACK, KIOSK_COUNTER, -KIOSK_HALF), (KIOSK_BACK + KIOSK_WALL, KIOSK_EAVE, KIOSK_HALF), 'Paint')
    for side in (-1, 1):
        near, far = sorted((side * inside, side * KIOSK_HALF))
        box(scene, f'WallSide{side}', (KIOSK_BACK + KIOSK_WALL, KIOSK_COUNTER, near), (KIOSK_FRONT, KIOSK_EAVE, far), 'Paint')
        # Flank window: a pane in a dark frame
        face = side * KIOSK_HALF
        pane = sorted((face, face + side * 0.008))
        frame = sorted((face, face + side * 0.022))
        box(scene, f'Pane{side}', (-0.27, 0.52, pane[0]), (0.17, 0.74, pane[1]), 'Glass')
        for k, (low, high) in enumerate(((0.49, 0.52), (0.74, 0.77))):
            box(scene, f'PaneRail{side}{k}', (-0.3, low, frame[0]), (0.2, high, frame[1]), 'PaintDark')
        for k, (start, end) in enumerate(((-0.3, -0.27), (-0.065, -0.035), (0.17, 0.2))):
            box(scene, f'PanePost{side}{k}', (start, 0.52, frame[0]), (end, 0.74, frame[1]), 'PaintDark')
    # Staff door in the back wall
    box(scene, 'BackDoor', (KIOSK_BACK - 0.012, 0.05, -0.16), (KIOSK_BACK, 0.72, 0.16), 'PaintDark')
    box(scene, 'BackHandle', (KIOSK_BACK - 0.032, 0.35, 0.085), (KIOSK_BACK - 0.012, 0.42, 0.115), 'Metal')
    box(scene, 'Header', (KIOSK_FRONT - KIOSK_WALL, KIOSK_EAVE - 0.035, -inside), (KIOSK_FRONT, KIOSK_EAVE, inside), 'Paint')
    # Counter: a metal slab through the window, standing out to the front
    box(scene, 'Counter', (KIOSK_BACK + KIOSK_WALL, KIOSK_COUNTER, -inside), (KIOSK_FRONT, KIOSK_COUNTER + 0.03, inside), 'Metal')
    box(scene, 'CounterLip', (KIOSK_FRONT, KIOSK_COUNTER - 0.015, -KIOSK_HALF - 0.01), (0.43, KIOSK_COUNTER + 0.035, KIOSK_HALF + 0.01), 'Metal')
    for side in (-1, 1):
        # A bracket under each end of the lip
        z = side * 0.36
        plate(scene, f'Bracket{side}', [G(KIOSK_FRONT, KIOSK_COUNTER - 0.015, z - 0.02), G(0.41, KIOSK_COUNTER - 0.015, z - 0.02), G(KIOSK_FRONT, 0.27, z - 0.02)],
              G(0.0, 0.0, 0.04), 'PaintDark')
    # Inside: the lit back wall, a lamp under the roof, two shelves full of goods
    back = KIOSK_BACK + KIOSK_WALL
    box(scene, 'LightInside', (back, KIOSK_COUNTER + 0.03, -inside), (back + 0.012, KIOSK_EAVE, inside), 'Light')
    box(scene, 'LightCeiling', (-0.2, KIOSK_EAVE - 0.025, -0.2), (0.1, KIOSK_EAVE, 0.2), 'Light')
    for row, shelf in enumerate((KIOSK_COUNTER + 0.03, 0.555, 0.68)):
        if 0 < row:
            box(scene, f'Shelf{row}', (back + 0.012, shelf - 0.018, -inside), (back + 0.11, shelf, inside), 'Metal')
        # Packs side by side, every one its own height and color
        pack = 2.0 * inside / 8
        for k in range(8):
            z = -inside + pack * k
            height = 0.06 + 0.015 * ((k * 3 + row) % 3)
            box(scene, f'Pack{row}{k}', (back + 0.02, shelf, z + 0.012), (back + 0.1, shelf + height, z + pack - 0.012), GOODS[(k + row) % 3])
    # Roof slab with a dark edge, flat on top
    box(scene, 'Roof', (KIOSK_BACK - 0.02, KIOSK_EAVE, -KIOSK_HALF - 0.02), (KIOSK_FRONT, KIOSK_ROOF, KIOSK_HALF + 0.02), 'Paint')
    box(scene, 'RoofEdge', (KIOSK_BACK - 0.035, KIOSK_EAVE + 0.012, -KIOSK_HALF - 0.035), (KIOSK_FRONT - 0.01, KIOSK_ROOF - 0.012, KIOSK_HALF + 0.035), 'PaintDark')
    # Canopy: stripes slanting down from the roof to the front, each ends in a hanging flap
    stripe = 2.0 * (KIOSK_HALF + 0.03) / CANOPY_STRIPES
    for k in range(CANOPY_STRIPES):
        z = -KIOSK_HALF - 0.03 + stripe * k
        is_dark = 0 == k % 2
        drop = 0.7 if is_dark else 0.725               # the dark flaps hang lower: a toothed edge
        outline = [G(KIOSK_FRONT - 0.02, 0.825, z), G(CANOPY_REACH - 0.02, 0.765, z), G(CANOPY_REACH - 0.02, drop, z), G(CANOPY_REACH, drop, z),
                   G(CANOPY_REACH, 0.79, z), G(KIOSK_FRONT - 0.02, KIOSK_ROOF, z)]
        plate(scene, f'Canopy{k}', outline, G(0.0, 0.0, stripe), 'PaintDark' if is_dark else 'Paint')
    # Sign board on the roof: a dark frame on two feet, a lit face with five dark letters
    for side in (-1, 1):
        box(scene, f'SignFoot{side}', (0.03, KIOSK_ROOF, side * 0.3 - 0.03), (0.09, KIOSK_ROOF + 0.03, side * 0.3 + 0.03), 'Metal')
    box(scene, 'Sign', (0.02, KIOSK_ROOF + 0.02, -0.4), (0.1, 1.0, 0.4), 'PaintDark')
    box(scene, 'LightSign', (0.1, KIOSK_ROOF + 0.04, -0.37), (0.112, 0.98, 0.37), 'Light')
    for k, (start, end) in enumerate(((-0.3, -0.2), (-0.16, -0.1), (-0.06, 0.06), (0.1, 0.16), (0.2, 0.3))):
        box(scene, f'Letter{k}', (0.112, KIOSK_ROOF + 0.065, start), (0.122, 0.955, end), 'PaintDark')
    # Magazine rack on the front: two rails, two rows of covers leaning back on them
    for row, low in enumerate((0.12, 0.25)):
        box(scene, f'RackRail{row}', (KIOSK_FRONT, low - 0.02, -0.4), (KIOSK_FRONT + 0.035, low, 0.4), 'Metal')
        for k in range(5):
            z = -0.39 + 0.158 * k
            board(scene, f'Cover{row}{k}', (KIOSK_FRONT + 0.022, low + 0.058), math.radians(78.0), 0.115, 0.014, z + 0.012, z + 0.146,
                  (GOODS + ('Bone',))[(k + row * 2) % 4])
    # On the counter: a stack of papers, a tray of fruit, a jar
    for k in range(3):
        box(scene, f'Paper{k}', (0.325 - 0.008 * k, KIOSK_COUNTER + 0.035 + 0.02 * k, 0.1 + 0.006 * k), (0.42 - 0.008 * k, KIOSK_COUNTER + 0.053 + 0.02 * k, 0.3 + 0.006 * k), 'Bone')
    box(scene, 'Tray', (0.32, KIOSK_COUNTER + 0.035, -0.37), (0.425, KIOSK_COUNTER + 0.06, -0.1), 'Wood')
    for k, (x, z, material_name) in enumerate(((0.35, -0.32, 'FruitRed'), (0.395, -0.265, 'FruitGold'), (0.35, -0.21, 'FruitGold'), (0.395, -0.15, 'FruitRed'))):
        fruit(scene, f'Fruit{k}', (x, KIOSK_COUNTER + 0.092, z), 0.04, material_name)
    lathe(scene, 'Jar', [(0.034, KIOSK_COUNTER + 0.035), (0.04, KIOSK_COUNTER + 0.1), (0.028, KIOSK_COUNTER + 0.12), (0.028, KIOSK_COUNTER + 0.12), (0.0, KIOSK_COUNTER + 0.12)],
          'Glass', segments=8, center=(0.37, 0.0))
    lathe(scene, 'JarLid', [(0.033, KIOSK_COUNTER + 0.118), (0.033, KIOSK_COUNTER + 0.138), (0.033, KIOSK_COUNTER + 0.138), (0.0, KIOSK_COUNTER + 0.138)],
          'FruitRed', segments=8, center=(0.37, 0.0), is_smooth=False)
    trim_height(scene, 1.0)


VEND_FRONT, VEND_BACK = 0.15, -0.22                   # x of the front and back of the vending machine
VEND_HALF = 0.23                                      # half its width along Z
VEND_PROUD = 0.215                                    # x of everything standing off the front: frame and trim
DISPLAY = (-0.19, 0.07)                               # z of both edges of the display, the coin unit is right of it
DISPLAY_LOW, DISPLAY_HIGH = 0.375, 0.86                 # heights of its lower and upper edge
VEND_ROWS = (0.4, 0.555, 0.71)                        # heights the three rows of drinks stand on
DRINKS = 4                                            # drinks per row
DRINK_RADIUS = 0.026


def build_vending_machine(scene):
    # Feet, the body on a dark kick board, a dark cap on top
    for i, (sx, sz) in enumerate(((1, 1), (1, -1), (-1, 1), (-1, -1))):
        x, z = -0.035 + sx * 0.15, sz * 0.19
        box(scene, f'Foot{i}', (x - 0.03, 0.0, z - 0.03), (x + 0.03, 0.04, z + 0.03), 'Metal')
    box(scene, 'Body', (VEND_BACK, 0.04, -VEND_HALF), (VEND_FRONT, 0.96, VEND_HALF), 'Paint')
    box(scene, 'Kick', (VEND_BACK - 0.008, 0.04, -VEND_HALF - 0.008), (VEND_FRONT + 0.008, 0.1, VEND_HALF + 0.008), 'PaintDark')
    box(scene, 'Cap', (VEND_BACK - 0.012, 0.955, -VEND_HALF - 0.012), (VEND_FRONT + 0.03, 1.0, VEND_HALF + 0.012), 'PaintDark')
    # Display: a glass pane in a deep dark frame
    box(scene, 'Glass', (VEND_FRONT, DISPLAY_LOW, DISPLAY[0]), (VEND_FRONT + 0.008, DISPLAY_HIGH, DISPLAY[1]), 'Glass')
    for k, (start, end) in enumerate(((DISPLAY[0] - 0.028, DISPLAY[0]), (DISPLAY[1], DISPLAY[1] + 0.028))):
        box(scene, f'Jamb{k}', (VEND_FRONT, DISPLAY_LOW - 0.028, start), (VEND_PROUD, DISPLAY_HIGH + 0.028, end), 'PaintDark')
    box(scene, 'Head', (VEND_FRONT, DISPLAY_HIGH, DISPLAY[0]), (VEND_PROUD, DISPLAY_HIGH + 0.028, DISPLAY[1]), 'PaintDark')
    box(scene, 'Sill', (VEND_FRONT, DISPLAY_LOW - 0.028, DISPLAY[0]), (VEND_PROUD, DISPLAY_LOW, DISPLAY[1]), 'PaintDark')
    # Three rows of drinks on metal shelves: bottles on top, cans below, one color per row
    step = (DISPLAY[1] - DISPLAY[0]) / DRINKS
    x = VEND_FRONT + 0.008 + DRINK_RADIUS             # the drinks touch the pane behind them
    for row, (low, material_name) in enumerate(zip(VEND_ROWS, reversed(GOODS))):
        box(scene, f'Shelf{row}', (VEND_FRONT, low - 0.02, DISPLAY[0]), (VEND_PROUD - 0.008, low, DISPLAY[1]), 'Metal')
        for k in range(DRINKS):
            z = DISPLAY[0] + step * (k + 0.5)
            # One drink per row is of another sort
            color = GOODS[(3 - row) % 3] if (row + 1) % DRINKS == k else material_name
            if len(VEND_ROWS) - 1 == row:
                points = [G(x, low, z), G(x, low + 0.075, z), G(x, low + 0.1, z), G(x, low + 0.125, z)]
                pole(scene, f'Bottle{k}', points, [DRINK_RADIUS, DRINK_RADIUS, 0.012, 0.012], color, sides=7, is_level=True)
                box(scene, f'BottleCap{k}', (x - 0.014, low + 0.125, z - 0.014), (x + 0.014, low + 0.14, z + 0.014), 'Metal')
            else:
                pole(scene, f'Can{row}{k}', [G(x, low, z), G(x, low + 0.105, z)], [DRINK_RADIUS, DRINK_RADIUS], color, sides=7, is_level=True)
                pole(scene, f'CanTop{row}{k}', [G(x, low + 0.105, z), G(x, low + 0.115, z)], [DRINK_RADIUS * 0.8, DRINK_RADIUS * 0.8], 'Metal', sides=7, is_level=True)
    # Right of the display: a lit panel, the coin unit with slot and return cup, three buttons
    box(scene, 'LightPanel', (VEND_FRONT, 0.7, 0.115), (VEND_FRONT + 0.016, 0.86, 0.205), 'Light')
    # Lit sign across the head of the machine
    box(scene, 'LightSign', (VEND_FRONT, 0.895, -0.2), (VEND_FRONT + 0.016, 0.945, 0.2), 'Light')
    box(scene, 'CoinUnit', (VEND_FRONT, 0.42, 0.115), (VEND_FRONT + 0.03, 0.66, 0.205), 'Metal')
    box(scene, 'CoinSlot', (VEND_FRONT + 0.03, 0.59, 0.15), (VEND_FRONT + 0.036, 0.63, 0.17), 'Door')
    box(scene, 'CoinCup', (VEND_FRONT + 0.03, 0.44, 0.135), (VEND_FRONT + 0.036, 0.485, 0.185), 'Door')
    for k, material_name in enumerate(GOODS):
        disc(scene, f'Button{k}', VEND_FRONT + 0.03, VEND_FRONT + 0.045, 0.54, 0.135 + 0.025 * k, 0.011, material_name, sides=6)
    # Delivery slot: a dark flap in a metal frame, a lip below it
    box(scene, 'SlotFrame', (VEND_FRONT, 0.125, DISPLAY[0] - 0.02), (VEND_FRONT + 0.025, 0.315, DISPLAY[1] + 0.02), 'Metal')
    box(scene, 'Slot', (VEND_FRONT + 0.025, 0.15, DISPLAY[0] + 0.005), (VEND_FRONT + 0.032, 0.29, DISPLAY[1] - 0.005), 'Door')
    box(scene, 'SlotLip', (VEND_FRONT + 0.025, 0.125, DISPLAY[0] - 0.02), (VEND_FRONT + 0.06, 0.15, DISPLAY[1] + 0.02), 'Metal')
    # Vent on each flank: dark louvres low down
    for side in (-1, 1):
        face = side * VEND_HALF
        for k in range(3):
            near, far = sorted((face, face + side * 0.01))
            box(scene, f'Vent{side}{k}', (-0.17, 0.16 + 0.06 * k, near), (0.0, 0.19 + 0.06 * k, far), 'PaintDark')
    # Cooling grille on the back: a dark opening behind four slats
    box(scene, 'Grille', (VEND_BACK - 0.008, 0.2, -0.17), (VEND_BACK, 0.6, 0.17), 'Door')
    for k in range(4):
        low = 0.215 + 0.1 * k
        box(scene, f'GrilleSlat{k}', (VEND_BACK - 0.02, low, -0.18), (VEND_BACK, low + 0.05, 0.18), 'PaintDark')
    trim_height(scene, 1.0)


BOX_HALF = 0.435                                      # half width of the sheet metal box of the container
BOX_DOORS, BOX_END = 0.94, -0.955                     # z of its two end walls: the doors sit a little deeper
FRAME = 0.49                                          # how far rails and posts reach out along X
FRAME_END = 0.99                                      # and along Z
RAIL_LOW, RAIL_HIGH = 0.09, 0.94                      # heights where the lower rail ends and the upper one starts
POST = 0.09                                           # width of a corner post along Z, 0.12 across the end wall
CASTING = 0.12                                        # length of a corner casting along Z
SIDE_RIBS = 11                                        # ribs on each long side
END_RIBS = 5                                          # ribs on the closed end
RIB_DEPTH = 0.045                                     # how far a rib stands off the sheet


def build_container(scene):
    # The sheet metal box, closed and flat on top: containers stack
    box(scene, 'Box', (-BOX_HALF, 0.02, BOX_END), (BOX_HALF, 1.0, BOX_DOORS), 'Paint')
    # Frame along both long sides: lower and upper rail, a corner post at each end, fork pockets
    for side in (-1, 1):
        near, far = sorted((side * BOX_HALF, side * FRAME))
        reach = 1.0 - CASTING
        box(scene, f'RailLow{side}', (near, 0.0, -reach), (far, RAIL_LOW, reach), 'PaintDark')
        box(scene, f'RailHigh{side}', (near, RAIL_HIGH, -reach), (far, 1.0, reach), 'PaintDark')
        for k, z in enumerate((-0.42, 0.42)):
            pocket = sorted((side * FRAME, side * (FRAME + 0.006)))
            box(scene, f'Pocket{side}{k}', (pocket[0], 0.015, z - 0.1), (pocket[1], 0.07, z + 0.1), 'Door')
        # Corrugated sheet between the posts
        span = 2.0 * (FRAME_END - POST)
        for k in range(SIDE_RIBS):
            z = -span / 2 + span * (k + 0.5) / SIDE_RIBS
            rib(scene, f'Rib{side}{k}', (side * (BOX_HALF - 0.005), z), (side, 0), (0, 1), span / SIDE_RIBS * 0.72, RIB_DEPTH, RAIL_LOW, RAIL_HIGH, 'Paint')
        for end in (-1, 1):
            # Corner post, standing in front of the end wall too
            across = sorted((side * (FRAME - 0.12), side * FRAME))
            along = sorted((end * (FRAME_END - POST), end * FRAME_END))
            box(scene, f'Post{side}{end}', (across[0], RAIL_LOW, along[0]), (across[1], RAIL_HIGH, along[1]), 'PaintDark')
            # Corner castings above and below it, outside the sheet metal box
            cast = sorted((end * (1.0 - CASTING), end * 1.0))
            outer = sorted((side * BOX_HALF, side * 0.5))
            for k, (low, high) in enumerate(((0.0, RAIL_LOW), (RAIL_HIGH - 0.02, 1.0))):
                box(scene, f'Casting{side}{end}{k}', (outer[0], low, cast[0]), (outer[1], high, cast[1]), 'Metal')
                hole = sorted((side * 0.5, side * 0.506))
                box(scene, f'CastingHole{side}{end}{k}', (hole[0], low + 0.025, cast[0] + 0.035), (hole[1], high - 0.025, cast[1] - 0.035), 'Door')
    # Sill and header across both ends, between the castings
    for end, wall in ((-1, BOX_END), (1, BOX_DOORS)):
        near, far = sorted((wall, end * FRAME_END))
        box(scene, f'Sill{end}', (-BOX_HALF, 0.0, near), (BOX_HALF, RAIL_LOW, far), 'PaintDark')
        box(scene, f'Header{end}', (-BOX_HALF, RAIL_HIGH, near), (BOX_HALF, 1.0, far), 'PaintDark')
    # Closed end: corrugated like the sides
    span = 2.0 * (FRAME - 0.12)
    for k in range(END_RIBS):
        x = -span / 2 + span * (k + 0.5) / END_RIBS
        rib(scene, f'RibEnd{k}', (x, BOX_END + 0.005), (0, -1), (1, 0), span / END_RIBS * 0.72, RIB_DEPTH - 0.015, RAIL_LOW, RAIL_HIGH, 'Paint')
    # Doors: two leaves with a dark gap between them, three proud panels each, hinges on the posts
    face = BOX_DOORS + 0.012
    box(scene, 'DoorGap', (-0.012, RAIL_LOW, BOX_DOORS), (0.012, RAIL_HIGH, BOX_DOORS + 0.006), 'Door')
    for side in (-1, 1):
        leaf = sorted((side * 0.012, side * (FRAME - 0.12)))
        box(scene, f'Leaf{side}', (leaf[0], RAIL_LOW, BOX_DOORS), (leaf[1], RAIL_HIGH, face), 'Paint')
        for k in range(3):
            low = RAIL_LOW + 0.05 + 0.265 * k
            panel = sorted((side * 0.04, side * (FRAME - 0.15)))
            box(scene, f'LeafPanel{side}{k}', (panel[0], low, face), (panel[1], low + 0.21, face + 0.012), 'Paint')
        for k, y in enumerate((0.2, 0.51, 0.82)):
            hinge = sorted((side * (FRAME - 0.16), side * (FRAME - 0.09)))
            box(scene, f'Hinge{side}{k}', (hinge[0], y - 0.03, face), (hinge[1], y + 0.03, FRAME_END + 0.006), 'PaintDark')
        # Two locking bars per leaf from sill to header: cam keepers at both ends, guides, a handle
        for k, x in enumerate((side * 0.11, side * 0.27)):
            pole(scene, f'Bar{side}{k}', [G(x, RAIL_LOW - 0.04, 0.975), G(x, RAIL_HIGH + 0.04, 0.975)], [0.014, 0.014], 'Metal', sides=6, is_level=True)
            for j, y in enumerate((RAIL_LOW - 0.035, RAIL_HIGH + 0.035)):
                box(scene, f'Keeper{side}{k}{j}', (x - 0.03, y - 0.022, FRAME_END), (x + 0.03, y + 0.022, 0.998), 'Metal')
            for j, y in enumerate((0.3, 0.72)):
                box(scene, f'Guide{side}{k}{j}', (x - 0.024, y - 0.018, face + 0.012), (x + 0.024, y + 0.018, 0.992), 'Metal')
            handle = sorted((x, x - side * 0.1))
            box(scene, f'Handle{side}{k}', (handle[0], 0.44 + 0.07 * k, 0.975), (handle[1], 0.468 + 0.07 * k, 0.995), 'Metal')
    # Data plate on the left leaf
    box(scene, 'DataPlate', (-0.2, 0.2, face + 0.012), (-0.14, 0.29, face + 0.02), 'Metal')
    trim_height(scene, 1.0)


# ===========================================================================
# ================================   STREET   ===============================
# ===========================================================================
#
#   build_bench           A park bench looking to +X: four wooden slats as seat and three as
#                         back on two cast metal sides with armrests, a stretcher between
#                         them, a round litter bin on a post at its +Z end.
#   lamp                  One lamp of the traffic light: a domed lens under a visor.
#   build_traffic_light   A traffic light: concrete foot, painted sleeve, metal pole, a black
#                         housing on a painted backboard with red, amber and green lamps under
#                         visors looking to +X, a pedestrian button box on the pole.
#
#   The traffic light stands off center (x = SIGNAL_POLE), so its visors stay inside the box.
#

BENCH_ENDS = (-0.5, 0.26)                             # z of both ends of the slats, the bin takes the rest
BENCH_SIDES = (-0.43, 0.19)                           # z of the two cast sides
SEAT = ((0.18, 0.212), (0.092, 0.206), (0.004, 0.2), (-0.084, 0.196))     # (x, y) of the seat slats: sloping back a little
BACK_FOOT, BACK_TOP = (-0.12, 0.262), (-0.2, 0.475)   # (x, y) of the line the back slats lean on
BIN = (0.02, 0.385)                                   # (x, z) of the litter bin
BIN_RADIUS = 0.088


def build_bench(scene):
    # Seat: four slats, the front one tipped down over the edge
    for k, (x, y) in enumerate(SEAT):
        tilt = math.radians(-20.0 if 0 == k else 3.0)
        board(scene, f'Seat{k}', (x, y), tilt, 0.076, 0.026, BENCH_ENDS[0], BENCH_ENDS[1], 'Wood')
    # Back: three slats leaning back
    lean = math.atan2(BACK_TOP[1] - BACK_FOOT[1], BACK_TOP[0] - BACK_FOOT[0])
    for k, share in enumerate((0.16, 0.5, 0.84)):
        x, y = BACK_FOOT[0] + (BACK_TOP[0] - BACK_FOOT[0]) * share, BACK_FOOT[1] + (BACK_TOP[1] - BACK_FOOT[1]) * share
        board(scene, f'Back{k}', (x, y), lean, 0.072, 0.024, BENCH_ENDS[0], BENCH_ENDS[1], 'Wood')
    # Cast sides: back leg running up into the back post, front leg curling up into the armrest
    for i, z in enumerate(BENCH_SIDES):
        rear = curve([G(-0.2, 0.0, z), G(-0.125, 0.17, z), G(-0.155, 0.3, z), G(-0.225, 0.485, z)], 7)
        pole(scene, f'Rear{i}', rear, [0.028, 0.026, 0.025, 0.024, 0.023, 0.022, 0.02], 'Metal', sides=5)
        front = curve([G(0.215, 0.0, z), G(0.165, 0.1, z), G(0.175, 0.2, z), G(0.205, 0.31, z)], 6)
        pole(scene, f'Front{i}', front, [0.028, 0.026, 0.025, 0.023, 0.022, 0.022], 'Metal', sides=5)
        beam(scene, f'SeatRail{i}', G(-0.14, 0.168, z), G(0.19, 0.182, z), 0.04, 0.034, 'Metal')
        beam(scene, f'Arm{i}', G(0.235, 0.318, z), G(-0.19, 0.345, z), 0.046, 0.028, 'Metal')
        for k, x in enumerate((-0.2, 0.215)):
            box(scene, f'Pad{i}{k}', (x - 0.04, 0.0, z - 0.035), (x + 0.03, 0.016, z + 0.035), 'Metal')
    pole(scene, 'Stretcher', [G(0.0, 0.1, BENCH_SIDES[0]), G(0.0, 0.1, BENCH_SIDES[1])], [0.016, 0.016], 'Metal', sides=5)
    for i, z in enumerate(BENCH_SIDES):
        beam(scene, f'Strut{i}', G(-0.15, 0.1, z), G(0.17, 0.1, z), 0.03, 0.026, 'Metal')
    # Litter bin: a dark fluted drum on a post, metal bands, a wide rim around the dark opening
    lathe(scene, 'BinBase', [(0.07, 0.0), (0.07, 0.016), (0.07, 0.016), (0.0, 0.016)], 'Metal', segments=10, center=BIN, is_smooth=False)
    pole(scene, 'BinPost', [G(BIN[0], 0.016, BIN[1]), G(BIN[0], 0.08, BIN[1])], [0.028, 0.028], 'Metal', sides=6, is_level=True)
    lathe(scene, 'Bin', [(0.0, 0.07), (BIN_RADIUS - 0.014, 0.07), (BIN_RADIUS - 0.014, 0.07), (BIN_RADIUS, 0.33), (BIN_RADIUS, 0.33), (0.0, 0.33)], 'PaintDark',
          segments=10, center=BIN, is_smooth=False, bulge=lambda k, j: 0.008 if 0 == k % 2 else 0.0)
    for k, y in enumerate((0.11, 0.27)):
        lathe(scene, f'BinBand{k}', [(BIN_RADIUS, y - 0.016), (BIN_RADIUS + 0.012, y - 0.01), (BIN_RADIUS + 0.012, y + 0.01), (BIN_RADIUS, y + 0.016)], 'Metal',
              segments=10, center=BIN)
    rim, mouth = (BIN_RADIUS + 0.012, 0.36), (BIN_RADIUS - 0.03, 0.36)
    lathe(scene, 'BinRim', [(BIN_RADIUS - 0.005, 0.32), (BIN_RADIUS + 0.012, 0.335), rim, rim, mouth, mouth, (BIN_RADIUS - 0.03, 0.345)], 'Metal',
          segments=10, center=BIN)
    lathe(scene, 'BinMouth', [(BIN_RADIUS - 0.03, 0.345), (0.0, 0.345)], 'Door', segments=10, center=BIN)
    trim_height(scene, 0.5)


SIGNAL_POLE = -0.1                                    # x of the pole, the lamps look forward from there
HOUSING = (-0.02, 0.1)                                # x of the back and the face of the housing
HOUSING_HALF = 0.095                                  # half its width along Z
HOUSING_LOW, HOUSING_HIGH = 0.925, 1.47               # heights of its lower and upper edge
LAMP_RADIUS = 0.06
LAMPS = ((1.375, 'FruitRed'), (1.2, 'FruitGold'), (1.025, 'FruitGreen'))  # height and color from the top down
VISOR_LONG, VISOR_SHORT = 0.215, 0.135                # x where a visor ends at its crown and at its lower ends
VISOR_STEPS = 8                                       # flat faces around a visor


def lamp(scene, name, y, material_name):
    """A lamp on the face of the housing at height y: a dark ring, a domed lens, a visor over both."""
    face = HOUSING[1]
    disc(scene, f'{name}Ring', face, face + 0.012, y, 0.0, LAMP_RADIUS + 0.012, 'PaintDark')
    disc(scene, f'{name}Lens', face, face + 0.022, y, 0.0, LAMP_RADIUS, material_name, dome=0.012)
    # Visor: the upper part of a tube, reaching down past the middle on both sides
    inner, outer = LAMP_RADIUS + 0.012, LAMP_RADIUS + 0.027
    angles = [math.radians(-25.0 + 230.0 * k / VISOR_STEPS) for k in range(VISOR_STEPS + 1)]
    outline = [G(face, y + math.sin(angle) * outer, math.cos(angle) * outer) for angle in angles]
    outline += [G(face, y + math.sin(angle) * inner, math.cos(angle) * inner) for angle in reversed(angles)]
    # Cut off slanted: the far end lies in one plane leaning back from the crown
    lowest = y + math.sin(angles[0]) * outer
    far = [G(VISOR_SHORT + (VISOR_LONG - VISOR_SHORT) * (point.z - lowest) / (y + outer - lowest), 0.0, 0.0) + G(0.0, point.z, -point.y) for point in outline]
    loft(scene, f'{name}Visor', [outline, far], 'Rubber', is_smooth=False)


def build_traffic_light(scene):
    # Concrete foot with a slanted shoulder, a painted sleeve with a ring, the pole
    lathe(scene, 'Foot', [(0.125, 0.0), (0.125, 0.07), (0.125, 0.07), (0.075, 0.13), (0.075, 0.13), (0.0, 0.13)], 'Stone',
          segments=8, center=(SIGNAL_POLE, 0.0), is_smooth=False, has_bottom_cap=True)
    lathe(scene, 'Sleeve', [(0.058, 0.13), (0.053, 0.4), (0.053, 0.4), (0.034, 0.44)], 'Paint', segments=8, center=(SIGNAL_POLE, 0.0))
    lathe(scene, 'SleeveRing', [(0.048, 0.35), (0.068, 0.365), (0.068, 0.395), (0.048, 0.41)], 'PaintDark', segments=8, center=(SIGNAL_POLE, 0.0))
    pole(scene, 'Pole', [G(SIGNAL_POLE, 0.13, 0), G(SIGNAL_POLE, 1.4, 0)], [0.04, 0.036], 'Metal', sides=8, is_level=True)
    lathe(scene, 'PoleCap', [(0.044, 1.39), (0.044, 1.42), (0.03, 1.445), (0.0, 1.45)], 'Metal', segments=8, center=(SIGNAL_POLE, 0.0))
    # Backboard behind the housing, two brackets holding it to the pole
    box(scene, 'Board', (HOUSING[0] - 0.025, 0.885, -0.165), (HOUSING[0], 1.5, 0.165), 'Paint')
    for k, y in enumerate((1.02, 1.32)):
        box(scene, f'Clamp{k}', (SIGNAL_POLE - 0.05, y - 0.03, -0.055), (HOUSING[0] - 0.025, y + 0.03, 0.055), 'Metal')
    # Housing: a black box, a painted band between the lamps, the three lamps
    box(scene, 'Housing', (HOUSING[0], HOUSING_LOW, -HOUSING_HALF), (HOUSING[1], HOUSING_HIGH, HOUSING_HALF), 'Rubber')
    for k, y in enumerate((1.2875, 1.1125)):
        box(scene, f'Seam{k}', (HOUSING[0] + 0.01, y - 0.008, -HOUSING_HALF - 0.006), (HOUSING[1] + 0.004, y + 0.008, HOUSING_HALF + 0.006), 'Paint')
    for k, (y, material_name) in enumerate(LAMPS):
        lamp(scene, f'Lamp{k}', y, material_name)
    # Pedestrian button box on the pole: a painted box, a dark plate, a lit "wait" strip, the button
    front = SIGNAL_POLE + 0.115
    box(scene, 'ButtonBox', (SIGNAL_POLE + 0.02, 0.56, -0.06), (front, 0.76, 0.06), 'Paint')
    box(scene, 'ButtonPlate', (front, 0.575, -0.045), (front + 0.008, 0.745, 0.045), 'PaintDark')
    box(scene, 'LightWait', (front + 0.008, 0.685, -0.032), (front + 0.014, 0.725, 0.032), 'Light')
    disc(scene, 'Button', front + 0.008, front + 0.024, 0.625, 0.0, 0.026, 'Metal', sides=8, dome=0.008)
    lathe(scene, 'ButtonBand', [(0.04, 0.6), (0.05, 0.61), (0.05, 0.71), (0.04, 0.72)], 'Metal', segments=8, center=(SIGNAL_POLE, 0.0))
    trim_height(scene, 1.5)
