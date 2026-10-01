using Xunit;

namespace CoreSim.Tests;

internal static class HistoricalPhysicsSource
{
    /// <summary>
    /// Retain the reviewed whole-engine physics hash. The only allowed change is
    /// forwarding immutable starting identity in CaptureSnapshot, outside resolution.
    /// Geometry/launch/decisions/contact/RNG/planner code must still be byte-identical.
    /// </summary>
    internal static string ForHash(string relative, string text)
    {
        if (relative != "src/CoreSim/SimulationEngine.cs") return text;
        const string forwarding = "rider.ManagerTrust) { StartingPosition = rider.StartingPosition });";
        Assert.Equal(1, text.Split(forwarding, StringSplitOptions.None).Length - 1);
        return text.Replace(forwarding, "rider.ManagerTrust));", StringComparison.Ordinal);
    }
}
