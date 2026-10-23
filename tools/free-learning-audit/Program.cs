using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

var dir=args[0];var mode=args.Length>1?args[1]:"offline";
if(mode=="baseline"){
    // Fixed FIRST held-out state from the pre-training protocol, chosen before results.
    using var budget=new FreeLearningBudget(dir);var n=budget.Reserve("matched-124","validation",1000);var cpu=Process.GetCurrentProcess().TotalProcessorTime;
    var scenario=FreeLearningExperiment.Validation[0];var spec=FreeLearningExperiment.Rider(scenario);var frame=RacingLineRootCauseAudit.Frame();
    var boundary=new RacingLineGeometry(frame);var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
    var states=new List<WorldSnapshot>();var minimum=double.PositiveInfinity;var legalProgress=0d;double? gateTime=null;var failure=false;
    long forceQueries=0,planQueries=0;int localQueries=0;
    var endReference=frame.SegmentStartMeters(4)+3;var gate=frame.At(endReference,0);
    using var world=new WorldRunner(new[]{spec},parameters:ContinuousCornerAudit.Parameters(),trackShape:RacingLineRootCauseAudit.Variant(),frozenSurface:surface,
        twoContactInitial:new(scenario.HeadingError,scenario.YawRate),diagnostics:new(){TwoContact=o=>{
            var q=o.Forces.TravelSteering?.ContactQueries??0;if(localQueries!=0&&q!=localQueries)throw new InvalidOperationException("Historical query count varied.");localQueries=q;forceQueries+=1+q;
        },CornerPlan=c=>planQueries+=c.DiagnosticContactQueries});
    states.Add(world.Snapshot);var result=world.Run("matched-124",.01,26,acceptedObserver:s=>{
        var previous=states[^1].Riders[0];states.Add(s);var v=s.Riders[0];var gap=boundary.Clearance(v);var oldGap=boundary.Clearance(previous);
        var sweep=.5*(Vector2.Distance(v.Position,previous.Position)+CoreSim.PhysicalSpace.SpeedwayBikeDimensions.Reference.BoundingRadiusMeters*Math.Abs(CoreSim.PhysicalSpace.BikeAngles.Wrap(v.Heading-previous.Heading)));
        minimum=Math.Min(minimum,Math.Min(Math.Min(gap.Inner,gap.Outer),Math.Min(oldGap.Inner,oldGap.Outer))-sweep);
        if(minimum<0||Math.Abs(v.SlideRadians)>Math.PI/4||Vector2.Dot(v.Velocity,v.Road.Tangent)<=0)failure=true;
        if(!failure)legalProgress=Math.Max(legalProgress,v.TotalReferenceMeters-scenario.Start);
        var bike=CoreSim.PhysicalSpace.BikeFootprint.Create(new(v.Position.X,v.Position.Y),v.Heading,CoreSim.PhysicalSpace.SpeedwayBikeDimensions.Reference);
        var point=new CoreSim.PhysicalSpace.MeterPoint(gate.Position.X,gate.Position.Y);var tangent=new CoreSim.PhysicalSpace.MeterPoint(gate.Road.Tangent.X,gate.Road.Tangent.Y);
        var beyond=new[]{bike.Chassis,bike.Handlebar}.Min(c=>Math.Min(CoreSim.PhysicalSpace.MeterPoint.Dot(c.Start-point,tangent),CoreSim.PhysicalSpace.MeterPoint.Dot(c.End-point,tangent))-c.RadiusMeters);
        if(!failure&&gateTime is null&&v.TotalReferenceMeters>=endReference&&beyond>=0)gateTime=s.TimeSeconds;
    },diagnosticStop:s=>failure||gateTime is{} t&&s.TimeSeconds>=t+1-1e-10);
    var work=result.Work;var evaluations=forceQueries+work.RearWheelBoundaryEvaluations*(1+localQueries)+work.PreparedContactQueries+planQueries;
    var completed=!failure&&gateTime is not null&&result.TimeSeconds>=gateTime+1-1e-8;
    FreeLearningExperiment.Save(dir,"matched-124.json",new{Scenario=scenario,Result=result,States=states,Completed=completed,GateSeconds=completed?gateTime:null,
        LegalProgress=legalProgress,MinimumClearanceMeters=minimum,ContactEvaluations=evaluations,ActualForceQueries=forceQueries,HistoricalLocalQueriesPerForce=localQueries,
        work.RearWheelBoundaryEvaluations,work.PreparedContactQueries,PlanDiagnosticQueries=planQueries});
    budget.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,evaluations,0);return;
}
var process=Process.GetCurrentProcess();var start=process.TotalProcessorTime;long queries=0,forecasts=0,originalPreflightForecasts=0,preflightQueries=0;var records=new List<object>();var allPassed=true;int decisionsMatched=0;
var frame2=RacingLineRootCauseAudit.Frame();var surface2=TrackState.CreateDefault(frame2.Track,new(1,0,.5f)).Snapshot();
// Recount the original 18 offline causal forecasts, including their nested action forecasts.
// This audit's duplicate work is itself counted; it creates no native world.
foreach(var scenario in FreeLearningExperiment.Scenarios(1)){
    var probe=FreeLearningExperiment.ReadEpisode(dir,"probe-"+scenario.Name);var spec=FreeLearningExperiment.Rider(scenario);
    foreach(var origin in new[]{0d,.4,.8})foreach(var horizon in new[]{.3,.6,.9}){
        var input=probe.Samples.Single(s=>Math.Abs(s.Input.Time-origin)<1e-8).Input;originalPreflightForecasts++;
        var prediction=FreeRacingModel.Predict(input,spec,frame2,surface2,FreeLearningExperiment.Parameters(),147,52.72097f,horizon,
            causalPolicy:x=>{var d=FreeLearningController.Choose(x,probe.Policy,spec,frame2,surface2,FreeLearningExperiment.Parameters(),147,52.72097f);originalPreflightForecasts+=d.Forecasts;return(d.Action,d.ForceEvaluations);},reviewSeconds:FreeLearningController.ReviewSeconds);
        preflightQueries+=prediction.ForceEvaluations;
    }
}
using(var prior=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"prediction-validation.json"))))
    if(prior.RootElement.GetProperty("Evaluations").GetInt64()!=preflightQueries)throw new InvalidOperationException("Preflight forecast recount changed.");
queries+=preflightQueries;forecasts+=originalPreflightForecasts;
foreach(var name in new[]{"overlay-initial","overlay-final"}){
    var run=FreeLearningExperiment.ReadEpisode(dir,name);var spec=FreeLearningExperiment.Rider(run.Scenario);
    foreach(var d in run.Decisions){
        var input=run.Samples.Single(s=>Math.Abs(s.Input.Time-d.Time)<1e-8).Input;
        input=input with{Pending=input.Pending.Where(c=>c.ObservedAt<input.Time-1e-8).ToArray(),NextReview=input.Time};
        var copy=FreeLearningController.Choose(input,run.Policy,spec,frame2,surface2,FreeLearningExperiment.Parameters(),147,52.72097f);queries+=copy.ForceEvaluations;forecasts+=copy.Forecasts;
        if(copy.Action!=d.Action)throw new InvalidOperationException("Decision mismatch at "+name+"/"+d.Time);decisionsMatched++;
    }
    var origins=run.Samples.Where(s=>s.Input.Rider.Road.IsTurn&&s.Input.Time+1<run.Metrics.DurationSeconds).Where((_,i)=>i%150==0).Take(3).ToArray();
    foreach(var origin in origins)foreach(var horizon in new[]{.3,.6,.9}){
        var actual=run.Samples.FirstOrDefault(s=>Math.Abs(s.Input.Time-origin.Input.Time-horizon)<1e-7);if(actual is null)continue;
        forecasts++;var prediction=FreeRacingModel.Predict(origin.Input,spec,frame2,surface2,FreeLearningExperiment.Parameters(),147,52.72097f,horizon,
            causalPolicy:input=>{var d=FreeLearningController.Choose(input,run.Policy,spec,frame2,surface2,FreeLearningExperiment.Parameters(),147,52.72097f);forecasts+=d.Forecasts;return(d.Action,d.ForceEvaluations);},reviewSeconds:FreeLearningController.ReviewSeconds);
        queries+=prediction.ForceEvaluations;var end=prediction.Points[^1];var v=actual.Input.Rider;
        var xy=Vector2.Distance(end.Rider.Position,v.Position);var speed=Math.Abs(end.Rider.Speed-v.Speed);
        var chi=Math.Abs(CoreSim.PhysicalSpace.BikeAngles.Wrap(Math.Atan2(end.Rider.Velocity.Y,end.Rider.Velocity.X)-Math.Atan2(v.Velocity.Y,v.Velocity.X)))*180/Math.PI;
        var beta=Math.Abs(CoreSim.PhysicalSpace.BikeAngles.Wrap(end.Rider.SlideRadians-v.SlideRadians))*180/Math.PI;var steering=Math.Abs(end.Steering-actual.Input.Steering)*180/Math.PI;
        var omega=Math.Abs(end.Omega-actual.Input.Omega);var yaw=Math.Abs(end.Rider.AngularVelocity-v.AngularVelocity);
        var optimism=Math.Max(end.Inner-actual.Inner,end.Outer-actual.Outer);
        var passed=xy<=.1&&speed<=.2&&chi<=1&&beta<=1&&steering<=1.5&&omega<=2&&yaw<=.05&&optimism<=.1;allPassed&=passed;
        records.Add(new{Name=name,Origin=origin.Input.Time,Horizon=horizon,XY=xy,Speed=speed,Chi=chi,Beta=beta,Steering=steering,Omega=omega,Yaw=yaw,ClearanceOptimism=optimism,Passed=passed});
    }
}
FreeLearningExperiment.Save(dir,"post-training-prediction.json",new{Passed=allPassed,Records=records,DecisionsMatched=decisionsMatched,NativeWorlds=0,Evaluations=queries,Forecasts=forecasts,OriginalPreflightForecasts=originalPreflightForecasts,RecountedPreflightEvaluations=preflightQueries,CpuSeconds=(process.TotalProcessorTime-start).TotalSeconds});
Console.WriteLine($"Offline: {decisionsMatched} exact decisions; bend predictions pass={allPassed}; {queries} evaluations; no native world.");
