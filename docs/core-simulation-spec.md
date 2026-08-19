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
- The chosen lane is discrete; `LateralPosition` is continuous and cannot jump directly across the track.
- `RiderPosition.TotalSegmentProgress` is the single topological source of truth. Lap, segment index and normalized `0..1` segment progress are derived values. Physical distance is updated together with canonical progress.
- Rider status distinguishes not started, racing, finished, crashed and retired states.
- Every segment/lane cell stores base grip, ruts and moisture. `EffectiveGrip` derives usable grip from all three values.
- Weather changes moisture unevenly. Rider passages wear the used lane and nearby material. Manager work can grade, water or pack a selected area.

## Physical constraints

- The base curvature constraint for a turn uses the concrete line radius `R = LaneModel.TurnArcRadiusMeters(lane, geometry)` and `v_geometry = 16 m/s * sqrt(R / 24 m)`. The 24 m radius and 16 m/s speed normalize the example track; a larger radius raises the limit by the square root of the radius ratio rather than by a linear bonus for the lane number.
- `TurnSegmentAngleRadians` changes path distance and time spent in a turn segment, but it does not change the radius-derived speed boundary. Surface, relevant control skill and setup fit modify that boundary. Morale does not; it may affect a later decision, accepted risk and quality of execution.
- This curvature-based boundary is a simulation constraint, not a complete motorcycle dynamics or cornering model.
- Deliberately choosing a wide line is a planned trajectory and can be advantageous for surface, passing or corner-exit reasons. `RunWide` is an unplanned or forced outward consequence of excessive speed, an error or contact; the two concepts must not be conflated.
- Line advantage emerges from the geometry of the concrete track, surface and complete trajectory. A projected comparison of extreme-line lap times is a diagnostic for that track, not a global equality test or a fixed percentage invariant.
- A concrete track may favor a particular line. No line may be universally best across all track geometries and surfaces.
- Exceeding the safe speed must cause braking, running wide or a crash.
- A rider cannot run wider than lane 4; an unresolved high-speed run-wide there becomes a crash.
- A straight preserves speed. It cannot create a passing advantage by itself; it only carries an advantage created at corner exit and positions riders for the next turn.

## Riders and decisions

- Skills use a `0..100` scale: start, speed, slide control, track reading, pair riding and adaptability.
- Style uses normalized preferences: risk, lane changes, outside line and setup independence.
- Morale is mutable and separate from physical form. It changes stability and follows results or incidents.
- A decision model evaluates local lanes. Track reading controls observation quality; style controls preferences; occupied space is penalized.
- Lane evaluation uses projected route time (bend plus following straight), not raw maximum speed. This lets a clean outside route beat a worn inside route without making the outside universally superior.
- Rider-to-rider contact depends on the time gap, segment, surface and control skills. Contact in `TurnMiddle` is more dangerous than on a straight.

## Setup

- Setup is a trade-off and cannot represent a more expensive or universally faster motorcycle.
- A manager supplies advice with a confidence level.
- Trust, confidence and rider independence determine whether the advice is accepted, adjusted or ignored.

## Result and player information

The core returns factual internal results and logs. Events created in one step are ordered by step, phase, rider id and event type. Segment logs distinguish entry speed, speed after the physical constraint and final exit speed. A change of running order between two active riders creates a typed overtake event; a retirement is not an overtake. Every completed lap creates a typed order snapshot with gaps to the active leader.

A starting-gate balance report must rotate the same four rider profiles evenly through gates 1..4. This prevents rider strength from being mistaken for gate advantage. Large diagnostic batches may disable log capture, but this must not change track evolution or the simulated classification.

A gameplay layer is responsible for hiding exact values and exposing observations, feedback and uncertainty to the player.
