using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using SpeedwayHandling;
using WholeCorner171;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class FastContactAndTechnique206Tests
{
    [Fact]
    public void ProvenExclusionAvoidsMidpointAndInconclusiveBoxUsesFullProof()
    {
        int calls=0;
        ContactResidual64<int> At(double x)
        {
            calls++;
            return new(x,x+3,0);
        }
        var statistics=new ContactSearchMetrics64();
        Assert.Equal(ContactRootStatus.NoPhysicallyAdmissibleRoot,Assert.Throws<ContactRootException>(()=>
            ContactLoadSolver64.SolveCertified(-1,1,1e-8,At,(_,_,_)=>throw new InvalidOperationException("midpoint used"),
                earlyExclude:(a,b)=>new ContactInterval64(a.Ax,b.Ax).Low+3>0,metrics:statistics)).Status);
        Assert.Equal(2,calls);
        Assert.Equal(1,statistics.EarlyExclusions);
        var root=ContactLoadSolver64.SolveCertified(-1,1,1e-8,x=>new ContactResidual64<int>(x,x-.2,0),
            (a,b,_)=>new(new(a-.2,b-.2),1),earlyExclude:(_,_)=>false);
        Assert.Equal(.2,root.Ax,9);
    }

    [Fact]
    public void EarlyExclusionKeepsMultipleAndUnresolvedBoundaryRootsDistinctFromNoRoot()
    {
        static ContactResidual64<int> At(double x)=>new(x,x*x-.25,0);
        static ContactSlope64 Enclose(double lo,double hi,ContactResidual64<int> _)=>
            new(new ContactInterval64(lo,hi).Square()-.25,new(2*lo,2*hi));
        Assert.Equal(ContactRootStatus.AmbiguousOrMultipleRoots,Assert.Throws<ContactRootException>(()=>
            ContactLoadSolver64.SolveCertified(-1,1,1e-8,At,Enclose,
                earlyExclude:(a,b)=>(new ContactInterval64(a.Ax,b.Ax).Square()-.25).ExcludesZero)).Status);
        var unresolved=Assert.Throws<ContactRootException>(()=>ContactLoadSolver64.SolveCertified(.5000000001,.500000004,
            1e-8,At,(_,_,_)=>new(new(-1,1),new(-1,1)),earlyExclude:(_,_)=>false));
        Assert.Equal(ContactRootStatus.AmbiguousOrMultipleRoots,unresolved.Status);
    }

    [Theory]
    [InlineData(0,0.01)]
    [InlineData(.53,.003)]
    [InlineData(1,.02)]
    public void PreparedEngineClutchAndWheelGiveIdenticalResults(double gas,double dt)
    {
        var crank=new CrankClutch144(omega:800,lossH26:H26Configuration.For("H26-LE").EngineLoss);
        var expected=crank.Evaluate64(gas,8,.96,dt,true,Wheel);
        var engine=crank.EngineMidpointEvaluator64(gas,dt);
        var optimized=crank.Evaluate64(gas,8,.96,dt,true,Wheel,engine);
        Assert.Equal(expected,optimized);
        Assert.Equal(optimized,crank.Evaluate64(gas,8,.96,dt,true,Wheel,engine));
        Assert.Equal(800,crank.Omega);

        RearWheelTrial64 Wheel(double torque)=>RearWheelDynamics.EvaluateCombined64(22,1.8,75,147,1300,torque,3,dt,
            RearWheelParameters.Default);
    }

    [Fact]
    public void ForksRestoreCorrectionStartAndRetainIndependentHistory()
    {
        var observer=new Technique200([new(0,100)]);
        var original=observer.Begin();
        Assert.Null(original.Observe(Pose(35,0,0),0));
        Assert.Null(original.Observe(Pose(37,0,0),.125));
        Assert.Null(original.Observe(Pose(38,.17,0),.20));
        var checkpoint=original.Capture();
        Assert.True(checkpoint.Settled);
        var a=observer.Restore(checkpoint);
        var b=observer.Restore(checkpoint);
        Assert.Null(a.Observe(Pose(40,.17,.055),.31));
        Assert.Equal("technique-correction",a.Observe(Pose(42,.17,.055),.325));
        Assert.Null(b.Observe(Pose(42,-.17,.055),.325));
        Assert.Equal(-1,b.Capture().CorrectionSign);
        Assert.Equal(checkpoint,original.Capture());
        Assert.Equal("technique-correction",observer.Restore(checkpoint).Observe(Pose(42,.17,.055),.325));
    }

    [Fact]
    public void TechniqueRequiresBothTurnAndSustainedSlip()
    {
        var observer=new Technique200([new(0,100)]);
        var cursor=observer.Begin();
        Assert.Equal("technique-turn",cursor.Observe(Pose(179,0,0),1));
        cursor=observer.Begin();
        Assert.Null(cursor.Observe(Pose(72,.26,0),0));
        Assert.Null(cursor.Observe(Pose(75,.26,0),.125));
        Assert.Equal("technique-slip",cursor.Observe(Pose(179,0,0),1));
        cursor=observer.Begin();
        Assert.Null(cursor.Observe(Pose(82,.26,0,.13),0));
        Assert.Null(cursor.Observe(Pose(86,.26,0,.13),.125));
        Assert.Null(cursor.Observe(Pose(179,0,0),1));
        Assert.Null(cursor.Finish());
    }

    [Fact]
    public void StrictCorridorDistinguishesYawAndCourseAndLeavesBendYawAlone()
    {
        var map=new BendMap(Cyclic172.TrafficWorld191.Frame());
        var profile=new RacingProfile206(map);
        var window=new Corridor199(map).Windows[1];
        double inCore=window.Start+20;
        var yaw=profile.Begin().InspectStyle(Pose(inCore,.231,0),1,1,1,.5).Corridor!;
        Assert.True(yaw.Corridor!.YawExceeded);
        Assert.False(yaw.Corridor.CourseExceeded);
        var course=profile.Begin().InspectStyle(Pose(inCore,.10,.08),1,1,1,.5).Corridor!;
        Assert.False(course.Corridor!.YawExceeded);
        Assert.True(course.Corridor.CourseExceeded);
        Assert.Null(profile.Begin().InspectStyle(Pose(window.End+15,.6,0),1,1,1,.5).Corridor);
        var physical=profile.Begin().Check(Pose(inCore,.231,0),1,1,.49,.5,null,false);
        Assert.Equal("swept",physical!.Gate);
    }

    [Fact]
    public void MissingHistoryAtAnInteriorCheckpointIsRejected()
    {
        var profile=new RacingProfile206(new BendMap(Cyclic172.TrafficWorld191.Frame()));
        var start=new Start197(new(Pose(178,0,0),10.8,70,0,0,800),default,[],10.8);
        Assert.Throws<InvalidOperationException>(()=>profile.Begin(start));
        var history=profile.Begin().Capture();
        Assert.Equal(history,profile.Begin(start with {TechniqueHistory=history}).Capture());
    }

    [Fact]
    public void Fast50UsesEveryDecisionAndRequiresP99OperatingMargin()
    {
        var oneSlow=DecisionTiming206.Measure(Enumerable.Repeat(20d,99).Append(50.001));
        Assert.Equal(1,oneSlow.Over50);
        Assert.False(oneSlow.PassesDeadline);
        Assert.True(DecisionTiming206.Measure([25,30,37,50]).HasMargin);
        Assert.False(DecisionTiming206.Measure([40,42,44,46]).HasMargin);
        Assert.False(DecisionTiming206.Measure([]).PassesDeadline);
    }

    private static RiderView Pose(double s,double yaw,double course,double beta=0)=>default(RiderView) with
    {
        TotalReferenceMeters=s,AngularVelocity=(float)yaw,Heading=(float)(course+beta),
        Velocity=new((float)(22*Math.Cos(course)),(float)(22*Math.Sin(course))),
        Road=default
    };
}
