# NOTE:
#   Mouths of the organism stage (assets/models/creature/): the shark jaw, the whale
#   mouth with baleen and the hooked beak. Registered in build.models.py.
#   Model space: the mouth wraps around the snout as a horseshoe that opens to the back.
#   Its back ends sit at x = 0, z = -1 and 1 (the flanks), its front at x = 1 (the nose
#   tip). The game stretches x to the length of the snout and z to its width.

#
#   Chapters:
#     MOUTHS   Shark jaw, whale mouth and beak.
#

import math

from models.helpers import (
    AXIS_OUT,
    FPS,
    G,
    animate,
    bind,
    cone,
    ellipsoid,
    rig,
    tube,
)


# ===========================================================================
# ===============================   MOUTHS   ================================
# ===========================================================================
#
#   around             A point on the horseshoe around the snout.
#   build_jaw          Shark jaw: two gums full of teeth around a dark maw, the lower one chomps.
#   on_snout           A point on the skin of the snout, around the nose and back along the flanks.
#   build_filtermouth  Whale mouth: a long grinning mouth line, a comb of baleen, a heavy lower lip, throat pleats.
#   build_beak         A hooked upper beak over a short lower one.
#

def around(degrees, height, reach=1.0):
    """0 degrees is the nose tip, -90 and 90 the flanks."""
    angle = math.radians(degrees)
    return G(math.cos(angle) * reach, height, math.sin(angle) * reach)


def build_jaw(scene):
    """
    The mouth of a great white, a little open so the teeth always show:
    > Lips:   a fleshy lip of skin over the upper gum, a heavier chin of skin under the lower one
    > Gums:   two thin pink rims the teeth grow from
    > Teeth:  flat triangles, big at the front and smaller towards the corners of the
              mouth, the lower row stands in the gaps of the upper one
    > Maw:    a dark disc between the rows
    Upper lip, upper gum, upper teeth and maw follow the bone Skull, the rest the bone Jaw.
    """
    from mathutils import Matrix     # only the jaw needs it: kept local, the module is shared

    TOOTH_FLATNESS = 0.5             # thickness of a tooth, share of its width
    upper_y, lower_y = 0.0, -0.4
    angles = [-80 + i * 20 for i in range(9)]
    chain = [(i, i + 1) for i in range(len(angles) - 1)]
    # The lips thin out towards the corners of the mouth
    lip_radii = [0.11 - 0.04 * abs(a) / 80 for a in angles]
    chin_radii = [0.15 - 0.06 * abs(a) / 80 for a in angles]
    upper_lip = tube(scene, 'UpperLip', [around(a, upper_y + 0.07, 1.02) for a in angles], lip_radii, chain, 'Skin')
    upper_gum = tube(scene, 'UpperGum', [around(a, upper_y - 0.02, 0.98) for a in angles], [0.065] * len(angles), chain, 'Pad')
    lower_gum = tube(scene, 'LowerGum', [around(a, lower_y + 0.02, 0.92) for a in angles], [0.065] * len(angles), chain, 'Pad')
    chin = tube(scene, 'Chin', [around(a, lower_y - 0.1, 0.95) for a in angles], chin_radii, chain, 'Skin')
    maw = ellipsoid(scene, 'Maw', G(0.25, (upper_y + lower_y) / 2, 0), (0.72, 0.9, 0.17), 'Dark')
    meshes = [upper_lip, upper_gum, lower_gum, chin, maw]
    rigid = {'UpperLip': 'Skull', 'UpperGum': 'Skull', 'Maw': 'Skull', 'LowerGum': 'Jaw', 'Chin': 'Jaw'}

    def tooth(name, degrees, base_y, tip_y, base_reach, tip_reach, radius, bone):
        """A flat triangular tooth on the gum at an angle of the horseshoe, its flat side facing out."""
        base = around(degrees, base_y, base_reach)
        obj = cone(scene, name, base, around(degrees, tip_y, tip_reach), radius)
        # Flattened along the line from the middle of the mouth out through the tooth
        out = around(degrees, 0.0).normalized()
        flatten = Matrix.Identity(3) + (TOOTH_FLATNESS - 1.0) * Matrix((out * out.x, out * out.y, out * out.z))
        obj.data.transform(Matrix.Translation(base) @ flatten.to_4x4() @ Matrix.Translation(-base))
        meshes.append(obj)
        rigid[obj.name] = bone

    # Teeth: 11 hang from the upper gum, 10 stand on the lower one in their gaps,
    # every tooth leans a little into the maw
    UPPER_COUNT, SPREAD = 11, 144.0
    step = SPREAD / (UPPER_COUNT - 1)
    for i in range(UPPER_COUNT):
        degrees = -SPREAD / 2 + i * step
        size = 1.0 - 0.45 * abs(degrees) / (SPREAD / 2)
        tooth(f'UpperTooth{i}', degrees, upper_y - 0.04, upper_y - 0.04 - 0.36 * size, 0.98, 0.93, 0.1 * (0.6 + 0.4 * size), 'Skull')
    for i in range(UPPER_COUNT - 1):
        degrees = -SPREAD / 2 + (i + 0.5) * step
        size = 1.0 - 0.45 * abs(degrees) / (SPREAD / 2)
        tooth(f'LowerTooth{i}', degrees, lower_y + 0.04, lower_y + 0.04 + 0.3 * size, 0.92, 0.86, 0.09 * (0.6 + 0.4 * size), 'Jaw')

    hinge = G(-0.1, lower_y, 0)
    armature = rig(scene, 'Rig', [
        ('Skull', G(-0.1, upper_y, 0), G(0.9, upper_y, 0), None),
        ('Jaw', hinge, G(0.9, lower_y, 0), 'Skull'),
    ])
    bind(armature, meshes, rigid=rigid)

    # Chomp: the lower jaw drops wide open and snaps shut
    animate(armature, 'Chomp', FPS * 2, {
        'Jaw': (AXIS_OUT, lambda share: -18.0 * max(0.0, math.sin(share * math.tau)) ** 2),
    })


# The snout of a marine mammal in the model space of the mouth (CreatureParts3D.BuildMouth):
# a half ellipsoid in front of the last vertebra, behind it the head runs on about as thick
SNOUT_START = -0.13                  # x of the last vertebra
SNOUT_REACH = 1.10                   # from there to the nose tip
SNOUT_CENTER = 0.24                  # height of the spine
SNOUT_HALF_HEIGHT = 1.34
SNOUT_HALF_WIDTH = 0.96

WHALE_MOUTH_BACK = 1.9               # the mouth line runs this far back along the flanks, past the horseshoe
WHALE_SMILE = 0.45                   # the corners of the mouth rise this far: a grin up to the eyes
WHALE_GAP = 0.62                     # height of the open mouth at the nose
WHALE_PLEATS = (0.2, 0.33, 0.46, 0.59, 0.72, 0.85)   # throat pleats: how far each one runs below the lower lip


def on_snout(degrees, back, height, push=1.0):
    """
    A point on the skin of the snout at a height. degrees runs around the nose (0 the
    tip, -90 and 90 the flanks), back continues from the flanks towards the tail.
    push > 1 stands out of the skin, < 1 sinks into it.
    """
    level = math.sqrt(max(0.02, 1.0 - ((height - SNOUT_CENTER) / SNOUT_HALF_HEIGHT) ** 2)) * push
    angle = math.radians(degrees)
    return G(SNOUT_START + SNOUT_REACH * level * math.cos(angle) - back, height, SNOUT_HALF_WIDTH * level * math.sin(angle))


def build_filtermouth(scene):
    # Stations from the left corner of the mouth around the nose to the right corner:
    # (degrees, back, open) with open 0 at the corners and 1 around the nose
    steps = 8
    flank = [(90.0, WHALE_MOUTH_BACK * (1.0 - i / steps), i / steps) for i in range(steps)]
    front = [(90.0 - i * 15.0, 0.0, 1.0) for i in range(13)]
    right = flank[::-1]
    stations = [(-degrees, back, share) for degrees, back, share in flank] + [(-degrees, 0.0, 1.0) for degrees, _, _ in front] \
        + right
    chain = [(i, i + 1) for i in range(len(stations) - 1)]

    def upper_y(share):
        return WHALE_SMILE * (1.0 - share) ** 1.7

    def gap(share):
        return WHALE_GAP * share ** 0.55

    # Lips: the upper one thin, the lower one heavy and jutting out like the jaw of a rorqual,
    # both meet in the corners high on the flanks
    tube(scene, 'UpperLip', [on_snout(d, b, upper_y(s), 1.0) for d, b, s in stations],
         [0.07 + 0.04 * s for _, _, s in stations], chain, 'BackSkin')
    tube(scene, 'LowerLip', [on_snout(d, b, upper_y(s) - gap(s), 1.0 + 0.07 * s) for d, b, s in stations],
         [0.08 + 0.08 * s for _, _, s in stations], chain, 'Skin')

    # Maw: a dark band between the lips, sunk into the skin
    open_stations = [(d, b, s) for d, b, s in stations if gap(s) > 0.1]
    tube(scene, 'Maw', [on_snout(d, b, upper_y(s) - gap(s) / 2, 1.0 - 0.55 * gap(s)) for d, b, s in open_stations],
         [gap(s) * 0.48 for _, _, s in open_stations], [(i, i + 1) for i in range(len(open_stations) - 1)], 'Dark', subdivisions=1)

    # Baleen: a comb of plates hanging from the upper lip all along the mouth line
    combs = [(-90.0 + i * 180.0 / 18, 0.0, 1.0) for i in range(19)]
    for side in (-1.0, 1.0):
        combs += [(side * 90.0, WHALE_MOUTH_BACK * i / 11, 1.0 - i / 11) for i in range(1, 9)]
    for i, (degrees, back, share) in enumerate(combs):
        length = gap(share) * 0.4
        ellipsoid(scene, f'Baleen{i}', on_snout(degrees, back, upper_y(share) - 0.06 - length, 1.04), (0.055, 0.05, length), 'Claw', subdivisions=1)

    # Throat pleats: grooves that follow the jaw line below the lower lip, around the chin
    # and back along the throat. The low ones do not fit under the chin: they only run along the flanks.
    floor = SNOUT_CENTER - SNOUT_HALF_HEIGHT * 0.93
    for i, drop in enumerate(WHALE_PLEATS):
        run = []
        for degrees, back, share in stations + [(0.0, 0.0, None)]:
            height = None if share is None or share <= 0.1 * (i + 1) else upper_y(share) - gap(share) - drop
            if height is not None and height > floor:
                run.append((on_snout(degrees, back, height, 1.03), 0.03 + 0.03 * share))
                continue
            if len(run) > 2:
                tube(scene, f'Pleat{i}_{degrees}_{back}', [point for point, _ in run], [radius for _, radius in run],
                     [(j, j + 1) for j in range(len(run) - 1)], 'ShellRim', subdivisions=1)
            run = []

    # Knobs of a humpback on the upper jaw, in the color of the back
    for i in range(9):
        degrees = -80.0 + i * 20.0
        ellipsoid(scene, f'Knob{i}', on_snout(degrees, 0.0, 0.34 + 0.12 * (i % 2), 1.0), (0.07, 0.07, 0.09), 'BackSkin', subdivisions=2)

def build_beak(scene):
    # Upper beak: a blunt horn cap over the nose tip with a small hook, the face stays free
    tube(scene, 'Upper', [G(-0.1, 0.04, 0), G(0.3, 0.05, 0), G(0.6, -0.02, 0), G(0.78, -0.18, 0)], (0.4, 0.36, 0.22, 0.06),
         [(0, 1), (1, 2), (2, 3)], 'Beak')
    # Lower beak: shorter, tucked under the hook
    tube(scene, 'Lower', [G(-0.1, -0.26, 0), G(0.3, -0.28, 0), G(0.6, -0.27, 0)], (0.32, 0.27, 0.08), [(0, 1), (1, 2)], 'Beak')
    for side in (-1, 1):
        ellipsoid(scene, f'Nostril{side}', G(0.5, 0.26, side * 0.11), (0.05, 0.03, 0.025), 'Dark', subdivisions=1)
