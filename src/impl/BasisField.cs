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
    public abstract int FIELD_TYPE { get; }
    public Vector2 Centre;
    protected double size;
    protected double decay;

    public BasisField(Vector2 centre, double size, double decay)
    {
        this.Centre = centre;
        this.size = size;
        this.decay = decay;
    }

    public double Decay
    {
        set { this.decay = value; }
    }

    public double Size
    {
        set { this.size = value; }
    }

    public abstract Tensor GetTensor(Vector2 point);

    public Tensor GetWeightedTensor(Vector2 point, bool smooth)
    {
        return this.GetTensor(point).Scale(this.GetTensorWeight(point, smooth));
    }

    /**
     * Interpolates between (0 and 1)^decay
     */
    protected double GetTensorWeight(Vector2 point, bool smooth)
    {
        double normDistanceToCentre = (point - this.Centre).Length() / this.size;
        if (smooth)
        {
            return Math.Pow(normDistanceToCentre, -this.decay);
        }
        // Stop (** 0) turning weight into 1, filling screen even when outside 'size'
        if (this.decay == 0 && normDistanceToCentre >= 1)
        {
            return 0;
        }
        return Math.Max(0, Math.Pow(1 - normDistanceToCentre, this.decay));
    }
}

public class Grid : BasisField
{
    public override int FIELD_TYPE => (int)FieldType.Grid;
    private double _theta;

    public Grid(Vector2 centre, double size, double decay, double theta) : base(centre, size, decay)
    {
        this._theta = theta;
    }

    public double Theta
    {
        set { this._theta = value; }
    }

    public override Tensor GetTensor(Vector2 point)
    {
        double cos = Math.Cos(2 * this._theta);
        double sin = Math.Sin(2 * this._theta);
        return new Tensor(1, new double[] { cos, sin });
    }
}

public class Radial : BasisField
{
    public override int FIELD_TYPE => (int)FieldType.Radial;

    public Radial(Vector2 centre, double size, double decay) : base(centre, size, decay)
    {
    }

    public override Tensor GetTensor(Vector2 point)
    {
        Vector2 t = point - this.Centre;
        double t1 = Math.Pow(t.Y, 2) - Math.Pow(t.X, 2);
        double t2 = -2 * t.X * t.Y;
        return new Tensor(1, new double[] { t1, t2 });
    }
}
