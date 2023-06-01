using Godot;
using System;
using System.Collections.Generic;

public struct NoiseParams
{
    public bool globalNoise;
    public double noiseSizePark;
    public double noiseAnglePark; // Degrees
    public double noiseSizeGlobal;
    public double noiseAngleGlobal;
}

/**
 * Combines basis fields
 * Noise added when sampling a point in a park
 */
public class TensorField
{
    private List<BasisField> basisFields = new List<BasisField>();
    private FastNoiseLite noise;

    public List<List<Vector2>> parks = new List<List<Vector2>>();
    public List<Vector2> sea = new List<Vector2>();
    public List<Vector2> river = new List<Vector2>();
    public bool ignoreRiver = false;

    public bool smooth = false;

    public NoiseParams noiseParams;

    public TensorField(NoiseParams noiseParams)
    {
        this.noiseParams = noiseParams;
        this.noise = new FastNoiseLite();
        this.noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
    }

    public void EnableGlobalNoise(double angle, double size)
    {
        noiseParams.globalNoise = true;
        noiseParams.noiseAngleGlobal = angle;
        noiseParams.noiseSizeGlobal = size;
    }

    public void DisableGlobalNoise()
    {
        noiseParams.globalNoise = false;
    }

    public void AddGrid(Vector2 centre, double size, double decay, double theta)
    {
        var grid = new Grid(centre, size, decay, theta);
        this.AddField(grid);
    }

    public void AddRadial(Vector2 centre, double size, double decay)
    {
        var radial = new Radial(centre, size, decay);
        this.AddField(radial);
    }

    protected void AddField(BasisField field)
    {
        this.basisFields.Add(field);
    }

    protected void RemoveField(BasisField field)
    {
        this.basisFields.Remove(field);
    }

    public void Reset()
    {
        this.basisFields.Clear();
        this.parks.Clear();
        this.sea.Clear();
        this.river.Clear();
    }

    public List<Vector2> GetCentrePoints()
    {
        var centrePoints = new List<Vector2>();
        foreach (var basisField in this.basisFields)
        {
            centrePoints.Add(basisField.Centre);
        }
        return centrePoints;
    }

    public List<BasisField> GetBasisFields()
    {
        return basisFields;
    }

    public Tensor SamplePoint(Vector2 point)
    {
        if (!this.OnLand(point))
        {
            // Degenerate point
            return Tensor.Zero;
        }

        // Default field is a grid
        if (this.basisFields.Count == 0)
        {
            return new Tensor(1, new double[] { 0, 0 });
        }

        var tensorAcc = Tensor.Zero;
        foreach (var field in this.basisFields)
        {
            tensorAcc.Add(field.GetWeightedTensor(point, smooth), smooth);
        }

        // Add rotational noise for parks - range -pi/2 to pi/2
        foreach (var park in this.parks)
        {
            if (PolygonUtil.InsidePolygon(point, park))
            {
                tensorAcc.Rotate(this.GetRotationalNoise(point, this.noiseParams.noiseSizePark, this.noiseParams.noiseAnglePark));
                break;
            }
        }

        if (this.noiseParams.globalNoise)
        {
            tensorAcc.Rotate(this.GetRotationalNoise(point, this.noiseParams.noiseSizeGlobal, this.noiseParams.noiseAngleGlobal));
        }

        return tensorAcc;
    }

    /**
     * Noise Angle is in degrees
     */
    public double GetRotationalNoise(Vector2 point, double noiseSize, double noiseAngle)
    {
        return noise.GetNoise2D((float) (point.X / noiseSize), (float)(point.Y / noiseSize)) * noiseAngle * Math.PI / 180;
    }

    public bool OnLand(Vector2 point)
    {
        bool inSea = PolygonUtil.InsidePolygon(point, this.sea);
        if (this.ignoreRiver)
        {
            return !inSea;
        }

        return !inSea && !PolygonUtil.InsidePolygon(point, this.river);
    }

    public bool InParks(Vector2 point)
    {
        foreach (var park in this.parks)
        {
            if (PolygonUtil.InsidePolygon(point, park))
            {
                return true;
            }
        }
        return false;
    }
}