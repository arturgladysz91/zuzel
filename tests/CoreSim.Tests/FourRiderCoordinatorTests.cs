using System.Diagnostics;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using RaceReadiness;
using Xunit;
using Xunit.Abstractions;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class FourRiderCoordinatorTests(ITestOutputHelper output)
{
    private static readonly HeatSimulationOptions Options = new() { Seed = 19, EnableLogging = false,
        EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = true,
        PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit };

    [Fact]
    public void RealControl19StartsAtTheTapesWithFourActualCausalStates()
    {
        var forward = Real(); var reverse = Real(reverse: true);
        Assert.True(forward.Result.Refusal is null, forward.Result.Refusal);
        Assert.Equal(CausalRunStatus.ContactResolved, forward.Result.Status);
        Assert.Equal(Exact.Hash(forward.Result), Exact.Hash(reverse.Result));
        Assert.Equal((3, 4), (forward.Result.Publication.Contact!.RiderA, forward.Result.Publication.Contact.RiderB));
        Assert.Equal(new[] { SegmentType.TurnMiddle, SegmentType.TurnMiddle, SegmentType.TurnEntry, SegmentType.TurnEntry },
            forward.Result.Publication.Riders.Select(s => s.Origin!.Segment.Type));
        Assert.All(forward.Result.Publication.Contact.Plan.Riders, r => Assert.Equal(PhysicalContactSeverity.Disturbed, r.Severity));
        Assert.Equal(Exact.Hash(Counters(forward.Coordinator.Work)), Exact.Hash(Counters(reverse.Coordinator.Work)));
        Verify(forward); Verify(reverse); WriteEvidence(forward);
    }

    [Fact]
    public void RealFourRiderEventAdmissionVetoPublishesNoPartOfTheEvent()
    {
        CausalPublication? before = null; int steps = -1; double distance = -1;
        FourRiderCausalCoordinator? coordinator = null; FrozenHeatContext? heat = null;
        var run = Real(admit: next =>
        {
            if (next.Contact is null) return true;
            before = coordinator!.Published; steps = heat!.AcceptedNumericalSteps; distance = heat.AcceptedDistanceMeters;
            return false;
        }, capture: (c, h) => { coordinator = c; heat = h; });
        Assert.Equal(CausalRunStatus.Refused, run.Result.Status);
        Assert.Contains("Atomic four-rider admission veto", run.Result.Refusal);
        Assert.Same(before, run.Result.Publication); Assert.Null(run.Result.Publication.Contact);
        Assert.Equal(steps, run.Heat.AcceptedNumericalSteps); Assert.Equal(distance, run.Heat.AcceptedDistanceMeters);
        Assert.All(run.Result.Publication.Riders, s => Assert.True(s.ExecutedThroughSeconds <= run.Result.Publication.CommittedFrontier));
    }

    [Fact]
    public void ExistingCrashSeverityInterruptsTheFourRiderTransactionWithoutStationaryFutureMotion()
    {
        // Declared rolling fixture: real integrated rear closing, with the
        // unchanged severity parameters. No prescribed/artificial impulse.
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight, 60),
            new TrackSegment(1, SegmentType.Straight, 60) }, TrackGeometry.Default);
        var progress = new[] { 0f, .105f, .35f, .6f };
        var live = Enumerable.Range(1, 4).Select(id => new RiderState(RiderProfile.CreateDefault(id), 2)
            { Speed = id == 1 ? 25 : 15 }).ToArray();
        foreach (var rider in live)
        {
            rider.RestorePosition(RiderPosition.Create(1, 0, progress[rider.RiderId - 1], 2));
            rider.SetStatus(RiderRaceStatus.Racing);
        }
        var surface = TrackState.CreateDefault(track); var heat = new FrozenHeatContext(track, surface, Options with { IncidentFrequency = 0 }, 91);
        var origin = heat.Capture(live, new(91, 0, 0, 0, 19, Options.Laps));
        var coordinator = new FourRiderCausalCoordinator(new(new Hold()), heat, origin);
        var run = new Run(coordinator, heat, coordinator.Advance(), new(), 0);
        Assert.Null(run.Result.Refusal);
        Assert.Equal(CausalRunStatus.InterruptedByCrash, run.Result.Status);
        var contact = run.Result.Publication.Contact!;
        Assert.Contains(contact.Plan.Riders, r => r.Severity == PhysicalContactSeverity.Crash);
        var crashed = run.Result.Publication.Riders.First(s => s.Rider.Status == RiderRaceStatus.Crashed);
        Assert.Equal(RiderRaceStatus.Crashed, crashed.Rider.Status); Assert.Equal(0, crashed.Rider.Speed);
        Assert.All(History(run.Result.Publication), h => Assert.True(h.ThroughSeconds <= contact.TimeSeconds + 1e-5));
        Assert.Equal(4, run.Coordinator.Work.InvalidatedForecasts); Verify(run);
    }

    [Fact]
    public void ClearHeatAdvancesOneFrontierThroughIndependentSegmentHandoffs()
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight, 20),
            new TrackSegment(1, SegmentType.Straight, 35), new TrackSegment(2, SegmentType.Straight, 45) }, TrackGeometry.Default);
        var live = Enumerable.Range(1, 4).Select(id => new RiderState(RiderProfile.CreateDefault(id), id - 1)
            { Speed = 12 + id * 3, LateralPosition = (id - 1) * 1.3f }).ToArray();
        foreach (var r in live) r.SetStatus(RiderRaceStatus.Racing);
        var surface = TrackState.CreateDefault(track);
        var heat = new FrozenHeatContext(track, surface, Options with { IncidentFrequency = 0 }, 91);
        var origin = heat.Capture(live, new(91, 0, 0, 0, 19, Options.Laps));
        var coordinator = new FourRiderCausalCoordinator(new(new Hold()), heat, origin);
        var result = coordinator.Advance(90);
        Assert.True(result.Refusal is null, result.Refusal);
        Assert.Equal(CausalRunStatus.HorizonReached, result.Status); Assert.Null(result.Publication.Contact);
        Assert.True(coordinator.Work.SegmentHandoffs > 0);
        Assert.True(result.Publication.Riders.Select(s => s.Rider.SegmentIndex).Distinct().Count() > 1);
        var before = result.Publication.CommittedFrontier; var next = coordinator.Advance(15);
        Assert.Equal(CausalRunStatus.HorizonReached, next.Status); Assert.True(next.Publication.CommittedFrontier > before);
        Assert.All(History(next.Publication), h =>
        {
            Assert.Equal(6, h.Certificate.Work.RiderPairs);
            Assert.All(h.Certificate.Intervals, r => { Assert.True(r.NumericallyResolved); Assert.False(r.HasConflict); });
        });
        Assert.All(next.Publication.Riders, s => Assert.True(s.ExecutedThroughSeconds <= next.Publication.CommittedFrontier));
        Assert.Equal(heat.AcceptedNumericalSteps, next.Publication.AcceptedSteps);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void CausalCursorUsesExistingLaunchFixedLineAndEveryCornerPhase(int segment)
    {
        var track = Track.CreateStandingStartExample(); var surface = TrackState.CreateDefault(track);
        var heat = new FrozenHeatContext(track, surface, Options with { IncidentFrequency = 0 }, 91);
        var rider = new RiderSnapshot(1, RiderProfile.CreateDefault(1), RiderPosition.Create(1, segment, 0, track.Segments.Count),
            -1, 2, 2, segment == 0 ? 0 : 18, 0, segment == 0 ? RiderRaceStatus.NotStarted : RiderRaceStatus.Racing,
            0, Setup.BikeSetup.Neutral, .5f, .5f);
        var origin = new SimulationSnapshot(new(91, segment, 0, segment, 19, Options.Laps)
            { FreezeSurfaceForHeat = true, ExecutionOriginTimeSeconds = 0 }, track, heat.Surface, new[] { rider });
        var engine = new SimulationEngine(new Hold()); var intent = new RiderIntent(1, new(2));
        var normal = engine.ResolveProduction(origin, new[] { intent }, heat.Options, legacyContacts: false);
        var cursor = engine.BeginCausalRiderIntegration(origin, intent, heat.Options).Cursor!; var first = cursor;
        while (cursor.Boundary.CanonicalProgress < cursor.Boundary.EndCanonicalProgress)
        {
            var old = cursor.Boundary; cursor = cursor.Resume(heat.Surface, 1).Cursor!;
            Assert.Equal(old.Steps, cursor.Boundary.Steps.Take(old.Steps.Count));
        }
        Assert.InRange(Math.Abs(cursor.Boundary.Speed - normal.Changes[0].Speed), 0, 2e-4);
        Assert.InRange(Math.Abs(cursor.Boundary.TimeSeconds - normal.Changes[0].ElapsedTimeSeconds), 0, 2e-4);
        Assert.InRange(Math.Abs(cursor.Boundary.DistanceMeters - normal.Diagnostics[0].TravelledMeters), 0, 2e-4);
        if (segment == 0) Assert.Equal(LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(rider.Profile.Skills), first.Boundary.TimeSeconds);
        Assert.NotNull(cursor.Resume(heat.Surface).Completed);
    }

    [Fact]
    public void SixPairGateRefusesSimultaneousUncertainAndIncompatibleInputsWithoutArbitraryOrder()
    {
        var row = Assert.Single(ContestedSpaceResolver.ObserveFirstContact(new[]
            { Analysis.PhysicalSpaceEvidence.Linear(1, new(0, 0), new(0, 0)),
              Analysis.PhysicalSpaceEvidence.Linear(2, new(3, 0), new(2, 0)) }).Intervals);
        var work = new SpaceWorkCounters(6, 6, 0, 0, 0, 0, 2, 0);
        var later = row with { RiderA = 3, RiderB = 4, FirstTouchCommonTimeSeconds = row.FirstTouchCommonTimeSeconds + 5e-6 };
        foreach (var rows in new[] { new[] { row, later }, new[] { later, row } })
            Assert.Contains("Simultaneous", Assert.Throws<RiderIntegrationRefusalException>(() =>
                FourRiderCausalCoordinator.SelectFirstEvent(new(rows, work, Array.Empty<FrameCoverageGap>()))).Message);
        Assert.Contains("Uncertain earlier", Assert.Throws<RiderIntegrationRefusalException>(() =>
            FourRiderCausalCoordinator.SelectFirstEvent(new(new[] { row, later with { FirstTouchCommonTimeSeconds = null, NumericallyResolved = false } },
                work, Array.Empty<FrameCoverageGap>()))).Message);
        Assert.Contains("six pair", Assert.Throws<RiderIntegrationRefusalException>(() =>
            FourRiderCausalCoordinator.SelectFirstEvent(new(new[] { row }, work with { RiderPairs = 1 }, Array.Empty<FrameCoverageGap>()))).Message);
        Assert.Contains("frames", Assert.Throws<RiderIntegrationRefusalException>(() =>
            FourRiderCausalCoordinator.SelectFirstEvent(new(new[] { row }, work,
                new[] { new FrameCoverageGap(1, 2, 0, 1, "a", "b") }))).Message);
        Assert.Equal((1, 2), FourRiderCausalCoordinator.SelectFirstEvent(new(new[]
            { row, later with { FirstTouchCommonTimeSeconds = row.FirstTouchCommonTimeSeconds + 2e-5 } },
            work, Array.Empty<FrameCoverageGap>())) is { } first ? (first.RiderA, first.RiderB) : (0, 0));
    }

    [Fact]
    public void HandoffReexpressesTheSamePhysicalOffsetAndNeverClampsANarrowerSegment()
    {
        var geometry = Track.CreateStandingStartExample().Geometry;
        var lateral = FourRiderCausalCoordinator.ReexpressLateralAtHandoff(2, SegmentType.Straight, SegmentType.TurnEntry, geometry);
        Assert.NotEqual(2, lateral);
        Assert.Equal(LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(2, SegmentType.Straight, geometry),
            LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(lateral, SegmentType.TurnEntry, geometry));
        Assert.Contains("never a clamp", Assert.Throws<RiderIntegrationRefusalException>(() =>
            FourRiderCausalCoordinator.ReexpressLateralAtHandoff(4, SegmentType.TurnExit, SegmentType.Straight, geometry)).Message);
    }

    [Fact]
    public void CausalLedgerPreflightsTheWholeSetAndRefusesStaleOrForeignAdmission()
    {
        var track = Track.CreateStandingStartExample(); var surface = TrackState.CreateDefault(track);
        var heat = new FrozenHeatContext(track, surface, Options, 91);
        heat.BeginCausalOwnership();
        RiderIntegrationCursor Cursor(FrozenHeatContext owner, int id)
        {
            var rider = new RiderSnapshot(id, RiderProfile.CreateDefault(id), RiderPosition.Create(1, 4, 0, track.Segments.Count),
                -1, 2, 2, 20, 0, RiderRaceStatus.Racing, 0, Setup.BikeSetup.Neutral, .5f, .5f);
            var origin = new SimulationSnapshot(new(91, 4, 0, 4, 19, Options.Laps)
                { FreezeSurfaceForHeat = true, ExecutionOriginTimeSeconds = 0 }, track, owner.Surface, new[] { rider });
            return new SimulationEngine(new Hold()).BeginCausalRiderIntegration(origin, new(id, new(2)), Options)
                .Cursor!.Resume(owner.Surface, 1).Cursor!;
        }
        var first = Cursor(heat, 1); var foreign = Cursor(new(track, surface, Options, 91), 2);
        var end = Math.Max(first.ExecutedThroughSeconds, foreign.ExecutedThroughSeconds);
        Assert.Throws<InvalidOperationException>(() => heat.PrepareCausalPrefixes(new[] { first, foreign }, end, false));
        Assert.Equal(0, heat.AcceptedNumericalSteps); Assert.Equal(0, heat.PassageCount);
        Assert.Throws<InvalidOperationException>(() => heat.PrepareCausalPrefixes(new[] { first, first }, end, false));
        var commit = heat.PrepareCausalPrefixes(new[] { first }, end, false);
        var publication = new CausalPublication(end, Array.Empty<CausalRiderState>(), null, null, 1,
            first.Boundary.CompletedSteps, first.Boundary.DistanceMeters);
        Assert.Equal(0, heat.PassageCount); commit(publication); Assert.Equal(1, heat.PassageCount);
        Assert.Same(publication, heat.CausalPublication);
        Assert.Throws<InvalidOperationException>(() => commit(publication)); Assert.Equal(1, heat.PassageCount);
        Assert.Throws<InvalidOperationException>(() => heat.AcceptPrefix(first, end, null));
        Assert.Throws<InvalidOperationException>(() => heat.BeginCausalOwnership());
    }

    [Fact]
    public void UnsupportedTerminalEntryAndOrdinaryWorldRefuseActualCursorCreation()
    {
        var track = Track.CreateStandingStartExample(); var surface = TrackState.CreateDefault(track).Snapshot();
        var rider = new RiderSnapshot(1, RiderProfile.CreateDefault(1), RiderPosition.Create(1, 1, 0, track.Segments.Count),
            -1, 4, 4, 100, 0, RiderRaceStatus.Racing, 0, Setup.BikeSetup.Neutral, .5f, .5f);
        var step = new SimulationStepContext(91, 1, 0, 1, 19, Options.Laps);
        var engine = new SimulationEngine(new Hold()); var intent = new RiderIntent(1, new(4));
        Assert.Throws<RiderIntegrationRefusalException>(() => engine.BeginCausalRiderIntegration(new(step, track, surface, new[] { rider }), intent, Options));
        Assert.Contains("terminal entry physics", Assert.Throws<RiderIntegrationRefusalException>(() =>
            engine.BeginCausalRiderIntegration(new(step with { FreezeSurfaceForHeat = true, ExecutionOriginTimeSeconds = 0 },
                track, surface, new[] { rider }), intent, Options with { IncidentFrequency = 0 })).Message);
    }

    private Run Real(bool reverse = false, Func<CausalPublication, bool>? admit = null,
        Action<FourRiderCausalCoordinator, FrozenHeatContext>? capture = null)
    {
        var (track, surface, live) = Catalog.All().Single(s => s.Id == "control").Create();
        if (reverse) live.Reverse();
        var heat = new FrozenHeatContext(track, surface, Options, 91);
        var origin = heat.Capture(live, new(91, 0, 0, 0, 19, Options.Laps));
        var before = Exact.Hash(new { live, Surface = surface.Snapshot() });
        var coordinator = new FourRiderCausalCoordinator(new(new AdaptiveDecisionModel()), heat, origin, reverse, admit);
        capture?.Invoke(coordinator, heat);
        var counts = new Dictionary<ProjectionMaterialization, int>();
        var totalNarrow = 0;
        var watch = Stopwatch.StartNew(); CausalRunResult result;
        try
        {
            ProjectionCaptureAudit.Observer = (kind, n) => counts[kind] = counts.GetValueOrDefault(kind) + n;
            SpaceSearchDiagnostics.Observer = diagnostic => totalNarrow += diagnostic.Evaluations;
            result = coordinator.Advance();
        }
        finally { ProjectionCaptureAudit.Observer = null; SpaceSearchDiagnostics.Observer = null; watch.Stop(); }
        Assert.Equal(before, Exact.Hash(new { live, Surface = surface.Snapshot() })); Assert.Equal(0, heat.WeatherUpdates);
        return new(coordinator, heat, result, counts, watch.Elapsed.TotalSeconds, totalNarrow);
    }

    private static object Counters(CausalCoordinatorWork work) => new { work.Windows, work.PairTimeTrials, work.RiderTimeTrials,
        work.AllRiderCertificates, work.PairCertificates, work.NarrowEvaluations, work.GeometrySubdivisions,
        work.Decisions, work.ReadyGroupPlans, work.SegmentHandoffs, work.WorldCopies, work.ReexecutedAcceptedSteps };

    private static IEnumerable<CausalHistory> History(CausalPublication publication)
    { for (var h = publication.History; h is not null; h = h.Previous) yield return h; }

    private static void Verify(Run run)
    {
        var result = run.Result; var publication = result.Publication; var contact = Assert.IsType<CausalContact>(publication.Contact);
        Assert.Equal(4, publication.Riders.Count); Assert.True(contact.Generation.Consumed);
        Assert.Single(contact.Plan.AppliedPairs); Assert.Equal(2, contact.Plan.Riders.Count);
        Assert.Equal(run.Heat.AcceptedNumericalSteps, publication.AcceptedSteps);
        Assert.InRange(Math.Abs(run.Heat.AcceptedDistanceMeters - publication.AcceptedDistanceMeters), 0, 1e-10);
        Assert.Equal(0, run.Coordinator.Work.WorldCopies); Assert.Equal(0, run.Coordinator.Work.ReexecutedAcceptedSteps);
        foreach (var s in publication.Riders)
        {
            Assert.NotNull(s.Execution); Assert.NotNull(s.Execution!.PartialStep); Assert.NotNull(s.Origin); Assert.NotNull(s.Intent);
            Assert.Same(run.Heat.Surface, s.Origin!.TrackState);
            Assert.Equal(s.Origin.Step.ExecutionOriginTimeSeconds!.Value + s.Execution.TimeSeconds, s.ExecutedThroughSeconds);
            Assert.Equal((float)s.ExecutedThroughSeconds, s.Rider.ElapsedTimeSeconds);
            Assert.InRange(s.ExecutedThroughSeconds - contact.TimeSeconds, 0, 1e-5);
            var effect = contact.Plan.Riders.SingleOrDefault(r => r.RiderId == s.Rider.RiderId);
            Assert.Equal(effect?.PostContactSpeed ?? s.Execution.Speed, s.Rider.Speed);
        }
        Assert.InRange(publication.Riders.Max(s => s.ExecutedThroughSeconds) - publication.Riders.Min(s => s.ExecutedThroughSeconds), 0, 1e-7);
        var history = History(publication).Reverse().ToArray(); Assert.Equal(0, history[0].FromSeconds);
        for (var i = 0; i < history.Length; i++)
        {
            var h = history[i]; if (i > 0) Assert.Equal(history[i - 1].ThroughSeconds, h.FromSeconds);
            Assert.Equal(6, h.Certificate.Work.RiderPairs); Assert.Empty(h.Certificate.FrameCoverageGaps);
            Assert.All(h.Certificate.Intervals, r => Assert.True(r.NumericallyResolved));
            if (i != history.Length - 1) Assert.DoesNotContain(h.Certificate.Intervals, r => r.HasConflict);
            foreach (var id in Enumerable.Range(1, 4))
            {
                var covered = h.FromSeconds;
                foreach (var p in h.Poses.Where(p => p.RiderId == id).OrderBy(p => p.StartTimeSeconds))
                { Assert.Equal(covered, p.StartTimeSeconds); covered = p.EndTimeSeconds; }
                Assert.Equal(h.ThroughSeconds, covered);
            }
        }
        Assert.All(contact.Certificate.Intervals, r => Assert.True(r.MinimumSeparationLowerBoundMeters >= -GeometryNumerics.ContactDistanceMeters));
        var beforeAgain = Exact.Hash(publication); var steps = run.Heat.AcceptedNumericalSteps;
        Assert.Same(result, run.Coordinator.Advance()); Assert.Equal(beforeAgain, Exact.Hash(run.Coordinator.Published));
        Assert.Equal(steps, run.Heat.AcceptedNumericalSteps);
        Assert.Throws<InvalidOperationException>(() => run.Heat.SettleAfterHeat(TrackState.CreateDefault(run.Heat.Track), new Logging.SimLog(false)));
    }

    private void WriteEvidence(Run run) => output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
    {
        run.Result.Status, run.Result.Publication.CommittedFrontier,
        States = run.Result.Publication.Riders.Select(s => new { s.Rider.RiderId, s.Rider.SegmentIndex,
            s.Rider.CanonicalProgress, s.Rider.LateralPosition, s.Rider.Speed, s.ExecutedThroughSeconds }),
        Contact = new { run.Result.Publication.Contact!.RiderA, run.Result.Publication.Contact.RiderB,
            run.Result.Publication.Contact.MaximumStateTimeErrorSeconds,
            run.Result.Publication.Contact.IntegratorClockSkewSeconds, run.Result.Publication.Contact.MaximumCenterDisplacementMeters,
            run.Result.Publication.Contact.MaximumFootprintDisplacementBoundMeters,
            Effects = run.Result.Publication.Contact.Plan.Riders.Select(r => new { r.RiderId, r.Severity, r.SeverityRatio, r.PreContactSpeed, r.PostContactSpeed }) },
        Work = Counters(run.Coordinator.Work), Counts = run.Counts.ToDictionary(p => p.Key.ToString(), p => p.Value),
        run.Heat.PassageCount, run.Heat.AcceptedNumericalSteps, run.Heat.AcceptedDistanceMeters,
        run.TotalNarrow, Seconds = run.Seconds
    }));
    private sealed class Hold : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane) { HoldLateralPosition = true }; }
    private sealed record Run(FourRiderCausalCoordinator Coordinator, FrozenHeatContext Heat,
        CausalRunResult Result, Dictionary<ProjectionMaterialization, int> Counts, double Seconds, int TotalNarrow = 0);
}
