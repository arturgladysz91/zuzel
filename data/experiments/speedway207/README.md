# Study #207 evidence

[Published report](../../../docs/experiments/speedway-h29-r/REPORT.md) · [Machine-readable summary](summary.json) · [SHA-256 manifest](manifest.json)

- `causal-and-phases.json.gz` contains the twelve conditional transition tests, three archived trajectory audits, seven causal observation windows and 728 paired interventions. Interventions starting after an older strict fault are retained as counterfactual data.
- `search-{pilot,greedy,beam,recovery,pace}.json.gz` contain all sampled branches, strict reasons for rejection, exact origin and accepted checkpoints, distinct parent IDs, CPU and evaluation caps, training counters, and initial locked results. Follow a lineage by matching a branch’s `Parent` to another branch’s `Id`. Both recovery arms start from `search-beam.json.gz` and identify their reverified prefixes in `Restored`.
- `locked-{pilot,greedy,beam,recovery,pace}.json.gz` contain fresh time-zero replays of the original shortlist on the five prescribed starts. Ranking uses the minimum of `LastLegalS`, number of strict finishes, the minimum of `LegalSweep` and then finish time. `End` includes the first rejected substep on failure. The recheck selected the same policy in every arm.
- `selected-replays.json.gz` contains the selected feedback parameters and every accepted and terminal substep on each start, including control requests, queued activations, forces, losses, loads, speeds, clearance and the technique checkpoint.
- `model-guard-timing-{concurrent,isolated}.json.gz` record the frozen controller and strict 0.25 s receding guard on the nominal MODEL prefix. Timings and certificates are per decision.
- `parity.json`, `published-policies-check.json` and `validation.json` give local verification and historical parity. `world-ledger.jsonl` is the empty central construction ledger for this study.

## Reproduction

Build the Release study tool and set `SPEEDWAY_BOX2D_NO_WORLDS=1` for all MODEL commands. Examples run from the repository root. Each search arm needs its own fresh output directory. The rewind paths point to the main beam’s output.

~~~powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS = '1'
dotnet build tools/speedway-h26/H26.csproj -c Release --warnaserror
$dll = 'tools/speedway-h26/bin/Release/net8.0/H26.dll'
dotnet $dll audit-207 . ../h29-audit
dotnet $dll plan-207 . ../h29-pilot pilot
dotnet $dll plan-207 . ../h29-greedy greedy
dotnet $dll plan-207 . ../h29-beam beam
dotnet $dll plan-207 . ../h29-recovery recovery ../h29-beam/beam.json.gz
dotnet $dll plan-207 . ../h29-pace pace ../h29-beam/beam.json.gz
dotnet $dll recheck-207 . ../h29-locked ../h29-pace/beam.json.gz pace
dotnet $dll replay-207 . ../h29-replay ../h29-pace/beam.json.gz
dotnet $dll decisions-207 . ../h29-timing ../h29-pace/beam.json.gz
node tools/speedway-h26/summarize207.mjs
~~~
