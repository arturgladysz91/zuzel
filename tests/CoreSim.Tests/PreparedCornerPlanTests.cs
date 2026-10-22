using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class PreparedCornerPlanTests
{
    private static readonly string DirectoryPath=Path.GetFullPath(PhysicalSteeringAudit.PreviousPath("../feasible-path-speed-planning"));
    [Fact]
    public void DefaultOffAndConflictingSourcesRejectedBeforeAnyWorld()
    {
        Assert.False(new WorldParameters().EnablePreparedCornerPlan);
        Assert.DoesNotContain("PreparedCornerPlan",JsonSerializer.Serialize(CornerEntryAudit.Parameters(),InverseFeasibilityAudit.Json));
        var spec=CornerEntryAudit.Rider();var p=PreparedCornerAudit.Parameters();
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:p with{EnableReachableTravelDirectionSteering=true}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:p with{EnableFeasibleCornerEntry=true}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:p,controls:new[]{new ControlCommand(1,0,5,.15f)}));
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{spec},parameters:CornerEntryAudit.Parameters(),preparedOptions:new()));
    }
    [Fact]
    public void QuinticHasContinuousPositionTangentAndCurvatureAtBothKnots()
    {
        var plan=PreparedCornerAudit.InitialPlan();
        foreach(var s in new[]{plan.TransitionStartMeters,plan.TransitionEndMeters}){
            var before=plan.Path.At(s-1e-5);var at=plan.Path.At(s);var after=plan.Path.At(s+1e-5);
            Assert.InRange(Math.Abs(before.OffsetMeters-after.OffsetMeters),0,1e-8);
            Assert.InRange(Math.Abs(before.Heading-after.Heading),0,1e-6);
            Assert.InRange(Math.Abs(before.Curvature-after.Curvature),0,1e-7);
            Assert.InRange(Math.Abs(at.OffsetFirst),0,1e-12);Assert.InRange(Math.Abs(at.OffsetSecond),0,1e-12);
        }
        var start=plan.Path.At(plan.TransitionStartMeters);var end=plan.Path.At(plan.TransitionEndMeters);
        Assert.Equal(1,start.OffsetMeters);Assert.Equal(plan.SelectedOffsetMeters,end.OffsetMeters);
    }
    [Theory]
    [InlineData(22)] [InlineData(38)] [InlineData(50)] [InlineData(75)] [InlineData(120)]
    public void CurvatureUsesActualXyDerivativesRatherThanConstantLane(double s)
    {
        var path=PreparedCornerAudit.InitialPlan().Path;const double h=.005;
        var a=path.At(s-h);var b=path.At(s);var c=path.At(s+h);var first=(c.Position-a.Position)*(1/(2*h));var second=(c.Position-b.Position*2+a.Position)*(1/(h*h));
        var numerical=CoreSim.PhysicalSpace.MeterPoint.Cross(first,second)/Math.Pow(first.Length,3);
        Assert.InRange(Math.Abs(numerical-b.Curvature),0,1e-6);
        if(s==22){var k=path.Frame.Shape!.At(s).Curvature;Assert.Equal(0,k);Assert.True(Math.Abs(b.Curvature)>.001);}
        Assert.True(double.IsFinite(path.CurvatureDerivative(s)));
    }
    [Fact]
    public void PreparedFootprintHasReserveAcrossBothBendsAndControlledAttitudes()
    {
        var plan=PreparedCornerAudit.InitialPlan();var geometry=new RacingLineGeometry(plan.Path.Frame);
        for(var s=4d;s<plan.Path.Frame.LapMeters;s+=.5){var q=plan.Path.At(s);
            foreach(var beta in new[]{0,-Math.PI/4,Math.PI/4}){
                var v=PreparedCornerPlan.Pose(PreparedCornerAudit.InitialView(),plan.Path.Frame,q) with{Heading=(float)(q.Heading+beta)};
                var gap=geometry.Clearance(v);Assert.True(gap.Inner>.5&&gap.Outer>.5,$"s={s:R},beta={beta:R}");
            }
        }
    }
    [Fact]
    public void CoastingRetainsStoredWheelAndPendingDriveEnergyAndNeverWritesState()
    {
        var v=PreparedCornerAudit.InitialView();var wheel=new RearWheelState(24);var omega=wheel.AngularVelocityRadPerSecond;
        var now=PreparedCornerPlan.Coast(v,wheel,0,0,147);var future=PreparedCornerPlan.Coast(v,wheel,0,30,147);
        var pending=PreparedCornerPlan.Coast(v,wheel,5000,30,147);
        Assert.Equal(v.Speed,now.Estimate);Assert.True(now.Upper>v.Speed);
        Assert.True(future.Estimate<now.Estimate);Assert.True(future.Upper>future.Estimate);Assert.True(pending.Upper>future.Upper);
        Assert.Equal(omega,wheel.AngularVelocityRadPerSecond);Assert.Equal(18,v.Speed);
        Assert.Throws<ArgumentOutOfRangeException>(()=>PreparedCornerPlan.Coast(v,wheel,0,-1,147));
    }
    [Fact]
    public void ProfileDistinguishesImmediateResponseFromAbsoluteCapacityAndDoesNotCertifyCoasting()
    {
        var plan=PreparedCornerAudit.InitialPlan();var v=PreparedCornerAudit.InitialView();var frame=plan.Path.Frame;var p=PreparedCornerAudit.Parameters();var wheel=new RearWheelState(18);var steer=new TwoContactBikeState();
        var d=plan.Observe(v,CornerEntryAudit.Rider(),frame,TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot(),p,0,147,52.72097f,wheel,steer);
        Assert.Equal(13,d.ContactQueries);Assert.True(d.ImmediateNormal<d.HeldSteeringNormal);Assert.True(d.HeldSteeringNormal<d.ContactCeiling);
        Assert.True(d.ResponseSeconds>1);Assert.True(d.NoCertifiedCoastSolution);Assert.Equal(.24,d.Activation,8);
        Assert.Null(plan.Guidance(v,p,.239));Assert.NotNull(plan.Guidance(v,p,.24));
        Assert.Equal(1,plan.Target(v,.239));Assert.Equal(0,steer.SteeringRadians);Assert.Equal(18,wheel.AngularVelocityRadPerSecond*.33,10);
        var peak=PreparedCornerPlan.MaximumWheelPower(p);
        foreach(var rpm in Enumerable.Range(0,1201).Select(i=>i*10d))Assert.InRange(Engine500.Torque(rpm)*rpm*Math.PI/30*.94,0,peak+1e-8);
    }
    [Fact]
    public void LineIntentAndSecondBendUseSameGeometryRatherThanClockOrTurnNumber()
    {
        var frame=RacingLineRootCauseAudit.Frame();var s=4+frame.LapMeters/2;var a=frame.At(s,1);var spec=CornerEntryAudit.Rider();
        var v=PreparedCornerAudit.InitialView() with{Position=a.Position,Road=a.Road,TotalReferenceMeters=s,Heading=(float)a.Road.Heading,Velocity=a.Road.Tangent*6};
        var plan=new PreparedCornerPlan(v,spec,frame,PreparedCornerAudit.Parameters(),123,147,new(6),new(),new(PreferredOffsetMeters:3,AlternativeOffsetMeters:7));
        Assert.Equal(3,plan.SelectedOffsetMeters);Assert.Equal(123.24,plan.ActivationSeconds,8);Assert.True(plan.BendExitMeters>s);
        Assert.True(plan.TransitionStartMeters>s);Assert.True(plan.TransitionEndMeters>plan.TransitionStartMeters);
    }
    [Theory]
    [InlineData("A")] [InlineData("B")] [InlineData("C")]
    public void AcceptedNativeCaptureReplaysOriginalContactsAndCausalReviewsWithoutWorld(string name)
    {
        var options=new PreparedCornerOptions(name!="B",name!="A");var policy=PreparedCornerAudit.InitialPlan(options);var frame=policy.Path.Frame;
        var rows=DynamicAxleLoadAudit.ReadSaved("../feasible-path-speed-planning/"+name);var controls=DynamicAxleLoadAudit.ReadSaved("../feasible-path-speed-planning/"+name+"-control");
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        var views=rows.Select((r,i)=>PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=(float)controls[i]["target"]}).ToArray();
        for(var i=0;i<rows.Length;i++){
            var r=rows[i];var wheel=new RearWheelState(r["omega_before"]*.33);var omega=wheel.AngularVelocityRadPerSecond;var steer=new TwoContactBikeState((float)r["steer_before"]);
            var f=TwoContactBikeDynamics.Forces(views[i],CornerEntryAudit.Rider(),frame,surface,PreparedCornerAudit.Parameters(),r["t"],147,wheel,steer,(float)r["dt"],policy.Guidance(views[i],PreparedCornerAudit.Parameters(),r["t"]),inertia:52.72097f);
            Assert.Equal(r["front_fx"],f.FrontForce.X);Assert.Equal(r["front_fy"],f.FrontForce.Y);Assert.Equal(r["rear_fx"],f.RearForce.X);Assert.Equal(r["rear_fy"],f.RearForce.Y);Assert.Equal(r["steer_after"],f.SteeringAfter);
            Assert.Equal(omega,wheel.AngularVelocityRadPerSecond);Assert.Equal(r["steer_before"],steer.SteeringRadians);Assert.Null(f.AxleLoad);
            Assert.Equal(51,f.TravelSteering!.ContactQueries);
        }
        var reviews=JsonSerializer.Deserialize<PreparedCornerReview[]>(File.ReadAllText(Path.Combine(DirectoryPath,name+"-reviews.json")))!;
        foreach(var expected in reviews){var i=Enumerable.Range(0,rows.Length).MinBy(i=>Math.Abs(rows[i]["t"]-expected.Time));var r=rows[i];
            var actual=policy.Observe(views[i],CornerEntryAudit.Rider(),frame,surface,PreparedCornerAudit.Parameters(),expected.Time,147,52.72097f,new(r["omega_before"]*.33),new((float)r["steer_before"]));
            Assert.Equal(expected with{Profile=Array.Empty<PreparedSpeedPoint>()},actual with{Profile=Array.Empty<PreparedSpeedPoint>()});
        }
    }
}
