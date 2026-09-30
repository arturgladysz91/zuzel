# Corner turning/slip energy cost experiment (#48)

Base merged main: 99f3afd08c8e6892a8b24eb785b5be6cbf116d38. Analysis-only; production physics, geometry, #47 envelope/search/lateral rules and historical artifacts are frozen.

## Continuous dissipative model

The frozen law is F_turn = c m v²|κ|, with m = 142 kg, speed v in m/s, signed path curvature κ in 1/m and dimensionless coefficient c. Power is F_turn v (W); work is Σ F_turn Δs (J). Force vanishes only at zero coefficient, speed or curvature, not at a controller phase boundary. The coefficients are a sensitivity sweep, not a fitted tyre model.

For c > 0 every interval first integrates the #47 passive drive/resistance acceleration A(p)(F_drive(v) − F_resistance(v))/m − F_turn/m with the existing signed distance-midpoint scheme. The predictor supplies the midpoint force, which is also used for work accounting. If passive end speed exceeds the unchanged maximum end target, only the required extra acceleration debit is applied, bounded by the unchanged #47 correction capability. Otherwise no active correction is applied, and passive speed is never raised to a target. Loss is debited exactly once; it is not appended to a full old target correction. Active correction occupies the accepted interval, with time 2Δs/(v_start+v_end).

Engine forward force remains nonnegative. The displayed useful-drive remainder is max(0, A F_drive − F_turn); excess turning loss remains passive drag rather than negative engine propulsion. In particular, A = 0 and correction-active intervals still dissipate energy. Unlike the rejected drive-only model, large coefficients do not saturate total dissipation at available engine drive. The #47 availability-scaled resistance abstraction is deliberately unchanged.

At c = 0 the original #47 midpoint/correction/carry branches are called directly, including partial correction distance/time. The zero scenario reuses the complete merged #47 search and repeated-lap objects. For c > 0 correction distance/time denote intervals needing additional control; mode-distance accounting partitions every physical metre, including coasting in the outside-correction (Drive) bucket.

## Zero-cost invariant

Winner FCT-B843ADDB373E; eleven zero controls; ConstantInner sector 6.676829 s; winner sector 6.676829 s; objective YES; geometry YES; repeated lap 13.353217 s. Direct-replay regressions also compare validity, full profiles and exact correction distance/time.

## Frozen independent-review exploit regression

Reviewed c = 0.005 geometry: FCT-AE7832278759. Controls (m): 0.522111535,0.617457867,0.604532838,0.488273501,0.313086927,0.145888388,0.050542116,0.063467115,0.179726511,0.354912996,0.584611535.

| trajectory | total J | correction J | outside correction J | correction m | correction s | corner s | exit m/s | sector s | headroom m |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| frozen reviewed winner | 1222.762858 | 651.832223 | 570.930635 | 48.389763 | 2.007824 | 4.223091 | 23.507759 | 6.690159 | 0.000039322 |

All accepted frozen-trajectory intervals with positive speed/nonzero curvature pay positive force, power and energy: YES. The reviewed drive-only implementation had zero correction-phase work and fails this invariant.

## Phase-boundary continuity regression

Fixed ConstantInner geometry and c = 0.005. Construct the first interval's passive-end-speed equality with the unmodified end target by deterministic bisection, then perturb only entry speed by ±0.00005 m/s. This constructs the boundary; it changes neither coefficients nor the law and performs no smoothing. The whole corner and following straight are replayed.

| state | entry m/s | curvature 1/m | correction active | first force N | first power W | total J | sector s |
|---|---:|---:|:---:|---:|---:|---:|---:|
| below | 26.730747223 | 0.032258064 | NO | 16.362466812 | 437.345703125 | 1212.845920862 | 6.695296764 |
| above | 26.730846405 | 0.032258064 | YES | 16.362588882 | 437.350585938 | 1212.846102060 | 6.695296764 |

Absolute whole-sector change 0.000000000 s; total-work change 0.000181198 J. Regression bounds are 0.0001 s / 0.1 J, with positive force and power on both sides.

## Complete unchanged sensitivity sweep

| c | winner / controls m | inner sector s | winner sector s | advantage s | path m | min radius m | max curvature 1/m | corner s | exit m/s | straight s | headroom m | boundary sensitive | objective / geometry |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---:|:---:|
| 0.000 | Flat-0 / FCT-B843ADDB373E<br>0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000 | 6.676829 | 6.676829 | 0.000000 | 97.390022 | 31.000000 | 0.032258 | 4.201727 | 23.405930 | 2.475102 | 0.019414285 | NO | YES / YES |
| 0.001 | Flat-0 / FCT-B843ADDB373E<br>0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000 | 6.680686 | 6.680686 | 0.000000 | 97.390022 | 31.000000 | 0.032258 | 4.203225 | 23.376137 | 2.477461 | 0.019424144 | NO | YES / YES |
| 0.005 | Low-phase-neutral / FCT-AE7832278759<br>0.522111535,0.617457867,0.604532838,0.488273501,0.313086927,0.145888388,0.050542116,0.063467115,0.179726511,0.354912996,0.584611535 | 6.696181 | 6.690159 | 0.006022 | 98.455078 | 30.295263 | 0.033008 | 4.223091 | 23.507759 | 2.467068 | 0.000039322 | YES | YES / YES |
| 0.020 | Final-pair-check / FCT-B0C31EE47BC8<br>0.459611535,0.554957867,0.604532838,0.550773501,0.375586927,0.145888388,0.050542116,0.063467115,0.179726511,0.354912996,0.584611535 | 6.754423 | 6.731904 | 0.022519 | 98.465294 | 30.298119 | 0.033005 | 4.238116 | 23.171309 | 2.493788 | 0.000025215 | YES | NO / YES |
| 0.080 | Final-pair-check / FCT-B51435269784<br>0.772000074,0.501000047,0.355000019,0.208999991,0.062999994,0.041999996,0.188000023,0.333999991,0.355000019,0.438499987,0.959500074 | 6.989709 | 6.919392 | 0.070317 | 98.442245 | 29.629589 | 0.033750 | 4.299716 | 21.662909 | 2.619677 | 0.000007698 | YES | YES / YES |
| 0.320 | Final-pair-check / FCT-FA7FFC9545FE<br>0.650216579,0.099796295,0.054597855,0.028198719,0.018896103,0.033119202,0.021539688,0.031957150,0.175445557,0.074354887,1.053765059 | 9.323348 | 9.135448 | 0.187900 | 97.846741 | 23.868666 | 0.041896 | 5.749759 | 14.504578 | 3.385690 | -0.000060436 | YES | NO / YES |
| 1.280 | Final-pair-check / FCT-E84B5F7D6C0E<br>0.574304104,0.066098213,0.180801630,0.054704666,0.037802935,0.729712009,2.666715860,2.945785046,1.078859329,0.281145573,1.865528822 | 21.452099 | 19.918243 | 1.533855 | 100.849014 | 17.914061 | 0.055822 | 15.618032 | 9.013933 | 4.300212 | 0.000169559 | YES | NO / YES |

### Phase energy and distance accounting: controls and winners

Drive means every interval outside active correction, including zero-drive coast. Energies are independently accumulated per accepted interval; correction + drive must equal total within 1e-7 J. Average force = phase work / phase-mode distance. Correction m/s are actual active-control distance/time, while mode metres partition the entire path.

| c | line | valid | sector s | total J | correction J | drive J | split error J | correction mode m | outside mode m | correction m | correction s | avg correction N | avg outside N |
|---:|---|:---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.000 | ConstantInner | YES | 6.676829 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 45.694866 | 51.695156 | 45.095268 | 1.876382 | 0.000000 | 0.000000 |
| 0.000 | ConstantL1 | YES | 6.863337 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 35.428375 | 73.429436 | 34.537167 | 1.401044 | 0.000000 | 0.000000 |
| 0.000 | ConstantL2 | YES | 7.079330 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 24.161289 | 96.163113 | 23.979046 | 0.950147 | 0.000000 | 0.000000 |
| 0.000 | ConstantL3 | YES | 7.320224 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 13.964947 | 117.825703 | 13.420996 | 0.520274 | 0.000000 | 0.000000 |
| 0.000 | ConstantOuter | YES | 7.582354 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 2.875862 | 140.379189 | 2.862947 | 0.108729 | 0.000000 | 0.000000 |
| 0.000 | Flat-0 | YES | 6.676829 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 45.694866 | 51.695156 | 45.095268 | 1.876382 | 0.000000 | 0.000000 |
| 0.001 | ConstantInner | YES | 6.680686 | 243.037163 | 122.988378 | 120.048786 | 0.000000000 | 45.694866 | 51.695156 | 45.694866 | 1.899051 | 2.691514 | 2.322244 |
| 0.001 | ConstantL1 | YES | 6.867476 | 266.514913 | 86.525687 | 179.989226 | 0.000000000 | 34.428375 | 74.429436 | 34.428375 | 1.396937 | 2.513209 | 2.418253 |
| 0.001 | ConstantL2 | YES | 7.083969 | 285.505869 | 57.455459 | 228.050410 | -0.000000000 | 24.161289 | 96.163113 | 24.161289 | 0.957060 | 2.377996 | 2.371496 |
| 0.001 | ConstantL3 | YES | 7.325498 | 301.162025 | 31.650737 | 269.511288 | 0.000000000 | 13.964947 | 117.825703 | 13.964947 | 0.540862 | 2.266442 | 2.287373 |
| 0.001 | ConstantOuter | YES | 7.588449 | 314.250584 | 6.225059 | 308.025525 | 0.000000000 | 2.875862 | 140.379189 | 2.875862 | 0.109246 | 2.164589 | 2.194239 |
| 0.001 | Flat-0 | YES | 6.680686 | 243.037163 | 122.988378 | 120.048786 | 0.000000000 | 45.694866 | 51.695156 | 45.694866 | 1.899051 | 2.691514 | 2.322244 |
| 0.005 | ConstantInner | YES | 6.696181 | 1211.928742 | 614.850746 | 597.077996 | 0.000000000 | 45.694866 | 51.695156 | 45.694866 | 1.899059 | 13.455576 | 11.549980 |
| 0.005 | ConstantL1 | YES | 6.884126 | 1328.310951 | 432.512832 | 895.798119 | -0.000000000 | 34.428375 | 74.429436 | 34.428375 | 1.396980 | 12.562685 | 12.035536 |
| 0.005 | ConstantL2 | YES | 7.102654 | 1421.784080 | 274.236370 | 1147.547710 | 0.000000000 | 23.161289 | 97.163113 | 23.161289 | 0.919243 | 11.840290 | 11.810528 |
| 0.005 | ConstantL3 | YES | 7.346894 | 1498.364052 | 134.566041 | 1363.798011 | 0.000000000 | 11.964947 | 119.825703 | 11.964947 | 0.465141 | 11.246689 | 11.381515 |
| 0.005 | ConstantOuter | YES | 7.613032 | 1562.059514 | 9.408825 | 1552.650690 | 0.000000000 | 0.875862 | 142.379189 | 0.875862 | 0.033393 | 10.742358 | 10.905040 |
| 0.005 | Low-phase-neutral | YES | 6.690159 | 1222.762858 | 651.832223 | 570.930635 | 0.000000000 | 48.389761 | 50.065317 | 48.389763 | 2.007824 | 13.470458 | 11.403716 |
| 0.020 | ConstantInner | YES | 6.754423 | 4799.615108 | 2393.984926 | 2405.630182 | 0.000000000 | 44.694866 | 52.695156 | 44.694866 | 1.861267 | 53.562861 | 45.651828 |
| 0.020 | ConstantL1 | YES | 6.947248 | 5248.738653 | 1558.614671 | 3690.123982 | 0.000000000 | 31.428375 | 77.429436 | 31.428375 | 1.282954 | 49.592595 | 47.657896 |
| 0.020 | ConstantL2 | YES | 7.174478 | 5598.327195 | 886.848236 | 4711.478959 | 0.000000000 | 19.053631 | 101.270771 | 19.053631 | 0.762412 | 46.544842 | 46.523581 |
| 0.020 | ConstantL3 | YES | 7.430105 | 5877.456116 | 261.984910 | 5615.471206 | 0.000000000 | 5.964947 | 125.825703 | 5.964947 | 0.234615 | 43.920746 | 44.628968 |
| 0.020 | ConstantOuter | YES | 7.783545 | 5999.879086 | 0.000000 | 5999.879086 | 0.000000000 | 0.000000 | 143.255051 | 0.000000 | 0.000000 | 0.000000 | 41.882496 |
| 0.020 | Final-pair-check | YES | 6.731904 | 4855.503327 | 2709.064531 | 2146.438796 | 0.000000000 | 51.254990 | 47.210304 | 51.254990 | 2.147328 | 52.854650 | 45.465473 |
| 0.080 | ConstantInner | YES | 6.989709 | 18466.583547 | 7810.272218 | 10656.311329 | 0.000000000 | 37.694866 | 59.695156 | 37.694866 | 1.592887 | 207.197240 | 178.512161 |
| 0.080 | ConstantL1 | YES | 7.222745 | 19809.803988 | 1637.431409 | 18172.372579 | 0.000000000 | 9.142967 | 99.714844 | 9.142967 | 0.391751 | 179.091904 | 182.243404 |
| 0.080 | ConstantL2 | YES | 7.604131 | 20247.366953 | 0.000000 | 20247.366953 | 0.000000000 | 0.000000 | 120.324402 | 0.000000 | 0.000000 | 0.000000 | 168.273157 |
| 0.080 | ConstantL3 | YES | 8.065269 | 20299.511902 | 0.000000 | 20299.511902 | 0.000000000 | 0.000000 | 131.790649 | 0.000000 | 0.000000 | 0.000000 | 154.028469 |
| 0.080 | ConstantOuter | YES | 8.525609 | 20350.829981 | 0.000000 | 20350.829981 | 0.000000000 | 0.000000 | 143.255051 | 0.000000 | 0.000000 | 0.000000 | 142.060122 |
| 0.080 | Final-pair-check | YES | 6.919392 | 18453.048132 | 8807.381098 | 9645.667035 | 0.000000000 | 44.161879 | 54.280367 | 44.161880 | 1.872765 | 199.434023 | 177.700844 |
| 0.320 | ConstantInner | YES | 9.323348 | 45790.086187 | 0.000000 | 45790.086187 | 0.000000000 | 0.000000 | 97.390022 | 0.000000 | 0.000000 | 0.000000 | 470.172253 |
| 0.320 | ConstantL1 | YES | 9.913499 | 46099.117314 | 0.000000 | 46099.117314 | 0.000000000 | 0.000000 | 108.857811 | 0.000000 | 0.000000 | 0.000000 | 423.480106 |
| 0.320 | ConstantL2 | YES | 10.500729 | 46406.062949 | 0.000000 | 46406.062949 | 0.000000000 | 0.000000 | 120.324402 | 0.000000 | 0.000000 | 0.000000 | 385.674578 |
| 0.320 | ConstantL3 | YES | 11.085304 | 46710.975551 | 0.000000 | 46710.975551 | 0.000000000 | 0.000000 | 131.790649 | 0.000000 | 0.000000 | 0.000000 | 354.433154 |
| 0.320 | ConstantOuter | YES | 11.667233 | 47013.821740 | 0.000000 | 47013.821740 | 0.000000000 | 0.000000 | 143.255051 | 0.000000 | 0.000000 | 0.000000 | 328.182647 |
| 0.320 | Final-pair-check | YES | 9.135448 | 44481.855397 | 0.000000 | 44481.855397 | 0.000000000 | 0.000000 | 97.846741 | 0.000000 | 0.000000 | 0.000000 | 454.607431 |
| 1.280 | ConstantInner | YES | 21.452099 | 56761.564118 | 0.000000 | 56761.564118 | 0.000000000 | 0.000000 | 97.390022 | 0.000000 | 0.000000 | 0.000000 | 582.827304 |
| 1.280 | ConstantL1 | YES | 22.932623 | 57572.139370 | 0.000000 | 57572.139370 | 0.000000000 | 0.000000 | 108.857811 | 0.000000 | 0.000000 | 0.000000 | 528.874675 |
| 1.280 | ConstantL2 | YES | 24.390408 | 58379.286981 | 0.000000 | 58379.286981 | 0.000000000 | 0.000000 | 120.324402 | 0.000000 | 0.000000 | 0.000000 | 485.182441 |
| 1.280 | ConstantL3 | YES | 25.828985 | 59182.751775 | 0.000000 | 59182.751775 | 0.000000000 | 0.000000 | 131.790649 | 0.000000 | 0.000000 | 0.000000 | 449.066395 |
| 1.280 | ConstantOuter | YES | 27.249607 | 59982.796109 | 0.000000 | 59982.796109 | 0.000000000 | 0.000000 | 143.255051 | 0.000000 | 0.000000 | 0.000000 | 418.713308 |
| 1.280 | Final-pair-check | YES | 19.918243 | 54775.454716 | 3815.936523 | 50959.518193 | 0.000000000 | 1.000000 | 99.849014 | 1.000000 | 0.038605 | 3815.936523 | 510.365761 |

### Every winner versus ConstantInner: integrated trade-off

Values are winner / inner at the same coefficient, not evidence of a uniformly wider line. Integrated demand uses midpoint speed and |κ| over physical ds; time-weighted demand and radius use actual interval time.

| c | min R m | max κ 1/m | mean abs κ 1/m | time-weighted R m | time-weighted a_lat m/s² | integral v²abs(κ) ds m²/s² | total work J | drive work J | exit m/s | extra path m | lower demand / drive work / total work / better exit / longer path |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---:|
| 0.000 | 31.000000 / 31.000000 | 0.032258 / 0.032258 | 0.032258 / 0.032258 | 31.000000 / 31.000000 | 17.401576 / 17.401576 | 1708.929064 / 1708.929064 | 0.000000 / 0.000000 | 0.000000 / 0.000000 | 23.405930 / 23.405930 | 0.000000 | NO / NO / NO / NO / NO |
| 0.001 | 31.000000 / 31.000000 | 0.032258 / 0.032258 | 0.032258 / 0.032258 | 31.000000 / 31.000000 | 17.389364 / 17.389364 | 1707.805565 / 1707.805565 | 243.037163 / 243.037163 | 120.048786 / 120.048786 | 23.376137 / 23.376137 | 0.000000 | NO / NO / NO / NO / NO |
| 0.005 | 30.295263 / 31.000000 | 0.033008 / 0.032258 | 0.031779 / 0.032258 | 31.505777 / 31.000000 | 17.349051 / 17.340751 | 1721.464270 / 1703.337804 | 1222.762858 / 1211.928742 | 570.930635 / 597.077996 | 23.507759 / 23.257538 | 1.065056 | NO / YES / NO / YES / YES |
| 0.020 | 30.298119 / 31.000000 | 0.033005 / 0.032258 | 0.031759 / 0.032258 | 31.527040 / 31.000000 | 17.214441 / 17.159630 | 1707.730578 / 1686.849894 | 4855.503327 / 4799.615108 | 2146.438796 / 2405.630182 | 23.171309 / 22.819992 | 1.075272 | NO / YES / NO / YES / YES |
| 0.080 | 29.629589 / 31.000000 | 0.033750 / 0.032258 | 0.030969 / 0.032258 | 32.431293 / 31.000000 | 16.320568 / 16.448759 | 1624.077687 / 1624.217020 | 18453.048132 / 18466.583547 | 9645.667035 / 10656.311329 | 21.662909 / 21.179106 | 1.052223 | YES / YES / YES / YES / YES |
| 0.320 | 23.868666 / 31.000000 | 0.041896 / 0.032258 | 0.030112 / 0.032258 | 34.795879 / 31.000000 | 9.121989 / 9.381151 | 978.966960 / 1007.790185 | 44481.855397 / 45790.086187 | 44481.855397 / 45790.086187 | 14.504578 / 13.845848 | 0.456718 | YES / YES / YES / YES / YES |
| 1.280 | 17.914061 / 31.000000 | 0.055822 / 0.032258 | 0.028180 / 0.032258 | 178.241623 / 31.000000 | 1.543809 / 1.540860 | 301.685236 / 312.754365 | 54775.454716 / 56761.564118 | 50959.518193 / 56761.564118 | 9.013933 / 7.164661 | 3.458992 | NO / YES / YES / YES / YES |

### LateralExecutionBoundarySensitive diagnostic

YES means minimum numerical interval headroom ≤ 0.01 m. For each such winner, replay the fixed controls after subtracting 0.01 / 0.02 m from the per-interval execution budget (floored at zero); no search, geometry or production limit is changed. This strict diagnostic is mesh-dependent, especially for dense variable-curvature intervals; rejection demonstrates numerical-boundary sensitivity, not a calibrated physical margin.

| c | reserve m | fixed winner valid | reason |
|---:|---:|:---:|---|
| 0.005 | 0.01 | NO | LateralExecutionConstraint |
| 0.005 | 0.02 | NO | LateralExecutionConstraint |
| 0.020 | 0.01 | NO | LateralExecutionConstraint |
| 0.020 | 0.02 | NO | LateralExecutionConstraint |
| 0.080 | 0.01 | NO | LateralExecutionConstraint |
| 0.080 | 0.02 | NO | LateralExecutionConstraint |
| 0.320 | 0.01 | NO | LateralExecutionConstraint |
| 0.320 | 0.02 | NO | LateralExecutionConstraint |
| 1.280 | 0.01 | NO | LateralExecutionConstraint |
| 1.280 | 0.02 | NO | LateralExecutionConstraint |

### Search closure and repeated-lap diagnostics

| c | starts | refined | top-3 closed | independent near starts | single residual s | pair residual s | objective | geometry | valid / evaluated | repeat converged | max v m/s | avg v m/s | lap s | guardrail V/A/L |
|---:|---:|---:|:---:|---:|---:|---:|:---:|:---:|---:|:---:|---:|---:|---:|:---:|
| 0.000 | 106 | 48 | YES | 5 | 0.000000 | 0.000000 | YES | YES | 5754 / 9196 | YES | 26.515173 | 23.872900 | 13.353217 | NO/NO/NO |
| 0.001 | 106 | 48 | YES | 5 | 0.000000 | 0.000000 | YES | YES | 5754 / 9200 | YES | 26.496538 | 23.858759 | 13.361132 | NO/NO/NO |
| 0.005 | 106 | 48 | YES | 5 | 0.000000 | 0.000000 | YES | YES | 5914 / 9289 | YES | 26.578981 | 23.984768 | 13.379751 | NO/NO/NO |
| 0.020 | 106 | 48 | YES | 1 | 0.000993 | 0.000000 | NO | YES | 5891 / 9211 | YES | 26.368790 | 23.834763 | 13.464823 | NO/NO/NO |
| 0.080 | 106 | 48 | YES | 3 | 0.000000 | 0.000000 | YES | YES | 6938 / 10999 | YES | 25.448679 | 23.055033 | 13.918221 | NO/NO/NO |
| 0.320 | 106 | 48 | YES | 2 | 0.000000 | 0.000000 | NO | YES | 5917 / 11522 | YES | 21.081026 | 15.146453 | 21.106995 | NO/NO/YES |
| 1.280 | 106 | 48 | YES | 1 | 0.001112 | 0.000000 | NO | YES | 8364 / 15603 | YES | 19.382330 | 7.061489 | 46.126945 | NO/NO/YES |

The unchanged descriptive ±15% guardrails reference maximum speed 111 km/h, average speed 22.38 m/s and flying lap 14.71 s. They are not optimization targets. Objective convergence and geometry convergence are separate; no conclusion is promoted from an unclosed search.

## Interpretation after repair

At c = 0.005 a non-inner candidate remains best, but now pays turning loss through the whole corner. Its integrated trade-offs and numerical lateral-boundary dependence are reported above; it is not automatically validated by removal of the exploit.

First objectively converged sampled non-inner advantage: c = 0.005; gain 0.006022 s. This is a sampled result, not a fitted continuous threshold.

Any surviving non-inner result must be judged by the full-corner energy/demand comparison, exit speed, extra path and lateral-boundary diagnostic, not by the name 'wider'. No controller state receives free dissipation. Large c remains an uncalibrated effective-slip surrogate; c = 1.280 is far outside a small-angle interpretation. Unclosed searches and broken global guardrails remain explicit uncertainty. No production promotion follows; a coupled longitudinal/lateral tyre-force envelope is a separate future hypothesis, not part of this repair.

## Fixed-geometry zero-limit validation (reviewed HEAD 3b1a1a8995e8a4f7a79edb7388012bc2223b65c3)

These are fixed ConstantInner and reviewed c=.005-winner controls, not fresh searches. The requested sequence is augmented with c=1e-8 and 1e-7 to resolve a finite offset. Delta/c should remain bounded with a zero intercept for an O(c) response. The two-point intercept uses only those two smallest positive coefficients; it is a numerical diagnostic, not an extrapolated physical calibration.

| geometry | c | sector s | delta vs c=0 s | delta/c s | corner s | exit m/s | correction m | correction s | work J |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| ConstantInner | 0.00000000 | 6.676829338 | 0.000000000 | — | 4.201726913 | 23.405929565 | 45.095268250 | 1.876382470 | 0.000000000 |
| ConstantInner | 0.00000001 | 6.676812172 | -0.000017166 | -1716.613780 | 4.201709747 | 23.405929565 | 45.694866180 | 1.899049401 | 0.002432009 |
| ConstantInner | 0.00000010 | 6.676813126 | -0.000016212 | -162.124632 | 4.201710224 | 23.405925751 | 45.694866180 | 1.899049401 | 0.024320092 |
| ConstantInner | 0.00000100 | 6.676817894 | -0.000011444 | -11.444092 | 4.201712132 | 23.405891418 | 45.694866180 | 1.899049401 | 0.243200710 |
| ConstantInner | 0.00001000 | 6.676853180 | 0.000023842 | 2.384186 | 4.201726437 | 23.405618668 | 45.694866180 | 1.899049520 | 2.431991676 |
| ConstantInner | 0.00010000 | 6.677200794 | 0.000371456 | 3.714562 | 4.201862335 | 23.402944565 | 45.694866180 | 1.899049759 | 24.318446346 |
| ConstantInner | 0.00100000 | 6.680686474 | 0.003857136 | 3.857136 | 4.203225136 | 23.376136780 | 45.694866180 | 1.899051428 | 243.037163422 |
| reviewed c=.005 winner | 0.00000000 | 6.683063030 | 0.000000000 | — | 4.219650269 | 23.554262161 | 42.368873596 | 1.751068354 | 0.000000000 |
| reviewed c=.005 winner | 0.00000001 | 6.682962418 | -0.000100613 | -10061.264099 | 4.219550133 | 23.554267883 | 56.519691467 | 2.370117426 | 0.002449193 |
| reviewed c=.005 winner | 0.00000010 | 6.682962418 | -0.000100613 | -1006.126392 | 4.219550133 | 23.554267883 | 56.472076416 | 2.367954731 | 0.024491926 |
| reviewed c=.005 winner | 0.00000100 | 6.682962894 | -0.000100136 | -100.135803 | 4.219550610 | 23.554267883 | 56.153541565 | 2.353452682 | 0.244919220 |
| reviewed c=.005 winner | 0.00001000 | 6.682965755 | -0.000097275 | -9.727478 | 4.219553471 | 23.554267883 | 54.350639343 | 2.271298647 | 2.449188775 |
| reviewed c=.005 winner | 0.00010000 | 6.683020592 | -0.000042439 | -0.424385 | 4.219608307 | 23.554267883 | 53.563560486 | 2.235792875 | 24.491320867 |
| reviewed c=.005 winner | 0.00100000 | 6.683599472 | 0.000536442 | 0.536442 | 4.220187187 | 23.554267883 | 52.205085754 | 2.176682234 | 244.853110584 |

ConstantInner: estimated sector intercept -0.000017272 s; zero-limit diagnostic FAIL at ±0.000008000 s resolution (about sixteen sector-output float ULPs). This is far stricter than 0.01 s and the 0.006022 s winning margin.
reviewed c=.005 winner: estimated sector intercept -0.000100613 s; zero-limit diagnostic FAIL at ±0.000008000 s resolution (about sixteen sector-output float ULPs). This is far stricter than 0.01 s and the 0.006022 s winning margin.

Finite c=0 discontinuity detected: the positive-c deltas plateau rather than scale to zero. Thus the requested clean zero-limit validation FAILS. At zero, #47 uses partial bounded correction followed by constant-speed carry; positive c uses midpoint passive integration plus additional control over a whole interval and endpoint-average time. Correction distance/time also change definition, and tiny positive loss can activate many negligible whole-interval corrections. Energy tends to zero, but that does not establish controller/time continuity. The regression tests verify that this failure is detected; green software tests do NOT certify zero-limit continuity.

The integrator, force law, exact c=0 #47 branch, seven-coefficient search and convergence diagnostics are intentionally unchanged in this validation-only iteration. No fitted bridge, smoothing, tolerance relaxation or production change hides the failure.

## Physical track-space robustness at c=.005

Frozen reference sector 6.690158844 s; ConstantInner 6.696181297 s. Whole-line shift adds the same signed metres to all eleven controls; positive is outward. Amplitude multiplies all offsets relative to ConstantInner (zero). Maximum displacement samples the full spline at 4097 equal-angle progress positions, comparing radial offsets at the same track angle, not just controls or percentage of arc length. This is a sampled maximum, not an analytical extremum. The reference occupies 0.041899353–0.626892686 m from the inside edge.

Invalid variants retain their original controls/reasons; — means no valid trajectory/performance metric. No invalid path is clamped or assigned a zero time. The inward shifts cross the physical inner boundary, not the numerical lateral-execution gate.

| mode / parameter | controls m | validity / reason | max displacement m | sector s | delta winner s | delta inner s | corner s | exit m/s | path m | min R m | max κ 1/m | mean abs κ 1/m | total J | correction J | outside correction J |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| shift -1.000000 m | -0.477888465,-0.382542133,-0.395467162,-0.511726499,-0.686913073,-0.854111612,-0.949457884,-0.936532855,-0.820273519,-0.645087004,-0.415388465 | TrackBoundary | — | — | — | — | — | — | — | — | — | — | — | — | — |
| shift -0.500000 m | 0.022111535,0.117457867,0.104532838,-0.011726499,-0.186913073,-0.354111612,-0.449457884,-0.436532885,-0.320273489,-0.145087004,0.084611535 | TrackBoundary | — | — | — | — | — | — | — | — | — | — | — | — | — |
| shift -0.250000 m | 0.272111535,0.367457867,0.354532838,0.238273501,0.063086927,-0.104111612,-0.199457884,-0.186532885,-0.070273489,0.104912996,0.334611535 | TrackBoundary | — | — | — | — | — | — | — | — | — | — | — | — | — |
| shift -0.100000 m | 0.422111541,0.517457843,0.504532814,0.388273507,0.213086933,0.045888387,-0.049457885,-0.036532886,0.079726510,0.254913002,0.484611541 | TrackBoundary | — | — | — | — | — | — | — | — | — | — | — | — | — |
| shift 0.000000 m | 0.522111535,0.617457867,0.604532838,0.488273501,0.313086927,0.145888388,0.050542116,0.063467115,0.179726511,0.354912996,0.584611535 | Valid | 0.000000000 | 6.690158844 | 0.000000000 | -0.006022453 | 4.223091125 | 23.507759094 | 98.455078125 | 30.295263290 | 0.033008460 | 0.031778995 | 1222.762857986 | 651.832223261 | 570.930634726 |
| shift +0.100000 m | 0.622111559,0.717457891,0.704532862,0.588273525,0.413086921,0.245888382,0.150542110,0.163467109,0.279726505,0.454912990,0.684611559 | Valid | 0.100000083 | 6.694768906 | 0.004610062 | -0.001412392 | 4.230184078 | 23.539333344 | 98.769126892 | 30.395090103 | 0.032900050 | 0.031678345 | 1226.412726112 | 654.994641217 | 571.418084895 |
| shift +0.250000 m | 0.772111535,0.867457867,0.854532838,0.738273501,0.563086927,0.395888388,0.300542116,0.313467115,0.429726511,0.604912996,0.834611535 | Valid | 0.250000030 | 6.701768875 | 0.011610031 | 0.005587578 | 4.240887165 | 23.586540222 | 99.240295410 | 30.544830322 | 0.032738764 | 0.031528559 | 1231.822183579 | 642.066293251 | 589.755890328 |
| shift +0.500000 m | 1.022111535,1.117457867,1.104532838,0.988273501,0.813086927,0.645888388,0.550542116,0.563467145,0.679726481,0.854912996,1.084611535 | Valid | 0.500000060 | 6.713553429 | 0.023394585 | 0.017372131 | 4.258799076 | 23.664880753 | 100.025688171 | 30.794410706 | 0.032473426 | 0.031282034 | 1240.731092128 | 631.012228845 | 609.718863282 |
| shift +1.000000 m | 1.522111535,1.617457867,1.604532838,1.488273501,1.313086987,1.145888329,1.050542116,1.063467145,1.179726481,1.354912996,1.584611535 | Valid | 1.000000119 | 6.737618446 | 0.047459602 | 0.041437149 | 4.294938087 | 23.820230484 | 101.596221924 | 31.293577194 | 0.031955440 | 0.030800374 | 1258.117935825 | 593.154757601 | 664.963178224 |
| amplitude 75.000000% | 0.391583651,0.463093400,0.453399628,0.366205126,0.234815195,0.109416291,0.037906587,0.047600336,0.134794891,0.266184747,0.438458651 | Valid | 0.156723142 | 6.690163612 | 0.000004768 | -0.006017685 | 4.219100952 | 23.457054138 | 98.186851501 | 30.461175919 | 0.032828674 | 0.031898558 | 1219.790605978 | 637.845531777 | 581.945074201 |
| amplitude 89.999998% | 0.469900370,0.555712044,0.544079542,0.439446151,0.281778216,0.131299540,0.045487903,0.057120401,0.161753863,0.319421679,0.526150346 | Valid | 0.062689245 | 6.689720154 | -0.000438690 | -0.006461143 | 4.221340179 | 23.491085052 | 98.347656250 | 30.360849380 | 0.032937154 | 0.031826805 | 1221.656097018 | 645.638967025 | 576.017129994 |
| amplitude 100.000000% | 0.522111535,0.617457867,0.604532838,0.488273501,0.313086927,0.145888388,0.050542116,0.063467115,0.179726511,0.354912996,0.584611535 | Valid | 0.000000000 | 6.690158844 | 0.000000000 | -0.006022453 | 4.223091125 | 23.507759094 | 98.455078125 | 30.295263290 | 0.033008460 | 0.031778995 | 1222.762857986 | 651.832223261 | 570.930634726 |
| amplitude 110.000002% | 0.574322701,0.679203689,0.664986134,0.537100852,0.344395638,0.160477236,0.055596329,0.069813825,0.197699159,0.390404314,0.643072724 | Valid | 0.062689304 | 6.690585613 | 0.000426769 | -0.005595684 | 4.224842072 | 23.524593353 | 98.562622070 | 30.230686188 | 0.033078972 | 0.031731214 | 1223.870466465 | 658.488778868 | 565.381687597 |
| amplitude 125.000000% | 0.652639389,0.771822333,0.755666018,0.610341907,0.391358674,0.182360485,0.063177645,0.079333894,0.224658132,0.443641245,0.730764389 | Valid | 0.156723201 | 6.691259384 | 0.001100540 | -0.004921913 | 4.227496147 | 23.549810410 | 98.724479675 | 30.135684967 | 0.033183251 | 0.031659592 | 1225.529438317 | 666.043152211 | 559.486286106 |

### Separate local coherent-amplitude refinement

The fixed 90% probe improves on the frozen reference beyond the 8 µs numerical resolution, so a separate one-parameter refinement starts there. Eight levels test only amplitude ±0.05, halving the step each level; retain the best valid sector time with lower-amplitude tie-breaking. 17 recorded evaluations including the seed; no 106-start searches or independent control perturbations. Best tested amplitude 83.593750%: sector 6.689475536 s, delta reference -0.000683308 s, displacement 0.102849543 m. This is local one-dimensional evidence, not a replacement globally converged winner.

| mode / parameter | controls m | validity / reason | max displacement m | sector s | delta winner s | delta inner s | corner s | exit m/s | path m | min R m | max κ 1/m | mean abs κ 1/m | total J | correction J | outside correction J |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| amplitude 89.999998% | 0.469900370,0.555712044,0.544079542,0.439446151,0.281778216,0.131299540,0.045487903,0.057120401,0.161753863,0.319421679,0.526150346 | Valid | 0.062689245 | 6.689720154 | -0.000438690 | -0.006461143 | 4.221340179 | 23.491085052 | 98.347656250 | 30.360849380 | 0.032937154 | 0.031826805 | 1221.656097018 | 645.638967025 | 576.017129994 |
| amplitude 84.999996% | 0.443794787,0.524839163,0.513852894,0.415032446,0.266123891,0.124005124,0.042960797,0.053947046,0.152767524,0.301676035,0.496919781 | Valid | 0.094033897 | 6.689521790 | -0.000637054 | -0.006659508 | 4.220472813 | 23.482597351 | 98.293922424 | 30.394029617 | 0.032901198 | 0.031850718 | 1221.096322360 | 640.712661468 | 580.383660892 |
| amplitude 94.999999% | 0.496005952,0.586584985,0.574306190,0.463859826,0.297432572,0.138593972,0.048015010,0.060293760,0.170740187,0.337167352,0.555380940 | Valid | 0.031344593 | 6.689938068 | -0.000220776 | -0.006243229 | 4.222213745 | 23.499414444 | 98.401321411 | 30.327930450 | 0.032972906 | 0.031802896 | 1222.209223951 | 649.093160018 | 573.116063933 |
| amplitude 82.499999% | 0.430741996,0.509402752,0.498739570,0.402825624,0.258296698,0.120357916,0.041697245,0.052360371,0.148274377,0.292803228,0.482304513 | Valid | 0.109706223 | 6.689567566 | -0.000591278 | -0.006613731 | 4.220091343 | 23.477169037 | 98.267143250 | 30.410720825 | 0.032883141 | 0.031862676 | 1220.790724612 | 638.873430404 | 581.917294209 |
| amplitude 87.499994% | 0.456847548,0.540275574,0.528966188,0.427239299,0.273951054,0.127652332,0.044224348,0.055533722,0.157260686,0.310548842,0.511535048 | Valid | 0.078361630 | 6.689622402 | -0.000536442 | -0.006558895 | 4.220908165 | 23.486848831 | 98.320846558 | 30.377414703 | 0.032919195 | 0.031838760 | 1221.377401384 | 643.310545178 | 578.066856205 |
| amplitude 83.749998% | 0.437268406,0.517120957,0.506296217,0.408929050,0.262210280,0.122181520,0.042329021,0.053153709,0.150520951,0.297239631,0.489612132 | Valid | 0.101870060 | 6.689480782 | -0.000678062 | -0.006700516 | 4.220257759 | 23.480394363 | 98.280441284 | 30.402364731 | 0.032892179 | 0.031856697 | 1220.953367435 | 638.900478409 | 582.052889026 |
| amplitude 86.249995% | 0.450321168,0.532557368,0.521409571,0.421135873,0.270037472,0.125828728,0.043592572,0.054740384,0.155014113,0.306112438,0.504227400 | Valid | 0.086197734 | 6.689571381 | -0.000587463 | -0.006609917 | 4.220689774 | 23.484725952 | 98.307380676 | 30.385711670 | 0.032910205 | 0.031844739 | 1221.237128073 | 641.992667702 | 579.244460371 |
| amplitude 83.124995% | 0.434005201,0.513261795,0.502517879,0.405877322,0.260253489,0.121269718,0.042013131,0.052757036,0.149397656,0.295021415,0.485958308 | Valid | 0.105788171 | 6.689515114 | -0.000643730 | -0.006666183 | 4.220172405 | 23.478876114 | 98.273834229 | 30.406538010 | 0.032887664 | 0.031859685 | 1220.874737838 | 638.875045957 | 581.999691881 |
| amplitude 84.375000% | 0.440531611,0.520980060,0.510074556,0.411980778,0.264167100,0.123093329,0.042644911,0.053550377,0.151644245,0.299457848,0.493265986 | Valid | 0.097952008 | 6.689497948 | -0.000660896 | -0.006683350 | 4.220365524 | 23.481534958 | 98.287239075 | 30.398195267 | 0.032896690 | 0.031853706 | 1221.026667698 | 640.041087510 | 580.985580188 |
| amplitude 83.437496% | 0.435636789,0.515191376,0.504407048,0.407403171,0.261231899,0.121725619,0.042171076,0.052955370,0.149959296,0.296130508,0.487785220 | Valid | 0.103829086 | 6.689490318 | -0.000668526 | -0.006690979 | 4.220215797 | 23.479728699 | 98.277236938 | 30.404455185 | 0.032889917 | 0.031858191 | 1220.917292834 | 638.801745302 | 582.115547533 |
| amplitude 84.062499% | 0.438899994,0.519050539,0.508185387,0.410454899,0.263188690,0.122637421,0.042486966,0.053352043,0.151082590,0.298348725,0.491439074 | Valid | 0.099910975 | 6.689485073 | -0.000673771 | -0.006696224 | 4.220309734 | 23.480991364 | 98.283813477 | 30.400278091 | 0.032894436 | 0.031855199 | 1220.990255428 | 639.508796031 | 581.481459396 |
| amplitude 83.593750% | 0.436452597,0.516156197,0.505351663,0.408166140,0.261721104,0.121953577,0.042250052,0.053054541,0.150240123,0.296685070,0.488698691 | Valid | 0.102849543 | 6.689475536 | -0.000683308 | -0.006705761 | 4.220232964 | 23.480138779 | 98.278831482 | 30.403408051 | 0.032891050 | 0.031857442 | 1220.937106658 | 638.861963350 | 582.075143308 |
| amplitude 83.906245% | 0.438084185,0.518085718,0.507240832,0.409691960,0.262699485,0.122409470,0.042407993,0.053252872,0.150801763,0.297794163,0.490525573 | Valid | 0.100890517 | 6.689483643 | -0.000675201 | -0.006697655 | 4.220286369 | 23.480716705 | 98.282211304 | 30.401325226 | 0.032893304 | 0.031855948 | 1220.973433689 | 639.468562268 | 581.504871421 |
| amplitude 83.515626% | 0.436044723,0.515673816,0.504879355,0.407784671,0.261476517,0.121839598,0.042210564,0.053004958,0.150099725,0.296407819,0.488241971 | Valid | 0.103339314 | 6.689480782 | -0.000678062 | -0.006700516 | 4.220221996 | 23.479934692 | 98.277976990 | 30.403930664 | 0.032890484 | 0.031857818 | 1220.926593622 | 638.824406117 | 582.102187505 |
| amplitude 83.671874% | 0.436860502,0.516638577,0.505823970,0.408547580,0.261965692,0.122067548,0.042289536,0.053104125,0.150380537,0.296962351,0.489155412 | Valid | 0.102359772 | 6.689477921 | -0.000680923 | -0.006703377 | 4.220244884 | 23.480266571 | 98.279617310 | 30.402883530 | 0.032891616 | 0.031857070 | 1220.944903882 | 638.880244941 | 582.064658941 |
| amplitude 83.554685% | 0.436248660,0.515914977,0.505115509,0.407975376,0.261598796,0.121896580,0.042230304,0.053029750,0.150169924,0.296546429,0.488470316 | Valid | 0.103094459 | 6.689482689 | -0.000676155 | -0.006698608 | 4.220232487 | 23.480045319 | 98.278526306 | 30.403669357 | 0.032890767 | 0.031857628 | 1220.933502372 | 638.835313372 | 582.098189001 |
| amplitude 83.632815% | 0.436656564,0.516397417,0.505587816,0.408356875,0.261843413,0.122010566,0.042269796,0.053079333,0.150310338,0.296823740,0.488927096 | Valid | 0.102604628 | 6.689478874 | -0.000679970 | -0.006702423 | 4.220241070 | 23.480203629 | 98.279266357 | 30.403146744 | 0.032891333 | 0.031857256 | 1220.941473468 | 638.872112617 | 582.069360851 |

### Tested-only width and interpretation

Within +0.005 s of the frozen reference: tested valid whole-line shifts 0.00 to 0.10 m. Only discrete tested points qualify; no claim that every intermediate shift qualifies or that the threshold boundary was located.
Within +0.010 s of the frozen reference: tested valid whole-line shifts 0.00 to 0.10 m. Only discrete tested points qualify; no claim that every intermediate shift qualifies or that the threshold boundary was located.
Within +0.020 s of the frozen reference: tested valid whole-line shifts 0.00 to 0.25 m. Only discrete tested points qualify; no claim that every intermediate shift qualifies or that the threshold boundary was located.

The measured basin is anisotropic and one-sided, not a millimetre needle. Outward shifts of 0.10/0.25/0.50/1.00 m lose about 4.61/11.61/23.39/47.46 ms; 0.10 m preserves a small advantage over ConstantInner, whereas 0.25 m loses that advantage. The 0.25–0.50 m whole-line direction is moderate rather than broad (losses are tens, not just a few, milliseconds). All five 75–125% amplitude probes remain valid within about 1.11 ms of the reference while displacing the full spline by up to 0.157 m; this is a comparatively flat coherent-shape direction. Inward shifts of 0.10 m or more are unavailable because the line already approaches the physical inner edge. These probes do not show validity dominated by the numerical gate, nor do they establish a two-sided 0.25–0.50 m basin.

The c=.005 non-inner advantage remains a reproducible exploratory result across a family of shapes; the tiny numerical headroom alone does not imply needle positioning. However, zero-limit validation has failed, so the model is NOT yet validated for production or as a clean physics baseline for the next experiment. Resolve the analysis integrator's c=0/controller semantics in a separately authorized iteration before using a small advantage as physical evidence. This iteration neither repairs that split nor changes the frozen full-search result.
