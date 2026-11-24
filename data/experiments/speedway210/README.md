# Study #210 evidence

The sporting parent is commit `3f668b43a5ead239133a52f5c9a0149d24f5c1ad`. Its frozen #209 controller and five native recordings are the original-start comparison. [Study report](../../../docs/experiments/speedway-h29-p/REPORT.md).

| Path | Contents |
|---|---|
| `reference/` | Exact #209 policy hash, five native re-audits from archived substeps, reconstructed decisions and temporal technique, and a complete nominal MODEL trace. |
| `search/A` through `search/E` | All proposals, exact prefixes and pending state, strict suffix outcomes including first rejections, budget counters and time-zero development screens. |
| `search/F`, `search/F-reserve`, `search/repair` | Predictive recovery menu, observed MODEL decision profiles, seven-start screens, extra selection reserve trial and the causal repair after the first native DNF. |
| `arm-traces/` | Full nominal MODEL trajectories for arms B through the initial F winner. The final frozen policy has its own trace in `official/`. |
| `frozen-candidate.json`, `freeze.json` | Final policy and source selection made before the four nonnominal official starts were opened. |
| `official/` | Five original-start MODEL traces and admission hash. |
| `fresh/` | Four additional independently held MODEL starts at ±9 and ±20 cm. |
| `first-freeze/` | First frozen policy, all five original MODEL preflights and its stopped nominal native run. |
| `native/`, `world-ledger.jsonl` | Original-world Box2D samples, applied and pending controls, clearance, physical work, strict audit and every decision time. The ledger counts each new construction. |
| `probes/` | Matched-state throttle interventions and guard horizons, through-next-bend outcomes, physical straight diagnostics and selected-policy ablations. |
| `race-metrics.json`, `bend-metrics.csv`, `figures/` | Original-step analysis, complete-race speed intervals with physical distance, split times, clearance and MODEL-to-native divergence. |
| `validation/` | Physical parity, source hashes, parent/current validation results and logs. |

In MODEL traces, `Before` holds the accepted contact input, active action, pending queue, wheel, shaft, steering, posture and technique history. `After` holds the accepted output. `Command` is the most recent selected request, which can differ from `Before.Active`. `Power` and `Forces` carry the actual step ledgers and forces. Rejected trajectories record their last legal progress and first failing state separately from accepted telemetry. Search roots may share only the portion before their first editable pulse. All screened and official races begin independently at time zero.

The replay stores positions and distances in metres, speed in m/s unless the field names `Kph`, times in seconds unless the field names `Milliseconds`, angles in radians, forces in N and work in J. Reference distance and actual travelled distance are distinct. The analysis linearly interpolates crossings and speed thresholds between accepted samples and clips intervals at the unchanged interpolated finish. Guard and diagnostic acceptance does not guarantee a strict completed race.

From the repository root, the read-only MODEL operations use the existing .NET 8 study tool. Choose fresh output paths. The physical parity command and `admit-210` construct no native world.

```powershell
dotnet restore tools/speedway-h26/H26.csproj --locked-mode
dotnet build tools/speedway-h26/H26.csproj -c Release --no-restore --warnaserror
$env:SPEEDWAY_BOX2D_NO_WORLDS='1'
$runner='tools/speedway-h26/bin/Release/net8.0/H26.dll'
$candidate='data/experiments/speedway210/frozen-candidate.json'
dotnet $runner freeze-reference-210 . work/recheck210/reference
dotnet $runner parity-206 . work/recheck210/parity
dotnet $runner admit-210 . work/recheck210/official $candidate
dotnet $runner probes-210 . work/recheck210/probes $candidate
```

`search-210 ... A|B|C|D` takes one arm. `search-210 ... E <component-output-parent>` uses the B, C and D results. `recovery-210` and `reserve-210` take the selected E policy. `repair-210` takes the first frozen native DNF archive and the reserve search archive. `fresh-210` takes the refrozen policy. To recover an individual trial's accepted-step trajectory, export its `Policy` object to JSON and run `trace-210 <workspace> <output> <policy.json> <fixture>` with its recorded fixture. `native-210` takes the frozen candidate and official admission file. A new native study needs its own predeclared construction budget and central ledger. The published ledger counts both original and repaired runs. `analyze210.py` reads these artifacts and regenerates the numeric summaries and optional static figure.
