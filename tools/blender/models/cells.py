# NOTE:
#   The eighteen cell editor parts as micro monsters (assets/models/cell/).
#   Registered in build.models.py.

#
#   Chapters:
#     CELL PARTS   The eighteen cell editor parts as micro monsters.
#

import math

from mathutils import Matrix, Vector

from models.helpers import (
    AXIS_UP,
    FPS,
    G,
    animate,
    bind,
    cone,
    curved_claw,
    ellipsoid,
    rig,
    tube,
    wave,
)


# ===========================================================================
# =============================   CELL PARTS   ==============================
# ===========================================================================
#
#   build_flagellum    A ridged whip with a bulging motor at the base and a forked tip.
#   build_cilium       One club-tipped hair, the game sets a row of them along the rim.
#   build_cell_jaw     Two serrated chitin mandibles that snap together.
#   build_spike        A ridged spike with two backward barbs and a collar.
#   build_armor        Three overlapping scale plates with knobs.
#   build_filter       A round sucker mouth: lumpy lips, a ring of teeth, a dark throat.
#   build_eyespot      A friendly round eye: white ball, blue iris, round pupil, a glint.
#   build_chloroplast  A green bean with stacked thylakoid discs.
#   build_vacuole      A translucent bubble with droplets inside.
#   build_toxin        A glowing venom sac with bubbles and a stinger.
#   build_mitochondrion  An orange bean with folded cristae inside.
#   build_starch       Nested starch grains with growth rings and three small grains.
#   build_slime        A wobbly slime gland of blobs with trapped bubbles.
#   build_pseudopod    A soft arm that splits into two lobes and sways slowly.
#   build_pili         A tuft of short, straight hairs with sticky knobs.
#   build_camouflage   A chromatophore: a dark pigment star with light and dark rays.
#   build_nematocyst   A stinging capsule: lid, coiled thread inside, trigger hair.
#   build_sucker       A stalked cup: a ring of pads around a dark, grooved hollow.
#
#   Unit space of a cell (radius 1). Rim parts sit with their base at the origin on the
#   membrane and point outwards along +X, inner parts are centered on the origin.
#

def build_flagellum(scene):
    count = 14
    length = 1.9
    points, radii = [], []
    for i in range(count):
        share = i / (count - 1)
        # Bulging motor near the base, then a ridged taper (every other ring a little thicker)
        bulge = 0.06 * math.exp(-((share - 0.06) / 0.07) ** 2)
        ridge = 1.12 if i % 2 else 1.0
        points.append(G(-0.08 + share * length, 0, 0))
        radii.append((0.085 * (1 - share) ** 0.8 + 0.018 + bulge) * ridge)
    whip = tube(scene, 'Whip', points, radii, [(i, i + 1) for i in range(count - 1)])
    tip = points[-1]
    hooks = [curved_claw(scene, f'Hook{side}', tip, G(0.6, 0, side * 0.8), 0.12, 0.022, curl=0.2) for side in (-1, 1)]

    bones = []
    joints = [G(-0.08 + length * j / 6, 0, 0) for j in range(7)]
    for j in range(6):
        bones.append((f'Whip{j}', joints[j], joints[j + 1], f'Whip{j - 1}' if j else None))
    armature = rig(scene, 'Rig', bones)
    bind(armature, [whip, *hooks], rigid={hook.name: 'Whip5' for hook in hooks})

    # Whip: a wave runs from the base to the tip, growing outwards
    animate(armature, 'Whip', FPS, {
        f'Whip{j}': (AXIS_UP, (lambda j: lambda share: (10.0 + j * 4.0) * wave(share, -j * 0.14))(j)) for j in range(6)
    })


def build_cilium(scene):
    base, middle, tip = G(0, 0, 0), G(0.18, 0, 0), G(0.36, 0, 0)
    hair = tube(scene, 'Hair', [base, middle, tip], (0.04, 0.025, 0.045), [(0, 1), (1, 2)])
    armature = rig(scene, 'Rig', [('Root', base, middle, None), ('Tip', middle, tip, 'Root')])
    bind(armature, [hair])

    # Beat: a quick power stroke backwards, a slow recovery bent forward
    stroke = lambda share: 40.0 * math.sin(share * math.tau) if share < 0.5 else 18.0 * math.sin(share * math.tau)
    animate(armature, 'Beat', FPS, {
        'Root': (AXIS_UP, stroke),
        'Tip':  (AXIS_UP, lambda share: 0.7 * stroke((share - 0.08) % 1.0)),
    })


def build_cell_jaw(scene):
    meshes, bones, rigid = [], [], {}
    for side, name in ((-1, 'L'), (1, 'R')):
        # Mandible: out along +X, curving in towards the middle
        points = [G(-0.02, 0, side * 0.14), G(0.2, 0.01, side * 0.2), G(0.4, 0.02, side * 0.12), G(0.5, 0.02, side * -0.02)]
        mandible = tube(scene, f'Mandible{name}', points, (0.09, 0.075, 0.05, 0.012), [(0, 1), (1, 2), (2, 3)], 'Chitin')
        meshes.append(mandible)
        # Serrated teeth on the inner edge
        for i, share in enumerate((0.3, 0.5, 0.7)):
            at = points[1].lerp(points[2], share) if share < 0.6 else points[2].lerp(points[3], (share - 0.6) * 2)
            tooth = cone(scene, f'Tooth{name}{i}', at, at + G(0.03, 0, -side * 0.09), 0.025)
            meshes.append(tooth)
            rigid[tooth.name] = f'Mandible{name}'
        rigid[mandible.name] = f'Mandible{name}'
        bones.append((f'Mandible{name}', G(0, 0, side * 0.14), G(0.45, 0, side * 0.05), None))
    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes, rigid=rigid)

    # Chomp: both mandibles swing open and snap shut
    snap = lambda share: 22.0 * (0.5 + 0.5 * math.cos(share * math.tau)) ** 2
    animate(armature, 'Chomp', FPS, {
        'MandibleL': (AXIS_UP, lambda share: snap(share)),
        'MandibleR': (AXIS_UP, lambda share: -snap(share)),
    })


def build_spike(scene):
    spike = curved_claw(scene, 'Spike', G(-0.04, 0, 0), G(1, 0, 0), 0.62, 0.15, curl=0.0)
    collar = tube(scene, 'Collar', [G(-0.05, 0, 0), G(0.06, 0, 0)], (0.19, 0.17), [(0, 1)], 'Chitin')
    barbs = [curved_claw(scene, f'Barb{side}', G(0.32, 0, side * 0.05), G(-0.7, 0, side * 0.7), 0.16, 0.035, curl=-0.1) for side in (-1, 1)]
    ridges = [tube(scene, f'Ridge{i}', [G(0.1 + i * 0.12, 0, 0), G(0.13 + i * 0.12, 0, 0)], (0.13 - i * 0.03, 0.12 - i * 0.03), [(0, 1)], 'Claw') for i in range(3)]
    return [spike, collar, *barbs, *ridges]


def build_armor(scene):
    for i, (z, tilt) in enumerate(((-0.24, -0.35), (0.0, 0.0), (0.24, 0.35))):
        plate = ellipsoid(scene, f'Plate{i}', Vector((0, 0, 0)), (0.07, 0.22, 0.26), 'Armor', subdivisions=3)
        plate.data.transform(Matrix.Translation(G(0.03 - abs(z) * 0.15, 0, z)) @ Matrix.Rotation(tilt, 4, 'Z'))
        for j, y in enumerate((-0.12, 0.0, 0.12)):
            base = G(0.08 - abs(z) * 0.15, y, z)
            cone(scene, f'Knob{i}{j}', base, base + G(0.09, 0, 0), 0.04, 'Chitin')


def build_filter(scene):
    ring = 12
    for i in range(ring):
        angle = i / ring * math.tau
        center = G(0.04, math.sin(angle) * 0.17, math.cos(angle) * 0.17)
        ellipsoid(scene, f'Lip{i}', center, (0.07, 0.075, 0.075), 'Skin', subdivisions=2)
        # Teeth on every other lip, pointing into the throat
        if i % 2 == 0:
            tip = G(0.05, math.sin(angle) * 0.06, math.cos(angle) * 0.06)
            cone(scene, f'Tooth{i}', center + G(0.02, 0, 0) - (center - G(0.04, 0, 0)) * 0.3, tip, 0.03)
    ellipsoid(scene, 'Throat', G(0.03, 0, 0), (0.03, 0.15, 0.15), 'Dark', subdivisions=3)


def build_eyespot(scene):
    radius = 0.2
    ellipsoid(scene, 'White', G(0, 0, 0), (radius, radius, radius), 'EyeWhite', subdivisions=4)
    # Iris and pupil: flat discs on the ball, looking forward and a little up
    direction = G(1, 0.25, 0).normalized()
    align = Vector((1, 0, 0)).rotation_difference(direction).to_matrix().to_4x4()
    iris = ellipsoid(scene, 'Iris', Vector((0, 0, 0)), (0.03, 0.11, 0.11), 'Iris', subdivisions=3)
    iris.data.transform(Matrix.Translation(direction * (radius - 0.015)) @ align)
    pupil = ellipsoid(scene, 'Pupil', Vector((0, 0, 0)), (0.025, 0.055, 0.055), 'Pupil', subdivisions=3)
    pupil.data.transform(Matrix.Translation(direction * (radius + 0.002)) @ align)
    ellipsoid(scene, 'Glint', direction * (radius + 0.02) + G(0, 0.035, -0.035), (0.022, 0.022, 0.022), 'Glint', subdivisions=1)


def build_chloroplast(scene):
    ellipsoid(scene, 'Body', G(0, 0, 0), (0.22, 0.14, 0.1), 'Chloro', subdivisions=3)
    for i, x in enumerate((-0.12, -0.04, 0.04, 0.12)):
        for j in range(3):
            ellipsoid(scene, f'Disc{i}{j}', G(x, 0.07 + j * 0.03, 0), (0.035, 0.035, 0.012), 'Grana', subdivisions=2)


def build_vacuole(scene):
    ellipsoid(scene, 'Bubble', G(0, 0, 0), (0.2, 0.2, 0.2), 'Vacuole', subdivisions=4)
    for i, (x, y, z, r) in enumerate(((0.06, 0.04, 0.05, 0.05), (-0.07, -0.02, 0.04, 0.04), (0.0, 0.08, -0.07, 0.035), (-0.03, -0.07, -0.05, 0.045), (0.08, -0.06, -0.02, 0.03))):
        ellipsoid(scene, f'Droplet{i}', G(x, y, z), (r, r, r), 'Droplet', subdivisions=2)


def build_toxin(scene):
    ellipsoid(scene, 'Sac', G(0, 0, 0), (0.2, 0.18, 0.18), 'Toxin', subdivisions=4)
    for i, (x, y, z, r) in enumerate(((0.05, 0.05, 0.04, 0.05), (-0.07, 0.0, 0.05, 0.045), (0.0, -0.06, -0.06, 0.05), (-0.04, 0.07, -0.05, 0.035), (0.08, -0.05, -0.03, 0.03), (-0.1, -0.06, 0.0, 0.03))):
        ellipsoid(scene, f'Bubble{i}', G(x, y, z), (r, r, r), 'Venom', subdivisions=2)
    cone(scene, 'Stinger', G(0.17, 0, 0), G(0.34, 0.02, 0), 0.05)


def build_mitochondrion(scene):
    ellipsoid(scene, 'Body', G(0, 0, 0), (0.23, 0.12, 0.12), 'Mito', subdivisions=4)
    # Cristae: folded inner membranes across the bean, every other one from the other side
    for i, x in enumerate((-0.14, -0.07, 0.0, 0.07, 0.14)):
        side = 1 if i % 2 else -1
        ellipsoid(scene, f'Crista{i}', G(x, side * 0.02, 0), (0.018, 0.085, 0.08), 'Cristae', subdivisions=2)
    ellipsoid(scene, 'Glint', G(0.08, 0.1, -0.05), (0.03, 0.02, 0.03), 'Glint', subdivisions=1)


def build_starch(scene):
    # Growth rings: nested grains, each a little smaller and higher, like an onion
    for i, (scale, y) in enumerate(((0.19, 0.0), (0.15, 0.05), (0.1, 0.09), (0.055, 0.125))):
        ellipsoid(scene, f'Ring{i}', G(0, y, 0), (scale, scale * 0.75, scale), 'Starch' if i % 2 == 0 else 'StarchCore', subdivisions=3)
    for i, (x, z, r) in enumerate(((0.17, 0.1, 0.07), (-0.15, 0.12, 0.06), (0.05, -0.18, 0.065))):
        ellipsoid(scene, f'Grain{i}', G(x, -0.02, z), (r, r * 0.8, r), 'Starch', subdivisions=2)


def build_slime(scene):
    # A wobbly gland of overlapping blobs, bubbles trapped inside
    for i, (x, y, z, r) in enumerate(((0.0, 0.0, 0.0, 0.2), (0.12, 0.02, 0.08, 0.12), (-0.11, -0.01, 0.09, 0.11), (0.02, 0.03, -0.13, 0.12))):
        ellipsoid(scene, f'Blob{i}', G(x, y, z), (r, r * 0.8, r), 'Slime', subdivisions=3)
    for i, (x, y, z, r) in enumerate(((0.05, 0.06, 0.03, 0.04), (-0.06, 0.02, -0.05, 0.03), (0.1, 0.0, -0.04, 0.025), (-0.08, 0.05, 0.08, 0.03))):
        ellipsoid(scene, f'Bubble{i}', G(x, y, z), (r, r, r), 'SlimeBubble', subdivisions=2)


def build_pseudopod(scene):
    # A soft arm that flows outwards and splits into two blunt lobes
    base, middle, fork = G(-0.04, 0, 0), G(0.18, 0.02, 0), G(0.34, 0.03, 0)
    arm = tube(scene, 'Arm', [base, middle, fork], (0.11, 0.085, 0.07), [(0, 1), (1, 2)])
    # Two round lobes at the end instead of a skin fork (forks pinch into spikes)
    for i, (z, r) in enumerate(((0.06, 0.07), (-0.065, 0.065))):
        ellipsoid(scene, f'Lobe{i}', G(0.42, 0.035, z), (r * 1.3, r, r), 'Skin', subdivisions=3)
    lobes = [obj for obj in scene.objects if obj.name.startswith('Lobe')]
    armature = rig(scene, 'Rig', [('Root', base, middle, None), ('Tip', middle, fork, 'Root')])
    bind(armature, [arm, *lobes], rigid={lobe.name: 'Tip' for lobe in lobes})

    # Ooze: a slow sway, the tip lags behind
    animate(armature, 'Ooze', FPS * 2, {
        'Root': (AXIS_UP, lambda share: 12.0 * wave(share)),
        'Tip':  (AXIS_UP, lambda share: 18.0 * wave(share, -0.15)),
    })


def build_pili(scene):
    # A tuft of short, straight hairs fanning out, every one with a sticky knob
    count = 9
    for i in range(count):
        spread = (i / (count - 1) - 0.5) * 1.6
        lift = 0.35 * math.sin(i * 2.3)
        length = 0.32 + 0.08 * math.cos(i * 1.7)
        direction = G(1, lift, spread).normalized()
        base = G(0, lift * 0.06, spread * 0.06)
        tip = base + direction * length
        cone(scene, f'Pilus{i}', tip, base, 0.016, 'Claw')
        ellipsoid(scene, f'Knob{i}', tip, (0.024, 0.024, 0.024), 'StarchCore', subdivisions=1)


def build_camouflage(scene):
    # A chromatophore: a dark pigment star, light and dark rays around it
    ellipsoid(scene, 'Core', G(0, 0, 0), (0.12, 0.05, 0.12), 'Pigment', subdivisions=3)
    rays = 8
    for i in range(rays):
        angle = i / rays * math.tau
        center = G(math.cos(angle) * 0.15, 0, math.sin(angle) * 0.15)
        material_name = 'PigmentLight' if i % 2 else 'Pigment'
        ray = ellipsoid(scene, f'Ray{i}', Vector((0, 0, 0)), (0.08, 0.035, 0.035), material_name, subdivisions=2)
        ray.data.transform(Matrix.Translation(center) @ Matrix.Rotation(-angle, 4, 'Z'))


def build_nematocyst(scene):
    # A capsule on the rim with a lid, a coiled thread inside and a trigger hair on top
    ellipsoid(scene, 'Capsule', G(0.15, 0, 0), (0.15, 0.09, 0.09), 'Nettle', subdivisions=3)
    turns, count = 3.0, 22
    points = [G(0.04 + 0.2 * i / (count - 1), math.sin(i / (count - 1) * turns * math.tau) * 0.045,
                math.cos(i / (count - 1) * turns * math.tau) * 0.045) for i in range(count)]
    tube(scene, 'Thread', points, [0.012] * count, [(i, i + 1) for i in range(count - 1)], 'Venom', subdivisions=1)
    cone(scene, 'Lid', G(0.27, 0, 0), G(0.33, 0, 0), 0.06, 'Chitin')
    cone(scene, 'Trigger', G(0.32, 0, 0), G(0.48, 0.03, 0), 0.012, 'Claw')
    tube(scene, 'Stalk', [G(-0.04, 0, 0), G(0.06, 0, 0)], (0.08, 0.07), [(0, 1)])


def build_sucker(scene):
    # A short fleshy stalk, then a cup: a raised ring of pads around a dark grooved hollow
    tube(scene, 'Stalk', [G(-0.04, 0, 0), G(0.08, 0, 0), G(0.14, 0, 0)], (0.1, 0.09, 0.12), [(0, 1), (1, 2)])
    ring = 14
    for i in range(ring):
        angle = i / ring * math.tau
        ellipsoid(scene, f'Rim{i}', G(0.17, math.sin(angle) * 0.15, math.cos(angle) * 0.15), (0.05, 0.055, 0.055), 'Pad', subdivisions=2)
    ellipsoid(scene, 'Hollow', G(0.15, 0, 0), (0.025, 0.13, 0.13), 'Dark', subdivisions=3)
    for i in range(6):
        angle = i / 6 * math.tau
        groove = ellipsoid(scene, f'Groove{i}', Vector((0, 0, 0)), (0.01, 0.06, 0.012), 'Pad', subdivisions=1)
        groove.data.transform(Matrix.Translation(G(0.16, math.sin(angle) * 0.07, math.cos(angle) * 0.07)) @ Matrix.Rotation(-angle, 4, 'X'))
