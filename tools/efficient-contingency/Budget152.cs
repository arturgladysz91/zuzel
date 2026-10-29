using System.Diagnostics;
using System.Text.Json.Nodes;
namespace Efficient152;
public sealed class Budget152:IDisposable {
 readonly Process process=Process.GetCurrentProcess();readonly TimeSpan began;readonly JsonArray rows;readonly JsonObject row;readonly FileStream lease;readonly double cap;bool done;
 static string Path=>Program152.Doc+"/budget.json";
 public double Elapsed=>(process.TotalProcessorTime-began).TotalSeconds;
 public double RemainingAtStart{get;}
 public Budget152(string name,int worlds,double maximumCpu,bool export=false){Directory.CreateDirectory(Program152.Doc);lease=new(Path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);rows=File.Exists(Path)?JsonNode.Parse(File.ReadAllText(Path))!.AsArray():new();
  if(rows.Any(r=>!r!["Complete"]!.GetValue<bool>())||rows.Any(r=>r!["Name"]!.GetValue<string>()==name))throw new IOException("Preserve and inspect Study152 reservations");
  if(worlds<0||rows.Sum(r=>r!["NativeWorlds"]!.GetValue<int>())+worlds>15||export&&worlds!=0)throw new IOException("Study152 native-world cap");
  RemainingAtStart=(export?1198:1160)-rows.Sum(r=>r!["CpuSeconds"]!.GetValue<double>());cap=Math.Min(maximumCpu,RemainingAtStart);if(cap<=0)throw new IOException("Study152 CPU cap; reserve40 CPU-s for final evidence");
  began=process.TotalProcessorTime;row=new(){["Name"]=name,["Complete"]=false,["NativeWorlds"]=worlds,["CpuSeconds"]=0,["Base"]=Program152.Base,["CapCpuSeconds"]=cap};rows.Add(row);Save();
 }
 void Save()=>File.WriteAllText(Path,rows.ToJsonString(Program152.Json));
 public void Guard(){if(Elapsed>=cap)throw new InvalidOperationException("STUDY152_CPU_STOP");}
 public void Finish(string outcome){if(done)return;row["Complete"]=true;row["CpuSeconds"]=Elapsed;row["Outcome"]=outcome;Save();done=true;}
 public void Dispose(){if(!done)Finish("exception; preserve negative attempt");lease.Dispose();}
}
