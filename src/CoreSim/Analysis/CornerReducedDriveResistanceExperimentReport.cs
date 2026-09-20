using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public static class CornerReducedDriveResistanceExperimentReport
{
    public static string Render(CornerReducedDriveResistanceExperimentResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var b = new StringBuilder();
        void Line(string value = "") => b.Append(value).Append('\n');
        void Heading(string title) { Line(); Line($"## {title}"); Line(); }
        void Table(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
        {
            var h = headers.ToArray();
            Line("| " + string.Join(" | ", h) + " |");
            Line("| " + string.Join(" | ", h.Select(_ => "---")) + " |");
            foreach (var row in rows)
                Line("| " + string.Join(" | ", row.Select(Cell)) + " |");
            Line();
        }

        Line("# Corner reduced-drive resistance experiment (#42)");
        Line();
        Line($"Base main SHA: `{CornerReducedDriveResistanceExperiment.BaseMainSha}` (`Test straight drive-envelope shape (#41)`).");
        Line("Diagnostic experiment only. No candidate is a production default; default/null options remain post-#41 production.");

        Heading("A. Hypothesis");
        Line("#41 classified the straight-only menu as `StraightEnvelopeShapeInsufficient`: Vmax increased while the already-fast flying lap became shorter. #42 tests whether the continuous-corner model loses too little energy because reduced `DriveAvailability` currently scales both available drive and existing longitudinal resistance.");
        Line("The experiment does not assume the current equation is a bug and does not fit a candidate to the PGE median.");

        Heading("B. Current production force semantics");
        Line("Reviewed production uses `availability × (F_drive - F_resistance) / mass`. At availability zero, net acceleration is exactly zero. R0 and a missing experiment option preserve this operation order and its float results.");

        Heading("C. Alternative force decomposition");
        Line("Candidates use `F_drive_available = availability × F_drive` and `F_resistance_exposed = lerp(availability × F_resistance, F_resistance, exposure)`, then `F_net = F_drive_available - F_resistance_exposed`. The resistance itself remains exactly `40 + 0.20 × v²` N and mass remains 142 kg. No slide drag, tyre scrub, banking, lean-angle term, engine braking or wheel-slip model is added.");

        Heading("D. Candidate definitions");
        Table(new[] { "Candidate", "ReducedDriveResistanceExposure", "Status" },
            result.Candidates.Select(item => new object?[]
            {
                item.Id, item.ReducedDriveResistanceExposure,
                item.Id == "R0" ? "production baseline" : "calibration-only diagnostic",
            }));

        Heading("E. Proof that R0 equals production");
        var f = result.Freeze;
        Table(new[] { "Invariant", "Result" }, new[]
        {
            Row("default production trace exact", Yn(f.ProductionDefaultExact)),
            Row("R0 trace exact production", Yn(f.R0ExactProduction)),
            Row("R0 force identity", Yn(f.R0ForceIdentity)),
            Row("availability=1 exact for R0…R100", Yn(f.AvailabilityOneExact)),
            Row("envelope exact for R0…R100", Yn(f.EnvelopeExact)),
            Row("correction formula/result exact", Yn(f.CorrectionExact)),
        });

        Heading("F. Motoarena L1 baseline");
        Line("Fixture: Motoarena 2026 symmetric modeled turn width 16.6 m, 31/31 m home-straight split, Dry, baseline surface, incidents off, neutral setup, all skills 50, HoldLane, seed 390039 and fixed normalized L1 rider observation. L1 is a controlled distance reference, not a real trajectory claim.");
        Table(PrimaryHeaders(), result.Primary.Select(PrimaryRow));
        Line("True apex is the mean of the two logical-corner production profile-grid speeds at canonical `p = 0.50`. Corner minimum is an independent raw production-node observation and may occur after the apex.");
        Table(new[]
        {
            "Candidate", "true apex m/s", "minimum m/s", "minimum corner", "minimum p",
            "minimum−apex m/s", "C1 minimum/p", "C2 minimum/p", "peak→minimum m/s",
        }, result.Primary.Select(item => new object?[]
        {
            item.CandidateId, item.ApexSpeedMetersPerSecond, item.MinimumSpeedMetersPerSecond,
            item.MinimumSpeedCornerNumber, item.MinimumSpeedCornerProgress,
            item.MinimumSpeedMetersPerSecond - item.ApexSpeedMetersPerSecond,
            $"{N(item.FirstCornerMinimumSpeedMetersPerSecond)} / {N(item.FirstCornerMinimumSpeedCornerProgress)}",
            $"{N(item.SecondCornerMinimumSpeedMetersPerSecond)} / {N(item.SecondCornerMinimumSpeedCornerProgress)}",
            item.PeakToMinimumAmplitudeMetersPerSecond,
        }));
        Line("Real context only: Vmax P10/P50/P90 = 108.6/113.7/116.9 km/h; Flying P10/P50/P90 = 14.56/14.87/15.15 s. Skill50 is not real P50.");

        Heading("G. Flying-lap time budget");
        Line("The budget uses the complete second modeled lap (L2). The three straight pieces and both logical corners are taken from actual production sample durations. `Flying L2` is one modeled lap and is deliberately separate from the three-lap `Flying median` reported in section F; they are not required to be equal.");
        Table(new[]
        {
            "Candidate", "home start s", "back s", "home finish s", "Straight total s",
            "C1 entry→apex s", "C1 apex→exit s", "C1 total s",
            "C2 entry→apex s", "C2 apex→exit s", "C2 total s",
            "Corner total s", "Flying L2 s", "reconciles",
        }, result.Primary.Select(item =>
        {
            var t = item.TimeBudget;
            return new object?[]
            {
                item.CandidateId, t.HomeStartStraightSeconds, t.BackStraightSeconds,
                t.HomeFinishStraightSeconds, t.TotalStraightTimeSeconds,
                t.FirstCornerEntryToApexSeconds, t.FirstCornerApexToExitSeconds,
                t.FirstCornerTotalSeconds, t.SecondCornerEntryToApexSeconds,
                t.SecondCornerApexToExitSeconds, t.SecondCornerTotalSeconds,
                t.TotalCornerTimeSeconds, t.FlyingLapTimeSeconds,
                Yn(Math.Abs(t.TotalStraightTimeSeconds + t.TotalCornerTimeSeconds
                    - t.FlyingLapTimeSeconds) <= 1e-5d),
            };
        }));

        Heading("H. Actual corner speed profile");
        Line("Canonical apex means only `p = 0.50`; it is not an alias for the lowest speed anywhere in a corner. Minimum speed and its raw-node location are reported separately in section F.");
        Table(new[]
        {
            "candidate", "corner", "p", "actual m/s", "actual km/h", "envelope m/s",
            "drive availability", "phase",
        }, result.Primary.SelectMany(item => item.ProfilePoints).Select(point => new object?[]
        {
            point.CandidateId, point.CornerNumber, point.CornerProgress,
            point.ActualSpeedMetersPerSecond, point.ActualSpeedKilometersPerHour,
            point.EnvelopeSpeedMetersPerSecond, point.DriveAvailability, Phase(point.PhaseClassification),
        }));

        Heading("I. Force decomposition by corner progress");
        Line("Values are observations carried by the production traversal nodes and interpolated only between adjacent actual nodes for the frozen p-grid; no parallel physics traversal is reconstructed.");
        Table(new[]
        {
            "candidate", "corner", "p", "drive N", "resistance N", "exposed resistance N",
            "net force N", "net acceleration m/s²", "phase",
        }, result.Primary.SelectMany(item => item.ProfilePoints).Select(point => new object?[]
        {
            point.CandidateId, point.CornerNumber, point.CornerProgress,
            point.AvailableDriveForceNewtons, point.ResistanceForceNewtons,
            point.ExposedResistanceForceNewtons, point.NetForceNewtons,
            point.NetAccelerationMetersPerSecondSquared, Phase(point.PhaseClassification),
        }));

        Heading("J. Distance buckets");
        Table(new[]
        {
            "Candidate", "travelled m", "correction m", "passive resistance m",
            "positive drive m", "negative signed drive m", "neutral carry m",
            "availability0/no-correction m", "conserved",
        }, result.Primary.Select(item => BucketRow(item.CandidateId, item.DistanceBuckets)));

        Heading("K. Kinetic-energy diagnostic");
        Line("`KE = 0.5 × 142 kg × v²`. Isolated apex energy uses canonical `p = 0.50`, never the corner minimum. This diagnostic is not asserted as a complete conservation law.");
        Table(new[] { "Candidate", "entry J", "apex J", "exit J", "Δ entry→apex J", "Δ apex→exit J" },
            result.Isolated.Select(item => new object?[]
            {
                item.CandidateId, item.Energy.EntryKineticEnergyJoules,
                item.Energy.ApexKineticEnergyJoules, item.Energy.ExitKineticEnergyJoules,
                item.Energy.EntryToApexDeltaJoules, item.Energy.ApexToExitDeltaJoules,
            }));

        Heading("L. Isolated corner");
        Line($"Fixed entry speed {N(CornerReducedDriveResistanceExperiment.IsolatedEntrySpeedMetersPerSecond)} m/s, L1, all skills 50, neutral setup, baseline surface and the production Motoarena logical-corner geometry.");
        Table(new[]
        {
            "Candidate", "entry m/s", "apex m/s", "exit m/s", "peak→apex m/s",
            "entry→apex Δv", "apex→exit Δv", "entry→apex s", "apex→exit s", "total s",
            "first correction p", "last correction p", "correction m", "target reached p",
        }, result.Isolated.Select(item => new object?[]
        {
            item.CandidateId, item.EntrySpeedMetersPerSecond, item.ApexSpeedMetersPerSecond,
            item.ExitSpeedMetersPerSecond, item.PeakToApexAmplitudeMetersPerSecond,
            item.EntryToApexDeltaSpeedMetersPerSecond, item.ApexToExitDeltaSpeedMetersPerSecond,
            item.EntryToApexTimeSeconds, item.ApexToExitTimeSeconds, item.TotalTimeSeconds,
            item.FirstCorrectionProgress, item.LastCorrectionProgress,
            item.CorrectionDistanceMeters, item.TargetReachedProgress,
        }));
        Line($"R0 first-half metres with availability=0 and no explicit correction: {N(result.Isolated.Single(item => item.CandidateId == "R0").DistanceBuckets.ZeroAvailabilityUncorrectedDistanceMeters)} m.");
        Line($"R100 same metric: {N(result.Isolated.Single(item => item.CandidateId == "R100").DistanceBuckets.ZeroAvailabilityUncorrectedDistanceMeters)} m.");

        Heading("M. Distance-matched proxy");
        Line("`LateralPosition ≈ 1.11065` is a `RealP50DistanceMatchedProxy`, not a real trajectory. The modeled distance below is the fixed-position geometric reference; production lateral execution is not redefined by this experiment.");
        Table(PrimaryHeaders(), result.DistanceMatched.Select(PrimaryRow));

        Heading("N. Line sweep");
        Table(new[] { "Candidate", "Line", "distance m", "Vmax km/h", "Flying s", "entry m/s", "true apex m/s", "minimum m/s", "minimum p", "exit m/s", "corner time s" },
            result.LineSweep.Select(item => new object?[]
            {
                item.CandidateId, $"L{N(item.LateralPosition)}", item.ModeledFourLapDistanceMeters,
                item.VmaxKilometersPerHour, item.FlyingLapMedianSeconds,
                item.CornerEntrySpeedMetersPerSecond, item.ApexSpeedMetersPerSecond,
                item.MinimumSpeedMetersPerSecond, item.MinimumSpeedCornerProgress,
                item.CornerExitSpeedMetersPerSecond, item.TimeBudget.TotalCornerTimeSeconds,
            }));

        Heading("O. Speed skill sweep");
        Table(SweepHeaders("Speed"), result.SpeedSweep.Select(item => SweepRow(item, item.SkillSpeed)));

        Heading("P. SlideControl sweep");
        Table(new[] { "Candidate", "SlideControl", "correction capability m/s²", "entry m/s", "true apex m/s", "minimum m/s", "minimum p", "corner time s", "Flying s" },
            result.SlideControlSweep.Select(item => new object?[]
            {
                item.CandidateId, item.SkillSlideControl,
                LongitudinalDynamics.CalculateCornerCorrectionDecelerationMetersPerSecondSquared(
                    new RiderSkills(50f, 50f, item.SkillSlideControl, 50f, 50f, 50f),
                    CalibrationScenarioCatalog.Baseline.Surface),
                item.CornerEntrySpeedMetersPerSecond, item.ApexSpeedMetersPerSecond,
                item.MinimumSpeedMetersPerSecond, item.MinimumSpeedCornerProgress,
                item.TimeBudget.TotalCornerTimeSeconds, item.FlyingLapMedianSeconds,
            }));

        Heading("Q. Surface sensitivity");
        Table(new[] { "Candidate", "Surface", "entry m/s", "true apex m/s", "minimum m/s", "minimum p", "exit m/s", "corner time s", "Flying s" },
            result.SurfaceSensitivity.Select(item => new object?[]
            {
                item.CandidateId, item.SurfaceId,
                item.Observation.CornerEntrySpeedMetersPerSecond,
                item.Observation.ApexSpeedMetersPerSecond,
                item.Observation.MinimumSpeedMetersPerSecond,
                item.Observation.MinimumSpeedCornerProgress,
                item.Observation.CornerExitSpeedMetersPerSecond,
                item.Observation.TimeBudget.TotalCornerTimeSeconds,
                item.Observation.FlyingLapMedianSeconds,
            }));

        Heading("R. Extreme check");
        Line("Speed100, SlideControl100, Gearing1, best tested surface and L4. No cap is added.");
        Table(new[] { "Candidate", "Vmax km/h", "entry m/s", "true apex m/s", "exit m/s", "B/RW/C", "minimum m/s", "minimum p", "zero-speed event" },
            result.Extreme.Select(item => new object?[]
            {
                item.CandidateId, item.VmaxKilometersPerHour,
                item.CornerEntrySpeedMetersPerSecond, item.ApexSpeedMetersPerSecond,
                item.CornerExitSpeedMetersPerSecond,
                $"{item.BrakeCount}/{item.RunWideCount}/{item.CrashCount}",
                item.MinimumSpeedMetersPerSecond, item.MinimumSpeedCornerProgress,
                Yn(item.AnyZeroSpeedEvent),
            }));

        Heading("S. Frozen systems");
        Table(new[] { "System", "Exact" }, new[]
        {
            Row("StandingStart", Yn(f.StandingStartExact)),
            Row("Straight isolated law", Yn(f.StraightExact)),
            Row("Legacy", Yn(f.LegacyExact)),
            Row("SegmentPhysics thresholds", Yn(f.SegmentPhysicsThresholdsExact)),
            Row("full production Calibration Scenario Suite", Yn(f.FullProductionScenarioSuiteExact)),
        });
        Line($"Standing-start Skill50 baseline: reaction {N(f.ReactionTimeSeconds)} s; TimeTo70 {N(f.TimeTo70KphSeconds)} s; SpeedAt2s {N(f.SpeedAtTwoSecondsKilometersPerHour)} km/h.");
        Line("Straight candidate is always production A0; #42 never composes with #41 H12. Advanced reference turn speed remains 19 m/s, ApexProgress 0.50, FullDriveProgress 5/6 and correction 2.0→3.2 m/s². Legacy, incident thresholds, contacts, surfaces and standing start are unchanged.");

        Heading("T. Pareto interpretation");
        var baseline = result.Primary.Single(item => item.CandidateId == "R0");
        Table(new[]
        {
            "Candidate", "ΔFlying s", "ΔCornerTime s", "ΔEntry→Apex s", "ΔApex→Exit s",
            "ΔTrueApex m/s", "ΔMinimum m/s", "minimum p", "ΔExit m/s", "ΔVmax km/h",
            "true peak→apex m/s", "peak→minimum m/s", "Extreme",
        }, result.Primary.Select(item =>
        {
            var extreme = result.Extreme.SingleOrDefault(value => value.CandidateId == item.CandidateId);
            return new object?[]
            {
                item.CandidateId,
                item.FlyingLapMedianSeconds - baseline.FlyingLapMedianSeconds,
                item.TimeBudget.TotalCornerTimeSeconds - baseline.TimeBudget.TotalCornerTimeSeconds,
                EntryApex(item) - EntryApex(baseline),
                ApexExit(item) - ApexExit(baseline),
                item.ApexSpeedMetersPerSecond - baseline.ApexSpeedMetersPerSecond,
                item.MinimumSpeedMetersPerSecond - baseline.MinimumSpeedMetersPerSecond,
                item.MinimumSpeedCornerProgress,
                item.CornerExitSpeedMetersPerSecond - baseline.CornerExitSpeedMetersPerSecond,
                item.VmaxKilometersPerHour - baseline.VmaxKilometersPerHour,
                item.PeakToApexAmplitudeMetersPerSecond,
                item.PeakToMinimumAmplitudeMetersPerSecond,
                extreme is null ? "n/a" : extreme.AnyZeroSpeedEvent ? "zero-speed" : "finite/no-zero",
            };
        }));
        Line($"Classification: `{result.Classification}`.");

        Heading("U. Next-model decision");
        var r100 = result.Primary.Single(item => item.CandidateId == "R100");
        var pre = EntryApex(r100) - EntryApex(baseline);
        var post = ApexExit(r100) - ApexExit(baseline);
        Line($"Resistance exposure alone changes the flying median by {N(r100.FlyingLapMedianSeconds - baseline.FlyingLapMedianSeconds)} s at R100. The L2 two-corner time effect is {N(pre)} s pre-apex and {N(post)} s post-apex; the dominant location is `{(Math.Abs(pre) >= Math.Abs(post) ? "pre-apex" : "post-apex")}`.");
        Line($"R100 changes true apex by {N(r100.ApexSpeedMetersPerSecond - baseline.ApexSpeedMetersPerSecond)} m/s, while its minimum is {N(r100.MinimumSpeedMetersPerSecond)} m/s at p={N(r100.MinimumSpeedCornerProgress)} versus R0 {N(baseline.MinimumSpeedMetersPerSecond)} m/s at p={N(baseline.MinimumSpeedCornerProgress)}. The lower post-apex minimum, lower exit and dominant post-apex time delta are direct evidence for `LossLocationMismatch`; minimum is not relabeled as apex.");
        Line($"The one subsystem proposed for #43 is `{result.NextSubsystem}`. #42 selects no production Rxx value.");

        Heading("V. Limitations");
        Line("- No real per-point corner-speed telemetry.");
        Line("- No real apex speed or throttle trace.");
        Line("- No lean angle, wheel slip, banking or physical rider trajectory.");
        Line("- The existing resistance model itself remains provisional.");
        Line("- Motoarena geometry remains a symmetric approximation.");
        Line("- L1 is a controlled reference, not a real trajectory.");
        Line("- Interpolated p-grid observations lie between actual 1 m traversal nodes and do not introduce another simulator.");

        return b.ToString();

        static IEnumerable<string> PrimaryHeaders() => new[]
        {
            "Candidate", "modeled 4-lap distance m", "Vmax km/h", "Flying median s",
            "Heat s", "Average m/s", "entry m/s", "true apex p=.50 m/s", "exit m/s",
            "peak→true-apex m/s", "minimum m/s", "minimum p", "corner L2 s", "straight L2 s", "B/RW/C",
        };
        static IEnumerable<object?> PrimaryRow(CornerResistanceHeatObservation item) => new object?[]
        {
            item.CandidateId, item.ModeledFourLapDistanceMeters, item.VmaxKilometersPerHour,
            item.FlyingLapMedianSeconds, item.HeatTimeSeconds, item.AverageSpeedMetersPerSecond,
            item.CornerEntrySpeedMetersPerSecond, item.ApexSpeedMetersPerSecond,
            item.CornerExitSpeedMetersPerSecond, item.PeakToApexAmplitudeMetersPerSecond,
            item.MinimumSpeedMetersPerSecond, item.MinimumSpeedCornerProgress,
            item.TimeBudget.TotalCornerTimeSeconds, item.TimeBudget.TotalStraightTimeSeconds,
            $"{item.BrakeCount}/{item.RunWideCount}/{item.CrashCount}",
        };
        static IEnumerable<object?> BucketRow(string id, CornerResistanceDistanceBuckets bucket) => new object?[]
        {
            id, bucket.TravelledDistanceMeters, bucket.CorrectionDistanceMeters,
            bucket.PassiveResistanceDistanceMeters, bucket.PositiveDriveDistanceMeters,
            bucket.NegativeSignedDriveDistanceMeters, bucket.NeutralCarryDistanceMeters,
            bucket.ZeroAvailabilityUncorrectedDistanceMeters, Yn(bucket.Conserved),
        };
        static IEnumerable<string> SweepHeaders(string axis) => new[]
        {
            "Candidate", axis, "Vmax km/h", "Flying s", "entry m/s", "true apex m/s",
            "minimum m/s", "minimum p", "exit m/s", "B/RW/C",
        };
        static IEnumerable<object?> SweepRow(CornerResistanceHeatObservation item, float axis) => new object?[]
        {
            item.CandidateId, axis, item.VmaxKilometersPerHour, item.FlyingLapMedianSeconds,
            item.CornerEntrySpeedMetersPerSecond, item.ApexSpeedMetersPerSecond,
            item.MinimumSpeedMetersPerSecond, item.MinimumSpeedCornerProgress,
            item.CornerExitSpeedMetersPerSecond,
            $"{item.BrakeCount}/{item.RunWideCount}/{item.CrashCount}",
        };
        static object?[] Row(object? a, object? b) => new[] { a, b };
        static double EntryApex(CornerResistanceHeatObservation item) =>
            item.TimeBudget.FirstCornerEntryToApexSeconds + item.TimeBudget.SecondCornerEntryToApexSeconds;
        static double ApexExit(CornerResistanceHeatObservation item) =>
            item.TimeBudget.FirstCornerApexToExitSeconds + item.TimeBudget.SecondCornerApexToExitSeconds;
    }

    private static string Phase(ContinuousCornerPhaseClassification value) => value switch
    {
        ContinuousCornerPhaseClassification.Correction => "correction",
        ContinuousCornerPhaseClassification.CarryPassive => "carry/passive",
        ContinuousCornerPhaseClassification.SignedDriveAcceleration => "signed-drive acceleration",
        ContinuousCornerPhaseClassification.SignedDriveDeceleration => "signed-drive deceleration",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static string Yn(bool value) => value ? "YES" : "NO";
    private static string Cell(object? value) => value switch
    {
        null => "—",
        float item => N(item),
        double item => N(item),
        _ => value.ToString()!.Replace("|", "\\|", StringComparison.Ordinal),
    };
    private static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
