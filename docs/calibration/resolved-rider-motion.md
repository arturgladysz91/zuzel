# Canonical resolved rider motion — audit of PR #53

Audited before code changes at `daf88c1b5d95eed49c4b65f3bb4290c6312cfc1c`, base `83a67616973e5d4fbbc8060525ec5ffc629da734`.

`ResolveRider` calls `ExecutedPathTraversal` only in advanced mode when `!IsFixedLine(entryLateral, resolvedLane)`. Fixed straight/launch results have aggregate profiles; fixed corners have immutable timed corner nodes. Their `ExecutedPath` is null. Moving straights/corners, forced RunWide movement and moving partial crashes have an executed path. Active riders resolving their final segment still have a result; already Finished/Crashed/Retired riders are excluded from Decide/Resolve.

Moving launch starts its first path node at reaction time, with zero distance/progress/speed. There is no node at zero or represented stationary interval. Fixed launch stores reaction in the profile only. #52 physical gate centers are canonical snapshot inputs; motion must retain those normalized positions, not replace them with the nearest integer lane. Reaction remains 0.28→0.20 seconds through Start skill.

Normal traversal consumes the remaining canonical segment progress; Crash consumes half the remainder. Moving Crash uses the existing entry-speed/2 (minimum 1 m/s) event-time abstraction and terminal zero speed; fixed corner Crash uses the same coarse average-speed timing. Physics Crash skips wear. RunWide keeps its resolved outward target, correction rules and no positive corner drive. Existing post-traversal contacts can additionally change final speed/lateral/time; their coarse state consequences must be explicit terminal events, never invented swept movement.

Motoarena usable spans are 10 m on straights and 14.6 m on turns. Offset from the inner reference is therefore 2.5×lateral versus 3.65×lateral. At lateral 2, Straight→TurnEntry changes 5→7.3 m; TurnExit→Straight changes 7.3→5 m. The 1 m inner-edge reference offset is common. This is an instantaneous coordinate reinterpretation, not traversed lateral distance. No transition spline, distance or time exists in production. The motion contract must forbid interpolation across segment coordinate frames and expose both boundary offsets.

Fixed-line physics and original profiles remain canonical. Recording already-computed longitudinal endpoints is allowed; rerunning integration, surface RNG or decisions is not. Moving wear continues to consume the original executed steps; fixed wear continues to use its exact entry kernel.

## Implemented contract — BINDING

`ResolvedSimulationStep.Motions` contains exactly one immutable `ResolvedRiderMotion` for every active rider resolved in the step, ordered by rider id. This includes a rider finishing or crashing in this step; already inactive riders have no new motion. Both fixed and moving riders use `motion.SampleAtTime(float localTimeSeconds)`. No consumer needs to test `ExecutedPath` for null.

`Initial` and `SampleAtTime(0)` are the exact immutable snapshot position/lateral/speed at local time zero. `StartElapsedTimeSeconds` gives the rider's heat-clock origin: common heat time is origin + local time. This engine still advances complete segments with independently accumulated rider clocks; a future resolver must align those origins and intersect their time intervals. It must not equate local times when origins differ.

Launch adds a stationary node at reaction time, retaining the exact #52 gate center. All `0..reaction` samples have zero progress, distance and speed; the first movement interval follows reaction. Motoarena centers from physical inner edge are exactly A/B/C/D = 1.5/4.5/7.5/10.5 m in the controls. The formula and launch physics are unchanged.

Moving nodes project the existing `ExecutedSegmentPath`. Fixed straight and fixed launch nodes observe distance/time/speed endpoints **inside the original integration**, without feedback or rerunning it. Fixed corners adapt their existing timed `ContinuousCornerNodes`. Existing profile objects, physics primitives, outcome rules and wear sources remain canonical and unchanged numerically.

Sample fields are local time, exact double canonical progress, consumed-segment progress, step-travelled physical metres, normalized lateral, offset from inner reference (and computed offset from physical inner edge), speed, local radius and curvature. The sample stays in the consumed segment frame even at its end: `SegmentProgress=1` at a completed segment; `CanonicalProgress` exactly equals the committed `RiderPosition.TotalSegmentProgress`, whose derived next-segment progress is zero. Partial/crash endpoints retain their actual partial progress. Travelled metres start at zero for each motion and are not a common longitudinal coordinate for two riders with different origins; use segment identity and canonical/segment progress.

Exact positive node times return the last stored node at that time. Interior times use bounded linear interpolation of stored endpoints; curvature is the reciprocal of interpolated radius. Duplicate-time state events are right-continuous, except `t=0` deliberately returns the snapshot before entry resolution. Negative times, NaN, infinities and times beyond duration raise `ArgumentOutOfRangeException`. Binary search is bounded by the immutable node count. Sampling runs no decisions, RNG, surface sampling or longitudinal integration, and cannot mutate production.

Diagnostics already define local duration as `finalElapsed-entryElapsed`, with accumulated float-clock rounding. Observation times are projected onto that duration: reaction stays exact; movement times scale to the resolved duration. The final canonical progress, distance, lateral, speed and duration use the actual resolved result exactly. This affects only the read-only representation, not elapsed arithmetic or profiles. It prevents two independent endpoint/time sources of truth. Repeated resolution, reversed mixed fixed/moving and gated collections, incident-OFF seeds and repeated samples are regression-tested.

## Events and segment boundaries

Entry bootstrap/constraint speed changes are explicit `EntryResolution` events at zero, not new acceleration intervals. Crash carries its existing coarse event-time speed over half the remaining canonical advance and reaches terminal zero **at the final event time**. The sampler does not fabricate deceleration over the last metre. Passage wear remains disabled. RunWide projects the actually consumed outward path with all old coefficients and targets.

Existing contacts are unchanged. Their coarse speed/lateral changes are explicit duplicate-time `ExistingContact` events at traversal end. The old LostRhythm time penalty is represented as constant endpoint occupation until the final resolved time; its reported terminal speed remains the existing state value. This is an abstract event delay, not additional longitudinal travel or a reconstructed physical contact trajectory. Future interaction should consume the traversal intervals and replace the old event resolver; it must not sweep across these model events. Tests cover both old contact crash and LostRhythm reconciliation without implementing new contact behavior.

`EntryBoundary`/`ExitBoundary` identify both segment ids, canonical boundary progress, unchanged normalized lateral and old/new physical offsets. `HasPhysicalOffsetDiscontinuity` is exact. Motoarena lateral 2 has Straight→TurnEntry 5→7.3 m and TurnExit→Straight 7.3→5 m from inner reference, or 6→8.3→6 m from physical inner edge. At arbitrary lateral the jump is ±1.15×lateral m. Metadata is emitted even when the offset is unchanged; Finished motion has no outgoing traversal into another segment. No boundary node adds time or distance. Every motion and sampler is segment-local: **interpolation/sweeping between the two boundary frames is prohibited**. A future resolver can detect occupancy within each frame and explicitly handle topology; it must not interpret the coordinate reinterpretation as lateral movement. No width spline or fake connecting trajectory is asserted.

## Common-time diagnostics

The new [deterministic JSON](resolved-rider-motion.json) is a current-production replay; #51 artifacts are unchanged.

The #51 K-crossing control uses its exact 60 m straight, 10/14 m widths, clean uniform surface, 18 m/s identical starts and opposite 0→1 / 1→0 intents. Bisection reads only the two samplers, with no contact radius or spatial eligibility threshold. A float-time bracket `[1.0251153, 1.0251154]` s contains lateral-order reversal. At its upper endpoint both samples are exactly equal: progress 0.32500508, travelled distance 19.525953 m, lateral 0.5, physical offset 1 m, speed 20.051582 m/s. This supplies the time/space evidence for the impossible crossing without resolving contact or yield.

The exact #51 A input retains leader progress 0.25 on a 60 m straight while follower starts at zero; their clock origins are both zero. Sampler-only separation is 15 m at common t=0, 14.940126 m at t=0.01, and 11.960794 m at t=0.5. The old occupancy predicate is unchanged; future interaction has the missing longitudinal information.

## Compatibility, cost and verdict

All five fixed-line lanes 0–4 still exactly match the archived hashes, total distance/time/speed and lap times. The original 21-control executed-trajectory benchmark regenerates byte-identically after canonical LF normalization: inside-hold remains fastest (7.6516 / 6.8793 / 6.7015 s at 19/22/25 m/s). **geometry/motion consistency alone does not produce an opening-line advantage**. Neither reaction nor boundary representation changes these physics results. Historical #39–#52 blobs/provenance remain protected, without retaining a second old engine.

Four-lap Motoarena counts, including origin/reaction/event nodes:

| control | segments | common motion nodes | adjacent node intervals | retained moving path nodes | solver fallbacks |
| --- | ---: | ---: | ---: | ---: | ---: |
| fixed lane 2 | 36 | 1525 | 1489 | 0 | 0 |
| inner entry/middle, outer elsewhere | 36 | 1626 | 1590 | 1553 | 0 |

Four similar riders require approximately 6100 fixed or 6504 moving common-motion nodes (5956/6360 intervals). Moving paths also retain their original 1553 nodes per rider; the adapter deliberately favors clarity over eliminating duplication. Fixed representation adds approximately metre-spaced observations on straight/launch plus adapters for the already stored corner nodes. There is no new solver for fixed lines. No shared mutable cache is introduced; the pre-existing envelope cache is instance-local.

A desktop Release measurement (10 warm-ups, 100 four-lap solo runs per control, .NET 8 runtime; full tests running concurrently) compared audited HEAD with current code. Fixed median increased 2.324→3.414 ms, mean 2.439→3.790 ms, allocations 761339→976126 bytes/run. Moving median was 11.381→11.753 ms, mean 11.969→12.039 ms, allocations 3881663→4086535 bytes/run. Fixed representation therefore has a material relative allocation/time cost (~28% allocations, ~47% median) but ~1.1 ms absolute median overhead in this fixture. Moving overhead is modest (~5.3% allocations, ~3.3% median). These are approximate observations, not CI timing gates or a production throughput guarantee. No tuning or micro-optimization followed.

**A — Canonical time-parametrized rider motion is ready as input for the next common-time contested-space PR, within the explicit segment-local geometry and model-event semantics above.** It does not establish real width-transition geometry or contact trajectories. Draft status and independent review remain required.

Regenerate only this new evidence with `dotnet run --project src/Sandbox -c Release -- resolved-rider-motion-report docs/calibration`. Existing current #53 benchmark may be regenerated to a scratch directory for byte comparison; historical reports must not be regenerated.
