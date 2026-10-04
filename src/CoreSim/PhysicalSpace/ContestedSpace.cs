using CoreSim.Race;

namespace CoreSim.PhysicalSpace;

[Flags]
public enum SpaceConflictKind
{
    None = 0, RearClosing = 1, AEncroachesByTranslation = 2, BEncroachesByTranslation = 4,
    AEncroachesByRotation = 8, BEncroachesByRotation = 16, AEncroachesMixed = 32, BEncroachesMixed = 64,
    MutualConvergence = 128, CrossingPaths = 256, ParallelOverlap = 512, BoundaryAmbiguous = 1024
}
public sealed record ClosingContributions(double ATranslationMeters, double ARotationMeters,
    double BTranslationMeters, double BRotationMeters, double ActualClosingMeters, double NonadditiveResidualMeters);
public sealed record ContestedSpaceEvent(int RiderA, int RiderB, string FrameId, double IntervalStartSeconds,
    double IntervalEndSeconds, double? FirstTouchCommonTimeSeconds, double MinimumSeparationMeters,
    double MinimumSeparationCommonTimeSeconds, BikeComponent ComponentA, BikeComponent ComponentB,
    SpaceConflictKind Kind, ClosingContributions Contributions, MeterPoint RelativePositionAtOnsetMeters,
    MeterPoint RelativeVelocityAtOnsetMetersPerSecond, bool NumericallyResolved, double MinimumSeparationLowerBoundMeters)
{
    public bool HasConflict => FirstTouchCommonTimeSeconds.HasValue;
    public double PenetrationMeters => Math.Max(0, -MinimumSeparationMeters);
    public bool EligibleForFutureInteraction => HasConflict && NumericallyResolved && !Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous);
}
public sealed record SpaceWorkCounters(int RiderPairs, int CandidateIntervals, int BroadPhaseRejects,
    int NarrowPhaseEvaluations, int AdaptiveSubdivisions, int RootIterations, int DetectedConflicts, int UnresolvedIntervals);
public sealed record FrameCoverageGap(int RiderA, int RiderB, double StartCommonTimeSeconds,
    double EndCommonTimeSeconds, string FrameA, string FrameB)
{
    public SpaceConflictKind Kind => SpaceConflictKind.BoundaryAmbiguous;
    public bool EligibleForFutureInteraction => false;
}
public sealed record ContestedSpaceReport(IReadOnlyList<ContestedSpaceEvent> Intervals, SpaceWorkCounters Work,
    IReadOnlyList<FrameCoverageGap> FrameCoverageGaps)
{
    public int IncompatibleFrameIntervals => FrameCoverageGaps.Count;
}

/// <summary>Deterministic pose-only observer. Owns no riders, surface, decision or RNG.</summary>
public static class ContestedSpaceResolver
{
    public static ContestedSpaceReport Observe(IEnumerable<PhysicalPoseInterval> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        var groups = intervals.GroupBy(i => i.RiderId).OrderBy(g => g.Key)
            .Select(g => g.OrderBy(i => i.StartTimeSeconds).ThenBy(i => i.EndTimeSeconds).ToArray()).ToArray();
        foreach (var group in groups)
            for (var i = 1; i < group.Length; i++)
                if (group[i].StartTimeSeconds < group[i - 1].EndTimeSeconds)
                    throw new ArgumentException("A rider's continuous pose intervals cannot overlap in heat time.");
        var rows = new List<ContestedSpaceEvent>(); var work = new Work(); var gaps = new List<FrameCoverageGap>();
        for (var a = 0; a < groups.Length; a++) for (var b = a + 1; b < groups.Length; b++)
        {
            work.Pairs++;
            var ai = 0; var bi = 0;
            var quarantined = false;
            var pairMinimum = new PairMinimum();
            string? previousFrame = null;
            var previousEnd = double.NaN;
            while (ai < groups[a].Length && bi < groups[b].Length)
            {
                var pa = groups[a][ai]; var pb = groups[b][bi];
                var start = Math.Max(pa.StartTimeSeconds, pb.StartTimeSeconds);
                var end = Math.Min(pa.EndTimeSeconds, pb.EndTimeSeconds);
                if (start < end)
                {
                    if (pa.FrameId == pb.FrameId)
                    {
                        var row = Evaluate(pa, pb, start, end, work, pairMinimum);
                        if (row.Kind == SpaceConflictKind.BoundaryAmbiguous) quarantined = true;
                        else if (quarantined && previousFrame == row.FrameId && previousEnd == start
                            && row.FirstTouchCommonTimeSeconds == start) row = row with { Kind = SpaceConflictKind.BoundaryAmbiguous };
                        else quarantined = false;
                        rows.Add(row); previousFrame = row.FrameId; previousEnd = end;
                    }
                    else
                    {
                        gaps.Add(new(pa.RiderId, pb.RiderId, start, end, pa.FrameId, pb.FrameId));
                        previousFrame = null; quarantined = false;
                    }
                }
                if (pa.EndTimeSeconds <= pb.EndTimeSeconds) ai++;
                if (pb.EndTimeSeconds <= pa.EndTimeSeconds) bi++;
            }
        }
        return new(rows.AsReadOnly(), work.Freeze(), gaps.AsReadOnly());
    }

    private static ContestedSpaceEvent Evaluate(PhysicalPoseInterval a, PhysicalPoseInterval b, double start, double end, Work work, PairMinimum pairMinimum)
    {
        work.Intervals++; var count = 0; var resolved = true; double? first = null;
        var minimum = double.PositiveInfinity; var minimumTime = start; FootprintSeparation minimumPair = default;
        var uncertainFirst = double.PositiveInfinity;
        var boundsA = a.RateBounds(start, end); var boundsB = b.RateBounds(start, end);
        foreach (var bounds in new[] { boundsA, boundsB })
        {
            GeometryValidation.Nonnegative(bounds.CenterAccelerationBoundMetersPerSecondSquared, nameof(bounds.CenterAccelerationBoundMetersPerSecondSquared));
            GeometryValidation.Nonnegative(bounds.AngularSpeedBoundRadiansPerSecond, nameof(bounds.AngularSpeedBoundRadiansPerSecond));
        }
        var relativeRate = (boundsA.CenterVelocityMetersPerSecond - boundsB.CenterVelocityMetersPerSecond).Length
            + (boundsA.CenterAccelerationBoundMetersPerSecondSquared + boundsB.CenterAccelerationBoundMetersPerSecondSquared) * (end - start) / 2;
        var lipschitz = relativeRate + a.Dimensions.BoundingRadiusMeters * boundsA.AngularSpeedBoundRadiansPerSecond
            + b.Dimensions.BoundingRadiusMeters * boundsB.AngularSpeedBoundRadiansPerSecond;
        var sa = a.Sample(start); var sb = b.Sample(start); var ea = a.Sample(end); var eb = b.Sample(end);
        var sphereLower = Math.Min((sa.Position - sb.Position).Length, (ea.Position - eb.Position).Length)
            - relativeRate * (end - start) / 2 - a.Dimensions.BoundingRadiusMeters - b.Dimensions.BoundingRadiusMeters;
        var broadClear = sphereLower > GeometryNumerics.BroadPhasePaddingMeters;
        if (broadClear) work.Broad++;
        double Gap(double time)
        {
            count++; work.Narrow++;
            var separation = MechanicalSeparation.Between(a.Sample(time), b.Sample(time));
            if (separation.SignedMeters < minimum)
            { minimum = separation.SignedMeters; minimumTime = time; minimumPair = separation; }
            pairMinimum.Value = Math.Min(pairMinimum.Value, separation.SignedMeters);
            if (!broadClear && separation.SignedMeters <= GeometryNumerics.ContactDistanceMeters
                && (!first.HasValue || time < first)) first = time;
            return separation.SignedMeters;
        }
        void Search(double lo, double hi, double gl, double gh, int depth)
        {
            var lower = Math.Max(-Math.Max(a.Dimensions.ChassisBodyWidthMeters, a.Dimensions.HandlebarTubeDiameterMeters) / 2
                - Math.Max(b.Dimensions.ChassisBodyWidthMeters, b.Dimensions.HandlebarTubeDiameterMeters) / 2,
                Math.Min(gl, gh) - lipschitz * (hi - lo) / 2);
            var needsFirst = !broadClear && (!first.HasValue || lo < first.Value - GeometryNumerics.TimeToleranceSeconds)
                && lower <= GeometryNumerics.ContactDistanceMeters;
            // Certify the pair-wide minimum, not every far-away knot's local minimum.
            // This lets broad separated intervals contribute two endpoint samples only.
            var needsMinimum = lower < pairMinimum.Value - GeometryNumerics.MinimumSeparationToleranceMeters;
            if (!needsFirst && !needsMinimum) return;
            if (depth >= GeometryNumerics.MaximumSubdivisionDepth || count >= GeometryNumerics.MaximumEvaluationsPerInterval)
            { resolved = false; return; }
            if (hi - lo <= GeometryNumerics.TimeToleranceSeconds)
            {
                if (needsMinimum && lipschitz * (hi - lo) / 2 > GeometryNumerics.MinimumSeparationToleranceMeters) resolved = false;
                // A near-tangent uncertainty remains explicit; never silently claim a clear pair.
                if (needsFirst && gl > GeometryNumerics.ContactDistanceMeters && gh > GeometryNumerics.ContactDistanceMeters)
                    uncertainFirst = Math.Min(uncertainFirst, lo);
                return;
            }
            var mid = (lo + hi) / 2; var gm = Gap(mid);
            work.Subdivisions++;
            if (needsFirst) work.Roots++;
            Search(lo, mid, gl, gm, depth + 1); Search(mid, hi, gm, gh, depth + 1);
        }
        var gStart = Gap(start); var gEnd = Gap(end);
        Search(start, end, gStart, gEnd, 0);
        if (uncertainFirst < (first ?? double.PositiveInfinity) - 2 * GeometryNumerics.TimeToleranceSeconds) resolved = false;
        if (first.HasValue) work.Conflicts++;
        if (!resolved) work.Unresolved++;
        var onsetEnd = first ?? end;
        // Use the whole onset knot interval, rather than tiny root brackets whose effects vanish.
        var onsetA = a.Sample(onsetEnd); var onsetB = b.Sample(onsetEnd);
        var contributions = Closing(sa, sb, onsetA, onsetB);
        var kind = first.HasValue ? Classify(sa, sb, ea, eb, contributions, boundsA, boundsB) : SpaceConflictKind.None;
        if (first == start && ((a.StartsAtDiscontinuity && start == a.StartTimeSeconds)
            || (b.StartsAtDiscontinuity && start == b.StartTimeSeconds))) kind = SpaceConflictKind.BoundaryAmbiguous;
        return new(a.RiderId, b.RiderId, a.FrameId, start, end, first, minimum, minimumTime,
            minimumPair.ComponentA, minimumPair.ComponentB, kind, contributions, sb.Position - sa.Position,
            boundsB.CenterVelocityMetersPerSecond - boundsA.CenterVelocityMetersPerSecond, resolved,
            Math.Min(gStart, gEnd) - lipschitz * (end - start) / 2);
    }

    private static ClosingContributions Closing(PhysicalBikePose a0, PhysicalBikePose b0, PhysicalBikePose a1, PhysicalBikePose b1)
    {
        var time = a1.CommonTimeSeconds;
        var da = a1.Position - a0.Position; var db = b1.Position - b0.Position;
        // Remove only shared forward transport for nearly parallel travel. This prevents
        // two bikes moving at 20 m/s together from receiving fictitious opposing closure.
        var referenceHeading = BikeAngles.Interpolate(a0.ReferenceTangentHeadingRadians, b0.ReferenceTangentHeadingRadians, .5);
        var forward = new MeterPoint(Math.Cos(referenceHeading), Math.Sin(referenceHeading));
        var common = Math.Abs(BikeAngles.Wrap(a0.ReferenceTangentHeadingRadians - b0.ReferenceTangentHeadingRadians)) < ParallelClassificationAngleRadians
            ? forward * Math.Max(0, Math.Min(MeterPoint.Dot(da, forward), MeterPoint.Dot(db, forward))) : new MeterPoint(0, 0);
        var af = a0.Repose(a0.Position, a0.Attitude.BikeHeadingRadians, time);
        var bf = b0.Repose(b0.Position, b0.Attitude.BikeHeadingRadians, time);
        var at = af.Repose(af.Position + da - common, af.Attitude.BikeHeadingRadians, time);
        var bt = bf.Repose(bf.Position + db - common, bf.Attitude.BikeHeadingRadians, time);
        var ar = af.Repose(af.Position, a1.Attitude.BikeHeadingRadians, time);
        var br = bf.Repose(bf.Position, b1.Attitude.BikeHeadingRadians, time);
        var baseline = MechanicalSeparation.Between(af, bf).SignedMeters;
        var cat = baseline - MechanicalSeparation.Between(at, bf).SignedMeters;
        var car = baseline - MechanicalSeparation.Between(ar, bf).SignedMeters;
        var cbt = baseline - MechanicalSeparation.Between(af, bt).SignedMeters;
        var cbr = baseline - MechanicalSeparation.Between(af, br).SignedMeters;
        var actual = baseline - MechanicalSeparation.Between(a1, b1).SignedMeters;
        return new(cat, car, cbt, cbr, actual, actual - cat - car - cbt - cbr);
    }
    private static SpaceConflictKind Classify(PhysicalBikePose a, PhysicalBikePose b, PhysicalBikePose ea, PhysicalBikePose eb,
        ClosingContributions c, PoseRateBounds ra, PoseRateBounds rb)
    {
        var epsilon = GeometryNumerics.ContactDistanceMeters;
        var at = c.ATranslationMeters > epsilon; var ar = c.ARotationMeters > epsilon;
        var bt = c.BTranslationMeters > epsilon; var br = c.BRotationMeters > epsilon;
        var result = SpaceConflictKind.None;
        if (at) result |= SpaceConflictKind.AEncroachesByTranslation;
        if (ar) result |= SpaceConflictKind.AEncroachesByRotation;
        if (bt) result |= SpaceConflictKind.BEncroachesByTranslation;
        if (br) result |= SpaceConflictKind.BEncroachesByRotation;
        if (at && ar) result |= SpaceConflictKind.AEncroachesMixed;
        if (bt && br) result |= SpaceConflictKind.BEncroachesMixed;
        if ((at || ar) && (bt || br)) result |= SpaceConflictKind.MutualConvergence;
        var heading = new MeterPoint(Math.Cos(a.Attitude.TravelHeadingRadians), Math.Sin(a.Attitude.TravelHeadingRadians));
        var relative = b.Position - a.Position;
        var lateral = Math.Abs(MeterPoint.Cross(heading, relative));
        var parallel = Math.Abs(BikeAngles.Wrap(a.Attitude.TravelHeadingRadians - b.Attitude.TravelHeadingRadians)) < ParallelClassificationAngleRadians;
        if (parallel && lateral <= (a.Dimensions.ChassisBodyWidthMeters + b.Dimensions.ChassisBodyWidthMeters) / 2
            && Math.Abs(MeterPoint.Dot(relative, heading)) > lateral
            && MeterPoint.Dot(relative, heading) * MeterPoint.Dot(rb.CenterVelocityMetersPerSecond - ra.CenterVelocityMetersPerSecond, heading) < 0)
            result |= SpaceConflictKind.RearClosing;
        if (MeterPoint.Cross(heading, relative) * MeterPoint.Cross(heading, eb.Position - ea.Position) < 0)
            result |= SpaceConflictKind.CrossingPaths;
        if (result == SpaceConflictKind.None) result = SpaceConflictKind.ParallelOverlap;
        return result;
    }
    private sealed class Work
    {
        public int Pairs, Intervals, Broad, Narrow, Subdivisions, Roots, Conflicts, Unresolved;
        public SpaceWorkCounters Freeze() => new(Pairs, Intervals, Broad, Narrow, Subdivisions, Roots, Conflicts, Unresolved);
    }
    private sealed class PairMinimum { public double Value = double.PositiveInfinity; }
    /// <summary>Diagnostic direction category only; never collision clearance or an angle equality tolerance.</summary>
    public const double ParallelClassificationAngleRadians = .1;
}

/// <summary>Explicit rich capture: attach to an actual heat, then Complete after all motions exist.</summary>
public sealed class PhysicalSpaceObserver : ISimulationStepObserver
{
    private readonly List<PhysicalPoseInterval> _intervals = new();
    private readonly SpeedwayBikeDimensions _dimensions;
    private readonly ReferenceBikeAttitude _attitude;
    public PhysicalSpaceObserver(SpeedwayBikeDimensions? dimensions = null, ReferenceBikeAttitude? attitude = null)
    { _dimensions = dimensions ?? SpeedwayBikeDimensions.Reference; _attitude = attitude ?? ReferenceBikeAttitude.Neutral; }
    public void OnStepResolved(ResolvedSimulationStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        foreach (var motion in step.Motions) _intervals.AddRange(ResolvedBikePoses.FromMotion(motion, step.Snapshot.Track, _dimensions, _attitude));
    }
    public IReadOnlyList<PhysicalPoseInterval> CapturedIntervals => _intervals.AsReadOnly();
    public ContestedSpaceReport Complete() => ContestedSpaceResolver.Observe(_intervals);
}
