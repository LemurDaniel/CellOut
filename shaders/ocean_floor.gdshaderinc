// NOTE:
//   Shared pieces of the sea floor and the coast, included by ocean.gdshader,
//   coast_ground.gdshader and coast_water.gdshader:
//     - ocean_floor_color   the soft patches and caustics of the sea floor, so the coast
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

// Sea floor color at pattern point p (world xz / world_scale), caustic strength 0..1 out
vec3 ocean_floor_color(vec2 p, float time, vec3 deep, vec3 shallow, out float caustic) {
	float t = time * 0.25;
	float c = sin(p.x * 11.0 + t * 2.0 + sin(p.y * 8.0 - t))
			* sin(p.y * 9.0 - t * 1.4 + sin(p.x * 6.0 + t));
	float n = sin(p.x * 2.3 + t * 0.3) * sin(p.y * 1.7 - t * 0.2);
	caustic = max(c, 0.0);
	return mix(deep, shallow, 0.5 + 0.5 * n);
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
