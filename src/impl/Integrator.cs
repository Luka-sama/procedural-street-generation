using Godot;

public abstract class FieldIntegrator
{
    protected TensorField field;

    public FieldIntegrator(TensorField field)
    {
        this.field = field;
    }

    public abstract Vector2 Integrate(Vector2 point, bool major);

    protected Vector2 SampleFieldVector(Vector2 point, bool major)
    {
        Tensor tensor = this.field.SamplePoint(point);
        if (major)
            return tensor.GetMajor();
        return tensor.GetMinor();
    }

    public bool OnLand(Vector2 point)
    {
        return this.field.OnLand(point);
    }
}

public class EulerIntegrator : FieldIntegrator
{
    private StreamlineParams parameters;

    public EulerIntegrator(TensorField field, StreamlineParams parameters) : base(field)
    {
        this.parameters = parameters;
    }

    public override Vector2 Integrate(Vector2 point, bool major)
    {
        return this.SampleFieldVector(point, major) * parameters.Dstep;
    }
}

public class RK4Integrator : FieldIntegrator
{
    private StreamlineParams parameters;

    public RK4Integrator(TensorField field, StreamlineParams parameters) : base(field)
    {
        this.parameters = parameters;
    }

    public override Vector2 Integrate(Vector2 point, bool major)
    {
        float dstep = parameters.Dstep;
        Vector2 k1 = this.SampleFieldVector(point, major);
        Vector2 k23 = this.SampleFieldVector(point + new Vector2(dstep / 2, dstep / 2), major);
        Vector2 k4 = this.SampleFieldVector(point + new Vector2(dstep, dstep), major);

        return (k1 + 4 * k23 + k4) * (dstep / 6);
    }
}