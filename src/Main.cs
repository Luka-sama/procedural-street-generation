using System;
using System.Collections.Generic;
using Godot;

public partial class Main : Node2D
{
    private const float TENSOR_LINE_DIAMETER = 20;
    private Vector2 origin = Vector2.Zero;
    private Vector2 worldDimensions = new Vector2(500, 500);
    private TensorField tensorField;
    private StreamlineGenerator streamlines;
    private bool end = false;

    public override void _Ready()
    {
        this.GenerateTensorField();
        this.StartCreatingStreamlines();
    }

    public override void _Process(double delta)
    {
        if (end) return;
        
        for (int i = 0; i < 5; i++) {
            if (!this.streamlines.Update())
            {
                end = true;
                GD.Print("End of generation");
            }
        }
        this.QueueRedraw();
    }

    public override void _Draw()
    {
        //this.DrawTensorField();
        this.DrawRoads();
    }

    void GenerateTensorField()
    {
        var noiseParams = new NoiseParams
        {
            globalNoise = false,
            noiseAngleGlobal = 0,
            noiseAnglePark = 0,
            noiseSizeGlobal = 0,
            noiseSizePark = 0
        };

        this.tensorField = new(noiseParams);
        this.tensorField.AddGrid(new Vector2(264, 134), 376, 49, 14);
        this.tensorField.AddRadial(new Vector2(426, 246), 286, 22);
    }

    public void StartCreatingStreamlines()
    {
        StreamlineParams parameters = new StreamlineParams
        {
            Dsep = 400,
            Dtest = 200,
            Dstep = 1,
            DLookahead = 500,
            DCircleJoin = 5,
            JoinAngle = 0.1f, // approx 30deg
            PathIterations = 10,
            SeedTries = 300,
            SimplifyTolerance = 0.5f,
            CollideEarly = 0,
        };
        var integrator = new RK4Integrator(this.tensorField, parameters);
        this.streamlines = new StreamlineGenerator(integrator, this.origin, this.worldDimensions, parameters);
        this.streamlines.StartCreatingStreamlines();
    }

    public void DrawRoads()
    {
        var roads = streamlines.allStreamlinesSimple;
        Color color1 = Color.Color8(255, 255, 255, 255);
        Color color2 = Color.Color8(0, 0, 0, 255);
        foreach (var road in roads)
        {
            this.DrawPolyline(road.ToArray(), color2, 5);
            this.DrawPolyline(road.ToArray(), color1, 4);
        }
    }

    public void DrawTensorField()
    {
        Color color = Color.Color8(255, 0, 0, 255);
        
        List<Vector2> tensorPoints = this.GetCrossLocations();
        foreach (var p in tensorPoints)
        {
            var t = this.tensorField.SamplePoint(p);
            this.DrawPolyline(this.GetTensorLine(p, t.GetMajor()), color, 3);
            this.DrawPolyline(this.GetTensorLine(p, t.GetMinor()), color, 3);
        }
    }

    private List<Vector2> GetCrossLocations()
    {
        /*List<Vector2> crossLocations = new List<Vector2>();
        crossLocations.Add(new Vector2(15.5f, 15.5f));
        return crossLocations;*/
        
        // Gets grid of points for vector field vis in world space
        
        float zoom = 1;
        float diameter = TENSOR_LINE_DIAMETER / zoom;
        int nHor = (int)Math.Ceiling(this.worldDimensions.X / diameter) + 1; // Prevent pop-in
        int nVer = (int)Math.Ceiling(this.worldDimensions.Y / diameter) + 1;
        float originX = diameter * (float)Math.Floor(this.origin.X / diameter);
        float originY = diameter * (float)Math.Floor(this.origin.Y / diameter);

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
        Vector2 transformedPoint = point; //this.domainController.worldToScreen(point);
        Vector2 diff = tensorV * (TENSOR_LINE_DIAMETER / 2);  // Assumes normalised
        Vector2 start = transformedPoint - diff;
        Vector2 end = transformedPoint + diff;
        return new Vector2[] {start, end};
    }
}