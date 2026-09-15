# Motoarena matched-venue calibration foundation (#39)

## A. Provenance

Base main SHA: `de0dabee70e1248278f6cc5ff4d7293f1501b17c`.
Frozen production physics version: `#38 — Calibrate continuous corner speed envelope`.
Dataset: `pge-v1`. Exact selector: `season == 2026 && league == "PGEE" && source_track_label == "Motoarena im. Mariana Rosego"`.
The selector uses ordinal label equality. It does not infer the 2025 `Toruń` label, home-team aliases, or venue aliases.
Current 2026 primary source: Speedway Ekstraliga, “PRES GRUPA DEWELOPERSKA Toruń — info — 2026”, used for track length, current straight/bend widths and the 56.500 s record. Supporting geometry/history source: Speedway Ekstraliga, “Czy podczas PGE IMME im. Zenona Plecha padnie nowy rekord toru?”, used for 62 m straights, 31 m radius and the pre-2017 second-corner history. The task-supplied source observations are stored as calibration evidence; tests and report generation are offline.

## B. Published geometry

| Field | Value | Evidence classification |
| --- | --- | --- |
| Published track length | 318 m | ExternalPublished |
| Straight length | 62 m each | ExternalPublished |
| Bend radius | 31 m | ExternalPublishedRadius / MeasurementConventionNotExplicitlyVerified |
| Starting / back straight width | 12.0 / 12.0 m | Current2026ExternalPublished |
| First / second bend width | 17.0 / 16.2 m | Current2026ExternalPublishedAsymmetric |
| Modeled symmetric turn width | 16.6 m | DerivedSymmetricWidthApproximationFromCurrentPublishedBendWidths |
| Older article general bend width | 18.0 m | OlderArticleReferenceOnly |
| Surface | granite | ExternalPublished |
| Historical record | 56.500 s — Jack Holder, 2017 | HistoricalContext only; not a Skill100 target |
Current 2026 published widths are 12 m on both straights and 17.0/16.2 m on the first/second bends. The model uses their 16.6 m arithmetic mean as a DerivedSymmetricWidthApproximationFromCurrentPublishedBendWidths. This is a SymmetricGeometryApproximation. The 31 m radius remains an ExternalPublishedRadius / MeasurementConventionNotExplicitlyVerified reference-radius approximation. This preserves aggregate full-lap lateral path contribution in the symmetric model, not local corner radius, safe speed, banking, or asymmetry.
Source conflict: the older/general article states 18 m bends, while the current official 2026 venue card states 17.0 m for the first bend and 16.2 m for the second. The current 2026 values take precedence; 18 m is retained only as `OlderArticleReferenceOnly` sensitivity context.
The 16.6 m modeled width is the arithmetic mean `(17.0 + 16.2) / 2`. For two semicircular bends at the same normalized lateral position, linear lateral-radius offset means this proxy preserves the two bends' combined full-lap path-length contribution in the current symmetric representation. It does not preserve either bend's local radius, local safe speed, banking, or first/second-corner asymmetry.
The source reports that the second corner geometry was changed before the 2017 season and that the track length has been 318 m since that change.
The published radius is used as the model's reference-radius approximation because two 62 m straights plus two semicircles at 31 m closely reproduce the published length. This is not a verified equality of measurement conventions or a geodetic reconstruction.

## C. Modeled geometry

Topology: marked Straight start-half → TurnEntry → TurnMiddle → TurnExit → 62 m back Straight → TurnEntry → TurnMiddle → TurnExit → finish-half Straight. Each logical corner is three 60° reporting subsegments and totals 180°.
The two corners use `SymmetricGeometryApproximation` with a primary modeled turn width of 16.6 m. The production model does not represent the current 17.0/16.2 m width asymmetry or the reported second-corner shape change. Known missing venue physics: banking.
Width envelope check: true. Modeled L0 lap = 318.778748 m; difference to published 318 m = +0.778748 m (+0.244889%). The published 31 m input is not adjusted to force equality.
| Lateral reference | Modeled lap distance m | Difference vs L0 m |
| --- | --- | --- |
| 0 | 318.7787 | 0 |
| 1 | 341.7124 | 22.93362 |
| 2 | 364.646 | 45.86725 |
| 3 | 387.5797 | 68.8009 |
| 4 | 410.5132 | 91.7345 |

| Symmetric turn width m | Classification | L0 m | L1 m | L2 m | L3 m | L4 m | Vmax P50 km/h | AverageSpeed P50 m/s | Flying P50 s | HeatTime P50 s | TotalDistance P50 m |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 16.2 | CurrentPublishedBendWidthSensitivity | 318.7787 | 341.084 | 363.3894 | 385.6947 | 408 | 98.900982 | 24.322718 | 14.074257 | 57.912996 | 1408.946716 |
| 16.6 | DerivedSymmetricWidthApproximationFromCurrentPublishedBendWidths / PRIMARY | 318.7787 | 341.7124 | 364.646 | 387.5797 | 410.5132 | 98.999028 | 24.358277 | 14.089063 | 57.982407 | 1412.716797 |
| 17 | CurrentPublishedBendWidthSensitivity | 318.7787 | 342.3407 | 365.9026 | 389.4646 | 413.0266 | 99.097644 | 24.393593 | 14.103806 | 58.052202 | 1416.487061 |
| 18 | OlderArticleReferenceOnly | 318.7787 | 343.9115 | 369.0443 | 394.177 | 419.3097 | 99.356544 | 24.482094 | 14.139977 | 58.225178 | 1425.911316 |
Width sensitivity is calibration-only. The 16.6 m row is primary; 16.2/17.0 m bound the current published bend widths, and 18.0 m is `OlderArticleReferenceOnly`.

| Fixed line | Lap distance m | Four-lap synthetic distance m | Real P10–P90 status |
| --- | --- | --- | --- |
| 0 | 318.7787 | 1275.115112 | below real P10 |
| 1 | 341.7124 | 1366.849609 | inside real P10–P90 |
| 2 | 364.646 | 1458.583984 | above real P90 |
| 3 | 387.5797 | 1550.318481 | above real P90 |
| 4 | 410.5132 | 1642.052734 | above real P90 |
Real TotalDistance P10/P25/P50/P75/P90 = 1335.6 / 1352.5 / 1377 / 1400 / 1418 m. Fixed normalized line(s) inside P10–P90: 1. Linear interpolation places real P50 near fixed normalized line 1.11065. Real lateral-path occupancy and telemetry distance convention are unknown, so line-distance plausibility remains unresolved.

## D. Dataset subset coverage

| Coverage item | Count/value |
| --- | --- |
| Matches | 7 |
| Exact match IDs | 6950, 6956, 6961, 6970, 6979, 6980, 6995 |
| Raw rider-heat rows | 456 |
| CompleteTelemetry | 415 |
| CleanPhysics | 407 |
| Eventful | 8 |
| AuditOnly | 41 |
| Full four-rider CleanPhysics attempts | 93 |

## E. Real Motoarena distributions

| Metric | Unit | N | P10 | P25 | P50 | P75 | P90 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| pge_clean_vmax | km/h | 407 | 108.6 | 111 | 113.7 | 115.5 | 116.9 |
| pge_clean_average_speed | m/s | 407 | 21.472577 | 21.919482 | 22.384854 | 22.861938 | 23.357308 |
| pge_clean_heat_time | s | 407 | 60.24 | 60.8505 | 61.479 | 62.068 | 62.587 |
| pge_clean_l1_time | s | 407 | 16.52 | 16.695 | 16.84 | 17.03 | 17.17 |
| pge_clean_l2_time | s | 407 | 14.52 | 14.68 | 14.85 | 15 | 15.13 |
| pge_clean_l3_time | s | 407 | 14.556 | 14.69 | 14.85 | 14.985 | 15.13 |
| pge_clean_l4_time | s | 407 | 14.61 | 14.73 | 14.9 | 15.06 | 15.23 |
| pge_clean_flying_lap_median | s | 407 | 14.56 | 14.71 | 14.87 | 15.01 | 15.15 |
| pge_clean_l1_penalty | s | 407 | 1.79 | 1.89 | 1.99 | 2.105 | 2.198 |
| pge_clean_total_distance | m | 407 | 1335.6 | 1352.5 | 1377 | 1400 | 1418 |

| Four-rider spread | Unit | N | P10 | P25 | P50 | P75 | P90 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| pge_four_rider_vmax_spread | km/h | 93 | 1.7 | 2.4 | 4.6 | 6.5 | 9.1 |
| pge_four_rider_average_speed_spread | m/s | 93 | 0.41176 | 0.638378 | 0.856879 | 1.211347 | 1.443082 |
| pge_four_rider_heat_time_spread | s | 93 | 0.9846 | 1.205 | 1.589 | 1.878 | 2.1386 |
| pge_four_rider_l1_spread | s | 93 | 0.254 | 0.33 | 0.45 | 0.56 | 0.64 |

## F. Synthetic fixture

Production path: `CalibrationRunner → HeatSimulator`; venue `pge-2026-motoarena-torun`; primary modeled symmetric turn width 16.6 m; primary provisional start split 31/31 m; four riders with all six skills 50; neutral setup; fixed normalized lines 0–3; HoldLane; baseline surface `(grip=1, ruts=0, moisture=0.35)`; Dry weather; incidents off; seed 390039; four laps.
Skill50 is a game-scale fixture, not an average PGEE rider. No real rider receives synthetic skills.
| Rider | Lane | Vmax km/h | Vmax location | Average m/s | L1 s | L2 s | L3 s | L4 s | Flying median s | L1 penalty s | HeatTime s | Distance m | Brake | RunWide | Crash | Corner min m/s | Corner entry/exit m/s |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 0 | 95.321489 | Straight 4 | 22.91321 | 15.02274 | 13.46999 | 13.55793 | 13.59912 | 13.55793 | 1.464814 | 55.64978 | 1275.115 | 0 | 0 | 0 | 21.12575 | 26.268127 / 23.114517 |
| 2 | 1 | 97.882807 | Straight 4 | 23.90799 | 15.48868 | 13.81536 | 13.90991 | 13.95731 | 13.90991 | 1.578773 | 57.17126 | 1366.85 | 0 | 0 | 0 | 22.30614 | 26.961128 / 24.211729 |
| 3 | 2 | 100.11525 | Straight 0 | 24.80857 | 16.03755 | 14.17225 | 14.26822 | 14.31554 | 14.26822 | 1.769327 | 58.79356 | 1458.584 | 0 | 0 | 0 | 23.45158 | 27.618839 / 25.225016 |
| 4 | 3 | 102.44212 | Straight 0 | 25.5988 | 16.7376 | 14.53096 | 14.62417 | 14.66942 | 14.62417 | 2.11343 | 60.56215 | 1550.318 | 0 | 0 | 0 | 23.63901 | 28.209831 / 26.131132 |
All 24 input-order permutations were executed. Trace hashes: 1 distinct across 24 permutations.

## G. Standing-start split sensitivity

ProvisionalStartLineSplit: no verified start-line offset is available; the primary fixture uses 31 m / 31 m and reports 25/37 and 37/25 sensitivity cases.
The three cases preserve the same 62 m home straight, 62 m back straight, two 180° corners and every flying-lap path length. The split is not tuned to telemetry. L1 remains `StartLineSensitiveContext`.
| Start→corner m | Finish-half m | Lap distance L1 m | L1 P50 s | Flying median P50 s | HeatTime P50 s | First-corner entry P50 m/s | Vmax P50 m/s |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 25 | 37 | 341.7124 | 16.229115 | 14.090119 | 58.451157 | 21.586765 | 27.477575 |
| 31 | 31 | 341.7124 | 15.763113 | 14.089063 | 57.982407 | 23.639006 | 27.49973 |
| 37 | 25 | 341.7124 | 15.616614 | 14.087967 | 57.83305 | 25.393847 | 27.523172 |

## H. Synthetic standing example vs Motoarena geometry

| Metric | Synthetic #38 standing example | Motoarena geometry / same rider fixture | Motoarena - standing |
| --- | --- | --- | --- |
| Vmax km/h | 93.680022 | 98.999028 | 5.319007 |
| AverageSpeed m/s | 22.218422 | 24.358277 | 2.139855 |
| L2 s | 13.482242 | 13.993808 | 0.511566 |
| L3 s | 13.574922 | 14.089063 | 0.514141 |
| L4 s | 13.622425 | 14.136423 | 0.513998 |
| FlyingLapMedian s | 13.574922 | 14.089063 | 0.514141 |
| HeatTime s | 55.630981 | 57.982407 | 2.351425 |
| TotalDistance m | 1236.283203 | 1412.716797 | 176.433594 |

## I. Motoarena synthetic vs Motoarena real telemetry

| Metric | Synthetic #38 standing example | Motoarena synthetic | Real Motoarena P10 | P50 | P90 | Synthetic - real P50 |
| --- | --- | --- | --- | --- | --- | --- |
| Vmax km/h | 93.680022 | 98.999028 | 108.6 | 113.7 | 116.9 | -14.700972 |
| AverageSpeed m/s | 22.218422 | 24.358277 | 21.472577 | 22.384854 | 23.357308 | 1.973423 |
| L2 s | 13.482242 | 13.993808 | 14.52 | 14.85 | 15.13 | -0.856192 |
| L3 s | 13.574922 | 14.089063 | 14.556 | 14.85 | 15.13 | -0.760937 |
| L4 s | 13.622425 | 14.136423 | 14.61 | 14.9 | 15.23 | -0.763577 |
| FlyingLapMedian s | 13.574922 | 14.089063 | 14.56 | 14.87 | 15.15 | -0.780937 |
| HeatTime s | 55.630981 | 57.982407 | 60.24 | 61.479 | 62.587 | -3.496593 |
| TotalDistance m | 1236.283203 | 1412.716797 | 1335.6 | 1377 | 1418 | 35.716797 |
Vmax and AverageSpeed are compared as distributions, not equality targets. L2/L3/L4, FlyingLapMedian, HeatTime and TotalDistance are now venue-matched context, while L1/L1Penalty remain start-line-sensitive.

## J. Flying-lap speed profile

Balanced Speed50 rider on Lateral1, L2. Corner values are interpolated between actual production traversal nodes. Straight quarter points use energy-space interpolation across the actual production profile's measured acceleration/cruise/deceleration phase distances and exact entry/peak/exit speeds; this is an observation projection, not a second physics path.
| Point | Cumulative distance m | Segment | CornerProgress | Actual speed m/s | Local envelope m/s | Drive availability |
| --- | --- | --- | --- | --- | --- | --- |
| home-start/000% | 0 | Straight | — | 25.94392 | — | — |
| home-start/025% | 7.75 | Straight | — | 26.24912 | — | — |
| home-start/050% | 15.5 | Straight | — | 26.55082 | — | — |
| home-start/075% | 23.25 | Straight | — | 26.84913 | — | — |
| corner-1/0.000 | 31 | TurnEntry | 0 | 27.14416 | 28.12339 | 0 |
| home-start/100% | 31 | Straight | — | 27.14416 | — | — |
| corner-1/0.125 | 44.60703 | TurnEntry | 0.125 | 26.85161 | 26.85161 | 0 |
| corner-1/0.250 | 58.21405 | TurnEntry | 0.25 | 25.51658 | 25.51658 | 0 |
| corner-1/0.375 | 71.82107 | TurnMiddle | 0.375 | 24.10807 | 24.09366 | 0 |
| corner-1/0.500 | 85.42809 | TurnMiddle | 0.5 | 22.61286 | 22.59749 | 0 |
| corner-1/0.625 | 99.03512 | TurnMiddle | 0.625 | 22.68159 | 22.68159 | 0.316406 |
| corner-1/0.750 | 112.6421 | TurnExit | 0.75 | 23.0981 | 23.08509 | 0.84375 |
| corner-1/0.875 | 126.2492 | TurnExit | 0.875 | 23.72793 | 23.71612 | 1 |
| back-straight/000% | 139.8562 | Straight | — | 24.32148 | — | — |
| corner-1/1.000 | 139.8562 | TurnExit | 1 | 24.32148 | 24.31075 | 1 |
| back-straight/025% | 155.3562 | Straight | — | 25.03224 | — | — |
| back-straight/050% | 170.8562 | Straight | — | 25.72337 | — | — |
| back-straight/075% | 186.3562 | Straight | — | 26.39641 | — | — |
| back-straight/100% | 201.8562 | Straight | — | 27.05271 | — | — |
| corner-2/0.000 | 201.8562 | TurnEntry | 0 | 27.05271 | 28.06386 | 0 |
| corner-2/0.125 | 215.4632 | TurnEntry | 0.125 | 26.79322 | 26.79322 | 0 |
| corner-2/0.250 | 229.0702 | TurnEntry | 0.25 | 25.4593 | 25.4593 | 0 |
| corner-2/0.375 | 242.6772 | TurnMiddle | 0.375 | 24.05185 | 24.0374 | 0 |
| corner-2/0.500 | 256.2843 | TurnMiddle | 0.5 | 22.55761 | 22.54221 | 0 |
| corner-2/0.625 | 269.8913 | TurnMiddle | 0.625 | 22.62663 | 22.62663 | 0.316406 |
| corner-2/0.750 | 283.4983 | TurnExit | 0.75 | 23.04469 | 23.03166 | 0.84375 |
| corner-2/0.875 | 297.1053 | TurnExit | 0.875 | 23.67678 | 23.66495 | 1 |
| corner-2/1.000 | 310.7124 | TurnExit | 1 | 24.27237 | 24.26163 | 1 |
| home-finish/000% | 310.7124 | Straight | — | 24.27237 | — | — |
| home-finish/025% | 318.4624 | Straight | — | 24.66125 | — | — |
| home-finish/050% | 326.2124 | Straight | — | 25.04408 | — | — |
| home-finish/075% | 333.9624 | Straight | — | 25.42116 | — | — |
| home-finish/100% | 341.7124 | Straight | — | 25.79272 | — | — |

## K. Vmax location

Across 65 controlled matched-venue rider observations (balanced, Speed, SlideControl and fixed-line sweeps), 100% peak on Straight and 0% in a Corner.
Corner Vmax progress distribution: no Corner maxima observed. This is reported, not forced to match the public telemetry context.
| Sweep | Value/line | Vmax P50 km/h | Average P50 m/s | Flying P50 s | HeatTime P50 s | Vmax location(s) |
| --- | --- | --- | --- | --- | --- | --- |
| Speed | 000 | 89.535079 | 22.657976 | 15.252453 | 62.333658 | Straight |
| Speed | 025 | 94.493806 | 23.548599 | 14.625793 | 59.976164 | Straight |
| Speed | 050 | 98.999028 | 24.358277 | 14.089063 | 57.982407 | Straight |
| Speed | 075 | 103.253693 | 25.099565 | 13.620422 | 56.269794 | Straight |
| Speed | 100 | 107.005404 | 25.784625 | 13.202477 | 54.774771 | Straight |
| SlideControl | 000 | 97.642687 | 23.746345 | 14.497486 | 59.477005 | Straight |
| SlideControl | 025 | 98.460513 | 24.069327 | 14.282846 | 58.67873 | Straight |
| SlideControl | 050 | 98.999028 | 24.358277 | 14.089063 | 57.982407 | Straight |
| SlideControl | 075 | 99.627989 | 24.627254 | 13.911438 | 57.348959 | Straight |
| SlideControl | 100 | 100.250907 | 24.881658 | 13.746703 | 56.762506 | Straight |
| Lateral | 0 | 95.321489 | 22.924622 | 13.548689 | 55.622078 | Straight |
| Lateral | 1 | 97.882807 | 23.931437 | 13.891222 | 57.115234 | Straight |
| Lateral | 2 | 100.11711 | 24.832481 | 14.249331 | 58.736942 | Straight |
| Lateral | 3 | 102.443012 | 25.610899 | 14.61462 | 60.533543 | Straight |
| Lateral | 4 | 104.599477 | 26.324972 | 14.989109 | 62.37624 | Straight |

## L. Diagnosis

| Diagnostic | Observation | Classification |
| --- | --- | --- |
| Vmax | Synthetic P50 98.999028 km/h vs real P10/P50/P90 108.6 / 113.7 / 116.9 | general/profile peak deficit remains |
| AverageSpeed | Synthetic P50 24.358277 m/s vs real P10/P50/P90 21.472577 / 22.384854 / 23.357308 | residual remains; attribution depends on lateral path and telemetry distance convention |
| FlyingLapMedian | Synthetic P50 14.089063 s vs real P10/P50/P90 14.56 / 14.87 / 15.15 | timing/profile mismatch remains |
| TotalDistance | Fixed-line distances are compared separately with real P10/P50/P90 1335.6 / 1377 / 1418 m; real P50 interpolates near line 1.11065 | line-distance plausibility unresolved without real lateral-path occupancy |
| Straight↔apex amplitude | Low Vmax, faster-than-P10 flying laps and Straight-only maxima persist across 16.2–18.0 m symmetric-width sensitivity | diagnostic hypothesis; real corner minima are unavailable |
| L1 | Sensitivity table changes start preparation while published start offset remains unknown | StartLineSensitiveContext; unsupported as a primary target |
| Venue effects | Banking and second-corner asymmetry are absent | known missing venue physics / geometry representation |
Residuals are not automatically assigned to physics. AverageSpeed equals TotalDistance / HeatTime, so its residual cannot be attributed solely to longitudinal physics before real lateral-path occupancy and telemetry distance convention are resolved. Rider-population mapping, symmetric geometry and missing banking remain competing explanations.

## M. Frozen physics

| Boundary | Frozen #38 value |
| --- | --- |
| ADVANCED settled/apex reference | 19 m/s |
| Legacy/reference compatibility | 16 m/s |
| Apex / full-drive progress | 0.50 / 5/6 |
| Correction / quiet | 2.0→3.2 m/s² / 1.015 |
| Brake / RunWide / retention | 1.06→1.14 / 1.18→1.34 / 0.35→0.65 |
| Straight / corner full-drive | 1.60→3.20 / 1.20→2.80 m/s² |
| Force fade / reference | 0.035 / 0.010; 16 m/s |
| Mass / resistance | 142 kg; 40 + 0.20v² N |
| Gearing / surface drive | 1.10→0.90; 0.75 + 0.25×EffectiveGrip |
| Integration | 1 m + exact remainder |
| Reaction / launch | 0.28→0.20 s / 9→11 m/s² |
| Incidents, RNG, lateral, contact, surface, setup and skills | unchanged |
NO PHYSICS CONSTANTS CHANGED. #39 measures the frozen #38 production path and does not tune it.

## N. Known limitations

This is a matched-venue reference-radius approximation, not a digital twin. The public 31 m radius measurement convention has not been explicitly verified against `InnerRadiusMeters`. Current bend widths are 17.0/16.2 m, but production `TrackGeometry` requires one turn width; 16.6 m is a transparent arithmetic-mean proxy. It preserves aggregate full-lap lateral contribution for a common normalized line, not local corner radii, safe speeds or asymmetry. The start-line split is provisional, and banking is absent. Surface material is provenance context; the synthetic heat uses the established baseline dry surface state rather than a granite-specific physics law. Active lateral motion still lacks diagonal/spiral path-length integration. Real telemetry supplies no lateral occupancy, corner minimum or per-point throttle trace. The historical 56.500 s record remains HistoricalContext and is not a target.

## Next-model decision evidence

| Question | Evidence |
| --- | --- |
| 1. Did real geometry alone materially raise Vmax? | P50 changed 93.680022 → 98.999028 km/h (+5.319007 km/h). |
| 2. Did AverageSpeed move in the useful direction? | P50 changed 22.218422 → 24.358277 m/s; the absolute gap to real P50 increased. |
| 3. Are flying laps fast, slow or within the envelope? | Synthetic flying laps are faster than the real P10 envelope. |
| 4. Is Straight↔Apex amplitude too small? | Likely as a width-stable diagnostic hypothesis: low Vmax, fast flying laps and Straight-only maxima persist from 16.2 to 18.0 m. Venue telemetry has no apex-speed series to prove causation. |
| 5. Does Vmax remain exclusively on Straight? | Yes for every controlled matched-venue observation. |
| 6. Are L1 conclusions stable to the start split? | P50 range 15.616614–16.229115 s (span 0.612501 s); treat L1 as StartLineSensitiveContext. |
| 7. Is line distance plausible against real TotalDistance? | Unresolved without real lateral-path occupancy. Fixed line(s) inside real P10–P90: 1; real P50 interpolates near line 1.11065. |
| 8. One subsystem for the next experiment | Next diagnostic experiment: longitudinal drive-availability / throttle-profile shape across Straight → corner entry. Width sensitivity supports testing this subsystem, not a conclusion that its physics is proven wrong; do not change it in #39. |
