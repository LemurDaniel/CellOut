# NOTE:
#   Props of pack and tribe (assets/models/village/): the woven nest of the pack and the
#   fire pit of the tribe. Both stand on the origin, about 1 unit across, the game scales
#   them (Tribe/Pack/Nest.cs). No rig, the flames of the fire are game primitives that
#   flicker and glow. Registered in build.models.py.

#
#   Chapters:
#     VILLAGE PARTS   Twigs, the nest and the fire pit.
#

import math
import random

from mathutils import Vector

from models.helpers import G, ellipsoid, tube
from models.trees import clouds_texture


# ===========================================================================
# ============================   VILLAGE PARTS   ============================
# ===========================================================================
#
#   twig_ring     A ring of short bent twigs lying tangentially around the nest.
#   build_nest    A mossy mound with a hollow, two rings of woven twigs, down and leaves.
#   build_fire    A ring of stones around crossed logs on a bed of embers.
#
#   Coordinates in Godot axes through G (+Y up), ellipsoid scales in Blender axes
#   (across, out, up) like the trees. Fixed seeds: the same model every build.
#

LOW_POLY = {'subdivisions': 1, 'max_pieces': 3}       # tube settings like the trees


def twig_ring(scene, prefix, rng, count, radius, height, thickness, arc):
    """count twigs around the ring at about radius and height, each a bent arc of 4 points."""
    for i in range(count):
        start = i / count * math.tau + rng.uniform(-0.1, 0.1)
        span = rng.uniform(arc * 0.7, arc * 1.3)
        lift = rng.uniform(-0.03, 0.03)
        points = []
        for k in range(4):
            share = k / 3
            angle = start + span * share
            r = radius + rng.uniform(-0.02, 0.02) + 0.02 * math.sin(share * math.pi)
            y = height + lift + 0.03 * math.sin(share * math.pi + rng.uniform(0, 1))
            points.append(G(math.cos(angle) * r, y, math.sin(angle) * r))
        radii = [thickness * 0.7, thickness, thickness, thickness * 0.6]
        tube(scene, f'{prefix}{i}', points, radii, [(0, 1), (1, 2), (2, 3)], 'Twig', **LOW_POLY)


def build_nest(scene):
    rng = random.Random(7)
    texture = clouds_texture()
    # Mossy mound with a flattened hollow of dark earth
    ellipsoid(scene, 'Mound', G(0, 0.0, 0), (0.44, 0.44, 0.11), 'Moss', subdivisions=3, texture=texture, strength=0.03)
    ellipsoid(scene, 'Hollow', G(0, 0.085, 0), (0.27, 0.27, 0.03), 'Earth', subdivisions=2)
    # Two rings of woven twigs: an upper tight ring and a lower looser one outside it
    twig_ring(scene, 'Twig', rng, count=26, radius=0.36, height=0.13, thickness=0.016, arc=0.5)
    twig_ring(scene, 'Outer', rng, count=18, radius=0.43, height=0.06, thickness=0.02, arc=0.42)
    # A few twig ends sticking out
    for i in range(7):
        angle = rng.uniform(0, math.tau)
        base = G(math.cos(angle) * 0.38, 0.12, math.sin(angle) * 0.38)
        tip = G(math.cos(angle + 0.3) * 0.55, 0.16 + rng.uniform(0, 0.08), math.sin(angle + 0.3) * 0.55)
        tube(scene, f'End{i}', [base, tip], [0.016, 0.008], [(0, 1)], 'Twig', **LOW_POLY)
    # Down feathers in the hollow, leaves on the rim
    for i in range(6):
        angle = rng.uniform(0, math.tau)
        r = rng.uniform(0.05, 0.2)
        ellipsoid(scene, f'Down{i}', G(math.cos(angle) * r, 0.11, math.sin(angle) * r), (0.05, 0.03, 0.012), 'Down', subdivisions=1)
    for i in range(5):
        angle = i / 5 * math.tau + 0.5
        ellipsoid(scene, f'Leaf{i}', G(math.cos(angle) * 0.4, 0.15, math.sin(angle) * 0.4), (0.07, 0.045, 0.01), 'Leaf', subdivisions=1)


def build_fire(scene):
    rng = random.Random(11)
    # Ring of stones
    for i in range(9):
        angle = i / 9 * math.tau + rng.uniform(-0.12, 0.12)
        size = rng.uniform(0.08, 0.12)
        ellipsoid(scene, f'Stone{i}', G(math.cos(angle) * 0.42, size * 0.5, math.sin(angle) * 0.42),
                  (size, size * rng.uniform(0.8, 1.1), size * rng.uniform(0.6, 0.85)), 'Stone', subdivisions=1)
    # Bed of embers, three logs crossing over it
    ellipsoid(scene, 'Embers', G(0, 0.02, 0), (0.24, 0.24, 0.035), 'Ember', subdivisions=2)
    for i in range(3):
        angle = i / 3 * math.tau + 0.4
        start = G(math.cos(angle) * 0.27, 0.05, math.sin(angle) * 0.27)
        end = G(-math.cos(angle) * 0.22, 0.11 + i * 0.03, -math.sin(angle) * 0.22)
        tube(scene, f'Log{i}', [start, end], [0.045, 0.038], [(0, 1)], 'Log', **LOW_POLY)
