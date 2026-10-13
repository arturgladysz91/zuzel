using CoreSim.Analysis;
using CoreSim.PhysicalSpace;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class FirstContactCertificationTests
{
    private static PhysicalPoseInterval Fixed(int id, double x) => PhysicalSpaceEvidence.Linear(id, new(x, 0), new(x, 0));

    [Fact]
    public void HiddenEarlierContactInsideClearEndpointsIsFound()
    {
        var input = new[] { Fixed(1, 0), new Curve(2, 2.05, .3, 4) };
        Assert.All(input, p =>
        {
            var other = input.Single(q => q.RiderId != p.RiderId);
            Assert.True(MechanicalSeparation.Between(p.Sample(0), other.Sample(0)).SignedMeters > GeometryNumerics.ContactDistanceMeters);
            Assert.True(MechanicalSeparation.Between(p.Sample(1), other.Sample(1)).SignedMeters > GeometryNumerics.ContactDistanceMeters);
        });
        var row = Assert.Single(ContestedSpaceResolver.ObserveFirstContact(input).Intervals);
        Assert.True(row.NumericallyResolved); Assert.InRange(row.FirstTouchCommonTimeSeconds!.Value, .18, .20);
        Assert.True(row.MinimumSeparationMeters < -.049);
    }

    [Fact]
    public void VeryCloseMissRequiresProvedClearance()
    {
        var row = Assert.Single(ContestedSpaceResolver.ObserveFirstContact(new[] { Fixed(1, 0), Fixed(2, 2.10000101) }).Intervals);
        Assert.True(row.NumericallyResolved); Assert.False(row.HasConflict);
        Assert.True(row.MinimumSeparationLowerBoundMeters > GeometryNumerics.ContactDistanceMeters);
    }

    [Fact]
    public void TangencyWithUncertainFirstTimeIsNeverDeclaredClear()
    {
        var diagnostics = new List<SpaceSearchDiagnostic>();
        ContestedSpaceReport report;
        try
        {
            SpaceSearchDiagnostics.Observer = diagnostics.Add;
            report = ContestedSpaceResolver.ObserveFirstContact(new[] { Fixed(1, 0), new Curve(2, 2.100001, .3141592653589793, .01) });
        }
        finally { SpaceSearchDiagnostics.Observer = null; }
        Assert.False(Assert.Single(report.Intervals).NumericallyResolved);
        Assert.Contains(diagnostics, d => d.Refusal != SpaceSearchRefusal.None);
        Assert.True(report.Work.NarrowPhaseEvaluations <= GeometryNumerics.MaximumEvaluationsPerInterval);
    }

    [Fact]
    public void PenetrationRemainsVisibleAfterRefinement()
    {
        var row = Assert.Single(ContestedSpaceResolver.ObserveFirstContact(new[] { Fixed(1, 0), Fixed(2, 2.099) }).Intervals);
        Assert.True(row.NumericallyResolved); Assert.True(row.HasConflict);
        Assert.True(row.MinimumSeparationMeters < -GeometryNumerics.ContactDistanceMeters);
        Assert.True(row.MinimumSeparationLowerBoundMeters < -GeometryNumerics.MinimumSeparationToleranceMeters);
    }

    [Fact]
    public void GeometricSplitsPreserveSamplerFirstContactAndReversedOrder()
    {
        var input = new[] { Fixed(1, 0), PhysicalSpaceEvidence.Linear(2, new(2.6, 0), new(1.6, 0)) };
        var split = input.SelectMany(p => new[] { new Clip(p, 0, .5), new Clip(p, .5, 1) }).ToArray();
        foreach (var p in split)
            Assert.Equal(input.Single(q => q.RiderId == p.RiderId).Sample((p.StartTimeSeconds + p.EndTimeSeconds) / 2),
                p.Sample((p.StartTimeSeconds + p.EndTimeSeconds) / 2));
        var whole = ContestedSpaceResolver.ObserveFirstContact(input);
        var divided = ContestedSpaceResolver.ObserveFirstContact(split);
        Assert.All(divided.Intervals, r => Assert.True(r.NumericallyResolved));
        Assert.InRange(Math.Abs(whole.Intervals.Single().FirstTouchCommonTimeSeconds!.Value
            - divided.Intervals.Where(r => r.HasConflict).Min(r => r.FirstTouchCommonTimeSeconds)!.Value), 0, GeometryNumerics.TimeToleranceSeconds);
        Assert.Equal(divided.Intervals, ContestedSpaceResolver.ObserveFirstContact(split.Reverse()).Intervals);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SplitsCannotMaskDiscontinuityOrFrameChange(bool changeFrame)
    {
        var a = Fixed(1, 0);
        var b = PhysicalSpaceEvidence.Linear(2, new(.2, 0), new(.2, 0), frame: changeFrame ? "other-frame" : a.FrameId, discontinuity: !changeFrame);
        var split = new PhysicalPoseInterval[] { new Clip(a, 0, .5), new Clip(a, .5, 1), new Clip(b, 0, .5), new Clip(b, .5, 1) };
        var report = ContestedSpaceResolver.ObserveFirstContact(split);
        Assert.DoesNotContain(report.Intervals, r => r.EligibleForFutureInteraction);
        if (changeFrame) Assert.Equal(2, report.FrameCoverageGaps.Count);
        else Assert.All(report.Intervals, r => Assert.True(r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)));
    }

    [Fact]
    public void ExactCertificateCacheSeparatesRefinementAndChangedGeometry()
    {
        var input = PhysicalSpaceEvidence.Controlled("I");
        var cache = new ContestedSpaceResolver.CertificationCache(refineFirstContact: true);
        var first = ContestedSpaceResolver.Observe(input, cache);
        var repeated = ContestedSpaceResolver.Observe(input.Reverse(), cache);
        Assert.Equal(first.Intervals, repeated.Intervals); Assert.True(cache.ReusedIntervals > 0);
        Assert.True(repeated.Work.NarrowPhaseEvaluations < first.Work.NarrowPhaseEvaluations);
        var priorReused = cache.ReusedIntervals;
        var changed = new[] { Fixed(1, 0), Fixed(2, 3) };
        Assert.False(Assert.Single(ContestedSpaceResolver.Observe(changed, cache).Intervals).HasConflict);
        Assert.Equal(priorReused, cache.ReusedIntervals);
    }

    private sealed class Curve(int id, double minimumX, double peakTime, double curvature)
        : PhysicalPoseInterval(id, "controlled-metres", 0, 1, SpeedwayBikeDimensions.Reference, false)
    {
        public override PhysicalBikePose Sample(double time)
        {
            Fraction(time);
            return new(RiderId, FrameId, new(minimumX + curvature * (time - peakTime) * (time - peakTime), 0),
                new(time, 0, 0), Dimensions);
        }
        public override PoseRateBounds RateBounds(double start, double end)
        { Fraction(start); Fraction(end); return new(new(2 * curvature * ((start + end) / 2 - peakTime), 0), 2 * curvature, 0); }
    }

    private sealed class Clip(PhysicalPoseInterval source, double start, double end)
        : PhysicalPoseInterval(source.RiderId, source.FrameId, start, end, source.Dimensions,
            source.StartsAtDiscontinuity && start == source.StartTimeSeconds, source.Source, source.SupportsLapWrap)
    {
        public override PhysicalBikePose Sample(double time) { Fraction(time); return source.Sample(time); }
        public override PoseRateBounds RateBounds(double start, double end) { Fraction(start); Fraction(end); return source.RateBounds(start, end); }
    }
}
