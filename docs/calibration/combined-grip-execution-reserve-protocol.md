# Combined-grip execution-reserve follow-up protocol

Analysis only; same Draft PR #50. Verified before edits: PR HEAD
`cea5ab20bb04796b81048e36a5d509771c355024`; main
`58771bac635d7d4962e11aa3ae5ad37a2d0c8d94`. No production calibration decision.

Freeze the combined-grip law, correction, turning envelope, spline, eleven
controls, geometry sampling, objective, starts and refinement tolerances.
Run the existing deterministic search for q=.5/.75 at reserve=0/.02/.05/.10 m.
The search generates 106 starts and requests up to 48 valid, geometry-diverse
starts for refinement. If fewer survive the extra validity gate, report that
count; do not manufacture valid starts or change the search.

Use the existing `lateralExecutionReserveMeters` exactly. At each replay interval:

`actualHeadroom = lateralCapacity(stepTime) - abs(exitOffset-entryOffset)`

`remainingBudget = max(0, lateralCapacity(stepTime)-reserve) - requiredMovement`

The unchanged gate rejects `remainingBudget < -0.0001 m`. The same reserve is
withheld from straight reposition capacity. It is an analysis validity test,
not a gameplay or tyre parameter. Record actual headroom separately from the
reserve-adjusted minimum; neither is a tyre-grip margin.

Important interpretation limit: capacity scales with interval time, including
short binding-constraint intervals and the final remainder. The clamp preserves
zero-motion feasibility even if capacity is below reserve; the existing absolute
gate tolerance can admit up to 0.1 mm movement in those intervals. Therefore this
literal existing reserve is a withheld movement budget, **not a guarantee of
2/5/10 cm actual minimum headroom for every valid trajectory**. Do not relabel
it as a mesh-independent real physical reserve, normalize by an invented length,
or change integration nodes to obtain a preferred outcome. Returning to inner
shows sensitivity to this execution-budget restriction; it alone neither
disproves the propulsion trade-off nor identifies its physical importance.

Report all eight cases, raw and adjusted margins, convergence, invalid counts,
controls, inner/free timing, path and exit-speed deltas. For surviving free winners,
show like-progress powered-exit forces at .6/.7/.8/.9/1 and maximum-curvature
controller/action states for each q at the largest reserve retaining a faster
non-constant winner. If none survives, show the largest-reserve winner and the
zero-reserve free winner as context. Four +/-5 cm translation/exit-fan probes
only, selecting the strongest surviving positive-reserve signal, otherwise the
zero-reserve q=.75 free winner. No further optimization.

No controller-label discontinuity or capacity disappearance was detected in
the original experiment. Effective correction/scrub remains outside the
positive-propulsion combined-force allocation, so a physically meaningful
correction-phase exploitation cannot yet be excluded. This is a limitation,
not a proven bug. Do not add a braking traction ellipse or invent a force split.

Regenerate the follow-up report in two independent processes; compare bytes and
SHA-256. Repeat Release build, complete .NET/Python tests and exact-HEAD CI.
Keep the PR Draft and unmerged for independent review.
