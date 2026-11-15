# NOTE:
#   New parts of the snail family of templates: the coiled house of a snail and the
#   striped spiral of a nautilus. Model space Hug (around a unit sphere at the origin,
#   the spine runs along x, the game stretches x to a share of the body length, y to the
#   height and z to the width of the body). A coil has to stay round on the animal, so
#   both are built round in a space of their own and squashed along x at the end
#   (SQUASH_X): the game stretches them back.
#
#   ./tools/blender/Show-Model.ps1 -Module new_snail -Call 'build_spiralshell(scene)' -Name spiralshell
#   ./tools/blender/Show-Model.ps1 -Module new_snail -Call 'build_spiralshell(scene, 1.0)' -Name spiralshell   # round, as on the animal

#
#   Chapters:
#     COILS         A tube growing along a spiral, the base of both shells.
#     SNAIL PARTS   The house of a snail.
#     NAUTILUS PARTS   The striped spiral of a nautilus.
#

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

from models.helpers import G, ellipsoid, link_mesh, tube

SQUASH_X = 0.36                      # the game stretches x about 2.8 times as much as y (reach of the spine against the body height)

RING_SEGMENTS = 24                   # points around the whorl
RINGS_PER_TURN = 84                  # six per growth rib of the house


# ===========================================================================
# ================================   COILS   ================================
# ===========================================================================
#
#   coil_frame    Apex, axis and the two directions of the coil plane.
#   coil_ring     Center and directions of the whorl at an angle.
#   coil_point    A point on the skin of the whorl.
#   coil          The mesh of a whorl (or a band on it) along the spiral.
#   squash        Squashes every mesh of the scene along x, the game stretches it back.
#

def coil_frame(apex, axis, forward):
    """
    The frame of a coil from Godot vectors: the apex (the point every whorl shrinks to),
    the axis (pointing out of the apex) and a forward direction, angle 0 of the coil.
    Angles grow from forward towards down seen from the apex side (a right-hand house).
    """
    axis = Vector(axis).normalized()
    forward = Vector(forward)
    forward = (forward - axis * forward.dot(axis)).normalized()
    up = axis.cross(forward)
    return G(*apex), G(*axis), G(*forward), G(*up)


def coil_ring(frame, shape, angle):
    """Center of the whorl at an angle, its direction away from the axis, the axis and its size there."""
    apex, axis, forward, up = frame
    size = shape['size'] * shape['growth'] ** ((angle - shape['end']) / math.tau)
    out = forward * math.cos(angle) - up * math.sin(angle)
    return apex + (out - axis * shape['drop']) * size, out, axis, size


def coil_point(frame, shape, angle, around, inflate=1.0):
    """A point on the whorl: around 0 is the outer edge, 90 degrees the side of the apex."""
    center, out, axis, size = coil_ring(frame, shape, angle)
    ribs = 1.0 + shape.get('rib', 0.0) * math.cos(angle * shape.get('ribs', 0))
    return center + (out * math.cos(around) * shape['wide'] + axis * math.sin(around) * shape['tall']) * size * inflate * ribs


def coil(scene, name, frame, shape, material_name, turns, sector=None, inflate=1.0, start_turns=0.0, lean=0.0):
    """
    A whorl along a spiral that grows by shape['growth'] per turn and ends at the angle
    shape['end'] with the size shape['size'] (distance of its center from the axis).
    > shape: size, growth, end, drop (center below the apex per size), wide and tall
      (radii of the whorl per size), rib and ribs (depth and count per turn of the growth ribs).
    > turns: how many turns back from the end it starts, start_turns skips the last ones.
    > sector: (from, to) around the whorl in radians for a band, None for the whole tube.
    > inflate: 1 = on the skin of the whorl, a band lies a little above.
    > lean: radians the outer edge trails behind the inner one, a stripe across the whorl sweeps back.
    """
    bm = bmesh.new()
    count = max(2, round((turns - start_turns) * RINGS_PER_TURN))
    is_closed = sector is None
    low, high = (0.0, math.tau) if is_closed else sector
    segments = RING_SEGMENTS if is_closed else max(2, round((high - low) / math.tau * RING_SEGMENTS * 2))
    rings = []
    centers = []
    for step in range(count + 1):
        angle = shape['end'] - math.tau * (turns - (turns - start_turns) * step / count)
        centers.append(coil_ring(frame, shape, angle)[0])
        points = segments if is_closed else segments + 1
        arounds = [low + (high - low) * i / segments for i in range(points)]
        rings.append([bm.verts.new(coil_point(frame, shape, angle - lean * (1.0 + math.cos(around)) * 0.5, around, inflate)) for around in arounds])
    for step in range(count):
        for i in range(segments):
            following = (i + 1) % len(rings[step])
            face = bm.faces.new((rings[step][i], rings[step][following], rings[step + 1][following], rings[step + 1][i]))
            face.normal_update()
            # Outwards: away from the middle line of the whorl
            if face.normal.dot(face.calc_center_median() - centers[step]) < 0.0:
                face.normal_flip()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, name, mesh, material_name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def squash(scene, factor):
    """Squashes every mesh of the scene along x (the spine): the Hug space stretches x by the reach of the part."""
    for obj in scene.objects:
        if 'MESH' == obj.type:
            obj.data.transform(Matrix.Diagonal((factor, 1.0, 1.0, 1.0)))


# ===========================================================================
# =============================   SNAIL PARTS   =============================
# ===========================================================================
#
#   build_spiralshell     The coiled house of a snail on the back.
#

HOUSE = {
    'size': 0.9,                     # distance of the last whorl's center from the axis
    'growth': 2.0,                   # every turn doubles the whorl
    'end': math.radians(18),         # the opening: at the front, a little below the axis
    'drop': 0.9,                     # the whorls slide down the axis: the apex stands out
    'wide': 0.8,
    'tall': 0.92,
    'rib': 0.016,
    'ribs': 14,
}
HOUSE_TURNS = 3.0
HOUSE_CENTER = (-0.05, 1.15, 0.0)    # the axis of the house above the spine: the last whorl sinks into the back (y 0.9)
HOUSE_AXIS = (-0.22, 0.5, 0.84)      # out of the right flank, tilted up and back: the spiral shows from the side and from above


def build_spiralshell(scene, squash_x=SQUASH_X):
    """
    The house of a snail (model space Hug): three whorls of a right-hand coil, the apex
    out of the right flank and tilted up, growth ribs across, two dark bands along the
    whorls, a pale rolled lip at the opening and the collar of the body filling it.
    """
    axis = Vector(HOUSE_AXIS).normalized()
    probe = coil_frame((0, 0, 0), HOUSE_AXIS, (1, 0, 0))
    # The apex: the middle of the house sits half its height down the axis and a little towards the opening
    out_end = coil_ring(probe, HOUSE, HOUSE['end'])[1]
    out_end = Vector((out_end.x, out_end.z, -out_end.y))            # back to Godot axes
    apex = Vector(HOUSE_CENTER) + axis * (HOUSE['drop'] + HOUSE['tall']) * 0.5 * HOUSE['size'] - out_end * 0.27 * HOUSE['size']
    frame = coil_frame(apex, HOUSE_AXIS, (1, 0, 0))

    coil(scene, 'Whorl', frame, HOUSE, 'BackSkin', HOUSE_TURNS)
    start, _, _, start_size = coil_ring(frame, HOUSE, HOUSE['end'] - math.tau * HOUSE_TURNS)
    ellipsoid(scene, 'Apex', start, (start_size * 0.9,) * 3, 'Shell', subdivisions=2)

    # Bands: two on the side of the apex, one below, none on the first small whorl
    for i, (low, high) in enumerate(((16, 40), (56, 68), (-52, -34))):
        coil(scene, f'Band{i}', frame, HOUSE, 'Shell', HOUSE_TURNS - 0.8, sector=(math.radians(low), math.radians(high)), inflate=1.012)

    # Lip: a pale roll around the opening
    lip = [coil_point(frame, HOUSE, HOUSE['end'], math.tau * i / 24, 0.97) for i in range(24)]
    tube(scene, 'Lip', lip, [0.05] * 24, [(i, (i + 1) % 24) for i in range(24)], 'StarchCore', subdivisions=1)

    # Collar: the soft body filling the opening, it joins the back below
    center, out, coil_axis, size = coil_ring(frame, HOUSE, HOUSE['end'] - 0.12)
    collar = ellipsoid(scene, 'Collar', Vector((0, 0, 0)), (size * HOUSE['wide'] * 0.93, size * HOUSE['wide'] * 0.5, size * HOUSE['tall'] * 0.93), 'Skin', subdivisions=3)
    tangent = coil_axis.cross(out)
    collar.data.transform(Matrix.Translation(center) @ Matrix((out, tangent, coil_axis)).transposed().to_4x4())

    squash(scene, squash_x)


# ===========================================================================
# ===========================   NAUTILUS PARTS   ============================
# ===========================================================================
#
#   build_nautilusshell   The striped spiral a nautilus lives in.
#

NAUTILUS_SQUASH_X = 0.45             # the reach of the part is longer than the one of the house

SPIRAL = {
    'size': 1.4,                     # distance of the last whorl's center from the axis
    'growth': 3.0,                   # every turn triples the whorl
    'end': math.radians(-65),        # the opening: below the axis, the body leaves it forwards
    'drop': 0.0,                     # coiled in one plane, both sides alike
    'wide': 0.75,                    # every whorl swallows most of the one before
    'tall': 0.65,
}
SPIRAL_TURNS = 2.0
SPIRAL_BACK = 0.1                    # the opening sits this far behind the spot of the part
STRIPES = 15
STRIPE_FIRST = 0.3                   # turns before the opening: the front bottom stays plain
STRIPE_STEP = 0.064
STRIPE_WIDTH = 0.026
STRIPE_LEAN = 0.16
PATCH_FROM = 0.8                     # turns before the opening the black patch covers, on the whorl before the last
PATCH_TO = 1.0


def build_nautilusshell(scene, squash_x=NAUTILUS_SQUASH_X):
    """
    The shell of a nautilus (model space Hug): a fat spiral coiled in the plane of the
    body, the axis across it above the back, the opening around the body at the spot of
    the part. Cream with stripes in the pattern color that fan out of the dark navel and
    sweep back, none near the opening, a black patch on the old whorl ahead of it. The mantle of the body closes the opening.
    """
    # Coiling the other way round than the house: from the top over the back and below to the front
    end = SPIRAL['end']
    axis_point = (-SPIRAL['size'] * math.cos(end) - SPIRAL_BACK, -SPIRAL['size'] * math.sin(end), 0.0)
    frame = coil_frame(axis_point, (0, 0, -1), (1, 0, 0))

    coil(scene, 'Whorl', frame, SPIRAL, 'EyeWhite', SPIRAL_TURNS)
    for i in range(STRIPES):
        start = STRIPE_FIRST + i * STRIPE_STEP
        if PATCH_FROM - STRIPE_WIDTH < start < PATCH_TO:
            continue
        coil(scene, f'Stripe{i}', frame, SPIRAL, 'BackSkin', start + STRIPE_WIDTH, sector=(math.radians(-140), math.radians(140)),
             inflate=1.008, start_turns=start, lean=STRIPE_LEAN)

    # Black patch: the old whorl is dark where the hood of the animal lies against it, just ahead of the opening
    coil(scene, 'Patch', frame, SPIRAL, 'Dark', PATCH_TO, sector=(math.radians(-115), math.radians(115)), inflate=1.03, start_turns=PATCH_FROM)

    # Navel: the dark hollow both sides coil around
    navel = SPIRAL['size'] * 0.27
    ellipsoid(scene, 'Navel', frame[0], (navel, SPIRAL['size'] * SPIRAL['tall'] * 0.34, navel), 'Dark', subdivisions=3)

    # Mantle: the soft body closing the opening, kept off the navel
    center, out, coil_axis, size = coil_ring(frame, SPIRAL, end - 0.08)
    mantle = ellipsoid(scene, 'Mantle', Vector((0, 0, 0)), (size * SPIRAL['wide'] * 0.8, size * 0.35, size * SPIRAL['tall'] * 0.95), 'Skin', subdivisions=3)
    mantle.data.transform(Matrix.Translation(center + out * size * SPIRAL['wide'] * 0.16) @ Matrix((out, coil_axis.cross(out), coil_axis)).transposed().to_4x4())

    squash(scene, squash_x)