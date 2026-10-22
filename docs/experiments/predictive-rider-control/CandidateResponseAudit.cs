using System.IO.Compression;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

// Offline diagnostics of the already frozen forecasts. No new rollout, controller decision or world.
public static class CandidateResponseAudit
{
    private sealed record SavedForecast(double ObservedAt,PredictiveRiderInput Input,PredictiveSteeringDecision Decision);
    public static void Run(string directory)
    {
        using var file=File.OpenRead(Path.Combine(directory,"decision-forecasts.json.gz"));
        using var zip=new GZipStream(file,CompressionMode.Decompress);
        var json=new JsonSerializerOptions{IncludeFields=true,WriteIndented=true};
        var forecasts=JsonSerializer.Deserialize<SavedForecast[]>(zip,json)!;
        var initial=PreparedCornerAudit.InitialPlan();var frame=initial.Path.Frame;
        var saved=ContinuousCornerAudit.SavedEnd();var p=ContinuousCornerAudit.Parameters();var spec=CornerEntryAudit.Rider();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
        var next=new PreparedCornerPlan(saved.Rider,spec,frame,p,saved.Time,147,saved.Wheel,saved.Steering,null,surface,new(saved.Old.Path,saved.Old.ControlMemory,saved.Pending));
        var records=new List<object>();
        foreach(var record in forecasts)foreach(var candidate in record.Decision.Candidates){
            var plan=record.ObservedAt<10.15?initial:next;var points=candidate.Prediction.Points;
            var point=points.First(q=>q.Time>=record.Decision.Activation-1e-8);
            var after=points.First(q=>q.Time>point.Time+1e-8);var dt=(float)(after.Time-point.Time);
            float? pulse=candidate.CorrectionRadians==0?null:point.Steering+candidate.CorrectionRadians;
            var force=TwoContactBikeDynamics.Forces(point.Rider,spec,frame,surface,p,point.Time,147,
                new(point.Omega*.33),new(point.Steering),dt,plan.Guidance(point.Rider,p,point.Time),pulse,inertia:52.72097f);
            var first=points[0].Rider;var last=points[^1].Rider;
            double Chi(RiderView v)=>Math.Atan2(v.Velocity.Y,v.Velocity.X);
            double Error(RiderView v){var q=plan.Path.At(v.TotalReferenceMeters);return MeterPoint.Dot(new(v.Position.X-q.Position.X,v.Position.Y-q.Position.Y),q.Outward);}
            records.Add(new{record.ObservedAt,record.Decision.Activation,candidate.CorrectionRadians,candidate.Safe,candidate.Score,
                ResponseTime=point.Time,ResponseDt=dt,force.SteeringBefore,force.SteeringRequest,force.SteeringUsed,force.SteeringAfter,
                force.FrontForce,force.RearForce,force.NetTorque,Motion=TravelDirectionState.Observe(point.Rider,force.NetForce,147),
                FrontUtilization=Math.Abs(force.FrontLateral)/force.TyreLoads!.FrontCapacityNewtons,
                RearUtilization=Math.Sqrt((double)force.RearLateral*force.RearLateral+(double)force.RearLongitudinal*force.RearLongitudinal)/force.TyreLoads.RearCapacityNewtons,
                RateRadiansPerSecond=Math.Abs(force.SteeringAfter-force.SteeringBefore)/dt,
                DeltaChiRadians=BikeAngles.Wrap(Chi(last)-Chi(first)),DeltaYawRadians=BikeAngles.Wrap(last.Heading-first.Heading),
                DeltaBetaRadians=BikeAngles.Wrap(last.SlideRadians-first.SlideRadians),InitialNormalError=Error(first),FinalNormalError=Error(last),
                candidate.Prediction.MinimumInner,candidate.Prediction.MinimumOuter});
        }
        File.WriteAllText(Path.Combine(directory,"candidate-response-audit.json"),JsonSerializer.Serialize(new{
            Meaning="Predicted C responses from saved forecasts; not native alternative-action continuations. Forces queried once at each predicted activation. No new model rollout or native world.",
            NativeWorlds=0,NewRollouts=0,ContactEvaluations=records.Count,Records=records},json)+"\n");
        Console.WriteLine($"Offline C responses: {records.Count}; no new rollouts or worlds.");
    }
}
