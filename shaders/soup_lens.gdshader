// NOTE:
//   The lens over the soup: slanted light shafts falling in from the top left and a dark
//   vignette, like looking through a microscope into lit water. One full-screen pass
//   without reading the screen: premultiplied alpha adds the light (rgb) and darkens
//   the rim (alpha) at once, so it costs the same on every graphics tier.
//   Set per frame by Game/Water/SoupLens.cs (strength, light_color, drift).

shader_type canvas_item;
render_mode blend_premul_alpha, unshaded;

uniform float strength = 1.0;                // 1 under water .. 0 in the open air
uniform vec3 light_color : source_color = vec3(0.7, 0.95, 0.9);
uniform float shaft_strength = 0.06;
uniform float vignette_strength = 0.55;
uniform float drift = 0.0;                   // the shafts slide a little when the view moves

// Hash without sin(), sin() gives blocky patterns on some GPUs
float hash(float n) {
	n = fract(n * 0.1031);
	n *= n + 33.33;
	n *= n + n;
	return fract(n);
}

// Smooth 1D value noise
float noise(float x) {
	float i = floor(x);
	float f = fract(x);
	float u = f * f * (3.0 - 2.0 * f);
	return mix(hash(i), hash(i + 1.0), u);
}

void fragment() {
	// Shafts: a few wide, soft bands along a slanted axis, drifting slowly. Kept calm on
	// purpose: the sea floor already carries moving caustics
	float across = UV.x * 1.25 + UV.y * 0.55 + drift;
	float shafts = smoothstep(0.5, 1.0, noise(across * 2.2 + TIME * 0.025));
	// They fade towards the bottom, where the light has travelled deeper
	float reach = smoothstep(1.25, 0.0, UV.y + UV.x * 0.25);
	vec3 light = light_color * shafts * reach * shaft_strength * strength;

	// Vignette: darker towards the corners, a little flatter than the screen
	vec2 from_center = (UV - vec2(0.5)) * vec2(1.0, 0.85);
	float rim = smoothstep(0.38, 0.85, length(from_center));
	float shade = rim * rim * vignette_strength * mix(0.45, 1.0, strength);

	COLOR = vec4(light * (1.0 - shade), shade);
}
