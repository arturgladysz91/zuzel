using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

public static class DecisionAudit
{
    public static void Run(string directory)
    {
        var output=Path.GetFullPath(directory);var json=new JsonSerializerOptions{WriteIndented=true,IncludeFields=true};
        if(!JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"prediction-validation.json"))).RootElement.GetProperty("Passed").GetBoolean())
            throw new InvalidOperationException("Prediction gate must pass first.");
        if(File.Exists(Path.Combine(output,"controller-attempt.json")))throw new InvalidOperationException("Controller experiment already frozen/attempted.");
        var hashes=new[]{"PredictiveRiderControl.cs","PredictiveRiderModel.cs","WorldRunner.cs","WorldModel.cs","PreparedCornerPlan.cs"}
            .ToDictionary(n=>n,n=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output,"../../../tools/shared-world-speedway",n)))));
        File.WriteAllText(Path.Combine(output,"controller-attempt.json"),JsonSerializer.Serialize(new{BaseHead="d8d7e959eab00d0b41236aa6292ab04f3c15f3fa",
            PredictiveRiderControl.HorizonSeconds,PredictiveRiderControl.ReviewSeconds,PredictiveRiderControl.PulseSeconds,PredictiveRiderControl.CorrectionRadians,
            PredictiveRiderControl.MinimumScoreGainSquareMeters,PredictiveRiderControl.ReversalCostSquareMeters,PredictiveRiderControl.ProgressWeightMeters,
            PredictiveRiderControl.PredictionAllowanceMeters,DefaultEnabled=false,MaximumCandidateNativeWorlds=1,MaximumOffVerificationWorlds=1,
            SourceHashes=hashes,RetuningAllowed=false,Rule="Fixed0.6s from model timescale, no selection by native elapsed time. Three actions original/+0.9/-0.9deg after0.24s, pulse0.1s. Keep path/throttle. At plan lifecycle uncertainty retain original controller. Stop first physical failure."},json)+"\n");
        var rows=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous");var planRows=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous-plan");
        var capture=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"../continuous-corner-planning/continuous.json"))).RootElement;
        var commands=capture.GetProperty("Result").GetProperty("Decisions").Deserialize<DecisionEvent[]>(InverseFeasibilityAudit.Json)!;
        var reviews=JsonSerializer.Deserialize<PreparedCornerReview[]>(File.ReadAllText(Path.Combine(output,"../continuous-corner-planning/reviews.json")))!;
        var times=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"protocol.json"))).RootElement.GetProperty("ObservationTimes").Deserialize<double[]>()!;
        var initial=PreparedCornerAudit.InitialPlan();var frame=initial.Path.Frame;var saved=ContinuousCornerAudit.SavedEnd();var spec=CornerEntryAudit.Rider();
        var p=ContinuousCornerAudit.Parameters() with{EnablePredictiveRiderControl=true};var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        var next=new PreparedCornerPlan(saved.Rider,spec,frame,p,saved.Time,147,saved.Wheel,saved.Steering,null,surface,new(saved.Old.Path,saved.Old.ControlMemory,saved.Pending));
        var records=new List<PredictiveSteeringDecision>();var data=new List<object>();
        // Native-free JIT warmup. Input is a copy and the active policy is forked inside Decide.
        foreach(var at in times){
            var i=Array.FindIndex(rows,r=>Math.Abs(r["t"]-at)<1e-8);var r=rows[i];var review=reviews.Last(q=>q.Time<=at+1e-8);
            var v=PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=(float)planRows[i]["target"]};
            var pending=commands.FirstOrDefault(d=>d.ObservedAt<=at+1e-8&&d.RequestedActivation>at+1e-8&&d.CancelledAt is null);
            var input=new PredictiveRiderInput(v,r["t"],r["omega_before"],(float)r["steer_before"],new((float)review.Drive,review.Time+.1),pending is null?null:pending with{ActualActivation=null});
            var decision=new PredictiveRiderControl().Decide(input,spec,at<10.15?initial:next,surface,p,147,52.72097f,false);
            records.Add(decision);data.Add(new{ObservedAt=at,Input=input,Decision=decision});
            Console.WriteLine($"C t={at:F2}: queued={decision.Queued} correction={decision.CorrectionRadians*180/Math.PI:F2}deg gain={(decision.Candidates.Length==0?0:decision.Candidates[0].Score-decision.Candidates.Min(c=>c.Score)):F5} m2 CPU={decision.Milliseconds:F2}ms");
        }
        var sorted=records.Select(d=>d.Milliseconds).Order().ToArray();var median=sorted[sorted.Length/2];
        var passed=median<=50&&sorted.Max()<=150&&records.All(d=>d.Candidates.Length<=3&&d.Candidates.All(c=>c.Prediction.ContactQueries<=5000));
        File.WriteAllText(Path.Combine(output,"decision-validation.json"),JsonSerializer.Serialize(new{Passed=passed,NativeWorlds=0,States=records.Count,
            Queued=records.Count(d=>d.Queued),MedianMilliseconds=median,MaximumMilliseconds=sorted.Max(),Records=records.Select(d=>new{d.ObservedAt,d.Activation,d.CorrectionRadians,d.Queued,d.Reason,d.Milliseconds,
                Candidates=d.Candidates.Select(c=>new{c.CorrectionRadians,c.Score,c.Safe,c.Prediction.ContactQueries,c.Prediction.MinimumInner,c.Prediction.MinimumOuter,
                    End=c.Prediction.Points[^1]})})},json)+"\n");
        using(var file=File.Create(Path.Combine(output,"decision-forecasts.json.gz")))using(var zip=new System.IO.Compression.GZipStream(file,System.IO.Compression.CompressionLevel.SmallestSize))
            JsonSerializer.Serialize(zip,data,json);
        Console.WriteLine($"C gate {(passed?"PASS":"FAIL")}; median {median:F2}ms maximum {sorted.Max():F2}ms. No new world yet.");
    }
}
