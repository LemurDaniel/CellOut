// NOTE:
//   Painted pattern on the main surface of a building block (material Paint of the models
//   in assets/models/building/): the base paint with a pattern in a second tone, picked
//   in the village builder. The pattern is laid out in the space of the block itself: v
//   runs up the block, u runs around its vertical axis, so bands close around a round
//   wall and a roof without a seam. Cheap on purpose (no textures, no screen reads), it
//   looks the same on every graphics tier. Created by Tribe/Building/BuildingVisual3D.cs.
//
//   pattern: 1 bands, 2 stripes, 3 zigzag, 4 dots, 5 checks (0 is the plain material, no shader).

shader_type spatial;

uniform vec3 base_color : source_color = vec3(0.72, 0.55, 0.38);
uniform vec3 pattern_color : source_color = vec3(0.9, 0.88, 0.8);
uniform int pattern = 1;

const float AROUND = 12.0;      // pattern cells once around the block
const float UPWARD = 4.0;       // pattern cells per model unit of height

varying vec3 block_position;

void vertex() {
	block_position = VERTEX;
}

void fragment() {
	float u = atan(block_position.z, block_position.x) / TAU * AROUND;
	float v = block_position.y * UPWARD;
	float mask = 0.0;
	if (pattern == 1) {
		mask = step(0.62, fract(v));
	} else if (pattern == 2) {
		mask = step(0.5, fract(u));
	} else if (pattern == 3) {
		mask = step(0.6, fract(v + abs(fract(u) - 0.5) * 1.2));
	} else if (pattern == 4) {
		mask = 1.0 - step(0.28, distance(fract(vec2(u, v * 1.5)), vec2(0.5)));
	} else if (pattern == 5) {
		mask = mod(floor(u) + floor(v * 1.5), 2.0);
	}
	ALBEDO = mix(base_color, pattern_color, mask);
	ROUGHNESS = 0.9;
}
