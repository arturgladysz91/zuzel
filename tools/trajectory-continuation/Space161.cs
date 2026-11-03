using System.Numerics;
using Autonomous146;
using SharedWorldSpeedway;
namespace Continuation161;
public sealed record Difference161(double Probe,double Step) {
 public static Difference161 OneSided(double value,double delta,double minimum,double maximum){
  if(!double.IsFinite(value)||!double.IsFinite(delta)||delta<=0||!double.IsFinite(minimum)||!double.IsFinite(maximum)||minimum>=maximum||value<minimum||value>maximum)throw new ArgumentException("Finite admissible derivative coordinate required");
  // Round to the actual command representation before forming the denominator.
  double proposed=value+delta<=maximum?value+delta:value-delta>=minimum?value-delta:value<(minimum+maximum)/2?maximum:minimum;
  float probe=(float)Math.Clamp(proposed,minimum,maximum);double step=probe-value;
  if(Math.Abs(step)<1e-9)throw new ArgumentException("No representable derivative step");return new(probe,step);
 }
 public static (float Low,float High,double Span) Bracket(float value,float delta,float minimum,float maximum){
  if(!float.IsFinite(value)||value<minimum||value>maximum||delta<=0)throw new ArgumentException("Admissible bracket centre required");
  float lo=Math.Clamp(value-delta,minimum,maximum),hi=Math.Clamp(value+delta,minimum,maximum);return(lo,hi,(double)hi-lo);
 }
}
public sealed record SpatialState161(double Reference,double Offset,double LateralVelocity,double Theta,double Chi,double Beta,double Yaw,double Steering,double ActiveGas,double ProjectedOffset,double Inner,double Outer,int PendingCount);
public static class Space161 {
 static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TrackFrame,RacingLineGeometry> geometry=new();
 public static SpatialState161 Observe(State146 state,TrackFrame frame){var v=state.Input.Rider;double vy=Vector2.Dot(v.Velocity,v.Road.Outward);const double FrozenReactionSeconds=.24;double horizon=FrozenReactionSeconds+2*FreeLearningController.ReviewSeconds;var gap=geometry.GetValue(frame,f=>new(f)).Clearance(v);return new(v.TotalReferenceMeters,v.Road.OffsetMeters,vy,v.Heading,Math.Atan2(v.Velocity.Y,v.Velocity.X),v.SlideRadians,v.AngularVelocity,state.Input.Steering,state.Input.Active.Drive,v.Road.OffsetMeters+vy*horizon,gap.Inner,gap.Outer,state.Input.Pending.Length);}
}
