using System.Collections.Generic;
using System.Linq;
using System;
using Godot;

public partial class BuildingGenerator : Node
{
	public static List<Tuple<Vector3I, Vector3I>> GenerateInfo(Graph graph)
	{
		var polygons = GeneratePolygons(graph);
		var buildings = new List<Tuple<Vector3I, Vector3I>>();
		foreach (var polygon in polygons)
		{
			buildings.AddRange(GenerateBuildings(polygon));
		}
		return buildings;
	}

	public void GenerateModels(List<Tuple<Vector3I, Vector3I>> buildings)
	{
		foreach (var building in buildings)
		{
			AddChild(GenerateModel(building));
		}		
	}
	
	private static List<List<Vector2I>> GeneratePolygons(Graph graph)
	{
		var polygons = new List<List<Vector2I>>();
		var visitedFrom = new HashSet<Edge>();
		var visitedTo = new HashSet<Edge>();
		foreach (var vertex in graph.Vertices)
		{
			var polygon = new List<Vector2I>();
			var currVertex = vertex;
			Edge currEdge = null;
			var currVisitedFrom = new HashSet<Edge>();
			var currVisitedTo = new HashSet<Edge>();
			
			while (!polygon.Contains(currVertex.Point))
			{
				polygon.Add(currVertex.Point);
				
				var minAngle = Mathf.Pi * 2;
				var newVertex = currVertex;
				var newEdge = currEdge;
				var transformAngle = 0f;
				if (currEdge != null)
				{
					var from = (currEdge.From == currVertex ? currEdge.To.Point : currEdge.From.Point);
					var to = currVertex.Point;
					transformAngle = ((Vector2)(from - to)).Angle();
				}
				foreach (var edge in currVertex.Edges)
				{
					var candidat = (edge.From == currVertex ? edge.To : edge.From);
					if (edge.From == currVertex && visitedTo.Contains(edge)) continue;
					if (edge.From != currVertex && visitedFrom.Contains(edge)) continue;
					var angle = ((Vector2)(candidat.Point - currVertex.Point)).Angle() - transformAngle;
					if (angle < 0 || Mathf.IsZeroApprox(angle))
					{
						angle += Mathf.Pi * 2;
					}
					if (angle < minAngle)
					{
						minAngle = angle;
						newVertex = candidat;
						newEdge = edge;
					}
				}
				currVertex = newVertex;
				currEdge = newEdge;
				if (currEdge != null)
				{
					if (currEdge.To == currVertex) currVisitedTo.Add(currEdge);
					else currVisitedFrom.Add(currEdge);
				}
			}

			if (currVertex == vertex && polygon.Count > 2)
			{
				var deflated = Geometry2D.OffsetPolygon(polygon.Select(v => (Vector2)v).ToArray(), -20);
				polygon = deflated
					.FirstOrDefault(p => !Geometry2D.IsPolygonClockwise(p), Array.Empty<Vector2>())
					.Select(v => (Vector2I)v)
					.ToList();
				if (polygon.Count <= 2) continue;

				polygons.Add(polygon);
				foreach (var edge in currVisitedFrom)
				{
					visitedFrom.Add(edge);
				}
				foreach (var edge in currVisitedTo)
				{
					visitedTo.Add(edge);
				}
			}
		}
		
		return polygons;
	}

	private static List<Tuple<Vector3I, Vector3I>> GenerateBuildings(List<Vector2I> polygon)
	{
		var buildings = new List<Tuple<Vector3I, Vector3I>>();

		for (var i = 0; i < 100; i++)
		{
			var building = GenerateRandomBuilding(polygon);
			while (!IsBuildingCorrect(building, polygon, buildings) && building.Item2.X > 20 && building.Item2.Y > 20)
			{
				var newSize = (Vector3I)((Vector3)building.Item2 * 0.9f);
				building = new Tuple<Vector3I, Vector3I>(building.Item1, newSize);
			}
			if (IsBuildingCorrect(building, polygon, buildings))
			{
				buildings.Add(building);
			}
		}
		
		return buildings;
	}

	private static Tuple<Vector3I, Vector3I> GenerateRandomBuilding(List<Vector2I> polygon)
	{
		var position = GetRandomPointInsideOfPolygon(polygon);
		var size = new Vector3I(GD.RandRange(20, 200), GD.RandRange(5, 40), GD.RandRange(20, 200));
		return new Tuple<Vector3I, Vector3I>(new Vector3I(position.X, 0, position.Y), size);
	}

	private static Vector2I GetRandomPointInsideOfPolygon(List<Vector2I> p)
	{
		var indexes = Geometry2D.TriangulatePolygon(p.Select(v => (Vector2)v).ToArray());
		if (indexes.Length < 1)
		{
			return (p.Count > 0 ? p[0] : Vector2I.Zero);
		}
		var randomTriangle = GD.RandRange(0, indexes.Length / 3 - 1) * 3;
		Vector2 a = p[indexes[randomTriangle]], b = p[indexes[randomTriangle + 1]], c = p[indexes[randomTriangle + 2]];
		float r1 = Mathf.Sqrt(GD.Randf()), r2 = GD.Randf();
		var randomPoint = (1 - r1) * a + (r1 * (1 - r2)) * b + (r2 * r1) * c;
		return (Vector2I) randomPoint;
	}

	private static bool IsInsideOfPolygon(Tuple<Vector3I, Vector3I> building, List<Vector2I> polygon)
	{
		var position = building.Item1;
		var size = building.Item2;
		var p = polygon.Select(v => (Vector2)v).ToArray();
		for (var x = position.X; x <= position.X + size.X; x++)
		{
			for (var z = position.Z; z <= position.Z + size.Z; z++)
			{
				if (!Geometry2D.IsPointInPolygon(new Vector2(x, z), p))
				{
					return false;
				}
			}
		}

		return true;
	}

	private static bool IsBuildingCorrect(Tuple<Vector3I, Vector3I> building, List<Vector2I> polygon, List<Tuple<Vector3I, Vector3I>> buildings)
	{
		return IsInsideOfPolygon(building, polygon) && !IntersectsOtherBuildings(building, buildings);
	}

	private static bool IntersectsOtherBuildings(Tuple<Vector3I, Vector3I> building, List<Tuple<Vector3I, Vector3I>> buildings)
	{
		int x1 = building.Item1.X, y1 = building.Item1.Z;
		int w1 = building.Item2.X, h1 = building.Item1.Z;
		foreach (var (pos2, size2) in buildings)
		{
			int x2 = pos2.X, y2 = pos2.Z;
			int w2 = size2.X, h2 = size2.Z;
			if (x1 < x2 + w2 && x2 < x1 + w1 && y1 < y2 + h2 && y2 < y1 + h1)
			{
				return true;
			}
		}
		return false;
	}

	private static Node3D GenerateModel(Tuple<Vector3I, Vector3I> building)
	{
		var staticBody = new StaticBody3D();
		staticBody.Scale = building.Item2;
		staticBody.Position = building.Item1 + staticBody.Scale / 2f;

		var mesh = new MeshInstance3D();
		mesh.Mesh = new BoxMesh();
		
		var collisionShape = new CollisionShape3D();
		var boxShape = new BoxShape3D();
		collisionShape.Shape = boxShape;
		
		staticBody.AddChild(mesh);
		staticBody.AddChild(collisionShape);
		return staticBody;
	}
}