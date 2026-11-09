// NOTE:
//   Sea floor of the primordial soup: soft patches, moving caustics and an edge fade.
//   The pattern runs in world coordinates / world_scale, so it grows with the player.
//   Pattern from ocean_floor.gdshaderinc, the coast ground fades into the same one.
//   Bends with the planet. Set per frame by World/Backdrop.cs (world_scale).

shader_type spatial;
render_mode cull_disabled;

#include "res://shaders/ocean_floor.gdshaderinc"
#include "res://shaders/planet.gdshaderinc"

uniform vec3 shallow_color : source_color = vec3(0.10, 0.32, 0.34);
uniform vec3 deep_color : source_color = vec3(0.03, 0.11, 0.15);
uniform vec3 fog_color : source_color = vec3(0.04, 0.13, 0.17);
uniform float caustic_strength = 0.12;
uniform float world_scale = 100.0;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX = planet_bend(VERTEX, MODEL_MATRIX);
}

void fragment() {
	float caustic;
	vec3 col = ocean_floor_color(world_pos.xz / world_scale, TIME, deep_color, shallow_color, caustic);
	float edge = smoothstep(0.5, 0.15, distance(UV, vec2(0.5)));
	ALBEDO = mix(fog_color, col, edge);
	EMISSION = vec3(0.4, 0.9, 0.8) * caustic * caustic_strength * edge;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
