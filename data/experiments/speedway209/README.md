# Study #209 evidence index

The immutable #208 source is commit `e0607f555de2d4c764b4bf26bd9bcafc420db6ec`. The report is [H29-S](../../../docs/experiments/speedway-h29-s/REPORT.md). H25 remains the default.

- `frozen-reproduction/`: original unmodified #208 binary's five-start MODEL reproduction.
- `baseline/`, `effective-audit/`: five full accepted-step #208 MODEL traces and command-local effective counterfactuals.
- `native-clamp-baseline/`: exact clamps reconstructed at all 5,190 original native decision checkpoints. Join each held request with the original #208 native substeps by physical request/activation time.
- `controlled/arms.json.gz`: all 51 A–E proposals and every development outcome, including failures.
- `controlled-held/`: registered five-start A–E checks; B fails robustness and is not promoted.
- `diagnostics/`: 108 six-throttle local/suffix pairs and 30 original-straight physical probes. Technique-disabled straight capability is not a full-race certificate.
- `events/`: paired unchanged and 0.25 s full-throttle suffixes at all 70 >5 percentage-point request drops. Every unchanged suffix finishes.
- `F5/`, `F6/`: all 1,536 proposals per joint search, parent identity, exact failure/solver details, steps, CPU, wall time and declared bounds. F6 also has durable per-batch records. Search minima are suffix minima, not full-race minima.
- `F5-screen/`, `F6-screen/`: 24×7 time-zero development screening results per bend. `F5-screen/robust-root.json` identifies the retained F5 root.
- `F-held/`: five complete races without the predictive final-exit window.
- `frozen-candidate.json`: complete #209 shared policy, SHA-256 `F5604A0F89374D14520078C92D7FEAAD04D488D336652603ED4B87D2CBD5D0DD`.
- `G-provisional/`, `official/`, `official-effective/`: initial fixed-policy five-start check, post-freeze independent recheck, and attribution-only recheck. Their physical trajectories and finish times agree.
- `native/`: five original-world Box2D S3 races, full samples, all measured decisions and independent strict audits.
- `native-clamp-final/`: the current source exactly reconstructs all 4,985 final native commands; no additional worlds.
- `world-ledger.jsonl`: all five new world reservations, required order, maximum eight.
- `physics-seal.json`, `parity-final/`, `embedded/`: frozen source hashes, 72,949 archived and 16,200 synthetic contact comparisons, and compatible embedded policy checks.
- `validation/`: actual local test logs, parent/current comparison and hosted status. Failed/blocked historical tests remain recorded.
- `figures/`: race-time, common-distance six-bend and physical original-straight PNG charts.
- `*-command-clamps.csv`, `phase-restrictions.csv`, `effective-attribution.json`: compact request and phase inventory. Effective removal deltas are at the #208 output and are not additive.

`Tick209.Before` is the actual contact input and carries active drive, ordered pending commands, posture and observer state. `After` is the accepted resulting state. `Power` contains engine, wheel, clutch and H26 work/loss ledgers; `Forces` contains front/rear forces, loads, resistance and yaw moment. Speed is m/s, positions/distances m, angles rad, wheel/shaft angular velocity rad/s, RPM explicitly named, force N, torque N·m, work J, physical time s, decision time ms. `Acceleration` is the finite speed difference; `ForceAcceleration` is force projected onto pre-step velocity. Rejected states appear in `Failure`/`Contact`, not in accepted telemetry.

The optional fast admission runs do not capture commands or forces and are used only for eligibility/finish/failure; their request-throttle summary fields are unmeasured. Full throttle and loss statistics in the report come exclusively from the instrumented accepted-step archives. Search run CPU inside parallel workers overlaps process-wide accounting; use each search's outer `Cpu`, not the sum of worker values, for its budget.

The nominal raw #208 and #209 traces contain actual activation timing. A request not activated before finish has no observed delay. Physical-grid duration and peak metrics include the final accepted crossing step; finish seconds use the unchanged harness interpolation.

## Reproduction

From the repository root, with .NET 8-compatible SDK, use a fresh output directory because evidence writers do not overwrite final files:

```powershell
dotnet restore tools/speedway-h26/H26.csproj --locked-mode
dotnet build tools/speedway-h26/H26.csproj -c Release --no-restore --warnaserror
$env:SPEEDWAY_BOX2D_NO_WORLDS='1'
$exe='tools/speedway-h26/bin/Release/net8.0/H26.dll'
dotnet $exe audit-209 . work/recheck209/baseline
dotnet $exe check-209 . work/recheck209/final data/experiments/speedway209/frozen-candidate.json
dotnet $exe reconstruct-209 . work/recheck209/native-actions
```

`arms-209`, `search-209 ... F5|F6 [upstream.json]`, `screen-209 ... search.json.gz`, `diagnostics-209`, `events-209` and `check-arms-209` are offline MODEL entry points. The source records the finite menus and caps. `native-209` requires all five strict MODEL admissions and an absolute central ledger; **do not reuse the already closed published native evidence directory or its ledger**. A new study must declare and count any additional native worlds rather than treating reproduction as free.

`analyze209.py` uses Matplotlib (this run: 3.11.2) to regenerate standard PNG figures and aggregate metrics. `export209.py` regenerates nominal command-local attribution CSVs. H28-F physics and production defaults are not changed by these tools.
