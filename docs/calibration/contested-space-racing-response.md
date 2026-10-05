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
uses one exhaustive response pass containing both competitive and safety choices, followed
by actual production verification; it does not perform a second redundant search.
The public maximum remains two passes. Work totals sum across clusters without hiding
additional work. No trajectory search spans arbitrary N riders.

Rider projections cache only identical rider/intent/control/hold tuples inside this
immutable evaluation. Pair reports cache identical projection pairs. Cold-cache tests
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
Forced production RunWide retains its original movement. The control expires with its
request. A lifted but still unresolved attempt is permitted only without worse separation,
new mechanical pairs, edge violation or an incomplete horizon; otherwise the original
requests remain with explicit unresolved diagnostics. Neither path declares uncertain
geometry safe or adds a new crash model.

## Episodes and legacy handoff

An episode stores rider set, common start/last-active time, context, commitments,
predicted mechanical conflict, clearance clock and legacy-attempt ownership. Meaningful
threats refresh it; observing a retained episode without a fresh threat does not reset its
release clock. Release requires certified separation above the release threshold for the
release delay. Missing/ambiguous coverage is not evidence of release. A later cleanly
separated battle can create a new episode. Merged episodes retain prior fallback ownership.

Within an unchanged context, a feasible joint commitment remains selected unless emergency
avoidance is required. The report distinguishes response changes/ABA oscillations from
changes where the previous entire joint commitment has become infeasible. A diagnostic
warning identifies a feasible joint commitment changed without context/emergency cause.

After actual-path verification, an eligible unresolved mechanical episode authorizes
exactly one attempt in the visibly separate legacy consequence routine, even if its
occurrence roll has no effect. The coordinator supplies that physical pair to the legacy
candidate boundary, preserving its original addressed occurrence/severity formulas and
consequences. Other contacts for that owned episode are filtered out. Clear pre-contact
responses cannot independently receive fabricated legacy side-by-side consequences.
#56B adds no consequence calculation; existing production incidents and the authorized
legacy fallback can still produce their existing events.

## Canonical inputs and provisional controls

Attack improves offensive alternative preference; Defense improves inside-cover recognition
and preference. Combativeness affects contest preference and conservative planning margin.
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

### Recorded batch

The 76 sampled heats produce 92 episodes: 30 first-bend and 62 ordinary-racing
phase groups. Their final observations contain 72 clear episodes (78.26%) and
20 unresolved episodes, with 10 legacy fallback attempts. First-bend observations
have zero legacy attempts; ordinary-racing observations have 10. Counts of clear
observations within a phase differ from final episode counts because one commitment
can be checked at several segment boundaries.

Maximum episodes per pair per heat is 2; maximum active duration is 5.7358650193 s;
maximum response changes inside one episode is 4; maximum legacy attempts per episode
is 1. Across the batch, 20 ABA changes occur where the previous joint commitment is
infeasible or context/emergency permits a change. Unexplained commitment changes and
storm warnings are zero. These are synthetic observations, not empirical race claims.

| Resolution case | OFF median | ON median | OFF allocation | ON allocation |
|---|---:|---:|---:|---:|
| Separated pair | 0.6725 ms | 5.3507 ms | 68,648 B | 414,864 B |
| Dense three-rider squeeze | 0.8977 ms | 228.1623 ms | 93,464 B | 71,786,312 B |

These desktop measurements were taken alongside validation work; they are representative
cost observations, not stable hardware-independent thresholds. Feature OFF introduces
only option validation and a gate; complete captures remain byte-identical. ON separated
riders pay geometry/retention cost, while clustered rich replay remains materially more
expensive and should remain opt-in during balance work.

```sh
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
dotnet test tests/CoreSim.Tests -c Release --no-build
python tools/rider-compatibility-audit/check-consumers.py
python tools/ci/check-shards.py results/discovery
python -m unittest discover -s tests/calibration -p 'test_*.py'
dotnet run --project tools/contested-space-audit -c Release -- results/contested-space
dotnet run --project src/Sandbox -c Release -- contested-space-response-performance results/performance
```

The independent [performance capture](contested-space-response-performance.json) uses
one warmup and five measured resolutions per case; medians and allocations are observational,
with no CI wall-clock gate. Feature OFF, feature ON separated, and feature ON a dense
three-rider cluster are measured separately. The quiet ON path still observes #55 geometry
and executes legacy-compatible final resolution, but generates no alternative combinations.
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
