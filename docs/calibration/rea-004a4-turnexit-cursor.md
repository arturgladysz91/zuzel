# REA-004A4 — TurnExit production cursor

Status: **internal experimental execution**, based on Draft #72 at `0d2653f`.
General D and the ordinary A/B/C callers remain unchanged. No physics expression,
coefficient, calibration value or accepted historical evidence changes.

## Implemented execution and retained state

The existing `RiderIntegrationCursor` now admits rolling **moving TurnExit**
through `ResolveMovingRider` and the same `ExecutedPathTraversal` loop. The scope
is rich capture, an `Ok` resolution, ordinary full drive/lateral authority, without
hold, interaction targets or contact recovery. Fixed-line traversal and other
segment types still refuse. Straight admission and execution remain as in A3.

TurnExit uses the original incident options and stateless RNG addresses at entry,
once. This small admission change is necessary to execute the real C witness with
its actual options, rather than substitute an incident-disabled experiment.
Only a classified `Ok` result proceeds; applied incidents/other outcomes refuse.
Resume never repeats entry classification or incident sampling.

The A3 double progress/end, time/distance, float speed/lateral/surface, accepted
step/subdivision counts, immutable nodes/steps, snapshot and selected controls are
retained. A new `ExecutedCornerBoundary` also retains:

- The original initial envelope speed, not an envelope recomputed at resume.
- The observed apex lateral position (possibly null when this segment starts
  after apex) and exact current corner progress. Original phase/topology, geometry,
  setup and surface inputs remain in the captured production context.
- The existing local scalar-query and envelope dictionaries, including each
  envelope object's already integrated post-apex metre prefixes.

Caches transfer exclusive execution ownership with the consumed token. They are
opaque reusable computations, not immutable historical diagnostics; their contents
can grow after resume. Boundary numeric values and executed prefixes stay immutable.
This avoids replaying envelope integration as well as the rider prefix. A pause
adds no cache snapshot/reintegration; it retains references. Single-use tokens and
the exact immutable surface-version check remain required.

Correction/carry/drive buckets and their sampled surfaces remain the original
accepted `ExecutedPathStep` values. No correction solver is left unfinished at a
pause: the next iteration depends on retained speed, lateral position, canonical
progress, original resolution and cached envelope inputs. The numerical equations,
step splitting, time root and final production materialization are unchanged.

## Actual control/19/C boundary

The test runs the current real heat through observation of step 8 before commit.
In step 7, rider 1 executes **segment 7 / TurnExit / logical corner 2**, starting
at corner progress 2/3, with `Ok`, original independent `E2-A2-X2` intent, no
drive reduction, hold, physical target, recovery or applied contact consequence.
His rich diagnostics contain an actual `ExecutedPath`, not a fixed-line profile.
Solo execution with that captured intent, snapshot and original C options exactly
matches his actual step-7 change, path, diagnostics and motion.

The new real token pauses after **24 accepted numerical steps**, before step 25:

| Value | Actual production execution |
| --- | --- |
| Common execution time after step 24 | **13.16668989157769 s** |
| Certified original first touch, pair 1–2 | **13.194772190598087 s** |
| Common execution time after full step 25 | **13.21535040820075 s** |
| Time from step 24 to touch | **0.028082299020397272 s** |
| Duration of the bracketed full step | **0.0486605166230607 s** |
| Local canonical progress after step 24 | **0.9145583394510511** |
| Executed segment distance after step 24 | **23.83666032552719 m** |
| Speed / lateral position | **20.514252 m/s / 0.5503331** |

Execution clock values above use the retained double accumulator plus the captured
float heat-clock origin. Motion observation nodes expose float local times as in
the existing model. The contact timestamp is unchanged and remains strictly inside
both intervals. Step 25 is executed only speculatively in the parity test; the
common-time guard still forbids publishing its unresolved contact or segment end.
This is not partial-prefix publication or a repaired causal heat.

## Validation and remaining step

Targeted tests cover exact continuous/resumed results and committed rider/wear/log
state, two pauses, correction plus drive, retained envelope work, an apex inside
a single-segment logical TurnExit fixture, surface-version refusal, publication
once and reversed input/resume order. Materialization and envelope-work counters
match exactly; accepted rider/envelope prefixes are not replayed. Straight and
the existing common-time regression remain covered. The source audit adds an exact
A4 inverse while retaining every older fixture and all 39 legacy consumer hashes.
No wall-clock benchmark or 342-case matrix is needed for this scope.

The next minimal contract must execute an **accepted partial step** from this
boundary to a contact root within step 25. It must evaluate the same coupled
geometry/surface/longitudinal equations at trial endpoints, retain an actual
accepted endpoint and define the remainder's numerical partition. Sampling or
scaling the stored full-step result cannot provide that state. All riders must
eventually execute to the common event, apply existing consequences once, invalidate
affected forecasts and recertify. That contract and contact handling are not in A4.
