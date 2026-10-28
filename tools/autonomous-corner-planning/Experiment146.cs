using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharedWorldSpeedway;
namespace Autonomous146;
public sealed record Treatment146(string Name,string PrefixVariant="K1",double StartSeconds=0,double MaximumSeconds=120,double CpuLimitSeconds=260,string? PlanSource=null);
public static class Experiment146{
    public static void Run(string directory,string treatmentPath){
        var output=Path.GetFullPath(directory);Directory.CreateDirectory(output);var json=FreeLearningExperiment.Json;
        void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
        string Hash(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,json)));
        var gate="docs/experiments/autonomous-corner-planning/stage-A/sequence-checks.json";
        var diagnosed=JsonNode.Parse(File.ReadAllText(gate))!;
        if(diagnosed["Checks"]!.AsArray().Any(c=>!c!["PrefixExact"]!.GetValue<bool>()||c["Agreement"] is not null&&!c["Agreement"]!["Passed"]!.GetValue<bool>()))throw new IOException("Stage A gate failed.");
        var treatments=JsonSerializer.Deserialize<Treatment146[]>(File.ReadAllText(treatmentPath),json)!;
        var ledgerPath=Path.Combine(output,"budget.json");using var lease=new FileStream(Path.Combine(output,"budget.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var ledger=File.Exists(ledgerPath)?JsonNode.Parse(File.ReadAllText(ledgerPath))!.AsArray():new JsonArray();
        if(ledger.Any(x=>!x!["Complete"]!.GetValue<bool>()))throw new IOException("Inspect incomplete native attempt before retry.");
        const double StageACpuSeconds=6.515625;const int StageAWorlds=3;
        foreach(var treatment in treatments){
            if(ledger.Any(x=>x!["Name"]!.GetValue<string>()==treatment.Name))continue;
            var spent=ledger.Sum(x=>x!["CpuSeconds"]!.GetValue<double>())+StageACpuSeconds;
            if(ledger.Count+StageAWorlds>=50||spent>=890)throw new IOException("Study146 native budget exhausted.");
            if(treatment.StartSeconds<0||Math.Abs(treatment.StartSeconds/.2-Math.Round(treatment.StartSeconds/.2))>1e-7)throw new ArgumentException("Start at an original review.");
            using var baselineStream=new GZipStream(File.OpenRead($"docs/experiments/standing-start-three-laps/native/{treatment.PrefixVariant}.json.gz"),CompressionMode.Decompress);
            var baseline=JsonSerializer.Deserialize<Run144>(baselineStream,json)!;var old=baseline.Riders[0];
            var reservation=new JsonObject{["Name"]=treatment.Name,["Complete"]=false,["CpuSeconds"]=0};ledger.Add(reservation);Save("budget.json",ledger);
            var started=Process.GetCurrentProcess().TotalProcessorTime;Planner146? planner=null;
            Plan146? validationPlan=null;
            if(treatment.PlanSource is not null){using var z=new GZipStream(File.OpenRead(Path.Combine(output,treatment.PlanSource+"-plans.json.gz")),CompressionMode.Decompress);
                validationPlan=JsonSerializer.Deserialize<Review146[]>(z,json)!.First(r=>r.Selected is not null).Selected!;
                if(Math.Abs(validationPlan.CreatedAt-treatment.StartSeconds)>1e-7)throw new ArgumentException("Validation requires the full native prefix of the selected plan.");}
            Decision144 Choose(FreeRacingInput i,CrankClutch144 c,RiderSpec s,TrackFrame f,CoreSim.TrackStateSnapshot t,WorldParameters p,float m,float inertia,string variant){
                if((Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds>Math.Min(treatment.CpuLimitSeconds,890-spent))throw new InvalidOperationException("STUDY146_BUDGET: native CPU reserve stop");
                if(i.Time<treatment.StartSeconds-1e-8){var recorded=old.Decisions.First(d=>Math.Abs(d.Choice.Time-i.Time)<1e-7);return new(recorded.Choice with{Candidates=Array.Empty<FreeLearningCandidate>(),Forecasts=0,ForceEvaluations=0},0);}
                if(validationPlan is not null){int k=(int)Math.Round((i.Time-validationPlan.CreatedAt)/.2);
                    if(k<validationPlan.Actions.Length)return new(new(i.Time,validationPlan.Actions[k],FreeActionSafety.Uncertain,false,Array.Empty<FreeLearningCandidate>(),0,0),0);
                    return Policy144.Choose(i,c,s,f,t,p,m,inertia,"K1");}
                planner??=new(s,f,t,p,new(Enabled:true),m,inertia);
                var decision=planner.Choose(i,c);
                if(planner.Reviews.Count%10==0)Console.WriteLine($"{treatment.Name}: t={i.Time:F1}s CPU={(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds:F2}s");
                return decision;
            }
            Run144 run;using(var world=new DiagnosticWorld145(new(Enabled:true,MaximumSeconds:treatment.MaximumSeconds)))run=world.Run(treatment.Name,"K1",Choose);
            void Zip(string name,object value){using var z=new GZipStream(File.Create(Path.Combine(output,name)),CompressionLevel.SmallestSize);JsonSerializer.Serialize(z,value,json);}
            Zip(treatment.Name+".json.gz",run);Zip(treatment.Name+"-plans.json.gz",planner?.Reviews??new());
            var prefix=run.Riders[0].Samples.Where(s=>s.Input.Time<=treatment.StartSeconds+1e-8).ToArray();var expected=old.Samples.Where(s=>s.Input.Time<=treatment.StartSeconds+1e-8).ToArray();
            var exact=treatment.StartSeconds==0?
                prefix.Length==1&&prefix[0].Input.Time==0&&prefix[0].Input.Rider.Position==expected[0].Input.Rider.Position&&prefix[0].Input.Rider.Velocity==System.Numerics.Vector2.Zero&&prefix[0].Input.Omega==0&&prefix[0].Input.Steering==0&&prefix[0].CrankOmega==expected[0].CrankOmega:
                prefix.Length==expected.Length&&prefix.Zip(expected).All(q=>Hash(q.First)==Hash(q.Second));
            object? planAgreement=null;
            if(validationPlan is not null){double xy=0,speed=0,yaw=0;int count=0;
                foreach(var point in validationPlan.Points){var sample=run.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-point.Time))!;if(Math.Abs(sample.Input.Time-point.Time)>1e-7)continue;
                    xy=Math.Max(xy,System.Numerics.Vector2.Distance(point.Rider.Position,sample.Input.Rider.Position));speed=Math.Max(speed,Math.Abs(point.Rider.Speed-sample.Input.Rider.Speed));yaw=Math.Max(yaw,Math.Abs(point.Rider.AngularVelocity-sample.Input.Rider.AngularVelocity));count++;}
                double? actualGoal=null;var ss=run.Riders[0].Samples;
                for(int k=1;k<ss.Length;k++)if(ss[k-1].Input.Rider.TotalReferenceMeters<validationPlan.GoalMeters&&ss[k].Input.Rider.TotalReferenceMeters>=validationPlan.GoalMeters){var a=ss[k-1].Input;var b=ss[k].Input;actualGoal=a.Time+(b.Time-a.Time)*(validationPlan.GoalMeters-a.Rider.TotalReferenceMeters)/(b.Rider.TotalReferenceMeters-a.Rider.TotalReferenceMeters);break;}
                planAgreement=new{Points=count,MaximumXYMeters=xy,MaximumSpeedMps=speed,MaximumYawRate=yaw,PredictedGoalTime=validationPlan.GoalTime,NativeGoalTime=actualGoal,GoalErrorSeconds=actualGoal-validationPlan.GoalTime,
                    Passed=count>=validationPlan.Points.Length*.8&&xy<.03&&speed<.03&&yaw<.01&&actualGoal is not null&&Math.Abs((actualGoal-validationPlan.GoalTime)??1)<.01};
            }
            var result=new{Treatment=treatment,PlanAgreement=planAgreement,run.Outcome,run.EndSeconds,run.Fingerprint,PrefixExact=exact,PrefixSamples=prefix.Length,
                PrefixFingerprint=Hash(prefix),HistoricalPrefixFingerprint=Hash(expected),CompletedLaps=run.Riders[0].CompletedLaps,run.Crossings,
                MinimumInner=run.Riders[0].Samples.Min(s=>s.Inner),MinimumOuter=run.Riders[0].Samples.Min(s=>s.Outer),
                run.TotalCpuSeconds,run.ControllerCpuSeconds,run.NativeSteps,run.Forecasts,run.ForecastContactQueries,
                Reviews=planner?.Reviews.Count??0,LastReview=planner?.Reviews.LastOrDefault() is { } last?new{last.Time,last.Reason,last.Candidates}:null,
                CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds,
                Scope=validationPlan is not null?"Forced execution of a previously autonomous forecast for model validation only; no claim of autonomous success.":treatment.StartSeconds==0?"Autonomous continuous native standing start; no saved commands used.":"Local autonomous suffix, full exact native standing-start prefix; not a full autonomous success."};
            reservation["Complete"]=true;reservation["CpuSeconds"]=result.CpuSeconds;Save("budget.json",ledger);Save(treatment.Name+"-result.json",result);
            Console.WriteLine(JsonSerializer.Serialize(result,json));
            if(!exact)throw new IOException("Native prefix differs; reject comparison.");
        }
    }
}
