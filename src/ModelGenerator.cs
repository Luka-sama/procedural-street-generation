using System;
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
		GenerateModel(cityScheme.GetGraph(), debugType);
		//GenerateModel(cityScheme.GetMinorRoads(), 5, 50);
	}
	
	private void GenerateModel(Graph graph, DebugType debugType)
	{
		_rng.State = _savedState;

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);
		st.SetUV(new Vector2(0, 0));

		List<Color> roadColors = new();
		if (debugType == DebugType.HighlightRoads)
		{
			for (int i = 0; i < graph.RoadCount; i++)
			{
				roadColors.Add(new Color(_rng.Randf(), _rng.Randf(), _rng.Randf()));
			}
		}

		if (debugType == DebugType.None) st.SetColor(Colors.Gray);
		for (int i = 0; i < graph.RoadCount; i++)
		{
			foreach (var edge in graph.Edges)
			{
				if (edge.RoadNum != i) continue;
				CalcEdgeTriangles(edge, 15);
				if (debugType == DebugType.HighlightRoads) st.SetColor(roadColors[edge.RoadNum]);
				DrawEdgeTriangles(edge, debugType, st);
			}
		}

		st.SetColor(Colors.Lime);
		foreach (var vertex in graph.Vertices)
		{
			DrawTriangle(vertex.Point + new Vector2(-2, -2), vertex.Point, vertex.Point + new Vector2(-2, 2), st, DebugType.None);
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

	private void CalcEdgeTriangles(Edge edge, float roadWidth)
	{
		if (!Mathf.IsZeroApprox(edge.Width)) return;
		edge.Width = roadWidth;

		// Calc original rectangle
		Vector2 from = edge.From.Point, to = edge.To.Point;
		Vector2 dir = (to - from).Normalized();
		Vector2 perpDir = new Vector2(dir.Y, -dir.X);
		Vector2 offset = perpDir * roadWidth / 2;
		edge.FromLeft = from - offset;
		edge.FromRight = from + offset;
		edge.ToLeft = to - offset;
		edge.ToRight = to + offset;
		
		TriangleConnection(edge, true);
		TriangleConnection(edge, false);

		// Shorten the sides to avoid overlapping
		CheckIntersections(edge, true);
		CheckIntersections(edge, false);
	}

	private void TriangleConnection(Edge edge, bool isFrom)
	{
		Vertex vertex = (isFrom ? edge.From : edge.To);
		foreach (var edge2 in vertex.Edges)
		{
			if (edge == edge2 || Mathf.IsZeroApprox(edge2.Width) || edge.RoadNum != edge2.RoadNum) continue;
			var left = (edge2.From == vertex ? edge2.FromLeft : edge2.ToLeft);
			var right = (edge2.From == vertex ? edge2.FromRight : edge2.ToRight);
			if (isFrom)
			{
				edge.FromLeft = left;
				edge.FromRight = right;
			} else
			{
				edge.ToLeft = left;
				edge.ToRight = right;
			}
		}
	}

	private void CheckIntersections(Edge edge, bool isFrom)
	{
		if (edge.Width < 0) return;
		
		Vertex vertex = (isFrom ? edge.From : edge.To);
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
			
			var shorterLeft = (edge2.From == vertex ? edge2.FromLeft : edge2.ToLeft);
			var shorterRight = (edge2.From == vertex ? edge2.FromRight : edge2.ToRight);
			var shortened1 = CheckIntersection(edge, edge2.FromLeft, edge2.ToLeft, isFrom, shorterLeft, shorterRight);
			var shortened2 = CheckIntersection(edge, edge2.FromRight, edge2.ToRight, isFrom, shorterLeft, shorterRight);
			var shortened3 = CheckIntersection(edge, shorterLeft, shorterRight, isFrom, shorterLeft, shorterRight);
			if (shortened1 || shortened2 || shortened3)
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

	private bool CheckIntersection(Edge edge, Vector2 fromB, Vector2 toB, bool isFrom, Vector2 shorterLeft, Vector2 shorterRight)
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

	private void DrawEdgeTriangles(Edge edge, DebugType debugType, SurfaceTool st)
	{
		if (edge.Width < 0) return;

		if (edge.HasFromExtraPoint && edge.IsFromExtraPointInside)
		{
			DrawTriangle(edge.FromLeft, edge.FromExtraPoint, edge.ToRight, st, debugType);
			DrawTriangle(edge.FromExtraPoint, edge.FromRight, edge.ToRight, st, debugType);
		} else
		{
			DrawTriangle(edge.FromLeft, edge.FromRight, edge.ToRight, st, debugType);
		}
		if (edge.HasToExtraPoint && edge.IsToExtraPointInside)
		{
			DrawTriangle(edge.ToRight, edge.ToExtraPoint, edge.FromLeft, st, debugType);
			DrawTriangle(edge.ToExtraPoint, edge.ToLeft, edge.FromLeft, st, debugType);
		} else
		{
			DrawTriangle(edge.ToRight, edge.ToLeft, edge.FromLeft, st, debugType);
		}

		if (edge.HasFromExtraPoint && !edge.IsFromExtraPointInside)
		{
			DrawTriangle(edge.FromLeft, edge.FromRight, edge.FromExtraPoint, st, debugType);
		}
		if (edge.HasToExtraPoint && !edge.IsToExtraPointInside)
		{
			DrawTriangle(edge.ToLeft, edge.ToRight, edge.ToExtraPoint, st, debugType);
		}
	}

	private void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, SurfaceTool st, DebugType debugType)
	{
		// Ensure that vertices are CCW
		if ((b - a).Cross(c - a) < 0) (a, b) = (b, a);
		
		// Colors.Yellow, Colors.Cyan, Colors.Magenta
		if (debugType == DebugType.HighlightTriangles) st.SetColor(Colors.Red);
		st.AddVertex(new Vector3(a.X, 0, a.Y));
		if (debugType == DebugType.HighlightTriangles) st.SetColor(Colors.Green);
		st.AddVertex(new Vector3(b.X, 0, b.Y));
		if (debugType == DebugType.HighlightTriangles) st.SetColor(Colors.Blue);
		st.AddVertex(new Vector3(c.X, 0, c.Y));
	}
}