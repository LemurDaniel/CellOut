# NOTE:
#   The scenery a city gets from its techs (assets/models/civ/, placed by Civ/CityScenery.cs):
#   a field with furrows, ripe grain, a fence and a scarecrow (Farming) and a harbor pier on
#   posts with bollards, rope, crates, amphoras and a lantern (Sailing). +X is the front of a
#   model, +Y up, the origin is the middle of its footprint on the ground (the field) or where
#   the pier meets the shore (the pier runs out along +X). Materials: Earth, Moss, Twig,
#   FruitGold, Wood, Log, Rope, Paint (cloth of the scarecrow, amphoras: tinted), PaintDark,
#   Bone, Ember, Bronze. Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only civ/

#
#   Chapters:
#     FIELD   Soil, furrows, rows of grain, a fence and a scarecrow.
#     PIER    Posts, deck, bollards, rope, cargo and a lantern.
#

import math
import random

from models.buildings import beam, lathe, plate, pole, rock
from models.helpers import G, ellipsoid, join_by_material

# ===========================================================================
# ================================   FIELD   ================================
# ===========================================================================
#
#   ear           One stalk of grain with its golden ear, leaning a little.
#   build_field   A plot of 2.4 x 1.6: soil, five furrows, two rows of grain on every ridge, a fence of
#                 posts and rails around it with a gap, a scarecrow in the middle.
#

FIELD_X, FIELD_Z = 1.2, 0.8


def ear(scene, name, base, rng):
    lean = G(rng.uniform(-0.04, 0.04), 0.0, rng.uniform(-0.04, 0.04))
    height = rng.uniform(0.28, 0.38)
    top = base + G(0.0, height, 0.0) + lean
    pole(scene, f'{name}Stalk', [base, top], [0.006, 0.004], 'Twig', sides=3)
    ellipsoid(scene, f'{name}Ear', top + G(0.0, 0.035, 0.0) + lean * 0.3, (0.02, 0.02, 0.06), 'FruitGold', subdivisions=1)


def build_field(scene):
    rng = random.Random(401)
    plate(scene, 'Soil', [G(-FIELD_X, 0.0, -FIELD_Z), G(FIELD_X, 0.0, -FIELD_Z), G(FIELD_X, 0.0, FIELD_Z), G(-FIELD_X, 0.0, FIELD_Z)], G(0.0, 0.03, 0.0), 'Earth')
    for i in range(5):
        z = -FIELD_Z + 0.16 + i * (FIELD_Z * 2.0 - 0.32) / 4
        beam(scene, f'Ridge{i}', G(-FIELD_X + 0.08, 0.05, z), G(FIELD_X - 0.08, 0.05, z), 0.12, 0.05, 'Earth', rng, 0.006)
        for k in range(22):
            x = -FIELD_X + 0.12 + k * (FIELD_X * 2.0 - 0.24) / 21
            if abs(x) < 0.2 and 2 == i:
                continue        # room for the scarecrow
            for row in (-0.035, 0.035):
                ear(scene, f'Grain{i}_{k}{row}', G(x + rng.uniform(-0.025, 0.025), 0.07, z + row + rng.uniform(-0.015, 0.015)), rng)
    # Fence: posts every so often, two rails, a gap at the front for the gate
    corners = [(-FIELD_X - 0.08, -FIELD_Z - 0.08), (FIELD_X + 0.08, -FIELD_Z - 0.08), (FIELD_X + 0.08, FIELD_Z + 0.08), (-FIELD_X - 0.08, FIELD_Z + 0.08)]
    for side, ((x0, z0), (x1, z1)) in enumerate(zip(corners, corners[1:] + corners[:1])):
        count = 7 if 0 == side % 2 else 5
        for i in range(count + 1):
            s = i / count
            x, z = x0 + (x1 - x0) * s, z0 + (z1 - z0) * s
            if 1 == side and 0.35 < s < 0.65:
                continue        # the gate in the front fence
            pole(scene, f'Post{side}{i}', [G(x, 0.0, z), G(x, 0.26 + rng.uniform(-0.02, 0.02), z)], [0.022, 0.018], 'Log', sides=5, is_level=True)
        for rail in (0.12, 0.22):
            if 1 == side:
                for (s0, s1) in ((0.0, 0.35), (0.65, 1.0)):
                    beam(scene, f'Rail{side}{rail}{s0}', G(x0 + (x1 - x0) * s0, rail, z0 + (z1 - z0) * s0), G(x0 + (x1 - x0) * s1, rail, z0 + (z1 - z0) * s1), 0.025, 0.025, 'Wood', rng, 0.004)
            else:
                beam(scene, f'Rail{side}{rail}', G(x0, rail, z0), G(x1, rail, z1), 0.025, 0.025, 'Wood', rng, 0.004)
    # Scarecrow: a post, a cross bar with a ragged shirt, a sack head with a hat
    pole(scene, 'CrowPost', [G(0.0, 0.0, 0.0), G(0.0, 0.62, 0.0)], [0.025, 0.02], 'Log', sides=5, is_level=True)
    beam(scene, 'CrowArms', G(0.0, 0.48, -0.24), G(0.0, 0.48, 0.24), 0.03, 0.03, 'Log')
    plate(scene, 'Shirt', [G(0.0, 0.5, -0.2), G(0.0, 0.5, 0.2), G(0.0, 0.3, 0.13), G(0.0, 0.26, 0.0), G(0.0, 0.3, -0.13)], G(0.03, 0.0, 0.0), 'Paint')
    ellipsoid(scene, 'CrowHead', G(0.0, 0.62, 0.0), (0.06, 0.06, 0.07), 'Bone', subdivisions=2)
    lathe(scene, 'Hat', [(0.13, 0.66), (0.12, 0.675), (0.06, 0.68), (0.05, 0.75), (0.0, 0.76)], 'Twig', segments=10, has_bottom_cap=True)
    join_by_material([obj for obj in scene.collection.objects if obj.type == 'MESH'])     # hundreds of stalks become one mesh per material


# ===========================================================================
# =================================   PIER   ================================
# ===========================================================================
#
#   build_pier   Two rows of posts from the shore out to the sea, a plank deck on beams,
#                bollards with rope, a coil of rope, crates, amphoras and a lantern at the end.
#

PIER_LENGTH = 3.0
PIER_HALF = 0.4
DECK_Y = 0.32


def build_pier(scene):
    rng = random.Random(402)
    # Posts deep into the sea bed, a little uneven
    for i in range(6):
        x = 0.2 + i * (PIER_LENGTH - 0.3) / 5
        for side in (-1, 1):
            z = side * (PIER_HALF - 0.04)
            pole(scene, f'Post{side}{i}', [G(x, -0.9, z), G(x + rng.uniform(-0.02, 0.02), DECK_Y + 0.06, z)], [0.05, 0.045], 'Log', sides=6, is_level=True)
        beam(scene, f'Joist{i}', G(x, DECK_Y - 0.04, -PIER_HALF), G(x, DECK_Y - 0.04, PIER_HALF), 0.06, 0.06, 'Wood')
    # Planks across, slightly uneven
    count = 26
    for i in range(count):
        x = 0.06 + i * (PIER_LENGTH - 0.06) / count
        width = (PIER_LENGTH - 0.06) / count - 0.012
        beam(scene, f'Plank{i}', G(x + width / 2, DECK_Y, -PIER_HALF), G(x + width / 2, DECK_Y, PIER_HALF), 0.025, width, 'Wood', rng, 0.004)
    # Bollards with rope at the far end
    for side in (-1, 1):
        at = G(PIER_LENGTH - 0.25, DECK_Y, side * (PIER_HALF - 0.1))
        lathe(scene, f'Bollard{side}', [(0.05, DECK_Y), (0.045, DECK_Y + 0.12), (0.06, DECK_Y + 0.14), (0.0, DECK_Y + 0.16)], 'PaintDark', segments=8,
              center=(at.x, -at.y), has_bottom_cap=True)
        lathe(scene, f'Loop{side}', [(0.056, DECK_Y + 0.06), (0.062, DECK_Y + 0.08), (0.056, DECK_Y + 0.1)], 'Rope', segments=10, center=(at.x, -at.y))
    # A coil of rope, crates, amphoras
    lathe(scene, 'Coil', [(0.09, DECK_Y + 0.01), (0.12, DECK_Y + 0.03), (0.09, DECK_Y + 0.05), (0.06, DECK_Y + 0.03), (0.09, DECK_Y + 0.01)], 'Rope', segments=14,
          center=(1.3, 0.22))
    for i, (x, z, s) in enumerate(((0.6, -0.2, 0.16), (0.78, -0.22, 0.13), (0.68, -0.2, 0.12))):
        y = DECK_Y + (0.0 if i < 2 else 0.16)
        beam(scene, f'Crate{i}', G(x, y, z), G(x, y + s, z), s, s, 'Wood', rng, 0.004)
    for i, (x, z) in enumerate(((2.0, 0.2), (2.12, 0.24), (2.06, 0.1))):
        profile = [(0.0, DECK_Y), (0.05, DECK_Y + 0.02), (0.08, DECK_Y + 0.1), (0.07, DECK_Y + 0.17), (0.03, DECK_Y + 0.21), (0.035, DECK_Y + 0.24), (0.0, DECK_Y + 0.245)]
        lathe(scene, f'Amphora{i}', profile, 'Paint', segments=12, center=(x, z))
    # A lantern on a pole at the very end
    pole(scene, 'LanternPole', [G(PIER_LENGTH - 0.08, DECK_Y, 0.0), G(PIER_LENGTH - 0.08, DECK_Y + 0.75, 0.0), G(PIER_LENGTH + 0.08, DECK_Y + 0.8, 0.0)], [0.025, 0.022, 0.018], 'Wood', sides=6)
    ellipsoid(scene, 'Lantern', G(PIER_LENGTH + 0.08, DECK_Y + 0.68, 0.0), (0.04, 0.04, 0.055), 'Ember', subdivisions=2)
    lathe(scene, 'LanternCap', [(0.05, DECK_Y + 0.73), (0.0, DECK_Y + 0.77)], 'Bronze', segments=8, center=(PIER_LENGTH + 0.08, 0.0))
    # A few stones where the pier meets the shore
    for i in range(4):
        s = rng.uniform(0.1, 0.16)
        rock(scene, f'Stone{i}', G(rng.uniform(-0.2, 0.15), 0.02, rng.uniform(-0.5, 0.5)), (s, s * 0.6, s * 0.9), 'Stone', rng)
    join_by_material([obj for obj in scene.collection.objects if obj.type == 'MESH'])
