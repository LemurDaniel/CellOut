# NOTE:
#   Parts of the three ability tabs of the creature editor (Gentle, Clever, Wild): the
#   scent gland, the healing moss, the glowing crest, the ink sac, the roar sac and
#   the ram brow. Every part shows its ability on the body: bulbs, moss, light, ink,
#   a throat bubble, horns. Registered in build.models.py.
#
#   ./tools/blender/Show-Model.ps1 -Module new_abilities -Call 'build_scent_gland(scene)' -Name scentgland
#   ./tools/blender/Show-Model.ps1 -Module new_abilities -Call 'build_healing_moss(scene)' -Name healingmoss
#   ./tools/blender/Show-Model.ps1 -Module new_abilities -Call 'build_glow_crest(scene)' -Name glowcrest
#   ./tools/blender/Show-Model.ps1 -Module new_abilities -Call 'build_ink_sac(scene)' -Name inksac
#   ./tools/blender/Show-Model.ps1 -Module new_abilities -Call 'build_roar_sac(scene)' -Name roarsac
#   ./tools/blender/Show-Model.ps1 -Module new_abilities -Call 'build_ram_brow(scene)' -Name rambrow

#
#   Chapters:
#     HELPERS         Ellipsoids in Godot axes, flat plates from an outline, sized keys.
#     ABILITY PARTS   The six builders.
#

import math

import bmesh
import bpy
from mathutils import Matrix

from models.helpers import AXIS_UP, FPS, G, MATERIAL_COLORS, bind, ellipsoid, hinge, link_mesh, rig, tube, wave

# Placeholder colors of the materials only ability parts use, the game maps the names in Core/PartModels.cs
MATERIAL_COLORS.update({
    'Scent':  (0.78, 0.6, 0.9, 1.0),
    'Pollen': (1.0, 0.85, 0.3, 1.0),
    'Bloom':  (0.98, 0.97, 0.92, 1.0),
    'Glow':   (0.3, 1.0, 0.92, 1.0),
    'Ink':    (0.16, 0.08, 0.22, 1.0),
    'Sac':    (0.9, 0.3, 0.28, 1.0),
    'SacLine':(0.62, 0.14, 0.16, 1.0),
    'Horn':   (0.62, 0.52, 0.4, 1.0),
})

SCENT_BULBS = (                      # foot on the pad (x, z), height, lean (x, z), bulb radius
    ((0.06, 0.0), 0.46, (0.04, 0.0), 0.12),
    ((-0.1, 0.1), 0.34, (-0.06, 0.08), 0.1),
    ((-0.08, -0.11), 0.38, (-0.04, -0.1), 0.105),
    ((0.14, 0.12), 0.26, (0.07, 0.09), 0.085),
    ((0.12, -0.12), 0.3, (0.06, -0.1), 0.09),
)
SCENT_POLLEN = ((0.3, 0.8, 0.26, 0.03), (-0.16, 0.96, -0.22, 0.024), (-0.32, 0.62, 0.3, 0.027), (0.36, 0.52, -0.3, 0.022))
SCENT_SWAY = 7.0                     # degrees the bulbs sway to each side
SCENT_LOOP = 2.5                     # seconds

MOSS_TUFTS = (                       # centre (x, y, z), half size (x, y, z): one cushion of soft lumps
    ((0.0, 0.05, 0.0), (0.3, 0.12, 0.24)),
    ((0.16, 0.05, 0.08), (0.16, 0.1, 0.14)),
    ((-0.17, 0.04, -0.06), (0.17, 0.1, 0.15)),
    ((0.04, 0.09, -0.12), (0.15, 0.1, 0.12)),
    ((-0.06, 0.1, 0.12), (0.14, 0.1, 0.12)),
)
MOSS_FLOWERS = ((0.08, 0.2, 0.06, 0.09), (-0.15, 0.17, -0.04, 0.08), (0.0, 0.19, -0.15, 0.07))   # centre, petal reach

CREST_FAN = ((-0.06, 0.38), (0.3, 0.3))   # centre (x, y) and half size of the round top of the fan
CREST_ARC = (-25.0, 205.0)                 # degrees the round top spans, from the front over to the back
CREST_ARC_POINTS = 14
CREST_FOOT = ((-0.15, 0.0), (0.12, 0.0))   # back and front corner of the narrow foot
CREST_THICK = 0.07
CREST_SPOTS = ((0.08, 0.44, 0.04), (-0.08, 0.54, 0.046), (-0.22, 0.4, 0.04), (-0.04, 0.3, 0.034), (0.02, 0.15, 0.026))   # x, y, radius

INK_SAC = ((0.0, -0.26, 0.0), (0.32, 0.3, 0.26))                   # centre and half size of the sac, its top sunk in the belly
INK_FUNNEL = ((-0.22, -0.3, 0.0), (-0.42, -0.28, 0.0), (-0.56, -0.24, 0.0))
INK_DROPS = ((-0.7, -0.27, 0.02, 0.05), (-0.82, -0.32, -0.03, 0.038), (-0.92, -0.28, 0.02, 0.028))

ROAR_CENTER = (0.0, -0.08, 0.3)      # the bubble sits half in the flank, its outer half bulges out
ROAR_SIZE = (0.5, 0.46, 0.44)
ROAR_LINES = 7                       # stretch lines across the bubble
ROAR_PUFF = (0.72, 1.12)             # size of the bubble empty and blown up
ROAR_LOOP = 1.5                      # seconds

RAM_CURL_CENTER = (-0.06, 0.1)      # x, y of the centre of the curl, beside the head
RAM_CURL_RADIUS = (0.27, 0.13)       # at the root and at the tip
RAM_CURL_TURN = (-10.0, 390.0)       # degrees: root in front, up, back, down and forward again
RAM_CURL_OUT = (0.24, 0.66)          # z of the root and of the tip
RAM_HORN_RADIUS = (0.13, 0.035)
RAM_HORN_POINTS = 44
RAM_RIDGES = 0.012                   # the rings of the horn bulge by this much


# ===========================================================================
# ===============================   HELPERS   ===============================
# ===========================================================================
#
#   blob          An ellipsoid with centre and half size in Godot axes.
#   plate         A flat plate of a given outline in the XY plane, with thickness along Z.
#   animate_size  One looping action that scales bones, keyed on every frame.
#

def blob(scene, name, center, size, material_name, subdivisions=3, texture=None, strength=0.0):
    """An ellipsoid in Godot axes: size is the half size along x (forward), y (up) and z (out)."""
    return ellipsoid(scene, name, G(*center), (size[0], size[2], size[1]), material_name, subdivisions, texture, strength)


def plate(scene, name, outline, thickness, material_name):
    """A flat plate through the (x, y) points of outline, thickness along z, closed at both faces."""
    bm = bmesh.new()
    front = [bm.verts.new(G(x, y, thickness * 0.5)) for x, y in outline]
    back = [bm.verts.new(G(x, y, -thickness * 0.5)) for x, y in outline]
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    count = len(outline)
    for i in range(count):
        j = (i + 1) % count
        bm.faces.new((front[j], front[i], back[i], back[j]))
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return link_mesh(scene, name, mesh, material_name)


def animate_size(armature, name, frames, sizes, rotations=None):
    """
    One looping action keyed on every frame: sizes maps bone -> function of the loop
    share 0..1 to a uniform scale, rotations (optional) bone -> (axis, share to degrees).
    """
    rotations = rotations or {}
    action = bpy.data.actions.new('CO_' + name)
    action.use_fake_user = False
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame in range(0, frames + 1):
        share = (frame % frames) / frames
        for bone_name, curve in sizes.items():
            pose_bone = armature.pose.bones[bone_name]
            pose_bone.scale = (curve(share),) * 3
            pose_bone.keyframe_insert('scale', frame=frame)
        for bone_name, (axis, curve) in rotations.items():
            pose_bone = armature.pose.bones[bone_name]
            pose_bone.rotation_mode = 'QUATERNION'
            pose_bone.rotation_quaternion = hinge(armature, bone_name, axis, curve(share))
            pose_bone.keyframe_insert('rotation_quaternion', frame=frame)
    # The glTF animation takes the action name, the game asks for it without prefix
    action.name = name
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = frames


# ===========================================================================
# ============================   ABILITY PARTS   ============================
# ===========================================================================
#
#   build_scent_gland    Top: a cluster of lilac scent bulbs on short stalks, swaying ("Sway", bone Bulbs).
#   build_healing_moss   Row: one cushion of fluffy moss with tiny flowers.
#   build_glow_crest     Row: one upright fan plate with a glowing rim and spots.
#   build_ink_sac        Belly: a glossy dark sac with a funnel pointing back and ink drops.
#   build_roar_sac       FlankPair: a red throat bubble with stretch lines, blown up ("Puff", bone Sac).
#   build_ram_brow       Top: a heavy scratched brow plate with two curled ram horns.
#

def build_scent_gland(scene):
    """
    The scent gland (Top: origin on the back line, +X forward, +Y up): a soft pad in the
    skin with a cluster of flower-bud bulbs (Scent) on short stalks, each with a dark
    pore at its tip, a few pollen beads (Pollen) drifting above. Everything sways
    together on the bone Bulbs ("Sway", 2.5 s).
    """
    meshes = [blob(scene, 'Pad', (0.0, -0.02, 0.0), (0.3, 0.1, 0.26), 'Skin')]
    for index, ((x, z), height, (lean_x, lean_z), radius) in enumerate(SCENT_BULBS):
        foot = G(x, -0.06, z)
        neck = G(x + lean_x * 0.5, height * 0.55, z + lean_z * 0.5)
        head = G(x + lean_x, height, z + lean_z)
        meshes.append(tube(scene, f'Stalk{index}', [foot, neck, head], (0.05, 0.04, 0.045), [(0, 1), (1, 2)], 'Skin'))
        center = (x + lean_x * 1.15, height + radius * 0.8, z + lean_z * 1.15)
        meshes.append(blob(scene, f'Bulb{index}', center, (radius, radius * 1.3, radius), 'Scent'))
        # The petal tips close over the pore: a ring of small bumps, the pore dark in the middle
        top = (center[0], center[1] + radius * 1.18, center[2])
        for petal in range(5):
            angle = petal * math.tau / 5 + index
            meshes.append(blob(scene, f'Tip{index}_{petal}', (top[0] + math.cos(angle) * radius * 0.42, top[1] - radius * 0.12, top[2] + math.sin(angle) * radius * 0.42),
                (radius * 0.3, radius * 0.32, radius * 0.3), 'Scent', subdivisions=2))
        meshes.append(blob(scene, f'Pore{index}', top, (radius * 0.3, radius * 0.14, radius * 0.3), 'Dark', subdivisions=2))
    for index, (x, y, z, radius) in enumerate(SCENT_POLLEN):
        meshes.append(blob(scene, f'Pollen{index}', (x, y, z), (radius, radius, radius), 'Pollen', subdivisions=2))

    armature = rig(scene, 'Rig', [('Bulbs', G(0, -0.05, 0), G(0, 0.6, 0), None)])
    bind(armature, meshes, rigid={mesh.name: 'Bulbs' for mesh in meshes})
    # Sway: forward and back with a little to the sides, like buds in a breeze
    axis = G(0.45, 0.0, 1.0).normalized()
    animate_size(armature, 'Sway', round(FPS * SCENT_LOOP), {}, {
        'Bulbs': (axis, lambda share: SCENT_SWAY * wave(share)),
    })


def build_healing_moss(scene):
    """
    One cushion of healing moss (model space Row: foot at the origin, +X forward, +Y up).
    The game repeats it along the back, a cushion is a little longer than the step, so
    they close to one carpet. Soft lumps of moss (Moss) with a noisy surface, two or
    three tiny white flowers (Bloom) with a yellow heart (Pollen) on top.
    """
    texture = bpy.data.textures.get('CO_MossClouds') or bpy.data.textures.new('CO_MossClouds', 'CLOUDS')
    texture.noise_scale = 0.05
    for index, (center, size) in enumerate(MOSS_TUFTS):
        blob(scene, f'Tuft{index}', center, size, 'Moss', subdivisions=4, texture=texture, strength=0.045)
    for index, (x, y, z, reach) in enumerate(MOSS_FLOWERS):
        for petal in range(5):
            angle = petal * math.tau / 5 + index * 0.7
            blob(scene, f'Petal{index}_{petal}', (x + math.cos(angle) * reach * 0.6, y, z + math.sin(angle) * reach * 0.6),
                (reach * 0.42, reach * 0.16, reach * 0.42), 'Bloom', subdivisions=2)
        blob(scene, f'Heart{index}', (x, y + reach * 0.12, z), (reach * 0.32, reach * 0.25, reach * 0.32), 'Pollen', subdivisions=2)


def build_glow_crest(scene):
    """
    One plate of the glowing crest (model space Row: foot at the origin, +X forward, +Y up).
    An upright fan, narrow at the foot and wide and round on top, leaning back a little:
    the plate takes the dark shell color of the body (Shell), its rim and a few spots on
    both faces shine turquoise (Glow), a low pad of skin (Skin) holds it.
    """
    blob(scene, 'Pad', (0.0, 0.0, 0.0), (0.24, 0.08, 0.13), 'Skin', subdivisions=2)
    (cx, cy), (rx, ry) = CREST_FAN
    arc = []
    for i in range(CREST_ARC_POINTS):
        angle = math.radians(CREST_ARC[0] + (CREST_ARC[1] - CREST_ARC[0]) * i / (CREST_ARC_POINTS - 1))
        arc.append((cx + math.cos(angle) * rx, cy + math.sin(angle) * ry))
    plate(scene, 'Plate', [CREST_FOOT[1]] + arc + [CREST_FOOT[0]], CREST_THICK, 'Shell')
    # The rim: a glowing bead along the round top, from the front over to the back, thickest at the crown
    rim = [G(x, y, 0) for x, y in arc]
    radii = [0.03 + 0.016 * math.sin(math.pi * i / (len(arc) - 1)) for i in range(len(arc))]
    tube(scene, 'Rim', rim, radii, [(i, i + 1) for i in range(len(rim) - 1)], 'Glow')
    for side in (-1, 1):
        for index, (x, y, radius) in enumerate(CREST_SPOTS):
            blob(scene, f'Spot{side}_{index}', (x, y, side * CREST_THICK * 0.45), (radius, radius, radius * 0.4), 'Glow', subdivisions=2)


def build_ink_sac(scene):
    """
    The ink sac (Belly: origin on the belly line, hanging down along -Y, +X forward). A
    glossy dark sac (Ink), its top sunk into the belly, a funnel out of its back end pointing
    backwards (-X) with a dark opening, a few drops of ink trailing behind it and a white
    glint, so it reads as wet from far away.
    """
    blob(scene, 'Sac', *INK_SAC, 'Ink', subdivisions=4)
    funnel = [G(*point) for point in INK_FUNNEL]
    tube(scene, 'Funnel', funnel, (0.1, 0.075, 0.08), [(0, 1), (1, 2)], 'Ink')
    # Rim and opening: discs facing backwards at the end of the funnel
    end = INK_FUNNEL[-1]
    blob(scene, 'Rim', (end[0] - 0.02, end[1], end[2]), (0.035, 0.1, 0.1), 'Ink', subdivisions=2)
    blob(scene, 'Hole', (end[0] - 0.045, end[1], end[2]), (0.02, 0.06, 0.06), 'Dark', subdivisions=2)
    for index, (x, y, z, radius) in enumerate(INK_DROPS):
        blob(scene, f'Drop{index}', (x, y, z), (radius * 1.3, radius, radius), 'Ink', subdivisions=2)
    blob(scene, 'Glint', (0.12, -0.16, 0.2), (0.07, 0.05, 0.03), 'Glint', subdivisions=2)


def build_roar_sac(scene):
    """
    The roar sac (FlankPair: origin on the skin of the right flank, +Z out of the body,
    1 = the body height there). A big round throat bubble (Sac) like a frog's, half sunk
    into the flank, with dark stretch lines (SacLine) running over it. The bone Sac sits
    on the flank and blows the bubble up and lets it go ("Puff", 1.5 s).
    """
    cx, cy, cz = ROAR_CENTER
    sx, sy, sz = ROAR_SIZE
    meshes = [blob(scene, 'Bubble', ROAR_CENTER, ROAR_SIZE, 'Sac', subdivisions=4)]
    # Stretch lines: arcs from the top over the outer face to the bottom, fanned along the body
    for line in range(ROAR_LINES):
        across = -0.75 + 1.5 * line / (ROAR_LINES - 1)
        points = []
        for step in range(9):
            angle = math.radians(-65.0 + 130.0 * step / 8)                     # from the top over the outside down
            ring = math.sqrt(max(0.0, 1.0 - across * across))
            points.append(G(cx + sx * across * 1.01, cy + sy * math.sin(-angle) * ring * 1.01, cz + sz * math.cos(angle) * ring * 1.01))
        meshes.append(tube(scene, f'Line{line}', points, [0.012] * len(points), [(i, i + 1) for i in range(len(points) - 1)], 'SacLine', subdivisions=1))
    meshes.append(blob(scene, 'Glint', (cx + 0.12, cy + 0.22, cz + sz * 0.88), (0.08, 0.06, 0.025), 'Glint', subdivisions=2))

    armature = rig(scene, 'Rig', [('Sac', G(0, cy, 0), G(0, cy, 0.8), None)])
    bind(armature, meshes, rigid={mesh.name: 'Sac' for mesh in meshes})
    # Puff: a quick breath in blows the bubble up, it sinks back slowly
    low, high = ROAR_PUFF

    def puff(share):
        swell = math.sin(min(1.0, share / 0.3) * math.pi * 0.5) if share < 0.3 else 0.5 + 0.5 * math.cos((share - 0.3) / 0.7 * math.pi)
        return low + (high - low) * swell

    animate_size(armature, 'Puff', round(FPS * ROAR_LOOP), {'Sac': puff})


def build_ram_brow(scene):
    """
    The ram brow (Top: origin on the back line, +X forward, +Y up; the player drags it
    onto the head). A heavy armored plate over the brow (Bone) with dark battle
    scratches and a band of horn on its back edge, two thick ridged ram horns (Horn)
    out of its sides, each curling up, back, down and forward again beside the head.
    """
    blob(scene, 'Brow', (0.1, 0.02, 0.0), (0.34, 0.18, 0.36), 'Bone', subdivisions=4)
    blob(scene, 'Ridge', (0.28, 0.06, 0.0), (0.16, 0.13, 0.32), 'Bone', subdivisions=3)
    blob(scene, 'Band', (-0.12, 0.0, 0.0), (0.12, 0.16, 0.33), 'Horn', subdivisions=3)
    # Scratches: short dark gashes across the top of the plate
    for index, (x, z, turn, length) in enumerate(((0.22, 0.1, 0.5, 0.16), (0.08, -0.14, -0.4, 0.14), (0.14, -0.04, 0.3, 0.1))):
        start = (x - math.cos(turn) * length * 0.5, z - math.sin(turn) * length * 0.5)
        end = (x + math.cos(turn) * length * 0.5, z + math.sin(turn) * length * 0.5)
        points = []
        for step in range(4):
            share = step / 3
            px, pz = start[0] + (end[0] - start[0]) * share, start[1] + (end[1] - start[1]) * share
            # On the dome of the plate: height of the brow ellipsoid at that spot
            height = 0.02 + 0.18 * math.sqrt(max(0.0, 1.0 - ((px - 0.1) / 0.34) ** 2 - (pz / 0.36) ** 2))
            points.append(G(px, height + 0.002, pz))
        tube(scene, f'Scratch{index}', points, [0.012] * 4, [(0, 1), (1, 2), (2, 3)], 'Dark', subdivisions=1)

    cx, cy = RAM_CURL_CENTER
    for side in (-1, 1):
        points, radii = [], []
        for i in range(RAM_HORN_POINTS):
            share = i / (RAM_HORN_POINTS - 1)
            angle = math.radians(RAM_CURL_TURN[0] + (RAM_CURL_TURN[1] - RAM_CURL_TURN[0]) * share)
            reach = RAM_CURL_RADIUS[0] + (RAM_CURL_RADIUS[1] - RAM_CURL_RADIUS[0]) * share
            # The turn runs against the clock seen from the right: from the front up and over to the back
            x = cx + math.cos(angle) * reach
            y = cy + math.sin(angle) * reach
            z = RAM_CURL_OUT[0] + (RAM_CURL_OUT[1] - RAM_CURL_OUT[0]) * share ** 0.8
            points.append(G(x, y, side * z))
            ridge = RAM_RIDGES * (1.0 if 0 == i % 2 else -1.0) * (1.0 - share)
            radii.append(RAM_HORN_RADIUS[0] + (RAM_HORN_RADIUS[1] - RAM_HORN_RADIUS[0]) * share ** 0.9 + ridge)
        # The root grows out of the side of the plate
        root = G(0.1, 0.06, side * 0.12)
        tube(scene, f'Horn{side}', [root] + points, [RAM_HORN_RADIUS[0]] + radii, [(i, i + 1) for i in range(RAM_HORN_POINTS)], 'Horn', max_pieces=3)
