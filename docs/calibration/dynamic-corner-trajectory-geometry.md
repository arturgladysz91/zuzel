# Dynamic corner trajectory geometry baseline (#46)

Base main: `4c54c7d2cc87c79ad7c28fd2b86080fb68319a99`. Analysis-only production observation; all calibration-only physics adjustments are null.

## A. Game design objective

This manager-game benchmark asks whether the current production simulation creates useful entry → apex-waypoint → exit trajectory economics. Gameplay variety is the objective; realism is a guardrail, and no candidate is promoted to production.

## B. Why constant lane is not an optimal-trajectory proof

#45 constant-lane physical best is a reference-path result, not a proof of globally optimal speedway trajectory. `HoldLane L0` means a small radius throughout the corner; it does not mean an optimal inner speedway path.

## C. Existing production trajectory architecture

The diagnostic model supplies only the existing integer `TargetLane`. Production still resolves `TargetLane → PlannedLane → Lane → continuous LateralPosition` through the unchanged planner, `LateralMovementModel`, `SegmentPhysics`, `ContinuousCornerEnvelope`, and `HeatSimulator`. Actual positions below are observations, never target aliases.

## D. Diagnostic waypoint model

Straight targets Entry; TurnEntry targets Apex; TurnMiddle and TurnExit target Exit; the following straight prepares the next Entry. The model is internal, stateless, analysis-only, and does not add Entry/Apex/Exit fields to `RiderDecision`.
Reference | Target plan
---|---:
ConstantInner | `E0-A0-X0`
ConstantL1 | `E1-A1-X1`
ConstantL2 | `E2-A2-X2`
ConstantL3 | `E3-A3-X3`
ConstantOuter | `E4-A4-X4`
TightEntryWideExit | `E0-A1-X2`
BalancedRelease | `E1-A1-X2`
WideDrive | `E2-A2-X3`
WideEntryCutBack | `E2-A1-X2`

## E. 125-plan uniform search

Target plans: **125**. Every E/A/X coordinate spans 0..4 in deterministic E-major, A-middle, X-minor order. Each candidate starts from a fresh baseline `TrackState`, neutral setup, Balanced 50/50/50/50/50/50 skills, one rider, four laps, Dry weather, zero incidents and seed 46000.

## F. Unique actual trajectory count

UniqueActualTrajectoryCount: **60**; duplicate target plans: **65**; planner-bound plans: **96**.
TrajectoryPlannerResolutionTooCoarse: **YES**.

## G. Constant reference trajectories

Reference | Plan | Actual fingerprint | 4-lap distance m | L2 | L3 | L4 | Flying s | Vmax m/s
---|---:|---:|---:|---:|---:|---:|---:|---:
ConstantInner | `E0-A0-X0` | `C0:0,0,0,0,0|C1:0,0,0,0,0` | 1275.115112 | 13.374661 | 13.397707 | 13.420746 | 13.397707 | 26.515163
ConstantL1 | `E1-A1-X1` | `C0:1,1,1,1,1|C1:1,1,1,1,1` | 1366.849609 | 13.714485 | 13.737913 | 13.761326 | 13.737913 | 27.227627
ConstantL2 | `E2-A2-X2` | `C0:2,2,2,2,2|C1:2,2,2,2,2` | 1458.583984 | 14.068207 | 14.092064 | 14.115902 | 14.092064 | 27.901592
ConstantL3 | `E3-A3-X3` | `C0:3,3,3,3,3|C1:3,3,3,3,3` | 1550.318481 | 14.432131 | 14.456406 | 14.480698 | 14.456406 | 28.542591
ConstantOuter | `E4-A4-X4` | `C0:4,4,4,4,4|C1:4,4,4,4,4` | 1642.052734 | 14.803703 | 14.828415 | 14.853123 | 14.828415 | 29.151857

#45 constant reference reproduction: **YES**. Distances remain strictly increasing L0 → L4.

## H. Named dynamic trajectories

Reference | Plan | Actual E/A/X | Exit release | Flying s | Delta to ConstantInner s
---|---:|---:|---:|---:|---:
TightEntryWideExit | `E0-A1-X2` | 1.000000/1.387389/2.000000 | +1.000000 | 13.956648 | +0.558941
BalancedRelease | `E1-A1-X2` | 1.000000/1.387424/2.000000 | +1.000000 | 13.961689 | +0.563982
WideDrive | `E2-A2-X3` | 2.000000/2.407371/3.000000 | +1.000000 | 14.319607 | +0.921900
WideEntryCutBack | `E2-A1-X2` | 2.000000/1.633532/2.000000 | 0.000000 | 14.111916 | +0.714209

## I. Top 15 actual trajectories

Rank | Target | Actual fingerprint | E/A/X | Flying s | Heat s | 4-lap m | Vmax | Entry | Apex | Min@p | Exit | Correction m/s | Brake/RunWide/Crash
---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:
1 | `E0-A0-X0` | `C0:0,0,0,0,0|C1:0,0,0,0,0` | 0.000000/0.000000/0.000000 | 13.397707 | 55.186684 | 1275.115112 | 26.515163 | 26.472385 | 21.503384 | 21.458405@0.500000 | 23.324699 | 46.085346/1.921191 | 0/0/0
2 | `E0-A1-X0` | `C0:0,0.643448,0.321724,0,0|C1:0,0.643493,0.321747,0,0` | 0.000000/0.321735/0.000000 | 13.507465 | 55.628323 | 1294.965088 | 26.515158 | 26.472385 | 22.337189 | 21.884756@0.728275 | 23.324697 | 44.096832/1.827320 | 6/0/0
3 | `E3-A0-X0` | `C0:1,0.308535,0.154267,0,0|C1:1,0.307109,0.153554,0,0` | 1.000000/0.153911/0.000000 | 13.545544 | 56.635139 | 1348.458374 | 27.726707 | 26.497379 | 22.926062 | 21.791687@0.697471 | 23.361176 | 44.806110/1.860441 | 12/0/0
4 | `E2-A0-X0` | `C0:1,0.30712,0.15356,0,0|C1:1,0.308526,0.154263,0,0` | 1.000000/0.153911/0.000000 | 13.556610 | 56.172283 | 1325.995483 | 27.193104 | 26.483639 | 22.901123 | 21.756350@0.697471 | 23.340906 | 44.806046/1.860435 | 12/0/0
5 | `E4-A0-X0` | `C0:1,0.307114,0.153557,0,0|C1:1,0.308526,0.154263,0,0` | 1.000000/0.153910/0.000000 | 13.556652 | 57.291203 | 1378.662109 | 27.902864 | 26.511261 | 22.951008 | 21.800535@0.697471 | 23.381496 | 44.804283/1.860370 | 11/0/0
6 | `E1-A0-X0` | `C0:1,0.308613,0.154306,0,0|C1:1,0.308259,0.15413,0,0` | 1.000000/0.154218/0.000000 | 13.565197 | 55.955097 | 1314.822754 | 26.515160 | 26.472847 | 22.880627 | 21.756350@0.697471 | 23.324697 | 44.875828/1.864385 | 12/0/0
7 | `E0-A1-X1` | `C0:0,0.636093,0.818046,1,1|C1:0,0.63668,0.81834,1,1` | 0.000000/0.818193/1.000000 | 13.581989 | 55.977417 | 1325.357178 | 27.038416 | 26.977360 | 22.328793 | 22.296526@0.500000 | 24.129402 | 44.286308/1.796313 | 0/0/0
8 | `E0-A0-X1` | `C0:0,0,0.366402,0.732804,1|C1:0,0,0.366402,0.732804,1` | 0.000000/0.366402/1.000000 | 13.617708 | 56.071751 | 1297.541016 | 26.592749 | 26.552059 | 21.503384 | 21.458405@0.500000 | 23.452152 | 46.902031/1.951995 | 0/0/0
9 | `E4-A1-X0` | `C0:1,1,0.612682,0.225363,0|C1:1,1,0.612116,0.224233,0` | 1.000000/0.612399/0.000000 | 13.708261 | 57.736282 | 1396.172852 | 27.902864 | 26.674194 | 22.802958 | 22.222809@0.726682 | 23.652256 | 43.359619/1.778601 | 6/0/0
10 | `E3-A1-X0` | `C0:1,1,0.612145,0.224289,0|C1:1,1,0.612684,0.225368,0` | 1.000000/0.612414/0.000000 | 13.718815 | 57.166805 | 1369.477295 | 27.726707 | 26.666967 | 22.779112 | 22.209276@0.726674 | 23.632536 | 43.355469/1.778387 | 6/0/0
11 | `E2-A1-X0` | `C0:1,1,0.61278,0.225559,0|C1:1,1,0.612576,0.225152,0` | 1.000000/0.612678/0.000000 | 13.729025 | 56.785740 | 1350.515991 | 27.193104 | 26.656279 | 22.755001 | 22.193867@0.726675 | 23.615532 | 43.363197/1.778546 | 6/0/0
12 | `E1-A1-X0` | `C0:1,1,0.612576,0.225152,0|C1:1,1,0.612576,0.225152,0` | 1.000000/0.612576/0.000000 | 13.733688 | 56.620575 | 1343.115967 | 26.676199 | 26.651512 | 22.734091 | 22.193758@0.726675 | 23.608986 | 43.400063/1.780447 | 6/0/0
13 | `E1-A1-X1` | `C0:1,1,1,1,1|C1:1,1,1,1,1` | 1.000000/1.000000/1.000000 | 13.737913 | 56.673038 | 1366.849609 | 27.227627 | 27.182255 | 22.734091 | 22.686533@0.500000 | 24.444401 | 42.923500/1.719816 | 0/0/0
14 | `E1-A0-X2` | `C0:1,0.312239,0.65612,1,1.785528|C1:1,0.312086,0.656043,1,1.785483` | 1.000000/0.656081/1.785506 | 13.751183 | 56.740944 | 1345.414673 | 26.807539 | 26.781345 | 22.879795 | 21.921387@0.579217 | 23.822071 | 45.426617/1.864092 | 6/0/0
15 | `E1-A0-X1` | `C0:1,0.312231,0.656115,1,1|C1:1,0.312061,0.656031,1,1` | 1.000000/0.656073/1.000000 | 13.751408 | 56.741650 | 1345.414185 | 26.807356 | 26.779785 | 22.879799 | 21.921366@0.579218 | 23.822073 | 45.410439/1.863487 | 6/0/0

## J. Best dynamic vs ConstantInner

BestDynamicTrajectory: `E0-A1-X0` / `C0:0,0.643448,0.321724,0,0|C1:0,0.643493,0.321747,0,0`.
BestDynamicFlying: **13.507465 s**; ConstantInnerFlying: **13.397707 s**; Delta dynamic - inner: **+0.109758 s**.
DynamicTrajectoryAdvantageObserved: **NO**; ConstantInnerDominates: **YES**; gameplay diagnostic tolerance: 0.020000 s/flying lap.

## K. Wide-exit trajectories

ExitRelease = actual exit lateral position - actual entry lateral position. `< -0.25` is inward finish, `-0.25..+0.25` approximately constant, and `> +0.25` outward release / wide exit; this grouping gives no bonus.
BestWideExitTrajectory: `E0-A1-X1` / `C0:0,0.636093,0.818046,1,1|C1:0,0.63668,0.81834,1,1`; Flying **13.581989 s**; delta to overall **+0.184282 s**; delta to ConstantInner **+0.184282 s**.
IsOverallBestWideExit: **NO**; WideExitBenefitObserved: **NO**.

## L. Entry/apex/exit actual geometry

Rank | Corner | Entry | p~.333 | p=.500 | p~.667 | Exit | Entry r | Apex r | Exit r | Entry v | Apex v | Exit v
---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:
1 | C1 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 31.000000 | 31.000000 | 31.000000 | 26.485676 | 21.503384 | 23.324699
1 | C2 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 31.000000 | 31.000000 | 31.000000 | 26.459095 | 21.503384 | 23.324699
2 | C1 | 0.000000 | 0.643448 | 0.321724 | 0.000000 | 0.000000 | 31.000000 | 32.174294 | 31.000000 | 26.485674 | 22.336945 | 23.324697
2 | C2 | 0.000000 | 0.643493 | 0.321747 | 0.000000 | 0.000000 | 31.000000 | 32.174374 | 31.000000 | 26.459095 | 22.337435 | 23.324697
3 | C1 | 1.000000 | 0.308535 | 0.154267 | 0.000000 | 0.000000 | 34.650002 | 31.563076 | 31.000000 | 26.508112 | 22.930634 | 23.365234
3 | C2 | 1.000000 | 0.307109 | 0.153554 | 0.000000 | 0.000000 | 34.650002 | 31.560474 | 31.000000 | 26.486647 | 22.921490 | 23.357115
4 | C1 | 1.000000 | 0.307120 | 0.153560 | 0.000000 | 0.000000 | 34.650002 | 31.560493 | 31.000000 | 26.487421 | 22.921705 | 23.357115
4 | C2 | 1.000000 | 0.308526 | 0.154263 | 0.000000 | 0.000000 | 34.650002 | 31.563059 | 31.000000 | 26.479858 | 22.880539 | 23.324697
5 | C1 | 1.000000 | 0.307114 | 0.153557 | 0.000000 | 0.000000 | 34.650002 | 31.560484 | 31.000000 | 26.514603 | 22.971420 | 23.397757
5 | C2 | 1.000000 | 0.308526 | 0.154263 | 0.000000 | 0.000000 | 34.650002 | 31.563059 | 31.000000 | 26.507919 | 22.930597 | 23.365234
6 | C1 | 1.000000 | 0.308613 | 0.154306 | 0.000000 | 0.000000 | 34.650002 | 31.563219 | 31.000000 | 26.486597 | 22.880646 | 23.324697
6 | C2 | 1.000000 | 0.308259 | 0.154129 | 0.000000 | 0.000000 | 34.650002 | 31.562572 | 31.000000 | 26.459095 | 22.880608 | 23.324697
7 | C1 | 0.000000 | 0.636093 | 0.818046 | 1.000000 | 1.000000 | 31.000000 | 33.985870 | 34.650002 | 26.988276 | 22.328205 | 24.128948
7 | C2 | 0.000000 | 0.636680 | 0.818340 | 1.000000 | 1.000000 | 31.000000 | 33.986942 | 34.650002 | 26.966444 | 22.329380 | 24.129854
8 | C1 | 0.000000 | 0.000000 | 0.366402 | 0.732804 | 1.000000 | 31.000000 | 32.337368 | 34.650002 | 26.565271 | 21.503384 | 23.452152
8 | C2 | 0.000000 | 0.000000 | 0.366402 | 0.732804 | 1.000000 | 31.000000 | 32.337368 | 34.650002 | 26.538845 | 21.503384 | 23.452152
9 | C1 | 1.000000 | 1.000000 | 0.612682 | 0.225363 | 0.000000 | 34.650002 | 33.236290 | 31.000000 | 26.675713 | 22.823252 | 23.660223
9 | C2 | 1.000000 | 1.000000 | 0.612116 | 0.224233 | 0.000000 | 34.650002 | 33.234226 | 31.000000 | 26.672674 | 22.782663 | 23.644289
10 | C1 | 1.000000 | 1.000000 | 0.612144 | 0.224289 | 0.000000 | 34.650002 | 33.234329 | 31.000000 | 26.672890 | 22.782808 | 23.643213
10 | C2 | 1.000000 | 1.000000 | 0.612684 | 0.225368 | 0.000000 | 34.650002 | 33.236298 | 31.000000 | 26.661045 | 22.775415 | 23.621859
11 | C1 | 1.000000 | 1.000000 | 0.612780 | 0.225559 | 0.000000 | 34.650002 | 33.236645 | 31.000000 | 26.666506 | 22.775909 | 23.622074
11 | C2 | 1.000000 | 1.000000 | 0.612576 | 0.225152 | 0.000000 | 34.650002 | 33.235905 | 31.000000 | 26.646051 | 22.734091 | 23.608990
12 | C1 | 1.000000 | 1.000000 | 0.612576 | 0.225152 | 0.000000 | 34.650002 | 33.235905 | 31.000000 | 26.665707 | 22.734091 | 23.608982
12 | C2 | 1.000000 | 1.000000 | 0.612576 | 0.225152 | 0.000000 | 34.650002 | 33.235905 | 31.000000 | 26.637318 | 22.734091 | 23.608990
13 | C1 | 1.000000 | 1.000000 | 1.000000 | 1.000000 | 1.000000 | 34.650002 | 34.650002 | 34.650002 | 27.196442 | 22.734091 | 24.444401
13 | C2 | 1.000000 | 1.000000 | 1.000000 | 1.000000 | 1.000000 | 34.650002 | 34.650002 | 34.650002 | 27.168066 | 22.734091 | 24.444401
14 | C1 | 1.000000 | 0.312239 | 0.656120 | 1.000000 | 1.785528 | 34.650002 | 33.394836 | 37.517178 | 26.788597 | 22.879835 | 23.822170
14 | C2 | 1.000000 | 0.312086 | 0.656043 | 1.000000 | 1.785483 | 34.650002 | 33.394554 | 37.517014 | 26.774092 | 22.879757 | 23.821972
15 | C1 | 1.000000 | 0.312231 | 0.656115 | 1.000000 | 1.000000 | 34.650002 | 33.394821 | 34.650002 | 26.787788 | 22.879835 | 23.822172
15 | C2 | 1.000000 | 0.312061 | 0.656031 | 1.000000 | 1.000000 | 34.650002 | 33.394512 | 34.650002 | 26.771780 | 22.879763 | 23.821974

## M. Radius diagnostics

Rank | Plan | Min radius m | Mean time-weighted radius m | Apex radius m | Exit radius m
---|---:|---:|---:|---:|---:
1 | `E0-A0-X0` | 31.000000 | 31.000000 | 31.000000 | 31.000000
2 | `E0-A1-X0` | 31.000000 | 31.782328 | 32.174332 | 31.000000
3 | `E3-A0-X0` | 31.000000 | 31.954945 | 31.561775 | 31.000000
4 | `E2-A0-X0` | 31.000000 | 31.955727 | 31.561775 | 31.000000
5 | `E4-A0-X0` | 31.000000 | 31.954947 | 31.561771 | 31.000000
6 | `E1-A0-X0` | 31.000000 | 31.956375 | 31.562897 | 31.000000
7 | `E0-A1-X1` | 31.000000 | 33.697845 | 33.986404 | 34.650002
8 | `E0-A0-X1` | 31.000000 | 32.599854 | 32.337368 | 34.650002
9 | `E4-A1-X0` | 31.000000 | 33.071686 | 33.235260 | 31.000000
10 | `E3-A1-X0` | 31.000000 | 33.071724 | 33.235313 | 31.000000
11 | `E2-A1-X0` | 31.000000 | 33.072319 | 33.236275 | 31.000000
12 | `E1-A1-X0` | 31.000000 | 33.072289 | 33.235905 | 31.000000
13 | `E1-A1-X1` | 34.650002 | 34.650002 | 34.650002 | 34.650002
14 | `E1-A0-X2` | 32.133331 | 34.351627 | 33.394695 | 37.517097
15 | `E1-A0-X1` | 32.133259 | 33.846321 | 33.394669 | 34.650002

Radii use the existing `LaneModel.TurnArcRadiusMeters(actual LateralPosition)` mapping. No new geometry is introduced.

## N. Correction burden

Rank | Plan | Correction distance m | Correction time s | Pre-apex m | Post-apex m
---|---:|---:|---:|---:|---:
1 | `E0-A0-X0` | 46.085346 | 1.921191 | 46.085346 | 0.000000
2 | `E0-A1-X0` | 44.096832 | 1.827320 | 39.001244 | 5.104650
3 | `E3-A0-X0` | 44.806110 | 1.860441 | 42.288765 | 2.447273
4 | `E2-A0-X0` | 44.806046 | 1.860435 | 42.286140 | 2.519907
5 | `E4-A0-X0` | 44.804283 | 1.860370 | 42.285156 | 2.379036
6 | `E1-A0-X0` | 44.875828 | 1.864385 | 42.254894 | 2.620934
7 | `E0-A1-X1` | 44.286308 | 1.796313 | 44.286308 | 0.000000
8 | `E0-A0-X1` | 46.902031 | 1.951995 | 46.902031 | 0.000000
9 | `E4-A1-X0` | 43.359619 | 1.778601 | 37.386673 | 5.991421
10 | `E3-A1-X0` | 43.355469 | 1.778387 | 37.397243 | 5.982643
11 | `E2-A1-X0` | 43.363197 | 1.778546 | 37.400757 | 5.962440
12 | `E1-A1-X0` | 43.400063 | 1.780447 | 37.400063 | 6.000000
13 | `E1-A1-X1` | 42.923500 | 1.719816 | 42.923500 | 0.000000
14 | `E1-A0-X2` | 45.426617 | 1.864092 | 45.426617 | 0.000000
15 | `E1-A0-X1` | 45.410439 | 1.863487 | 45.410439 | 0.000000

## O. Exit quality

Rank | Plan | Exit speed m/s | Speed +10 m m/s | Apex→exit s | First 20 m straight s
---|---:|---:|---:|---:|---:
1 | `E0-A0-X0` | 23.324699 | 23.880730 | 2.197930 | 0.837718
2 | `E0-A1-X0` | 23.324697 | 23.880730 | 2.220222 | 0.837718
3 | `E3-A0-X0` | 23.361176 | 23.914864 | 2.197714 | 0.836520
4 | `E2-A0-X0` | 23.340906 | 23.895878 | 2.199754 | 0.837186
5 | `E4-A0-X0` | 23.381496 | 23.934025 | 2.197710 | 0.835849
6 | `E1-A0-X0` | 23.324697 | 23.880730 | 2.201283 | 0.837718
7 | `E0-A1-X1` | 24.129402 | 24.629292 | 2.338234 | 0.812206
8 | `E0-A0-X1` | 23.452152 | 23.999107 | 2.318265 | 0.833577
9 | `E4-A1-X0` | 23.652256 | 24.185619 | 2.252110 | 0.827135
10 | `E3-A1-X0` | 23.632536 | 24.167044 | 2.252131 | 0.827772
11 | `E2-A1-X0` | 23.615532 | 24.151020 | 2.254158 | 0.828322
12 | `E1-A1-X0` | 23.608986 | 24.144871 | 2.254616 | 0.828534
13 | `E1-A1-X1` | 24.444401 | 24.923048 | 2.331152 | 0.802616
14 | `E1-A0-X2` | 23.822071 | 24.343426 | 2.340283 | 0.821763
15 | `E1-A0-X1` | 23.822073 | 24.343086 | 2.340282 | 0.821774

The +10 m and 20 m values use deterministic interpolation between production trace endpoints and do not change production stepping or timing.

## P. Lateral-acceleration proxy

Rank | Plan | Peak v²/r m/s² | Mean time-weighted v²/r m/s²
---|---:|---:|---:
1 | `E0-A0-X0` | 22.676760 | 17.270435
2 | `E0-A1-X0` | 22.650270 | 17.234791
3 | `E3-A0-X0` | 21.897419 | 17.815268
4 | `E2-A0-X0` | 21.075899 | 17.783842
5 | `E4-A0-X0` | 21.845331 | 17.846802
6 | `E1-A0-X0` | 21.069103 | 17.759533
7 | `E0-A1-X1` | 23.421106 | 16.794262
8 | `E0-A0-X1` | 22.810375 | 16.516827
9 | `E4-A1-X0` | 21.342190 | 17.548708
10 | `E3-A1-X0` | 21.395199 | 17.517658
11 | `E2-A1-X0` | 20.538591 | 17.488222
12 | `E1-A1-X0` | 20.537361 | 17.473656
13 | `E1-A1-X1` | 21.393097 | 17.147099
14 | `E1-A0-X2` | 21.399054 | 16.862722
15 | `E1-A0-X1` | 21.398911 | 17.077654

`v²/r` is named only a lateral-acceleration proxy. It is not tyre force, lean, yaw, slip angle or slip ratio.

## Q. Planner binding

Rank | Plan | Target transitions | Planned transitions | Actual waypoint arrivals | Binding count | Binding fraction
---|---:|---:|---:|---:|---:|---:
1 | `E0-A0-X0` | 0 | 0 | 36 | 0 | 0.000000
2 | `E0-A1-X0` | 16 | 16 | 28 | 0 | 0.000000
3 | `E3-A0-X0` | 16 | 18 | 13 | 16 | 0.444444
4 | `E2-A0-X0` | 16 | 17 | 15 | 13 | 0.361111
5 | `E4-A0-X0` | 16 | 19 | 11 | 19 | 0.527778
6 | `E1-A0-X0` | 16 | 16 | 24 | 0 | 0.000000
7 | `E0-A1-X1` | 16 | 16 | 24 | 0 | 0.000000
8 | `E0-A0-X1` | 16 | 16 | 24 | 0 | 0.000000
9 | `E4-A1-X0` | 24 | 19 | 11 | 18 | 0.500000
10 | `E3-A1-X0` | 24 | 18 | 13 | 15 | 0.416667
11 | `E2-A1-X0` | 24 | 17 | 15 | 12 | 0.333333
12 | `E1-A1-X0` | 16 | 16 | 24 | 0 | 0.000000
13 | `E1-A1-X1` | 0 | 0 | 36 | 0 | 0.000000
14 | `E1-A0-X2` | 24 | 30 | 8 | 9 | 0.250000
15 | `E1-A0-X1` | 16 | 16 | 27 | 0 | 0.000000

TrajectoryPlannerResolutionTooCoarse remains a separate search-resolution diagnostic; the global bound-plan fraction does not by itself identify why ConstantInner wins.

## Planner-independent trajectory subset

PlannerIndependentTrajectoryCount: **21**. Every member has `PlannerCapBindingCount == 0`.
Best planner-independent overall: `E0-A0-X0`; best dynamic: `E0-A1-X0`; best wide exit: `E0-A1-X1`.
Rank | Plan | Actual fingerprint | E/A/X | Flying s | 4-lap m | Exit m/s | Binding count
---|---:|---:|---:|---:|---:|---:|---:
1 | `E0-A0-X0` | `C0:0,0,0,0,0|C1:0,0,0,0,0` | 0.000000/0.000000/0.000000 | 13.397707 | 1275.115112 | 23.324699 | 0
2 | `E0-A1-X0` | `C0:0,0.643448,0.321724,0,0|C1:0,0.643493,0.321747,0,0` | 0.000000/0.321735/0.000000 | 13.507465 | 1294.965088 | 23.324697 | 0
3 | `E1-A0-X0` | `C0:1,0.308613,0.154306,0,0|C1:1,0.308259,0.15413,0,0` | 1.000000/0.154218/0.000000 | 13.565197 | 1314.822754 | 23.324697 | 0
4 | `E0-A1-X1` | `C0:0,0.636093,0.818046,1,1|C1:0,0.63668,0.81834,1,1` | 0.000000/0.818193/1.000000 | 13.581989 | 1325.357178 | 24.129402 | 0
5 | `E0-A0-X1` | `C0:0,0,0.366402,0.732804,1|C1:0,0,0.366402,0.732804,1` | 0.000000/0.366402/1.000000 | 13.617708 | 1297.541016 | 23.452152 | 0
6 | `E1-A1-X0` | `C0:1,1,0.612576,0.225152,0|C1:1,1,0.612576,0.225152,0` | 1.000000/0.612576/0.000000 | 13.733688 | 1343.115967 | 23.608986 | 0
7 | `E1-A1-X1` | `C0:1,1,1,1,1|C1:1,1,1,1,1` | 1.000000/1.000000/1.000000 | 13.737913 | 1366.849609 | 24.444401 | 0
8 | `E1-A0-X1` | `C0:1,0.312231,0.656115,1,1|C1:1,0.312061,0.656031,1,1` | 1.000000/0.656073/1.000000 | 13.751408 | 1345.414185 | 23.822073 | 0
9 | `E1-A2-X1` | `C0:1,1.684009,1.342004,1,1|C1:1,1.684228,1.342114,1,1` | 1.000000/1.342059/1.000000 | 13.848108 | 1388.097290 | 24.444397 | 0
10 | `E1-A2-X2` | `C0:1,1.681126,1.840563,2,2|C1:1,1.681229,1.840614,2,2` | 1.000000/1.840589/2.000000 | 13.942122 | 1418.596069 | 25.224293 | 0

## Planner-independent ConstantInner comparison

Role | Target plan | Actual fingerprint | Flying s | 4-lap production distance m | Diagonal extra m/lap | Diagonal time s | Actual E/A/X
---|---:|---:|---:|---:|---:|---:|---:
ConstantInner | `E0-A0-X0` | `C0:0,0,0,0,0|C1:0,0,0,0,0` | 13.397707 | 1275.115112 | 0.000000 | 0.000000 | 0.000000/0.000000/0.000000
Best PI dynamic | `E0-A1-X0` | `C0:0,0.643448,0.321724,0,0|C1:0,0.643493,0.321747,0,0` | 13.507465 | 1294.965088 | 0.327484 | 0.013665 | 0.000000/0.321735/0.000000
Best PI wide exit | `E0-A1-X1` | `C0:0,0.636093,0.818046,1,1|C1:0,0.63668,0.81834,1,1` | 13.581989 | 1325.357178 | 0.320135 | 0.013125 | 0.000000/0.818193/1.000000

Role | Entry/Apex/Exit radius m | Minimum/mean radius m | Entry/Apex/Minimum@progress/Exit speed m/s
---|---:|---:|---:
ConstantInner | 31.000000/31.000000/31.000000 | 31.000000/31.000000 | 26.472385/21.503384/21.458405@0.500000/23.324699
Best PI dynamic | 31.000000/32.174332/31.000000 | 31.000000/31.782328 | 26.472385/22.337189/21.884756@0.728275/23.324697
Best PI wide exit | 31.000000/33.986404/34.650002 | 31.000000/33.697845 | 26.977360/22.328793/22.296526@0.500000/24.129402

Role | Correction distance/time | Pre/post-apex correction m | Apex→exit s | +10 m speed | First 20 m s | Peak/mean v²/r | Brake/RunWide/Crash | Binding count/fraction
---|---:|---:|---:|---:|---:|---:|---:|---:
ConstantInner | 46.085346/1.921191 | 46.085346/0.000000 | 2.197930 | 23.880730 | 0.837718 | 22.676760/17.270435 | 0/0/0 | 0/0.000000
Best PI dynamic | 44.096832/1.827320 | 39.001244/5.104650 | 2.220222 | 23.880730 | 0.837718 | 22.650270/17.234791 | 6/0/0 | 0/0.000000
Best PI wide exit | 44.286308/1.796313 | 44.286308/0.000000 | 2.338234 | 24.629292 | 0.812206 | 23.421106/16.794262 | 0/0/0 | 0/0.000000

## Exit benefit versus total-lap cost

Compared with ConstantInner, planner-independent wide exit `E0-A1-X1` gains **+0.804703 m/s** at corner exit and **+0.748562 m/s** after 10 m, saving **+0.025512 s** over the first 20 m of the straight. It nevertheless loses **+0.184282 s** on the flying lap while production records **+50.242065 m** more over four laps.
Metric | Value
---|---:
ExitSpeedGain | +0.804703 m/s
TenMeterSpeedGain | +0.748562 m/s
First20mStraightTimeGain | +0.025512 s
FlyingLoss | +0.184282 s
ProductionDistanceDifference | +50.242065 m / four laps


## Can planner explain ConstantInner dominance?

PlannerIndependentDynamicTrajectoryExists: **YES**.
PlannerIndependentWideExitExists: **YES**.
PlannerCanExplainConstantInnerDominance: **NO**. A planner-independent dynamic path is sufficient to test the basic mechanism even though **96/125** target plans remain bound.

## R. Diagonal-distance omission estimate

Rank | Plan | Extra diagonal m/lap | Time equivalent s | Margin vs inner s
---|---:|---:|---:|---:
1 | `E0-A0-X0` | 0.000000 | 0.000000 | 0.000000
2 | `E0-A1-X0` | 0.327484 | 0.013665 | +0.109758
3 | `E3-A0-X0` | 0.317646 | 0.013087 | +0.147837
4 | `E2-A0-X0` | 0.317869 | 0.013107 | +0.158903
5 | `E4-A0-X0` | 0.318251 | 0.012974 | +0.158945
6 | `E1-A0-X0` | 0.317705 | 0.013108 | +0.167490
7 | `E0-A1-X1` | 0.320135 | 0.013125 | +0.184282
8 | `E0-A0-X1` | 0.351467 | 0.014755 | +0.220001
9 | `E4-A1-X0` | 0.344990 | 0.013976 | +0.310555
10 | `E3-A1-X0` | 0.344276 | 0.014066 | +0.321108
11 | `E2-A1-X0` | 0.344585 | 0.014089 | +0.331318
12 | `E1-A1-X0` | 0.344662 | 0.014097 | +0.335981
13 | `E1-A1-X1` | 0.000000 | 0.000000 | +0.340206
14 | `E1-A0-X2` | 0.660053 | 0.026977 | +0.353476
15 | `E1-A0-X1` | 0.360542 | 0.014736 | +0.353701

DiagonalDistanceOmissionCouldExplainWinner: **NO**. This `sqrt(ds²+dy²)-ds` estimate is observation-only and never feeds a simulated time.

## S. Surface-dependent trajectory ranking

Surface | Severity | Best | Best constant | Best wide exit | Best-constant s | Changed vs Uniform
---|---:|---:|---:|---:|---:|---:
OutsideCushion | 0.500000 | `E0-A0-X0` | `E0-A0-X0` | `E0-A1-X1` | 0.000000 | NO
OutsideCushion | 1.000000 | `E0-A1-X1` | `E2-A2-X2` | `E0-A1-X1` | -0.078392 | YES
MiddleCushion | 0.500000 | `E0-A0-X0` | `E0-A0-X0` | `E0-A1-X1` | 0.000000 | NO
MiddleCushion | 1.000000 | `E2-A2-X2` | `E2-A2-X2` | `E0-A1-X1` | 0.000000 | YES

SurfaceDependentTrajectoryOptimum: **YES**.
LineSwitchRequiresStrongSurfaceContrast: **YES**.

## T. Rider-skill-dependent trajectory ranking

Archetype | Best | Top 3 | Top-3 spread s
---|---:|---:|---:
Balanced | `E0-A0-X0` | `E0-A0-X0` (13.397707) → `E0-A1-X0` (13.507465) → `E3-A0-X0` (13.545544) | 0.147837
Technical | `E0-A0-X0` | `E0-A0-X0` (13.184128) → `E0-A1-X0` (13.301956) → `E4-A0-X0` (13.336290) | 0.152163
Low-control | `E0-A0-X0` | `E0-A0-X0` (13.637247) → `E0-A0-X1` (13.701071) → `E0-A1-X0` (13.734993) | 0.097746
Fast/Loose | `E0-A0-X1` | `E0-A0-X1` (13.021635) → `E0-A0-X0` (13.034470) → `E0-A1-X1` (13.085793) | 0.064157

SkillDependentTrajectoryOptimum: **YES**. SlideControl receives no added speed multiplier.

## U. Setup sanity

TractionBias | Best trajectory | Flying s | Apex m/s | Exit m/s | Correction m
---|---:|---:|---:|---:|---:
0.000000 | `E0-A0-X0` | 13.134113 | 22.093824 | 23.782902 | 44.146141
0.500000 | `E0-A0-X0` | 13.397707 | 21.503384 | 23.324699 | 46.085346
1.000000 | `E0-A0-X0` | 13.696623 | 20.859095 | 22.829872 | 48.229195

Gearing is neutral and production setup formulas are unchanged.

## V. Gameplay interpretation

1. Is constant L0 globally best among the studied trajectories? **YES**.
2. Can a narrow-entry/wide-exit actual trajectory beat constant L0? **NO**.
3. Does the best wide-exit path have a higher exit speed than constant L0? **YES**.
4. Does that speed compensate for production distance? **NO**.
5. Does optimum change with surface? **YES**.
6. Does optimum change with rider profile? **YES**.
7. Does the planner limit trajectory realization? **YES**.
8. Can missing diagonal distance change interpretation? **NO**.
9. Does a planner-independent dynamic trajectory exist? **YES**.
10. Does a planner-independent wide exit exist? **YES**.
11. Can planner binding explain ConstantInner dominance? **NO**.
12. Does the winning comparison use within-segment lateral change? **YES**.
13. Is changing radius fully integrated inside each segment? **NO**.
14. Is a corner-trajectory economics mismatch observed? **YES**.
Smaller radius requires more turning demand; wider release can allow higher modeled corner/exit speed. No real trajectory telemetry exists here, and the PGE dataset cannot select the #46 winner.

## W. Production limitations

Surface, radius, wear and path distance are sampled from segment-entry lateral position; production does not integrate changing within-segment radius/surface or diagonal/spiral distance. Actual waypoint positions between segment endpoints use a canonical observation interpolation. There is no yaw, lean, slip angle, tyre slip, traffic, blocking, overtaking or natural-meeting wear comparison in this benchmark.

## Segment-entry trajectory sampling limitation

WithinSegmentTrajectorySamplingLimitation: **YES**. Production resolves surface, segment length and `ContinuousCornerEnvelope.Create(...)` from segment-entry `LateralPosition`; it does not continuously resample radius, safe speed, surface or path length as lateral position changes within that segment.
DynamicPathUsesWithinSegmentLateralChange: **YES**.
Best planner-independent dynamic `E0-A1-X0` changes lateral position inside **4/6** observed turn segments.
Corner | Segment | Type | Entry lateral | Observed exit lateral | Delta lateral | Physical change m | Entry radius m | Observed exit radius m | Production entry surface G/R/M/E | Changed within segment
---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:
C1 | 1 | TurnEntry | 0.000000 | 0.643448 | 0.643448 | 2.348586 | 31.000000 | 33.348587 | 0.992500/0.030000/0.350000/0.979101 | YES
C1 | 2 | TurnMiddle | 0.643448 | 0.000000 | 0.643448 | 2.348586 | 33.348587 | 31.000000 | 0.995215/0.019140/0.350000/0.986643 | YES
C1 | 3 | TurnExit | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 31.000000 | 31.000000 | 0.992500/0.030000/0.350000/0.979101 | NO
C2 | 5 | TurnEntry | 0.000000 | 0.643493 | 0.643493 | 2.348750 | 31.000000 | 33.348751 | 0.992500/0.030000/0.350000/0.979101 | YES
C2 | 6 | TurnMiddle | 0.643493 | 0.000000 | 0.643493 | 2.348750 | 33.348751 | 31.000000 | 0.995250/0.019001/0.350000/0.986740 | YES
C2 | 7 | TurnExit | 0.000000 | 0.000000 | 0.000000 | 0.000000 | 31.000000 | 31.000000 | 0.992500/0.030000/0.350000/0.979101 | NO

Best planner-independent wide exit `E0-A1-X1` changes lateral position inside **4/6** observed turn segments.
Corner | Segment | Type | Entry lateral | Observed exit lateral | Delta lateral | Physical change m | Entry radius m | Observed exit radius m | Production entry surface G/R/M/E | Changed within segment
---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:
C1 | 1 | TurnEntry | 0.000000 | 0.636093 | 0.636093 | 2.321738 | 31.000000 | 33.321739 | 0.992500/0.030000/0.350000/0.979101 | YES
C1 | 2 | TurnMiddle | 0.636093 | 1.000000 | 0.363907 | 1.328262 | 33.321739 | 34.650002 | 0.995235/0.019060/0.350000/0.986699 | YES
C1 | 3 | TurnExit | 1.000000 | 1.000000 | 0.000000 | 0.000000 | 34.650002 | 34.650002 | 0.992500/0.030000/0.350000/0.979101 | NO
C2 | 5 | TurnEntry | 0.000000 | 0.636680 | 0.636680 | 2.323881 | 31.000000 | 33.323883 | 0.992500/0.030000/0.350000/0.979101 | YES
C2 | 6 | TurnMiddle | 0.636680 | 1.000000 | 0.363320 | 1.326119 | 33.323883 | 34.650002 | 0.995272/0.018913/0.350000/0.986801 | YES
C2 | 7 | TurnExit | 1.000000 | 1.000000 | 0.000000 | 0.000000 | 34.650002 | 34.650002 | 0.992500/0.030000/0.350000/0.979101 | NO

These observations can expose a delayed modeled benefit from opening the path; they diagnose sampling granularity and do not prove that it explains the entire time loss.

## Within-segment radius limitation

WithinSegmentChangingRadiusNotFullyIntegrated: **YES**. When actual lateral position changes during a turn segment, production still uses the entry-position radius, safe-speed context, surface and segment length for that segment rather than integrating them continuously along the observed path.
This leaves coarse within-segment radius, surface and path-geometry sampling as an unresolved explanation alongside correction economics and a possible later turning/slip-cost hypothesis.

## X. Failure-layer diagnosis

## Revised failure-layer diagnosis

Classification: `ConstantInnerDominates, NoWideExitBenefitObserved, TrajectoryPlannerResolutionTooCoarse, SurfaceDependentTrajectoryOptimum, LineSwitchRequiresStrongSurfaceContrast, SkillDependentTrajectoryOptimum, PlannerIndependentDynamicTrajectoryExists, PlannerIndependentWideExitExists, WithinSegmentTrajectorySamplingLimitation, DynamicPathUsesWithinSegmentLateralChange, WithinSegmentChangingRadiusNotFullyIntegrated, CornerTrajectoryEconomicsMismatch`.
First actual bottleneck: **corner trajectory physics economics**. The rule checks, in order: any dynamic actual path; a planner-independent dynamic path; a planner-independent outward-release path; its radius/apex/exit-speed signal; diagonal/path-distance accounting; and only then unresolved corner-trajectory economics.
PlannerCanExplainConstantInnerDominance: **NO**; CornerTrajectoryEconomicsMismatch: **YES**.
CornerAsymmetryUnexpected: **NO**; TrajectoryGeometrySignalWeak: **NO**.

## Y. Recommended #47 subsystem

## Revised #47 recommendation

Open exactly one next subsystem: **within-corner continuous trajectory/radius sampling experiment**. This is an experiment before any production rewrite. Do not infer a definitive slip model requirement or change `AdaptiveDecisionModel` until continuous trajectory/radius sampling has isolated the remaining economics.
Future #47 question: what happens to the ConstantInner versus narrow-entry / wide-exit ranking when the existing production corner model is observed in an experimental variant where effective radius, surface and path geometry change together with actual continuous `LateralPosition` inside the arc?
Fallback hypothesis: if continuous trajectory/radius sampling still leaves ConstantInner decisively best, investigate a separate `corner turning/slip cost` for tight-radius or higher-turning-demand riding. #46 does not implement or claim that model is required.

## Z. Frozen systems

Production behavior changed: **NO**. `AdaptiveDecisionModel`, `RiderDecision`, `LateralMovementModel`, `SegmentPhysics`, `ContinuousCornerEnvelope`, `LongitudinalDynamics`, `TrackEvolution`, `TrackState`, StandingStart, setup, wear, incidents, contact, Legacy, historical reports and `pge-v1` are unchanged. All calibration-only adjustments remain null. The report is deterministic, invariant-culture, LF-only, and derived from the production path.
