# Single-rider executed trajectory — Motoarena

Base: `83a67616973e5d4fbbc8060525ec5ffc629da734` after #52. Analysis only; no coefficients changed.

One rider, neutral setup, six default skills, clean uniform surface reset before each segment, incidents OFF, traffic OFF. Actual SimulationEngine CaptureSnapshot → Decide → Resolve → Commit. Corner segments 1–3 and the 62 m following straight; its immediate next corner preparation remains active. The following straight requests the exit target. Requested integer targets are subject to the unchanged nearest-reference planner and bounded movement law.

| intent | actual entry | entry/middle/exit requests |
| --- | ---: | --- |
| inside-hold | 0 | 0/0/0 |
| mid-hold | 2 | 2/2/2 |
| outside-hold | 4 | 4/4/4 |
| inside-open | 0 | 0/0/4 |
| mid-open | 2 | 2/1/4 |
| wide-tight | 4 | 1/1/0 |
| wide-tight-open | 3 | 1/1/4 |

## Entry 19 m/s

| trajectory | actual entry/apex/exit | path m | Δ vs inside m | corner s | min/apex/exit m/s | radius min/apex/max m | max curvature 1/m | next straight s | next exit m/s | combined s |
| --- | --- | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: |
| inside-hold | 0.000/0.000/0.000 | 97.389 | 0.000 | 5.0132 | 19.000/19.000/21.448 | 31.000/31.000/31.000 | 0.032258 | 2.6384 | 25.321 | 7.6516 |
| mid-hold | 2.000/2.000/2.000 | 120.323 | 22.934 | 6.1672 | 19.000/19.000/21.928 | 38.300/38.300/38.300 | 0.026110 | 2.5968 | 25.607 | 8.7640 |
| outside-hold | 4.000/4.000/4.000 | 143.257 | 45.867 | 7.3132 | 19.000/19.000/22.377 | 45.600/45.600/45.600 | 0.021930 | 2.5588 | 25.880 | 9.8720 |
| inside-open | 0.000/0.000/0.840 | 99.140 | 1.750 | 5.0948 | 19.000/19.000/21.557 | 31.000/31.000/34.067 | 0.032258 | 2.6293 | 25.386 | 7.7241 |
| mid-open | 2.000/1.483/1.932 | 116.662 | 19.272 | 5.9888 | 19.000/19.000/21.813 | 34.650/36.413/38.300 | 0.028860 | 2.6069 | 25.539 | 8.5957 |
| wide-tight | 4.000/2.434/1.064 | 126.046 | 28.657 | 6.4797 | 19.000/19.000/21.828 | 34.884/39.883/45.600 | 0.028667 | 2.6056 | 25.547 | 9.0853 |
| wide-tight-open | 3.000/1.483/1.932 | 118.533 | 21.143 | 6.0873 | 19.000/19.000/21.813 | 34.650/36.413/41.950 | 0.028860 | 2.6069 | 25.539 | 8.6942 |

Fastest combined control: **inside-hold**, 7.6516 s.

inside-open vs inside-hold: exit speed Δ +0.109 m/s; combined time Δ +0.0725 s. Actual apex local safe capability: 21.594 m/s.

mid-open vs mid-hold: exit speed Δ -0.115 m/s; combined time Δ -0.1683 s. Actual apex local safe capability: 23.403 m/s.

## Entry 22 m/s

| trajectory | actual entry/apex/exit | path m | Δ vs inside m | corner s | min/apex/exit m/s | radius min/apex/max m | max curvature 1/m | next straight s | next exit m/s | combined s |
| --- | --- | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: |
| inside-hold | 0.000/0.000/0.000 | 97.389 | 0.000 | 4.4042 | 21.594/21.594/23.406 | 31.000/31.000/31.000 | 0.032258 | 2.4751 | 26.515 | 6.8793 |
| mid-hold | 2.000/2.000/2.000 | 120.323 | 22.934 | 5.3798 | 22.000/22.000/24.069 | 38.300/38.300/38.300 | 0.026110 | 2.4236 | 26.933 | 7.8033 |
| outside-hold | 4.000/4.000/4.000 | 143.257 | 45.867 | 6.3882 | 22.000/22.000/24.398 | 45.600/45.600/45.600 | 0.021930 | 2.3986 | 27.143 | 8.7869 |
| inside-open | 0.000/0.000/0.753 | 98.938 | 1.548 | 4.4704 | 21.594/21.594/23.479 | 31.000/31.000/33.748 | 0.032258 | 2.4699 | 26.562 | 6.9403 |
| mid-open | 2.000/1.552/1.959 | 117.128 | 19.739 | 5.2414 | 22.000/22.000/23.996 | 35.107/36.667/38.300 | 0.028485 | 2.4292 | 26.887 | 7.6707 |
| wide-tight | 4.000/2.510/2.000 | 128.218 | 30.829 | 5.7382 | 22.000/22.000/24.072 | 38.300/40.161/45.600 | 0.026110 | 2.4256 | 26.938 | 8.1638 |
| wide-tight-open | 3.000/2.000/2.907 | 124.305 | 26.916 | 5.5535 | 22.000/22.000/24.148 | 38.300/38.300/41.950 | 0.026110 | 2.4177 | 26.984 | 7.9712 |

Fastest combined control: **inside-hold**, 6.8793 s.

inside-open vs inside-hold: exit speed Δ +0.073 m/s; combined time Δ +0.0610 s. Actual apex local safe capability: 21.594 m/s.

mid-open vs mid-hold: exit speed Δ -0.072 m/s; combined time Δ -0.1327 s. Actual apex local safe capability: 23.485 m/s.

## Entry 25 m/s

| trajectory | actual entry/apex/exit | path m | Δ vs inside m | corner s | min/apex/exit m/s | radius min/apex/max m | max curvature 1/m | next straight s | next exit m/s | combined s |
| --- | --- | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: |
| inside-hold | 0.000/0.000/0.000 | 97.389 | 0.000 | 4.2264 | 21.594/21.594/23.406 | 31.000/31.000/31.000 | 0.032258 | 2.4751 | 26.515 | 6.7015 |
| mid-hold | 2.000/2.000/2.000 | 120.323 | 22.934 | 4.8631 | 24.002/24.002/25.569 | 38.300/38.300/38.300 | 0.026110 | 2.3134 | 27.903 | 7.1765 |
| outside-hold | 4.000/4.000/4.000 | 143.257 | 45.867 | 5.6671 | 25.000/25.000/26.553 | 45.600/45.600/45.600 | 0.021930 | 2.2457 | 28.555 | 7.9128 |
| inside-open | 0.000/0.000/0.753 | 98.938 | 1.548 | 4.2925 | 21.594/21.594/23.479 | 31.000/31.000/33.748 | 0.032258 | 2.4699 | 26.562 | 6.7625 |
| mid-open | 2.000/1.600/2.000 | 117.470 | 20.081 | 4.8285 | 23.239/23.804/24.800 | 35.359/36.840/38.300 | 0.028281 | 2.3709 | 27.404 | 7.1994 |
| wide-tight | 4.000/3.000/2.167 | 132.465 | 35.076 | 5.2670 | 25.000/25.000/25.750 | 38.908/41.950/45.600 | 0.025701 | 2.3010 | 28.023 | 7.5680 |
| wide-tight-open | 3.000/2.000/2.841 | 124.408 | 27.018 | 5.0249 | 24.002/24.002/25.625 | 38.300/38.300/41.950 | 0.026110 | 2.3098 | 27.940 | 7.3347 |

Fastest combined control: **inside-hold**, 6.7015 s.

inside-open vs inside-hold: exit speed Δ +0.073 m/s; combined time Δ +0.0610 s. Actual apex local safe capability: 21.594 m/s.

mid-open vs mid-hold: exit speed Δ -0.768 m/s; combined time Δ +0.0229 s. Actual apex local safe capability: 23.540 m/s.

## Exact #51 first-segment control

Scenario D, rider 2 alone, 21 m/s, balanced skills, original 24 m inner radius and 14 m turn width, original uniform surface, incidents OFF. Target 3 vs 0; nearest-reference planning is unchanged. The historical report remains frozen; this is a new current-production replay.

| control | actual entry/exit | distance m | time s | exit m/s | radius min/max m | nodes |
| --- | --- | ---: | ---: | ---: | --- | ---: |
| entry-hold | 3.000000/3.000000 | 34.557522 | 1.645596 | 21.000000 | 33.000000/33.000000 | 36 |
| entry-inward | 3.000000/2.205268 | 33.378830 | 1.589468 | 21.000000 | 30.615805/33.000000 | 35 |

## Mechanism and limits

Changing executed trajectories change physical distance, local radius/curvature, sampled surface and the consumed longitudinal steps. Inside-hold retains the shortest path. Opening is bounded: requested targets are not achieved trajectories. The JSON appendix retains every segment's requested/resolved target and actual endpoint, plus actual apex capability. Nonuniform surface-following and wear-budget controls are tested separately.

**geometry/motion consistency alone does not produce an opening-line advantage** in these controls. No additional experiments or tuning were applied.

The local moving envelope uses current radius/surface as a pointwise fixed-line continuation forecast. It shares ContinuousCornerEnvelope and LongitudinalDynamics primitives; it never substitutes an entry or mean radius for executed geometry. Future intent is not predicted. Apex stays at 0.5. Width mapping is segment-local (12/16.6 m); boundary offset changes add no teleport distance and no width-transition spline. Contact/common-time interactions and AdaptiveDecisionModel remain separate future work. Historical #47–#51 reports are frozen.
