# Corner reduced-drive resistance experiment (#42)

Base main SHA: `c1aae7e54e5e70a037b6202787246d3348afbe2b` (`Test straight drive-envelope shape (#41)`).
Diagnostic experiment only. No candidate is a production default; default/null options remain post-#41 production.

## A. Hypothesis

#41 classified the straight-only menu as `StraightEnvelopeShapeInsufficient`: Vmax increased while the already-fast flying lap became shorter. #42 tests whether the continuous-corner model loses too little energy because reduced `DriveAvailability` currently scales both available drive and existing longitudinal resistance.
The experiment does not assume the current equation is a bug and does not fit a candidate to the PGE median.

## B. Current production force semantics

Reviewed production uses `availability × (F_drive - F_resistance) / mass`. At availability zero, net acceleration is exactly zero. R0 and a missing experiment option preserve this operation order and its float results.

## C. Alternative force decomposition

Candidates use `F_drive_available = availability × F_drive` and `F_resistance_exposed = lerp(availability × F_resistance, F_resistance, exposure)`, then `F_net = F_drive_available - F_resistance_exposed`. The resistance itself remains exactly `40 + 0.20 × v²` N and mass remains 142 kg. No slide drag, tyre scrub, banking, lean-angle term, engine braking or wheel-slip model is added.

## D. Candidate definitions

| Candidate | ReducedDriveResistanceExposure | Status |
| --- | --- | --- |
| R0 | 0 | production baseline |
| R25 | 0.25 | calibration-only diagnostic |
| R50 | 0.5 | calibration-only diagnostic |
| R75 | 0.75 | calibration-only diagnostic |
| R100 | 1 | calibration-only diagnostic |


## E. Proof that R0 equals production

| Invariant | Result |
| --- | --- |
| default production trace exact | YES |
| R0 trace exact production | YES |
| R0 force identity | YES |
| availability=1 exact for R0…R100 | YES |
| envelope exact for R0…R100 | YES |
| correction formula/result exact | YES |


## F. Motoarena L1 baseline

Fixture: Motoarena 2026 symmetric modeled turn width 16.6 m, 31/31 m home-straight split, Dry, baseline surface, incidents off, neutral setup, all skills 50, HoldLane, seed 390039 and fixed normalized L1 rider observation. L1 is a controlled distance reference, not a real trajectory claim.
| Candidate | modeled 4-lap distance m | Vmax km/h | Flying median s | Heat s | Average m/s | entry m/s | true apex p=.50 m/s | exit m/s | peak→true-apex m/s | minimum m/s | minimum p | corner L2 s | straight L2 s | B/RW/C |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 1366.849609 | 97.882807 | 13.909906 | 57.171257 | 23.907986 | 27.098434 | 22.585236 | 24.296925 | 4.558926 | 22.542242 | 0.509186 | 9.004429 | 4.810934 | 0/0/0 |
| R25 | 1366.849609 | 97.52113 | 13.966417 | 57.415943 | 23.806099 | 27.003799 | 22.585236 | 24.149593 | 4.464027 | 22.485674 | 0.582678 | 9.037148 | 4.833316 | 0/0/0 |
| R50 | 1366.849609 | 97.03905 | 14.025337 | 57.735416 | 23.67437 | 26.91007 | 22.585234 | 24.003204 | 4.370058 | 22.383512 | 0.619424 | 9.072334 | 4.855732 | 0/0/0 |
| R75 | 1366.849609 | 96.704057 | 14.087852 | 58.116192 | 23.519257 | 26.817263 | 22.585234 | 23.857765 | 4.277004 | 22.261019 | 0.637796 | 9.111329 | 4.878174 | 0/0/0 |
| R100 | 1366.849609 | 96.296828 | 14.156315 | 58.539989 | 23.348989 | 26.714882 | 22.585234 | 23.713253 | 4.163885 | 22.127251 | 0.646983 | 9.157445 | 4.901711 | 0/0/0 |

True apex is the mean of the two logical-corner production profile-grid speeds at canonical `p = 0.50`. Corner minimum is an independent raw production-node observation and may occur after the apex.
| Candidate | true apex m/s | minimum m/s | minimum corner | minimum p | minimum−apex m/s | C1 minimum/p | C2 minimum/p | peak→minimum m/s |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 22.585236 | 22.542242 | 2 | 0.509186 | -0.042994 | 22.597519 / 0.509186 | 22.542242 / 0.509186 | 4.601919 |
| R25 | 22.585236 | 22.485674 | 2 | 0.582678 | -0.099562 | 22.540764 / 0.582678 | 22.485674 / 0.582678 | 4.563589 |
| R50 | 22.585234 | 22.383512 | 2 | 0.619424 | -0.201721 | 22.438263 / 0.619424 | 22.383512 / 0.619424 | 4.571779 |
| R75 | 22.585234 | 22.261019 | 2 | 0.637796 | -0.324215 | 22.315458 / 0.637796 | 22.261019 / 0.637796 | 4.601219 |
| R100 | 22.585234 | 22.127251 | 2 | 0.646983 | -0.457983 | 22.181412 / 0.656169 | 22.127251 / 0.646983 | 4.621868 |

Real context only: Vmax P10/P50/P90 = 108.6/113.7/116.9 km/h; Flying P10/P50/P90 = 14.56/14.87/15.15 s. Skill50 is not real P50.

## G. Flying-lap time budget

The budget uses the complete second modeled lap (L2). The three straight pieces and both logical corners are taken from actual production sample durations. `Flying L2` is one modeled lap and is deliberately separate from the three-lap `Flying median` reported in section F; they are not required to be equal.
| Candidate | home start s | back s | home finish s | Straight total s | C1 entry→apex s | C1 apex→exit s | C1 total s | C2 entry→apex s | C2 apex→exit s | C2 total s | Corner total s | Flying L2 s | reconciles |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 1.167139 | 2.406477 | 1.237318 | 4.810934 | 2.152479 | 2.344374 | 4.496853 | 2.157828 | 2.349748 | 4.507576 | 9.004429 | 13.815361 | YES |
| R25 | 1.171805 | 2.417665 | 1.243845 | 4.833316 | 2.15516 | 2.357952 | 4.513112 | 2.160666 | 2.36337 | 4.524037 | 9.037148 | 13.870463 | YES |
| R50 | 1.176468 | 2.428873 | 1.250391 | 4.855732 | 2.158968 | 2.371648 | 4.530616 | 2.164606 | 2.377112 | 4.541718 | 9.072334 | 13.928066 | YES |
| R75 | 1.181126 | 2.440092 | 1.256956 | 4.878174 | 2.164473 | 2.385464 | 4.549937 | 2.170417 | 2.390975 | 4.561392 | 9.111329 | 13.989504 | YES |
| R100 | 1.18684 | 2.451326 | 1.263544 | 4.901711 | 2.173861 | 2.399404 | 4.573265 | 2.179219 | 2.404961 | 4.58418 | 9.157445 | 14.059156 | YES |


## H. Actual corner speed profile

Canonical apex means only `p = 0.50`; it is not an alias for the lowest speed anywhere in a corner. Minimum speed and its raw-node location are reported separately in section F.
| candidate | corner | p | actual m/s | actual km/h | envelope m/s | drive availability | phase |
| --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 1 | 0 | 27.144161 | 97.71898 | 28.123388 | 0 | carry/passive |
| R0 | 1 | 0.125 | 26.851614 | 96.66581 | 26.851614 | 0 | correction |
| R0 | 1 | 0.25 | 25.516584 | 91.859704 | 25.516582 | 0 | correction |
| R0 | 1 | 0.375 | 24.10807 | 86.789053 | 24.093657 | 0 | correction |
| R0 | 1 | 0.5 | 22.612858 | 81.406288 | 22.597488 | 0 | correction |
| R0 | 1 | 0.625 | 22.681591 | 81.653728 | 22.681591 | 0.316544 | signed-drive acceleration |
| R0 | 1 | 0.75 | 23.098101 | 83.153162 | 23.085089 | 0.843672 | signed-drive acceleration |
| R0 | 1 | 0.875 | 23.727928 | 85.420541 | 23.716116 | 1 | signed-drive acceleration |
| R0 | 1 | 1 | 24.321482 | 87.557334 | 24.310753 | 1 | signed-drive acceleration |
| R0 | 2 | 0 | 27.05271 | 97.389754 | 28.063862 | 0 | carry/passive |
| R0 | 2 | 0.125 | 26.793224 | 96.455608 | 26.793224 | 0 | correction |
| R0 | 2 | 0.25 | 25.459301 | 91.653484 | 25.459299 | 0 | correction |
| R0 | 2 | 0.375 | 24.051847 | 86.586651 | 24.037403 | 0 | correction |
| R0 | 2 | 0.5 | 22.557611 | 81.207401 | 22.542212 | 0 | correction |
| R0 | 2 | 0.625 | 22.626631 | 81.455871 | 22.626631 | 0.316544 | signed-drive acceleration |
| R0 | 2 | 0.75 | 23.044691 | 82.960888 | 23.03166 | 0.843672 | signed-drive acceleration |
| R0 | 2 | 0.875 | 23.676777 | 85.236397 | 23.664946 | 1 | signed-drive acceleration |
| R0 | 2 | 1 | 24.272369 | 87.38053 | 24.261631 | 1 | signed-drive acceleration |
| R25 | 1 | 0 | 27.049263 | 97.377347 | 28.123388 | 0 | carry/passive |
| R25 | 1 | 0.125 | 26.844616 | 96.640617 | 26.851614 | 0 | correction |
| R25 | 1 | 0.25 | 25.516584 | 91.859704 | 25.516582 | 0 | correction |
| R25 | 1 | 0.375 | 24.10807 | 86.789053 | 24.093657 | 0 | correction |
| R25 | 1 | 0.5 | 22.612858 | 81.406288 | 22.597488 | 0 | correction |
| R25 | 1 | 0.625 | 22.560438 | 81.217577 | 22.681591 | 0.316544 | signed-drive acceleration |
| R25 | 1 | 0.75 | 22.924681 | 82.528851 | 23.085089 | 0.843672 | signed-drive acceleration |
| R25 | 1 | 0.875 | 23.565493 | 84.835773 | 23.716116 | 1 | signed-drive acceleration |
| R25 | 1 | 1 | 24.17404 | 87.026543 | 24.310753 | 1 | signed-drive acceleration |
| R25 | 2 | 0 | 26.958334 | 97.050002 | 28.063862 | 0 | carry/passive |
| R25 | 2 | 0.125 | 26.773577 | 96.384876 | 26.793224 | 0 | correction |
| R25 | 2 | 0.25 | 25.459301 | 91.653484 | 25.459299 | 0 | correction |
| R25 | 2 | 0.375 | 24.051847 | 86.586651 | 24.037403 | 0 | correction |
| R25 | 2 | 0.5 | 22.557611 | 81.207401 | 22.542212 | 0 | correction |
| R25 | 2 | 0.625 | 22.50561 | 81.020194 | 22.626631 | 0.316544 | signed-drive acceleration |
| R25 | 2 | 0.75 | 22.871475 | 82.337311 | 23.03166 | 0.843672 | signed-drive acceleration |
| R25 | 2 | 0.875 | 23.514555 | 84.652398 | 23.664946 | 1 | signed-drive acceleration |
| R25 | 2 | 1 | 24.125147 | 86.850529 | 24.261631 | 1 | signed-drive acceleration |
| R50 | 1 | 0 | 26.955292 | 97.03905 | 28.123388 | 0 | carry/passive |
| R50 | 1 | 0.125 | 26.627035 | 95.857327 | 26.851614 | 0 | carry/passive |
| R50 | 1 | 0.25 | 25.516584 | 91.859704 | 25.516582 | 0 | correction |
| R50 | 1 | 0.375 | 24.10807 | 86.789053 | 24.093657 | 0 | correction |
| R50 | 1 | 0.5 | 22.612858 | 81.406288 | 22.597488 | 0 | correction |
| R50 | 1 | 0.625 | 22.439579 | 80.782484 | 22.681591 | 0.316544 | signed-drive acceleration |
| R50 | 1 | 0.75 | 22.751991 | 81.907169 | 23.085089 | 0.843672 | signed-drive acceleration |
| R50 | 1 | 0.875 | 23.403934 | 84.254164 | 23.716116 | 1 | signed-drive acceleration |
| R50 | 1 | 1 | 24.027548 | 86.499172 | 24.310753 | 1 | signed-drive acceleration |
| R50 | 2 | 0 | 26.864849 | 96.713457 | 28.063862 | 0 | carry/passive |
| R50 | 2 | 0.125 | 26.537214 | 95.533971 | 26.793224 | 0 | carry/passive |
| R50 | 2 | 0.25 | 25.459299 | 91.653477 | 25.459299 | 0 | correction |
| R50 | 2 | 0.375 | 24.051846 | 86.586644 | 24.037403 | 0 | correction |
| R50 | 2 | 0.5 | 22.55761 | 81.207394 | 22.542212 | 0 | correction |
| R50 | 2 | 0.625 | 22.384876 | 80.585555 | 22.626631 | 0.316544 | signed-drive acceleration |
| R50 | 2 | 0.75 | 22.698982 | 81.716336 | 23.03166 | 0.843672 | signed-drive acceleration |
| R50 | 2 | 0.875 | 23.353205 | 84.071537 | 23.664946 | 1 | signed-drive acceleration |
| R50 | 2 | 1 | 23.978861 | 86.323899 | 24.261631 | 1 | signed-drive acceleration |
| R75 | 1 | 0 | 26.862238 | 96.704057 | 28.123388 | 0 | carry/passive |
| R75 | 1 | 0.125 | 26.371658 | 94.93797 | 26.851614 | 0 | carry/passive |
| R75 | 1 | 0.25 | 25.516584 | 91.859704 | 25.516582 | 0 | correction |
| R75 | 1 | 0.375 | 24.10807 | 86.789053 | 24.093657 | 0 | correction |
| R75 | 1 | 0.5 | 22.612858 | 81.406288 | 22.597488 | 0 | correction |
| R75 | 1 | 0.625 | 22.31901 | 80.348435 | 22.681591 | 0.316544 | signed-drive deceleration |
| R75 | 1 | 0.75 | 22.580023 | 81.288082 | 23.085089 | 0.843672 | signed-drive acceleration |
| R75 | 1 | 0.875 | 23.243259 | 83.675734 | 23.716116 | 1 | signed-drive acceleration |
| R75 | 1 | 1 | 23.881998 | 85.975193 | 24.310753 | 1 | signed-drive acceleration |
| R75 | 2 | 0 | 26.772287 | 96.380235 | 28.063862 | 0 | carry/passive |
| R75 | 2 | 0.125 | 26.282631 | 94.617471 | 26.793224 | 0 | carry/passive |
| R75 | 2 | 0.25 | 25.459299 | 91.653477 | 25.459299 | 0 | correction |
| R75 | 2 | 0.375 | 24.051846 | 86.586644 | 24.037403 | 0 | correction |
| R75 | 2 | 0.5 | 22.55761 | 81.207394 | 22.542212 | 0 | correction |
| R75 | 2 | 0.625 | 22.264441 | 80.151986 | 22.626631 | 0.316544 | signed-drive deceleration |
| R75 | 2 | 0.75 | 22.527214 | 81.097971 | 23.03166 | 0.843672 | signed-drive acceleration |
| R75 | 2 | 0.875 | 23.192741 | 83.493869 | 23.664946 | 1 | signed-drive acceleration |
| R75 | 2 | 1 | 23.83353 | 85.80071 | 24.261631 | 1 | signed-drive acceleration |
| R100 | 1 | 0 | 26.749119 | 96.296828 | 28.123388 | 0 | carry/passive |
| R100 | 1 | 0.125 | 26.097668 | 93.951604 | 26.851614 | 0 | carry/passive |
| R100 | 1 | 0.25 | 25.459091 | 91.652728 | 25.516582 | 0 | correction |
| R100 | 1 | 0.375 | 24.10807 | 86.789053 | 24.093657 | 0 | correction |
| R100 | 1 | 0.5 | 22.612858 | 81.406288 | 22.597488 | 0 | correction |
| R100 | 1 | 0.625 | 22.198727 | 79.915416 | 22.681591 | 0.316544 | signed-drive deceleration |
| R100 | 1 | 0.75 | 22.40876 | 80.671536 | 23.085089 | 0.843672 | signed-drive acceleration |
| R100 | 1 | 0.875 | 23.083441 | 83.100387 | 23.716116 | 1 | signed-drive acceleration |
| R100 | 1 | 1 | 23.737387 | 85.454592 | 24.310753 | 1 | signed-drive acceleration |
| R100 | 2 | 0 | 26.680645 | 96.050322 | 28.063862 | 0 | carry/passive |
| R100 | 2 | 0.125 | 26.030127 | 93.708456 | 26.793224 | 0 | carry/passive |
| R100 | 2 | 0.25 | 25.394453 | 91.420031 | 25.459299 | 0 | correction |
| R100 | 2 | 0.375 | 24.051846 | 86.586644 | 24.037403 | 0 | correction |
| R100 | 2 | 0.5 | 22.55761 | 81.207394 | 22.542212 | 0 | correction |
| R100 | 2 | 0.625 | 22.144285 | 79.719427 | 22.626631 | 0.316544 | signed-drive deceleration |
| R100 | 2 | 0.75 | 22.356148 | 80.482132 | 23.03166 | 0.843672 | signed-drive acceleration |
| R100 | 2 | 0.875 | 23.033133 | 82.919277 | 23.664946 | 1 | signed-drive acceleration |
| R100 | 2 | 1 | 23.689117 | 85.280823 | 24.261631 | 1 | signed-drive acceleration |


## I. Force decomposition by corner progress

Values are observations carried by the production traversal nodes and interpolated only between adjacent actual nodes for the frozen p-grid; no parallel physics traversal is reconstructed.
| candidate | corner | p | drive N | resistance N | exposed resistance N | net force N | net acceleration m/s² | phase |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 1 | 0 | 0 | 187.361099 | 0 | 0 | 0 | carry/passive |
| R0 | 1 | 0.125 | 0 | 184.202271 | 0 | 0 | 0 | correction |
| R0 | 1 | 0.25 | 0 | 170.219559 | 0 | 0 | 0 | correction |
| R0 | 1 | 0.375 | 0 | 156.240372 | 0 | 0 | 0 | correction |
| R0 | 1 | 0.5 | 0 | 142.268265 | 0 | 0 | 0 | correction |
| R0 | 1 | 0.625 | 99.943932 | 142.89093 | 45.232712 | 54.71122 | 0.38529 | signed-drive acceleration |
| R0 | 1 | 0.75 | 263.289978 | 146.704498 | 123.771286 | 139.518677 | 0.982526 | signed-drive acceleration |
| R0 | 1 | 0.875 | 306.813965 | 152.603012 | 152.603012 | 154.210968 | 1.085993 | signed-drive acceleration |
| R0 | 1 | 1 | 301.854065 | 158.3069 | 158.3069 | 143.547165 | 1.010895 | signed-drive acceleration |
| R0 | 2 | 0 | 0 | 186.369827 | 0 | 0 | 0 | carry/passive |
| R0 | 2 | 0.125 | 0 | 183.575806 | 0 | 0 | 0 | correction |
| R0 | 2 | 0.25 | 0 | 169.635544 | 0 | 0 | 0 | correction |
| R0 | 2 | 0.375 | 0 | 155.698837 | 0 | 0 | 0 | correction |
| R0 | 2 | 0.5 | 0 | 141.769165 | 0 | 0 | 0 | correction |
| R0 | 2 | 0.625 | 99.859894 | 142.392899 | 45.075066 | 54.784828 | 0.385809 | signed-drive acceleration |
| R0 | 2 | 0.75 | 263.061615 | 146.211594 | 123.355446 | 139.706161 | 0.983846 | signed-drive acceleration |
| R0 | 2 | 0.875 | 306.536591 | 152.118057 | 152.118057 | 154.418533 | 1.087454 | signed-drive acceleration |
| R0 | 2 | 1 | 301.571045 | 157.82959 | 157.82959 | 143.741455 | 1.012264 | signed-drive acceleration |
| R25 | 1 | 0 | 0 | 186.332535 | 46.583134 | -46.583134 | -0.32805 | carry/passive |
| R25 | 1 | 0.125 | 0 | 184.126984 | 46.031746 | -46.031746 | -0.324167 | correction |
| R25 | 1 | 0.25 | 0 | 170.219559 | 42.55489 | -42.55489 | -0.299682 | correction |
| R25 | 1 | 0.375 | 0 | 156.240372 | 39.060093 | -39.060093 | -0.275071 | correction |
| R25 | 1 | 0.5 | 0 | 142.268265 | 35.567066 | -35.567066 | -0.250472 | correction |
| R25 | 1 | 0.625 | 100.265144 | 141.794662 | 69.11248 | 31.15266 | 0.219385 | signed-drive acceleration |
| R25 | 1 | 0.75 | 264.512573 | 145.108231 | 128.095459 | 136.417114 | 0.960684 | signed-drive acceleration |
| R25 | 1 | 0.875 | 308.171326 | 151.066589 | 151.066589 | 157.104752 | 1.106372 | signed-drive acceleration |
| R25 | 1 | 1 | 303.086121 | 156.876846 | 156.876846 | 146.209274 | 1.029643 | signed-drive acceleration |
| R25 | 2 | 0 | 0 | 185.350357 | 46.337589 | -46.337589 | -0.326321 | carry/passive |
| R25 | 2 | 0.125 | 0 | 183.36499 | 45.841248 | -45.841248 | -0.322826 | correction |
| R25 | 2 | 0.25 | 0 | 169.635544 | 42.408886 | -42.408886 | -0.298654 | correction |
| R25 | 2 | 0.375 | 0 | 155.698837 | 38.924709 | -38.924709 | -0.274118 | correction |
| R25 | 2 | 0.5 | 0 | 141.769165 | 35.442291 | -35.442291 | -0.249594 | correction |
| R25 | 2 | 0.625 | 100.180038 | 141.300491 | 68.87162 | 31.308414 | 0.220482 | signed-drive acceleration |
| R25 | 2 | 0.75 | 264.280029 | 144.620895 | 127.665283 | 136.614746 | 0.962076 | signed-drive acceleration |
| R25 | 2 | 0.875 | 307.889069 | 150.586945 | 150.586945 | 157.302109 | 1.107761 | signed-drive acceleration |
| R25 | 2 | 1 | 302.798462 | 156.404541 | 156.404541 | 146.393921 | 1.030943 | signed-drive acceleration |
| R50 | 1 | 0 | 0 | 185.317551 | 92.658775 | -92.658775 | -0.652527 | carry/passive |
| R50 | 1 | 0.125 | 0 | 181.799835 | 90.899918 | -90.899918 | -0.64014 | carry/passive |
| R50 | 1 | 0.25 | 0 | 170.219559 | 85.109779 | -85.109779 | -0.599365 | correction |
| R50 | 1 | 0.375 | 0 | 156.240372 | 78.120186 | -78.120186 | -0.550142 | correction |
| R50 | 1 | 0.5 | 0 | 142.268265 | 71.134132 | -71.134132 | -0.500945 | correction |
| R50 | 1 | 0.625 | 100.585571 | 140.70694 | 92.62355 | 7.962018 | 0.056071 | signed-drive acceleration |
| R50 | 1 | 0.75 | 265.730042 | 143.530655 | 132.312119 | 133.417923 | 0.939563 | signed-drive acceleration |
| R50 | 1 | 0.875 | 309.521362 | 149.54895 | 149.54895 | 159.972412 | 1.126566 | signed-drive acceleration |
| R50 | 1 | 1 | 304.310272 | 155.464615 | 155.464615 | 148.845657 | 1.048209 | signed-drive acceleration |
| R50 | 2 | 0 | 0 | 184.344025 | 92.172012 | -92.172012 | -0.649099 | carry/passive |
| R50 | 2 | 0.125 | 0 | 180.844788 | 90.422394 | -90.422394 | -0.636777 | carry/passive |
| R50 | 2 | 0.25 | 0 | 169.635529 | 84.817764 | -84.817764 | -0.597308 | correction |
| R50 | 2 | 0.375 | 0 | 155.698822 | 77.849411 | -77.849411 | -0.548235 | correction |
| R50 | 2 | 0.5 | 0 | 141.76915 | 70.884575 | -70.884575 | -0.499187 | correction |
| R50 | 2 | 0.625 | 100.499397 | 140.216537 | 92.300735 | 8.198662 | 0.057737 | signed-drive acceleration |
| R50 | 2 | 0.75 | 265.493286 | 143.048782 | 131.86792 | 133.625381 | 0.941024 | signed-drive acceleration |
| R50 | 2 | 0.875 | 309.234253 | 149.074539 | 149.074539 | 160.159714 | 1.127885 | signed-drive acceleration |
| R50 | 2 | 1 | 304.018066 | 154.997162 | 154.997162 | 149.020905 | 1.049443 | signed-drive acceleration |
| R75 | 1 | 0 | 0 | 184.315964 | 138.236969 | -138.236969 | -0.9735 | carry/passive |
| R75 | 1 | 0.125 | 0 | 179.092941 | 134.319702 | -134.319702 | -0.945913 | carry/passive |
| R75 | 1 | 0.25 | 0 | 170.219559 | 127.664665 | -127.664665 | -0.899047 | correction |
| R75 | 1 | 0.375 | 0 | 156.240372 | 117.180283 | -117.180283 | -0.825213 | correction |
| R75 | 1 | 0.5 | 0 | 142.268265 | 106.701202 | -106.701202 | -0.751417 | correction |
| R75 | 1 | 0.625 | 100.905235 | 139.62764 | 115.77021 | -14.864975 | -0.104683 | signed-drive deceleration |
| R75 | 1 | 0.75 | 266.942413 | 141.971512 | 136.423172 | 130.519241 | 0.91915 | signed-drive acceleration |
| R75 | 1 | 0.875 | 310.864014 | 148.049927 | 148.049927 | 162.814087 | 1.146578 | signed-drive acceleration |
| R75 | 1 | 1 | 305.52652 | 154.069962 | 154.069962 | 151.456558 | 1.066595 | signed-drive acceleration |
| R75 | 2 | 0 | 0 | 183.351074 | 137.513306 | -137.513306 | -0.968404 | carry/passive |
| R75 | 2 | 0.125 | 0 | 178.155411 | 133.616562 | -133.616562 | -0.940962 | carry/passive |
| R75 | 2 | 0.25 | 0 | 169.635529 | 127.226654 | -127.226654 | -0.895962 | correction |
| R75 | 2 | 0.375 | 0 | 155.698822 | 116.774117 | -116.774117 | -0.822353 | correction |
| R75 | 2 | 0.5 | 0 | 141.76915 | 106.326859 | -106.326859 | -0.748781 | correction |
| R75 | 2 | 0.625 | 100.81797 | 139.141052 | 115.366768 | -14.548798 | -0.102456 | signed-drive deceleration |
| R75 | 2 | 0.75 | 266.701477 | 141.495102 | 135.965378 | 130.736099 | 0.920677 | signed-drive acceleration |
| R75 | 2 | 0.875 | 310.572052 | 147.580765 | 147.580765 | 162.991272 | 1.147826 | signed-drive acceleration |
| R75 | 2 | 1 | 305.229706 | 153.607437 | 153.607437 | 151.622269 | 1.067762 | signed-drive acceleration |
| R100 | 1 | 0 | 0 | 183.103073 | 183.103073 | -183.103073 | -1.289458 | carry/passive |
| R100 | 1 | 0.125 | 0 | 176.217758 | 176.217758 | -176.217758 | -1.24097 | carry/passive |
| R100 | 1 | 0.25 | 0 | 169.633087 | 169.633087 | -169.633087 | -1.194599 | correction |
| R100 | 1 | 0.375 | 0 | 156.240372 | 156.240372 | -156.240372 | -1.100284 | correction |
| R100 | 1 | 0.5 | 0 | 142.268265 | 142.268265 | -142.268265 | -1.001889 | correction |
| R100 | 1 | 0.625 | 101.224144 | 138.556717 | 138.556717 | -37.332569 | -0.262905 | signed-drive deceleration |
| R100 | 1 | 0.75 | 268.149811 | 140.430527 | 140.430527 | 127.719284 | 0.899432 | signed-drive acceleration |
| R100 | 1 | 0.875 | 312.199524 | 146.569153 | 146.569153 | 165.630371 | 1.166411 | signed-drive acceleration |
| R100 | 1 | 1 | 306.734924 | 152.692703 | 152.692703 | 154.042221 | 1.084804 | signed-drive acceleration |
| R100 | 2 | 0 | 0 | 182.371368 | 182.371368 | -182.371368 | -1.284305 | carry/passive |
| R100 | 2 | 0.125 | 0 | 175.513611 | 175.513611 | -175.513611 | -1.236011 | carry/passive |
| R100 | 2 | 0.25 | 0 | 168.975677 | 168.975677 | -168.975677 | -1.18997 | correction |
| R100 | 2 | 0.375 | 0 | 155.698822 | 155.698822 | -155.698822 | -1.096471 | correction |
| R100 | 2 | 0.5 | 0 | 141.76915 | 141.76915 | -141.76915 | -0.998374 | correction |
| R100 | 2 | 0.625 | 101.135811 | 138.073883 | 138.073883 | -36.938068 | -0.260127 | signed-drive deceleration |
| R100 | 2 | 0.75 | 267.904724 | 139.959488 | 139.959488 | 127.945221 | 0.901023 | signed-drive acceleration |
| R100 | 2 | 0.875 | 311.90271 | 146.105164 | 146.105164 | 165.797562 | 1.167588 | signed-drive acceleration |
| R100 | 2 | 1 | 306.433685 | 152.234863 | 152.234863 | 154.198822 | 1.085907 | signed-drive acceleration |


## J. Distance buckets

| Candidate | travelled m | correction m | passive resistance m | positive drive m | negative signed drive m | neutral carry m | availability0/no-correction m | conserved |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 217.712379 | 87.713778 | 0 | 106.856194 | 0 | 23.142408 | 21.4129 | YES |
| R25 | 217.712379 | 82.416565 | 26 | 90.856194 | 16 | 2.439621 | 26.710113 | YES |
| R50 | 217.71238 | 75.719662 | 32 | 82.856194 | 24 | 3.136524 | 33.407 | YES |
| R75 | 217.712378 | 66.616455 | 41 | 78.856193 | 28 | 3.239731 | 42.510204 | YES |
| R100 | 217.71238 | 53.581023 | 54 | 75.856193 | 31 | 3.275163 | 55.545639 | YES |


## K. Kinetic-energy diagnostic

`KE = 0.5 × 142 kg × v²`. Isolated apex energy uses canonical `p = 0.50`, never the corner minimum. This diagnostic is not asserted as a complete conservation law.
| Candidate | entry J | apex J | exit J | Δ entry→apex J | Δ apex→exit J |
| --- | --- | --- | --- | --- | --- |
| R0 | 52137.085618 | 37004.755756 | 42719.887571 | -15132.329861 | 5715.131815 |
| R25 | 52137.085618 | 37004.755756 | 42175.481672 | -15132.329861 | 5170.725915 |
| R50 | 52137.085618 | 37004.755756 | 41638.226726 | -15132.329861 | 4633.470969 |
| R75 | 52137.085618 | 37004.755756 | 41108.078193 | -15132.329861 | 4103.322437 |
| R100 | 52137.085618 | 37004.755756 | 40584.92012 | -15132.329861 | 3580.164364 |


## L. Isolated corner

Fixed entry speed 27.098434 m/s, L1, all skills 50, neutral setup, baseline surface and the production Motoarena logical-corner geometry.
| Candidate | entry m/s | apex m/s | exit m/s | peak→apex m/s | entry→apex Δv | apex→exit Δv | entry→apex s | apex→exit s | total s | first correction p | last correction p | correction m | target reached p |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 27.098434 | 22.829668 | 24.529341 | 4.268766 | -4.268766 | 1.699673 | 2.137882 | 2.322021 | 4.459904 | 0.12861 | 0.5 | 40.986763 | 0.009186 |
| R25 | 27.098434 | 22.829668 | 24.372543 | 4.268766 | -4.268766 | 1.542875 | 2.139593 | 2.336419 | 4.476012 | 0.146983 | 0.5 | 39.098728 | 0.009186 |
| R50 | 27.098434 | 22.829668 | 24.21681 | 4.268766 | -4.268766 | 1.387142 | 2.141887 | 2.35095 | 4.492836 | 0.165356 | 0.5 | 36.735722 | 0.009186 |
| R75 | 27.098434 | 22.829668 | 24.062149 | 4.268766 | -4.268766 | 1.232481 | 2.145091 | 2.365614 | 4.510705 | 0.192915 | 0.5 | 33.55291 | 0.009186 |
| R100 | 27.098434 | 22.829668 | 23.908546 | 4.268766 | -4.268766 | 1.078878 | 2.14993 | 2.380414 | 4.530343 | 0.238847 | 0.5 | 28.768726 | 0.009186 |

R0 first-half metres with availability=0 and no explicit correction: 13.441332 m.
R100 same metric: 25.659367 m.

## M. Distance-matched proxy

`LateralPosition ≈ 1.11065` is a `RealP50DistanceMatchedProxy`, not a real trajectory. The modeled distance below is the fixed-position geometric reference; production lateral execution is not redefined by this experiment.
| Candidate | modeled 4-lap distance m | Vmax km/h | Flying median s | Heat s | Average m/s | entry m/s | true apex p=.50 m/s | exit m/s | peak→true-apex m/s | minimum m/s | minimum p | corner L2 s | straight L2 s | B/RW/C |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 1376.999878 | 97.882807 | 13.891214 | 57.115219 | 23.931442 | 27.104744 | 22.604454 | 24.314007 | 4.540365 | 22.561489 | 0.509186 | 8.997688 | 4.808809 | 0/0/0 |
| R100 | 1376.999878 | 96.299258 | 14.138378 | 58.486202 | 23.370462 | 26.721045 | 22.604452 | 23.73004 | 4.145342 | 22.14612 | 0.646983 | 9.151249 | 4.899549 | 0/0/0 |


## N. Line sweep

| Candidate | Line | distance m | Vmax km/h | Flying s | entry m/s | true apex m/s | minimum m/s | minimum p | exit m/s | corner time s |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | L0 | 1275.115112 | 95.321489 | 13.548689 | 26.396645 | 21.380764 | 21.340143 | 0.510268 | 23.200043 | 8.483189 |
| R0 | L1 | 1366.849609 | 97.882807 | 13.891222 | 27.104687 | 22.604454 | 22.561489 | 0.509186 | 24.314007 | 8.99769 |
| R0 | L2 | 1458.583984 | 100.11711 | 14.249331 | 27.768467 | 23.765213 | 23.720036 | 0.508311 | 25.333441 | 9.504667 |
| R0 | L3 | 1550.318481 | 102.443012 | 14.61462 | 28.412844 | 24.871864 | 24.824574 | 0.507588 | 26.303093 | 10.003305 |
| R0 | L4 | 1642.052734 | 104.599477 | 14.989109 | 29.010254 | 25.931328 | 25.882015 | 0.50698 | 27.19223 | 10.496955 |
| R100 | L0 | 1275.115112 | 94.015833 | 13.739773 | 26.078144 | 21.380764 | 21.004423 | 0.643753 | 22.688435 | 8.585118 |
| R100 | L1 | 1366.849609 | 96.298812 | 14.138382 | 26.720982 | 22.604452 | 22.14612 | 0.646983 | 23.73004 | 9.151254 |
| R100 | L2 | 1458.583984 | 98.219923 | 14.590982 | 27.178352 | 23.749054 | 23.171566 | 0.657908 | 24.653105 | 9.770218 |
| R100 | L3 | 1550.318481 | 99.428075 | 15.186371 | 27.208267 | 24.103806 | 23.313913 | 0.659345 | 24.991707 | 10.640667 |
| R100 | L4 | 1642.052734 | 98.955361 | 16.264572 | 27.140091 | 23.779261 | 22.97031 | 0.660551 | 24.841373 | 11.685535 |


## O. Speed skill sweep

| Candidate | Speed | Vmax km/h | Flying s | entry m/s | true apex m/s | minimum m/s | minimum p | exit m/s | B/RW/C |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 0 | 88.349339 | 15.053471 | 24.455114 | 21.230312 | 21.189699 | 0.509186 | 22.270374 | 0/0/0 |
| R0 | 25 | 93.294759 | 14.437695 | 25.82629 | 21.907772 | 21.865971 | 0.509186 | 23.301556 | 0/0/0 |
| R0 | 50 | 97.882807 | 13.909906 | 27.098434 | 22.585236 | 22.542242 | 0.509186 | 24.296925 | 0/0/0 |
| R0 | 75 | 102.1257 | 13.450254 | 28.274879 | 23.262699 | 23.218513 | 0.509186 | 25.241196 | 0/0/0 |
| R0 | 100 | 105.635962 | 13.039486 | 29.209652 | 23.959227 | 23.894785 | 0.509186 | 26.149874 | 0/0/0 |
| R100 | 0 | 86.703065 | 15.469013 | 24.041258 | 21.21566 | 20.676626 | 0.675853 | 21.651936 | 0/0/0 |
| R100 | 25 | 91.697127 | 14.757849 | 25.427608 | 21.892756 | 21.38632 | 0.665356 | 22.690655 | 0/0/0 |
| R100 | 50 | 96.296828 | 14.156315 | 26.714882 | 22.585234 | 22.127251 | 0.646983 | 23.713253 | 0/0/0 |
| R100 | 75 | 100.567083 | 13.654634 | 27.899996 | 23.262699 | 22.818493 | 0.646983 | 24.660145 | 0/0/0 |
| R100 | 100 | 104.629202 | 13.220409 | 29.017357 | 23.940163 | 23.504883 | 0.637796 | 25.569881 | 0/0/0 |


## P. SlideControl sweep

| Candidate | SlideControl | correction capability m/s² | entry m/s | true apex m/s | minimum m/s | minimum p | corner time s | Flying s |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 0 | 2 | 26.370859 | 21.924519 | 21.865982 | 0.509186 | 9.330975 | 14.323858 |
| R0 | 50 | 2.6 | 27.098434 | 22.585236 | 22.542242 | 0.509186 | 9.004429 | 13.909906 |
| R0 | 100 | 3.2 | 27.429405 | 23.26305 | 23.218506 | 0.509186 | 8.740826 | 13.565853 |
| R100 | 0 | 2 | 26.370859 | 21.924519 | 21.471216 | 0.646983 | 9.445281 | 14.527426 |
| R100 | 50 | 2.6 | 26.714882 | 22.585234 | 22.127251 | 0.646983 | 9.157445 | 14.156315 |
| R100 | 100 | 3.2 | 27.028696 | 23.246946 | 22.73785 | 0.656169 | 8.948952 | 13.865749 |


## Q. Surface sensitivity

| Candidate | Surface | entry m/s | true apex m/s | minimum m/s | minimum p | exit m/s | corner time s | Flying s |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | baseline | 27.098434 | 22.585236 | 22.542242 | 0.509186 | 24.296925 | 9.004429 | 13.909906 |
| R0 | grip_080 | 26.385933 | 21.659195 | 21.614868 | 0.509186 | 23.474838 | 9.354698 | 14.412971 |
| R0 | moisture_070 | 26.822025 | 22.40811 | 22.331696 | 0.5 | 24.159124 | 9.073063 | 13.84396 |
| R0 | ruts_025 | 26.699478 | 22.065563 | 22.02182 | 0.509186 | 23.835331 | 9.19741 | 14.188627 |
| R100 | baseline | 26.714882 | 22.585234 | 22.127251 | 0.646983 | 23.713253 | 9.157445 | 14.156315 |
| R100 | grip_080 | 26.024088 | 21.659195 | 21.217888 | 0.646983 | 22.905178 | 9.50115 | 14.657587 |
| R100 | moisture_070 | 26.433878 | 22.40811 | 21.878128 | 0.646983 | 23.545351 | 9.239356 | 14.084385 |
| R100 | ruts_025 | 26.322411 | 22.065563 | 21.61697 | 0.646983 | 23.259573 | 9.346889 | 14.433661 |


## R. Extreme check

Speed100, SlideControl100, Gearing1, best tested surface and L4. No cap is added.
| Candidate | Vmax km/h | entry m/s | true apex m/s | exit m/s | B/RW/C | minimum m/s | minimum p | zero-speed event |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 118.400002 | 32.832893 | 28.311886 | 30.277294 | 0/0/0 | 28.257988 | 0.50698 | NO |
| R100 | 114.90135 | 30.75499 | 27.138351 | 28.501238 | 0/0/0 | 26.019724 | 0.653571 | NO |


## S. Frozen systems

| System | Exact |
| --- | --- |
| StandingStart | YES |
| Straight isolated law | YES |
| Legacy | YES |
| SegmentPhysics thresholds | YES |
| full production Calibration Scenario Suite | YES |

Standing-start Skill50 baseline: reaction 0.24 s; TimeTo70 2.237621 s; SpeedAt2s 62.332127 km/h.
Straight candidate is always production A0; #42 never composes with #41 H12. Advanced reference turn speed remains 19 m/s, ApexProgress 0.50, FullDriveProgress 5/6 and correction 2.0→3.2 m/s². Legacy, incident thresholds, contacts, surfaces and standing start are unchanged.

## T. Pareto interpretation

| Candidate | ΔFlying s | ΔCornerTime s | ΔEntry→Apex s | ΔApex→Exit s | ΔTrueApex m/s | ΔMinimum m/s | minimum p | ΔExit m/s | ΔVmax km/h | true peak→apex m/s | peak→minimum m/s | Extreme |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.509186 | 0 | 0 | 4.558926 | 4.601919 | finite/no-zero |
| R25 | 0.056511 | 0.03272 | 0.005519 | 0.0272 | 0 | -0.056568 | 0.582678 | -0.147331 | -0.361677 | 4.464027 | 4.563589 | n/a |
| R50 | 0.115431 | 0.067905 | 0.013267 | 0.054638 | -0.000002 | -0.15873 | 0.619424 | -0.29372 | -0.843757 | 4.370058 | 4.571779 | n/a |
| R75 | 0.177946 | 0.1069 | 0.024583 | 0.082317 | -0.000002 | -0.281223 | 0.637796 | -0.439159 | -1.178751 | 4.277004 | 4.601219 | n/a |
| R100 | 0.246408 | 0.153016 | 0.042773 | 0.110243 | -0.000002 | -0.414991 | 0.646983 | -0.583672 | -1.585979 | 4.163885 | 4.621868 | finite/no-zero |

Classification: `LossLocationMismatch`.

## U. Next-model decision

Resistance exposure alone changes the flying median by 0.246408 s at R100. The L2 two-corner time effect is 0.042773 s pre-apex and 0.110243 s post-apex; the dominant location is `post-apex`.
R100 changes true apex by -0.000002 m/s, while its minimum is 22.127251 m/s at p=0.646983 versus R0 22.542242 m/s at p=0.509186. The lower post-apex minimum, lower exit and dominant post-apex time delta are direct evidence for `LossLocationMismatch`; minimum is not relabeled as apex.
The one subsystem proposed for #43 is `corner-specific slip-loss / scrub-drag`. #42 selects no production Rxx value.

## V. Limitations

- No real per-point corner-speed telemetry.
- No real apex speed or throttle trace.
- No lean angle, wheel slip, banking or physical rider trajectory.
- The existing resistance model itself remains provisional.
- Motoarena geometry remains a symmetric approximation.
- L1 is a controlled reference, not a real trajectory.
- Interpolated p-grid observations lie between actual 1 m traversal nodes and do not introduce another simulator.
