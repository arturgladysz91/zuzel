# REA-004A7 — real Straight admission and an explicit world-version blocker

Status: **bounded experimental admission; real causal pair NOT executed**.
Based on Draft #75 / `229685c2aeaf2c0d79ec826efa1640e8b5a82ac0`.
The task's stop condition applies. No new contact time, event-state pair,
consequence plan or certified continuation is claimed for `control/19/C/heat91`.
`13.194772190598087 s` remains the original diagnostic reference, not a new result.

## Safe changes

`BeginRiderIntegration` admits moving Straight with its original incident options.
`ResolveRandomIncident` returns the original Straight resolution before occurrence
or severity sampling. The only other frequency use in that method is the corner
probability multiplier. The production resolver, both addressed RNG channels,
classification and `Solve/Evaluate` equations are unchanged. Corner admission and
incident behavior retain their existing guards. Resume retains the classified
session rather than calling entry resolution again.

The existing two-rider helper accepts two captured single-rider snapshots. It
preserves independent decision step addresses, segment contexts and surface
identities. Before forecasting or constructing an event, it checks track/heat,
seed/laps, rider/context agreement and **identical frozen surface provenance**.
Equal-valued detached surfaces also refuse. Different origins are never collapsed
into one surface by copying cells. After that guard, asynchronous start clocks
explicitly refuse until retained-prefix/initial-cursor support exists. The
existing same-world, same-start A6 experiment continues through unchanged #55 and
#56C1/#56C2 logic. This is an admission boundary, not asynchronous execution.

## Captured real case and why execution stops

The regression runs the existing production `control` catalog scenario with
seed 19, heat 91, C options and `IncidentFrequency=1`, stopping its observer at
step 8 before Commit. It captures both original immutable snapshots and the real
Adaptive decisions. Selected response diagnostics verify that both decisions
remain `KeepIntent` with no substituted drive, hold or lateral target.

| Captured input | Rider 1 | Rider 2 |
| --- | --- | --- |
| Segment / decision step | 7 / 7 | 8 / 8 |
| Segment type | moving TurnExit | moving Straight |
| Segment start, heat seconds | 11.950141 | 13.187445 |
| Entry speed, m/s | 18.779615 | 22.255537 |
| Entry lateral position | 0 | 1 |
| Actual trajectory intent | E2-A2-X2 | E0-A0-X0 |
| Decision risk | 0.054125015 | 0.052062515 |

The snapshot clocks above are the original floats; they are not equalized. The
test preserves profiles, setup, condition, recovery, options and complete step
contexts directly from production, and verifies the actual Straight executed path.

Rider 1's accepted 24-step TurnExit prefix is retained at exactly
`13.16668989157769 s`: local progress `0.9145583394510511`, executed distance
`23.83666032552719 m`, speed `20.514252 m/s`, lateral `0.5503331`.
The original 25th full step ends at `13.21535040820075 s`, beyond the diagnostic
touch. This prefix is neither resimulated nor shifted to rider 2's start.

**The two origins are different physical world versions, not merely different
snapshot allocations.** All 45 surface cells differ. Dry weather still has a
drying rate of `0.015` per topological simulation tick: moisture changes from
`0.3800001` to `0.36500013`. On segment 8 / lane 1, effective grip changes from
`0.98349994` to `0.99174994`. Segment 7 additionally includes the complete
passage wear of all four riders; for example lane 1 changes from Grip/Ruts `1/0`
to `0.9901641/0.039343327`.

The regression reconstructs the entire step-8 surface exactly by committing the
original full step 7 on detached state and applying the original step-8 weather
tick. Rider 1's original committed segment end, `13.27869 s`, is already after the contact.
Therefore that surface includes contributions from his old unresolved future.
Neither freezing the older version for both riders nor accepting the newer one
for rider 1 preserves both original executions. Cell equality or local segment
independence cannot repair this: weather changes the actual grip used by both
integrations, and no physical-time weather/wear ledger currently exists.

The pair entry refuses **before any forecast, time trial, impulse, surface copy,
event materialization or live publication**, identically in both input orders.
Both original snapshots and the retained prefix remain unchanged; the prefix
token remains usable on its own original surface. This independently exercised
original production replay is not publication through the unresolved contact.

There is a second temporal limitation: the helper initially accepts one numerical
step per rider. Rider 2's first Straight step ends at `13.232303824958386 s`,
after the reference touch only about 7.3 ms after his segment starts. An initial cursor before his
first accepted step is needed. Once world chronology is established, #55 must
certify only actually executed common coverage starting at `13.187445 s`;
missing rider-2 pre-segment history must not be synthesized. This PR leaves that
asynchronous execution unavailable rather than demonstrating a modified fixture.

## Smallest next architectural step

Create one experimental pair-window transaction **before the old full step-7
commit**. Retain addressed controls and accepted integrator prefixes with their
world provenance. Give the later rider a classified zero-step cursor, retaining
the earlier rider's existing numerical prefix. Define a physical-time ordering
for the existing weather ticks and account for only causally accepted passage
samples once in a detached surface ledger. Its common surface frontier must
exclude old full-segment suffix wear; do not invent a partial wear formula or
reuse already published whole-segment deltas. Atomically certify/publish rider
prefixes, surface, log and contact ownership only through a resolved event.

This needs an explicit surface/publication contract; the original independent
step snapshots cannot establish it alone. General four-rider scheduling remains
later work: retain all four trajectories, find every earliest unresolved pair,
share the surface/weather frontier and publish all accepted contributions once.
The current result proves neither a causal pair nor an entire synchronized heat.

## Validation and cost

The real Straight's continuous and twice-paused execution are exactly equal for
all resolved data and counters, including its 36 accepted steps / **142 coupled
equation evaluations**. Applying either complete resolution to detached state
produces identical riders, passage wear and logs. Original-frequency and
frequency-zero Straight resolutions are also exact, confirming the no-incident
path without disabling incidents in the captured case.

The rejected real pair costs **0 contact-time trials / 0 coupled evaluations**
at admission (capture and independent cursor parity are separate costs).
A6's successful simplified case cost 25 trials / 3116 evaluations; zero work on
refusal is not an efficiency comparison with successful contact execution.
Existing counters and the solver are unchanged, with no per-trial surface copies
or retained-prefix replay added.

Tests cover real inputs, exact world evolution, continuous/resume parity,
single complete wear publication, preserved step addresses, different-world and
missing-time refusal, input-order independence and the existing unsafe-geometry
barriers. Normal A/B/C has no cursor caller and its production arithmetic remains
unchanged. An exact A7 inverse restores #75 before every older source extraction;
all 39 historical consumers and all earlier extraction fixtures stay frozen.
Only targeted tests, one final Release solution build and the standard Draft
Windows CI are required; no historical full audit, 342-case matrix, Ubuntu job
or broad benchmark is introduced.

Local validation: **167/167 targeted C# cases** and **12/12 A3–A7 extraction
tests** pass. The retained A6 demonstration still reports exactly 25 time trials
and 3116 coupled evaluations. The final solution Release build and standard
Draft Windows CI validate the published source separately.
