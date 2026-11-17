// NOTE:
//   The Milky Way of the galaxy menu (Game/Ui/Menu/GalaxyView.cs), after the artist's
//   impression of ESO/NASA (eso1339g): a warm yellow bar in the middle, a lavender blue
//   disk of old stars, two strong spiral arms leaving the ends of the bar and two fainter
//   ones, clumps of young blue-white stars on the arms with pink nebulae, brown lanes of
//   dust along their inner edge, the field of stars around it, and in the very middle
//   Sgr A*, the black hole: a dark shadow in a glowing ring (like the EHT image).
//   The disk lies tilted (tilt) and turns slowly (angle). C# places the planets with the
//   same mapping (GalaxyView.ToScreen): screen q = R(-angle) * disk, y squashed by tilt,
//   times scale, plus pan. Pure screen shader: looks the same on every graphics tier.

shader_type canvas_item;

uniform float aspect = 1.777;        // width / height of the rect
uniform float angle = 0.0;           // turn of the disk in radians
uniform float tilt = 0.58;           // 1 = seen from above, smaller = flatter
uniform float scale = 0.46;          // disk radius in screen heights
uniform vec2 pan = vec2(0.0);        // shift of the disk in screen heights
uniform float dim = 0.0;             // 0 .. 1 darkens towards the planet view

const float ARM_WIND = 3.1;          // turns of the spiral per e-folding of the radius
const float BAR_ANGLE = 0.62;
const float BH_SHADOW = 0.022;       // Sgr A*: radius of the shadow, in disk radii (far too big, else it would not show)
const float BH_RING = 0.034;          // radius of the glowing ring around it
const float BH_WIDTH = 0.011;
const float BH_MAX_SCALE = 0.6;      // the hole grows with the view only up to this scale

float g_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float g_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(g_hash(i), g_hash(i + vec2(1.0, 0.0)), u.x),
			mix(g_hash(i + vec2(0.0, 1.0)), g_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float g_fbm(vec2 p) {
	return g_noise(p) * 0.5 + g_noise(p * 2.07 + 3.1) * 0.25 + g_noise(p * 4.13 - 1.7) * 0.15 + g_noise(p * 8.3 + 5.3) * 0.1;
}

mat2 g_rot(float a) {
	return mat2(vec2(cos(a), sin(a)), vec2(-sin(a), cos(a)));
}

// Small sharp dots on a grid: one star per cell at most, brighter ones rarer
float stars(vec2 p, float density, float size) {
	vec2 cell = floor(p);
	vec2 spot = vec2(g_hash(cell + 1.3), g_hash(cell + 7.9));
	float d = length(fract(p) - spot);
	float has = step(1.0 - density, g_hash(cell));
	return has * smoothstep(size, 0.0, d);
}

// Brightness of an arm pair at disk point d: 1 on the ridge of the spiral
float arm(float theta, float lr, float offset, float sharp) {
	float phase = theta - ARM_WIND * lr + offset;
	return pow(0.5 + 0.5 * cos(2.0 * phase), sharp);
}

void fragment() {
	vec2 q = (UV - 0.5) * vec2(aspect, 1.0) - pan;
	vec2 d = g_rot(angle) * (vec2(q.x, q.y / tilt) / scale);
	float r = length(d);
	float theta = atan(d.y, d.x);
	float lr = log(max(r, 0.02));

	// Field of stars behind and around the galaxy, twinkling a little
	vec2 sky = UV * vec2(aspect, 1.0);
	float field = stars(sky * 140.0, 0.08, 0.06) * (0.4 + 0.6 * g_hash(floor(sky * 140.0) + 2.0))
		+ stars(sky * 60.0, 0.03, 0.05) * (0.6 + 0.4 * sin(TIME * 2.0 + g_hash(floor(sky * 60.0)) * 30.0));
	vec3 col = vec3(0.01, 0.012, 0.03) + vec3(0.8, 0.85, 1.0) * field * 0.8;

	// Disk of old stars: a lavender haze, densest inside, mottled
	float clump = g_fbm(d * 5.0);
	float disk = exp(-r * 1.9) * smoothstep(1.25, 0.55, r);
	col += vec3(0.5, 0.54, 0.86) * disk * (0.8 + 0.5 * clump) * 0.85;

	// Spiral arms: two main ones from the bar ends, two faint ones between, frayed by noise
	float grow = smoothstep(0.16, 0.32, r) * smoothstep(1.15, 0.75, r);
	float wobble = (g_fbm(d * 3.0 + 7.0) - 0.5) * 1.2;
	float main_arms = arm(theta + wobble * 0.25, lr, BAR_ANGLE, 6.0);
	float side_arms = arm(theta + wobble * 0.3, lr, BAR_ANGLE + 1.5708, 10.0) * 0.45;
	float arms = (main_arms + side_arms) * grow;
	float knots = g_fbm(d * 14.0 + 2.0);
	col += vec3(0.62, 0.74, 1.0) * arms * (0.45 + 0.9 * knots);

	// Young stars on the arms: blue-white clusters, pink nebulae here and there
	float young = stars(d * 90.0, 0.35, 0.08) + stars(d * 190.0 + 4.0, 0.3, 0.07) * 0.6;
	col += vec3(0.75, 0.88, 1.0) * young * arms * 1.6;
	float nebula = stars(d * 34.0 + 11.0, 0.3, 0.2) * smoothstep(0.5, 0.7, knots);
	col += vec3(1.0, 0.35, 0.55) * nebula * main_arms * grow * 1.4;

	// Dust: brown lanes along the inner edge of the main arms
	float lane = arm(theta + wobble * 0.25, lr, BAR_ANGLE + 0.42, 14.0) * grow;
	float dust = lane * smoothstep(0.35, 0.7, g_fbm(d * 9.0 + 5.0));
	col = mix(col, col * vec3(0.4, 0.28, 0.22), dust * 0.85);

	// Bar and bulge: an elongated warm glow and a bright core
	vec2 b = g_rot(-BAR_ANGLE) * d;
	float bar = exp(-(b.x * b.x / 0.075 + b.y * b.y / 0.012));
	float bulge = exp(-r * r * 26.0);
	col += vec3(1.0, 0.86, 0.58) * (bar * 1.1 + bulge * 1.3);

	// Sgr A*: the black hole in the middle, round on the screen (not tilted with the disk),
	// a ring of hot gas brighter on the side that comes towards us, its shadow inside
	float hole = length(q) / min(scale, BH_MAX_SCALE);   // stays small when the view flies in
	float side = 0.65 + 0.35 * cos(atan(q.y, q.x) + 0.8);
	float ring = exp(-pow((hole - BH_RING) / BH_WIDTH, 2.0));

	// Soft tone curve, the view darkens towards the planet
	col = 1.0 - exp(-col * 1.4);
	col = mix(col, mix(vec3(0.95, 0.35, 0.08), vec3(1.0, 0.85, 0.45), side), clamp(ring * (0.55 + 0.6 * side), 0.0, 1.0));
	col *= smoothstep(BH_SHADOW - 0.004, BH_SHADOW + 0.004, hole);
	col *= 1.0 - dim * 0.7;
	float vignette = smoothstep(1.2, 0.35, length((UV - 0.5) * vec2(aspect * 0.8, 1.0)));
	COLOR = vec4(col * mix(0.55, 1.0, vignette), 1.0);
}
