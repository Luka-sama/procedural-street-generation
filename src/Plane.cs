using Godot;
using System;

public partial class Plane : MeshInstance3D
{
	public override void _Ready()
	{
		GeneratePlane();
	}

	public void GeneratePlane()
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		st.SetUV(new Vector2(0, 0));

		int width = 192, height = 108;
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				st.SetUV(new Vector2((float)x / width, (float)y / height));
				st.AddVertex(new Vector3(x, 0, y));

				st.SetUV(new Vector2((float)(x + 1) / width, (float)y / height));
				st.AddVertex(new Vector3(x + 1, 0, y));

				st.SetUV(new Vector2((float)(x + 1) / width, (float)(y + 1) / height));
				st.AddVertex(new Vector3(x + 1, 0, y + 1));

				st.SetUV(new Vector2((float)x / width, (float)y / height));
				st.AddVertex(new Vector3(x, 0, y));

				st.SetUV(new Vector2((float)(x + 1) / width, (float)(y + 1) / height));
				st.AddVertex(new Vector3(x + 1, 0, y + 1));

				st.SetUV(new Vector2((float)x / width, (float)(y + 1) / height));
				st.AddVertex(new Vector3(x, 0, y + 1));
			}
		}

		st.Index();
		st.GenerateNormals();
		st.GenerateTangents();
		var mesh = st.Commit();
		//Scale = 0.2f * Vector3.One;
		Mesh = mesh;
		
		var shader = new ShaderMaterial();
		shader.Shader = GD.Load<Shader>("res://src/height_map.gdshader");
		shader.SetShaderParameter("source", GD.Load<Texture2D>("res://height_map.png"));
		shader.SetShaderParameter("height_range", 10);
		MaterialOverride = shader;
	}
}
