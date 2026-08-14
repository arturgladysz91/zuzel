using CoreSim;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

public sealed class BalanceAnalyzerTests
{
    [Fact]
    public void RotatesFourRidersAcrossEveryGateAndAccountsForEveryWin()
    {
        var profiles = Enumerable.Range(1, 4)
            .Select(id => RiderProfile.CreateDefault(id))
            .ToArray();

        var report = BalanceAnalyzer.AnalyzeStartingGates(
            Track.CreateExample(),
            profiles,
            simulations: 24,
            seed: 90,
            incidentFrequency: 0f);

        Assert.Equal(24, report.Simulations);
        Assert.Equal(4, report.Gates.Count);
        Assert.All(report.Gates, gate => Assert.Equal(24, gate.Starts));
        Assert.Equal(24, report.Gates.Sum(gate => gate.Wins));
        Assert.InRange(report.Gates.Sum(gate => gate.WinRate), 0.999f, 1.001f);
    }

    [Fact]
    public void ReportIsDeterministicForTheSameSeed()
    {
        var profiles = Enumerable.Range(1, 4)
            .Select(id => RiderProfile.CreateDefault(id))
            .ToArray();

        var first = BalanceAnalyzer.AnalyzeStartingGates(Track.CreateExample(), profiles, 12, 44, 0f);
        var second = BalanceAnalyzer.AnalyzeStartingGates(Track.CreateExample(), profiles, 12, 44, 0f);

        Assert.Equal(first.Simulations, second.Simulations);
        Assert.Equal(first.Gates, second.Gates);
        Assert.Equal(first.WinRateSpread, second.WinRateSpread);
    }
}
