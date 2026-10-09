# Race Engine — realism and readiness audit

Production baseline: `c13af7b8a16fe4e58e9d404c849cdf323649b0ed` (merged #59 and #61).
This PR observes and prioritizes. It changes no production source, defaults,
constants, decisions, random draws, frozen golden or PGEE input.

## Decision and scope

**REQUIRES SPECIFIC FIXES** before enabling physical consequences by default.
An ordinary outside-grip standing-start heat aborts at step 0 in B and C because
an infeasible safety projection's bounded coupled solve escapes to HeatSimulator
(REA-001). Valid partial positions also abort the next canonical segment handoff in
all three configurations (REA-009). After those separate correctness fixes, severity,
endpoint impact timing and recovery still require
further calibration/evidence. The geometry, ownership and recovery contracts have substantial exact
technical regression evidence. The severity/reserve scale and recovery duration
have synthetic design evidence, not measured speedway impact validation. This is
an integration candidate for controlled evaluation, not a declaration that the
Race Engine is empirically complete. Neither feature flag is enabled by this PR.

The final matrix processed **342 cases: 322 completed heats and 20 explicit failures**.
The two failure paths reproduce with reversed identities/gates held fixed and without
an observer. The 322 completed heats have no additional recorded invariant witness,
43,685 exact clock/distance checks have zero residuals, and no applied pair generation
is duplicated. Green CI validates the audit and existing contracts; it does not turn
these failed races into successful races.

| Readiness question | Evidence-based answer |
| --- | --- |
| Starts | Production reaction/acceleration profiles and gate-dependent first-bend orders work. Numeric reaction/2 s speeds lack compatible PGEE validation; canonical launch migration is unfinished. |
| Corners and lines | Executed entry/apex/exit paths preserve physical distance/time; wider paths can retain more speed at a distance cost. Full-footprint/local geometry certification remains incomplete. |
| Passing and close racing | Verified common-clock straight, inside-entry and outside-after-bend lead transitions, repeated pair battles and re-passes occur. A tactical intention or proximity alone is never counted as success. |
| Collisions and evasion | B/C avoid predicted conflicts; C has verified first-touch impulse, pair ownership and one-next-step recovery. Severity scale and endpoint application timing remain provisional. |
| Attributes | Actual consumers are distinguished. Speed/control effects are visible; some quiet-heat canonical sweeps have no measurable result change, with no arbitrary strength/age speed bonus. |
| Empirical speed/time/gaps | Matched-venue N and P10/P25/P50/P75/P90 are reported separately. Low peak-speed and fast heat-time distributions need compatible cohort/geometry evidence, not immediate constant fitting. |
| Missing racing behavior | Longer-horizon opponent strategy, causal maneuver attribution, canonical launch migration and empirical impact/venue evidence remain unfinished or unverified. |
| Gameplay integration | Suitable for controlled integration experiments after the two P1 fixes. Not ready to declare the engine complete or enable physical consequences by default. |

The five leading review priorities are REA-001 (escaping safety projection), REA-009
(canonical handoff), REA-002 (coarse default consequences), REA-003 (matched cohort
calibration), and REA-004 (endpoint timing/severity/recovery evidence). REA-006 defines
the remaining canonical gameplay consumer contract. None is implemented in this PR.

The audit uses `HeatSimulator → CaptureSnapshot → Decide → Resolve → Commit`.
`CalibrationTraceCollector` and a composite passive observer retain immutable
resolved observations. Post-hoc #55 geometry consumes the executed nodes; it never
feeds a decision, contact owner, force, surface update or classification. No second
simulator or replacement physics equation exists in the harness.

## Architecture actually exercised

Read together: [architecture](../../architecture.md), [binding specification](../../core-simulation-spec.md),
[calibration rules](../../calibration.md), [gameplay status](../../gameplay/README.md),
[historical baseline](../current-model-baseline.md), [matched venue](../motoarena-matched-venue.md),
[trajectory evaluation](../trajectory-intent-evaluation.md), [responses](../contested-space-racing-response.md),
[contact consequences](../physical-contact-consequences.md). Earlier statements that
contested space/diagonal integration are absent describe older commits. The contact
document's Draft/review wording records its writing time; #59 is merged at this
audit baseline. Current code and the precedence notes in README/spec/gameplay govern
the active implementation; historical numerical tables are not current measurements.

| Status | Production mechanism / practical limit |
| --- | --- |
| Active | Explicit gates; addressed deterministic RNG; simultaneous snapshot decisions; four-lap orchestration; distance/time-controlled longitudinal launch/drive and coupled lateral path integration; continuous logical-corner envelope; physical passage wear, weather and incidents; final classification. |
| Active | AdaptiveDecisionModel evaluates production corner horizons and surface perception, line/style cost and current occupancy. It has no fixed overtaking reward. Legacy contact eligibility/consequences remain the default. |
| Feature-gated B/C | Heat-owned contested-space coordinator: bounded joint requests, #55 safety verification, CoverInside/CutInside/ContinueOutside/YieldOutward/Hold/BackOut/EmergencyAvoid responses, competitive episodes, retained actual motion, release hysteresis and safety rerun. #61 local projection/pair caches remain intact. |
| Feature-gated C | Verified causal contact → impulse/reserve/severity → simultaneous consequence → one next active step of reduced positive drive/lateral authority. Pair generation survives episode merging; consumed recovery is replaced only by fresh impact. Legacy consequence application is disabled in C. |
| Observation only | ResolvedRiderMotion, calibration traces, mechanical capsule pose adapters, diagnostic severity analysis, audit summaries and PGEE evaluator. Capsule/reference attitude geometry is a constrained game-space model. |
| Provisional | Drive/launch force and resistance calibration; envelope/reference radius interpretation; standing-start split; symmetric venue width approximation; lateral motion controls; tactical margins/reach/windows; reserve factors/severity boundaries/recovery map. Passing tests do not empirically validate these parameters. |
| Specified / later work | Canonical Reaction/Start are not independent launch consumers; longitudinal physics uses legacy skills. Body contact, injuries, long-term fatigue and match/manager gameplay integration remain outside this implementation. No direct age multiplier exists. |

## Three separate configurations

| Configuration | Contested responses | Physical consequences | Other options |
| --- | --- | --- | --- |
| A | false | false | Actual `new HeatSimulationOptions()` defaults; only seed/weather set per scenario. Four laps, incident frequency 1, logging true, Summary interaction diagnostics, physical diagnostics None. |
| B | true | false | Same defaults; existing authorized legacy fallback may follow an unresolved eligible physical episode. |
| C | true | true | Same defaults; physical application replaces the legacy consequence model. Enabled contact decision/clearance geometry also uses the reviewed deterministic arithmetic. |

Public `JsonIgnore` fields are explicitly captured, including the physical flag,
parameters, condition, recovery and canonical profiles. Calibration adjustment
selectors remain null. Thus a B/C difference with no applied impact can arise from
the enabled arithmetic/safety path; it must not automatically be attributed to impulse.
Named FullAudit reruns record their diagnostic settings and prove exact final-state
parity with the corresponding Summary run.

## Coverage, cost and reproduction

See [reproduction commands](../../../tools/race-engine-readiness/README.md),
[numerical results](observed-results.md), [machine-readable summary](race-engine-readiness.json),
[per-case evidence](cases.json.gz), and [typed corner/pass trace index](trace-evidence.json).
The expanded batch is offline. CI runs only the six-case smoke, on each OS against
both original main and the audit head. Original strict cross-platform goldens remain
unchanged; the existing 22 native-platform cases and issue #62 remain a separate
numerical portability concern.

The final pilot took 62.4514043 seconds for six cases including reversed and
unobserved reruns. Production heats took 0.5319296–5.0980539 seconds on Windows
10, .NET 8.0.26, six logical processors. Capture/logging are included in heat timing;
post-hoc geometry, hashing and serialization are excluded. This is a cold/warm
observational sample, not a speedup benchmark or CI timing assertion.

Batch coverage: 27 four-rider scenarios × seeds 7/19/83 × A/B/C, plus 11 one-variable
sweeps × three values × seed 19 × A/B/C = **342 cases, 60 scenario/settings, 114 per
configuration**. Each is reversed with identity/gates held fixed. Both existing
standing-start example and Motoarena geometry are used. Dry, light rain, inside/outside
grip gradients, low grip and production-prepared worn surface are covered. Rolling
A/B/C/D/E/H fixtures, three-rider squeeze, four-rider close reattack and contact-heavy
stress remain separate populations. The full batch has an explicit 90-minute limit.
The pilot preceded expansion. Offline wall time was 1960.8812953 seconds, including
the persisted 315-case first batch and the 27-case supplement. The supplement added
the bend-entry catch and distinct binary-fraction squeeze/reattack controls; all
original failing decimal cases remain present. All 342 reversals and 23 offline
observer-free checks agree exactly. Six pilot observer-free checks and 16 named
FullAudit reruns also agree; each named Summary rerun matches its batch typed hashes.
Nine compressed trace/diagnostic files preserve representative suspicious sequences.
The 35,045,384-byte raw gzip is reproducible offline; the committed compact cases
retain flags, inputs, physics, incidents, pair/generation/recovery, orders, work and
exposure summaries without duplicating full incident profiles.

## Starts and first bend

The launch observer records reaction, movement onset, production acceleration
profile, speed at one/two seconds, time to 70 km/h, and actual common-boundary entry/
exit arrival groups. IDs 1/2/3/4 occupy gates A/B/C/D; reversal does not reassign them.
The controlled Start sweep distinguishes the deterministic reaction/force effect
from subsequent interaction results. Compensation by a faster distance rider is
tested separately, rather than treated as a monotonic Start requirement.

Pilot Motoarena, seed 19, A, rider 1: reaction `0.24000001 s`, observed speed at 1 s
`7.44003 m/s`, at 2 s `17.019146 m/s`, launch-profile time to 70 km/h `2.2745256 s`,
first movement duration `2.714497 s`. The profile's speed-at-two-seconds is
`17.019148 m/s`: that is a distinct production profile observation from the stored
motion's interpolation; both are retained, not rounded into agreement. Existing
literature observations are context only. PGEE speed_2s/curve_speed are rankings,
and supply no individual numeric launch or corner speed target.

## Corners and verified racing behavior

Per-segment observations retain entry/exit/minimum/maximum speed, physical distance,
time, lateral range, curvature range, route-sampled grip, node-based acceleration
range, phase context, correction, exit drive and explicit state/boundary transitions.
Named complete typed traces supply apex brackets and suspicious sequences. Wider
paths can retain higher speed through existing radius/grip constraints while paying
more physical distance. Coupled diagonal/polar path cost is already implemented;
historical claims to the contrary do not describe this baseline.

Order evidence uses a common heat clock and unwrapped canonical race progress,
not physical cumulative distance, equal segment-step snapshots or rider-ID ties.
A strict signed lead reversal on continuous executed-node pieces yields a timestamp
**bracket**. Zero-time progress changes, missing coverage and terminal absence reset
the comparison. Start-phase transitions, racing transitions, final classification and
crash/retirement effects remain separate. Counts include repeated reference-point
lead reversals in an ongoing side-by-side fight; they do not imply a clear-bike pass,
held position or success of the tactical intention. No exact pass timestamp is invented.

Exposure duration is an upper bracket of native #55 intervals containing a verified
near sample, not exact time below the threshold. Alongside uses the existing rotated
footprint relation at that sample. Attempted responses are not counted as passes.
FullAudit candidate rejection reasons are available for selected diagnostic reruns;
Summary telemetry does not expose every rejected alternative in every heat.

In `bend-catch/19/C`, the faster outside follower completes the pass after the bend,
on segment 3, during `[4.384176716208458, 4.42447005212307] s`. Its physical outward
offset at the end of that common-clock bracket is `4.735764980316162 m`, versus the
leader's `1 m`. Thus outside passing after a bend is demonstrated. The probes do
not establish that every outside attack should pass within the bend: the wider route
pays more distance and its corner traversal can take longer despite higher speed.
`rolling-E` provides blocked-inner/defensive-occupation opportunity but does not prove
a successful forced-widening defense. A causal undercut forcing an opponent wide is
unverified; selected CutInside/YieldOutward alternatives alone are insufficient.

A failed `WithinTrack` certificate is not itself a proved illegal trajectory. The
pilot Motoarena has three uncertified motion footprints per configuration and zero
reference-point edge witnesses. Boundary width reinterpretations are retained as
zero-time unswept transitions. Their empirical/legal-footprint adequacy needs separate
evidence; this audit does not turn a certification failure into an asserted P1 bug.

## Ability consumers and interpretation

| Input | Actual production consumer | Interpretation / outcomes not promised |
| --- | --- | --- |
| Legacy Start | Reaction and standing launch force | Reaction is monotonic by construction; final position may change through traffic. Canonical Reaction/Start currently do not replace it. |
| Legacy Speed | Longitudinal drive and corner envelope | Not a purely straight-only attribute; one-variable heat time need not be monotonic through incidents. |
| Legacy SlideControl | Corner envelope/correction/risk, legacy contact severity; fallback maps canonical Technique | Canonical Technique sweep holds legacy control fixed to isolate the new consumer. |
| Legacy TrackReading | Adaptive surface perception error; correction path | Better information does not inject grip, engine force or a guaranteed winning line. |
| Legacy PairRiding | Legacy contact occurrence; canonical compatibility data | Canonical PairRiding is not opponent contact superiority in B/C. No arbitrary speed bonus. |
| Legacy Adaptability | Existing correction/control calculation | No added engine power or new canonical rating. |
| Technique | B/C execution safety margin; C contact stability reserve | Not a new launch/straight force bonus. |
| Strength | C stability reserve | No pre-contact longitudinal speed bonus. |
| Physical mass | C impulse/reduced mass | Longitudinal drive still uses the provisional nominal reference system mass; not a complete mass-dependent motorcycle model. |
| Condition | B/C safety margin; C reserve | One-heat state input; this stage does not implement long-term fatigue. |
| Legacy risk style | Adaptive risk/surface cost | Does not map to Combativeness; canonical contest willingness remains a separate input. |

Sweeps alter only rider 1, retaining its physical gate and other inputs. The full
heat sweeps are conditional seed-19 experiments. Existing fixed-geometry contact
physiology fixtures independently check reserve/mass responses without interpreting
stress frequency as an ordinary race crash rate. Real rider names are never assigned
fabricated game ratings; Skill50 is not the professional population mean.

In the C seed-19 sweep, Speed 20/50/80 gives rider-1 heat times
`58.14207 / 55.897003 / 53.955265 s`; SlideControl gives
`56.90022 / 55.897003 / 54.988434 s`. Start reaction is monotonic, while whole-heat
time is not. Technique, Strength, mass, Condition and PairRiding have identical
rider-1 totals in this quiet cohort, with no applied rider-1 contact. Fixed-contact
physiology fixtures exercise their actual consumers separately. These results cannot
establish population effect size or justify making every attribute a lap-time bonus.

## PGEE comparison rules

Use unchanged `data/calibration/pge/v1` and `RealWorldCalibrationEvaluator`, reporting
N and P10/P25/P50/P75/P90 component by component. Exact Motoarena selector:
`season == 2026 && source_track_label == "Motoarena im. Mariana Rosego"`; no Toruń
alias is inferred. Whole-dataset results remain context. Finished simulated rider
records and complete four-rider heat spreads have separate denominators. The
simulation sample spans controlled surfaces/abilities and has no calibrated mapping
to the professional population; a percentile discrepancy is a hypothesis to test,
not proof of a faulty force constant.

| Comparability | Metrics |
| --- | --- |
| Directly comparable definitions | Maximum observed speed, path-distance/time average, L1 minus median L2–L4, within-heat differences. Populations/selection still differ. |
| Conditional numeric comparison | Motoarena heat/lap times and finishing spreads under provisional matched geometry, standing-start split and an eventual calibrated cohort. |
| Contextual evidence | All-venue aggregate absolute times; published historical record; literature start observations; current neutral/synthetic cohort versus professional distributions. |
| Unsupported source | Individual numeric 2 s speed, reaction, curve speed, impact impulse/severity/recovery and racing-line shape from PGEE rank flags. |

The symmetric 16.6 m turn width derives from published 17.0/16.2 m widths; the
31 m radius convention and 35/27 m standing split remain provisional. Banking and
local asymmetry are absent. Synthetic example absolute times are not Motoarena
targets. No dataset is downloaded, no constants are fitted, and no realism percentage
is produced.

Full numerical tables retain all requested quantiles and denominators. Motoarena
real clean records number 407; real four-attempt heat spread records number 93.
Simulated finished rider counts are A/B/C `68/67/66`; complete four-finish heat
counts are `14/16/16`. Failed heats are excluded. First-lap records and four-lap
penalties have their own counts; a first lap may precede a later retirement.
Own-median rider-relative offsets are contextual comparisons, not skill estimates.

## Contact timing and workload limits

C ordinary standing cases apply 21 pairs and 41 rider consequences: 10 Brush,
8 Disturbed, 12 LostRhythm and 11 Crash. Twelve riders crash among 176 riders in
44 completed heats; one is a normal segment incident. A/B record seven/one crashed
riders in 45/44 completed heats. These deliberately varied cases are not estimates
of a league crash rate. Stress C is reported separately: six crashed riders,
nine pairs, and three frontiers with at least three riders. Six ordinary standing
repeated-overlap suppressions occur without duplicated applied pair generations.

The accepted #59 contract computes impulse from first touch and changes normal
endpoint speed without reintegrating the intervening remainder. In `overall/19/C`,
step 27, rider 3's touch is `41.068336266334654 s`, application endpoint
`42.579429626464844 s`, speed `18.603743 → 19.074354 m/s`. This is a documented
endpoint approximation, not a newly alleged correctness breach. Ordinary standing
C application-delay median is `1.3084196189548152 s` (N=41); next active recovery
segment median is `1.4399585723876953 s` (N=20). These timing scales need realism
review alongside severity, not just an impulse conservation test.

Complete-heat execution/allocations, candidate projection, production verification,
repeated production resolution and narrow-phase totals are in the numerical report.
Failed captures are excluded from complete-heat timing distributions and have separate
cost records. Capture/logging and worker allocations are measured; native post-hoc
exposure/hashing/serialization are separate. No production cache, search bound or
physics constant changes, and no #61 speedup claim is inferred from this machine.

## Correctness and next work

**P1 reproduced: REA-009.** Valid partial `.1f` positions in the original
three-rider squeeze/four-rider reattack fixtures abort Adaptive's next production
horizon with `Rider 1 is not in segment 1.` across A/B/C and seeds 7/19/83.
The first partial segment's float remainder is `float:3F666666`; adding it to
`float:3DCCCCCD` yields double canonical progress `0.9999999776482582`, so the
next projected segment and rider disagree. The existing 1e-9 boundary normalization
does not cover this 2.2351741790771484e-8 residual. This is a separate canonical
handoff defect; no inputs, tolerance or production arithmetic are altered here.
Distinct `.125f`/`.375f` companion scenarios investigate the racing maneuver while
the original failed decimal scenarios stay visible in the matrix.

```sh
dotnet run --project tools/race-engine-readiness -c Release -- repro results/partial-position c13af7b8a16fe4e58e9d404c849cdf323649b0ed three-squeeze 19 A
```

**P1 reproduced: REA-001.** `outside`, seed 7, Motoarena, gates A/B/C/D,
legacy/canonical compatibility Skill50, dry, outside-improving grip/ruts gradient,
incident frequency 1, heat ID 91. Both B and C throw
`CoreSim.ExecutedPathTraversal+TimeSolveDiscontinuityException` with
`Coupled time solve did not converge within its bound.` No segment has committed:
all riders are still at tape release. The failure propagates through
`ContestedSpaceInteractionCoordinator.SafetyProject` while pre-projecting safety
alternatives (`ResolveProduction → ExecutedPathTraversal.Solve`). A failed optional
projection currently aborts the entire otherwise valid race. Forward/reversed
pre-failure states and failure type/message match exactly; unobserved reproduction
is also checked. This is an existing production blocker, not repaired in the audit.

```sh
dotnet run --project tools/race-engine-readiness -c Release -- repro results/blocker-B c13af7b8a16fe4e58e9d404c849cdf323649b0ed outside 7 B
dotnet run --project tools/race-engine-readiness -c Release -- repro results/blocker-C c13af7b8a16fe4e58e9d404c849cdf323649b0ed outside 7 C
```

The reproduction command captures a **failed** heat as diagnostic evidence; a zero
tool exit means capture completion, never successful race completion. The smoke
command rejects any failed heat. Expanded batch results preserve failure records,
exclude failed heats from completed-rider/PGEE denominators and continue the matrix.

The summary contains invariant witnesses, exact float accumulation checks and
applied pair-generation checks. Frozen constructors enforce nonnegative/monotone
motion; the audit additionally records speeds, edge witnesses, finish-time order,
recovery replacement, exact reverse/observer parity and a reused-simulator stress
test. A single-precision source value is reconstructed from its shortest JSON
literal before checking the existing float accumulation contract; no tolerance is
used to suppress differences. Full typed hashes retain the original IEEE bits.

Findings and independent-review priorities are recorded in the machine-readable
summary and [backlog](race-engine-backlog.md). No backlog item is implemented here.
Recommended next PR, subject to independent audit review:
**“Race Engine — reject infeasible safety projections without aborting heats.”**
Keep the #61 cache/bounds, classify infeasible optional requests explicitly, preserve
the verified frozen execution when appropriate, and handle any genuine execution
failure through a reviewed invariant-preserving contract. Do not raise solve limits,
loosen geometry, add tolerances or fit speed constants to conceal this reproduction.
