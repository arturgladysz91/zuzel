# #56C1 mechanical contact physics and severity analysis

**PROVISIONAL — requires #56C1 review. Shadow diagnostics only.** Base: `a25ab81b2d5c89d47ae6a4f33ba851eb47fc2be7`.

The independent contact model reads final unresolved #55 mechanical contacts after #56B tactical selection and safety verification. It describes an analytical impulse and a continuous control burden. It changes no speed, elapsed time, lateral position, status, surface, random stream, episode ownership, fallback authorization, or legacy consequence. The existing occurrence/crash calculations, speed multiplier, time penalty and displacement remain active. Strength and rider mass become inputs to this shadow calculation only.

## Geometry and source authority

#55 remains the sole motorcycle contact authority. The adapter reuses the final actual verification and the already available immutable pre-fallback pose history. It does not run another observer, project an alternative, replay production physics, or add a safety search. All final unresolved contacts are included, including those with an already consumed fallback allowance; `LegacyFallbackAuthorized` distinguishes them and `NotAuthorized` prevents confusing them with a legacy occurrence attempt. Episode and original fallback provenance are retained.

The event's components refer to its minimum separation, which can occur after onset. At the verified first-touch time, the geometry helper samples the existing chassis/handlebar capsules and calls unchanged `MechanicalSeparation.Between` to locate the touching component pair. It presents poses in physical position order projected onto the shared reference tangent and lateral direction, so equal-minimum chassis/bar ties preserve the same attached components after an A/B or ID swap. Opposing reference tangents use a fixed basis of the supplied metric frame for that ordering. Closest capsule-axis points yield two surface contact points, their midpoint, the normal from A to B, each center-to-contact lever, and signed separation. Equally close parallel axis solutions are averaged symmetrically. For intersecting axes, the helper finds the actual capsule boundary along the fallback normal, so a nominal radius shift cannot leave the contact point inside the capsule.

The normal uses closest component-axis separation, then first-touch bike-center separation, then `ContestedSpaceEvent.RelativePositionAtOnsetMeters` from the same verified #55 interval. #55 records this as B's minus A's interval-start position; canonicalization negates it on A/B swap. `MeterPoint` validates finite coordinates, and the fallback additionally requires finite length greater than the 1 nm direction tolerance. It supplies orientation only. Relative velocity and a reference lateral axis never invent an A-to-B sign. If every physical direction is degenerate, the result is `GeometryUnresolved`, with no manifold, impulse or severity. The old relative-velocity/reference-axis enum values remain for API compatibility but are never produced. Ambiguous #55 rows and missing first-touch pose coverage produce no rider severity. Coverage-gap fixtures preserve #55's gap report and contain no fabricated contact. No rider ID selects a physical normal.

## Effective mass and impulse model

`totalMass = canonical Profile.Physical.MassKg + ReferenceBikeMassKg`, with a centralized **77 kg** reference. FIM's 2026 Track Racing Technical Regulations, §01.19, lists 77 kg as the 500 cc Speedway minimum excluding the rider, with operational fluids and an empty fuel tank. This is a reference assumption, not a measurement of every racing bike. [FIM regulations, June 10, 2026, printed page 8](https://www.fim-moto.com/fileadmin/2026_1_TRACK_RACING_Technical_Regulations_10.06.2026.pdf).

```
relativeVelocity = velocityB - velocityA
normalClosing = max(0, -dot(relativeVelocity, normalAtoB))
reducedMass = massA * massB / (massA + massB)
referenceClosing = min(normalClosing, 50 m/s)
J = (1 + restitution) * reducedMass * referenceClosing
impulseA = -J * normalAtoB; impulseB = +J * normalAtoB
deltaVA = impulseA / massA; deltaVB = impulseB / massB
```

Restitution defaults to 0 and accepts only [0,1]. The 50 m/s reference cap is a provisional analytical bound; raw closing speed is retained separately. Shared forward speed is excluded: the brush fixture has both bikes traveling approximately 20 m/s and only 0.05 m/s normal closing. Impulses are equal and opposite, conserve normal momentum, and do not increase normal kinetic energy. Analytical vectors are never applied to rider state.

Positive #55 A/B rotational closure, divided by first-touch minus interval-start time, is a separate destabilization signal. Translation, actual closing, and the nonadditive residual are excluded from it. Initial overlap has no observed pre-contact interval: availability is false and the rotational equivalent is zero. This avoids reconstructing unobserved motion or counting translation twice. Rotation enters demand, not the translational reference impulse.

## Stability demand and reserve

Each analytical delta velocity is projected into that rider's own travel frame. `normalizedYawLever = abs(cross(leverArm, normal)) / bikeBoundingRadius`; yaw equivalent is delta-velocity magnitude times this geometric lever. It is a proxy in m/s, not an angular-velocity prediction or a full rigid-body inertia solve. There is no chassis/handlebar multiplier.

```
pairDemand² = (1 * abs(deltaVforward))²
            + (2 * abs(deltaVlateral))²
            + (1.5 * yawEquivalent)²
            + (0.5 * rotationalClosureEquivalent)²
reserve = 1 m/s * TechniqueFactor * StrengthFactor * ConditionFactor * GripFactor
SeverityRatio = combinedDemand / reserve
```

| Reserve factor | Linear bounded mapping |
| --- | --- |
| Technique | rating 1–99 → 0.75–1.25 |
| Strength | rating 1–99 → 0.80–1.20 |
| Condition | 0–1 → 0.90–1.05 |
| Effective grip | 0–1 → 0.90–1.10 |

All coefficients and thresholds live in `PhysicalContactParameters`. Technique and Strength change reserve only. Condition has a smaller effect than Technique: at this frozen contact, the Condition 0.4→1 ratio change is about 1.094×, versus about 1.361× for Technique 20→80. The adapter samples effective grip at each rider's own first-touch location in the frozen track-state snapshot by inverting the existing metric mapping. A named 1-nanometer inverse-mapping tolerance admits roundoff at valid reference edges; it never moves a bike pose, and materially outside coordinates are rejected. It does not use opponent grip or a post-displacement path. For a rider touching different surface cells within the frontier, aggregation uses the smallest sampled reserve and reports its factors.

Reaction, Start, TrackReading, Attack, Defense, PairRiding, Combativeness, PreferredLine and SetupIndependence do not enter this calculation. The canonical-data consumer audit permits only the five named shadow sources and rejects legacy execution-data reads in them; the prior 39-reader manifest and reviewed #56B extraction remain unchanged.

## Multi-rider aggregation and contact frontier

All unique pair analyses finish before any rider aggregation. Net analytical impulses are vector sums. Combined stability demand is `sqrt(sum(pairDemand²))`, so opposite impulses cannot erase the rider's control burden. Numeric summands are sorted by magnitude/value rather than rider identity. Collection order and renamed rider IDs do not choose a physical result.

Canonical, deduplicated eligible contacts are processed in chronological order. A seed whose participants have authoritative frozen paths starts a frontier. Fixed-point expansion includes only unassigned eligible contacts connected to its riders and within **0.04 seconds of the seed's earliest touch**, using the existing #55 time tolerance of `1e-7 s`. The window never chains from edge to edge. Physical minimum time anchors the window; rider IDs only serialize exact ties. Effectively simultaneous connected contacts are discovered before any invalidation, including edges encountered before their connecting edge in the sorted scan.

After complete membership and frozen pair analysis, every participating rider's original future path becomes invalid in analysis-local state. A later contact involving either such rider is `DeferredByEarlierContact`, with no manifold, impulse or severity. Unaffected riders can still start independent frontiers, even within another frontier's time window. A future bridge cannot retroactively connect completed earlier frontiers. An unresolved manifold still corresponds to a verified #55 contact, so it also invalidates that rider's future frozen path while producing no severity. `PhysicalContactWork.ContactGroups` counts causal eligible frontiers submitted to analysis, including those with unresolved manifolds. No gameplay path is changed, no impact is iterated and no trajectory is recomputed.

The three-rider squeeze has two analyzed pairs and one component. The middle rider's net impulse is zero within tolerance while combined demand is approximately 1.5592 m/s and ratio approximately 1.4278. The four-rider chain has three analyzed pairs. The disjoint fixture has two components. The later fixture analyzes one pair and defers the second; widening the experimental window to 0.12 seconds includes both.

The regenerated evidence adds these causal schedules, each composed of individually verified #55 pair onsets shifted in common time. Their combined verification manifest has zero observer-work counters because it performs no additional joint observation.

| Regression | Contacts in seconds | Frontiers | Analyzed | Deferred |
| --- | --- | ---: | ---: | ---: |
| F1 late bridge | A-B 0; C-D .060; B-C 1.000 | 2 | 2 | 1 |
| F2 simultaneous chain | A-B 0; B-C .020; C-D .035 | 1 | 3 | 0 |
| F3 start-anchored window | A-B 0; B-C .035; C-D .065 | 1 | 2 | 1 |
| F4 simultaneous disconnected | A-B 0; C-D .020 | 2 | 2 | 0 |
| F5 distant untouched | A-B 0; C-D .500 | 2 | 2 | 0 |
| F6 later mixed | A-B 0; B-C .200 | 1 | 1 | 1 |
| F7 two earlier frontiers | A-B 0; C-D .100; B-C .300 | 2 | 2 | 1 |

F8 runs each with reversed collections, swapped A/B and renamed IDs, comparing pair assignments and scalar/vector results modulo identifiers. Separate tests cover fixed-point backtracking, equivalent same-time order, the tolerance boundary, ineligible bridges and unresolved contact invalidation. N1 uses a genuinely verified overlapping #55 onset and coincident poses sampled within its allowed time uncertainty: its nonzero onset position yields a unit normal and positive approaching impulse. N2/N3 fail closed for degenerate incoming/separating motion. N4 checks all three produced normal sources under A/B swap.

## Provisional SeverityRatio labels

| SeverityRatio | Provisional label |
| --- | --- |
| < 0.35 | Brush |
| 0.35–<0.70 | Disturbed |
| 0.70–<1.05 | LostRhythm |
| 1.05–<1.55 | MajorSave |
| ≥ 1.55 | Crash |

**PROVISIONAL — requires #56C1 review.** These names describe shadow bins; they do not prescribe a race outcome, probability, recovery time, or applied penalty. Continuous raw ratio is the primary result. Thresholds have not been fitted to legacy incident rates.

## Controlled A–R evidence and sensitivity

The JSON companion contains all frozen inputs, original #55 verification, manifold/impulse decomposition, rider aggregates and work counts. A–F cover gentle parallel, moderate side, rear, crossing, center-versus-lever and chassis-versus-handlebar geometry. G–L cover Technique 20/50/80, Strength 20/50/80, Condition 1/0.7/0.4, rider mass 60/67.5/75 kg, grip 0.35/0.6/0.85 and irrelevant abilities. M–R cover squeeze, four-rider frontier, disconnected groups, deferred contact, ambiguity and coverage gaps. An additional rotating-bike onset reuses #55's existing controlled fixture.

| Frozen comparison | SeverityRatio for rider A |
| --- | --- |
| A gentle brush | 0.0505 |
| B moderate side | 1.0094 |
| C rear closing | 0.9158 |
| D crossing, rider A | 4.7241 |
| Technique 20 / 50 / 80 | 1.1919 / 1.0094 / 0.8754 |
| Strength 20 / 50 / 80 | 1.1503 / 1.0094 / 0.8993 |
| Condition 1 / 0.7 / 0.4 | 1.0094 / 1.0546 / 1.1041 |
| Rider A mass 60 / 67.5 / 75, rider B fixed 67.5 kg | 1.0363 / 1.0094 / 0.9839 |

Center-to-lever comparison changes yaw demand from zero to a positive value at unchanged closing speed. The narrow-bar chassis-control fixture is a synthetic geometry perturbation, not a proposed legal racing setup. Its difference from the reference handlebar contact is explained by the recorded lever geometry, not a component bonus. A one-rider mass sweep decreases that rider's delta velocity as mass rises. In the symmetric grid both masses rise together: delta velocity stays the same while impulse rises, as expected from reduced mass.

## Raw distributions

The ordinary grid has 2,916 rows: six normal-closing settings (0.05, 0.1, 0.25, 0.5, 1, 1.5 m/s), two contact geometries, and 3×3×3×3×3 Technique/Strength/Condition/mass/grip combinations. The deliberately severe grid has 1,458 rows with closing 3, 5, 8 m/s and the same combinations. These are synthetic input populations, not a measured racing frequency. The report retains every row and separate P10/P25/P50/P75/P90/P95/P99/MAX for closing, impulse, demand, reserve and ratio. Only 18 #55 geometry observations are needed for the grid; physiology sweeps reuse them.

| Metric / population | P10 | P25 | P50 | P75 | P90 | P95 | P99 | MAX |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Closing m/s, ordinary | .050 | .100 | .375 | 1.000 | 1.500 | 1.500 | 1.500 | 1.500 |
| Impulse N·s, ordinary | 3.613 | 7.225 | 26.625 | 72.250 | 108.375 | 114.000 | 114.000 | 114.000 |
| Demand m/s, ordinary | .050 | .096 | .263 | .601 | 1.102 | 1.653 | 1.653 | 1.653 |
| Reserve m/s, both | .799 | .900 | 1.019 | 1.143 | 1.262 | 1.327 | 1.454 | 1.454 |
| Ratio, ordinary | .041 | .078 | .258 | .651 | 1.233 | 1.582 | 2.020 | 2.388 |
| Closing m/s, severe | 3.000 | 3.000 | 5.000 | 8.000 | 8.000 | 8.000 | 8.000 | 8.000 |
| Impulse N·s, severe | 205.500 | 228.000 | 361.250 | 548.000 | 608.000 | 608.000 | 608.000 | 608.000 |
| Demand m/s, severe | 1.500 | 2.500 | 3.651 | 5.486 | 8.715 | 8.715 | 8.715 | 8.715 |
| Ratio, severe | 1.539 | 2.434 | 3.588 | 5.521 | 8.160 | 9.404 | 11.425 | 12.592 |

| Synthetic population | Brush | Disturbed | LostRhythm | MajorSave | Crash |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ordinary | 1,683 | 576 | 267 | 243 | 147 |
| Deliberately severe | 0 | 0 | 3 | 147 | 1,308 |

The ordinary grid's upper side-contact settings and weakest reserves can enter the provisional Crash bin. This is visible review evidence, not a target crash rate. The gentle brush stays below MajorSave/Crash, and the deliberately severe grid has no Brush bin. Warnings cover energy creation, unequal impulses, closing/reserve monotonicity, squeeze cancellation, irrelevant-ability influence and pair-order drift. Tests fail on these invariants.

## Legacy comparison

Twelve production-resolved cases use fixed seeds 7/19/57 and existing ownership plus unresolved scenarios, at IncidentFrequency 2. All observed legacy events and state changes are retained beside the shadow analysis. Across these cases the contact observations include 27 `NoLegacyOccurrence`, two `LegacyLostRhythm`, one `LegacyCrash`, and 12 `NotAuthorized` rider observations. Each row retains both actual legacy outcome and physical ratio/label; agreement is not required. Identical physical inputs can have different addressed legacy outcomes. No legacy formula or random draw enters shadow severity. The previous prose said fifteen cases; the actual unchanged JSON sample contains twelve.

## Review-fix calibration comparison

Compared with reviewed HEAD `4cfc3f6595d684d32b17df364b85d8ce9adda78d`, controlled fixture count increases from **31 to 38** solely through F1–F7. All existing physical values and work counts remain equal: 30 frontiers, 33 analyzed pairs and one deferred contact. The seven new schedules add 11 frontiers, 14 analyzed pairs and four deferred contacts, yielding totals of 41/47/5. The complete 4,374-row grid, both severity distributions and all twelve legacy comparison rows remain exactly equal as structured values. The only existing pair-row text change describes causal deferral. There are no physical warnings. The 0.35/0.70/1.05/1.55 thresholds remain explicitly provisional and unchanged. Updated validation and performance evidence accompanies the PR review-fix section.

## Diagnostics and performance

The default is `None`, which bypasses the production analyzer. Explicit `Summary` retains only pair IDs/status/authorization, ratios/labels and rider demand/reserve. `FullAudit` also retains manifolds, vectors, decomposed pair demand and aggregate net impulse. Full geometry is transient during Summary calculation and is not retained in normal results. The optional metadata and options are ignored by the historical JSON serializers, preserving the meaning and bytes of existing captures.

Measured on October 6, 2026, in Release on Windows with 2,000 repeated frozen FullAudit analyses:

| Analyzed pairs | Reviewed HEAD time µs | Fixed time µs | Reviewed allocation bytes | Fixed allocation bytes |
| --- | ---: | ---: | ---: | ---: |
| One | 27.03 | 31.59 | 13,448 | 14,632 |
| Two, squeeze | 40.58 | 18.88 | 20,448 | 21,136 |
| Three, chain | 23.45 | 20.78 | 27,257 | 28,241 |

None returns the cached empty result without enumerating inputs or allocating model objects. On scenario I, seed 7, four laps, interleaved None/Summary runs with two warmups per level followed by three samples, fixed-code median heat time was **4,228.9/4,248.0 ms** and allocation **327,167,520/327,247,144 bytes**. Summary added **79,624 bytes (0.0243%)**. Reviewed-code None/Summary medians were 4,606.4/4,507.1 ms and 327,168,560/327,245,968 bytes. The full .NET suite was running concurrently during these observations; timing variation cannot establish a speedup or regression. The causal helper adds about 0.7–1.2 KB per enabled frozen analysis in these cases. `--performance` reproduces measurements separately so nondeterministic timing does not enter golden JSON. These remain observations, not CI timing gates.

## Reproduction and compatibility

```
dotnet build SpeedwayManager.sln -c Release --warnaserror
dotnet test tests/CoreSim.Tests -c Release --filter FullyQualifiedName~PhysicalContactAnalysisTests
dotnet run --project tools/physical-contact-audit -c Release -- results/physical-contact
dotnet run --project tools/physical-contact-audit -c Release -- results/performance --performance
python tools/rider-compatibility-audit/check-consumers.py
```

CI keeps all nine existing jobs and all four historical comparison commands, adds this test class to the exact cross-platform suite, and compares the new JSON and typed IEEE capture across Windows/Ubuntu. The new comparison also requires the existing #56B report to match its checked-in golden bytes. Focused full-heat tests compare classification, every committed step and raw final surface with None versus FullAudit. The PR records final-head CI and historical artifact comparisons.

#56C2 can use this reviewed continuous model to design applied consequences and recovery time. Rider body/shoulder geometry remains a separate later contact source.
