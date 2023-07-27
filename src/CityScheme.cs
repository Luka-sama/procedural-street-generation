using System;
using System.Collections.Generic;
using Godot;

public partial class CityScheme : Node2D
{
    public Vector2 UserPosition { get; set; } = Vector2.Zero;
    public bool ShouldDrawTensorField { get; set; } = false;
    private const float TensorLineDiameter = 20;
    private Vector2 _origin = Vector2.Zero;
    private Vector2 _worldDimensions = new Vector2(1920, 1080);
    private TensorField _tensorField;
    private StreamlineGenerator _mainStreamlines;
    private StreamlineGenerator _majorStreamlines;
    private StreamlineGenerator _minorStreamlines;
    private bool _end;

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

    public List<List<Vector2>> GetMainRoads()
    {
        return _mainStreamlines.AllStreamlinesSimple;
    }
    
    public List<List<Vector2>> GetMajorRoads()
    {
        return _majorStreamlines.AllStreamlinesSimple;
    }
    
    public List<List<Vector2>> GetMinorRoads()
    {
        return _minorStreamlines.AllStreamlinesSimple;
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
        StreamlineParams parameters = new StreamlineParams
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
        Color polylineColor = Color.Color8(255, 255, 255, 50);
        Color pointColor = Colors.Aqua;
        
        /*foreach (var road in GetMainRoads())
        {
            DrawPolylineWithPoints(road.ToArray(), polylineColor, pointColor, 8);
        }*/
        foreach (var road in GetMajorRoads())
        {
            DrawPolylineWithPoints(road.ToArray(), polylineColor, pointColor, 4);
        }
        /*foreach (var road in GetMinorRoads())
        {
            DrawPolylineWithPoints(road.ToArray(), polylineColor, pointColor, 2);
        }*/
    }

    private void DrawTensorField()
    {
        Color color = Color.Color8(255, 0, 0);
        
        List<Vector2> tensorPoints = GetCrossLocations();
        foreach (var p in tensorPoints)
        {
            var t = _tensorField.SamplePoint(p);
            DrawPolyline(GetTensorLine(p, t.GetMajor()), color, 3);
            DrawPolyline(GetTensorLine(p, t.GetMinor()), color, 3);
        }
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
        
        float zoom = 1;
        float diameter = TensorLineDiameter / zoom;
        int nHor = (int)Math.Ceiling(_worldDimensions.X / diameter) + 1; // Prevent pop-in
        int nVer = (int)Math.Ceiling(_worldDimensions.Y / diameter) + 1;
        float originX = diameter * (float)Math.Floor(_origin.X / diameter);
        float originY = diameter * (float)Math.Floor(_origin.Y / diameter);

        List<Vector2> outList = new List<Vector2>();
        for (int x = 0; x <= nHor; x++)
        {
            for (int y = 0; y <= nVer; y++)
            {
                outList.Add(new Vector2(originX + (x * diameter), originY + (y * diameter)));
            }
        }

        return outList;
    }
    
    private Vector2[] GetTensorLine(Vector2 point, Vector2 tensorV)
    {
        Vector2 transformedPoint = point; //domainController.worldToScreen(point);
        Vector2 diff = tensorV * (TensorLineDiameter / 2);  // Assumes normalised
        Vector2 start = transformedPoint - diff;
        Vector2 end = transformedPoint + diff;
        return new[] {start, end};
    }
}