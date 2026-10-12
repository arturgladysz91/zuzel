using System.Text.Json;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;

var rows = new List<object>();
var scenarios = ContestedSpaceResponseEvidence.Scenarios().ToList();
foreach (var count in new[] { 2, 3, 4 })
foreach (var segment in new[] { 1, 2, 4 })
{
    var scenario = new ContestedScenario($"attack-{count}-{segment}", segment,
        Enumerable.Range(1, count).Select(id => new ContestedRiderInput(id, 1 + (id - 1) * .6f,
            0, 21, id == 1 ? new(2,2,2) : new((int)MathF.Round(1 + (id - 1) * .6f),
                (int)MathF.Round(1 + (id - 1) * .6f), (int)MathF.Round(1 + (id - 1) * .6f)))).ToArray());
    scenarios.Add(scenario);
}
foreach (var physical in new[] { false, true })
foreach (var scenario in scenarios)
{
    var snapshot = ContestedSpaceResponseEvidence.Snapshot(scenario);
    var intents = scenario.Riders.Select(r=>new RiderIntent(r.Id,new RiderDecision(r.Intent.TargetFor(snapshot.Segment.Type)){Trajectory=r.Intent})).ToArray();
    var options = new HeatSimulationOptions {Seed=scenario.Seed, EnableContestedSpaceResponses=true,
        EnablePhysicalContactConsequences=physical, IncidentFrequency=scenario.IncidentFrequency,
        InteractionDiagnostics=InteractionDiagnosticsLevel.FullAudit};
    var step = new SimulationEngine(new Hold()).Resolve(snapshot,intents,options);
    var reverse = new SimulationEngine(new Hold()).Resolve(ContestedSpaceResponseEvidence.Snapshot(scenario,true),intents.Reverse().ToArray(),options);
    string Capture(ResolvedSimulationStep value)=>JsonSerializer.Serialize(new{value.Changes,value.Diagnostics,value.Motions,value.Events,value.Interaction});
    if (Capture(step)!=Capture(reverse)) throw new InvalidOperationException("Input order regression: "+scenario.Name);

    var embedding = new TrackMetricEmbedding(step.Snapshot.Track,physical);
    var clearance = CommonTimePoseHistory.Observe(step.Motions.SelectMany(m => ResolvedBikePoses.FromMotion(m, step.Snapshot.Track, embedding: embedding)));
    rows.Add(new {
        scenario.Name, Configuration=physical ? "C" : "B", RiderOrderExact=true,
        ClearanceCertified=clearance.FrameCoverageGaps.Count==0 && clearance.Intervals.All(i=>
            !i.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous) && i.MinimumSeparationLowerBoundMeters > GeometryNumerics.ContactDistanceMeters),
        MinimumMechanicalSeparationMeters=clearance.Intervals.Select(i=>i.MinimumSeparationMeters).DefaultIfEmpty(0).Min(),

        Riders = step.Changes.Select(c => new { c.RiderId, c.LateralPosition,
            DisplacementMeters = LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(c.LateralPosition, step.Snapshot.Segment.Type, step.Snapshot.Track.Geometry)
                - LaneModel.PhysicalLateralOffsetFromInnerReferenceMeters(step.Snapshot.Rider(c.RiderId).LateralPosition, step.Snapshot.Segment.Type, step.Snapshot.Track.Geometry) }),
        Responses = step.Interaction!.Episodes.SelectMany(e => e.SelectedResponses),
        MechanicalContacts = clearance.Intervals.Count(i => i.EligibleForFutureInteraction),
        step.Interaction.Work
    });
}
File.WriteAllText(args[0], JsonSerializer.Serialize(rows, new JsonSerializerOptions {WriteIndented=true}) + "\n");

sealed class Hold : IRiderDecisionModel {public RiderDecision Decide(TrackSegment segment,RiderState rider)=>new(rider.Lane);}
