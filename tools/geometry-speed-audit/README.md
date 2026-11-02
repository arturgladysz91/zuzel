# Study159 — read-only geometry and original-model audit

PROVISIONAL, default OFF. Base: exact study158 HEAD
`9c94a21e37877fd82565fd309fd2850aeb23f341`. No new production rider or physics.

Read the [report](../../docs/experiments/geometry-speed-audit/REPORT.md) and
[predeclared budget](../../docs/experiments/geometry-speed-audit/budget-plan.json).
All native inputs remain frozen under `docs/experiments/robust-three-lap`.

Build `dotnet build tools/geometry-speed-audit -c Release --warnaserror`.
Explicit modes: `--geometry`, `--diagnostics`, `--certificates`,
`--operating-points`, `--microprofile`. Each mode writes receipts and refuses to
overwrite prior evidence. Budget includes prior process CPU and the explicitly
charged failed bootstrap reservation. The CLI has no native-world mode.

Geometry export reads exact original boundary vertices through diagnostic-only
reflection. The operating-point search queries original contacts and verifies
the original clutch/shaft/wheel balances. A numerical root is local, not a
stability or safe-completion certificate. Failed finite searches are not
impossibility proofs. Forecasts never become native S3 results.

Run `python tools/geometry-speed-audit/analyze159.py` to audit the ten native
histories and export100Hz telemetry. Run `render159.py` with matplotlib/numpy
to build plots, the report and standalone replay. The replay samples exact
recorded states, retaining original source indices; it does not integrate or
interpolate physical states.
