using Autonomous146;
using Robust150;
using SharedWorldSpeedway;
using ThreeLap158;
using System.Numerics;
using CoreSim.PhysicalSpace;
namespace Dynamic160;
public static class Feedback160 {
 public static FreeRacingAction Action(Runtime158 rt,State146 observed,Tape150 ownPlan,Control160 control){
  var k=Math.Clamp((int)Math.Round((observed.Input.Time-ownPlan.CreatedAt)/.2),0,ownPlan.Actions.Length-1);
  var reference=ownPlan.States[k];var action=ownPlan.Actions[k];
  if(Efficient147.ExactKey147.State(observed)==Efficient147.ExactKey147.State(reference))return action;
  var future=control.ActivationState(observed);var expected=control.ActivationState(reference);var v=future.Input.Rider;var r=expected.Input.Rider;
  var tau=2*(RiderController.ReactionSeconds(rt.Spec)+FreeLearningController.ReviewSeconds);
  var tangent=r.Speed>.5f?Vector2.Normalize(r.Velocity):r.Road.Tangent;var left=new Vector2(-tangent.Y,tangent.X);
  double lateral=Vector2.Dot(v.Position-r.Position,left),lateralVelocity=Vector2.Dot(v.Velocity-r.Velocity,left);
  // Body heading error contains both course and beta errors, without equating yaw
  // to course rate. Targets belong only to this newly generated causal plan.
  double heading=BikeAngles.Wrap(v.Heading-r.Heading)+(lateral/tau+lateralVelocity)/Math.Max(r.Speed,1);
  double nominal=control.ActualAlpha(expected,action.SteeringRadians,action.Drive);
  double desired=nominal-2*(v.AngularVelocity-r.AngularVelocity)/tau-heading/(tau*tau);
  const float LocalPerturbationRadians=.005f;var actual=future.Input.Steering;
  // The original rate-limited actuator starts at actual steering in both probes.
  double gradient=(control.ActualAlpha(future,actual+LocalPerturbationRadians,action.Drive)-control.ActualAlpha(future,actual-LocalPerturbationRadians,action.Drive))/(2*LocalPerturbationRadians);
  double current=control.ActualAlpha(future,action.SteeringRadians,action.Drive);
  const double MaximumCorrectionRadians=.05;
  if(Math.Abs(gradient)<.01)return action;
  double correction=Math.Clamp((desired-current)/gradient,-MaximumCorrectionRadians,MaximumCorrectionRadians);
  return action with{SteeringRadians=(float)Math.Clamp(action.SteeringRadians+correction,-TwoContactBikeParameters.Default.MaximumSteeringRadians,TwoContactBikeParameters.Default.MaximumSteeringRadians)};
 }
}
