# Combined grip audit and experiment protocol

Status: **PROVISIONAL / ANALYSIS ONLY / NOT EMPIRICALLY CALIBRATED**.
Fetched main before work: `58771bac635d7d4962e11aa3ae5ad37a2d0c8d94`.
This audit and sweep were recorded before implementation or optimization.

## A. Existing turning envelope

`SegmentPhysics.MaxSafeTurnSpeedForRadiusAtReference` is
`v_settled(R) = 19 * sqrt(R / 24) * control * speed * setup * surface`.
Consequently `a_capacity = v_settled(24)^2 / 24` is independent of radius.
This is an effective lateral capability, with provisional skill/setup/surface
and empirical reference-speed factors; it is not a measured tyre-force limit.
The recoverable #47 target is a different quantity: canonical apex capability,
continuous differential-v² restrictions from all 2049 geometry samples, and a
backward reachable envelope using the existing bounded scrub capability. It
allows controlled entry overspeed and post-apex drive. Thus neither lookahead
target nor instantaneous speed is a hard friction-circle boundary. Numerical
lateral movement headroom is yet another constraint, measured in metres per
integration interval, not lateral grip headroom.

## B. Correction

`CalculateCornerSpeedCorrectionProfile` spends physical distance at bounded
deceleration, then carries at constant speed over the remainder. Propulsion is
zero there. It is an abstract, effective roll-off/setting/slide speed controller,
not a mechanical brake and not an identified longitudinal tyre force. The
current model does not decompose scrub into drivetrain, rolling resistance and
slide contributions. Mapping all `m*a_correction` to friction braking would
invent that decomposition and disable the very recovery assumed by lookahead.
We retain correction exactly; it receives no additional force or loss. Its
equivalent negative action is diagnostic, not an independently clipped tyre
force. Coupling limits positive propulsive force, including hypothetical positive
requests submitted with a Correction label. Labels never enter the capacity law.

## C. Why ConstantInner wins without coupling

It has the shortest arc. Larger radii already raise settled speed as sqrt(R),
but path length rises as R. Existing post-apex progress availability and the
speed-dependent engine curve have no lateral-demand allocation. Correction
is bounded but also neutral carry; there is no turning energy debit in #47.
Variable splines add path length, may tighten local curvature (the complete
envelope then restricts speed), and spend lateral/reposition budget. The #48
dissipative cost is a separate sensitivity experiment, default off; its c=.005
winner is close to the numerical lateral gate. It is not active production
physics and will not be stacked on this experiment.

## D. Double counting and chosen minimal law

Adding a second independent `mu`, `a_lat_max`, or another lateral speed limiter
would describe the same capability twice. Instead reuse `a_capacity` above and
retain #47's trajectory/outcome guardrails. Define `u = v²*abs(kappa)/a_capacity`.
`F_lat = 142*v²*abs(kappa)` is demand, never drag or work.
One new dimensionless parameter `q` allocates lateral demand to the shared
propulsion budget: `factor = sqrt(max(0, 1 - q*u²))`.
Full at-speed engine force from the existing model is the effective longitudinal
reference capacity, not a calibrated tyre maximum. Requested propulsion is
`A(progress)*F_engine(v)`, and usable propulsion is
`min(requested, factor*F_engine(v))`. Small requests below capacity are unchanged.
Subtract the existing `A(progress)*F_resistance(v)` after clipping; resistance
is not clipped. This preserves the existing availability-scaled resistance
abstraction and never adds a lateral energy loss. Net force can be negative.

q=0 executes the old replay/integrator directly. q=1 is the full effective
ellipse; controlled overspeed u>1 then leaves zero propulsion capacity, without
inventing a second rejection gate. This conflict with recoverable overspeed is
explicit evidence about the limits of the surrogate, not something to tune away.
This adds a missing simultaneous positive-force allocation, not a second lateral
limit. It does not claim that the existing envelope and surrogate form a fully
self-consistent physical tyre model.

## Frozen sweep and checks

Sweep **q = 0, .25, .5, .75, 1**: disabled, quarter, half, three-quarter and
full lateral-budget allocation. These span the meaning of the parameter and
are not selected for a crossover. No value is empirically fitted. Keep geometry,
11 spline controls, sampling, validity gates, 106 starts / 48 refinements and
all search tolerances unchanged. Evaluate all five constants and the full
deterministic search at each point; report repeated laps, convergence, clipping
and geometry. Replay every winner with actual integration-interval diagnostics
and inspect every controller transition. Coherent +/-5, +/-10 and +/-15 cm
geometry probes are robustness checks, not searches. Preserve invalid probes.

Speedway powered oversteer and controlled rear slide on loose surfaces make this
an effective manager-scale surrogate. No real slip angle, yaw, wheelspin, tyre
temperature, suspension, surface evolution, new skill or gameplay UI is inferred.
Production HeatSimulator and advanced reference speed 19.0f are frozen.
