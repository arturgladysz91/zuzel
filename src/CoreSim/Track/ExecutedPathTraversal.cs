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

    internal abstract class TimeSolveFeasibilityException(string message) : InvalidOperationException(message);

    internal sealed class TimeSolveDiscontinuityException : TimeSolveFeasibilityException
    {
        public TimeSolveDiscontinuityException() : base("Coupled time solve did not converge within its bound.") { }
    }

    internal sealed class NonFiniteTimeSolveException(string message) : TimeSolveFeasibilityException(message);

    internal static bool IsFixedLine(float lateral, float target)
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

    internal static ExecutedTraversalResult Traverse(SimulationSnapshot snapshot, RiderSnapshot rider,
        SegmentResolution resolution, float entrySpeed, float canonicalAdvance, bool launch,
        Func<float, float?> nextCornerTarget, bool captureRich = true, float positiveDriveFraction = 1f,
        bool holdLateralPosition = false, float lateralAuthority01 = 1f, bool contactRecovery = false,
        float? interactionTargetPosition = null, TraversalPauseRequest? pause = null)
    {
        if (pause?.CausalHeat == true && (!snapshot.Step.FreezeSurfaceForHeat || !captureRich
            || snapshot.Step.UseLegacyPhysics || resolution.Outcome == SegmentOutcome.Crash))
            throw new RiderIntegrationRefusalException($"Causal cursor R{rider.RiderId}: unsupported terminal entry physics {resolution.Outcome} in {snapshot.Segment.Type}.");
        if (pause is not null && !pause.CausalHeat && (!captureRich || launch
            || snapshot.Step.UseLegacyPhysics || snapshot.Segment.Type is not (SegmentType.Straight or SegmentType.TurnExit)
            || resolution.Outcome != SegmentOutcome.Ok || entrySpeed <= 0f
            || !pause.AfterContact && (contactRecovery || lateralAuthority01 != 1f
                || snapshot.Segment.Type != SegmentType.Straight && positiveDriveFraction != 1f)
            || holdLateralPosition && snapshot.Segment.Type != SegmentType.Straight
            || interactionTargetPosition.HasValue))
            throw new RiderIntegrationRefusalException("Cursor requires rich rolling Ok traversal; captured Straight drive/hold controls are supported, corner controls and incoming recovery are not.");
        var segment = snapshot.Segment; var geometry = snapshot.Track.Geometry;
        var corner = snapshot.Track.CornerTopology.CornerForSegment(snapshot.Step.SegmentIndex);
        var skills = rider.Profile.Skills; var setup = rider.ActiveSetup;
        // Exact, segment-local reuse of immutable envelope inputs. Every sample
        // still uses the production surface interpolation and factory. The existing
        // apex-anchored metre integrator remains the only speed implementation.
        var retainedCorner = pause?.Boundary?.Corner;
        var envelopes = corner is null ? null : retainedCorner?.Cache.Envelopes ?? new Dictionary<EnvelopeKey, ContinuousCornerEnvelope>();
        var scalarSpeeds = corner is null ? null : retainedCorner?.Cache.ScalarSpeeds ?? new Dictionary<EnvelopeQueryKey, float>();
        var start = (double)rider.SegmentProgress;
        var end = start + canonicalAdvance;
        var lateral = rider.LateralPosition;
        var reaction = launch ? LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(skills) : 0f;
        double time = reaction, distance = 0d;
        var crash = resolution.Outcome == SegmentOutcome.Crash;
        // Zero requested lateral delta is a local control, not extra grip or a
        // displacement. Forced RunWide keeps its existing production movement.
        var voluntaryLateralAuthority = resolution.Outcome == SegmentOutcome.RunWide ? 1f : lateralAuthority01;
        var hold = holdLateralPosition && resolution.Outcome is SegmentOutcome.Ok or SegmentOutcome.Brake;
        var lateralTarget = interactionTargetPosition.HasValue && resolution.Outcome is SegmentOutcome.Ok or SegmentOutcome.Brake
            ? interactionTargetPosition.Value : resolution.Lane;
        var speed = crash ? MathF.Max(1f, entrySpeed * .5f) : resolution.Speed;
        var nodeSurface = snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, lateral);
        var initialEnvelope = corner is null ? (float?)null : retainedCorner?.InitialEnvelopeSpeed ?? LocalEnvelope(start, lateral, nodeSurface);
        var nodes = captureRich ? pause?.Boundary is { } savedNodes ? new List<ExecutedPathNode>(savedNodes.Nodes)
            : new List<ExecutedPathNode> { Node(start, lateral, speed) } : null;
        var steps = captureRich ? pause?.Boundary is { } savedSteps ? new List<ExecutedPathStep>(savedSteps.Steps)
            : new List<ExecutedPathStep>() : null;
        var maxNodes = checked((int)Math.Ceiling(LaneModel.SegmentLengthMeters(segment, 4f, geometry)
            + LaneModel.UsableRacingWidthMeters(segment.Type, geometry)) * 4 + 16);
        // Normalized wear must divide each distance by the final float total before
        // adding its kernel. Keep only two value fields, preserving that exact order.
        Span<ExecutedWearSample> wearSamples = captureRich ? Span<ExecutedWearSample>.Empty
            : maxNodes <= 1024 ? stackalloc ExecutedWearSample[maxNodes] : new ExecutedWearSample[maxNodes];
        var stepCount = 0; var subdivisionTotal = 0;
        float? apexLateral = retainedCorner is not null ? retainedCorner.ApexLateralPosition
            : Progress(start) == ContinuousCornerEnvelope.ApexProgress ? lateral : null;
        if (pause?.Boundary is { } boundary)
        {
            start = boundary.CanonicalProgress; end = boundary.EndCanonicalProgress;
            time = boundary.TimeSeconds; distance = boundary.DistanceMeters;
            speed = boundary.Speed; lateral = boundary.LateralPosition; nodeSurface = boundary.NodeSurface;
            stepCount = boundary.CompletedSteps; subdivisionTotal = boundary.TimeSolveSubdivisions;
        }
        var resumeStepCount = stepCount;
        var executionEndpoints = pause is null ? null : pause.Boundary?.ExecutionEndpoints is { } savedEndpoints
            ? new List<ExecutedStepEndpoint>(savedEndpoints)
            : new List<ExecutedStepEndpoint> { new(start, time, distance, speed, lateral) };
        if (pause?.AdditionalSteps == 0 && pause.Boundary is null)
            throw new TraversalPausedException(new(start, end, time, distance, speed, lateral,
                nodeSurface, 0, subdivisionTotal, Array.AsReadOnly(nodes!.ToArray()), Array.AsReadOnly(steps!.ToArray()),
                corner is null ? null : new(initialEnvelope!.Value, apexLateral, Progress(start)!.Value,
                    new CornerExecutionCache(envelopes!, scalarSpeeds!)),
                ExecutionEndpoints: Array.AsReadOnly(executionEndpoints!.ToArray())));
        while (start < end)
        {
            if (stepCount >= maxNodes) throw new InvalidOperationException("Coupled traversal exceeded its geometric node bound.");
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
                    if (!hold && lateral != lateralTarget && result.Lateral == lateralTarget)
                    {
                        var rate = LateralMovementModel.CalculateMaxLateralDistanceMeters(1f, segment.Type, geometry,
                            nodeSurface, skills) * voluntaryLateralAuthority;
                        var arrivalTime = LateralSpaceModel.LateralDistanceMeters(lateral, lateralTarget, segment.Type, geometry) / rate;
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
            ExecutedPartialStep? partial = null;
            if (pause?.PartialStep is { } search)
            {
                if (!double.IsFinite(search.TimeBracketToleranceSeconds) || search.TimeBracketToleranceSeconds <= 0
                    || search.TimeBracketToleranceSeconds > TimeToleranceSeconds)
                    throw new ArgumentOutOfRangeException(nameof(search.TimeBracketToleranceSeconds));
                // Trial evaluations never append a node/step or advance any accumulator.
                // Every endpoint uses the existing coupled equations from this exact boundary.
                var unsplitEnd = finish; var unsplitTime = time + result.Time;
                var lo = start; var hi = finish; var loTime = time; var hiTime = unsplitTime;
                var trials = 1;
                ExecutedStepEndpoint Endpoint(double progress, CoupledStep step) =>
                    new(progress, time + step.Time, distance + step.Distance, step.Speed, step.Lateral);
                if (search.Reached(new(start, time, distance, speed, lateral)))
                    throw new RiderIntegrationRefusalException("Partial-step event is already reached at the accepted boundary.");
                var reached = search.Reached(Endpoint(hi, result));
                if (!reached && !search.ContinueBeforeBracket)
                    throw new RiderIntegrationRefusalException("Requested event is not bracketed inside the next numerical step.");
                if (reached)
                {
                    for (var i = 0; i < BisectionIterations && hiTime - loTime > search.TimeBracketToleranceSeconds; i++)
                    {
                        var mid = (lo + hi) * .5d;
                        if (mid == lo || mid == hi) break;
                        var trial = Solve(mid); trials++;
                        if (!(time + trial.Time > loTime && time + trial.Time < hiTime))
                            throw new RiderIntegrationRefusalException("Partial-step time is not strictly monotonic inside its bracket.");
                        if (search.Reached(Endpoint(mid, trial)))
                        { hi = mid; hiTime = time + trial.Time; result = trial; }
                        else { lo = mid; loTime = time + trial.Time; }
                    }
                    if (hiTime - loTime > search.TimeBracketToleranceSeconds || hi <= start || hi >= unsplitEnd)
                        throw new RiderIntegrationRefusalException("No resolved interior partial-step endpoint within the numerical bound.");
                    finish = hi;
                    partial = new(start, unsplitEnd, time, unsplitTime, lo, loTime, trials);
                }
            }
            distance += result.Distance; time += result.Time;
            var sampledLateral = (lateral + result.Lateral) * .5f;
            if (captureRich)
            {
                ProjectionCaptureAudit.Record(ProjectionMaterialization.ExecutedStep);
                steps!.Add(new(result.Distance, (float)result.Time, sampledLateral,
                    snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, sampledLateral),
                    result.CorrectionDistance, result.CarryDistance, result.DriveDistance,
                    result.CorrectionTime, result.CarryTime, result.DriveTime,
                    result.Force, result.Iterations, result.Fallback) { TimeSolveSubdivisions = subdivisions });
            }
            else wearSamples[stepCount] = new(result.Distance, sampledLateral);
            stepCount++; subdivisionTotal += subdivisions;
            lateral = result.Lateral; speed = result.Speed; start = finish;
            nodeSurface = snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, lateral);
            if (Progress(start) == ContinuousCornerEnvelope.ApexProgress) apexLateral ??= lateral;
            if (captureRich) nodes!.Add(Node(start, lateral, crash && start == end ? 0f : speed));
            executionEndpoints?.Add(new(start, time, distance, speed, lateral));
            // Only accepted steps can pause: all subdivisions, arrival splits and accumulator
            // updates above have finished. No final path, wear normalization or event exists yet.
            if (partial is not null || (start < end || pause?.CausalHeat == true)
                && pause?.AdditionalSteps is { } limit && stepCount - resumeStepCount == limit)
                throw new TraversalPausedException(new(start, end, time, distance, speed, lateral,
                    nodeSurface, stepCount, subdivisionTotal, Array.AsReadOnly(nodes!.ToArray()),
                    Array.AsReadOnly(steps!.ToArray()), corner is null ? null : new(initialEnvelope!.Value,
                        apexLateral, Progress(start)!.Value,
                        retainedCorner?.Cache ?? new CornerExecutionCache(envelopes!, scalarSpeeds!)), partial,
                    Array.AsReadOnly(executionEndpoints!.ToArray())));
        }
        var finalDistance = (float)distance;
        ProjectionWear wear = default;
        if (!captureRich)
        {
            var weights = new float[LaneModel.LanesCount];
            for (var i = 0; i < stepCount; i++)
                SimulationEngine.AddInterpolatedWearKernel(weights, wearSamples[i].LateralPosition,
                    wearSamples[i].DistanceMeters / finalDistance);
            wear = SimulationEngine.FinishExecutedWear(weights, rider.LateralPosition);
        }
        return new(captureRich ? new ExecutedSegmentPath(nodes!, steps!, reaction, setup) : null,
            finalDistance, (float)time, lateral, crash ? 0f : speed, apexLateral, wear, subdivisionTotal);

        float? Progress(double local) => corner is null ? null
            : (float)((snapshot.Step.SegmentIndex - corner.StartSegmentIndex + local) / corner.SegmentCount);

        ExecutedPathNode Node(double local, float atLateral, float atSpeed)
        {
            ProjectionCaptureAudit.Record(ProjectionMaterialization.ExecutedNode);
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
                contactRecovery
                    ? availability * LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(atSpeed, force * positiveDriveFraction, setup)
                    : positiveDriveFraction < 1f
                        ? LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(atSpeed, force * positiveDriveFraction * availability, setup)
                        : availability * LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(atSpeed, force, setup));
        }

        float LocalEnvelope(double local, float atLateral, TrackSurfaceState atSurface)
        {
            var key = new EnvelopeKey(BitConverter.SingleToInt32Bits(atLateral),
                BitConverter.SingleToInt32Bits(atSurface.Grip), BitConverter.SingleToInt32Bits(atSurface.Ruts),
                BitConverter.SingleToInt32Bits(atSurface.Moisture));
            var progress = Progress(local)!.Value;
            // Held target positions benefit from successive cached metre prefixes.
            // Distinct moving samples usually need one value: store that exact query,
            // without an envelope object, lock, list or expandable speed array.
            if (!IsFixedLine(atLateral, lateralTarget))
            {
                var query = new EnvelopeQueryKey(key, BitConverter.SingleToInt32Bits(progress));
                if (!scalarSpeeds!.TryGetValue(query, out var value))
                {
                    var phase = snapshot.Track.CornerTopology.Resolve(snapshot.Step.SegmentIndex, (float)local, atLateral, geometry)!.Value;
                    value = ContinuousCornerEnvelope.CreateParameters(phase, atLateral, geometry, atSurface, skills, setup)
                        .SpeedMetersPerSecond(progress);
                    scalarSpeeds.Add(query, value);
                }
                return value;
            }
            if (!envelopes!.TryGetValue(key, out var envelope))
            {
                var phase = snapshot.Track.CornerTopology.Resolve(snapshot.Step.SegmentIndex, (float)local, atLateral, geometry)!.Value;
                envelope = ContinuousCornerEnvelope.Create(phase, atLateral, geometry, atSurface, skills, setup);
                envelopes.Add(key, envelope);
            }
            return envelope.SpeedMetersPerSecond(progress);
        }

        CoupledStep Solve(double finish)
        {
            if (finish <= start) return default;
            var tangentDistance = LaneModel.SegmentLengthMeters(segment, lateral, geometry) * (finish - start);
            var guess = speed > 0f ? tangentDistance / speed : Math.Sqrt(2d * tangentDistance
                / LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(0f,
                    LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(skills, setup, nodeSurface), setup));
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
            if (!bracketed) throw new NonFiniteTimeSolveException("Coupled time solve could not bracket a finite root.");
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
            // This time is generated by the coupled solve, not supplied by a caller.
            // Classify its numerical infeasibility before passing it to validated movement APIs.
            if (!double.IsFinite(seconds) || !float.IsFinite((float)seconds))
                throw new NonFiniteTimeSolveException("Coupled time solve generated a non-finite movement time.");
            ProjectionCaptureAudit.Record(ProjectionMaterialization.CoupledEvaluation);
            var atLateral = hold ? lateral : LateralMovementModel.MoveTowardsContinuous(lateral, lateralTarget, (float)seconds * voluntaryLateralAuthority,
                segment.Type, geometry, nodeSurface, skills);
            var ds = GeometricDistance(segment, geometry, lateral, atLateral, finish - start);
            var sample = snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, (lateral + atLateral) * .5f);
            // Force is physical input during drive, but only an observation during
            // correction/carry/crash. Full still records it in every rich step.
            float DriveForce() => launch ? LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(skills, setup, sample)
                : corner is null ? LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(skills, setup, sample)
                : LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(skills, setup, sample);
            var force = captureRich ? DriveForce() : 0f;
            if (crash) return new(ds, ds / speed, atLateral, speed, 0f, ds, 0f, 0f, ds / speed, 0f, force, 0, false);
            if (corner is not null)
            {
                var target = LocalEnvelope(finish, atLateral,
                    snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex, atLateral));
                var allowCorrection = resolution.Outcome != SegmentOutcome.RunWide
                    || resolution.ContinuousCorrectionTargetSpeedMetersPerSecond.HasValue;
                if (resolution.Outcome == SegmentOutcome.RunWide && resolution.ContinuousCorrectionTargetSpeedMetersPerSecond is { } retained)
                    target += MathF.Max(0f, retained - initialEnvelope!.Value);
                if (allowCorrection && speed > target)
                {
                    var correctionCapability = LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(skills, sample);
                    var correction = LongitudinalDynamics.CalculateCornerSpeedCorrectionProfile(speed, target, correctionCapability, ds);
                    var carry = MathF.Max(0f, ds - correction.CorrectionDistanceMeters);
                    var carryTime = carry / correction.ExitSpeedMetersPerSecond;
                    return new(ds, (double)correction.TravelTimeSeconds + carryTime, atLateral, correction.ExitSpeedMetersPerSecond,
                        correction.CorrectionDistanceMeters, carry, 0f, correction.TravelTimeSeconds, carryTime, 0f, force, 0, false);
                }
                var availability = resolution.Outcome is SegmentOutcome.Ok or SegmentOutcome.Brake
                    ? ContinuousCornerEnvelope.DriveAvailability(Progress((start + finish) * .5d)!.Value) : 0f;
                if (!captureRich) force = DriveForce();
                var next = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(speed, ds, force * positiveDriveFraction, setup, availability);
                var dt = 2d * ds / ((double)speed + next);
                return new(ds, dt, atLateral, next, 0f, availability == 0f ? ds : 0f,
                    availability == 0f ? 0f : ds, 0f, availability == 0f ? (float)dt : 0f,
                    availability == 0f ? 0f : (float)dt, force, 0, false);
            }
            if (!captureRich) force = DriveForce();
            var candidate = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(speed, ds, force * positiveDriveFraction, setup);
            var targetExit = nextCornerTarget(atLateral);
            var preparationCapability = LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(skills, sample);
            var boundary = targetExit is { } exit ? (float?)Math.Sqrt((double)exit * exit
                + 2d * preparationCapability * (1d - finish) * LaneModel.SegmentLengthMeters(segment, atLateral, geometry)) : null;
            var endSpeed = LongitudinalDynamics.ApplyPreparationBoundary(speed, candidate, ds, preparationCapability, boundary);
            var elapsed = 2d * ds / ((double)speed + endSpeed);
            return new(ds, elapsed, atLateral, endSpeed, 0f, 0f, ds, 0f, 0f, (float)elapsed, force, 0, false);
        }
    }

    // One consumed token transfers these caches to the next traversal; rebuilding them
    // would repeat apex metre integration even though the rider prefix was retained.
    internal sealed class CornerExecutionCache(Dictionary<EnvelopeKey, ContinuousCornerEnvelope> envelopes,
        Dictionary<EnvelopeQueryKey, float> scalarSpeeds)
    {
        internal Dictionary<EnvelopeKey, ContinuousCornerEnvelope> Envelopes { get; } = envelopes;
        internal Dictionary<EnvelopeQueryKey, float> ScalarSpeeds { get; } = scalarSpeeds;
    }
    internal readonly record struct EnvelopeQueryKey(EnvelopeKey Inputs, int ProgressBits);
    internal readonly record struct EnvelopeKey(int LateralBits, int GripBits, int RutsBits, int MoistureBits);

    private readonly record struct CoupledStep(float Distance, double Time, float Lateral, float Speed,
        float CorrectionDistance, float CarryDistance, float DriveDistance,
        float CorrectionTime, float CarryTime, float DriveTime, float Force, int Iterations, bool Fallback);
}
