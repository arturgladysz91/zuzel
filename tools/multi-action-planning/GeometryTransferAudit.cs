using SharedWorldSpeedway;

// Fixed-plan transfer, explicitly after the main results. No foreign-world result
// changes the previously chosen plan, causal planner, or independent-pool verdict.
internal static class GeometryTransferAudit135
{
    internal static void Run(string d,string raw){
        var state=MultiActionExperiment135.Read<State135[]>(d,"confirm-states.json").Single(s=>s.Name=="V-C-preparation2");
        var plan=MultiActionExperiment135.Read<System.Text.Json.JsonElement>(d,"mechanism-audit-plan.json");
        var actions=System.Text.Json.JsonSerializer.Deserialize<FreeRacingAction[]>(plan.GetProperty("OriginalSequence"),RobustLearningExperiment.Json)!;
        var filename=Path.Combine(d,"geometry-transfer-plan.json");if(File.Exists(filename))throw new IOException("Do not overwrite transfer plan.");
        MultiActionExperiment135.Save(d,"geometry-transfer-plan.json",new{state.Time,Actions=actions,Scenario=state.Case.Scenario,Geometries=new[]{"progressive-c","progressive-b","egg-asymmetric"},
            NewWorlds=4,CachedWorlds=2,Scope="Post-hoc fixed-plan transfer. Identical full initial scenario, original134A, same observation times and commands. Reuse the original C pair; no extra search, tuning or independent success count."});
        var cpu=MultiActionExperiment135.Read<System.Text.Json.JsonElement>(d,"budget-freeze.json").GetProperty("CpuLimitSeconds").GetDouble();
        using var budget=new RobustBudget(d,new Dictionary<string,int>{{"native",96},{"offline",60}},96,cpu,25000000);
        var reference=RobustLearningExperiment.ReadEpisode(raw,"baseline-V-C");var rows=new List<object>();
        foreach(var geometry in new[]{"progressive-c","progressive-b","egg-asymmetric"}){
            var c=state.Case with{Name="transfer-"+geometry,Geometry=geometry};
            var baseline=geometry=="progressive-c"?reference:MultiActionExperiment135.Native(d,raw,budget,"transfer-base-"+geometry,c);
            var trial=geometry=="progressive-c"?RobustLearningExperiment.ReadEpisode(raw,"V-C-preparation2-B"):MultiActionExperiment135.Native(d,raw,budget,"transfer-plan-"+geometry,c,state.Time,actions);
            var initial=baseline.Replay.Samples[0].Input;var original=reference.Replay.Samples[0].Input;
            bool same=initial.Rider==original.Rider&&initial.Active==original.Active&&initial.Steering==original.Steering&&initial.Omega==original.Omega;
            bool prefix=MultiActionExperiment135.Prefix(baseline,trial,state.Time,RiderController.ReactionSeconds(FreeLearningExperiment.Rider(c.Scenario)));
            rows.Add(new{Geometry=geometry,Scenario=c.Scenario,StateTime=state.Time,Actions=actions,SameInitialPhysicalState=same,ExactPrefix=prefix,BaselineName=baseline.Replay.Name,TrialName=trial.Replay.Name,Baseline=baseline.Metrics,Trial=trial.Metrics});
            MultiActionExperiment135.Save(d,"geometry-transfer.json",rows);if(!same||!prefix)throw new InvalidOperationException("Transfer initial conditions/prefix differ.");
        }
    }
}
