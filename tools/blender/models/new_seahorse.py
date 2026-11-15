# NOTE:
#   New parts for the seahorse and the moray eel of the organism stage: the curled
#   grasping tail, the tube snout and the turret eye of the seahorse, the gaping maw
#   and the fin ribbon of the eel. Registered in build.models.py.
#   Model spaces (Visual/CreatureModels.cs):
#   > Curl tail:   Tail, root at the origin at the tail tip, reaching back (-X), about 1 unit.
#   > Tube snout:  Snout, x = 0 at the mouth corners, x = 1 at the nose tip. The tube reaches far past the nose tip.
#   > Turret eye:  Eye, an eyeball of radius 1 at the origin on the right flank, +Z out of the head.
#   > Gape maw:    Snout, nostril tubes on the nose; opening and teeth come from data/mouths.json.
#   > Fin ribbon:  Hug, x -0.5 .. 0.5 along the body (half its length), y = 1 the back, z = 1 the flank.
#
#   ./tools/blender/Show-Model.ps1 -Module new_seahorse -Call 'build_curltail(scene)' -Name curltail
#   ./tools/blender/Show-Model.ps1 -Module new_seahorse -Call 'build_tubesnout(scene)' -Name tubesnout

#
#   Chapters:
#     SEAHORSE PARTS   Curled tail, tube snout and turret eye.
#     EEL PARTS        Gaping maw and fin ribbon.
#

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

from models.helpers import (
    AXIS_OUT,
    FPS,
    G,
    animate,
    apply_modifiers,
    bind,
    cone,
    ellipsoid,
    link_mesh,
    rig,
    tube,
    wave,
)


# ===========================================================================
# ===========================   SEAHORSE PARTS   ============================
# ===========================================================================
#
#   spiral_point      A point of the tail spiral and its distance to the spiral center.
#   build_curltail    Grasping tail: hangs down behind the body and curls forward into a spiral, bony rings.
#   tilted_disc       A flat ellipsoid standing across a direction in the XY plane.
#   build_tubesnout   Tube snout: a muzzle over the nose, a tapering pipe and a small mouth.
#   build_turreteye   Turret eye: a cone of skin with a golden eye in its tip, a bony spike above.
#
#   The tail curls tighter and loosens again in two seconds ("Curl"), the snout stands still.
#

CURL_CENTER_Y = -0.55                # the spiral center hangs this far below the tail tip
CURL_START_RADIUS = 0.55             # spiral radius at the tail tip
CURL_END_RADIUS = 0.09               # spiral radius at the curled-in tip
CURL_DEGREES = 560.0                 # the spiral turns this far from tail tip to its end
CURL_STEP = 10.0                     # one tube point every this many degrees
CURL_RING_EVERY = 3                  # every n-th tube point is a bony ring
CURL_ROOT_GIRTH = 0.135              # tube radius at the tail tip
CURL_TAPER = 0.62                    # the tube thins with the spiral radius to this power
CURL_BONE_DEGREES = (0.0, 100.0, 200.0, 310.0, 430.0, CURL_DEGREES)

SNOUT_PATH = (                       # (x, y, radius) from the muzzle over the nose to the mouth
    (0.1, 0.0, 0.88),
    (0.6, -0.02, 0.78),
    (1.1, -0.08, 0.56),
    (1.7, -0.16, 0.42),
    (2.3, -0.24, 0.36),
    (2.8, -0.3, 0.4),
)
SNOUT_END_GIRTH = 0.12               # the pipe closes to this inside the lip, so its round cap stays hidden


def spiral_point(degrees):
    """
    The tail leaves the origin backwards (-X), turns down, forward and up again while
    the radius shrinks (a logarithmic spiral around a center below the tail tip).
    Returns the point (Blender vector) and the spiral radius there.
    """
    share = degrees / CURL_DEGREES
    radius = CURL_START_RADIUS * (CURL_END_RADIUS / CURL_START_RADIUS) ** share
    angle = math.radians(degrees)
    return G(-math.sin(angle) * radius, CURL_CENTER_Y + math.cos(angle) * radius, 0.0), radius


def build_curltail(scene):
    # Tube: a short root inside the tail cap of the body, then the spiral, every third point a ring
    points = [G(0.3, 0.0, 0.0), G(0.14, 0.0, 0.0)]
    radii = [CURL_ROOT_GIRTH * 1.1, CURL_ROOT_GIRTH * 1.05]
    knobs = []
    steps = int(CURL_DEGREES / CURL_STEP)
    for step in range(steps + 1):
        point, radius = spiral_point(step * CURL_STEP)
        girth = CURL_ROOT_GIRTH * (radius / CURL_START_RADIUS) ** CURL_TAPER
        # The last half turn stays smooth: rings that small only crumple the tip
        is_ring = 0 == step % CURL_RING_EVERY and step * CURL_STEP < CURL_DEGREES - 200.0
        points.append(point)
        radii.append(girth * (1.2 if is_ring else 0.9))
        if is_ring:
            knobs.append((step * CURL_STEP, girth))
    meshes = [tube(scene, 'Tail', points, radii, [(i, i + 1) for i in range(len(points) - 1)], 'Skin')]

    # Bony knobs on the outside of every ring, the back edge of the tail
    for index, (degrees, girth) in enumerate(knobs):
        point, radius = spiral_point(degrees)
        center = G(0.0, CURL_CENTER_Y, 0.0)
        outwards = (point - center).normalized()
        meshes.append(ellipsoid(scene, f'Knob{index}', point + outwards * girth * 1.15, (girth * 0.5,) * 3, 'ShellRim', subdivisions=2))

    # Bones along the spiral, each curls a little further than its parent
    bones = [('Root', G(0.3, 0.0, 0.0), spiral_point(0.0)[0], None)]
    names = ['Root']
    for index in range(len(CURL_BONE_DEGREES) - 1):
        name = f'Curl{index}'
        bones.append((name, spiral_point(CURL_BONE_DEGREES[index])[0], spiral_point(CURL_BONE_DEGREES[index + 1])[0], names[-1]))
        names.append(name)
    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes)
    animate(armature, 'Curl', FPS * 2, {
        name: (AXIS_OUT, lambda share, phase=-index * 0.08: 5.0 * wave(share, phase))
        for index, name in enumerate(names[1:])
    })


def tilted_disc(scene, name, center, direction, thickness, radius, material_name):
    """A flat ellipsoid at center (Blender vector), its thin axis along direction."""
    disc = ellipsoid(scene, name, Vector((0.0, 0.0, 0.0)), (thickness, radius, radius), material_name)
    turn = Vector((1.0, 0.0, 0.0)).rotation_difference(direction.normalized()).to_matrix().to_4x4()
    disc.data.transform(Matrix.Translation(center) @ turn)
    return disc


def build_tubesnout(scene):
    # Muzzle and pipe: wide over the nose cap, thin along the pipe, a little wider at the mouth
    points = [G(x, y, 0.0) for x, y, _ in SNOUT_PATH]
    radii = [radius for _, _, radius in SNOUT_PATH]
    forward = (points[-1] - points[-2]).normalized()
    points.append(points[-1] + forward * 0.1)
    radii.append(SNOUT_END_GIRTH)
    tube(scene, 'Pipe', points, radii, [(i, i + 1) for i in range(len(points) - 1)], 'Skin')

    # Trumpet mouth: a lip ring across the pipe and the dark hole in it
    tilted_disc(scene, 'Lip', points[-2] + forward * 0.08, forward, 0.17, 0.5, 'ShellRim')
    tilted_disc(scene, 'Hole', points[-2] + forward * 0.2, forward, 0.1, 0.29, 'Dark')

    # Two bony bumps on the bridge of the snout, like the nose spine of a seahorse
    for index, share in enumerate((0.3, 0.52)):
        x = SNOUT_PATH[0][0] + (SNOUT_PATH[-1][0] - SNOUT_PATH[0][0]) * share
        low = max(i for i, (px, _, _) in enumerate(SNOUT_PATH) if px <= x)
        (x0, y0, r0), (x1, y1, r1) = SNOUT_PATH[low], SNOUT_PATH[low + 1]
        mix = (x - x0) / (x1 - x0)
        ellipsoid(scene, f'Bump{index}', G(x, y0 + (y1 - y0) * mix + (r0 + (r1 - r0) * mix) * 0.95, 0.0), (0.14, 0.14, 0.12), 'ShellRim', subdivisions=2)


TURRET_PATH = ((-0.5, 1.15), (0.2, 1.0), (0.65, 0.72))   # (z out of the head, radius) of the cone of skin
TURRET_KNOBS = 6                     # bony knobs around the foot of the cone


def build_turreteye(scene):
    # Turret: a cone of skin growing out of the head, the eye sits in its tip
    tube(scene, 'Turret', [G(0.0, 0.0, z) for z, _ in TURRET_PATH], [radius for _, radius in TURRET_PATH], [(0, 1), (1, 2)], 'Skin')
    for index in range(TURRET_KNOBS):
        angle = math.tau * (index + 0.5) / TURRET_KNOBS
        ellipsoid(scene, f'Knob{index}', G(math.cos(angle) * 1.0, math.sin(angle) * 1.0, 0.15), (0.16, 0.16, 0.16), 'ShellRim', subdivisions=2)

    # Eye: a golden iris peeking out of the tip, a big pupil looking out and a little forward
    ellipsoid(scene, 'Iris', G(0.0, 0.0, 1.0), (0.62, 0.62, 0.62), 'Beak')
    ellipsoid(scene, 'Pupil', G(0.1, 0.0, 1.32), (0.4, 0.4, 0.4), 'Pupil')
    ellipsoid(scene, 'Glint', G(0.26, 0.2, 1.63), (0.1, 0.1, 0.1), 'Glint', subdivisions=2)

    # Eye spine: a bony spike above the eye, leaning back
    cone(scene, 'Spine', G(-0.1, 0.75, 0.3), G(-0.4, 1.6, 0.6), 0.26, 'ShellRim')


# ===========================================================================
# ==============================   EEL PARTS   ==============================
# ===========================================================================
#
#   build_gapemaw     Gaping maw of a moray: two short nostril tubes on the nose.
#   build_finribbon   A low fin seam along the back with a wavy edge, one piece covers half the body.
#
#   The opening and the needle teeth of the maw come from the game (data/mouths.json,
#   line GapeMaw), the model only adds what the carve cannot do.
#

RIBBON_BASE = 0.8                    # the seam starts inside the back
RIBBON_HEIGHT = 2.0                  # mean height of its edge, 1 = the back
RIBBON_WAVE = 0.1                    # how far the edge waves up and down
RIBBON_WAVES = 7                     # waves along one piece
RIBBON_COLUMNS = 56
RIBBON_ROWS = 4
RIBBON_THICKNESS = 0.12              # across the body, in flank widths
RIBBON_END = 0.05                    # the edge sinks to the back over this share at both ends


def build_gapemaw(scene):
    # Nostril tubes: two short pipes lying on the tip of the nose, pointing forward
    for side in (-1, 1):
        tube(scene, f'Nostril{side}', [G(0.78, 0.8, side * 0.3), G(0.9, 0.87, side * 0.32), G(1.0, 0.92, side * 0.34)],
             (0.075, 0.065, 0.06), [(0, 1), (1, 2)], 'Skin')


def build_finribbon(scene):
    # Seam: a quad strip standing on the back, its edge waving, sinking to the back at both ends
    bm = bmesh.new()
    grid = []
    for column in range(RIBBON_COLUMNS + 1):
        share = column / RIBBON_COLUMNS
        ends = min(1.0, share / RIBBON_END, (1.0 - share) / RIBBON_END)
        top = RIBBON_HEIGHT + RIBBON_WAVE * math.sin(share * math.tau * RIBBON_WAVES)
        height = RIBBON_BASE + (top - RIBBON_BASE) * math.sin(ends * math.pi / 2)
        grid.append([bm.verts.new(G(share - 0.5, RIBBON_BASE + (height - RIBBON_BASE) * row / RIBBON_ROWS, 0.0)) for row in range(RIBBON_ROWS + 1)])
    for column in range(RIBBON_COLUMNS):
        for row in range(RIBBON_ROWS):
            bm.faces.new([grid[column][row], grid[column + 1][row], grid[column + 1][row + 1], grid[column][row + 1]])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new('Seam')
    bm.to_mesh(mesh)
    bm.free()
    seam = link_mesh(scene, 'Seam', mesh, 'Fin')
    solidify = seam.modifiers.new('Thick', 'SOLIDIFY')
    solidify.thickness = RIBBON_THICKNESS
    solidify.offset = 0.0
    apply_modifiers(seam)
    for polygon in seam.data.polygons:
        polygon.use_smooth = True
