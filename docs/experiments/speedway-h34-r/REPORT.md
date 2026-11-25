# Study #215 · H34-R physical roll

Tools-only MODEL study on #214 head
`7242ce5fc715f1643b6060da35bad5196db6aeab`. The application continues
to select H25. The experiment uses zero native worlds.

## Frozen references and steering audit

The parent process reproduced all 41 H33-U manifest artifacts and all
39 H32-P manifest artifacts byte for byte. The independent H33 verifier
recomputed all 9,804 accepted-step receipts and the same timestep
landing table. The 0.16 m longitudinal rider stroke gives 0.0762 m frame
recoil with the combined COM fixed horizontally. Fixed-rider launch
front flight is 0.4541 s. All three active #214 rider commands suppress
that launch flight. On the matched #211 exit the mean front yaw band
rises from 1.422 to 2.357 N·m from C to F. All four earlier
throttle-recovery variants still fail their planar sweep.
[Frozen H33-U report](../speedway-h33-u/REPORT.md) ·
[Frozen H32-P report](../speedway-h32-p/REPORT.md)

H33's requested steering constructs a clipped yaw-rate target and a
sideslip correction. It converts yaw-rate error into a target moment,
then inverts the front tyre to choose actual steering. Its kinematic
lean estimate reduces front and rear slip scales by up to 13% and
25%. H34 instead uses the old steering bound and rate limit for the
actual angle. The front slip direction then determines tyre force and
both roll and yaw moments. An H33 command tape drives a different
actuator interpretation, a controller-transfer variable in every
matched comparison. The reference H34 contact computes ground-plane
wheel axes with the inherited unscaled slip coefficients. F-scaled
reapplies the empirical slip scale to physical roll to isolate its
contribution. The study retained the tyre and engine parameters.

The PZM manual describes steering opposite the turn during slide
initiation and weight distribution that changes with track conditions
and the phase of cornering. These observations motivate the
countersteering and rider-mass tests. The angle-servo approximation
omits steer assembly inertia, trail dynamics and passive steer
response included in more detailed motorcycle models.
[PZM coaching manual, §§5.3.3–5.3.5](https://pzm.pl/pliki/zg/zuzel/2026/metodyka_szkolenia_a5ver_z_160322.pdf) ·
[Sharp and Limebeer, motorcycle dynamics](https://doi.org/10.1023/A:1017508214101)

## Mechanical derivation

Use a right-handed motorcycle frame with x forward, y left and z up.
The quaternion Q maps body vectors into the world. Rotate by yaw about
world z, then pitch about the rotated y, then roll about the rotated x
when extracting display angles. Positive reported pitch raises the nose.
Positive roll raises the left side. The state integrates the quaternion
and total world angular momentum, not the displayed angles.

Let R denote the rotation matrix, c the system COM, P the world linear
momentum, L the system angular momentum about c, and r the rider
position relative to the unladen frame COM. The frame COM and rider
position are c−νRr and c+(1−ν)Rr, respectively. The mass fraction
ν=m_r/M and reduced mass μ=m_r(1−ν). Define p=μ(ṙ+ω×r) and absolute
spin momenta k_c and k_w around the body y axis. The equations are

    RᵀL = I₀ω + r×p + (k_c+k_w)e_y
    ċ = P/M
    Ṗ = Mg_world + Σ F_i + F_drag
    L̇ = Σ a_i × F_i
    ṙ = p/μ − ω×r
    ṗ = F_act − ω×p − νRᵀΣ F_i

The rotor momenta change with engine, shaft and rear patch torques.
For each locked relative axis j, p_j=μ(ω×r)_j. Move its contribution to
the left by adding μ(r×e_j)(r×e_j)ᵀ to I₀. Free axes keep their stored p.
This reproduces the parallel-axis tensor when all axes are locked,
including the off-diagonal terms. Changing r and rotor momenta therefore
changes angular velocity at fixed world L even in free flight.
Uniform gravity and the study drag act through c. Gravity appears in
the roll balance about the ground support through Ṗ. Actuator power
is F_act·ṙ. The kinetic energy is |P|²/(2M) + ω·I₀ω/2 +
|p|²/(2μ) + k_c²/(2I_c) + k_w²/(2I_w).

Each tyre is an infinitesimally thin ring of radius ρ. Its axle direction
n is R e_y at the rear and R Rot_z(δ)e_y at the front. The lowest
ring point relative to its axle is d=−ρ(e_z−n n_z)/sqrt(1−n_z²).
The tyre arm a=R(a_frame−νr)+d gives gap g=c_z+a_z. The moving-frame
patch velocity uses P/M + (Rω)×a − νRṙ, with the front steer-rate term
(R e_z)×d δ̇ and rear relative spin term n×d α̇. Its normal component
equals ġ, and the rolling direction is n×e_z normalized.
The front has free longitudinal rolling and a lateral tyre response.

The tyre's normal compliance is an explicit reduced-model assumption.
Compression d_n=max(0,−g), effective gap g+d_n, and load
N=k d_n + c_eff max(0,−ġ) when g<0, with damping ramped linearly
from zero to c_d over the first 2 mm. Thus g+d_n≥0, N≥0
and N(g+d_n)=0. Compression stores k d_n²/2 and the damper
dissipates c_eff max(0,−ġ)². Touchdown impulse is the integral
of the supported reaction through the finite compression phase.
The steer actuator's power is −F_front·∂a/∂δ δ̇. It offsets the
patch force's virtual work in the commanded, massless steer joint.
The joint has no inertia or calibrated torque capacity.
Front wheel spin inertia, tyre width and frame articulation are
omitted. No camber-thrust term is introduced.

### Dense gap bound

The accepted-step dense path uses constant world angular velocity,
constant relative rider velocity, constant steering rate and linear
COM drift. Let W=|ω_world|, U=|ṙ|, S=|δ̇|, A=W+S and
B_a=max |a_frame−νr| at the interval endpoints. Then the axle-height
curvature is bounded by W²B_a+2WνU. For s=n_z, |ṡ|≤A, |s̈|≤A².
Bound |s| by |s(0)|+A·dt, reject the singular domain, and add
ρA²[|s|/sqrt(1−s²)+(1−s²)^(-3/2)] for ring height. This
bounds the **numerical dense path** passed to H33-U's chord detector.
It does not certify the continuous exact solution beyond timestep
refinement.

### Third-contact interface

Give the foot an anatomically bounded displacement f(q_leg) from the
rider COM. Its world position is c+R[(1−ν)r+f]. With a_f its arm,
the contact Jacobian maps generalized velocities to
P/M + Ω×a_f + R[(1−ν)ṙ+J_leg q̇_leg]. Its normal row is the
dot product with the ground normal. Its transpose maps each force
or impulse once to the system translation, rotation, rider relative
momentum and leg momentum. Solve the three contacts in the same
active-set transaction using their coupled inverse mass matrix.
Enforce g_f≥0, N_f≥0 and g_f N_f=0 in a rigid formulation,
separation implies zero traction, and |F_t|≤μ_f N_f. Use dissipative
tangential and impact work. In the compliant formulation use
additional measured foot compliance and carry its stored energy.
The leg requires explicit generalized force, power, travel and
velocity limits. Hip and knee geometry, force envelopes, slider
friction, and impact response need calibration before activation.

## Nine findings

1. **Roll follows the applied moments.** An upright machine stayed at
   equilibrium for 0.35 s. A 1.89° initial lean grew to 2.93° over 0.25 s
   with zero steering request. The spatial inertia and the moving rider
   and rotor momenta determine angular velocity from the integrated
   total angular momentum. Gravity-driven fall, yaw, pitch and roll
   all use that state. The combined pitch/roll probe included 58 ms
   of front flight.

2. **The rider can change the frame's roll while conserving combined
   momentum.** The isolated lateral command shifted the frame 67.0 mm
   to the opposite side and produced 5.07° of frame roll while the
   horizontal COM and all three components of total angular momentum
   stayed fixed. The 0.16 m target entered its 10% band 0.238 s after
   activation, or 0.288 s after request. The moving 3D flight completed
   with an energy remainder of about 0.000052 J. The stationary shift
   test also changed roll, now with a genuine change in external
   wheel reactions. The stationary test establishes a response to a
   shift. The straight feedback test establishes recovery.

3. **Steer-induced roll and recovery are present.** At 36 km/h,
   equal opposite ±0.11 rad steering requests gave mirrored
   ±6.07° roll after 0.21 s. Turning the front wheel left generated
   an initial leftward front force and rolled the frame to the right.
   With 50 ms actuator latency, steering and rider feedback reduced
   the absolute lean from 4.93° to 0.82° in 0.70 s at about 39 km/h.
   A drivetrain-balanced circle ran for 1.25 s at 12.00 m/s,
   0.23 rad/s yaw and 2.760 m/s² measured lateral acceleration. Its
   initial bank was 16.28° and the final bank a fraction of a degree
   from that value. The point-contact benchmark is 15.71°, from
   atan(2.760/g). The 3D equilibrium includes thin-ring offsets,
   a slipping tyre, longitudinal support and precessing rotor and
   frame momenta. The separate unpowered circle seed at 8 m/s
   was an open-loop transient and drifted from 14.32° to 8.47°.
   [Circle balance](../../../data/experiments/speedway215/circle.json)

4. **The transferred steering law reaches a different envelope.**
   On the upright #211 exit, C and F produced mean sampled front
   yaw bands of 4.30 and 5.82 N·m and roll bands of 3.60 and
   4.19 N·m over their respective surviving prefixes. F-scaled
   reached 7.46 and 5.35 N·m over a similar prefix. These bands
   test the locally reachable half-step angles while holding the
   sampled support load. The exit ended at the chassis clearance
   envelope after 0.322–0.359 s in these arms, and the low-front
   examples lost almost all steer authority. The mean exit band
   across unequal surviving horizons is descriptive. On bend five
   the scaled and unscaled F prefixes both ended at 0.236 s, with
   nearly identical local steering bands and a 0.06 km/h terminal
   speed difference. The pre-bank sensitivity changes these
   comparisons appreciably, including the front-flight duration.
   The results confound the different steering semantics and the
   absent measured 3D initial posture. They narrow what the old
   controller can transfer, while leaving the optimized physical
   corner envelope unresolved.

5. **Full-drive slip and deceleration occur in the mechanical model.**
   Drive requests a normalized torque-map input, after impairment,
   and the net torque still depends on RPM, losses, clutch engagement
   and wheelspin. No acceleration requirement was applied. In matched
   bend five, C spent all 0.153 s of its full-drive interval losing
   speed, and F did so for 0.082 of 0.146 s. A force-limited feedback
   proposal spent 0.185 of 0.197 s decelerating with full Drive active.
   Both low-front fixed probes ran their entire 0.400 s at full
   Drive, with about 0.373 s of front flight. These are local
   force-accounted observations. Neither 60-proposal search reached
   its 1.45 s endpoint. There is no demonstrated sustained,
   repeatable high-drive entry–slide–exit sequence or increase
   over H33's 1.36 s full-drive bend-five segment.

6. **Force and power demand depend strongly on the maneuver.** The
   moving flight sampled 1.72 kN, 731 W, 1.29 m/s and 51.2 m/s².
   Roll recovery sampled 0.91 kN and 393 W. F peaked at 3.27 kN and
   1.42 kW in the low-front probes. In the synthetic leaned drop,
   the responsive F variant needed 2.09 kN and reached its 4 kW
   ceiling. It supplied 41.66 J and absorbed 338.84 J over its
   accepted interval. The model does not convert these requirements
   into physiological claims. Its actuator is the entire moving
   rider–frame link. In stationary balance the vertical link alone
   ultimately supports about 687 N. The model has no separately
   resolved passive seat, footrest or handlebar load sharing.

7. **Limited actuation visibly departs from the commanded posture.**
   The G drop at 700 N and 350 W integrated 281.88 N·s of wheel
   normal reaction and 7.65 N·s of rider force magnitude over 40 ms
   after first wheel contact, compared with 299.85 and 45.24 N·s
   for responsive F. These are force-time integrals in the compliant
   model. No instantaneous stroke-preserving impulse is applied.
   The earliest G contact was rear-first at 0.1902 s, then front
   at 0.2025 s. G absorbed 31.47 J, reached 4.64 m/s relative
   speed and hit the rider travel envelope at 0.268 s. It never
   settled to its target. The 350 N/150 W and 1,400 N/700 W
   sensitivities similarly reached the travel boundary. All 120
   centered/pre-banked search candidates stopped on that boundary
   before any planar sweep failure. They characterize this total
   rider-link limit and initial state. The model cannot yet
   distinguish athlete strength from the missing passive supports
   or demonstrate that better control and load sharing would fail.

8. **A third-contact Jacobian is available for the left foot.**
   The implemented kinematic and transpose maps pass a virtual-power
   test. The wheel solve still has two active contact rows. The leg
   coordinate inertia, coupled force or impulse solve, leg clearance,
   and calibrated leg and ground response remain the prerequisites
   for the third active row. No external foot force is present.
   [Foot Jacobian](../../../tools/speedway-h26/FootContact215.cs)

9. **Native qualification requires a synchronized transaction.**
   First, add dynamically projected 3D capsule occupancy and sweep
   both the frame and moving rider across roll, pitch and yaw,
   including the tyre geometry and segment seams. Cross-check
   bounds by adversarial sweeps and an independent dense oracle.
   Next, obtain native collision time, world impulse and contact
   location in a preview/commit transaction that can subdivide and
   roll back without advancing the native clock twice. Feed the
   collision wrench once into the same spatial L and P, then roll
   the shared interval again from the causal checkpoint. Verify
   early/late, simultaneous wheel touchdown and collision, open
   and closed clutch, and equality of MODEL/native when the
   collision impulse is zero. Then run the frozen strict observer
   on accepted native states and use the existing complete S
   criteria. The current MODEL bridge checks horizontal impulse
   and L_z against the H33 receipt and uses the quaternion yaw
   for display and occupancy. Box2D would own planar collision
   discovery. The mechanical state would own roll and vertical
   response.

## Matched protocol and sector results

The frozen A H28-F and B H33-U artifacts remain in their original
locations, with summary receipts collected in the #215 evidence.
B supplies the archived planar COM, velocity, yaw, crank and wheel
momenta, longitudinal and vertical rider location, and bike-command
activation times. H34 starts upright, with zero roll rate, nominal
static tyre sag at the lowest patch and no lateral rider
offset. The pre-bank sensitivity uses the negative of the archived
kinematic lean, clipped to ±0.48 rad, with zero roll rate. H33 stores
no physical roll counterpart. For the four early-recovery rows, A is
the common unperturbed H28 exit reference and B supplies each
challenged tape. The H34 arms share that tape within each matched
comparison. On corner entry, mid-bend and exit the sampled rider
targets activate after 50 ms. Foot contact was disabled in every arm.

| Arm | Physical roll | Moving rider axes | Rider link |
|---|---|---|---|
| A H28-F | Kinematic lean | Frozen | Frozen |
| B H33-U | Kinematic lean | Frozen sagittal variants | Prescribed stroke |
| C | Yes | Fixed | Locked internally |
| D | Yes | Longitudinal and vertical | Responsive |
| E | Yes | Lateral | Responsive |
| F | Yes | All three | Responsive |
| G | Yes | All three | 700 N and 350 W |
| F-scaled | Yes | All three | Responsive with inherited lean scaling |

The two searches used the same pre-banked seed. The first varied
archived steering bias, drive cap and lateral rider target. The
second used causal roll feedback, front contact slip-angle
feedforward, four drive levels and three rider positions. Both
ranked completed horizons before actual reference progress. Lower
Drive ties the best first-stage progress at 4.50 m. No throttle
utilization term entered the ranking. The minimum swept clearance
reported with each row is a legacy planar proxy with actual yaw
projection. It does not bound the lateral projection of the
inclined rider and frame.

| H34 MODEL probe | Ordinary straight | Bend | Finishing straight |
|---|---|---|---|
| Matched entry F with archived command | 0.828 s sampled, 0 s full Drive | 0.050 s sampled, 0 s full Drive | No samples |
| Matched bend five F | No samples | 0.236 s sampled, 0.146 s full Drive | No samples |
| Matched exit F | No samples before chassis limit | 0.357 s sampled, 0 s full Drive | No samples |
| Matched low-front 3 C | No samples | 0.400 s sampled, 0.400 s full Drive, 91.8 km/h peak | No samples |
| Dedicated launch F | 1.120 s sampled, 1.120 s full Drive | No samples | No samples |

The launch is a standing-start test with active Drive before
clutch release, and is outside ordinary flying pace. The comparison
does not credit the finishing straight. Each archived row includes
wheelspin, tyre components and loads, acceleration, steering angle
and rate, torque, RPM, 3D attitude and momenta, and cumulative work
inputs. Signed lateral acceleration is calculated from
(v_x a_y−v_y a_x)/|v_xy|. Course curvature is a_lat/|v_xy|².
The per-sector totals use accepted interval midpoints.

All four early-recovery fixtures reached the H34 chassis clearance
envelope in about 0.30–0.34 s under the transferred tapes. The
frozen H33 cases are planar sweep failures later in the trajectory.
H34's chassis stop is a physical geometry-domain rejection with
an uncalibrated frame envelope. Its travel stop is an actuation or
unmodeled support limit. Those diagnoses are separate from a
controller failure proven against a range of initial roll states.
There was no numerical solver rejection in the two bend search
stages. The official profile and observer stayed fixed. The
practice-specific 0.12 s windows, 0.10/0.16/0.25 rad/s
yaw requirements, 6° slip event and correction/course pattern
need empirical checks against physical roll, velocity curvature
and actual tyre slip. Their numerical thresholds are separate
from the 0.5 m full-body clearance and actuation requirements.

## Validation and reproduction

The independent verifier checked 91 traces, 16,071 accepted steps
and 191 detected wheel crossings. Maximum linear and angular
impulse defects were 9.87×10⁻¹³ N·s and 9.26×10⁻¹⁵ N·m·s.
Airborne tyre force and force times effective gap were zero.
The largest nominal compression was 30.45 mm. The maximum
accumulated signed energy remainder was 0.0426 J, on the
pre-banked bend-five G probe. For a fixed rider launched with
lean, timestep refinement from 5 to 2.5 to 1.25 ms gave front-flight
durations of 0.55135, 0.55152 and 0.55156 s, and energy
remainders of 0.00124, 0.00032 and 0.00008 J. In the hard G drop,
the rider boundary time moved by about 0.06 ms across the same
grid. The G drop work remainder is sensitive to which accepted
interval reaches the boundary. The dense event test hides a
negative gap between two positive endpoint values. Separate
tests check differentiation of the inclined steered ring, delayed
command activation, unilateral contact and force/power saturation.

Two independent processes produced 98 byte-identical H34 artifacts.
The Release build passed with warnings as errors. Twenty-one
focused MODEL tests passed. The parent core shard passed 2,081 of
2,302 tests. H34 passed 2,087 of 2,308. Both had the same 221 fail
identities including 219 guarded native cases and two inherited
frozen-source assertions. A wider rear-wheel focus included ten of
the guarded native tests. Those were blocked before construction.
The H28, H32, H33, official profile and original technique source
hashes match the base. Zero native worlds were created.

[Reproduction](../../../data/experiments/speedway215/baseline-reproduction.json) ·
[Mechanical summaries](../../../data/experiments/speedway215/mechanics.json) ·
[Matched A/B](../../../data/experiments/speedway215/frozen-AB.json) ·
[Matched C–G](../../../data/experiments/speedway215/matched.json) ·
[Steering-tape search](../../../data/experiments/speedway215/search.json) ·
[Feedback search](../../../data/experiments/speedway215/feedback-search.json) ·
[Independent verifier receipts](../../../data/experiments/speedway215/trajectory-validation.json) ·
[Determinism](../../../data/experiments/speedway215/determinism.json) ·
[Regression](../../../data/experiments/speedway215/regression.json)

~~~powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS='1'
dotnet build tools/speedway-h26/H26.csproj -c Release -m:1
dotnet run --project tools/speedway-h26/H26.csproj -c Release --no-build -- roll-215 . out215
python tools/speedway-h26/verify215.py out215 out215/trajectory-validation.json
~~~
