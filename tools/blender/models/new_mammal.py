# NOTE:
#   New parts of the mammal family of templates (elephant, giraffe): tusks, the big ear
#   of an elephant, the horns of a giraffe, a tail with a tuft and a cloven hoof.
#   Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module new_mammal -Call 'build_tusks(scene)' -Name tusks
#   ./tools/blender/Show-Model.ps1 -Module new_mammal -Call 'build_bigears(scene)' -Name bigears

#
#   Chapters:
#     ELEPHANT PARTS   Tusk and ear, one model per flank.
#     GIRAFFE PARTS    Ossicones and the cloven hoof.
#     SHARED PARTS     The tail with a tuft.
#

import math

import bmesh
import bpy

from models.helpers import AXIS_FORWARD, AXIS_UP, FPS, G, animate, bind, ellipsoid, link_mesh, rig, tube, wave

# Tusk: points in flank space (forward, up, out of the flank) from the root in the cheek to the tip
TUSK_PATH = ((-0.12, 0.06, -0.2), (0.1, -0.12, 0.0), (0.36, -0.36, 0.1), (0.68, -0.52, 0.15), (1.0, -0.52, 0.14), (1.26, -0.38, 0.08), (1.42, -0.2, 0.02))
TUSK_RADIUS = 0.135                  # at the root, it runs out into a blunt point
TUSK_SOCKET = (0.2, 0.17, 0.17)      # the lip of skin the tusk grows out of

# Ear: outline around its center in the plane of the ear, (angle in degrees, radius): 0 = back, 90 = up
EAR_CENTER = (0.62, 0.0)            # back from the root, up
EAR_OUTLINE = ((-180, 0.72), (-135, 0.7), (-100, 0.96), (-65, 0.9), (-25, 0.92), (15, 1.0), (50, 1.04), (90, 0.8), (135, 0.76), (180, 0.72))
EAR_SPREAD = math.radians(46.0)      # the ear stands this far out of the flank
EAR_THICKNESS = 0.07                 # at the root, half of it at the rim
EAR_CUP = 0.1                        # the rim bends back to the body by this much
EAR_FLAP = 11.0                      # degrees the ear swings open and closed

OSSICONE_OUT = 0.14                  # each horn stands this far beside the middle
OSSICONE_HEIGHT = 0.4
OSSICONE_LEAN = 0.1                  # the tips lean back by this much
OSSICONE_SPLAY = 0.04                # and apart
OSSICONE_RADII = (0.085, 0.07, 0.062, 0.066)

HOOF_SOLE = -0.105                   # height of the sole below the ankle
HOOF_TOE = (0.1, 0.054, 0.085)     # half sizes of one toe: forward, out, up
HOOF_CLEFT = 0.008                   # half of the gap between both toes

TUFT_PATH = ((0.08, 0.0), (-0.1, -0.02), (-0.22, -0.16), (-0.28, -0.4), (-0.3, -0.66), (-0.3, -0.9))      # back, up: the tail hangs
TUFT_RADII = (0.085, 0.075, 0.058, 0.045, 0.038, 0.034)
TUFT_STRANDS = 9
TUFT_LENGTH = 0.36
TUFT_SWISH = (3.0, 7.0, 11.0, 14.0)  # degrees per tail bone, root to tip


# ===========================================================================
# ===========================   ELEPHANT PARTS   ============================
# ===========================================================================
#
#   build_tusks     One long curved tusk of ivory.
#   build_bigears   One huge thin ear, it flaps slowly.
#

def _smooth(points, radii, pieces=4):
    """A Catmull-Rom curve through the points with eased radii: pieces steps per span, so a tube has no kinks."""
    fine_points, fine_radii = [], []
    last = len(points) - 1
    for i in range(last):
        p0, p1, p2, p3 = points[max(i - 1, 0)], points[i], points[i + 1], points[min(i + 2, last)]
        for step in range(pieces):
            t = step / pieces
            fine_points.append(0.5 * ((2 * p1) + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t ** 3))
            fine_radii.append(radii[i] + (radii[i + 1] - radii[i]) * t)
    fine_points.append(points[last])
    fine_radii.append(radii[last])
    return fine_points, fine_radii, [(i, i + 1) for i in range(len(fine_points) - 1)]


def build_tusks(scene):
    """
    The tusk of an elephant (FlankPair: origin on the skin of the right flank, +X
    forward, +Y up, +Z out of the flank; the game mirrors it for the left flank). It
    grows out of a lip of skin, runs down and forward and curves up again to a blunt
    point, about 1.6 body heights long. Ivory (Bone), no rig.
    """
    count = len(TUSK_PATH)
    radii = [TUSK_RADIUS * (1.0 - 0.8 * (i / (count - 1)) ** 1.6) for i in range(count)]
    tube(scene, 'Tusk', *_smooth([G(*point) for point in TUSK_PATH], radii), 'Bone')
    ellipsoid(scene, 'Socket', G(0.0, -0.03, -0.06), TUSK_SOCKET, 'Skin', subdivisions=3)


def _ear_radius(angle):
    """Radius of the ear outline at an angle in degrees, eased between the keys of EAR_OUTLINE."""
    for (a0, r0), (a1, r1) in zip(EAR_OUTLINE, EAR_OUTLINE[1:]):
        if a0 <= angle <= a1:
            share = (angle - a0) / (a1 - a0)
            return r0 + (r1 - r0) * (0.5 - 0.5 * math.cos(share * math.pi))
    return EAR_OUTLINE[0][1]


def build_bigears(scene):
    """
    The ear of an African elephant (FlankPair: origin on the skin of the right flank).
    A thin sheet of skin as tall as two body heights: its front edge sits on the head,
    it lies back along the shoulder and stands out of the flank by EAR_SPREAD. Wide
    top edge, a pointed lobe below, the rim cupped back to the body.
    It swings open and closed ("Flap", 3 s).
    """
    back = G(-math.cos(EAR_SPREAD), 0, math.sin(EAR_SPREAD))          # along the ear, from the head to its rim
    up = G(0, 1, 0)
    face = G(math.sin(EAR_SPREAD), 0, math.cos(EAR_SPREAD))           # out of its outer side

    def at(reach, height, lift=0.0):
        return back * reach + up * height + face * lift

    # A sphere pressed flat and pulled to the outline: a closed sheet with a round rim
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1.0)
    for vertex in bm.verts:
        u, v, w = vertex.co.x, vertex.co.y, vertex.co.z
        spread = math.hypot(u, v)
        radius = _ear_radius(math.degrees(math.atan2(v, u)))
        reach = EAR_CENTER[0] + u * radius
        height = EAR_CENTER[1] + v * radius
        thickness = EAR_THICKNESS * (1.0 - 0.5 * min(1.0, max(0.0, reach) / 1.3))
        cup = -EAR_CUP * (spread * radius) ** 2
        vertex.co = at(reach, height, w * thickness + cup)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new('Ear')
    bm.to_mesh(mesh)
    bm.free()
    ear = link_mesh(scene, 'Ear', mesh, 'Skin')
    for polygon in ear.data.polygons:
        polygon.use_smooth = True

    armature = rig(scene, 'Rig', [
        ('Base', at(-0.1, 0.0), at(0.55, 0.0), None),
        ('Tip', at(0.55, 0.0), at(1.4, 0.0), 'Base'),
    ])
    bind(armature, [ear])
    animate(armature, 'Flap', FPS * 3, {
        'Base': (AXIS_UP, lambda share: EAR_FLAP * wave(share)),
        'Tip': (AXIS_UP, lambda share: EAR_FLAP * 0.7 * wave(share, -0.18)),
    })


# ===========================================================================
# ============================   GIRAFFE PARTS   ============================
# ===========================================================================
#
#   build_ossicones   The two short knobbed horns.
#   build_hoof        A cloven hoof on a limb tip.
#

def build_ossicones(scene):
    """
    The horns of a giraffe (Top: origin on the back line, +X forward, +Y up): two short
    stalks covered in skin, leaning back a little and apart, each with a dark knob on
    top. The roots are sunk, so they grow out of heads of any shape.
    """
    count = len(OSSICONE_RADII)
    for side in (-1, 1):
        points = []
        for i in range(count):
            share = i / (count - 1)
            points.append(G(-OSSICONE_LEAN * share, -0.12 + (OSSICONE_HEIGHT + 0.12) * share, side * (OSSICONE_OUT + OSSICONE_SPLAY * share)))
        tube(scene, f'Stalk{side}', points, OSSICONE_RADII, [(i, i + 1) for i in range(count - 1)], 'Skin')
        ellipsoid(scene, f'Knob{side}', points[-1] + G(-0.005, 0.035, 0), (0.082, 0.082, 0.07), 'Dark', subdivisions=3)


def build_hoof(scene):
    """
    A cloven hoof, the end of a sculpted limb: the ankle at the origin, the sole below
    it, the toes forward (+X). A short pastern of skin widens to the coronet, below it
    two hard dark toes side by side: flat soles, steep fronts, a cleft between them.
    No rig: the foot stands flat on its limb.
    """
    tube(scene, 'Pastern', [G(0, 0.03, 0), G(0.008, 0.0, 0), G(0.016, -0.025, 0)], (0.046, 0.05, 0.058), [(0, 1), (1, 2)], 'Skin')
    forward, out, tall = HOOF_TOE
    for side in (-1, 1):
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1.0)
        for vertex in bm.verts:
            x, y, z = vertex.co.x, vertex.co.y, vertex.co.z          # unit sphere: forward, out, up
            # Flat sole, flat inner wall at the cleft, the top narrows to the coronet and the toe runs to a blunt point
            z = max(z, -0.55)
            narrow = 1.0 - 0.3 * max(0.0, z)
            point = 1.0 - 0.45 * max(0.0, x) ** 2
            inner = max(y * side, -0.35) * side
            vertex.co = G(0.035 + x * forward * narrow - 0.03 * max(0.0, z),
                          HOOF_SOLE + (z + 0.55) * tall,
                          side * (HOOF_CLEFT + 0.35 * out) + inner * out * narrow * point)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        mesh = bpy.data.meshes.new(f'Toe{side}')
        bm.to_mesh(mesh)
        bm.free()
        toe = link_mesh(scene, f'Toe{side}', mesh, 'Dark')
        for polygon in toe.data.polygons:
            polygon.use_smooth = True


# ===========================================================================
# ============================   SHARED PARTS   =============================
# ===========================================================================
#
#   build_tufttail   A thin hanging tail with a dark tuft.
#

def build_tufttail(scene):
    """
    The tail of an elephant or a giraffe (Tail: root at the origin on the tail tip of
    the body, reaching back along -X): a thin rope of skin that leaves the body
    backwards and hangs down, about one unit long, with a brush of dark hair at its
    end. It swishes from side to side ("Swish", 3 s).
    """
    points = [G(x, y, 0) for x, y in TUFT_PATH]
    meshes = [tube(scene, 'Rope', *_smooth(points, TUFT_RADII), 'Skin')]

    # Tuft: strands in a ring around the end of the rope, one in the middle, all running out into a point
    end = points[-1]
    for i in range(TUFT_STRANDS):
        is_middle = TUFT_STRANDS - 1 == i
        angle = math.tau * i / (TUFT_STRANDS - 1)
        ring = 0.0 if is_middle else 1.0
        out = G(math.cos(angle) * ring, 0, math.sin(angle) * ring)
        length = TUFT_LENGTH * (1.15 if is_middle else 0.86 + 0.14 * math.sin(i * 2.4))
        strand = [
            end + G(0, 0.09, 0) + out * 0.012,
            end + G(0, -length * 0.3, 0) + out * 0.06,
            end + G(-0.01, -length * 0.7, 0) + out * 0.055,
            end + G(-0.02, -length, 0) + out * 0.02,
        ]
        meshes.append(tube(scene, f'Strand{i}', strand, (0.03, 0.05, 0.04, 0.012), [(0, 1), (1, 2), (2, 3)], 'Dark', subdivisions=1))

    bones = len(TUFT_SWISH)
    cuts = [points[1].lerp(end + G(-0.02, -TUFT_LENGTH, 0), i / bones) for i in range(bones + 1)]
    armature = rig(scene, 'Rig', [(f'Tail{i}', cuts[i], cuts[i + 1], f'Tail{i - 1}' if i > 0 else None) for i in range(bones)])
    bind(armature, meshes)
    animate(armature, 'Swish', FPS * 3, {
        f'Tail{i}': (AXIS_FORWARD, lambda share, phase=-i * 0.12, degrees=TUFT_SWISH[i]: degrees * wave(share, phase)) for i in range(bones)
    })


def build_elephantfoot(scene):
    """Placeholder for the kind ElephantFeet (model space: a foot on a limb tip, like build_hoof): replace this body with the real model."""
    from models.helpers import G as _g, ellipsoid as _ellipsoid
    _ellipsoid(scene, 'Placeholder', _g(0, 0, 0), (0.5, 0.5, 0.5), 'Skin', subdivisions=2)


def build_mane(scene):
    """Placeholder for the kind Mane (model space: Row: ONE tuft, foot at the origin, standing up along +Y, +X forward; the game repeats it along the back): replace this body with the real model."""
    from models.helpers import G as _g, ellipsoid as _ellipsoid
    _ellipsoid(scene, 'Placeholder', _g(0, 0, 0), (0.5, 0.5, 0.5), 'Skin', subdivisions=2)
