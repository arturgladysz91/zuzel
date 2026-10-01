# Executed trajectory: method, audit and compatibility

Base is `83a67616973e5d4fbbc8060525ec5ffc629da734`, main after #52. This document and the new benchmark describe current production; all pre-existing calibration reports retain their original Git blob bytes.

## Audit finding

ResolveRider previously sampled entry lateral geometry/surface, constructed its fixed-radius corner envelope, traversed that distance longitudinally, then called MoveTowards with the resulting time. RiderPosition.Advance received the old entry arc/straight distance and wear received the entry kernel. The #47–#50 trajectory research and #51 audit did not make executed moving geometry the production ground truth. In #51's D control entry-hold and entry-inward shared their first-segment distance, time and exit speed despite different actual endpoints.

## Coupled traversal

Held integer lines (including two-ULP offset round-trip noise) retain the exact existing code path and arithmetic. All advanced moving paths use one immutable ExecutedSegmentPath: canonical progress, elapsed time, physical distance, lateral position/offset, local radius/curvature, surface, speed and local capability at nodes, plus consumed geometry/force/correction/drive/time buckets per step. RiderPosition, elapsed time, endpoint, profiles, calibration observations and wear consume this same result. There is no second telemetry simulator or final full-segment MoveTowards.

Each candidate canonical advance is capped by the existing one-metre longitudinal resolution, apex 0.5, full drive 5/6 and segment end. A scalar time solve calls the unchanged MoveTowards law using local start-step surface, computes physical geometry, samples midpoint surface for existing force/correction primitives, and reevaluates endpoint surface/radius capability. Existing signed midpoint drive, correction profile, preparation boundary, setup and skill formulas determine the speed and time of that same distance. The predictor/corrector has 16 iterations and absolute time residual 1e-7 s; a bracket search has 32 attempts followed by at most 64 bisections. Failure to bracket/converge raises an explicit error. Physical step overshoot and target-arrival splitting also have bounded subdivisions. No RNG or shared mutable cache is involved.

Straight geometry is `ds = sqrt(dx² + dy²)`. Turn geometry is `ds = sqrt((r_mid * dθ)² + dr²)`, with segment-local physical offsets and midpoint radius. Zero lateral displacement delegates to the exact existing arc calculation. Target arrival ends the diagonal portion; subsequent steps hold the target. Stationary launch reaction adds time but no distance or lateral movement. Crash retains half remaining canonical advance, the existing entry-speed/2 event-time abstraction, terminal zero speed and no wear. RunWide retains entry classification and the forced outward target; its actual subsequent geometry is integrated.

Every moving corner node evaluates the unchanged SegmentPhysics.MaxSafeTurnSpeed at actual local radius/surface. ContinuousCornerEnvelope remains the shared fixed-line implementation; its pointwise local continuation forecast is recomputed at actual local position. No mean radius or independent copy of envelope/engine formulas is used. That forecast assumes holding the current line for reachability; predicting future intent is later decision work.

## Surface and wear

Snapshot.SampleSurface interpolates the existing five stored bands' raw Grip/Ruts/Moisture, then recomputes EffectiveGrip. Entry surface still selects outcome and incident risk. Moving traversal samples start/mid/end local surface for its distinct physical calculations. Passage wear integrates the original interpolation kernels over actual step-midpoint locations, weighted by physical distance. It normalizes their sum to the original entry-kernel total budget (1.2 at edge, 1.4 interior), preserving 0.004/0.015 ruts and -0.25 grip multipliers. Integer held-line wear remains exact. There is no new surface map, cell or wear coefficient.

## Exact fixed-line gate

`executed-trajectory-fixed-line-before.json` was collected on the base before implementation. Five Motoarena hold controls use neutral setup, default skills, clean uniform surface, incidents OFF and four laps from rest. SHA-256 covers every original typed change/diagnostic field and all original corner nodes: distance, time, speeds including minimum/peak/apex, outcome, reaction, surfaces and profiles. Current production matches all five hashes and lap times exactly. No tolerance masks changed fixed-line physics. Separate existing longitudinal/standing/envelope controls remain active.

Tests of historical reports now validate archived artifact hashes/provenance independently of current moving production. The checked-in manifest records base blob SHA-256 using canonical LF bytes; Git checkout line-ending conversion is normalized only when verifying text. No historical result was regenerated and no old engine is forked. Current geometric, local surface/capability, conservation, wear, collection-order and incident-OFF seed tests exercise actual production.

## Controlled result and limits

A Release desktop measurement (with the full test suite running concurrently) averaged about 77 ms for the 21 named controls. A four-lap single-rider Motoarena control requesting inner on entry/middle and outer elsewhere completed with 34 moving segments, 1,553 stored nodes / 1,519 steps, about 50 ms, zero solver fallbacks and at most eight predictor/corrector iterations. This is a representative cost observation, not a timing assertion. Four riders of similar movement would require roughly 6,200 path nodes per heat. The hard per-segment node bound is `4 * ceil(outer segment length + usable width) + 16`; local envelope continuation recomputation can cost more than linear work after apex, but uses the existing one-metre primitive and no shared cache.

The [21-control benchmark](single-rider-executed-trajectory.md) reports actual requested/resolved/entry/apex/exit coordinates and corner plus following-straight performance at 19/22/25 m/s. It includes the exact isolated #51 first-segment replay. Inside-hold remains the shortest and fastest combined line in all three groups; geometry/motion consistency alone does not produce an opening-line advantage. No tuning or further search followed that result.

The Motoarena 12/16.6 m width mapping remains segment-local, with no artificial boundary distance and no transition spline. Apex stays at 0.5. Entry-classified outcomes remain the existing coarse model. AdaptiveDecisionModel, contact, occupancy, common-time interaction, combined grip and turning-slip research are unchanged. These boundaries do not block using the internally consistent single-rider evaluator as the ground truth for subsequent trajectory decision work, subject to independent review.
