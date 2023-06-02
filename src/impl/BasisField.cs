using Godot;
using System;

public enum FieldType
{
    Radial,
    Grid
}

/**
 * Grid or Radial field to be combined with others to create the tensor field
 */
public abstract class BasisField
{
    public abstract FieldType FieldType { get; }
    public Vector2 Centre { get; }
    private double _size;
    private double _decay;

    protected BasisField(Vector2 centre, double size, double decay)
    {
        Centre = centre;
        _size = size;
        _decay = decay;
    }

    public double Decay
    {
        set => _decay = value;
    }

    public double Size
    {
        set => _size = value;
    }

    protected abstract Tensor GetTensor(Vector2 point);

    public Tensor GetWeightedTensor(Vector2 point, bool smooth)
    {
        return GetTensor(point).Scale(GetTensorWeight(point, smooth));
    }

    /**
     * Interpolates between (0 and 1)^decay
     */
    private double GetTensorWeight(Vector2 point, bool smooth)
    {
        double normDistanceToCentre = (point - Centre).Length() / _size;
        if (smooth)
        {
            return Math.Pow(normDistanceToCentre, -_decay);
        }
        // Stop (** 0) turning weight into 1, filling screen even when outside 'size'
        if (_decay == 0 && normDistanceToCentre >= 1)
        {
            return 0;
        }
        return Math.Max(0, Math.Pow(1 - normDistanceToCentre, _decay));
    }
}

public class Grid : BasisField
{
    public override FieldType FieldType => FieldType.Grid;
    private double _theta;

    public Grid(Vector2 centre, double size, double decay, double theta) : base(centre, size, decay)
    {
        _theta = theta;
    }

    public double Theta
    {
        set => _theta = value;
    }

    protected override Tensor GetTensor(Vector2 point)
    {
        double cos = Math.Cos(2 * _theta);
        double sin = Math.Sin(2 * _theta);
        return new Tensor(1, new[] { cos, sin });
    }
}

public class Radial : BasisField
{
    public override FieldType FieldType => FieldType.Radial;

    public Radial(Vector2 centre, double size, double decay) : base(centre, size, decay)
    {
    }

    protected override Tensor GetTensor(Vector2 point)
    {
        Vector2 t = point - Centre;
        double t1 = Math.Pow(t.Y, 2) - Math.Pow(t.X, 2);
        double t2 = -2 * t.X * t.Y;
        return new Tensor(1, new[] { t1, t2 });
    }
}
