# Execution-reserve follow-up: reviewed interpretation

**The literal existing reserve removes both crossovers, but it does not prove
that the optimizer was exhausting the physical execution budget.** Its per-step
meaning makes this an inconclusive test of centimetres of real reserve.
Keep Draft PR #50 for independent review; no production promotion or q selection.

[Generated eight-case evidence](combined-grip-execution-reserve.md) and
[frozen protocol](combined-grip-execution-reserve-protocol.md).

| q | required reserve cm | winner | free-inner delta ms | extra path m | exit delta m/s | actual min headroom m | interpretation |
|---:|---:|---|---:|---:|---:|---:|---|
| .5 | 0 | nonconstant | -9.528160 | 1.065055847 | .274202347 | .000039342 | crossover; no physical robustness claim |
| .5 | 2 | ConstantInner | 0 | 0 | 0 | .019414285 | returns to inner; search uncertain |
| .5 | 5 | ConstantInner | 0 | 0 | 0 | .019414285 | returns to inner; search uncertain |
| .5 | 10 | ConstantInner | 0 | 0 | 0 | .019414285 | returns to inner; search uncertain |
| .75 | 0 | nonconstant | -22.162437 | 1.094551086 | .394069672 | .000064534 | crossover; no physical robustness claim |
| .75 | 2 | ConstantInner | 0 | 0 | 0 | .019414524 | returns to inner; search uncertain |
| .75 | 5 | ConstantInner | 0 | 0 | 0 | .019414524 | returns to inner; search uncertain |
| .75 | 10 | ConstantInner | 0 | 0 | 0 | .019414524 | returns to inner; search uncertain |

All cases generate the same 106 starts. At zero reserve, 103 starts are valid
and 48 are refined. At each positive reserve, only the five constant starts are
valid, so the unchanged search refines five. ObjectiveConvergence is NO (only
two independent near-optimal starts), GeometryConvergence and top-three closure
are YES, and winner single/pair residuals are zero. Do not present this as eight
successful 48-start converged optimizations. Both original free winners become
invalid through LateralExecutionConstraint at every positive reserve. A slower
nonconstant candidate is found at 2 cm, about 4.13/4.12 ms behind inner; none is
found at 5/10 cm. This says what the frozen search found, not what a global search
would prove.

The key additional evidence is the unused fraction of each interval's movement
capacity. The minimum raw headrooms occur at progress .779294/.191401 for q=.5/.75
in intervals only .000587463/.000959396 m long. Those intervals use 17.55%/8.38%
of their lateral capacity. Across **all** corner intervals, maximum used fractions
are only 30.55%/58.89%, leaving at least 69.45%/41.11% unused. Tiny raw margins
therefore do not show that these two winners nearly exhaust the time-based
execution constraint. This diagnosis does not validate q=1.

The reserve withholds an absolute number of metres on every integration interval,
whose capacity itself scales with traversal time. The free winners have 1943
intervals; inner has 100. In a tiny interval, 2 cm can exceed the entire movement
capacity even when most of it is unused. The clamp still allows zero cross-track
movement there, so valid inner has only 1.94 cm actual minimum headroom even in
the requested 10 cm case. Changing the reserve definition or integration mesh
now to recover the crossover would change the experiment; neither is done here.

Thus **current non-constant optimum depends materially on the lateral-execution
boundary under the literal existing reserve gate**. That must not be read as
proof of a physically near-saturated execution budget. The intended stronger
inference about 2–10 cm real, mesh-independent spare capacity remains unresolved.
The supplied reserve option does not guarantee that quantity.

Both free winners reach maximum curvature at progress .100097656 during
Correction/carry (interval midpoint .09541666), u≈1.45358, factor=0, signed
effective scrub request≈-369.2 N, unchanged usable negative request. Geometry
still tightens while scrubbing, then opens for drive. Negative effective scrub
is outside the positive-propulsion allocation; a physically meaningful phase
exploitation remains possible and unproven. This could also be valid speedway
behaviour. No braking ellipse or unsupported scrub decomposition is introduced.

## Ten decision answers

1. **q=.5 at 2 cm?** No faster free winner under the existing reserve gate.
   Physical 2 cm robustness is unresolved because of that gate's semantics and
   the reduced number of eligible starts.
2. **q=.5 at 5 cm?** No faster free winner under that gate; same limitation.
3. **q=.5 at 10 cm?** No faster free winner under that gate; same limitation.
4. **q=.75 at 2/5/10 cm?** No faster free winner in all three cases; same limitation.
5. **Does the force/path chain remain away from the boundary?** No positive-reserve
   free winner demonstrates it. At zero reserve, a longer path and higher exit
   speed coexist with locally lower utilization/more propulsion, particularly
   q=.75 at progress .9: u .9304 vs 1.0368 and usable drive 190.57 vs 142.90 N.
   This is not uniform: progress .8 and 1 can have higher utilization and less
   usable drive. The independent used-budget diagnostic gives no evidence that
   q=.5/.75 achieve this by nearly saturating lateral execution.
6. **Meaningful size and continuity?** The zero-reserve gains are 9.53/22.16 ms.
   q=.75 outward +5 cm costs 2.256 ms; exit fan -5/+5 cm changes time by
   +3.287/-0.506 ms. These are small finite changes on valid sides. Inward
   translation is TrackBoundary-invalid; no smoothness claim across that boundary
   or positive-reserve robust effect is established.
7. **Structurally promising?** Yes as a hypothesis of shared positive propulsion;
   no as a demonstrated physically robust nonconstant optimum. The execution
   fraction evidence weakens the original near-saturation concern, while the
   mesh-dependent reserve and correction abstraction prevent a stronger result.
8. **Select a q?** No. Neither original nor follow-up evidence supplies an empirical
   q calibration or measured combined-force tyre envelope.
9. **Correction unresolved?** Yes. No controller-label discontinuity or capacity
   disappearance was detected. Effective correction/scrub remains outside the
   positive-propulsion combined-force allocation, so a physically meaningful
   correction-phase exploitation cannot yet be excluded. It is not a proven bug.
10. **Continue or reject?** Continue analysis of this same subsystem. Define a
    physically interpretable execution margin independent of interval splitting
    before a new robustness experiment, and improve the evidence for correction.
    Do not reject the propulsion law from this mesh-dependent negative sweep,
    select q, or promote the result to production.

Production HeatSimulator, the 19.0f turning reference, #47/#48 historical reports,
the combined-grip formula, correction, geometry and optimizer are unchanged.
Validation and exact tested commit are recorded in the PR description.
