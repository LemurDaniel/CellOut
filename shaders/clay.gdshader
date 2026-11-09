// NOTE:
//   Modelling clay for the organism stage: the kneaded blob of the evolve cutscene and
//   the body of every creature. Fresh clay (knead 1) is lumpy with fingerprint grooves
//   and matt, a living creature (knead low) is smoother, darker on the back and lighter
//   on the belly (countershading). The swim wave bends the body in model space, the
//   same formula as CreatureVisual3D.Bend, so the parts stay attached.
//   Set by World/Cutscenes/EvolveCutscene.cs and Cells/CreatureVisual3D.cs.
//   The body lives in clay.gdshaderinc, clay_translucent.gdshader is the see-through variant.

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley;

#include "res://shaders/clay.gdshaderinc"
