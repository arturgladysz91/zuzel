using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using SpeedwayHandling;
using WholeCorner171;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class ExitViability208Tests
{
    [Fact]
    public void OutwardProjectionAndSignedInwardAccelerationUseTheFollowingStraightBasis()
    {
        var road=new RoadPoint(0,0,0,0,Math.PI/2,0,0);
        var pose=default(RiderView) with {Velocity=new(3,12),AngularVelocity=.42f};
        var motion=ExitMotion208.From(pose,road,147);
        Assert.Equal(12,motion.Forward);
        Assert.Equal(3,motion.Outward);
        Assert.Equal(441,motion.Momentum);
        Assert.Equal(Math.Atan2(3,12),motion.Course);
        Assert.Equal(4,ExitMotion208.Inward(new(-588,99),road,147));
        Assert.Equal(-4,ExitMotion208.Inward(new(588,99),road,147));
    }

    [Fact]
    public void StoppingEstimateDoesNotCreditOutwardOrUnavailableForce()
    {
        var state=new ExitMotion208(22,7,0,1029,0);
        Assert.Equal(2.45,state.StopDistance(10)!.Value,10);
        Assert.Equal(.7,state.StopTime(10)!.Value,10);
        Assert.Null(state.StopDistance(0));
        Assert.Null(state.StopTime(-5));
        var inward=state with {Outward=-1};
        Assert.Equal(0,inward.StopDistance(0));
        Assert.Equal(.45,state.Room(1.25,.95,.5),9);
    }

    [Fact]
    public void ProgressiveBendTransitionsToAConstantFollowingStraightHeading()
    {
        var frame=Cyclic172.TrafficWorld191.Frame();
        var map=new BendMap(frame);
        var bend=map.At(1,1);
        double curvature(double s)=>BikeAngles.Wrap(map.Locate(s+.25).Road.Heading-map.Locate(s-.25).Road.Heading)/.5;
        Assert.True(curvature((bend.Entry+bend.Exit)/2)>.01);
        Assert.InRange(Math.Abs(curvature(bend.Exit+5)),0,1e-8);
        Assert.InRange(Math.Abs(BikeAngles.Wrap(map.Locate(bend.Exit+1e-5).Road.Heading-
            map.Locate(bend.Exit-1e-5).Road.Heading)),0,1e-5);
    }

    [Fact]
    public void CompleteManeuverBlendsBackToTheSameFeedbackAtBothGeometryBoundaries()
    {
        var frame=Cyclic172.TrafficWorld191.Frame();
        var map=new BendMap(frame);
        var q=new FreeRacingAction(.003f,.52f);
        RoutePoint208[] route=[new(frame.LapMeters+100,9.35,0,21,0,0),new(frame.LapMeters+330,9.35,0,21,0,0)];
        var bank=new ExitPolicy208(map,frame.LapMeters,_=>q,route,route);
        var t=new Shape208(38,.6,1,-.3,4,.4,.3,.6,0,18,2,.3,5,3,4,-.2,.02,.04,2,.4,.3);
        var bend=bank.Bend(3);
        var m=new Maneuver208(3,t,new(-.02,-.3,.4));
        foreach(double boundary in new[]{bank.Begin(bend,m),bend.Exit+20})
        {
            var at=Shot(boundary);
            Assert.Equal(q,bank.At(at,bend,m));
            foreach(double s in new[]{boundary-1e-3,boundary+1e-3})
            {
                var a=bank.At(Shot(s),bend,m);
                Assert.InRange(Math.Abs(a.SteeringRadians-q.SteeringRadians),0,1e-6);
                Assert.InRange(Math.Abs(a.Drive-q.Drive),0,1e-6);
            }
        }
        Assert.Equal(q.Drive,bank.At(Shot(bend.Exit-19),bend,m with {Speed=false}).Drive);

        Start197 Shot(double s)
        {
            var road=map.Locate(s).Road;
            var body=default(RiderView) with {Road=road,Velocity=road.Tangent*22,TotalReferenceMeters=s};
            return new(new(body,20,60,.02f,.12,550),q,[],20);
        }
    }

    [Fact]
    public void ExactRolloutRejectsTheFrozenOutwardMomentumDespiteAdmissibleTyreForces()
    {
        var frame=Cyclic172.TrafficWorld191.Frame();
        var map=new BendMap(frame);
        var start=FrozenExit();
        var spec=Cyclic172.TrafficWorld191.Rider(StartingGate.D);
        var engine=Engine500Parameters.Default;
        spec=spec with {Setup=new((float)((engine.ShortOverallRatio-8)/(engine.ShortOverallRatio-engine.LongOverallRatio)),
            spec.Setup.TractionBias)};
        var profile=new RacingProfile206(map);
        var model=new Predictor198(frame,MultiActionExperiment135.Surface(frame),FreeLearningExperiment.Parameters(),
            new HandlingSettings {H26=H26Configuration.For("H26-LE"),PostureH27=new(),DoubleContactSolve=true,
                CoupledRearTyre=true,FastCoupledCertificate=true},new(0,.05,.05),spec,147,52.72097f,profile);
        var original=profile.Begin(start);
        Assert.Equal(start.TechniqueHistory,original.Capture());
        var initial=ExitMotion208.From(start.State.Rider,map.Locate(map.At(1,1).Exit+1).Road,147);
        Assert.InRange(initial.Outward,7,8);
        var protectedStart=start with {Pending=[..start.Pending]};
        Assert.True(Beam207.CanResume(21,21,start,protectedStart));
        Assert.False(Beam207.CanResume(21,22,start,protectedStart));
        var moreDelay=new FreeRacingCommand(start.Review,start.Review+.12,new(0,0));
        Assert.False(Beam207.CanResume(21,21,start,start with {Pending=[..start.Pending,moreDelay]}));
        var shortForecast=model.BeginFeedback207(start,_=>new(0,.1f),3*frame.LapMeters,.5,profile:true);
        shortForecast.Advance(100,start.State.Time+.65);
        Assert.Equal("swept",shortForecast.Outcome);
        Assert.True(shortForecast.LastPower!.Wheel.RearUtilization<=1.000001);
        Assert.True(shortForecast.LastContact!.Forces.TyreLoads!.FrontNormalNewtons>0);
        Assert.True(shortForecast.LastLegalS207<shortForecast.Snapshot.S);
        Assert.Equal(start.TechniqueHistory,original.Capture());
    }

    [Fact]
    public void StrictPhysicalRejectionAndMomentumTakePrecedenceOverAnInstantaneousWideMargin()
    {
        var stable=Study191.Quality208(638,true,true,.65,2,.1,true);
        var outward=Study191.Quality208(638,true,true,.65,8,.1,true);
        var partial=Study191.Quality208(635,false,false,1.85,8,.1,true);
        Assert.True(stable>outward);
        Assert.True(outward>partial);
        Assert.True(Study191.Quality208(956,true,true,.5,8,.1,true)>
            Study191.Quality208(956,true,false,1.2,2,.1,true));
        Assert.Throws<ContactRootException>(()=>ContactLoadSolver64.SolveCertified(-1,1,1e-8,
            x=>new ContactResidual64<int>(x,x+3,0),(_,_,_)=>new(new(2,4),1),
            earlyExclude:(_,_)=>true));
        const double mass=147, inertia=52.72097;
        var bike=TwoContactBikeParameters.Default;
        var spec=Cyclic172.TrafficWorld191.Rider(StartingGate.D);
        var state=default(RiderView) with {Velocity=new(20,2),AngularVelocity=.22f};
        var settings=new HandlingSettings {H26=H26Configuration.For("H26-LE"),PostureH27=new(),
            DoubleContactSolve=true,CoupledRearTyre=true,FastCoupledCertificate=true};
        var normal=mass*LoadBasedTyreGrip.GravityMetersPerSecondSquared;
        var loads=new LoadBasedTyreBudget(1.6,normal*.4,normal*.6);
        var wheel=new RearWheelState(78,RearWheelParameters.Default);
        var query=new HandlingContact.ContactQuery64(settings,state,spec,bike,loads,mass,inertia,.55,
            20,.01,0,.02,.2,wheel,new CrankClutch144(omega:500),.05,diagnosticRearNormal:normal);
        var excluded=Assert.Throws<ContactRootException>(()=>query.Evaluate(0));
        Assert.Equal(ContactRootStatus.InvalidPhysicalState,excluded.Status);
        Assert.Contains("positive contact loads",excluded.Message);
    }

    [Fact]
    public void FiveStartSelectionAndDeadlineStatisticsTrackTheWorstCase()
    {
        var rows=new[] {new[]{640d,640,640,640,617},new[]{631d,632,631,630,632}};
        var ranked=Beam207.RankRobust(rows,x=>x.Min(),x=>x.Count(s=>s>622.557),_=>.6,_=>85);
        Assert.Same(rows[1],ranked.First());
        var times=DecisionTiming206.Measure([1,7,50,50.01,91]);
        Assert.Equal(2,times.Over50);
        Assert.Equal(91,times.Max);
        Assert.False(times.PassesDeadline);
    }

    private static Start197 FrozenExit()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"SpeedwayManager.sln")))root=root.Parent;
        Assert.NotNull(root);
        using var stream=new GZipStream(File.OpenRead(Path.Combine(root.FullName,
            "data/experiments/speedway207/selected-replays.json.gz")),CompressionMode.Decompress);
        using var json=JsonDocument.Parse(stream);
        var trace=json.RootElement.GetProperty("Runs")[0].GetProperty("Trace");
        foreach(var row in trace.EnumerateArray())
        {
            var state=row.GetProperty("Start");
            if(state.GetProperty("State").GetProperty("Rider").GetProperty("TotalReferenceMeters").GetDouble()<610.5575)continue;
            return state.Deserialize<Start197>(FreeLearningExperiment.Json)!;
        }
        throw new InvalidOperationException("Archived fourth exit missing.");
    }
}
