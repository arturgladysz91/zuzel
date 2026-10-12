using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.Logging;
using CoreSim.Race;
using CausalAudit;
using RaceReadiness;

var mode = args.ElementAtOrDefault(0) ?? "probe";
var output = args.ElementAtOrDefault(1) ?? "results/rea004a-probe";
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { WriteIndented = false }; json.Converters.Add(new JsonStringEnumConverter());
if (mode == "performance")
{
    File.WriteAllText(Path.Combine(output, "performance.json"), JsonSerializer.Serialize(Performance.Run(), json));
    return;
}
var rows = new List<object>();
SimulationSnapshot? failingBoundary = null; double failingTime = 0;
var baselineHashes = new Dictionary<(string Id, int Seed), string>();
if (mode == "probe")
{
    foreach (var (name, closing, ahead, count, lateral) in new[] { ("start-rear-brush", .2f, .105f, 2, 0f), ("start-rear-moderate", 1f, .105f, 2, 0f),
        ("start-rear-lost-rhythm", 2f, .105f, 2, 0f), ("start-rear-major-save", 3f, .105f, 2, 0f), ("start-rear-crash", 4f, .105f, 2, 0f), ("interior-rear", 1f, .125f, 2, 0f), ("near-end-rear", 4f, .285f, 2, 0f), ("side-overlap-unsupported", 1f, 0f, 2, .3f), ("three-start-frontier", 1f, .105f, 3, 0f),
        ("four-start-frontier", 1f, .105f, 4, 0f), ("four-start-crash-stress", 4f, .105f, 4, 0f), ("no-contact", 1f, .4f, 2, 1f) })
    foreach (var reversed in new[] { false, true })
    {
        var snapshot = Fixtures.Rear(closing, ahead, count, lateral);
        var engine = new SimulationEngine(new Fixtures.Hold()); var tracker = new InteractionEpisodeTracker();
        var live = snapshot.Riders.Select(r => r.ToMutableCopy()).ToList(); if (reversed) live.Reverse();
        var surface = new TrackState(2, 5, snapshot.TrackState.GetSurface);
        var intents = engine.Decide(snapshot).ToArray(); if (reversed) Array.Reverse(intents);
        var before = Exact.Hash(new { Live = live.OrderBy(r => r.RiderId).ToArray(), Surface = surface.Snapshot(), Tracker = Fixtures.State(tracker) });
        try
        {
            var result = StartFrontierPrototype.Resolve(engine, snapshot, intents, Fixtures.Options, tracker);
            var afterResolve = Exact.Hash(new { Live = live.OrderBy(r => r.RiderId).ToArray(), Surface = surface.Snapshot(), Tracker = Fixtures.State(tracker) });
            if (before != afterResolve) throw new InvalidOperationException("Prototype Resolve mutated real state");
            result.Commit(engine, live, surface, new SimLog(false));
            var nextRecovery = result.Executed.Changes.Where(c => c.ContactRecovery is not null && c.Status == RiderRaceStatus.Racing).Select(c =>
            {
                var rider = result.PreImpact.Rider(c.RiderId).Apply(c);
                var next = new SimulationSnapshot(snapshot.Step with { StepNumber = 1, SegmentIndex = 1 }, snapshot.Track, surface.Snapshot(), new[] { rider });
                var intent = new[] { new RiderIntent(rider.RiderId, new RiderDecision(2)) };
                var impaired = engine.ResolveProduction(next, intent, Fixtures.Options, legacyContacts: false);
                var clean = new SimulationSnapshot(next.Step, next.Track, next.TrackState, new[] { rider with { ContactRecovery = null } });
                var control = engine.ResolveProduction(clean, intent, Fixtures.Options, legacyContacts: false);
                return new { rider.RiderId, Incoming = rider.ContactRecovery, DurationSeconds = impaired.Motions[0].TotalTimeSeconds,
                    Impaired = impaired.Changes[0], MatchedWithoutRecovery = control.Changes[0], Outgoing = impaired.Changes[0].ContactRecovery,
                    Scope = "Receiver-only next-active-segment probe; no traffic continuation of D is supported." };
            }).ToArray();
            rows.Add(new { Name = name, Reversed = reversed, Supported = true, result.EventFrontiers, result.PartialTraversals, result.PostImpactReplays,
                C = result.EndpointC.Changes, D = result.Executed.Changes, result.EventPlan, result.Verification,
                CWork = result.EndpointC.Interaction?.Work, AdditionalNarrowPhase = result.Verification?.Work, NextSoloRecovery = nextRecovery,
                FirstAffectedMovement = result.Executed.Motions.Select(m => new { m.RiderId, FirstPositiveTimeNode = m.Nodes.FirstOrDefault(n => n.LocalTimeSeconds > 0) }).ToArray(),
                PartialExposureClassification = RaceClassification.Build(live, Fixtures.Options.Laps),
                EventBefore = result.PreImpact.Riders, EventAfter = result.PostImpact?.Riders, Motions = result.Executed.Motions,
                ResolvePrivate = before == afterResolve, Tracker = Fixtures.State(tracker),
                Exact = Exact.Leaves(new { EndpointC = result.EndpointC.Changes, NextSoloRecovery = nextRecovery, result.EventPlan, EventBefore = result.PreImpact.Riders, EventAfter = result.PostImpact?.Riders,
                    result.Executed.Changes, result.Executed.Motions, Surface = surface.Snapshot(), Tracker = Fixtures.State(tracker) }) });
        }
        catch (UnsupportedCausalReplayException e)
        {
            Console.WriteLine("unsupported: " + e.Message);
            var after = Exact.Hash(new { Live = live.OrderBy(r => r.RiderId).ToArray(), Surface = surface.Snapshot(), Tracker = Fixtures.State(tracker) });
            if (before != after) throw new InvalidOperationException("Refusal mutated real state");
            rows.Add(new { Name = name, Reversed = reversed, Supported = false, Reason = e.Message, Uncommitted = before == after });
        }
        Console.WriteLine(name + "/" + reversed);
    }
}
else if (mode is "witness" or "require-causal" or "contacts")
{
    var keys = new List<(string Id, int Seed)>();
    if (mode is "witness" or "require-causal") keys.Add(("control", 19));
    else
    {
        using var stream = new GZipStream(File.OpenRead(Path.Combine(args[2], "runs.json.gz")), CompressionMode.Decompress);
        using var document = JsonDocument.Parse(stream);
        foreach (var r in document.RootElement.EnumerateArray())
            if (r.GetProperty("Configuration").GetString() == "C" && r.GetProperty("Pairs").GetArrayLength() > 0)
                {
                    var key = (r.GetProperty("Id").GetString()!, r.GetProperty("Seed").GetInt32());
                    keys.Add(key); baselineHashes[key] = r.GetProperty("FinalHash").GetString()!;
                }
    }
    foreach (var (id, seed) in keys)
    foreach (var reversed in new[] { false, true })
    {
        var scenario = Catalog.All().Single(s => s.Id == id); var (track, surface, riders) = scenario.Create();
        if (reversed) riders.Reverse();
        var observer = new TimingObserver(); var options = new HeatSimulationOptions { Seed = seed, Weather = scenario.Weather,
            EnableContestedSpaceResponses = true, EnablePhysicalContactConsequences = true, PhysicalContactDiagnostics = PhysicalContactDiagnosticsLevel.FullAudit };
        var timer = Stopwatch.StartNew();
        var result = new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(track, surface, riders, options, 91, observer);
        var finalHash = Exact.Hash(new { result.Classification, result.Log, Riders = riders.OrderBy(r => r.RiderId).ToArray(), Surface = surface.Snapshot() });
        if (baselineHashes.TryGetValue((id, seed), out var expected) && expected != finalHash)
            throw new InvalidOperationException("Retained diagnostics changed baseline final state.");
        var historical = observer.Contacts.FirstOrDefault(c => c.HistoricalParticipant);
        if (historical is not null)
        {
            failingBoundary = observer.Steps.Single(s => s.Snapshot.Step.StepNumber == historical.Step).Snapshot;
            failingTime = historical.FirstTouchSeconds;
        }
        rows.Add(new { Id = id, Seed = seed, Configuration = "C", Reversed = reversed, Flags = Exact.Leaves(options),
            Contacts = observer.Complete(), observer.Frontiers, result.Classification,
            observer.VerifiedContacts, SubsequentAppliedContacts = observer.Contacts.Select(c => new { c.Step, c.RiderId, c.OtherRiderId, c.FirstTouchSeconds }).ToArray(),
            Final = finalHash, BaselineFinalMatches = mode == "contacts" ? true : (bool?)null,
            ExactContacts = Exact.Leaves(observer.Complete()), ExactVerified = Exact.Leaves(observer.VerifiedContacts), WallMilliseconds = timer.Elapsed.TotalMilliseconds });
        Console.WriteLine(id + "/" + seed + "/" + reversed);
    }
}
else throw new ArgumentException("probe|witness|require-causal|contacts output [fresh-readiness-directory]");
File.WriteAllText(Path.Combine(output, "evidence.json"), JsonSerializer.Serialize(new { Schema = "rea004a-v1", Mode = mode,
    StartingMain = "f40b2fd129124d969fd22cfe7767e2fb00bd651f", Seed = mode == "probe" ? 19 : (int?)null, Flags = mode == "probe" ? Exact.Leaves(Fixtures.Options) : null, ProductionChanged = false, GeneralReplayEnabled = false, RequestedD = mode == "require-causal", ExpectedCausalFailure = mode == "require-causal" ? "CommittedBeyondFirstTouch" : null,
    Environment = new { OS = RuntimeInformation.OSDescription, Runtime = RuntimeInformation.FrameworkDescription }, Rows = rows }, json));

if (mode == "require-causal")
{
    if (failingBoundary is null) throw new InvalidOperationException("Historical-span fixture failed to reproduce its blocker.");
    CausalBoundary.RequireCommonStart(failingBoundary, failingTime);
    throw new InvalidOperationException("Historical-span D request unexpectedly accepted committed future motion.");
}
