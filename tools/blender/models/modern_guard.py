# NOTE:
#   Fences and edges of the modern town: two building blocks of the building designer
#   (assets/models/building/), a chain-link fence panel and a concrete jersey barrier, and
#   the two pieces of the boundary of the city (assets/models/village/), a low concrete wall
#   with a railing and the gate where a street leaves town.
#   Blocks stand in the frame of models/buildings.py: +Y up, the origin is the center of
#   the footprint on the ground, one grid cell is 0.5 units, a block stays inside its box.
#   Both blocks run along X like the palisade. The wall pieces stand in the frame of the
#   WALL chapter of models/village.py: they run along Z, +X looks out of the city.
#   Straight edges, no wobble. Materials: Paint (what the player repaints), PaintDark (its
#   trim), Stone (concrete), Metal, Leaf and Light (glows, its objects are named Light...).
#   Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module modern_guard -Call 'build_chain_fence(scene)' -Name chainfence
#   ./tools/blender/Show-Model.ps1 -Module modern_guard -Call 'build_city_gate(scene)' -Name citygate

#
#   Chapters:
#     FENCES      Chain-link fence (4 x 2 x 1 cells) and jersey barrier (2 x 1 x 1).
#     CITY WALL   Low wall with a railing and the gate with two lamp pillars, 2.6 long each.
#

import math

from models.buildings import beam, lathe, plate, pole, trim_height
from models.helpers import G, ellipsoid
from models.modern import box


# ===========================================================================
# ================================   FENCES   ===============================
# ===========================================================================
#
#   mesh_wires          The crossing diagonal wires of the chain-link mesh, cut to its frame.
#   build_chain_fence   A chain-link fence panel along X: a concrete strip with a footing
#                       under each of the three posts, metal posts with painted caps, a top
#                       rail, a tension wire, the mesh of crossing wires, a sign plate.
#   barrier_profile     Cross section of the jersey barrier.
#   barrier_face        How far the upper face of the barrier stands off the middle.
#   build_barrier       A concrete jersey barrier along X: a wide foot, a flared slope, a
#                       slim upright part with a flat top, two lifting slots through the
#                       foot, raised diagonal warning stripes on both faces.
#
#   Both run along X, their faces look along +Z and -Z. The sign hangs on the +Z face.
#

FENCE_HALF = 0.9                                      # x of the two outer posts
FENCE_FOOT = 0.09                                     # height of the concrete strip
FENCE_RAIL = 0.875                                    # height of the top rail
FENCE_WIRE = 0.17                                     # height of the tension wire, the mesh hangs between the two
FENCE_POST_TOP = 0.93                                 # where the posts end under their caps
MESH_DIAGONALS = 10                                   # wires per direction
MESH_WIDTH = 0.024                                   # width of a wire in the plane of the mesh
MESH_DEPTH = 0.02                                     # its thickness across the fence
MESH_WEAVE = 0.006                                    # how far the two directions stand apart across the fence
MESH_SHORTEST = 0.1                                   # shorter stubs in the corners are left out


def mesh_wires(scene, low, high):
    """
    Wires at 45 degrees in both directions between the heights low and high, from post
    to post. Every wire is cut where it leaves that frame. Both directions start at the
    same places along X, so they cross in even diamonds.
    """
    height = high - low
    spacing = (2.0 * FENCE_HALF + height) / MESH_DIAGONALS
    for k in range(MESH_DIAGONALS):
        # x where the uncut wire starts, it ends height further right
        start = -FENCE_HALF - height + spacing * (k + 0.5)
        near, far = max(0.0, -FENCE_HALF - start), min(height, FENCE_HALF - start)
        if far - near < MESH_SHORTEST:
            continue
        for name, side, foot, rise in (('Rising', 1, low, 1), ('Falling', -1, high, -1)):
            z = side * MESH_WEAVE
            beam(scene, f'Mesh{name}{k}', G(start + near, foot + rise * near, z), G(start + far, foot + rise * far, z),
                 MESH_DEPTH, MESH_WIDTH, 'Metal')


def build_chain_fence(scene):
    # Concrete strip under the whole panel, a bigger footing under every post
    box(scene, 'Strip', (-0.99, 0.0, -0.09), (0.99, FENCE_FOOT, 0.09), 'Stone')
    for i, x in enumerate((-FENCE_HALF, 0.0, FENCE_HALF)):
        box(scene, f'Footing{i}', (x - 0.095, 0.0, -0.16), (x + 0.095, FENCE_FOOT + 0.035, 0.16), 'Stone')
        # Post with a painted cap
        pole(scene, f'Post{i}', [G(x, FENCE_FOOT, 0), G(x, FENCE_POST_TOP, 0)], [0.04, 0.04], 'Metal', sides=6, is_level=True)
        lathe(scene, f'Cap{i}', [(0.055, FENCE_POST_TOP - 0.03), (0.055, FENCE_POST_TOP + 0.01), (0.055, FENCE_POST_TOP + 0.01),
                                 (0.035, FENCE_POST_TOP + 0.05), (0.0, FENCE_POST_TOP + 0.065)],
              'PaintDark', segments=6, center=(x, 0.0), has_bottom_cap=True)
        # Clamps holding the tension wire and the rail
        for k, y in enumerate((FENCE_WIRE, FENCE_RAIL)):
            lathe(scene, f'Clamp{i}{k}', [(0.04, y - 0.04), (0.052, y - 0.03), (0.052, y + 0.03), (0.04, y + 0.04)], 'Metal',
                  segments=6, center=(x, 0.0))
    # Top rail and tension wire from the first post to the last
    pole(scene, 'Rail', [G(-FENCE_HALF, FENCE_RAIL, 0), G(FENCE_HALF, FENCE_RAIL, 0)], [0.03, 0.03], 'Metal', sides=6)
    pole(scene, 'Wire', [G(-FENCE_HALF, FENCE_WIRE, 0), G(FENCE_HALF, FENCE_WIRE, 0)], [0.016, 0.016], 'Metal', sides=4)
    mesh_wires(scene, FENCE_WIRE, FENCE_RAIL)
    # Sign plate on the +Z face, in a darker frame
    box(scene, 'SignFrame', (0.25, 0.43, 0.012), (0.65, 0.71, 0.03), 'PaintDark')
    box(scene, 'Sign', (0.275, 0.455, 0.022), (0.625, 0.685, 0.042), 'Paint')
    trim_height(scene, 1.0)


BARRIER_HALF = 0.49                                   # half its length along X
BARRIER_TOP = 0.5
BARRIER_FOOT = (0.24, 0.05)                           # (half width, height) of the upright edge of the foot
BARRIER_KINK = (0.125, 0.15)                          # where the flared slope turns into the upright part
BARRIER_CROWN = 0.085                                 # half width of the flat top
SLOT_HEIGHT = 0.04                                    # lifting slots: gaps under the foot, right through the barrier
SLOTS = ((-0.33, -0.19), (0.19, 0.33))                # their extent along X
STRIPE_LOW, STRIPE_HIGH = 0.185, 0.465               # heights between which the warning stripes lie
STRIPE_COUNT = 7
STRIPE_LEAN = 0.13                                    # how far a stripe leans along X from its lower to its upper edge
STRIPE_RAISE = 0.014                                  # how far it stands out of the concrete
STRIPE_SINK = 0.004                                   # how deep it starts inside the concrete


def barrier_profile(x, base):
    """Cross section of the barrier at x as an outline, its underside at the height base."""
    foot, kink = BARRIER_FOOT, BARRIER_KINK
    right = [(foot[0], base), (foot[0], foot[1]), kink, (BARRIER_CROWN, BARRIER_TOP)]
    return [G(x, y, z) for z, y in right] + [G(x, y, -z) for z, y in reversed(right)]


def barrier_face(y):
    """Half width of the barrier at height y on its upright part: it narrows evenly from the kink to the top."""
    share = (y - BARRIER_KINK[1]) / (BARRIER_TOP - BARRIER_KINK[1])
    return BARRIER_KINK[0] + (BARRIER_CROWN - BARRIER_KINK[0]) * share


def build_barrier(scene):
    # Body in pieces along X: the pieces over a lifting slot start above the ground
    cuts = [-BARRIER_HALF] + [x for slot in SLOTS for x in slot] + [BARRIER_HALF]
    for i, (start, end) in enumerate(zip(cuts, cuts[1:])):
        is_slot = (start, end) in SLOTS
        plate(scene, f'Body{i}', barrier_profile(start, SLOT_HEIGHT if is_slot else 0.0), G(end - start, 0.0, 0.0), 'Stone')
    # Warning stripes: leaning bands on both faces, side by side, Paint and PaintDark in turns
    width = (2.0 * BARRIER_HALF - 0.04 - STRIPE_LEAN) / STRIPE_COUNT
    # The upright part leans inwards, the stripes are pushed out square to it
    lean_in = (BARRIER_KINK[0] - BARRIER_CROWN) / (BARRIER_TOP - BARRIER_KINK[1])
    out_z, out_y = 1.0 / math.hypot(1.0, lean_in), lean_in / math.hypot(1.0, lean_in)
    for side in (-1, 1):
        raise_by = G(0.0, out_y * (STRIPE_RAISE + STRIPE_SINK), side * out_z * (STRIPE_RAISE + STRIPE_SINK))
        for i in range(STRIPE_COUNT):
            x = -BARRIER_HALF + 0.02 + width * i
            corners = ((x, STRIPE_LOW), (x + width, STRIPE_LOW), (x + width + STRIPE_LEAN, STRIPE_HIGH), (x + STRIPE_LEAN, STRIPE_HIGH))
            outline = [G(along, y, side * (barrier_face(y) - STRIPE_SINK)) for along, y in corners]
            plate(scene, f'Stripe{side}{i}', outline, raise_by, 'Paint' if 0 == i % 2 else 'PaintDark')
    trim_height(scene, BARRIER_TOP)


# ===========================================================================
# ==============================   CITY WALL   ==============================
# ===========================================================================
#
#   wall_run          A stretch of the low concrete wall along Z: foot, wall and cap.
#   railing           The two rails of the railing along Z over a stretch of the wall.
#   build_city_wall   One straight piece of the boundary: the low wall, a metal railing
#                     on four posts, a concrete planter with a clipped hedge on the inside.
#   build_city_gate   An opening of the same length where a street leaves town: two square
#                     concrete pillars with a globe lamp each, a short stub of the wall
#                     and its railing from each pillar to the end of the piece.
#
#   The modern successors of build_wall and build_wall_gate in models/village.py, in the
#   same frame: the origin in the middle of the piece on the ground, the piece runs along
#   Z from -1.3 to 1.3, +X looks out of the city. Wall and rails end flat and flush at
#   both ends, so pieces set end to end close up. Low enough to step over. No trim_height:
#   these are no blocks. Nothing lies on the ground between the two pillars.
#

CITY_WALL_LENGTH = 2.6                                # along Z, the same as WALL_LENGTH in models/village.py
CITY_WALL_FOOT = (0.16, 0.07)                         # (half thickness, top) of the wider foot
CITY_WALL_BODY = (0.11, 0.25)                         # of the wall itself
CITY_WALL_CAP = (0.15, 0.31)                         # of the cap lying on it
RAIL_HIGH, RAIL_LOW = 0.59, 0.45                      # heights of the two rails
RAIL_POSTS = 4                                        # evenly spread, half a gap at each end: the spacing runs on over a joint
PILLAR_Z = 0.95                                       # the gate pillars left and right of the street
PILLAR_HALF = 0.17                                    # half the side of a pillar
PILLAR_TOP = 1.14                                     # top of its cap slab
PILLAR_COPING = 0.035                                 # height of the smaller slab on it, the lamp stands on that
LAMP_RADIUS = 0.14                                  # of the glowing globe


def wall_run(scene, name, start, end):
    """The low wall from z = start to z = end: a wider foot, the wall, a cap, all ending flat."""
    low = 0.0
    for part, (half, top) in (('Foot', CITY_WALL_FOOT), ('Body', CITY_WALL_BODY), ('Cap', CITY_WALL_CAP)):
        box(scene, f'{name}{part}', (-half, low, start), (half, top, end), 'Stone')
        low = top


def railing(scene, name, start, end):
    """The upper and the lower rail from z = start to z = end, cut flat at both ends."""
    pole(scene, f'{name}High', [G(0, RAIL_HIGH, start), G(0, RAIL_HIGH, end)], [0.028, 0.028], 'Metal', sides=6)
    pole(scene, f'{name}Low', [G(0, RAIL_LOW, start), G(0, RAIL_LOW, end)], [0.018, 0.018], 'Metal', sides=4)


def build_city_wall(scene):
    half = CITY_WALL_LENGTH / 2
    wall_run(scene, 'Wall', -half, half)
    # Railing: posts on little shoes on the cap, two rails through them
    railing(scene, 'Rail', -half, half)
    for i in range(RAIL_POSTS):
        z = -half + CITY_WALL_LENGTH * (i + 0.5) / RAIL_POSTS
        box(scene, f'Shoe{i}', (-0.045, CITY_WALL_CAP[1], z - 0.045), (0.045, CITY_WALL_CAP[1] + 0.025, z + 0.045), 'Metal')
        pole(scene, f'Post{i}', [G(0, CITY_WALL_CAP[1], z), G(0, RAIL_HIGH + 0.035, z)], [0.024, 0.024], 'Metal', sides=6, is_level=True)
    # Planter on the inside (-X): a concrete trough with a rim, leaning on the wall
    trough = (-0.5, -0.11, -0.2, 0.75)              # x from, x to, z from, z to
    box(scene, 'Planter', (trough[0], 0.0, trough[2]), (trough[1], 0.19, trough[3]), 'Stone')
    box(scene, 'PlanterRim', (trough[0] - 0.02, 0.19, trough[2] - 0.02), (trough[1], 0.235, trough[3] + 0.02), 'Stone')
    # Clipped hedge in it: a box with its upper edges cut off
    hedge = [(-0.47, 0.235), (-0.17, 0.235), (-0.17, 0.44), (-0.23, 0.52), (-0.41, 0.52), (-0.47, 0.44)]   # (x, y), clear of the cap
    plate(scene, 'Hedge', [G(x, y, trough[2] + 0.03) for x, y in hedge], G(0.0, 0.0, trough[3] - trough[2] - 0.06), 'Leaf')


def build_city_gate(scene):
    half = CITY_WALL_LENGTH / 2
    for i, side in enumerate((-1, 1)):
        z = side * PILLAR_Z
        # Pillar: plinth, shaft, a metal band under the cap slab, a smaller coping on it
        box(scene, f'Plinth{i}', (-PILLAR_HALF - 0.045, 0.0, z - PILLAR_HALF - 0.045), (PILLAR_HALF + 0.045, 0.14, z + PILLAR_HALF + 0.045), 'Stone')
        box(scene, f'Shaft{i}', (-PILLAR_HALF, 0.14, z - PILLAR_HALF), (PILLAR_HALF, PILLAR_TOP - 0.08, z + PILLAR_HALF), 'Stone')
        box(scene, f'Band{i}', (-PILLAR_HALF - 0.012, PILLAR_TOP - 0.2, z - PILLAR_HALF - 0.012),
            (PILLAR_HALF + 0.012, PILLAR_TOP - 0.15, z + PILLAR_HALF + 0.012), 'Metal')
        box(scene, f'Slab{i}', (-PILLAR_HALF - 0.05, PILLAR_TOP - 0.08, z - PILLAR_HALF - 0.05), (PILLAR_HALF + 0.05, PILLAR_TOP, z + PILLAR_HALF + 0.05), 'Stone')
        lamp = PILLAR_TOP + PILLAR_COPING
        box(scene, f'Coping{i}', (-PILLAR_HALF + 0.03, PILLAR_TOP, z - PILLAR_HALF + 0.03), (PILLAR_HALF - 0.03, lamp, z + PILLAR_HALF - 0.03), 'Stone')
        # Plaque on the face that looks out of the city
        box(scene, f'Plaque{i}', (PILLAR_HALF - 0.01, 0.62, z - 0.1), (PILLAR_HALF + 0.015, 0.9, z + 0.1), 'Metal')
        # Lamp: a metal socket, the glowing globe, a metal lid with a knob
        globe = lamp + 0.05 + LAMP_RADIUS * 0.9
        lathe(scene, f'Socket{i}', [(0.11, lamp), (0.11, lamp + 0.025), (0.11, lamp + 0.025), (0.06, lamp + 0.06), (0.0, lamp + 0.06)],
              'Metal', segments=8, center=(0.0, z))
        ellipsoid(scene, f'LightGlobe{i}', G(0, globe, z), (LAMP_RADIUS, LAMP_RADIUS, LAMP_RADIUS), 'Light', subdivisions=2)
        lathe(scene, f'Lid{i}', [(0.095, globe + LAMP_RADIUS * 0.72), (0.06, globe + LAMP_RADIUS + 0.015), (0.0, globe + LAMP_RADIUS + 0.03)],
              'Metal', segments=8, center=(0.0, z), has_bottom_cap=True)
        ellipsoid(scene, f'Knob{i}', G(0, globe + LAMP_RADIUS + 0.045, z), (0.025, 0.025, 0.028), 'Metal', subdivisions=1)
        # Stub of the wall and its railing from inside the pillar to the end of the piece
        inner, outer = sorted((side * (PILLAR_Z + PILLAR_HALF - 0.02), side * half))
        wall_run(scene, f'Stub{i}', inner, outer)
        railing(scene, f'Rail{i}', inner, outer)
