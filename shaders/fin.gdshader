// NOTE:
//   Membrane of fins and wings: the body color at the root fading to a light edge, thin
//   painted rays fanning out from the root (bat wings; the fin models bring real rays,
//   feathers none), see-through and lit from behind like real skin. Two sided. Set by
//   Core/PartModels.cs (Membrane) for the fin and wing models.

shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_disabled, diffuse_burley;

uniform vec4 base_color : source_color = vec4(0.8, 0.55, 0.45, 1.0);
uniform vec4 edge_color : source_color = vec4(1.0, 0.9, 0.85, 1.0);
uniform int plane = 0;           // 0 the membrane lies in XY, 1 in XZ (wings), 2 any (fin models: distance from the root in 3D)
uniform float reach = 0.45;      // distance from the root where the edge color is full, model units
uniform float rays = 22.0;       // rays per full turn around the root, 0 for none
uniform float opacity = 0.85;

varying vec3 local_pos;

void vertex() {
	local_pos = VERTEX;
}

void fragment() {
	vec2 p = plane == 1 ? local_pos.xz : local_pos.xy;
	float r = plane == 2 ? length(local_pos) : length(p);

	// Root color to edge color, the rays a little darker
	float edge = smoothstep(reach * 0.2, reach, r);
	vec3 col = mix(base_color.rgb, edge_color.rgb, edge);
	float ray = rays > 0.0 ? smoothstep(0.7, 1.0, sin(atan(p.y, p.x) * rays)) * smoothstep(0.05, 0.16, r) : 0.0;
	col *= 1.0 - ray * 0.28;

	float fres = pow(1.0 - abs(dot(NORMAL, VIEW)), 2.0);
	ALBEDO = col;
	ROUGHNESS = 0.45;
	SPECULAR = 0.45;
	BACKLIGHT = col * 0.7;
	EMISSION = edge_color.rgb * fres * 0.12;
	ALPHA = base_color.a * opacity * mix(1.0, 0.75, edge);
}
