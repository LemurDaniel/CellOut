# NOTE:
#   New parts for the seahorse of the organism stage: the curled grasping tail that
#   replaces a tail fin and the tube snout that replaces a mouth. Not registered in
#   build.models.py yet, previewed with Show-Model.ps1.
#   Model spaces (Visual/CreatureModels.cs):
#   > Curl tail:   Tail, root at the origin at the tail tip, reaching back (-X), about 1 unit.
#   > Tube snout:  Snout, a horseshoe around the snout: ends at x = 0, z = -1 and 1,
#                  the nose tip at x = 1. The tube reaches far past the nose tip.
#
#   ./tools/blender/Show-Model.ps1 -Module new_seahorse -Call 'build_curltail(scene)' -Name curltail
#   ./tools/blender/Show-Model.ps1 -Module new_seahorse -Call 'build_tubesnout(scene)' -Name tubesnout

#
#   Chapters:
#     SEAHORSE PARTS   Curled tail and tube snout.
#

import math

from mathutils import Matrix, Vector

from models.helpers import (
    AXIS_OUT,
    FPS,
    G,
    animate,
    bind,
    ellipsoid,
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
#   build_tubesnout   Tube snout: a muzzle over the nose, a long pipe and a small trumpet mouth.
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
    (0.15, 0.0, 0.84),
    (0.6, -0.02, 0.72),
    (1.0, -0.08, 0.44),
    (1.5, -0.17, 0.3),
    (2.4, -0.37, 0.27),
    (3.2, -0.55, 0.29),
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
    tilted_disc(scene, 'Lip', points[-2] + forward * 0.08, forward, 0.17, 0.45, 'ShellRim')
    tilted_disc(scene, 'Hole', points[-2] + forward * 0.2, forward, 0.1, 0.29, 'Dark')

    # Two bony bumps on the bridge of the snout, like the nose spine of a seahorse
    for index, share in enumerate((0.3, 0.52)):
        x = SNOUT_PATH[0][0] + (SNOUT_PATH[-1][0] - SNOUT_PATH[0][0]) * share
        low = max(i for i, (px, _, _) in enumerate(SNOUT_PATH) if px <= x)
        (x0, y0, r0), (x1, y1, r1) = SNOUT_PATH[low], SNOUT_PATH[low + 1]
        mix = (x - x0) / (x1 - x0)
        ellipsoid(scene, f'Bump{index}', G(x, y0 + (y1 - y0) * mix + (r0 + (r1 - r0) * mix) * 0.95, 0.0), (0.14, 0.14, 0.12), 'ShellRim', subdivisions=2)
