using System.Numerics;
using Autonomous146;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
namespace Robust150;
/// <summary>Tube feedback around an endogenous predicted maneuver, not an authored racing line.
/// Critical damping timescale is two existing reaction+review pipelines. No force/body writes.</summary>
public static class Feedback150{
 public static FreeRacingAction Action(Runtime150 rt,State146 state,Tape150 anchor){
  var index=Math.Clamp((int)Math.Round((state.Input.Time-anchor.CreatedAt)/.2),0,anchor.Actions.Length-1);var reference=anchor.States[index];var action=anchor.Actions[index];var v=state.Input.Rider;var r=reference.Input.Rider;
  if(v.Position==r.Position&&v.Velocity==r.Velocity&&v.Heading==r.Heading&&v.AngularVelocity==r.AngularVelocity&&state.Input.Omega==reference.Input.Omega&&state.CrankOmega==reference.CrankOmega)return action;
  var tau=2*(RiderController.ReactionSeconds(rt.Spec)+FreeLearningController.ReviewSeconds);var course=r.Speed>.5f?Vector2.Normalize(r.Velocity):new Vector2(MathF.Cos(r.Heading),MathF.Sin(r.Heading));var left=new Vector2(-course.Y,course.X);var lateral=Vector2.Dot(v.Position-r.Position,left);var lateralVelocity=Vector2.Dot(v.Velocity-r.Velocity,left);
  var headingError=BikeAngles.Wrap(v.Heading-r.Heading)+(lateral/tau+lateralVelocity)/Math.Max(r.Speed,RearWheelParameters.Default.LowSpeedMetersPerSecond);var yawError=v.AngularVelocity-r.AngularVelocity;
  var nominal=rt.LocalAlpha(reference,action,action.SteeringRadians);var actual=rt.LocalAlpha(state,action,action.SteeringRadians);var desired=nominal-2*yawError/tau-headingError/(tau*tau);
  var delta=.5f*MathF.PI/180;var max=TwoContactBikeParameters.Default.MaximumSteeringRadians;var lo=Math.Clamp(action.SteeringRadians-delta,-max,max);var hi=Math.Clamp(action.SteeringRadians+delta,-max,max);var derivative=(rt.LocalAlpha(state,action,hi)-rt.LocalAlpha(state,action,lo))/(hi-lo);
  if(!double.IsFinite(derivative)||Math.Abs(derivative)<1e-8)return action;var request=Math.Clamp(action.SteeringRadians+(desired-actual)/derivative,-max,max);return action with{SteeringRadians=(float)request};
 }
 public static Tape150 Evaluate(Runtime150 rt,State146 state,Tape150 anchor,double goal,double safetyGoal,string origin,Action guard,bool extend=false){var shift=Math.Max(0,(int)Math.Round((state.Input.Time-anchor.CreatedAt)/.2));return Rollout150.Evaluate(rt,state,anchor.Actions.Skip(shift).ToArray(),goal,safetyGoal,origin,generateTail:extend,guard:guard,feedback:(s,_)=>Action(rt,s,anchor));}
}
