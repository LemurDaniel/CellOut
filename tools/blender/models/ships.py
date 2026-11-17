# NOTE:
#   Ships of the civilization (assets/models/civ/): the chunky little motor ship (a harbor
#   tug) the chieftain becomes on the sea once the city knows Sailing (Civ/ShipKeeper.cs,
#   Civ/ShipModel.cs), the neighbors sail the same model in their color. +X is the bow,
#   +Y up, the origin is the middle of the ship on the waterline, the hull is 2.6 units
#   long and 1 wide, the masthead light stands 2 high. No rig, no animation.
#   Materials: Paint (bulwark band of the hull, roof of the wheelhouse, band of the funnel,
#   flag: the game tints them in the color of the chieftain or the neighbor), PaintDark
#   (lower hull, doors, cheeks of the winch: the same color 30 % darker), Bone (the white
#   deckhouse, rail cap, strake, radar), Wood (deck, crates), Glass (windows, portholes), Metal
#   (mast, railings, winch, bollards), Rubber (tyre fenders, bow fender, funnel top),
#   FruitRed (life rings), Rope, Light (masthead light and searchlight, objects named Light...).
#   Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only civ/

#
#   Chapters:
#     HULL    The lofted steel hull in two bands, inner shell, rail cap, strake and the deck.
#     HOUSE   Deckhouse with portholes and doors, wheelhouse with its windows and roof.
#     STACK   Funnel with its colored band, mast with light and flag, radar, searchlight, railings.
#     TRIM    Tyre fenders, bow fender, life rings, winch, bollards, capstan and crates.
#

import math

import bmesh
from mathutils import Vector

from models.buildings import _link, beam, lathe, plate, pole, sheet
from models.helpers import G, ellipsoid, join_by_material

LENGTH = 1.3                 # half the length of the hull
WIDTH = 0.5                  # half the width in the middle
KEEL = -0.24                 # lowest point of the keel, below the waterline
DECK_Y = 0.2                 # the deck inside the bulwark
SECTIONS = 22
SPAN = 13                    # points across one section, rail to rail
BAND = 1                     # index of the section point where the bulwark band ends


# ===========================================================================
# =================================   HULL   ================================
# ===========================================================================
#
#   half_width    Half the width of the hull at t (-1 stern .. 1 bow): a round stern, a pointed bow.
#   rail          Height of the rail at t: low aft, rising steeply to the bow.
#   section       The points of one section from the left rail over the keel to the right.
#   loft          Faces between sections, looking away from the keel line or towards it.
#   build_hull    Bulwark band and lower hull, inner shell, white rail cap and rubbing strake,
#                 the wooden deck.
#

def half_width(t):
    if t < 0.0:
        return max(0.012, WIDTH * (1.0 - abs(t) ** 5) ** 0.5)
    return max(0.012, WIDTH * (1.0 - t ** 3.2) ** 0.55)


def rail(t):
    return 0.32 + 0.26 * max(t, 0.0) ** 3 + 0.04 * abs(t) ** 4


def section(t, shrink=1.0, lift=0.0):
    x, w, top = t * LENGTH, half_width(t) * shrink, rail(t)
    keel = KEEL * (1.0 - abs(t) ** 3) * shrink + lift
    points = []
    for k in range(SPAN):
        u = math.sin((-1.0 + 2.0 * k / (SPAN - 1)) * math.pi / 2)     # more points on the steep sides than on the bottom
        y = top - (top - keel) * (1.0 - abs(u) ** 3) ** 0.5           # boxy steel section: upright sides, flat bottom
        points.append(G(x, y, u * w))
    return points


def loft(scene, name, rings, material_name, is_inward=False):
    """Smooth faces between rings of points, every face looking away from the keel line (or towards it)."""
    bm = bmesh.new()
    verts = [[bm.verts.new(point) for point in ring] for ring in rings]
    for a, b in zip(verts, verts[1:]):
        for k in range(len(a) - 1):
            face = bm.faces.new((a[k], b[k], b[k + 1], a[k + 1]))
            face.smooth = True
            face.normal_update()
            outward = Vector((0.0, face.calc_center_median().y, 0.0))
            if (face.normal.dot(outward) < 0.0) != is_inward:
                face.normal_flip()
    return _link(scene, bm, name, material_name)


def build_hull(scene):
    ts = [math.sin((-1.0 + 2.0 * i / (SECTIONS - 1)) * math.pi / 2) for i in range(SECTIONS)]     # denser at the round ends
    outer = [section(t) for t in ts]
    inner = [section(t, shrink=0.9, lift=0.05) for t in ts]
    # The bulwark band in the paint on both sides, the hull below it darker
    loft(scene, 'BandL', [ring[:BAND + 1] for ring in outer], 'Paint')
    loft(scene, 'BandR', [ring[SPAN - 1 - BAND:] for ring in outer], 'Paint')
    loft(scene, 'Hull', [ring[BAND:SPAN - BAND] for ring in outer], 'PaintDark')
    loft(scene, 'Inside', inner, 'Bone', is_inward=True)
    # White cap along both rails
    for side, k in (('L', 0), ('R', SPAN - 1)):
        for i in range(SECTIONS - 1):
            beam(scene, f'Rim{side}{i}', outer[i][k], outer[i + 1][k], 0.07, 0.035, 'Bone')
    # White rubbing strake where the band meets the lower hull
    for side, k in (('L', BAND), ('R', SPAN - 1 - BAND)):
        for i in range(1, SECTIONS - 2):
            beam(scene, f'Strake{side}{i}', outer[i][k], outer[i + 1][k], 0.035, 0.035, 'Bone')
    # The deck inside the bulwark
    deck_t = [t * 0.93 for t in ts]
    outline = [G(t * LENGTH, DECK_Y, -half_width(t) * 0.9) for t in deck_t] + [G(t * LENGTH, DECK_Y, half_width(t) * 0.9) for t in reversed(deck_t)]
    plate(scene, 'Deck', outline, G(0.0, -0.05, 0.0), 'Wood')


# ===========================================================================
# ================================   HOUSE   ================================
# ===========================================================================
#
#   prism         A house on a plan of (x, z) corners between two heights.
#   pane          A flat panel on a wall of a plan: windows and doors.
#   drum          A short cylinder across the ship: portholes, the winch.
#   build_house   The long deckhouse with portholes and a door on each side, on it the
#                 wheelhouse with windows all around and a roof in the paint.
#

HOUSE_PLAN = [(-0.5, -0.3), (0.4, -0.3), (0.58, -0.14), (0.58, 0.14), (0.4, 0.3), (-0.5, 0.3)]
HOUSE_TOP = 0.56
BRIDGE_PLAN = [(-0.06, -0.25), (0.36, -0.25), (0.5, -0.12), (0.5, 0.12), (0.36, 0.25), (-0.06, 0.25)]
BRIDGE_TOP = 1.0
PROUD = 0.01                 # how far windows and doors stand off their wall


def prism(scene, name, plan, low, high, material_name, grow=0.0):
    """A solid on a plan of (x, z) corners from low to high, grow pushes every corner away from the middle."""
    middle_x = sum(x for x, _ in plan) / len(plan)
    outline = [G(x + math.copysign(grow, x - middle_x), low, z + math.copysign(grow, z)) for x, z in plan]
    return plate(scene, name, outline, G(0.0, high - low, 0.0), material_name)


def pane(scene, name, start, end, low, high, material_name, inset=0.035):
    """
    A flat panel on the upright wall whose foot runs from start to end ((x, z), the ship
    lies around z = 0 and the wall looks away from it), inset shorter at both ends.
    """
    length = math.hypot(end[0] - start[0], end[1] - start[1])
    along = ((end[0] - start[0]) / length, (end[1] - start[1]) / length)
    out = (along[1], -along[0])
    middle = ((start[0] + end[0]) / 2, (start[1] + end[1]) / 2)
    if out[0] * (middle[0] - 0.1) + out[1] * middle[1] < 0.0:     # 0.1: a point inside both houses
        out = (-out[0], -out[1])
    a = (start[0] + along[0] * inset, start[1] + along[1] * inset)
    b = (end[0] - along[0] * inset, end[1] - along[1] * inset)
    sink = G(out[0], 0.0, out[1]) * -0.004
    outline = [G(a[0], low, a[1]) + sink, G(b[0], low, b[1]) + sink, G(b[0], high, b[1]) + sink, G(a[0], high, a[1]) + sink]
    return plate(scene, name, outline, G(out[0], 0.0, out[1]) * (PROUD + 0.004), material_name)


def drum(scene, name, center, radius, width, material_name, sides=10):
    """A cylinder around center (Godot x, y, z) with its axis across the ship."""
    x, y, z = center
    return pole(scene, name, [G(x, y, z - width / 2), G(x, y, z + width / 2)], [radius, radius], material_name, sides=sides)


def build_house(scene):
    prism(scene, 'House', HOUSE_PLAN, DECK_Y, HOUSE_TOP, 'Bone')
    prism(scene, 'HouseEdge', HOUSE_PLAN, HOUSE_TOP, HOUSE_TOP + 0.03, 'Metal', grow=0.025)
    for side in (-1, 1):
        z = side * 0.3
        for i, x in enumerate((-0.07, 0.29)):
            drum(scene, f'PortholeRim{side}{i}', (x, 0.4, z), 0.07, 0.016, 'Metal')
            drum(scene, f'Porthole{side}{i}', (x, 0.4, z), 0.05, 0.028, 'Glass')
        pane(scene, f'Door{side}', (-0.46, z), (-0.22, z), DECK_Y, 0.5, 'PaintDark')
        drum(scene, f'DoorEye{side}', (-0.305, 0.41, z + side * PROUD), 0.035, 0.012, 'Glass', sides=8)
    # Wheelhouse: windows on every wall, the front ones lean in the chamfer
    prism(scene, 'Bridge', BRIDGE_PLAN, HOUSE_TOP + 0.03, BRIDGE_TOP, 'Bone')
    for i, (start, end) in enumerate(zip(BRIDGE_PLAN, BRIDGE_PLAN[1:] + BRIDGE_PLAN[:1])):
        pane(scene, f'Window{i}', start, end, 0.72, 0.93, 'Glass', inset=0.03)
    prism(scene, 'BridgeRoof', BRIDGE_PLAN, BRIDGE_TOP, BRIDGE_TOP + 0.05, 'Paint', grow=0.05)


# ===========================================================================
# ================================   STACK   ================================
# ===========================================================================
#
#   build_stack   The fat funnel behind the wheelhouse (white foot, band in the paint, black
#                 top), the mast on the roof with a cross bar, the masthead light and a waving
#                 flag, the radar bar and a searchlight, a railing around the deckhouse roof.
#

FUNNEL_X = -0.28
MAST_X = 0.1
MAST_TOP = 1.95


def build_stack(scene):
    # Funnel: three rings stacked, dark inside
    low = HOUSE_TOP + 0.03
    lathe(scene, 'FunnelFoot', [(0.17, low), (0.16, 0.86)], 'Bone', segments=12, center=(FUNNEL_X, 0.0))
    lathe(scene, 'FunnelBand', [(0.16, 0.86), (0.155, 1.12)], 'Paint', segments=12, center=(FUNNEL_X, 0.0))
    lathe(scene, 'FunnelTop', [(0.155, 1.12), (0.165, 1.15), (0.165, 1.26), (0.125, 1.26), (0.125, 1.18), (0.0, 1.18)], 'Rubber', segments=12, center=(FUNNEL_X, 0.0))
    # Mast with a cross bar, the light on its head
    roof = BRIDGE_TOP + 0.05
    pole(scene, 'Mast', [G(MAST_X, roof, 0.0), G(MAST_X, MAST_TOP, 0.0)], [0.03, 0.02], 'Metal', sides=6, is_level=True)
    beam(scene, 'Yard', G(MAST_X, 1.58, -0.24), G(MAST_X, 1.58, 0.24), 0.03, 0.03, 'Metal')
    ellipsoid(scene, 'LightMast', G(MAST_X, MAST_TOP + 0.03, 0.0), (0.045, 0.045, 0.045), 'Light', subdivisions=1)
    # Radar: a white bar on a short foot in front of the mast
    pole(scene, 'RadarFoot', [G(0.3, roof, 0.0), G(0.3, roof + 0.1, 0.0)], [0.035, 0.025], 'Metal', sides=6, is_level=True)
    beam(scene, 'Radar', G(0.3, roof + 0.125, -0.17), G(0.3, roof + 0.125, 0.17), 0.06, 0.05, 'Bone')
    # Searchlight on the front edge of the roof
    pole(scene, 'SearchFoot', [G(0.46, roof, 0.0), G(0.46, roof + 0.05, 0.0)], [0.02, 0.02], 'Metal', sides=6, is_level=True)
    pole(scene, 'SearchCan', [G(0.42, roof + 0.085, 0.0), G(0.5, roof + 0.085, 0.0)], [0.04, 0.05], 'Metal', sides=8)
    pole(scene, 'LightSearch', [G(0.5, roof + 0.085, 0.0), G(0.512, roof + 0.085, 0.0)], [0.042, 0.042], 'Light', sides=8)
    # Flag: streams back from the mast below its head, waving
    rows = []
    for y in (1.9, 1.68):
        rows.append([G(MAST_X - 0.02 - s * 0.46, y - 0.03 * s, math.sin(s * math.pi * 1.5) * 0.05) for s in (k / 5 for k in range(6))])
    sheet(scene, 'Flag', rows, G(0.0, 0.0, 0.012), 'Paint')
    # Railing around the roof of the deckhouse behind the wheelhouse
    posts = [(-0.48, -0.28), (-0.48, 0.0), (-0.48, 0.28), (-0.28, 0.28), (-0.09, 0.28), (-0.28, -0.28), (-0.09, -0.28)]
    for i, (x, z) in enumerate(posts):
        beam(scene, f'RailPost{i}', G(x, low, z), G(x, low + 0.13, z), 0.022, 0.022, 'Metal')
    for i, (start, end) in enumerate((((-0.09, -0.28), (-0.48, -0.28)), ((-0.48, -0.28), (-0.48, 0.28)), ((-0.48, 0.28), (-0.09, 0.28)))):
        beam(scene, f'Rail{i}', G(start[0], low + 0.13, start[1]), G(end[0], low + 0.13, end[1]), 0.026, 0.026, 'Metal')


# ===========================================================================
# =================================   TRIM   ================================
# ===========================================================================
#
#   ring         A closed ring standing along the ship: tyres and life rings.
#   build_trim   Three tyres on ropes along each side, the fat rubber fender around the bow,
#                life rings on the deckhouse, the towing winch and two bollards on the aft
#                deck, crates at the stern, a capstan on the fore deck.
#

def ring(scene, name, center, radius, thickness, material_name, count=10, sides=5):
    """A ring around center (Godot x, y, z) whose hole looks across the ship."""
    x, y, z = center
    points = [G(x + math.cos(a) * radius, y + math.sin(a) * radius, z) for a in (i / count * math.tau for i in range(count + 1))]
    return pole(scene, name, points, [thickness] * len(points), material_name, sides=sides)


def build_trim(scene):
    for side in (-1, 1):
        # Tyres hang on ropes from the rail
        for i, t in enumerate((-0.52, -0.1, 0.32)):
            x, z = t * LENGTH, side * (half_width(t) + 0.035)
            ring(scene, f'Tyre{side}{i}', (x, 0.14, z), 0.085, 0.04, 'Rubber')
            beam(scene, f'TyreRope{side}{i}', G(x, 0.2, z), G(x, rail(t), z - side * 0.03), 0.016, 0.016, 'Rope')
        # Life ring on the wall of the deckhouse
        ring(scene, f'LifeRing{side}', (0.11, 0.4, side * 0.32), 0.065, 0.026, 'FruitRed')
    # Rubber fender around the bow, just below the rail
    bow = [0.62, 0.74, 0.84, 0.92, 0.97, 1.0]
    path = [G(t * LENGTH + 0.02, rail(t) - 0.1, -half_width(t) - 0.03) for t in bow] + [G(t * LENGTH + 0.02, rail(t) - 0.1, half_width(t) + 0.03) for t in reversed(bow[:-1])]
    pole(scene, 'BowFender', path, [0.05] + [0.065] * (len(path) - 2) + [0.05], 'Rubber', sides=6)
    # Towing winch on the aft deck: a drum between two cheeks
    drum(scene, 'Winch', (-0.72, DECK_Y + 0.1, 0.0), 0.06, 0.24, 'Metal')
    for side in (-1, 1):
        drum(scene, f'WinchCheek{side}', (-0.72, DECK_Y + 0.1, side * 0.13), 0.1, 0.03, 'PaintDark')
        lathe(scene, f'Bollard{side}', [(0.035, DECK_Y), (0.035, DECK_Y + 0.1), (0.055, DECK_Y + 0.11), (0.055, DECK_Y + 0.14), (0.0, DECK_Y + 0.14)], 'Metal', segments=8,
              center=(-1.08, side * 0.2))
    # Crates at the stern
    for i, (x, z, size) in enumerate(((-0.96, 0.14, 0.15), (-0.94, -0.04, 0.12), (-0.96, 0.13, 0.1))):
        y = DECK_Y + (0.0 if i < 2 else 0.15)
        beam(scene, f'Crate{i}', G(x, y, z), G(x, y + size, z), size, size, 'Wood')
    # Capstan on the fore deck
    lathe(scene, 'Capstan', [(0.07, DECK_Y), (0.045, DECK_Y + 0.04), (0.045, DECK_Y + 0.11), (0.07, DECK_Y + 0.13), (0.0, DECK_Y + 0.14)], 'Metal', segments=8, center=(0.9, 0.0))


def build_ship(scene):
    build_hull(scene)
    build_house(scene)
    build_stack(scene)
    build_trim(scene)
    join_by_material([obj for obj in scene.collection.objects if obj.type == 'MESH'])     # one mesh per material, the light ends up as the object Light
