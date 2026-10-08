# Contested-space performance optimization (#60)

This change removes identical production resolutions, full geometric observations,
and pose construction from the merged #56B/#56C1 engine. It also reuses exact
constant metric geometry and capsule vectors. It changes no tactical search,
physical equation, tolerance, integration step, random address, or eligible pair.

Baseline: `5989301192565f6a265d53a2db12c7d00c94fedc` (merged main, #58).
Production optimization: `44a2d2f6261ad697d0003911825da72fe8fbbb56`.
Uninstrumented measurement harness: `a76fe303c1ad29dc9f2771a934a44a3af96df566`.
Subsequent commits extend audit coverage/scopes and contain measurement evidence
and tooling only. PR #59 and #56D
are outside this change. No PR is merged.

## Environment and reproducible protocol

Measured on the same Windows 10.0.19045 x64 machine, AMD Ryzen 5 3500X
(AMD64 Family 23 Model 113 Stepping 0, AuthenticAMD), six logical processors,
.NET 8.0.26, SDK 10.0.101,
Release net8.0. No debugger or synchronous projection counter observer is attached;
the established maximum degree of four trajectory workers remains enabled.

Both versions use the same checked-in executable source. `CoreSimRoot` selects a
detached original-main checkout for the before build. Run builds and tests before
measurement; use separate output directories and run each measurement process
serially on an otherwise idle machine. Logging is off for timings; production
interaction diagnostics are Summary. Optional #56C1 diagnostics are None except
for the separate contact-diagnostics case.

Each case warms for at least eight complete executions and at least three seconds,
then records nine samples. Values below are sample medians; all samples, warm-up
counts, work counters, process CPU, allocated bytes and GC collections are in
[`contested-space-performance-optimization.json`](contested-space-performance-optimization.json).
The earlier five-warm-up runs exposed early-sample drift in the short squeeze
case (roughly 103 to 87 ms within a series), so both versions were rerun with this
identical extended protocol. Earlier exploratory measurements are not the final
comparison. Historical measurements from other machines are not a baseline.

Cases execute in the listed order within each process, sharing its runtime/JIT
profile history; both versions follow that same order. The shorter exploratory
protocol measured the I heat at 4.450 → 3.198 s (28.1% reduction), whereas the
final extended protocol measures 5.209 → 3.682 s (29.3%). Do not mix baselines
between these series. Both raw series are retained, and neither reaches 30%.

Wall time is `Stopwatch`, process CPU is `Process.TotalProcessorTime`, and total
allocation is `GC.GetTotalAllocatedBytes(precise: true)` including parallel workers.
MB means decimal cumulative allocated MB, not retained or peak memory. This
Windows process CPU clock has 15.625 ms increments; CPU totals for short cases
cannot resolve small differences. GC values are collections per measured execution.

```powershell
$baseline = (Resolve-Path ../baseline-plain).Path
dotnet build tools/contested-performance -c Release --warnaserror "-p:CoreSimRoot=$baseline" -o ../before-bin
dotnet build tools/contested-performance -c Release --warnaserror -o ../after-bin
dotnet ../before-bin/CoreSim.Tests.dll measure ../before.json
dotnet ../after-bin/CoreSim.Tests.dll measure ../after.json
```

Create `baseline-plain` with `git worktree add --detach ../baseline-plain
5989301192565f6a265d53a2db12c7d00c94fedc`. For subsystem measurements, create two
additional disposable checkouts at the baseline and optimization commits, apply
`python tools/contested-performance/instrument.py CHECKOUT` to each, and build the
same harness with their absolute `CoreSimRoot` paths. Never apply observation-only
instrumentation to production source or compare its timings with uninstrumented
totals.

## Baseline profiling and confirmed bottlenecks

Profiling preceded production edits. EventPipe `dotnet-trace` 10.0.750501 with
`dotnet-sampled-thread-time` sampled 100 measured dense squeeze executions plus
the initial five warm-ups. The CPU-only
trace avoids the finalizer/wait pollution observed in an initial combined GC trace.
The exported Speedscope stacks are summarized by `profile-summary.py`; the summary
is retained in the structured report. These are sampled managed thread times, not
an exact additive CPU accounting. Synchronous hot stacks identify computation;
inlining can attribute work to the caller.

```powershell
dotnet-trace collect --profile dotnet-sampled-thread-time --format Speedscope --output ../baseline-cpu.nettrace --show-child-io -- dotnet ../before-bin/CoreSim.Tests.dll measure ../baseline-cpu-bench.json H-three-squeeze 100
python tools/contested-performance/profile-summary.py ../baseline-cpu.speedscope.json ../baseline-cpu-summary.json
```

| Exclusive sampled hot method | Share |
| --- | ---: |
| IntervalSearch.Gap | 21.84% |
| List.ToArray | 11.02% |
| ProductionPoseInterval.Geometry | 10.05% |
| IntervalSearch.Evaluate | 7.24% |
| ContestedSpaceResolver.Observe | 7.01% |
| List.set_Capacity | 6.74% |
| Array.Resize | 6.09% |
| IntervalSearch.Search | 3.77% |
| MechanicalSeparation.Between | 2.73% |
| BikeFootprint.Create | 2.24% |

Inclusive sampled CPU attribution also places pair compatibility requests at
62.7%, candidate projection at 6.8%, history stitching at 7.1%, full contact
observation at 5.7%, safety pair compatibility at 4.5%, and all production
materialization calls together at 4.8%. These stack scopes overlap and inlining
can remove a separate frame; the remaining small subsystems cannot be ranked
reliably by this sample. The controlled scopes below provide their elapsed time
and allocation separately rather than inventing precise per-stage CPU totals.

Observation-only scopes independently measured elapsed time, allocation and call
counts for all ten requested subsystems. They also counted all trajectory
evaluations, prefix executions/hits, projection requests and pair-cache requests.
The initial full-heat profile attributed approximately 2.53 s to full contact
observations and 1.50 s to compatibility geometry, versus 74 ms to candidate
projection and 1.2 ms to joint scoring. Geometric observation, including repeated
observations of identical final paths, was the valuable target. Candidate count
alone was not a useful proxy for cost.

## Implementation and exact reuse conditions

1. **Separated fast path:** use the initial independent production result. The
   removed second call had the same immutable snapshot, intents and options and
   `legacyContacts: false`. The already computed full observation is also the
   exact observation required after retaining these intervals.
2. **Final materialization:** when `fallbackPairs.Count == 0`, use the verified
   actual production result. Previously the same requests were resolved again
   with a legacy-contact filter rejecting every pair before contact RNG. When
   any fallback is authorized, retain the original production call and observe
   its changed executed paths. No safety observation or fallback is skipped.
3. **Verified history retention:** `RetainVerified` receives the full report of
   exactly the current immutable history plus the intervals being retained. It
   performs the same unresolved-boundary quarantine and cutoff calculation as a
   fresh full observation. Episode updates between observation and retention do
   not mutate pose history. Future steps still observe their own complete inputs.
4. **Pose reuse:** a local reference-identity dictionary maps immutable resolved
   motion objects to their immutable intervals for this coordinator invocation
   and frozen track embedding. No value-based candidate merging, global cache,
   cross-heat cache or new persistent state is introduced. Prepared interval
   constants are derived solely from that interval's immutable segment and rates.
5. **Allocation and geometry:** read progress knots once per motion, reuse the
   node cut buffer, size interval lists up front, and omit unused straight endpoint
   transforms in `RateBounds`. Validate both endpoint times as before. Prepare
   constant straight tangent/velocity/headings and corner center once; preserve
   all arithmetic order and signed-zero behavior. Prepare each capsule direction
   and squared length once for the four component comparisons, preserving the
   original intersection tests, clamp, minimum and component tie order.

The existing trajectory prefix graph, projection key (rider, complete intent,
drive fraction, lateral hold) and pair compatibility caches already remove
repeated tactical work. Their identities, bounds and lifetime remain unchanged.
No lane-only cache or approximate outcome equivalence is introduced. Alternative
enumeration, all six eligible four-rider pairs, canonical tie breaking, surface
reads/wear and both existing response passes remain unchanged.

`DeterministicRandom` is stateless and addressed by domain keys. A removed
identical replay would recompute the same addressed rider samples; no generator
state or stream position advances. Retained physical computations use the same
addresses and values in the same canonical rider order. The empty legacy filter
also returns before any contact sample is requested. RNG implementation and all
surviving incident/contact decisions are unchanged.

Only `ProductionResolutions`, `ActualProductionVerifications` and
`NarrowPhaseEvaluations` may decrease. The first two count eliminated calls and
fresh observations; the third counts geometric work avoided by reusing their
unchanged full reports. Projection/pair misses, combinations, response passes,
safety passes, components and fallback attempts remain exact. Historical golden
files are untouched. Their comparator admits decreases only in these three fields
inside the exact `InteractionWork` schema; every other historical field remains
exact. Tests reject increased counters, schema changes and physical drift.

## Before/after measurements

| Scenario | Before ms | After ms | Speedup | Time reduction | Before MB | After MB | Allocation reduction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| four-separated | 1.358 | 0.589 | 2.31x | 56.6% | 0.320 | 0.178 | 44.3% |
| H-three-squeeze | 89.821 | 74.231 | 1.21x | 17.4% | 6.143 | 5.793 | 5.7% |
| G-four-first-bend | 421.831 | 372.019 | 1.13x | 11.8% | 5.718 | 5.243 | 8.3% |
| real-bridge | 396.874 | 356.666 | 1.11x | 10.1% | 4.259 | 4.232 | 0.6% |
| safety-correction | 52.634 | 48.507 | 1.09x | 7.8% | 2.785 | 2.497 | 10.3% |
| I-heat-ON | 5209.356 | 3681.775 | 1.41x | 29.3% | 269.463 | 243.931 | 9.5% |
| I-heat-OFF | 392.118 | 397.047 | 0.99x | -1.3% | 151.595 | 151.654 | -0.04% |
| repeated-contact | 1754.558 | 1584.203 | 1.11x | 9.7% | 20.309 | 18.473 | 9.0% |
| solo-heat | 96.284 | 96.169 | 1.00x | 0.1% | 38.849 | 38.849 | 0.0% |
| contact-diagnostics | 213.930 | 193.854 | 1.10x | 9.4% | 2.256 | 2.052 | 9.0% |

A is four spatially separated riders; B is the existing H three-rider squeeze;
C includes G connected four-rider and real-bridge fixtures; D is scenario I,
four complete laps; E repeats imminent overlap eight times with the same episode
tracker and commits between coverage gaps; F uses the existing seed-276 safety
correction regression (heat id 57, incident frequency 2), including tactical
alternatives and an actual safety correction. I OFF and solo are regression
controls. The diagnostic case enables FullAudit #56C1 on imminent overlap.

| Scenario | Before CPU ms | After CPU ms | Before GC 0/1/2 | After GC 0/1/2 |
| --- | ---: | ---: | --- | --- |
| four-separated | 0.000 | 0.000 | 0/0/0 | 0/0/0 |
| H-three-squeeze | 93.750 | 78.125 | 1/1/0 | 1/1/0 |
| G-four-first-bend | 421.875 | 375.000 | 1/1/0 | 1/1/0 |
| real-bridge | 406.250 | 359.375 | 0/0/0 | 1/1/0 |
| safety-correction | 46.875 | 46.875 | 0/0/0 | 0/0/0 |
| I-heat-ON | 6093.750 | 4609.375 | 34/16/2 | 31/13/2 |
| I-heat-OFF | 1265.625 | 1312.500 | 20/6/0 | 20/0/0 |
| repeated-contact | 1750.000 | 1578.125 | 2/1/0 | 2/1/0 |
| solo-heat | 328.125 | 265.625 | 5/1/0 | 5/1/0 |
| contact-diagnostics | 218.750 | 187.500 | 0/0/0 | 0/0/0 |

| Scenario | Production before → after | Saved narrow phase | Unique projections | Pair misses | Joint combinations | Verification before → after | Safety passes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| four-separated | 2 → 1 | 264 | 0 | 0 | 0 | 2 → 1 | 0 |
| H-three-squeeze | 60 → 60 | 360 | 14 | 35 | 54 | 3 → 3 | 1 |
| G-four-first-bend | 43 → 42 | 24,874 | 10 | 37 | 81 | 2 → 1 | 0 |
| real-bridge | 40 → 40 | 2,331 | 17 | 63 | 162 | 3 → 3 | 1 |
| safety-correction | 31 → 30 | 264 | 11 | 15 | 18 | 3 → 2 | 1 |
| I-heat-ON | 704 → 668 | 1,375,449 | 165 | 427 | 333 | 73 → 37 | 1 |
| I-heat-OFF | 36 → 36 | 0 | 0 | 0 | 0 | 0 → 0 | 0 |
| repeated-contact | 184 → 184 | 4,020 | 72 | 88 | 144 | 24 → 24 | 8 |
| solo-heat | 36 → 36 | 0 | 0 | 0 | 0 | 0 → 0 | 0 |
| contact-diagnostics | 23 → 23 | 268 | 9 | 11 | 18 | 3 → 3 | 1 |

Unique projections, pair misses, joint combinations and safety passes are unchanged in every row. No trajectory evaluation is removed.

Prefix executions below include the existing independent decision evaluator as
well as coordinator projections; they are not the same scope as the coordinator's
production-resolution counter. Projection misses include recorded failed
projections; requests minus misses give cache reuse. Pair misses are actual
compatibility observations. All scoped counters are independent of sample timing.

| Scenario | Projection hits/misses before → after | Pair hits/misses before → after | Prefix hits/executions before → after | All trajectory evaluations before → after |
| --- | ---: | ---: | ---: | ---: |
| four-separated | 0/0 → 0/0 | 0/0 → 0/0 | 0/0 → 0/0 | 0 → 0 |
| H-three-squeeze | 634/14 → 634/14 | 187/35 → 187/35 | 0/54 → 0/54 | 9 → 9 |
| G-four-first-bend | 662/10 → 662/10 | 515/37 → 515/37 | 0/40 → 0/40 | 10 → 10 |
| real-bridge | 2719/17 → 2719/17 | 1029/63 → 1029/63 | 1/35 → 1/35 | 12 → 12 |
| safety-correction | 131/11 → 131/11 | 22/15 → 22/15 | 0/24 → 0/24 | 6 → 6 |
| I-heat-ON | 3819/165 → 3819/165 | 2043/427 → 2043/427 | 6011/9964 → 6011/9964 | 3532 → 3532 |
| I-heat-OFF | 0/0 → 0/0 | 0/0 → 0/0 | 6008/9356 → 6008/9356 | 3376 → 3376 |
| repeated-contact | 1080/72 → 1080/72 | 216/88 → 216/88 | 0/144 → 0/144 | 48 → 48 |
| solo-heat | 0/0 → 0/0 | 0/0 → 0/0 | 1502/2339 → 1502/2339 | 844 → 844 |
| contact-diagnostics | 135/9 → 135/9 | 27/11 → 27/11 | 0/18 → 0/18 | 6 → 6 |

## Subsystem costs and allocation ranking

Scopes are **inclusive and overlap**: candidate evaluation includes pose creation;
pair checks include compatibility geometry; safety includes safety projections,
pairs and scoring; coordinator includes its child scopes. They must not be summed.
Elapsed scopes contain synchronous computation plus any scheduling delay.
Per-stage allocation uses the current thread; total allocations above include all
workers. Independent parallel decisions occur before the coordinator scope.
Absent stages have zero calls, rather than an unmeasured estimate.
All initial, safety and final cluster builds are scoped. Final materialization
also includes the baseline quiet-path replay. Pass-1 candidate/pair/scoring rows
have separate safety child scopes in the JSON and are included in the whole
safety-pass row.

### H-three-squeeze

| Subsystem | Before ms | After ms | Before MB | After MB | Calls before → after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Initial independent production | 0.215 | 0.215 | 0.085 | 0.085 | 1 → 1 |
| Common-time pose construction | 0.580 | 0.624 | 1.293 | 1.002 | 80 → 68 |
| Full physical contact observations | 17.873 | 12.947 | 0.783 | 0.711 | 6 → 5 |
| All cluster construction | 0.016 | 0.015 | 0.005 | 0.005 | 3 → 3 |
| Candidate trajectory requests (pass 1) | 5.026 | 4.408 | 2.342 | 2.288 | 180 → 180 |
| Pair compatibility requests (pass 1) | 61.323 | 50.436 | 1.569 | 1.569 | 114 → 114 |
| Joint scoring (pass 1) | 0.080 | 0.076 | 0.052 | 0.052 | 27 → 27 |
| Whole safety pass | 4.838 | 3.635 | 0.477 | 0.419 | 1 → 1 |
| Final materialization | 0.135 | 0.133 | 0.074 | 0.074 | 1 → 1 |
| Contact analysis/diagnostics | 0.000 | 0.000 | 0.000 | 0.000 | 0 → 0 |
| Compatibility geometry (child) | 65.329 | 53.466 | 1.697 | 1.697 | 35 → 35 |
| Coordinator (inclusive) | 90.737 | 73.428 | 6.138 | 5.774 | 1 → 1 |

Largest baseline allocation scopes: candidate-trajectory (2.342 MB), pair-compatibility (1.569 MB), common-time-pose-construction (1.293 MB). These overlap.

### I-heat-ON

| Subsystem | Before ms | After ms | Before MB | After MB | Calls before → after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Initial independent production | 15.213 | 15.361 | 3.926 | 3.923 | 36 → 36 |
| Common-time pose construction | 8.792 | 8.394 | 16.747 | 10.830 | 1200 → 833 |
| Full physical contact observations | 3031.470 | 1614.641 | 43.415 | 27.710 | 127 → 73 |
| All cluster construction | 0.175 | 0.181 | 0.034 | 0.034 | 54 → 54 |
| Candidate trajectory requests (pass 1) | 77.277 | 73.122 | 28.200 | 27.598 | 1920 → 1920 |
| Pair compatibility requests (pass 1) | 1704.318 | 1469.003 | 24.560 | 24.560 | 1930 → 1930 |
| Joint scoring (pass 1) | 1.224 | 1.263 | 0.626 | 0.626 | 252 → 252 |
| Whole safety pass | 97.828 | 85.919 | 1.481 | 1.432 | 1 → 1 |
| Final materialization | 14.367 | 0.002 | 3.853 | 0.000 | 36 → 18 |
| Contact analysis/diagnostics | 0.000 | 0.000 | 0.000 | 0.000 | 0 → 0 |
| Compatibility geometry (child) | 1800.293 | 1554.249 | 25.151 | 25.151 | 427 → 427 |
| Coordinator (inclusive) | 4967.973 | 3287.352 | 119.999 | 94.469 | 36 → 36 |

Largest baseline allocation scopes: contact-detection (43.415 MB), candidate-trajectory (28.200 MB), pair-compatibility (24.560 MB). These overlap.

### safety-correction

| Subsystem | Before ms | After ms | Before MB | After MB | Calls before → after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Initial independent production | 0.208 | 0.205 | 0.085 | 0.085 | 1 → 1 |
| Common-time pose construction | 0.306 | 0.277 | 0.615 | 0.435 | 43 → 33 |
| Full physical contact observations | 13.412 | 11.521 | 0.238 | 0.190 | 6 → 4 |
| All cluster construction | 0.016 | 0.008 | 0.003 | 0.003 | 3 → 3 |
| Candidate trajectory requests (pass 1) | 3.854 | 3.555 | 1.311 | 1.278 | 46 → 46 |
| Pair compatibility requests (pass 1) | 28.868 | 25.352 | 0.446 | 0.446 | 19 → 19 |
| Joint scoring (pass 1) | 0.030 | 0.019 | 0.014 | 0.014 | 9 → 9 |
| Whole safety pass | 6.817 | 6.304 | 0.248 | 0.216 | 1 → 1 |
| Final materialization | 0.119 | 0.000 | 0.060 | 0.000 | 1 → 1 |
| Contact analysis/diagnostics | 0.000 | 0.000 | 0.000 | 0.000 | 0 → 0 |
| Compatibility geometry (child) | 35.235 | 31.277 | 0.505 | 0.505 | 15 → 15 |
| Coordinator (inclusive) | 54.048 | 47.649 | 2.778 | 2.490 | 1 → 1 |

Largest baseline allocation scopes: candidate-trajectory (1.311 MB), common-time-pose-construction (0.615 MB), pair-compatibility (0.446 MB). These overlap.

### contact-diagnostics

| Subsystem | Before ms | After ms | Before MB | After MB | Calls before → after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Initial independent production | 0.125 | 0.127 | 0.048 | 0.048 | 1 → 1 |
| Common-time pose construction | 0.276 | 0.274 | 0.562 | 0.390 | 35 → 27 |
| Full physical contact observations | 46.059 | 40.927 | 0.252 | 0.220 | 6 → 5 |
| All cluster construction | 0.015 | 0.028 | 0.004 | 0.004 | 3 → 3 |
| Candidate trajectory requests (pass 1) | 2.249 | 2.293 | 0.890 | 0.864 | 48 → 48 |
| Pair compatibility requests (pass 1) | 131.384 | 121.203 | 0.328 | 0.328 | 20 → 20 |
| Joint scoring (pass 1) | 0.027 | 0.040 | 0.014 | 0.014 | 9 → 9 |
| Whole safety pass | 39.427 | 34.324 | 0.157 | 0.125 | 1 → 1 |
| Final materialization | 0.204 | 0.213 | 0.082 | 0.082 | 1 → 1 |
| Contact analysis/diagnostics | 0.102 | 0.107 | 0.025 | 0.025 | 1 → 1 |
| Compatibility geometry (child) | 170.471 | 160.238 | 0.352 | 0.352 | 11 → 11 |
| Coordinator (inclusive) | 220.635 | 205.268 | 2.249 | 2.045 | 1 → 1 |

Largest baseline allocation scopes: candidate-trajectory (0.890 MB), common-time-pose-construction (0.562 MB), pair-compatibility (0.328 MB). These overlap.


## Exact equivalence and validation

The same harness is compiled against original main and optimized source on each
OS. It walks every public output leaf into an ordinal canonical map, representing
each float/double by raw IEEE bits; the only omitted object is `InteractionWork`.
It captures selected trajectories, all available per-step snapshot progression,
motions, speeds/times, physical lateral positions, raw surfaces/wear, conditions,
episodes and provenance, physical contact analysis, event order, logs, final
riders and classification. Case hashes are hashes of exact typed leaves; use
`capture OUTPUT --raw` or `--raw-case=NAME` to inspect raw leaves. No numeric
rounding, tolerance or normalization is used.

CI captures **all** raw leaves with `--raw` on both platforms, before and after
optimization. Each raw file must match its manifest SHA256 and leaf/IEEE counts.
`tools/contested-performance/compare.py` requires exact original-main preservation
independently on Windows and Ubuntu, including rider-order equality. It then
compares the complete Windows/Ubuntu divergence map before optimization with the
map after optimization: every case, field path, capture type and exact IEEE bit
pattern on both platforms must be unchanged. Introduced, removed or changed
divergences fail. This is computed from every case without a case whitelist;
missing raw captures fail, and missing field paths are explicit map entries.

The `platform-comparison` CI artifact retains both complete maps in
`contested-performance-comparison.json`, alongside the existing comparison
report. The previously existing trajectory, physical-space, rider-compatibility,
contested-space and physical-contact cross-platform tests and golden files remain
unchanged by this review correction. Original main's platform differences are
tracked separately in [numerical portability issue #62](https://github.com/arturgladysz91/zuzel/issues/62)
and [the portability investigation note](contested-space-numerical-portability.md).

There are 108 cases and 3,860,491 leaves, including 3,121,627 IEEE leaves. Coverage
includes all 13 original response fixtures, four independent-ownership fixtures,
four-separated, an actual safety correction, equal/adjacent tactical preferences,
interactions enabled/disabled, reversed rider and intent ordering, I and H complete
heats with two seeds in Dry/LightRain, an explicit interactions-OFF complete heat
in both rider orders, full ownership evidence, shadow physical
contact analysis and persistent overlap across retained history. The fixtures and
existing tests cover disjoint simultaneous contacts, real bridges, certified
episode separation, boundaries, track edges and fallback ownership. The complete
four-rider suite independently repeats/reverses 33 scenario/seed combinations;
existing degree 1/4 and Full/Lean tests retain their exact behavior checks.

Focused tests compare 10,000 capsule pairs and 2,592 metric-map inputs (six IEEE
fields on each of the public and prepared paths) against
independent pre-optimization arithmetic using IEEE equality, plus four fresh vs
reused full-history pruning fixtures. No expected physical value changed. Two
existing assertions changed only the documented redundant-call counts.

- Release solution build with warnings as errors: passed, zero warnings/errors.
- Complete local .NET suite: 2,055 passed, zero failures/skips (17 min 46 s).
- Focused geometry/interaction/contact suite: 261 passed.
- Complete Python calibration suite: 36 passed, including 12 new platform
  comparison tests. They reject new divergences in previously equal cases and
  already divergent cases, removed divergences, changed paths/types/bits (including
  signed zero and NaN payloads), missing/corrupt raw captures, and case coverage
  changes. A common per-OS behavior change fails even if the divergence map stays
  unchanged. Map coverage beyond the first 20 fields is tested explicitly.
- All existing trajectory, #55, #56A, #56B and #56C1 frozen checks: passed on CI;
  only the explicitly allowed redundant-work fields decrease. No golden was
  regenerated.
- Local Windows original-main exact capture: all 108 cases and reversed inputs
  pass. The prior 106-case CI audit passes on Windows and Ubuntu independently;
  final expanded-audit outcomes are recorded in the PR description. Raw sidecars
  for the investigated cases are byte-identical before/after within each OS.
- CI on production optimization commit `44a2d2f`: eight jobs passed, new exhaustive
  `compare-platforms` check failed for the pre-existing baseline divergence below.
  The four complete shards executed 1,405 core + 129 trajectory + 48 four-rider +
  473 historical-analysis cases, with no missing, extra, duplicate or skipped case.
  [Run 193](https://github.com/arturgladysz91/zuzel/actions/runs/37742878121).
- Exact final PR HEAD and all nine final-job outcomes are recorded in the
  [Draft PR description](https://github.com/arturgladysz91/zuzel/pull/61), after
  the review correction. Final-head acceptance requires all nine jobs green.

## Remaining bottlenecks and review status

The full scenario-I heat is 29.3% faster (1.41x) and allocates 9.5% less (25.53 MB saved per heat). The dense H squeeze improves 17.4% to 74.231 ms; the historical approximately 50 ms objective is not reached.

The 30% primary target is not reached by this safe optimization; it must not be represented as achieved.

Regression controls: four-separated +56.6% time reduction, I-heat-OFF -1.3% time reduction, solo-heat +0.1% time reduction. No control has a measured time increase above 5%.

Most dense-case cost remains exact adaptive common-time capsule search. Different
candidate projections have different physical paths, and the safety pass can
change executed requests. Those inputs cannot be merged merely because lanes or
apparent outcomes agree. The original bounded search, sampling coverage and
verification tolerances are therefore retained. A future change should first
profile repeated exact sample times within immutable intervals and allocation in
stitching/search buffers, then prove complete cache identity and arithmetic parity
before introducing reuse. This report does not claim that 30% is mathematically
impossible; it reports the safe changes measured here.

**Separate numerical portability issue: 22 pre-existing platform-divergent cases.**
The initial exhaustive comparison found 22 differing case manifests on original
main and the same 22 after optimization; the expanded 108-case run confirms this
case set. Both per-OS original-main comparisons pass every case. The original
failure came from demanding byte equality between optimized platforms despite
different original-main values. [Review #5453855563](https://github.com/arturgladysz91/zuzel/pull/61#pullrequestreview-5453855563)
corrects that acceptance condition to exact per-OS preservation plus complete
before/after divergence-map equality. The 22 cases are documented and tracked in
[issue #62](https://github.com/arturgladysz91/zuzel/issues/62); they are not an
allowlist of ignored differences.

For example, `C-cutback-clean/True/False`,
`root[0].Interaction.Episodes[0].Candidates[7].MinimumSeparationMeters`, is
`double:3FFF3C829310C52A` on Windows (1.9522729630228448) and
`double:3FFF3C829310C522` on Ubuntu (1.952272963022843), an eight-ULP difference.
There are nine differing unrounded leaves in that case and 77 in the inspected
I-heat capture; all 77 are inside interaction geometry/diagnostics. Existing
canonical #56B presentation and raw production-motion checks, #56C1 raw analysis,
trajectory IEEE checks and behavioral goldens pass. They did not inspect every
unrounded diagnostic field now captured by this additional check.

This is numerical drift, not a newline/serializer artifact. The exact source of
the original platform arithmetic difference has not been isolated; it must not be
attributed conclusively to libm, JIT or hardware from these observations alone.
The structured report retains the differing paths and raw bit patterns.

For a baseline field with Windows value W and Ubuntu value U where W != U,
optimized Windows must still equal W and optimized Ubuntu must still equal U.
The full divergence map records both values and rejects any change to that pair.
Resolving baseline numerical portability needs a separately reviewed numerical
contract; this performance PR preserves the original IEEE values on both
platforms. No rounding, tolerance, normalization, new platform golden, approximate
trigonometry or production physics change was introduced by this correction.
The PR remains **Draft pending independent full code review**, even after all nine
final-head CI jobs pass. No merge is authorized, and PR #59 remains untouched.
