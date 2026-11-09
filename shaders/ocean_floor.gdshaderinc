// NOTE:
//   Shared pieces of the sea floor and the coast, included by ocean.gdshader,
//   coast_ground.gdshader and coast_water.gdshader:
//     - ocean_floor_color   the soft patches and caustics of the sea floor, so the coast
//                           ground fades into exactly the same floor with depth
//     - coast_shoreline     the wobbling, breathing water line, shared by ground and water
//     - coast_hash / coast_noise / coast_fbm   2D value noise

float coast_hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
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

// Distance of the water line from the straight logic shore, in world units: a slow
// wobble along the coast plus the swash that rolls in and out
float coast_shoreline(float along, float unit, float time) {
	float a = along / unit;
	return (coast_fbm(vec2(a * 0.15, 3.7)) - 0.5) * 1.6 * unit + sin(time * 0.7 + a * 0.11) * 0.35 * unit;
}
