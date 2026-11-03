using System.Numerics;
using Autonomous146;
using Continuation161;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
namespace FeasibleSpeed164;

public sealed record SpeedTarget164(double BaseBendMetersPerSecond,double TargetMetersPerSecond,double NextEntryReferenceMeters,double EntryDistanceMeters,double AvailableDistanceMeters,double BrakingProposalMetersPerSecondSquared,double StabilityFraction,double LateralVelocity,double Chi,double Theta,double Beta,double Yaw,double Inner,double Outer,string Scope);
/// <summary>Proposal only. No bound here replaces original complete physics13/9.
/// Keep161's two-bend force fraction/equilibrium ceiling in every bend. On a
/// straight use a backward stopping-distance envelope before that ceiling.</summary>
public static class SpeedProfile164 {
 public const double MaximumCoastProposalMetersPerSecondSquared=.25;
 public const double CommandDelaySeconds=.24,ReviewSeconds=.2,AdditionalPreparationMeters=2;
 public static double Envelope(double bendSpeed,double availableDistance,double brake,double stability){
  if(!double.IsFinite(bendSpeed)||bendSpeed<=0||!double.IsFinite(availableDistance)||availableDistance<0||!double.IsFinite(brake)||brake<0||!double.IsFinite(stability)||stability is <0 or >1)throw new ArgumentException("Finite physical proposal coordinates required");
  return Math.Sqrt(bendSpeed*bendSpeed+2*brake*availableDistance*stability);
 }
 public static double NextEntry(TrackFrame frame,double reference){
  int lap=(int)Math.Floor(reference/frame.LapMeters);
  return new[]{lap*frame.LapMeters+frame.SegmentStartMeters(1),lap*frame.LapMeters+frame.SegmentStartMeters(5),(lap+1)*frame.LapMeters+frame.SegmentStartMeters(1)}.First(s=>s>reference+.005);
 }
 public static SpeedTarget164 Propose(State146 activation,TrackFrame frame,double bendSpeed,double steeringPreparation){
  var v=activation.Input.Rider;var space=Space161.Observe(activation,frame);double chi=Math.Atan2(v.Velocity.Y,v.Velocity.X);
  double courseError=Math.Abs(BikeAngles.Wrap((float)(chi-v.Road.Heading)));
  double stability=Math.Clamp(1-Math.Abs(v.SlideRadians)/(Math.PI/4)-courseError/.35-Math.Abs(space.LateralVelocity)/2,0,1);
  if(Math.Min(space.Inner,space.Outer)<.75)stability=0;
  double next=NextEntry(frame,v.TotalReferenceMeters),distance=Math.Max(0,next-v.TotalReferenceMeters);
  double delay=CommandDelaySeconds+ReviewSeconds+steeringPreparation;
  double debt=v.Speed*delay+Math.Abs(space.LateralVelocity)*delay+1.05*Math.Abs(v.AngularVelocity)*delay+AdditionalPreparationMeters;
  double available=v.Road.IsTurn?0:Math.Max(0,distance-debt);
  // Half original longitudinal resistance/mass, capped conservatively. Wheel,
  // crank, residual drive and tyre transients can invalidate even this proposal:
  // actual predictors and independent complete certification decide acceptance.
  double resistance=LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(v.Speed)/147d;
  double brake=Math.Min(MaximumCoastProposalMetersPerSecondSquared,.5*resistance);
  return new(bendSpeed,Envelope(bendSpeed,available,brake,stability),next,distance,available,brake,stability,space.LateralVelocity,chi,v.Heading,v.SlideRadians,v.AngularVelocity,space.Inner,space.Outer,"Uncertified speed proposal; original pending activation, tyre/shaft prediction and complete independent13/9 own feasibility. No line or force-fraction change.");
 }
}
