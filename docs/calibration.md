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

## Continuous corner-speed correction observation (#34, historical)

The [continuous-correction impact report](calibration/continuous-corner-correction-impact.md)
is generated deterministically from the production simulation path. It validates
the committed #33 report as its immutable before-state, runs balanced and
Speed/SlideControl 0/50/100 production sweeps through the existing evaluator,
and adds controlled TurnEntry/TurnMiddle/TurnExit probes for below-max, quiet,
Brake and RunWide bands.

Calibration step samples append nullable correction entry/target/exit speed,
phase time, required/actual/remaining distance, effective deceleration and
target-reached fields. Existing columns retain their order, invariant culture,
decimal point, stable rider order and `\n`; null remains empty. Diagnostic total
travel time was the actual final elapsed delta even when the then-current scrub,
correction and TurnExit drive coexisted. #38 supersedes those phase bridges in
ADVANCED production while preserving this artifact.

Regenerate with:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- \
  continuous-corner-correction-impact-report \
  data/calibration/pge/v1 \
  docs/calibration/physical-width-impact.md \
  docs/calibration/continuous-corner-correction-impact.md
```

This is a structural physics observation. It does not tune the physical model,
modify the versioned PGEE snapshot or overwrite the historical #32/#33 reports.

## Calibration Scenario Suite (#35): measurement only

`CalibrationScenarioCatalog` defines explicit one-axis-at-a-time experiments
around the standing-start example, balanced skills/style, neutral setup,
surface `(1, 0, 0.35)`, Dry weather, incidents 0 and seed 320032. Skill 50 is a
fixture, not a real rider mapping. The few interaction probes are labelled:
gearing × short/long distance, traction bias × moisture and synthetic skill
patterns on fixed four-rider lines. All input values and resulting EffectiveGrip
are printed, including the distinction between initial surface and normal
production surface evolution during a heat.

The `CoreSim.Analysis` layer uses immutable typed scenario metadata/inputs and
separate Start, Straight, Turn, LineGeometry and FullHeat results. Seven kinds
distinguish the three turn phases. `CalibrationScenarioSuite` invokes existing
production primitives for pure launch/free straights; prepared launch and turn
probes retain `SimulationEngine.Resolve` diagnostics. Full heat and single-rider
line experiments use `CalibrationRunner -> HeatSimulator`. No second engine,
physics formulas, new RNG, constants, tuning or diagnostic production API changes
were introduced. The existing `CalibrationSkillSweep` keeps its public contract
and shares only its unchanged rider-telemetry projection with the suite.

Corner transitions and RunWide retention are measured by calls to
`SegmentPhysics.Apply`, not copied threshold equations. At outer lane 4 no
recoverable RunWide band exists: production crashes instead, and the corresponding
observation is absent. The historical #35 baseline captured TurnEntry scrub,
TurnMiddle carry and segment-gated TurnExit drive. Current #38 scenarios use a
full logical-corner fixture and the continuous production traversal instead.
Crash probes preserve production half-distance semantics.
Requested HoldLane geometry probes never overwrite a resolved lateral position;
the report prints observed min/max lateral positions to disclose any deviation.

`CalibrationScenarioReport` is a pure, ordinal-ordered, invariant-culture LF
Markdown renderer; only Sandbox reads files and accepts the provenance SHA.
The CLI refuses to overwrite the dataset directory or the three prior report
filenames. The existing evaluator supplies comparability classifications and
real quantiles; absolute times/distance stay `ContextOnlyUntilTrackGeometry`.
Synthetic within-heat spreads include fixed-line geometry and are not an
unconfounded skill-only estimate or an estimated real population distribution.

The [scenario baseline](calibration/calibration-scenarios-baseline.md) is the
historical **pre-tuning baseline after PR #34**, based on main
`4572ad9c5af032572f528f0ce434df9c97153594`. Later tuning PRs must preserve it and
write a separately named report. Tests protect determinism, production reuse,
input-order independence and phase/distance semantics, not hundreds of frozen
performance numbers or perpetual equality with this historical Markdown.

Generate from the repository root:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- calibration-scenarios-report data/calibration/pge/v1 4572ad9c5af032572f528f0ce434df9c97153594 docs/calibration/calibration-scenarios-baseline.md
```

## Longitudinal speed-envelope calibration (#36)

#36 is the first bounded calibration of the existing signed-force model. It
doubles the Straight reference-acceleration range to `1.60–3.20 m/s²`, doubles
the TurnExit range to `1.20–2.80 m/s²`, and doubles both linear fade endpoints
to `0.0350/0.0100 1/(m/s)`. Reference speed remains 16 m/s. Mass, resistance,
the 1 m integration step, gearing/surface mappings, standing-start constants and
all corner physics remain unchanged.

The [impact report](calibration/longitudinal-speed-envelope-impact.md) pairs a
serialized production snapshot from base main with the current production
candidate. It covers Straight distance/entry/Speed axes, force and effective
`F_drive × v` diagnostics, signed equilibrium, gearing crossover, legal
TurnExit recovery distance, prepared/pure start regression, complete production
heats, corner transition equality and within-heat spreads. The before snapshot
is measurement evidence, not a second engine or a runtime input.

Regenerate a candidate report from the repository root:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- \
  longitudinal-speed-envelope-impact-report \
  data/calibration/pge/v1 \
  docs/calibration/longitudinal-speed-envelope-before.json \
  842e0ce861466cdf5a67287c155f81d879cf6666 \
  <candidate-code-head-sha> \
  docs/calibration/longitudinal-speed-envelope-impact.md
```

Full-heat Vmax remains a secondary diagnostic rather than a target forced onto
Speed 100. Residual system-level speed and average-speed deficits are left
visible for the later corner-envelope calibration phase.

## Continuous corner phase foundation (#37)

The [foundation impact report](calibration/continuous-corner-foundation-impact.md)
documents the immutable logical-corner map for both example tracks and proves
that the complete production Calibration Scenario Suite is byte-identical
before and after the structural refactor. It includes controlled start,
straight, turn-phase and full-heat before/after/delta observations.

Regenerate from a base-main Calibration Scenario Suite capture:

    dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- \
      continuous-corner-foundation-impact-report \
      data/calibration/pge/v1 \
      <base-main-calibration-scenarios-report.md> \
      0f6dfba7767b155a9c988687a95c5cc7e50a50bd \
      <candidate-code-head-sha> \
      docs/calibration/continuous-corner-foundation-impact.md

The before capture must be produced on the exact base SHA with
calibration-scenarios-report and that same base SHA argument. The generator
refuses any scenario-byte difference. The report is observation only: no
longitudinal, corner, start, contact, lateral or surface constant is changed.

## Continuous corner envelope calibration (#38)

The [#38 impact report](calibration/continuous-corner-envelope-impact.md) is
regenerated from the final 197-scenario production suite and the unchanged
`pge-v1` dataset. The complete base capture had 186 scenarios; new families are
`continuous_corner/progress/*`, the bounded extreme production heat and the
outermost-line extreme diagnostic. A0 keeps 16 m/s, while B17/B18/B19 change
only the ADVANCED settled/apex reference. B19 is selected at 19 m/s; the report
lists every frozen constant and the remaining telemetry/geometry gap.

Regenerate from the repository root:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- \
  continuous-corner-envelope-impact-report \
  data/calibration/pge/v1 \
  c06f632e04c72d119e5cf925f9f08a8e4ee6a9d6e2dde4c01491a462f8165417 \
  docs/calibration/continuous-corner-envelope-impact.md
```

## Motoarena matched-venue calibration foundation (#39)

The [Motoarena matched-venue report](calibration/motoarena-matched-venue.md)
measures the frozen #38 production path on a concrete calibration geometry:
published 318 m track length, two 62 m straights, 31 m reference-radius
approximation, 12 m straight widths, and current published first/second bend
widths of 17.0/16.2 m. Because production `TrackGeometry` has one symmetric
turn width, the primary fixture transparently uses their 16.6 m arithmetic
mean. This is classified as
`DerivedSymmetricWidthApproximationFromCurrentPublishedBendWidths`, not as a
published dimension. Production physics receives only the existing
`TrackGeometry`; venue identity remains in `CoreSim.Analysis`.

The real-data subset uses exact ordinal selection only:
`season == 2026`, `league == "PGEE"`, and
`source_track_label == "Motoarena im. Mariana Rosego"`. The 2025 `Toruń` label
is not inferred as an alias. Subset distributions reuse the sole deterministic
quantile contract and leave `CleanPhysics` unchanged.

The public radius convention and start-line offset are not verified. The report
therefore labels 31 m as
`ExternalPublishedRadius / MeasurementConventionNotExplicitlyVerified`, uses a
provisional 31/31 m home-straight split with 25/37 and 37/25 sensitivity checks,
and treats L1 as `StartLineSensitiveContext`. Both corners remain a
`SymmetricGeometryApproximation`; banking is known missing venue physics. The
report includes 16.2/16.6/17.0 m symmetric-width sensitivity, retaining the
older article's general 18 m bend width only as `OlderArticleReferenceOnly`.
The historical 56.50 s record is context only.

Regenerate offline from the repository root:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- \
  motoarena-matched-venue-report \
  data/calibration/pge/v1 \
  docs/calibration/motoarena-matched-venue.md
```

#39 changes no production physics constant and performs no tuning. The complete
`data/calibration/pge/v1` snapshot and all historical calibration reports remain
byte-identical.

## Straight drive-envelope shape experiment (#41)

The deterministic [#41 experiment report](calibration/straight-drive-envelope-experiment.md)
tests exactly five frozen multiplier shapes on the #39 Motoarena fixture. A0 is
the reviewed production envelope. R08/R12 are signed-area-control proxies and
H08/H12 retain progressively more high-speed drive after suppressing the same
lower-mid-speed band. The perturbation is calibration-only and enters the
existing `CalibrationRunner -> HeatSimulator -> SimulationEngine` path through
an internal immutable `HeatSimulationOptions` context. It is consulted only for
ordinary ADVANCED Straight traversal. Standing start, continuous-corner drive,
the TurnExit compatibility helper, Legacy and default production options remain
on the existing shared envelope.

The menu shows a consistent causal result on fixed LateralPosition 1: every
non-baseline candidate raises Vmax, but every one also shortens an already-too-
fast flying lap. The bounded screen is therefore classified
`StraightEnvelopeShapeInsufficient`; no candidate is made the production
default. H12 is retained only as the strongest sensitivity/trace diagnostic.
The next isolated subsystem indicated by this evidence is corner-speed loss /
Straight-to-apex amplitude, without changing it in #41.

Regenerate from the repository root:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- \
  straight-drive-envelope-experiment-report \
  docs/calibration/straight-drive-envelope-experiment.md
```

The report uses invariant culture, LF line endings and stable ordering. It does
not rewrite `pge-v1` or the historical #38, #39 and #40 reports.
