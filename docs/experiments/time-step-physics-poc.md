# Fixed-time 2D race PoC — decision report

Base: Draft #81, `5008f5f8e3fad997b5bc2eff3d8066b14e5d04d5`. Date: 2026-10-16.
**Recommendation C — retain the current engine rather than replace it with this
PoC.** The simpler scheduling works, but neither the real two-second target,
contact convergence nor a performance improvement is established. This is an
experimental result, not a proof that every fixed-time architecture is inferior.

## 1. What becomes simpler?

One double clock, four immutable physical values and six pair lifetimes replace
independent segment clocks, integration tokens and retained forecast chains.
The default base step is 10 ms, with a separate global 100 ms control schedule.
Every force evaluation reads the same four accepted values. A quadratic motion
interval covers the whole step; #55 supplies broad phase, swept first contact and
interval-wide geometry bounds. Contact splits the motion, changes velocity/yaw,
then the remaining time executes with fresh forces. There is one small rollback
of four unaccepted values for bounded local halving, not an accepted-prefix replay.

This simplifies orchestration, **not the hard continuous-contact problem**.
Reusing #55 directly still pays for its near-contact geometry searches. When
the simple normal-contact solver cannot prove an admissible continuation, it
returns the last safe state and a concrete rejected interval, without moving
positions apart or classifying numerical uncertainty as a physical crash.

## 2. What code and state does it require?

`tools/time-step-physics-poc`: **432 lines of runtime, 86 of scenario adapters,
112 of benchmark/CLI code**, plus the project file and about 108 test lines.
The existing `CoreSim`, `HeatSimulator`, coordinator and calibrations have **no
source changes**. Only solution/test links and this report/measurement CSV are added.
The tool uses the existing offline-audit assembly-access convention.

A bike stores metric position, full velocity vector, heading, angular velocity,
current driver command, transient control loss/deadline, status and double distance.
Longitudinal/lateral speeds are projections of that actual vector. No independent
`lateralPosition` displacement is integrated. Road projection only reads position.

| Existing component | Use / architectural limit |
| --- | --- |
| `FourRiderCausalCoordinator`, `FrozenHeatContext` | Exact #81 starting fixture and reference benchmark only; absent from new runtime scheduling |
| `ExecutedPathTraversal`, `AdaptiveDecisionModel` | Whole-segment traversal/route forecasts are not a time-step force API; runtime retains imported intent and uses a small controller |
| `LongitudinalDynamics` | Existing drive-force functions and resistance coefficients, divided by actual rider+bike mass |
| `TrackMetricEmbedding`, `BikeGeometry`, `ContestedSpace` | Existing metric track, chassis/handlebar capsules, sweep detection and bounds |
| `PhysicalContactConsequenceResolver` / #56C1 | Reuse manifold, mass, reserve, severity and control-loss mapping; do not apply its scalar-speed projection |

The new contact is a frictionless planar rigid-body normal impulse:
`J = max(0, -(1+e)*relativePointNormalVelocity / effectiveInverseMass)`, including
lever arms and yaw inertia. Both velocity components and angular velocity change.
Every impact/support impulse checks translational+rotational kinetic energy;
restitution remains the existing zero. Four bounded normal-constraint passes
carry ongoing unilateral contact without refreshing severity/recovery. Support
acts only within the existing 1 micrometre contact distance, never across a gap,
and cannot push separating bodies. A new consequence requires an actual accepted
release witness above the existing 0.1 mm numerical separation tolerance; no timer
cooldown is added. This PoC lifetime is not production #56's 0.65 m / 0.45 s policy.

New **PROVISIONAL** assumptions: lane response 0.8 s; lateral slip relaxation
0.25 s; yaw response 0.2 s; yaw acceleration bound 4 rad/s²; visual lookahead
1 s / range 8 m / threat distance 2.2 m; requested evasive offset 1 m; new-event
recovery 0.5 s; uniform-box inertia `m*(length²+barWidth²)/12`. Visual delay uses
the existing 0.20–0.28 s standing-reaction function as an explicit uncalibrated
proxy. Tyre lateral acceleration follows speed and body-heading/slip error;
drive/braking and lateral demand share a friction circle derived from the existing
safe-corner-speed relationship. Reference slide angle supplies a provisional
neutral slip, not a calibrated tyre law. Crash/Retired bodies retain motion and
skid under grip-limited dissipation; the PoC has no articulated fall model.

## 3. Does the real R3–R4 case work without corrections?

**Not for two seconds.** Import preserves each accepted metric pose, heading,
post-consequence scalar speed, profile/setup, frozen surface and impairment.
The original plan is not reapplied. Full velocity follows the accepted travel
heading and post-effect speed; yaw rate is inferred from the last geometric
interval. The old model never measured the missing post-impact lateral/yaw state.
Native positions remain unchanged when labelled with the common latest native
time, **4.180639183848264 s**; native skew is only 29.38 ns, but this is still an
explicit initialization approximation. Incoming recovery duration is estimated
from remaining segment distance/speed, rather than presented as measured data.

| Step | Accepted further time | Final absolute time | New contacts | Outcome |
| --- | ---: | ---: | ---: | --- |
| 5 ms | 0.03046875 s | 4.211107933848264 s | 0 | Geometry uncertainty blocks further motion |
| 10 ms | 0.085 s | 4.265639183848263 s | 0 | Geometry uncertainty blocks further motion |
| 20 ms | 1.0753125 s | 5.2559516838482425 s | 7 | Subsequent contacts/Crash, then geometry refusal |

At 10 ms the rejected interval has sampled separation **+1.014 micrometres**,
but lower bound **-103.806 micrometres**: this is lack of a proof, not observed
centimetre penetration. At 20 ms a later rejected sample penetrates **0.1344 mm**;
it is not accepted. Accepted endpoint penetration is at most **0.4953 micrometres**
in these runs; every accepted swept interval also retains the independent
**1 micrometre lower-bound guard**. No positional correction, collision disabling
or artificial retirement is used to count the real target as passed.

## 4. Do parameters actually affect motion?

Yes: targeted comparisons change skill/setup, canonical rider mass and frozen
surface grip and observe actual speed/position changes. Existing speed/control
skills enter force/corner capability; mass enters acceleration, inertia and impact;
canonical technique/strength/condition/grip enter the reused stability reserve.
Preferred line is an intent, never a position assignment. This is not a calibration
claim or a claim that every historical tactical attribute is implemented.

## 5. Which step is acceptable?

For free motion, 10 ms is a useful **research default**: after one common second,
its largest position difference from 5 ms is 0.133 mm on the straight and 1.044 mm
in the corner; 20 ms gives 0.399 / 3.112 mm. Lateral-contact differences are
4.207 / 11.837 mm. These are trajectory differences between models' resolutions,
not physical overlap. **No step is validated for the real contact sequence.**
5 ms stops much earlier than 20 ms; choosing the longer-running coarse version
would hide nonconvergence. Encroachment also stops early at 5 ms, while 10/20 ms
complete. A diagnostic split of `Run(2)` into two calls can change difficult-contact
branching as well; the timed protocol always uses the single `Run(2)` tested path.

Straight, corner, side, rear and close-miss cases complete two seconds at every
step. The side, oblique encroachment and rear scenarios produce actual impacts;
the close miss produces none. A 250 m/s stress exposure detects and mechanically
resolves a within-step rear hit rather than tunnelling. Reversed collection and
processing orders produce identical states/events for the same inputs/call protocol.

## 6. Measured cost

Windows / .NET 8 Release, optimized JIT (`DOTNET_TieredCompilation=0` for **both**
engines), one scenario warmup, median of three fresh short runs. Capture and #54
planning are outside continuation timings. No detailed observer runs inside them;
primitive integer work counters remain enabled. CPU uses process time with
15.625 ms resolution: CSV zeros mean below timer resolution, **not zero CPU**.

| 10 ms scenario | Executed duration | Elapsed median | Physics evaluations | Exact narrow evaluations | Thread allocation |
| --- | ---: | ---: | ---: | ---: | ---: |
| Straight | 2 s | 8.600 ms | 800 | 6720 | 3.15 MB |
| Corner | 2 s | 21.005 ms | 800 | 44076 | 3.29 MB |
| Side impact | 2 s | 23.834 ms | 832 | 57037 | 3.56 MB |
| Encroachment | 2 s | 32.220 ms | 832 | 84062 | 3.68 MB |
| Rear impact | 2 s | 44.035 ms | 804 | 94014 | 9.47 MB |
| Close miss | 2 s | 18.911 ms | 800 | 41652 | 3.14 MB |
| Real R3–R4 | **0.085 s** | **42.868 ms** | 104 | 128514 | 0.74 MB |

Every completed 2 s run has 80 driver-command updates and **zero new #54 route
plans**. An isolated existing #54 probe costs **4.5994 ms** median; it is not
silently charged to, or omitted from, a supposedly identical planning workload.

For the same imported scenario/common end **4.1836476179597195 s**, #81 costs
**0.2352 ms / 89,744 bytes**, versus PoC **1.4439 ms / 54,272 bytes**. #81 executes
four native forecasts, 172 coupled evaluations and **139 narrow tests / one
geometry call**; PoC executes four movement/force updates and **4131 narrow tests /
two geometry calls**. Its force primitive differs, so those evaluation counts are
not equivalent units of arithmetic. The current PoC has **no demonstrated speedup**;
near-contact geometry dominates. We do not compare a rejected old 3 ms run with a
new 2 s run as a percentage acceleration.

[Full 21-row measurements, chronology and common-horizon deltas](time-step-physics-poc-measurements.csv).
For failed scenarios the position comparison uses only a common reached prefix
(real: 20 ms; encroachment: 200 ms), not states at different stopping times.

## 7. Decision and reproduction

**C for this PoC.** Keep #81 and the existing physical primitives; do not replace
the experimental engine with this implementation. Scheduling is shorter, but the
contact lifetime/constraint solver and the existing observer's ambiguity/bounds
remain difficult, tyre/yaw/fall dynamics are provisional, and performance/stability
do not support replacement. This does not imply #81 itself completes a heat.
The next useful architectural experiment would need a separately justified robust
continuous-contact treatment; adding another layer of cursor/certificate machinery
or enlarging tolerances to manufacture a result is not done here.

Five targeted facts/theory cases pass, including the 21 scenario/step combinations
and reversed runs. They test mechanics, bounds and **safe refusals**; they do not
certify that the failed real two-second criterion passed. The original 39 source
consumer hashes remain exact. Final Release compilation has zero warnings/errors;
standard Draft Windows CI is required. The requested one-build budget was exceeded:
five solution Release builds plus targeted recompilations were needed while fixing
the prototype/measurement bugs. No historical matrix or historical audit was run.

Run `dotnet build SpeedwayManager.sln -c Release`, then
`dotnet test tests/CoreSim.Tests -c Release --no-build --filter FullyQualifiedName~TimeStepPhysicsTests`.
For measurements set `DOTNET_TieredCompilation=0` and run
`dotnet tools/time-step-physics-poc/bin/Release/net8.0/CoreSim.Tests.dll results.json`.
`--probe` replaces the output path for a short real-case-only diagnostic.
