using System.Text.Json;
using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
namespace Robust150;
public static class ContractAudit150{
 public static void Run(){using var budget=new Budget150("uncached-feedback-contract-audit",0,100);var output=new List<object>();
  foreach(var path in Directory.GetFiles(Program150.Doc+"/native","robust150*-result.json").Order()){
   var result=Program150.Read<JsonElement>(path);var name=Path.GetFileName(path).Replace("-result.json","");var run=Program150.Read<Run144>(Program150.Doc+"/native/"+name+".json.gz");var model=result.GetProperty("Model").GetString()=="B";var h=result.TryGetProperty("InverseQuerySeconds",out var q)?q.GetSingle():0;var options=result.TryGetProperty("Options",out var o)?o.Deserialize<Options150>(Program150.Json)!:new(Enabled:true,Laps:1,Feedback:true);var rt=new Runtime150(run.Riders[0].Spec,model,false,h,options.SpatialFeedback,options.AcceptedOperatingPoint,options.JointFeedback);var rows=Journal147.ReadComplete(Program150.Doc+"/native/"+name+"-decisions.jsonl").Where(x=>x.GetProperty("Event").GetString()=="decision").ToArray();int verified=0;double minSweep=double.PositiveInfinity;var anchors=new Dictionary<string,Tape150>();
   foreach(var row in rows){budget.Guard();var r=row.GetProperty("Review");var state=r.GetProperty("Observed").Deserialize<State146>(Program150.Json)!;var id=row.GetProperty("Anchor").GetString()!;if(!anchors.TryGetValue(id,out var anchor)){anchor=Program150.Read<Tape150>(Program150.Doc+"/plans/"+name+"/"+id+".json.gz");anchors[id]=anchor;}rt.Begin(state.Input.Time);using var independent=rt.Operation("independent_preflight");var check=Feedback150.Evaluate(rt,state,anchor,r.GetProperty("Goal").GetDouble(),r.GetProperty("SafetyGoal").GetDouble(),"uncached-audit",budget.Guard);var actions=r.GetProperty("Selected").Deserialize<FreeRacingAction[]>(Program150.Json)!;var selected=row.GetProperty("Selected").Deserialize<FreeRacingAction>(Program150.Json);if(!check.Safety.Legal||!actions.SequenceEqual(check.Actions)||check.Actions[0]!=selected)throw new InvalidOperationException("Contract audit mismatch "+name+" at "+state.Input.Time);minSweep=Math.Min(minSweep,check.Safety.SweepLowerBoundMeters);verified++;}
   output.Add(new{Name=name,VerifiedDecisions=verified,AllCommandSequencesExact=true,AllCompleteForecastsLegal=true,MinimumSweep=minSweep,PhysicsAndFeedbackCacheBypassed=true,Counts=rt.Counters()});Console.WriteLine(name+" verified="+verified);
  }
  Program150.Save("contract-audit.json",output);budget.Finish("all selected complete feedback contracts independently re-evaluated without caches");
 }
}
