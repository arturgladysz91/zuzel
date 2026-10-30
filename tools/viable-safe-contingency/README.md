# Study154 — viable half-metre continuation (PARTIAL)

Exact base: `de500229a6fdb5c57da7cbeb90f5484c28d57130`, Draft PR152.
New branch `experiment/viable-safe-contingency-154`, intended Draft PR153; no merge.
Tools only, default OFF. Physics A/B, engine, tyres, dimensions, native integration,
geometry and historical evidence are frozen. Every accepted decision still needs
13 logical nodes, nine complete reserves and fresh independent preflight.

S0 is153 without optional policy ranking. S1 ranks original full H09 predictions by
future outward drift and necessary progress through the terminal geometric section.
These are proposal scores; no approximate criterion is a certificate. S2 first
repropagates retained controls and preserves covered controls when the geometric
goal advances, then repairs/extends the uncovered tail. Exact last-batch prediction
views avoid a second H09 propagation. All actual source versions, including the
failed pure-pursuit and slow variants, are archived per experiment.

The final S1/S2 implementation has NOT passed native gate B. Source snapshots are
the authoritative reproduction of earlier variants; a former label does not make
the final CLI behave like that older algorithm.

The S3 two-endpoint Lipschitz-cone formula is a diagnostic only. Its theorem uses
the same153 velocity/yaw motion bound and numerical allowances. It is not promoted
into the native or forecast guard. All accepted native runs use the original153
strict0.5 m certificate.

## Reproduction

Build `dotnet restore tools/viable-safe-contingency` then
`dotnet build tools/viable-safe-contingency -c Release --no-restore --warnaserror`.
Use a new `SPEEDWAY154_OUTPUT` directory; the supplied ledger and evidence are sealed.
Never erase the ledger to continue tuning. Maximum1500 process CPU-s /15 worlds,
50 CPU-s reserved for export. Native trials retain30 CPU-s/review and at most330
CPU-s per world, rather than increasing caps to claim progress. Standard tests/CI
are outside the experimental budget.

```powershell
$env:SPEEDWAY154_OUTPUT='work/reproduce154'
dotnet tools/viable-safe-contingency/bin/Release/net8.0/viable-safe-contingency.dll --probe 0 fresh
dotnet tools/viable-safe-contingency/bin/Release/net8.0/viable-safe-contingency.dll --probe 1 fresh
dotnet tools/viable-safe-contingency/bin/Release/net8.0/viable-safe-contingency.dll --probe 2 fresh
dotnet tools/viable-safe-contingency/bin/Release/net8.0/viable-safe-contingency.dll --sweep-audit
dotnet tools/viable-safe-contingency/bin/Release/net8.0/viable-safe-contingency.dll --native 1 B-fresh 12 3 120
```

`--probe` reconstructs all states/queues of the actual13.6s policy and checks its
dense digest before choosing from the saved13.8s observation. It creates no world.
`--native` always starts a new Box2D world at rest, with endogenous commands,
0.24s delay and0.2s observations. No saved commands are supplied to that world.

`diagnosis153.json` and `decisions153.csv` inspect all69 accepted153 decisions and
the final failed callback. They distinguish the first bad interval from the whole
forecast reason: later poses may breach0.5m even when the first interval is only
uncertified.153 did not archive per-candidate K1 gap values; those local counts
are N/D for the historical run. New S0 records preserve all local H09 candidates,
full states, terminal states, rejection reasons and actual counters in `diagnostics/`.

`native/`, `policies/`, `diagnostics/`, `timings/`, `sources/` contain actual dense
telemetry, hash-chain journals, complete policy inputs, rejected rollouts and CPU.
Reports distinguish callback CPU, planner CPU, failed callbacks, saved-state work,
CPU/s of native time, CPU/accepted decision and full versus partial distance.

Replay: run `python -m http.server 8000 --bind 127.0.0.1` at repo root and open
`http://127.0.0.1:8000/tools/replay-viewer/?case=viable154`. A stopped prefix and a
local saved-state certificate cannot become a legal three-lap completion.

The finite solo envelope is not a global disturbance guarantee or four-rider proof.
