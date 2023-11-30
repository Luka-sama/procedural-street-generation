using System;
using Godot;
using System.Collections.Generic;

public partial class ModelGenerator : Node
{
	private Graph _graph;
	private List<Tuple<Vector3I, Vector3I>> _buildings;
	private TerrainGenerator _terrainGenerator;
	private BuildingGenerator _buildingGenerator;
	private AgentGenerator _agentGenerator;

	public override void _Ready()
	{
		_terrainGenerator = GetNode<TerrainGenerator>("%TerrainGenerator");
		_buildingGenerator = GetNode<BuildingGenerator>("%BuildingGenerator");
		_agentGenerator = GetNode<AgentGenerator>("%AgentGenerator");
	}

	public async void Generate(Graph graph)
	{
		_graph = graph;
		_buildings = BuildingGenerator.GenerateInfo(_graph);
		GenerateModel();
		await ToSignal(this, "ready");
		var heightMap = _terrainGenerator.Generate(_graph, _buildings);
		_buildingGenerator.GenerateModels(_buildings);
		_agentGenerator.Generate(_graph, heightMap);
	}

	private void GenerateModel()
	{
		foreach (var edge in _graph.Edges)
		{
			CalcEdgePolygon(edge, GD.RandRange(20, 30));
		}
	}

	private void CalcEdgePolygon(Edge edge, int roadWidth)
	{
		if (edge.Polygon.Count > 0) return;

		// Calc original rectangle
		Vector2I from = edge.From.Point, to = edge.To.Point;
		var dir = to - from;
		var perpDir = new Vector2I(Mathf.FloorToInt(dir.Y / dir.Length()), Mathf.FloorToInt(-dir.X / dir.Length()));
		var offset = perpDir * roadWidth / 2;
		edge.Polygon.Add(from - offset); // FromLeft
		edge.Polygon.Add(from + offset); // FromRight
		edge.Polygon.Add(to + offset); // ToRight
		edge.Polygon.Add(to - offset); // ToLeft

		TriangleConnection(edge, true);
		TriangleConnection(edge, false);
	}

	private void TriangleConnection(Edge edge, bool isFrom)
	{
		var vertex = (isFrom ? edge.From : edge.To);
		foreach (var edge2 in vertex.Edges)
		{
			if (edge == edge2 || edge2.Polygon.Count < 1 || edge.RoadNum != edge2.RoadNum) continue;
			
			var left = (edge2.From == vertex ? edge2.Polygon[0] : edge2.Polygon[3]);
			var right = (edge2.From == vertex ? edge2.Polygon[1] : edge2.Polygon[2]);
			if (isFrom)
			{
				edge.Polygon[0] = left;
				edge.Polygon[1] = right;
			} else
			{
				edge.Polygon[3] = left;
				edge.Polygon[2] = right;
			}
		}
	}
}