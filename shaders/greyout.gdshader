// NOTE:
//   Greys out everything drawn below it: desaturates and darkens the screen, blended
//   by amount (0 = untouched, 1 = fully greyed). Used by Ui/CellEditor.cs behind its
//   panel while the game is paused, amount is tweened in and out.

shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear;
uniform float amount : hint_range(0.0, 1.0) = 1.0;
uniform float desaturation : hint_range(0.0, 1.0) = 0.7;
uniform float darken : hint_range(0.0, 1.0) = 0.3;

void fragment() {
	vec3 color = texture(screen_texture, SCREEN_UV).rgb;
	float luma = dot(color, vec3(0.299, 0.587, 0.114));
	vec3 grey = mix(color, vec3(luma), desaturation) * (1.0 - darken);
	COLOR = vec4(mix(color, grey, amount), 1.0);
}
