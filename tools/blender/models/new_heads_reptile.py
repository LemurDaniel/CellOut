# NOTE:
#   Head models of the creature editor (tab Heads, model space Head): the skulls of a
#   crocodile, a tyrannosaur and a horned dinosaur, with slots for the parts of the
#   player. Model space Head: origin at the neck joint, +X forward, +Y up, +Z the right
#   flank, the neck stump has radius 1. Jaws, teeth and beaks are geometry of the skull.
#   Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module new_heads_reptile -Call 'build_crochead(scene)' -Name crochead
#   ./tools/blender/Show-Model.ps1 -Module new_heads_reptile -Call 'build_rexhead(scene)' -Name rexhead
#   ./tools/blender/Show-Model.ps1 -Module new_heads_reptile -Call 'build_ceratopshead(scene)' -Name ceratopshead

#
#   Chapters:
#     SKULL HELPERS     Hulls lofted through rings, spikes, teeth along a jaw, the jaw rig.
#     CROCODILE HEAD    Long flat skull, teeth along the closed mouth.
#     TYRANT HEAD       Deep skull, brow bosses, banana teeth.
#     HORNED HEAD       Beak, cheeks, nose horn and the frill.
#

import math

import bmesh
import bpy
from mathutils import Vector

from models.helpers import AXIS_OUT, FPS, G, animate, bind, ellipsoid, link_mesh, rig, slot

HULL_AROUND = 28                     # points of a ring of a hull
HULL_PIECES = 5                      # rings between two keys of a hull
STUMP_CENTER = (0.05, 0.0, 0.0)      # the round plug that sinks into the body
STUMP_SIZE = (0.52, 1.0, 1.0)        # Blender axes: along the body, across, up
GAPE_SECONDS = 4

# Keys of a hull: x, y of the ring middle, half width, reach up, reach down, roundness of the upper and
# of the lower half (1 = an ellipse, lower = boxy with a flat side)
CROC_UPPER = (
    (-0.25, -0.10, 0.95, 0.80, 0.75, 0.80, 0.90),
    (0.35, -0.10, 1.22, 0.92, 0.70, 0.55, 0.80),
    (0.90, -0.12, 1.24, 0.90, 0.25, 0.55, 0.60),
    (1.50, -0.14, 1.12, 0.72, 0.10, 0.60, 0.50),
    (2.10, -0.14, 0.94, 0.44, 0.08, 0.65, 0.50),
    (3.20, -0.10, 0.74, 0.34, 0.08, 0.65, 0.50),
    (4.20, -0.12, 0.62, 0.30, 0.08, 0.65, 0.50),
    (4.85, -0.10, 0.74, 0.34, 0.08, 0.70, 0.50),
    (5.30, -0.10, 0.56, 0.28, 0.08, 0.90, 0.60),
    (5.46, -0.10, 0.2, 0.10, 0.05, 1.00, 1.00),
)
CROC_LOWER = (
    (0.30, -0.20, 1.00, 0.10, 0.62, 0.60, 0.80),
    (0.90, -0.20, 1.16, 0.08, 0.60, 0.50, 0.80),
    (1.50, -0.20, 1.05, 0.06, 0.48, 0.50, 0.80),
    (2.10, -0.20, 0.87, 0.06, 0.36, 0.50, 0.80),
    (3.20, -0.16, 0.68, 0.06, 0.30, 0.50, 0.80),
    (4.20, -0.18, 0.56, 0.06, 0.27, 0.50, 0.80),
    (4.85, -0.16, 0.66, 0.06, 0.27, 0.50, 0.80),
    (5.22, -0.16, 0.5, 0.06, 0.22, 0.50, 0.90),
    (5.36, -0.16, 0.16, 0.04, 0.08, 1.00, 1.00),
)
CROC_JAW_HINGE = (0.45, -0.25)
CROC_GAPE = 16.0                     # degrees the jaw drops in its loop
CROC_UPPER_TEETH = ((2.0, 0.22), (2.3, 0.28), (2.6, 0.24), (2.95, 0.3), (3.3, 0.42), (3.65, 0.28), (3.95, 0.22),
                    (4.5, 0.28), (4.75, 0.4), (4.98, 0.44), (5.2, 0.3))         # along the jaw: x, length
CROC_LOWER_TEETH = ((2.15, 0.2), (2.45, 0.22), (2.78, 0.2), (3.12, 0.25), (3.48, 0.22), (3.8, 0.25), (4.24, 0.56),
                    (4.62, 0.22), (4.88, 0.28), (5.1, 0.26))                    # the fourth from the tip is the big one
CROC_EYE = (1.32, 0.86, 0.6)         # middle of the raised socket
CROC_NOSTRIL = (5.02, 0.28, 0.0)     # middle of the nostril mound
CROC_SCUTES = ((0.0, 0.3), (0.0, 0.72), (0.42, 0.34), (0.42, 0.8), (0.8, 0.3))  # bony pads on the skull roof: x, z

REX_UPPER = (
    (-0.25, -0.10, 0.95, 0.95, 0.90, 0.80, 0.90),
    (0.40, -0.20, 1.42, 1.22, 0.95, 0.60, 0.80),
    (1.10, -0.28, 1.36, 1.34, 0.30, 0.55, 0.60),
    (1.55, -0.30, 0.86, 1.22, 0.10, 0.60, 0.50),
    (1.90, -0.30, 0.66, 1.06, 0.08, 0.55, 0.50),
    (2.70, -0.30, 0.64, 0.96, 0.08, 0.55, 0.50),
    (3.50, -0.30, 0.60, 0.86, 0.08, 0.50, 0.50),
    (3.95, -0.30, 0.54, 0.74, 0.08, 0.50, 0.60),
    (4.06, -0.30, 0.3, 0.42, 0.05, 0.80, 1.00),
)
REX_LOWER = (
    (0.50, -0.40, 1.2, 0.12, 0.85, 0.60, 0.80),
    (1.10, -0.40, 1.2, 0.10, 0.92, 0.50, 0.80),
    (1.70, -0.40, 0.74, 0.08, 0.9, 0.50, 0.65),
    (2.60, -0.40, 0.63, 0.08, 0.8, 0.50, 0.6),
    (3.40, -0.40, 0.58, 0.08, 0.72, 0.50, 0.6),
    (3.82, -0.40, 0.5, 0.08, 0.62, 0.60, 0.7),
    (3.94, -0.40, 0.26, 0.05, 0.34, 1.00, 1.00),
)
REX_JAW_HINGE = (0.65, -0.45)
REX_GAPE = 20.0
REX_UPPER_TEETH = ((1.8, 0.34), (2.1, 0.46), (2.4, 0.58), (2.72, 0.66), (3.04, 0.54), (3.34, 0.62), (3.62, 0.46), (3.86, 0.38))
REX_LOWER_TEETH = ((1.9, 0.2), (2.2, 0.26), (2.52, 0.3), (2.85, 0.28), (3.15, 0.3), (3.42, 0.24), (3.62, 0.2))
REX_BROW = (1.22, 1.16, 0.92)        # the bony boss over the eye
REX_EYE = (1.28, 0.7, 1.18) 
REX_KNOBS = ((1.95, 0.16), (2.3, 0.15), (2.65, 0.14), (3.0, 0.12))             # rough knobs on the ridge of the nose: x, size

CERA_UPPER = (
    (-0.25, -0.10, 0.95, 0.95, 0.90, 0.80, 0.90),
    (0.40, -0.25, 1.35, 1.20, 0.90, 0.70, 0.70),
    (1.10, -0.35, 1.25, 1.15, 0.35, 0.70, 0.60),
    (1.70, -0.40, 0.85, 0.95, 0.10, 0.75, 0.50),
    (2.30, -0.42, 0.58, 0.72, 0.08, 0.80, 0.50),
    (2.75, -0.42, 0.40, 0.50, 0.08, 0.90, 0.60),
    (2.90, -0.42, 0.15, 0.20, 0.05, 1.00, 1.00),
)
CERA_LOWER = (
    (0.50, -0.45, 1.10, 0.10, 0.75, 0.60, 0.80),
    (1.10, -0.45, 1.05, 0.10, 0.72, 0.50, 0.80),
    (1.70, -0.50, 0.72, 0.08, 0.58, 0.50, 0.80),
    (2.30, -0.50, 0.48, 0.08, 0.42, 0.50, 0.80),
    (2.60, -0.50, 0.30, 0.06, 0.30, 0.60, 0.90),
    (2.70, -0.50, 0.10, 0.04, 0.10, 1.00, 1.00),
)
CERA_EYE = (1.3, 0.42, 1.16)
CERA_BROW = (1.15, 1.02, 0.52)       # the boss the brow horn stands on
CERA_HORN_SIZE = 1.6                 # the Horns part is built this tall
FRILL_COLUMNS = 40                   # columns of the fan from one flank over the top to the other
FRILL_ROWS = 8                       # rows from the skull roof out to the rim
FRILL_ARC = 108.0                    # the fan reaches this many degrees down each side
FRILL_FADE = 34.0                    # over the last degrees it shrinks back to the skull
FRILL_ROOT = (0.55, 0.85)            # it leaves the skull at this x with this radius
FRILL_REACH = (3.0, 2.55)            # radius of the rim over the neck and at the sides
FRILL_SWEEP = 2.0                    # the rim over the neck lies this far behind the root
FRILL_THICKNESS = 0.07               # half thickness
FRILL_STUDS = 15                     # bony points on the rim
FRILL_SCALLOP = 0.1                  # the rim bulges by this much at every stud
FRILL_FIELD = ((2, 14), (3, 7))      # the coloured field of each half: columns from the top and rows from the root of the shield


# ===========================================================================
# ============================   SKULL HELPERS   ============================
# ===========================================================================
#
#   _dense      Keys of a hull resampled to a smooth run of rings.
#   _at         The key values of a hull at an x.
#   _hull       A closed skin lofted through rings of its keys.
#   _spike      A tapering horn, tooth or beak along a bent line.
#   _teeth      A row of teeth along the edge of a jaw, on both flanks.
#   _stump      The round plug behind the skull.
#   _jaw_rig    Skull and Jaw bones with the slow gape loop.
#

def _dense(keys, pieces=HULL_PIECES):
    """The keys of a hull with pieces rings between two neighbors, eased by a Catmull-Rom curve per value."""
    rings = []
    last = len(keys) - 1
    for i in range(last):
        before, start, end, after = keys[max(0, i - 1)], keys[i], keys[i + 1], keys[min(last, i + 2)]
        for piece in range(pieces if i < last - 1 else pieces + 1):
            t = piece / pieces
            ring = [0.5 * (2.0 * b + (c - a) * t + (2.0 * a - 5.0 * b + 4.0 * c - d) * t * t + (3.0 * b - a - 3.0 * c + d) * t ** 3)
                    for a, b, c, d in zip(before, start, end, after)]
            # The curve may overshoot between a big and a small ring: keep sizes and roundness sane
            ring[2:5] = [max(0.02, value) for value in ring[2:5]]
            ring[5:7] = [min(1.0, max(0.3, value)) for value in ring[5:7]]
            rings.append(ring)
    return rings


def _at(keys, x):
    """The values of a hull at x, straight between the two keys around it."""
    for start, end in zip(keys, keys[1:]):
        if start[0] <= x <= end[0]:
            t = (x - start[0]) / (end[0] - start[0])
            return [a + (b - a) * t for a, b in zip(start, end)]
    return list(keys[0] if x < keys[0][0] else keys[-1])


def _hull(scene, name, keys, material_name='Skin'):
    """
    A closed skin through the rings of its keys (x, y, half width, up, down, roundness
    above, roundness below): every ring a superellipse with its own upper and lower
    half, so a skull can have a flat roof over a round cheek.
    """
    bm = bmesh.new()
    rings = []
    for x, y, half_width, up, down, round_up, round_down in _dense(keys):
        ring = []
        for k in range(HULL_AROUND):
            angle = math.tau * k / HULL_AROUND
            cosine, sine = math.cos(angle), math.sin(angle)
            power = round_up if sine >= 0 else round_down
            out = math.copysign(abs(cosine) ** power, cosine) * half_width
            rise = math.copysign(abs(sine) ** power, sine) * (up if sine >= 0 else down)
            ring.append(bm.verts.new(G(x, y + rise, out)))
        rings.append(ring)
    for ring, following in zip(rings, rings[1:]):
        for k in range(HULL_AROUND):
            bm.faces.new((ring[k], ring[(k + 1) % HULL_AROUND], following[(k + 1) % HULL_AROUND], following[k]))
    for ring in (rings[0], rings[-1]):
        center = bm.verts.new(sum((vertex.co for vertex in ring), Vector()) / len(ring))
        for k in range(HULL_AROUND):
            bm.faces.new((ring[k], ring[(k + 1) % HULL_AROUND], center))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, name, mesh, material_name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def _spike(scene, name, base, direction, length, radius, material_name='Bone', bend=(0.0, 0.0, 0.0), flat=1.0, blunt=0.0, rings=6, around=10):
    """
    A horn, tooth or beak: round at its base (Godot point), tapering along direction to
    a tip after length, bent by bend * length on the way (Godot axes). flat squeezes it
    across the head (1 = round), blunt leaves this share of the radius at the tip.
    """
    start, heading, curve = G(*base), G(*direction).normalized(), G(*bend)
    bm = bmesh.new()
    loops = []
    for ring in range(rings):
        t = ring / rings
        middle = start + heading * (length * t) + curve * (length * t * t)
        tangent = (heading + curve * (2.0 * t)).normalized()
        side = tangent.cross(Vector((0.0, 1.0, 0.0)))
        side = side.normalized() if side.length > 1e-4 else Vector((0.0, 0.0, 1.0))
        across = side.cross(tangent).normalized()
        reach = radius * (blunt + (1.0 - blunt) * (1.0 - t) ** 0.8)
        loops.append([bm.verts.new(middle + side * (math.cos(math.tau * k / around) * reach) + across * (math.sin(math.tau * k / around) * reach * flat))
                      for k in range(around)])
    tip = bm.verts.new(start + heading * length + curve * length)
    root = bm.verts.new(start)
    for loop, following in zip(loops, loops[1:]):
        for k in range(around):
            bm.faces.new((loop[k], loop[(k + 1) % around], following[(k + 1) % around], following[k]))
    for k in range(around):
        bm.faces.new((loops[-1][k], loops[-1][(k + 1) % around], tip))
        bm.faces.new((loops[0][k], loops[0][(k + 1) % around], root))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, name, mesh, material_name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def _teeth(scene, name, keys, teeth, points_up, inset, thickness, lean=0.0, bend=0.0):
    """
    Teeth (x, length) along the edge of a jaw hull on both flanks: they stand on the
    mouth line at inset times the half width, point down from the upper jaw or up from
    the lower one, lean along the head and curl back by bend.
    """
    built = []
    for index, (x, length) in enumerate(teeth):
        _, y, half_width, up, down, _, _ = _at(keys, x)
        edge = y + up * 0.5 if points_up else y - down * 0.5
        for side in (-1, 1):
            direction = (lean, 1.0 if points_up else -1.0, side * 0.12)
            built.append(_spike(scene, f'{name}{index}_{side}', (x, edge, side * half_width * inset), direction, length, length * thickness,
                                bend=(-bend, 0.0, 0.0), rings=4, around=7))
    return built


def _stump(scene):
    """The neck stump: a round plug of radius 1 behind the skull that sinks into the body."""
    return ellipsoid(scene, 'Stump', G(*STUMP_CENTER), STUMP_SIZE, 'Skin', subdivisions=3)


def _jaw_rig(scene, skull_meshes, jaw_meshes, hinge, tip_x, gape):
    """
    Two bones: Skull holds the head still, Jaw turns the lower jaw about its hinge
    (x, y). One loop "Gape": the jaw drops slowly by gape degrees and closes again.
    """
    armature = rig(scene, 'Rig', [
        ('Skull', G(-0.3, 0, 0), G(hinge[0], 0, 0), None),
        ('Jaw', G(hinge[0], hinge[1], 0), G(tip_x, hinge[1], 0), 'Skull'),
    ])
    rigid = {mesh.name: 'Skull' for mesh in skull_meshes}
    rigid.update({mesh.name: 'Jaw' for mesh in jaw_meshes})
    bind(armature, skull_meshes + jaw_meshes, rigid=rigid)
    # A turn about the flank axis lifts the jaw tip: negative degrees open the mouth
    animate(armature, 'Gape', FPS * GAPE_SECONDS, {
        'Jaw': (AXIS_OUT, lambda share: -gape * 0.5 * (1.0 - math.cos(math.tau * share))),
    })


# ===========================================================================
# ===========================   CROCODILE HEAD   ============================
# ===========================================================================
#
#   build_crochead   The long flat skull of a crocodile.
#

def build_crochead(scene):
    """
    The skull of a crocodile (model space Head: origin at the neck joint, +X forward,
    +Y up, +Z the right flank, the neck stump has radius 1):

                  _socket_
        ==(stump)/ table  \\______________ _mound_
                 \\_cheek  vvVvvvvVvvv vvVVv_)      teeth of both jaws show along
                    \\_____^^^^^^^^^^^^A^^^_/        the closed mouth, A = the big fourth

    A broad flat skull table with bony pads, raised eye sockets, a long low snout that
    is narrowest behind its swollen tip, the nostril mound on that tip. The upper teeth
    hang over the lower jaw, the lower ones stand outside the upper jaw.
    The eyeballs are not part of the model: slot Eye on top of the socket.
    Rig: Skull holds the head still, Jaw drops the lower jaw ("Gape", 4 s).
    """
    upper = _hull(scene, 'Upper', CROC_UPPER)
    lower = _hull(scene, 'Lower', CROC_LOWER)
    skull = [_stump(scene), upper]
    jaw = [lower]

    # Sockets: a raised mound per eye with a ridge running out towards the snout
    for side in (-1, 1):
        x, y, z = CROC_EYE
        skull.append(ellipsoid(scene, f'Socket{side}', G(x, y - 0.2, side * z), (0.5, 0.36, 0.3), 'Skin', subdivisions=3))
        skull.append(ellipsoid(scene, f'Cheek{side}', G(0.75, -0.3, side * 1.02), (0.6, 0.3, 0.4), 'Skin', subdivisions=3))

    # Nostrils: one round mound on the tip with two dark holes
    x, y, _ = CROC_NOSTRIL
    skull.append(ellipsoid(scene, 'Mound', G(x, y, 0), (0.3, 0.3, 0.16), 'Skin', subdivisions=3))
    for side in (-1, 1):
        skull.append(ellipsoid(scene, f'Nostril{side}', G(x + 0.04, y + 0.13, side * 0.11), (0.07, 0.06, 0.035), 'Dark', subdivisions=2))

    # Scutes: low bony pads with a keel on the skull table
    for index, (x, z) in enumerate(CROC_SCUTES):
        for side in (-1, 1):
            skull.append(_spike(scene, f'Scute{index}_{side}', (x + 0.12, 0.74, side * z), (-0.35, 1.0, 0.0), 0.3, 0.22, 'Skin', flat=0.7, blunt=0.25, rings=3, around=6))

    skull += _teeth(scene, 'UpperTooth', CROC_UPPER, CROC_UPPER_TEETH, False, 0.92, 0.32)
    jaw += _teeth(scene, 'LowerTooth', CROC_LOWER, CROC_LOWER_TEETH, True, 1.02, 0.32)

    x, y, z = CROC_EYE
    slot(scene, 'Eye', (x, y + 0.04, z + 0.14), 0.2, out=(0.0, 0.35, 1.0), forward=(1.0, 0.0, 0.0))
    _jaw_rig(scene, skull, jaw, CROC_JAW_HINGE, CROC_LOWER[-1][0], CROC_GAPE)


# ===========================================================================
# =============================   TYRANT HEAD   =============================
# ===========================================================================
#
#   build_rexhead   The huge deep skull of a tyrannosaur.
#

def build_rexhead(scene):
    """
    The skull of a tyrannosaur (model space Head: origin at the neck joint, +X forward,
    +Y up, +Z the right flank, the neck stump has radius 1):

                  __boss_
                 /  (eye) \\___________
        ==(stump)  cheek   | snout    \\       wide cheeks, then the notch: the snout
                 \\        |VVVVVVVVVVV_)      is much narrower than the back of the skull
                  \\_jaw___^^^^^^^^^^_/        banana teeth, a heavy lower jaw

    A deep boxy skull, widest at the cheeks behind the eyes, a rough bony boss over
    each eye, knobs along the ridge of the nose, nostrils at the tip. Long curved teeth
    of mixed length hang from the upper jaw, shorter ones stand in the lower jaw.
    Not part of the model: the eyeballs (slot Eye under the boss) and whatever the
    player puts on the brow (slot Horn, on the midline between the bosses).
    Rig: Skull holds the head still, Jaw drops the lower jaw ("Gape", 4 s).
    """
    upper = _hull(scene, 'Upper', REX_UPPER)
    lower = _hull(scene, 'Lower', REX_LOWER)
    skull = [_stump(scene), upper]
    jaw = [lower]

    for side in (-1, 1):
        x, y, z = REX_BROW
        # Boss: a rough bony ridge over the eye
        skull.append(ellipsoid(scene, f'Boss{side}', G(x, y, side * z), (0.55, 0.24, 0.2), 'Skin', subdivisions=3))
        # Cheek: the back of the skull flares out over the jaw joint
        skull.append(ellipsoid(scene, f'Cheek{side}', G(0.8, -0.1, side * 1.2), (0.5, 0.26, 0.6), 'Skin', subdivisions=3))
        for index, (knob_x, size) in enumerate(REX_KNOBS):
            top = _at(REX_UPPER, knob_x)
            skull.append(ellipsoid(scene, f'Knob{side}_{index}', G(knob_x, top[1] + top[3] - size * 0.35, side * 0.2), (size, size * 0.9, size * 0.7), 'Skin', subdivisions=2))
        skull.append(ellipsoid(scene, f'Nostril{side}', G(3.82, 0.1, side * 0.4), (0.1, 0.05, 0.08), 'Dark', subdivisions=2))

    skull += _teeth(scene, 'UpperTooth', REX_UPPER, REX_UPPER_TEETH, False, 0.92, 0.26, lean=0.1, bend=0.22)
    jaw += _teeth(scene, 'LowerTooth', REX_LOWER, REX_LOWER_TEETH, True, 0.92, 0.26, lean=0.0, bend=0.15)

    slot(scene, 'Eye', REX_EYE, 0.2, out=(0.3, 0.1, 1.0), forward=(1.0, 0.0, 0.0))
    slot(scene, 'Horn', (REX_BROW[0], REX_BROW[1] + 0.12, 0.5), 0.8, out=(0.0, 0.0, 1.0), forward=(1.0, -0.15, 0.0))
    _jaw_rig(scene, skull, jaw, REX_JAW_HINGE, REX_LOWER[-1][0], REX_GAPE)


# ===========================================================================
# =============================   HORNED HEAD   =============================
# ===========================================================================
#
#   _frill_radius   How far the frill reaches at an angle.
#   _frill_point    A point of the frill dish.
#   _frill_sheet    A piece of the dish as a closed sheet.
#   build_ceratopshead   The skull of a triceratops with beak and frill.
#

def _frill_radius(degrees):
    """The reach of the frill at an angle from the top (degrees): tallest over the neck, fading out down the sides."""
    fade = min(1.0, (FRILL_ARC - abs(degrees)) / FRILL_FADE) ** 0.6
    reach = FRILL_REACH[1] + (FRILL_REACH[0] - FRILL_REACH[1]) * math.cos(math.radians(min(90.0, abs(degrees)))) ** 2
    return FRILL_ROOT[1] + (reach - FRILL_ROOT[1]) * fade


def _frill_point(degrees, share, shift):
    """A point of the dish (Blender vector): at an angle from the top, a share of the way from the skull to the rim, shifted along the head."""
    reach = _frill_radius(degrees) - FRILL_ROOT[1]
    scallop = FRILL_SCALLOP * abs(math.cos(math.radians(degrees) * (FRILL_STUDS - 1) / 2 * 180.0 / FRILL_ARC)) * share ** 5
    radius = FRILL_ROOT[1] + reach * share + scallop
    # The dish leans back over the neck, most at the top, and curls up a little at the rim
    lean = -FRILL_SWEEP * share ** 1.25 * reach / (FRILL_REACH[0] - FRILL_ROOT[1])
    return G(FRILL_ROOT[0] + lean + shift, radius * math.cos(math.radians(degrees)), radius * math.sin(math.radians(degrees)))


def _frill_sheet(scene, name, columns, rows, thickness, shift, material_name):
    """A piece of the dish between two columns and two rows of its grid as a closed sheet with a round edge."""
    (first_column, last_column), (first_row, last_row) = columns, rows
    step = 2.0 * FRILL_ARC / FRILL_COLUMNS
    bm = bmesh.new()
    loops = []
    for column in range(first_column, last_column + 1):
        degrees = -FRILL_ARC + column * step
        # Thin at the first and last column: the sheet closes into an edge instead of a cut
        closing = math.sin(math.pi * (column - first_column) / (last_column - first_column)) ** 0.35 if first_column < column < last_column else 0.0
        loop = []
        for sign in (1, -1):
            for row in (range(first_row, last_row + 1) if 1 == sign else range(last_row, first_row - 1, -1)):
                edge = math.sin(math.pi * (row - first_row) / (last_row - first_row)) ** 0.35 if first_row < row < last_row else 0.0
                loop.append(bm.verts.new(_frill_point(degrees, row / FRILL_ROWS, shift + sign * thickness * closing * edge)))
        loops.append(loop)
    for loop, following in zip(loops, loops[1:]):
        count = len(loop)
        for i in range(count):
            bm.faces.new((loop[i], loop[(i + 1) % count], following[(i + 1) % count], following[i]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    # A thin sheet can come out inside out: its faces must look away from its middle
    middle = sum((vertex.co for vertex in bm.verts), Vector()) / len(bm.verts)
    if sum(face.normal.dot(face.calc_center_median() - middle) for face in bm.faces) < 0.0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    sheet = link_mesh(scene, name, mesh, material_name)
    for polygon in sheet.data.polygons:
        polygon.use_smooth = True
    return sheet


def build_ceratopshead(scene):
    """
    The skull of a triceratops (model space Head: origin at the neck joint, +X forward,
    +Y up, +Z the right flank, the neck stump has radius 1):

            .-''''-.
           / frill  \\    (Horn slot)
          |   over   \\__boss___ nose horn
           \\ the neck  (eye)   \\_/\\__
            '-.==(stump) cheek        \\ beak       the frill is one piece with the
                  \\__spike\\__jaw______/v           skull roof and leans back

    A deep narrow snout that ends in a hooked parrot beak (Bone), a short horn on the
    nose, deep cheeks that flare out into a spike, a boss over each eye, and the broad
    bony frill growing out of the skull roof back over the neck: scalloped rim with a
    bony stud on every bulge, a coloured field on each half (BackSkin).
    Not part of the model: the eyeballs (slot Eye) and the two long brow horns (slot
    Horn on the midline between the bosses, for the Horns part).
    No rig: the head is one rigid piece.
    """
    _stump(scene)
    _hull(scene, 'Upper', CERA_UPPER)
    _hull(scene, 'Lower', CERA_LOWER)

    # Beak: the upper one hooks down over the shorter lower one
    _spike(scene, 'BeakUpper', (2.5, -0.04, 0.0), (1.0, -0.15, 0.0), 0.72, 0.45, 'Bone', bend=(0.0, -0.42, 0.0), flat=0.8, rings=8, around=14)
    _spike(scene, 'BeakLower', (2.3, -0.72, 0.0), (1.0, 0.2, 0.0), 0.6, 0.3, 'Bone', flat=0.9, rings=6, around=12)
    _spike(scene, 'NoseHorn', (2.02, 0.42, 0.0), (0.38, 1.0, 0.0), 0.8, 0.25, 'Bone', bend=(0.12, 0.0, 0.0), around=12)
    ellipsoid(scene, 'NoseBoss', G(2.05, 0.42, 0.0), (0.36, 0.34, 0.22), 'Skin', subdivisions=3)

    for side in (-1, 1):
        x, y, z = CERA_BROW
        ellipsoid(scene, f'Boss{side}', G(x, y - 0.1, side * z), (0.4, 0.3, 0.28), 'Skin', subdivisions=3)
        # Cheek: deep, flaring out and down into the jugal spike
        ellipsoid(scene, f'Cheek{side}', G(0.85, -0.55, side * 1.08), (0.6, 0.42, 0.55), 'Skin', subdivisions=3)
        _spike(scene, f'Jugal{side}', (0.8, -0.72, side * 1.2), (-0.1, -0.6, side * 0.8), 0.45, 0.24, 'Skin', blunt=0.15, around=10)
        _spike(scene, f'JugalTip{side}', (0.77, -0.9, side * 1.44), (-0.1, -0.6, side * 0.8), 0.24, 0.11, 'Bone', around=8)
        ellipsoid(scene, f'Nostril{side}', G(2.42, -0.02, side * 0.47), (0.1, 0.07, 0.07), 'Dark', subdivisions=2)

    # Frill: the shield, a coloured field per half, a stud on every bulge of the rim
    _frill_sheet(scene, 'Frill', (0, FRILL_COLUMNS), (0, FRILL_ROWS), FRILL_THICKNESS, 0.0, 'Skin')
    (first_column, last_column), field_rows = FRILL_FIELD
    for name, columns in (('FieldRight', (FRILL_COLUMNS // 2 + first_column, FRILL_COLUMNS // 2 + last_column)),
                          ('FieldLeft', (FRILL_COLUMNS // 2 - last_column, FRILL_COLUMNS // 2 - first_column))):
        _frill_sheet(scene, name, columns, field_rows, FRILL_THICKNESS * 0.6, FRILL_THICKNESS * 1.5, 'BackSkin')
    for index in range(FRILL_STUDS):
        degrees = -FRILL_ARC + 2.0 * FRILL_ARC * index / (FRILL_STUDS - 1)
        if abs(degrees) > FRILL_ARC - 10.0:
            continue
        size = 0.17 * (1.0 - 0.3 * abs(degrees) / FRILL_ARC)
        ellipsoid(scene, f'Stud{index}', _frill_point(degrees, 1.0, 0.0), (size * 0.8, size, size), 'Bone', subdivisions=2)

    slot(scene, 'Eye', CERA_EYE, 0.17, out=(0.2, 0.1, 1.0), forward=(1.0, 0.0, 0.0))
    slot(scene, 'Horn', (CERA_BROW[0], CERA_BROW[1] + 0.1, CERA_BROW[2]), CERA_HORN_SIZE, out=(0.0, 0.0, 1.0), forward=(1.0, 0.25, 0.0))
