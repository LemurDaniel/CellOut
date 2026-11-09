# NOTE:
#   Wings, brain and stalk eye of the organism stage (assets/models/creature/).
#   Registered in build.models.py.

#
#   Chapters:
#     OTHER PARTS   Membrane and feather wings, brain, stalk eye.
#

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

from models.helpers import (
    AXIS_FORWARD,
    AXIS_OUT,
    AXIS_UP,
    FPS,
    G,
    animate,
    bind,
    cone,
    ellipsoid,
    link_mesh,
    rig,
    tube,
    wave,
)


# ===========================================================================
# =============================   OTHER PARTS   =============================
# ===========================================================================
#
#   build_wing        Membrane or feather wing: root at the origin, spreading out (+Z), swept back.
#   build_brain       Two displaced lobes with folds, no rig.
#   build_stalk_eye   A bending stalk up (+Y) with a big eye, pupil forward and out.
#

def build_wing(scene, kind):
    root, elbow, wrist = G(0, 0, 0), G(0.08, 0.08, 0.45), G(-0.02, 0.12, 0.88)
    bones = [('Arm', root, elbow, None), ('Fore', elbow, wrist, 'Arm')]
    meshes = []
    rigid = {}

    # Membrane: long bony fingers fanned back, skin stretched between them with a scalloped trailing edge
    if kind == 'membranewings':
        finger_tips = [G(-0.3, 0.06, 1.2), G(-0.66, 0.0, 0.98), G(-0.82, -0.02, 0.52)]
        thumb_claw = wrist + G(0.08, 0.04, 0.02)
        points = [root, elbow, wrist, *finger_tips, thumb_claw]
        radii = (0.065, 0.045, 0.04, 0.012, 0.012, 0.012, 0.025)
        meshes.append(tube(scene, 'Bones', points, radii, [(0, 1), (1, 2), (2, 3), (2, 4), (2, 5), (2, 6)]))
        meshes.append(cone(scene, 'ThumbClaw', thumb_claw, thumb_claw + G(0.08, -0.02, 0.0), 0.02))
        for i, tip in enumerate(finger_tips):
            bones.append((f'Finger{i}', wrist, tip, 'Fore'))
        # Outline: leading edge, finger tips with scallops dipping in towards the wrist, back to the body
        outline = [
            G(0.16, 0, 0), elbow, wrist, finger_tips[0],
            finger_tips[0].lerp(finger_tips[1], 0.5).lerp(wrist, 0.22), finger_tips[1],
            finger_tips[1].lerp(finger_tips[2], 0.5).lerp(wrist, 0.22), finger_tips[2],
            G(-0.62, -0.01, 0.2), G(-0.5, 0, 0),
        ]
        bm = bmesh.new()
        face = bm.faces.new([bm.verts.new(point) for point in outline])
        bmesh.ops.triangulate(bm, faces=[face])
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=3, use_grid_fill=True)
        mesh = bpy.data.meshes.new('Sail')
        bm.to_mesh(mesh)
        bm.free()
        meshes.append(link_mesh(scene, 'Sail', mesh, 'Membrane'))
        flap_bones = [f'Finger{i}' for i in range(len(finger_tips))]

    # Feathers: a fleshy leading edge, overlapping flight feathers swept back, a row of short coverts on top
    else:
        hand_tip = G(-0.12, 0.1, 1.15)
        meshes.append(tube(scene, 'Edge', [root, elbow, wrist, hand_tip], (0.08, 0.065, 0.055, 0.03), [(0, 1), (1, 2), (2, 3)], 'Feather'))
        bones.append(('Hand', wrist, hand_tip, 'Fore'))

        def along_edge(share):
            """0 = hand tip .. 1 = root, the hand part takes the first 35%."""
            if share < 0.35:
                return hand_tip.lerp(wrist, share / 0.35), 'Hand'
            if share < 0.7:
                return wrist.lerp(elbow, (share - 0.35) / 0.35), 'Fore'
            return elbow.lerp(root, (share - 0.7) / 0.3), 'Arm'

        for row, (count, max_length, min_length, width, lift) in enumerate(((14, 0.8, 0.42, 0.1, 0.0), (10, 0.34, 0.24, 0.12, 0.03))):
            for i in range(count):
                share = i / (count - 1)
                base, bone_name = along_edge(share)
                length = max_length - (max_length - min_length) * share
                sweep = math.radians(48 + share * 42)        # primaries fan out, secondaries trail straight back
                direction = G(-math.sin(sweep), -0.04, math.cos(sweep)).normalized()
                feather = ellipsoid(scene, f'Feather{row}_{i}', Vector((0, 0, 0)), (length * 0.5, width * 0.5, 0.014), 'Feather', subdivisions=2)
                align = Vector((1, 0, 0)).rotation_difference(direction).to_matrix().to_4x4()
                feather.data.transform(Matrix.Translation(base + direction * (length * 0.45) + AXIS_UP * (lift - i * 0.002)) @ align)
                meshes.append(feather)
                rigid[feather.name] = bone_name
        flap_bones = ['Hand']

    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes, rigid=rigid)

    # Flap: the arm beats up and down about the forward axis, the outer bones trail behind
    curves = {
        'Arm':  (AXIS_FORWARD, lambda share: 32.0 * wave(share)),
        'Fore': (AXIS_FORWARD, lambda share: 18.0 * wave(share, -0.12)),
    }
    for bone_name in flap_bones:
        curves[bone_name] = (AXIS_FORWARD, lambda share: 10.0 * wave(share, -0.22))
    animate(armature, 'Flap', FPS, curves)


def build_brain(scene):
    texture = bpy.data.textures.get('CO_BrainFolds') or bpy.data.textures.new('CO_BrainFolds', 'MARBLE')
    texture.marble_type = 'SHARPER'
    texture.noise_scale = 0.14
    texture.turbulence = 9.0
    texture.noise_depth = 3
    for side in (-1, 1):
        ellipsoid(scene, f'Lobe{side}', G(0, 0, side * 0.27), (0.62, 0.3, 0.38), 'Brain', subdivisions=5, texture=texture, strength=0.09)


def build_stalk_eye(scene):
    base, middle, top = G(0, 0, 0), G(0.03, 0.5, 0.02), G(0, 1.0, 0)
    stalk = tube(scene, 'Stalk', [base, middle, top], (0.08, 0.05, 0.06), [(0, 1), (1, 2)])
    ball_center = G(0.02, 1.16, 0)
    white = ellipsoid(scene, 'White', ball_center, (0.24, 0.24, 0.24), 'EyeWhite', subdivisions=3)
    pupil = ellipsoid(scene, 'Pupil', ball_center + G(0.13, 0.04, 0.12), (0.12, 0.12, 0.12), 'Pupil', subdivisions=2)
    glint = ellipsoid(scene, 'Glint', ball_center + G(0.2, 0.12, 0.17), (0.035, 0.035, 0.035), 'Glint', subdivisions=1)

    armature = rig(scene, 'Rig', [
        ('Stalk0', base, middle, None),
        ('Stalk1', middle, top, 'Stalk0'),
        ('Ball', top, G(0, 1.3, 0), 'Stalk1'),
    ])
    bind(armature, [stalk, white, pupil, glint], rigid={'White': 'Ball', 'Pupil': 'Ball', 'Glint': 'Ball'})

    # Sway: the stalk bends in an S, the ball looks around
    animate(armature, 'Sway', FPS * 3, {
        'Stalk0': (AXIS_FORWARD, lambda share: 7.0 * wave(share)),
        'Stalk1': (AXIS_OUT, lambda share: 10.0 * wave(share, 0.3)),
        'Ball':   (AXIS_UP, lambda share: 18.0 * wave(share, 0.55)),
    })
