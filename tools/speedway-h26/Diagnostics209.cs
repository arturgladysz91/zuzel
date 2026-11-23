using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Events209(string root,string output)
    {
        var source=ReadBase208(root);var geometry=Geometry207(root);
        var plan=ReadPlan208(Path.Combine(root,"data/experiments/speedway208/frozen-candidate.json"));
        var law=Law209(source,geometry,plan);var (seed,_,spec)=Fixture206(root,"C");
        using var zip=new GZipStream(File.OpenRead(Path.Combine(root,Evidence209,"baseline/C.json.gz")),CompressionMode.Decompress);
        using var baseline=JsonDocument.Parse(zip);
        var events=new List<object>();double previous=0;
        using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var watch=Stopwatch.StartNew();
        foreach(var row in baseline.RootElement.GetProperty("Trace").EnumerateArray())
        {
            var before=row.GetProperty("Before").Deserialize<Start197>(Json)!;
            if(Math.Abs(before.Review-before.State.Time)>1e-8)continue;
            var action=law(before);double drop=previous-action.Drive;previous=action.Drive;
            if(drop<=.05)continue;
            var control=Follow209("C",Model208(spec),before,seed.Goal,law);
            var counterfactual=Follow209("C",Model208(spec),before,seed.Goal,at=>
            {
                var q=law(at);return at.State.Time<before.State.Time+.25-1e-9?q with {Drive=1}:q;
            });
            events.Add(new {Initial=before,Original=action,Drop=drop,
                Dominant=row.GetProperty("Command").GetProperty("Dominant").GetString(),
                Control=control,Counterfactual=counterfactual,
                Gain=control.Complete&&counterfactual.Complete?control.Finish-counterfactual.Finish:null});
            if((process.TotalProcessorTime-cpu).TotalSeconds>180)break;
        }
        Save(Path.Combine(output,"reduction-events.json.gz"),new {Events=events,MinimumDrop=.05,
            RequestedPulseSeconds=.25,Cpu=(process.TotalProcessorTime-cpu).TotalSeconds,Wall=watch.Elapsed.TotalSeconds,
            CpuCap=180,NativeWorlds=0},true);
        Console.WriteLine($"Completed {events.Count} paired full-suffix reduction counterfactuals.");
    }

    private static void Diagnostics209(string root,string output)
    {
        var source=ReadBase208(root);var geometry=Geometry207(root);
        var plan=ReadPlan208(Path.Combine(root,"data/experiments/speedway208/frozen-candidate.json"));
        var law=Law209(source,geometry,plan);
        var (seed,_,spec)=Fixture206(root,"C");
        using var compressed=new GZipStream(File.OpenRead(Path.Combine(root,Evidence209,"baseline/C.json.gz")),CompressionMode.Decompress);
        using var baseline=JsonDocument.Parse(compressed);
        var states=baseline.RootElement.GetProperty("Trace").EnumerateArray()
            .Select(x=>x.GetProperty("Before").Deserialize<Start197>(Json)!).ToArray();
        var anchors=(from visit in Enumerable.Range(0,6) let b=Map199.At(visit/Map199.Count,visit%Map199.Count)
            from mark in new[]{("entry",b.Entry),("exit",b.Exit),("straight",b.Exit+22)}
            where mark.Item2<seed.Goal-3
            select (Visit:visit,Name:mark.Item1,S:mark.Item2)).ToArray();
        var trials=new List<object>();var straights=new List<object>();
        using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var wall=Stopwatch.StartNew();
        foreach(var anchor in anchors)
        {
            var shot=states.First(x=>x.State.Rider.TotalReferenceMeters>=anchor.S&&
                Math.Abs(x.Review-x.State.Time)<1e-8);
            foreach(var gas in new[]{1d,.9,.7,-1,0,.15})
            {
                var label=gas<0?"frozen":gas.ToString("F2",System.Globalization.CultureInfo.InvariantCulture);
                FreeRacingAction Control(Start197 at)
                {
                    var q=law(at);
                    return gas<0?q:q with {Drive=(float)gas};
                }
                var local=Follow209("C",Model208(spec),shot,seed.Goal,Control,true,1.0);
                // A 0.25 s request intervention must still admit the exact frozen suffix to the finish.
                var continuation=Follow209("C",Model208(spec),shot,seed.Goal,at=>
                    at.State.Time<shot.State.Time+.25-1e-9?Control(at):law(at),true);
                trials.Add(new {Anchor=anchor,Gas=label,Initial=shot,Local=local,
                    Continuation=continuation with {Trace=null}});
            }
            if(anchor.Name=="straight")
            {
                double end=anchor.Visit<5?Map199.At((anchor.Visit+1)/Map199.Count,(anchor.Visit+1)%Map199.Count).Entry:seed.Goal;
                foreach(float gas in new[]{1f,.9f,.7f,0f,.15f})
                {
                    FreeRacingAction control(Start197 at)=>law(at) with {Drive=gas};
                    // Original track and full original mechanical state. Technique is separately
                    // reported: this controlled power diagnostic does not certify a race suffix.
                    var physical=new Predictor198(Frame,Surface,Physics,Settings202("H28-F"),Timing,spec,Mass,Inertia);
                    var run=Follow209("C",physical,shot,end,control,true,8);
                    straights.Add(new {Visit=anchor.Visit,Gas=gas,Initial=shot,Goal=end,Run=run});
                }
            }
            Console.WriteLine($"diagnostics bend {anchor.Visit+1} {anchor.Name}; {trials.Count} local/suffix pairs");
            if((process.TotalProcessorTime-cpu).TotalSeconds>360)break;
        }
        Save(Path.Combine(output,"capability.json.gz"),new {Trials=trials,Straights=straights,
            Bounds=new {Anchors=anchors.Length,GasLevels=6,LocalHorizon=1.0,InterventionSeconds=.25,CpuSeconds=360},
            Cpu=(process.TotalProcessorTime-cpu).TotalSeconds,Wall=wall.Elapsed.TotalSeconds,NativeWorlds=0},true);
    }
}
