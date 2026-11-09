// NOTE:
//   Props of the coast (grass, seaweed, flowers, rocks, shells, driftwood), drawn as
//   multimeshes with one color per instance. Blades (cone meshes, height 0.6 around 0)
//   sway at their tip; the sway is set in world units per blade height, so thin blades
//   bend as far as thick ones. Rocks and wood use sway 0. Set by World/Coast.cs.

shader_type spatial;
render_mode cull_disabled;

uniform float sway = 0.0;            // tip offset per blade height
uniform float sway_speed = 1.6;

void vertex() {
	float scale_x = length(MODEL_MATRIX[0].xyz);
	float scale_y = length(MODEL_MATRIX[1].xyz);
	vec3 origin = MODEL_MATRIX[3].xyz;
	float tip = clamp((VERTEX.y + 0.3) / 0.6, 0.0, 1.0);
	float phase = TIME * sway_speed + origin.x * 0.013 + origin.z * 0.017;
	VERTEX.x += sin(phase) * sway * 0.6 * scale_y * tip * tip / max(scale_x, 0.0001);
}

void fragment() {
	ALBEDO = COLOR.rgb;
	ROUGHNESS = 0.85;
	SPECULAR = 0.2;
}
