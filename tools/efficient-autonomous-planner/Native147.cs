using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autonomous146;
using SharedWorldSpeedway;
namespace Efficient147;
public sealed record Trial147(string Name,bool Enabled=false,RuntimeOptions147? Runtime=null,double MaximumCpuSeconds=350);
public static class Native147{
    public static void Run(string directory,string trialPath){
        var trial=JsonSerializer.Deserialize<Trial147>(File.ReadAllText(trialPath),FreeLearningExperiment.Json)!;
        if(!trial.Enabled)throw new InvalidOperationException("Study147 native controller is OFF by default.");
        if(trial.Name.Length<1||trial.Name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("Simple unique trial name required.");
        var existing=JsonNode.Parse(File.ReadAllText("docs/experiments/efficient-autonomous-planner/budget.json"))!.AsArray();
        if(existing.Any(x=>x!["NativeWorlds"]!.GetValue<int>()>0))throw new IOException("One complete native attempt per Study147 experiment; inspect it before any further work.");
        var output=Path.GetFullPath(directory);Directory.CreateDirectory(output);using var budget=new Budget147(trial.Name,1,trial.MaximumCpuSeconds);
        var journalPath=Path.Combine(output,trial.Name+"-decisions.jsonl");using var journal=new Journal147(journalPath);var json=FreeLearningExperiment.Json;
        var process=Process.GetCurrentProcess();Planner146? planner=null;ExactRuntime147? runtime=null;double journalCpu=0;var costs=new List<double>();
        Decision144 Choose(FreeRacingInput input,CrankClutch144 crank,RiderSpec spec,TrackFrame frame,CoreSim.TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,string variant){
            var entered=process.TotalProcessorTime;budget.Guard();
            runtime??=new(spec,frame,surface,p,trial.Runtime??new(EventSearch:true,WorkBudget:true,ProposalsPerThrottle:2,MaximumActualForecasts:8192,MaximumContacts:700000,OptionalSearchContacts:500000),mass,inertia);
            planner??=new(spec,frame,surface,p,new(Enabled:true),mass,inertia,budget.Guard,runtime.Hooks());
            var f=runtime.ActualForecasts;var c=runtime.ActualContacts;var s=runtime.SeedRequests;var hits=runtime.PredictionHits;
            Decision144 decision;
            try{decision=planner.Choose(input,crank);}
            catch(InvalidOperationException error){journal.Append(new{Event="abstain",input.Time,Observed=input,CrankOmega=crank.Omega,Reason=error.Message,runtime.BudgetReason,Progress=planner.LastProgress,Counts=runtime.Counters(),CpuSeconds=(process.TotalProcessorTime-entered).TotalSeconds});throw;}
            var review=planner.Reviews[^1];var plan=review.Selected!;
            var physicalForecasts=checked((int)(runtime.ActualForecasts-f));var contacts=checked((int)(runtime.ActualContacts-c));
            var planningCpu=(process.TotalProcessorTime-entered).TotalSeconds;
            var beganJournal=process.TotalProcessorTime;
            journal.Append(new{Event="decision",input.Time,Observed=input,CrankOmega=crank.Omega,Selected=decision.Choice.Action,
                Disposition=plan.Origin=="shifted-incumbent"?"retained and freshly certified":"newly certified sequence",Origin=plan.Origin,review.Reason,
                BudgetFallback=runtime.BudgetReason,review.ExpectedAction,review.FollowedExpected,review.PredictionError,
                PlannedActions=plan.Actions,plan.GoalMeters,plan.SafetyGoalMeters,plan.GoalTime,plan.CornerExitTime,plan.CornerExitSpeed,PredictedGoalState=plan.Terminal,PredictedEnd=plan.End,
                Certificate=plan.Safety,Independent=review.IndependentSafety,LogicalForecasts=decision.Choice.Forecasts,ActualForecasts=physicalForecasts,ActualContacts=contacts,SeedCalls=runtime.SeedRequests-s,CacheHits=runtime.PredictionHits-hits,
                PlanningCpuSeconds=planningCpu,Profile=runtime.Profile!.Snapshot()});
            journalCpu+=(process.TotalProcessorTime-beganJournal).TotalSeconds;runtime.Completed(input,plan);planner.Reviews.Clear();
            var total=(process.TotalProcessorTime-entered).TotalSeconds;costs.Add(total);
            return new(decision.Choice with{Forecasts=physicalForecasts,ForceEvaluations=contacts},total);
        }
        Run144 run;using(var world=new DiagnosticWorld145(new(Enabled:true,RequiredLaps:3,MaximumSeconds:120)))run=world.Run(trial.Name,"K1",Choose);
        journal.Append(new{Event="summary",run.Outcome,run.EndSeconds,run.Fingerprint,CompletedLaps=run.Riders[0].CompletedLaps,NativeDecisions=run.Riders[0].Decisions.Length,run.Crossings});
        using(var z=new GZipStream(File.Create(Path.Combine(output,trial.Name+".json.gz")),CompressionLevel.Fastest))JsonSerializer.Serialize(z,run,json);
        var rows=Journal147.ReadComplete(journalPath);var decisions=rows.Where(r=>r.GetProperty("Event").GetString()=="decision").ToArray();var native=run.Riders[0].Decisions;
        var complete=decisions.Length==native.Length&&decisions.Zip(native).All(q=>Math.Abs(q.First.GetProperty("Time").GetDouble()-q.Second.Choice.Time)<1e-8&&q.First.GetProperty("Selected").Deserialize<FreeRacingAction>(json)==q.Second.Choice.Action);
        double? Percentile(double fraction)=>costs.Count==0?null:costs.Order().ElementAt(Math.Min(costs.Count-1,(int)((costs.Count-1)*fraction)));
        var rider=run.Riders[0];var minimum=rider.Samples.Where(s=>s.Certificate is not null).Select(s=>s.Certificate!.Value).DefaultIfEmpty(double.NegativeInfinity).Min();
        var pass=run.Outcome=="completed"&&rider.CompletedLaps==3&&complete&&minimum>=0&&rider.Samples[0].Input.Rider.Speed==0&&rider.Samples[0].Input.Omega==0;
        var result=new{Trial=trial,TechnicalPass=pass,SportingImprovement=pass&&rider.FinishSeconds<54.794529838609535,run.Outcome,run.EndSeconds,rider.CompletedLaps,rider.FinishSeconds,run.Fingerprint,
            JournalComplete=complete,JournalDecisions=decisions.Length,NativeDecisions=native.Length,JournalSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(journalPath))),
            MinimumInner=rider.Samples.Min(s=>s.Inner),MinimumOuter=rider.Samples.Min(s=>s.Outer),MinimumSweepCertificate=minimum,
            run.TotalCpuSeconds,run.ControllerCpuSeconds,run.NativeAndObservationCpuSeconds,JournalCpuSeconds=journalCpu,DecisionCpuSum=costs.Sum(),DecisionCpuMedian=Percentile(.5),DecisionCpuP95=Percentile(.95),DecisionCpuMaximum=costs.Count==0?(double?)null:costs.Max(),
            run.Forecasts,run.ForecastContactQueries,run.NativeSteps,run.NativeContactQueries,run.NativeClutchWheelQueries,Runtime=runtime?.Counters(),CpuSeconds=budget.Elapsed,
            PhysicsSource="Unchanged Contact144/crank/tyres/Box2D; only optional immutable boundary reuse in the original predictor.",Scope="One autonomous continuous native standing start from gate B. No saved native controls or body-state restoration."};
        File.WriteAllText(Path.Combine(output,trial.Name+"-result.json"),JsonSerializer.Serialize(result,json));budget.Finish(pass?"technical PASS":"PARTIAL: "+run.Outcome);
        Console.WriteLine(JsonSerializer.Serialize(result,json));if(!complete)throw new IOException("Native decisions missing from durable journal; no PASS permitted.");
    }
}
