using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CoreSim.Analysis;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class ActiveCorrectionControlLossExperimentTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly Lazy<ActiveCorrectionControlLossExperimentResult> Result =
        new(ActiveCorrectionControlLossExperiment.Run);

    [Fact]
    public void CandidateMenuIsExactAndCandidateRangeIsValidated()
    {
        Assert.Equal(new[] { "C0", "C05", "C10", "C15", "C20" },
            ActiveCorrectionControlLossExperiment.CandidateMenu.Select(item => item.Id));
        Assert.Equal(new[] { 0f, .05f, .10f, .15f, .20f },
            ActiveCorrectionControlLossExperiment.CandidateMenu
                .Select(item => item.MaxControlLossFraction));

        foreach (var invalid in new[]
                 {
                     float.NegativeInfinity, -.0001f, 1.0001f,
                     float.PositiveInfinity, float.NaN,
                 })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ActiveCorrectionControlLossCandidate("invalid", invalid));
    }

    [Fact]
    public void ControlLoadIsClampedAndItsInputsAreValidated()
    {
        Assert.Equal(0f, ActiveCorrectionControlLossExperiment.ControlLoad(0f, 1f));
        Assert.Equal(.5f, ActiveCorrectionControlLossExperiment.ControlLoad(.5f, 1f));
        Assert.Equal(1f, ActiveCorrectionControlLossExperiment.ControlLoad(2f, 1f));
        foreach (var invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ActiveCorrectionControlLossExperiment.ControlLoad(invalid, 1f));
        foreach (var invalid in new[] { -1f, 0f, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ActiveCorrectionControlLossExperiment.ControlLoad(1f, invalid));
    }

    [Fact]
    public void SurfaceChallengeIsBoundedAndGoodSurfaceIsExactlyNeutral()
    {
        Assert.Equal(1f, ActiveCorrectionControlLossExperiment.SurfaceChallenge(0f));
        Assert.Equal(1f / 3f,
            ActiveCorrectionControlLossExperiment.SurfaceChallenge(.8f), 6);
        Assert.Equal(0f, ActiveCorrectionControlLossExperiment.SurfaceChallenge(.9f));
        Assert.Equal(0f, ActiveCorrectionControlLossExperiment.SurfaceChallenge(1f));
        foreach (var invalid in new[] { -.001f, 1.001f, float.NaN })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ActiveCorrectionControlLossExperiment.SurfaceChallenge(invalid));
    }

    [Fact]
    public void AdaptabilityOnlyReducesDifficultSurfacePenalty()
    {
        foreach (var adaptability in new[] { 0f, 20f, 50f, 80f, 100f })
            Assert.Equal(0f, ActiveCorrectionControlLossExperiment
                .SurfaceAdaptationPenalty(.9f, adaptability));

        var penalties = new[] { 20f, 50f, 80f }.Select(value =>
            ActiveCorrectionControlLossExperiment.SurfaceAdaptationPenalty(.8f, value)).ToArray();
        Assert.True(penalties.Zip(penalties.Skip(1)).All(pair => pair.Second < pair.First));
        foreach (var invalid in new[] { -1f, 101f, float.NaN })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ActiveCorrectionControlLossExperiment.SurfaceAdaptationPenalty(.8f, invalid));
    }

    [Fact]
    public void PressureUsesFrozenSeventyFiveTwentyFiveWeighting()
    {
        Assert.Equal(0f, ActiveCorrectionControlLossExperiment.ControlLossPressure(0f, 1f));
        Assert.Equal(.75f,
            ActiveCorrectionControlLossExperiment.ControlLossPressure(1f, 0f));
        Assert.Equal(1f,
            ActiveCorrectionControlLossExperiment.ControlLossPressure(1f, 1f));
        Assert.Equal(.4375f,
            ActiveCorrectionControlLossExperiment.ControlLossPressure(.5f, .5f));
    }

    [Fact]
    public void EnergyFormulaIsNonNegativeAndC0IsIdentity()
    {
        const float entry = 27f;
        const float productionExit = 26f;
        var removed = ActiveCorrectionControlLoss.CorrectionEnergyRemovedJoules(
            entry, productionExit);
        Assert.Equal(.5d * 142d * (entry * entry - productionExit * productionExit), removed);
        var zero = ActiveCorrectionControlLoss.ControlLossEnergyJoules(
            removed, new ActiveCorrectionControlLossAdjustment(0f), .75f);
        Assert.Equal(0d, zero);
        Assert.Equal(BitConverter.SingleToInt32Bits(productionExit),
            BitConverter.SingleToInt32Bits(
                ActiveCorrectionControlLoss.ApplyEnergyLoss(productionExit, zero)));
        var loss = ActiveCorrectionControlLoss.ControlLossEnergyJoules(
            removed, new ActiveCorrectionControlLossAdjustment(.2f), .75f);
        Assert.True(loss > 0d);
        var expected = Math.Sqrt(productionExit * productionExit - 2d * loss / 142d);
        Assert.Equal((float)expected,
            ActiveCorrectionControlLoss.ApplyEnergyLoss(productionExit, loss));
    }

    [Fact]
    public void ExperimentSelectorIsInternalNullByDefaultAndCannotCombineExperiments()
    {
        var property = typeof(HeatSimulationOptions).GetProperty(
            "ActiveCorrectionControlLossAdjustment",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        Assert.False(property!.GetMethod!.IsPublic);
        Assert.Null(property.GetValue(new HeatSimulationOptions()));
        Assert.False(typeof(ActiveCorrectionControlLossAdjustment).IsPublic);

        var combined = new HeatSimulationOptions
        {
            StraightDriveEnvelopeAdjustment = new StraightDriveEnvelopeAdjustment(1f, 1f),
            ActiveCorrectionControlLossAdjustment = new ActiveCorrectionControlLossAdjustment(.2f),
        };
        Assert.Throws<InvalidOperationException>(combined.Validate);
    }

    [Fact]
    public void DefaultC0ZeroPathAndCanonicalEndpointsAreExactProduction()
    {
        var freeze = Result.Value.Freeze;
        Assert.True(freeze.ProductionDefaultExact);
        Assert.True(freeze.C0ExactProduction);
        Assert.True(freeze.ZeroForceExperimentalPathExact);
        Assert.True(freeze.CanonicalStepEndpointsExact);

        var baseline = Primary("C0");
        Assert.Equal(97.882807, baseline.VmaxKilometersPerHour, 6);
        Assert.Equal(13.909906, baseline.FlyingLapMedianSeconds, 6);
        Assert.Equal(13.815361, baseline.FlyingL2Seconds, 6);
        Assert.Equal(22.585236, baseline.TrueApexSpeedMetersPerSecond, 6);
        Assert.Equal(24.296925, baseline.CornerExitSpeedMetersPerSecond, 6);
    }

    [Fact]
    public void ControlLossUsesOnlyExistingCorrectionOwnedDistance()
    {
        Assert.Null(typeof(ContinuousCornerTraversalProfile).GetProperty(
            "ControlLossDistanceMeters", BindingFlags.Instance | BindingFlags.Public
            | BindingFlags.NonPublic));
        Assert.Null(typeof(ActiveCorrectionHeatObservation).GetProperty(
            "ControlLossDistanceMeters", BindingFlags.Instance | BindingFlags.Public));

        foreach (var step in Result.Value.Primary.SelectMany(item => item.ControlSteps))
        {
            Assert.True(step.AppliedCorrectionDistanceMeters > 0f);
            Assert.InRange(step.AppliedCorrectionDistanceMeters, 0f,
                step.AvailableStepDistanceMeters);
            Assert.InRange(step.AppliedCorrectionDistanceMeters, 0f,
                step.RequiredCorrectionDistanceMeters + 1e-4f);
            Assert.InRange(step.ControlLoad, 0f, 1f);
        }
    }

    [Fact]
    public void ExistingCorrectionCarryDrivePartitionStillConservesTravelledDistance()
    {
        foreach (var profile in Result.Value.Primary.SelectMany(item => item.Trace.StepSamples)
                     .Where(item => item.ContinuousCornerProfile is not null)
                     .Select(item => item.ContinuousCornerProfile!))
        {
            var first = profile.Nodes[0];
            var last = profile.Nodes[^1];
            var travelled = (last.CornerProgress - first.CornerProgress)
                * last.EnvelopeTotalLengthMeters;
            var exclusive = profile.CorrectionDistanceMeters + profile.CarryDistanceMeters
                + profile.DriveDistanceMeters;
            Assert.InRange(Math.Abs(travelled - exclusive), 0f, 1e-3f);
        }
    }

    [Fact]
    public void ControlLossIsPreApexOnlyAndTargetsRemainProductionOwned()
    {
        var grouped = Result.Value.Primary.SelectMany(item => item.ControlSteps)
            .GroupBy(item => (item.CornerNumber, item.CornerProgress));
        foreach (var group in grouped)
        {
            Assert.True(group.Key.CornerProgress <= ContinuousCornerEnvelope.ApexProgress);
            Assert.Single(group.Select(item => BitConverter.SingleToInt32Bits(
                item.TargetSpeedMetersPerSecond)).Distinct());
        }

        Assert.All(Result.Value.Primary.SelectMany(item => item.ControlSteps), step =>
        {
            Assert.True(step.CorrectionEnergyRemovedJoules >= 0d);
            Assert.True(step.ControlLossEnergyJoules >= 0d);
        });
        Assert.All(Primary("C0").ControlSteps,
            step => Assert.Equal(0d, step.ControlLossEnergyJoules));
    }

    [Fact]
    public void EveryDiagnosticStepUsesTheFrozenEnergyEquations()
    {
        var fractions = ActiveCorrectionControlLossExperiment.CandidateMenu
            .ToDictionary(item => item.Id, item => item.MaxControlLossFraction,
                StringComparer.Ordinal);
        foreach (var step in Result.Value.Primary.SelectMany(item => item.ControlSteps))
        {
            var correction = .5d * 142d * Math.Max(0d,
                (double)step.EntrySpeedMetersPerSecond * step.EntrySpeedMetersPerSecond
                - (double)step.ProductionCorrectionExitSpeedMetersPerSecond
                * step.ProductionCorrectionExitSpeedMetersPerSecond);
            Assert.Equal(correction, step.CorrectionEnergyRemovedJoules, 8);
            var loss = correction * fractions[step.CandidateId] * step.ControlLossPressure;
            Assert.Equal(loss, step.ControlLossEnergyJoules, 8);
            var final = Math.Sqrt(Math.Max(0d,
                (double)step.ProductionCorrectionExitSpeedMetersPerSecond
                * step.ProductionCorrectionExitSpeedMetersPerSecond
                - 2d * loss / 142d));
            Assert.Equal((float)final, step.FinalCorrectionExitSpeedMetersPerSecond);
        }
    }

    [Fact]
    public void CandidateEnergyAndControlLossAreBoundedAndMonotonic()
    {
        var primary = Result.Value.Primary.OrderBy(item => item.CandidateId).ToArray();
        Assert.All(primary, item => Assert.True(item.ControlLossEnergyJoules >= 0d));
        Assert.Equal(0d, Primary("C0").ControlLossEnergyJoules);
        var menuOrder = ActiveCorrectionControlLossExperiment.CandidateMenu.Select(item =>
            Primary(item.Id).ControlLossEnergyJoules).ToArray();
        Assert.True(menuOrder.Zip(menuOrder.Skip(1)).All(pair => pair.Second > pair.First));
        Assert.All(primary, item =>
        {
            Assert.InRange(item.MeanControlLoad, 0d, 1d);
            Assert.InRange(item.MaximumControlLoad, 0d, 1d);
            Assert.InRange(item.MeanControlLossPressure, 0d, 1d);
            Assert.InRange(item.MaximumControlLossPressure, 0d, 1d);
        });
    }

    [Fact]
    public void TrackReadingAndRiderStyleAreAbsentFromPhysicalFormulaBoundary()
    {
        var formulaMethods = typeof(ActiveCorrectionControlLoss).GetMethods(
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.DoesNotContain(formulaMethods.SelectMany(method => method.GetParameters()),
            parameter => parameter.ParameterType == typeof(RiderSkills)
                || parameter.ParameterType == typeof(RiderStyle)
                || parameter.Name!.Contains("trackReading", StringComparison.OrdinalIgnoreCase)
                || parameter.Name.Contains("style", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FrozenProductionSystemsAndScenarioSuiteRemainExact()
    {
        var freeze = Result.Value.Freeze;
        Assert.True(freeze.EnvelopeExact);
        Assert.True(freeze.CorrectionCapabilityExact);
        Assert.True(freeze.CorrectionTargetsExact);
        Assert.True(freeze.StandingStartExact);
        Assert.True(freeze.StraightExact);
        Assert.True(freeze.PreviousExperimentsAbsent);
        Assert.True(freeze.LegacyExact);
        Assert.True(freeze.SegmentPhysicsThresholdsExact);
        Assert.True(freeze.LateralMovementModelExact);
        Assert.True(freeze.AdaptiveDecisionModelExact);
        Assert.True(freeze.FullProductionScenarioSuiteExact);
    }

    [Fact]
    public void FrozenSourceFilesRemainByteIdentical()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/CoreSim/Track/SegmentPhysics.cs"] = "693D3C911F76CB83F60CD6DED850490F101B0FE0F6558D453DC35927DC123B16",
            ["src/CoreSim/Track/LateralMovementModel.cs"] = "4A5A71D726B10D1D52C84BCFC053E63E11A4F4D21A381225DC23821D3BA9AC44",
            ["src/CoreSim/Decisions/AdaptiveDecisionModel.cs"] = "A1FCBA7E068C09B69CF849492424C89083BAD69F965FC6D767415EA9F7478314",
        };
        AssertHashes(expected);
    }

    [Fact]
    public void SlideControlOrderingStaysLogicalWithoutDoubleCountingRisk()
    {
        foreach (var candidate in new[] { "C0", "C20" })
        {
            var values = Result.Value.SlideControlSweep
                .Where(item => item.CandidateId == candidate)
                .OrderBy(item => item.SkillSlideControl).ToArray();
            Assert.True(values.Zip(values.Skip(1)).All(pair =>
                pair.Second.FlyingLapMedianSeconds < pair.First.FlyingLapMedianSeconds));
        }
        Assert.True(Result.Value.SlideControlSpreadAmplification > 0d);
        Assert.True(Result.Value.SlideControlSpreadAmplification <= 1.5d);
        Assert.False(Result.Value.SkillDoubleCountingRisk);
    }

    [Fact]
    public void SpeedOrderingIsLogicalAndHasNoPathology()
    {
        foreach (var candidate in new[] { "C0", "C20" })
        {
            var values = Result.Value.SpeedSweep.Where(item => item.CandidateId == candidate)
                .OrderBy(item => item.SkillSpeed).ToArray();
            Assert.True(values.Zip(values.Skip(1)).All(pair =>
                pair.Second.VmaxKilometersPerHour > pair.First.VmaxKilometersPerHour));
            Assert.All(values, AssertFinitePositive);
        }
    }

    [Fact]
    public void AdaptabilityIsExactlyNeutralOnGoodSurfaceAndUsefulOnRuts()
    {
        foreach (var candidate in new[] { "C0", "C20" })
        {
            var baseline = Result.Value.AdaptabilitySweep.Where(item =>
                item.CandidateId == candidate && item.SurfaceId == "baseline").ToArray();
            Assert.Single(baseline.Select(item => BitConverter.DoubleToInt64Bits(
                item.FlyingLapMedianSeconds)).Distinct());
            Assert.Single(baseline.Select(item => BitConverter.DoubleToInt64Bits(
                item.ControlLossEnergyJoules)).Distinct());
        }

        var ruts = Result.Value.AdaptabilitySweep.Where(item =>
            item.CandidateId == "C20" && item.SurfaceId == "ruts_025")
            .OrderBy(item => item.SkillAdaptability).ToArray();
        Assert.True(ruts.Zip(ruts.Skip(1)).All(pair =>
            pair.Second.ControlLossEnergyJoules < pair.First.ControlLossEnergyJoules
            && pair.Second.FlyingLapMedianSeconds < pair.First.FlyingLapMedianSeconds));
    }

    [Fact]
    public void LineGeometryAndChoiceRemainMeaningful()
    {
        foreach (var group in Result.Value.LineSweep.GroupBy(item =>
                     (item.SurfaceId, item.CandidateId)))
        {
            var lines = group.OrderBy(item => item.LateralPosition).ToArray();
            Assert.Equal(5, lines.Length);
            Assert.True(lines.Zip(lines.Skip(1)).All(pair =>
                pair.Second.ModeledFourLapDistanceMeters
                    > pair.First.ModeledFourLapDistanceMeters));
            Assert.True(lines.Select(item => item.FlyingLapMedianSeconds).Distinct().Count() > 1);
        }
        Assert.All(Result.Value.LineSpreads, spread =>
        {
            Assert.True(double.IsFinite(spread.Ratio) && spread.Ratio > 0d);
            Assert.Equal(spread.Ratio < .5d, spread.LineChoiceFlatteningRisk);
            Assert.Equal(spread.Ratio > 1.5d, spread.LineChoiceOverAmplificationRisk);
        });
    }

    [Fact]
    public void ExistingSetupTradeOffRemainsObservableWithoutNewMultiplier()
    {
        Assert.Equal(12, Result.Value.SetupSweep.Count);
        foreach (var group in Result.Value.SetupSweep.GroupBy(item =>
                     (item.SurfaceId, item.CandidateId)))
        {
            Assert.Equal(new[] { 0f, .5f, 1f },
                group.OrderBy(item => item.TractionBias).Select(item => item.TractionBias));
            Assert.True(group.Select(item => item.FlyingLapMedianSeconds).Distinct().Count() > 1);
            Assert.All(group, AssertFinitePositive);
        }
    }

    [Fact]
    public void DecisionModelSanityCoversEveryArchetypeAndRequestedSurface()
    {
        Assert.Equal(8, Result.Value.DecisionModelSanity.Count);
        Assert.Equal(8, Result.Value.DecisionModelSanity.Select(item =>
            (item.RiderProfileId, item.SurfaceId)).Distinct().Count());
        Assert.All(Result.Value.DecisionModelSanity, item =>
        {
            Assert.InRange(item.ChosenLane, 0, 4);
            Assert.True(float.IsFinite(item.Risk));
        });
    }

    [Fact]
    public void TimeBudgetReconcilesAndApexRemainsSeparateFromMinimum()
    {
        foreach (var item in Result.Value.Primary)
        {
            Assert.Equal(item.TimeBudget.TotalCornerTimeSeconds,
                item.TimeBudget.PreApexTimeSeconds + item.TimeBudget.PostApexTimeSeconds, 6);
            Assert.InRange(Math.Abs(item.FlyingL2Seconds
                - item.TimeBudget.StraightTimeSeconds
                - item.TimeBudget.TotalCornerTimeSeconds), 0d, 1e-5d);
            Assert.InRange(item.MinimumSpeedCornerProgress, 0d, 1d);
            Assert.InRange(item.MinimumSpeedCornerNumber, 1, 2);
            Assert.True(item.TrueApexSpeedMetersPerSecond > 0d);
            Assert.True(item.MinimumSpeedMetersPerSecond > 0d);
        }
        Assert.False(Result.Value.RecoveryLocationRisk);
    }

    [Fact]
    public void EveryNormalAndExtremeObservationIsFinitePositiveAndIncidentFree()
    {
        foreach (var item in AllObservations())
        {
            AssertFinitePositive(item);
            Assert.False(item.AnyZeroSpeedEvent);
            Assert.Equal(0, item.BrakeCount);
            Assert.Equal(0, item.RunWideCount);
            Assert.Equal(0, item.CrashCount);
        }
    }

    [Fact]
    public void SameSeedAndAllTwentyFourRiderPermutationsAreInvariant()
    {
        Assert.Equal(24, ActiveCorrectionControlLossExperiment.RiderPermutationCount);
        Assert.Equal(1, Result.Value.PermutationDistinctTraceHashes["C0"]);
        Assert.Equal(1, Result.Value.PermutationDistinctTraceHashes["C20"]);
        var second = ActiveCorrectionControlLossExperiment.Run();
        Assert.Equal(ActiveCorrectionControlLossExperimentReport.Render(Result.Value),
            ActiveCorrectionControlLossExperimentReport.Render(second));
    }

    [Fact]
    public void ExperimentPathHasNoGlobalMutableState()
    {
        var types = new[]
        {
            typeof(ActiveCorrectionControlLossExperiment),
            typeof(ActiveCorrectionControlLossExperimentReport),
            typeof(ActiveCorrectionControlLoss),
            typeof(ActiveCorrectionControlLossAdjustment),
        };
        foreach (var field in types.SelectMany(type => type.GetFields(
                     BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)))
            Assert.True(field.IsInitOnly || field.IsLiteral,
                $"Mutable static field: {field.DeclaringType}.{field.Name}");
    }

    [Fact]
    public void ClassificationIsDiagnosticAndDoesNotSelectProductionCandidate()
    {
        Assert.Contains(Result.Value.Classification, new[]
        {
            "GameplayControlLossPlausiblyUseful", "GameplaySignalTooWeak",
            "SkillDoubleCountingRisk", "LineChoiceDistortionRisk",
            "ControlLossOverpowered", "MixedGameplayTradeoff",
        });
        Assert.Equal("GameplaySignalTooWeak", Result.Value.Classification);
        Assert.Equal("bounded active-correction demand shaping", Result.Value.NextSubsystem);
    }

    [Fact]
    public void ReportIsCommittedByteStableInvariantCultureLfOnlyAndComplete()
    {
        var path = Path.Combine(Root, "docs", "calibration",
            "gameplay-corner-control-loss-experiment.md");
        var expected = File.ReadAllText(path);
        Assert.DoesNotContain('\r', expected);

        string Render(string culture)
        {
            var prior = CultureInfo.CurrentCulture;
            var priorUi = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                return ActiveCorrectionControlLossExperimentReport.Render(Result.Value);
            }
            finally
            {
                CultureInfo.CurrentCulture = prior;
                CultureInfo.CurrentUICulture = priorUi;
            }
        }

        var en = Render("en-US");
        var pl = Render("pl-PL");
        Assert.Equal(expected, en);
        Assert.Equal(en, pl);
        Assert.DoesNotContain('\r', en);
        Assert.DoesNotContain("NaN", en, StringComparison.Ordinal);
        Assert.DoesNotContain("Infinity", en, StringComparison.Ordinal);
        foreach (var section in "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
            Assert.Contains($"## {section}.", en, StringComparison.Ordinal);
    }

    [Fact]
    public void PgeV1AndReports38To43RemainByteIdentical()
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
            ["docs/calibration/pre-apex-scrub-loss-experiment.md"] = "7ABD8AF54C9E8FB8B9E1D281F911D45F00505DE42FBF7C7363D077E2D02CB5B3",
        };
        AssertHashes(expected);
    }

    private static ActiveCorrectionHeatObservation Primary(string id) =>
        Result.Value.Primary.Single(item => item.CandidateId == id);

    private static IEnumerable<ActiveCorrectionHeatObservation> AllObservations() =>
        Result.Value.Primary
            .Concat(Result.Value.ArchetypeSurfaceMatrix)
            .Concat(Result.Value.SlideControlSweep)
            .Concat(Result.Value.AdaptabilitySweep)
            .Concat(Result.Value.SpeedSweep)
            .Concat(Result.Value.LineSweep)
            .Concat(Result.Value.SetupSweep)
            .Concat(Result.Value.Extremes);

    private static void AssertFinitePositive(ActiveCorrectionHeatObservation item)
    {
        Assert.All(new[]
        {
            item.ModeledFourLapDistanceMeters,
            item.VmaxKilometersPerHour,
            item.FlyingLapMedianSeconds,
            item.FlyingL2Seconds,
            item.HeatTimeSeconds,
            item.AverageSpeedMetersPerSecond,
            item.StraightPeakSpeedMetersPerSecond,
            item.CornerEntrySpeedMetersPerSecond,
            item.TrueApexSpeedMetersPerSecond,
            item.MinimumSpeedMetersPerSecond,
            item.CornerExitSpeedMetersPerSecond,
            item.CorrectionDistanceMeters,
            item.CorrectionTimeSeconds,
            item.CorrectionEnergyRemovedJoules,
        }, value => Assert.True(double.IsFinite(value) && value > 0d, item.ScenarioId));
        Assert.True(double.IsFinite(item.ControlLossEnergyJoules)
            && item.ControlLossEnergyJoules >= 0d, item.ScenarioId);
    }

    private static void AssertHashes(IReadOnlyDictionary<string, string> expected)
    {
        foreach (var (relative, sha) in expected)
        {
            var path = Path.Combine(Root,
                relative.Replace('/', Path.DirectorySeparatorChar));
            var text = File.ReadAllText(path)
                .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(text))));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SpeedwayManager.sln")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
