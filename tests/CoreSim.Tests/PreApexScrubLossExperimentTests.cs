using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Analysis;
using CoreSim.Race;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "historical-analysis")]
public sealed class PreApexScrubLossExperimentTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly Lazy<PreApexScrubLossExperimentResult> Result =
        new(PreApexScrubLossExperiment.Run);

    [Fact]
    public void CandidateMenuIsExactAndValuesAreValidated()
    {
        Assert.Equal(new[] { "S0", "S025", "S050", "S075", "S100" },
            PreApexScrubLossExperiment.CandidateMenu.Select(item => item.Id));
        Assert.Equal(new[] { 0f, .25f, .50f, .75f, 1f },
            PreApexScrubLossExperiment.CandidateMenu.Select(item =>
                item.PeakScrubDecelerationMetersPerSecondSquared));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreApexScrubCandidate("bad", -.01f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreApexScrubCandidate("bad", float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreApexScrubCandidate("bad", float.PositiveInfinity));
    }

    [Fact]
    public void ScrubWindowHasFrozenContinuousSmoothstepShape()
    {
        Assert.Equal(0f, PreApexScrubLossExperiment.ScrubWindow(0f));
        Assert.Equal(0f, PreApexScrubLossExperiment.ScrubWindow(.10f));
        Assert.Equal(.5f, PreApexScrubLossExperiment.ScrubWindow(.20f), 5);
        Assert.Equal(1f, PreApexScrubLossExperiment.ScrubWindow(.30f));
        Assert.Equal(1f, PreApexScrubLossExperiment.ScrubWindow(.40f));
        Assert.Equal(.5f, PreApexScrubLossExperiment.ScrubWindow(.45f), 5);
        Assert.Equal(0f, PreApexScrubLossExperiment.ScrubWindow(.50f));
        Assert.All(new[] { .500001f, .625f, .75f, .875f, 1f },
            value => Assert.Equal(0f, PreApexScrubLossExperiment.ScrubWindow(value)));
        const float epsilon = 1e-4f;
        Assert.InRange(PreApexScrubLossExperiment.ScrubWindow(.10f + epsilon) / epsilon, 0f, .1f);
        Assert.InRange((1f - PreApexScrubLossExperiment.ScrubWindow(.30f - epsilon)) / epsilon, 0f, .1f);
        Assert.InRange((1f - PreApexScrubLossExperiment.ScrubWindow(.40f + epsilon)) / epsilon, 0f, .1f);
        Assert.InRange(PreApexScrubLossExperiment.ScrubWindow(.50f - epsilon) / epsilon, 0f, .1f);
    }

    [Fact]
    public void S0UsesBitExactProductionTraversal()
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        var phase = track.CornerTopology.Resolve(1, 0f, 1f, track.Geometry)!.Value;
        var skills = new RiderSkills(50f, 50f, 50f, 50f, 50f, 50f);
        var envelope = ContinuousCornerEnvelope.Create(phase, 1f, track.Geometry,
            CalibrationScenarioCatalog.Baseline.Surface, skills, BikeSetup.Neutral);
        var production = envelope.Traverse(27.098435f, 0f, phase.TotalCornerLengthMeters);
        var s0 = Isolated("S0");
        var productionApex = production.Nodes.Single(node => node.CornerProgress == .5f);
        Assert.Equal(BitConverter.SingleToInt32Bits(production.EntrySpeedMetersPerSecond),
            BitConverter.SingleToInt32Bits(s0.EntrySpeedMetersPerSecond));
        Assert.Equal(BitConverter.SingleToInt32Bits(productionApex.SpeedMetersPerSecond),
            BitConverter.SingleToInt32Bits(s0.TrueApexSpeedMetersPerSecond));
        Assert.Equal(BitConverter.SingleToInt32Bits(production.ExitSpeedMetersPerSecond),
            BitConverter.SingleToInt32Bits(s0.ExitSpeedMetersPerSecond));
        Assert.Equal(BitConverter.SingleToInt32Bits(production.TravelTimeSeconds),
            BitConverter.SingleToInt32Bits(s0.TotalTimeSeconds));
        Assert.True(Result.Value.Freeze.S0ExactProduction);
        Assert.True(Result.Value.Freeze.ProductionDefaultExact);
    }

    [Fact]
    public void ZeroForceExperimentalPathMatchesProductionStepForStep()
    {
        var envelope = CreateIsolatedEnvelope();
        var production = envelope.Traverse(PreApexScrubLossExperiment.IsolatedEntrySpeedMetersPerSecond,
            0f, envelope.TotalLengthMeters);
        var experimental = envelope.TraverseWithZeroForcePreApexScrubLossExperimentPath(
            PreApexScrubLossExperiment.IsolatedEntrySpeedMetersPerSecond, 0f,
            envelope.TotalLengthMeters);

        Assert.Equal(BitConverter.SingleToInt32Bits(production.ExitSpeedMetersPerSecond),
            BitConverter.SingleToInt32Bits(experimental.ExitSpeedMetersPerSecond));
        Assert.Equal(BitConverter.SingleToInt32Bits(production.TravelTimeSeconds),
            BitConverter.SingleToInt32Bits(experimental.TravelTimeSeconds));
        Assert.Equal(BitConverter.SingleToInt32Bits(production.CorrectionDistanceMeters),
            BitConverter.SingleToInt32Bits(experimental.CorrectionDistanceMeters));
        Assert.Equal(production.Nodes.Count, experimental.Nodes.Count);
        foreach (var (expected, actual) in production.Nodes.Zip(experimental.Nodes))
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.CornerProgress),
                BitConverter.SingleToInt32Bits(actual.CornerProgress));
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.SpeedMetersPerSecond),
                BitConverter.SingleToInt32Bits(actual.SpeedMetersPerSecond));
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.EnvelopeSpeedMetersPerSecond),
                BitConverter.SingleToInt32Bits(actual.EnvelopeSpeedMetersPerSecond));
            Assert.Equal(BitConverter.SingleToInt32Bits(expected.ElapsedTimeSeconds),
                BitConverter.SingleToInt32Bits(actual.ElapsedTimeSeconds));
        }
        Assert.Equal(0f, experimental.ScrubDistanceMeters);
        Assert.Null(experimental.FirstScrubProgress);
        Assert.True(Result.Value.Freeze.ZeroForceExperimentalPathExact);
    }

    [Fact]
    public void ScrubKnotsDoNotSplitCanonicalProductionSteps()
    {
        var envelope = CreateIsolatedEnvelope();
        var production = envelope.Traverse(PreApexScrubLossExperiment.IsolatedEntrySpeedMetersPerSecond,
            0f, envelope.TotalLengthMeters);
        var full = envelope.TraverseWithPreApexScrubLoss(
            PreApexScrubLossExperiment.IsolatedEntrySpeedMetersPerSecond, 0f,
            envelope.TotalLengthMeters, new PreApexScrubLossAdjustment(1f));

        Assert.Equal(production.Nodes.Select(node => BitConverter.SingleToInt32Bits(node.CornerProgress)),
            full.Nodes.Select(node => BitConverter.SingleToInt32Bits(node.CornerProgress)));
        Assert.DoesNotContain(full.Nodes, node => node.CornerProgress is .10f or .30f or .40f);
    }

    [Fact]
    public void RawTrajectoryIsExactThroughWindowStartAndFirstDifferenceRequiresAppliedScrub()
    {
        var baseline = TraverseIsolated(0f);
        foreach (var peak in new[] { .25f, .50f, .75f, 1f })
        {
            var candidate = TraverseIsolated(peak);
            var baselinePreWindow = baseline.Nodes.Where(node => node.CornerProgress <= .10f).ToArray();
            var candidatePreWindow = candidate.Nodes.Where(node => node.CornerProgress <= .10f).ToArray();
            Assert.Equal(baselinePreWindow.Length, candidatePreWindow.Length);
            foreach (var (expected, actual) in baselinePreWindow.Zip(candidatePreWindow))
            {
                Assert.Equal(BitConverter.SingleToInt32Bits(expected.CornerProgress),
                    BitConverter.SingleToInt32Bits(actual.CornerProgress));
                Assert.Equal(BitConverter.SingleToInt32Bits(expected.SpeedMetersPerSecond),
                    BitConverter.SingleToInt32Bits(actual.SpeedMetersPerSecond));
            }

            var firstDifference = baseline.Nodes.Zip(candidate.Nodes)
                .Select((pair, index) => new { pair.First, pair.Second, Index = index })
                .First(item => BitConverter.SingleToInt32Bits(item.First.SpeedMetersPerSecond)
                    != BitConverter.SingleToInt32Bits(item.Second.SpeedMetersPerSecond));
            Assert.True(firstDifference.Second.ScrubApplied);
            Assert.True(firstDifference.Second.ScrubAppliedDistanceMeters > 0f);
            Assert.True(firstDifference.Second.CornerProgress > .10f);
            Assert.All(baseline.Nodes.Take(firstDifference.Index).Zip(
                    candidate.Nodes.Take(firstDifference.Index)),
                pair => Assert.Equal(BitConverter.SingleToInt32Bits(pair.First.SpeedMetersPerSecond),
                    BitConverter.SingleToInt32Bits(pair.Second.SpeedMetersPerSecond)));
        }

        foreach (var corner in new[] { 1, 2 })
        {
            var expected = Primary("S0").PartitionControl.Single(item => item.CornerNumber == corner);
            foreach (var candidate in Result.Value.Primary.Where(item => item.CandidateId != "S0"))
            {
                var actual = candidate.PartitionControl.Single(item => item.CornerNumber == corner);
                Assert.Equal(BitConverter.SingleToInt32Bits(
                        expected.LastRawNodeAtOrBeforeWindowStartProgress),
                    BitConverter.SingleToInt32Bits(actual.LastRawNodeAtOrBeforeWindowStartProgress));
                Assert.Equal(BitConverter.SingleToInt32Bits(
                        expected.LastRawNodeAtOrBeforeWindowStartSpeedMetersPerSecond),
                    BitConverter.SingleToInt32Bits(
                        actual.LastRawNodeAtOrBeforeWindowStartSpeedMetersPerSecond));
            }
        }
    }

    [Fact]
    public void AppliedScrubForceScalesExactlyWithCandidateStrength()
    {
        var peaks = new[] { .25f, .50f, .75f, 1f };
        var firstApplied = peaks.Select(peak => TraverseIsolated(peak).Nodes
            .First(node => node.ScrubApplied)).ToArray();
        Assert.Single(firstApplied.Select(node => BitConverter.SingleToInt32Bits(node.CornerProgress))
            .Distinct());
        Assert.Single(firstApplied.Select(node => BitConverter.SingleToInt32Bits(
            node.ScrubAppliedDistanceMeters)).Distinct());
        for (var index = 0; index < peaks.Length; index++)
        {
            Assert.Equal(firstApplied[^1].AppliedScrubWindow,
                firstApplied[index].AppliedScrubWindow);
            Assert.Equal(peaks[index] * firstApplied[index].AppliedScrubWindow,
                firstApplied[index].ScrubAccelerationMetersPerSecondSquared, 6);
            Assert.Equal(LongitudinalDynamics.ProvisionalNominalSystemMassKilograms
                    * firstApplied[index].ScrubAccelerationMetersPerSecondSquared,
                firstApplied[index].ScrubForceNewtons, 5);
            Assert.Equal(peaks[index],
                firstApplied[index].ScrubAccelerationMetersPerSecondSquared
                / firstApplied[^1].ScrubAccelerationMetersPerSecondSquared, 5);
        }
    }

    [Fact]
    public void ExperimentSelectorIsInternalAndPriorAdjustmentsAreAbsent()
    {
        var property = typeof(HeatSimulationOptions).GetProperty(
            "PreApexScrubLossAdjustment", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        Assert.False(property!.GetMethod!.IsPublic);
        Assert.True(Result.Value.Freeze.CornerResistanceExperimentAbsent);
    }

    [Fact]
    public void TrueApexAndMinimumRemainSeparateObservations()
    {
        foreach (var item in Result.Value.Primary)
        {
            Assert.Equal(2, item.ProfilePoints.Count(point => point.CornerProgress == .5f));
            Assert.InRange(item.MinimumSpeedCornerProgress, 0d, 1d);
            Assert.InRange(item.MinimumSpeedCornerNumber, 1, 2);
            Assert.True(item.TrueApexSpeedMetersPerSecond > 0d);
            Assert.True(item.MinimumSpeedMetersPerSecond > 0d);
        }
    }

    [Fact]
    public void EnvelopeCorrectionCapabilityAndTargetsAreFrozen()
    {
        Assert.True(Result.Value.Freeze.EnvelopeExact);
        Assert.True(Result.Value.Freeze.CorrectionCapabilityExact);
        Assert.True(Result.Value.Freeze.CorrectionTargetExact);
        foreach (var group in Result.Value.Primary.SelectMany(item => item.ProfilePoints)
                     .GroupBy(item => (item.CornerNumber, item.CornerProgress)))
            Assert.Single(group.Select(item => BitConverter.SingleToInt32Bits(
                item.EnvelopeSpeedMetersPerSecond)).Distinct());
    }

    [Fact]
    public void DistanceBucketsAreExclusiveAndConservedWithoutOverlap()
    {
        foreach (var item in AllHeatObservations())
        {
            var d = item.DistanceBuckets;
            Assert.True(d.Conserved, item.ScenarioId);
            var exclusive = d.CorrectionDistanceMeters + d.ScrubDistanceMeters
                + d.PositiveDriveDistanceMeters + d.NegativeSignedDriveDistanceMeters
                + d.NeutralCarryDistanceMeters;
            Assert.InRange(Math.Abs(d.TravelledDistanceMeters - exclusive), 0d,
                Math.Max(1e-5d, d.TravelledDistanceMeters * 1e-5d));
            Assert.True(d.ScrubWhileDriveZeroDistanceMeters + d.ScrubWhileDrivePositiveDistanceMeters
                <= d.ScrubDistanceMeters + 1e-5d);
        }
        Assert.All(Result.Value.Isolated, item => Assert.True(item.DistanceBuckets.Conserved));
    }

    [Fact]
    public void ScrubIsDissipativePreApexOnlyAndS0WorkIsZero()
    {
        Assert.Equal(0d, Primary("S0").Energy.ScrubWorkJoules);
        Assert.Equal(0d, Isolated("S0").Energy.ScrubWorkJoules);
        Assert.All(Result.Value.Primary, item => Assert.True(item.Energy.ScrubWorkJoules >= 0d));
        Assert.All(Result.Value.Isolated, item => Assert.True(item.Energy.ScrubWorkJoules >= 0d));
        foreach (var point in Result.Value.Primary.SelectMany(item => item.ProfilePoints))
        {
            Assert.True(point.ScrubAccelerationMetersPerSecondSquared >= 0f);
            Assert.True(point.ScrubForceNewtons >= 0f);
            if (point.CornerProgress >= .5f)
            {
                Assert.Equal(0f, point.ScrubWindow);
                Assert.Equal(0f, point.ScrubAccelerationMetersPerSecondSquared);
                Assert.Equal(0f, point.ScrubForceNewtons);
            }
        }
    }

    [Fact]
    public void CorrectionOwnsDistanceBeforeScrubRemainder()
    {
        var full = Isolated("S100");
        Assert.True(full.DistanceBuckets.CorrectionDistanceMeters > 0d);
        Assert.True(full.DistanceBuckets.ScrubDistanceMeters > 0d);
        Assert.NotNull(full.FirstCorrectionProgress);
        Assert.NotNull(full.LastCorrectionProgress);
        Assert.NotNull(full.FirstScrubProgress);
        Assert.NotNull(full.LastScrubProgress);
        Assert.True(full.LastScrubProgress <= .5f);
        Assert.True(full.DistanceBuckets.Conserved);
    }

    [Fact]
    public void StandingStartStraightLegacyThresholdsAndScenarioSuiteAreExact()
    {
        var freeze = Result.Value.Freeze;
        Assert.True(freeze.StandingStartExact);
        Assert.True(freeze.StraightExact);
        Assert.True(freeze.LegacyExact);
        Assert.True(freeze.SegmentPhysicsThresholdsExact);
        Assert.True(freeze.FullProductionScenarioSuiteExact);
        Assert.Equal(.24d, freeze.ReactionTimeSeconds, 6);
        Assert.Equal(2.237621d, freeze.TimeTo70KphSeconds, 6);
        Assert.Equal(62.332127d, freeze.SpeedAtTwoSecondsKilometersPerHour, 5);
    }

    [Fact]
    public void AllNormalAndExtremeRunsRemainFinitePositiveAndIncidentFree()
    {
        foreach (var item in AllHeatObservations())
        {
            Assert.All(new[]
            {
                item.VmaxKilometersPerHour,
                item.FlyingLapMedianSeconds,
                item.TrueApexSpeedMetersPerSecond,
                item.MinimumSpeedMetersPerSecond,
                item.CornerExitSpeedMetersPerSecond,
            }, value => Assert.True(double.IsFinite(value) && value > 0d));
            Assert.False(item.AnyZeroSpeedEvent);
            Assert.Equal(0, item.BrakeCount);
            Assert.Equal(0, item.RunWideCount);
            Assert.Equal(0, item.CrashCount);
        }
    }

    [Fact]
    public void SkillAndSlideControlOrderingRemainLogical()
    {
        foreach (var candidate in new[] { "S0", "S100" })
        {
            var speeds = Result.Value.SpeedSweep.Where(item => item.CandidateId == candidate)
                .OrderBy(item => item.SkillSpeed).ToArray();
            Assert.True(speeds.Zip(speeds.Skip(1)).All(pair =>
                pair.Second.VmaxKilometersPerHour > pair.First.VmaxKilometersPerHour
                && pair.Second.FlyingLapMedianSeconds < pair.First.FlyingLapMedianSeconds));
            var slides = Result.Value.SlideControlSweep.Where(item => item.CandidateId == candidate)
                .OrderBy(item => item.SkillSlideControl).ToArray();
            Assert.True(slides.Zip(slides.Skip(1)).All(pair =>
                pair.Second.FlyingLapMedianSeconds < pair.First.FlyingLapMedianSeconds));
        }
    }

    [Fact]
    public void LineAndDistanceMatchedSweepsRetainExpectedGeometry()
    {
        foreach (var candidate in new[] { "S0", "S100" })
        {
            var lines = Result.Value.LineSweep.Where(item => item.CandidateId == candidate)
                .OrderBy(item => item.LateralPosition).ToArray();
            Assert.Equal(5, lines.Length);
            Assert.True(lines.Zip(lines.Skip(1)).All(pair =>
                pair.Second.ModeledFourLapDistanceMeters > pair.First.ModeledFourLapDistanceMeters));
        }
        Assert.All(Result.Value.DistanceMatched,
            item => Assert.InRange(item.ModeledFourLapDistanceMeters, 1376.9d, 1377.1d));
    }

    [Fact]
    public void ResultClassifiesConservativeScrubDistanceWithoutSelectingProductionCandidate()
    {
        Assert.Equal("ConservativeScrubDistanceInsufficient", Result.Value.Classification);
        Assert.Equal("slip-loss integration with active correction", Result.Value.NextSubsystem);
        Assert.True(Primary("S100").DistanceBuckets.ScrubDistanceMeters
            < Primary("S100").DistanceBuckets.TravelledDistanceMeters * .01d);
        Assert.True(Primary("S0").CornerExitSpeedMetersPerSecond
            - Primary("S100").CornerExitSpeedMetersPerSecond
            < PreApexScrubLossExperiment.CornerResistanceR100ExitPenaltyMetersPerSecond);
    }

    [Fact]
    public void SameSeedIsDeterministicAndAllTwentyFourPermutationsAreInvariant()
    {
        Assert.Equal(24, PreApexScrubLossExperiment.RiderPermutationCount);
        Assert.Equal(1, Result.Value.PermutationDistinctTraceHashes["S0"]);
        Assert.Equal(1, Result.Value.PermutationDistinctTraceHashes["S100"]);
        var second = PreApexScrubLossExperiment.Run();
        Assert.Equal(PreApexScrubLossExperimentReport.Render(Result.Value),
            PreApexScrubLossExperimentReport.Render(second));
    }

    [Fact]
    public void ExperimentPathHasNoGlobalMutableState()
    {
        var types = new[]
        {
            typeof(PreApexScrubLossExperiment),
            typeof(PreApexScrubLossExperimentReport),
            typeof(PreApexScrubLossExperiment).Assembly.GetType(
                "CoreSim.PreApexScrubLossAdjustment", throwOnError: true)!,
        };
        foreach (var field in types.SelectMany(type => type.GetFields(
                     BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
            Assert.True(field.IsInitOnly || field.IsLiteral,
                $"Mutable static field: {field.DeclaringType}.{field.Name}");
    }

    [Fact]
    public void ReportIsCommittedByteStableInvariantCultureLfOnlyAndComplete()
    {
        var expected = CanonicalText(Path.Combine(
            Root, "docs", "calibration", "pre-apex-scrub-loss-experiment.md"));
        string Render(string culture)
        {
            var prior = CultureInfo.CurrentCulture;
            var priorUi = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                return PreApexScrubLossExperimentReport.Render(Result.Value);
            }
            finally
            {
                CultureInfo.CurrentCulture = prior;
                CultureInfo.CurrentUICulture = priorUi;
            }
        }

        var en = Render("en-US");
        var pl = Render("pl-PL");
        HistoricalPhysicsSource.AssertArtifactUnchanged("docs/calibration/pre-apex-scrub-loss-experiment.md");
        Assert.Equal(en, pl);
        Assert.DoesNotContain('\r', en);
        Assert.DoesNotContain("NaN", expected, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", expected, StringComparison.Ordinal);
        foreach (var section in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
            Assert.Contains($"## {section}.", en, StringComparison.Ordinal);
    }

    [Fact]
    public void PgeV1AndReports38To42RemainCanonicalContentIdentical()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["data/calibration/pge/v1/literature_targets.json"] = "0BA6F118F0FC3F8A6298302FAC59CF04E72563D8773D22D59E5E56F367FE7765",
            ["data/calibration/pge/v1/pge_matches.csv"] = "88F9450116CF1807B3884FB9A84A953763B919C0E8D03649516064391885F99B",
            ["data/calibration/pge/v1/pge_rider_heats.csv"] = "DCBFF29E5EEB11402271D083DC8AF23BCDC0D7A921001B2B8A53228690CC29DC",
            ["data/calibration/pge/v1/pge_split.csv"] = "609BC682360658F3B452E8BC71B92665CB50CAE5B084AC7B21403A886F715A2C",
            ["data/calibration/pge/v1/README.md"] = "85125FAF298604AF018491BB0A382594A053B3FB89DF80E21590E6570E601C0E",
            ["data/calibration/pge/v1/source_manifest.json"] = "F2186F38E165DB26B372623EABCA732AFC8E3070A838C56ADEEA4C8336E4B3FD",
            ["data/calibration/pge/v1/summary.json"] = "4015DF4FBE1D9A78EAD116AC2B725BE3C41721DD079558FEE27C8AB45E8431F8",
            ["docs/calibration/continuous-corner-envelope-impact.md"] = "9F97DE671EF6656E3054BC408C2B089CDC0D225208F7A6D0C5A2D36F3489CBB9",
            ["docs/calibration/motoarena-matched-venue.md"] = "E13FD2A9D3B21C7AF5F2FB8C9BBBE89EEB6A796A4AAE4299D0D46EC04807DAF9",
            ["docs/calibration/real-start-telemetry.md"] = "916B6DDB543A70D2DFCD5110F90DCDD1C345B58562BB9A3780E6CCBBF4039913",
            ["docs/calibration/straight-drive-envelope-experiment.md"] = "AC99CCC757584E9F1DB833C6F4EF7343BD109479EC964AAC013A73A50E016BA8",
            ["docs/calibration/corner-reduced-drive-resistance-experiment.md"] = "A3C75BBD06EA59BCEE97F1753EFCAACD80436676379E03D562FBE81408F11E72",
        };
        foreach (var (relative, sha) in expected)
        {
            var bytes = Encoding.UTF8.GetBytes(CanonicalText(Path.Combine(
                Root, relative.Replace('/', Path.DirectorySeparatorChar))));
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(bytes)));
        }
    }

    private static PreApexScrubHeatObservation Primary(string id) =>
        Result.Value.Primary.Single(item => item.CandidateId == id);

    private static IsolatedPreApexScrubObservation Isolated(string id) =>
        Result.Value.Isolated.Single(item => item.CandidateId == id);

    private static ContinuousCornerEnvelope CreateIsolatedEnvelope()
    {
        var track = MatchedVenueProfiles.Motoarena2026.CreateTrack(31f);
        var phase = track.CornerTopology.Resolve(1, 0f, 1f, track.Geometry)!.Value;
        return ContinuousCornerEnvelope.Create(phase, 1f, track.Geometry,
            CalibrationScenarioCatalog.Baseline.Surface,
            new RiderSkills(50f, 50f, 50f, 50f, 50f, 50f), BikeSetup.Neutral);
    }

    private static ContinuousCornerTraversalProfile TraverseIsolated(float peak)
    {
        var envelope = CreateIsolatedEnvelope();
        return envelope.TraverseWithPreApexScrubLoss(
            PreApexScrubLossExperiment.IsolatedEntrySpeedMetersPerSecond,
            0f,
            envelope.TotalLengthMeters,
            new PreApexScrubLossAdjustment(peak));
    }

    private static IEnumerable<PreApexScrubHeatObservation> AllHeatObservations() =>
        Result.Value.Primary
            .Concat(Result.Value.DistanceMatched)
            .Concat(Result.Value.LineSweep)
            .Concat(Result.Value.SpeedSweep)
            .Concat(Result.Value.SlideControlSweep)
            .Concat(Result.Value.SurfaceSensitivity.Select(item => item.Observation))
            .Concat(Result.Value.Extreme);

    private static string CanonicalText(string path) => File.ReadAllText(path)
        .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SpeedwayManager.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
