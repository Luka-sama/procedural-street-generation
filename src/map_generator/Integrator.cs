using Godot;

public abstract class FieldIntegrator
{
    private readonly TensorField _field;

    protected FieldIntegrator(TensorField field)
    {
        _field = field;
    }

    public abstract Vector2 Integrate(Vector2 point, bool major);

    protected Vector2 SampleFieldVector(Vector2 point, bool major)
    {
        var tensor = _field.SamplePoint(point);
        return (major ? tensor.GetMajor() : tensor.GetMinor());
    }
}

// ReSharper disable once InconsistentNaming
public class RK4Integrator : FieldIntegrator
{
    private readonly StreamlineParams _parameters;

    public RK4Integrator(TensorField field, StreamlineParams parameters) : base(field)
    {
        _parameters = parameters;
    }

    public override Vector2 Integrate(Vector2 point, bool major)
    {
        var dstep = _parameters.Dstep;
        var k1 = SampleFieldVector(point, major);
        var k23 = SampleFieldVector(point + new Vector2(dstep / 2, dstep / 2), major);
        var k4 = SampleFieldVector(point + new Vector2(dstep, dstep), major);

        return (k1 + 4 * k23 + k4) * (dstep / 6);
    }
}