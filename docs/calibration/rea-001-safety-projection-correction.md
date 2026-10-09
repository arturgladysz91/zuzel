# REA-001: optional safety projection feasibility

**BINDING failure boundary; unchanged PROVISIONAL physics/balance.** Baseline is
merged readiness audit main `350f59cc53184399066bdf93b18341e4628ec8ef`.
This correction addresses REA-001 only. REA-009 and portability issue #62 remain
separate. The original readiness catalog, reports, compressed captures and all
historical goldens remain unchanged.

## Original reproduction and traced request

Fresh `repro` captures of `outside/7/B` and `outside/7/C` both have `Failed=true`,
zero completed production steps and unchanged pre-Commit rider/surface state.
The diagnostic process exits successfully because it saved the failure; it did
not complete a race. Reverse-input and unobserved runs reproduce the same failure.

Temporary observation followed by rethrow identified **rider 1 / gate A**, safety
`BackOut`, Entry/Apex/Exit **0/0/0**, drive fraction **0** (`float:00000000`),
hold lateral position **true**, heat 91, seed 7, step 0, segment 0. Initial speed
and elapsed time are zero; lateral position is `float:3E4CCCCD` (0.2).
The error is `CoreSim.ExecutedPathTraversal+TimeSolveDiscontinuityException`:
`Coupled time solve did not converge within its bound.`

Original call chain, preserved in the separate [exact evidence](rea-001-safety-projection-changes.json):
`Solve` (original traversal line 263) → `Traverse` (95; subdivision budget
exhausted) → `ResolveMovingRider` (engine 785) → `ResolveRiderCore` (618) →
`ResolveProduction` (267) → coordinator `SafetyProject` (original 323).
The first selected actual production motion had already succeeded. No Commit
had happened. Only this hypothetical solo correction was unavailable.

Once that failure was rejected, tracing exposed a second unavailable request:
rider 2, `EmergencyAvoid`, **2/2/2**, drive **0**, hold **false**. The solver
generated infinite movement time, which previously escaped as an
`ArgumentOutOfRangeException` from `LateralMovementModel` validation. The new
guard classifies only non-finite time generated internally by `Evaluate` (including
overflow on conversion to its existing float movement input). It never catches
ordinary invalid arguments. An exhausted finite-root bracket is also explicitly
typed. No equation, iteration count, subdivision rule or tolerance changes.

## Feasibility, cache and search contract

`TimeSolveDiscontinuityException` is internally accessible under the narrow
`TimeSolveFeasibilityException` contract. `NonFiniteTimeSolveException` describes
the separately traced non-finite/bracketing failure. The optional safety cache
catches only this contract. It returns `Resolved` with the actual motion/poses
and track/horizon certification results, or `InfeasibleProductionProjection`
with a classified failure type and **no motion or certificate**.

The per-evaluation cache uses the existing physical `ProjectionKey`: rider ID,
trajectory intent, drive fraction and hold flag. Labels/reasons are excluded.
Both successes and failures are cached; duplicate failed requests never rerun
the solver. There is no heat-global or process-global cache. Production resolution
counts include failed attempts; cache hits and frozen-motion reuse are separate
optional operational observations, not domain state.

Eager projection, pair precomputation, joint feasibility and scoring consume the
same explicit result. A joint containing an unavailable rider is rejected before
pair geometry or cost calculation. Its FullAudit diagnostic has null separation
and null cost, rather than invented clearance or zero-cost motion. The rejection
names `Infeasible optional production safety projection`, rider and failure type.
Resolved candidates retain distinct overlap, boundary/coverage, outside-track and
incomplete/crashed-horizon rejection reasons. The 3-choice/81-joint bound, canonical
ordering, safety priority and tie-breaking remain unchanged. Production pose and
pair-cache arithmetic, decision costs and RNG addresses/calls remain unchanged.

The candidate diagnostic's cost/separation are now nullable because an unavailable
trajectory has neither value. Previously available values serialize identically.
All 108 pre-existing local typed captures remain exact, including FullAudit data.

## Actual fallback and state ownership

Selection remains certified clear joint response, then the existing genuinely
verified emergency attempt under its unchanged constraints, then frozen actual
requests with real unresolved contact. When cached failures leave no replacement,
exact physical equality with the frozen requests permits retaining the successful
first execution and its #55 observation. No rider, clock, surface or tracker was
committed during search. The poses, wear, addressed incident outcome and pair history
are the actual ones already calculated. A diagnostic explicitly records that no
feasible correction was found and actual motion was retained.

Contact is never declared clear on that basis. Existing final #55 checks still
determine `ResolvedWithoutMechanicalContact`. B preserves originating legacy
allowances and authorized pairs, including the unchanged legacy consequence
effects. C uses actual #55 contacts, pair generations, exactly-once impulses and
one-next-active-step recovery. Final consequence materialization and one Commit
retain their existing ownership. Hypothetical failures cannot mutate real state,
apply wear, consume generations or apply recovery.

Original mandatory production failures still escape. A selected changed safety
joint must undergo required final production execution, whose errors also escape;
this PR adds no catch there. Retaining earlier motion after a failed changed joint
execution would need a separate causal proof and is not implemented.

## Completed production outcomes and exact differences

Fresh corrected heats have `Failed=false`. Both happen to have four finishers;
that is observed output, not a guarantee or an acceptance rule that forces finishes.

| Configuration | Actual classification (rider: elapsed seconds) |
| --- | --- |
| B | 2: 60.89192; 4: 61.35127; 1: 61.47685; 3: 61.59118 |
| C | 1: 61.60596; 4: 61.86267; 3: 61.864933; 2: 62.03843 |

B retains one authorized legacy attempt. C applies four rider consequences across
two actual pair contacts: 1–2 on step 0 at common time 1.5544269525721575, and
3–4 on step 7 at 14.435613423585892. Riders 1/2 receive `MajorSave` with actual
next-step recovery. Rider 2's endpoint speed changes 23.766573 → 23.717918 m/s;
rider 1 has real lateral impulse/disturbance with unchanged scalar endpoint speed.
Riders 3/4 receive `Brush` (21.588781 → 21.635254 / 21.542198 m/s). No legacy
fallback applies in C. No crash is fabricated or suppressed.

The before side has no classification or completed heat to compare with. The
focused JSON preserves every completed final rider/surface/classification typed leaf
(logs are covered by FinalHash and complete CI traces) and lists **199 B / 198 C**
changed shared rider/surface leaves against the real failed pre-Commit state,
including exact IEEE bits. Classification, positions, clocks, surface wear,
events and contact/recovery histories now exist because production continues.
FullAudit also gains explicit infeasibility rejection rows. Successful existing
cases are not silently recaptured: six smoke cases, contact-heavy C and the
108-case preservation sample retain exact baseline final behavior. More specific
safety rejection reasons and nullable unavailable diagnostic values are intentional
schema clarification; frozen evidence remains original.

## Validation and CI contract

Focused tests cover B/C completion, exact reversal and observer independence,
real physical consequences, duplicate natural failures, all physical key inputs,
mixed success/failure selection, unexpected exceptions, all-corrections-failed
fallback/ownership and fatal required execution. Existing stable squeeze,
connected-frontier, emergency, failed-clearance, recontact, simultaneous-impact
and sequential-recovery tests remain in the complete suite. Decimal REA-009
fixtures remain unchanged and deliberately fail for their existing reason.

Feature OFF `outside/7/A` and the six-case smoke matrix are exact against main,
including classification, logs, final riders and every surface cell. The local
108-case audit compares 3,874,319 leaves, 3,121,627 typed IEEE leaves, with exact
input-order parity. Complete Python tests additionally reject new corrected
platform bit/type divergences. Release builds use warnings as errors.

CI keeps nine jobs and every older strict platform/golden comparison. The old
readiness `src`-unchanged guard belonged to an observation-only PR; it is replaced
by a frozen-data/audit-evidence guard for this authorized production correction.
Its six-case exact baseline comparison remains. New fresh per-OS before/after
outside A/B/C captures verify original failures, A preservation and B/C completed
reversal/observer parity. Corrected B/C additionally require complete Windows/Ubuntu
typed FullAudit trace equality. The unchanged contested-performance comparator
still requires the original **22 cases / 1,088 leaves** divergence map to remain
identical, with no rounding, tolerance or excluded cases. Final-head 9/9 results
are linked from the Draft PR after the run completes.

The new CI adds three unique outside cases; it does not run the 342-case offline
matrix. Local benchmark work is separate: four populations × six executions
(one warmup and five measured) per implementation, 48 heats total, including
the original aborted REA-001 attempts. No extended offline matrix was rerun.

## Same-machine performance

Windows 10 x64, .NET 8.0.26, one warmup and median of five fresh runs. Counter-only
baseline instrumentation records and rethrows the original exception; it changes
no outcome. Counter observations occur only in the separate warmup. Timed runs
have no materialization observer and exclude final hashing. Other local tests ran
on this machine, so timing is observational and speedup percentages are not claims
about isolated CPU performance. The work/allocations below are more useful here.

| Population | Before → after median ms | Before → after allocated bytes |
| --- | ---: | ---: |
| Normal Motoarena C | 4062.500 → 3899.641 (−4.01%) | 283301528 → 283316640 (+0.0053%) |
| Contact-heavy C | 700.671 → 615.268 (−12.19%) | 78826840 → 78743368 (−0.106%) |
| Outside/7/B | 140.825 abort → 5227.172 completed | 13486344 partial → 352263896 completed |
| Outside/7/C | 151.205 abort → 5909.850 completed | 13487616 partial → 399504016 completed |

Normal work stays 626 production resolutions / 3,858,880 narrow-phase evaluations,
with no safety pass. Contact-heavy work stays 99 / 347,505, eight unique safety
requests (four frozen reuses, four solver attempts), zero failures. Its redundant
cache lookups fall 2056 → 436; pair checks and physical outputs remain exact.
No successful-population slowdown exceeds 10%; the allocation difference is small.

Both original outside attempts stop after two unique safety requests (one frozen
reuse, one failed solver attempt), 43 total production resolutions and 112,906
narrow-phase evaluations. Completed B uses 12 unique safety requests, eight solver
attempts/eight cached failures, 396 hits, 1,114 production resolutions and 6,098,677
narrow-phase evaluations. Completed C uses 18 requests, ten solver attempts/eight
cached failures, 834 hits, 1,054 resolutions and 5,993,626 narrow-phase evaluations.
The increased total is a complete heat versus an aborted first step, so no
end-to-end slowdown percentage is meaningful. Known failures are not retried.

Reproduction and report generation are in [the focused audit README](../../tools/rea001-safety-audit/README.md).

## Remaining blocker

REA-009's decimal partial-segment canonical handoff and its readiness fixtures are
untouched. `InvalidOperationException: Rider 1 is not in segment 1.` is outside the
typed optional solver contract and remains visible. This PR neither fixes it nor
changes tolerances, catalog inputs, physics/defaults, issue #62, PR #49 or PGEE data.
Keep the new PR Draft and stop after final-head CI for independent code review.
