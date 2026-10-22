using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

public static class PredictionAudit
{
    public static void Run(string directory)
    {
        var output=Path.GetFullPath(directory);var resultPath=Path.Combine(output,"prediction-validation.json");
        if(File.Exists(resultPath)||File.Exists(Path.Combine(output,"prediction-attempt.json")))
            throw new InvalidOperationException("Frozen prediction experiment already attempted; no silent retuning/retry.");
        var options=new JsonSerializerOptions{WriteIndented=true,IncludeFields=true};
        var config=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"protocol.json"))).RootElement;
        var times=config.GetProperty("ObservationTimes").Deserialize<double[]>()!;
        var horizons=config.GetProperty("HorizonsSeconds").Deserialize<double[]>()!;
        var repo=Path.GetFullPath(Path.Combine(output,"../../.."));
        string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var inputs=new[]{"continuous.csv.gz","continuous-plan.csv.gz","continuous.json","changes.json","reviews.json"}
            .ToDictionary(n=>n,n=>Hash(Path.Combine(output,"../continuous-corner-planning",n)));
        var sourceFiles=new[]{"TwoContactBikeDynamics.cs","RearWheelDynamics.cs","TravelDirectionSteering.cs","PreparedCornerPlan.cs",
            "ContinuousCornerPlanning.cs","WorldRunner.cs","Engine500.cs","LoadBasedTyreGrip.cs","PredictiveRiderModel.cs"};
        var sourceHashes=sourceFiles.ToDictionary(n=>n,n=>Hash(Path.Combine(repo,"tools/shared-world-speedway",n)));
        File.WriteAllText(Path.Combine(output,"prediction-attempt.json"),JsonSerializer.Serialize(new{ProtocolSha256=Hash(Path.Combine(output,"protocol.json")),
            InputHashes=inputs,SourceHashes=sourceHashes,NativeWorlds=0,RetuningAllowed=false},options)+"\n");
        var rows=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous");
        var plans=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous-plan");
        var capture=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"../continuous-corner-planning/continuous.json"))).RootElement;
        var commands=capture.GetProperty("Result").GetProperty("Decisions").Deserialize<DecisionEvent[]>(InverseFeasibilityAudit.Json)!;
        var reviews=JsonSerializer.Deserialize<PreparedCornerReview[]>(File.ReadAllText(Path.Combine(output,"../continuous-corner-planning/reviews.json")))!;
        var basis=PreparedCornerAudit.InitialPlan();var frame=basis.Path.Frame;var saved=ContinuousCornerAudit.SavedEnd();
        var p=ContinuousCornerAudit.Parameters();var spec=CornerEntryAudit.Rider();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        PreparedCornerPlan Plan(double time)=>time<10.15?PreparedCornerAudit.InitialPlan():
            new(saved.Rider,spec,frame,p,saved.Time,147,saved.Wheel,saved.Steering,null,surface,new(saved.Old.Path,saved.Old.ControlMemory,saved.Pending));
        var native=rows.Select((r,i)=>PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=(float)plans[i]["target"]}).ToArray();
        var indexByTime=rows.Select((r,i)=>(Key:Math.Round(r["t"],6),Index:i)).ToDictionary(x=>x.Key,x=>x.Index);
        PredictiveRiderInput Input(int i){var r=rows[i];var review=reviews.Last(q=>q.Time<=r["t"]+1e-8);
            var pending=commands.FirstOrDefault(d=>d.ObservedAt<=r["t"]+1e-8&&d.RequestedActivation>r["t"]+1e-8&&d.CancelledAt is null);
            return new(native[i],r["t"],r["omega_before"],(float)r["steer_before"],new((float)review.Drive,review.Time+.1),pending is null?null:pending with{ActualActivation=null});}
        var t=config.GetProperty("PredictionTolerance");
        double Limit(string k)=>t.GetProperty(k).GetDouble();
        var records=new List<object>();var trajectories=new List<object>();var costs=new List<double>();
        var allPassed=true;double maximumPosition=0,maximumSpeed=0,maximumChi=0,maximumBeta=0,maximumSteer=0,maximumOmega=0;
        int contactQueries=0;double setupMilliseconds=0;
        // One discarded non-native warmup, explicitly excluded from measured cost and saved separately.
        var warmInput=Input(indexByTime[1.4]);var warm=PredictiveRiderModel.Predict(warmInput,spec,Plan(1.4),surface,p,147,52.72097f,.3,new(true));
        foreach(var at in times)foreach(var horizon in horizons){
            var i=indexByTime[Math.Round(at,6)];var input=Input(i);var watch=Stopwatch.StartNew();var plan=Plan(at);watch.Stop();setupMilliseconds+=watch.Elapsed.TotalMilliseconds;
            watch.Restart();var prediction=PredictiveRiderModel.Predict(input,spec,plan,surface,p,147,52.72097f,horizon,new(true));watch.Stop();
            var elapsed=watch.Elapsed.TotalMilliseconds;costs.Add(elapsed);contactQueries+=prediction.ContactQueries;
            double ep=0,ev=0,ec=0,eb=0,es=0,ew=0;int compared=0;
            foreach(var point in prediction.Points){
                if(!indexByTime.TryGetValue(Math.Round(point.Time,6),out var j)||Math.Abs(rows[j]["t"]-point.Time)>1e-7)continue;
                var actual=native[j];var chi=Math.Atan2(point.Rider.Velocity.Y,point.Rider.Velocity.X);var actualChi=Math.Atan2(actual.Velocity.Y,actual.Velocity.X);
                ep=Math.Max(ep,Vector2.Distance(point.Rider.Position,actual.Position));ev=Math.Max(ev,Math.Abs(point.Rider.Speed-actual.Speed));
                ec=Math.Max(ec,Math.Abs(BikeAngles.Wrap(chi-actualChi))*180/Math.PI);eb=Math.Max(eb,Math.Abs(BikeAngles.Wrap(point.Rider.SlideRadians-actual.SlideRadians))*180/Math.PI);
                es=Math.Max(es,Math.Abs(point.Steering-rows[j]["steer_before"])*180/Math.PI);ew=Math.Max(ew,Math.Abs(point.Omega-rows[j]["omega_before"]));compared++;
                trajectories.Add(new{ObservedAt=at,Horizon=horizon,point.Time,Prediction=point,Actual=actual,ActualSteering=rows[j]["steer_before"],ActualOmega=rows[j]["omega_before"]});
            }
            var actualInterval=rows.Where(r=>r["t"]>=at-1e-8&&r["t"]+r["dt"]<=at+horizon+1e-7).ToArray();
            var boundaryOptimism=Math.Max(prediction.MinimumInner-actualInterval.Min(r=>r["inner"]),prediction.MinimumOuter-actualInterval.Min(r=>r["outer"]));
            var passed=ep<=Limit("PositionMeters")&&ev<=Limit("SpeedMetersPerSecond")&&ec<=Limit("ChiDegrees")&&eb<=Limit("BetaDegrees")
                &&es<=Limit("SteeringDegrees")&&ew<=Limit("WheelOmegaRadPerSecond")&&boundaryOptimism<=Limit("BoundaryOptimismMeters")
                &&prediction.ContactQueries<=config.GetProperty("MaximumContactQueriesPerCandidate").GetInt32();
            allPassed&=passed;maximumPosition=Math.Max(maximumPosition,ep);maximumSpeed=Math.Max(maximumSpeed,ev);maximumChi=Math.Max(maximumChi,ec);
            maximumBeta=Math.Max(maximumBeta,eb);maximumSteer=Math.Max(maximumSteer,es);maximumOmega=Math.Max(maximumOmega,ew);
            records.Add(new{ObservedAt=at,Horizon=horizon,Passed=passed,ComparedNativeStates=compared,PositionError=ep,SpeedError=ev,ChiDegrees=ec,BetaDegrees=eb,
                SteeringDegrees=es,WheelOmegaError=ew,BoundaryOptimism=boundaryOptimism,prediction.ContactQueries,prediction.ForceEvaluations,Milliseconds=elapsed,
                KnownPending=input.Pending,Activations=prediction.Activations,prediction.MaximumFrontUtilization,prediction.MaximumRearUtilization,prediction.MaximumSteeringRate});
            Console.WriteLine($"t={at:F2} H={horizon:F1}: {(passed?"PASS":"FAIL")} xy={ep:F4} v={ev:F4} chi={ec:F3} beta={eb:F3} delta={es:F3} omega={ew:F3}");
        }
        costs.Sort();var median=costs[costs.Count/2];
        var costPassed=3*median<=config.GetProperty("MaximumMedianMillisecondsPerThreeCandidates").GetDouble()
            &&3*costs.Max()<=config.GetProperty("MaximumMillisecondsPerThreeCandidates").GetDouble();
        File.WriteAllText(resultPath,JsonSerializer.Serialize(new{Passed=allPassed&&costPassed,AccuracyPassed=allPassed,CostPassed=costPassed,
            NativeWorlds=0,NativeSteps=0,Forecasts=records.Count,ContactQueries=contactQueries,WarmupContactQueries=warm.ContactQueries,
            MaximumPositionError=maximumPosition,MaximumSpeedError=maximumSpeed,MaximumChiDegrees=maximumChi,MaximumBetaDegrees=maximumBeta,
            MaximumSteeringDegrees=maximumSteer,MaximumWheelOmegaError=maximumOmega,MedianMilliseconds=median,MaximumMilliseconds=costs.Max(),
            ThreeCandidateCostIsConservativeEstimate=true,SetupMilliseconds=setupMilliseconds,Records=records},options)+"\n");
        File.WriteAllText(Path.Combine(output,"prediction-trajectories.json"),JsonSerializer.Serialize(trajectories,options)+"\n");
        Console.WriteLine($"Prediction gate: {(allPassed&&costPassed?"PASS":"FAIL")}; no candidate controller or native ride is authorized by a failed gate.");
    }
}
