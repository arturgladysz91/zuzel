using System.Numerics;
using Contingency151;
using Robust150;
using SharedWorldSpeedway;
namespace Safety153;
/// <summary>No lateral-line reward or peak-speed reward. All inputs are causal
/// forecasts from the current observation; every ranked policy already has nine
/// complete reserves and an independent certificate.</summary>
public sealed record PolicyRank153(string Origin,double CommonGoalMeters,double ArrivalSeconds,double WorstReserveArrivalSeconds,
 double TerminalAlongMps,double PathMeters,double ClosedGasSeconds,double MinimumReserveSweepMeters):IComparable<PolicyRank153>{
 public static PolicyRank153 From(Tape150 t,IEnumerable<Terminal151> reserves,TrackFrame frame){
  var leaves=reserves.ToArray();var end=t.End.Input.Rider;
  return new(t.Origin,t.Goal,t.Arrival!.Value-t.CreatedAt,leaves.Max(l=>l.Tape.Arrival!.Value-t.CreatedAt),Vector2.Dot(end.Velocity,end.Road.Tangent),end.DistanceMeters-t.States[0].Input.Rider.DistanceMeters,t.Actions.Count(a=>a.Drive==0)*.2,leaves.Min(l=>l.Tape.Safety.SweepLowerBoundMeters));
 }
 public int CompareTo(PolicyRank153? b){
  if(b is null)return -1;if(CommonGoalMeters!=b.CommonGoalMeters)throw new InvalidOperationException("Identical geometric goals required");
  foreach(var c in new[]{WorstReserveArrivalSeconds.CompareTo(b.WorstReserveArrivalSeconds),ArrivalSeconds.CompareTo(b.ArrivalSeconds),b.TerminalAlongMps.CompareTo(TerminalAlongMps),PathMeters.CompareTo(b.PathMeters),ClosedGasSeconds.CompareTo(b.ClosedGasSeconds),b.MinimumReserveSweepMeters.CompareTo(MinimumReserveSweepMeters)})if(c!=0)return c;
  return 0;
 }
}
