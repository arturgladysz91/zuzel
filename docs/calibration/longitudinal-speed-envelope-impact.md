# Longitudinal speed-envelope calibration impact

Base main SHA: `842e0ce861466cdf5a67287c155f81d879cf6666`

Branch candidate HEAD used to generate the after-state: `49d4d5ab8567f022bbea2cdd3bc9416e13b38b5c`

The candidate HEAD is the code-only calibration checkpoint used before materializing this generated report; the Draft PR body records the final one-commit branch HEAD. A commit cannot contain its own final SHA because that text changes the commit hash.

## Scope and method

This is the first bounded calibration pass over the existing signed-force model. No cap, optimizer, alternate heat loop or surrogate traversal was added. Straight and TurnExit observations call the production `LongitudinalDynamics` profiles; controlled corner/start observations reuse the #35 `CalibrationScenarioSuite`; complete heats run through `CalibrationRunner → HeatSimulator → SimulationEngine` with normal production surface evolution.

The signed model remains `F_net(v) = F_drive(v) - F_resistance(v)` and `a(v) = F_net(v) / 142 kg`. `F_resistance(v) = 40 + 0.20v²` N. Equilibrium is a diagnostic root, never a limiter, clamp, target or integration stop. The reported effective power diagnostic is only `F_drive(v) × v`; it is not literal crankshaft power and does not introduce RPM, torque, sprocket, wheelspin, slip, traction-cap or engine-efficiency physics.

The search followed the prescribed hierarchy. At each stage only a small deterministic menu was evaluated, with no weighted objective: Straight reference acceleration 1.5× then 2×; TurnExit reference acceleration 1.5× then 2×; then both fade endpoints 1.5× and 2×. Reference speed and envelope shape were not opened because Stage C produced finite-distance gains with credible signed-force equilibria and a useful gearing crossover.

## Bounded candidate screen

All heat medians below are the four-rider `full_heat/speed/050` production fixture. Candidate rows are sequential: later stages include the selected earlier-stage values.

| Candidate | Straight range | TurnExit range | Fade drive/speed | Straight 60 m exit | Straight equilibrium | TurnExit exit | TurnExit equilibrium | Heat Vmax P50 km/h | Heat average P50 m/s | Heat L1 penalty P50 s | Decision |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Baseline | 0.80–1.60 | 0.60–1.40 | 0.01750/0.00500 | 19.59488 | 30.02662 | 18.08419 | 28.35396 | 76.432554 | 18.14121 | 1.057449 | diagnosis |
| A1 | 1.20–2.40 | 0.60–1.40 | 0.01750/0.00500 | 21.1202 | 34.30593 | 18.08419 | 28.35396 | 79.520327 | 18.284455 | 1.097585 | retain A2 instead |
| A2 | 1.60–3.20 | 0.60–1.40 | 0.01750/0.00500 | 22.51816 | 37.81421 | 18.08419 | 28.35396 | 81.846088 | 18.39057 | 1.118208 | retain |
| B1 | 1.60–3.20 | 0.90–2.10 | 0.01750/0.00500 | 22.51816 | 37.81421 | 18.63987 | 32.28527 | 82.916452 | 18.579705 | 1.127445 | retain B2 instead |
| B2 | 1.60–3.20 | 1.20–2.80 | 0.01750/0.00500 | 22.51816 | 37.81421 | 19.17608 | 35.54561 | 83.903985 | 18.75129 | 1.133822 | retain |
| C1 | 1.60–3.20 | 1.20–2.80 | 0.02625/0.00750 | 22.37066 | 35.39729 | 19.14208 | 33.49741 | 83.476075 | 18.728495 | 1.134804 | test steeper fade |
| C2 selected | 1.60–3.20 | 1.20–2.80 | 0.03500/0.01000 | 22.22699 | 33.3896 | 19.1085 | 31.7796 | 83.104438 | 18.70542 | 1.135835 | selected |

C2 preserves nearly all finite 60 m and full-heat response of B2 while lowering the neutral Straight/TurnExit equilibrium diagnostics by about 4.4/3.8 m/s. No reference-speed or nonlinear-envelope change is justified.

## Selected constants and frozen boundaries

| Parameter | Before | After | Status |
| --- | --- | --- | --- |
| Straight reference acceleration m/s² | 0.8–1.6 | 1.6–3.2 | tuned proportionally (Stage A) |
| TurnExit reference acceleration m/s² | 0.6–1.4 | 1.2–2.8 | tuned proportionally (Stage B); Straight remains >= TurnExit |
| Drive/speed-oriented fade 1/(m/s) | 0.0175/0.005 | 0.035/0.01 | tuned together (Stage C) |
| Reference speed m/s | 16 | 16 | frozen |
| Mass kg | 142 | 142 | frozen |
| Resistance N | 40 + 0.2v² | 40 + 0.2v² | frozen |
| Integration step m | 1 | 1 | frozen |
| Gearing drive multipliers | 1.1–0.9 | 1.1–0.9 | frozen |
| Surface mapping | 0.75 + 0.25 × EffectiveGrip | 0.75 + 0.25 × EffectiveGrip | frozen |
| Start reaction s | 0.28–0.2 | 0.28–0.2 | frozen |
| Start acceleration m/s² | 9–11 | 9–11 | frozen |
| Corner correction m/s² | 2–3.2 | 2–3.2 | frozen |


## Straight finite-distance response

Free-drive production profiles; no following-corner target. Phase distances are printed after calibration and continue to reconcile exactly to requested distance.

| Scenario | Speed | Gearing | Distance | Entry | Exit before | Exit after | Peak before | Peak after | Time before | Time after | A/C/D after | Equilibrium before | Equilibrium after |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| distance/010 | 50 | 0.5 | 10 | 16 | 16.71872 | 17.38864 | 16.71872 | 17.38864 | 0.61118 | 0.598722 | 10/0/0 | 30.02662 | 33.3896 |
| distance/020 | 50 | 0.5 | 20 | 16 | 17.38091 | 18.59295 | 17.38091 | 18.59295 | 1.197621 | 1.154359 | 20/0/0 | 30.02662 | 33.3896 |
| distance/030 | 50 | 0.5 | 30 | 16 | 17.99413 | 19.65457 | 17.99413 | 19.65457 | 1.762927 | 1.677115 | 30/0/0 | 30.02662 | 33.3896 |
| distance/060 | 50 | 0.5 | 60 | 16 | 19.59488 | 22.22699 | 19.59488 | 22.22699 | 3.35781 | 3.107062 | 60/0/0 | 30.02662 | 33.3896 |
| distance/100 | 50 | 0.5 | 100 | 16 | 21.30854 | 24.71977 | 21.30854 | 24.71977 | 5.31155 | 4.807678 | 100/0/0 | 30.02662 | 33.3896 |
| distance/300 | 50 | 0.5 | 300 | 16 | 26.13476 | 30.51291 | 26.13476 | 30.51291 | 13.63987 | 11.92173 | 300/0/0 | 30.02662 | 33.3896 |
| distance/600 | 50 | 0.5 | 600 | 16 | 28.73876 | 32.75316 | 28.73876 | 32.75316 | 24.48416 | 21.32584 | 600/0/0 | 30.02662 | 33.3896 |
| entry/010 | 50 | 0.5 | 30 | 10 | 13.47899 | 15.83431 | 13.47899 | 15.83431 | 2.550162 | 2.315143 | 30/0/0 | 30.02662 | 33.3896 |
| entry/016 | 50 | 0.5 | 30 | 16 | 17.99413 | 19.65457 | 17.99413 | 19.65457 | 1.762927 | 1.677115 | 30/0/0 | 30.02662 | 33.3896 |
| entry/020 | 50 | 0.5 | 30 | 20 | 21.25609 | 22.48247 | 21.25609 | 22.48247 | 1.453486 | 1.409866 | 30/0/0 | 30.02662 | 33.3896 |
| entry/024 | 50 | 0.5 | 30 | 24 | 24.68096 | 25.56186 | 24.68096 | 25.56186 | 1.232199 | 1.209547 | 30/0/0 | 30.02662 | 33.3896 |
| speed/000 | 0 | 0.5 | 30 | 16 | 17.35982 | 18.55008 | 17.35982 | 18.55008 | 1.797185 | 1.732951 | 30/0/0 | 26.51224 | 29.90096 |
| speed/025 | 25 | 0.5 | 30 | 16 | 17.68041 | 19.11405 | 17.68041 | 19.11405 | 1.779716 | 1.704047 | 30/0/0 | 28.35396 | 31.7796 |
| speed/050 | 50 | 0.5 | 30 | 16 | 17.99413 | 19.65457 | 17.99413 | 19.65457 | 1.762927 | 1.677115 | 30/0/0 | 30.02662 | 33.3896 |
| speed/075 | 75 | 0.5 | 30 | 16 | 18.30138 | 20.1738 | 18.30138 | 20.1738 | 1.74677 | 1.651925 | 30/0/0 | 31.56181 | 34.79316 |
| speed/100 | 100 | 0.5 | 30 | 16 | 18.6025 | 20.67361 | 18.6025 | 20.67361 | 1.731202 | 1.628281 | 30/0/0 | 32.98248 | 36.03298 |


## Force, acceleration and effective-power curves

Every row uses the production envelope and resistance helpers at the requested speed. Effective power is `F_drive × v` only; it is not a literal engine-power model.

| Speed skill | Gearing | v | Envelope before | Envelope after | Drive N before | Drive N after | Resistance N | Net N before | Net N after | a before | a after | Effective W before | Effective W after |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 0 | 10 | 1 | 1 | 216.16 | 341.12 | 60 | 156.16 | 281.12 | 1.099718 | 1.979718 | 2161.6 | 3411.2 |
| 0 | 0 | 16 | 1 | 1 | 216.16 | 341.12 | 91.2 | 124.96 | 249.92 | 0.88 | 1.76 | 3458.56 | 5457.92 |
| 0 | 0 | 20 | 0.93 | 0.86 | 201.0288 | 293.3632 | 120 | 81.02881 | 173.3632 | 0.570625 | 1.220868 | 4020.576 | 5867.264 |
| 0 | 0 | 24 | 0.86 | 0.72 | 185.8976 | 245.6064 | 155.2 | 30.69762 | 90.4064 | 0.21618 | 0.636665 | 4461.543 | 5894.554 |
| 0 | 0 | 28 | 0.79 | 0.58 | 170.7664 | 197.8496 | 196.8 | -26.0336 | 1.049591 | -0.183335 | 0.007391 | 4781.459 | 5539.789 |
| 0 | 0 | 30 | 0.755 | 0.51 | 163.2008 | 173.9712 | 220 | -56.79919 | -46.02881 | -0.399994 | -0.324147 | 4896.024 | 5219.136 |
| 0 | 0 | 32 | 0.72 | 0.44 | 155.6352 | 150.0928 | 244.8 | -89.16479 | -94.7072 | -0.627921 | -0.666952 | 4980.327 | 4802.97 |
| 0 | 0 | 36 | 0.65 | 0.3 | 140.504 | 102.336 | 299.2 | -158.696 | -196.864 | -1.117578 | -1.386366 | 5058.144 | 3684.096 |
| 0 | 0.5 | 10 | 1 | 1 | 204.8 | 318.4 | 60 | 144.8 | 258.4 | 1.019718 | 1.819718 | 2048 | 3184 |
| 0 | 0.5 | 16 | 1 | 1 | 204.8 | 318.4 | 91.2 | 113.6 | 227.2 | 0.8 | 1.6 | 3276.8 | 5094.4 |
| 0 | 0.5 | 20 | 0.955 | 0.91 | 195.584 | 289.744 | 120 | 75.58398 | 169.744 | 0.532282 | 1.19538 | 3911.68 | 5794.88 |
| 0 | 0.5 | 24 | 0.91 | 0.82 | 186.368 | 261.088 | 155.2 | 31.168 | 105.888 | 0.219493 | 0.74569 | 4472.832 | 6266.111 |
| 0 | 0.5 | 28 | 0.865 | 0.73 | 177.152 | 232.432 | 196.8 | -19.64801 | 35.632 | -0.138366 | 0.25093 | 4960.256 | 6508.096 |
| 0 | 0.5 | 30 | 0.8425 | 0.685 | 172.544 | 218.104 | 220 | -47.45601 | -1.895996 | -0.334197 | -0.013352 | 5176.32 | 6543.12 |
| 0 | 0.5 | 32 | 0.82 | 0.64 | 167.936 | 203.776 | 244.8 | -76.86401 | -41.02402 | -0.541296 | -0.288902 | 5373.952 | 6520.832 |
| 0 | 0.5 | 36 | 0.775 | 0.55 | 158.72 | 175.12 | 299.2 | -140.48 | -124.08 | -0.989296 | -0.873803 | 5713.919 | 6304.32 |
| 0 | 1 | 10 | 1 | 1 | 193.44 | 295.68 | 60 | 133.44 | 235.68 | 0.939718 | 1.659718 | 1934.4 | 2956.8 |
| 0 | 1 | 16 | 1 | 1 | 193.44 | 295.68 | 91.2 | 102.24 | 204.48 | 0.72 | 1.44 | 3095.04 | 4730.88 |
| 0 | 1 | 20 | 0.98 | 0.96 | 189.5712 | 283.8528 | 120 | 69.57121 | 163.8528 | 0.489938 | 1.153893 | 3791.424 | 5677.056 |
| 0 | 1 | 24 | 0.96 | 0.92 | 185.7024 | 272.0256 | 155.2 | 30.5024 | 116.8256 | 0.214806 | 0.822716 | 4456.857 | 6528.614 |
| 0 | 1 | 28 | 0.94 | 0.88 | 181.8336 | 260.1984 | 196.8 | -14.9664 | 63.39839 | -0.105397 | 0.446468 | 5091.341 | 7285.555 |
| 0 | 1 | 30 | 0.93 | 0.86 | 179.8992 | 254.2848 | 220 | -40.1008 | 34.28481 | -0.2824 | 0.241442 | 5396.976 | 7628.544 |
| 0 | 1 | 32 | 0.92 | 0.84 | 177.9648 | 248.3712 | 244.8 | -66.83521 | 3.571198 | -0.470671 | 0.025149 | 5694.874 | 7947.878 |
| 0 | 1 | 36 | 0.9 | 0.8 | 174.096 | 236.544 | 299.2 | -125.104 | -62.65602 | -0.881014 | -0.44124 | 6267.456 | 8515.584 |
| 50 | 0 | 10 | 1 | 1 | 278.64 | 466.08 | 60 | 218.64 | 406.08 | 1.539718 | 2.859718 | 2786.4 | 4660.8 |
| 50 | 0 | 16 | 1 | 1 | 278.64 | 466.08 | 91.2 | 187.44 | 374.88 | 1.32 | 2.64 | 4458.24 | 7457.28 |
| 50 | 0 | 20 | 0.93 | 0.86 | 259.1352 | 400.8288 | 120 | 139.1352 | 280.8288 | 0.979826 | 1.977668 | 5182.705 | 8016.577 |
| 50 | 0 | 24 | 0.86 | 0.72 | 239.6304 | 335.5776 | 155.2 | 84.43042 | 180.3776 | 0.59458 | 1.270265 | 5751.13 | 8053.863 |
| 50 | 0 | 28 | 0.79 | 0.58 | 220.1256 | 270.3264 | 196.8 | 23.32561 | 73.52641 | 0.164265 | 0.517792 | 6163.517 | 7569.14 |
| 50 | 0 | 30 | 0.755 | 0.51 | 210.3732 | 237.7008 | 220 | -9.626785 | 17.70081 | -0.067794 | 0.124654 | 6311.196 | 7131.024 |
| 50 | 0 | 32 | 0.72 | 0.44 | 200.6208 | 205.0752 | 244.8 | -44.17918 | -39.72479 | -0.311121 | -0.279752 | 6419.866 | 6562.407 |
| 50 | 0 | 36 | 0.65 | 0.3 | 181.116 | 139.824 | 299.2 | -118.084 | -159.376 | -0.831578 | -1.122366 | 6520.176 | 5033.664 |
| 50 | 0.5 | 10 | 1 | 1 | 261.6 | 432 | 60 | 201.6 | 372 | 1.419718 | 2.619718 | 2616 | 4320 |
| 50 | 0.5 | 16 | 1 | 1 | 261.6 | 432 | 91.2 | 170.4 | 340.8 | 1.2 | 2.4 | 4185.6 | 6912 |
| 50 | 0.5 | 20 | 0.955 | 0.91 | 249.828 | 393.12 | 120 | 129.828 | 273.12 | 0.914282 | 1.92338 | 4996.56 | 7862.4 |
| 50 | 0.5 | 24 | 0.91 | 0.82 | 238.056 | 354.24 | 155.2 | 82.85602 | 199.04 | 0.583493 | 1.40169 | 5713.344 | 8501.76 |
| 50 | 0.5 | 28 | 0.865 | 0.73 | 226.284 | 315.36 | 196.8 | 29.48401 | 118.56 | 0.207634 | 0.83493 | 6335.952 | 8830.08 |
| 50 | 0.5 | 30 | 0.8425 | 0.685 | 220.398 | 295.92 | 220 | 0.397995 | 75.92001 | 0.002803 | 0.534648 | 6611.94 | 8877.601 |
| 50 | 0.5 | 32 | 0.82 | 0.64 | 214.512 | 276.48 | 244.8 | -30.28799 | 31.67998 | -0.213296 | 0.223098 | 6864.384 | 8847.359 |
| 50 | 0.5 | 36 | 0.775 | 0.55 | 202.74 | 237.6 | 299.2 | -96.46001 | -61.60001 | -0.679296 | -0.433803 | 7298.64 | 8553.601 |
| 50 | 1 | 10 | 1 | 1 | 244.56 | 397.92 | 60 | 184.56 | 337.92 | 1.299718 | 2.379718 | 2445.6 | 3979.2 |
| 50 | 1 | 16 | 1 | 1 | 244.56 | 397.92 | 91.2 | 153.36 | 306.72 | 1.08 | 2.16 | 3912.96 | 6366.72 |
| 50 | 1 | 20 | 0.98 | 0.96 | 239.6688 | 382.0032 | 120 | 119.6688 | 262.0032 | 0.842738 | 1.845093 | 4793.376 | 7640.063 |
| 50 | 1 | 24 | 0.96 | 0.92 | 234.7776 | 366.0864 | 155.2 | 79.57759 | 210.8864 | 0.560406 | 1.485116 | 5634.662 | 8786.073 |
| 50 | 1 | 28 | 0.94 | 0.88 | 229.8864 | 350.1696 | 196.8 | 33.0864 | 153.3696 | 0.233003 | 1.080068 | 6436.819 | 9804.748 |
| 50 | 1 | 30 | 0.93 | 0.86 | 227.4408 | 342.2112 | 220 | 7.440796 | 122.2112 | 0.0524 | 0.860642 | 6823.224 | 10266.34 |
| 50 | 1 | 32 | 0.92 | 0.84 | 224.9952 | 334.2528 | 244.8 | -19.80479 | 89.4528 | -0.13947 | 0.629949 | 7199.847 | 10696.09 |
| 50 | 1 | 36 | 0.9 | 0.8 | 220.104 | 318.336 | 299.2 | -79.09602 | 19.13599 | -0.557014 | 0.134761 | 7923.744 | 11460.1 |
| 100 | 0 | 10 | 1 | 1 | 341.12 | 591.04 | 60 | 281.12 | 531.04 | 1.979718 | 3.739719 | 3411.2 | 5910.4 |
| 100 | 0 | 16 | 1 | 1 | 341.12 | 591.04 | 91.2 | 249.92 | 499.84 | 1.76 | 3.52 | 5457.92 | 9456.641 |
| 100 | 0 | 20 | 0.93 | 0.86 | 317.2416 | 508.2944 | 120 | 197.2416 | 388.2944 | 1.389025 | 2.734468 | 6344.832 | 10165.89 |
| 100 | 0 | 24 | 0.86 | 0.72 | 293.3632 | 425.5489 | 155.2 | 138.1632 | 270.3489 | 0.97298 | 1.903865 | 7040.717 | 10213.17 |
| 100 | 0 | 28 | 0.79 | 0.58 | 269.4848 | 342.8032 | 196.8 | 72.6848 | 146.0032 | 0.511865 | 1.028192 | 7545.574 | 9598.49 |
| 100 | 0 | 30 | 0.755 | 0.51 | 257.5456 | 301.4304 | 220 | 37.54559 | 81.43042 | 0.264406 | 0.573454 | 7726.368 | 9042.912 |
| 100 | 0 | 32 | 0.72 | 0.44 | 245.6064 | 260.0576 | 244.8 | 0.806397 | 15.25761 | 0.005679 | 0.107448 | 7859.405 | 8321.844 |
| 100 | 0 | 36 | 0.65 | 0.3 | 221.728 | 177.312 | 299.2 | -77.47203 | -121.888 | -0.545578 | -0.858366 | 7982.208 | 6383.232 |
| 100 | 0.5 | 10 | 1 | 1 | 318.4 | 545.6 | 60 | 258.4 | 485.6 | 1.819718 | 3.419718 | 3184 | 5456 |
| 100 | 0.5 | 16 | 1 | 1 | 318.4 | 545.6 | 91.2 | 227.2 | 454.4 | 1.6 | 3.2 | 5094.4 | 8729.6 |
| 100 | 0.5 | 20 | 0.955 | 0.91 | 304.072 | 496.496 | 120 | 184.072 | 376.496 | 1.296282 | 2.65138 | 6081.44 | 9929.92 |
| 100 | 0.5 | 24 | 0.91 | 0.82 | 289.744 | 447.392 | 155.2 | 134.544 | 292.192 | 0.947493 | 2.05769 | 6953.855 | 10737.41 |
| 100 | 0.5 | 28 | 0.865 | 0.73 | 275.416 | 398.288 | 196.8 | 78.61598 | 201.488 | 0.553634 | 1.418929 | 7711.647 | 11152.06 |
| 100 | 0.5 | 30 | 0.8425 | 0.685 | 268.252 | 373.736 | 220 | 48.25198 | 153.736 | 0.339803 | 1.082648 | 8047.56 | 11212.08 |
| 100 | 0.5 | 32 | 0.82 | 0.64 | 261.088 | 349.184 | 244.8 | 16.28798 | 104.384 | 0.114704 | 0.735099 | 8354.815 | 11173.89 |
| 100 | 0.5 | 36 | 0.775 | 0.55 | 246.76 | 300.08 | 299.2 | -52.44002 | 0.879974 | -0.369296 | 0.006197 | 8883.359 | 10802.88 |
| 100 | 1 | 10 | 1 | 1 | 295.68 | 500.16 | 60 | 235.68 | 440.16 | 1.659718 | 3.099718 | 2956.8 | 5001.6 |
| 100 | 1 | 16 | 1 | 1 | 295.68 | 500.16 | 91.2 | 204.48 | 408.96 | 1.44 | 2.88 | 4730.88 | 8002.56 |
| 100 | 1 | 20 | 0.98 | 0.96 | 289.7664 | 480.1536 | 120 | 169.7664 | 360.1536 | 1.195538 | 2.536293 | 5795.328 | 9603.071 |
| 100 | 1 | 24 | 0.96 | 0.92 | 283.8528 | 460.1472 | 155.2 | 128.6528 | 304.9472 | 0.906006 | 2.147516 | 6812.467 | 11043.53 |
| 100 | 1 | 28 | 0.94 | 0.88 | 277.9392 | 440.1408 | 196.8 | 81.13918 | 243.3408 | 0.571403 | 1.713667 | 7782.297 | 12323.94 |
| 100 | 1 | 30 | 0.93 | 0.86 | 274.9824 | 430.1376 | 220 | 54.98239 | 210.1376 | 0.3872 | 1.479842 | 8249.472 | 12904.13 |
| 100 | 1 | 32 | 0.92 | 0.84 | 272.0256 | 420.1344 | 244.8 | 27.2256 | 175.3344 | 0.19173 | 1.234749 | 8704.819 | 13444.3 |
| 100 | 1 | 36 | 0.9 | 0.8 | 266.112 | 400.128 | 299.2 | -33.08801 | 100.928 | -0.233014 | 0.71076 | 9580.032 | 14404.61 |


## Signed-force equilibrium

The ±0.5 m/s probes show positive net force below and negative net force above each root. Near-root residual is numerical bisection/float error, not a dead zone or clamp; production integration continues on both sides.

| Speed skill | Gearing | Equilibrium before | Equilibrium after | Below v after | Net N below | Net N near | Above v after | Net N above |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 0 | 26.22015 | 28.04534 | 27.54534 | 11.52864 | -0.000031 | 28.54534 | -11.62871 |
| 0 | 0.5 | 26.51224 | 29.90096 | 29.40096 | 9.512161 | -0.000031 | 30.40096 | -9.612198 |
| 0 | 1 | 26.74401 | 32.22599 | 31.72599 | 7.873642 | 0.000031 | 32.72599 | -7.973557 |
| 50 | 0 | 29.42566 | 30.62245 | 30.12245 | 14.23085 | -0.000046 | 31.12245 | -14.33093 |
| 50 | 0.5 | 30.02662 | 33.3896 | 32.8896 | 11.48795 | 0.000031 | 33.8896 | -11.58789 |
| 50 | 1 | 30.55802 | 37.02964 | 36.52964 | 9.34552 | 0 | 37.52964 | -9.445557 |
| 100 | 0 | 32.04294 | 32.4544 | 31.9544 | 16.78423 | 0.000122 | 32.9544 | -16.88396 |
| 100 | 0.5 | 32.98248 | 36.03298 | 35.53298 | 13.29453 | -0.000061 | 36.53298 | -13.39465 |
| 100 | 1 | 33.85839 | 40.94951 | 40.44951 | 10.64078 | 0.000061 | 41.44951 | -10.74066 |


## Gearing crossover

Gearing 0 retains the stronger reference drive and wins at short distance; gearing 1 retains force longer and wins later. This is an emergent crossover from the same one-gear envelope, not a top-speed bonus.

| Distance | Low exit before | Neutral exit before | High exit before | Low exit after | Neutral exit after | High exit after | Winner after |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 10 | 16.78571 | 16.71872 | 16.6506 | 17.50147 | 17.38864 | 17.26956 | low |
| 30 | 18.15732 | 17.99413 | 17.82312 | 19.84839 | 19.65457 | 19.42488 | low |
| 60 | 19.84065 | 19.59488 | 19.32646 | 22.36707 | 22.22699 | 22.00125 | low |
| 100 | 21.59741 | 21.30854 | 20.97539 | 24.6547 | 24.71977 | 24.64681 | low |
| 300 | 26.26378 | 26.13476 | 25.88335 | 29.19909 | 30.51291 | 31.69526 | high |
| 600 | 28.51603 | 28.73876 | 28.81145 | 30.43025 | 32.75316 | 35.31264 | high |

| Diagnostic | Before | After |
| --- | --- | --- |
| Drive-force crossover speed m/s | 25.3283 | 21.52637 |
| Traversal crossover distance m from 16 m/s | 459.5515 | 100.7107 |

The selected traversal crossover is within the 100–300 m diagnostic interval. It is longer than one example straight, so short-track setup remains drive-oriented while sustained distance exposes the speed-oriented benefit; it is not far outside the useful diagnostic range.

## TurnExit recovery

Production semantics are unchanged: constraint → correction distance → drive only over legal remaining distance when the target is reached. `RunWide`, an unreached target and `Crash` receive no positive drive. Incoming and target values come from the #35 controlled production probe.

| Speed | Outcome | Incoming | Target | Correction exit | Correction m | Legal drive m | Drive entry | Drive exit before | Drive exit after | Peak after | Drive time after | Entry a before | Entry a after | Equilibrium before | Equilibrium after | Final after |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | RunWide | 17.94637 | 16.94935 | 16.94935 | 6.690722 | 21.58361 | — | — | — | — | — | — | — | — | — | 16.94935 |
| 25 | Brake | 17.94637 | 16.46144 | 16.46144 | 9.825577 | 18.44876 | 16.46144 | 17.27631 | 18.03943 | 18.03943 | 1.068606 | 0.771415 | 1.555622 | 26.51224 | 29.90096 | 18.03943 |
| 50 | Brake | 17.94637 | 16.97056 | 16.97056 | 6.552345 | 21.72199 | 16.97056 | 18.08419 | 19.1085 | 19.1085 | 1.202561 | 0.936998 | 1.897229 | 28.35396 | 31.7796 | 19.1085 |
| 75 | Brake | 17.94637 | 17.47968 | 17.47968 | 3.17942 | 25.09491 | 17.47968 | 18.92262 | 20.21687 | 20.21687 | 1.328823 | 1.09956 | 2.228941 | 30.02662 | 33.3896 | 20.21687 |
| 100 | Ok | 17.94637 | — | — | 0 | 28.27433 | 17.94637 | 19.73599 | 21.29437 | 21.29437 | 1.437176 | 1.262222 | 2.556193 | 31.56181 | 34.79316 | 21.29437 |


## Standing-start regression

Start reaction and 9–11 m/s² launch constants are untouched. Differences are collateral effects of the shared post-16 m/s fade only; prepared launch still uses production first-corner lookahead, while `pure_launch` has no preparation target.

| Scenario | Mode | Start | Reaction before/after | Movement before | Movement after | Total before | Total after | TimeTo70 before/after | SpeedAt2s before | SpeedAt2s after | Exit before | Exit after | Peak before | Peak after | A/C/P after | Equilibrium before | Equilibrium after |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| start/pure_launch | PureLaunch | 50 | 0.24/0.24 | 2.675449 | 2.683285 | 2.915449 | 2.923285 | 2.22782/2.236097 | 17.3381 | 17.32689 | 25.31828 | 24.84465 | 25.31828 | 24.84465 | 35/0/0 | 59.4369 | 46.2203 |
| start/start_skill/000 | FirstCornerPreparation | 0 | 0.28/0.28 | 2.880836 | 2.881961 | 3.160836 | 3.161961 | 2.493311/2.502545 | 15.29033 | 15.29033 | 19.0135 | 19.0135 | 20.33503 | 20.33503 | 25/0/10 | 57.53724 | 45.27259 |
| start/start_skill/025 | FirstCornerPreparation | 25 | 0.26/0.26 | 2.81953 | 2.820688 | 3.07953 | 3.080688 | 2.354524/2.363269 | 16.31051 | 16.30968 | 19.0135 | 19.0135 | 20.46248 | 20.46248 | 24/0/11 | 58.51113 | 45.76262 |
| start/start_skill/050 | FirstCornerPreparation | 50 | 0.24/0.24 | 2.764091 | 2.765249 | 3.004091 | 3.00525 | 2.22782/2.236097 | 17.3381 | 17.32689 | 19.0135 | 19.0135 | 20.58915 | 20.58915 | 23/0/12 | 59.4369 | 46.2203 |
| start/start_skill/075 | FirstCornerPreparation | 75 | 0.22/0.22 | 2.713689 | 2.714808 | 2.933689 | 2.934808 | 2.111409/2.1192 | 18.37 | 18.33617 | 19.0135 | 19.0135 | 20.71505 | 20.71505 | 22/0/13 | 60.31862 | 46.64886 |
| start/start_skill/100 | FirstCornerPreparation | 100 | 0.2/0.2 | 2.667652 | 2.668721 | 2.867652 | 2.868721 | 2.003975/2.011381 | 19.40448 | 19.33461 | 19.0135 | 19.0135 | 20.84018 | 20.84018 | 21/0/14 | 61.15988 | 47.05114 |


## Complete production heats

Four identical riders per Speed value, fixed ids/lanes, four laps, Dry, incidents 0, HoldLane, seed 320032. These are complete production heats, not isolated independent probes.

| Speed | Rider | Vmax before | Vmax after | Average before | Average after | L1 penalty before | L1 penalty after | Heat before | Heat after | Flying before | Flying after | Distance before | Distance after | RW/B/C before | RW/B/C after |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 1 | 69.169462 | 72.522249 | 15.76831 | 16.29296 | 0.892117 | 1.013365 | 71.23059 | 68.93687 | 17.6008 | 16.99654 | 1123.186 | 1123.186 | 0/0/0 | 0/0/0 |
| 0 | 2 | 72.033742 | 75.060599 | 16.54974 | 17.06497 | 0.911556 | 1.03307 | 72.42316 | 70.23653 | 17.89432 | 17.31675 | 1198.584 | 1198.584 | 0/0/0 | 0/0/0 |
| 0 | 3 | 74.336826 | 77.345137 | 17.29553 | 17.80242 | 0.939983 | 1.062038 | 73.65961 | 71.56231 | 18.19664 | 17.64129 | 1273.982 | 1273.982 | 0/0/0 | 0/0/0 |
| 0 | 4 | 76.929785 | 79.481758 | 18.01605 | 18.51643 | 0.979252 | 1.102137 | 74.89879 | 72.87474 | 18.49694 | 17.95972 | 1349.38 | 1349.38 | 0/0/0 | 0/0/0 |
| 25 | 1 | 70.528677 | 76.024876 | 16.35422 | 16.91341 | 0.963123 | 1.060081 | 68.67865 | 66.40799 | 16.94451 | 16.35205 | 1123.186 | 1123.186 | 0/0/0 | 0/0/0 |
| 25 | 2 | 73.041991 | 78.71139 | 17.15868 | 17.70824 | 0.983965 | 1.082811 | 69.85295 | 67.68512 | 17.23314 | 16.66594 | 1198.584 | 1198.584 | 0/0/0 | 0/0/0 |
| 25 | 3 | 75.471659 | 81.051684 | 17.92968 | 18.47158 | 1.013643 | 1.114601 | 71.0544 | 68.96985 | 17.52642 | 16.97952 | 1273.982 | 1273.982 | 0/0/0 | 0/0/0 |
| 25 | 4 | 78.256961 | 83.416065 | 18.67528 | 19.21147 | 1.054733 | 1.157862 | 72.2549 | 70.23826 | 17.8166 | 17.28613 | 1349.38 | 1349.38 | 0/0/0 | 0/0/0 |
| 50 | 1 | 72.415524 | 79.155835 | 16.91381 | 17.49112 | 1.019554 | 1.093054 | 66.40646 | 64.21463 | 16.36186 | 15.79496 | 1123.186 | 1123.186 | 0/0/0 | 0/0/0 |
| 50 | 2 | 75.151902 | 81.801542 | 17.74297 | 18.31076 | 1.041573 | 1.118332 | 67.55262 | 65.45789 | 16.64322 | 16.09979 | 1198.584 | 1198.584 | 0/0/0 | 0/0/0 |
| 50 | 3 | 77.713206 | 84.407334 | 18.53945 | 19.10008 | 1.073324 | 1.153337 | 68.71738 | 66.70036 | 16.92678 | 16.40201 | 1273.982 | 1273.982 | 0/0/0 | 0/0/0 |
| 50 | 4 | 80.118608 | 86.821168 | 19.30991 | 19.86534 | 1.116371 | 1.199566 | 69.88019 | 67.92635 | 17.20704 | 16.69727 | 1349.38 | 1349.38 | 0/0/0 | 0/0/0 |
| 75 | 1 | 75.130245 | 82.393362 | 17.45259 | 18.07601 | 1.065224 | 1.163013 | 64.3564 | 62.13683 | 15.83751 | 15.25663 | 1123.186 | 1123.186 | 0/0/0 | 0/3/0 |
| 75 | 2 | 78.007434 | 84.891069 | 18.30772 | 18.89472 | 1.089392 | 1.159735 | 65.46879 | 63.43487 | 16.10987 | 15.58241 | 1198.584 | 1198.584 | 0/0/0 | 0/0/0 |
| 75 | 3 | 80.647305 | 87.406739 | 19.12947 | 19.69832 | 1.123304 | 1.184086 | 66.59791 | 64.67465 | 16.38401 | 15.88747 | 1273.982 | 1273.982 | 0/0/0 | 0/0/0 |
| 75 | 4 | 83.138022 | 89.959783 | 19.92462 | 20.48851 | 1.168282 | 1.233109 | 67.72427 | 65.86034 | 16.65466 | 16.17193 | 1349.38 | 1349.38 | 0/0/0 | 0/0/0 |
| 100 | 1 | 77.713447 | 85.942255 | 17.97521 | 18.69734 | 1.103643 | 1.285928 | 62.48526 | 60.07195 | 15.35974 | 14.70906 | 1123.186 | 1123.186 | 0/0/0 | 0/3/0 |
| 100 | 2 | 80.650848 | 88.493108 | 18.85628 | 19.53207 | 1.130249 | 1.274578 | 63.56417 | 61.36492 | 15.62313 | 15.0356 | 1198.584 | 1198.584 | 0/0/0 | 0/3/0 |
| 100 | 3 | 83.425644 | 90.909462 | 19.70324 | 20.33784 | 1.166464 | 1.277918 | 64.65852 | 62.64099 | 15.88798 | 15.3542 | 1273.982 | 1273.982 | 0/0/0 | 0/3/0 |
| 100 | 4 | 85.985973 | 93.203146 | 20.52289 | 21.12052 | 1.213236 | 1.295761 | 65.74999 | 63.88952 | 16.14944 | 15.66228 | 1349.38 | 1349.38 | 0/0/0 | 0/3/0 |

| Speed | Vmax P50 before | Vmax P50 after | Average P50 before | Average P50 after | L1 penalty P50 before | L1 penalty P50 after | Heat P50 before | Heat P50 after | Flying P50 before | Flying P50 after |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 73.185284 | 76.202868 | 16.922634 | 17.433693 | 0.92577 | 1.047554 | 73.041386 | 70.899422 | 18.045479 | 17.479021 |
| 25 | 74.256825 | 79.881537 | 17.544176 | 18.08991 | 0.998804 | 1.098706 | 70.453674 | 68.327484 | 17.37978 | 16.822731 |
| 50 | 76.432554 | 83.104438 | 18.14121 | 18.705422 | 1.057448 | 1.135835 | 68.134998 | 66.079128 | 16.784998 | 16.2509 |
| 75 | 79.327369 | 86.148904 | 18.718592 | 19.296522 | 1.106348 | 1.17355 | 66.033348 | 64.05476 | 16.246943 | 15.734943 |
| 100 | 82.038246 | 89.701285 | 19.27976 | 19.934956 | 1.148356 | 1.281923 | 64.111349 | 62.002954 | 15.755558 | 15.194901 |


## Corner regression boundary

These values are black-box observations of unchanged `SegmentPhysics` transitions and the shared corner-correction capability. Exact before/after equality confirms that this PR does not calibrate corner physics.

| Scenario | MaxSafe before/after | FirstBrake before/after | FirstRunWide before/after | FirstCrash before/after | Retention before/after | Capability before/after | Identical |
| --- | --- | --- | --- | --- | --- | --- | --- |
| baseline | 16.97056/16.97056 | 17.22512/17.22512 | 18.66762/18.66762 | 21.38291/21.38291 | 0.5/0.5 | 2.6/2.6 | true |
| speed/000 | 15.95233/15.95233 | 16.19162/16.19162 | 17.54756/17.54756 | 20.09994/20.09994 | 0.5/0.5 | 2.6/2.6 | true |
| speed/050 | 16.97056/16.97056 | 17.22512/17.22512 | 18.66762/18.66762 | 21.38291/21.38291 | 0.5/0.5 | 2.6/2.6 | true |
| speed/100 | 17.98879/17.98879 | 18.25863/18.25863 | 19.78767/19.78767 | 22.66588/22.66588 | 0.5/0.5 | 2.6/2.6 | true |
| slide/000 | 16.46144/16.46144 | 16.70837/16.70837 | 17.44913/17.44913 | 19.42451/19.42451 | 0.35/0.35 | 2/2 | true |
| slide/050 | 16.97056/16.97056 | 17.22512/17.22512 | 18.66762/18.66762 | 21.38291/21.38291 | 0.5/0.5 | 2.6/2.6 | true |
| slide/100 | 17.47968/17.47968 | 17.74188/17.74188 | 19.92684/19.92684 | 23.42277/23.42277 | 0.65/0.65 | 3.2/3.2 | true |


## Within-heat spreads

Spreads are diagnostics, not the tuning objective. The identical-rider rows combine fixed-line geometry with normal production evolution; `[25,50,50,75]` rows retain the #35 cautions and are not unconfounded skill-only estimates.

| Scenario | Heat spread before | Heat spread after | Vmax spread before | Vmax spread after | L1 spread before | L1 spread after | Average spread before | Average spread after |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| full_heat/baseline | 3.473732 | 3.711723 | 7.703085 | 7.665333 | 0.941994 | 1.008821 | 2.396105 | 2.374222 |
| full_heat/speed/000 | 3.668198 | 3.937874 | 7.760323 | 6.959509 | 0.983284 | 1.051945 | 2.24774 | 2.223469 |
| full_heat/speed/025 | 3.576248 | 3.830269 | 7.728284 | 7.39119 | 0.963696 | 1.031866 | 2.321054 | 2.298056 |
| full_heat/speed/050 | 3.473732 | 3.711723 | 7.703085 | 7.665333 | 0.941994 | 1.008821 | 2.396105 | 2.374222 |
| full_heat/speed/075 | 3.367874 | 3.723503 | 8.007777 | 7.566422 | 0.920204 | 0.985397 | 2.472027 | 2.412504 |
| full_heat/speed/100 | 3.264729 | 3.817574 | 8.272527 | 7.260892 | 0.899292 | 0.963054 | 2.547684 | 2.423182 |
| full_heat/within_heat/slide_control | 1.586838 | 1.865967 | 9.753751 | 9.941975 | 0.493156 | 0.561956 | 2.896563 | 2.896717 |
| full_heat/within_heat/speed | 1.164757 | 1.24247 | 12.609345 | 13.934908 | 0.315317 | 0.337221 | 3.570395 | 3.575096 |
| full_heat/within_heat/start | 3.326218 | 3.564194 | 7.703085 | 7.665333 | 0.794485 | 0.861294 | 2.435284 | 2.415895 |


## Real PGE context and interpretation

The versioned PGEE distributions are context envelopes, never caps, equalities, real-rider mappings or evidence that Skill 50 is an average PGE rider.

| Metric | Unit | P10 | P50 | P90 | Use |
| --- | --- | --- | --- | --- | --- |
| pge_clean_vmax | km/h | 109.7 | 114.8 | 119.4 | secondary diagnostic only |
| pge_clean_average_speed | m/s | 22.023974 | 22.980452 | 23.968246 | context; geometry-dependent residual remains |
| pge_clean_l1_penalty | s | 1.82 | 2.05 | 2.32 | context; start constants frozen |
| pge_four_rider_heat_time_spread | s | 0.9542 | 1.5285 | 2.2734 | spread diagnostic only |
| pge_four_rider_vmax_spread | km/h | 1.8 | 4.5 | 8.7 | spread diagnostic only |
| pge_four_rider_l1_spread | s | 0.32 | 0.53 | 0.76 | spread diagnostic only |
| pge_four_rider_average_speed_spread | m/s | 0.431415 | 0.927094 | 1.658977 | spread diagnostic only |

Full-heat Vmax is a secondary diagnostic only. The selected constants improve finite-distance Straight and TurnExit response without forcing Speed 100 to reach the real P10 Vmax envelope. Residual system-level speed deficit may belong to Phase 2 corner-envelope calibration.

Average-speed medians remain below the real comparable envelope on the standing-start example. Likely Phase 2 corner-envelope calibration gap. Absolute lap/heat/flying times remain `ContextOnlyUntilTrackGeometry`; this PR does not fit the example track to every real venue.

Standing-start reaction and launch constants remain unchanged; corner transition thresholds, overspeed retention and correction capability remain byte/float-identical. No corner constant, surface model, geometry, decisions, contact, RNG, telemetry schema, dataset or historical report was modified.
