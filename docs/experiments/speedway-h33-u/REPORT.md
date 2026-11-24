# Study #214 — H33-U unified dynamics and active rider mass

H33-U models bounded rider motion with frame recoil, variable inertia and an actuator work ledger. In the matched full-drive launch, the fixed rider produces 0.454 s of front flight. Each of the three active rider commands retains front support. The earlier throttle recovery challenge reaches the clearance stop in all four configurations. The horizontal and sagittal mechanics share one accepted MODEL step. Native integration remains gated on a transactional, event-ordered collision and footprint interface.

## Model and equations

The opt-in MODEL uses one clock, one combined center of mass, yaw and pitch, a finite internal rider stroke, and two unilateral wheel contacts. The front wheel is a massless steered contact. The rear wheel and crank retain the published powertrain and wheel parameters. Roll and left-foot support are reserved extensions.

Let `M=m_b+m_r`, `ν=m_r/M`, and `μ=m_b m_r/M`. Let `r=(r_x,r_z)` be the rider COM relative to the motorcycle COM in body sagittal axes. Define `J(x,z)=(-z,x)` and `(d_x,d_z)=R(θ)r`. The two masses are at `COM−νRr` and `COM+(1−ν)Rr`, respectively, with yaw applied to the horizontal projection. Their relative velocity before yaw rotation has sagittal components `R(ṙ+θ̇ Jr)` and lateral component `d_x ψ̇`. The stroke prescribes only `r(t)`. All frame translation and orientation follow from forces, inertia and contact.

The reduced kinetic energy is

```text
T = ½ M |V|² + ½ Iθ0 θ̇² + ½ Iψ0 ψ̇²
    + ½ μ |ṙ + θ̇ Jr|² + ½ μ d_x² ψ̇²
    + ½ I_c (α̇_c − θ̇)² + ½ I_w (α̇_w − θ̇)² .
```

Gravity gives `V_g=Mgz`. The effective pitch inertia is `Iθ=Iθ0+μ|r|²`, yaw inertia is `Iψ=Iψ0+μd_x²`, and `C=μ Jr·ṙ`. Rotor absolute angular momenta in their forward-spin convention are `p_c=I_c(α̇_c−θ̇)` and `p_w=I_w(α̇_w−θ̇)`. Total pitch momentum is `Pθ=Iθ θ̇+C−p_c−p_w`. The canonical yaw momentum is `Pψ=Iψψ̇`. The yaw-dependent contribution to `dPθ/dt` is `−μ d_x d_z ψ̇²`. Ground-force moments and this geometric term update pitch momentum. Internal rider and rotor forces give no direct external force or yaw moment.

For a wheel with structural axle offset `a_i`, let `(X_i,Y_i)=R(θ)(a_i−νr)`. Its gap is `g_i=z+Y_i−R_i` and normal speed is `u_i=ż+X_i θ̇−ν(Rṙ)_z`. Acceleration solves `0≤N ⟂ g̈≥0` at a resting contact and impact solves `0≤J_n ⟂ u⁺≥0` with perfectly inelastic normal restitution. With prescribed instantaneous rider velocity, the normal mobility is `A_ij=1/M+X_i X_j/Iθ`. The horizontal lever arm projects `X_i` onto heading. Tyre pitch torque uses the bottom-of-tyre lever `R_i−Y_i`; rear spin receives the matching `−R_w F_x` torque. Rear slip uses the absolute rim speed and the moving axle's tangential velocity. Forces at unsupported tyres are zero.

On one contact, eliminate height and integrate `πθ=Pθ−X_i P_z`. For two coplanar supports with equal wheel radii, eliminate height and pitch. The reduced coordinates reconstruct the dependent momentum from zero normal speed. A normal impact keeps position, relative rider velocity and absolute rotor speeds continuous. Its internal actuator impulse is explicitly accounted when a moving stroke crosses an impact.

For the rider actuator the relative momentum is `p_r=μ(ṙ+θ̇ Jr)`. Its force is `ḋp_r−∂T/∂r−Q_r,ground` and its work rate is that force dotted with `ṙ`. Ground contributes through the same moving-COM contact Jacobian used in the primary equations. The actuator stroke uses quintic rest-to-rest interpolation within the #212 travel envelope. The speed and acceleration limits are explicit hypotheses. A new delayed command can update the target while a stroke is active. The next stroke starts when the active one reaches rest. An ideal internal servo supplies the required generalized force or impact reaction. There is no calibrated muscle force or power limit in this variant.

## Shared step contract

An immutable state holds the clock, planar pose and momentum, vertical pose and momentum, pitch and angular momentum, steering, spin momenta, stroke, active command, ordered pending activations and support geometry. The step owner cuts intervals at command activation, stroke completion and geometric events. An accepted interval passes two horizontal force-point pairs and one drag force to the horizontal backend. The backend returns actual linear and yaw impulses at the same time boundary. Preparing the force preview has no external side effects. A native collision would require a synchronized event and rollback strategy before this contract can be used against a live world.

`IHorizontal214.Preview` returns a receipt without committing. The step owner checks the returned clock, linear and angular impulses and any collision impulse before it accepts the vertical state or calls `Commit`. The MODEL backend fulfils this interface analytically. The contract test injects a duplicated force receipt, a shifted clock and an unscheduled collision. The collision case never reaches commit. The current `ContactAdapter` immediately mutates the Box2D body when applying forces and advances the world through a void-returning step. It provides neither a transactional event preview nor a synchronized collision receipt at the vertical impact boundary. That is the native blocker demonstrated here. No Box2D worlds were created.

The initial physical support comes from a unilateral normal solve. H33-U solves the midpoint state and force projection together, and checks the normal reaction signs against the beginning and end of the interval. A detected change shortens the interval. For touchdown it searches the geometric gap inside a force preview, using the chord bound `min(g(a),g(b))−B(b−a)²/8` and subdivision. The demonstrated domain uses a conservative numerical envelope on gap curvature. The adversarial test has two positive endpoint gaps and a negative interior and returns the first closing event. A grazing case returns no impact. The tyre and normal root solver provides a local converged root or a bracketed continuation. It provides no global uniqueness proof for the coupled friction and control laws.

## H32-P reproduction and audit

H32-P reproduced 39 published artifacts byte for byte in a new process. Independent ledger results matched for 31 trajectories and 11,646 accepted steps. With the experimental variant disabled, the frozen H28-F receipt reproduced 48.94288044161751 s, 0.5576869874011356 m swept reserve, 956.5509777367096 m last reference and 4,895 steps.

| Audit item | Evidence and implication |
| --- | --- |
| Contact changes within the step | H32-P only enters its touchdown search if the unsupported endpoint gap is negative. The adversarial test exposes a class that an endpoint-only trigger cannot see. H33-U starts with a midpoint and scans closing gaps inside the interval. |
| Midpoint and initial support | Published H32 traces agree at all recorded midpoints, with 10,484 two-support and 1,162 rear-support steps. That agreement alone cannot exclude an event between samples. |
| Contact arms and inertia | H32 rotates each axle arm for pitch and holds yaw inertia fixed. H33 rotates the arm about the mass-weighted COM of the prescribed rider stroke and varies both pitch and yaw inertia. |
| Root conditioning | Both models check achieved residuals. H33 also checks endpoint reaction signs and reduces the interval. A local bracket gives no general uniqueness guarantee. |
| Energy drift | H32's largest individual remainder is 0.00522 J. Its largest absolute accumulated remainder is 0.05930 J in the rearward bend-three probe. The H33 validator checks the cumulative balance as a separate test. |
| Footprint | A published H32 launch reaches 61.05° pitch while the sweep still uses a planar prototype silhouette. H33 records that planar proxy alongside a separate padded screen for pitch projection and moving COM. Neither result provides a calibrated native swept volume. |

## Experiments

**A** is the archived H28-F response and **B** is the archived neutral H32-P response. **C** uses H33 with fixed neutral rider position. **D**, **E** and **F** permit longitudinal, vertical and combined rider strokes respectively. All four H33 arms receive the same bike commands in each comparison. Rider commands activate after 0.05 s and have separate targets. Local exact-failure cases start from the recorded sagittal posture and planar, steering and rotor state. Fixed case C retains that posture. The H33 engine map, friction coefficient, steering bounds and track geometry are inherited.

The test set includes equilibrium, recoil under gravity with horizontal forces disabled, force-free flight, angular momentum under changing yaw inertia, synthetic touchdown during an active stroke, repeated work over 8.2 s, full-drive launch and matched corner probes. The fixed early-recovery challenge sets at least 85% Drive at 0.35–0.65 s of the same exit probe in all four H33 arms. There was no racing-controller search.

### Momentum and actuator work

1. **Internal movement conserves momentum.** With the tyres disabled in the horizontal recoil test, a 0.16 m forward rider translation moves the motorcycle frame about 0.0762 m rearward while the combined center of mass stays fixed horizontally. Force-free flight preserves linear and sagittal angular momentum exactly at stored precision. A separate yawing flight preserves yaw momentum while the inertia and yaw rate change. These are statements about the retained constrained coordinates. Transverse angular momentum requires the future roll degree of freedom.
2. **The actuator supplies or absorbs work.** The 8.2 s gravity-only ground trial exercises alternating forward, rearward and vertical strokes and returns to neutral. Maximum accumulated signed energy remainder is 0.00263 J and the final remainder is about 1.77 μJ. A synthetic moving-rider drop dissipates 415.05 J across its impacts. The ideal actuator absorbs 54.61 J of work during impact to maintain the relative velocity. The largest sampled continuous rider force in the matched launch is about 1.33 kN. Rider stroke bounds are 0.70 m/s and 6.0 m/s². The force demand requires calibration before athlete feasibility can be inferred.

### Wheel lift and steering

| H33 arm | Full-drive launch minimum front | Front flight | Turn-preparation minimum front | High-drive bend-five minimum front | Exit mean front yaw band |
| --- | ---: | ---: | ---: | ---: | ---: |
| C fixed | 0.0 N | 0.4541 s | 542.9 N | 74.3 N | 1.422 N·m |
| D longitudinal | 31.4 N | 0 | 600.0 N | 219.3 N | 1.496 N·m |
| E vertical | 23.1 N | 0 | 437.0 N | 210.5 N | 1.561 N·m |
| F combined | 29.6 N | 0 | 536.2 N | 200.5 N | 2.357 N·m |

The fixed launch reproduces frozen neutral H32-P lift at 0.300 s and touchdown near 0.7541 s. H28-F reaches the tested launch horizon through its positive-load model. Active movement suppresses lift in the matched launch while marginally reducing peak acceleration. In high-drive bend five the longitudinal command trades rear thrust for front support, reducing peak acceleration from 9.23 to 7.86 m/s² while raising the minimum front load by 145 N. Combined movement increases the mean sampled front yaw band on the #211 exit by about 66%. The band samples the reachable half-step steering range at the same timestep. It is a local measure of front tyre yaw authority, which falls to zero in front flight.

The direction and timing matter. In the reconstructed #212 low-load bend-three and bend-five queries, fixed H33 continues through 0.082 s and 0.108 s of front flight. Longitudinal and combined motion yield similar flight times. The vertical-only stroke lengthens flight to 0.192 s and 0.195 s. All eight H33 cases recontact within their 0.40 s local horizon. The separate numerical failure from #211 remains supported with at least 337 N on the front. These outcomes distinguish a recoverable local contact continuation from a sporting result.

Longitudinal movement improves absolute course error on the #211 exit from 3.61° to 2.14° while reducing final speed from 87.07 to 85.11 km/h. Combined movement in high-drive bend five reduces absolute course error from 26.32° to 18.86° with lower final speed. All four early-recovery perturbations reach the planar sweep gate before completing. The experiments therefore support improved steering in specific phases, with no demonstrated earlier qualifying throttle recovery.

### Ordinary straights and finish

The C and F exit probes cover 0.595 s of ordinary straight with zero WOT, reaching 87.07 and 87.29 km/h there. The matched C and F high-drive probes are entirely in bend five and include 1.36 s WOT during 1.45 s, with peaks of 101.55 and 97.85 km/h. The terminal-certificate probe covers 0.250 s of finishing straight, with zero WOT and a 105.85 km/h peak. These sector totals bin accepted substeps by their endpoint. The stationary launch tests are mechanical probes and excluded from the ordinary-straight comparison. Repeatable 110–120 km/h pace remains a separate future qualification target.

## Validation and remaining architecture

Four launch timesteps from 10 to 1.25 ms retain the lift and touchdown. The front-flight estimate converges from 0.45440 to 0.45402 s. The signed energy remainder decreases from 5.35×10⁻⁴ J to 1.06×10⁻⁵ J. Two independent processes reproduced 41 H33-U artifacts byte for byte. The independent ledger check covers 39 trajectories and 9,804 accepted steps. Airborne tyre force and airborne front yaw authority are exactly zero. The smallest numerical gap is −1.80×10⁻¹¹ m. The maximum measured planar and yaw impulse errors are 6.20×10⁻¹² N·s and 2.24×10⁻¹² N·m·s. The maximum projected-arm mismatch is 1.58×10⁻¹⁶ m. Of 17 impacts the largest energy identity defect is 7.91×10⁻¹² J. The worst trajectory's cumulative work remainder is 0.00775 J. Continuous vertical and pitch integration residuals reach 0.00312 N·s and 0.00119 N·m·s, respectively, and are distinct from the instantaneous impact balance.

The projected forces and receipts use one MODEL clock and are consistently coupled within the reduced representation. Native qualification still requires a backend that can predict and commit a common event interval, return exact planar collision impulses, and apply a validated dynamic occupancy shape. The padded screen here is a sensitivity check, and original strict acceptance and FAST50 remain on the frozen path. The entire study constructed zero native worlds.

`IContactGeometryExtension214` reserves gap, speed and Jacobian data for additional contacts. A roll extension must first add a generalized coordinate, inertia and work terms and update the wheel/tyre frame. A foot extension must add a genuine unilateral contact row and a bounded dissipative friction law, then solve it with the wheels. The interface provides a boundary for both, while the active solver currently accepts exactly two wheel contacts. The actuator also needs calibrated force, power and impact-compliance limits before it can stand for a physical rider.

## Regression and publication

The Release build passed with warnings as errors. The 24 focused tests include eight new H33-U cases and 16 inherited checks. The #213 reference passed 2,073 of 2,294 core cases. #214 passed 2,081 of 2,302. All 221 failing cases match their parent identities. They comprise 219 blocked native cases and two inherited frozen-source assertions. There are zero new failures. The protected H28-F, H32-P, strict observer and native source blobs are unchanged. H25 remains the default.

[Regression evidence](../../../data/experiments/speedway214/regression.json) · [Determinism evidence](../../../data/experiments/speedway214/determinism.json) · [World ledger](../../../data/experiments/speedway214/world-ledger.json)

GitHub confirmed implementation commit `8ef26276795675dfa53d41017a80c343c402cc57` and reported zero workflow runs, check runs and status contexts on 2026-11-24. The combined status was `pending`. Push CI targets `main`. The tests and mechanics evidence above are local. [Hosted CI snapshot](../../../data/experiments/speedway214/hosted-ci.json)

## Reproduce and inspect

```powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS='1'
dotnet build tools/speedway-h26/H26.csproj -c Release -m:1
dotnet run --project tools/speedway-h26/H26.csproj -c Release --no-build -- unified-214 . out214
python tools/speedway-h26/verify214.py out214 out214/trajectory-validation.json
```

[Protocol](../../../data/experiments/speedway214/protocol.json) · [All mechanical probes](../../../data/experiments/speedway214/summary.json) · [Archived references](../../../data/experiments/speedway214/reference.json) · [H32 reproduction](../../../data/experiments/speedway214/h32-reproduction.json) · [H32 audit](../../../data/experiments/speedway214/h32-audit.json) · [Independent contact and work validation](../../../data/experiments/speedway214/trajectory-validation.json)
