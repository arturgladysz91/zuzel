using System.Text.Json;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Logging;
using CoreSim.Race;
using Xunit;
using TrajectoryIntent = CoreSim.Decisions.TrajectoryIntent;

namespace CoreSim.Tests;

public sealed class TrajectoryEvaluatorTests
{
    [Theory]
    [InlineData(SegmentType.Straight, 35)] [InlineData(SegmentType.TurnEntry, 35)]
    [InlineData(SegmentType.TurnMiddle, 13)] [InlineData(SegmentType.TurnExit, 5)]
    public void GrammarIsBoundedUniqueCanonicalAndContainsAllHolds(SegmentType phase, int count)
    {
        var candidates = TrajectoryCandidates.Generate(phase);
        Assert.Equal(count, candidates.Count);
        Assert.Equal(candidates.Count, candidates.Distinct().Count());
        Assert.Equal(candidates, TrajectoryCandidates.Generate(phase));
        for (var lane = 0; lane <= 4; lane++) Assert.Contains(new(lane, lane, lane), candidates);
        Assert.All(candidates, c =>
        {
            Assert.InRange(c.EntryTarget, 0, 4); Assert.InRange(c.ApexTarget, 0, 4); Assert.InRange(c.ExitTarget, 0, 4);
            Assert.InRange(Math.Abs(c.EntryTarget - c.ApexTarget), 0, 1);
            Assert.InRange(Math.Abs(c.ApexTarget - c.ExitTarget), 0, 1);
        });
        if (count == 35)
            foreach (var intent in new[] { new TrajectoryIntent(3, 2, 3), new(0, 1, 2), new(4, 3, 2) })
                Assert.Contains(intent, candidates);
    }

    [Theory]
    [InlineData(-1, 0, 0)] [InlineData(0, 5, 0)] [InlineData(0, 0, 5)]
    public void IntentRejectsInvalidAnchors(int e, int a, int x)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new TrajectoryIntent(e, a, x));

    [Theory]
    [InlineData(0, 0, 0)] [InlineData(1, 1, 1)] [InlineData(2, 2, 2)]
    [InlineData(3, 3, 3)] [InlineData(4, 4, 4)]
    [InlineData(3, 2, 3)] [InlineData(0, 1, 2)] [InlineData(4, 3, 2)]
    public void FixedAndMovingCandidatesExactlyMatchSeparateProduction(int e, int a, int x)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = At(track, 1, e, 22f);
        var context = Context(track, rider);
        Parity(context, new(e, a, x), new[] { 1, 2, 3, 4 });
    }

    [Theory]
    [InlineData(StartingGate.A)] [InlineData(StartingGate.D)]
    public void GateCentersAndStationaryReactionSurviveProjection(StartingGate gate)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = new RiderState(RiderProfile.CreateDefault(17), gate, track);
        var context = Context(track, rider);
        var intent = new TrajectoryIntent(2, 1, 2);
        var projected = Parity(context, intent, new[] { 0, 1, 2, 3, 4 });
        var launch = projected.ResolvedMotions[0];
        Assert.Equal(rider.LateralPosition, launch.Initial.LateralPosition);
        Assert.NotEqual((float)rider.Lane, launch.Initial.LateralPosition);
        var reaction = LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(rider.Profile.Skills);
        Assert.Equal(reaction, launch.ReactionTimeSeconds);
        foreach (var time in new[] { 0f, reaction / 2f, reaction })
        {
            var stationary = launch.SampleAtTime(time);
            Assert.Equal(rider.LateralPosition, stationary.LateralPosition);
            Assert.Equal(0f, stationary.TravelledMeters); Assert.Equal(0f, stationary.SpeedMetersPerSecond);
            Assert.Equal(0d, stationary.CanonicalProgress);
        }
        Assert.True(launch.TotalTimeSeconds > reaction);
    }

    [Theory]
    [InlineData(0, .125f, 0, 0, 1, 2)]
    [InlineData(1, .25f, 0, 3, 2, 3)]
    [InlineData(2, .25f, 0, 2, 2, 3)]
    [InlineData(3, .25f, 0, 2, 2, 2)]
    [InlineData(5, .125f, 0, 3, 2, 3)]
    [InlineData(5, .125f, 3, 3, 2, 3)]
    [InlineData(8, .125f, 3, 2, 1, 2)]
    public void RemainingHorizonUsesExactCurrentStateAndWholeLogicalStraights(int index, float progress,
        int lap, int e, int a, int x)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = At(track, index, 2, 21f, progress, lap);
        var context = Context(track, rider, laps: 4);
        int[] expected = index switch
        {
            0 => new[] { 0, 1, 2, 3, 4 }, 1 => new[] { 1, 2, 3, 4 },
            2 => new[] { 2, 3, 4 }, 3 => new[] { 3, 4 },
            5 when lap == 3 => new[] { 5, 6, 7, 8 },
            5 => new[] { 5, 6, 7, 8, 0 }, _ => new[] { 8 },
        };
        var result = Parity(context, new(e, a, x), expected);
        Assert.Equal(context.Rider.CanonicalProgress, result.ResolvedMotions[0].Initial.CanonicalProgress);
        Assert.Equal(context.Rider.ElapsedTimeSeconds, result.ResolvedMotions[0].StartElapsedTimeSeconds);
        if (index == 2) Assert.Null(result.EntryLateralPosition);
        if (index == 3) { Assert.Null(result.EntryLateralPosition); Assert.Null(result.MiddleLateralPosition); }
        if (index == 5 && lap == 0)
            Assert.InRange(result.FollowingStraightDistanceMeters, 62d, 64d);
    }

    [Fact]
    public void SplitPreCornerStraightWrapsAndUsesEntryUntilTheNextCorner()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var context = Context(track, At(track, 8, 2, 22f), laps: 4);
        var evaluator = new TrajectoryEvaluator(context);
        Assert.Equal(new[] { 8, 0, 1, 2, 3, 4 }, evaluator.Horizon.Select(p => p.SegmentIndex));
        Assert.Equal(new[] { 0, 1, 1, 1, 1, 1 }, evaluator.Horizon.Select(p => p.LapIndex));
        Assert.All(evaluator.Horizon.Take(2), p => Assert.Equal(TrajectoryPhase.Entry, p.Phase));
        Parity(context, new(3, 2, 3), new[] { 8, 0, 1, 2, 3, 4 });
    }

    [Fact]
    public void HorizonUsesTopologyWithArbitrarySegmentIdsAndMultipleStraightPieces()
    {
        var track = new Track(new[] { new TrackSegment(90, SegmentType.TurnEntry),
            new TrackSegment(12, SegmentType.TurnMiddle), new TrackSegment(43, SegmentType.TurnExit),
            new TrackSegment(81, SegmentType.Straight, 10f), new TrackSegment(2, SegmentType.Straight, 20f),
            new TrackSegment(72, SegmentType.TurnEntry) });
        Parity(Context(track, At(track, 0, 2, 18f)), new(2, 1, 2), new[] { 0, 1, 2, 3, 4 });
    }

    [Fact]
    public void CFixedRouteRankingComesFromActualProductionAndRemovesStaticInconsistency()
    {
        var source = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "C");
        var track = CornerFirst(source.Track.Geometry);
        var focal = source.Riders.Single(r => r.Id == 2);
        var predicted = new List<(int Lane, double Time)>();
        var actual = new List<(int Lane, double Time)>();
        for (var lane = 0; lane <= 4; lane++)
        {
            var rider = (focal with { Lane = lane, SegmentProgress = 0f, ArrivalOffsetSeconds = 0f }).Create(track);
            var context = Context(track, rider, source.CreateSurface());
            var intent = new TrajectoryIntent(lane, lane, lane);
            var projection = new TrajectoryEvaluator(context).Evaluate(intent);
            var replay = Independent(context, intent, new[] { 0, 1, 2, 3 });
            predicted.Add((lane, projection.PredictedTraversalTimeSeconds));
            actual.Add((lane, replay.Sum(s => (double)s.Motions.Single().TotalTimeSeconds)));
        }
        Assert.Equal(actual.OrderBy(r => r.Time).Select(r => r.Lane), predicted.OrderBy(r => r.Time).Select(r => r.Lane));
        Assert.Equal(actual.Select(r => r.Time), predicted.Select(r => r.Time));
        // Historical formula stays analysis-only; its ordering is computed, never a forced winner.
        var old = Enumerable.Range(0, 5).OrderBy(lane =>
            (LaneModel.TurnArcLengthMeters(lane, track.Geometry) * 3f + track.Geometry.StraightLengthMeters)
            / SegmentPhysics.MaxSafeTurnSpeed(lane, track.Geometry, source.LaneSurfaces[lane], focal.Skills,
                focal.Create(track).ActiveSetup)).ToArray();
        Assert.NotEqual(string.Join(',', old), string.Join(',', predicted.OrderBy(r => r.Time).Select(r => r.Lane)));
    }

    [Fact]
    public void EvaluationDoesNotMutateLiveRaceAndCandidateWearCannotLeak()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var rider = At(track, 1, 2, 22f); var rival = At(track, 1, 3, 20f, id: 18);
        var state = TrackState.CreateDefault(track); var log = new SimLog(); log.Add("already committed");
        var engine = new SimulationEngine(new AdaptiveDecisionModel());
        var snapshot = engine.CaptureSnapshot(track, state, new[] { rider, rival }, new(54, 1, 0, 1, 13, 4));
        var beforeRiders = JsonSerializer.Serialize(new[] { rider, rival });
        var beforeSurface = Cells(state.Snapshot()); var beforeLog = JsonSerializer.Serialize(log);
        var model = new AdaptiveDecisionModel(); var context = new RiderDecisionContext(snapshot, snapshot.Rider(17));
        var decision = model.Decide(context);
        Assert.Equal(decision, model.Decide(context));
        Assert.Equal(beforeRiders, JsonSerializer.Serialize(new[] { rider, rival }));
        Assert.Equal(beforeSurface, Cells(state.Snapshot())); Assert.Equal(beforeLog, JsonSerializer.Serialize(log));
        var evaluator = new TrajectoryEvaluator(context);
        var b = evaluator.Evaluate(new(3, 2, 3), true);
        evaluator.Evaluate(new(0, 1, 2), true);
        Assert.Equal(JsonSerializer.Serialize(b), JsonSerializer.Serialize(evaluator.Evaluate(new(3, 2, 3), true)));
    }

    [Fact]
    public void PrefixReuseRepeatedAndReversedCandidatesAreBitIdentical()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var context = Context(track, At(track, 1, 2, 22f));
        var candidates = TrajectoryCandidates.Generate(SegmentType.TurnEntry);
        var cached = new TrajectoryEvaluator(context); var cold = new TrajectoryEvaluator(context, reuseProductionPrefixes: false);
        foreach (var intent in candidates)
            Assert.Equal(JsonSerializer.Serialize(cold.Evaluate(intent, true)), JsonSerializer.Serialize(cached.Evaluate(intent, true)));
        var reversed = new TrajectoryEvaluator(context);
        foreach (var intent in candidates.Reverse())
            Assert.Equal(JsonSerializer.Serialize(cached.Evaluate(intent, true)), JsonSerializer.Serialize(reversed.Evaluate(intent, true)));
        Assert.True(cached.ProductionResolutionCount < cold.ProductionResolutionCount);
        var model = new AdaptiveDecisionModel();
        var forward = model.Evaluate(context, candidates); var backward = model.Evaluate(context, candidates.Reverse());
        Assert.Equal(forward.Decision, backward.Decision);
        Assert.Equal(JsonSerializer.Serialize(forward.Candidates), JsonSerializer.Serialize(backward.Candidates));
    }

    [Theory]
    [InlineData(20f)] [InlineData(100f)]
    public void SeedAndModelSeedAffectOnlyImperfectRawCellObservations(float reading)
    {
        var track = CornerFirst(Track.CreateStandingStartExample().Geometry);
        var skills = new RiderSkills(50f, 50f, 50f, reading, 50f, 50f);
        var rider = new RiderState(new RiderProfile(17, "Reader", skills, RiderStyle.Balanced), 2) { Speed = 20f };
        var a = Context(track, rider, seed: 1); var b = Context(track, rider, seed: 73);
        var model = new AdaptiveDecisionModel(1);
        var perceivedA = model.PerceivedTrackState(a); var perceivedB = model.PerceivedTrackState(b);
        var otherModel = new AdaptiveDecisionModel(73).PerceivedTrackState(a);
        if (reading == 100f)
        {
            Assert.Equal(Cells(a.TrackState), Cells(perceivedA));
            Assert.Equal(Cells(perceivedA), Cells(perceivedB)); Assert.Equal(Cells(perceivedA), Cells(otherModel));
            Assert.Equal(JsonSerializer.Serialize(model.Evaluate(a).Candidates), JsonSerializer.Serialize(model.Evaluate(b).Candidates));
        }
        else
        {
            Assert.NotEqual(Cells(perceivedA), Cells(perceivedB)); Assert.NotEqual(Cells(perceivedA), Cells(otherModel));
            Assert.NotEqual(model.Evaluate(a).Selected.PredictedTraversalTimeSeconds, model.Evaluate(b).Selected.PredictedTraversalTimeSeconds);
        }
        Assert.Equal(Cells(perceivedA), Cells(model.PerceivedTrackState(a)));
        Assert.Equal(a.TrackState.GetSurface(0, 2).Moisture, perceivedA.GetSurface(0, 2).Moisture);
        var intent = new TrajectoryIntent(2, 1, 2);
        Assert.Equal(JsonSerializer.Serialize(new TrajectoryEvaluator(a).Evaluate(intent)),
            JsonSerializer.Serialize(new TrajectoryEvaluator(b).Evaluate(intent)));
    }

    [Fact]
    public void ReversedRidersAndExternalOccupancyDoNotAlterSoloPhysicalScores()
    {
        var track = CornerFirst(Track.CreateStandingStartExample().Geometry);
        var riders = new[] { At(track, 0, 2, 20f), At(track, 0, 3, 20f, id: 18) };
        var engine = new SimulationEngine(new AdaptiveDecisionModel()); var state = TrackState.CreateDefault(track);
        var step = new SimulationStepContext(54, 0, 0, 0, 13, 4);
        var normal = engine.CaptureSnapshot(track, state, riders, step);
        var reverse = engine.CaptureSnapshot(track, state, riders.Reverse().ToArray(), step);
        Assert.Equal(engine.Decide(normal), engine.Decide(reverse));
        var model = new AdaptiveDecisionModel();
        var all = model.Evaluate(new(normal, normal.Rider(17)));
        var soloSnapshot = engine.CaptureSnapshot(track, state, new[] { riders[0] }, step);
        var solo = model.Evaluate(new(soloSnapshot, soloSnapshot.Rider(17)));
        Assert.Equal(all.Candidates.Select(c => JsonSerializer.Serialize(c.Traversal)), solo.Candidates.Select(c => JsonSerializer.Serialize(c.Traversal)));
        Assert.Contains(all.Candidates, c => c.OccupancyCost == .30f);
        Assert.All(solo.Candidates, c => Assert.Equal(0f, c.OccupancyCost));
        foreach (var c in all.Candidates.Where(c => c.Traversal.CompletedHorizon))
            Assert.Equal(c.PredictedTraversalTimeSeconds + c.StylePreferenceCost + c.LaneChangeReluctanceCost
                + c.OccupancyCost + c.SurfaceRiskCost, c.TotalCost);
    }

    [Fact]
    public void RiskyPhysicalConsequencesAreRetainedAndIncompleteCrashesAreInfeasible()
    {
        var track = CornerFirst(Track.CreateStandingStartExample().Geometry);
        var rider = At(track, 1, 4, 80f);
        var context = Context(track, rider);
        var result = new TrajectoryEvaluator(context).Evaluate(new(4, 4, 4), true);
        Assert.False(result.CompletedHorizon); Assert.Contains(result.PhaseEndpoints, p => p.Outcome == SegmentOutcome.Crash);
        Assert.True(result.PredictedTraversalTimeSeconds > 0); Assert.True(result.PhysicalDistanceMeters > 0);
        Assert.Equal(double.PositiveInfinity, new AdaptiveDecisionModel().Evaluate(context, new[] { new TrajectoryIntent(4, 4, 4) }).Selected.TotalCost);
        Parity(context, new(4, 4, 4), new[] { 1, 2, 3 });
    }

    [Fact]
    public void BNeutralChoiceUsesFastestAvailableProductionTrajectory()
    {
        var source = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "B");
        var focal = source.Riders.Single(r => r.Id == 2);
        var skills = focal.Skills;
        var neutral = focal with
        {
            Skills = new RiderSkills(skills.Start, skills.Speed, skills.SlideControl, 100f, skills.PairRiding, skills.Adaptability),
            Style = new RiderStyle(1f, 1f, .5f, .5f),
        };
        var result = new AdaptiveDecisionModel().Evaluate(Context(source.Track, neutral.Create(source.Track), source.CreateSurface()));
        var fastest = result.Candidates.OrderBy(c => c.PredictedTraversalTimeSeconds)
            .ThenBy(c => c.RequestedLaneChange).ThenBy(c => c.Intent.EntryTarget)
            .ThenBy(c => c.Intent.ApexTarget).ThenBy(c => c.Intent.ExitTarget).First();
        Assert.Equal(fastest.Intent, result.Selected.Intent);
        Assert.Equal(0f, result.Selected.OccupancyCost); Assert.Equal(0f, result.Selected.LaneChangeReluctanceCost);
    }

    [Fact]
    public void SurfaceReversalAndGeometryChangeMeasuredMetricsWithoutForcedWinner()
    {
        var evidence = TrajectoryIntentEvidence.Run();
        var f = evidence.Choices.Single(c => c.Scenario == "F"); var g = evidence.Choices.Single(c => c.Scenario == "G");
        Assert.NotEqual(f.SelectedIntent, g.SelectedIntent);
        Assert.NotEqual(f.Selected.PredictedTraversalTimeSeconds, g.Selected.PredictedTraversalTimeSeconds);
        var tight = evidence.Choices.Single(c => c.Scenario == "J-tight");
        var wide = evidence.Choices.Single(c => c.Scenario == "J-wide");
        Assert.NotEqual(tight.Selected.PhysicalDistanceMeters, wide.Selected.PhysicalDistanceMeters);
        Assert.NotEqual(tight.Selected.PredictedTraversalTimeSeconds, wide.Selected.PredictedTraversalTimeSeconds);
        foreach (var row in evidence.FixedControls)
            Assert.Equal(row.IndependentProductionTimeSeconds, row.Projection.PredictedTraversalTimeSeconds);
        var h = evidence.FixedControls.Where(c => c.Scenario == "H").ToArray();
        Assert.NotEqual(h[0].Projection.PhysicalDistanceMeters, h[4].Projection.PhysicalDistanceMeters);
        Assert.NotEqual(h[0].Projection.CornerExitSpeedMetersPerSecond, h[4].Projection.CornerExitSpeedMetersPerSecond);
        Assert.NotEqual(h[0].Projection.FollowingStraightTimeSeconds, h[4].Projection.FollowingStraightTimeSeconds);
        Assert.Contains(evidence.MovingExamples, c => c.PhaseEndpoints.Any(p => !p.AnchorReached));
    }

    [Fact]
    public void ExactlyEqualCostsUseCanonicalTupleRegardlessOfCandidateOrder()
    {
        var track = new Track(new[] { new TrackSegment(81, SegmentType.Straight, 1f) });
        var context = Context(track, At(track, 0, 2, 18f), laps: 1);
        var candidates = new[] { new TrajectoryIntent(2, 3, 3), new(2, 1, 1) };
        var model = new AdaptiveDecisionModel();
        var forward = model.Evaluate(context, candidates); var reverse = model.Evaluate(context, candidates.Reverse());
        Assert.Equal(forward.Candidates[0].TotalCost, forward.Candidates[1].TotalCost);
        Assert.Equal(new(2, 1, 1), forward.Selected.Intent); Assert.Equal(forward.Selected.Intent, reverse.Selected.Intent);
    }

    [Fact]
    public void CorrectionDriveDiscontinuitySubdividesProductionStepAndRetainsExactReplayParity()
    {
        // This real A/seed 3 snapshot exposed a missing time root in #53 when a
        // hypothetical outward target crossed the correction/drive switching boundary.
        var source = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "A");
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/fixtures/trajectory-time-discontinuity.json"));
        using var json = JsonDocument.Parse(File.ReadAllText(fixture));
        var root = json.RootElement;
        var input = root.GetProperty("Rider"); var position = input.GetProperty("Position");
        var rider = new RiderState(JsonSerializer.Deserialize<RiderProfile>(input.GetProperty("Profile"))!, input.GetProperty("Lane").GetInt32())
        {
            LateralPosition = input.GetProperty("LateralPosition").GetSingle(), Speed = input.GetProperty("Speed").GetSingle(),
            ElapsedTimeSeconds = input.GetProperty("ElapsedTimeSeconds").GetSingle(), Risk = input.GetProperty("Risk").GetSingle(),
        };
        rider.RestorePosition(RiderPosition.Create(position.GetProperty("LapsCompleted").GetInt32() + 1,
            position.GetProperty("SegmentIndex").GetInt32(), position.GetProperty("SegmentProgress").GetSingle(),
            source.Track.Segments.Count, position.GetProperty("DistanceMeters").GetSingle()));
        rider.SetStatus((RiderRaceStatus)input.GetProperty("Status").GetInt32());
        rider.SetLastResolvedSegmentId(input.GetProperty("LastResolvedSegmentId").GetInt32());
        var cells = root.GetProperty("Surface").EnumerateArray().Select(cell => new TrackSurfaceState(
            cell.GetProperty("Grip").GetSingle(), cell.GetProperty("Ruts").GetSingle(),
            cell.GetProperty("Moisture").GetSingle())).ToArray();
        var state = new TrackState(source.Track.Segments.Count, 5, (segment, lane) => cells[segment * 5 + lane]);
        var snapshot = new SimulationEngine(new Targets(new(1, 1, 1), 15)).CaptureSnapshot(source.Track, state,
            new[] { rider }, JsonSerializer.Deserialize<SimulationStepContext>(root.GetProperty("Step"))!);
        var context = new RiderDecisionContext(snapshot, snapshot.Rider(2));
        var intent = new TrajectoryIntent(1, 1, 1);
        Parity(context, intent, new[] { 7, 0 });
        var replay = Independent(context, intent, new[] { 7, 0 });
        var steps = replay.SelectMany(r => r.Diagnostics.Single().ExecutedPath?.Steps ?? Array.Empty<ExecutedPathStep>()).ToArray();
        Assert.Contains(steps, s => s.TimeSolveSubdivisions > 0);
        Assert.All(steps, s =>
        {
            Assert.InRange(s.TimeSolveSubdivisions, 0, ExecutedPathTraversal.TimeSolveSubdivisionLimit);
            Assert.InRange(s.DistanceMeters, float.Epsilon, LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters);
        });
    }

    [Fact]
    public void CurrentEvidenceRegeneratesIdenticallyAndMatchesCheckedInBytes()
    {
        var evidence = TrajectoryIntentEvidence.Run();
        Assert.Equal(TrajectoryIntentEvidence.Json(evidence), TrajectoryIntentEvidence.Json(TrajectoryIntentEvidence.Run()));
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/calibration"));
        Assert.Equal(File.ReadAllText(Path.Combine(root, "trajectory-intent-evaluation.json")).Replace("\r\n", "\n", StringComparison.Ordinal),
            TrajectoryIntentEvidence.Json(evidence));
        Assert.Equal(File.ReadAllText(Path.Combine(root, "trajectory-intent-evaluation.md")).Replace("\r\n", "\n", StringComparison.Ordinal),
            TrajectoryIntentEvidence.Markdown(evidence));
    }


    [Theory]
    [InlineData(0, 0f, true)]
    [InlineData(0, .125f, false)]
    [InlineData(1, 0f, false)]
    [InlineData(2, .125f, false)]
    public void EntrySpeedIsObservedOnlyWhenReplayIncludesActualCornerStart(int segment, float progress, bool observed)
    {
        var track = CornerFirst(TrackGeometry.Default);
        var context = Context(track, At(track, segment, 2, 20f, progress));
        var result = new TrajectoryEvaluator(context).Evaluate(new(2, 2, 2));
        if (observed) Assert.Equal(20f, result.CornerEntrySpeedMetersPerSecond);
        else Assert.Null(result.CornerEntrySpeedMetersPerSecond);
    }

    internal static Track CornerFirst(TrackGeometry geometry) => new(new[]
    {
        new TrackSegment(0, SegmentType.TurnEntry), new TrackSegment(1, SegmentType.TurnMiddle),
        new TrackSegment(2, SegmentType.TurnExit), new TrackSegment(3, SegmentType.Straight),
        new TrackSegment(4, SegmentType.TurnEntry), new TrackSegment(5, SegmentType.TurnMiddle),
        new TrackSegment(6, SegmentType.TurnExit), new TrackSegment(7, SegmentType.Straight),
    }, geometry);
    internal static RiderState At(Track track, int index, int lane, float speed, float progress = 0,
        int lap = 0, int id = 17)
    {
        var rider = new RiderState(RiderProfile.CreateDefault(id), lane) { Speed = speed, ElapsedTimeSeconds = 11f };
        rider.RestorePosition(RiderPosition.Create(lap + 1, index, progress, track.Segments.Count, 100f));
        return rider;
    }
    internal static RiderDecisionContext Context(Track track, RiderState rider, TrackState? state = null, int laps = 4, int seed = 13)
    {
        var snapshot = new SimulationEngine(new Targets(new(2, 2, 2), 0)).CaptureSnapshot(track,
            state ?? TrackState.CreateDefault(track, new TrackSurfaceState(.8f, .1f, .35f)), new[] { rider },
            new(54, rider.SegmentIndex + rider.LapsCompleted * track.Segments.Count,
                rider.LapsCompleted, rider.SegmentIndex, seed, laps));
        return new(snapshot, snapshot.Rider(rider.RiderId));
    }
    private static string Cells(TrackStateSnapshot surface) => JsonSerializer.Serialize(Enumerable.Range(0, surface.SegmentCount)
        .SelectMany(index => Enumerable.Range(0, 5).Select(lane => surface.GetSurface(index, lane))));

    private static TrajectoryTraversal Parity(RiderDecisionContext context, TrajectoryIntent intent, int[] expected)
    {
        var evaluator = new TrajectoryEvaluator(context);
        Assert.Equal(expected, evaluator.Horizon.Select(p => p.SegmentIndex));
        var projected = evaluator.Evaluate(intent, true); var replay = Independent(context, intent, expected);
        Assert.Equal(replay.Sum(s => (double)s.Motions.Single().TotalTimeSeconds), projected.PredictedTraversalTimeSeconds);
        Assert.Equal(replay.Sum(s => (double)s.Motions.Single().TotalDistanceMeters), projected.PhysicalDistanceMeters);
        Assert.Equal(replay.Select(s => s.Motions.Single()), projected.ResolvedMotions);
        Assert.Equal(replay.Select(s => s.Changes.Single().Outcome), projected.PhaseEndpoints.Select(p => p.Outcome));
        Assert.Equal(replay.Select(s => s.Changes.Single().LateralPosition), projected.PhaseEndpoints.Select(p => p.LateralPosition));
        var corner = context.Snapshot.Track.CornerTopology.Corners.FirstOrDefault(c => c.ContainsSegment(expected.First())
            || expected.Contains(c.StartSegmentIndex));
        if (corner is not null)
        {
            var exit = replay.FirstOrDefault(s => s.Snapshot.Step.SegmentIndex == corner.EndSegmentIndex);
            if (exit is not null) Assert.Equal(exit.Changes.Single().Speed, projected.CornerExitSpeedMetersPerSecond);
        }
        if (projected.FollowingStraightEndSpeedMetersPerSecond.HasValue)
            Assert.Equal(replay.Last().Changes.Single().Speed, projected.FollowingStraightEndSpeedMetersPerSecond);
        return projected;
    }
    internal static IReadOnlyList<ResolvedSimulationStep> Independent(RiderDecisionContext context, TrajectoryIntent intent, int[] indices)
    {
        // Independent normal engine run: explicit fixture segment sequence, no evaluator/horizon call.
        var rider = context.Rider.ToMutableCopy(); var riders = new[] { rider };
        var state = new TrackState(context.TrackState.SegmentCount, 5, context.TrackState.GetSurface);
        var lastCorner = Array.FindLastIndex(indices, index => context.Snapshot.Track.Segments[index].Type != SegmentType.Straight);
        var engine = new SimulationEngine(new Targets(intent, lastCorner < 0 ? int.MaxValue : context.StepNumber + lastCorner));
        var results = new List<ResolvedSimulationStep>();
        for (var i = 0; i < indices.Length && rider.Status is RiderRaceStatus.NotStarted or RiderRaceStatus.Racing; i++)
        {
            var input = engine.CaptureSnapshot(context.Snapshot.Track, state, riders, context.Snapshot.Step with
            { SegmentIndex = indices[i], LapIndex = rider.LapsCompleted, StepNumber = context.StepNumber + i });
            var result = engine.Resolve(input, engine.Decide(input), new HeatSimulationOptions
            { IncidentFrequency = 0, Laps = context.Snapshot.Step.RequiredLaps });
            results.Add(result); engine.Commit(result, riders, state, new SimLog());
        }
        return results;
    }
    private sealed class Targets(TrajectoryIntent intent, int lastCornerStep) : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(intent.TargetFor(segment.Type));
        public RiderDecision Decide(RiderDecisionContext context) => new(context.StepNumber > lastCornerStep
            ? intent.ExitTarget : intent.TargetFor(context.Segment.Type));
    }
}
