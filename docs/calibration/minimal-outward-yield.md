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
can bound the attainable margin. This never relaxes mechanical collision certification. A smaller certified seed is retained if the outer probe is uncertified.
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

Continuous-target pair and track-bound checks use the existing deterministic #55
arithmetic in B as well as C. Canonical production intervals are re-mapped with the
same nodes, time cuts, attitude and geometry; no path is interpolated or altered.
Native B collision observations retain their original arithmetic. Searches that
neither construct nor retain a continuous target also retain their pair arithmetic.
This prevents platform-native trigonometric differences from selecting different
continuous destinations. Actual production execution and contact checks still
retain their existing arithmetic modes.
Every competing pair in a search that constructs or retains a continuous target
uses that same arithmetic mode. Near-zero signed outward measurements, within
the existing #55 minimum-separation tolerance, also establish the inside/outside
role with deterministic geometry before building responses. Only that signed
metre measurement is replaced; the original collision certificate is retained.
No clearance or numerical tolerance is relaxed.

The #56C1/#56C2 historical reports also include production scenarios. Their explicit
`--minimal-yield` CI gate preserves controlled fixtures, sensitivity, raw grid,
threshold boundaries and recovery kernels exactly. Each changed production row
must have an independently observed positive `OutwardTargetTrials` count; unchanged
rows without target work stay exact. Complete leaf deltas are exported separately,
and aggregate consequence totals must equal their heat rows. The full report and
typed-bit files still match exactly across Windows/Ubuntu. The default historical
golden gates and the golden files remain unchanged.

The 96-heat #56C2 production matrix intentionally changes aggregate outcomes:
verified contacts **61 -> 53**, applied contacts **32 -> 29**, applied rider
consequences **45 -> 40**, and contact crashes **19 -> 24**. Major-save rider
consequences change **20 -> 16**, repeated-overlap suppressions **3 -> 0**, and
deferred contacts **26 -> 24**; geometry-unresolved remains zero. Fewer contacts
do not imply fewer crashes. These are resulting C behavior changes from changed
paths and encounters, not a retuning of severity thresholds or recovery. Fresh
base-main reports reproduce the untouched historical #56C1/#56C2 goldens exactly.

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

Sequential Windows benchmark medians (milliseconds; nine samples), measured
against the final runtime source `be8da06`:

These are the original, unoptimized #67 measurements. The certificate-reuse
measurements below describe the current implementation.

| Case | Main | PR | Wall change | Allocation change |
| --- | ---: | ---: | ---: | ---: |
| four-separated | 0.359 | 0.359 | +0.0% | +0.1% |
| H-three-squeeze | 45.396 | 227.722 | +401.6% | +167.0% |
| G-four-first-bend | 225.694 | 427.702 | +89.5% | +233.0% |
| real-bridge | 232.720 | 269.913 | +16.0% | +21.5% |
| safety-correction | 30.243 | 54.599 | +80.5% | +28.7% |
| I-heat-ON | 2281.229 | 5478.076 | +140.1% | +50.0% |
| I-heat-OFF | 286.593 | 288.267 | +0.6% | -0.1% |
| repeated-contact | 992.710 | 5877.597 | +492.1% | +35.4% |
| solo-heat | 70.195 | 73.228 | +4.3% | +0.2% |
| contact-diagnostics | 119.043 | 349.348 | +193.5% | +42.8% |
| eight-contact-resolutions-C | 914.542 | 2416.147 | +164.2% | +22.6% |
| I-heat-C | 3645.289 | 5186.417 | +42.3% | +25.2% |

The dense B squeeze and persistent B contact cases cost approximately five and six
times main respectively. The complete four-rider B heat costs 140.1% more. The
actual C contact batch costs 164.2% more, and the complete C four-rider heat costs
42.3% more. This is a material performance regression, not a performance improvement.
Additional bounded production/collision certification, consistent deterministic
geometry for target searches, and longer genuine failed-avoidance contact paths
contribute. Raw CPU, allocations and deterministic work counters are retained;
no joint cap increased. Separated/OFF/solo wall differences are +0.0%, +0.6%, and
+4.3%. These sequential final-source samples supersede the initial measurements.
Treat the dense/contact-heavy costs as a review consideration for this Draft PR.

The original ten-case protocol uses B/shadow diagnostics. The separate
`benchmark-contact` mode uses actual C consequences: eight fresh imminent-contact
resolutions (each asserts genuine applied contact consequences) and a complete
four-rider I/7 heat. Both binaries use the same harness, warm-up and sample protocol.
No claim about #66 replay or causal timing follows from these benchmarks.

## Behavior-preserving certificate reuse

The primary optimization baseline is #67 at `5373d594`, not main. Observation-only
profiling of that baseline attributed 82% of repeated-contact wall time to pair
compatibility and 17% to contact observation; trajectory integration was below 1%.
In the complete B heat, those geometry stages cost 63% and 26%, versus 2.8% for
candidate trajectories. MinimalOutward itself included about 15% of repeated-contact
time and 3.9% of full-heat time, predominantly nested geometry work. These nested
timings must not be summed with their children. Existing prefix and request caches
already remove repeated production inputs; reducing target iterations would change
the requested destinations and would miss the principal bottleneck.

One Resolve now owns a bounded exact-certificate cache shared by planning, safety
and executed-path observation. Its identity includes immutable interval references,
exact start/end bits, the incoming pair-wide minimum bits, and attribution mode.
The incoming minimum is essential: it determines #55's adaptive subdivision. Hits
restore the exact row and outgoing minimum, retain logical conflict/ambiguity work,
and count only mechanical evaluations actually performed. Jump-escape searches have
their own exact interval/time identity. Every caller still traverses the complete
common-time history and all executed intervals; quarantine, gaps, eligibility and
ready-time filtering are recomputed in their original order.

Canonical production intervals are interned only when all sampler and provenance
inputs match: geometry/attitude/dimension references, node progress and physical
offset bits, node time/duration, interval time cuts, corner phase/angle, frame, rider,
source, discontinuity and lap-wrap flags. This shares identical executed prefixes
without merging request identities, targets, production resolutions or later paths.
Trimmed history wrappers also retain exact source/end identity. The scope ends with
Resolve; nothing survives changes in snapshot, controls, incidents or time. Limits
are 4,000 sampler entries, 4,000 expensive interval certificates and 256 jump searches.
Endpoint-only searches are not stored (at most eight evaluations). Full caches simply
recompute through unchanged #55. No physics/calibration, search tolerance or 81-joint
limit changes.

The final 122-case baseline/head comparison contains **4,553,155 typed leaves** and
**3,650,054 IEEE leaves**, all identical. It includes OFF, reversal, B heats, C contact
fixtures and a complete C heat. All activity counters are identical; the only changed
InteractionWork field is reduced NarrowPhaseEvaluations. The new cached/uncached tests
also compare exact roots, minima, attribution, jump escape/recontact, time cuts and
complete production decisions/consequences. Local validation passed a zero-warning
Release build, 210 focused .NET tests and all 87 Python tests. CI runs the complete
.NET shards and the exact original-#67 comparison on both supported platforms.
The reverse source extraction was extended for the new helpers; all 39 original
skill/style reader hashes and all historical physical/calibration goldens stay frozen.

Fresh sequential Windows/.NET 8.0.30 medians follow; each case uses at least eight
complete warmups and three seconds, then nine samples. Triples are **main / original
#67 / optimized #67**, MB means 1,000,000 bytes. CPU is process CPU across threads.
All samples, counters and the guard confirmation are retained in the compact
[performance evidence](minimal-outward-yield-evidence/performance-optimization.json).

| Case | Wall ms: main / before / after | CPU ms: main / before / after | MB: main / before / after | Wall reduction vs before |
| --- | ---: | ---: | ---: | ---: |
| four-separated | 0.362 / 0.367 / 0.412 | 0.000 / 0.000 / 0.000 | 0.179 / 0.179 / 0.179 | -12.2% |
| H-three-squeeze | 47.554 / 232.887 / 233.333 | 46.875 / 250.000 / 234.375 | 5.779 / 15.430 / 16.489 | -0.2% |
| G-four-first-bend | 231.615 / 429.246 / 411.188 | 234.375 / 437.500 / 406.250 | 5.239 / 17.446 / 19.436 | 4.2% |
| real-bridge | 229.846 / 270.927 / 165.058 | 234.375 / 265.625 / 171.875 | 4.148 / 5.041 / 5.558 | 39.1% |
| safety-correction | 31.046 / 54.939 / 37.183 | 31.250 / 62.500 / 31.250 | 2.503 / 3.223 / 3.509 | 32.3% |
| I-heat-ON | 2424.371 / 5594.566 / 4198.806 | 3140.625 / 6312.500 / 4953.125 | 244.256 / 366.208 / 413.799 | 24.9% |
| I-heat-OFF | 306.062 / 305.412 / 298.628 | 1015.625 / 984.375 / 1062.500 | 151.760 / 151.844 / 151.844 | 2.2% |
| repeated-contact | 1020.667 / 5926.523 / 2346.125 | 1015.625 / 5921.875 / 2343.750 | 18.495 / 25.031 / 28.933 | 60.4% |
| solo-heat | 75.474 / 76.837 / 74.066 | 250.000 / 265.625 / 250.000 | 38.964 / 38.989 / 38.950 | 3.6% |
| contact-diagnostics | 123.771 / 350.377 / 178.950 | 125.000 / 343.750 / 171.875 | 2.057 / 2.938 / 3.425 | 48.9% |
| eight-contact-resolutions-C | 916.196 / 2319.978 / 1181.644 | 921.875 / 2312.500 / 1187.500 | 17.030 / 20.863 / 24.787 | 49.1% |
| I-heat-C | 3590.634 / 5131.896 / 3833.369 | 4328.125 / 5828.125 / 4484.375 | 276.424 / 346.193 / 390.215 | 25.3% |

| Case | Production | Narrow phase | Target trials | Episode IDs | Clusters | Joint combinations |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| H-three-squeeze | 60 / 180 / 180 | 89624 / 411859 / 411226 | 0 / 24 / 24 | 1 / 1 / 1 | 1 / 1 / 1 | 54 / 54 / 54 |
| I-heat-ON | 668 / 1546 / 1546 | 4330040 / 10338094 / 7104556 | 0 / 65 / 65 | 9 / 11 / 11 | 24 / 40 / 40 | 333 / 837 / 837 |
| repeated-contact | 184 / 241 / 241 | 2333520 / 12578347 / 4933417 | 0 / 48 / 48 | 8 / 1 / 1 | 8 / 8 / 8 | 144 / 144 / 144 |
| eight-contact-resolutions-C | 184 / 248 / 248 | 2303600 / 5975784 / 2924840 | 0 / 48 / 48 | 1 / 1 / 1 | 8 / 8 / 8 | 144 / 144 / 144 |
| I-heat-C | 916 / 1478 / 1478 | 7226535 / 10196565 / 6870073 | 0 / 59 / 59 | 8 / 11 / 11 | 29 / 36 / 36 | 495 / 837 / 837 |

Episode IDs are tracker-local. The persistent B case retains one #67 episode through
eight observations (main had eight episodes). The C batch starts eight fresh trackers:
ID 1 is reused, representing eight independent episodes in every version.
The first separated sample set showed a 0.045 ms increase; a paired confirmation with
the same warmup/nine-sample protocol measured **0.3580 -> 0.3583 ms** (+0.08%), with
264 mechanical evaluations and eight extra allocated bytes. OFF/solo wall medians
decrease 2.2%/3.6%; their allocations are effectively unchanged. The primary OFF
CPU median rises 7.9%; a separate paired OFF confirmation measured 291.148 ->
298.705 ms wall (+2.6%) with identical 1,015.625 ms CPU and essentially identical
allocations. No OFF CPU improvement or material guard regression was established.

The largest repeated-contact reduction is **60.4% wall / 60.4% CPU**; the C contact
batch improves **49.1% wall / 48.6% CPU**. Complete B/C heats improve **24.9%/25.3%**
wall and **21.5%/23.1%** CPU. Dense-case allocations increase roughly **7–19%** because
bounded certificates are retained. The squeeze is effectively unchanged and remains
about five times main: its costly post-reaction targets mostly produce distinct
samplers that require distinct certification. This optimization stops short of
changing those decisions or relaxing collision bounds.

Interaction activity also remains a cost: full B has 11 episodes/40 clusters instead
of main's 9/24, with 837 joint combinations instead of 333. Whole-heat narrow work
normalized by clusters falls from 258,452 before optimization to 177,614 after
(main: 180,418); this is a workload proxy, not a per-episode timing measurement.
The added activity and genuine failed-avoidance histories explain remaining costs
that cannot be removed while reproducing current #67 outcomes. No B/C behavior or
calibration change is introduced by this optimization. The original intentional
main-to-#67 C changes above remain separate; #66 is untouched and no causal-timing
or general-replay fix is claimed.
