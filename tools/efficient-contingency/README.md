# Study #152 — efficient contingency planning, default OFF

Exact base: PR #150 HEAD `5558a30c8864199fb1c7abc18852ca27f4d29d9b`.
This opt-in tool adapts the frozen151 planner; it does not change production defaults,
physics A/B, geometry, tyres, engine, parameters or historical results.

The experiment cap is **1200 process CPU-s /15 native worlds**, including failed
attempts and saved-data analysis. A write-ahead ledger rejects duplicate names and
overlapping reservations. Native experiments reserve at most420 CPU-s each, with
the original30 CPU-s per-review and forecast/contact limits.40 CPU-s is reserved
for export. Compilation, regression tests and CI are outside the experiment ledger.

Read [the report](../../docs/experiments/efficient-contingency/REPORT.md) and
`budget.json` before attempting reproduction. Never erase the supplied ledger to
continue tuning. Use an isolated checkout and a fresh output directory/attempt
names for a new study; supplied evidence is write-once.

```powershell
dotnet restore tools/efficient-contingency --locked-mode
dotnet build tools/efficient-contingency -c Release --no-restore
python tools/efficient-contingency/generate152.py --check
dotnet tools/efficient-contingency/bin/Release/net8.0/efficient-contingency.dll --native-a152
```

The final command deliberately rejects a duplicate reservation in the supplied
checkout. All entry points require an explicit flag; `Planner152` also requires
`Options151(Enabled:true)`.

| Flag | Scope |
|---|---|
| `--micro152` | Every wheel output bit in1920 paired A/B kernel cases |
| `--baseline152` | Compact digests of all173 original151 decisions; no old controller rerun |
| `--bench-cold152` | Negative coefficient-only timing experiment |
| `--bench-verified152` | Exact substep DAG/independent-geometry experiment |
| `--bench-project152` | Negative nearest-piece upper-bound experiment |
| `--bench-boundary152` | Selected exact BVH primitive upper-bound comparison |
| `--gate1-152` | Native historical own-control prefix to1s, then new recertified A/B recovery |
| `--gate1-b-v2-152` | Preserved B diagnostic retry; its later CPU stop remains visible |
| `--repair38-v7-152` | Same saved38.6s counterexample with freshly reconstructed own prior policy; complete new finite certificate |
| `--recovery-portfolio152` | Algorithmic native1s pair: A first bend recovered; B later CPU stop preserved |
| `--native-portfolio-a152` | Selected algorithmic variant: native three-lap A completed |
| `--native-portfolio-b152` | Requires completed A, completed new B recovery and held probes; not run in this study |
| `--native-a152` | Continuous standing three-lap A; validates every original151 prefix decision before submitting it |
| `--held-b152` | Fresh finite certification of the frozen39.4/45.8 B observations |
| `--native-b152` | Continuous standing B, gated by completed A and held probes |

Gates2–4 share one continuous A world per variant. The new algorithmic B
recovery remained partial; its first finite certificates passed, then the pair CPU
reservation expired. Full A proceeded on its successful A recovery and the
successfully repaired saved38.6 state; full B stays gated.

Gates2–4 share one continuous A world. Gate1 means an exact151 certificate at1s
and native recovery through the difficult first bend on both models. Full
diagnostic-lap completion is a separate field; a later diagnostic CPU stop never
becomes a completed lap. See the saved gate1 audit for this distinction.

The selected implementation shares exact0.2s physical prefixes (the newly issued
command cannot activate until0.24s), complete identical rollouts, and independent
prefixes within one fresh preflight. It retains all13 logical nodes /9 reserves.
Search and verification caches are isolated. Verification uses the **frozen**
148/149 predictor, original wheel/clutch calculations and a separate original BVH;
its already-proved connected-capsule fast path preserves signed clearances.
Corrupting a search result must fail the fresh comparison. No previous review's
legality is accepted as proof. The exact mode preserves151 proposal ordering. The selected algorithmic mode
adds existing-policy proposals, spreads its six repairs across earlier epochs, and
retains a useful policy prefix across a goal change. It replans the uncovered tail
one existing K1 horizon before the old policy ends. The full K1 grid/order/scoring,
observation envelope, branch signs, repair count limit, terminal horizons and
physical guards remain151. The historical counter name Complete27K1Batches means
a complete grid batch; actual grids contain27 or30 candidates.

`generate152.py` records mechanical adapters rather than modifying frozen source.
The nearest-segment upper bound comes from an actual unchanged distance query;
the original complete BVH still determines the minimum. Negative dictionary and
projection experiments are retained as results; their timing must not be presented
as a successful optimization. Runtime counters distinguish logical contacts from
actual search work. Failed original fallback calls lack partial contact counters;
reported counts for those calls are explicitly lower bounds, with faults retained.

`policies/` stores content-addressed tape inputs, complete logical/rejected nodes,
all command sequences, starting states, terminal goals and a digest of every dense
selected physical forecast/certificate. Dense traces are regenerated from these
inputs instead of duplicated nine times. Native body states, forces, energy,
queues and activations remain dense in `native/`. Hash-chained journals flush
before returning a command. Display traces are decimated and never used for error
measurement. Replay: `tools/replay-viewer/?case=efficient152`.

```powershell
dotnet test tests/CoreSim.Tests -c Release --filter FullyQualifiedName~EfficientContingency152Tests
python -m unittest discover -s tests/calibration -p test_efficient_contingency152.py
node --test tools/replay-viewer/efficient152.test.mjs
```

The certificate covers the unchanged finite correlated scenarios. It is not a
global guarantee over all disturbances, a native Box2D proof for each analytical
leaf, or evidence of readiness for four simultaneous riders.

Final local validation:2507 C# core/trajectory,318 Python,7 CI-policy and89 Node tests passed.3028 C# cases were discovered. The full selected A is55.167078825s /153.890625 controller CPU-s; exact151 prefix uses219.046875 CPU-s. See REPORT.md for scope, negatives and the incomplete B gate.
