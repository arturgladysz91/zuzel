# Physical track-width impact

This deterministic observation compares the historical PR #32 production baseline with the PR #33 segment-local physical-width geometry. It uses the unchanged versioned PGEE dataset, `RealWorldCalibrationEvaluator`, and the production-path `CalibrationSkillSweep`; it does not tune any model constant.

## Geometry

`LateralPosition` remains a dimensionless continuous coordinate from 0 to 4. The old effective reference used 1 m between every reference position. The new conversion derives physical metres from the current segment width, with a 1 m FIM inner measurement/reference offset and a separate provisional 1 m game margin at the outer edge.

| Measure | Before | After |
|---|---:|---:|
| Straight physical width | 6 m effective compatibility width | 10 m |
| Turn physical width | 6 m effective compatibility width | 14 m |
| Straight usable span | 4 m | 8 m |
| Turn usable span | 4 m | 12 m |
| Straight reference spacing | 1 m | 2 m |
| Turn reference spacing | 1 m | 3 m |
| Turn radii at positions 0/1/2/3/4 | 24 / 25 / 26 / 27 / 28 m | 24 / 27 / 30 / 33 / 36 m |

The standing example's 10 m straight and 14 m turn are an FIM-minimum-width example, not global dimensions for every real track. `CreateExample()` remains a 6 m / 6 m synthetic compatibility fixture.

## Lap distances

The before column reconstructs the historical effective geometry `130 + 2π × (24 + LateralPosition)`. The after column sums the current physical segments. Position 0 remains the canonical 1 m inner measurement/reference trajectory; position 4 is not an official track-length measurement.

| LateralPosition | Before m | After m | Delta m |
|---:|---:|---:|---:|
| 0 | 280.796452 | 280.796452 | 0 |
| 1 | 287.079636 | 299.646004 | 12.566368 |
| 2 | 293.36282 | 318.495567 | 25.132748 |
| 3 | 299.646004 | 337.345131 | 37.699127 |
| 4 | 305.929199 | 356.194672 | 50.265472 |

## Balanced production fixture

The fixture uses HoldLane decisions, incidents disabled, seed 320032, dry weather, neutral setup, perfect/neutral surface, and all six skills at 50.

| Metric | Before | After | Delta |
|---|---:|---:|---:|
| Vmax median (km/h) | 73.812906 | 76.440358 | 2.627452 |
| Average-speed median (m/s) | 17.33039 | 18.142772 | 0.812382 |
| L1-penalty median (s) | 1.024132 | 1.056545 | 0.032413 |
| Total-heat-time median (s) | 66.984001 | 68.129131 | 1.14513 |
| Four-rider heat-time spread (s) | 1.109383 | 3.473694 | 2.364311 |
| Four-rider Vmax spread (km/h) | 2.736893 | 7.702803 | 4.96591 |
| Four-rider L1 spread (s) | 0.303368 | 0.941988 | 0.63862 |
| Four-rider average-speed spread (m/s) | 0.838881 | 2.396242 | 1.557361 |
| RunWide count | 0 | 0 | 0 |
| Brake count | 0 | 0 | 0 |
| Crash count | 0 | 0 | 0 |

## Selected skill-sweep percentile positions

Percentiles locate each four-rider model median in the real PGEE distribution. They are observations, not targets or hard caps.

| Scenario | Vmax before / after km/h | Vmax percentile before / after | Average before / after m/s | Average percentile before / after | L1 penalty before / after s | L1 percentile before / after |
|---|---:|---:|---:|---:|---:|---:|
| `speed_0` | 70.615407 / 73.185284 | 0 / 0 | 16.161275 / 16.923858 | 0 / 0 | 0.895885 / 0.924995 | 0 / 0 |
| `speed_50` | 73.812906 / 76.440358 | 0 / 0 | 17.33039 / 18.142772 | 0 / 0 | 1.024132 / 1.056545 | 0 / 0 |
| `speed_100` | 79.18968 / 82.043818 | 0 / 0 | 18.417853 / 19.281636 | 0 / 0 | 1.11051 / 1.147342 | 0 / 0 |
| `slide_control_0` | 71.681304 / 74.31656 | 0 / 0 | 16.828265 / 17.625194 | 0 / 0 | 0.986497 / 1.018971 | 0 / 0 |
| `slide_control_50` | 73.812906 / 76.440358 | 0 / 0 | 17.33039 / 18.142772 | 0 / 0 | 1.024132 / 1.056545 | 0 / 0 |
| `slide_control_100` | 75.957351 / 78.482967 | 0 / 0 | 17.799085 / 18.628987 | 0 / 0 | 1.050641 / 1.084136 | 0 / 0 |

## Balanced lateral-traversal diagnostic

The diagnostic uses the perfect/neutral surface from the controlled fixture, so the unchanged grip multiplier is 1.

| Measure | Value |
|---|---:|
| Normalized traversal rate | 0.5 lane-units/s |
| Physical equivalent on 10 m straight | 1 m/s |
| Physical equivalent on 14 m turn | 1.5 m/s |
| Lane 0 -> Lane 1 time on straight | 2 s |
| Lane 0 -> Lane 1 time on turn | 2 s |

Formula: `execution = 0.5 × SlideControlNorm + 0.5 × AdaptabilityNorm`; `laneRate = 0.35 + (0.65 - 0.35) × execution`; `maxLaneDelta = laneRate × time × gripMultiplier`; `physicalMeters = maxLaneDelta × localReferenceSpacing`. Values 0.35-0.65 were not tuned.

## Physics invariant

**NO SPEED/PERFORMANCE CONSTANTS CHANGED. NO CALIBRATION WAS PERFORMED.** The checked production constants remain:

| Constant/formula | Before | After |
|---|---:|---:|
| `SegmentPhysics.ReferenceTurnRadiusMeters` | 24 | 24 |
| `SegmentPhysics.ReferenceTurnSpeedMetersPerSecond` | 16 | 16 |
| `SegmentPhysics.BrakeSpeedFactor` | 1.1 | 1.1 |
| `SegmentPhysics.RunWideSpeedFactor` | 1.3 | 1.3 |
| `standing launch acceleration (m/s²)` | 9-11 | 9-11 |
| `reaction time (s)` | 0.28-0.2 | 0.28-0.2 |
| `Straight acceleration (m/s²)` | 0.8-1.6 | 0.8-1.6 |
| `TurnExit acceleration (m/s²)` | 0.6-1.4 | 0.6-1.4 |
| `corner preparation deceleration (m/s²)` | 2-3.2 | 2-3.2 |
| `nominal system mass (kg)` | 142 | 142 |
| `resistance (N)` | 40 + 0.2 × v² | 40 + 0.2 × v² |
| `force fade` | 0.0175 -> 0.005 | 0.0175 -> 0.005 |
| `TurnEntry scrub fraction` | 0.5 | 0.5 |
| `turn control multiplier` | 0.97 + control × 0.06 | 0.97 + control × 0.06 |
| `turn speed multiplier` | 0.94 + speedAbility × 0.12 | 0.94 + speedAbility × 0.12 |
| `turn surface multiplier` | 0.74 + effectiveGrip × 0.26 | 0.74 + effectiveGrip × 0.26 |
| `turn setup multiplier` | 1.03 - setupError × 0.06 | 1.03 - setupError × 0.06 |

## Interpretation and boundaries

Wider normalized positions now have longer arcs and larger curvature radii, while physical occupancy/contact and displacement use the local segment width. Changes in timing, speed, incidents, and real-data percentile position are therefore geometry observations, not a reason to tune this PR.

The five surface bands remain normalized and their wear/grip formulas are unchanged. Segment boundaries reinterpret the same normalized position against the local width without a synthetic lateral event, width-transition spline, or additional distance/time. Active lateral movement still has no diagonal/spiral path-length correction. Physical A/B/C/D starting gates, rider/motorcycle width, and calibration remain out of scope.

Regenerate from the repository root with:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- physical-width-impact-report data/calibration/pge/v1 docs/calibration/current-model-baseline.md docs/calibration/physical-width-impact.md
```
