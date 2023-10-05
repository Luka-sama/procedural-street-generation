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
		var k = 0;
		foreach (var edge in graph.Edges)
		{
			if (_debugType == DebugType.HighlightRoads) _st.SetColor(roadColors[edge.RoadNum]);
			ClipEdgePolygon(edge);
			DrawEdgePolygon(edge);

			k++;
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
		if (!Mathf.IsZeroApprox(edge.Width)) return;
		edge.Width = roadWidth;

		// Calc original rectangle
		Vector2 from = edge.From.Point, to = edge.To.Point;
		Vector2 dir = (to - from).Normalized();
		Vector2 perpDir = new Vector2(dir.Y, -dir.X);
		Vector2 offset = perpDir * roadWidth / 2;
		edge.Polygons[0].Add(from - offset); // FromLeft
		edge.Polygons[0].Add(from + offset); // FromRight
		edge.Polygons[0].Add(to + offset); // ToRight
		edge.Polygons[0].Add(to - offset); // ToLeft

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
			if (edge == edge2 || edge2.Polygons[0].Count < 1 || edge.RoadNum != edge2.RoadNum) continue;
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

	private void CheckIntersections(Edge edge, bool isFrom)
	{
		if (edge.Width < 0) return;
		
		var vertex = (isFrom ? edge.From : edge.To);
		List<Edge> edgesToCheck = new();
		List<Edge> intersectingEdges = new();
		foreach (var edge2 in vertex.Edges)
		{
			if (edge == edge2 || Mathf.IsZeroApprox(edge2.Width) || edge.RoadNum == edge2.RoadNum || edgesToCheck.Contains(edge2)) continue;
			edgesToCheck.Add(edge2);
			/*var vertex2 = (edge2.From == vertex ? edge2.To : edge2.From);
			foreach (var edge3 in vertex2.Edges)
			{
				if (edge == edge3 || Mathf.IsZeroApprox(edge3.Width) || edge.RoadNum == edge3.RoadNum || edgesToCheck.Contains(edge3)) continue;
				edgesToCheck.Add(edge3);
			}*/
		}
		
		foreach (var edge2 in edgesToCheck)
		{
			/*Vector2[] polygon = { edge2.FromLeft, edge2.FromRight, edge2.ToRight, edge2.ToLeft };
			if (Geometry2D.IsPointInPolygon(edge.FromLeft, polygon) &&
			    Geometry2D.IsPointInPolygon(edge.FromRight, polygon) &&
			    Geometry2D.IsPointInPolygon(edge.ToLeft, polygon) && Geometry2D.IsPointInPolygon(edge.ToRight, polygon))
			{
				edge.Width = -1;
				return;
			}*/
			
			var shortened1 = CheckIntersection(edge, edge2.FromLeft, edge2.ToLeft, isFrom);
			var shortened2 = CheckIntersection(edge, edge2.FromRight, edge2.ToRight, isFrom);
			var shortened3 = CheckIntersection(edge, edge2.FromLeft, edge2.FromRight, isFrom);
			var shortened4 = CheckIntersection(edge, edge2.ToLeft, edge2.ToRight, isFrom);
			if (shortened1 || shortened2 || shortened3 || shortened4)
			{
				intersectingEdges.Add(edge2);
			}
		}
		
		foreach (var edge2 in intersectingEdges)
		{
			var shorterLeft = (edge2.From == vertex ? edge2.FromLeft : edge2.ToLeft);
			var shorterRight = (edge2.From == vertex ? edge2.FromRight : edge2.ToRight);
			var left = (isFrom ? edge.FromLeft : edge.ToLeft);
			var right = (isFrom ? edge.FromRight : edge.ToRight);
			var distance1 = (shorterRight - Geometry2D.GetClosestPointToSegment(shorterRight, left, right)).Length();
			var distance2 = (shorterLeft - Geometry2D.GetClosestPointToSegment(shorterLeft, left, right)).Length();
			var minDistance = Mathf.Min(distance1, distance2);
			var extraPoint = shorterLeft;
			if (distance1 < distance2) extraPoint = shorterRight;
			var dir1 = (right - left).Normalized();
			var dir2 = (extraPoint - left).Normalized();
			if (Mathf.IsZeroApprox((dir2 - dir1).Length())) continue;
			var hasPoint = (isFrom ? edge.HasFromExtraPoint : edge.HasToExtraPoint);
			var oldPoint = (isFrom ? edge.FromExtraPoint : edge.ToExtraPoint);
			var oldDistance = (hasPoint ? (oldPoint - Geometry2D.GetClosestPointToSegment(oldPoint, left, right)).Length() : 0);
			if (isFrom)
			{
				if (edge.HasFromExtraPoint && oldDistance < minDistance) return;
				edge.HasFromExtraPoint = true;
				edge.FromExtraPoint = extraPoint;
				edge.IsFromExtraPointInside = Geometry2D.PointIsInsideTriangle(extraPoint, edge.FromLeft, edge.FromRight, edge.ToRight);
			} else
			{
				if (edge.HasToExtraPoint && oldDistance < minDistance) return;
				edge.HasToExtraPoint = true;
				edge.ToExtraPoint = extraPoint;
				edge.IsToExtraPointInside = Geometry2D.PointIsInsideTriangle(extraPoint, edge.ToRight, edge.ToLeft, edge.FromLeft);
			}
		}
	}

	private bool CheckIntersection(Edge edge, Vector2 fromB, Vector2 toB, bool isFrom)
	{
		bool shortened1 = CheckIntersectionOneSide(edge, fromB, toB, isFrom, true);
		bool shortened2 = CheckIntersectionOneSide(edge, fromB, toB, isFrom, false);
		return shortened1 || shortened2;
	}

	private bool CheckIntersectionOneSide(Edge edge, Vector2 fromB, Vector2 toB, bool isFrom, bool isLeft)
	{
		var from = (isLeft ? edge.FromLeft : edge.FromRight);
		var to = (isLeft ? edge.ToLeft : edge.ToRight);
		var check = Geometry2D.SegmentIntersectsSegment(from, to, fromB, toB);
		if (check.VariantType != Variant.Type.Vector2) return false;
		var intersection = (Vector2)check;
		if ((to - intersection).Length() >= (to - from).Length()) return false;
		
		if (isFrom && isLeft) edge.FromLeft = intersection;
		else if (isFrom) edge.FromRight = intersection;
		else if (isLeft) edge.ToLeft = intersection;
		else edge.ToRight = intersection;
		
		return true;
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

	private void DrawEdgeTriangles(Edge edge)
	{
		if (edge.Width < 0) return;

		if (edge.HasFromExtraPoint && edge.IsFromExtraPointInside)
		{
			DrawTriangle(edge.FromLeft, edge.FromExtraPoint, edge.ToRight);
			DrawTriangle(edge.FromExtraPoint, edge.FromRight, edge.ToRight);
		} else
		{
			DrawTriangle(edge.FromLeft, edge.FromRight, edge.ToRight);
		}
		if (edge.HasToExtraPoint && edge.IsToExtraPointInside)
		{
			DrawTriangle(edge.ToRight, edge.ToExtraPoint, edge.FromLeft);
			DrawTriangle(edge.ToExtraPoint, edge.ToLeft, edge.FromLeft);
		} else
		{
			DrawTriangle(edge.ToRight, edge.ToLeft, edge.FromLeft);
		}

		if (edge.HasFromExtraPoint && !edge.IsFromExtraPointInside)
		{
			DrawTriangle(edge.FromLeft, edge.FromRight, edge.FromExtraPoint);
		}
		if (edge.HasToExtraPoint && !edge.IsToExtraPointInside)
		{
			DrawTriangle(edge.ToLeft, edge.ToRight, edge.ToExtraPoint);
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