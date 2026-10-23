using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public static class RobustLearningAudit
{
    public static void Offline(string directory,string raw,RobustBudget budget){
        var n=budget.Reserve("audit129-offline","offline",native:false);var cpu=Process.GetCurrentProcess().TotalProcessorTime;long evaluations=0,forecasts=0;
        var old="docs/experiments/free-racing-learning";var frame=RacingLineRootCauseAudit.Frame();var boundary=new RacingLineGeometry(frame);
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var p=FreeLearningExperiment.Parameters();
        var failed=FreeLearningExperiment.ReadEpisode(old,"final-holdout-14-5");var spec=FreeLearningExperiment.Rider(failed.Scenario);
        var styles=new List<object>();
        foreach(var label in new[]{"after-100","after-200","final"}){
            var run=FreeLearningExperiment.ReadEpisode(old,"overlay-"+label);var policy=run.Policy;
            var requestChanges=run.Decisions.Zip(run.Decisions.Skip(1),(a,b)=>Math.Abs(a.Action.SteeringRadians-b.Action.SteeringRadians)*180/Math.PI).ToArray();
            var gate=run.Metrics.GateSeconds;var atGate=gate is{} t?run.Samples.Single(s=>Math.Abs(s.Input.Time-t)<1e-7):null;
            var saturated=FreeLearningPolicy.Names.Where((_,i)=>Math.Abs(policy.Values[i]-FreeLearningPolicy.Lower[i])<1e-8||Math.Abs(policy.Values[i]-FreeLearningPolicy.Upper[i])<1e-8).ToArray();
            styles.Add(new{Checkpoint=label,Policy=policy,Saturated=saturated,Metrics=run.Metrics,GateDistance=atGate?.Input.Rider.DistanceMeters,
                RequestTravelDegrees=requestChanges.Sum(),ChangesAtLeast5Degrees=requestChanges.Count(d=>d>=5-1e-5),MaximumChangeDegrees=requestChanges.Max()});
        }
        var shadow=new List<object>();double? intervene=null;FreeRacingAction? alternative=null;int exactDecisions=0;
        foreach(var saved in failed.Decisions){
            var input=failed.Samples.Single(s=>Math.Abs(s.Input.Time-saved.Time)<1e-8).Input;
            input=input with{Pending=input.Pending.Where(c=>c.ObservedAt<input.Time-1e-8).ToArray(),NextReview=input.Time};
            FreeLearningDecision Choose(double h){var d=FreeLearningController.Choose(input,failed.Policy,spec,frame,surface,p,147,52.72097f,h);evaluations+=d.ForceEvaluations;forecasts+=d.Forecasts;return d;}
            var shortDecision=Choose(.6);if(shortDecision.Action!=saved.Action)throw new InvalidOperationException("129 default decision changed.");exactDecisions++;
            var longDecision=Choose(.9);var shortAtLong=longDecision.Candidates.Single(c=>c.Action==shortDecision.Action);
            var chosenLong=longDecision.Candidates.Single(c=>c.Action==longDecision.Action);
            var safer=chosenLong.Safety<shortAtLong.Safety&&chosenLong.Safety==FreeActionSafety.SafeInHorizon;
            if(intervene is null&&safer){intervene=input.Time;alternative=longDecision.Action;}
            shadow.Add(new{Time=input.Time,Short=shortDecision,Long=longDecision,ShortChoiceAtLong=shortAtLong,LongChoice=chosenLong,LocallySaferAlternative=safer});
        }
        var predictionRecords=new List<object>();var predictionPassed=true;
        foreach(var name in new[]{"after-100-holdout-14-5","after-200-holdout-14-5","final-holdout-14-5"}){
            var run=FreeLearningExperiment.ReadEpisode(old,name);var rider=FreeLearningExperiment.Rider(run.Scenario);
            foreach(var origin in new[]{3.2,4.4,5.8})foreach(var horizon in new[]{.3,.6,.9}){
                var start=run.Samples.Single(s=>Math.Abs(s.Input.Time-origin)<1e-8);var actual=run.Samples.Single(s=>Math.Abs(s.Input.Time-origin-horizon)<1e-8);
                forecasts++;var prediction=FreeRacingModel.Predict(start.Input,rider,frame,surface,p,147,52.72097f,horizon,
                    causalPolicy:input=>{var d=FreeLearningController.Choose(input,run.Policy,rider,frame,surface,p,147,52.72097f);forecasts+=d.Forecasts;return(d.Action,d.ForceEvaluations);},reviewSeconds:.2);
                evaluations+=prediction.ForceEvaluations;var end=prediction.Points[^1];var v=actual.Input.Rider;
                var xy=Vector2.Distance(end.Rider.Position,v.Position);var speed=Math.Abs(end.Rider.Speed-v.Speed);
                var chi=Math.Abs(BikeAngles.Wrap(Math.Atan2(end.Rider.Velocity.Y,end.Rider.Velocity.X)-Math.Atan2(v.Velocity.Y,v.Velocity.X)))*180/Math.PI;
                var beta=Math.Abs(BikeAngles.Wrap(end.Rider.SlideRadians-v.SlideRadians))*180/Math.PI;var steering=Math.Abs(end.Steering-actual.Input.Steering)*180/Math.PI;
                var omega=Math.Abs(end.Omega-actual.Input.Omega);var yaw=Math.Abs(end.Rider.AngularVelocity-v.AngularVelocity);var optimism=Math.Max(end.Inner-actual.Inner,end.Outer-actual.Outer);
                var passed=xy<=.1&&speed<=.2&&chi<=1&&beta<=1&&steering<=1.5&&omega<=2&&yaw<=.05&&optimism<=.1;predictionPassed&=passed;
                predictionRecords.Add(new{Name=name,Origin=origin,Horizon=horizon,XY=xy,Speed=speed,Chi=chi,Beta=beta,Steering=steering,Omega=omega,Yaw=yaw,ClearanceOptimism=optimism,Passed=passed});
            }
        }
        var ablations=new List<object>();var prior=RobustLearningExperiment.Reference("129-200");
        foreach(var origin in new[]{3.2,4.8,5.8})foreach(var index in new[]{1,4,5,6}){
            var input=failed.Samples.Single(s=>Math.Abs(s.Input.Time-origin)<1e-8).Input;
            input=input with{Pending=input.Pending.Where(c=>c.ObservedAt<input.Time-1e-8).ToArray(),NextReview=input.Time};
            var values=failed.Policy.Values;values[index]=prior.Values[index];var policy=FreeLearningPolicy.From(values);
            var d=FreeLearningController.Choose(input,policy,spec,frame,surface,p,147,52.72097f);evaluations+=d.ForceEvaluations;forecasts+=d.Forecasts;
            ablations.Add(new{Time=origin,Parameter=FreeLearningPolicy.Names[index],Replaced=failed.Policy.Values[index],With=values[index],Decision=d,
                Scope="One coefficient intervention on the identical saved state; proves a local decision effect only, not an entire native counterfactual."});
        }
        var a=failed.Samples[^2].Input;var b=failed.Samples[^1].Input;var dt=b.Time-a.Time;
        var force=TwoContactBikeDynamics.Forces(a.Rider,spec,frame,surface,p,a.Time,147,new(a.Omega*.33),new(a.Steering),(float)dt,steeringOverride:a.Active.SteeringRadians,inertia:52.72097f);evaluations++;
        var certificate=RobustSweepCertificate.Refine(a.Rider,b.Rider,dt,force,147,52.72097f,p.SolverSubsteps,boundary);
        RobustLearningExperiment.Save(directory,"audit129.json",new{NativeWorlds=0,Styles=styles,Shadow=shadow,ExactDefaultDecisions=exactDecisions,Predictions=predictionRecords,PredictionPassed=predictionPassed,
            Ablations=ablations,LastIntervalCertificate=certificate,InterveneAt=intervene,Alternative=alternative,
            Diagnosis="Observed generalization regression and more aggressive style; no causal proof of catastrophic forgetting because129 never re-tested earlier training levels. Four saturated weights co-occur with longer XY and higher beta. One-at-a-time ablations are local, not whole-run causes. Main A/B keeps the legacy certificate and0.6s horizon.",
            InterventionRule="Earliest OLD129 observation where the0.9s ranking offers a safe action and the0.6s chosen action is uncertain/unsafe at0.9s; request exactly one alternative, with full reaction, then resume0.6s feedback. Not a claim of globally earliest avoidable danger.",
            CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,Evaluations=evaluations,Forecasts=forecasts});
        budget.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,evaluations,forecasts);
        Console.WriteLine($"Offline129: exact={exactDecisions}, prediction pass={predictionPassed}, refined final interval={certificate.Minimum:F5}, intervention={intervene}; native worlds=0.");
    }

    public static void Diagnose(string directory,string raw,RobustBudget budget){
        var audit=RobustLearningExperiment.Read<System.Text.Json.JsonElement>(directory,"audit129.json");
        var original=FreeLearningExperiment.ReadEpisode("docs/experiments/free-racing-learning","final-holdout-14-5");
        var continued=RobustLearningExperiment.Execute(directory,raw,budget,"diagnostic","diagnostic-boundary",161,original.Policy,original.Scenario,refinedDiagnostic:true);
        var prefixExact=original.Samples.All(old=>continued.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-old.Input.Time)<1e-8).Input.Rider==old.Input.Rider);
        RobustEpisode? alternate=null;
        if(audit.GetProperty("InterveneAt").ValueKind==System.Text.Json.JsonValueKind.Number){
            var time=audit.GetProperty("InterveneAt").GetDouble();var action=audit.GetProperty("Alternative").Deserialize<FreeRacingAction>(RobustLearningExperiment.Json);
            alternate=RobustLearningExperiment.Execute(directory,raw,budget,"diagnostic","diagnostic-one-command",161,original.Policy,original.Scenario,refinedDiagnostic:true,interveneAt:time,intervention:action);
        }
        RobustLearningExperiment.Save(directory,"boundary-diagnostic.json",new{NativeWorlds=alternate is null?1:2,OriginalStop=original.Metrics.DurationSeconds,OriginalEndpointOuter=original.Samples[^1].Outer,
            OriginalPrefixBitExact=prefixExact,Continued=continued.Metrics,Alternate=alternate?.Metrics,
            Scope="Refined tube certification only in these diagnostic worlds; original forces, substeps,0.24s reaction and0.6s main policy. A/B and sealed test retain the original conservative acceptance rule."});
        if(!prefixExact)throw new InvalidOperationException("Native diagnostic prefix differs from129.");
    }
}
