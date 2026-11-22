using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using SpeedwayHandling;
using WholeCorner171;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class JointPlanner207Tests
{
    [Fact]
    public void BeamExcludesHardFailuresAndKeepsDifferentStatesAtTheSameReferenceDistance()
    {
        var a=Root();
        var b=a with {State=a.State with {Rider=a.State.Rider with {AngularVelocity=.11f}}};
        var c=a with {RiderHeightOffsetMeters=-.02};
        var d=a with {TechniqueHistory=a.TechniqueHistory!.Value with {CorrectionSince=.15,CorrectionSign=1}};
        Branch207[] options=[Branch(1,1,a,2,1.0,0),Branch(2,2,b,1.5,1.7,.4),
            Branch(3,3,c,1.4,1.1,.1),Branch(4,4,d,1.6,.9,.5),Branch(5,5,a,100,50,0,false)];
        var kept=Beam207.Retain(options,4);
        Assert.Equal(4,kept.Length);
        Assert.DoesNotContain(kept,x=>x.Id==5);
        Assert.Equal(4,kept.Select(x=>x.Parent).Distinct().Count());
        Assert.Single(Beam207.Retain([options[0],options[0] with {Id=6}],3));
    }

    [Fact]
    public void CheckpointReuseRequiresExactPrefixPendingAndObserverState()
    {
        var snapshot=Root();
        var inherited=new FreeRacingCommand(.05,.23,new(.03f,.8f));
        var a=snapshot with {Pending=[inherited]};
        var b=a with {Pending=[inherited with {ActivationAt=.24}]};
        var c=a with {TechniqueHistory=a.TechniqueHistory!.Value with {TurnSince=.12}};
        Assert.True(Beam207.CanResume(31,31,a,a with {Pending=[inherited]}));
        Assert.False(Beam207.CanResume(31,32,a,a));
        Assert.False(Beam207.CanResume(31,31,a,b));
        Assert.False(Beam207.CanResume(31,31,a,c));
    }

    [Fact]
    public void WorstCaseRankingPrecedesNominalSpeed()
    {
        var pool=new[] {(Id:"quick nominal",Worst:400,Count:4,Reserve:.9,Time:43d),
            (Id:"legal all",Worst:956,Count:5,Reserve:.62,Time:49d),
            (Id:"higher reserve all",Worst:956,Count:5,Reserve:.72,Time:50d),
            (Id:"four only",Worst:955,Count:4,Reserve:1.1,Time:44d)};
        Assert.Equal(new[]{"higher reserve all","legal all","four only","quick nominal"},
            Beam207.RankRobust(pool,x=>x.Worst,x=>x.Count,x=>x.Reserve,x=>x.Time).Select(x=>x.Id));
    }

    [Fact]
    public void FeedbackAndTapeShareNativePendingOrderWhenResumingAtReview()
    {
        var frame=Cyclic172.TrafficWorld191.Frame();
        var spec=Cyclic172.TrafficWorld191.Rider(StartingGate.D);
        var pose=frame.At(0,spec.OffsetMeters);
        var strict=new RacingProfile206(new BendMap(frame));
        var model=new Predictor198(frame,MultiActionExperiment135.Surface(frame),FreeLearningExperiment.Parameters(),
            new HandlingSettings {H26=H26Configuration.For("H26-LE"),PostureH27=new(),DoubleContactSolve=true,
                CoupledRearTyre=true,FastCoupledCertificate=true},new(0,.05,.05),spec,147,52.72097f,strict);
        var rider=new RiderView(1,pose.Position,Vector2.Zero,(float)pose.Road.Heading,0,pose.Road,0,0,
            RiderActivity.Racing,0,spec.OffsetMeters,0);
        var pending=new FreeRacingCommand(-.01,.071,new(.002f,.18f));
        var initial=new Start197(new(rider,0,0,0,0,4000*Math.PI/30),default,[pending],0);
        var actions=new[]{new FreeRacingAction(0,.22f),new FreeRacingAction(.001f,.25f),new FreeRacingAction(0,.27f),
            new FreeRacingAction(0,.24f),new FreeRacingAction(-.001f,.21f),new FreeRacingAction(0,.20f)};
        FreeRacingAction Feedback(Start197 at)=>actions[(int)Math.Round(at.Review/.05)];
        var tape=model.Begin(initial,actions,0,3*frame.LapMeters,.5);
        tape.Advance(50,.25);
        var first=model.BeginFeedback207(initial,Feedback,3*frame.LapMeters,.5);
        first.Advance(30,.04);
        Assert.Null(first.Outcome);
        var checkpoint=first.Checkpoint();
        Assert.Equal(default,checkpoint.Active);
        Assert.Equal(pending,checkpoint.Pending[0]);
        Assert.Equal(2,checkpoint.Pending.Length);
        Assert.Equal(actions[0],checkpoint.Pending[1].Action);
        Assert.All(checkpoint.Pending,x=>Assert.Equal(.071,x.ActivationAt,8));
        var continued=model.BeginFeedback207(checkpoint,Feedback,3*frame.LapMeters,.5);
        continued.Advance(35,.25);
        Assert.Null(tape.Outcome);
        Assert.Null(continued.Outcome);
        Assert.True(Beam207.SameCheckpoint(tape.Checkpoint(),continued.Checkpoint()));
        Assert.Single(initial.Pending);
        Assert.Equal(pending,initial.Pending[0]);
    }

    [Fact]
    public void FinishRequiresTechniqueInEveryVisit()
    {
        var frame=Cyclic172.TrafficWorld191.Frame();
        var profile=new RacingProfile206(new BendMap(frame));
        var seed=Root();
        var finish=seed.State.Rider with {TotalReferenceMeters=3*frame.LapMeters};
        var invalid=profile.Begin(seed).Check(finish,50,1,1,.5,null,true);
        Assert.Equal("technique-turn",invalid!.Condition);
        var complete=seed with {TechniqueHistory=seed.TechniqueHistory!.Value with {Index=6}};
        Assert.Null(profile.Begin(complete).Check(finish,50,1,1,.5,null,true));
    }

    private static Start197 Root()=>new(new(default(RiderView) with {TotalReferenceMeters=150},9,60,.02f,.12,550),
        new(.01f,.45f),[],9){TechniqueHistory=new Technique200.Checkpoint(0,null,null,null,null,false,false,false,0,0)};

    private static Branch207 Branch(int id,int parent,Start197 start,double quality,double swept,double yawRisk,bool legal=true)
    {
        var end=new HeadingSample198(9,150,0,0,21,0,start.State.Rider.AngularVelocity,0,60,550,.02f,.12,swept,1,1,start.Active);
        var reserve=new Reserve207(swept,0,start.State.Rider.AngularVelocity,0,21,1,1,500,.7,0,.4,0,yawRisk,0,.12,0,1,quality);
        return new(id,parent,2,default,[new("C",legal,legal?"checkpoint":"swept",start,legal?start:null,end,swept,reserve,null,null,30)]);
    }
}
