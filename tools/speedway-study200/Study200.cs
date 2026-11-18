using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static class Entry200
{
    private static int Main(string[] args)
    {
        if(args.Length<4)
        {
            Console.Error.WriteLine("Cycle200 <operation> <workspace> <output> <arm> [rounds]");
            return 2;
        }
        try
        {
            var (operation,workspace,output,arm)=(args[0],args[1],args[2],args[3]);
            Study191.Run200(operation,workspace,output,arm,args.Length>4?int.Parse(args[4]):110);
            return 0;
        }
        catch(Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}

internal static partial class Study191
{
    internal static void Run200(string mode,string workspace,string output,string arm,int rounds)
    {
        Directory.CreateDirectory(output);
        switch(mode)
        {
            case "train": Train200(workspace,output,arm,rounds);break;
            case "dry": RunNative200(workspace,output,arm,true);break;
            case "native": RunNative200(workspace,output,arm,false);break;
            case "diagnostic": Diagnostic200(workspace,output);break;
            case "diagnostic-native": NearNative200(workspace,output);break;
            case "throttle": Throttle200(workspace,output);break;
            default: throw new ArgumentException("Unknown study operation.");
        }
    }

    private static (Seed198 Seed, StartingGate Gate, RiderSpec Spec) Fixture200(string workspace,string arm)
    {
        var root=Seed199(workspace,"B");
        var (gates,specs)=Grid194("solo-wide");
        var spec=specs[0];
        if(arm=="C-gear")spec=Spec193(gates[0],1,8.4);
        if(arm=="C-confirm")spec=Spec193(gates[0],1,8.2);
        if(arm is "C-alt" or "C-confirm")
        {
            float displacement=arm=="C-alt"?-.12f:.10f;
            spec=spec with {OffsetMeters=spec.OffsetMeters+displacement,TargetOffsetMeters=spec.TargetOffsetMeters+displacement};
            var where=Frame.At(spec.ReferenceMeters,spec.OffsetMeters);
            var original=root.Start.State.Rider;
            var rider=original with {Position=where.Position,Road=Frame.Project(where.Position),
                Heading=(float)where.Road.Heading,TargetOffsetMeters=(float)spec.TargetOffsetMeters};
            root=root with {Start=root.Start with {State=root.Start.State with {Rider=rider}}};
        }
        return (root,gates[0],spec);
    }

    private static (Corridor199 Corridor, Technique200? Technique) StyleFor200(string arm)
    {
        var corridor=Corridor(arm=="strict");
        return (corridor,arm.StartsWith('C') || arm=="strict"?new(corridor.Windows):null);
    }

    private static List<(string Name,FreeRacingAction[] Controls)> Warm200(string workspace,Seed198 root)
    {
        var prior=Path.Combine(workspace,"outputs","badanie199");
        var warm=new List<(string,FreeRacingAction[])>();
        foreach(var name in new[]{"B-smooth","B","A"})
        {
            var learned=Read199<Training199>(Path.Combine(prior,"training-"+name+".json.gz"));
            warm.Add(("#199 model "+name,Pad200(learned.Winner.Result.Controls!)));
        }
        foreach(var name in new[]{"B-smooth","B","A","B-held","B-second"})
        {
            var archive=Read199<Archive198>(Path.Combine(prior,name+"-native.json.gz"));
            warm.Add(("#199 native "+name,Tape199(archive.Run.Riders.Single(),root.Start.Review)));
        }
        warm.Add(("#194",root.Controls));
        return warm;
    }

    private static FreeRacingAction[] Pad200(FreeRacingAction[] controls)=>
        controls.Concat(Enumerable.Repeat(controls[^1],14)).ToArray();

    private static void Train200(string workspace,string output,string arm,int rounds)
    {
        var (root,_,spec)=Fixture200(workspace,arm);
        var (corridor,technique)=StyleFor200(arm);
        var warm=Warm200(workspace,root);
        foreach(var source in new[]{"C","B"}.Where(x=>arm is "C-alt" or "C-gear" or "C-confirm" or "B-refine"))
        {
            var p=Path.Combine(output,"training-"+source+".json.gz");
            if(File.Exists(p))
            {
                var trained=Read199<Training199>(p).Winner.Result;
                if(trained.Legal)warm.Add(("#200 model "+source,Pad200(trained.Controls!)));
            }
        }
        var engine=new CyclePlanner199(Model199(spec),Frame.LapMeters,root.Goal,corridor,technique);
        if(arm is "C-alt" or "C-gear" or "C-confirm")
        {
            var trained=Read199<Training199>(Path.Combine(output,"training-C.json.gz")).Winner.Result;
            var transfer=engine.Transfer(root.Start,trained,true);
            var fallback=Read199<Training199>(Path.Combine(workspace,"outputs","badanie199","training-B-smooth.json.gz"));
            var adjusted=engine.Transfer(root.Start,fallback.Winner.Result,true);
            Save(Path.Combine(output,$"transfer-{arm}.json.gz"),new{New=transfer,Prior=adjusted},true);
            if(transfer.Legal)warm.Add(("#200 state-adapted C",Pad200(transfer.Controls!)));
            if(adjusted.Legal)warm.Add(("#199 state-adapted C",Pad200(adjusted.Controls!)));
        }
        var result=engine.Learn(arm,"B",root.Start,warm,rounds,28,arm=="B"?200202:200313,
            explore:true);
        Save(Path.Combine(output,$"training-{arm}.json.gz"),result,true);
        Console.WriteLine($"{arm}: legal={result.Winner.Result.Legal} {result.Winner.Result.Stop}, " +
            $"t={result.Winner.Result.Finish:F6} swept={result.Winner.Result.Sweep:F4}, " +
            $"accepted={result.History.Length}, n={result.Candidates.Length}, CPU={result.Winner.TrainingCpu:F2}");
    }

    private static void RunNative200(string workspace,string output,string arm,bool dry) =>
        Native200(workspace,output,arm,dry);

    private static void Diagnostic200(string workspace,string output) =>
        NearBand200(workspace,output);
}
