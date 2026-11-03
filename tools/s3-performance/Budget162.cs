using System.Diagnostics;
using System.Text.Json.Nodes;
using LineThrottle156;
namespace S3Performance162;
public sealed class Budget162:IDisposable {
 readonly string label,kind,directory;readonly Process process=Process.GetCurrentProcess();readonly double start,cap;bool done;
 string FileName=>Path.Combine(directory,"budget-journal.json");
 public double Elapsed=>process.TotalProcessorTime.TotalSeconds-start;
 public Budget162(string label,string kind="diagnostic",bool concurrent=false,string? evidenceDirectory=null){
  if(kind is not ("diagnostic" or "certificate" or "native"))throw new ArgumentException("Declared research category required");
  this.label=label;this.kind=kind;directory=evidenceDirectory??Program162.Doc;start=process.TotalProcessorTime.TotalSeconds;
  if(!File.Exists(Path.Combine(directory,"budget-plan.json")))throw new IOException("Declare budget first");
  using var lease=Lease();
  var rows=Read();if(rows.Any(r=>r!["Label"]!.GetValue<string>()==label))throw new IOException("Preserve existing attempt");
  foreach(var active in rows.Where(r=>!r!["Complete"]!.GetValue<bool>())){
   if(!concurrent||kind!="native"||active!["Concurrent"]?.GetValue<bool>()!=true||active["Kind"]!.GetValue<string>()!="native")throw new IOException("Unfinished reservation; inspect owner before continuing");
   try{if(Process.GetProcessById(active["ProcessId"]!.GetValue<int>()).HasExited)throw new IOException("Unfinished owner exited; retain full reservation");}catch(ArgumentException){throw new IOException("Unfinished owner missing; retain full reservation");}
  }
  var plan=JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(directory,"budget-plan.json")))!;
  int count=rows.Count(r=>r!["Kind"]!.GetValue<string>()==kind);
  int limit=kind=="native"?6:plan[kind=="diagnostic"?"MaximumDiagnostics":"MaximumCertificateProbes"]!.GetValue<int>();
  double used=rows.Where(r=>r!["Kind"]!.GetValue<string>()==kind).Sum(r=>r!["ChargedCpuSeconds"]!.GetValue<double>());
  cap=kind=="diagnostic"?Math.Min(90,plan["MaximumDiagnosticCpuSeconds"]!.GetValue<double>()-used):kind=="certificate"?60:Math.Min(2400,14400-used);
  if(count>=limit||cap<=0)throw new InvalidOperationException("STUDY162_DECLARED_BUDGET_EXHAUSTED");
  rows.Add(new JsonObject{["Label"]=label,["Kind"]=kind,["Complete"]=false,["ChargedCpuSeconds"]=cap,["CpuCapSeconds"]=cap,["ProcessId"]=process.Id,["Concurrent"]=concurrent});Write(rows);
 }
 FileStream Lease(){for(int j=0;j<2000;j++){try{return new(FileName+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){Thread.Sleep(5);}}throw new IOException("Budget journal busy");}
 JsonArray Read()=>File.Exists(FileName)?JsonNode.Parse(File.ReadAllText(FileName))!.AsArray():new();
 void Write(JsonArray rows){File.WriteAllText(FileName+".next",rows.ToJsonString(Study156.Json));File.Move(FileName+".next",FileName,true);}
 public void Guard(){if(Elapsed>=cap)throw new InvalidOperationException("STUDY162_CPU_STOP");}
 public void Finish(string reason){if(done)return;using var lease=Lease();var rows=Read();var row=rows.Single(r=>r!["Label"]!.GetValue<string>()==label)!;row["Complete"]=true;row["CpuSeconds"]=Elapsed;row["ChargedCpuSeconds"]=Math.Ceiling(Elapsed+1);row["Reason"]=reason;Write(rows);done=true;}
 public void Dispose()=>Finish("attempt ended; negative evidence retained");
}
