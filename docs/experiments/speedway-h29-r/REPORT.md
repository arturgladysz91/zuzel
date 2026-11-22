# Study #207 — H29-R joint throttle and steering planning

H29-R is an opt-in controller experiment on the exact #206 parent `ea6aaf21debab9f977cec1ed300d0932b0eeef3f`. H29-R uses H28-F contact physics, proof, tyre and drivetrain parameters, 50 ms review, irrevocable activation queue and the same strict H28-F racing profile. H25 remains the default. The work prioritizes a complete strict trajectory and treats lap time as secondary. [H28-F baseline](../speedway-h28-f/REPORT.md) · [H28-C mechanics](../speedway-h28-c/REPORT.md)

## Frozen reference

The active H26 executable links `speedway-controller200/Receding198.cs` and `speedway-study201/Technique200.cs`. Training and strict replay use `Predictor198 → HandlingContact.Prepare`. The native adapter uses `TrafficWorld191 → HandlingRig → HandlingContact.Prepare` with the same `RacingProfile206` at accepted substeps. H29-R adds a feedback entry point to the predictor. It invokes the same review/activation path as tape replay. An eligible H29-R native run would use its frozen feedback policy and a synchronous 0.25 s strict guard at each real decision.

The #206 sources confirm **72,949 cases of archived physical parity and 16,200 fixed-load cases**, four H28-F MODEL decision passes with 3,760 decisions and zero >50 ms, and p99 36.675–37.099 ms. The progress-only experiment tried 441 nominal edits and selected five tapes that failed all five strict preflights. The deepest nominal attempt, #434, failed straight yaw with closed gas at 26.620 s, S = 502.002119 m. The #206 ledger was empty. [Frozen summary](../../../data/experiments/speedway206/summary.json) · [#206 results and locked recheck](../../../data/experiments/speedway206/final-policy-recheck.json.gz)

### Strict status of historical times

All recorded accepted substeps were examined with H28-F’s original technique and corridor observer from rest to finish. The archive also supplies the applied physical input and swept certificate for every step, allowing the original physical checks and recorded collision count to be verified without Box2D construction.

| Archive | Finish [s] | Min swept [m] | Contacts | Result of the strict archive audit |
|---|---:|---:|---:|---|
| H25, #202 | 46.386863 | 0.543118 | 0 | Pass on its recorded H25 physics and route |
| H27-N, #204 | 47.356525 | 0.534170 | 0 | Wide-direction at 10.930 s and correction at 11.050 s |
| H28-C, #205 | 46.988410 | 0.612236 | 0 | Wide-direction at 10.840 s and correction at 10.960 s |

H25’s strict archive is a valid benchmark on **H25 physics**. H27-N and H28-C retain historical finish times. This also shows why satisfying each official rule individually is an incomplete argument about H28-F’s trajectory. [Recomputed archive statuses](../../../data/experiments/speedway207/causal-and-phases.json.gz)

## Phase feasibility

Each straight is 62 m between a bend exit and the next entry. Its strict core is +12 to +54 m. Positive yaw of at least 0.25 rad/s must be sustained for 0.12 s during the approach from entry −30 to entry +8 m. That trigger overlaps the straight core for **22 m**. The same instant cannot satisfy both yaw predicates in the overlap, but the turn can begin in the **16 m exclusive part** after the core. The slide window extends from entry −22 to entry +70 m and needs |β| ≥6° for 0.12 s. Some of it overlaps the core. The β requirement can continue into the bend.

The technique observer records settlement after 0.12 s with |yaw| <0.10 rad/s and |course| <3°. After settlement it rejects a same-sign correction at |yaw| ≥0.16 rad/s when the duration reaches 0.12 s and the course swing exceeds 2°. Its monitoring window ends at entry −12 m, 4 m before the end of the straight yaw core. The course error limit in the core is independently 4°. Neither the start of settlement nor the trigger for a correction changes the hard steering limits. Pending makes a new request ineffective before its scheduled activation, with the regular 0.05 s delay and 0.05 s review cadence. The physical actuator is limited to ±35° and 90°/s, so one review interval permits at most 4.5° of movement after activation.

For a conservative lower bound on yaw manoeuvre time, the circle budget gives |Mz| ≤ max(lf, lr)·μmg = **1,620.8 N·m** at the archived grip. With Iz = 52.72097 kg·m² this implies an ideal lower bound of about **0.12065 s** to move from +0.23 to +0.25 rad/s and then sustain it. At exit the analogous ideal calculation uses the recorded yaw and the 0.10 rad/s settlement threshold. This assumes instantaneous use of the entire moment bound. It omits delay already covered by pending, sliding loss, course matching and the actuator. It is a necessary time bound rather than a reachable trajectory or an estimate of typical transition time. The recorded state-dependent front moment ranges below sweep reachable mid-step steering at the solved load. The corresponding rear moment is held at the local solved value, so these numbers bound a **local steering opportunity** rather than an independently available sum over time.

The measured changes give the yaw rate, body heading, velocity direction and β between core release and entry, or between exit and +12 m. The first number is in rad/s and the others in rad. Speed is at core release. The conditional rollout uses the archived physical state, the observed technique timers and pending, then applies the strict H28-F profile to the indicated phase. A conditional pass can start after an earlier historical violation and cannot certify that earlier prefix.

| Bend | Transition | Speed [km/h] or phase | Available [s] | Ideal minimum [s] | Δyaw / Δheading / Δdirection / Δβ | Strict local rollout |
|---|---|---:|---:|---:|---|---|
| 1 | Into bend | 65.34 | 0.870 | 0.12065 | +0.285 / +0.109 / +0.088 / +0.021 | Pass |
| 1 | Out of bend | Exit to +12 m | 0.516 | 0.13455 | −0.559 / +0.132 / +0.147 / −0.015 | Pass |
| 2 | Into bend | 88.08 | 0.740 | 0.12065 | +0.136 / +0.184 / +0.148 / +0.036 | Wide-direction |
| 2 | Out of bend | Exit to +12 m | 0.529 | 0.13269 | −0.305 / +0.165 / +0.238 / −0.073 | Pass |
| 3 | Into bend | 83.82 | 0.780 | 0.12065 | +0.472 / +0.169 / +0.118 / +0.051 | Wide-direction |
| 3 | Out of bend | Exit to +12 m | 0.531 | 0.12731 | −0.176 / +0.171 / +0.221 / −0.050 | Pass |
| 4 | Into bend | 87.01 | 0.760 | 0.12065 | +0.218 / +0.160 / +0.126 / +0.034 | Wide-direction |
| 4 | Out of bend | Exit to +12 m | 0.491 | 0.13725 | −0.419 / +0.221 / +0.261 / −0.040 | Pass |
| 5 | Into bend | 93.58 | 0.710 | 0.12065 | +0.206 / +0.179 / +0.130 / +0.048 | Wide-direction |
| 5 | Out of bend | Exit to +12 m | 0.509 | 0.13182 | −0.214 / +0.193 / +0.240 / −0.046 | Wide-direction |
| 6 | Into bend | 88.49 | 0.740 | 0.12065 | +0.148 / +0.169 / +0.135 / +0.034 | Wide-direction |
| 6 | Out of bend | Exit to +12 m | 0.476 | 0.13080 | −0.197 / +0.166 / +0.187 / −0.021 | Pass |

Front moment ranges in bend order are **343–347**, **581–598**, **571–589**, **530–565**, **596–622** and **535–569 N·m**. The matching rear moments are −313, −581, −534, −524, −583 and −541 N·m. The small difference between opposing moments matters more than either individual number. Near some entries the sign of the remaining moment can change within the available steering travel. Slide-window times span 4.33–5.15 s at the archived pace. The windows admit time for the required events. The measured failures result from trajectory and actuation timing. No phase in this calculation supplies a mathematical certificate of H28-F impossibility. [Phase budgets, states and conditional rollouts](../../../data/experiments/speedway207/causal-and-phases.json.gz)

## Causal reconstruction

The audit retains up to 3.20 s before each fault. It includes active/requested gas and steering, pending activation, physical steering and saturation, per-contact longitudinal and lateral force, loads, applied torque, yaw and β rates, course, clearances, wheel slip, shaft and wheel RPM, and the technique timers. The diagnostic intervention grid applies old-tape edits from the same recorded checkpoint. It distinguishes crossing the original fault marker from preserving a further continuation. For later faults on the permissive #205 tape, the already-invalid strict prefix remains invalid. Those probes diagnose position or swept only. No such trajectory can enter the planner’s archive.

| Fault | First change tested that crossed the fault | First tested edit with ≥2 m continuation | Local physical account |
|---|---|---|---|
| A, S 178.363 at 10.840 s | 7.650 s | None in the audit menu | Yaw +0.232037 crossed 0.23. Course was 1.55°, within 4°. Front +200 and rear −164 N·m left +35.83 N·m. |
| B, S 181.288 at 10.960 s | 7.750 s | 10.400 s, gas −0.22 and steer −0.016 rad | Settlement preceded a +yaw correction starting at 10.740 s. Duration was 0.22 s, the course swung 2.124° and yaw reached +0.322 rad/s. This audit isolates the correction after the earlier yaw fault. |
| C, S 502.002 at 26.620 s | 23.400 s | 26.050 s, gas −0.22 and steer −0.016 rad | Active gas was 0. Rear Fx was −97 N, Fy +239 N and front load 776 N. Front +188 and rear −143 N·m left **+44.94 N·m**, giving **+0.852 rad/s²** despite zero gas. |
| D, +2 cm S 476.885 at 25.110 s | 22.800 s | 22.800 s, gas +0.18 | The bike drifted toward the inside of the straight and the outer clearance exceeded 1.80 m while instantaneous swept remained 1.675 m. Negative yaw and decreasing β preceded the crossing. |
| D, +12 cm S 472.595 at 24.930 s | 22.650 s | None in the audit menu | The same position rule failed earlier, with more gas active, yaw −0.139 rad/s and outer clearance 1.80051 m. |
| E, −12 cm S 783.517 at 39.260 s | 36.050 s | 36.050 s, gas +0.14 and steer +0.025 rad | Physical steer and positive yaw were falling while gas stayed near 51%. Outer clearance was 0.623 m and swept fell to 0.49763 m. |
| F, +12 cm S 194.097 at 11.630 s | 9.350 s | 9.350 s, gas +0.18 | Outer clearance reached 2.20897 m in the 2.20 m entry window. The large swept reserve rules out a band-clearance failure there. |

Times are the earliest successful command changes **in the finite menu**, with an additional 0.05 s activation delay unless a queued command extends it. An edit can cross a fault and still lose a later predicate. In C the positive steering demand of +0.01862 rad, carried yaw, side force and loaded front axle all survive throttle closure. Continued positive yaw acceleration identifies the front/rear moment imbalance as the immediate cause. It does not require positive rear traction. The joint steering/gas intervention changes the trajectory before the last sample. [All causal traces and bounded edits](../../../data/experiments/speedway207/causal-and-phases.json.gz)

## Planner and independently locked evaluation

`JointPlanner207` keeps exact accepted prefix states for three training starts at nominal and ±1 cm. Each state contains position, heading, velocity, wheel and shaft, yaw, β through velocity/heading, physical steer, lean, posture, fired action, pending commands, the full technique checkpoint and a separate geometry/authority record. The beam has slots for different advantages including worst-start reserve, swept clearance, yaw/line conditions, elapsed time and axle/traction reserve. No state is merged on reference progress. Exact deduplication compares the whole multi-start state including the ordered queue and timers. Recovery replays a common prefix from rest and compares each stored state before a suffix can be optimized.

Action proposals change a feedback law jointly in drive, desired yaw and lateral offset, with separate gas and steering lead times. The controller uses the observed road offset, course, yaw, β, speed and outward drift. It samples a geometric line/pace reference with a launch template and a repeating flying template. It contains no clocked command tape. Each phase crossing is derived from bend/straight geometry and selects feedback parameters that blend over the first 3 m. Wheel, engine, steering rate and throttle activation remain in the original predictor. A smaller straight yaw target and an optional drive ceiling affect proposals. Actual certification still applies the exact original rules. The post-failure recovery arm also evaluates the velocity component toward the boundary of the *next* straight while the bike is in the bend.

Geometry anchors divide preparation and exit into short actuator transitions and longer continuations through initiation, slide, apex and exit. Near race speed most stage horizons are roughly 0.25–1.0 s. Earlier exit preparation starts across more than one phase. The same accepted trajectory can be continued over multiple stages or replayed from time zero. Only the full original predictor with strict style, positive axle loads, actuator invariants, and ≥0.50 m swept declares a stage valid. A failed stage contributes diagnosis and is ineligible for selection. Soft ranking combines swept and static clearances, yaw/course risk, drift and its direction, axle load and rear force utilisation. These quantities influence which legal states survive without changing an acceptance threshold.

The four prescribed nonzero offsets stay outside training. Nominal is the shared anchor and is independently rerun from rest for locked evaluation. After a shortlist is locked on the training starts, one law per candidate is run from rest on nominal, −2, +2, −12 and +12 cm. Worst legal progress governs first, followed by the number of completions, worst reserve and time. All five share the same phase parameters and feedback. A completed policy needs an exact end-to-end strict run from rest. [Planner](../../../tools/speedway-h26/JointPlanner207.cs) · [Training and verification entry points](../../../tools/speedway-h26/Study207.cs)

## Bounded search results

The pilot ran before the wide search. All training rollouts used the same strict MODEL integrator. The width-one ablation uses the same move menu, phases, three paired starts and predicates as the main beam. It keeps the highest worst-start progress at each phase and uses time as its first tie break. The recovery arms resumed ten independently reverified prefixes at the approach to the fourth apex, from the checkpoint at S = 542.168 m. The arms added drift-aware steering and line changes. The pace arm also tested 0, 18, 38 and 60% throttle ceilings before and through the exit, then released the ceiling when the feedback policy reached the next geometric phase.

| Arm | Width | Caps, calls / steps / CPU s | Measured calls / steps / CPU s | Wall [s] | Last accepted training phase [m] | Worst locked first rejection [m] |
|---|---:|---|---|---:|---:|---:|
| Pilot | 3 | 2,200 / 450,000 / 95 | 2,199 / 179,896 / 35.36 | 17.00 | First exit 132.389 | 179.028 |
| Width-one | 1 | 48,000 / 7,900,000 / 640 | 1,377 / 78,351 / 15.81 | 5.54 | First exit 132.389 | 140.456 |
| Main beam | 5 | 48,000 / 7,900,000 / 640 | 31,758 / 1,620,836 / 236.72 | 70.08 | Fourth exit 610.557 | 615.241 |
| Earlier steering and line | 5 | 36,000 / 5,400,000 / 500 | 15,906 / 596,974 / 107.63 | 52.89 | Fourth exit 610.557 | 615.409 |
| Earlier pace, steering and line | 5 | 36,000 / 5,400,000 / 500 | 18,429 / 812,771 / 139.48 | 56.17 | Fourth exit 610.557 | **617.197** |

A call is one phase on one start. The two recovery counts include 1,170 exact-prefix replay calls each. The five arms consumed 69,669 phase calls and 3,288,828 steps. Full-course rechecks of the locked shortlist are outside these training counters. No search reached the final phase. A rewind import initially rejected the serialized archive before any rollout. The recovery runs read the selected lineages and validated the reconstructed checkpoints against the archived full states. [Search counts, state ranges and rejection categories](../../../data/experiments/speedway207/summary.json)

The width-one arm tried 213 joint candidates after the first exit. All three starts for all 213 fell below swept before the next stabilization checkpoint. From five outgoing histories the beam found 86 of 222 joint candidates that reached stabilization on all three starts. Their terminal course errors were −3.99° to +3.74°, with yaw −0.141 to +0.205 rad/s. At release from the next yaw core, 56 of 220 joint candidates retained a strict continuation. Five states survived that phase from four distinct parents. Before the S ≈502 fault, 73 of 220 joint candidates reached the turn-preparation phase legally, and the five retained lineages came from five distinct parents. The joint planner reached and passed the old failure at S ≈502 under the original strict profile. The same-menu single-lineage comparison isolates the benefit of preserving future control states. The archived #206 result remains the stronger historical absolute-progress comparator at S = 502.002 m.

At the fourth exit, the late arm retained five distinct full states with a single immediate parent. At that point more width alone would not establish more diverse incoming motion. Moving preparation back 73 m changed the set of reachable states:

| Legal checkpoint | Arm | Outward velocity [m/s] | Course [rad] | Yaw [rad/s] | Legal joint candidates |
|---|---|---|---|---|---:|
| Exit preparation at 596.557 m | Main beam | +3.986 to +6.310 | +0.167 to +0.250 | +0.426 to +0.839 | 160 / 160 |
| Exit preparation at 596.557 m | Earlier pace, steer and line | −0.522 to +6.081 | −0.027 to +0.241 | −0.280 to +0.895 | 368 / 460 |
| Exit at 610.557 m | Main beam | +7.402 to +7.970 | +0.311 to +0.327 | +0.647 to +0.819 | 32 / 160 |
| Exit at 610.557 m | Earlier pace, steer and line | +5.596 to +7.505 | +0.236 to +0.305 | +0.549 to +0.838 | 184 / 460 |

Ranges span training starts that belong to branches legal on all three starting offsets. They are projections of a finite sampled envelope. They make no claim about untested controls or about jointly reaching every combination of extrema. Near S = 596.557 m, the main beam’s fastest legal candidate had worst soft quality −1.927 while the best candidate had +1.376 on that same comparison frontier. Those states could both be legal then. They implied very different ability to stay within the next boundary. The pace arm retained five exit states from two immediate parents and broadened the exit envelope. At the following stabilization checkpoint, beyond S = 622.557 m, every outcome failed the 0.50 m clearance requirement. The main arm evaluated 3,182 late joint candidates from 15 inputs and the pace arm evaluated 4,093 from 15. Fourteen other pace outcomes earlier in the bend were rejected by the contact certificate as no physically admissible root. The selected policy never reached that failure. [Every proposal and its outcome](../../../data/experiments/speedway207/README.md)

### Locked five-start verification

Policy **12152** came from the pace arm. Every row uses that one feedback law with the same parameters. “Last legal” is the final substep that passed every strict rule. “Rejected” is the first rejected substep at its actual clearance.

| Initial offset | Last legal S [m] | First rejected S [m] | Time [s] | Rejected swept [m] | Stop |
|---|---:|---:|---:|---:|---|
| Nominal | 616.993 | 617.229 | 33.110 | 0.493716 | Swept |
| −2 cm | 617.261 | 617.498 | 33.120 | 0.483102 | Swept |
| +2 cm | **616.960** | **617.197** | 33.110 | 0.494051 | Swept |
| −12 cm | 617.189 | 617.425 | 33.120 | 0.475065 | Swept |
| +12 cm | 617.025 | 617.262 | 33.110 | 0.498491 | Swept |

All five cleared the older first-straight, approach and exit obstacles on their route to the fourth exit. The first outcome divergence at this limit was one 10 ms substep. Nominal and the two positive offsets failed at 33.110 s, while the two negative offsets lasted through that substep. None passed the section, so the data do not identify a successful offset outside this envelope. The old taped cases amplified offsets enough to fail different gates at different sections. The selected feedback brought all starts to the same limiting section, with a 0.301 m spread at rejection. [Full state replay of each start](../../../data/experiments/speedway207/selected-replays.json.gz)

On nominal at S = 610.626 m, the trajectory had 1.957 m outer clearance, 17.33° course error, +0.807 rad/s yaw, and already used 1,145 N rear lateral force. At S = 615.100 m, opposing yaw torque had reached −117 N·m and yaw was declining. Outer clearance had meanwhile shrunk to 0.929 m. At S = 617.229 m, yaw and course were still +0.454 rad/s and +7.68°, lateral rear force exceeded 1,186 N, and nominal already had only 0.616 m static outer clearance. Its swept result was 0.494 m even though it had not reached the straight yaw-core start. That sequence identifies outward translational motion as the first binding limit. Changing steering or reducing gas after the exit can reduce yaw while the velocity vector continues carrying the bike toward the boundary. Lower approach pace, earlier line rotation and drift-aware steering improved outgoing authority. None of the tested combinations balanced exit course, clearance and momentum enough to reach the next checkpoint. [Forces, loads, steering and loss measurements](../../../data/experiments/speedway207/selected-replays.json.gz)

## Performance and sporting evidence

The timed online path evaluates the frozen feedback once and runs the full strict MODEL guard over 0.25 s with inherited pending actions. Beam construction is offline and its CPU cost appears above. Three passes under concurrent load and six dedicated passes measured 663 decisions each. Five guards in each pass correctly rejected the final states from which the 0.25 s window contained the impending swept violation. The concurrent passes measured p50 2.05–2.53 ms, p95 6.19–8.02 ms and p99 6.64–10.63 ms. One measured decision took 54.349 ms. The dedicated passes measured p50 2.05–2.08 ms, p95 6.19–6.38 ms, p99 6.55–7.14 ms and a pooled maximum of 10.634 ms. The final controller build measured a dedicated maximum of 9.131 ms. These are same-machine MODEL timings. They include queue handling, state feedback, contact solving and the strict certificate. The archive records each decision’s work and costs. [Concurrent timings](../../../data/experiments/speedway207/model-guard-timing-concurrent.json.gz) · [Dedicated timings](../../../data/experiments/speedway207/model-guard-timing-isolated.json.gz)

Peak legal speed across the five partial runs was 91.85–91.96 km/h. Time at 100 km/h on a straight was 0 s in each run. Nominal physically traveled 696.279 m to the last legal reference S = 616.993 m. Its 361 rate-limited steering steps stayed within the original ±35° angle and 90°/s maximum rate. Front load ranged from 43 to 771 N. Rear Fx ranged from −92 to +1,713 N, and Fy from −628 to +1,327 N. Nominal accumulated 59.36 kJ crank drag, 15.44 kJ clutch heat, 28.27 kJ transmission loss, 93.64 kJ longitudinal rear slip loss and 3.86 kJ rear bearing loss on the accepted prefix. The strict sweep reserve near the final accepted step was small. The dataset gives it by phase and start. No new lap splits or S3 time qualified for sporting comparison with the audited 46.386863 s H25 archive. [Partial sporting and mechanical metrics](../../../data/experiments/speedway207/summary.json)

No locked candidate completed on any of the five MODEL starts. The central eight-slot construction budget therefore recorded **0 reservations and 0 worlds**. All tests and searches ran with native construction disabled. The native adapter is connected to the same physics, frozen policy and strict observer, and requires the five MODEL preflights before requesting a slot. Native contact count, timing, controller adoption and model-to-native trajectory divergence are unmeasured for H29-R. [Construction ledger](../../../data/experiments/speedway207/world-ledger.jsonl)

## Validation and verdicts

Local Release builds of the solution and study tool completed with warnings treated as errors. The 117 existing targeted .NET tests and five new H29-R tests passed. New tests cover hard filtering, independent histories at the same distance, exact deduplication, stale parent and queue/state rejection, feedback/tape and resumed activation agreement, worst-case ranking, and strict finish. The live feedback and tape use one predictor scheduler and the native callback uses the same 50 ms review convention and original pending order. Fresh contact parity matched 72,949 archived force/load decisions, 16,200 fixed-load synthetic cases and 2,346 deterministic rechecks with zero physical or classification differences. Three existing embedded model policies also passed. Replay viewer tests passed 124/124 and CI policy tests passed 7/7. Python calibration on the exact #206 parent and #207 each ran 448 tests with the same 400 passes, 45 failures and three errors. All 48 nonpassing test identities match.

GitHub Actions is separate from local validation. The repository’s workflows trigger on main pushes, pull requests, manual dispatch or the default-branch schedule. Publishing the experiment branch creates no pull request and triggers no Actions run. [Validation record](../../../data/experiments/speedway207/validation.json) · [Full parity evidence](../../../data/experiments/speedway207/parity.json)

| Criterion | Verdict | Evidence |
|---|---|---|
| Diagnostic | **PASS** | All 12 transitions and seven recorded failures audited. Reachable phase evidence and its limits are documented. |
| Planner | **PASS** | Preserved lineages reached strict continuations absent from the width-one ablation. Earlier preparation improved measurable outgoing course and lateral drift. |
| Strict functional | **FAIL** | Zero strict MODEL S3 completions in this bounded search. |
| Robust | **FAIL** | The locked policy finished 0/5 prescribed starts. |
| Native | **NOT RUN** | No MODEL fixture qualified. The ledger is empty. |
| Performance | **NOT CERTIFIED** | No native measurements. The MODEL concurrent sample includes one decision over 50 ms. |
| Sporting | **NOT DEMONSTRATED** | The 46.386863 s strict H25 reference has no strict H28-F S3 result to compare. |

**Overall verdict** · H28-F strict S3 feasibility remains undemonstrated.

## Answers

1. **Is strict-valid H28-F S3 achievable?** No full run was found. The search bounds and timed technique windows do not establish physical impossibility.
2. **What limited the old controller?** Progress-only selection removed alternative legal incoming states. A late one-metric repair can clear the original marker while leaving insufficient course, yaw or line reserve. Closed throttle still allows the front/rear moment imbalance to increase yaw.
3. **Does joint planning solve the straight-to-corner transition?** It produced strict continuations through the old first-straight yaw/correction bottleneck and past S = 502 m for all five exact starts. A new fourth-exit stabilization failure prevents certifying all six bend cycles.
4. **Does one policy handle all five starts?** One shared policy reached the same fourth-exit limit on all five. Zero finished.
5. **What first prevents more sustained throttle?** Outward velocity and loss of swept reserve on the fourth exit. The rear is already heavily loaded in lateral force, and falling yaw near the boundary comes too late to realign course. The next straight cannot support sustained acceleration until the trajectory reaches it with adequate clearance and direction.
6. **Where should #208 focus?** Continue control optimization first. Search the fourth entry, apex and exit as one earlier joint maneuver, with terminal constraints on next-straight lateral velocity, course, and reachable swept reserve. Repeat that at a bounded set of lower approach speeds before changing the physical model or rider-body controller.
