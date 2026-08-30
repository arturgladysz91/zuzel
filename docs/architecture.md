# Architecture

## Project boundaries

- `CoreSim` contains deterministic simulation rules and domain state. It has no UI, file system or console dependencies.
- `Sandbox` is a console host for manual runs and balance experiments.
- `CoreSim.Tests` protects physical constraints, determinism and manager-facing decisions.
- `docs` is the source of truth for terminology and simulation invariants.

## Core flow

1. The host creates a `Track` from ordered segments and immutable `TrackGeometry`, plus a separate per-segment/per-lane `TrackState` and rider states.
2. Optional manager actions change the surface through `TrackEvolution.ApplyTrackWork`.
3. Optional setup advice is resolved by `SetupResolver`; trust and rider independence decide whether it is accepted, adjusted or ignored.
4. `HeatSimulator.SimulateHeat` advances weather and delegates one segment at a time to the existing `SimulationEngine`.
5. Every segment has four explicit phases:
   - `CaptureSnapshot` copies every rider and every surface cell into a detached immutable view and preserves the track's immutable geometry.
   - `Decide` gives every active rider the same snapshot; legacy decision models receive a mutable clone, never the source state.
   - `Resolve` samples geometry and physical surface once from the rider's entry `LateralPosition`, passes them through `SegmentPhysicsContext`, calculates current-segment distance, applies eligible distance-limited `TurnExit` drive through `LongitudinalDynamics`, calculates travel time, delegates continuous movement to `LateralMovementModel`, and resolves existing incidents and stable events without changing live riders or the track.
   - `Commit` applies all rider changes, then stable logs and surface wear in rider-id order. Advanced wear is calculated from each rider's immutable segment-entry `LateralPosition`, not from the already committed lane or position.
6. `RiderPosition.TotalSegmentProgress` is the canonical topological position. Lap, segment index and progress in the segment are derived from it; physical distance is advanced atomically with it.
   `LastResolvedSegmentId` (also exposed through the compatibility alias `CurrentSegmentId`) is observational metadata containing the real `TrackSegment.Id` committed for the previous step. It is not used by classification, physics or track occupancy.
7. `RaceProgressTracker` and final classification sort by canonical track progress, with elapsed time and rider id used only as deterministic tie-breakers.
8. The result contains classification, points and a deterministic log. Morale is updated after the heat.

Race randomness is stateless and addressed by seed, heat id, step number, rider id and a named `RandomChannel`. Optional discriminators such as lane or the other rider make separate draws explicit. Collection order and the number of unrelated draws cannot reassign randomness.

`LateralMovementModel` is the single owner of time-based physical movement between reference lanes. It converts segment time and `LaneSpacingMeters` into a bounded change in lane units, validates the continuous `0..4` domain, and selects the nearest unexecuted reference from `LateralPosition` toward `TargetLane`. A discrete lane forced farther away by `RunWide` cannot skip that reference. `SimulationEngine` supplies immutable snapshot inputs and commits the returned position later; `AdaptiveDecisionModel` measures route-change distance from `LateralPosition` but may still price that choice using style.

`LateralSpaceModel` is the shared conversion boundary from continuous lane-unit positions to physical separation in meters. `AdaptiveDecisionModel` uses it with the concrete track geometry for occupancy. Contact candidate selection uses the same conversion on post-`ResolveRider` positions, with a separate provisional `0.55 m` threshold; candidates are fixed before any contact effects. Contact probability remains unchanged, and contact surface plus random discriminators still use the trailing rider's discrete `Lane`. For `ContactLostRhythm`, `LateralMovementModel` owns conversion of the provisional `0.50 m` outward displacement into lane units. The existing discrete outward `Lane` step remains the maximum reference for that continuous displacement; straight segments and riders already on lane 4 receive no outward push. This is not a final motorcycle contact-response model.

This is a transitional split: advanced physics uses one segment-entry `LateralPosition` for curvature radius, turn path length, physical surface read and surface wear. `TrackStateSnapshot.SampleSurface` linearly interpolates stored `Grip`, `Ruts` and `Moisture` between adjacent reference lanes, then constructs a new `TrackSurfaceState` so nonlinear `EffectiveGrip` is recomputed rather than interpolated. Advanced wear does not interpolate surface state: it blends the existing discrete kernels centered on `floor(LateralPosition)` and `ceil(LateralPosition)`. Each kernel applies `100%` wear to its center and `20%` to each existing adjacent cell; contributions to the same cell are aggregated before one deterministic write in ascending lane order. Integer positions therefore preserve the former advanced kernel exactly, while fractional positions change wear smoothly.

The entry sample is not updated after `MoveTowards`, contact or `RunWide`, so a forced outer lane does not move the whole segment's wear outward. Straights retain their fixed `StraightLengthMeters` while using the entry surface for physical lateral execution and the same continuous wear distribution. `TrackState` and `TrackEvolution` remain discrete segment-by-lane storage and mutation systems. `AdaptiveDecisionModel` candidate surfaces, contact surface, run-wide resolution and all legacy geometry, surface-read and wear behavior remain discrete.

`SegmentPhysics` remains the constraint and outcome resolver. For advanced `TurnExit` only, `LongitudinalDynamics` follows it with a first deterministic positive-drive step after actual travelled distance is known: `v_out² = v_physics² + 2 * acceleration * travelledMeters`. Acceleration combines the rider's `Speed` ability, the corner-drive side of `BikeSetup.Gearing`, and the already sampled entry `EffectiveGrip`. It is applied only after `Ok` or `Brake`; `RunWide`, `Crash`, other turn segments, straights and all legacy physics bypass it. The current acceleration and multiplier constants are provisional. Straight acceleration, distance-limited braking, top speed, drag and finish-aware optimization remain future longitudinal work.

`BalanceAnalyzer` is an offline diagnostic. It rotates the same four profiles through all four starting gates over a large deterministic batch. Detailed logging is disabled for these batches, while track evolution and race rules remain active.

The presentation layer must consume results and logs; it must never change the simulation outcome.

## Compatibility

Compatibility is intentionally limited to `HeatSimulator.Simulate`, `SegmentPhysics.Apply(segment, lane, speed)` and the older `IRiderDecisionModel.Decide(TrackSegment, RiderState)` overload. The legacy heat pass also uses the four-phase commit while retaining its original neutral-surface physics, and legacy decision models receive a detached mutable rider copy.

The original `Track(segments)` constructor and geometry-free `LaneModel` overloads remain available. They use the example-compatible `TrackGeometry.Default`; new simulation paths pass the geometry of the concrete track explicitly.

The canonical corner-speed path derives its base limit from a continuous lateral radius and carries no morale argument. Integer overloads delegate to the same calculation and retain exactly the reference-lane results. Morale remains in `SegmentPhysicsContext` only for the existing incident-risk calculation. The legacy `SegmentPhysics.Apply(segment, lane, speed)` and geometry-free speed overloads retain their signatures and use `TrackGeometry.Default`; the legacy heat path also keeps immediate alignment of `LateralPosition` with the resolved lane.

`RiderDecisionContext.Rider`, `Riders` and `TrackState` now expose immutable snapshot types. Code compiled against their former mutable types is not source-compatible and should migrate to the snapshot API. New gameplay code should use `SimulateHeat` and `SegmentPhysicsContext`.
