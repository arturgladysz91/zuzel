# PR #59: deterministic contact decision geometry

Status: first P1 correction from review `5452627332`; Draft, independent review required.

## Integrated baseline and arithmetic contract

Main `de905e96990f8ab80e98334682c3d2aafe39e9b0` (squashed #61) was merged into the existing
`physics/physical-contact-consequences` branch at `96f8f1bb05c8bbe2a35c992b6e92a0025c10427e`.
Before the P1 correction, the Release warnings-as-errors build, all 2,109 .NET tests and
37 Python tests passed. The merged engine reproduced the existing #56C2 report exactly.

The integration retains prepared metric segments and capsule vectors, motion-to-pose caches,
verified history reuse, and identical production-resolution reuse. A nonempty consequence plan
still requires production materialization and fresh executed verification. An empty enabled plan
can reuse the already verified production result. Pair consumption and recovery selection remain
the implementations reviewed in #59; their separate review findings are outstanding.

`ContactFrameArithmetic.Direction` and `Heading` remain the only enabled angle kernel.
Prepared straight tangent, outward vector, velocity and travel heading honor the segment mode.
Prepared corner centre, sampled tangent, velocity and heading honor that same mode. Clearance now
derives outward as `(forward.Y, -forward.X)` and reuses the exact deterministic corner centre
computed during embedding construction. The cache is private to that immutable embedding;
native embeddings allocate no centre cache. Tactical geometry obtains its interpolated direction
from the poses' mode and rejects incompatible pose/event frames or mixed modes. Linear intervals
reject a change of mode between endpoints; trimmed history forwards the source mode. A tracker
cannot switch mode even before its first retained interval.

All OFF native expressions keep their original operation order, including the native
`(sin(tangent), -cos(tangent))` clearance axis and `(-sin(entry), cos(entry))` corner centre.
Track widths, boundaries, motorcycle dimensions, radial bulge, recursive certification/depth 12,
fail-closed zero clearance, tactical weights/thresholds, detector/search resolution, severity,
impulse, reserve, safety policy and recovery equations are unchanged. No output rounding or
comparison tolerance was added. The kernel's existing quadrant reduction is unchanged.

## Native trigonometry audit

Search: `rg -n 'Math(F)?\.(Sin|Cos|Atan2)\(' src/CoreSim`.
Each remaining call belongs to A (OFF only) or B (independent offline calculation).
There are no remaining C calls in the enabled production contact decision graph.

| Remaining location / calls | Class | Reason |
| --- | --- | --- |
| `ContactFrameArithmetic.Direction`: `Math.Cos`, `Math.Sin` | A | Explicit `!deterministic` branch. |
| `ContactFrameArithmetic.Heading`: `Math.Atan2` | A | Explicit `!deterministic` branch. |
| `PreparedMetricTrackSegment` entry-left: `Math.Sin`, `Math.Cos` | A | Native `else` branch; enabled centre uses `Direction`. |
| `WithinTrack.Clearance` outward: `Math.Sin`, `Math.Cos` | A | Native embedding branch only. |
| `WithinTrack.Clearance` entry-left: `Math.Sin`, `Math.Cos` | A | Native arm of the centre selection only. |
| `Analysis/PhysicalSpaceEvidence` synthetic pose travel: `Math.Atan2` | B | Offline injected fixture construction; no production caller. Frozen #55 evidence remains unchanged. |
| `Analysis/PhysicalContactEvidence` synthetic pose travel: `Math.Atan2` | B | Offline injected fixture construction; no production caller. Frozen #56C1 evidence remains unchanged. |
| `Analysis/FreeContinuousRacingTrajectoryGeometryExperiment` Cartesian mapper: `Math.Cos`, `Math.Sin`, two `Math.Atan2` | B | Offline free-trajectory experiment, outside the production traversal/contact graph. |
| Same experiment's low-phase seed: `Math.Sin`; inside/outside seed families: two `MathF.Sin` | B | Offline search initialization only; no contact decision consumer. |

The reachable #55 footprint/separation and attribution paths, #56B candidate/clearance/tactical
paths, #56C1 manifold/normal/travel/grip projection, and #56C2 first-touch projection were inspected.
They use the propagated mode or the already constructed deterministic pose/footprint. The offline
experiment's report and partial experiment classes are its consumers, rather than the production
`SimulationEngine`, `ProductionTraversalService` or interaction coordinator.

## Exact evidence and changed behavior

`tools/contact-decision-audit` builds the **same** harness and shared boundary fixtures against
the merged pre-fix assembly and the corrected assembly. Enabled captures retain complete snapshots,
motions, physics diagnostics, candidate costs/rejections/responses, selected responses, physical
shadow analysis, applied plans, events, final riders, classification, logs, raw surface cells and
all work counters. IEEE leaves retain `float`/`double` tags and exact bits; enum, string, boolean,
integer, decimal, character and null leaves retain their types. Gzip is lossless storage, not a
numeric normalization. Each manifest hashes the complete uncompressed canonical leaf map.

The matrix includes 432 stationary edge fixtures (two turn angles, straight/entry/middle/exit,
start/middle/end, three orientations, both edges, zero and ±1e-8 margins), 218 tactical fixtures
(side-by-side, near-zero longitudinal/closing values, footprint-overlap boundaries, opposite sides,
both roles and basis directions), production squeeze/bridge/disjoint/safety/continuous-overlap
controls, and complete four-lap G, I and contact-heavy heats. Heat seeds are 7/19/83, Dry/LightRain,
in both rider orders. Capture heat ID 59 matches the original #56C2 calibration. Focused production
pose tests additionally cover grip .55/1/1.25 with both weather states, prepared-versus-unprepared
mode-specific IEEE values, and trimmed interval propagation. Boundary/oracle, tactical direction,
frame/mode, and heat-mode regressions fail against the pre-fix implementation and pass after it.

The original 718-case subset (seeds 7/19) changes 848 leaves in 146 cases. Sixteen zero-margin
clearance certificates change: eight pass-to-fail and eight fail-to-pass, according to the exact
deterministic certificate. Nonzero ±1e-8 margins retain their expected decisions. Two exact-zero
longitudinal tactical fixtures at heading -2.9 change Defender to Neutral for one rider. Other
changes are exact tactical projections and time-to-conflict, candidate costs and alternative
enumeration. In I / Dry / seed 19 / step 14, the available Hold/ContinueOutside alternatives change
at the tactical boundary, and narrow-phase evaluations decrease from 193,093 to 179,069. The
selected responses, physical consequences and race state are unchanged in that seeds-7/19 subset.

The expanded matrix contains **730 cases, 6,871,994 leaves and 5,245,706 IEEE leaves** after the
correction, with exact rider-order equality. Its Windows before/after map changes **64,182 leaves
in 154 cases**; most additional changes are subsequent race evolution in G / seed 83 / LightRain.
The complete [losslessly compressed field map](contact-decision-geometry-changes.json.gz), the
[seeds-7/19 subset](contact-decision-geometry-seeds-7-19-changes.json), and the
[91 changed selected-response fields for G/83/Rain](contact-decision-selected-response-changes.json)
retain every path, type and bit pattern. CI also publishes separate full Windows and Ubuntu maps.

The first selected-response change in G/83/Rain is step 21 (lap index 2, segment 5). The projected
outward separation changes sign from double `BCC66E29C0000000` to `3CA660AD00000000`, crossing the
existing exact tactical-side gate. Candidate feasibility changes for combinations 1/3/4/7.
Rider 1 changes ContinueOutside `(1,1,1)` to KeepIntent `(1,0,0)`; rider 3 changes Hold `(0,0,0)` to
ContinueOutside `(1,1,1)`. Later selections and race states diverge from these corrected gates.
No applied contact occurs in this heat. This account and the full maps are recorded **before**
replacing the enabled #56C2 golden. The separate
[consequence report diff](contact-decision-consequence-changes.json) records **every** changed field,
including the eight exact float patterns: only G / seed 83 / LightRain (`Heats[55]`) changes,
from finishing order 3/4/2/1 to 1/3/4/2, with the four times and distances recorded exactly.
All riders still finish four laps; this heat has zero verified contacts and zero applied outcomes
before and after. Applied classes, impulses, recovery, aggregate counts and the other 95 enabled
heat reports remain unchanged. The report change follows corrected avoidance decisions, rather
than retuning consequence physics. The older #55/#56B/#56C1 goldens are preserved.

## OFF compatibility and numerical portability

The existing #61 comparison remains strict against original main
`5989301192565f6a265d53a2db12c7d00c94fedc`, separately on Windows and Ubuntu, across all 108
complete behavior captures. #59's eight new nullable state/diagnostic members are materialized
as null **only when absent in the old assembly**. Present current values are always captured;
any non-null new OFF state fails the original-main comparison. Every historical leaf remains.

The complete original Windows/Ubuntu divergence map must remain identical after this correction,
including case, field path, type and IEEE patterns. The 22 existing native-mode cases belong to
[issue #62](https://github.com/arturgladysz91/zuzel/issues/62). This task neither repairs those cases
nor weakens their map-invariance check. Enabled captures instead require exact equality between
Windows and Ubuntu. No enabled platform divergence is allowed. All older strict platform
comparators remain enabled; the #56C2 golden changes only by the explicitly documented report diff.

## Performance protocol

Before and after use the same harness, Windows machine, .NET 8.0.26 X64 runtime, Release build,
Summary interaction diagnostics, no logging, and no retained FullAudit payload. Each case runs at
least eight complete warmups and at least three seconds, followed by nine samples. Allocation
counts include all threads. No other test/capture workloads run during timing. Full I uses seed 7
Dry and contact-heavy uses seed 19 LightRain; timing heat ID is 57 in both assemblies. OFF timing
keeps #56B enabled and switches only #56C2 off. Step scenarios reset their snapshot/tracker each run.
Raw samples retain wall/CPU milliseconds, allocated bytes, GC counts and every interaction-work
counter. Comparison is against merged pre-P1 `96f8f1b`, rather than the older unoptimized #59.

Raw samples and full counters are in [contact-decision-performance.json](contact-decision-performance.json).
Both environments match. Medians:

| Case | Before wall ms | After wall ms | Change | Before / after CPU ms | Before / after allocated bytes |
| --- | ---: | ---: | ---: | ---: | ---: |
| four-separated | 0.5883 | 0.5798 | -1.44% | 0.000 / 0.000 | 179,080 / 179,256 |
| H-three-squeeze | 77.1776 | 79.1717 | +2.58% | 78.125 / 78.125 | 5,915,144 / 5,915,320 |
| real-bridge | 383.3552 | 387.4968 | +1.08% | 390.625 / 390.625 | 4,321,120 / 4,321,296 |
| I-heat-ON | 6162.8666 | 6221.0149 | +0.94% | 7062.500 / 7046.875 | 273,197,168 / 273,208,456 |
| I-heat-OFF | 3785.5103 | 3835.8830 | +1.33% | 4609.375 / 4718.750 | 244,590,768 / 244,595,296 |
| contact-heavy-heat-ON | 465.6530 | 467.5795 | +0.41% | 875.000 / 859.375 | 77,854,528 / 77,841,176 |

Every listed counter is identical before/after in all 18 samples per case:

| Case | Narrow phase | Production resolutions | Unique projections | Pair checks |
| --- | ---: | ---: | ---: | ---: |
| four-separated | 264 | 1 | 0 | 0 |
| H-three-squeeze | 89,624 | 60 | 14 | 35 |
| real-bridge | 533,242 | 40 | 17 | 63 |
| I-heat-ON | 7,226,535 | 916 | 228 | 569 |
| I-heat-OFF | 4,330,040 | 668 | 165 | 427 |
| contact-heavy-heat-ON | 308,272 | 99 | 20 | 78 |

The largest observed wall increase is 2.58% (dense squeeze); no hot scenario exceeds 10%.
Allocation changes range from -0.017% to +0.098%. Windows process CPU accounting is coarse
for the shortest cases. These are observational measurements, not tolerance-based CI gates.
Exact correctness and the existing work-reuse invariants remain acceptance conditions.

## Reproduction and final checks

```text
dotnet build SpeedwayManager.sln -c Release --warnaserror
dotnet test tests/CoreSim.Tests -c Release --no-build
python -m unittest discover -s tests/calibration -p "test_*.py"
dotnet build tools/contact-decision-audit -c Release -p:CoreSimRoot=BASELINE -o BEFORE_BIN
dotnet build tools/contact-decision-audit -c Release -o AFTER_BIN
dotnet BEFORE_BIN/CoreSim.Tests.dll capture BEFORE_DIR/before.json
dotnet AFTER_BIN/CoreSim.Tests.dll capture AFTER_DIR/after.json
python tools/contact-decision-audit/compare.py --changes BEFORE_DIR AFTER_DIR changes.json.gz
dotnet BEFORE_BIN/CoreSim.Tests.dll measure performance-before.json
dotnet AFTER_BIN/CoreSim.Tests.dll measure performance-after.json
```

CI retains nine jobs. Both OS jobs execute focused geometry regressions, all historical captures,
the exact per-OS original-main comparison, and complete pre/post-P1 enabled captures. The comparison
job validates all raw hashes/coverage, requires rider-order equality and rejects any changed
enabled type/value/bit across platforms. It publishes complete per-OS correction maps and platform
maps even on failure. Python tests explicitly reject new enabled divergences and corrupted sidecars.
Final-head CI and exact commit are recorded in the PR after completion. Keep the PR Draft and stop
for independent review; same-pair recontact and sequential recovery findings remain separate work.
