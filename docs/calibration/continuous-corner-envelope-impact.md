# Continuous Corner Envelope impact (#38)

## A. Provenance

Base main SHA: `a7c7c387777ff9ee8899d2d1a34258e735ae0f60`
Selected candidate checkpoint: `B19` (advanced settled-corner reference 19 m/s; all other candidate inputs frozen).
Complete BEFORE scenario snapshot SHA-256: `d85d5752a82a1ac083e90fedc2182374cf5a32442a4d683b3e50cf374462862d` (186 scenarios captured on base before production edits).
Complete AFTER scenario snapshot SHA-256: `c06f632e04c72d119e5cf925f9f08a8e4ee6a9d6e2dde4c01491a462f8165417` (197 final scenarios).
Candidate snapshots contain complete typed production results, including full traces. No timestamp or machine path enters this report.
Final model: one physical-distance traversal over CornerProgress 0..1; recoverable pre-apex envelope, settled apex at 0.5, and signed existing corner drive after the apex.

## B. Architecture before

`Straight → TurnEntry scrub → TurnMiddle carry/correction → TurnExit correction + drive → Straight`. Segment labels selected longitudinal laws.

## C. Architecture after

`Straight → Continuous Logical Corner 0..1 → Straight`. TurnEntry/Middle/Exit remain topology/reporting compatibility labels, not longitudinal physics phases.
ADVANCED production calls to the old TurnEntry scrub path: **0**. ADVANCED production calls to segment-gated TurnExit drive: **0**. Legacy behavior remains on its prior path.

## D. Envelope shape

ApexProgress = 0.50 is a provisional geometry assumption, not telemetry-derived. FullDriveProgress = 5/6 is a behavior-derived starting assumption: smoothstep exposure integrates to approximately the former final-third full-drive exposure.

| Progress | Lateral | Radius m | EffectiveGrip | Settled/apex m/s | Envelope entry m/s | Envelope exit m/s | Drive availability | Correction m/s² | Actual entry m/s | Actual exit m/s | Outcome | Residual m/s |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 1 | 27 | 1 | 20.15254 | 25.03327 | 21.90064 | 0 | 2.6 | 25.03327 | 21.90065 | Ok | 0.000002 |
| 0.125 | 1 | 27 | 1 | 20.15254 | 23.90669 | 21.90064 | 0 | 2.6 | 23.90669 | 21.90065 | Ok | 0.000006 |
| 0.25 | 1 | 27 | 1 | 20.15254 | 22.72432 | 21.90064 | 0 | 2.6 | 22.72432 | 21.90064 | Ok | 0 |
| 0.375 | 1 | 27 | 1 | 20.15254 | 21.47696 | 20.35263 | 0 | 2.6 | 21.47696 | 20.35263 | Ok | 0.000002 |
| 0.5 | 1 | 27 | 1 | 20.15254 | 20.15254 | 20.35263 | 0 | 2.6 | 20.15254 | 20.35263 | Ok | 0 |
| 0.625 | 1 | 27 | 1 | 20.15254 | 20.24453 | 20.35263 | 0.316406 | 2.6 | 20.24453 | 20.35263 | Ok | 0.000002 |
| 0.75 | 1 | 27 | 1 | 20.15254 | 20.70319 | 22.06063 | 0.84375 | 2.6 | 20.70319 | 22.06063 | Ok | 0 |
| 0.875 | 1 | 27 | 1 | 20.15254 | 21.39989 | 22.06063 | 1 | 2.6 | 21.39989 | 22.06063 | Ok | 0 |
| 0.999999 | 1 | 27 | 1 | 20.15254 | 22.06063 | 22.06063 | 1 | 2.6 | 22.06063 | 22.06063 | Ok | 0 |

For p < 0.5: `v_envelope = sqrt(v_apex² + 2 × correctionCapability × (0.5 - p) × totalCornerLength)`.
For p > 0.5: start at v_apex and integrate the existing signed full-turn net force over post-apex distance in 1 m steps plus final remainder. The net contribution is multiplied by `smoothstep(clamp((p-0.5)/(5/6-0.5),0,1))`; availability 0 is neutral carry.

## E. Candidate screen

The screen is hierarchical, not an optimizer. A0 measures architecture at 16 m/s. Stage B changes only the advanced settled-corner reference using the required 17/18/19 menu.

| Candidate | Reference m/s | Entry envelope m/s | Apex m/s | Exit envelope m/s | Speed50 Vmax P50 m/s | Avg P50 m/s | L1 penalty P50 s | HeatTime P50 s | Brake | RunWide | Crash | Snapshot/checkpoint |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| #37 baseline | 16 | historical segmented | historical segmented | historical segmented | 23.08457 | 18.70542 | 1.135835 | 66.07912 | 0 | 0 | 0 | captured base |
| A0 | 16 | 22.55083 | 16.97056 | 19.65941 | 24.2496 | 19.80721 | 1.2473 | 62.40294 | 0 | 0 | 0 | 530597104b31785ab04c8bdac0c57cf45898281b3f14c49c21a832aaadf0ca07 |
| B17 | 17 | 23.35947 | 18.03122 | 20.44124 | 24.82695 | 20.61112 | 1.286373 | 59.96913 | 0 | 0 | 0 | d76f618b20f4291f765bde90ade70a6c3df29d7048c4436f597c5fa9bb2932b4 |
| B18 | 18 | 24.1876 | 19.09188 | 21.24232 | 25.41457 | 21.4158 | 1.328361 | 57.71591 | 0 | 0 | 0 | b20c33988a512b0889c62d78b648b330186a42dd2ebbe1ea4c968e798126b1af |
| B19 selected | 19 | 25.03327 | 20.15254 | 22.06063 | 26.02223 | 22.21842 | 1.376471 | 55.63098 | 0 | 0 | 0 | final snapshot hash is recorded in provenance |

Vmax location summary: the baseline and all tested candidates retain complete-heat maxima on Straight segments; the new peak diagnostic can also report corner progress when a corner wins.

## F. Selected candidate

B19 is selected. A0 removes much of the structural bottleneck but Speed50 average remains 19.807 m/s. B17 and B18 remain below the PGE CleanPhysics P10 average-speed context. B19 is the smallest tested menu value that reaches that lower contextual envelope (22.218 m/s), keeps all measured baseline events at zero, preserves monotonic skill/line behavior, and still leaves conservative Vmax rather than forcing the real PGE median.
The remaining Vmax deficit is documented as geometry/model context. No second parameter was opened and no magic aggregate score was used.

## G. Speed skill sweep

| Scenario | Vmax P50 m/s | Average P50 m/s | L1 penalty P50 s | HeatTime P50 s | Brake | RunWide | Crash |
| --- | --- | --- | --- | --- | --- | --- | --- |
| full_heat/speed/000 | 24.544689 | 20.660103 | 1.199057 | 59.826935 | 0 | 0 | 0 |
| full_heat/speed/025 | 24.919615 | 21.471357 | 1.298641 | 57.566551 | 0 | 0 | 0 |
| full_heat/speed/050 | 26.022228 | 22.218422 | 1.376471 | 55.630981 | 0 | 0 | 0 |
| full_heat/speed/075 | 26.96079 | 22.91818 | 1.441101 | 53.932409 | 0 | 0 | 0 |
| full_heat/speed/100 | 27.839308 | 23.58122 | 1.500023 | 52.415962 | 0 | 0 | 0 |


## H. SlideControl sweep

| Scenario | Vmax P50 m/s | Average P50 m/s | L1 penalty P50 s | HeatTime P50 s | Brake | RunWide | Crash |
| --- | --- | --- | --- | --- | --- | --- | --- |
| full_heat/slide_control/000 | 25.227831 | 21.563769 | 1.318012 | 57.320301 | 0 | 0 | 0 |
| full_heat/slide_control/025 | 25.657127 | 21.903524 | 1.348008 | 56.430971 | 0 | 0 | 0 |
| full_heat/slide_control/050 | 26.022228 | 22.218422 | 1.376471 | 55.630981 | 0 | 0 | 0 |
| full_heat/slide_control/075 | 26.312289 | 22.513044 | 1.403145 | 54.902761 | 0 | 0 | 0 |
| full_heat/slide_control/100 | 26.538836 | 22.790176 | 1.43076 | 54.234989 | 0 | 0 | 0 |


## I. Lines

Outer trajectories have larger radius and capability but pay a longer physical distance. No outer-line bonus exists.

| Lateral | Radius m | Corner-segment distance m | Entry envelope m/s | Apex m/s | Exit envelope m/s | Production Vmax m/s | Avg m/s | HeatTime s |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 24 | 25.13274 | 23.6016 | 19 | 20.9583 | 24.72046 | 20.79464 | 54.01325 |
| 1 | 27 | 28.27433 | 25.03327 | 20.15254 | 22.06063 | 25.61044 | 21.77998 | 55.03145 |
| 2 | 30 | 31.41593 | 26.38739 | 21.24265 | 23.0816 | 26.43513 | 22.70108 | 56.11989 |
| 3 | 33 | 34.55752 | 27.67533 | 22.27948 | 24.03269 | 27.1165 | 23.56032 | 57.27341 |
| 4 | 36 | 37.69911 | 28.90594 | 23.27015 | 24.92289 | 27.65659 | 24.36205 | 58.48351 |


## J. Surfaces and gearing

Surface remains entry-sampled through the existing grid; a real surface difference changes capability, while labels alone do not. Gearing remains the existing normalized trade-off.

| Scenario | Vmax P50 m/s | Average P50 m/s | L1 penalty P50 s | HeatTime P50 s | Brake | RunWide | Crash |
| --- | --- | --- | --- | --- | --- | --- | --- |
| full_heat/gearing/000 | 25.784816 | 22.220901 | 1.255064 | 55.624361 | 0 | 0 | 0 |
| full_heat/gearing/050 | 26.022228 | 22.218422 | 1.376471 | 55.630981 | 0 | 0 | 0 |
| full_heat/gearing/100 | 26.173841 | 22.175668 | 1.525037 | 55.738605 | 0 | 0 | 0 |
| full_heat/surface/baseline | 26.022228 | 22.218422 | 1.376471 | 55.630981 | 0 | 0 | 0 |
| full_heat/surface/grip_080 | 25.271159 | 21.457006 | 1.388259 | 57.605125 | 0 | 0 | 0 |
| full_heat/surface/moisture_070 | 26.000648 | 22.123307 | 1.802237 | 55.870178 | 0 | 0 | 0 |
| full_heat/surface/ruts_025 | 25.60795 | 21.791037 | 1.380085 | 56.722095 | 0 | 0 | 0 |


## K. Before/after complete heat

| Fixture/rider | Vmax before m/s | Vmax after m/s | Average before m/s | Average after m/s | L1 penalty before s | L1 penalty after s | HeatTime before s | HeatTime after s | Events before | Events after |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baseline/rider_1 | 21.98773 | 24.71212 | 17.49112 | 20.78423 | 1.093054 | 1.309654 | 64.21463 | 54.04029 | 0/0/0 | 0/0/0 |
| baseline/rider_2 | 22.72265 | 25.60991 | 18.31076 | 21.75821 | 1.118332 | 1.347896 | 65.45789 | 55.08651 | 0/0/0 | 0/0/0 |
| baseline/rider_3 | 23.44648 | 26.43455 | 19.10008 | 22.67863 | 1.153337 | 1.405046 | 66.70036 | 56.17545 | 0/0/0 | 0/0/0 |
| baseline/rider_4 | 24.11699 | 27.11619 | 19.86534 | 23.54871 | 1.199566 | 1.485113 | 67.92635 | 57.30166 | 0/0/0 | 0/0/0 |


| Fixture | Vmax P50 before m/s | Vmax P50 after m/s | Avg P50 before m/s | Avg P50 after m/s | HeatTime P50 before s | HeatTime P50 after s |
| --- | --- | --- | --- | --- | --- | --- |
| Speed0 | 21.16746 | 24.544689 | 17.43369 | 20.660103 | 70.89942 | 59.826935 |
| Speed50/baseline | 23.08457 | 26.022228 | 18.70542 | 22.218422 | 66.07912 | 55.630981 |
| Speed100 | 24.91702 | 27.839308 | 19.93496 | 23.58122 | 62.00295 | 52.415962 |


## L. Vmax and corner extrema

Before: 4/4 complete-heat baseline maxima were on Straight segments. After: 4/4 are on Straight segments (100% Straight, 0% Corner). Peak selection takes the maximum of every observed source; it does not nullable-coalesce one source away.

| Rider | Vmax m/s | Segment id/type | Corner progress if applicable | Actual corner minimum m/s @ progress | Last corner exit m/s |
| --- | --- | --- | --- | --- | --- |
| 1 | 24.71212 | 0/Straight | — | 18.58819 @ 0.5 | 20.5808 |
| 2 | 25.60991 | 0/Straight | — | 19.6904 @ 0.5 | 21.64085 |
| 3 | 26.43455 | 0/Straight | — | 20.75551 @ 0.5 | 22.64303 |
| 4 | 27.11619 | 0/Straight | — | 21.79658 @ 0.5 | 23.60169 |

Extreme production diagnostic (Speed100, gearing 1, best surface, lanes 0–4) maximum actual Vmax: 30.42642 m/s (109.5351 km/h). It combines the four-rider lanes-0–3 heat with the outermost-line-4 production heat. No hard speed cap was added.

## M. Real telemetry comparison

The existing pge-v1 dataset is parsed by RealWorldCalibrationDataset and evaluated component-wise by RealWorldCalibrationEvaluator. Skill50 is not assumed to be the average PGE rider; absolute time/distance remains geometry-sensitive.

| Context metric | P10 | P50 | P90 | Selected simulation P50 | Classification |
| --- | --- | --- | --- | --- | --- |
| CleanPhysics Vmax km/h | 109.7 | 114.8 | 119.4 | 93.680022 | ComparableEnvelope |
| CleanPhysics AverageSpeed m/s | 22.023974 | 22.980452 | 23.968246 | 22.218422 | ComparableEnvelope |
| Within-heat Vmax spread km/h | — | 4.5 | — | 8.654665 | context |
| Within-heat AvgSpeed spread m/s | — | 0.927 | — | 2.764479 | context |
| Within-heat L1 spread s | — | 0.53 | — | 0.947607 | context |
| Within-heat HeatTime spread s | — | 1.53 | — | 3.261372 | context |


## N. Frozen boundaries

| Boundary | Final value/status |
| --- | --- |
| Straight reference acceleration | 1.60–3.20 m/s² unchanged |
| Turn full-drive endpoint | 1.20–2.80 m/s² unchanged |
| Drive/speed fade | 0.0350 / 0.0100 unchanged |
| Positive-drive reference speed | 16 m/s unchanged |
| Mass / resistance | 142 kg / 40 + 0.20v² N unchanged |
| Gearing / surface drive | 1.10→0.90 / 0.75+0.25×EffectiveGrip unchanged |
| Integration | 1 m + final remainder unchanged |
| Reaction / launch | 0.28→0.20 s / 9→11 m/s² unchanged |
| Correction capability | 2.0→3.2 m/s² unchanged |
| Quiet / Brake / RunWide | 1.015 / 1.06→1.14 / 1.18→1.34 unchanged |
| RunWide retention | 0.35→0.65 unchanged |
| Legacy | numerically unchanged; reference remains 16 m/s |
| Only tuned physics parameter | ADVANCED settled/apex reference 16→19 m/s |

Random incident channels/probabilities/severity/0.88 consequence, target clearing, contact probability and geometry, lateral traversal, occupancy thresholds, surface physics and RiderSkills multipliers are unchanged.

## O. Known limitations

Apex 0.50 and FullDriveProgress 5/6 are provisional behavior/geometry assumptions, not telemetry fits. The track is not matched to a real PGE venue. Entry-sampled surface can change at old subsegment boundaries when the surface itself differs. Diagonal/spiral path length, throttle AI, RPM, torque curves, clutch, real sprockets, wheelspin, traction cap, slip/lean/steering/body/banking/suspension and additional drag remain outside scope. No artificial Vmax, outer-line bonus or final-corner bonus exists. B19 retains a Vmax deficit versus real PGE context; it is reported rather than hidden by further tuning.
