# NOTE:
#   The ten cell editor parts as micro monsters (assets/models/cell/).
#   Registered in build.models.py.

#
#   Chapters:
#     CELL PARTS   The ten cell editor parts as micro monsters.
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
