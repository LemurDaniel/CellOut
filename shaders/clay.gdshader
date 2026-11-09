// NOTE:
//   Modelling clay for the organism stage: the kneaded blob of the evolve cutscene and
//   the body of every creature. Fresh clay (knead 1) is lumpy with fingerprint grooves
//   and matt, a living creature (knead low) is smoother, darker on the back and lighter
//   on the belly (countershading). The swim wave bends the body in model space, the
//   same formula as CreatureVisual3D.Bend, so the parts stay attached.
//   Set by World/Cutscenes/EvolveCutscene.cs and Cells/CreatureVisual3D.cs.

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley;

uniform vec4 clay_color : source_color = vec4(0.8, 0.55, 0.45, 1.0);
uniform float seed = 0.0;
uniform float knead = 1.0;           // 1 fresh clay, 0 smooth living skin
uniform float wobble = 0.0;          // jelly wobble of the cutscene blob
uniform float flash = 0.0;           // hit flash 0..1
uniform float swim_phase = 0.0;      // runs faster while swimming
uniform float swim_amp = 0.0;        // wave amplitude at the tail in model units
uniform float swim_vertical = 0.0;   // 0 fish (side to side), 1 marine mammal (up and down)
uniform float half_length = 0.7;     // half the vertebra span, the wave grows towards the tail

varying vec3 local_pos;

float hash(vec3 p) {
	return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
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

void fragment() {
	float grain = noise(local_pos * 9.0 + vec3(seed));
	float fine = noise(local_pos * 30.0 - vec3(seed));
	// Fingerprint grooves: wavy parallel lines, bent by the grain
	float prints = sin((local_pos.x * 1.3 + local_pos.y + local_pos.z * 0.6) * 55.0 + grain * 7.0) * 0.5 + 0.5;

	vec3 col = clay_color.rgb * (0.9 + 0.18 * grain);
	col *= 1.0 - knead * (0.07 * prints + 0.07 * fine);

	// Countershading of the living body: darker back, lighter belly
	float belly = 1.0 - smoothstep(-0.25, 0.2, local_pos.y);
	col = mix(col, mix(col * 0.82, col * 1.3 + vec3(0.06), belly), (1.0 - knead) * 0.8);
	col = mix(col, vec3(1.0, 0.35, 0.35), flash * 0.7);

	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	ALBEDO = col;
	ROUGHNESS = mix(0.45, 0.85, knead);
	SPECULAR = mix(0.5, 0.25, knead);
	EMISSION = col * rim * 0.22 + vec3(1.0, 0.3, 0.3) * flash * 0.35;
}
