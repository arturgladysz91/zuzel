# REA-004A5 — actual partial TurnExit step at first touch

Status: **internal experimental execution**, based on Draft #73 at
`ffe5be37c1402706252835d5a6145004290c2699`. Ordinary A/B/C callers, constants,
calibration and accepted historical evidence remain unchanged. General D,
post-impact heat execution and partial publication remain unavailable.

## Execution contract

`RiderIntegrationCursor.SplitNextStepAtTime` and `SplitNextStepAtFirstTouch`
continue the existing `ExecutedPathTraversal` from its accepted TurnExit
boundary. Admission remains A4's rich, moving, rolling `Ok` traversal with
ordinary controls, without launch, hold, interaction targets or recovery.
Straight's accepted-step pause/resume remains supported; partial Straight,
other segments and incident paths are outside this change.

The original next-step construction still applies its metre bound, corner
knots, lateral arrival split and coupled-solve subdivisions. Its full endpoint
is evaluated speculatively to bracket an interior event. A bounded bisection
then changes only the requested canonical endpoint: **every trial calls the
existing `Solve`/`Evaluate` equations from the retained boundary**. Time, lateral
movement, polar distance, sampled surface, correction, carry, signed drive and
envelope constraints are solved together. No interpolation supplies integrator
state and no alternative integrator is introduced.

Trials do not append nodes/steps, move accumulators, materialize a segment, draw
entry RNG again or publish wear/events. Envelope query caches may grow as pure
reusable computations. Only the chosen endpoint becomes one accepted shortened
step and one node. Existing time tolerance is `1e-7 s`, with at most the existing
64 bisection iterations; nonmonotonic time, an absent interior bracket or an
unresolved bound refuses explicitly. The accepted prefix is copied as retained
values, without re-executing its equations.

The returned boundary retains double canonical progress/end, elapsed local time
and distance; float speed/lateral position and surface; original context,
decision, setup, RNG addresses and exact frozen surface-version identity;
accepted nodes/steps, correction/carry/drive samples and subdivision counters;
initial corner envelope, apex lateral position, current corner phase and the
exclusive A4 envelope/scalar caches. `ExecutedPartialStep` additionally records
the clear lower search endpoint and the discarded full-step endpoint/time.

The parent token is consumed before numerical execution. Version/admission
refusals do not consume it; a failed attempted execution does. A returned
partial event token **refuses ordinary `Resume` and another split**. Its old
full endpoint/time are diagnostic forecasts only: the unused advance must be
recomputed from explicit post-event state/controls in a later change. This PR
does not provide that continuation contract or apply an impulse.

## Geometry and common time

First-touch search requires one other rider's actual production pose history,
sorted/stitched by the existing common-time history code, continuously covering
each trial. `ResolvedBikePoses.FromExecutedStep` projects the two actually solved
endpoints into the existing #55 metric frame, capsule, attitude and swept-pose
observer. It uses double execution clocks without remapping them onto the old
complete-segment endpoint. The normal `FromMotion` arithmetic remains identical.
The observer's interpolation between executed samples remains its geometry
model; it never reconstructs an integrator speed, position or accumulator.

A sampled touch can supply an upper search witness even if #55's *later* minimum
search exhausts its budget on a discarded trial. It cannot certify publication.
Every clear lower trial and the **final accepted endpoint** require resolved
geometry, with no frame gaps or boundary ambiguity. The final endpoint must lie
within `1e-7 s` of its newly certified first touch; missing coverage, initial
overlap and unresolved final geometry refuse. The geometric tolerances and
mechanical dimensions remain #55's existing values.

`CommonTimeMotionWindow.SplitSelectedMotionAtFirstTouch` retains the resulting
cursor separately from complete pending motions. Its watermark, committed
history, rider status, event log and surface do not advance. Repeated attempts
to publish the paused result refuse before mutation. There is no wear
normalization or completed production motion until the existing completion
contract can run; partial passage samples are retained for future accounting.

This certifies a **single pair against a frozen reference opponent trajectory**.
It is not an all-four first-event guarantee: another opponent can invalidate
that trajectory after an earlier event. The reference C history is geometric
evidence, not permission to commit its original later states or surface versions.

## Real control / 19 / C / heat 91

The test captures the real production heat before step-8 commit. Rider 1's
step-7 execution is segment 7 / TurnExit / logical corner 2, independent
`E2-A2-X2` intent, `Ok`, without modified controls or incoming recovery.
The retained 24-step prefix exactly matches that execution.

| Value | After accepted step 24 | After accepted partial step 25 |
| --- | --- | --- |
| Common execution time (s) | 13.16668989157769 | **13.194784676331274** |
| Local canonical segment progress | 0.9145583394510511 | 0.9359733403877556 |
| Executed segment distance (m) | 23.83666032552719 | 24.41359168291092 |
| Speed (m/s) | 20.514252 | 20.5561 |
| Normalized lateral position | 0.5503331 | 0.5630424 |

The accepted part advances approximately `0.5769313574 m` in
`0.0280947848 s`. It adds one step and one node: 25 accepted steps / 26 nodes
in total. The unmodified original root was `13.194772190598087 s`; #55
recertifies the new shortened-step geometry at **13.194784676331274 s**, about
12.49 microseconds later. No timestamp is forced. Final certification is
resolved, with minimum capsule gap `8.960321727924381e-7 m`, inside #55's
existing `1e-6 m` touch tolerance, without accepted penetration.

The discarded full-step endpoint was `0.9516569275931949` at
`13.21535040820075 s`. Its remaining canonical advance is approximately
`0.0156835872`; its old remaining duration of `20.5657319 ms` is **not** a
post-impact prediction. There is no accepted step beyond the new event boundary.
The shortened partition is not expected to match the old unsplit trajectory
bitwise; the executed prefix and ordinary A/B/C are expected to match exactly.

The real search uses **20 endpoint trials and 63 coupled equation evaluations**,
including construction of the speculative full-step bracket. Prefix integration
is not repeated; one accepted step/node is materialized, with no completed
motion/event/private commit. This is an operation count, not a wall-time or
whole-heat performance claim. Geometry is observed at each trial as well.

## Validation and next step

Targeted tests cover a genuine shortened step with and without correction,
time/distance/progress monotonicity, retained envelope state/surface samples,
single-use tokens, version mismatch, unsupported paths, missing/overlapping
opponent history, four independent rider input orders, real recertified contact,
reversed opponent history, exact prefix retention and repeated publication
refusal with unchanged live state/wear/log. Existing Straight, TurnExit,
common-time, executed-path, motion and continuous-wear regressions also run.

The compatibility guard adds an exact inverse source extraction pinned to #73
and pins the new helper, before applying the unchanged older extractions. The
historical 39 consumer fingerprints and accepted result fixtures are unchanged.
Targeted Python mutation tests prove both new execution and old physics edits
are rejected by that guard.

Final local validation: one Release solution build, **103/103** targeted C#
cases (including Straight/TurnExit and the reused #55 pose mapping), and **8/8**
A3/A4/A5 Python extraction cases passed. The build had zero errors and one
`NU1900` warning because the local NuGet vulnerability service was unavailable.
A small external harness compared one complete `control/19/heat91` run in
each A/B/C mode against the pre-change #73 Release binary: all three hashes
matched, covering 489,554 typed leaves including 413,014 exact IEEE values,
with states, surface, events, motions and contact diagnostics. This is a smoke
comparison of three runs, not the historical 342-case matrix. The standard Draft
Windows CI is the remaining repository-level validation; no workflow changes
or historical audits are requested.

**There is now actual production integration to an interior first-touch boundary
without accepting or publishing its future, within the existing solver and
geometry tolerances, for this supported TurnExit pair.** There is not yet a
complete causal four-rider heat. The smallest next step is an explicit
post-event continuation that consumes the accepted boundary and the existing
contact resolver's simultaneous speed/status/recovery changes once, invalidates
old future forecasts, and continues the same integrator. Before applying those
changes, the shared-clock coordinator must provide actual event-time states for
both participants and verify the earliest event against all four riders; a
frozen peer reference cannot substitute for that step. Start/other segments,
successive contacts and recovery admission remain subsequent bounded work.

The final game contract remains: compute the whole heat before animation, then
record time trajectories, bike orientations, events and result for read-only
playback. This PR implements no renderer or playback API.
