// NOTE:
//   Ground of the islands, from the deep up to the meadow on the hill. Patterns run in
//   world coordinates / unit, the bands follow d = island_depth (distance into the land):
//     - meadow:  lush and light green in patches, sunny yellowish stretches, fine blades,
//                clover cushions, flower specks, tufts as relief, a sheen at flat angles,
//                gusts of wind running over it as lighter waves
//     - dunes:   an irregular edge between sand and grass that frays into tufts
//     - yards:   trampled earth with pebbles on the flat foundations of buildings; a
//                settlement is meadow with trodden ways (a place around the fire, a ring
//                way, ways to the gates), a city has asphalt streets, sidewalks and paving
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

// Flat foundations of buildings (World/CoastTerrain.cs, MAX_PADS): xy center, z radius,
// w look (0 yard of trampled earth, 1 settlement with trodden ways, 2 city with streets)
const int MAX_PADS = 48;
uniform vec4 pads[MAX_PADS];
uniform int pad_count = 0;

// Ground plan of a settlement (Tribe/Village/Settlement.cs) in world units: x radius of the
// place around the fire, y radius of the ring way, z half the width of a way. Straight
// ways run from the place out through the gates at the plane angles settlement_gates
uniform vec3 settlement_plan = vec3(90.0, 266.0, 18.0);
uniform vec4 settlement_gates = vec4(1.0, 2.95, 4.14, 6.09);
const float WAY_TRAIL = 22.0;                // Units a way runs on beyond the foundation while it thins out

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

// The nearest way of a settlement around a point relative to its fire: x the distance to the
// middle line of the way (below 0 on the place around the fire), y the length along the way
// (-1 on the place)
vec2 settlement_lane(vec2 rel) {
	float r = length(rel);
	if (r < settlement_plan.x) {
		return vec2(r - settlement_plan.x, -1.0);
	}
	vec2 lane = vec2(abs(r - settlement_plan.y), atan(rel.y, rel.x) * settlement_plan.y + 100.0 * settlement_plan.y);
	for (int k = 0; k < 4; k++) {
		vec2 out_dir = vec2(cos(settlement_gates[k]), sin(settlement_gates[k]));
		float along = dot(rel, out_dir);
		float off = abs(dot(rel, vec2(-out_dir.y, out_dir.x)));
		if (along > 0.0 && off < lane.x) {
			lane = vec2(off, along);
		}
	}
	return lane;
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
	// A settlement (w 1) is meadow with trodden ways, a city (w 2) a lawn with asphalt streets,
	// sidewalks and a paved place
	float yard = 0.0;
	float street = 0.0;
	float walk = 0.0;
	float paved = 0.0;
	float dash = 0.0;
	float half_way = settlement_plan.z;
	for (int i = 0; i < pad_count; i++) {
		vec2 rel = world_pos.xz - pads[i].xy;
		float reach = length(rel) + (coast_noise(q * 1.8 + float(i) * 3.1) - 0.5) * 1.6 * unit;
		float here = 1.0 - smoothstep(pads[i].z * 0.8, pads[i].z + 0.6 * unit, reach);
		if (pads[i].w < 0.5) {
			yard = max(yard, here);
			continue;
		}
		vec2 lane = settlement_lane(rel);
		// Beyond the foundation nothing ends at a line: the ways through the gates run on, get
		// narrower and break up (trail 0 at the edge .. 1 where they are gone), and trampled
		// patches lie scattered in the meadow around the edge
		float beyond = length(rel) - pads[i].z;
		float trail = clamp(beyond / (WAY_TRAIL * unit), 0.0, 1.0);
		float width = half_way * mix(1.0, 0.4, trail);
		float wobble = (coast_noise(q * 1.3 + float(i) * 5.0) - 0.5) * 1.2 * unit;
		float crumble = coast_fbm(q * 0.9 + float(i) * 2.3);
		float track = (1.0 - smoothstep(width * 0.5, width * 1.4, lane.x + wobble)) * (1.0 - trail)
			* smoothstep(trail * 0.75 - 0.2, trail * 0.75 + 0.1, crumble);
		float patches = smoothstep(0.5, 0.72, coast_fbm(q * 0.45 + float(i) * 4.1)) * (1.0 - smoothstep(-2.0 * unit, 9.0 * unit, beyond)) * 0.55;
		if (pads[i].w < 1.5) {
			yard = max(yard, max(here * (0.18 + 0.82 * track), max(track, patches)));
			continue;
		}
		// City: the whole foundation is paved but a few lawns away from the streets; outside,
		// the asphalt crumbles into the dirt track of the old way
		float on_pad = smoothstep(0.35, 0.6, here);
		float edge = 0.06 * unit;
		float solid = smoothstep(trail * 1.3 - 0.1, trail * 1.3 + 0.1, crumble) * (1.0 - trail);
		float lane_here = (1.0 - smoothstep(width - edge, width + edge, lane.x)) * step(0.0, lane.y) * solid;
		float lawn = smoothstep(0.6, 0.66, coast_fbm(q * 0.3 + float(i) * 7.7)) * smoothstep(half_way * 2.0, half_way * 2.6, lane.x);
		street = max(street, lane_here);
		walk = max(walk, on_pad * (1.0 - smoothstep(half_way * 1.5 - edge, half_way * 1.5 + edge, lane.x)));
		paved = max(paved, on_pad * (1.0 - lawn));
		dash = max(dash, lane_here * step(beyond, 0.0) * (1.0 - step(0.07 * unit, lane.x)) * step(fract(lane.y / (2.4 * unit)), 0.5));
		yard = max(yard, max(track, patches) * (1.0 - on_pad));
	}
	vec3 earth = mix(vec3(0.3, 0.21, 0.13), vec3(0.42, 0.32, 0.2), coast_fbm(q * 1.2 + 3.0)) * (0.9 + 0.2 * grain);
	vec2 pebbleCell = floor(q * 6.0);
	float pebble = step(0.86, coast_hash(pebbleCell + 11.0)) * (1.0 - smoothstep(0.12, 0.22, length(fract(q * 6.0) - 0.5)));
	earth = mix(earth, vec3(0.55, 0.52, 0.47) * (0.85 + 0.3 * coast_hash(pebbleCell)), pebble * 0.8 * near);
	float stubble = smoothstep(0.62, 0.8, coast_noise(q * 13.0)) * (1.0 - yard * yard);
	col = mix(col, mix(earth, grass, stubble), yard * meadow);
	// City: pale paving slabs with joints, dark asphalt with a dashed middle line
	vec2 slab = abs(fract(q * 1.1) - 0.5);
	float joint = smoothstep(0.44, 0.48, max(slab.x, slab.y)) * near;
	vec3 paving = vec3(0.58, 0.56, 0.52) * (0.92 + 0.14 * coast_hash(floor(q * 1.1))) * (1.0 - 0.25 * joint);
	vec3 asphalt = vec3(0.12, 0.12, 0.135) * (0.85 + 0.3 * grain);
	// Between the streets: worn grey asphalt of yards and parking lots with patched spots and cracks
	float patched = smoothstep(0.55, 0.6, coast_fbm(q * 0.7 + 13.0));
	float crack = (1.0 - smoothstep(0.0, 0.035, abs(coast_fbm(q * 0.8 + 31.0) - 0.5))) * near;
	vec3 lot = vec3(0.3, 0.3, 0.32) * (0.88 + 0.24 * grain) * (1.0 - 0.18 * patched) * (1.0 - 0.3 * crack);
	col = mix(col, lot, paved * meadow);
	col = mix(col, paving, walk * meadow);
	col = mix(col, asphalt, street * meadow);
	col = mix(col, vec3(0.86, 0.82, 0.62), dash * meadow);
	yard = max(yard, max(walk, max(paved, street)));     // paved ground has no tufts either
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
