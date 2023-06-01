using Godot;
using System;

public class Tensor
{
    private bool oldTheta;
    private double _theta;
    private double r;
    private double[] matrix;

    public static Tensor Zero => new Tensor(0, new double[] { 0, 0 });

    public double Theta
    {
        get
        {
            if (oldTheta)
            {
                this._theta = this.CalculateTheta();
                this.oldTheta = false;
            }

            return _theta;
        }
    }

    public Tensor(double r, double[] matrix)
    {
        // Represent the matrix as a 2 element list
        // [ 0, 1
        //   1, -0 ]
        this.r = r;
        this.matrix = matrix;
        this.oldTheta = false;
        this._theta = this.CalculateTheta();
    }

    public static Tensor FromAngle(double angle)
    {
        return new Tensor(1, new double[] { Math.Cos(angle * 4), Math.Sin(angle * 4) });
    }

    public static Tensor FromVector(Vector2 vector)
    {
        double t1 = Math.Pow(vector.X, 2) - Math.Pow(vector.Y, 2);
        double t2 = 2 * vector.X * vector.Y;
        double t3 = Math.Pow(t1, 2) - Math.Pow(t2, 2);
        double t4 = 2 * t1 * t2;
        return new Tensor(1, new double[] { t3, t4 });
    }

    public Tensor Add(Tensor tensor, bool smooth)
    {
        for (int i = 0; i < this.matrix.Length; i++)
        {
            this.matrix[i] = this.matrix[i] * r + tensor.matrix[i] * tensor.r;
        }

        if (smooth)
        {
            this.r = Math.Sqrt(Math.Pow(this.matrix[0], 2) + Math.Pow(this.matrix[1], 2));
            this.matrix[0] /= this.r;
            this.matrix[1] /= this.r;
        } else
        {
            this.r = 2;
        }

        this.oldTheta = true;
        return this;
    }

    public Tensor Scale(double s)
    {
        this.r *= s;
        this.oldTheta = true;
        return this;
    }

    // Radians
    public Tensor Rotate(double theta)
    {
        if (theta == 0)
        {
            return this;
        }

        double newTheta = this.Theta + theta;
        if (newTheta < Math.PI)
        {
            newTheta += Math.PI;
        }

        if (newTheta >= Math.PI)
        {
            newTheta -= Math.PI;
        }

        matrix[0] = Math.Cos(2 * newTheta) * r;
        matrix[1] = Math.Sin(2 * newTheta) * r;
        _theta = newTheta;
        return this;
    }

    public Vector2 GetMajor()
    {
        // Degenerate case
        if (r == 0)
        {
            return Vector2.Zero;
        }

        return new Vector2((float)Math.Cos(Theta), (float)Math.Sin(Theta));
    }

    public Vector2 GetMinor()
    {
        // Degenerate case
        if (r == 0)
        {
            return Vector2.Zero;
        }

        double angle = Theta + Math.PI / 2;
        return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
    }

    private double CalculateTheta()
    {
        if (r == 0)
        {
            return 0;
        }

        return Math.Atan2(this.matrix[1] / this.r, this.matrix[0] / this.r) / 2;
    }
}