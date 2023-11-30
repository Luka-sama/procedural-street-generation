using Godot;
using System;

/** Grid or Radial field to be combined with others to create the tensor field */
public abstract class BasisField
{
    protected Vector2 Centre;
    private readonly double _size;
    private readonly double _decay;

    protected BasisField(Vector2 centre, double size, double decay)
    {
        Centre = centre;
        _size = size;
        _decay = decay;
    }

    protected abstract Tensor GetTensor(Vector2 point);

    public Tensor GetWeightedTensor(Vector2 point, bool smooth)
    {
        return GetTensor(point).Scale(GetTensorWeight(point, smooth));
    }

    /** Interpolates between (0 and 1)^decay */
    private double GetTensorWeight(Vector2 point, bool smooth)
    {
        var normDistanceToCentre = (point - Centre).Length() / _size;
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
    private readonly double _theta;

    public Grid(Vector2 centre, double size, double decay, double theta) : base(centre, size, decay)
    {
        _theta = theta;
    }

    protected override Tensor GetTensor(Vector2 point)
    {
        var cos = Math.Cos(2 * _theta);
        var sin = Math.Sin(2 * _theta);
        return new Tensor(1, new[] { cos, sin });
    }
}

public class Radial : BasisField
{
    public Radial(Vector2 centre, double size, double decay) : base(centre, size, decay)
    {
    }

    protected override Tensor GetTensor(Vector2 point)
    {
        var t = point - Centre;
        var t1 = Math.Pow(t.Y, 2) - Math.Pow(t.X, 2);
        var t2 = -2 * t.X * t.Y;
        return new Tensor(1, new[] { t1, t2 });
    }
}
