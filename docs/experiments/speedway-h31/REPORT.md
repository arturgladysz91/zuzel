# Study #212 — Wide-open throttle and rider posture

The stock H28-F MODEL can slow down in a controlled slide at full drive, with positive net crank torque. It can also complete repeating flying laps with short full-drive pulses on ordinary straights or in bends. Both qualified policies were slower than #211. The independent steering search found no complete lap on constant 100%, 90% or 80% drive. A failed command sequence establishes where that sequence ran out of clearance or failed a technique rule. The search covered a finite steering family on one track and surface.

H25 remains the default. Study #212 runs in the research harness on `experiment/speedway-212`, based on published #211 commit `bb9c42443d4772166b632570b5562af4fe2c11bd`. It uses no native worlds. The strict #211 nominal MODEL receipt matched exactly for finish time, minimum swept clearance, last progress and step count.

## What Drive means

`Drive` is a normalized combustion command. Contact computes `Drive × (1 − impairment)`. The crank solve applies that multiplier to the RPM-dependent combustion curve and accounts for mechanical and pumping losses, crank inertia, clutch, gearing and wheel slip. At full drive, net torque follows the full-open map and falls towards the 12,000 RPM cutoff. The wheel can rotate faster than the road speed, and the limiter never clamps vehicle speed. The control is a throttle proxy with a linear combustion assumption. It has no calibrated grip-angle, manifold-pressure or carburetor-flow model. The evidence records requested drive, active drive, net crank torque and wheel torque separately. [Contact solve](../../../tools/speedway-handling/HandlingContact64.cs) · [Crank and clutch ledger](../../../tools/shared-world-speedway/StandingStart144/CrankClutch144.cs)

The PZM manual describes forward weight transfer and loading the left leg during entry. It describes a small lift on tight tracks and entry without that lift on gentler bends. Bend and exit technique vary with the surface, lean and how the rider supports or straightens the bike. Cieślak describes modern riders holding full throttle, while Loram describes trimming excessive wheelspin and reopening on a grippier patch. These are qualitative observations with no measured target duty cycle. [PZM methodology, pp. 97–103](https://pzm.pl/pliki/zg/zuzel/2026/metodyka_szkolenia_a5ver_z_160322.pdf#page=100) · [Marek Cieślak interview](https://sportowefakty.wp.pl/zuzel/847358/zuzel-od-szczakiela-do-zmarzlika-kiedys-100-tysiecy-ludzi-teraz-wieksza-predkosc) · [Mark Loram](https://www.visordown.com/features/guides/how-control-speedway-bike-mark-loram)

## Controlled protocol

Every repeating-lap arm begins at 18.020 s and 318.875 m from the same physically reached checkpoint in nominal fixture C. The frozen #211 feedback policy drives the complete prefix through the first crossing. The checkpoint carries the actual position, 80.74 km/h velocity, yaw, steering, wheel and shaft speeds, posture, history and pending command. Policy changes still pass through the ordinary activation queue. Results clip to the next two line crossings. Time from the seed to the next crossing omits the 0.097 m crossed in the last warm-up substep.

The first search uses independent geometric steering feedback around cutback, middle and outside racing lines. Their target apex offsets are 6.0, 9.3 and 12.1 m. Each arm and line tunes steering and, where permitted, drive separately. The later pulse sweep uses #211 feedback with additional phase-local steering and drive corrections. This separately tests whether a WOT sequence has a complete continuation under a controller with established race pace. Qualification uses finish time among complete attempts. The longest throttle interval has no selection bonus.

The diagnostic profiles each omit one restriction, covering straight position, entry position, straight course, straight yaw, correction, turn and slide history. A combined diagnostic omits all seven. Each keeps the .50 m swept margin and the original actuator, progress, sideslip, load and energy checks. The official profile keeps all strict rules.

### Independent steering search

| Drive arm | Furthest strict progress | Complete |
| --- | ---: | ---: |
| Constant 100% after activation | 370.90 m | 0 |
| Constant 90% after activation | 372.10 m | 0 |
| Constant 80% after activation | 373.45 m | 0 |
| WOT through the straight, managed bends | 375.46 m | 0 |
| WOT on the main part of straights, earlier transition | 441.19 m | 0 |
| WOT during entry and slide, managed elsewhere | 369.76 m | 0 |
| Joint drive, steering and sagittal posture | 438.68 m | 0 |
| Variable drive and steering with stock posture | 438.90 m | 0 |

The scan covers 3,168 candidates. Swept reserve was the first stop for all 216 matched replays of the selected arms. Removing the technique restrictions left the best progress and first stop unchanged. The constant 100% cutback run approached the first bend at about 109.5 km/h. It was still accelerating at about 115.2 km/h as it ran out of outer swept reserve. The model kept positive loads and solved the contact. This establishes a speed, line and preparation failure for the tested command. Available bar angle at the stop cannot establish the front tyre's yaw authority under load.

### Where the ordinary-straight reductions come from

Separating the launch and final straight changes the interpretation of the frozen #211 duty cycle. On ordinary straights the #211 MODEL averages 26.5% active Drive with zero full-drive time. It runs on full drive for 2.51 s on the launch straight, 0.20 s in the bends and 0.75 s on the finishing straight. Durations use active Drive ≥0.9999 and clip at the geometry and finish crossings. The #211 controller uses speed feedback, caps and corner preparation. The constant-drive tests show what happens when it cannot shed speed before the next bend. The pulse tests show that bounded full-drive operation is compatible with the model when the rest of the trajectory prepares for entry.

On the coarse late-bend pulse, the strict model stops at 784.01 m on 4.08° of course error against its 4° limit. The bike has 1.12 m outer clearance at that step. Omitting only the course restriction moves the first stop to the straight yaw rule at 786.45 m. Omitting all seven technique restrictions completes the same control law in 48.914764 s with 0.548 m minimum swept reserve. The course and yaw rejection of that trajectory is artificial. The clearance stops from the independent steering search occur with the technique conditions omitted as well.

Classifications use the first failing check. A positive swept-clearance result below the required reserve is classified as a planning failure for the tested control. An actual envelope crossing, sideslip or loss of progress is a physical failure of that trajectory under the model assumptions. A contact solver or certificate error is separate. A demonstrated missing degree of freedom or steering saturation would require an isolated intervention that changes the result. Unused steering travel alone cannot make that attribution.

### Complete flying laps with full-drive pulses

| Strict MODEL policy | Flying lap 1 | Flying lap 2 | Finish from standing prefix | Δ vs #211 | Flying swept minimum |
| --- | ---: | ---: | ---: | ---: | ---: |
| Frozen #211 control | 15.43144 s | 15.49144 s | 48.94288 s | 0 | 0.597 m |
| Repeated straight pulse | 15.44076 s | 15.49364 s | 48.95440 s | +0.01152 s | 0.597 m |
| Repeated bend pulse | 15.45445 s | 15.51899 s | 48.99345 s | +0.05057 s | 0.597 m |

The strict search completed 30 of 219 straight pulse proposals and 1 of 323 bend pulse proposals. Within each arm, selection used the fastest complete policy. Both final policies also complete with each diagnostic omission individually. The shared standing prefix brings whole-run minimum swept reserve to 0.558 m. The approximate complete-lap times in the table use the common sampled start at 18.020 s.

| Policy | Ordinary straight active WOT | Bend active WOT | Final straight active WOT |
| --- | ---: | ---: | ---: |
| Frozen #211, flying portion | 0.000 / 9.319 s | 0.000 / 20.611 s | 0.750 / 0.993 s |
| Straight pulse, flying portion | 0.600 / 9.340 s | 0.006 / 20.596 s | 0.744 / 0.998 s |
| Bend pulse, flying portion | 0.000 / 9.342 s | 0.452 / 20.640 s | 0.748 / 0.992 s |

The evidence supports short, repeatable WOT periods on this fixture. The overlays repeat across four homologous bend passages while the reference controller retains its finishing behavior. The trials did not establish a sustained high-WOT periodic state under the strict profile. Neither pulse policy replaces #211.

### WOT while slowing in a slide

The strongest local strict witness reaches the test from the frozen #211 run, 16 m into bend five. With a small steering adjustment and active Drive at 1.0, the model slows from approximately 76.54 to 74.52 km/h over 0.21 s. Speed-derived acceleration ranges from −4.54 to −1.02 m/s². Net crank torque stays between 50.39 and 56.97 N·m at about 4,970–7,677 RPM. Beta stays between 15.77° and 17.63°, with positive yaw and over 5.54 m swept clearance during the interval. The lead-in activation delay is included in the trace.

The front contact and road/air resistance remove travel-direction speed throughout the interval. The rear force initially also opposes travel even though it drives forward in the bike frame. Rear longitudinal slip rises from 0.035 to 0.651 and dissipates approximately 872 J. The front load stays between 499 and 670 N and the rear load between 772 and 943 N. The crank delivers positive torque well below cutoff. The entire 1.45 s rollout stays in that bend, with 1.36 s of active WOT and 5.18 m minimum swept reserve. The speed starts rising again during the later part of the rollout.

## Rider-body and tyre mechanisms

| Mechanism | Current H28-F model and Study #212 evidence |
| --- | --- |
| Independent fore-aft weight | Stock H28-F cannot command it. The experiment adds a bounded, independent ±0.18 m rider displacement with a 0.50 m/s slew and an independent crouch request. Both alter axle loads through center-of-mass geometry. The paired runs measure its effect under the same steering and throttle policy. |
| Independent lateral rider movement | The planar mass model lacks a lateral rider coordinate. It cannot reproduce the coach's lateral body position or balance corrections. |
| Dynamic roll and lean | A rate-limited lean surrogate changes lateral compliance. It has no roll moment, roll inertia or rider–chassis roll coupling. |
| Pitch and unloading | The solver includes instantaneous acceleration and height based load transfer, requiring two positive axle loads. Pitch dynamics, suspension and valid front-lift continuation are absent. |
| Unilateral left-foot contact | The model has two tyre contacts. The coach's entry technique with ground-supported weight is unavailable. An experimental contact would need nonnegative normal reaction, separation and impact conditions, bounded friction and a work ledger. |
| Internal footrest, seat and bar forces | The model has no separate rider free body. The sagittal experiment only changes the lever arms relative to the combined mass and uses tyre forces through those arms. It injects no rider or footrest force into the world. |
| Camber | Lean affects lateral compliance through a surrogate. No independent tyre camber force or contact geometry is modeled. |
| Combined slip at high wheelspin | H28-F couples longitudinal and lateral rear force in the same budget. The accepted slide shows longitudinal slip loss with substantial lateral force. The tyre response is provisional and has no high-slip dirt-track calibration in this study. |
| Engine torque and RPM limiting | The existing engine, losses, clutch, ratios and cutoff remain active. The slowing WOT witness occurs below cutoff. The fixed-100% outer-clearance stop likewise occurs below cutoff. |
| Track-dependent traction | The fixture uses the original uniform surface, effective grip 0.9175 before the existing contact scale. The MODEL obtains one fixed track sample. The study establishes no inference about grippier patches, rut effects or other tracks. |

The sagittal prototype is quasi-static. The shared state integrates the combined center of mass with total tyre and drag force. The fore-aft shift translates both axle moment arms by `rider mass / total mass × rider displacement`. Static front loading rises when the rider moves forward at fixed wheelbase and axle loads sum to weight. The prototype omits chassis recoil and displacement velocity at the contacts, variable yaw inertia, rider actuator work and full body dynamics. It uses a conservative swept deduction for the chassis translation. The study tests sagittal load sensitivity within these assumptions.

From the matched reached state in bend five, both rider commands use the same full-drive request, steering law and crouch. Forward travel keeps at least 305 N on the front and completes the 1.45 s horizon. Rearward travel drops front load to 21.9 N before the solver certifies that no positive-load two-contact root exists. Bend three shows the same directional contrast. The rearward run in bend four falls below swept reserve, while forward survives the local horizon. This isolates a real sagittal load effect in the experimental model. Independent posture did not rescue the repeating-lap joint control in the geometric search.

## Evidence and validation

The Release harness built with warnings as errors. Nine focused physics and strict-planner tests passed. The archived baseline matches the frozen #211 nominal MODEL receipt. The audited baseline, flying runs and posture/slide probes conserve axle weight to the serialized precision. Across these traces the maximum per-axis momentum residual is 0.000372 N·s per step, and the maximum crank/clutch ledger residual is 7.76 × 10⁻¹² J per step. Native acceptance remains the existing #211 result.

[Protocol and common checkpoint](../../../data/experiments/speedway212/protocol.json) · [Steering search](../../../data/experiments/speedway212/steering-search.json.gz) · [Matched steering replays](../../../data/experiments/speedway212/matched-replays.json) · [Pulse search and gate replays](../../../data/experiments/speedway212/pulse-search.json.gz) · [Coarse pulse and gate replays](../../../data/experiments/speedway212/coarse-bend-pulse-search.json.gz)

[Sector metrics](../../../data/experiments/speedway212/metrics.json.gz) · [Classifications](../../../data/experiments/speedway212/classifications.json.gz) · [Strict straight pulses](../../../data/experiments/speedway212/strict-straight-pulses.json.gz) · [Strict bend pulses](../../../data/experiments/speedway212/strict-bend-pulses.json.gz) · [Strict slide witness](../../../data/experiments/speedway212/strict-slide-witness.json.gz) · [Validation](../../../data/experiments/speedway212/validation.json)

## Reproduce

Run from the repository root with a fresh output directory for every command.

```powershell
dotnet build tools/speedway-h26/H26.csproj -c Release -m:1
dotnet run --project tools/speedway-h26/H26.csproj -c Release --no-build -- throttle-212 . runs212
dotnet run --project tools/speedway-h26/H26.csproj -c Release --no-build -- slides-212 . slides212
dotnet run --project tools/speedway-h26/H26.csproj -c Release --no-build -- laps-212 . laps212
python tools/speedway-h26/analyze212.py runs212 slides212 metrics212.json --laps laps212
```
