# Dynamic Line Choice & Track Evolution Gameplay Baseline

Base main: `e6d519a1d00d82dc75570db8b596304d727b923a`. Motoarena 2026 uses the matched 16.6 m symmetric-turn approximation and 31/31 m start split. Every physical benchmark is a production `CalibrationRunner -> HeatSimulator` one-rider four-lap heat with neutral setup, all skills 50, zero incidents, a fixed lane, and every calibration-only physics adjustment null.

## A. Game design objective

This experiment asks whether the current manager-game architecture already creates state-dependent line choice, imperfect information, skill-dependent execution, persistent wear, and meaningful manager intervention. Realism is a guardrail; gameplay is the objective. No inner penalty, outer bonus, cushion multiplier, preferred-line speed multiplier, or final-turn bonus is introduced.

## B. Existing production mechanisms

The experiment uses the existing five-band `TrackState`, `TrackStateSnapshot.SampleSurface`, continuous surface interpolation and wear, `AdaptiveDecisionModel`, seeded TrackReading noise, `LateralMovementModel`, `TrackEvolution.ApplyWeather`, `TrackEvolution.ApplyTrackWork`, geometry, setup, contact, and `HeatSimulator`. Lane 0..4 remains a tactical grid; physical `LateralPosition` remains continuous 0..4.

## C. Why uniform-surface L0 is not sufficient evidence

Uniform ranking is `L0 -> L1 -> L2 -> L3 -> L4`. Equal surface naturally favors the shorter inner route, so this alone neither proves line blindness nor justifies a physics change. Controlled unequal surfaces are required before diagnosis.

## D. Fixed-line methodology

`PHYSICAL BEST LINE` is measured separately from `AI CHOSEN LINE`. Each static state starts from a fresh surface, runs HoldLane L0-L4 through the production heat, and ranks the flying-lap median. Surface values are actual integer-position samples from `SampleSurface`; benchmarks of evolved states run on copies and cannot wear the persistent meeting state.

## E. Uniform benchmark

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Uniform | 0.000000 | L0 | 1.000000 | 1275.115000 | 13.397707 | 55.186684 | 26.515163 | 26.472385 | 21.525948 | 23.344968 | 1 | 0.000000 |
| Uniform | 0.000000 | L1 | 1.000000 | 1366.850000 | 13.737913 | 56.673038 | 27.227627 | 27.182254 | 22.757946 | 24.465595 | 2 | 0.340206 |
| Uniform | 0.000000 | L2 | 1.000000 | 1458.584000 | 14.092064 | 58.289753 | 27.901592 | 27.839408 | 23.901510 | 25.480280 | 3 | 0.694357 |
| Uniform | 0.000000 | L3 | 1.000000 | 1550.318000 | 14.456406 | 60.083672 | 28.542591 | 28.477345 | 25.014505 | 26.443600 | 4 | 1.058699 |
| Uniform | 0.000000 | L4 | 1.000000 | 1642.053000 | 14.828415 | 61.915035 | 29.151857 | 29.083755 | 26.080048 | 27.343258 | 5 | 1.430708 |

## F. Outside-cushion severity sweep

`OutsideCushionStrong` is the severity 1 endpoint. All turn segments receive the specified lane deltas; straights remain baseline and moisture is unchanged.

### severity 0.000000 — best L0, second L1, gap 0.340206 s

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| OutsideCushion | 0.000000 | L0 | 1.000000 | 1275.115000 | 13.397707 | 55.186684 | 26.515163 | 26.472385 | 21.525948 | 23.344968 | 1 | 0.000000 |
| OutsideCushion | 0.000000 | L1 | 1.000000 | 1366.850000 | 13.737913 | 56.673038 | 27.227627 | 27.182254 | 22.757946 | 24.465595 | 2 | 0.340206 |
| OutsideCushion | 0.000000 | L2 | 1.000000 | 1458.584000 | 14.092064 | 58.289753 | 27.901592 | 27.839408 | 23.901510 | 25.480280 | 3 | 0.694357 |
| OutsideCushion | 0.000000 | L3 | 1.000000 | 1550.318000 | 14.456406 | 60.083672 | 28.542591 | 28.477345 | 25.014505 | 26.443600 | 4 | 1.058699 |
| OutsideCushion | 0.000000 | L4 | 1.000000 | 1642.053000 | 14.828415 | 61.915035 | 29.151857 | 29.083755 | 26.080048 | 27.343258 | 5 | 1.430708 |

### severity 0.250000 — best L0, second L1, gap 0.252367 s

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| OutsideCushion | 0.250000 | L0 | 0.878172 | 1275.115000 | 13.669724 | 56.234695 | 26.218582 | 26.158861 | 20.998125 | 22.871270 | 1 | 0.000000 |
| OutsideCushion | 0.250000 | L1 | 0.917938 | 1366.850000 | 13.922091 | 57.375187 | 27.013205 | 26.969426 | 22.383172 | 24.132688 | 2 | 0.252367 |
| OutsideCushion | 0.250000 | L2 | 0.958547 | 1458.584000 | 14.185659 | 58.611622 | 27.786459 | 27.725399 | 23.703485 | 25.306143 | 3 | 0.515936 |
| OutsideCushion | 0.250000 | L3 | 0.986917 | 1550.318000 | 14.486258 | 60.185509 | 28.504251 | 28.439378 | 24.949223 | 26.386763 | 4 | 0.816534 |
| OutsideCushion | 0.250000 | L4 | 1.000000 | 1642.053000 | 14.828415 | 61.915035 | 29.151857 | 29.083755 | 26.080048 | 27.343258 | 5 | 1.158691 |

### severity 0.500000 — best L0, second L1, gap 0.165192 s

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| OutsideCushion | 0.500000 | L0 | 0.763938 | 1275.115000 | 13.941252 | 57.285580 | 25.851252 | 25.618723 | 20.494559 | 22.420049 | 1 | 0.000000 |
| OutsideCushion | 0.500000 | L1 | 0.839250 | 1366.850000 | 14.106443 | 58.080246 | 26.806820 | 26.764674 | 22.019511 | 23.809969 | 2 | 0.165192 |
| OutsideCushion | 0.500000 | L2 | 0.917938 | 1458.584000 | 14.279352 | 58.940948 | 27.674702 | 27.628603 | 23.532568 | 25.155910 | 3 | 0.338100 |
| OutsideCushion | 0.500000 | L3 | 0.973919 | 1550.318000 | 14.516098 | 60.287434 | 28.466139 | 28.401619 | 24.884233 | 26.330187 | 4 | 0.574846 |
| OutsideCushion | 0.500000 | L4 | 1.000000 | 1642.053000 | 14.828415 | 61.915035 | 29.151857 | 29.083755 | 26.080048 | 27.343258 | 5 | 0.887163 |

### severity 0.750000 — best L0, second L1, gap 0.080429 s

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| OutsideCushion | 0.750000 | L0 | 0.657297 | 1275.115000 | 14.210005 | 58.330299 | 25.496874 | 25.107361 | 20.017003 | 21.992776 | 1 | 0.000000 |
| OutsideCushion | 0.750000 | L1 | 0.763938 | 1366.850000 | 14.290434 | 58.786179 | 26.608650 | 26.568165 | 21.667529 | 23.497921 | 2 | 0.080429 |
| OutsideCushion | 0.750000 | L2 | 0.878172 | 1458.584000 | 14.373085 | 59.295746 | 27.563795 | 27.518538 | 23.339901 | 24.986642 | 3 | 0.163080 |
| OutsideCushion | 0.750000 | L3 | 0.961005 | 1550.318000 | 14.545952 | 60.389416 | 28.428230 | 28.364078 | 24.819546 | 26.273888 | 4 | 0.335947 |
| OutsideCushion | 0.750000 | L4 | 1.000000 | 1642.053000 | 14.828415 | 61.915035 | 29.151857 | 29.083755 | 26.080048 | 27.343258 | 5 | 0.618410 |

### severity 1.000000 — best L2, second L1, gap 0.006718 s

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| OutsideCushion | 1.000000 | L0 | 0.558250 | 1275.115000 | 14.473646 | 59.359760 | 25.192894 | 24.626337 | 19.567061 | 21.590816 | 3 | 0.006887 |
| OutsideCushion | 1.000000 | L1 | 0.692000 | 1366.850000 | 14.473476 | 59.490623 | 26.418863 | 26.380075 | 21.327768 | 23.197002 | 2 | 0.006718 |
| OutsideCushion | 1.000000 | L2 | 0.839250 | 1458.584000 | 14.466759 | 59.651031 | 27.455004 | 27.410620 | 23.150233 | 24.820085 | 1 | 0.000000 |
| OutsideCushion | 1.000000 | L3 | 0.948175 | 1550.318000 | 14.575823 | 60.491474 | 28.390551 | 28.326766 | 24.755159 | 26.217852 | 4 | 0.109064 |
| OutsideCushion | 1.000000 | L4 | 1.000000 | 1642.053000 | 14.828415 | 61.915035 | 29.151857 | 29.083755 | 26.080048 | 27.343258 | 5 | 0.361656 |

## G. Middle-cushion severity sweep

### severity 0.000000 — best L0, second L1, gap 0.340206 s

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| MiddleCushion | 0.000000 | L0 | 1.000000 | 1275.115000 | 13.397707 | 55.186684 | 26.515163 | 26.472385 | 21.525948 | 23.344968 | 1 | 0.000000 |
| MiddleCushion | 0.000000 | L1 | 1.000000 | 1366.850000 | 13.737913 | 56.673038 | 27.227627 | 27.182254 | 22.757946 | 24.465595 | 2 | 0.340206 |
| MiddleCushion | 0.000000 | L2 | 1.000000 | 1458.584000 | 14.092064 | 58.289753 | 27.901592 | 27.839408 | 23.901510 | 25.480280 | 3 | 0.694357 |
| MiddleCushion | 0.000000 | L3 | 1.000000 | 1550.318000 | 14.456406 | 60.083672 | 28.542591 | 28.477345 | 25.014505 | 26.443600 | 4 | 1.058699 |
| MiddleCushion | 0.000000 | L4 | 1.000000 | 1642.053000 | 14.828415 | 61.915035 | 29.151857 | 29.083755 | 26.080048 | 27.343258 | 5 | 1.430708 |

### severity 0.250000 — best L0, second L1, gap 0.230030 s

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| MiddleCushion | 0.250000 | L0 | 0.900586 | 1275.115000 | 13.618336 | 56.036320 | 26.273272 | 26.232328 | 21.095984 | 22.959038 | 1 | 0.000000 |
| MiddleCushion | 0.250000 | L1 | 0.950358 | 1366.850000 | 13.848366 | 57.093845 | 27.098026 | 27.053596 | 22.531777 | 24.264650 | 2 | 0.230030 |
| MiddleCushion | 0.250000 | L2 | 1.000000 | 1458.584000 | 14.092064 | 58.289753 | 27.901592 | 27.839408 | 23.901510 | 25.480280 | 3 | 0.473728 |
| MiddleCushion | 0.250000 | L3 | 0.966770 | 1550.318000 | 14.532587 | 60.343754 | 28.445156 | 28.380856 | 24.848469 | 26.299059 | 4 | 0.914251 |
| MiddleCushion | 0.250000 | L4 | 0.928141 | 1642.053000 | 14.997787 | 62.489799 | 28.930487 | 28.864566 | 25.704668 | 27.019777 | 5 | 1.379452 |

### severity 0.500000 — best L0, second L1, gap 0.119806 s

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| MiddleCushion | 0.500000 | L0 | 0.806094 | 1275.115000 | 13.839128 | 56.889751 | 25.999353 | 25.819065 | 20.681426 | 22.587411 | 1 | 0.000000 |
| MiddleCushion | 0.500000 | L1 | 0.901930 | 1366.850000 | 13.958935 | 57.516014 | 26.971273 | 26.927820 | 22.309526 | 24.067307 | 2 | 0.119806 |
| MiddleCushion | 0.500000 | L2 | 1.000000 | 1458.584000 | 14.092064 | 58.289753 | 27.901592 | 27.839408 | 23.901510 | 25.480280 | 3 | 0.252935 |
| MiddleCushion | 0.500000 | L3 | 0.934080 | 1550.318000 | 14.608829 | 60.604347 | 28.349119 | 28.285768 | 24.684330 | 26.156219 | 4 | 0.769701 |
| MiddleCushion | 0.500000 | L4 | 0.858813 | 1642.053000 | 15.167297 | 63.066521 | 28.715992 | 28.652299 | 25.338751 | 26.704691 | 5 | 1.328169 |

### severity 0.750000 — best L0, second L1, gap 0.010752 s

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| MiddleCushion | 0.750000 | L0 | 0.716524 | 1275.115000 | 14.058813 | 57.742058 | 25.688004 | 25.392336 | 20.283206 | 22.230871 | 1 | 0.000000 |
| MiddleCushion | 0.750000 | L1 | 0.854718 | 1366.850000 | 14.069565 | 57.939095 | 26.847441 | 26.804962 | 22.091324 | 23.873672 | 2 | 0.010752 |
| MiddleCushion | 0.750000 | L2 | 1.000000 | 1458.584000 | 14.092064 | 58.289753 | 27.901592 | 27.839408 | 23.901510 | 25.480280 | 3 | 0.033251 |
| MiddleCushion | 0.750000 | L3 | 0.901930 | 1550.318000 | 14.685120 | 60.865364 | 28.254486 | 28.192096 | 24.522135 | 26.015120 | 4 | 0.626307 |
| MiddleCushion | 0.750000 | L4 | 0.792016 | 1642.053000 | 15.336624 | 63.644070 | 28.508526 | 28.447117 | 24.982735 | 26.398367 | 5 | 1.277811 |

### severity 1.000000 — best L2, second L1, gap 0.088022 s

| Profile | Severity | Lane | EffectiveGrip | 4-lap distance m | Flying s | HeatTime s | Vmax m/s | Entry m/s | True apex m/s | Exit m/s | Rank | Delta to best s |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| MiddleCushion | 1.000000 | L0 | 0.631875 | 1275.115000 | 14.276209 | 58.588585 | 25.428339 | 24.984612 | 19.902214 | 21.890171 | 3 | 0.184145 |
| MiddleCushion | 1.000000 | L1 | 0.808720 | 1366.850000 | 14.180086 | 58.362637 | 26.726553 | 26.685066 | 21.877283 | 23.683843 | 2 | 0.088022 |
| MiddleCushion | 1.000000 | L2 | 1.000000 | 1458.584000 | 14.092064 | 58.289753 | 27.901592 | 27.839408 | 23.901510 | 25.480280 | 1 | 0.000000 |
| MiddleCushion | 1.000000 | L3 | 0.870320 | 1550.318000 | 14.761375 | 61.126606 | 28.161283 | 28.099865 | 24.361927 | 25.875809 | 4 | 0.669312 |
| MiddleCushion | 1.000000 | L4 | 0.727750 | 1642.053000 | 15.505375 | 64.221039 | 28.308239 | 28.249154 | 24.637016 | 26.101118 | 5 | 1.413311 |

## H. Line-switch thresholds

| Profile | FirstNonInnerBestSeverity | FirstOuterHalfBestSeverity | Best-line sequence severity 0/.25/.50/.75/1 |
|---|---:|---:|---|
| OutsideCushion | 1.000000 | 1.000000 | L0 -> L0 -> L0 -> L0 -> L2 |
| MiddleCushion | 1.000000 | 1.000000 | L0 -> L0 -> L0 -> L0 -> L2 |

The threshold is an observed diagnostic, not a required winner.

### Physical line sensitivity

LineSwitchRequiresStrongSurfaceContrast: **YES**. A switch only at severity 1 is not an automatic failure; it shows that the current constant-reference physical economy strongly rewards shorter lines.

## I. Effective-grip matrix

| Profile | Severity | Lane | Grip | Ruts | Moisture | EffectiveGrip |
|---|---:|---:|---:|---:|---:|---:|
| MiddleCushion | 0.000000 | L0 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| MiddleCushion | 0.000000 | L1 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| MiddleCushion | 0.000000 | L2 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| MiddleCushion | 0.000000 | L3 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| MiddleCushion | 0.000000 | L4 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| MiddleCushion | 0.250000 | L0 | 0.937500 | 0.087500 | 0.350000 | 0.900586 |
| MiddleCushion | 0.250000 | L1 | 0.970000 | 0.045000 | 0.350000 | 0.950358 |
| MiddleCushion | 0.250000 | L2 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| MiddleCushion | 0.250000 | L3 | 0.980000 | 0.030000 | 0.350000 | 0.966770 |
| MiddleCushion | 0.250000 | L4 | 0.955000 | 0.062500 | 0.350000 | 0.928141 |
| MiddleCushion | 0.500000 | L0 | 0.875000 | 0.175000 | 0.350000 | 0.806094 |
| MiddleCushion | 0.500000 | L1 | 0.940000 | 0.090000 | 0.350000 | 0.901930 |
| MiddleCushion | 0.500000 | L2 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| MiddleCushion | 0.500000 | L3 | 0.960000 | 0.060000 | 0.350000 | 0.934080 |
| MiddleCushion | 0.500000 | L4 | 0.910000 | 0.125000 | 0.350000 | 0.858813 |
| MiddleCushion | 0.750000 | L0 | 0.812500 | 0.262500 | 0.350000 | 0.716524 |
| MiddleCushion | 0.750000 | L1 | 0.910000 | 0.135000 | 0.350000 | 0.854718 |
| MiddleCushion | 0.750000 | L2 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| MiddleCushion | 0.750000 | L3 | 0.940000 | 0.090000 | 0.350000 | 0.901930 |
| MiddleCushion | 0.750000 | L4 | 0.865000 | 0.187500 | 0.350000 | 0.792016 |
| MiddleCushion | 1.000000 | L0 | 0.750000 | 0.350000 | 0.350000 | 0.631875 |
| MiddleCushion | 1.000000 | L1 | 0.880000 | 0.180000 | 0.350000 | 0.808720 |
| MiddleCushion | 1.000000 | L2 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| MiddleCushion | 1.000000 | L3 | 0.920000 | 0.120000 | 0.350000 | 0.870320 |
| MiddleCushion | 1.000000 | L4 | 0.820000 | 0.250000 | 0.350000 | 0.727750 |
| OutsideCushion | 0.000000 | L0 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| OutsideCushion | 0.000000 | L1 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| OutsideCushion | 0.000000 | L2 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| OutsideCushion | 0.000000 | L3 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| OutsideCushion | 0.000000 | L4 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| OutsideCushion | 0.250000 | L0 | 0.925000 | 0.112500 | 0.350000 | 0.878172 |
| OutsideCushion | 0.250000 | L1 | 0.950000 | 0.075000 | 0.350000 | 0.917938 |
| OutsideCushion | 0.250000 | L2 | 0.975000 | 0.037500 | 0.350000 | 0.958547 |
| OutsideCushion | 0.250000 | L3 | 0.992500 | 0.012500 | 0.350000 | 0.986917 |
| OutsideCushion | 0.250000 | L4 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| OutsideCushion | 0.500000 | L0 | 0.850000 | 0.225000 | 0.350000 | 0.763938 |
| OutsideCushion | 0.500000 | L1 | 0.900000 | 0.150000 | 0.350000 | 0.839250 |
| OutsideCushion | 0.500000 | L2 | 0.950000 | 0.075000 | 0.350000 | 0.917938 |
| OutsideCushion | 0.500000 | L3 | 0.985000 | 0.025000 | 0.350000 | 0.973919 |
| OutsideCushion | 0.500000 | L4 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| OutsideCushion | 0.750000 | L0 | 0.775000 | 0.337500 | 0.350000 | 0.657297 |
| OutsideCushion | 0.750000 | L1 | 0.850000 | 0.225000 | 0.350000 | 0.763938 |
| OutsideCushion | 0.750000 | L2 | 0.925000 | 0.112500 | 0.350000 | 0.878172 |
| OutsideCushion | 0.750000 | L3 | 0.977500 | 0.037500 | 0.350000 | 0.961005 |
| OutsideCushion | 0.750000 | L4 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| OutsideCushion | 1.000000 | L0 | 0.700000 | 0.450000 | 0.350000 | 0.558250 |
| OutsideCushion | 1.000000 | L1 | 0.800000 | 0.300000 | 0.350000 | 0.692000 |
| OutsideCushion | 1.000000 | L2 | 0.900000 | 0.150000 | 0.350000 | 0.839250 |
| OutsideCushion | 1.000000 | L3 | 0.970000 | 0.050000 | 0.350000 | 0.948175 |
| OutsideCushion | 1.000000 | L4 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| Uniform | 0.000000 | L0 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| Uniform | 0.000000 | L1 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| Uniform | 0.000000 | L2 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| Uniform | 0.000000 | L3 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| Uniform | 0.000000 | L4 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |

## J. AdaptiveDecisionModel static choices

The probe is captured on the home-straight segment immediately before the next logical corner.

| Profile | Severity | Actual best | AI target | Regret s |
|---|---:|---:|---:|---:|
| Uniform | 0.000000 | L0 | L0 | 0.000000 |
| OutsideCushion | 0.500000 | L0 | L2 | 0.338100 |
| OutsideCushion | 1.000000 | L2 | L3 | 0.109064 |
| MiddleCushion | 0.500000 | L0 | L2 | 0.252935 |
| MiddleCushion | 1.000000 | L2 | L2 | 0.000000 |

### Decision oracle at TrackReading 100

TrackReading 100 makes the existing observation-noise multiplier zero. The oracle is therefore the current production decision objective under exact surface perception, not an oracle for the physically fastest fixed line.

`RiderStyle.Balanced` has `OutsidePreference = 0.5`, which production maps to a preferred lane near L2. Physical regret measures distance to the fastest fixed line, while the production decision objective also contains rider-style preference, movement cost, occupancy, surface risk, and projected route time.

| Profile | Severity | Physical best | TR100 oracle | Seed invariant | Oracle physical regret | TR100 L0/L1/L2/L3/L4 |
|---|---:|---:|---:|---|---:|---|
| Uniform | 0.000000 | L0 | L0 | YES | 0.000000 | 64/0/0/0/0 |
| OutsideCushion | 0.500000 | L0 | L2 | YES | 0.338100 | 0/0/64/0/0 |
| OutsideCushion | 1.000000 | L2 | L3 | YES | 0.109064 | 0/0/0/64/0 |
| MiddleCushion | 0.500000 | L0 | L2 | YES | 0.252935 | 0/0/64/0/0 |
| MiddleCushion | 1.000000 | L2 | L2 | YES | 0.000000 | 0/0/64/0/0 |

### Perception agreement vs physical regret

| Profile | Severity | Physical best | TR100 oracle | Oracle regret | TR20 agreement | TR50 agreement | TR80 agreement | TR20 physical regret | TR50 physical regret | TR80 physical regret |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Uniform | 0.000000 | L0 | L0 | 0.000000 | 90.625000% | 100.000000% | 100.000000% | 0.031894 | 0.000000 | 0.000000 |
| OutsideCushion | 0.500000 | L0 | L2 | 0.338100 | 42.187500% | 56.250000% | 90.625000% | 0.230186 | 0.252024 | 0.321890 |
| OutsideCushion | 1.000000 | L2 | L3 | 0.109064 | 56.250000% | 60.937500% | 78.125000% | 0.066999 | 0.066461 | 0.085206 |
| MiddleCushion | 0.500000 | L0 | L2 | 0.252935 | 53.125000% | 73.437500% | 100.000000% | 0.177427 | 0.217573 | 0.252935 |
| MiddleCushion | 1.000000 | L2 | L2 | 0.000000 | 100.000000% | 100.000000% | 100.000000% | 0.000000 | 0.000000 | 0.000000 |

### Decision-objective mismatch

DecisionObjectiveVsFastestLineMismatch: **YES**.

## K. TrackReading seed sweep

Each row uses the stable 64-seed set `45101..45164` and only the existing deterministic RNG.

| Profile | Severity | TR | Seeds | Physical-perfect % | Within .05s % | Mean physical regret | Median | P90 | Oracle | Oracle agreement % | L0/L1/L2/L3/L4 choices | Oracle delta -4/-3/-2/-1/0/+1/+2/+3/+4 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| Uniform | 0.000000 | 20 | 64 | 90.625000 | 90.625000 | 0.031894 | 0.000000 | 0.000000 | L0 | 90.625000 | 58/6/0/0/0 | 0/0/0/0/58/6/0/0/0 |
| Uniform | 0.000000 | 50 | 64 | 100.000000 | 100.000000 | 0.000000 | 0.000000 | 0.000000 | L0 | 100.000000 | 64/0/0/0/0 | 0/0/0/0/64/0/0/0/0 |
| Uniform | 0.000000 | 80 | 64 | 100.000000 | 100.000000 | 0.000000 | 0.000000 | 0.000000 | L0 | 100.000000 | 64/0/0/0/0 | 0/0/0/0/64/0/0/0/0 |
| OutsideCushion | 0.500000 | 20 | 64 | 20.312500 | 20.312500 | 0.230186 | 0.165192 | 0.338100 | L2 | 42.187500 | 13/20/27/4/0 | 0/0/13/20/27/4/0/0/0 |
| OutsideCushion | 0.500000 | 50 | 64 | 14.062500 | 14.062500 | 0.252024 | 0.338100 | 0.338100 | L2 | 56.250000 | 9/17/36/2/0 | 0/0/9/17/36/2/0/0/0 |
| OutsideCushion | 0.500000 | 80 | 64 | 0.000000 | 0.000000 | 0.321890 | 0.338100 | 0.338100 | L2 | 90.625000 | 0/6/58/0/0 | 0/0/0/6/58/0/0/0/0 |
| OutsideCushion | 1.000000 | 20 | 64 | 42.187500 | 42.187500 | 0.066999 | 0.109064 | 0.109064 | L3 | 56.250000 | 0/0/27/36/1 | 0/0/0/27/36/1/0/0/0 |
| OutsideCushion | 1.000000 | 50 | 64 | 39.062500 | 39.062500 | 0.066461 | 0.109064 | 0.109064 | L3 | 60.937500 | 0/0/25/39/0 | 0/0/0/25/39/0/0/0/0 |
| OutsideCushion | 1.000000 | 80 | 64 | 21.875000 | 21.875000 | 0.085206 | 0.109064 | 0.109064 | L3 | 78.125000 | 0/0/14/50/0 | 0/0/0/14/50/0/0/0/0 |
| MiddleCushion | 0.500000 | 20 | 64 | 10.937500 | 10.937500 | 0.177427 | 0.252935 | 0.252935 | L2 | 53.125000 | 7/23/34/0/0 | 0/0/7/23/34/0/0/0/0 |
| MiddleCushion | 0.500000 | 50 | 64 | 0.000000 | 0.000000 | 0.217573 | 0.252935 | 0.252935 | L2 | 73.437500 | 0/17/47/0/0 | 0/0/0/17/47/0/0/0/0 |
| MiddleCushion | 0.500000 | 80 | 64 | 0.000000 | 0.000000 | 0.252935 | 0.252935 | 0.252935 | L2 | 100.000000 | 0/0/64/0/0 | 0/0/0/0/64/0/0/0/0 |
| MiddleCushion | 1.000000 | 20 | 64 | 100.000000 | 100.000000 | 0.000000 | 0.000000 | 0.000000 | L2 | 100.000000 | 0/0/64/0/0 | 0/0/0/0/64/0/0/0/0 |
| MiddleCushion | 1.000000 | 50 | 64 | 100.000000 | 100.000000 | 0.000000 | 0.000000 | 0.000000 | L2 | 100.000000 | 0/0/64/0/0 | 0/0/0/0/64/0/0/0/0 |
| MiddleCushion | 1.000000 | 80 | 64 | 100.000000 | 100.000000 | 0.000000 | 0.000000 | 0.000000 | L2 | 100.000000 | 0/0/64/0/0 | 0/0/0/0/64/0/0/0/0 |

## L. Decision regret

`PhysicalRegret = FixedLineFlying(ChosenLane) - FixedLineFlying(PhysicalBestLane)`, clamped only for sub-tolerance floating error. `OracleAgreement` instead measures convergence to the seed-invariant TR100 production decision. These answer different questions; TrackReading remains perception/decision-only.

- TR20 aggregate mean/median/P90 regret: `0.101301` / `0.105438` / `0.140020` s; perfect `52.812500%`; within 0.05 s `52.812500%`.
- TR50 aggregate mean/median/P90 regret: `0.107212` / `0.140020` / `0.140020` s; perfect `50.625000%`; within 0.05 s `50.625000%`.
- TR80 aggregate mean/median/P90 regret: `0.132006` / `0.140020` / `0.140020` s; perfect `44.375000%`; within 0.05 s `44.375000%`.
- TrackReadingPerceptionSignalHealthy: **YES**.
- TrackReadingPerceptionSignalWeak: **NO**.
- TrackReadingPerceptionReversal: **NO**.
- DecisionObjectiveVsFastestLineMismatch: **YES**.

## M. Lateral execution matrix

### Planner waypoint binding

| SC | Adaptability | Profile/severity | Target | Planned | Start | Entry | To planned m | To target m | Max move m | Actual move m | Cap binding | Arrival error m | Arrived | Flying s |
|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|---|---:|
| 20 | 20 | MiddleCushion/1.000000 | L2 | L1 | 0.000000 | 1.000000 | 2.500000 | 5.000000 | 2.581297 | 2.500000 | YES | 3.650000 | NO | 14.313210 |
| 20 | 80 | MiddleCushion/1.000000 | L2 | L1 | 0.000000 | 1.000000 | 2.500000 | 5.000000 | 3.147924 | 2.500000 | YES | 3.650000 | NO | 14.313684 |
| 50 | 50 | MiddleCushion/1.000000 | L2 | L1 | 0.000000 | 1.000000 | 2.500000 | 5.000000 | 3.147924 | 2.500000 | YES | 3.650000 | NO | 14.088446 |
| 80 | 20 | MiddleCushion/1.000000 | L2 | L1 | 0.000000 | 1.000000 | 2.500000 | 5.000000 | 3.147924 | 2.500000 | YES | 3.650000 | NO | 13.883675 |
| 80 | 80 | MiddleCushion/1.000000 | L2 | L1 | 0.000000 | 1.000000 | 2.500000 | 5.000000 | 3.714550 | 2.500000 | YES | 3.650000 | NO | 13.884136 |

### Execution capacity vs realized movement

ExecutionCapacitySpread: `1.133252 m`; max/min ratio `1.439024`. ExecutionSkillCapacityExists: **YES**. ExecutionSkillCapacityWeak: **NO**. ExecutionPlannerBound: **YES**. PlanningHorizonLimitsExecutionExpression: **YES**.

### Adjacent-lane control

| SC | Adaptability | Target | Planned | Max move m | Actual move m | Cap binding | Arrived |
|---:|---:|---:|---:|---:|---:|---|---|
| 20 | 20 | L1 | L1 | 2.581297 | 2.500000 | NO | YES |
| 20 | 80 | L1 | L1 | 3.147924 | 2.500000 | NO | YES |
| 50 | 50 | L1 | L1 | 3.147924 | 2.500000 | NO | YES |
| 80 | 20 | L1 | L1 | 3.147924 | 2.500000 | NO | YES |
| 80 | 80 | L1 | L1 | 3.714550 | 2.500000 | NO | YES |

AdjacentExecutionFixtureNonDiscriminating: **YES**. Arrival uses the unchanged exact `0.050000 m` contract. This control is reported, not tuned to manufacture separation.

Planning-horizon answer: the current preparation segment resolves a distant target through the next reference-lane waypoint. In this fixture the waypoint cap, rather than available movement capacity, determines realized movement; whether a rider can begin early enough across multiple segments remains a future planning-horizon question.

## N. Natural 12-heat evolution

The meeting begins from Uniform, uses one persistent TrackState, four balanced riders per heat, `AdaptiveDecisionModel`, zero incident frequency, neutral setup, no weather delta, and the predeclared seeds 45001..45012. Rider state is recreated per heat; surface state is not reset. Evolution results are conditional on current production decision behavior, including the decision-objective alignment measured in sections J-L.

| State | Best | L0 | L1 | L2 | L3 | L4 | Best gap | AI most-chosen |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| heat 0 | L0 | 13.397707 | 13.737913 | 14.092064 | 14.456406 | 14.828415 | 0.340206 | N/A |
| heat 6 | L0 | 14.136330 | 14.584641 | 14.884148 | 15.019562 | 15.065384 | 0.448311 | L3 |
| heat 12 | L0 | 14.664715 | 15.201155 | 15.654789 | 15.732975 | 15.639336 | 0.536440 | L3 |
| after track work | L0 | 14.664715 | 15.057377 | 15.532753 | 15.732975 | 15.639336 | 0.392662 | L0 |

## O. Surface evolution by lane

Raw diagnostics preserve both logical corners. Values are the mean of the three production segment cells within that logical corner, sampled at the integer reference position.

| Heat | Corner | Lane | Grip | Ruts | Moisture | EffectiveGrip |
|---:|---:|---:|---:|---:|---:|---:|
| 0 | 1 | L0 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 0 | 1 | L1 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 0 | 1 | L2 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 0 | 1 | L3 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 0 | 1 | L4 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 0 | 2 | L0 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 0 | 2 | L1 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 0 | 2 | L2 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 0 | 2 | L3 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 0 | 2 | L4 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 1 | 1 | L0 | 0.965431 | 0.138275 | 0.350000 | 0.905407 |
| 1 | 1 | L1 | 0.973329 | 0.106682 | 0.350000 | 0.926680 |
| 1 | 1 | L2 | 0.988065 | 0.047740 | 0.350000 | 0.966842 |
| 1 | 1 | L3 | 0.995853 | 0.016587 | 0.350000 | 0.988425 |
| 1 | 1 | L4 | 0.999475 | 0.002100 | 0.350000 | 0.998531 |
| 1 | 2 | L0 | 0.959977 | 0.160090 | 0.350000 | 0.890911 |
| 1 | 2 | L1 | 0.972860 | 0.108560 | 0.350000 | 0.925430 |
| 1 | 2 | L2 | 0.991377 | 0.034491 | 0.350000 | 0.975997 |
| 1 | 2 | L3 | 0.999033 | 0.003870 | 0.350000 | 0.997293 |
| 1 | 2 | L4 | 1.000000 | 0.000000 | 0.350000 | 1.000000 |
| 2 | 1 | L0 | 0.940248 | 0.239009 | 0.350000 | 0.839213 |
| 2 | 1 | L1 | 0.943508 | 0.225969 | 0.350000 | 0.847702 |
| 2 | 1 | L2 | 0.969115 | 0.123541 | 0.350000 | 0.915245 |
| 2 | 1 | L3 | 0.990454 | 0.038184 | 0.350000 | 0.973453 |
| 2 | 1 | L4 | 0.998949 | 0.004205 | 0.350000 | 0.997059 |
| 2 | 2 | L0 | 0.931056 | 0.275775 | 0.350000 | 0.815588 |
| 2 | 2 | L1 | 0.945729 | 0.217083 | 0.350000 | 0.853398 |
| 2 | 2 | L2 | 0.973383 | 0.106469 | 0.350000 | 0.926763 |
| 2 | 2 | L3 | 0.994454 | 0.022183 | 0.350000 | 0.984534 |
| 2 | 2 | L4 | 0.999635 | 0.001462 | 0.350000 | 0.998977 |
| 3 | 1 | L0 | 0.922904 | 0.308382 | 0.350000 | 0.794949 |
| 3 | 1 | L1 | 0.915608 | 0.337567 | 0.350000 | 0.776836 |
| 3 | 1 | L2 | 0.942858 | 0.228566 | 0.350000 | 0.845893 |
| 3 | 1 | L3 | 0.981522 | 0.073912 | 0.350000 | 0.948953 |
| 3 | 1 | L4 | 0.998007 | 0.007974 | 0.350000 | 0.994428 |
| 3 | 2 | L0 | 0.906286 | 0.374854 | 0.350000 | 0.753434 |
| 3 | 2 | L1 | 0.922251 | 0.310996 | 0.350000 | 0.793242 |
| 3 | 2 | L2 | 0.949328 | 0.202687 | 0.350000 | 0.862856 |
| 3 | 2 | L3 | 0.987650 | 0.049399 | 0.350000 | 0.965728 |
| 3 | 2 | L4 | 0.999094 | 0.003624 | 0.350000 | 0.997468 |
| 4 | 1 | L0 | 0.913971 | 0.344117 | 0.350000 | 0.772574 |
| 4 | 1 | L1 | 0.900459 | 0.398163 | 0.350000 | 0.739525 |
| 4 | 1 | L2 | 0.915915 | 0.336339 | 0.350000 | 0.777293 |
| 4 | 1 | L3 | 0.954611 | 0.181554 | 0.350000 | 0.876778 |
| 4 | 1 | L4 | 0.993361 | 0.026558 | 0.350000 | 0.981495 |
| 4 | 2 | L0 | 0.895432 | 0.418271 | 0.350000 | 0.726904 |
| 4 | 2 | L1 | 0.906102 | 0.375590 | 0.350000 | 0.753050 |
| 4 | 2 | L2 | 0.924519 | 0.301923 | 0.350000 | 0.799224 |
| 4 | 2 | L3 | 0.961670 | 0.153318 | 0.350000 | 0.895410 |
| 4 | 2 | L4 | 0.994626 | 0.021497 | 0.350000 | 0.985012 |
| 5 | 1 | L0 | 0.898623 | 0.405507 | 0.350000 | 0.734804 |
| 5 | 1 | L1 | 0.877551 | 0.489794 | 0.350000 | 0.684725 |
| 5 | 1 | L2 | 0.890709 | 0.437162 | 0.350000 | 0.715569 |
| 5 | 1 | L3 | 0.942450 | 0.230200 | 0.350000 | 0.845091 |
| 5 | 1 | L4 | 0.988147 | 0.047413 | 0.350000 | 0.967074 |
| 5 | 2 | L0 | 0.882722 | 0.469109 | 0.350000 | 0.696386 |
| 5 | 2 | L1 | 0.882398 | 0.470404 | 0.350000 | 0.695743 |
| 5 | 2 | L2 | 0.898587 | 0.405649 | 0.350000 | 0.735289 |
| 5 | 2 | L3 | 0.945416 | 0.218336 | 0.350000 | 0.852669 |
| 5 | 2 | L4 | 0.991244 | 0.035023 | 0.350000 | 0.975651 |
| 6 | 1 | L0 | 0.890344 | 0.438621 | 0.350000 | 0.714787 |
| 6 | 1 | L1 | 0.870073 | 0.519706 | 0.350000 | 0.667381 |
| 6 | 1 | L2 | 0.876859 | 0.492562 | 0.350000 | 0.682687 |
| 6 | 1 | L3 | 0.916642 | 0.333432 | 0.350000 | 0.779698 |
| 6 | 1 | L4 | 0.964893 | 0.140427 | 0.350000 | 0.903937 |
| 6 | 2 | L0 | 0.870227 | 0.519089 | 0.350000 | 0.666953 |
| 6 | 2 | L1 | 0.872507 | 0.509970 | 0.350000 | 0.672491 |
| 6 | 2 | L2 | 0.886965 | 0.452139 | 0.350000 | 0.707524 |
| 6 | 2 | L3 | 0.923726 | 0.305093 | 0.350000 | 0.797089 |
| 6 | 2 | L4 | 0.968985 | 0.124060 | 0.350000 | 0.914940 |
| 7 | 1 | L0 | 0.864942 | 0.540231 | 0.350000 | 0.654928 |
| 7 | 1 | L1 | 0.848689 | 0.605240 | 0.350000 | 0.618386 |
| 7 | 1 | L2 | 0.865975 | 0.536099 | 0.350000 | 0.657184 |
| 7 | 1 | L3 | 0.904429 | 0.382281 | 0.350000 | 0.749728 |
| 7 | 1 | L4 | 0.956510 | 0.173959 | 0.350000 | 0.881656 |
| 7 | 2 | L0 | 0.847824 | 0.608702 | 0.350000 | 0.615592 |
| 7 | 2 | L1 | 0.852975 | 0.588099 | 0.350000 | 0.627456 |
| 7 | 2 | L2 | 0.870361 | 0.518552 | 0.350000 | 0.668617 |
| 7 | 2 | L3 | 0.911439 | 0.354240 | 0.350000 | 0.766301 |
| 7 | 2 | L4 | 0.961017 | 0.155933 | 0.350000 | 0.893725 |
| 8 | 1 | L0 | 0.859950 | 0.560199 | 0.350000 | 0.643428 |
| 8 | 1 | L1 | 0.834373 | 0.662503 | 0.350000 | 0.586843 |
| 8 | 1 | L2 | 0.844510 | 0.621959 | 0.350000 | 0.608364 |
| 8 | 1 | L3 | 0.882306 | 0.470773 | 0.350000 | 0.696446 |
| 8 | 1 | L4 | 0.938872 | 0.244511 | 0.350000 | 0.835615 |
| 8 | 2 | L0 | 0.843902 | 0.624390 | 0.350000 | 0.606788 |
| 8 | 2 | L1 | 0.847170 | 0.611319 | 0.350000 | 0.614334 |
| 8 | 2 | L2 | 0.845135 | 0.619456 | 0.350000 | 0.611228 |
| 8 | 2 | L3 | 0.884055 | 0.463778 | 0.350000 | 0.699911 |
| 8 | 2 | L4 | 0.942910 | 0.228359 | 0.350000 | 0.846160 |
| 9 | 1 | L0 | 0.836250 | 0.654997 | 0.350000 | 0.590123 |
| 9 | 1 | L1 | 0.815379 | 0.738479 | 0.350000 | 0.545653 |
| 9 | 1 | L2 | 0.832421 | 0.670314 | 0.350000 | 0.581716 |
| 9 | 1 | L3 | 0.870506 | 0.517975 | 0.350000 | 0.668724 |
| 9 | 1 | L4 | 0.927589 | 0.289644 | 0.350000 | 0.806812 |
| 9 | 2 | L0 | 0.817395 | 0.730417 | 0.350000 | 0.548768 |
| 9 | 2 | L1 | 0.830991 | 0.676034 | 0.350000 | 0.578338 |
| 9 | 2 | L2 | 0.835246 | 0.659014 | 0.350000 | 0.589763 |
| 9 | 2 | L3 | 0.871969 | 0.512121 | 0.350000 | 0.671361 |
| 9 | 2 | L4 | 0.930585 | 0.277660 | 0.350000 | 0.814606 |
| 10 | 1 | L0 | 0.828247 | 0.687010 | 0.350000 | 0.572619 |
| 10 | 1 | L1 | 0.798981 | 0.804071 | 0.350000 | 0.510923 |
| 10 | 1 | L2 | 0.812380 | 0.750476 | 0.350000 | 0.538630 |
| 10 | 1 | L3 | 0.851812 | 0.592748 | 0.350000 | 0.625730 |
| 10 | 1 | L4 | 0.910698 | 0.357206 | 0.350000 | 0.764519 |
| 10 | 2 | L0 | 0.809095 | 0.763615 | 0.350000 | 0.531190 |
| 10 | 2 | L1 | 0.818583 | 0.725666 | 0.350000 | 0.551377 |
| 10 | 2 | L2 | 0.820789 | 0.716839 | 0.350000 | 0.558910 |
| 10 | 2 | L3 | 0.847549 | 0.609803 | 0.350000 | 0.615326 |
| 10 | 2 | L4 | 0.910659 | 0.357361 | 0.350000 | 0.764670 |
| 11 | 1 | L0 | 0.813313 | 0.746746 | 0.350000 | 0.540506 |
| 11 | 1 | L1 | 0.776339 | 0.893810 | 0.350000 | 0.464738 |
| 11 | 1 | L2 | 0.789527 | 0.841889 | 0.350000 | 0.490957 |
| 11 | 1 | L3 | 0.838619 | 0.645524 | 0.350000 | 0.596130 |
| 11 | 1 | L4 | 0.903695 | 0.385219 | 0.350000 | 0.747321 |
| 11 | 2 | L0 | 0.793583 | 0.825663 | 0.350000 | 0.499034 |
| 11 | 2 | L1 | 0.798643 | 0.805425 | 0.350000 | 0.509263 |
| 11 | 2 | L2 | 0.790168 | 0.807844 | 0.350000 | 0.505865 |
| 11 | 2 | L3 | 0.836482 | 0.654070 | 0.350000 | 0.590582 |
| 11 | 2 | L4 | 0.906963 | 0.372145 | 0.350000 | 0.755643 |
| 12 | 1 | L0 | 0.808337 | 0.766647 | 0.350000 | 0.529963 |
| 12 | 1 | L1 | 0.765972 | 0.923997 | 0.350000 | 0.447914 |
| 12 | 1 | L2 | 0.766919 | 0.922719 | 0.350000 | 0.449180 |
| 12 | 1 | L3 | 0.812853 | 0.748586 | 0.350000 | 0.540286 |
| 12 | 1 | L4 | 0.886761 | 0.452956 | 0.350000 | 0.706655 |
| 12 | 2 | L0 | 0.791463 | 0.834145 | 0.350000 | 0.494713 |
| 12 | 2 | L1 | 0.789196 | 0.843211 | 0.350000 | 0.489837 |
| 12 | 2 | L2 | 0.772126 | 0.851866 | 0.350000 | 0.478687 |
| 12 | 2 | L3 | 0.811850 | 0.752596 | 0.350000 | 0.537197 |
| 12 | 2 | L4 | 0.881706 | 0.473173 | 0.350000 | 0.694519 |

Initial -> final mean-corner grip changes: L0 -0.200100, L1 -0.222416, L2 -0.230478, L3 -0.187649, L4 -0.115767.
Initial -> final mean-corner ruts changes: L0 0.800396, L1 0.883604, L2 0.887292, L3 0.750591, L4 0.463065.

## P. Line usage by heat

Usage bins actual continuous turn-entry/turn traversal positions to the nearest reference only for diagnostic counting; wear itself remains production continuous wear.

| Heat | Usage L0/L1/L2/L3/L4 | Most used | Target L0/L1/L2/L3/L4 | Entry-bin L0/L1/L2/L3/L4 | Mean entry lateral |
|---:|---|---|---|---|---:|
| 0 | 0/0/0/0/0 | N/A | 0/0/0/0/0 | 0/0/0/0/0 | 0.000000 |
| 1 | 56/28/10/2/0 | L0 | 78/37/11/2/16 | 17/11/3/1/0 | 0.705857 |
| 2 | 37/34/21/4/0 | L0 | 60/34/29/5/16 | 13/10/8/1/0 | 0.954468 |
| 3 | 27/31/32/6/0 | L2 | 53/22/40/13/16 | 11/6/14/1/0 | 1.202867 |
| 4 | 13/16/33/34/0 | L3 | 42/14/23/49/16 | 4/6/10/12/0 | 2.014376 |
| 5 | 18/27/33/14/4 | L2 | 56/16/14/30/28 | 5/9/13/4/1 | 1.668993 |
| 6 | 15/8/15/29/29 | L3 | 43/7/8/24/62 | 5/3/5/11/8 | 2.539276 |
| 7 | 34/23/15/15/9 | L0 | 65/12/13/17/37 | 12/7/4/6/3 | 1.501468 |
| 8 | 5/11/29/30/21 | L3 | 36/13/13/28/54 | 2/2/11/9/8 | 2.701509 |
| 9 | 37/18/12/14/15 | L0 | 64/12/13/12/43 | 13/6/3/4/6 | 1.602796 |
| 10 | 9/18/20/25/24 | L3 | 33/11/18/25/57 | 4/6/6/8/8 | 2.412619 |
| 11 | 19/24/37/10/6 | L2 | 50/12/26/10/46 | 8/8/10/4/2 | 1.627280 |
| 12 | 4/10/24/31/27 | L3 | 38/9/8/21/68 | 1/4/9/12/6 | 2.690676 |

## Q. Fixed-line optimum after each heat

| Heat | Best | Ranking | Best Flying | Second gap | L0 | L1 | L2 | L3 | L4 |
|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 0 | L0 | L0 -> L1 -> L2 -> L3 -> L4 | 13.397707 | 0.340206 | 13.397707 | 13.737913 | 14.092064 | 14.456406 | 14.828415 |
| 1 | L0 | L0 -> L1 -> L2 -> L3 -> L4 | 13.643007 | 0.273010 | 13.643007 | 13.916018 | 14.162426 | 14.476400 | 14.830742 |
| 2 | L0 | L0 -> L1 -> L2 -> L3 -> L4 | 13.810019 | 0.290068 | 13.810019 | 14.100086 | 14.272564 | 14.515995 | 14.835686 |
| 3 | L0 | L0 -> L1 -> L2 -> L3 -> L4 | 13.935316 | 0.333765 | 13.935316 | 14.269081 | 14.431633 | 14.580402 | 14.844101 |
| 4 | L0 | L0 -> L1 -> L2 -> L3 -> L4 | 13.994020 | 0.379528 | 13.994020 | 14.373549 | 14.612183 | 14.756771 | 14.875362 |
| 5 | L0 | L0 -> L1 -> L2 -> L3 -> L4 | 14.072971 | 0.452002 | 14.072971 | 14.524973 | 14.792648 | 14.849276 | 14.907169 |
| 6 | L0 | L0 -> L1 -> L2 -> L3 -> L4 | 14.136330 | 0.448311 | 14.136330 | 14.584641 | 14.884148 | 15.019562 | 15.065384 |
| 7 | L0 | L0 -> L1 -> L2 -> L3 -> L4 | 14.278889 | 0.434513 | 14.278889 | 14.713402 | 14.972754 | 15.106762 | 15.124786 |
| 8 | L0 | L0 -> L1 -> L2 -> L4 -> L3 | 14.306705 | 0.475933 | 14.306705 | 14.782639 | 15.125578 | 15.274837 | 15.236580 |
| 9 | L0 | L0 -> L1 -> L2 -> L4 -> L3 | 14.463541 | 0.433247 | 14.463541 | 14.896788 | 15.217426 | 15.353245 | 15.319691 |
| 10 | L0 | L0 -> L1 -> L2 -> L4 -> L3 | 14.514744 | 0.473679 | 14.514744 | 14.988422 | 15.357681 | 15.487793 | 15.450317 |
| 11 | L0 | L0 -> L1 -> L4 -> L2 -> L3 | 14.633595 | 0.494888 | 14.633595 | 15.128483 | 15.530003 | 15.556328 | 15.489151 |
| 12 | L0 | L0 -> L1 -> L4 -> L2 -> L3 | 14.664715 | 0.536440 | 14.664715 | 15.201155 | 15.654789 | 15.732975 | 15.639336 |

FirstEvolutionLineSwitchHeat: **NONE**. InitialLineSpread `1.430708 s`; FinalLineSpread `1.068260 s`.

## R. AI response to evolving optimum

Physical best sequence H0-H12: `L0 -> L0 -> L0 -> L0 -> L0 -> L0 -> L0 -> L0 -> L0 -> L0 -> L0 -> L0 -> L0`.
Most-used sequence H1-H12: `L0 -> L0 -> L2 -> L3 -> L2 -> L3 -> L0 -> L3 -> L0 -> L3 -> L2 -> L3`.
Target distributions and actual entry-position distributions are reported in section P, keeping perception, intent, and execution separate.

## S. Feedback-loop / oscillation guardrails

Healthy popularity -> wear -> line-change feedback observed: **NO**. TrackEvolutionOscillationRisk (>4 physical-best switches): **NO**. TrackEvolutionTooWeak: **NO**. TrackEvolutionTooStrong: **YES**. The bounded strong flag is raised when any fixed-line flying shift exceeds 1.00 s or any final corner EffectiveGrip falls below 0.25; the weak flag requires wear plus no best-line switch and less than 0.01 s spread change. These are diagnostic thresholds, not calibration failures.
Wear distribution and feedback-loop interpretation are conditional on current production decision behavior; they are not an independent calibration verdict while the decision objective differs from the fastest fixed-line benchmark.

## T. Track-work manager intervention

`Pack`, intensity 0.50, turn segments only. The two lanes selected automatically by heat-12 mean turn ruts are `L2, L1`.

Before ranking: `L0 -> L1 -> L4 -> L2 -> L3`. After ranking: `L0 -> L1 -> L2 -> L4 -> L3`. AI before/after: `L0 -> L0`.

| Lane | Grip delta | Ruts delta | Moisture delta | EffectiveGrip delta | Flying delta s |
|---:|---:|---:|---:|---:|---:|
| L0 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 0.000000 |
| L1 | 0.030000 | -0.060000 | -0.012500 | 0.037376 | -0.143778 |
| L2 | 0.030000 | -0.060000 | -0.012500 | 0.037109 | -0.122036 |
| L3 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 0.000000 |
| L4 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 0.000000 |

## U. Weather sanity

One existing `ApplyWeather` tick uses `Rain`, rain intensity `0.500000`. Lane exposure creates different turn surfaces: **YES**.

| Lane | Moisture before | Moisture after | Delta |
|---:|---:|---:|---:|
| L0 | 0.350000 | 0.362500 | 0.012500 |
| L1 | 0.350000 | 0.362813 | 0.012813 |
| L2 | 0.350000 | 0.363125 | 0.013125 |
| L3 | 0.350000 | 0.363438 | 0.013438 |
| L4 | 0.350000 | 0.363750 | 0.013750 |

## V. Frozen production systems

Production behavior changed: **NO**. `SegmentPhysics`, `ContinuousCornerEnvelope`, `LongitudinalDynamics`, `LateralMovementModel`, `AdaptiveDecisionModel`, `TrackEvolution`, `TrackState`, wear/surface/setup/incident formulas, `StandingStart`, and Legacy are unchanged. `StraightDriveEnvelopeAdjustment`, `CornerReducedDriveResistanceAdjustment`, `PreApexScrubLossAdjustment`, and `ActiveCorrectionControlLossAdjustment` are all null. Historical pge-v1 and reports #38-#44 are SHA-256 guarded.

## W. Gameplay interpretation

1. Can production physics make L1/L2/L3/L4 faster than L0 on lane-specific surface? **YES**.
2. First surface threshold: Outside non-inner `1.000000`, Outside outer-half `1.000000`, Middle non-inner `1.000000`, Middle outer-half `1.000000`.
3. Can a middle line naturally become best? **YES**.
4. Can the outer line naturally become best? **NO**.
5. Does AdaptiveDecisionModel notice a non-inner optimum? **YES**.
6. Does high TrackReading converge toward the TR100 production oracle? **YES**.
7. Does low TrackReading sometimes differ from the production oracle? **YES**.
8. Do Adaptability/SlideControl change available execution capacity? **YES**.
9. Can natural wear move the optimum during 12 heats? **NO**.
10. Does AI usage respond during evolution? **YES**.
11. Is a healthy feedback loop observed? **NO**.
12. Is oscillation risk present? **NO**.
13. Does manager track work change line economy? **YES**.
14. Is one lane universally best in every static state? **NO**.
15. First actual bottleneck: **decision objective alignment**.
Physical regret worsens from TR20 aggregate `0.101301` s to TR80 `0.132006` s while oracle agreement improves: **YES**. This is decision-objective mismatch evidence, not perception failure.

## X. Failure-layer diagnosis

1. Physical line economy: `responds, but requires strong surface contrast`.
2. Decision objective alignment: `TR100 oracle differs materially from fastest fixed line`.
3. TrackReading perception: `healthy convergence to TR100 oracle`.
4. Planning horizon: `waypoint cap limits expression`.
5. Execution capacity: `skill-dependent capacity exists`.
6. Evolution: `too strong, conditional on current decisions`. Manager work: `noticeable`.

Classification: **`MixedDynamicLineGameplayDiagnostics: LineSwitchRequiresStrongSurfaceContrast, DecisionObjectiveVsFastestLineMismatch, TrackReadingPerceptionSignalHealthy, ExecutionPlannerBound, PlanningHorizonLimitsExecutionExpression, TrackEvolutionTooStrong`**.

## Y. Recommended #46 subsystem

Recommended single subsystem: **AdaptiveDecisionModel route-cost alignment**. This follows the mandatory diagnostic order and does not implement the fix in #45.

## Z. Limitations

This is a bounded gameplay-architecture experiment, not a fit to real lane trajectories. PGE telemetry has no per-point lane/surface truth. `surface sampled at current production granularity; not continuously resampled every physical metre`. The `constant-reference-line benchmark does not model diagonal/spiral racing trajectory`; therefore L4 not being fastest does not rule out a future effective outside trajectory. The current model also keeps five reference bands, entry-position corner geometry and wear, provisional occupancy/contact widths, and no diagonal/spiral distance correction. Weather receives one sanity tick only. Traffic is present in the meeting probe but excluded from one-rider physical benchmarks.
