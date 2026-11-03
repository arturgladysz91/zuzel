using System.Numerics;
using Autonomous146;
using CoreSim.PhysicalSpace;
using Robust150;
using SharedWorldSpeedway;
using ThreeLap158;
namespace FeasibleSpeed164;
using Continuation161;
public static class Feedback164 {
 public static long Corrections,UnreliableDerivatives;
 public static FreeRacingAction Action(Runtime158 rt,State146 observed,Tape150 ownPlan,Control164 control){
  int k=Math.Clamp((int)Math.Round((observed.Input.Time-ownPlan.CreatedAt)/.2),0,ownPlan.Actions.Length-1);var reference=ownPlan.States[k];var ff=ownPlan.Actions[k];ff.Validate();
  if(Efficient147.ExactKey147.State(observed)==Efficient147.ExactKey147.State(reference))return ff;
  var state=control.ActivationState(observed);var anchor=control.ActivationState(reference);var v=state.Input.Rider;var r=anchor.Input.Rider;
  double tau=2*(RiderController.ReactionSeconds(rt.Spec)+FreeLearningController.ReviewSeconds);var tangent=r.Speed>.5?Vector2.Normalize(r.Velocity):r.Road.Tangent;var left=new Vector2(-tangent.Y,tangent.X);
  double lateral=Vector2.Dot(v.Position-r.Position,left),lateralVelocity=Vector2.Dot(v.Velocity-r.Velocity,left);
  double heading=BikeAngles.Wrap(v.Heading-r.Heading)+(lateral/tau+lateralVelocity)/Math.Max(r.Speed,1);
  var nominal=control.Response(anchor,anchor.Input.Steering,anchor.Input.Active.Drive);var current=control.Response(state,state.Input.Steering,state.Input.Active.Drive);
  double yaw=nominal.YawAcceleration-2*(v.AngularVelocity-r.AngularVelocity)/tau-heading/(tau*tau);
  var max=TwoContactBikeParameters.Default.MaximumSteeringRadians;var sb=Difference161.Bracket(state.Input.Steering,.005f,-max,max);var gb=Difference161.Bracket(state.Input.Active.Drive,.08f,0,1);
  var sl=control.Response(state,sb.Low,state.Input.Active.Drive);var sh=control.Response(state,sb.High,state.Input.Active.Drive);var gl=control.Response(state,state.Input.Steering,gb.Low);var gh=control.Response(state,state.Input.Steering,gb.High);
  double j11=(sh.YawAcceleration-sl.YawAcceleration)/sb.Span,j21=(sh.SlipRate-sl.SlipRate)/sb.Span,j12=(gh.YawAcceleration-gl.YawAcceleration)/gb.Span,j22=(gh.SlipRate-gl.SlipRate)/gb.Span,det=j11*j22-j12*j21;
  if(!double.IsFinite(det)||Math.Abs(det)<1e-8){UnreliableDerivatives++;return ff;}
  double y1=yaw-current.YawAcceleration,y2=nominal.SlipRate-(current.Slip-nominal.Slip)/tau-current.SlipRate;
  double ds=(j22*y1-j12*y2)/det,dg=(j11*y2-j21*y1)/det;
  if(!double.IsFinite(ds)||!double.IsFinite(dg)){UnreliableDerivatives++;return ff;}
  Corrections++;return new((float)Math.Clamp(ff.SteeringRadians+Math.Clamp(ds,-.05,.05),-max,max),(float)Math.Clamp(ff.Drive+Math.Clamp(dg,-.2,.2),0,1));
 }
}
