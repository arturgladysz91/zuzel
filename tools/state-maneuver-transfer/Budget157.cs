using System.Diagnostics;
using System.Text.Json.Nodes;
namespace Transfer157;
public sealed class Budget157:IDisposable {
 readonly Process process=Process.GetCurrentProcess();readonly JsonArray rows;readonly JsonObject row;readonly FileStream lease;readonly double cap;bool done;
 string Path=>Program157.Doc+"/budget.json";
 // Charge startup, JSON input and output too, not only the timed planner loop.
 public double Elapsed=>process.TotalProcessorTime.TotalSeconds;
 public Budget157(string name,int worlds,double maximumCpu){Directory.CreateDirectory(Program157.Doc);lease=new(Path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);rows=File.Exists(Path)?JsonNode.Parse(File.ReadAllText(Path))!.AsArray():new();
  if(rows.Any(r=>!r!["Complete"]!.GetValue<bool>())||rows.Any(r=>r!["Name"]!.GetValue<string>()==name))throw new IOException("Inspect unfinished/duplicate Study157 reservation");
  if(worlds<0||rows.Sum(r=>r!["NativeWorlds"]!.GetValue<int>())+worlds>15)throw new IOException("Study157 native cap15");cap=Math.Min(maximumCpu,950-rows.Sum(r=>r!["CpuSeconds"]!.GetValue<double>()));if(cap<=0)throw new IOException("Study157 cap1000 CPU-s, final50 reserved");
  row=new(){["Name"]=name,["Complete"]=false,["NativeWorlds"]=worlds,["CpuSeconds"]=0,["Base"]=Program157.Base,["CapCpuSeconds"]=cap};rows.Add(row);Save();
 }
 void Save()=>File.WriteAllText(Path,rows.ToJsonString(Program157.Json));
 public void Guard(){if(Elapsed>=cap)throw new InvalidOperationException("STUDY157_CPU_STOP");}
 public void Finish(string outcome){if(done)return;row["Complete"]=true;row["Outcome"]=outcome;row["CpuSeconds"]=Elapsed+1;Save();done=true;}
 public void Dispose(){if(!done)Finish("exception; negative attempt retained");lease.Dispose();}
}
