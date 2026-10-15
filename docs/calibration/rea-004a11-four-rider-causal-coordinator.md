# REA-004A11 — four actual causal rider states and the first event

Base: Draft #79, `experiment/rea-004a10-practical-contact-time`,
`f430da8b31e757ec3c7b984ce85518d0fa9eb7c4`. Evidence date: 2026-10-15.

The internal `FourRiderCausalCoordinator` executes the real control/19/C/heat91
from the tapes to **R3–R4 first contact at 4.1806383146820165 s**. All four states
are actual solved boundaries. Existing #56C1/#56C2 gives both riders Disturbed.
The result is `ContactResolved`, with one atomic four-state/event/wear publication.
It deliberately stops at this first event, before A12 continuation or race results.

This is a new causal experiment with independent segment clocks and one frozen
surface. It does not use A/B/C's committed future as physical input. The R1–R2
Crash demonstrated by #79 at 13.265295379262433 s belongs to that earlier bounded
pair experiment; it is not silently weakened or substituted here. A separate
integrated rear-closing fixture produces Crash using the unchanged parameters
and returns `InterruptedByCrash`, without a stationary-bike continuation.

## Execution and ownership

- One `FrozenHeatContext`, one immutable surface, four retained cursors and one
  `CommittedFrontier`. Lap, segment, intent, classified integration context and
  exact execution clock belong to each rider independently.
- #54 is called only for a fresh segment decision. Ready riders read the same
  accepted four-rider values; forecasts cannot enter that decision snapshot.
  Joint #56B selection is reused for riders ready on the same segment. Other
  executing riders retain their captured controls. General asynchronous tactical
  responses/replanning remain A12 work.
- Each rider keeps one speculative next native numerical step. The common horizon
  is the earliest next endpoint, not a fixed millisecond tick. #55 checks all six
  pairs over the completely covered interval. A clear certificate advances the
  frontier; only actual native endpoints at/before it become accepted states.
  A rider's older native endpoint is explicitly clocked, never interpolated into
  a newer published rider state.
- Retained forks share the exact prefix and immutable classified inputs. Each
  resume starts at its native boundary; accepted steps are not reintegrated.
  Root trials execute only the selected pair's unaccepted suffix. The other two
  riders are solved once after the pair search, followed by a new all-six proof.
- Requests retain canonical lap/segment RNG addresses and the same initial
  surface. Planning forecasts and accepted execution have separate ownership.
  Addressed random samples do not consume a shared generator. The existing
  physical consequence resolver has no occurrence/severity RNG.

`CausalPublication` contains the four exact solved states and clocks, their
origin/intent/integration context, append-only certified history, first event,
consumed pair generation, accepted step count and distance. The frozen context
owns an immutable journal/publication aggregate. **One CompareExchange publishes
the entire aggregate**; stale transactions cannot overwrite it. The admission
callback runs before this write. Once bound, the causal journal rejects ordinary
prefix/segment admission and incomplete-heat settlement, preventing bypasses.

The ordinary live riders/surface are not mutated by this experimental entrypoint.
Its publication is the actual new coordinator state, not an old A/B/C result.
A refusal preserves the preceding safe publication and journal. Event veto tests
prove no partial rider effect, contact generation or wear change is published.

## Necessary cursor and coordinate support

The explicit frozen causal cursor uses the **existing coupled traversal** for
launch, fixed lines and every corner phase. No force, correction, lateral-motion
equation, physical dimension or integration resolution is changed. Ordinary fixed
production routing remains exact and unchanged. Targeted launch/fixed/corner
checks compare the cursor with the existing production primitive results.

Native launch reaction is a stationary interval with zero movement/wear. The
double clock is carried across segment boundaries through an internal execution
origin; the public float clock is an output projection, never the new time origin.
Native one-ULP target-arrival steps that have no representable clock advance are
retained in the accumulators. Geometry omits only their unrepresentable duration
after checking the distance/progress and lateral displacement against machine
resolution; no finite move is replaced by a synthetic pose.

The old normalized width reinterpretation would put R3's footprint into R4 at
the first Straight/TurnEntry boundary (2.9556521519844643 s). A11 instead carries
the **same physical lateral offset** through the existing LaneModel conversion
and recomputes its local discrete reference lane. This is an experimental
coordinate handoff, not extra physical travel. An offset outside the next usable
width refuses, without clamping. The physical center must remain within the
unchanged #55 numerical distance bound. A fresh reference-attitude discontinuity
is marked explicitly: #55 quarantines overlap at that boundary, rather than
inventing a swept rotation or assigning it an interior first-touch time.

This A11-only handoff supersedes historical normalized reinterpretation in this
coordinator; normal A/B/C, old evidence, calibration and defaults remain unchanged.

## Actual first event

| Rider | Segment | Canonical progress | LateralPosition | Actual execution clock [s] | Final speed [m/s] |
| --- | --- | ---: | ---: | ---: | ---: |
| R1 | TurnMiddle | 2.0185011959247037 | 0.01042299 | 4.180639183848264 | 19.754972 |
| R2 | TurnMiddle | 2.080385164603861 | 0.01874418 | 4.180639162601338 | 21.43645 |
| R3 | TurnEntry | 1.9976724374714814 | 1 | 4.180639170837891 | 21.878466 |
| R4 | TurnEntry | 1.9744806608370884 | 1.3531259 | 4.180639154468221 | 23.105322 |

Each row retains an actual partial-step boundary and original decision/classified
context. Sampling the geometry never constructs these states. R3 changes from
21.974556 to 21.878466 m/s; R4 from 23.01685 to 23.105322 m/s. Existing Disturbed
recovery is stored once. R1/R2 receive no contact effect.

Largest state/first-touch discrepancy: **0.8691662474 microseconds**, below the
local maximum 10 microseconds. Native four-clock skew: **29.38004329 ns**, below
the unchanged #55 100 ns time bound. Corresponding center displacement is at most
**19.34928210 micrometres**; translation plus bounding-radius/heading displacement
bounds any footprint point by **20.26014099 micrometres**.

The final executed all-six certificate has no frame gaps or unresolved intervals.
The R3–R4 sampled minimum is **-3.092390368601705e-7 m** and its whole-interval
conservative lower bound **-9.748656992162407e-7 m**. Both satisfy the existing
`-1e-6 m` numerical penetration floor. This is not a claim of mathematically zero
overlap or absolute physical accuracy at micrometre scale. All actual endpoint
footprints are independently checked against that same floor.

All preceding certified chunks cover all four riders continuously from zero and
contain no contact. No previous or potentially earlier unresolved pair is treated
as clear. A short forecast beyond the event must exclude another pair within
10 microseconds; simultaneous groups and insufficient lookahead refuse explicitly.
No six-pair root search runs at every trial. Changed actual trajectories receive
their own certificate; an old forecast proof cannot authorize new endpoints.

There are **268 accepted native/partial steps**, **10 passage entries** and
**252.81073874845683 m** accepted distance across all four riders. The journal
replaces an extended prefix instead of adding it twice. Repeated Advance after
the event returns the same result. Forecast steps, weather updates and surface
settlement contribute zero. Accepted endpoints can exceed first touch only by
the explicitly reported local numerical agreement; all longer forecasts are sealed.

## Work and comparison with #79

| Deterministic work | #79, setup + 10 microsecond pair | A11, start to first event |
| --- | ---: | ---: |
| Physics evaluations including #54/planning | 3020 + 19 = 3039 | 824 |
| Coupled-equation evaluations including #54/planning | 435943 + 3786 = 439729 | 161788 |
| Pair time-search requests | 17 | 11 |
| Individual rider partial-time previews | 34 | 24 (22 pair + 2 others) |
| All-six causal window certificates | Background setup + pre/post pair checks | 265 |
| Pair certificates during time search | Included in pair work | 11 |
| Geometry narrow, excluding C tactical planning | 10200 + 4022 = 14222 | 39211 |
| Geometry subdivisions in the causal coordinator | Pair portion: 3496 | 35967 |
| Reexecuted accepted prefix steps | 0 | 0 |
| Full-world copies per root trial | 0 | 0 |

These runs end at **different first events and times**. The lower A11 physics
total is not a like-for-like speedup claim. A11 pays for chronological all-six
coverage from the tapes; #79 checked a retained pair against precomputed future
backgrounds. A11 executes 264 adaptive windows, ten decisions and six handoffs.
One initial ready group uses the existing joint planner. Its ordinary auxiliary
work includes 20 RiderState copies and ten perceived-surface snapshots; the heat
surface snapshot is created once. The coordinator does not clone the world at a
time trial. Actual #54 prefixes and the existing envelope caches are reused.

Total narrow evaluations including tactical planning are **52352**: 39211 in the
coordinator and 13141 in the reused planner. One representative Windows Release
`Advance()` measurement is **1.040486 s**, including planning, causal physics and
geometry with operation counters enabled (the #54 observer selects deterministic
serial counting). Initial capture and test assertions are outside that interval.
This is one machine-specific run, not a CPU benchmark matrix or calibration.

## Validation and A12 boundary

379 targeted C# cases pass, including 12 new coordinator/cursor/atomicity cases;
the final journal publication change is rechecked with all coordinator and frozen
heat cases. Twenty source-extraction tests restore exact #79 shared source bytes
before every unchanged older extraction; the original 39 legacy consumers and
all old fixtures remain pinned. Final validation is one Release build and one
standard Draft Windows CI, without Ubuntu or historical audit matrices.

A12 still needs four-rider replanning/resumption after a nonterminal event,
recovery consumption and further chronological contact generations/clearance.
Joint tactical responses across independently executing segments need integration.
Narrowing-width transitions outside the usable domain, terminal entry-physics
crashes, unresolved simultaneous groups and some complete-lap/start-finish seams
have precise refusals. Complete heat termination, final accepted wear/weather
settlement and a recorded full result remain absent. Crash restart/exclusion rules,
UI and four-lap implementation are outside A11. The existing known Crash result
is never replaced by a prescribed milder consequence.
