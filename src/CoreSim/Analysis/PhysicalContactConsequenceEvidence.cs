using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using System.Text.Json;
using System.Diagnostics;

namespace CoreSim.Analysis;

/// <summary>Offline evidence using the same consequence plan and production traversal as a heat.</summary>
public static class PhysicalContactConsequenceEvidence
{
    public static HeatSimulationOptions Options => new() { EnableContestedSpaceResponses = true,
        EnablePhysicalContactConsequences = true, IncidentFrequency = 0, EnableLogging = false,
        PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit };

    public static SimulationSnapshot RecoverySnapshot(ContactRecoveryState? recovery = null, int target = 2)
    {
        var track = new Track(new[] { new TrackSegment(0, SegmentType.Straight, 20) }, TrackGeometry.Default);
        var rider = new RiderSnapshot(1, RiderProfile.CreateDefault(1), RiderPosition.Start(1), 0,
            target, target, 15, 0, RiderRaceStatus.Racing, 0, Setup.BikeSetup.Neutral, .5f, .5f)
            { ContactRecovery = recovery };
        return new(new(59,0,0,0,7,4),track,new TrackState(1,5,(_,_)=>new(1,0,0)).Snapshot(),new[]{rider});
    }

    public static PhysicalContactConsequencePlan Plan(PhysicalContactFixture fixture, double? controlledRatio = null)
    {
        var inputs = fixture.Inputs;
        var analysis = fixture.Analysis;
        var template = RecoverySnapshot();
        var ids = inputs.SelectMany(i=>new[]{i.RiderA.RiderId,i.RiderB.RiderId}).Distinct().Order().ToArray();
        var snapshot = new SimulationSnapshot(template.Step,template.Track,template.TrackState,
            ids.Select(id=>template.Rider(1) with{RiderId=id,Profile=RiderProfile.CreateDefault(id)}));
        var engine = new SimulationEngine(new FixedDecision(2));
        var normal = engine.ResolveProduction(snapshot,engine.Decide(snapshot),Options,legacyContacts:false);
        var directions = ids.ToDictionary(id=>id,id=>
        {
            var input = inputs.Where(i=>i.RiderA.RiderId==id||i.RiderB.RiderId==id)
                .OrderBy(i=>i.Contact.FirstTouchCommonTimeSeconds).First();
            var heading = (input.RiderA.RiderId==id?input.PoseA:input.PoseB)?.Attitude.TravelHeadingRadians ?? 0;
            var pose=input.RiderA.RiderId==id?input.PoseA:input.PoseB;
            return ContactFrameArithmetic.Direction(heading,pose?.DeterministicArithmetic??false);
        });
        var riders = analysis.AuditRiders.Select(r=>controlledRatio.HasValue?r with{SeverityRatio=controlledRatio.Value}:r).ToArray();
        return PhysicalContactConsequenceResolver.Build(analysis,riders,
            analysis.AuditPairs.Where(p=>p.Status==PhysicalContactStatus.Analyzed).ToArray(),snapshot,
            normal.Changes,directions,new(),new());
    }

    public static object[] Boundaries() => new[]{.35,.70,1.05,1.55}.SelectMany(boundary=>new[]{
        boundary*.9,Math.BitDecrement(boundary),boundary,Math.BitIncrement(boundary),boundary*1.1}
        .Select(ratio=>(object)new { Boundary=boundary,Ratio=ratio,
            Applied=Plan(PhysicalContactEvidence.Side("controlled-boundary"),ratio).Riders[0] })).ToArray();

    public static object[] RecoveryRuns() => new[]{0d,.5,.9,1.3}.Select(ratio=>
    {
        var consequence=Plan(PhysicalContactEvidence.Side("recovery-control"),ratio).Riders[0];
        var engine=new SimulationEngine(new FixedDecision(3));
        var snapshot=RecoverySnapshot(consequence.Recovery);
        var step=engine.ResolveProduction(snapshot,engine.Decide(snapshot),Options,legacyContacts:false);
        return (object)new {Ratio=ratio,Consequence=consequence,Final=step.Changes[0],
            Motion=step.Motions[0],Diagnostics=step.Diagnostics[0],RecoveryAfter=step.Changes[0].ContactRecovery};
    }).ToArray();

    public static object Report(bool includeHeats = true, Action<string>? progress = null)
    {
        var fixtures=PhysicalContactEvidence.Fixtures();
        var controlled=fixtures.Select(f=>new {f.Name,Analysis=f.Analysis,Applied=Plan(f)}).ToArray();
        var scenarios=ContestedSpaceResponseEvidence.OwnershipScenarios()
            .SelectMany(s=>new[]{7,19,57}.Select(seed=>s with{Seed=seed,IncidentFrequency=2})).Select(s=>
        {
            var snapshot=ContestedSpaceResponseEvidence.Snapshot(s);
            var engine=new SimulationEngine(new FixedDecision(2));
            var intents=s.Riders.Select(r=>new RiderIntent(r.Id,new(r.Intent.TargetFor(snapshot.Segment.Type)){Trajectory=r.Intent})).ToArray();
            var applied=engine.Resolve(snapshot,intents,Options with{Seed=s.Seed,IncidentFrequency=s.IncidentFrequency});
            var legacy=ContestedSpaceResponseEvidence.Resolve(s);
            return new{s.Name,s.Seed,LegacyChanges=legacy.Changes,LegacyEvents=legacy.Events,AppliedChanges=applied.Changes,
                AppliedEvents=applied.Events.Select(e=>new{e.Type,e.RiderId,e.PhysicalContactConsequence}),
                Analysis=applied.Interaction?.PhysicalContactAnalysis,Plan=applied.Interaction?.PhysicalContactConsequences};
        }).ToArray();
        var heats=new List<ContactConsequenceHeatEvidence>();
        if(includeHeats) foreach(var scenario in HeatScenarios())
            foreach(var seed in new[]{7,19,57,83}) foreach(var weather in new[]{WeatherState.Dry,WeatherState.LightRain})
            {
                var heat=Heat(scenario,seed,weather);heats.Add(heat);
                progress?.Invoke($"#56C2 {scenario.Id} seed={seed} weather={weather.Condition}: contacts={heat.VerifiedContacts} applied={heat.AppliedRiders}");
            }
        return new{Schema="56C2-v1",Thresholds=new PhysicalContactParameters(),
            RecoveryMapping=new PhysicalContactConsequenceParameters(),Controlled=controlled,Boundaries=Boundaries(),
            Recovery=RecoveryRuns(),LegacyComparison=scenarios,Heats=heats,
            Totals=new{Heats=heats.Count,VerifiedContacts=heats.Sum(h=>h.VerifiedContacts),AppliedRiders=heats.Sum(h=>h.AppliedRiders),
                AppliedContacts=heats.Sum(h=>h.AppliedContacts),ContactsPerHeat=heats.Count==0?0:heats.Sum(h=>h.VerifiedContacts)/(double)heats.Count,
                ContactCrashes=heats.Sum(h=>h.ContactCrashes),RepeatedOverlapSuppressions=heats.Sum(h=>h.RepeatedOverlapSuppressions),
                Deferred=heats.Sum(h=>h.Deferred),GeometryUnresolved=heats.Sum(h=>h.GeometryUnresolved),
                Classes=Enum.GetValues<PhysicalContactSeverity>().ToDictionary(c=>c.ToString(),c=>heats.Sum(h=>h.Classes.GetValueOrDefault(c.ToString()))) }};

    }

    public static IReadOnlyList<BehaviorScenario> HeatScenarios()
    {
        var scenarios=FourRiderBehaviorSuite.CreateScenarios();var basis=scenarios[0];
        var contactHeavy=basis with{Id="contact-heavy",Description="Deliberately overlapping rear-closing four-rider stress control",
            Riders=basis.Riders.Select((r,i)=>r with{Id=i+1,Lane=2,SpeedMetersPerSecond=18+3*i,
                SegmentProgress=.09375f-.03125f*i,ArrivalOffsetSeconds=0}).ToArray()};
        return scenarios.Append(contactHeavy).ToArray();
    }

    public static object Trace()
    {
        var scenario=HeatScenarios().Single(s=>s.Id=="F");
        var observer=new ContactObserver(captureInputs:true);
        new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track,scenario.CreateSurface(),
            scenario.Riders.Select(r=>r.Create(scenario.Track)).ToList(),
            Options with{Seed=83,Weather=WeatherState.LightRain,IncidentFrequency=1},59,observer);
        return observer.Trace;
    }

    public static object Performance()
    {
        var rows=new List<object>();
        // Isolate the cross-platform geometry kernel from the existing search.
        // This is observational evidence, never a wall-clock assertion.
        foreach(var deterministic in new[]{false,true})
        {
            var segment=new MetricTrackSegment(0,0,SegmentType.TurnEntry,new(1,2),.3,40,25,Math.PI/3)
                {DeterministicArithmetic=deterministic};
            var phase=0;
            Measure("contact-frame-map-"+deterministic,
                ()=>GC.KeepAlive(segment.Map((++phase%1024)/1024d,3,.2,.1)),100000,10000);
            Measure("contact-footprint-"+deterministic,
                ()=>GC.KeepAlive(BikeFootprint.Create(new(1,2),(++phase%1024)/1024d*Math.PI,
                    SpeedwayBikeDimensions.Reference,deterministic)),100000,10000);
        }
        foreach(var name in new[]{"B-moderate-side","M-three-squeeze","N-four-frontier"})
        {
            var fixture=PhysicalContactEvidence.Fixtures().Single(f=>f.Name==name);
            var template=RecoverySnapshot();var analysis=fixture.Analysis;
            var ids=analysis.AuditRiders.Select(r=>r.RiderId).ToArray();
            var snapshot=new SimulationSnapshot(template.Step,template.Track,template.TrackState,
                ids.Select(id=>template.Rider(1) with{RiderId=id,Profile=RiderProfile.CreateDefault(id)}));
            var engine=new SimulationEngine(new FixedDecision(2));
            var normal=engine.ResolveProduction(snapshot,engine.Decide(snapshot),Options,legacyContacts:false);
            var directions=ids.ToDictionary(id=>id,_=>new MeterPoint(1,0));
            var scale=new PhysicalContactParameters();var parameters=new PhysicalContactConsequenceParameters();
            PhysicalContactConsequencePlan Build()=>PhysicalContactConsequenceResolver.Build(analysis,analysis.AuditRiders,
                analysis.AuditPairs,snapshot,normal.Changes,directions,scale,parameters);
            var plan=Build();
            Measure(name+"-plan",()=>GC.KeepAlive(Build()),2000);
            Measure(name+"-apply",()=>{foreach(var c in plan.Riders)GC.KeepAlive(PhysicalContactConsequenceResolver.Apply(normal.Changes.Single(n=>n.RiderId==c.RiderId),c));},2000);
        }
        var production=new SimulationEngine(new FixedDecision(3));
        foreach(var ratio in new[]{0d,.5,.9,1.3})
        {
            var snapshot=RecoverySnapshot(Plan(PhysicalContactEvidence.Side("performance"),ratio).Riders[0].Recovery);
            Measure("recovery-production-"+ratio.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ()=>GC.KeepAlive(SimulationEngine.ResolveSoloProjection(snapshot,snapshot.Rider(1),new(3),Options)),500);
        }
        // Reset ownership for every sample: these measure the actual contact-heavy
        // coordinator, including the existing #56B search and #56C1 analysis.
        foreach(var scenario in ContestedSpaceResponseEvidence.OwnershipScenarios())
        {
            var snapshot=ContestedSpaceResponseEvidence.Snapshot(scenario);
            var engine=new SimulationEngine(new FixedDecision(2));
            var intents=scenario.Riders.Select(r=>new RiderIntent(r.Id,
                new(r.Intent.TargetFor(snapshot.Segment.Type)){Trajectory=r.Intent})).ToArray();
            foreach(var enabled in new[]{false,true})
                Measure(scenario.Name+"-contact-step-"+enabled,
                    ()=>GC.KeepAlive(engine.Resolve(snapshot,intents,Options with{EnablePhysicalContactConsequences=enabled})),30,5);
        }
        foreach(var scenario in HeatScenarios().Where(s=>s.Id is "I" or "contact-heavy"))
        {
            var samples=new Dictionary<bool,List<(double Ms,long Bytes)>>{{false,new()},{true,new()}};
            for(var iteration=0;iteration<5;iteration++) foreach(var enabled in new[]{false,true})
            {
                var riders=scenario.Riders.Select(r=>r.Create(scenario.Track)).ToList();var state=scenario.CreateSurface();
                var bytes=GC.GetTotalAllocatedBytes(true);var clock=Stopwatch.StartNew();
                new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track,state,riders,
                    Options with{Seed=7,IncidentFrequency=1,EnablePhysicalContactConsequences=enabled},58);
                clock.Stop();if(iteration>=2)samples[enabled].Add((clock.Elapsed.TotalMilliseconds,GC.GetTotalAllocatedBytes(true)-bytes));
            }
            foreach(var enabled in new[]{false,true})rows.Add(new{Name=scenario.Id+"-full-heat",Enabled=enabled,Iterations=3,
                MedianMilliseconds=samples[enabled].Select(s=>s.Ms).Order().ElementAt(1),
                MedianAllocatedBytes=samples[enabled].Select(s=>s.Bytes).Order().ElementAt(1)});
        }
        return rows;

        void Measure(string name,Action action,int iterations,int warmups=100)
        {
            for(var warm=0;warm<warmups;warm++)action();
            var bytes=GC.GetAllocatedBytesForCurrentThread();var clock=Stopwatch.StartNew();
            for(var iteration=0;iteration<iterations;iteration++)action();clock.Stop();
            rows.Add(new{Name=name,Iterations=iterations,Microseconds=clock.Elapsed.TotalMilliseconds*1000/iterations,
                AllocatedBytes=(GC.GetAllocatedBytesForCurrentThread()-bytes)/(double)iterations});
        }
    }

    private static ContactConsequenceHeatEvidence Heat(BehaviorScenario scenario,int seed,WeatherState weather)
    {
        var observer=new ContactObserver();var riders=scenario.Riders.Select(r=>r.Create(scenario.Track)).ToList();
        var result=new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track,scenario.CreateSurface(),riders,
            Options with{Seed=seed,Laps=4,Weather=weather,IncidentFrequency=1},59,observer);
        var legacyRiders=scenario.Riders.Select(r=>r.Create(scenario.Track)).ToList();
        var legacy=new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(scenario.Track,scenario.CreateSurface(),legacyRiders,
            Options with{Seed=seed,Laps=4,Weather=weather,IncidentFrequency=1,EnablePhysicalContactConsequences=false,
                PhysicalContactDiagnostics=PhysicalContactDiagnosticsLevel.None},59);
        return new(scenario.Id,seed,weather.Condition.ToString(),observer.Verified,observer.AppliedContacts,observer.Applied.Count,
            observer.Applied.Count(c=>c.Severity==PhysicalContactSeverity.Crash),observer.Suppressed,observer.Deferred,observer.Unresolved,
            Enum.GetValues<PhysicalContactSeverity>().ToDictionary(c=>c.ToString(),c=>observer.Applied.Count(a=>a.Severity==c)),
            result.Classification,legacy.Classification,observer.Applied);
    }
    private sealed class ContactObserver(bool captureInputs=false) : ISimulationStepObserver
    {
        public int Verified,AppliedContacts,Suppressed,Deferred,Unresolved;
        public List<RiderContactConsequence> Applied {get;}=new();
        public List<object> Trace {get;}=new();
        public void OnStepResolved(ResolvedSimulationStep step)
        {
            var analysis=step.Interaction?.PhysicalContactAnalysis;
            if(captureInputs&&analysis is{AuditPairs.Count:>0})
                Trace.Add(new{step.Snapshot.Step,analysis.SourceInputs,Analysis=analysis,
                    Applied=step.Interaction?.PhysicalContactConsequences});
            Verified+=analysis?.Pairs.Count??0;
            Deferred+=analysis?.Work.DeferredContacts??0;
            Unresolved+=analysis?.Pairs.Count(p=>p.Status==PhysicalContactStatus.GeometryUnresolved)??0;
            if(step.Interaction?.PhysicalContactConsequences is not{} plan)return;
            AppliedContacts+=plan.AppliedPairs.Count;Suppressed+=plan.RepeatedOverlapSuppressions;Applied.AddRange(plan.Riders);
        }
    }
    private sealed class FixedDecision(int target) : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment, RiderState rider)=>new(target); }
}

public sealed record ContactConsequenceHeatEvidence(string Scenario,int Seed,string Weather,int VerifiedContacts,
    int AppliedContacts,int AppliedRiders,int ContactCrashes,int RepeatedOverlapSuppressions,int Deferred,int GeometryUnresolved,
    IReadOnlyDictionary<string,int> Classes,IReadOnlyList<RiderHeatResult> Classification,
    IReadOnlyList<RiderHeatResult> LegacyClassification,IReadOnlyList<RiderContactConsequence> Applied);
