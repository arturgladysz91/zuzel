# Race Engine readiness backlog

Observation baseline: `c13af7b8a16fe4e58e9d404c849cdf323649b0ed`.
These are proposed future scopes. None is implemented by the audit PR.
Numeric case references and machine-readable finding fields accompany the report.

## Mandatory correctness before enabling the complete interaction model

1. **REA-001 / P1 — reject infeasible safety projections without aborting heats.**
   Ordinary Motoarena `outside`, seed 7, B and C, four real gates, aborts at step 0
   in `SafetyProject → ResolveProduction → ExecutedPathTraversal.Solve`.
   All pre-failure riders remain at zero time/speed/progress; no finish exists.
   Preserve exact reproduction, forward/reverse and observer-free evidence. A failed
   optional projection must receive explicit infeasible handling at the correct
   boundary, while keeping verified frozen motions and the bounded #61 caches.
   Do not increase solve limits, invent substitute motion, adjust constants, add
   tolerances or silently ignore a failure on a genuinely selected execution path.
   Scope is the feasibility/error contract and its scenario regression. Complexity:
   medium, with a focused review of actual-path fallback safety. No dependencies.

2. **REA-009 / P1 — exact canonical handoff for a valid partial segment.**
   `three-squeeze` / `four-close-regain`, seeds 7/19/83, A/B/C, enter through the
   existing BehaviorRiderInput factory at `.1f` progress. Adaptive's production
   horizon aborts before first Commit: `Rider 1 is not in segment 1.`
   Existing float remainder addition gives
   `0.10000000149011612 + 0.8999999761581421 = 0.9999999776482582`,
   leaving the rider in segment 0 while the next horizon requests segment 1.
   Original failing decimal cases remain recorded. Distinct binary-fraction companion
   scenarios investigate racing without concealing the invalid handoff.
   Future scope: exact intended whole-segment boundary ownership and projection/
   execution consistency, with a valid-partial-position regression. Do not round
   inputs or enlarge BoundaryTolerance. Complexity: small/medium; no dependency
   on issue #62, contact calibration or the safety-projection fix.

No P1 is inferred merely from a failed full-bike track certificate, a synthetic
stress crash or a gap against an unmatched professional distribution. Any additional
reproducible blocker in the final matrix is listed in the machine-readable report.

## Mandatory realism/integration decisions before declaring the engine complete

2. **REA-002 / P2 — retire the provisional default consequence boundary only after C is validated.**
   Default A still uses 0.55 m lateral/0.12 s coarse eligibility and addressed random
   occurrence/severity, including ×0.82 speed and +0.20 s lost-rhythm effects.
   These are compatibility rules, not an impulse-derived collision. B adds physical
   eligibility/avoidance but retains the authorized old consequence routine.
   C provides the new structure but remains gated. Choose migration/default policy
   in a separate PR only after REA-001, REA-009 and REA-004; do not enable both models.
   Complexity: small integration change, substantial evidence/review dependency.

3. **REA-006 / P2 — make the canonical ability consumption contract explicit for gameplay.**
   Canonical Reaction/Start are specified independent concepts, while current launch
   uses legacy Start for both. Canonical Technique/Strength/mass/Condition already
   have specific B/C consumers. No generic overall-to-professional conversion exists.
   Decide and test the future migration mapping while preserving current behavior
   until deliberately replaced. Do not mistake this specified, unmigrated feature
   for an existing launch bug; do not add strength/age speed bonuses.
   Complexity: medium. Depends on gameplay rating design and REA-003 cohort evidence.

## Recommended calibration and evidence

4. **REA-003 / P3 — matched cohort speed/time amplitude calibration protocol.**
   Separate Vmax, average speed, first-lap penalty and within-heat spreads. Use the
   exact Motoarena 2026 selector and recorded quantiles, then establish compatible
   geometry/setup/ability populations before attributing discrepancies to an envelope
   or force term. Neutral Skill50 is not the professional mean; no named riders get
   invented ratings. No single realism score, no benchmark-driven constant fitting.
   Complexity: medium/large evidence task. Depends on REA-005 venue interpretation.

5. **REA-004 / P3 — impact severity and recovery calibration.**
   Current reserve factors, 0.35/0.70/1.05/1.55 class boundaries and quadratic
   recoverable loss up to 0.8 are synthetic. One-next-active-step recovery has a
   variable duration in seconds. The existing endpoint abstraction applies first-touch
   impulse at normal segment completion without reintegrating the remainder; review
   whether its measured delay is adequate for realistic racing. This is the accepted
   model contract, not a newly alleged invariant violation. Retain impulse/momentum and ownership contracts;
   gather controlled compatible exposure/outcome evidence and assess duration,
   not just category monotonicity. PGEE supplies no impact impulse or recovery target.
   Stress frequency is not an ordinary crash-rate estimate. Complexity: large evidence
   task, then a separately reviewed calibration PR. Depends on REA-001 for stable runs.

6. **REA-005 / P3 — venue geometry, gates and full-footprint boundary validation.**
   Resolve radius measurement convention, standing split, width transition and local
   asymmetry uncertainty. Pilot Motoarena has three uncertified footprint motions per
   configuration without reference-point edge witnesses. Review full traces before
   claiming an illegal path. The 16.6 m symmetric width and zero-time unswept reference
   transitions are explicit approximations. Complexity: medium/large. Track Engine
   dependencies: measured local geometry, banking/asymmetry and surface observations.

## Optional realism improvements

7. **REA-007 / P4 — extend opponent-aware planning evidence.**
   Current physical inside/outside/cutback/cover/yield and bounded safety responses are
   implemented. Adaptive candidate ranking still forecasts a solo production horizon
   with current occupancy, rather than a complete future opponent strategy. Evaluate
   longer-horizon reattack/defense needs from the recorded complete races before adding
   search or objective complexity. A response selection is not a completed maneuver.
   Complexity: large. Depends on correctness/calibration, measured workload and a
   product-level definition of useful tactical behavior. Preserve physical pass proof.

8. **REA-008 / P4 — telemetry precision for racing intent and exposure duration.**
   Exact native motion and common-time strict order brackets are available; exact
   below-threshold duration and intent-success attribution are not. Improve passive
   attribution/continuous exposure measurement only when it supports a meaningful
   gameplay/review decision. Never count candidate intent or asynchronous snapshots
   as overtakes. Complexity: small/medium. No physics change is required by this audit.

## Track Engine and later gameplay boundaries

Existing deterministic passage wear, weather, grading/watering/packing and route grip
are already used. Better measured local surfaces, venue asymmetry/banking and start
geometry precede stronger empirical racing claims. Race mechanisms must continue
to consume the same immutable snapshot and commit surfaces once.

Manager setup advice, match scheduling, rulesets, team orders, injuries, long-term
fatigue, skill development, league AI and UI belong to later approved gameplay scopes.
Canonical PairRiding is cooperation, not collision dominance. Age does not become a
direct physics multiplier. Do not implement these later features through this backlog.

The independent review should select the next PR. The audit's concrete recommendation
is **“Race Engine — reject infeasible safety projections without aborting heats.”**
After that, reassess the full matrix before any default-flag decision.
