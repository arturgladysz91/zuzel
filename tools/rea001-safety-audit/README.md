# REA-001 correction verification

The unchanged readiness catalog provides the exact standing-start fixture.
`compare.py` checks original failure versus completed B/C production, exact A
preservation, input reversal, observer parity and complete corrected platform traces.
All comparisons use typed IEEE values or hashes over those complete values.

CI uses new `results/rea001/before-A` through `after-C` directories. Neither the
342-case offline matrix nor frozen historical evidence is rewritten. The two new
enabled cases require identical complete final rider/classification/log/surface
states. C additionally requires strict complete Summary/FullAudit trace equality.
B retains native geometry with physical consequences OFF: its complete diagnostic
divergence map and each OS's exact Summary behavior hash are pinned in the new
`tests/fixtures/rea001-native-b-portability.json`. Main aborted before these 121
double diagnostic leaves became observable; they are new evidence, not a claimed
completed baseline. Every introduced, removed or changed case/path/type/bit pattern
fails. No rounding or tolerance is used. The older 22-case portability map and all
existing strict golden tests remain unchanged. CI uploads the entire observed and
expected new map as `rea001-platform-comparison.json`.

The benchmark runs normal Motoarena C, contact-heavy C and outside/7 B/C, with one
unmeasured warmup and five measured fresh heats. It counts materializations only
in the warmup. Timing/allocation runs have no materialization observer, retain the
cheap work observer, and exclude final hashing. An original failed heat has partial
work rather than a fabricated completed heat. It is not an end-to-end speed baseline.

```powershell
git worktree add --detach ../rea001-baseline 350f59cc53184399066bdf93b18341e4628ec8ef
# Counters only: every exception is rethrown; no baseline outcome changes.
git -C ../rea001-baseline apply --unidiff-zero ../zuzel/tools/rea001-safety-audit/baseline-counters.patch
dotnet build tools/rea001-safety-audit -c Release --warnaserror -p:CoreSimRoot=ABSOLUTE_BASELINE_PATH -o ../rea001-before-bench
dotnet build tools/rea001-safety-audit -c Release --warnaserror -o ../rea001-after-bench
dotnet ../rea001-before-bench/CoreSim.Tests.dll results/rea001-before-performance.json 5 --baseline
dotnet ../rea001-after-bench/CoreSim.Tests.dll results/rea001-after-performance.json 5
```

`report.py BEFORE_B BEFORE_C AFTER_B AFTER_C PERFORMANCE_BEFORE PERFORMANCE_AFTER OUTPUT`
creates a separate focused JSON containing every changed final rider/surface typed
leaf, complete final rider/surface/classification leaves, outcomes/consequences and
performance totals. FinalHash and CI traces also cover the complete log.
Its before side is the real failed pre-Commit state, not an imagined completed race.
