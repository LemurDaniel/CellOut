# NOTE:
#   New parts of the snake family of templates (snake, chameleon): the rattle of a
#   rattlesnake, the forked tongue of a snake and the helmet crest of a chameleon.
#   Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module new_snake -Call 'build_rattle(scene)' -Name rattle
#   ./tools/blender/Show-Model.ps1 -Module new_snake -Call 'build_forktongue(scene)' -Name forktongue
#   ./tools/blender/Show-Model.ps1 -Module new_snake -Call 'build_casque(scene)' -Name casque

#
#   Chapters:
#     HELPERS           A loop keyed on every frame, for fast shakes and a tongue that darts.
#     SNAKE PARTS       The rattle and the forked tongue.
#     CHAMELEON PARTS   The casque.
#

import math

import bmesh
import bpy
from mathutils import Matrix

from models.helpers import AXIS_OUT, AXIS_UP, FPS, G, apply_modifiers, bind, ellipsoid, hinge, link_mesh, rig, tube, wave

RATTLE_LENGTH = 1.3                  # root to the last bell, along the bend
RATTLE_STUB = 0.4                    # the banded skin of the tail ends here, the bells follow
RATTLE_RISE = 62.0                   # degrees the tip points up: a rattlesnake holds its rattle high
RATTLE_STUB_RADII = (0.115, 0.095)   # the skin at the root and where the bells start
RATTLE_BANDS = (0.06, 0.2, 0.34)                                   # dark rings on the pale tail, along the bend
RATTLE_BELLS = 8
RATTLE_BELL = (0.1, 0.135, 0.185)    # half length, half width, half height of the first bell: taller than wide
RATTLE_BELL_TAPER = 0.34             # the last bell is this much smaller
RATTLE_SHAKES = 6                    # shakes per loop
RATTLE_SWING = 15.0                  # degrees to each side

TONGUE_ROOT_X = 0.8                  # inside the head, in snout space (1 = the nose tip)
TONGUE_FORK_X = 1.32                 # the tongue splits here
TONGUE_TIP_X = 1.68
TONGUE_SPREAD = 0.3                  # the tips end this far off the middle (z of snout space: 1 = the flank)
TONGUE_RADIUS = 0.11                 # the game squeezes y more than z: the tongue comes out flat
TONGUE_FLICKS = 3                    # waves of the tongue per loop
TONGUE_PULL = 0.55                   # the tongue shrinks by this share when it darts back in
TONGUE_OUT_SHARE = 0.62              # share of the loop it stays out

CASQUE_HEIGHT = 1.15                 # apex above the back of the head
CASQUE_SINK = 0.2                    # the base dips this far into the head
CASQUE_FRONT = 0.62                  # x of the front edge at the base: over the brow
CASQUE_REAR = -0.5                   # x of the rear edge at the base: the neck
CASQUE_APEX_X = -0.42                # the apex sits far back, over the neck
CASQUE_CROWN = 0.4                   # length of the top before it is rounded off
CASQUE_ROUND_FROM = 0.62             # share of the height where the round apex starts
CASQUE_WIDTH = (0.3, 0.07)           # half width at the base and at the apex: narrow, flat sides
CASQUE_ROWS = 14
CASQUE_AROUND = 16                   # points of one ring
CASQUE_BEAD = 0.036                  # radius of a bead of the rim


# ===========================================================================
# ===============================   HELPERS   ===============================
# ===========================================================================
#
#   animate_dense   One looping action keyed on every frame: rotations and sizes of bones.
#

def animate_dense(armature, name, frames, rotations, sizes=None):
    """
    One looping action like helpers.animate, but with a key on every frame (a shake of
    a few frames per swing would be lost between the sparse keys of the helper) and
    with sizes: rotations maps bone -> (axis, function of the loop share 0..1 to
    degrees), sizes maps bone -> function of the loop share to a uniform scale.
    """
    sizes = sizes or {}
    action = bpy.data.actions.new('CO_' + name)
    action.use_fake_user = False
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame in range(0, frames + 1):
        share = (frame % frames) / frames
        for bone_name, (axis, curve) in rotations.items():
            pose_bone = armature.pose.bones[bone_name]
            pose_bone.rotation_mode = 'QUATERNION'
            pose_bone.rotation_quaternion = hinge(armature, bone_name, axis, curve(share))
            pose_bone.keyframe_insert('rotation_quaternion', frame=frame)
        for bone_name, curve in sizes.items():
            pose_bone = armature.pose.bones[bone_name]
            pose_bone.scale = (curve(share),) * 3
            pose_bone.keyframe_insert('scale', frame=frame)
    # The glTF animation takes the action name, the game asks for it without prefix
    action.name = name
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = frames


# ===========================================================================
# =============================   SNAKE PARTS   =============================
# ===========================================================================
#
#   rattle_point       A point on the bend of the rattle tail and the angle it points up there.
#   build_rattle       The tail of a rattlesnake: black and white rings, then the dry bells, held up.
#   build_forktongue   The forked tongue flicking out of the closed mouth, two nostrils on the snout.
#

def rattle_point(along):
    """
    The point at a distance along the tail (0 = root, RATTLE_LENGTH = tip) and the
    angle in radians the tail points up there. The tail leaves the body level (-X) and
    bends up more and more: the angle grows with the square of the distance.
    """
    STEPS = 40
    x, y = 0.0, 0.0
    for step in range(STEPS):
        angle = math.radians(RATTLE_RISE) * ((step + 0.5) / STEPS * along / RATTLE_LENGTH) ** 2
        x -= math.cos(angle) * along / STEPS
        y += math.sin(angle) * along / STEPS
    return G(x, y, 0), math.radians(RATTLE_RISE) * (along / RATTLE_LENGTH) ** 2


def build_rattle(scene):
    """
    The tail of a rattlesnake (Tail): root at the origin on the tail tip of the body,
    reaching back (-X) and bending up. First the thin end of the tail in pale skin with
    black rings, then the rattle: a stack of dry horn-coloured bells, each taller than
    wide and a little smaller than the one before. The rattle shakes ("Rattle", 1 s).
    """
    def stub_radius(along):
        return RATTLE_STUB_RADII[0] + (RATTLE_STUB_RADII[1] - RATTLE_STUB_RADII[0]) * min(1.0, along / RATTLE_STUB)

    def stand(mesh, along):
        """Moves a mesh built at the origin (long axis x) onto the bend."""
        point, angle = rattle_point(along)
        mesh.data.transform(Matrix.Translation(point) @ Matrix.Rotation(angle, 4, 'Y'))

    # Stub: the end of the tail, pale, starting inside the body
    alongs = [RATTLE_STUB * i / 5 for i in range(6)] + [RATTLE_STUB + 0.06]
    points = [G(0.3, 0, 0)] + [rattle_point(along)[0] for along in alongs]
    radii = [stub_radius(0.0)] + [stub_radius(along) for along in alongs]
    core = tube(scene, 'Core', points, radii, [(i, i + 1) for i in range(len(points) - 1)], 'Starch')
    soft = [core]

    # Rings: black bands a little thicker than the tail
    for i, along in enumerate(RATTLE_BANDS):
        radius = stub_radius(along) * 1.2
        band = ellipsoid(scene, f'Band{i}', G(0, 0, 0), (0.075, radius, radius), 'Dark', subdivisions=3)
        stand(band, along)
        soft.append(band)

    # Bells: a stack from the end of the skin to the tip, the last one a small button
    bells = []
    for i in range(RATTLE_BELLS):
        share = i / (RATTLE_BELLS - 1)
        size = 1.0 - RATTLE_BELL_TAPER * share ** 1.4
        bell = ellipsoid(scene, f'Bell{i}', G(0, 0, 0), tuple(value * size for value in RATTLE_BELL), 'StarchCore', subdivisions=3)
        stand(bell, RATTLE_STUB + 0.05 + (RATTLE_LENGTH - RATTLE_STUB - 0.05) * share)
        bells.append(bell)

    # Rig: two soft bones in the skin, the rattle swings as one stiff piece
    joints = [rattle_point(along)[0] for along in (0.0, RATTLE_STUB * 0.5, RATTLE_STUB, RATTLE_LENGTH)]
    armature = rig(scene, 'Rig', [
        ('Tail0', joints[0], joints[1], None),
        ('Tail1', joints[1], joints[2], 'Tail0'),
        ('Rattle', joints[2], joints[3], 'Tail1'),
    ])
    bind(armature, soft + bells, rigid={bell.name: 'Rattle' for bell in bells})
    animate_dense(armature, 'Rattle', FPS, {
        'Tail1': (AXIS_UP, lambda share: RATTLE_SWING * 0.3 * wave(share * RATTLE_SHAKES)),
        'Rattle': (AXIS_UP, lambda share: RATTLE_SWING * wave(share * RATTLE_SHAKES, -0.1)),
    })


def build_forktongue(scene):
    """
    The tongue of a snake (Snout: x 0 at the mouth corners .. 1 at the nose tip, z the
    width of the head, y 0 the mouth line). The mouth stays a closed line carved into
    the head (data/mouths.json), the model adds the thin tongue that comes out of its
    front and splits into two tips curling apart, and two nostrils on the snout.
    The tongue waves up and down and darts back in for a moment ("Flick", 2 s).
    """
    fork = G(TONGUE_FORK_X, -0.1, 0)
    points = [G(TONGUE_ROOT_X, 0, 0), G(1.15, -0.02, 0), fork]
    radii = [TONGUE_RADIUS, TONGUE_RADIUS * 0.95, TONGUE_RADIUS * 0.85]
    edges = [(0, 1), (1, 2)]
    for side in (-1, 1):
        middle = len(points)
        points += [G((TONGUE_FORK_X + TONGUE_TIP_X) * 0.5, -0.08, side * TONGUE_SPREAD * 0.4), G(TONGUE_TIP_X, 0.06, side * TONGUE_SPREAD)]
        radii += [TONGUE_RADIUS * 0.55, TONGUE_RADIUS * 0.25]
        edges += [(2, middle), (middle, middle + 1)]
    tongue = tube(scene, 'Tongue', points, radii, edges, 'Gill')

    # Nostrils: two dark dots high on the front of the snout
    nostrils = [ellipsoid(scene, f'Nostril{side}', G(0.88, 0.56, side * 0.3), (0.06, 0.09, 0.1), 'Dark', subdivisions=2) for side in (-1, 1)]

    # Rig: the head holds the nostrils, the tongue bends in the middle and at both tips
    armature = rig(scene, 'Rig', [
        ('Head', G(0.2, 0, 0), G(0.6, 0, 0), None),
        ('Root', G(TONGUE_ROOT_X, 0, 0), G(1.15, -0.02, 0), 'Head'),
        ('Mid', G(1.15, -0.02, 0), fork, 'Root'),
        ('TipL', fork, G(TONGUE_TIP_X, 0.06, -TONGUE_SPREAD), 'Mid'),
        ('TipR', fork, G(TONGUE_TIP_X, 0.06, TONGUE_SPREAD), 'Mid'),
    ])
    bind(armature, [tongue] + nostrils, rigid={nostril.name: 'Head' for nostril in nostrils})
    # The weights of the tongue ignore the head bone: it only holds the nostrils
    for vertex in tongue.data.vertices:
        tongue.vertex_groups['Head'].remove([vertex.index])

    def dart(share):
        """1 while the tongue is out, a quick dip towards the mouth for the rest of the loop."""
        if share <= TONGUE_OUT_SHARE:
            return 1.0
        return 1.0 - TONGUE_PULL * math.sin((share - TONGUE_OUT_SHARE) / (1.0 - TONGUE_OUT_SHARE) * math.pi) ** 2

    animate_dense(armature, 'Flick', FPS * 2, {
        'Mid': (AXIS_OUT, lambda share: 16.0 * wave(share * TONGUE_FLICKS)),
        'TipL': (AXIS_OUT, lambda share: 20.0 * wave(share * TONGUE_FLICKS, -0.2)),
        'TipR': (AXIS_OUT, lambda share: 20.0 * wave(share * TONGUE_FLICKS, -0.3)),
    }, sizes={'Root': dart})


# ===========================================================================
# ===========================   CHAMELEON PARTS   ===========================
# ===========================================================================
#
#   casque_row     Front edge, rear edge and half width of the casque at a share of its height.
#   build_casque   The helmet crest of a chameleon: a tall narrow blade leaning back, a ridge on its rim.
#

def casque_row(share):
    """
    The casque at a share of its height (0 = sunk in the head, 1 = apex): x of the
    front edge, x of the rear edge, height and half width. The front edge leans back in
    a bow, the rear edge falls almost straight, both meet in a round apex at the back.
    """
    lead = CASQUE_FRONT - (CASQUE_FRONT - CASQUE_APEX_X - CASQUE_CROWN) * share ** 1.8
    trail = CASQUE_REAR + (CASQUE_APEX_X - CASQUE_REAR) * share - 0.05 * math.sin(share * math.pi)
    # Round apex: the chord closes in a quarter circle over the last part of the height
    closing = max(0.0, (share - CASQUE_ROUND_FROM) / (1.0 - CASQUE_ROUND_FROM))
    chord = (lead - trail) * math.sqrt(max(0.0, 1.0 - closing ** 2))
    middle = (lead + trail) * 0.5 - (lead - trail) * 0.25 * closing ** 2
    height = -CASQUE_SINK + (CASQUE_HEIGHT + CASQUE_SINK) * share
    half_width = CASQUE_WIDTH[1] + (CASQUE_WIDTH[0] - CASQUE_WIDTH[1]) * (1.0 - share) ** 0.8
    return middle + chord * 0.5, middle - chord * 0.5, height, half_width


def build_casque(scene):
    """
    The casque of a veiled chameleon (Top: origin on the back of the head, standing up,
    +X forward): a helmet as tall as the head, narrow and flat on the sides. Its front
    edge climbs from the brow in a bow to a round apex far back, the rear edge drops
    steeply to the neck. A beaded ridge in the paint colour follows the whole rim, a
    second ridge runs up each side (it starts above the head, which is narrower than the helmet). The base sinks into the head. No animation.
    """
    # Rows crowd towards the apex, where the outline turns the most
    rows = [casque_row(math.sin(step / CASQUE_ROWS * math.pi * 0.5)) for step in range(CASQUE_ROWS + 1)]

    # Body: one ring per row, an ellipse from the front to the rear edge, closed at the apex
    bm = bmesh.new()
    rings = []
    for lead, trail, height, half_width in rows[:-1]:
        ring = []
        for step in range(CASQUE_AROUND):
            angle = step / CASQUE_AROUND * math.tau
            # Blunt edges, flat sides: the ellipse is pushed towards a box
            across = math.copysign(abs(math.sin(angle)) ** 0.7, math.sin(angle))
            ring.append(bm.verts.new(G((lead + trail) * 0.5 + (lead - trail) * 0.5 * math.cos(angle), height, half_width * across)))
        rings.append(ring)
    for ring, following in zip(rings, rings[1:]):
        for step in range(CASQUE_AROUND):
            bm.faces.new((ring[step], ring[(step + 1) % CASQUE_AROUND], following[(step + 1) % CASQUE_AROUND], following[step]))
    apex = bm.verts.new(G(rows[-1][0], rows[-1][2], 0))
    for step in range(CASQUE_AROUND):
        bm.faces.new((rings[-1][step], rings[-1][(step + 1) % CASQUE_AROUND], apex))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new('Helmet')
    bm.to_mesh(mesh)
    bm.free()
    helmet = link_mesh(scene, 'Helmet', mesh, 'Skin')
    helmet.modifiers.new('Smooth', 'SUBSURF').levels = 2
    apply_modifiers(helmet)
    for polygon in helmet.data.polygons:
        polygon.use_smooth = True

    # Rim: a row of beads up the front edge, over the apex and down the rear edge
    rim = [G(lead, height, 0) for lead, _, height, _ in rows[1:-1]] + [G(trail, height, 0) for _, trail, height, _ in reversed(rows[1:-1])]
    for i, (point, following) in enumerate(zip(rim, rim[1:])):
        ellipsoid(scene, f'Bead{i}', point.lerp(following, 0.5), (CASQUE_BEAD,) * 3, 'BackSkin', subdivisions=2)

    # Side ridges: from the brow up towards the apex, on both faces of the helmet
    for side in (-1, 1):
        points = []
        for share in (0.24, 0.42, 0.6, 0.78):
            lead, trail, height, half_width = casque_row(share)
            points.append(G(lead + (trail - lead) * (0.3 + 0.25 * share), height, side * half_width * 0.92))
        tube(scene, f'Ridge{side}', points, (0.02, 0.04, 0.035, 0.02), [(i, i + 1) for i in range(len(points) - 1)], 'Skin', subdivisions=1)
