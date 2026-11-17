# NOTE:
#   Building blocks of the civilization (assets/models/building/), the antique group of the
#   building designer: a fluted column, a stepped base and a temple roof with its pediment.
#   Same frame as models/buildings.py: +Y up, +X is the front of a block, the origin is the
#   center of its footprint on the ground, one grid cell is 0.5 units, a block stays inside
#   its box and ends flat where the next one stacks. Materials Paint (the marble the player
#   repaints) and PaintDark (its trim). Registered in build.models.py.
#
#   ./tools/blender/Build-Models.ps1 -Only building/building.column.glb, building/building.steps.glb

#
#   Chapters:
#     ANTIQUE   Column (1 x 3 x 1 cells), steps (4 x 1 x 4) and pediment roof (4 x 2 x 8).
#

import math
import random

from models.buildings import beam, lathe, plate, pole, trim_height
from models.helpers import G, ellipsoid

# ===========================================================================
# ===============================   ANTIQUE   ===============================
# ===========================================================================
#
#   fluted            A bulge for lathe: shallow grooves around a column shaft.
#   build_column      Base rings, a fluted shaft that swells a little (entasis), a round
#                     capital under a square slab.
#   build_steps       Three steps of stone slabs, each a little smaller than the one below.
#   build_pediment    Beam course on the columns, a row of teeth under the cornice, two roof
#                     slopes along Z and the triangular gables at both ends.
#

FLUTES = 16


def fluted(k, j):
    """Every other segment of the shaft a little deeper: the grooves of a column."""
    return -0.012 if 0 == k % 2 else 0.0


def volute(scene, name, center_x, z, y, turn):
    """A spiral scroll in the plane across X at (center_x, y, z), turning outwards, a golden eye in its middle."""
    points, radii = [], []
    for i in range(22):
        a = i / 21 * math.tau * 1.6
        r = 0.095 * (1.0 - i / 24)
        points.append(G(center_x, y + math.sin(a) * r, z + turn * math.cos(a) * r))
        radii.append(0.019 * (1.0 - i / 30))
    pole(scene, name, points, radii, 'PaintDark', sides=5)
    ellipsoid(scene, f'{name}Eye', G(center_x + math.copysign(0.004, center_x), y, z), (0.012, 0.02, 0.02), 'Gold', subdivisions=1)


def build_column(scene):
    # Base: a square plinth, two round rings with a hollow between them
    beam(scene, 'Plinth', G(0.0, 0.0, 0.0), G(0.0, 0.06, 0.0), 0.46, 0.46, 'PaintDark')
    lathe(scene, 'Base', [(0.2, 0.06), (0.215, 0.08), (0.2, 0.1), (0.17, 0.115), (0.175, 0.13), (0.19, 0.145), (0.17, 0.16)], 'PaintDark', segments=24, has_bottom_cap=True)
    # Shaft: slightly thicker a third of the way up, fluted
    shaft = [(0.165, 0.16), (0.172, 0.55), (0.165, 0.95), (0.148, 1.27)]
    lathe(scene, 'Shaft', shaft, 'Paint', segments=FLUTES * 2, bulge=fluted, is_smooth=False)
    # Capital: a golden necking band, the round echinus with its egg ornament
    lathe(scene, 'Necking', [(0.152, 1.27), (0.162, 1.285), (0.152, 1.3)], 'Gold', segments=24)
    lathe(scene, 'Echinus', [(0.152, 1.3), (0.2, 1.34), (0.205, 1.37)], 'PaintDark', segments=24, has_top_cap=True)
    for i in range(14):
        a = i / 14 * math.tau
        ellipsoid(scene, f'Egg{i}', G(math.cos(a) * 0.196, 1.335, math.sin(a) * 0.196), (0.018, 0.018, 0.026), 'Paint', subdivisions=1)
    # Ionic scrolls: a bolster across the top, a spiral at each of its four ends
    for side in (-1, 1):
        pole(scene, f'Bolster{side}', [G(-0.2, 1.4, side * 0.17), G(0.2, 1.4, side * 0.17)], [0.045, 0.045], 'PaintDark', sides=10)
        for end in (-1, 1):
            volute(scene, f'Volute{side}{end}', end * 0.205, side * 0.17, 1.38, side)
    beam(scene, 'Slab', G(0.0, 1.44, 0.0), G(0.0, 1.5, 0.0), 0.5, 0.5, 'PaintDark')
    trim_height(scene, 1.5)


def build_steps(scene):
    rng = random.Random(301)
    for i, (reach, low, high) in enumerate(((1.0, 0.0, 0.17), (0.9, 0.17, 0.34), (0.8, 0.34, 0.5))):
        beam(scene, f'Step{i}', G(0.0, low, 0.0), G(0.0, high, 0.0), reach * 2.0, reach * 2.0, 'Paint', rng, 0.004)
        # A thin darker line where one step lies on the next
        beam(scene, f'Joint{i}', G(0.0, high - 0.012, 0.0), G(0.0, high, 0.0), reach * 2.0 + 0.01, reach * 2.0 + 0.01, 'PaintDark')
    trim_height(scene, 0.5)


def palmette(scene, name, base, leaves, size, material_name, is_across_z=True):
    """A fan of pointed leaves standing up from a point: the crowning ornament of a temple roof.
    is_across_z fans it across Z (seen from the long side), else across X (seen from the gable)."""
    for i in range(leaves):
        a = (i / max(leaves - 1, 1) - 0.5) * 2.0
        spread = G(0.0, 0.0, math.sin(a) * size) if is_across_z else G(math.sin(a) * size, 0.0, 0.0)
        tip = base + G(0.0, math.cos(a) * size, 0.0) + spread
        middle = base.lerp(tip, 0.45)
        pole(scene, f'{name}Leaf{i}', [base, middle, tip], [0.008, size * 0.16, 0.0], material_name, sides=5)


def build_pediment(scene):
    rng = random.Random(302)
    half_x, half_z = 1.0, 2.0
    # Beam course lying on the columns, a frieze with blocks and plain fields above it
    beam(scene, 'Architrave', G(0.0, 0.0, 0.0), G(0.0, 0.16, 0.0), half_z * 2.0, half_x * 2.0, 'Paint')
    beam(scene, 'Frieze', G(0.0, 0.16, 0.0), G(0.0, 0.3, 0.0), half_z * 2.0 - 0.04, half_x * 2.0 - 0.04, 'PaintDark')
    for i in range(9):
        z = -half_z + 0.22 + i * (half_z * 2.0 - 0.44) / 8
        for side in (-1, 1):
            beam(scene, f'Glyph{side}{i}', G(side * (half_x - 0.015), 0.165, z), G(side * (half_x - 0.015), 0.295, z), 0.09, 0.03, 'Paint')
    for i in range(5):
        x = -half_x + 0.2 + i * (half_x * 2.0 - 0.4) / 4
        for side in (-1, 1):
            beam(scene, f'GlyphZ{side}{i}', G(x, 0.165, side * (half_z - 0.015)), G(x, 0.295, side * (half_z - 0.015)), 0.03, 0.09, 'Paint')
    # Teeth under the cornice, all around
    for i in range(18):
        z = -half_z + 0.1 + i * (half_z * 2.0 - 0.2) / 17
        for side in (-1, 1):
            beam(scene, f'ToothX{side}{i}', G(side * (half_x - 0.03), 0.3, z), G(side * (half_x - 0.03), 0.36, z), 0.07, 0.06, 'Paint')
    for i in range(9):
        x = -half_x + 0.1 + i * (half_x * 2.0 - 0.2) / 8
        for side in (-1, 1):
            beam(scene, f'ToothZ{side}{i}', G(x, 0.3, side * (half_z - 0.03)), G(x, 0.36, side * (half_z - 0.03)), 0.06, 0.07, 'Paint')
    beam(scene, 'Cornice', G(0.0, 0.36, 0.0), G(0.0, 0.42, 0.0), half_z * 2.0 + 0.04, half_x * 2.0 + 0.04, 'Paint')
    # Roof: the ridge runs along Z, the two slopes fall to the long sides, tile ribs and a ridge cap
    ridge, eave = 0.8, 0.42             # the golden crown on the ridge still fits the box
    for side in (-1, 1):
        plate(scene, f'Slope{side}', [G(side * half_x, eave, -half_z), G(0.0, ridge, -half_z), G(0.0, ridge, half_z), G(side * half_x, eave, half_z)],
              G(0.0, 0.04, 0.0), 'PaintDark')
        for i in range(14):
            z = -half_z + 0.15 + i * (half_z * 2.0 - 0.3) / 13
            beam(scene, f'Rib{side}{i}', G(side * (half_x - 0.02), eave + 0.04, z), G(0.0, ridge + 0.03, z), 0.035, 0.03, 'PaintDark', rng, 0.003)
            # A small palmette where every rib ends at the eave
            palmette(scene, f'Antefix{side}{i}', G(side * (half_x + 0.01), eave + 0.04, z), 3, 0.07, 'Paint')
    pole(scene, 'RidgeCap', [G(0.0, ridge + 0.05, -half_z - 0.02), G(0.0, ridge + 0.05, half_z + 0.02)], [0.035, 0.035], 'PaintDark', sides=6)
    # Pediments: a triangle at each end, set back a little, with a raking cornice and a sculpted scene
    for end in (-1, 1):
        z = end * (half_z - 0.04)
        plate(scene, f'Gable{end}', [G(-half_x + 0.05, eave, z), G(half_x - 0.05, eave, z), G(0.0, ridge - 0.04, z)], G(0.0, 0.0, -end * 0.08), 'Paint')
        for side in (-1, 1):
            beam(scene, f'Rake{end}{side}', G(side * half_x, eave + 0.02, end * half_z), G(0.0, ridge + 0.02, end * half_z), 0.08, 0.07, 'Paint')
        front = z + end * 0.012
        # A golden sun in the middle, two figures reclining towards it, small shields in the corners
        ellipsoid(scene, f'Sun{end}', G(0.0, 0.57, front), (0.1, 0.02, 0.1), 'Gold', subdivisions=2)
        for side in (-1, 1):
            ellipsoid(scene, f'Body{end}{side}', G(side * 0.33, 0.5, front), (0.17, 0.03, 0.05), 'PaintDark', subdivisions=2)
            ellipsoid(scene, f'Head{end}{side}', G(side * 0.17, 0.56, front), (0.04, 0.03, 0.04), 'PaintDark', subdivisions=2)
            ellipsoid(scene, f'Shield{end}{side}', G(side * 0.68, 0.47, front), (0.04, 0.02, 0.04), 'Gold', subdivisions=1)
        # Crowning palmettes: on the ridge and on both lower corners
        palmette(scene, f'Crown{end}', G(0.0, ridge + 0.05, end * half_z), 5, 0.14, 'Gold', is_across_z=False)
        for side in (-1, 1):
            palmette(scene, f'Corner{end}{side}', G(side * (half_x - 0.02), eave + 0.04, end * half_z), 3, 0.13, 'Gold', is_across_z=False)
    trim_height(scene, 1.0)
