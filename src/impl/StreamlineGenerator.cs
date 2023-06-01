using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
    protected readonly bool SEED_AT_ENDPOINTS = false;
    protected readonly int NEAR_EDGE = 3; // Sample near edge

    protected GridStorage majorGrid;
    protected GridStorage minorGrid;
    protected StreamlineParams paramsSq;

    // How many samples to skip when checking streamline collision with itself
    protected int nStreamlineStep;
    // How many samples to ignore backwards when checking streamline collision with itself
    protected int nStreamlineLookBack;
    protected double dcollideselfSq;

    protected List<Vector2> candidateSeedsMajor = new();
    protected List<Vector2> candidateSeedsMinor = new();

    protected bool streamlinesDone = true;
    protected bool lastStreamlineMajor = true;
    
    protected FieldIntegrator integrator;
    protected Vector2 origin;
    protected Vector2 worldDimensions;
    protected StreamlineParams parameters;

    public List<List<Vector2>> allStreamlines = new();
    public List<List<Vector2>> streamlinesMajor = new();
    public List<List<Vector2>> streamlinesMinor = new();
    public List<List<Vector2>> allStreamlinesSimple = new(); // Reduced vertex count

    /**
     * Uses world-space coordinates
     */
    public StreamlineGenerator(FieldIntegrator integrator, Vector2 origin, Vector2 worldDimensions, StreamlineParams parameters) {
        if (parameters.Dstep > parameters.Dsep) {
            GD.PushError("STREAMLINE SAMPLE DISTANCE BIGGER THAN DSEP");
        }
        
        this.integrator = integrator;
        this.origin = origin;
        this.worldDimensions = worldDimensions;
        this.parameters = parameters;

        // Enforce test < sep
        parameters.Dtest = Math.Min(parameters.Dtest, parameters.Dsep);
        
        // Needs to be less than circlejoin
        this.dcollideselfSq = Math.Pow(parameters.DCircleJoin / 2, 2);
        this.nStreamlineStep = (int)(parameters.DCircleJoin / parameters.Dstep);
        this.nStreamlineLookBack = 2 * this.nStreamlineStep;

        this.majorGrid = new GridStorage(worldDimensions, origin, parameters.Dsep);
        this.minorGrid = new GridStorage(worldDimensions, origin, parameters.Dsep);

        this.SetParamsSq(this.parameters);
    }
    
    void clearStreamlines() {
        this.allStreamlinesSimple.Clear();
        this.streamlinesMajor.Clear();
        this.streamlinesMinor.Clear();
        this.allStreamlines.Clear();
    }
    
    /**
     * Edits streamlines
     */
    public void JoinDanglingStreamlines()
    {
        // TODO do in update method
        foreach (bool major in new bool[] { true, false })
        {
            foreach (List<Vector2> streamline in this.Streamlines(major))
            {
                // Ignore circles
                if (streamline[0].Equals(streamline[streamline.Count - 1]))
                {
                    continue;
                }

                Vector2? newStart = this.GetBestNextPoint(streamline[0], streamline[4], streamline);
                if (newStart != null)
                {
                    foreach (Vector2 p in this.PointsBetween(streamline[0], newStart.Value, this.parameters.Dstep))
                    {
                        streamline.Prepend(p);
                        this.Grid(major).AddSample(p);
                    }
                }

                Vector2? newEnd = this.GetBestNextPoint(streamline[streamline.Count - 1], streamline[streamline.Count - 4], streamline);
                if (newEnd != null)
                {
                    foreach (Vector2 p in this.PointsBetween(streamline[streamline.Count - 1], newEnd.Value, parameters.Dstep))
                    {
                        streamline.Add(p);
                        this.Grid(major).AddSample(p);
                    }
                }
            }
        }

        // Reset simplified streamlines
        this.allStreamlinesSimple.Clear();
        foreach (List<Vector2> s in this.allStreamlines)
        {
            this.allStreamlinesSimple.Add(this.SimplifyStreamline(s));
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
            return new();

        Vector2 stepVector = v2 - v1;

        List<Vector2> outList = new();
        for (int i = 1; i <= nPoints; i++)
        {
            Vector2 next = v1 + stepVector * (i / (float)nPoints);
            if (this.integrator.Integrate(next, true).LengthSquared() > 0.001) // Test for degenerate point
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
    private Vector2? GetBestNextPoint(Vector2 point, Vector2 previousPoint, List<Vector2> streamline)
    {
        List<Vector2> nearbyPoints = this.majorGrid.GetNearbyPoints(point, this.parameters.DLookahead);
        nearbyPoints.AddRange(this.minorGrid.GetNearbyPoints(point, this.parameters.DLookahead));
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
                if (distanceToSample < 2 * this.paramsSq.Dstep)
                {
                    closestSample = sample;
                    break;
                }

                double angleBetween = Math.Abs(direction.AngleTo(differenceVector));

                // Filter by angle
                if (angleBetween < this.parameters.JoinAngle && distanceToSample < closestDistance)
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
            closestSample += 4 * this.parameters.SimplifyTolerance * direction.Normalized();
        }

        return closestSample;
    }
    
    /**
     * Assumes s has already generated
     */
    public void AddExistingStreamlines(StreamlineGenerator s)
    {
        this.majorGrid.AddAll(s.majorGrid);
        this.minorGrid.AddAll(s.minorGrid);
    }

    public void SetGrid(StreamlineGenerator s)
    {
        this.majorGrid = s.majorGrid;
        this.minorGrid = s.minorGrid;
    }
    
    /**
     * returns true if state updates
     */
    public bool Update()
    {
        if (!this.streamlinesDone)
        {
            this.lastStreamlineMajor = !this.lastStreamlineMajor;
            if (!this.CreateStreamline(this.lastStreamlineMajor))
            {
                this.streamlinesDone = true;
                this.JoinDanglingStreamlines();
            }
            return true;
        }

        return false;
    }

    public void StartCreatingStreamlines()
    {
        this.streamlinesDone = false;
    }

    /**
     * All at once - will freeze if dsep small
     */
    public void CreateAllStreamlines()
    {
        this.StartCreatingStreamlines();
        bool major = true;
        while (this.CreateStreamline(major))
        {
            major = !major;
        }
        this.JoinDanglingStreamlines();
    }
    
    // Square distance from a point to a segment
    float GetSqSegDist(Vector2 p, Vector2 p1, Vector2 p2)
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
    
    void SimplifyDPStep(List<Vector2> points, int first, int last, float sqTolerance, List<Vector2> simplified)
    {
        float maxSqDist = 0;
        int index = 0;
        
        for (int i = first + 1; i < last; i++)
        {
            float sqDist = this.GetSqSegDist(points[i], points[first], points[last]);

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
                SimplifyDPStep(points, first, index, sqTolerance, simplified);
            }
            simplified.Add(points[index]);
            if (last - index > 1)
            {
                SimplifyDPStep(points, index, last, sqTolerance, simplified);
            }
        }
    }
    
    // simplification using Ramer-Douglas-Peucker algorithm
    List<Vector2> SimplifyDouglasPeucker(List<Vector2> points, float sqTolerance)
    {
        int last = points.Count - 1;
        
        List<Vector2> simplified = new List<Vector2> { points[0] };
        this.SimplifyDPStep(points, 0, last, sqTolerance, simplified);
        simplified.Add(points[last]);

        return simplified;
    }
    
    List<Vector2> Simplify(List<Vector2> points, float tolerance)
    {
        if (points.Count <= 2) return points;
        
        var sqTolerance = tolerance * tolerance;
        return this.SimplifyDouglasPeucker(points, sqTolerance);
    }
    
    protected List<Vector2> SimplifyStreamline(List<Vector2> streamline)
    {
        List<Vector2> simplified = new();
        foreach (Vector2 point in this.Simplify(streamline, this.parameters.SimplifyTolerance))
        {
            simplified.Add(point);
        }
        return simplified;
    }

    /**
     * Finds seed and creates a streamline from that point
     * Pushes new candidate seeds to queue
     * @return {Vector[]} returns false if seed isn't found within params.seedTries
     */
    protected bool CreateStreamline(bool major)
    {
        Vector2? seed = this.GetSeed(major);
        if (seed == null)
        {
            return false;
        }
        List<Vector2> streamline = this.IntegrateStreamline(seed.Value, major);
        if (this.ValidStreamline(streamline))
        {
            this.Grid(major).AddPolyline(streamline);
            this.Streamlines(major).Add(streamline);
            allStreamlines.Add(streamline);

            allStreamlinesSimple.Add(SimplifyStreamline(streamline));

            // Add candidate seeds
            if (!streamline[0].Equals(streamline[streamline.Count - 1]))
            {
                this.CandidateSeeds(!major).Add(streamline[0]);
                this.CandidateSeeds(!major).Add(streamline[streamline.Count - 1]);
            }
        }
        
        return true;
    }
    
    protected bool ValidStreamline(List<Vector2> s)
    {
        return s.Count > 5;
    }

    protected void SetParamsSq(StreamlineParams thisParameters) // hack to copy the value
    {
        this.paramsSq = thisParameters;
        foreach (var property in thisParameters.GetType().GetProperties())
        {
            if (property.PropertyType == typeof(float))
            {
                float value = (float)property.GetValue(thisParameters);
                property.SetValue(paramsSq, value * value);
            }
        }
    }

    protected Vector2 SamplePoint()
    {
        // TODO better seeding scheme
        return new Vector2(
            (float)GD.RandRange(0, this.worldDimensions.X - 1),
            (float)GD.RandRange(0, this.worldDimensions.Y - 1)
        ) + this.origin;
    }
    
    /**
     * Tries this.candidateSeeds first, then samples using this.samplePoint
     */
    protected Vector2? GetSeed(bool major)
    {
        Vector2 seed;
        
        // Candidate seeds first
        if (this.SEED_AT_ENDPOINTS && this.CandidateSeeds(major).Count > 0)
        {
            while (this.CandidateSeeds(major).Count > 0)
            {
                var candidateSeeds = this.CandidateSeeds(major);
                seed = candidateSeeds[candidateSeeds.Count - 1];
                candidateSeeds.RemoveAt(candidateSeeds.Count - 1);
                if (this.IsValidSample(major, seed, this.paramsSq.Dsep))
                {
                    return seed;
                }
            }
        }

        seed = this.SamplePoint();
        int i = 0;
        while (!this.IsValidSample(major, seed, this.paramsSq.Dsep))
        {
            if (i >= this.parameters.SeedTries)
            {
                return null;
            }
            seed = SamplePoint();
            i++;
        }
        
        return seed;
    }
    
    protected bool IsValidSample(bool major, Vector2 point, double dSq, bool bothGrids = false)
    {
        // dSq *= point.DistanceSquaredTo(Vector2.Zero);
        bool gridValid = this.Grid(major).IsValidSample(point, dSq);
        if (bothGrids)
        {
            gridValid = gridValid && this.Grid(!major).IsValidSample(point, dSq);
        }
        return this.integrator.OnLand(point) && gridValid;
    }
    
    protected List<Vector2> CandidateSeeds(bool major)
    {
        return major ? this.candidateSeedsMajor : this.candidateSeedsMinor;
    }
    
    protected List<List<Vector2>> Streamlines(bool major)
    {
        return major ? this.streamlinesMajor : this.streamlinesMinor;
    }

    protected GridStorage Grid(bool major)
    {
        return major ? this.majorGrid : this.minorGrid;
    }
    
    protected bool PointInBounds(Vector2 v)
    {
        return (v.X >= this.origin.X
                && v.Y >= this.origin.Y
                && v.X < this.worldDimensions.X + this.origin.X
                && v.Y < this.worldDimensions.Y + this.origin.Y
            );
    }
    
    /**
     * Didn't end up using - bit expensive, used streamlineTurned instead
     * Stops spirals from forming
     * uses 0.5 dcirclejoin so that circles are still joined up
     * testSample is candidate to pushed on end of streamlineForwards
     * returns true if streamline collides with itself
     */
    protected bool DoesStreamlineCollideSelf(Vector2 testSample, List<Vector2> streamlineForwards, List<Vector2> streamlineBackwards)
    {
        // Streamline long enough
        if (streamlineForwards.Count > this.nStreamlineLookBack)
        {
            for (int i = 0; i < streamlineForwards.Count - this.nStreamlineLookBack; i += this.nStreamlineStep)
            {
                if (testSample.DistanceSquaredTo(streamlineForwards[i]) < this.dcollideselfSq)
                {
                    return true;
                }
            }

            // Backwards check
            for (int i = 0; i < streamlineBackwards.Count; i += this.nStreamlineStep)
            {
                if (testSample.DistanceSquaredTo(streamlineBackwards[i]) < this.dcollideselfSq)
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
    protected bool StreamlineTurned(Vector2 seed, Vector2 originalDir, Vector2 point, Vector2 direction)
    {
        if (originalDir.Dot(direction) < 0)
        {
            // TODO optimise
            Vector2 perpendicularVector = new Vector2(originalDir.Y, -originalDir.X);
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
    protected void StreamlineIntegrationStep(StreamlineIntegration parameters, bool major, bool collideBoth)
    {
        if (parameters.Valid)
        {
            parameters.Streamline.Add(parameters.PreviousPoint);
            Vector2 nextDirection = this.integrator.Integrate(parameters.PreviousPoint, major);

            // Stop at degenerate point
            if (nextDirection.LengthSquared() < 0.01)
            {
                parameters.Valid = false;
                return;
            }

            // Make sure we travel in the same direction
            if (nextDirection.Dot(parameters.PreviousDirection) < 0)
            {
                nextDirection = -nextDirection;
            }

            Vector2 nextPoint = parameters.PreviousPoint + nextDirection;

            // Visualize stopping points
            // if (this.StreamlineTurned(parameters.Seed, parameters.OriginalDir, nextPoint, nextDirection)) {
            //     parameters.Valid = false;
            //     parameters.Streamline.Add(Vector2.Zero);
            // }

            if (this.PointInBounds(nextPoint)
                && this.IsValidSample(major, nextPoint, this.paramsSq.Dtest, collideBoth)
                && !this.StreamlineTurned(parameters.Seed, parameters.OriginalDir, nextPoint, nextDirection))
            {
                parameters.PreviousPoint = nextPoint;
                parameters.PreviousDirection = nextDirection;
            }
            else
            {
                // One more step
                parameters.Streamline.Add(nextPoint);
                parameters.Valid = false;
            }
        }
    }
    
    /**
     * By simultaneously integrating in both directions we reduce the impact of circles not joining
     * up as the error matches at the join
     */
    protected List<Vector2> IntegrateStreamline(Vector2 seed, bool major)
    {
        int count = 0;
        bool pointsEscaped = false; // True once two integration fronts have moved dlookahead away

        // Whether or not to test validity using both grid storages
        // (Collide with both major and minor)
        bool collideEarly = GD.Randf() < this.parameters.CollideEarly;

        Vector2 d = this.integrator.Integrate(seed, major);

        StreamlineIntegration forwardParams = new StreamlineIntegration
        {
            Seed = seed,
            OriginalDir = d,
            Streamline = new List<Vector2> { seed },
            PreviousDirection = d,
            PreviousPoint = seed + d,
            Valid = true
        };

        forwardParams.Valid = this.PointInBounds(forwardParams.PreviousPoint);

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

        backwardParams.Valid = this.PointInBounds(backwardParams.PreviousPoint);

        while (count < this.parameters.PathIterations && (forwardParams.Valid || backwardParams.Valid))
        {
            this.StreamlineIntegrationStep(forwardParams, major, collideEarly);
            this.StreamlineIntegrationStep(backwardParams, major, collideEarly);

            // Join up circles
            float sqDistanceBetweenPoints = forwardParams.PreviousPoint.DistanceSquaredTo(backwardParams.PreviousPoint);

            if (!pointsEscaped && sqDistanceBetweenPoints > this.paramsSq.DCircleJoin)
            {
                pointsEscaped = true;
            }

            if (pointsEscaped && sqDistanceBetweenPoints <= this.paramsSq.DCircleJoin)
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