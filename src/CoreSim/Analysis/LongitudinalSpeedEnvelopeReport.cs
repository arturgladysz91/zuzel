using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

/// <summary>Pure deterministic renderer for the PR #36 before/after evidence.</summary>
public static class LongitudinalSpeedEnvelopeReport
{
    public static string Render(LongitudinalCalibrationSnapshot before,
        LongitudinalCalibrationSnapshot after,
        IReadOnlyDictionary<string, CalibrationDistribution> realDistributions,
        string baseMainSha,
        string candidateHeadSha)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(realDistributions);
        ValidateSha(baseMainSha, nameof(baseMainSha));
        ValidateSha(candidateHeadSha, nameof(candidateHeadSha));
        if (!StringComparer.Ordinal.Equals(before.SourceSha, baseMainSha))
            throw new ArgumentException("The before snapshot must come from the declared base main SHA.", nameof(before));
        if (!StringComparer.Ordinal.Equals(after.SourceSha, candidateHeadSha))
            throw new ArgumentException("The after snapshot must come from the declared candidate HEAD.", nameof(after));

        var b = new StringBuilder();
        Line("# Longitudinal speed-envelope calibration impact");
        Line();
        Line($"Base main SHA: `{baseMainSha}`");
        Line();
        Line($"Branch candidate HEAD used to generate the after-state: `{candidateHeadSha}`");
        Line();
        Line("The candidate HEAD is the code-only calibration checkpoint used before materializing this generated report; the Draft PR body records the final one-commit branch HEAD. A commit cannot contain its own final SHA because that text changes the commit hash.");
        Heading("Scope and method");
        Line("This is the first bounded calibration pass over the existing signed-force model. No cap, optimizer, alternate heat loop or surrogate traversal was added. Straight and TurnExit observations call the production `LongitudinalDynamics` profiles; controlled corner/start observations reuse the #35 `CalibrationScenarioSuite`; complete heats run through `CalibrationRunner → HeatSimulator → SimulationEngine` with normal production surface evolution.");
        Line();
        Line("The signed model remains `F_net(v) = F_drive(v) - F_resistance(v)` and `a(v) = F_net(v) / 142 kg`. `F_resistance(v) = 40 + 0.20v²` N. Equilibrium is a diagnostic root, never a limiter, clamp, target or integration stop. The reported effective power diagnostic is only `F_drive(v) × v`; it is not literal crankshaft power and does not introduce RPM, torque, sprocket, wheelspin, slip, traction-cap or engine-efficiency physics.");
        Line();
        Line("The search followed the prescribed hierarchy. At each stage only a small deterministic menu was evaluated, with no weighted objective: Straight reference acceleration 1.5× then 2×; TurnExit reference acceleration 1.5× then 2×; then both fade endpoints 1.5× and 2×. Reference speed and envelope shape were not opened because Stage C produced finite-distance gains with credible signed-force equilibria and a useful gearing crossover.");

        Heading("Bounded candidate screen");
        Line("All heat medians below are the four-rider `full_heat/speed/050` production fixture. Candidate rows are sequential: later stages include the selected earlier-stage values.");
        Line();
        Table("Candidate | Straight range | TurnExit range | Fade drive/speed | Straight 60 m exit | Straight equilibrium | TurnExit exit | TurnExit equilibrium | Heat Vmax P50 km/h | Heat average P50 m/s | Heat L1 penalty P50 s | Decision", new[]
        {
            Row("Baseline", "0.80–1.60", "0.60–1.40", "0.01750/0.00500", 19.59488, 30.02662, 18.08419, 28.35396, 76.432554, 18.14121, 1.057449, "diagnosis"),
            Row("A1", "1.20–2.40", "0.60–1.40", "0.01750/0.00500", 21.12020, 34.30593, 18.08419, 28.35396, 79.520327, 18.284455, 1.097585, "retain A2 instead"),
            Row("A2", "1.60–3.20", "0.60–1.40", "0.01750/0.00500", 22.51816, 37.81421, 18.08419, 28.35396, 81.846088, 18.39057, 1.118208, "retain"),
            Row("B1", "1.60–3.20", "0.90–2.10", "0.01750/0.00500", 22.51816, 37.81421, 18.63987, 32.28527, 82.916452, 18.579705, 1.127445, "retain B2 instead"),
            Row("B2", "1.60–3.20", "1.20–2.80", "0.01750/0.00500", 22.51816, 37.81421, 19.17608, 35.54561, 83.903985, 18.75129, 1.133822, "retain"),
            Row("C1", "1.60–3.20", "1.20–2.80", "0.02625/0.00750", 22.37066, 35.39729, 19.14208, 33.49741, 83.476075, 18.728495, 1.134804, "test steeper fade"),
            Row("C2 selected", "1.60–3.20", "1.20–2.80", "0.03500/0.01000", 22.22699, 33.38960, 19.10850, 31.77960, 83.104438, 18.70542, 1.135835, "selected"),
        });
        Line("C2 preserves nearly all finite 60 m and full-heat response of B2 while lowering the neutral Straight/TurnExit equilibrium diagnostics by about 4.4/3.8 m/s. No reference-speed or nonlinear-envelope change is justified.");

        Heading("Selected constants and frozen boundaries");
        var bc = before.Constants;
        var ac = after.Constants;
        Table("Parameter | Before | After | Status", new[]
        {
            Row("Straight reference acceleration m/s²", Range(bc.StraightMinimumReferenceAcceleration, bc.StraightMaximumReferenceAcceleration), Range(ac.StraightMinimumReferenceAcceleration, ac.StraightMaximumReferenceAcceleration), "tuned proportionally (Stage A)"),
            Row("TurnExit reference acceleration m/s²", Range(bc.TurnExitMinimumReferenceAcceleration, bc.TurnExitMaximumReferenceAcceleration), Range(ac.TurnExitMinimumReferenceAcceleration, ac.TurnExitMaximumReferenceAcceleration), "tuned proportionally (Stage B); Straight remains >= TurnExit"),
            Row("Drive/speed-oriented fade 1/(m/s)", $"{F(bc.DriveOrientedFadeRate)}/{F(bc.SpeedOrientedFadeRate)}", $"{F(ac.DriveOrientedFadeRate)}/{F(ac.SpeedOrientedFadeRate)}", "tuned together (Stage C)"),
            Row("Reference speed m/s", bc.ReferenceSpeed, ac.ReferenceSpeed, "frozen"),
            Row("Mass kg", bc.NominalMass, ac.NominalMass, "frozen"),
            Row("Resistance N", $"{F(bc.BaseResistance)} + {F(bc.QuadraticResistance)}v²", $"{F(ac.BaseResistance)} + {F(ac.QuadraticResistance)}v²", "frozen"),
            Row("Integration step m", bc.IntegrationStep, ac.IntegrationStep, "frozen"),
            Row("Gearing drive multipliers", Range(bc.LowGearingDriveMultiplier, bc.HighGearingDriveMultiplier), Range(ac.LowGearingDriveMultiplier, ac.HighGearingDriveMultiplier), "frozen"),
            Row("Surface mapping", $"{F(bc.SurfaceDriveMinimum)} + {F(bc.SurfaceDriveRange)} × EffectiveGrip", $"{F(ac.SurfaceDriveMinimum)} + {F(ac.SurfaceDriveRange)} × EffectiveGrip", "frozen"),
            Row("Start reaction s", Range(bc.StartReactionSlow, bc.StartReactionFast), Range(ac.StartReactionSlow, ac.StartReactionFast), "frozen"),
            Row("Start acceleration m/s²", Range(bc.StartAccelerationMinimum, bc.StartAccelerationMaximum), Range(ac.StartAccelerationMinimum, ac.StartAccelerationMaximum), "frozen"),
            Row("Corner correction m/s²", Range(bc.CornerCorrectionMinimum, bc.CornerCorrectionMaximum), Range(ac.CornerCorrectionMinimum, ac.CornerCorrectionMaximum), "frozen"),
        });

        Heading("Straight finite-distance response");
        Line("Free-drive production profiles; no following-corner target. Phase distances are printed after calibration and continue to reconcile exactly to requested distance.");
        Line();
        Table("Scenario | Speed | Gearing | Distance | Entry | Exit before | Exit after | Peak before | Peak after | Time before | Time after | A/C/D after | Equilibrium before | Equilibrium after",
            after.Straights.Where(item => !item.ScenarioId.StartsWith("gearing/", StringComparison.Ordinal))
                .Select(item =>
                {
                    var prior = Find(before.Straights, item.ScenarioId);
                    return Row(item.ScenarioId, item.SpeedSkill, item.Gearing, item.DistanceMeters, item.EntrySpeed,
                        prior.ExitSpeed, item.ExitSpeed, prior.PeakSpeed, item.PeakSpeed, prior.TravelTime, item.TravelTime,
                        $"{F(item.AccelerationDistance)}/{F(item.CruiseDistance)}/{F(item.DecelerationDistance)}",
                        prior.EquilibriumSpeed, item.EquilibriumSpeed);
                }));

        Heading("Force, acceleration and effective-power curves");
        Line("Every row uses the production envelope and resistance helpers at the requested speed. Effective power is `F_drive × v` only; it is not a literal engine-power model.");
        Line();
        Table("Speed skill | Gearing | v | Envelope before | Envelope after | Drive N before | Drive N after | Resistance N | Net N before | Net N after | a before | a after | Effective W before | Effective W after",
            after.ForceCurve.Select(item =>
            {
                var prior = before.ForceCurve.Single(value => value.SpeedSkill == item.SpeedSkill
                    && value.Gearing == item.Gearing && value.Speed == item.Speed);
                return Row(item.SpeedSkill, item.Gearing, item.Speed, prior.Envelope, item.Envelope,
                    prior.DriveForce, item.DriveForce, item.ResistanceForce, prior.NetForce, item.NetForce,
                    prior.Acceleration, item.Acceleration, prior.EffectivePowerWatts, item.EffectivePowerWatts);
            }));

        Heading("Signed-force equilibrium");
        Line("The ±0.5 m/s probes show positive net force below and negative net force above each root. Near-root residual is numerical bisection/float error, not a dead zone or clamp; production integration continues on both sides.");
        Line();
        Table("Speed skill | Gearing | Equilibrium before | Equilibrium after | Below v after | Net N below | Net N near | Above v after | Net N above",
            after.Equilibria.Select(item =>
            {
                var prior = before.Equilibria.Single(value => value.SpeedSkill == item.SpeedSkill && value.Gearing == item.Gearing);
                return Row(item.SpeedSkill, item.Gearing, prior.EquilibriumSpeed, item.EquilibriumSpeed,
                    item.BelowSpeed, item.BelowNetForce, item.NearNetForce, item.AboveSpeed, item.AboveNetForce);
            }));

        Heading("Gearing crossover");
        Line("Gearing 0 retains the stronger reference drive and wins at short distance; gearing 1 retains force longer and wins later. This is an emergent crossover from the same one-gear envelope, not a top-speed bonus.");
        Line();
        Table("Distance | Low exit before | Neutral exit before | High exit before | Low exit after | Neutral exit after | High exit after | Winner after",
            new[] { 10f, 30f, 60f, 100f, 300f, 600f }.Select(distance =>
            {
                float Exit(LongitudinalCalibrationSnapshot snapshot, float gearing)
                    => snapshot.Straights.Single(item =>
                        item.ScenarioId.StartsWith("gearing/", StringComparison.Ordinal)
                        && item.Gearing == gearing
                        && item.DistanceMeters == distance).ExitSpeed;
                var low = Exit(after, 0f);
                var high = Exit(after, 1f);
                return Row(distance, Exit(before, 0f), Exit(before, 0.5f), Exit(before, 1f),
                    low, Exit(after, 0.5f), high, low > high ? "low" : "high");
            }));
        Table("Diagnostic | Before | After", new[]
        {
            Row("Drive-force crossover speed m/s", before.GearingCrossover.DriveForceCrossoverSpeed, after.GearingCrossover.DriveForceCrossoverSpeed),
            Row("Traversal crossover distance m from 16 m/s", before.GearingCrossover.TraversalCrossoverDistance, after.GearingCrossover.TraversalCrossoverDistance),
        });
        Line("The selected traversal crossover is within the 100–300 m diagnostic interval. It is longer than one example straight, so short-track setup remains drive-oriented while sustained distance exposes the speed-oriented benefit; it is not far outside the useful diagnostic range.");

        Heading("TurnExit recovery");
        Line("Production semantics are unchanged: constraint → correction distance → drive only over legal remaining distance when the target is reached. `RunWide`, an unreached target and `Crash` receive no positive drive. Incoming and target values come from the #35 controlled production probe.");
        Line();
        Table("Speed | Outcome | Incoming | Target | Correction exit | Correction m | Legal drive m | Drive entry | Drive exit before | Drive exit after | Peak after | Drive time after | Entry a before | Entry a after | Equilibrium before | Equilibrium after | Final after",
            after.TurnExits.Select(item =>
            {
                var prior = Find(before.TurnExits, item.ScenarioId);
                return Row(item.SpeedSkill, item.Outcome, item.IncomingSpeed, item.CorrectionTarget,
                    item.CorrectionExit, item.CorrectionDistance, item.LegalDriveDistance, item.DriveEntry,
                    prior.DriveExit, item.DriveExit, item.DrivePeak, item.DriveTime,
                    prior.DriveEntryNetAcceleration, item.DriveEntryNetAcceleration,
                    prior.EquilibriumSpeed, item.EquilibriumSpeed, item.FinalExit);
            }));

        Heading("Standing-start regression");
        Line("Start reaction and 9–11 m/s² launch constants are untouched. Differences are collateral effects of the shared post-16 m/s fade only; prepared launch still uses production first-corner lookahead, while `pure_launch` has no preparation target.");
        Line();
        Table("Scenario | Mode | Start | Reaction before/after | Movement before | Movement after | Total before | Total after | TimeTo70 before/after | SpeedAt2s before | SpeedAt2s after | Exit before | Exit after | Peak before | Peak after | A/C/P after | Equilibrium before | Equilibrium after",
            after.Starts.Select(item =>
            {
                var prior = Find(before.Starts, item.ScenarioId);
                return Row(item.ScenarioId, item.Mode, item.StartSkill, $"{F(prior.ReactionTime)}/{F(item.ReactionTime)}",
                    prior.MovementTime, item.MovementTime, prior.TotalTime, item.TotalTime,
                    $"{F(prior.TimeTo70)}/{F(item.TimeTo70)}", prior.SpeedAtTwoSeconds, item.SpeedAtTwoSeconds,
                    prior.ExitSpeed, item.ExitSpeed, prior.PeakSpeed, item.PeakSpeed,
                    $"{F(item.AccelerationDistance)}/{F(item.CruiseDistance)}/{F(item.PreparationDistance)}",
                    prior.EquilibriumSpeed, item.EquilibriumSpeed);
            }));

        Heading("Complete production heats");
        Line("Four identical riders per Speed value, fixed ids/lanes, four laps, Dry, incidents 0, HoldLane, seed 320032. These are complete production heats, not isolated independent probes.");
        Line();
        Table("Speed | Rider | Vmax before | Vmax after | Average before | Average after | L1 penalty before | L1 penalty after | Heat before | Heat after | Flying before | Flying after | Distance before | Distance after | RW/B/C before | RW/B/C after",
            after.HeatRiders.Select(item =>
            {
                var prior = before.HeatRiders.Single(value => value.ScenarioId == item.ScenarioId && value.RiderId == item.RiderId);
                return Row(item.SpeedSkill, item.RiderId, prior.VmaxKph, item.VmaxKph,
                    prior.AverageSpeed, item.AverageSpeed, prior.L1Penalty, item.L1Penalty,
                    prior.HeatTime, item.HeatTime, prior.FlyingMedian, item.FlyingMedian,
                    prior.Distance, item.Distance, Counts(prior), Counts(item));
            }));
        Table("Speed | Vmax P50 before | Vmax P50 after | Average P50 before | Average P50 after | L1 penalty P50 before | L1 penalty P50 after | Heat P50 before | Heat P50 after | Flying P50 before | Flying P50 after",
            new[] { 0f, 25f, 50f, 75f, 100f }.Select(skill =>
            {
                var prior = before.HeatRiders.Where(item => item.SpeedSkill == skill).ToArray();
                var current = after.HeatRiders.Where(item => item.SpeedSkill == skill).ToArray();
                return Row(skill, Median(prior.Select(item => item.VmaxKph)), Median(current.Select(item => item.VmaxKph)),
                    Median(prior.Select(item => (double)item.AverageSpeed)), Median(current.Select(item => (double)item.AverageSpeed)),
                    Median(prior.Select(item => (double)item.L1Penalty!.Value)), Median(current.Select(item => (double)item.L1Penalty!.Value)),
                    Median(prior.Select(item => (double)item.HeatTime)), Median(current.Select(item => (double)item.HeatTime)),
                    Median(prior.Select(item => (double)item.FlyingMedian!.Value)), Median(current.Select(item => (double)item.FlyingMedian!.Value)));
            }));

        Heading("Corner regression boundary");
        Line("These values are black-box observations of unchanged `SegmentPhysics` transitions and the shared corner-correction capability. Exact before/after equality confirms that this PR does not calibrate corner physics.");
        Line();
        Table("Scenario | MaxSafe before/after | FirstBrake before/after | FirstRunWide before/after | FirstCrash before/after | Retention before/after | Capability before/after | Identical",
            after.Corners.Select(item =>
            {
                var prior = Find(before.Corners, item.ScenarioId);
                var identical = prior == item;
                return Row(item.ScenarioId, Pair(prior.MaxSafeSpeed, item.MaxSafeSpeed),
                    Pair(prior.FirstBrakeSpeed, item.FirstBrakeSpeed), Pair(prior.FirstRunWideSpeed, item.FirstRunWideSpeed),
                    Pair(prior.FirstCrashSpeed, item.FirstCrashSpeed), Pair(prior.RunWideRetention, item.RunWideRetention),
                    Pair(prior.CorrectionCapability, item.CorrectionCapability), identical);
            }));

        Heading("Within-heat spreads");
        Line("Spreads are diagnostics, not the tuning objective. The identical-rider rows combine fixed-line geometry with normal production evolution; `[25,50,50,75]` rows retain the #35 cautions and are not unconfounded skill-only estimates.");
        Line();
        Table("Scenario | Heat spread before | Heat spread after | Vmax spread before | Vmax spread after | L1 spread before | L1 spread after | Average spread before | Average spread after",
            after.WithinHeat.Select(item =>
            {
                var prior = Find(before.WithinHeat, item.ScenarioId);
                return Row(item.ScenarioId, prior.HeatTimeSpread, item.HeatTimeSpread,
                    prior.VmaxSpread, item.VmaxSpread, prior.L1Spread, item.L1Spread,
                    prior.AverageSpeedSpread, item.AverageSpeedSpread);
            }));

        Heading("Real PGE context and interpretation");
        Line("The versioned PGEE distributions are context envelopes, never caps, equalities, real-rider mappings or evidence that Skill 50 is an average PGE rider.");
        Line();
        Table("Metric | Unit | P10 | P50 | P90 | Use", new[]
        {
            Real("pge_clean_vmax", "secondary diagnostic only"),
            Real("pge_clean_average_speed", "context; geometry-dependent residual remains"),
            Real("pge_clean_l1_penalty", "context; start constants frozen"),
            Real("pge_four_rider_heat_time_spread", "spread diagnostic only"),
            Real("pge_four_rider_vmax_spread", "spread diagnostic only"),
            Real("pge_four_rider_l1_spread", "spread diagnostic only"),
            Real("pge_four_rider_average_speed_spread", "spread diagnostic only"),
        });
        Line("Full-heat Vmax is a secondary diagnostic only. The selected constants improve finite-distance Straight and TurnExit response without forcing Speed 100 to reach the real P10 Vmax envelope. Residual system-level speed deficit may belong to Phase 2 corner-envelope calibration.");
        Line();
        Line("Average-speed medians remain below the real comparable envelope on the standing-start example. Likely Phase 2 corner-envelope calibration gap. Absolute lap/heat/flying times remain `ContextOnlyUntilTrackGeometry`; this PR does not fit the example track to every real venue.");
        Line();
        Line("Standing-start reaction and launch constants remain unchanged; corner transition thresholds, overspeed retention and correction capability remain byte/float-identical. No corner constant, surface model, geometry, decisions, contact, RNG, telemetry schema, dataset or historical report was modified.");
        return b.ToString();

        object?[] Real(string id, string use)
        {
            var distribution = realDistributions[id];
            return Row(id, distribution.Definition.Unit, distribution.Quantiles.P10,
                distribution.Quantiles.P50, distribution.Quantiles.P90, use);
        }
        void Line(string value = "") => b.Append(value).Append('\n');
        void Heading(string value) { Line(); Line($"## {value}"); Line(); }
        void Table(string header, IEnumerable<object?[]> rows)
        {
            Line($"| {header} |");
            Line($"| {string.Join(" | ", header.Split('|').Select(_ => "---"))} |");
            foreach (var row in rows) Line($"| {string.Join(" | ", row.Select(F))} |");
            Line();
        }
    }

    private static T Find<T>(IEnumerable<T> values, string id) where T : class
        => values.Single(item => StringComparer.Ordinal.Equals(
            (string)item.GetType().GetProperty("ScenarioId")!.GetValue(item)!, id));

    private static string Counts(LongitudinalHeatRiderObservation value)
        => $"{value.RunWideCount}/{value.BrakeCount}/{value.CrashCount}";
    private static string Range(float first, float second) => $"{F(first)}–{F(second)}";
    private static string Pair(object? before, object? after) => $"{F(before)}/{F(after)}";
    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        return ordered.Length % 2 == 0
            ? (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) * 0.5d
            : ordered[ordered.Length / 2];
    }
    private static object?[] Row(params object?[] values) => values;
    private static string F(object? value) => value switch
    {
        null => "—",
        float number => number.ToString("0.######", CultureInfo.InvariantCulture),
        double number => number.ToString("0.######", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable item => item.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" "),
    };
    private static void ValidateSha(string sha, string name)
    {
        if (sha is null || sha.Length != 40 || sha.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("A full 40-character SHA is required.", name);
    }
}
