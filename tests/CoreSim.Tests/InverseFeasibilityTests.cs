using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
[Collection("Box2D worlds")]
public sealed class InverseFeasibilityTests
{
    [Fact]
    public void MatchedPathsSharePhysicalApexAndExitAndAreClosedC2WithCertifiedFootprint()
    {
        var inner=AuthoredRacingPath.Matched(1);var f=inner.Frame;var apex=(f.SegmentStartMeters(1)+f.SegmentStartMeters(4))/2;var exit=f.SegmentStartMeters(4);
        foreach(var path in InverseFeasibilityAudit.MatchedPaths()){
            Assert.Equal(path.MatchedEntryOffset!.Value,path.Offset(20).D,9);
            var g=new RacingLineGeometry(path.Frame);
            foreach(var s in new[]{apex,exit,exit+15}){
                var a=inner.At(s);var b=path.At(s);Assert.InRange((a.Position-b.Position).Length,0,1e-9);
                Assert.Equal(a.Heading,b.Heading,9);Assert.Equal(a.Curvature,b.Curvature,9);
            }
            foreach(var s in new[]{20d,35,apex,exit+15,f.SegmentStartMeters(1)+f.LapMeters/2-15}){
                var l=path.At(s-1e-5);var r=path.At(s+1e-5);
                Assert.InRange(Math.Abs(BikeAngles.Wrap(r.Heading-l.Heading)),0,2e-6);
                Assert.InRange(Math.Abs(r.Curvature-l.Curvature),0,2e-6);
                Assert.InRange(Math.Abs(path.Offset(s+1e-5).Second-path.Offset(s-1e-5).Second),0,2e-6);
            }
            for(var i=0;i<400;i++){
                var s=(i+.31)*f.LapMeters/400;var q=path.At(s);const double h=.01;
                var a=path.At(s-h).Position;var b=q.Position;var c=path.At(s+h).Position;var d1=(c-a)*(1/(2*h));var d2=(c-b*2+a)*(1/(h*h));
                var curvature=(d1.X*d2.Y-d1.Y*d2.X)/Math.Pow(d1.Length,3);
                Assert.InRange(Math.Abs(curvature-q.Curvature),0,.000025);
                Assert.InRange((path.At(s+f.LapMeters).Position-q.Position).Length,0,1e-9);
                for(var yaw=0;yaw<8;yaw++){
                    var v=new RiderView(1,new((float)q.Position.X,(float)q.Position.Y),Vector2.Zero,(float)(yaw*Math.PI/4),0,
                        f.RoadAt(s,q.OffsetMeters),s,0,RiderActivity.Racing,1,(float)q.OffsetMeters,0);
                    var gap=g.Clearance(v);Assert.True(gap.Inner>=.05);Assert.True(gap.Outer>=.5);
                }
            }
        }
    }
    [Fact]
    public void OriginalForceBalanceCanPassScalarGripWhileWheelTorqueIsUnavailable()
    {
        var path=new AuthoredRacingPath("outside-inside-outside");const double s=44.4;var q=path.At(s);
        Assert.True(324*Math.Abs(q.Curvature)<RacingLineRootCauseAudit.AccelerationBudget());
        var state=InverseFeasibilityAudit.Solve(path,s,18);Assert.InRange(state.Residual,0,.01);
        var before=InverseFeasibilityAudit.Solve(path,s-.02,18);var after=InverseFeasibilityAudit.Solve(path,s+.02,18);
        var required=RearWheelParameters.Default.WheelInertiaKgMetersSquared*(after.Omega-before.Omega)/.04*18/q.Metric+.33*state.Fx+.02*state.Omega;
        Assert.True(required>state.AvailableTorque*1.5);
        var legal=InverseFeasibilityAudit.Evaluate(path,s,18,state.Beta,state.Omega,1,1,.01f);
        Assert.InRange(legal.Wheel.DriveTorqueNewtonMeters,0,legal.Wheel.AvailableTorqueNewtonMeters);
        var saturated=InverseFeasibilityAudit.Evaluate(path,s,18,state.Beta,state.Omega,1,1,.01f,diagnosticCurvature:1e4);
        Assert.Equal(legal.Force,saturated.Force);Assert.True(saturated.Slip.TargetSlipDegrees>legal.Slip.TargetSlipDegrees);
        Assert.InRange(legal.Wheel.RearUtilization,0,1.000001);Assert.InRange(legal.Slip.GripUtilization,0,1.000001);
        Assert.InRange(Math.Abs(legal.Wheel.RootResidualMetersPerSecond),0,.000001);
        var mech=InverseFeasibilityAudit.Mechanical();var omega=1d;
        var work=legal.Yaw.AfterEnergyNewtonMeters*omega+.01*legal.Yaw.AfterEnergyNewtonMeters*legal.Yaw.AfterEnergyNewtonMeters/(2*mech.Inertia);
        Assert.True(work<=legal.Yaw.DissipationCreditWatts+1e-3);
    }
    [Fact]
    public void OioC2KnotRequiresNonzeroYawRateJumpEvenAtTheLowerSpeed()
    {
        static double Jump(string name,double speed,double h){var p=new AuthoredRacingPath(name);var s=35d;
            var b=InverseFeasibilityAudit.Solve(p,s,speed).Beta;
            double B(double x)=>InverseFeasibilityAudit.Solve(p,x,speed).Beta;
            var left=(3*b-4*B(s-h)+B(s-2*h))/(2*h);var right=(-3*b+4*B(s+h)-B(s+2*h))/(2*h);
            return speed*(right-left);}
        var coarse=Jump("outside-inside-outside",18,.01);var fine=Jump("outside-inside-outside",18,.005);
        Assert.InRange(fine,.16,.17);Assert.InRange(Math.Abs(coarse-fine),0,.0001);
        Assert.InRange(Jump("outside-inside-outside",15,.005),.09,.10);
        Assert.InRange(Math.Abs(Jump("inner",18,.005)),0,.0001);
    }
    [Fact]
    public void EquivalentInitialStatesAreActualStraightEquilibriaAndNoPoseIsCorrected()
    {
        var start=InverseFeasibilityAudit.SteadyStart(18);var positions=new List<Vector2>();
        foreach(var d in new[]{1d,5,8.5}){
            var path=AuthoredRacingPath.Matched(d);var local=InverseFeasibilityAudit.Evaluate(path,20,18,0,start.RearWheelOmega,start.Drive,0,.01f);
            Assert.InRange(local.Force.Length(),0,.002);Assert.InRange(Math.Abs(local.Wheel.AfterOmega-local.Wheel.BeforeOmega),0,.00001);
            var spec=RacingLineRootCauseAudit.Rider(path) with{ReferenceMeters=20,OffsetMeters=(float)d,TargetOffsetMeters=(float)d};
            using var world=new WorldRunner(new[]{spec},parameters:LocalOffsetAudit.Parameters(),trackShape:RacingLineRootCauseAudit.Variant(),authoredPath:path,
                diagnosticPathGuidance:true,diagnosticPathPreview:true,diagnosticSteadyStart:start);
            positions.Add(world.Snapshot.Riders[0].Position);Assert.Equal(start.RearWheelOmega,world.RearWheel(1).RearWheelAngularVelocityRadPerSecond,9);
            world.Run("steady",.01,.2,3);Assert.InRange(Math.Abs(world.Snapshot.Riders[0].Speed-18),0,.002);
        }
        Assert.True(Vector2.Distance(positions[0],positions[1])>3.9);Assert.True(Vector2.Distance(positions[1],positions[2])>3.4);
        Assert.Throws<ArgumentException>(()=>new WorldRunner(new[]{RacingLineRootCauseAudit.Rider(new("inner"))},diagnosticSteadyStart:start));
    }
    [Fact]
    public void NativeReplayKeepsOriginalGuardsAndRepeatsWithoutObservers()
    {
        var path=AuthoredRacingPath.Matched(8.5,15);var(c,w)=InverseFeasibilityAudit.Replay(path,18);
        Assert.True(c.Result.Completed);Assert.True(c.Ticks.All(t=>t.Slip.GripUtilization<=1.00001));
        Assert.True(w.All(x=>x.Step.DriveTorqueNewtonMeters>=0&&x.Step.DriveTorqueNewtonMeters<=x.Step.AvailableTorqueNewtonMeters+1e-5));
        var spec=RacingLineRootCauseAudit.Rider(path) with{ReferenceMeters=20,OffsetMeters=8.5f,TargetOffsetMeters=8.5f};
        using var plain=new WorldRunner(new[]{spec},parameters:LocalOffsetAudit.Parameters(),trackShape:RacingLineRootCauseAudit.Variant(),authoredPath:path,
            diagnosticPathGuidance:true,diagnosticPathPreview:true,diagnosticSteadyStart:InverseFeasibilityAudit.SteadyStart(18),frozenSurface:TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot());
        Assert.Equal(c.Result.Fingerprint,plain.Run("plain",.01,180,3,lapsFromInitialPosition:true).Fingerprint);
    }
    [Fact]
    public void EvidenceCannotBeOverwrittenAndFreezeRejectsParameterChanges()
    {
        var dir=Path.Combine(Path.GetTempPath(),"inverse-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try{
            InverseFeasibilityAudit.Run(dir,"freeze");var file=Path.Combine(dir,"freeze.json");var original=File.ReadAllText(file);
            Assert.Throws<IOException>(()=>InverseFeasibilityAudit.Run(dir,"freeze"));Assert.Equal(original,File.ReadAllText(file));
            File.WriteAllText(file,original.Replace("\"Spacing\":0.05","\"Spacing\":0.1",StringComparison.Ordinal));
            Assert.Throws<InvalidOperationException>(()=>InverseFeasibilityAudit.Run(dir,"inverse"));
        }finally{File.Delete(Path.Combine(dir,"freeze.json"));Directory.Delete(dir);}
    }
}
