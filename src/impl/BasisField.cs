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
    public double Size { get; }
    public double Decay { get; }

    protected BasisField(Vector2 centre, double size, double decay)
    {
        Centre = centre;
        Size = size;
        Decay = decay;
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
        var normDistanceToCentre = (point - Centre).Length() / Size;
        if (smooth)
        {
            return Math.Pow(normDistanceToCentre, -Decay);
        }
        // Stop (** 0) turning weight into 1, filling screen even when outside 'size'
        if (Decay == 0 && normDistanceToCentre >= 1)
        {
            return 0;
        }
        return Math.Max(0, Math.Pow(1 - normDistanceToCentre, Decay));
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
        var cos = Math.Cos(2 * _theta);
        var sin = Math.Sin(2 * _theta);
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
