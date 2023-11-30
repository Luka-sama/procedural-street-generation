using System;
using Godot;
using System.Collections.Generic;

public partial class ModelGenerator : MeshInstance3D
{
	public float ModelScale => 1f;
	private Graph _graph;
	private ulong _savedState;
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
			CalcEdgePolygon(edge, 15);
		}
		
		Scale = ModelScale * Vector3.One;
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

	private void TriangleConnection(Edge edge, bool isFrom)
	{
		Vertex vertex = (isFrom ? edge.From : edge.To);
		foreach (var edge2 in vertex.Edges)
		{
			if (edge == edge2 || edge2.Polygons.Count < 1 || edge.RoadNum != edge2.RoadNum) continue;
			
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
}