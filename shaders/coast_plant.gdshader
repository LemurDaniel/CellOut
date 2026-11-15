// NOTE:
//   Props of the coast (grass, seaweed, flowers, rocks, shells, driftwood), drawn as
//   multimeshes with one color per instance. Blades (cone meshes, height 0.6 around 0)
//   sway at their tip; the sway is set in world units per blade height, so thin blades
//   bend as far as thick ones. Rocks and wood use sway 0. Every prop is darker at its
//   foot and lighter at its top, a little mottled, with a rim of light, and the shadows
//   of the clouds drift over it (planet.gdshaderinc). Bends with the planet.
//   Set by World/Coast.cs.

shader_type spatial;
render_mode cull_disabled;

#include "res://shaders/planet.gdshaderinc"

uniform float sway = 0.0;            // tip offset per blade height
uniform float sway_speed = 1.6;

varying vec3 world_pos;
varying float tip;                   // 0 at the foot .. 1 at the top of the prop

void vertex() {
	float scale_x = length(MODEL_MATRIX[0].xyz);
	float scale_y = length(MODEL_MATRIX[1].xyz);
	vec3 origin = MODEL_MATRIX[3].xyz;
	tip = clamp((VERTEX.y + 0.3) / 0.6, 0.0, 1.0);
	float phase = TIME * sway_speed + origin.x * 0.013 + origin.z * 0.017;
	VERTEX.x += sin(phase) * sway * 0.6 * scale_y * tip * tip / max(scale_x, 0.0001);
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX = planet_bend(VERTEX, MODEL_MATRIX);
}

void fragment() {
	// Dark at the foot where it sits in the grass, light at the top, a little mottled,
	// below the drifting shadows of the clouds
	vec3 across = world_pos / max(sky_unit, 0.001);
	float mottle = planet_noise(across.xz * 5.0 + across.y * 3.0);
	// Instance colors arrive as sRGB values, unconverted they look washed out
	vec3 tint = pow(COLOR.rgb, vec3(2.2));
	vec3 col = tint * mix(0.7, 1.25, tip) * (0.88 + 0.24 * mottle);
	ALBEDO = col * cloud_shadow(world_pos.xz, TIME);
	ROUGHNESS = 0.85;
	SPECULAR = 0.2;
	RIM = 0.25;
	RIM_TINT = 0.7;
}
