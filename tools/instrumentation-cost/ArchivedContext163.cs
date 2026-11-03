using System.Reflection;
using System.Text.Json;
using Autonomous146;
using Contingency151;
using Continuation161;
using CoreSim;
using Efficient152;
using LineThrottle156;
using Robust150;
using Safety153;
using ThreeLap158;
using SharedWorldSpeedway;
namespace Instrumentation163;

/// <summary>Archived inputs reconstruct a previous full policy/portfolio.
/// They seed a controlled microbenchmark, never a certificate for a new state.</summary>
public sealed class ArchivedContext163 {
 public required RiderSpec Spec{get;init;}
 public required State146 Observed{get;init;}
 public required Review151 Previous{get;init;}
 public required Settings161 Settings{get;init;}
 public required string ArchivedHash{get;init;}
 public required string PreviousReconstructedHash{get;init;}
 public required string PreviousArchivedHash{get;init;}
 public static ArchivedContext163 Load(string label,int decision,Action guard){
  var folder=Path.Combine(Program161.Doc,"policies",label);var before=Study156.Read<JsonElement>(Path.Combine(folder,"decision-"+(decision-1).ToString("D4")+".json.gz"));var current=Study156.Read<JsonElement>(Path.Combine(folder,"decision-"+decision.ToString("D4")+".json.gz"));var r=before.GetProperty("Review");var spec=Study156.Read<JsonElement>(Path.Combine(Program161.Doc,"native",label+"-result.json")).GetProperty("Spec").Deserialize<RiderSpec>(Study156.Json)!;var rt=new Runtime158(spec,false,Hierarchical155.Program155.Options());rt.Begin(r.GetProperty("Time").GetDouble());
  Tape150 Build(JsonElement t){var start=t.GetProperty("Start").Deserialize<State146>(Study156.Json)!;var actions=t.GetProperty("Actions").Deserialize<SharedWorldSpeedway.FreeRacingAction[]>(Study156.Json)!;var states=new List<State146>{start};var points=new List<SharedWorldSpeedway.FreeRacingPoint>();var state=start;
   foreach(var a in actions){guard();var step=rt.Step(state,a);states.Add(step.End);points.AddRange(step.Prediction.Points.Skip(points.Count==0?0:1));state=step.End;}
   var safety=t.GetProperty("Safety").Deserialize<Safety146>(Study156.Json)!;var checkedSafety=Clearance153.Check(points.ToArray(),Regime153.SafetyHalfMeter);if(safety!=checkedSafety)throw new InvalidOperationException("Archived full tape safety reconstruction mismatch");
   return new(t.GetProperty("CreatedAt").GetDouble(),t.GetProperty("Goal").GetDouble(),t.GetProperty("SafetyGoal").GetDouble(),actions,states.ToArray(),points.ToArray(),t.GetProperty("Arrival").ValueKind==JsonValueKind.Null?null:t.GetProperty("Arrival").GetDouble(),safety,"rehydrated archived input");
  }
  var pool=r.GetProperty("Tapes").EnumerateObject().ToDictionary(p=>p.Name,p=>Build(p.Value));var nodes=r.GetProperty("Tree").Deserialize<Node151[]>(Study156.Json)!;var terminals=r.GetProperty("Terminals").EnumerateArray().Select(t=>new Terminal151(t.GetProperty("Id").GetInt32(),t.GetProperty("Node").GetInt32(),pool[t.GetProperty("Tape").GetString()!])).ToArray();var nominal=pool[r.GetProperty("Nominal").GetString()!];
  var previous=new Review151(r.GetProperty("Time").GetDouble(),r.GetProperty("Observed").Deserialize<State146>(Study156.Json)!,r.GetProperty("Goal").GetDouble(),r.GetProperty("SafetyGoal").GetDouble(),r.GetProperty("Reason").GetString()!,r.GetProperty("ConditionalCertificate").GetBoolean(),r.GetProperty("Independent").GetBoolean(),r.GetProperty("IrrevocableUntil").GetDouble(),nodes,terminals,Array.Empty<RejectedTree151>(),Array.Empty<Attempt151>(),nominal,new{},new{},0);
  string rebuilt=Bits152.Review(previous),oldHash=r.GetProperty("PhysicalHash").GetString()!;if(rebuilt!=oldHash)throw new InvalidOperationException("Previous complete physical-review hash not reconstructed exactly");
  return new(){Spec=spec,Observed=current.GetProperty("Observed").Deserialize<State146>(Study156.Json)!,Previous=previous,Settings=before.GetProperty("ControlSettings").Deserialize<Settings161>(Study156.Json)!,ArchivedHash=current.GetProperty("Review").GetProperty("PhysicalHash").GetString()!,PreviousReconstructedHash=rebuilt,PreviousArchivedHash=oldHash};
 }
 public Planner163 Planner(ProfileMode163 mode,Action guard){var p=new Planner163(Spec,false,new(Enabled:true,MaximumRoots:3),guard,settings:Settings,profileMode:mode);typeof(Planner163).GetMethod("Remember",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(p,new object[]{Previous.Nominal!,Previous.Tree,Previous.Terminals,Previous.Time});return p;}
}
