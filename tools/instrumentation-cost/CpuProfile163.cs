using System.Diagnostics;
namespace Instrumentation163;
public enum ProfileMode163 { Fine, Aggregate, Disabled }
/// <summary>Measurement only. Hard budget clocks remain in the unchanged planner.
/// Aggregate owns one phase over each complete rollout; equal nested phases are noops.</summary>
public sealed class CpuProfile163 {
 readonly Process process=Process.GetCurrentProcess();readonly Func<long> clock;
 readonly Dictionary<string,long> phases=new(),roles=new(),rangeTicks=new(),rangeBytes=new();
 string phase="outside_decision",role="dispatch";long last;
 public ProfileMode163 Mode{get;}
 public long StartTicks{get;}
 public long ClockReads{get;private set;}
 public long PhaseRequests{get;private set;}
 public long SamePhaseNoops{get;private set;}
 public long ScopeAllocations{get;private set;}
 public static IDisposable EmptyScope{get;}=new Empty();
 sealed class Empty:IDisposable{public void Dispose(){}}
 public CpuProfile163(ProfileMode163 mode=ProfileMode163.Fine,Func<long>? clock=null){Mode=mode;this.clock=clock??(()=>process.TotalProcessorTime.Ticks);StartTicks=last=Now();}
 long Now(){ClockReads++;return clock();}
 void Accrue(){var now=Now();var dt=now-last;if(dt<0)throw new InvalidOperationException("Nonmonotonic process CPU");phases[phase]=phases.GetValueOrDefault(phase)+dt;roles[role]=roles.GetValueOrDefault(role)+dt;last=now;}
 public IDisposable Phase(string name){PhaseRequests++;if(Mode==ProfileMode163.Disabled)return EmptyScope;if(Mode==ProfileMode163.Aggregate&&name==phase){SamePhaseNoops++;return EmptyScope;}Accrue();var old=phase;phase=name;ScopeAllocations++;return new Scope(()=>{Accrue();phase=old;});}
 public IDisposable Operation(string name){if(Mode==ProfileMode163.Disabled)return EmptyScope;Accrue();var old=role;role=name;ScopeAllocations++;return new Scope(()=>{Accrue();role=old;});}
 /// <summary>Same outer range meter in every mode. Ranges overlap ordinary phase
 /// attribution and must not be summed with phase CPU.</summary>
 public IDisposable Continuation(string name){long began=Now(),allocated=GC.GetAllocatedBytesForCurrentThread();var lease=Mode==ProfileMode163.Aggregate?Phase(name):EmptyScope;return new Scope(()=>{lease.Dispose();long ticks=Now()-began;rangeTicks[name]=rangeTicks.GetValueOrDefault(name)+ticks;rangeBytes[name]=rangeBytes.GetValueOrDefault(name)+GC.GetAllocatedBytesForCurrentThread()-allocated;});}
 public object Ranges()=>new{CpuSeconds=rangeTicks.ToDictionary(p=>p.Key,p=>p.Value/(double)TimeSpan.TicksPerSecond),AllocatedBytesOnCurrentThread=rangeBytes,Scope="Whole actual Rollout calls, same outer meter in all modes. Includes unchanged hard budget checks, physical propagation, feedback and planner operations; not isolated tyre-kernel CPU."};
 public object Snapshot(){Accrue();var total=phases.Values.Sum();object[] Table(Dictionary<string,long> rows)=>rows.OrderByDescending(x=>x.Value).Select(x=>(object)new{Name=x.Key,CpuSeconds=x.Value/(double)TimeSpan.TicksPerSecond,Percent=total==0?0:100d*x.Value/total}).ToArray();return new{MeasuredCpuSeconds=total/(double)TimeSpan.TicksPerSecond,Phases=Table(phases),Operations=Table(roles),Method="Fine: original147 per-step attribution. Aggregate: whole-rollout ownership, same-phase noops; phase attribution changes, so compare common outer Ranges. Disabled: phase instrumentation off; ALL hard budget guards still active."};}
 sealed class Scope(Action close):IDisposable{bool done;public void Dispose(){if(!done){done=true;close();}}}
}
