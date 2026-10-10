# Geometry-based minimal outward yielding

**BINDING execution architecture; PROVISIONAL existing synthetic balance.**
Base: current main `f40b2fd129124d969fd22cfe7767e2fb00bd651f` (#65).
This change has no dependency on Draft #66. It does not fix or certify #66 causal
timing or general replay. Older #56B/#56C evidence and all calibration goldens remain
historical records; changed interaction-enabled results are reported separately.

The root cause was `Alternatives` rounding continuous `LateralPosition` to a
reference lane and requesting `current + 1` for `YieldOutward` and
`ContinueOutside`. Production correctly limited movement speed, but the requested
destination could demand a complete reference-lane move after sufficient clearance
had already been obtained.

`TrajectoryIntent` keeps its three integer manager anchors. An interaction response
can additionally carry `InteractionLateralTarget.OffsetFromInnerReferenceMeters`.
The current segment's canonical physical geometry converts that metre destination
to continuous lateral position. The same physical destination is carried through
the projected horizon and converted again at every segment. It is never a fixed
normalized-lane increment. Requests remain subject to the existing movement budget,
surface grip, local corner envelope, coupled time/distance solve, throttle controls,
track limits, and forced run-wide/crash outcomes.

For one outward response, the coordinator first projects holding the current physical
offset. A #55 signed footprint-separation shortfall supplies a metre-valued seed.
If necessary it checks the outer physical span, bounded by the narrowest segment in
the horizon. It refines a certified upper destination with at most eight deterministic
bisections, stopping at 0.02 m. At most eleven trials construct one planning response;
an actual-incident safety correction can add at most eleven current-path trials.
The retained destination is the smallest *found* safe outward request within this
bounded bracket. The residual bracket is at most `max(0.02 m, initialBracket / 256)`;
there is no claim of a global optimum for non-monotonic coupled trajectories.

Every trial uses production executed paths, motorcycle footprints, #55 common-time
compatibility, coverage/ambiguity rejection and complete track-bound certification.
It checks all relevant inside requests. The existing joint search certifies all pairs,
including outside opponents, before selecting a response. The existing 81-combination
limits and two-pass ownership remain unchanged; target-construction trials are reported
separately in `OutwardTargetTrials` and counted in production/pair/narrow-phase work.

The preferred clearance buffer uses the existing execution-margin/style formula. If
the initial gap prevents attaining that buffer, a mechanically certified endpoint
can bound the attainable margin. This never relaxes mechanical collision certification.
If no endpoint is certified, the ordinary held/back-out/intent choices and actual
contact handling decide the outcome. No movement, time, gap or collision state is
invented. Actual incident options are replayed, and the existing safety pass can
refine its single emergency target against the actual paths before joint certification.

Target values participate in projection identity, optional-safety success/failure
caching, commitment retention, diagnostics, deviation cost and addressed tactical
tie comparison. Requests without targets keep their previous tie address. A retained
feasible commitment keeps the exact metre destination, avoiding cumulative outward
creep. Lift/hold controls still expire after the current projected prefix; the
physical destination persists through projection. Production requests are still
reissued per step; this is not a general replay guarantee.

Small arrival splits can make an attitude knot round to an existing common-time
endpoint. The #55 adapter omits only zero-duration cuts; adjacent positive intervals
cover the path. It preserves discontinuity flags and does not discard a positive
contact interval.

Fresh baseline/head measurements are in `minimal-outward-yield-evidence/`. In the
established `D-inside-overlap` attack, the outside rider moves **1.8000002 m before**
and **0.18281269 m after**, an **89.84374% reduction**, in both B and C. Independent
executed-path certification measures minimum signed footprint separation
**0.291609289 m**; selected full-horizon projections are also independently checked.
The audit covers 44 cases: the 13 existing fixtures plus attacks with two, three and
four riders on three segment types, in B/C. All 40 certified path sets contain zero
mechanical overlaps; the four uncertified sets are the existing three-rider squeeze
and initial imminent overlap in B/C. They retain genuine contact. These are synthetic
tests, not a real-world calibration.

Intentional behavior changes: interaction-enabled B/C may choose a shorter outward
destination, hold where existing clearance suffices, or retain contact when a bounded
outward attempt cannot certify clearance. The imminent-overlap fixture now holds
instead of sweeping 2.597228 m outward, while remaining in contact. C therefore can
have changed contact timing, pair episodes, recovery and final speeds; its contact
consequence formulas and parameters are unchanged. No A behavior, motorcycle
dimensions, grip constants, longitudinal formulas or balance calibration are changed.
The CI audit requires exact feature-OFF/unrelated captures, exact rider-order parity,
strict C cross-platform traces and final states, and records every intentional B/C
typed leaf delta. Native B double-geometry differences are recorded separately;
float/domain divergence is rejected.

Reproduce measurements with the same harness compiled against base main and head:

```sh
git worktree add --detach ../yield-main f40b2fd129124d969fd22cfe7767e2fb00bd651f
dotnet build tools/minimal-yield-audit -c Release -p:CoreSimRoot="$PWD/../yield-main" -o ../yield-before-bin
dotnet build tools/minimal-yield-audit -c Release -o ../yield-after-bin
dotnet ../yield-before-bin/CoreSim.Tests.dll before.json
dotnet ../yield-after-bin/CoreSim.Tests.dll after.json
python tools/minimal-yield-audit/measure.py before.json after.json measurements.json
```

Performance uses the existing `tools/contested-performance` harness, eight complete
warm-ups and at least three seconds per case, then nine samples, run sequentially
against main/head without concurrent test work. Raw wall/CPU/allocation/work samples
and the median comparison are retained. Dense/contact-heavy cases are explicitly
included; no wall-clock threshold is added to CI.

Sequential Windows benchmark medians (milliseconds; nine samples):

| Case | Main | Change | Wall change | Allocation change |
| --- | ---: | ---: | ---: | ---: |
| four-separated | 0.380 | 0.358 | -5.8% | +0.0% |
| H-three-squeeze | 46.904 | 191.133 | +307.5% | +130.5% |
| G-four-first-bend | 230.746 | 364.802 | +58.1% | +193.7% |
| real-bridge | 229.181 | 234.002 | +2.1% | +11.1% |
| safety-correction | 31.110 | 47.019 | +51.1% | +12.2% |
| I-heat-ON | 2357.970 | 4234.258 | +79.6% | +38.0% |
| I-heat-OFF | 304.240 | 310.842 | +2.2% | +0.1% |
| repeated-contact | 1009.797 | 5121.557 | +407.2% | +17.4% |
| solo-heat | 77.356 | 76.439 | -1.2% | -0.0% |
| contact-diagnostics | 122.780 | 308.200 | +151.0% | +23.3% |

The dense squeeze and repeated-contact cases cost approximately four and five times
main respectively. The complete four-rider enabled heat costs 79.6% more; the
separated/OFF/solo cases show -5.8%, +2.2%, and -1.2% wall differences. This is a
material performance regression, not a performance improvement. Additional bounded
production/collision certification and the longer genuine failed-avoidance contact
paths both contribute. The raw deterministic work counters distinguish target-search
work from the unchanged joint cap. No cap was increased, and no performance claim
is based on the earlier concurrent measurements. Treat these costs as a review
consideration for this Draft PR.
