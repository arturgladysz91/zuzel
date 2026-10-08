# #56C2 physical contact consequences and recovery

Status: implemented behind a default-OFF flag; Draft, independent review required. Calibration decision recorded before consequence code in commit `2cb8acc10ac6e1f7e585324b3604438bb73e4637` and the initial PR #59 body.

The first P1 correction from review `5452627332` integrates #61's merged performance changes and
closes native trigonometry in enabled track-clearance and tactical decision gates. Its arithmetic
contract, complete native-call audit, exact changed fields and measured pre/post performance are
documented in [contact-decision-geometry.md](contact-decision-geometry.md). The two separate
pair-recontact/recovery review findings remain outstanding.

## Prerequisite

PR #58 merged on 2026-10-07 at 20:45:15 UTC as `5989301192565f6a265d53a2db12c7d00c94fedc`. Its tree is exactly `400eee32bc10d4df001fc2c8d7243052bdd81ab2`, equal to reviewed head `51d07b8762a7925aeb9f8a31ea8018a29d2dc853`. Final-head Actions run 187 (`37534295406`) passed all nine jobs, including Windows/Ubuntu determinism and cross-platform comparison. This branch starts at that merged main.

## Applied severity calibration

Retain centralized boundaries **0.35 / 0.70 / 1.05 / 1.55** after reviewing the complete #56C1 evidence. No impulse, reserve, threshold or frozen #56C1 evidence changes are needed. This is an explicit applied design decision, still a synthetic calibration rather than measured motorcycle recovery capability.

Gentle parallel brush has ratio 0.0505; moderate side contact 1.0094 and rear closing 0.9158 sit in LostRhythm; crossing rider A is 4.7241. Center versus lever and chassis versus bar show additional geometric yaw demand rather than component bonuses. Technique 20/50/80 produces 1.1919/1.0094/0.8754; Strength produces 1.1503/1.0094/0.8993; Condition 1/0.7/0.4 produces 1.0094/1.0546/1.1041. One-rider mass 60/67.5/75 kg produces 1.0363/1.0094/0.9839. Grip changes reserve monotonically. The three-rider squeeze retains aggregate demand despite canceled net velocity (middle ratio 1.4278, MajorSave); four-rider and disconnected frontiers retain independent, simultaneous aggregates.

The ordinary synthetic population (2,916 rows) has ratio P10/P25/P50/P75/P90/P95/P99/MAX = .041/.078/.258/.651/1.233/1.582/2.020/2.388 and labels **1683 Brush, 576 Disturbed, 267 LostRhythm, 243 MajorSave, 147 Crash**. The deliberately severe population (1,458 rows) has 0/0/3/147/1308 respectively, with ratio median 3.588 and maximum 12.592. These are equal-weight synthetic grids, not frequencies of racing contacts. Crash is confined to the high-demand tail of ordinary settings; moderate reference contacts recover, MajorSave has a distinct useful region, and severe crossing exceeds recovery. No thresholds were fitted to a crash percentage or legacy outcomes.

Continuous recoverable control loss is `0.8 * (SeverityRatio / CrashBoundary)^2`, bounded to [0,0.8]. At the retained boundaries it is approximately .041/.163/.367/.8. Drive and voluntary lateral availability are `1 - ControlLoss01`. Brush stores no recovery; the other recoverable classes store one active step. These parameters are centralized in the consequence layer. Crash is terminal. Controlled below/at/above boundary evidence and production recovery measurements will accompany implementation.

Legacy comparison remains diagnostic. The unchanged #56C1 twelve-case sample contains 27 NoLegacyOccurrence, 2 LegacyLostRhythm, 1 LegacyCrash and 12 NotAuthorized rider observations. Matching those rates is not a calibration objective.

## Consequence ownership and materialization

`EnablePhysicalContactConsequences` defaults to false and requires `EnableContestedSpaceResponses`. Diagnostics may be None, Summary or FullAudit without affecting gameplay. The coordinator reuses final actual #55 verification after the single #56B safety pass. It requests the existing #56C1 analyzer internally, retaining the complete shadow analysis and deriving an application aggregate from fresh, analyzed pairs only. This is the same pair impulse, canonical summation, RSS disturbance demand and reserve calculation; it introduces no additional detector, candidate search, impulse model or ability bonus. Filtering happens after causal-frontier classification, so consuming an earlier overlap cannot promote an invalid later contact.

The separate `PhysicalContactConsequenceResolver` builds an immutable plan from that aggregate, episode provenance, frozen snapshot, normal production endpoints and first-touch travel directions. It projects `NetDeltaVelocityMetersPerSecond` onto each rider's own unit travel direction, then sets final scalar speed to `max(0, normal endpoint speed + deltaForward)`. Crash instead sets speed zero and status Crashed. The plan is applied before final diagnostics, motions and events are constructed. Runtime assertions and tests require the final state, diagnostic speed/status, motion endpoint and typed consequence to agree. Existing solo terminal outcomes remain authoritative.

The cloned #56B tracker records consumed canonical pairs per continuous episode. Only Commit publishes that ownership. Verified episode merges union consumption; unrelated pairs remain independent. Certified clearance closes the old episode, allowing a fresh episode to apply another impact. Deferred, GeometryUnresolved and ineligible pairs produce no applied outcome. Unresolved geometry remains diagnostic; the resolver does not guess a physical consequence.

Enabled contact frames use fixed, range-reduced sine/cosine and arctangent polynomial arithmetic in the existing metric embedding, footprint, #55 diagnostics and #56C1 projection. The first cross-platform audit exposed native libm variation in one rain contact's track coordinates, propagating to five applied double fields while final race states stayed equal. This is fixed at the geometric arithmetic source, without rounding output, adding comparison tolerance, changing the detector, impulse/reserve formulas or severity boundaries. A dense quadrant/heading accuracy regression bounds error against the native functions by 2e-15. The separate raw-input/manifold/impulse regression capture is compared byte-for-byte and by typed IEEE bits on both platforms. Feature-OFF frames retain their original native calls and frozen captures; arithmetic mode remains fixed within a heat.

With the flag ON, legacy contact fallback authorization and its occurrence/crash rolls are bypassed. `SlideControl`, `PairRiding` and segment crash multipliers do not enter applied contact physics. Solo incident logic and heat-result morale remain unchanged. With the flag OFF, the original legacy ownership, event stream and race behavior remain exact, and #56C1 shadow diagnostics remain available.

## Controlled outcomes and boundaries

The JSON contains 38 reviewed #56C1 fixtures with applied plans, plus 20 boundary controls: clearly below, the adjacent IEEE value below, exactly at, adjacent above and clearly above each threshold. Equality deterministically enters the next category; there is no severity or threshold RNG. Boundary controls override the already-computed ratio explicitly to isolate classification, while geometric examples use unmodified physical analysis.

| Fixture / rider | Ratio | Class | Forward delta (m/s) | Control loss |
| --- | ---: | --- | ---: | ---: |
| Gentle parallel / both | 0.050482 | Brush | -0.000031 | 0.000849; no stored recovery |
| Moderate side / both | 1.009444 | LostRhythm | -0.012496 | 0.339305 |
| Rear closing / trailing | 0.915751 | LostRhythm | -1.000000 | 0.279242 |
| Rear closing / struck rider | 0.915751 | LostRhythm | +1.000000 | 0.279242 |
| Crossing / both | 4.724090 | Crash | -2.000000 analytical; final speed 0 | terminal; no recovery |
| Three-rider squeeze / middle | 1.427844 | MajorSave | 0.000000 | 0.678872 |
| Four-rider frontier / middle two | 1.427569 | MajorSave | approximately 0 | 0.678611 |

The squeeze demonstrates why canceled net velocity is not zero disturbance: #56C1 RSS demand retains simultaneous opposing burdens. MajorSave remains upright and has a large, finite recovery cost. Its state differs materially from Crash. A primarily side impulse has little forward effect; yaw/lateral disturbance feeds control loss rather than an invented lateral displacement.

## One-step production recovery

`ContactRecoveryState` carries source episode/frontier, ratio/class, control loss, drive availability, lateral authority and one remaining step. Brush stores none; Disturbed, LostRhythm and MajorSave store one. A new genuine impact still applies its own immediate deltaV; the stronger of existing and new recoverable impairment is retained, without multiplicative stacking. Crash, finish, retirement, heat reset and the next active production commit clear recovery.

Positive drive is scaled before the existing longitudinal resistance calculation. Drag, rolling/track resistance, braking and preparation corrections retain their existing formulas. Voluntary lateral movement uses the existing movement budget multiplied by authority; forced RunWide movement is unchanged. Rich and lean trajectory projections consume the same state through production traversal. Permanent ratings, tactical attributes and morale are not rewritten.

The matched next-step test starts at 15 m/s in lane 2, requests lane 3 on a 20 m straight, and changes only recovery. It does not include the immediate contact impulse, isolating recovery cost.

| Input ratio / class | Loss | Exit speed (m/s) | Step time (s) | Final lateral position |
| --- | ---: | ---: | ---: | ---: |
| 0 / no recovery | 0 | 17.798370 | 1.218115 | 2.594137 |
| 0.5 / Disturbed | 0.083247 | 17.535604 | 1.228182 | 2.549177 |
| 0.9 / LostRhythm | 0.269719 | 16.925852 | 1.252134 | 2.446004 |
| 1.3 / MajorSave | 0.562747 | 15.897952 | 1.294355 | 2.276047 |

Every resulting recovery is cleared. Drive, voluntary lateral progress and traversal time change monotonically. Lost time emerges from normal physics, with no direct time penalty.

## Matched legacy comparison

Four contact/ownership scenarios run at seeds 7, 19 and 57 with identical snapshots and incident frequency 2. For the 36 rider observations that receive new applied outcomes, legacy produces 33 with no contact event, two LostRhythm and one Crash. Parallel zero-closing contacts become Brush without recovery instead of occasionally crashing from legacy RNG. The real bridge gives two MajorSave outcomes at ratios 1.4598 and 1.4624 on every seed, with forward deltas -0.0640 and approximately zero. Full old/new endpoint state and typed events are retained in the JSON. These comparisons explain differences; they are not calibration targets.

## Deterministic full-heat batch and gameplay sanity

Run 96 feature-ON four-lap heats and 96 matched legacy heats: all eleven standard `FourRiderBehaviorSuite` scenarios plus a deliberately overlapping rear-closing four-rider control, seeds 7/19/57/83, Dry/LightRain, heat ID 59, incident frequency 1. Counts below distinguish repeated verified pair observations from newly applied pair impacts, and classes count rider outcomes rather than pairs.

| Population | Heats | Verified pair observations | Newly applied pairs | Applied riders | Brush | Disturbed | LostRhythm | MajorSave | Contact Crash |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Standard scenarios | 88 | 13 | 8 | 13 | 4 | 0 | 2 | 4 | 3 |
| Deliberate overlap stress | 8 | 48 | 24 | 32 | 0 | 0 | 0 | 16 | 16 |
| Total | 96 | 61 | 32 | 45 | 4 | 0 | 2 | 20 | 19 |

Verified contacts per heat are 0.1477 standard, 6.0 stress and 0.6354 overall; newly applied pairs overall are 0.3333 per heat. Three repeated overlaps are suppressed in standard scenarios. Two standard contacts and 24 stress contacts are deferred rather than applying invalid trajectories. GeometryUnresolved is zero in this batch; direct unresolved tests prove refusal instead. Terminal solo outcomes can prevent an already-crashed participant receiving another physical consequence, so rider outcomes need not be exactly twice applied pairs.

Standard scenario F / seed 83 crashes one active rider in each weather state at ratios 4.5632/4.5691 with analytical forward delta approximately -5.74 m/s: these are high-demand contacts, not moderate contacts mislabeled Crash. Scenario G / seed 7 gives four zero-closing Brushes across two weathers. G / seed 19 / LightRain has two MajorSave outcomes (ratios 1.3001/1.5104), two LostRhythm outcomes (.9659/1.0058) and one Crash (2.0069), across fresh causal frontiers. Scenario I / seed 83 / LightRain gives two MajorSave outcomes at ratios 1.3975/1.2942 and opposite forward deltas approximately +/-1.1094 m/s. Standard final order changes in 6 of 88 matched heats; classification status/time changes in 9 of 88. All eight stress heats change order/classification; their middle riders accumulate simultaneous demand near ratio 1.837 and crash while the outer riders save near ratio 1.299.

The sample is sparse after #56B avoidance: Disturbed does not occur in these standard heats, so it cannot establish its racing frequency. The contacts span zero-closing Brush, moderate LostRhythm/MajorSave and strongly above-boundary Crash; unmodified controlled moderate side/rear cases also produce LostRhythm, boundary controls produce Disturbed, and measured recovery separates all three recoverable classes. MajorSave appears in real full heats and is distinct from Crash. The aggregate stress-dominated 19/45 Crash count must not be interpreted as an ordinary crash rate. These observations support the retained physical mapping without tuning it to the legacy distribution, while broader observed contact populations remain an independent-review need.

The enabled deterministic arithmetic also changes one avoidance branch at a geometry tie (G / seed 19 / LightRain), adding its contact sequence relative to the initial native-arithmetic capture. Matched ON/OFF heat differences therefore include this numerical-mode effect as well as physical consequences. This is not a change to the contact detector or its tolerances; feature OFF retains its original arithmetic and exact captures.

Matched full-heat legacy runs use diagnostics None, which preserves the same legacy gameplay. An existing #56C1-only FullAudit replay at heat ID 59 can retain a closed episode's contact diagnostic and reject its stale provenance. The new path supplies only current verified diagnostics; feature OFF deliberately preserves the merged behavior. The frozen #56C1 standalone audit is unchanged and reproducible, and the performance baselines use stable #56B+#56C1 FullAudit heats (scenario I and contact-heavy, heat ID 58).

## Observational performance

Windows, Release/net8.0, after warmup. Plan/application loops use 2,000 iterations; solo recovery uses 500. The full contact coordinator uses the same frozen ownership scenarios for 30 resolutions after five warmups per mode, resetting episode ownership for each sample. Full heats interleave OFF/ON, discard two warmup pairs and report three-sample medians. Both full-heat modes enable #56B and #56C1 FullAudit. Isolated map/footprint calls use 100,000 iterations after 10,000 warmups; reported allocations include the benchmark boxing the returned value.

| Operation | Time per call | Allocated bytes per call |
| --- | ---: | ---: |
| Moderate-side plan / apply | 5.964 / 0.335 microseconds | 4792 / 504 |
| Three-squeeze plan / apply | 9.172 / 0.513 microseconds | 6824 / 736 |
| Four-frontier plan / apply | 15.770 / 1.221 microseconds | 9640 / 968 |
| Solo production, no recovery | 110.752 microseconds | 10392 |
| Solo production, Disturbed | 110.314 microseconds | 10392 |
| Solo production, LostRhythm | 82.166 microseconds | 5608 |
| Solo production, MajorSave | 63.917 microseconds | 5608 |
| Track-frame map, native | 0.380 microseconds | 64 |
| Track-frame map, deterministic | 0.463 microseconds | 64 |
| Footprint, native | 0.362 microseconds | 96 |
| Footprint, deterministic | 0.341 microseconds | 96 |

Reduced lateral authority can avoid the existing target-arrival subdivision, so stronger recovery can take fewer numerical substeps. These timings measure computation cost, not sporting benefit; the recovery table above shows worse speed/time.

| Complete contact step / heat | #56B+#56C1 OFF baseline (ms) | #56C2 ON (ms) | Observed change |
| --- | ---: | ---: | ---: |
| Two disjoint unresolved pairs | 76.816 | 97.830 | +27.36% |
| One disjoint pair clears | 130.192 | 180.594 | +38.71% |
| Real four-rider bridge | 188.082 | 245.241 | +30.39% |
| Contact with unrelated riders | 59.766 | 76.912 | +28.69% |
| Scenario I full heat | 3774.239 | 4371.249 | +15.82% |
| Deliberate contact-heavy full heat | 1413.630 | 462.435 | -67.29% |

Full-heat median allocation is 327,730,144 versus 300,239,192 bytes (I) and 174,941,784 versus 83,512,720 bytes (stress). Different physical outcomes, particularly stress crashes removing active riders, change subsequent search work. The stress decrease therefore does not show an isolated engine speedup.

The contact-step overhead exceeds the requested 10% investigation threshold. Before the cross-platform geometry correction, these same benchmarks ranged from -6.19% to +0.68%. The investigation isolates plan/apply work above and adds 100,000-call native/deterministic map and footprint measurements after 10,000 warmups. The frequently sampled map is about 22% slower; footprint cost is comparable. Fixed polynomial heading evaluation adds work throughout existing #55/#56B sampling, rather than adding a second search or detector. Unrolling the heading polynomial preserves all trace bits and reduces its loop overhead. Remaining overhead is explicit and requires review; exact platform equality takes priority over the original native arithmetic's speed. The I heat is about 16% slower, while terminal outcomes make the stress heat shorter. These timings are observational, with no wall-clock CI gate.

## Explicit approximation and scope

The contact plan changes final velocity/status and next-step control. It preserves the segment's already generated position, elapsed time, lateral position and surface wear. Production traversal profiles therefore describe pre-impact motion; the explicit final diagnostic fields and motion endpoint describe the applied outcome. There is no full post-impact reintegration of the segment remainder and no compensating time penalty. A crashed rider exposes speed zero and cannot participate in later active racing movement.

Only scalar speed is persisted: lateral/yaw impulse contributes to disturbance and recovery, without persistent lateral velocity or rigid-body rotation. Motorcycle geometry remains #55's responsibility. Rider-body contact, blame, injury, damage, iterative impact solves and long-term impairment remain outside this stage.

## Reproduction and validation

From the repository root:

```text
dotnet build SpeedwayManager.sln -c Release --warnaserror
dotnet test tests/CoreSim.Tests -c Release --no-build
python -m unittest discover -s tests/calibration -p "test_*.py"
python tools/rider-compatibility-audit/check-consumers.py
python tools/ci/check-shards.py results/discovery
dotnet run --project tools/physical-contact-audit -c Release -- results/physical-contact
dotnet run --project tools/physical-contact-consequence-audit -c Release -- results/physical-contact-consequences
dotnet run --project tools/physical-contact-consequence-audit -c Release -- results/performance --performance
git diff --check
```

The deterministic audit emits structured JSON plus typed float/double IEEE bits. CI captures both on Windows and Ubuntu and requires byte equality across platforms and equality with this checked-in consequence JSON. Performance is a separate observational report, never a timing gate. All nine existing CI jobs remain, with the complete .NET suite partitioned exactly once and all five older platform comparisons retained.

Local validation includes the full 2,102-case suite before the extra arithmetic regression, all 54 final consequence cases, 308 geometry/avoidance/analysis/consequence regressions, 21 Python tests and warnings-as-errors builds. Eleven historical capture files remain byte-exact against reviewed #56C1, including 50,262 #56C1 IEEE fields; eight historical calibration document blobs remain unchanged. The frozen 39-consumer audit verifies exact reviewed source extraction rather than widening its legacy allowlist. Final-head CI is recorded in the PR after completion.
