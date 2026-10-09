# REA-004A — causal contact timing feasibility and initial-frontier prototype

**Full REA-004A correctness is not achieved. General causal replay remains disabled and unavailable.**
Starting main is `f40b2fd129124d969fd22cfe7767e2fb00bd651f`. The entire production
`src/CoreSim` tree remains `462d15a656f986bf01519c1a5dddaf8ff5f4649c`.
This PR adds an offline experiment, tests, observations and a scheduler design.
It adds no public heat option, production callback, alternate integrator or calibration.
A/B/C keep their accepted meanings and defaults. The experiment is PROVISIONAL.

## Measured endpoint contract

The current coordinator executes and verifies production movement before requesting
#56C1 analysis. The analyzer uses each pair's verified first-touch poses and
center-velocity bounds. The consequence resolver projects aggregate delta velocity
onto travel direction, adds it to the completed segment's scalar speed, and sets
terminal status or pending recovery. The final production resolution repeats the
original traversal and then applies that endpoint plan. Its already traversed
remainder, elapsed time, position and passage wear are retained.

The fresh 342-case matrix and the reruns of every C heat with an applied pair are
stored in [machine-readable evidence](rea004a-evidence/summary.json).
The summary separates the existing per-rider **frontier-to-endpoint** delay from
per-pair **first-touch-to-endpoint** records. A simultaneous frontier can contain
pairs whose individual touch times differ within the accepted 0.04 s window.
These definitions must not be combined into one distribution.

Each contact rerun retains scenario, seed, flags, step, exact first-touch pose/time,
normal, mass/impulse/momentum/demand/severity, current segment clocks, the segment
containing touch, canonical/lateral/scalar-speed node observation, heading,
detector center velocity, remaining distance/time, applied endpoint time (null for
a participant without an applied consequence), next active recovery duration,
and later verified/applied contacts. Non-applied solo-terminal participants are
not counted as receiving an impulse. Reversal and original final-state hashes are
checked. The raw node observation uses the existing #53 float-time sampler;
#55's event pose/time and chord velocity are separately retained at double time.
Neither representation is an execution cursor or exact rigid-body velocity state.

The fresh run completed **342/342** cases (114 each A/B/C), with 342 exact input-
order checks and three representative no-observer checks. All 342 final and behavior
hashes match the accepted merged REA-009 captures. There are zero new violations,
zero distance/clock residuals in 46,277 exact accumulation checks, and zero duplicate
applied pair generations. The run took 1,421.623 s on Windows 10.0.19045 / .NET
8.0.30 / 16 logical processors. No cached experimental D result was reused.

There are 32 applied pairs, 57 applied rider consequences and 26 applied frontiers.
The maximum observed C count is two frontiers per heat, three pairs per frontier
and four riders per frontier. The 57 per-rider frontier-to-endpoint delays are:

| Statistic | Delay (s) |
| --- | ---: |
| Minimum | 0.14789264505270694 |
| P10 | 0.39315116642465003 |
| P25 | 0.5508249797844655 |
| Median | 1.5013887049018138 |
| P75 | 1.7417995631694794 |
| P90 | 2.675366401672363 |
| Maximum | 3.222980499267578 |

The fresh detailed rerun covers all 22 C heats with applied pairs, plus 22 reversed
runs, with all final hashes matching the matrix. Its 64 pair-participant records
include one participant with no applied consequence; that participant's application
time and delay are null. Multiple pairs can share a rider consequence, so these 64
records are not a consequence count. Event segments include 43 straight, three
TurnEntry, ten TurnMiddle and eight TurnExit records. A measured closing speed up
to 5.65829530110147 m/s supplies strong-convergence evidence. Fresh geometry-only
controls separately retain side/oblique/head-on and separation/recontact inputs;
they are analyzer controls, not physical replay races.

The sampled delay range is about 0.148–3.223 seconds. This is a diagnostic sample,
not a bound on possible speedway contacts or evidence of ordinary crash rates.
Deliberate squeeze/connected/crash populations remain separate from ordinary heats.

## A real committed-history blocker

`control / seed 19 / C / heat 91 / step 8` verifies pair 1–2 at
**13.194772190598087 s**. Rider 1's retained segment-7 TurnExit motion runs from
11.950140953063965 to 13.278690338134766 s. At Resolve, rider 1 already has the
latter committed clock and canonical boundary 8. Touch lies inside that committed
motion, with roughly 0.083918 s / 1.729815 m of its already executed motion remaining.
Riders 3 and 4 also have committed clocks beyond touch. Rider 2's current request
begins at 13.187445 s. C applies rider 1's consequence at the next endpoint,
14.867663 s, roughly 1.672891 s after first touch.

`CausalBoundary.RequireCommonStart` rejects this actual snapshot with
`CommittedBeyondFirstTouch: 1,3,4`. Its regression asserts the real historical
interval, physical time ordering and unchanged input after refusal. The
`require-causal` command saves evidence and then exits unsuccessfully through that
same guard. A successful `witness` command means that the expected failure was
measured; it does not mean a D heat completed. Other fresh witnesses and their
clocks are recorded without rounding away the blocker. Across the 22 C reruns,
five applied pair-participant records use a previously committed touch interval:
control seeds 7/19/83 rider 1, rain-motoarena seed 7 rider 1, and low-grip seed 7
rider 2. Of the 26 frontiers, **11** have at least one world rider already committed
beyond touch (including uninvolved riders), **12** need an interior production
cursor, and only **three** have an exact common start available. Common-clock
availability alone does not certify scalar, surface or route replay feasibility.

The tracker retains immutable pose histories, not rewindable rider state, weather,
surface versions or integration cursors. Sampling history can locate a motorcycle
at touch; it cannot restore the rider's production state there. Rewriting its
current endpoint would also leave its already committed wear, subsequent motion,
classification and consumed generations inconsistent.

## Scheduler and commit redesign required

There is no authoritative global physical time today. `HeatSimulator` advances
logical lap/segment indexes; each rider accumulates its own float elapsed clock.
The coordinator uses pair request minima and retained history for common-time
geometry. Neither the minimum clock nor the global segment index is a commit
watermark. All riders currently Commit a whole segment together despite their
individual physical endpoint times.

A valid next phase must introduce an explicit event-time commit boundary:

1. Keep immutable per-rider production cursors, an authoritative common event
   clock, and a committed world watermark. A cursor may belong to a different
   segment from another rider's cursor.
2. Execute speculative production movement from the watermark with the original
   addressed decisions/incidents, route request and immutable surface provenance.
   Use #55 to certify coverage and locate the earliest eligible pair/frontier.
3. Establish every rider's state at that boundary before any participant can
   commit beyond it. Advance unaffected riders to the same boundary too; they can
   enter the next connected frontier after an impact.
4. Preserve the already executed prefix, freeze the complete event state, and
   compute all frontier impulses before any state mutation. Do not reuse later
   forecasts involving changed riders.
5. Resume original production integration from the post-impact state. Recompute
   #55 certificates and event ordering, and publish riders, actual wear and pair
   generations atomically only after final verification.

An alternative requires complete checkpoint/rollback/replay of **all** affected
riders, surface/weather, decisions, RNG addresses and ownership, including already
published observers/logs. No such mechanism exists. This PR selects a forward
watermark/cursor design for independent review, rather than introducing rollback.
The logical weather application must occur exactly once under an explicit reviewed
ordering policy; it cannot be replayed on every partial solve. This is a scheduler
provenance dependency, not permission to invent new Track Engine physics.

## Partial production execution contract

`ExecutedPathTraversal.Traverse` accepts a canonical distance horizon and internally
solves coupled time, lateral position, geometry, surface and longitudinal motion.
It has no public pause/resume token for an absolute event time. #53 sampling merely
interpolates stored endpoints. Restarting `ResolveRiderCore` at an interpolated
sample would rerun entry classification/incident logic, rebuild metre partitions,
possibly resample different surfaces, and lose current solve/envelope context.
Splitting a midpoint step and executing its shorter prefix can also disagree with
the original unsplit trajectory. A float sampled position is therefore insufficient.

The future cursor must retain validated integrator/node/subdivision state, exact
physical distance/time prefix, local canonical ownership, resolved controls,
entry/incident classification, reaction phase, envelope and immutable surface
version. First touch inside an integration step needs a reviewed dense execution
extension or certified re-execution of that step with common-time root refinement.
It must not call a linear node interpolant an executed prefix. No new acceleration,
cornering or grip formula is needed or permitted.

Partial passage contributions must accumulate provisionally and be normalized
once under the existing wear contract. The same metre cannot contribute twice.
Only successful complete traversal may use REA-009 `AdvanceToSegmentEnd`; partial
motion retains its actual canonical progress. Width reinterpretations remain
zero-time unswept transitions, with explicit entry/exit ownership. A contact crash
ends the cursor at the event; no pre-impact propulsion remainder may be committed.

## Tested initial-frontier prototype

`tools/rea004a-causal-audit/StartFrontierPrototype.cs` is an offline host using the
existing coordinator, analyzer, consequence resolver, production resolution,
geometry verification and Commit. Production never calls it. Its supported domain
is deliberately narrower than an intra-segment replay:

- A fresh rolling straight exposure, two to four active riders, positive speed,
  no incoming recovery or incidents, no standing reaction/history.
- Exactly one connected frontier at the **exact shared request start**. Every
  pair touches at that same time; the existing simultaneity window is unchanged.
- Certified longitudinal first touch with no meaningful penetration, lateral/yaw
  demand or requested lateral motion. Selected production requests must hold their
  fixed line. Stopped nonterminal continuations and uncertified coverage are refused.

C first supplies the actual post-safety contact certificate and selected requests
without committing. The prototype builds the accepted consequence plan on
zero-distance/time event states instead of C endpoints. All deltas are frozen
before constructing the post-impact snapshot. Existing production integration then
executes the remaining segment from that changed scalar speed and impairment.
Next-step recovery retains the accepted severity/loss/provenance mapping separately.
No second impulse is added at the endpoint.

The pre-contact prefix is empty and exact. Pre-/post-impact snapshots are explicit;
upright executed motions begin at the post-impact speed. The retained held-line
route has unchanged spatial shape but changed speed/time parameterization. This is
not a demonstration of lateral or interior replay. A crash at this boundary has
zero continuation distance/time/wear and remains at its actual event position.

The new motion is verified again with #55. Persistent observations of the same
consumed pair do not repeat an impulse. The existing continuous certified
separation/rearming model decides whether a later observation is a fresh event;
a fresh later contact throws `LaterContactRequiresRecomputedEventLoop`. No stale
pre-impact prediction is used to declare it resolved. Resolve changes no live
rider, wear, recovery, history or generation. The guarded result checks ownership
before Commit and rejects a stale or duplicate commit before mutation.

This is one initial exposure, not a full D race. There is no production
`EnableCausalImpactReplay` flag because the requested feasibility precondition
fails. Invoking the tool is an explicit experiment gate; unsupported input throws
instead of silently returning C. A certified no-contact exposure returns the
actual C resolution object bit-exactly. An unresolved contact without an applicable
plan is refused rather than mislabeled no-contact.

### C versus the supported experiment

Twelve named fixtures are captured in both orders: nine supported initial
exposures and three explicit refusals. Supported contacts comprise two-rider
Brush/Disturbed/LostRhythm/MajorSave/Crash starts, a three-rider start chain, a
four-rider start chain and a four-rider crash stress case. The ninth supported
exposure has no contact and returns the exact C object. Interior and near-end
rear contact refuse `PartialProductionCursorRequired`; side penetration refuses
`ScalarLongitudinalTouchOnly`.

For the moderate two-rider case, exact pre-impact speeds are 16 and 15 m/s;
post-impact speeds become 15.500058 and 15.499942 m/s at time zero. Their first
positive-time production nodes already differ from C. C endpoint times are
1.2823863 and 1.2226591 s; the executed D exposure produces 1.3247622 and
1.1823342 s from production integration. Both upright riders still cover their
actual remaining 20 and 17.9 m and reach the exact next canonical boundary.
C's endpoint application delays equal those C times; D's application delay is
zero because this contact is at the request start. No timestamp edit substitutes
for motion execution.

The two-rider crash fixture illustrates terminal ownership: C executes its
1.0762657 / 1.2226591 s traversal before endpoint crash; D stops both riders at
the common start with zero new time, distance or wear. These are deliberate crash
stress controls, not ordinary crash-frequency evidence. Three-/four-rider plans,
first affected nodes, final speed/time/distance, held-line motion, reverified
contact intervals, partial-exposure classification and committed pair states are
retained in `probe.json.gz`. Classification there is for one exposure, not a
completed heat. Fresh later contact after rearming remains unsupported.

## Simultaneity, scalar-state limits and bounded work

Existing #56C1 per-pair impulse/mass/momentum and RSS stability aggregation remain
authoritative. The prototype admits only exact common-time frontiers. Three-/four-
rider longitudinal chains retain action/reaction and frozen aggregate deltas;
collection/intent reversal produces identical typed leaves. The existing 0.04 s
window groups nearby pair onsets in C, but does not itself establish one physically
executed common-time state. General replay must resolve that distinction explicitly.

Lateral delta velocity, yaw demand and control loss are not interchangeable.
The current production state has scalar speed and a provisional reference attitude,
without persistent lateral/yaw dynamics. Side, oblique and head-on analyzer controls
remain useful detector/impulse evidence; they do not establish a replayable full
motorcycle state. The prototype rejects side penetration/state requirements and
never invents a displacement. Existing severity and recovery tests remain intact.

The matrix measures actual applied frontier counts before choosing any general
heat budget. These C counts describe endpoint trajectories; D can create different
later events, so they cannot justify a supposedly safe full replay cap. The tested
prototype bound is its supported domain: one initial frontier, the existing
four-rider limit (six possible pairs), one production remainder, and one complete
reverification. Dense four-rider and crash stress cases exercise that bound.
A later fresh event fails explicitly. No general heat budget is chosen or claimed
supported until cursor/recomputed-contact stress evidence exists.

## Recovery and REA-004B

The evidence separates immediate delta velocity, remaining-motion impairment and
one-next-active-segment recovery. The held/lifted prototype fixtures isolate the
impulse: drive is already lifted and voluntary lateral movement is held, so their
new loss coefficient adds no invented movement. Other production recovery tests
still verify positive-drive and voluntary-steering effects.

The prototype records receiver-only next-segment controls with/without the exact
pending recovery, including actual duration and consumption. Those detached probes
are not a traffic continuation of D. C contact reruns record actual next recovery
segments in full heats. REA-004B must define how impairment evolves on the shared
physical clock, preserve event provenance, distinguish remainder exposure from
later recovery, handle interruption/recontact/finish/crash, and obtain empirical
duration evidence. The 28 measured C next-recovery durations have P10 1.2944183 s,
P25 1.3802545 s, median 1.4592590 s, P75 1.6107271 s and P90 1.7803543 s. These
are physical durations of the existing next active segment, separate from the
endpoint delay distribution. This PR adds no arbitrary half-/one-/two-second duration.

## Verification, limitations and next phase

The focused suite proves shared-start motion effects, exact matched production
replay, single wear/commit ownership, no-contact C identity, terminal start crash,
three/four frozen frontiers, reversal, stale/duplicate refusal and the real
asynchronous blocker. Interior and near-end contact tests expect explicit cursor
refusal. Side-state tests expect explicit scalar-model refusal. Existing pair
separation/rearming, threshold, safety projection and decimal handoff suites remain
required; no expectation or frozen golden is removed.

CI captures the prototype and real C witness on Windows/Ubuntu and compares full
case coverage, missing/null/type/IEEE leaves and refusal reasons exactly. Existing
REA-001/REA-009/native-B maps remain unchanged. The full offline matrix is not
added to routine CI. Performance reports preserve #61 production counters and
compare isolated no-contact/contact workloads; no production path calls the new
experiment.

Local validation passed warnings-as-errors builds, the full then-current 2,492-
case .NET suite and the final focused 14-case suite (the latter adds two cases;
final discovery contains 2,494 distinct tests). Python calibration tests pass
78/78; the 39 frozen skill/style consumers pass; discovery assigns all 2,494
cases exactly once (core 1,578; trajectory 395; historical-analysis 473;
four-rider 48). The 26 bounded prototype/witness captures compare 25,146 typed
leaves exactly under reversal. The `require-causal` process exits nonzero through
the actual committed-time guard after saving its deterministic witness. CI on
the final PR head must independently confirm all shards and Windows/Ubuntu
captures; its strict comparison report is attached to the PR run artifacts.
No platform exception is added for REA-004A.

Proceed next with an independently reviewed watermark/cursor implementation and
its atomic wear/weather/ownership contract. General first-touch replay, interior
prefix correctness, later events, asynchronous horizons and lateral/yaw evolution
remain unfinished. **Do not enable D, claim contact timing fixed, begin recovery
calibration as a substitute, or merge without independent review.**

## Performance

The unchanged REA-009 benchmark is built against an archived `f40b2fd` CoreSim
and against this PR's CoreSim. Each workload has at least five warmups/two seconds,
nine samples, normal decision parallelism four, and a separate synchronous
counter run. No concurrent heavy work ran during timing. Median full-heat/workload
measurements are:

| Workload | Main (ms) | Head (ms) | Change |
| --- | ---: | ---: | ---: |
| balanced-motoarena | 400.3408 | 400.2184 | -0.03% |
| contact-heavy | 311.0625 | 303.9259 | -2.29% |
| three-squeeze | 244.9042 | 243.2732 | -0.67% |
| three-squeeze-binary | 245.1319 | 244.2323 | -0.37% |
| fixed-line | 3.0429 | 2.9850 | -1.90% |
| rich-trajectory | 10.9422 | 10.8618 | -0.73% |
| lean-cached-trajectory | 9.4239 | 9.4447 | +0.22% |
| decimal-single-segment | 0.0442 | 0.0440 | -0.45% |

All production work counters match exactly; allocations are equal or differ by
at most 128 bytes in median. Production source is identical, so the small timing
variation is observation noise, not a claimed optimization. No ordinary workload
has a slowdown above 10%. #61 candidate/cache counters remain intact.

The additional isolated experiment uses at least eight paired warmups/two seconds,
nine interleaved samples per mode, ten resolves per sample, and process-wide
allocation measurements. Commit/serialization are outside this timing scope.
These are initial exposures, not full D heats:

| Exposure | C (ms) | D initial only (ms) | C / D allocated bytes |
| --- | ---: | ---: | ---: |
| No contact | 0.15815 | 0.16443 | 60,652 / 62,644 |
| Two-rider frontier | 7.23109 | 7.55380 | 2,256,256 / 2,720,054 |
| Four-rider frontier | 39.13658 | 40.68017 | 7,282,916 / 8,443,836 |
| Start crash | 6.01357 | 6.04077 | 2,244,200 / 2,279,682 |

No-contact C/D make two physical rider resolutions, zero partial traversals,
zero replay calls and the same 236 coordinator narrow-phase evaluations. The
wrapper adds a result/ownership guard; production never pays that wrapper cost.
Two-rider replay adds two physical rider resolutions and 70 narrow-phase checks;
four-rider replay adds four resolutions and 1,407 checks (1,033 adaptive
subdivisions). Both use one initial frontier and one replay call; all partial
traversal counts are zero. All-crash invokes the production replay API once with
zero active riders, adds no physical propulsion resolutions and no post-impact
narrow phase. First-touch root/subdivision work and full coordinator work records
are preserved in `performance.json`; certificates are not skipped for timing.

## Reproduction

Run from the repository root with .NET 8 and Python 3. Use fresh output directories.

```sh
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
dotnet test tests/CoreSim.Tests -c Release --no-build
python -m unittest discover -s tests/calibration -p 'test_*.py'
python tools/rider-compatibility-audit/check-consumers.py
python tools/ci/check-shards.py results/rea004a-discovery

dotnet build tools/race-engine-readiness -c Release --warnaserror
dotnet tools/race-engine-readiness/bin/Release/net8.0/CoreSim.Tests.dll batch results/rea004a-baseline f40b2fd129124d969fd22cfe7767e2fb00bd651f

dotnet build tools/rea004a-causal-audit -c Release --warnaserror
dotnet tools/rea004a-causal-audit/bin/Release/net8.0/CoreSim.Tests.dll probe results/rea004a/probe
dotnet tools/rea004a-causal-audit/bin/Release/net8.0/CoreSim.Tests.dll witness results/rea004a/witness
dotnet tools/rea004a-causal-audit/bin/Release/net8.0/CoreSim.Tests.dll contacts results/rea004a/contacts results/rea004a-baseline
python tools/rea004a-causal-audit/compare.py results/rea004a
python tools/rea004a-causal-audit/summarize.py results/rea004a-baseline results/rea004a/contacts results/rea004a/probe results/rea004a/summary.json
# Expected nonzero exit after saving the actual historical-span evidence:
dotnet tools/rea004a-causal-audit/bin/Release/net8.0/CoreSim.Tests.dll require-causal results/rea004a/expected-failure
# Run with no concurrent heavy work:
dotnet tools/rea004a-causal-audit/bin/Release/net8.0/CoreSim.Tests.dll performance results/rea004a
```

The full matrix can also run on this PR's head: its production tree is identical
to the named main baseline. Captures retain source identity; no D evidence is resumed
or taken from a cached experiment. Existing geometry-only controlled fixtures in
the baseline runner remain explicitly separate from real production heat records.

The production benchmark before/after binaries use the unchanged benchmark host:

```sh
git archive f40b2fd129124d969fd22cfe7767e2fb00bd651f src/CoreSim -o ../rea004a-main-source.zip
# Extract that ZIP into ../rea004a-main-source using the platform's ZIP utility.
dotnet build tools/rea009-handoff-audit -c Release --warnaserror -p:CoreSimRoot="<absolute path to ../rea004a-main-source>" -o ../rea004a-perf-before-bin
dotnet build tools/rea009-handoff-audit -c Release --warnaserror -o ../rea004a-perf-after-bin
dotnet ../rea004a-perf-before-bin/CoreSim.Tests.dll benchmark results/rea004a/performance-before.json 9
dotnet ../rea004a-perf-after-bin/CoreSim.Tests.dll benchmark results/rea004a/performance-after.json 9
python tools/rea004a-causal-audit/compare.py results/rea004a --report results/rea004a/local-comparison.json
python tools/rea004a-causal-audit/package.py results/rea004a-baseline results/rea004a docs/calibration/rea004a-evidence
```

`files.json` records every packaged file's size/SHA256. `cases.json.gz` contains
fresh 342-case observations using the existing readiness `compact()` function;
complete near-exposure intervals remain reproducible through the raw batch.
`baseline-manifest.json` identifies each scenario/seed/configuration, environment
and source. Contact captures contain exact typed flags; prototype captures retain
seed 19 and their full exact feature flags. All unsupported records are explicitly
uncommitted. `expected-failure.json.gz` is successful evidence collection followed
by a failed requested D boundary, not a completed experimental race.
