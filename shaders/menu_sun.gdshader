// NOTE:
//   The star of the planet view (Game/Ui/Menu/StarSystemView.cs) on a square rect, added onto
//   the dark sky: a disk with darker rim (limb darkening) and slowly boiling granules, a
//   corona with streamers that drift around it, a soft wide glow and a thin horizontal
//   streak of light like a camera lens catches it. Color and size come from the star kind
//   (Core/StarSystem.cs ColorOf, SizeOf). Pure screen shader: looks the same on every tier.

shader_type canvas_item;
render_mode blend_add;

uniform vec3 tint : source_color = vec3(1.0, 0.92, 0.68);
uniform float disk = 0.3;            // radius of the disk in half rect sizes
uniform float seed = 0.0;

float s_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float s_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(s_hash(i), s_hash(i + vec2(1.0, 0.0)), u.x),
			mix(s_hash(i + vec2(0.0, 1.0)), s_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float s_fbm(vec2 p) {
	return s_noise(p) * 0.5 + s_noise(p * 2.1 + 3.1) * 0.25 + s_noise(p * 4.3 - 1.7) * 0.15 + s_noise(p * 8.7 + 5.3) * 0.1;
}

void fragment() {
	vec2 c = UV * 2.0 - 1.0;
	float r = length(c);
	float a = atan(c.y, c.x);
	float out_r = max(r - disk, 0.0);

	// Corona: a bright rim glow and long soft streamers that drift slowly around the star
	float streamer = s_fbm(vec2(a * 3.0 + seed, TIME * 0.03)) * s_fbm(vec2(a * 9.0 - seed, TIME * 0.05 + 4.0));
	float corona = exp(-out_r * 14.0 / max(disk, 0.05)) * 0.9
		+ exp(-out_r * 3.2 / max(disk, 0.05)) * (0.18 + 0.9 * streamer)
		+ exp(-out_r * 1.6) * 0.18;
	vec3 col = tint * corona;

	// Lens streak: a thin horizontal line of light through the star
	col += mix(tint, vec3(1.0), 0.4) * exp(-abs(c.y) * 160.0) * exp(-abs(c.x) * 1.6) * 0.5;

	// Disk: hotter and whiter in the middle, darker at the rim, granules boil slowly
	if (r < disk) {
		float mu = sqrt(1.0 - (r / disk) * (r / disk));
		float granules = s_fbm(c / disk * 9.0 + vec2(seed, TIME * 0.06)) - 0.5;
		vec3 face = mix(tint, vec3(1.0, 0.98, 0.92), 0.55 * mu) * (0.55 + 0.5 * mu) * (1.0 + granules * 0.35);
		col = mix(col, face * 1.15, smoothstep(disk, disk - 0.008, r));
	}

	// Nothing at the edge of the rect
	col *= smoothstep(1.0, 0.75, r);
	COLOR = vec4(col, 1.0);
}
