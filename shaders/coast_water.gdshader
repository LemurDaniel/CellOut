// NOTE:
//   Water surface over the sea next to the coast: a faint tint that thins out towards the
//   wobbling water line, wave crests rolling in to the shore, broken by noise, and a
//   foam fringe that follows the swash. Same water line as coast_ground.gdshader.
//   Set by World/Coast.cs.

shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

#include "res://shaders/ocean_floor.gdshaderinc"

uniform vec2 coast_origin = vec2(0.0);
uniform vec2 coast_normal = vec2(1.0, 0.0);
uniform float unit = 14.0;
uniform vec4 water_color : source_color = vec4(0.25, 0.6, 0.7, 0.18);

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 rel = world_pos.xz - coast_origin;
	float d = dot(rel, coast_normal);
	float along = dot(rel, vec2(-coast_normal.y, coast_normal.x));
	float to_shore = coast_shoreline(along, unit, TIME) - d;    // > 0 out at sea

	// Tint thins out on the last stretch to the water line
	float body = smoothstep(0.0, 1.5 * unit, to_shore);

	// Wave crests rolling towards the shore, only close to it, broken into pieces
	float wave = sin(d / unit * 2.2 - TIME * 1.6 + coast_fbm(vec2(along / unit * 0.2, TIME * 0.05)) * 4.0);
	float broken = smoothstep(0.35, 0.65, coast_fbm(vec2(along / unit * 0.6, d / unit * 0.6 - TIME * 0.3)));
	float crests = smoothstep(0.86, 1.0, wave) * broken * (1.0 - smoothstep(0.0, 8.0 * unit, to_shore));

	// Foam fringe right at the water line
	float foam = 1.0 - smoothstep(0.0, 0.35 * unit, abs(to_shore - 0.1 * unit));
	foam *= 0.55 + 0.45 * coast_fbm(vec2(along / unit * 1.5, TIME * 0.4));

	float in_water = step(-0.4 * unit, to_shore);
	ALBEDO = mix(water_color.rgb, vec3(1.0), clamp(crests + foam, 0.0, 1.0));
	ALPHA = clamp((water_color.a * body + crests * 0.3 + foam * 0.75) * in_water, 0.0, 1.0);
}
