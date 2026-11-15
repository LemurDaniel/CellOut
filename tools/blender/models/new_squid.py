# NOTE:
#   New parts for the squid of the organism stage (assets/models/creature/): the mantle
#   fins, two fleshy triangles around a pointed tail tip, and the siphon, the funnel
#   under the head. Not registered in build.models.py yet, previewed with Show-Model.ps1.
#   Model spaces:
#   - Mantle fins (Tail): root at the origin at the tail tip, the point reaches back
#     (-X), the fins lie flat (XZ) and run forward along the mantle (+X), about 1 unit.
#   - Siphon (Belly): base at the origin under the belly, the funnel points down (-Y)
#     and forward (+X), about 0.5 units.
#
#   ./tools/blender/Show-Model.ps1 -Module new_squid -Call 'build_mantle_fins(scene)' -Name mantlefins
#   ./tools/blender/Show-Model.ps1 -Module new_squid -Call 'build_siphon(scene)' -Name siphon

#
#   Chapters:
#     SQUID PARTS   Mantle fins and siphon.
#

import math

import bmesh
import bpy
from mathutils import Matrix

from models.helpers import (
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

FIN_FRONT_X = 0.8                    # where the fins start on the mantle
FIN_BACK_X = -0.4                    # where they meet behind the tail tip
FIN_WIDTH = 0.85                     # reach of one fin out of the midline
FIN_PEAK = 0.58                      # share of the fin length where it is the widest (behind the middle: an arrowhead)
FIN_THICKNESS = 0.07                 # fleshy, not a see-through fish fin
FIN_ALONG = 12                       # quad rows from front to back
FIN_ACROSS = 5                       # quad columns from the midline to the edge
FIN_RAYS = 4                         # flutter bones per fin


# ===========================================================================
# =============================   SQUID PARTS   =============================
# ===========================================================================
#
#   fin_edge            A point on the outline of the right mantle fin.
#   mantle_fin          One fleshy fin: a rounded triangle with thickness.
#   build_mantle_fins   Squid tail: a pointed mantle tip between two triangular fins, the fins ripple.
#   build_siphon        Squid funnel: a short hose under the head with a dark opening, the nozzle nods.
#

def fin_edge(share):
    """Outline of the right fin from its front (0) to the tail point (1): x along the mantle, z out."""
    x = FIN_FRONT_X + (FIN_BACK_X - FIN_FRONT_X) * share
    # Two halves meeting at the peak, a long leading and a short trailing edge: half sine, half straight ramp (a triangle with a soft corner)
    bend = share / FIN_PEAK * 0.5 if share < FIN_PEAK else 0.5 + (share - FIN_PEAK) / (1.0 - FIN_PEAK) * 0.5
    ramp = 1.0 - abs(bend - 0.5) * 2.0
    return x, FIN_WIDTH * (0.5 * math.sin(bend * math.pi) + 0.5 * ramp ** 0.8)


def mantle_fin(scene, name, side):
    """One fin as a quad grid from the midline to its outline, thick at the root, smooth edges."""
    bm = bmesh.new()
    grid = []
    for row in range(FIN_ALONG + 1):
        x, reach = fin_edge(row / FIN_ALONG)
        grid.append([bm.verts.new(G(x, 0.0, side * reach * column / FIN_ACROSS)) for column in range(FIN_ACROSS + 1)])
    for row in range(FIN_ALONG):
        for column in range(FIN_ACROSS):
            bm.faces.new([grid[row][column], grid[row][column + 1], grid[row + 1][column + 1], grid[row + 1][column]])
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    fin = link_mesh(scene, name, mesh, 'Skin')
    solidify = fin.modifiers.new('Thick', 'SOLIDIFY')
    solidify.thickness = FIN_THICKNESS
    solidify.offset = 0.0
    fin.modifiers.new('Smooth', 'SUBSURF').levels = 2
    apply_modifiers(fin)
    for polygon in fin.data.polygons:
        polygon.use_smooth = True
    return fin


def build_mantle_fins(scene):
    """
    The tail of a squid instead of a fish tail fin: the mantle ends in a point reaching
    back (-X), two fleshy triangular fins lie flat on both sides of it and run forward
    along the mantle, together an arrowhead. The fins ripple from front to back in one
    second ("Flutter").
    """
    # The pointed mantle tip: it covers the blunt tail end of the body
    tip_points = [G(0.78, 0, 0), G(0.35, 0, 0), G(0.0, 0, 0), G(-0.22, 0, 0), G(-0.4, 0, 0)]
    tip = tube(scene, 'Tip', tip_points, (0.22, 0.18, 0.12, 0.065, 0.015), [(i, i + 1) for i in range(len(tip_points) - 1)])
    meshes = [tip]
    bones = [('Core', G(FIN_FRONT_X, 0, 0), G(FIN_BACK_X, 0, 0), None)]
    curves = {}
    for side, label in ((1, 'R'), (-1, 'L')):
        meshes.append(mantle_fin(scene, f'Fin{label}', side))
        # A fleshy ridge where the fin grows out of the mantle
        ridge = [G(0.66, 0, side * 0.1), G(0.2, 0, side * 0.12), G(-0.2, 0, side * 0.06)]
        meshes.append(tube(scene, f'Ridge{label}', ridge, (0.06, 0.075, 0.04), [(0, 1), (1, 2)], 'Skin'))
        for i in range(FIN_RAYS):
            share = (i + 0.5) / FIN_RAYS
            x, reach = fin_edge(share)
            root, end = G(x, 0, side * 0.1), G(x, 0, side * max(reach, 0.14))
            bones.append((f'Ray{label}{i}', root, end, 'Core'))
            axis = (end - root).cross(AXIS_UP).normalized()
            curves[f'Ray{label}{i}'] = (axis, lambda share, phase=-i * 0.18: 7.0 * wave(share, phase))

    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes, rigid={'Tip': 'Core'})
    animate(armature, 'Flutter', FPS, curves)


def build_siphon(scene):
    """
    The funnel a squid squirts water through: a short hose growing out of the belly,
    pointing down and forward, with a soft rim around a dark opening. The nozzle nods
    back and forth in one second ("Puff").
    """
    points = [G(-0.16, 0.06, 0), G(0.0, -0.06, 0), G(0.16, -0.2, 0), G(0.3, -0.3, 0)]
    hose = tube(scene, 'Hose', points, (0.16, 0.13, 0.1, 0.085), [(i, i + 1) for i in range(len(points) - 1)])
    direction = (points[3] - points[2]).normalized()
    mouth = points[3] + direction * 0.07
    # Rim and opening: flat discs turned to face along the hose
    turn = AXIS_UP.rotation_difference(direction).to_matrix().to_4x4()
    rim = ellipsoid(scene, 'Rim', (0, 0, 0), (0.125, 0.125, 0.05), 'Pad', subdivisions=3)
    hole = ellipsoid(scene, 'Hole', (0, 0, 0), (0.08, 0.08, 0.04), 'Dark', subdivisions=2)
    for disc, push in ((rim, 0.0), (hole, 0.028)):
        disc.data.transform(turn)
        disc.data.transform(Matrix.Translation(mouth + direction * push))
    meshes = [hose, rim, hole]

    armature = rig(scene, 'Rig', [
        ('Base', points[0], points[2], None),
        ('Nozzle', points[2], mouth, 'Base'),
    ])
    bind(armature, meshes, rigid={'Rim': 'Nozzle', 'Hole': 'Nozzle'})
    # Puff: the nozzle nods forward and back like a hose under pressure
    animate(armature, 'Puff', FPS, {
        'Nozzle': (G(0, 0, 1), lambda share: 10.0 * wave(share)),
    })

