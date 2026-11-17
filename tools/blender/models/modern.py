# NOTE:
#   Building blocks of the modern town (assets/models/building/): one storey of a brick
#   house, one storey of a glass high-rise, a flat roof deck and a glazed entrance.
#   Same frame as models/buildings.py: +Y up, +X is the front of a block, the origin is the
#   center of its footprint on the ground, one grid cell is 0.5 units, a block stays inside
#   its box and ends flat where the next one stacks. Cleaner than the village blocks:
#   straight edges, no wobble. Materials Paint (the wall the player repaints), PaintDark
#   (its trim), Stone (concrete), Glass, Metal, Light (glows, objects named Light...) and
#   Door (dark openings). Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module modern -Call 'build_brick_room(scene)' -Name brickroom

#
#   Chapters:
#     HELPERS       Straight boxes, bands around a footprint and boxes lying on a wall.
#     TOWN BLOCKS   Brick room, glass floor (both 4 x 2 x 4 cells), roof deck (4 x 1 x 4)
#                   and entrance (1 x 2 x 2).
#

import math
import random

from models.buildings import beam, lathe, plate, pole, trim_height
from models.helpers import G

WALL = 0.98                                           # outer face of every wall on a 4 x 4 footprint
SIDES = ((1, 0), (-1, 0), (0, 1), (0, -1))            # the way the four walls look (x, z), the first is the front


# ===========================================================================
# ===============================   HELPERS   ===============================
# ===========================================================================
#
#   box        A straight box between two corners.
#   band       A square ring around the middle: cornices, parapets, copings.
#   wall_box   A box lying on one of the four walls, measured along and off the wall.
#
#   All three take Godot coordinates (x, y, z) and build on beam of models/buildings.py.
#

def box(scene, name, low, high, material_name):
    """A box from the corner low to the corner high, both Godot (x, y, z)."""
    middle_x, middle_z = (low[0] + high[0]) / 2, (low[2] + high[2]) / 2
    # An upright beam has its width along Z and its height along X
    return beam(scene, name, G(middle_x, low[1], middle_z), G(middle_x, high[1], middle_z),
                high[2] - low[2], high[0] - low[0], material_name)


def band(scene, name, inner, outer, low, high, material_name):
    """
    A square ring from the half width inner to outer between the heights low and high.
    Four boxes that touch without overlapping: nothing covers the area inside inner.
    """
    for side in (-1, 1):
        near, far = sorted((side * inner, side * outer))
        box(scene, f'{name}X{side}', (near, low, -outer), (far, high, outer), material_name)
        box(scene, f'{name}Z{side}', (-inner, low, near), (inner, high, far), material_name)


def wall_box(scene, name, side, start, end, low, high, inner, outer, material_name):
    """
    A box on the wall that looks along side (x, z): from start to end along the wall,
    from low to high, from inner to outer away from the middle of the block.
    """
    out_x, out_z = side
    across_x, across_z = -out_z, out_x
    xs = sorted((out_x * inner + across_x * start, out_x * outer + across_x * end))
    zs = sorted((out_z * inner + across_z * start, out_z * outer + across_z * end))
    return box(scene, name, (xs[0], low, zs[0]), (xs[1], high, zs[1]), material_name)


# ===========================================================================
# =============================   TOWN BLOCKS   =============================
# ===========================================================================
#
#   brick_window       A tall window on a brick wall: pane, frame, concrete sill and lintel.
#   proud_bricks       Scattered bricks standing proud of a wall, clear of its windows.
#   quoins             Corner blocks at both ends of a wall, locking around the corner.
#   build_brick_room   One storey of a brick house: concrete base course, brick walls with
#                      proud bricks and corner blocks, two tall windows per side (the front
#                      leaves its middle to the entrance), a cornice on a row of teeth.
#   chamfered          The outline of a square with its corners cut off.
#   build_glass_floor  One storey of a glass high-rise: floor slabs below and above, a
#                      curtain wall of panes over a steel spandrel between slim mullions,
#                      chamfered corner piers, a few lit offices.
#   build_roof_deck    A parapet with a concrete coping around a lower gravel deck: air-
#                      conditioning unit with two fans and a pipe on sleepers, pitched
#                      skylight, vent pipe, mushroom vent, hatch.
#   build_entrance     A glass double door in a steel frame on a concrete landing with a
#                      step and handrails, a cantilevered canopy on two tie rods with a
#                      lit sign and two lamps.
#
#   Room and floor end flat at y = 1 and stack onto themselves and each other, the deck
#   is flat below. The entrance leans on the wall behind it (-X) like build_door.
#

WINDOW_LOW, WINDOW_HIGH = 0.24, 0.78                  # the pane of a brick room window, tall like a sash window
WINDOW_FRAME = 0.04                                   # width of its frame
BRICK_LENGTH, BRICK_HEIGHT = 0.21, 0.055              # one proud brick, big enough to read from far away
BRICK_HEADER = 0.095                                  # a brick showing its short end
BRICK_PITCH = 0.07                                    # height of a course: brick plus joint
BRICK_FIRST = 0.135                                   # underside of the lowest course, on the base course
BRICK_COURSES = 11                                    # courses between base course and cornice
QUOIN_LONG, QUOIN_SHORT = 0.11, 0.07                  # lengths of the corner blocks, alternating up the corner
QUOINS = 5                                            # corner blocks per corner and wall
DENTILS = 10                                          # teeth under the cornice per wall


def brick_window(scene, name, side, center, width):
    """A window around center along the wall. Returns the area (start, end, low, high) it takes."""
    start, end = center - width / 2, center + width / 2
    wall_box(scene, f'{name}Pane', side, start, end, WINDOW_LOW, WINDOW_HIGH, WALL - 0.03, WALL + 0.008, 'Glass')
    # Frame: jambs, head and bottom rail around the pane
    for i, edge in enumerate((start - WINDOW_FRAME, end)):
        wall_box(scene, f'{name}Jamb{i}', side, edge, edge + WINDOW_FRAME, WINDOW_LOW, WINDOW_HIGH, WALL - 0.03, WALL + 0.032, 'PaintDark')
    wall_box(scene, f'{name}Head', side, start, end, WINDOW_HIGH - WINDOW_FRAME, WINDOW_HIGH, WALL - 0.03, WALL + 0.032, 'PaintDark')
    wall_box(scene, f'{name}Rail', side, start, end, WINDOW_LOW, WINDOW_LOW + WINDOW_FRAME, WALL - 0.03, WALL + 0.032, 'PaintDark')
    # Glazing bars: one upright, one across in the upper third (a sash window)
    wall_box(scene, f'{name}Post', side, center - 0.014, center + 0.014, WINDOW_LOW + WINDOW_FRAME, WINDOW_HIGH - WINDOW_FRAME,
             WALL - 0.03, WALL + 0.022, 'PaintDark')
    bar = WINDOW_LOW + (WINDOW_HIGH - WINDOW_LOW) * 0.62
    wall_box(scene, f'{name}Bar', side, start, end, bar - 0.014, bar + 0.014, WALL - 0.03, WALL + 0.02, 'PaintDark')
    # Concrete lintel above and a sill that sticks out further below
    reach = width / 2 + WINDOW_FRAME
    wall_box(scene, f'{name}Lintel', side, center - reach - 0.035, center + reach + 0.035, WINDOW_HIGH, WINDOW_HIGH + 0.075,
             WALL - 0.03, WALL + 0.04, 'Stone')
    wall_box(scene, f'{name}Sill', side, center - reach - 0.03, center + reach + 0.03, WINDOW_LOW - 0.05, WINDOW_LOW,
             WALL - 0.03, WALL + 0.065, 'Stone')
    return (center - reach - 0.035, center + reach + 0.035, WINDOW_LOW - 0.05, WINDOW_HIGH + 0.075)


def proud_bricks(scene, name, side, taken, count, rng):
    """
    count bricks on the wall, each on its course and shifted by half a brick against the
    course below (running bond). taken holds the areas (start, end, low, high) of the
    windows, a brick keeps clear of them and of the bricks before it.
    """
    half = (BRICK_LENGTH + 0.015) / 2                 # half a brick plus half a joint: the step of the bond
    steps = int((2.0 * WALL - 0.1 - BRICK_LENGTH) / half)
    areas = list(taken)
    placed = 0
    for _ in range(count * 30):                       # tries, most land on a window or a brick
        if count == placed:
            break
        course = rng.randrange(BRICK_COURSES)
        step = rng.randrange(steps // 2) * 2 + course % 2
        start, low = -WALL + 0.05 + step * half, BRICK_FIRST + course * BRICK_PITCH
        # Two of three are stretchers, the rest headers: bricks laid across the wall, half as long
        length = BRICK_LENGTH if rng.random() < 0.66 else BRICK_HEADER
        end, high = start + length, low + BRICK_HEIGHT
        if any(start < area[1] + 0.012 and area[0] - 0.012 < end and low < area[3] + 0.012 and area[2] - 0.012 < high for area in areas):
            continue
        wall_box(scene, f'{name}{placed}', side, start, end, low, high, WALL - 0.02, WALL + 0.014, 'PaintDark')
        areas.append((start, end, low, high))
        placed += 1


def quoins(scene, name, side, is_long_first):
    """
    Corner blocks at both ends of a wall, long and short in turn. The neighbour wall starts
    with the other length, so the blocks lock around the corner. Returns the area they take.
    """
    pitch = (0.89 - BRICK_FIRST) / QUOINS
    for k in range(QUOINS):
        length = QUOIN_LONG if is_long_first == (0 == k % 2) else QUOIN_SHORT
        low = BRICK_FIRST + k * pitch
        for end in (-1, 1):
            start, stop = sorted((end * (WALL + 0.016), end * (WALL - length)))
            wall_box(scene, f'{name}{end}{k}', side, start, stop, low, low + pitch - 0.03, WALL - 0.02, WALL + 0.016, 'PaintDark')
    return [(end * WALL - QUOIN_LONG, end * WALL + QUOIN_LONG, 0.0, 1.0) for end in (-1, 1)]


def build_brick_room(scene):
    rng = random.Random(401)
    # Walls: one closed brick box, its top is the flat top of the room
    box(scene, 'Walls', (-WALL, 0.06, -WALL), (WALL, 1.0, WALL), 'Paint')
    # Concrete base course, standing out a little
    box(scene, 'Base', (-WALL - 0.035, 0.0, -WALL - 0.035), (WALL + 0.035, 0.12, WALL + 0.035), 'Stone')
    # Cornice: a band around the wall (not on it), a row of teeth below it
    band(scene, 'Cornice', WALL, WALL + 0.055, 0.935, 1.0, 'PaintDark')
    for i, side in enumerate(SIDES):
        is_front = 0 == i
        # The entrance block (1 cell wide on each side of the middle) stands before the front
        centers, width = ((-0.68, 0.68), 0.22) if is_front else ((-0.45, 0.45), 0.3)
        taken = [brick_window(scene, f'Window{i}{k}', side, center, width) for k, center in enumerate(centers)]
        taken += quoins(scene, f'Quoin{i}', side, is_long_first=i < 2)
        for k in range(DENTILS):
            at = (k + 0.5) / DENTILS * 2.0 * WALL - WALL
            wall_box(scene, f'Dentil{i}{k}', side, at - 0.045, at + 0.045, 0.885, 0.935, WALL - 0.02, WALL + 0.035, 'PaintDark')
        if is_front:
            taken.append((-0.5, 0.5, 0.0, 1.0))
        proud_bricks(scene, f'Brick{i}', side, taken, 14, rng)
    trim_height(scene, 1.0)


GLASS_FACE = 0.94                                     # the panes of the curtain wall, set back behind the slabs
SLAB = 0.1                                            # height of the floor slab bands at both edges
CHAMFER = 0.06                                        # how much of every corner of the high-rise is cut off
PIER = 0.14                                           # width of a corner pier along each wall
PANES = 6                                             # panes per side between the corner piers
PANE_RAIL = 0.3                                       # height of the rail between the steel spandrel and the pane


def chamfered(reach, y):
    """The outline of a square of half width reach at height y with its four corners cut off."""
    cut = reach - CHAMFER
    corners = [(reach, cut), (cut, reach), (-cut, reach), (-reach, cut), (-reach, -cut), (-cut, -reach), (cut, -reach), (reach, -cut)]
    return [G(x, y, z) for x, z in corners]


def build_glass_floor(scene):
    rng = random.Random(402)
    # Floor slabs: the lower one carries the panes, the upper one is the flat top
    plate(scene, 'SlabLow', chamfered(WALL, 0.0), G(0.0, SLAB, 0.0), 'Paint')
    plate(scene, 'SlabHigh', chamfered(WALL, 1.0 - SLAB), G(0.0, SLAB, 0.0), 'Paint')
    # The glass: one closed box between the slabs
    box(scene, 'Glass', (-GLASS_FACE, SLAB, -GLASS_FACE), (GLASS_FACE, 1.0 - SLAB, GLASS_FACE), 'Glass')
    # Corner piers: five-sided posts with the cut corner of the slabs
    inner = WALL - PIER
    for i, (sx, sz) in enumerate(((1, 1), (-1, 1), (-1, -1), (1, -1))):
        outline = [(WALL, inner), (WALL, WALL - CHAMFER), (WALL - CHAMFER, WALL), (inner, WALL), (inner, inner)]
        plate(scene, f'Pier{i}', [G(sx * x, SLAB, sz * z) for x, z in outline], G(0.0, 1.0 - 2.0 * SLAB, 0.0), 'Paint')
    pane = 2.0 * inner / PANES
    for i, side in enumerate(SIDES):
        # Spandrel: a steel panel hiding the floor behind the glass, slim mullions, a rail across them
        wall_box(scene, f'Spandrel{i}', side, -inner, inner, SLAB, PANE_RAIL, GLASS_FACE - 0.02, GLASS_FACE + 0.012, 'Metal')
        for k in range(1, PANES):
            at = -inner + pane * k
            wall_box(scene, f'Mullion{i}{k}', side, at - 0.02, at + 0.02, SLAB, 1.0 - SLAB, GLASS_FACE - 0.02, WALL, 'Metal')
        wall_box(scene, f'Rail{i}', side, -inner, inner, PANE_RAIL - 0.018, PANE_RAIL + 0.018, GLASS_FACE - 0.02, WALL - 0.012, 'Metal')
        # Lit offices: one or two panes per side glow
        for k in rng.sample(range(PANES), rng.choice((1, 2))):
            start = -inner + pane * k + (0.0 if 0 == k else 0.02)
            end = -inner + pane * (k + 1) - (0.0 if PANES - 1 == k else 0.02)
            wall_box(scene, f'Light{i}{k}', side, start, end, PANE_RAIL + 0.018, 1.0 - SLAB, GLASS_FACE - 0.02, GLASS_FACE + 0.012, 'Light')
    trim_height(scene, 1.0)


DECK = 0.2                                            # height of the gravel
PARAPET = 0.29                                        # height of the parapet wall below its coping
PARAPET_INNER = 0.86                                  # inner face of the parapet wall
ROOF_TOP = 0.49                                       # nothing on the deck is higher: the box ends at 0.5


def build_roof_deck(scene):
    # Slab with a flat underside, the parapet wall around it, a coping lying on the wall
    box(scene, 'Slab', (-WALL, 0.0, -WALL), (WALL, DECK - 0.04, WALL), 'Paint')
    band(scene, 'Parapet', PARAPET_INNER, WALL, DECK - 0.04, PARAPET, 'Paint')
    band(scene, 'Coping', PARAPET_INNER - 0.03, WALL + 0.03, PARAPET, PARAPET + 0.05, 'Stone')
    box(scene, 'Gravel', (-PARAPET_INNER, DECK - 0.04, -PARAPET_INNER), (PARAPET_INNER, DECK, PARAPET_INNER), 'Stone')
    # Air-conditioning unit on two concrete curbs: a long casing, dark louvres on two faces
    for i, z in enumerate((-0.64, -0.3)):
        box(scene, f'Curb{i}', (-0.72, DECK, z - 0.04), (0.0, DECK + 0.05, z + 0.04), 'Stone')
    box(scene, 'Unit', (-0.74, DECK + 0.05, -0.7), (0.02, 0.44, -0.24), 'Metal')
    box(scene, 'LouvreFront', (0.02, DECK + 0.09, -0.66), (0.032, 0.4, -0.28), 'Door')
    box(scene, 'LouvreSide', (-0.7, DECK + 0.09, -0.24), (-0.3, 0.4, -0.228), 'Door')
    for k in range(4):
        y = DECK + 0.115 + 0.045 * k
        box(scene, f'SlatFront{k}', (0.02, y, -0.66), (0.044, y + 0.02, -0.28), 'Metal')
        box(scene, f'SlatSide{k}', (-0.7, y, -0.24), (-0.3, y + 0.02, -0.216), 'Metal')
    box(scene, 'UnitPanel', (-0.24, DECK + 0.1, -0.24), (-0.04, 0.39, -0.226), 'PaintDark')
    # Two fans in its top: a ring, the dark shaft, a hub and a grille of three bars
    for i, fan in enumerate(((-0.54, -0.47), (-0.18, -0.47))):
        lathe(scene, f'FanRing{i}', [(0.165, 0.44), (0.165, ROOF_TOP), (0.165, ROOF_TOP), (0.13, ROOF_TOP), (0.13, ROOF_TOP), (0.13, 0.44)], 'Metal',
              segments=12, center=fan, is_smooth=False)
        lathe(scene, f'FanDark{i}', [(0.13, 0.452), (0.0, 0.452)], 'Door', segments=12, center=fan)
        lathe(scene, f'FanHub{i}', [(0.04, 0.452), (0.04, 0.48), (0.04, 0.48), (0.0, 0.48)], 'Metal', segments=8, center=fan, is_smooth=False)
        for k in range(3):
            angle = k / 3 * math.pi
            reach_x, reach_z = math.cos(angle) * 0.13, math.sin(angle) * 0.13
            beam(scene, f'FanGrille{i}{k}', G(fan[0] - reach_x, 0.475, fan[1] - reach_z), G(fan[0] + reach_x, 0.475, fan[1] + reach_z), 0.028, 0.016, 'Metal')
    # Coolant pipe from the unit across the deck on two sleepers, bending down into the roof
    pipe = [G(0.02, 0.285, -0.4), G(0.27, 0.285, -0.4), G(0.31, 0.285, -0.36), G(0.31, 0.285, -0.08), G(0.31, DECK, -0.03)]
    pole(scene, 'Pipe', pipe, [0.032] * len(pipe), 'Metal', sides=6)
    box(scene, 'Sleeper0', (0.11, DECK, -0.46), (0.19, DECK + 0.055, -0.34), 'Stone')
    box(scene, 'Sleeper1', (0.25, DECK, -0.26), (0.37, DECK + 0.055, -0.18), 'Stone')
    # Skylight: a pitched glass roof on a curb, its ridge along Z, steel bars over the panes
    box(scene, 'SkylightCurb', (0.14, DECK, 0.08), (0.72, DECK + 0.07, 0.72), 'Paint')
    gable = [G(0.17, DECK + 0.07, 0.11), G(0.69, DECK + 0.07, 0.11), G(0.43, 0.43, 0.11)]
    plate(scene, 'Skylight', gable, G(0.0, 0.0, 0.58), 'Glass')
    for k, z in enumerate((0.11, 0.4, 0.69)):
        for side, eave in ((-1, 0.17), (1, 0.69)):
            beam(scene, f'SkylightBar{k}{side}', G(eave, DECK + 0.075, z), G(0.43, 0.435, z), 0.035, 0.022, 'Metal')
    beam(scene, 'SkylightRidge', G(0.43, 0.44, 0.09), G(0.43, 0.44, 0.71), 0.045, 0.03, 'Metal')
    # Vent pipe with a rain hat on a flange
    vent = (0.62, -0.5)
    lathe(scene, 'VentFlange', [(0.115, DECK), (0.115, DECK + 0.03), (0.115, DECK + 0.03), (0.0, DECK + 0.03)], 'Stone', segments=8, center=vent, is_smooth=False)
    pole(scene, 'VentPipe', [G(vent[0], DECK, vent[1]), G(vent[0], 0.4, vent[1])], [0.065, 0.065], 'Metal', sides=8, is_level=True)
    lathe(scene, 'VentHat', [(0.0, 0.385), (0.125, 0.385), (0.125, 0.385), (0.125, 0.405), (0.125, 0.405), (0.0, 0.485)], 'Metal',
          segments=8, center=vent, is_smooth=False)
    # Mushroom vent: a short stub under a round cap
    stub = (0.36, -0.68)
    pole(scene, 'StubPipe', [G(stub[0], DECK, stub[1]), G(stub[0], 0.3, stub[1])], [0.045, 0.045], 'Metal', sides=6, is_level=True)
    lathe(scene, 'StubCap', [(0.0, 0.285), (0.085, 0.285), (0.085, 0.285), (0.075, 0.32), (0.0, 0.345)], 'Metal', segments=8, center=stub)
    # Roof hatch: a curb, a lid hanging over it, two hinges and a handle
    box(scene, 'HatchCurb', (-0.64, DECK, 0.26), (-0.26, DECK + 0.1, 0.64), 'Paint')
    box(scene, 'HatchLid', (-0.67, DECK + 0.1, 0.23), (-0.23, DECK + 0.14, 0.67), 'Metal')
    for k, z in enumerate((0.33, 0.57)):
        box(scene, f'HatchHinge{k}', (-0.7, DECK + 0.07, z - 0.035), (-0.67, DECK + 0.15, z + 0.035), 'PaintDark')
    handle = [G(-0.33, DECK + 0.14, 0.37), G(-0.33, DECK + 0.185, 0.39), G(-0.33, DECK + 0.185, 0.51), G(-0.33, DECK + 0.14, 0.53)]
    pole(scene, 'HatchHandle', handle, [0.014] * len(handle), 'PaintDark', sides=5)
    trim_height(scene, 0.5)


ENTRANCE_BACK = -0.25                                 # the wall the entrance leans on
LANDING = 0.09                                        # height of the landing the door stands on
DOOR_HIGH = 0.76                                      # upper edge of the door leaves
FRAME_HIGH = 0.875                                    # upper edge of the steel frame, the canopy lies on it
DOOR_HALF = 0.33                                      # half width of the opening between the jambs


def build_entrance(scene):
    # Concrete landing against the wall, one lower step in front of it
    box(scene, 'Landing', (ENTRANCE_BACK, 0.0, -0.47), (0.03, LANDING, 0.47), 'Stone')
    box(scene, 'Step', (0.03, 0.0, -0.47), (0.25, LANDING / 2, 0.47), 'Stone')
    # Steel frame: two jambs, a head, a transom between door and fanlight
    for side in (-1, 1):
        near, far = sorted((side * DOOR_HALF, side * (DOOR_HALF + 0.06)))
        box(scene, f'Jamb{side}', (ENTRANCE_BACK, LANDING, near), (-0.17, FRAME_HIGH, far), 'Metal')
    box(scene, 'Head', (ENTRANCE_BACK, FRAME_HIGH - 0.045, -DOOR_HALF), (-0.17, FRAME_HIGH, DOOR_HALF), 'Metal')
    box(scene, 'Transom', (ENTRANCE_BACK, DOOR_HIGH, -DOOR_HALF), (-0.17, DOOR_HIGH + 0.03, DOOR_HALF), 'Metal')
    # Glass: the fanlight above the transom, the two leaves below
    box(scene, 'Fanlight', (ENTRANCE_BACK, DOOR_HIGH + 0.03, -DOOR_HALF), (-0.2, FRAME_HIGH - 0.045, DOOR_HALF), 'Glass')
    for side in (-1, 1):
        near, far = sorted((side * 0.008, side * DOOR_HALF))
        box(scene, f'Leaf{side}', (ENTRANCE_BACK, LANDING, near), (-0.205, DOOR_HIGH, far), 'Glass')
        # Leaf frame: a tall kick plate, a top rail, a stile where the leaves meet
        box(scene, f'Kick{side}', (ENTRANCE_BACK, LANDING, near), (-0.19, LANDING + 0.1, far), 'Metal')
        box(scene, f'TopRail{side}', (ENTRANCE_BACK, DOOR_HIGH - 0.04, near), (-0.19, DOOR_HIGH, far), 'Metal')
        stile = sorted((side * 0.008, side * 0.05))
        box(scene, f'Stile{side}', (ENTRANCE_BACK, LANDING + 0.1, stile[0]), (-0.19, DOOR_HIGH - 0.04, stile[1]), 'Metal')
        # Handle: an upright bar on two stand-offs
        z = side * 0.105
        pole(scene, f'Handle{side}', [G(-0.15, 0.33, z), G(-0.15, 0.6, z)], [0.02, 0.02], 'Metal', sides=6, is_level=True)
        for k, y in enumerate((0.37, 0.56)):
            box(scene, f'HandleMount{side}{k}', (-0.205, y - 0.014, z - 0.014), (-0.15, y + 0.014, z + 0.014), 'Metal')
    # Canopy: a flat slab on the frame, a darker fascia around its edge
    box(scene, 'Canopy', (ENTRANCE_BACK, FRAME_HIGH, -0.47), (0.2, FRAME_HIGH + 0.06, 0.47), 'Paint')
    box(scene, 'FasciaFront', (0.2, FRAME_HIGH - 0.03, -0.5), (0.235, FRAME_HIGH + 0.085, 0.5), 'PaintDark')
    for side in (-1, 1):
        near, far = sorted((side * 0.47, side * 0.5))
        box(scene, f'FasciaSide{side}', (ENTRANCE_BACK, FRAME_HIGH - 0.03, near), (0.2, FRAME_HIGH + 0.085, far), 'PaintDark')
        # Tie rod from the wall above down to the front of the canopy
        z = side * 0.36
        pole(scene, f'Rod{side}', [G(ENTRANCE_BACK + 0.012, 0.995, z), G(0.13, FRAME_HIGH + 0.05, z)], [0.014, 0.014], 'Metal', sides=5)
        box(scene, f'RodFoot{side}', (0.1, FRAME_HIGH + 0.06, z - 0.03), (0.16, FRAME_HIGH + 0.075, z + 0.03), 'Metal')
        # Lamp under the canopy
        lathe(scene, f'LightLamp{side}', [(0.0, FRAME_HIGH - 0.03), (0.05, FRAME_HIGH - 0.03), (0.05, FRAME_HIGH - 0.03), (0.06, FRAME_HIGH)], 'Light',
              segments=8, center=(0.06, side * 0.2), is_smooth=False)
    # Lit sign strip on the front fascia
    box(scene, 'LightSign', (0.235, FRAME_HIGH - 0.012, -0.38), (0.25, FRAME_HIGH + 0.067, 0.38), 'Light')
    # Handrails down the step on both sides: two posts under a sloping bar
    for side in (-1, 1):
        z = side * 0.44
        top, bottom = G(-0.06, LANDING + 0.25, z), G(0.2, LANDING / 2 + 0.25, z)
        pole(scene, f'RailPostHigh{side}', [G(-0.06, LANDING, z), top], [0.016, 0.016], 'Metal', sides=5, is_level=True)
        pole(scene, f'RailPostLow{side}', [G(0.2, LANDING / 2, z), bottom], [0.016, 0.016], 'Metal', sides=5, is_level=True)
        pole(scene, f'RailBar{side}', [top.lerp(bottom, -0.12), bottom.lerp(top, -0.12)], [0.02, 0.02], 'Metal', sides=5)
    trim_height(scene, 1.0)
