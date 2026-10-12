# REA-004A6 — executed two-rider contact and continuation

Internal bounded experiment based on #74 / `5aafc1dad2936242b64035ec4ddfe51cd9ea338e`.
No ordinary A/B/C call site, calibration, historical result or CI policy changes.
No general D, rendering or next-stage implementation.

## Feasibility and scope

Real `control/19/C/heat91`: rider 1 uses moving TurnExit in step 7, rider 2 uses
moving Straight in step 8, starting at `13.187445 s`. Both are `Ok`, without
incoming recovery. The original rider-2 incident options are refused by its
Straight cursor. A5 also cannot partially execute that Straight through its
TurnExit-only API. The real case therefore remains explicitly unsupported;
this change demonstrates a controlled rear contact on the actual example track
using the existing moving Straight path, with random incidents disabled.

Both default riders start together on segment 7, at progress .05/.09, lateral
.98, speeds 18/17 m/s, neutral setups and target lane 1. They genuinely move
to lane 1 before touching; fixed-line cursor admission is not relaxed.

## Execution and safety contract

The two-rider helper owns one frozen snapshot and two production sessions.
Full forecasts only bracket the earliest contact. Bounded time trials continue
from each retained one-step prefix through the same `Solve/Evaluate` equations.
Uncommitted candidate steps can be re-evaluated; the retained accepted prefix is
not replayed. Only the chosen pair of trials is accepted, once, by its owner.
Neither `SampleAtTime` nor old segment-end speeds create execution state.

Requested cursors now retain each step's actual double time/progress/distance
and float speed/lateral endpoint. The #55 adapter can use those doubles rather
than round event geometry back through telemetry floats. Existing `FromMotion`
and A5 adapters keep their original arithmetic. This is an execution-observation
contract change, not new geometry or physics. Capsules, tolerances, surface
samples and attitude remain the existing #55 model.

A trial with unresolved geometry is never declared clear. Refinement may stop
with its previous bounds, but acceptance still requires complete final #55
certification and both executed endpoints within the existing `1e-7 s` of the
new first touch. The example endpoint clocks differ by **4.702 ns**; they are
not overwritten to pretend mathematical equality. Both domain state clocks
round to the same float, and the shared event has one certified double time.

#56C1 receives the verified poses/directions, actual endpoint speeds, canonical
physiology and actual sampled surface. #56C2 builds and applies one simultaneous
plan to the executed event states. Existing heat-scoped pair-generation rules
consume the pair once. The same traversal resumes from the retained position,
new scalar speed and existing recovery drive/lateral fractions, without entry
classification or RNG re-sampling. Recovery applies to the remaining active
segment; no old unused step or old endpoint is copied into that continuation.
The pre-event boundary stays immutable; the post-event boundary records the
zero-time speed change explicitly. Terminal contact refuses continuation at
the event, rather than moving a crash to an old segment end.

Six real numerical steps are then evaluated for each rider. #55 must resolve
their continuation, without ambiguity/frame gaps, observed penetration beyond
its contact tolerance or a minimum lower bound beyond its existing uncertainty.
Consumed impulse ownership does not waive this check. An uncertified candidate
retains history only to contact. All demonstration tokens are sealed when the
bounded experiment ends, so failed futures cannot be resumed or published.
The certified history is clipped to the covered shared horizon and contains no
old forecast future. Clipping observes geometry only; it never restores physics.

Live riders, surface, log and general `CommonTimeMotionWindow` publication remain
unchanged. Accepted passage samples are retained once across prefix/continuation;
there is **no partial wear publication**. Full race publication/accounting across
events remains subsequent work, not a second wear algorithm in this experiment.

## Demonstrated result

First touch: **0.30538477754650795 s**, freshly certified from both executed paths.

| Value | Rider 1 | Rider 2 |
| --- | --- | --- |
| Actual endpoint time (s) | 0.30538478224828397 | 0.30538477754650795 |
| Segment progress | 0.14323859695175786 | 0.17823861189640847 |
| Executed distance (m) | 5.594584763050079 | 5.294601261615753 |
| Lateral position | 1 | 1 |
| Pre-contact speed (m/s) | 18.63584 | 17.671106 |
| Post-contact speed (m/s) | 18.153473 | 18.153473 |
| Status / consequence | Racing / Disturbed | Racing / Disturbed |
| Speed after six further metres (m/s) | 18.769539 | 18.769539 |
| Time after six further metres (s) | 0.6303524248577125 | 0.6303524201559364 |
| Matched no-consequence speed (m/s) | 19.27079 | 18.37575 |
| Matched no-consequence time (s) | 0.6219192737614523 | 0.6382433762290246 |

Recovery loss is approximately .05986108 for both. The matched control starts
from exactly the same executed partition/state and continues the same equations
with no impulse/recovery. Its later steps differ, not only its diagnostics.
The safe continuation's sampled minimum gap is `9.822812163107209e-7 m`, with
lower bound `9.820340916401305e-7 m`. This remains near contact, without certified
pair release or a new generation. Separation/recontact ownership retains the
existing .65 m / .45 s rules; they are not replaced by a new impulse flag.

A lateral-encroachment variant returns uncertified continuation and a history
barrier at its contact. It is not proof that recovery solves lateral collisions;
neither uncertainty nor sampled penetration authorizes passage through a rival.

Cost of the supported demo: **25 pair-time trials / 3116 coupled evaluations**,
including both complete forecasts, trial candidates and resumed steps. There
are 12 uniquely accepted steps per rider: six before contact and six after.
This is an operation count, not a whole-heat CPU benchmark. No 342-case matrix
or historical audit is run.

Targeted tests protect the complete chain, matched no-impact execution, sealed
unsafe futures, exact retained samples, ownership/version rejection, reversed
input determinism and the explicit real-case blocker. The new exact inverse
source extraction restores #74 before all unchanged historical extractions;
original 39 consumers and older fixtures remain frozen.

Local final validation: one Release solution build, **149/149** targeted C#
cases and **10/10** A3–A6 Python extraction cases passed. Local build had only
NU1900 from the unavailable NuGet vulnerability service. Three external
`control/19/heat91` smoke runs, one A/B/C each, match #74 exactly across 489,554
typed fields (413,014 IEEE values). Standard Draft Windows CI validates the
final commit; full historical audits and the 342-case matrix remain excluded.
