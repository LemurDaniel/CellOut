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

from mathutils import Matrix

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
#   on_mouth           A point on the skin of the head, relative to the carved mouth line.
#   mouth_stations     Stations from one mouth corner around the nose to the other.
#   squash             Brings parts built round into the stretched model space.
#   build_filtermouth  Whale mouth: baleen in the carved grin, knobs on the upper jaw, throat pleats.
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


# The opening the game carves for a mouth kind (data/mouths.json, Visual/CreatureClay.cs CarveMouth).
# The numbers here must match the line of the kind in data/mouths.json, or the model leaves its opening.
FILTER_MOUTH = {'length': 2.8, 'level': -0.2, 'smile': 0.28, 'gape': 0.16}

MOUTH_FIT = 1.03                     # the game stretches the model this far past the nose tip and the flanks
MOUTH_HEIGHT = 0.75                  # model y = 1 is this share of the head height
MOUTH_Y_UNIT = 0.66                  # a model y unit in head widths: parts are built round and squashed at the end
WHALE_PLEATS = (0.2, 0.36, 0.52, 0.68, 0.84, 1.0)   # throat pleats: how far each one runs below the lower lip, model y


def on_mouth(shape, degrees, back, gapes=0.0, extra=0.0, push=1.0):
    """
    A point on the skin of a round head, relative to the carved mouth line of a shape.
    degrees runs around the nose (0 the tip, -90 and 90 the flanks), back continues from
    the flanks towards the mouth corners (model x units). gapes moves the point up from
    the mouth line in half openings (1 = upper edge, -1 = lower edge), extra in model y.
    push > 1 stands out of the skin. Returns the point in round head units (see squash)
    and s, 0 at the mouth corners .. 1 at the nose tip.
    """
    length = shape['length']
    cap = 1.0 / length / MOUTH_FIT
    cap_start = (length - 1.0) / length / MOUTH_FIT
    center = -shape['level'] / MOUTH_HEIGHT
    angle = math.radians(degrees)
    height, s, level = 0.0, 1.0, 1.0
    for _ in range(5):
        level = math.sqrt(max(0.02, 1.0 - ((height - center) * MOUTH_HEIGHT) ** 2))
        s = max(0.0, min(1.0, (cap_start + cap * level * math.cos(angle) - back) * MOUTH_FIT))
        ramp = max(0.0, min(1.0, s / 0.3))
        opening = max(shape['gape'] * ramp * ramp * (3.0 - 2.0 * ramp), 0.045) / MOUTH_HEIGHT
        height = shape['smile'] * (1.0 - s) ** 2 / MOUTH_HEIGHT + gapes * opening + extra
    x = cap_start + cap * level * push * math.cos(angle) - back
    return G(x * length * MOUTH_FIT, height * MOUTH_Y_UNIT, level * push * math.sin(angle) / (MOUTH_FIT + 0.01)), s


def mouth_stations(shape, flank_count, front_count):
    """(degrees, back) from the left mouth corner around the nose to the right one."""
    reach = max(0.0, (shape['length'] - 1.0) / shape['length'] / MOUTH_FIT)
    flank = [(90.0, reach * (1.0 - i / flank_count)) for i in range(flank_count)] if reach > 0.0 else []
    front = [(90.0 - i * 180.0 / (front_count - 1), 0.0) for i in range(front_count)]
    return [(-degrees, back) for degrees, back in flank] + [(-degrees, 0.0) for degrees, _ in front] + flank[::-1]


def squash(scene, shape):
    """Parts were built round in head units: brings every mesh into the model space the game stretches."""
    fit = Matrix.Diagonal((1.0 / (shape['length'] * MOUTH_FIT), 1.0, 1.0 / MOUTH_Y_UNIT, 1.0))
    for obj in scene.objects:
        if 'MESH' == obj.type:
            obj.data.transform(fit)


def build_filtermouth(scene):
    """
    What the carved opening cannot do for a baleen whale: the fringe of baleen hanging
    in the long grin, the knobs of a humpback on the upper jaw and the throat pleats
    below the lower lip.
    """
    shape = FILTER_MOUTH
    stations = mouth_stations(shape, 16, 27)

    # Baleen: thin plates hanging from the upper edge of the opening, all along the mouth line
    for i, (degrees, back) in enumerate(stations):
        center, s = on_mouth(shape, degrees, back, 0.1, 0.0, 0.95)
        upper, _ = on_mouth(shape, degrees, back, 1.0, 0.0, 0.95)
        if s < 0.06:
            continue
        ellipsoid(scene, f'Baleen{i}', center, (0.028, 0.028, max(0.02, (upper - center).length)), 'Claw', subdivisions=1)

    # Knobs of a humpback on the upper jaw, in the color of the back
    for i, (degrees, back) in enumerate(mouth_stations(shape, 4, 9)):
        center, s = on_mouth(shape, degrees, back, 1.0, 0.3 + 0.2 * (i % 2), 1.0)
        if s > 0.25:
            ellipsoid(scene, f'Knob{i}', center, (0.06, 0.06, 0.06), 'BackSkin', subdivisions=2)

    # Throat pleats: grooves that follow the jaw line below the lower lip, around the chin
    # and back along the throat. The low ones do not fit under the chin: they only run along the flanks.
    floor = (-shape['level'] / MOUTH_HEIGHT - 0.93 / MOUTH_HEIGHT) * MOUTH_Y_UNIT
    for i, drop in enumerate(WHALE_PLEATS):
        run = []
        for index, (degrees, back) in enumerate(stations + [(0.0, None)]):
            point, s = on_mouth(shape, degrees, back, -1.0, -drop, 1.02) if back is not None else (None, 0.0)
            if point is not None and s > 0.07 * (i + 1) and point.z > floor:
                run.append(point)
                continue
            if len(run) > 2:
                tube(scene, f'Pleat{i}_{index}', run, [0.035] * len(run), [(j, j + 1) for j in range(len(run) - 1)], 'ShellRim', subdivisions=1)
            run = []

    squash(scene, shape)

def build_beak(scene):
    # Upper beak: a blunt horn cap over the nose tip with a small hook, the face stays free
    tube(scene, 'Upper', [G(-0.1, 0.04, 0), G(0.3, 0.05, 0), G(0.6, -0.02, 0), G(0.78, -0.18, 0)], (0.4, 0.36, 0.22, 0.06),
         [(0, 1), (1, 2), (2, 3)], 'Beak')
    # Lower beak: shorter, tucked under the hook
    tube(scene, 'Lower', [G(-0.1, -0.26, 0), G(0.3, -0.28, 0), G(0.6, -0.27, 0)], (0.32, 0.27, 0.08), [(0, 1), (1, 2)], 'Beak')
    for side in (-1, 1):
        ellipsoid(scene, f'Nostril{side}', G(0.5, 0.26, side * 0.11), (0.05, 0.03, 0.025), 'Dark', subdivisions=1)
