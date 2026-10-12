using System.Diagnostics;
using System.Runtime.InteropServices;
using CoreSim;
using CoreSim.Interactions;

namespace CausalAudit;

internal static class Performance
{
    internal static object Run()
    {
        var rows = new List<object>();
        foreach (var (name, snapshot) in new[] { ("no-contact", Fixtures.Rear(ahead: .4f, lateralGap: 1)),
            ("two-start-frontier", Fixtures.Rear()), ("four-start-frontier", Fixtures.Rear(count: 4)), ("start-crash", Fixtures.Rear(4)) })
        {
            var engine = new SimulationEngine(new Fixtures.Hold()); var intents = engine.Decide(snapshot); var options = Fixtures.Options;
            void Execute(bool d)
            {
                if (d) StartFrontierPrototype.Resolve(engine, snapshot, intents, options, new());
                else engine.Resolve(snapshot, intents, options, new());
            }
            var begun = Stopwatch.StartNew(); var warmups = 0;
            do { Execute(false); Execute(true); warmups++; } while (warmups < 8 || begun.Elapsed.TotalSeconds < 2);
            var samples = new List<object>();
            for (var sample = 0; sample < 9; sample++)
            foreach (var d in sample % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                var allocated = GC.GetTotalAllocatedBytes(true); var start = Stopwatch.GetTimestamp();
                for (var i = 0; i < 10; i++) Execute(d);
                samples.Add(new { Configuration = d ? "D-initial-only" : "C", Sample = sample,
                    Milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds / 10,
                    AllocatedBytes = (GC.GetTotalAllocatedBytes(true) - allocated) / 10 });
            }
            var counts = new List<object>();
            foreach (var d in new[] { false, true })
            {
                var before = ProjectionCaptureAudit.Observer; var physical = 0;
                ProjectionCaptureAudit.Observer = (kind, n) => { if (kind == ProjectionMaterialization.PhysicalEvaluation) physical += n; };
                try
                {
                    StartFrontierPrototype.Result? result = d ? StartFrontierPrototype.Resolve(engine, snapshot, intents, options, new()) : null;
                    var c = result?.EndpointC ?? engine.Resolve(snapshot, intents, options, new());
                    counts.Add(new { Configuration = d ? "D-initial-only" : "C", ProductionRiderResolutions = physical, PartialTraversals = 0,
                        EventFrontiers = result?.EventFrontiers ?? c.Interaction!.PhysicalContactConsequences?.AppliedPairs.GroupBy(p => p.FrontierStartTimeSeconds).Count() ?? 0,
                        PostImpactReplayCalls = result?.PostImpactReplays ?? 0, CCoordinatorWork = c.Interaction?.Work,
                        AdditionalFirstTouchWork = result?.Verification?.Work });
                }
                finally { ProjectionCaptureAudit.Observer = before; }
            }
            rows.Add(new { Name = name, Warmups = warmups, Samples = samples, Counts = counts });
        }
        return new { Schema = "rea004a-prototype-performance-v1", ProductionChanged = false,
            Environment = new { OS = RuntimeInformation.OSDescription, Runtime = RuntimeInformation.FrameworkDescription },
            Protocol = "Eight or more paired warmups and two seconds; nine interleaved samples per mode; ten resolves per sample; no Commit or serialization timed; process-wide allocations.", Rows = rows };
    }
}
