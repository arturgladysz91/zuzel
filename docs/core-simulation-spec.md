# Core simulation specification

This document defines the binding invariants of the speedway manager simulation.

## Scope

The core simulates decisions and believable consequences. It does not integrate a complete motorcycle physics model. Rendering, economy, league rules and persistence remain outside `CoreSim`.

## Determinism

Given identical domain state, options and random seed, a heat must produce identical classification, typed surface changes and text logs. Reordering riders in the input collection must not change the result associated with any rider.

Every segment is processed as `CaptureSnapshot -> Decide -> Resolve -> Commit`. Decisions use one detached snapshot of all riders and all relevant surface cells. Neither decisions nor resolution may mutate live state. Random samples are addressed by seed, heat, step, rider id and a named channel; shared generator sequence, collection index, `string.GetHashCode()` and `HashCode` are forbidden for race outcomes.

## Track

- A track is an ordered list of `TurnEntry`, `TurnMiddle`, `TurnExit` and `Straight` segments.
- Every concrete track owns immutable `TrackGeometry`: straight length, inner reference turn radius, spacing between its five reference lanes and the angle in radians covered by one turn segment. Geometry is separate from mutable surface state.
- Every segment has five local reference lanes numbered `0..4` from inside to outside.
- `TargetLane` is the decision's requested destination. `PlannedLane` is the nearest discrete reference lane executed in the current step, `Lane` is the discrete lane resolved by physics for that step, and `LateralPosition` is the rider's actual continuous position at the end of the step.
- `LateralPosition` uses lane units in the inclusive range `0..4`. Its physical displacement is `abs(deltaLateralPosition) * TrackGeometry.LaneSpacingMeters`; invalid, NaN and infinite values are rejected at the domain boundary.
- `RiderPosition.TotalSegmentProgress` is the single topological source of truth. Lap, segment index and normalized `0..1` segment progress are derived values. Physical distance is updated together with canonical progress.
- Rider status distinguishes not started, racing, finished, crashed and retired states.
- Every segment/lane cell stores base grip, ruts and moisture. `EffectiveGrip` derives usable grip from all three values.
- Weather changes moisture unevenly. Rider passages wear the used lane and nearby material. Manager work can grade, water or pack a selected area.

## Physical constraints

- In advanced physics, the base curvature constraint for a turn uses the rider's actual entry position from the immutable snapshot: `R = InnerRadiusMeters + LateralPosition * LaneSpacingMeters`, followed by `v_geometry = 16 m/s * sqrt(R / 24 m)`. Integer `LateralPosition` values `0..4` exactly match the existing reference-lane radii and safe speeds. The 24 m radius and 16 m/s speed normalize the example track; a larger radius raises the limit by the square root of the radius ratio rather than by interpolating ready-made speeds.
- `TurnSegmentAngleRadians` changes path distance and time spent in a turn segment, but it does not change the radius-derived speed boundary. Surface, relevant control skill and setup fit modify that boundary. Morale does not; it may affect a later decision, accepted risk and quality of execution.
- This curvature-based boundary is a simulation constraint, not a complete motorcycle dynamics or cornering model.
- Deliberately choosing a wide line is a planned trajectory and can be advantageous for surface, passing or corner-exit reasons. It changes `PlannedLane`; when the physical constraint is satisfied, `Outcome` may remain `Ok` and the final `Lane` equals `PlannedLane`. `RunWide` is an unplanned or forced outward consequence of excessive speed, an error or contact; the two concepts must not be conflated.
- A speed-triggered `RunWide` finishes one lane wider than `PlannedLane`, lengthening the route. It corrects only the overspeed above the rider's `MaxSafeTurnSpeed`: `correctedSpeed = maxSafeSpeed + (entrySpeed - maxSafeSpeed) * overspeedRetention`. Consequently, its physical speed remains at least the safe boundary but is strictly lower than entry speed.
- Advanced physics interpolates overspeed retention from `0.35` to `0.65` using normalized `SlideControl`; legacy physics without rider data uses the neutral value `0.50`. Better control reduces the loss but never removes the consequence. Random incidents retain their existing separate resolution.
- Line advantage emerges from the geometry of the concrete track, surface and complete trajectory. A projected comparison of extreme-line lap times is a diagnostic for that track, not a global equality test or a fixed percentage invariant.
- A concrete track may favor a particular line. No line may be universally best across all track geometries and surfaces.
- Exceeding the safe speed must cause braking, running wide or a crash.
- A rider cannot run wider than lane 4; an unresolved high-speed run-wide there becomes a crash.
- A straight preserves speed. It cannot create a passing advantage by itself; it only carries an advantage created at corner exit and positions riders for the next turn.

## Lateral movement

- Advanced physics executes continuous lateral movement from the exact time added for the current segment: `segmentTravelTimeSeconds = travelledMeters / averageSpeedMetersPerSecond`. A rider's cumulative elapsed time is never used as a movement duration.
- Physical execution is the equally weighted normalized `SlideControl` and `Adaptability`. Provisional lateral speed ranges from `0.35 m/s` to `0.65 m/s`; provisional grip scaling is `0.65 + 0.35 * EffectiveGrip`. The maximum change in lane units is physical lateral speed multiplied by segment time and grip scaling, divided by `LaneSpacingMeters`.
- Movement is a bounded `MoveTowards` operation aimed at the physics-resolved `Lane`. It is symmetric inward and outward, cannot pass its target, remains finite and within `0..4`, and does not move for a zero-duration segment.
- In advanced physics, `PlannedLane` is the nearest not-yet-executed reference lane from `LateralPosition` toward `TargetLane`. Reaching that reference uses `LaneArrivalToleranceMeters = 0.05 m`; only then may planning advance to the next reference. A discrete `Lane` placed farther away by an earlier `RunWide` cannot skip an unreached reference, and a decision reversing direction begins immediately from the actual lateral position.
- A `RunWide` therefore aims continuous movement at its forced resolved lane while retaining the existing outcome, speed thresholds and overspeed-retention rules. Legacy physics remains compatible and may set `LateralPosition` directly to the resolved lane.
- `LaneChangeTendency` remains a style preference used by decisions and route cost. It does not change physical lateral speed; neither does morale.
- Transitional limitation: only the advanced curvature limit uses entry `LateralPosition`. Surface still comes from discrete `PlannedLane`, travelled distance from discrete resolved `Lane`, surface wear remains discrete, and `RunWide` still resolves as the existing discrete `Lane + 1` operation including the lane-4 rule. The radius is fixed from segment-entry position for the current constraint; it is not varied during the segment or coupled iteratively to `MoveTowards` and travel time.

## Riders and decisions

- Skills use a `0..100` scale: start, speed, slide control, track reading, pair riding and adaptability.
- Style uses normalized preferences: risk, lane changes, outside line and setup independence.
- Morale is mutable and separate from physical form. It changes stability and follows results or incidents.
- A decision model evaluates local lanes. Track reading controls observation quality; style controls preferences; occupied space is penalized. `AdaptiveDecisionModel` converts continuous lateral separation to meters with the concrete track's `LaneSpacingMeters`; its current `0.55 m` occupancy threshold is provisional and preserves default-track compatibility.
- The movement distance evaluated by `AdaptiveDecisionModel` starts at continuous `LateralPosition`, while style still changes the preference cost of choosing a lane.
- Lane evaluation uses projected route time (bend plus following straight), not raw maximum speed. This lets a clean outside route beat a worn inside route without making the outside universally superior.
- Contact candidates are selected from post-`ResolveRider` `LateralPosition` using the concrete track's `LaneSpacingMeters` and a separate provisional `0.55 m` threshold. Riders are ordered by elapsed time and rider id; each trailing rider uses the nearest earlier rider within that lateral threshold, and the stable candidate list is fixed before contact effects. Longitudinal eligibility remains `0.12 s`. Contact probability and effects are not a complete collision model; surface and random discriminators still use the trailing rider's discrete `Lane`. `ContactLostRhythm` retains its transitional one-reference outward `Lane` step on non-straight segments, while its continuous outward displacement is the provisional physical distance `0.50 m` converted through `LaneSpacingMeters` and capped at that forced reference lane and the `0..4` domain. A straight or a rider already on lane 4 receives no outward contact displacement. This value is not a final model of motorcycle response to contact.

## Setup

- Setup is a trade-off and cannot represent a more expensive or universally faster motorcycle.
- A manager supplies advice with a confidence level.
- Trust, confidence and rider independence determine whether the advice is accepted, adjusted or ignored.

## Result and player information

The core returns factual internal results and logs. Events created in one step are ordered by step, phase, rider id and event type. Segment logs distinguish entry speed, speed after the physical constraint and final exit speed, and record continuous movement as `lateral=before->after` using invariant formatting. A change of running order between two active riders creates a typed overtake event; a retirement is not an overtake. Every completed lap creates a typed order snapshot with gaps to the active leader.

A starting-gate balance report must rotate the same four rider profiles evenly through gates 1..4. This prevents rider strength from being mistaken for gate advantage. Large diagnostic batches may disable log capture, but this must not change track evolution or the simulated classification.

A gameplay layer is responsible for hiding exact values and exposing observations, feedback and uncertainty to the player.
