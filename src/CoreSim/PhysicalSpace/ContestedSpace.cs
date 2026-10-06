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
    MeterPoint RelativeVelocityAtOnsetMetersPerSecond, bool NumericallyResolved, double MinimumSeparationLowerBoundMeters,
    PoseSource? SourceA = null, PoseSource? SourceB = null)
{
    public bool HasConflict => FirstTouchCommonTimeSeconds.HasValue;
    public double PenetrationMeters => Math.Max(0, -MinimumSeparationMeters);
    public bool EligibleForFutureInteraction => HasConflict && NumericallyResolved && !Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous);
}
internal readonly record struct SpaceIntervalValue(int RiderA, int RiderB, string FrameId, double IntervalStartSeconds,
    double IntervalEndSeconds, double? FirstTouchCommonTimeSeconds, double MinimumSeparationMeters,
    double MinimumSeparationCommonTimeSeconds, BikeComponent ComponentA, BikeComponent ComponentB,
    SpaceConflictKind Kind, ClosingContributions Contributions, MeterPoint RelativePositionAtOnsetMeters,
    MeterPoint RelativeVelocityAtOnsetMetersPerSecond, bool NumericallyResolved, double MinimumSeparationLowerBoundMeters,
    PoseSource? SourceA = null, PoseSource? SourceB = null)
{
    public bool HasConflict => FirstTouchCommonTimeSeconds.HasValue;
    public double PenetrationMeters => Math.Max(0, -MinimumSeparationMeters);
    public bool EligibleForFutureInteraction => HasConflict && NumericallyResolved && !Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous);
    internal ContestedSpaceEvent ToEvent() => new(RiderA, RiderB, FrameId, IntervalStartSeconds, IntervalEndSeconds,
        FirstTouchCommonTimeSeconds, MinimumSeparationMeters, MinimumSeparationCommonTimeSeconds, ComponentA, ComponentB,
        Kind, Contributions, RelativePositionAtOnsetMeters, RelativeVelocityAtOnsetMetersPerSecond, NumericallyResolved,
        MinimumSeparationLowerBoundMeters, SourceA, SourceB);
}
internal sealed class SpaceCompatibilityResult(double ready)
{
    internal double MinimumSeparation = double.PositiveInfinity;
    internal bool HasBoundaryAmbiguity, HasCoverageGap;
    internal int IneligibleIntervals, EligibleIntervals;
    internal readonly List<ContestedSpaceEvent> Contacts = new();
    internal SpaceWorkCounters Work = new(0,0,0,0,0,0,0,0);
    internal void Add(SpaceIntervalValue row)
    {
        if (row.IntervalEndSeconds <= ready) return;
        if (!row.NumericallyResolved || row.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous))
        { HasBoundaryAmbiguity = true; IneligibleIntervals++; return; }
        EligibleIntervals++; MinimumSeparation = Math.Min(MinimumSeparation, row.MinimumSeparationMeters);
        if (!row.EligibleForFutureInteraction) return;
        foreach (var contact in Contacts)
            if (contact.RiderA == row.RiderA && contact.RiderB == row.RiderB) return;
        Contacts.Add(row.ToEvent());
    }
    internal void Gap(FrameCoverageGap gap) { HasCoverageGap |= gap.EndCommonTimeSeconds > ready; }
}

public sealed record SpaceWorkCounters(int RiderPairs, int CandidateIntervals, int BroadPhaseRejects,
    int NarrowPhaseEvaluations, int AdaptiveSubdivisions, int RootIterations, int DetectedConflicts, int UnresolvedIntervals);
public sealed record FrameCoverageGap(int RiderA, int RiderB, double StartCommonTimeSeconds,
    double EndCommonTimeSeconds, string FrameA, string FrameB, string Reason = "UnrelatedMetricFrames")
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
    private static readonly ClosingContributions EmptyContributions = new(0, 0, 0, 0, 0, 0);
    public static ContestedSpaceReport Observe(IEnumerable<PhysicalPoseInterval> intervals) => Observe(intervals, true);
    // Compatibility uses identical #55 certification, without unused attribution poses.
    internal static SpaceCompatibilityResult ObserveCompatibility(IEnumerable<PhysicalPoseInterval> intervals, double ready)
    {
        var summary = new SpaceCompatibilityResult(ready);
        summary.Work = Observe(intervals, false, summary).Work;
        return summary;
    }
    private static ContestedSpaceReport Observe(IEnumerable<PhysicalPoseInterval> intervals, bool attribution, SpaceCompatibilityResult? summary = null)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        var groups = intervals.GroupBy(i => i.RiderId).OrderBy(g => g.Key)
            .Select(g => g.OrderBy(i => i.StartTimeSeconds).ThenBy(i => i.EndTimeSeconds).ToArray()).ToArray();
        foreach (var group in groups)
            for (var i = 1; i < group.Length; i++)
                if (group[i].StartTimeSeconds < group[i - 1].EndTimeSeconds)
                    throw new ArgumentException("A rider's continuous pose intervals cannot overlap in heat time.");
        var rows = new List<ContestedSpaceEvent>(); var work = new Work(); var gaps = new List<FrameCoverageGap>();
        void Add(SpaceIntervalValue row) { if (summary is null) rows.Add(row.ToEvent()); else summary.Add(row); }
        for (var a = 0; a < groups.Length; a++) for (var b = a + 1; b < groups.Length; b++)
        {
            work.Pairs++;
            var ai = 0; var bi = 0;
            var quarantined = false;
            var pairMinimum = new PairMinimum();
            string? previousFrame = null;
            var previousEnd = double.NaN;
            var previousEligibleOverlap = false;
            while (ai < groups[a].Length && bi < groups[b].Length)
            {
                var pa = groups[a][ai]; var pb = groups[b][bi];
                var start = Math.Max(pa.StartTimeSeconds, pb.StartTimeSeconds);
                var end = Math.Min(pa.EndTimeSeconds, pb.EndTimeSeconds);
                if (start < end)
                {
                    if (pa.FrameId == pb.FrameId && ((pa.SupportsLapWrap && pb.SupportsLapWrap)
                        || pa.Source?.LapIndex == pb.Source?.LapIndex))
                    {
                        var contiguous = previousFrame == pa.FrameId && previousEnd == start;
                        if (!contiguous) { quarantined = false; previousEligibleOverlap = false; }
                        var jump = (pa.StartsAtDiscontinuity && start == pa.StartTimeSeconds)
                            || (pb.StartsAtDiscontinuity && start == pb.StartTimeSeconds);
                        if (quarantined || jump)
                        {
                            var startGap = MechanicalSeparation.Between(pa.SampleFootprint(start), pb.SampleFootprint(start)).SignedMeters;
                            if (startGap > GeometryNumerics.ContactDistanceMeters) quarantined = false;
                            else if (jump && !previousEligibleOverlap) quarantined = true;
                        }
                        var continuousStart = start;
                        if (quarantined)
                        {
                            var escape = FindClear(pa, pb, start, end, work);
                            var ambiguous = Evaluate(pa, pb, start, escape.Time ?? end, work, pairMinimum, attribution);
                            if (ambiguous.HasConflict) work.Conflicts--;
                            if (!escape.Resolved && ambiguous.NumericallyResolved) work.Unresolved++;
                            Add(ambiguous with { Kind = SpaceConflictKind.BoundaryAmbiguous,
                                FirstTouchCommonTimeSeconds = null, NumericallyResolved = ambiguous.NumericallyResolved && escape.Resolved });
                            continuousStart = escape.Time ?? end;
                            quarantined = !escape.Time.HasValue;
                        }
                        SpaceIntervalValue? row = null;
                        if (continuousStart < end)
                        { row = Evaluate(pa, pb, continuousStart, end, work, pairMinimum, attribution); Add(row.Value); }
                        previousEligibleOverlap = !quarantined && row?.EligibleForFutureInteraction == true
                            && MechanicalSeparation.Between(pa.SampleFootprint(end), pb.SampleFootprint(end)).SignedMeters <= GeometryNumerics.ContactDistanceMeters;
                        previousFrame = pa.FrameId; previousEnd = end;
                    }
                    else
                    {
                        var gap = new FrameCoverageGap(pa.RiderId, pb.RiderId, start, end, pa.FrameId, pb.FrameId,
                            pa.FrameId == pb.FrameId ? "NonClosingLapWrap" : "UnrelatedMetricFrames");
                        if (summary is null) gaps.Add(gap); else summary.Gap(gap);
                        previousFrame = null; quarantined = false; previousEligibleOverlap = false;
                    }
                }
                if (pa.EndTimeSeconds <= pb.EndTimeSeconds) ai++;
                if (pb.EndTimeSeconds <= pa.EndTimeSeconds) bi++;
            }
        }
        return new(rows.AsReadOnly(), work.Freeze(), gaps.AsReadOnly());
    }

    private static SpaceIntervalValue Evaluate(PhysicalPoseInterval a, PhysicalPoseInterval b, double start, double end,
        Work work, PairMinimum pairMinimum, bool attribution)
        => new IntervalSearch(a, b, start, end, work, pairMinimum).Evaluate(attribution);

    // Root-search state lives on the stack. No change to #55 bounds, roots, order or tolerances.
    private struct IntervalSearch
    {
        private readonly PhysicalPoseInterval a, b;
        private readonly double start, end, lipschitz;
        private readonly Work work;
        private readonly PairMinimum pairMinimum;
        private readonly PoseRateBounds boundsA, boundsB;
        private readonly BikePoseValue sa, sb, ea, eb;
        private readonly bool broadClear;
        private int count;
        private bool resolved;
        private double? first;
        private double minimum, minimumTime, uncertainFirst;
        private FootprintSeparation minimumPair;
        internal IntervalSearch(PhysicalPoseInterval a, PhysicalPoseInterval b, double start, double end, Work work, PairMinimum pairMinimum)
        {
            this.a = a; this.b = b; this.start = start; this.end = end; this.work = work; this.pairMinimum = pairMinimum;
            count = 0; resolved = true; first = null; minimum = double.PositiveInfinity; minimumTime = start;
            uncertainFirst = double.PositiveInfinity; minimumPair = default; work.Intervals++;
            boundsA = a.RateBounds(start, end); boundsB = b.RateBounds(start, end);
            Validate(boundsA); Validate(boundsB);
            var relativeRate = (boundsA.CenterVelocityMetersPerSecond - boundsB.CenterVelocityMetersPerSecond).Length
                + (boundsA.CenterAccelerationBoundMetersPerSecondSquared + boundsB.CenterAccelerationBoundMetersPerSecondSquared) * (end - start) / 2;
            lipschitz = relativeRate + a.Dimensions.BoundingRadiusMeters * boundsA.AngularSpeedBoundRadiansPerSecond
                + b.Dimensions.BoundingRadiusMeters * boundsB.AngularSpeedBoundRadiansPerSecond;
            sa = a.SampleValue(start); sb = b.SampleValue(start); ea = a.SampleValue(end); eb = b.SampleValue(end);
            var sphereLower = Math.Min((sa.Position - sb.Position).Length, (ea.Position - eb.Position).Length)
                - relativeRate * (end - start) / 2 - a.Dimensions.BoundingRadiusMeters - b.Dimensions.BoundingRadiusMeters;
            broadClear = sphereLower > GeometryNumerics.BroadPhasePaddingMeters;
            if (broadClear) work.Broad++;
        }
        private static void Validate(PoseRateBounds bounds)
        {
            GeometryValidation.Nonnegative(bounds.CenterAccelerationBoundMetersPerSecondSquared, nameof(bounds.CenterAccelerationBoundMetersPerSecondSquared));
            GeometryValidation.Nonnegative(bounds.AngularSpeedBoundRadiansPerSecond, nameof(bounds.AngularSpeedBoundRadiansPerSecond));
        }
        private double Gap(double time)
        {
            count++; work.Narrow++;
            var separation = MechanicalSeparation.Between(time == start ? sa.Footprint : time == end ? ea.Footprint : a.SampleFootprint(time),
                time == start ? sb.Footprint : time == end ? eb.Footprint : b.SampleFootprint(time));
            if (separation.SignedMeters < minimum)
            { minimum = separation.SignedMeters; minimumTime = time; minimumPair = separation; }
            pairMinimum.Value = Math.Min(pairMinimum.Value, separation.SignedMeters);
            if (!broadClear && separation.SignedMeters <= GeometryNumerics.ContactDistanceMeters
                && (!first.HasValue || time < first)) first = time;
            return separation.SignedMeters;
        }
        private void Search(double lo, double hi, double gl, double gh, int depth)
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
        internal SpaceIntervalValue Evaluate(bool attribution)
        {
            var gStart = Gap(start); var gEnd = Gap(end);
            Search(start, end, gStart, gEnd, 0);
            if (uncertainFirst < (first ?? double.PositiveInfinity) - 2 * GeometryNumerics.TimeToleranceSeconds) resolved = false;
            if (first.HasValue) work.Conflicts++;
            if (!resolved) work.Unresolved++;
            var onsetEnd = first ?? end;
            // Use the whole onset knot interval, rather than tiny root brackets whose effects vanish.
            var contributions = attribution ? Closing(sa, sb, a.SampleValue(onsetEnd), b.SampleValue(onsetEnd)) : EmptyContributions;
            var kind = first.HasValue && attribution ? Classify(sa, sb, ea, eb, contributions, boundsA, boundsB) : SpaceConflictKind.None;
            return new(a.RiderId, b.RiderId, a.FrameId, start, end, first, minimum, minimumTime,
                minimumPair.ComponentA, minimumPair.ComponentB, kind, contributions, sb.Position - sa.Position,
                boundsB.CenterVelocityMetersPerSecond - boundsA.CenterVelocityMetersPerSecond, resolved,
                Math.Min(gStart, gEnd) - lipschitz * (end - start) / 2, a.Source, b.Source);
        }
    }

    // Find the end of unsupported jump overlap even when clearance and re-contact occur
    // inside one supplied span. A Lipschitz upper bound prunes continuously overlapping regions.
    private static (double? Time, bool Resolved) FindClear(PhysicalPoseInterval a, PhysicalPoseInterval b,
        double start, double end, Work work)
    {
        var ra = a.RateBounds(start, end); var rb = b.RateBounds(start, end);
        var rate = (ra.CenterVelocityMetersPerSecond - rb.CenterVelocityMetersPerSecond).Length
            + (ra.CenterAccelerationBoundMetersPerSecondSquared + rb.CenterAccelerationBoundMetersPerSecondSquared) * (end - start) / 2
            + a.Dimensions.BoundingRadiusMeters * ra.AngularSpeedBoundRadiansPerSecond
            + b.Dimensions.BoundingRadiusMeters * rb.AngularSpeedBoundRadiansPerSecond;
        var count = 0; var resolved = true;
        // Restart outside the root-time uncertainty band; contact tolerance itself is unchanged.
        var clearThreshold = GeometryNumerics.ContactDistanceMeters + 2 * rate * GeometryNumerics.TimeToleranceSeconds;
        double Gap(double time)
        { count++; work.Narrow++; return MechanicalSeparation.Between(a.SampleFootprint(time), b.SampleFootprint(time)).SignedMeters; }
        double? Search(double lo, double hi, double gl, double gh, int depth)
        {
            if (gl > clearThreshold) return lo;
            if (Math.Max(gl, gh) + rate * (hi - lo) / 2 <= clearThreshold) return null;
            if (hi - lo <= GeometryNumerics.TimeToleranceSeconds)
            {
                if (gh > clearThreshold) return hi;
                return null;
            }
            if (depth >= GeometryNumerics.MaximumSubdivisionDepth || count >= GeometryNumerics.MaximumEvaluationsPerInterval)
            { resolved = false; return null; }
            var mid = (lo + hi) / 2; var gm = Gap(mid); work.Subdivisions++;
            var first = Search(lo, mid, gl, gm, depth + 1);
            if (first.HasValue || !resolved) return first;
            return Search(mid, hi, gm, gh, depth + 1);
        }
        var time = Search(start, end, Gap(start), Gap(end), 0);
        return (time, resolved);
    }

    private static ClosingContributions Closing(BikePoseValue a0, BikePoseValue b0, BikePoseValue a1, BikePoseValue b1)
    {
        var da = a1.Position - a0.Position; var db = b1.Position - b0.Position;
        // Remove only shared forward transport for nearly parallel travel. This prevents
        // two bikes moving at 20 m/s together from receiving fictitious opposing closure.
        var referenceHeading = BikeAngles.Interpolate(a0.ReferenceTangentHeadingRadians, b0.ReferenceTangentHeadingRadians, .5);
        var forward = new MeterPoint(Math.Cos(referenceHeading), Math.Sin(referenceHeading));
        var common = Math.Abs(BikeAngles.Wrap(a0.ReferenceTangentHeadingRadians - b0.ReferenceTangentHeadingRadians)) < ParallelClassificationAngleRadians
            ? forward * Math.Max(0, Math.Min(MeterPoint.Dot(da, forward), MeterPoint.Dot(db, forward))) : new MeterPoint(0, 0);
        var af = a0.Footprint; var bf = b0.Footprint;
        var at = BikeFootprint.Create(a0.Position + da - common, a0.BikeHeadingRadians, a0.Dimensions);
        var bt = BikeFootprint.Create(b0.Position + db - common, b0.BikeHeadingRadians, b0.Dimensions);
        var ar = BikeFootprint.Create(a0.Position, a1.BikeHeadingRadians, a0.Dimensions);
        var br = BikeFootprint.Create(b0.Position, b1.BikeHeadingRadians, b0.Dimensions);
        var baseline = MechanicalSeparation.Between(af, bf).SignedMeters;
        var cat = baseline - MechanicalSeparation.Between(at, bf).SignedMeters;
        var car = baseline - MechanicalSeparation.Between(ar, bf).SignedMeters;
        var cbt = baseline - MechanicalSeparation.Between(af, bt).SignedMeters;
        var cbr = baseline - MechanicalSeparation.Between(af, br).SignedMeters;
        var actual = baseline - MechanicalSeparation.Between(a1.Footprint, b1.Footprint).SignedMeters;
        return new(cat, car, cbt, cbr, actual, actual - cat - car - cbt - cbr);
    }
    private static SpaceConflictKind Classify(BikePoseValue a, BikePoseValue b, BikePoseValue ea, BikePoseValue eb,
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
        var heading = new MeterPoint(Math.Cos(a.TravelHeadingRadians), Math.Sin(a.TravelHeadingRadians));
        var relative = b.Position - a.Position;
        var lateral = Math.Abs(MeterPoint.Cross(heading, relative));
        var parallel = Math.Abs(BikeAngles.Wrap(a.TravelHeadingRadians - b.TravelHeadingRadians)) < ParallelClassificationAngleRadians;
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
    public TrackMetricEmbedding? Embedding { get; private set; }
    public PhysicalSpaceObserver(SpeedwayBikeDimensions? dimensions = null, ReferenceBikeAttitude? attitude = null)
    { _dimensions = dimensions ?? SpeedwayBikeDimensions.Reference; _attitude = attitude ?? ReferenceBikeAttitude.Neutral; }
    public void OnStepResolved(ResolvedSimulationStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        Embedding ??= new TrackMetricEmbedding(step.Snapshot.Track);
        Embedding.ValidateCompatible(step.Snapshot.Track);
        foreach (var motion in step.Motions) _intervals.AddRange(ResolvedBikePoses.FromMotion(motion, step.Snapshot.Track, _dimensions, _attitude, Embedding));
    }
    public IReadOnlyList<PhysicalPoseInterval> CapturedIntervals => _intervals.AsReadOnly();
    public ContestedSpaceReport Complete() => ContestedSpaceResolver.Observe(_intervals);
}
