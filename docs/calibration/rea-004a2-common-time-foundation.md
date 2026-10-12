# REA-004A2 — common physical time and guarded publication

Status: **internal experimental foundation; general D remains unavailable**.
This document describes A2. The subsequent [A3 production cursor](rea-004a3-integrator-cursor.md)
implements accepted-step pause/resume for a restricted Straight path; the
within-step contact and general publication gaps below still remain.
Based on Draft #70 (`cd5e372`), including #66's offline prototype and current
Windows-only CI. No historical evidence or calibration parameters change.

## Implemented boundary

`CommonTimeMotionWindow` in CoreSim executes preselected rider controls using
`SimulationEngine.ResolveProduction`. Every rider has an independent exact
segment/lap boundary, forecast state, addressed decision step and retained
`PendingRiderSegment` (intent, snapshot, actual resolution, events, motion and wear
diagnostics). Four riders can start in four different segments at one exact
physical time. The snapshot surface version is frozen for the entire window.
Staging changes no live rider, surface, log, pair generation or committed history.

The caller supplies already selected controls. This API does **not** run the
contested-space response planner or import its endpoint consequence callbacks.
It is internal and explicitly invoked; neither `HeatSimulator` nor the normal
A/B/C `SimulationEngine.Resolve` flow calls it. It is a forward publication
foundation, not an alternative heat simulator or a completed causal replay.

Before the first complete segment of each rider can publish:

1. Every rider must have continuous actual production pose coverage from the
   shared origin through the latest candidate endpoint. The shortest forecast
   limits all four riders, including riders outside the contact pair. Additional
   complete segment forecasts can supply this lookahead.
2. Existing #55 observes the stitched forecasts in a deterministic shared metric
   frame. Missing time coverage, unresolved geometry and ambiguous frames refuse
   explicitly. Clipping limits the observation domain only; it creates no rider
   position or executed prefix.
3. All candidate endpoints must be **strictly before** the earliest unresolved
   touch. Equality also refuses: the event state/consequences must be resolved
   before anything at that event can publish. No timestamp is moved.
4. Exact captured rider values (including condition, recovery and status) and
   surface cells must still match the live world. Weather/manager work changing
   any cell invalidates the window.
5. Production commits first execute on detached rider/surface/log copies. Only
   after preflight succeeds are final rider changes, exact surface cells and
   buffered events published, in stable rider-id order. Deltas are not applied
   twice. Only the first published segment of each rider enters committed motion
   history; speculative suffixes remain separate.

`CommittedWatermarkSeconds` is the minimum end time of the published complete
segments: all riders have actually executed published motion through it. Individual
published segment endpoints may be later, but never beyond the certified horizon
or through an unresolved contact. `CertifiedThroughSeconds` is a geometric safety
certificate, **not** a materialized rider state at that time. Both start unchanged
on refusal. A window publishes once and is then sealed. Its suffix retains exact
frozen provenance and is not silently reused after passage wear changes the world.

RNG remains stateless and addressed by the original seed/heat/decision-step/rider
channels. Preflight uses the already resolved changes; it performs no new decision,
incident draw, traversal or contact application. A rider cannot stage the same
decision-step address twice. This scope has no contact-generation commits: every
contact that would affect publication refuses, so no endpoint consequence can
silently enter the experiment or consume/rearm a physical pair.

Weather must already be represented by the captured world version. This window
does not tick weather. Ordering weather across future common-time windows remains
a heat-host integration task; the logical A/B/C weather policy is unchanged.

## Real regression and the earlier publication that must be held

The focused test runs the **current** production `control / seed 19 / C` from the
existing readiness catalogue, stopping in the observer before `Commit(step 8)`.
It retains the current #55/#56C certificate, without modifying accepted evidence:

- Step 8, pair 1–2, first touch: **13.194772190598087 s**.
- Rider 1's step-7 endpoint: **13.278690338134766 s**.
- Step-7 candidate endpoints for riders **1, 3, 4** are already later than touch.

The same `CommonTimeCommitGuard` used by the new window rejects step 7 with only
step-7 coverage, before the later contact is known. Its shortest rider horizon
cannot authorize the other endpoints. Once step 8 reveals the certified earlier
touch, the guard refuses all-at-once publication of step 7 with
`PartialProductionCursorRequired`. The retained source states do not change.
The test also asserts touch is strictly inside rider 1's actual segment-7 node
interval, showing that an endpoint-only continuation cannot establish that event
state. This regression checks the production witness against the new publication
invariants; it does **not** claim the new window completes a `control/19/D` heat.

## Supported and missing

Supported here: contact-free, certified complete-segment publication on a frozen
world version, exact segment-boundary continuations, independent four-rider
segment ownership, unchanged incoming production constraints/incidents/recovery,
single wear/events/status publication, stale/duplicate refusal and deterministic
staging/publication order. Existing #66 shared-start longitudinal impulse/crash
fixtures remain a separate offline prototype; they are not newly enabled by A2.

No new contact consequence is implemented. Shared-start, boundary and interior
contacts that intersect candidate publication all stop explicitly. Incomplete
terminal-rider coverage cannot authorize another rider's later motion. Recontact,
event generations, side/yaw dynamics and asynchronous *materialized event states*
still need the next stage. An asynchronous snapshot alone is refused as a new
window origin; it lacks retained prefixes back to a shared time.

The smallest next step is a production pause/resume token at an actual integration
boundary, then a reviewed execution extension/root refinement for first touch
inside its distance step. For rider 1 at the regression, it must retain segment 7's
exact distance/time prefix and current partition; `ExecutedPathTraversal` currently
keeps `start/end/finish`, double time/distance accumulators, lateral/speed state,
reaction, sampled surface, envelope/solve context and wear samples as local state.
Fixed-line longitudinal/corner paths also need compatible tokens. The retained
resolution and `SampleAtTime` cannot supply that missing execution state.

After that token exists, advance **all four** riders to the same first-touch state,
freeze and apply the existing consequence plan once, discard affected forecasts,
resume production motion, and re-run #55. Publish only verified prefixes; normalize
partial passage wear once and carry weather/control/pair-generation provenance
through the transaction. Recovery redesign and physics tuning stay out of this step.

## Validation

Focused tests cover actual mixed-segment execution, exact production wear/log parity,
single publication and suffix separation, missing/short unaffected-rider coverage,
initial/interior contact refusal, stale surface/rider versions, stored control/RNG
addresses, order reversal, terminal status and the real step-8 witness. Routine
validation uses one final Release solution build plus changed-module tests and the
standard Draft Windows CI. No 342-case matrix, benchmark or historical audit is
added or rerun here. Performance cost of the experimental window is **not measured**;
the ordinary A/B/C flow pays no window execution/certification/preflight cost.
