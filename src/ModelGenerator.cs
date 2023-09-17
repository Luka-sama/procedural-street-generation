using Godot;
using System.Collections.Generic;

public enum DebugType
{
	None,
	HighlightRoads,
	HighlightTriangles
}

public partial class ModelGenerator : MeshInstance3D
{
	public float ModelScale { get; } = 0.3f;
	private RandomNumberGenerator _rng = new RandomNumberGenerator();
	private ulong _savedState;
	
	public override async void _Ready()
	{
		_savedState = _rng.State;

		await ToSignal(GetTree().CreateTimer(5), "timeout");
		GenerateRoads(DebugType.None);
	}

	public void GenerateRoads(DebugType debugType)
	{
		var cityScheme = (GetNode("../CityScheme") as CityScheme);
		//GenerateModel(cityScheme.GetMainRoads(), 30, 150);
		var lerpSteps = new List<float> { 0f, 0.05f, 0.1f, 0.15f, 0.2f, 0.8f, 0.85f, 0.9f, 0.95f, 1f };
		GenerateModel(cityScheme.GetMajorRoads(), 10, lerpSteps, debugType);
		//GenerateModel(cityScheme.GetMinorRoads(), 5, 50);
	}

	private void GenerateModel(List<List<Vector2>> roads, float roadWidth, List<float> lerpSteps, DebugType debugType)
	{
		_rng.State = _savedState;
		
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		st.SetUV(new Vector2(0, 0));

		foreach (var road in roads)
		{
			roadWidth = _rng.RandiRange(10, 30);
			var firstPointFrom = road[0];
			var firstPointTo = road[1];
			Vector2 firstDirection = (firstPointTo - firstPointFrom).Normalized();
			Vector2 firstPerpDirection = new Vector2(-firstDirection.Y, firstDirection.X);
			
			// For first point they have the value of bottomLeft and topLeft
			Vector2 lastBottomRight = firstPointFrom - firstPerpDirection * roadWidth / 2;
			Vector2 lastTopRight = firstPointFrom + firstPerpDirection * roadWidth / 2;
			
			Color color = new Color(_rng.Randf(), _rng.Randf(), _rng.Randf());
			for (var i = 1; i < road.Count; i++)
			{
				var start = road[i - 1];
				var end = road[i];
				for (var j = 0; j < lerpSteps.Count - 1; j++)
				{
					var pointFrom = start.Lerp(end, lerpSteps[j]);
					var pointTo = start.Lerp(end, lerpSteps[j + 1]);

					// Calculate direction and perpendicular direction
					Vector2 direction = (pointTo - pointFrom).Normalized();
					Vector2 perpDirection = new Vector2(-direction.Y, direction.X);

					// Calculate four corners of the road segment
					Vector2 bottomRight = pointTo - perpDirection * roadWidth / 2;
					Vector2 topRight = pointTo + perpDirection * roadWidth / 2;

					// Add two triangles to form the road segment
					if (debugType == DebugType.HighlightRoads) st.SetColor(color);
					// Triangle 1
					if (debugType == DebugType.HighlightTriangles) st.SetColor(Colors.Red);
					st.AddVertex(new Vector3(lastTopRight.X, 0, lastTopRight.Y));
					if (debugType == DebugType.HighlightTriangles) st.SetColor(Colors.Green);
					st.AddVertex(new Vector3(lastBottomRight.X, 0, lastBottomRight.Y));
					if (debugType == DebugType.HighlightTriangles) st.SetColor(Colors.Blue);
					st.AddVertex(new Vector3(bottomRight.X, 0, bottomRight.Y));

					// Triangle 2
					if (debugType == DebugType.HighlightTriangles) st.SetColor(Colors.Yellow);
					st.AddVertex(new Vector3(lastTopRight.X, 0, lastTopRight.Y));
					if (debugType == DebugType.HighlightTriangles) st.SetColor(Colors.Cyan);
					st.AddVertex(new Vector3(bottomRight.X, 0, bottomRight.Y));
					if (debugType == DebugType.HighlightTriangles) st.SetColor(Colors.Magenta);
					st.AddVertex(new Vector3(topRight.X, 0, topRight.Y));
					
					lastBottomRight = bottomRight;
					lastTopRight = topRight;
				}
			}
		}

		st.Index();
		st.GenerateNormals();
		st.GenerateTangents();
		var mesh = st.Commit();
		Scale = ModelScale * Vector3.One;
		Mesh = mesh;

		var material = new StandardMaterial3D();
		material.VertexColorUseAsAlbedo = true;
		MaterialOverride = material;
	}
}