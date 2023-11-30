using Godot;
using System;
using System.Collections.Generic;

/**
 * Cartesian grid accelerated data structure
 * Grid of cells, each containing a list of vectors
 */
public class GridStorage
{
    private readonly Vector2I _gridDimensions;
    private readonly List<Vector2>[][] _grid;
    private readonly float _dsepSq;

    private readonly Vector2 _worldDimensions;
    private readonly Vector2 _origin;
    private readonly float _dsep;

    /**
     * worldDimensions assumes origin of 0,0
     * @param {number} dsep Separation distance between samples
     */
    public GridStorage(Vector2 worldDimensions, Vector2 origin, float dsep)
    {
        _worldDimensions = worldDimensions;
        _origin = origin;
        _dsep = dsep;

        _dsepSq = dsep * dsep;
        _gridDimensions = new Vector2I((int)Math.Ceiling(worldDimensions.X / dsep), (int)Math.Ceiling(worldDimensions.Y / dsep));
        _grid = new List<Vector2>[_gridDimensions.X][];
        for (int x = 0; x < _gridDimensions.X; x++)
        {
            _grid[x] = new List<Vector2>[_gridDimensions.Y];
            for (int y = 0; y < _gridDimensions.Y; y++)
            {
                _grid[x][y] = new List<Vector2>();
            }
        }
    }

    /**
     * Add all samples from another grid to this one
     */
    public void AddAll(GridStorage gridStorage)
    {
        foreach (var row in gridStorage._grid)
        {
            foreach (var cell in row)
            {
                foreach (var sample in cell)
                {
                    AddSample(sample);
                }
            }
        }
    }

    public void AddPolyline(List<Vector2> line)
    {
        foreach (var v in line)
        {
            AddSample(v);
        }
    }

    /**
     * Does not enforce separation
     * Does not clone
     */
    public void AddSample(Vector2 v, Vector2? givenCoords = null)
    {
        Vector2 coords = givenCoords ?? GetSampleCoords(v);
        _grid[(int)coords.X][(int)coords.Y].Add(v);
    }

    /**
     * Tests whether v is at least d away from samples
     * Performance very important - this is called at every integration step
     * @param dSq=dsepSq squared test distance
     * Could be dtest if we are integrating a streamline
     */
    public bool IsValidSample(Vector2 v, double? givenDSq = null)
    {
        // Code duplication with getNearbyPoints but much slower when calling
        // getNearbyPoints due to array creation in that method

        var dSq = givenDSq ?? _dsepSq;
        var coords = GetSampleCoords(v);

        // Check samples in 9 cells in 3x3 grid
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                var cell = coords + new Vector2(x, y);
                if (!VectorOutOfBounds(cell, _gridDimensions) &&
                    !VectorFarFromVectors(v, _grid[(int)cell.X][(int)cell.Y], dSq))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /**
     * Test whether v is at least d away from vectors
     * Performance very important - this is called at every integration step
     * @param {number}   dSq     squared test distance
     */
    private bool VectorFarFromVectors(Vector2 v, List<Vector2> vectors, double dSq)
    {
        foreach (var sample in vectors)
        {
            if (sample != v)
            {
                double distanceSq = sample.DistanceSquaredTo(v);
                if (distanceSq < dSq)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /**
     * Returns points in cells surrounding v
     * Results include v, if it exists in the grid
     * @param {number} returns samples (kind of) closer than distance - returns all samples in 
     * cells so approximation (square to approximate circle)
     */
    public List<Vector2> GetNearbyPoints(Vector2 v, double distance)
    {
        var radius = (int)Math.Ceiling((distance / _dsep) - 0.5);
        var coords = GetSampleCoords(v);
        var outList = new List<Vector2>();
        for (int x = -1 * radius; x <= 1 * radius; x++)
        {
            for (int y = -1 * radius; y <= 1 * radius; y++)
            {
                Vector2 cell = coords + new Vector2(x, y);
                if (!VectorOutOfBounds(cell, _gridDimensions))
                {
                    foreach (var v2 in _grid[(int)cell.X][(int)cell.Y])
                    {
                        outList.Add(v2);
                    }
                }
            }
        }

        return outList;
    }

    private Vector2 WorldToGrid(Vector2 v)
    {
        return v - _origin;
    }

    private bool VectorOutOfBounds(Vector2 gridV, Vector2 bounds)
    {
        return (gridV.X < 0 || gridV.Y < 0 ||
                gridV.X >= bounds.X || gridV.Y >= bounds.Y);
    }

    private Vector2 GetSampleCoords(Vector2 worldV)
    {
        var v = WorldToGrid(worldV);
        if (VectorOutOfBounds(v, _worldDimensions))
        {
            return Vector2.Zero;
        }

        return new Vector2(
            (int)Math.Floor(v.X / _dsep),
            (int)Math.Floor(v.Y / _dsep)
        );
    }
}