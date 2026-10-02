# Production-backed trajectory intent evaluation

Audited main: `3f4caf599f3e0539ab0c39f058b98d80b9798f54` (squash #53). Historical #39–#53 evidence is unchanged. This is current solo evidence, not a new physical calibration.

## Architecture and semantics

Before: AdaptiveDecisionModel ranked one anchor with a static route-length / perceived settled-safe-speed ratio. Current speed, achievable path, correction/carry/drive and straight payoff were absent. After: bounded Entry/Apex/Exit intents replay exact current rider and raw-cell surface copies through the ordinary SimulationEngine CaptureSnapshot → Decide → Resolve → Commit. Scripted targets prevent recursion. Elapsed time and distance are sums of canonical ResolvedRiderMotion totals, including heat-clock float reconciliation. Endpoints, speeds, outcomes and anchor arrival are read from actual production resolutions. No prediction physics or static fallback exists.

Entry applies on every pre-corner Straight piece and TurnEntry. Apex is the TurnMiddle intent, not a guaranteed position at CornerProgress 0.5. Exit applies on TurnExit and is held on all pieces of the immediately following logical straight. Horizon uses CornerTopology membership plus topological lap progress; it stops before a second logical corner or at race finish. The split Motoarena home straight crosses lap wrap correctly without segment-id special cases. A straight-only track continues to finish; absent future/past phases have null observations. There is no persistent RiderState plan and only the current phase target executes.

Grammar: Entry 0..4, Apex in clamped/deduplicated Entry±1, Exit in Apex±1: exactly 35 full candidates, 13 remaining Middle/Exit, 5 Exit candidates. All five holds and 3/2/3, 0/1/2, 4/3/2 occur. Completed phases collapse to canonical tuple placeholders; they are never claimed as achieved history. Anchors constrain planning, not continuous movement.

Each candidate gets fresh mutable rider and surface copies, a private disabled log and IncidentFrequency=0. Deterministic braking, RunWide, inability to reach targets and terminal crash remain active. Incomplete deterministic crash routes retain measured partial time/distance but have infinite selection cost. Own passage wear is committed privately; future weather/other riders' passages are not forecast. Equal scripted prefixes reuse immutable ResolvedSimulationStep results within one evaluator only; cold, repeated and reversed replay parity tests prove identical paths/outcomes/winners. There is no global mutable cache. Immutable Track/topology can be shared safely.

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

35/35/13/5 candidate traversals per Straight/Entry/Middle/Exit decision; repeated exact scripted prefixes reduce production resolutions (counts above). Every candidate still starts from the same immutable snapshot and executes private commits. Runtime/allocation measurements are recorded in the PR; they are observations, with no CI timing gate. The bounded replay remains materially more expensive than the old static projection.

Desktop Release, .NET 8.0.30, 5 warm-ups then 15 four-lap runs per control: one rider median 531.656 ms, mean 540.513 ms, approximately 221.090 MB allocated/run; four-rider heat median 2151.049 ms, mean 2157.824 ms, approximately 879.587 MB allocated/run. Solo executes 844 candidate traversals / 2339 production segment resolutions; four riders execute 3248 / 9119 in this existing-contact fixture. All measurements use incidents OFF and logs OFF; existing contact still operates. These are cumulative allocations rather than retained/peak memory. Prefix reuse and immutable topology sharing reduced the initial approximate 313/1241 MB observations to 221/880 MB; no candidates, physics or winners were pruned for speed. Allocation cost remains material and warrants follow-up if batch throughput is insufficient.

## Remaining boundaries

production-backed trajectory evaluation is now solo-physical; traffic feasibility and contested-space constraints remain the next subsystem.

No common-time contested-space, traffic feasibility, motorcycle/rider footprint, passing/yielding/blocking/defence or global XY is implemented. Existing occupancy/contact remains provisional. MotionBoundaryTransition still reinterprets segment-local width coordinates; no offset sweep, transition spline or fake boundary distance is added. Combined grip/slip remains uncalibrated and outside production; no q/friction-circle tuning. Longitudinal constants remain provisional; no banking, asymmetric detailed Motoarena shape, RPM/clutch/wheelspin or real-data drive recalibration. The major trajectory-decision consistency blocker is resolved within current production physics; these remaining geometry/interaction/calibration limits remain.
