// NOTE:
//   Primeval trees of the islands (assets/models/tree/), drawn as multimeshes with one
//   tint per instance (COLOR). One material per Blender material name (Bark, Leaf, Frond, ...).
//   The model is about 1 unit high: the whole tree leans in the wind with the square of the
//   height, leaves and fronds also shiver. Two sided (fronds are single blades).
//   Bends with the planet. Set by Core/TreeModels.cs.

shader_type spatial;
render_mode cull_disabled;

#include "res://shaders/planet.gdshaderinc"

uniform vec3 albedo : source_color = vec3(0.4, 0.3, 0.2);
uniform float roughness = 0.85;
uniform float sway = 0.03;           // lean of the top in model units
uniform float flutter = 0.0;         // shiver of leaves, 0 for bark

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	float phase = origin.x * 0.011 + origin.z * 0.013;
	float height = max(VERTEX.y, 0.0);
	float bend = height * height;
	VERTEX.x += sin(TIME * 0.9 + phase) * sway * bend;
	VERTEX.z += sin(TIME * 0.7 + phase * 1.3) * sway * 0.6 * bend;
	VERTEX.y += flutter * sin(TIME * 5.0 + dot(VERTEX, vec3(23.0, 17.0, 29.0)) + phase) * 0.008 * height;
	VERTEX = planet_bend(VERTEX, MODEL_MATRIX);
}

void fragment() {
	ALBEDO = albedo * COLOR.rgb;
	ROUGHNESS = roughness;
	SPECULAR = 0.2;
}
