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

Generate a report of the checked-out model into a disposable output. The committed
`current-model-baseline.md` is the historical PR #32 snapshot and must not be
overwritten after a geometry change:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- calibration-report data/calibration/pge/v1 <temporary-output.md>
```

The dataset and reports are deterministic. CI runs Python standard-library tests plus the .NET build/test suite and performs no telemetry download.

## Physical-width observation (#33)

`TrackGeometry` now stores separate straight/turn physical widths. Lane references
and continuous lateral positions remain dimensionless `0..4`, with fraction
`position / 4`. Usable span subtracts the 1 m inner measurement/reference offset
and a separate provisional 1 m outer game margin. `InnerRadiusMeters` describes
the inner reference trajectory, not the kerb. The standing example uses 10/14 m
widths; these are minimum-width examples rather than a fit to every real venue.
The synthetic compatibility example uses 6/6 m.

The geometric reference is [FIM Track Racing Circuits Standards 2026](https://www.fim-moto.com/en/documents/view/fim-standards-for-track-racing-circuits):
track length measured 1 m from the inner edge, 260–425 m for speedway, minimum
straight width 10 m and bend width 14 m. Only the inner reference trajectory is
the length reference; outer trajectory distance is not official track length.
Generic geometry validation permits synthetic tracks with widths greater than
the 2 m sum of margins. The optional width-envelope helper does not certify a
whole circuit or run automatically in the constructor.

The [physical-width impact report](calibration/physical-width-impact.md) compares
the historical #32 measurements with the current production sweep/evaluator:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- physical-width-impact-report data/calibration/pge/v1 docs/calibration/current-model-baseline.md docs/calibration/physical-width-impact.md
```

Radius, arc length, contact/occupancy separation and lateral displacement use
local physical metres. Surface storage remains five normalized bands with
unchanged wear/grip formulas. Width transitions remain segment-local without
extra movement/time/distance; gradual transitions, active lateral diagonal/spiral
path correction and physical A/B/C/D gates are future work. No speed/performance
constants changed and no calibration was performed. The six versioned dataset
files and historical report remain byte-identical.

## Change boundary

PR #32 adds measurement infrastructure. PR #33 changes only physical lateral
geometry and its required conversions, and records the observed impact. Earlier
roadmap text in the historical #32 report assigned tuning to #33; the revised
specification supersedes that plan. Empirical performance tuning remains future
work.
