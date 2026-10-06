# Core simulation specification

Current racing-response precedence (#56B): [bounded pre-contact contested-space responses](calibration/contested-space-racing-response.md)
adds an opt-in heat-scoped coordinator after independent #54 planning. Joint alternatives
execute production motion and are verified by #55; legacy consequences are authorized once
for an unresolved eligible episode. Feature OFF retains the merged #56A behavior. Strength,
MassKg, canonical PairRiding, impact severity and body-contact response remain outside #56B.
Earlier statements that traffic behavior is future work describe the historical feature-OFF stage.

Current occupancy precedence (#55): [physical occupied-space and independent bike attitude](calibration/physical-occupancy-contested-space.md)
adds explicit observation-only mechanical capsules and common-time conflict diagnostics.
Older statements that motorcycle dimensions/contested space are absent describe their historical stage.
Legacy contact formulas and #54 solo projection remain unchanged. The opt-in #56B
coordinator now supplies pre-contact responses and episode-owned legacy eligibility.
Straight, corner and start/finish poses share one metric model-space
track frame across closed laps. Width coordinate reinterpretations remain zero-time, unswept
state transitions; genuine unrelated/nonclosing frame inputs retain explicit diagnostics.

Current decision precedence: [Production-backed trajectory decisions](#production-backed-trajectory-decisions)
supersedes prior static `AdaptiveDecisionModel` route-time scoring descriptions.

This document defines the binding invariants of the speedway manager simulation.

Version precedence: [Continuous corner envelope calibration (#38)](#continuous-corner-envelope-calibration-38)
supersedes earlier #24–#37 descriptions of segment-gated TurnEntry scrub,
TurnMiddle carry and TurnExit drive in advanced production. Current advanced
corner physics is a single distance-conserving traversal over logical-corner
progress. Peaks include every actual pre/post correction and drive speed;
moving lateral paths couple local movement time to executed geometry, with stationary launch reaction
excluded. Fixed-line traversal remains exact. Legacy behavior is unchanged.

## Scope

The core simulates decisions and believable consequences. It does not integrate a complete motorcycle physics model. Rendering, economy, league rules and persistence remain outside `CoreSim`.

## Determinism

Given identical domain state, options and random seed, a heat must produce identical classification, typed surface changes and text logs. Reordering riders in the input collection must not change the result associated with any rider.

Every segment is processed as `CaptureSnapshot -> Decide -> Resolve -> Commit`. Decisions use one detached snapshot of all riders and all relevant surface cells. Neither decisions nor resolution may mutate live state. Random samples are addressed by seed, heat, step, rider id and a named channel; shared generator sequence, collection index, `string.GetHashCode()` and `HashCode` are forbidden for race outcomes.

## Track

- A track is an ordered list of `TurnEntry`, `TurnMiddle`, `TurnExit` and `Straight` segments.
- Every concrete track owns immutable `TrackGeometry`: straight length, independent physical straight/turn widths, inner reference turn radius and the angle in radians covered by one turn segment. Geometry is separate from mutable surface state. Generic widths must be finite and greater than the 2 m sum of reference margins; they are not forced to FIM minima so synthetic fixtures remain valid.
- Every segment has five local normalized reference positions numbered `0..4` from inside to outside. Their physical spacing is derived from the local segment width, never stored as one global value.
- `TargetLane` is the decision's requested destination. `PlannedLane` is the nearest discrete reference lane executed in the current step, `Lane` is the discrete lane resolved by physics for that step, and `LateralPosition` is the rider's actual continuous position at the end of the step.
- `LateralPosition` is dimensionless in the inclusive range `0..4`; `normalizedFraction = LateralPosition / 4`. Physical offset from the inner reference trajectory is `normalizedFraction * usableRacingWidth`, where usable width is local physical width minus the 1 m inner reference offset and provisional 1 m outer margin. The inverse is `offset / usableRacingWidth * 4`; pure conversion rejects values outside the physical span. Invalid, NaN and infinite values are rejected at the domain boundary.
- `InnerRadiusMeters` is the radius of the canonical FIM measurement/reference trajectory 1 m from the physical inner edge, not the kerb radius. The 1 m outer margin is a provisional game-geometry choice, not an FIM rule. Rider/motorcycle width is not modeled.
- `RiderPosition.TotalSegmentProgress` is the single topological source of truth. Lap, segment index and normalized `0..1` segment progress are derived values. Physical distance is updated together with canonical progress.
- Rider status distinguishes not started, racing, finished, crashed and retired states.
- Every segment/lane cell stores base grip, ruts and moisture. `EffectiveGrip` derives usable grip from all three values.
- The immutable `TrackStateSnapshot` can sample physical surface at a continuous lateral position. It linearly interpolates adjacent cells' raw `Grip`, `Ruts` and `Moisture`, then constructs a new `TrackSurfaceState`; the nonlinear `EffectiveGrip` is recomputed from that raw state and is never interpolated directly. Exact integer positions return the corresponding stored reference cell.
- Advanced fixed-line wear retains the existing entry kernel. Moving wear integrates that kernel over actual step midpoint samples, weighted by physical distance and normalized to the original entry-kernel total budget. Raw surface storage remains five bands; writes remain in ascending lane order.
- Weather changes moisture unevenly. Rider passages wear the used lane and nearby material. Manager work can grade, water or pack a selected area.

## Physical constraints

- In advanced physics, entry classification uses entry position; moving local capability reevaluates actual position from the immutable snapshot: `R = InnerRadiusMeters + (LateralPosition / 4) * usableTurnWidth`, followed by the unchanged `v_geometry = 16 m/s * sqrt(R / 24 m)`. On the 14 m standing-example turn, positions `0..4` have radii `24/27/30/33/36 m`; position 2.5 has radius 31.5 m. A larger radius changes the existing constraint through geometry, without an inner/outer speed bonus or tuned constant.
- Advanced fixed-line turn distance remains exactly `R * TurnSegmentAngleRadians`. Moving turn steps use `sqrt((r_mid * dθ)^2 + dr^2)`, with local `R = InnerRadiusMeters + actual physical offset`. Requested/resolved targets do not replace actual geometry.
- Entry surface still classifies the unchanged physical outcome and incident risk. Moving traversal then uses immutable start-step surface for the unchanged lateral rate, midpoint surface for drive/correction, and endpoint surface/radius for the local envelope. No post-hoc full-segment lateral update occurs.
- Moving wear follows executed step samples; fixed-line wear remains centered on entry. Contact displacement is a subsequent event and does not rewrite the path. Physics crashes still skip passage wear.
- `TurnSegmentAngleRadians` changes path distance and time spent in a turn segment, but it does not change the radius-derived speed boundary. Surface, relevant control skill and setup fit modify that boundary. Morale does not; it may affect a later decision, accepted risk and quality of execution.
- `StraightLengthMeters` remains the fixed canonical longitudinal span. Moving physical distance is `sqrt(dx^2 + dy^2)` per step, with target arrival separating diagonal and held portions. Legacy distance remains discrete.
- This curvature-based boundary is a simulation constraint, not a complete motorcycle dynamics or cornering model.
- Deliberately choosing a wide line is a planned trajectory and can be advantageous for surface, passing or corner-exit reasons. It changes `PlannedLane`; when the physical constraint is satisfied, `Outcome` may remain `Ok` and the final `Lane` equals `PlannedLane`. `RunWide` is an unplanned or forced outward consequence of excessive speed, an error or contact; the two concepts must not be conflated.
- A speed-triggered `RunWide` finishes one lane wider than `PlannedLane`, lengthening the route. It corrects only the overspeed above the rider's `MaxSafeTurnSpeed`: `correctedSpeed = maxSafeSpeed + (entrySpeed - maxSafeSpeed) * overspeedRetention`. Consequently, its physical speed remains at least the safe boundary but is strictly lower than entry speed.
- Advanced physics interpolates overspeed retention from `0.35` to `0.65` using normalized `SlideControl`; legacy physics without rider data uses the neutral value `0.50`. Better control reduces the loss but never removes the consequence. Random incidents retain their existing separate resolution.
- Line advantage emerges from the geometry of the concrete track, surface and complete trajectory. A projected comparison of extreme-line lap times is a diagnostic for that track, not a global equality test or a fixed percentage invariant.
- A concrete track may favor a particular line. No line may be universally best across all track geometries and surfaces.
- `MaxSafeTurnSpeed` is the settled physical corner speed. From #24 it is not the maximum permissible speed at the first instant of advanced `TurnEntry`; a higher approach speed is valid when the distance-limited first phase can scrub it to the settled boundary.
- Residual overspeed after the advanced `TurnEntry` scrub must cause the existing quiet correction, braking, running wide or a crash.
- A rider cannot run wider than lane 4; an unresolved high-speed run-wide there becomes a crash.
- Legacy straight resolution preserves speed. Advanced straight traversal uses the provisional distance-limited longitudinal profile defined below; it remains deterministic and does not add an arbitrary lane bonus.

## Longitudinal movement

- `SegmentPhysics` remains the physical constraint and outcome resolver, not a complete longitudinal dynamics model. `RiderStateChange.PhysicsSpeed` is its post-resolution speed; final `Speed` may differ only through a later explicitly eligible consequence.
- #31 removes artificial attainable-speed ceilings from production Straight, turn full-drive and StandingStart. All three use signed full-drive force: `a(v) = (F_drive(v) - F_resistance(v))/142`, without `max(0)` on net force or acceleration. Below equilibrium the rider accelerates; above it the rider naturally decelerates.
- Natural `FullDriveEquilibriumSpeedMetersPerSecond` observes `F_drive(v) = F_resistance(v)`, not a clamp or target. Deterministic bisection uses `[0,16+1/fadeRate]`, at most 64 iterations, bracket-width tolerance `1e-6 m/s`, float result, no RNG/initial guess/hardcoded Vmax. Force <40 N has no nonnegative root and is rejected; force=40 N returns 0. Production never assigns/clamps speed to equilibrium or stops integration on reaching it. The separate gearing top-speed multiplier is removed.
- Continuous-corner drive uses the deterministic shared signed midpoint integrator. Its availability comes from CornerProgress rather than a TurnExit eligibility check. A wider physical entry position can create more speed only through radius and longer geometric path, never through a lane bonus.
- The #36 calibration target is `referenceAcceleration = baseAcceleration * gearingDriveMultiplier * surfaceDriveMultiplier`. Corner full-drive base acceleration interpolates from `1.20 m/s²` to `2.80 m/s²` using normalized `Speed`; gearing remains `1.10` at `Gearing = 0` to `0.90` at `1`; surface drive remains `0.75 + 0.25 * entrySampledSurface.EffectiveGrip`. Available force is calibrated at the unchanged positive-drive reference `16 m/s`: `F_drive = 142 kg * referenceAcceleration + F_resistance(16)`.
- The one-gear drive envelope is exactly `1` for `v <= 16 m/s`. Above reference speed, #36 calibrates `fadeRate = 0.0350 + (0.0100 - 0.0350) * Gearing` in `1/(m/s)` while retaining `envelope = max(0, 1 - fadeRate * (v - 16))`. Actual available force is `F_drive(v) = F_drive_reference * envelope`. Drive-oriented gearing therefore keeps its stronger reference force but fades faster; speed-oriented gearing starts lower but retains force longer, allowing the curves to cross naturally. This is a coarse abstraction, not an RPM model, torque/power curve, rev limiter or real sprocket mapping.
- Aggregate resistance remains `40 + 0.20*v²` N. Signed net force is `F_drive(v) - F_resistance(v)`, and acceleration divides it by nominal system mass 142 kg, not an individual rider Weight. The 16 m/s reference speed, mass and resistance remain **PROVISIONAL / NOT REAL-WORLD CALIBRATED** and frozen in #36; only the reference-acceleration ranges and fade endpoints receive the bounded first calibration. No CdA, wind or resistance-component split is added. Pure zero-drive acceleration is `-(40+0.20*v²)/142`: negative even at zero, but NOT final engine braking and NOT a replacement for the existing effective roll-off / engine-drivetrain / slide-preparation capability.
- Fixed-line distance and surface retain the immutable entry sample exactly. Moving traversal integrates actual geometry and resamples the immutable surface along its path. Surface and `Speed` affect reference force through the unchanged #25 formula. `Gearing` affects that reference force and separately the envelope shape, representing two distinct trade-off aspects; no third gearing multiplier exists. The envelope is independent of surface and `Speed` skill. `TractionBias`, morale, style, `SlideControl`, other skills and RNG do not modify it. Resistance is surface-independent in this foundation.
- `Brake` may recover speed after the constraint. `RunWide` receives no positive drive, and `Crash` remains at zero speed with its existing partial-distance semantics. For other advanced turn outcomes, drive availability is determined only by CornerProgress. Legacy physics preserves its existing post-resolution behavior.
- Advanced `Straight` uses a speed-dependent force-based `StraightSpeedProfile` over the actually remaining distance. The **PROVISIONAL / NUMERICAL INTEGRATION RESOLUTION** is fixed distance `1 m`, never a time/frame step; the final step is the exact shorter remainder and classified phase distances reconcile to the requested distance.
- Straight reference acceleration is the #36-calibrated `1.60–3.20 m/s²` interpolation through normalized `Speed`, multiplied by the unchanged entry-sampled `0.75 + 0.25 * EffectiveGrip` and `1.10 + (0.90 - 1.10) * Gearing` drive multiplier. Reference force is `142 kg * referenceAcceleration + F_resistance(16)`. The generic at-speed force calculation multiplies this by the same one-gear envelope as TurnExit; subtracting the unchanged `40 + 0.20*v²` resistance and dividing the signed remainder by `142 kg` yields Straight net acceleration. Surface is sampled once at immutable entry `LateralPosition`, never once per integration metre.
- Every full-drive step computes signed `a_start`, predicts with `sqrt(max(0,v_start²+2*a_start*ds))`, evaluates signed `a_mid` at `(v_start+v_predicted)/2`, then corrects with `sqrt(max(0,v_start²+2*a_mid*ds))`. Squared speed is bounded only for safe stopping. There is no ceiling parameter. With no next-turn target, long traversal converges naturally toward force equilibrium from below or above.
- With an immediate logical corner, its continuous envelope at CornerProgress zero supplies the maximum-recoverable target for the backward preparation envelope: `allowedStart² = allowedEnd² + 2*cornerEntryDeceleration*ds`. Final forward speed is `min(fullDriveCandidate, max(allowedEnd, DecelerateOverDistance(start,deceleration,ds)))`. The upper constraint cannot raise a naturally decreasing full-drive candidate or teleport to an unreachable target. Insufficient preparation leaves residual overspeed. Natural resistance can exceed available preparation deceleration; only the explicit preparation is bounded by that capability.
- Straight travel time is the sum of every step's `2*ds/(v_start+v_end)`, not a total-distance endpoint average. Advanced lateral movement receives this exact sum. Peak speed is the maximum node speed; each step contributes its whole `ds` to acceleration, cruise or deceleration using a `1e-6 m/s` phase tolerance. Deceleration now includes natural signed-force decrease as well as explicit corner preparation; phase distances sum to actual distance.
- The old constant-acceleration analytic `CalculateStraightSpeedProfile` remains for compatibility and unit coverage but is a non-production utility with an explicit general constraint, no attainable-speed calculation and null equilibrium; SimulationEngine never calls it.
- Lookahead is exactly one segment. Only an immediate logical-corner start creates an approach target, using that corner's immutable entry surface, full physical length and the rider's segment-entry `LateralPosition`; it uses neither `TargetLane`, `PlannedLane`, resolved lane nor post-movement position. A last straight on a non-final lap wraps to segment `0`. The last segment of the last required lap has no next-corner target and uses its remaining distance for acceleration; this is derived from `LapIndex`, `RequiredLaps`, `SegmentIndex` and segment count, with no hardcoded lap number or finish-line lane bonus.
- Advanced corners traverse the continuous envelope described below. `Brake`, `RunWide`, outer-lane crash and all existing thresholds are relative to the envelope at current CornerProgress. The `Brake` enum does not imply a conventional mechanical brake.
- Advanced StandingStart, continuous-corner drive and Straight share one 1 m resolution, exact step creator, one-gear force envelope, resistance, canonical signed force equation and midpoint. After #36 corner full-drive uses `1.20–2.80 × gearing × surface`, Straight `1.60–3.20 × gearing × surface`, while launch remains `9–11 × gearing × surface`. Profiles expose equilibrium from the exact reference force/setup used in traversal. No profile uses it as a limiter.
- #31 changes structure, not calibration. No explicit throttle input, RPM, rev limiter, torque/power, engine kW, CdA, wind, sprockets, clutch, wheelspin, slip, traction-force cap, physical gate geometry or final telemetry fitting is introduced. Surface scales effective reference force and thus equilibrium; TractionBias and morale do not. Separating F_engine and F_traction is a later refinement.

## Standing start / launch foundation (#30)

- `Track.CreateExample()` retains its eight-segment compatibility layout and uses 6 m straight/turn widths, which preserve the former 1 m equivalent reference spacing; it is a synthetic fixture, not a regulatory-size track. `Track.CreateStandingStartExample()` retains `(60 m straight, 24 m inner reference radius, PI/3 turn segment angle)`, uses a 10 m straight and 14 m turns as an FIM-minimum-width example, and has topology `Straight 35 m (marked), TurnEntry, TurnMiddle, TurnExit, Straight 60 m, TurnEntry, TurnMiddle, TurnExit, Straight 35 m`. These example widths are not universal real-track dimensions. The canonical start/finish boundary lies between segments 8 and 0. The home straight is 35 + 35 = 70 m; the first turn is 35 m after the start line. Physical lap distance is `130 + 2 * PI * (24 + 3 * lateralPosition)` metres: about 280.796 m on the inner measurement/reference trajectory and 356.195 m on the outer reference trajectory. No extra virtual launch distance is added.
- Compatible `TrackSegment(int id, SegmentType type, float? straightLengthMetersOverride = null, bool isStandingStartSegment = false)` stores immutable metadata. A length override must be finite, >0 and Straight-only; otherwise geometry supplies Straight length. A marker must also be Straight-only. Track immutable copies retain both fields; at most one marker is allowed, only at topology index zero, and a marked track must end with a Straight.
- Launch eligibility is the conjunction of advanced physics, snapshot lap index zero and segment index zero, explicit marker, rider canonical total progress exactly zero and physical distance exactly zero, status NotStarted, speed <=0 and actual remaining distance >0. No marker is inferred from segment index/type. For eligible riders entry speed is exactly zero; unmarked-track bootstrap and legacy remain unchanged. The same segment on subsequent laps uses ordinary Straight physics.
- Reaction is `0.28 + (0.20 - 0.28) * Normalize(Start)` seconds from tape movement to first motorcycle movement: Start 0/50/100 maps to 0.28/0.24/0.20 s. The named `ProvisionalStandingStartSlowReactionSeconds` and `ProvisionalStandingStartFastReactionSeconds` constants are 0.28f and 0.20f. Launch reference acceleration is `(9.0 + (11.0 - 9.0) * Normalize(Start)) * (1.10 + (0.90 - 1.10) * Gearing) * (0.75 + 0.25 * entrySurface.EffectiveGrip)` m/s². `ProvisionalStandingStartMinimumReferenceAccelerationMetersPerSecondSquared` and `ProvisionalStandingStartMaximumReferenceAccelerationMetersPerSecondSquared` are 9.0f and 11.0f. These constants are **PROVISIONAL / NOT REAL-WORLD CALIBRATED** and must not be tuned to start telemetry targets.
- Launch reference force is `142 kg * referenceAcceleration + CalculateLongitudinalResistanceForceNewtons(0)`, so net acceleration at zero preserves the reference acceleration. Launch reuses the same nominal mass, `40 + 0.20*v²` resistance, one-gear at-speed envelope, signed net-force helper, exact `1 m` distance-step creator (with final remainder), and shared midpoint signed-drive step as Straight/continuous-corner drive. There is no second integrator. Surface is sampled once at immutable entry lateral position, not per metre or after lateral movement. Morale and TractionBias do not affect reaction or launch force; no gate bonus is added.
- `StandingStartLaunchProfile` starts at zero and reports reaction/movement/total time, exit/peak speed, acceleration/cruise/preparation distance, entry net acceleration and nullable TimeTo70/SpeedAt2s. `AccelerationDistanceMeters + CruiseDistanceMeters + PreparationDistanceMeters` equals actual launch segment distance. Every step time is `2*ds/(v_start+v_end)`; movement sums all steps and total is reaction + movement. `FullDriveEquilibriumSpeedMetersPerSecond` is a diagnostic of the same launch reference force/setup, not a start target or limiter. Artificial attainable ceiling is removed.
- When the immediate next segment begins a logical corner, launch uses the same continuous envelope at CornerProgress zero as production Straight, with that corner's entry-lateral geometry and sampled surface. It builds the existing backward allowed-speed envelope using `CalculateCornerEntryDecelerationMetersPerSecondSquared` on the current entry surface. Shared signed midpoint is constrained by the preparation boundary described above, never lifting a naturally slowing full-drive candidate. The fastest feasible profile is acceleration, optional peak/cruise, then throttle roll-off/preparation toward the recoverable approach speed. There is no instantaneous speed clamp or unavailable deceleration. Straight and launch share that boundary helper. If the next segment does not begin a logical corner, no target is inferred through further segments.
- Order is entry speed zero -> existing Straight SegmentPhysics -> existing incident resolver -> launch profile -> existing contact processing -> Commit. Launch replaces, never accompanies, normal StraightSpeedProfile. Actual exit speed feeds the continuous-corner traversal. Immediate-only lookahead is not extended across finish-half -> start-half -> logical corner.
- Reaction leaves speed, distance, canonical progress and lateral position at zero. Segment elapsed time is reaction + movement, but the lateral movement budget is movement alone. All non-launch timing paths retain their previous duration. Physical distance advances through the usual `Position.Advance` exactly once; NotStarted becomes Racing without a new Launching status. Lap completion and finish are generic topology rules: on this example the fourth lap ends after segment 8, before another start-half traversal. Four riders completing four laps yield 144 samples, not a production hardcode.
- TimeTo70 uses the output threshold `70 / 3.6 m/s`, measured from tape movement including reaction. If reached inside a corrected step, crossing time uses effective acceleration `(v_end² - v_start²)/(2*ds)`, not the metre's end time. SpeedAtTwoSeconds uses the same corrected-step kinematics at 2.0 s from tape movement; speed during reaction is zero, and the value is null if the segment ends before 2 s. No following segment is predicted. These are measured outputs, never calibration targets. Zero-distance standalone profile contains reaction but zero movement, distance and speed; both metrics are null when outside that event.
- Diagnostics retain the actual production profile and its pre-contact total time. Peak is the maximum of launch, Straight, continuous-corner nodes, constraint input/output, fallback and final/contact speeds. Calibration duration remains final elapsed delta and may exceed launch total after contact LostRhythm; profile time is not recomputed after contact.
- Samples and Step CSV append nine nullable fields without changing existing column order: `StandingStartReactionTimeSeconds`, `StandingStartMovementTimeSeconds`, `StandingStartProfileTotalTimeSeconds`, `StandingStartAccelerationDistanceMeters`, `StandingStartCruiseDistanceMeters`, `StandingStartEntryNetAccelerationMetersPerSecondSquared`, `StandingStartTimeTo70KphSeconds`, `StandingStartSpeedAtTwoSecondsMetersPerSecond`, `StandingStartPreparationDistanceMeters`. No duplicate exit speed or FirstTurnEntrySpeed is added; use the next sample's entry speed. Generic lap summaries still close at the supplied track's last segment. CSV keeps invariant culture, decimal dots, commas, `\n` and empty nullable values.
- Explicit physical starting fields are now separate from the five racing references; see [Physical starting gates](#physical-starting-gates). Ungated fixtures retain compatibility `Lane/LateralPosition` inputs. No hardcoded gate A bonus is introduced.
- Standing-start reaction/launch constants and first-turn preparation remain provisional and unchanged. No clutch, RPM, wheelspin, traction cap, false starts, reaction RNG, morale modifier or gate bonus is introduced. Signed forces and ceiling removal arrive in #31; real-world calibration is still future work.

## Calibration telemetry

- #28 adds observation only; it changes no physics formula or constant and applies no real-world calibration target.
- `HeatSimulator.SimulateHeat` invokes an optional `ISimulationStepObserver` exactly once per resolved simulation step, after `Resolve` and before `Commit`. Observer exceptions propagate. The hook does not add RNG or alter decisions, commits, surface wear or classification.
- Every `ResolvedSimulationStep` exposes RiderId-ordered typed diagnostics paired one-to-one with its rider changes. Distance and time are the actual resolved traversal. Straight peak and phase values are taken from the same production `StraightSpeedProfile`; advanced turns expose their actual `ContinuousCornerTraversalProfile`. Historical TurnEntry/TurnExit fields remain schema compatibility fields and are null for the advanced #38 path. No profile is recomputed for telemetry.
- `CalibrationTraceCollector` creates one typed sample per resolved rider per step from the immutable entry snapshot, exact entry surface, resolved state change and diagnostics. It never parses `SimLog`, and disabling human-readable logging does not disable telemetry.
- Rider maximum speed is the maximum sample peak, not merely an exit speed. Lap summaries use the actual last segment of the supplied topology and only positions that crossed a lap boundary; lap counts and segment counts are not hardcoded. Rider summaries reconcile status, laps, time and distance with final classification.
- A completed trace owns detached, read-only metadata, samples, summaries and classification. Step ordering is `StepNumber, RiderId`; lap ordering is `RiderId, LapNumber`; rider ordering is `RiderId`. CSV uses commas, invariant-culture decimal points, `\n`, empty nullable fields and deterministic escaping, with no CoreSim file I/O.
- `CalibrationRunner` invokes only the production `HeatSimulator` loop. Supplied `RiderState` and `TrackState` objects are mutated normally; no hidden world copy or second laps-by-segments simulation exists.
- Compatibility bootstrap is not a physical standing start. #30's marked launch remains provisional. #36 calibrates the existing longitudinal Straight/TurnExit acceleration ranges and shared fade endpoints without changing launch constants. Residual system-level speed and average-speed gaps may belong to later corner-envelope calibration. Later refinements can cover F_engine vs F_traction, slip/wheelspin, TractionBias, real sprockets, RPM, torque/power, throttle, engine braking and within-gate positioning/footprint; not implemented now.

- **Breaking diagnostic-schema change #31:** `FullDriveEquilibriumSpeedMetersPerSecond` replaces `AttainableTopSpeedMetersPerSecond` in RiderStepDiagnostics, CalibrationStepSample and CSV. StandingStart, Straight and advanced continuous-corner drive use the actual profile value; compatibility fields remain in stable order. CSV inserts #38 continuous-corner fields after the prior schema. No duplicate Vmax field remains. Invariant culture, decimal dots, `\n` and null-as-empty are retained.

## Physical starting gates

- `StartingGate.A/B/C/D` is explicit input, inside to outside, not a synonym for `Lane 0/1/2/3`. `StartingGateGeometry` divides the **full physical straight width W** into four equal nominal fields `W/4`, with no deduction for painted lines. Shared boundaries belong to the outer field; only D includes the outer physical edge.
- `StartingGrid.Create(track, assignments)` requires four unique riders and four distinct explicit fields. Assignment follows neither rider id nor input list order. Each rider begins `NotStarted`, speed/time/distance/progress zero, at the same `Track.StartFinishLine`, at the neutral physical center of its own field. The canonical lap boundary before segment zero already is both start and finish; there is no separate finish line.
- `RiderState(profile, gate, track)` and `ResetForHeat(gate, track)` support explicit neutral placement. The immutable `StartingPosition` bounds/center and gate identity are retained in snapshots and legacy decision adapters as initial metadata, not a movement constraint. Ungated constructors/reset preserve old synthetic/legacy paths and clear gate metadata. Explicit placement rejects unmarked tracks or a center outside the existing racing-reference span rather than silently clamping it.
- Transform the physical center by subtracting the inner reference offset, then call the existing `LaneModel.LateralPositionFromPhysicalOffsetMeters`. Discrete `Lane` is the nearest racing reference; it does not determine the field. Motoarena W=12 m yields four 3 m fields and centers 1.5/4.5/7.5/10.5 m; the existing 1 m inner/outer reference margins derive racing positions approximately 0.2/1.4/2.6/3.8.
- Starting-area markings expose three internal edge offsets, 0.05 m dividing-line width and 1 m backward length as geometry metadata. PZM marshal placement between B/C is documentation only, not an entity/collider. True within-gate rider positioning and motorcycle footprint are future refinements; this PR establishes physical gate bounds and a neutral center position.
- Production launch continuously samples the existing five surface bands at entry `LateralPosition` (the physical gate center). No grip, reaction, acceleration or ordering modifier depends on gate identity. Reaction 0.28→0.20 s, launch 9–11 m/s², gearing, resistance, preparation, TimeTo70 and SpeedAt2s algorithms are unchanged. Once moving, normal TargetLane/PlannedLane/LateralPosition execution applies immediately, with no gate lock.
- `RaceProgressTracker.StartingGridOrder` uses explicit A→D as presentation identity only. All riders are longitudinally tied at the tape; A is not a leader. Running order remains progress/time/id. Breaking a tied progress/time pair of explicitly gated riders is not logged as an overtake; later genuine reversals are. Ungated compatibility logging is unchanged.
- Current Motoarena geometry is **35/27 FIM-constrained baseline; exact Motoarena start-line offset not publicly verified**. `MatchedVenueProfiles.CreateMotoarenaStandingStartTrack()` preserves 62 m home/back straights, the existing radius/width conventions and fixed-line flying-lap geometry. The old 31/31 input and notes remain explicitly historical for #39–#51 evidence, not a current default. See [source audit and current geometry](calibration/physical-starting-gates.md). No telemetry fitting or common-time swept-space interaction is implemented here.

## Lateral movement

- Advanced moving paths execute `MoveTowards` inside each bounded coupled step, using only that step’s movement time. Standing reaction is stationary. Canonical progress, physical distance and elapsed time are committed from the same immutable path; cumulative rider time never drives movement.
- Execution is `0.5 * SlideControlNorm + 0.5 * AdaptabilityNorm`. The unchanged provisional normalized traversal rate is `0.35 + (0.65 - 0.35) * execution` lane-units/s and grip scaling remains `0.65 + 0.35 * EffectiveGrip`. Maximum normalized delta is rate times segment time and grip scaling; its explicit physical distance is that delta times reference spacing derived from the current segment width. `MoveTowards` compares/meters, applies the bounded physical distance, then uses the canonical inverse conversion. `LaneArrivalToleranceMeters = 0.05 m` compares physical separation.
- Movement is a bounded `MoveTowards` operation aimed at the physics-resolved `Lane`. It is symmetric inward and outward, cannot pass its target, remains finite and within `0..4`, and does not move for a zero-duration segment.
- In advanced physics, `PlannedLane` is the nearest not-yet-executed reference lane from `LateralPosition` toward `TargetLane`. Reaching that reference uses `LaneArrivalToleranceMeters = 0.05 m`; only then may planning advance to the next reference. A discrete `Lane` placed farther away by an earlier `RunWide` cannot skip an unreached reference, and a decision reversing direction begins immediately from the actual lateral position.
- A `RunWide` therefore aims continuous movement at its forced resolved lane while retaining the existing outcome, speed thresholds and overspeed-retention rules. Legacy physics remains compatible and may set `LateralPosition` directly to the resolved lane.
- `LaneChangeTendency` remains a style preference used by decisions and route cost. It does not change physical lateral speed; neither does morale.
- Moving local capability uses actual radius and surface. `ContinuousCornerEnvelope` remains the exact fixed-line path and supplies a pointwise local continuation forecast for moving nodes; it does not predict future trajectory intent. Width transitions remain segment-local with no teleport distance. See [executed path method](calibration/executed-trajectory-method.md).

## Canonical resolved motion (#53) — BINDING

Every active resolved rider has one immutable `ResolvedSimulationStep.Motions` entry,
including fixed lines, moving lines, launch and partial Crash. `SampleAtTime` reads
stored production endpoints in a single segment frame, starts at snapshot local
t=0, includes stationary launch reaction and reconciles its final canonical
progress/lateral/speed/distance/time with the actual resolved state and diagnostics.
Fixed-line endpoints observe the original integration without recomputing physics.
Coordinate reinterpretations at width boundaries and existing coarse state events
are explicit metadata; consumers must never sweep/interpolate across them as physical
movement. Common heat time must include each motion's `StartElapsedTimeSeconds`.
No interaction resolver or dimensions are added. See the full
[audit, sampling/event/boundary contract and evidence](calibration/resolved-rider-motion.md).

## Riders and decisions

- Skills use a `0..100` scale: start, speed, slide control, track reading, pair riding and adaptability.
- Style uses normalized preferences: risk, lane changes, outside line and setup independence.
- Morale is mutable and separate from physical form. It changes stability and follows results or incidents.
- A decision model evaluates local lanes from their exact discrete stored surface cells. Track reading controls observation quality; style controls preferences; occupied space is penalized. `AdaptiveDecisionModel` converts continuous lateral separation through the current segment's physical width; its current `0.55 m` occupancy threshold is provisional and preserves compatibility behavior, not a final rider/motorcycle dimension.
- The movement distance evaluated by `AdaptiveDecisionModel` starts at continuous `LateralPosition`, while style still changes the preference cost of choosing a lane.
- Lane evaluation uses projected route time (bend plus following straight), not raw maximum speed. This lets a clean outside route beat a worn inside route without making the outside universally superior.
- Contact candidates are selected from post-`ResolveRider` `LateralPosition` using physical separation derived from the current segment width and a separate unchanged provisional `0.55 m` threshold. Riders are ordered by elapsed time and rider id; each trailing rider uses the nearest earlier rider within that lateral threshold, and the stable candidate list is fixed before contact effects. Longitudinal eligibility remains `0.12 s`. Contact probability and effects are not a complete collision model; surface and random discriminators still use the trailing rider's discrete `Lane`. `ContactLostRhythm` retains its transitional one-reference outward `Lane` step on non-straight segments, while its continuous outward displacement is the unchanged provisional physical distance `0.50 m`, converted through local width and capped at that forced reference and the `0..4` domain. A straight or rider already on position 4 receives no outward contact displacement.
- The surface grid remains five normalized bands. Continuous sampling still interpolates `LateralPosition 0..4`, and the adjacent-band wear kernel is unchanged; adjacency is not asserted to equal a fixed number of metres.
- Width changes are segment-local: the same normalized coordinate denotes the same fraction of usable width on straight and turn. No synthetic lateral event, gradual width-transition spline or extra distance/time is added at the boundary. Within each segment, moving paths include their actual cartesian/polar distance and local radius history.

## Setup

- Setup is a trade-off and cannot represent a more expensive or universally faster motorcycle.
- A manager supplies advice with a confidence level.
- Trust, confidence and rider independence determine whether the advice is accepted, adjusted or ignored.

## Result and player information

The core returns factual internal results and logs. Events created in one step are ordered by step, phase, rider id and event type. Segment logs distinguish entry speed, speed after the physical constraint and final exit speed, and record continuous movement as `lateral=before->after` using invariant formatting. A change of running order between two active riders creates a typed overtake event; a retirement is not an overtake. Every completed lap creates a typed order snapshot with gaps to the active leader.

A physical starting-gate balance report must rotate the same four rider profiles evenly through explicit A/B/C/D. The historical `BalanceAnalyzer` still rotates compatibility racing references, not physical fields; its output is not final gate-advantage evidence. This prevents rider strength from being mistaken for gate advantage. Large diagnostic batches may disable log capture, but this must not change track evolution or the simulated classification.

A gameplay layer is responsible for hiding exact values and exposing observations, feedback and uncertainty to the player.

## Calibration evidence boundary (#32)

The versioned PGEE snapshot and evaluator measure the current production model without modifying it. Real Vmax, average speed, first-lap penalty, and within-heat spreads are distribution envelopes rather than caps or equality targets. Absolute heat/lap time and distance remain context until a concrete simulated track matches real geometry. Source `speed_2s` and `curve_speed` are gate rankings, not individual physical speeds; reaction has separate literature context supplied in the task specification.

RiderSkills express capability; age/category does not apply a speed, start, or corner multiplier. Skill 50 is not assigned the meaning “average PGEE rider,” and #32 adds neither new execution RNG nor real-rider skill labels. The calibration observer and added skill metadata cannot affect results. #33 changes physical-width geometry and records observations only; empirical performance calibration remains future work.

## Continuous corner-speed correction (#34, historical foundation)

- Advanced corner resolution keeps recoverable input speed and emits a nullable
  target: none at/below `MaxSafeTurnSpeed`, `max` in the quiet `1.015` and Brake
  bands, and `max + (input - max) × retention` for RunWide. Thresholds remain
  `1.06–1.14` for Brake, `1.18–1.34` for RunWide and `0.35–0.65` retention.
  The RunWide target uses the constraint's entry radius and is not recomputed on
  the forced outer lane. Outer-lane RunWide and above-threshold speed still crash.
- `CornerSpeedCorrectionProfile` uses the unchanged effective corner correction
  capability `2.00–3.20 m/s² × (0.75 + 0.25 × EffectiveGrip)`. For entry above
  target, required distance is `(entry²-target²)/(2a)`. Sufficient distance ends
  exactly at target and returns the remainder; insufficient distance is consumed
  completely and leaves exit speed above target. Phase time is
  `2d/(entry+exit)`. Zero distance preserves speed and takes zero time.
- TurnEntry retains the `0.50` first-half scrub. Only post-scrub metres can be
  used by residual correction, followed by constant-speed carry. TurnMiddle uses
  correction then carry. TurnExit applies correction first and may run its
  existing drive profile only on correction remainder; it never reuses the full
  segment. RunWide receives carry but no drive. Crash retains half-remaining
  progress and existing time/status semantics.
- `RiderStateChange.PhysicsSpeed` is actual speed after the discrete
  constraint/random-incident stage and before continuous correction. `Speed` is
  final speed after all physical phases. `RiderStepDiagnostics.TravelTimeSeconds`
  is always final elapsed minus entry elapsed. Typed correction diagnostics may
  coexist with TurnEntry scrub and TurnExit drive profiles.
- Random incident probability, addressed RNG channels, severity, immediate
  `0.88` loss and crash behavior remain unchanged. An incident-created
  Crash/RunWide clears the earlier constraint target, preventing a double loss.
  Correction itself is deterministic and uses no RNG. Legacy physics remains
  byte/float-compatible and instantaneous. No calibration or constant tuning was
  performed.

## Continuous corner phase foundation (#37)

- Track.CornerTopology owns the canonical immutable logical-corner map. A
  logical corner is one maximal contiguous run of TurnEntry, TurnMiddle and
  TurnExit segments in topology order. Runs are never joined across the lap
  boundary, even when the last and first segments are both turns.
- CornerPhaseContext projects local segment progress to CornerProgress in
  [0,1] from accumulated physical arc distance divided by total logical-corner
  length at the rider's immutable entry LateralPosition. It exposes corner
  id, membership, current subsegment start/end progress, total corner length
  and remaining corner length. It does not assume equal thirds.
- The domains remain separate: LateralPosition is continuous cross-track
  position, CornerProgress is longitudinal logical-corner phase,
  TurnEntry/TurnMiddle/TurnExit are compatibility/reporting labels, and
  Lane is a discrete reference used by decisions and constraints.
- Advanced SimulationEngine.Resolve computes the context from the immutable
  snapshot and passes it through SegmentPhysicsContext and typed diagnostics.
  Legacy resolution receives no context and keeps identical behavior. This adds
  no state mutation, random draw, surface resampling or second physics path.
- Straight lookahead resolves an immediately adjacent logical-corner start,
  then retains the existing TurnEntry compatibility requirement and exact
  approach-speed calculation. Existing TurnEntry scrub and TurnExit drive use
  the same context but remain compatibility bridges for #38. Their order,
  distances, times, thresholds, outcomes and numerical results are unchanged.
- #37 performs no calibration. All #36 longitudinal constants, standing-start
  constants, corner constants, contact/lateral/surface behavior, production
  CaptureSnapshot -> Decide -> Resolve -> Commit flow and historical reports
  remain unchanged.

## Continuous corner envelope calibration (#38)

- Advanced production uses one `ContinuousCornerEnvelope` over the complete
  logical corner. `TurnEntry`, `TurnMiddle` and `TurnExit` remain topology,
  surface-indexing and reporting labels; they do not select longitudinal phases.
- The settled/apex capability is the existing radius/surface/skill/setup
  calculation with an advanced reference turn speed of `19 m/s`. The legacy and
  context-free compatibility reference remains `16 m/s`.
- `ApexProgress = 0.5` is a provisional geometry assumption. Before it,
  `distanceToApex = (0.5 - CornerProgress) * TotalCornerLengthMeters` and
  `v_envelope = sqrt(v_apex² + 2 * correctionCapability * distanceToApex)`.
  At the apex the envelope is exactly `v_apex`.
- After the apex, the envelope starts at `v_apex` and is integrated over physical
  distance with the existing signed turn-drive force and midpoint step. Net
  drive availability is `0` through the apex, `smoothstep((p-0.5)/(5/6-0.5))`
  until `5/6`, and `1` afterwards. These values are provisional geometry and
  behavior-derived assumptions, not telemetry-derived constants.
- Every traversal step is at most the existing `1 m`, preserves the exact final
  remainder and belongs to exactly one of correction, carry or drive. Correction
  uses the unchanged `2.00–3.20 m/s²` capability. It reaches the target exactly
  when possible and otherwise preserves residual overspeed. There is no speed
  teleport, artificial Vmax or hard cap.
- Advanced `SegmentPhysics` classifies `Ok`, `Brake`, `RunWide` and `Crash`
  relative to the local continuous envelope. The quiet `1.015`, Brake
  `1.06–1.14`, RunWide `1.18–1.34` and retention `0.35–0.65` factors are
  unchanged. Random incidents remain separate and still clear stale correction
  targets for incident-created Crash/RunWide.
- Straight and standing-start preparation both query the canonical next-corner
  envelope at progress zero. Advanced production does not invoke the historical
  TurnEntry scrub helper or the segment-gated TurnExit drive helper.
- The complete typed traversal profile reports entry/exit/peak/minimum speeds and
  progress, timing, correction/carry/drive distance, envelope endpoints, apex,
  residual and equilibrium diagnostics. Peak selection takes the maximum across
  standing start, Straight, corner entry, correction, drive and contact sources.
- All #36 longitudinal constants and standing-start constants remain frozen, as
  do correction/outcome factors, contact, lateral, surface and RNG behavior.
  The dataset and historical calibration reports are unchanged.

## Production-backed trajectory decisions

- `TrajectoryIntent` requests Entry/Apex/Exit anchors in `0..4`. Entry applies
  during remaining pre-corner straight preparation and TurnEntry, middle/apex
  intent during TurnMiddle, exit during TurnExit and the entire following logical
  straight. These are requested manager-scale targets, never guaranteed waypoints.
- Receding-horizon decisions evaluate at most 35 full, 13 middle/exit, or 5 exit
  candidates. Entry is any anchor; subsequent targets are deduplicated adjacent
  anchors. Only the current phase target executes; RiderState stores no persistent plan.
- Physical time comes only from isolated solo production `SimulationEngine`
  replay, starting at the exact immutable rider snapshot and ending before the
  second logical corner or at race finish. Split straights cross lap wrap through
  topology; there are no segment-id exceptions. Full normal resolution and Lean
  projection call the same rider physics core and return bit-identical canonical
  totals, achieved endpoints, speeds, outcomes and own-wear contributions.
- Projection owns detached branch surface copies and compact rider state, no logs and zero random
  incident frequency. It retains deterministic correction, RunWide, missed anchors
  and terminal crash. Its own passage wear commits privately; future weather or
  other riders' wear is not forecast. A terminal crash cannot win as a short route.
- Raw perceived surface cells use unchanged observation-noise amplitude/mapping,
  addressed by race seed, heat, decision step, rider, segment index, lane and model
  seed. Segment index extends the old addressing. Perfect reading removes noise;
  existing continuous raw-cell interpolation remains the physical consumer.
- Total cost decomposes into production time, existing style/risk/occupancy costs
  and behavioral lane-change reluctance. The independent physical movement
  surcharge is removed. Reluctance uses the existing `0.025*(1-LaneChangeTendency)`
  coefficient over remaining requested phase changes. No extra distance or speed
  bonus is applied. Tie-breaking is total cost, physical time, requested change,
  then canonical Entry/Apex/Exit tuple.
- A typed target-history prefix graph executes each unique physical prefix once
  and commits own wear to its private branch. Cold/reversed parity remains exact.
  Rich motion/path/event materialization is explicit; normal Decide uses Lean capture.
  Full race motion contracts remain unchanged. There is no global mutable cache
  or second physics model.
- Production time solve retains its original tolerance and one-metre maximum;
  nonconvergent correction/drive switching-boundary steps may be halved at most
  16 times using the same primitives, with typed subdivision diagnostics.
- Occupancy remains an external provisional current-target cost. Solo replay
  implements no traffic feasibility, contested space, collision avoidance, footprint
  or tactical interaction. Width transitions remain segment-local coordinate
  reinterpretations; combined grip/slip and provisional physical calibration are unchanged.

See [current controls and exact production parity](calibration/trajectory-intent-evaluation.md).
