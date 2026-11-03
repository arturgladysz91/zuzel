using System.Security.Cryptography;
using System.Text.Json;
using Autonomous146;
using Continuation161;
using Efficient152;
using LineThrottle156;
using ThreeLap158;
using Robust150;
namespace S3Performance162;
public static class Program162 {
 public const string Base="85a5ee3295c2dd983fbacb21859649f794cc5ba5";
 public static string Doc=>Path.Combine(Study156.Root,"docs/experiments/s3-performance");
 public static void Save(string path,object value)=>Study156.Save(Path.Combine(Doc,path),value);
 public static Dictionary<string,string> Sources()=>Directory.GetFiles(Path.Combine(Study156.Root,"tools/s3-performance")).Where(p=>p.EndsWith(".cs",StringComparison.Ordinal)||p.EndsWith(".csproj",StringComparison.Ordinal)||Path.GetFileName(p) is "packages.lock.json" or "generate162.py").OrderBy(p=>p,StringComparer.Ordinal).ToDictionary(p=>Path.GetFileName(p)!,p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))))!;
 public static string PolicyHash()=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Sources())));
 public static int Main(string[] args){try{
  if(args.Length==2&&args[0]=="--diagnostic"){Diagnostic(args[1]);return 0;}
  if(args.Length==3&&args[0]=="--certificate"){if(args[2] is not ("baseline" or "optimized"))throw new ArgumentException("baseline or optimized required");Certificate(args[1],args[2]=="optimized");return 0;}
  if(args.Length==2&&args[0]=="--native"){Native162.Run(args[1]);return 0;}
  if(args.Length==3&&args[0]=="--validate"){Native162.Run(args[2],int.Parse(args[1],System.Globalization.CultureInfo.InvariantCulture),true);return 0;}
  if(args.Length==1&&args[0]=="--freeze"){Gates162.Freeze();return 0;}
  throw new ArgumentException("Study162 OFF; explicit budgeted research command required");
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 public static void Diagnostic(string label){using var budget=new Budget162(label);var rt=new Runtime158(Program161.Spec,false,Hierarchical155.Program155.Options());var initial=Program158.Initial(Program161.Spec);rt.Begin(0);var g=new Generator162(rt,(s,a,_)=>rt.Step(s,a),budget.Guard);Tape150[] legal=Array.Empty<Tape150>();string? error=null;bool exact=false;
  try{g.Begin();legal=g.Generate(initial,3*rt.Frame.LapMeters,3*rt.Frame.LapMeters+rt.Frame.SegmentStartMeters(1)-3,"diagnostic authentic state");var baseline=Study156.Read<JsonElement>(Path.Combine(Program161.Doc,"diagnostics/d2-rest-terminal.json.gz")).GetProperty("Candidates").Deserialize<Tape150[]>(Study156.Json)!;exact=baseline.Length==g.Evaluated.Count&&baseline.Zip(g.Evaluated).All(p=>p.First.Actions.SequenceEqual(p.Second.Actions)&&p.First.States.Select(s=>Efficient147.ExactKey147.State(s)).SequenceEqual(p.Second.States.Select(s=>Efficient147.ExactKey147.State(s)))&&p.First.Safety==p.Second.Safety);if(!exact)error="Diagnostic maneuver behavior differs from frozen161";}catch(Exception e){error=e.ToString();}
  Save("diagnostic.json.gz",new{Label=label,PolicyHash=PolicyHash(),Initial=initial,Legal=legal.Length>0,Exact161=exact,Error=error,Candidates=g.Evaluated,Outcomes=g.Outcomes,CpuSeconds=budget.Elapsed});budget.Finish(error??"full three-lap+32m diagnostic and three retained maneuvers match161");Console.WriteLine(label+" Legal="+(legal.Length>0)+" exact161="+exact+" CPU="+budget.Elapsed);
 }
 public static void Certificate(string label,bool optimized){using var budget=new Budget162(label,"certificate");var initial=Program158.Initial(Program161.Spec);string? error=null;Contingency151.Review151? review=null;object? statistics=null;
  try{if(optimized){var p=new Planner162(Program161.Spec,false,new(Enabled:true,MaximumRoots:3),budget.Guard,settings:new(Enabled:true,Adaptive:true));p.Choose(initial.Input,new(omega:initial.CrankOmega));review=p.Last;statistics=p.Generator.ResponseStatistics;}else{var p=new Planner161(Program161.Spec,false,new(Enabled:true,MaximumRoots:3),budget.Guard,settings:new(Enabled:true,Adaptive:true));p.Choose(initial.Input,new(omega:initial.CrankOmega));review=p.Last;}}
  catch(Exception e){error=e.ToString();}
  Save("certificates/"+label+".json.gz",new{Label=label,Optimized=optimized,PolicyHash=PolicyHash(),Error=error,Initial=initial,Review=review is null?null:Evidence152.Compact(review),ResponseStatistics=statistics,CpuSeconds=budget.Elapsed});budget.Finish(error??"full13/9 independent from rest");Console.WriteLine(label+" "+(error??"CERTIFIED")+" CPU="+budget.Elapsed);
 }
}
