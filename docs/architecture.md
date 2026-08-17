# Architecture

## Project boundaries

- `CoreSim` contains deterministic simulation rules and domain state. It has no UI, file system or console dependencies.
- `Sandbox` is a console host for manual runs and balance experiments.
- `CoreSim.Tests` protects physical constraints, determinism and manager-facing decisions.
- `docs` is the source of truth for terminology and simulation invariants.

## Core flow

1. The host creates a `Track`, a per-segment/per-lane `TrackState` and rider states.
2. Optional manager actions change the surface through `TrackEvolution.ApplyTrackWork`.
3. Optional setup advice is resolved by `SetupResolver`; trust and rider independence decide whether it is accepted, adjusted or ignored.
4. `HeatSimulator.SimulateHeat` advances weather and delegates one segment at a time to the existing `SimulationEngine`.
5. Every segment has four explicit phases:
   - `CaptureSnapshot` copies every rider and every surface cell into a detached immutable view.
   - `Decide` gives every active rider the same snapshot; legacy decision models receive a mutable clone, never the source state.
   - `Resolve` calculates movement, existing incidents and stable events without changing live riders or the track.
   - `Commit` applies all rider changes, then stable logs and surface wear in rider-id order.
6. `RiderPosition.TotalSegmentProgress` is the canonical topological position. Lap, segment index and progress in the segment are derived from it; physical distance is advanced atomically with it.
7. `RaceProgressTracker` and final classification sort by canonical track progress, with elapsed time and rider id used only as deterministic tie-breakers.
8. The result contains classification, points and a deterministic log. Morale is updated after the heat.

Race randomness is stateless and addressed by seed, heat id, step number, rider id and a named `RandomChannel`. Optional discriminators such as lane or the other rider make separate draws explicit. Collection order and the number of unrelated draws cannot reassign randomness.

`BalanceAnalyzer` is an offline diagnostic. It rotates the same four profiles through all four starting gates over a large deterministic batch. Detailed logging is disabled for these batches, while track evolution and race rules remain active.

The presentation layer must consume results and logs; it must never change the simulation outcome.

## Compatibility

`HeatSimulator.Simulate` and `SegmentPhysics.Apply(segment, lane, speed)` remain as a small neutral-surface contract for existing experiments. The legacy heat pass also uses the four-phase commit, while retaining its original neutral-surface physics. New gameplay code should use `SimulateHeat` and `SegmentPhysicsContext`.
