using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using System.Text.Json;
using Xunit;
using RacingIntent = CoreSim.Decisions.TrajectoryIntent;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class ContestedSpaceResponseTests
{
    public static IEnumerable<object[]> Scenarios => ContestedSpaceResponseEvidence.Scenarios().Select(s => new object[] {s.Name});
    private static ContestedScenario Scenario(string name) => ContestedSpaceResponseEvidence.Scenarios().Single(s => s.Name == name);
    private static string Capture(ResolvedSimulationStep step) => JsonSerializer.Serialize(new
        { step.Changes, step.Events, step.Diagnostics, Motions = step.Motions, step.Interaction });

    [Theory, MemberData(nameof(Scenarios))]
    public void RiderAndIntentCollectionOrderDoesNotChangeAnyPhysicalOrTacticalResult(string name)
        => Assert.Equal(Capture(ContestedSpaceResponseEvidence.Resolve(Scenario(name))),
            Capture(ContestedSpaceResponseEvidence.Resolve(Scenario(name), reverse:true)));

    [Theory, MemberData(nameof(Scenarios))]
    public void EveryClusterHasBoundedSearchAndNoNewConsequenceEventType(string name)
    {
        var step = ContestedSpaceResponseEvidence.Resolve(Scenario(name));
        foreach (var episode in step.Interaction!.Episodes)
        {
            Assert.InRange(episode.RiderIds.Count, 2, 4);
            Assert.InRange(episode.Geometry.Count, 1, 6);
            Assert.InRange(episode.Candidates.Count, 1, 81 * episode.PassCount);
            Assert.InRange(episode.PassCount, 1, 2);
            foreach (var alternatives in episode.ResponseAlternatives.GroupBy(a => a.RiderId)) Assert.InRange(alternatives.Count(),1,3);
            foreach (var candidate in episode.Candidates.Where(c => c.Feasible))
            {
                Assert.True(candidate.MinimumSeparationMeters > GeometryNumerics.ContactDistanceMeters);
                Assert.Equal(0,candidate.IneligibleIntervals);
            }
        }
        foreach (var change in step.Changes)
        {
            Assert.InRange(change.LateralPosition, 0, 4);
            Assert.True(float.IsFinite(change.Speed)); Assert.True(float.IsFinite(change.ElapsedTimeSeconds));
        }
        foreach (var contact in step.Events.Where(e => e.Type != SimulationEventType.SegmentResolved))
            Assert.Contains(step.Interaction.Episodes, e => e.LegacyFallbackUsed && e.UnresolvedMechanicalContacts.Count > 0);
    }
    [Fact]
    public void DefenderCannotCoverThroughAnEstablishedInsideFootprintEvenWithMaximumDefense()
    {
        var input = Scenario("B-close-too-late");
        input = input with { Riders = input.Riders.Select(r => r with { Defense = 99, Attack = 99, Combativeness = 1 }).ToArray() };
        var episode = Assert.Single(ContestedSpaceResponseEvidence.Resolve(input).Interaction!.Episodes);
        Assert.True(episode.Geometry[0].ForwardFootprintOverlapMeters > 0);
        Assert.DoesNotContain(episode.ResponseAlternatives, a => a.RiderId == 1 && a.Response == InteractionResponse.CoverInside);
        Assert.True(episode.ResolvedWithoutMechanicalContact);
        Assert.Contains(episode.SelectedResponses, a => a.RiderId == 1 && a.Response is InteractionResponse.YieldOutward or InteractionResponse.Hold or InteractionResponse.BackOut);
        Assert.Contains(episode.Candidates, c => !c.Feasible && c.Rejection == "Mechanical overlap (#55)");
    }
    [Fact]
    public void EntryDefenseIsAnIntentRequestWithProductionCostAndNoScriptedWinner()
    {
        var step = ContestedSpaceResponseEvidence.Resolve(Scenario("A-entry-close"));
        var episode = Assert.Single(step.Interaction!.Episodes);
        Assert.Equal(InteractionContext.CornerEntryClosing, episode.Context);
        Assert.True(episode.Geometry[0].ForwardFootprintOverlapMeters < 0);
        Assert.Contains(episode.ResponseAlternatives, a => a.RiderId == 1 && a.Response == InteractionResponse.CoverInside
            && a.Intent.EntryTarget < 2 && a.Intent.ApexTarget < 2);
        Assert.Contains(episode.Candidates, c => c.Cost.AdditionalTraversalTimeSeconds != 0);
        Assert.Contains(episode.SelectedResponses, a => a.RiderId == 1 && a.Response == InteractionResponse.CoverInside);
        Assert.True(episode.ResolvedWithoutMechanicalContact);
    }
    [Theory]
    [InlineData("C-cutback-clean")]
    [InlineData("C-cutback-poor")]
    public void CutbackUsesProductionExitTargetsAndRemainsClear(string name)
    {
        var episode = Assert.Single(ContestedSpaceResponseEvidence.Resolve(Scenario(name)).Interaction!.Episodes);
        Assert.Contains(episode.ResponseAlternatives, a => a.Response == InteractionResponse.CutInside && a.Intent.ExitTarget < 2);
        Assert.True(episode.ResolvedWithoutMechanicalContact);
        if (name == "C-cutback-clean") Assert.Contains(episode.SelectedResponses,a => a.Response == InteractionResponse.CutInside);
    }
    [Fact]
    public void OutsideSurfaceChangesPhysicalTimeWithoutAnOutsideBonus()
    {
        var clean = ContestedSpaceResponseEvidence.Resolve(Scenario("E-outside-useful"));
        var poor = ContestedSpaceResponseEvidence.Resolve(Scenario("E-outside-poor"));
        Assert.NotEqual(clean.Changes.Single(c => c.RiderId == 2).ElapsedTimeSeconds, poor.Changes.Single(c => c.RiderId == 2).ElapsedTimeSeconds);
        Assert.NotEqual(clean.Diagnostics.Single(c => c.RiderId == 2).ContinuousCornerProfile!.TravelTimeSeconds,
            poor.Diagnostics.Single(c => c.RiderId == 2).ContinuousCornerProfile!.TravelTimeSeconds);
    }
    [Fact]
    public void FourRiderFirstBendIsOneConnectedJointCluster()
    {
        var episode = Assert.Single(ContestedSpaceResponseEvidence.Resolve(Scenario("G-four-first-bend")).Interaction!.Episodes);
        Assert.Equal(InteractionContext.FirstBendCluster, episode.Context);
        Assert.Equal(4, episode.RiderIds.Count); Assert.Equal(81, episode.Candidates.Count);
        Assert.True(episode.ResolvedWithoutMechanicalContact);
        Assert.Contains(episode.SelectedResponses, a => a.Response == InteractionResponse.BackOut);
    }
    [Theory]
    [InlineData("H-three-squeeze")]
    [InlineData("edge-trapped")]
    public void TrappedRidersCanRequestLongitudinalYieldWithNoOpponentDisplacement(string name)
    {
        var episode = Assert.Single(ContestedSpaceResponseEvidence.Resolve(Scenario(name)).Interaction!.Episodes);
        Assert.Contains(episode.ResponseAlternatives, a => a.Response is InteractionResponse.BackOut or InteractionResponse.EmergencyAvoid
            && a.DriveControl == RiderDriveControl.LiftThrottle && a.HoldLateralPosition);
        Assert.All(episode.ResponseAlternatives, a => Assert.InRange(a.Intent.EntryTarget, 0, 4));
        if (name == "H-three-squeeze") Assert.All(episode.SelectedResponses,a => Assert.Equal(RiderDriveControl.LiftThrottle,a.DriveControl));
    }
    [Fact]
    public void MaximumCombativenessDoesNotSuppressEmergencyAttemptOrMakeOverlapFeasible()
    {
        var episode = Assert.Single(ContestedSpaceResponseEvidence.Resolve(Scenario("imminent-overlap")).Interaction!.Episodes);
        Assert.Contains(episode.ResponseAlternatives, a => a.RiderId == 2 && a.Response == InteractionResponse.EmergencyAvoid);
        Assert.DoesNotContain(episode.Candidates, c => c.Feasible);
        Assert.Contains(episode.SelectedResponses, a => a.Response == InteractionResponse.EmergencyAvoid);
        Assert.NotEmpty(episode.UnresolvedMechanicalContacts);
    }
    [Theory]
    [InlineData(.125f)]
    [InlineData(.0625f)]
    [InlineData(.03125f)]
    public void DeeperPenetrationAtMaximumCombativenessKeepsEmergencyResponse(float lateralDelta)
    {
        var input = Scenario("imminent-overlap");
        input = input with {Riders=input.Riders.Select(r => r with {Combativeness=1,Lateral=r.Id==2?2+lateralDelta:2}).ToArray()};
        var episode = ContestedSpaceResponseEvidence.Resolve(input).Interaction!.Episodes.Single();
        Assert.DoesNotContain(episode.Candidates,c => c.Feasible);
        Assert.All(episode.SelectedResponses,a => Assert.Equal(InteractionResponse.EmergencyAvoid,a.Response));
        Assert.All(episode.SelectedResponses,a => Assert.Equal(RiderDriveControl.LiftThrottle,a.DriveControl));
    }
    [Fact]
    public void ShorterConflictTimeAndLessAvailableRoomCannotSuppressHighCombatEmergency()
    {
        var early = Scenario("F-exit-cross"); var late = Scenario("imminent-overlap");
        InteractionEpisodeDiagnostic Run(ContestedScenario s) => ContestedSpaceResponseEvidence.Resolve(s with
            {Riders=s.Riders.Select(r => r with {Defense=99,Attack=99,Combativeness=1}).ToArray()}).Interaction!.Episodes.Single();
        var a=Run(early);var b=Run(late);
        Assert.True(b.Geometry.Min(g=>g.TimeToConflictSeconds) < a.Geometry.Min(g=>g.TimeToConflictSeconds));
        Assert.True(a.ResolvedWithoutMechanicalContact);
        Assert.All(b.SelectedResponses,r=>Assert.Equal(InteractionResponse.EmergencyAvoid,r.Response));
        var trapped=Run(Scenario("edge-trapped"));
        Assert.Contains(trapped.ResponseAlternatives,r=>r.DriveControl==RiderDriveControl.LiftThrottle);
        Assert.DoesNotContain(trapped.Candidates,c=>c.Feasible && c.Rejection.Length>0);
    }
    [Fact]
    public void FarApartFeatureOnIsExactlyInertIncludingWearAndPhysicalMotion()
    {
        var off = ContestedSpaceResponseEvidence.Resolve(Scenario("K-far-apart"), enabled:false);
        var on = ContestedSpaceResponseEvidence.Resolve(Scenario("K-far-apart"));
        Assert.Equal(off.Changes, on.Changes); Assert.Equal(off.Events, on.Events);
        Assert.Equal(off.Diagnostics, on.Diagnostics); Assert.Equal(off.Motions, on.Motions);
        Assert.Empty(on.Interaction!.Episodes); Assert.Equal(2,on.Interaction.Work.ProductionResolutions);
    }
    [Theory]
    [InlineData(20, 55, 1)]
    [InlineData(99, 105, 99)]
    public void StrengthMassAndCanonicalPairRidingCannotAffectPreContactChoiceOrPhysics(int strength, float mass, int pairRiding)
    {
        var input = Scenario("G-four-first-bend");
        Assert.Equal(Capture(ContestedSpaceResponseEvidence.Resolve(input)), Capture(ContestedSpaceResponseEvidence.Resolve(input with
            { Riders = input.Riders.Select(r => r with { Strength = strength, MassKg = mass, PairRiding = pairRiding }).ToArray() })));
    }
    [Fact]
    public void TechniqueAndConditionOnlyChangeSmallExecutionMargins()
    {
        var input = Scenario("G-four-first-bend");
        double Margin(int technique, float condition) => InteractionGeometryModel.ExecutionMarginMeters(
            ContestedSpaceResponseEvidence.Snapshot(input with { Riders = input.Riders.Select(r => r with
                { Technique = technique, Condition = condition }).ToArray() }).Riders[0], new());
        Assert.True(Margin(20,1) > Margin(50,1)); Assert.True(Margin(50,1) > Margin(80,1));
        Assert.InRange(Margin(50,.7f) - Margin(50,1), .0119, .0121);
        Assert.InRange(Margin(50,.4f) - Margin(50,1), .0239, .0241);
        Assert.Equal(Capture(ContestedSpaceResponseEvidence.Resolve(input,enabled:false)),
            Capture(ContestedSpaceResponseEvidence.Resolve(input with { Riders = input.Riders.Select(r => r with
                { Technique = 1, Condition = .4f, Attack = 99, Defense = 99, Combativeness = 1 }).ToArray() },enabled:false)));
    }
    [Fact]
    public void LiftRunsTheExistingSignedForcePrimitiveAndAutomaticallyEndsWithItsRequest()
    {
        var input = Scenario("K-far-apart") with { Riders = new[] { new ContestedRiderInput(1,1.5f,0,22,new(2,2,2)) } };
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(input);
        var engine = new SimulationEngine(new Hold());
        var options = new HeatSimulationOptions { EnableContestedSpaceResponses = true, IncidentFrequency = 0 };
        ResolvedSimulationStep Run(bool lift) => engine.ResolveProduction(snapshot, new[] {new RiderIntent(1,new RiderDecision(2)
            { HoldLateralPosition = true, DriveControl = lift ? RiderDriveControl.LiftThrottle : null })},options,legacyContacts:false);
        var full = Run(false); var lifted = Run(true);
        Assert.True(lifted.Changes[0].Speed < full.Changes[0].Speed);
        Assert.True(lifted.Changes[0].ElapsedTimeSeconds > full.Changes[0].ElapsedTimeSeconds);
        Assert.Equal(full.Changes[0].Position, lifted.Changes[0].Position);
        Assert.Equal(22f,lifted.Motions[0].Initial.SpeedMetersPerSecond);
        var path = lifted.Diagnostics[0].ExecutedPath!;
        Assert.All(path.Nodes, n => Assert.Equal(1.5f,n.LateralPosition));
        Assert.All(path.Nodes, n => Assert.True(n.NetDriveAccelerationMetersPerSecondSquared < 0));
        for (var i = 0; i < path.Steps.Count; i++)
            Assert.Equal(LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(path.Nodes[i].SpeedMetersPerSecond,
                path.Steps[i].DistanceMeters,0,snapshot.Riders[0].ActiveSetup), path.Nodes[i+1].SpeedMetersPerSecond);
        Assert.Equal(Capture(full),Capture(Run(false)));
        var off = options with {EnableContestedSpaceResponses=false};
        Assert.Equal(engine.ResolveProduction(snapshot,new[]{new RiderIntent(1,new RiderDecision(2))},off).Changes,
            engine.ResolveProduction(snapshot,new[]{new RiderIntent(1,new RiderDecision(2)
                {DriveControl=RiderDriveControl.LiftThrottle,HoldLateralPosition=true})},off).Changes);
    }
    [Fact]
    public void ResolveIsPureUntilCommitAndTrackerCannotCrossHeats()
    {
        var input = Scenario("D-inside-overlap"); var tracker = new InteractionEpisodeTracker();
        var a = ContestedSpaceResponseEvidence.Resolve(input,tracker:tracker);
        var b = ContestedSpaceResponseEvidence.Resolve(input,tracker:tracker);
        Assert.Equal(Capture(a),Capture(b)); Assert.Empty(tracker.Active);
        var riders = a.Snapshot.Riders.Select(r => r.ToMutableCopy()).ToArray();
        var surface = new TrackState(a.Snapshot.Track.Segments.Count,5,a.Snapshot.TrackState.GetSurface);
        new SimulationEngine(new Hold()).Commit(a,riders,surface,new SimLog(false));
        Assert.Equal(1,tracker.EpisodeCount);
        Assert.Throws<InvalidOperationException>(() => tracker.Bind(new(a.Snapshot.Step with {HeatId=999},a.Snapshot.Track,a.Snapshot.TrackState,a.Snapshot.Riders)));
    }
    [Fact]
    public void HysteresisKeepsOneEpisodeThenCleanSeparationAllowsANewBattle()
    {
        var step = ContestedSpaceResponseEvidence.Resolve(Scenario("D-inside-overlap"));
        var row = step.Interaction!.Episodes[0].Geometry[0].Space;
        var tracker = new InteractionEpisodeTracker(); tracker.Bind(step.Snapshot);
        var first = tracker.Engage(new[]{1,2},0,InteractionContext.InsideOverlap);
        Assert.Same(first,tracker.Engage(new[]{2,1},.4,InteractionContext.InsideOverlap));
        Assert.Same(first,tracker.Engage(new[]{1,2},.8,InteractionContext.InsideOverlap));
        var clear = row with { MinimumSeparationMeters = 2, Kind = SpaceConflictKind.None, NumericallyResolved = true,
            FirstTouchCommonTimeSeconds = null,IntervalStartSeconds=1,IntervalEndSeconds=1.3,MinimumSeparationLowerBoundMeters=2 };
        tracker.ObserveClearance(0,new[]{clear with {IntervalStartSeconds=0,IntervalEndSeconds=1,MinimumSeparationLowerBoundMeters=.2}},new());
        Assert.Null(first.End); // #55's sampled pair minimum cannot certify this local interval.
        tracker.ObserveClearance(1,new[]{clear},new()); Assert.Null(first.End);
        tracker.ObserveClearance(1.3,new[]{clear with {MinimumSeparationMeters=.3,IntervalStartSeconds=1.3,IntervalEndSeconds=1.5}},new()); Assert.Null(first.End);
        tracker.ObserveClearance(1.5,new[]{clear with {IntervalStartSeconds=1.5,IntervalEndSeconds=1.8}},new());
        tracker.ObserveClearance(2,new[]{clear with {IntervalStartSeconds=1.8,IntervalEndSeconds=2.1}},new()); Assert.Equal(1.95,first.End);
        Assert.NotEqual(first.Id,tracker.Engage(new[]{1,2},3,InteractionContext.StraightReattack).Id);
    }
    [Fact]
    public void AlongsideGeometryUsesActualFootprintNotLaneOrArrivalSort()
    {
        PhysicalBikePose Pose(int id,double x,double y,double angle=0) => new(id,"test",new(x,y),new(0,0,angle),SpeedwayBikeDimensions.Reference,0);
        var a = Pose(1,0,0); var b = Pose(2,2.05,1);
        var report = ContestedSpaceResolver.Observe(new PhysicalPoseInterval[]
        { new LinearBikePoseInterval(a,a.Repose(new(20,0),0,1)), new LinearBikePoseInterval(b,b.Repose(new(22.05,1),0,1)) });
        var row = report.Intervals[0];
        var overlapping = InteractionGeometryModel.Describe(row,a,b,new(new(20,0),0,0),new(new(20,0),0,0));
        Assert.True(overlapping.ForwardFootprintOverlapMeters > 0); Assert.Equal(OverlapState.PartialOverlap,overlapping.AOverlap);
        var behind = InteractionGeometryModel.Describe(row,a,Pose(2,2.15,1),new(new(21,0),0,0),new(new(20,0),0,0));
        Assert.True(behind.ForwardFootprintOverlapMeters < 0); Assert.Equal(OverlapState.Approaching,behind.AOverlap);
        var rotated = InteractionGeometryModel.Project(Pose(1,0,0,Math.PI/4).Footprint,new(1,0));
        Assert.NotEqual(2.1,rotated.Max - rotated.Min);
    }
    [Theory]
    [InlineData("F-exit-cross")]
    [InlineData("G-four-first-bend")]
    public void PairCacheChangesOnlyWorkCountsAndNotSelectionOrPhysics(string name)
    {
        var scenario = Scenario(name); var snapshot = ContestedSpaceResponseEvidence.Snapshot(scenario);
        var intents = scenario.Riders.Select(r => new RiderIntent(r.Id,new RiderDecision(r.Intent.TargetFor(snapshot.Segment.Type))
            {Trajectory=r.Intent})).ToArray();
        var options = new HeatSimulationOptions {EnableContestedSpaceResponses=true,IncidentFrequency=0}; var engine = new SimulationEngine(new Hold());
        var warm = new ContestedSpaceInteractionCoordinator(new()).Resolve(engine,snapshot,intents,options);
        var cold = new ContestedSpaceInteractionCoordinator(new(),reusePairResults:false).Resolve(engine,snapshot,intents.Reverse().ToArray(),options);
        Assert.Equal(warm.Changes,cold.Changes); Assert.Equal(warm.Motions,cold.Motions);
        Assert.Equal(JsonSerializer.Serialize(warm.Interaction!.Episodes),JsonSerializer.Serialize(cold.Interaction!.Episodes));
        Assert.True(warm.Interaction.Work.NarrowPhaseEvaluations < cold.Interaction.Work.NarrowPhaseEvaluations);
    }
    [Fact]
    public void InvalidControlsAndParametersAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RiderDriveControl(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RiderDriveControl(-.1f));
        Assert.Throws<ArgumentException>(() => new ContestedSpaceParameters {ReleaseClearanceMeters=.1}.Validate());
    }
    [Fact]
    public void ClusterGraphIsIndependentOfPairAndEdgeEnumerationOrder()
    {
        var input = Scenario("G-four-first-bend");
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(input);
        var edges = ContestedSpaceResponseEvidence.Resolve(input).Interaction!.Episodes.Single().Geometry.ToArray();
        var first = ContestedSpaceInteractionCoordinator.Clusters(edges,snapshot,new());
        var reversed = ContestedSpaceInteractionCoordinator.Clusters(edges.Reverse().ToArray(),snapshot,new());
        Assert.Equal(JsonSerializer.Serialize(first),JsonSerializer.Serialize(reversed));
    }
    [Fact]
    public void AsynchronousThreatSamplesCommittedOpponentMotionAtTheSameHeatTime()
    {
        PhysicalBikePose Pose(int id,double time,double x,double y) => new(id,"clock",new(x,y),new(time,0,0),SpeedwayBikeDimensions.Reference);
        var committed = new LinearBikePoseInterval(Pose(1,0,0,0),Pose(1,2,20,0));
        var future = new LinearBikePoseInterval(Pose(1,2,20,0),Pose(1,3,30,0));
        var requested = new LinearBikePoseInterval(Pose(2,1,10,.5),Pose(2,3,30,.5));
        var report = ContestedSpaceResolver.Observe(new[]{committed,future,requested});
        var edge = Assert.Single(ContestedSpaceInteractionCoordinator.Threats(report,new PhysicalPoseInterval[]{future,requested,committed},1,new()));
        Assert.Equal(1,edge.CommonTimeSeconds);
        Assert.True(edge.Space.EligibleForFutureInteraction);
        Assert.True(edge.ForwardFootprintOverlapMeters>0);
        Assert.Empty(ContestedSpaceInteractionCoordinator.Threats(report,new PhysicalPoseInterval[]{future,requested,committed},0,new(),
            new Dictionary<int,double>{{1,3},{2,3}}));
    }
    [Fact]
    public void ForecastCommitmentCannotExpireBeforeItsContestedWindow()
    {
        var step=ContestedSpaceResponseEvidence.Resolve(Scenario("D-inside-overlap"));
        var row=step.Interaction!.Episodes[0].Geometry[0].Space;
        var tracker=new InteractionEpisodeTracker(); tracker.Bind(step.Snapshot);
        var episode=tracker.Engage(new[]{1,2},0,InteractionContext.InsideOverlap);
        episode.ReleaseNotBefore=2;
        var clear=row with {MinimumSeparationMeters=2,Kind=SpaceConflictKind.None,NumericallyResolved=true,
            FirstTouchCommonTimeSeconds=null,IntervalStartSeconds=0,IntervalEndSeconds=1,MinimumSeparationLowerBoundMeters=2};
        tracker.ObserveClearance(0,new[]{clear},new()); Assert.Null(episode.End);
        tracker.ObserveClearance(1,new[]{clear with {IntervalStartSeconds=1,IntervalEndSeconds=2.3}},new()); Assert.Null(episode.End);
        tracker.ObserveClearance(2.3,new[]{clear with {IntervalStartSeconds=2.3,IntervalEndSeconds=2.6}},new());
        Assert.Equal(2.45,episode.End);
    }
    [Fact]
    public void UnresolvedEpisodeCallsLegacyOnlyOnceEvenIfOccurrenceRollDoesNothing()
    {
        var tracker = new InteractionEpisodeTracker(); var input = Scenario("imminent-overlap");
        // Stop near the boundary so this actual step cannot already integrate the
        // release delay; the later coverage gap must preserve fallback ownership.
        input = input with { Riders = input.Riders.Select(r => r with { Progress = .98f }).ToArray() };
        var first = ContestedSpaceResponseEvidence.Resolve(input,tracker:tracker);
        Assert.True(first.Interaction!.Episodes.Single().LegacyFallbackUsed);
        Commit(first);
        // Repeat the same unresolved physical fixture after a coverage gap.
        // Missing clearance evidence must neither release nor reroll the episode.
        var snapshot = new SimulationSnapshot(first.Snapshot.Step with {StepNumber=10},first.Snapshot.Track,
            first.Snapshot.TrackState,first.Snapshot.Riders.Select(r => r with {ElapsedTimeSeconds=5}));
        var intents = input.Riders.Select(r => new RiderIntent(r.Id,new RiderDecision(2) {Trajectory=r.Intent})).ToArray();
        var second = new SimulationEngine(new Hold()).Resolve(snapshot,intents,
            new() {EnableContestedSpaceResponses=true,IncidentFrequency=0},tracker);
        var episode = Assert.Single(second.Interaction!.Episodes);
        Assert.Equal(first.Interaction.Episodes[0].EpisodeId,episode.EpisodeId);
        Assert.False(episode.LegacyFallbackUsed); Assert.NotEmpty(episode.UnresolvedMechanicalContacts);
    }
    [Fact]
    public void ClearFallbackObservationDoesNotResetReleaseClock()
    {
        var prior = ContestedSpaceResponseEvidence.Resolve(Scenario("D-inside-overlap")).Interaction!.Episodes.Single();
        var tracker = new InteractionEpisodeTracker(); var input = Scenario("K-far-apart");
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(input); tracker.Bind(snapshot);
        tracker.Engage(new[]{1,2},0,prior.Context).LastDiagnostic = prior;
        ResolvedSimulationStep Run(float time) => new SimulationEngine(new Hold()).Resolve(
            new(snapshot.Step with {StepNumber=(int)time},snapshot.Track,snapshot.TrackState,
                snapshot.Riders.Select(r => r with {ElapsedTimeSeconds=time})),
            input.Riders.Select(r => new RiderIntent(r.Id,new RiderDecision(r.Intent.TargetFor(snapshot.Segment.Type)) {Trajectory=r.Intent})).ToArray(),
            new() {EnableContestedSpaceResponses=true,IncidentFrequency=0},tracker);
        var first = Run(1); Commit(first); Assert.Empty(tracker.Active);
        Assert.Contains(first.Interaction!.Episodes,e => e.EndTimeSeconds==1.45);
        var clear = Run(5); Commit(clear); Assert.Empty(tracker.Active);
        Assert.Empty(clear.Interaction!.Episodes);
    }
    [Fact]
    public void PruningCannotPromoteDiscontinuityOverlapToEligibleContact()
    {
        var tracker = new InteractionEpisodeTracker();
        PhysicalBikePose Pose(int id,double time) => new(id,"quarantine",new(0,0),new(time,0,0),SpeedwayBikeDimensions.Reference);
        foreach (var id in new[]{1,2})
        {
            tracker.History.Add(new LinearBikePoseInterval(Pose(id,0),Pose(id,1),startsAtDiscontinuity:true));
            tracker.History.Add(new LinearBikePoseInterval(Pose(id,1),Pose(id,4)));
        }
        tracker.PruneHistory(5);
        Assert.Equal(4,tracker.History.Count);
        Assert.All(CommonTimePoseHistory.Observe(tracker.History).Intervals,r => Assert.False(r.EligibleForFutureInteraction));
    }
    [Fact]
    public void EdgeCertificateRejectsRotationExcursionBetweenEndpointAndMidpointSamples()
    {
        var track = ContestedSpaceResponseEvidence.CreateTrack(false);
        var interval = new EdgeExcursion();
        foreach (var time in new[]{0d,.5,1d})
            Assert.InRange(interval.Sample(time).Footprint.Chassis.Start.Y,-15,-14);
        Assert.False(ContestedSpaceInteractionCoordinator.WithinTrack(new[]{interval},track,new(track)));
    }
    [Fact]
    public async Task ConcurrentHeatsOwnIndependentEpisodeState()
    {
        var scenario = FourRiderBehaviorSuite.CreateScenarios().Single(s => s.Id == "I");
        var simulator = new HeatSimulator(new AdaptiveDecisionModel());
        string Run(int heat)
        {
            var observer = new EpisodeObserver();
            var result = simulator.SimulateHeat(scenario.Track,scenario.CreateSurface(),scenario.Riders.Select(r => r.Create(scenario.Track)).ToList(),
                new() {Laps=1,Seed=7,EnableContestedSpaceResponses=true},heat,observer);
            foreach (var episode in observer.Rows.GroupBy(e => e.EpisodeId))
                Assert.InRange(episode.Count(e => e.LegacyFallbackUsed),0,1);
            return JsonSerializer.Serialize(new {result.Classification,observer.Rows});
        }
        var expected = new[]{Run(57),Run(58)};
        Assert.Equal(expected,await Task.WhenAll(Task.Run(() => Run(57)),Task.Run(() => Run(58))));
    }
    [Fact]
    public void BackOutProjectionExpiresAfterCurrentTurnStepAndMatchesActualExecutionExactly()
    {
        var scenario = Scenario("D-inside-overlap") with
        { Riders = new[] { new ContestedRiderInput(1, 2.3f, 0, 21, new(2,2,2)) } };
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(scenario);
        var control = new InteractionProjectionControl(RiderDriveControl.LiftThrottle, true);
        Assert.Equal(0, control.Decision(2,0).DriveControl!.Value.PositiveDriveFraction);
        Assert.True(control.Decision(2,0).HoldLateralPosition);
        Assert.Null(control.Decision(2,1).DriveControl); Assert.False(control.Decision(2,1).HoldLateralPosition);
        var evaluator = new TrajectoryEvaluator(new RiderDecisionContext(snapshot,snapshot.Riders[0]),
            driveControl:RiderDriveControl.LiftThrottle,holdLateralPosition:true);
        Assert.Equal(new[]{TrajectoryPhase.Middle,TrajectoryPhase.Exit,TrajectoryPhase.FollowingStraight},evaluator.Horizon.Select(h=>h.Phase));
        var replay = evaluator.Evaluate(new(2,2,2),retainResolvedMotions:true);
        var engine = new SimulationEngine(new Hold());
        var options = new HeatSimulationOptions {EnableContestedSpaceResponses=true,IncidentFrequency=0,EnableLogging=false};
        var rider = snapshot.Riders[0].ToMutableCopy();
        var state = new TrackState(snapshot.Track.Segments.Count,5,snapshot.TrackState.GetSurface);
        for(var prefix=0;prefix<evaluator.Horizon.Count;prefix++)
        {
            var part=evaluator.Horizon[prefix];
            var input=engine.CaptureSnapshot(snapshot.Track,state,new[]{rider},snapshot.Step with {StepNumber=snapshot.Step.StepNumber+prefix,
                SegmentIndex=part.SegmentIndex,LapIndex=part.LapIndex});
            var actual=engine.ResolveProduction(input,new[]{new RiderIntent(1,control.Decision(2,prefix))},options,legacyContacts:false);
            Assert.Equal(JsonSerializer.Serialize(actual.Motions[0]),JsonSerializer.Serialize(replay.ResolvedMotions[prefix]));
            if(prefix==0) Assert.Equal(2.3f,actual.Changes[0].LateralPosition);
            else if(prefix==1)
            {
                Assert.NotEqual(2.3f,actual.Changes[0].LateralPosition);
                var reissued=engine.ResolveProduction(input,new[]{new RiderIntent(1,control.Decision(2,0))},options,legacyContacts:false);
                Assert.Equal(2.3f,reissued.Changes[0].LateralPosition);
                Assert.True(reissued.Changes[0].Speed<actual.Changes[0].Speed);
            }
            engine.Commit(actual,new[]{rider},state,new SimLog(false));
        }
    }
    [Fact]
    public void RealIncidentConflictAfterClearFirstPassGetsOneJointSafetyPassBeforeFallback()
    {
        var scenario=Scenario("B-close-too-late");
        var basis=ContestedSpaceResponseEvidence.Snapshot(scenario);
        var snapshot=new SimulationSnapshot(basis.Step with {Seed=276},basis.Track,basis.TrackState,basis.Riders);
        var engine=new SimulationEngine(new Hold());
        var options=new HeatSimulationOptions {EnableContestedSpaceResponses=true,IncidentFrequency=2,
            InteractionDiagnostics=InteractionDiagnosticsLevel.FullAudit};
        var intents=scenario.Riders.Select(r=>new RiderIntent(r.Id,new RiderDecision(r.Intent.TargetFor(snapshot.Segment.Type)){Trajectory=r.Intent})).ToArray();
        var step=engine.Resolve(snapshot,intents,options);
        var episode=Assert.Single(step.Interaction!.Episodes);
        Assert.Equal(2,episode.PassCount);Assert.True(episode.Pass1ActualMechanicalContact);
        Assert.Contains(episode.Candidates,c=>c.Feasible && c.Responses.SequenceEqual(episode.Pass1SelectedResponses));
        var pass1=engine.ResolveProduction(snapshot,episode.Pass1SelectedResponses.Select(a=>new RiderIntent(a.RiderId,
            new RiderDecision(a.Intent.TargetFor(snapshot.Segment.Type)){Trajectory=a.Intent,DriveControl=a.DriveControl,
                HoldLateralPosition=a.HoldLateralPosition})).ToArray(),options,legacyContacts:false);
        var conflict=ContestedSpaceResolver.Observe(pass1.Motions.SelectMany(m=>ResolvedBikePoses.FromMotion(m,snapshot.Track)));
        Assert.Contains(conflict.Intervals,r=>r.EligibleForFutureInteraction);
        Assert.True(episode.ResolvedWithoutMechanicalContact);Assert.False(episode.LegacyFallbackUsed);
        Assert.Empty(episode.UnresolvedMechanicalContacts);
        Assert.All(episode.SelectedResponses,a=>Assert.Contains(a.Response,new[]{InteractionResponse.KeepIntent,InteractionResponse.BackOut,InteractionResponse.EmergencyAvoid}));
        Assert.Equal(1,step.Interaction.Work.SafetyPasses);Assert.Equal(0,step.Interaction.Work.LegacyFallbackAttempts);
        Assert.Equal(3,step.Interaction.Work.ActualProductionVerifications);
    }
    [Fact]
    public void UnclearSecondSafetyPassAuthorizesExactlyOneLegacyAttempt()
    {
        var step=ContestedSpaceResponseEvidence.Resolve(Scenario("imminent-overlap"));
        var episode=Assert.Single(step.Interaction!.Episodes);
        Assert.Equal(2,episode.PassCount);Assert.NotEmpty(episode.UnresolvedMechanicalContacts);
        Assert.True(episode.LegacyFallbackUsed);Assert.Equal(1,step.Interaction.Work.LegacyFallbackAttempts);
        Assert.Equal(1,step.Interaction.Work.SafetyPasses);
    }
    [Theory]
    [InlineData("A-entry-close",InteractionResponse.CoverInside,1,true)]
    [InlineData("D-inside-overlap",InteractionResponse.YieldOutward,2,true)]
    [InlineData("C-cutback-clean",InteractionResponse.CutInside,2,false)]
    public void IdenticalGeometryAssignsQualityToTheResponsibleAbility(string name,InteractionResponse response,int riderId,bool defensive)
    {
        InteractionAlternative Alternative(int attack,int defense)
        {
            var input=Scenario(name) with {Riders=Scenario(name).Riders.Select(r=>r with {Attack=attack,Defense=defense}).ToArray()};
            return ContestedSpaceResponseEvidence.Resolve(input).Interaction!.Episodes.SelectMany(e=>e.ResponseAlternatives)
                .Single(a=>a.RiderId==riderId && a.Response==response);
        }
        var attacking=Alternative(90,20);var defending=Alternative(20,90);
        Assert.True(defensive ? defending.TacticalPreference<attacking.TacticalPreference : attacking.TacticalPreference<defending.TacticalPreference);
        Assert.Equal(Alternative(20,20).TacticalPreference,(defensive?attacking:defending).TacticalPreference);
    }
    [Fact]
    public void AttackAndDefenseCannotChangeHardEmergencyWhenTechniqueAndConditionAreFixed()
    {
        ResolvedSimulationStep Run(int attack,int defense)=>ContestedSpaceResponseEvidence.Resolve(Scenario("imminent-overlap") with
        {Riders=Scenario("imminent-overlap").Riders.Select(r=>r with {Attack=attack,Defense=defense}).ToArray()});
        var a=Run(90,20);var b=Run(20,90);
        Assert.Equal(a.Changes,b.Changes);Assert.Equal(a.Motions,b.Motions);Assert.Equal(a.Events,b.Events);
        Assert.Equal(JsonSerializer.Serialize(a.Interaction!.Episodes[0].SelectedResponses),JsonSerializer.Serialize(b.Interaction!.Episodes[0].SelectedResponses));
    }
    [Fact]
    public void ProjectionAndPairWorkAreBoundedAndSummaryDoesNotRetainCandidateAudit()
    {
        var scenario=Scenario("G-four-first-bend");var snapshot=ContestedSpaceResponseEvidence.Snapshot(scenario);
        var intents=scenario.Riders.Select(r=>new RiderIntent(r.Id,new RiderDecision(r.Intent.TargetFor(snapshot.Segment.Type)){Trajectory=r.Intent})).ToArray();
        var engine=new SimulationEngine(new Hold());
        var summary=engine.Resolve(snapshot,intents,new(){EnableContestedSpaceResponses=true,IncidentFrequency=0});
        var audit=engine.Resolve(snapshot,intents,new(){EnableContestedSpaceResponses=true,IncidentFrequency=0,InteractionDiagnostics=InteractionDiagnosticsLevel.FullAudit});
        Assert.Equal(summary.Changes,audit.Changes);Assert.Equal(summary.Motions,audit.Motions);
        var unique=Assert.Single(audit.Interaction!.Episodes).ResponseAlternatives
            .Select(a=>(a.RiderId,a.Intent,a.DriveControl?.PositiveDriveFraction??1f,a.HoldLateralPosition)).Distinct().Count();
        Assert.Equal(unique,summary.Interaction!.Work.UniqueRiderAlternativeProjections);
        Assert.InRange(unique,1,12);
        Assert.InRange(summary.Interaction.Work.PairAlternativeChecks,1,54);
        Assert.Equal(81,summary.Interaction.Work.JointCombinationsScored);
        Assert.Empty(Assert.Single(summary.Interaction.Episodes).Candidates);
        Assert.Equal(81,Assert.Single(audit.Interaction!.Episodes).Candidates.Count);
    }
    [Fact]
    public void CertifiedLongClearIntervalOutsideCompetitiveReachEndsStaleForecastOwnership()
    {
        var prior=ContestedSpaceResponseEvidence.Resolve(Scenario("D-inside-overlap")).Interaction!.Episodes.Single();
        var tracker=new InteractionEpisodeTracker();var episode=tracker.Engage(new[]{1,2},0,prior.Context);
        episode.LastDiagnostic=prior;episode.ReleaseNotBefore=100;
        var row=prior.Geometry[0].Space with {IntervalStartSeconds=1,IntervalEndSeconds=2,
            MinimumSeparationMeters=4,MinimumSeparationLowerBoundMeters=4,NumericallyResolved=true,Kind=SpaceConflictKind.None,FirstTouchCommonTimeSeconds=null};
        tracker.ObserveClearance(1,new[]{row},new());Assert.Equal(1.45,episode.End);
        var pending=new InteractionEpisodeTracker();
        var pendingEpisode=pending.Engage(new[]{1,2},0,prior.Context);pendingEpisode.ReleaseNotBefore=100;
        pending.ObserveClearance(0,new[]{row with {IntervalStartSeconds=0,IntervalEndSeconds=1,
            MinimumSeparationMeters=2,MinimumSeparationLowerBoundMeters=2}},new());
        Assert.Null(pendingEpisode.End);Assert.Equal(1,pendingEpisode.ObservedUntil);
        pending.ObserveClearance(1,new[]{row},new());Assert.Equal(1.45,pendingEpisode.End);
        var preciseBounds=new InteractionEpisodeTracker();
        var far=preciseBounds.Engage(new[]{1,2},0,prior.Context);far.ReleaseNotBefore=100;
        preciseBounds.ObserveClearance(1,new[]{row with {NumericallyResolved=false}},new());
        Assert.Equal(1.45,far.End); // Local #55 bound proves distance despite unresolved minimum precision.
        var ambiguous=new InteractionEpisodeTracker();
        var quarantined=ambiguous.Engage(new[]{1,2},0,prior.Context);quarantined.ReleaseNotBefore=100;
        ambiguous.ObserveClearance(1,new[]{row with {Kind=SpaceConflictKind.BoundaryAmbiguous}},new());
        Assert.Null(quarantined.End);
    }
    [Fact]
    public void BoundaryAmbiguityCannotAuthorizeAnUnownedLegacyContact()
    {
        var basis=ContestedSpaceResponseEvidence.Snapshot(Scenario("imminent-overlap"));
        var track=new Track(basis.Track.Segments,new TrackGeometry(60,24,10,14,MathF.PI/3));
        var riders=basis.Riders.Select(r=>r with {Position=RiderPosition.Create(1,1,0,track.Segments.Count),LateralPosition=2,Lane=2}).ToArray();
        var snapshot=new SimulationSnapshot(basis.Step with {SegmentIndex=1},track,basis.TrackState,riders);
        var intents=riders.Select(r=>new RiderIntent(r.RiderId,new RiderDecision(2){Trajectory=new(2,2,2)})).ToArray();
        var engine=new SimulationEngine(new Hold());var options=new HeatSimulationOptions {EnableContestedSpaceResponses=true,IncidentFrequency=0};
        var independent=engine.ResolveProduction(snapshot,intents,options,legacyContacts:false);
        var observed=ContestedSpaceResolver.Observe(independent.Motions.SelectMany(m=>ResolvedBikePoses.FromMotion(m,track)));
        Assert.Contains(observed.Intervals,r=>r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous));
        Assert.DoesNotContain(observed.Intervals,r=>r.EligibleForFutureInteraction);
        var resolved=engine.Resolve(snapshot,intents,options);
        Assert.DoesNotContain(resolved.Events,e=>e.Type!=SimulationEventType.SegmentResolved);
        Assert.Equal(0,resolved.Interaction!.Work.LegacyFallbackAttempts);
    }
    private static void Commit(ResolvedSimulationStep step)
        => new SimulationEngine(new Hold()).Commit(step,step.Snapshot.Riders.Select(r => r.ToMutableCopy()).ToArray(),
            new TrackState(step.Snapshot.Track.Segments.Count,5,step.Snapshot.TrackState.GetSurface),new SimLog(false));
    private sealed class EpisodeObserver : ISimulationStepObserver
    {
        internal List<InteractionEpisodeDiagnostic> Rows = new();
        public void OnStepResolved(ResolvedSimulationStep step) => Rows.AddRange(step.Interaction!.Episodes);
    }
    private sealed class EdgeExcursion() : PhysicalPoseInterval(1,"edge",0,1,SpeedwayBikeDimensions.Reference,false,
        new(0,0,1,SegmentType.Straight,null))
    {
        public override PhysicalBikePose Sample(double time) => new(1,"edge",new(3,-14.5),
            new(time,0,Math.PI/2*Math.Sin(Math.Tau*time)),Dimensions,0,Source);
        public override PoseRateBounds RateBounds(double start,double end) => new(new(0,0),0,Math.PI*Math.PI);
    }
    private sealed class Hold : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment,RiderState rider) => new(rider.Lane); }
}
