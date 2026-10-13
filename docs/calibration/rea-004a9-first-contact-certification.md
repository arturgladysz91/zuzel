# REA-004A9 — first geometric contact and explicit event-state refusal

Base: Draft #77, `experiment/rea-004a8-frozen-heat-common-time`,
`ccb2bac78fc25972c8ed213cc0d28713a00fcf29`.

The original #55 uncertainty is resolved. **The real contact is still not accepted:**
shorter genuinely integrated partial steps expose a float lateral quantum between
trajectory versions. The earliest certified touch precedes both candidate states
by more than the unchanged `1e-7 s` bound. The experiment stops before trial
acceptance or #56C1/#56C2. No post-contact continuation or full causal heat is claimed.

## Exact diagnosis of #77

An opt-in, thread-local test observer reports refusal flags, the first uncertain
leaf, Lipschitz rate, sample count and attained depth. It creates no records/logs
when absent. Frame/discontinuity evidence remains in the existing typed report.

For the exact old actual endpoints, the only refusal is `UncertainFirst`:

| Quantity | Exact observed value |
| --- | ---: |
| Interval start [s] | 13.257002830505371 |
| Interval end [s] | 13.265295632338661 |
| First uncertain leaf start [s] | 13.265295126186206 |
| Leaf width [s] | 6.326905577225261e-8 |
| Leaf conservative lower gap [m] | 9.64012713786792e-7 |
| First sampled touch [s] | 13.265295379262433 |
| Sampled minimum [m] | 6.683278898078626e-7 |
| Signed-gap Lipschitz bound [m/s] | 5.7661375187393595 |
| Narrow evaluations | 52 of 4096 |
| Attained subdivision depth | 17 of 30 |

Both leaf endpoints are clear, but its lower bound admits touching at `1e-6 m`.
Historical `Search` stops at width <= `1e-7 s` and records `uncertainFirst` instead
of proving clearance. `Evaluate` refuses because this possible earlier event is
253.076 ns before the sampled touch, beyond its existing `2 * timeTolerance`
uncertainty band. Minimum-bound failure, depth exhaustion and evaluation exhaustion
are all absent. There are no frame gaps or boundary ambiguity in this interval.

`ProductionPoseInterval.RateBounds` is conservative here: it retains the global
slide-slope bound even after neutral straightening completion, and the `2*thetaRate`
travel-heading bound. These contribute unnecessary slack, but their validity is
not the defect. Six additional geometric evaluations already resolve the old
interval with these exact bounds. No bound, sampler, attitude or physical input
needs to change to remove the original refusal. The old witness-to-state time
mismatch is a separate condition; it does not set `NumericallyResolved`.

## Minimal geometric change

Explicit `ObserveFirstContact` continues subdividing only leaves with clear
endpoints, an unresolved first event and a lower bound admitting touch. The
root-time tolerance is an event uncertainty bound, not a proof that an unsampled
leaf is clear. Existing left-first search, pair minimum, penetration rules,
dimensions, rate bounds, samples, provenance, discontinuity and frame admission
remain unchanged. Maximum depth 30 and per-interval evaluations 4096 still apply.
Exhaustion/tangency remains an explicit refusal. A witness is accepted only when
the remaining first-event search is certified to the existing time bound.

For #77's identical sampler, the report changes from unresolved to resolved with
**58 evaluations / 56 subdivisions**, preserving the sampled first-touch time.
This operation divides geometry only and creates no rider state.

The ordinary public observer and A/B/C coordinator keep the historical search.
The pair experiment opts in only when both origins carry the frozen-heat marker.
Certification caches are scoped to one search mode. Existing exact immutable
interval identities, incoming minimum, frame and provenance remain the keys;
there is no reuse after an endpoint changes its sampler. A controlled test proves
exact repeat reuse and rejects reuse for changed geometry.

## Re-execution and the remaining representation limit

R1 retains its original 24 actual steps, ending at `13.237989014792033 s`; R2
retains its zero-step Straight cursor, original zero drive/hold and start clock.
Each trial runs `Solve/Evaluate` from those retained boundaries. Prefix steps,
speed/lateral/progress and clocks are never reconstructed from geometry.

The frozen pair narrows each real partial-step time bracket to `1e-7 / 16 s`.
This is stricter internal accuracy, not a changed acceptance tolerance; callers
cannot request a looser bound. The coupled solve, equations and 64-iteration
limit are unchanged. When touch predates a trial endpoint, the experiment requests
shorter real partial steps, certifies their new samplers and keeps clear/contact
trial evidence. It never transfers a clear certificate to a different trajectory.

The final neighboring requested times are:

- clear: `13.265295618761535 s`;
- contact candidate: `13.265295619575447 s`;
- requested bracket width: approximately `0.814 ns`.

| Actual state | Clear R1 | Contact candidate R1 | Contact candidate R2 |
| --- | ---: | ---: | ---: |
| Absolute executed time [s] | 13.26529561920375 | 13.2652956250673 | 13.265295621516506 |
| Segment progress | 0.9354752457697102 | 0.9354752501961872 | 0.005219861468464426 |
| Segment distance [m] | 24.388079941272736 | 24.388080060482025 | 0.1826951503753662 |
| LateralPosition | 0.5553498 | 0.5553499 | 1 |
| Physical offset [m] | 1.6660495 | 1.6660497 | 2 |
| Speed [m/s] | 20.350824 | 20.350824 | 22.026596 |

R2's clear and contact candidate states are identical. R1's lateral values are
adjacent `float` values, verified with `MathF.BitIncrement`. The clear sampler's
certified sampled minimum is `1.1141496641942084e-6 m`; it has no contact. The
contact sampler's minimum is `5.92393537779401e-7 m`, with certified first touch
**13.265295305171634 s**. Its actual endpoints exceed that touch by **319.896 ns
(R1) / 316.345 ns (R2)**. The required inequality
`0 <= actualEnd - firstTouch <= 1e-7 s` remains false for both.

The adapter interpolates progress/offset between solved endpoints. Shortening a
partial step rounds lateral movement and metre offset to another float value,
changing the whole open step's interpolated geometry. Thus these executions do
not expose a single prefix-preserving continuous trajectory on which an earlier
geometry root can also be evaluated as an exact physical state. Refining the
root or its time request does not remove this observed adjacent-state transition.
This is an event-state representation limitation, not an unproved #55 minimum.

The smallest next numerical step is a retained continuous partial-step extension
owned by the coupled integrator, carrying unrounded lateral/metre accumulators
through event evaluation and certifying its correspondence to `Solve/Evaluate`.
The old float public state must remain a projection of that accepted extension.
This needs an explicit representation/invariance change; A9 does not invent it,
interpolate a rider state, alter the equations, increase tolerances or relabel a
later endpoint as the earlier root. There is no proof of a safe event state here.

## Four riders, effects and continuation

The earlier frozen C preparation still verifies all six pairs in the requested
step-7 frontier. One additional all-six check uses the final actual **unaccepted**
pair sampler plus the captured genuine R3/R4 production histories in their common
window, starting at `12.415122032165527 s`. It includes R2's actually executed
previous TurnExit and preserves its Straight entry discontinuity. It reports
six pairs, 225 intersected intervals, one earliest R1–R2
contact, no numerical refusal/frame gap/boundary ambiguity. This certifies
geometry for that candidate, not four accepted rider states or a causal heat.
It costs 1998 narrow evaluations / 1548 subdivisions and runs once, not per time trial.

No time trial is accepted. Retained cursors are sealed; the ledger includes only
the unchanged 24-step R1 prefix and zero-distance R2 origin. No live state, surface
or future passage is published. #56C1/#56C2 are not applied to the real pair,
so there is no authorized real post-impact continuation to certify. The #75 safe
contact regression and unsafe-penetration refusal remain exercised through the
unchanged existing consequence and continuation code.

## Cost and validation

Costs below describe one attempt; comparison-only #77 reconstruction and reversed
repeat are excluded. Cold C setup is unchanged: 3020 physical / 435943 coupled
evaluations and 36 detached full rider segments. Pair preparation and search have
19 physical evaluations and two detached full pair forecasts in both versions.

| Work | #77 | A9 |
| --- | ---: | ---: |
| Pair time trials | 21 | 28 |
| Preparation + pair coupled evaluations | 4196 | 5473 |
| Pair forecast + time-search narrow evaluations | 1731 | 2230 |
| Pair forecast + time-search subdivisions | 1675 | 2160 |
| Final pair-report narrow evaluations | 52 | 72 |
| Original refused sampler narrow evaluations | 52 | 58 |
| Extra all-six narrow evaluations | 10200 | 12198 |
| Reused real pair certificates | 0 | 0 |
| Re-executed accepted prefix steps | 0 | 0 |

Each short suffix changes the sampler, so a previous clear certificate cannot
be reused. Cost increases are explicit; correctness is not traded for fewer
evaluations. The cache is available for exact immutable repeats, not these changed
suffixes. No global limits, heat scheduler, physical coefficient, historical
calibration, ordinary HeatSimulator or UI is changed.

The #75 safe regression retains 25 time trials / 3116 coupled evaluations,
one consumed consequence and certified safe actual continuation. Local validation
passes 281 distinct targeted C# cases and 16 A3–A9 source-extraction cases. The
expanded actual four-rider window is rechecked separately after the targeted run.

Targeted validation includes the real case/reversed order, hidden earlier contact,
proved near miss, uncertain tangency, penetration, interval boundary, unswept jump,
frame change, exact certificate reuse, #75, cursor prefixes/partial steps and
historical observer evidence. Validation uses targeted tests, one final Release
build and one standard Draft Windows CI; no full matrix or historical audit run.
