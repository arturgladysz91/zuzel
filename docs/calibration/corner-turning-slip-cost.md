# Corner turning/slip energy cost experiment (#48)

Base merged main: `99f3afd08c8e6892a8b24eb785b5be6cbf116d38`. Analysis-only; production race equations and #47 baseline are frozen.

## Model fixed before sweep

Let m = 142 kg (the existing provisional rider plus motorcycle mass), v be path speed in m/s, and κ be signed local path curvature in 1/m. Lateral demand is a_lat = v²|κ| in m/s² and lateral force is F_lat = m v²|κ| in N. With one dimensionless experimental coefficient c, requested effective slip drag is F_slip = c F_lat. Requested dissipated power is P_slip = F_slip v = c m v³|κ| in W. The coefficient represents an effective longitudinal-to-lateral force ratio, approximately tan(effective slip angle) for small angles; it is not a measured tyre slip angle.

The #47 availability factor A(p) and speed-dependent drive force F_drive(v) give available forward force A F_drive. Applied slip loss is min(F_slip, A F_drive), so useful drive force is max(0, A F_drive − F_slip). The original #47 net acceleration A(F_drive − F_resistance)/m is reduced by applied slip loss/m only during a positive-drive step. Existing explicit correction steps retain their #47 braking law and apply no additional slip debit. At c = 0 the original midpoint integrator is called directly. Work is integrated as Σ F_loss Δs over accepted corner drive steps; peak power is the maximum at integration midpoints and reported trajectory sample points.

Units: kg·(m/s)²·(1/m) = N, and N·m/s = W; N·m = J. Loss tends to zero as κ → 0 or v → 0. It grows with |κ| and as v² in force / v³ in power until useful drive saturates at zero. The clamp prevents the surrogate from manufacturing negative propulsion, and it is continuous at the saturation boundary. No line ID, lane number, preferred offset, or time penalty enters this equation. The coefficient values were set as a logarithmic sensitivity sweep, not fitted to an optimizer winner.

## Zero-cost regression

Winner `Flat-0` / `FCT-B843ADDB373E` has eleven zero offsets, exactly the ConstantInner geometry; sector 6.676829 s; free minus ConstantInner 0.000000 s; objective convergence YES; geometry convergence YES. The zero scenario directly reuses the merged #47 result, including all validity outcomes and search diagnostics.

## Sensitivity sweep

c is dimensionless. Δ values are relative to c = 0. All times are seconds, length m, energy J, peak power W, speed m/s, acceleration m/s², curvature 1/m. Repeated lap includes two periodic corner/straight sectors.

| c | winner / controls (m) | nonconstant | path m | corner s | exit m/s | straight s | sector s | Δsector s | min radius m @p | max curvature | peak a_lat | loss J | peak W | max v | avg v | lap s | Δlap s | objective / geometry | guardrails V/A/L |
|---:|---|:---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|:---:|:---:|
| 0.000 | `Flat-0` `FCT-B843ADDB373E`<br>0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00 | NO | 97.390 | 4.201727 | 23.405930 | 2.475102 | 6.676829 | 0.000000 | 31.000 @ 0.000 | 0.032258 | 22.606 | 0.0 | 0.0 | 26.515 | 23.873 | 13.353217 | 0.000000 | YES / YES | NO/NO/NO |
| 0.001 | `Flat-0` `FCT-B843ADDB373E`<br>0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00,0.00 | NO | 97.390 | 4.203175 | 23.376656 | 2.477420 | 6.680595 | 0.003766 | 31.000 @ 0.000 | 0.032258 | 22.606 | 108.4 | 58.5 | 26.497 | 23.859 | 13.360945 | 0.007728 | YES / YES | NO/NO/NO |
| 0.005 | `Low-phase-neutral` `FCT-AE7832278759`<br>0.52,0.62,0.60,0.49,0.31,0.15,0.05,0.06,0.18,0.35,0.58 | YES | 98.455 | 4.222685 | 23.507759 | 2.467068 | 6.689753 | 0.012923 | 30.295 @ 0.100 | 0.033008 | 22.400 | 519.2 | 292.1 | 26.579 | 23.986 | 13.378957 | 0.025740 | YES / YES | NO/NO/NO |
| 0.020 | `Low-phase-neutral` `FCT-B9250D7462DD`<br>0.52,0.62,0.60,0.49,0.31,0.15,0.05,0.06,0.18,0.35,0.71 | YES | 98.474 | 4.239342 | 23.150276 | 2.495480 | 6.734822 | 0.057993 | 30.295 @ 0.100 | 0.033008 | 22.400 | 1939.3 | 1112.1 | 26.356 | 23.826 | 13.470627 | 0.117410 | YES / YES | NO/NO/NO |
| 0.080 | `Halton-041` `FCT-C861DD3E34D6`<br>0.02,0.05,0.04,0.31,0.25,0.09,0.04,0.09,0.18,0.21,0.06 | YES | 97.815 | 4.272137 | 21.872240 | 2.601579 | 6.873715 | 0.196886 | 25.995 @ 0.300 | 0.038469 | 22.612 | 6387.8 | 4134.3 | 25.574 | 23.041 | 13.872164 | 0.518947 | YES / YES | NO/NO/NO |
| 0.320 | `Halton-041` `FCT-7298E28EE8FB`<br>0.02,0.05,0.04,0.31,0.25,0.09,0.04,0.09,0.18,0.28,0.06 | YES | 97.839 | 4.307969 | 20.669649 | 2.708346 | 7.016315 | 0.339486 | 25.993 @ 0.300 | 0.038471 | 22.612 | 9502.8 | 7048.4 | 24.864 | 22.538 | 14.184100 | 0.830883 | YES / YES | NO/NO/NO |
| 1.280 | `Halton-041` `FCT-7298E28EE8FB`<br>0.02,0.05,0.04,0.31,0.25,0.09,0.04,0.09,0.18,0.28,0.06 | YES | 97.839 | 4.307969 | 20.669649 | 2.708346 | 7.016315 | 0.339486 | 25.993 @ 0.300 | 0.038471 | 22.612 | 9502.8 | 7048.4 | 24.864 | 22.538 | 14.184100 | 0.830883 | YES / YES | NO/NO/NO |

### Changes from the c = 0 winning geometry

All deltas use the zero-coefficient winner as reference, even when the optimizer changes controls. The `same controls at c=0` column isolates the new loss effect from changing geometry.

| c | Δpath m | Δcorner s | Δexit m/s | Δstraight s | Δmin radius m | Δmax curvature | Δpeak a_lat | Δenergy J | Δpeak W | Δmax v | Δavg v | same controls at c=0: Δsector s | winner advantage over inner s | correction distance winner / inner m | correction time winner / inner s | min lateral headroom m |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.000 | 0.000 | 0.000000 | 0.000000 | 0.000000 | 0.000 | 0.000000 | 0.000 | 0.0 | 0.0 | 0.000 | 0.000 | 0.000000 | 0.000000 | 45.095 / 45.095 | 1.876 / 1.876 | 0.019 |
| 0.001 | 0.000 | 0.001448 | -0.029274 | 0.002318 | 0.000 | 0.000000 | 0.000 | 108.4 | 58.5 | -0.018 | -0.014 | 0.000000 | 0.000000 | 45.095 / 45.095 | 1.876 / 1.876 | 0.019 |
| 0.005 | 1.065 | 0.020958 | 0.101830 | -0.008035 | -0.705 | 0.000750 | -0.206 | 519.2 | 292.1 | 0.064 | 0.113 | 0.006234 | 0.005227 | 42.369 / 45.095 | 1.751 / 1.876 | 0.000 |
| 0.020 | 1.084 | 0.037615 | -0.255653 | 0.020378 | -0.705 | 0.000750 | -0.206 | 1939.3 | 1112.1 | -0.159 | -0.047 | 0.025354 | 0.009692 | 42.336 / 45.095 | 1.750 / 1.876 | 0.000 |
| 0.080 | 0.425 | 0.070410 | -1.533689 | 0.126476 | -5.005 | 0.006211 | 0.006 | 6387.8 | 4134.3 | -0.941 | -0.832 | 0.109153 | 0.033034 | 41.606 / 45.095 | 1.716 / 1.876 | 0.000 |
| 0.320 | 0.449 | 0.106242 | -2.736280 | 0.233244 | -5.007 | 0.006213 | 0.006 | 9502.8 | 7048.4 | -1.651 | -1.335 | 0.173681 | 0.056433 | 42.431 / 45.095 | 1.754 / 1.876 | 0.000 |
| 1.280 | 0.449 | 0.106242 | -2.736280 | 0.233244 | -5.007 | 0.006213 | 0.006 | 9502.8 | 7048.4 | -1.651 | -1.335 | 0.173681 | 0.056433 | 42.431 / 45.095 | 1.754 / 1.876 | 0.000 |

The descriptive guard band is ±15% around Motoarena maximum speed 111 km/h, average speed 22.38 m/s, and flying lap 14.71 s. V/A/L mark where the measured maximum-speed, average-speed, or lap-time band is exceeded. These are diagnostic bounds, never fitting targets. Repeated-lap convergence must be checked before interpreting them.

### Constant-line controls and free winner

| c | line | sector s | Δ vs zero inner s | slip J | exit m/s | valid |
|---:|---|---:|---:|---:|---:|:---:|
| 0.000 | ConstantInner | 6.676829 | 0.000000 | 0.0 | 23.405930 | YES |
| 0.000 | ConstantL1 | 6.863337 | 0.186508 | 0.0 | 24.529385 | YES |
| 0.000 | ConstantL2 | 7.079330 | 0.402501 | 0.0 | 25.568707 | YES |
| 0.000 | ConstantL3 | 7.320224 | 0.643394 | 0.0 | 26.535183 | YES |
| 0.000 | ConstantOuter | 7.582354 | 0.905525 | 0.0 | 27.437767 | YES |
| 0.000 | Flat-0 | 6.676829 | 0.000000 | 0.0 | 23.405930 | YES |
| 0.001 | ConstantInner | 6.680595 | 0.003766 | 108.4 | 23.376656 | YES |
| 0.001 | ConstantL1 | 6.867154 | 0.190325 | 120.1 | 24.498755 | YES |
| 0.001 | ConstantL2 | 7.083202 | 0.406373 | 131.7 | 25.536793 | YES |
| 0.001 | ConstantL3 | 7.324151 | 0.647322 | 143.3 | 26.502054 | YES |
| 0.001 | ConstantOuter | 7.586339 | 0.909510 | 154.9 | 27.403481 | YES |
| 0.001 | Flat-0 | 6.680595 | 0.003766 | 108.4 | 23.376656 | YES |
| 0.005 | ConstantInner | 6.694980 | 0.018150 | 525.0 | 23.263609 | YES |
| 0.005 | ConstantL1 | 6.881732 | 0.204903 | 581.5 | 24.380451 | YES |
| 0.005 | ConstantL2 | 7.098001 | 0.421172 | 638.4 | 25.413404 | YES |
| 0.005 | ConstantL3 | 7.339114 | 0.662285 | 693.4 | 26.374203 | YES |
| 0.005 | ConstantOuter | 7.601492 | 0.924663 | 748.6 | 27.271307 | YES |
| 0.005 | Low-phase-neutral | 6.689753 | 0.012923 | 519.2 | 23.507759 | YES |
| 0.020 | ConstantInner | 6.744514 | 0.067685 | 1956.4 | 22.869616 | YES |
| 0.020 | ConstantL1 | 6.931909 | 0.255080 | 2166.8 | 23.968035 | YES |
| 0.020 | ConstantL2 | 7.148710 | 0.471881 | 2373.6 | 24.984354 | YES |
| 0.020 | ConstantL3 | 7.390354 | 0.713524 | 2577.0 | 25.929602 | YES |
| 0.020 | ConstantOuter | 7.653183 | 0.976354 | 2776.6 | 26.812561 | YES |
| 0.020 | Low-phase-neutral | 6.734822 | 0.057993 | 1939.3 | 23.150276 | YES |
| 0.080 | ConstantInner | 6.906750 | 0.229920 | 6471.9 | 21.568813 | YES |
| 0.080 | ConstantL1 | 7.095107 | 0.418278 | 7143.3 | 22.611717 | YES |
| 0.080 | ConstantL2 | 7.312596 | 0.635767 | 7797.1 | 23.578079 | YES |
| 0.080 | ConstantL3 | 7.554760 | 0.877931 | 8434.8 | 24.477970 | YES |
| 0.080 | ConstantOuter | 7.817970 | 1.141141 | 9055.9 | 25.319746 | YES |
| 0.080 | Halton-041 | 6.873715 | 0.196886 | 6387.8 | 21.872240 | YES |
| 0.320 | ConstantInner | 7.072748 | 0.395919 | 10838.9 | 20.200621 | YES |
| 0.320 | ConstantL1 | 7.254683 | 0.577853 | 11767.3 | 21.239578 | YES |
| 0.320 | ConstantL2 | 7.465931 | 0.789102 | 12644.9 | 22.207712 | YES |
| 0.320 | ConstantL3 | 7.702019 | 1.025189 | 13474.9 | 23.114344 | YES |
| 0.320 | ConstantOuter | 7.959368 | 1.282539 | 14260.1 | 23.966724 | YES |
| 0.320 | Halton-041 | 7.016315 | 0.339486 | 9502.8 | 20.669649 | YES |
| 1.280 | ConstantInner | 7.072748 | 0.395919 | 10838.9 | 20.200621 | YES |
| 1.280 | ConstantL1 | 7.254683 | 0.577853 | 11767.3 | 21.239578 | YES |
| 1.280 | ConstantL2 | 7.465931 | 0.789102 | 12644.9 | 22.207712 | YES |
| 1.280 | ConstantL3 | 7.702019 | 1.025189 | 13474.9 | 23.114344 | YES |
| 1.280 | ConstantOuter | 7.959368 | 1.282539 | 14260.1 | 23.966724 | YES |
| 1.280 | Halton-041 | 7.016315 | 0.339486 | 9502.8 | 20.669649 | YES |

### Search closure and validity

| c | starts | refined | top-3 closed | independent near starts | single residual s | pair residual s | objective | geometry | valid / evaluated | repeat converged |
|---:|---:|---:|:---:|---:|---:|---:|:---:|:---:|---:|:---:|
| 0.000 | 106 | 48 | YES | 5 | 0.000000 | 0.000000 | YES | YES | 5754 / 9196 | YES |
| 0.001 | 106 | 48 | YES | 5 | 0.000000 | 0.000000 | YES | YES | 5736 / 9179 | YES |
| 0.005 | 106 | 48 | YES | 5 | 0.000000 | 0.000000 | YES | YES | 5783 / 9192 | YES |
| 0.020 | 106 | 48 | YES | 5 | 0.000000 | 0.000000 | YES | YES | 5884 / 9347 | YES |
| 0.080 | 106 | 48 | YES | 4 | 0.000000 | 0.000000 | YES | YES | 6272 / 10020 | YES |
| 0.320 | 106 | 48 | YES | 3 | 0.001109 | 0.000000 | YES | YES | 6230 / 9694 | YES |
| 1.280 | 106 | 48 | YES | 3 | 0.001109 | 0.000000 | YES | YES | 6230 / 9694 | YES |

## Trajectory samples

Each distinct converged winner is shown once. Drive remaining is useful forward force after the slip debit, in N; it is zero during explicit correction. Power is applied slip loss in W.

### c = 0.000: `FCT-B843ADDB373E`

| p | lateral offset m | lateral position (lane units) | radius m | speed m/s | a_lat m/s² | slip W | drive remaining N |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.00 | 0.000 | 0.000 | 31.000 | 26.472 | 22.606 | 0.0 | 0.0 |
| 0.10 | 0.000 | 0.000 | 31.000 | 25.862 | 21.576 | 0.0 | 0.0 |
| 0.20 | 0.000 | 0.000 | 31.000 | 24.864 | 19.942 | 0.0 | 0.0 |
| 0.30 | 0.000 | 0.000 | 31.000 | 23.824 | 18.309 | 0.0 | 0.0 |
| 0.40 | 0.000 | 0.000 | 31.000 | 22.736 | 16.675 | 0.0 | 0.0 |
| 0.50 | 0.000 | 0.000 | 31.000 | 21.594 | 15.042 | 0.0 | 0.0 |
| 0.60 | 0.000 | 0.000 | 31.000 | 21.641 | 15.108 | 0.0 | 70.8 |
| 0.70 | 0.000 | 0.000 | 31.000 | 21.899 | 15.470 | 0.0 | 210.9 |
| 0.80 | 0.000 | 0.000 | 31.000 | 22.377 | 16.153 | 0.0 | 312.4 |
| 0.90 | 0.000 | 0.000 | 31.000 | 22.910 | 16.931 | 0.0 | 316.9 |
| 1.00 | 0.000 | 0.000 | 31.000 | 23.406 | 17.672 | 0.0 | 312.7 |

### c = 0.005: `FCT-AE7832278759`

| p | lateral offset m | lateral position (lane units) | radius m | speed m/s | a_lat m/s² | slip W | drive remaining N |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.00 | 0.522 | 0.143 | 31.520 | 26.472 | 22.233 | 0.0 | 0.0 |
| 0.10 | 0.617 | 0.169 | 30.295 | 25.714 | 21.826 | 0.0 | 0.0 |
| 0.20 | 0.605 | 0.166 | 30.576 | 24.775 | 20.074 | 0.0 | 0.0 |
| 0.30 | 0.488 | 0.134 | 30.846 | 23.797 | 18.359 | 0.0 | 0.0 |
| 0.40 | 0.313 | 0.086 | 31.397 | 22.875 | 16.667 | 0.0 | 0.0 |
| 0.50 | 0.146 | 0.040 | 31.943 | 21.920 | 15.042 | 0.0 | 0.0 |
| 0.60 | 0.051 | 0.014 | 32.251 | 21.940 | 14.925 | 232.5 | 59.6 |
| 0.70 | 0.063 | 0.017 | 32.285 | 22.155 | 15.204 | 239.2 | 198.7 |
| 0.80 | 0.180 | 0.049 | 31.602 | 22.559 | 16.104 | 257.9 | 299.4 |
| 0.90 | 0.355 | 0.097 | 32.088 | 23.049 | 16.557 | 271.0 | 303.9 |
| 1.00 | 0.585 | 0.160 | 31.575 | 23.508 | 17.502 | 292.1 | 299.4 |

### c = 0.020: `FCT-B9250D7462DD`

| p | lateral offset m | lateral position (lane units) | radius m | speed m/s | a_lat m/s² | slip W | drive remaining N |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.00 | 0.522 | 0.143 | 31.520 | 26.472 | 22.233 | 0.0 | 0.0 |
| 0.10 | 0.617 | 0.169 | 30.295 | 25.714 | 21.826 | 0.0 | 0.0 |
| 0.20 | 0.605 | 0.166 | 30.576 | 24.775 | 20.074 | 0.0 | 0.0 |
| 0.30 | 0.488 | 0.134 | 30.846 | 23.797 | 18.359 | 0.0 | 0.0 |
| 0.40 | 0.313 | 0.086 | 31.394 | 22.874 | 16.667 | 0.0 | 0.0 |
| 0.50 | 0.146 | 0.040 | 31.954 | 21.924 | 15.042 | 0.0 | 0.0 |
| 0.60 | 0.051 | 0.014 | 32.209 | 21.901 | 14.892 | 926.3 | 28.0 |
| 0.70 | 0.063 | 0.017 | 32.444 | 22.021 | 14.947 | 934.8 | 167.7 |
| 0.80 | 0.180 | 0.049 | 31.052 | 22.361 | 16.102 | 1022.6 | 266.8 |
| 0.90 | 0.355 | 0.097 | 34.364 | 22.769 | 15.086 | 975.5 | 275.2 |
| 1.00 | 0.710 | 0.194 | 31.684 | 23.150 | 16.915 | 1112.1 | 266.8 |

### c = 0.080: `FCT-C861DD3E34D6`

| p | lateral offset m | lateral position (lane units) | radius m | speed m/s | a_lat m/s² | slip W | drive remaining N |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.00 | 0.018 | 0.005 | 31.017 | 26.472 | 22.593 | 0.0 | 0.0 |
| 0.10 | 0.054 | 0.015 | 28.781 | 25.496 | 22.587 | 0.0 | 0.0 |
| 0.20 | 0.037 | 0.010 | 39.348 | 24.482 | 15.233 | 0.0 | 0.0 |
| 0.30 | 0.306 | 0.084 | 25.983 | 23.419 | 21.108 | 0.0 | 0.0 |
| 0.40 | 0.253 | 0.069 | 30.782 | 22.535 | 16.497 | 0.0 | 0.0 |
| 0.50 | 0.089 | 0.024 | 32.754 | 22.196 | 15.042 | 0.0 | 0.0 |
| 0.60 | 0.039 | 0.011 | 32.137 | 22.030 | 15.102 | 0.0 | 0.0 |
| 0.70 | 0.092 | 0.025 | 31.477 | 21.868 | 15.193 | 3774.3 | 38.4 |
| 0.80 | 0.182 | 0.050 | 30.901 | 21.822 | 15.410 | 3820.0 | 141.9 |
| 0.90 | 0.214 | 0.059 | 28.650 | 21.846 | 16.659 | 4134.3 | 136.6 |
| 1.00 | 0.057 | 0.016 | 31.050 | 21.872 | 15.407 | 3828.2 | 150.6 |

### c = 0.320: `FCT-7298E28EE8FB`

| p | lateral offset m | lateral position (lane units) | radius m | speed m/s | a_lat m/s² | slip W | drive remaining N |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.00 | 0.018 | 0.005 | 31.017 | 26.472 | 22.593 | 0.0 | 0.0 |
| 0.10 | 0.054 | 0.015 | 28.781 | 25.496 | 22.587 | 0.0 | 0.0 |
| 0.20 | 0.037 | 0.010 | 39.349 | 24.482 | 15.232 | 0.0 | 0.0 |
| 0.30 | 0.306 | 0.084 | 25.981 | 23.419 | 21.109 | 0.0 | 0.0 |
| 0.40 | 0.253 | 0.069 | 30.790 | 22.536 | 16.495 | 0.0 | 0.0 |
| 0.50 | 0.089 | 0.024 | 32.719 | 22.184 | 15.042 | 0.0 | 0.0 |
| 0.60 | 0.039 | 0.011 | 32.264 | 22.068 | 15.094 | 1544.3 | 0.0 |
| 0.70 | 0.092 | 0.025 | 31.034 | 21.868 | 15.409 | 4614.8 | 0.0 |
| 0.80 | 0.182 | 0.050 | 32.595 | 21.513 | 14.199 | 6872.5 | 0.0 |
| 0.90 | 0.276 | 0.076 | 26.890 | 21.090 | 16.541 | 7006.7 | 0.0 |
| 1.00 | 0.057 | 0.016 | 31.042 | 20.670 | 13.763 | 6940.4 | 0.0 |

## Interpretation

First converged winner shape change: 0.005. First converged coefficient where free beats ConstantInner: 0.005. A discrete sampled interval, rather than an interpolated physical threshold, is reported; the prior tested coefficient is the lower bound.

The first optimizer crossover is bracketed by c = 0.001 and 0.005. At the upper endpoint the gain over ConstantInner is 0.005227 s, the new path is 1.065 m longer, and its applied slip work is -5.8 J different. Its maximum curvature is 0.033008 1/m versus inner 0.032258 1/m; this is a redistribution of curvature and drive-phase timing, not uniformly broader local radius. Correction distance is 42.369 m versus inner 45.095 m; the higher correction-step count reflects a denser variable-curvature integration mesh, not greater corrected travel.

Fixed-shape probe across the first crossover: the geometry is held at the upper-endpoint winning controls. This checks that a winner switch is not caused by a jump in the objective or in the correction branch. It does not replace a full free search at intermediate c.

| c | fixed new shape − inner sector s | correction distance new / inner m | correction steps new / inner | new shape loss J | inner loss J |
|---:|---:|---:|---:|---:|---:|
| 0.000 | 0.006234 | 42.369 / 45.095 | 928 / 47 | 0.0 | 0.0 |
| 0.001 | 0.003164 | 42.372 / 45.095 | 908 / 47 | 103.4 | 108.4 |
| 0.002 | 0.000490 | 42.370 / 45.095 | 898 / 47 | 207.2 | 214.3 |
| 0.003 | -0.001458 | 42.368 / 45.095 | 891 / 47 | 310.8 | 319.0 |
| 0.004 | -0.003347 | 42.369 / 45.095 | 883 / 47 | 414.8 | 422.2 |
| 0.005 | -0.005227 | 42.369 / 45.095 | 876 / 47 | 519.2 | 525.0 |
| 0.006 | -0.006993 | 42.369 / 45.095 | 869 / 47 | 624.3 | 625.5 |

Case A: a converged non-inner optimum appears before the descriptive global guardrails break.

1. **Does the optimum move?** Yes within this surrogate: the first converged non-inner winner occurs at c = 0.005.
2. **Coefficient range?** The full searches bracket the first switch between 0.001 and 0.005; the fixed-shape probe crosses between 0.002 and 0.003, without claiming a global optimum at those intermediate values.
3. **Physical trade-off?** Slip force removes useful drive work according to speed and curvature, while the selected geometry changes the turning-demand envelope and exit speed. It pays more path length for a small sector gain.
4. **Smooth and feasible?** The #47 spline, turning-demand, lateral-execution, and straight-closure gates pass. The new line has near-zero remaining lateral headroom, so it is at the modeled execution boundary; real-bike plausibility remains unvalidated.
5. **Before global guardrails break?** Yes: the V/A/L bands are unbroken at the first switch. These broad bounds do not establish a physically correct coefficient.
6. **Search converged?** Objective and geometry convergence are YES at every sweep point, with three or more independent near starts and locally closed top-three results. The fixed-shape objective changes smoothly through the switch.
7. **Future production experiment?** Only after a loss law valid through correction and measured force or speed traces support the coefficient. This draft makes no production change.
8. **If unsupported, what next?** Test one coupled rear-tyre longitudinal/lateral force envelope.

At high c the useful-drive clamp saturates: c = 0.320 and 1.280 produce identical evaluated results. If c is interpreted literally as tan(effective slip angle), 1.280 implies about 52°, beyond the small-angle surrogate's defensible range. The upper-sweep wavy geometry is a possible #47 smoothness-gate limitation, not validated racing technique. No production calibration or promotion follows this result.
