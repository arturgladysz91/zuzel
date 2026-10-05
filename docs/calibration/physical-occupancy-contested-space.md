# Physical motorcycle occupancy and common-time contested space (#55)

Base: `12cce6f9709f34ea1617d21399d2a615b3f689af` (current main, squash #54).
Status: observation foundation; dimensions/attitude remain **PROVISIONAL** except the sourced legal handlebar range.
Deterministic evidence: [machine-readable scenarios and sensitivity](physical-occupancy-evidence.json).

## Audited architecture and binding boundary

#53 supplies immutable `ResolvedRiderMotion` for moving, fixed, launch and partial-crash paths. Moving nodes adapt `ExecutedSegmentPath`; fixed nodes observe existing longitudinal integration. The sampler interpolates stored local progress and physical offset, with each rider's independent heat-clock origin. #54 ranks bounded Entry/Apex/Exit intents by isolated Full/Lean production replay. This PR changes neither owner.

`ResolvedBikePoses.FromMotion` reads those same nodes and immutable `Track.CornerTopology`. `PhysicalSpaceObserver` attaches only through the existing explicit post-Resolve/pre-Commit observer hook. `Complete()` runs after capture, so it can intersect motions from different simulation steps and segment traversal durations. It never runs in `TrajectoryEvaluator`, normal Decide, or Lean candidate projection. Canonical rider results, motion equality/hashes, diagnostics, surface wear and legacy contacts are unchanged. No production object receives an extra geometry field.

The new dependency is:

```mermaid
flowchart LR
    R[Production Resolve / immutable #53 motions] --> A[Shared metric track embedding]
    P[Shared provisional attitude profile] --> A
    D[Explicit mechanical dimensions] --> A
    A --> C[Pose-only continuous space observer]
    I[Injected attitude / pose intervals] --> C
    C --> E[Typed conflict / closure / ambiguity evidence]
    E --> F[Future #56 consequence layer]
```

This is **not a complete tyre/slip motorcycle dynamics model**. Legacy `0.55 m` decision/contact thresholds, `0.12 s` endpoint eligibility, RNG, LostRhythm/crash and forced movement remain temporary gameplay mechanics. They neither define the new dimensions nor consume the geometry in #55.

## Physical provenance and assumptions

Source audit performed against the FIM 2026 Track Racing Technical Regulations, version 1 updated 10 June 2026. [Official publication page](https://www.fim-moto.com/en/documents/view/2026-1-track-racing-technical-regulations-10062026) and [official PDF](https://www.fim-moto.com/fileadmin/2026_1_TRACK_RACING_Technical_Regulations_10.06.2026.pdf), §01.33 / 33.01, printed p.16: 250/500 cc Track Racing handlebar width is 700–900 mm. The June revision changes §01.67, not this range.

| Quantity | Source/status | Use in #55 |
| --- | --- | --- |
| Handlebar legal range 0.70–0.90 m | **VERIFIED**, FIM 2026 §01.33 | Sensitivity envelope; midpoint is a provisional reference choice |
| Overall mechanical length | **PROVISIONAL**, no authoritative modern 500 cc whole-bike measurement established | Longitudinal capsule extent |
| Chassis/body width | **PROVISIONAL** | Body capsule diameter |
| Handlebar location and tube diameter | **PROVISIONAL** | Transverse capsule location/diameter |
| β peak and phase timing | **PROVISIONAL**, no validated rider yaw telemetry | Independent shared reference attitude |
| Production reference point as mechanical footprint centre | **PROVISIONAL** mapping | Explicit geometric reference, not a measured axle/CG location |

FIM wheelbase limits for sidecars (§01.54) and 85 cc machines (§01.83) were rejected as out-of-class evidence. Historical road-motorcycle dimensions and engine-only manufacturer specifications are not evidence for a modern speedway bike's complete mechanical length. No illustrative large-slide statement is used to calibrate a universal yaw angle. Wheelbase is omitted because this footprint does not need it.

| Central parameter | Lower / reference / upper used in sensitivity | Status |
| --- | --- | --- |
| OverallMechanicalLengthMeters | 1.90 / 2.10 / 2.30 m | PROVISIONAL |
| ChassisBodyWidthMeters | 0.25 / 0.30 / 0.35 m | PROVISIONAL |
| HandlebarWidthMeters | 0.70 / 0.80 / 0.90 m, varied independently | Range VERIFIED, selected value PROVISIONAL |
| HandlebarLongitudinalOffsetMeters | +0.55 / +0.65 / +0.75 m | PROVISIONAL |
| HandlebarTubeDiameterMeters | 0.03 / 0.04 / 0.05 m | PROVISIONAL |
| PeakSlideAngleRadians | 0 / π/6 / π/3 in the production sensitivity | PROVISIONAL; 0 is control |
| SlideBuildStartProgress | reference 0.05 | PROVISIONAL |
| PeakProgress | reference 0.35 | PROVISIONAL |
| StraighteningStartProgress | reference 0.55 | PROVISIONAL |
| StraighteningCompletionProgress | reference 0.95 | PROVISIONAL |

`SpeedwayBikeDimensions.Reference` and `ReferenceBikeAttitude.Neutral` own all defaults. Inputs are immutable and finite/domain-validated. Legal range enforcement belongs to a future ruleset; mathematical geometry can accept other explicitly supplied dimensions. These are an audit menu, not telemetry confidence intervals or contact-count targets. No constant was selected to obtain a preferred number of overlaps.

## Mechanical footprint

The longitudinal body capsule has axis length `L − bodyWidth` and radius `bodyWidth/2`, preserving total mechanical length L. A transverse handlebar capsule has axis length `barWidth − tubeDiameter`, radius `tubeDiameter/2`, and centre `referencePoint + forward * barOffset`. Both rotate with ψ. The geometry allocates no component collection per separation query.

```text
                  transverse bar capsule
                         |  width  |
    rear  (==============+==============)  front
          longitudinal body capsule
                     reference centre --> forward ψ
                        bar centre at +offset
```

Mechanical occupancy contains no rider shoulder, leg, personal space or tactical clearance. A future body envelope may be a separate component/layer. Capsule-axis distance minus both radii is signed separation; the composite result is the minimum of the four ordered component pairs. Positive is clear, zero is touching, negative is overlapping. `PenetrationMeters = max(0, -signedGap)` is the capsule signed-gap depth, not a full rigid-body penetration/impulse solution. Components at the sampled minimum are retained.

## Coordinates, travel and motorcycle direction

All positions are metres in one deterministic Euclidean model-space frame. Times are seconds of the heat; angles are radians, counterclockwise positive, wrapped to [-pi, pi). The origin at segment 0 is (0,0), initial reference heading 0. These choices have no physical significance: a common rigid translation/rotation preserves separation, first-touch time and classification (AG).

`TrackMetricEmbedding` composes the **model-space track implied by existing TrackGeometry**, not surveyed Motoarena coordinates. An observer constructs one immutable embedding lazily, reuses it throughout the heat and rejects incompatible later geometry/topology. No process-global cache, physics replay, altered travelled distance or production state is introduced.

```text
                    back straight
          .-------------------------------.
        /                                   \
       /                                     \
       |          modeled circular arcs      |
       \                                     /
        \                                   /
          '-----------|-------------------'
                 27 m | 35 m home straight
                   start/finish (0,0), heading 0 ->
```

Walking the ordered segments stores every inner-reference entry position P0 and heading h. Let F=(cos h,sin h), N_left=(-sin h,cos h), N_out=-N_left. On a straight of L=override ?? StraightLengthMeters:

```text
P(p,o) = P0 + F*L*p + N_out*o
V = F*L*dp/dt + N_out*do/dt
```

On a left turn, R=InnerRadiusMeters, alpha=TurnSegmentAngleRadians, C=P0+N_left*R, delta=alpha*p, O=Rotate(N_out,delta), T=Rotate(F,delta):

```text
P(p,o) = C + O*(R+o)
V = T*(R+o)*alpha*dp/dt + O*do/dt
```

Travel heading chi is atan2(V.y,V.x) when moving, the reference tangent when stationary. Bike heading psi=chi+the unchanged independent beta profile. Actual progress and physical offset derivatives come from stored #53 nodes, never requested lanes. A segment endpoint gives the next P0; a turn adds exactly alpha to h. Internal Entry/Middle/Exit labels are ordinary continuous portions of the same arc.

`FrameId` identifies coordinates that may be subtracted directly. Its deterministic identity contains exact track geometry/topology and the chosen rigid transform, **no lap or corner number**. `PoseSource` separately stores LapIndex, SegmentIndex, SegmentId, SegmentType and CornerId on intervals, samples and evidence events. Diagnostics never parse frame strings. All valid closed-track laps share one frame, including lapped riders. The reference tangent only removes shared forward transport in contribution diagnostics; it does not replace chi or orient the bike.

For the same physical offset o at straight -> turn:

```text
P_straight_end(o) = P0 + F*L + N_out*o
P_turn_start(o)   = (P0+F*L+N_left*R) + N_out*(R+o)
                  = P0 + F*L + N_out*o
T_straight_end    = T_turn_start = F
```

The same cancellation at turn exit gives the following straight's point and tangent. U/V/W test both corners and all internal arc joins to 1e-12 m/rad. This continuity uses **equal physical metres**, independently from normalized lateral coordinates.

At normalized position 2, Motoarena usable straight width is 10 m and modeled turn width is 14.6 m: offsetStraight=5 m, offsetTurn=7.3 m (2.3 m difference). P/Q/X preserve that one-sided width reinterpretation. It is a zero-time state discontinuity, with no extra metres/time and no interpolated path between its two positions.

The endpoint of segment 8 (27 m) and start of segment 0 (35 m) represent one physical start/finish on the modeled 62 m home straight. Exact float input angles leave a measured closure residual, never snapped or distributed: position 5.4651e-6 m, heading 1.748e-7 rad. Closure tolerances are 2e-5 m and 5e-7 rad, explicit micrometre-scale allowances for the existing float geometry. AD/AF retain the nonzero residual, exact original angle and 35+27 split. Malformed/open tracks expose their residual; within-lap embedding works, but cross-lap comparisons report `NonClosingLapWrap` coverage gaps. Unsupported geometry is never forced closed.

## Independent provisional attitude

Straight/reaction β is zero. Corner β is cubic smoothstep from zero between build-start and peak, holds the peak through straightening-start, then smoothstep returns to zero at completion. The neutral peak is 30°, a provisional input. β is independent from speed, `v²/R`, lateral acceleration, lean, rear tyre slip ratio and tyre slip angle. Current constraints do not contain enough tyre information to determine ψ uniquely from χ.

Early `(0,.5,.15,.25,.60)` and late `(.1,.5,.40,.70,1)` profiles in the evidence demonstrate different footprints at identical centres, with tuples `(build, peakRadians, peakPhase, straightenStart, completion)`. A smaller profile is simply a lower peak. Production uses one shared neutral profile for all riders. The resolver consumes `PhysicalPoseInterval`; it has no rider-skill/profile dependency and does not know the attitude's cause. Future injected intervals must supply conservative translation/rotation rate bounds.

> A rider may keep approximately the same centre trajectory while the rotating motorcycle footprint enters another rider's occupied space.

This is demonstrated by I: A and B both translate 20 m parallel with unchanged centre separation. A rotates from 0 to π/2 over the interval, B stays aligned. Both endpoint footprints are clear; an interior footprint overlaps. The 90° controlled sweep is a geometric stress probe, not the default reference peak. K compares identical centre paths with fixed zero versus π/4 yaw; occupied-space conclusions differ. J retains positive translation and rotation contributions simultaneously.

## Common-time continuous algorithm

The adapter creates continuous spans between **existing motion nodes**, plus exact attitude-phase crossings. Common time is `double(StartElapsedTimeSeconds) + LocalTimeSeconds`; travelled metres are never used as a shared longitudinal origin. Reaction endpoints, duplicate-time state events and segment boundaries are temporal cuts. Pair evaluation intersects actual domains using two sorted interval streams across the entire captured heat. A maximum of six unordered four-rider pairs is independent of input collection order; A/B mean ascending rider ids.

Bounding circles conservatively reject contact where centre separation cannot close enough over the interval. Narrow phase evaluates only immutable poses and four capsule pairs. For linear centre/shortest-angle histories, centre velocity is exact and angular speed is bounded by the normalized angle advance. For production polar nodes, radius and angular progress are linear: `|position''| <= rMax*θRate² + 2*|rRate*θRate|`; χ rate is at most `2*|θRate|`. Smoothstep contributes at most `1.5*peak/phaseSpan * progressRate` to ψ rate.

Relative centre-speed bound uses the difference of midpoint velocities plus both acceleration bounds times half duration. Add each footprint's bounding radius times its angular-speed bound to obtain a conservative signed-distance Lipschitz bound L. For an interval with endpoint gaps g0/g1, `min(g0,g1)−L*duration/2` is a lower bound, clamped only by the mathematical minimum `−radiusA−radiusB`. Deterministic left-first adaptive bisection searches for the earliest contact and a pair-wide minimum. The minimum incumbent is shared only inside this unordered pair. Far intervals that cannot improve it avoid extra subdivision. No uniform global timestep or second physics replay exists.

Every compatible interval retains its sampled minimum and conservative lower bound. The **pair-wide** minimum across the complete report is certified within the minimum-gap tolerance when `NumericallyResolved` is true for all searched intervals; individual far-interval sampled minima are not independently refined to that tolerance. Components/time refer to the evaluated minimum; the time of a flat or near-flat minimum need not be unique. Work counts are interval/algorithm counts, not distinct racing incidents.

Bound exhaustion and unresolved tangent intervals are explicit (`NumericallyResolved=false`, `UnresolvedIntervals`); they are excluded from `EligibleForFutureInteraction`. A potential interior tangent must never become a silent clear result. Clear endpoints alone never reject a rotating or crossing candidate.

## Numerical tolerances

| Named value | Value | Purpose |
| --- | --- | --- |
| ContactDistanceMeters | 1e−6 m | Numerical touching tolerance, not clearance/body inflation |
| MinimumSeparationToleranceMeters | 1e−4 m | Pair-minimum branch bound termination |
| TimeToleranceSeconds | 1e−7 s | Earliest-contact bracket and common-time equality |
| AngleToleranceRadians | 1e−9 rad | Documented angle equality precision; interpolation uses wrap, not scalar epsilon jumps |
| BroadPhasePaddingMeters | 1e−8 m | Conservative rejection pad |
| MaximumSubdivisionDepth | 30 | Hard deterministic recursion bound |
| MaximumEvaluationsPerInterval | 4096 | Hard narrow-phase budget; exhaustion remains unresolved |
| ParallelClassificationAngleRadians | 0.1 rad | Direction category for diagnostics only; never a contact tolerance |

The new JSON writer rounds presentation doubles to 10 decimal places (at most 5e−11 in displayed units). Raw geometry and tests still use double values; this does not feed physics or change any existing calibration/fingerprint. New Windows/Ubuntu evidence compares canonical bytes at that presentation precision. The independent #54 audit still compares complete raw structured values and IEEE bits exactly.

## Translation/rotation contributions and labels

At the first-touch onset knot interval, compare stored start/end poses only. For nearly parallel reference-track tangents, remove the minimum shared positive forward transport so two bikes moving together are not assigned fictitious longitudinal encroachment. Then measure signed gap reduction for A translation with its old orientation and frozen B, A rotation at its old centre and frozen B, and both corresponding B cases. Actual closure and the nonadditive residual are also retained. Contributions can be negative (opening space), and they are an approximate geometric decomposition, not conserved forces, blame or additive credit.

Flags distinguish RearClosing, A/B translation, A/B rotation, A/B mixed, MutualConvergence, CrossingPaths, ParallelOverlap and BoundaryAmbiguous. RearClosing uses physical relative longitudinal position and velocity; crossing uses lateral-order reversal across the interval. Existing overlap with zero onset change is ParallelOverlap. A closing contribution above numerical contact tolerance supports its corresponding flag. Relative position and relative velocity at the onset are preserved for #56. The onset's whole meaningful knot interval is used, not an almost-zero root bracket. Equivalent subdivision tests protect labels/contact time/minimum, while raw contribution magnitudes are interval-dependent and should not be mistaken for whole-manoeuvre strength.

## Boundaries and incomplete frame coverage

**NEVER sweep FromPhysicalOffsetMeters to ToPhysicalOffsetMeters.** Every positive-time stored node span stays continuous; duplicate-time events and width transitions remain explicit temporal cuts. CCD terminates at the pre-boundary pose and restarts at the post-boundary pose in the **same metric frame**. Translation contributions use only traversed spans.

Clear -> jump-created overlap becomes `BoundaryAmbiguous`, with no physical first-touch root and `EligibleForFutureInteraction=false`. Quarantine persists through continuously overlapping spans. A bounded Lipschitz clearance search ends it once a physically clear pose outside the root-time uncertainty band is observed, including clearance inside one supplied span; subsequent continuous contact is eligible. Search exhaustion remains unresolved and ineligible. Genuine pre-boundary contact stays eligible in history and an already established overlapping contact is not reclassified merely for crossing a boundary (AA/AB/AC).

Ordinary production Straight/Corner, Corner/Straight, internal corner and start/finish changes produce **zero metric frame gaps**. Boundary-discontinuity ambiguities are a separate concept, not missing geometric coverage. Injected unrelated frames retain defensive typed `FrameCoverageGap` diagnostics; malformed nonclosing tracks retain explicit unsupported cross-lap gaps. #56 receives complete ordinary production metric geometry.

## Deterministic scenarios and acceptance evidence

`PhysicalSpaceTests` contains focused A–AK controls, executed again in Windows/Ubuntu determinism CI. The generated appendix below records scenario numbers, all six production start/first-bend pairs, sensitivity and work. Different-lane C uses physical overlap irrespective of lane identity; same-lane B stays safe due to longitudinal distance. Geometry intentionally accepts no Lane parameter.

| Case | First touch s | Pair minimum m | Geometric conclusion |
| --- | ---: | ---: | --- |
| A | none | 2.2000000 | No observed eligible conflict |
| B | none | 2.9000000 | No observed eligible conflict |
| C | 0.0000000 | -0.1700000 | ParallelOverlap |
| D | 0.2899999 | -0.3000000 | RearClosing, AEncroachesByTranslation |
| E | 0.5999995 | -0.3000000 | AEncroachesByTranslation |
| F | 0.5999995 | -0.3000000 | BEncroachesByTranslation |
| G | 0.7333331 | -0.3000000 | AEncroachesByTranslation, BEncroachesByTranslation, MutualConvergence |
| H | 0.3999999 | -0.3000000 | AEncroachesByTranslation, BEncroachesByTranslation, MutualConvergence, CrossingPaths |
| I | 0.1047091 | -0.3000000 | AEncroachesByRotation |
| J | 0.4006876 | -0.2201244 | AEncroachesByTranslation, AEncroachesByRotation, AEncroachesMixed |
| K-zero | none | 0.1000000 | No observed eligible conflict |
| K-yaw | 0.0000000 | -0.2863961 | ParallelOverlap |

All controlled cases are numerically resolved. D touches at approximately `(5−2.10)/10 = 0.29 s`. I/J distinguish rotation-only and mixed closure.

| Required case | Additional tested evidence |
| --- | --- |
| L | Early/late profiles produce different ψ/footprints at identical centres; profile samples are in JSON |
| M | Four stationary production gate centres; six pairs clear throughout common reaction |
| N | Genuine production launch convergence, first-bend capture and unchanged consequences |
| O | 0.75 m separation maps through different normalized straight/turn widths with identical mechanical gap |
| P/Q | Both width-boundary directions share one metric frame while one-sided offsets remain unswept |
| R | Controlled and actual four-rider/four-lap reversed collections return exactly equal diagnostics |
| S | D/G/H/I/J at an added equivalent 0.37 s node preserve labels, first touch within 2e−7 s and min within 1e−4 m |
| T | π−0.01 → −π+0.01 interpolates through −π with 0.02 rad sweep, no spurious revolution |

Production K-crossing has first physical touch at 0.5816524 s, before centreline intersection. It still produces its unchanged legacy events.

| Start/first-bend pair | First eligible time s | Min gap m | Min time s | A translation / rotation m | B translation / rotation m | First classification | Boundary ambiguities / metric gaps |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| 1-2 | 1.3345104 | -0.3000000 | 1.7940948 | 0.0109309 / 0.0000000 | -0.0190137 / 0.0000000 | AEncroachesByTranslation | 7 / 0 |
| 1-3 | none eligible | 0.3069720 | 7.1556736 | - | - | No observed eligible conflict | 0 / 0 |
| 1-4 | none eligible | 4.0982967 | 1.8416817 | - | - | No observed eligible conflict | 0 / 0 |
| 2-3 | none eligible | 0.6011304 | 2.9329557 | - | - | No observed eligible conflict | 0 / 0 |
| 2-4 | none eligible | 3.0995333 | 2.9329557 | - | - | No observed eligible conflict | 0 / 0 |
| 3-4 | none eligible | -0.1700000 | 2.9342363 | - | - | No observed eligible conflict | 1 / 0 |

All six pairs now have complete metric coverage through launch and the entire first bend. The first eligible contact remains 1-2 at 1.3345104 s; geometry was not tuned to preserve it. Newly evaluated straddling spans reduce minima for 2-3 (1.6353340 -> 0.6011304 m) and 2-4 (4.1501554 -> 3.0995333 m). Pair 3-4 now has a -0.17 m width-jump-only overlap at 2.9342363 s, quarantined without an eligible physical root. Pair 1-2 has seven discontinuity-ambiguity spans; 3-4 has one. These eight spans are not eight physical collisions. Previously these regions were missing behind frame mismatches.

The complete four-rider/four-lap audit also has zero metric frame gaps, eight boundary ambiguity spans and zero unresolved intervals. JSON includes all pair minima, times, classifications and raw first-contact translation/rotation contributions for both audits. Negative contributions mean opening space in a frozen-pose counterfactual; the joint closure retains its separate residual.

| Sensitivity group | Cases | Conflicting / clear | Conclusion |
| --- | ---: | ---: | --- |
| A | 27 | 0 / 27 | robust safe across menu |
| B | 27 | 0 / 27 | robust safe across menu |
| C | 27 | 27 / 0 | robust conflict across menu |
| I | 27 | 18 / 9 | assumption dependent |
| J | 27 | 18 / 9 | assumption dependent |
| production-first-bend-1/3 | 27 | 0 / 27 | robust safe across menu |

The 135 controlled combinations vary three independent bar widths, three body/length/location bundles and three yaw sweeps. The 27 production first-bend 1/3 combinations vary the same dimensions/bar widths with the actual smooth neutral profile peak 0/30/60°. Zero yaw removes the rotation mechanism. These observations neither fit telemetry nor imply empirical contact probabilities.

| Acceptance gate | Evidence |
| --- | --- |
| 1 | Straight/adjacent Corner comparable: YES — Y/Z, genuine eligible straddling contact. |
| 2 | Corner/following Straight comparable: YES — Y covers both exits. |
| 3 | Segment 8/0 comparable: YES — AE clear and genuine contact across laps. |
| 4 | Motoarena ordinary frame gaps zero: YES — AI and regenerated first-bend JSON. |
| 5 | Four-lap frame gaps zero: YES — AJ and complete-heat JSON. |
| 6 | Equal physical offset continuous: YES — U/V/W, both corners at 1e-12 tolerance. |
| 7 | Unequal width reinterpretation unswept: YES — P/Q/X, unchanged #53 boundary offsets/metres/time. |
| 8 | Jump-only overlap ineligible: YES — AA, null first-touch and zero jump contribution. |
| 9 | Later continuous re-contact eligible: YES — AB after certified clearance. |
| 10 | Chi/psi/beta independent: YES — unchanged neutral profile and original diagonal/polar/attitude controls. |
| 11 | Translation/rotation preserved: YES — original I/J plus Z and AG. |
| 12 | Between-node CCD preserved: YES — original H/I clear endpoints with interior contact. |
| 13 | Rider order invariant: YES — R/AH exact reports and full reversed heat. |
| 14 | Race outcomes unchanged: YES — AK existing enabled/disabled fixed and Adaptive exact state/log/surface tests. |
| 15 | #54 projection unchanged: YES — no changes in TrajectoryEvaluator or production replay; frozen/Full-Lean guards. |
| 16 | Observer RNG-free: YES — immutable pose-only dependency audit and enabled/disabled equality. |
| 17 | Motoarena closes: YES — AF, residual retained, no geometry tuning. |
| 18 | Start/finish one physical location: YES — AD/AE, 35+27 m split. |
| 19 | Remaining gaps unsupported only: YES — unrelated injected frames and explicit NonClosingLapWrap tests. |
| 20 | #56 needs no ordinary coordinate-transition repair: YES — shared metric pose, separate topology/discontinuity validity. |

## Performance protocol

`physical-occupancy-performance` captures an actual four-rider/four-lap Motoarena heat once with shared fixed convergence intents, then observes the same immutable spans after one warmup over five repeats. It reports heat+capture time/allocations separately from resolver time/allocations. Main-thread `GC.GetAllocatedBytesForCurrentThread` is valid because this geometry fixture has no parallel decision workers. No wall-clock CI assertion is added. Algorithmic counters are deterministic; runtime and allocation observations are not checked-in deterministic evidence.

Measured on this same Windows desktop in Release, old reviewed HEAD 4151a4646123d483d15fbcedfa3590a09be5ef4c versus the corrected embedding. No wall-clock CI gate is added.

| Metric | Before | After |
| --- | ---: | ---: |
| Captured pose intervals | 5964 | 5964 |
| Pairs | 6 | 6 |
| Metric candidate intervals | 11183 | 17481 |
| Broad-phase rejects | 11080 | 17374 |
| Narrow-phase evaluations | 31575 | 44282 |
| Subdivisions | 9209 | 9304 |
| Root iterations | 74 | 106 |
| Eligible/continuous contact spans | 68 | 61 |
| Unresolved intervals | 0 | 0 |
| Boundary ambiguity spans | 7 (prior report; separate from frame gaps) | 8 |
| Genuine metric frame gaps | 6297 | 0 |
| Resolver median ms | 70.5590 | 95.6452 |
| Mean resolver allocation MB | 26.510 | 32.252 |
| Heat + capture ms / allocation MB | 149.746 / 5.809 | 124.091 / 5.849 |

The 6297 previously missing spans are now evaluated geometrically; candidate count grows about 56%. Distant track regions are broadly rejected. Embedding reuse, avoiding redundant initial clear-pose samples and reusing endpoint poses in narrow evaluation limit allocation growth. The measured resolver cost increases about 36% and allocations about 22%, while preserving all coverage. Timing depends on machine/load. #54 candidate projection remains outside this observer cost.

## Reproduction, historical freeze and limitations

```text
dotnet restore
dotnet build -c Release --no-restore -warnaserror
dotnet test -c Release --no-build
python -m unittest discover -s tests/calibration -p 'test_*.py'
python tools/ci/check-shards.py <scratch>/discovery
dotnet run --project src/Sandbox -c Release --no-build -- physical-occupancy-report <scratch>
dotnet run --project src/Sandbox -c Release --no-build -- physical-occupancy-performance <scratch>
git diff --check
```

Regenerate new evidence byte-for-byte and compare to the checked-in file. Re-run existing #53/#54 reports only into scratch space and compare frozen bytes. Existing historical documents/JSON/manifests are unchanged. Observer enabled/disabled tests compare exact classification, final rider position/speed/morale, decisions/logs/legacy events, surface changes and every final raw surface cell, including Adaptive. Four-lap reversed collection diagnostics are also exactly compared.

Not implemented: tyre forces/slip/lean integration; rider-specific attitude skill; shoulder/limb envelope; safety margin; collision impulse; holding/yielding/forced-wide behaviour; aggression/strength/racecraft; traffic-aware #54 projection; speed loss/recovery/crash consequences; new RNG; calibrated real β telemetry; a physically validated straight/corner width transition. The reference point and mechanical dimensions remain provisional. Geometry does not validate real race contact frequency.

## Contract for PR #56

Consume `PhysicalBikePose` (common-time metric position, χ, ψ, β and reference tangent), `BikeFootprint`/dimensions, `ContestedSpaceEvent`, signed separation/components, translation/rotation contributions including their residual, relative position/speed and numerical/frame validity. Supply rider-specific immutable attitude intervals with meaningful knots and conservative rate bounds through `PhysicalPoseInterval`, or vary named `ReferenceBikeAttitude` phases. The collision engine consumes those poses without knowing their cause.

Ordinary production segment changes no longer create interaction coverage gaps. Straight, corner and start/finish poses share one metric model-space frame. Existing straight/turn width reinterpretation remains an explicit zero-time discontinuity and is never swept as physical movement.

Only numerically resolved events with `EligibleForFutureInteraction=true` may enter a consequence layer. BoundaryAmbiguous and coverage gaps need explicit treatment and must never silently become physical collisions. #56 can then decide hold, yield, force wider, fight for space, body versus mechanical contact, lose rhythm, recover or fall from rider characteristics. It owns deliberate replacement of the legacy consequence layer while consuming the complete metric embedding; it need not repair ordinary track coordinate transitions or rewrite capsule separation/continuous detection.

**PR #55 establishes physical occupied-space and bike-attitude geometry only. Rider-vs-rider tactical/skill resolution remains PR #56.**
