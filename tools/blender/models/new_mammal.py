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
#     ROUND 2 PARTS    The round foot of an elephant and the mane of a giraffe.
#     ELEPHANT HEAD    Skull and trunk in one rigged model with slots for eyes, ears and tusks.
#

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

from models.helpers import AXIS_FORWARD, AXIS_UP, FPS, G, animate, bind, ellipsoid, link_mesh, rig, slot, tube, wave

# Tusk: points in flank space (forward, up, out of the flank) from the root in the cheek to the tip
TUSK_PATH = ((-0.12, 0.06, -0.2), (0.1, -0.12, 0.0), (0.36, -0.36, 0.1), (0.68, -0.52, 0.15), (1.0, -0.52, 0.14), (1.26, -0.38, 0.08), (1.42, -0.2, 0.02))
TUSK_RADIUS = 0.135                  # at the root, it runs out into a blunt point
TUSK_SOCKET = (0.2, 0.17, 0.17)      # the lip of skin the tusk grows out of

# Ear: outline around its center in the plane of the ear, (angle in degrees, radius): 0 = back, 90 = up
EAR_CENTER = (0.62, 0.0)            # back from the root, up
EAR_OUTLINE = ((-180, 0.72), (-135, 0.7), (-100, 0.96), (-65, 0.9), (-25, 0.92), (15, 1.0), (50, 1.04), (90, 0.8), (135, 0.76), (180, 0.72))
EAR_SPREAD = math.radians(46.0)      # the ear stands this far out of the flank
EAR_THICKNESS = 0.07                 # at the root, half of it at the rim
EAR_CUP = 0.17                       # the rim bends back to the body by this much
EAR_FLAP = 11.0                      # degrees the ear swings open and closed

OSSICONE_OUT = 0.14                  # each horn stands this far beside the middle
OSSICONE_HEIGHT = 0.4
OSSICONE_LEAN = 0.1                  # the tips lean back by this much
OSSICONE_SPLAY = 0.04                # and apart
OSSICONE_RADII = (0.085, 0.07, 0.062, 0.066)

HOOF_SOLE = -0.105                   # height of the sole below the ankle
HOOF_TOE = (0.1, 0.054, 0.085)     # half sizes of one toe: forward, out, up
HOOF_CLEFT = 0.008                   # half of the gap between both toes

FOOT_RADIUS = 0.175                  # of the pad at the ankle, a column leg is about as wide
FOOT_FLARE = 0.1                     # the sole is this much wider
FOOT_SOLE = -0.115                   # height of the sole below the ankle
FOOT_HEIGHT = 0.1                    # of the unit sphere the pad is made of
FOOT_CUT = 0.55                      # the sphere is cut flat this far below its middle
FOOT_NAILS = (-48, -16, 16, 48)      # degrees around the front

MANE_STRANDS = 8
MANE_LENGTH = 0.95                   # along the spine
MANE_HEIGHT = 0.38
MANE_LEAN = 0.1                      # the tips lean back by this much
MANE_THIN = 0.6                      # strands are this much thinner across the spine

# Elephant head: skull and trunk are drawn in skull radii around a point inside the neck (forward, up, out of
# the right flank) and then grown and shifted into the model space Head (_to_head)
HEAD_SCALE = 1.35                    # the skull is this much bigger than the neck stump (radius 1)
HEAD_SHIFT = 0.26                    # and sits this far in front of the neck joint: its back stands on the origin
SKULL_CENTER = (0.55, 0.05)          # forward, up
SKULL_SIZE = (0.95, 0.85, 0.9)
# Where the skull swells: direction from its center (z is mirrored when not 0), amount, sharpness
SKULL_BULGES = (
    ((0.05, 0.85, 0.5), 0.42, 9.0),   # the two domes, the dip between them stays
    ((0.85, -0.35, 0.0), 0.3, 3.5),  # the face runs out into the trunk root
    ((0.45, -0.6, 0.6), 0.2, 5.0),   # cheeks
    ((0.62, -0.62, 0.48), 0.1, 16.0), # the socket a tusk grows out of
    ((0.12, 0.3, 0.9), -0.07, 12.0), # the hollow of the temple between eye and ear
    ((0.75, 0.45, 0.45), 0.08, 9.0), # brow over each eye
)
LIP_CENTER = (1.02, -0.93, 0.0)        # the short lower lip under the trunk root
LIP_SIZE = (0.42, 0.3, 0.19)          # in Blender axes of the helper: forward, out, up
TRUNK_PATH = ((0.75, 0.25), (1.45, -0.3), (1.78, -0.85), (1.86, -1.35), (1.82, -1.78), (1.76, -2.08), (1.86, -2.3), (2.06, -2.34), (2.2, -2.15))      # forward, up
TRUNK_RADII = (0.4, 0.55, 0.48, 0.4, 0.33, 0.28, 0.24, 0.2, 0.17)
TRUNK_RINGS = 8                      # loft rings per span of the path
TRUNK_RIPPLE = 0.1                   # cross rings: the trunk swells by this share ...
TRUNK_RIPPLES = 24                   # ... this many times along its length
TRUNK_SWAY = (4.0, 6.0, 9.0)         # degrees: root, middle, tip
# In the model space Head
STUMP_CENTER = (0.15, 0.0, 0.0)      # the neck plug behind the skull, it sinks into the body
STUMP_SIZE = (0.6, 1.0, 1.0)         # in Blender axes of the helper: forward, out, up
# Slots, right half: direction from the skull center to the spot on its skin, size of the part there
EYE_TOWARDS = (0.5, 0.12, 0.82)
EYE_SLOT_SIZE = 0.13                 # radius of the eyeball
EAR_TOWARDS = (-0.3, 0.12, 0.95)     # on the side of the skull, behind the eye
EAR_SLOT_SIZE = 1.15                 # the ear part is about two of these tall: nearly as tall as the skull
HORN_TOWARDS = (0.62, -0.62, 0.48)   # the tusk socket beside the trunk root
HORN_SLOT_SIZE = 1.3                 # the tusk part is about one and a half of these long
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


def _ear_sheet(scene, name, root, side=1, size=1.0, spread=EAR_SPREAD, root_thickness=EAR_THICKNESS):
    """
    One ear as a closed thin sheet: a sphere pressed flat and pulled to EAR_OUTLINE, the
    rim cupped back to the body. Its front edge sits on root, it runs back and stands out
    to the given side by spread. Returns the mesh and at(reach, height, lift): a point of
    the ear plane (back from the root, up, out of its outer face) in Blender axes.
    """
    back = G(-math.cos(spread), 0, side * math.sin(spread))          # along the ear, from the head to its rim
    up = G(0, 1, 0)
    face = G(math.sin(spread), 0, side * math.cos(spread))           # out of its outer side

    def at(reach, height, lift=0.0):
        return root + (back * reach + up * height + face * lift) * size

    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=1.0)
    for vertex in bm.verts:
        u, v, w = vertex.co.x, vertex.co.y, vertex.co.z
        spread_share = math.hypot(u, v)
        radius = _ear_radius(math.degrees(math.atan2(v, u)))
        reach = EAR_CENTER[0] + u * radius
        height = EAR_CENTER[1] + v * radius
        thickness = root_thickness * (1.0 - 0.5 * min(1.0, max(0.0, reach) / 1.3)) + (root_thickness - EAR_THICKNESS) * max(0.0, 1.0 - max(0.0, reach) / 0.4)
        cup = -EAR_CUP * (spread_share * radius) ** 2
        vertex.co = at(reach, height, w * thickness + cup)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    ear = link_mesh(scene, name, mesh, 'Skin')
    for polygon in ear.data.polygons:
        polygon.use_smooth = True
    return ear, at


def build_bigears(scene):
    """
    The ear of an African elephant (FlankPair: origin on the skin of the right flank).
    A thin sheet of skin as tall as two body heights: its front edge sits on the head,
    it lies back along the shoulder and stands out of the flank by EAR_SPREAD. Wide
    top edge, a pointed lobe below, the rim cupped back to the body.
    It swings open and closed ("Flap", 3 s).
    """
    ear, at = _ear_sheet(scene, 'Ear', G(0, 0, 0))
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


# ===========================================================================
# ============================   ROUND 2 PARTS   ============================
# ===========================================================================
#
#   build_elephantfoot   A round soft pad with four small toenails.
#   build_mane           One tuft of a short stiff mane, repeated along the back.
#

def build_elephantfoot(scene):
    """
    The foot of an elephant, the end of a sculpted limb: the ankle at the origin, the
    sole below it, the toes forward (+X). One round soft pad (Skin: the clay of the leg) as wide as a column leg,
    a little wider at the flat sole, with four small pale toenails low on its front.
    No rig: the foot stands flat on its limb.
    """
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1.0)
    for vertex in bm.verts:
        x, y, z = vertex.co.x, vertex.co.y, vertex.co.z              # unit sphere: forward, out, up
        z = max(z, -FOOT_CUT)
        flare = FOOT_RADIUS * (1.0 + FOOT_FLARE * max(0.0, -z) / FOOT_CUT)
        vertex.co = G(0.012 + x * flare, FOOT_SOLE + (z + FOOT_CUT) * FOOT_HEIGHT, y * flare)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new('Pad')
    bm.to_mesh(mesh)
    bm.free()
    pad = link_mesh(scene, 'Pad', mesh, 'Skin')
    for polygon in pad.data.polygons:
        polygon.use_smooth = True

    # Toenails: small flat ovals lying on the front of the pad, just above the sole
    rim = FOOT_RADIUS * (1.0 + FOOT_FLARE * 0.7)
    for i, angle in enumerate(FOOT_NAILS):
        turn = math.radians(angle)
        nail = ellipsoid(scene, f'Nail{i}', G(0, 0, 0), (0.014, 0.034, 0.03), 'Bone', subdivisions=2)
        nail.data.transform(Matrix.Translation(G(0.012 + math.cos(turn) * rim, FOOT_SOLE + 0.036, math.sin(turn) * rim)) @ Matrix.Rotation(-turn, 4, 'Z'))


def build_mane(scene):
    """
    One tuft of a short stiff mane (model space Row: foot at the origin, standing up
    along +Y, +X forward). The game repeats it along the back and neck, so a tuft is
    longer than the step between two copies and they close to one brush. A few thick
    strands side by side along the spine, leaning back, in the pattern color (BackSkin).
    """
    for i in range(MANE_STRANDS):
        share = i / (MANE_STRANDS - 1)
        x = MANE_LENGTH * (0.5 - share)
        height = MANE_HEIGHT * (0.86 + 0.14 * math.sin(i * 2.1 + 0.6))
        points = [G(x + 0.02, -0.06, 0), G(x - MANE_LEAN * 0.4, height * 0.55, 0), G(x - MANE_LEAN, height, 0)]
        strand = tube(scene, f'Strand{i}', points, (0.1, 0.085, 0.04), [(0, 1), (1, 2)], 'BackSkin', subdivisions=1)
        for vertex in strand.data.vertices:
            vertex.co.y *= MANE_THIN

# ===========================================================================
# ============================   ELEPHANT HEAD   ============================
# ===========================================================================
#
#   _loft                A tube of exact rings along a path, with cross rings.
#   _to_head             A point in skull radii, in the model space Head.
#   _skull_point         The point on the skin of the skull in a direction.
#   build_elephanthead   The skull with its trunk, slots for eye, ear and tusk, rigged.
#

def _loft(scene, name, points, radii, material_name, around=16, ripple=0.0, ripples=0, flat=1.0):
    """
    A tube of rings around a path lying in the plane of the body's middle (no skin
    modifier: the radius is exact, so it can carry cross rings). ripple swells every
    ring ripples times along the path, flat squeezes the tube across the body.
    Both ends are closed with a round cap.
    """
    out = G(0, 0, 1)
    bm = bmesh.new()
    rings = []
    last = len(points) - 1
    for i, (point, radius) in enumerate(zip(points, radii)):
        tangent = (points[min(i + 1, last)] - points[max(i - 1, 0)]).normalized()
        normal = tangent.cross(out).normalized()
        swell = 1.0 + ripple * (0.5 + 0.5 * math.cos(math.tau * ripples * i / last)) if ripples else 1.0
        rings.append([bm.verts.new(point + (normal * math.cos(math.tau * k / around) + out * (flat * math.sin(math.tau * k / around))) * (radius * swell)) for k in range(around)])
    for ring, following in zip(rings, rings[1:]):
        for k in range(around):
            bm.faces.new((ring[k], ring[(k + 1) % around], following[(k + 1) % around], following[k]))
    for ring, point, direction in ((rings[0], points[0], points[0] - points[1]), (rings[-1], points[-1], points[-1] - points[-2])):
        center = bm.verts.new(point + direction.normalized() * (ring[0].co - point).length * 0.6)
        for k in range(around):
            bm.faces.new((ring[k], ring[(k + 1) % around], center))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, name, mesh, material_name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def _to_head(point):
    """A Blender point given in skull radii around the old spine end, in the model space Head (neck stump radius 1, origin at the neck joint)."""
    return point * HEAD_SCALE + G(HEAD_SHIFT, 0, 0)


def _skull_point(direction):
    """The point on the skin of the skull in a direction from its center (Godot axes, any length), in skull radii, as Godot coordinates."""
    direction = Vector(direction).normalized()
    swell = 1.0
    for (bx, by, bz), amount, sharpness in SKULL_BULGES:
        for side in ((-1, 1) if bz else (1,)):
            swell += amount * math.exp(-sharpness * (direction - Vector((bx, by, side * bz)).normalized()).length_squared)
    return Vector((SKULL_CENTER[0] + direction.x * SKULL_SIZE[0] * swell, SKULL_CENTER[1] + direction.y * SKULL_SIZE[1] * swell, direction.z * SKULL_SIZE[2] * swell))


def build_elephanthead(scene):
    """
    The skull of an African elephant with its trunk (model space Head: origin at the
    neck joint, +X forward, +Y up, +Z the right flank, the neck stump has radius 1):

               _.-._  _.-._            two domes, a dip between them
             /   skull      \\         broad forehead falling to the trunk root
        ===(  stump   (eye)  )         cheeks; eye, ear and tusk are slots
             \\_______  _.--' \\
                    lip |    |         a short lower lip under the trunk root
                        | trunk        thick root, cross rings, a slight S,
                         \\   \\       the tip curled forward
                          '-._)

    Behind the skull a short round neck stump sinks into the body. Eyes, ears and
    tusks are not part of the model: the game plugs the parts of the player onto the
    slots Eye, Ear and Horn (right half, mirrored by the game).
    Rig: Skull holds the head still; TrunkRoot, TrunkMid and TrunkTip sway the trunk
    ("Sway", 3 s).
    """
    # Skull: one sphere pushed out where the head bulges, so domes, brow and cheeks have no seams
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=5, radius=1.0)
    for vertex in bm.verts:
        # Blender unit sphere (x forward, -y the right flank, z up) read as a direction in Godot axes
        vertex.co = G(*_skull_point((vertex.co.x, vertex.co.z, -vertex.co.y)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new('Skull')
    bm.to_mesh(mesh)
    bm.free()
    skull = link_mesh(scene, 'Skull', mesh, 'Skin')
    for polygon in skull.data.polygons:
        polygon.use_smooth = True
    lip = ellipsoid(scene, 'Lip', G(*LIP_CENTER), LIP_SIZE, 'Skin', subdivisions=3)

    # Trunk: a loft with cross rings, thick out of the face, hanging in a slight S, the tip curled forward
    path, radii, _ = _smooth([G(x, y, 0) for x, y in TRUNK_PATH], TRUNK_RADII, pieces=TRUNK_RINGS)
    trunk = _loft(scene, 'Trunk', path, radii, 'Skin', ripple=TRUNK_RIPPLE, ripples=TRUNK_RIPPLES)

    # Into the model space Head; the stump is given there directly: a plug of radius 1 behind the skull
    for obj in (skull, lip, trunk):
        for vertex in obj.data.vertices:
            vertex.co = _to_head(vertex.co)
    stump = ellipsoid(scene, 'Stump', G(*STUMP_CENTER), STUMP_SIZE, 'Skin', subdivisions=3)

    def on_skin(direction):
        point = _to_head(G(*_skull_point(direction)))
        return (point.x, point.z, -point.y)                           # back to Godot axes for slot()

    slot(scene, 'Eye', on_skin(EYE_TOWARDS), EYE_SLOT_SIZE, out=EYE_TOWARDS, forward=(1.0, 0.0, 0.0))
    slot(scene, 'Ear', on_skin(EAR_TOWARDS), EAR_SLOT_SIZE, out=(0.12, 0.0, 1.0), forward=(1.0, 0.1, 0.0))
    slot(scene, 'Horn', on_skin(HORN_TOWARDS), HORN_SLOT_SIZE, out=(0.0, -0.25, 1.0), forward=(1.0, -0.08, 0.0))

    def cut(share):
        return _to_head(path[round(share * (len(path) - 1))])

    armature = rig(scene, 'Rig', [
        ('Skull', G(-0.3, 0, 0), cut(0.0), None),
        ('TrunkRoot', cut(0.0), cut(0.34), 'Skull'),
        ('TrunkMid', cut(0.34), cut(0.68), 'TrunkRoot'),
        ('TrunkTip', cut(0.68), cut(1.0), 'TrunkMid'),
    ])
    bind(armature, [skull, stump, lip, trunk], rigid={'Skull': 'Skull', 'Stump': 'Skull', 'Lip': 'Skull'})
    animate(armature, 'Sway', FPS * 3, {
        'TrunkRoot': (AXIS_FORWARD, lambda share: TRUNK_SWAY[0] * wave(share)),
        'TrunkMid': (AXIS_FORWARD, lambda share: TRUNK_SWAY[1] * wave(share, -0.12)),
        'TrunkTip': (AXIS_FORWARD, lambda share: TRUNK_SWAY[2] * wave(share, -0.26)),
    })