# REA-004A10 — practical event time with strict executed geometry

Base: Draft #78, `experiment/rea-004a9-certified-first-contact`,
`b4b510f42c6d805ca286313fa441c6b3cba71829`.

**10 microseconds is sufficient for the real `control/19/C/heat91` R1–R2 contact.**
One microsecond also passes. Existing #56C1/#56C2 applies once: R1 receives Crash
and stops; R2 receives MajorSave and executes one further metre. The continuation
passes all six pair checks with R1 retaining its last stationary footprint.
This is a bounded frozen-heat experiment, not a complete four-rider causal heat.

## Local time policy and comparison

`ExecutePractical` explicitly selects `1e-5 s`. The ordinary helper default stays
`1e-7 s`; comparison callers can request `1e-6 s`. Values must be finite and in
`[1e-7, 1e-5] s`, practical agreement requires retained frozen-heat cursors and
an other-rider certificate. The same local bound gates post-contact cursor
continuation. No public heat option, default mode D or ordinary A/B/C is changed.

The policy changes the event-state agreement and search termination budget only.
The coupled solve and partial-step default still use their existing `1e-7 s`.
Practical searches omit A9's additional `1e-7/16` partial-step accuracy. No physical
equation, dimension, calibration coefficient, RNG address or #55 constant changes.
#55 still locates first touch to its existing `1e-7 s` geometry time bound.
Even practical execution requires the two actual endpoint clocks to differ by
at most that unchanged geometry time tolerance; they are never overwritten.

The test performs one cold C preparation and one retained-window preparation.
Separate tokens share the identical immutable 24-step R1 prefix, zero-step R2
boundary, frozen surface and classified context. Each token owns its trial set,
consumption and contact controls. Exact-input envelope/approach caches can be
shared; geometry certificates are rebuilt for changed suffixes. Forking performs
no classification, RNG or integration. Reverse order and veto checks reuse those
same prefixes too. No accepted prefix is reintegrated across the comparison.
Two ordinary unaccepted full pair forecasts still supply each search bracket.

| Agreement tolerance | 0.1 µs, #78 baseline | 1 µs | 10 µs |
| --- | ---: | ---: | ---: |
| Actual event admitted | No | Yes | Yes |
| Largest state/touch difference [µs] | 0.3198956655 | 0.2751226162 | 0.2751226162 |
| Largest center displacement [µm] | 6.9692669484 | 5.5973289493 | 5.5973289493 |
| Pair time trials | 28 | 20 | 17 |
| Coupled evaluations including shared preparation | 5473 | 4095 | 3786 |
| Pair execution alone, coupled evaluations | 3659 | 2281 | 1972 |
| Physical evaluations including shared preparation | 19 | 19 | 19 |

The shared preparation is paid once: 17 physical / 1814 coupled evaluations.
The inclusive columns add this same preparation cost to each measured branch
for comparison with #78; it is not repeatedly executed. The 1972 count includes
R2's actual post-contact metre. Cold C/#54 setup remains 3020 physical / 435943
coupled evaluations; it is captured once and is outside these pair totals.

## Actual event states and spatial error

The accepted first-touch certificate is **13.265295379262433 s**. Both 1 and 10 µs
select the same actual states and consequence plan:

| Actually integrated state | R1 TurnExit | R2 held Straight, zero drive |
| --- | ---: | ---: |
| Absolute execution clock [s] | 13.26529565438505 | 13.265295632338661 |
| Segment progress | 0.9354752723285719 | 0.005219868280423867 |
| Executed segment distance [m] | 24.388080656528473 | 0.1826953887939453 |
| LateralPosition | 0.5553499 | 1 |
| Pre-contact speed [m/s] | 20.350824 | 22.026596 |
| Resulting status | Crashed | Racing |
| Post-contact speed [m/s] | 0 | 22.37483 |

Largest state/touch error is **275.122616 ns**; mutual integrator clock skew is
**22.046388 ns**. Largest center displacement from the certified touch pose to
that rider's own actual endpoint is **5.597329 µm**. Adding bounding radius times
the actual heading change bounds any footprint-point displacement by **5.826800 µm**.
These are alignment displacements on the executed samplers, not claims about
the absolute error of the overall provisional physics model. The tolerance is
not used to inflate bodies or infer missing trajectory coverage.

## Geometry admission, chronology and publication

A small time discrepancy never grants admission by itself:

1. Existing #55 first-contact certification must resolve all common pair intervals
   and preserve frame/discontinuity provenance. An absent witness with unresolved
   bounds still means unknown, never clear.
2. Actual endpoint footprints must not penetrate beyond the existing `1e-6 m`
   distance tolerance; their observed gap is `4.4449418612479974e-7 m`.
3. `ObserveExecutedContactWindow` adds a **stronger** opt-in penetration-floor
   certificate over the entire executed common window. It reuses #55 samples and
   Lipschitz bounds, subdividing only while a leaf could cross `-1e-6 m`. The
   minimum of the conservative terminal-leaf bounds covers the whole interval.
   A sampled forbidden penetration or depth/evaluation exhaustion refuses.
   Default observer/minimum semantics remain unchanged; 30/4096 limits remain.
4. The stronger certificate must retain the same first event within #55's own
   root-time tolerance. The caller must certify four-rider coverage and all six
   pairs; another contact or ambiguous/missing coverage vetoes acceptance.
5. Only after those checks are time trials accepted and a simultaneous existing
   consequence plan consumed. Pending full segments cannot be published through
   the unresolved event. The actual clocks remain the solver's double values.

Event window sampled minimum is `6.683278898078626e-7 m`; its certified lower
bound is **-6.130827808341179e-8 m**, above the unchanged `-1e-6 m` admission floor.
All six pre-contact pairs are checked from the step-7 common frontier. This includes
the actual prior TurnExit of R2 and its unswept Straight entry discontinuity.
An explicit veto test proves that even valid time/geometry cannot bypass another
rider's certificate. Hidden penetration, near misses, uncertain tangency, interval
boundaries, frame changes and reversed inputs retain targeted checks.

## Existing effects and actual continuation

The original parameters classify R1 as **Crash** and R2 as **MajorSave**. No severity
threshold or impulse is weakened to keep R1 riding. One pair generation is consumed;
its repeated overlap cannot apply another impulse. Applying the same plan to the
already changed state also refuses by existing pre-speed ownership checks.

R2's control loss is `0.6728558236092437`; recovery drive and lateral authority
are `0.3271441763907563`. Its captured zero drive and hold remain in force. The
same production integrator executes **1 m**, ending at **13.31003278106817 s** with
speed **22.330746 m/s**, using the new post-impact speed/recovery. It retains every
old executed step and cannot reuse a discarded forecast endpoint.

R1 has no further ridden movement or passage wear. During this short continuation
its existing terminal speed-zero state occupies the **unchanged last position,
bike heading and mechanical footprint**, with zero translational/angular bounds.
This is an explicit stationary occupancy assumption for the bounded stopped-state
experiment; no falling orientation, sliding body, removal time or enlarged shape
is simulated. R3/R4 remain captured production histories, not accepted causal cursors.

Pair continuation sampled minimum is `6.474543602918992e-7 m`; its conservative
lower bound is **-6.972368162437325e-7 m**, above the same penetration floor.
All six pairs are checked again in the continued common window. Any new pair
contact or uncertainty blocks admission. Unsafe suffixes are excluded from the
certified history and all resulting tokens are sealed. The successful certificate
therefore covers this metre with the stated terminal occupancy model.

Only accepted distance enters the frozen heat ledger: 25 R1 steps through the
event and 2 R2 steps through its continued metre, **27 total**, in two passage
entries. Prefix extension replaces each entry; repeat admission refuses. Forecasts
and stationary occupancy have no wear budget. The surface/live riders remain
unpublished, weather updates are zero, and final heat settlement is not attempted.
Contact effects/recovery contain no occurrence or severity RNG; fork/trial/resume
retains the original classifications and RNG addresses.

## Geometry work and remaining full-heat work

| Extra work excluding cold setup | #78 | A10, 1 µs | A10, 10 µs |
| --- | ---: | ---: | ---: |
| Pair forecast/search/event/continuation narrow evaluations | 2230 | 1821 | 1687 |
| All-six narrow evaluations | 1998, refused-candidate diagnostic | 2335, before + after | 2335, before + after |
| Combined narrow evaluations | 4228 | 4156 | 4022 |
| Combined geometric subdivisions | 3708 | 3624 | 3496 |
| Re-executed accepted prefix steps | 0 | 0 | 0 |

The new pre/post all-six checks cost **1980/355** evaluations. The separate strict
event-window proof costs **58**, and the pair continuation proof **33**. The two
extra admission/continuation proofs are absent from #78 because its event refuses.
Geometry inputs change with each actual trial, so their old certificates are not
reused. No all-six history is recertified at every time-search iteration. Cold
six-pair setup work remains 10200. These are deterministic operation counts,
not CPU benchmarks or a statistical fit of physical coefficients.

Ten microseconds solves **event agreement and this local continuation**, not the
entire four-rider race. A complete heat still needs actual retained frontier states
for R3/R4, atomic four-rider event admission/publication, fresh decisions after
Crash/MajorSave, fixed/standing/other-segment cursor coverage and segment handoff.
Terminal occupancy must have a defined lifetime/recording beyond this short window.
Only then can all four accepted passages settle wear and one weather update and
produce a complete result. No new race scheduler or dense integrator reconstruction
is introduced here; normal A/B/C and historical results remain unchanged.

Validation uses targeted C# and source-extraction tests, one final Release build
and one standard Draft Windows CI. No full matrix, historical audit, Ubuntu run
or broad benchmark is introduced.
