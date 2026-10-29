using System.Security.Cryptography;
using Autonomous146;
using Efficient147;
using Contingency151;
using Robust150;
using SharedWorldSpeedway;
namespace Efficient152;
public static class Bits152 {
 static long D(double x)=>BitConverter.DoubleToInt64Bits(x);static long F(float x)=>BitConverter.SingleToInt32Bits(x);
 public static bool Wheel(RearWheelStep a,RearWheelStep b) {
  ReadOnlySpan<long> x=stackalloc long[]{D(a.Seconds),D(a.BeforeOmega),D(a.AfterOmega),D(a.RequestedTorqueNewtonMeters),D(a.AvailableTorqueNewtonMeters),D(a.DriveTorqueNewtonMeters),D(a.BearingTorqueNewtonMeters),F(a.LongitudinalNewtons),F(a.FrontLateralNewtons),F(a.RearLateralNewtons),D(a.InstantSlipRatio),D(a.MidSlipVelocityMetersPerSecond),D(a.LongitudinalLossJoules),D(a.BearingLossJoules),D(a.EngineWorkJoules),D(a.RearUtilization),D(a.RootResidualMetersPerSecond),a.RootIterations};
  ReadOnlySpan<long> y=stackalloc long[]{D(b.Seconds),D(b.BeforeOmega),D(b.AfterOmega),D(b.RequestedTorqueNewtonMeters),D(b.AvailableTorqueNewtonMeters),D(b.DriveTorqueNewtonMeters),D(b.BearingTorqueNewtonMeters),F(b.LongitudinalNewtons),F(b.FrontLateralNewtons),F(b.RearLateralNewtons),D(b.InstantSlipRatio),D(b.MidSlipVelocityMetersPerSecond),D(b.LongitudinalLossJoules),D(b.BearingLossJoules),D(b.EngineWorkJoules),D(b.RearUtilization),D(b.RootResidualMetersPerSecond),b.RootIterations};return x.SequenceEqual(y);
 }
 public static void Rider(BinaryWriter w,RiderView v) {
  var r=v.Road;w.Write(v.Id);w.Write(v.Position.X);w.Write(v.Position.Y);w.Write(v.Velocity.X);w.Write(v.Velocity.Y);w.Write(v.Heading);w.Write(v.AngularVelocity);w.Write(r.Segment);w.Write(r.Progress);w.Write(r.ReferenceMeters);w.Write(r.OffsetMeters);w.Write(r.Heading);w.Write(r.RadiusMeters);w.Write(r.CornerProgress);w.Write(v.TotalReferenceMeters);w.Write(v.DistanceMeters);w.Write((int)v.Activity);w.Write(v.Drive);w.Write(v.TargetOffsetMeters);w.Write(v.Impairment);w.Write((int)v.Context);
 }
 public static void Point(BinaryWriter w,FreeRacingPoint p){w.Write(p.Time);Rider(w,p.Rider);w.Write(p.Omega);w.Write(p.Steering);w.Write(p.Inner);w.Write(p.Outer);}
 public static bool Transition(Transition146 a,Transition146 b) {
  if(ExactKey147.State(a.End)!=ExactKey147.State(b.End)||a.Prediction.Points.Length!=b.Prediction.Points.Length||D(a.Prediction.DistanceMeters)!=D(b.Prediction.DistanceMeters))return false;
  for(int k=0;k<a.Prediction.Points.Length;k++){
   var x=a.Prediction.Points[k];var y=b.Prediction.Points[k];
   // Typed equality checks every rider field; explicit bit checks retain signed zero on the floating outputs.
   if(x.Rider!=y.Rider||D(x.Time)!=D(y.Time)||D(x.Omega)!=D(y.Omega)||F(x.Steering)!=F(y.Steering)||D(x.Inner)!=D(y.Inner)||D(x.Outer)!=D(y.Outer))return false;
  }return true;
 }
 static void Safety(BinaryWriter w,Safety146 s){w.Write(s.Legal);w.Write(s.MinimumGapMeters);w.Write(s.SweepLowerBoundMeters);w.Write(s.MaximumBetaDegrees);w.Write(s.MaximumYawRate);w.Write(s.Reason);}
 static void Tape(BinaryWriter w,Tape150 t){w.Write(t.CreatedAt);w.Write(t.Goal);w.Write(t.SafetyGoal);w.Write(t.Arrival??double.NaN);w.Write(t.Actions.Length);foreach(var a in t.Actions){w.Write(a.SteeringRadians);w.Write(a.Drive);}w.Write(t.States.Length);foreach(var s in t.States)w.Write(ExactKey147.State(s));w.Write(t.Points.Length);foreach(var p in t.Points)Point(w,p);Safety(w,t.Safety);}
 public static string Review(Review151 r){using var m=new MemoryStream();using var w=new BinaryWriter(m);w.Write(r.Time);w.Write(r.Goal);w.Write(r.SafetyGoal);w.Write(r.ConditionalCertificate);w.Write(r.Independent);w.Write(ExactKey147.State(r.Observed));if(r.Nominal is {} nominal)Tape(w,nominal);foreach(var n in r.Tree){w.Write(n.Id);w.Write(n.Parent??-1);w.Write(n.Sign);w.Write(n.Depth);w.Write(ExactKey147.State(n.Observed));w.Write(n.Action.SteeringRadians);w.Write(n.Action.Drive);Safety(w,n.Execution);w.Write(n.TerminalPlan??-1);}foreach(var t in r.Terminals){w.Write(t.Id);w.Write(t.Node);Tape(w,t.Tape);}return Convert.ToHexString(SHA256.HashData(m.GetBuffer().AsSpan(0,(int)m.Length)));}
}
