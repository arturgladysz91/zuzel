using System.Text.Json;
using System.IO.Compression;
using SharedWorldSpeedway;

internal static class MechanismAudit135
{
    internal static void Run(string d,string raw){
        var state=MultiActionExperiment135.Read<State135[]>(d,"confirm-states.json").Single(s=>s.Name=="V-C-preparation2");
        using var z=new GZipStream(File.OpenRead(Path.Combine(d,"confirm-search.json.gz")),CompressionMode.Decompress);
        var searches=JsonSerializer.Deserialize<JsonElement[]>(z,RobustLearningExperiment.Json)!;var original=searches.Single(s=>s.GetProperty("State").GetString()==state.Name).GetProperty("ChosenTwo").Deserialize<FreeRacingAction[]>(RobustLearningExperiment.Json)!;
        var controls=new[]{new{Arm="first-only",Actions=new[]{original[0]}},new{Arm="repeat-first",Actions=new[]{original[0],original[0]}}};
        var plan=Path.Combine(d,"mechanism-audit-plan.json");if(File.Exists(plan))throw new IOException("Do not overwrite mechanism plan.");
        MultiActionExperiment135.Save(d,"mechanism-audit-plan.json",new{State=state.Name,state.Time,OriginalSequence=original,Controls=controls,NativeWorldsBefore=MultiActionExperiment135.Read<RobustReservation[]>(d,"attempts.json").Sum(e=>e.NativeWorlds),Scope="Post-hoc mechanistic diagnostic after main sealed pool. Two controls frozen before their native outcomes. Never counted as additional independent evidence, no planner/model selection or GO threshold changes."});
        var cpu=MultiActionExperiment135.Read<JsonElement>(d,"budget-freeze.json").GetProperty("CpuLimitSeconds").GetDouble();
        using var b=new RobustBudget(d,new Dictionary<string,int>{{"native",96},{"offline",60}},96,cpu,25000000);
        var baseline=RobustLearningExperiment.ReadEpisode(raw,"baseline-V-C");var rows=new List<object>();
        foreach(var control in controls){
            var run=MultiActionExperiment135.Native(d,raw,b,"mechanism-"+control.Arm,state.Case,state.Time,control.Actions);
            var prefix=MultiActionExperiment135.Prefix(baseline,run,state.Time,RiderController.ReactionSeconds(FreeLearningExperiment.Rider(state.Case.Scenario)));
            rows.Add(new{control.Arm,control.Actions,run.Replay.Name,run.Metrics,ExactPrefix=prefix});if(!prefix)throw new InvalidOperationException("Mechanism prefix mismatch.");
            MultiActionExperiment135.Save(d,"mechanism-controls.json",rows);
        }
    }
}
