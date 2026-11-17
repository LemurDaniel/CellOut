// NOTE:
//   Curvature of the planet: the world bends down with the squared distance from the
//   camera, so far islands sink behind the horizon. The logic stays flat, only the
//   rendering bends. Globals in project.godot [shader_globals], set every frame by
//   World/Main.cs (UpdateCamera), 0 = flat (the soup). Plane3D.Drop is the same on the CPU.
//
//   Also the shadows of the clouds that drift over the open land (shaders/sky.gdshader
//   draws the clouds): cloud_shadow darkens the albedo of the scenery below a cloud.
//   Globals sky_open and sky_unit are set by Plane3D.SetSky.
//
//   #include "res://shaders/planet.gdshaderinc"
//   VERTEX = planet_bend(VERTEX, MODEL_MATRIX);      // in vertex()
//   ALBEDO *= cloud_shadow(world_pos.xz, TIME);      // in fragment()

global uniform float planet_curve;                // 1 / (2 * planet radius) in 1 / world units
global uniform vec3 planet_origin;                // the camera, the world stays flat around it
global uniform float sky_open;                    // 0 under water .. 1 under the open sky (third person)
global uniform float sky_unit;                    // world units of one Unit of the coast

// The planet of the game (Core/Planets.cs, Planets.Use): every green of the plants turns
// towards its foliage color, water and sand take its tints
global uniform vec4 planet_foliage;               // target hue of the greens
global uniform float planet_foliage_amount;       // 0 keeps the green .. 1 the full planet hue
global uniform vec4 planet_water;                 // multiplier on water colors, white keeps them
global uniform vec4 planet_sand;                  // multiplier on sand colors, white keeps them

// A color whose green part turns towards the foliage of the planet, same brightness;
// browns, greys, blues and teals stay as they are
vec3 planet_green(vec3 c) {
	float green = clamp((c.g - max(c.r, c.b)) / max(c.g, 0.001) * 2.5, 0.0, 1.0);
	float lum = dot(c, vec3(0.3, 0.59, 0.11));
	vec3 target = planet_foliage.rgb * lum / max(dot(planet_foliage.rgb, vec3(0.3, 0.59, 0.11)), 0.001);
	return mix(c, target, green * planet_foliage_amount);
}

const float CLOUD_SHADE = 0.26;                   // share of the light a cloud takes
const float CLOUD_PATCH = 30.0;                   // size of a cloud shadow in Unit

// Hash without sine, see ocean_floor.gdshaderinc
float planet_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float planet_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(planet_hash(i), planet_hash(i + vec2(1.0, 0.0)), u.x),
			mix(planet_hash(i + vec2(0.0, 1.0)), planet_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

// Light left below the drifting clouds at a world point: 1 in the sun, less under a cloud
float cloud_shadow(vec2 world_xz, float time) {
	vec2 p = world_xz / (max(sky_unit, 0.001) * CLOUD_PATCH) + time * vec2(0.012, 0.0054);
	float cover = planet_noise(p) * 0.62 + planet_noise(p * 2.3 + 3.7) * 0.38;
	return 1.0 - CLOUD_SHADE * sky_open * smoothstep(0.5, 0.7, cover);
}

// How far a world point sinks below the flat plane
float planet_drop(vec3 world) {
	vec2 d = world.xz - planet_origin.xz;
	return dot(d, d) * planet_curve;
}

// A model space vertex moved down by the drop of its world position
vec3 planet_bend(vec3 vertex, mat4 model) {
	vec3 world = (model * vec4(vertex, 1.0)).xyz;
	return vertex + inverse(mat3(model)) * vec3(0.0, -planet_drop(world), 0.0);
}
