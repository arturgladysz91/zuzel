# Study #225 H44-R — Racing-Line Geometry, Exit Acceleration and Sustainable Racing

PROVISIONAL. Opt-in stock spatial MODEL experiment built on `fd085297ef590729e72879c1de936240121781ac`. H25 is the default. The native gate remains closed. Physical graduation requires the original 0.50 m whole-bike swept boundary allowance and strictly positive swept hard-part clearance.

## Experimental design

The track reference is station from the first geometric bend entry. A lap advances 318.778750 m in this coordinate. Time between complete crossing markers and the actual world-space distance travelled by the system center are distinct quantities. Four flying laps plus 22 m require station 1297.115 m. Every crossed marker retains the complete state and pending FIFO.

A positive offset goes outwards from the reference boundary. The primary frozen guide has EntryOutside 9.839 m, Inside 7.464 m and ExitOutside 6.343 m. Its effective entry knot also subtracts (0.30 \, mathrm{m}) per unit of the heading parameter. The guide falls from its inside knot toward the exit and recovers outward on the straight. The fastest study #224 schedule changes approach geometry and pace as well as switching to a third route at station 518.168 m. It retains the 6.343 m exit knot. Guide knots describe controller requests. Vehicle position, velocity direction and curvature come from the propagated motorcycle pose and momentum.

The H44 geometry screen included four families.

- **A** uses the H43 geometry. The first run reproduces the frozen command policy. The additional A comparisons vary pre-exit acceleration, balance reserve and anticipatory rider control with the same geometric guide.
- **B** approaches wide, turns toward an inner apex and opens toward the outer part of the exit.
- **C** uses larger radii and shallower lateral excursions to retain momentum.
- **D** varies entry width, apex position, exit width and preparation independently. The achievable geometry can be asymmetric about the middle of the bend.

The follow-up gradually blends families B–D from the H43 guide after the common entry. All families affect feedback targets only. A 60 ms review uses the current state and commands already in flight, and projected commands activate after the original 50 ms physical delay. A scaled throttle request during late sliding depends on front support, current travel direction, rear slip and remaining roll aperture. It can act while slip is still present. Longer runs add a posture target based on projected rearward travel, recent rear-wheel force and phase before and after exit. Stock unilateral supports, muscle force and power budgets remain in the plant. The proposed target supplies no force to the center of mass.

The ordinary-straight trials start 20 m before the first exit on the archived H41 frontier trajectory. To qualify, a run must reach its band on the ordinary straight, traverse the entire next corner, and continue for the required 22 m. The separate downstream stage starts from actually reached greater-than-100 km/h states with all prior inputs and pending commands preserved. The stages rank complete horizon results ahead of failed attempts.

## Reproduction and the four-lap screening discrepancy

Full-root replay reproduced the #224 35.946885 s pair, the original 36.289385 s frozen pair, the four old holdout tapes and the 970.901521 m continuation. Six of eight terminal records matched field for field. The initial travel stop and the outside-lower holdout had position differences of `4.84e-14` and `2.59e-11` m respectively. A separate live rerun of the frozen H43 policy reproduced all five original seeds, tapes, accepted terminal states and sampled states. H43 completed the two-lap recovery nominally and in three old holdouts. [Full-root reproduction](../../../data/experiments/speedway225/references.json) · [Live H43 comparison](../../../data/experiments/speedway225/reproduced-feedback.json)

| #224 plan | Screening station (m) | Full-root reduced / exact (m) | Exact from saved fork (m) | First rejected interval (s) | Lowest hard part | Swept ground (mm) |
| --- | ---: | ---: | ---: | --- | --- | ---: |
| 475 | 1297.129 | 936.109 / 936.109 | 965.341 | 58.713834–58.716334 | left peg | −0.853 |
| 476 | 1297.115 | 970.902 / 970.902 | 970.902 | 60.473834–60.476334 | left peg | −1.933 |
| 477 | 1297.129 | 962.996 / 962.996 | 963.935 | 59.888834–59.891334 | right peg | −2.067 |

The two clearance modes share the hard-part formula. The reduced mode can use the center disk for boundary clearance when it clears its broad-phase threshold. Replaying the same requests from the original seed with that reduced boundary calculation reproduced all three exact stops. The screening integration used the already accepted prefix, then issued commands at the actual review states. Full-root tape replay split integration at recorded request times. The first differences are on the order of `4e-14` m and `2e-12` kg·m/s, at the branch time for 475 and 477 and 1.680 s after it for 476. The exact-from-fork runs further isolate the downstream numerical sensitivity. The sub-picometer perturbations grow to materially different trajectories in these continuations. The three screening passes therefore give an insufficiently conservative indication of repeatability. There is no evidence here that the choice of boundary geometry caused the discrepancy.

The model candidate in each rejected interval still had positive endpoint peg height. The exact continuous bound subtracts half the interval transport bound and rejects the interval. At the three rejected endpoints roll was −0.9241, −0.9324 and +0.8078 rad, and pitch was −0.00069, −0.00049 and −0.00095 rad. Rider X was −0.0055, +0.0266 and +0.0254 m. Fore-aft rider momenta were −5.49, −4.04 and +0.29 kg·m/s. The large lateral rider momentum of case 477, −25.55 kg·m/s, coincided with outward roll. This identifies the part and the certification failure without inferring a peg strike between samples. Full endpoint states, midpoints, request times and pending FIFO are archived. [Matched replays and rejected intervals](../../../data/experiments/speedway225/discrepancies.json.gz)

## Line geometry and exit acceleration

The first 64-proposal sweep produced 18 screening completions and four exact two-lap horizons out of eight promotions. Gradual geometry handoff produced 16 screening completions and four exact horizons out of six promotions. The table compares the same complete lap from station 318.779 to 637.557 m. C covers only its available prefix on that lap. Tyre shear and vehicle resistance are integrated separately from the archived forces and midpoint momentum. The earlier stage field `TyreLossJ` contains both.

| Family and plan | Common lap (s) | Actual path (m) | Mean absolute curvature (1/m) | Tyre shear / resistance (kJ) | Lap peak (km/h) | Minimum exact boundary (m) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| A frozen H43 | 18.360 | 380.65 | 0.0185 | 48.30 / 48.94 | 89.21 | 1.817 |
| A 156, same guide with exit and posture feedback | 17.944 | 379.58 | 0.0182 | 52.89 / 50.39 | 91.14 | 1.824 |
| B 615, gradual wide-inward-outward | **17.833** | 378.01 | 0.0181 | 58.74 / 50.24 | 91.32 | 0.611 |
| C 628, wider radius | DNF at 416.10 m | 130.56 | 0.0235 | 22.31 / 16.01 | 86.76 | 0.502 |
| D 617, gradual asymmetric | 17.836 | 376.81 | 0.0180 | 53.88 / 49.91 | 91.24 | 1.705 |
| D 103, immediate asymmetric | 17.862 | 378.21 | 0.0179 | 56.83 / 50.25 | 91.06 | 1.389 |

B 615 is the fastest in the two geometry stages by 2.8 ms over D 617, with lower boundary reserve and greater tyre dissipation. B's pair took 35.701 s against the reproduced #224 best of 35.947 s. D 617 travelled 1.20 m less and dissipated 4.86 kJ less tyre shear work than B across the common lap. Using a ±0.0015 rad steering deadband, frozen A made 13 sign reversals in the first 22 m of the measured exit recovery. B and gradual D each made ten. Immediate D used 15 reversals in the same interval and reduced curvature and yaw in other parts of the exit. The result ranks trajectories on the tested seed. [Dissipation and reversal evidence](../../../data/experiments/speedway225/line-losses.json)

The old exit has unused performance on its own geometry. A 156 used 0.465 mean Drive over the last 24 reference metres of the launch bend, compared with 0.413 for frozen A. Exit speed rose from 77.18 to 79.38 km/h while rear slip remained near 0.26. At exit plus 22 m it was about 90.89 versus 88.91 km/h. The frozen H43 guide points inward through this phase, while the reached bike already has 1.29 m/s of outward velocity at the geometric exit. D 103 exited at 77.77 km/h with 1.99 m/s of outward velocity. Actual yaw rate at that exit dropped from 0.308 to 0.104 rad/s. Front normal load dropped from 287 to 197 N, illustrating the trade-off between powered lateral recovery and available steering force. Its mean absolute curvature in the 22 m recovery decreased from 0.0040 to 0.0037 1/m and mean Drive increased from 0.565 to 0.615. The outcome supports changing the exit phase before the geometric boundary, with results dependent on the whole motion through the next bend. The experiment measures the combined effect of the guide and its feedback settings. [Initial geometry screen](../../../data/experiments/speedway225/lines.json.gz) · [Gradual transition](../../../data/experiments/speedway225/transition.json.gz) · [Actual XY trajectories](../../../data/experiments/speedway225/racing-trajectories.png) · [Offset, speed, curvature and Drive](../../../data/experiments/speedway225/racing-line-metrics.png)

## Controller limits and ordinary-straight speed

Each diagnostic changes one rule from the active #224 planned route at the same reached state. The pending FIFO and force budgets are held fixed. Changes are counted only above 0.001 Drive across 17 checkpoints.

| Isolated change | Drive requests changed | Largest Drive change |
| --- | ---: | ---: |
| Raise StraightKph cap to 140 km/h for the matched diagnostic | 3 | +0.472 |
| Increase nominal TurnAcceleration by 4 m/s² | 11 | +0.657 |
| Increase assumed Deceleration by 3 m/s² | 8 | +0.166 |
| Disable the curvature-lookahead cap | 12 | +1.000 |
| Disable rear-slip multiplier | 2 | +0.068 |
| Replace soft lean floor by the physical orientation bound | 0 | 0 |
| Disable ExitDrive floor | 0 | 0 |

The lookahead and its assumed acceleration dominate the sampled suppression. StraightKph matters on selected straights. The lean and exit-floor interventions did not bind at these selected states. Independent Drive, steering and rider-target probes found at least one safe 240 ms full-Drive interval at each of the 17 checkpoints. These hold the physical delay and do not qualify the full next bend. [Matched checkpoints, requests, and controller-free command probes](../../../data/experiments/speedway225/matched-limits.json.gz)

| Requested band (km/h) | Screened | Peak achieved (km/h) | Exact next bend plus 22 m at that band |
| ---: | ---: | ---: | ---: |
| 100 | 12 | 100.87 | 0 |
| 105 | 12 | 103.56 | 0 |
| 110 | 12 | 107.43 | 0 |
| 115 | 12 | 108.36 | 0 |
| 120 | 12 | 102.06 | 0 |

The exact early-proposal speed maximum was 108.361 km/h (318), and the furthest station was 193.385 m (320). The downstream recovery retained the first exact 100 km/h crossing from 320 and 318 at stations 123.477 and 116.177 m. Across 56 continuations and six selected exact replays, plan 725 reached 210.781 m after a 102.228 km/h ordinary-straight peak. It still stopped 46.0 m before the next geometric exit at 256.779 m on swept boundary clearance. The previous 95.63 km/h complete-bend record therefore stands. The main obstacles in these runs were the direction and amount of lateral velocity at turn-in, projected peg clearance during roll correction, and the movement available before boundary recovery. Independent predictions show some controller headroom. The experiment leaves the global physical speed ceiling unresolved. [Speed screen](../../../data/experiments/speedway225/speed.json.gz) · [Actual high-speed forks and downstream recovery](../../../data/experiments/speedway225/speed-recovery.json.gz)

## Rider support and repeated motion

The old unassisted sequence did not show a slow drift to the travel limit. At station 725 m, the front carried 653 N, the pad vertical total was 992 N and X reserve was 0.382 m. At 730 m Drive reached 1.0 and front load fell to 343 N. At 738 m the rear carried 1965 N and the front 312 N. Both wheels and all five rider pads were unloaded near station 740 m, while Drive remained 0.880. Pitch rose to +0.3155 rad by station 744 m. A rear landing close to 745 m showed about 5148 N rear normal load and 3567 N pad vertical reaction. The rider target was neutral in X, and active power had reached 350 W. At the final accepted state, X was −0.35253 m, relative X speed −5.09 m/s, fore-aft internal momentum −180.31 kg·m/s and remaining X travel 7.47 mm. These measurements place the excessive travel after the high-Drive exit, airborne pitch and recontact, with the actuator power-limited at the end. Pad opening is consistent with unilateral support and does not demonstrate inadequate static support. Force, power, travel, contact and work residual all remain available in the archive. The original trace reached 700 N and 350 W and accumulated at most 0.00954 J of absolute energy error. [Pad loads, lag, power and momentum](../../../data/experiments/speedway225/rider-history.json.gz) · [Exit event plot](../../../data/experiments/speedway225/rider-exit-event.png)

The #224 longest exact sequence completed its first three laps in 17.929, 18.360 and 18.615 s and then advanced 14.565 m. Ordinary peaks declined from 91.59 to 89.21 to 86.80 km/h. At the next three entries, offset moved 6.929 → 7.233 → 7.657 m, inward speed fell 1.267 → 0.458 → 0.058 m/s, and rear-wheel momentum was 60.70 → 60.47 → 59.51 kg·m²/s. Roll moved −0.330 → −0.370 → −0.386 rad. The result fails both the required fourth lap and stable repeated pace. That trajectory includes a station-specific support change in lap three. H44 support and sustain trials apply the same state-dependent policy each lap after the common handoff. Exact phase comparisons include the active and queued commands, and the full archive permits checking state between crossings.

The A-family support study screened 56 proposals. Twenty cleared the four-lap horizon while screening, and eight initially selected candidates failed independent root replay. I then replayed all twelve remaining screening passes. They still completed the full horizon online with exact geometry, and all twelve stopped early on independent full-root replay. Plan 224 led this group at 800.672 m after exact replay, while its locally propagated run had reached 1297.121 m. The accepted exact prefix had 0.735 m of swept boundary margin and 1.728 mm of swept ground margin. After crossing into lap three, its sampled rearward travel reserve remained at least 0.369 m and muscle power was at most 11.1 W. The first rejected interval concerned the left peg near −0.923 rad roll, with −1.482 mm swept ground. This mirrors the #224 sensitivity and confirms that switching the online boundary calculation to exact geometry alone is insufficient to certify continuity from the seed. [Support candidates](../../../data/experiments/speedway225/support.json.gz) · [All remaining four-lap screening passes](../../../data/experiments/speedway225/support-exact.json.gz)

Fifty-six further proposals extended the two-lap B and D lines and an A control with the same periodic policy on each lap. Twenty-three screening traces reached four flying laps plus 22 m. None of the eight exact promotions qualified. D 802 recorded almost equal lap times of 17.863 and 17.862 s. From the first return to the second return its outward offset moved 8.023 → 7.903 m, road-aligned speed 23.599 → 23.591 m/s, inward speed 1.289 → 1.088 m/s and roll −0.429 → −0.420 rad. Rider X changed 10.99 → 10.49 mm and fore-aft momentum +0.042 → −0.035 kg·m/s. Full-root replay then stopped at station 785.360 m on a left-peg swept result of −0.091 mm. Plan 850 stopped at 782.289 m with the right peg governing. The longest exact H44 run was A 224, at 800.672 m. #224 remains the longest verified continuation at 970.902 m. Four flying laps plus 22 m remain unqualified and the prerequisite for a separate standing-start race was unmet. [Repeated-line screen and exact continuations](../../../data/experiments/speedway225/sustain.json.gz)

Across all exact runs eligible through both laps plus 22 m, family **A**, plan 225, was the fastest on the common complete lap at **17.661 s** and the fastest pair at **35.539 s**, 0.408 s ahead of the reproduced #224 pair. An independent two-lap run and full-root replay reached the required 22 m of continuation in **36.812 s** at station 659.570 m. Minimum swept clearances were 1.917 m to the boundary and 3.360 mm to the ground. The complete common lap followed a 371.24 m actual path. Its long attempt reached 773.585 m before the hard-part gate. B remains the fastest in the geometry-only comparison. The later A result uses anticipatory posture and roll reserve on the frozen A geometry, while the holdout result below applies to the independently frozen B policy. [Best qualified lap and pair](../../../data/experiments/speedway225/best-laps.json.gz) · [Complete tape for the best pair](../../../data/experiments/speedway225/best-lap-225-tape.json.gz)

## Feedback runtime and qualification

Offline proposals use multiple workers. The runtime comparison uses one decision worker on the selected B guide. It measures controller latency from the handoff and separates controller and exact integration CPU. The 50 ms physical reaction remains in the command schedule for every row. Parallel offline work was active on the host during some measurements.

| Physical inverse response window (ms) | Final station (m) | P50 / P95 / P99 / max (ms) | Misses over 50 ms / reviews | Controller / integration CPU (s) |
| ---: | ---: | --- | --- | --- |
| 65 | 659.559 | 44.48 / 160.17 / 198.25 / 223.22 | 178 / 373 | 25.094 / 103.063 |
| 40 | 344.166 | 84.00 / 288.22 / 1872.57 / 1872.57 | 59 / 81 | 8.141 / 31.250 |
| 30 | 334.061 | 59.67 / 192.33 / 872.70 / 872.70 | 45 / 70 | 4.813 / 18.391 |
| 22 | 333.034 | 54.46 / 154.03 / 279.89 / 279.89 | 43 / 69 | 3.672 / 17.469 |
| 16 | 332.001 | 42.81 / 115.41 / 368.94 / 368.94 | 15 / 68 | 3.266 / 16.047 |

The 65 ms response completed the nominal two-lap recovery. Shorter forecasts did not reach the finish and often triggered additional recovery forecasts in the first return bend. The nominal trial misses FAST50 and already exceeds the 40–60 s full-race CPU budget on the measured portion. These observations retain the existing qualification thresholds. Timings from the shorter failed rides describe their own horizons. [Single-worker measurements](../../../data/experiments/speedway225/runtime.json) · [Policy frozen before independent starts](../../../data/experiments/speedway225/feedback-freeze.json)

The fresh holdout results apply to B 615 with the full 65 ms response, frozen as 500 before testing. This choice was based on the geometry-stage timing and the nominal runtime screen. Holdout timings ran concurrently, with a single decision worker in each ride. They include computation on the online trace before full-root replay could reject part of it.

| Fresh start | Speed / offset / initial queue perturbation | H43 exact endpoint (m) | H44 exact endpoint (m) | H44 two laps plus 22 m | H44 P99 / misses |
| --- | --- | ---: | ---: | --- | --- |
| A | −0.34 km/h / −0.067 m / +3.4 ms | 317.169 | 318.630 | FAIL | 433.36 ms / 321 of 373 |
| B | +0.29 km/h / +0.056 m / +5.4 ms | 240.257 | 240.257 | FAIL | 412.36 ms / 306 of 373 |
| C | −0.19 km/h / +0.100 m / +7.6 ms | 317.607 | 318.976 | FAIL | 414.06 ms / 318 of 373 |
| D | +0.39 km/h / −0.074 m / +9.6 ms | 316.093 | 317.594 | FAIL | 533.43 ms / 314 of 373 |

Both policies continued through the linked endpoint in three fresh cases, and both reached the two-lap horizon in zero. H44 crossed one complete lap in C. The common H41 prefix itself fails on the right peg in B at 240.257 m, before the 244.605 m handoff. A, C and D advance a further 1.37–1.50 m under H44 and stop on the left peg. All four H44 online calculations reached the target before their root replays disagreed. The fresh evidence supports an exact certification failure in these executions and leaves transfer to other starts unresolved. [Fresh matched starts](../../../data/experiments/speedway225/holdouts.json)

The original first acceleration window, next bend and recovery tests pass on the retained B, D and A legal examples. The later first-exit acceleration test is still false on these examples. The 102.228 km/h continuation fails the complete second bend and recovery. A spatial MODEL pass on a timed lap remains separate from these sporting gates and from native autonomy. [Original gate results](../../../data/experiments/speedway225/sporting-gates.json)

The parent and final tool builds both pass the same 50 filtered MODEL tests. The same ten native-required tests stop at the explicit Box2D construction guard. The final tool built in Release with zero warnings and errors after locked restore. The GitHub CI workflow triggers on main pushes, pull requests, scheduled runs and dispatch. The final published SHA's hosted check status is reported with the publication receipt. [Local test receipts and archive](../../../data/experiments/speedway225)

## What limited the result

Spatial MODEL qualified a faster nominal pair and confirmed stronger acceleration before straight alignment. Exit load transfer, turnover to the incoming heading, and the reachable roll and steering response limit the tested recovery. Peg clearance and the swept boundary reject distinct motions even with useful tyre force and engine reserve. The tests also expose three controller and validation constraints. A scalar curvature envelope withheld available Drive at many reached states. Several exit and entry references left little room for the actual lateral motion. Full-root replay exposed enough time-partition sensitivity to disqualify locally accepted four-lap and holdout runs. The study establishes these mechanisms in the tested trajectories. It leaves the existence of a repeatable four-lap solution and a complete following bend above 100 km/h unresolved.

## Reproducibility

~~~powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS = '1'
dotnet restore tools/speedway-h26/H26.csproj --locked-mode
dotnet build tools/speedway-h26/H26.csproj -c Release -m:1 --no-restore
$dll = 'tools/speedway-h26/bin/Release/net8.0/H26.dll'
foreach ($stage in 'references','discrepancies','limits','support','lines','transition','support-exact','sustain','speed','speed-recovery','runtime','holdouts','best-lap') {
    dotnet $dll racing-225 . out225 $stage
}
python tools/speedway-h26/pack225.py out225 data/experiments/speedway225
dotnet $dll racing-225 . data/experiments/speedway225 verify
dotnet $dll racing-225 . data/experiments/speedway225 gates
python tools/speedway-h26/pack225.py out225 data/experiments/speedway225
python tools/speedway-h26/verify225.py data/experiments/speedway225
~~~

In the reported run the 56-proposal support screen began with the frozen A geometry while the line screen was still running. To reproduce that support screen run its mode before the line aggregate exists. The published stage stores every proposal and its seed route. Checkpoints allow a search stage to resume without repeating completed proposals. Release build, pause-safe output directories and local regression receipts are recorded alongside the evidence.

The tape files preserve each original mechanical seed and all command payloads, including requests still pending at the terminal state. Profile rows retain actual line, velocity, attitude, contact forces, muscle work and current FIFO. Discrepancy tapes retain the same full command histories and can be replayed in both geometry modes. Bundled parent references point to versioned #224 tapes for full histories during old holdout reproduction.

[Evidence, plots, controller traces and replay tapes](../../../data/experiments/speedway225)
