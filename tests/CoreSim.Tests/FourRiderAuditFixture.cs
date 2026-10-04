using CoreSim.Analysis;

namespace CoreSim.Tests;

internal static class FourRiderAuditFixture
{
    // Both audit reader classes inspect the same immutable production aggregate.
    // Generate it once; explicit repeated/reversed heat tests still replay independently.
    private static readonly Lazy<BehaviorAuditResult> Audit = new(() => FourRiderBehaviorSuite.RunSuite());
    internal static BehaviorAuditResult Result => Audit.Value;
}
