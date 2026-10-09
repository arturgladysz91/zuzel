# REA-009 â€” exact canonical segment-boundary handoff

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

The fresh batch completes **342/342 production heats, zero failures**, in
1,547.9498504 seconds (25 min 48 s). Each configuration contributes 114 cases.
All 342 reverse-order comparisons are exact; the three batch observer representatives
are exact, in addition to the full four-way observer/order checks for all 18 original
REA-009 reproductions. The frozen baseline is 322 completed / 20 failed (two REA-001
and eighteen REA-009). All **322 previously completing cases retain both exact
FinalHash and BehaviorHash**. All 20 former failures now complete.

There are zero invariant witnesses, zero distance/clock accumulation residuals in
46,277 exact checks, and zero duplicated applied pair generations. Finish status is
1,338 Finished / 30 Crashed / zero Racing, Retired or NotStarted across 1,368 riders;
a completed heat does not mean every rider finishes. The original 18 repaired heats
have 72/72 Finished riders.

| Configuration | Finished / Crashed | Contact events | Applied pairs / rider consequences | Uncertified footprints | Racing passes | Pass-coverage breaks |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| A | 447 / 9 | 36 | 0 / 0 | 149 | 521 | 712 |
| B | 454 / 2 | 1 | 0 / 0 | 131 | 633 | 774 |
| C | 437 / 19 | 57 | 32 / 57 | 127 | 590 | 728 |

These populations include controlled sweeps and extreme stress; the detailed report
keeps them distinct from ordinary standing-start and rolling racing. Uncertified
footprints are geometry-certification gaps, not proven track departures. Pass-coverage
breaks remain explicit; no overtake is inferred across them. The existing endpoint
impact abstraction retains 0.14789264505270694–3.222980499267578 s application delays.
Those are remaining modeling/evidence deficiencies, not handoff failures.

New evidence is separate from the historical folder:
[full summary](rea-009-evidence/summary.json),
[342 compact cases](rea-009-evidence/cases.json.gz),
[18 reproduction classifications and hashes](rea-009-evidence/reproduction-summary.json),
and [complete typed physical/representation differences](rea-009-evidence/physical-comparison.json).
The raw batch and compact-case SHA256 values are recorded in the summary. The batch
was executed at the production implementation commit; `git diff 7285430 HEAD -- src tools/race-engine-readiness` is empty.
The production `src` tree is `4d6ced758d082af8950e24afc1301ae685baf64c`. Later regression/comparator/report changes
therefore use identical production and readiness-capture code.

## Validation and portability

Release restore/build uses warnings as errors and has zero warnings/errors.
All **2,480 .NET cases pass** in CI at `66fccfd6df65c8b71a52099def3068e2d14ddd37`:
core 1,564; trajectory 395; historical-analysis 473; four-rider 48. Exact discovery
proves every case has one shard, with zero missing, extra or duplicate cases. There
are 266 new focused handoff cases. The obsolete `.05f` expected-domain-failure test
is replaced with successful exact serial/parallel decision equivalence, rather than
removed. The final local focused run passes all 267 cases including that replacement.
The complete final local run also passes **2,480/2,480**, zero skipped or failed,
in 13 min 16 s. [TRX counters, red-regression messages and validation provenance](rea-009-evidence/validation.json)
are saved separately. All **78 Python tests pass**, including strict historical-delta rejection and legacy
source-extraction checks. The four original red regressions were captured before
the production change.

Windows/Ubuntu captures at `66fccfd6df65c8b71a52099def3068e2d14ddd37` preserve the
existing **22 divergent cases / 1,088 typed leaves exactly**, including their complete
before/after maps. Both platforms verify all 108 historical captures, with exactly
the same four-case / 300-leaf REA-009 topology delta. The 850-case contact-decision
comparison has zero cross-platform differences before or after; its existing 26-case /
13,554-leaf sequential-recovery change remains identical. Physical-contact consequence
and other historical strict comparisons pass. REA-001's strict C trace, final B/C
states, and separately pinned 121 native-B diagnostic leaves remain unchanged.

For the newly repaired cases, all **six final production states are exact**, and all
four **A/C complete typed traces and behavior hashes are exact**. Native B retains the
existing native geometry arithmetic when physical consequences are OFF. The previously
aborted rolling heats now expose 91 (`three-squeeze/19/B`) and 121
(`four-close-regain/19/B`) differing double diagnostic leaves. All 212 differences are
inside `Interaction.Episodes`: candidate separation, geometry and closing attribution.
Every other complete trace leaf, including actual resolved position, time, speed,
distance, motions, decisions, events, raw surface and final state, remains exact.

The first chronological differences are:

| Case | Typed path | Windows double bits | Ubuntu double bits |
| --- | --- | --- | --- |
| three-squeeze/19/B | Steps[4].Interaction.Episodes[0].Candidates[0].MinimumSeparationMeters | 3FDCAF37FDB257E8 | 3FDCAF37FDB25868 |
| four-close-regain/19/B | Steps[1].Interaction.Episodes[0].Candidates[3].MinimumSeparationMeters | 3FF33B4159B27D20 | 3FF33B4159B27D40 |

Source routing is unchanged: `TrackMetricEmbedding` and `ContactFrameArithmetic`
retain native `Math.Sin`, `Math.Cos` and `Math.Atan2` for B, and the existing deterministic
frame for C. This is newly observable downstream evidence after the early abort is
removed, following the established REA-001 native-B contract; it is not evidence of
fully portable B diagnostics. The initial all-hash assertion correctly failed and
prompted complete typed inspection. No physics or numerical portability implementation
was changed to hide that finding.

The new separate [212-leaf native-B map](../../tests/fixtures/rea009-native-b-portability.json)
is SHA256-pinned with exact per-OS behavior hashes. The comparator requires the entire
case/path/type/value/IEEE map and rejects missing deltas or any extra executed-state
change. A/C full traces and all six final states remain strictly exact. There are no
rounded values, tolerances or excluded diagnostic fields.
[Complete platform comparison](rea-009-evidence/platform-comparison.json) records all
six trace sizes, hashes and differences. Existing historical artifacts, the old map,
REA-001 pins and issue #62 are untouched. The final CI rerun uses this precise contract.

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

Same machine: Windows 10.0.19045, .NET 8.0.30, 16 processors; baseline
`cd19c399dfc97d7abbd2aca03d50aa2c94a76262`, identical corrected production implementation.
The complete offline audit and other local captures had finished before timing.

| Case | Before median ms | After median ms | Change | Before / after median allocated bytes |
| --- | ---: | ---: | ---: | ---: |
| balanced-motoarena | 425.9195 | 426.1486 | +0.054% | 187,958,768 / 187,958,000 |
| contact-heavy | 342.1281 | 343.7371 | +0.470% | 78,429,416 / 78,429,480 |
| original three-squeeze | abort | 263.1964 | no valid ratio | — / 122,554,248 |
| three-squeeze-binary | 265.0123 | 264.3609 | −0.246% | 122,415,344 / 122,414,512 |
| fixed-line | 3.1880 | 3.2063 | +0.574% | 2,461,064 / 2,461,064 |
| rich-trajectory | 11.4310 | 11.4899 | +0.515% | 4,055,976 / 4,055,976 |
| lean-cached-trajectory | 9.6636 | 9.6301 | −0.347% | 1,494,792 / 1,494,792 |
| decimal-single-segment | 0.0447 | 0.0444 | −0.671% | 35,824 / 35,872 |

No comparable median slowdown exceeds 10%; the largest is 0.574%. These are nine-sample
observations, not a statistical speedup claim. Every comparable case has identical
counted production/solo resolutions, candidate traversals, prefix hits, motion samples,
committed segments and contact evaluations.

| Case | All physical resolutions / solo projections | Candidate traversals / prefix hits | Motion samples | Committed segments | Contact evaluations |
| --- | ---: | ---: | ---: | ---: | ---: |
| balanced-motoarena | 9,500 / 9,356 | 3,376 / 6,008 | 5,624 | 144 | 0 |
| contact-heavy | 3,704 / 3,562 | 1,478 / 2,436 | 2,465 | 66 | 347,505 |
| original three-squeeze (after only) | 6,880 / 6,752 | 2,816 / 4,544 | 4,701 | 128 | 0 |
| three-squeeze-binary | 6,880 / 6,752 | 2,816 / 4,544 | 4,689 | 128 | 0 |
| fixed-line | 115 / 0 | 0 / 0 | 5,040 | 115 | 0 |
| rich-trajectory | 93 / 93 | 35 / 82 | 6,847 | 0 | 0 |
| lean-cached-trajectory | 93 / 93 | 70 / 257 | 0 | 0 | 0 |
| decimal-single-segment | 1 / 0 | 0 / 0 | 33 | 1 | 0 |

`SynchronousResolutions` in the report counts actual physical evaluations during the
separate synchronous counter run, including solo projections. `MotionNodes` counts
the existing recorded motion-sample materializations for committed/returned rich motions.
It is not an integrator-step count. The decimal single-segment's +48 allocated bytes
match the newly applicable `MotionBoundaryTransition` record (six stored fields and
object overhead); before completion its ExitBoundary was absent. Traversal diagnostics,
33 motion samples, physical distance, time and wear remain exact. The tiny process-wide
allocation variations in threaded heat cases accompany exact work counts.
[Raw warmups, nine samples, allocations and work counters](rea-009-evidence/benchmarks.json)
remain available. #61 source optimizations and defaults are unchanged.

## Remaining readiness work and review boundary

REA-009 addresses exact topological completion only. It does not calibrate physical
forces, grip, incident frequency, severity or recovery. Remaining scopes from the
frozen backlog include default consequence migration (REA-002), cohort calibration
(REA-003), impact/recovery calibration and endpoint timing (REA-004), venue geometry
interpretation (REA-005), canonical ability consumption (REA-006), longer-horizon opponent strategy (REA-007), and richer exposure/intent telemetry
(REA-008). The recorded behavior/geometry evidence gaps remain visible. Uncertified full-bike geometry is reported separately
from proven track departures. Portability issue #62 remains independent.

Keep PR #65 draft and unmerged. Stop for independent code review after complete
validation and evidence are recorded. PR #49 and issue #62 are not modified.
