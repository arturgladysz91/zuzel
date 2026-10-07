using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using System.Text.Json;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class PhysicalContactConsequenceTests
{
    private static readonly Lazy<IReadOnlyList<PhysicalContactFixture>> Fixtures = new(PhysicalContactEvidence.Fixtures);
    private static PhysicalContactFixture Side => PhysicalContactEvidence.Side("controlled");
    private static string Json(object? value)=>JsonSerializer.Serialize(value,PhysicalContactEvidence.JsonOptions);
    private static HeatSimulationOptions Options=>PhysicalContactConsequenceEvidence.Options;
    private sealed class Target(int lane) : IRiderDecisionModel
    {public RiderDecision Decide(TrackSegment segment,RiderState rider)=>new(lane);}

    [Fact]
    public void EnabledContactFrameArithmeticIsAccurateAcrossQuadrantsAndLegacyCallsStayExact()
    {
        for(var i=-4096;i<=4096;i++)
        {
            var angle=Math.PI*i/4096;
            var direction=ContactFrameArithmetic.Direction(angle,true);
            Assert.InRange(Math.Abs(direction.X-Math.Cos(angle)),0,2e-15);
            Assert.InRange(Math.Abs(direction.Y-Math.Sin(angle)),0,2e-15);
            Assert.InRange(Math.Abs(direction.Length-1),0,2e-15);
            var native=ContactFrameArithmetic.Direction(angle,false);
            Assert.Equal(Math.Cos(angle),native.X);Assert.Equal(Math.Sin(angle),native.Y);
            var heading=ContactFrameArithmetic.Heading(direction.Y,direction.X,true);
            Assert.InRange(Math.Abs(BikeAngles.Wrap(heading-angle)),0,2e-15);
        }
        foreach(var x in new[]{-100d,-1,-.001,0,.001,1,100})
        foreach(var y in new[]{-100d,-1,-.001,0,.001,1,100})
            Assert.InRange(Math.Abs(ContactFrameArithmetic.Heading(y,x,true)-Math.Atan2(y,x)),0,2e-15);
    }

    [Theory,InlineData(.05,PhysicalContactSeverity.Brush),InlineData(.5,PhysicalContactSeverity.Disturbed),
        InlineData(.9,PhysicalContactSeverity.LostRhythm),InlineData(1.3,PhysicalContactSeverity.MajorSave),InlineData(2,PhysicalContactSeverity.Crash)]
    public void FiveClassesHavePhysicalVelocityAndBoundedRecovery(double ratio,PhysicalContactSeverity expected)
    {
        foreach(var c in PhysicalContactConsequenceEvidence.Plan(Side,ratio).Riders)
        {
            Assert.Equal(expected,c.Severity);Assert.InRange(c.ControlLoss01,0,.8);
            if(expected==PhysicalContactSeverity.Crash){Assert.Equal(0,c.PostContactSpeed);Assert.Null(c.Recovery);}
            else
            {
                Assert.Equal((float)Math.Max(0,c.PreContactSpeed+c.DeltaForwardMetersPerSecond),c.PostContactSpeed);
                if(expected==PhysicalContactSeverity.Brush)Assert.Null(c.Recovery);
                else{Assert.Equal(1,c.Recovery!.RemainingSteps);Assert.Equal(1-c.ControlLoss01,c.Recovery.DriveAvailability01);}
            }
        }
    }
    public static IEnumerable<object[]> Boundaries=>new[]{.35,.7,1.05,1.55}.SelectMany((b,index)=>new[]{
        new object[]{b*.9,index},new object[]{Math.BitDecrement(b),index},new object[]{b,index+1},
        new object[]{Math.BitIncrement(b),index+1},new object[]{b*1.1,index+1}});
    [Theory,MemberData(nameof(Boundaries))]
    public void AppliedBoundariesAreDeterministicWithoutRandomness(double ratio,int expected)
        =>Assert.All(PhysicalContactConsequenceEvidence.Plan(Side,ratio).Riders,c=>Assert.Equal((PhysicalContactSeverity)expected,c.Severity));

    [Fact]
    public void GentleContactHasNoStoredRecoveryRearImpulseCanAddSpeedAndSideIsMostlyLateral()
    {
        var gentle=PhysicalContactConsequenceEvidence.Plan(Fixtures.Value.Single(f=>f.Name=="A-gentle-parallel"));
        Assert.All(gentle.Riders,c=>{Assert.Equal(PhysicalContactSeverity.Brush,c.Severity);Assert.Null(c.Recovery);});
        var rear=PhysicalContactConsequenceEvidence.Plan(PhysicalContactEvidence.Rear("rear"));
        Assert.Contains(rear.Riders,c=>c.DeltaForwardMetersPerSecond>0&&c.PostContactSpeed>c.PreContactSpeed);
        Assert.Contains(rear.Riders,c=>c.DeltaForwardMetersPerSecond<0);
        Assert.All(PhysicalContactConsequenceEvidence.Plan(Side).Riders,c=>Assert.InRange(Math.Abs(c.DeltaForwardMetersPerSecond),0,.02));
    }

    [Fact]
    public void RecoveryMonotonicallyReducesProductionDriveAndVoluntaryLateralMovementThenClears()
    {
        var engine=new SimulationEngine(new Target(3));
        var steps=new[]{0d,.5,.9,1.3}.Select(ratio=>
        {
            var recovery=PhysicalContactConsequenceEvidence.Plan(Side,ratio).Riders[0].Recovery;
            var snapshot=PhysicalContactConsequenceEvidence.RecoverySnapshot(recovery);
            var step=engine.ResolveProduction(snapshot,engine.Decide(snapshot),Options,legacyContacts:false);
            Assert.Null(step.Changes[0].ContactRecovery);
            return step;
        }).ToArray();
        for(var i=1;i<steps.Length;i++)
        {
            Assert.True(steps[i].Changes[0].Speed<steps[i-1].Changes[0].Speed);
            Assert.True(steps[i].Changes[0].LateralPosition<steps[i-1].Changes[0].LateralPosition);
            Assert.True(steps[i].Changes[0].ElapsedTimeSeconds>steps[i-1].Changes[0].ElapsedTimeSeconds);
        }
        var current=PhysicalContactConsequenceEvidence.RecoverySnapshot(PhysicalContactConsequenceEvidence.Plan(Side,.9).Riders[0].Recovery);
        var rider=current.Rider(1).ToMutableCopy();var surface=new TrackState(1,5,(_,_)=>new(1,0,0));
        var resolved=engine.ResolveProduction(current,engine.Decide(current),Options,legacyContacts:false);
        engine.Commit(resolved,new[]{rider},surface,new SimLog(false));Assert.Null(rider.ContactRecovery);
        var next=engine.CaptureSnapshot(current.Track,surface,new[]{rider},current.Step with{StepNumber=1});
        Assert.Null(next.Rider(1).ContactRecovery);
    }

    [Fact]
    public void RecoveryScalesPositiveForceWithoutScalingResistanceOrPreparation()
    {
        var recovery=PhysicalContactConsequenceEvidence.Plan(Side,1.3).Riders[0].Recovery!;
        var snapshot=PhysicalContactConsequenceEvidence.RecoverySnapshot(recovery);
        var engine=new SimulationEngine(new Target(2));
        var step=engine.ResolveProduction(snapshot,engine.Decide(snapshot),Options,legacyContacts:false);
        var path=step.Diagnostics[0].ExecutedPath!;
        for(var i=0;i<path.Steps.Count;i++)
        {
            var start=path.Nodes[i].SpeedMetersPerSecond;var ds=path.Steps[i].DistanceMeters;
            var force=path.Steps[i].ReferenceDriveForceNewtons;
            Assert.Equal(LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(start,ds,
                force*(float)recovery.DriveAvailability01,snapshot.Rider(1).ActiveSetup),path.Nodes[i+1].SpeedMetersPerSecond);
            var expected=LongitudinalDynamics.CalculateNetDriveAccelerationMetersPerSecondSquared(start,
                force*(float)recovery.DriveAvailability01,snapshot.Rider(1).ActiveSetup);
            Assert.Equal(expected,path.Nodes[i].NetDriveAccelerationMetersPerSecondSquared);
        }
    }

    [Fact]
    public void ContactMaterializesBeforeDiagnosticsAndMotionWithNoFakeTimeOrTeleport()
    {
        foreach(var ratio in new[]{.05,.5,.9,1.3,2d})
        {
            var template=PhysicalContactConsequenceEvidence.RecoverySnapshot();
            var snapshot=new SimulationSnapshot(template.Step,template.Track,template.TrackState,new[]{template.Rider(1),template.Rider(1) with{RiderId=2,Profile=RiderProfile.CreateDefault(2)}});
            var engine=new SimulationEngine(new Target(2));var intents=engine.Decide(snapshot);
            var normal=engine.ResolveProduction(snapshot,intents,Options,legacyContacts:false);
            var plan=PhysicalContactConsequenceEvidence.Plan(Side,ratio);
            var final=engine.ResolveProduction(snapshot,intents,Options,legacyContacts:false,consequencePlan:plan);
            foreach(var change in final.Changes)
            {
                var before=normal.Changes.Single(c=>c.RiderId==change.RiderId);
                Assert.Equal(before.Position,change.Position);Assert.Equal(before.ElapsedTimeSeconds,change.ElapsedTimeSeconds);
                Assert.Equal(before.LateralPosition,change.LateralPosition);Assert.Equal(before.Morale,change.Morale);
                Assert.Equal(change.Speed,final.Motions.Single(m=>m.RiderId==change.RiderId).Final.SpeedMetersPerSecond);
                var d=final.Diagnostics.Single(d=>d.RiderId==change.RiderId);
                Assert.Equal(change.Speed,d.FinalSpeedMetersPerSecond);Assert.Equal(change.Status,d.FinalStatus);
                Assert.Equal(plan.Riders.Single(c=>c.RiderId==change.RiderId),d.PhysicalContactConsequence);
                if(ratio==2){Assert.Equal(RiderRaceStatus.Crashed,change.Status);Assert.Null(change.ContactRecovery);}
            }
            Assert.Equal(2,final.Events.Count(e=>e.PhysicalContactConsequence is not null));
            if(ratio==2)
            {
                var next=new SimulationSnapshot(snapshot.Step with{StepNumber=1},snapshot.Track,snapshot.TrackState,
                    snapshot.Riders.Select(r=>r.Apply(final.Changes.Single(c=>c.RiderId==r.RiderId))));
                Assert.Empty(engine.Decide(next));
            }
        }
    }

    [Fact]
    public void FrozenSeverityDoesNotReadTechniqueStrengthAttackDefenseOrMoraleAgain()
    {
        var template=PhysicalContactConsequenceEvidence.RecoverySnapshot();var analysis=Side.Analysis;
        var normal=new SimulationEngine(new Target(2)).ResolveProduction(template,new[]{new RiderIntent(1,new(2))},Options,legacyContacts:false);
        var directions=new Dictionary<int,MeterPoint>{{1,new(1,0)}};
        PhysicalContactConsequencePlan Plan(SimulationSnapshot snapshot)=>PhysicalContactConsequenceResolver.Build(analysis,analysis.AuditRiders,
            analysis.AuditPairs,snapshot,normal.Changes,directions,new(),new());
        var expected=Json(Plan(template));
        foreach(var rating in new[]{1,20,80,99})
        {
            var profile=RiderProfile.CreateCanonical(1,"frozen",PhysicalContactEvidence.Physiology(1,rating,rating).Profile,
                template.Rider(1).Profile.Skills,template.Rider(1).Profile.Style);
            var changed=new SimulationSnapshot(template.Step,template.Track,template.TrackState,new[]{template.Rider(1) with{Profile=profile,Morale=1}});
            Assert.Equal(expected,Json(Plan(changed)));
        }
    }

    [Theory,InlineData("M-three-squeeze"),InlineData("N-four-frontier"),InlineData("frontier-F1-late-bridge"),InlineData("frontier-F4-simultaneous-disconnected")]
    public void FrontierPlanUsesAggregateVelocityAndIsPairAndRiderOrderInvariant(string name)
    {
        var fixture=Fixtures.Value.Single(f=>f.Name==name);
        var plan=PhysicalContactConsequenceEvidence.Plan(fixture);
        var reversed=fixture with{Inputs=fixture.Inputs.Reverse().ToArray(),Analysis=PhysicalContactAnalyzer.Analyze(fixture.Inputs.Reverse())};
        Assert.Equal(Json(plan),Json(PhysicalContactConsequenceEvidence.Plan(reversed)));
        foreach(var c in plan.Riders)Assert.Equal(fixture.Analysis.AuditRiders.Single(r=>r.RiderId==c.RiderId).NetDeltaVelocityMetersPerSecond,c.NetDeltaVelocityMetersPerSecond);
        Assert.All(plan.AppliedPairs,p=>Assert.Equal(PhysicalContactStatus.Analyzed,p.Status));
        if(name is "M-three-squeeze" or "N-four-frontier")
        {
            Assert.Single(plan.Riders.Select(r=>r.FrontierTimeSeconds).Distinct());
            Assert.All(plan.Riders,r=>Assert.Equal(plan.Riders.Count,r.ContactRiderIds.Count));
        }
        if(name=="M-three-squeeze")
        {
            var middle=plan.Riders.Single(c=>c.RiderId==2);Assert.InRange(middle.NetDeltaVelocityMetersPerSecond.Length,0,1e-9);
            Assert.Equal(PhysicalContactSeverity.MajorSave,middle.Severity);Assert.NotNull(middle.Recovery);
        }
    }

    [Fact]
    public void DeferredUnresolvedAndIneligibleContactsHaveNoAppliedOutcome()
    {
        var first=PhysicalContactEvidence.TimedContact(1,2,0);
        var inputs=new[]{first with{PoseA=null},PhysicalContactEvidence.TimedContact(2,3,.2),
            PhysicalContactEvidence.TimedContact(3,4,.4) with{Contact=PhysicalContactEvidence.TimedContact(3,4,.4).Contact with{NumericallyResolved=false}}};
        var fixture=new PhysicalContactFixture("invalid",Side.Verification,inputs,PhysicalContactAnalyzer.Analyze(inputs));
        Assert.Empty(PhysicalContactConsequenceEvidence.Plan(fixture).Riders);Assert.Empty(PhysicalContactConsequenceEvidence.Plan(fixture).AppliedPairs);
    }

    [Fact]
    public void ConsumedPairSurvivesCloneAndMergeButCertifiedReleaseAllowsNewImpact()
    {
        var tracker=new InteractionEpisodeTracker();var episode=tracker.Engage(new[]{1,2},0,InteractionContext.MechanicalConflict);
        var pair=Side.Analysis.AuditPairs[0] with{EpisodeId=episode.Id};
        var plan=new PhysicalContactConsequencePlan(Array.Empty<RiderContactConsequence>(),new[]{pair},0);
        Assert.True(tracker.CanApplyPhysical(pair));tracker.ConsumePhysical(plan);Assert.False(tracker.CanApplyPhysical(pair));
        var clone=tracker.Clone();Assert.False(clone.CanApplyPhysical(pair));
        var other=tracker.Engage(new[]{3,4},0,InteractionContext.MechanicalConflict);
        var second=pair with{RiderA=3,RiderB=4,EpisodeId=other.Id};Assert.True(tracker.CanApplyPhysical(second));
        var contact=PhysicalContactEvidence.TimedContact(1,2,0).Contact with{Kind=SpaceConflictKind.None,IntervalStartSeconds=1,
            IntervalEndSeconds=2,MinimumSeparationMeters=3,MinimumSeparationLowerBoundMeters=3};
        var release=new InteractionEpisodeTracker();var old=release.Engage(new[]{1,2},0,InteractionContext.MechanicalConflict);
        release.ConsumePhysical(plan with{AppliedPairs=new[]{pair with{EpisodeId=old.Id}}});
        release.ObserveClearance(2,new[]{contact},new());Assert.Empty(release.Active);
        var fresh=release.Engage(new[]{1,2},3,InteractionContext.MechanicalConflict);
        Assert.NotEqual(old.Id,fresh.Id);Assert.True(release.CanApplyPhysical(pair with{EpisodeId=fresh.Id}));
    }

    [Fact]
    public void RecoveryClearsOnCrashFinishRetirementResetAndCannotExistOnBrush()
    {
        var recovery=PhysicalContactConsequenceEvidence.Plan(Side,.9).Riders[0].Recovery!;
        var rider=new RiderState(1,2){ContactRecovery=recovery};rider.IsCrashed=true;Assert.Null(rider.ContactRecovery);
        rider.IsCrashed=false;rider.ContactRecovery=recovery;rider.Retire();Assert.Null(rider.ContactRecovery);
        rider.ContactRecovery=recovery;rider.ResetForHeat(2);Assert.Null(rider.ContactRecovery);
        rider.ContactRecovery=recovery;rider.SetStatus(RiderRaceStatus.Finished);Assert.Null(rider.ContactRecovery);
        Assert.Throws<ArgumentOutOfRangeException>(()=>new ContactRecoveryState(null,0,.1,PhysicalContactSeverity.Brush,.01));
    }

    [Theory,InlineData(PhysicalContactDiagnosticsLevel.None),InlineData(PhysicalContactDiagnosticsLevel.Summary),InlineData(PhysicalContactDiagnosticsLevel.FullAudit)]
    public void GameplayComputesPhysicsWithoutRequiringDiagnosticsAndNeverInvokesLegacyFallback(PhysicalContactDiagnosticsLevel level)
    {
        var s=ContestedSpaceResponseEvidence.Scenarios().Single(s=>s.Name=="imminent-overlap");
        var snapshot=ContestedSpaceResponseEvidence.Snapshot(s);var engine=new SimulationEngine(new Target(2));
        ResolvedSimulationStep Run(PhysicalContactDiagnosticsLevel diagnostics)=>engine.Resolve(snapshot,engine.Decide(snapshot),
            Options with{PhysicalContactDiagnostics=diagnostics});
        var actual=Run(level);var audit=Run(PhysicalContactDiagnosticsLevel.FullAudit);
        Assert.Equal(Json(audit.Changes),Json(actual.Changes));Assert.Equal(Json(audit.Motions),Json(actual.Motions));
        Assert.Equal(Json(audit.Interaction!.PhysicalContactConsequences),Json(actual.Interaction!.PhysicalContactConsequences));
        Assert.Equal(0,actual.Interaction.Work.LegacyFallbackAttempts);Assert.All(actual.Interaction.Episodes,e=>Assert.False(e.LegacyFallbackUsed));
        Assert.NotEmpty(actual.Interaction.PhysicalContactConsequences!.Riders);
        Assert.All(actual.Events.Where(e=>e.Type!=SimulationEventType.SegmentResolved),e=>Assert.NotNull(e.PhysicalContactConsequence));
    }

    [Fact]
    public void FeatureFlagDefaultsOffAndRequiresContestedSpaceResponses()
    {
        Assert.False(new HeatSimulationOptions().EnablePhysicalContactConsequences);
        Assert.Throws<InvalidOperationException>(()=>(new HeatSimulationOptions{EnablePhysicalContactConsequences=true}).Validate());
    }

    [Fact]
    public void ContactHeavyHeatFixtureCompletesWithCanonicalPlannerPositionsInBothModes()
    {
        var scenario=PhysicalContactConsequenceEvidence.HeatScenarios().Single(s=>s.Id=="contact-heavy");
        foreach(var enabled in new[]{false,true})
        {
            var result=new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track,scenario.CreateSurface(),
                scenario.Riders.Select(r=>r.Create(scenario.Track)).ToList(),Options with{Seed=7,IncidentFrequency=1,
                    EnablePhysicalContactConsequences=enabled,PhysicalContactDiagnostics=enabled
                        ? PhysicalContactDiagnosticsLevel.FullAudit : PhysicalContactDiagnosticsLevel.None},59);
            Assert.Equal(4,result.Classification.Count);
            Assert.All(result.Classification,r=>Assert.True(r.Status is RiderRaceStatus.Finished or RiderRaceStatus.Crashed));
        }
    }

    [Fact]
    public void ContinuousOverlapsAreSuppressedAfterCommitAndIndependentPairsAreBothApplied()
    {
        var scenario=ContestedSpaceResponseEvidence.OwnershipScenarios().Single(s=>s.Name=="two-disjoint-unresolved");
        var snapshot=ContestedSpaceResponseEvidence.Snapshot(scenario);var tracker=new InteractionEpisodeTracker();
        var engine=new SimulationEngine(new Target(1));
        var intents=scenario.Riders.Select(r=>new RiderIntent(r.Id,new(r.Intent.TargetFor(snapshot.Segment.Type)){Trajectory=r.Intent})).ToArray();
        var first=engine.Resolve(snapshot,intents,Options,tracker);
        Assert.Equal(2,first.Interaction!.PhysicalContactConsequences!.AppliedPairs.Count);
        Assert.Equal(4,first.Interaction.PhysicalContactConsequences.Riders.Count);
        Assert.Empty(tracker.Active); // Resolve is pure; ownership changes only on Commit.
        engine.Commit(first,snapshot.Riders.Select(r=>r.ToMutableCopy()).ToArray(),
            new TrackState(snapshot.Track.Segments.Count,5,snapshot.TrackState.GetSurface),new SimLog(false));
        var repeated=new SimulationSnapshot(snapshot.Step with{StepNumber=10},snapshot.Track,snapshot.TrackState,
            snapshot.Riders.Select(r=>r with{ElapsedTimeSeconds=5}));
        var next=engine.Resolve(repeated,intents,Options,tracker);
        Assert.Empty(next.Interaction!.PhysicalContactConsequences!.Riders);
        Assert.Equal(2,next.Interaction.PhysicalContactConsequences.RepeatedOverlapSuppressions);
        Assert.DoesNotContain(next.Events,e=>e.PhysicalContactConsequence is not null);
    }

    [Fact]
    public void PreviouslyConsumedPairDoesNotContributeASecondImpulseOrPromoteDeferredContacts()
    {
        var inputs=new[]{PhysicalContactEvidence.TimedContact(1,2,0),PhysicalContactEvidence.TimedContact(2,3,.02),
            PhysicalContactEvidence.TimedContact(3,4,.2)};
        var analysis=PhysicalContactAnalyzer.AnalyzeForConsequences(inputs,new(),p=>p.RiderA!=1);
        Assert.Equal(PhysicalContactStatus.DeferredByEarlierContact,analysis.AuditPairs.Single(p=>p.RiderB==4).Status);
        Assert.DoesNotContain(analysis.ApplicationRiders,r=>r.RiderId==1||r.RiderId==4);
        var onlyFresh=PhysicalContactAnalyzer.Analyze(new[]{inputs[1]});
        Assert.Equal(Json(onlyFresh.AuditRiders),Json(analysis.ApplicationRiders));
        Assert.Equal(3,analysis.AuditRiders.Count); // The full shadow aggregate is still available.
    }

    [Fact]
    public void PhysicalConsumptionSurvivesVerifiedEpisodeBridge()
    {
        var tracker=new InteractionEpisodeTracker();var a=tracker.Engage(new[]{1,2},0,InteractionContext.InsideOverlap);
        var b=tracker.Engage(new[]{3,4},0,InteractionContext.InsideOverlap);
        var pair=Side.Analysis.AuditPairs[0] with{RiderA=3,RiderB=4,EpisodeId=b.Id};
        tracker.ConsumePhysical(new(Array.Empty<RiderContactConsequence>(),new[]{pair},0));
        var scenario=ContestedSpaceResponseEvidence.OwnershipScenarios().Single(s=>s.Name=="real-bridge");
        var snapshot=ContestedSpaceResponseEvidence.Snapshot(scenario);var engine=new SimulationEngine(new Target(1));
        var raw=engine.ResolveProduction(snapshot,engine.Decide(snapshot),Options,legacyContacts:false);
        var poses=raw.Motions.SelectMany(m=>ResolvedBikePoses.FromMotion(m,snapshot.Track)).ToArray();
        var edges=ContestedSpaceInteractionCoordinator.Contacts(CommonTimePoseHistory.Observe(poses),poses,
            snapshot.Riders.ToDictionary(r=>r.RiderId,r=>(double)r.ElapsedTimeSeconds));
        var component=Assert.Single(ContestedSpaceInteractionCoordinator.Clusters(edges,snapshot,new()));
        var merged=tracker.Reconcile(component,0);Assert.Equal(a.Id,merged.Id);
        Assert.False(tracker.CanApplyPhysical(pair with{EpisodeId=merged.Id}));
        Assert.True(tracker.CanApplyPhysical(pair with{RiderA=2,RiderB=3,EpisodeId=merged.Id}));
    }

    [Fact]
    public void ANewGenuineContactKeepsTheStrongerBoundedRecoveryRatherThanMultiplyingPenalties()
    {
        var recovery=PhysicalContactConsequenceEvidence.Plan(Side,1.3).Riders[0].Recovery!;
        var snapshot=PhysicalContactConsequenceEvidence.RecoverySnapshot(recovery);var engine=new SimulationEngine(new Target(2));
        var normal=engine.ResolveProduction(snapshot,engine.Decide(snapshot),Options,legacyContacts:false);
        var analysis=Side.Analysis;
        var plan=PhysicalContactConsequenceResolver.Build(analysis,analysis.AuditRiders.Select(r=>r with{SeverityRatio=.5}).ToArray(),
            analysis.AuditPairs,snapshot,normal.Changes,new Dictionary<int,MeterPoint>{{1,new(1,0)}},new(),new());
        var next=Assert.Single(plan.Riders);Assert.Equal(recovery,next.Recovery);
        Assert.Equal((float)Math.Max(0,next.PreContactSpeed+next.DeltaForwardMetersPerSecond),next.PostContactSpeed);
        Assert.InRange(next.Recovery!.ControlLoss01,0,.8);
    }

    [Theory,InlineData(.5),InlineData(.9),InlineData(1.3)]
    public void RecoveryHasIdenticalRichAndLeanProductionProjectionAndExpiresAfterFirstPrefix(double ratio)
    {
        var recovery=PhysicalContactConsequenceEvidence.Plan(Side,ratio).Riders[0].Recovery!;
        var snapshot=PhysicalContactConsequenceEvidence.RecoverySnapshot(recovery);
        var evaluator=new TrajectoryEvaluator(new RiderDecisionContext(snapshot,snapshot.Rider(1)));
        var rich=evaluator.Evaluate(new(3,3,3),true);var lean=evaluator.Evaluate(new(3,3,3));
        Assert.Equal(Json(rich with{ResolvedMotions=Array.Empty<ResolvedRiderMotion>()}),Json(lean));
        var engine=new SimulationEngine(new Target(3));var first=engine.ResolveProduction(snapshot,engine.Decide(snapshot),Options,legacyContacts:false);
        Assert.Equal(Json(first.Motions[0]),Json(rich.ResolvedMotions[0]));Assert.Null(first.Changes[0].ContactRecovery);
    }

    [Fact]
    public void FullHeatDoesNotReanalyzeHistoricalContactsFromClosedEpisodes()
    {
        var scenario=FourRiderBehaviorSuite.CreateScenarios().Single(s=>s.Id=="I");
        var riders=scenario.Riders.Select(r=>r.Create(scenario.Track)).ToList();
        var result=new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track,scenario.CreateSurface(),riders,
            Options with{Seed=7,IncidentFrequency=1},59);
        Assert.Equal(4,result.Classification.Count);
        Assert.All(riders.Where(r=>!r.IsCrashed),r=>Assert.Equal(RiderRaceStatus.Finished,r.Status));
        Assert.All(riders,r=>Assert.Null(r.ContactRecovery));
    }

    [Theory,InlineData("M-three-squeeze"),InlineData("N-four-frontier"),InlineData("frontier-F1-late-bridge")]
    public void EquivalentIdRelabelingAndPairReversalPreservePhysicalOutcomes(string name)
    {
        foreach(var deterministic in new[]{false,true})
        {
        var original=Fixtures.Value.Single(f=>f.Name==name);
        var inputs=original.Inputs.Select(i=>i with{PoseA=i.PoseA is null?null:i.PoseA with{DeterministicArithmetic=deterministic},
            PoseB=i.PoseB is null?null:i.PoseB with{DeterministicArithmetic=deterministic}}).ToArray();
        var fixture=original with{Inputs=inputs,Analysis=PhysicalContactAnalyzer.Analyze(inputs)};
        var expected=PhysicalContactConsequenceEvidence.Plan(fixture);
        int Id(int id)=>101-id;
        PhysicalBikePose? Pose(PhysicalBikePose? p)=>p is null?null:new(Id(p.RiderId),p.FrameId,p.Position,p.Attitude,p.Dimensions,p.ReferenceTangentHeadingRadians,p.Source)
            {DeterministicArithmetic=deterministic};
        var renamed=fixture.Inputs.Select(i=>i with{Contact=i.Contact with{RiderA=Id(i.Contact.RiderA),RiderB=Id(i.Contact.RiderB)},
            RiderA=i.RiderA with{RiderId=Id(i.RiderA.RiderId)},RiderB=i.RiderB with{RiderId=Id(i.RiderB.RiderId)},PoseA=Pose(i.PoseA),PoseB=Pose(i.PoseB)}).Reverse().ToArray();
        var actual=PhysicalContactConsequenceEvidence.Plan(fixture with{Inputs=renamed,Analysis=PhysicalContactAnalyzer.Analyze(renamed)});
        foreach(var c in expected.Riders)
        {
            var other=actual.Riders.Single(r=>r.RiderId==Id(c.RiderId));
            Assert.Equal(c.Severity,other.Severity);Assert.Equal(c.SeverityRatio,other.SeverityRatio,12);
            Assert.Equal(c.PostContactSpeed,other.PostContactSpeed);Assert.Equal(c.ControlLoss01,other.ControlLoss01,12);
            Assert.Equal(c.NetDeltaVelocityMetersPerSecond,other.NetDeltaVelocityMetersPerSecond);
        }
        var reversed=fixture.Inputs.Select(i=>
        {
            var c=i.Contact;var v=c.Contributions;
            return i with{Contact=c with{RiderA=c.RiderB,RiderB=c.RiderA,ComponentA=c.ComponentB,ComponentB=c.ComponentA,SourceA=c.SourceB,SourceB=c.SourceA,
                RelativePositionAtOnsetMeters=c.RelativePositionAtOnsetMeters*-1,RelativeVelocityAtOnsetMetersPerSecond=c.RelativeVelocityAtOnsetMetersPerSecond*-1,
                Contributions=new(v.BTranslationMeters,v.BRotationMeters,v.ATranslationMeters,v.ARotationMeters,v.ActualClosingMeters,v.NonadditiveResidualMeters)},
                PoseA=i.PoseB,PoseB=i.PoseA,VelocityA=i.VelocityB,VelocityB=i.VelocityA,RiderA=i.RiderB,RiderB=i.RiderA};
        }).Reverse().ToArray();
        Assert.Equal(Json(expected),Json(PhysicalContactConsequenceEvidence.Plan(fixture with{Inputs=reversed,Analysis=PhysicalContactAnalyzer.Analyze(reversed)})));
        }
    }
}
