# Windows CI

The game ships only on Windows. This policy supersedes the Windows/Ubuntu CI
descriptions in older calibration reports; those reports remain frozen historical
evidence. No physics, test expectations, seeds or simulation options change.

## Responsibilities

| Job / mode | Responsibility |
| --- | --- |
| `coverage` (every update) | Warnings-as-errors Release build, unchanged consumer guard, exhaustive test discovery and exactly-one-shard coverage; publish the Windows binaries once. |
| `dotnet (core)` (every update) | All domain, physics, collision, contact, recovery, raw surface and basic regression tests. |
| `dotnet (trajectory)` (every update) | All trajectory, exact fingerprints, Full/Lean, cold/cache, serial/parallel, rider-order and canonical handoff tests. |
| `dotnet (historical-analysis)` (full) | Complete existing calibration integrations and frozen evidence/provenance guards. |
| `dotnet (four-rider)` (full) | Complete 48-case suite, shared 352-run aggregate, all skill controls, real repeats, reversal, culture and observer parity. |
| `python` (every update) | All calibration/comparator tests and CI routing regression tests. |
| `historical-audits` (audit mode) | Current typed captures, unchanged Windows goldens and shape/storm/order guards, pinned-commit contested/contact/readiness/REA-001/REA-009 comparisons, plus fresh-process Windows trajectory replay. |
| `Draft Windows CI` / `Full Windows CI` | Verify all selected jobs succeeded; fail on missing, failed, cancelled or unexpectedly skipped work and on stale PR head/base/status. |

All jobs run on Windows. There is no Ubuntu simulation, mandatory cross-OS bit
comparison, platform-comparison job or platform artifact. Historical comparison
tools and their Python contract tests remain available; their cross-OS entrypoints
are no longer invoked by CI. Windows-only assertions formerly hidden in the
platform comparison are retained by the small `check-windows-captures.py` adapter.

Draft PR updates run the **entire** `core` and `trajectory` shards. Ready PRs and
`main` updates run **all four** existing shards exactly once. Nothing is removed
from test discovery. Every executed shard retains TRX completeness checks and the
top-20 duration profile. There is no test result cache.

## Historical audits

Use Run workflow with `historical_audits=true`, or apply the
`ci:historical-audit` PR label (including on a Draft). The same audit mode runs
weekly on Monday at 02:17 UTC. Workflow dispatch and the schedule become available
from the default branch after this workflow change is merged.

A ready PR changing `src/`, `tests/`, `data/`, `docs/calibration/`, non-CI `tools/`,
project/solution/build files, `global.json` or NuGet configuration automatically
requires historical audits in the same run before `Full Windows CI` can pass.
Paths come from the complete Git merge-base diff, including deletions; no label
can opt a risky change out. CI orchestration and ordinary documentation changes
use normal full validation. Main always gets the full suite; costly pinned-commit
audits already required on the originating risky PR are not repeated on every
push to main. Direct engine pushes therefore require the normal PR process.

## Merge gate and status transitions

`opened`, `synchronize`, `reopened`, `ready_for_review`, `converted_to_draft` and
`edited` events all trigger CI. `labeled`/`unlabeled` support the explicit audit
mode. One concurrency group per PR cancels obsolete runs, including Draft/Ready
transitions at the same SHA. The final job checks live PR head, base and mode.
A fast Draft run emits **only `Draft Windows CI`**, never a skipped or green
`Full Windows CI` check. Marking it Ready starts full validation even without
a new commit. Reopening, retargeting or updating a ready PR also starts full CI.

Repository inspection on 2026-10-11 found `main.protected=false`, status-check
enforcement `off`, zero required contexts/checks and no rulesets. There are
therefore no legacy required Ubuntu check names to preserve or migrate.
**Before using this CI as an enforced merge gate**, protect `main`, require PRs,
require **`Full Windows CI` from GitHub Actions**, and disallow bypass of those
requirements. Keep strict/up-to-date checks enabled so the current main base is
validated. A workflow alone cannot enforce merge restrictions on an unprotected
branch. The connected GitHub integration cannot write administration settings.

## Compilation, caching and four-rider cost

The solution is restored/built once in `coverage`. Test jobs run the published
test assembly through `dotnet vstest`, requiring neither another restore nor
MSBuild-generated absolute package paths. The Sandbox binary is also reused by
audits. NuGet's package cache uses Windows/.NET 8 plus dependency/build-input
hashes and is followed by a real restore. Current readiness probes are built once
and reused by the later REA-001 and REA-009 captures. Pinned-baseline tool builds
remain isolated.

CI #218's four-rider profile attributes 859.415 seconds to initialization of the
already shared aggregate and 122.858 seconds to the other reader/replay work.
Splitting its readers across processes would regenerate the aggregate; removing
seeds/replays or changing the production audit would weaken coverage. The minimum
change keeps its existing shard and collection. Only measured runner/build gains
are claimed, never a simulated four-rider speedup.

## Measurement

Baseline [CI #218](https://github.com/arturgladysz91/zuzel/actions/runs/38002273706)
completed successfully in **27m05s** (run start to completion). Job wall times:
Windows determinism 23m52s; Ubuntu determinism 20m35s; four-rider 17m11s;
historical-analysis 8m01s; platform comparison 3m06s. This run was PR #66 and
includes its extra prototype tests, so the unchanged-production control is
[main CI #217](https://github.com/arturgladysz91/zuzel/actions/runs/37997893652).
Summed TRX time is not workflow wall time and must not be reported as such.

The PR records the new Draft, Ready and explicit-audit run links, exact SHA,
complete test counts, wall time and total job minutes after GitHub completes
them. Queue/setup time is included. No estimated timing is presented as a result.
