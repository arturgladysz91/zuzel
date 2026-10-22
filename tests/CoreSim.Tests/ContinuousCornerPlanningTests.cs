using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class ContinuousCornerPlanningTests
{
    [Fact]
    public void DefaultOffRequiresPreparedCAndDoesNotAlterItsSerializedParameters()
    {
        Assert.False(new WorldParameters().EnableContinuousCornerPlanning);
        Assert.DoesNotContain("ContinuousCorner",JsonSerializer.Serialize(PreparedCornerAudit.Parameters(),InverseFeasibilityAudit.Json));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{CornerEntryAudit.Rider()},parameters:CornerEntryAudit.Parameters() with{EnableContinuousCornerPlanning=true}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{CornerEntryAudit.Rider()},parameters:ContinuousCornerAudit.Parameters(),preparedOptions:new(Path:false)));
    }
    [Fact]
    public void FullFootprintMustClearExitAndReferenceMustAlreadyHaveTraversedBend()
    {
        var frame=RacingLineRootCauseAudit.Frame();var exit=frame.SegmentStartMeters(4);var basis=PreparedCornerAudit.InitialView();
        Assert.False(ContinuousCornerPlanning.Cleared(basis,frame,exit));
        foreach(var delta in new[]{.4,1.1}){var at=frame.At(exit+delta,5);var v=basis with{Position=at.Position,Heading=(float)at.Road.Heading,Road=at.Road,TotalReferenceMeters=exit+delta};
            Assert.Equal(delta>1.05,ContinuousCornerPlanning.Cleared(v,frame,exit));}
    }
    [Fact]
    public void SavedCPreparesNextBendWithoutResettingMemoryOrPendingCommand()
    {
        var s=ContinuousCornerAudit.SavedEnd();var sequence=new ContinuousCornerPlanning(s.Old);var frame=s.Old.Path.Frame;
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var omega=s.Wheel.AngularVelocityRadPerSecond;var steer=s.Steering.SteeringRadians;
        var pending=s.Pending;var memory=s.Old.ControlMemory;
        var prepared=sequence.Advance(s.Rider,CornerEntryAudit.Rider(),frame,surface,ContinuousCornerAudit.Parameters(),s.Time,147,s.Wheel,s.Steering,pending)!;
        Assert.Equal("prepared",prepared.Phase);Assert.Same(s.Old,sequence.Active);Assert.Equal(memory,sequence.Waiting!.ControlMemory);
        Assert.Equal(s.Time+.24,prepared.Activation,9);Assert.Equal(frame.SegmentStartMeters(5),prepared.BendStart,8);Assert.Equal(frame.SegmentStartMeters(8),prepared.BendExit,8);
        Assert.Null(sequence.Advance(s.Rider,CornerEntryAudit.Rider(),frame,surface,ContinuousCornerAudit.Parameters(),s.Time+.239,147,s.Wheel,s.Steering,pending));
        // The active #122 policy can make a further review while the new path waits.
        var latest=new PreparedControlMemory(.37f,s.Time+.29);s.Old.CarryControlMemory(latest);
        var activated=sequence.Advance(s.Rider,CornerEntryAudit.Rider(),frame,surface,ContinuousCornerAudit.Parameters(),s.Time+.24,147,s.Wheel,s.Steering,pending)!;
        Assert.Equal("activated",activated.Phase);Assert.Equal(latest,sequence.Active.ControlMemory);Assert.Null(sequence.Waiting);Assert.Equal(2,sequence.Generation);
        Assert.Same(pending,activated.Pending);Assert.Null(pending!.ActualActivation);Assert.Null(pending.CancelledAt);
        Assert.Equal(omega,s.Wheel.AngularVelocityRadPerSecond);Assert.Equal(steer,s.Steering.SteeringRadians);Assert.Equal(s.Rider,activated.Rider);
    }
    [Theory]
    [InlineData(30)] [InlineData(80)]
    public void ConnectedOffsetMatchesNonzeroJetAndOldXyAtSplice(double splice)
    {
        var prefix=PreparedCornerAudit.InitialPlan().Path;var connected=AuthoredRacingPath.Connected(prefix,splice,splice+60,5);
        var old=prefix.At(splice);var next=connected.At(splice);var after=connected.At(splice+1e-6);
        Assert.Equal(old,next);Assert.Equal(prefix.At(splice-2),connected.At(splice-2));
        Assert.InRange(Math.Abs(old.Heading-after.Heading),0,1e-6);Assert.InRange(Math.Abs(old.Curvature-after.Curvature),0,1e-7);
        var end=connected.Offset(splice+60);Assert.Equal(5,end.D);Assert.Equal(0,end.First);Assert.Equal(0,end.Second);
        var almost=connected.Offset(splice+60-1e-5);Assert.InRange(Math.Abs(almost.First),0,1e-9);Assert.InRange(Math.Abs(almost.Second),0,1e-8);
    }
    [Fact]
    public void NextBendWrapsLapWithoutCoordinatesOrTimeSchedule()
    {
        var frame=RacingLineRootCauseAudit.Frame();var p=ContinuousCornerAudit.Parameters();var spec=CornerEntryAudit.Rider();
        var at=frame.At(frame.SegmentStartMeters(5)+1,5);var basis=PreparedCornerAudit.InitialView();
        var oldView=basis with{Position=at.Position,Road=at.Road,Heading=(float)at.Road.Heading,TotalReferenceMeters=frame.SegmentStartMeters(5)+1,Velocity=at.Road.Tangent*15};
        var old=new PreparedCornerPlan(oldView,spec,frame,p,50,147,new(15),new());
        var done=frame.At(old.BendExitMeters+1.2,5);var v=oldView with{Position=done.Position,Road=done.Road,Heading=(float)done.Road.Heading,TotalReferenceMeters=old.BendExitMeters+1.2,Velocity=done.Road.Tangent*15};
        var sequence=new ContinuousCornerPlanning(old);
        var change=sequence.Advance(v,spec,frame,TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot(),p,1234,147,new(15),new(),null)!;
        Assert.Equal(frame.LapMeters+frame.SegmentStartMeters(1),change.BendStart,8);Assert.Equal(frame.LapMeters+frame.SegmentStartMeters(4),change.BendExit,8);
        Assert.Equal(1234.24,change.Activation,8);
    }
    [Fact]
    public void FullRecordedTwoBendStreamReplaysContactsPlansAndPolicyWithoutAnotherWorld()
    {
        var directory=PhysicalSteeringAudit.PreviousPath("../continuous-corner-planning");
        var rows=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous");var plans=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous-plan");
        var reviews=JsonSerializer.Deserialize<PreparedCornerReview[]>(File.ReadAllText(Path.Combine(directory,"reviews.json")))!;
        var changes=JsonSerializer.Deserialize<CornerPlanChange[]>(File.ReadAllText(Path.Combine(directory,"changes.json")),InverseFeasibilityAudit.Json)!;
        var sequence=new ContinuousCornerPlanning(PreparedCornerAudit.InitialPlan());var frame=sequence.Active.Path.Frame;var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        var p=ContinuousCornerAudit.Parameters();var ri=0;var ci=0;
        for(var i=0;i<rows.Length;i++){
            var r=rows[i];var v=PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=(float)plans[i]["target"]};
            var wheel=new RearWheelState(r["omega_before"]*.33);var steering=new TwoContactBikeState((float)r["steer_before"]);
            if(ci<changes.Length&&Math.Abs(r["t"]-changes[ci].Time)<1e-8){
                var expected=changes[ci++];var actual=sequence.Advance(v,CornerEntryAudit.Rider(),frame,surface,p,expected.Time,147,wheel,steering,expected.Pending)!;
                Assert.Equal(expected.Phase,actual.Phase);Assert.Equal(expected.Memory,actual.Memory);Assert.Equal(expected.Splice,actual.Splice);Assert.Equal(expected.Offset,actual.Offset);Assert.Equal(expected.Pending,actual.Pending);
            }
            if(ri<reviews.Length&&Math.Abs(r["t"]-reviews[ri].Time)<1e-8){
                var expected=reviews[ri++];var actual=sequence.Active.Observe(v,CornerEntryAudit.Rider(),frame,surface,p,expected.Time,147,52.72097f,wheel,steering);
                Assert.Equal(expected.Drive,actual.Drive);Assert.Equal(expected.SpeedTarget,actual.SpeedTarget);Assert.Equal(expected.Activation,actual.Activation);
            }
            var f=TwoContactBikeDynamics.Forces(v,CornerEntryAudit.Rider(),frame,surface,p,r["t"],147,wheel,steering,(float)r["dt"],sequence.Active.Guidance(v,p,r["t"]),inertia:52.72097f);
            Assert.Equal(r["front_fx"],f.FrontForce.X);Assert.Equal(r["front_fy"],f.FrontForce.Y);Assert.Equal(r["rear_fx"],f.RearForce.X);Assert.Equal(r["rear_fy"],f.RearForce.Y);Assert.Equal(r["steer_after"],f.SteeringAfter);
            var q=sequence.Active.Path.At(v.TotalReferenceMeters);Assert.Equal(plans[i]["path_x"],q.Position.X);Assert.Equal(plans[i]["path_y"],q.Position.Y);Assert.Equal(plans[i]["path_k"],q.Curvature);
            Assert.Equal(r["omega_before"]*.33,wheel.AngularVelocityRadPerSecond*.33,12);Assert.Equal(r["steer_before"],steering.SteeringRadians);
        }
        Assert.Equal(reviews.Length,ri);Assert.Equal(changes.Length,ci);
        using var prefix=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"prefix.json")));
        Assert.True(prefix.RootElement.GetProperty("Equal").GetBoolean());Assert.Equal("A44925BFD8741A5E6ECA89EB9D635EFF27BC0AC1857227EB5F55D718F24E62A9",prefix.RootElement.GetProperty("Actual").GetString());
    }
}
