using System.Diagnostics;
namespace Efficient147;
/// <summary>Exclusive partition of measured process CPU ticks. Inclusive roles are a separate partition.</summary>
public sealed class CpuProfile147{
    readonly Process process=Process.GetCurrentProcess();readonly Dictionary<string,long> phases=new(),roles=new();
    string phase="outside_decision",role="dispatch";long last;public long StartTicks {get;}
    public CpuProfile147(){StartTicks=last=process.TotalProcessorTime.Ticks;}
    void Accrue(){var now=process.TotalProcessorTime.Ticks;var dt=now-last;if(dt<0)throw new InvalidOperationException("Nonmonotonic process CPU");phases[phase]=phases.GetValueOrDefault(phase)+dt;roles[role]=roles.GetValueOrDefault(role)+dt;last=now;}
    public IDisposable Phase(string name){Accrue();var old=phase;phase=name;return new Scope(()=>{Accrue();phase=old;});}
    public IDisposable Operation(string name){Accrue();var old=role;role=name;return new Scope(()=>{Accrue();role=old;});}
    public object Snapshot(){Accrue();var total=phases.Values.Sum();object[] Table(Dictionary<string,long> rows)=>rows.OrderByDescending(x=>x.Value).Select(x=>(object)new{Name=x.Key,CpuSeconds=x.Value/(double)TimeSpan.TicksPerSecond,Percent=total==0?0:100d*x.Value/total}).ToArray();
        return new{MeasuredCpuSeconds=total/(double)TimeSpan.TicksPerSecond,Phases=Table(phases),Operations=Table(roles),Method="Exclusive process-CPU tick partition at phase switches. Operations are a separate partition, not additive to phases. Counter granularity and instrumentation overhead are retained; percentages are measured instrumented attribution, not estimates of uninstrumented cost."};}
    sealed class Scope(Action close):IDisposable{bool done;public void Dispose(){if(!done){done=true;close();}}}
}
