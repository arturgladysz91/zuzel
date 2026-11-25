
# Study #218 · Speed-dependent corner entry preparation

The starting state is Study #217 commit
6bf7b4afc773e8f6875fd3ebf6c691ddd545a651. The subject is the
same 97.389 m reference bend on fixture C. All results here
are solo MODEL results. The official sporting and strict sliding
profiles remain separate.

## Reconstructed approach

We replayed the archived 77.50 km/h completion and 91.16 km/h
failure from their original 40 km/h state 51 m before entry.
The independent replay reproduced 3,978 and 1,419 accepted
states, respectively, including positions, momenta, quaternion,
drivetrain and rider coordinates. All 216 issued commands
matched the reconstructed decisions, including the inherited
approach prefix. Each kept the 50 ms activation delay and the
original steering slew rate.

Angles are in degrees. Negative roll is inward. Actual curvature
derives from COM velocity and acceleration. Planned curvature is
the pure-pursuit command evaluated at the predicted activation
state. The telemetry includes complete command queues, tyre and
foot loads, torque, actuator usage and rider coordinates.

| Distance | Speed, 77 / 91 (km/h) | Actual roll, 77 / 91 (°) | Requested roll, 77 / 91 (°) | Front normal, 77 / 91 (N) | Active Drive, 77 / 91 |
|---:|---:|---:|---:|---:|---:|
| −50 m | 40.2 / 40.2 | 0.0 / 0.0 | 0.0 / 0.0 | 291 / 291 | 1.00 / 1.00 |
| −40 m | 56.1 / 56.1 | 0.0 / −1.6 | 0.0 / −2.0 | 201 / 203 | 1.00 / 1.00 |
| −30 m | 68.3 / 69.6 | 0.0 / −1.1 | 0.0 / +1.5 | 329 / 164 | 0.68 / 1.00 |
| −20 m | 73.8 / 81.1 | 0.0 / +1.7 | 0.0 / +6.0 | 467 / 203 | 0.40 / 0.86 |
| −10 m | 76.3 / 87.9 | −0.4 / +4.4 | −9.2 / −5.0 | 533 / 386 | 0.29 / 0.54 |
| −5 m | 77.0 / 89.8 | −8.7 / 0.0 | −27.1 / −41.8 | 481 / 416 | 0.26 / 0.43 |
| Entry | 77.5 / 91.2 | −31.0 / −13.3 | −46.4 / −58.8 | 516 / 415 | 0.18 / 0.34 |
| +5 m | 76.4 / 90.6 | −50.6 / −31.5 | −48.8 / −68.0 | 861 / 640 | 0.00 / 0.00 |
| +10 m | 72.8 / 88.4 | −48.0 / −46.9 | −42.9 / −71.5 | 733 / 772 | 0.00 / 0.00 |
| +15 m | 70.7 / — | −37.6 / — | −48.7 / — | 497 / — | 0.09 / — |
| +20 m | 69.6 / — | −47.4 / — | −53.7 / — | 694 / — | 0.13 / — |

The first 0.5° separation in frame roll occurred at −46.08 m.
Both bikes still had full Drive and about 46.47 km/h. The faster
plan targeted 8.319 m of track offset after retaining a seed at
8.600 m. The completed run held its 6.400 m approach offset.
It issued an early countersteer, then reversed the direction of
its planned curvature. At −20 m its target was 6° outward.
The frame reached 4.4° outward at −10 m. The final 3°
inward-bank crossing was at −3.56 m on the faster bike and
−7.30 m on the completed bike. Actual inward velocity
curvature crossed 0.005 m⁻¹ 2.44 m after entry on the failed
bike and 2.00 m before entry on the completed bike. At entry
the actual curvatures were 0.0039 and 0.0102 m⁻¹, respectively.
The faster bike reached −53.2° about 0.69 s after entry
and stopped at the chassis swept ground gate 13.14 m into
the bend.

The faster controller already asked for 12° inward at
−9.51 m, ahead of −8.79 m in the successful run. The adverse
outward preparation, front load and steering response explain
why the larger entry target did not produce the larger
achieved entry roll.

[Motion and curvature plots](../../../data/experiments/speedway218/approach-kinematics.png) ·
[Steering, tyres and power](../../../data/experiments/speedway218/approach-loads.png) ·
[Rider and clearance](../../../data/experiments/speedway218/approach-rider.png) ·
[77.50 km/h telemetry](../../../data/experiments/speedway218/archive-77.json.gz) ·
[91.16 km/h telemetry](../../../data/experiments/speedway218/archive-91.json.gz)

## Matched interventions

Each fork starts from the same accepted mechanical state, retains
its prior pending commands and applies the new request after
50 ms. The held-command control and each intervention advance
at most 0.27 s through the same limits.

At −20.81 m on the failed approach, the held command produced
236.7 N mean front load and 6.61 N·m mean front roll band.
Coasting raised these by 366.6 N and 10.52 N·m, at a
3.99 km/h cost versus the held request, and increased inward
bank by 2.89°. Full Drive lowered front load by 88.0 N
and the band by 2.48 N·m. At −10.98 m, increasing the
curvature preview by 9 m and replacing only the steering
request produced 19.81° more inward roll and 149.8 N·m more
inward mean front contact moment. The independent countersteer
test at that state produced 17.26° more inward roll.

| Proposed cause | Observation and matched result |
|---|---|
| A · Late countersteer | First steering past 0.3° outward occurred at −48.33 m on the failed approach and −11.49 m on the completed approach. The early action on the failed path was followed by reversals. A targeted countersteer at −20.81 m gave 5.48° more inward bank in 0.27 s. |
| B · Late roll initiation | Sustained inward roll and inward trajectory curvature were delayed by 3.74 m and 4.44 m, respectively. |
| C · Front roll authority | At −20 m the available front roll band was 5.6 vs 13.6 N·m. Coasting restored the matched band, and the stronger countersteer produced a local roll response even under the original Drive. |
| D · Front unloading | The Drive traces separated well before entry. Same-state coast and full-Drive forks causally changed front normal load and available roll moment. |
| E · Angle and rate limits | Actual steering stayed within 3.3° on both reconstructed approaches through +13 m, far inside the 35° travel limit. The slew cap was active in 558 of 1,419 accepted steps on the failed run and in 270 of 3,978 on the completed run. Rate-limited countersteering remained effective. |
| F · Latency | The same 50 ms delay corresponds to 1.27 and 1.08 m at the two entry speeds. The test keeps that delay. Earlier commands still have effects much larger than this distance difference suggests. |
| G · Curvature anticipation | The fast plan predicted outward curvature at −20 m and a steep inward target rise near entry. Longer preview moved the actual response earlier on a fork. Applying it near entry sometimes worsened the chassis margin. |
| H · Roll damping | On the failed fork at −20.81 m, halving damping produced 2.75° less inward bank, while increasing it by half produced 2.24° more. At −10.98 m halving gave 1.81° more, and at +5.87 m halving ended at the ground gate. |
| I · Rider posture | An earlier lateral target moved frame bank by less than 1° on the reported 91 km/h forks. A forward target added about 1–9 N front load and changed bank by at most 0.11°. The short windows cannot rank every pre-built posture. |
| J · Rear combined slip | Near +0.98 m, full Drive raised final normalized rear wheelspin from −0.018 to +0.231 and dropped mean front load by 401.5 N. The longer test must judge any slide benefit together with front authority and recovery. |
| K · Yaw, roll and velocity | At +5 m the faster bike had 2.8° yaw vs 14.2°, 1.0° vs 5.5° sideslip, and 0.0099 vs 0.0332 m⁻¹ actual inward curvature. |
| L · Chassis geometry | The faster run exhausted the hard-part ground margin while its accepted prefix still had 0.922 m track clearance. Extra countersteer at late forks often reached the ground earlier. |
| M · Racing line | A line correction and reversal of desired curvature precede the speed divergence. The preparation grid varies line and holding points using the same model. |
| N · Entry objective | The faster plan held Drive later and accepted reduced front load. New candidates are ranked by complete bend time and checked into the following straight. |
| O · Rotor speed | At −10.98 m the rear-speed sensitivity relative to the 77.50 km/h equivalent was −0.18 N·m, or −0.015 rad/s² of roll acceleration. At +5.87 m it rose to +3.80 N·m or +0.317 rad/s². |

The rotor calculation changes only the instantaneous angular
momentum transport term and holds frame rate, pose, contact
forces and crank momentum fixed. H35/H36 carries crank and
rear-wheel spin. The steered front geometry has no separately
integrated front-wheel spin inertia. The result quantifies the
gyro response of the modeled rotors. [Sharp, The Stability and Control of Motorcycles](https://doi.org/10.1243/JMES_JOUR_1971_013_051_02)

## Steady circular mechanics

Steady equilibria provide laboratory references. The approach
experiments start on the straight and develop their own speed,
yaw and roll. Measured lateral acceleration is compared with
v²/R. Combined COM lean uses the ground centre of pressure.

| Speed | Radius | Expected / measured acceleration | Ideal / frame / COM lean magnitudes |
|---:|---:|---:|---:|
| 40 km/h | 42.93 m | 2.876 / 2.876 m/s² | 16.3° / 16.9° / 17.0° |
| 55 km/h | 42.93 m | 5.437 / 5.437 m/s² | 29.0° / 29.9° / 30.0° |
| 70 km/h | 94.52 m | 4.000 / 4.000 m/s² | 22.2° / 22.8° / 23.0° |
| 80 km/h | 123.46 m | 4.000 / 4.000 m/s² | 22.2° / 22.8° / 23.0° |
| 90 km/h | 156.25 m | 4.000 / 4.000 m/s² | 22.2° / 22.8° / 23.0° |
| 100 km/h | 192.90 m | 4.000 / 4.000 m/s² | 22.2° / 22.7° / 23.0° |

At fixed 4 m/s², the modeled frame angle changed by 0.09°
from 70 to 100 km/h despite the rise in rotor speed.
Contact moments, distributed mass and aerodynamic resistance
account for differences among frame, COM and force angles.
The bounded equilibrium solver did not converge at 70 km/h
on radius 42.93 m. This solver result leaves transient
reachability open.

## Continuous preparation experiment

Each speed uses 50 distinct plans. The grid crosses preparations
at 50, 40, 30, 20 and 10 m before entry with five strategy
families and two settings of their pace, line and gains. The
wheels-only plans share the same straight equilibrium 64 m
before entry. The foot-equipped family has its own equilibrium
with the articulated leg and passive supports. In the separate
foot pair, both variants start from that exact leg-equipped state
and differ only in whether they request extension.

The candidates use a planned preview point, realizable steering
response after activation, a position-based Drive schedule and
ordinary constrained rider controls. The plan varies offset and
preview before the bend to let yaw and inward velocity curvature
develop under the tyre forces. Progressive Drive probes use a
lower bound on the command over a short entry window. They leave
wheelspin and the resulting tyre forces to the mechanical model.
The first proposal set is intentionally broad in onset, action
family and line. The model integrates it to the actual exit and
8 m runout.

The selection rule prioritizes the requested entry band (±2 km/h)
and shortest actual entry-to-exit time. A retained candidate
also has to reproduce the same states when replaying the exact
command tape under the spatial gates. Its 1.15 s continuation
from that actual terminal state must improve speed by at least
1 km/h and satisfy the roll and yaw-rate limits. Robustness uses
four independently initialized approach equilibria, crossing
±0.65 km/h and ±0.14 m of starting lane. One repeated tape is
a determinism check. The perturbed traces qualify separately.

### Preparation near 90 and 100 km/h

The 90 km/h grid completed zero of 50 proposals. Thirty-nine
ended at a forecast of chassis contact and 11 at the actual
swept chassis gate. The deepest accepted new preparation
entered at 89.96 km/h and traveled 15.06 m into the bend.
At entry the roll, roll rate and inward velocity curvature
were −25.2°, −63.7°/s and 0.0081 m⁻¹, compared with −13.3°,
−81.3°/s and 0.0039 m⁻¹ in the archived 91.16 km/h run.
The frame rolled another 12.9° by +5 m in the new run,
versus 18.2° in the archived run. Front normal load at entry
rose from 415 to 606 N. At +15 m the new run still carried
83.3 km/h, with actual curvature 0.0291 m⁻¹, front tyre
utilization about 0.90 and 0.4 mm swept chassis margin.
Earlier preparation improved the state before the physical
limit without producing a complete 90 km/h bend.

The second 90 km/h set tested 36 wider and earlier coast
plans with a clearance-aware roll target. Its cap uses the
projected hard parts at the predicted activation state. It
changes the requested steering response through the same
actuator and leaves integrated contact checks in force.
The deepest plan advanced 24.68 m after a 94.60 km/h entry.
It was outside the requested band and stopped on a chassis
forecast. Its final speed was 79.41 km/h. Twelve subsequent
trims used four distinct line/reserve combinations and reduced
only the approach speed target. The deepest exact replay
entered at 88.47 km/h and advanced 27.83 m. The approximate
proposal at 89.91 km/h advanced 26.83 m. Seven of the trims
stopped at a chassis forecast and five at the orientation
forecast. These were candidate failures before mid-corner.
All 98 proposals across the three 90 km/h sets are recorded.

The 100 km/h diagnostic set completed zero of 50 proposals.
The selected prefix entered at 98.63 km/h and advanced
11.40 m. Forty-five requests stopped on a roll forecast and
five on actual swept chassis ground limits. Across the grid
the highest reached entry was 100.15 km/h. The 100 km/h
diagnostics do not qualify a continuation beyond the exit.

[Same-speed preparation comparison](../../../data/experiments/speedway218/preparation-90.png) ·
[89.96 km/h preparation telemetry](../../../data/experiments/speedway218/speed-90-frontier.json.gz)

### Matched foot and power

At 70 km/h both variants completed with otherwise identical
mechanics, seeds and tuning. Requesting extension gave a
247.0 N peak foot load and a 922.0 N·s impulse. It produced
0.915 s of measured slide, including 0.218 s at Drive
of at least 0.25. The parked request produced zero measured
slide. The deployed variant added 0.227 s to the bend,
reduced minimum speed by 1.65 km/h and exit speed by
0.21 km/h. At 80 km/h the parked request completed and the
deployed request stopped on the chassis forecast. The parked
boot could still contact the track unilaterally under
compression and lean, reaching 103.2 N in that 80 km/h
test. This distinguishes parked actuation from removal of
the actual external contact.

### Nominal completions and perturbations

The 70 km/h grid produced 49 balanced search candidates.
Three rechecked candidates completed with nominal straight
acceleration. The fastest of those entered at 69.34 km/h,
held a minimum of 66.79 km/h, exited at 68.82 km/h and
covered the bend in 6.338 s. Its preparation began 50 m
before entry. It had 17.5 mm minimum chassis margin and
1.998 m minimum swept track clearance. At the end of the
additional straight the speed had risen from 74.39 to
87.12 km/h. Its 0.140 s of measured slide occurred under
partial or zero Drive below 0.25.

All four perturbations of each of the three retained 70 km/h
candidates completed the balanced bend within the band.
Only two perturbations per candidate also passed the
1.15 s stability-and-acceleration gate. The fastest retained
70 km/h candidate therefore passed nominal continuation and
4/4 balanced corners with perturbations, with 2/4 combined
corner and straight qualifications.

The base 80 km/h grid produced 18 balanced candidates. The
eight exact contenders all reproduced their bends and
increased speed in the aggressive continuation, but failed its
terminal roll or yaw condition. The fastest bend entered at
78.57 km/h, held a minimum of 68.46 km/h and exited at
70.67 km/h in 6.125 s. The archived 77.50 km/h bend took
6.246 s, held a minimum of 66.74 km/h and exited at 67.30 km/h.
This base comparison establishes a faster bend and motivated
the separate progressive straight-acceleration comparison.

Gradual Drive alone still left the fastest bend at +22.8°
of roll after 1.15 s and −19.9° after 2.10 s. Each interval
started from the same accepted state and pending command queue.
The targeted straight search used 24 steering and acceleration
settings from that state. Twelve passed nominal acceleration
and stability. The first ranked setting passed three of four
perturbations. The next setting passed all four. It keeps a
23 m preview, reduces the line-following roll target to 60%,
aims 0.50 m inward from the retained offset, and gradually
requests additional Drive. Its force and steering limits are the
original ones. The bend trajectory itself is unchanged.

With the selected terminal settings the motorcycle accelerated
from 76.08 to 82.48 km/h during the 2.10 s straight check.
Throughout the last 0.25 s it met the roll and yaw limits and
continued to gain speed. Final roll was −1.74° and yaw rate
+0.030 rad/s. Each holdout passed the same test with an increase
of 6.403–6.405 km/h. The 50 m plan completed the bend 0.121 s
faster than the archive and gained 3.37 km/h of exit speed.
This is a robust balanced MODEL bend and straight acceleration
result for the stated four perturbations.

[Complete bend comparison](../../../data/experiments/speedway218/complete-maneuver.png) ·
[78.57 km/h bend telemetry](../../../data/experiments/speedway218/exit-80-proposal-6.json.gz) ·
[Selected straight telemetry](../../../data/experiments/speedway218/terminal-80-runout.json.gz) ·
[Terminal search and perturbation results](../../../data/experiments/speedway218/terminal-80.json)

## Required conclusions

1. **Why did the 91.16 km/h entry have less roll?** The plan
   made an early line correction, then asked for outward
   curvature and reached −10 m with an outward frame angle.
   Its later larger inward request had to reverse the state
   while higher Drive reduced front authority. Inward velocity
   curvature and yaw developed later than on the 77.50 km/h run.
2. **Physical inability or request choice?** The 0.27 s matched
   forks establish room to develop roll earlier within the
   original mechanics. The same-state preview and countersteer
   tests added up to 19.81° and 17.26° of inward bank. A later
   chassis limit still appeared in the full approaches.
3. **Did acceleration unload the front?** Yes. At −20.81 m,
   keeping steering and posture fixed, coast increased the mean
   front load by 366.6 N and available front roll band by
   10.52 N·m. Full Drive had the opposite effect.
4. **Did rotor speed materially change initiation?** The measured
   frozen-state roll contribution of the extra rear spin near
   −11 m was −0.015 rad/s², small compared with the matched
   steering changes. The contribution grew inside the bend.
   The model omits separately integrated front-wheel spin.
5. **Was the curvature anticipated early enough?** The fast
   plan did prepare earlier, but its anticipated curvature
   first oscillated and then pointed outward. Earlier suitable
   curvature and steering improved reachable inward roll. The
   request still needs to fit available tyre loads and subsequent
   chassis clearance.
6. **Can earlier preparation reduce the rapid increase near
   entry?** Yes in the measured 89.96 km/h witness. It entered
   with 11.9° more inward bank, smaller inward roll rate and
   twice the measured inward path curvature. Its first five
   metres added 12.9° of bank, versus 18.2° on the archive.
   It still exhausted the chassis clearance later in the bend.
7. **Can yaw, roll and powered slide improve corner speed?**
   The measured full-Drive interventions generated rear slip
   together with a large front-load cost. The matched deployed
   boot produced powered slide at 70 km/h and lost bend time.
   The faster balanced 80 km/h witness did not use full Drive
   during slide. An improvement attributable to powered sliding
   remains unestablished by the matched experiments.
8. **Fastest robust complete bend?** The 50 m early-line
   setup entering at 78.57 km/h, followed by the selected
   progressive straight controller, completed in 6.125 s,
   exited at 70.67 km/h and passed all four tested changes
   to the dynamically reached approach. The nominal straight
   check ended at 82.48 km/h.
9. **Which limitations remain?** The sampled 90–100 km/h
   trajectories reached hard-part ground or track constraints,
   or had no verified continuation in the controller's
   finite preview. The 42.93 m steady-circle nonconvergence
   at 70 km/h is a solver result. Planned curvature, front-load
   allocation, steering response and post-exit stabilization
   are controller issues. Additional rotor and rider geometry
   calibration remains a physical-model question. Sport,
   strict sliding and native qualification remain outside
   these MODEL results.

## Evidence and reproduction

| Search | Proposals | Actual search steps | Forecast steps | Exact and perturbation steps | CPU / wall seconds |
|---|---:|---:|---:|---:|---:|
| 70 km/h | 50 | 239,740 | 685,665 | 95,088 | 1,107.55 / 784.89 |
| 80 km/h | 50 | 149,916 | 422,370 | 74,815 | 789.19 / 546.08 |
| 90 km/h | 50 | 88,417 | 242,588 | 1,682 | 184.02 / 58.04 |
| 100 km/h | 50 | 83,343 | 227,335 | 1,554 | 149.31 / 40.06 |
| 90 km/h wider line | 36 | 69,315 | 175,856 | 1,955 | 136.66 / 44.73 |
| 90 km/h speed trim | 12 | 24,383 | 61,659 | 2,096 | 50.53 / 25.78 |

The CPU and wall receipts include search and retained replay
work. The bounded straight search took 83.00 CPU and 25.65 wall
seconds, with 18,125 accepted terminal steps and 39,906
perturbation steps. All timings are offline. The matched archive pairs
reconstructed their full command timelines and all tested
mechanical state columns exactly. The 30 focused spatial, pitch,
roll, unified-dynamics and rider-support tests passed under the
zero-native-world guard. The independent verifier checks command
chronology, energy and moment accounting, actuator and tyre
budgets, spatial margins, perturbation evidence and file digests.
It verified 84,605 recorded approach and bend steps and
11,496 steps from 100 matched mechanical forks.

In PowerShell, run from the repository root with a new output
directory and the .NET SDK. The plotting tool uses Matplotlib.

```powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS = '1'
dotnet build tools/speedway-h26/H26.csproj -c Release -m:1
$dll = 'tools/speedway-h26/bin/Release/net8.0/H26.dll'
dotnet $dll preparation-218 . out218 all
dotnet $dll preparation-218 . out218 wide-90
dotnet $dll preparation-218 . out218 trim-90
dotnet $dll preparation-218 . out218 exit-80
dotnet $dll preparation-218 . out218 settle-80
dotnet $dll preparation-218 . out218 terminal-80
python tools/speedway-h26/verify218.py data/experiments/speedway218
```

[Manifest](../../../data/experiments/speedway218/manifest.json) ·
[Verification](../../../data/experiments/speedway218/verification.json) ·
[Matched probes and circle summary](../../../data/experiments/speedway218/diagnosis.json.gz) ·
[Focused regression log](../../../data/experiments/speedway218/focused-218.trx.gz)
