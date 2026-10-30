using Autonomous146;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
namespace Safety153;
public enum Regime153 { Legacy, SafetyHalfMeter }
/// <summary>Metres from the nearest point of either physical capsule to either
/// continuous track edge. Existing chord/numerical allowances are already subtracted.
/// A pose reserve alone does not certify the swept interval.</summary>
public static class Clearance153 {
 public const double RequiredMeters=.5, FloatTubeMeters=.0005;
 public static double SweepLowerBound(RiderView a,RiderView b,double dt,(double Inner,double Outer) before,(double Inner,double Outer) after){
  if(!double.IsFinite(dt)||dt<=0)throw new ArgumentOutOfRangeException(nameof(dt));
  var travel=dt*.5*(Math.Max(a.Speed,b.Speed)+SpeedwayBikeDimensions.Reference.BoundingRadiusMeters*Math.Max(Math.Abs(a.AngularVelocity),Math.Abs(b.AngularVelocity)));
  return Math.Min(Math.Min(before.Inner,before.Outer),Math.Min(after.Inner,after.Outer))-travel-FloatTubeMeters;
 }
 public static Safety146 Check(FreeRacingPoint[] points,Regime153 regime){
  if(regime==Regime153.Legacy)return Planner146.Check(points,.1);
  if(regime!=Regime153.SafetyHalfMeter)throw new ArgumentOutOfRangeException(nameof(regime));
  if(points.Length==0||points.Any(p=>!double.IsFinite(p.Time)||!double.IsFinite(p.Inner)||!double.IsFinite(p.Outer)||!float.IsFinite(p.Rider.Heading)||!float.IsFinite(p.Rider.AngularVelocity)||!float.IsFinite(p.Rider.Position.X)||!float.IsFinite(p.Rider.Position.Y)||!float.IsFinite(p.Rider.SlideRadians)))
   return new(false,double.NegativeInfinity,double.NegativeInfinity,0,0,"missing or nonfinite physical clearance");
  for(int j=1;j<points.Length;j++)if(points[j].Time<=points[j-1].Time)return new(false,0,double.NegativeInfinity,0,0,"nonpositive swept interval");
  var s=Planner146.Check(points,RequiredMeters);
  if(points.Length==1)s=s with{SweepLowerBoundMeters=s.MinimumGapMeters};
  return s with{Legal=s.Legal&&s.SweepLowerBoundMeters>=RequiredMeters,Reason=!s.Legal?s.Reason:s.SweepLowerBoundMeters<RequiredMeters?"swept physical footprint reserve0.5m":"legal complete forecast; swept physical footprint>=0.5m"};
 }
}
