// NOTE:
//   Curvature of the planet: the world bends down with the squared distance from the
//   camera, so far islands sink behind the horizon. The logic stays flat, only the
//   rendering bends. Globals in project.godot [shader_globals], set every frame by
//   World/Main.cs (UpdateCamera), 0 = flat (the soup). Plane3D.Drop is the same on the CPU.
//
//   #include "res://shaders/planet.gdshaderinc"
//   VERTEX = planet_bend(VERTEX, MODEL_MATRIX);      // in vertex()

global uniform float planet_curve;                // 1 / (2 * planet radius) in 1 / world units
global uniform vec3 planet_origin;                // the camera, the world stays flat around it

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
