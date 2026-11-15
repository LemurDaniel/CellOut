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
BEAK_LIP_X = 0.98                    # where the lip ring lies around the beak, snout space
BEAK_LIP_RADIUS = 0.3
BEAK_FORWARD = 3.0                   # stretch of the beak along the snout
BEAK_ACROSS = 2.2                    # growth of the beak across the snout
IRIS_RADIUS = 0.74                   # golden iris, share of the eyeball
IRIS_CENTER_OUT = 0.48               # the iris dome is a flat ball pushed out of the eyeball
IRIS_BULGE = 0.42


# ===========================================================================
# =============================   SQUID PARTS   =============================
# ===========================================================================
#
#   fin_edge            A point on the outline of the right mantle fin.
#   mantle_fin          One fleshy fin: a rounded triangle with thickness.
#   build_mantle_fins   Squid tail: a pointed mantle tip between two triangular fins, the fins ripple.
#   build_siphon        Squid funnel: a short hose under the head with a dark opening, the nozzle nods.
#   build_squidbeak     Dark hooked beak in a pink lip ring, at the nose tip between the arms.
#   pupil_height        How far the iris dome stands out at a spot of the eye.
#   build_squideye      Flat eye in a skin socket: golden iris, W pupil, heavy upper lid.
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


def build_squidbeak(scene):
    """
    The beak of a squid in the middle of its arm crown (model space Snout: x 0 at the
    mouth corners .. 1 at the nose tip): a dark parrot beak poking out of the nose tip,
    the upper half hooks down over the shorter lower half, a fleshy pink lip ring lies
    around its base. No rig.
    """
    upper = [G(0.86, 0.13, 0), G(1.08, 0.16, 0), G(1.26, 0.07, 0), G(1.34, -0.12, 0)]
    lower = [G(0.86, -0.17, 0), G(1.04, -0.21, 0), G(1.17, -0.15, 0), G(1.21, -0.03, 0)]
    top = tube(scene, 'Upper', upper, (0.2, 0.18, 0.11, 0.02), [(0, 1), (1, 2), (2, 3)], 'Dark')
    bottom = tube(scene, 'Lower', lower, (0.16, 0.14, 0.085, 0.02), [(0, 1), (1, 2), (2, 3)], 'Dark')
    # Lip ring around the base of the beak, a closed loop in the plane across the snout
    count = 14
    ring = [G(BEAK_LIP_X, -0.02 + BEAK_LIP_RADIUS * math.cos(i / count * math.tau), BEAK_LIP_RADIUS * math.sin(i / count * math.tau)) for i in range(count)]
    lip = tube(scene, 'Lip', ring, [0.085] * count, [(i, (i + 1) % count) for i in range(count)], 'Pad')
    # The snout space is short along x (the mouth line of this kind is short) and narrow at the nose tip:
    # grow the beak across and stretch it forward so it shows between the arms
    for part in (top, bottom, lip):
        for vertex in part.data.vertices:
            vertex.co.x = BEAK_LIP_X + (vertex.co.x - BEAK_LIP_X) * BEAK_FORWARD
            vertex.co.y *= BEAK_ACROSS
            vertex.co.z *= BEAK_ACROSS


def pupil_height(x, y):
    """How far the iris dome stands out of the head at a spot of the eye (x forward, y up)."""
    inside = max(0.0, 1.0 - (x * x + y * y) / (IRIS_RADIUS * IRIS_RADIUS))
    return IRIS_CENTER_OUT + IRIS_BULGE * math.sqrt(inside)


def build_squideye(scene):
    """
    The eye of a squid (model space Eye: eyeball of radius 1 at the origin on the right
    flank, +Z out of the head, +X forward): a flat silvery ball in a soft socket ring,
    a big golden iris, the pupil a lying W, a heavy upper lid that makes it look calm,
    one highlight. No rig, the game blinks it.
    """
    ellipsoid(scene, 'Ball', G(0, 0, 0), (1.0, 0.8, 1.0), 'EyeWhite', subdivisions=3)
    ellipsoid(scene, 'Iris', G(0, 0, IRIS_CENTER_OUT), (IRIS_RADIUS, IRIS_BULGE, IRIS_RADIUS), 'Beak', subdivisions=3)

    # Pupil: a lying W on the iris dome, built from its middle so both ends get a round cap
    stroke = [(0.0, 0.07), (-0.2, -0.13), (-0.42, 0.1), (0.2, -0.13), (0.42, 0.1)]
    points = [G(x, y - 0.04, pupil_height(x, y) - 0.03) for x, y in stroke]
    tube(scene, 'PupilW', points, (0.1, 0.11, 0.09, 0.11, 0.09), [(0, 1), (1, 2), (0, 3), (3, 4)], 'Pupil')
    ellipsoid(scene, 'Glint', G(0.3, 0.26, pupil_height(0.3, 0.26) + 0.02), (0.13, 0.07, 0.13), 'Glint', subdivisions=2)

    # Socket: a soft ring of skin the eye sits in
    count = 18
    ring = [G(1.04 * math.cos(i / count * math.tau), 1.04 * math.sin(i / count * math.tau), 0.02) for i in range(count)]
    tube(scene, 'Socket', ring, [0.14] * count, [(i, (i + 1) % count) for i in range(count)], 'Skin')
    # Upper lid: a cap of skin over the top quarter
    ellipsoid(scene, 'Lid', G(0, 0.8, -0.05), (0.98, 0.8, 0.44), 'Skin', subdivisions=3)
