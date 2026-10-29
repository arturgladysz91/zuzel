using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
namespace Speed148;
public static partial class Program{
 public const string Doc="docs/experiments/planner-speed-root-cause";
 public static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
 public static T ReadZip<T>(string path){using var z=new GZipStream(File.OpenRead(path),CompressionMode.Decompress);return JsonSerializer.Deserialize<T>(z,Json)!;}
 public static void Save(string path,object value){if(File.Exists(path))throw new IOException("Preserve evidence "+path);if(path.EndsWith(".gz",StringComparison.Ordinal)){using var z=new GZipStream(File.Create(path),CompressionLevel.Fastest);JsonSerializer.Serialize(z,value,Json);}else File.WriteAllText(path,JsonSerializer.Serialize(value,Json));}
 public static Run144 Baseline()=>ReadZip<Run144>("docs/experiments/efficient-autonomous-planner/native/autonomous147-shortlist.json.gz");
 public static void Main(string[] args){if(args.Length!=1)throw new ArgumentException("Explicit --profile148 only; default OFF.");if(args[0]=="--profile148")Profile();else if(args[0]=="--prefix148")Profile(true,true,true);else if(args[0]=="--coefficients148")Profile(true,true);else if(args[0]=="--powertrain-profile148")Profile(true);else if(args[0]=="--boundary-grid148")BoundaryGrid();else if(args[0]=="--maps148")Maps148.Run();else if(args[0]=="--causal148")Causal148.Run();else if(args[0]=="--geometry148")Diagnostics148.Geometry();else if(args[0]=="--capabilities148")Diagnostics148.Capabilities();else if(args[0]=="--geometry-audit148")Diagnostics148.GeometryAudit();else if(args[0]=="--compare148")Compare148.Run();else if(args[0]=="--nativeA148")Native148.Run("autonomous148-exact",false);else if(args[0]=="--nativeA3-148")Native148.Run("autonomous148-prefix",false);else if(args[0]=="--nativeA2-148")Native148.Run("autonomous148-hoisted",false);else if(args[0]=="--nativeB148")Native148.Run("autonomous148-sparse-tail",true);else throw new ArgumentException("Unknown explicit study flag.");}

 static void BoundaryGrid(){
  using var b=new Budget148("boundary-adversarial-grid",0,40);var r=Baseline();var f=StandingWorld144.Frame();var old=new RacingLineGeometry(f);var fast=new FastBoundary148(f);var template=r.Riders[0].Samples[300].Input.Rider;int n=0;
  foreach(var reference in Enumerable.Range(0,160).Select(i=>i*f.LapMeters/160))foreach(var offset in new[]{-3d,-1.2,-.8,-.4,0,4,10,14.5,15.5,17,20})foreach(var yaw in new[]{-1.2f,-.4f,0f,.4f,1.2f}){
   b.Guard();var at=f.At(reference,offset);var v=template with{Position=at.Position,Road=f.Project(at.Position),Heading=(float)at.Road.Heading+yaw};if(old.Clearance(v)!=fast.Clearance(v))throw new InvalidOperationException("Adversarial exact parity failed at "+reference+" / "+offset);n++;
  }
  Save(Doc+"/boundary-grid.json",new{States=n,BitExact=true,fast.FastQueries,fast.FallbackQueries,CpuSeconds=b.Elapsed});b.Finish("full-clearance bit parity PASS");
 }
 static State146 State(StartSample144 s)=>new(s.Input with{NextReview=s.Input.Time,Pending=s.Input.Pending.Where(c=>c.ObservedAt<s.Input.Time-1e-8).ToArray()},s.CrankOmega);
 static void Profile(bool hoisted=false,bool coefficients=false,bool prefix=false){
  using var budget=new Budget148(prefix?"exact-prefix-profile":coefficients?"exact-coefficients-profile":hoisted?"exact-hoisted-profile":"exact-clearance-profile",0,120);var r=Baseline();var f=StandingWorld144.Frame();var p=FreeLearningExperiment.Parameters();var surface=MultiActionExperiment135.Surface(f);var spec=r.Riders[0].Spec;
  var fast=new FastBoundary148(f);var old=new RacingLineGeometry(f);var views=r.Riders[0].Samples.Select(s=>s.Input.Rider).ToArray();var checks=views.Select(v=>(Old:old.Clearance(v),New:fast.Clearance(v))).ToArray();if(checks.Any(q=>q.Old!=q.New))throw new InvalidOperationException("Clearance parity failed.");
  object Measure(Func<(double,double)> action,int count){var cpu=Process.GetCurrentProcess().TotalProcessorTime;var alloc=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<count;i++)action();return new{CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-alloc};}
  int index=0;var oldCost=Measure(()=>old.Clearance(views[index++%views.Length]),views.Length*2);index=0;var fastCost=Measure(()=>fast.Clearance(views[index++%views.Length]),views.Length*2);
  var results=new List<object>();foreach(var at in new[]{0d,22.6,30,38.2}){
   budget.Guard();var s=State(r.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-at))!);var baseRt=new ExactRuntime147(spec,f,surface,p);var newRt=new ExactRuntime148(spec,f,surface,p);baseRt.Begin(s.Input.Time);newRt.Begin(s.Input.Time);
   var cpu=Process.GetCurrentProcess().TotalProcessorTime;var alloc=GC.GetAllocatedBytesForCurrentThread();var a=baseRt.Seed(s);var oldCpu=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;var oldBytes=GC.GetAllocatedBytesForCurrentThread()-alloc;
   cpu=Process.GetCurrentProcess().TotalProcessorTime;alloc=GC.GetAllocatedBytesForCurrentThread();var b=newRt.Seed(s);var newCpu=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;var newBytes=GC.GetAllocatedBytesForCurrentThread()-alloc;
   var equal=JsonSerializer.Serialize(a.Choice,Json)==JsonSerializer.Serialize(b.Choice,Json);if(!equal)throw new InvalidOperationException("Seed decision parity failed.");
   var trajectories=FreeLearningController.Actions(s.Input,Controller143.Policy,true).Select(action=>JsonSerializer.Serialize(baseRt.ForecastForDiagnostics(s,action,.9),Json)==JsonSerializer.Serialize(newRt.ForecastForDiagnostics(s,action,.9),Json)).ToArray();if(trajectories.Any(q=>!q))throw new InvalidOperationException("Full prediction parity failed.");
   results.Add(new{Time=s.Input.Time,DecisionBitExact=equal,PredictionsBitExact=trajectories.All(q=>q),oldCpu,newCpu,oldBytes,newBytes,Counts=newRt.Counters()});
  }
  Save(Doc+(prefix?"/prefix-profile.json":coefficients?"/coefficients-profile.json":hoisted?"/hoisted-profile.json":"/exact-profile.json"),new{ClearanceSamples=views.Length,AllBitExact=true,OldBoundary=oldCost,FastBoundary=fastCost,fast.FastQueries,fast.FallbackQueries,Seeds=results,CpuSeconds=budget.Elapsed});budget.Finish("exact prediction/seed/clearance parity PASS");Console.WriteLine(JsonSerializer.Serialize(results,Json));
 }
}
