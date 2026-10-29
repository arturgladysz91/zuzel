using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
using Speed148;
using Limits149;
namespace Robust150;
/// <summary>Frozen exact physical providers; no new force, integration or geometry arithmetic.</summary>
public sealed class Runtime150{
 readonly ExactRuntime148? a;readonly ExactRuntime149? b;readonly WorldParameters physicsParameters;readonly CoreSim.TrackStateSnapshot frozenSurface;readonly Dictionary<string,double> alphas=new();public long FeedbackRequests {get;private set;}public long FeedbackHits {get;private set;}
 public TrackFrame Frame {get;}public RiderSpec Spec {get;}public bool Separated {get;}
 public float InverseStepSeconds {get;}public bool Spatial {get;}public bool AcceptedOperatingPoint {get;}public bool Joint {get;}
 public Runtime150(RiderSpec spec,bool separated,bool workBudget=true,float feedbackStep=.01f,bool spatial=false,bool accepted=false,bool joint=false){Joint=joint;AcceptedOperatingPoint=accepted;Spec=spec;Separated=separated;InverseStepSeconds=feedbackStep;Spatial=spatial;Frame=StandingWorld144.Frame();var p=FreeLearningExperiment.Parameters();var surface=MultiActionExperiment135.Surface(Frame);physicsParameters=p;frozenSurface=surface;var options=new RuntimeOptions147(EventSearch:true,WorkBudget:workBudget,ProposalsPerThrottle:2,MaximumActualForecasts:8192,MaximumContacts:700000,OptionalSearchContacts:500000);if(separated)b=new(spec,Frame,surface,p,options);else a=new(spec,Frame,surface,p,options);}
 public Response150 Response(State146 state,float drive,float steer){FeedbackContactQueries++;FeedbackRequests++;var v=state.Input.Rider with{Drive=drive};var wheel=ForecastFork148.Wheel(state.Input.Omega);var shaft=new CrankClutch144(omega:state.CrankOmega);var p=physicsParameters;var steering=new TwoContactBikeState(steer);var h=InverseStepSeconds;
  var force=Separated?Contact149.Forces(v,Spec,Frame,frozenSurface,p,state.Input.Time,147,wheel,steering,h,steeringOverride:steer,inertia:52.72097f,powertrain:shaft,releaseAt:RiderController.ReactionSeconds(Spec)):Contact144.Forces(v,Spec,Frame,frozenSurface,p,state.Input.Time,147,wheel,steering,h,steeringOverride:steer,inertia:52.72097f,powertrain:shaft,releaseAt:RiderController.ReactionSeconds(Spec));var alpha=force.NetTorque/52.72097f;var arm=force.RearArm;var acceleration=force.NetForce/147+new System.Numerics.Vector2(-arm.Y,arm.X)*alpha-arm*(v.AngularVelocity*v.AngularVelocity);var uDot=System.Numerics.Vector2.Dot(acceleration,force.BodyForward)+v.AngularVelocity*System.Numerics.Vector2.Dot(force.RearVelocity,force.BodyLeft);var u=System.Numerics.Vector2.Dot(force.RearVelocity,force.BodyForward);return new(alpha,.33*(wheel.Prepared.AfterOmega-state.Input.Omega)/h-uDot,.33*state.Input.Omega-u);
 }
 public void Begin(double time){alphas.Clear();if(a is not null)a.Begin(time);else b!.Begin(time);}
 public Transition146 Step(State146 state,FreeRacingAction action){return a is not null?a.Step(state,action,.2):b!.Step(state,action,.2);}
 public Decision144 Seed(State146 state)=>a is not null?a.Seed(state):b!.Seed(state);
 bool independent;
 sealed class Scope150(IDisposable inner,Action restore):IDisposable{public void Dispose(){inner.Dispose();restore();}}
 public IDisposable Operation(string role){var before=independent;if(role=="independent_preflight")independent=true;return new Scope150(a is not null?a.Operation(role):b!.Operation(role),()=>independent=before);}
 public object Counters(){var node=System.Text.Json.JsonSerializer.SerializeToNode(a is not null?a.Counters():b!.Counters(),Program150.Json)!.AsObject();node["FeedbackContactQueries"]=FeedbackContactQueries;node["FeedbackRequests"]=FeedbackRequests;node["FeedbackHits"]=FeedbackHits;node["TotalContactQueriesWithFeedback"]=Contacts+FeedbackContactQueries;return node;}
 public long Forecasts=>a?.ActualForecasts??b!.ActualForecasts;public long Contacts=>a?.ActualContacts??b!.ActualContacts;public long K1Calls=>a?.FullK1Executions??b!.FullK1Executions;
 public long FeedbackContactQueries {get;private set;}
 public double LocalAlpha(State146 state,FreeRacingAction action,float steer){FeedbackRequests++;var key=ExactKey147.State(state)+"/"+steer.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"/"+action.Drive.ToString("R",System.Globalization.CultureInfo.InvariantCulture);if(!independent&&alphas.TryGetValue(key,out var hit)){FeedbackHits++;return hit;}FeedbackContactQueries++;var v=state.Input.Rider with{Drive=action.Drive};var wheel=ForecastFork148.Wheel(state.Input.Omega);var shaft=new CrankClutch144(omega:state.CrankOmega);var p=physicsParameters;var surface=frozenSurface;var steering=new TwoContactBikeState(steer);
  var force=Separated?Contact149.Forces(v,Spec,Frame,surface,p,state.Input.Time,147,wheel,steering,InverseStepSeconds,steeringOverride:steer,inertia:52.72097f,powertrain:shaft,releaseAt:RiderController.ReactionSeconds(Spec)):Contact144.Forces(v,Spec,Frame,surface,p,state.Input.Time,147,wheel,steering,InverseStepSeconds,steeringOverride:steer,inertia:52.72097f,powertrain:shaft,releaseAt:RiderController.ReactionSeconds(Spec));var alpha=(double)(force.NetTorque/52.72097f);if(!independent)alphas[key]=alpha;return alpha;
 }
}
