using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class TerrainGenerator : Node
{
	public const float HeightScale = 60f;
	public static readonly Vector2I Size = new(2048, 1024);

	public Image Generate(Graph graph, List<Tuple<Vector3I, Vector3I>> buildings)
	{
		var controlMap = GenerateControlMap(graph);
		var heightMap = GenerateHeightMap(controlMap, buildings);
		//controlMap.SavePng("control_map.png");
		//heightMap.SavePng("height_map.png");
		var begin = Time.GetTicksMsec();
		
		var terrainAsVariant = ClassDB.Instantiate("Terrain3D");
		var terrain = terrainAsVariant.AsGodotObject();
		var terrainAsNode = terrainAsVariant.As<Node>();
		var storage = ClassDB.Instantiate("Terrain3DStorage").AsGodotObject();
		terrain.Call("set_collision_enabled", false);
		terrain.Set("storage", storage);
		terrain.Set("name", "Terrain3D");
		AddChild(terrainAsNode, true);
		terrain.Set("texture_list", GD.Load("res://textures/texture_list.tres"));
		storage.Set("noise_enabled", true);
		//storage.Set("show_heightmap", true);
		
		storage.Call("import_images", new[] {heightMap, controlMap, null}, Vector3.Zero, 0f, HeightScale);
		
		//terrain.Call("set_show_debug_collision", true);
		terrain.Call("set_collision_enabled", true);

		GD.Print("Terrain generated in ", (Time.GetTicksMsec() - begin), " ms");
		return heightMap;
	}

	private static Image GenerateControlMap(Graph graph)
	{
		var begin = Time.GetTicksMsec();
		
		var img = Image.Create(Size.X, Size.Y, false, Image.Format.Rgb8);
		// black - terrain, Color8(1, 0, 0) - roads
		img.Fill(Colors.Black);
		foreach (var edge in graph.Edges)
		{
			var p = edge.Polygon;
			var indexes = Geometry2D.TriangulatePolygon(p.Select(v => (Vector2)v).ToArray());
			for (var i = 0; i < indexes.Length; i += 3)
			{
				Vector2I a = p[indexes[i]], b = p[indexes[i + 1]], c = p[indexes[i + 2]];
				var d = 4;
				var minX = Mathf.Clamp(Mathf.Min(Mathf.Min(a.X, b.X), c.X) - d, 0, Size.X - 1);
				var maxX = Mathf.Clamp(Mathf.Max(Mathf.Max(a.X, b.X), c.X) + d, 0, Size.X - 1);
				var minY = Mathf.Clamp(Mathf.Min(Mathf.Min(a.Y, b.Y), c.Y) - d, 0, Size.Y - 1);
				var maxY = Mathf.Clamp(Mathf.Max(Mathf.Max(a.Y, b.Y), c.Y) + d, 0, Size.Y - 1);
				for (var x = minX; x <= maxX; x++)
				{
					for (var y = minY; y <= maxY; y++)
					{
						var point = new Vector2I(x, y);
						if (SquaredDistanceFromPointToTriangle(point, a, b, c) < d * d)
						{
							img.SetPixelv(point, Color.Color8(1, 0, 0));
						}
					}
				}
			}
		}

		GD.Print("Control map generated in ", (Time.GetTicksMsec() - begin), " ms");
		return img;
	}

	private static Image GenerateHeightMap(Image controlMap, List<Tuple<Vector3I, Vector3I>> buildings)
	{
		var begin = Time.GetTicksMsec();
		
		var noise = new FastNoiseLite();
		noise.Seed = (int)GD.Randi();
		noise.Frequency = 0.001f;
		var maxPixel = new Vector3I(Size.X - 1, 0, Size.Y - 1);
		var noiseImg = noise.GetImage(Size.X, Size.Y);
		var heightMap = (Image)noiseImg.Duplicate();
		var filterSize = 30;

		var heightMapSat = GenerateSummedAreaTable(heightMap);
		for (var y = 0; y < Size.Y; y++)
		{
			for (var x = 0; x < Size.X; x++)
			{
				if (controlMap.GetPixel(x, y) == Colors.Black)
				{
					continue; // not road
				}
				var x0 = Mathf.Max(x - filterSize, 0);
				var y0 = Mathf.Max(y - filterSize, 0);
				var x1 = Mathf.Min(x + filterSize, Size.X - 1);
				var y1 = Mathf.Min(y + filterSize, Size.Y - 1);
				
				var sumHeight = GetAreaSum(heightMapSat, x0, y0, x1, y1);
				var area = (x1 - x0 + 1) * (y1 - y0 + 1);
				var avgHeight = sumHeight / area;

				var h = avgHeight - 0.01f;
				heightMap.SetPixel(x, y, new Color(h, h, h));
			}
        }

		for (var i = 0; i < buildings.Count; i++)
		{
			var building = buildings[i];
			var position = building.Item1;
			var size = building.Item2;

			// Calculate average height in the building area
			var from = position.Clamp(Vector3I.Zero, maxPixel);
			var to = (position + size).Clamp(Vector3I.Zero, maxPixel);
			var h = 0f;
			var count = 0;
			for (var x = from.X; x <= to.X; x++)
			{
				for (var y = from.Z; y <= to.Z; y++)
				{
					h += heightMap.GetPixel(x, y).R;
					count++;
				}
			}

			h /= count;
			position.Y = Mathf.FloorToInt(h * HeightScale);
			buildings[i] = new Tuple<Vector3I, Vector3I>(position, size);

			var d = BuildingGenerator.DistanceBetweenBuildings;
			var fromMinusD = (from - new Vector3I(d, d, d)).Clamp(Vector3I.Zero, maxPixel);
			var toPlusD = (to + new Vector3I(d, d, d)).Clamp(Vector3I.Zero, maxPixel);
			// Set constant height for a building and make a smooth transition to this height
			for (var x = fromMinusD.X; x <= toPlusD.X; x++)
			{
				for (var y = fromMinusD.Z; y <= toPlusD.Z; y++)
				{
					if (x >= from.X && x <= to.X && y >= from.Z && y <= to.Z)
					{
						heightMap.SetPixel(x, y, new Color(h, h, h));
					} else
					{
						var oldHeight = heightMap.GetPixel(x, y).R;
						var dX = (x < from.X ? from.X - x : (x > to.X ? x - to.X : 0));
						var dY = (y < from.Z ? from.Z - y : (y > to.Z ? y - to.Z : 0));
						var wX = (float)dX / d;
						var wY = (float)dY / d;
						var w = 1 - Mathf.Max(wX, wY);
						var newHeight = Mathf.Lerp(oldHeight, h,  w);
						heightMap.SetPixel(x, y, new Color(newHeight, newHeight, newHeight));
					}
				}
			}
		}

		GD.Print("Height map generated in ", (Time.GetTicksMsec() - begin), " ms");
		return heightMap;
	}

	private static float SquaredDistanceFromPointToTriangle(Vector2I point, Vector2I a, Vector2I b, Vector2I c)
	{
		if (Geometry2D.PointIsInsideTriangle(point, a, b, c))
		{
			return 0;
		}

		var d1 = (point - Geometry2D.GetClosestPointToSegment(point, a, b)).LengthSquared();
		var d2 = (point - Geometry2D.GetClosestPointToSegment(point, b, c)).LengthSquared();
		var d3 = (point - Geometry2D.GetClosestPointToSegment(point, a, c)).LengthSquared();
		var d = Mathf.Min(Mathf.Min(d1, d2), d3);
		return d;
	}
	
	private static float[,] GenerateSummedAreaTable(Image image) {
		int width = image.GetWidth();
		int height = image.GetHeight();
		float[,] sat = new float[width, height];
		
		// Generate SAT by accumulating values
		for (int y = 0; y < height; y++) {
			for (int x = 0; x < width; x++) {
				var pixelValue = image.GetPixel(x, y).R;
				var left = (x > 0 ? sat[x - 1, y] : 0);
				var above = (y > 0 ? sat[x, y - 1] : 0);
				var aboveLeft = (x > 0 && y > 0 ? sat[x - 1, y - 1] : 0);
				sat[x, y] = pixelValue + left + above - aboveLeft;
			}
		}

		return sat;
	}

	// Method to get the sum of a region from the SAT
	private static float GetAreaSum(float[,] sat, int x0, int y0, int x1, int y1) {
		var a = (x0 > 0 && y0 > 0 ? sat[x0 - 1, y0 - 1] : 0);
		var b = (y0 > 0 ? sat[x1, y0 - 1] : 0);
		var c = (x0 > 0 ? sat[x0 - 1, y1] : 0);
		var d = sat[x1, y1];
		return d + a - b - c;
	}

}