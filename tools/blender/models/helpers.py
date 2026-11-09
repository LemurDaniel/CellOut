# NOTE:
#   Shared building blocks of the model generator: Godot axes, the scene, named
#   materials, tubes, cones, ellipsoids, digits, claws, rigs, skin weights, looping keys
#   and the glTF export. Every part module imports what it needs from here.
#   Requires: Blender 5.2 (bpy, bmesh, mathutils).
#
#   from models.helpers import G, tube, ellipsoid

#
#   Chapters:
#     HELPERS   Coordinates, scene, materials, tubes, cones, spheres, rigs, weights, keys, export.
#

import math
import os

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
    # Trees, recolored by the game (Core/TreeModels.cs)
    'Bark':      (0.4, 0.3, 0.22, 1.0),
    'Leaf':      (0.27, 0.5, 0.2, 1.0),
    'LeafDark':  (0.18, 0.38, 0.15, 1.0),
    'Frond':     (0.33, 0.58, 0.22, 1.0),
    'Horsetail': (0.45, 0.55, 0.3, 1.0),
    'Needle':    (0.3, 0.5, 0.22, 1.0),
    'Cone':      (0.85, 0.5, 0.2, 1.0),
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
    mat.use_backface_culling = name not in ('Membrane', 'Feather', 'Fin', 'Frond')
    # The glTF exporter reads the principled node, not the viewport color
    mat.use_nodes = True
    shader = next(node for node in mat.node_tree.nodes if node.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = MATERIAL_COLORS[name]
    shader.inputs['Roughness'].default_value = 0.35 if name in ('Claw', 'EyeWhite', 'Pupil') else 0.65
    return mat


def link_mesh(scene, name, mesh, material_name):
    mesh.materials.append(material(material_name))
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    return obj


def apply_modifiers(obj):
    """Bakes the modifier stack into the mesh without operators (works in the background)."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    baked = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph))
    obj.modifiers.clear()
    obj.data = baked


def tube(scene, name, points, radii, edges, material_name='Skin', subdivisions=TUBE_SUBDIVISIONS, max_pieces=10):
    """
    A smooth tube through a tree of points (skin modifier + subdivision). Point 0 is
    the root, edges connect point indices, radii are per point. Every edge is cut into
    short pieces first (a coarse skin hull shrinks under subdivision, short tubes the
    most), every open end but the root gets a round cap.
    Low poly (trees, hundreds at once): subdivisions=1 and few max_pieces per edge.
    """
    points = [Vector(point) for point in points]
    radii = [radius * TUBE_SHRINK_FIX for radius in radii]
    fine_edges = []
    for start, end in edges:
        pieces = max(1, min(max_pieces, math.ceil((points[end] - points[start]).length / (TUBE_STEP * min(radii[start], radii[end])))))
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
    obj = link_mesh(scene, name, mesh, material_name)
    obj.modifiers.new('Skin', 'SKIN')
    for vertex, radius in zip(mesh.skin_vertices[0].data, radii):
        vertex.radius = (radius, radius)
    mesh.skin_vertices[0].data[0].use_root = True
    obj.modifiers.new('Smooth', 'SUBSURF').levels = subdivisions
    apply_modifiers(obj)
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
    return link_mesh(scene, name, mesh, material_name)


def ellipsoid(scene, name, center, scale, material_name, subdivisions=3, texture=None, strength=0.0):
    """An icosphere scaled per Blender axis, optionally displaced by a texture."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=1.0)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Diagonal((*scale, 1.0)), verts=bm.verts)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = link_mesh(scene, name, mesh, material_name)
    if texture is not None:
        displace = obj.modifiers.new('Displace', 'DISPLACE')
        displace.texture = texture
        displace.strength = strength
        displace.texture_coords = 'OBJECT'
        apply_modifiers(obj)
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
    join_by_material(meshes)


def join_by_material(meshes):
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
