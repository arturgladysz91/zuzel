using CoreSim.Analysis;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed partial class PhysicalSpaceTests
{
    [Theory]
    [InlineData(0, 1)] [InlineData(4, 5)] // U: straight to turn
    [InlineData(3, 4)] [InlineData(7, 8)] // V: turn to straight
    [InlineData(1, 2)] [InlineData(2, 3)] [InlineData(5, 6)] [InlineData(6, 7)] // W
    public void U_V_W_EqualPhysicalOffsetHasAnalyticBoundaryPositionAndTangentContinuity(int from, int to)
    {
        var embedding = new TrackMetricEmbedding(MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        foreach (var offset in new[] { 0d, .75, 5, 9.123 })
        {
            var pre = embedding.Segments[from].Map(1, offset, .2);
            var post = embedding.Segments[to].Map(0, offset, .2);
            Assert.InRange((pre.Position - post.Position).Length, 0, 1e-12);
            Assert.InRange(Math.Abs(BikeAngles.Wrap(pre.TangentHeadingRadians - post.TangentHeadingRadians)), 0, 1e-12);
            Assert.InRange(Math.Abs(BikeAngles.Wrap(pre.TravelHeadingRadians - post.TravelHeadingRadians)), 0, 1e-12);
        }
    }

    [Fact]
    public void AD_AF_MotoarenaClosesWithoutSnappingOrChangingItsSplit()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var embedding = new TrackMetricEmbedding(track);
        Assert.Equal(35, embedding.Segments[0].StraightLengthMeters);
        Assert.Equal(27, embedding.Segments[8].StraightLengthMeters);
        Assert.Equal(62, embedding.Segments[0].StraightLengthMeters + embedding.Segments[8].StraightLengthMeters);
        Assert.True(embedding.Closure.SupportsLapWrap);
        Assert.InRange(embedding.Closure.PositionErrorMeters, 1e-8, TrackClosure.PositionToleranceMeters);
        Assert.InRange(Math.Abs(embedding.Closure.HeadingResidualRadians), 1e-9, TrackClosure.HeadingToleranceRadians);
        Assert.Equal(track.Geometry.TurnSegmentAngleRadians, embedding.Segments[1].TurnAngleRadians);
        foreach (var offset in new[] { 0d, .75, 5, 9.123 })
        {
            var pre = embedding.Segments[8].Map(1, offset);
            var post = embedding.Segments[0].Map(0, offset);
            Assert.InRange((pre.Position - post.Position).Length, 0, TrackClosure.PositionToleranceMeters);
            Assert.InRange(Math.Abs(BikeAngles.Wrap(pre.TangentHeadingRadians - post.TangentHeadingRadians)), 0, TrackClosure.HeadingToleranceRadians);
        }
    }

    [Theory]
    [InlineData(0, 1, 0)] [InlineData(3, 4, 0)] [InlineData(4, 5, 0)] [InlineData(7, 8, 0)]
    [InlineData(8, 0, 1)]
    public void Y_AE_ProductionPairsStraddlingEachBoundaryHaveClearComparableGeometry(int from, int to, int nextLap)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var a = MetricMotion(track, 1, 0, from, .97f, 0, 18);
        var b = MetricMotion(track, 2, nextLap, to, .03f, 8, 18);
        Assert.Equal(a[0].FrameId, b[0].FrameId);
        Assert.Equal(nextLap, b[0].Sample(0).Source!.LapIndex);
        var report = ContestedSpaceResolver.Observe(a.Concat(b));
        Assert.NotEmpty(report.Intervals); Assert.Empty(report.FrameCoverageGaps);
        Assert.All(report.Intervals, i => { Assert.True(i.MinimumSeparationMeters > 0); Assert.False(i.HasConflict); });
    }

    [Theory]
    [InlineData(0, 1, 0)] [InlineData(8, 0, 1)]
    public void Z_AE_FirstContinuousContactWhileStraddlingBoundaryIsEligible(int from, int to, int nextLap)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var a = MetricMotion(track, 1, 0, from, .9f, 5, 20);
        var b = MetricMotion(track, 2, nextLap, to, .01f, 5, 3);
        var report = ContestedSpaceResolver.Observe(a.Concat(b));
        Assert.Empty(report.FrameCoverageGaps);
        var first = report.Intervals.First(i => i.EligibleForFutureInteraction);
        Assert.True(first.NumericallyResolved);
        Assert.InRange(first.FirstTouchCommonTimeSeconds!.Value, 1e-5, a[^1].EndTimeSeconds - 1e-5);
        Assert.Equal(from, first.SourceA!.SegmentIndex); Assert.Equal(to, first.SourceB!.SegmentIndex);
        Assert.True(first.Kind.HasFlag(SpaceConflictKind.AEncroachesByTranslation));
        Assert.True(first.Contributions.ATranslationMeters > 0);
        Assert.True(first.RelativePositionAtOnsetMeters.Length > 0);
        Assert.True(first.RelativeVelocityAtOnsetMetersPerSecond.Length > 0);
        Assert.Equal(0, report.Work.UnresolvedIntervals);
    }

    [Fact]
    public void AA_WidthReinterpretationOnlyOverlapHasNoPhysicalRootOrJumpContribution()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var embedding = new TrackMetricEmbedding(track);
        var before = embedding.Segments[0].Map(1, LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(2, SegmentType.Straight, track.Geometry));
        var after = embedding.Segments[1].Map(0, LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(2, SegmentType.TurnEntry, track.Geometry));
        var other = after.Position + new MeterPoint(0, -.2);
        var a = PhysicalSpaceEvidence.Linear(1, before.Position, before.Position);
        var b = PhysicalSpaceEvidence.Linear(2, other, other);
        var jumped = PhysicalSpaceEvidence.Linear(1, after.Position, after.Position, startTime: 1, endTime: 2, discontinuity: true);
        var held = PhysicalSpaceEvidence.Linear(2, other, other, startTime: 1, endTime: 2);
        Assert.True(MechanicalSeparation.Between(a.Sample(1), b.Sample(1)).SignedMeters > 0);
        var report = ContestedSpaceResolver.Observe(new[] { a, b, jumped, held });
        var row = Assert.Single(report.Intervals.Where(i => i.Kind == SpaceConflictKind.BoundaryAmbiguous));
        Assert.True(row.MinimumSeparationMeters < 0); Assert.Null(row.FirstTouchCommonTimeSeconds);
        Assert.False(row.EligibleForFutureInteraction); Assert.Equal(0, row.Contributions.ATranslationMeters);
        Assert.Equal(0, row.Contributions.ActualClosingMeters); Assert.Empty(report.FrameCoverageGaps);
    }

    [Fact]
    public void AB_ClearanceEndsQuarantineAndLaterContinuousContactIsEligible()
    {
        var a = PhysicalSpaceEvidence.Linear(1, new(0, 0), new(0, 0), endTime: 4);
        var pre = PhysicalSpaceEvidence.Linear(2, new(0, 3), new(0, 3));
        var jump = PhysicalSpaceEvidence.Linear(2, new(0, .2), new(0, .2), startTime: 1, endTime: 2, discontinuity: true);
        var clear = PhysicalSpaceEvidence.Linear(2, new(0, .2), new(0, 3), startTime: 2, endTime: 3);
        var returnMotion = PhysicalSpaceEvidence.Linear(2, new(0, 3), new(0, .2), startTime: 3, endTime: 4);
        var report = ContestedSpaceResolver.Observe(new[] { a, pre, jump, clear, returnMotion });
        Assert.Contains(report.Intervals, i => i.Kind == SpaceConflictKind.BoundaryAmbiguous);
        var first = report.Intervals.First(i => i.EligibleForFutureInteraction);
        Assert.InRange(first.FirstTouchCommonTimeSeconds!.Value, 3, 4);
        Assert.Equal(0, report.Work.UnresolvedIntervals);
    }

    [Fact]
    public void AC_GenuinePreBoundaryContactRemainsEligibleInHistory()
    {
        var a = PhysicalSpaceEvidence.Linear(1, new(0, 0), new(0, 0), endTime: 2);
        var pre = PhysicalSpaceEvidence.Linear(2, new(0, 3), new(0, .2));
        var jump = PhysicalSpaceEvidence.Linear(2, new(0, .1), new(0, .1), startTime: 1, endTime: 2, discontinuity: true);
        var report = ContestedSpaceResolver.Observe(new[] { a, pre, jump });
        var first = report.Intervals.First(i => i.EligibleForFutureInteraction);
        Assert.InRange(first.FirstTouchCommonTimeSeconds!.Value, 0, 1);
        Assert.DoesNotContain(report.Intervals, i => i.Kind == SpaceConflictKind.BoundaryAmbiguous);
        Assert.True(report.Intervals.Last().EligibleForFutureInteraction);
    }

    [Fact]
    public void AB_ClearanceAndRecontactInsideOneContinuousIntervalEndQuarantine()
    {
        var a = PhysicalSpaceEvidence.Linear(1, new(0, 0), new(0, 0), endTime: 3);
        var pre = PhysicalSpaceEvidence.Linear(2, new(0, 3), new(0, 3));
        var report = ContestedSpaceResolver.Observe(new PhysicalPoseInterval[] { a, pre, new ClearAndReturn() });
        Assert.Contains(report.Intervals, i => i.Kind == SpaceConflictKind.BoundaryAmbiguous);
        var first = report.Intervals.First(i => i.EligibleForFutureInteraction);
        Assert.InRange(first.FirstTouchCommonTimeSeconds!.Value, 2, 3);
        Assert.Equal(0, report.Work.UnresolvedIntervals);
    }

    [Fact]
    public void AG_GlobalRigidTransformPreservesSeparationContactAndContributions()
    {
        const double angle = .713;
        MeterPoint Transform(MeterPoint p) => new(123 + p.X * Math.Cos(angle) - p.Y * Math.Sin(angle),
            -87 + p.X * Math.Sin(angle) + p.Y * Math.Cos(angle));
        PhysicalBikePose TransformPose(PhysicalBikePose p) => new(p.RiderId, "transformed", Transform(p.Position),
            new(p.CommonTimeSeconds, p.Attitude.TravelHeadingRadians + angle, p.Attitude.BikeHeadingRadians + angle),
            p.Dimensions, p.ReferenceTangentHeadingRadians + angle, p.Source);
        foreach (var scenario in new[] { "D", "G", "H", "I", "J" })
        {
            var input = PhysicalSpaceEvidence.Controlled(scenario);
            var rotated = input.Select(i => new LinearBikePoseInterval(TransformPose(i.Sample(0)), TransformPose(i.Sample(1))));
            var a = Assert.Single(ContestedSpaceResolver.Observe(input).Intervals);
            var b = Assert.Single(ContestedSpaceResolver.Observe(rotated).Intervals);
            Assert.Equal(a.Kind, b.Kind);
            Assert.InRange(Math.Abs(a.MinimumSeparationMeters - b.MinimumSeparationMeters), 0, 1e-12);
            Assert.InRange(Math.Abs(a.FirstTouchCommonTimeSeconds!.Value - b.FirstTouchCommonTimeSeconds!.Value), 0, 2e-7);
            Assert.InRange(Math.Abs(a.Contributions.ATranslationMeters - b.Contributions.ATranslationMeters), 0, 1e-12);
            Assert.InRange(Math.Abs(a.Contributions.ARotationMeters - b.Contributions.ARotationMeters), 0, 1e-12);
        }
    }

    [Fact]
    public void AH_AI_AJ_FourRiderHeatHasZeroMetricGapsAndOrderInvariantEvidence()
    {
        var capture = PhysicalSpaceEvidence.FourRiderHeat().Observer;
        Assert.Single(capture.CapturedIntervals.Select(i => i.FrameId).Distinct());
        Assert.Equal(new[] { 0, 1, 2, 3 }, capture.CapturedIntervals.Select(i => i.Source!.LapIndex).Distinct().Order());
        var a = capture.Complete(); var b = ContestedSpaceResolver.Observe(capture.CapturedIntervals.Reverse());
        Assert.Empty(a.FrameCoverageGaps); Assert.Equal(a.Intervals, b.Intervals); Assert.Equal(a.Work, b.Work);
        Assert.Equal(0, a.Work.UnresolvedIntervals);
        var bend = ContestedSpaceResolver.Observe(capture.CapturedIntervals.Where(PhysicalSpaceEvidence.IsFirstBend));
        Assert.Empty(bend.FrameCoverageGaps); Assert.Equal(6, bend.Work.RiderPairs);
        Assert.Contains(bend.Intervals, i => i.SourceA!.SegmentType == SegmentType.Straight && i.SourceB!.CornerId == 1
            || i.SourceB!.SegmentType == SegmentType.Straight && i.SourceA!.CornerId == 1);
    }

    [Fact]
    public void NonClosingTrackExposesResidualAndRejectsOnlyUnsupportedLapComparisons()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight, 35), new TrackSegment(1, SegmentType.TurnEntry) });
        var embedding = new TrackMetricEmbedding(track);
        Assert.False(embedding.Closure.SupportsLapWrap); Assert.True(embedding.Closure.PositionErrorMeters > 1);
        Assert.Equal(embedding.Segments[^1].Map(1, 0).Position, embedding.Closure.PositionResidualMeters);
        var a = MetricMotion(track, 1, 0, 0, .9f, 0, 18);
        var b = MetricMotion(track, 2, 0, 1, .01f, 1, 18);
        Assert.Empty(ContestedSpaceResolver.Observe(a.Concat(b)).FrameCoverageGaps);
        var nextLap = MetricMotion(track, 2, 1, 0, .01f, 1, 18);
        var gap = ContestedSpaceResolver.Observe(a.Concat(nextLap));
        Assert.NotEmpty(gap.FrameCoverageGaps); Assert.All(gap.FrameCoverageGaps, i => Assert.Equal("NonClosingLapWrap", i.Reason));
    }

    [Fact]
    public void ObserverReusesEmbeddingAndValidatesExactCompatibleTopology()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var observer = new PhysicalSpaceObserver();
        var rider = new RiderState(1, 2) { Speed = 18 };
        observer.OnStepResolved(Resolve(track, new[] { rider }, 0, 2));
        var embedding = observer.Embedding;
        observer.OnStepResolved(Resolve(new Track(track.Segments, track.Geometry), new[] { rider }, 0, 2));
        Assert.Same(embedding, observer.Embedding);
        Assert.Throws<ArgumentException>(() => embedding!.ValidateCompatible(Track.CreateStandingStartExample()));
        var reordered = new Track(track.Segments.Reverse().Select(s => new TrackSegment(s.Id, s.Type, s.StraightLengthMetersOverride)).ToArray(), track.Geometry);
        Assert.Throws<ArgumentException>(() => embedding!.ValidateCompatible(reordered));
    }

    private static IReadOnlyList<PhysicalPoseInterval> MetricMotion(Track track, int id, int lap, int segment,
        float progress, float offset, float speed)
    {
        var lateral = LaneModel.LateralPositionFromPhysicalOffsetMeters(offset, track.Segments[segment].Type, track.Geometry);
        var rider = new RiderState(id, (int)MathF.Round(lateral)) { LateralPosition = lateral, Speed = speed };
        rider.RestorePosition(RiderPosition.Create(lap + 1, segment, progress, track.Segments.Count));
        return ResolvedBikePoses.FromMotion(Resolve(track, new[] { rider }, segment, rider.Lane).Motions[0], track);
    }
    private sealed class ClearAndReturn() : PhysicalPoseInterval(2, "controlled-metres", 1, 3, SpeedwayBikeDimensions.Reference, true)
    {
        public override PhysicalBikePose Sample(double time)
        {
            var phase = Fraction(time) * Math.PI;
            return new(RiderId, FrameId, new(0, .2 + 2.8 * Math.Sin(phase)), new(time, Math.PI / 2, 0), Dimensions, 0);
        }
        public override PoseRateBounds RateBounds(double start, double end)
        {
            var phase = Fraction((start + end) / 2) * Math.PI;
            return new(new(0, 1.4 * Math.PI * Math.Cos(phase)), .7 * Math.PI * Math.PI, 0);
        }
    }
}
