shader_type spatial;

uniform sampler2D source;
uniform float height_range;

void vertex() {
	float h = texture(source, UV).r * height_range;
	VERTEX = vec3(VERTEX.x, h, VERTEX.z);
}

void fragment() {
	ALBEDO = vec3(1.0, 0.0, 0.0);
}