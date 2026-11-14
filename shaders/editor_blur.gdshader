// NOTE:
//   Backdrop of the village builder: the paused game behind it (the own island with the
//   village), out of focus and dimmed towards a tint, so the panels and the building in
//   front read clearly. A full screen ColorRect carries it (Tribe/Building/Editor/
//   BuildingEditor.cs). One blurred read of the screen from its mipmaps plus four around
//   it for a soft disc, the same cost on every graphics tier and in the OpenGL fallback.
//
//   blur: mip level of the read, 0 sharp. amount: 0 shows the game untouched, 1 the full backdrop.

shader_type canvas_item;

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
uniform float blur = 3.2;
uniform float amount = 1.0;
uniform vec3 tint : source_color = vec3(0.05, 0.12, 0.16);
uniform float tint_share = 0.38;
uniform float vignette = 0.35;

void fragment() {
	float level = blur * amount;
	vec2 spread = SCREEN_PIXEL_SIZE * exp2(level);
	vec3 color = textureLod(screen, SCREEN_UV, level).rgb * 0.4;
	color += textureLod(screen, SCREEN_UV + vec2(spread.x, 0.0), level).rgb * 0.15;
	color += textureLod(screen, SCREEN_UV - vec2(spread.x, 0.0), level).rgb * 0.15;
	color += textureLod(screen, SCREEN_UV + vec2(0.0, spread.y), level).rgb * 0.15;
	color += textureLod(screen, SCREEN_UV - vec2(0.0, spread.y), level).rgb * 0.15;

	// Dimmed towards the tint, darker towards the corners
	float edge = smoothstep(0.35, 0.95, distance(SCREEN_UV, vec2(0.5)) * 1.3);
	color = mix(color, tint, (tint_share + vignette * edge) * amount);
	COLOR = vec4(color, 1.0);
}
