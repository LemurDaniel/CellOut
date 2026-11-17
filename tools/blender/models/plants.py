# NOTE:
#   Undergrowth of the island meadows (assets/models/plant/), cartoony and soft like the
#   trees: a leafy shrub, a berry bush, a ground fern, clumps of daisies, bellflowers and
#   lupines, a mossy boulder. Every plant stands on the origin and is about 1 unit high,
#   the game scales it per instance (Core/PlantModels.cs, Creature/World/CoastProps.cs)
#   and sways it in the wind (shaders/tree.gdshader). Petals are white: the game colors
#   them per instance. No rig, low poly on purpose: hundreds stand at once.
#   Registered in build.models.py.

#
#   Chapters:
#     LEAVES    Leaf cards and the leafy dome they cover.
#     PLANTS    The seven plants of the meadow.
#

import math
import random

import bmesh
from mathutils import Vector

from models.helpers import (
    G,
    ellipsoid,
    link_mesh,
    tube,
)
from models.trees import bmesh_to_mesh, clouds_texture, frond

UP = Vector((0.0, 0.0, 1.0))
THIN = {'subdivisions': 1, 'max_pieces': 2}           # tube settings for stems


# ===========================================================================
# ===============================   LEAVES   ================================
# ===========================================================================
#
#   leaf_card     A pointed leaf: a diamond folded along its midrib, into a shared bmesh.
#   leafy_dome    A lumpy dark core covered in leaf cards that point outwards and up.
#   random_dir    A random direction on the upper part of a sphere.
#
#   Coordinates in Blender axes (z up) inside a builder, G() converts Godot points.
#

def leaf_card(bm, base, direction, length, width, fold=0.25):
    """A diamond from base along direction, its sides folded up by fold (a shallow V)."""
    direction = direction.normalized()
    helper = UP if abs(direction.dot(UP)) < 0.95 else Vector((1.0, 0.0, 0.0))
    side = direction.cross(helper).normalized()
    normal = side.cross(direction).normalized()
    if normal.dot(UP) < 0.0:
        normal = -normal
    middle = base + direction * (length * 0.45)
    lift = normal * (width * fold)
    verts = (
        bm.verts.new(base),
        bm.verts.new(middle + side * (width * 0.5) + lift),
        bm.verts.new(base + direction * length + normal * (length * 0.08)),
        bm.verts.new(middle - side * (width * 0.5) + lift),
        bm.verts.new(middle),
    )
    # Two halves along the midrib, so the fold shows in the light
    bm.faces.new((verts[0], verts[1], verts[2], verts[4]))
    bm.faces.new((verts[0], verts[4], verts[2], verts[3]))


def random_dir(rng, min_up):
    """A random unit vector whose z is at least min_up."""
    while True:
        v = Vector((rng.uniform(-1.0, 1.0), rng.uniform(-1.0, 1.0), rng.uniform(min_up, 1.0)))
        if 0.2 < v.length <= 1.0:
            return v.normalized()


def leafy_dome(scene, name, center, radii, count, leaf_length, rng, lobes=5):
    """
    A dome of foliage around center (Blender point), radii (x, y, z) of the dome:
      - a ring of lumpy dark lobes plus one on top fill the inside, so no gap shows through
      - count leaf cards sit on the dome surface, pointing outwards and a little up, so the
        outline is ragged and soft instead of a smooth ball
    """
    texture = clouds_texture()
    for i in range(lobes):
        angle = i / lobes * math.tau + rng.uniform(-0.3, 0.3)
        offset = Vector((math.cos(angle) * radii[0] * 0.45, math.sin(angle) * radii[1] * 0.45, -radii[2] * 0.15))
        size = rng.uniform(0.5, 0.62)
        ellipsoid(scene, f'{name}Lobe{i}', center + offset, (radii[0] * size, radii[1] * size, radii[2] * size * 0.9),
                  'LeafDark', subdivisions=2, texture=texture, strength=0.05)
    ellipsoid(scene, f'{name}Top', center + Vector((0.0, 0.0, radii[2] * 0.2)), (radii[0] * 0.62, radii[1] * 0.62, radii[2] * 0.7),
              'LeafDark', subdivisions=2, texture=texture, strength=0.05)

    # Leaves lie on the dome like shingles: mostly along the surface, tip a little out and down
    bm = bmesh.new()
    for _ in range(count):
        direction = random_dir(rng, -0.25)
        surface = center + Vector((direction.x * radii[0], direction.y * radii[1], direction.z * radii[2])) * rng.uniform(0.86, 1.0)
        tangent = direction.cross(random_dir(rng, -1.0)).normalized()
        out = (direction * 0.55 + tangent - UP * 0.25).normalized()
        length = leaf_length * rng.uniform(0.8, 1.2)
        leaf_card(bm, surface - out * (length * 0.35), out, length, length * 0.85, fold=0.3)
    return link_mesh(scene, f'{name}Leaves', bmesh_to_mesh(bm, f'{name}Leaves'), 'Leaf')


# ===========================================================================
# ===============================   PLANTS   ================================
# ===========================================================================
#
#   build_plant_shrub       A wide leafy shrub: two domes of leaf cards, twigs at the foot.
#   build_plant_berrybush   A rounder, smaller shrub with clusters of red berries.
#   build_plant_fernbush    A ground fern: arching fronds from one crown, curled young ones.
#   build_plant_daisy       A clump of daisies: white rays (colored by the game) around a golden eye.
#   build_plant_bell        Arching stems with nodding bells.
#   build_plant_lupine      Upright spikes of small blooms, smaller towards the tip.
#   build_plant_boulder     A lumpy flat-sided stone with moss on top.
#

def build_plant_shrub(scene):
    rng = random.Random(61)
    leafy_dome(scene, 'Main', G(0.0, 0.42, 0.0), (0.52, 0.48, 0.42), 170, 0.2, rng)
    leafy_dome(scene, 'Side', G(0.3, 0.3, 0.32), (0.34, 0.32, 0.3), 80, 0.17, rng, lobes=4)
    for i in range(4):
        angle = i / 4 * math.tau + 0.3
        tube(scene, f'Twig{i}', [G(0, 0, 0), G(math.cos(angle) * 0.2, 0.22, math.sin(angle) * 0.2)], [0.035, 0.02], [(0, 1)], 'Bark', **THIN)


def build_plant_berrybush(scene):
    rng = random.Random(67)
    leafy_dome(scene, 'Main', G(0.0, 0.45, 0.0), (0.42, 0.42, 0.42), 150, 0.17, rng)
    for k in range(9):
        direction = random_dir(rng, -0.1)
        cluster = G(0.0, 0.45, 0.0) + Vector((direction.x * 0.44, direction.y * 0.44, direction.z * 0.42))
        for b in range(3):
            berry = cluster + Vector((rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), -b * 0.035))
            ellipsoid(scene, f'Berry{k}_{b}', berry, (0.035, 0.035, 0.035), 'Berry', subdivisions=1)


def build_plant_fernbush(scene):
    rng = random.Random(71)
    base = G(0.0, 0.02, 0.0)
    for i in range(10):
        angle = (i + rng.uniform(-0.25, 0.25)) / 10 * math.tau
        heading = Vector((math.cos(angle), math.sin(angle), 0.0))
        frond(scene, f'Frond{i}', base, heading, rng.uniform(0.75, 0.95), 0.13, rng.uniform(1.1, 1.5), rng.uniform(1.2, 1.5))
    for i in range(3):
        angle = i / 3 * math.tau + 0.4
        center = base + G(math.cos(angle) * 0.05, 0.12, math.sin(angle) * 0.05)
        ellipsoid(scene, f'Curl{i}', center, (0.04, 0.04, 0.045), 'Frond', subdivisions=1)


def _flower_stem(scene, name, foot, head, lean, radius=0.012):
    """A thin bowed stem from foot to head (Blender points)."""
    middle = foot.lerp(head, 0.5) + lean
    tube(scene, name, [foot, middle, head], [radius * 1.3, radius, radius * 0.9], [(0, 1), (1, 2)], 'Stem', **THIN)


def _basal_leaves(scene, name, rng, count, length):
    bm = bmesh.new()
    for i in range(count):
        angle = (i + rng.uniform(-0.3, 0.3)) / count * math.tau
        out = Vector((math.cos(angle), math.sin(angle), rng.uniform(0.5, 0.9))).normalized()
        leaf_card(bm, G(0.0, 0.0, 0.0), out, length * rng.uniform(0.8, 1.1), length * 0.35, fold=0.35)
    link_mesh(scene, name, bmesh_to_mesh(bm, name), 'Stem')


def build_plant_daisy(scene):
    rng = random.Random(73)
    _basal_leaves(scene, 'Leaves', rng, 9, 0.34)
    heads = ((0.0, 0.0, 0.85), (0.24, 0.1, 0.62), (-0.2, 0.2, 0.7), (0.08, -0.26, 0.55), (-0.22, -0.14, 0.48))
    petals = bmesh.new()
    for i, (x, z, height) in enumerate(heads):
        head = G(x, height, z)
        _flower_stem(scene, f'Stem{i}', G(x * 0.3, 0.0, z * 0.3), head, G(x * 0.3, 0.0, z * 0.3), radius=0.016)
        # Rays: a flat ring of round-tipped petals, tilted towards the sky and a little outwards
        facing = (G(x, 0.0, z) * 1.2 + UP).normalized()
        side = facing.cross(Vector((1.0, 0.0, 0.0)) if abs(facing.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
        other = facing.cross(side).normalized()
        ellipsoid(scene, f'Eye{i}', head + facing * 0.02, (0.075, 0.075, 0.045), 'Pollen', subdivisions=2)
        for k in range(14):
            angle = k / 14 * math.tau + i
            out = (side * math.cos(angle) + other * math.sin(angle) + facing * 0.12).normalized()
            leaf_card(petals, head + out * 0.04, out, 0.17, 0.08, fold=0.08)
    link_mesh(scene, 'Petals', bmesh_to_mesh(petals, 'Petals'), 'Petal')


def build_plant_bell(scene):
    rng = random.Random(79)
    _basal_leaves(scene, 'Leaves', rng, 7, 0.3)
    for i in range(3):
        angle = i / 3 * math.tau + rng.uniform(-0.3, 0.3)
        direction = Vector((math.cos(angle), math.sin(angle), 0.0))
        # The stem rises and arches over outwards like a shepherd's crook
        height = rng.uniform(0.85, 1.0)
        points = [G(0, 0, 0), UP * height * 0.55 + direction * 0.04, UP * height * 0.92 + direction * 0.16,
                  UP * height + direction * 0.32, UP * height * 0.9 + direction * 0.44]
        tube(scene, f'Stem{i}', points, [0.02, 0.016, 0.014, 0.012, 0.01], [(k, k + 1) for k in range(4)], 'Stem', **THIN)
        # Bells hang from the arch, nodding: a cup that flares wide at its mouth
        for k, stalk in enumerate((points[2], points[3], points[4])):
            mouth = stalk - UP * 0.14 + direction * 0.02
            tube(scene, f'Bell{i}_{k}', [stalk, stalk.lerp(mouth, 0.45), mouth],
                 [0.022, 0.05, 0.075], [(0, 1), (1, 2)], 'Petal', **THIN)
            ellipsoid(scene, f'Clapper{i}_{k}', mouth - UP * 0.01, (0.02, 0.02, 0.02), 'Pollen', subdivisions=1)


def build_plant_lupine(scene):
    rng = random.Random(83)
    _basal_leaves(scene, 'Leaves', rng, 9, 0.28)
    spikes = ((0.0, 0.0, 1.0), (0.12, 0.1, 0.8), (-0.1, -0.08, 0.72))
    for i, (x, z, height) in enumerate(spikes):
        foot = G(x * 0.4, 0.0, z * 0.4)
        top = G(x, height, z)
        _flower_stem(scene, f'Stem{i}', foot, top, G(x * 0.2, 0.0, z * 0.2), radius=0.014)
        # Whorls of small blooms on the upper half, smaller towards the tip
        for k in range(7):
            share = 0.45 + k * 0.08
            center = foot.lerp(top, share)
            size = 0.05 * (1.2 - share * 0.8)
            for j in range(4):
                angle = j / 4 * math.tau + k * 0.8
                bloom = center + Vector((math.cos(angle), math.sin(angle), 0.0)) * size
                ellipsoid(scene, f'Bloom{i}_{k}_{j}', bloom, (size, size, size * 0.8), 'Petal', subdivisions=1)


def build_plant_boulder(scene):
    rng = random.Random(89)
    texture = clouds_texture()
    # Stone: a chunky ball pushed in and out per vertex, the bottom cut flat, flat shaded facets
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    for vert in bm.verts:
        bulge = rng.uniform(0.82, 1.12)
        x, y, z = vert.co.x * 0.62 * bulge, vert.co.y * 0.5 * bulge, vert.co.z * 0.45 * bulge
        vert.co = Vector((x, y, max(z, -0.25) + 0.25))
    mesh = bpy_mesh(bm, 'Stone')
    link_mesh(scene, 'Stone', mesh, 'Stone')
    # Moss: a flattened cushion on the top
    ellipsoid(scene, 'Moss', G(rng.uniform(-0.05, 0.05), 0.6, 0.0), (0.36, 0.32, 0.12), 'Moss', subdivisions=2, texture=texture, strength=0.04)


def bpy_mesh(bm, name):
    """A finished bmesh as flat shaded mesh data (stones keep their facets)."""
    mesh = bmesh_to_mesh(bm, name)
    for polygon in mesh.polygons:
        polygon.use_smooth = False
    return mesh
