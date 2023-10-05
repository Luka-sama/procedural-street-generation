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
	public float ModelScale => 0.5f;
	private CityScheme _cityScheme;
	private RandomNumberGenerator _rng = new();
	private ulong _savedState;
	private SurfaceTool _st = new();
	private DebugType _debugType = DebugType.None;

	public override async void _Ready()
	{
		_savedState = _rng.State;
		_cityScheme = GetNode("../CityScheme") as CityScheme;

		await ToSignal(GetTree().CreateTimer(5), "timeout");
		GenerateModel();
	}

	public void SetDebugType(DebugType debugType)
	{
		_debugType = debugType;
	}

	public void GenerateModel()
	{
		var graph = _cityScheme.GetGraph();
		_rng.State = _savedState;
		
		_st.Begin(Mesh.PrimitiveType.Triangles);
		_st.SetUV(new Vector2(0, 0));

		List<Color> roadColors = new();
		if (_debugType == DebugType.HighlightRoads)
		{
			for (int i = 0; i < graph.RoadCount; i++)
			{
				roadColors.Add(new Color(_rng.Randf(), _rng.Randf(), _rng.Randf()));
			}
		}
		
		foreach (var edge in graph.Edges)
		{
			CalcEdgePolygon(edge, 15);
		}

		if (_debugType == DebugType.None) _st.SetColor(Colors.Gray);
		foreach (var edge in graph.Edges)
		{
			if (_debugType == DebugType.HighlightRoads) _st.SetColor(roadColors[edge.RoadNum]);
			ClipEdgePolygon(edge);
			DrawEdgePolygon(edge);
		}

		_st.SetColor(Colors.Lime);
		var savedDebugType = _debugType;
		foreach (var vertex in graph.Vertices)
		{
			DrawTriangle(vertex.Point + new Vector2(-2, -2), vertex.Point, vertex.Point + new Vector2(-2, 2));
		}
		_debugType = savedDebugType;

		_st.Index();
		_st.GenerateNormals();
		_st.GenerateTangents();
		var mesh = _st.Commit();
		Scale = ModelScale * Vector3.One;
		Mesh = mesh;

		var material = new StandardMaterial3D();
		material.VertexColorUseAsAlbedo = true;
		MaterialOverride = material;
	}

	private void CalcEdgePolygon(Edge edge, float roadWidth)
	{
		if (edge.Polygons.Count > 0) return;
		edge.Width = roadWidth;

		// Calc original rectangle
		Vector2 from = edge.From.Point, to = edge.To.Point;
		Vector2 dir = (to - from).Normalized();
		Vector2 perpDir = new Vector2(dir.Y, -dir.X);
		Vector2 offset = perpDir * roadWidth / 2;
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
	
	private void ClipEdgePolygon(Edge edge)
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
		foreach (var edge2 in vertex.Edges)
		{
			if (edge == edge2 || edge2.Polygons.Count == 0 || edge.RoadNum == edge2.RoadNum || !edge2.IsClipped) continue;
			edge.Polygons = ClipMultiplePolygons(edge.Polygons, edge2.Polygons);
		}
	}

	private List<List<Vector2>> ClipMultiplePolygons(List<List<Vector2>> polygonsA, List<List<Vector2>> polygonsB)
	{
		foreach (var polygonB in polygonsB)
		{
			List<List<Vector2>> result = new();
			foreach (var polygonA in polygonsA)
			{
				var polygons = Geometry2D.ClipPolygons(polygonA.ToArray(), polygonB.ToArray());
				foreach (var polygon in polygons)
				{
					if (!Geometry2D.IsPolygonClockwise(polygon)) result.Add(polygon.ToList());
				}
			}
			polygonsA = result;
		}

		return polygonsA;
	}

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
			
			/*var left = (edge2.From == vertex ? edge2.FromLeft : edge2.ToLeft);
			var right = (edge2.From == vertex ? edge2.FromRight : edge2.ToRight);
			if (isFrom)
			{
				edge.FromLeft = left;
				edge.FromRight = right;
			} else
			{
				edge.ToLeft = left;
				edge.ToRight = right;
			}*/
		}
	}

	private void DrawEdgePolygon(Edge edge)
	{
		foreach (var p in edge.Polygons)
		{
			var indexes = Geometry2D.TriangulatePolygon(p.ToArray());
			for (int i = 0; i < indexes.Length; i += 3)
			{
				DrawTriangle(p[indexes[i]], p[indexes[i + 1]], p[indexes[i + 2]]);
			}
		}
	}

	private void DrawTriangle(Vector2 a, Vector2 b, Vector2 c)
	{
		// Ensure that vertices are CCW
		if ((b - a).Cross(c - a) < 0) (a, b) = (b, a);
		
		if (_debugType == DebugType.HighlightTriangles) _st.SetColor(Colors.Red);
		_st.AddVertex(new Vector3(a.X, 0, a.Y));
		if (_debugType == DebugType.HighlightTriangles) _st.SetColor(Colors.Green);
		_st.AddVertex(new Vector3(b.X, 0, b.Y));
		if (_debugType == DebugType.HighlightTriangles) _st.SetColor(Colors.Blue);
		_st.AddVertex(new Vector3(c.X, 0, c.Y));
	}
}