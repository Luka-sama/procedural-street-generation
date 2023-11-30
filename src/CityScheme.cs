using System;
using System.Collections.Generic;
using Godot;

public enum SchemeState
{
    TensorField,
    Streamlines,
    Graph
}

public partial class CityScheme : Node2D
{
    public Vector2 UserPosition { get; set; } = Vector2.Zero;
    public SchemeState SchemeState { get; set; }
    private const float TensorLineDiameter = 20;
    private Vector2 _origin = Vector2.Zero;
    private TensorField _tensorField;
    private StreamlineGenerator _majorStreamlines;
    private Graph _graph;
    private ModelGenerator _modelGenerator;

    public override void _Ready()
    {
        _modelGenerator = GetNode<ModelGenerator>("%ModelGenerator");
        GenerateTensorField();
        GenerateStreamlines();
    }

    public override void _Draw()
    {
        if (SchemeState == SchemeState.TensorField) DrawTensorField();
        else DrawRoads();
        
        DrawCircle(UserPosition, 10, Colors.LightGreen);
    }
    
    private List<List<Vector2>> GetMajorRoads()
    {
        return _majorStreamlines.AllStreamlinesSimple;
    }

    private void GenerateTensorField()
    {
        var begin = Time.GetTicksMsec();
        
        var noiseParams = new NoiseParams
        {
            GlobalNoise = true,
            NoiseAngleGlobal = GD.RandRange(30, 60),
            NoiseSizeGlobal = 2,
        };

        _tensorField = new(noiseParams);
        var fieldCount = 10;
        for (var i = 0; i < fieldCount; i++)
        {
            var centre = new Vector2(
                (float)GD.RandRange(0, TerrainGenerator.Size.X - 1),
                Mathf.Lerp(0f, TerrainGenerator.Size.Y - 1, (float)i / (fieldCount - 1))
            );
            var size = GD.RandRange(TerrainGenerator.Size.X / 10, TerrainGenerator.Size.X / 4);
            
            if (GD.RandRange(1, 10) <= 3)
            {
                var theta = GD.RandRange(0, Math.PI / 2);
                _tensorField.AddGrid(centre, size, 0, theta);
            } else
            {
                _tensorField.AddRadial(centre, size, 0);
            }
        }
        
        GD.Print("Tensor field generated in ", (Time.GetTicksMsec() - begin), " ms");
    }

    private void GenerateStreamlines()
    {
        var begin = Time.GetTicksMsec();
        
        var parameters = new StreamlineParams
        {
            Dsep = 400,
            Dtest = 200,
            Dstep = 1,
            DLookahead = 500,
            DCircleJoin = 5,
            JoinAngle = 0.1f, // approx 30deg
            PathIterations = 2304,
            SeedTries = 300,
            SimplifyTolerance = 0.5f,
            CollideEarly = 0,
        };
        var integrator = new RK4Integrator(_tensorField, parameters);

        parameters.Dsep = 100;
        parameters.Dtest = 30;
        parameters.DLookahead = 200;
        _majorStreamlines = new StreamlineGenerator(integrator, _origin, TerrainGenerator.Size, parameters);
        _majorStreamlines.CreateAllStreamlines();
        
        GD.Print("Streamlines generated in ", (Time.GetTicksMsec() - begin), " ms");
        
        _graph = Graph.CreateFromStreamlines(GetMajorRoads());
        _modelGenerator.Generate(_graph);
        QueueRedraw();
    }

    private void DrawRoads()
    {
        if (_graph == null)
        {
            return;
        }
        
        var polylineColor = Color.Color8(255, 255, 255, 50);
        var pointColor = Colors.Aqua;

        if (SchemeState == SchemeState.Graph)
        {
            foreach (var edge in _graph.Edges)
            {
                DrawLineWithPoints(edge.From.Point, edge.To.Point, polylineColor, pointColor, 4);
            }
        } else
        {
            foreach (var road in GetMajorRoads())
            {
                DrawPolylineWithPoints(road.ToArray(), polylineColor, pointColor, 4);
            }
        }
    }

    private void DrawTensorField()
    {
        var color = Colors.White;
        
        var tensorPoints = GetCrossLocations();
        foreach (var p in tensorPoints)
        {
            var t = _tensorField.SamplePoint(p);
            DrawPolyline(GetTensorLine(p, t.GetMajor()), color, 3);
            DrawPolyline(GetTensorLine(p, t.GetMinor()), color, 3);
        }
    }
    
    private void DrawLineWithPoints(Vector2 from, Vector2 to, Color lineColor, Color pointColor, float width)
    {
        DrawLine(from, to, lineColor, width);
        DrawCircle(from, width, pointColor);
        DrawCircle(to, width, pointColor);
    }

    private void DrawPolylineWithPoints(Vector2[] points, Color polylineColor, Color pointColor, float width)
    {
        DrawPolyline(points, polylineColor, width);
        foreach (var point in points)
        {
            DrawCircle(point, width, pointColor);
        }
    }

    private List<Vector2> GetCrossLocations()
    {
        // Gets grid of points for vector field vis in world space
        
        var zoom = 1f;
        var diameter = TensorLineDiameter / zoom;
        var nHor = Mathf.CeilToInt(TerrainGenerator.Size.X / diameter) + 1; // Prevent pop-in
        var nVer = Mathf.CeilToInt(TerrainGenerator.Size.Y / diameter) + 1;
        var originX = diameter * (float)Math.Floor(_origin.X / diameter);
        var originY = diameter * (float)Math.Floor(_origin.Y / diameter);

        var outList = new List<Vector2>();
        for (var x = 0; x <= nHor; x++)
        {
            for (var y = 0; y <= nVer; y++)
            {
                outList.Add(new Vector2(originX + (x * diameter), originY + (y * diameter)));
            }
        }

        return outList;
    }
    
    private Vector2[] GetTensorLine(Vector2 point, Vector2 tensorV)
    {
        var diff = tensorV * (TensorLineDiameter / 2);  // Assumes normalised
        var start = point - diff;
        var end = point + diff;
        return new[] {start, end};
    }
}