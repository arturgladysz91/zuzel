using System.IO.Compression;
using System.Text.Json;
using Autonomous146;
using SharedWorldSpeedway;
namespace Efficient147;
public static class Benchmark147{
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    static T ReadZip<T>(string p){using var z=new GZipStream(File.OpenRead(p),CompressionMode.Decompress);return JsonSerializer.Deserialize<T>(z,Json)!;}
    public static void Run(string directory){
        var output=Path.GetFullPath(directory);Directory.CreateDirectory(output);
        var run=ReadZip<Run144>("docs/experiments/autonomous-corner-planning/native/autonomous146-viable.json.gz");var rider=run.Riders[0];var frame=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(frame);var p=FreeLearningExperiment.Parameters();
        var before=ReadZip<Memory146>("docs/experiments/efficient-autonomous-planner/profiles/memory-before-22-6.json.gz");
        var rows=File.ReadLines("docs/experiments/efficient-autonomous-planner/profiles/replay-profiles.jsonl").Select(s=>JsonSerializer.Deserialize<Baseline147>(s,Json)!).ToArray();var reference=rows[^1];
        foreach(var mode in new[]{"exact-0","exact-22-6","budget-only-22-6","buffered-events-22-6","retained-tape-22-6","retained-budget-22-6","retained-budget-0","shortlist-22-6","shortlist-0"}){
            var path=Path.Combine(output,mode+".json");if(File.Exists(path))continue;
            using var budget=new Budget147(mode,0,150);var at=mode.EndsWith("-0")?0:reference.Time;
            var sample=rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-at))!;var input=sample.Input with{Time=at,NextReview=at,Pending=sample.Input.Pending.Where(c=>c.ObservedAt<at-1e-8).ToArray()};
            bool bounded=mode.StartsWith("budget")||mode.StartsWith("buffered")||mode.StartsWith("retained")||mode.StartsWith("shortlist");
            bool workBudget=mode.StartsWith("budget")||mode.StartsWith("buffered")||mode.StartsWith("retained-budget")||mode.StartsWith("shortlist");var runtime=new ExactRuntime147(rider.Spec,frame,surface,p,new(EventSearch:mode.StartsWith("buffered")||mode.StartsWith("retained"),WorkBudget:workBudget,BufferedTail:mode.StartsWith("buffered"),RetainProposalTape:mode.StartsWith("retained"),ProposalsPerThrottle:mode.StartsWith("shortlist")?2:0,MaximumActualForecasts:mode.StartsWith("shortlist")?8192:4096,MaximumContacts:mode.StartsWith("shortlist")?700000:400000,OptionalSearchContacts:mode.StartsWith("shortlist")?500000:100000));
            var planner=new Planner146(rider.Spec,frame,surface,p,new(Enabled:true),budgetGuard:budget.Guard,hooks:runtime.Hooks());
            if(at>0){planner.RestoreMemoryForDiagnostics(before);var prior=rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-(at-.2)))!;runtime.InitializeHistory(prior.Input,before.Previous);}
            Decision144? decision=null;string? error=null;
            try{decision=planner.Choose(input,new CrankClutch144(omega:sample.CrankOmega));}catch(InvalidOperationException e){error=e.Message;}
            var review=planner.Reviews.LastOrDefault();var selected=review?.Selected;
            var expectedAction=at==0?rider.Decisions[0].Choice.Action:reference.Action;
            var result=new{Mode=mode,At=at,StrategyChanged=bounded,Outcome=decision is null?"abstain":"certified decision",Error=error,
                Command=decision?.Choice.Action,CommandMatchesBaseline=decision?.Choice.Action==expectedAction,
                EndStateBitExact=at>0&&selected is not null?ExactKey147.State(selected.End)==ExactKey147.State(reference.End):(bool?)null,
                GoalTimeMatches=at>0?selected?.GoalTime==reference.GoalTime:(bool?)null,
                LogicalForecasts=decision?.Choice.Forecasts,LogicalForecastsMatch=at>0?decision?.Choice.Forecasts==reference.Forecasts:(bool?)null,
                CpuSeconds=budget.Elapsed,Counts=runtime.Counters(),Profile=runtime.Profile?.Snapshot(),Review=review is null?null:new{review.Reason,review.Candidates,review.IndependentSafety,selected?.Safety,selected?.GoalTime,selected?.End}};
            File.WriteAllText(path,JsonSerializer.Serialize(result,Json));budget.Finish(result.Outcome);Console.WriteLine(JsonSerializer.Serialize(new{mode,result.Outcome,result.CommandMatchesBaseline,result.EndStateBitExact,result.CpuSeconds},Json));Console.Out.Flush();
            if(!bounded&&(!result.CommandMatchesBaseline||at>0&&(!result.EndStateBitExact.GetValueOrDefault()||!result.GoalTimeMatches.GetValueOrDefault()||!result.LogicalForecastsMatch.GetValueOrDefault())))throw new InvalidOperationException("Exact optimization changed semantics; stop before native test.");
        }
    }
    sealed record Baseline147(double Time,FreeRacingAction Action,long Forecasts,State146 End,double GoalTime);
}
