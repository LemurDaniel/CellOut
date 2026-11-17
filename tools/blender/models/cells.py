# NOTE:
#   The sixteen cell editor parts as micro monsters, all on the rim (assets/models/cell/).
#   Registered in build.models.py.

#
#   Chapters:
#     CELL PARTS   The sixteen cell editor parts as micro monsters, all on the rim.
#

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

from models.helpers import (
    AXIS_UP,
    FPS,
    G,
    MATERIAL_COLORS,
    animate,
    bind,
    cone,
    curved_claw,
    ellipsoid,
    link_mesh,
    rig,
    tube,
    wave,
)

# Placeholder colors of the materials only cell parts use, the game maps the names in Core/PartModels.cs
MATERIAL_COLORS.update({
    'Sail':  (0.45, 0.9, 0.4, 0.6),
    'Plate': (0.55, 0.6, 0.72, 1.0),
    'Spark': (0.55, 0.88, 1.0, 1.0),
})


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
#   build_pseudopod    A soft arm that splits into two lobes and sways slowly.
#   build_nematocyst   A stinging capsule: lid, coiled thread inside, trigger hair.
#   build_sucker       A stalked cup: a ring of pads around a dark, grooved hollow.
#   build_light_sail   A green fan of leafy sails on a stalk, dotted with grana, sways slowly.
#   build_jet_nozzle   A bulging bladder with droplets and a flared nozzle, dark inside.
#   build_slime_gland  Slimy blobs with trapped bubbles, a drooping spout with a hanging drop.
#   build_chromatophore  A cluster of raised pigment stars, the outer rays fanned outwards.
#   build_toxin_gland  A glowing venom sac with a curved spray nozzle and a ring of pores.
#   build_electrocyte  A stack of electric plates, two glowing electrodes, a zig-zag spark.
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


def _aim(direction):
    """Rotation turning +X onto a direction given in Blender axes."""
    return Vector((1, 0, 0)).rotation_difference(direction.normalized()).to_matrix().to_4x4()


def _funnel(scene, name, base, tip, radius_base, radius_tip, material_name, inward=False):
    """An open, flaring cone from base to tip; inward turns its faces to the inside (a lining)."""
    bm = bmesh.new()
    length = (tip - base).length
    bmesh.ops.create_cone(bm, cap_ends=False, segments=18, radius1=radius_base, radius2=radius_tip, depth=length)
    if inward:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    rotation = Vector((0.0, 0.0, 1.0)).rotation_difference((tip - base).normalized()).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(base) @ rotation @ Matrix.Translation((0.0, 0.0, length / 2)), verts=bm.verts)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, name, mesh, material_name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def build_light_sail(scene):
    # A short stalk, then a fan of leafy sails in the plane of the cell, grana dotted on top
    base, crown = G(-0.04, 0, 0), G(0.08, 0.01, 0)
    stalk = tube(scene, 'Stalk', [base, crown], (0.04, 0.026), [(0, 1)])
    meshes, rigid, bones = [stalk], {stalk.name: 'Stalk'}, [('Stalk', base, crown, None)]
    for i, degrees in enumerate((-72, -36, 0, 36, 72)):
        angle = math.radians(degrees)
        length = 0.3 if 0 == degrees else 0.28 - abs(degrees) * 0.0008
        direction = G(math.cos(angle), 0.12 + 0.08 * (i % 2), math.sin(angle)).normalized()
        leaf = ellipsoid(scene, f'Leaf{i}', Vector((0, 0, 0)), (length / 2, 0.05, 0.01), 'Sail', subdivisions=3)
        leaf.data.transform(Matrix.Translation(crown + direction * (length / 2 + 0.01)) @ _aim(direction))
        rib = cone(scene, f'Rib{i}', crown, crown + direction * (length * 0.95), 0.009, 'Grana')
        meshes += [leaf, rib]
        rigid[leaf.name] = rigid[rib.name] = f'Leaf{i}'
        # Grana: flat dark discs on the upper face, left and right of the rib
        side = direction.cross(Vector((0, 0, 1))).normalized()
        for j, (share, offset) in enumerate(((0.3, 0.02), (0.45, -0.022), (0.6, 0.022), (0.75, -0.02), (0.88, 0.01))):
            at = crown + direction * (0.01 + length * share) + side * offset + Vector((0, 0, 0.011))
            bead = ellipsoid(scene, f'Bead{i}{j}', Vector((0, 0, 0)), (0.013, 0.013, 0.005), 'Grana', subdivisions=2)
            bead.data.transform(Matrix.Translation(at) @ _aim(direction))
            meshes.append(bead)
            rigid[bead.name] = f'Leaf{i}'
        bones.append((f'Leaf{i}', crown, crown + direction * length, 'Stalk'))
    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes, rigid=rigid)

    # Sway: the fan swings slowly, every sail a little behind its neighbor
    curves = {'Stalk': (AXIS_UP, lambda share: 7.0 * wave(share))}
    for i in range(5):
        curves[f'Leaf{i}'] = (AXIS_UP, (lambda i: lambda share: 6.0 * wave(share, -0.08 * (i + 1)))(i))
    animate(armature, 'Sway', FPS * 2, curves)


def build_jet_nozzle(scene):
    # A bulging bladder on the rim, droplets inside, a flared nozzle out along +X
    tube(scene, 'Neck', [G(-0.04, 0, 0), G(0.05, 0, 0)], (0.1, 0.11), [(0, 1)])
    ellipsoid(scene, 'Bladder', G(0.13, 0.01, 0), (0.14, 0.15, 0.12), 'Vacuole', subdivisions=4)
    for i, (x, y, z, r) in enumerate(((0.1, 0.04, 0.04, 0.04), (0.16, -0.03, -0.04, 0.035), (0.08, -0.04, -0.02, 0.03), (0.18, 0.05, -0.02, 0.028))):
        ellipsoid(scene, f'Droplet{i}', G(x, y, z), (r, r, r), 'Droplet', subdivisions=2)
    # Nozzle: a chitin funnel flaring outwards, lined dark inside, a lip of beads at the mouth
    start, mouth = G(0.22, 0.01, 0), G(0.4, 0.01, 0)
    _funnel(scene, 'Nozzle', start, mouth, 0.045, 0.1, 'Chitin')
    _funnel(scene, 'Throat', start + G(0.004, 0, 0), mouth + G(-0.004, 0, 0), 0.04, 0.094, 'Dark', inward=True)
    ellipsoid(scene, 'Plug', start + G(0.01, 0, 0), (0.012, 0.045, 0.045), 'Dark', subdivisions=2)
    ring = 12
    for i in range(ring):
        angle = i / ring * math.tau
        ellipsoid(scene, f'Lip{i}', mouth + G(0, math.sin(angle) * 0.1, math.cos(angle) * 0.1), (0.022, 0.022, 0.022), 'Claw', subdivisions=2)
        # A collar of chitin knobs where the nozzle leaves the bladder
        ellipsoid(scene, f'Collar{i}', start + G(0.01, math.sin(angle) * 0.05, math.cos(angle) * 0.05), (0.02, 0.018, 0.018), 'Chitin', subdivisions=2)


def build_slime_gland(scene):
    # A wobbly gland of overlapping blobs on the rim, bubbles trapped inside
    for i, (x, y, z, r) in enumerate(((0.08, 0.0, 0.0, 0.15), (0.16, 0.02, 0.07, 0.1), (0.04, 0.0, -0.11, 0.1), (0.03, 0.01, 0.11, 0.09), (0.17, 0.03, -0.06, 0.09))):
        ellipsoid(scene, f'Blob{i}', G(x, y, z), (r, r, r * 0.8), 'Slime', subdivisions=3)
    for i, (x, y, z, r) in enumerate(((0.1, 0.05, 0.03, 0.035), (0.04, 0.02, -0.07, 0.03), (0.17, 0.03, 0.06, 0.025), (0.07, -0.03, 0.08, 0.025), (0.15, 0.06, -0.05, 0.02))):
        ellipsoid(scene, f'Bubble{i}', G(x, y, z), (r, r, r), 'SlimeBubble', subdivisions=2)
    # Spout: droops outwards and down, a heavy drop hangs from its lip
    points = [G(0.18, 0.02, 0), G(0.28, 0.03, 0), G(0.35, -0.01, 0), G(0.38, -0.07, 0)]
    tube(scene, 'Spout', points, (0.07, 0.05, 0.04, 0.032), [(0, 1), (1, 2), (2, 3)], 'Slime')
    tube(scene, 'Strand', [G(0.38, -0.08, 0), G(0.385, -0.13, 0)], (0.018, 0.012), [(0, 1)], 'Slime', subdivisions=1)
    ellipsoid(scene, 'Drop', G(0.39, -0.17, 0), (0.045, 0.045, 0.055), 'Slime', subdivisions=3)
    ellipsoid(scene, 'DropCore', G(0.385, -0.165, 0.012), (0.018, 0.018, 0.018), 'SlimeBubble', subdivisions=1)
    ellipsoid(scene, 'Glint', G(0.12, 0.13, -0.03), (0.025, 0.02, 0.012), 'Glint', subdivisions=1)


def build_chromatophore(scene):
    # A cluster of flat pigment stars raised on the rim, the outer rays fanned outwards
    stars = ((0.1, 0.0, 0.06, 1.0, 0.0), (0.2, 0.15, 0.03, 0.8, 0.4), (0.18, -0.14, 0.035, 0.75, 1.1), (0.0, 0.0, 0.02, 0.65, 0.8))
    for s, (x, z, y, size, spin) in enumerate(stars):
        center = G(x, y, z)
        ellipsoid(scene, f'Mound{s}', center + G(0, -0.012, 0), (0.1 * size, 0.1 * size, 0.03), 'PigmentLight', subdivisions=2)
        ellipsoid(scene, f'Core{s}', center, (0.055 * size, 0.055 * size, 0.035 * size), 'Pigment' if s % 2 == 0 else 'Dark', subdivisions=3)
        rays = 8
        for i in range(rays):
            angle = i / rays * math.tau + spin
            # Rays pointing out of the cell reach further, as if the cell fans them open
            reach = 1.0 + 0.45 * max(0.0, math.cos(angle))
            length = 0.07 * size * reach
            center_ray = center + G(math.cos(angle) * (0.05 * size + length * 0.8), 0.004, math.sin(angle) * (0.05 * size + length * 0.8))
            ray = ellipsoid(scene, f'Ray{s}{i}', Vector((0, 0, 0)), (length, 0.022 * size, 0.012), 'PigmentLight' if i % 2 else 'Pigment', subdivisions=2)
            ray.data.transform(Matrix.Translation(center_ray) @ Matrix.Rotation(-angle, 4, 'Z'))
        ellipsoid(scene, f'Glint{s}', center + G(-0.015 * size, 0.03 * size, -0.015 * size), (0.012, 0.012, 0.008), 'Glint', subdivisions=1)


def build_toxin_gland(scene):
    # A glowing venom sac on the rim, bubbles inside
    tube(scene, 'Neck', [G(-0.04, 0, 0), G(0.04, 0, 0)], (0.09, 0.1), [(0, 1)])
    ellipsoid(scene, 'Sac', G(0.13, 0.0, 0), (0.15, 0.14, 0.14), 'Toxin', subdivisions=4)
    for i, (x, y, z, r) in enumerate(((0.12, 0.05, 0.04, 0.045), (0.07, -0.02, -0.05, 0.04), (0.17, -0.05, 0.03, 0.035), (0.15, 0.04, -0.06, 0.03), (0.09, 0.07, -0.01, 0.025))):
        ellipsoid(scene, f'Bubble{i}', G(x, y, z), (r, r, r), 'Venom', subdivisions=2)
    # Spray nozzle: a curved, tapering spout bending up and out, a glowing jet hole at the tip
    points = [G(0.24, 0.02, 0), G(0.32, 0.05, 0), G(0.38, 0.1, 0), G(0.41, 0.15, 0)]
    tube(scene, 'Nozzle', points, (0.06, 0.045, 0.035, 0.03), [(0, 1), (1, 2), (2, 3)], 'Chitin')
    direction = (points[3] - points[2]).normalized()
    jet = ellipsoid(scene, 'Jet', Vector((0, 0, 0)), (0.008, 0.016, 0.016), 'Venom', subdivisions=2)
    jet.data.transform(Matrix.Translation(points[3] + direction * 0.034) @ _aim(direction))
    # A ring of small pores around the tip
    side = direction.cross(Vector((0, 0, 1))).normalized()
    other = direction.cross(side).normalized()
    pores = 7
    for i in range(pores):
        angle = i / pores * math.tau
        offset = side * math.cos(angle) + other * math.sin(angle)
        pore = ellipsoid(scene, f'Pore{i}', Vector((0, 0, 0)), (0.006, 0.01, 0.01), 'Dark', subdivisions=1)
        pore.data.transform(Matrix.Translation(points[3] + direction * 0.012 + offset * 0.034) @ _aim(offset))
    ellipsoid(scene, 'Glint', G(0.1, 0.12, -0.05), (0.03, 0.03, 0.02), 'Glint', subdivisions=1)


def build_electrocyte(scene):
    # A stack of flat electric plates out along +X, a fleshy core through them, charge glowing between
    tube(scene, 'Core', [G(-0.04, 0, 0), G(0.2, 0, 0)], (0.045, 0.04), [(0, 1)])
    plates = 5
    for i in range(plates):
        x = 0.02 + i * 0.05
        radius = 0.13 - i * 0.012
        ellipsoid(scene, f'Plate{i}', G(x, 0, 0), (0.024, radius * 0.9, radius), 'Plate', subdivisions=3)
        if i < plates - 1:
            ellipsoid(scene, f'Charge{i}', G(x + 0.025, 0, 0), (0.012, radius * 0.8, radius * 0.85), 'Spark', subdivisions=2)
    # Two knob electrodes on short prongs at the tip, a zig-zag spark jumping between them
    for side, name in ((-1, 'L'), (1, 'R')):
        tube(scene, f'Prong{name}', [G(0.22, 0, side * 0.03), G(0.32, 0, side * 0.085)], (0.022, 0.018), [(0, 1)], 'Plate', subdivisions=1)
        ellipsoid(scene, f'Knob{name}', G(0.34, 0, side * 0.095), (0.035, 0.035, 0.035), 'Spark', subdivisions=2)
    zig = [G(0.36, 0.0, -0.07), G(0.42, 0.015, -0.045), G(0.35, -0.01, -0.015), G(0.43, 0.012, 0.015), G(0.35, 0.0, 0.045), G(0.37, 0.0, 0.07)]
    tube(scene, 'Bolt', zig, [0.009] * len(zig), [(i, i + 1) for i in range(len(zig) - 1)], 'Spark', subdivisions=1)
