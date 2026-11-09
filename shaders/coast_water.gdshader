// NOTE:
//   Water surface around the islands: a faint tint that thins out towards the wobbling
//   water line, wave crests rolling in to every shore, broken by noise, and a foam
//   fringe that follows the swash. Same water line as coast_ground.gdshader.
//   open_sea (0 top view .. 1 third person) turns the tint into a real sea: blue, more
//   opaque at grazing angles (fresnel) and towards the horizon, clearer right around the
//   player, so a swimming body stays visible. Bends with the planet.
//   Set by World/Coast.cs.

shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

#include "res://shaders/ocean_floor.gdshaderinc"
#include "res://shaders/planet.gdshaderinc"

uniform float unit = 14.0;
uniform vec4 water_color : source_color = vec4(0.25, 0.6, 0.7, 0.18);
uniform float open_sea = 0.0;
uniform vec3 sea_color : source_color = vec3(0.07, 0.3, 0.42);
uniform vec3 sky_tint : source_color = vec3(0.62, 0.8, 0.92);
uniform vec2 focus = vec2(0.0);              // player on the plane
uniform float focus_size = 14.0;             // player size

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX = planet_bend(VERTEX, MODEL_MATRIX);
}

void fragment() {
	float d = island_depth(world_pos.xz);
	vec2 q = world_pos.xz / unit;
	float to_shore = coast_shoreline(world_pos.xz, unit, TIME) - d;    // > 0 out at sea

	// Tint thins out on the last stretch to the water line
	float body = smoothstep(0.0, 1.5 * unit, to_shore);

	// Wave crests rolling towards every shore, only close to it, broken into pieces
	float wave = sin(d / unit * 2.2 - TIME * 1.6 + coast_fbm(q * 0.2 + TIME * 0.05) * 4.0);
	float broken = smoothstep(0.35, 0.65, coast_fbm(q * 0.6 + vec2(0.0, -TIME * 0.3)));
	float crests = smoothstep(0.86, 1.0, wave) * broken * (1.0 - smoothstep(0.0, 8.0 * unit, to_shore));

	// Foam fringe right at the water line
	float foam = 1.0 - smoothstep(0.0, 0.35 * unit, abs(to_shore - 0.1 * unit));
	foam *= 0.55 + 0.45 * coast_fbm(q * 1.5 + TIME * 0.4);

	// Open sea: fresnel to the sky tint, clearer around the player, small glints
	float grazing = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.5);
	float around = 1.0 - smoothstep(focus_size * 1.5, focus_size * 6.0, distance(world_pos.xz, focus));
	float glint = smoothstep(0.78, 0.95, coast_noise(q * 2.5 + vec2(TIME * 0.3, 0.0)) * coast_noise(q * 3.1 - vec2(0.0, TIME * 0.25) + 4.0) * 1.6);
	vec3 sea = mix(sea_color, sky_tint, grazing * 0.7) + glint * 0.35;
	float sea_alpha = mix(0.45, 0.97, grazing) * (1.0 - 0.75 * around) * body;

	float in_water = step(-0.4 * unit, to_shore);
	vec3 tint = mix(water_color.rgb, sea, open_sea);
	float tint_alpha = mix(water_color.a * body, sea_alpha, open_sea);
	ALBEDO = mix(tint, vec3(1.0), clamp(crests + foam, 0.0, 1.0));
	ALPHA = clamp((tint_alpha + crests * 0.3 + foam * 0.75) * in_water, 0.0, 1.0);
}
