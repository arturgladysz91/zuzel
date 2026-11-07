using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using LearnedCorner166;
using LineThrottle156;
using SharedWorldSpeedway;
using WholeCorner171;

namespace Cyclic172;

internal static class Study172 { internal static double Cpu=>Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds; }
internal sealed record Selected174(string Id,string Trial,string Reason,FreeRacingInput Input,double CrankOmega,
    FreeRacingAction Action,RiderSpec Spec,JsonElement Classification);

internal sealed class Context174
{
    internal static readonly JsonSerializerOptions Json=new(Study156.Json){WriteIndented=false};
    internal const string Tool="tools/state-feasible-throttle",Docs172="docs/experiments/three-lap-global-planning";
    internal readonly TrackFrame Frame=NativeWorld172.Frame();
    internal readonly Teacher A,B;
    internal readonly Weights Weights;
    internal readonly Integration Tuning;
    internal readonly Nominal[][] Profiles;
    internal readonly string Directory,Stage,Label;
    internal double Began=Study172.Cpu,Prior;
    internal int PriorWorlds,Worlds=0;

    internal Context174(string stage,string label,string directory)
    {
        Stage=stage;Label=label;Directory=directory;
        System.IO.Directory.CreateDirectory(directory);
        if(stage is "develop" or "integrate" or "validate" && File.Exists(Path.Combine(directory,"native-closed.json")))
            throw new InvalidOperationException("Research native phase closed on performance gate; no further worlds or S3 authorized by this protocol");
        if(label.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("Simple unique label");
        var receipts=System.IO.Directory.GetFiles(directory,"*.done.json");
        if(receipts.Length!=System.IO.Directory.GetFiles(directory,"*.started.json").Length)throw new InvalidOperationException("Unclosed process reservation");
        Prior=receipts.Sum(p=>Read<JsonElement>(p).GetProperty("CpuSeconds").GetDouble());
        Prior+=System.IO.Directory.GetFiles(directory,"*.cpu.json").Sum(p=>Read<JsonElement>(p).GetProperty("CpuSeconds").GetDouble());
        Prior+=Read<JsonElement>(Path.Combine(directory,"archive-proof.json")).GetProperty("CpuSeconds").GetDouble();
        PriorWorlds=System.IO.Directory.GetFiles(directory,"*.world-reserved.json").Length;
        var fit=Read<JsonElement>("docs/experiments/autonomous-fast-corner/evidence/fit.json");
        var freeze=Read<JsonElement>("docs/experiments/autonomous-fast-corner/evidence/freeze.json");
        if(Hash("docs/experiments/autonomous-fast-corner/evidence/fit.json")!=freeze.GetProperty("FitSha256").GetString())throw new InvalidOperationException("Frozen Teacher mismatch");
        A=fit.GetProperty("A").Deserialize<Teacher>(Json)!;B=fit.GetProperty("B").Deserialize<Teacher>(Json)!;
        Weights=freeze.GetProperty("Selected").GetProperty("Weights").Deserialize<Weights>(Json)!;
        Profiles=Read<Nominal[][]>("docs/experiments/whole-corner-planning/profile.json");
        using var zip=ZipFile.OpenRead("docs/experiments/two-corner-full-drive/170-dowody.zip");
        Tuning=Read<JsonElement>(zip,"freeze.json").GetProperty("Tuning").Deserialize<Integration>(Json)!;
        var history=Read<JsonElement>(Docs172+"/summary.json").GetProperty("freeze");
        foreach(string group in new[]{"Sources","PhysicsSha256"})foreach(var source in history.GetProperty(group).EnumerateObject())
            if(Hash(source.Name)!=source.Value.GetString())throw new InvalidOperationException("Historical source changed "+source.Name);
    }
    internal CyclicController172 Controller(RiderSpec spec)=>new(A,B,Weights,Tuning,Profiles,"K4",spec,Frame,
        MultiActionExperiment135.Surface(Frame),FreeLearningExperiment.Parameters(),147,52.72097f);
    internal void Guard(){if(Prior+Study172.Cpu-Began>=295)throw new InvalidOperationException("300 CPU-s budget reserve reached");}
    internal string At(string name)=>Path.Combine(Directory,Label+name);
    internal static T Read<T>(string p)=>JsonSerializer.Deserialize<T>(File.ReadAllBytes(p),Json)!;
    internal static T Read<T>(ZipArchive z,string name){using var s=z.GetEntry(name)!.Open();return JsonSerializer.Deserialize<T>(s,Json)!;}
    internal static string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
    internal static void Save(string p,object value){using var s=new FileStream(p,FileMode.CreateNew);JsonSerializer.Serialize(s,value,Json);}
    internal static FreeRacingInput Observation(StartSample144 sample,double time)=>sample.Input with{Time=time,
        NextReview=time==0?0:sample.Input.NextReview,Pending=sample.Input.Pending.Where(q=>q.ObservedAt<time-1e-8).ToArray(),
        Rider=sample.Input.Rider with{Drive=sample.Input.Active.Drive}};

    internal void States()
    {
        var selected=Read<Selected174[]>(Path.Combine(Directory,"selected173.json")).ToList();
        using var outer=ZipFile.OpenRead(Docs172+"/172-validation-K4.zip");
        if(Stage=="extra")
        {
            selected.Clear();
            foreach(var request in new[]{("X4","v1-K4-S4",11.6),("XB","v1-K4-S2",11.8)})
            {
                using var stream=outer.GetEntry(request.Item2+".zip")!.Open();using var buffer=new MemoryStream();stream.CopyTo(buffer);buffer.Position=0;using var z=new ZipArchive(buffer);
                var run=Read<Run144>(z,"native.json");var r=run.Riders.Single();var d=Read<CyclicDecision172[]>(z,"decisions.json").Single(d=>Math.Abs(d.Time-request.Item3)<1e-8);
                var s=r.Samples.MinBy(s=>Math.Abs(s.Input.Time-d.Time))!;
                selected.Add(new(request.Item1,request.Item2,"Additional authentic first-lap state within selective region",Observation(s,d.Time),s.CrankOmega,d.Action,r.Spec,JsonSerializer.SerializeToElement(new{Source="archive"})));
            }
        }
        // Add three actual second-lap reviews, without touching the future holdout set.
        if(Stage!="extra")using(var source=outer.GetEntry("v1-K4-S1.zip")!.Open())using(var memory=new MemoryStream())
        {
            source.CopyTo(memory);memory.Position=0;using var zip=new ZipArchive(memory);
            var run=Read<Run144>(zip,"native.json");var rider=run.Riders.Single();var decisions=Read<CyclicDecision172[]>(zip,"decisions.json");
            foreach(double distance in new[]{41d,25,12})
            {
                var next=new BendMap(Frame).At(1,1);
                var choice=decisions.Where(q=>q.Reference<next.Entry-distance).Last();
                var sample=rider.Samples.MinBy(q=>Math.Abs(q.Input.Time-choice.Time))!;
                selected.Add(new("E"+distance,"v1-K4-S1","Additional authentic second-lap review",Observation(sample,choice.Time),sample.CrankOmega,choice.Action,rider.Spec,JsonSerializer.SerializeToElement(new{Source="archive"})));
            }
        }
        Save(At("-selection.json"),selected);
        using var file=new FileStream(At("-states.jsonl.gz"),FileMode.CreateNew);using var gzip=new GZipStream(file,CompressionLevel.Optimal);using var writer=new StreamWriter(gzip);
        var summaries=new List<object>();
        foreach(var group in selected.GroupBy(s=>s.Trial))
        {
            using var source=outer.GetEntry(group.Key+".zip")!.Open();using var memory=new MemoryStream();source.CopyTo(memory);memory.Position=0;using var zip=new ZipArchive(memory);
            var native=Read<Run144>(zip,"native.json");var rider=native.Riders.Single();var old=Read<CyclicDecision172[]>(zip,"decisions.json");
            var controller=Controller(rider.Spec);var rule=new StateFeasibility174(this,rider.Spec);var states=group.ToArray();
            foreach(var decision in old.TakeWhile(d=>d.Time<=states.Max(s=>s.Input.Time)+1e-8))
            {
                Guard();var sample=rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-decision.Time))!;var input=Observation(sample,decision.Time);
                var crank=new CrankClutch144(omega:sample.CrankOmega);var actual=controller.Choose(input,crank);
                if(actual.Action!=decision.Action||actual.Proposal!=decision.Proposal||actual.Tracking.Gate!=decision.Tracking.Gate)
                    throw new InvalidOperationException("Fork baseline differs from frozen K4");
                foreach(var state in states.Where(s=>Math.Abs(s.Input.Time-decision.Time)<1e-8))
                {
                    if(input!=state.Input&&JsonSerializer.Serialize(input,Json)!=JsonSerializer.Serialize(state.Input,Json))throw new InvalidOperationException("Selected physical observation differs");
                    var evaluation=rule.Evaluate(input,crank,actual,controller);
                    var following=old.FirstOrDefault(q=>q.Time>decision.Time+1e-8);
                    if(following is not null)
                    {
                        var nextSample=rider.Samples.MinBy(q=>Math.Abs(q.Input.Time-following.Time))!;
                        var forkChoice=controller.Fork174().Choose(Observation(nextSample,following.Time),new(omega:nextSample.CrankOmega));
                        if(forkChoice.Action!=following.Action)throw new InvalidOperationException("Fork private controller state mismatch");
                    }
                    writer.WriteLine(JsonSerializer.Serialize(new{state.Id,state.Trial,state.Reason,Evaluation=evaluation},Json));writer.Flush();
                    var compact=evaluation with{BaselinePath=evaluation.BaselinePath is {} a?a with{Points=[]}:null,ProposedPath=evaluation.ProposedPath is {} b?b with{Points=[]}:null};
                    summaries.Add(new{state.Id,state.Trial,Evaluation=compact});
                    Console.WriteLine($"{state.Id}: admitted={evaluation.Admitted} oldPrepare={evaluation.OldCanPrepare} CPU={evaluation.CpuSeconds:F3} wall={evaluation.Milliseconds:F1}ms {evaluation.Reason}");
                }
            }
        }
        Save(At("-summary.json"),summaries);
    }
}

internal static class Program174
{
    public static int Main(string[] args)
    {
        if(args.Length!=3||args[0] is not ("states" or "extra" or "develop" or "integrate" or "validate"))
        {Console.Error.WriteLine("Study #174 OFF. Explicit stage, unique label, evidence directory required.");return 2;}
        var study=new Context174(args[0],args[1],args[2]);
        using var gate=new FileStream(Path.Combine(args[2],".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        using(var sources=ZipFile.Open(study.At("-source.zip"),ZipArchiveMode.Create))foreach(var source in Directory.GetFiles(Context174.Tool).Where(p=>p.EndsWith(".cs")||p.EndsWith(".py")||p.EndsWith(".csproj")))
            sources.CreateEntryFromFile(source,Path.GetFileName(source));
        Context174.Save(study.At(".started.json"),new{Stage=args[0],PreviousCpu=study.Prior,PreviousWorlds=study.PriorWorlds,
            Sources=Directory.GetFiles(Context174.Tool,"*.cs").Concat(Directory.GetFiles(Context174.Tool,"*.py")).ToDictionary(p=>p.Replace('\\','/'),Context174.Hash)});
        string? error=null;
        try{if(args[0] is "states" or "extra")study.States();else if(args[0]=="develop")Native174.Develop(study);else throw new InvalidOperationException("Full S3 stages gated until native safety and decision-cost gate pass");return 0;}
        catch(Exception e){error=e.ToString();Console.Error.WriteLine(e);return 1;}
        finally{Context174.Save(study.At(".done.json"),new{Stage=args[0],CpuSeconds=Study172.Cpu-study.Began,NativeWorlds=study.Worlds,Error=error});}
    }
}
