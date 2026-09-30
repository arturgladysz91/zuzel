# Corner turning/slip energy cost experiment (#48)

Base merged main: 99f3afd08c8e6892a8b24eb785b5be6cbf116d38. Analysis-only; production physics, geometry, #47 envelope/search/lateral rules and historical artifacts are frozen.

## Continuous dissipative model

The frozen law is F_turn = c m v²|κ|, with m = 142 kg, speed v in m/s, signed path curvature κ in 1/m and dimensionless coefficient c. Power is F_turn v (W); work is Σ F_turn Δs (J). Force vanishes only at zero coefficient, speed or curvature, not at a controller phase boundary. The coefficients are a sensitivity sweep, not a fitted tyre model.

For c > 0 a free-drive interval retains the signed distance-midpoint acceleration A(p)(F_drive(v) − F_resistance(v))/m − F_turn/m. A controller-limited interval instead preserves #47 topology: correct only while entry speed exceeds the maximum target, reach that target if possible, then process only the remaining distance. With k=c|κ| and unchanged bounded control B, distance-midpoint correction solves v1²=v0²−2[B+k((v0+v1)/2)²]ds. The target-crossing distance is (v0²−target²)/[2(B+k((v0+target)/2)²)]. If unreachable, solve the same quadratic over the accepted distance. This is an algebraic crossing, not a coefficient threshold, smoothing or a fitted offset; slip is not appended to a full old correction.

During correction propulsion is rolled off, as in #47. After reaching the target, remaining carry has zero propulsion and slip drag only: v1=v0(1−k ds/2)/(1+k ds/2), bounded below by zero. It never raises speed to the maximum target, creates energy, exceeds the target or reuses correction metres. At c→0 the same crossing and carry reduce to #47 partial correction and neutral constant-speed carry. Each subphase uses #47's float time 2ds/(v0+v1). Work is accumulated once per physical subphase at its midpoint speed; interval force/power diagnostics are work/distance and work/time, rather than an additional force debit.

A candidate that stops before completing the physical corner is explicitly invalid (NonTraversable), not rescued by a positive epsilon, fabricated propulsion, reduced loss or zero-time carry. A free-drive midpoint that reaches zero records its stopping distance v0²/(−2a); neutral midpoint carry stops at ds=2/k. Only distance/time/work up to that stop are evaluated, with finite nonnegative diagnostics and explicit remaining metres. No NaN/Infinity sentinel is used. Counts are separate from geometry, lateral and corner-control failures; coefficients/search tolerances are not tuned to keep stopped candidates alive.

Engine forward force remains nonnegative. The displayed useful-drive remainder is max(0, A F_drive − F_turn); excess turning loss remains passive drag rather than negative engine propulsion. In particular, A = 0 and correction-active intervals still dissipate energy. Unlike the rejected drive-only model, large coefficients do not saturate total dissipation at available engine drive. The #47 availability-scaled resistance abstraction is deliberately unchanged.

At c = 0 the original #47 midpoint/correction/carry branches are called directly, including partial correction distance/time. The zero scenario reuses the complete merged #47 search and repeated-lap objects. For c > 0 correction distance/time denote the physical correction subphase, not a whole controller-limited interval. Profile controller flags retain #47's limiter/carry classification; they are not used to charge a whole interval to correction. Outside-correction (Drive) work includes both actual drive and zero-drive carry/coast. Positive-c mode distances partition the physical subphases; c=0 legacy mode counters retain their old whole-interval classification, with no energy, while actual correction distance/time are comparable at every coefficient.

## Zero-cost invariant

Winner FCT-B843ADDB373E; eleven zero controls; ConstantInner sector 6.676829 s; winner sector 6.676829 s; objective YES; geometry YES; repeated lap 13.353217 s. Direct-replay regressions also compare validity, full profiles and exact correction distance/time.

## Frozen independent-review exploit regression

Reviewed c = 0.005 geometry: FCT-AE7832278759. Controls (m): 0.522111535,0.617457867,0.604532838,0.488273501,0.313086927,0.145888388,0.050542116,0.063467115,0.179726511,0.354912996,0.584611535.

| trajectory | total J | correction J | outside correction J | correction m | correction s | corner s | exit m/s | sector s | headroom m |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| frozen reviewed winner | 1222.185768 | 550.207613 | 671.978155 | 40.553581 | 1.676647 | 4.223166 | 23.507738 | 6.690235 | 0.000039322 |

All accepted frozen-trajectory intervals with positive speed/nonzero curvature pay positive force, power and energy: YES. The reviewed drive-only implementation had zero correction-phase work and fails this invariant.

## Phase-boundary continuity regression

Fixed ConstantInner geometry and c = 0.005. Locate the first interval's physical correction-subphase false→true boundary (entry speed equals the unmodified target) by deterministic bisection, then perturb only entry speed by ±0.00005 m/s. This constructs the repaired phase boundary; it changes neither coefficients nor the law and performs no smoothing. The whole corner and following straight are replayed. Displayed force/power are interval averages of the same subphase work.

| state | entry m/s | curvature 1/m | correction active | first force N | first power W | total J | sector s |
|---|---:|---:|:---:|---:|---:|---:|---:|
| below | 26.726436615 | 0.032258064 | NO | 16.357189178 | 437.134124756 | 1210.013377365 | 6.695475578 |
| above | 26.726535797 | 0.032258064 | YES | 16.357252121 | 437.136627197 | 1210.013496228 | 6.695475578 |

Absolute whole-sector change 0.000000000 s; total-work change 0.000118863 J. Regression bounds are 0.0001 s / 0.1 J, with positive force and power on both sides.

## Complete freshly rerun sensitivity sweep (unchanged coefficient grid/search settings)

| c | winner / controls m | inner sector s | winner sector s | advantage s | path m | min radius m | max curvature 1/m | corner s | exit m/s | straight s | headroom m | boundary sensitive | objective / geometry |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---:|:---:|
| 0.000 | Flat-0 / FCT-B843ADDB373E<br>0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000 | 6.676829 | 6.676829 | 0.000000 | 97.390022 | 31.000000 | 0.032258 | 4.201727 | 23.405930 | 2.475102 | 0.019414285 | NO | YES / YES |
| 0.001 | Flat-0 / FCT-B843ADDB373E<br>0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000,0.000000000 | 6.680735 | 6.680735 | 0.000000 | 97.390022 | 31.000000 | 0.032258 | 4.203273 | 23.376135 | 2.477462 | 0.019424144 | NO | YES / YES |
| 0.005 | Low-phase-neutral / FCT-AE7832278759<br>0.522111535,0.617457867,0.604532838,0.488273501,0.313086927,0.145888388,0.050542116,0.063467115,0.179726511,0.354912996,0.584611535 | 6.696359 | 6.690235 | 0.006124 | 98.455078 | 30.295263 | 0.033008 | 4.223166 | 23.507738 | 2.467069 | 0.000039322 | YES | YES / YES |
| 0.020 | Final-pair-check / FCT-B0C31EE47BC8<br>0.459611535,0.554957867,0.604532838,0.550773501,0.375586927,0.145888388,0.050542116,0.063467115,0.179726511,0.354912996,0.584611535 | 6.755196 | 6.732460 | 0.022735 | 98.465294 | 30.298119 | 0.033005 | 4.238637 | 23.170862 | 2.493824 | 0.000025215 | YES | NO / YES |
| 0.080 | Final-pair-check / FCT-D364B91FB8D5<br>0.772000074,0.626000047,0.480000019,0.333999991,0.187999994,0.104499996,0.188000023,0.333999991,0.480000019,0.875999928,1.647000074 | 6.994877 | 6.904014 | 0.090864 | 98.922935 | 30.521454 | 0.032764 | 4.302255 | 21.872946 | 2.601759 | 0.000000643 | YES | NO / YES |
| 0.320 | Final-pair-check / FCT-95A4880D3BEF<br>0.950299501,0.377585173,0.265010595,0.181962490,0.084559679,0.060225964,0.075712919,0.057570934,0.174176931,0.213428020,1.242872000 | 9.323348 | 9.155634 | 0.167714 | 98.227180 | 25.911469 | 0.038593 | 5.769971 | 14.504566 | 3.385663 | -0.000092650 | YES | YES / YES |
| 1.280 | Final-pair-check / FCT-B8C0D490FA7A<br>0.894259036,0.369964361,0.446734667,0.211199999,0.007228971,0.772461057,2.801538229,3.057003498,0.985074520,0.083381474,1.621976376 | 21.452099 | 19.985252 | 1.466846 | 101.157089 | 17.209085 | 0.058109 | 15.695846 | 9.065117 | 4.289406 | 0.000299238 | YES | NO / YES |

### Phase energy and distance accounting: controls and winners

Drive means physical distance outside active correction, including zero-drive carry/coast. Positive-c energies are independently accumulated per physical subphase; correction + drive must equal total within 1e-7 J. Average force = phase work / phase-mode distance. Correction m/s are actual active-control distance/time, and positive-c mode metres partition those subphases across the entire path. At c=0 only the legacy mode counters use whole-interval classification; all work is zero.

| c | line | valid / reason | sector s | total J | correction J | drive J | split error J | correction mode m | outside mode m | correction m | correction s | avg correction N | avg outside N |
|---:|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.000 | ConstantInner | YES | 6.676829 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 45.694866 | 51.695156 | 45.095268 | 1.876382 | 0.000000 | 0.000000 |
| 0.000 | ConstantL1 | YES | 6.863337 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 35.428375 | 73.429436 | 34.537167 | 1.401044 | 0.000000 | 0.000000 |
| 0.000 | ConstantL2 | YES | 7.079330 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 24.161289 | 96.163113 | 23.979046 | 0.950147 | 0.000000 | 0.000000 |
| 0.000 | ConstantL3 | YES | 7.320224 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 13.964947 | 117.825703 | 13.420996 | 0.520274 | 0.000000 | 0.000000 |
| 0.000 | ConstantOuter | YES | 7.582354 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 2.875862 | 140.379189 | 2.862947 | 0.108729 | 0.000000 | 0.000000 |
| 0.000 | Flat-0 | YES | 6.676829 | 0.000000 | 0.000000 | 0.000000 | 0.000000000 | 45.694866 | 51.695156 | 45.095268 | 1.876382 | 0.000000 | 0.000000 |
| 0.001 | ConstantInner | YES | 6.680735 | 242.502000 | 119.553776 | 122.948224 | 0.000000000 | 44.737529 | 52.652493 | 44.737530 | 1.861709 | 2.672337 | 2.335088 |
| 0.001 | ConstantL1 | YES | 6.867498 | 266.151187 | 85.443484 | 180.707702 | 0.000000000 | 34.148409 | 74.709402 | 34.148411 | 1.385737 | 2.502122 | 2.418808 |
| 0.001 | ConstantL2 | YES | 7.083999 | 285.275947 | 55.735186 | 229.540761 | 0.000000000 | 23.571211 | 96.753190 | 23.571211 | 0.934462 | 2.364545 | 2.372436 |
| 0.001 | ConstantL3 | YES | 7.325512 | 301.045953 | 29.268326 | 271.777628 | 0.000000000 | 13.002187 | 118.788463 | 13.002187 | 0.504365 | 2.251031 | 2.287913 |
| 0.001 | ConstantOuter | YES | 7.588468 | 314.229523 | 5.261420 | 308.968103 | 0.000000000 | 2.440593 | 140.814458 | 2.440593 | 0.092759 | 2.155796 | 2.194150 |
| 0.001 | Flat-0 | YES | 6.680735 | 242.502000 | 119.553776 | 122.948224 | 0.000000000 | 44.737529 | 52.652493 | 44.737530 | 1.861709 | 2.672337 | 2.335088 |
| 0.005 | ConstantInner | YES | 6.696359 | 1209.255150 | 578.085008 | 631.170142 | -0.000000000 | 43.307014 | 54.083008 | 43.307014 | 1.803028 | 13.348531 | 11.670396 |
| 0.005 | ConstantL1 | YES | 6.884250 | 1326.511762 | 406.586161 | 919.925601 | 0.000000000 | 32.595105 | 76.262706 | 32.595104 | 1.324538 | 12.473841 | 12.062588 |
| 0.005 | ConstantL2 | YES | 7.102735 | 1420.682791 | 258.299308 | 1162.383483 | 0.000000000 | 21.943719 | 98.380683 | 21.943718 | 0.871770 | 11.770991 | 11.815160 |
| 0.005 | ConstantL3 | YES | 7.346948 | 1497.836886 | 126.907883 | 1370.929004 | -0.000000000 | 11.337328 | 120.453322 | 11.337328 | 0.440960 | 11.193809 | 11.381413 |
| 0.005 | ConstantOuter | YES | 7.613083 | 1562.011809 | 8.203738 | 1553.808071 | -0.000000000 | 0.765884 | 142.489167 | 0.765884 | 0.029200 | 10.711462 | 10.904745 |
| 0.005 | Low-phase-neutral | YES | 6.690235 | 1222.185768 | 550.207613 | 671.978155 | -0.000000000 | 40.553582 | 57.901496 | 40.553581 | 1.676647 | 13.567423 | 11.605541 |
| 0.020 | ConstantInner | YES | 6.755196 | 4788.777717 | 2017.760057 | 2771.017660 | -0.000000000 | 37.950512 | 59.439510 | 37.950512 | 1.583215 | 53.168190 | 46.619120 |
| 0.020 | ConstantL1 | YES | 6.947736 | 5241.960007 | 1320.698892 | 3921.261115 | 0.000000000 | 26.805748 | 82.052063 | 26.805748 | 1.095784 | 49.269242 | 47.789915 |
| 0.020 | ConstantL2 | YES | 7.174756 | 5594.645872 | 736.057946 | 4858.587926 | 0.000000000 | 15.925498 | 104.398904 | 15.925498 | 0.638347 | 46.218835 | 46.538687 |
| 0.020 | ConstantL3 | YES | 7.430383 | 5876.075174 | 229.417338 | 5646.657836 | 0.000000000 | 5.242250 | 126.548400 | 5.242249 | 0.206199 | 43.763146 | 44.620539 |
| 0.020 | ConstantOuter | YES | 7.783545 | 5999.879086 | 0.000000 | 5999.879086 | 0.000000000 | 0.000000 | 143.255051 | 0.000000 | 0.000000 | 0.000000 | 41.882496 |
| 0.020 | Final-pair-check | YES | 6.732460 | 4848.508281 | 1850.884324 | 2997.623957 | -0.000000000 | 33.839446 | 64.625848 | 33.839447 | 1.394769 | 54.696059 | 46.384288 |
| 0.080 | ConstantInner | YES | 6.994877 | 18410.742026 | 3430.505529 | 14980.236497 | -0.000000000 | 16.735095 | 80.654927 | 16.735096 | 0.710371 | 204.988706 | 185.732441 |
| 0.080 | ConstantL1 | YES | 7.225869 | 19788.937793 | 865.872885 | 18923.064908 | 0.000000000 | 4.839956 | 104.017855 | 4.839956 | 0.207245 | 178.900970 | 181.921315 |
| 0.080 | ConstantL2 | YES | 7.604131 | 20247.366953 | 0.000000 | 20247.366953 | 0.000000000 | 0.000000 | 120.324402 | 0.000000 | 0.000000 | 0.000000 | 168.273157 |
| 0.080 | ConstantL3 | YES | 8.065269 | 20299.511902 | 0.000000 | 20299.511902 | 0.000000000 | 0.000000 | 131.790649 | 0.000000 | 0.000000 | 0.000000 | 154.028469 |
| 0.080 | ConstantOuter | YES | 8.525609 | 20350.829981 | 0.000000 | 20350.829981 | 0.000000000 | 0.000000 | 143.255051 | 0.000000 | 0.000000 | 0.000000 | 142.060122 |
| 0.080 | Final-pair-check | YES | 6.904014 | 18598.533397 | 2078.572216 | 16519.961181 | 0.000000000 | 9.732182 | 89.190753 | 9.732183 | 0.400968 | 213.577198 | 185.220559 |
| 0.320 | ConstantInner | YES | 9.323348 | 45790.086187 | 0.000000 | 45790.086187 | 0.000000000 | 0.000000 | 97.390022 | 0.000000 | 0.000000 | 0.000000 | 470.172253 |
| 0.320 | ConstantL1 | YES | 9.913499 | 46099.117314 | 0.000000 | 46099.117314 | 0.000000000 | 0.000000 | 108.857811 | 0.000000 | 0.000000 | 0.000000 | 423.480106 |
| 0.320 | ConstantL2 | YES | 10.500729 | 46406.062949 | 0.000000 | 46406.062949 | 0.000000000 | 0.000000 | 120.324402 | 0.000000 | 0.000000 | 0.000000 | 385.674578 |
| 0.320 | ConstantL3 | YES | 11.085304 | 46710.975551 | 0.000000 | 46710.975551 | 0.000000000 | 0.000000 | 131.790649 | 0.000000 | 0.000000 | 0.000000 | 354.433154 |
| 0.320 | ConstantOuter | YES | 11.667233 | 47013.821740 | 0.000000 | 47013.821740 | 0.000000000 | 0.000000 | 143.255051 | 0.000000 | 0.000000 | 0.000000 | 328.182647 |
| 0.320 | Final-pair-check | YES | 9.155634 | 44503.164169 | 0.000000 | 44503.164169 | 0.000000000 | 0.000000 | 98.227180 | 0.000000 | 0.000000 | 0.000000 | 453.063642 |
| 1.280 | ConstantInner | YES | 21.452099 | 56761.564118 | 0.000000 | 56761.564118 | 0.000000000 | 0.000000 | 97.390022 | 0.000000 | 0.000000 | 0.000000 | 582.827304 |
| 1.280 | ConstantL1 | YES | 22.932623 | 57572.139370 | 0.000000 | 57572.139370 | 0.000000000 | 0.000000 | 108.857811 | 0.000000 | 0.000000 | 0.000000 | 528.874675 |
| 1.280 | ConstantL2 | YES | 24.390408 | 58379.286981 | 0.000000 | 58379.286981 | 0.000000000 | 0.000000 | 120.324402 | 0.000000 | 0.000000 | 0.000000 | 485.182441 |
| 1.280 | ConstantL3 | YES | 25.828985 | 59182.751775 | 0.000000 | 59182.751775 | 0.000000000 | 0.000000 | 131.790649 | 0.000000 | 0.000000 | 0.000000 | 449.066395 |
| 1.280 | ConstantOuter | YES | 27.249607 | 59982.796109 | 0.000000 | 59982.796109 | 0.000000000 | 0.000000 | 143.255051 | 0.000000 | 0.000000 | 0.000000 | 418.713308 |
| 1.280 | Final-pair-check | YES | 19.985252 | 54370.571880 | 6683.090197 | 47687.481684 | 0.000000000 | 1.863790 | 99.293299 | 1.863790 | 0.073301 | 3585.753183 | 480.268880 |

### Every winner versus ConstantInner: integrated trade-off

Values are winner / inner at the same coefficient, not evidence of a uniformly wider line. Integrated demand uses midpoint speed and |κ| over physical ds; time-weighted demand and radius use actual interval time.

| c | min R m | max κ 1/m | mean abs κ 1/m | time-weighted R m | time-weighted a_lat m/s² | integral v²abs(κ) ds m²/s² | total work J | drive work J | exit m/s | extra path m | lower demand / drive work / total work / better exit / longer path |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|:---:|
| 0.000 | 31.000000 / 31.000000 | 0.032258 / 0.032258 | 0.032258 / 0.032258 | 31.000000 / 31.000000 | 17.401576 / 17.401576 | 1708.929064 / 1708.929064 | 0.000000 / 0.000000 | 0.000000 / 0.000000 | 23.405930 / 23.405930 | 0.000000 | NO / NO / NO / NO / NO |
| 0.001 | 31.000000 / 31.000000 | 0.032258 / 0.032258 | 0.032258 / 0.032258 | 31.000000 / 31.000000 | 17.389381 / 17.389381 | 1707.804124 / 1707.804124 | 242.502000 / 242.502000 | 122.948224 / 122.948224 | 23.376135 / 23.376135 | 0.000000 | NO / NO / NO / NO / NO |
| 0.005 | 30.295263 / 31.000000 | 0.033008 / 0.032258 | 0.031779 / 0.032258 | 31.505770 / 31.000000 | 17.349001 / 17.340662 | 1721.453893 / 1703.320938 | 1222.185768 / 1209.255150 | 671.978155 / 631.170142 | 23.507738 / 23.257521 | 1.065056 | NO / NO / NO / YES / YES |
| 0.020 | 30.298119 / 31.000000 | 0.033005 / 0.032258 | 0.031759 / 0.032258 | 31.526976 / 31.000000 | 17.213035 / 17.157958 | 1707.552480 / 1686.655245 | 4848.508281 / 4788.777717 | 2997.623957 / 2771.017660 | 23.170862 / 22.819618 | 1.075272 | NO / NO / NO / YES / YES |
| 0.080 | 30.521454 / 31.000000 | 0.032764 / 0.032258 | 0.030779 / 0.032258 | 32.636990 / 31.000000 | 16.372401 / 16.421934 | 1637.532914 / 1621.449043 | 18598.533397 / 18410.742026 | 16519.961181 / 14980.236497 | 21.872946 / 21.170938 | 1.532913 | YES / NO / NO / YES / YES |
| 0.320 | 25.911469 / 31.000000 | 0.038593 / 0.032258 | 0.029991 / 0.032258 | 34.465206 / 31.000000 | 9.091865 / 9.381151 | 979.421406 / 1007.790185 | 44503.164169 / 45790.086187 | 44503.164169 / 45790.086187 | 14.504566 / 13.845848 | 0.837158 | YES / YES / YES / YES / YES |
| 1.280 | 17.209085 / 31.000000 | 0.058109 / 0.032258 | 0.028062 / 0.032258 | 143.999664 / 31.000000 | 1.529191 / 1.540860 | 299.487183 / 312.754365 | 54370.571880 / 56761.564118 | 47687.481684 / 56761.564118 | 9.065117 / 7.164661 | 3.767067 | YES / YES / YES / YES / YES |

### LateralExecutionBoundarySensitive diagnostic

The unchanged YES flag means minimum numerical interval headroom ≤ 0.01 m. It is a mesh-dependent classification, not a calibrated physical margin. No centimetre-scale lateral-reserve replays are run in this repair; no old reserve results are retained. The requested physical outward/amplitude probes around the fresh c=.005 winner are reported below.

### Search closure and repeated-lap diagnostics

| c | starts | refined | top-3 closed | independent near starts | single residual s | pair residual s | objective | geometry | valid / evaluated | longitudinal stalls | repeat converged | max v m/s | avg v m/s | lap s | guardrail V/A/L |
|---:|---:|---:|:---:|---:|---:|---:|:---:|:---:|---:|---:|:---:|---:|---:|---:|:---:|
| 0.000 | 106 | 48 | YES | 5 | 0.000000 | 0.000000 | YES | YES | 5754 / 9196 | 0 | YES | 26.515173 | 23.872900 | 13.353217 | NO/NO/NO |
| 0.001 | 106 | 48 | YES | 5 | 0.000000 | 0.000000 | YES | YES | 5762 / 9199 | 0 | YES | 26.496536 | 23.858589 | 13.361227 | NO/NO/NO |
| 0.005 | 106 | 48 | YES | 5 | 0.000000 | 0.000000 | YES | YES | 5894 / 9278 | 0 | YES | 26.578968 | 23.984497 | 13.379902 | NO/NO/NO |
| 0.020 | 106 | 48 | YES | 1 | 0.000990 | 0.000000 | NO | YES | 5946 / 9291 | 0 | YES | 26.368515 | 23.832876 | 13.465888 | NO/NO/NO |
| 0.080 | 106 | 48 | YES | 2 | 0.000000 | 0.000000 | NO | YES | 6992 / 11017 | 2 | YES | 25.574823 | 23.200150 | 13.873110 | NO/NO/NO |
| 0.320 | 106 | 48 | YES | 9 | 0.000708 | 0.000000 | YES | YES | 5791 / 11015 | 3 | YES | 21.080799 | 15.146408 | 21.157211 | NO/NO/YES |
| 1.280 | 106 | 48 | YES | 1 | 0.000834 | 0.000034 | NO | YES | 8184 / 15271 | 66 | YES | 19.399527 | 7.062599 | 46.204338 | NO/NO/YES |

The unchanged descriptive ±15% guardrails reference maximum speed 111 km/h, average speed 22.38 m/s and flying lap 14.71 s. They are not optimization targets. Objective convergence and geometry convergence are separate; no conclusion is promoted from an unclosed search.

### Candidate validity counts by reason

| c | evaluated | valid | TrackBoundary | SelfIntersection | NonSmoothGeometry | LateralExecutionConstraint | RunWide | Crash | StraightRepositionConstraint | NonTraversable | initially NonTraversable |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.000 | 9196 | 5754 | 1223 | 0 | 0 | 1050 | 1074 | 93 | 2 | 0 | 0 |
| 0.001 | 9199 | 5762 | 1224 | 0 | 0 | 1048 | 1070 | 93 | 2 | 0 | 0 |
| 0.005 | 9278 | 5894 | 1213 | 0 | 0 | 1058 | 1018 | 93 | 2 | 0 | 0 |
| 0.020 | 9291 | 5946 | 1194 | 0 | 0 | 1178 | 878 | 93 | 2 | 0 | 0 |
| 0.080 | 11017 | 6992 | 1188 | 0 | 0 | 1922 | 820 | 91 | 2 | 2 | 0 |
| 0.320 | 11015 | 5791 | 1074 | 0 | 0 | 3075 | 971 | 99 | 2 | 3 | 0 |
| 1.280 | 15271 | 8184 | 2134 | 0 | 0 | 2523 | 2181 | 181 | 2 | 66 | 0 |

Strong dissipation can legitimately invalidate high-c trajectories. NonTraversable counts must be read alongside closure and Motoarena guardrails: ObjectiveConvergence=NO is not a verified optimum, and broken global guardrails are not calibration evidence.

## Interpretation after repair

At c = 0.005 the fresh search finds a non-inner candidate paying turning loss through the whole corner, with #47 partial-correction topology. Its integrated trade-offs and numerical lateral-boundary dependence are reported above; controller continuity does not automatically validate the physical model.

First objectively converged sampled non-inner advantage: c = 0.005; gain 0.006124 s. This is a sampled result, not a fitted continuous threshold.

Any surviving non-inner result must be judged by the full-corner energy/demand comparison, exit speed, extra path and lateral-boundary diagnostic, not by the name 'wider'. No controller state receives free dissipation. Large c remains an uncalibrated effective-slip surrogate; c = 1.280 is far outside a small-angle interpretation. Unclosed searches and broken global guardrails remain explicit uncertainty. No production promotion follows; a coupled longitudinal/lateral tyre-force envelope is a separate future hypothesis, not part of this repair.

## Fixed-geometry zero-limit validation (repair after reviewed HEAD d9eebbf49d3c6bb2bc9f8f5cc1e14522bcc08db2)

These are fixed ConstantInner and the old reviewed c=.005-winner controls, not the fresh search winner. The seven coefficients include c=1e-8 and 1e-7 to resolve a finite offset. The two-point sector intercept uses those two smallest positive coefficients; its bound is two actual output-float ULPs, not a gameplay-scale time tolerance. Float quantization can make individual deltas or delta/c nonmonotonic; neither a slope fit nor an offset is applied to the simulation.

| geometry | c | sector s | delta vs c=0 s | delta/c s | corner s | exit m/s | correction m | correction s | work J |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| ConstantInner | 0.00000000 | 6.676829338 | 0.000000000 | — | 4.201726913 | 23.405929565 | 45.095268250 | 1.876382470 | 0.000000000 |
| ConstantInner | 0.00000001 | 6.676829338 | 0.000000000 | 0.000000 | 4.201726913 | 23.405929565 | 45.095268250 | 1.876382470 | 0.002426655 |
| ConstantInner | 0.00000010 | 6.676829338 | 0.000000000 | 0.000000 | 4.201726913 | 23.405925751 | 45.095264435 | 1.876382470 | 0.024266548 |
| ConstantInner | 0.00000100 | 6.676836014 | 0.000006676 | 6.675720 | 4.201729774 | 23.405885696 | 45.094940186 | 1.876369238 | 0.242665185 |
| ConstantInner | 0.00001000 | 6.676870346 | 0.000041008 | 4.100800 | 4.201743603 | 23.405618668 | 45.091735840 | 1.876237750 | 2.426637270 |
| ConstantInner | 0.00010000 | 6.677220345 | 0.000391006 | 3.910065 | 4.201881886 | 23.402944565 | 45.059524536 | 1.874916673 | 24.264905388 |
| ConstantInner | 0.00100000 | 6.680734634 | 0.003905296 | 3.905296 | 4.203272820 | 23.376134872 | 44.737529755 | 1.861709356 | 242.502000098 |
| reviewed c=.005 winner geometry | 0.00000000 | 6.683063030 | 0.000000000 | — | 4.219650269 | 23.554262161 | 42.368873596 | 1.751068354 | 0.000000000 |
| reviewed c=.005 winner geometry | 0.00000001 | 6.683063030 | 0.000000000 | 0.000000 | 4.219650269 | 23.554262161 | 42.368869781 | 1.751068234 | 0.002447966 |
| reviewed c=.005 winner geometry | 0.00000010 | 6.683063030 | 0.000000000 | 0.000000 | 4.219650269 | 23.554262161 | 42.368705750 | 1.751061201 | 0.024479661 |
| reviewed c=.005 winner geometry | 0.00000100 | 6.683061600 | -0.000001431 | -1.430511 | 4.219647408 | 23.554248810 | 42.369525909 | 1.751098394 | 0.244796882 |
| reviewed c=.005 winner geometry | 0.00001000 | 6.683066368 | 0.000003338 | 0.333786 | 4.219650269 | 23.554216385 | 42.364311218 | 1.750877380 | 2.447963073 |
| reviewed c=.005 winner geometry | 0.00010000 | 6.683178902 | 0.000115871 | 1.158714 | 4.219693661 | 23.553337097 | 42.334548950 | 1.749661207 | 24.479197364 |
| reviewed c=.005 winner geometry | 0.00100000 | 6.683729649 | 0.000666618 | 0.666618 | 4.220256805 | 23.553501129 | 42.002933502 | 1.736059546 | 244.734480204 |

ConstantInner: output ULP 0.000000476837158 s; estimated sector intercept 0.000000000000000 s = 0.000000 ULPs; zero-limit diagnostic PASS within ±2 output ULPs.
reviewed c=.005 winner geometry: output ULP 0.000000476837158 s; estimated sector intercept 0.000000000000000 s = 0.000000 ULPs; zero-limit diagnostic PASS within ±2 output ULPs.

No resolved finite sector-time intercept in either fixed probe. At c=1e-8 the regression also requires sector/corner time, exit speed, actual correction distance/time and every sampled profile speed within two respective float ULPs of #47, with identical profile controller flags/outcomes. Work remains positive and tends to zero. This is a resolved numerical limit check, not a proof below float resolution.

The positive-c integrator now preserves partial correction, target crossing and remaining dissipative carry. The force law and exact c=0 #47 branch are frozen. All seven full searches are rerun with unchanged starts, refinement and closure tolerances. No coefficient threshold, fitted bridge, smoothing or production change is used.

## Physical track-space robustness at c=.005

Reference FCT-AE7832278759, sector 6.690234661 s; ConstantInner 6.696358681 s. In the complete experiment this reference is the freshly searched c=.005 winner, not the old reviewed controls. Whole-line shift adds the same metres to all eleven controls; positive is outward. Amplitude multiplies all offsets relative to ConstantInner (zero). Maximum displacement samples the full spline at 4097 equal-angle progress positions, comparing radial offsets at the same track angle, not just controls or percentage of arc length. This is a sampled maximum, not an analytical extremum. The reference occupies 0.041899353–0.626892686 m from the inside edge.

Invalid variants retain their original controls/reasons; — means no valid trajectory/performance metric. No invalid path is clamped or assigned a zero time. Only the requested outward shifts and coherent amplitudes are tested; impossible inward shifts and centimetre-scale reserve diagnostics are not rerun.

| mode / parameter | controls m | validity / reason | max displacement m | sector s | delta winner s | delta inner s | corner s | exit m/s | path m | min R m | max κ 1/m | mean abs κ 1/m | total J | correction J | outside correction J |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| shift 0.000000 m | 0.522111535,0.617457867,0.604532838,0.488273501,0.313086927,0.145888388,0.050542116,0.063467115,0.179726511,0.354912996,0.584611535 | Valid | 0.000000000 | 6.690234661 | 0.000000000 | -0.006124020 | 4.223165512 | 23.507738113 | 98.455078125 | 30.295263290 | 0.033008460 | 0.031778995 | 1222.185767964 | 550.207613277 | 671.978154686 |
| shift +0.100000 m | 0.622111559,0.717457891,0.704532862,0.588273525,0.413086921,0.245888382,0.150542110,0.163467109,0.279726505,0.454912990,0.684611559 | Valid | 0.100000083 | 6.694944859 | 0.004710197 | -0.001413822 | 4.230289459 | 23.538442612 | 98.769126892 | 30.395090103 | 0.032900050 | 0.031678345 | 1225.843878671 | 545.095475293 | 680.748403378 |
| shift +0.250000 m | 0.772111535,0.867457867,0.854532838,0.738273501,0.563086927,0.395888388,0.300542116,0.313467115,0.429726511,0.604912996,0.834611535 | Valid | 0.250000030 | 6.702037811 | 0.011803150 | 0.005679131 | 4.241025925 | 23.584871292 | 99.240295410 | 30.544830322 | 0.032738764 | 0.031528559 | 1231.270586914 | 537.544553636 | 693.726033278 |
| shift +0.500000 m | 1.022111535,1.117457867,1.104532838,0.988273501,0.813086927,0.645888388,0.550542116,0.563467145,0.679726481,0.854912996,1.084611535 | Valid | 0.500000060 | 6.713634491 | 0.023399830 | 0.017275810 | 4.258870602 | 23.664758682 | 100.025688171 | 30.794410706 | 0.032473426 | 0.031282034 | 1240.284582306 | 525.025628023 | 715.258954283 |
| shift +1.000000 m | 1.522111535,1.617457867,1.604532838,1.488273501,1.313086987,1.145888329,1.050542116,1.063467145,1.179726481,1.354912996,1.584611535 | Valid | 1.000000119 | 6.737822533 | 0.047587872 | 0.041463852 | 4.295037270 | 23.818876266 | 101.596221924 | 31.293577194 | 0.031955440 | 0.030800374 | 1257.791493379 | 500.205663265 | 757.585830114 |
| amplitude 75.000000% | 0.391583651,0.463093400,0.453399628,0.366205126,0.234815195,0.109416291,0.037906587,0.047600336,0.134794891,0.266184747,0.438458651 | Valid | 0.156723142 | 6.690232277 | -0.000002384 | -0.006126404 | 4.219168186 | 23.457035065 | 98.186851501 | 30.461175919 | 0.032828674 | 0.031898558 | 1219.238200651 | 557.352037739 | 661.886162912 |
| amplitude 89.999998% | 0.469900370,0.555712044,0.544079542,0.439446151,0.281778216,0.131299540,0.045487903,0.057120401,0.161753863,0.319421679,0.526150346 | Valid | 0.062689245 | 6.689832687 | -0.000401974 | -0.006525993 | 4.221425533 | 23.490749359 | 98.347656250 | 30.360849380 | 0.032937154 | 0.031826805 | 1221.080874279 | 553.034768358 | 668.046105921 |
| amplitude 100.000000% | 0.522111535,0.617457867,0.604532838,0.488273501,0.313086927,0.145888388,0.050542116,0.063467115,0.179726511,0.354912996,0.584611535 | Valid | 0.000000000 | 6.690234661 | 0.000000000 | -0.006124020 | 4.223165512 | 23.507738113 | 98.455078125 | 30.295263290 | 0.033008460 | 0.031778995 | 1222.185767964 | 550.207613277 | 671.978154686 |
| amplitude 110.000002% | 0.574322701,0.679203689,0.664986134,0.537100852,0.344395638,0.160477236,0.055596329,0.069813825,0.197699159,0.390404314,0.643072724 | Valid | 0.062689304 | 6.690694809 | 0.000460148 | -0.005663872 | 4.224927425 | 23.524293900 | 98.562622070 | 30.230686188 | 0.033078972 | 0.031731214 | 1223.279765117 | 547.210655746 | 676.069109371 |
| amplitude 125.000000% | 0.652639389,0.771822333,0.755666018,0.610341907,0.391358674,0.182360485,0.063177645,0.079333894,0.224658132,0.443641245,0.730764389 | Valid | 0.156723201 | 6.691336632 | 0.001101971 | -0.005022049 | 4.227569103 | 23.549753189 | 98.724479675 | 30.135684967 | 0.033183251 | 0.031659592 | 1224.935726069 | 542.792815201 | 682.142910868 |

### Separate local coherent-amplitude refinement

The best fixed amplitude probe (89.999998%) improves on the reference by more than two sector-output ULPs, so a separate one-parameter refinement starts there. Eight levels test only amplitude ±0.05, halving the step each level; retain the best valid sector time with lower-amplitude tie-breaking. 17 recorded evaluations including the seed; no 106-start searches or independent control perturbations. Best tested amplitude 83.593750%: sector 6.689545631 s, delta reference -0.000689030 s, displacement 0.102849543 m. This is local one-dimensional evidence, not a replacement globally converged winner.

| mode / parameter | controls m | validity / reason | max displacement m | sector s | delta winner s | delta inner s | corner s | exit m/s | path m | min R m | max κ 1/m | mean abs κ 1/m | total J | correction J | outside correction J |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| amplitude 89.999998% | 0.469900370,0.555712044,0.544079542,0.439446151,0.281778216,0.131299540,0.045487903,0.057120401,0.161753863,0.319421679,0.526150346 | Valid | 0.062689245 | 6.689832687 | -0.000401974 | -0.006525993 | 4.221425533 | 23.490749359 | 98.347656250 | 30.360849380 | 0.032937154 | 0.031826805 | 1221.080874279 | 553.034768358 | 668.046105921 |
| amplitude 84.999996% | 0.443794787,0.524839163,0.513852894,0.415032446,0.266123891,0.124005124,0.042960797,0.053947046,0.152767524,0.301676035,0.496919781 | Valid | 0.094033897 | 6.689702034 | -0.000532627 | -0.006656647 | 4.220579624 | 23.481670380 | 98.293922424 | 30.394029617 | 0.032901198 | 0.031850718 | 1220.513647264 | 554.469638030 | 666.044009234 |
| amplitude 94.999999% | 0.496005952,0.586584985,0.574306190,0.463859826,0.297432572,0.138593972,0.048015010,0.060293760,0.170740187,0.337167352,0.555380940 | Valid | 0.031344593 | 6.690020561 | -0.000214100 | -0.006338120 | 4.222290039 | 23.499334335 | 98.401321411 | 30.327930450 | 0.032972906 | 0.031802896 | 1221.634997201 | 551.611689157 | 670.023308044 |
| amplitude 82.499999% | 0.430741996,0.509402752,0.498739570,0.402825624,0.258296698,0.120357916,0.041697245,0.052360371,0.148274377,0.292803228,0.482304513 | Valid | 0.109706223 | 6.689639568 | -0.000595093 | -0.006719112 | 4.220160961 | 23.477148056 | 98.267143250 | 30.410720825 | 0.032883141 | 0.031862676 | 1220.230336864 | 555.200349225 | 665.029987640 |
| amplitude 87.499994% | 0.456847548,0.540275574,0.528966188,0.427239299,0.273951054,0.127652332,0.044224348,0.055533722,0.157260686,0.310548842,0.511535048 | Valid | 0.078361630 | 6.689770222 | -0.000464439 | -0.006588459 | 4.221005440 | 23.486206055 | 98.320846558 | 30.377414703 | 0.032919195 | 0.031838760 | 1220.797909261 | 553.762080536 | 667.035828725 |
| amplitude 81.250000% | 0.424215615,0.501684546,0.491182923,0.396722227,0.254383117,0.118534312,0.041065469,0.051567033,0.146027789,0.288366795,0.474996865 | Valid | 0.117542326 | 6.689737320 | -0.000497341 | -0.006621361 | 4.219997406 | 23.473829269 | 98.253822327 | 30.419086456 | 0.032874096 | 0.031868655 | 1220.066291907 | 555.562152958 | 664.504138950 |
| amplitude 83.749998% | 0.437268406,0.517120957,0.506296217,0.408929050,0.262210280,0.122181520,0.042329021,0.053153709,0.150520951,0.297239631,0.489612132 | Valid | 0.101870060 | 6.689550400 | -0.000684261 | -0.006808281 | 4.220327377 | 23.480390549 | 98.280441284 | 30.402364731 | 0.032892179 | 0.031856697 | 1220.392128393 | 554.839650055 | 665.552478337 |
| amplitude 83.124995% | 0.434005201,0.513261795,0.502517879,0.405877322,0.260253489,0.121269718,0.042013131,0.052757036,0.149397656,0.295021415,0.485958308 | Valid | 0.105788171 | 6.689587116 | -0.000647545 | -0.006771564 | 4.220242500 | 23.478849411 | 98.273834229 | 30.406538010 | 0.032887664 | 0.031859685 | 1220.313648381 | 555.013332818 | 665.300315563 |
| amplitude 84.375000% | 0.440531611,0.520980060,0.510074556,0.411980778,0.264167100,0.123093329,0.042644911,0.053550377,0.151644245,0.299457848,0.493265986 | Valid | 0.097952008 | 6.689748764 | -0.000485897 | -0.006609917 | 4.220496178 | 23.480010986 | 98.287239075 | 30.398195267 | 0.032896690 | 0.031853706 | 1220.431529646 | 554.646901554 | 665.784628092 |
| amplitude 83.437496% | 0.435636789,0.515191376,0.504407048,0.407403171,0.261231899,0.121725619,0.042171076,0.052955370,0.149959296,0.296130508,0.487785220 | Valid | 0.103829086 | 6.689562798 | -0.000671864 | -0.006795883 | 4.220285892 | 23.479705811 | 98.277236938 | 30.404455185 | 0.032889917 | 0.031858191 | 1220.355974722 | 554.927308632 | 665.428666090 |
| amplitude 84.062499% | 0.438899994,0.519050539,0.508185387,0.410454899,0.263188690,0.122637421,0.042486966,0.053352043,0.151082590,0.298348725,0.491439074 | Valid | 0.099910975 | 6.689762115 | -0.000472546 | -0.006596565 | 4.220449448 | 23.479251862 | 98.283813477 | 30.400278091 | 0.032894436 | 0.031855199 | 1220.390479727 | 554.735048499 | 665.655431228 |
| amplitude 83.593750% | 0.436452597,0.516156197,0.505351663,0.408166140,0.261721104,0.121953577,0.042250052,0.053054541,0.150240123,0.296685070,0.488698691 | Valid | 0.102849543 | 6.689545631 | -0.000689030 | -0.006813049 | 4.220302582 | 23.480134964 | 98.278831482 | 30.403408051 | 0.032891050 | 0.031857442 | 1220.376048068 | 554.878501494 | 665.497546575 |
| amplitude 83.906245% | 0.438084185,0.518085718,0.507240832,0.409691960,0.262699485,0.122409470,0.042407993,0.053252872,0.150801763,0.297794163,0.490525573 | Valid | 0.100890517 | 6.689779758 | -0.000454903 | -0.006578922 | 4.220432281 | 23.478816986 | 98.282211304 | 30.401325226 | 0.032893304 | 0.031855948 | 1220.370258072 | 554.779881758 | 665.590376314 |
| amplitude 83.515626% | 0.436044723,0.515673816,0.504879355,0.407784671,0.261476517,0.121839598,0.042210564,0.053004958,0.150099725,0.296407819,0.488241971 | Valid | 0.103339314 | 6.689553261 | -0.000681400 | -0.006805420 | 4.220292091 | 23.479909897 | 98.277976990 | 30.403930664 | 0.032890484 | 0.031857818 | 1220.365150185 | 554.902448571 | 665.462701614 |
| amplitude 83.671874% | 0.436860502,0.516638577,0.505823970,0.408547580,0.261965692,0.122067548,0.042289536,0.053104125,0.150380537,0.296962351,0.489155412 | Valid | 0.102359772 | 6.689550400 | -0.000684261 | -0.006808281 | 4.220315456 | 23.480237961 | 98.279617310 | 30.402883530 | 0.032891616 | 0.031857070 | 1220.383232233 | 554.853264101 | 665.529968131 |
| amplitude 83.554685% | 0.436248660,0.515914977,0.505115509,0.407975376,0.261598796,0.121896580,0.042230304,0.053029750,0.150169924,0.296546429,0.488470316 | Valid | 0.103094459 | 6.689555168 | -0.000679493 | -0.006803513 | 4.220303059 | 23.480016708 | 98.278526306 | 30.403669357 | 0.032890767 | 0.031857628 | 1220.371947091 | 554.891231247 | 665.480715844 |
| amplitude 83.632815% | 0.436656564,0.516397417,0.505587816,0.408356875,0.261843413,0.122010566,0.042269796,0.053079333,0.150310338,0.296823740,0.488927096 | Valid | 0.102604628 | 6.689554214 | -0.000680447 | -0.006804466 | 4.220312119 | 23.480150223 | 98.279266357 | 30.403146744 | 0.032891333 | 0.031857256 | 1220.379299139 | 554.867961812 | 665.511337328 |

### Tested-only width and interpretation

Within +0.005 s of the reference: tested valid whole-line shifts 0.00 to 0.10 m. Only discrete tested points qualify; no claim that every intermediate shift qualifies or that the threshold boundary was located.
Within +0.010 s of the reference: tested valid whole-line shifts 0.00 to 0.10 m. Only discrete tested points qualify; no claim that every intermediate shift qualifies or that the threshold boundary was located.
Within +0.020 s of the reference: tested valid whole-line shifts 0.00 to 0.25 m. Only discrete tested points qualify; no claim that every intermediate shift qualifies or that the threshold boundary was located.

Outward shift / measured delta: +0.10 m / 4.710197 ms; +0.25 m / 11.803150 ms; +0.50 m / 23.399830 ms; +1.00 m / 47.587872 ms. These one-sided discrete probes do not establish a two-sided basin or a continuous width boundary.
Valid coherent amplitudes: 5 of 5; maximum absolute delta 1.101971 ms; maximum sampled physical displacement 0.156723201 m. This direction is distinct from whole-line translation; percentages are not physical metres.

The repaired zero-limit check and fresh search/width results are separate evidence. Passing continuity removes the controller-state discontinuity; it does not calibrate the effective slip coefficient, certify a globally optimal geometry, or justify production promotion. Any unclosed sweep coefficient remains explicitly unresolved.
