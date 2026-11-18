using CoreSim;
using System.Text.Json;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static class Entry201
{
    internal static int Main(string[] args)
    {
        try
        {
            Study191.Run201(args[0],args[1],args[2]);
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}

internal static partial class Study191
{
    internal static void Run201(string mode,string workspace,string output)
    {
        Directory.CreateDirectory(output);
        switch(mode)
        {
            case "matched": Matched201(workspace,output);break;
            case "gate": Matched201(workspace,output,true);break;
            case "sensitivity": Sensitivity201(workspace,output);break;
            case "train": TrainFull201(workspace,output);break;
            case "train-yaw": TrainFull201(workspace,output,true);break;
            case "train-refined": TrainFull201(workspace,output,true,true);break;
            case "finish": FinishAudit201(workspace,output);break;
            case "releases": ReleaseAudit201(workspace,output);break;
            case "native-matched": NativeMatched201(workspace,output);break;
            case "native-current": NativeMatched201(workspace,output,true);break;
            case "dry-full": Native200(workspace,output,"C",true);break;
            case "native-full": Native200(workspace,output,"C",false);break;
            default:throw new ArgumentException("Unknown #201 operation.");
        }
    }

    private static void FinishAudit201(string workspace,string output)
    {
        var archive=Read199<Archive198>(Path.Combine(workspace,"outputs","badanie200","C-native.json.gz"));
        var rider=archive.Run.Riders.Single();
        var learned=Read199<Training199>(Path.Combine(workspace,"outputs","badanie200","training-C.json.gz")).Winner.Result;
        var next=Map199.At(3,0);
        var continuing=Planner201(rider.Spec,next.Exit+20,false);
        var probes=new List<object>();
        foreach(double before in new[]{66d,46,26,6,0})
        {
            var goal=3*Frame.LapMeters;
            int ix=Array.FindIndex(rider.Samples,s=>s.Input.Rider.TotalReferenceMeters>=goal-before);
            var root=Sample199(rider.Samples[ix],archive.Lean[0][ix]);
            var tape=root.Review>rider.Activations[^1].ObservedAt+1e-8?
                Enumerable.Repeat(new FreeRacingAction(rider.Samples[^1].RequestedSteering,
                    rider.Samples[^1].RequestedDrive),300).ToArray():Tape199(rider,root.Review);
            var truncated=Model199(rider.Spec).Begin(root,tape,0,goal,.50);
            truncated.Advance(3000);
            var held=Model199(rider.Spec).Begin(root,tape,0,next.Exit+20,.50);
            held.Advance(3000);
            var repeat=continuing.RepeatFeedback201(root,learned);
            var search=continuing.Learn("finish-continuation","A",root,
                new(){("repeat previous-cycle feedback",repeat.Controls??tape)},70,24,201666,explore:true);
            probes.Add(new{BeforeReferenceMeters=before,Start=root,NextEntry=next.Entry,NextGoal=next.Exit+20,
                ToFinish=new{truncated.Outcome,truncated.GoalTime,End=truncated.Snapshot},
                HoldTail=new{held.Outcome,held.Sweep,End=held.Snapshot},Repeat=repeat,Search=search});
        }
        Save(Path.Combine(output,"finish-model.json.gz"),probes,true);
    }

    private static (Archive198 Archive,Start197 Start,int Index,double Goal) MatchedRoot201(string workspace)
    {
        var preflight=JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace,"outputs","badanie196","preflight.json")));
        double when=preflight.RootElement.GetProperty("BranchTime").GetDouble();
        double goal=preflight.RootElement.GetProperty("Goal").GetDouble();
        var archive=Read199<Archive198>(Path.Combine(workspace,"outputs","badanie196","natywny.json.gz"));
        var samples=archive.Run.Riders.Single().Samples;
        int i=Array.FindIndex(samples,s=>Math.Abs(s.Input.Time-when)<1e-9);
        if(i<0)throw new InvalidOperationException("The #196 state is missing.");
        return(archive,Sample199(samples[i],archive.Lean[0][i]),i,goal);
    }

    private static CyclePlanner199 Planner201(RiderSpec spec,double goal,bool style,HandlingSettings? settings=null)
    {
        var corridor=Corridor();
        var model=settings is null?Model199(spec):new Predictor198(Frame,Surface,Physics,settings,Timing,spec,Mass,Inertia);
        return new(model,Frame.LapMeters,goal,corridor,style?new(corridor.Windows):null);
    }

    private static void Matched201(string workspace,string output,bool auditOnly=false)
    {
        var (archive,root,index,goal)=MatchedRoot201(workspace);
        var spec=archive.Run.Riders.Single().Spec;
        var frozen=Tape199(archive.Run.Riders.Single(),root.Review);
        var learned=Read199<Training199>(Path.Combine(workspace,"outputs","badanie200","training-C.json.gz")).Winner.Result;
        var model=Planner201(spec,goal,false);
        var style=Planner201(spec,goal,true);
        var a=model.Replay(root,frozen,false);
        var gated=style.Replay(root,frozen,true);
        var current=style.Transfer(root,learned,true);
        object? rejectingState=null;
        string? Reject(RiderView body,double gap)
        {
            var why=Corridor().Check(body,gap);
            if(why is not null)rejectingState=new{why,body.TotalReferenceMeters,SpeedKmh=body.Speed*3.6,
                CourseDegrees=Corridor199.CourseError(body)*180/Math.PI,body.AngularVelocity,Outer=gap,
                LimitCourse=4,LimitYaw=.23,RequiredSweep=.50};
            return why;
        }
        var rejected=Model199(spec).Begin(root,frozen,0,goal,.50,constraint:Reject);
        rejected.Advance(3000);
        Save(Path.Combine(output,"196-rejection.json"),new{Rejected=rejectingState});
        if(auditOnly)return;
        var baselineArchive=Read199<Archive198>(Path.Combine(workspace,"outputs","badanie194","natywne","solo-wide.json.gz"));
        var baseline=Tape199(baselineArchive.Run.Riders.Single(),root.Review);
        var adapted=model.Transfer(root,learned,false);
        var warm=new List<(string,FreeRacingAction[])>{("#196 frozen",frozen),("#194",baseline)};
        if(current.Legal)warm.Add(("#200 feedback from state",current.Controls!));
        else if(adapted.Legal)warm.Add(("#200 physical feedback",adapted.Controls!));
        var training=style.Learn("196-matched","B",root,warm,160,32,201196,explore:true);
        Save(Path.Combine(output,"matched-model.json.gz"),new{Start=root,Goal=goal,Prefix=index,
            Original196=a,Style196=gated,Transfer200=current,PhysicalTransfer200=adapted,Training=training},true);
        Save(Path.Combine(output,"matched-training.json.gz"),training,true);
        Console.WriteLine($"196 physical={a.Stop}/{a.Finish:F6} style={gated.Stop} S={gated.Progress:F4}, "+
            $"C transfer={current.Stop}, learned={training.Winner.Result.Stop} {training.Winner.Result.Finish:F6}.");
    }

    private static void Sensitivity201(string workspace,string output)
    {
        var (archive,root,_,goal)=MatchedRoot201(workspace);
        var spec=archive.Run.Riders.Single().Spec;
        var frozen=Tape199(archive.Run.Riders.Single(),root.Review);
        var nominal=Read199<Training199>(Path.Combine(workspace,"outputs","badanie200","training-C.json.gz")).Winner.Result;
        var all=new List<object>();
        foreach(double multiplier in new[]{1d,1.05,1.10,1.15,1.20})
        {
            var settings=Handling with {GripScale=Handling.GripScale*multiplier};
            var model=Planner201(spec,goal,false,settings);
            var frozenResult=model.Replay(root,frozen,false);
            var oldPolicy=model.Transfer(root,nominal,false);
            var sources=new List<(string,FreeRacingAction[])>{("#196",frozen)};
            if(oldPolicy.Legal)sources.Add(("#200 feedback",oldPolicy.Controls!));
            var recovered=new List<object>();
            if(multiplier>=1.10)
            {
                var raw196=Planner201(spec,goal,false).Replay(root,frozen,false);
                foreach(double steer in new[]{.65,.8,.9,1d,1.1})
                foreach(double gas in new[]{.6,.8,1d,1.15})
                {
                    var trial=model.Rebalance201(root,raw196,steer,gas);
                    recovered.Add(new{steer,gas,trial.Legal,trial.Stop,trial.Finish,trial.Progress,trial.Sweep});
                    if(trial.Legal)sources.Add(($"joint seed {steer}/{gas}",trial.Controls!));
                }
            }
            var training=model.Learn("grip-"+multiplier,"A",root,sources,90,24,201777,explore:true);
            all.Add(new{Multiplier=multiplier,Settings=settings,Frozen=frozenResult,Original200=oldPolicy,Seeds=recovered,Training=training});
            Console.WriteLine($"grip {multiplier:F2}: frozen={frozenResult.Stop}, learned={training.Winner.Result.Stop} {training.Winner.Result.Finish:F6}");
        }
        Save(Path.Combine(output,"grip-sensitivity-expanded.json.gz"),all,true);
    }

    private static void TrainFull201(string workspace,string output,bool removeYaw=false,bool refined=false)
    {
        var (seed,_,spec)=Fixture200(workspace,"C");
        var warm=Warm200(workspace,seed);
        var prior=Read199<Training199>(Path.Combine(workspace,"outputs","badanie200","training-C.json.gz")).Winner.Result;
        warm.Insert(0,("#200",Pad200(prior.Controls!)));
        var corridor=removeYaw?new Corridor199(Map199,double.PositiveInfinity):Corridor();
        var model=new CyclePlanner199(Model199(spec),Frame.LapMeters,seed.Goal,corridor,new(corridor.Windows));
        var training=model.Learn("C201","B",seed.Start,warm,refined?110:220,refined?28:32,201201,explore:true);
        Save(Path.Combine(output,refined?"training-C-refined.json.gz":removeYaw?"training-C-yaw.json.gz":"training-C.json.gz"),training,true);
        Console.WriteLine($"C201 {training.Winner.Result.Finish:F6}, {training.Winner.Result.Stop}, N={training.Candidates.Length}");
    }
}
