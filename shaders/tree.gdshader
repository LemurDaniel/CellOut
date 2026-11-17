// NOTE:
//   Primeval trees of the islands (assets/models/tree/), drawn as multimeshes with one
//   tint per instance (COLOR). One material per Blender material name (Bark, Leaf, Frond, ...).
//   Also the undergrowth (Core/PlantModels.cs): petals take the bloom color of their instance.
//   The model is about 1 unit high: the whole tree leans in the wind with the square of the
//   height, leaves and fronds also shiver. Two sided (fronds are single blades).
//   The look comes from the shader, so it holds on every graphics tier:
//     - every tree is darker at its foot and lighter towards the top
//     - foliage (flutter > 0) is mottled in light and dark green with sunlit yellowish
//       spots, the sun shines through it (backlight) and its edges catch a rim of light
//     - bark carries long vertical streaks, moss creeps up the foot of the trunk
//     - the shadows of the clouds drift over it (planet.gdshaderinc)
//   Bends with the planet. Set by Core/TreeModels.cs.

shader_type spatial;
render_mode cull_disabled;

#include "res://shaders/planet.gdshaderinc"

uniform vec3 albedo : source_color = vec3(0.4, 0.3, 0.2);
uniform float roughness = 0.85;
uniform float sway = 0.03;           // lean of the top in model units
uniform float flutter = 0.0;         // shiver of leaves, 0 for bark
uniform bool bloom = false;          // petals: the color of the instance comes from INSTANCE_CUSTOM (sRGB)

varying vec3 world_pos;
varying vec3 model_pos;
varying vec3 bloom_tint;

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	float phase = origin.x * 0.011 + origin.z * 0.013;
	float height = max(VERTEX.y, 0.0);
	float bend = height * height;
	model_pos = VERTEX;
	bloom_tint = bloom ? pow(INSTANCE_CUSTOM.rgb, vec3(2.2)) : vec3(1.0);
	VERTEX.x += sin(TIME * 0.9 + phase) * sway * bend;
	VERTEX.z += sin(TIME * 0.7 + phase * 1.3) * sway * 0.6 * bend;
	VERTEX.y += flutter * sin(TIME * 5.0 + dot(VERTEX, vec3(23.0, 17.0, 29.0)) + phase) * 0.008 * height;
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX = planet_bend(VERTEX, MODEL_MATRIX);
}

void fragment() {
	vec3 col = albedo * bloom_tint * COLOR.rgb;
	float foliage = step(0.5, flutter);

	// Foot to top: dark where the tree stands, light where the sun reaches it
	col *= mix(0.68, 1.0, smoothstep(0.0, 0.9, model_pos.y));

	// Foliage: mottled, with sunlit spots. Bark: streaks along the trunk
	vec3 across = world_pos / max(sky_unit, 0.001);
	float mottle = planet_noise(across.xz * 2.6 + across.y * 1.7);
	vec3 leaf = col * (0.8 + 0.4 * mottle);
	leaf = mix(leaf, leaf * vec3(1.2, 1.14, 0.8), smoothstep(0.55, 0.9, mottle) * 0.3);
	float streak = planet_noise(vec2((model_pos.x - model_pos.z) * 46.0, model_pos.y * 5.0));
	vec3 bark = col * (0.78 + 0.4 * streak);
	// Moss: green patches creep up the foot of the trunk
	float moss = smoothstep(0.35, 0.75, planet_noise(across.xz * 9.0 + across.y * 5.0 + 9.0)) * (1.0 - smoothstep(0.02, 0.16, model_pos.y));
	bark = mix(bark, vec3(0.16, 0.27, 0.08) * (0.8 + 0.4 * mottle), moss * 0.45);
	col = mix(bark, leaf, foliage);

	col = bloom ? col : planet_green(col);   // the green of the planet (moss too), petals keep their bloom
	ALBEDO = col * cloud_shadow(world_pos.xz, TIME);
	ROUGHNESS = roughness;
	SPECULAR = 0.2;
	BACKLIGHT = col * 0.22 * foliage;
	RIM = mix(0.12, 0.2, foliage);
	RIM_TINT = 0.6;
}
