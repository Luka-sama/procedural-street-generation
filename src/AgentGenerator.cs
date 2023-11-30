using System.Linq;
using Godot;

public partial class AgentGenerator : Node
{
	private Graph _graph;
	private Image _heightMap;
	private Vector2I _size;

	public void Generate(Graph graph, Image heightMap)
	{
		var begin = Time.GetTicksMsec();
		_graph = graph;
		_heightMap = heightMap;
		_size = heightMap.GetSize();
		var agentScene = GD.Load<PackedScene>("res://scenes/Agent.tscn");
		foreach (var vertex in graph.Vertices)
		{
			if (GD.RandRange(0, 2) != 0 || !IsInBorders(vertex.Point)) continue;
			var edge = GetRandomDirection(vertex);
			if (edge == null) continue;
			
			var h = heightMap.GetPixelv(vertex.Point).R * TerrainGenerator.HeightScale;
			var agent = agentScene.Instantiate<Node3D>();
			var scale = GD.RandRange(2, 5);
			agent.Position = new Vector3(vertex.Point.X, h, vertex.Point.Y);
			agent.Scale = new Vector3(scale, scale, scale);
			edge.Agent = agent;
			if (edge.AgentDir)
			{
				edge.To.Agent = agent;
			} else
			{
				edge.From.Agent = agent;
			}
			AddChild(agent);
		}
		GD.Print("Agents generated in ", (Time.GetTicksMsec() - begin), " ms");
	}

	public override void _Process(double delta)
	{
		var speed = 15f;
		var speedY = 10f;
		
		foreach (var edge in _graph.Edges)
		{
			var agent = edge.Agent;
			if (agent == null) continue;
			var from = (edge.AgentDir ? edge.From : edge.To);
			var to = (edge.AgentDir ? edge.To : edge.From);
			var dir = ((Vector2)(to.Point - from.Point)).Normalized();
			var velocity = speed * new Vector3(dir.X, 0, dir.Y);

			var to3 = new Vector3(to.Point.X, 0, to.Point.Y);
			var motion = velocity * (float)delta;
			if ((agent.Position + motion).DistanceSquaredTo(to3) > agent.Position.DistanceSquaredTo(to3))
			{
				motion = Vector3.Zero;
				var newEdge = GetRandomDirection(to, edge);
				if (newEdge != null)
				{
					edge.Agent = null;
					if (edge.To == to)
					{
						edge.To.Agent = null;
					} else
					{
						edge.From.Agent = null;
					}
					
					newEdge.Agent = agent;
					if (newEdge.AgentDir)
					{
						newEdge.To.Agent = agent;
					} else
					{
						newEdge.From.Agent = agent;
					}
				}
			}
			
			var pos = agent.Position + motion;
			var samplePoint = new Vector2I(Mathf.RoundToInt(pos.X), Mathf.RoundToInt(pos.Z));
			var h = 0f;
			if (IsInBorders(samplePoint)) {
				h = _heightMap.GetPixelv(samplePoint).R * TerrainGenerator.HeightScale;
			}
			var motionY = speedY * (float)delta;
			pos.Y = (h > pos.Y ? Mathf.Min(h, pos.Y + motionY) : Mathf.Max(h, pos.Y - motionY));
			agent.Position = pos;
		}
	}

	private Edge GetRandomDirection(Vertex from, Edge oldEdge = null)
	{
		var edges = from.Edges.Where(edge =>
			edge.Agent == null && edge != oldEdge && (edge.From == from ?
				edge.To.Agent == null && IsInBorders(edge.To.Point) :
				edge.From.Agent == null && IsInBorders(edge.From.Point)
			)
		).ToList();
		
		if (edges.Count < 1) return null;
		var edge = edges[GD.RandRange(0, edges.Count - 1)];
		edge.AgentDir = (edge.From == from);
		return edge;
	}

	private bool IsInBorders(Vector2I pos)
	{
		return (pos.X >= 0 && pos.Y >= 0 && pos.X < _size.X && pos.Y < _size.Y);
	}
}