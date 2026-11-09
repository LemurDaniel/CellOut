// NOTE:
//   Cell membrane: a softly wobbling, translucent hull drawn as a lipid bilayer, a bright
//   outer edge and a second, thinner line just inside it, with a faint speckle of lipid
//   heads. See-through in the middle (nucleus, organelles and DNA visible), dense towards
//   the rim. The outline follows shape: eight radii around the cell (index 0 forward),
//   interpolated like CellDesign.ShapeRadius. Set per cell by Cells/CellVisual3D.cs
//   (body_color, seed, core_alpha, shape) and per frame (stretch, flash).

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back;

uniform vec4 body_color : source_color = vec4(0.45, 0.85, 0.65, 1.0);
uniform float seed = 0.0;
uniform float stretch = 0.0;      // squash and stretch along the movement (+X)
uniform float flash = 0.0;        // hit flash 0..1
uniform float core_alpha = 0.45;  // transparency in the middle
uniform float shape[8];           // unset (all 0) means round, see shape_radius

varying vec3 local_pos;

float hash(vec3 p) {
	return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

// Smooth 3D value noise, the speckle of lipid heads on the surface
float noise(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	vec3 u = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(mix(hash(i), hash(i + vec3(1, 0, 0)), u.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), u.x), u.y),
		mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), u.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), u.x), u.y),
		u.z);
}

// Periodic Catmull-Rom through the shape radii, the same spline as CellDesign.ShapeRadius
float shape_radius(float angle) {
	float t = fract(angle / 6.2831853) * 8.0;
	int i = int(floor(t));
	float f = t - float(i);
	float p0 = shape[(i + 7) % 8];
	float p1 = shape[i % 8];
	float p2 = shape[(i + 1) % 8];
	float p3 = shape[(i + 2) % 8];
	return 0.5 * (2.0 * p1 + (p2 - p0) * f + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * f * f + (3.0 * p1 - p0 - 3.0 * p2 + p3) * f * f * f);
}

void vertex() {
	// Outline: stretch or squash the hull towards its shape radius at this angle
	float a = atan(VERTEX.z, VERTEX.x);
	float r = shape_radius(a);
	VERTEX.xz *= r > 0.01 ? r : 1.0;   // materials that never set shape stay round (intro bubble)

	// Soft wobble: a slow two-lobe breath and small, quick ripples, no sharp corners
	float w = sin(a * 2.0 + TIME * 1.1 + seed) * 0.03
			+ sin(a * 3.0 - TIME * 1.7 + seed * 1.3) * 0.018
			+ sin(a * 7.0 + TIME * 2.6 + seed * 0.7) * 0.006
			+ sin(VERTEX.y * 5.0 + TIME * 1.3 + seed) * 0.012;
	VERTEX += NORMAL * w;
	VERTEX.x *= 1.0 + stretch;
	VERTEX.z *= 1.0 - stretch * 0.45;
	local_pos = VERTEX;
}

void fragment() {
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.2);
	vec3 col = mix(body_color.rgb, vec3(1.0, 0.35, 0.35), flash);
	vec3 light = mix(col, vec3(1.0), 0.55);

	// Bilayer: the outer edge glows, a second thin line runs just inside it
	float inner_line = smoothstep(0.30, 0.38, fres) - smoothstep(0.42, 0.52, fres);
	float outer_edge = smoothstep(0.62, 0.9, fres);

	// Lipid heads: faint speckle drifting slowly over the surface
	float speck = smoothstep(0.62, 0.8, noise(local_pos * 18.0 + vec3(TIME * 0.15, 0.0, seed)));

	ALBEDO = col;
	ROUGHNESS = 0.15;
	SPECULAR = 0.75;
	EMISSION = col * 0.16
			+ light * fres * 0.35
			+ light * inner_line * 0.35
			+ light * outer_edge * 0.25
			+ light * speck * 0.06
			+ vec3(1.0, 0.3, 0.3) * flash * 0.6;
	ALPHA = clamp(mix(core_alpha, 1.0, fres) + inner_line * 0.25 + speck * 0.05, 0.0, 1.0) * body_color.a;
}
