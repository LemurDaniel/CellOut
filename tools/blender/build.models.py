# NOTE:
#   Builds the part models of Cell Out in Blender and exports one .glb per part:
#   - assets/models/creature/  feet, hands, wings, fins, brain, stalk eyes of the organism stage
#   - assets/models/cell/      the ten cell editor parts (flagellum, cilium, jaw, spike, ...)
#   Everything is generated: skin modifier tubes, displaced spheres, an armature per
#   moving part with one looping animation (Walk, Grab, Flap, Flutter, Sway, Whip, Beat, Chomp).
#   Materials only carry names (Skin, Claw, Pad, Feather, Membrane, Fin, FinRay, Brain, EyeWhite,
#   Pupil, Glint, Chitin, Armor, Chloro, Grana, Vacuole, Droplet, Toxin, Venom, Iris, Dark),
#   the game recolors them with the body color (Core/PartModels.cs).
#   Requires: Blender 5.2. Usually run through Build-Models.ps1.
#
#   blender -b --factory-startup -P tools/blender/build.models.py -- assets/models
#
#   Inside a running Blender (MCP, text editor) it builds into its own scene
#   "CellOut Parts" and leaves every other scene alone.

#
#   Chapters:
#     HELPERS       Coordinates, scene, materials, tubes, cones, spheres, rigs, weights, keys, export.
#     LIMB PARTS    Three feet, a hand and a claw paw: the ends of the sculpted limbs.
#     OTHER PARTS   Membrane and feather wings, brain, stalk eye.
#     FIN PARTS     Tail fin, flukes, back fin and paddles: rays, webbing, flutter.
#     CELL PARTS    The ten cell editor parts as micro monsters.
#     BUILD ALL     Builds and exports every part.
#

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

SCENE_NAME = 'CellOut Parts'
FPS = 24
TUBE_SUBDIVISIONS = 2
TUBE_STEP = 0.5                      # resampled tube segments are at most this share of their radius long
TUBE_SHRINK_FIX = 1.18               # subdivision rounds the skin hull inwards, radii grow to make up
WEIGHT_POWER = 4                     # higher = sharper bends at the joints

# Godot axes as Blender vectors: +X forward, +Y up, +Z out of the right flank
AXIS_FORWARD = Vector((1.0, 0.0, 0.0))
AXIS_UP = Vector((0.0, 0.0, 1.0))
AXIS_OUT = Vector((0.0, -1.0, 0.0))

MATERIAL_COLORS = {
    'Skin':     (0.62, 0.55, 0.45, 1.0),
    'Claw':     (0.95, 0.92, 0.82, 1.0),
    'Pad':      (0.88, 0.62, 0.6, 1.0),
    'Feather':  (0.92, 0.9, 0.85, 1.0),
    'Membrane': (0.5, 0.42, 0.38, 1.0),
    'Fin':      (0.7, 0.6, 0.55, 1.0),
    'FinRay':   (0.85, 0.78, 0.7, 1.0),
    'Brain':    (0.95, 0.62, 0.7, 1.0),
    'EyeWhite': (0.97, 0.97, 0.97, 1.0),
    'Pupil':    (0.06, 0.06, 0.1, 1.0),
    'Glint':    (1.0, 1.0, 1.0, 1.0),
    'Chitin':   (0.35, 0.12, 0.1, 1.0),
    'Armor':    (0.5, 0.55, 0.62, 1.0),
    'Chloro':   (0.25, 0.7, 0.3, 1.0),
    'Grana':    (0.12, 0.45, 0.18, 1.0),
    'Vacuole':  (0.55, 0.8, 1.0, 0.55),
    'Droplet':  (0.85, 0.95, 1.0, 1.0),
    'Toxin':    (0.6, 0.25, 0.75, 0.7),
    'Venom':    (0.75, 1.0, 0.35, 1.0),
    'Iris':     (0.25, 0.55, 0.85, 1.0),
    'Dark':     (0.12, 0.04, 0.06, 1.0),
}


# ===========================================================================
# ===============================   HELPERS   ===============================
# ===========================================================================
#
#   G               A point given in Godot axes, as Blender vector.
#   build_scene     The scene parts are built in, cleared.
#   material        Named material, created once.
#   tube            Skin modifier tube along a tree of points, applied.
#   cone            A cone from a base point to a tip.
#   ellipsoid       A scaled sphere, optionally displaced.
#   digit           A finger or toe: a tube bending down along its length, knuckle bumps.
#   curved_claw     A tapering claw that curls down, from thick base to sharp tip.
#   rig             An armature with bones given in Godot axes.
#   bind            Parents meshes to the rig, weights by bone distance or rigidly, joins per material.
#   hinge           Rotation of a bone about a Godot axis, in its rest frame.
#   animate         One looping action of hinge keys.
#   export          Writes the scene as .glb.
#

def G(x, y, z):
    """Godot (x forward, y up, z out) to Blender (x forward, -y out, z up)."""
    return Vector((x, -z, y))


def build_scene():
    """In the background the factory scene, inside a running Blender its own scene."""
    if bpy.app.background:
        scene = bpy.context.scene
    else:
        scene = bpy.data.scenes.get(SCENE_NAME) or bpy.data.scenes.new(SCENE_NAME)
        bpy.context.window.scene = scene
    for obj in list(scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        if action.name.startswith('CO_'):
            bpy.data.actions.remove(action)
    scene.render.fps = FPS
    return scene


def material(name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = MATERIAL_COLORS[name]
    mat.use_backface_culling = name not in ('Membrane', 'Feather', 'Fin')
    # The glTF exporter reads the principled node, not the viewport color
    mat.use_nodes = True
    shader = next(node for node in mat.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = MATERIAL_COLORS[name]
    shader.inputs['Roughness'].default_value = 0.35 if name in ('Claw', 'EyeWhite', 'Pupil') else 0.65
    return mat


def _link_mesh(scene, name, mesh, material_name):
    mesh.materials.append(material(material_name))
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj


def _apply_modifiers(obj):
    """Bakes the modifier stack into the mesh without operators (works in the background)."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
    obj.modifiers.clear()
    obj.data = baked


def tube(scene, name, points, radii, edges, material_name='Skin'):
    """
    A smooth tube through a tree of points (skin modifier + subdivision). Point 0 is
    the root, edges connect point indices, radii are per point. Every edge is cut into
    short pieces first (a coarse skin hull shrinks under subdivision, short tubes the
    most), every open end but the root gets a round cap.
    """
    points = [Vector(point) for point in points]
    radii = [radius * TUBE_SHRINK_FIX for radius in radii]
    fine_edges = []
    for start, end in edges:
        pieces = max(1, min(10, math.ceil((points[end] - points[start]).length / (TUBE_STEP * min(radii[start], radii[end])))))
        previous = start
        for piece in range(1, pieces):
            share = piece / pieces
            points.append(points[start].lerp(points[end], share))
            radii.append(radii[start] + (radii[end] - radii[start]) * share)
            fine_edges.append((previous, len(points) - 1))
            previous = len(points) - 1
        fine_edges.append((previous, end))

    # Round caps: every open end but the root (hidden in the body, the skin root breaks with a cap)
    # gets two shrinking points beyond it, a dome instead of a flat cut
    degree = [0] * len(points)
    neighbor = [0] * len(points)
    for start, end in fine_edges:
        degree[start] += 1
        degree[end] += 1
        neighbor[start], neighbor[end] = end, start
    for index in [i for i in range(1, len(degree)) if 1 == degree[i]]:
        outwards = (points[index] - points[neighbor[index]]).normalized()
        previous = index
        for reach, shrink in ((0.55, 0.8), (0.95, 0.4)):
            points.append(points[index] + outwards * radii[index] * reach)
            radii.append(radii[index] * shrink)
            fine_edges.append((previous, len(points) - 1))
            previous = len(points) - 1

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(point) for point in points], fine_edges, [])
    obj = _link_mesh(scene, name, mesh, material_name)
    obj.modifiers.new('Skin', 'SKIN')
    for vertex, radius in zip(mesh.skin_vertices[0].data, radii):
        vertex.radius = (radius, radius)
    mesh.skin_vertices[0].data[0].use_root = True
    obj.modifiers.new('Smooth', 'SUBSURF').levels = TUBE_SUBDIVISIONS
    _apply_modifiers(obj)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def cone(scene, name, base, tip, radius, material_name='Claw'):
    """A cone standing on base, pointing to tip."""
    bm = bmesh.new()
    length = (tip - base).length
    bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=radius, radius2=0.0, depth=length)
    # create_cone is centered on the origin along +Z: move the base to 0, then aim
    rotation = Vector((0.0, 0.0, 1.0)).rotation_difference((tip - base).normalized()).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(base) @ rotation @ Matrix.Translation((0.0, 0.0, length / 2)), verts=bm.verts)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return _link_mesh(scene, name, mesh, material_name)


def ellipsoid(scene, name, center, scale, material_name, subdivisions=3, texture=None, strength=0.0):
    """An icosphere scaled per Blender axis, optionally displaced by a texture."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1.0)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Diagonal((*scale, 1.0)), verts=bm.verts)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = _link_mesh(scene, name, mesh, material_name)
    if texture is not None:
        displace = obj.modifiers.new('Displace', 'DISPLACE')
        displace.texture = texture
        displace.strength = strength
        displace.texture_coords = 'OBJECT'
        _apply_modifiers(obj)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


DOWN = Vector((0.0, 0.0, -1.0))


def _arc(base, direction, length, curl, count):
    """Points from base along direction, bending down by curl * length at the end."""
    direction = direction.normalized()
    return [base + direction * (length * t) + DOWN * (length * curl * t * t) for t in (i / (count - 1) for i in range(count))]


def digit(scene, name, base, direction, length, radii, curl=0.2, material_name='Skin'):
    """
    A finger or toe with one point per radius (base knuckle .. tip). Bigger radii in
    the middle make knuckles, a big last radius a fat fingertip.
    Returns the mesh, the tip point and the direction at the tip.
    """
    points = _arc(base, direction, length, curl, len(radii))
    mesh = tube(scene, name, points, radii, [(i, i + 1) for i in range(len(points) - 1)], material_name)
    return mesh, points[-1], (points[-1] - points[-2]).normalized()


def curved_claw(scene, name, base, direction, length, radius, curl=0.45):
    """A claw tapering from radius at the base to a sharp tip, curling down."""
    count = 6
    points = _arc(base, direction, length, curl, count)
    radii = [radius * (1.0 - i / (count - 1)) ** 0.9 + 0.003 for i in range(count)]
    return tube(scene, name, points, radii, [(i, i + 1) for i in range(count - 1)], 'Claw')


def nail(scene, name, tip, direction, size):
    """A flat shiny nail on top of a fingertip, pointing along direction."""
    obj = ellipsoid(scene, name, Vector((0, 0, 0)), (size, size * 0.75, size * 0.35), 'Claw', subdivisions=2)
    align = Vector((1, 0, 0)).rotation_difference(direction.normalized()).to_matrix().to_4x4()
    obj.data.transform(Matrix.Translation(tip + direction.normalized() * size * 0.2 + Vector((0, 0, size * 0.35))) @ align)
    return obj


def rig(scene, name, bones):
    """
    An armature from (bone, head, tail, parent) tuples, head and tail as Blender
    vectors (use G). Parents connect when the child starts at the parent's tail.
    """
    data = bpy.data.armatures.new(name)
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    for bone_name, head, tail, parent in bones:
        bone = data.edit_bones.new(bone_name)
        bone.head = head
        bone.tail = tail
        if parent is not None:
            bone.parent = data.edit_bones[parent]
            bone.use_connect = (data.edit_bones[parent].tail - head).length < 1e-4
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj


def _segment_distance(point, head, tail):
    direction = tail - head
    share = max(0.0, min(1.0, (point - head).dot(direction) / max(direction.length_squared, 1e-9)))
    return (point - (head + direction * share)).length


def bind(armature, meshes, rigid=None):
    """
    Parents every mesh to the armature with an armature modifier. Weights:
    > Mesh listed in rigid: every vertex follows that one bone.
    > Otherwise: the two closest bone segments, weighted by 1 / distance^WEIGHT_POWER.
    """
    rigid = rigid or {}
    bones = list(armature.data.bones)
    for mesh_obj in meshes:
        groups = {bone.name: mesh_obj.vertex_groups.new(name=bone.name) for bone in bones}
        for vertex in mesh_obj.data.vertices:
            # Rigid piece: one bone
            if mesh_obj.name in rigid:
                groups[rigid[mesh_obj.name]].add([vertex.index], 1.0, 'REPLACE')
                continue
            # Soft: the two closest bones
            scored = sorted(
                ((1.0 / (_segment_distance(vertex.co, bone.head_local, bone.tail_local) + 1e-4) ** WEIGHT_POWER, bone.name) for bone in bones),
                reverse=True)[:2]
            total = sum(score for score, _ in scored)
            for score, bone_name in scored:
                groups[bone_name].add([vertex.index], score / total, 'REPLACE')
        mesh_obj.parent = armature
        mesh_obj.modifiers.new('Armature', 'ARMATURE').object = armature
    _join_by_material(meshes)


def _join_by_material(meshes):
    """One mesh per material: fewer draw calls in the game, vertex groups join by name."""
    groups = {}
    for mesh_obj in meshes:
        groups.setdefault(mesh_obj.data.materials[0].name, []).append(mesh_obj)
    for material_name, group in groups.items():
        target = group[0]
        if len(group) > 1:
            with bpy.context.temp_override(active_object=target, object=target, selected_objects=group, selected_editable_objects=group):
                bpy.ops.object.join()
        target.name = material_name


def hinge(armature, bone_name, axis, degrees):
    """Pose rotation of a bone about a Godot axis (AXIS_*), expressed in the bone's rest frame."""
    rest = armature.data.bones[bone_name].matrix_local.to_3x3()
    return Quaternion(rest.inverted() @ axis, math.radians(degrees))


def animate(armature, name, frames, curves):
    """
    One looping action: curves maps bone -> (axis, function of the loop share 0..1
    to degrees). Keyed every few frames, the last key repeats the first.
    """
    action = bpy.data.actions.new('CO_' + name)
    action.use_fake_user = False
    armature.animation_data_create()
    armature.animation_data.action = action
    step = max(1, frames // 12)
    for frame in range(0, frames + 1, step):
        share = (frame % frames) / frames
        for bone_name, (axis, curve) in curves.items():
            pose_bone = armature.pose.bones[bone_name]
            pose_bone.rotation_mode = 'QUATERNION'
            pose_bone.rotation_quaternion = hinge(armature, bone_name, axis, curve(share))
            pose_bone.keyframe_insert('rotation_quaternion', frame=frame)
    # The glTF animation takes the action name, the game asks for it without prefix
    action.name = name
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = frames


def export(out_dir, file_name):
    path = os.path.join(out_dir, file_name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    # Blender 5 merges the active actions into one glTF animation, named by this option (default "Animation")
    animation_names = [action.name for action in bpy.data.actions if not action.name.startswith('CO_')]
    merged_name = animation_names[0] if animation_names else 'Animation'
    bpy.ops.export_scene.gltf(
        export_nla_strips_merged_animation_name=merged_name,
        filepath=path,
        export_format='GLB',
        use_active_scene=True,
        export_yup=True,
        export_apply=False,
        export_animations=True,
        export_animation_mode='ACTIVE_ACTIONS',
        export_skins=True,
    )
    # Rename the action back so the next part's cleanup finds it
    for action in bpy.data.actions:
        if not action.name.startswith('CO_'):
            action.name = 'CO_' + action.name
    print(f'[build.creature-parts] Exported {path}')


def wave(share, phase=0.0):
    return math.sin((share + phase) * math.tau)


# ===========================================================================
# =============================   LIMB PARTS   ==============================
# ===========================================================================
#
#   build_foot    One of three feet alone: ankle at the origin, sole below, toes forward. No rig.
#   paw           Paw: round foot, four fat toes with pads and short nails.
#   column_foot   Column foot: thick column on a wide sole, flat nails around the front.
#   talon_foot    Talon foot: three knuckled toes forward, one back, every toe a curved talon.
#   build_hand    A grasping hand or a clawed paw alone: wrist at the origin, reaching forward.
#   hand          Four knuckled fingers with nails, a thumb towards the body, a palm pad.
#   claw_paw      A heavy paw with three big sickle claws.
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
    ankle = {'paw': G(0.03, -0.86, 0), 'columnfoot': G(0.02, -0.84, 0), 'talonfoot': G(-0.08, -0.78, 0)}[kind]
    builder = {'paw': paw, 'columnfoot': column_foot, 'talonfoot': talon_foot}[kind]
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
        meshes.append(_link_mesh(scene, 'Sail', mesh, 'Membrane'))
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

# ===========================================================================
# ==============================   FIN PARTS   ==============================
# ===========================================================================
#
#   fin               Rays, webbing with thickness and scalloped edge, fleshy root, a flutter rig.
#   build_caudal      Forked tail fin of fish: upright (XY), reaching back (-X).
#   build_fluke       Flukes of marine mammals: flat (XZ), two lobes reaching back.
#   build_dorsal      Back fin: upright, swept back, rising (+Y) from a base along X.
#   build_pectoral    Paddle of the flank: flat, reaching out (+Z) and back.
#
#   Every fin flutters in one second: each ray bends out of the fin plane, the wave
#   runs from the first ray to the last. The game rides it on the swim wave.
#

FIN_THICKNESS = 0.014
FIN_SCALLOP = 0.16                   # how far the edge dips between two rays, share of the ray length
FIN_ALONG = 8                        # webbing rows from root to edge
FIN_ACROSS = 4                       # webbing columns between two rays


def fin(scene, roots, tips, normal, lobe_center, lobe_scale, flutter_degrees):
    """
    A fin from rays (roots[i] -> tips[i], Godot axes via G). The webbing is a clean quad
    grid between every two neighbouring rays: rows from root to edge, columns across;
    the edge dips between the rays in a smooth arc (FIN_SCALLOP). It gets a thickness and
    smooth edges. Rays are thin tapering tubes ending just inside the edge, the root sits
    in a fleshy lobe (lobe_scale in Blender axes). normal is the fin plane normal (Blender
    axes): the rays bend about their in-plane perpendicular.
    """
    meshes = []

    # Webbing: one quad grid per gap between two rays, the edge dipping in an arc between them
    bm = bmesh.new()
    for i in range(len(roots) - 1):
        grid = []
        for row in range(FIN_ALONG + 1):
            along = row / FIN_ALONG
            line = []
            for column in range(FIN_ACROSS + 1):
                across = column / FIN_ACROSS
                root = roots[i].lerp(roots[i + 1], across)
                tip = tips[i].lerp(tips[i + 1], across)
                reach = 1.0 - FIN_SCALLOP * math.sin(across * math.pi)
                line.append(bm.verts.new(root.lerp(tip, along * reach)))
            grid.append(line)
        for row in range(FIN_ALONG):
            for column in range(FIN_ACROSS):
                bm.faces.new([grid[row][column], grid[row][column + 1], grid[row + 1][column + 1], grid[row + 1][column]])
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    mesh = bpy.data.meshes.new('Web')
    bm.to_mesh(mesh)
    bm.free()
    web = _link_mesh(scene, 'Web', mesh, 'Fin')
    solidify = web.modifiers.new('Thick', 'SOLIDIFY')
    solidify.thickness = FIN_THICKNESS
    solidify.offset = 0.0
    web.modifiers.new('Smooth', 'SUBSURF').levels = 1
    _apply_modifiers(web)
    for polygon in web.data.polygons:
        polygon.use_smooth = True
    meshes.append(web)

    # Rays: thin tubes from the root to just inside the edge, standing out a little on both sides
    for i, (root, tip) in enumerate(zip(roots, tips)):
        end = root.lerp(tip, 0.94)
        meshes.append(tube(scene, f'Ray{i}', [root, root.lerp(end, 0.5), end], (0.013, 0.008, 0.004), [(0, 1), (1, 2)], 'FinRay'))

    # Fleshy root the rays grow out of
    meshes.append(ellipsoid(scene, 'Root', lobe_center, lobe_scale, 'Skin', subdivisions=3))

    # One bone per ray, the flutter wave runs along them
    bones = [(f'Ray{i}', root, tip, None) for i, (root, tip) in enumerate(zip(roots, tips))]
    armature = rig(scene, 'Rig', bones)
    bind(armature, meshes)
    curves = {}
    for i, (root, tip) in enumerate(zip(roots, tips)):
        axis = (tip - root).cross(normal).normalized()
        curves[f'Ray{i}'] = (axis, lambda share, phase=-i * 0.1: flutter_degrees * wave(share, phase))
    animate(armature, 'Flutter', FPS, curves)


def build_caudal(scene):
    roots = [G(0.0, 0.07, 0), G(0.0, 0.03, 0), G(0.0, 0.0, 0), G(0.0, -0.03, 0), G(0.0, -0.07, 0)]
    tips = [G(-0.46, 0.56, 0), G(-0.38, 0.3, 0), G(-0.26, 0.0, 0), G(-0.38, -0.3, 0), G(-0.46, -0.56, 0)]
    fin(scene, roots, tips, AXIS_OUT, G(0.0, 0.0, 0), (0.09, 0.03, 0.1), 14.0)


def build_fluke(scene):
    roots = [G(0.0, 0, 0.07), G(0.0, 0, 0.03), G(0.0, 0, 0.0), G(0.0, 0, -0.03), G(0.0, 0, -0.07)]
    tips = [G(-0.44, 0, 0.58), G(-0.4, 0, 0.32), G(-0.22, 0, 0.0), G(-0.4, 0, -0.32), G(-0.44, 0, -0.58)]
    fin(scene, roots, tips, AXIS_UP, G(0.0, 0.0, 0), (0.09, 0.1, 0.035), 12.0)


def build_dorsal(scene):
    roots = [G(0.16, 0, 0), G(0.06, 0, 0), G(-0.05, 0, 0), G(-0.16, 0, 0)]
    tips = [G(0.04, 0.33, 0), G(-0.07, 0.4, 0), G(-0.18, 0.4, 0), G(-0.26, 0.16, 0)]
    fin(scene, roots, tips, AXIS_OUT, G(0.0, 0.0, 0), (0.2, 0.035, 0.05), 8.0)


def build_pectoral(scene):
    roots = [G(0.1, 0, 0.0), G(0.03, 0, 0.0), G(-0.04, 0, 0.0), G(-0.1, 0, 0.0)]
    tips = [G(0.06, 0, 0.26), G(-0.06, 0, 0.36), G(-0.16, 0, 0.32), G(-0.2, 0, 0.16)]
    fin(scene, roots, tips, AXIS_UP, G(0.0, 0.0, 0.0), (0.12, 0.05, 0.035), 18.0)


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


# ===========================================================================
# ==============================   BUILD ALL   ==============================
# ===========================================================================
#
#   1. Output   Resolves the models folder from the arguments after "--".
#   2. Parts    Builds every part in a cleared scene and exports it.
#

# ---------------------------------------------------------------------------
# Output
#   Resolves the output folder from the arguments after "--".
# ---------------------------------------------------------------------------

def output_dir():
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    if not args:
        raise ValueError('The models folder must follow "--", e.g. blender -b -P build.models.py -- assets/models')
    os.makedirs(args[0], exist_ok=True)
    return os.path.abspath(args[0])


# ---------------------------------------------------------------------------
# Parts
#   Builds every part in a cleared scene and exports it.
# ---------------------------------------------------------------------------

PARTS = {
    'creature/creature.paw.glb':           lambda scene: build_foot(scene, 'paw'),
    'creature/creature.columnfoot.glb':    lambda scene: build_foot(scene, 'columnfoot'),
    'creature/creature.talonfoot.glb':     lambda scene: build_foot(scene, 'talonfoot'),
    'creature/creature.hand.glb':          lambda scene: build_hand(scene, 'hand'),
    'creature/creature.clawpaw.glb':       lambda scene: build_hand(scene, 'clawpaw'),
    'creature/creature.membranewings.glb': lambda scene: build_wing(scene, 'membranewings'),
    'creature/creature.featherwings.glb':  lambda scene: build_wing(scene, 'featherwings'),
    'creature/creature.brain.glb':         build_brain,
    'creature/creature.stalkeye.glb':      build_stalk_eye,
    'creature/creature.caudalfin.glb':     build_caudal,
    'creature/creature.fluke.glb':         build_fluke,
    'creature/creature.dorsalfin.glb':     build_dorsal,
    'creature/creature.pectoralfin.glb':   build_pectoral,
    'cell/cell.flagellum.glb':             build_flagellum,
    'cell/cell.cilium.glb':                build_cilium,
    'cell/cell.jaw.glb':                   build_cell_jaw,
    'cell/cell.spike.glb':                 build_spike,
    'cell/cell.armorplate.glb':            build_armor,
    'cell/cell.filtermouth.glb':           build_filter,
    'cell/cell.eyespot.glb':               build_eyespot,
    'cell/cell.chloroplast.glb':           build_chloroplast,
    'cell/cell.vacuole.glb':               build_vacuole,
    'cell/cell.toxin.glb':                 build_toxin,
}


def build_all(out_dir, only=None):
    for file_name, build in PARTS.items():
        if only is not None and file_name not in only:
            continue
        scene = build_scene()
        build(scene)
        # Static parts (no rig) join per material too, rigged ones did in bind
        if not any('ARMATURE' == obj.type for obj in scene.objects):
            _join_by_material([obj for obj in scene.objects if 'MESH' == obj.type])
        export(out_dir, file_name)


if __name__ == '__main__':
    build_all(output_dir())
