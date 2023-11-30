using Godot;
using System;

public class Tensor
{
    private bool _oldTheta;
    private double _theta;
    private double _r;
    private readonly double[] _matrix;
    private readonly double _angleBetweenRoads;

    public static Tensor Zero => new Tensor(0, new double[] { 0, 0 });

    private double Theta
    {
        get
        {
            if (_oldTheta)
            {
                _theta = CalculateTheta();
                _oldTheta = false;
            }

            return _theta;
        }
    }

    public Tensor(double r, double[] matrix)
    {
        // Represent the matrix as a 2 element list
        // [ 0, 1
        //   1, -0 ]
        _r = r;
        _matrix = matrix;
        _oldTheta = false;
        _theta = CalculateTheta();
        _angleBetweenRoads = Math.PI / 2;//GD.RandRange(2, 4);
    }

    public static Tensor FromAngle(double angle)
    {
        return new Tensor(1, new[] { Math.Cos(angle * 4), Math.Sin(angle * 4) });
    }

    public static Tensor FromVector(Vector2 vector)
    {
        double t1 = Math.Pow(vector.X, 2) - Math.Pow(vector.Y, 2);
        double t2 = 2 * vector.X * vector.Y;
        double t3 = Math.Pow(t1, 2) - Math.Pow(t2, 2);
        double t4 = 2 * t1 * t2;
        return new Tensor(1, new[] { t3, t4 });
    }

    public void Add(Tensor tensor, bool smooth)
    {
        for (int i = 0; i < _matrix.Length; i++)
        {
            _matrix[i] = _matrix[i] * _r + tensor._matrix[i] * tensor._r;
        }

        if (smooth)
        {
            _r = Math.Sqrt(Math.Pow(_matrix[0], 2) + Math.Pow(_matrix[1], 2));
            _matrix[0] /= _r;
            _matrix[1] /= _r;
        } else
        {
            _r = 2;
        }

        _oldTheta = true;
    }

    public Tensor Scale(double s)
    {
        _r *= s;
        _oldTheta = true;
        return this;
    }

    // Radians
    public void Rotate(double theta)
    {
        if (theta == 0)
        {
            return;
        }

        double newTheta = Theta + theta;
        if (newTheta < Math.PI)
        {
            newTheta += Math.PI;
        }

        if (newTheta >= Math.PI)
        {
            newTheta -= Math.PI;
        }

        _matrix[0] = Math.Cos(2 * newTheta) * _r;
        _matrix[1] = Math.Sin(2 * newTheta) * _r;
        _theta = newTheta;
    }

    public Vector2 GetMajor()
    {
        // Degenerate case
        if (_r == 0)
        {
            return Vector2.Zero;
        }

        return new Vector2((float)Math.Cos(Theta), (float)Math.Sin(Theta));
    }

    public Vector2 GetMinor()
    {
        // Degenerate case
        if (_r == 0)
        {
            return Vector2.Zero;
        }

        double angle = Theta + _angleBetweenRoads;
        return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
    }

    private double CalculateTheta()
    {
        if (_r == 0)
        {
            return 0;
        }

        return Math.Atan2(_matrix[1] / _r, _matrix[0] / _r) / 2;
    }
}