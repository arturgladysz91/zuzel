using System.Diagnostics;
using Autonomous146;
using CoreSim;
using Efficient147;
using SharedWorldSpeedway;
using Speed148;
namespace Limits149;
/// <summary>Two bounded capability probes. Same search as full A/B; no speed target or authored line.</summary>
public static class Local149{
 public static void Run(){foreach(var separated in new[]{false,true}){
  var name="planned-entry-90-outer-"+(separated?"B":"A");using var b=new Budget149(name,1,80);using var journal=new Journal147(Program149.Doc+"/fixtures/"+name+"-decisions.jsonl");var process=Process.GetCurrentProcess();ExactRuntime148? a=null;ExactRuntime149? rt=null;Planner146? planner=null;var options=new RuntimeOptions147(EventSearch:true,WorkBudget:true,ProposalsPerThrottle:2,MaximumActualForecasts:8192,MaximumContacts:700000,OptionalSearchContacts:500000);
  Decision144 Choose(FreeRacingInput i,CrankClutch144 c,RiderSpec s,TrackFrame f,TrackStateSnapshot surface,WorldParameters p,float m,float inertia,string variant){
   b.Guard();var start=process.TotalProcessorTime;ExecutionHooks146 hooks;
   if(separated){rt??=new(s,f,surface,p,options,m,inertia);hooks=rt.Hooks();}else{a??=new(s,f,surface,p,options,m,inertia);hooks=a.Hooks();}
   planner??=new(s,f,surface,p,new(Enabled:true,RequiredLaps:1),m,inertia,b.Guard,hooks);
   try{var d=planner.Choose(i,c);var review=planner.Reviews[^1];var plan=review.Selected!;journal.Append(new{Event="decision",i.Time,Observed=i,CrankOmega=c.Omega,Selected=d.Choice.Action,plan.Actions,plan.GoalTime,plan.Safety,Independent=review.IndependentSafety,review.Reason,review.PredictionError,review.FollowedExpected});if(separated)rt!.Completed(i,plan);else a!.Completed(i,plan);planner.Reviews.Clear();return new(d.Choice,(process.TotalProcessorTime-start).TotalSeconds);}
   catch(InvalidOperationException error){journal.Append(new{Event="abstain",i.Time,Reason=error.Message,Progress=planner.LastProgress});throw;}
  }
  var frame=StandingWorld144.Frame();var spec=StandingWorld144.Rider(CoreSim.StartingGate.B) with{SpeedMetersPerSecond=90/3.6f,ReferenceMeters=frame.SegmentStartMeters(1)-12,OffsetMeters=10};Run144 run;
  using(var w=new DiagnosticWorld149(new(Enabled:true,RequiredLaps:1,MaximumSeconds:14),diagnosticRider:spec,separated:separated))run=w.Run(name,"K1",Choose);
  journal.Append(new{Event="summary",run.Outcome,run.Fingerprint});Program149.Save("fixtures/"+name+".json.gz",run);var ss=run.Riders[0].Samples;
  Program149.Save("fixtures/"+name+"-result.json",new{Model=separated?"B":"A",Control="Unchanged exact148 sequence planner; one-lap geometric finish; no forced speed or line",run.Outcome,run.EndSeconds,run.Fingerprint,run.ControllerCpuSeconds,run.NativeAndObservationCpuSeconds,MaximumBetaDegrees=ss.Max(s=>Math.Abs(s.Input.Rider.SlideRadians)*180/Math.PI),MinimumGap=ss.Min(s=>Math.Min(s.Inner,s.Outer)),MinimumSweep=ss.Where(s=>s.Certificate is not null).Min(s=>s.Certificate),Contacts=run.Contacts.Length,ExtendedBetaObservation=false,CpuSeconds=b.Elapsed,Counts=separated?rt?.Counters():a?.Counters()});b.Finish(run.Outcome);Console.WriteLine(name+": "+run.Outcome+" cpu="+b.Elapsed);Console.Out.Flush();
 }}
}
