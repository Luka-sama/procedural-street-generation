using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class CityScheme : Node2D
{
    public Vector2 UserPosition { get; set; } = Vector2.Zero;
    public bool ShouldDrawTensorField { get; set; }
    public bool WithGraph { get; set; }
    private const float TensorLineDiameter = 20;
    private Vector2 _origin = Vector2.Zero;
    private Vector2 _worldDimensions = new Vector2(1920, 1080);
    private TensorField _tensorField;
    private StreamlineGenerator _mainStreamlines;
    private StreamlineGenerator _majorStreamlines;
    private StreamlineGenerator _minorStreamlines;
    private bool _end;
    private Graph _graph;

    public override void _Ready()
    {
        GenerateTensorField();
        StartCreatingStreamlines();
    }

    public override void _Process(double delta)
    {
        if (_end) return;
        
        for (int i = 0; i < 5 && !_end; i++) {
            if (!_mainStreamlines.Update() && !_majorStreamlines.Update() && !_minorStreamlines.Update())
            {
                _end = true;
                GD.Print("End of generation");
                _graph = Graph.CreateFromStreamlines(GetMajorRoads());
                QueueRedraw();
            }
        }
    }

    public override void _Draw()
    {
        if (ShouldDrawTensorField) DrawTensorField();
        else DrawRoads();
        
        DrawCircle(UserPosition, 10, Colors.LightGreen);
    }

    private List<List<Vector2>> GetMainRoads()
    {
        return _mainStreamlines.AllStreamlinesSimple;
    }
    
    private List<List<Vector2>> GetMajorRoads()
    {
        /*return new()
        {
            new() {new(2, 2), new(200, 200), new(400, 500)},
            new() {new(0, 350), new(350, 350)},
            new() {new(2, 500), new(200, 400), new(500, 2)},
            new() {new(70, 420), new(300, 400)}
        };*/
        /*foreach (var streamline in _majorStreamlines.AllStreamlinesSimple)
        {
            var str = "new() {";
            foreach (var point in streamline)
            {
                str += "new(" + point.X.ToString().Replace(",", ".") + "f, " + point.Y.ToString().Replace(",", ".") + "f), ";
            }
            str = str.Substr(0, str.Length - 2) + "},";
            GD.Print(str);
        }*/
        return _majorStreamlines.AllStreamlinesSimple;
    }
    
    private List<List<Vector2>> GetMinorRoads()
    {
        return _minorStreamlines.AllStreamlinesSimple;
    }

    public Graph GetGraph()
    {
        return _graph;
    }

    private void GenerateTensorField()
    {
        var noiseParams = new NoiseParams
        {
            GlobalNoise = false,
            NoiseAngleGlobal = 0,
            NoiseAnglePark = 0,
            NoiseSizeGlobal = 0,
            NoiseSizePark = 0
        };

        _tensorField = new(noiseParams);
        for (int i = 0; i < 10; i++)
        {
            var centre = new Vector2((float)GD.RandRange(0, _worldDimensions.X - 1), _worldDimensions.Y / (i + 1));
            var size = GD.RandRange(_worldDimensions.X / 10, _worldDimensions.X / 4);
            
            var decay = 0;//GD.RandRange(0, 50);
            if (i < 4)
            {
                var theta = GD.RandRange(0, Math.PI / 2);
                _tensorField.AddGrid(centre, size, decay, theta);
            } else
            {
                _tensorField.AddRadial(centre, size, decay);
            }
        }
    }

    private void StartCreatingStreamlines()
    {
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
        
        _mainStreamlines = new StreamlineGenerator(integrator, _origin, _worldDimensions, parameters);
        _mainStreamlines.StartCreatingStreamlines();

        parameters.Dsep = 100;
        parameters.Dtest = 30;
        parameters.DLookahead = 200;
        _majorStreamlines = new StreamlineGenerator(integrator, _origin, _worldDimensions, parameters);
        _majorStreamlines.StartCreatingStreamlines();

        parameters.Dsep = 20;
        parameters.Dtest = 15;
        parameters.DLookahead = 40;
        _minorStreamlines = new StreamlineGenerator(integrator, _origin, _worldDimensions, parameters);
        _minorStreamlines.StartCreatingStreamlines();
    }

    private void DrawRoads()
    {
        if (_graph == null)
        {
            return;
        }
        var polylineColor = Color.Color8(255, 255, 255, 50);
        var pointColor = Colors.Aqua;
        
        /*var buildingGenerator = GetNode<BuildingGenerator>("%BuildingGenerator");
        var polygons = buildingGenerator.Generate(_graph);
        foreach (var polygon in polygons)
        {
            var color = new Color(GD.Randf(), GD.Randf(), GD.Randf());
            DrawColoredPolygon(polygon.Select(v => (Vector2)v).ToArray(), color);
        }*/
        
        /*foreach (var road in GetMainRoads())
        {
            DrawPolylineWithPoints(road.ToArray(), polylineColor, pointColor, 8);
        }*/
        if (WithGraph)
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
        /*foreach (var road in GetMinorRoads())
        {
            DrawPolylineWithPoints(road.ToArray(), polylineColor, pointColor, 2);
        }*/
    }

    private void DrawTensorField()
    {
        var color = Colors.Red;
        
        List<Vector2> tensorPoints = GetCrossLocations();
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
        var nHor = Mathf.CeilToInt(_worldDimensions.X / diameter) + 1; // Prevent pop-in
        var nVer = Mathf.CeilToInt(_worldDimensions.Y / diameter) + 1;
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
        var transformedPoint = point; //domainController.worldToScreen(point);
        var diff = tensorV * (TensorLineDiameter / 2);  // Assumes normalised
        var start = transformedPoint - diff;
        var end = transformedPoint + diff;
        return new[] {start, end};
    }
}