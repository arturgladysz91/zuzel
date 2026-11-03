using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Autonomous146;
using CoreSim;
using Efficient152;
using LineThrottle156;
using Robust150;
using SharedWorldSpeedway;
using ThreeLap158;
namespace Continuation161;
public static class Program161 {
 public const string Base="e8866e91c4c3818b8ca836260883ca847ccc4a8a";
 public static string Doc=>Path.Combine(Study156.Root,"docs/experiments/trajectory-continuation");
 public static void Save(string file,object value)=>Study156.Save(Path.Combine(Doc,file),value);
 public static int Main(string[] args){try{
  if(args.Length==2&&args[0]=="--native"){Native161.Run(args[1]);return 0;}
  if(args.Length==3&&args[0]=="--validate"){Native161.Run(args[2],int.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture),true);return 0;}
  if(args.Length==1&&args[0]=="--freeze"){Freeze161.Create();return 0;}
  if(args.Length==3&&args[0]=="--diagnostic"){Diagnostic(args[1],args[2]);return 0;}
  if(args.Length==3&&args[0]=="--certificate"){Certificate(args[1],args[2]);return 0;}
  throw new ArgumentException("Study161 OFF; explicit bounded research operation required");
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 public static RiderSpec Spec=>StandingWorld144.Rider(StartingGate.B);
 public static State146 Fixture(string name){
  if(name=="rest")return Program158.Initial(Spec);
  var run=Study156.Read<Run144>("docs/experiments/dynamic-slip-preview/native/native-dev-v5.json.gz");var samples=run.Riders[0].Samples;
  var sample=name=="native-end"?samples[^1]:samples.First(s=>Math.Abs(s.Input.Time-double.Parse(name,System.Globalization.CultureInfo.InvariantCulture))<1e-8);
  return new(sample.Input,sample.CrankOmega);
 }
 public static string PolicyHash(){var dir=Path.Combine(Study156.Root,"tools/trajectory-continuation");var bytes=new MemoryStream();foreach(var name in new[]{"Control161.cs","Feedback161.cs","Generator161.cs","Planner161.Generated.cs","Space161.cs"}){bytes.Write(System.Text.Encoding.UTF8.GetBytes(name));bytes.Write(File.ReadAllBytes(Path.Combine(dir,name)));}return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));}
 public static void Snapshot(string label){var file=Path.Combine(Doc,"sources",label+".zip");Directory.CreateDirectory(Path.GetDirectoryName(file)!);using var z=ZipFile.Open(file,ZipArchiveMode.Create);foreach(var p in Directory.GetFiles(Path.Combine(Study156.Root,"tools/trajectory-continuation")))z.CreateEntryFromFile(p,Path.GetFileName(p));}
 public static void Diagnostic(string label,string fixture){using var budget=new Budget161(label);Snapshot(label);var initial=Fixture(fixture);var rt=new Runtime158(Spec,false,Hierarchical155.Program155.Options());rt.Begin(initial.Input.Time);
  var generator=new Generator161(rt,(s,a,_)=>rt.Step(s,a),budget.Guard);double goal=3*rt.Frame.LapMeters,safetyGoal=goal+rt.Frame.SegmentStartMeters(1)-3;Tape150[] legal=Array.Empty<Tape150>();string? error=null;
  try{generator.Begin();legal=generator.Generate(initial,goal,safetyGoal,"diagnostic authentic state");}catch(Exception e){error=e.ToString();}
  var summary=new{Label=label,Fixture=fixture,PolicyHash=PolicyHash(),Legal=legal.Length>0,Error=error,Goal=goal,SafetyGoal=safetyGoal,InitialTime=initial.Input.Time,InitialSpace=Space161.Observe(initial,rt.Frame),Arrival=legal.FirstOrDefault()?.Arrival,Outcomes=generator.Outcomes,CpuSeconds=budget.Elapsed,PhysicalForecasts=rt.Forecasts,PhysicalContacts=rt.Contacts};
  Save("diagnostics/"+label+".json.gz",new{Summary=summary,Initial=initial,Selected=legal.FirstOrDefault(),Candidates=generator.Evaluated,Rejections=generator.Rejections,Scope="Diagnostic forecasts only; original model and full queue, no native world or13/9 result"});Console.WriteLine(JsonSerializer.Serialize(summary,Study156.Json));budget.Finish(error??(legal.Length>0?"legal full diagnostic continuation":"all bounded maneuvers rejected"));
 }
 public static void Certificate(string label,string fixture){using var budget=new Budget161(label,"certificate");Snapshot(label);var initial=Fixture(fixture);var planner=new Planner161(Spec,false,new(Enabled:true,MaximumRoots:3),budget.Guard);string? error=null;
  try{planner.Choose(initial.Input,new(omega:initial.CrankOmega));}catch(Exception e){error=e.ToString();}
  Save("certificates/"+label+".json.gz",new{Label=label,Fixture=fixture,PolicyHash=PolicyHash(),Initial=initial,Error=error,Review=planner.Last is null?null:Evidence152.Compact(planner.Last),Counters=planner.Runtime.Counters(),planner.Generator.Outcomes,planner.Generator.Rejections,CpuSeconds=budget.Elapsed});Console.WriteLine(label+" "+(error??"CERTIFIED")+" CPU="+budget.Elapsed);budget.Finish(error??"full13/9 independent certificate");
 }
}
