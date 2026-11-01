using System.Numerics;
using Autonomous146;
using CoreSim.PhysicalSpace;
using Robust150;
using Safety153;
using SharedWorldSpeedway;
namespace ThreeLap158;
public static class Feedback158 {
 public static long JointCalls{get;private set;}
 public static FreeRacingAction Action(Runtime158 rt,State146 observed,Tape150 ownPlan,Control158 control){
  var k=Math.Clamp((int)Math.Round((observed.Input.Time-ownPlan.CreatedAt)/.2),0,ownPlan.Actions.Length-1);var reference=ownPlan.States[k];var action=ownPlan.Actions[k];
  // Queue equality is necessary even when every body/wheel/crank bit agrees.
  if(Efficient147.ExactKey147.State(observed)==Efficient147.ExactKey147.State(reference))return action;
  var future=control.ActivationState(observed);var expected=control.ActivationState(reference);var v=future.Input.Rider;var r=expected.Input.Rider;
  var tau=2*(RiderController.ReactionSeconds(rt.Spec)+FreeLearningController.ReviewSeconds);var tangent=r.Speed>.5f?Vector2.Normalize(r.Velocity):r.Road.Tangent;var left=new Vector2(-tangent.Y,tangent.X);
  var lateral=Vector2.Dot(v.Position-r.Position,left);var lateralVelocity=Vector2.Dot(v.Velocity-r.Velocity,left);var heading=BikeAngles.Wrap(v.Heading-r.Heading)+(lateral/tau+lateralVelocity)/Math.Max(r.Speed,1);
  var nominal=rt.LocalAlpha(expected,action,action.SteeringRadians);var desired=nominal-2*(v.AngularVelocity-r.AngularVelocity)/tau-heading/(tau*tau);
  JointCalls++;return Joint158.Correct(rt.Alpha,future,expected,action,desired,tau,control);
 }
}
