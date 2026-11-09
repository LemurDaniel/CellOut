// NOTE:
//   Ground of the coast, from the deep up to the meadow. Patterns run in coast
//   coordinates (d = distance onto the land, along = along the shore, both / unit):
//     - meadow:  two greens in patches, darker clumps, rare round flower specks
//     - dunes:   an irregular edge between sand and grass
//     - beach:   dry sand with ripples and grain, wet sand behind the swash, a sheen
//     - water:   teal sand with dancing caustics, sinking into the sea floor color
//   With depth (height in player sizes) the ground turns into exactly the sea floor of
//   ocean.gdshader, so the coast has no edge. Set by World/Coast.cs.

shader_type spatial;
render_mode cull_disabled;

#include "res://shaders/ocean_floor.gdshaderinc"

uniform vec2 coast_origin = vec2(0.0);
uniform vec2 coast_normal = vec2(1.0, 0.0);
uniform float unit = 14.0;
uniform float world_scale = 100.0;           // the same as the Backdrop floor
uniform vec3 shallow_color : source_color = vec3(0.10, 0.32, 0.34);
uniform vec3 deep_color : source_color = vec3(0.03, 0.11, 0.15);
uniform float caustic_strength = 0.12;

varying vec3 world_pos;
varying float height;                        // in player sizes, the mesh is scaled by them

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	height = VERTEX.y;
}

void fragment() {
	vec2 rel = world_pos.xz - coast_origin;
	float d = dot(rel, coast_normal);
	float along = dot(rel, vec2(-coast_normal.y, coast_normal.x));
	vec2 q = vec2(d, along) / unit;
	float shore = coast_shoreline(along, unit, TIME);
	float grain = coast_fbm(q * 3.0);

	// Sand: dry with ripples parallel to the shore, wet a little darker
	float ripples = sin(q.x * 9.0 + coast_fbm(q * 1.5) * 6.0) * 0.5 + 0.5;
	vec3 dry = vec3(0.6, 0.53, 0.39) * (0.9 + 0.18 * grain) * (0.95 + 0.07 * ripples);
	vec3 wet = vec3(0.4, 0.35, 0.27) * (0.9 + 0.18 * grain);

	// Meadow: patches of two greens, clumps and rare flowers
	float patches = coast_fbm(q * 0.6 + 7.0);
	vec3 grass = mix(vec3(0.13, 0.24, 0.11), vec3(0.24, 0.35, 0.15), patches);
	grass *= 0.8 + 0.3 * coast_fbm(q * 4.0);
	// Flowers: a round speck in a few random cells
	vec2 flowerCell = floor(q * 10.0);
	vec2 flowerSpot = fract(q * 10.0) - 0.5;
	float flower = step(0.99, coast_hash(flowerCell)) * (1.0 - smoothstep(0.12, 0.2, length(flowerSpot)));
	vec3 bloom = mix(vec3(0.95, 0.85, 0.3), vec3(0.92, 0.5, 0.7), coast_hash(flowerCell + 3.1));
	grass = mix(grass, bloom, flower * 0.85);

	// Bands: grass behind an irregular dune edge, wet sand behind the swash
	float grassEdge = (3.5 + (coast_fbm(vec2(q.y * 0.3, 1.3)) - 0.5) * 3.0) * unit;
	vec3 col = mix(dry, grass, smoothstep(grassEdge - 0.6 * unit, grassEdge + 0.6 * unit, d));
	float wetEdge = shore + 0.9 * unit + sin(TIME * 0.7 + q.y * 0.11 - 0.6) * 0.3 * unit;
	float wetness = 1.0 - smoothstep(wetEdge - 0.3 * unit, wetEdge + 0.3 * unit, d);
	col = mix(col, wet, wetness);

	// Under water: teal and darker, then the sea floor takes over with depth
	float under = 1.0 - smoothstep(shore - 0.15 * unit, shore + 0.15 * unit, d);
	col *= mix(vec3(1.0), vec3(0.55, 0.78, 0.8), under);
	float caustic;
	vec3 ocean = ocean_floor_color(world_pos.xz / world_scale, TIME, deep_color, shallow_color, caustic);
	float depth = 1.0 - smoothstep(-2.7, -0.7, height);
	col = mix(col, ocean, depth);

	// Caustics: the floor ones in the deep, livelier ones on the shallow sand
	float dance = sin(q.x * 7.0 + TIME * 1.3 + sin(q.y * 5.0)) * sin(q.y * 6.0 - TIME + sin(q.x * 4.0));
	EMISSION = vec3(0.4, 0.9, 0.8) * (caustic * caustic_strength * depth + max(dance, 0.0) * 0.09 * under * (1.0 - depth));
	ALBEDO = col;
	ROUGHNESS = mix(mix(1.0, 0.4, wetness * (1.0 - under)), 1.0, depth);
	SPECULAR = mix(0.4 * wetness * (1.0 - under), 0.0, depth);
}
