# NOTE:
#   Primeval trees of the islands (assets/models/tree/), cartoony and soft like the
#   creatures: tree fern, cycad, giant horsetail, gnarled broadleaf, scale tree giant.
#   Every tree stands on the origin and is about 1 unit high, the game scales it per
#   instance (Core/TreeModels.cs, World/Coast.cs) and sways it in the wind (shaders/tree.gdshader).
#   No rig, low poly on purpose: hundreds stand at once. Registered in build.models.py.

#
#   Chapters:
#     TREE PARTS   Fronds, ringed trunks and the five trees.
#

import math
import random

import bmesh
import bpy
from mathutils import Vector

from models.helpers import (
    G,
    cone,
    ellipsoid,
    link_mesh,
    tube,
)


# ===========================================================================
# =============================   TREE PARTS   ==============================
# ===========================================================================
#
#   frond                 A leaf blade with serrated edges along an arching spine, two sided.
#   bmesh_to_mesh         A finished bmesh as smooth shaded mesh data.
#   crown                 A ring of fronds around a point.
#   ringed_trunk          A trunk tube with a ring bulge every few points.
#   build_tree_fern       Shaggy trunk, a crown of arching fronds, curled young fronds.
#   build_tree_cycad      Leaning ringed trunk for the beach, long fronds, orange seed cones.
#   build_tree_horsetail  Segmented green trunk, whorls of needles at every node, a cone on top.
#   build_tree_broadleaf  Gnarled forked trunk with root flares and fluffy round crowns.
#   build_tree_giant      Tall scale tree: buttress roots, a crown forking three times, tufted tips.
#   clouds_texture        Shared cloud noise that makes the round crowns fluffy.
#
#   Coordinates in Godot axes through G (+Y up). Fixed seeds: the same tree every build.
#

UP = Vector((0.0, 0.0, 1.0))
LOW_POLY = {'subdivisions': 1, 'max_pieces': 3}       # tube settings: hundreds of trees stand at once


def frond(scene, name, base, heading, length, width, rise, droop, segments=12):
    """
    A two sided leaf blade along an arching spine:
      spine(t) = base + heading * length * t + up * length * (rise * t - droop * t^2)
      width(t) = width * sin(pi * t)^0.7, every other sample narrower (serrated edge)
    The blade folds a little down to both sides (a shallow V), so it has volume.
    """
    heading = Vector((heading.x, heading.y, 0.0)).normalized()
    side = heading.cross(UP).normalized()
    bm = bmesh.new()
    spine, left, right = [], [], []
    for i in range(segments + 1):
        t = i / segments
        point = base + heading * (length * t) + UP * (length * (rise * t - droop * t * t))
        blade = width * math.sin(math.pi * t) ** 0.7 * (1.0 if 0 == i % 2 else 0.6)
        fold = UP * (-blade * 0.25)
        spine.append(bm.verts.new(point))
        left.append(bm.verts.new(point + side * blade + fold))
        right.append(bm.verts.new(point - side * blade + fold))
    for i in range(segments):
        bm.faces.new((spine[i], spine[i + 1], left[i + 1], left[i]))
        bm.faces.new((spine[i], right[i], right[i + 1], spine[i + 1]))
    mesh = bmesh_to_mesh(bm, name)
    return link_mesh(scene, name, mesh, 'Frond')


def bmesh_to_mesh(bm, name):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    return mesh


def crown(scene, name, top, count, length, width, rise, droop, rng, twist=0.0):
    """count fronds around top, slightly irregular in angle, length and droop."""
    for i in range(count):
        angle = (i + rng.uniform(-0.2, 0.2)) / count * math.tau + twist
        heading = Vector((math.cos(angle), math.sin(angle), 0.0))
        frond(scene, f'{name}{i}', top, heading, length * rng.uniform(0.85, 1.1), width,
              rise * rng.uniform(0.8, 1.2), droop * rng.uniform(0.85, 1.15))


def ringed_trunk(scene, name, points, radius_bottom, radius_top, ring_every, material_name):
    """A tube through points (Blender vectors), every ring_every-th point bulges as a ring."""
    count = len(points)
    radii = []
    for i in range(count):
        radius = radius_bottom + (radius_top - radius_bottom) * i / (count - 1)
        radii.append(radius * (1.18 if i > 0 and 0 == i % ring_every else 1.0))
    return tube(scene, name, points, radii, [(i, i + 1) for i in range(count - 1)], material_name, **LOW_POLY)


def _line(start, end, count, bend=Vector((0.0, 0.0, 0.0))):
    """count points from start to end, bowing sideways by bend in the middle."""
    return [start.lerp(end, t) + bend * math.sin(math.pi * t) for t in (i / (count - 1) for i in range(count))]


def build_tree_fern(scene):
    rng = random.Random(11)
    top = G(0.03, 0.72, 0.0)
    tube(scene, 'Trunk', [G(0, 0, 0), G(0.02, 0.25, 0.0), G(0.0, 0.5, 0.02), top],
         [0.07, 0.055, 0.05, 0.06], [(0, 1), (1, 2), (2, 3)], 'Bark', **LOW_POLY)
    # Shaggy stubs of old fronds along the trunk
    for i in range(14):
        height = rng.uniform(0.15, 0.68)
        angle = rng.uniform(0.0, math.tau)
        center = G(0.01 + math.cos(angle) * 0.055, height, math.sin(angle) * 0.055)
        ellipsoid(scene, f'Stub{i}', center, (0.03, 0.03, 0.02), 'Bark', subdivisions=1)
    crown(scene, 'Frond', top, 11, 0.55, 0.1, 0.55, 0.75, rng)
    # Young fronds still curled up in the middle
    for i in range(3):
        angle = i / 3 * math.tau
        center = top + G(math.cos(angle) * 0.04, 0.07, math.sin(angle) * 0.04)
        ellipsoid(scene, f'Curl{i}', center, (0.035, 0.035, 0.035), 'Frond', subdivisions=1)


def build_tree_cycad(scene):
    rng = random.Random(23)
    points = _line(G(0, 0, 0), G(0.3, 0.82, 0.0), 14, bend=G(-0.06, 0.0, 0.0))
    ringed_trunk(scene, 'Trunk', points, 0.075, 0.06, 2, 'Bark')
    top = points[-1] + G(0.0, 0.03, 0.0)
    crown(scene, 'Frond', top, 13, 0.6, 0.07, 0.45, 0.85, rng)
    crown(scene, 'Young', top, 4, 0.3, 0.05, 1.3, 0.6, rng, twist=0.4)
    for i in range(3):
        angle = i / 3 * math.tau + 0.5
        center = top + G(math.cos(angle) * 0.05, 0.0, math.sin(angle) * 0.05)
        ellipsoid(scene, f'Seed{i}', center, (0.04, 0.04, 0.05), 'Cone', subdivisions=1)


def build_tree_horsetail(scene):
    rng = random.Random(37)
    points = _line(G(0, 0, 0), G(0.0, 0.95, 0.0), 20, bend=G(0.03, 0.0, 0.02))
    ringed_trunk(scene, 'Trunk', points, 0.055, 0.03, 2, 'Horsetail')
    # A whorl of needles at every node above the lower third, shorter towards the top
    for node in range(6, 19, 2):
        center = points[node]
        height = node / 19
        reach = 0.2 * (1.15 - height)
        for k in range(9):
            angle = (k + rng.uniform(-0.2, 0.2)) / 9 * math.tau + node * 0.35
            tip = center + Vector((math.cos(angle) * reach, math.sin(angle) * reach, reach * 0.6))
            cone(scene, f'Needle{node}_{k}', center, tip, 0.012, 'Needle')
    ellipsoid(scene, 'Strobilus', points[-1] + G(0.0, 0.05, 0.0), (0.035, 0.035, 0.07), 'Cone', subdivisions=2)


def build_tree_broadleaf(scene):
    rng = random.Random(41)
    texture = clouds_texture()
    # Trunk forking into four gnarled limbs
    points = [
        G(0, 0, 0), G(0.03, 0.22, 0.0), G(-0.02, 0.42, 0.01),                        # 0-2 trunk
        G(0.16, 0.58, 0.06), G(0.27, 0.72, 0.1),                                     # 3-4
        G(-0.17, 0.6, -0.05), G(-0.26, 0.74, -0.13),                                 # 5-6
        G(0.02, 0.62, -0.17), G(0.05, 0.78, -0.24),                                  # 7-8
        G(-0.03, 0.66, 0.15), G(-0.06, 0.82, 0.22),                                  # 9-10
    ]
    radii = [0.09, 0.07, 0.065, 0.045, 0.03, 0.045, 0.03, 0.04, 0.028, 0.04, 0.028]
    edges = [(0, 1), (1, 2), (2, 3), (3, 4), (2, 5), (5, 6), (2, 7), (7, 8), (2, 9), (9, 10)]
    tube(scene, 'Trunk', points, radii, edges, 'Bark', **LOW_POLY)
    # Root flares
    for i in range(4):
        angle = i / 4 * math.tau + 0.4
        tip = G(math.cos(angle) * 0.16, -0.01, math.sin(angle) * 0.16)
        tube(scene, f'Root{i}', [G(0, 0.1, 0), tip], [0.05, 0.02], [(0, 1)], 'Bark', **LOW_POLY)
    # Fluffy crowns on every limb end and one on top, darker ones underneath
    for i, (center, radius) in enumerate(((points[4], 0.22), (points[6], 0.21), (points[8], 0.2), (points[10], 0.22), (G(0.0, 0.92, 0.0), 0.25))):
        ellipsoid(scene, f'Crown{i}', center + G(0, 0.05, 0), (radius, radius, radius * 0.8), 'Leaf',
                  subdivisions=3, texture=texture, strength=0.07)
        ellipsoid(scene, f'Shade{i}', center + G(rng.uniform(-0.05, 0.05), -0.06, rng.uniform(-0.05, 0.05)),
                  (radius * 0.75, radius * 0.75, radius * 0.5), 'LeafDark', subdivisions=1)


def build_tree_giant(scene):
    rng = random.Random(53)
    # Trunk and a crown forking three times (2, 4, 8 tips), one tube tree
    points = [G(0, 0, 0), G(0.0, 0.35, 0.0), G(0.01, 0.7, 0.0)]
    radii = [0.07, 0.055, 0.045]
    edges = [(0, 1), (1, 2)]
    tips = []

    def fork(parent, heading, length, radius, depth):
        for side in (-1.0, 1.0):
            spread = 0.55 if 2 == depth else 0.45
            direction = (heading + Vector((side * spread * math.cos(depth * 1.3), side * spread * math.sin(depth * 1.3), 0.0))).normalized()
            end = points[parent] + direction * length
            points.append(end)
            radii.append(radius)
            edges.append((parent, len(points) - 1))
            if depth > 0:
                fork(len(points) - 1, (direction + UP * 0.6).normalized(), length * 0.75, radius * 0.7, depth - 1)
            else:
                tips.append((end, direction))

    fork(2, UP, 0.16, 0.032, 2)
    tube(scene, 'Trunk', points, radii, edges, 'Bark', **LOW_POLY)
    # Buttress roots
    for i in range(5):
        angle = i / 5 * math.tau
        tube(scene, f'Root{i}', [G(0, 0.12, 0), G(math.cos(angle) * 0.13, -0.01, math.sin(angle) * 0.13)], [0.045, 0.018], [(0, 1)], 'Bark', **LOW_POLY)
    # Tufts of grassy leaves on every tip
    for i, (tip, direction) in enumerate(tips):
        ellipsoid(scene, f'Bud{i}', tip, (0.035, 0.035, 0.035), 'LeafDark', subdivisions=1)
        for k in range(7):
            angle = k / 7 * math.tau + rng.uniform(-0.3, 0.3)
            out = (direction + Vector((math.cos(angle), math.sin(angle), 0.2)) * 0.9).normalized()
            cone(scene, f'Leaf{i}_{k}', tip, tip + out * rng.uniform(0.09, 0.13), 0.018, 'Leaf')


def clouds_texture():
    texture = bpy.data.textures.get('CO_LeafClouds') or bpy.data.textures.new('CO_LeafClouds', 'CLOUDS')
    texture.noise_scale = 0.12
    return texture
