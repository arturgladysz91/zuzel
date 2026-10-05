using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed partial class PhysicalSpaceTests
{
    [Theory]
    [InlineData("A", false)] [InlineData("B", false)] [InlineData("C", true)]
    [InlineData("D", true)] [InlineData("E", true)] [InlineData("F", true)] [InlineData("G", true)]
    [InlineData("H", true)] [InlineData("I", true)] [InlineData("J", true)]
    [InlineData("K-zero", false)] [InlineData("K-yaw", true)]
    public void ControlledMechanicalCases(string scenario, bool conflict)
    {
        var r = ContestedSpaceResolver.Observe(PhysicalSpaceEvidence.Controlled(scenario));
        Assert.Equal(conflict, r.Intervals.Any(i => i.EligibleForFutureInteraction));
        Assert.Equal(0, r.Work.UnresolvedIntervals);
    }
    [Theory]
    [InlineData("D", SpaceConflictKind.RearClosing)]
    [InlineData("E", SpaceConflictKind.AEncroachesByTranslation)]
    [InlineData("F", SpaceConflictKind.BEncroachesByTranslation)]
    [InlineData("G", SpaceConflictKind.MutualConvergence)]
    [InlineData("H", SpaceConflictKind.CrossingPaths)]
    [InlineData("I", SpaceConflictKind.AEncroachesByRotation)]
    [InlineData("J", SpaceConflictKind.AEncroachesMixed)]
    public void ClosingCausesAreTyped(string scenario, SpaceConflictKind required)
    {
        var first = First(PhysicalSpaceEvidence.Controlled(scenario));
        Assert.True(first.Kind.HasFlag(required), first.Kind.ToString());
        if (scenario == "E") Assert.InRange(first.Contributions.BTranslationMeters, -1e-9, 1e-9);
        if (scenario == "I") Assert.InRange(first.Contributions.ATranslationMeters, -1e-9, 1e-9);
    }
    [Fact]
    public void RearClosingFirstTouchMatchesMechanicalLengthAndRelativeSpeed()
    {
        var first = First(PhysicalSpaceEvidence.Controlled("D"));
        Assert.InRange(first.FirstTouchCommonTimeSeconds!.Value, .29 - 1e-6, .29 + 1e-6);
        Assert.Equal(-.3, first.MinimumSeparationMeters, 6);
    }
    [Theory]
    [InlineData("H")] [InlineData("I")]
    public void ClearEndpointsCannotHideInteriorCrossingOrRotation(string scenario)
    {
        var poses = PhysicalSpaceEvidence.Controlled(scenario);
        Assert.True(MechanicalSeparation.Between(poses[0].Sample(0), poses[1].Sample(0)).SignedMeters > 0);
        Assert.True(MechanicalSeparation.Between(poses[0].Sample(1), poses[1].Sample(1)).SignedMeters > 0);
        var first = First(poses);
        Assert.InRange(first.FirstTouchCommonTimeSeconds!.Value, .0001, .9999);
        Assert.True(first.Contributions.ActualClosingMeters > 0);
    }
    [Fact]
    public void WholeCornerAttitudeHasContinuousNamedPhasesAndZeroStraightTargets()
    {
        var profile = ReferenceBikeAttitude.Neutral;
        Assert.Equal(0, profile.RelativeSlideAngle(0)); Assert.Equal(0, profile.RelativeSlideAngle(1));
        Assert.Equal(profile.PeakSlideAngleRadians, profile.RelativeSlideAngle(.5));
        foreach (var p in profile.ProgressKnots.Concat(new[] { 1d / 3, 2d / 3 }))
            Assert.InRange(Math.Abs(profile.RelativeSlideAngle(p - 1e-8) - profile.RelativeSlideAngle(p + 1e-8)), 0, 1e-6);
    }
    [Fact]
    public void L_EarlyAndLateStraighteningChangeAttitudeWithoutChangingCollisionCode()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = new RiderState(RiderProfile.CreateDefault(1), 2) { Speed = 18 };
        rider.RestorePosition(RiderPosition.Create(1, 2, 0, track.Segments.Count));
        var step = Resolve(track, new[] { rider }, 2, 2);
        var early = new ReferenceBikeAttitude(0, .5, .15, .25, .60);
        var late = new ReferenceBikeAttitude(.1, .5, .4, .7, 1);
        var a = ResolvedBikePoses.FromMotion(step.Motions[0], track, attitude: early);
        var b = ResolvedBikePoses.FromMotion(step.Motions[0], track, attitude: late);
        var time = (double)step.Motions[0].StartElapsedTimeSeconds + step.Motions[0].TotalTimeSeconds * .8;
        var pa = a.Single(i => time > i.StartTimeSeconds && time < i.EndTimeSeconds).Sample(time);
        var pb = b.Single(i => time > i.StartTimeSeconds && time < i.EndTimeSeconds).Sample(time);
        Assert.Equal(pa.Position, pb.Position); Assert.Equal(pa.Attitude.TravelHeadingRadians, pb.Attitude.TravelHeadingRadians);
        Assert.NotEqual(pa.Attitude.BikeHeadingRadians, pb.Attitude.BikeHeadingRadians);
        Assert.NotEqual(pa.Footprint, pb.Footprint);
    }
    [Fact]
    public void DiagonalTravelHeadingComesFromActualMetresNotTheRequestedLane()
    {
        var step = MotionFoundationDiagnostics.CrossingStep();
        var poses = ResolvedBikePoses.FromMotion(step.Motions[0], step.Snapshot.Track);
        var span = poses.First(p => (p.Sample(p.EndTimeSeconds).Position - p.Sample(p.StartTimeSeconds).Position).Y != 0);
        var sample = span.Sample((span.StartTimeSeconds + span.EndTimeSeconds) / 2);
        var delta = span.Sample(span.EndTimeSeconds).Position - span.Sample(span.StartTimeSeconds).Position;
        Assert.Equal(Math.Atan2(delta.Y, delta.X), sample.Attitude.TravelHeadingRadians, 10);
        Assert.NotEqual(0, sample.Attitude.TravelHeadingRadians);
        Assert.Equal(0, sample.Attitude.RelativeSlideAngleRadians, 10);
    }
    [Fact]
    public void PolarTravelUsesActualRadiusAndAngularProgress()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = new RiderState(RiderProfile.CreateDefault(1), 2) { Speed = 18 };
        rider.RestorePosition(RiderPosition.Create(1, 1, 0, track.Segments.Count));
        var step = Resolve(track, new[] { rider }, 1, 2);
        var spans = ResolvedBikePoses.FromMotion(step.Motions[0], track);
        var sampleTime = step.Motions[0].TotalTimeSeconds * .6;
        var span = spans.Single(i => i.StartTimeSeconds < sampleTime && sampleTime < i.EndTimeSeconds);
        var mid = (span.StartTimeSeconds + span.EndTimeSeconds) / 2; var pose = span.Sample(mid);
        var derivative = span.Sample(mid + 1e-5).Position - span.Sample(mid - 1e-5).Position;
        Assert.InRange(Math.Abs(BikeAngles.Wrap(Math.Atan2(derivative.Y, derivative.X) - pose.Attitude.TravelHeadingRadians)), 0, 1e-7);
        Assert.NotEqual(pose.Attitude.TravelHeadingRadians, pose.Attitude.BikeHeadingRadians);
    }
    [Fact]
    public void M_ProductionReactionKeepsAllFourGateCentresStationaryAndClear()
    {
        var heat = PhysicalSpaceEvidence.FourRiderHeat(1);
        var reaction = heat.Observer.CapturedIntervals.Where(i => i.StartTimeSeconds == 0).ToArray();
        Assert.Equal(4, reaction.Length);
        Assert.Equal(new[] { -.5, -3.5, -6.5, -9.5 }, reaction.Select(i => i.Sample(0).Position.Y));
        foreach (var interval in reaction)
        {
            var sample = interval.Sample(interval.EndTimeSeconds / 2);
            Assert.Equal(interval.Sample(0).Position, sample.Position);
            Assert.Equal(0, sample.Attitude.TravelHeadingRadians); Assert.Equal(0, sample.Attitude.BikeHeadingRadians);
        }
        var report = ContestedSpaceResolver.Observe(reaction);
        Assert.Equal(6, report.Work.RiderPairs); Assert.Equal(0, report.Work.DetectedConflicts);
    }
    [Fact]
    public void N_RealFourRiderLaunchAndFirstBendObserveSixPairsAndGenuineConflict()
    {
        var heat = PhysicalSpaceEvidence.FourRiderHeat(1);
        var r = ContestedSpaceResolver.Observe(heat.Observer.CapturedIntervals.Where(PhysicalSpaceEvidence.IsFirstBend));
        Assert.Contains(r.Intervals, i => i.SourceA?.CornerId == 1);
        Assert.Empty(r.FrameCoverageGaps);
        Assert.Equal(6, r.Work.RiderPairs); Assert.Equal(6, PhysicalSpaceEvidence.Summarize(r).Count);
        Assert.Contains(r.Intervals, i => i.EligibleForFutureInteraction && i.FirstTouchCommonTimeSeconds > .28);
        Assert.Equal(0, r.Work.UnresolvedIntervals);
    }
    [Fact]
    public void O_PhysicalMetresAreEquivalentDespiteDifferentNormalizedWidthCoordinates()
    {
        var geometry = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack().Geometry;
        double Gap(SegmentType type)
        {
            var normalized = LaneModel.LateralPositionFromPhysicalOffsetMeters(.75f, type, geometry);
            var offset = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(normalized, type, geometry);
            var a = PhysicalSpaceEvidence.Linear(1, new(0, 0), new(10, 0));
            var b = PhysicalSpaceEvidence.Linear(2, new(0, offset), new(10, offset));
            return MechanicalSeparation.Between(a.Sample(0), b.Sample(0)).SignedMeters;
        }
        Assert.Equal(Gap(SegmentType.Straight), Gap(SegmentType.TurnEntry), 6);
    }
    [Theory]
    [InlineData(0, 1)] [InlineData(3, 4)]
    public void P_Q_X_ProductionWidthBoundariesShareMetricFrameButRemainUnswept(int from, int to)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = new RiderState(RiderProfile.CreateDefault(1), 2) { Speed = 18 };
        rider.RestorePosition(RiderPosition.Create(1, from, 0, track.Segments.Count));
        var step = Resolve(track, new[] { rider }, from, 2);
        Assert.True(step.Motions[0].ExitBoundary!.HasPhysicalOffsetDiscontinuity);
        var engine = new SimulationEngine(new Targets(2));
        engine.Commit(step, new[] { rider }, TrackState.CreateDefault(track), new CoreSim.Logging.SimLog());
        var next = Resolve(track, new[] { rider }, to, 2);
        var before = ResolvedBikePoses.FromMotion(step.Motions[0], track);
        var after = ResolvedBikePoses.FromMotion(next.Motions[0], track);
        Assert.Equal(before[^1].FrameId, after[0].FrameId);
        Assert.NotEqual(before[^1].Source, after[0].Source);
        var boundary = step.Motions[0].ExitBoundary!;
        Assert.Equal(2f, boundary.LateralPosition);
        Assert.InRange(Math.Abs(boundary.FromPhysicalOffsetMeters - boundary.ToPhysicalOffsetMeters), 2.299999, 2.300001);
        var distance = step.Motions[0].TotalDistanceMeters;
        var duration = step.Motions[0].TotalTimeSeconds;
        Assert.InRange((before[^1].Sample(before[^1].EndTimeSeconds).Position - after[0].Sample(after[0].StartTimeSeconds).Position).Length,
            2.299999, 2.300001);
        Assert.Equal(distance, step.Motions[0].Final.TravelledMeters);
        Assert.InRange(Math.Abs(before.Sum(i => i.EndTimeSeconds - i.StartTimeSeconds) - duration), 0, 1e-12);
        Assert.True(after[0].StartsAtDiscontinuity);
        Assert.Equal(before[^1].EndTimeSeconds, after[0].StartTimeSeconds);
        Assert.DoesNotContain(before.Concat(after), i => i.StartTimeSeconds < after[0].StartTimeSeconds && i.EndTimeSeconds > after[0].StartTimeSeconds);
    }
    [Fact]
    public void BoundaryOnlyOverlapIsQuarantinedUntilClearIncludingSubsequentKnots()
    {
        var a = PhysicalSpaceEvidence.Linear(1, new(0, 0), new(1, 0), endTime: 1);
        var b = PhysicalSpaceEvidence.Linear(2, new(0, 3), new(1, 3), endTime: 1);
        var jumped = PhysicalSpaceEvidence.Linear(2, new(1, .2), new(2, .2), startTime: 1, endTime: 2, discontinuity: true);
        var held = PhysicalSpaceEvidence.Linear(1, new(1, 0), new(2, 0), startTime: 1, endTime: 2);
        var jumpedAgain = PhysicalSpaceEvidence.Linear(2, new(2, .2), new(3, .2), startTime: 2, endTime: 3);
        var heldAgain = PhysicalSpaceEvidence.Linear(1, new(2, 0), new(3, 0), startTime: 2, endTime: 3);
        var report = ContestedSpaceResolver.Observe(new[] { a, b, jumped, held, jumpedAgain, heldAgain });
        Assert.Equal(2, report.Intervals.Count(i => i.Kind == SpaceConflictKind.BoundaryAmbiguous));
        Assert.DoesNotContain(report.Intervals, i => i.EligibleForFutureInteraction);
    }
    [Fact]
    public void UnrelatedFramesReturnTypedCoverageGapsNotFalseClearResults()
    {
        var r = ContestedSpaceResolver.Observe(new[] { PhysicalSpaceEvidence.Linear(1, new(0, 0), new(1, 0), frame: "first"),
            PhysicalSpaceEvidence.Linear(2, new(0, 0), new(1, 0), frame: "second") });
        Assert.Empty(r.Intervals); Assert.Equal(SpaceConflictKind.BoundaryAmbiguous, Assert.Single(r.FrameCoverageGaps).Kind);
    }
    [Fact]
    public void CommonTimeOriginsAreIntersectedRatherThanEqualLocalTimes()
    {
        var a = PhysicalSpaceEvidence.Linear(1, new(0, 0), new(10, 0), startTime: 0, endTime: 1);
        var b = PhysicalSpaceEvidence.Linear(2, new(0, 0), new(10, 0), startTime: 10, endTime: 11);
        Assert.Empty(ContestedSpaceResolver.Observe(new[] { a, b }).Intervals);
        var c = PhysicalSpaceEvidence.Linear(2, new(0, 0), new(10, 0), startTime: .5, endTime: 1.5);
        var row = Assert.Single(ContestedSpaceResolver.Observe(new[] { a, c }).Intervals);
        Assert.Equal(.5, row.IntervalStartSeconds); Assert.Equal(1, row.IntervalEndSeconds);
        Assert.False(row.HasConflict); // five metres apart at the same heat time
    }
    [Fact]
    public void R_CollectionOrderAndRepeatedObservationsAreExactlyEqual()
    {
        var input = PhysicalSpaceEvidence.Controlled("G");
        var a = ContestedSpaceResolver.Observe(input); var b = ContestedSpaceResolver.Observe(input.Reverse());
        Assert.Equal(a.Intervals, b.Intervals); Assert.Equal(a.Work, b.Work);
        Assert.Equal(a.Intervals, ContestedSpaceResolver.Observe(input).Intervals);
    }
    [Fact]
    public void FourRiderFourLapDiagnosticsAreIndependentOfCollectionOrder()
    {
        var a = PhysicalSpaceEvidence.FourRiderHeat().Observer.Complete();
        var b = PhysicalSpaceEvidence.FourRiderHeat(reverse: true).Observer.Complete();
        Assert.Equal(a.Intervals, b.Intervals); Assert.Equal(a.Work, b.Work);
        Assert.Equal(a.FrameCoverageGaps, b.FrameCoverageGaps);
    }
    [Theory]
    [InlineData("D")] [InlineData("G")] [InlineData("H")] [InlineData("I")] [InlineData("J")]
    public void S_EquivalentSubdivisionPreservesPairConclusion(string scenario)
    {
        var input = PhysicalSpaceEvidence.Controlled(scenario);
        var split = input.SelectMany(i => new[] { new LinearBikePoseInterval(i.Sample(0), i.Sample(.37)),
            new LinearBikePoseInterval(i.Sample(.37), i.Sample(1)) }).ToArray();
        var a = PhysicalSpaceEvidence.Summarize(ContestedSpaceResolver.Observe(input))[0];
        var b = PhysicalSpaceEvidence.Summarize(ContestedSpaceResolver.Observe(split))[0];
        Assert.Equal(a.Classifications, b.Classifications);
        Assert.InRange(Math.Abs(a.FirstTouchSeconds!.Value - b.FirstTouchSeconds!.Value), 0, 2e-7);
        Assert.InRange(Math.Abs(a.MinimumSeparationMeters - b.MinimumSeparationMeters), 0, 1e-4);
    }
    [Fact]
    public void T_WrappedPoseUsesTwoDegreesInsteadOfFullRevolution()
    {
        var span = PhysicalSpaceEvidence.Linear(1, new(0, 0), new(20, 0), Math.PI - .01, -Math.PI + .01);
        Assert.Equal(-Math.PI, span.Sample(.5).Attitude.BikeHeadingRadians, 10);
        Assert.Equal(.02, span.RateBounds(0, 1).AngularSpeedBoundRadiansPerSecond, 10);
        var other = PhysicalSpaceEvidence.Linear(2, new(0, 2), new(20, 2));
        Assert.Equal(0, ContestedSpaceResolver.Observe(new[] { span, other }).Work.DetectedConflicts);
    }
    [Fact]
    public void MechanicalSignedSeparationIsSymmetricAndHasExpectedTouchAndComponents()
    {
        var a = PhysicalSpaceEvidence.Linear(1, new(0, 0), new(0, 0)).Sample(0);
        var b = PhysicalSpaceEvidence.Linear(2, new(2.1, 0), new(2.1, 0)).Sample(0);
        Assert.Equal(0, MechanicalSeparation.Between(a, b).SignedMeters, 12);
        Assert.Equal(MechanicalSeparation.Between(a, b).SignedMeters, MechanicalSeparation.Between(b, a).SignedMeters);
        var parallel = PhysicalSpaceEvidence.Controlled("C");
        var overlap = MechanicalSeparation.Between(parallel[0].Sample(0), parallel[1].Sample(0));
        Assert.True(overlap.PenetrationMeters > 0);
        Assert.True(overlap.ComponentA == BikeComponent.Handlebar || overlap.ComponentB == BikeComponent.Handlebar);
    }
    [Fact]
    public void InvalidDomainValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeterPoint(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BikeAttitudeSample(-1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => BikeAngles.Wrap(double.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => new SpeedwayBikeDimensions(1, 2, .8, 0, .04));
        Assert.Throws<ArgumentException>(() => new ReferenceBikeAttitude(.5, .5, .3, .4, .9));
        Assert.Throws<ArgumentException>(() => PhysicalSpaceEvidence.Linear(1, new(0, 0), new(1, 0), startTime: 1, endTime: 1));
    }
    [Fact]
    public void NumericalBudgetExhaustionStaysExplicitAndCannotFeedFutureConsequences()
    {
        var a = new LooseBounds(PhysicalSpaceEvidence.Linear(1, new(0, 0), new(0, 0)));
        var b = PhysicalSpaceEvidence.Linear(2, new(0, .81), new(0, .81));
        var r = ContestedSpaceResolver.Observe(new PhysicalPoseInterval[] { a, b });
        Assert.True(r.Work.UnresolvedIntervals > 0);
        Assert.InRange(r.Work.NarrowPhaseEvaluations, 1, GeometryNumerics.MaximumEvaluationsPerInterval);
        Assert.False(Assert.Single(r.Intervals).EligibleForFutureInteraction);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CaptureAndGeometryLeaveProductionResultsWearDecisionsAndLegacyEventsExactlyUnchanged(bool adaptive)
    {
        var baseline = PhysicalSpaceEvidence.FourRiderHeat(4, capture: false, adaptive: adaptive, seed: 3);
        var observed = PhysicalSpaceEvidence.FourRiderHeat(4, reverse: true, adaptive: adaptive, seed: 3);
        observed.Observer.Complete();
        Assert.Equal(baseline.Result.Classification, observed.Result.Classification);
        Assert.Equal(baseline.Result.Log.Lines, observed.Result.Log.Lines);
        Assert.Equal(baseline.Result.Log.SurfaceChanges, observed.Result.Log.SurfaceChanges);
        foreach (var a in baseline.Riders)
        {
            var b = observed.Riders.Single(r => r.RiderId == a.RiderId);
            Assert.Equal(a.LateralPosition, b.LateralPosition); Assert.Equal(a.Morale, b.Morale);
            Assert.Equal(a.Speed, b.Speed); Assert.Equal(a.Position, b.Position);
        }
        for (var s = 0; s < baseline.Surface.SegmentCount; s++) for (var l = 0; l < 5; l++)
            Assert.Equal(baseline.Surface.GetSurface(s, l), observed.Surface.GetSurface(s, l));
    }
    [Fact]
    public void SensitivitySeparatesRobustControlsFromAssumptionDependentRotation()
    {
        var rows = PhysicalSpaceEvidence.Sensitivity();
        Assert.All(rows.Where(r => r.Scenario is "A" or "B"), r => Assert.False(r.Conflict));
        Assert.All(rows.Where(r => r.Scenario == "C"), r => Assert.True(r.Conflict));
        Assert.Contains(rows, r => r.Scenario == "I" && r.Conflict);
        Assert.Contains(rows, r => r.Scenario == "I" && !r.Conflict);
    }
    [Fact]
    public void DeterministicEvidenceRegeneratesByteForByte()
    {
        var actual = PhysicalSpaceEvidence.DeterministicJson();
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/calibration/physical-occupancy-evidence.json"));
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal), actual);
        Assert.Equal(actual, PhysicalSpaceEvidence.DeterministicJson());
    }
    private static ContestedSpaceEvent First(IEnumerable<PhysicalPoseInterval> input) => ContestedSpaceResolver.Observe(input)
        .Intervals.Where(i => i.EligibleForFutureInteraction).OrderBy(i => i.FirstTouchCommonTimeSeconds).First();
    private static ResolvedSimulationStep Resolve(Track track, IReadOnlyList<RiderState> riders, int segment, int target)
    {
        var engine = new SimulationEngine(new Targets(target));
        var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track, new TrackSurfaceState(1, 0, .35f)), riders,
            new SimulationStepContext(55, 0, (int)(riders[0].Position.TotalSegmentProgress / track.Segments.Count), segment, 55, 4));
        return engine.Resolve(snapshot, engine.Decide(snapshot), new HeatSimulationOptions { Laps = 4, IncidentFrequency = 0 });
    }
    private sealed class Targets(int target) : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(target, 0); }
    private sealed class LooseBounds(LinearBikePoseInterval source) : PhysicalPoseInterval(source.RiderId, source.FrameId,
        source.StartTimeSeconds, source.EndTimeSeconds, source.Dimensions, false)
    {
        public override PhysicalBikePose Sample(double time) => source.Sample(time);
        public override PoseRateBounds RateBounds(double start, double end) => new(new(0, 0), 0, 1e12);
    }
}
