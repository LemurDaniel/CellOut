# NOTE:
#   Ends of the sculpted limbs of the organism stage: three feet, a crab tip, a grasping
#   hand, a clawed paw, a crab pincer and a squid club (assets/models/creature/).
#   Registered in build.models.py.

#
#   Chapters:
#     LIMB PARTS   Feet, hand, claw paw, pincer and squid club: the ends of the sculpted limbs.
#

import math

from mathutils import Matrix

from models.helpers import (
    AXIS_FORWARD,
    AXIS_OUT,
    FPS,
    G,
    animate,
    bind,
    curved_claw,
    digit,
    ellipsoid,
    nail,
    rig,
    tube,
)

END_SCALE = 0.5                      # pincer and squid club are modelled one unit long and shrunk to the size of a hand


# ===========================================================================
# =============================   LIMB PARTS   ==============================
# ===========================================================================
#
#   build_foot    One of three feet alone: ankle at the origin, sole below, toes forward. No rig.
#   paw           Paw: round foot, four fat toes with pads and short nails.
#   column_foot   Column foot: thick column on a wide sole, flat nails around the front.
#   talon_foot    Talon foot: three knuckled toes forward, one back, every toe a curved talon.
#   crab_tip      Crab foot: a short segment ending in one pointed chitin hook.
#   build_hand    A grasping hand or a clawed paw alone: wrist at the origin, reaching forward.
#   hand          Four knuckled fingers with nails, a thumb towards the body, a palm pad.
#   claw_paw      A heavy paw with three big sickle claws.
#   build_pincer  The pincer of a crab: a bulbous palm, a fixed and a movable finger.
#   build_sucker_club   The club of a squid tentacle with two rows of suckers.
#
#   The game puts them on the tip of a sculpted limb. Hands grab in two seconds:
#   fingers or claws close and open, the thumb meets them.
#

def build_foot(scene, kind):
    """
    Foot only, the end of a sculpted limb: the ankle at the origin, the sole below it,
    the toes forward (+X). Built at the ankle of the old legs and moved to the origin.
    No rig: the foot stands flat on its limb.
    """
    ankle = {'paw': G(0.03, -0.86, 0), 'columnfoot': G(0.02, -0.84, 0), 'talonfoot': G(-0.08, -0.78, 0), 'crabtip': G(0, 0, 0)}[kind]
    builder = {'paw': paw, 'columnfoot': column_foot, 'talonfoot': talon_foot, 'crabtip': crab_tip}[kind]
    meshes, _ = builder(scene, ankle)
    for mesh in meshes:
        mesh.data.transform(Matrix.Translation(-ankle))


def paw(scene, ankle):
    center = G(0.09, -0.93, 0)
    meshes = [tube(scene, 'Foot', [ankle, center], (0.08, 0.13), [(0, 1)])]
    meshes.append(ellipsoid(scene, 'Sole', center + G(0, -0.06, 0), (0.12, 0.11, 0.035), 'Pad'))
    for i, angle in enumerate((-38, -13, 13, 38)):
        direction = G(math.cos(math.radians(angle)), -0.15, math.sin(math.radians(angle)))
        base = center + direction.normalized() * 0.08
        toe, tip, tip_direction = digit(scene, f'Toe{i}', base, direction, 0.13 - abs(angle) * 0.0008, (0.058, 0.052, 0.06), curl=0.15)
        meshes.append(toe)
        meshes.append(ellipsoid(scene, f'ToePad{i}', tip + G(-0.01, -0.04, 0), (0.04, 0.034, 0.018), 'Pad', subdivisions=2))
        meshes.append(nail(scene, f'Nail{i}', tip, tip_direction, 0.04))
    return meshes, center + G(0.2, -0.05, 0)


def column_foot(scene, ankle):
    center = G(0.03, -0.93, 0)
    meshes = [tube(scene, 'Foot', [ankle, center], (0.16, 0.19), [(0, 1)])]
    meshes.append(ellipsoid(scene, 'Sole', center + G(0, -0.055, 0), (0.19, 0.19, 0.025), 'Pad'))
    for i, angle in enumerate((-60, -20, 20, 60)):
        rim = center + G(math.cos(math.radians(angle)) * 0.2, -0.05, math.sin(math.radians(angle)) * 0.2)
        meshes.append(nail(scene, f'Nail{i}', rim, G(math.cos(math.radians(angle)), 0.5, math.sin(math.radians(angle))), 0.08))
    return meshes, center + G(0.2, -0.05, 0)


def crab_tip(scene, ankle):
    """Crab foot: a short knobbly segment ending in one pointed chitin hook, no sole."""
    knee = ankle + G(0.03, -0.1, 0)
    meshes = [tube(scene, 'Foot', [ankle, knee], (0.075, 0.055), [(0, 1)])]
    meshes.append(curved_claw(scene, 'Tip', knee + G(0, -0.01, 0), G(0.12, -1.0, 0), 0.17, 0.05, curl=-0.12))
    return meshes, knee


def talon_foot(scene, ankle):
    ball = G(0.0, -0.94, 0)
    meshes = [tube(scene, 'Foot', [ankle, ball], (0.05, 0.065), [(0, 1)])]
    toes = [(-28, 0.22), (0, 0.28), (28, 0.22), (180, 0.11)]       # angle, length; 180 = back toe
    for i, (angle, length) in enumerate(toes):
        direction = G(math.cos(math.radians(angle)), -0.1, math.sin(math.radians(angle)) * 0.9)
        toe, tip, tip_direction = digit(scene, f'Toe{i}', ball, direction, length, (0.045, 0.035, 0.04, 0.028, 0.032), curl=0.12)
        meshes.append(toe)
        meshes.append(curved_claw(scene, f'Talon{i}', tip, tip_direction + G(0, 0.25, 0), 0.13 if angle != 180 else 0.09, 0.03, curl=0.55))
    return meshes, ball + G(0.3, -0.05, 0)


def build_hand(scene, kind):
    """
    Hand only, the end of a sculpted limb: the wrist at the origin, the palm reaching
    forward (+X) and a little down, thumb towards the body (-Z). Built at the wrist of
    the old arms and moved to the origin, the fingers or claws keep their bones.
    """
    wrist = G(0.38, -0.62, 0.04)
    builder = hand if kind == 'hand' else claw_paw
    meshes, palm, digits = builder(scene, wrist)
    for mesh in meshes:
        mesh.data.transform(Matrix.Translation(-wrist))

    bones = [('Hand', G(0, 0, 0), palm - wrist, None)]
    rigid = {}
    for bone_name, base, tip, digit_meshes in digits:
        bones.append((bone_name, base - wrist, tip - wrist, 'Hand'))
        for mesh in digit_meshes:
            rigid[mesh.name] = bone_name
    for mesh in meshes:
        rigid.setdefault(mesh.name, 'Hand')
    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes, rigid=rigid)

    # Grab: fingers or claws close and open, the thumb meets them
    curl_degrees = 60.0 if kind == 'hand' else 28.0
    curl = lambda share: curl_degrees * (0.5 - 0.5 * math.cos(share * math.tau))
    curves = {}
    for bone_name, _, _, _ in digits:
        if bone_name == 'Thumb':
            curves[bone_name] = (AXIS_FORWARD, lambda share: curl(share) * 0.7)
        else:
            curves[bone_name] = (AXIS_OUT, lambda share: -curl(share))
    animate(armature, 'Grab', FPS * 2, curves)


def hand(scene, wrist):
    """Returns the meshes, the palm tip and (bone, base, tip, meshes) per digit."""
    palm = wrist + G(0.16, -0.07, 0)
    meshes = [tube(scene, 'Palm', [wrist, palm], (0.075, 0.125), [(0, 1)])]
    meshes.append(ellipsoid(scene, 'PalmPad', palm + G(-0.02, -0.08, 0), (0.11, 0.09, 0.04), 'Pad'))
    digits = []
    for i, (spread, length) in enumerate(((-0.095, 0.2), (-0.032, 0.25), (0.032, 0.26), (0.095, 0.21))):
        base = palm + G(0.09, -0.01, spread)
        direction = G(1.0, -0.35, spread * 1.5)
        finger, tip, tip_direction = digit(scene, f'Finger{i}', base, direction, length, (0.046, 0.042, 0.048, 0.04, 0.046), curl=0.25)
        finger_nail = nail(scene, f'FingerNail{i}', tip, tip_direction, 0.03)
        digits.append((f'Finger{i}', base, tip, [finger, finger_nail]))
        meshes += [finger, finger_nail]
    # Thumb on the inner side (towards the body, -Z), pointing forward and inwards
    base = palm + G(-0.04, -0.01, -0.1)
    thumb, tip, tip_direction = digit(scene, 'Thumb', base, G(0.7, -0.3, -0.6), 0.17, (0.054, 0.048, 0.052), curl=0.15)
    thumb_nail = nail(scene, 'ThumbNail', tip, tip_direction, 0.03)
    digits.append(('Thumb', base, tip, [thumb, thumb_nail]))
    meshes += [thumb, thumb_nail]
    return meshes, palm, digits


def build_pincer(scene):
    """
    The pincer of a crab, the end of a sculpted limb: the wrist at the origin, reaching
    forward (+X). A bulbous palm, a fixed lower finger and a movable upper one, both
    with a row of blunt teeth. Snaps open and shut in two seconds ("Grab").
    """
    # A boxing glove of a palm, taller than wide, with three knuckle bumps on its back
    wrist = tube(scene, 'Wrist', [G(0, 0, 0), G(0.14, 0, 0)], (0.1, 0.12), [(0, 1)])
    palm = ellipsoid(scene, 'Palm', G(0.34, 0.02, 0), (0.33, 0.24, 0.3), 'Skin')
    meshes = [wrist, palm]
    rigid = {'Wrist': 'Hand', 'Palm': 'Hand'}
    for i, (forward, up) in enumerate(((0.16, 0.25), (0.31, 0.31), (0.46, 0.29))):
        bump = ellipsoid(scene, f'Bump{i}', G(forward, up, 0), (0.06, 0.07, 0.045), 'Skin', subdivisions=2)
        meshes.append(bump)
        rigid[bump.name] = 'Hand'

    # Fingers: a fat fixed one below, a hooked movable one above, both end in a dark tip
    lower_points = [G(0.46, -0.12, 0), G(0.74, -0.2, 0), G(0.93, -0.12, 0)]
    upper_points = [G(0.44, 0.14, 0), G(0.7, 0.3, 0), G(0.92, 0.2, 0)]
    lower = tube(scene, 'Lower', lower_points, (0.16, 0.12, 0.07), [(0, 1), (1, 2)])
    upper = tube(scene, 'Upper', upper_points, (0.14, 0.11, 0.065), [(0, 1), (1, 2)])
    lower_tip = tube(scene, 'LowerTip', [G(0.9, -0.13, 0), G(1.02, -0.03, 0)], (0.075, 0.015), [(0, 1)], 'Dark')
    upper_tip = tube(scene, 'UpperTip', [G(0.89, 0.22, 0), G(1.02, 0.06, 0)], (0.07, 0.015), [(0, 1)], 'Dark')
    meshes += [lower, upper, lower_tip, upper_tip]
    rigid.update({'Lower': 'Hand', 'LowerTip': 'Hand', 'Upper': 'Finger', 'UpperTip': 'Finger'})
    for i, forward in enumerate((0.56, 0.68, 0.8)):
        size = 0.055 - i * 0.008
        lower_tooth = ellipsoid(scene, f'LowerTooth{i}', G(forward, -0.03 - (0.8 - forward) * 0.1, 0), (size, size, size), 'Claw', subdivisions=1)
        upper_tooth = ellipsoid(scene, f'UpperTooth{i}', G(forward + 0.06, 0.13 + (0.8 - forward) * 0.2, 0), (size, size, size), 'Claw', subdivisions=1)
        meshes += [lower_tooth, upper_tooth]
        rigid[lower_tooth.name] = 'Hand'
        rigid[upper_tooth.name] = 'Finger'

    # Built one unit long for round numbers, the game wants it the size of the other hands
    for mesh in meshes:
        mesh.data.transform(Matrix.Scale(END_SCALE, 4))
    armature = rig(scene, 'Rig', [
        ('Hand', G(0, 0, 0), G(0.4, 0, 0) * END_SCALE, None),
        ('Finger', G(0.4, 0.1, 0) * END_SCALE, G(1.02, 0.1, 0) * END_SCALE, 'Hand'),
    ])
    bind(armature, meshes, rigid=rigid)

    # Grab: the upper finger lifts and snaps back down
    animate(armature, 'Grab', FPS * 2, {
        'Finger': (AXIS_OUT, lambda share: 24.0 * max(0.0, math.sin(share * math.tau)) ** 2),
    })


CLUB_WIDEN = 1.35                    # the squid club is a leaf: wider than its tube ...
CLUB_FLATTEN = 0.6                   # ... and flatter


def build_sucker_club(scene):
    """
    The club of a squid tentacle, the end of a sculpted limb: the wrist at the origin,
    reaching forward (+X) and curling down. It widens into a leaf and ends in a point,
    two rows of suckers sit on its underside. Curls and stretches in two seconds ("Grab").
    """
    points = [G(0, 0, 0), G(0.18, 0.0, 0), G(0.4, -0.02, 0), G(0.62, -0.06, 0), G(0.82, -0.14, 0), G(0.97, -0.26, 0)]
    radii = (0.085, 0.09, 0.16, 0.175, 0.12, 0.03)
    club = tube(scene, 'Club', points, radii, [(i, i + 1) for i in range(len(points) - 1)])
    # A leaf instead of a sausage: flat (Godot Y) and wide (Godot Z), the wrist stays almost round
    for vertex in club.data.vertices:
        leaf = min(1.0, max(0.0, (vertex.co.x - 0.1) / 0.25))
        vertex.co.y *= 1.0 + (CLUB_WIDEN - 1.0) * leaf
        vertex.co.z *= 1.0 + (CLUB_FLATTEN - 1.0) * leaf
    meshes = [club]

    # Two rows of sucker cups on the flat underside, shrinking towards the tip
    for i, (share, cup) in enumerate(((0.36, 0.062), (0.52, 0.075), (0.68, 0.07), (0.83, 0.052))):
        index = max(j for j in range(len(points) - 1) if points[j].x <= share)
        along = (share - points[index].x) / (points[index + 1].x - points[index].x)
        middle = points[index].lerp(points[index + 1], along)
        radius = radii[index] + (radii[index + 1] - radii[index]) * along
        for side in (-1, 1):
            center = G(share, (middle.z - radius * 1.02) * CLUB_FLATTEN, side * radius * CLUB_WIDEN * 0.5)
            meshes.append(ellipsoid(scene, f'Sucker{i}_{side}', center, (cup, cup, cup * 0.6), 'Pad', subdivisions=2))
            meshes.append(ellipsoid(scene, f'SuckerHole{i}_{side}', center + G(0, -cup * 0.42, 0), (cup * 0.55, cup * 0.55, cup * 0.3), 'Dark', subdivisions=2))

    for mesh in meshes:
        mesh.data.transform(Matrix.Scale(END_SCALE, 4))
    armature = rig(scene, 'Rig', [
        ('Club0', points[0] * END_SCALE, points[2] * END_SCALE, None),
        ('Club1', points[2] * END_SCALE, points[4] * END_SCALE, 'Club0'),
        ('Club2', points[4] * END_SCALE, points[5] * END_SCALE, 'Club1'),
    ])
    bind(armature, meshes)

    # Grab: the club curls in and stretches again, the tip the most
    curl = lambda share: 0.5 - 0.5 * math.cos(share * math.tau)
    animate(armature, 'Grab', FPS * 2, {
        'Club0': (AXIS_OUT, lambda share: -8.0 * curl(share)),
        'Club1': (AXIS_OUT, lambda share: -20.0 * curl(share)),
        'Club2': (AXIS_OUT, lambda share: -30.0 * curl(share)),
    })


def claw_paw(scene, wrist):
    palm = G(0.5, -0.68, 0.04)
    meshes = [tube(scene, 'Palm', [wrist, palm], (0.08, 0.11), [(0, 1)])]
    digits = []
    for i, spread in enumerate((-0.07, 0.0, 0.07)):
        base = palm + G(0.07, 0.0, spread)
        knuckle, tip, tip_direction = digit(scene, f'Knuckle{i}', base, G(1.0, -0.2, spread * 2), 0.06, (0.05, 0.045), curl=0.1)
        claw = curved_claw(scene, f'Claw{i}', tip, tip_direction, 0.34 - abs(spread) * 1.2, 0.045, curl=0.6)
        digits.append((f'Claw{i}', base, tip + tip_direction * 0.2, [knuckle, claw]))
        meshes += [knuckle, claw]
    return meshes, palm, digits
