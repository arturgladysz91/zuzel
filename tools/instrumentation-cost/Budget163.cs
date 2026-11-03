using System.Diagnostics;
using System.Text.Json;
using LineThrottle156;
namespace Instrumentation163;
public sealed class Budget163:IDisposable {
 readonly Process process=Process.GetCurrentProcess();readonly double start,cap;readonly string label;bool done;
 public double Elapsed=>process.TotalProcessorTime.TotalSeconds-start;
 public Budget163(string label){this.label=label;start=process.TotalProcessorTime.TotalSeconds;var plan=Study156.Read<JsonElement>(Path.Combine(Program163.Doc,"budget-plan.json"));var rows=Directory.Exists(Path.Combine(Program163.Doc,"micro"))?Directory.GetFiles(Path.Combine(Program163.Doc,"micro"),"*.json.gz"):Array.Empty<string>();if(File.Exists(Path.Combine(Program163.Doc,"cost-"+label+".json"))||rows.Length>=3||File.Exists(Path.Combine(Program163.Doc,"reservation.json")))throw new InvalidOperationException("Preserve microbenchmark quota/unfinished reservation");if(!plan.GetProperty("Cases").EnumerateArray().Any(x=>x.GetProperty("Label").GetString()==label))throw new ArgumentException("Declared microbenchmark label required");cap=plan.GetProperty("CpuCapPerMicrobenchmarkSeconds").GetDouble();Program163.Save("reservation.json",new{Label=label,ProcessId=process.Id,Cap=cap});}
 public void Guard(){if(Elapsed>=cap)throw new InvalidOperationException("STUDY163_CPU_STOP");}
 public void Finish(string reason){if(done)return;Program163.Save("cost-"+label+".json",new{Label=label,CpuSeconds=Elapsed,Reason=reason});File.Move(Path.Combine(Program163.Doc,"reservation.json"),Path.Combine(Program163.Doc,"reservation-"+label+".json"));done=true;}
 public void Dispose()=>Finish("operation ended; all evidence retained");
}
