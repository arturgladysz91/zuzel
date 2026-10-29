# Study150 — default OFF / PARTIAL

Base PR148 commit `8aedd292bda2de5742952f4ddc280f873f5a517c`. No original physics or historical results are edited. `Planner150` requires explicit `Enabled:true`; `Program150` requires one explicit flag. It is an investigation harness, not a production controller.

Read [REPORT.md](../../docs/experiments/robust-autonomous-racing/REPORT.md) before running more worlds. No new three-lap standing-start completion exists. All saved failures count toward the ledger. The eight native worlds include two fixed-tape boundary crossings, rolling diagnostics, native-prefix diagnostics and one failed full start.

Build from repo root:

```powershell
dotnet restore tools/robust-autonomous-racing/robust-autonomous-racing.csproj --locked-mode
dotnet build tools/robust-autonomous-racing/robust-autonomous-racing.csproj -c Release --no-restore
```

Representative flags in `Program150.cs`:

| Flag | Scope |
|---|---|
| `--diagnosis150` | Fixed-tape A–E causal counterfactuals, no world |
| `--native-diagnosis150` | Two native fixed-tape worlds; confirmed crossings |
| `--critical-midpoint150` | Exact1s accepted state + own prior certified tape |
| `--state-probes-midpoint150` | Critical and held-out actual149 states |
| `--recover-a150`, `--recover-b150` | Exact native rolling prefix to1s, then new feedback |
| `--prefix-b28-150`, `--prefix-b45-150` | Native prefix from zero with own-model old controls, then new planner |
| `--full-a-tube150` | Full autonomous attempt from zero; abstains0.4s |
| `--contract-audit150` | Independently re-evaluate all333 saved contracts without caches |
| `--recourse-audit150` | Finite next-observation coverage audit at failed0.4s |

Example executable invocation: `dotnet run --project tools/robust-autonomous-racing/robust-autonomous-racing.csproj -c Release --no-build -- --contract-audit150`.

The saved attempts are write-once. These flags deliberately reject duplicate attempt names; use an isolated checkout/output folder with a fresh study ledger for independent reproduction. Preserve inputs, immutable sources, options and all failed attempts. Do not delete the supplied evidence or edit old budgets to make a flag rerun. Intermediate native result files record source SHA; the first rolling pilot used inverse query h0, later results h0.01. Contract audit selects each saved variant explicitly.

Saved-only processing (unique label per invocation):

```powershell
$env:PYTHONUTF8='1'
python tools/robust-autonomous-racing/analyze150.py review-copy
python tools/robust-autonomous-racing/report150.py review-copy
python -m unittest discover -s tests/calibration -p test_robust150.py
node --test tools/replay-viewer/robust150.test.mjs
```

`report150.py` needs matplotlib. C# fixture tests join the existing core shard, Python evidence tests the existing calibration job, and Node tests the existing replay entrypoint, including Windows CI. Replay `?case=robust150` defaults to the failed standing-start; rolling and prefix successes are explicitly labeled diagnostic.

Known limitations: finite prior-error scenarios are not a global uncertainty bound; long-horizon predictor certificates are not native guarantees; K1 tails and bounded event proposals remain incomplete. CPU is measured per actual invocation and experiment, with real operation counts, but no per-role CPU timers were recorded. No production or four-rider readiness claim.
