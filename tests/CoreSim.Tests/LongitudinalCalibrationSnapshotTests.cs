using System.Globalization;
using System.Text.Json;
using CoreSim.Analysis;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class LongitudinalCalibrationSnapshotTests
{
    private const string BaseSha = "842e0ce861466cdf5a67287c155f81d879cf6666";
    private const string CandidateSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly LongitudinalCalibrationSnapshot Before = ReadBefore();
    private static readonly LongitudinalCalibrationSnapshot After =
        LongitudinalCalibrationSnapshotBuilder.Capture(CandidateSha);

    [Fact]
    public void SelectedConstantsFollowStagesABCAndEverythingElseIsFrozen()
    {
        var expected = Before.Constants with
        {
            StraightMinimumReferenceAcceleration = 1.60f,
            StraightMaximumReferenceAcceleration = 3.20f,
            TurnExitMinimumReferenceAcceleration = 1.20f,
            TurnExitMaximumReferenceAcceleration = 2.80f,
            DriveOrientedFadeRate = 0.0350f,
            SpeedOrientedFadeRate = 0.0100f,
        };
        Assert.Equal(expected, After.Constants);
        Assert.True(After.Constants.StraightMinimumReferenceAcceleration
            >= After.Constants.TurnExitMinimumReferenceAcceleration);
        Assert.True(After.Constants.StraightMaximumReferenceAcceleration
            >= After.Constants.TurnExitMaximumReferenceAcceleration);
    }

    [Fact]
    public void SnapshotHasEveryRequiredAxisAndUsesProductionProfiles()
    {
        Assert.Equal(new[] { 10f, 16f, 20f, 24f }, Axis("entry/", item => item.EntrySpeed));
        Assert.Equal(new[] { 10f, 20f, 30f, 60f, 100f, 300f, 600f },
            Axis("distance/", item => item.DistanceMeters));
        Assert.Equal(new[] { 0f, 25f, 50f, 75f, 100f }, Axis("speed/", item => item.SpeedSkill));
        Assert.Equal(18, After.Straights.Count(item => item.ScenarioId.StartsWith("gearing/", StringComparison.Ordinal)));
        Assert.Equal(72, After.ForceCurve.Length);
        Assert.Equal(9, After.Equilibria.Length);
        Assert.Equal(new[] { 0f, 25f, 50f, 75f, 100f }, After.TurnExits.Select(item => item.SpeedSkill));
        Assert.Equal(new[] { 0f, 25f, 50f, 75f, 100f }, After.HeatRiders.Select(item => item.SpeedSkill).Distinct());
        Assert.All(After.Straights, item => Assert.Equal(item.DistanceMeters,
            item.AccelerationDistance + item.CruiseDistance + item.DecelerationDistance, 3));
    }

    [Fact]
    public void SignedEquilibriumAndGearingCrossoverRemainNatural()
    {
        Assert.All(After.Equilibria, item =>
        {
            Assert.True(item.BelowNetForce > 0f);
            Assert.InRange(MathF.Abs(item.NearNetForce), 0f, 0.001f);
            Assert.True(item.AboveNetForce < 0f);
        });
        float Exit(float gearing, float distance) => After.Straights.Single(item =>
            item.ScenarioId.StartsWith("gearing/", StringComparison.Ordinal)
            && item.Gearing == gearing
            && item.DistanceMeters == distance).ExitSpeed;
        Assert.True(Exit(0f, 60f) > Exit(1f, 60f));
        Assert.True(Exit(1f, 300f) > Exit(0f, 300f));
        Assert.InRange(After.GearingCrossover.TraversalCrossoverDistance, 60f, 300f);
    }

    [Fact]
    public void ScenarioIdsAreCanonicalAndIndependentOfCurrentCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var enUsIds = ScenarioIds(LongitudinalCalibrationSnapshotBuilder.Capture(CandidateSha));

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            var plPlIds = ScenarioIds(LongitudinalCalibrationSnapshotBuilder.Capture(CandidateSha));

            Assert.Equal(enUsIds, plPlIds);
            Assert.Contains("gearing/0.0/distance/060", enUsIds);
            Assert.Contains("gearing/0.5/distance/060", enUsIds);
            Assert.Contains("gearing/1.0/distance/060", enUsIds);
            Assert.All(enUsIds, id => Assert.DoesNotContain(',', id));
            Assert.Equal(enUsIds, ScenarioIds(Before));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void StartAndControlConstantsAreFrozenWhileAdvancedCornerReferenceChanges()
    {
        Assert.Equal(Before.Constants.StartReactionSlow, After.Constants.StartReactionSlow);
        Assert.Equal(Before.Constants.StartReactionFast, After.Constants.StartReactionFast);
        Assert.Equal(Before.Constants.StartAccelerationMinimum, After.Constants.StartAccelerationMinimum);
        Assert.Equal(Before.Constants.StartAccelerationMaximum, After.Constants.StartAccelerationMaximum);
        foreach (var current in After.Starts)
            Assert.Equal(Before.Starts.Single(item => item.ScenarioId == current.ScenarioId).ReactionTime,
                current.ReactionTime);
        foreach (var current in After.Corners)
        {
            var prior = Before.Corners.Single(item => item.ScenarioId == current.ScenarioId);
            Assert.Equal(prior.CorrectionCapability, current.CorrectionCapability);
            Assert.Equal(prior.MaxSafeSpeed * 19f / 16f, current.MaxSafeSpeed, 4);
            Assert.Equal(prior.FirstBrakeSpeed * 19f / 16f, current.FirstBrakeSpeed, 4);
            Assert.Equal(prior.FirstRunWideSpeed!.Value * 19f / 16f, current.FirstRunWideSpeed!.Value, 4);
            Assert.Equal(prior.FirstCrashSpeed * 19f / 16f, current.FirstCrashSpeed, 4);
            Assert.InRange(MathF.Abs(prior.RunWideRetention!.Value - current.RunWideRetention!.Value), 0f, 0.00001f);
        }
    }

    [Fact]
    public void ReportIsDeterministicInvariantCultureAndLfOnly()
    {
        var datasetPath = Path.Combine(AppContext.BaseDirectory, "data", "calibration", "pge", "v1", "pge_rider_heats.csv");
        var dataset = RealWorldCalibrationDataset.ParseCsv(File.ReadAllText(datasetPath));
        var expected = LongitudinalSpeedEnvelopeReport.Render(Before, After, dataset.Distributions, BaseSha, CandidateSha);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            Assert.Equal(expected, LongitudinalSpeedEnvelopeReport.Render(
                Before, LongitudinalCalibrationSnapshotBuilder.Capture(CandidateSha),
                dataset.Distributions, BaseSha, CandidateSha));
        }
        finally { CultureInfo.CurrentCulture = previous; }
        Assert.DoesNotContain('\r', expected);
        Assert.DoesNotContain("NaN", expected, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", expected, StringComparison.Ordinal);
        Assert.Contains("Residual system-level speed deficit may belong to Phase 2 corner-envelope calibration.", expected);
        Assert.Contains("Likely Phase 2 corner-envelope calibration gap.", expected);
    }

    private static float[] Axis(string prefix, Func<LongitudinalStraightObservation, float> value)
        => After.Straights.Where(item => item.ScenarioId.StartsWith(prefix, StringComparison.Ordinal))
            .Select(value).ToArray();

    private static string[] ScenarioIds(LongitudinalCalibrationSnapshot snapshot)
        => snapshot.Straights.Select(item => item.ScenarioId)
            .Concat(snapshot.TurnExits.Select(item => item.ScenarioId))
            .Concat(snapshot.Starts.Select(item => item.ScenarioId))
            .Concat(snapshot.HeatRiders.Select(item => item.ScenarioId))
            .Concat(snapshot.WithinHeat.Select(item => item.ScenarioId))
            .Concat(snapshot.Corners.Select(item => item.ScenarioId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private static LongitudinalCalibrationSnapshot ReadBefore()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "calibration", "longitudinal-speed-envelope-before.json");
        return JsonSerializer.Deserialize<LongitudinalCalibrationSnapshot>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Before snapshot is empty.");
    }
}
