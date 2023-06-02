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
        Tensor tensor = _field.SamplePoint(point);
        if (major)
            return tensor.GetMajor();
        return tensor.GetMinor();
    }

    public bool OnLand(Vector2 point)
    {
        return _field.OnLand(point);
    }
}

public class EulerIntegrator : FieldIntegrator
{
    private readonly StreamlineParams _parameters;

    public EulerIntegrator(TensorField field, StreamlineParams parameters) : base(field)
    {
        _parameters = parameters;
    }

    public override Vector2 Integrate(Vector2 point, bool major)
    {
        return SampleFieldVector(point, major) * _parameters.Dstep;
    }
}

public class Rk4Integrator : FieldIntegrator
{
    private readonly StreamlineParams _parameters;

    public Rk4Integrator(TensorField field, StreamlineParams parameters) : base(field)
    {
        _parameters = parameters;
    }

    public override Vector2 Integrate(Vector2 point, bool major)
    {
        float dstep = _parameters.Dstep;
        Vector2 k1 = SampleFieldVector(point, major);
        Vector2 k23 = SampleFieldVector(point + new Vector2(dstep / 2, dstep / 2), major);
        Vector2 k4 = SampleFieldVector(point + new Vector2(dstep, dstep), major);

        return (k1 + 4 * k23 + k4) * (dstep / 6);
    }
}