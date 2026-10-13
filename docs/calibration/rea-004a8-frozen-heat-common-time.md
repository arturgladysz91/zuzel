# REA-004A8 — frozen heat surface, deferred wear and asynchronous contact attempt

Based on Draft #76 / `3fe36abb01bb0367a2d2f716b838863c09d5f81d`.
Implementation status: **explicit internal experiment, ordinary A/B/C unchanged**.
The surface-version blocker is removed. The real pair reaches a newly executed
common-time candidate, but #55 does not numerically certify that candidate.
No real contact consequence or continuation is accepted.

## Binding heat-lifetime decision

Weather and surface are fixed throughout one heat. At its beginning capture one
immutable surface and the weather inputs; all four riders use that version for
every segment/lap and their independent clock origins. Keep accepted passage
samples without altering the current physics surface. After the heat ends, apply
accepted wear once and one weather update. The resulting surface belongs to the
next heat. There is no within-heat weather/wear timeline or dynamic surface system.

This is the new **BINDING target contract**. Historical A/B/C still performs its
old per-segment weather and passage commits by explicit task scope. Those older
documentation paragraphs describe compatibility behavior, not the new target.
`HeatSimulator` has no call to the experimental context and no new mode is enabled.

## Frozen context and post-heat accounting

`FrozenHeatContext` captures `TrackStateSnapshot` exactly once. Its captures reuse
the same object, preserve all rider values and addressed heat/step/seed/lap/segment
fields, and carry an internal heat-lifetime marker. The existing #54 evaluator
honors that marker too: rich and lean decision forecasts keep their immutable
surface, skip private passage commits and share unchanged branch surface state.
Perceived surface inputs remain the existing decision model's observations.
Ordinary snapshots leave the marker false and preserve the old evaluator path.

Only explicitly accepted complete resolutions or cursor prefixes enter the
passage ledger. Complete acceptance validates original rider/world ownership;
prefix acceptance checks actual double clocks against certified coverage and the
first unresolved contact. Repeated admission is rejected; an extended prefix must
retain every old step exactly. Pauses therefore replace one ledger entry with its
longer accepted prefix rather than append duplicate samples. Forecasts and rejected
time trials have no accounting entry. Terminal/interrupted movement is charged
for its accepted distance, not treated as a complete segment or omitted solely
because the old crash policy skipped segment wear.

The existing `.004` Straight / `.015` turn ruts budgets, `-.25` grip relationship,
interpolated center/adjacent kernels and `.20` neighboring fraction are unchanged.
The new accounting gives that original segment budget a physical-distance density:

`budget × accepted physical metres / full entry-reference segment length`.

Moving samples preserve their actual midpoint kernel and normalize its distribution
to the original entry-kernel total, then scale it by the accepted distance ratio.
Fixed-line passages use their existing entry kernel and actual diagnostic distance.
A full moving path may travel slightly farther than the reference arc/straight;
that extra distance is charged. This explicit distance scaling implements partial
passages without fitting the new totals to old per-segment commits. There is no
invented crash or partial-wear coefficient.

Settlement sorts by addressed step and rider id, applies deltas to one detached
surface, applies the captured weather once, then publishes the surface and logs.
External surface changes and a second settlement refuse. The current frozen
snapshot remains unchanged even after settlement. An unfinished causal experiment
does not settle its heat or charge its unaccepted four-rider future.

Tests exercise an entire four-rider, four-lap heat with constant identity/values,
one weather update at completion, next-heat visibility, rich/lean frozen planning,
partial-distance conservation, prefix extension once, refused future admission and
deterministic settlement under reversed passage admission.

## Common-time integration

The cursor now admits a classified zero-step boundary. It contains the original
speed/lateral position, phase/envelope caches and addressed decision, with no
movement or second classification/RNG. Moving Straight also retains its actual
#56B drive and hold controls; the production traversal already implements them.
Corner controls, physical interaction targets and incoming recovery keep their
existing cursor refusals. Ordinary fixed-line paths are not replaced.

The existing pair helper accepts independently retained cursors on the same world
version. It keeps each original start clock/context and reuses accepted numerical
prefixes. Only overlapping executed coverage can supply a touch. Full forecasts
bracket the search; all candidate states come from `Solve/Evaluate` and fresh #55
observations. No trajectory sampler creates a rider state and missing pre-segment
history is not filled. A caller's other-rider certificate can veto the pair before
trial acceptance/consequences and can reject its continuation. Final uncertainty
seals the retained tokens and returns typed diagnostics of the **unaccepted**
boundaries/cost, before constructing a physical contact plan.

## Real `control / seed 19 / C / heat 91`

The test starts a fresh frozen experiment from the catalog's four starting riders
and initial surface, with the original C options and incident frequency 1. It
captures actual selected C decisions, prepares the coherent state before Commit 7
and keeps lookahead in detached forecast state. No old step-7/8 snapshot is selected.
The live riders/surface remain untouched. Earlier setup replay and its coarse
width-boundary quarantines are not claimed as a complete causal heat certificate.

All six pairs are checked in the requested step-7 frontier before publication.
The first forecast touch is R1–R2, TurnExit 7 / Straight 8, at
**13.265199338930188 s**; no other pair has an earlier mechanical contact in that
window. This is only a new bracket, not an imposed event time. The historical
`13.194772190598087 s` is not an input to execution.

R1 enters step 7 at `12.008455 s`, speed `18.564419`, lateral 0. R2's real Straight
origin is `13.257003 s`, speed `22.034601`, lateral 1. R3/R4 step-7 origins are
`12.415122 / 12.412069 s`; their real production histories remain in the six-pair
coverage check. R2's selected C response is **E1-A1-X1, zero positive drive and
hold lateral position**, rather than substituting the independent E0 decision.

`CommonTimeMotionWindow` retains R1's **24 actual steps**, ending at
**13.237989014792033 s**, and R2's classified zero-step cursor. R1's prefix stays
immutable throughout all trials. R2 physically executes a part of its first
Straight step. Both use the identical heat surface; no clock is equalized.

The final attempted boundaries are:

| Unaccepted candidate | R1 TurnExit | R2 Straight |
| --- | --- | --- |
| Actual absolute clock [s] | 13.26529565438505 | 13.265295632338661 |
| Segment progress | 0.9354752723285719 | 0.005219868280423867 |
| Segment distance [m] | 24.388080656528473 | 0.1826953887939453 |
| Lateral position | 0.5553499 | 1 |
| Speed [m/s] | 20.350824 | 22.026596 |

Requested trial time is `13.265295613064154 s`; both actual endpoints are within
the unchanged `1e-7 s` integration bound and differ by about 22 ns. They are not
overwritten. #55 observes a touch witness near `13.265295379262433 s` and minimum
separation `6.683278898078626e-7 m`, but the final interval
`13.257002830505371 .. 13.265295632338661 s` has **NumericallyResolved=false**.
Its witness is also about 253 ns before the later pair endpoint, outside the
event-state tolerance. It cannot authorize a simultaneous physical consequence.

**Result: explicit refusal before #56C1/#56C2 application, no approved contact
time, no accepted post-contact motion and no certified continuation.** The ledger
retains only R1's 24 accepted steps and the zero-distance R2 origin; attempted
suffixes are not charged. Neither an old endpoint nor tracker suppression grants
permission to cross this unresolved event. Reversed execution/input order produces
identical refusal evidence, candidates and search counters.

## Remaining work and validation

The next independent step is a numerically resolved #55 certificate for the short
mixed TurnExit / held Straight interval, retaining all current tolerances and
re-executed integrator states. Diagnose the detector/time-bracket uncertainty;
do not lower its requirements or apply a witness as a contact. After that, validate
the changed-state continuation against all six pairs before admitting it.

A full four-rider causal heat additionally needs retained frontier states for
fixed paths/other segment types, segment handoff, fresh decisions after accepted
events and atomic four-rider publication. The deferred ledger must receive all
four genuinely accepted passages before final settlement. The event-free frozen
heat test proves the lifetime/accounting contract; this real refused attempt does
not prove an entire causal race.

Counters separate cold C setup forecasts, retained-cursor preparation, pair time
trials and the extra all-pair certification. No world is copied during a pair time
trial and an accepted prefix is never reintegrated. A7 source restoration now has
one exact A8 inverse; all 39 original historical consumers and older fixtures stay
frozen. Validation is limited to the changed components/real case, one final
Release solution build and one standard Draft Windows CI. No 342-case matrix,
Ubuntu run, full historical audit or broad benchmark is introduced.

Local targeted validation covers **216 distinct C# cases** (173 foundation/
interaction cases plus 47 frozen-planning/evaluator cases, with four overlaps)
and **14 A3–A8 extraction tests**, all passing. The source and final Release
build are validated again by standard Draft Windows CI.

| Cost of one real-case attempt | Count |
| --- | ---: |
| Cold C setup physics evaluations | 3020 |
| Cold C setup coupled evaluations | 435943 |
| Retained-window preparation plus attempted pair physics evaluations | 19 |
| Retained-window preparation plus attempted pair coupled evaluations | 4196 |
| Pair-time trials, all rejected before acceptance | 21 |
| Unaccepted complete rider segments in cold capture | 36 |
| Additional full pair forecasts, unaccepted | 2 |
| Extra all-pair narrow-phase evaluations | 10200 |
| Narrow-phase evaluations in final refused pair report | 52 |

Totals are 3039 physics / 440139 coupled evaluations, dominated by existing
Adaptive/#56B setup planning. Internal planning alternatives are included in the
3020 setup evaluations, not miscounted as 36 scheduler segments. The 4196 count
includes replay preparation of 15 complete selected rider segments and the two
initial cursors; only the already accepted 24-step cursor prefix is reused during
the pair trials. #75's simpler shared-start Straight demonstration remains
25 trials / 3116 coupled evaluations. The two counts are different workloads,
not a whole-race CPU benchmark or a claim that refusal improves contact cost.
