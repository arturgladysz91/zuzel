# Architecture

Current decision precedence: `AdaptiveDecisionModel` uses bounded production-backed
Entry/Apex/Exit planning described in [current evidence](calibration/trajectory-intent-evaluation.md).
Any earlier static route-time projection descriptions are historical. `TrajectoryEvaluator`
replays the existing `SimulationEngine` with isolated state and scripted phase targets;
it owns no alternative physics. Immutable Track/topology is shared by snapshots.

## Project boundaries

- `CoreSim` contains deterministic simulation rules and domain state. It has no UI, file system or console dependencies.
- `Sandbox` is a console host for manual runs and balance experiments.
- `CoreSim.Tests` protects physical constraints, determinism and manager-facing decisions.
- `docs` is the source of truth for terminology and simulation invariants.

## Core flow

Version precedence: the older longitudinal paragraphs below describe the #24–#37
foundation. Their segment-gated TurnEntry scrub and TurnExit drive statements are
historical, superseded in advanced production by the continuous-corner envelope
contract. Current advanced flow is constraint/incident against the local
CornerProgress envelope, then one distance-conserving corner traversal containing
correction, carry and smoothly available signed drive. Lateral movement receives
the complete segment duration (launch excludes stationary reaction), not drive
time alone.

1. The host creates a `Track` from ordered segments and immutable `TrackGeometry`, plus a separate per-segment/per-lane `TrackState` and rider states.
2. Optional manager actions change the surface through `TrackEvolution.ApplyTrackWork`.
3. Optional setup advice is resolved by `SetupResolver`; trust and rider independence decide whether it is accepted, adjusted or ignored.
4. `HeatSimulator.SimulateHeat` advances weather and delegates one segment at a time to the existing `SimulationEngine`.
5. Every segment has four explicit phases:
   - `CaptureSnapshot` copies every rider and every surface cell into a detached immutable view and preserves the track's immutable geometry.
   - `Decide` gives every active rider the same snapshot; legacy decision models receive a mutable clone, never the source state.
   - `Resolve` retains entry-based outcome/incident classification. Fixed lines traverse the exact existing envelope/longitudinal path. Moving advanced riders use `ExecutedPathTraversal`, which jointly solves time, lateral endpoint and physical distance in bounded one-metre steps and returns immutable `ExecutedSegmentPath`. Existing longitudinal primitives remain shared.
   - `Commit` applies rider changes, logs and wear in rider-id order. Moving wear consumes executed step samples; fixed-line wear retains the original entry kernel and deltas.
6. `RiderPosition.TotalSegmentProgress` is the canonical topological position. Lap, segment index and progress in the segment are derived from it; physical distance is advanced atomically with it.
   `LastResolvedSegmentId` (also exposed through the compatibility alias `CurrentSegmentId`) is observational metadata containing the real `TrackSegment.Id` committed for the previous step. It is not used by classification, physics or track occupancy.
7. `RaceProgressTracker` and final classification sort by canonical track progress, with elapsed time and rider id used only as deterministic tie-breakers.
8. The result contains classification, points and a deterministic log. Morale is updated after the heat.

Race randomness is stateless and addressed by seed, heat id, step number, rider id and a named `RandomChannel`. Optional discriminators such as lane or the other rider make separate draws explicit. Collection order and the number of unrelated draws cannot reassign randomness.

Track.CornerTopology is the canonical immutable grouping of maximal contiguous
turn runs. It never joins a run ending at the lap boundary to one beginning on
the next lap. CornerPhaseContext derives its [0,1] CornerProgress,
subsegment start/end progress, total physical length and remaining physical
length by summing actual arc lengths at the rider's immutable entry
LateralPosition. Advanced Resolve carries this context into diagnostics and
SegmentPhysicsContext; it introduces no mutation or RNG. Immediate Straight
lookahead resolves the next logical corner and queries the same envelope at
CornerProgress zero that production traversal uses. TurnEntry, TurnMiddle and
TurnExit remain topology/reporting compatibility labels, not longitudinal
physics phases.

`Lane` and continuous `LateralPosition` are dimensionless normalized `0..4` coordinates. `LaneModel` is the canonical conversion boundary: `fraction = LateralPosition / 4`, usable span is physical segment width minus the 1 m inner reference offset and provisional 1 m outer game margin, and physical offset is `fraction * usableSpan`. `TrackGeometry` owns independent straight/turn widths; `InnerRadiusMeters` is the radius of the inner FIM measurement/reference trajectory 1 m from the inner edge, not the kerb.

These names describe separate axes and roles: LateralPosition is continuous
cross-track position, CornerProgress is longitudinal progress within one
logical corner, TurnEntry/TurnMiddle/TurnExit are legacy-compatible
segment labels, and Lane is a discrete decision/constraint reference.

`LateralMovementModel` is the single owner of time-based movement between reference positions. It preserves the `0.35..0.65` normalized lane-units/s execution capability, converts the budget to metres using reference spacing derived from the current segment width, moves in physical space, and converts back to `0..4`. It selects the nearest unexecuted reference from `LateralPosition` toward `TargetLane`; a discrete lane forced farther away by `RunWide` cannot skip that reference. `SimulationEngine` supplies immutable snapshot inputs and commits the returned position later; `AdaptiveDecisionModel` measures route-change distance from `LateralPosition` but may still price that choice using style.

`LateralSpaceModel` converts continuous normalized positions to physical separation using the current segment's straight or turn width. `AdaptiveDecisionModel` uses it for occupancy. Contact candidate selection uses the same local-width conversion on post-`ResolveRider` positions, with a separate provisional `0.55 m` threshold; candidates are fixed before any contact effects. Contact probability remains unchanged, and contact surface plus random discriminators still use the trailing rider's discrete `Lane`. For `ContactLostRhythm`, `LateralMovementModel` converts the provisional physical `0.50 m` outward displacement with the current segment width. The existing discrete outward `Lane` step remains the maximum reference for that continuous displacement; straight segments and riders already on lane 4 receive no outward push. This is not a final motorcycle contact-response model.

`TrackStateSnapshot.SampleSurface` interpolates adjacent stored raw Grip/Ruts/Moisture and recomputes EffectiveGrip. Fixed lines keep the entry sample exactly. Moving steps sample local surface for movement, force, correction and capability. Wear blends the original center-100%/neighbor-20% kernels along executed physical distance, then normalizes to the original entry total budget; no new cells or coefficients are introduced.

Entry sampling remains authoritative for outcome classification and incident risk; moving traversal and wear follow the executed path. Contact remains a later event and does not rewrite passage wear. TrackState/TrackEvolution still store five bands; candidate decision/contact/legacy surface logic remains discrete.

Width is segment-local. The same normalized position denotes the same fraction of usable span on a straight and a turn, so its physical offset changes at a boundary without a synthetic lateral event. This coarse geometry has no gradual straight-to-turn width spline and adds no transition distance or time. Fixed turn distance uses the entry-position radius; moving traversal includes its actual cartesian/polar distance and couples lateral movement with time.

`ResolvedSimulationStep.Motions` exposes one immutable `ResolvedRiderMotion` for every active resolved rider. Its read-only time sampler adapts the actual moving path, observed fixed straight/launch integration endpoints and existing timed fixed corner nodes. It includes snapshot t=0 and stationary reaction, exact final state/diagnostic reconciliation, rider heat-clock origin, explicit coarse state events and both offsets of every segment boundary coordinate reinterpretation. Sampling and future interaction may interpolate only within a consumed segment frame, never across boundary or model-event discontinuities. No new fixed-line integrator, shared cache or interaction logic is introduced. See [canonical motion contract](calibration/resolved-rider-motion.md).

`SegmentPhysics` remains the constraint and outcome resolver. Advanced turn thresholds are evaluated against the continuous envelope at the rider's current CornerProgress, not against one settled speed for an entire labelled segment. The subsequent continuous-corner traversal uses the existing turn full-drive force endpoint (`1.20–2.80 × gearing × surface`) with a smooth net-drive availability after the apex. At and below the unchanged `16 m/s` positive-drive reference the one-gear force envelope is exactly `1`; above it the calibrated gearing-dependent fade applies. Full-drive acceleration remains signed and there is no artificial attainable ceiling.

Advanced `Straight` receives a deterministic force-based `StraightSpeedProfile` over the actually remaining distance without any attainable top-speed ceiling. It partitions distance, never time, into steps of at most the **PROVISIONAL / NUMERICAL INTEGRATION RESOLUTION** `1 m`; the final remainder preserves the exact distance. Each full-drive step predicts speed from acceleration at the start, evaluates corrected acceleration at `(start + predicted) / 2`, and applies `v_next² = max(0, v_start² + 2*a_mid*ds)`. #36 calibrates Straight reference acceleration to `1.60–3.20 m/s²` through Speed, times the unchanged entry-sampled `0.75 + 0.25 * EffectiveGrip` and `1.10–0.90` gearing drive multiplier. Reference force is `142 kg * referenceAcceleration + resistance(16)`. The same one-gear envelope and unchanged `40 + 0.20*v²` resistance used by TurnExit then produce signed net acceleration; surface remains sampled once, not at each numerical step.

Standing start, corner drive and Straight share the one-metre integration resolution, one-gear envelope, resistance, signed drive primitive, midpoint step and correction primitive. Moving execution adds a bounded scalar time solve around those primitives, required apex/full-drive/target/end splits and physical cartesian/polar geometry. The same path feeds distance, time, lateral endpoint, profiles, typed diagnostics and wear. See [method and bounds](calibration/executed-trajectory-method.md).

For an immediate logical corner, the approach target is the canonical envelope at CornerProgress zero. Before the provisional apex at `0.5`, recoverable speed is `sqrt(v_apex² + 2*a_correction*distanceToApex)`; Straight and standing-start preparation consume this exact same target. A backward allowed-speed envelope then preserves the existing feasible-preparation behavior. It never lifts a naturally decelerating candidate; unavailable preparation leaves residual overspeed. The one-segment lookahead still uses immutable entry `LateralPosition`, wraps between non-final laps, and omits the nonexistent next lap on the final segment.

#31 removes the artificial `21–25 × 0.94–1.06` ceiling and its constants/helpers from the active API. The non-production analytic `CalculateStraightSpeedProfile` is only a general constant-acceleration compatibility utility with an explicit constraint and null equilibrium; SimulationEngine never calls it. There is no second production Vmax model.

The canonical signed equation is `(F_drive(v) - (40 + 0.20*v²))/142`, without clamping net force or acceleration. Available drive is reference force times the unchanged linear envelope form: 1 at/below 16, otherwise `clamp(1 - fadeRate*(v-16), 0, 1)`, with the #36-calibrated `fadeRate = 0.0350 + (0.0100 - 0.0350)*Gearing`. Equilibrium is the diagnostic root `drive = resistance`, never a limiter, target or integration stop. Deterministic bisection brackets `[0,16 + 1/fadeRate]`, uses at most 64 iterations and bracket-width tolerance `1e-6 m/s` (float output). Reference force <40 N has no nonnegative root and is rejected; exactly 40 N returns zero. The shared signed midpoint predicts and corrects via `sqrt(max(0,v²+2*a*ds))`; only squared speed is bounded for safe stopping. Below equilibrium full drive accelerates; above it full drive naturally decelerates.

#36 calibrates only the Straight/turn reference-acceleration ranges and both fade endpoints. Mass, resistance, positive-drive reference speed, integration step, gearing and surface mappings remain **PROVISIONAL / NOT REAL-WORLD CALIBRATED** and frozen. Surface still scales effective reference drive and therefore equilibrium; resistance itself stays surface-independent. Morale and TractionBias do not affect forces/equilibrium, and there is no separate gearing top-speed multiplier. The pure zero-drive helper `-(40+0.20*v²)/142` is not final engine braking. Continuous corner correction is the effective roll-off / engine-drivetrain / slide-preparation capability, not zero-drive resistance. No explicit throttle or engine/traction split, wheelspin or traction-force cap is added.

A real speedway motorcycle runs one gear during a race and has no conventional braking system; its final-drive ratio is a setup choice. Speed loss before and through a corner must not be interpreted as road-motorcycle braking: rolling off the throttle, setting the motorcycle, slide and resistance can all contribute. Advanced physics now uses the unchanged `2.00–3.20 m/s²` SlideControl-and-surface correction capability over physical distance whenever speed exceeds the local continuous envelope. No labelled segment owns a separate scrub phase. `Brake`, `RunWide` and `Crash` classify excess over the local recoverable envelope; `Brake` remains an outcome name rather than a literal mechanical brake.

#38 calibrates only the advanced settled-corner reference from `16` to `19 m/s` after measuring architecture-only A0 and the bounded B17/B18/B19 menu. The legacy/reference compatibility path remains at `16 m/s`; all #36 longitudinal and standing-start constants, correction capability, outcome factors, contact/lateral/surface behavior and RNG remain unchanged. Power and torque curves, RPM/rev limiting, real sprockets, wheel radius, clutch, wheelspin, slip ratio, traction-force cap, CdA and wind remain unimplemented.

### Standing-start topology and resolution

`Track.CreateExample()` is the unchanged eight-segment compatibility layout with synthetic 6 m straight/turn widths, preserving the former 1 m reference spacing; it is not a regulatory track. `CreateStandingStartExample()` keeps the `(60 m, 24 m, PI/3)` longitudinal/radius topology and uses independent FIM-minimum-example widths of 10 m straight and 14 m turn. It overrides the home-straight halves around the canonical start/finish boundary between segments 8 and 0: marked start-half Straight 35 m, TurnEntry/Middle/Exit, back Straight 60 m, TurnEntry/Middle/Exit, finish-half Straight 35 m. The home straight is 70 m, with 35 m from the start line to the first turn. Physical lap distance is `130 + 2 * PI * (24 + 3 * lateralPosition)` metres: approximately 280.796 m on the inner measurement/reference trajectory and 356.195 m on the outer reference trajectory. Finish remains generic last-segment advancement, not an extra virtual launch distance or a hardcoded nine-segment rule.

`TrackSegment` accepts optional `StraightLengthMetersOverride` (finite, positive, Straight only) and `IsStandingStartSegment` (default false, Straight only). Track copies preserve both. At most one marker is allowed; it must be topology index zero and the last segment must also be Straight.

Launch eligibility requires advanced physics, lap/segment zero, explicit marker, exact canonical and physical heat-start position, NotStarted, non-positive speed and positive remaining distance. Only then entry speed is exactly zero, passed through the existing Straight SegmentPhysics and incident-resolution order before a `StandingStartLaunchProfile` replaces (never accompanies) `StraightSpeedProfile`. Later laps and unmarked tracks retain their existing paths. Launch and production Straight query the same next logical-corner envelope at CornerProgress zero. The shared preparation-boundary helper constrains signed full drive only when preparation is needed; it never increases a naturally decelerating candidate. This yields the fastest feasible acceleration/optional cruise/preparation profile; peak may exceed exit speed. Lookahead still sees only the immediate next segment, not both halves across the boundary.

Reaction is `0.28 + (0.20 - 0.28) * StartNorm` seconds (0.28/0.24/0.20 for Start 0/50/100). Reference acceleration is `(9.0 + (11.0 - 9.0) * StartNorm) * (1.10 + (0.90 - 1.10) * Gearing) * (0.75 + 0.25 * EffectiveGrip)`; reference force is `142 * acceleration + resistance(0)`. The shared exact 1 m distance-step creator and midpoint signed-drive helper apply the existing one-gear envelope and resistance `40 + 0.20*v²`, without an artificial ceiling. Entry surface is immutable and sampled once. TractionBias and morale have no launch-force or reaction role. These constants are **PROVISIONAL / NOT REAL-WORLD CALIBRATED**, not tuned to TimeTo70 or SpeedAt2s.

`segmentElapsedTimeSeconds` is reaction plus launch movement; `lateralMovementTimeSeconds` is only movement. During reaction speed, distance, progress and lateral displacement remain zero. Non-launch paths use identical elapsed and movement budgets. Canonical `Position.Advance` accounts for physical distance exactly once and status becomes Racing (or Finished via existing rules). Existing contact processing remains after launch: diagnostics/profile time stays pre-contact while final sample duration may include LostRhythm time.

Diagnostics retain the actual typed launch profile, prefer its peak before other profiles/fallback, and then include final post-contact speed. Acceleration + cruise + preparation distances equal actual launch distance; movement sums `2*ds/(v_start+v_end)` over all steps, and total adds reaction. Nine nullable CSV/sample fields expose reaction, movement, total profile time, acceleration/cruise distances, entry acceleration, time to 70 km/h, speed at 2 s, and finally preparation distance without reordering earlier columns. These metrics are measured outputs, not targets. Threshold crossing and speed observations use the effective corrected step acceleration `(v_end²-v_start²)/(2*ds)`, including preparation, not whole-profile interpolation. Both observations count from tape movement and include reaction. TimeTo70 is null if not reached; speed at 2 s is null if launch ends earlier. Zero distance yields reaction but no movement. No clutch/RPM, torque/power, real sprockets, wheelspin/slip/traction cap, false starts, reaction randomness, gate bonus or explicit throttle input is introduced.

`StartingGrid.Create` is the explicit physical start input: four unique rider-to-A/B/C/D assignments, independent of list order/id, full-width `W/4` fields and neutral centers transformed through `LaneModel`. Immutable initial bounds/identity pass through `RiderSnapshot`; they do not enter launch forces, decisions or movement constraints. The Sandbox standing-start example uses this API. `Track.StartFinishLine` names the existing canonical boundary without changing topology. The current Motoarena fixture uses the documented uncertain 35/27 baseline; historical experiments explicitly keep their frozen 31/31 inputs. See [current geometry](calibration/physical-starting-gates.md).

`BalanceAnalyzer` is an offline historical compatibility diagnostic. It rotates the same four profiles through racing-reference starting positions, not the new physical fields. Its output is not final gate-effect calibration. No hardcoded gate A bonus exists. Detailed logging is disabled for these batches, while track evolution and race rules remain active.

The #28 calibration telemetry harness is an observation boundary, not a physics calibration. `HeatSimulator.SimulateHeat` accepts an optional production `ISimulationStepObserver` and invokes it exactly once after `Resolve` and before `Commit`. `ResolvedSimulationStep.Diagnostics` retains the exact production values already calculated for each rider: travelled distance and time, actual peak, profile full-drive equilibrium when evaluated, TurnExit net acceleration when applied, and the actual Straight and TurnEntry profiles. The observer receives detached/read-only resolution data; it does not run RNG and cannot affect decisions, commit order, wear or classification.

`CalibrationTraceCollector` converts that typed result into ordered step samples without parsing `SimLog`. It derives time and distance deltas from the immutable entry snapshot and resolved state change, uses the exact entry-sampled surface, and derives completed-lap and final-rider summaries that reconcile with `HeatResult`. `CalibrationRunner` only attaches the collector to the production `HeatSimulator`; it contains no laps-by-segments loop and mutates the caller-supplied rider and track state in the same way as a normal heat. Logging and telemetry are independent, so `EnableLogging=false` still produces a complete trace. The detached trace and invariant-culture CSV exports use stable rider/step ordering.

This harness reports what production does now and contains no real-world target dataset. From #29 it retains the exact production `TurnExitDriveProfile`; the existing TurnExit acceleration scalar means entry net acceleration at the profile start. From #30 it also retains the production standing-start profile. Unmarked-track bootstrap is compatibility behavior, not a physical launch. #31 supplies signed forces and natural deceleration without an artificial ceiling; real-world target calibration is next. Later refinements include F_engine vs F_traction, wheelspin/slip, TractionBias, RPM, torque/power, real sprockets, throttle, engine braking and within-gate positioning/footprint.

#31 is a breaking diagnostic-schema change: `FullDriveEquilibriumSpeedMetersPerSecond` replaces `AttainableTopSpeedMetersPerSecond` in diagnostics, sample and CSV. Current advanced production obtains it from the actual StandingStart, Straight or continuous-corner profile. Historical TurnExit fields remain stable schema compatibility fields and are null on the #38 path; #38 appends typed continuous-corner observations. Invariant decimal dots, `\n` and empty nullable fields remain. Equilibrium never feeds speed resolution.

The presentation layer must consume results and logs; it must never change the simulation outcome.

## Real-world calibration boundary (#32)

External PGEE files are normalized offline by the standard-library Python tool into `data/calibration/pge/v1`; CoreSim never depends on Python or performs source file/network access. `RealWorldCalibrationDataset` accepts text, validates the snapshot, and exposes typed distributions. `CalibrationSkillSweep` measures only the production `CalibrationRunner -> HeatSimulator` path, and `RealWorldCalibrationEvaluator` emits component comparisons without an optimizer or overall score. Sandbox owns filesystem I/O and deterministic Markdown report generation.

Telemetry values are population envelopes, not hard limits. Source gate `speed_2s`/`curve_speed` values remain rankings, reaction observations come from separate task-supplied literature context, and absolute timing stays context until concrete track geometry matches. Rider category is not a physics multiplier; no real rider is assigned a game skill. #32 is observational and changes no physics constants or outcomes; #33 is the first empirical-physics tuning boundary.

## Compatibility

Compatibility is intentionally limited to `HeatSimulator.Simulate`, `SegmentPhysics.Apply(segment, lane, speed)` and the older `IRiderDecisionModel.Decide(TrackSegment, RiderState)` overload. The legacy heat pass also uses the four-phase commit while retaining its original neutral-surface physics, and legacy decision models receive a detached mutable rider copy.

The original `Track(segments)` constructor and geometry-free `LaneModel` overloads remain available. They use the example-compatible `TrackGeometry.Default`; new simulation paths pass the geometry of the concrete track explicitly.

The canonical corner-speed path derives its base limit from a continuous lateral radius and carries no morale argument. Integer overloads delegate to the same calculation and retain exactly the reference-lane results. Morale remains in `SegmentPhysicsContext` only for the existing incident-risk calculation. The legacy `SegmentPhysics.Apply(segment, lane, speed)` and geometry-free speed overloads retain their signatures and use `TrackGeometry.Default`; the legacy heat path also keeps immediate alignment of `LateralPosition` with the resolved lane.

`RiderDecisionContext.Rider`, `Riders` and `TrackState` now expose immutable snapshot types. Code compiled against their former mutable types is not source-compatible and should migrate to the snapshot API. New gameplay code should use `SimulateHeat` and `SegmentPhysicsContext`.

## Continuous corner correction (#34, historical foundation)

For advanced turns, `SegmentPhysics` owns discrete classification and the typed
`ContinuousCorrectionTargetSpeedMetersPerSecond`; recoverable outcomes keep the
actual input speed. `SimulationEngine` then asks `LongitudinalDynamics` for a
`CornerSpeedCorrectionProfile` over actual remaining metres. `PhysicsSpeed` is
the speed after the discrete constraint/incident stage and before this profile;
final `Speed` is the speed after correction, optional carry and optional TurnExit
drive.

TurnEntry composes unchanged first-half scrub, residual correction and residual
carry. TurnMiddle composes correction and carry. TurnExit composes correction
with a drive profile only over `RemainingDistanceMeters`; a target that cannot be
reached consumes the segment and prevents drive. RunWide uses correction plus
carry and no drive. Each composition validates distance conservation, while
diagnostic `TravelTimeSeconds` is always the actual final elapsed delta and phase
times remain separately observable. Incident-created Crash/RunWide clears a
stale constraint target so the unchanged incident consequence cannot be applied
twice. Resolve remains mutation-free and adds no RNG.

## Continuous corner envelope calibration (#38)

Advanced production represents every logical corner with one canonical envelope
over `CornerProgress` rather than with longitudinal phases selected by
`TurnEntry`, `TurnMiddle` or `TurnExit`. The settled/apex capability uses the
advanced `19 m/s` reference with existing radius, surface, rider-skill and setup
factors. For `p < 0.5`, the recoverable envelope is
`sqrt(v_apex² + 2*a_correction*(0.5-p)*totalCornerLength)`. At and after the
provisional apex, the envelope is obtained by traversing the physical distance
from the apex with the existing signed turn-drive midpoint integrator.

Net drive availability is zero through the apex, then follows smoothstep from
`0.5` to `5/6`, and is one thereafter. Zero means neutral carry, not an added
resistance-only phase; one reproduces the existing full turn-drive endpoint.
Each 1 m step, plus its exact final remainder, belongs to exactly one of
correction, carry or drive. Insufficient correction preserves residual
overspeed; no speed is teleported or capped.

Straight and standing-start lookahead use this same envelope at progress zero.
Advanced `SimulationEngine` no longer invokes the old TurnEntry scrub or a
segment-gated TurnExit drive. Legacy remains on the previous compatibility
path. `ApexProgress = 0.5` is a provisional geometry assumption and
`FullDriveProgress = 5/6` is a behavior-derived starting assumption, not a
telemetry-derived fit. The calibration changed no #36 longitudinal constants,
standing-start constants, correction capability, outcome factors, incident,
contact, lateral or surface rules.
