using System.Globalization;
using System.Text;

namespace CoreSim.Analysis;

public static class RealStartTelemetryReport
{
    public const string Unsupported = "UnsupportedNumericByAvailableSource";

    public static string Render(RealStartTelemetryCalibrationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var builder = new StringBuilder();
        void Line(string value = "") => builder.Append(value).Append('\n');
        void Heading(string value)
        {
            Line();
            Line($"## {value}");
            Line();
        }

        var primary = result.Splits.Single(item => item.StartLineToFirstCornerMeters == 31f);

        Line("# Real start telemetry recovery and first-corner calibration evidence (#40)");

        Heading("A. Source archaeology");
        Line("Base main SHA: `ba0547cc1f686539ee19d52d5f60a5a3278c99e9` (`Add Motoarena matched-venue calibration (#39)`, squash merge).");
        Line("Inspected repository sources: `tools/calibration/prepare_pge_dataset.py`, `data/calibration/pge/v1/source_manifest.json`, the dataset README, historical calibration reports, tests, and every repository occurrence of the start/telemetry search terms. The external package manifest identifies `telemetry_full.csv`, `matches.csv`, `heats.csv`, `run_summary.json`, `download_telemetry.py`, `sample_match_raw.json`, and raw `7734.json`; its recorded SHA256 values remain unchanged in `pge-v1`.");
        Line("| Source-package artifact | Rows/documents | SHA256 recorded by immutable manifest |");
        Line("| --- | ---: | --- |");
        Line("| `telemetry_full.csv` | 6789 | `7c9f9c50eddab7fcc6ab1bd24aa3b3fd83fcc7cdd83c7ac186609cdc503cb355` |");
        Line("| `matches.csv` | 100 | `4501f897d5e3d8651bc57c5e8035bd7cc49cbe6917cb9e7da14f8766054347ef` |");
        Line("| `heats.csv` | 6772 | `5941c7241d4059f44c1824a8399e8584918a193666df41c96c6d170b2615d0a9` |");
        Line("| `run_summary.json` | 100 | `78d9975f805dbf11b125d25a88e53c11b093523981e5b43e6386517616decb2f` |");
        Line("| source `README.md` | — | `2e1554418e7ce0d2f4b098b62c16b4ecc38b7549d798af1966dbe62f86a94760` |");
        Line("| `scripts/download_telemetry.py` | — | `0e30795a6871e2b83ad9784387157fc1a4c72d49b9e67670d620dc7a30c1e5dc` |");
        Line("| `output/sample_match_raw.json` | 1 | `d3ee80f1a28796b34c19f6e6d666ce3cddfc0188a4705361ac16a3f49399f020` |");
        Line("| `output/raw/7734.json` | 1 | `d3ee80f1a28796b34c19f6e6d666ce3cddfc0188a4705361ac16a3f49399f020` |");
        Line("Original source access was verified on 2026-09-16 against public Firestore endpoint template `https://firestore.googleapis.com/v1/projects/ezapi-446d0/databases/(default)/documents/telemetry/{match_id}`: all 95 PGEE match ids in `pge-v1` returned documents (16,672,939 response bytes; ordered retrieval fingerprint `95d7179d1f84270c5403055f0374b75d7a2237426323aba9f11f2b887f4f28f3`, defined as SHA256 over numeric match-id order of `UTF8(match_id) + NUL + raw response body`). Their top-level telemetry fields were exactly `gates`, `motorbikes`, and `riders`; the observed leaf-name set was `best_max_speed`, `best_max_speed_heat_no`, `best_time`, `best_time_heat_no`, `distance`, `enabled`, `gate`, `heat_no`, `heat_uid`, `l1_time`, `l2_time`, `l3_time`, `l4_time`, `max_speed`, `motorbike`, `no`, `position`, `rider_id`, `rider_name`, `rider_surname`, `team_shortcut`, `time`, and `uid`.");
        Line("The official public stats API paths exposed by the app (`telemetry/dashboard`, `telemetry/ranking`, `telemetry/ranking/meta`) were also inspected. They expose heat time, total distance, overall Vmax and overtake rankings, not reaction, TimeTo70, physical SpeedAt2s, first-corner entry speed, or a sample series.");
        Line("The supplied `PGE+Ekstraliga_2.4.22_APKPure.xapk` (SHA256 `e522d4e58d7e2bd32fe5293f88011c73f50d0d4f8db9921f2e654fb4ca5d591a`) contains package `com.speedwayekstraliga.app`, version 2.4.22. Its Hermes front end reads Firestore `telemetry/{matchId}` directly. It labels `speed_2s` as the highest speed after two seconds and `curve_speed` as the highest speed at first-corner entry, but consumes the raw gate-category objects; no client-side physical-value derivation or 0–60 m chart calculation was found.");

        Heading("B. Source schema");
        Line("| Source path | Example raw value | Type | Unit | Semantic meaning | Physical/ranking | Availability | Confidence |");
        Line("| --- | --- | --- | --- | --- | --- | --- | --- |");
        Line("| `fields.gates.mapValue.fields.data.mapValue.fields.speed_2s.mapValue.fields.<heat_no>.arrayValue.values[<index>].mapValue.fields.gate.stringValue` | `a` | categorical gate id | none | gate associated with the per-heat highest speed after 2 s | RankingCategory | available | HighExactRawShapeAndAppLabel |");
        Line("| `fields.gates.mapValue.fields.data.mapValue.fields.speed_2s.mapValue.fields.<heat_no>.arrayValue.values[<index>].mapValue.fields.no.integerValue` | `1` | integer encoded as string | none | source heat number | Identifier | available | HighExactRawShape |");
        Line("| `fields.gates.mapValue.fields.data.mapValue.fields.curve_speed.mapValue.fields.<heat_no>.arrayValue.values[<index>].mapValue.fields.gate.stringValue` | `c` | categorical gate id | none | gate associated with the per-heat highest first-corner-entry speed | RankingCategory | available | HighExactRawShapeAndAppLabel |");
        Line("| `fields.gates.mapValue.fields.data.mapValue.fields.curve_speed.mapValue.fields.<heat_no>.arrayValue.values[<index>].mapValue.fields.no.integerValue` | `1` | integer encoded as string | none | source heat number | Identifier | available | HighExactRawShape |");
        Line("| `fields.riders.mapValue.fields.<rider_id>.mapValue.fields.details.mapValue.fields.<heat_uid>.mapValue.fields.max_speed.stringValue` | `111.8` | numeric string | km/h | maximum speed over the rider heat | PhysicalNumeric | available, not a start metric | HighSourcePackageAndAppDescription |");
        Line("| `...details...<heat_uid>...time.stringValue` | `62.114` | numeric string | s | four-lap heat time | PhysicalNumeric | available, not a start metric | HighSourcePackageAndAppDescription |");
        Line("| `...details...<heat_uid>...distance.stringValue` | `1398` | numeric string | m | total rider heat distance | PhysicalNumeric | available, not a start metric | HighSourcePackageAndAppDescription |");
        Line("| no source path | — | none | s | reaction time | PhysicalNumeric | UnsupportedNumericByAvailableSource | HighFullSchemaScan |");
        Line("| no source path | — | none | s | time to 70 km/h | PhysicalNumeric | UnsupportedNumericByAvailableSource | HighFullSchemaScan |");
        Line("| no source path | — | none | km/h | speed at 2.0 s | PhysicalNumeric | UnsupportedNumericByAvailableSource | HighFullSchemaScan |");
        Line("| no source path | — | none | km/h | first-corner-entry speed | PhysicalNumeric | UnsupportedNumericByAvailableSource | HighFullSchemaScan |");
        Line("The public standalone Firestore document paths above start at `fields`. The archived downloader JSON wraps the identical paths under `responses.telemetry.fields`; for example its exact speed path is `responses.telemetry.fields.gates.mapValue.fields.data.mapValue.fields.speed_2s.mapValue.fields.<heat_no>.arrayValue.values[<index>].mapValue.fields.gate.stringValue`.");
        Line("Firestore may encode a missing gate as `nullValue`; it remains blank. Blank is never zero.");

        Heading("C. Ranking vs physical values");
        Line("`speed_2s` and `curve_speed` are not scalar measurements. Each is a map keyed by heat number whose array entries contain a source heat number and gate id (`a`–`d`, sometimes null). They identify the gate associated with the category winner. The normalized `gate_rank_speed_2s` and `gate_rank_curve_speed` columns are categorical flags derived from those objects. Values such as `1`, `2`, `3`, or `4` must not be interpreted as seconds, m/s, km/h, distance, or a recoverable magnitude.");
        Line("The app supplies display labels for the categories but does not calculate physical values from a hidden series. Classification: `RankingCategory`, not `PhysicalNumeric` and not `DisplayDerivedPhysicalNumeric`.");

        Heading("D. Recovered metrics coverage");
        Line($"The offline PGEE audit snapshot contains {result.Coverage.GlobalMatchCount} matches, {result.Coverage.GlobalAttemptCount} distinct `match_id + heat_uid` attempts, and {result.Coverage.GlobalRiderObservationCount} rider observations.");
        Line("| Metric | Unit | Physical observations | Completeness | Result |");
        Line("| --- | --- | ---: | ---: | --- |");
        foreach (var row in new[]
                 {
                     ("ReactionTime", "s"), ("TimeTo70", "s"), ("SpeedAt2s", "km/h"),
                     ("FirstCornerEntrySpeed", "km/h"), ("0–60 m / 0–3 s speed samples", "km/h plus source axis"),
                 })
        {
            Line($"| {row.Item1} | {row.Item2} | 0 | 0% | {Unsupported} |");
        }
        Line("No `data/calibration/pge-start/v1` dataset was created: a zero-row shell would imply recoverable measurements that the available source does not contain.");

        Heading("E. Motoarena coverage");
        Line("Exact selector: `season == 2026 && league == \"PGEE\" && source_track_label == \"Motoarena im. Mariana Rosego\"`. Ordinal equality only; no `Toruń`, team, case-folded, trimmed, or venue aliases.");
        Line("| Item | Count |");
        Line("| --- | ---: |");
        Line($"| Matches | {result.Coverage.MotoarenaMatchCount} |");
        Line($"| Distinct `match_id + heat_uid` attempts | {result.Coverage.MotoarenaAttemptCount} |");
        Line($"| Rider observations | {result.Coverage.MotoarenaRiderObservationCount} |");
        Line("| Physical ReactionTime / TimeTo70 / SpeedAt2s / FirstCornerEntrySpeed | 0 / 0 / 0 / 0 |");
        Line("The seven exact match ids remain `6950, 6956, 6961, 6970, 6979, 6980, 6995`. Restarted attempts are distinct by `heat_uid`; they are not merged by heat number.");

        Heading("F. Real distributions");
        Line("| Metric | N | P10 | P25 | P50 | P75 | P90 |");
        Line("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var metric in new[] { "ReactionTime s", "TimeTo70 s", "SpeedAt2s km/h", "SpeedAt2s m/s", "FirstCornerEntrySpeed km/h", "FirstCornerEntrySpeed m/s" })
            Line($"| {metric} | 0 | — | — | — | — | — |");
        Line($"Quantiles and four-rider within-heat spreads are `{Unsupported}`; no gate breakdown is emitted for nonexistent numeric values.");
        Line("Separate `LiteratureContext` only: senior reaction approximately 0.246 ± 0.050 s, junior approximately 0.258 ± 0.050 s, speedway above 100 km/h, and approximately 80 km/h in about 2.4 s. These are not substitutes for missing PGE start telemetry and are not fitted targets.");

        Heading("G. Synthetic start diagnostics");
        Line("Production path: `RealStartTelemetryCalibration → CalibrationRunner → HeatSimulator`; Motoarena #39 geometry; primary 31/31 m split; four riders with all six skills 50; neutral setup; fixed normalized lines 0–3; HoldLane; baseline surface `(grip=1, ruts=0, moisture=0.35)`; Dry weather; incidents off; seed 390039; four laps. Values are the four-rider median. Skill50 is a controlled game fixture, not an inferred PGEE rider.");
        Line("| Metric | Synthetic Skill50 | Unit | Observer status |");
        Line("| --- | ---: | --- | --- |");
        Line($"| ReactionTime | {F(primary.ReactionTimeSeconds)} | s | production launch observer |");
        Line($"| TimeTo70 | {F(primary.TimeTo70KphSeconds)} | s | production launch observer |");
        Line($"| SpeedAt2s | {Kph(primary.SpeedAtTwoSecondsMetersPerSecond)} | km/h | production launch observer |");
        Line($"| SpeedAt2s | {F(primary.SpeedAtTwoSecondsMetersPerSecond)} | m/s | production launch observer |");
        Line($"| FirstCornerEntrySpeed | {Kph(primary.FirstCornerEntrySpeedMetersPerSecond)} | km/h | first production TurnEntry sample |");
        Line($"| PeakSpeedBeforeFirstCorner | {Kph(primary.PeakSpeedBeforeFirstCornerMetersPerSecond)} | km/h | production launch profile peak |");
        Line($"| TimeToFirstCorner | {F(primary.TimeToFirstCornerSeconds)} | s | reaction plus launch movement to TurnEntry |");
        Line("| DistanceAt2s | — | m | `UnsupportedDiagnosticByFrozenObserver`; current observer exposes speed but not distance, and #40 does not modify a frozen physics file or recreate the integration model |");

        Heading("H. 25/31/37 m sensitivity");
        Line("| Start→corner m | Reaction s | TimeTo70 s | SpeedAt2s km/h | First-corner entry km/h | Peak before corner km/h | Time to corner s | Prep distance m | DistanceAt2s m |");
        Line("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var split in result.Splits)
        {
            Line($"| {F(split.StartLineToFirstCornerMeters)} | {F(split.ReactionTimeSeconds)} | {F(split.TimeTo70KphSeconds)} | {Kph(split.SpeedAtTwoSecondsMetersPerSecond)} | {Kph(split.FirstCornerEntrySpeedMetersPerSecond)} | {Kph(split.PeakSpeedBeforeFirstCornerMetersPerSecond)} | {F(split.TimeToFirstCornerSeconds)} | {F(split.PreparationDistanceMeters)} | — |");
        }
        Line($"Sanity checks: ReactionTime span across splits = {F(result.ReactionTimeSplitSpanSeconds)} s; SpeedAt2s span = {F(result.SpeedAtTwoSecondsSplitSpanMetersPerSecond)} m/s. Both are zero at report precision. The production launch profile reports zero preparation distance for every tested split, so no first-corner preparation phase begins before the 2.0 s observation and SpeedAt2s is independent of the tested Motoarena split.");

        Heading("I. Real vs synthetic");
        Line("| Metric | Synthetic Skill50 | Real P10 | Real P50 | Real P90 | Synthetic − P50 |");
        Line("| --- | ---: | ---: | ---: | ---: | ---: |");
        Line($"| ReactionTime s | {F(primary.ReactionTimeSeconds)} | — | — | — | — |");
        Line($"| TimeTo70 s | {F(primary.TimeTo70KphSeconds)} | — | — | — | — |");
        Line($"| SpeedAt2s km/h | {Kph(primary.SpeedAtTwoSecondsMetersPerSecond)} | — | — | — | — |");
        Line($"| FirstCornerEntrySpeed km/h | {Kph(primary.FirstCornerEntrySpeedMetersPerSecond)} | — | — | — | — |");
        Line($"Real columns and deltas are `{Unsupported}`. Synthetic values are evidence about current code only, not evidence of agreement with real starts.");

        Heading("J. 0–60 m series availability");
        Line("`No recoverable 0–60 m physical speed series found`.");
        Line("The Firestore schema has no sample array, time axis, distance axis, sampling interval, or per-sample speed. The downloader snapshot contains aggregate heat/lap/Vmax/distance fields plus gate-category winners. The app presents category markers and aggregate statistics; no hidden client-side 0–3 s or 0–60 m curve derivation was found. Therefore no curve artifact, interpolation, smoothing, invented time axis, or invented distance axis is produced.");

        Heading("K. Diagnosis");
        Line($"Diagnosis case: `{Unsupported}`; Cases A/B/C/D cannot be selected from real start telemetry because their required physical measurements are absent.");
        Line("| Question | Answer |");
        Line("| --- | --- |");
        Line("| 1. Physical PGE TimeTo70? | NO — `UnsupportedNumericByAvailableSource`. |");
        Line("| 2. Physical PGE SpeedAt2s? | NO — the similarly named raw field is a gate category. |");
        Line("| 3. Physical PGE FirstCornerEntrySpeed? | NO — `curve_speed` is a gate category. |");
        Line("| 4. Full 0–60 m or 0–3 s speed series? | NO. |");
        Line("| 5. Is synthetic launch to 2 s too fast / correct / too slow? | Unsupported real comparison; current source cannot classify it. |");
        Line("| 6. Is synthetic first-corner entry too fast / correct / too slow? | Unsupported real comparison; current source cannot classify it. |");
        Line("| 7. One subsystem for the next PR? | Longitudinal drive-availability / throttle-profile shape across Straight → corner entry, tested diagnostically without assuming the launch is wrong. |");
        Line("Launch acceleration deficit: not demonstrated. Premature first-corner preparation: not demonstrated; the production launch profile reports no preparation phase in any controlled split. The later-lap longitudinal-profile hypothesis from #39 remains the primary available suspect, but is not proven by missing start telemetry.");

        Heading("L. What #41 should test");
        Line("Open one experimental subsystem only: longitudinal drive-availability / throttle-profile shape across Straight → corner entry. Compare the existing production profile against controlled diagnostic variants on the matched Motoarena fixture, preserving a clean baseline and reporting Straight Vmax, flying-lap time, corner-entry speed and Straight↔apex amplitude together. Do not use gate-category winners as numeric targets, and do not tune standing-start acceleration until a physical start source exists.");

        Heading("M. Frozen physics");
        Line("No production physics file or constant changes in #40. `SimulationEngine`, `StandingStartDynamics`, `LongitudinalDynamics`, `ContinuousCornerEnvelope`, `SegmentPhysics`, `TrackGeometry`, skills, setup and RNG remain byte-identical to post-#39 `main`. Frozen values include reaction 0.28→0.20 s, launch reference acceleration 9→11 m/s², straight acceleration 1.60→3.20 m/s², corner reference 19 m/s, mass 142 kg, resistance `40 + 0.20v²` N, fades, correction and `FullDriveProgress`. Diagnostics observe production output only.");

        Heading("N. Limitations");
        Line("The original external source package itself is not committed; its filenames and hashes are retained in the immutable `pge-v1` manifest. Live public documents can change, so the report records the inspected schema and does not use network retrieval during deterministic regeneration. The app binary proves the consumed paths and labels, not sensor methodology, sampling location, or units for absent start magnitudes. `DistanceAt2s` is unavailable in the current observer and remains blank instead of being reconstructed. The Motoarena start split remains provisional. Missing real values prevent start-lane numeric breakdowns, within-heat spreads, real quantiles, real-vs-synthetic deltas and an A/B/C/D diagnosis.");

        return builder.ToString();
    }

    private static string Kph(double? metersPerSecond) =>
        metersPerSecond.HasValue ? F(metersPerSecond.Value * 3.6d) : "—";

    private static string F(double? value) => value.HasValue ? F(value.Value) : "—";

    private static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
