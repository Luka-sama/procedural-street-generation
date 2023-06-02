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
    private readonly bool _seedAtEndpoints = false;
    //private readonly int _nearEdge = 3; // Sample near edge

    private GridStorage _majorGrid;
    private GridStorage _minorGrid;
    private StreamlineParams _paramsSq;

    // How many samples to skip when checking streamline collision with itself
    private readonly int _nStreamlineStep;
    // How many samples to ignore backwards when checking streamline collision with itself
    private readonly int _nStreamlineLookBack;
    private readonly double _dcollideselfSq;

    private readonly List<Vector2> _candidateSeedsMajor = new();
    private readonly List<Vector2> _candidateSeedsMinor = new();

    private bool _streamlinesDone = true;
    private bool _lastStreamlineMajor = true;

    private readonly FieldIntegrator _integrator;
    private readonly Vector2 _origin;
    private readonly Vector2 _worldDimensions;
    private readonly StreamlineParams _parameters;

    public List<List<Vector2>> AllStreamlines { get; } = new();
    public List<List<Vector2>> StreamlinesMajor { get; } = new();
    public List<List<Vector2>> StreamlinesMinor { get; } = new();
    public List<List<Vector2>> AllStreamlinesSimple { get; } = new();

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
        
        // Needs to be less than circlejoin
        _dcollideselfSq = Math.Pow(parameters.DCircleJoin / 2, 2);
        _nStreamlineStep = (int)(parameters.DCircleJoin / parameters.Dstep);
        _nStreamlineLookBack = 2 * _nStreamlineStep;

        _majorGrid = new GridStorage(worldDimensions, origin, parameters.Dsep);
        _minorGrid = new GridStorage(worldDimensions, origin, parameters.Dsep);

        SetParamsSq();
    }
    
    public void ClearStreamlines() {
        AllStreamlinesSimple.Clear();
        StreamlinesMajor.Clear();
        StreamlinesMinor.Clear();
        AllStreamlines.Clear();
    }
    
    /**
     * Edits streamlines
     */
    private void JoinDanglingStreamlines()
    {
        // TODO do in update method
        foreach (bool major in new[] { true, false })
        {
            foreach (List<Vector2> streamline in Streamlines(major))
            {
                // Ignore circles
                if (streamline[0].Equals(streamline[^1]))
                {
                    continue;
                }

                Vector2? newStart = GetBestNextPoint(streamline[0], streamline[4]);
                if (newStart != null)
                {
                    foreach (Vector2 p in PointsBetween(streamline[0], newStart.Value, _parameters.Dstep))
                    {
                        streamline.Insert(0, p);
                        Grid(major).AddSample(p);
                    }
                }

                Vector2? newEnd = GetBestNextPoint(streamline[^1], streamline[^4]);
                if (newEnd != null)
                {
                    foreach (Vector2 p in PointsBetween(streamline[^1], newEnd.Value, _parameters.Dstep))
                    {
                        streamline.Add(p);
                        Grid(major).AddSample(p);
                    }
                }
            }
        }

        // Reset simplified streamlines
        AllStreamlinesSimple.Clear();
        foreach (List<Vector2> s in AllStreamlines)
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
        int nPoints = (int)Math.Floor(d / dstep);
        if (nPoints == 0)
            return new List<Vector2>();

        Vector2 stepVector = v2 - v1;

        List<Vector2> outList = new();
        for (int i = 1; i <= nPoints; i++)
        {
            Vector2 next = v1 + stepVector * (i / (float)nPoints);
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
        List<Vector2> nearbyPoints = _majorGrid.GetNearbyPoints(point, _parameters.DLookahead);
        nearbyPoints.AddRange(_minorGrid.GetNearbyPoints(point, _parameters.DLookahead));
        Vector2 direction = point - previousPoint;

        Vector2? closestSample = null;
        double closestDistance = double.PositiveInfinity;

        foreach (Vector2 sample in nearbyPoints)
        {
            if (!sample.Equals(point) && !sample.Equals(previousPoint))// && !streamline.Contains(sample)) {
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

        // TODO is reimplement simplify-js to preserve intersection points
        //  - this is the primary reason polygons aren't found
        // If trying to find intersections in the simplified graph
        // prevent ends getting pulled away from simplified lines
        if (closestSample != null)
        {
            closestSample += 4 * _parameters.SimplifyTolerance * direction.Normalized();
        }

        return closestSample;
    }
    
    /**
     * Assumes s has already generated
     */
    public void AddExistingStreamlines(StreamlineGenerator s)
    {
        _majorGrid.AddAll(s._majorGrid);
        _minorGrid.AddAll(s._minorGrid);
    }

    public void SetGrid(StreamlineGenerator s)
    {
        _majorGrid = s._majorGrid;
        _minorGrid = s._minorGrid;
    }
    
    /**
     * returns true if state updates
     */
    public bool Update()
    {
        if (!_streamlinesDone)
        {
            _lastStreamlineMajor = !_lastStreamlineMajor;
            if (!CreateStreamline(_lastStreamlineMajor))
            {
                _streamlinesDone = true;
                JoinDanglingStreamlines();
            }
            return true;
        }

        return false;
    }

    public void StartCreatingStreamlines()
    {
        _streamlinesDone = false;
    }

    /**
     * All at once - will freeze if dsep small
     */
    public void CreateAllStreamlines()
    {
        StartCreatingStreamlines();
        bool major = true;
        while (CreateStreamline(major))
        {
            major = !major;
        }
        JoinDanglingStreamlines();
    }
    
    // Square distance from a point to a segment
    private float GetSqSegDist(Vector2 p, Vector2 p1, Vector2 p2)
    {
        float x = p1.X;
        float y = p1.Y;
        float dx = p2.X - x;
        float dy = p2.Y - y;

        if (dx != 0 || dy != 0)
        {
            float t = ((p.X - x) * dx + (p.Y - y) * dy) / (dx * dx + dy * dy);

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
        float maxSqDist = 0;
        int index = 0;
        
        for (int i = first + 1; i < last; i++)
        {
            float sqDist = GetSqSegDist(points[i], points[first], points[last]);

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
        int last = points.Count - 1;
        
        List<Vector2> simplified = new List<Vector2> { points[0] };
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
        Vector2? seed = GetSeed(major);
        if (seed == null)
        {
            return false;
        }
        List<Vector2> streamline = IntegrateStreamline(seed.Value, major);
        if (ValidStreamline(streamline))
        {
            Grid(major).AddPolyline(streamline);
            Streamlines(major).Add(streamline);
            AllStreamlines.Add(streamline);

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
        // TODO better seeding scheme
        return new Vector2(
            (float)GD.RandRange(0, _worldDimensions.X - 1),
            (float)GD.RandRange(0, _worldDimensions.Y - 1)
        ) + _origin;
    }
    
    /**
     * Tries candidateSeeds first, then samples using samplePoint
     */
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
        int i = 0;
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
        bool gridValid = Grid(major).IsValidSample(point, dSq);
        if (bothGrids)
        {
            gridValid = gridValid && Grid(!major).IsValidSample(point, dSq);
        }
        return _integrator.OnLand(point) && gridValid;
    }

    private List<Vector2> CandidateSeeds(bool major)
    {
        return major ? _candidateSeedsMajor : _candidateSeedsMinor;
    }

    private List<List<Vector2>> Streamlines(bool major)
    {
        return major ? StreamlinesMajor : StreamlinesMinor;
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
    
    /**
     * Didn't end up using - bit expensive, used streamlineTurned instead
     * Stops spirals from forming
     * uses 0.5 dcirclejoin so that circles are still joined up
     * testSample is candidate to pushed on end of streamlineForwards
     * returns true if streamline collides with itself
     */
    private bool DoesStreamlineCollideSelf(Vector2 testSample, List<Vector2> streamlineForwards, List<Vector2> streamlineBackwards)
    {
        // Streamline long enough
        if (streamlineForwards.Count > _nStreamlineLookBack)
        {
            for (int i = 0; i < streamlineForwards.Count - _nStreamlineLookBack; i += _nStreamlineStep)
            {
                if (testSample.DistanceSquaredTo(streamlineForwards[i]) < _dcollideselfSq)
                {
                    return true;
                }
            }

            // Backwards check
            for (int i = 0; i < streamlineBackwards.Count; i += _nStreamlineStep)
            {
                if (testSample.DistanceSquaredTo(streamlineBackwards[i]) < _dcollideselfSq)
                {
                    return true;
                }
            }
        }

        return false;
    }
    
    /**
     * Tests whether streamline has turned through greater than 180 degrees
     */
    private bool StreamlineTurned(Vector2 seed, Vector2 originalDir, Vector2 point, Vector2 direction)
    {
        if (originalDir.Dot(direction) < 0)
        {
            // TODO optimise
            Vector2 perpendicularVector = new(originalDir.Y, -originalDir.X);
            bool isLeft = (point - seed).Dot(perpendicularVector) < 0;
            bool directionUp = direction.Dot(perpendicularVector) > 0;
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
            Vector2 nextDirection = _integrator.Integrate(currParameters.PreviousPoint, major);

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

            Vector2 nextPoint = currParameters.PreviousPoint + nextDirection;

            // Visualize stopping points
            // if (StreamlineTurned(parameters.Seed, parameters.OriginalDir, nextPoint, nextDirection)) {
            //     parameters.Valid = false;
            //     parameters.Streamline.Add(Vector2.Zero);
            // }

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
        int count = 0;
        bool pointsEscaped = false; // True once two integration fronts have moved dlookahead away

        // Whether or not to test validity using both grid storages
        // (Collide with both major and minor)
        bool collideEarly = GD.Randf() < _parameters.CollideEarly;

        Vector2 d = _integrator.Integrate(seed, major);

        StreamlineIntegration forwardParams = new StreamlineIntegration
        {
            Seed = seed,
            OriginalDir = d,
            Streamline = new List<Vector2> { seed },
            PreviousDirection = d,
            PreviousPoint = seed + d,
            Valid = true
        };

        forwardParams.Valid = PointInBounds(forwardParams.PreviousPoint);

        Vector2 negD = -d;
        StreamlineIntegration backwardParams = new StreamlineIntegration
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
            float sqDistanceBetweenPoints = forwardParams.PreviousPoint.DistanceSquaredTo(backwardParams.PreviousPoint);

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