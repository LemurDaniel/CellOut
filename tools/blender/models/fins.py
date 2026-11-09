# NOTE:
#   Fins of the organism stage: tail fin, flukes, back fin and paddles with rays,
#   webbing and a flutter rig (assets/models/creature/). Registered in build.models.py.

#
#   Chapters:
#     FIN PARTS   Tail fin, flukes, back fin and paddles: rays, webbing, flutter.
#

import math

import bmesh
import bpy

from models.helpers import (
    AXIS_OUT,
    AXIS_UP,
    FPS,
    G,
    animate,
    apply_modifiers,
    bind,
    ellipsoid,
    link_mesh,
    rig,
    tube,
    wave,
)


# ===========================================================================
# ==============================   FIN PARTS   ==============================
# ===========================================================================
#
#   fin               Rays, webbing with thickness and scalloped edge, fleshy root, a flutter rig.
#   build_caudal      Forked tail fin of fish: upright (XY), reaching back (-X).
#   build_fluke       Flukes of marine mammals: flat (XZ), two lobes reaching back.
#   build_dorsal      Back fin: upright, swept back, rising (+Y) from a base along X.
#   build_pectoral    Paddle of the flank: flat, reaching out (+Z) and back.
#
#   Every fin flutters in one second: each ray bends out of the fin plane, the wave
#   runs from the first ray to the last. The game rides it on the swim wave.
#

FIN_THICKNESS = 0.014
FIN_SCALLOP = 0.16                   # how far the edge dips between two rays, share of the ray length
FIN_ALONG = 8                        # webbing rows from root to edge
FIN_ACROSS = 4                       # webbing columns between two rays


def fin(scene, roots, tips, normal, lobe_center, lobe_scale, flutter_degrees):
    """
    A fin from rays (roots[i] -> tips[i], Godot axes via G). The webbing is a clean quad
    grid between every two neighbouring rays: rows from root to edge, columns across;
    the edge dips between the rays in a smooth arc (FIN_SCALLOP). It gets a thickness and
    smooth edges. Rays are thin tapering tubes ending just inside the edge, the root sits
    in a fleshy lobe (lobe_scale in Blender axes). normal is the fin plane normal (Blender
    axes): the rays bend about their in-plane perpendicular.
    """
    meshes = []

    # Webbing: one quad grid per gap between two rays, the edge dipping in an arc between them
    bm = bmesh.new()
    for i in range(len(roots) - 1):
        grid = []
        for row in range(FIN_ALONG + 1):
            along = row / FIN_ALONG
            line = []
            for column in range(FIN_ACROSS + 1):
                across = column / FIN_ACROSS
                root = roots[i].lerp(roots[i + 1], across)
                tip = tips[i].lerp(tips[i + 1], across)
                reach = 1.0 - FIN_SCALLOP * math.sin(across * math.pi)
                line.append(bm.verts.new(root.lerp(tip, along * reach)))
            grid.append(line)
        for row in range(FIN_ALONG):
            for column in range(FIN_ACROSS):
                bm.faces.new([grid[row][column], grid[row][column + 1], grid[row + 1][column + 1], grid[row + 1][column]])
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new('Web')
    bm.to_mesh(mesh)
    bm.free()
    web = link_mesh(scene, 'Web', mesh, 'Fin')
    solidify = web.modifiers.new('Thick', 'SOLIDIFY')
    solidify.thickness = FIN_THICKNESS
    solidify.offset = 0.0
    web.modifiers.new('Smooth', 'SUBSURF').levels = 1
    apply_modifiers(web)
    for polygon in web.data.polygons:
        polygon.use_smooth = True
    meshes.append(web)

    # Rays: thin tubes from the root to just inside the edge, standing out a little on both sides
    for i, (root, tip) in enumerate(zip(roots, tips)):
        end = root.lerp(tip, 0.94)
        meshes.append(tube(scene, f'Ray{i}', [root, root.lerp(end, 0.5), end], (0.013, 0.008, 0.004), [(0, 1), (1, 2)], 'FinRay'))

    # Fleshy root the rays grow out of
    meshes.append(ellipsoid(scene, 'Root', lobe_center, lobe_scale, 'Skin', subdivisions=3))

    # One bone per ray, the flutter wave runs along them
    bones = [(f'Ray{i}', root, tip, None) for i, (root, tip) in enumerate(zip(roots, tips))]
    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes)
    curves = {}
    for i, (root, tip) in enumerate(zip(roots, tips)):
        axis = (tip - root).cross(normal).normalized()
        curves[f'Ray{i}'] = (axis, lambda share, phase=-i * 0.1: flutter_degrees * wave(share, phase))
    animate(armature, 'Flutter', FPS, curves)


def build_caudal(scene):
    roots = [G(0.0, 0.07, 0), G(0.0, 0.03, 0), G(0.0, 0.0, 0), G(0.0, -0.03, 0), G(0.0, -0.07, 0)]
    tips = [G(-0.46, 0.56, 0), G(-0.38, 0.3, 0), G(-0.26, 0.0, 0), G(-0.38, -0.3, 0), G(-0.46, -0.56, 0)]
    fin(scene, roots, tips, AXIS_OUT, G(0.0, 0.0, 0), (0.09, 0.03, 0.1), 14.0)


def build_fluke(scene):
    roots = [G(0.0, 0, 0.07), G(0.0, 0, 0.03), G(0.0, 0, 0.0), G(0.0, 0, -0.03), G(0.0, 0, -0.07)]
    tips = [G(-0.44, 0, 0.58), G(-0.4, 0, 0.32), G(-0.22, 0, 0.0), G(-0.4, 0, -0.32), G(-0.44, 0, -0.58)]
    fin(scene, roots, tips, AXIS_UP, G(0.0, 0.0, 0), (0.09, 0.1, 0.035), 12.0)


def build_dorsal(scene):
    roots = [G(0.16, 0, 0), G(0.06, 0, 0), G(-0.05, 0, 0), G(-0.16, 0, 0)]
    tips = [G(0.04, 0.33, 0), G(-0.07, 0.4, 0), G(-0.18, 0.4, 0), G(-0.26, 0.16, 0)]
    fin(scene, roots, tips, AXIS_OUT, G(0.0, 0.0, 0), (0.2, 0.035, 0.05), 8.0)


def build_pectoral(scene):
    roots = [G(0.1, 0, 0.0), G(0.03, 0, 0.0), G(-0.04, 0, 0.0), G(-0.1, 0, 0.0)]
    tips = [G(0.06, 0, 0.26), G(-0.06, 0, 0.36), G(-0.16, 0, 0.32), G(-0.2, 0, 0.16)]
    fin(scene, roots, tips, AXIS_UP, G(0.0, 0.0, 0.0), (0.12, 0.05, 0.035), 18.0)
