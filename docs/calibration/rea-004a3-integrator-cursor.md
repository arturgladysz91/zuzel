# REA-004A3 — production integration cursor

This document describes A3. [A4](rea-004a4-turnexit-cursor.md) subsequently extends
the same cursor to a restricted moving TurnExit path and retains its envelope work.
Contact execution inside a numerical step remains unsupported.

Status: **internal experimental execution mechanism**, based on Draft #71 at
`ba945c6`. General D remains unavailable. The normal A/B/C callers do not request
a pause. No equations, calibration constants or accepted historical data change.

## Actual supported execution

`SimulationEngine.BeginRiderIntegration` enters the existing rider classification
and `ResolveMovingRider` path. It supports rich `ExecutedPathTraversal` on a
Straight, a rolling Racing rider, `Ok` resolution, ordinary full drive and lateral
authority, without random incidents or contact recovery. Both a final straight
and a straight preparing for the next corner are tested, including an origin
already inside the segment. A traversal can continue holding its destination
after lateral arrival; this remains the same moving-path execution.

The pause occurs **after an accepted coupled numerical step**, after its time
solve, physical metre subdivisions and lateral arrival refinement have completed.
The requested count concerns accepted steps, not trial evaluations or arbitrary
timestamps. A request reaching the segment end returns a complete result.
No unfinished predictor/corrector or bisection state is exposed.

The token retains:

- Exact double segment-local canonical progress/end, accumulated time/distance;
  float speed, lateral position, node surface and accepted step/subdivision counts.
- Immutable copies of actual executed nodes and steps, including quadrature
  surface samples, physical buckets and solver observations needed for final wear.
- Original immutable snapshot: rider position/time/setup/skills, track geometry,
  seed/heat/decision-step/rider RNG addresses and frozen surface version.
- Selected intent and classified resolution, entry speed, planned destination,
  advance, risk, entry surface, phase and effective controls. The session retains
  the production next-corner target cache and completion functions.

Resume restores the double accumulators directly and re-enters the **same loop**
at its next iteration. `LongitudinalDynamics.CalculateMidpointDriveEndSpeed...`
is the same completed-step primitive, with no persistent state between calls.
Classification and incident addressing are not rerun. Final diagnostics, events,
`ResolvedRiderMotion` and wear use the existing production materialization once,
after traversal finishes. `SampleAtTime` supplies none of this execution state.

Tokens are single-use, synchronous, in-memory capabilities, not serializable
savegames. Resume requires reference identity of the same immutable
`TrackStateSnapshot`; even equal cells in a newly captured snapshot represent a
different version and refuse. A version refusal leaves the token usable with
its original version. A numerical execution failure consumes it and propagates;
there is no replacement interpolation or delayed-contact fallback.

## Publication and tests

`CommonTimeMotionWindow.StageSelectedMotionWithPause` retains a cursor separately
from complete pending segments. Neither its committed state nor its full-segment
forecast advances during a pause. `ResumeSelectedMotion` appends the complete
production result only after finishing. Publication refuses while any rider has
a paused prefix; existing all-rider coverage, earliest-contact and stale-world
guards still apply. Partial wear is never published or normalized independently.
Four riders can execute/resume independently in their own addressed segments.

Targeted tests compare complete results exactly (nodes, steps, speed, time,
distance, events, motion and post-commit surface cells/logs), exercise two pauses,
double precision retention, one-use tokens, version rejection, publication once
and reversed input/resume orders. Production audit counters also match continuous
execution exactly: no accepted prefix, classification, event or motion is replayed.
No wall-clock performance benchmark was run. Each pause copies its retained
prefix; token allocation and copying are additional experimental costs.

The legacy source audit reverses only the exact A3 plumbing via a new pinned
source extraction before applying older extractions. Both restored source hashes
must match #71. All historical manifests, extraction fixtures and their hashes
remain unchanged; tests reject changes to new plumbing and unchanged physics.

## Explicit remaining boundary

Fixed-line longitudinal paths, corners, standing launches, reduced drive/hold
controls, incidents and contact recovery refuse cursor entry. There is no
pause at an arbitrary time inside an accepted step, no changed control on resume,
and no partial-prefix publication or causal heat-loop integration.

The real `control/19/C` regression still reports first touch at
**13.194772190598087 s**, strictly inside rider 1's executed TurnExit step.
Its cursor entry refuses; the common guard still blocks earlier step-7 publication.
A3 does not resolve this contact.

The smallest next implementation is to extend this cursor to the TurnExit path,
retaining its initial envelope and apex/correction context, then add reviewed
step splitting/root refinement that actually integrates to the first touch.
Simply restoring a preceding node or interpolating its endpoint cannot supply
that event state. All four riders must eventually execute to the common event
boundary before existing consequences publish; affected forecasts then require
invalidation and recertification against the new surface/control version.
