using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharedWorldSpeedway;
namespace Limits149;
public sealed class Budget149:IDisposable{
    const string Path="docs/experiments/tyre-physics-corner-limits/budget.json";
    readonly Process process=Process.GetCurrentProcess();readonly TimeSpan start;readonly JsonArray ledger;readonly JsonObject row;
    readonly double cap;readonly FileStream lease;bool complete;
    public double SpentBefore {get;}public double Elapsed=>(process.TotalProcessorTime-start).TotalSeconds;
    public Budget149(string name,int nativeWorlds,double maximumCpu){
        lease=new FileStream(Path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        ledger=JsonNode.Parse(File.ReadAllText(Path))!.AsArray();if(ledger.Any(x=>!x!["Complete"]!.GetValue<bool>()))throw new IOException("Inspect incomplete Study149 reservation.");
        if(ledger.Any(x=>x!["Name"]!.GetValue<string>()==name))throw new IOException("Preserve existing attempt "+name);
        SpentBefore=ledger.Sum(x=>x!["CpuSeconds"]!.GetValue<double>());cap=Math.Min(maximumCpu,1180-SpentBefore);if(cap<=0)throw new IOException("Study149 1200 CPU-s reserve exhausted.");
        start=process.TotalProcessorTime;row=new(){["Name"]=name,["Complete"]=false,["NativeWorlds"]=nativeWorlds,["CpuSeconds"]=0};ledger.Add(row);Save();
    }
    void Save()=>File.WriteAllText(Path,ledger.ToJsonString(FreeLearningExperiment.Json));
    public void Guard(){if(Elapsed>cap)throw new InvalidOperationException("STUDY149_GLOBAL_CPU_STOP");}
    public void Finish(string outcome){if(complete)return;row["Complete"]=true;row["CpuSeconds"]=Elapsed;row["Outcome"]=outcome;Save();complete=true;}
    public void Dispose(){if(!complete)Finish("exception; inspect saved diagnostics");lease.Dispose();}
}
