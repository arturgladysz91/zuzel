using System.Globalization;
using CoreSim;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

public sealed class ContinuousCornerFoundationReportTests
{
    private const string BaseSha = "0f6dfba7767b155a9c988687a95c5cc7e50a50bd";
    private const string CandidateSha = "1111111111111111111111111111111111111111";

    [Fact]
    public void ReportIsDeterministicLfOnlyAndCultureIndependent()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var reports = new List<string>();
            foreach (var cultureName in new[] { "en-US", "pl-PL" })
            {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                reports.Add(Render());
            }

            Assert.Equal(reports[0], reports[1]);
            Assert.Equal(reports[0], Render());
            Assert.DoesNotContain("\r", reports[0], StringComparison.Ordinal);
            Assert.Contains("Calibration Scenario Suite before/after bytes identical: yes", reports[0]);
            Assert.Contains("CreateExample", reports[0]);
            Assert.Contains("CreateStandingStartExample", reports[0]);
            Assert.Contains("NO MATERIAL PHYSICS CHANGE.", reports[0]);
            Assert.Contains("NO SPEED/PERFORMANCE CONSTANTS CHANGED.", reports[0]);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void ReportRejectsAnyProductionScenarioDifference()
        => Assert.Throws<InvalidOperationException>(() =>
            ContinuousCornerFoundationReport.Render(
                CalibrationScenarioSuite.RunRequired(),
                "before\n",
                "after\n",
                BaseSha,
                CandidateSha));

    [Fact]
    public void FoundationLocksEveryLongitudinalAndCornerConstantFromMain()
    {
        Assert.Equal(1.60f, LongitudinalDynamics.MinStraightAccelerationMetersPerSecondSquared);
        Assert.Equal(3.20f, LongitudinalDynamics.MaxStraightAccelerationMetersPerSecondSquared);
        Assert.Equal(1.20f, LongitudinalDynamics.MinTurnExitAccelerationMetersPerSecondSquared);
        Assert.Equal(2.80f, LongitudinalDynamics.MaxTurnExitAccelerationMetersPerSecondSquared);
        Assert.Equal(0.0350f, LongitudinalDynamics.ProvisionalDriveOrientedForceFadePerMeterPerSecond);
        Assert.Equal(0.0100f, LongitudinalDynamics.ProvisionalSpeedOrientedForceFadePerMeterPerSecond);
        Assert.Equal(16f, LongitudinalDynamics.ProvisionalPositiveDriveReferenceSpeedMetersPerSecond);
        Assert.Equal(142f, LongitudinalDynamics.ProvisionalNominalSystemMassKilograms);
        Assert.Equal(40f, LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons);
        Assert.Equal(0.20f, LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient);
        Assert.Equal(1.10f, LongitudinalDynamics.LowGearingDriveMultiplier);
        Assert.Equal(0.90f, LongitudinalDynamics.HighGearingDriveMultiplier);
        Assert.Equal(0.75f, LongitudinalDynamics.MinSurfaceDriveMultiplier);
        Assert.Equal(0.25f, LongitudinalDynamics.SurfaceDriveMultiplierRange);
        Assert.Equal(0.28f, LongitudinalDynamics.ProvisionalStandingStartSlowReactionSeconds);
        Assert.Equal(0.20f, LongitudinalDynamics.ProvisionalStandingStartFastReactionSeconds);
        Assert.Equal(9f, LongitudinalDynamics.ProvisionalStandingStartMinimumReferenceAccelerationMetersPerSecondSquared);
        Assert.Equal(11f, LongitudinalDynamics.ProvisionalStandingStartMaximumReferenceAccelerationMetersPerSecondSquared);
        Assert.Equal(2f, LongitudinalDynamics.MinCornerEntryDecelerationMetersPerSecondSquared);
        Assert.Equal(3.2f, LongitudinalDynamics.MaxCornerEntryDecelerationMetersPerSecondSquared);
        Assert.Equal(0.50f, LongitudinalDynamics.ProvisionalTurnEntryScrubDistanceFraction);
        Assert.Equal(1.015f, SegmentPhysics.AdvancedQuietCorrectionSpeedFactor);
        Assert.Equal(1.06f, SegmentPhysics.MinAdvancedBrakeSpeedFactor);
        Assert.Equal(1.14f, SegmentPhysics.MaxAdvancedBrakeSpeedFactor);
        Assert.Equal(1.18f, SegmentPhysics.MinAdvancedRunWideSpeedFactor);
        Assert.Equal(1.34f, SegmentPhysics.MaxAdvancedRunWideSpeedFactor);
        Assert.Equal(0.35f, SegmentPhysics.MinRunWideOverspeedRetention);
        Assert.Equal(0.65f, SegmentPhysics.MaxRunWideOverspeedRetention);
    }

    private static string Render()
        => ContinuousCornerFoundationReport.Render(
            CalibrationScenarioSuite.RunRequired(),
            "captured production scenario report\n",
            "captured production scenario report\n",
            BaseSha,
            CandidateSha);
}
