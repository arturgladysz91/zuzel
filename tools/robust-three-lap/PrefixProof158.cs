using System.Numerics;
using System.Text.Json;
using Autonomous146;
using Efficient152;
using LineThrottle156;
using Safety153;
using SharedWorldSpeedway;
namespace ThreeLap158;
/// <summary>Read-only equivalence experiment for overlapping0.24/0.2 forecasts.
/// Never supplies a command or state to a native world.</summary>
public static class PrefixProof158 {
 public static void Run(string label){using var b=new Budget158(label,0,60);var dir=Path.Combine(Program158.Doc,"policies/dev-native-v3");var files=Directory.GetFiles(dir,"decision-*.json.gz").Order().ToArray();var rows=new List<object>();var spec=StandingWorld144.Rider(CoreSim.StartingGate.B);var frame=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(frame);var parameters=FreeLearningExperiment.Parameters();var boundary=new Boundary152(frame);
  foreach(var file in files.Take(Math.Max(0,files.Length-1)).Where((_,i)=>i%7==0)){b.Guard();var raw=Study156.Read<JsonElement>(file);var state=raw.GetProperty("Observed").Deserialize<State146>(Program158.Json)!;var rt=new Runtime153(spec,false,Hierarchical155.Program155.Options());rt.Begin(state.Input.Time);var action=new FreeRacingAction(0,0);var canonical=rt.Step(state,action);double crank=state.CrankOmega;
   var full=PredictorA153.Predict(state.Input,spec,frame,surface,parameters,147,52.72097f,.24,null,powertrain:new(omega:state.CrankOmega),observeCrank:(t,w)=>{if(t<=state.Input.Time+.2+1e-10)crank=w;},fastBoundary:boundary,exactInitialWheel:false);
   var points=full.Points.TakeWhile(p=>p.Time<=state.Input.Time+.2+1e-10).ToArray();var last=points[^1];last=last with{Time=Math.Min(last.Time,state.Input.Time+.2)};points[^1]=last;double length=0;for(int i=1;i<points.Length;i++)length+=Vector2.Distance(points[i-1].Rider.Position,points[i].Rider.Position);
   var queue=state.Input.Pending.Append(new FreeRacingCommand(state.Input.Time,state.Input.Time+.24,action)).OrderBy(c=>c.ActivationAt).ToArray();var active=queue.Where(c=>c.ActivationAt<=last.Time+1e-10).Select(c=>c.Action).LastOrDefault(state.Input.Active);var end=new State146(new(last.Rider with{Drive=active.Drive},last.Time,last.Omega,last.Steering,active,queue.Where(c=>c.ActivationAt>last.Time+1e-10).ToArray(),last.Time),crank);var extracted=new Transition146(end,new(points,0,length));
   rows.Add(new{Time=state.Input.Time,Reference=state.Input.Rider.TotalReferenceMeters,Equal=Bits152.Transition(canonical,extracted),Canonical=canonical.End,Extracted=extracted.End,CanonicalPoints=canonical.Prediction.Points.Length,ExtractedPoints=points.Length,CanonicalLength=canonical.Prediction.DistanceMeters,ExtractedLength=length});
  }
  Program158.Save(label+".json.gz",rows);Console.WriteLine(label+" "+rows.Count+" saved-observation comparisons");b.Finish("overlap equivalence diagnostic; not a native result");
 }
}
