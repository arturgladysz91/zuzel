# #56B — contested-space racing responses

**BINDING architecture; PROVISIONAL synthetic balance.** Base is merged #56A,
`a1609e485131f553619c386a051a158796c9e2dd`. The public heat option
`EnableContestedSpaceResponses` defaults to false. No physics calibration, legal
bike dimension, force curve, mass, corner envelope, launch, wear or existing crash
constant changes in this stage.

## Production ownership

`Decide` still creates each independent #54 Entry/Apex/Exit request from one immutable
snapshot. The decision now carries that request as JSON-ignored metadata. During
`Resolve`, a detached `ContestedSpaceInteractionCoordinator` observes actual and
projected `ResolvedRiderMotion` poses through #55. It builds common-time threat edges
and connected clusters, evaluates simultaneous alternatives through rich production
replay, then resolves the selected requests with the actual incident options.

`Commit` alone applies rider/surface changes and the detached episode tracker. A new
tracker belongs to each `SimulateHeat` call; it rejects another heat identity. Concurrent
heats share no commitments, caches, history or episode RNG. The compatibility
`Simulate` path retains legacy physics.

#55 is the sole mechanical overlap authority. Tactical clearance does not inflate
its 2.10 m chassis or 0.80 m handlebar reference footprint. BoundaryAmbiguous intervals
never produce interaction pressure or contact eligibility. Unknown numerical intervals
and frame coverage gaps cannot certify a safe alternative. Completed history is normally
pruned after two seconds; the causal origin of an unresolved #55 boundary quarantine
remains until physical separation, so pruning cannot promote a jump into a contact.
One float heat-clock ULP of adapter overlap is trimmed from the preceding continuous
interval only. No state jump is swept or interpolated.

## Bounded selection

A cluster has at most four riders and six edges. Edges connect by common heat time,
including topology boundaries. Each rider has at most three alternatives: original
request, one local competitive/committed response, and one lateral hold plus positive-drive
lift. Their Cartesian product has at most 81 combinations per cluster. The implementation
uses a first competitive/safety pass, actual verification with real incident options,
and one joint safety-only pass if an eligible actual mechanical conflict remains or
appears. Total search is bounded to two passes, at most 81 combinations each. Work totals sum across clusters without hiding
additional work. No trajectory search spans arbitrary N riders.

Rider projections cache only identical rider/intent/control/hold tuples inside this
immutable evaluation. Each unique tuple is projected once per immutable evaluation, including failed
projections. At most 12 alternatives and 54 pair checks are needed for a four-rider
evaluation. A fresh immutable safety evaluation has its own bounded cache. Compact
pair compatibility uses the exact #55 algorithm once per alternative pair; joint
scoring reads those results. Normal gameplay defaults to Summary, keeping selected
responses, episode state, context, separation, passes and fallback without retaining
all candidate arrays. None hides summaries; FullAudit is explicit in the calibration
generator. Pair reports cache identical projection pairs. Cold-cache tests
require equal selected responses, physical results and episode diagnostics; work counts
alone differ. Geometry edges and clusters use canonical ordering. Safe tactical ties keep
more original requests, then use the new addressed `InteractionTacticalTie` channel.
Rider ids supply addresses and serialization order, never fixed physical priority.

The hard filter rejects #55 mechanical overlap, uncertified intervals, incomplete/crashed
production horizons and footprints leaving physical usable track bounds. The edge check
uses actual capsules, a conservative curved-edge allowance and rate-certified recursive
subdivision (maximum depth 12); it fails closed when it cannot certify clearance.
Only after those filters are satisfied does selection compare bounded execution-margin
shortfall, production traversal time, anchor deviation and tactical preferences. Differences
below #55 minimum-separation precision do not decide a tactical priority.

`CoverInside` requires a recognized closing threat before established inside footprint
overlap. Existing overlap removes that alternative even at Defense 99. An outside rider
may keep/yield according to available geometry. `CutInside` changes an exit request;
`ContinueOutside` changes a line request. Their radius, distance, grip exposure,
correction, speed and time come from production replay. There are no passing bonuses.

`BackOut`/`EmergencyAvoid` request `RiderDriveControl.LiftThrottle` and zero voluntary
lateral delta. The existing signed midpoint primitive receives nominal positive reference
force times the request fraction (zero for lift); resistance, gearing, correction and
nominal system mass remain unchanged. It never multiplies rider speed or adds elapsed
time directly. Full-drive equilibrium remains the nominal diagnostic, not a lift target.
Forced production RunWide retains its original movement. `InteractionProjectionControl` applies LiftThrottle/lateral hold only to replay
prefix zero, the current production step. Every future prefix uses normal controls;
the trajectory commitment can continue. The next step must explicitly reissue the
temporary control. The regression spans the next turn phase and following straight,
compares each future motion with independently executed normal production and requires
exact current-step output equality. The control expires with its request. A lifted but still unresolved attempt is permitted only without worse separation,
new mechanical pairs, edge violation or an incomplete horizon; otherwise the original
requests remain with explicit unresolved diagnostics. Neither path declares uncertain
geometry safe or adds a new crash model.

## Episodes and legacy handoff

An episode stores rider set, common start/last-active time, context, commitments,
predicted mechanical conflict, clearance clock and legacy-attempt ownership. Meaningful
threats refresh it. The initial forecast deadline is retained for ordinary clearance, but never advances
the observed clock into the future. A sustained actual interval with certified local
separation lower bounds above CompetitiveReachMeters can close stale ownership before
that forecast deadline. #55 may certify such a far bound without resolving the minimum
to its tighter presentation precision. Boundary ambiguity, gaps and lower bounds within
competitive reach still block release. Episodes also end when fewer than two participants
remain active. Later forecasts do not continually move the release deadline. Clearance
is integrated over the final executed common-time intervals, including any authorized
legacy consequence, rather than over independent requests or a single segment-boundary
sample. Every participating active pair needs continuous coverage and a #55 separation
lower bound above the release threshold for the release delay. Missing/ambiguous coverage
is not evidence of release. A later cleanly separated battle can create a new episode.
Merged episodes retain prior fallback ownership.

### Independent episode ownership fix

The temporary `SafetyEvaluationScope` contains all active riders for one global
simultaneous safety search. It is never registered with the episode tracker.
Eligible actual #55 contacts are canonicalized and passed through the existing
common-time `Clusters()` graph before the search and again after the selected
requests are executed. Two disconnected pairs retain separate identities,
commitments, diagnostics, final separations and release clocks. Every active pair,
including all four cross-group pairs, is still verified. Safety choice prefers
preserving an unrelated rider's frozen request whenever a valid joint response
can do so. Work records one search and at most 81 combinations; each participating
episode records pass 2 without duplicating the global work counter.

Only certified connected geometry can call tracker reconciliation. A verified
B–C bridge can join active A–B and C–D battles even when each prior episode shares
only one rider with that edge. The oldest episode (then lowest ID) survives;
membership, commitments, response history, forecast deadlines and originating
fallback provenance are retained. A disconnected optimization group is rejected.
An established episode is not split by a sparse subsequent observation; the
existing certified-clearance and hysteresis model still governs release.

**Merged fallback policy:** each originating continuous battle retains exactly
one allowance. Merging mints no new allowance and resets none. Existing local
contacts receive their own unused origin first; only then may a newly verified
bridge spend an unused adjacent origin. An already handled canonical pair cannot
be rerolled through another origin. Thus a genuine merge can carry more than one
historically independent allowance, while an ordinary continuous episode still
has one. Every final eligible contact is recorded with its authorization or an
explicit exhausted-allowance/safety-not-evaluated reason. Ambiguous geometry never
creates an authorization. Diagnostics expose source episode IDs and the actual
pair/time that consumed each allowance; storm auditing checks attempts per origin.

The dedicated `IndependentOwnership` JSON evidence covers two unresolved pairs,
one pair cleared by pass 2, prior independent fallback history, a real bridge,
a bridge with prior fallback, unrelated riders and independent production-observed
release. The report also retains all 13 original fixtures and the 76-heat batch.

Each pair uses its own earliest current request time. An unrelated rider's earlier clock
cannot reopen that pair's committed past. At asynchronous boundaries, threat geometry
samples the opponent's retained executed motion when its next request begins later; the
newest continuous interval owns an exact shared endpoint. No pose is fabricated to fill
missing coverage. Focused regressions cover both this clock ordering and release before
a forecast contested window, including a sampled minimum whose local lower bound cannot
certify clearance.

Within an unchanged context, a feasible joint commitment remains selected unless emergency
avoidance is required. The report distinguishes response changes/ABA oscillations from
changes where the previous entire joint commitment has become infeasible. A diagnostic
warning identifies a feasible joint commitment changed without context/emergency cause.

Pass 2 freezes all actual verified requests/motions before selecting changes jointly.
It re-executes the same pre-step production snapshot, never advances a rider twice.
KeepIntent is allowed only when jointly safe; BackOut and EmergencyAvoid may use an
already-present hold/yield alternative. No cover, cutback or outside tactical optimization
is reopened. All active riders are checked together so separate corrections cannot create
a new pair conflict. Technique/Condition and hard geometry govern this pass.

Concrete regression: B-close-too-late with seed 276 and incident frequency 2 has a feasible
pass-1 projected choice, then an eligible conflict under actual production incident options.
Pass 2 selects EmergencyAvoid/BackOut and clears it: PassCount 2, resolved true, fallback
false. The imminent-overlap fixture remains in mechanical contact after pass 2 and authorizes
one legacy attempt; the episode persistence regression proves that the attempt cannot repeat.

Only after pass-2 actual-path verification, an eligible unresolved mechanical episode authorizes
exactly one attempt in the visibly separate legacy consequence routine, even if its
occurrence roll has no effect. The coordinator supplies that physical pair to the legacy
candidate boundary, preserving its original addressed occurrence/severity formulas and
consequences. Other contacts for that owned episode are filtered out. Clear pre-contact
responses cannot independently receive fabricated legacy side-by-side consequences.
#56B adds no consequence calculation; existing production incidents and the authorized
legacy fallback can still produce their existing events.

## Canonical inputs and provisional controls

Typed InteractionSkillDomain separates Offensive, Defensive, Safety and Neutral.
Attack improves CutInside and attacking ContinueOutside/Hold preference. Defense improves
CoverInside, defensive YieldOutward and defending ContinueOutside/Hold. Roles come from
longitudinal/inside-outside relation, closing speed and established footprint overlap,
never RiderId. BackOut/EmergencyAvoid use neither Attack nor Defense as superiority terms.
Profiles Attack 90/Defense 20 and Attack 20/Defense 90 prove cover, overlap/yield and cutback
ownership separately; swapping them leaves the hard emergency result unchanged.
Competitive preference is `-0.09 * quality - 0.09 * Combativeness`, with quality
supplied by Attack/99 or Defense/99 according to the maneuver's domain. The bounded
quality term remains present at Combativeness zero; the maximum combined preference
is the former 0.18 scale. Safety-domain alternatives receive no skill or contest
superiority term. Combativeness also affects conservative planning margin.
All remain below hard safety filters; Combativeness 1 still attempts EmergencyAvoid.
Technique and Condition change a small planning margin only. PreferredLine is a small tie
preference. They never change power, Vmax, grip, bike dimensions or the production execution
skills. Strength, MassKg and canonical PairRiding have zero pre-contact effects. Legacy
PairRiding remains only in the unchanged legacy compatibility consequence formula.

| Parameter | Default | Unit / purpose |
|---|---:|---|
| Pressure clearance | 0.25 | metres, tight-space relevance |
| Alongside engagement | 0.60 | metres, physically alongside competition |
| Release clearance | 0.65 | metres, greater than engagement |
| Competitive reach | 2.50 | metres, broad tactical reach |
| Minimum meaningful closing speed | 0.25 | m/s, avoid stable distant parallel contests |
| Cluster time window | 0.30 | seconds, connected simultaneous threat grouping |
| Release delay | 0.45 | seconds of certified clearance |
| Emergency horizon | 0.35 | seconds to predicted physical touch |

The execution margin is `pressure * (1.15 - 0.30*(Technique-1)/98) +
0.04*(1-Condition)` metres. Technique 20/50/80 and Condition 1/0.7/0.4 are crossed
in nine production fixtures. Nine pressure/window combinations span 0.15/0.25/0.35 m
and 0.15/0.30/0.45 s. These values are synthetic game-balance controls, not measured
speedway parameters. New fixture tracks use matching 60 m logical straights (home
halves 30+30), 24 m reference radius and 14 m widths, so lap-wrap geometry closes.
Historical track fixtures are unchanged.

## Evidence and reproduction

The accompanying [deterministic JSON](contested-space-racing-response.json) contains
13 controlled scenarios, nine clearance/window sensitivities, nine Technique/Condition
combinations, 44 existing four-rider scenario heats (11 cases × seeds 7/19 × dry/light rain)
and 32 canonical-archetype heats. Technical attacker, aggressive mediocre attacker,
strong defender and cautious defender are contrasted without promising an archetype winner.
Outside-useful and outside-poor cases measure ordinary production traversal; separated
riders need no interaction episode. Inside cover, late defense, cutback, exit crossing,
a four-rider first bend and boxed longitudinal yield have explicit focused assertions.

Each heat reports first-bend and ordinary-racing statistics separately, episode/context
counts, average cluster size, response selections, final clear/unresolved episodes,
legacy attempts, joint/pass/production/narrow-phase work, overtakes/order snapshots,
maximum episodes per pair, active duration, response changes, ABA oscillations and
unexplained commitment changes. Phase observations describe their phase; final episode
counts describe the last observation and are not independent collision rolls.
Storm warnings flag ≥12 episodes for one pair, ≥20 seconds continuously active,
≥12 response changes, repeated legacy attempts or an unexplained feasible commitment
change. These are evidence-review alarms, not runtime percentages or crash probabilities.
The supplied batch must have no warnings. The requested 70–90% synthetic clear-response
range is observation guidance only and is never encoded in the engine.

### Recorded batch after review fixes

The 76 complete sampled heats produce 262 episodes: 236
pre-contact resolved, 26 unresolved, and 0 legacy attempts.
Maximum episodes per pair is 9; maximum duration is
8.9550539970 s; maximum response changes is 5.
There are 20 ABA changes, zero unexplained commitment changes and zero
storm warnings. The prior reviewed batch had 229 episodes, 208 resolved, 21 unresolved,
3 attempts and a 9.5660539985 s maximum. No count or percentage is preserved artificially.
Resolved means certified actual current-step clearance; future steps reissue controls.
The dedicated two-disjoint-unresolved fixture authorizes two distinct source episodes;
one-disjoint-clears authorizes only C–D. Prior A–B fallback cannot suppress C–D.

| Dedicated evidence | Episodes | Memberships | Actual contact components before → after safety | Global searches | Authorized attempts | Genuine merges | Unexpected merges |
|---|---:|---|---|---:|---:|---:|---:|
| two-disjoint-unresolved | 2 | 1,2; 3,4 | 2 → 2 | 1 | 2 | 0 | 0 |
| one-disjoint-clears | 2 | 1,2; 3,4 | 2 → 1 | 1 | 1 | 0 | 0 |
| real-bridge | 1 | 1,2,3,4 | 1 → 1 | 1 | 2 | 1 | 0 |
| unrelated-riders | 1 | 1,2 | 1 → 1 | 1 | 1 | 0 | 0 |
| one-previously-attempted | 2 | 1,2; 3,4 | 2 → 2 | 1 | 1 | 0 | 0 |
| bridge-with-previously-attempted | 1 | 1,2,3,4 | 1 → 1 | 1 | 1 | 1 | 0 |

Production-observed independent release closes A–B at 0.45 s while C–D stays active.

### Top ten longest episodes

Separation min/max below summarizes the per-resolution final #55 minimum, not a sampled
continuous path. Reach/coverage columns distinguish observed proximity from a certified
continuous claim. Missing or ambiguous coverage remains unknown and cannot close ownership.
The old future-clock bug and conservative far-bound release caused stale ownership;
regressions now require closure after 0.45 s of actual certified far separation. Remaining
long episodes include changing contexts/merged participants or uncertified intervals, so
they cannot all be described as one continuously certified side-by-side pair battle.

| Fixture / episode | Riders | Start → end (s) | Contexts seen | Separation min…max (m), observations | Changes | Continuous reach / coverage certified | Merged riders |
|---|---|---|---|---|---:|---|---|
| I / seed 19 / Dry / legacy-fallback / #2 | 1,2,3,4 | 6.666 → 15.621 | StraightReattack, CornerEntryClosing, MidCornerPressure, CornerExitCross | 0.120…0.654, 7 | 0 | No / No | No |
| I / seed 19 / Dry / technical-attacker / #2 | 1,2,3,4 | 6.666 → 15.621 | StraightReattack, CornerEntryClosing, MidCornerPressure, CornerExitCross | 0.120…0.654, 7 | 0 | No / No | No |
| I / seed 19 / Dry / aggressive-mediocre / #2 | 1,2,3,4 | 6.666 → 15.621 | StraightReattack, CornerEntryClosing, MidCornerPressure, CornerExitCross | 0.120…0.654, 7 | 0 | No / No | No |
| I / seed 19 / Dry / strong-defender / #2 | 1,2,3,4 | 6.666 → 15.621 | StraightReattack, CornerEntryClosing, MidCornerPressure, CornerExitCross | 0.120…0.654, 7 | 0 | No / No | No |
| I / seed 19 / Dry / cautious-defender / #2 | 1,2,3,4 | 6.666 → 15.621 | StraightReattack, CornerEntryClosing, MidCornerPressure, CornerExitCross | 0.120…0.654, 7 | 0 | No / No | No |
| I / seed 19 / LightRain / legacy-fallback / #2 | 1,2,3,4 | 6.665 → 15.617 | StraightReattack, CornerEntryClosing, MidCornerPressure, CornerExitCross | 0.127…0.662, 7 | 0 | No / No | No |
| I / seed 19 / LightRain / technical-attacker / #2 | 1,2,3,4 | 6.665 → 15.617 | StraightReattack, CornerEntryClosing, MidCornerPressure, CornerExitCross | 0.127…0.662, 7 | 0 | No / No | No |
| I / seed 19 / LightRain / aggressive-mediocre / #2 | 1,2,3,4 | 6.665 → 15.617 | StraightReattack, CornerEntryClosing, MidCornerPressure, CornerExitCross | 0.127…0.662, 7 | 0 | No / No | No |
| I / seed 19 / LightRain / strong-defender / #2 | 1,2,3,4 | 6.665 → 15.617 | StraightReattack, CornerEntryClosing, MidCornerPressure, CornerExitCross | 0.127…0.662, 7 | 0 | No / No | No |
| I / seed 19 / LightRain / cautious-defender / #2 | 1,2,3,4 | 6.665 → 15.617 | StraightReattack, CornerEntryClosing, MidCornerPressure, CornerExitCross | 0.127…0.662, 7 | 0 | No / No | No |

### Performance before / after

Fresh same-machine comparison: Windows 10.0.19045, 6 logical
processors exposed to this process, .NET 8.0.26, Release. Exact reviewed
HEAD `c4f8d3a` and the corrected engine use the unchanged harness and Summary diagnostics.
One warmup followed by five resolutions or three complete four-rider/four-lap scenario-I
seed-7 heats; medians. The earlier 76.07 ms / 6.4 MB / 2.61 s recording used 16 exposed
processors and .NET 8.0.30; the fresh measurements below provide the comparable baseline.

| Case | Reviewed wall | Corrected wall | Reviewed allocations | Corrected allocations |
|---|---:|---:|---:|---:|
| K-far-apart OFF | 0.6803 ms | 0.7252 ms | 68,648 B | 68,648 B |
| K-far-apart ON | 5.1800 ms | 5.1000 ms | 236,640 B | 236,680 B |
| H-three-squeeze OFF | 0.9029 ms | 0.9320 ms | 93,464 B | 93,464 B |
| H-three-squeeze ON | 107.0673 ms | 115.3875 ms | 6,283,000 B | 6,361,568 B |
| Full heat OFF | 408.0048 ms | 405.8549 ms | 149,652,536 B | 149,652,272 B |
| Full heat ON | 4574.0316 ms | 4818.1609 ms | 301,024,496 B | 267,254,296 B |

Dense allocation is 6,361,568 B, below 10 MB. Fresh dense wall time
changes by 7.8%; full-heat ON changes by 5.3%. The preferred 50 ms target remains unmet.
All six active pairs, cached alternatives and cached pair compatibility remain verified;
there is no CI wall-clock gate. Full-heat allocation falls 11.2%.

| Current case | Episodes | Unique alternatives | Pair checks | Joint combinations | Production resolutions | Actual verifications | Safety passes | Legacy attempts |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Full heat OFF | 0 | 0 | 0 | 0 | 36 | 0 | 0 | 0 |
| Full heat ON | 9 | 165 | 427 | 333 | 704 | 73 | 1 | 0 |

Full heat OFF CPU median: 1484.375 ms; descriptive 100/1000-heat
CPU projections: 148.438/1484.375 seconds.

Full heat ON CPU median: 5843.750 ms; descriptive 100/1000-heat
CPU projections: 584.375/5843.750 seconds.

```sh
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
dotnet test tests/CoreSim.Tests -c Release --no-build
python tools/rider-compatibility-audit/check-consumers.py
python tools/ci/check-shards.py results/discovery
python -m unittest discover -s tests/calibration -p 'test_*.py'
dotnet run --project tools/contested-space-audit -c Release -- results/contested-space
dotnet run --project src/Sandbox -c Release -- contested-space-response-report results/contested-space
dotnet run --project src/Sandbox -c Release -- contested-space-response-performance results/performance
# Combine reviewed/current raw captures with separately captured resolution Work counters:
dotnet run --project tools/contested-space-audit -c Release -- --resolution-work counters.json
python tools/contested-space-audit/compose-performance.py before.json after.json counters.json docs/calibration/contested-space-response-performance.json
```

The independent [performance capture](contested-space-response-performance.json) uses
one warmup and five measured resolutions or three measured full heats; medians and allocations are observational,
with no CI wall-clock gate. Feature OFF, feature ON separated, and feature ON a dense
three-rider cluster are measured separately. The quiet ON path still observes #55 geometry
and executes ordinary production without an unowned legacy contact authorization, but generates no alternative combinations.
Production-resolution counters include baseline, projected prefixes, actual verification
and the final legacy-filtered resolution.

CI retains all existing shard responsibility, Python and frozen #54/#55/#56A gates and
unchanged 30-minute job timeouts. The new report uses #55's ten-decimal presentation
precision for double geometry values only. A separate complete typed IEEE capture of
actual changes, diagnostics, motions and events is compared byte for byte between Windows
and Ubuntu, alongside exact report bytes. Historical Decision source hashes remain their
original values after removing only reviewed exact metadata blocks under a pinned extraction
fixture; the original 39-reader legacy manifest is unchanged.

#56B adds bounded pre-contact racing decisions and physically re-evaluated trajectory responses for contested space. Most battles can now be resolved by covering, yielding, continuing outside, cutting back or lifting throttle before mechanical impact. PR #56B does not model impact severity, body contact, strength/mass collision response, recovery or new crash consequences. Those belong to #56C.
