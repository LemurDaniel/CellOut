// NOTE:
//   Sky over the islands, from the land animal on: a gradient from the horizon to the
//   zenith, a warm band of haze right above the horizon, the sun as a disc with a wide
//   glow (the direction of the sun light), and puffy clouds drifting on a layer above,
//   lit from the sun side with a shaded belly, thin cirrus streaks high above them.
//   Below the horizon the color of the sea.
//   amount (0 soup .. 1 open sky) fades clouds, sun and haze in while the camera rises
//   out of the water, the colors themselves are blended by the camera.
//   Not a screen-space effect: looks the same on every graphics tier.
//   Set by Game/World/FollowCamera.cs (UpdateSky).

shader_type sky;

uniform vec3 top_color : source_color = vec3(0.24, 0.5, 0.86);
uniform vec3 horizon_color : source_color = vec3(0.7, 0.84, 0.95);
uniform vec3 below_color : source_color = vec3(0.07, 0.3, 0.42);
uniform vec3 haze_color : source_color = vec3(1.0, 0.93, 0.8);
uniform vec3 sun_color : source_color = vec3(1.0, 0.95, 0.8);
uniform vec3 cloud_light : source_color = vec3(1.0, 0.99, 0.96);
uniform vec3 cloud_shade : source_color = vec3(0.62, 0.72, 0.86);
uniform float cloud_cover = 0.46;            // share of the sky the clouds take
uniform float cloud_speed = 0.006;           // drift of the layer per second
uniform float amount = 1.0;

// Hash without sine, see ocean_floor.gdshaderinc
float sky_hash(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float sky_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(sky_hash(i), sky_hash(i + vec2(1.0, 0.0)), u.x),
			mix(sky_hash(i + vec2(0.0, 1.0)), sky_hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

// Four octaves, 0..1
float sky_fbm(vec2 p) {
	return sky_noise(p) * 0.5 + sky_noise(p * 2.03 + 4.1) * 0.27 + sky_noise(p * 4.1 - 1.9) * 0.15 + sky_noise(p * 8.3 + 7.7) * 0.08;
}

void sky() {
	float up = EYEDIR.y;

	// Gradient: the horizon color rises fast into the zenith color, the sea lies below
	vec3 col = mix(horizon_color, top_color, pow(clamp(up, 0.0, 1.0), 0.5));
	col = mix(col, below_color, smoothstep(0.0, -0.12, up));

	// Haze: a warm band hugging the horizon
	float haze = exp(-abs(up) * 9.0);
	col = mix(col, haze_color, haze * 0.35 * amount);

	// Sun: a small disc and a wide glow around it
	float facing = LIGHT0_ENABLED ? max(dot(EYEDIR, LIGHT0_DIRECTION), 0.0) : 0.0;
	float disc = smoothstep(0.9988, 0.9994, facing);
	float glow = pow(facing, 24.0) * 0.45 + pow(facing, 4.0) * 0.12;
	col += sun_color * (disc + glow) * amount * step(0.0, up);

	// Clouds: the view ray meets a flat layer above, far clouds flatten towards the horizon
	if (up > 0.0) {
		vec2 layer = EYEDIR.xz / (up + 0.16) * 1.1 + TIME * vec2(cloud_speed, cloud_speed * 0.45);
		float density = sky_fbm(layer);
		float edge = 1.0 - cloud_cover;
		float cover = smoothstep(edge, edge + 0.2, density);

		// Lit side: the density a step towards the sun is thinner on the side that faces it
		vec2 to_sun = LIGHT0_ENABLED ? normalize(LIGHT0_DIRECTION.xz + vec2(0.0001)) : vec2(0.7, 0.7);
		float toward = sky_fbm(layer + to_sun * 0.22);
		float lit = clamp(0.62 + (density - toward) * 2.6, 0.0, 1.0);
		// Belly: thick parts of a cloud are darker
		lit *= 1.0 - 0.3 * smoothstep(edge + 0.12, edge + 0.45, density);
		vec3 cloud = mix(cloud_shade, cloud_light, lit);
		// Far clouds sink into the haze
		cloud = mix(cloud, mix(horizon_color, haze_color, 0.4), exp(-up * 7.0) * 0.75);
		cover *= smoothstep(0.015, 0.14, up) * amount;

		// Cirrus: thin streaks high above the clouds, stretched along the wind
		float streaks = sky_fbm(layer * vec2(0.35, 1.9) + 13.0 + TIME * vec2(cloud_speed * 0.4, 0.0));
		float cirrus = smoothstep(0.52, 0.8, streaks) * smoothstep(0.1, 0.4, up) * amount;
		col = mix(col, cloud_light, cirrus * 0.3);

		col = mix(col, cloud, cover * 0.94);
	}
	COLOR = col;
}
