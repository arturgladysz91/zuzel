using System.Diagnostics;
using System.Text.Json;
using Autonomous146;
using Continuation161;
using CoreSim;
using Efficient147;
using Efficient152;
using LineThrottle156;
using S3Performance162;
using SharedWorldSpeedway;
using ThreeLap158;
namespace FeasibleSpeed164;
public static class Native164 {
 public static void Require(bool validation){
  Program164.RequireDiagnostics();foreach(var name in new[]{"c1-rest","c2-straight"}){var c=Study156.Read<JsonElement>(Path.Combine(Program164.Doc,"certificates/"+name+"-result.json"));if(!c.GetProperty("GatePassed").GetBoolean()||c.GetProperty("PolicyHash").GetString()!=Program164.PolicyHash())throw new InvalidOperationException("Both current-policy full13/9 independent certificates required");}
  if(File.Exists(Path.Combine(Program164.Doc,"series-closure.json")))throw new InvalidOperationException("First DNF closes all further native starts");
  var results=Directory.Exists(Path.Combine(Program164.Doc,"native"))?Directory.GetFiles(Path.Combine(Program164.Doc,"native"),"*-result.json"):Array.Empty<string>();
  if(!validation&&results.Length>0)throw new InvalidOperationException("Only one development native world");
  if(validation){var dev=Study156.Read<JsonElement>(Path.Combine(Program164.Doc,"native/native-dev-result.json"));if(!dev.GetProperty("Pass").GetBoolean())throw new InvalidOperationException("Successful full native development required");CheckFreeze();}
 }
 public static void Freeze(){var dev=Study156.Read<JsonElement>(Path.Combine(Program164.Doc,"native/native-dev-result.json"));if(!dev.GetProperty("Pass").GetBoolean()||!dev.GetProperty("FasterThan161").GetBoolean())throw new InvalidOperationException("No freeze without legal faster development S3");if(File.Exists(Path.Combine(Program164.Doc,"controller-freeze.json")))throw new IOException("Never overwrite freeze");Program164.Save("controller-freeze.json",new{PolicyHash=Program164.PolicyHash(),Sources=Program164.Sources(),HeldScenarios=new[]{0,1,2,3,4},BeforeHeldResults=true});}
 public static void CheckFreeze(){var f=Study156.Read<JsonElement>(Path.Combine(Program164.Doc,"controller-freeze.json"));var sources=Program164.Sources();foreach(var p in f.GetProperty("Sources").EnumerateObject())if(sources.GetValueOrDefault(p.Name)!=p.Value.GetString())throw new InvalidOperationException("Frozen source changed: "+p.Name);if(sources.Count!=f.GetProperty("Sources").EnumerateObject().Count())throw new InvalidOperationException("Frozen source set changed");}
 public static void Run(string label,int scenario=0,bool validation=false){
  if(scenario<0||scenario>=5||!validation&&(scenario!=0||label!="native-dev")||validation&&label!="held-"+scenario)throw new ArgumentException("One development and five declared matched held labels only");Require(validation);
  var condition=Study156.Read<JsonElement>(Path.Combine(Study156.Root,"docs/experiments/robust-three-lap/final-v6/scenarios-frozen.json")).GetProperty("Scenarios")[scenario];var gate=Enum.Parse<StartingGate>(condition.GetProperty("Gate").GetString()!);var spec=StandingWorld144.Rider(gate) with{OffsetMeters=condition.GetProperty("OffsetMeters").GetSingle(),TargetOffsetMeters=condition.GetProperty("OffsetMeters").GetSingle()};
  string baselineLabel=validation?"held-"+scenario:"native-dev";var baseline=Study156.Read<JsonElement>(Path.Combine(Program161.Doc,"native/"+baselineLabel+"-result.json"));
  using var budget=new Budget164(label,"native",concurrent:validation);using var evidence=new EvidenceBundle162(Path.Combine(Program164.Doc,"native/"+label+".zip"));Planner164? planner=null;State146? first=null;float? actualMass=null,actualInertia=null;int accepted=0,abstain=0;double printAt=0;var journal=new List<object>();
  Decision144 Choose(FreeRacingInput input,CrankClutch144 crank,RiderSpec rider,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,string variant){
   first??=new(input,crank.Omega);actualMass??=mass;actualInertia??=inertia;var began=Process.GetCurrentProcess().TotalProcessorTime;Decision144 decision=null!;string? error=null;object? compact=null;
   try{budget.Guard();planner??=new(rider,false,new(Enabled:true,MaximumRoots:3),budget.Guard,settings:new(Enabled:true,Adaptive:true));decision=planner.Choose(input,crank);var review=planner.Last!;if(!review.Independent||!review.ConditionalCertificate||review.Tree.Length!=13||review.Terminals.Length!=9)throw new InvalidOperationException("Missing full fresh independent13/9");compact=Evidence152.Compact(review);}
   catch(Exception e)when(e is InvalidOperationException or ArgumentException){error=e.ToString();abstain++;}
   evidence.Policy(error is null?"decision-"+accepted.ToString("D4"):"abstain",new{Observed=new State146(input,crank.Omega),Error=error,Review=compact,Counters=planner?.Runtime.Counters(),SelectedOrigin=planner?.SelectedAnchor?.Origin,ControlSettings=planner?.Generator.Control.Settings,LastGeneratedSpeedProfile=planner?.Generator.Control.LastSpeedProfile,ControlPredictions=planner?.Generator.Control.Predictions,ControlContactQueries=planner?.Generator.Control.ContactPredictions,ControlForceQueries=planner?.Generator.Control.ForceQueries,Outcomes=planner?.Generator.Outcomes,Rejections=planner?.Generator.Rejections});
   double cpu=(Process.GetCurrentProcess().TotalProcessorTime-began).TotalSeconds;journal.Add(new{Time=input.Time,Reference=input.Rider.TotalReferenceMeters,PlannerCpuSeconds=planner?.Last?.CpuSeconds,CallbackCpuSeconds=cpu,Accepted=error is null,Reason=error??planner?.Last?.Reason,Action=error is null?decision.Choice.Action:(FreeRacingAction?)null,Nodes=planner?.Last?.Tree.Length??0,Reserves=planner?.Last?.Terminals.Length??0});
   if(error is not null)throw new InvalidOperationException(error);accepted++;if(input.Time>=printAt){Console.WriteLine($"{label}: t={input.Time:F1} ref={input.Rider.TotalReferenceMeters:F1} kmh={input.Rider.Speed*3.6:F2} certificates={accepted} CPU={budget.Elapsed:F1}");printAt=input.Time+5;}
   return decision with{CpuSeconds=cpu};
  }
  Run144 run;using(var world=new NativeWorld156(new(Enabled:true,RequiredLaps:3,MaximumSeconds:180,SoloGate:gate),diagnosticRider:spec,safetyHalfMeter:true,safetyFrom:0))run=world.Run(label,"K1",Choose);
  var samples=run.Riders[0].Samples;var pose=samples.Min(s=>Math.Min(s.Inner,s.Outer));var swept=Native161.MinimumSweep(samples.Skip(1).Select(s=>s.Certificate));var beta=samples.Max(s=>Math.Abs(s.Input.Rider.SlideRadians))*180/Math.PI;bool exactStart=first is not null&&ExactKey147.State(first)==ExactKey147.State(baseline.GetProperty("Initial").Deserialize<State146>(Study156.Json)!);
  bool legal=exactStart&&first!.Input.Time==0&&first.Input.Rider.Speed==0&&first.Input.Pending.Length==0&&actualMass==147&&actualInertia==baseline.GetProperty("ActualInertiaKgMetersSquared").GetSingle()&&run.Riders[0].CompletedLaps==3&&run.Riders[0].FinishSeconds is not null&&run.Outcome=="completed"&&run.Contacts.Length==0&&pose>=.5&&swept>=.5&&beta<=45&&abstain==0;
  bool faster=legal&&run.Riders[0].FinishSeconds<baseline.GetProperty("FinishSeconds").GetDouble();
  var result=new{Label=label,Validation=validation,Scenario=condition,PolicyHash=Program164.PolicyHash(),Pass=legal,FasterThan161=faster,Exact161Initial=exactStart,Initial=first,Spec=spec,ActualMassKg=actualMass,ActualInertiaKgMetersSquared=actualInertia,FinishSeconds=run.Riders[0].FinishSeconds,CompletedLaps=run.Riders[0].CompletedLaps,MinimumPoseMeters=pose,MinimumSweepMeters=swept,MaximumBetaDegrees=beta,Contacts=run.Contacts.Length,AcceptedDecisions=accepted,Abstain=abstain,run.Outcome,run.Fingerprint,run.TotalCpuSeconds,run.ControllerCpuSeconds,ChargedCpuSeconds=budget.Elapsed,BaselineLabel=baselineLabel,BaselineFinishSeconds=baseline.GetProperty("FinishSeconds").GetDouble(),BaselineCpuSeconds=baseline.GetProperty("ChargedCpuSeconds").GetDouble(),BundleFormat="S3Evidence162-v1"};
  evidence.Complete(run,result,journal,new{Program164.Base,PolicyHash=Program164.PolicyHash(),Files=Program164.Sources(),Scope="One uninterrupted original Box2D world. Archives never supply commands or certificates. Original Runtime158 preflight is fresh for every accepted review."});Program164.Save("native/"+label+"-result.json",result);
  if(!legal||!validation&&!faster)Program164.Save("series-closure.json",new{NativeDNF=!legal,NoFurtherNativeWorlds=true,Reason=legal?"No faster development S3":run.Outcome});budget.Finish(run.Outcome);Console.WriteLine(label+" PASS="+legal+" faster="+faster+" finish="+run.Riders[0].FinishSeconds+" CPU="+budget.Elapsed);
 }
}
