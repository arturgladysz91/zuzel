using System.Text.Json;
using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
namespace Speed148;
public static class Causal148{
 public static void Run(){
  var records=Compare148.Journal();var saved=Program.Baseline();var frame=StandingWorld144.Frame();
  foreach(var at in new[]{28.4,29.4,32.8})foreach(var mode in new[]{"control","pulse","short"}){
   if(at==28.4&&mode=="short")continue;var duration=mode=="short"?.2:.6;var name="causal-"+at.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"-"+mode;var file=Program.Doc+"/diagnostics/"+name+"-result.json";if(File.Exists(file))continue;
   var prefix=true;int prefixDecisions=0;ExactRuntime148? runtime=null;FreeRacingAction? held=null;object? checkpoint=null;
   Decision144 Choose(FreeRacingInput input,CrankClutch144 crank,RiderSpec spec,TrackFrame f,CoreSim.TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,string variant){
    if(input.Time<at-1e-7){var record=records.Single(e=>Math.Abs(e.GetProperty("Time").GetDouble()-input.Time)<1e-7);var same=ExactKey147.State(new(input,crank.Omega))==ExactKey147.State(Compare148.State(record));prefix&=same;if(!same)throw new InvalidOperationException("Diagnostic native prefix differs.");prefixDecisions++;return new(new(input.Time,record.GetProperty("Selected").Deserialize<FreeRacingAction>(Program.Json),FreeActionSafety.SafeInHorizon,false,Array.Empty<FreeLearningCandidate>(),0,0),0);}
    runtime??=new(spec,f,surface,p,mass:mass,inertia:inertia);runtime.Begin(input.Time);var decision=runtime.Seed(new(input,crank.Omega));
    if(checkpoint is null){checkpoint=new{Observed=input,CrankOmega=crank.Omega,decision.Choice.Action};if(mode!="control"){
      if(at==28.4)held=new(decision.Choice.Action.SteeringRadians,0);else if(at==29.4&&mode!="short")held=new(decision.Choice.Action.SteeringRadians,1);else held=decision.Choice.Candidates.Where(c=>c.Action.Drive==1).OrderBy(c=>c.Safety).ThenBy(c=>c.Score).ThenBy(c=>c.Action.SteeringRadians).First().Action;
    }}
    return held is {} action&&input.Time<at+duration-1e-7?new(decision.Choice with{Action=action},decision.CpuSeconds):decision;
   }
   var result=Diagnostics148.Execute(name,frame,75,null,"recorded native prefix then causal K1; diagnostic pulse is not autonomous improvement",Choose,laps:3);
   Program.Save(Program.Doc+"/diagnostics/"+name+"-causal.json",new{At=at,Mode=mode,PrefixExact=prefix,PrefixDecisions=prefixDecisions,Checkpoint=checkpoint,HeldRequest=held,Duration=duration,ActualActivation=at+.24,Outcome=result.Outcome,Counts=runtime?.Counters()});
  }
 }
}
