using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SharedWorldSpeedway;

// Postflight instrumentation only. The frozen scientific search/physics is unchanged.
// A stopped world cannot issue or activate a command at a later timestamp.
internal static class FinishAudit135
{
    internal static void Run(string d,string raw){
        var protocol=MultiActionExperiment135.Read<JsonElement>(d,"protocol.json");var sources=MultiActionExperiment135.Sources();
        foreach(var source in protocol.GetProperty("Sources").EnumerateObject())if(sources[source.Name]!=source.Value.GetString())throw new InvalidOperationException("Frozen scientific source changed.");
        var rows=MultiActionExperiment135.Read<List<Pair135>>(d,"confirm-pairs.json");var last=rows.Last();
        if(rows.Count!=17||last.State!="V-E-preparation2"||last.Arm!="C"||!last.ExactPrefix||last.ExactRequests)throw new InvalidOperationException("Only the recorded final stopped-world activation check may be recovered.");
        var original=Path.Combine(d,"confirm-pairs-initial.json.gz");if(File.Exists(original))throw new IOException("Do not overwrite initial failure evidence.");
        using(var z=new GZipStream(File.Create(original),CompressionLevel.SmallestSize))z.Write(File.ReadAllBytes(Path.Combine(d,"confirm-pairs.json")));
        var state=MultiActionExperiment135.Read<State135[]>(d,"confirm-states.json").Last();
        var saved=MultiActionExperiment135.Read<JsonElement[]>(d,"confirm-search.json").Single(x=>x.GetProperty("State").GetString()==state.Name);
        var actions=saved.GetProperty("OracleSample").Deserialize<FreeRacingAction[]>(RobustLearningExperiment.Json)!;
        var leaf=saved.GetProperty("Search").GetProperty("Three").EnumerateArray().Select(x=>x.Deserialize<Sequence135>(RobustLearningExperiment.Json)!).Single(x=>x.Actions.SequenceEqual(actions));
        var baseline=RobustLearningExperiment.ReadEpisode(raw,"baseline-"+state.Case.Name);
        var cpu=MultiActionExperiment135.Read<JsonElement>(d,"budget-freeze.json").GetProperty("CpuLimitSeconds").GetDouble();
        using var budget=new RobustBudget(d,new Dictionary<string,int>{{"native",96},{"offline",60}},96,cpu,25000000);
        var name=state.Name+"-oracle-sample";var native=MultiActionExperiment135.Native(d,raw,budget,name,state.Case,state.Time,actions);
        var reaction=RiderController.ReactionSeconds(FreeLearningExperiment.Rider(state.Case.Scenario));var prefix=MultiActionExperiment135.Prefix(baseline,native,state.Time,reaction);
        var accuracy=MultiActionExperiment135.Accuracy(leaf.Prediction,native,out _);
        var requests=actions.Select((a,j)=>new{Action=a,Time=state.Time+j*.2}).All(q=>native.Replay.Decisions.Any(x=>Math.Abs(x.Time-q.Time)<1e-8&&x.Action==q.Action)&&native.Replay.Activations.Any(x=>Math.Abs(x.ObservedAt-q.Time)<1e-8&&x.Action==q.Action));
        rows.Add(new(state.Name,"oracle-sample",name,actions,leaf.Evaluation,baseline.Metrics,native.Metrics,prefix,requests,accuracy));
        MultiActionExperiment135.Save(d,"confirm-pairs.json",rows);
        var checks=rows.Select(r=>{
            var run=RobustLearningExperiment.ReadEpisode(raw,r.RawName);var origin=MultiActionExperiment135.Read<State135[]>(d,"confirm-states.json").Single(s=>s.Name==r.State);var end=run.Replay.Metrics.DurationSeconds;
            var q=r.Actions.Select((a,j)=>{var at=origin.Time+j*.2;var activation=at+reaction;return new{ObservedAt=at,ActivationAt=activation,Action=a,
                RequestRequiredBeforeStop=at<end-1e-8,ActivationRequiredBeforeStop=activation<end-1e-8,
                Requested=run.Replay.Decisions.Any(x=>Math.Abs(x.Time-at)<1e-8&&x.Action==a),Activated=run.Replay.Activations.Any(x=>Math.Abs(x.ObservedAt-at)<1e-8&&Math.Abs(x.ActivationAt-activation)<1e-8&&x.Action==a)};}).ToArray();
            return new{r.RawName,OriginalAllActivationsRequiredPassed=r.ExactRequests,End=end,run.Replay.Metrics.Outcome,Events=q,Passed=r.ExactPrefix&&q.All(x=>(!x.RequestRequiredBeforeStop||x.Requested)&&(!x.ActivationRequiredBeforeStop||x.Activated))};
        }).ToArray();
        MultiActionExperiment135.Save(d,"postflight-request-audit.json",new{Passed=checks.All(x=>x.Passed),Rule="Require requests/activations only strictly before native termination. Keep absent later activations visible. No scientific code, plan, pool, threshold or raw replay changed.",InitialFailure="confirm-pairs-initial.json.gz",Checks=checks,AuditSourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("tools/multi-action-planning/FinishAudit.cs")))});
        if(checks.Any(x=>!x.Passed))throw new InvalidOperationException("Actual causal queue mismatch, no successful completion claim.");
        MultiActionExperiment135.Save(d,"confirm-complete.json",new{SourceHash=TrackAwarenessExperiment134.Hash(sources),States=6,Pairs=18,Worlds=budget.Entries.Sum(e=>e.NativeWorlds),NativeResultsNeverUsedForCausalChoice=true,OriginalSuiteStopped=true,Recovery="Postflight request audit only; one previously selected remaining oracle trial, no repeated worlds or searches."});
    }
}
