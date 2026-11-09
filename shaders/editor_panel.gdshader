// NOTE:
//   Background of the cell editor panel (Ui/Editor/CellEditor.cs): deep water with
//   drifting light, soft organelle blobs and rising bubbles. The right edge is a
//   wobbling, glowing membrane, everything outside it stays transparent.
//   rect_size is set by the editor whenever the panel is resized, the last edge_px
//   of the rect are reserved for the wobble. tint_color / tint_amount give the whole
//   panel a hint of the player's diet (green herbivore, red carnivore), set by the editor.

shader_type canvas_item;

uniform vec4 deep_color : source_color = vec4(0.03, 0.045, 0.085, 1.0);
uniform vec4 shallow_color : source_color = vec4(0.09, 0.12, 0.2, 1.0);
uniform vec4 rim_color : source_color = vec4(0.72, 0.82, 0.96, 1.0);
uniform vec4 tint_color : source_color = vec4(1.0);
uniform float tint_amount : hint_range(0.0, 1.0) = 0.0;
uniform vec2 rect_size = vec2(800.0, 720.0);
uniform float edge_px = 40.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

// Soft blob per grid cell, drifting slowly: the organelle haze in the back
float blobs(vec2 px, float cell_px, float speed) {
	vec2 p = px / cell_px + vec2(0.0, TIME * speed);
	vec2 id = floor(p);
	vec2 center = vec2(hash(id), hash(id + 7.3)) * 0.6 + 0.2;
	center += 0.12 * vec2(sin(TIME * 0.6 + hash(id) * 6.0), cos(TIME * 0.5 + hash(id + 3.1) * 6.0));
	float radius = 0.12 + 0.18 * hash(id + 1.7);
	return smoothstep(radius, radius * 0.2, length(fract(p) - center)) * step(0.45, hash(id + 9.1));
}

// Thin bubble rings rising upwards
float bubbles(vec2 px) {
	vec2 p = px / 70.0 + vec2(0.0, TIME * 0.35);
	vec2 id = floor(p);
	vec2 center = vec2(hash(id + 2.2), hash(id + 5.5)) * 0.6 + 0.2;
	center.x += 0.08 * sin(TIME * 1.5 + hash(id) * 6.0);
	float radius = 0.05 + 0.07 * hash(id + 4.4);
	float d = length(fract(p) - center);
	float ring = smoothstep(0.025, 0.0, abs(d - radius)) + smoothstep(radius * 0.5, 0.0, length(fract(p) - center + radius * 0.35)) * 0.6;
	return ring * step(0.7, hash(id + 8.8));
}

void fragment() {
	vec2 px = UV * rect_size;

	// Diet hint: water and rim lean towards the tint
	vec3 deep = mix(deep_color.rgb, tint_color.rgb * 0.16, tint_amount);
	vec3 shallow = mix(shallow_color.rgb, tint_color.rgb * 0.32, tint_amount);
	vec3 rim_tint = mix(rim_color.rgb, tint_color.rgb, clamp(tint_amount * 1.6, 0.0, 1.0));

	// Membrane edge: two waves running along the right side
	float wobble = sin(px.y * 0.017 + TIME * 1.3) * 8.0 + sin(px.y * 0.041 - TIME * 0.9) * 4.0;
	float edge = rect_size.x - edge_px * 0.5 + wobble;
	float inside = edge - px.x;

	// Water: gradient, drifting light and caustic streaks
	float n = noise(px * 0.005 + vec2(TIME * 0.04, -TIME * 0.03));
	vec3 color = mix(deep, shallow, clamp(0.15 + UV.y * 0.35 + n * 0.45, 0.0, 1.0));
	float caustic = pow(abs(sin((px.x + px.y * 0.6) * 0.018 + n * 6.0 + TIME * 0.35)), 14.0);
	color += rim_tint * caustic * 0.035;

	// Organelle haze and bubbles
	color = mix(color, shallow * 1.6, blobs(px, 160.0, 0.025) * 0.18);
	color = mix(color, rim_tint * 0.6, blobs(px + 80.0, 110.0, 0.04) * 0.08);
	color += rim_tint * bubbles(px) * 0.12;

	// Vignette towards the left and the corners
	color *= mix(0.75, 1.0, smoothstep(0.0, 0.35, UV.x)) * mix(0.8, 1.0, sin(UV.y * 3.14159));

	// Glowing membrane rim with a soft glow inwards
	float rim = smoothstep(4.0, 0.0, abs(inside - 3.0));
	float glow = exp(-max(inside, 0.0) / 22.0);
	color = mix(color, rim_tint, rim * 0.85) + rim_tint * glow * 0.18;

	COLOR = vec4(color, smoothstep(-1.5, 0.5, inside));
}
