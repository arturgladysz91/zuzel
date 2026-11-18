using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static class Entry199
{
    private static int Main(string[] args)
    {
        if(args.Length<4)throw new ArgumentException("Cycle199 train|native|transfer|diagnostic WORKSPACE OUTPUT ARM [rounds]");
        Study191.Run199(args[0],args[1],args[2],args[3],args.Skip(4).ToArray());
        return 0;
    }
}

internal sealed record Calibration198(double BaseMargin,double PositionTolerance,double YawTolerance,
    double SpeedTolerance,double CourseTolerance);
internal sealed record Archive198(Run144 Run,LeanSample[][] Lean,Schedule188 Timing,HandlingSettings Handling);
internal sealed record Seed198(string Arm,Archive198 Archive,int Branch,Start197 Start,double Goal,FreeRacingAction[] Controls);
internal sealed record ModelScore199(string Arm,double ActivationError,double PredictedFinish,int Steps,double Sweep);

internal static partial class Study191
{
    private static BendMap Map199=>new(Frame);
    private static double Finish199=>Laps*Frame.LapMeters;
    private static Corridor199 Corridor(bool smooth=false)=>new(Map199,smooth?.15:.23);
    private static Predictor198 Model199(RiderSpec s)=>new(Frame,Surface,Physics,Handling,Timing,s,Mass,Inertia);

    internal static void Run199(string mode,string workspace,string output,string arm,string[] args)
    {
        Directory.CreateDirectory(output);
        switch(mode)
        {
            case "diagnostic-parallel": NearParallel199(workspace,output);break;
            case "diagnostic": Near199(workspace,output);break;
            case "native": Native199(workspace,output,arm,false);break;
            case "dry": Native199(workspace,output,arm,true);break;
            case "train": Train199(workspace,output,arm,args.Length>0?int.Parse(args[0]):80);break;
            case "margin-audit": Train199(workspace,output,arm,int.Parse(args[0]),true);break;
            case "adapt": Adapt199(workspace,output,arm,args.Length>0?int.Parse(args[0]):59);break;
            case "transfer": Transfer199(workspace,output,arm);break;
            default: throw new ArgumentException("Unknown #199 operation.");
        }
    }

    private static T Read199<T>(string path)
    {
        using var f=File.OpenRead(path);
        using var g=new GZipStream(f,CompressionMode.Decompress);
        return JsonSerializer.Deserialize<T>(g,Json)??throw new InvalidOperationException("Archive missing "+path);
    }

    private static Start197 Sample199(StartSample144 sample,LeanSample lean)
    {
        var x=sample.Input;
        return new(new(x.Rider,x.Time,x.Omega,x.Steering,lean.ActualRadians,sample.CrankOmega),
            x.Active,x.Pending,x.NextReview);
    }

    private static Seed198 Seed199(string workspace,string arm)
    {
        var baseFile=arm.EndsWith("-inside",StringComparison.Ordinal)?"solo-inside":"solo-wide";
        var history=Read199<Archive198>(Path.Combine(workspace,"outputs/badanie194/natywne",baseFile+".json.gz"));
        if(history.Timing!=Timing || history.Handling!=Handling || history.Run.LapMeters!=Frame.LapMeters)
            throw new InvalidOperationException("The input archive differs from H25/FAST50.");
        var rider=history.Run.Riders.Single();
        int branch=0;
        if(arm.EndsWith("-held",StringComparison.Ordinal))
        {
            double at=Map199.At(0,0).Exit-10;
            branch=Array.FindIndex(rider.Samples,x=>x.Input.Rider.TotalReferenceMeters>=at);
        }
        else if(arm.EndsWith("-second",StringComparison.Ordinal))
        {
            double at=Map199.At(0,1).Entry-16;
            branch=Array.FindIndex(rider.Samples,x=>x.Input.Rider.TotalReferenceMeters>=at);
        }
        if(branch<0)throw new InvalidOperationException("The test start could not be found.");
        var root=Sample199(rider.Samples[branch],history.Lean[0][branch]);
        return new(arm,history,branch,root,Finish199,Tape199(rider,root.Review));
    }

    private static FreeRacingAction[] Tape199(RiderRun144 rider,double after)
    {
        var commands=rider.Activations.Where(x=>x.ObservedAt>=after-1e-8).Select(x=>x.Action).ToArray();
        if(commands.Length==0)throw new InvalidOperationException("Archive has no complete command prefix.");
        return commands.Concat(Enumerable.Repeat(commands[^1],45)).ToArray();
    }

    private static void Train199(string workspace,string output,string arm,int rounds,bool checkMargin=false)
    {
        if(arm is not ("A" or "B" or "B-smooth"))throw new ArgumentException("Expected A, B or B-smooth.");
        bool smooth=arm=="B-smooth";
        var root=Seed199(workspace,arm);
        var warm=new List<(string,FreeRacingAction[])>{("#194",root.Controls)};
        foreach(var key in new[]{"standing","matched-long"})
        {
            var b=Read199<Archive198>(Path.Combine(workspace,"outputs/badanie198",key+"-native.json.gz"));
            warm.Add(("#198 "+key,Tape199(b.Run.Riders.Single(),root.Start.Review)));
        }
        if(smooth)
        {
            foreach(var name in new[]{"B","B-held","B-second"})
            {
                var candidate=Read199<Archive198>(Path.Combine(output,name+"-native.json.gz"));
                warm.Add(("#199 "+name,Tape199(candidate.Run.Riders.Single(),root.Start.Review)));
            }
        }
        var engine=new CyclePlanner199(Model199(root.Archive.Run.Riders.Single().Spec),Frame.LapMeters,root.Goal,Corridor(smooth));
        var audit=checkMargin?new List<MarginTrial199>():null;
        var result=engine.Learn("nominal",arm[..1],root.Start,warm,rounds,24,
            smooth?199555:arm=="A"?199101:199202,audit);
        if(audit is not null)
        {
            var original=Read199<Training199>(Path.Combine(output,"training-"+arm+".json.gz"));
            if(!result.History.SequenceEqual(original.History) ||
                !result.Candidates.Select(q=>(q.Id,q.Finish,q.Progress,q.Stop,q.Sweep))
                    .SequenceEqual(original.Candidates.Select(q=>(q.Id,q.Finish,q.Progress,q.Stop,q.Sweep))))
                throw new InvalidOperationException("The paired margin audit did not reproduce the original search.");
            Save(Path.Combine(output,"audyt-marginesu-"+arm+".json.gz"),audit,true);
        }
        else Save(Path.Combine(output,"training-"+arm+".json.gz"),result,true);
        Console.WriteLine($"{arm}: T={result.Winner.Result.Finish:F6}, legal={result.Winner.Result.Legal}, " +
            $"sweep={result.Winner.Result.Sweep:F6}, accepted={result.History.Length}, " +
            $"N={result.Candidates.Length}, wall={result.Winner.WallSeconds:F2}, CPU={result.Winner.TrainingCpu:F2} {result.Winner.Result.Stop}");
    }

    private static void NearParallel199(string workspace,string output)
    {
        var training=Read199<Training199>(Path.Combine(output,"training-B.json.gz")).Winner.Result;
        var root=Seed199(workspace,"B");
        var corridor=Corridor();
        var b=Map199.At(0,1);
        var predictor=new CyclePlanner199(Model199(root.Archive.Run.Riders[0].Spec),Frame.LapMeters,b.Exit+20,corridor);
        var result=predictor.ParallelBand(training,corridor.Windows[1],new WideTrack194(Track));
        Save(Path.Combine(output,"blisko-rownolegle.json.gz"),result,true);
    }

    private static void Near199(string workspace,string output)
    {
        var training=Read199<Training199>(Path.Combine(output,"training-B.json.gz")).Winner.Result;
        var root=Seed199(workspace,"B");
        var corridor=Corridor();
        var b=Map199.At(0,1);
        var predictor=new CyclePlanner199(Model199(root.Archive.Run.Riders[0].Spec),Frame.LapMeters,b.Exit+20,corridor);
        var result=predictor.NearBand(training,corridor.Windows[1]);
        Save(Path.Combine(output,"blisko-bandy.json.gz"),result,true);
    }

    private static void Adapt199(string workspace,string output,string arm,int rounds)
    {
        if(arm is not ("A-held" or "B-held" or "B-second"))throw new ArgumentException("Unknown held state.");
        string variant=arm[..1];
        var root=Seed199(workspace,arm);
        var training=Read199<Training199>(Path.Combine(output,"training-"+variant+".json.gz"));
        var sim=new CyclePlanner199(Model199(root.Archive.Run.Riders.Single().Spec),Frame.LapMeters,root.Goal,Corridor());
        var timer=Stopwatch.StartNew();
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        var plan=sim.Adapt(arm,variant,root.Start,root.Controls,training.Winner.Result,rounds);
        Save(Path.Combine(output,"adapt-"+arm+".json.gz"),new{plan.Training,plan.Blends,Wall=timer.Elapsed.TotalSeconds,
            Cpu=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,root.Start},true);
        var win=plan.Training.Winner.Result;
        if(!win.Legal)throw new InvalidOperationException("No adapted legal plan.");
        Save(Path.Combine(output,"transfer-"+arm+".json.gz"),win,true);
        Console.WriteLine($"{arm}: feasible blends {plan.Blends.Count(x=>x.Legal)}/{plan.Blends.Length}, time={win.Finish:F6}, swept={win.Sweep:F6}, winners={plan.Training.History.Length}");
    }

    private static void Transfer199(string workspace,string output,string arm)
    {
        if(arm.Length<3)throw new ArgumentException("Expected arm-held or arm-second.");
        string variant=arm[..1];
        var root=Seed199(workspace,arm);
        var training=Read199<Training199>(Path.Combine(output,"training-"+variant+".json.gz"));
        var sim=new CyclePlanner199(Model199(root.Archive.Run.Riders.Single().Spec),Frame.LapMeters,root.Goal,Corridor());
        var adjusted=sim.Transfer(root.Start,training.Winner.Result,variant=="B");
        if(adjusted.Legal)
        {
            Save(Path.Combine(output,"transfer-"+arm+".json.gz"),adjusted,true);
            Console.WriteLine($"transfer {arm} from observed state: {adjusted.Finish:F6}s, swept={adjusted.Sweep:F4}");
        }
        else
        {
            Save(Path.Combine(output,"transfer-"+arm+"-failed.json.gz"),adjusted,true);
            Console.WriteLine($"transfer {arm} rejected: reason={adjusted.Stop}, s={adjusted.Progress:F2}.");
        }
    }
}
