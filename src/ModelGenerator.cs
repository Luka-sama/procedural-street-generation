using System;
using Godot;
using System.Collections.Generic;
using System.Linq;

public enum DebugType
{
	None,
	HighlightRoads,
	HighlightTriangles
}

public partial class ModelGenerator : MeshInstance3D
{
	public float ModelScale => 1f;
	private Graph _graph;
	private RandomNumberGenerator _rng = new();
	private ulong _savedState;
	//private SurfaceTool _st = new();
	private DebugType _debugType = DebugType.None;
	private List<Tuple<Vector3I, Vector3I>> _buildings;
	private TerrainGenerator _terrainGenerator;
	private BuildingGenerator _buildingGenerator;

	public override async void _Ready()
	{
		_savedState = _rng.State;
		_terrainGenerator = GetNode<TerrainGenerator>("%TerrainGenerator");
		_buildingGenerator = GetNode<BuildingGenerator>("%BuildingGenerator");

		await ToSignal(GetTree().CreateTimer(5), "timeout");
		var cityScheme = GetNode<CityScheme>("%CityScheme");
		_graph = cityScheme.GetGraph();
		GenerateBuildingInfo();
		GenerateModel();
		GenerateTerrain();
		GenerateBuildings();
	}

	public void SetDebugType(DebugType debugType)
	{
		_debugType = debugType;
	}

	private void GenerateTerrain()
	{
		_terrainGenerator.Generate(_graph, _buildings);
	}

	private void GenerateBuildingInfo()
	{
		_buildings = BuildingGenerator.GenerateInfo(_graph);
	}
	
	private void GenerateBuildings()
	{
		_buildingGenerator.GenerateModels(_buildings);
	}

	public void GenerateModel()
	{
		_rng.State = _savedState;
		
		//_st.Begin(Mesh.PrimitiveType.Triangles);

		/*List<Color> roadColors = new();
		if (_debugType == DebugType.HighlightRoads)
		{
			for (int i = 0; i < _graph.RoadCount; i++)
			{
				roadColors.Add(new Color(_rng.Randf(), _rng.Randf(), _rng.Randf()));
			}
		}*/
		
		foreach (var edge in _graph.Edges)
		{
			CalcEdgePolygon(edge, 15);
		}

		/*if (_debugType == DebugType.None) _st.SetColor(Colors.Gray);
		foreach (var edge in _graph.Edges)
		{
			if (_debugType == DebugType.HighlightRoads) _st.SetColor(roadColors[edge.RoadNum]);
			ClipEdgePolygon(edge);
			DrawEdgePolygon(edge);
		}*/

		/*_st.SetColor(Colors.Lime);
		var savedDebugType = _debugType;
		foreach (var vertex in _graph.Vertices)
		{
			DrawTriangle(vertex.Point + new Vector2(-2, -2), vertex.Point, vertex.Point + new Vector2(-2, 2));
		}
		_debugType = savedDebugType;*/

		/*_st.Index();
		_st.GenerateNormals();
		_st.GenerateTangents();
		var mesh = _st.Commit();
		Mesh = mesh;*/
		Scale = ModelScale * Vector3.One;

		//var material = new StandardMaterial3D();
		//material.VertexColorUseAsAlbedo = true;
		/*var material = new ShaderMaterial();
		material.Shader = GD.Load<Shader>("res://src/roads_to_terrain.gdshader");
		var noise = new FastNoiseLite();
		noise.Seed = 123;
		noise.Frequency = 0.001f;
		var img = noise.GetImage(1024, 3072);
		material.SetShaderParameter("region_blend_map", img);*/
		//MaterialOverride = material;
	}

	private void CalcEdgePolygon(Edge edge, int roadWidth)
	{
		if (edge.Polygons.Count > 0) return;
		edge.Width = roadWidth;

		// Calc original rectangle
		Vector2I from = edge.From.Point, to = edge.To.Point;
		var dir = to - from;
		var perpDir = new Vector2I(Mathf.FloorToInt(dir.Y / dir.Length()), Mathf.FloorToInt(-dir.X / dir.Length()));
		var offset = perpDir * roadWidth / 2;
		edge.Polygons.Add(new()
		{
			from - offset, // FromLeft
			from + offset, // FromRight
			to + offset, // ToRight
			to - offset // ToLeft
		});

		TriangleConnection(edge, true);
		TriangleConnection(edge, false);
	}
	
	/*private void ClipEdgePolygon(Edge edge)
	{
		if (edge.IsClipped) return;
		ClipPolygons(edge, true);
		ClipPolygons(edge, false);
		edge.IsClipped = true;
	}

	// Clips the polygons to avoid overlapping
	private void ClipPolygons(Edge edge, bool isFrom)
	{
		Vertex vertex = (isFrom ? edge.From : edge.To);
		List<Edge> edgesToCheck = new();
		foreach (var edge2 in vertex.Edges)
		{
			if (edge == edge2 || edge2.Polygons.Count == 0 || edge.RoadNum == edge2.RoadNum ||
			    edgesToCheck.Contains(edge2) || !edge2.IsClipped) continue;
			edgesToCheck.Add(edge2);
			
			var vertex2 = (edge2.From == vertex ? edge2.To : edge2.From);
			foreach (var edge3 in vertex2.Edges)
			{
				if (edge == edge3 || edge3.Polygons.Count == 0 || edge.RoadNum == edge3.RoadNum ||
				    edgesToCheck.Contains(edge3) || !edge3.IsClipped) continue;
				edgesToCheck.Add(edge3);
			}
		}

		foreach (var edge2 in edgesToCheck)
		{
			edge.Polygons = ClipMultiplePolygons(edge.Polygons, edge2.Polygons);
		}
	}

	private List<List<Vector2I>> ClipMultiplePolygons(List<List<Vector2I>> polygonsA, List<List<Vector2I>> polygonsB)
	{
		foreach (var polygonB in polygonsB)
		{
			List<List<Vector2I>> result = new();
			foreach (var polygonA in polygonsA)
			{
				var polygons = Geometry2D.ClipPolygons(
					polygonA.Select(v => (Vector2)v).ToArray(),
					polygonB.Select(v => (Vector2)v).ToArray()
				);
				foreach (var polygon in polygons)
				{
					if (!Geometry2D.IsPolygonClockwise(polygon)) result.Add(polygon.Select(v => (Vector2I)v).ToList());
				}
			}
			polygonsA = result;
		}

		return polygonsA;
	}*/

	private void TriangleConnection(Edge edge, bool isFrom)
	{
		Vertex vertex = (isFrom ? edge.From : edge.To);
		foreach (var edge2 in vertex.Edges)
		{
			if (edge == edge2 || edge2.Polygons.Count < 1 || edge.RoadNum != edge2.RoadNum) continue;
			/*int offset = (isFrom ? 0 : 2);
			Vector2 nearestToFirst = edge2.Polygon[0];
			Vector2 nearestToSecond = edge2.Polygon[0];
			float minDistanceToFirst = (edge.Polygon[offset] - nearestToFirst).Length();
			float minDistanceToSecond = (edge.Polygon[offset + 1] - nearestToSecond).Length();
			foreach (var point in edge2.Polygon)
			{
				var distanceToFirst = (edge.Polygon[offset] - point).Length();
				if (distanceToFirst < minDistanceToFirst)
				{
					minDistanceToFirst = distanceToFirst;
					nearestToFirst = point;
				}
				
				var distanceToSecond = (edge.Polygon[offset + 1] - point).Length();
				if (distanceToSecond < minDistanceToSecond)
				{
					minDistanceToSecond = distanceToSecond;
					nearestToSecond = point;
				}
			}
			edge.Polygon[offset] = nearestToFirst;
			edge.Polygon[offset + 1] = nearestToSecond;*/

			var left = (edge2.From == vertex ? edge2.Polygons[0][0] : edge2.Polygons[0][3]);
			var right = (edge2.From == vertex ? edge2.Polygons[0][1] : edge2.Polygons[0][2]);
			if (isFrom)
			{
				edge.Polygons[0][0] = left;
				edge.Polygons[0][1] = right;
			} else
			{
				edge.Polygons[0][3] = left;
				edge.Polygons[0][2] = right;
			}
		}
	}

	/*private void DrawEdgePolygon(Edge edge)
	{
		foreach (var p in edge.Polygons)
		{
			var indexes = Geometry2D.TriangulatePolygon(p.Select(v => (Vector2)v).ToArray());
			for (var i = 0; i < indexes.Length; i += 3)
			{
				DrawTriangle(p[indexes[i]], p[indexes[i + 1]], p[indexes[i + 2]]);
			}
		}
	}

	private void DrawTriangle(Vector2 a, Vector2 b, Vector2 c)
	{
		// Ensure that vertices are CCW
		if ((b - a).Cross(c - a) < 0) (a, b) = (b, a);

		Vector2 size = new Vector2(1025, 1025);
		if (_debugType == DebugType.HighlightTriangles) _st.SetColor(Colors.Red);
		_st.SetUV((a / size).Clamp(Vector2.Zero, Vector2.One));
		_st.AddVertex(new Vector3(a.X, 0, a.Y));
		if (_debugType == DebugType.HighlightTriangles) _st.SetColor(Colors.Green);
		_st.SetUV((b / size).Clamp(Vector2.Zero, Vector2.One));
		_st.AddVertex(new Vector3(b.X, 0, b.Y));
		if (_debugType == DebugType.HighlightTriangles) _st.SetColor(Colors.Blue);
		_st.SetUV((c / size).Clamp(Vector2.Zero, Vector2.One));
		_st.AddVertex(new Vector3(c.X, 0, c.Y));
	}*/
}