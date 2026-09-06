# Current production-model calibration baseline

This deterministic report measures the unchanged production model against the versioned PGEE telemetry envelope. It does not fit parameters, assign game skills to real riders, or produce an overall score.

## Dataset

| Measure | Count |
|---|---:|
| Downloaded source matches | 100 |
| Source telemetry details | 6021 |
| Source heat-result rows | 6772 |
| Source telemetry_full rows | 6789 |
| PGEE matches | 95 |
| Normalized PGEE rider-heats | 6373 |
| CompleteTelemetry | 5410 |
| CleanPhysics | 5328 |
| Eventful / non-steady complete | 82 |
| Audit-only | 963 |
| CompleteTelemetry matches | 93 |
| CompleteTelemetry riders | 97 |
| Four-rider CleanPhysics attempts | 1174 |

The task's approximate regression oracle said 5,417 CompleteTelemetry and 89 Eventful rows. Applying the exact stated rule produces 5,410 and 82: six otherwise steady PGEE rows contain source points `W`, which is not in `{0,1,2,3}`, and the remaining one-row difference is already present before the points condition. The filter was not changed to force the oracle.

CompleteTelemetry requires both presence flags, finite positive heat/L1/L2/L3/L4/Vmax/distance, points in `{0,1,2,3}`, and `abs(heat - sum(laps)) <= 0.05 s`. CleanPhysics additionally requires each of L2-L4 within ±10% of their median. Complete rows outside that heuristic are Eventful/non-steady; this is not a crash or invalidity claim. All other rows remain AuditOnly.

Physical attempt identity is `match_id + heat_uid`, preserving restarts such as `7734_2_0` and `7734_2_1`. Quantiles use linear interpolation at zero-based sorted position `(n - 1) * p`.

Split: 74 DEVELOPMENT matches and 19 newest FINAL_TEST matches (chronological `ceil(20%)`, match-level). No match is split between partitions or folds.

| Development fold | Matches | Clean rows |
|---:|---:|---:|
| 0 | 17 | 977 |
| 1 | 14 | 802 |
| 2 | 14 | 807 |
| 3 | 15 | 864 |
| 4 | 14 | 802 |

Track metadata coverage is zero in this v1 snapshot; the original source label is retained without inventing aliases or geometry.

## Real distributions

CleanPhysics observations define distributions/performance envelopes, never hard caps.

| Metric | Unit | N | P01 | P10 | P25 | P50 | P75 | P90 | P99 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Vmax | km/h | 5328 | 105.4 | 109.7 | 112 | 114.8 | 117.1 | 119.4 | 122.9 |
| Heat time | s | 5328 | 59.52948 | 60.889 | 62.26375 | 64.688 | 67.09975 | 68.345 | 69.76165 |
| L1 | s | 5328 | 16.3 | 16.7 | 17.06 | 17.68 | 18.36 | 18.74 | 19.1473 |
| L2 | s | 5328 | 14.3327 | 14.68 | 15.04 | 15.63 | 16.2 | 16.49 | 16.83 |
| L3 | s | 5328 | 14.36 | 14.7 | 15.04 | 15.64 | 16.22 | 16.5 | 16.84 |
| L4 | s | 5328 | 14.46 | 14.76 | 15.13 | 15.74 | 16.33 | 16.65 | 17.1746 |
| Flying-lap median | s | 5328 | 14.38 | 14.71 | 15.06 | 15.66 | 16.24 | 16.52 | 16.8573 |
| L1 penalty | s | 5328 | 1.6227 | 1.82 | 1.93 | 2.05 | 2.18 | 2.32 | 2.57 |
| Average speed | m/s | 5328 | 21.34302 | 22.023974 | 22.486805 | 22.980452 | 23.498926 | 23.968246 | 24.817001 |
| Total distance | m | 5328 | 1336 | 1382 | 1416 | 1490 | 1548 | 1599 | 1648 |

## Within-heat spreads

Each value is max minus min within an attempt containing exactly four CleanPhysics riders. These are distribution constraints, not per-heat equalities.

| Metric | Unit | N | P01 | P10 | P25 | P50 | P75 | P90 | P99 |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Four-rider heat-time spread | s | 1174 | 0.62179 | 0.9542 | 1.20625 | 1.5285 | 1.93025 | 2.2734 | 3.12664 |
| Four-rider Vmax spread | km/h | 1174 | 0.7 | 1.8 | 2.7 | 4.5 | 6.6 | 8.7 | 12 |
| Four-rider L1 spread | s | 1174 | 0.2 | 0.32 | 0.41 | 0.53 | 0.65 | 0.76 | 0.9827 |
| Four-rider average-speed spread | m/s | 1174 | 0.202341 | 0.431415 | 0.630739 | 0.927094 | 1.275998 | 1.658977 | 2.486604 |

## Rider-relative observations

The four-rider CleanPhysics attempts yield 4696 residual observations across 97 riders. Each residual is the rider value minus its heat mean; residuals therefore sum to approximately zero within every heat.

| Split-half metric | Eligible riders (>=12 observations) | Spearman rho |
|---|---:|---:|
| average_speed_residual_mps | 79 | 0.635979 |
| heat_time_residual_s | 79 | 0.848703 |
| l1_residual_s | 79 | 0.770695 |
| vmax_residual_kph | 79 | 0.429327 |

| Within-heat residual relationship | N | Spearman rho |
|---|---:|---:|
| average_speed_vs_heat_time | 4696 | -0.75301 |
| l1_vs_heat_time | 4696 | 0.88421 |
| vmax_vs_heat_time | 4696 | -0.415965 |

These are empirical performance fingerprints. They are not assignments of Start, Speed, SlideControl, or any other game skill to real riders.

## Literature reaction context

Values were supplied explicitly in the task specification for Markowski M, Szczepan S, Zatoń M, Martin S, Michalik K, *The importance of reaction time to the starting signal on race results in elite motorcycle speedway racing*, PLOS ONE 18(1): e0281138 (2023), DOI 10.1371/journal.pone.0281138.

| Population / phase | Mean reaction time | Reported dispersion |
|---|---:|---:|
| Senior riders | 0.246 s | ±0.050 s |
| Junior riders | 0.258 s | ±0.050 s |
| Main phase | 0.255 s | ±0.048 s |
| Knockout phase | 0.239 s | ±0.046 s |
| Semifinals | 0.229 s | ±0.044 s |

Definition: time from lifting of the starting tape to the first forward movement of the motorcycle; reported measurement accuracy 0.01 s. Approximately 80 km/h in 2.4 s is context/sanity only. Approximately 0.10-0.12 s is future false-start-rule context only.

The PDF is not part of the repository. No values were inferred from it. These observations do not create a junior penalty, senior bonus, age modifier, gate multiplier, reaction cap, or calibration equality target.

## Current production model: balanced fixture

Production fixture: `Track.CreateStandingStartExample()`, HoldLane decisions, incident frequency 0, seed 320032, dry weather, neutral setup (Gearing 0.5, TractionBias 0.5), perfect/neutral surface, and all six RiderSkills at 50. Skill 50 is not defined as an average PGEE rider.

| Rider | Reaction s | Movement s | TimeTo70 s | SpeedAt2s km/h | First curve km/h | L1 s | L2 s | L3 s | L4 s | Flying s | L1 penalty s | Vmax km/h | Distance m | Total s | Avg m/s | RunWide | Brake | Crash |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 0.24 | 2.802852 | 2.22934 | 62.371767 | 64.498192 | 17.379221 | 16.249582 | 16.360569 | 16.411255 | 16.360569 | 1.018652 | 72.423358 | 1123.185791 | 66.400627 | 16.915289 | 0 | 0 | 0 |
| 2 | 0.24 | 2.789077 | 2.22934 | 62.371767 | 65.82819 | 17.478563 | 16.342302 | 16.460217 | 16.517448 | 16.460217 | 1.018347 | 73.407575 | 1148.318359 | 66.798531 | 17.190773 | 0 | 0 | 0 |
| 3 | 0.24 | 2.776627 | 2.22934 | 62.371767 | 67.131869 | 17.579863 | 16.431417 | 16.550251 | 16.607941 | 16.550251 | 1.029612 | 74.218236 | 1173.451172 | 67.169472 | 17.470007 | 0 | 0 | 0 |
| 4 | 0.24 | 2.765228 | 2.22934 | 62.371767 | 68.410664 | 17.682589 | 16.516245 | 16.629654 | 16.681522 | 16.629654 | 1.052935 | 75.160252 | 1198.584106 | 67.51001 | 17.754169 | 0 | 0 | 0 |

| Scenario | Reaction s | TimeTo70 s | Vmax km/h | Vmax pct | Avg m/s | Avg pct | L1 penalty s | L1 penalty pct | Heat spread s | Vmax spread km/h | RunWide | Brake | Crash |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `balanced` | 0.24 | 2.22934 | 73.812906 | 0 | 17.33039 | 0 | 1.024132 | 0 | 1.109383 | 2.736893 | 0 | 0 | 0 |

## Start sweep

The named skill varies through 0/25/50/75/100 while all other RiderSkills stay at 50. Values are four-rider medians; percentiles are positions in the real PGEE distributions.

| Scenario | Reaction s | TimeTo70 s | Vmax km/h | Vmax pct | Avg m/s | Avg pct | L1 penalty s | L1 penalty pct | Heat spread s | Vmax spread km/h | RunWide | Brake | Crash |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `start_0` | 0.28 | 2.495008 | 73.812906 | 0 | 17.290507 | 0 | 1.178484 | 0 | 1.11393 | 2.736893 | 0 | 0 | 0 |
| `start_25` | 0.26 | 2.356128 | 73.812906 | 0 | 17.311168 | 0 | 1.098357 | 0 | 1.111526 | 2.736893 | 0 | 0 | 0 |
| `start_50` | 0.24 | 2.22934 | 73.812906 | 0 | 17.33039 | 0 | 1.024132 | 0 | 1.109383 | 2.736893 | 0 | 0 | 0 |
| `start_75` | 0.22 | 2.112852 | 73.812906 | 0 | 17.348381 | 0 | 0.954939 | 0 | 1.107483 | 2.736893 | 0 | 0 | 0 |
| `start_100` | 0.2 | 2.005352 | 73.812906 | 0 | 17.365282 | 0 | 0.890056 | 0 | 1.105774 | 2.736893 | 0 | 0 | 0 |

## Speed sweep

The named skill varies through 0/25/50/75/100 while all other RiderSkills stay at 50. Values are four-rider medians; percentiles are positions in the real PGEE distributions.

| Scenario | Reaction s | TimeTo70 s | Vmax km/h | Vmax pct | Avg m/s | Avg pct | L1 penalty s | L1 penalty pct | Heat spread s | Vmax spread km/h | RunWide | Brake | Crash |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `speed_0` | 0.24 | 2.22934 | 70.615407 | 0 | 16.161275 | 0 | 0.895885 | 0 | 1.153427 | 2.86428 | 0 | 0 | 0 |
| `speed_25` | 0.24 | 2.22934 | 71.771865 | 0 | 16.758768 | 0 | 0.967382 | 0 | 1.136459 | 2.513315 | 0 | 0 | 0 |
| `speed_50` | 0.24 | 2.22934 | 73.812906 | 0 | 17.33039 | 0 | 1.024132 | 0 | 1.109383 | 2.736893 | 0 | 0 | 0 |
| `speed_75` | 0.24 | 2.22934 | 76.600982 | 0 | 17.882148 | 0 | 1.070749 | 0 | 1.076538 | 2.87769 | 0 | 0 | 0 |
| `speed_100` | 0.24 | 2.22934 | 79.18968 | 0 | 18.417853 | 0 | 1.11051 | 0 | 1.043968 | 2.948971 | 0 | 0 | 0 |

## SlideControl sweep

The named skill varies through 0/25/50/75/100 while all other RiderSkills stay at 50. Values are four-rider medians; percentiles are positions in the real PGEE distributions.

| Scenario | Reaction s | TimeTo70 s | Vmax km/h | Vmax pct | Avg m/s | Avg pct | L1 penalty s | L1 penalty pct | Heat spread s | Vmax spread km/h | RunWide | Brake | Crash |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `slide_control_0` | 0.24 | 2.22934 | 71.681304 | 0 | 16.828265 | 0 | 0.986497 | 0 | 1.104332 | 2.687908 | 0 | 0 | 0 |
| `slide_control_25` | 0.24 | 2.22934 | 72.82566 | 0 | 17.084696 | 0 | 1.007334 | 0 | 1.109482 | 2.754066 | 0 | 0 | 0 |
| `slide_control_50` | 0.24 | 2.22934 | 73.812906 | 0 | 17.33039 | 0 | 1.024132 | 0 | 1.109383 | 2.736893 | 0 | 0 | 0 |
| `slide_control_75` | 0.24 | 2.22934 | 74.717496 | 0 | 17.567913 | 0 | 1.038215 | 0 | 1.105049 | 2.722797 | 0 | 0 | 0 |
| `slide_control_100` | 0.24 | 2.22934 | 75.957351 | 0 | 17.799085 | 0 | 1.050641 | 0 | 1.097588 | 2.662653 | 0 | 0 | 0 |

## Combined 27-point sweep

Start, Speed, and SlideControl each take 25/50/75; TrackReading, PairRiding, and Adaptability remain 50.

| Scenario | Reaction s | TimeTo70 s | Vmax km/h | Vmax pct | Avg m/s | Avg pct | L1 penalty s | L1 penalty pct | Heat spread s | Vmax spread km/h | RunWide | Brake | Crash |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `combined_start_25_speed_25_slide_25` | 0.26 | 2.361858 | 69.923117 | 0 | 16.507471 | 0 | 1.026933 | 0 | 1.137238 | 2.538474 | 0 | 0 | 0 |
| `combined_start_25_speed_25_slide_50` | 0.26 | 2.356128 | 71.323469 | 0 | 16.740994 | 0 | 1.04063 | 0 | 1.138458 | 2.529904 | 0 | 0 | 0 |
| `combined_start_25_speed_25_slide_75` | 0.26 | 2.356128 | 72.916356 | 0 | 16.96716 | 0 | 1.052367 | 0 | 1.135681 | 2.532541 | 0 | 0 | 0 |
| `combined_start_25_speed_50_slide_25` | 0.26 | 2.356128 | 72.82566 | 0 | 17.066345 | 0 | 1.080172 | 0 | 1.11161 | 2.754066 | 0 | 0 | 0 |
| `combined_start_25_speed_50_slide_50` | 0.26 | 2.356128 | 73.812906 | 0 | 17.311168 | 0 | 1.098357 | 0 | 1.111526 | 2.736893 | 0 | 0 | 0 |
| `combined_start_25_speed_50_slide_75` | 0.26 | 2.356128 | 74.717496 | 0 | 17.547827 | 0 | 1.113778 | 0 | 1.107208 | 2.722797 | 0 | 0 | 0 |
| `combined_start_25_speed_75_slide_25` | 0.26 | 2.356128 | 75.488022 | 0 | 17.605157 | 0 | 1.123848 | 0 | 1.079941 | 2.803045 | 0 | 0 | 0 |
| `combined_start_25_speed_75_slide_50` | 0.26 | 2.356128 | 76.600982 | 0 | 17.861428 | 0 | 1.146051 | 0 | 1.078804 | 2.87769 | 0 | 0 | 0 |
| `combined_start_25_speed_75_slide_75` | 0.26 | 2.356128 | 77.592518 | 0 | 18.108673 | 0 | 1.164949 | 0 | 1.075024 | 2.860188 | 0 | 0 | 0 |
| `combined_start_50_speed_25_slide_25` | 0.24 | 2.230454 | 70.022523 | 0 | 16.524435 | 0 | 0.955024 | 0 | 1.135262 | 2.517957 | 0 | 0 | 0 |
| `combined_start_50_speed_25_slide_50` | 0.24 | 2.22934 | 71.771865 | 0 | 16.758768 | 0 | 0.967382 | 0 | 1.136459 | 2.513315 | 0 | 0 | 0 |
| `combined_start_50_speed_25_slide_75` | 0.24 | 2.22934 | 73.372422 | 0 | 16.985727 | 0 | 0.977699 | 0 | 1.133659 | 2.514832 | 0 | 0 | 0 |
| `combined_start_50_speed_50_slide_25` | 0.24 | 2.22934 | 72.82566 | 0 | 17.084696 | 0 | 1.007334 | 0 | 1.109482 | 2.754066 | 0 | 0 | 0 |
| `combined_start_50_speed_50_slide_50` | 0.24 | 2.22934 | 73.812906 | 0 | 17.33039 | 0 | 1.024132 | 0 | 1.109383 | 2.736893 | 0 | 0 | 0 |
| `combined_start_50_speed_50_slide_75` | 0.24 | 2.22934 | 74.717496 | 0 | 17.567913 | 0 | 1.038215 | 0 | 1.105049 | 2.722797 | 0 | 0 | 0 |
| `combined_start_50_speed_75_slide_25` | 0.24 | 2.22934 | 75.488022 | 0 | 17.624928 | 0 | 1.049879 | 0 | 1.077599 | 2.803045 | 0 | 0 | 0 |
| `combined_start_50_speed_75_slide_50` | 0.24 | 2.22934 | 76.600982 | 0 | 17.882148 | 0 | 1.070749 | 0 | 1.076538 | 2.87769 | 0 | 0 | 0 |
| `combined_start_50_speed_75_slide_75` | 0.24 | 2.22934 | 77.592518 | 0 | 18.130322 | 0 | 1.088408 | 0 | 1.072754 | 2.860188 | 0 | 0 | 0 |
| `combined_start_75_speed_25_slide_25` | 0.22 | 2.112852 | 70.273093 | 0 | 16.540301 | 0 | 0.887964 | 0 | 1.133339 | 2.801012 | 0 | 0 | 0 |
| `combined_start_75_speed_25_slide_50` | 0.22 | 2.112852 | 72.03289 | 0 | 16.77539 | 0 | 0.899108 | 0 | 1.134483 | 2.841463 | 0 | 0 | 0 |
| `combined_start_75_speed_25_slide_75` | 0.22 | 2.112852 | 73.69542 | 0 | 17.003096 | 0 | 0.908235 | 0 | 1.131683 | 2.804171 | 0 | 0 | 0 |
| `combined_start_75_speed_50_slide_25` | 0.22 | 2.112852 | 72.82566 | 0 | 17.101862 | 0 | 0.93939 | 0 | 1.107613 | 2.754066 | 0 | 0 | 0 |
| `combined_start_75_speed_50_slide_50` | 0.22 | 2.112852 | 73.812906 | 0 | 17.348381 | 0 | 0.954939 | 0 | 1.107483 | 2.736893 | 0 | 0 | 0 |
| `combined_start_75_speed_50_slide_75` | 0.22 | 2.112852 | 74.874998 | 0 | 17.586712 | 0 | 0.967863 | 0 | 1.103149 | 2.634741 | 0 | 0 | 0 |
| `combined_start_75_speed_75_slide_25` | 0.22 | 2.112852 | 75.488022 | 0 | 17.643434 | 0 | 0.981075 | 0 | 1.075546 | 2.803045 | 0 | 0 | 0 |
| `combined_start_75_speed_75_slide_50` | 0.22 | 2.112852 | 76.600982 | 0 | 17.901537 | 0 | 1.000587 | 0 | 1.074516 | 2.87769 | 0 | 0 | 0 |
| `combined_start_75_speed_75_slide_75` | 0.22 | 2.112852 | 77.592518 | 0 | 18.150576 | 0 | 1.017008 | 0 | 1.070713 | 2.860188 | 0 | 0 | 0 |

## Model vs real percentile positions

Balanced-fixture P50 comparisons are component-wise. There is deliberately no combined accuracy score.

| Metric | Class | Unit | Real P50 | Model P50 | Signed gap | Relative gap | Model real-percentile |
|---|---|---|---:|---:|---:|---:|---:|
| Average speed | ComparableEnvelope | m/s | 22.980452 | 17.33039 | -5.650062 | -24.586384% | 0 |
| Flying-lap median | ContextOnlyUntilTrackGeometry | s | 15.66 | 16.505234 | 0.845234 | 5.397406% | 89.283033 |
| Heat time | ContextOnlyUntilTrackGeometry | s | 64.688 | 66.984001 | 2.296001 | 3.549346% | 73.536036 |
| L1 penalty | ComparableEnvelope | s | 2.05 | 1.024132 | -1.025868 | -50.042352% | 0 |
| L1 | ContextOnlyUntilTrackGeometry | s | 17.68 | 17.529213 | -0.150787 | -0.852868% | 43.843844 |
| L2 | ContextOnlyUntilTrackGeometry | s | 15.63 | 16.38686 | 0.75686 | 4.842354% | 85.003754 |
| L3 | ContextOnlyUntilTrackGeometry | s | 15.64 | 16.505234 | 0.865234 | 5.532185% | 90.334084 |
| L4 | ContextOnlyUntilTrackGeometry | s | 15.74 | 16.562695 | 0.822695 | 5.226776% | 86.655405 |
| Total distance | ContextOnlyUntilTrackGeometry | m | 1490 | 1160.884766 | -329.115234 | -22.088271% | 0 |
| Vmax | ComparableEnvelope | km/h | 114.8 | 73.812906 | -40.987094 | -35.703044% | 0 |
| Four-rider average-speed spread | ComparableEnvelope | m/s | 0.927094 | 0.838881 | -0.088213 | -9.515012% | 43.441227 |
| Four-rider heat-time spread | ComparableEnvelope | s | 1.5285 | 1.109383 | -0.419117 | -27.420175% | 19.250426 |
| Four-rider L1 spread | ComparableEnvelope | s | 0.53 | 0.303368 | -0.226632 | -42.760827% | 8.091993 |
| Four-rider Vmax spread | ComparableEnvelope | km/h | 4.5 | 2.736893 | -1.763107 | -39.180145% | 25.383305 |

## Metrics not yet directly comparable

- **ComparableEnvelope:** Vmax, average speed, L1 penalty, and four-rider spreads. They are envelopes, not equality targets or caps.
- **ContextOnlyUntilTrackGeometry:** absolute heat/L1/L2/L3/L4/flying times and total distance. The example simulation track is not a fitted representation of every PGEE track.
- **UnsupportedNumericByCurrentSource:** individual reaction time, SpeedAt2s, and first-curve speed. Source `speed_2s` and `curve_speed` fields are gate rankings, not physical rider speeds. Reaction has separate literature context only.

## Main gaps detected

- Average speed: balanced model P50 17.33039 m/s, real P50 22.980452 m/s, percentile 0 — below the real P10 envelope.
- L1 penalty: balanced model P50 1.024132 s, real P50 2.05 s, percentile 0 — below the real P10 envelope.
- Vmax: balanced model P50 73.812906 km/h, real P50 114.8 km/h, percentile 0 — below the real P10 envelope.
- Four-rider L1 spread: balanced model P50 0.303368 s, real P50 0.53 s, percentile 8.091993 — below the real P10 envelope.
- Four-rider heat-time spread: balanced model P50 1.109383 s, real P50 1.5285 s, percentile 19.250426 — inside the real P10-P90 envelope.
- Four-rider Vmax spread: balanced model P50 2.736893 km/h, real P50 4.5 km/h, percentile 25.383305 — inside the real P10-P90 envelope.
- Four-rider average-speed spread: balanced model P50 0.838881 m/s, real P50 0.927094 m/s, percentile 43.441227 — inside the real P10-P90 envelope.

This is a measurement result for review, not a recommendation or an automatically selected set of constants. Physics calibration belongs to PR #33.

## Physics invariant

**NO PHYSICS CONSTANTS WERE CHANGED IN PR #32.** The sweep uses `CalibrationRunner -> HeatSimulator` with production physics. Calibration diagnostics are observational only.

Regenerate from the repository root with:

```text
dotnet run --project src/Sandbox/Sandbox.csproj --configuration Release -- calibration-report data/calibration/pge/v1 docs/calibration/current-model-baseline.md
```
