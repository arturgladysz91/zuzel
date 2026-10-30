using Autonomous146;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
namespace Viable154;
/// <summary>Optimal lower envelope of two endpoint Lipschitz cones.
/// For clearance g(t), g(t)>=max(gA-L*t,gB-L*(dt-t)). Its minimum
/// is bounded by min(gA,gB,(gA+gB-L*dt)/2). Same153 motion L and tube;
/// no interpolation assumption, new dynamics, centre-only distance or smaller radius.</summary>
public static class Sweep154 {
 public static double Interval(FreeRacingPoint a,FreeRacingPoint b){
  var dt=b.Time-a.Time;var ga=Math.Min(a.Inner,a.Outer);var gb=Math.Min(b.Inner,b.Outer);
  if(!double.IsFinite(dt)||dt<=0||!double.IsFinite(ga)||!double.IsFinite(gb)||!float.IsFinite(a.Rider.Speed)||!float.IsFinite(b.Rider.Speed)||!float.IsFinite(a.Rider.AngularVelocity)||!float.IsFinite(b.Rider.AngularVelocity))return double.NegativeInfinity;
  var motion=dt*(Math.Max(a.Rider.Speed,b.Rider.Speed)+SpeedwayBikeDimensions.Reference.BoundingRadiusMeters*Math.Max(Math.Abs(a.Rider.AngularVelocity),Math.Abs(b.Rider.AngularVelocity)));
  return Math.Min(Math.Min(ga,gb),(ga+gb-motion)/2)-Safety153.Clearance153.FloatTubeMeters;
 }
}
