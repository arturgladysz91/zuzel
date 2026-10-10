using System.Collections;
using System.Reflection;
using System.Text.Json;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Race;

Directory.CreateDirectory(args[0]);
if(args.Contains("--yield-probe",StringComparer.Ordinal))
{
    var rows=new List<object>();
    foreach(var (scenarioId,seed,weather) in new[]{("B",83,WeatherState.Dry),("B",83,WeatherState.LightRain),("G",7,WeatherState.Dry)})
    {
        var scenario=PhysicalContactConsequenceEvidence.HeatScenarios().Single(s=>s.Id==scenarioId);
        var observer=new YieldProbeObserver();
        var result=new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track,scenario.CreateSurface(),
            scenario.Riders.Select(r=>r.Create(scenario.Track)).ToList(),
            PhysicalContactConsequenceEvidence.Options with{Seed=seed,Laps=4,Weather=weather,IncidentFrequency=1,
                EnablePhysicalContactConsequences=false,PhysicalContactDiagnostics=PhysicalContactDiagnosticsLevel.None,
                InteractionDiagnostics=InteractionDiagnosticsLevel.FullAudit},59,observer);
        rows.Add(new{scenario.Id,Seed=seed,Weather=weather.Condition,result.Classification,Steps=observer.Steps});
    }
    File.WriteAllText(Path.Combine(args[0],"minimal-yield-native-b-probe.json"),
        JsonSerializer.Serialize(rows,PhysicalContactEvidence.JsonOptions)+"\n");
    Console.WriteLine("Three native-B target heat probes written.");
    return;
}
if(args.Contains("--performance",StringComparer.Ordinal))
{
    File.WriteAllText(Path.Combine(args[0],"physical-contact-consequence-performance.json"),
        JsonSerializer.Serialize(PhysicalContactConsequenceEvidence.Performance(),PhysicalContactEvidence.JsonOptions)+"\n");
    return;
}
var trace=args.Contains("--trace",StringComparer.Ordinal);
var minimalYield=args.Contains("--minimal-yield",StringComparer.Ordinal);
var targetWork=new SortedDictionary<string,int>(StringComparer.Ordinal);
var report=trace?PhysicalContactConsequenceEvidence.Trace()
    :PhysicalContactConsequenceEvidence.Report(!args.Contains("--controlled",StringComparer.Ordinal),Console.WriteLine,
        minimalYield?(name,trials)=>targetWork.Add(name,trials):null);
var json=JsonSerializer.Serialize(report,PhysicalContactEvidence.JsonOptions).Replace("\r\n","\n",StringComparison.Ordinal)+"\n";
File.WriteAllText(Path.Combine(args[0],trace?"physical-contact-arithmetic.json":"physical-contact-consequences.json"),json);
var bits=new SortedDictionary<string,object>(StringComparer.Ordinal);Walk(report,"Report",bits);
File.WriteAllText(Path.Combine(args[0],trace?"physical-contact-arithmetic-bits.json":"physical-contact-consequence-bits.json"),
    JsonSerializer.Serialize(bits,PhysicalContactEvidence.JsonOptions).Replace("\r\n","\n",StringComparison.Ordinal)+"\n");
Console.WriteLine("#56C2 consequence evidence and typed IEEE bits written.");
if(minimalYield&&!trace)
    File.WriteAllText(Path.Combine(args[0],"minimal-yield-target-work.json"),
        JsonSerializer.Serialize(targetWork,PhysicalContactEvidence.JsonOptions)+"\n");
static void Walk(object? value,string path,IDictionary<string,object> bits)
{
    if(value is float single){bits[path]=new{Type="float",Bits=$"0x{BitConverter.SingleToInt32Bits(single):X8}"};return;}
    if(value is double number){bits[path]=new{Type="double",Bits=$"0x{BitConverter.DoubleToInt64Bits(number):X16}"};return;}
    if(value is null||value is string||value.GetType().IsPrimitive||value.GetType().IsEnum)return;
    if(value is IEnumerable sequence){var index=0;foreach(var item in sequence)Walk(item,$"{path}[{index++}]",bits);return;}
    foreach(var property in value.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.GetIndexParameters().Length==0))
        Walk(property.GetValue(value),path+"."+property.Name,bits);
}

sealed class YieldProbeObserver : ISimulationStepObserver
{
    public List<object> Steps {get;}=new();
    public void OnStepResolved(ResolvedSimulationStep step)
        => Steps.Add(new{step.Changes,step.Motions,step.Events,
            Targets=step.Interaction?.Episodes.SelectMany(e=>e.SelectedResponses).ToArray()});
}
