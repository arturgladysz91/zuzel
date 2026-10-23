using System.Diagnostics;
using System.Numerics;
using SharedWorldSpeedway;
using CoreSim;
using CoreSim.PhysicalSpace;

var directory=args[0];var raw=args[1];var phase=args[2];
using var budget=new RobustBudget(directory,ExtendedLearningExperiment.Limits,1000,6000,90000000);
var reservation=budget.Reserve("extra-audit-"+phase,"offline",false);var started=Process.GetCurrentProcess().TotalProcessorTime;long queries=0,forecasts=0;
var root=RobustLearningExperiment.ReadEpisode(raw,"audit130-two");var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
var p=FreeLearningExperiment.Parameters();var spec=FreeLearningExperiment.Rider(root.Replay.Scenario);var records=new List<object>();
if(phase=="h06-parity"){
    var baseline=RobustLearningExperiment.ReadEpisode(raw,"final-130-H06-U00");
    var repeated=RobustLearningExperiment.Execute(directory,raw,budget,"evaluation","repeat130-U00",1020,baseline.Replay.Policy,baseline.Replay.Scenario);
    var passed=baseline.Metrics.Native.Fingerprint==repeated.Metrics.Native.Fingerprint;
    RobustLearningExperiment.Save(directory,"h06-reference-parity.json",new{Passed=passed,Normalized=baseline.Metrics.Native.Fingerprint,LegacyUnnormalized=repeated.Metrics.Native.Fingerprint,
        Scope="One additional native replay after final policy freeze; same130 checkpoint and U00, original0.6s default scoring. Never used for selection."});
    budget.Complete(reservation,Math.Max(0,(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds-repeated.Metrics.Native.CpuSeconds),0,0);
    if(!passed)throw new InvalidOperationException("H06 reference parity failed; retain evidence, do not retune on final.");return;
}
if(phase is "high-slip" or "full-gas"){
    var fullGas=phase=="full-gas";var origin=fullGas?8.4:9.6;var action=fullGas?new FreeRacingAction(0,1):new FreeRacingAction(35*MathF.PI/180,0);
    var saved=root.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-origin)<1e-8);
    var input=saved.Input with{Pending=saved.Input.Pending.Where(c=>c.ObservedAt<origin-1e-8).ToArray(),NextReview=origin};
    var prediction=FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,.9,action);queries+=prediction.ForceEvaluations;forecasts++;
    var native=RobustLearningExperiment.Execute(directory,raw,budget,"evaluation",fullGas?"validate-full-gas":"validate-high-beta",1020,root.Replay.Policy,root.Replay.Scenario,
        shortSeconds:origin+.9,twoBends:true,holdAt:origin,holdAction:action);
    var comparisons=new List<object>();var passed=true;double maxBeta=0;
    double Degrees(double x)=>Math.Abs(BikeAngles.Wrap(x))*180/Math.PI;
    foreach(var actual in native.Replay.Samples.Where(s=>s.Input.Time>=origin-1e-8)){
        var predicted=prediction.Points.FirstOrDefault(x=>Math.Abs(x.Time-actual.Input.Time)<1e-7);
        if(predicted is null)continue;var v=actual.Input.Rider;maxBeta=Math.Max(maxBeta,Degrees(v.SlideRadians));
        var xy=Vector2.Distance(predicted.Rider.Position,v.Position);var speed=Math.Abs(predicted.Rider.Speed-v.Speed);
        var chi=Degrees(Math.Atan2(predicted.Rider.Velocity.Y,predicted.Rider.Velocity.X)-Math.Atan2(v.Velocity.Y,v.Velocity.X));
        var psi=Degrees(predicted.Rider.Heading-v.Heading);var beta=Degrees(predicted.Rider.SlideRadians-v.SlideRadians);var yaw=Math.Abs(predicted.Rider.AngularVelocity-v.AngularVelocity);
        var steering=Degrees(predicted.Steering-actual.Input.Steering);var omega=Math.Abs(predicted.Omega-actual.Input.Omega);var optimism=Math.Max(predicted.Inner-actual.Inner,predicted.Outer-actual.Outer);
        var ok=xy<=.1&&speed<=.2&&chi<=1&&psi<=1&&beta<=1&&yaw<=.05&&steering<=1.5&&omega<=2&&optimism<=.1;passed&=ok;
        comparisons.Add(new{Time=actual.Input.Time,Passed=ok,XY=xy,Speed=speed,Chi=chi,Psi=psi,Beta=beta,Yaw=yaw,Steering=steering,Omega=omega,ClearanceOptimism=optimism});
    }
    var hasEndpoint=Math.Abs(native.Replay.Samples[^1].Input.Time-origin-.9)<1e-7;passed&=hasEndpoint&&maxBeta<=45;
    var prefix=root.Replay.Samples.Where(s=>s.Input.Time<=origin+1e-8).All(s=>native.Replay.Samples.Any(t=>Math.Abs(t.Input.Time-s.Input.Time)<1e-8&&t.Input.Rider==s.Input.Rider&&t.Input.Omega==s.Input.Omega&&t.Input.Steering==s.Input.Steering));passed&=prefix;
    RobustLearningExperiment.Save(directory,fullGas?"full-gas-prediction.json":"high-slip-prediction.json",new{Passed=passed,HasEndpoint=hasEndpoint,PrefixExact=prefix,MaximumNativeBeta=maxBeta,Native=native.Metrics,Comparisons=comparisons,ThresholdsChanged=false,
        Scope=fullGas?"Native full-gas feasibility probe from an actually reached state, with full reaction and original physics. Manual diagnostic command, never a learned-policy or full-two-bend success claim."
            :"Independent physical validation of one held action selected from frozen diagnostic grid; no training trajectory or learned-policy success claim. Native samples include large beta while remaining below45 degrees."});
    budget.Complete(reservation,Math.Max(0,(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds-native.Metrics.Native.CpuSeconds),queries,forecasts);
    if(!passed)throw new InvalidOperationException("Additional high-slip predictor gate failed; stop H09.");
    Console.WriteLine($"Additional predictor validation: {comparisons.Count} native points, max beta={maxBeta:F3}, passed={passed}.");return;
}
// This is an action-availability diagnostic, never a policy or a training intervention.
foreach(var time in new[]{8.4,9.2,9.6,9.8,10.0}){
    var saved=root.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-time)<1e-7);var input=saved.Input with{
        Pending=saved.Input.Pending.Where(c=>c.ObservedAt<time-1e-8).ToArray(),NextReview=time};
    foreach(var steering in new[]{-35f,-25f,-15f,-5f,0f,5f,15f,25f,35f})foreach(var drive in new[]{0f,(float)root.Replay.Policy.Drive,.8f,1f}){
        var action=new FreeRacingAction(steering*MathF.PI/180,drive);action.Validate();
        var prediction=FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,.9,action);queries+=prediction.ForceEvaluations;forecasts++;
        var end=prediction.Points[^1];var clearance=prediction.Points.Min(q=>Math.Min(q.Inner,q.Outer));
        var closing=Vector2.Dot(end.Rider.Velocity,end.Rider.Road.Outward);var future=Math.Min(end.Inner-Math.Max(0,-closing)*.24,end.Outer-Math.Max(0,closing)*.24);
        var beta=prediction.Points.Max(q=>Math.Abs(q.Rider.SlideRadians))*180/Math.PI;
        var safe=clearance>=.2&&future>=.1&&beta<=45;
        records.Add(new{Time=time,Action=action,SafeInHorizon=safe,Clearance=clearance,FutureClearance=future,MaximumBeta=beta,EndSpeed=end.Rider.Speed,
            Scope="Prediction only. Beyond-envelope points cannot establish validated recovery; no native or learned trajectory claim."});
    }
}
RobustLearningExperiment.Save(directory,"action-availability.json",new{Records=records,Queries=queries,Forecasts=forecasts,NativeWorlds=0});
budget.Complete(reservation,(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds,queries,forecasts);
Console.WriteLine($"Action availability: {forecasts} forecasts, {queries} contact evaluations; no native worlds.");
