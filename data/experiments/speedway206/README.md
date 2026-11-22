# Study #206 evidence

See the [report](../../../docs/experiments/speedway-h28-f/REPORT.md) for the decision and the numerical derivation. The exact parent is `0b8f817187ffb69b8476e3cc99a2bce055089257`.

| File | Contents |
|---|---|
| [summary.json](summary.json) | Frozen #205 cross-check, totals, per-replay percentile and cost breakdown, timing correlations and locked five-start results. |
| [profiling/](profiling/) | Eight serial replays of the archived review states. Detailed files also have per-decision guard/worker counters and every observed >50 ms state. All use the old permissive profile to hold behaviour fixed. |
| [parity.json](parity.json) | Every accepted step from 21 named native archives #202–#205, exact comparison, H28-C/F counters and focused historical states. |
| [strict-style.json.gz](strict-style.json.gz) | Original yaw and technique violations, both thresholds and their timing. Causal predecessor checkpoints and all five frozen-tape predictions under both profiles. |
| [controlled-search-rounds1-6.json.gz](controlled-search-rounds1-6.json.gz) | Initial attempts and each first-round locked finalist's predictions. |
| [controlled-search.json.gz](controlled-search.json.gz) | All 441 nominal attempts, their edits and failures, five locked tapes and 25 predictions. |
| [final-policy-recheck.json.gz](final-policy-recheck.json.gz) | Repeated strict predictions of those 25 frozen combinations using the final solver. |
| [world-ledger.jsonl](world-ledger.jsonl) | Central append-only ledger in JSON Lines format. The zero-byte file records zero reservations. |
| [validation.json](validation.json) and [manifest.json](manifest.json) | Local suite counts and exact failing case IDs, plus SHA-256 hashes over the Git blob bytes. |

The baseline archives remain in [#202](../speedway202/), [#203](../speedway203/), [#204](../speedway204/) and [#205](../speedway205/).

## Reproduction on Windows

Run from the repository root. Use a fresh folder for each writing command because the evidence writer refuses to overwrite previous runs. The example replays no worlds. Compare the decision benchmarks serially on an otherwise idle host. The optional final argument `detail` enables per-decision counters.

```powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS = '1'
dotnet build tools/speedway-h26/H26.csproj -c Release --warnaserror
$h28 = 'tools/speedway-h26/bin/Release/net8.0/H26.dll'
$out206 = Join-Path ([IO.Path]::GetTempPath()) ('speedway206-' + [guid]::NewGuid())
dotnet $h28 parity-206 . "$out206/parity"
dotnet $h28 style-206 . "$out206/style"
dotnet $h28 decisions-206 . "$out206/C-fast" C H28-F
dotnet $h28 decisions-206 . "$out206/C-baseline" C H28-C detail
dotnet $h28 search-206 . "$out206/initial" 6
dotnet $h28 search-206 . "$out206/extended" 4 "$out206/initial/controlled-search.json.gz"
dotnet $h28 recheck-206 . "$out206/recheck" "$out206/extended/controlled-search.json.gz"
```

The `native-206` entry point requires a frozen eligible candidate and recomputes all five strict preflights before reserving a slot. It uses the shared absolute `SPEEDWAY_BOX2D_LEDGER`, with a global maximum of eight reservations. This study found no eligible candidate.
