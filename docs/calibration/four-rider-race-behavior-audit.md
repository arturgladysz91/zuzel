# Four-rider production race behavior audit

Baseline: `a3165260183067bff9b6a0e9e94034fa1f361dcf` (merged #50). Analysis only; no production tuning.
Frozen method: [protocol and model inventory](four-rider-race-behavior-protocol.md). Typed evidence: [JSON appendix](four-rider-race-behavior-evidence.json).

## Executive summary

**Verdict: C — core race behavior still structurally insufficient for credible four-rider battles.**
This is not a rejection of the longitudinal/corner foundation or a request for motorcycle dynamics.
The suite finds two blockers, not a list of missing realism features:

1. **INTERACTION: no shared-time swept-space race resolution.** Faster riders catch and order changes are detected, but rivals mostly traverse independently. A separate equal-speed production control swaps two adjacent lines at identical arrival times without any contact or yielding. In A, a leader 15 m ahead at initial time zero still changes the follower's occupancy-based target. Endpoint time-gap contacts and overtake events are not overtaking mechanics.
2. **DECISION: static safe-speed route cost can invert the actual production ranking.** C's outer line has higher exit speed, but its longer route is not compensated in the integrated bend-plus-straight traversal. The existing projection ranks that outer route first. Constant-lane refreshes do not constitute an entry/apex/exit or opening-exit plan.

Positive evidence is substantial: A's faster follower passes in every seed; B recognizes an inner alternative; side-by-side riders can complete corners; F/G reverse line choice with reversed surface; geometry changes J; all six current skills have consumers; retained wear changes later decisions. None proves overlap, held space, defence or undercut response.

Start/gate geometry is **not established as the next blocker**: I's first-corner contacts are limited in this seed set, and equal-skill fixed-line launch differences have an existing geometric explanation. Gate calibration remains unverified. Frozen entry radius/surface during lateral motion and incident scaling are important limitations, not independently established third blockers. Classification C follows the impossible shared-time crossing control and measured decision misranking, not the number of TODOs. No fix, tuning, Ready or merge is part of this PR.

## Scenario matrix

Credibility here means support for the requested behavior, not empirical validation of lap times.
Repeated BLOCKER labels refer to the same two mechanisms, not additional blockers.

| scenario | observed behavior | credible? | primary issue class | severity |
| --- | --- | --- | --- | --- |
| A | Faster follower closes 15 m and passes; contact not necessary. Occupancy still reacts to the distant initial leader. | catch yes; spatial traffic no | INTERACTION | BLOCKER |
| B | Inner target and bounded lateral execution; inside pair inversions occur. Initial-grid event artifact; no defended overlap. | partly | INTERACTION | BLOCKER |
| C | Follower selects wide, gets higher exit speed; static projection prefers the slower integrated route. First pass is already on the straight, not proof of a bend exit pass. | decision ranking no | DECISION | BLOCKER |
| D | Distinct continuous paths coexist through entry; later order can change. No forced first-entry contact. | supported at boundary scale; held space unproven | INTERACTION | ACCEPTABLE SIMPLIFICATION |
| E | Occupancy changes target, generally to an adjacent alternative; seed 0 returns inward at exit. No wholesale extreme-lane escape. | yes locally; temporal occupancy approximate | DECISION | IMPORTANT |
| F | Worn inside pushes targets outward; reading/style/control do not produce four distinct guaranteed choices. Many non-contact crashes. | surface direction yes; severity not calibrated | DATA/CALIBRATION | IMPORTANT |
| G | Reverse surface makes riders target inner despite outside preference; no invariant outer bias. | yes in this fixture | DECISION | PASS |
| H | Inner shorter, outer faster at exit; AI targets inner rather than explicitly planning an opening exit. Coarse whole-bend topology inflates static projection. | trade-off exists; dynamic battle unproven | DECISION | IMPORTANT |
| I | Launch skill changes crossing times; first-corner order compresses/reorders without gate bonuses. Event tie/order artifacts must not be called passes. | useful foundation; physical gate claims unverified | START | ACCEPTABLE SIMPLIFICATION |
| J-tight | Same riders choose/execute differently; shorter lap times and different pair outcomes. | geometry sensitivity yes | PHYSICS | PASS |
| J-wide | Different width/radius/time budget changes lines, path costs and contacts; no artificial equalization. | geometry sensitivity yes | PHYSICS | PASS |

## Measured scenario outcomes

Every main case: actual AdaptiveDecisionModel → HeatSimulator → SimulationEngine, four laps, seeds 0..31, incidents 1, all calibration adjustments null. Counts below are totals over 32 heats; initial ties are excluded from established-pass totals. R2/R1 is a focal diagnostic, not a required winner.

| case | initial order (seed 0) | final order (seed 0) | established pair inversions / tied inversions / production events | R2 passes R1 (heats) | R2 ahead of R1 at finish (both finish) | contacts / LostRhythm / crashes | first-boundary event mismatch | traffic changes target | mean winner time s |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A | 1>2>3>4 | 2>1>3>4 | 36/0/36 | 32 | 32/32 | 1/1/1 | 0 | 95 | 49.973 |
| B | 1>2>3>4 | 2>1>3>4 | 69/0/101 | 31 | 15/29 | 6/4/4 | 32 | 277 | 51.773 |
| C | 1>2>3>4 | 2>1>3>4 | 32/0/32 | 32 | 19/19 | 0/0/20 | 0 | 55 | 53.694 |
| D | 1>2>3>4 | 1>2>3>4 | 50/0/50 | 18 | 12/31 | 4/3/1 | 0 | 248 | 51.989 |
| E | 1>2>3>4 | 1>2>3>4 | 16/0/16 | 10 | 10/31 | 2/2/3 | 0 | 181 | 51.555 |
| F | 1>2>3>4 | 3>2>4>1 | 189/0/189 | 32 | 18/18 | 22/18/32 | 0 | 1014 | 56.086 |
| G | 1>2>3>4 | 2>3>1>4 | 91/0/91 | 28 | 27/28 | 28/24/8 | 0 | 302 | 51.034 |
| H | 1>2>3>4 | 1>2>3>4 | 32/0/64 | 17 | 6/32 | 2/2/0 | 32 | 116 | 48.862 |
| I | 1>2>3>4 | 1>3>2>4 | 422/192/548 | 11 | 8/29 | 25/19/7 | 32 | 1087 | 54.328 |
| J-tight | 1>2>3>4 | 1>2>3>4 | 36/0/68 | 22 | 16/31 | 9/8/2 | 32 | 236 | 44.030 |
| J-wide | 1>2>3>4 | 2>1>3>4 | 65/0/97 | 31 | 15/31 | 2/2/2 | 32 | 275 | 62.088 |

Crash total includes constraint outcomes, random surface incidents and contact crashes; contact-crash total is contacts minus LostRhythm. Typed Resolve outcomes do not distinguish a constraint crash from a random-incident crash, so non-contact totals are not attributed to RNG alone. A first-boundary mismatch is the symmetric difference of production events and true-initial-order inversions. It does not accuse normal starting-grid events of being false passes.
Two causes must stay separate: rolling B/H/J start from a different true order than the production lane/id grid; I instead exposes the net-rank-gainer event filter (some pair inversions are missed during multi-rider reshuffles). Neither count is a continuous manoeuvre count.

## Focal execution and geometry — seed 0

| case | R2 targets through first bend | R2 planned lanes | R2 exit lateral positions | R2 end speeds m/s | first R2→R1 established inversion (lap/segment/boundary) | R1/R2 first-bend path m | R1/R2 bend-exit speed m/s |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A | 2,1,1,0 | 2,1,1,0 | 2.000,1.370,1.000,0.361 | 25.194,23.764,21.498,23.059 | 1/0/1.000 | 80.616/89.127 | 20.595/23.059 |
| B | 1,1,1,0 | 1,1,1,0 | 1.000,1.000,1.000,0.334 | 23.799,21.876,20.317,22.026 | 1/1/2.000 | 94.248/84.823 | 23.046/22.026 |
| C | 4,4,4,4 | 4,4,4,4 | 4.000,4.000,4.000,4.000 | 26.206,26.206,24.696,26.359 | 1/0/1.000 | 89.798/113.097 | 21.812/26.359 |
| D | 2,1,1 | 2,2,1 | 2.178,2.000,1.288 | 21.000,21.203,22.892 | 2/1/10.000 | 82.290/97.949 | 21.141/22.892 |
| E | 1,1,1,0 | 1,1,1,0 | 1.000,1.000,1.000,0.348 | 23.103,21.340,19.798,21.550 | none | 75.398/84.823 | 20.926/21.550 |
| F | 4,4,4,4 | 2,3,3,4 | 2.000,2.662,3.000,3.790 | 22.657,22.028,21.357,23.058 | 1/1/2.000 | 89.643/99.468 | 20.789/23.058 |
| G | 0,0,0,0 | 0,0,0,0 | 0.000,0.000,0.000,0.000 | 23.034,21.154,19.504,21.185 | 1/2/3.000 | 77.072/75.398 | 20.650/21.185 |
| H | 0 | 3 | 3.011 | 23.920 | 3/0/9.000 | 30.159/45.239 | 20.137/23.920 |
| I | 1,1,1,1 | 1,1,1,1 | 1.000,1.000,1.000,1.000 | 24.837,21.876,20.317,22.026 | none | 75.398/84.823 | 20.926/22.026 |
| J-tight | 2,1,1,0 | 2,1,1,0 | 2.000,1.457,1.000,0.682 | 22.585,19.760,17.912,15.823 | none | 65.880/65.884 | 19.296/19.296 |
| J-wide | 1,1,0,0 | 1,1,0,0 | 1.000,1.000,0.183,0.000 | 24.908,24.908,24.032,24.505 | 1/1/2.000 | 128.150/115.958 | 25.538/24.505 |

The boundary is only where a changed order was observed. No exact intra-segment crossing coordinate is inferred. Distances are integrated production traversal distances, not a replacement trajectory solver. H starts after the apex, so its apex observation is unavailable.

## Fixed-route projection sanity checks — separate, non-stochastic solo controls

Same rider/setup/surface/entry speed within each set. Fixed lanes 0..4, no incidents, no rivals, zero drying. Actual time integrates the whole first bend and following straight. Projected time transcribes the existing static route formula without perception noise or other costs. These are not optimized trajectories or main four-rider results.

| source | lane | projected s | production integrated s | bend path m | entry / actual apex / bend exit / straight exit m/s |
| --- | --- | --- | --- | --- | --- |
| B | 0 | 7.126 | 6.510 | 75.398 | 19.000/19.000/20.958/23.602 |
| B | 1 | 7.186 | 6.960 | 84.823 | 19.000/19.000/21.172/25.033 |
| B | 2 | 7.261 | 7.420 | 94.248 | 19.000/19.000/21.380/25.183 |
| B | 3 | 7.346 | 7.878 | 103.673 | 19.000/19.000/21.583/25.305 |
| B | 4 | 7.439 | 8.336 | 113.097 | 19.000/19.000/21.780/25.425 |
| C | 0 | 7.326 | 6.382 | 75.398 | 22.000/18.482/20.809/23.318 |
| C | 1 | 7.244 | 6.473 | 84.823 | 22.000/19.993/22.250/25.152 |
| C | 2 | 7.174 | 6.615 | 94.248 | 22.000/21.502/23.657/26.972 |
| C | 3 | 7.111 | 6.934 | 103.673 | 22.000/22.000/24.276/27.583 |
| C | 4 | 7.053 | 7.322 | 113.097 | 22.000/22.000/24.525/27.840 |
| H | 0 | 15.063 | 6.318 | 75.398 | 22.000/19.000/20.958/23.602 |
| H | 1 | 15.604 | 6.503 | 84.823 | 22.000/20.153/22.061/25.033 |
| H | 2 | 16.135 | 6.724 | 94.248 | 22.000/21.243/23.082/26.230 |
| H | 3 | 16.653 | 7.013 | 103.673 | 22.000/22.000/23.818/26.698 |
| H | 4 | 17.159 | 7.418 | 113.097 | 22.000/22.000/23.961/26.790 |

- B: projected fastest lane 0, integrated fastest lane 0.
- C: projected fastest lane 4, integrated fastest lane 0.
- H: projected fastest lane 0, integrated fastest lane 0.

H's three-arc projection is deliberately checked against a one-segment whole bend: it exposes topology sensitivity and must not be treated as an ordinary three-segment venue calibration.

## Targeted production mechanics controls — separate from main AI cases

These fixed-intent controls disable random incidents and have no eligible contacts. They diagnose mechanism capability, not how often the AI chooses the inputs. Equal-start uses identical six skills/setup, not the main I profiles; reference lanes are not physical A/B/C/D gate coordinates.

| control | rider | first-step entry→exit lateral | first-step arrival s | first-step path m | first-step traversal exit m/s | contacts in heat |
| --- | --- | --- | --- | --- | --- | --- |
| crossing | 1 | 0.000→1.000 | 2.887 | 60.000 | 23.252 | 0 |
| crossing | 2 | 1.000→0.000 | 2.887 | 60.000 | 23.252 | 0 |
| entry-hold | 2 | 3.000→3.000 | 1.646 | 34.558 | 21.000 | 0 |
| entry-inward | 2 | 3.000→2.178 | 1.646 | 34.558 | 21.000 | 0 |
| equal-start | 1 | 0.000→0.000 | 2.928 | 35.000 | 23.589 | 0 |
| equal-start | 2 | 1.000→1.000 | 2.924 | 35.000 | 24.837 | 0 |
| equal-start | 3 | 3.000→3.000 | 2.924 | 35.000 | 24.837 | 0 |
| equal-start | 4 | 4.000→4.000 | 2.924 | 35.000 | 24.837 | 0 |

Main I first start/corner across 32 heats: contacts 1, LostRhythm 1, contact crashes 0. Whole-heat counts above must not be presented as first-corner frequency.

## Six current production skills — matched 20/80 controls

Only rider 2's named skill changes, paired seeds 0..31; incidents disabled to isolate other effects (contacts still stochastic). Start uses I, Speed A, reading F. SlideControl/PairRiding use a fixed aligned contact probe; Adaptability a fixed inward target. These intent controls are separate from the main AI suite. Time averages below use only pairs where rider 2 finishes both versions; negative high-minus-low means high is faster. A changed time alone does not measure the full skill's quality.

| skill | paired finished heats | R2 mean high-minus-low time s | R2 contacts low/high | all heat contacts low/high | R2 target differences | R2 first-step lateral movement low/high |
| --- | --- | --- | --- | --- | --- | --- |
| Start | 27 | -0.149 | 14/4 | 20/27 | 386 | 0.063/0.063 |
| Speed | 32 | -4.389 | 3/0 | 3/1 | 298 | 1.000/1.000 |
| SlideControl | 28 | -1.886 | 31/17 | 113/105 | 0 | 0.010/0.010 |
| TrackReading | 29 | -0.348 | 12/0 | 27/23 | 304 | 1.000/1.000 |
| PairRiding | 22 | -0.094 | 42/41 | 136/131 | 0 | 0.021/0.010 |
| Adaptability | 32 | -0.230 | 0/0 | 0/0 | 0 | 0.952/1.000 |

## Surface feedback — six repeated four-rider heats, 32 matched seeds

B inputs are reset each heat; surface is retained, not reset. No rain/drying; wear remains production. Fresh replays have the same seed/riders/options. Changed-choice counts compare only common (step,rider) observations: differential crashes can truncate a path and are not silently treated as choices. Histograms include all observed choices.

| heat | mean grip before→after | mean ruts before→after | changed cells (sum) | targets different from fresh | retained target 0/1/2/3/4 | fresh target 0/1/2/3/4 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 1.000→0.987 | 0.000→0.050 | 1121 | 0 | 2072/1395/562/0/0 | 2072/1395/562/0/0 |
| 2 | 0.987→0.974 | 0.050→0.102 | 1182 | 1929 | 1590/874/1464/142/0 | 2072/1395/562/0/0 |
| 3 | 0.974→0.961 | 0.102→0.155 | 1280 | 2595 | 1435/516/1022/1094/8 | 2072/1395/562/0/0 |
| 4 | 0.961→0.948 | 0.155→0.207 | 1279 | 2924 | 1215/398/630/1200/653 | 2072/1395/562/0/0 |
| 5 | 0.948→0.935 | 0.207→0.258 | 1279 | 2680 | 1359/460/587/697/944 | 2072/1395/562/0/0 |
| 6 | 0.935→0.923 | 0.258→0.308 | 1280 | 2790 | 1217/404/594/578/1140 | 2072/1395/562/0/0 |

## Separate single-seed non-stochastic control

Fixed separated straight paths: initial/final 1>2>3>4/1>2>3>4, contacts 0, inversions 0, crashes 0. Seed independence is regression-tested; main Adaptive/contact cases are never classified as RNG-free.

## Top blockers — two, both mechanism failures

### 1. INTERACTION — arrival-time sorting is not spatial racing

Main A demonstrates speed-driven catching without needing contact, but its seed-0 follower selects target 2 versus no-traffic target 1 while its same-line leader is 15 m ahead. The occupancy predicate uses equal elapsed time and segment membership, not longitudinal separation. All subsequent riders are synchronized to topology boundaries despite different arrival times. The contact phase sees endpoint lateral positions and arrival-time differences, not coexistence on a common-time trajectory.

The fixed crossing control strengthens this beyond code inspection: R1 goes 0→1, R2 1→0, on the same straight with identical speed, distance, arrival time and surface. Both full-segment longitudinal profiles are identical. Any continuous realization of these simultaneous lateral swaps crosses occupied space, yet the solver accepts both and registers zero contacts/yields because endpoints are separated. This proves unsupported contested-space mechanics; it does **not** estimate how often Adaptive chooses that manoeuvre. Main D succeeding on separated lines does not cure it.

Production overtakes are order-change events, only emitted for net-rank gainers. They do not causally generate speed/passing. The audit independently counts all active pair inversions and marks previous ties. Initial lane/id ordering also invents rolling-fixture events. Those logging discrepancies are narrower symptoms; the blocker is the absence of common-time overlap, held territory, closure and response, not merely the event formatter.

### 2. DECISION — candidate score is inconsistent with its own physics

C's fixed-route controls keep rider/setup/surface/entry speed equal. The projection prefers lane 4, while actually integrating the bend and next straight prefers lane 0. The wide route does have higher exit speed; that benefit is insufficient to pay its extra distance. Main C follows the static wide preference. Its initial straight inversion cannot be used to declare an outside-exit manoeuvre successful. B's ranking agrees in the simpler uniform fixture, so this is a counterexample rather than a claim that every decision fails.

`routeLength/projectedSafeSpeed` is therefore insufficient as the sole performance estimate. It ignores current speed, braking/carry/drive phases, achievable lateral transition, remaining corner progress and the following straight's acceleration. H separately exposes the hard-coded three-arc topology assumption; that coarser fixture cannot by itself calibrate a normal bend. Neither model anticipates a rival's response or its own entry/apex/exit intent. Occupancy/style costs can redirect a target, but cannot repair the predicted travel-time ranking.

## Decision, execution, physics and interaction are not interchangeable

Five reference targets are **not demonstrated to be intrinsically too few** for manager-scale AI. F/G and J already differentiate surfaces and geometry using them. Adding more samples to the same wrong cost would not cure C. The larger deficiency is temporal trajectory intent: bounded alternatives for entry, apex and exit, evaluated with achievable motion and a consistent production speed profile. This is a recommendation, not a new API in this PR; no optimized #47–#50 trajectory replaces the heat.

Continuous lateral execution is useful but not sufficient by itself. The matched solo D control moves from 3 to about 2.178 instead of holding 3, yet both first traversals have exactly the same path distance, time and exit speed. Production freezes entry lateral position for that segment's radius/surface/distance and integrates lateral movement afterwards. This is an **EXECUTION/PHYSICS — IMPORTANT** cost-consistency limitation. Its scale has not been shown to independently break race order enough to call it a third blocker. Better intent alone would still need coherent costs along the executed motion and spatial interaction constraints.

F/G's profile differences are intentionally mixed scenario inputs, not an isolated skill experiment. The one-skill controls separate the consumers:

| skill | current consumer | strength supported by this audit |
| --- | --- | --- |
| Start | Standing reaction and launch force; zero-speed compatibility bootstrap outside this suite's rolling cases | Clear launch effect; modest whole-heat delta that can cascade into traffic |
| Speed | Longitudinal drive reference force | Strong elapsed-time differentiation in matched A |
| SlideControl | Corner capability/correction/overspeed retention; half lateral execution; contact occurrence/severity | Strong, multi-subsystem; aligned control also changes contact eligibility through physics |
| TrackReading | Addressed noise in perceived grip/ruts | Context-dependent meaningful target differences, not guaranteed always-better finish |
| PairRiding | Contact occurrence factor only | Narrow and weak observed count difference in 32 seeds; not team riding or overtake intelligence |
| Adaptability | Half lateral traversal-rate weight | Narrow execution effect; no learned-surface/trajectory adaptation in production |

None is literally consumer-free. PairRiding/Adaptability are narrow, not proof of rich tactical behavior. Matched contact counts are descriptive: eligibility and consequence chains can change, so they are not unbiased estimators of each probability coefficient. Start and Speed control heat times are measured, not new target values.

## What is not a blocker / what remains uncalibrated

- **Start:** defer physical A/B/C/D gate geometry. Main I shows compression and differentiated launch, not an automatic collision storm. The equal-skill fixed-line control removes its Start/gate confounding and shows existing lookahead/geometry can produce different launch endpoints. Reference lanes are not gate coordinates. This does not validate real gate advantages, but no separate gate mechanism failure outranks contested-space resolution.
- **Surface feedback:** retained wear raises ruts, lowers grip and changes later choices against identical fresh replays. The loop is alive. Entry-path sampling and wear kernels remain coarse, provisional spatial approximations; zero drying in the matched probe prevents a weather confound.
- **Incidents/contact calibration:** F and C's crashes deserve review, but synthetic counts alone cannot establish real acceptable rates. Planned outer lane 4 makes a triggered random surface incident a crash regardless of its severity draw. Do not conflate this with contact crash probability. This audit changes no probabilities or 0.55 m / 0.12 s / 0.50 m constants.
- **Fractional initialization:** the 0.05 probe's boundary exception is a real technical limitation for resumed/custom states, not evidence of a routine zero-progress standing-start failure. It belongs with the temporal/progress resolution work, not a balancing tweak.
- **Manager-scale scope:** no RPM, Pacejka, precise CdA, suspension, tyre temperature or granular soil is required by these failures. Lack of telemetry keeps absolute physics magnitudes DATA/CALIBRATION, not automatically BLOCKER. Different gate/line advantages are allowed; equality of winners is not an acceptance target.

## Recommended next PRs — at most three, no implementation here

1. **Common-time contested-space resolution.** Problem: independent segment endpoints allow simultaneous crossed paths and false occupancy at a 15 m gap. Scope: existing SimulationEngine/position/interaction owners, bounded common-time or swept occupancy resolution, pair holding/yielding and coherent order-event attribution; include fractional-boundary initialization. Do not retune acceleration/safe-speed/start/contact probabilities, add combined grip, change skills or implement motorcycle dynamics. Acceptance: crossing control cannot traverse occupied space unconditionally; an initially distant leader does not block merely because arrival offsets match; A/B/D/I have reproducible overlap/response evidence; no DNF or tie-break pseudo-passes; deterministic reversed collections and all 32 seeds.
2. **Motion/cost consistency for continuous lateral execution.** Problem: inward and held D paths receive identical full-segment entry-radius costs. Scope: carry executed lateral motion into physical distance/surface/curvature sampling with a bounded manager-scale approximation, keeping current longitudinal/corner formulas. Do not retune their constants, add #48 turning-slip cost or #50 combined grip, or redesign rider attributes. Acceptance: held-line controls preserve their baseline; moving-line distance/surface observations follow the executed path without teleportation; entry/apex/exit and segment-splitting consistency are tested; shared-time interactions remain valid.
3. **Bounded trajectory-intent decision scoring.** Problem: C static ranking disagrees with integrated production, while H has no opening-exit plan. Scope: entry/apex/exit intent built from existing five reference anchors, current speed, achievable transitions and the same production profile costs; bounded rival-response/occupancy constraints from PR 1. Do not multiply lane samples as a substitute for intent, tune grip/drive/contact, import optimizer physics or implement manager AI. Acceptance: C's predicted fixed-route ranking matches production under equal constraints; B still finds free inner space; H compares short/slow versus wide/fast exit profiles explicitly; F/G/J respond without forced winners; score differences decompose into time/movement/style/traffic/risk.

The sequence puts spatial constraints before predicted manoeuvres. It does not prescribe new gate or contact constants. **Final judgment remains C**, because producing believable permutations is not yet a believable process of four riders contesting space. After this analysis PR, stop; the recommendations require separate approval.

## Reproduction and limits

Run `dotnet run --project src/Sandbox -c Release --no-build -- four-rider-race-behavior-report docs/calibration`. The writer emits only this Markdown and its JSON evidence; frozen protocol is maintained separately. UTF-8 without BOM, LF, invariant numeric formatting, no clock/generated timestamp or HEAD-dependent content. Hashes and exact-head CI are recorded in the Draft PR body to avoid self-referential hashes.
This is a synthetic behavior audit, not empirical calibration or proof that any elapsed time/contact frequency matches real racing. Existing geometry, setup, incidents and surface assumptions remain provisional. Production files are unchanged; only analysis, tests and the Sandbox command are added.
Fractional-progress compatibility probe (B with initial 0.05 instead of 0.0625): `Rider 1 is not in segment 1.`. Float remainder rounding can leave canonical progress below the boundary tolerance. The accepted battle input is the exactly representable 0.0625 (3.75 m on 60 m), not a production repair; the rejected input remains recorded.
