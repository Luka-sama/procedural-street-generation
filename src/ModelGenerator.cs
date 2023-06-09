using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;

public partial class ModelGenerator : MeshInstance3D
{
	public override async void _Ready()
	{
		await ToSignal(GetTree().CreateTimer(3), "timeout");
		GenerateModel();
	}

	public void GenerateModel()
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Lines);
		st.SetUV(new Vector2(0, 0));

		var cityScheme = (GetNode("../Main") as CityScheme);
		List<List<Vector2>> roads = cityScheme.GetRoads();
		foreach (var road in roads)
		{
			for (var i = 1; i < road.Count; i++)
			{
				var pointFrom = road[i - 1];
				var pointTo = road[i];
				st.AddVertex(new Vector3(pointFrom.X, 0, pointFrom.Y));
				st.AddVertex(new Vector3(pointTo.X, 0, pointTo.Y));
			}
		}

		/*st.Index();
		st.GenerateNormals();
		st.GenerateTangents();*/
		var mesh = st.Commit();
		Scale = 0.02f * Vector3.One;
		Mesh = mesh;
	}
}
