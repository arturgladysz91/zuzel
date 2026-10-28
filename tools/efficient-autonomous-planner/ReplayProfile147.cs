using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autonomous146;
using SharedWorldSpeedway;
namespace Efficient147;
public static class ReplayProfile147{
    public static void Run(string directory){
        var output=Path.GetFullPath(directory);Directory.CreateDirectory(output);var json=FreeLearningExperiment.Json;
        var ledgerPath="docs/experiments/efficient-autonomous-planner/budget.json";var ledger=JsonNode.Parse(File.ReadAllText(ledgerPath))!.AsArray();
        if(ledger.Any(x=>x!["Name"]!.GetValue<string>()=="profile-replay"))throw new IOException("Preserve profile attempt.");
        var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var entry=new JsonObject{["Name"]="profile-replay",["Complete"]=false,["NativeWorlds"]=0,["CpuSeconds"]=0};ledger.Add(entry);File.WriteAllText(ledgerPath,ledger.ToJsonString(json));
        using var oldZip=new GZipStream(File.OpenRead("docs/experiments/autonomous-corner-planning/native/autonomous146-viable.json.gz"),CompressionMode.Decompress);
        var run=JsonSerializer.Deserialize<Run144>(oldZip,json)!;var rider=run.Riders[0];var frame=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(frame);var parameters=FreeLearningExperiment.Parameters();
        using var memoryZip=new GZipStream(File.OpenRead("docs/experiments/efficient-autonomous-planner/memory-14-2.json.gz"),CompressionMode.Decompress);
        var saved=JsonSerializer.Deserialize<SavedMemory147>(memoryZip,json)!;var last=saved.LastReview;
        var expected=new Model146(rider.Spec,frame,surface,parameters).Step(new(last.Input,last.CrankOmega),last.Selected!.Actions[0]).End;
        CpuProfile147? profile=null;var planner=new Planner146(rider.Spec,frame,surface,parameters,new(Enabled:true),budgetGuard:()=>{if((process.TotalProcessorTime-cpu).TotalSeconds>280)throw new InvalidOperationException("Study147 profile reserve stop");},
            hooks:new(Phase:n=>profile!.Phase(n),Operation:n=>profile!.Operation(n),BeginReview:_=>profile=new()));
        planner.RestoreMemoryForDiagnostics(new(last.Selected,expected,saved.NextSearch));
        using var stream=new FileStream(Path.Combine(output,"replay-profiles.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read);using var journal=new StreamWriter(stream);var compact=new JsonSerializerOptions(json){WriteIndented=false};
        bool pass=true;string? failure=null;var summaries=new List<object>();
        try{foreach(var original in rider.Decisions.Where(d=>d.Choice.Time>14.3&&d.Choice.Time<22.7)){
            var at=original.Choice.Time;var sample=rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-at))!;
            var input=sample.Input with{Time=at,NextReview=at,Pending=sample.Input.Pending.Where(c=>c.ObservedAt<at-1e-8).ToArray()};
            if(at>22.5){using var z=new GZipStream(File.Create(Path.Combine(output,"memory-before-22-6.json.gz")),CompressionLevel.SmallestSize);JsonSerializer.Serialize(z,planner.CaptureMemory(),json);}
            var chosen=planner.Choose(input,new CrankClutch144(omega:sample.CrankOmega));var review=planner.Reviews[^1];
            var row=new{Time=at,CommandMatches=chosen.Choice.Action==original.Choice.Action,LogicalForecastsMatch=chosen.Choice.Forecasts==original.Choice.Forecasts,
                Action=chosen.Choice.Action,Original=original.Choice.Action,chosen.Choice.Forecasts,chosen.Choice.ForceEvaluations,review.Candidates,review.Reason,review.CpuSeconds,review.Selected!.GoalTime,review.Selected.Safety,
                End=review.Selected.End,Profile=profile!.Snapshot()};
            summaries.Add(row);journal.WriteLine(JsonSerializer.Serialize(row,compact));journal.Flush();stream.Flush(true);
            Console.WriteLine($"t={at:F1} action={row.CommandMatches} count={row.LogicalForecastsMatch} forecasts={row.Forecasts} CPU={row.CpuSeconds:F3}");Console.Out.Flush();
            if(!row.CommandMatches||!row.LogicalForecastsMatch){pass=false;failure="Causal planner-memory reconstruction differs; do not label later profile exact.";break;}
        }}catch(InvalidOperationException error){pass=false;failure=error.Message;}
        var result=new{NativeWorlds=0,Passed=pass,Failure=failure,Rows=summaries.Count,CpuSeconds=(process.TotalProcessorTime-cpu).TotalSeconds};
        File.WriteAllText(Path.Combine(output,"replay-profile-result.json"),JsonSerializer.Serialize(result,json));entry["Complete"]=true;entry["CpuSeconds"]=result.CpuSeconds;File.WriteAllText(ledgerPath,ledger.ToJsonString(json));Console.WriteLine(JsonSerializer.Serialize(result,json));
    }
    sealed record SavedMemory147(Review146 LastReview,double NextSearch);
}
