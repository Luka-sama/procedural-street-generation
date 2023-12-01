using Godot;
using System;

public class Tensor
{
    private double _theta;
    private double _r;
    private readonly double[] _matrix;

    public static Tensor Zero => new(0, new double[] { 0, 0 });

    public Tensor(double r, double[] matrix)
    {
        // Represent the matrix as a 2 element list
        // [ 0th element, 1th element
        //   1th element, -0th element ]
        _r = r;
        _matrix = matrix;
        _theta = CalculateTheta();
    }

    public void Add(Tensor tensor, bool smooth)
    {
        for (var i = 0; i < _matrix.Length; i++)
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

        _theta = CalculateTheta();
    }

    public Tensor Scale(double s)
    {
        _r *= s;
        _theta = CalculateTheta();
        return this;
    }

    // Radians
    public void Rotate(double theta)
    {
        if (theta == 0)
        {
            return;
        }

        var newTheta = _theta + theta;
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

        return new Vector2((float)Math.Cos(_theta), (float)Math.Sin(_theta));
    }

    public Vector2 GetMinor()
    {
        // Degenerate case
        if (_r == 0)
        {
            return Vector2.Zero;
        }

        var angle = _theta + Math.PI / 2;
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