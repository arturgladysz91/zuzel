using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Race;

Directory.CreateDirectory(args[0]);
if (args.Contains("--performance",StringComparer.Ordinal))
{
    var rows=new List<object>();
    foreach(var name in new[]{"B-moderate-side","M-three-squeeze","N-four-frontier"})
    foreach(var level in new[]{PhysicalContactDiagnosticsLevel.None,PhysicalContactDiagnosticsLevel.Summary,PhysicalContactDiagnosticsLevel.FullAudit})
    {
        var fixture=PhysicalContactEvidence.Fixtures().Single(f=>f.Name==name);
        for(var warm=0;warm<100;warm++) PhysicalContactAnalyzer.Analyze(fixture.Inputs,level:level);
        var startBytes=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();
        for(var iteration=0;iteration<2000;iteration++) PhysicalContactAnalyzer.Analyze(fixture.Inputs,level:level);
        sw.Stop();rows.Add(new{Name=name,Level=level.ToString(),Iterations=2000,MicrosecondsPerAnalysis=sw.Elapsed.TotalMilliseconds*1000/2000,
            AllocatedBytesPerAnalysis=(GC.GetAllocatedBytesForCurrentThread()-startBytes)/2000d});
    }
    var scenario=FourRiderBehaviorSuite.CreateScenarios().Single(s=>s.Id=="I");
    var heatLevels=new[]{PhysicalContactDiagnosticsLevel.None,PhysicalContactDiagnosticsLevel.Summary};
    var heatTimings=heatLevels.ToDictionary(level=>level,_=>new List<double>());
    var heatAllocations=heatLevels.ToDictionary(level=>level,_=>new List<long>());
    for(var iteration=0;iteration<5;iteration++)
    foreach(var level in heatLevels)
    {
            var riders=scenario.Riders.Select(r=>r.Create(scenario.Track)).ToList();var surface=scenario.CreateSurface();
            var startBytes=GC.GetTotalAllocatedBytes(precise:true);var sw=Stopwatch.StartNew();
            new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track,surface,riders,
                new(){Laps=4,Seed=7,EnableContestedSpaceResponses=true,PhysicalContactDiagnostics=level},58);
            sw.Stop();if(iteration>=2){heatTimings[level].Add(sw.Elapsed.TotalMilliseconds);heatAllocations[level].Add(GC.GetTotalAllocatedBytes(precise:true)-startBytes);}
    }
    foreach(var level in heatLevels)
    {
        rows.Add(new{Name="I-four-lap-production-heat",Level=level.ToString(),Iterations=3,
            MedianMilliseconds=heatTimings[level].Order().ElementAt(1),MedianAllocatedBytes=heatAllocations[level].Order().ElementAt(1)});
    }
    File.WriteAllText(Path.Combine(args[0],"physical-contact-performance.json"),JsonSerializer.Serialize(rows,PhysicalContactEvidence.JsonOptions)+"\n");
    return;
}
var json=PhysicalContactEvidence.DeterministicJson().Replace("\r\n","\n",StringComparison.Ordinal);
File.WriteAllText(Path.Combine(args[0],"physical-contact-analysis.json"),json);
var bits=new SortedDictionary<string,object>(StringComparer.Ordinal);
Walk(PhysicalContactEvidence.Fixtures(),"Controlled",bits);
Walk(PhysicalContactEvidence.Grid(),"Grid",bits);
File.WriteAllText(Path.Combine(args[0],"physical-contact-bits.json"),
    JsonSerializer.Serialize(bits,PhysicalContactEvidence.JsonOptions).Replace("\r\n","\n",StringComparison.Ordinal)+"\n");
Console.WriteLine("#56C1 controlled fixtures, 4,374 raw grid rows, legacy comparison and typed IEEE bits written.");
if(args.Contains("--minimal-yield",StringComparer.Ordinal))
{
    var trials=ContestedSpaceResponseEvidence.OwnershipScenarios()
        .Concat(ContestedSpaceResponseEvidence.Scenarios().Where(s=>s.Name.StartsWith("I-",StringComparison.Ordinal)))
        .SelectMany(s=>new[]{7,19,57}.Select(seed=>s with{Seed=seed,IncidentFrequency=2}))
        .ToDictionary(s=>$"LegacyComparisons/{s.Name}/{s.Seed}",
            s=>PhysicalContactEvidence.Resolve(s,PhysicalContactDiagnosticsLevel.FullAudit).Interaction?.Work.OutwardTargetTrials??0);
    File.WriteAllText(Path.Combine(args[0],"minimal-yield-target-work.json"),
        JsonSerializer.Serialize(trials,PhysicalContactEvidence.JsonOptions)+"\n");
}
static void Walk(object? value,string path,IDictionary<string,object> bits)
{
    if(value is float single){bits[path]=new{Type="float",Bits=$"0x{BitConverter.SingleToInt32Bits(single):X8}"};return;}
    if(value is double number){bits[path]=new{Type="double",Bits=$"0x{BitConverter.DoubleToInt64Bits(number):X16}"};return;}
    if(value is null||value is string||value.GetType().IsPrimitive||value.GetType().IsEnum)return;
    if(value is IEnumerable sequence){var index=0;foreach(var item in sequence)Walk(item,$"{path}[{index++}]",bits);return;}
    foreach(var property in value.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.GetIndexParameters().Length==0))
        Walk(property.GetValue(value),path+"."+property.Name,bits);
}
