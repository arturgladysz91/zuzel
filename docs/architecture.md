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
   - `Resolve` passes the snapshot's `TrackGeometry` through `SegmentPhysicsContext`, calculates the exact current-segment travel time, delegates continuous movement to `LateralMovementModel`, and resolves existing incidents and stable events without changing live riders or the track.
   - `Commit` applies all rider changes, then stable logs and surface wear in rider-id order.
6. `RiderPosition.TotalSegmentProgress` is the canonical topological position. Lap, segment index and progress in the segment are derived from it; physical distance is advanced atomically with it.
   `LastResolvedSegmentId` (also exposed through the compatibility alias `CurrentSegmentId`) is observational metadata containing the real `TrackSegment.Id` committed for the previous step. It is not used by classification, physics or track occupancy.
7. `RaceProgressTracker` and final classification sort by canonical track progress, with elapsed time and rider id used only as deterministic tie-breakers.
8. The result contains classification, points and a deterministic log. Morale is updated after the heat.

Race randomness is stateless and addressed by seed, heat id, step number, rider id and a named `RandomChannel`. Optional discriminators such as lane or the other rider make separate draws explicit. Collection order and the number of unrelated draws cannot reassign randomness.

`LateralMovementModel` is the single owner of time-based physical movement between reference lanes. It converts segment time and `LaneSpacingMeters` into a bounded change in lane units, validates the continuous `0..4` domain, and enforces arrival at the current discrete lane before another step farther in the same direction. `SimulationEngine` supplies immutable snapshot inputs and commits the returned position later; `AdaptiveDecisionModel` measures route-change distance from `LateralPosition` but may still price that choice using style.

This is a transitional split: `Lane` remains the discrete input for segment physics, distance and surface wear, while `LateralPosition` records continuous execution. Interpolation of radius, surface and wear between neighboring lanes belongs to a later stage.

`BalanceAnalyzer` is an offline diagnostic. It rotates the same four profiles through all four starting gates over a large deterministic batch. Detailed logging is disabled for these batches, while track evolution and race rules remain active.

The presentation layer must consume results and logs; it must never change the simulation outcome.

## Compatibility

Compatibility is intentionally limited to `HeatSimulator.Simulate`, `SegmentPhysics.Apply(segment, lane, speed)` and the older `IRiderDecisionModel.Decide(TrackSegment, RiderState)` overload. The legacy heat pass also uses the four-phase commit while retaining its original neutral-surface physics, and legacy decision models receive a detached mutable rider copy.

The original `Track(segments)` constructor and geometry-free `LaneModel` overloads remain available. They use the example-compatible `TrackGeometry.Default`; new simulation paths pass the geometry of the concrete track explicitly.

The canonical corner-speed path derives its base limit from the concrete lane radius and carries no morale argument. Morale remains in `SegmentPhysicsContext` only for the existing incident-risk calculation. The legacy `SegmentPhysics.Apply(segment, lane, speed)` and geometry-free speed overloads retain their signatures and use `TrackGeometry.Default`; the legacy heat path also keeps immediate alignment of `LateralPosition` with the resolved lane.

`RiderDecisionContext.Rider`, `Riders` and `TrackState` now expose immutable snapshot types. Code compiled against their former mutable types is not source-compatible and should migrate to the snapshot API. New gameplay code should use `SimulateHeat` and `SegmentPhysicsContext`.
