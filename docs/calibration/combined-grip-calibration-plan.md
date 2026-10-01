# Combined-grip calibration and data-acquisition plan (#50)

Status: **PROVISIONAL / PLAN ONLY / OUTCOME C / NO FIT PERFORMED**.
Date: 2026-10-01. Code inspected at
`9a82e86685b767859a2891ae3ab0dd619d883877`; main
`58771bac635d7d4962e11aa3ae5ad37a2d0c8d94`.
See the [real-data audit](combined-grip-real-data-audit.md) for sources, quality,
access limits and the decision to stop before numerical calibration.

## 1. Meaning of the current parameter

This section is algebra derived from the current implementation, not a measured
tyre law or a code change. The existing owner is
[`CombinedGripAvailabilityExperiment.cs`](../../src/CoreSim/Analysis/CombinedGripAvailabilityExperiment.cs).
Its effective lateral scale is `C = v_settled(24 m)² / 24 m`, derived from the
existing settled envelope, including provisional rider/setup/surface factors.
Its longitudinal reference is the current at-speed engine force, not an
independently measured maximum tyre force.

Use `l = v² |kappa|` for lateral acceleration demand, `P(v)` for full at-speed
drive force, `A` for existing progress availability, `R(v)` for resistance,
`m` for system mass, and `phi` for the availability factor. For a positive-drive
interval without the separate correction action, the implemented force balance is:

```text
C, l: m/s²;  kappa: 1/m;  P, R: N;  m: kg;  A, q, phi: dimensionless
u = l / C
phi = sqrt(max(0, 1 - q * u²))
F_requested = A * P
F_use = P * min(A, phi)
m * a_long = F_use - A * R
```

The last equation deliberately retains **A-scaled resistance**. Replacing it
with unscaled resistance would silently fit a different model. In the current
surrogate `m = 142 kg` and `R(v) = 40 + 0.20*v² N`; these are unchanged code
assumptions, not empirical measurements supplied by this audit. For real data,
the mapping from physical losses/request to this abstraction must be justified
independently. Unknown banking, wind, engine braking, surface losses, rider
choice or contact cannot simply be assigned to q.

q=0 means no coupling and directly uses the existing replay/integrator path.
q=1 applies the full *effective*, normalized ellipse-like restriction. It does
not establish a physical friction circle: the two axes are an engine reference
and a game settled capability, not measured matching tyre-force intercepts.
q is a dimensionless surrogate coupling strength, **not a friction coefficient**.
At `u > 1`, q=1 makes positive capacity zero while the existing recovery
controller still permits overspeed. This known limitation must not be hidden
by fitting to a convenient crossover.

## 2. Identifiability and equivalent parameterizations

The factor depends on `q` and `C` through only one combination:

```text
beta = q / C²                         units: s⁴/m²
phi = sqrt(max(0, 1 - beta * l²))
a_cut = C / sqrt(q)                   units: m/s², for q > 0
phi = sqrt(max(0, 1 - (l / a_cut)²))
```

At q=0 there is no finite cutoff; no numerical infinity need be stored in code.
Changing `C` to `lambda*C` and q to `lambda²*q` preserves the factor wherever
the transformed q remains in its allowed domain. Consequently, fitting C and q
freely from the same observations is structurally underidentified. A game's
fixed C permits a conditional q, but does not make that C a measured physical
capacity. Maximum observed `v²|kappa|` is not automatically lateral capacity.

**Recommendation:** leave the formula and q parameter unchanged in this PR.
For future research, estimate/report beta or `a_cut` first, if the force/request
anchors are established; then map to `q = beta*C²` using an explicitly frozen
capacity convention. This is an equivalent single-parameter representation,
not another tyre subsystem. It removes the misleading interpretation of q as
a material property; it does not solve unknown throttle, engine force or losses.

Under the stated force-balance assumptions, with `P > 0` and known A, define:

```text
z = (m*a_long + A*R) / P = min(A, phi)
```

Three branches must be distinguished:

| Observation branch | What can be inferred, only with independent anchors |
| --- | --- |
| Capacity-active, `0 < z < A`, `l > 0` | `beta = (1-z²)/l²`; conditional `q = (1-z²)/(l/C)²` |
| Request-limited, `z = A`, `l > 0` | Only `beta <= (1-A²)/l²`, or `q <= (1-A²)/u²`; equality is not a force-capacity measurement |
| Zero-capacity boundary, `z = 0 < A`, `l > 0` | Conditional lower bound `beta >= 1/l²`; a point value is not identifiable beyond the clamp |

At `l = 0` the observation has no information on coupling. A=0 has no requested
drive and supplies no coupling information either. With positive net powered
acceleration and no other positive tangential force, `phi > 0` implies the
conditional bound `q < 1/u²`; current data lacks the required local u, so no
number is reported. These are algebraic possibilities, not outcome B evidence.

Real rider throttle is **not** measured by the simulator's A(progress). Treating
every slow exit as capacity-active would confuse voluntary control with grip.
At unknown A, `v,x,y` alone can identify kinematics, not the available force
envelope. Bench S2 has measured forces but no justified mapping to the current
speedway engine/capacity axes; it cannot fill this gap.

## 3. Minimum data delivery and acquisition routes

First request a small, licensed, anonymized export from a telemetry custodian or
controlled training session, not another public-summary scrape. Audit sources
S1/S6 provide research/provider leads; restricted access may require an ethics
process and joint research. Contacting them, purchasing video, accessing private
data or commissioning recording needs separate user authorization. None was
done in this task.

Deliver immutable raw files plus a provenance manifest: source/custodian, rights,
acquisition date, instrument/firmware, units, coordinate frame, raw sample rate,
timebase, filtering already applied, missing-data policy and SHA-256. Do not
provide only screenshots of average/max speed.

| Data block | Required fields / evidence |
| --- | --- |
| Time series | `session_id, track_id, rider_pseudonym, attempt_id, lap_id, sample_id, t_seconds, x_m, y_m`; native timestamps with monotonicity, gaps and sample-quality flags |
| Speed / timing validation | Independent `v_mps` where available, its measurement method and timestamps; timing-gate/sector crossings; synchronization offset and uncertainty. If speed is derived from position, mark it as derived, not independent validation |
| Geometry | Survey/control-point coordinates, inner/outer boundaries, banking/elevation and uncertainty; sensor mounting/antenna offset and reference point tracked |
| Positive-drive anchor | Synchronized drive request if available, or controlled full-request protocol; straight runs at overlapping speeds and matched equipment/conditions, with independent justification of losses and system mass |
| Capacity convention | Independently anchored lateral scale or an explicit frozen C used only for a conditional q; no simultaneous fit of C and q |
| Conditions / exclusions | Equipment configuration, rider/session, surface preparation/state, weather/wind and banking; contact, traffic, occlusion, restart and scrub/roll-off flags. No new gameplay modifiers are inferred |
| Matched riding passages | Repeated tighter and opened exits at comparable entry speeds and positive-drive conditions; bend/sector boundaries, actual path length and crossing times |
| Holdout | Separate riders/sessions and at least two surveyed contrasting tracks when available, including a tighter/technical and a broader/faster track; Motoarena may remain the simulator fixture |

No fixed number of laps can be certified sufficient before the signal/noise and
capacity-active coverage are known. Collect repeated sessions, not thousands of
correlated frames from one lap. If only track length and heat times arrive, the
outcome stays C. If suitable powered local samples establish only inequalities,
report B and the assumptions rather than manufacture a fitted point.

## 4. Derive kinematics before force fitting

Work in surveyed metric ground coordinates. Correct sensor/reference-point
offsets and timing first. Use one uncertainty-aware position representation
with documented differentiation/smoothing bandwidth, selected through independent
measurement validation rather than a preferred q or lap time. For planar motion:

```text
v = sqrt(x_dot² + y_dot²)
kappa = (x_dot*y_ddot - y_dot*x_ddot) / (x_dot² + y_dot²)^(3/2)
a_lat = v² * abs(kappa)
a_long = dv/dt
s(t) = integral(v dt)
a_long = v * dv/ds = 0.5 * d(v²)/ds
```

Handle near-zero speed, gaps and differentiation boundaries explicitly. Corner
entry/apex/exit must be fixed geometric crossings/progress conventions applied
to all runs; do not move the apex to select a favourable acceleration. Integrate
actual path length and report full sector time plus the local powered-exit
window. Do not substitute track radius for ridden curvature.

`a_lat` is trajectory normal demand and `a_long` is tangential acceleration,
not an automatically identified tyre-force split. A raw body-mounted IMU needs
orientation/gravity treatment before comparison; banking and sensor/rider motion
need error bounds. This is measurement preprocessing, not permission to add yaw,
slip, front/rear tyre or suspension physics to the manager.

## 5. Video feasibility and measurement error

Video is a fallback only after an input passes all of these gates:

1. Licensed original frames, exact URL/file hash, event/rider and segment timecodes;
   reliable presentation timestamps/timebase and identification of slow motion,
   dropped frames, interlacing and cuts. A nominal player FPS label is insufficient.
2. Surveyed metric landmarks distributed over the measured area; at least four
   non-collinear correspondences for a planar homography, with additional
   independent points for error validation. Known track length alone is insufficient.
3. Lens/camera calibration, stable view or a calibrated transform for every
   pan/zoom; account for track banking/elevation. An onboard camera needs its own
   time-varying pose solution, not a stationary-camera homography.
4. Visible, consistently defined motorcycle ground reference, not rider head or
   an uncorrected airborne/leaning feature. Record occlusion, compression, blur,
   rolling-shutter and tracking uncertainty.
5. Independent residuals in metres and seconds, propagated through smoothing,
   velocity, curvature and acceleration; signal large enough to distinguish the
   hypothesized effect under a predeclared uncertainty criterion.

An illustrative error calculation, **not a measurement of any retrieved clip**:
for independent one-dimensional position errors with standard deviation sigma_x,
sample spacing dt and unsmoothed central differences,

```text
sigma_v = sigma_x / (sqrt(2)*dt)
sigma_a = sqrt(6)*sigma_x / dt²
assumed sigma_x = 0.05 m and dt = 0.04 s:
sigma_v approximately 0.88 m/s; sigma_a approximately 77 m/s²
```

The second derivative is therefore not justified by a plausible-looking pixel
track alone. Smoothing reduces noise but changes temporal resolution and may
erase the exit signal; correlated errors invalidate the independent-error
example. Propagate timing/calibration/position uncertainty with whole-trajectory
resampling, and cluster confidence intervals by run/session, not adjacent frames.
The claimed Pegasus positional specification is not sigma_x in this example.

For lateral demand, relative sensitivity is approximately
`delta_l/l = 2*delta_v/v + delta_kappa/kappa` to first order; statistical error
propagation must include covariance. For a capacity-active observation,

```text
beta = (1-z²)/l²
d_beta/d_z = -2*z/l²
d_beta/d_l = -2*(1-z²)/l³
q = beta*C²
```

Near z=1 or small l the inferred coupling has little information and unstable
relative uncertainty; C uncertainty also affects q quadratically. Carry force,
request, mass, losses, timing, smoothing and geometry uncertainty, not just
regression scatter. Reject uncalibrated broadcast material for numerical fitting;
it may still illustrate line opening qualitatively. Current S10 has unknown
error and no certified measurable segment, so no video fit was attempted.

## 6. Future fit and falsification gates

Only after the data audit upgrades to A or B:

1. Freeze exclusions, independent anchors, fitting window, smoothing validation,
   units and holdout assignments before looking at optimizer results. Separate
   powered passages from scrub, voluntary roll-off, traffic and contact.
2. Establish straight at-speed drive/loss anchors independently. Compare curvature
   effects at similar speeds and matched conditions. Raw negative correlation
   across arbitrary phases is not proof of grip allocation.
3. Fit the **observed** `z = min(A, sqrt(max(0,1-beta*l²)))` relation directly,
   with errors in both axes and correct censored/boundary branches. Squared ratios
   can be diagnostic but are heteroscedastic; do not blindly ordinary-least-square
   all samples. If request is unknown or active limits unsampled, report the
   remaining non-identifiability and stop.
4. Compare the one-parameter surrogate with no coupling and empirical residuals.
   Report rounded estimates/intervals and sensitivity to anchors, bandwidth,
   exclusions and venue/session. Do not truncate an incompatible estimate into
   `0..1` and call it validation. A mapped q outside that domain challenges the
   fixed capacity convention or surrogate.
5. Falsify rather than repair by tuning: independently anchored, genuinely
   capacity-active powered observations inconsistent with the monotone envelope
   beyond measurement error, persistent speed/venue-dependent residuals, or
   positive powered acceleration where the proposed cutoff forbids thrust reject
   that candidate range/law under its stated assumptions. First test the anchors
   and omitted forces; do not classify every voluntary control variation as a
   counterexample. Do not tune correction to hide disagreement.
6. Validate on held-out riders/sessions and contrasting surveyed tracks. If a
   range survives, freeze it before one existing simulator consequence check:
   ConstantInner/free trajectory, sector time, exit speed, path/curvature and
   grip utilization. This is out-of-sample validation, never a fitting objective.
   No retuning after seeing the winner, and no production promotion without review.

## 7. Manager-scale sanity and scope boundary

At fixed speed and capacity, greater absolute curvature raises lateral demand;
for q>0 it cannot increase propulsion capacity. Requests below the reduced cap
remain unchanged. A tighter line can save distance; opening an exit can lower
demand and permit more positive drive where capacity was binding. Extra path
length and the unchanged engine/controller constraints prevent either a wide
line or inner from being automatically best. These are structural properties,
not a prediction that every real opened exit accelerates more strongly.

The candidate remains one hidden positive-propulsion allocation, simple enough
for a manager-scale model. Keep effective correction/scrub as a separate
speed-reduction abstraction: its negative action is not a resolved physical
longitudinal tyre force. No braking ellipse, tyre model, wheelspin/slip/yaw,
temperature/wear/suspension/soil system, rider attribute, player option, setup
modifier, wet/hard/soft integration or production physics is added.

**Current execution:** outcome C; documentation only. No fit, no chosen q,
no new geometry/reserve sweep, no simulator consequence run and no production
implementation. Stop with the acquisition requirements above. PR #50 remains
Draft and unmerged.
