using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SharedWorldSpeedway;
namespace Efficient147;
/// <summary>Read-only native/journal audit after a post-run reader failure. Does not create a world or choose controls.</summary>
public static class Finalize147{
    public static void Run(string directory){
        var output=Path.GetFullPath(directory);var resultPath=Path.Combine(output,"autonomous147-shortlist-result.json");if(File.Exists(resultPath))throw new IOException("Preserve completed native audit.");
        using var budget=new Budget147("finalize-native-audit",0,10);var json=FreeLearningExperiment.Json;
        using var zip=new GZipStream(File.OpenRead(Path.Combine(output,"autonomous147-shortlist.json.gz")),CompressionMode.Decompress);var run=JsonSerializer.Deserialize<Run144>(zip,json)!;
        var path=Path.Combine(output,"autonomous147-shortlist-decisions.jsonl");var all=Journal147.ReadComplete(path);var rows=all.Where(x=>x.GetProperty("Event").GetString()=="decision").ToArray();var native=run.Riders[0].Decisions;
        bool complete=rows.Length==native.Length&&rows.Zip(native).All(q=>Math.Abs(q.First.GetProperty("Time").GetDouble()-q.Second.Choice.Time)<1e-8&&q.First.GetProperty("Selected").Deserialize<FreeRacingAction>(json)==q.Second.Choice.Action);
        bool modelCertificates=rows.All(x=>x.GetProperty("Certificate").GetProperty("Legal").GetBoolean()&&x.GetProperty("Independent").GetProperty("Legal").GetBoolean());
        var rider=run.Riders[0];var minimum=rider.Samples.Where(s=>s.Certificate is not null).Min(s=>s.Certificate!.Value);var costs=native.Select(x=>x.CpuSeconds).Order().ToArray();
        bool zero=rider.Samples[0].Input.Rider.Speed==0&&rider.Samples[0].Input.Omega==0&&rider.Gate==CoreSim.StartingGate.B;
        bool pass=run.Outcome=="completed"&&rider.CompletedLaps==3&&run.Crossings.Length==3&&complete&&modelCertificates&&minimum>=0&&zero&&run.ControllerCpuSeconds<410.140625;
        var result=new{TechnicalPass=pass,SportingImprovement=pass&&rider.FinishSeconds<54.794529838609535,run.Outcome,run.EndSeconds,rider.CompletedLaps,rider.FinishSeconds,run.Fingerprint,
            JournalComplete=complete,JournalDecisions=rows.Length,NativeDecisions=native.Length,JournalHashChainValid=true,JournalSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),ModelCertificatesAllLegal=modelCertificates,ZeroStartFromGateB=zero,
            MinimumInner=rider.Samples.Min(s=>s.Inner),MinimumOuter=rider.Samples.Min(s=>s.Outer),MinimumSweepCertificate=minimum,
            run.TotalCpuSeconds,run.ControllerCpuSeconds,run.NativeAndObservationCpuSeconds,
            PlanningCpuSeconds=rows.Sum(x=>x.GetProperty("PlanningCpuSeconds").GetDouble()),PostPlanningChooserCpuSeconds=run.ControllerCpuSeconds-rows.Sum(x=>x.GetProperty("PlanningCpuSeconds").GetDouble()),
            DecisionCpuSum=costs.Sum(),DecisionCpuMedian=costs[costs.Length/2],DecisionCpuP95=costs[(int)((costs.Length-1)*.95)],DecisionCpuMaximum=costs[^1],
            run.Forecasts,run.ForecastContactQueries,run.NativeSteps,run.NativeContactQueries,run.NativeClutchWheelQueries,
            SeedRequests=rows.Sum(x=>x.GetProperty("SeedCalls").GetInt64()),PredictionCacheHits=rows.Sum(x=>x.GetProperty("CacheHits").GetInt64()),
            MissingNonessentialMetrics="Final runtime seed-hit/cross-review-hit counters were not in the first summary. SeedRequests includes memo hits; it is not labelled an exact count of full K1 executions.",
            Recovery="The native world and all 270 write-ahead decision records completed before a Windows read-sharing exception in post-processing. This command only validates and finalizes those existing files. No second native run or reconstructed control action.",
            NativeControlSourceCommit="1607ae7",CpuSeconds=budget.Elapsed};
        File.WriteAllText(resultPath,JsonSerializer.Serialize(result,json));budget.Finish(pass?"technical PASS and complete durable audit":"PARTIAL audit");Console.WriteLine(JsonSerializer.Serialize(result,json));
        if(!pass)throw new InvalidOperationException("Complete native PASS criteria not all satisfied.");
    }
}
