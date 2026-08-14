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
4. `HeatSimulator.SimulateHeat` advances weather, asks the decision model for a local target lane, applies lane-change inertia and resolves physical constraints.
5. Rider passages wear the chosen lane and, to a smaller degree, adjacent lanes.
6. Riders occupying the same lane with a very small time gap can make contact. Middle-turn contacts are intentionally more dangerous than straight contacts.
7. `RaceProgressTracker` compares the running order after every segment, records real passes between active riders and saves an order/gap snapshot at the end of every lap.
8. The result contains classification, points and a deterministic log. Morale is updated after the heat.

`BalanceAnalyzer` is an offline diagnostic. It rotates the same four profiles through all four starting gates over a large deterministic batch. Detailed logging is disabled for these batches, while track evolution and race rules remain active.

The presentation layer must consume results and logs; it must never change the simulation outcome.

## Compatibility

`HeatSimulator.Simulate` and `SegmentPhysics.Apply(segment, lane, speed)` remain as a small neutral-surface contract for existing experiments. New gameplay code should use `SimulateHeat` and `SegmentPhysicsContext`.
