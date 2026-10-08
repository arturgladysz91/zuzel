# Pair-level physical contact generations

Review `5452627332`, same-pair recontact finding; baseline
`fe3f8c6794a24bb9bf4d18ed0c2e46acda847521`. PR #59 remains Draft.
The first deterministic geometry correction is retained. The separate sequential
strong-to-weak recovery finding remains **OPEN**; its resolver and tests are unchanged.

## Previous failure and ownership

An episode-lifetime `ConsumedPhysicalPairs` set suppressed every subsequent A-B
impact until the entire episode closed. A-B could physically separate while B-C
kept the connected battle alive; a genuine later A-B impact then disappeared.
Two new fixtures were run against the reviewed baseline before implementation.
Both failed at the second-contact eligibility assertion after certified separation.
The short-clearance, repeated-history and continuing-parent assertions passed.

The heat-owned tracker now has one immutable state per canonical unordered
`(minRiderId,maxRiderId)` pair. Four riders require at most six states. Episodes
continue to own tactical commitments and legacy fallback origins. Merging,
expanding or closing an episode does not alter physical pair ownership. There
is one causal state for each pair, so merging episodes cannot choose an older
copy, union incompatible clocks, or invent a generation.

| State | Transition |
| --- | --- |
| Never applied | First eligible analyzed contact consumes generation 0. |
| Consumed | Ongoing overlap, ambiguity, insufficient clearance or missing coverage cannot apply again. |
| Separation in progress | Consecutive certified intervals retain a common-time start and observation watermark. |
| Rearmed | The complete release interval increments generation once and records its release time. |
| Consumed again | A later eligible frontier applies that generation once and begins a new post-impact clock. |

`LastImpactSeconds`, `ObservedUntilSeconds`, `ClearSinceSeconds` and
`ArmedAtSeconds` make the transitions explicit. Old contact times cannot acquire
a newly armed generation. Queries do not mutate ownership. Duplicate consumption
is rejected. Records are immutable; Clone copies the dictionary, and Commit
publishes the staged states without aliasing the live pair dictionary.

## Certified release and causal frontiers

The unchanged release constants are **0.65 m** and **0.45 s**. Release requires
continuous positive-duration #55 common-time intervals whose conservative
`MinimumSeparationLowerBoundMeters` is strictly greater than 0.65 m. Every
covering interval must be numerically resolved, nonambiguous and free of touch.
There is no sampled-distance shortcut, float rounding or numerical tolerance.

The observer partitions verified rows and explicit frame gaps by their time
boundaries. Gaps, boundary ambiguity, unresolved geometry, overlap or an
insufficient lower bound reset incomplete progress. Duplicate and overlapping
historical observations cannot advance the watermark twice; conflicting
overlapping certificates fail closed. Time before the consumed impact is
excluded. Asynchronous rider clocks advance only over their actual shared
coverage, never to the unrelated heat clock or a forecast deadline.

Eligibility reuses the final selected production verification after the existing
single safety pass. A pair can use only certificates preceding its frozen
causal-frontier start. It cannot rearm inside that frontier. The existing #56C1
frontier classifier still runs before applicability filtering: simultaneous A-B
and B-C aggregate together, and a consumed early overlap cannot promote a
deferred later trajectory. The consequence layer still changes only the
existing endpoint speed/status; already-traversed paths are not reintegrated.
After materialization, the complete executed report updates release progress.
Safety verification, executed verification and replayed historical rows share
the same watermark and consumption guard.

The release observer adds no geometry solve or production projection; newly
admitted contacts use the existing consequence-materialization replay. The observer
groups an existing report once and sweeps its interval boundaries. Pair state
is bounded independently of retained #55 pose history; that history's existing
pruning and discontinuity quarantine remain unchanged.

Finished, crashed and retired riders produce no new eligible production contact.
Their pair records remain bounded heat ownership records; removing a participant
does not reset its generation or release another pair. The remaining riders can
continue their episode. If fewer than two active riders remain, the existing
episode closure applies. A new heat owns a new tracker. No process-global state
or implicit rider resurrection is added. Leaving a tactical episode alone is
not a physical clearance certificate.

## Regression evidence

`PhysicalContactPairGenerationTests` proves lifecycle fields, rather than only
totals: initial consumption, short separation, repeated observations, complete
release, second consumption, pure eligibility and clone/commit isolation.
Four-rider tests release the two pairs independently before and after a bridge,
including reversed rider-ID assignment. Unknown geometry, exact-threshold lower
bounds, boundary ambiguity, explicit gaps, asynchronous coverage and a real #55
discontinuity cannot complete an incomplete release clock. Inactive-rider tests
retain ownership while the remaining battle continues.

Controlled production tests use the existing two-disjoint-contact fixture with
rider A entering at 22 m/s and rider B at lateral position 0.9, producing a
nonzero recoverable impulse without changing any physics coefficient. They
commit its first impacts, supply a separately verified common-time separation
interval, and execute later frozen production contacts. Short separation gives
zero new impacts; complete separation gives exactly one new A-B impact while
the other consumed pair stays suppressed. Abandoned Resolve cannot consume it;
Commit consumes generation 1, and repeated overlap adds no third impact. These
controlled snapshots are an ownership integration fixture, not a claim that the
synthetic path between snapshots is a natural full-heat trajectory.

The existing simultaneous-frontier, aggregate momentum/demand, consumed-contact
deferral, continuous-overlap, physical ID relabeling, recovery and historical
golden tests remain in place. One old direct release fixture now explicitly
clears its synthetic first-touch field and uses a later frontier timestamp;
a row still claiming a touch is not a valid separation certificate.

The complete enabled capture now includes 12 production recontact traces
(seeds 7/19/83, Dry/LightRain, both input orders), including intermediate lifecycle
states, changes, diagnostics, motions, events, analysis and plans. The same
harness runs against the reviewed baseline and this correction. Windows/Ubuntu
comparison remains exact over every typed leaf and IEEE bit. All older strict
cross-platform golden comparators remain unchanged, as does the original-main
OFF audit and its preserved numerical portability divergence map.

## Validation and performance

The Release solution build passes with warnings treated as errors. The local
full-suite run passed 2,132 tests before the last additional boundary/production
regressions; all 26 final pair-generation tests pass, and all 40 Python tests pass.
The final targeted geometry/avoidance/analysis/consequence run passes all 269 tests.
Final discovery contains **2,142 tests exactly once**: core 1,492, trajectory 129,
historical-analysis 473 and four-rider 48. Final-head CI executes all four shards
and the strict Windows/Ubuntu audits; the PR body records its exact SHA and run.

The fresh local enabled audit contains **742 cases / 6,958,262 typed leaves /
5,297,822 IEEE leaves** with exact reversed-input equality. All 730 previous
captures are byte/typed-bit unchanged against the accepted `fe3f8c6` baseline,
including every retained full-heat classification, event, consequence and raw
surface value. **No existing race output or golden is regenerated.**

Only the 12 newly introduced ownership traces differ: **7,224 changed leaves**,
including the newly observable internal lifecycle. The complete case/path/type/
IEEE before/after map is retained in
[physical-contact-pair-generation-changes.json.gz](physical-contact-pair-generation-changes.json.gz).
Each trace changes its second-contact application from zero pairs to one A-B
pair in generation 1; C-D remains consumed in generation 0. The new impact has
nonzero impulse and uses the unchanged Brush/Disturbed mapping. For seed 19 /
Dry, the second contact changes rider 1's endpoint speed from float bits
`41AF71C9` to `41AEC2A5`, and rider 2's from `41A7678F` to `41A816B3`.
Rider 2 receives the existing one-step Disturbed recovery with control-loss
double bits `3FA7AC411AA82DA0`; this is a newly admitted impact, not a recovery
formula change.

The original-main OFF audit passes all **108 cases / 3,874,319 typed leaves /
3,121,627 IEEE leaves**, including exact reversed-input equality. CI regenerates
both original-main and candidate captures independently on Windows and Ubuntu.
Its complete pre-existing 22-case / 1,088-field platform divergence map must stay
unchanged; [numerical portability issue #62](https://github.com/arturgladysz91/zuzel/issues/62)
remains separate. All strict enabled cross-platform goldens are retained.

Same-machine Release measurements use the unchanged six-workload protocol: at
least eight complete warmups and three seconds per workload, then nine measured
runs per version. The benchmark ran without concurrent tests/captures. Windows
10.0.19045, .NET 8.0.26, x64, six processors; the complete raw wall/CPU/allocation/
GC/work samples and environment are retained in
[physical-contact-pair-generation-performance.json](physical-contact-pair-generation-performance.json).

| Workload | Before median ms | After median ms | Wall change | Allocation change |
| --- | ---: | ---: | ---: | ---: |
| Four separated | 0.5842 | 0.5886 | +0.75% | +0.10% |
| H three-rider squeeze | 79.6140 | 79.5538 | -0.08% | +1.32% |
| Real four-rider bridge | 393.5714 | 395.0376 | +0.37% | +0.34% |
| Scenario I full heat, ON | 6347.4208 | 6507.0628 | +2.52% | +1.32% |
| Scenario I full heat, OFF | 3905.2675 | 4074.7954 | +4.34% | -0.01% |
| Dense-contact full heat, ON | 477.3321 | 505.1995 | +5.84% | +0.83% |

All measured work counters are identical across all 18 samples per workload.
The largest wall increase is 5.84%; none exceeds the 10% investigation threshold.
CPU medians change by 0% / 0% / 0% / +1.74% / +2.96% / -3.33%, respectively;
the smallest workload is below the process CPU clock's resolution. These are
observational timings, including host variation, rather than physics tuning.
