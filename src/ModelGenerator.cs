using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;

public partial class ModelGenerator : MeshInstance3D
{
	public override async void _Ready()
	{
		await ToSignal(GetTree().CreateTimer(5), "timeout");
		
		var cityScheme = (GetNode("../CityScheme") as CityScheme);
		//GenerateModel(cityScheme.GetMainRoads(), 30, 150);
		GenerateModel(cityScheme.GetMajorRoads(), 10, 100);
		//GenerateModel(cityScheme.GetMinorRoads(), 5, 50);
	}

	public void GenerateModel(List<List<Vector2>> roads, float roadWidth, int lerpSteps)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		st.SetUV(new Vector2(0, 0));

		foreach (var road in roads)
		{
			roadWidth = GD.RandRange(10, 30);
			var firstPointFrom = road[0];
			var firstPointTo = road[1];
			Vector2 firstDirection = (firstPointTo - firstPointFrom).Normalized();
			Vector2 firstPerpDirection = new Vector2(-firstDirection.Y, firstDirection.X);
			Vector2 lastBottomLeft = firstPointFrom - firstPerpDirection * roadWidth / 2;
			Vector2 lastTopLeft = firstPointFrom + firstPerpDirection * roadWidth / 2;

			for (var i = 1; i < road.Count; i++)
			{
				var start = road[i - 1];
				var end = road[i];
				for (var j = 0; j < lerpSteps; j++)
				{
					var pointFrom = start.Lerp(end, (float)j / lerpSteps);
					var pointTo = start.Lerp(end, (float)(j + 1)/lerpSteps);

					// Calculate direction and perpendicular direction
					Vector2 direction = (pointTo - pointFrom).Normalized();
					Vector2 perpDirection = new Vector2(-direction.Y, direction.X);

					// Calculate four corners of the road segment
					Vector2 bottomLeft = pointFrom - perpDirection * roadWidth / 2;
					Vector2 topLeft = pointFrom + perpDirection * roadWidth / 2;
					Vector2 bottomRight = pointTo - perpDirection * roadWidth / 2;
					Vector2 topRight = pointTo + perpDirection * roadWidth / 2;

					// Add two triangles to form the road segment
					// Triangle 1
					st.AddVertex(new Vector3(lastTopLeft.X, 0, lastTopLeft.Y));
					st.AddVertex(new Vector3(lastBottomLeft.X, 0, lastBottomLeft.Y));
					st.AddVertex(new Vector3(bottomRight.X, 0, bottomRight.Y));

					// Triangle 2
					st.AddVertex(new Vector3(lastTopLeft.X, 0, lastTopLeft.Y));
					st.AddVertex(new Vector3(bottomRight.X, 0, bottomRight.Y));
					st.AddVertex(new Vector3(topRight.X, 0, topRight.Y));
					
					lastBottomLeft = bottomLeft;
					lastTopLeft = topLeft;
				}
			}
		}

		st.Index();
		st.GenerateNormals();
		st.GenerateTangents();
		var mesh = st.Commit();
		Scale = 0.2f * Vector3.One;
		Mesh = mesh;
	}
}
