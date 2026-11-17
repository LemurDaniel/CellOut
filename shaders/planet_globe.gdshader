// NOTE:
//   A planet of the galaxy menu as a turning globe on a square rect (Game/Ui/Menu/PlanetGlobe.cs):
//   oceans and continents from 3D noise (land_share of the surface), beaches at the coast,
//   highlands inside, ice caps at the poles, clouds drifting on their own, sunlight from sun_dir
//   (upper left by default) with a soft terminator, a glint on the sea and an atmosphere that glows at
//   the rim and around the globe. Colors and shares come from data/planets.json (Core/Planets.cs).
//   Giants may wear a ring (ring > 0, radius shrinks so it fits the rect).
//   Pure screen shader: looks the same on every graphics tier.

shader_type canvas_item;

uniform vec3 ocean : source_color = vec3(0.08, 0.32, 0.62);
uniform vec3 land : source_color = vec3(0.27, 0.56, 0.24);
uniform vec3 highland : source_color = vec3(0.55, 0.47, 0.33);
uniform vec3 sand : source_color = vec3(0.86, 0.78, 0.55);
uniform vec3 atmosphere : source_color = vec3(0.45, 0.7, 1.0);
uniform float land_share = 0.4;
uniform float clouds = 0.5;
uniform float ice = 0.1;
uniform float bands = 0.0;          // 0 a rocky globe .. 1 a gas giant: stripes of land and highland color instead of continents
uniform float seed = 0.0;
uniform float spin = 0.12;           // turns per ... radians per second
uniform float glow = 1.0;            // 0 .. 1, the halo around the globe (hover, selection)
uniform float radius = 0.8;          // globe radius in half rect sizes, the halo fills the rest (smaller with rings)
uniform float ring = 0.0;            // 0 no ring .. 1 a full ring of ice and dust around the equator
uniform vec3 sun_dir = vec3(-0.55, 0.45, 0.7);   // where the light comes from: x right, y up, z towards the camera (behind: a crescent)

const float RING_TILT = 0.28;        // the ring plane seen from a little above: flat ellipse
const float RING_INNER = 1.3;        // ring from .. to, in globe radii
const float RING_OUTER = 2.15;

// Integer hash of a lattice point: float hashes gave whole flat lattice cells (straight
// coast lines) or mirrored continents
float p_hash(vec3 p) {
	uvec3 q = uvec3(ivec3(p) + 4096);
	uint h = (q.x * 1597334673u) ^ (q.y * 3812015801u) ^ (q.z * 2798796415u);
	h = (h ^ (h >> 16u)) * 2246822519u;
	h = h ^ (h >> 13u);
	return float(h) / 4294967295.0;
}
float p_noise(vec3 x) {
	vec3 i = floor(x);
	vec3 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(p_hash(i + vec3(0, 0, 0)), p_hash(i + vec3(1, 0, 0)), f.x),
			mix(p_hash(i + vec3(0, 1, 0)), p_hash(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(p_hash(i + vec3(0, 0, 1)), p_hash(i + vec3(1, 0, 1)), f.x),
			mix(p_hash(i + vec3(0, 1, 1)), p_hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

float p_fbm(vec3 p) {
	return p_noise(p) * 0.5 + p_noise(p * 2.03 + 1.7) * 0.25 + p_noise(p * 4.07 - 3.1) * 0.15 + p_noise(p * 8.1 + 5.2) * 0.1;
}

vec3 rot_y(vec3 v, float a) {
	return vec3(v.x * cos(a) + v.z * sin(a), v.y, -v.x * sin(a) + v.z * cos(a));
}

void fragment() {
	vec2 c = (UV * 2.0 - 1.0) / radius;
	float r = length(c);
	vec3 sun = normalize(sun_dir);

	// Halo outside the globe
	float halo = smoothstep(1.0 + 0.25, 1.0, r) * step(1.0, r);
	vec4 outside = vec4(atmosphere, pow(halo, 2.2) * (0.35 + 0.45 * glow));
	vec4 base;
	if (r >= 1.0) {
		base = outside;
	} else {
		vec3 n = vec3(c.x, -c.y, sqrt(1.0 - r * r));
		vec3 s = rot_y(n, TIME * spin);

		// Continents: the noise above a threshold that keeps land_share of the surface
		vec3 at = s * 1.9 + seed;
		float h = p_fbm(at) + (p_noise(at * 6.0) - 0.5) * 0.06;
		float sea_level = mix(0.68, 0.33, land_share);
		float is_land = smoothstep(sea_level - 0.006, sea_level + 0.006, h);
		float coast = 1.0 - smoothstep(0.0, 0.035, h - sea_level);
		float high = smoothstep(sea_level + 0.08, sea_level + 0.2, h);
		vec3 ground = mix(land, highland, high);
		ground = mix(ground, sand, coast * 0.8);
		ground *= 0.85 + 0.3 * p_noise(at * 9.0);
		vec3 sea = ocean * (0.75 + 0.35 * smoothstep(sea_level - 0.2, sea_level, h));
		vec3 col = mix(sea, ground, is_land);

		// Gas giant: wavy stripes along the latitude, a storm here and there
		if (bands > 0.001) {
			float wave = p_fbm(vec3(s.x * 2.0, s.y * 9.0, s.z * 2.0) + seed) * 2.0;
			float stripe = 0.5 + 0.5 * sin(s.y * 16.0 + wave * 3.0);
			vec3 giant = mix(land, highland, stripe) * (0.85 + 0.3 * p_noise(s * 12.0 + seed));
			col = mix(col, giant, bands);
		}

		// Ice caps
		float cap = smoothstep(1.0 - ice * 0.75, 1.0 - ice * 0.75 + 0.05, abs(s.y) + (p_noise(s * 7.0) - 0.5) * 0.12);
		col = mix(col, vec3(0.93, 0.96, 1.0), cap * step(0.001, ice));

		// Clouds drift slower than the ground turns
		vec3 sky = rot_y(n, TIME * spin * 0.6) * 2.6 + seed * 1.3 + vec3(TIME * 0.02, 0.0, 0.0);
		float cover = smoothstep(1.0 - clouds * 0.62, 1.0 - clouds * 0.62 + 0.18, p_fbm(sky));
		col = mix(col, vec3(1.0), cover * 0.85);

		// Light: soft terminator, a faint night side, a glint on the sea
		float lambert = dot(n, sun);
		float day = smoothstep(-0.15, 0.35, lambert);
		col *= mix(0.06, 1.05, day);
		vec3 half_vec = normalize(sun + vec3(0.0, 0.0, 1.0));
		float glint = pow(max(dot(n, half_vec), 0.0), 60.0) * (1.0 - is_land) * (1.0 - cover);
		col += vec3(1.0, 0.95, 0.85) * glint * 0.6;

		// Atmosphere: a bright rim on the lit side, thin over the night
		float rim = pow(1.0 - n.z, 2.5);
		col = mix(col, atmosphere, rim * mix(0.15, 0.85, day));

		// Antialiased edge against the halo
		float edge = smoothstep(1.0, 1.0 - 0.02, r);
		base = mix(outside, vec4(col, 1.0), edge);
	}

	// Ring: bands of ice and dust on a tilted plane; the far half hides behind the globe,
	// the near half crosses in front of it, lit from the left like the globe
	if (ring > 0.001) {
		vec2 e = vec2(c.x, c.y / RING_TILT);
		float rr = length(e);
		float stripes = 0.55 + 0.45 * sin(rr * 38.0 + seed) * sin(rr * 11.0 + seed * 0.3);
		float in_ring = smoothstep(RING_INNER, RING_INNER + 0.04, rr) * smoothstep(RING_OUTER, RING_OUTER - 0.04, rr);
		float gap = 1.0 - exp(-pow((rr - mix(RING_INNER, RING_OUTER, 0.62)) / 0.025, 2.0)) * 0.9;
		float alpha = in_ring * stripes * gap * ring * 0.85;
		vec3 ring_col = mix(highland, sand, 0.6) * (0.75 + 0.35 * stripes) * (0.6 + 0.5 * smoothstep(0.6, -0.6, c.x));
		bool is_far = c.y < 0.0;
		float behind = is_far ? step(1.0, r) : 1.0;
		base = mix(base, vec4(ring_col, 1.0), alpha * behind);
	}
	COLOR = base;
}
