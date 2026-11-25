
# Study #219 · H38-D powered drift mechanics and independent corner control

Base 7385987c824be319e157ea836c8f045aa6a12258, fixture C, Motoarena.
The measured arc is the same 97.389 m reference bend used in #217
and #218. This study runs the spatial MODEL integrator. A measured
powered slide here means more than 20 N rear normal load, at least
6° of vehicle sideslip, at least +0.06 normalized rear wheelspin
and active Drive of at least 0.25 simultaneously. The official
strict sliding and sporting tests are distinct qualification levels.

## Archived baselines

The replay engine starts from the original translational, angular,
rotor, rider and contact state and submits the original commands.
Each submitted command retains its 50 ms activation delay. Where
the archive stopped on a controller forecast, the replay certifies
only its accepted prefix. An additional held command beyond
the recorded forecast boundary would be a different experiment.

The baseline check includes the 68.50, 77.50 and 78.57 km/h
complete bends, the 91.16, 89.96 and 88.47 km/h unfinished
frontiers, the selected #218 subsequent straight, and its four
published approach perturbations.

| Replayed entry | Bend time | Verified outcome |
|---:|---:|---|
| 68.50 km/h | 6.095 s | Balanced corner |
| 77.50 km/h | 6.246 s | Balanced corner |
| 78.57 km/h | 6.125 s | Balanced corner |
| 91.16 km/h | — | 13.136 m accepted after entry |
| 89.96 km/h | — | 15.063 m accepted after entry |
| 88.47 km/h | — | 27.831 m accepted after entry |

The selected straight replay reproduced 76.08 to 82.48 km/h
and the recorded sequence of physical states. All four reported
perturbations reproduced. The baseline verification used 45,374
accepted steps, 61.81 CPU seconds and 29.65 elapsed seconds.

## What the current tyre law computes

The rear model uses normalized longitudinal slip \(q_x\) and lateral
contact slip \(q_y\). The force vector is proportional to
\((q_x,-q_y)/\sqrt{1+q_x^2+q_y^2}\), with the total budget
\(C=\mu N\). The code solves \(q_x\) implicitly at the wheel
midpoint against drive torque, wheel inertia and bearing loss.
At fixed lateral demand and normal load, increasing positive
wheelspin increases the longitudinal allocation and reduces
the lateral allocation. The yaw effect depends separately on
the direction of the two force components and their contact arm.

The denominator's longitudinal scale combines the speed-based
slip stiffness term with a load-dependent mobility term and
the configured 30 ms minimum time. H35/H38 does not integrate
a separate delayed carcass shear state. The front force has zero
longitudinal component in this approximation. Its lateral law
uses actual projected velocity and the axle's load budget.
The map's available front yaw and roll bands are the local
half-step steering-rate envelopes. They differ from the actual
yaw and roll moment produced at the accepted state.

Camber rotates the model's wheel geometry, normal contact and
lever arm. The active law has no independent camber-thrust term
or measured twisting torque, and the older lean-scale option is
inactive in fixture C. The numerical map includes a separately
marked sensitivity using the older lateral shape scale. It does
not change the qualifying model. The geometry and pitch-aware
rear-wheel plus crank angular momenta are integrated. There is
no separately integrated front-wheel spin inertia, so real
front-assembly precession and the steering-axis response to
front wheel speed are beyond this model.

The coupled Drive cells use the existing Engine500 torque curve,
crank midpoint, clutch engagement and wheel midpoint solve.
The imposed axle-torque cells are local constitutive probes.
They do not represent a vehicle capable of maintaining every
combination of input slip, speed and normal load. The dynamic
circle starts have actual leg and rider mechanics and are
labeled synthetic. Velocity curvature is computed from COM
velocity and acceleration. Frame yaw rate is recorded separately.

### Calibration evidence

The rotating-disc motorcycle tests by Cossalter, Doria and
colleagues measure large camber-dependent lateral forces and
moments. The experimental riding-data study from Florence
identifies a model with camber, slip angle and longitudinal slip
as distinct inputs. Neither dataset identifies the Motoarena
surface or its current speedway tyre. [Padova rotating-disc measurements](https://www.research.unipd.it/handle/11577/2486330) ·
[Florence riding-data tyre study](https://doi.org/10.1177/09544070241247239)

The small-wheel dry-sand experiment measures longitudinal
and lateral forces jointly with sideslip and sinkage. More
recent high-sideslip tests likewise observe terrain effects
that a single friction budget cannot reproduce. These are
loose-surface examples, not measurements from a speedway dirt
track with a racing motorcycle. They motivate calibration
variables. They do not supply a transferable friction increase
or a validation of an alternate tyre law. [Dry-sand force measurements](https://doi.org/10.1016/j.jterra.2014.08.001) ·
[High-slip-angle wheel–soil measurements](https://www.sciencedirect.com/science/article/abs/pii/S0022489823000691)

## Experimental design

The force map spans 60–120 km/h, negative through high positive
rear slip, five rear slip angles, three loads, three lean angles
and three steering angles. The axle-torque, Drive and historical
lean-scale sensitivities use the central load and steering
slice. The synthetic starts at the actual inner, middle and
outer line radii include balanced running, moderate and large
initial wheelspin, rider actuation, foot contact and coast then
Drive. The rider study crosses 0.30–1.50 s of preparation,
four target postures and two Drive settings from identical
archived approach snapshots.

For each matched local Drive challenge the controller submits
one Drive value with steering and rider targets selected
independently. Three steering requests and up to three rider
positions share the same retained prior commands. The selected
temporary request is integrated over a local window and then
carried through a separate continuation toward the exit. A
verified local horizon is never counted as a completed corner.

The whole-corner controller uses the archived legal approaches
as state and line references. It starts replanning from a
mechanically reached checkpoint. It scores propagated velocity
heading, yaw momentum, lateral acceleration, roll rate, front
load and swept hard-part reserve. Drive comes from a separate
entry, distributed, slip-regulated, after-formation or exit
policy. Rider displacement is commanded separately. A full
command tape replays from the original approach. Geometry and
fresh approach holds are evaluated after the policy is frozen.
The model, coefficient, steering and rider budgets are shared
with the archive. The 60 ms decision cadence and offline
search timings do not establish FAST50.

## Observed force and contact response

The 15,960 mapped cells use a model friction coefficient of about
1.606. This is the fixture's existing effective coefficient. At
80 km/h, −28° frame roll, 900 N rear normal, 10° rear contact
slip angle and zero imposed axle torque, the midpoint response
was as follows. The slip column is the input ratio before the
implicit wheel midpoint solve.

| Input slip | Rear longitudinal | Rear lateral | Rear yaw moment | Rear utilization |
|---:|---:|---:|---:|---:|
| 0.00 | −0.07 N | 1,237.74 N | −402.20 N·m | 0.857 |
| 0.12 | 315.04 N | 1,207.97 N | −386.43 N·m | 0.864 |
| 0.35 | 790.37 N | 1,036.20 N | −295.43 N·m | 0.902 |
| 0.55 | 1,036.49 N | 862.46 N | −203.39 N·m | 0.933 |

At 0.55, lateral force retained 69.7% of the zero-input-slip
value and rear yaw-moment magnitude retained 50.6%. The front
moment band stayed fixed in this frozen-load slice. The coupled
Drive sweep at input slip 0.12 delivered 16.9–227.9 N·m of
axle torque over one midpoint. The immediate change in lateral
force was small, which separates changing throttle now from
having already developed high wheelspin. The dynamic tests
show the load transfer and slip developing over time.

The 54 synthetic starts use 37.93, 41.63 and 45.13 m radii.
Twenty-three maintained the 0.62 s integration window. Thirty-one
reached hard-part ground contact. The outer-line 80 km/h large
spin fixture measured 0.583 s of powered motion, 9.66 m/s²
of lateral acceleration and 79% rear contribution to the
inward force measured relative to velocity. Its initial
speed and radius imply 10.94 m/s², so the measured slide
does not establish exact constant-radius equilibrium.
The active-boot example measured 0.440 s powered motion
at 8.53 m/s². These are synthetic motion envelopes.

In the #218 approach forks at 85% Drive, forward posture
advanced the rider roughly 23 mm after the short preparation
and added 7.5 N of average front load against the neutral
command. The corresponding 50% Drive case advanced the
rider about 66 mm and added 18.9 N. Longer lead time can
increase realized travel, but the early high-Drive forks
frequently encounter the ground or step budget before their
intended endpoint. In the longer qualifying comparison on
the #217 70 km/h approach, forward posture added about
21 N and advanced the rider about 53 mm at 85% Drive.
Force saturated for 1.47 s in that case. Power did not
saturate. Passive contact load, actual target tracking,
steering and approach timing are all visible in the phased
records. The forward target does not offset the several
hundred newtons lost to high Drive.

## Matched requests and independent maneuver

All 60 matched requests produced a certified local command.
The 78 km/h −17 m coast/100% pair changed mean front load
from 638 to 293 N, rear normalized spin from 0.010 to
0.147, and mean local front yaw band from 24.14 to
11.04 N·m. At +48 m, the same comparison reduced mean
inward acceleration from 9.81 to 9.50 m/s², with front
load falling from 670 to 410 N. Positive wheelspin does
not imply greater inward acceleration.

Seven matched continuations reached a balanced bend.
The 78 km/h +87 m full-Drive request reached a 6.100 s
bend, 68.53 km/h minimum, 75.14 km/h exit and 81.44 km/h
at +20 m. It measured 0.180 s of continuous powered
sliding, with roughly 0.012 s at full Drive in the local
window. Its +20 m balance gate failed. Statement of this
result requires the full continuation evidence, including
the failed terminal condition.

The 60 first-stage independent trajectories all exhausted
the certified action set before the exit. The longest
accepted replay covered 74.82 m from a 78.58 km/h entry.
It preserved 1.36 m of exact swept track clearance and a
28.3 mm hard-part margin. This is a forecast refusal with
physical reserve at the last accepted checkpoint. It leaves
global mechanical reachability open.

The H37 powered comparison from proposal 30 provides a
dynamically reached positive witness. At 78.68 km/h entry
it completed the balanced bend in 6.468 s, with 65.57 km/h
minimum, 70.20 km/h exit and 79.24 km/h at +20 m.
It measured 2.647 s of loaded sideslip and 1.709 s at
the powered-slip threshold, including a 1.237 s continuous
interval. There was no full Drive overlap and it failed
the +20 m balance test. That trajectory kept the stock
tyre law and its measured rear contribution to inward
force was about 63%. It establishes a powered MODEL bend
under the stated diagnostic definition.

The second 60-proposal stage used that existing powered
trajectory to identify phase windows. It compared independent
Drive, steering and body response in the entry, established
slide and exit windows. The governor checks the propagated
angular response, front load, heading and swept reserve, and
falls back to the reference guidance when no pulse candidate
certifies. This is a short-window intervention around a
known physically reached state.

Twenty-four candidates reached +20 m. One passed its full
balanced-bend and +20 m gate while maintaining a sustained
powered interval. The winning pulse ran between reference
+26.82 and +42.82 m and coordinated a 0.25 minimum request
with lateral posture and an independently chosen steering
angle. Its original approach and delayed actions replayed to
the following exact result.

| Measurement | H38-D nominal replay |
|---|---:|
| Entry | 78.68 km/h |
| Reference arc time | 6.491 s |
| Minimum speed | 65.65 km/h |
| Speed at exit | 70.67 km/h |
| Speed at +20 m | 78.82 km/h |
| Sustained powered interval | 1.335 s |
| Total powered duration | 1.992 s |
| Total measured slide | 2.917 s |
| Full Drive overlapping powered slip | 0 s |
| Minimum swept track clearance | 2.145 m |
| Minimum swept chassis reserve | 7.15 mm |

The compact and large hard-part projections both certified
the same continuous commands and dynamic states. Minimum
chassis margins were 7.20, 7.15 and 7.10 mm for compact,
nominal and large geometry. The four fresh perturbations
all completed the balanced arc with 1.320–1.847 s sustained
powered slip. All four reached +20 m but exceeded the roll
gate there by 0.012–0.081 rad. Full Drive overlapping
powered slip reached 1.072 s on a recourse candidate that
did not qualify the whole maneuver. The winning powered
corner was slower than the 6.095 s baseline.

### Straight recovery at the #218 comparison horizon

The stricter instantaneous +20 m gate exposed a consistent
transient in the perturbations. The follow-through kept the
frozen controller, lane latch, physical states, pending FIFO
and decision clock. It continued each run for the same
2.10 s from the +8 m marker used in #218. The last 0.25 s
had to satisfy the roll and yaw-rate limits while maintaining
acceleration. The nominal run and all four holdouts passed.
Their final speeds were 79.48–79.54 km/h and final roll
magnitudes were 2.63–3.51°. All results occurred before
the next bend.

The nominal H38 run was faster at the +20 m station,
78.82 versus 77.77 km/h for the #218 reference. At the
matched later horizon it was slower, 79.52 versus 82.48
km/h for #218. The respective distances were +54.05 and
+54.45 m beyond the exit. This comparison distinguishes
fast early acceleration from the settled straight result.

[Nominal powered arc, original approach and commands](../../../data/experiments/speedway219/recourse-nominal.json.gz) ·
[Straight comparison and four frozen perturbations](../../../data/experiments/speedway219/straight-comparison.json) ·
[Powered motion comparison figure](../../../data/experiments/speedway219/corner-comparison.png)

## Required conclusions

1. **Can the stock tyre law sustain a physically plausible powered
   slide?** Yes in the MODEL, over measured time intervals.
   Both the dynamically reached comparison and the seeded loaded
   wheels sustained positive rear slip under drive. Transfer to
   real speedway tyres and track surface remains uncalibrated.
2. **How does wheelspin affect lateral force and yaw authority?**
   Fixed-load high wheelspin redirects the force budget toward
   propulsion and reduces rear lateral force and rear yaw moment.
   In dynamic high-Drive tests front load and the front steering
   yaw band also fell. The two effects can occur simultaneously.
3. **Can earlier posture preserve front normal load?** It can
   recover tens of newtons on some approach intervals. Tests
   at 85% Drive show the forward actuator reaching its force
   budget with much less realized motion than requested. None of
   these isolated body-position changes recovers the hundreds of
   newtons available by coasting. Passive pads, rider travel,
   front loading and steering response are coupled.
4. **Can the rear support cornering?** Yes. On the powered
   reference the rear contributes approximately 63% of measured
   inward force. Interpreting the contribution in the velocity
   frame matters, because the rear longitudinal force projects
   into that direction when the bike has sideslip.
5. **Can a complete bend contain substantial Drive and sustained
   slip?** Yes at the diagnostic level. The H38 nominal replay
   has 1.335 s of continuously loaded powered slip and follows
   through to stable straight acceleration. Full Drive was
   measured separately on shorter or unqualified combinations.
6. **What is the fastest robust complete powered bend?** In
   the tested set, 6.491 s from a 78.68 km/h entry, with all
   four frozen holdouts passing the later straight test.
   The four holdout bend times were 6.480–6.503 s. The
   6.095 s benchmark remains faster.
7. **What speeds did it achieve?** The nominal minimum was
   65.65 km/h, exit speed 70.67 km/h, speed at +20 m
   78.82 km/h, and speed at the later #218 comparison
   horizon 79.52 km/h.
8. **Where do the remaining limitations lie?** The first H38
   action set exhausts its preview before closure, a controller
   result. The stock denominator limits combined longitudinal
   and lateral force, and the geometry limits hard-part ground
   clearance. Front rotor precession, camber thrust, a tyre
   twisting-moment model and deforming soil are missing
   physical-model degrees of freedom. None of these limitations
   was changed during qualification.
9. **Is a credible complete 90 km/h bend reachable now?** The
   accepted frontier still comes from #218 at 88.47 km/h and
   27.83 m after entry among its in-band runs. This study did not
   repeat that speed grid or certify a complete 90 km/h bend.
10. **What remains from #218?** Strict sliding and sporting
    qualification, a complete 90–120 km/h progression, native
    validation, and a measured FAST50 controller remain open.

## Validation, cost and reproduction

| Experiment | Actual integration steps | Forecast steps | CPU seconds | Wall seconds |
|---|---:|---:|---:|---:|
| Archived replay and #218 holdouts | 45,374 | — | 61.81 | 29.65 |
| Force map and circle rigs | 12,428 rig steps | — | 28.14 | 16.37 |
| 60 matched Drive challenges and continuations | 90,754 | 600,721 | 962.30 | 200.69 |
| 60 first independent plans | 73,934 | 470,859 | 647.80 | 257.86 |
| 60 powered checkpoint recourse plans | 185,742 | 819,924 | 1,514.44 | 475.47 |
| Five straight suffixes | 4,688 | 15,414 | — | — |

The last two search timings include selected verification and
holdouts, while the step columns count their training executions
and forecasts. Command histories cover all 120 search proposals.
The independent Python verifier checks those histories, accepted
trajectory steps, the replay and straight boundary, tyre and
rider limits, and full-package digests.

The same targeted set was run against parent and current binaries
with the native guard. Both recorded 40 passes and 10 identical
failures caused by blocked native world construction. None of
these tests constructs a native world. The unguarded native
suite and competitive sporting validation remain outside this
study.

From the repository root in PowerShell:

```powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS = '1'
dotnet build tools/speedway-h26/H26.csproj -c Release -m:1
$dll = 'tools/speedway-h26/bin/Release/net8.0/H26.dll'
dotnet $dll drift-219 . out219 all
dotnet $dll drift-219 . out219 recourse
dotnet $dll drift-219 . out219 recover
python tools/speedway-h26/plot219.py out219
python tools/speedway-h26/verify219.py data/experiments/speedway219
```

Replay and test reports are linked below. Hosted CI is separate
from the local regression. The repository's primary CI workflow
uses main pushes, pull requests and manual dispatch.

[Force map data](../../../data/experiments/speedway219/force-maps.json.gz) ·
[Matched-state comparisons](../../../data/experiments/speedway219/matched.json.gz) ·
[Verification](../../../data/experiments/speedway219/verification.json) ·
[Parent tests](../../../data/experiments/speedway219/parent.trx.gz) ·
[Current tests](../../../data/experiments/speedway219/current.trx.gz) ·
[Evidence manifest](../../../data/experiments/speedway219/manifest.json)
