using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record RiderDecisionAuditAction(FreeLearningCandidate Original,RiderDecisionCosts Costs,double B1Score,double B2Score,FreeRacingPoint End,double TerminalCreditMeters);
public sealed record RiderDecisionAuditState(string Run,int PolicyId,FreeLearningPolicy Policy,bool FullThrottle,FreeLearningScenario Scenario,
    FreeRacingInput Input,FreeLearningDecision Original,string Phase,RiderDecisionAuditAction[] Actions,FreeRacingAction B1,FreeRacingAction B2,double MaximumScoreError);

public static class RiderDecisionAudit133
{
    public static string Phase(int s)=>s switch{0=>"straight",1=>"entry1",2=>"middle1",3=>"exit1",4=>"preparation2",5 or 6 or 7=>"bend2",_=>"after2"};
    public static FreeRacingInput BeforeDecision(FreeRacingSample s)=>s.Input with{Pending=s.Input.Pending.Where(c=>c.ObservedAt<s.Input.Time-1e-8).ToArray(),NextReview=s.Input.Time};
    private static RiderDecisionAuditAction Best(IEnumerable<RiderDecisionAuditAction> actions,FreeRacingInput input,Func<RiderDecisionAuditAction,double> score)=>actions.OrderBy(c=>c.Original.Safety).ThenBy(score)
        .ThenBy(c=>Math.Abs(c.Original.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(c=>c.Original.Action.SteeringRadians).First();
    public static void Run(string d,string raw,RobustBudget budget){
        var reservation=budget.Reserve("cost-components-offline","offline",false);var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        var old131=PhysicalSteeringAudit.PreviousPath("../autonomous-speedway-extended-learning");var old132=PhysicalSteeringAudit.PreviousPath("../full-throttle-learning");
        var sources=new[]{("131-Q0",old131,"two-select-two-4061-Q0",false),("131-V00",old131,"final-131-final-V00",false),
            ("132-Z01",old132,"final-132-final-Z01",true),("132-D1",old132,"diag-B-D1",true)};
        var originals=new Dictionary<string,RobustEpisode>();var states=new List<RiderDecisionAuditState>();long contacts=0,forecasts=0;bool passed=true;
        var frame=RacingLineRootCauseAudit.Frame();var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var parameters=FreeLearningExperiment.Parameters();
        foreach(var (name,dir,runName,full) in sources){
            var run=RiderDecisionExperiment133.Historical(dir,runName);originals[name]=run;var spec=FreeLearningExperiment.Rider(run.Replay.Scenario);
            foreach(var decision in run.Replay.Decisions){
                var sample=run.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-decision.Time)<1e-8);var input=BeforeDecision(sample);var policy=run.Replay.Policy;
                if(!FreeLearningController.Actions(input,policy,full).SequenceEqual(decision.Candidates.Select(c=>c.Action)))throw new InvalidOperationException("Historical action space mismatch.");
                var actions=new List<RiderDecisionAuditAction>();double maximum=0;
                foreach(var candidate in decision.Candidates){
                    var p=FreeRacingModel.Predict(input,spec,frame,surface,parameters,147,52.72097f,.9,candidate.Action);contacts+=p.ForceEvaluations;forecasts++;
                    var costs=RiderDecisionObjective133.Costs(input,policy,candidate.Action,p,frame,surface,147,.9,true);maximum=Math.Max(maximum,Math.Abs(costs.Total-candidate.Score));
                    var b1=candidate.Score+policy.DirectionWeight*(RiderDecisionObjective133.DirectionShape(candidate.DirectionError,candidate.Safety,candidate.FutureClearance,candidate.Progress)-candidate.DirectionError*candidate.DirectionError*10);
                    var credit=RiderDecisionObjective133.OfflineTerminalCredit(p.Points[^1],candidate.FutureClearance,RiderController.ReactionSeconds(spec));
                    var b2=candidate.Score-policy.ProgressWeight*credit*.6/.9;actions.Add(new(candidate,costs,b1,b2,p.Points[^1],credit));
                    var clearance=p.Points.Min(q=>Math.Min(q.Inner,q.Outer));var closing=Vector2.Dot(p.Points[^1].Rider.Velocity,p.Points[^1].Rider.Road.Outward);
                    var gap=Math.Min(p.Points[^1].Inner-Math.Max(0,-closing)*.24,p.Points[^1].Outer-Math.Max(0,closing)*.24);
                    var safety=clearance<0?FreeActionSafety.Unsafe:clearance<.2||gap<.1?FreeActionSafety.Uncertain:FreeActionSafety.SafeInHorizon;
                    if(p.Points.Any(q=>Math.Abs(q.Rider.SlideRadians)>Math.PI/4))safety=FreeActionSafety.Unsafe;
                    passed&=safety==candidate.Safety;
                }
                passed&=maximum<=1e-9&&Best(actions,input,c=>c.Original.Score).Original.Action==decision.Action;
                states.Add(new(name,run.Replay.PolicyId,policy,full,run.Replay.Scenario,input,decision,Phase(input.Rider.Road.Segment),actions.ToArray(),
                    Best(actions,input,c=>c.B1Score).Original.Action,Best(actions,input,c=>c.B2Score).Original.Action,maximum));
            }
        }
        RiderDecisionExperiment133.Save(d,"cost-audit.json",states);
        RiderDecisionExperiment133.Save(d,"cost-audit-summary.json",new{Passed=passed,States=states.Count,Contacts=contacts,Forecasts=forecasts,MaximumScoreError=states.Max(s=>s.MaximumScoreError),
            B1Changed=states.Count(s=>s.B1!=s.Original.Action),B2Changed=states.Count(s=>s.B2!=s.Original.Action),Scope="Same reconstructed held predictions for all score variants. Direction error is travel chi versus preview geometry tangent, not frame psi; beta is separate. All seven terms and endpoint states saved; no new native world in this step."});
        budget.Complete(reservation,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,contacts,forecasts);
        bool b1Useful=states.Any(s=>s.Original.Action.Drive==0&&s.B1.Drive>0&&s.Actions.Single(a=>a.Original.Action==s.B1).Original.Safety==s.Original.Safety
            &&s.Actions.Single(a=>a.Original.Action==s.B1).Original.Progress>s.Actions.Single(a=>a.Original.Action==s.Original.Action).Original.Progress);
        RiderDecisionExperiment133.Save(d,"score-choice.json",new{Frozen=true,Chosen="B1-robust-direction",Passed=passed&&b1Useful,FinalWorlds=0,TrainingWorlds=0,
            Reason="Minimal change to the dominant cost shape; preserves local DirectionWeight semantics, original quadratic near terminal threat/other safety classes, all yaw/clearance costs and eight bounds. Offline B2 needs an unverified terminal reaction-distance credit and does not directly address the observed direction cost. B1 changes at least one same-class coast choice to positive greater predicted progress. This is a hypothesis for controlled native training, not a full-run safety claim.",
            B2="Offline heuristic only, never trained or combined with B1",DirectionTransitionDegrees=10,TerminalReserveMeters=.2,OriginalRankForBothTrainingArms="R0 exactly132"});
        if(!passed||!b1Useful)throw new InvalidOperationException("Offline score audit failed; no native trials/training.");
        // Small, deterministic causal audit. The alternative is one existing action,
        // not a scripted driving rule; every subsequent observation uses old autonomy.
        var baselines=new Dictionary<string,RobustEpisode>();foreach(var key in new[]{"131-Q0","132-Z01"}){
            var reference=originals[key];var c=new RiderDecisionCandidate("audit-"+key,reference.Replay.PolicyId,reference.Replay.Policy,false,key=="132-Z01");
            var baseline=RiderDecisionExperiment133.Execute(d,raw,budget,"diagnostic","causal-base-"+key,c,reference.Replay.Scenario,true);
            if(baseline.Metrics.Native.Fingerprint!=reference.Metrics.Native.Fingerprint)throw new InvalidOperationException("OFF differs from historical native reference.");baselines[key]=baseline;
        }
        var phases=new[]{("straight","131-Q0",frame.SegmentStartMeters(1)-5),("entry1","131-Q0",frame.SegmentStartMeters(1)+3),
            ("middle1","131-Q0",frame.SegmentStartMeters(2)+8),("exit1","132-Z01",frame.SegmentStartMeters(3)+3),
            ("preparation2","132-Z01",frame.SegmentStartMeters(5)-7),("bend2","132-Z01",frame.SegmentStartMeters(6)+8)};
        var trials=new List<object>();var savedForecasts=new List<object>();
        foreach(var (phase,key,target) in phases){
            var candidates=states.Where(s=>s.Run==key&&s.Phase==phase&&s.Original.Action.Drive==0&&s.Original.Safety==FreeActionSafety.SafeInHorizon)
                .Select(s=>(State:s,Chosen:s.Actions.Single(a=>a.Original.Action==s.Original.Action),Alternative:s.Actions.Where(a=>a.Original.Action.Drive>0&&a.Original.Safety==s.Original.Safety
                    &&a.Original.Progress>s.Actions.Single(x=>x.Original.Action==s.Original.Action).Original.Progress+1e-8&&a.Original.Score>s.Actions.Single(x=>x.Original.Action==s.Original.Action).Original.Score+.25)
                    .OrderBy(a=>a.Original.Score).FirstOrDefault())).Where(x=>x.Alternative is not null).OrderBy(x=>Math.Abs(x.State.Input.Rider.TotalReferenceMeters-target)).ThenBy(x=>x.State.Input.Time).ToArray();
            if(candidates.Length==0){trials.Add(new{Phase=phase,Missing=true,Reason="No frozen eligible same-class positive-progress losing candidate"});continue;}
            var x=candidates[0];var state=x.State;var alternative=x.Alternative!;var baseline=baselines[key];var c=new RiderDecisionCandidate("counter-"+key,state.PolicyId,state.Policy,false,state.FullThrottle);
            var trial=RiderDecisionExperiment133.Execute(d,raw,budget,"diagnostic","causal-"+phase,c,state.Scenario,true,state.Input.Time,alternative.Original.Action);
            var activation=state.Input.Time+.24;
            var mechanicalPrefix=baseline.Replay.Samples.Where(s=>s.Input.Time<=activation+1e-9).All(s=>trial.Replay.Samples.Any(t=>Math.Abs(t.Input.Time-s.Input.Time)<1e-8
                &&(t.Input.Rider with{Drive=0})==(s.Input.Rider with{Drive=0})&&t.Input.Omega==s.Input.Omega&&t.Input.Steering==s.Input.Steering));
            var fullPrefix=baseline.Replay.Samples.Where(s=>s.Input.Time<activation-1e-9).All(s=>trial.Replay.Samples.Any(t=>Math.Abs(t.Input.Time-s.Input.Time)<1e-8&&t.Input.Rider==s.Input.Rider));
            if(!mechanicalPrefix||!fullPrefix)throw new InvalidOperationException("Causal reaction prefix mismatch.");
            var n=budget.Reserve("causal-forecast-"+phase,"offline",false);var forecastCpu=Process.GetCurrentProcess().TotalProcessorTime;
            var p=FreeRacingModel.Predict(state.Input,FreeLearningExperiment.Rider(state.Scenario),frame,surface,parameters,147,52.72097f,.9,alternative.Original.Action);
            budget.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-forecastCpu).TotalSeconds,p.ForceEvaluations,1);
            savedForecasts.Add(new{Phase=phase,Origin=state.Input,Action=alternative.Original.Action,Prediction=p,Scope="Prediction only; not a replay trajectory"});
            trials.Add(new{Phase=phase,Source=key,Time=state.Input.Time,Activation=activation,Original=state.Original.Action,Alternative=alternative.Original.Action,MechanicalPrefixExact=mechanicalPrefix,FullRiderBeforeActivationExact=fullPrefix,
                PredictedProgressGain=alternative.Original.Progress-x.Chosen.Original.Progress,OldCosts=x.Chosen.Costs,AlternativeCosts=alternative.Costs,
                Baseline=baseline.Metrics,Trial=trial.Metrics,BaselineFirstWholeGate=FirstGate(baseline),TrialFirstWholeGate=FirstGate(trial),
                FullRunFaster=baseline.Metrics.Native.Completed&&trial.Metrics.Native.Completed&&trial.Metrics.Native.GateSeconds<baseline.Metrics.Native.GateSeconds-.01});
        }
        RiderDecisionExperiment133.Save(d,"causal-trials.json",trials);RiderDecisionExperiment133.Save(d,"causal-forecasts.json",savedForecasts);
    }
    public static double? FirstGate(RobustEpisode run){
        var frame=RacingLineRootCauseAudit.Frame();var reference=frame.SegmentStartMeters(4)+3;var gate=frame.At(reference,0);var point=new MeterPoint(gate.Position.X,gate.Position.Y);var tangent=new MeterPoint(gate.Road.Tangent.X,gate.Road.Tangent.Y);
        foreach(var s in run.Replay.Samples){var v=s.Input.Rider;if(v.TotalReferenceMeters<reference)continue;var bike=BikeFootprint.Create(new(v.Position.X,v.Position.Y),v.Heading,SpeedwayBikeDimensions.Reference);
            if(new[]{bike.Chassis,bike.Handlebar}.Min(c=>Math.Min(MeterPoint.Dot(c.Start-point,tangent),MeterPoint.Dot(c.End-point,tangent))-c.RadiusMeters)>=0)return s.Input.Time;}
        return null;
    }
    public static void Preflight(string d,string raw,RobustBudget b){
        if(!RiderDecisionExperiment133.Read<System.Text.Json.JsonElement>(d,"score-choice.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Score choice closed.");
        var profiles=new List<RobustEpisode>();foreach(var newer in new[]{false,true})for(int i=0;i<RiderDecisionExperiment133.Diagnostic.Length;i++){
            var s=RiderDecisionExperiment133.Diagnostic[i];profiles.Add(RiderDecisionExperiment133.Execute(d,raw,b,"diagnostic",$"profile-{(newer?"B":"A")}-P{i}",new("profile",4061,RiderDecisionExperiment133.StartPolicy(),newer),s,i>0));}
        bool deterministic=true;foreach(var newer in new[]{false,true}){
            var label=newer?"B":"A";var original=profiles.Single(r=>r.Replay.Name==$"profile-{label}-P1");var repeat=RiderDecisionExperiment133.Execute(d,raw,b,"diagnostic","repeat-profile-"+label,new("profile",4061,RiderDecisionExperiment133.StartPolicy(),newer),original.Replay.Scenario,true);
            deterministic&=original.Metrics.Native.Fingerprint==repeat.Metrics.Native.Fingerprint;
        }
        var historical=RiderDecisionExperiment133.Historical(PhysicalSteeringAudit.PreviousPath("../full-throttle-learning"),"final-132-final-Z01");
        var parity=RiderDecisionExperiment133.Execute(d,raw,b,"diagnostic","parity132-Z01",new("132",6022,historical.Replay.Policy,false),historical.Replay.Scenario,true);
        var repeatParity=RiderDecisionExperiment133.Execute(d,raw,b,"diagnostic","repeat132-Z01",new("132",6022,historical.Replay.Policy,false),historical.Replay.Scenario,true);
        bool exact=parity.Metrics.Native.Fingerprint==historical.Metrics.Native.Fingerprint&&repeatParity.Metrics.Native.Fingerprint==historical.Metrics.Native.Fingerprint;
        RiderDecisionExperiment133.Save(d,"preflight.json",new{Passed=deterministic&&exact,NativeRepeatsExact=deterministic,Off132FingerprintExact=exact,Profiles=profiles.Select(r=>new{r.Replay.Name,r.Metrics}),FinalWorlds=0,TrainingWorlds=0,
            Scope="Native outcomes/CPU/forecasts/contacts recorded for both scores without retraining. DNF is an experimental outcome, not a relaxed safety class. Predictor/physics sources unchanged."});
        if(!deterministic||!exact)throw new InvalidOperationException("Native preflight failed.");
    }
}
