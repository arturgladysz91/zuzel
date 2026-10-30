using Contingency151;
using Autonomous146;
using Robust150;
namespace Safety153;
public static class Failure153 {
 public static double Time(Tape150 tape,Regime153 regime){
  if(regime==Regime153.Legacy)return tape.Points.FirstOrDefault(p=>!Planner146.Check(new[]{p},.1).Legal)?.Time??tape.End.Input.Time;
  for(int j=0;j<tape.Points.Length;j++)if(!Clearance153.Check(j==0?new[]{tape.Points[j]}:new[]{tape.Points[j-1],tape.Points[j]},regime).Legal)return tape.Points[j].Time;
  return tape.End.Input.Time;
 }
 public static object? First(Tape150 tape,Regime153 regime){
  if(regime==Regime153.Legacy)return Audit151.FirstFailure(tape);
  for(int j=0;j<tape.Points.Length;j++){
   var p=tape.Points[j];var s=Clearance153.Check(j==0?new[]{p}:new[]{tape.Points[j-1],p},regime);
   if(!s.Legal)return new{Time=p.Time,Reference=p.Rider.TotalReferenceMeters,p.Inner,p.Outer,s.SweepLowerBoundMeters,s.MaximumBetaDegrees,s.Reason};
  }
  return null;
 }
}
