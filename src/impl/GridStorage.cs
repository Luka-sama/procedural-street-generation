using Godot;
using System;
using System.Collections.Generic;

/**
 * Cartesian grid accelerated data structure
 * Grid of cells, each containing a list of vectors
 */
public class GridStorage
{
    private Vector2I gridDimensions;
    private List<Vector2>[][] grid;
    private float dsepSq;

    private Vector2 worldDimensions;
    private Vector2 origin;
    private float dsep;

    /**
     * worldDimensions assumes origin of 0,0
     * @param {number} dsep Separation distance between samples
     */
    public GridStorage(Vector2 worldDimensions, Vector2 origin, float dsep)
    {
        this.worldDimensions = worldDimensions;
        this.origin = origin;
        this.dsep = dsep;

        this.dsepSq = dsep * dsep;
        this.gridDimensions = new Vector2I((int)Math.Ceiling(worldDimensions.X / dsep), (int)Math.Ceiling(worldDimensions.Y / dsep));
        this.grid = new List<Vector2>[this.gridDimensions.X][];
        for (int x = 0; x < this.gridDimensions.X; x++)
        {
            this.grid[x] = new List<Vector2>[this.gridDimensions.Y];
            for (int y = 0; y < this.gridDimensions.Y; y++)
            {
                this.grid[x][y] = new List<Vector2>();
            }
        }
    }

    /**
     * Add all samples from another grid to this one
     */
    public void AddAll(GridStorage gridStorage)
    {
        foreach (var row in gridStorage.grid)
        {
            foreach (var cell in row)
            {
                foreach (var sample in cell)
                {
                    this.AddSample(sample);
                }
            }
        }
    }

    public void AddPolyline(List<Vector2> line)
    {
        foreach (var v in line)
        {
            this.AddSample(v);
        }
    }

    /**
     * Does not enforce separation
     * Does not clone
     */
    public void AddSample(Vector2 v, Vector2? givenCoords = null)
    {
        Vector2 coords = givenCoords ?? this.GetSampleCoords(v);
        this.grid[(int)coords.X][(int)coords.Y].Add(v);
    }

    /**
     * Tests whether v is at least d away from samples
     * Performance very important - this is called at every integration step
     * @param dSq=this.dsepSq squared test distance
     * Could be dtest if we are integrating a streamline
     */
    public bool IsValidSample(Vector2 v, double? givenDSq = null)
    {
        // Code duplication with this.getNearbyPoints but much slower when calling
        // this.getNearbyPoints due to array creation in that method

        double dSq = givenDSq ?? this.dsepSq;
        Vector2 coords = this.GetSampleCoords(v);

        // Check samples in 9 cells in 3x3 grid
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2 cell = coords + new Vector2(x, y);
                if (!this.VectorOutOfBounds(cell, this.gridDimensions) &&
                    !this.VectorFarFromVectors(v, this.grid[(int)cell.X][(int)cell.Y], dSq))
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
    public bool VectorFarFromVectors(Vector2 v, List<Vector2> vectors, double dSq)
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
        int radius = (int)Math.Ceiling((distance / this.dsep) - 0.5);
        Vector2 coords = this.GetSampleCoords(v);
        List<Vector2> outList = new();
        for (int x = -1 * radius; x <= 1 * radius; x++)
        {
            for (int y = -1 * radius; y <= 1 * radius; y++)
            {
                Vector2 cell = coords + new Vector2(x, y);
                if (!this.VectorOutOfBounds(cell, this.gridDimensions))
                {
                    foreach (var v2 in this.grid[(int)cell.X][(int)cell.Y])
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
        return v - this.origin;
    }

    private Vector2 GridToWorld(Vector2 v)
    {
        return v + this.origin;
    }

    private bool VectorOutOfBounds(Vector2 gridV, Vector2 bounds)
    {
        return (gridV.X < 0 || gridV.Y < 0 ||
                gridV.X >= bounds.X || gridV.Y >= bounds.Y);
    }

    private Vector2 GetSampleCoords(Vector2 worldV)
    {
        Vector2 v = this.WorldToGrid(worldV);
        if (this.VectorOutOfBounds(v, this.worldDimensions))
        {
            return Vector2.Zero;
        }

        return new Vector2(
            (int)Math.Floor(v.X / this.dsep),
            (int)Math.Floor(v.Y / this.dsep)
        );
    }
}