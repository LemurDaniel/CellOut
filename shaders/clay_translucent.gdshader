// NOTE:
//   See-through variant of clay.gdshader for a body color with alpha below 1
//   (EditorStyle.TRANSLUCENT_COLOR): looks like a cell membrane, not like glass.
//   Alpha blended, so it casts no shadow. Picked by Cells/CreatureVisual3D.cs.

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley;

#define CLAY_TRANSLUCENT
#include "res://shaders/clay.gdshaderinc"
