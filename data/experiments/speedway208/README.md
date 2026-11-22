# Study #208 evidence

[Study report](../../../docs/experiments/speedway-h29-v/REPORT.md) · [Summary](summary.json) · [SHA-256 manifest](manifest.json)

- `fourth-exit-audit.json.gz` contains fresh strict replays of #207 policy 12152 on the five exact fixtures. It records all substeps through the fourth exit and 420 accepted-prefix steering and throttle counterfactuals.
- `arm-{b4,c4,d4,e4}.json.gz` contain the controlled fourth-corner ablations and offline resource accounting. `arm-x{5,6}-{c,d,e}.json.gz` extend three independent roots. The per-arm `Prefixes`, branch shapes, `Gates` and full pending and technique checkpoint establish reachability. `Successors` preserves every tested common post-exit control and its outcome. Branch IDs are local to their arm.
- `paired-training.json.gz` changes only preparation lead or enabled pace feedback on each fixed arm leader. These diagnostic runs use nominal and ±1 cm.
- `training-selection.json.gz` contains the time-zero screening of the finalist set. The supplemental development starts at ±6 and ±16 cm were registered before selecting the policy. `frozen-candidate.json` is the one policy evaluated on the exact official fixtures afterward.
- `official/` records the time-zero strict outcomes and each official fixture’s full dynamics trace. `C-alt` is −12 cm and `C-alt-plus` is +12 cm.
- `model-timing.json.gz` measures a full 0.25 s receding guard and baseline feedback on MODEL observations. Native results and timings belong to the separately ledgered run files.
- `parity.json`, `published-policies-check.json`, `validation.json` and `world-ledger.jsonl` give regression and construction evidence.
- `figures/` contains the data-derived trajectory and exit momentum diagrams.

## Replay and recheck

The commands below run from the repository root. Use fresh output directories. The offline training creates no Box2D worlds.

~~~powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS = '1'
dotnet build tools/speedway-h26/H26.csproj -c Release --warnaserror
$dll = 'tools/speedway-h26/bin/Release/net8.0/H26.dll'
$data = 'data/experiments/speedway208'
dotnet $dll audit-208 . ../h29-v-audit
dotnet $dll plan-208 . ../h29-v-late B
dotnet $dll plan-208 . ../h29-v-steering C
dotnet $dll plan-208 . ../h29-v-joint D
dotnet $dll plan-208 . ../h29-v-viability E
dotnet $dll plan-208 . ../h29-v-fifth X ../h29-v-viability/candidate.json
dotnet $dll plan-208 . ../h29-v-sixth X ../h29-v-fifth/candidate.json
dotnet $dll lock-208 . ../h29-v-official "$data/frozen-candidate.json"
dotnet $dll time-208 . ../h29-v-timing "$data/frozen-candidate.json"
node tools/speedway-h26/summarize208.mjs
~~~
