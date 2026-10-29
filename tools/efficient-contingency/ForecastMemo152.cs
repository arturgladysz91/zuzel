using System.Numerics;
using SharedWorldSpeedway;
namespace Efficient152;
/// <summary>Pure frozen-contact and geometry DAG. Each seed or independent preflight owns a separate lifetime.</summary>
public sealed class ForecastMemo152 {
 readonly TrackFrame frame;readonly TwoContactBikeParameters geometry;
 readonly Dictionary<KernelKey,CachedForce> forces=new();readonly Dictionary<(int,int),RoadPoint> roads=new();readonly Dictionary<PoseKey,(double,double)> gaps=new();
 public bool Enabled {get;private set;}public long KernelHits,KernelMisses,ProjectionHits,ProjectionMisses,ClearanceHits;
 public ForecastMemo152(TrackFrame frame,WorldParameters parameters){this.frame=frame;geometry=parameters.TwoContactBike??TwoContactBikeParameters.Default;}
 static long D(double x)=>BitConverter.DoubleToInt64Bits(x);static long F(float x)=>BitConverter.SingleToInt32Bits(x);
 public readonly record struct KernelKey(long Id,long X,long Y,long VX,long VY,long H,long W,long Segment,long Progress,long Ref,long Offset,long RoadH,long Radius,long Corner,long Total,long Distance,long Activity,long Drive,long Target,long Impairment,long Context,long Omega,long Crank,long Steering,long After,long Time,long Dt);
 readonly record struct PoseKey(long X,long Y,long H,long Segment,long Progress,long Ref,long Offset,long RoadH,long Radius,long Corner);
 public sealed record CachedForce(TwoContactForces Force,RearWheelStep Wheel,float Steering,ClutchStep144? Crank);
 public KernelKey Key(RiderView v,double omega,double crank,float steering,float request,double time,float h){var r=v.Road;var bounded=Math.Clamp(request,-geometry.MaximumSteeringRadians,geometry.MaximumSteeringRadians);var after=steering+Math.Clamp(bounded-steering,-geometry.SteeringRateRadiansPerSecond*h,geometry.SteeringRateRadiansPerSecond*h);
  return new(v.Id,F(v.Position.X),F(v.Position.Y),F(v.Velocity.X),F(v.Velocity.Y),F(v.Heading),F(v.AngularVelocity),r.Segment,D(r.Progress),D(r.ReferenceMeters),D(r.OffsetMeters),D(r.Heading),D(r.RadiusMeters),D(r.CornerProgress),D(v.TotalReferenceMeters),D(v.DistanceMeters),(int)v.Activity,F(v.Drive),F(v.TargetOffsetMeters),F(v.Impairment),(int)v.Context,D(omega),D(crank),F(steering),F(after),D(time),F(h));
 }
 public bool Try(KernelKey key,out CachedForce hit){if(Enabled&&forces.TryGetValue(key,out hit!)){KernelHits++;return true;}KernelMisses++;hit=null!;return false;}
 public void Store(KernelKey key,TwoContactForces force,RearWheelState wheel,TwoContactBikeState steering,FastCrank152? crank){if(Enabled)forces[key]=new(force,wheel.Prepared,steering.PreparedSteeringRadians,crank?.Prepared);}
 public RoadPoint Project(Vector2 position){var key=(BitConverter.SingleToInt32Bits(position.X),BitConverter.SingleToInt32Bits(position.Y));if(Enabled&&roads.TryGetValue(key,out var road)){ProjectionHits++;return road;}ProjectionMisses++;road=frame.Project(position);if(Enabled)roads[key]=road;return road;}
 public (double Inner,double Outer) Clearance(RiderView v,Func<RiderView,(double Inner,double Outer)> calculate){var r=v.Road;var key=new PoseKey(F(v.Position.X),F(v.Position.Y),F(v.Heading),r.Segment,D(r.Progress),D(r.ReferenceMeters),D(r.OffsetMeters),D(r.Heading),D(r.RadiusMeters),D(r.CornerProgress));if(Enabled&&gaps.TryGetValue(key,out var hit)){ClearanceHits++;return hit;}var value=calculate(v);if(Enabled)gaps[key]=value;return value;}
 sealed class Scope(Action close):IDisposable{public void Dispose()=>close();}
 public IDisposable Lease(){if(Enabled)throw new InvalidOperationException("Forecast DAG lifetime overlaps");forces.Clear();roads.Clear();gaps.Clear();Enabled=true;return new Scope(()=>{Enabled=false;forces.Clear();roads.Clear();gaps.Clear();});}
 public object Counters()=>new{KernelHits,KernelMisses,ProjectionHits,ProjectionMisses,ClearanceHits};
}
