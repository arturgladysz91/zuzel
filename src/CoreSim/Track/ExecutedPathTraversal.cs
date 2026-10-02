namespace CoreSim;

/// <summary>
/// Distance-controlled coupled integration. A scalar time solve jointly determines lateral
/// endpoint, polar/cartesian path length and the existing longitudinal endpoint/time.
/// Fixed lines continue to use their exact existing production path.
/// </summary>
internal static class ExecutedPathTraversal
{
    internal const int PredictorCorrectorIterations = 16;
    internal const int BisectionIterations = 64;
    internal const double TimeToleranceSeconds = 1e-7;
    internal const int TimeSolveSubdivisionLimit = 16;

    private sealed class TimeSolveDiscontinuityException : InvalidOperationException
    {
        public TimeSolveDiscontinuityException() : base("Coupled time solve did not converge within its bound.") { }
    }

    internal static bool IsFixedLine(float lateral, int target)
        // Preserve existing offset round-trip noise (not the 0.05 m arrival tolerance).
        => lateral >= MathF.BitDecrement(MathF.BitDecrement((float)target))
        && lateral <= MathF.BitIncrement(MathF.BitIncrement((float)target));

    internal static float GeometricDistance(TrackSegment segment, TrackGeometry geometry,
        float startLateral, float endLateral, double canonicalAdvance)
    {
        var startOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(startLateral, segment.Type, geometry);
        var endOffset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(endLateral, segment.Type, geometry);
        var dy = (double)endOffset - startOffset;
        var tangent = segment.Type == SegmentType.Straight
            ? (segment.StraightLengthMetersOverride ?? geometry.StraightLengthMeters) * canonicalAdvance
            : (geometry.InnerRadiusMeters + ((double)startOffset + endOffset) * .5d)
                * geometry.TurnSegmentAngleRadians * canonicalAdvance;
        if (dy == 0d)
            return LaneModel.SegmentLengthMeters(segment, startLateral, geometry) * (float)canonicalAdvance;
        return (float)Math.Sqrt(tangent * tangent + dy * dy);
    }

    internal static ExecutedSegmentPath Traverse(SimulationSnapshot snapshot, RiderSnapshot rider,
        SegmentResolution resolution, float entrySpeed, float canonicalAdvance, bool launch,
        Func<float, float?> nextCornerTarget)
    {
        var segment = snapshot.Segment; var geometry = snapshot.Track.Geometry;
        var corner = snapshot.Track.CornerTopology.CornerForSegment(snapshot.Step.SegmentIndex);
        var skills = rider.Profile.Skills; var setup = rider.ActiveSetup;
        var start = (double)rider.SegmentProgress;
        var end = start + canonicalAdvance;
        var lateral = rider.LateralPosition;
        var reaction = launch ? LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(skills) : 0f;
        double time = reaction, distance = 0d;
        var crash = resolution.Outcome == SegmentOutcome.Crash;
        var speed = crash ? MathF.Max(1f, entrySpeed * .5f) : resolution.Speed;
        var nodes = new List<ExecutedPathNode> { Node(start, lateral, speed) };
        var steps = new List<ExecutedPathStep>();
        var maxNodes = checked((int)Math.Ceiling(LaneModel.SegmentLengthMeters(segment, 4f, geometry)
            + LaneModel.UsableRacingWidthMeters(segment.Type, geometry)) * 4 + 16);
        while (start < end)
        {
            if (steps.Count >= maxNodes) throw new InvalidOperationException("Coupled traversal exceeded its geometric node bound.");
            var length = LaneModel.SegmentLengthMeters(segment, lateral, geometry);
            var finish = Math.Min(end, start + LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters / length);
            if (corner is not null)
            {
                foreach (var knot in new[] { (double)ContinuousCornerEnvelope.ApexProgress, ContinuousCornerEnvelope.FullDriveProgress })
                {
                    var localKnot = knot * corner.SegmentCount - (snapshot.Step.SegmentIndex - corner.StartSegmentIndex);
                    if (start < localKnot && finish > localKnot) finish = localKnot;
                }
            }
            CoupledStep result;
            var subdivisions = 0;
            while (true)
            {
                try
                {
                    result = Solve(finish);
                    // Reuse the physical metre resolution, rather than introduce a second grid.
                    for (var split = 0; split < PredictorCorrectorIterations
                        && result.Distance > LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters; split++)
                    {
                        finish = start + (finish - start) * MathF.BitDecrement(LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters) / result.Distance;
                        result = Solve(finish);
                    }
                    if (result.Distance > LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters)
                    {
                        var lo = start; var hi = finish;
                        for (var i = 0; i < BisectionIterations; i++)
                        {
                            var mid = (lo + hi) * .5d;
                            if (mid == lo || mid == hi) break;
                            if (Solve(mid).Distance > LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters) hi = mid;
                            else lo = mid;
                        }
                        finish = lo; result = Solve(finish);
                    }
                    // The diagonal part ends at actual target arrival; the remainder holds the target.
                    if (lateral != resolution.Lane && result.Lateral == resolution.Lane)
                    {
                        var rate = LateralMovementModel.CalculateMaxLateralDistanceMeters(1f, segment.Type, geometry,
                            nodes[^1].Surface, skills);
                        var arrivalTime = LateralSpaceModel.LateralDistanceMeters(lateral, resolution.Lane, segment.Type, geometry) / rate;
                        if (result.Time > arrivalTime + TimeToleranceSeconds)
                        {
                            var lo = start; var hi = finish;
                            for (var i = 0; i < BisectionIterations; i++)
                            {
                                var mid = (lo + hi) * .5d;
                                if (mid == lo || mid == hi) break;
                                if (Solve(mid).Time < arrivalTime) lo = mid; else hi = mid;
                            }
                            finish = hi; result = Solve(finish);
                        }
                    }
                    break;
                }
                catch (TimeSolveDiscontinuityException) when (subdivisions < TimeSolveSubdivisionLimit)
                {
                    // The unchanged correction/drive branch can have no time root
                    // over a finite step at its switching boundary. Subdivide the
                    // physical step and resolve again with the same primitives;
                    // never accept a stale lateral endpoint or alter the physics.
                    finish = start + (finish - start) * .5d;
                    subdivisions++;
                }
            }
            if (finish <= start || !double.IsFinite(result.Time) || result.Time <= 0d)
                throw new InvalidOperationException("Coupled traversal has no finite forward solution.");
            distance += result.Distance; time += result.Time;
            var sampledLateral = (lateral + result.Lateral) * .5f;
            steps.Add(new(result.Distance, (float)result.Time, sampledLateral,
                snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, sampledLateral),
                result.CorrectionDistance, result.CarryDistance, result.DriveDistance,
                result.CorrectionTime, result.CarryTime, result.DriveTime,
                result.Force, result.Iterations, result.Fallback) { TimeSolveSubdivisions = subdivisions });
            lateral = result.Lateral; speed = result.Speed; start = finish;
            nodes.Add(Node(start, lateral, crash && start == end ? 0f : speed));
        }
        return new(nodes, steps, reaction, setup);

        float? Progress(double local) => corner is null ? null
            : (float)((snapshot.Step.SegmentIndex - corner.StartSegmentIndex + local) / corner.SegmentCount);

        ExecutedPathNode Node(double local, float atLateral, float atSpeed)
        {
            var surface = snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, atLateral);
            var radius = corner is null ? (float?)null : LaneModel.TurnArcRadiusMeters(atLateral, geometry);
            var safe = corner is null ? (float?)null : SegmentPhysics.MaxSafeTurnSpeed(atLateral, geometry, surface, skills, setup);
            float? envelope = corner is null ? null : LocalEnvelope(local, atLateral, surface);
            var force = launch ? LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(skills, setup, surface)
                : corner is null ? LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(skills, setup, surface)
                : LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(skills, setup, surface);
            var availability = corner is null ? 1f : ContinuousCornerEnvelope.DriveAvailability(Progress(local)!.Value);
            return new((float)local, Progress(local), (float)time, (float)distance, atLateral,
                LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(atLateral, segment.Type, geometry),
                radius, radius.HasValue ? 1f / radius.Value : 0f, surface, atSpeed, safe, envelope,
                availability * LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(atSpeed, force, setup));
        }

        float LocalEnvelope(double local, float atLateral, TrackSurfaceState atSurface)
        {
            var phase = snapshot.Track.CornerTopology.Resolve(snapshot.Step.SegmentIndex, (float)local, atLateral, geometry)!.Value;
            // Pointwise local continuation, never an entry/average radius for the executed path.
            return ContinuousCornerEnvelope.Create(phase, atLateral, geometry, atSurface, skills, setup)
                .SpeedMetersPerSecond(Progress(local)!.Value);
        }

        CoupledStep Solve(double finish)
        {
            if (finish <= start) return default;
            var tangentDistance = LaneModel.SegmentLengthMeters(segment, lateral, geometry) * (finish - start);
            var entryForce = LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(skills, setup, nodes[^1].Surface);
            var guess = speed > 0f ? tangentDistance / speed : Math.Sqrt(2d * tangentDistance
                / LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(0f, entryForce, setup));
            CoupledStep evaluated = default;
            for (var i = 1; i <= PredictorCorrectorIterations; i++)
            {
                evaluated = Evaluate(guess, finish);
                if (Math.Abs(evaluated.Time - guess) <= TimeToleranceSeconds)
                    return evaluated with { Iterations = i };
                guess = evaluated.Time;
            }
            // Explicit bounded fallback, using the same equations (no penalty or stale lateral).
            double lo = 0d, hi = Math.Max(1d, guess * 2d);
            var bracketed = false;
            for (var i = 0; i < 32; i++)
            {
                if (hi >= Evaluate(hi, finish).Time) { bracketed = true; break; }
                hi *= 2d;
            }
            if (!bracketed) throw new InvalidOperationException("Coupled time solve could not bracket a finite root.");
            for (var i = 1; i <= BisectionIterations; i++)
            {
                guess = (lo + hi) * .5d; evaluated = Evaluate(guess, finish);
                if (Math.Abs(evaluated.Time - guess) <= TimeToleranceSeconds)
                    return evaluated with { Iterations = PredictorCorrectorIterations + i, Fallback = true };
                if (guess < evaluated.Time) lo = guess; else hi = guess;
            }
            throw new TimeSolveDiscontinuityException();
        }

        CoupledStep Evaluate(double seconds, double finish)
        {
            var atLateral = LateralMovementModel.MoveTowards(lateral, resolution.Lane, (float)seconds,
                segment.Type, geometry, nodes[^1].Surface, skills);
            var ds = GeometricDistance(segment, geometry, lateral, atLateral, finish - start);
            var sample = snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, (lateral + atLateral) * .5f);
            var force = launch ? LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(skills, setup, sample)
                : corner is null ? LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(skills, setup, sample)
                : LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(skills, setup, sample);
            var correctionCapability = LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(skills, sample);
            if (crash) return new(ds, ds / speed, atLateral, speed, 0f, ds, 0f, 0f, ds / speed, 0f, force, 0, false);
            if (corner is not null)
            {
                var target = LocalEnvelope(finish, atLateral,
                    snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, atLateral));
                var allowCorrection = resolution.Outcome != SegmentOutcome.RunWide
                    || resolution.ContinuousCorrectionTargetSpeedMetersPerSecond.HasValue;
                if (resolution.Outcome == SegmentOutcome.RunWide && resolution.ContinuousCorrectionTargetSpeedMetersPerSecond is { } retained)
                    target += MathF.Max(0f, retained - nodes[0].EnvelopeSpeedMetersPerSecond!.Value);
                if (allowCorrection && speed > target)
                {
                    var correction = LongitudinalDynamics.CalculateCornerSpeedCorrectionProfile(speed, target, correctionCapability, ds);
                    var carry = MathF.Max(0f, ds - correction.CorrectionDistanceMeters);
                    var carryTime = carry / correction.ExitSpeedMetersPerSecond;
                    return new(ds, (double)correction.TravelTimeSeconds + carryTime, atLateral, correction.ExitSpeedMetersPerSecond,
                        correction.CorrectionDistanceMeters, carry, 0f, correction.TravelTimeSeconds, carryTime, 0f, force, 0, false);
                }
                var availability = resolution.Outcome is SegmentOutcome.Ok or SegmentOutcome.Brake
                    ? ContinuousCornerEnvelope.DriveAvailability(Progress((start + finish) * .5d)!.Value) : 0f;
                var next = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(speed, ds, force, setup, availability);
                var dt = 2d * ds / ((double)speed + next);
                return new(ds, dt, atLateral, next, 0f, availability == 0f ? ds : 0f,
                    availability == 0f ? 0f : ds, 0f, availability == 0f ? (float)dt : 0f,
                    availability == 0f ? 0f : (float)dt, force, 0, false);
            }
            var candidate = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(speed, ds, force, setup);
            var targetExit = nextCornerTarget(atLateral);
            var boundary = targetExit is { } exit ? (float?)Math.Sqrt((double)exit * exit
                + 2d * correctionCapability * (1d - finish) * LaneModel.SegmentLengthMeters(segment, atLateral, geometry)) : null;
            var endSpeed = LongitudinalDynamics.ApplyPreparationBoundary(speed, candidate, ds, correctionCapability, boundary);
            var elapsed = 2d * ds / ((double)speed + endSpeed);
            return new(ds, elapsed, atLateral, endSpeed, 0f, 0f, ds, 0f, 0f, (float)elapsed, force, 0, false);
        }
    }

    private readonly record struct CoupledStep(float Distance, double Time, float Lateral, float Speed,
        float CorrectionDistance, float CarryDistance, float DriveDistance,
        float CorrectionTime, float CarryTime, float DriveTime, float Force, int Iterations, bool Fallback);
}
