using Autonomous146;
using Efficient152;
using SharedWorldSpeedway;
namespace Safety153;
public static class GeometryAudit153 {
 public static void Run(){
  using var budget=new Budget153("frozen-native-geometry-audit-v1",0,35);var frame=DiagnosticWorld149.Frame();var boundary=new RacingLineGeometry(frame);var pointBoundary=new CurveBoundaryAudit(frame);var rows=new List<object>();bool equal=true;var memo=new ForecastMemo152(frame,FreeLearningExperiment.Parameters());var independent=new IndependentBoundary152(frame,memo);
  foreach(var name in new[]{"baseline149-final148","efficient152-A-portfolio-v1"}){
   var path=name.StartsWith("baseline",StringComparison.Ordinal)?"docs/experiments/tyre-physics-corner-limits/native/"+name+".json.gz":Program152.Doc+"/native/"+name+".json.gz";
   var run=Program153.Read<Run144>(path);var rider=run.Riders[0];int poses=0,bad=0,definite=0,intervals=0,unsafeIntervals=0;double minCenter=double.PositiveInfinity;var all=new List<FreeRacingPoint>();var samples=new List<object>();
   foreach(var s in rider.Samples){budget.Guard();var v=s.Input.Rider;var gap=boundary.Clearance(v);var check=independent.Clearance(v);var bits=gap.Inner==s.Inner&&gap.Outer==s.Outer&&check==gap;equal&=bits;poses++;var minimum=Math.Min(gap.Inner,gap.Outer);if(minimum<.5)bad++;if(minimum+.001425<.5)definite++;
    var center=Math.Min(pointBoundary.InnerPointDistance(new(v.Position.X,v.Position.Y)),pointBoundary.OuterPointDistance(new(v.Position.X,v.Position.Y)));minCenter=Math.Min(minCenter,center);
    var p=new FreeRacingPoint(s.Input.Time,v,s.Input.Omega,s.Input.Steering,gap.Inner,gap.Outer);all.Add(p);double? sweep=null;if(all.Count>1){intervals++;sweep=Clearance153.Check(new[]{all[^2],p},Regime153.SafetyHalfMeter).SweepLowerBoundMeters;if(sweep<.5)unsafeIntervals++;}
    samples.Add(new{Time=s.Input.Time,Reference=v.TotalReferenceMeters,Inner=gap.Inner,Outer=gap.Outer,Center=center,SweepLowerBound=sweep,MeetsPoseHalfMeter=minimum>=.5,MeetsIntervalHalfMeter=sweep is null?minimum>=.5:sweep>=.5,OriginalGapBitsEqual=bits});
   }
   int legalDecisions=0;var decisionRows=new List<object>();foreach(var d in rider.Decisions){var t=d.Choice.Time;var ps=all.Where(p=>p.Time>=t-1e-8&&p.Time<=t+.2+1e-8).ToArray();var full=ps.Length>1&&ps[0].Time<=t+1e-8&&ps[^1].Time>=Math.Min(t+.2,run.EndSeconds)-1e-8;var safety=Clearance153.Check(ps,Regime153.SafetyHalfMeter);if(full&&safety.Legal)legalDecisions++;decisionRows.Add(new{Time=t,FullExecutedInterval=full,MeetsHalfMeter=full&&safety.Legal,Safety=safety});}
   Program153.Save("history/"+name+"-geometry.json.gz",new{Run=name,Source=path,Samples=samples,Decisions=decisionRows});rows.Add(new{Run=name,Poses=poses,PoseViolations=bad,DefinitelyPhysicalPoseViolations=definite,Intervals=intervals,UncertifiedHalfMeterIntervals=unsafeIntervals,Decisions=rider.Decisions.Length,LegalExecutedDecisions=legalDecisions,MinimumGap=all.Min(p=>Math.Min(p.Inner,p.Outer)),MinimumCenterGap=minCenter,HistoricalThreeLaps=rider.CompletedLaps,LegalThreeLapsUnderS=bad==0&&unsafeIntervals==0&&rider.CompletedLaps==3});
   Console.WriteLine($"geometry153 {name} poses={poses} gap<.5={bad} definitely={definite} intervals={unsafeIntervals}/{intervals} decisionsOK={legalDecisions}/{rider.Decisions.Length}");
  }
  // Static geometric room is distinct from dynamic feasibility. Inspect the full
  // continuous loop and yawed footprint, including every segment handoff.
  var prototype=Program153.Read<Run144>(Program152.Doc+"/native/efficient152-A-portfolio-v1.json.gz").Riders[0].Samples[0].Input.Rider;double minimumWidth=double.PositiveInfinity,minimumMiddleGap=double.PositiveInfinity;int staticPoses=0;
  for(double s=0;s<frame.LapMeters;s+=.5){budget.Guard();var road=frame.Project(frame.At(s,0).Position);var width=frame.PhysicalWidthMeters(road);minimumWidth=Math.Min(minimumWidth,width);var p=frame.At(s,width/2-1);foreach(var yaw in new[]{-.7853981634,0,.7853981634}){var v=prototype with{Position=p.Position,Road=frame.Project(p.Position),Heading=(float)(p.Road.Heading+yaw)};var g=boundary.Clearance(v);minimumMiddleGap=Math.Min(minimumMiddleGap,Math.Min(g.Inner,g.Outer));staticPoses++;}}
  Program153.Save("geometry-audit.json",new{Passed=equal,OriginalAndIndependentGapBitsEqual=equal,Runs=rows,MinimumPhysicalTrackWidth=minimumWidth,MinimumMiddleCorridorGapAtSampledYaw=minimumMiddleGap,StaticPoses=staticPoses,StaticScope="0.5m reference sampling and yaw0,+/-45deg. Static room only; no proof of dynamic or all-continuous-pose feasibility.",Allowances=new{ChordMeters=.0007,NumericalMeters=RacingLineGeometry.NumericalAllowanceMeters,FloatTubeMeters=Clearance153.FloatTubeMeters,DefiniteViolationUpperErrorMeters=.001425},Semantics="Distances are full chassis+handlebar capsule clearance, not CoM offset. Chord and numerical allowances are already subtracted. S requires swept lower bound>=0.5; historical certificates remain unchanged."});budget.Finish(equal?"frozen native margins independently reproduced":"geometry mismatch");
 }
}
