# Study #211 evidence

[H30-C report](../../../docs/experiments/speedway-h30-c/REPORT.md). The parent and H28-F equations are identified in `protocol.json`. H25 remains the default.

| Evidence | Contents |
| --- | --- |
| `root/` | Exact rejected #210 guard query, its nearby native reviews, original interval certificates, control perturbations and independent Decimal enclosure for the rejected box. |
| `reference/` | Independent #209 and #210 native archive re-audits, frozen nominal MODEL trace and five-metre transition audit. |
| `search/` | Bounded trial policies, exact checkpoint lineage, stop states, verified entry and stabilization states as available, CPU and proposal budgets, reserve candidates and receding guard checks. |
| `entry/` | Five joint entry-speed surveys and identical-state short, medium, next-bend and full-race probes. |
| `terminal-envelopes.json` | Sample ranges and complete mechanical and technique state for legal incoming-corner witnesses. Independent combinations of their endpoints remain untested. |
| `freeze.json`, `frozen-candidate.json`, `protocol.json` | Exact policy, input configuration and selection boundary. |
| `official/` | Five regression starts, four fresh MODEL holdouts, two queued-request disturbances, their complete accepted traces, and native admission checks. |
| `native/`, `world-ledger.jsonl` | The native series, ordered construction reservations, every decision time and strict observer results. |
| `validation/` | Release, regression, parity, source seal and hosted status. |

Positions are reference metres unless recorded as physical distance. Sample speeds are m/s unless the field says `Kph`. The reported threshold intervals interpolate the accepted samples and clip the actual finish. Strict swept is 0.50 m. Reserve thresholds are selection metrics.

To reproduce using .NET 8-compatible Release tools from the repository root, use fresh output directories. The following commands construct no native worlds.

```powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS='1'
$exe='tools/speedway-h26/bin/Release/net8.0/H26.dll'
$candidate='data/experiments/speedway211/frozen-candidate.json'
dotnet $exe freeze-211 . work/recheck211/reference
dotnet $exe root-211 . work/recheck211/root
python tools/speedway-h26/root_precision211.py work/recheck211/root/root-audit.json.gz work/recheck211/root/decimal.json
dotnet $exe admit-211 . work/recheck211/official $candidate
dotnet $exe entries-211 . work/recheck211/entry $candidate
dotnet $exe parity-206 . work/recheck211/parity
```

`search-211` takes an arm and optional component folder, `select-211` takes a search output folder and `native-211` requires both the frozen candidate path and its valid admission receipt. New native work requires a fresh, declared ledger. The supplied ledger records Study #211's actual attempts. `analyze211.py` produces the summary tables and optional static charts from the recorded evidence.
