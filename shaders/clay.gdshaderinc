// NOTE:
//   Shared body of the modelling clay, included by clay.gdshader (opaque, soft skin) and
//   clay_translucent.gdshader (see-through cell look, CLAY_TRANSLUCENT). See clay.gdshader for the look.
//   Paint: a second color laid on as a pattern (stripes, spots, patches, saddle), on the
//   back and flanks, the belly stays light (CreatureDesign.Pattern). On top the spray paint:
//   up to 160 dots (CreatureDesign.MAX_SPLATS) with a speckled rim like a spray can.
uniform vec4 clay_color : source_color = vec4(0.8, 0.55, 0.45, 1.0);
uniform float seed = 0.0;
uniform float knead = 1.0;           // 1 fresh clay, 0 smooth living skin
uniform float wobble = 0.0;          // jelly wobble of the cutscene blob
uniform float flash = 0.0;           // hit flash 0..1
uniform float swim_phase = 0.0;      // runs faster while swimming
uniform float swim_amp = 0.0;        // wave amplitude at the tail in model units
uniform float swim_vertical = 0.0;   // 0 fish (side to side), 1 marine mammal (up and down)
uniform float half_length = 0.7;     // half the vertebra span, the wave grows towards the tail
uniform int pattern = 0;             // 0 none, 1 stripes, 2 spots, 3 patches, 4 saddle (CreaturePattern)
uniform vec4 pattern_color : source_color = vec4(0.3, 0.2, 0.2, 1.0);
uniform float pattern_scale = 1.0;
uniform int splat_count = 0;
uniform vec4 splat_pos[160];         // xyz center in model space, w radius
uniform vec4 splat_color[160];

varying vec3 local_pos;

float hash(vec3 p) {
	return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

vec3 hash3(vec3 p) {
	return fract(sin(vec3(dot(p, vec3(127.1, 311.7, 74.7)), dot(p, vec3(269.5, 183.3, 246.1)), dot(p, vec3(113.5, 271.9, 124.6)))) * 43758.5453);
}

// Distance to the nearest of one random point per cell (Worley noise): round spots
float cells(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	float best = 8.0;
	for (int x = -1; x <= 1; x++)
	for (int y = -1; y <= 1; y++)
	for (int z = -1; z <= 1; z++) {
		vec3 o = vec3(float(x), float(y), float(z));
		vec3 d = o + hash3(i + o) - f;
		best = min(best, dot(d, d));
	}
	return sqrt(best);
}

// Smooth 3D value noise for lumps and the grain of the clay
float noise(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	vec3 u = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(mix(hash(i), hash(i + vec3(1, 0, 0)), u.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), u.x), u.y),
		mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), u.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), u.x), u.y),
		u.z);
}

void vertex() {
	local_pos = VERTEX;

	// Lumps of hand-kneaded clay and the jelly wobble of the blob
	float lumps = noise(VERTEX * 3.2 + vec3(seed)) - 0.5;
	float jelly = sin(TIME * 5.0 + VERTEX.y * 4.0 + seed) * sin(TIME * 3.7 + VERTEX.x * 3.0 + seed * 0.5);
	VERTEX += NORMAL * (lumps * 0.07 * knead + jelly * wobble * 0.08);

	// Swim wave: grows quadratically towards the tail, see CreatureVisual3D.Bend
	float t = clamp((half_length - local_pos.x) / (2.0 * half_length), 0.0, 1.0);
	float bend = swim_amp * t * t * sin(local_pos.x * 4.0 - swim_phase);
	VERTEX.z += bend * (1.0 - swim_vertical);
	VERTEX.y += bend * swim_vertical;
}

/*
  Paint mask 0..1 of the second color at a point of the body (model space, +X forward):
  - Stripes:  bands across the body, wavy by the grain
  - Spots:    round dots (Worley cells)
  - Patches:  big round blobs like a cow (large Worley cells, the edge wobbled by the grain)
  - Saddle:   the whole back dark, fading down the flanks
  Stripes, spots and patches fade out towards the belly.
*/
float paint(vec3 p, float grain) {
	vec3 q = p * pattern_scale;
	float back = smoothstep(-0.3, 0.12, p.y);
	if (pattern == 1) {
		float band = sin(q.x * 13.0 + grain * 2.5 + q.y * 3.0);
		return smoothstep(0.15, 0.35, band) * back;
	}
	if (pattern == 2) {
		return (1.0 - smoothstep(0.2, 0.28, cells(q * 6.0 + vec3(seed)))) * back;
	}
	if (pattern == 3) {
		float blob = cells(q * 2.4 + vec3(seed * 1.7)) + (grain - 0.5) * 0.12;
		return (1.0 - smoothstep(0.36, 0.42, blob)) * back;
	}
	if (pattern == 4) {
		return smoothstep(-0.02, 0.22, p.y + (grain - 0.5) * 0.12);
	}
	return 0.0;
}

void fragment() {
	float grain = noise(local_pos * 9.0 + vec3(seed));
	float fine = noise(local_pos * 30.0 - vec3(seed));
	// Fingerprint grooves: wavy parallel lines, bent by the grain
	float prints = sin((local_pos.x * 1.3 + local_pos.y + local_pos.z * 0.6) * 55.0 + grain * 7.0) * 0.5 + 0.5;

	vec3 col = clay_color.rgb * (0.9 + 0.18 * grain);
	col = mix(col, pattern_color.rgb * (0.9 + 0.18 * grain), paint(local_pos, grain));

	// Spray paint: soft dots, the rim broken into specks, later dots on top
	for (int i = 0; i < splat_count; i++) {
		vec4 s = splat_pos[i];
		float d = distance(local_pos, s.xyz) / s.w;
		if (d >= 1.2) {
			continue;
		}
		float speck = noise(local_pos * 80.0 + vec3(float(i) * 7.13));
		float cover = 1.0 - smoothstep(0.35, 1.0, d + (speck - 0.5) * 0.55);
		col = mix(col, splat_color[i].rgb * (0.92 + 0.12 * grain), cover);
	}
	col *= 1.0 - knead * (0.07 * prints + 0.07 * fine);

	// Countershading of the living body: darker back, lighter belly
	float belly = 1.0 - smoothstep(-0.25, 0.2, local_pos.y);
	col = mix(col, mix(col * 0.82, col * 1.3 + vec3(0.06), belly), (1.0 - knead) * 0.8);
	col = mix(col, vec3(1.0, 0.35, 0.35), flash * 0.7);

#ifdef CLAY_TRANSLUCENT
	// Cell membrane look, the recipe of membrane.gdshader: see-through and soft in the
	// middle, a glowing outer edge with a thin second line inside it, faint lipid speckle
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.2);
	vec3 tint = mix(clay_color.rgb, vec3(1.0, 0.35, 0.35), flash);
	vec3 light = mix(tint, vec3(1.0), 0.55);
	float inner_line = smoothstep(0.30, 0.38, fres) - smoothstep(0.42, 0.52, fres);
	float outer_edge = smoothstep(0.62, 0.9, fres);
	float speck = smoothstep(0.62, 0.8, noise(local_pos * 18.0 + vec3(TIME * 0.15, 0.0, seed)));
	ALBEDO = tint;
	ROUGHNESS = 0.6;
	SPECULAR = 0.2;
	EMISSION = tint * 0.16
			+ light * fres * 0.35
			+ light * inner_line * 0.35
			+ light * outer_edge * 0.25
			+ light * speck * 0.06
			+ vec3(1.0, 0.3, 0.3) * flash * 0.6;
	ALPHA = clamp(mix(clay_color.a, 1.0, fres) + inner_line * 0.25 + speck * 0.05, 0.0, 1.0);
#else
	// Soft living skin: light wraps around the edges and shines through thin parts
	// (necks, limbs, fins), a soft rim separates the silhouette from the dark water
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.5);
	ALBEDO = col;
	ROUGHNESS = mix(0.4, 0.8, knead);
	SPECULAR = mix(0.5, 0.25, knead);
	RIM = 0.55;
	RIM_TINT = 0.35;
	BACKLIGHT = col * 0.3 + vec3(0.15, 0.05, 0.03);
	EMISSION = col * rim * 0.16 + vec3(1.0, 0.3, 0.3) * flash * 0.35;
#endif
}
