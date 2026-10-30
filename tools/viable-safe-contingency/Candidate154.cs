using System.Numerics;
using Autonomous146;
using Safety153;
using SharedWorldSpeedway;
namespace Viable154;
/// <summary>Rank original physical H09 candidates by corridor capture and future
/// drift, not racing time. Only complete13/9 plus independent replay can accept.</summary>
public static class Candidate154 {
 public const double TargetSpeedMetersPerSecond=15,DriftPreviewSeconds=1.2;
 public static Decision144 Choose(Runtime154 rt,State146 state){
  var began=System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
  var old=rt.OriginalSeed(state);var candidates=new List<(FreeLearningCandidate Candidate,double Cost,bool Legal,double Sweep)>();
  foreach(var item in old.Choice.Candidates){
   var prediction=rt.CandidateForecast(state,item.Action);rt.ViableCandidateForecasts++;
   var safety=Clearance153.Check(prediction.Points,Regime153.SafetyHalfMeter);
   if(safety.MinimumGapMeters<.5)rt.LocalPoseRejected++;else if(!safety.Legal)rt.LocalNonPoseRejected++;else rt.LocalLegalCandidates++;
   var v=prediction.Points[^1].Rider;var centre=(rt.Frame.Track.Geometry.StraightWidthMeters-2)/2;
   var outward=Vector2.Dot(v.Velocity,v.Road.Outward);var drift=v.Road.OffsetMeters-centre+DriftPreviewSeconds*outward;
   var ahead=rt.Frame.RoadAt(v.TotalReferenceMeters+Math.Max(5,v.Speed*.8),v.Road.OffsetMeters);
   var targetYaw=ahead.IsTurn&&double.IsFinite(ahead.RadiusMeters)?v.Speed/ahead.RadiusMeters:0;
   // Cheap future-drift/approach ranking is not an exclusion or proof. A later
   // full rollout must reach the following safety goal under exact dynamics.
   var referenceRate=rt.Frame.ReferenceSpeed(v);
   var cost=drift*drift+.5*outward*outward+.4*Math.Pow(v.AngularVelocity-targetYaw,2)+.25*Math.Pow(Math.Max(0,v.Speed-22),2)+.7*Math.Pow(Math.Max(0,rt.RequiredReferenceSpeed-referenceRate),2)+.00001*Math.Pow(item.Action.SteeringRadians-state.Input.Active.SteeringRadians,2);
   candidates.Add((item,cost,safety.Legal,safety.SweepLowerBoundMeters));
  }
  var chosen=candidates.OrderBy(x=>x.Legal?0:1).ThenByDescending(x=>x.Legal?0:x.Sweep).ThenBy(x=>x.Cost).ThenBy(x=>Math.Abs(x.Candidate.Action.SteeringRadians-state.Input.Active.SteeringRadians)).ThenBy(x=>x.Candidate.Action.SteeringRadians).First();
  return new(old.Choice with{Action=chosen.Candidate.Action},(System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime-began).TotalSeconds);
 }
}
