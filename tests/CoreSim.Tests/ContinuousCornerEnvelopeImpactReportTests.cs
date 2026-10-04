using System.Globalization;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class ContinuousCornerEnvelopeImpactReportTests
{
    private const string AfterHash = "c06f632e04c72d119e5cf925f9f08a8e4ee6a9d6e2dde4c01491a462f8165417";

    [Fact]
    public void ReportIsByteStableLfOnlyAndCultureIndependent()
    {
        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "calibration",
            "continuous-corner-envelope-impact.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var csv = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "calibration", "pge", "v1",
            "pge_rider_heats.csv"));
        var dataset = RealWorldCalibrationDataset.ParseCsv(csv);
        string Render(string culture)
        {
            var prior = CultureInfo.CurrentCulture;
            var priorUi = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                return ContinuousCornerEnvelopeImpactReport.Render(CalibrationScenarioSuite.RunRequired(), dataset, AfterHash);
            }
            finally { CultureInfo.CurrentCulture = prior; CultureInfo.CurrentUICulture = priorUi; }
        }
        var en = Render("en-US");
        var pl = Render("pl-PL");
        Assert.Equal(expected, en);
        Assert.Equal(en, pl);
        Assert.DoesNotContain('\r', en);
        Assert.DoesNotContain("NaN", en, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", en, StringComparison.Ordinal);
        Assert.Contains("B19 is selected", en);
        Assert.Contains("No hard speed cap was added", en);
    }

    [Fact]
    public void FrozenConstantsAndOnlyBoundedAdvancedReferenceAreExplicit()
    {
        Assert.Equal(16f, SegmentPhysics.ReferenceTurnSpeedMetersPerSecond);
        Assert.Equal(19f, SegmentPhysics.AdvancedReferenceTurnSpeedMetersPerSecond);
        Assert.Equal(1.6f, LongitudinalDynamics.MinStraightAccelerationMetersPerSecondSquared);
        Assert.Equal(3.2f, LongitudinalDynamics.MaxStraightAccelerationMetersPerSecondSquared);
        Assert.Equal(1.2f, LongitudinalDynamics.MinTurnExitAccelerationMetersPerSecondSquared);
        Assert.Equal(2.8f, LongitudinalDynamics.MaxTurnExitAccelerationMetersPerSecondSquared);
        Assert.Equal(.035f, LongitudinalDynamics.ProvisionalDriveOrientedForceFadePerMeterPerSecond);
        Assert.Equal(.010f, LongitudinalDynamics.ProvisionalSpeedOrientedForceFadePerMeterPerSecond);
        Assert.Equal(142f, LongitudinalDynamics.ProvisionalNominalSystemMassKilograms);
        Assert.Equal(1f, LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters);
        Assert.Equal(1.015f, SegmentPhysics.AdvancedQuietCorrectionSpeedFactor);
        Assert.Equal(1.06f, SegmentPhysics.MinAdvancedBrakeSpeedFactor);
        Assert.Equal(1.14f, SegmentPhysics.MaxAdvancedBrakeSpeedFactor);
        Assert.Equal(1.18f, SegmentPhysics.MinAdvancedRunWideSpeedFactor);
        Assert.Equal(1.34f, SegmentPhysics.MaxAdvancedRunWideSpeedFactor);
        Assert.Equal(.35f, SegmentPhysics.MinRunWideOverspeedRetention);
        Assert.Equal(.65f, SegmentPhysics.MaxRunWideOverspeedRetention);
    }
}
