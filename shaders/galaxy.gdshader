// NOTE:
//   The Milky Way of the galaxy menu (Game/Ui/Menu/GalaxyView.cs), after the artist's
//   impression of ESO/NASA (eso1339g): a warm yellow bar in the middle, a lavender blue
//   disk of old stars, two strong spiral arms leaving the ends of the bar and two fainter
//   ones, clumps of young blue-white stars on the arms, glowing clouds of hydrogen (pink,
//   teal in their hot hearts) and dark globules on them, brown lanes of dust along their
//   inner edge, and in the very middle Sgr A*, the black hole: a dark shadow in a glowing
//   ring with a thin disk of hot gas across it.
//   Depth: three layers move at their own pace when the view pans or zooms. The far field
//   of stars barely moves, the disk moves with the view, a few near stars (big, with
//   diffraction spikes) sweep past faster. While the view flies (warp) thin streaks
//   rush away from warp_center (or towards it, flying out).
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
uniform float warp = 0.0;            // 0 .. 1 streaks of the flight (onto a planet or out again)
uniform vec2 warp_center = vec2(0.5);   // UV the streaks run away from
uniform float warp_way = 1.0;        // 1 streaks fly outwards (flying in), -1 inwards (flying out)
uniform float hole_glow = 0.0;       // 0 .. 1 Sgr A* lights up (hovered)

const float ARM_WIND = 3.1;          // turns of the spiral per e-folding of the radius
const float BAR_ANGLE = 0.62;
const float BH_SHADOW = 0.022;       // Sgr A*: radius of the shadow, in disk radii (far too big, else it would not show)
const float BH_RING = 0.034;         // radius of the glowing ring around it
const float BH_WIDTH = 0.011;
const float BH_MAX_SCALE = 0.9;      // beyond this scale the hole grows slower than the view
const float FAR_PAN = 0.05;          // the far field moves this much of the view
const float NEAR_PAN = 1.6;          // the near stars move this much faster than the disk
const float NEAR_ZOOM = 1.35;        // .. and grow this much faster when zooming (power of the scale)
const float WARP_LANES = 220.0;      // radial lanes of the warp streaks around the circle

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

// The far field of stars at a point of the sky, twinkling a little
float far_field(vec2 sky) {
	return stars(sky * 140.0, 0.08, 0.06) * (0.4 + 0.6 * g_hash(floor(sky * 140.0) + 2.0))
		+ stars(sky * 60.0, 0.03, 0.05) * (0.6 + 0.4 * sin(TIME * 2.0 + g_hash(floor(sky * 60.0)) * 30.0));
}

// A few big near stars with a soft glow and four diffraction spikes, blue-white or golden
vec3 near_stars(vec2 p) {
	vec2 cell = floor(p);
	if (g_hash(cell + 31.7) < 0.9) {
		return vec3(0.0);
	}
	vec2 spot = vec2(g_hash(cell + 3.3), g_hash(cell + 9.1)) * 0.5 + 0.25;
	vec2 o = fract(p) - spot;
	float bright = 0.35 + 0.65 * g_hash(cell + 5.5);
	float core = exp(-dot(o, o) * 5200.0);
	float glow = exp(-length(o) * 24.0) * 0.22;
	float spikes = exp(-abs(o.x) * 260.0) * exp(-abs(o.y) * 9.0) + exp(-abs(o.y) * 260.0) * exp(-abs(o.x) * 9.0);
	float twinkle = 0.85 + 0.15 * sin(TIME * 3.0 + g_hash(cell) * 40.0);
	vec3 tint = mix(vec3(0.7, 0.82, 1.0), vec3(1.0, 0.85, 0.6), step(0.6, g_hash(cell + 2.2)));
	return tint * (core * 1.6 + glow + spikes * 0.45 * bright) * bright * twinkle;
}

// Hyperspace: thin streaks on radial lanes around the center, rushing outwards (or inwards)
float warp_streaks(vec2 from_center) {
	float lane = atan(from_center.y, from_center.x) / 6.2832 * WARP_LANES;
	float seed = g_hash(vec2(floor(lane), 3.7));
	float width = smoothstep(0.22, 0.0, abs(fract(lane) - 0.5));
	float radius = length(from_center);
	float travel = fract(log(radius + 0.04) * 1.2 - TIME * warp_way * (1.2 + seed * 1.6) + seed * 9.0);
	float dash = smoothstep(0.0, 0.25, travel) * smoothstep(0.55, 0.25, travel);
	return dash * width * step(0.45, seed) * smoothstep(0.04, 0.35, radius);
}

// Brightness of an arm pair at disk point d: 1 on the ridge of the spiral
float arm(float theta, float lr, float offset, float sharp) {
	float phase = theta - ARM_WIND * lr + offset;
	return pow(0.5 + 0.5 * cos(2.0 * phase), sharp);
}

void fragment() {
	vec2 screen = (UV - 0.5) * vec2(aspect, 1.0);
	vec2 q = screen - pan;
	vec2 d = g_rot(angle) * (vec2(q.x, q.y / tilt) / scale);
	float r = length(d);
	float theta = atan(d.y, d.x);
	float lr = log(max(r, 0.02));
	float close = smoothstep(1.4, 4.0, scale);    // 0 at the start view .. 1 deep in an arm

	// Far field of stars, barely moving
	vec2 sky = screen * (1.0 + 0.04 * log(scale)) + pan * FAR_PAN + 0.5;
	float field = far_field(sky);
	vec3 col = vec3(0.01, 0.012, 0.03) + vec3(0.8, 0.85, 1.0) * field * 0.8;

	// Disk of old stars: a lavender haze, densest inside, mottled
	float clump = g_fbm(d * 5.0);
	float disk = exp(-r * 1.9) * smoothstep(1.25, 0.55, r);
	col += vec3(0.5, 0.54, 0.86) * disk * (0.8 + 0.5 * clump) * 0.85;

	// Spiral arms: two main ones from the bar ends, two faint ones between, frayed by noise;
	// close in, a finer layer of noise keeps them from going soft
	float grow = smoothstep(0.16, 0.32, r) * smoothstep(1.15, 0.75, r);
	float wobble = (g_fbm(d * 3.0 + 7.0) - 0.5) * 1.2;
	float main_arms = arm(theta + wobble * 0.25, lr, BAR_ANGLE, 6.0);
	float side_arms = arm(theta + wobble * 0.3, lr, BAR_ANGLE + 1.5708, 10.0) * 0.45;
	float arms = (main_arms + side_arms) * grow;
	float knots = g_fbm(d * 14.0 + 2.0);
	float fine = mix(1.0, 0.55 + 0.9 * g_fbm(d * 52.0 + 9.0), close);
	col += vec3(0.62, 0.74, 1.0) * arms * (0.45 + 0.9 * knots) * fine;

	// Young stars on the arms: blue-white clusters; close in single stars of the disk resolve
	float young = stars(d * 90.0, 0.35, 0.08) + stars(d * 190.0 + 4.0, 0.3, 0.07) * 0.6;
	col += vec3(0.75, 0.88, 1.0) * young * arms * 1.6;
	float resolved = stars(d * 420.0 + 1.7, 0.4, 0.09) * (0.3 + 0.7 * g_hash(floor(d * 420.0 + 1.7)));
	col += mix(vec3(1.0, 0.85, 0.65), vec3(0.75, 0.88, 1.0), arms) * resolved * (disk * 2.0 + arms) * close * 1.4;

	// Nebulae: glowing hydrogen on the main arms (pink, teal in the hottest middle), filaments inside
	float cloud = smoothstep(0.58, 0.78, g_fbm(d * 7.0 + 21.0)) * main_arms * grow;
	float strands = 0.45 + 0.8 * g_fbm(d * 34.0 + 3.0);
	float heart = smoothstep(0.74, 0.86, g_fbm(d * 7.0 + 21.0)) * smoothstep(0.5, 0.75, g_fbm(d * 18.0 + 8.0));
	vec3 glow_gas = mix(vec3(1.0, 0.32, 0.5), vec3(0.35, 0.95, 0.9), heart * 0.7);
	col += glow_gas * cloud * strands * 0.75;
	float knot = stars(d * 34.0 + 11.0, 0.3, 0.2) * smoothstep(0.5, 0.7, knots);
	col += vec3(1.0, 0.35, 0.55) * knot * main_arms * grow * 1.1;

	// Dust: brown lanes along the inner edge of the main arms, dark globules on the arms
	float lane = arm(theta + wobble * 0.25, lr, BAR_ANGLE + 0.42, 14.0) * grow;
	float dust = lane * smoothstep(0.35, 0.7, g_fbm(d * 9.0 + 5.0)) * mix(1.0, 0.6 + 0.6 * g_fbm(d * 40.0), close);
	col = mix(col, col * vec3(0.4, 0.28, 0.22), dust * 0.85);
	float globule = smoothstep(0.7, 0.9, g_fbm(d * 16.0 + 40.0)) * arms * (0.6 + 0.4 * g_fbm(d * 60.0));
	col = mix(col, col * vec3(0.45, 0.34, 0.3), globule * mix(0.45, 0.8, close));

	// Bar and bulge: an elongated warm glow and a bright core
	vec2 b = g_rot(-BAR_ANGLE) * d;
	float bar = exp(-(b.x * b.x / 0.075 + b.y * b.y / 0.012));
	float bulge = exp(-r * r * 26.0);
	col += vec3(1.0, 0.86, 0.58) * (bar * 1.1 + bulge * 1.3) * mix(1.0, 0.5, close);
	col += vec3(1.0, 0.8, 0.5) * resolved * (bar + bulge) * close * 2.0;

	// Soft tone curve
	col = 1.0 - exp(-col * 1.4);

	// Sgr A*: the black hole in the middle, round on the screen (not tilted with the disk).
	// A ring of hot gas, brighter on the side that comes towards us, its shadow inside, and a
	// thin disk of gas seen almost edge on that crosses in front of the shadow
	float size = scale < BH_MAX_SCALE ? scale : BH_MAX_SCALE * pow(scale / BH_MAX_SCALE, 0.6);
	vec2 h = q / size;
	float hole = length(h);
	float side = 0.65 + 0.35 * cos(atan(h.y, h.x) + 0.8);
	float ring = exp(-pow((hole - BH_RING) / BH_WIDTH, 2.0)) * (1.0 + hole_glow * 0.5);
	vec3 hot = mix(vec3(0.95, 0.35, 0.08), vec3(1.0, 0.85, 0.45), side);
	col = mix(col, hot, clamp(ring * (0.55 + 0.6 * side), 0.0, 1.0));
	col *= smoothstep(BH_SHADOW - 0.004, BH_SHADOW + 0.004, hole);
	vec2 flat_disk = g_rot(0.18) * h;
	float band = exp(-pow(flat_disk.y / 0.0035, 2.0)) * smoothstep(BH_RING * 2.6, BH_RING * 0.9, abs(flat_disk.x));
	float doppler = 0.55 + 0.45 * smoothstep(0.04, -0.04, flat_disk.x);
	col += vec3(1.0, 0.75, 0.45) * band * doppler * 1.1;
	col += vec3(1.0, 0.6, 0.3) * exp(-hole * 18.0) * 0.12 * (1.0 + hole_glow * 2.0);

	// Near stars sweep past in front of everything
	vec2 near = (screen - pan * NEAR_PAN) / pow(scale, NEAR_ZOOM) * 7.0;
	col += near_stars(near) * (1.0 - dim * 0.5);

	// Warp streaks in front of everything while flying
	if (warp > 0.01) {
		col += vec3(0.75, 0.85, 1.0) * warp_streaks((UV - warp_center) * vec2(aspect, 1.0)) * warp * 0.9;
	}

	// The view darkens towards the planet
	col *= 1.0 - dim * 0.7;
	float vignette = smoothstep(1.2, 0.35, length((UV - 0.5) * vec2(aspect * 0.8, 1.0)));
	COLOR = vec4(col * mix(0.55, 1.0, vignette), 1.0);
}
