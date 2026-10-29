using System.Diagnostics;
using System.Numerics;
using Autonomous146;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Speed148;
namespace Limits149;
public static class Fixtures149{
 public static Run144 Execute(string name,bool separated,RiderSpec? spec,double seconds,bool extended=false,bool parity=false){
  using var b=new Budget149(name,1,90);var process=Process.GetCurrentProcess();ExactRuntime148? a=null;ExactRuntime149? rt=null;FreeRacingPoint? expected=null;double expectedCrank=0,maxPosition=0,maxSpeed=0,maxWheel=0,maxCrank=0;int comparisons=0;
  Decision144 Choose(FreeRacingInput i,CrankClutch144 c,RiderSpec s,TrackFrame f,CoreSim.TrackStateSnapshot surface,WorldParameters p,float m,float inertia,string variant){
   b.Guard();var entered=process.TotalProcessorTime;
   if(expected is not null&&Math.Abs(expected.Time-i.Time)<1e-8){maxPosition=Math.Max(maxPosition,Vector2.Distance(expected.Rider.Position,i.Rider.Position));maxSpeed=Math.Max(maxSpeed,Math.Abs(expected.Rider.Speed-i.Rider.Speed));maxWheel=Math.Max(maxWheel,Math.Abs(expected.Omega-i.Omega));maxCrank=Math.Max(maxCrank,Math.Abs(expectedCrank-c.Omega));comparisons++;}
   Decision144 d;if(separated){rt??=new(s,f,surface,p,mass:m,inertia:inertia);rt.Begin(i.Time);d=rt.Seed(new(i,c.Omega));}else{a??=new(s,f,surface,p,mass:m,inertia:inertia);a.Begin(i.Time);d=a.Seed(new(i,c.Omega));}
   if(parity){var prediction=separated?FastPredictor149.Predict(i,s,f,surface,p,m,inertia,.2,d.Choice.Action,powertrain:c,observeCrank:(_,w)=>expectedCrank=w):FastPredictor148.Predict(i,s,f,surface,p,m,inertia,.2,d.Choice.Action,powertrain:c,observeCrank:(_,w)=>expectedCrank=w);expected=prediction.Points[^1];}
   return new(d.Choice,(process.TotalProcessorTime-entered).TotalSeconds);
  }
  Run144 run;using(var w=new DiagnosticWorld149(new(Enabled:true,RequiredLaps:1,MaximumSeconds:seconds),diagnosticRider:spec,separated:separated,observeBeyondBeta:extended))run=w.Run(name,"K1",Choose);
  Directory.CreateDirectory(Program149.Doc+"/fixtures");Program149.Save("fixtures/"+name+".json.gz",run);var samples=run.Riders[0].Samples;
  var beta=samples.Max(s=>Math.Abs(s.Input.Rider.SlideRadians)*180/Math.PI);var gap=samples.Min(s=>Math.Min(s.Inner,s.Outer));var sweep=samples.Where(s=>s.Certificate is not null).Min(s=>s.Certificate!.Value);
  var frame=StandingWorld144.Frame();var exit=frame.SegmentStartMeters(4);var next=frame.SegmentStartMeters(5);var cleared=samples.Any(s=>s.Input.Rider.TotalReferenceMeters>=next);
  Program149.Save("fixtures/"+name+"-result.json",new{Model=separated?"B":"A",run.Outcome,run.EndSeconds,run.Fingerprint,run.ControllerCpuSeconds,run.NativeAndObservationCpuSeconds,run.NativeSteps,Contacts=run.Contacts.Length,ExtendedBetaObservation=extended,Beta45Exceeded=beta>45,MaximumBetaDegrees=beta,MinimumGap=gap,MinimumSweep=sweep,EndReference=samples[^1].Input.Rider.TotalReferenceMeters,CurrentCornerExit=exit,NextEntry=next,CompletedCornerAndWholeNextStraight=cleared,RaceEnvelopeCertifiedWindow=cleared&&beta<=45&&gap>=0&&sweep>=0&&run.Contacts.Length==0,Comparisons=comparisons,MaximumPredictionPositionError=maxPosition,MaximumPredictionSpeedError=maxSpeed,MaximumPredictionWheelError=maxWheel,MaximumPredictionCrankError=maxCrank,CpuSeconds=b.Elapsed});b.Finish(run.Outcome);Console.WriteLine(name+": "+run.Outcome+" s="+run.EndSeconds+" cpu="+b.Elapsed);Console.Out.Flush();return run;
 }
 public static void Parity(){foreach(var separated in new[]{false,true})Execute("parity-"+(separated?"B":"A"),separated,null,2,parity:true);}
 public static void Capabilities(){var f=StandingWorld144.Frame();foreach(var separated in new[]{false,true})foreach(var speed in new[]{70,80,90,100})foreach(var offset in new[]{2f,6f,10f}){
  var name=$"entry-{speed}-offset-{offset:0}-"+(separated?"B":"A");if(File.Exists(Program149.Doc+"/fixtures/"+name+"-result.json"))continue;
  var s=StandingWorld144.Rider(StartingGate.B) with{SpeedMetersPerSecond=speed/3.6f,ReferenceMeters=f.SegmentStartMeters(1)-12,OffsetMeters=offset};Execute(name,separated,s,14,extended:true);
 }}
}
