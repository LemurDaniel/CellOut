// NOTE:
//   Ground of the islands, from the deep up to the meadow on the hill. Patterns run in
//   world coordinates / unit, the bands follow d = island_depth (distance into the land):
//     - meadow:  lush and light green in patches, sunny yellowish stretches, fine blades,
//                clover cushions, flower specks, tufts as relief, a sheen at flat angles,
//                gusts of wind running over it as lighter waves
//     - dunes:   an irregular edge between sand and grass that frays into tufts
//     - yards:   trampled earth with pebbles on the flat foundations of buildings
//     - clouds:  their shadows drift over the land (planet.gdshaderinc)
//     - beach:   dry sand with ripples along the shore, wet sand behind the swash, a sheen
//     - water:   teal sand with dancing caustics, sinking into the sea floor color
//   With depth (height in player sizes) the ground turns into exactly the sea floor of
//   ocean.gdshader, so the islands have no edge. Bends with the planet (planet.gdshaderinc).
//   Set by World/Coast.cs.

shader_type spatial;
render_mode cull_disabled;

#include "res://shaders/ocean_floor.gdshaderinc"
#include "res://shaders/planet.gdshaderinc"

uniform float unit = 14.0;
uniform float world_scale = 100.0;           // the same as the Backdrop floor
uniform vec3 shallow_color : source_color = vec3(0.10, 0.32, 0.34);
uniform vec3 deep_color : source_color = vec3(0.03, 0.11, 0.15);
uniform float caustic_strength = 0.12;

// Flat foundations of buildings (World/CoastTerrain.cs, MAX_PADS): xy center, z radius
const int MAX_PADS = 48;
uniform vec4 pads[MAX_PADS];
uniform int pad_count = 0;

varying vec3 world_pos;
varying float height;                        // in player sizes, the mesh is scaled by them

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	height = VERTEX.y;
	VERTEX = planet_bend(VERTEX, MODEL_MATRIX);
}

// Height of the small relief at pattern point q: tufts of grass on the meadow (1), flat
// ripples on the sand (0)
float ground_relief(vec2 q, float meadow) {
	float tufts = coast_noise(q * 7.0) * 0.65 + coast_noise(q * 17.0 + 2.0) * 0.35;
	float sand = coast_noise(q * vec2(4.0, 9.0) + 5.0) * 0.2;
	return mix(sand, tufts, meadow);
}

void fragment() {
	float d = island_depth(world_pos.xz);
	vec2 q = world_pos.xz / unit;
	float shore = coast_shoreline(world_pos.xz, unit, TIME);
	float grain = coast_fbm(q * 3.0);

	// Detail fades with the distance, else the fine patterns flicker far away
	float near = 1.0 - smoothstep(18.0 * unit, 70.0 * unit, length(VERTEX));

	// Sand: dry with ripples along the shore and darker grains, wet a little darker
	float ripples = sin(d / unit * 9.0 + coast_fbm(q * 1.5) * 6.0) * 0.5 + 0.5;
	float grains = coast_noise(q * 34.0);
	vec3 dry = vec3(0.66, 0.56, 0.38) * (0.9 + 0.18 * grain) * (0.94 + 0.08 * ripples) * (1.0 - 0.1 * near * smoothstep(0.55, 0.9, grains));
	vec3 wet = vec3(0.4, 0.35, 0.27) * (0.9 + 0.18 * grain);
	dry *= planet_sand.rgb;
	wet *= planet_sand.rgb;

	// Meadow: lush and light green in patches, wide sunny stretches of yellowish grass,
	// fine blades close up, a bit lighter up the hill
	float patches = coast_fbm(q * 0.6 + 7.0);
	float sunny = smoothstep(0.42, 0.7, coast_fbm(q * 0.13 + 21.0));
	vec3 grass = mix(vec3(0.06, 0.19, 0.07), vec3(0.15, 0.3, 0.09), patches);
	grass = mix(grass, vec3(0.27, 0.32, 0.1), sunny * (0.25 + 0.3 * patches));
	float blades = coast_noise(q * 23.0) * 0.6 + coast_noise(q * 47.0 + 3.0) * 0.4;
	grass *= (0.8 + 0.3 * coast_fbm(q * 4.0)) * (1.0 + 0.55 * near * (blades - 0.5)) * (1.0 + 0.12 * clamp(height, 0.0, 1.5));
	// Wind: gusts run over the meadow as wide lighter waves, only under the open sky
	float gust = coast_noise(q * vec2(0.22, 0.5) + TIME * vec2(0.42, 0.15));
	grass *= 1.0 + 0.2 * (smoothstep(0.45, 0.85, gust) - 0.25) * sky_open;
	// Clover: dark round cushions here and there
	vec2 cloverCell = floor(q * 2.2);
	vec2 cloverSpot = fract(q * 2.2) - 0.5 - (vec2(coast_hash(cloverCell + 5.0), coast_hash(cloverCell + 9.0)) - 0.5) * 0.5;
	float clover = step(0.72, coast_hash(cloverCell)) * (1.0 - smoothstep(0.1, 0.26, length(cloverSpot) + 0.08 * coast_noise(q * 9.0)));
	grass = mix(grass, vec3(0.07, 0.2, 0.08), clover * 0.55);
	// Flowers: a round speck in a few random cells
	vec2 flowerCell = floor(q * 10.0);
	vec2 flowerSpot = fract(q * 10.0) - 0.5;
	float flower = step(0.985, coast_hash(flowerCell)) * (1.0 - smoothstep(0.12, 0.2, length(flowerSpot)));
	vec3 bloom = mix(vec3(0.98, 0.86, 0.3), vec3(0.95, 0.5, 0.72), coast_hash(flowerCell + 3.1));
	bloom = mix(bloom, vec3(0.95, 0.96, 1.0), step(0.66, coast_hash(flowerCell + 6.3)));
	grass = planet_green(grass);           // the green of the planet (planet.gdshaderinc), flowers keep their colors
	grass = mix(grass, bloom, flower * 0.9);

	// Bands: grass behind an irregular dune edge that frays into tufts, wet sand behind the swash
	float grassEdge = (3.5 + (coast_fbm(q * 0.3 + 1.3) - 0.5) * 3.0) * unit;
	float frayed = d + (coast_noise(q * 2.6) - 0.5) * 1.5 * unit;
	float meadow = smoothstep(grassEdge - 0.25 * unit, grassEdge + 0.25 * unit, frayed);
	vec3 col = mix(dry, grass, meadow);

	// Yards: trampled earth on the foundations of buildings, frayed into the grass at the
	// edge, worn darker in the middle, pebbles and a few blades left standing
	float yard = 0.0;
	for (int i = 0; i < pad_count; i++) {
		float reach = length(world_pos.xz - pads[i].xy) + (coast_noise(q * 1.8 + float(i) * 3.1) - 0.5) * 1.6 * unit;
		yard = max(yard, 1.0 - smoothstep(pads[i].z * 0.8, pads[i].z + 0.6 * unit, reach));
	}
	vec3 earth = mix(vec3(0.3, 0.21, 0.13), vec3(0.42, 0.32, 0.2), coast_fbm(q * 1.2 + 3.0)) * (0.9 + 0.2 * grain);
	vec2 pebbleCell = floor(q * 6.0);
	float pebble = step(0.86, coast_hash(pebbleCell + 11.0)) * (1.0 - smoothstep(0.12, 0.22, length(fract(q * 6.0) - 0.5)));
	earth = mix(earth, vec3(0.55, 0.52, 0.47) * (0.85 + 0.3 * coast_hash(pebbleCell)), pebble * 0.8 * near);
	float stubble = smoothstep(0.62, 0.8, coast_noise(q * 13.0)) * (1.0 - yard * yard);
	col = mix(col, mix(earth, grass, stubble), yard * meadow);
	float wetEdge = shore + 0.9 * unit + sin(TIME * 0.7 + (q.x + q.y) * 0.11 - 0.6) * 0.3 * unit;
	float wetness = 1.0 - smoothstep(wetEdge - 0.3 * unit, wetEdge + 0.3 * unit, d);
	col = mix(col, wet, wetness);

	// Under water: teal and darker, then the sea floor takes over with depth
	float under = 1.0 - smoothstep(shore - 0.15 * unit, shore + 0.15 * unit, d);
	col *= mix(vec3(1.0), vec3(0.55, 0.78, 0.8), under);
	float caustic;
	vec3 ocean = ocean_floor_color(world_pos.xz / world_scale, TIME, deep_color, shallow_color, caustic);
	ocean *= planet_water.rgb;
	float depth = 1.0 - smoothstep(-2.7, -0.7, height);
	col = mix(col, ocean, depth);

	// Caustics: the floor ones in the deep, livelier ones on the shallow sand
	float dance = sin(q.x * 7.0 + TIME * 1.3 + sin(q.y * 5.0)) * sin(q.y * 6.0 - TIME + sin(q.x * 4.0));
	EMISSION = vec3(0.4, 0.9, 0.8) * (caustic * caustic_strength * depth + max(dance, 0.0) * 0.09 * under * (1.0 - depth));

	// Relief: tufts on the meadow and soft ripples on the sand tilt the normal, so the
	// sun models them. Height differences of a step along x and z give the slope
	float dry_land = (1.0 - under) * (1.0 - depth) * near;
	float step_size = 0.04;
	float tufty = meadow * (1.0 - yard);     // yards are trampled flat
	float h0 = ground_relief(q, tufty);
	float hx = ground_relief(q + vec2(step_size, 0.0), tufty);
	float hz = ground_relief(q + vec2(0.0, step_size), tufty);
	vec3 slope = vec3(h0 - hx, 0.0, h0 - hz) / step_size;
	NORMAL = normalize(NORMAL + (VIEW_MATRIX * vec4(slope, 0.0)).xyz * 0.12 * dry_land);

	// Sheen: grass seen at a flat angle catches the sky, the far meadow glows a little
	float grazing = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	col += vec3(0.06, 0.08, 0.025) * grazing * tufty * (1.0 - wetness) * (1.0 - depth) * sky_open;

	col *= mix(1.0, cloud_shadow(world_pos.xz, TIME), 1.0 - depth);
	ALBEDO = col;
	ROUGHNESS = mix(mix(1.0, 0.4, wetness * (1.0 - under)), 1.0, depth);
	SPECULAR = mix(0.4 * wetness * (1.0 - under), 0.0, depth);
}
