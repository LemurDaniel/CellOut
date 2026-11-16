# NOTE:
#   New parts of the spider family of templates (spider, scorpion): the fangs and the
#   eye cluster of a spider and the tail of a scorpion with its sting.
#   Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module new_spider -Call 'build_fangs(scene)' -Name fangs
#   ./tools/blender/Show-Model.ps1 -Module new_spider -Call 'build_stinger(scene)' -Name stinger

#
#   Chapters:
#     SPIDER PARTS     Fangs and the eye cluster.
#     SCORPION PARTS   The tail with the sting.
#

import math

from mathutils import Matrix, Vector

from models.helpers import AXIS_OUT, FPS, G, animate, bind, ellipsoid, rig, tube, wave

FANG_OUT = 0.34                      # the two chelicerae hang this far out of the middle of the face (snout z)
FANG_FRONT = 0.72                    # ... and this far forward (snout x, 1 = nose tip)
FANG_CURVE = ((0.0, 0.0, 0.0), (0.06, -0.26, -0.04), (0.09, -0.48, -0.16), (0.06, -0.62, -0.31))     # the fang from its root: forward, down, inwards
FANG_RADII = (0.13, 0.11, 0.07, 0.014)

# The eyes of one side: center (forward, up, out of the head) and radius, the main eye first
SPIDER_EYES = (((0.0, 0.0, 0.0), 1.0), ((-1.5, 0.3, -0.3), 0.66), ((-0.75, 1.3, -0.45), 0.4), ((-1.9, 1.2, -0.75), 0.36))
EYE_RING = 0.13                      # thickness of the ring of skin around every eye, in eye radii

# The tail from its root on the body to the bulb of the sting (forward, up): back, up, forward over the back
STING_PATH = ((0.15, 0.0), (-0.25, 0.03), (-0.56, 0.28), (-0.68, 0.66), (-0.52, 1.02), (-0.16, 1.2), (0.26, 1.18))
STING_RADII = (0.28, 0.27, 0.26, 0.25, 0.24, 0.22)                   # half thickness of the segments, root first
STING_BULB = ((0.52, 1.1), 0.25)     # center and radius of the bulb behind the sting
STING_NEEDLE = ((0.7, 1.02), (0.88, 0.9), (0.97, 0.72), (0.94, 0.52))   # the sting out of the bulb, curving down
STING_CURL = (0.0, 2.0, 3.0, 4.0, 5.0, 6.0)                          # degrees per bone of the Loop

# ===========================================================================
# ============================   SPIDER PARTS   =============================
# ===========================================================================
#
#   build_fangs        The two chelicerae under the face of a spider, a curved fang on each.
#   build_spidereyes   The eye cluster of one side: a big shiny black eye and three smaller ones.
#

def build_fangs(scene):
    """
    The fangs of a spider (Snout: x 0 at the mouth corners .. 1 at the nose tip, z the
    width of the head, y the height of the head above and below the mouth line). Two fat
    chelicerae hang side by side under the front of the face, each ends in a curved fang
    that points down and in towards the other. The fangs are part of the model on purpose:
    they are no teeth on the lips of the mouth, the mouth behind them stays a closed line.
    """
    for side in (-1, 1):
        # Chelicera: a fat drop of skin, thicker below
        ellipsoid(scene, f'Root{side}', G(FANG_FRONT - 0.08, -0.1, side * FANG_OUT), (0.4, 0.3, 0.42), 'Skin', subdivisions=3)
        ellipsoid(scene, f'Drop{side}', G(FANG_FRONT, -0.5, side * FANG_OUT), (0.42, 0.3, 0.5), 'Skin', subdivisions=3)

        # Fang: out of the underside of the drop, curving inwards
        root = G(FANG_FRONT + 0.08, -0.9, side * (FANG_OUT - 0.02))
        points = [root + G(x, y, side * z) for x, y, z in FANG_CURVE]
        tube(scene, f'Fang{side}', points, FANG_RADII, [(i, i + 1) for i in range(len(points) - 1)], 'Claw')


def build_spidereyes(scene):
    """
    The eyes of a spider (Eye: an eyeball of radius 1 at the origin, +Z out of the head,
    +X forward): a cluster of four round shiny black eyes without lids. The big main eye
    sits at the origin, a second one behind it, two small ones above. Every eye lies in
    a thin ring of the pattern color and carries a glint.
    """
    for i, ((x, y, z), radius) in enumerate(SPIDER_EYES):
        center = G(x, y, z)
        ellipsoid(scene, f'Ball{i}', center, (radius, radius, radius), 'Pupil', subdivisions=3)
        ellipsoid(scene, f'Glint{i}', center + G(0.32, 0.36, 0.84) * radius, (0.17 * radius,) * 3, 'Glint', subdivisions=2)

        # Ring: a closed circle around the base of the ball
        count = 12
        ring = [center + G(math.cos(math.tau * step / count), math.sin(math.tau * step / count), 0.1) * radius for step in range(count)]
        tube(scene, f'Ring{i}', ring, [EYE_RING * radius] * count, [(step, (step + 1) % count) for step in range(count)], 'BackSkin', subdivisions=1, max_pieces=2)


# ===========================================================================
# ===========================   SCORPION PARTS   ============================
# ===========================================================================
#
#   bead            One segment of the tail: an ellipsoid lying between two points.
#   build_stinger   The segmented tail curling up and forward over the back, the sting on its tip.
#

def bead(scene, name, start, end, radius, material_name):
    """An ellipsoid between two points (Blender vectors), radius thick, a little longer than their distance."""
    half_length = (end - start).length * 0.6
    obj = ellipsoid(scene, name, Vector((0, 0, 0)), (half_length, radius, radius), material_name, subdivisions=3)
    turn = Vector((1, 0, 0)).rotation_difference((end - start).normalized()).to_matrix().to_4x4()
    obj.data.transform(Matrix.Translation((start + end) * 0.5) @ turn)
    return obj


def build_stinger(scene):
    """
    The tail of a scorpion (Tail): root at the origin on the tail tip of the body, the
    body lies towards +X. Fat segments like beads on a string run back, climb and curl
    forward over the back, dark joints between them. The last one carries the round bulb
    with the curved sting, its tip pointing down at whatever stands in front. The tail
    curls and opens slowly ("Loop", 3 s).
    """
    points = [G(x, y, 0) for x, y in STING_PATH]
    meshes = []
    for i, (start, end) in enumerate(zip(points, points[1:])):
        meshes.append(bead(scene, f'Segment{i}', start, end, STING_RADII[i], 'Skin'))
        meshes.append(ellipsoid(scene, f'Joint{i}', end, (STING_RADII[i] * 0.72,) * 3, 'Dark', subdivisions=2))

    # Bulb and sting
    (bulb_x, bulb_y), bulb_radius = STING_BULB
    meshes.append(bead(scene, 'Bulb', points[-1], points[-1].lerp(G(bulb_x, bulb_y, 0), 1.5), bulb_radius, 'Skin'))
    needle = [G(x, y, 0) for x, y in STING_NEEDLE]
    meshes.append(tube(scene, 'Sting', needle, (0.1, 0.07, 0.04, 0.008), [(i, i + 1) for i in range(len(needle) - 1)], 'Chitin'))

    # One bone per segment, bulb and sting ride on the last
    count = len(points) - 1
    armature = rig(scene, 'Rig', [(f'Tail{i}', points[i], points[i + 1], f'Tail{i - 1}' if i > 0 else None) for i in range(count)])
    last = f'Tail{count - 1}'
    bind(armature, meshes, rigid={**{f'Segment{i}': f'Tail{i}' for i in range(count)}, **{f'Joint{i}': f'Tail{i}' for i in range(count)}, 'Bulb': last, 'Sting': last})
    animate(armature, 'Loop', FPS * 3, {
        f'Tail{i}': (AXIS_OUT, lambda share, phase=-i * 0.05, degrees=STING_CURL[i]: degrees * wave(share, phase)) for i in range(count)
    })
