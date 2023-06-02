using Godot;
using System;
using System.Collections.Generic;

public struct NoiseParams
{
    public bool GlobalNoise;
    public double NoiseSizePark;
    public double NoiseAnglePark; // Degrees
    public double NoiseSizeGlobal;
    public double NoiseAngleGlobal;
}

/**
 * Combines basis fields
 * Noise added when sampling a point in a park
 */
public class TensorField
{
    private readonly List<BasisField> _basisFields = new();
    private readonly FastNoiseLite _noise;

    private readonly List<List<Vector2>> _parks = new();
    private readonly List<Vector2> _sea = new();
    private readonly List<Vector2> _river = new();
    private readonly bool _ignoreRiver = false;

    private readonly bool _smooth = false;

    private NoiseParams _noiseParams;

    public TensorField(NoiseParams noiseParams)
    {
        _noiseParams = noiseParams;
        _noise = new FastNoiseLite();
        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
    }

    public void EnableGlobalNoise(double angle, double size)
    {
        _noiseParams.GlobalNoise = true;
        _noiseParams.NoiseAngleGlobal = angle;
        _noiseParams.NoiseSizeGlobal = size;
    }

    public void DisableGlobalNoise()
    {
        _noiseParams.GlobalNoise = false;
    }

    public void AddGrid(Vector2 centre, double size, double decay, double theta)
    {
        var grid = new Grid(centre, size, decay, theta);
        AddField(grid);
    }

    public void AddRadial(Vector2 centre, double size, double decay)
    {
        var radial = new Radial(centre, size, decay);
        AddField(radial);
    }

    private void AddField(BasisField field)
    {
        _basisFields.Add(field);
    }

    protected void RemoveField(BasisField field)
    {
        _basisFields.Remove(field);
    }

    public void Reset()
    {
        _basisFields.Clear();
        _parks.Clear();
        _sea.Clear();
        _river.Clear();
    }

    public List<Vector2> GetCentrePoints()
    {
        var centrePoints = new List<Vector2>();
        foreach (var basisField in _basisFields)
        {
            centrePoints.Add(basisField.Centre);
        }
        return centrePoints;
    }

    public List<BasisField> GetBasisFields()
    {
        return _basisFields;
    }

    public Tensor SamplePoint(Vector2 point)
    {
        if (!OnLand(point))
        {
            // Degenerate point
            return Tensor.Zero;
        }

        // Default field is a grid
        if (_basisFields.Count == 0)
        {
            return new Tensor(1, new double[] { 0, 0 });
        }

        var tensorAcc = Tensor.Zero;
        foreach (var field in _basisFields)
        {
            tensorAcc.Add(field.GetWeightedTensor(point, _smooth), _smooth);
        }

        // Add rotational noise for parks - range -pi/2 to pi/2
        foreach (var park in _parks)
        {
            if (PolygonUtil.InsidePolygon(point, park))
            {
                tensorAcc.Rotate(GetRotationalNoise(point, _noiseParams.NoiseSizePark, _noiseParams.NoiseAnglePark));
                break;
            }
        }

        if (_noiseParams.GlobalNoise)
        {
            tensorAcc.Rotate(GetRotationalNoise(point, _noiseParams.NoiseSizeGlobal, _noiseParams.NoiseAngleGlobal));
        }

        return tensorAcc;
    }

    /**
     * Noise Angle is in degrees
     */
    private double GetRotationalNoise(Vector2 point, double noiseSize, double noiseAngle)
    {
        return _noise.GetNoise2D((float) (point.X / noiseSize), (float)(point.Y / noiseSize)) * noiseAngle * Math.PI / 180;
    }

    public bool OnLand(Vector2 point)
    {
        bool inSea = PolygonUtil.InsidePolygon(point, _sea);
        if (_ignoreRiver)
        {
            return !inSea;
        }

        return !inSea && !PolygonUtil.InsidePolygon(point, _river);
    }

    public bool InParks(Vector2 point)
    {
        foreach (var park in _parks)
        {
            if (PolygonUtil.InsidePolygon(point, park))
            {
                return true;
            }
        }
        return false;
    }
}