"""Apply observation-only scopes to a disposable source checkout, never a production branch.

Usage: python instrument.py CHECKOUT
Build that checkout through -p:CoreSimRoot=CHECKOUT and run the same benchmark tool.
Keep profiled measurements separate from uninstrumented benchmark measurements.
"""
import pathlib
import sys

root = pathlib.Path(sys.argv[1]).resolve()

def edit(name, before, after, count=1):
    path = root / name
    text = path.read_text(encoding="utf-8")
    actual = text.count(before)
    if actual != count:
        raise ValueError(f"{name}: expected {count} occurrences, found {actual}: {before[:100]}")
    path.write_text(text.replace(before, after), encoding="utf-8", newline="\n")

coordinator = "src/CoreSim/Interactions/ContestedSpaceInteractionCoordinator.cs"
edit(coordinator, "        tracker.Bind(snapshot);", '        using var profileResolve = CoreSim.PerformanceProbe.Section("coordinator");\n        tracker.Bind(snapshot);')
edit(coordinator, "        PairAlternativeResult PairCheck(Projection a, Projection b)\n        {", '        PairAlternativeResult PairCheck(Projection a, Projection b)\n        {\n            using var profilePair = CoreSim.PerformanceProbe.Section("pair-compatibility");')
edit(coordinator, "        Projection Project(InteractionAlternative alternative)\n        {", '        Projection Project(InteractionAlternative alternative)\n        {\n            using var profileCandidate = CoreSim.PerformanceProbe.Section("candidate-trajectory");')
edit(coordinator, "            Projection SafetyProject(InteractionAlternative alternative)\n            {", '            Projection SafetyProject(InteractionAlternative alternative)\n            {\n                using var profileCandidate = CoreSim.PerformanceProbe.Section("safety-trajectory");')
edit(coordinator, "            PairAlternativeResult SafetyPair(Projection left, Projection right)\n            {", '            PairAlternativeResult SafetyPair(Projection left, Projection right)\n            {\n                using var profilePair = CoreSim.PerformanceProbe.Section("safety-pair-compatibility");')
edit(coordinator, "        if (safetyEdges.Length > 0)\n        {", '        if (safetyEdges.Length > 0)\n        {\n            using var profileSafety = CoreSim.PerformanceProbe.Section("safety-pass");')
edit(coordinator, "                foreach (var joint in Joint(choices))\n                {", '                foreach (var joint in Joint(choices))\n                {\n                    using var profileScore = CoreSim.PerformanceProbe.Section("joint-scoring");')
edit(coordinator, "            foreach (var joint in Joint(choices))\n            {", '            foreach (var joint in Joint(choices))\n            {\n                using var profileScore = CoreSim.PerformanceProbe.Section("safety-joint-scoring");')
edit(coordinator, "var independent = engine.ResolveProduction(snapshot, intents, options, legacyContacts: false);", 'var independent = CoreSim.PerformanceProbe.Run("initial-production", () => engine.ResolveProduction(snapshot, intents, options, legacyContacts: false));')
edit(coordinator, "var clusters = Clusters(edges, snapshot, p);", 'var clusters = CoreSim.PerformanceProbe.Run("cluster-construction", () => Clusters(edges, snapshot, p));')
text = (root / coordinator).read_text(encoding="utf-8")
if "var final = fallbackPairs.Count == 0 ? actual" in text:
    edit(coordinator, "        var final = fallbackPairs.Count == 0 ? actual : engine.ResolveProduction(snapshot, finalIntents, options, contactFilter: (a, b) =>", '        var final = CoreSim.PerformanceProbe.Run("final-materialization", () => fallbackPairs.Count == 0 ? actual : engine.ResolveProduction(snapshot, finalIntents, options, contactFilter: (a, b) =>')
else:
    edit(coordinator, "        var final = engine.ResolveProduction(snapshot, finalIntents, options, contactFilter: (a, b) =>", '        var final = CoreSim.PerformanceProbe.Run("final-materialization", () => engine.ResolveProduction(snapshot, finalIntents, options, contactFilter: (a, b) =>')
edit(coordinator, "? (pair.Item1, pair.Item2) : (pair.Item2, pair.Item1)).ToArray());", "? (pair.Item1, pair.Item2) : (pair.Item2, pair.Item1)).ToArray()));")
edit("src/CoreSim/PhysicalSpace/BikePoseIntervals.cs", "        ArgumentNullException.ThrowIfNull(motion); ArgumentNullException.ThrowIfNull(track);", '        using var profilePoses = CoreSim.PerformanceProbe.Section("common-time-pose-construction");\n        ArgumentNullException.ThrowIfNull(motion); ArgumentNullException.ThrowIfNull(track);')
history = "src/CoreSim/Interactions/CommonTimePoseHistory.cs"
edit(history, "=> ContestedSpaceResolver.Observe(Stitch(source));", '=> CoreSim.PerformanceProbe.Run("contact-detection", () => ContestedSpaceResolver.Observe(Stitch(source)));')
edit(history, "=> ContestedSpaceResolver.ObserveCompatibility(Stitch(source), ready);", '=> CoreSim.PerformanceProbe.Run("compatibility-geometry", () => ContestedSpaceResolver.ObserveCompatibility(Stitch(source), ready));')
adapter = "src/CoreSim/Interactions/PhysicalContactSnapshotAdapter.cs"
if (root / adapter).exists():
    # The optional diagnostics stage is zero in Summary / physical diagnostics None.
    pass
trajectory = "src/CoreSim/Decisions/TrajectoryEvaluator.cs"
edit(trajectory, "        CandidateTraversalCount++;", '        CoreSim.PerformanceProbe.Count("trajectory-evaluations");\n        CandidateTraversalCount++;')
edit(trajectory, "                ProductionResolutionCount++;", '                CoreSim.PerformanceProbe.Count("prefix-executions");\n                ProductionResolutionCount++;')
edit(trajectory, "            else ProjectionCaptureAudit.Record(ProjectionMaterialization.PrefixCacheHit);", '            else { CoreSim.PerformanceProbe.Count("prefix-cache-hits"); ProjectionCaptureAudit.Record(ProjectionMaterialization.PrefixCacheHit); }')

(root / "src/CoreSim/PerformanceProbe.cs").write_text('''using System.Collections.Concurrent;
using System.Diagnostics;
namespace CoreSim;
public static class PerformanceProbe
{
    public sealed class Row { public long Ticks, Bytes, Calls; }
    public static readonly ConcurrentDictionary<string,Row> Rows = new();
    public static void Reset() => Rows.Clear();
    public static void Count(string name) => Interlocked.Increment(ref Rows.GetOrAdd(name, _ => new()).Calls);
    public static Scope Section(string name) => new(name);
    public static T Run<T>(string name, Func<T> run) { using var scope = Section(name); return run(); }
    public readonly struct Scope : IDisposable
    {
        private readonly string name;
        private readonly long ticks, bytes;
        public Scope(string name) { this.name = name; ticks = Stopwatch.GetTimestamp(); bytes = GC.GetAllocatedBytesForCurrentThread(); }
        public void Dispose()
        {
            var elapsed = Stopwatch.GetTimestamp() - ticks; var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            var row = Rows.GetOrAdd(name, _ => new()); Interlocked.Add(ref row.Ticks, elapsed);
            Interlocked.Add(ref row.Bytes, allocated); Interlocked.Increment(ref row.Calls);
        }
    }
}
''', encoding="utf-8", newline="\n")
print(f"Instrumented disposable checkout {root}")
