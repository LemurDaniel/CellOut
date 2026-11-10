// NOTE:
//   Shared pieces of the sea floor and the coast, included by ocean.gdshader,
//   coast_ground.gdshader and coast_water.gdshader:
//     - ocean_floor_color   the patches, sediment, ripples, stones and caustics of the sea floor, so the coast
//                           ground fades into exactly the same floor with depth
//     - island_depth        signed distance into the islands of the archipelago, the same
//                           formula as Coast.IslandRadius / Coast.LandDepth in C#
//     - coast_shoreline     the wobbling, breathing water line, shared by ground and water
//     - coast_hash / coast_noise / coast_fbm   2D value noise

// Hash without sine: sin() of large values is imprecise on some GPUs and turns the
// noise into blocks
float coast_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// Smooth 2D value noise, 0..1
float coast_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(coast_hash(i), coast_hash(i + vec2(1.0, 0.0)), u.x),
			mix(coast_hash(i + vec2(0.0, 1.0)), coast_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

// Three octaves, 0..1
float coast_fbm(vec2 p) {
	return coast_noise(p) * 0.55 + coast_noise(p * 2.1 + 5.3) * 0.3 + coast_noise(p * 4.3 - 2.7) * 0.15;
}

// Stones lying on the floor: at most one per grid cell, shifted inside its cell so the
// grid does not show, a little oval and lit from the top left. Returns coverage 0..1 in x
// and the light on the stone 0..1 in y
vec2 floor_stones(vec2 g, float share) {
	vec2 cell = floor(g);
	vec2 shift = (vec2(coast_hash(cell + 1.7), coast_hash(cell + 8.3)) - 0.5) * 0.5;
	vec2 spot = (fract(g) - 0.5 - shift) * vec2(1.0, 1.0 + 0.5 * coast_hash(cell + 6.1));
	float size = mix(0.07, 0.22, coast_hash(cell + 4.1)) * step(1.0 - share, coast_hash(cell + 2.9));
	float cover = 1.0 - smoothstep(size * 0.75, size, length(spot));
	float lit = clamp(0.5 - dot(spot, vec2(0.7, 0.7)) / max(size, 0.001) * 0.6, 0.0, 1.0);
	return vec2(cover, lit);
}

// Sea floor color at pattern point p (world xz / world_scale), caustic strength 0..1 out:
// wide soft patches, drifts of sediment, fine sand ripples and stones in two sizes
vec3 ocean_floor_color(vec2 p, float time, vec3 deep, vec3 shallow, out float caustic) {
	float t = time * 0.25;
	float c = sin(p.x * 11.0 + t * 2.0 + sin(p.y * 8.0 - t))
			* sin(p.y * 9.0 - t * 1.4 + sin(p.x * 6.0 + t));
	float n = sin(p.x * 2.3 + t * 0.3) * sin(p.y * 1.7 - t * 0.2);
	caustic = max(c, 0.0);
	vec3 col = mix(deep, shallow, 0.5 + 0.5 * n);

	// Sediment and ripples: the sand lies in darker and lighter drifts, fine waves bend around them
	float sediment = coast_fbm(p * 5.0 + 11.0);
	col *= 0.7 + 0.6 * sediment;
	col *= 1.0 + 0.08 * sin(p.x * 95.0 + p.y * 40.0 + sediment * 14.0);

	// Stones: a few big ones and many pebbles, dark below, the water color on their lit side
	vec2 big = floor_stones(p * 13.0 + 3.1, 0.4);
	vec2 small = floor_stones(p * 41.0 - 7.7, 0.55);
	col = mix(col, mix(deep * 0.55, shallow * 1.2, small.y), small.x * 0.6);
	col = mix(col, mix(deep * 0.45, shallow * 1.3, big.y), big.x * 0.85);
	return col;
}

// Islands of the archipelago, set by World/Coast.cs: xy center (world xz), z radius, w seed
#define MAX_ISLANDS 8
uniform vec4 islands[MAX_ISLANDS];
uniform int island_count = 0;

// Radius of one island towards an angle: a circle with three soft bulges
float island_radius(vec4 island, float angle) {
	float s = island.w;
	return island.z * (1.0
			+ 0.12 * sin(2.0 * angle + s * 1.7)
			+ 0.08 * sin(3.0 * angle + s * 2.9)
			+ 0.05 * sin(5.0 * angle + s * 4.3));
}

// Signed distance into the nearest island in world units, negative in the sea
float island_depth(vec2 p) {
	float depth = -1e9;
	for (int i = 0; i < MAX_ISLANDS; i++) {
		if (i >= island_count) {
			break;
		}
		vec2 rel = p - islands[i].xy;
		depth = max(depth, island_radius(islands[i], atan(rel.y, rel.x)) - length(rel));
	}
	return depth;
}

// Offset of the water line from the logic shore, in world units: a slow wobble around
// the islands plus the swash that rolls in and out
float coast_shoreline(vec2 p, float unit, float time) {
	vec2 a = p / unit;
	return (coast_fbm(a * 0.15 + 3.7) - 0.5) * 1.6 * unit + sin(time * 0.7 + (a.x + a.y) * 0.11) * 0.35 * unit;
}
