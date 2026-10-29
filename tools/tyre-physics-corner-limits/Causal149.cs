using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
using Speed148;
namespace Limits149;
public static class Causal149{
 public static void Run(bool ownPrefix=false){
  var records=Journal147.ReadComplete(Program149.Doc+"/native/"+(ownPrefix?"separated149-autonomous":"baseline149-final148")+"-decisions.jsonl").Where(r=>r.GetProperty("Event").GetString()=="decision").ToArray();
  foreach(var separated in new[]{false,true})foreach(var at in new[]{28.4,37.2})foreach(var mode in new[]{"control","gas","early","outward"}){
   if(at==37.2&&mode=="outward")continue;
   if(ownPrefix&&(!separated||at!=37.2))continue;
   var name="causal-"+at.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"-"+mode+"-"+(separated?"B":"A")+(ownPrefix?"-nativeprefix":"");if(File.Exists(Program149.Doc+"/fixtures/"+name+"-result.json"))continue;
   using var b=new Budget149(name,1,55);var process=Process.GetCurrentProcess();ExactRuntime148? a=null;ExactRuntime149? rt=null;int prefix=0;double prefixXY=0;bool exact=true;var interventions=new List<object>();
   Decision144 Choose(FreeRacingInput i,CrankClutch144 c,RiderSpec s,TrackFrame f,CoreSim.TrackStateSnapshot surface,WorldParameters p,float m,float inertia,string variant){
    b.Guard();var entered=process.TotalProcessorTime;
    if(i.Time<at-1e-7){var saved=records.Single(r=>Math.Abs(r.GetProperty("Time").GetDouble()-i.Time)<1e-7);var observed=saved.GetProperty("Observed").Deserialize<FreeRacingInput>(Program149.Json)!;var old=new State146(observed,saved.GetProperty("CrankOmega").GetDouble());var same=ExactKey147.State(new(i,c.Omega))==ExactKey147.State(old);exact&=same;prefixXY=Math.Max(prefixXY,Vector2.Distance(i.Rider.Position,observed.Rider.Position));if((!separated||ownPrefix)&&!same)throw new InvalidOperationException("Native prefix differs from its own preserved baseline");prefix++;return new(new(i.Time,saved.GetProperty("Selected").Deserialize<FreeRacingAction>(Program149.Json),FreeActionSafety.Uncertain,false,Array.Empty<FreeLearningCandidate>(),0,0),(process.TotalProcessorTime-entered).TotalSeconds);}
    Decision144 d;if(separated){rt??=new(s,f,surface,p,mass:m,inertia:inertia);rt.Begin(i.Time);d=rt.Seed(new(i,c.Omega));}else{a??=new(s,f,surface,p,mass:m,inertia:inertia);a.Begin(i.Time);d=a.Seed(new(i,c.Omega));}
    var action=d.Choice.Action;bool pulse=mode=="gas"&&i.Time>=at+1-1e-7&&i.Time<at+1.2-1e-7;bool steer=(mode=="early"||mode=="outward")&&i.Time<at+.4-1e-7;
    if(pulse)action=action with{Drive=1};if(steer)action=action with{SteeringRadians=Math.Clamp(action.SteeringRadians+(mode=="early"?1:-1)*.5f*MathF.PI/180,-TwoContactBikeParameters.Default.MaximumSteeringRadians,TwoContactBikeParameters.Default.MaximumSteeringRadians)};
    if(pulse||steer)interventions.Add(new{i.Time,Observed=i,CrankOmega=c.Omega,Unmodified=d.Choice.Action,Submitted=action,ActualActivation=i.Time+.24,Scope="Diagnostic intervention, no autonomous credit; after pulse endogenous K1 through current and following bend."});
    return new(d.Choice with{Action=action},(process.TotalProcessorTime-entered).TotalSeconds);
   }
   Run144 run;using(var w=new DiagnosticWorld149(new(Enabled:true,RequiredLaps:3,MaximumSeconds:65),separated:separated,observeBeyondBeta:true))run=w.Run(name,"K1",Choose);
   Program149.Save("fixtures/"+name+".json.gz",run);var ss=run.Riders[0].Samples;
   Program149.Save("fixtures/"+name+"-result.json",new{Model=separated?"B":"A",At=at,Mode=mode,PrefixSource=ownPrefix?"B":"A",run.Outcome,run.EndSeconds,run.Fingerprint,FinishSeconds=run.Riders[0].FinishSeconds,PrefixDecisions=prefix,PrefixExact=exact,PrefixMaximumPositionDifference=prefixXY,ExtendedBetaObservation=true,MaximumBetaDegrees=ss.Max(s=>Math.Abs(s.Input.Rider.SlideRadians)*180/Math.PI),MinimumGap=ss.Min(s=>Math.Min(s.Inner,s.Outer)),MinimumSweep=ss.Where(s=>s.Certificate is not null).Min(s=>s.Certificate),Contacts=run.Contacts.Length,run.ControllerCpuSeconds,run.NativeAndObservationCpuSeconds,CpuSeconds=b.Elapsed,Interventions=interventions});b.Finish(run.Outcome);Console.WriteLine(name+": "+run.Outcome+" t="+run.EndSeconds+" cpu="+b.Elapsed);Console.Out.Flush();
  }
 }
 public static void CornerParity(){var f=StandingWorld144.Frame();foreach(var model in new[]{false,true})Fixtures149.Execute("corner-parity-"+(model?"B":"A"),model,StandingWorld144.Rider(CoreSim.StartingGate.B) with{SpeedMetersPerSecond=80/3.6f,ReferenceMeters=f.SegmentStartMeters(1)-12,OffsetMeters=6},9,extended:true,parity:true);}
}
