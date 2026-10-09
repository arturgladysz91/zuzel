# REA-009 — exact canonical segment-boundary handoff

**BINDING completion contract; unchanged PROVISIONAL physical model.**
Base main: `cd19c399dfc97d7abbd2aca03d50aa2c94a76262` (merged REA-001 correction).
Production implementation: `7285430c8f1a563e6984c0e22cd74dfcdd8d7c78`.
Draft PR: https://github.com/arturgladysz91/zuzel/pull/65.
Later audit/test/documentation commits do not change this production implementation.
Historical readiness evidence, data, goldens, binary-fraction companions and issue #62 remain frozen.

## Failure and exact numerical cause

All 18 original `three-squeeze` and `four-close-regain` combinations (seeds 7/19/83,
A/B/C) reproduce `InvalidOperationException: Rider 1 is not in segment 1.` on
unchanged main. The saved cases have `Failed=true`, no `LastCompletedStep`, and
unchanged real pre-Commit state. A successful diagnostic command only means its
failed-heat evidence was saved.

The trace is:

1. `BehaviorRiderInput.Create` passes the original `.1f`/`.4f` to `RiderPosition.Create`.
2. Create promotes that float to the double canonical total; `RiderSnapshot.SegmentProgress` derives a float.
3. `ResolveRiderCore` validates current segment ownership, then computes `1f - SegmentProgress`.
4. Non-crash `canonicalAdvance` is that float remainder. Physics integrates it unchanged.
5. General `RiderPosition.Advance` adds the float to the double total.
6. `TrajectoryEvaluator.ResolvePrefix` retains the resolved position; `RiderSnapshot.Apply` copies it.
7. The horizon requests the next discrete segment, but the rider can still own the preceding one.
8. The next `ResolveRiderCore` segment validation throws before the first actual heat Commit.

| Input | Promoted float start | Float remainder | Old double endpoint | Endpoint IEEE bits |
| --- | ---: | ---: | ---: | --- |
| `.1f` | 0.10000000149011612 | 0.8999999761581421 | 0.9999999776482582 | `double:3FEFFFFFF4000000` |
| `.4f` | 0.4000000059604645 | 0.6000000238418579 | 1.0000000298023224 | `double:3FF0000008000000` |

The `.1f` shortfall is `2.2351741790771484e-8`, greater than the unchanged `1e-9`
BoundaryTolerance. `.4f` overshoots by `2.9802322387695312e-8`: the next segment
may validate, but its origin is still wrong. A four-case regression was run before
production changes: both `.1f` variants threw, and both `.4f` variants failed exact
endpoint equality. All four pass after the correction.

Increasing tolerance would erase genuine remaining distance. For example,
`MathF.BitDecrement(1f)` is a valid fractional start, `5.960464477539063e-8`
before the boundary. Completion is a semantic fact, not a proximity measurement.
Constructor validation, general Advance and BoundaryTolerance are unchanged.

## Explicit completion and production coverage

`RiderPosition.AdvanceToSegmentEnd(actualPhysicalDistanceMeters)` is internal and
immutable. It obtains the next integer boundary from the same discrete ownership
used by SegmentIndex: `floor(TotalSegmentProgress + BoundaryTolerance) + 1`.
It rejects an uninitialized/nonfinite position, nonfinite or negative distance,
accumulated distance overflow, and a non-representable/non-forward transition.
It has no status, lane, speed, time, surface, force or RNG responsibility.

Both fixed/normal `ResolveRiderCore` and moving/controlled `ResolveMovingRider`
call it only after a non-Crash traversal has returned successfully. Thus exact
position already exists in immutable RiderStateChange, before Commit. Straight,
TurnEntry, TurnMiddle and TurnExit, Brake and RunWide, legacy execution, controlled
safety alternatives, rich projection, lean projection and real heat execution share
this rule. General `Advance` remains the partial/terminal operation.

A partial crash still consumes `(1f - SegmentProgress) * .5f`, retains its actual
fractional endpoint, terminal status and zero passage wear. A post-traversal contact
crash retains its already completed traversal under the existing consequence rules.
`CommitRiderChange`, `CommitSoloProjection` and `RiderSnapshot.Apply` copy the exact
resolved position; none repairs it later. Lap completion and Finished classification
follow the existing derived lap count and required-lap check. Examples `.1f -> 1`
and `4 + .1f -> 5` are exact, including lap wrap and the final race segment.

## Physical execution and motion representation

The float canonical advance supplied to physical integration is preserved. Fixed
physical distance still uses the same float length-times-remainder multiplication;
moving ExecutedPathTraversal retains its original double internal end, steps,
local surface samples, forces, lateral solve, distance and time accumulation.
No extra remainder integration, motion substep, physical travel or wear is added.
DistanceMeters adds only the actual returned traversal distance in the existing
float arithmetic. Finishing a topological segment is not additional physical travel.

ResolvedRiderMotion already maps stored path/profile samples against incoming and
outgoing canonical state, and reconciles its terminal node with the resolved endpoint.
ExecutedPathNode stores segment progress as float; ordinary decimal full traversals
already expose terminal float progress 1. Interior source geometry, times, speeds
and distances are preserved; canonical observations use the corrected outgoing span.
The adapter's existing endpoint mapping is sufficient for ordinary decimal starts.
No ExecutedPathTraversal equation is modified.

One representation edge case required a narrow change. Immediately below a corner
boundary, corner-progress knots can round together and the float accumulated rider
clock can give resolved duration zero. Previously the sole origin node was overwritten
by the endpoint. When only that node remains, the adapter now appends the endpoint
at the same existing time. The incoming origin survives; the already executed tiny
distance and outgoing state remain represented. It does not rerun physics or add time.
This preserves SampleAtTime(0)'s origin contract and exposes the zero-time progress
change instead of hiding it. Width reinterpretations retain explicit EntryBoundary/
ExitBoundary metadata; consumers must not sweep between segment coordinate frames.

Regression checks enforce exact initial/final canonical values, next-motion origin,
shared clock boundary, no added time/distance, monotonic nodes, exact rich/lean state
and wear, and private speculative state. The consumed-segment final sample has
SegmentProgress 1; the resulting RiderPosition has SegmentProgress 0.

## Focused and historical exact evidence

The focused matrix covers `0`, `.1`, `.2`, `.4`, `.5`, `.9`, `.125`, `.375`, the
float immediately below 1 and a valid small positive position outside the existing
constructor tolerance; straight and all three corner phases, nonzero indexes,
middle laps, lap wrap and final finish. Additional regressions cover near-boundary
partial crashes, Brake/RunWide, controlled/legacy paths, repeated prefix reuse,
cold-versus-cached and serial-versus-parallel evaluation. Six seed-19 production
cases run all four collection-order/observer combinations in bounded CI. The full
342-case offline batch is not added to mandatory CI.

`tools/rea009-handoff-audit` builds the identical probe against main and the corrected
engine. All **104 single-segment traversals** preserve every captured physical IEEE
leaf: speed, lane/lateral, distance, elapsed time, outcome, morale, integration/path
and phase diagnostics, and final raw wear. Four partial crashes are entirely exact.
Forty-two cases have necessary canonical/motion representation changes; two of those
preserve a previously lost zero-duration corner origin. The comparator allows only
these explicit representation categories and rejects any physical difference.

The existing 108-case contested-performance capture preserves **104 complete
captures bit-exactly**. Four `four-separated` variants change only canonical position,
motion progress and newly correct boundary metadata. All **300 presence-aware typed
leaf deltas** are recorded in `tests/fixtures/rea009-historical-boundaries.json`.
No speed, distance, time, surface, contact, decision or recovery leaf changes. The
first differing canonical double is `4013FFFFFF400000 -> 4014000000000000`:
segment 4 completion now ends exactly at 5. Missing and null leaves are distinguished.
The fixture has a pinned SHA256; the comparator rejects omitted corrections, any
extra physical change, and altered missing/null semantics. Historical artifacts are
not regenerated. The original full 22-case/1,088-leaf platform map still has its
independent exact before/after check.

The existing physical-contact consequence golden is byte-exact after canonical LF
handling, SHA256 `5824cf1605266358502ff715e399f6f2428047aa80c8814aad21caad9ade90ce`.
The existing 850-case contact-decision audit completes. Pair-generation, recovery,
feature defaults/OFF, observer/order independence and global-state isolation remain
covered by the existing suite. All 39 legacy skill/style source consumers retain
the original hashes after extracting only the two pinned semantic handoff edits.
The REA-001 source extraction and native-B diagnostic portability fixture are unchanged.

## Original 18 production reproductions

Every row below has the original decimal inputs, `Failed=false` after correction,
zero audit violations and four Finished riders. Before correction every row has
`Failed=true` before first actual Commit. Normal/reversed collections, each with a
passive observer and with no observer, have identical exact final hashes. Diagnostic
reruns also preserve exact final state.

| Scenario | Seed | A | B | C |
| --- | ---: | --- | --- | --- |
| three-squeeze | 7 | complete | complete | complete |
| three-squeeze | 19 | complete | complete | complete |
| three-squeeze | 83 | complete | complete | complete |
| four-close-regain | 7 | complete | complete | complete |
| four-close-regain | 19 | complete | complete | complete |
| four-close-regain | 83 | complete | complete | complete |

All 72 riders finish. The `.125f`/`.375f` companions remain separate unchanged
catalog entries. No failing input is replaced with a binary-friendly fraction.

## Full offline readiness recheck

The fresh offline batch uses 27 race scenarios and 33 controlled sweep settings,
A/B/C, seeds 7/19/83 for regular scenarios: 342 evidence cases. The capture directory
is new; no older resume evidence is used. Its production implementation SHA is the
one above. A completed diagnostic batch and completed heats are counted separately.

**Final offline metrics are being collected; see the final evidence update.**
The frozen baseline is 322 completed / 20 failed (two REA-001 and eighteen REA-009).
The recheck separately inspects failures, order parity, finish classification,
segment/lap progression, motion reconciliation, contacts, uncertified footprints,
observed racing and performance. No remaining issue is suppressed to obtain a
342/342 claim.

## Validation and portability

Release restore/build uses warnings as errors and has zero warnings. The complete
local .NET run passed 2,474 cases with one obsolete known-bug expectation; its
replacement passed separately. Additional focused regressions are run after that
update. CI executes the complete final test discovery with one shard per case.
The Python suite includes extraction and strict historical-comparison rejection tests.

**Final counts and Windows/Ubuntu results are being collected.** CI retains historical
strict goldens, contact-decision/consequence audits, exact platform-map preservation,
REA-001 native-B evidence and adds the bounded original decimal matrix. New repaired
cases compare complete FinalHash and BehaviorHash without numeric tolerances.

## Same-machine performance

The offline benchmark records standard four-rider heat, contact-heavy heat, original
fractional rolling heat plus its binary companion, rich trajectory, lean cached
trajectory, fixed-line control and decimal single-segment execution. Wall clock and
process-wide allocations include branch workers. Each successful case warms for at
least five runs and two seconds, then takes nine samples. Timed decisions retain
normal degree up to four. One separate untimed run uses the existing synchronous
capture hook for exact work/cache counters (degree one); it is not part of production.

The unchanged engine cannot complete the original decimal rolling heat; its abort
must not be treated as a faster completed baseline. The binary companion supplies a
completing fractional control. No additional integration or full simulation pass is
introduced by the production completion rule.

**Final benchmark measurements are being collected.** Any median slowdown over 10%
is investigated before acceptance; allocations and resolution/node/cache/contact
counts are compared as well. #61 source optimizations and defaults are unchanged.

## Remaining readiness work and review boundary

REA-009 addresses exact topological completion only. It does not calibrate physical
forces, grip, incident frequency, severity or recovery. Remaining scopes from the
frozen backlog include default consequence migration (REA-002), cohort calibration
(REA-003), impact/recovery calibration and endpoint timing (REA-004), venue geometry
interpretation (REA-005), canonical ability consumption (REA-006), and the recorded
behavior/geometry evidence gaps. Uncertified full-bike geometry is reported separately
from proven track departures. Portability issue #62 remains independent.

Keep PR #65 draft and unmerged. Stop for independent code review after complete
validation and evidence are recorded. PR #49 and issue #62 are not modified.
