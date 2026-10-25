using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public static class TrackAwarenessAudit134
{
    private static readonly string[] Geometries={"progressive-b","progressive-c","egg-mild","egg-medium","egg-asymmetric"};
    public static void Geometry(string d,string raw,RobustBudget b){
        var rows=new List<object>();bool all=true;
        foreach(var name in Geometries){
            var offline= b.Reserve("geometry-static-"+name,"offline",false);var staticCpu=Process.GetCurrentProcess().TotalProcessorTime;
            var frame=TrackAwarenessExperiment134.Frame(name);var boundary=new RacingLineGeometry(frame);double projection=0,wrap=0,minGap=double.PositiveInfinity,maxK=0,minWidth=100,maxWidth=0;
            for(double s=0;s<frame.LapMeters;s+=1)foreach(var offset in new[]{2f,5f,8f}){
                var q=frame.At(s,offset);var road=frame.Project(q.Position);projection=Math.Max(projection,Math.Abs(frame.UnwrapDelta(s,road.ReferenceMeters)));
                wrap=Math.Max(wrap,Vector2.Distance(q.Position,frame.At(s+frame.LapMeters,offset).Position));
                var k=frame.Shape!.At(s).Curvature;maxK=Math.Max(maxK,Math.Abs(k));var width=frame.PhysicalWidthMeters(q.Road);minWidth=Math.Min(minWidth,width);maxWidth=Math.Max(maxWidth,width);
                var rider=new RiderView(1,q.Position,q.Road.Tangent*10,(float)q.Road.Heading,0,q.Road,s,0,RiderActivity.Racing,1,offset,0);
                var gap=boundary.Clearance(rider);minGap=Math.Min(minGap,Math.Min(gap.Inner,gap.Outer));
            }
            var valid=projection<=.0002&&wrap<=.00001&&minGap>0&&maxK<.1&&minWidth>=12&&maxWidth<=17.00001;all&=valid;
            b.Complete(offline,(Process.GetCurrentProcess().TotalProcessorTime-staticCpu).TotalSeconds,0,0);
            // A tiny held-command world checks runner support, not rider skill
            // or performance on the held-out geometry. No policy forecast here.
            var scenario=new AwarenessCase134("geometry-"+name,name,new("geometry-"+name,2,10,5,18.125),false);
            var native=TrackAwarenessExperiment134.Execute(d,raw,b,"geometry","geometry-"+name,new("geometry",12023,TrackAwarenessExperiment134.StartPolicy(),false),scenario,.02,holdAt:0,held:new(0,0));
            valid&=native.Metrics.Native.Outcome=="probe"&&native.Metrics.Native.MinimumClearanceMeters>0;all&=valid;
            File.WriteAllText(Path.Combine(d,name+"-geometry.json"),JsonSerializer.Serialize(ReplayGeometry.Create(frame),ReplayGeometry.Json)+"\n");
            rows.Add(new{Geometry=name,Passed=valid,MaximumProjectionReferenceError=projection,MaximumWrapXYError=wrap,MinimumSampledCapsuleGap=minGap,MaximumCurvature=maxK,MinimumWidth=minWidth,MaximumWidth=maxWidth,frame.LapMeters,Native=native.Metrics,
                Scope="Metric/projection/width/closure checks plus0.02s native capability probe. No racing result, held-out policy selection or geometry fitting."});
        }
        TrackAwarenessExperiment134.Save(d,"geometry-preflight.json",new{Passed=all,Checks=rows,FinalWorlds=0,TrainingWorlds=0});
        if(!all)throw new InvalidOperationException("Invalid geometry/runner; do not count as rider DNF.");
    }
    public static void Audit(string d,RobustBudget budget){
        var reservation=budget.Reserve("old-knowledge-offline","offline",false);var cpu=Process.GetCurrentProcess().TotalProcessorTime;long contacts=0,forecasts=0;var rows=new List<object>();bool exact=true;
        var run=RiderDecisionExperiment133.Historical(PhysicalSteeringAudit.PreviousPath("../rider-decision-optimization"),"final-133-A0-X00");
        var frame=TrackAwarenessExperiment134.Frame("progressive-b");var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var p=FreeLearningExperiment.Parameters();
        foreach(var decision in run.Replay.Decisions.Where((_,i)=>i%10==0)){
            var sample=run.Replay.Samples.Single(s=>Math.Abs(s.Input.Time-decision.Time)<1e-8);var input=RiderDecisionAudit133.BeforeDecision(sample);var spec=FreeLearningExperiment.Rider(run.Replay.Scenario);
            var a=FreeLearningController.Choose(input,run.Replay.Policy,spec,frame,surface,p,147,52.72097f,.9,true,true);
            var newer=FreeLearningController.Choose(input,run.Replay.Policy,spec,frame,surface,p,147,52.72097f,.9,true,true,false,true);
            exact&=a.Action==decision.Action&&a.Candidates.SequenceEqual(decision.Candidates);
            var held=FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,.9,a.Action);var end=held.Points[^1].Rider;
            var ahead=Math.Max(.1,frame.ReferenceSpeed(end.Road,end.Speed)*run.Replay.Policy.PreviewSeconds);var target=frame.RoadAt(end.TotalReferenceMeters+ahead,end.Road.OffsetMeters);
            rows.Add(new{Time=input.Time,Input=input,Original=a,WithKnowledge=newer,Knowledge=TrackAwareness134.Observe(input,frame,p),
                OriginalGeometryQuery=new{PhysicalHorizonSeconds=.9,PreviewSeconds=run.Replay.Policy.PreviewSeconds,EndpointReference=end.TotalReferenceMeters,PostEndpointAheadReferenceMeters=ahead,QueryReference=target.ReferenceMeters,
                    TotalAheadReferenceMeters=end.TotalReferenceMeters-input.Rider.TotalReferenceMeters+ahead,Direction="chi at predicted endpoint versus tangent at geometry query, not frame psi"}});
            contacts+=a.ForceEvaluations+newer.ForceEvaluations+held.ForceEvaluations;forecasts+=a.Forecasts+newer.Forecasts+1;
        }
        budget.Complete(reservation,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,contacts,forecasts);
        TrackAwarenessExperiment134.Save(d,"old-knowledge-audit.json",new{Off133DecisionsExact=exact,Rows=rows,Scope="Read-only historical133A0 native states. Identical H09 forecasts, no new native world. Arrival, coast continuation and width reach estimates remain heuristic."});
        if(!exact)throw new InvalidOperationException("OFF objective differs from133.");
    }
    private static bool ValidatePrediction(FreeRacingPrediction prediction,RobustEpisode actual,out object result){
        var points=new List<object>();bool ok=true;int compared=0;double Degrees(double x)=>Math.Abs(BikeAngles.Wrap(x))*180/Math.PI;
        foreach(var q in prediction.Points){
            var sample=actual.Replay.Samples.OrderBy(s=>Math.Abs(s.Input.Time-q.Time)).First();var delta=Math.Abs(q.Time-sample.Input.Time);
            var required=Math.Abs(q.Time/.01-Math.Round(q.Time/.01))<1e-6||Math.Abs(q.Time-prediction.Points[0].Time-.9)<1e-8;
            if(delta>1e-7){if(required)ok=false;points.Add(new{q.Time,NativeTime=sample.Input.Time,Compared=false,RequiredGridPoint=required,DeltaSeconds=delta});continue;}
            var v=sample.Input.Rider;var xy=Vector2.Distance(q.Rider.Position,v.Position);var speed=Math.Abs(q.Rider.Speed-v.Speed);var chi=Degrees(Math.Atan2(q.Rider.Velocity.Y,q.Rider.Velocity.X)-Math.Atan2(v.Velocity.Y,v.Velocity.X));
            var psi=Degrees(q.Rider.Heading-v.Heading);var beta=Degrees(q.Rider.SlideRadians-v.SlideRadians);var steering=Degrees(q.Steering-sample.Input.Steering);var omega=Math.Abs(q.Omega-sample.Input.Omega);var yaw=Math.Abs(q.Rider.AngularVelocity-v.AngularVelocity);
            var optimism=Math.Max(q.Inner-sample.Inner,q.Outer-sample.Outer);var passed=xy<=.1&&speed<=.2&&chi<=1&&psi<=1&&beta<=1&&steering<=1.5&&omega<=2&&yaw<=.05&&optimism<=.1;
            ok&=passed;compared++;points.Add(new{q.Time,NativeTime=sample.Input.Time,Compared=true,Passed=passed,RequiredGridPoint=required,XY=xy,Speed=speed,Chi=chi,Psi=psi,Beta=beta,Steering=steering,Omega=omega,Yaw=yaw,ClearanceOptimism=optimism});
        }
        ok&=compared>=91;result=new{Passed=ok,Compared=compared,Points=points,Tolerances="Exactly132: XY.1m speed.2m/s chi/psi/beta1deg steer1.5deg omega2rad/s yaw.05rad/s clearance optimism.1m. All91 regular points/end required; distinct microsecond curvature events retained, no interpolation."};return ok;
    }
    public static void Preflight(string d,string raw,RobustBudget b){
        if(!TrackAwarenessExperiment134.Read<JsonElement>(d,"geometry-preflight.json").GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Geometry gate closed.");
        var profiles=new List<RobustEpisode>();foreach(var aware in new[]{false,true})foreach(var c in TrackAwarenessExperiment134.Diagnostic)
            profiles.Add(TrackAwarenessExperiment134.Execute(d,raw,b,"diagnostic",$"profile-{(aware?"B":"A")}-{c.Name}",new("profile",12023,TrackAwarenessExperiment134.StartPolicy(),aware),c));
        bool repeated=true;foreach(var aware in new[]{false,true}){
            var label=aware?"B":"A";var prior=profiles.Single(r=>r.Replay.Name==$"profile-{label}-P1");var c=TrackAwarenessExperiment134.Diagnostic[1];
            var repeat=TrackAwarenessExperiment134.Execute(d,raw,b,"diagnostic","repeat-"+label,new("repeat",12023,TrackAwarenessExperiment134.StartPolicy(),aware),c);repeated&=repeat.Metrics.Native.Fingerprint==prior.Metrics.Native.Fingerprint;
        }
        var old=RiderDecisionExperiment133.Historical(PhysicalSteeringAudit.PreviousPath("../rider-decision-optimization"),"final-133-A0-X00");
        var parityCase=new AwarenessCase134("OFF133-X00","progressive-b",old.Replay.Scenario,true);
        var parity=TrackAwarenessExperiment134.Execute(d,raw,b,"diagnostic","off133-X00",new("off133",12023,TrackAwarenessExperiment134.StartPolicy(),false),parityCase);
        var off=parity.Metrics.Native.Fingerprint==old.Metrics.Native.Fingerprint;bool predictionOK=true;var validations=new List<object>();
        foreach(var aware in new[]{false,true})foreach(var i in new[]{1,2}){
            var c=TrackAwarenessExperiment134.Diagnostic[i];var run=profiles.Single(r=>r.Replay.Name==$"profile-{(aware?"B":"A")}-{c.Name}");var input=RiderDecisionAudit133.BeforeDecision(run.Replay.Samples[0]);var action=run.Replay.Decisions[0].Action;
            var frame=TrackAwarenessExperiment134.Frame(c.Geometry);var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var n=b.Reserve($"predict-{aware}-{c.Name}","offline",false);var cpu=Process.GetCurrentProcess().TotalProcessorTime;
            var forecast=FreeRacingModel.Predict(input,FreeLearningExperiment.Rider(c.Scenario),frame,surface,FreeLearningExperiment.Parameters(),147,52.72097f,.9,action);
            b.Complete(n,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,forecast.ForceEvaluations,1);
            var native=TrackAwarenessExperiment134.Execute(d,raw,b,"diagnostic",$"held-{(aware?"B":"A")}-{c.Name}",new("held",12023,TrackAwarenessExperiment134.StartPolicy(),aware),c,.9,holdAt:0,held:action);
            predictionOK&=ValidatePrediction(forecast,native,out var comparison);validations.Add(new{Aware=aware,c.Geometry,c.Name,Action=action,Comparison=comparison});
        }
        TrackAwarenessExperiment134.Save(d,"predictor.json",new{Passed=predictionOK,Checks=validations,ThresholdsChanged=false});
        // Finite, predeclared one-command family; every later review restores A.
        var timing=new List<object>();foreach(var i in new[]{1,3})foreach(var time in new[]{0d,.2,.4,.6})foreach(var action in new[]{new FreeRacingAction(5*MathF.PI/180,0),new(15*MathF.PI/180,(float)TrackAwarenessExperiment134.StartPolicy().Drive)}){
            var c=TrackAwarenessExperiment134.Diagnostic[i];var baseline=profiles.Single(r=>r.Replay.Name=="profile-A-"+c.Name);
            var name=$"timing-{c.Name}-{time*100:000}-{(action.Drive==0?"coast":"drive")}";var native=TrackAwarenessExperiment134.Execute(d,raw,b,"diagnostic",name,new("timing",12023,TrackAwarenessExperiment134.StartPolicy(),false),c,interveneAt:time,alternative:action);
            var activation=time+.24;var prefix=baseline.Replay.Samples.Where(s=>s.Input.Time<activation-1e-8).All(s=>native.Replay.Samples.Any(t=>Math.Abs(t.Input.Time-s.Input.Time)<1e-8&&t.Input.Rider==s.Input.Rider&&t.Input.Omega==s.Input.Omega&&t.Input.Steering==s.Input.Steering));
            if(!prefix)throw new InvalidOperationException("Causal pending/reaction prefix differs.");
            timing.Add(new{c.Name,c.Geometry,Observation=time,Activation=activation,Action=action,ExactPrefix=true,Baseline=baseline.Metrics,Trial=native.Metrics,RawName=name,Scope="One existing command then restored A; last successful among only2actions x4times,0.2s resolution, not a global last possible reaction."});
        }
        TrackAwarenessExperiment134.Save(d,"reaction-continuations.json",timing);
        var a=profiles.Where(r=>r.Replay.Name.StartsWith("profile-A",StringComparison.Ordinal)).ToArray();var newer=profiles.Where(r=>r.Replay.Name.StartsWith("profile-B",StringComparison.Ordinal)).ToArray();
        var paired=a.Zip(newer).Where(x=>x.First.Replay.Scenario.Name!="P0"&&x.First.Metrics.Native.Completed&&x.Second.Metrics.Native.Completed).Select(x=>x.Second.Metrics.Native.GateSeconds!.Value/x.First.Metrics.Native.GateSeconds!.Value-1).Order().ToArray();
        var median=paired.Length==0?(double?)null:paired.Length%2==1?paired[paired.Length/2]:(paired[paired.Length/2-1]+paired[paired.Length/2])/2;
        var performanceGate=newer.Count(r=>r.Metrics.Native.Completed)>=a.Count(r=>r.Metrics.Native.Completed)-1&&median is not null&&median<=.1;
        var gate=off&&repeated&&predictionOK&&performanceGate;
        TrackAwarenessExperiment134.Save(d,"preflight.json",new{TrainingGatePassed=gate,Off133Exact=off,NativeRepeatsExact=repeated,PredictorPassed=predictionOK,PerformanceGate=performanceGate,PairedTwoMedianRatio=median,
            Profiles=profiles.Select(r=>new{r.Replay.Name,r.GeometryName,r.Metrics}),FinalWorlds=0,TrainingWorlds=0,Scope="If the gate fails, diagnose before training. Do not change physics, H09, action grid, or open final to tune the mechanism."});
        if(!gate)throw new InvalidOperationException("Preflight closed training; diagnose the recorded cause.");
    }
}
