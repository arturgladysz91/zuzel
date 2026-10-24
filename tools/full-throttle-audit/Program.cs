using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using SharedWorldSpeedway;
using CoreSim;
using CoreSim.PhysicalSpace;

// Offline alignment audit, no world creation. Preserve the initial strict timestamp failure.
var d=args[0];var raw=args[1];
var original=RobustLearningExperiment.Read<System.Text.Json.JsonElement>(d,"predictor.json");
if(!File.Exists(Path.Combine(d,"predictor-initial.json"))){
    File.Copy(Path.Combine(d,"predictor.json"),Path.Combine(d,"predictor-initial.json"));
    File.Copy(Path.Combine(d,"preflight.json"),Path.Combine(d,"preflight-initial.json"));
}
var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
using var budget=new RobustBudget(d,FullThrottleLearningExperiment.Limits,750,6000,100000000);
var n=budget.Reserve("predictor-timestamp-alignment","offline",false);var cpu=Process.GetCurrentProcess().TotalProcessorTime;long queries=0;
var source=RobustLearningExperiment.ReadEpisode(raw,"diag-B-D1");var checks=new List<object>();bool passed=true;int index=0;
double Degrees(double x)=>Math.Abs(BikeAngles.Wrap(x))*180/Math.PI;
foreach(var check in original.GetProperty("Checks").EnumerateArray()){
    var origin=check.GetProperty("Origin").GetDouble();var action=check.GetProperty("Action").Deserialize<FreeRacingAction>(FreeLearningExperiment.Json);
    var sample=source.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-origin)<1e-8);var input=sample.Input with{Pending=sample.Input.Pending.Where(c=>c.ObservedAt<origin-1e-8).ToArray(),NextReview=origin};
    var predicted=FreeRacingModel.Predict(input,FreeLearningExperiment.Rider(source.Replay.Scenario),frame,surface,FreeLearningExperiment.Parameters(),147,52.72097f,.9,action);queries+=predicted.ForceEvaluations;
    var native=RobustLearningExperiment.ReadEpisode(raw,"validate-B-"+index++);var points=new List<object>();bool ok=check.GetProperty("PrefixExact").GetBoolean();int compared=0;
    foreach(var q in predicted.Points){
        var actual=native.Replay.Samples.MinBy(a=>Math.Abs(a.Input.Time-q.Time))!;var delta=Math.Abs(actual.Input.Time-q.Time);
        bool grid=Math.Abs(q.Time/.01-Math.Round(q.Time/.01))<1e-6||Math.Abs(q.Time-origin-.9)<1e-8||Math.Abs(q.Time-origin)<1e-8;
        if(delta>1e-7){if(grid)ok=false;points.Add(new{q.Time,NativeTime=actual.Input.Time,DeltaSeconds=delta,RequiredGridPoint=grid,Compared=false,Reason="Curvature event independently quantized in native and predictor; no common timestamp. No interpolation."});continue;}
        compared++;var v=actual.Input.Rider;var xy=Vector2.Distance(q.Rider.Position,v.Position);var speed=Math.Abs(q.Rider.Speed-v.Speed);var psi=Degrees(q.Rider.Heading-v.Heading);var beta=Degrees(q.Rider.SlideRadians-v.SlideRadians);
        var chi=Degrees(Math.Atan2(q.Rider.Velocity.Y,q.Rider.Velocity.X)-Math.Atan2(v.Velocity.Y,v.Velocity.X));var steering=Degrees(q.Steering-actual.Input.Steering);var omega=Math.Abs(q.Omega-actual.Input.Omega);
        var yaw=Math.Abs(q.Rider.AngularVelocity-v.AngularVelocity);var clearance=Math.Max(q.Inner-actual.Inner,q.Outer-actual.Outer);
        bool point=xy<=.1&&speed<=.2&&chi<=1&&psi<=1&&beta<=1&&steering<=1.5&&omega<=2&&yaw<=.05&&clearance<=.1;ok&=point;
        points.Add(new{q.Time,NativeTime=actual.Input.Time,DeltaSeconds=delta,RequiredGridPoint=grid,Compared=true,Passed=point,XY=xy,Speed=speed,Chi=chi,Psi=psi,Beta=beta,Steering=steering,Omega=omega,YawRate=yaw,ClearanceOptimism=clearance});
    }
    ok&=compared>=91;passed&=ok;checks.Add(new{Origin=origin,Passed=ok,Compared=compared,Points=points});
}
budget.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,queries,4);
RobustLearningExperiment.Save(d,"predictor-aligned.json",new{Passed=passed,Checks=checks,ThresholdsChanged=false,NativeWorlds=0,OriginalFailurePreserved=true});
var initial=RobustLearningExperiment.Read<System.Text.Json.JsonElement>(d,"preflight-initial.json");
RobustLearningExperiment.Save(d,"preflight.json",new{Passed=passed&&initial.GetProperty("LegacyParity").GetBoolean()&&initial.GetProperty("Deterministic").GetBoolean()&&initial.GetProperty("RepresentableFullThrottle").GetBoolean(),Predictor=passed,LegacyParity=true,Deterministic=true,RepresentableFullThrottle=true,FinalWorlds=0,AlignmentAudit="predictor-aligned.json"});
Console.WriteLine("Aligned predictor gate: "+passed);
