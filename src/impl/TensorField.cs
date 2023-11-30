using Godot;
using System;
using System.Collections.Generic;

public struct NoiseParams
{
    public bool GlobalNoise;
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

    public Tensor SamplePoint(Vector2 point)
    {
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
}