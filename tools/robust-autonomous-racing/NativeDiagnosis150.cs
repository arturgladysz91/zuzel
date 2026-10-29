using System.Text.Json;
using Autonomous146;
using Efficient147;
using Limits149;
using SharedWorldSpeedway;
namespace Robust150;
public static class NativeDiagnosis150{
 public static void Run(){var data=Program150.Read<JsonElement>(Diagnosis150.Previous+"/local-replanning-audit.json.gz");foreach(var separated in new[]{false,true}){
  var model=separated?"B":"A";var name="fixed-certified-tape-native-"+model;using var budget=new Budget150(name,1,25);var old=Program150.Read<Run144>(Diagnosis150.Previous+"/fixtures/planned-entry-90-outer-"+model+".json.gz");var records=Journal147.ReadComplete(Diagnosis150.Previous+"/fixtures/planned-entry-90-outer-"+model+"-decisions.jsonl").Where(x=>x.GetProperty("Event").GetString()=="decision").ToArray();var item=data.EnumerateArray().Single(x=>x.GetProperty("Name").GetString()!.EndsWith(model,StringComparison.Ordinal));var plan=item.GetProperty("Reviews")[4].GetProperty("Selected").Deserialize<Plan146>(Program150.Json)!;bool prefixExact=true;
  Decision144 Choose(FreeRacingInput i,CrankClutch144 c,RiderSpec s,TrackFrame f,CoreSim.TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,string variant){budget.Guard();FreeRacingAction action;
   if(i.Time<1-1e-7){var row=records.Single(x=>Math.Abs(x.GetProperty("Time").GetDouble()-i.Time)<1e-7);var observed=row.GetProperty("Observed").Deserialize<FreeRacingInput>(Program150.Json)!;prefixExact&=ExactKey147.State(new(i,c.Omega))==ExactKey147.State(new(observed,row.GetProperty("CrankOmega").GetDouble()));if(!prefixExact)throw new InvalidOperationException("Native prefix mismatch");action=row.GetProperty("Selected").Deserialize<FreeRacingAction>(Program150.Json);}
   else{var index=1+(int)Math.Round((i.Time-1)/.2);if(index>=plan.Actions.Length)throw new InvalidOperationException("Saved diagnostic tape exhausted");action=plan.Actions[index];}
   return new(new(i.Time,action,FreeActionSafety.Uncertain,false,Array.Empty<FreeLearningCandidate>(),0,0),0);
  }
  Run144 run;using(var w=new DiagnosticWorld149(new(Enabled:true,RequiredLaps:1,MaximumSeconds:18),diagnosticRider:old.Riders[0].Spec,separated:separated,observeBeyondBeta:true))run=w.Run(name,"K1",Choose);Program150.Save("native/"+name+".json.gz",run);var ss=run.Riders[0].Samples;Program150.Save("native/"+name+"-result.json",new{run.Outcome,run.EndSeconds,run.Fingerprint,PrefixExact=prefixExact,MaximumBeta=ss.Max(s=>Math.Abs(s.Input.Rider.SlideRadians)*180/Math.PI),MinimumGap=ss.Min(s=>Math.Min(s.Inner,s.Outer)),MinimumSweep=ss.Where(s=>s.Certificate is not null).Min(s=>s.Certificate),Contacts=run.Contacts.Length,CpuSeconds=budget.Elapsed,Scope="Diagnostic fixed tape with exact native prefix. No autonomy or race PASS outside beta45; original finite/passivity/energy/footprint/actuator guards retained."});budget.Finish(run.Outcome);Console.WriteLine(name+": "+run.Outcome+" "+run.EndSeconds);
 }}
}
