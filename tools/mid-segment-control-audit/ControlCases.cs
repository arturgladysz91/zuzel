using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MidSegmentControl;

internal static class ControlCases
{
    internal sealed record Context(SimulationSnapshot Snapshot, RiderIntent[] Intents, SimulationEngine Engine);
    internal static HeatSimulationOptions Options => new() { EnableContestedSpaceResponses = true,
        IncidentFrequency = 0, EnableLogging = false, InteractionDiagnostics = InteractionDiagnosticsLevel.Summary };
    internal static Context Prepare(string name)
    {
        var input = ContestedSpaceResponseEvidence.Scenarios().Single(s => s.Name == "D-inside-overlap");
        var straight = name.Contains("straight"); var fixedLine = name.StartsWith("fixed");
        var lateral = fixedLine || straight ? 2f : 1.6f;
        var target = fixedLine || straight ? 2 : 1;
        input = input with { SegmentIndex = straight ? 4 : 2, Riders = new[]
        { input.Riders[1] with { Lateral = lateral, Progress = straight ? 0 : .125f,
            Speed = straight ? 20 : 21, Intent = new(target, target, target) } } };
        var context = Prepare(input);
        return context with { Intents = context.Intents.Select(i => i with { Decision = i.Decision with
            { HoldLateralPosition = !fixedLine && name != "moving" } }).ToArray() };
    }
    internal static Context Pair(bool late = false, bool reverse = false)
    {
        var input = ContestedSpaceResponseEvidence.Scenarios().Single(s => s.Name == "D-inside-overlap");
        input = input with { Riders = input.Riders.Select(r => r with
            { Lateral = late ? (r.Id == 1 ? 1 : 1.31f) : (r.Id == 1 ? 1.2f : 1.6f),
              Progress = late ? .75f : .125f }).ToArray() };
        return Prepare(input, reverse);
    }
    private static Context Prepare(ContestedScenario input, bool reverse = false)
    {
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(input, reverse);
        var intents = snapshot.Riders.Select(r => new RiderIntent(r.RiderId, new RiderDecision(
            input.Riders.Single(i => i.Id == r.RiderId).Intent.TargetFor(snapshot.Segment.Type)))).ToArray();
        return new(snapshot, intents, new SimulationEngine(new Hold()));
    }
    internal static ResolvedSimulationStep Run(Context context)
        => context.Engine.ResolveProduction(context.Snapshot, context.Intents, Options, legacyContacts: false);
    internal static ContestedSpaceReport Audit(ResolvedSimulationStep step)
        => CommonTimePoseHistory.Observe(step.Motions.SelectMany(m => ResolvedBikePoses.FromMotion(m, step.Snapshot.Track)));
    internal static string Fingerprint(ResolvedSimulationStep step) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { step.Changes, step.Diagnostics, step.Motions, step.Events }))));
    private sealed class Hold : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane); }
}
