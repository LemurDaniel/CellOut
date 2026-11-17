# NOTE:
#   Building blocks of the modern town (assets/models/building/), the home group: a small
#   brick storey, a gabled roof of clay tiles, a rack of solar panels, a balcony and a fire
#   escape. Same frame as models/buildings.py: +Y up, +X is the front of a block, the origin
#   is the center of its footprint on the ground, one grid cell is 0.5 units, a block stays
#   inside its box and ends flat where the next one stacks. Siblings of models/modern.py:
#   straight edges, no wobble, the same bricks, windows and cornice. Materials: Paint (what
#   the player repaints), PaintDark (its trim), Stone (concrete), Glass, Solar (the dark cells
#   of the solar panels), Metal, Clay, Leaf,
#   FruitRed and FruitGold (blossoms). Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module modern_home -Call 'build_tile_roof(scene)' -Name tileroof

#
#   Chapters:
#     HELPERS       Windows, corner blocks and proud bricks on a wall of any size.
#     BRICK HOUSE   Small brick room (2 x 2 x 2 cells) and tile roof (4 x 2 x 4).
#     ROOFTOP       Solar panels (2 x 1 x 2).
#     FACADE        Balcony (1 x 1 x 2) and fire escape (1 x 2 x 2).
#

import math
import random

from mathutils import Vector

from models.buildings import beam, fruit, plate, pole, trim_height
from models.helpers import G, ellipsoid
from models.modern import (BRICK_COURSES, BRICK_FIRST, BRICK_HEADER, BRICK_HEIGHT, BRICK_LENGTH, BRICK_PITCH, QUOIN_LONG, QUOIN_SHORT,
                           QUOINS, SIDES, WINDOW_FRAME, WINDOW_HIGH, WINDOW_LOW, band, box, wall_box)

BRICK_JOINT = 0.012                                   # the least gap between a proud brick and its neighbours


# ===========================================================================
# ===============================   HELPERS   ===============================
# ===========================================================================
#
#   window_on   A sash window on a wall: pane, frame, glazing bars, concrete sill and lintel.
#   quoins_on   Corner blocks at both ends of a wall, locking around the corner.
#   bricks_on   Scattered bricks standing proud of a wall, clear of windows and corners.
#
#   The same details as brick_window, quoins and proud_bricks of models/modern.py, which are
#   fixed to the wall of a 4 x 4 footprint: these take the distance of the wall from the
#   middle of the block (reach) and so fit a wall of any size.
#

def window_on(scene, name, side, reach, center, width, low=WINDOW_LOW, high=WINDOW_HIGH, has_lintel=True):
    """
    A window around center along the wall that looks along side (x, z) and stands reach
    away from the middle, its pane from low to high. Returns the area (start, end, low,
    high) it takes on the wall.
    """
    start, end = center - width / 2, center + width / 2
    wall_box(scene, f'{name}Pane', side, start, end, low, high, reach - 0.03, reach + 0.008, 'Glass')
    # Frame: jambs, head and bottom rail around the pane
    for i, edge in enumerate((start - WINDOW_FRAME, end)):
        wall_box(scene, f'{name}Jamb{i}', side, edge, edge + WINDOW_FRAME, low, high, reach - 0.03, reach + 0.032, 'PaintDark')
    wall_box(scene, f'{name}Head', side, start, end, high - WINDOW_FRAME, high, reach - 0.03, reach + 0.032, 'PaintDark')
    wall_box(scene, f'{name}Rail', side, start, end, low, low + WINDOW_FRAME, reach - 0.03, reach + 0.032, 'PaintDark')
    # Glazing bars: one upright, one across in the upper third (a sash window)
    wall_box(scene, f'{name}Post', side, center - 0.014, center + 0.014, low + WINDOW_FRAME, high - WINDOW_FRAME, reach - 0.03, reach + 0.022, 'PaintDark')
    bar = low + (high - low) * 0.62
    wall_box(scene, f'{name}Bar', side, start, end, bar - 0.014, bar + 0.014, reach - 0.03, reach + 0.02, 'PaintDark')
    # Concrete sill that sticks out, a lintel above where the wall has room for one
    span = width / 2 + WINDOW_FRAME
    wall_box(scene, f'{name}Sill', side, center - span - 0.03, center + span + 0.03, low - 0.05, low, reach - 0.03, reach + 0.065, 'Stone')
    if not has_lintel:
        return (center - span - 0.03, center + span + 0.03, low - 0.05, high)
    wall_box(scene, f'{name}Lintel', side, center - span - 0.035, center + span + 0.035, high, high + 0.075, reach - 0.03, reach + 0.04, 'Stone')
    return (center - span - 0.035, center + span + 0.035, low - 0.05, high + 0.075)


def quoins_on(scene, name, side, reach, is_long_first):
    """
    Corner blocks at both ends of a wall, long and short in turn. The neighbour wall starts
    with the other length, so the blocks lock around the corner. Returns the areas they take.
    """
    pitch = (0.89 - BRICK_FIRST) / QUOINS             # they end under the teeth of the cornice at 0.89
    for k in range(QUOINS):
        length = QUOIN_LONG if is_long_first == (0 == k % 2) else QUOIN_SHORT
        low = BRICK_FIRST + k * pitch
        for end in (-1, 1):
            start, stop = sorted((end * (reach + 0.016), end * (reach - length)))
            wall_box(scene, f'{name}{end}{k}', side, start, stop, low, low + pitch - 0.03, reach - 0.02, reach + 0.016, 'PaintDark')
    return [(end * reach - QUOIN_LONG, end * reach + QUOIN_LONG, 0.0, 1.0) for end in (-1, 1)]


def bricks_on(scene, name, side, reach, taken, count, rng):
    """
    Up to count bricks on the wall, each on its course. A brick starts half bricks away
    from the left or the right corner blocks, every other course shifted by a quarter
    brick. taken holds the areas (start, end, low, high) of windows and corner blocks, a
    brick keeps clear of them and of the bricks before it.
    """
    half = (BRICK_LENGTH + 0.015) / 2                 # half a brick plus half a joint: the step of the bond
    edge = reach - QUOIN_LONG - BRICK_JOINT - 0.003   # where the bricks next to the corner blocks end
    steps = int((2.0 * edge - BRICK_HEADER) / half) + 1
    areas = list(taken)
    placed = 0
    for _ in range(count * 30):                       # tries, most land on a window or a brick
        if count == placed:
            break
        course = rng.randrange(BRICK_COURSES)
        # Two of three are stretchers, the rest headers: bricks laid across the wall, half as long
        length = BRICK_LENGTH if rng.random() < 0.66 else BRICK_HEADER
        inset = rng.randrange(steps) * half + (course % 2) * half / 2
        start = -edge + inset if rng.random() < 0.5 else edge - inset - length
        end, low = start + length, BRICK_FIRST + course * BRICK_PITCH
        high = low + BRICK_HEIGHT
        if start < -edge or edge < end:
            continue
        if any(start < area[1] + BRICK_JOINT and area[0] - BRICK_JOINT < end and low < area[3] + BRICK_JOINT and area[2] - BRICK_JOINT < high
               for area in areas):
            continue
        wall_box(scene, f'{name}{placed}', side, start, end, low, high, reach - 0.02, reach + 0.014, 'PaintDark')
        areas.append((start, end, low, high))
        placed += 1


# ===========================================================================
# =============================   BRICK HOUSE   =============================
# ===========================================================================
#
#   build_small_brick_room   A small brick storey (annex, stairwell): concrete base course,
#                            brick walls with proud bricks and corner blocks, one tall window
#                            on both flanks and the back, a plain front, a cornice on teeth.
#   roof_slope               Height of the tiled slope over a distance from the ridge.
#   build_tile_roof          A gabled roof of clay tiles, its ridge along Z: seven courses of
#                            flat tiles in bond, a ridge of overlapping cap tiles, closed
#                            gables with a window, barge boards, gutters, a dormer looking
#                            to the front and a brick chimney with a clay pot at the back.
#
#   The room ends flat at y = 1 and stacks onto itself. Its front stays plain: entrance,
#   balcony and fire escape lean there. The roof is flat below and overhangs its footprint
#   by 0.21 at the eaves and 0.14 at the gables.
#

ANNEX_WALL = 0.46                                     # outer face of every wall of the small room (2 x 2 cells)
ANNEX_WINDOW = 0.26                                   # width of its windows
ANNEX_DENTILS = 5                                     # teeth under the cornice per wall


def build_small_brick_room(scene):
    rng = random.Random(411)
    # Walls: one closed brick box, its top is the flat top of the room
    box(scene, 'Walls', (-ANNEX_WALL, 0.06, -ANNEX_WALL), (ANNEX_WALL, 1.0, ANNEX_WALL), 'Paint')
    # Concrete base course, standing out a little
    box(scene, 'Base', (-ANNEX_WALL - 0.035, 0.0, -ANNEX_WALL - 0.035), (ANNEX_WALL + 0.035, 0.12, ANNEX_WALL + 0.035), 'Stone')
    # Cornice: a band around the wall (not on it), a row of teeth below it
    band(scene, 'Cornice', ANNEX_WALL, ANNEX_WALL + 0.055, 0.935, 1.0, 'PaintDark')
    for i, side in enumerate(SIDES):
        is_front = 0 == i
        taken = quoins_on(scene, f'Quoin{i}', side, ANNEX_WALL, is_long_first=i < 2)
        if not is_front:
            taken.append(window_on(scene, f'Window{i}', side, ANNEX_WALL, 0.0, ANNEX_WINDOW))
        for k in range(ANNEX_DENTILS):
            at = (k + 0.5) / ANNEX_DENTILS * 2.0 * ANNEX_WALL - ANNEX_WALL
            wall_box(scene, f'Dentil{i}{k}', side, at - 0.045, at + 0.045, 0.885, 0.935, ANNEX_WALL - 0.02, ANNEX_WALL + 0.035, 'PaintDark')
        # The plain front shows more of its bricks, next to a window only headers fit
        bricks_on(scene, f'Brick{i}', side, ANNEX_WALL, taken, 13 if is_front else 8, rng)
    trim_height(scene, 1.0)


TILE_EAVE = Vector((1.13, 0.06))                      # (x, y) of the tiled slope: lower edge
TILE_RIDGE = Vector((0.0, 0.87))                      # and upper edge, the cap tiles lie on it
TILE_COURSES = 7                                      # courses of tiles per slope
TILES = 10                                            # tiles per course
TILE_REACH = 1.1                                      # half length of a course along the ridge
TILE_THICK = 0.05                                     # thickness of a tile
TILE_LIFT = 0.045                                     # how far the lower end of a tile stands off the slope: the step
ROOF_WALL = 0.98                                      # half width of the gable wall, as wide as the brick room below
ROOF_GABLE = 1.0                                      # z of the gable faces
ROOF_SINK = 0.03                                      # the roof body ends this far below the tiled slope
CAP_TILES = 7                                         # cap tiles along the ridge
DORMER_Z = 0.4                                        # middle of the dormer along the ridge
DORMER_HALF = 0.23                                    # half width of its walls
DORMER_FACE = 0.74                                    # x of its front wall
DORMER_EAVE, DORMER_PEAK = 0.56, 0.69                 # heights of its walls and of its little ridge
CHIMNEY = (-0.42, -0.45)                              # (x, z) of the middle of the chimney
CHIMNEY_HALF = 0.125                                  # half width of its shaft


def roof_slope(x):
    """Height of the tiled slope at the distance x from the ridge."""
    return TILE_RIDGE.y + (TILE_EAVE.y - TILE_RIDGE.y) * abs(x) / TILE_EAVE.x


def build_tile_roof(scene):
    # Body below the tiles: closed gable walls, as wide as the wall the roof stands on
    eave, peak = roof_slope(ROOF_WALL) - ROOF_SINK, roof_slope(0.0) - ROOF_SINK
    body = [G(-ROOF_WALL, 0.0, -ROOF_GABLE), G(ROOF_WALL, 0.0, -ROOF_GABLE), G(ROOF_WALL, eave, -ROOF_GABLE),
            G(0.0, peak, -ROOF_GABLE), G(-ROOF_WALL, eave, -ROOF_GABLE)]
    plate(scene, 'Body', body, G(0.0, 0.0, 2.0 * ROOF_GABLE), 'PaintDark')
    # The slope in (x, y): from the eave up to the ridge
    along = (TILE_RIDGE - TILE_EAVE).normalized()
    outward = Vector((along.y, -along.x))
    step = 2.0 * TILE_REACH / TILES
    for course in range(TILE_COURSES):
        # Every course lies with its lower end on the upper end of the one below
        low = TILE_EAVE.lerp(TILE_RIDGE, course / TILE_COURSES) + outward * TILE_LIFT
        high = TILE_EAVE.lerp(TILE_RIDGE, min(1.0, (course + 1) / TILE_COURSES + 0.05))
        # Every other course is shifted by half a tile
        if 0 == course % 2:
            cuts = [-TILE_REACH + step * i for i in range(TILES + 1)]
        else:
            cuts = [-TILE_REACH] + [-TILE_REACH + step * (i + 0.5) for i in range(TILES)] + [TILE_REACH]
        for side in (1, -1):
            for i, (near, far) in enumerate(zip(cuts, cuts[1:])):
                z = (near + far) / 2
                beam(scene, f'Tile{course}{side}{i}', G(side * low.x, low.y, z), G(side * high.x, high.y, z), far - near - 0.022, TILE_THICK, 'Paint')
    # Ridge: cap tiles, each with its wide end over the narrow end of the next
    cap = 2.0 * (TILE_REACH + 0.02) / CAP_TILES
    for i in range(CAP_TILES):
        z = -TILE_REACH - 0.02 + cap * i
        pole(scene, f'Cap{i}', [G(0.0, TILE_RIDGE.y + 0.005, z), G(0.0, TILE_RIDGE.y + 0.005, z + cap + 0.02)], [0.085, 0.062], 'Paint', sides=6, is_smooth=False)
    for end in (-1, 1):
        # Gable: a small window under the ridge
        window_on(scene, f'GableWindow{end}', (0, end), ROOF_GABLE, 0.0, 0.24, low=0.27, high=0.55)
        # Barge boards over the tile ends, meeting at the ridge
        for side in (1, -1):
            foot, top = TILE_EAVE - along * 0.03 + outward * 0.02, TILE_RIDGE + along * 0.03 + outward * 0.02
            beam(scene, f'Barge{end}{side}', G(side * foot.x, foot.y, end * (TILE_REACH + 0.015)), G(side * top.x, top.y, end * (TILE_REACH + 0.015)),
                 0.05, 0.11, 'PaintDark')
    # Gutters hanging under both eaves, a stop at each end
    for side in (1, -1):
        near, far = sorted((side * (TILE_EAVE.x + 0.005), side * (TILE_EAVE.x + 0.08)))
        box(scene, f'Gutter{side}', (near, 0.0, -TILE_REACH - 0.04), (far, 0.065, TILE_REACH + 0.04), 'Metal')
    # Dormer on the front slope: walls with their own gable, a window, two tiled wings
    back = TILE_EAVE.x * (TILE_RIDGE.y - DORMER_PEAK) / (TILE_RIDGE.y - TILE_EAVE.y) - 0.04    # where its ridge runs into the slope
    walls = [(-DORMER_HALF, 0.3), (DORMER_HALF, 0.3), (DORMER_HALF, DORMER_EAVE), (0.0, DORMER_PEAK), (-DORMER_HALF, DORMER_EAVE)]
    plate(scene, 'Dormer', [G(back, y, DORMER_Z + z) for z, y in walls], G(DORMER_FACE - back, 0.0, 0.0), 'PaintDark')
    window_on(scene, 'DormerWindow', (1, 0), DORMER_FACE, DORMER_Z, 0.2, low=0.4, high=0.57, has_lintel=False)
    for side in (-1, 1):
        wing = [(side * (DORMER_HALF + 0.07), DORMER_EAVE - 0.045), (0.0, DORMER_PEAK - 0.005), (0.0, DORMER_PEAK + 0.05),
                (side * (DORMER_HALF + 0.07), DORMER_EAVE + 0.01)]
        plate(scene, f'DormerWing{side}', [G(back - 0.03, y, DORMER_Z + z) for z, y in wing], G(DORMER_FACE + 0.06 - back + 0.03, 0.0, 0.0), 'Paint')
    pole(scene, 'DormerCap', [G(back - 0.04, DORMER_PEAK + 0.04, DORMER_Z), G(DORMER_FACE + 0.075, DORMER_PEAK + 0.04, DORMER_Z)], [0.045, 0.045], 'Paint',
         sides=6, is_smooth=False)
    # Chimney on the back slope: a brick shaft with two proud courses, a concrete cap, a clay pot
    x, z = CHIMNEY
    box(scene, 'Chimney', (x - CHIMNEY_HALF, 0.4, z - CHIMNEY_HALF), (x + CHIMNEY_HALF, 0.9, z + CHIMNEY_HALF), 'PaintDark')
    for i, y in enumerate((0.72, 0.81)):
        band_at = CHIMNEY_HALF + 0.014
        box(scene, f'ChimneyCourse{i}', (x - band_at, y, z - band_at), (x + band_at, y + 0.05, z + band_at), 'Paint')
    box(scene, 'ChimneyCap', (x - CHIMNEY_HALF - 0.035, 0.9, z - CHIMNEY_HALF - 0.035), (x + CHIMNEY_HALF + 0.035, 0.94, z + CHIMNEY_HALF + 0.035), 'Stone')
    pole(scene, 'ChimneyPot', [G(x, 0.94, z), G(x, 1.0, z)], [0.07, 0.058], 'Clay', sides=8, is_level=True)
    pole(scene, 'ChimneyFlue', [G(x, 0.99, z), G(x, 1.0, z)], [0.04, 0.04], 'Door', sides=8, is_level=True)
    trim_height(scene, 1.0)


# ===========================================================================
# ===============================   ROOFTOP   ===============================
# ===========================================================================
#
#   build_solar_panels   Two rows of tilted solar panels for a flat roof, looking to the
#                        front and up: dark glass with a grid of cells in a metal frame, on
#                        a metal stand (sloping rails, rear legs, a cross brace) that stands
#                        on two concrete sleepers. A switch box hangs behind the rear row.
#
#   Flat below: the sleepers lie on the roof.
#

PANEL_TILT = math.radians(30.0)                       # how steep the panels stand
PANEL_LENGTH = 0.5                                    # a panel from its lower to its upper edge
PANEL_HALF = 0.46                                     # half width of a row along Z
PANEL_FEET = (0.465, -0.035)                          # x of the lower edge of the two rows
PANEL_LOW = 0.1                                       # height of the lower edge
PANEL_GLASS = 0.03                                    # thickness of the glass
PANEL_CELLS = (6, 4)                                  # cells of one row across and up the slope
SLEEPERS = (-0.3, 0.3)                                # z of the two concrete sleepers and of the stand on them


def build_solar_panels(scene):
    # Concrete sleepers along X, flat on the roof
    for i, z in enumerate(SLEEPERS):
        box(scene, f'Sleeper{i}', (-0.48, 0.0, z - 0.07), (0.48, 0.055, z + 0.07), 'Stone')
    # The slope of a panel in (x, y): up towards the back, its face looks along normal
    up = Vector((-math.cos(PANEL_TILT), math.sin(PANEL_TILT)))
    normal = Vector((math.sin(PANEL_TILT), math.cos(PANEL_TILT)))
    for row, foot_x in enumerate(PANEL_FEET):
        foot = Vector((foot_x, PANEL_LOW))

        def on_panel(share, off, z, foot=foot):
            """A point on the panel: share of its length up the slope, off its middle plane, at z."""
            point = foot + up * (PANEL_LENGTH * share) + normal * off
            return G(point.x, point.y, z)

        # Glass: one dark slab
        beam(scene, f'Panel{row}', on_panel(0.0, 0.0, 0.0), on_panel(1.0, 0.0, 0.0), 2.0 * PANEL_HALF, PANEL_GLASS, 'Solar')
        # Frame: a rail along the lower and the upper edge, one down each flank and the middle
        for name, share in (('Low', 0.0), ('High', 1.0)):
            beam(scene, f'Frame{name}{row}', on_panel(share, 0.0, -PANEL_HALF - 0.02), on_panel(share, 0.0, PANEL_HALF + 0.02), 0.045, 0.055, 'Metal')
        for i, z in enumerate((-PANEL_HALF, 0.0, PANEL_HALF)):
            beam(scene, f'FrameSide{row}{i}', on_panel(0.0, 0.0, z), on_panel(1.0, 0.0, z), 0.04, PANEL_GLASS + 0.02, 'Metal')
        # Grid of cells: thin bars lying on the glass
        across, rows = PANEL_CELLS
        for i in range(1, across):
            if across / 2 == i:
                continue                              # the middle rail of the frame runs there
            z = -PANEL_HALF + 2.0 * PANEL_HALF * i / across
            beam(scene, f'GridUp{row}{i}', on_panel(0.0, PANEL_GLASS / 2, z), on_panel(1.0, PANEL_GLASS / 2, z), 0.02, 0.012, 'Metal')
        for i in range(1, rows):
            beam(scene, f'GridAcross{row}{i}', on_panel(i / rows, PANEL_GLASS / 2, -PANEL_HALF), on_panel(i / rows, PANEL_GLASS / 2, PANEL_HALF),
                 0.02, 0.012, 'Metal')
        # Stand on each sleeper: a rail under the glass, a short front foot, a rear leg
        rear = on_panel(0.9, 0.0, 0.0)
        for i, z in enumerate(SLEEPERS):
            beam(scene, f'Rail{row}{i}', on_panel(0.0, -0.035, z), on_panel(1.0, -0.035, z), 0.04, 0.04, 'Metal')
            box(scene, f'Foot{row}{i}', (foot_x - 0.075, 0.055, z - 0.035), (foot_x - 0.015, PANEL_LOW, z + 0.035), 'Metal')
            box(scene, f'Leg{row}{i}', (rear.x - 0.02, 0.055, z - 0.02), (rear.x + 0.02, rear.z - 0.03, z + 0.02), 'Metal')
        # Cross brace between the two rear legs
        pole(scene, f'Brace{row}', [G(rear.x, 0.075, SLEEPERS[0]), G(rear.x, rear.z - 0.07, SLEEPERS[1])], [0.016, 0.016], 'Metal', sides=4)
    # Switch box on the rear legs of the back row
    rear_x = PANEL_FEET[1] + up.x * PANEL_LENGTH * 0.9
    box(scene, 'SwitchBox', (rear_x - 0.045, 0.16, -0.11), (rear_x + 0.03, 0.3, 0.11), 'PaintDark')
    trim_height(scene, 0.5)


# ===========================================================================
# ===============================   FACADE   ================================
# ===========================================================================
#
#   build_balcony       A balcony leaning on the wall behind it (-X): a concrete slab with a
#                       painted edge, metal posts and handrail, painted panels between them,
#                       a flower box with blossoms on the front rail.
#   build_fire_escape   A steel fire escape leaning on the wall behind it: a landing grate
#                       at the top with a hatch, a steep stair down from the hatch, columns
#                       and guard rails around the foot, braces to the wall.
#
#   Both lean on the wall like build_entrance of models/modern.py and are flat below. Fire
#   escapes stack: the stair of the upper one stands on the landing of the lower one, the
#   guard rails around its foot are the railing of that landing.
#

BALCONY_SLAB = 0.07                                   # thickness of the concrete slab
BALCONY_EDGE = 0.11                                   # height of the painted edge around it
BALCONY_RAIL = 0.4                                    # underside of the handrail
BALCONY_POST = 0.02                                   # half width of a post
PLANTER_HALF = 0.33                                   # half length of the flower box


def build_balcony(scene):
    # Slab against the wall, a painted edge around its three free sides
    box(scene, 'Slab', (-0.25, 0.0, -0.47), (0.22, BALCONY_SLAB, 0.47), 'Stone')
    box(scene, 'EdgeFront', (0.21, 0.0, -0.5), (0.25, BALCONY_EDGE, 0.5), 'Paint')
    for side in (-1, 1):
        near, far = sorted((side * 0.46, side * 0.5))
        box(scene, f'EdgeSide{side}', (-0.25, 0.0, near), (0.21, BALCONY_EDGE, far), 'Paint')
    # Posts: one on each front corner, one at the wall on each flank, one in the middle of the front
    for i, (x, z) in enumerate(((0.225, -0.475), (0.225, 0.475), (-0.225, -0.475), (-0.225, 0.475), (0.225, 0.0))):
        box(scene, f'Post{i}', (x - BALCONY_POST, BALCONY_EDGE, z - BALCONY_POST), (x + BALCONY_POST, BALCONY_RAIL, z + BALCONY_POST), 'Metal')
    # Handrail lying on the posts
    box(scene, 'RailFront', (0.195, BALCONY_RAIL, -0.5), (0.25, BALCONY_RAIL + 0.04, 0.5), 'Metal')
    for side in (-1, 1):
        near, far = sorted((side * 0.445, side * 0.5))
        box(scene, f'RailSide{side}', (-0.25, BALCONY_RAIL, near), (0.195, BALCONY_RAIL + 0.04, far), 'Metal')
    # Panels between the posts, each on a dark bottom rail
    panels = [('Front0', (0.213, -0.455), (0.237, -0.02)), ('Front1', (0.213, 0.02), (0.237, 0.455)),
              ('Side0', (-0.205, -0.487), (0.205, -0.463)), ('Side1', (-0.205, 0.463), (0.205, 0.487))]
    for name, (low_x, low_z), (high_x, high_z) in panels:
        box(scene, f'Panel{name}', (low_x, 0.17, low_z), (high_x, 0.385, high_z), 'Paint')
        box(scene, f'PanelRail{name}', (low_x - 0.006, 0.14, low_z - 0.006), (high_x + 0.006, 0.17, high_z + 0.006), 'PaintDark')
    # Flower box riding on the front rail: leaves spilling over, blossoms in two colors
    box(scene, 'Planter', (0.12, 0.345, -PLANTER_HALF), (0.285, 0.445, PLANTER_HALF), 'PaintDark')
    box(scene, 'PlanterRim', (0.108, 0.425, -PLANTER_HALF - 0.012), (0.297, 0.452, PLANTER_HALF + 0.012), 'Paint')
    for i, z in enumerate((-0.21, 0.0, 0.21)):
        ellipsoid(scene, f'Leaves{i}', G(0.2025, 0.452, z), (0.085, 0.125, 0.038), 'Leaf', subdivisions=2)
    blossoms = ((0.23, -0.25, 'FruitRed'), (0.17, -0.13, 'FruitGold'), (0.24, -0.02, 'FruitRed'), (0.17, 0.1, 'FruitRed'), (0.235, 0.2, 'FruitGold'),
                (0.18, 0.28, 'FruitRed'))
    for i, (x, z, material_name) in enumerate(blossoms):
        fruit(scene, f'Blossom{i}', (x, 0.47, z), 0.03, material_name)
    trim_height(scene, 0.5)


ESCAPE_OUT = 0.23                                     # x of the columns and guard rails on the outer edge
ESCAPE_DECK = 0.93                                    # underside of the landing frame, its top is the top of the box
ESCAPE_HATCH = -0.1                                   # z where the hatch in the landing ends, the stair comes up behind it
STAIR_FOOT = Vector((0.33, 0.0))                      # (z, y) of the stair: its foot on the landing below
STAIR_HEAD = Vector((-0.45, 0.97))                    # and its head in the hatch
STAIR_INNER, STAIR_OUTER = 0.035, 0.195               # x of its two stringers, the walkway to the hatch runs along the wall
TREADS = 8
GRATE_BARS = 13                                       # bars of the landing grate along Z
RAIL_RADIUS = 0.016                                   # every rail is at least 0.03 thick to read from afar
GUARD_HEIGHTS = (0.22, 0.42)                          # heights of the two guard rails around the foot


def build_fire_escape(scene):
    # Landing frame: a channel around the edge, two more around the hatch
    box(scene, 'FrameOuter', (0.21, ESCAPE_DECK, -0.5), (0.25, 1.0, 0.5), 'Metal')
    box(scene, 'FrameWall', (-0.25, ESCAPE_DECK, -0.5), (-0.21, 1.0, 0.5), 'Metal')
    for side in (-1, 1):
        near, far = sorted((side * 0.46, side * 0.5))
        box(scene, f'FrameEnd{side}', (-0.21, ESCAPE_DECK, near), (0.21, 1.0, far), 'Metal')
    box(scene, 'HatchSide', (-0.02, ESCAPE_DECK, -0.46), (0.015, 1.0, ESCAPE_HATCH), 'Metal')
    box(scene, 'HatchEnd', (0.015, ESCAPE_DECK, ESCAPE_HATCH - 0.035), (0.21, 1.0, ESCAPE_HATCH), 'Metal')
    # Grate: dark bars across the frame, short ones along the wall next to the hatch
    for i in range(GRATE_BARS):
        z = -0.46 + 0.92 * (i + 0.5) / GRATE_BARS
        is_at_hatch = z < ESCAPE_HATCH - 0.035
        box(scene, f'Grate{i}', (-0.22, 0.95, z - 0.02), (-0.01 if is_at_hatch else 0.22, 0.985, z + 0.02), 'PaintDark')
    # Stair: two stringers from the landing below up into the hatch, dark treads between them
    for i, x in enumerate((STAIR_INNER, STAIR_OUTER)):
        beam(scene, f'Stringer{i}', G(x, STAIR_FOOT.y, STAIR_FOOT.x), G(x, STAIR_HEAD.y, STAIR_HEAD.x), 0.032, 0.085, 'Metal')
    for i in range(TREADS):
        at = STAIR_FOOT.lerp(STAIR_HEAD, (i + 0.6) / TREADS)
        box(scene, f'Tread{i}', (STAIR_INNER, at.y - 0.016, at.x - 0.055), (STAIR_OUTER, at.y + 0.016, at.x + 0.055), 'PaintDark')
    # Handrail over each stringer on three posts, ending under the landing
    lift = Vector((0.0, 0.26))
    low, high = STAIR_FOOT + lift, STAIR_FOOT.lerp(STAIR_HEAD, (ESCAPE_DECK - lift.y) / STAIR_HEAD.y) + lift
    for k, x in enumerate((STAIR_INNER, STAIR_OUTER)):
        pole(scene, f'Handrail{k}', [G(x, low.y, low.x), G(x, high.y, high.x)], [RAIL_RADIUS, RAIL_RADIUS], 'Metal', sides=5)
        for i, share in enumerate((0.04, 0.5, 0.96)):
            top = low.lerp(high, share)
            pole(scene, f'HandrailPost{k}{i}', [G(x, top.y - lift.y, top.x), G(x, top.y, top.x)], [RAIL_RADIUS, RAIL_RADIUS], 'Metal', sides=5, is_level=True)
    # Columns under the outer corners of the landing, standing on the landing below
    for side in (-1, 1):
        box(scene, f'Column{side}', (ESCAPE_OUT - 0.02, 0.0, side * 0.48 - 0.02), (ESCAPE_OUT + 0.02, ESCAPE_DECK, side * 0.48 + 0.02), 'Metal')
    # Guard rails around the foot: along the outer edge and across both ends to the wall
    for i, y in enumerate(GUARD_HEIGHTS):
        pole(scene, f'Guard{i}', [G(ESCAPE_OUT, y, -0.48), G(ESCAPE_OUT, y, 0.48)], [RAIL_RADIUS, RAIL_RADIUS], 'Metal', sides=5)
        for side in (-1, 1):
            pole(scene, f'GuardEnd{i}{side}', [G(-0.25, y, side * 0.48), G(ESCAPE_OUT, y, side * 0.48)], [RAIL_RADIUS, RAIL_RADIUS], 'Metal', sides=5)
    for i, z in enumerate((-0.29, -0.1, 0.1, 0.29)):
        pole(scene, f'GuardPost{i}', [G(ESCAPE_OUT, 0.0, z), G(ESCAPE_OUT, GUARD_HEIGHTS[1], z)], [RAIL_RADIUS, RAIL_RADIUS], 'Metal', sides=5, is_level=True)
    # Braces from the wall up to the outer edge of the landing, a dark wall plate under each
    for side in (-1, 1):
        z = side * 0.48
        pole(scene, f'Brace{side}', [G(-0.24, 0.6, z), G(ESCAPE_OUT - 0.02, ESCAPE_DECK + 0.01, z)], [0.02, 0.02], 'Metal', sides=5)
        box(scene, f'WallPlate{side}', (-0.25, 0.53, z - 0.02), (-0.225, 0.67, z + 0.02), 'PaintDark')
    trim_height(scene, 1.0)
