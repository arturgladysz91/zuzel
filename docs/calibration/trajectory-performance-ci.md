# Exact trajectory performance and CI follow-up

The behavior baseline is production `6ab245c`, evidence Git blob
`ad266b8861a7518fd62da28ccd7ed4c4eaeabd34`, and the original all-candidate SHA-256
`F20AE8E3895CF54625D549DB127BA1C8E2168A7AA3549ED94F305A04D89C3D18`.
[The platform audit](trajectory-platform-audit.md) found only serializer newline
differences, with zero numeric or behavioral divergence. The raw golden remains
one exact hash on Windows and Ubuntu; there is no numeric normalization.

## Shared scalar envelope

Moving continuous samples usually ask for one envelope value. They now use value
parameters and one shared `SpeedCore`, without constructing an envelope object,
lock, list or expanding speed array. The reusable public object uses that same
primitive with its canonical full-metre cache. Held target positions retain this
cache; distinct moving queries memoize only the exact lateral/raw-surface/progress
bits within one segment. Factory arithmetic, one-metre steps, midpoint availability,
float/double cast order and exact remainder remain unchanged.

The independent audited-arithmetic oracle checks 65610 queries across all five
integer anchors, continuous and edge positions, three raw surfaces, three skill
levels, every gearing/traction endpoint and midpoint, apex/full-drive boundaries,
exit, increasing/reversed queries and remainder lengths. Existing Full/Lean tests
also cover entry speed, launch/gates/reaction, fixed/moving paths, partial segments,
RunWide/crash, solver subdivision and raw own-wear bits. The original source digests
remain protected through a strict frozen extraction mapping, not a loose removal
of numerical expressions. Historical artifacts are untouched.

## Data flow and scheduling

| data | consumer and treatment |
| --- | --- |
| Immutable rider/raw surface, geometry, addressed RNG | Required physical inputs, unchanged |
| Speed, canonical elapsed/distance/progress, outcome, reaction, wear | Required replay/state/selection inputs, unchanged |
| Force during signed drive; correction capability during correction/preparation | Required physical calculations, same primitives and operation order |
| Force during correction/carry/crash | Observation only; Full records it, Lean omits it |
| Standing-start force in a positive-speed solver initial guess | Unused; calculate only for the zero-speed branch |
| Rich motions/nodes/events/strings | Full only; zero such projection materializations |
| Canonical published candidate records/Selected clones | Explicit Evaluate only; normal Decide omits publication |
| Compact phase endpoints/apex and fixed-profile scalar totals | Retained by the typed replay API; no rich graph is created |

Independent first-target groups execute in at most four workers. Every worker
owns its prefix graph, branch state and result list; observations are sampled once
from the common immutable root. Canonical reduction retains the original tie order.
Domain failures retain their original type/message in stable first-target order.
No engine-wide parallelism, pooling, global mutable cache or shared mutable state
is introduced. The optional synchronous counter observer uses degree 1; explicit
degree 1/4 tests independently compare every candidate and unique physical count,
complete heat classification, final riders and raw surfaces, including incidents.

## Measurements

Desktop Windows, .NET 8.0.30 Release, five warm-ups and fifteen measured samples,
four laps, incidents/logs OFF, existing contact ON. Allocations are cumulative MB
(decimal), not peak memory. Parallel samples use process-wide
`GC.GetTotalAllocatedBytes(precise: true)` so worker allocations are included.

| stage | solo ms / MB | four-rider ms / MB |
| --- | ---: | ---: |
| Supplied current HEAD | 342.626 / 129.577 | 1322.303 / 498.089 |
| Re-measured with optional counters | 359.224 / 129.577 | 1339.325 / 498.089 |
| Shared scalar envelope | 333.403 / 46.720 | 1299.725 / 179.062 |
| Physical/observation data flow | 333.197 / 46.720 | 1282.158 / 179.062 |
| Initial parallel prototype | 107.911 / 47.240 | 397.867 / 181.041 |
| Final degree 1, process-wide allocations | 328.462 / 46.728 | 1272.575 / 179.093 |
| Final production degree 4 | 99.210 / 47.023 | 382.144 / 180.206 |

Parallelism is retained: 3.33x faster than the final sequential four-rider path,
with about 0.62% more allocation. Against the supplied current HEAD, the final
four-rider path is 3.46x faster with 2.76x less cumulative allocation. The model is
unchanged; timing is an observation, never a CI assertion.

Candidate/physical counts remain solo 844/2339 and four-rider 3248/9119. Projection
still performs 1666204 coupled evaluations. Envelope objects fall from 929378 to
218999; scalar queries = 728529. Canonical full-metre envelope integrations are
18803343, plus 736340 remainder integrations. Full metres increase slightly from
18494673 because distinct single queries do not retain a complete speed list.
The measured allocation/wall-time gain justifies that tradeoff. Early stage counters
included remainders; the final JSON separates both counts explicitly.

Possible actual prefix visits = 14955; unique executions = 9119; duplicate
executions avoided = 5836. Assertions reconcile these totals and repeated/reversed
requests; parallel partitions retain identical unique execution counts.

## Full CI responsibility

Every test class has a stable `Shard` Trait. Current discovery:

| shard | cases | purpose |
| --- | ---: | --- |
| core | 1035 | Unit physics/domain rules |
| trajectory | 129 | Decision/evaluator/Full-Lean/degree/evidence/integration |
| historical-analysis | 473 | Complete calibration analyses and immutable provenance/blob/schema guards |
| four-rider | 48 | Shared current aggregate readers and independent repeated/reversed/culture/instrumentation runs |

Full discovered = 1685; union = 1685; missing = 0; unintended duplicates = 0.
`tools/ci/check-shards.py` repeats full/per-shard `--list-tests` in CI. After each
shard, `profile-results.py` verifies its TRX names against discovery, requires every
case Passed and emits top 20 method durations plus cumulative class durations and
trajectory percentage. All jobs have timeout 30 minutes.

The two four-rider reader classes already use one immutable aggregate; they now
share an xUnit collection as well, preventing duplicate waiting time from appearing
as two aggregate computations in TRX. Independent repetitions remain real replays.
Historical byte/provenance guards read protected artifacts without current AI
regeneration. Current analysis integration tests retain their full scenario/seed
coverage. No historical artifacts, tests or seeds are removed.

A small Windows/Ubuntu matrix repeats exact candidate/evidence, Full/Lean, scalar
and degree parity tests. A dependent comparison checks both raw capture bytes and
all typed IEEE/final-state observations, so a new platform divergence fails CI.
These smoke repeats are intentional and separate from full-suite responsibility.
Python's 21 tests run in their own job.

Before this pass, the available 1622-case TRX had 76.80% trajectory-related summed
test duration. Top entries were aggregate readers 2687.792/2206.951 seconds,
turning-slip regeneration 444.503, grip availability 400.541 and dynamic-line
regret 234.347 seconds. The current-head resumed reader batch showed one shared
aggregate taking about 1515.465 seconds, also reflected as waiting time in the other
reader class. These are duration observations from different source stages; summed
test times include concurrent/waiting time and are not CPU or wall-time totals.
Exact-head post-change profiles are attached by each full CI shard.

PR #54 remains Draft. No interaction, coefficients, grammar, TrackReading, physical
resolution/tolerances, wear, horizon or production Resolve/Commit rules change.
