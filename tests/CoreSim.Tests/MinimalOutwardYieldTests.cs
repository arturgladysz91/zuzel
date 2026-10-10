using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class MinimalOutwardYieldTests
{
    private static ContestedScenario Scenario(string name) => ContestedSpaceResponseEvidence.Scenarios().Single(s => s.Name == name);

    [Fact]
    public void NearZeroSideRolesAndMetreRequestsUseTheSameGeometryInBandC()
    {
        var scenario=new ContestedScenario("near-zero-side",4,new[]{
            new ContestedRiderInput(1,0,.30f,22,new(0,0,0)),
            new ContestedRiderInput(2,0,.28f,22,new(0,0,0))});
        var snapshot=ContestedSpaceResponseEvidence.Snapshot(scenario);
        var intents=scenario.Riders.Select(r=>new RiderIntent(r.Id,new RiderDecision(0){Trajectory=r.Intent})).ToArray();
        var options=new HeatSimulationOptions{EnableContestedSpaceResponses=true,IncidentFrequency=0,
            InteractionDiagnostics=InteractionDiagnosticsLevel.FullAudit};
        var b=new SimulationEngine(new Hold()).Resolve(snapshot,intents,options);
        var c=new SimulationEngine(new Hold()).Resolve(snapshot,intents,options with{EnablePhysicalContactConsequences=true});
        var alternatives=b.Interaction!.Episodes.SelectMany(e=>e.ResponseAlternatives).ToArray();
        Assert.NotEmpty(alternatives);
        Assert.Equal(alternatives,c.Interaction!.Episodes.SelectMany(e=>e.ResponseAlternatives).ToArray());
    }

    [Fact]
    public void EstablishedInsideAttackNeedsMuchLessThanTheOldFullReferenceResponse()
    {
        var step = ContestedSpaceResponseEvidence.Resolve(Scenario("D-inside-overlap"));
        var episode = Assert.Single(step.Interaction!.Episodes);
        var response = episode.SelectedResponses.Single(a => a.RiderId == 2);
        Assert.Equal(InteractionResponse.YieldOutward, response.Response);
        Assert.NotNull(response.PhysicalTarget);
        var snapshot = step.Snapshot;
        var defender = snapshot.Rider(2);
        var actual = step.Changes.Single(c => c.RiderId == 2);
        var metres = LateralSpaceModel.LateralDistanceMeters(defender.LateralPosition, actual.LateralPosition,
            snapshot.Segment.Type, snapshot.Track.Geometry);
        var old = new TrajectoryEvaluator(new(snapshot, defender)).Evaluate(new(2,2,2), true);
        var oldMetres = LateralSpaceModel.LateralDistanceMeters(defender.LateralPosition, old.ResolvedMotions[0].Final.LateralPosition,
            snapshot.Segment.Type, snapshot.Track.Geometry);
        Assert.InRange(metres, .01, .50);
        Assert.True(metres < oldMetres * .30, $"{metres} m versus {oldMetres} m");
        Assert.Equal(1f, step.Changes.Single(c => c.RiderId == 1).LateralPosition);
        Assert.True(episode.ResolvedWithoutMechanicalContact);
        AssertClear(step);
        VerifyHorizon(snapshot, episode.SelectedResponses);
    }

    [Theory]
    [InlineData("D-inside-overlap")]
    [InlineData("F-exit-cross")]
    [InlineData("H-three-squeeze")]
    [InlineData("G-four-first-bend")]
    [InlineData("edge-trapped")]
    public void CertifiedResponsesHaveNoMechanicalOverlapAcrossExecutedPaths(string name)
    {
        var step = ContestedSpaceResponseEvidence.Resolve(Scenario(name));
        AssertClear(step);
        foreach (var episode in step.Interaction!.Episodes.Where(e => e.ResolvedWithoutMechanicalContact && e.Candidates.Any(c=>c.Feasible && c.Responses.SequenceEqual(e.SelectedResponses))))
            VerifyHorizon(step.Snapshot, episode.SelectedResponses);
        Assert.All(step.Interaction.Episodes, e => Assert.InRange(e.Candidates.Count, 0, 81));
        Assert.InRange(step.Interaction.Work.OutwardTargetTrials, 0, step.Snapshot.Riders.Count * 22);
    }

    [Fact]
    public void PhysicalDestinationUsesEachSegmentsWidthAndTheExistingMovementBudget()
    {
        var basis = ContestedSpaceResponseEvidence.Snapshot(Scenario("D-inside-overlap"));
        var track = new Track(basis.Track.Segments, new TrackGeometry(60,24,10,14,MathF.PI/3));
        var snapshot = new SimulationSnapshot(basis.Step,track,basis.TrackState,new[]{basis.Rider(2)});
        var target = new InteractionLateralTarget(5);
        Assert.Equal(2.5f,target.Position(SegmentType.Straight,track.Geometry));
        Assert.InRange(target.Position(SegmentType.TurnMiddle,track.Geometry),1.6666f,1.6667f);
        var traversal = new TrajectoryEvaluator(new(snapshot,snapshot.Rider(2)),physicalTarget:target).Evaluate(new(1,1,1),true);
        foreach (var motion in traversal.ResolvedMotions)
        {
            var type = track.Segments[motion.SegmentIndex].Type;
            Assert.All(motion.Nodes, n => Assert.InRange(n.LateralPosition,0,4));
            for (var i=1;i<motion.Nodes.Count;i++)
            {
                var a=motion.Nodes[i-1];var b=motion.Nodes[i];
                var actual=LateralSpaceModel.LateralDistanceMeters(a.LateralPosition,b.LateralPosition,type,track.Geometry);
                var maximum=LateralMovementModel.CalculateMaxLateralDistanceMeters(b.LocalTimeSeconds-a.LocalTimeSeconds,
                    type,track.Geometry,snapshot.TrackState.SampleSurface(motion.SegmentIndex,a.LateralPosition),snapshot.Rider(2).Profile.Skills);
                Assert.True(actual <= maximum + .00002f);
            }
            var offset=LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(motion.Final.LateralPosition,type,track.Geometry);
            Assert.InRange(offset,4.99f,5.01f);
            var embedding=new TrackMetricEmbedding(track,true);
            var native=ResolvedBikePoses.FromMotion(motion,track);
            var deterministic=ResolvedBikePoses.FromMotion(motion,track,embedding:embedding);
            Assert.Equal(native.Count,deterministic.Count);
            for(var i=0;i<native.Count;i++)
            {
                var mapped=ResolvedBikePoses.InEmbedding(native[i],embedding);
                var expected=deterministic[i];
                Assert.Equal(expected.StartsAtDiscontinuity,mapped.StartsAtDiscontinuity);
                foreach(var time in new[]{expected.StartTimeSeconds,
                    (expected.StartTimeSeconds+expected.EndTimeSeconds)/2,expected.EndTimeSeconds})
                    Assert.Equal(expected.SampleValue(time),mapped.SampleValue(time));
                Assert.Equal(expected.RateBounds(expected.StartTimeSeconds,expected.EndTimeSeconds),
                    mapped.RateBounds(mapped.StartTimeSeconds,mapped.EndTimeSeconds));
            }
        }
    }

    [Fact]
    public void PhysicalTargetIsInCacheIdentityAndLabelsDoNotChangeThatIdentity()
    {
        var alternative = new InteractionAlternative(1,InteractionResponse.YieldOutward,new(1,1,1),null,0,"test",PhysicalTarget:new(4));
        var calls=0;var cache=new OptionalSafetyProjectionCache<object>(_=>{calls++;return new();});
        var first=cache.Get(alternative);
        Assert.Same(first,cache.Get(alternative with {Reason="other",Response=InteractionResponse.EmergencyAvoid}));
        Assert.NotSame(first,cache.Get(alternative with {PhysicalTarget=new(4.01f)}));
        Assert.Equal(2,calls);
    }

    [Fact]
    public void FeasibleCommitmentRetainsTheExactMetreDestinationWithoutCreeping()
    {
        var scenario=Scenario("D-inside-overlap");
        var first=ContestedSpaceResponseEvidence.Resolve(scenario);
        var row=Assert.Single(first.Interaction!.Episodes);
        var tracker=new InteractionEpisodeTracker();tracker.Bind(first.Snapshot);
        var episode=tracker.Engage(row.RiderIds.ToArray(),0,row.Context);
        foreach(var response in row.SelectedResponses) episode.Commitments[response.RiderId]=response;
        var next=ContestedSpaceResponseEvidence.Resolve(scenario,tracker:tracker,
            parameters:new(){EmergencyTimeSeconds=.000001});
        Assert.Equal(row.SelectedResponses,Assert.Single(next.Interaction!.Episodes).SelectedResponses);
        Assert.Equal(0,next.Interaction.Work.OutwardTargetTrials);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void InvalidPhysicalTargetsAreRejected(float metres)
        => Assert.Throws<ArgumentOutOfRangeException>(()=>new InteractionLateralTarget(metres));

    [Theory]
    [InlineData("edge-trapped")]
    [InlineData("imminent-overlap")]
    public void InsufficientRoomOrLateReactionCannotInventClearance(string name)
    {
        var basis=ContestedSpaceResponseEvidence.Snapshot(Scenario(name));
        if (name=="imminent-overlap") basis=new(basis.Step,basis.Track,basis.TrackState,basis.Riders.Select(r=>r with {Speed=r.RiderId==2 ? 26 : 20}));
        var intents=Scenario(name).Riders.Select(r=>new RiderIntent(r.Id,new RiderDecision(r.Intent.TargetFor(basis.Segment.Type)){Trajectory=r.Intent})).ToArray();
        var step=new SimulationEngine(new Hold()).Resolve(basis,intents,new() {EnableContestedSpaceResponses=true,
            EnablePhysicalContactConsequences=true,IncidentFrequency=0,InteractionDiagnostics=InteractionDiagnosticsLevel.FullAudit});
        if (name=="imminent-overlap")
        {
            Assert.Contains(step.Interaction!.Episodes,e=>!e.ResolvedWithoutMechanicalContact && e.UnresolvedMechanicalContacts.Count>0);
            Assert.NotEmpty(step.Interaction.PhysicalContactConsequences!.AppliedPairs);
            Assert.NotEmpty(step.Events.Where(e=>e.PhysicalContactConsequence is not null));
            Assert.All(step.Interaction.PhysicalContactConsequences.Riders,r=>Assert.Equal(r.PostContactSpeed,step.Changes.Single(c=>c.RiderId==r.RiderId).Speed));
        }
        else
        {
            Assert.Equal(4f,step.Changes.Single(c=>c.RiderId==2).LateralPosition);
            AssertClear(step);
        }
    }

    [Fact]
    public void NoContactAndFeatureOffStayInertWithAnUnusedPhysicalRequest()
    {
        var basis=ContestedSpaceResponseEvidence.Snapshot(Scenario("K-far-apart"));
        var engine=new SimulationEngine(new Hold());
        var intents=basis.Riders.Select(r=>new RiderIntent(r.RiderId,new RiderDecision(r.Lane))).ToArray();
        var a=engine.ResolveProduction(basis,intents,new(){IncidentFrequency=0});
        var b=engine.ResolveProduction(basis,intents.Select(i=>i with {Decision=i.Decision with {InteractionTarget=new(7)}}).ToArray(),new(){IncidentFrequency=0});
        Assert.Equal(a.Changes,b.Changes);Assert.Equal(a.Motions,b.Motions);Assert.Equal(a.Diagnostics,b.Diagnostics);
        Assert.Empty(ContestedSpaceResponseEvidence.Resolve(Scenario("K-far-apart")).Interaction!.Episodes);
    }

    private static void AssertClear(ResolvedSimulationStep step)
    {
        var poses=step.Motions.SelectMany(m=>ResolvedBikePoses.FromMotion(m,step.Snapshot.Track,
            embedding:new TrackMetricEmbedding(step.Snapshot.Track,step.Interaction?.PhysicalContactConsequences is not null))).ToArray();
        var report=CommonTimePoseHistory.Observe(poses);
        foreach (var episode in step.Interaction!.Episodes.Where(e=>e.ResolvedWithoutMechanicalContact))
            Assert.DoesNotContain(report.Intervals,r=>episode.RiderIds.Contains(r.RiderA) && episode.RiderIds.Contains(r.RiderB) && r.EligibleForFutureInteraction);
    }

    private static void VerifyHorizon(SimulationSnapshot snapshot,IReadOnlyList<InteractionAlternative> responses)
    {
        var poses=responses.SelectMany(a=>new TrajectoryEvaluator(new(snapshot,snapshot.Rider(a.RiderId)),
            driveControl:a.DriveControl,holdLateralPosition:a.HoldLateralPosition,physicalTarget:a.PhysicalTarget)
            .Evaluate(a.Intent,true).ResolvedMotions.SelectMany(m=>ResolvedBikePoses.FromMotion(m,snapshot.Track))).ToArray();
        var report=CommonTimePoseHistory.Observe(poses);
        Assert.Empty(report.FrameCoverageGaps);
        Assert.DoesNotContain(report.Intervals,r=>r.EligibleForFutureInteraction);
        Assert.DoesNotContain(report.Intervals,r=>r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous));
        Assert.DoesNotContain(report.Intervals,r=>!r.NumericallyResolved);
    }
    private sealed class Hold : IRiderDecisionModel {public RiderDecision Decide(TrackSegment segment,RiderState rider)=>new(rider.Lane);}
}
