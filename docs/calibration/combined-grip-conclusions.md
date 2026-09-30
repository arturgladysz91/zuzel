# Combined grip: reviewed interpretation and required answers

Status: **analysis only; physical signal present, robustness limited**.
Read with [the pre-implementation audit](combined-grip-audit.md) and
[the independently reproduced numerical report](combined-grip-availability.md).
No production promotion follows this result.

The existing model has no simultaneous allocation between lateral demand and
positive propulsion. Reusing its effective lateral capability makes a small
allocation law possible with one parameter. The frozen sweep finds a plausible
powered-exit trade-off, but its best geometries remain close to numerical
execution limits. This is evidence for the missing dependency, not evidence of
a calibrated or robust optimum.

| q | ConstantInner sector s | best found sector s | extra path m | exit gain m/s | flying-lap gain s |
|---:|---:|---:|---:|---:|---:|
| 0 | 6.676829338 | 6.676829338 | 0 | 0 | 0 |
| .25 | 6.709475517 | 6.709475517 | 0 | 0 | 0 |
| .5 | 6.758304596 | 6.748776436 | 1.065055847 | .274202347 | .023601532 |
| .75 | 6.831285477 | 6.809123039 | 1.094551086 | .394069672 | .054149628 |
| 1 | 6.961415291 | 6.904582977 | .682365417 | .666849136 | .022308350 |

At q=.5 the advantage is only 9.53 ms and useful propulsion work is almost
unchanged (7763.00 versus 7761.27 J). Its higher apex speed and altered curvature
already supply much of the exit advantage; it loses 6.23 ms to inner when
replayed at q=0. Do not describe this as a strong, uniform grip improvement.
At q=.75 and q=1 the late powered exit shows a clearer force-allocation signal.
For q=1 at progress .9, winner/inner utilization is .79963/.96190, positive
capacity factor .60049/.27341, and usable propulsion 196.30/90.63 N. At the
very end the winner's factor is lower again. The advantage is distributed over
the exit, not a uniformly better radius or a bonus for a named line.

The q=1 winner's early maximum curvature is .038471214 1/m (R=25.99346 m) at
p=.300293. It is not aligned with a Drive/Correction transition. At all winner
transitions the largest neighboring curvature ratio is <=1.004007; every
interval retains exactly its state-derived capacity, including saturated
correction intervals. The sampled profiles show no new branch-dependent
capacity disappearance. Existing float-scale correction/carry chatter remains;
retaining an effective scrub controller is not a full tyre-force validation.

Crucially, the q=1 geometry is invalid at q=0 due to **LateralExecutionConstraint**.
Its lower coupled speed gives more time to execute lateral motion. Thus its
ranking depends on the unchanged numerical movement gate as well as propulsion;
the 56.83 ms sector gain must not be treated as an isolated tyre-physics success.
The numerical minimum headrooms are .000039342/.000064534/.000002889 m at
q=.5/.75/1. These micrometre-scale per-interval reserves are not sensible physical
margins and cannot certify robust geometry.

The q=1 coherent outward +5/+10/+15 cm translations remain valid and cost
2.07/5.33/9.56 ms. Inward translations cross the track boundary. The smooth exit
fan is valid at both signs: -15/-10/-5/+5/+10/+15 cm gives
+13.79/+8.46/+3.16/+0.063/-1.47/-1.20 ms. Changes are small and finite; no large
phase cliff appears. The 1.47 ms improvement at +10 cm is below the existing
2 ms search tolerance; the optimizer is not an exact minimum. These probes were
not refined or substituted as a new winner.

## Fifteen required answers

1. **What is #47 turning envelope?** Its settled v(R) encodes an effective
   radius-independent lateral-acceleration capability (15.041667 m/s² in this
   scenario). Recoverable lookahead adds bounded scrub and physical distance;
   it is not an instantaneous measured friction limit.
2. **Does combined grip duplicate it?** A second lateral cap would. The chosen
   positive-propulsion allocation adds a missing simultaneous dependency;
   it does not add another lateral rejection rule.
3. **How is double counting avoided?** Reuse v_settled²/R, retain #47 validity,
   keep #48 dissipation off, and add no lateral drag/work debit. Engine force is
   an existing longitudinal reference proxy, explicitly not a measured tyre cap.
4. **Does zero coupling reproduce baseline?** Yes, by direct old-path execution,
   with exact fixed/profile/correction comparisons and an independent old-path
   optimizer replay. No gameplay-scale tolerance masks a difference.
5. **Is it branch independent?** Yes. Speed, curvature, existing capacities and
   signed requested action define the result; controller labels are not inputs.
   Negative effective scrub remains unchanged rather than being invented as
   friction braking.
6. **Is there a phase exploit?** No new capacity-immunity exploit was detected by
   the actual-transition regression, all-interval recomputation or manual trace
   inspection. High curvature under zero propulsion request does not imply an
   exemption. This conclusion is narrower than validating the abstract scrub
   controller as a physical tyre model.
7. **Does ConstantInner still win?** Yes at q=0/.25; all constant-line comparisons
   still prefer inner. The free search finds faster non-constant geometries at
   q=.5/.75/1 under the frozen convergence tolerance.
8. **Why are they faster?** They pay extra distance and corner time to retain
   higher exit speed and save following-straight time. At stronger coupling,
   opening the late curvature releases more propulsion. Earlier apex geometry
   and, especially at q=1, lateral execution feasibility also contribute.
9. **Is the requested trade-off observed?** Locally, yes: longer path, lower
   critical powered-exit utilization, more usable drive, higher exit speed and
   shorter sector. It is not a whole-corner monotonic relation: peak demand can
   be higher, and q=.5 has only a weak extra propulsion-work signal.
10. **Is winning geometry stable and sensible?** Numerically converged under the
    unchanged search criteria, with 3–5 independent near starts and no transition
    curvature cliff. Physical robustness is **not established**, especially for
    q=1's early tightening and near-boundary execution. No global-optimum claim.
11. **Does it have sensible lateral headroom?** **No demonstrated physical
    headroom.** Numerical per-interval reserves are micrometre scale. This is a
    material limitation, not an extra physical grip parameter.
12. **Are small perturbations smooth?** Feasible 5–15 cm probes give small finite
    time changes without a large discontinuity. Both signs of the exit fan are
    valid; inward global shifts hit the track boundary. This does not prove an
    unconstrained optimum or smoothness outside the sampled neighborhood.
13. **More than one new parameter?** No: only q. Existing mass, engine curve,
    surface/setup mapping, correction and geometry/search settings are reused.
14. **Any empirical parameter value?** No empirical q or combined tyre envelope
    is established. Calibration context for 19 m/s cannot calibrate this new
    allocation law.
15. **Simple enough for a hidden manager mechanic?** Yes in computational and
    conceptual size, potentially. Evidence is insufficient for promotion:
    powered oversteer makes this an effective surrogate, and execution-boundary
    dependence and scrub interpretation still require independent review.

**Decision: retain as an analysis-only hypothesis and stop at Draft PR for
independent review. Keep production HeatSimulator and 19.0f unchanged.**
