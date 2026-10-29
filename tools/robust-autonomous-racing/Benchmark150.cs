using System.Text.Json;
using SharedWorldSpeedway;
namespace Robust150;
public static class Benchmark150{
 public static void Run(){using var budget=new Budget150("manual-benchmark-same-observation",0,60);var old=Program150.Read<JsonElement>(Diagnosis150.Previous+"/fixtures/causal-28.4-outward-B-result.json");var obs=old.GetProperty("Interventions")[0];var input=obs.GetProperty("Observed").Deserialize<FreeRacingInput>(Program150.Json)!;var crank=obs.GetProperty("CrankOmega").GetDouble();var native=Program150.Read<Run144>(Diagnosis150.Previous+"/fixtures/causal-28.4-outward-B.json.gz");var planner=new Planner150(native.Riders[0].Spec,true,new(Enabled:true,Feedback:true),budget.Guard);string? failure=null;try{planner.Choose(input,new(omega:crank));}catch(InvalidOperationException error){failure=error.Message;}
  Program150.Save("manual-benchmark-planner-probe.json",new{Failure=failure,Review=planner.Last,Scope="Only the accepted current observation and prior error deltas supplied to planner. Benchmark commands/time are not supplied."});budget.Finish(failure??"certified");Console.WriteLine(failure??"certified benchmark-state maneuver");
 }
}
