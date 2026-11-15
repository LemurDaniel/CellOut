# NOTE:
#   Builds one model of the generator and renders it from three sides, so a builder can
#   be checked by eye before the game knows the part: <name>.side.png (the +X of Godot,
#   forward, points right), <name>.front.png (three quarters from the front) and
#   <name>.top.png. Colors are the placeholder colors of helpers.MATERIAL_COLORS, the
#   game recolors skin and fins with the body color.
#   Requires: Blender 5.2 or newer. Usually run through Show-Model.ps1.
#
#   blender -b --factory-startup -P tools/blender/preview.model.py -- <out folder> <name> <module> "<call>"
#   blender -b --factory-startup -P tools/blender/preview.model.py -- C:/Temp/prev jaw mouths "build_jaw(scene)"

#
#   Chapters:
#     PREVIEW MODEL   Builds the model, frames it and renders three views.
#

import importlib
import math
import os
import sys

import bpy
from mathutils import Vector

GENERATOR_DIR = os.path.dirname(os.path.abspath(__file__))
if GENERATOR_DIR not in sys.path:
    sys.path.insert(0, GENERATOR_DIR)

import models.helpers
from models.helpers import build_scene

RESOLUTION = (640, 480)
MARGIN = 1.35                         # air around the model, in shares of its size

# View name -> direction the camera looks from (Blender axes: x forward, -y out of the right flank, z up)
VIEWS = {
    'side':  Vector((0.0, -1.0, 0.12)),
    'front': Vector((0.75, -0.8, 0.45)),
    'top':   Vector((0.0, -0.001, 1.0)),
}


# ===========================================================================
# ===========================   PREVIEW MODEL   =============================
# ===========================================================================
#
#   1. Arguments   Out folder, file name, module and the call of the builder.
#   2. Build       Runs the builder in a cleared scene.
#   3. Render      Frames the model and renders every view with the workbench engine.
#

# ---------------------------------------------------------------------------
# Arguments
#   Out folder, file name, module and the call of the builder.
# ---------------------------------------------------------------------------

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
if 4 != len(args):
    raise ValueError('Four arguments must follow "--": <out folder> <name> <module> "<call>", e.g. C:/Temp/prev jaw mouths "build_jaw(scene)"')
out_dir, name, module_name, call = args
os.makedirs(out_dir, exist_ok=True)

# ---------------------------------------------------------------------------
# Build
#   Runs the builder in a cleared scene.
# ---------------------------------------------------------------------------

module = importlib.import_module(f'models.{module_name}')
scene = build_scene()
eval(call, {**vars(module), 'scene': scene})

# ---------------------------------------------------------------------------
# Render
#   Frames the model and renders every view with the workbench engine.
# ---------------------------------------------------------------------------

meshes = [obj for obj in scene.objects if 'MESH' == obj.type]
if not meshes:
    raise ValueError(f'The call "{call}" must build at least one mesh, the scene is empty.')
bpy.context.view_layer.update()
corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
low = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
high = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
center = (low + high) / 2
size = max((high - low).length, 0.01)

camera_data = bpy.data.cameras.new('PreviewCamera')
camera_data.type = 'ORTHO'
camera_data.ortho_scale = size * MARGIN
camera = bpy.data.objects.new('PreviewCamera', camera_data)
scene.collection.objects.link(camera)
scene.camera = camera

scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_cavity = True
scene.render.resolution_x, scene.render.resolution_y = RESOLUTION
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new('PreviewWorld')
scene.world.color = (0.13, 0.17, 0.23)
scene.render.image_settings.file_format = 'PNG'

for view, direction in VIEWS.items():
    camera.location = center + direction.normalized() * size * 4.0
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Z' if 'top' != view else 'X').to_euler()
    scene.render.filepath = os.path.join(out_dir, f'{name}.{view}.png')
    bpy.ops.render.render(write_still=True)
    print(f'[preview.model] Rendered {scene.render.filepath}')
print(f'[preview.model] Bounds {tuple(round(v, 3) for v in low)} .. {tuple(round(v, 3) for v in high)} (Blender axes)')
