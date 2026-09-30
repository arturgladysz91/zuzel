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
