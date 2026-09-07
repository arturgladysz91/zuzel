# Continuous corner-speed correction impact

This deterministic PR #34 report is generated through production `SimulationEngine` and `CalibrationRunner -> HeatSimulator` paths. It compares the immutable PR #33 physical-width report with continuous, distance-limited corner correction. No physics calibration was performed.

## Constraint and correction model

Advanced `SegmentPhysics` now classifies `Ok`, `Brake`, `RunWide`, or `Crash` and emits a nullable correction target. Recoverable outcomes keep their input speed at this discrete stage. `LongitudinalDynamics` then applies `requiredDistance = (entry² - target²) / (2 × deceleration)` over actual remaining metres and uses `time = 2 × distance / (entry + exit)`. An unreachable target leaves residual overspeed. Legacy resolution remains instantaneous.

TurnEntry retains its first-half scrub and gives only post-scrub distance to correction. TurnMiddle corrects and then carries. TurnExit corrects first and gives only correction remainder to drive; RunWide carries instead and receives no drive.

## Controlled corner probes

Balanced rider, neutral setup, perfect/neutral surface, standing-example physical width, lane/position 1, incidents disabled. Band speeds are derived from the current quiet, balanced Brake, and balanced RunWide factors. TurnEntry's displayed entry speed is chosen so its unchanged first-half scrub exits in the requested constraint band.

| Segment | Band | Entry m/s | Max safe m/s | Outcome | Target m/s | Available correction m | Required correction m | Actual correction m | Target reached | Correction exit m/s | Remaining m | Drive m | Final exit m/s | Travel time s |
|---|---|---:|---:|---|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|
| TurnEntry | below max | 16.631151 | 16.970562 | Ok | — | — | — | — | — | — | — | 0 | 16.631151 | 1.700083 |
| TurnEntry | quiet correction | 19.12719 | 16.970562 | Ok | 16.970562 | 14.137167 | 0.833872 | 0.833872 | True | 16.970562 | 13.303294 | 0 | 16.970562 | 1.613376 |
| TurnEntry | Brake | 19.889328 | 16.970562 | Brake | 16.970562 | 14.137167 | 6.552332 | 6.552332 | True | 16.970562 | 7.584835 | 0 | 16.970562 | 1.569543 |
| TurnEntry | RunWide | 21.783581 | 16.970562 | RunWide | 18.497913 | 14.137167 | 11.315068 | 11.315068 | True | 18.497913 | 2.822099 | 0 | 18.497913 | 1.416282 |
| TurnMiddle | below max | 16.631151 | 16.970562 | Ok | — | — | — | — | — | — | — | 0 | 16.631151 | 1.700083 |
| TurnMiddle | quiet correction | 17.097839 | 16.970562 | Ok | 16.970562 | 28.274334 | 0.833872 | 0.833872 | True | 16.970562 | 27.440462 | 0 | 16.970562 | 1.665898 |
| TurnMiddle | Brake | 17.946367 | 16.970562 | Brake | 16.970562 | 28.274334 | 6.552332 | 6.552332 | True | 16.970562 | 21.722002 | 0 | 16.970562 | 1.655291 |
| TurnMiddle | RunWide | 20.025263 | 16.970562 | RunWide | 18.497913 | 28.274334 | 11.315068 | 11.315068 | True | 18.497913 | 16.959267 | 0 | 18.497913 | 1.504263 |
| TurnExit | below max | 16.631151 | 16.970562 | Ok | — | — | — | — | — | — | — | 28.274334 | 18.109766 | 1.626462 |
| TurnExit | quiet correction | 17.097839 | 16.970562 | Ok | 16.970562 | 28.274334 | 0.833872 | 0.833872 | True | 16.970562 | 27.440462 | 27.440462 | 18.351646 | 1.601605 |
| TurnExit | Brake | 17.946367 | 16.970562 | Brake | 16.970562 | 28.274334 | 6.552332 | 6.552332 | True | 16.970562 | 21.722002 | 21.722002 | 18.08419 | 1.614078 |
| TurnExit | RunWide | 20.025263 | 16.970562 | RunWide | 18.497913 | 28.274334 | 11.315068 | 11.315068 | True | 18.497913 | 16.959267 | 0 | 18.497913 | 1.504263 |

## Balanced fixture: PR #33 to PR #34

The current fixture uses HoldLane decisions, incidents disabled, seed 320032, dry weather, neutral setup, perfect/neutral surface, and all skills at 50. The before values are validated against the committed #33 report.

| Metric | #33 | #34 | Delta |
|---|---:|---:|---:|
| Vmax median (km/h) | 76.440358 | 76.432554 | -0.007804 |
| Average-speed median (m/s) | 18.142772 | 18.14121 | -0.001562 |
| L1-penalty median (s) | 1.056545 | 1.057448 | 0.000903 |
| Total-heat-time median (s) | 68.129131 | 68.134998 | 0.005867 |
| Four-rider heat-time spread (s) | 3.473694 | 3.473732 | 0.000038 |
| Four-rider Vmax spread (km/h) | 7.702803 | 7.703085 | 0.000282 |
| Four-rider L1 spread (s) | 0.941988 | 0.941994 | 0.000006 |
| Four-rider average-speed spread (m/s) | 2.396242 | 2.396105 | -0.000137 |
| Ok count | 144 | 144 | 0 |
| Brake count | 0 | 0 | 0 |
| RunWide count | 0 | 0 | 0 |
| Crash count | 0 | 0 | 0 |

## Speed and SlideControl diagnostics

Real percentile positions are deterministic midranks in the versioned PGEE distributions. They are observations, not fitting targets.

| Scenario | #33 / #34 Vmax km/h | #34 real percentile | #33 / #34 average m/s | #34 real percentile | #33 / #34 L1 penalty s | #34 real percentile |
|---|---:|---:|---:|---:|---:|---:|
| `speed_0` | 73.185284 / 73.185284 | 0 | 16.923858 / 16.922634 | 0 | 0.924995 / 0.92577 | 0 |
| `speed_50` | 76.440358 / 76.432554 | 0 | 18.142772 / 18.14121 | 0 | 1.056545 / 1.057448 | 0 |
| `speed_100` | 82.043818 / 82.038246 | 0 | 19.281636 / 19.27976 | 0 | 1.147342 / 1.148356 | 0 |
| `slide_control_0` | 74.31656 / 74.311321 | 0 | 17.625194 / 17.623468 | 0 | 1.018971 / 1.020091 | 0 |
| `slide_control_50` | 76.440358 / 76.432554 | 0 | 18.142772 / 18.14121 | 0 | 1.056545 / 1.057448 | 0 |
| `slide_control_100` | 78.482967 / 78.482967 | 0 | 18.628987 / 18.62754 | 0 | 1.084136 / 1.084905 | 0 |

## Correction diagnostics in the controlled balanced heat

| Measure | Value |
|---|---:|
| Correction profiles | 66 |
| Mean correction distance (m) | 0.069501 |
| Target reached count | 66 |
| Target reached rate | 1 |
| Residual overspeed count | 0 |
| Maximum residual overspeed (m/s) | 0 |
| Correction time contribution (rider-s) | 0.265086 |
| Correction share of summed rider time | 0.000973 |

## Physics invariant

**ALL LISTED CONSTANTS ARE UNCHANGED. NO CALIBRATION WAS PERFORMED.**

| Constant/formula | Value |
|---|---:|
| Reference turn radius (m) | 24 |
| Reference turn speed (m/s) | 16 |
| Advanced quiet correction factor | 1.015 |
| Legacy Brake factor | 1.1 |
| Legacy RunWide factor | 1.3 |
| Advanced Brake factors | 1.06 -> 1.14 |
| Advanced RunWide factors | 1.18 -> 1.34 |
| RunWide retention | 0.35 -> 0.65 |
| TurnEntry scrub fraction | 0.5 |
| Corner correction deceleration (m/s²) | 2 -> 3.2 |
| Standing launch reference acceleration (m/s²) | 9 -> 11 |
| Reaction time (s) | 0.28 -> 0.2 |
| Straight reference acceleration (m/s²) | 0.8 -> 1.6 |
| TurnExit reference acceleration (m/s²) | 0.6 -> 1.4 |
| Nominal system mass (kg) | 142 |
| Resistance (N) | 40 + 0.2 × v² |
| Force fade | 0.0175 -> 0.005 |

## Interpretation and boundary

PR #34 changes the realization of recoverable corner constraints, not their thresholds. Correction uses the entry-sampled surface and entry `LateralPosition`; it does not resample per metre, change radius during lateral movement, add RNG, or model brakes, engine RPM, torque, clutch, wheelspin, banking, diagonal paths, contact redesign, or crash trajectories. Random incident probability, channels, severity, immediate 0.88 consequence, and crash semantics remain separate. No constants were tuned in response to these results.

Regenerate from the repository root with:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- continuous-corner-correction-impact-report data/calibration/pge/v1 docs/calibration/physical-width-impact.md docs/calibration/continuous-corner-correction-impact.md
```
