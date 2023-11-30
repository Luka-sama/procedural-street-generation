using Godot;
using System;
using System.Collections.Generic;

public struct StreamlineIntegration
{
    public Vector2 Seed;
    public Vector2 OriginalDir;
    public List<Vector2> Streamline;
    public Vector2 PreviousDirection;
    public Vector2 PreviousPoint;
    public bool Valid;
}

public struct StreamlineParams
{
    public float Dsep; // Streamline seed separating distance
    public float Dtest; // Streamline integration separating distance
    public float Dstep; // Step size
    public float DCircleJoin; // How far to look to join circles - (e.g. 2 x dstep)
    public float DLookahead; // How far to look ahead to join up dangling
    public float JoinAngle; // Angle to join roads in radians
    public int PathIterations; // Path integration iteration limit
    public int SeedTries; // Max failed seeds
    public float SimplifyTolerance;
    public float CollideEarly; // Chance of early collision 0-1
}

public class StreamlineGenerator
{
    public List<List<Vector2>> AllStreamlinesSimple { get; } = new();
    
    private readonly bool _seedAtEndpoints = false;

    private readonly GridStorage _majorGrid;
    private readonly GridStorage _minorGrid;
    private StreamlineParams _paramsSq;

    private readonly List<Vector2> _candidateSeedsMajor = new();
    private readonly List<Vector2> _candidateSeedsMinor = new();

    private readonly FieldIntegrator _integrator;
    private readonly Vector2 _origin;
    private readonly Vector2 _worldDimensions;
    private readonly StreamlineParams _parameters;

    private readonly List<List<Vector2>> _allStreamlines = new();
    private readonly List<List<Vector2>> _streamlinesMajor = new();
    private readonly List<List<Vector2>> _streamlinesMinor = new();

    /**
     * Uses world-space coordinates
     */
    public StreamlineGenerator(FieldIntegrator integrator, Vector2 origin, Vector2 worldDimensions, StreamlineParams parameters) {
        if (parameters.Dstep > parameters.Dsep) {
            GD.PushError("STREAMLINE SAMPLE DISTANCE BIGGER THAN DSEP");
        }
        
        _integrator = integrator;
        _origin = origin;
        _worldDimensions = worldDimensions;
        _parameters = parameters;

        // Enforce test < sep
        parameters.Dtest = Math.Min(parameters.Dtest, parameters.Dsep);

        _majorGrid = new GridStorage(worldDimensions, origin, parameters.Dsep);
        _minorGrid = new GridStorage(worldDimensions, origin, parameters.Dsep);

        SetParamsSq();
    }
    
    private void JoinDanglingStreamlines()
    {
        foreach (var major in new[] { true, false })
        {
            foreach (var streamline in Streamlines(major))
            {
                // Ignore circles
                if (streamline[0].Equals(streamline[^1]))
                {
                    continue;
                }

                var newStart = GetBestNextPoint(streamline[0], streamline[4]);
                if (newStart != null)
                {
                    foreach (var p in PointsBetween(streamline[0], newStart.Value, _parameters.Dstep))
                    {
                        streamline.Insert(0, p);
                        Grid(major).AddSample(p);
                    }
                }

                var newEnd = GetBestNextPoint(streamline[^1], streamline[^4]);
                if (newEnd != null)
                {
                    foreach (var p in PointsBetween(streamline[^1], newEnd.Value, _parameters.Dstep))
                    {
                        streamline.Add(p);
                        Grid(major).AddSample(p);
                    }
                }
            }
        }

        // Reset simplified streamlines
        AllStreamlinesSimple.Clear();
        foreach (var s in _allStreamlines)
        {
            AllStreamlinesSimple.Add(SimplifyStreamline(s));
        }
    }
    
    /**
     * Returns array of points from v1 to v2 such that they are separated by at most dsep
     * not including v1
     */
    private List<Vector2> PointsBetween(Vector2 v1, Vector2 v2, double dstep)
    {
        double d = v1.DistanceTo(v2);
        var nPoints = (int)Math.Floor(d / dstep);
        if (nPoints == 0)
            return new List<Vector2>();

        var stepVector = v2 - v1;

        var outList = new List<Vector2>();
        for (var i = 1; i <= nPoints; i++)
        {
            var next = v1 + stepVector * (i / (float)nPoints);
            if (_integrator.Integrate(next, true).LengthSquared() > 0.001) // Test for degenerate point
            {
                outList.Add(next);
            }
            else
            {
                return outList;
            }
        }
        return outList;
    }

    /**
     * Gets next best point to join streamline
     * returns null if there are no good candidates
     */
    private Vector2? GetBestNextPoint(Vector2 point, Vector2 previousPoint)
    {
        var nearbyPoints = _majorGrid.GetNearbyPoints(point, _parameters.DLookahead);
        nearbyPoints.AddRange(_minorGrid.GetNearbyPoints(point, _parameters.DLookahead));
        var direction = point - previousPoint;

        Vector2? closestSample = null;
        var closestDistance = double.PositiveInfinity;

        foreach (Vector2 sample in nearbyPoints)
        {
            if (!sample.Equals(point) && !sample.Equals(previousPoint))
            {
                Vector2 differenceVector = sample - point;
                if (differenceVector.Dot(direction) < 0)
                {
                    // Backwards
                    continue;
                }

                // Acute angle between vectors (agnostic of CW, ACW)
                double distanceToSample = point.DistanceSquaredTo(sample);
                if (distanceToSample < 2 * _paramsSq.Dstep)
                {
                    closestSample = sample;
                    break;
                }

                double angleBetween = Math.Abs(direction.AngleTo(differenceVector));

                // Filter by angle
                if (angleBetween < _parameters.JoinAngle && distanceToSample < closestDistance)
                {
                    closestDistance = distanceToSample;
                    closestSample = sample;
                }
            }
        }

        if (closestSample != null)
        {
            closestSample += 4 * _parameters.SimplifyTolerance * direction.Normalized();
        }

        return closestSample;
    }

    /** All at once - will freeze if dsep small */
    public void CreateAllStreamlines()
    {
        var major = true;
        while (CreateStreamline(major))
        {
            major = !major;
        }
        JoinDanglingStreamlines();
    }
    
    /** Square distance from a point to a segment */
    private float GetSqSegDist(Vector2 p, Vector2 p1, Vector2 p2)
    {
        var x = p1.X;
        var y = p1.Y;
        var dx = p2.X - x;
        var dy = p2.Y - y;

        if (dx != 0 || dy != 0)
        {
            var t = ((p.X - x) * dx + (p.Y - y) * dy) / (dx * dx + dy * dy);

            if (t > 1)
            {
                x = p2.X;
                y = p2.Y;
            }
            else if (t > 0)
            {
                x += dx * t;
                y += dy * t;
            }
        }

        dx = p.X - x;
        dy = p.Y - y;

        return dx * dx + dy * dy;
    }

    private void SimplifyDpStep(List<Vector2> points, int first, int last, float sqTolerance, List<Vector2> simplified)
    {
        var maxSqDist = 0f;
        var index = 0;
        
        for (var i = first + 1; i < last; i++)
        {
            var sqDist = GetSqSegDist(points[i], points[first], points[last]);

            if (sqDist > maxSqDist)
            {
                index = i;
                maxSqDist = sqDist;
            }
        }

        if (maxSqDist > sqTolerance)
        {
            if (index - first > 1)
            {
                SimplifyDpStep(points, first, index, sqTolerance, simplified);
            }
            simplified.Add(points[index]);
            if (last - index > 1)
            {
                SimplifyDpStep(points, index, last, sqTolerance, simplified);
            }
        }
    }
    
    // simplification using Ramer-Douglas-Peucker algorithm
    private List<Vector2> SimplifyDouglasPeucker(List<Vector2> points, float sqTolerance)
    {
        var last = points.Count - 1;
        
        var simplified = new List<Vector2> { points[0] };
        SimplifyDpStep(points, 0, last, sqTolerance, simplified);
        simplified.Add(points[last]);

        return simplified;
    }

    private List<Vector2> Simplify(List<Vector2> points, float tolerance)
    {
        if (points.Count <= 2) return points;
        
        var sqTolerance = tolerance * tolerance;
        return SimplifyDouglasPeucker(points, sqTolerance);
    }

    private List<Vector2> SimplifyStreamline(List<Vector2> streamline)
    {
        return Simplify(streamline, _parameters.SimplifyTolerance);
    }

    /**
     * Finds seed and creates a streamline from that point
     * Pushes new candidate seeds to queue
     * @return {Vector[]} returns false if seed isn't found within params.seedTries
     */
    private bool CreateStreamline(bool major)
    {
        var seed = GetSeed(major);
        if (seed == null)
        {
            return false;
        }
        var streamline = IntegrateStreamline(seed.Value, major);
        if (ValidStreamline(streamline))
        {
            Grid(major).AddPolyline(streamline);
            Streamlines(major).Add(streamline);
            _allStreamlines.Add(streamline);

            AllStreamlinesSimple.Add(SimplifyStreamline(streamline));

            // Add candidate seeds
            if (!streamline[0].Equals(streamline[^1]))
            {
                CandidateSeeds(!major).Add(streamline[0]);
                CandidateSeeds(!major).Add(streamline[^1]);
            }
        }
        
        return true;
    }

    private bool ValidStreamline(List<Vector2> s)
    {
        return s.Count > 5;
    }

    private void SetParamsSq()
    {
        _paramsSq = new StreamlineParams
        {
            Dsep = (float)Math.Pow(_parameters.Dsep, 2),
            Dtest = (float)Math.Pow(_parameters.Dtest, 2),
            Dstep = (float)Math.Pow(_parameters.Dstep, 2),
            DCircleJoin = (float)Math.Pow(_parameters.DCircleJoin, 2),
            DLookahead = (float)Math.Pow(_parameters.DLookahead, 2),
            JoinAngle = (float)Math.Pow(_parameters.JoinAngle, 2),
            PathIterations = (int)Math.Pow(_parameters.PathIterations, 2),
            SeedTries = (int)Math.Pow(_parameters.SeedTries, 2),
            SimplifyTolerance = (float)Math.Pow(_parameters.SimplifyTolerance, 2),
            CollideEarly = (float)Math.Pow(_parameters.CollideEarly, 2)
        };
    }

    private Vector2 SamplePoint()
    {
        return new Vector2(
            (float)GD.RandRange(0, _worldDimensions.X - 1),
            (float)GD.RandRange(0, _worldDimensions.Y - 1)
        ) + _origin;
    }
    
    /** Tries candidateSeeds first, then samples using samplePoint */
    private Vector2? GetSeed(bool major)
    {
        Vector2 seed;
        
        // Candidate seeds first
        if (_seedAtEndpoints && CandidateSeeds(major).Count > 0)
        {
            while (CandidateSeeds(major).Count > 0)
            {
                var candidateSeeds = CandidateSeeds(major);
                seed = candidateSeeds[^1];
                candidateSeeds.RemoveAt(candidateSeeds.Count - 1);
                if (IsValidSample(major, seed, _paramsSq.Dsep))
                {
                    return seed;
                }
            }
        }

        seed = SamplePoint();
        var i = 0;
        while (!IsValidSample(major, seed, _paramsSq.Dsep))
        {
            if (i >= _parameters.SeedTries)
            {
                return null;
            }
            seed = SamplePoint();
            i++;
        }
        
        return seed;
    }

    private bool IsValidSample(bool major, Vector2 point, double dSq, bool bothGrids = false)
    {
        // dSq *= point.DistanceSquaredTo(Vector2.Zero);
        var gridValid = Grid(major).IsValidSample(point, dSq);
        if (bothGrids)
        {
            gridValid = gridValid && Grid(!major).IsValidSample(point, dSq);
        }
        return gridValid;
    }

    private List<Vector2> CandidateSeeds(bool major)
    {
        return major ? _candidateSeedsMajor : _candidateSeedsMinor;
    }

    private List<List<Vector2>> Streamlines(bool major)
    {
        return major ? _streamlinesMajor : _streamlinesMinor;
    }

    private GridStorage Grid(bool major)
    {
        return major ? _majorGrid : _minorGrid;
    }

    private bool PointInBounds(Vector2 v)
    {
        return (v.X >= _origin.X
                && v.Y >= _origin.Y
                && v.X < _worldDimensions.X + _origin.X
                && v.Y < _worldDimensions.Y + _origin.Y
            );
    }

    /** Tests whether streamline has turned through greater than 180 degrees */
    private bool StreamlineTurned(Vector2 seed, Vector2 originalDir, Vector2 point, Vector2 direction)
    {
        if (originalDir.Dot(direction) < 0)
        {
            var perpendicularVector = new Vector2(originalDir.Y, -originalDir.X);
            var isLeft = (point - seed).Dot(perpendicularVector) < 0;
            var directionUp = direction.Dot(perpendicularVector) > 0;
            return isLeft == directionUp;
        }

        return false;
    }
    
    /**
     * // TODO this doesn't work well - consider something disallowing one direction (F/B) to turn more than 180 deg
     * One step of the streamline integration process
     */
    private void StreamlineIntegrationStep(ref StreamlineIntegration currParameters, bool major, bool collideBoth)
    {
        if (currParameters.Valid)
        {
            currParameters.Streamline.Add(currParameters.PreviousPoint);
            var nextDirection = _integrator.Integrate(currParameters.PreviousPoint, major);

            // Stop at degenerate point
            if (nextDirection.LengthSquared() < 0.01)
            {
                currParameters.Valid = false;
                return;
            }

            // Make sure we travel in the same direction
            if (nextDirection.Dot(currParameters.PreviousDirection) < 0)
            {
                nextDirection = -nextDirection;
            }

            var nextPoint = currParameters.PreviousPoint + nextDirection;

            if (PointInBounds(nextPoint)
                && IsValidSample(major, nextPoint, _paramsSq.Dtest, collideBoth)
                && !StreamlineTurned(currParameters.Seed, currParameters.OriginalDir, nextPoint, nextDirection))
            {
                currParameters.PreviousPoint = nextPoint;
                currParameters.PreviousDirection = nextDirection;
            }
            else
            {
                // One more step
                currParameters.Streamline.Add(nextPoint);
                currParameters.Valid = false;
            }
        }
    }
    
    /**
     * By simultaneously integrating in both directions we reduce the impact of circles not joining
     * up as the error matches at the join
     */
    private List<Vector2> IntegrateStreamline(Vector2 seed, bool major)
    {
        var count = 0;
        var pointsEscaped = false; // True once two integration fronts have moved dlookahead away

        // Whether or not to test validity using both grid storages
        // (Collide with both major and minor)
        var collideEarly = GD.Randf() < _parameters.CollideEarly;

        var d = _integrator.Integrate(seed, major);

        var forwardParams = new StreamlineIntegration
        {
            Seed = seed,
            OriginalDir = d,
            Streamline = new List<Vector2> { seed },
            PreviousDirection = d,
            PreviousPoint = seed + d,
            Valid = true
        };

        forwardParams.Valid = PointInBounds(forwardParams.PreviousPoint);

        var negD = -d;
        var backwardParams = new StreamlineIntegration
        {
            Seed = seed,
            OriginalDir = negD,
            Streamline = new List<Vector2>(),
            PreviousDirection = negD,
            PreviousPoint = seed + negD,
            Valid = true
        };

        backwardParams.Valid = PointInBounds(backwardParams.PreviousPoint);

        while (count < _parameters.PathIterations && (forwardParams.Valid || backwardParams.Valid))
        {
            StreamlineIntegrationStep(ref forwardParams, major, collideEarly);
            StreamlineIntegrationStep(ref backwardParams, major, collideEarly);

            // Join up circles
            var sqDistanceBetweenPoints = forwardParams.PreviousPoint.DistanceSquaredTo(backwardParams.PreviousPoint);

            if (!pointsEscaped && sqDistanceBetweenPoints > _paramsSq.DCircleJoin)
            {
                pointsEscaped = true;
            }

            if (pointsEscaped && sqDistanceBetweenPoints <= _paramsSq.DCircleJoin)
            {
                forwardParams.Streamline.Add(forwardParams.PreviousPoint);
                forwardParams.Streamline.Add(backwardParams.PreviousPoint);
                backwardParams.Streamline.Add(backwardParams.PreviousPoint);
                break;
            }

            count++;
        }

        backwardParams.Streamline.Reverse();
        backwardParams.Streamline.AddRange(forwardParams.Streamline);
        return backwardParams.Streamline;
    }
}