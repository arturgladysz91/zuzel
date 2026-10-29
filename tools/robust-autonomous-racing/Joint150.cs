using Autonomous146;
using SharedWorldSpeedway;
namespace Robust150;
public sealed record Response150(double YawAcceleration,double SlipRate,double Slip);
/// <summary>Bounded 2x2 inverse of unchanged local yaw/slip response. Only causal queued commands are returned.</summary>
public static class Joint150{
 public static FreeRacingAction Correct(Runtime150 rt,State146 state,State146 reference,FreeRacingAction ff,double desiredYaw,double tau){
  var d=state.Input.Active.Drive;var s=state.Input.Steering;var nominal=rt.Response(reference,reference.Input.Active.Drive,reference.Input.Steering);var actual=rt.Response(state,d,s);var delta=.5f*MathF.PI/180;var max=TwoContactBikeParameters.Default.MaximumSteeringRadians;var lo=Math.Clamp(s-delta,-max,max);var hi=Math.Clamp(s+delta,-max,max);var a=rt.Response(state,d,lo);var b=rt.Response(state,d,hi);var dlo=d/2;var dhi=(1+d)/2;var c=rt.Response(state,dlo,s);var e=rt.Response(state,dhi,s);
  var j11=(b.YawAcceleration-a.YawAcceleration)/(hi-lo);var j21=(b.SlipRate-a.SlipRate)/(hi-lo);var j12=(e.YawAcceleration-c.YawAcceleration)/(dhi-dlo);var j22=(e.SlipRate-c.SlipRate)/(dhi-dlo);var det=j11*j22-j12*j21;
  if(!double.IsFinite(det)||Math.Abs(det)<1e-8)return ff;var y1=desiredYaw-actual.YawAcceleration;var y2=nominal.SlipRate-(actual.Slip-nominal.Slip)/tau-actual.SlipRate;var dd=(j11*y2-j21*y1)/det;var drive=Math.Clamp(ff.Drive+dd,0,1);var ds=Math.Abs(j11)>1e-8?(y1-j12*(drive-ff.Drive))/j11:0;return new((float)Math.Clamp(ff.SteeringRadians+ds,-max,max),(float)drive);
 }
}
