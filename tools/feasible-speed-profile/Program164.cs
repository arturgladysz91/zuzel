using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Autonomous146;
using Continuation161;
using CoreSim;
using Efficient152;
using LineThrottle156;
using Robust150;
using S3Performance162;
using SharedWorldSpeedway;
using ThreeLap158;
namespace FeasibleSpeed164;
public static class Program164 {
 public const string Base="75c208c28225005589b5ad84eb5825c1abcf3e1b";
 public static string Doc=>Path.Combine(Study156.Root,"docs/experiments/feasible-speed-profile");
 public static void Save(string name,object data)=>Study156.Save(Path.Combine(Doc,name),data);
 public static Dictionary<string,string> Sources()=>Directory.GetFiles(Path.Combine(Study156.Root,"tools/feasible-speed-profile")).Where(p=>p.EndsWith(".cs",StringComparison.Ordinal)||p.EndsWith(".csproj",StringComparison.Ordinal)||Path.GetFileName(p) is "generate164.py" or "packages.lock.json").OrderBy(p=>p,StringComparer.Ordinal).ToDictionary(p=>Path.GetFileName(p),p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
 public static string PolicyHash()=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Sources().Where(p=>p.Key is "Control164.cs" or "Feedback164.cs" or "Generator164.cs" or "Planner164.Generated.cs" or "SpeedProfile164.cs").ToDictionary(p=>p.Key,p=>p.Value))));
 public static int Main(string[] args){try{if(args.Length==2&&args[0]=="--diagnostic"){Diagnostic(args[1]);return 0;}if(args.Length==2&&args[0]=="--certificate"){Certificate(args[1]);return 0;}if(args.Length==2&&args[0]=="--native"){Native164.Run(args[1]);return 0;}if(args.Length==3&&args[0]=="--validate"){Native164.Run(args[2],int.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture),true);return 0;}if(args.Length==1&&args[0]=="--freeze"){Native164.Freeze();return 0;}throw new ArgumentException("Study164 OFF; explicit declared budgeted operation only. No native admission without all successful gates.");}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 public static void RequireDiagnostics(){if(File.Exists(Path.Combine(Doc,"stage-closure.json")))throw new InvalidOperationException("Earlier failed gate closes later stages");foreach(var label in new[]{"d1-straight","d2-entry","d3-rest"}){var q=Study156.Read<JsonElement>(Path.Combine(Doc,"diagnostics/"+label+"-result.json"));if(!q.GetProperty("GatePassed").GetBoolean()||q.GetProperty("PolicyHash").GetString()!=PolicyHash())throw new InvalidOperationException("All three current-policy diagnostic gates required");}}
 public static void Certificate(string label){
  RequireDiagnostics();int decision=label switch{"c1-rest"=>0,"c2-straight"=>63,_=>throw new ArgumentException("Two declared certification cases only")};
  if(decision!=0){var prior=Study156.Read<JsonElement>(Path.Combine(Doc,"certificates/c1-rest-result.json"));if(!prior.GetProperty("GatePassed").GetBoolean())throw new InvalidOperationException("First certification must pass");}
  using var budget=new Budget164(label,"certificate");var fixture=Fixture(decision);var planner=new Planner164(Program161.Spec,false,new(Enabled:true,MaximumRoots:3),budget.Guard,settings:new(Enabled:true,Adaptive:true));string? error=null;
  try{planner.Choose(fixture.State.Input,new(omega:fixture.State.CrankOmega));}catch(Exception e){error=e.ToString();}
  var r=planner.Last;bool gate=error is null&&r is not null&&r.ConditionalCertificate&&r.Independent&&r.Tree.Length==13&&r.Terminals.Length==9;
  var result=new{Label=label,PolicyHash=PolicyHash(),GatePassed=gate,Error=error,CpuSeconds=budget.Elapsed,Nodes=r?.Tree.Length??0,Reserves=r?.Terminals.Length??0,Independent=r?.Independent??false,PhysicalHash=r is null?null:Bits152.Review(r),PhysicalCounters=planner.Runtime.Counters(),Scope="Fresh original complete13/9 independent certificate, not a native S3"};
  using(var evidence=new EvidenceBundle162(Path.Combine(Doc,"certificates/"+label+".zip"))){evidence.Policy("certificate",new{Initial=fixture.State,Result=result,Review=r is null?null:Evidence152.Compact(r),Outcomes=planner.Generator.Outcomes,Rejections=planner.Generator.Rejections});evidence.Complete(new{Scope="No native world"},result,new{Label=label},new{Base,PolicyHash=PolicyHash(),Files=Sources()});}
  Save("certificates/"+label+"-result.json",result);if(!gate)Save("stage-closure.json",new{Label=label,Reason=error??"Missing full fresh13/9 independent certificate",NoLaterCostlyStages=true,NoNativeWorlds=true});budget.Finish(error??(gate?"full13/9 independent certification gate passed":"certification failed; later stages closed"));Console.WriteLine(label+" gate="+gate+" CPU="+budget.Elapsed);
 }
 public static (State146 State,double Goal,double SafetyGoal,double Arrival) Fixture(int decision){
  if(decision==0){var d=Study156.Read<JsonElement>(Path.Combine(Program161.Doc,"diagnostics/d2-rest-terminal.json.gz"));var tape=d.GetProperty("Selected");return(Program158.Initial(Program161.Spec),tape.GetProperty("Goal").GetDouble(),tape.GetProperty("SafetyGoal").GetDouble(),tape.GetProperty("Arrival").GetDouble());}
  var q=Study156.Read<JsonElement>(Path.Combine(Program161.Doc,"policies/native-dev/decision-"+decision.ToString("D4")+".json.gz"));var r=q.GetProperty("Review");var t=r.GetProperty("Tapes").GetProperty(r.GetProperty("Nominal").GetString()!);return(q.GetProperty("Observed").Deserialize<State146>(Study156.Json)!,r.GetProperty("Goal").GetDouble(),r.GetProperty("SafetyGoal").GetDouble(),t.GetProperty("Arrival").GetDouble());
 }
 public static void Diagnostic(string label){
  var cases=Study156.Read<JsonElement>(Path.Combine(Doc,"budget-plan.json")).GetProperty("Cases").EnumerateArray().ToArray();int index=Array.FindIndex(cases,c=>c.GetProperty("Label").GetString()==label);if(index<0)throw new ArgumentException("Declared label only");
  if(File.Exists(Path.Combine(Doc,"stage-closure.json")))throw new InvalidOperationException("Failed gate closes later stages; retain all evidence");
  for(int i=0;i<index;i++){var prior=Study156.Read<JsonElement>(Path.Combine(Doc,"diagnostics/"+cases[i].GetProperty("Label").GetString()+"-result.json"));if(!prior.GetProperty("GatePassed").GetBoolean()||prior.GetProperty("PolicyHash").GetString()!=PolicyHash())throw new InvalidOperationException("Previous current-source diagnostic gate must pass");}
  using var budget=new Budget164(label);var fixture=Fixture(cases[index].GetProperty("Decision").GetInt32());var rt=new Runtime158(Program161.Spec,false,Hierarchical155.Program155.Options());rt.Begin(fixture.State.Input.Time);var points=new List<object>();var traces=new List<object>();Generator164? generator=null;
  Transition146 Step(State146 state,FreeRacingAction action,string phase){budget.Guard();var t=rt.Step(state,action);var v=state.Input.Rider;
   var force=Contact144.Forces(v,Program161.Spec,rt.Frame,MultiActionExperiment135.Surface(rt.Frame),FreeLearningExperiment.Parameters(),state.Input.Time,147,Speed148.ForecastFork148.Wheel(state.Input.Omega),new(state.Input.Steering),.01f,steeringOverride:state.Input.Active.SteeringRadians,inertia:52.72097f,powertrain:new(omega:state.CrankOmega),releaseAt:.24);
   points.Add(new{Time=state.Input.Time,State=state,Requested=action,ForceAction=state.Input.Active,Force=force,Scope="Original force probe at each diagnostic review start with current active command; independent uncommitted wheel/crank copies, not a native accepted100Hz ledger."});return t;}
  Control164.DiagnosticObserver=(c,s,a)=>traces.Add(new{Maneuver=c.Settings.Maneuver.ToString(),Observed=s,Action=a,Detail=c.Last,Profile=c.LastSpeedProfile,c.Predictions,c.ContactPredictions,c.ForceQueries});
  string? error=null;Tape150[] legal=Array.Empty<Tape150>();
  try{generator=new(rt,Step,budget.Guard);generator.Begin();legal=generator.Generate(fixture.State,fixture.Goal,fixture.SafetyGoal,"study164 diagnostic",3);}catch(Exception e){error=e.ToString();}finally{Control164.DiagnosticObserver=null;}
  var track=generator?.Evaluated.FirstOrDefault(t=>t.Origin.EndsWith("/Track",StringComparison.Ordinal));bool gate=error is null&&track is not null&&track.Safety.Legal&&track.Arrival is {} arrival&&arrival<fixture.Arrival-.02;
  var result=new{Label=label,PolicyHash=PolicyHash(),Initial=fixture.State,fixture.Goal,fixture.SafetyGoal,Baseline161Arrival=fixture.Arrival,TrackArrival=track?.Arrival,GatePassed=gate,Error=error,Outcomes=generator?.Outcomes,Rejections=generator?.Rejections,PhysicalCounters=rt.Counters(),ControlObservations=traces.Count,UncommittedForceProbes=points.Count,CpuSeconds=budget.Elapsed,Scope="Diagnostic predictions only, not13/9 certificate or nativeS3. Full identical initial/pending and original propagation."};
  using(var evidence=new EvidenceBundle162(Path.Combine(Doc,"diagnostics/"+label+".zip"))){evidence.Policy("diagnostic",new{Initial=fixture.State,Result=result,Candidates=generator?.Evaluated,Control=traces,Forces=points});evidence.Complete(new{Scope="No native world"},result,new{Label=label},new{Base,PolicyHash=PolicyHash(),Files=Sources()});}
  Save("diagnostics/"+label+"-result.json",result);if(!gate)Save("stage-closure.json",new{Label=label,Reason=error??"Complete legal Track with >=0.02s same-state improvement not demonstrated",NoLaterCostlyStages=true,NoNativeWorlds=true});budget.Finish(error??(gate?"diagnostic gate passed":"diagnostic gate failed; later stages closed"));Console.WriteLine(label+" gate="+gate+" baselineArrival="+fixture.Arrival+" TrackArrival="+track?.Arrival+" CPU="+budget.Elapsed);
 }
}
