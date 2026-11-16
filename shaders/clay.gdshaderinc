// NOTE:
//   Shared body of the modelling clay, included by clay.gdshader (opaque, soft skin) and
//   clay_translucent.gdshader (see-through cell look, CLAY_TRANSLUCENT). See clay.gdshader for the look.
//   Paint: a second color laid on as a pattern (stripes, spots, patches, saddle), on the
//   back and flanks, the belly stays light (CreatureDesign.Pattern). On top the spray paint:
//   up to 160 dots (CreatureDesign.MAX_SPLATS) with a speckled rim like a spray can.
//   Under the open sky of the land the skin is matt and glows less (sky_open).
//   Parts and limbs sharing this clay hand in to_body: their paint continues the pattern of the body.
//   The skin surface (CreatureDesign.Skin): smooth clay, scales or warts, under the paint. Scales and
//   warts are domes in relief: their slope tilts the normal like a bump map, the light models them.
//   The mouth opening is cut into the body mesh (Visual/CreatureClay.cs), the line and its dark cavity
//   are drawn here per pixel from the mark the mesh carries in its UV2.
global uniform float sky_open;       // 0 under water .. 1 under the open sky, set by Plane3D.SetSky

uniform vec4 clay_color : source_color = vec4(0.8, 0.55, 0.45, 1.0);
uniform float seed = 0.0;
uniform float knead = 1.0;           // 1 fresh clay, 0 smooth living skin
uniform float wobble = 0.0;          // jelly wobble of the cutscene blob
uniform float flash = 0.0;           // hit flash 0..1
uniform float swim_phase = 0.0;      // runs faster while swimming
uniform float swim_amp = 0.0;        // wave amplitude at the tail in model units
uniform float swim_vertical = 0.0;   // 0 fish (side to side), 1 marine mammal (up and down)
uniform float half_length = 0.7;     // half the vertebra span, the wave grows towards the tail
uniform int pattern = 0;             // 0 none, 1 stripes, 2 spots, 3 patches, 4 saddle, 5 net (CreaturePattern)
uniform vec4 pattern_color : source_color = vec4(0.3, 0.2, 0.2, 1.0);
uniform float pattern_scale = 1.0;
uniform float pattern_wrap = 0.0;     // 1: the pattern runs all around, belly and legs too (CreatureDesign.PatternWraps)
uniform vec4 belly_color : source_color = vec4(0.0);   // third color on the underside, alpha 0 = none (CreatureDesign.BellyColor)
uniform vec4 head_color : source_color = vec4(0.0);    // color zone of the head, alpha 0 = none
uniform vec4 tail_color : source_color = vec4(0.0);    // color zone of the tail end
uniform float head_from = 9.0;       // X in body space where the head zone begins
uniform float tail_to = -9.0;        // X where the tail zone ends
uniform mat4 to_body = mat4(1.0);    // from this mesh into the space of the body: parts and limbs continue the paint of the body
uniform float body_lift = 0.0;       // height of the spine where a part sits (the body mesh carries it per vertex instead)
uniform bool is_body = true;         // false on parts and limbs: the plates only step the body itself
uniform int skin = 0;                // 0 smooth, 1 scales of a reptile, 2 warts of a toad, 3 hard plates across the body, 4 feathers, 5 fur, 6 wrinkles (CreatureSkin)
uniform vec4 tip_color : source_color = vec4(0.0);   // limb tubes: a plain color from a joint outwards, alpha 0 = none (CreaturePart.TipColor)
uniform float tip_from = 2.0;        // share along the tube (UV.x, 0 shoulder .. 1 tip) where that color begins
uniform int splat_count = 0;
uniform vec4 splat_pos[160];         // xyz center in model space, w radius
uniform vec4 splat_color[160];
uniform float mouth_gape = -1.0;     // half the opening at the nose in heights of the head, negative = no mouth (MouthShape.Gape)
uniform float mouth_min_gape = 0.045; // MouthShape.MIN_GAPE

varying vec3 local_pos;
varying vec3 paint_pos;              // the same point in the space of the body, for pattern, belly and countershading
varying float spine_lift;            // height of the spine at this ring, back and belly are measured from it (Visual/CreatureClay.cs, vertex color)
varying vec2 mouth_mark;             // x = 0 at the mouth corners .. 1 at the nose tip, y = below the mouth line in heights of the head

const vec3 MAW_COLOR = vec3(0.13, 0.03, 0.05);
const float LIFT_RANGE = 2.0;        // CreatureClay.LIFT_RANGE
const float LIP_RIM = 0.03;          // width of the pink rim in heights of the head
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

// Distance to the border between the two nearest cells (second nearest minus nearest point):
// 0 on the border, growing into the cell. For patterns of fields with lines between them
float cell_border(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	float first = 8.0;
	float second = 8.0;
	for (int x = -1; x <= 1; x++)
	for (int y = -1; y <= 1; y++)
	for (int z = -1; z <= 1; z++) {
		vec3 o = vec3(float(x), float(y), float(z));
		vec3 d = o + hash3(i + o) - f;
		float distance_to = length(d);
		if (distance_to < first) {
			second = first;
			first = distance_to;
		} else if (distance_to < second) {
			second = distance_to;
		}
	}
	return second - first;
}

// Like cells, but with the way to that nearest point: xyz from p to the point, w the distance.
// The distance grows straight away from the point, so a bump over the cell needs no second sample
vec4 cell_offset(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	vec3 nearest = vec3(8.0);
	float best = 64.0;
	for (int x = -1; x <= 1; x++)
	for (int y = -1; y <= 1; y++)
	for (int z = -1; z <= 1; z++) {
		vec3 o = vec3(float(x), float(y), float(z));
		vec3 d = o + hash3(i + o) - f;
		float square = dot(d, d);
		if (square < best) {
			best = square;
			nearest = d;
		}
	}
	return vec4(nearest, sqrt(best));
}

const float PLATE_COUNT = 4.5;       // plates per unit of body length
const float PLATE_STEP = 0.035;      // how far a plate stands out over the groove behind it

// Height 0..1 of the plate skin along the body: every plate rises fast at its front edge,
// stays up and drops into a narrow groove before the next one (roof tiles seen from the side)
float plate_height(float x) {
	float along = fract(x * PLATE_COUNT);
	return smoothstep(0.0, 0.12, along) * (1.0 - smoothstep(0.78, 1.0, along));
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
	paint_pos = (to_body * vec4(VERTEX, 1.0)).xyz;
	spine_lift = (COLOR.g - COLOR.r) * LIFT_RANGE;     // meshes without colors (limbs, fillets) are white: 0
	mouth_mark = UV2;                   // meshes without UV2 (limbs, fillets, knuckles) carry 0: plain skin

	// Lumps of hand-kneaded clay and the jelly wobble of the blob
	float lumps = noise(VERTEX * 3.2 + vec3(seed)) - 0.5;
	float jelly = sin(TIME * 5.0 + VERTEX.y * 4.0 + seed) * sin(TIME * 3.7 + VERTEX.x * 3.0 + seed * 0.5);
	VERTEX += NORMAL * (lumps * 0.07 * knead + jelly * wobble * 0.08);

	// Plates: a real step in the silhouette, the body mesh is fine enough along its length
	if (skin == 3 && is_body) {
		VERTEX += NORMAL * (plate_height(local_pos.x) - 0.6) * PLATE_STEP;
	}

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
  - Net:      big dark fields, thin light lines between them
  Stripes, spots, patches and the net fade out towards the belly, unless the pattern wraps all around (pattern_wrap).
*/
float paint(vec3 p, float height, float grain) {
	// The marks are laid out in real space (a steep neck must not smear them), only back and belly count from the spine
	vec3 q = p * pattern_scale;
	float back = mix(smoothstep(-0.3, 0.12, height), 1.0, pattern_wrap);
	if (pattern == 5) {
		// Net: big dark fields with thin light lines between them (a giraffe), the lines a little ragged
		float line = cell_border(q * 2.2 + vec3(seed * 1.3)) + (grain - 0.5) * 0.05;
		return smoothstep(0.05, 0.11, line) * back;
	}
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
		return smoothstep(-0.02, 0.22, height + (grain - 0.5) * 0.12);
	}
	return 0.0;
}

void fragment() {
	float grain = noise(local_pos * 9.0 + vec3(seed));
	float fine = noise(local_pos * 30.0 - vec3(seed));
	// Fingerprint grooves: wavy parallel lines, bent by the grain
	float prints = sin((local_pos.x * 1.3 + local_pos.y + local_pos.z * 0.6) * 55.0 + grain * 7.0) * 0.5 + 0.5;

	// Back and belly count from the spine: a lifted neck or a lowered body keeps its paint
	vec3 body_pos = vec3(paint_pos.x, paint_pos.y - spine_lift - body_lift, paint_pos.z);

	vec3 col = clay_color.rgb * (0.9 + 0.18 * grain);
	col = mix(col, pattern_color.rgb * (0.9 + 0.18 * grain), paint(paint_pos, body_pos.y, grain));

	// A belly of its own color, fading up the flanks
	float under = (1.0 - smoothstep(-0.3, -0.04, body_pos.y + (grain - 0.5) * 0.06)) * belly_color.a;
	col = mix(col, belly_color.rgb * (0.9 + 0.18 * grain), under);

	// Head and tail end in a color of their own, the border a little ragged
	float zone_edge = (grain - 0.5) * 0.08;
	col = mix(col, head_color.rgb * (0.9 + 0.18 * grain), smoothstep(head_from - 0.05, head_from + 0.05, paint_pos.x + zone_edge) * head_color.a);
	col = mix(col, tail_color.rgb * (0.9 + 0.18 * grain), (1.0 - smoothstep(tail_to - 0.05, tail_to + 0.05, paint_pos.x + zone_edge)) * tail_color.a);

	// A limb in a second color from a joint outwards (white lower legs): plain, over pattern and zones
	col = mix(col, tip_color.rgb * (0.9 + 0.18 * grain), smoothstep(tip_from - 0.015, tip_from + 0.015, UV.x + zone_edge * 0.25) * tip_color.a);

	// Skin surface, under the spray paint: the paint lies on the scales
	// Every scale and wart is a dome: the light sees its slope (bump), the color only follows
	float sheen = 0.0;                  // domes catch a little more light
	vec3 bump = vec3(0.0);              // slope of the skin relief in model space, times its strength
	if (skin == 1) {
		// Scales: one dome per cell with a groove between them, a little longer along the body
		vec3 stretch = vec3(13.0, 17.0, 17.0);
		vec4 cell = cell_offset(local_pos * stretch + vec3(seed));
		float t = clamp((cell.w - 0.12) / 0.4, 0.0, 1.0);          // 0 on the dome .. 1 in the groove
		float groove = t * t * (3.0 - 2.0 * t);
		col *= mix(1.08, 0.7, groove);
		sheen = (1.0 - groove) * 0.5;
		bump = cell.xyz / max(cell.w, 0.001) * stretch * (6.0 * t * (1.0 - t)) * 0.05;
	} else if (skin == 2) {
		// Warts: scattered bumps with a dark ring around each
		vec4 cell = cell_offset(local_pos * 10.0 + vec3(seed * 2.0));
		float t = clamp((cell.w - 0.06) / 0.18, 0.0, 1.0);         // 0 on the wart .. 1 on the skin around it
		float wart = 1.0 - t * t * (3.0 - 2.0 * t);
		float ring = smoothstep(0.18, 0.24, cell.w) * (1.0 - smoothstep(0.24, 0.34, cell.w));
		col = mix(col, col * 1.18 + vec3(0.03), wart) * (1.0 - 0.2 * ring);
		sheen = wart * 0.4;
		bump = cell.xyz / max(cell.w, 0.001) * (6.0 * t * (1.0 - t)) * 0.75;
	}
	if (skin == 4) {
		// Feathers: long soft domes lying back along the body, each with a darker tip and a faint shaft
		vec3 stretch = vec3(7.0, 15.0, 15.0);
		vec4 cell = cell_offset(local_pos * stretch + vec3(seed));
		float t = clamp((cell.w - 0.1) / 0.5, 0.0, 1.0);
		float shaft = 1.0 - smoothstep(0.0, 0.05, abs(cell.y + cell.z) * 0.5);
		col *= mix(1.1, 0.78, t * t) * (1.0 - 0.08 * shaft * (1.0 - t));
		sheen = (1.0 - t) * 0.25;
		bump = cell.xyz / max(cell.w, 0.001) * stretch * (6.0 * t * (1.0 - t)) * 0.03;
	} else if (skin == 5) {
		// Fur: fine streaks running back along the body, a soft uneven shade, no shine
		float streak = noise(local_pos * vec3(9.0, 70.0, 70.0) + vec3(seed));
		float tuft = noise(local_pos * 14.0 - vec3(seed));
		col *= 0.8 + 0.3 * streak + 0.1 * tuft;
		sheen = -0.6;
		bump = vec3(0.0, streak - 0.5, tuft - 0.5) * 0.35;
	}
	if (skin == 6) {
		// Wrinkles: folds running around body and limbs like the hide of an elephant, a fine crackle between them.
		// A fold is the thin line where a noise stretched along the body crosses its middle
		vec3 stretch = vec3(30.0, 5.0, 5.0);
		float fold = noise(local_pos * stretch + vec3(seed));
		float ahead = noise((local_pos + vec3(0.004, 0.0, 0.0)) * stretch + vec3(seed));
		float crease = 1.0 - smoothstep(0.0, 0.1, abs(fold - 0.5));
		vec4 cell = cell_offset(local_pos * 24.0 + vec3(seed * 3.0));
		float crack = smoothstep(0.36, 0.5, cell.w);
		col *= 1.0 - 0.3 * crease - 0.14 * crack;
		sheen = -0.5;                    // dry hide, no shine
		bump = vec3((ahead - fold) / 0.004 * 0.02 * crease, 0.0, 0.0) + cell.xyz * crack * 0.25;
	}
	if (skin == 3 && is_body) {
		// Plates: dark in the groove, lit on the rising edge, the slope runs along the body
		float h = plate_height(local_pos.x);
		float ahead = plate_height(local_pos.x + 0.01);
		col *= mix(0.6, 1.06, h);
		sheen = h * 0.45;
		bump = vec3((ahead - h) / 0.01 * PLATE_STEP * 0.6, 0.0, 0.0);
	}
	if (skin != 0) {
		// Tilt the normal against the slope, along the surface only (NORMAL is in view space)
		vec3 slope = (VIEW_MATRIX * MODEL_MATRIX * vec4(bump, 0.0)).xyz / max(length(MODEL_MATRIX[0].xyz), 0.0001);
		NORMAL = normalize(NORMAL - (slope - dot(slope, NORMAL) * NORMAL));
	}

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

	// The mouth cavity: dark red-black inside, a pink rim where the lips turn in
	float in_maw = 0.0;                 // the cavity swallows the light: no rim, no backlight, no shine
	if (mouth_gape >= 0.0 && mouth_mark.x > 0.0) {
		float gape = max(mouth_gape * smoothstep(0.0, 0.3, mouth_mark.x), mouth_min_gape);
		float off = abs(mouth_mark.y);
		float ends = smoothstep(0.0, 0.08, mouth_mark.x);      // the line thins out into the corners
		// The maw fills the opening, the pink rim around it keeps its width however wide the mouth gapes
		float maw = (1.0 - smoothstep(gape - min(0.02, gape * 0.4), gape, off)) * ends;
		float lips = (1.0 - smoothstep(gape, gape + LIP_RIM, off)) * ends * (1.0 - maw);
		col = mix(col, mix(col, vec3(0.85, 0.45, 0.5), 0.45), lips);
		col = mix(col, MAW_COLOR, maw);
		in_maw = maw;
	}

	// Countershading of the living body: darker back, lighter belly
	float belly = 1.0 - smoothstep(-0.25, 0.2, body_pos.y);
	// Only on and near the body: a leg or a foot far below it is no belly, it keeps the plain tone (feet rendered pale)
	float near_body = is_body ? 1.0 : 1.0 - smoothstep(0.35, 0.8, -body_pos.y);
	col = mix(col, mix(col * 0.82, col * 1.3 + vec3(0.06), belly), (1.0 - knead) * 0.8 * (1.0 - in_maw) * near_body);
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
	// (necks, limbs, fins), a soft rim separates the silhouette from the dark water.
	// Under the open sky (sky_open) the strong sun would make that glow garish: there the
	// skin is matt with fine pores and the glow of rim and backlight is taken back, so the
	// body sits in the landscape instead of shining out of it. The color itself stays, the
	// limbs and fins (own materials) carry the same one
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.5);
	float pores = noise(local_pos * 60.0 + vec3(seed * 3.0));
	col *= 1.0 - 0.1 * smoothstep(0.45, 0.8, pores) * sky_open;
	ALBEDO = col;
	ROUGHNESS = mix(mix(0.4, 0.72, sky_open), 0.8, knead) * (1.0 - 0.35 * sheen);
	SPECULAR = mix(mix(0.5, 0.28, sky_open), 0.25, knead) * (1.0 - in_maw);
	RIM = mix(0.55, 0.2, sky_open) * (1.0 - in_maw);
	RIM_TINT = 0.35;
	BACKLIGHT = mix(col * 0.3 + vec3(0.15, 0.05, 0.03), col * 0.1, sky_open) * (1.0 - in_maw);
	EMISSION = col * rim * 0.16 * (1.0 - 0.85 * sky_open) * (1.0 - in_maw) + vec3(1.0, 0.3, 0.3) * flash * 0.35;
#endif
}
