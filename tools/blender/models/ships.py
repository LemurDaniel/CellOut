# NOTE:
#   Ships of the civilization (assets/models/civ/): the war galley the chieftain becomes on
#   the sea once the city knows Sailing (Civ/ShipKeeper.cs). +X is the bow, +Y up, the origin
#   is the middle of the ship on the waterline, it is about 3 units long with its ram.
#   Materials: Wood (hull, mast, oars), Door (dark seams of the planks), PaintDark (stripe,
#   posts, bands of the sail), Paint (sail, shields, canopy, pennant: the game tints them in
#   the color of the chieftain), Bronze (ram, bosses, lantern cage), Gold (sun on the sail,
#   tips of the posts), EyeWhite and Pupil (the eyes painted on the bow), Ember (the lantern),
#   Rope. Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only civ/

#
#   Chapters:
#     HULL   The lofted hull with its inner shell, seams of the planks, deck, benches, stripe.
#     BOW    Bronze ram, painted eyes, curled posts with golden tips.
#     RIG    Mast, yard, the bellied sail with its bands and sun, shrouds, stays, pennant.
#     TRIM   Oars, steering oars, shields with bosses, the canopy and lantern at the stern.
#

import math

import bmesh

from models.buildings import _link, beam, lathe, plate, pole, sheet
from models.helpers import G, ellipsoid

LENGTH = 1.3                 # half the length of the hull
WIDTH = 0.42                 # half the width in the middle
KEEL = -0.24                 # lowest point of the keel, below the waterline
SECTIONS = 28
SPAN = 14                    # points across one section, rail to rail


# ===========================================================================
# =================================   HULL   ================================
# ===========================================================================
#
#   half_width    Half the width of the hull at t (-1 stern .. 1 bow), pointed at both ends.
#   rail          Height of the rail at t: low in the middle, rising to bow and stern.
#   section       The points of one section from the left rail over the keel to the right.
#   loft          Faces between all sections, inner shell faces turned inwards.
#   build_hull    Outer and inner shell, rims, seams of the planks, deck, benches, stripe.
#

def half_width(t):
    return max(0.012, WIDTH * (1.0 - abs(t) ** 2.4) ** 0.55)


def rail(t):
    return 0.22 + 0.2 * abs(t) ** 4


def section(t, shrink=1.0, lift=0.0, grow=0.0):
    x, w, top = t * LENGTH, half_width(t) * shrink + grow, rail(t)
    keel = KEEL * (1.0 - abs(t) ** 3) * shrink + lift - grow
    points = []
    for k in range(SPAN):
        u = -1.0 + 2.0 * k / (SPAN - 1)
        y = top - (top - keel) * (1.0 - u * u) ** 0.6
        points.append(G(x, y, u * w))
    return points


def loft(scene, name, rings, material_name, is_inward):
    bm = bmesh.new()
    verts = [[bm.verts.new(point) for point in ring] for ring in rings]
    for a, b in zip(verts, verts[1:]):
        for k in range(len(a) - 1):
            face = bm.faces.new((a[k], b[k], b[k + 1], a[k + 1]))
            face.smooth = True
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if is_inward:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    return _link(scene, bm, name, material_name)


def build_hull(scene):
    ts = [-1.0 + 2.0 * i / (SECTIONS - 1) for i in range(SECTIONS)]
    outer = [section(t) for t in ts]
    inner = [section(t, shrink=0.88, lift=0.06) for t in ts]
    seams = [section(t, grow=0.006) for t in ts]
    loft(scene, 'Hull', outer, 'Wood', is_inward=False)
    loft(scene, 'Inside', inner, 'Wood', is_inward=True)
    # Rims along both rails
    for side, k in (('L', 0), ('R', SPAN - 1)):
        for i in range(SECTIONS - 1):
            beam(scene, f'Rim{side}{i}', outer[i][k], outer[i + 1][k], 0.055, 0.04, 'PaintDark')
    # Seams of the planks: dark lines along the hull, a little outside it
    for k in (2, 4, 6, SPAN - 3, SPAN - 5, SPAN - 7):
        for i in range(2, SECTIONS - 3):
            beam(scene, f'Seam{k}_{i}', seams[i][k], seams[i + 1][k], 0.016, 0.012, 'Door')
    # A flat deck inside, benches for the rowers across it
    deck_t = [t * 0.82 for t in ts]
    outline = [G(t * LENGTH, 0.09, -half_width(t) * 0.82) for t in deck_t] + [G(t * LENGTH, 0.09, half_width(t) * 0.82) for t in reversed(deck_t)]
    plate(scene, 'Deck', outline, G(0.0, 0.025, 0.0), 'Wood')
    for i, t in enumerate((-0.42, -0.21, 0.0, 0.21, 0.42)):
        w = half_width(t) * 0.86
        beam(scene, f'Bench{i}', G(t * LENGTH, 0.17, -w), G(t * LENGTH, 0.17, w), 0.07, 0.03, 'Wood')
    # A painted stripe along the hull just below the rail, a thin bronze line under it
    for side in (-1, 1):
        stripe = [[G(t * LENGTH, rail(t) - 0.07 - dy, side * half_width(t) * 1.014) for t in ts[1:-1]] for dy in (0.0, 0.06)]
        sheet(scene, f'Stripe{side}', stripe, G(0.0, 0.0, side * 0.006), 'PaintDark')
        line = [[G(t * LENGTH, rail(t) - 0.145 - dy, side * half_width(t) * 1.012) for t in ts[2:-2]] for dy in (0.0, 0.015)]
        sheet(scene, f'Line{side}', line, G(0.0, 0.0, side * 0.004), 'Bronze')


# ===========================================================================
# =================================   BOW   =================================
# ===========================================================================
#
#   post        A post that rises from an end of the hull and curls inwards, a golden ball on top.
#   build_bow   The bronze ram on the waterline with its three blades, the eyes on both sides,
#               the posts at bow and stern.
#

def post(scene, name, sign):
    points = []
    for i in range(11):
        a = i / 10 * math.pi * 1.35
        r = 0.17
        x = sign * (LENGTH - 0.02 + math.sin(a) * r * 0.9)
        y = rail(1.0) - 0.02 + (1.0 - math.cos(a)) * r + 0.06 * i / 10
        points.append(G(x, y, 0.0))
    radii = [0.05 - 0.032 * i / 10 for i in range(11)]
    pole(scene, name, points, radii, 'PaintDark', sides=8)
    ellipsoid(scene, f'{name}Tip', points[-1], (0.035, 0.035, 0.035), 'Gold', subdivisions=2)


def build_bow(scene):
    # Ram: a bronze beak on the waterline with three blades
    tip = LENGTH + 0.36
    pole(scene, 'Ram', [G(LENGTH * 0.72, 0.03, 0.0), G(LENGTH + 0.1, 0.02, 0.0), G(tip, -0.01, 0.0)], [0.11, 0.08, 0.0], 'Bronze', sides=8)
    for i, y in enumerate((0.07, 0.02, -0.03)):
        plate(scene, f'RamBlade{i}', [G(LENGTH + 0.02, y + 0.012, -0.0), G(tip - 0.06, y, 0.0), G(LENGTH + 0.02, y - 0.012, 0.0)], G(0.0, 0.0, 0.012), 'Bronze')
        for side in (-1, 1):
            plate(scene, f'RamFin{i}{side}', [G(LENGTH + 0.05, y, 0.0), G(tip - 0.1, y, 0.0), G(LENGTH + 0.08, y, side * 0.07)], G(0.0, 0.012, 0.0), 'Bronze')
    # Painted eyes: an eye on each side of the bow that watches where the ship goes
    t = 0.8
    x, w, y = t * LENGTH, half_width(t), rail(t) - 0.1
    for side in (-1, 1):
        ellipsoid(scene, f'Eye{side}', G(x, y, side * (w + 0.004)), (0.085, 0.008, 0.05), 'EyeWhite', subdivisions=2)
        ellipsoid(scene, f'Pupil{side}', G(x + 0.02, y, side * (w + 0.011)), (0.032, 0.006, 0.032), 'Pupil', subdivisions=2)
    post(scene, 'Prow', 1.0)
    post(scene, 'Stern', -1.0)


# ===========================================================================
# =================================   RIG   =================================
# ===========================================================================
#
#   sail_point   A point of the sail at (u across, v down), bellying forward in the wind.
#   build_rig    Mast, yard, the sail with two dark bands and a golden sun with rays, shrouds
#                to both rails, stays to bow and stern, a pennant on the masthead.
#

MAST_X = 0.15
MAST_TOP = 2.05
YARD_Y = 1.82
SAIL_LOW = 0.72
SAIL_HALF = 0.74


def sail_point(u, v, ahead=0.0):
    y = YARD_Y - 0.03 - (YARD_Y - 0.03 - SAIL_LOW) * v
    belly = 0.2 * (1.0 - u * u) * math.sin(math.pi * (0.15 + 0.85 * v))
    return G(MAST_X + 0.08 + belly + ahead, y, u * SAIL_HALF * (1.0 - 0.06 * v))


def build_rig(scene):
    pole(scene, 'Mast', [G(MAST_X, 0.08, 0.0), G(MAST_X, 1.0, 0.0), G(MAST_X, MAST_TOP, 0.0)], [0.05, 0.042, 0.03], 'Wood', sides=8)
    lathe(scene, 'MastTop', [(0.045, MAST_TOP - 0.02), (0.06, MAST_TOP), (0.0, MAST_TOP + 0.05)], 'Gold', segments=10, center=(MAST_X, 0.0))
    pole(scene, 'Yard', [G(MAST_X + 0.05, YARD_Y, -SAIL_HALF - 0.1), G(MAST_X + 0.05, YARD_Y, 0.0), G(MAST_X + 0.05, YARD_Y, SAIL_HALF + 0.1)],
         [0.022, 0.032, 0.022], 'Wood', sides=6)
    # The sail and two dark bands across it, a little in front of the cloth
    rows = [[sail_point(-1.0 + 2.0 * k / 10, j / 8) for k in range(11)] for j in range(9)]
    sheet(scene, 'Sail', rows, G(0.012, 0.0, 0.0), 'Paint')
    for b, (v0, v1) in enumerate(((0.12, 0.2), (0.82, 0.9))):
        band = [[sail_point(-1.0 + 2.0 * k / 10, v, ahead=0.014) for k in range(11)] for v in (v0, v1)]
        sheet(scene, f'Band{b}', band, G(0.004, 0.0, 0.0), 'PaintDark')
    # The sun in the middle of the sail, with rays
    center = sail_point(0.0, 0.5, ahead=0.016)
    ellipsoid(scene, 'Sun', center, (0.012, 0.13, 0.13), 'Gold', subdivisions=3)
    for i in range(12):
        a = i / 12 * math.tau
        inner = G(0.0, math.sin(a) * 0.16, math.cos(a) * 0.16)
        outer = G(0.0, math.sin(a) * 0.26, math.cos(a) * 0.26)
        beam(scene, f'Ray{i}', center + inner, center + outer, 0.03, 0.01, 'Gold')
    # Shrouds from the masthead to both rails, stays to bow and stern
    for side in (-1, 1):
        for i, t in enumerate((-0.15, 0.05, 0.25)):
            pole(scene, f'Shroud{side}{i}', [G(MAST_X, MAST_TOP - 0.08, 0.0), G(t * LENGTH, rail(t) + 0.02, side * half_width(t))], [0.007, 0.007], 'Rope', sides=4)
    pole(scene, 'StayBow', [G(MAST_X, MAST_TOP - 0.05, 0.0), G(LENGTH * 0.97, rail(0.97) + 0.12, 0.0)], [0.008, 0.008], 'Rope', sides=4)
    pole(scene, 'StayStern', [G(MAST_X, MAST_TOP - 0.05, 0.0), G(-LENGTH * 0.95, rail(-0.95) + 0.1, 0.0)], [0.008, 0.008], 'Rope', sides=4)
    # A long pennant streaming back from the masthead, waving
    pennant = []
    for j in range(2):
        row = []
        for k in range(8):
            s = k / 7
            width = 0.09 * (1.0 - s * 0.85)
            row.append(G(MAST_X - s * 0.7, MAST_TOP + 0.02 - (j - 0.5) * width, math.sin(s * math.pi * 1.6) * 0.06))
        pennant.append(row)
    sheet(scene, 'Pennant', pennant, G(0.0, 0.0, 0.008), 'Paint')


# ===========================================================================
# =================================   TRIM   ================================
# ===========================================================================
#
#   build_trim   Six oars a side through the hull, two steering oars at the stern, shields
#                with bronze bosses along the rail, a canopy over the stern, a lantern.
#

def build_trim(scene):
    for side in (-1, 1):
        for i, t in enumerate((-0.5, -0.3, -0.1, 0.1, 0.3, 0.5)):
            x, w = t * LENGTH, half_width(t)
            beam(scene, f'Oar{side}{i}', G(x + 0.05, rail(t) - 0.04, side * (w - 0.03)), G(x - 0.13, -0.12, side * (w + 0.62)), 0.026, 0.026, 'Wood')
            beam(scene, f'Blade{side}{i}', G(x - 0.11, -0.08, side * (w + 0.55)), G(x - 0.15, -0.16, side * (w + 0.72)), 0.085, 0.014, 'Wood')
        for i, t in enumerate((-0.6, -0.36, -0.12, 0.12, 0.36, 0.6)):
            x, w = t * LENGTH, half_width(t)
            ellipsoid(scene, f'Shield{side}{i}', G(x, rail(t) - 0.02, side * (w + 0.02)), (0.1, 0.025, 0.1), 'Paint', subdivisions=2)
            ellipsoid(scene, f'Boss{side}{i}', G(x, rail(t) - 0.02, side * (w + 0.045)), (0.03, 0.018, 0.03), 'Bronze', subdivisions=2)
        # Steering oar at the stern, its blade deep in the water
        t = -0.82
        x, w = t * LENGTH, half_width(t)
        beam(scene, f'Rudder{side}', G(x + 0.1, rail(t) + 0.12, side * (w + 0.02)), G(x - 0.25, -0.3, side * (w + 0.12)), 0.045, 0.045, 'Wood')
        beam(scene, f'RudderBlade{side}', G(x - 0.16, -0.12, side * (w + 0.1)), G(x - 0.3, -0.36, side * (w + 0.13)), 0.16, 0.02, 'Wood')
    # Canopy over the stern on four posts
    t0, t1 = -0.74, -0.52
    for i, (t, side) in enumerate(((t0, -1), (t0, 1), (t1, -1), (t1, 1))):
        w = half_width(t) * 0.8
        pole(scene, f'CanopyPost{i}', [G(t * LENGTH, 0.1, side * w), G(t * LENGTH, 0.6, side * w)], [0.018, 0.018], 'Wood', sides=6)
    canopy = []
    for j in range(5):
        row = []
        for k in range(7):
            u = -1.0 + 2.0 * k / 6
            t = t0 + (t1 - t0) * j / 4
            w = half_width(t) * 0.86
            row.append(G(t * LENGTH, 0.62 + 0.1 * (1.0 - u * u), u * w))
        canopy.append(row)
    sheet(scene, 'Canopy', canopy, G(0.0, 0.015, 0.0), 'Paint')
    # Lantern on the stern post
    at = G(-LENGTH - 0.05, rail(1.0) + 0.12, 0.0)
    ellipsoid(scene, 'Lantern', at, (0.04, 0.04, 0.055), 'Ember', subdivisions=2)
    lathe(scene, 'LanternCap', [(0.05, at.z + 0.055), (0.0, at.z + 0.1)], 'Bronze', segments=8, center=(at.x, -at.y))    # at is in Blender axes: z is up


def build_ship(scene):
    build_hull(scene)
    build_bow(scene)
    build_rig(scene)
    build_trim(scene)
