# Real start telemetry recovery and first-corner calibration evidence (#40)

## A. Source archaeology

Base main SHA: `ba0547cc1f686539ee19d52d5f60a5a3278c99e9` (`Add Motoarena matched-venue calibration (#39)`, squash merge).
Inspected repository sources: `tools/calibration/prepare_pge_dataset.py`, `data/calibration/pge/v1/source_manifest.json`, the dataset README, historical calibration reports, tests, and every repository occurrence of the start/telemetry search terms. The external package manifest identifies `telemetry_full.csv`, `matches.csv`, `heats.csv`, `run_summary.json`, `download_telemetry.py`, `sample_match_raw.json`, and raw `7734.json`; its recorded SHA256 values remain unchanged in `pge-v1`.
| Source-package artifact | Rows/documents | SHA256 recorded by immutable manifest |
| --- | ---: | --- |
| `telemetry_full.csv` | 6789 | `7c9f9c50eddab7fcc6ab1bd24aa3b3fd83fcc7cdd83c7ac186609cdc503cb355` |
| `matches.csv` | 100 | `4501f897d5e3d8651bc57c5e8035bd7cc49cbe6917cb9e7da14f8766054347ef` |
| `heats.csv` | 6772 | `5941c7241d4059f44c1824a8399e8584918a193666df41c96c6d170b2615d0a9` |
| `run_summary.json` | 100 | `78d9975f805dbf11b125d25a88e53c11b093523981e5b43e6386517616decb2f` |
| source `README.md` | — | `2e1554418e7ce0d2f4b098b62c16b4ecc38b7549d798af1966dbe62f86a94760` |
| `scripts/download_telemetry.py` | — | `0e30795a6871e2b83ad9784387157fc1a4c72d49b9e67670d620dc7a30c1e5dc` |
| `output/sample_match_raw.json` | 1 | `d3ee80f1a28796b34c19f6e6d666ce3cddfc0188a4705361ac16a3f49399f020` |
| `output/raw/7734.json` | 1 | `d3ee80f1a28796b34c19f6e6d666ce3cddfc0188a4705361ac16a3f49399f020` |
Original source access was verified on 2026-09-16 against public Firestore endpoint template `https://firestore.googleapis.com/v1/projects/ezapi-446d0/databases/(default)/documents/telemetry/{match_id}`: all 95 PGEE match ids in `pge-v1` returned documents (16,672,939 response bytes; ordered retrieval fingerprint `95d7179d1f84270c5403055f0374b75d7a2237426323aba9f11f2b887f4f28f3`, defined as SHA256 over numeric match-id order of `UTF8(match_id) + NUL + raw response body`). Their top-level telemetry fields were exactly `gates`, `motorbikes`, and `riders`; the observed leaf-name set was `best_max_speed`, `best_max_speed_heat_no`, `best_time`, `best_time_heat_no`, `distance`, `enabled`, `gate`, `heat_no`, `heat_uid`, `l1_time`, `l2_time`, `l3_time`, `l4_time`, `max_speed`, `motorbike`, `no`, `position`, `rider_id`, `rider_name`, `rider_surname`, `team_shortcut`, `time`, and `uid`.
The official public stats API paths exposed by the app (`telemetry/dashboard`, `telemetry/ranking`, `telemetry/ranking/meta`) were also inspected. They expose heat time, total distance, overall Vmax and overtake rankings, not reaction, TimeTo70, physical SpeedAt2s, first-corner entry speed, or a sample series.
The supplied `PGE+Ekstraliga_2.4.22_APKPure.xapk` (SHA256 `e522d4e58d7e2bd32fe5293f88011c73f50d0d4f8db9921f2e654fb4ca5d591a`) contains package `com.speedwayekstraliga.app`, version 2.4.22. Its Hermes front end reads Firestore `telemetry/{matchId}` directly. It labels `speed_2s` as the highest speed after two seconds and `curve_speed` as the highest speed at first-corner entry, but consumes the raw gate-category objects; no client-side physical-value derivation or 0–60 m chart calculation was found.

## B. Source schema

| Source path | Example raw value | Type | Unit | Semantic meaning | Physical/ranking | Availability | Confidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `fields.gates.mapValue.fields.data.mapValue.fields.speed_2s.mapValue.fields.<heat_no>.arrayValue.values[<index>].mapValue.fields.gate.stringValue` | `a` | categorical gate id | none | gate associated with the per-heat highest speed after 2 s | RankingCategory | available | HighExactRawShapeAndAppLabel |
| `fields.gates.mapValue.fields.data.mapValue.fields.speed_2s.mapValue.fields.<heat_no>.arrayValue.values[<index>].mapValue.fields.no.integerValue` | `1` | integer encoded as string | none | source heat number | Identifier | available | HighExactRawShape |
| `fields.gates.mapValue.fields.data.mapValue.fields.curve_speed.mapValue.fields.<heat_no>.arrayValue.values[<index>].mapValue.fields.gate.stringValue` | `c` | categorical gate id | none | gate associated with the per-heat highest first-corner-entry speed | RankingCategory | available | HighExactRawShapeAndAppLabel |
| `fields.gates.mapValue.fields.data.mapValue.fields.curve_speed.mapValue.fields.<heat_no>.arrayValue.values[<index>].mapValue.fields.no.integerValue` | `1` | integer encoded as string | none | source heat number | Identifier | available | HighExactRawShape |
| `fields.riders.mapValue.fields.<rider_id>.mapValue.fields.details.mapValue.fields.<heat_uid>.mapValue.fields.max_speed.stringValue` | `111.8` | numeric string | km/h | maximum speed over the rider heat | PhysicalNumeric | available, not a start metric | HighSourcePackageAndAppDescription |
| `...details...<heat_uid>...time.stringValue` | `62.114` | numeric string | s | four-lap heat time | PhysicalNumeric | available, not a start metric | HighSourcePackageAndAppDescription |
| `...details...<heat_uid>...distance.stringValue` | `1398` | numeric string | m | total rider heat distance | PhysicalNumeric | available, not a start metric | HighSourcePackageAndAppDescription |
| no source path | — | none | s | reaction time | PhysicalNumeric | UnsupportedNumericByAvailableSource | HighFullSchemaScan |
| no source path | — | none | s | time to 70 km/h | PhysicalNumeric | UnsupportedNumericByAvailableSource | HighFullSchemaScan |
| no source path | — | none | km/h | speed at 2.0 s | PhysicalNumeric | UnsupportedNumericByAvailableSource | HighFullSchemaScan |
| no source path | — | none | km/h | first-corner-entry speed | PhysicalNumeric | UnsupportedNumericByAvailableSource | HighFullSchemaScan |
The public standalone Firestore document paths above start at `fields`. The archived downloader JSON wraps the identical paths under `responses.telemetry.fields`; for example its exact speed path is `responses.telemetry.fields.gates.mapValue.fields.data.mapValue.fields.speed_2s.mapValue.fields.<heat_no>.arrayValue.values[<index>].mapValue.fields.gate.stringValue`.
Firestore may encode a missing gate as `nullValue`; it remains blank. Blank is never zero.

## C. Ranking vs physical values

`speed_2s` and `curve_speed` are not scalar measurements. Each is a map keyed by heat number whose array entries contain a source heat number and gate id (`a`–`d`, sometimes null). They identify the gate associated with the category winner. The normalized `gate_rank_speed_2s` and `gate_rank_curve_speed` columns are categorical flags derived from those objects. Values such as `1`, `2`, `3`, or `4` must not be interpreted as seconds, m/s, km/h, distance, or a recoverable magnitude.
The app supplies display labels for the categories but does not calculate physical values from a hidden series. Classification: `RankingCategory`, not `PhysicalNumeric` and not `DisplayDerivedPhysicalNumeric`.

## D. Recovered metrics coverage

The offline PGEE audit snapshot contains 95 matches, 1593 distinct `match_id + heat_uid` attempts, and 6373 rider observations.
| Metric | Unit | Physical observations | Completeness | Result |
| --- | --- | ---: | ---: | --- |
| ReactionTime | s | 0 | 0% | UnsupportedNumericByAvailableSource |
| TimeTo70 | s | 0 | 0% | UnsupportedNumericByAvailableSource |
| SpeedAt2s | km/h | 0 | 0% | UnsupportedNumericByAvailableSource |
| FirstCornerEntrySpeed | km/h | 0 | 0% | UnsupportedNumericByAvailableSource |
| 0–60 m / 0–3 s speed samples | km/h plus source axis | 0 | 0% | UnsupportedNumericByAvailableSource |
No `data/calibration/pge-start/v1` dataset was created: a zero-row shell would imply recoverable measurements that the available source does not contain.

## E. Motoarena coverage

Exact selector: `season == 2026 && league == "PGEE" && source_track_label == "Motoarena im. Mariana Rosego"`. Ordinal equality only; no `Toruń`, team, case-folded, trimmed, or venue aliases.
| Item | Count |
| --- | ---: |
| Matches | 7 |
| Distinct `match_id + heat_uid` attempts | 114 |
| Rider observations | 456 |
| Physical ReactionTime / TimeTo70 / SpeedAt2s / FirstCornerEntrySpeed | 0 / 0 / 0 / 0 |
The seven exact match ids remain `6950, 6956, 6961, 6970, 6979, 6980, 6995`. Restarted attempts are distinct by `heat_uid`; they are not merged by heat number.

## F. Real distributions

| Metric | N | P10 | P25 | P50 | P75 | P90 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| ReactionTime s | 0 | — | — | — | — | — |
| TimeTo70 s | 0 | — | — | — | — | — |
| SpeedAt2s km/h | 0 | — | — | — | — | — |
| SpeedAt2s m/s | 0 | — | — | — | — | — |
| FirstCornerEntrySpeed km/h | 0 | — | — | — | — | — |
| FirstCornerEntrySpeed m/s | 0 | — | — | — | — | — |
Quantiles and four-rider within-heat spreads are `UnsupportedNumericByAvailableSource`; no gate breakdown is emitted for nonexistent numeric values.
Separate `LiteratureContext` only: senior reaction approximately 0.246 ± 0.050 s, junior approximately 0.258 ± 0.050 s, speedway above 100 km/h, and approximately 80 km/h in about 2.4 s. These are not substitutes for missing PGE start telemetry and are not fitted targets.

## G. Synthetic start diagnostics

Production path: `RealStartTelemetryCalibration → CalibrationRunner → HeatSimulator`; Motoarena #39 geometry; primary 31/31 m split; four riders with all six skills 50; neutral setup; fixed normalized lines 0–3; HoldLane; baseline surface `(grip=1, ruts=0, moisture=0.35)`; Dry weather; incidents off; seed 390039; four laps. Values are the four-rider median. Skill50 is a controlled game fixture, not an inferred PGEE rider.
| Metric | Synthetic Skill50 | Unit | Observer status |
| --- | ---: | --- | --- |
| ReactionTime | 0.24 | s | production launch observer |
| TimeTo70 | 2.237621 | s | production launch observer |
| SpeedAt2s | 62.332127 | km/h | production launch observer |
| SpeedAt2s | 17.31448 | m/s | production launch observer |
| FirstCornerEntrySpeed | 85.10042 | km/h | first production TurnEntry sample |
| PeakSpeedBeforeFirstCorner | 85.10042 | km/h | production launch profile peak |
| TimeToFirstCorner | 2.759259 | s | reaction plus launch movement to TurnEntry |
| DistanceAt2s | — | m | `UnsupportedDiagnosticByFrozenObserver`; current observer exposes speed but not distance, and #40 does not modify a frozen physics file or recreate the integration model |

## H. 25/31/37 m sensitivity

| Start→corner m | Reaction s | TimeTo70 s | SpeedAt2s km/h | First-corner entry km/h | Peak before corner km/h | Time to corner s | Prep distance m | DistanceAt2s m |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 25 | 0.24 | 2.237621 | 62.332127 | 77.712355 | 77.712355 | 2.494076 | 0 | — |
| 31 | 0.24 | 2.237621 | 62.332127 | 85.10042 | 85.10042 | 2.759259 | 0 | — |
| 37 | 0.24 | 2.237621 | 62.332127 | 91.417847 | 91.417847 | 3.003888 | 0 | — |
Sanity checks: ReactionTime span across splits = 0 s; SpeedAt2s span = 0 m/s. Both are zero at report precision. The production launch profile reports zero preparation distance for every tested split, so no first-corner preparation phase begins before the 2.0 s observation and SpeedAt2s is independent of the tested Motoarena split.

## I. Real vs synthetic

| Metric | Synthetic Skill50 | Real P10 | Real P50 | Real P90 | Synthetic − P50 |
| --- | ---: | ---: | ---: | ---: | ---: |
| ReactionTime s | 0.24 | — | — | — | — |
| TimeTo70 s | 2.237621 | — | — | — | — |
| SpeedAt2s km/h | 62.332127 | — | — | — | — |
| FirstCornerEntrySpeed km/h | 85.10042 | — | — | — | — |
Real columns and deltas are `UnsupportedNumericByAvailableSource`. Synthetic values are evidence about current code only, not evidence of agreement with real starts.

## J. 0–60 m series availability

`No recoverable 0–60 m physical speed series found`.
The Firestore schema has no sample array, time axis, distance axis, sampling interval, or per-sample speed. The downloader snapshot contains aggregate heat/lap/Vmax/distance fields plus gate-category winners. The app presents category markers and aggregate statistics; no hidden client-side 0–3 s or 0–60 m curve derivation was found. Therefore no curve artifact, interpolation, smoothing, invented time axis, or invented distance axis is produced.

## K. Diagnosis

Diagnosis case: `UnsupportedNumericByAvailableSource`; Cases A/B/C/D cannot be selected from real start telemetry because their required physical measurements are absent.
| Question | Answer |
| --- | --- |
| 1. Physical PGE TimeTo70? | NO — `UnsupportedNumericByAvailableSource`. |
| 2. Physical PGE SpeedAt2s? | NO — the similarly named raw field is a gate category. |
| 3. Physical PGE FirstCornerEntrySpeed? | NO — `curve_speed` is a gate category. |
| 4. Full 0–60 m or 0–3 s speed series? | NO. |
| 5. Is synthetic launch to 2 s too fast / correct / too slow? | Unsupported real comparison; current source cannot classify it. |
| 6. Is synthetic first-corner entry too fast / correct / too slow? | Unsupported real comparison; current source cannot classify it. |
| 7. One subsystem for the next PR? | Longitudinal drive-availability / throttle-profile shape across Straight → corner entry, tested diagnostically without assuming the launch is wrong. |
Launch acceleration deficit: not demonstrated. Premature first-corner preparation: not demonstrated; the production launch profile reports no preparation phase in any controlled split. The later-lap longitudinal-profile hypothesis from #39 remains the primary available suspect, but is not proven by missing start telemetry.

## L. What #41 should test

Open one experimental subsystem only: longitudinal drive-availability / throttle-profile shape across Straight → corner entry. Compare the existing production profile against controlled diagnostic variants on the matched Motoarena fixture, preserving a clean baseline and reporting Straight Vmax, flying-lap time, corner-entry speed and Straight↔apex amplitude together. Do not use gate-category winners as numeric targets, and do not tune standing-start acceleration until a physical start source exists.

## M. Frozen physics

No production physics file or constant changes in #40. `SimulationEngine`, `StandingStartDynamics`, `LongitudinalDynamics`, `ContinuousCornerEnvelope`, `SegmentPhysics`, `TrackGeometry`, skills, setup and RNG remain byte-identical to post-#39 `main`. Frozen values include reaction 0.28→0.20 s, launch reference acceleration 9→11 m/s², straight acceleration 1.60→3.20 m/s², corner reference 19 m/s, mass 142 kg, resistance `40 + 0.20v²` N, fades, correction and `FullDriveProgress`. Diagnostics observe production output only.

## N. Limitations

The original external source package itself is not committed; its filenames and hashes are retained in the immutable `pge-v1` manifest. Live public documents can change, so the report records the inspected schema and does not use network retrieval during deterministic regeneration. The app binary proves the consumed paths and labels, not sensor methodology, sampling location, or units for absent start magnitudes. `DistanceAt2s` is unavailable in the current observer and remains blank instead of being reconstructed. The Motoarena start split remains provisional. Missing real values prevent start-lane numeric breakdowns, within-heat spreads, real quantiles, real-vs-synthetic deltas and an A/B/C/D diagnosis.
