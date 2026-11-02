using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Autonomous146;
using Contingency151;
using CoreSim;
using Efficient152;
using LineThrottle156;
using SharedWorldSpeedway;
using Transfer157;
namespace Dynamic160;
using ThreeLap158;

public static class Native160 {
 public static void Run(string label,int scenario=0,bool validation=false){
  var scenarioRows=Study156.Read<JsonElement>(Path.Combine(Study156.Root,"docs/experiments/robust-three-lap/final-v6/scenarios-frozen.json")).GetProperty("Scenarios");if(scenario<0||scenario>=scenarioRows.GetArrayLength())throw new ArgumentException("Scenario outside frozen set");var condition=scenarioRows[scenario];
  var gate=Enum.Parse<StartingGate>(condition.GetProperty("Gate").GetString()!);var spec=StandingWorld144.Rider(gate) with{OffsetMeters=condition.GetProperty("OffsetMeters").GetSingle(),TargetOffsetMeters=condition.GetProperty("OffsetMeters").GetSingle()};
  if(!validation&&condition.GetProperty("Held").GetBoolean())throw new InvalidOperationException("Unseen condition reserved for frozen validation");
  if(validation&&!File.Exists(Path.Combine(Program160.Doc,"controller-freeze.json")))throw new InvalidOperationException("Freeze after first native S completion before validation");
  if(validation){Freeze160.Check();if(scenario>=Study156.Read<JsonElement>(Path.Combine(Program160.Doc,"controller-freeze.json")).GetProperty("HeldCount").GetInt32())throw new InvalidOperationException("Outside predeclared final subset");}
  Gates160.RequireNative(validation);
  using var budget=new Budget160(label,"native",concurrent:validation);Program160.Snapshot(label);Planner160? next=null;State146? first=null;float? actualMass=null,actualInertia=null;var journal=new List<object>();int accepted=0,abstain=0;double progressAt=0;
  Decision144 Choose(FreeRacingInput input,CrankClutch144 crank,RiderSpec rider,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,string variant){
   first??=new(input,crank.Omega);actualMass??=mass;actualInertia??=inertia;var started=Process.GetCurrentProcess().TotalProcessorTime;
   Decision144 result;string? error=null;
   try{budget.Guard();next??=new(rider,false,new(Enabled:true,MaximumRoots:3),budget.Guard,performance:Hierarchical155.Program155.Options() with{RankPolicies=true},settings:new(Enabled:true,Adaptive:true));result=next.Choose(input,crank);}catch(InvalidOperationException e){error=e.Message;abstain++;result=null!;}
   var review=next?.Last;var counters=next?.Runtime.Counters();if(error is null&&(review is null||!review.ConditionalCertificate||!review.Independent||review.Tree.Length!=13||review.Terminals.Length!=9)){error="STUDY160_INCOMPLETE_FRESH_CERTIFICATE";abstain++;}
   Program160.Save("policies/"+label+"/"+(error is null?"decision-"+accepted.ToString("D4"):"abstain")+".json.gz",new{Observed=new State146(input,crank.Omega),Error=error,Review=review is null?null:Evidence152.Compact(review),Counters=counters,ControlPredictions=next?.Generator.Control.Predictions??0,ControlForceQueries=next?.Generator.Control.ForceQueries??0,Outcomes=next?.Generator.Outcomes,Rejections=next?.Generator.Rejections});
   var cpu=(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds;
   journal.Add(new{Time=input.Time,Reference=input.Rider.TotalReferenceMeters,Accepted=error is null,Reason=error??review!.Reason,Action=error is null?result.Choice.Action:(FreeRacingAction?)null,PlannerCpuSeconds=review?.CpuSeconds,CallbackCpuSeconds=cpu,Nodes=review?.Tree.Length??0,FullReserves=review?.Terminals.Length??0,Goal=review?.Goal,SafetyGoal=review?.SafetyGoal});
   if(error is not null)throw new InvalidOperationException(error);accepted++;
   if(input.Time>=progressAt){Console.WriteLine($"{label}: t={input.Time:F1} ref={input.Rider.TotalReferenceMeters:F1} v={input.Rider.Speed*3.6:F1} decisions={accepted} CPU={budget.Elapsed:F1}");progressAt=input.Time+5;}
   return result with{CpuSeconds=cpu};
  }
  Run144 run;using(var world=new NativeWorld156(new(Enabled:true,RequiredLaps:3,MaximumSeconds:180,SoloGate:gate),diagnosticRider:spec,safetyHalfMeter:true,safetyFrom:0))run=world.Run(label,"K1",Choose);
  Program160.Save("native/"+label+".json.gz",run);Program160.Save("journals/"+label+".json.gz",journal);var samples=run.Riders[0].Samples;var full=run.Riders[0].CompletedLaps==3&&run.Riders[0].FinishSeconds is not null;
  var pose=samples.Min(s=>Math.Min(s.Inner,s.Outer));var sweep=MinimumSweep(samples.Skip(1).Select(s=>s.Certificate));var beta=samples.Max(s=>Math.Abs(s.Input.Rider.SlideRadians))*180/Math.PI;
  var stoppedStart=first is not null&&first.Input.Time==0&&first.Input.Rider.Speed==0&&first.Input.Pending.Length==0&&first.Input.Active==new FreeRacingAction(0,0);var legal=stoppedStart&&full&&run.Outcome=="completed"&&run.Contacts.Length==0&&pose>=.5&&sweep is >=.5&&beta<=45&&abstain==0;
  Program160.Save("native/"+label+"-result.json",new{Label=label,Baseline=false,Validation=validation,Scenario=condition,Spec=spec,Initial=first,ExpectedInitial=Program158.Initial(spec),ActualMassKg=actualMass,ActualInertiaKgMetersSquared=actualInertia,StoppedStart=stoppedStart,RequiredLaps=3,CompletedLaps=run.Riders[0].CompletedLaps,FinishSeconds=run.Riders[0].FinishSeconds,Pass=legal,Complete=full,MinimumPoseMeters=pose,MinimumSweepMeters=sweep,MaximumBetaDegrees=beta,Contacts=run.Contacts.Length,AcceptedDecisions=accepted,Abstain=abstain,run.Outcome,run.Fingerprint,run.TotalCpuSeconds,run.ControllerCpuSeconds,ChargedCpuSeconds=budget.Elapsed,Scope="ONE continuous original Box2D world from stopped start, three-lap target, autonomous decisions only, S from0; DNF never promoted"});
  Console.WriteLine($"{label}: PASS={legal} laps={run.Riders[0].CompletedLaps} time={run.EndSeconds:F6} {run.Outcome} CPU={budget.Elapsed:F1}");budget.Finish(run.Outcome);
 }
 public static double? MinimumSweep(IEnumerable<double?> intervals){var values=intervals.ToArray();if(values.Length==0||values.Any(x=>x is null))return null;return values.Min();}
}
