# Real-world calibration and evaluation

PR #32 adds measurement infrastructure. It does not calibrate production physics. Real telemetry flows through a deterministic offline normalizer into a validated, versioned dataset; CoreSim then constructs empirical distributions and compares production-simulation output component by component.

```text
external PGEE source package
  -> tools/calibration/prepare_pge_dataset.py
  -> data/calibration/pge/v1
  -> RealWorldCalibrationDataset
  -> CalibrationSkillSweep (CalibrationRunner -> HeatSimulator)
  -> RealWorldCalibrationEvaluator
  -> docs/calibration/current-model-baseline.md
```

## Interpretation boundary

Real telemetry defines distributions and performance envelopes, not hard speed, corner, reaction, or age-specific limits. Rider age/category is not a physics multiplier: capability comes from RiderSkills together with setup, track, surface, trajectory, decisions, and—when implemented—execution variation. PR #32 adds no new execution RNG and assigns no game skill to a real rider. Skill 50 is not defined as the average PGE Ekstraliga rider.

CleanPhysics is a conservative steady-flying-lap analysis subset. Records outside it are not declared invalid or crashes; complete non-steady attempts remain Eventful and incomplete/source-mismatch records remain AuditOnly. Restarts are kept distinct by `match_id + heat_uid`.

The source `speed_2s` and `curve_speed` fields describe gate rankings in a heat. They are retained as categorical metadata and are never parsed as physical rider speeds. Individual reaction time also does not exist in the PGEE telemetry feed. Reaction-time population observations are separately stored as literature context using only values supplied explicitly in the task specification; the PDF is not included and no values were inferred from it.

## Units and definitions

- Heat and lap times: seconds.
- Source Vmax: kilometres per hour.
- Distance: metres.
- Derived average speed: metres per second (`distance / heat time`).
- Flying pace: median of L2, L3, and L4.
- First-lap penalty: L1 minus the flying-lap median.

`CalibrationUnits.KphToMetersPerSecond` and `MetersPerSecondToKph` are pure conversions. Unit mismatches are rejected by the evaluator.

For each CleanPhysics distribution, Python and C# both use linear interpolation at sorted zero-based position `(n - 1) * p`. The committed summary records P01/P10/P25/P50/P75/P90/P99. The evaluator compares P10/P25/P50/P75/P90 and reports real and simulated values, signed and meaningful relative gaps, and the simulated value's deterministic midrank percentile in the real distribution.

There is deliberately no overall accuracy number and no optimizer. Conflicts remain visible per metric; PR #32 performs no automatic parameter search and records no “best constants.”

## Comparability classes

`ComparableEnvelope` covers Vmax, average speed, first-lap penalty, and four-rider within-heat spreads. These are useful distribution constraints today, never per-race equalities or caps.

`ContextOnlyUntilTrackGeometry` covers absolute heat and lap times, flying-lap time, and distance. `Track.CreateStandingStartExample()` is an example geometry, while real PGEE tracks differ in length, radii, widths, straights, and shape. The example must not be fitted directly to all PGEE absolute times.

`UnsupportedNumericByCurrentSource` covers individual reaction, SpeedAt2s, and first-curve speed. Production simulation may report these values, but this PGEE source cannot numerically evaluate them. The rankings with similar source names must not be substituted.

Literature observations are context only; approximately 80 km/h in 2.4 s is sanity context and approximately 0.10-0.12 s is future false-start rule context. Neither is a calibration equality target or hard reaction cap.

## Within-heat and rider-relative evidence

Four-rider CleanPhysics attempts provide distributions of `max - min` for heat time, Vmax, L1, and average speed. A typical spread constrains the scale of rider differences; it does not require every simulated heat to reproduce the median exactly.

For each qualifying heat, rider residuals subtract the four-rider heat mean for heat time, Vmax, L1, and average speed. This partly removes shared venue, surface, and heat pace. Per-rider counts, mean/median/P25/P75/standard deviation describe empirical performance fingerprints only. Chronological split-half medians for riders with at least 12 observations and within-heat Spearman correlations provide persistence/relationship diagnostics, not a mapping to RiderSkills.

## Leakage-resistant split

The unit of splitting is `match_id`, never a rider row. Of 93 eligible matches, the newest `ceil(20%)` (19) form FINAL_TEST and the earlier 74 form DEVELOPMENT. Five DEVELOPMENT folds are assigned by `SHA256(match_id) modulo 5`. The checked-in `pge_split.csv` makes the assignment stable and auditable; all rows from one match share it.

## Production skill-response sweeps

Sweeps run only through `CalibrationRunner -> HeatSimulator` on `Track.CreateStandingStartExample()`, with HoldLane decisions, incident frequency zero, a fixed seed, dry weather, neutral setup, and a perfect/neutral deterministic surface. They include a balanced fixture, separate Start/Speed/SlideControl values 0/25/50/75/100, and the 27 combinations of 25/50/75 for those three skills. TrackReading, PairRiding, and Adaptability stay 50.

Each result retains reaction, launch movement, TimeTo70, SpeedAt2s, first-curve entry speed, L1-L4, flying median, first-lap penalty, Vmax, distance, total time, average speed, and RunWide/Brake/Crash counts. Calibration CSV samples also carry all six exact RiderSkills in stable invariant-culture columns. Diagnostics only observe already-resolved production state and do not change physics, random draws, commit order, classification, or track evolution.

## Offline reproduction

Prepare the snapshot from the eight external source files (no network and no PDF):

```text
python tools/calibration/prepare_pge_dataset.py \
  --telemetry-full <source>/output/telemetry_full.csv \
  --matches <source>/output/matches.csv \
  --heats <source>/output/heats.csv \
  --run-summary <source>/output/run_summary.json \
  --source-readme <source>/README.md \
  --downloader <source>/scripts/download_telemetry.py \
  --sample-match-raw <source>/output/sample_match_raw.json \
  --raw-match <source>/output/raw/7734.json \
  --output data/calibration/pge/v1 \
  --generated-at-utc 2026-09-05T21:03:47.868178Z
```

Generate the unchanged current-model report:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- calibration-report data/calibration/pge/v1 docs/calibration/current-model-baseline.md
```

Both committed artifacts are deterministic. CI runs Python standard-library tests plus the .NET build/test suite and performs no telemetry download.

## Change boundary

PR #32 changes no constants or production result. It adds the evidence, metadata, controlled measurements, comparison semantics, and current-model baseline needed for review. PR #33 is the first PR allowed to modify empirical physics calibration parameters, after these gaps and metric definitions are reviewed.
