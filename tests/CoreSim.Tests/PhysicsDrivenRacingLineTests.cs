using System.Numerics;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class PhysicsDrivenRacingLineTests
{
    private static TrackFrame Frame()=>new(CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
    private static RiderView Pose(TrackFrame frame,double reference,double offset,double angle=0)
    {
        var p=frame.At(reference,offset);
        return new(1,p.Position,p.Road.Tangent*18,(float)(p.Road.Heading+angle),0,p.Road,reference,0,RiderActivity.Racing,1,5,0);
    }
    [Fact]
    public void PhysicalEdgesAreNotTargetsAndCapsulesNeedOrientationDependentClearance()
    {
        var f=Frame();var g=new RacingLineGeometry(f);
        Assert.Equal(10,f.MinimumUsableWidthMeters);Assert.Equal(14.6f,LaneModel.UsableRacingWidthMeters(SegmentType.TurnEntry,f.Track.Geometry),4);
        var straight=Pose(f,15,9.5);var arc=Pose(f,65,9.5);
        Assert.InRange(g.Clearance(straight).Outer,1.099,1.101);
        Assert.InRange(g.Clearance(arc).Outer,5.68,5.72);
        Assert.True(g.Clearance(Pose(f,15,9.5,1)).Outer<g.Clearance(straight).Outer-.3);
        Assert.True(g.Clearance(Pose(f,65,15.5)).Outer<0);
        Assert.Throws<ArgumentException>(()=>PhysicsDrivenRacingLine.Compile(new("invalid",5,14,5,5),f));
    }
    [Fact]
    public void WidthStepMustBeCheckedAgainstTheWholeBikeBeforeItsCenterEntersTheStraight()
    {
        var f=Frame();var g=new RacingLineGeometry(f);
        var end=35+f.Track.Geometry.InnerRadiusMeters*f.Track.Geometry.TurnSegmentAngleRadians*3;
        Assert.True(g.Clearance(Pose(f,end-3,14)).Outer>0);
        Assert.True(g.Clearance(Pose(f,end-.2,14)).Outer<0);
    }
    [Fact]
    public void LandmarkCommandsUseAcceptedCrossingsWithoutTeleportOrAnExtraStep()
    {
        var scenario=new Scenario("prefix",new[]{SlipDynamicsAudit.Rider()},2);
        var baseline=SlipControlValidation.Execute(scenario);
        var changed=SlipControlValidation.Execute(scenario,spatialControls:new[]{new SpatialControlCommand(1,10,3,1)});
        var command=Assert.Single(changed.Result.Decisions);var activation=command.ActualActivation!.Value;
        var step=changed.Ticks.First(t=>t.TimeSeconds==activation);
        Assert.True(step.Before.TotalReferenceMeters>=10);
        Assert.True(changed.Ticks.Last(t=>t.TimeSeconds<activation).Before.TotalReferenceMeters<10);
        Assert.Equal(SlipControlValidation.PrefixFingerprint(baseline,activation),SlipControlValidation.PrefixFingerprint(changed,activation));
        Assert.True(changed.Result.Work.MaximumStepDisplacementMeters<.3);
        var hold=SlipControlValidation.Execute(scenario,spatialControls:new[]{new SpatialControlCommand(1,10,5,1)});
        Assert.Equal(baseline.Result.Fingerprint,hold.Result.Fingerprint);
        Assert.Equal(baseline.Result.Work.SolverCalls,hold.Result.Work.SolverCalls);
    }
    [Fact]
    public void SpatialPlansRequireIsolatedSlipExecutionAndValidFiniteUniqueLandmarks()
    {
        var riders=new[]{SlipDynamicsAudit.Rider()};var p=SlipControlValidation.Parameters();
        Assert.Throws<ArgumentException>(()=>new WorldRunner(riders,spatialControls:new[]{new SpatialControlCommand(1,0,5,1)}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(riders,parameters:p,spatialControls:new[]{new SpatialControlCommand(1,double.NaN,5,1)}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(riders,parameters:p,spatialControls:new[]{new SpatialControlCommand(1,0,5,1),new(1,0,3,1)}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(riders,parameters:p,controls:new[]{new ControlCommand(1,0,5,1)},spatialControls:new[]{new SpatialControlCommand(1,0,3,1)}));
    }
    [Fact]
    public void SpatialSchedulingIsIndependentOfRiderOrderAndDoesNotBlockOtherRiders()
    {
        var riders=new[]{SlipDynamicsAudit.Rider(3,0,18,1),SlipDynamicsAudit.Rider(7,8,18,2)};
        var commands=new[]{new SpatialControlCommand(1,30,3,1),new SpatialControlCommand(2,10,7,.7f)};
        RunResult Execute(IEnumerable<RiderSpec> r){using var w=new WorldRunner(r,parameters:SlipControlValidation.Parameters(),spatialControls:commands);return w.Run("order",.01,2);}
        var a=Execute(riders);var b=Execute(riders.Reverse());
        Assert.Equal(a.Fingerprint,b.Fingerprint);Assert.Equal(a.Decisions,b.Decisions);
        Assert.Equal(2,a.Decisions[0].RiderId);
    }
    [Fact]
    public void OfflineSelectionUsesMeasuredTimeRejectsUnsafeAndReplaysDeterministically()
    {
        var condition=PhysicsDrivenRacingLineAudit.Conditions()[0];var plans=PhysicsDrivenRacingLine.Plans();
        var a=PhysicsDrivenRacingLineAudit.Evaluate(condition,plans[0]);var b=PhysicsDrivenRacingLineAudit.Evaluate(condition,plans[2]);
        Assert.Equal(a.Evaluation.InitialFingerprint,b.Evaluation.InitialFingerprint);
        var rows=new[]{a.Evaluation,b.Evaluation};var expected=rows.Where(r=>r.Feasible).Min(r=>r.FlyingSeconds);
        var winner=PhysicsDrivenRacingLine.Select(rows)!;Assert.Equal(expected,winner.FlyingSeconds);
        var renamed=rows.Select(r=>r with{Plan=r.Plan with{Name=r==winner?"zzzz":"aaaa"}}).Reverse();
        Assert.Equal(winner.Plan.NumericKey,PhysicsDrivenRacingLine.Select(renamed)!.Plan.NumericKey);
        var unsafeFast=winner with{Feasible=false,Rejections=new[]{"CapsuleClearance"},FlyingSeconds=0};
        Assert.Equal(winner.Fingerprint,PhysicsDrivenRacingLine.Select(new[]{unsafeFast,winner})!.Fingerprint);
        Assert.Null(PhysicsDrivenRacingLine.Select(new[]{unsafeFast}));
        var repeat=PhysicsDrivenRacingLineAudit.Evaluate(condition,winner.Plan);
        Assert.Equal(winner.Fingerprint,repeat.Evaluation.Fingerprint);Assert.Equal(winner.LapSeconds,repeat.Evaluation.LapSeconds);
        Assert.True(Math.Abs(winner.BalanceResidualJoules)<5);
        var unsafePhysical=PhysicsDrivenRacingLineAudit.Evaluate(condition,new("unsafe-fixture",.5f,.5f,.5f,.5f)).Evaluation;
        Assert.False(unsafePhysical.Feasible);Assert.Contains("CapsuleClearance",unsafePhysical.Rejections);
        Assert.Null(PhysicsDrivenRacingLine.Select(new[]{unsafePhysical}));
    }
}
