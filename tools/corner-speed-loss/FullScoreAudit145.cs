using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SharedWorldSpeedway;

/// <summary>Exhaustive read-only decomposition of the frozen four three-lap runs.</summary>
public static class FullScoreAudit145
{
    public static void Run(string output){
        var path=Path.Combine(output,"all-scores.json.gz");
        if(File.Exists(path))throw new IOException("Preserve the exhaustive saved-score audit; use a new output directory.");
        var started=Process.GetCurrentProcess().TotalProcessorTime;var json=FreeLearningExperiment.Json;
        var frame=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(frame);var p=FreeLearningExperiment.Parameters();var policy=Controller143.Policy;
        var audits=new List<object>();var variants=new List<object>();int forecasts=0,totalCandidates=0;double maximum=0;
        foreach(var name in new[]{"A","B","F","K1"}){
            using var z=new GZipStream(File.OpenRead("docs/experiments/standing-start-three-laps/native/"+name+".json.gz"),CompressionMode.Decompress);
            var run=JsonSerializer.Deserialize<Run144>(z,json)!;var rider=run.Riders[0];int candidateCount=0;double localMaximum=0;bool choicesMatch=true;
            foreach(var decision in rider.Decisions){
                if((Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds>400)throw new InvalidOperationException("Exhaustive forecast audit CPU reserve reached; no native worlds were created.");
                var s=rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-decision.Choice.Time))!;
                var input=s.Input with{Time=decision.Choice.Time,NextReview=decision.Choice.Time,Pending=s.Input.Pending.Where(c=>c.ObservedAt<decision.Choice.Time-1e-8).ToArray()};
                var shaft=new CrankClutch144(omega:s.CrankOmega);var leaves=new List<object>();var candidates=new List<FreeLearningCandidate>();
                foreach(var recorded in decision.Choice.Candidates){
                    var prediction=Predictor144.Predict(input,rider.Spec,frame,surface,p,147,52.72097f,.9,recorded.Action,powertrain:shaft);forecasts++;
                    var leaf=Objective142.FromPrediction(input,policy,frame,surface,prediction,recorded.Action);var scored=Controller143.Score(leaf,Policy144.Variant(name));
                    var difference=Math.Abs(scored.Score-recorded.Score);localMaximum=Math.Max(localMaximum,difference);candidateCount++;
                    var candidate=leaf.Candidate with{Score=scored.Score,Safety=scored.Safety};candidates.Add(candidate);
                    leaves.Add(new{candidate.Action,Selected=candidate.Action==decision.Choice.Action,RecordedScore=recorded.Score,Score=scored.Score,ScoreDifference=difference,
                        Safety=scored.Safety,leaf.Terms,RemovedThrottle=name is "B" or "K1"?leaf.Terms.Throttle:0,
                        RemovedGeometry=name is "F" or "K1"?leaf.Terms.Geometry:0,leaf.HardViolation,leaf.MaximumBetaDegrees,
                        leaf.MaximumYawRate,leaf.EndYawRate,Candidate=candidate});
                }
                var selected=candidates.OrderBy(c=>c.Safety).ThenBy(c=>c.Score).ThenBy(c=>Math.Abs(c.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(c=>c.Action.SteeringRadians).First();
                var match=selected.Action==decision.Choice.Action;choicesMatch&=match;
                audits.Add(new{Run=name,Time=decision.Choice.Time,Input=input,s.CrankOmega,Chosen=decision.Choice.Action,ChosenMatches=match,Leaves=leaves});
            }
            variants.Add(new{Run=name,Decisions=rider.Decisions.Length,Candidates=candidateCount,MaximumScoreDifference=localMaximum,AllChoicesMatch=choicesMatch});
            maximum=Math.Max(maximum,localMaximum);totalCandidates+=candidateCount;
            Console.WriteLine($"{name}: {rider.Decisions.Length} decisions, {candidateCount} candidates, maximum score difference {localMaximum:R}, choices match {choicesMatch}");
            if(localMaximum>1e-8||!choicesMatch)throw new InvalidOperationException("Exhaustive recorded-score reconstruction differs; report before promotion.");
        }
        using(var z=new GZipStream(File.Create(path),CompressionLevel.SmallestSize))JsonSerializer.Serialize(z,new{NativeWorlds=0,Audits=audits},json);
        var summary=new{NativeWorlds=0,Decisions=audits.Count,Candidates=totalCandidates,Forecasts=forecasts,MaximumScoreDifference=maximum,Variants=variants,
            CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds,RawSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            Scope="Every chosen and rejected candidate at every saved A/B/F/K1 decision, including start and all six corners. No native worlds or control interventions."};
        File.WriteAllText(Path.Combine(output,"all-scores-summary.json"),JsonSerializer.Serialize(summary,json));
    }
}
