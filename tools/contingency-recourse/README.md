# Study #151 — finite contingency planning, default OFF

Base: PR #149 HEAD `5bc8575f60d9fa1116e45dad85c08786158faccd`. Physics A/B, engine, tyres, geometry, production defaults, study150 and all historical evidence are immutable. This tools-only experiment requires `Enabled:true` and an explicit CLI flag.

Read [the complete report](../../docs/experiments/contingency-recourse/REPORT.md) and the write-ahead budget before running experiments. Do not rerun a saved attempt name or overwrite its evidence. A failed prerequisite prevents creation of the next native world. The cumulative cap is25 native worlds /1200 process CPU-s, with20 CPU-s reserved for export. Compilation, local tests and CI are outside the experiment ledger.

```powershell
dotnet restore tools/contingency-recourse --locked-mode
dotnet build tools/contingency-recourse -c Release --no-restore
dotnet tools/contingency-recourse/bin/Release/net8.0/contingency-recourse.dll --gate1-v6-151
```

The last command is the recorded detached0.4s experiment and deliberately rejects a duplicate attempt in this checkout. For independent reproduction, use an isolated checkout, preserve the immutable input directories and create a fresh study151 output directory/ledger. Never delete the supplied ledger to continue tuning.

| Flag | Scope |
|---|---|
| `--audit-v2-151` | Exact same-command/same-time predictor/native audit; original0.4s rejection scenarios |
| `--axis-audit151` | Check every eligible saved native pair against the exact straight-motion invariant |
| `--gate1-v6-151` | Detached0.4s, full thirteen-node/nine-reserve certificate |
| `--gate2-v3-a151` | Continuous native first bend from rest; explicit target stop |
| `--gate3-a151` | Continuous first lap, both bends |
| `--gate4-a151` | Three native A laps from rest, only after gate3 |
| `--probes151` | Frozen historical and held-out states; certificates are not native recovery |
| `--gate5-b151` | Three native B laps, only after A and successful held-out probes |
| `--early151` | Earlier throttle and symmetric steering alternatives at0.0/0.2s; saved counterfactuals only |
| `--native-errors151` | New actual native decisions, same-command errors and separate decision contribution |

Older flags preserve the failed engineering attempts. Source snapshots accompany later experiments. The full selected/rejected trees, physical terminal tapes and causal states are in `docs/experiments/contingency-recourse/trees/`. Hash-chained native journals are flushed before any command is returned. Native execution uses the unchanged `DiagnosticWorld149` and all its physical guards.

The finite observation tree uses nominal and signed correlated innovations at two successive observations, then a complete legal reserve at each of nine leaves. The three committed cycles and each reserve cover the0.44s interval before a correction based on the next observation can activate. Every leaf is replayed independently without physics/feedback caches. The long sporting forecast ranks geometry-based arrival times and supplies reserve proposals; its failure triggers another proposal or abstention, not an immediate collision claim. Uncertainty is explicitly finite, not an independently calibrated global box. A model certificate is not a formal native Box2D guarantee.

Saved-only analysis and tests:

```powershell
python tools/contingency-recourse/analyze151.py unique-label
python -m unittest discover -s tests/calibration -p test_contingency151.py
node --test tools/replay-viewer/contingency151.test.mjs
dotnet test tests/CoreSim.Tests -c Release --filter FullyQualifiedName~Contingency151Tests
```

Replay case: `tools/replay-viewer/?case=contingency151`. The default is an actual native attempt; previous failures and first-lap-only results remain selectable. The tree selector exposes every command, observation sign, activation time and terminal reserve. No four-rider validation or real-world safety claim is made.
