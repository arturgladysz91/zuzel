# Production-backed trajectory intent evaluation

Audited main: `3f4caf599f3e0539ab0c39f058b98d80b9798f54` (squash #53). Historical #39–#53 evidence is unchanged. This is current solo evidence, not a new physical calibration.

## Architecture and semantics

Before: AdaptiveDecisionModel ranked one anchor with a static route-length / perceived settled-safe-speed ratio. Current speed, achievable path, correction/carry/drive and straight payoff were absent. After: bounded Entry/Apex/Exit intents use the shared SimulationEngine ResolveRiderCore with isolated state and scripted phase targets. Normal production uses Full materialization; hypothetical ranking uses Lean materialization. Elapsed time and distance are the exact same canonical production totals, including heat-clock float reconciliation. Endpoints, speeds, outcomes and anchor arrival are read from actual production resolutions. No prediction physics or static fallback exists.

Entry applies on every pre-corner Straight piece and TurnEntry. Apex is the TurnMiddle intent, not a guaranteed position at CornerProgress 0.5. Exit applies on TurnExit and is held on all pieces of the immediately following logical straight. Horizon uses CornerTopology membership plus topological lap progress; it stops before a second logical corner or at race finish. The split Motoarena home straight crosses lap wrap correctly without segment-id special cases. A straight-only track continues to finish; absent future/past phases have null observations. There is no persistent RiderState plan and only the current phase target executes.

Grammar: Entry 0..4, Apex in clamped/deduplicated Entry±1, Exit in Apex±1: exactly 35 full candidates, 13 remaining Middle/Exit, 5 Exit candidates. All five holds and 3/2/3, 0/1/2, 4/3/2 occur. Completed phases collapse to canonical tuple placeholders; they are never claimed as achieved history. Anchors constrain planning, not continuous movement.

Each candidate starts from the same immutable rider/surface root, with IncidentFrequency=0. A typed prefix graph owns detached branch surface states and compact immutable rider states. Deterministic braking, RunWide, inability to reach targets and terminal crash remain active. Incomplete deterministic crash routes retain measured partial time/distance but have infinite selection cost. Own passage wear is committed privately; future weather/other riders' passages are not forecast. Equal target-history prefixes execute once within one evaluator and inherit their exact parent state; cold, repeated and reversed replay parity tests prove identical paths/outcomes/winners. There is no global mutable cache. Immutable Track/topology can be shared safely.

Perceived raw Grip/Ruts/Moisture cells use existing TrackObservation with seed, heat, decision step, rider, segment index, lane and model seed. Segment index is newly added to addressing; noise amplitude 0.09 and ruts factor −0.5 are unchanged, moisture remains observed unchanged. Perfect reading removes all noise. Normal production interpolation follows over those isolated raw cells. Every candidate sees the same observations.

Total cost = measured production time + style + behavioral lane-change reluctance + provisional current-target occupancy + existing surface/risk preference. Style, occupancy and risk coefficients remain 0.035, 0.30, 0.06/0.12/0.08. Style/risk/occupancy judge the current target (risk samples the upcoming corner when present); behavioral demand sums remaining requested phase changes at existing 0.025 × (1−LaneChangeTendency). The old independent 0.015 movement surcharge is removed because movement/path distance already costs physical time. OutsidePreference supplies no speed bonus. Selection orders total cost, physical time, requested change, then canonical E/A/X; enumeration order has no role.

## Fixed controls B/C/H

Same rider/setup/surface/22 or 19 m/s entry within each group; fixed lanes start at their own anchor as in #51 C. Each row independently runs the normal production engine for the same corner and following straight, with private wear and no weather change/incidents/traffic. The historical static formula is analysis-only; H's coarse single TurnEntry bend deliberately retains its historical topology difference.

| control | lane | old static s | production s | independent s | distance m | corner exit m/s | following straight s | straight end m/s |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| B | 0 | 7.126222 | 6.509595 | 6.509595 | 135.398226 | 20.958303 | 2.611304 | 23.601597 |
| B | 1 | 7.186339 | 6.960396 | 6.960396 | 144.823002 | 21.172218 | 2.583241 | 25.033272 |
| B | 2 | 7.261232 | 7.419580 | 7.419580 | 154.247784 | 21.380306 | 2.565146 | 25.182854 |
| B | 3 | 7.346338 | 7.877954 | 7.877954 | 163.672565 | 21.582802 | 2.547755 | 25.305010 |
| B | 4 | 7.438599 | 8.335518 | 8.335518 | 173.097336 | 21.779963 | 2.531008 | 25.424627 |
| C | 0 | 7.325893 | 6.382229 | 6.382229 | 135.398226 | 20.809408 | 2.611121 | 23.317865 |
| C | 1 | 7.243582 | 6.473121 | 6.473121 | 144.823002 | 22.250498 | 2.472651 | 25.151649 |
| C | 2 | 7.173513 | 6.614922 | 6.614922 | 154.247784 | 23.656969 | 2.355094 | 26.972471 |
| C | 3 | 7.111012 | 6.933611 | 6.933611 | 163.672565 | 24.276287 | 2.305455 | 27.582937 |
| C | 4 | 7.053158 | 7.322237 | 7.322237 | 173.097336 | 24.524996 | 2.283192 | 27.840160 |
| H | 0 | 15.062878 | 6.317708 | 6.317708 | 135.398224 | 20.958303 | 2.611304 | 23.601597 |
| H | 1 | 15.604435 | 6.502701 | 6.502701 | 144.823006 | 22.060638 | 2.509142 | 25.033274 |
| H | 2 | 16.134684 | 6.723707 | 6.723707 | 154.247780 | 23.081598 | 2.424861 | 26.230412 |
| H | 3 | 16.652891 | 7.012663 | 7.012663 | 163.672562 | 23.818064 | 2.368042 | 26.698149 |
| H | 4 | 17.158976 | 7.418324 | 7.418324 | 173.097336 | 23.961094 | 2.357267 | 26.789940 |

C old rank: 4 → 3 → 2 → 1 → 0. Production-backed rank: 0 → 1 → 2 → 3 → 4. Independent actual rank: 0 → 1 → 2 → 3 → 4. Rankings are computed, not forced. Wider exits are faster but their longer path does not win these matched controls.

## B/H/F/G/J remaining-horizon decisions

Solo R2 uses the original audit input and TrackReading; occupancy is absent. Choices include unchanged external personality/risk terms. H starts at 0.60 of its single TurnEntry-labelled corner; the current Entry intent and following Straight Exit intent are observed, while no TurnMiddle target executes in that coarse fixture. F/G reverse lane surface quality; J changes geometry. No winner is an acceptance target.

| control | chosen intent | current target | physical s | distance m | exit speed m/s | straight payoff s | straight end m/s | candidates / actual resolutions |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| B | E0-A0-X0 | 0 | 9.139565 | 198.186745 | 20.824810 | 2.625312 | 23.482723 | 35 / 93 |
| C | E1-A1-X1 | 1 | 8.844208 | 207.533281 | 22.341400 | 2.466055 | 25.031776 | 35 / 93 |
| H | E2-A1-X2 | 2 | 4.277223 | 103.507427 | 23.834610 | 2.368515 | 26.676016 | 35 / 24 |
| F | E3-A3-X3 | 3 | 9.836572 | 221.541431 | 23.512547 | 2.400623 | 26.321743 | 35 / 93 |
| G | E0-A0-X0 | 0 | 9.104438 | 195.451223 | 21.172749 | 2.585998 | 24.390711 | 35 / 93 |
| J-tight | E0-A0-X0 | 0 | 7.574791 | 148.680529 | 18.325396 | 2.223516 | 20.336636 | 35 / 93 |
| J-wide | E0-A0-X0 | 0 | 11.282105 | 269.766762 | 24.204992 | 3.073938 | 27.600540 | 35 / 93 |

## Requested versus achieved Motoarena endpoints

All examples start at actual lateral 3 and 22 m/s. Middle end and actual 0.5-region sample are separate observations; null means unobserved. Arrival uses the existing physical 0.05 m lane-arrival tolerance.

| requested E/A/X | Entry end | Middle end | actual apex region | Exit end | total s | anchors reached at phase endpoints |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| E3-A2-X3 | 3.000000 | 2.041845 | 2.509830 | 2.951964 | 8.150259 | Entry=True, Middle=False, Exit=False |
| E3-A3-X3 | 3.000000 | 3.000000 | 3.000000 | 3.000000 | 8.295458 | Entry=True, Middle=True, Exit=True |
| E0-A1-X2 | 2.040565 | 2.000000 | 2.000000 | 2.000000 | 7.899579 | Entry=False, Middle=False, Exit=True |
| E4-A3-X2 | 4.000000 | 3.000000 | 3.467181 | 2.087523 | 8.417060 | Entry=True, Middle=True, Exit=False |

Intent is not guaranteed achieved path. No fictitious waypoint or boundary displacement is stored.

## Standing starts A/D

- A: exact gate center 0.200000, chosen E0-A0-X0; reaction 0.240000 s. Mid-reaction lateral 0.200000, physical distance 0.000000 m, progress 0.000000, speed 0.000000 m/s. Full horizon 9.650545 s.
- D: exact gate center 3.800000, chosen E2-A2-X2; reaction 0.240000 s. Mid-reaction lateral 3.800000, physical distance 0.000000 m, progress 0.000000, speed 0.000000 m/s. Full horizon 10.236804 s.

A/D normal-engine parity tests compare all resolved motions exactly, including reaction, achieved phase endpoints, exit and straight speeds. No gate-specific bonus or integer gate-position replacement occurs.

## Determinism, isolation and validation

Tests compare fixed/moving/partial/standing/lap-wrap projections with independently scripted normal production, including exact total time/distance, every motion node, phase endpoints, speeds and outcomes. Other tests cover reversed rider/candidate collections, equal-cost canonical ties, no live state/surface/time/log mutation, private wear, repeated/cold prefix replay, perfect/imperfect readers and seed/model-seed observations. B chooses a faster available route under neutral conditions; F/G and J change measured physics/choice without forced winners. Regenerate this report and JSON with `dotnet run --project src/Sandbox -c Release -- trajectory-intent-evaluation-report <scratch-directory>` and compare bytes. Historical blobs are separately protected.

## Performance

Broader replay exposed a #53 correction/drive switching-boundary step with no converged time root (A, seed 3, step 15). The shared production solver now halves that physical step and resolves the same primitives, up to 16 subdivisions, retaining the original tolerance and geometric node bound. Typed TimeSolveSubdivisions records recovery. No force, speed, correction or movement coefficient changes. Existing accepted #53 fixed-line hashes and moving evidence are checked separately.

35/35/13/5 candidate traversals per Straight/Entry/Middle/Exit decision; repeated exact scripted prefixes reduce production resolutions (counts above). Every candidate starts from the same immutable snapshot; each unique prefix commits own wear once to its detached branch state. Runtime/allocation measurements are recorded in the PR; they are observations, with no CI timing gate. The bounded replay remains materially more expensive than the old static projection.

Audited pre-performance HEAD `9ff730c5b1b2cfca739c3ead2ccc2760be1b7c71`: solo 531.656 ms / 221.090 MB cumulative allocations; four riders 2151.049 ms / 879.587 MB. Re-measurement on the same desktop/runtime/protocol before refactoring: 561.269 ms / 221.090 MB and 2308.697 ms / 879.587 MB. Five warm-ups and fifteen measured four-lap samples; incidents/logs OFF, existing contact ON.

## Production projection performance architecture

Full normal production still creates ResolvedSimulationStep, RiderStepDiagnostics, ResolvedRiderMotion, canonical metre observations, events and logs. Lean calls the exact same ResolveRiderCore, ExecutedPathTraversal loop, fixed-corner TraverseCore and longitudinal primitives, but returns only RiderStateChange, canonical elapsed time/distance, apex lateral and exact wear. It never creates full motion/path/node graphs, production events or formatted segment messages. Rich evaluation is explicit through Evaluate(intent, retainResolvedMotions: true), and uses a separate prefix graph. No extra rich replay runs during Decide.

Projection capture observes the same one-metre physical loop, solver equations, midpoint sampling, knot splits, tolerance and bounded subdivision recovery. Fixed straight/launch passes a null longitudinal observation collection; fixed corner uses no diagnostic nodes. Moving wear retains a bounded stack/value buffer of only (distance, midpoint lateral) until final distance is known: each original float distance/final-distance division, kernel addition and final scale/multiplication order is preserved. Full and Lean share the kernel and wear application. Streaming unnormalized weights then dividing their sum would alter rounding and is deliberately avoided.

A bounded typed target-child graph replaces string prefixes and replayed commits. Every unique prefix owns compact rider state, detached exact TrackState.Clone values, cumulative metrics and observations. Each executes once; shared parents remain unchanged. Counts stay 844 traversals / 2339 physical resolutions solo and 3248 / 9119 for four riders. Graph branches are not pruned. Normal public snapshots remain immutable copied values; profiling did not justify a copy-on-write contract change.

Stage C profiling found 1,488,987 corner-envelope creations and 25,819,453 repeated apex-anchored metre integrations inside projection, compared with 9119 input snapshots. Stage D reuses existing envelopes only for bit-identical lateral/raw-surface inputs within one physical segment, retaining the existing canonical speed integrator and surface sampling. Straight approach target reuse is likewise scoped to identical inputs in one snapshot. No physics formulas, integration resolution, coefficients, winners, observation noise or ties change.

| stage | solo median ms | solo allocated MB | four-rider median ms | four-rider allocated MB |
| --- | ---: | ---: | ---: | ---: |
| Frozen HEAD re-measured | 561.269 | 221.090 | 2308.697 | 879.587 |
| A: Lean result, no motion/events/logs | 504.377 | 182.520 | 2032.097 | 728.748 |
| B: compact path/corner/longitudinal capture | 434.659 | 136.738 | 1758.718 | 549.338 |
| C: typed prefix state graph | 436.482 | 136.465 | 1762.543 | 548.336 |
| D: exact immutable envelope reuse | 348.721 | 129.558 | 1356.639 | 498.016 |
| Final measured source | 342.626 | 129.577 | 1322.303 | 498.089 |

The first performance pass reduced allocation and replay overhead but did not achieve its original 4x target. That target is now a stretch observation, not an acceptance gate. The subsequent exact scalar/parallel pass and cross-platform audit are documented in [performance and CI follow-up](trajectory-performance-ci.md). PR remains Draft; each CI job retains a 30-minute limit.

Final scripted-only four-rider physical heat: 2.284 ms / 2.738 MB; Adaptive heat: 1322.303 ms / 498.089 MB. Per-decision Straight / Entry / Middle / Exit medians: 14.888 / 15.930 / 4.651 / 2.479 ms; allocations: 5.428 / 5.459 / 1.739 / 0.803 MB. Across fifteen measured runs, solo Gen0/1/2 collections: 116/0/0; four-rider: 447/208/9. These are cumulative allocations and collection observations, not peak RAM. No reliable peak/live-memory measurement is claimed.

Projection materialization counts for one representative four-rider heat: 9119 unique physical evaluations, 9119 SimulationSnapshot captures, 9119 input TrackStateSnapshot copies plus 138 perception snapshots, 0 RiderState copies, 9119 branch TrackState clones, 9119 private commits and 5836 prefix hits. Full ResolvedSimulationStep, diagnostics, motions, motion samples, executed paths/nodes/steps, longitudinal/corner observation nodes, events and formatted logs: all zero in projection. Baseline created 9119 full motions/events/formatted logs, 419335 motion samples, 279348 executed nodes and 272912 executed steps. Detailed baseline instrumentation lives outside the production path; an optional synchronous internal test observer accumulates nothing in normal runs.

Behavior JSON keeps Git blob `ad266b8861a7518fd62da28ccd7ed4c4eaeabd34`. The pre-refactor all-candidate cost/endpoint/outcome/arrival/choice trace, including seed/model-seed controls and a complete four-rider heat, keeps SHA-256 `F20AE8E3895CF54625D549DB127BA1C8E2168A7AA3549ED94F305A04D89C3D18`. Exact Full/Lean tests also compare every segment state and raw surface-cell bits across fixed/moving/continuous/edge paths, all gates/reaction, partial segments, lap wrap, split straights, RunWide, crash, correction and the existing subdivision fixture. Historical source guards normalize only explicit capture/clone support extraction, retaining their original protected physics digests.

Reproduce the offline benchmark with `dotnet run --project src/Sandbox -c Release -- trajectory-projection-performance-report <scratch-directory>`. The separate measured JSON is `trajectory-projection-performance.json`; it has timing/GC metadata and is not behavioral evidence. `trajectory-behavior-fingerprint <scratch-directory>` emits the exact behavioral trace. Real Adaptive integration, four-rider controls and explicit repeated/reversed trials remain covered; source/artifact hash checks do not trigger their own AI replay.

## Remaining boundaries

production-backed trajectory evaluation is now solo-physical; traffic feasibility and contested-space constraints remain the next subsystem.

No common-time contested-space, traffic feasibility, motorcycle/rider footprint, passing/yielding/blocking/defence or global XY is implemented. Existing occupancy/contact remains provisional. MotionBoundaryTransition still reinterprets segment-local width coordinates; no offset sweep, transition spline or fake boundary distance is added. Combined grip/slip remains uncalibrated and outside production; no q/friction-circle tuning. Longitudinal constants remain provisional; no banking, asymmetric detailed Motoarena shape, RPM/clutch/wheelspin or real-data drive recalibration. The major trajectory-decision consistency blocker is resolved within current production physics; these remaining geometry/interaction/calibration limits remain.
