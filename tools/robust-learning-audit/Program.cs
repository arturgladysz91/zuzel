using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

var directory=args[0];var raw=args[1];var mode=args[2];RobustLearningExperiment.VerifySources(directory);
using var budget=new RobustBudget(directory);var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var p=FreeLearningExperiment.Parameters();
if(mode=="cost"){
    var reservation=budget.Reserve("horizon-cost-offline","offline",false);var started=Process.GetCurrentProcess().TotalProcessorTime;long evaluations=0,forecasts=0;
    var run=FreeLearningExperiment.ReadEpisode("docs/experiments/free-racing-learning","final-holdout-14-5");var spec=FreeLearningExperiment.Rider(run.Scenario);
    var inputs=run.Decisions.Select(d=>run.Samples.Single(s=>Math.Abs(s.Input.Time-d.Time)<1e-8).Input).Select(x=>x with{Pending=x.Pending.Where(c=>c.ObservedAt<x.Time-1e-8).ToArray(),NextReview=x.Time}).ToArray();
    FreeLearningDecision Choose(FreeRacingInput input,FreeLearningPolicy policy,double horizon){var d=FreeLearningController.Choose(input,policy,spec,frame,surface,p,147,52.72097f,horizon);evaluations+=d.ForceEvaluations;forecasts+=d.Forecasts;return d;}
    Choose(inputs[0],run.Policy,.6);Choose(inputs[0],run.Policy,.9); // both JIT paths warm, work counted
    var timings=new List<object>();
    foreach(var horizon in new[]{.6,.9}){var cpu=Process.GetCurrentProcess().TotalProcessorTime;var before=evaluations;var f0=forecasts;
        foreach(var input in inputs)Choose(input,run.Policy,horizon);
        timings.Add(new{Horizon=horizon,Observations=inputs.Length,CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,Evaluations=evaluations-before,Forecasts=forecasts-f0});}
    var early=inputs.Single(i=>Math.Abs(i.Time-1)<1e-8);var prior=RobustLearningExperiment.Reference("129-200");var ablations=new List<object>();
    foreach(var index in new[]{1,4,5,6}){var values=run.Policy.Values;values[index]=prior.Values[index];var d=Choose(early,FreeLearningPolicy.From(values),.6);
        ablations.Add(new{Parameter=FreeLearningPolicy.Names[index],Value=values[index],Action=d.Action,Scope="local one-weight intervention at1.0s on original129 state; no native trajectory"});}
    RobustLearningExperiment.Save(directory,"horizon-cost.json",new{NativeWorlds=0,Timings=timings,EarlyAblations=ablations,Evaluations=evaluations,Forecasts=forecasts,CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds});
    budget.Complete(reservation,(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds,evaluations,forecasts);return;
}
if(mode is not ("post" or "post-aligned"))throw new ArgumentException("cost|post|post-aligned");
var alignedRevision=mode=="post-aligned";
var selected=RobustLearningExperiment.Read<JsonElement>(directory,"selected-policy.json").GetProperty("Selected").Deserialize<RobustEvaluation>(RobustLearningExperiment.Json)!;
if(!alignedRevision){
    var repeat=RobustLearningExperiment.Execute(directory,raw,budget,"diagnostic","repeat-final-130",selected.Id,selected.Policy,RobustLearningExperiment.SealedTest[0]);
    var original=RobustLearningExperiment.ReadEpisode(raw,"test-130-final-T00");
    RobustLearningExperiment.Save(directory,"determinism.json",new{Passed=original.Replay.Metrics.Fingerprint==repeat.Replay.Metrics.Fingerprint,Original=original.Replay.Metrics.Fingerprint,Repeat=repeat.Replay.Metrics.Fingerprint,ExtraNativeWorlds=1});
}
if(alignedRevision){
    File.Copy(Path.Combine(directory,"post-prediction.json"),Path.Combine(directory,"post-prediction-initial.json"),false);
    RobustLearningExperiment.Save(directory,"post-audit-alignment.json",new{NativeWorlds=0,ChangedThresholds=false,ChangedPolicy=false,
        Reason="First turn samples occurred at curvature-event microtimes; nine horizon endpoints did not exist in the saved native grid. Initial records and all work retained. Corrected sampling chooses only origins with actual endpoints for every requested horizon.",
        OriginalFile="post-prediction-initial.json",OriginRule="first/middle/last available native turn origin with actual endpoints at0.3,0.6,0.9s; no invented interpolation or tolerance widening"});
}
var n=budget.Reserve(alignedRevision?"post-prediction-aligned-offline":"post-prediction-offline","offline",false);var start=Process.GetCurrentProcess().TotalProcessorTime;long queries=0,predictions=0;var records=new List<object>();var passedAll=true;
foreach(var name in new[]{"test-130-final-T00","test-130-final-T08","test-130-final-T11"}){
    var run=RobustLearningExperiment.ReadEpisode(raw,name).Replay;var spec=FreeLearningExperiment.Rider(run.Scenario);
    var available=run.Samples.Where(s=>s.Input.Rider.Road.IsTurn&&s.Input.Time+.9<run.Metrics.DurationSeconds-1e-7
        &&new[]{.3,.6,.9}.All(h=>run.Samples.Any(end=>Math.Abs(end.Input.Time-s.Input.Time-h)<1e-7))).ToArray();
    if(available.Length==0){passedAll=false;records.Add(new{Name=name,Passed=false,Reason="No available native turn continuation"});continue;}
    foreach(var origin in new[]{available[0],available[available.Length/2],available[^1]}.DistinctBy(s=>s.Input.Time))foreach(var horizon in new[]{.3,.6,.9}){
        var actual=run.Samples.FirstOrDefault(s=>Math.Abs(s.Input.Time-origin.Input.Time-horizon)<1e-7);
        if(actual is null){passedAll=false;records.Add(new{Name=name,Origin=origin.Input.Time,Horizon=horizon,Passed=false,Reason="No native endpoint"});continue;}
        predictions++;var prediction=FreeRacingModel.Predict(origin.Input,spec,frame,surface,p,147,52.72097f,horizon,
            causalPolicy:input=>{var d=FreeLearningController.Choose(input,run.Policy,spec,frame,surface,p,147,52.72097f);predictions+=d.Forecasts;return(d.Action,d.ForceEvaluations);},reviewSeconds:.2);
        queries+=prediction.ForceEvaluations;var end=prediction.Points[^1];var v=actual.Input.Rider;
        var xy=Vector2.Distance(end.Rider.Position,v.Position);var speed=Math.Abs(end.Rider.Speed-v.Speed);
        var chi=Math.Abs(BikeAngles.Wrap(Math.Atan2(end.Rider.Velocity.Y,end.Rider.Velocity.X)-Math.Atan2(v.Velocity.Y,v.Velocity.X)))*180/Math.PI;
        var beta=Math.Abs(BikeAngles.Wrap(end.Rider.SlideRadians-v.SlideRadians))*180/Math.PI;var steering=Math.Abs(end.Steering-actual.Input.Steering)*180/Math.PI;
        var omega=Math.Abs(end.Omega-actual.Input.Omega);var yaw=Math.Abs(end.Rider.AngularVelocity-v.AngularVelocity);var optimism=Math.Max(end.Inner-actual.Inner,end.Outer-actual.Outer);
        var passed=xy<=.1&&speed<=.2&&chi<=1&&beta<=1&&steering<=1.5&&omega<=2&&yaw<=.05&&optimism<=.1;passedAll&=passed;
        records.Add(new{Name=name,Origin=origin.Input.Time,Horizon=horizon,XY=xy,Speed=speed,Chi=chi,Beta=beta,Steering=steering,Omega=omega,Yaw=yaw,ClearanceOptimism=optimism,Passed=passed});
    }
}
RobustLearningExperiment.Save(directory,"post-prediction.json",new{NativeWorlds=0,Passed=passedAll,Records=records,Evaluations=queries,Forecasts=predictions,CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-start).TotalSeconds});
budget.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-start).TotalSeconds,queries,predictions);
Console.WriteLine($"New-policy offline predictions pass={passedAll}; {queries} contact evaluations; {predictions} forecasts.");
