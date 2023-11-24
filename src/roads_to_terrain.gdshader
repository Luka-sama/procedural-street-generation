shader_type spatial;

uniform sampler2D height_map;

void vertex() {
    float height = texture(height_map, UV).r;
    VERTEX.y = height;
	//COLOR = vec4(height, height, height, 255.) / 255.;
	//COLOR = vec4(UV.x, UV.y, 0., 1.);
}

void fragment() {
	ALBEDO = COLOR.rgb;
}