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
4. `HeatSimulator.SimulateHeat` advances weather and delegates each segment to `SegmentRaceEngine`.
5. The segment engine captures one immutable rider/surface snapshot. All decisions and movement proposals use clones of that same snapshot and are evaluated in stable rider-id order.
6. Attack and defence intents are resolved from projected route time, real entry/exit speed, available lanes and physical clearance. Failed attacks lose speed/time; aggressive close fights may produce contact.
7. New rider states are committed simultaneously. Only after commit are lane wear and adjacent material movement applied, so no rider sees another rider's same-segment mutations.
8. `RaceProgressTracker` compares the running order after every segment, records real passes between active riders, saves segment order/gaps and retains end-of-lap snapshots.
9. The result contains classification, points and deterministic text plus typed race events. Morale is updated after the heat.

Random race incidents use stateless samples addressed by seed, heat, lap, segment, rider and event channel. Reordering an input collection therefore cannot reassign a draw to another rider.

`BalanceAnalyzer` is an offline diagnostic. It rotates the same four profiles through all four starting gates over a large deterministic batch. Detailed logging is disabled for these batches, while track evolution and race rules remain active.

The presentation layer must consume results and logs; it must never change the simulation outcome.

## Compatibility

`HeatSimulator.Simulate` and `SegmentPhysics.Apply(segment, lane, speed)` remain as a small neutral-surface contract for existing experiments. New gameplay code should use `SimulateHeat` and `SegmentPhysicsContext`.

## Known v2 limits

The engine advances decision segments rather than continuous animation ticks. `SegmentProgressMeters` and physical following gaps make occupancy explicit at the commit boundary, but visual interpolation remains a presentation concern. Defence currently models one directly preceding rival per attacker. Track grip is already supplied per segment/lane, while watering, grading, weather and long-term wear remain separate systems and are not expanded by Race Engine v2.
