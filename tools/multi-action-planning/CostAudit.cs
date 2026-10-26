using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SharedWorldSpeedway;

internal static class CostAudit135
{
    static T Read<T>(string d,string file){var path=Path.Combine(d,file);if(File.Exists(path))return MultiActionExperiment135.Read<T>(d,file);using var z=new GZipStream(File.OpenRead(path+".gz"),CompressionMode.Decompress);return JsonSerializer.Deserialize<T>(z,RobustLearningExperiment.Json)!;}
    internal static void Run(string d){
        var states=Read<State135[]>(d,"diagnostic-states.json").Concat(Read<State135[]>(d,"confirm-states.json")).ToArray();
        var searches=Read<JsonElement[]>(d,"diagnostic-search.json").Concat(Read<JsonElement[]>(d,"confirm-search.json")).ToArray();
        var cpuLimit=Read<JsonElement>(d,"budget-freeze.json").GetProperty("CpuLimitSeconds").GetDouble();
        using var budget=new RobustBudget(d,new Dictionary<string,int>{{"native",96},{"offline",60}},96,cpuLimit,25000000);
        var reservation=budget.Reserve("postflight-sequence-cost","offline",false);var start=Process.GetCurrentProcess().TotalProcessorTime;long contacts=0,forecasts=0;var rows=new List<object>();
        foreach(var index in new[]{0,2,5,8,9,12,16,20})foreach(var arm in new[]{"B","C"}){
            var state=states[index];var saved=searches.Single(x=>x.GetProperty("State").GetString()==state.Name);var actions=saved.GetProperty(arm=="B"?"ChosenTwo":"ChosenThree").Deserialize<FreeRacingAction[]>(RobustLearningExperiment.Json)!;
            var f=TrackAwarenessExperiment134.Frame(state.Case.Geometry);var surface=MultiActionExperiment135.Surface(f);var spec=FreeLearningExperiment.Rider(state.Case.Scenario);var p=FreeLearningExperiment.Parameters();
            var warm=MultiActionPlanner135.Predict(state.Input,actions,spec,f,surface,p);contacts+=warm.ForceEvaluations;forecasts++;
            var before=Process.GetCurrentProcess().TotalProcessorTime;long evaluations=0;
            for(int repeat=0;repeat<20;repeat++){var q=MultiActionPlanner135.Predict(state.Input,actions,spec,f,surface,p);evaluations+=q.ForceEvaluations;forecasts++;}
            var cpu=(Process.GetCurrentProcess().TotalProcessorTime-before).TotalSeconds;contacts+=evaluations;
            rows.Add(new{State=state.Name,Arm=arm,Repetitions=20,Warmups=1,ForecastCpuSeconds=cpu,CpuSecondsPerSequence=cpu/20,ContactEvaluations=evaluations,ContactEvaluationsPerSequence=evaluations/20d});
        }
        var total=(Process.GetCurrentProcess().TotalProcessorTime-start).TotalSeconds;budget.Complete(reservation,total,contacts,forecasts);
        MultiActionExperiment135.Save(d,"sequence-cost.json",new{NativeWorlds=0,CpuSeconds=total,Forecasts=forecasts,ContactEvaluations=contacts,Checks=rows,Scope="Postflight cost only. Frozen model and previously chosen sequences;1warmup+20 timed repetitions. No search, native futures, tuning or thresholds changed. CPU timer quantized; batch averages, same process."});
    }
}
