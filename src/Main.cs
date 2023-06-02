using System;
using System.Collections.Generic;
using Godot;

public partial class Main : Node2D
{
    private const float TensorLineDiameter = 20;
    private Vector2 _origin = Vector2.Zero;
    private Vector2 _worldDimensions = new Vector2(1920, 1080);
    private TensorField _tensorField;
    private StreamlineGenerator _streamlines;
    private bool _end;

    public override void _Ready()
    {
        GenerateTensorField();
        StartCreatingStreamlines();
    }

    public override void _Process(double delta)
    {
        if (_end) return;
        
        for (int i = 0; i < 5; i++) {
            if (!_streamlines.Update())
            {
                _end = true;
                GD.Print("End of generation");
            }
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        //DrawTensorField();
        DrawRoads();
    }

    void GenerateTensorField()
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
        _tensorField.AddGrid(new Vector2(264, 134), 376, 49, 14);
        _tensorField.AddRadial(new Vector2(426, 246), 286, 22);
    }

    private void StartCreatingStreamlines()
    {
        StreamlineParams parameters = new StreamlineParams
        {
            Dsep = 100,
            Dtest = 30,
            Dstep = 1,
            DLookahead = 200,
            DCircleJoin = 5,
            JoinAngle = 0.1f, // approx 30deg
            PathIterations = 2304,
            SeedTries = 300,
            SimplifyTolerance = 0.5f,
            CollideEarly = 0,
        };
        var integrator = new Rk4Integrator(_tensorField, parameters);
        _streamlines = new StreamlineGenerator(integrator, _origin, _worldDimensions, parameters);
        _streamlines.StartCreatingStreamlines();
    }

    private void DrawRoads()
    {
        var roads = _streamlines.AllStreamlinesSimple;
        Color color1 = Color.Color8(255, 255, 255);
        Color color2 = Color.Color8(0, 0, 0);
        foreach (var road in roads)
        {
            DrawPolyline(road.ToArray(), color2, 5);
            DrawPolyline(road.ToArray(), color1, 4);
        }
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

    private List<Vector2> GetCrossLocations()
    {
        /*List<Vector2> crossLocations = new List<Vector2>();
        crossLocations.Add(new Vector2(15.5f, 15.5f));
        return crossLocations;*/
        
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