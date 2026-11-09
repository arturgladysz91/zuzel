# Study 177 · trajectory and joint control

PROVISIONAL · default OFF. The executable links the native #172 world directly and uses the #174 mechanical fork of K4 for separate causal continuations. The experimental policy executes only in explicitly requested worlds. Production CoreSim is outside this tool.

From the repository root, with .NET 8 on Windows:

```powershell
dotnet restore tools/trajectory-control/trajectory-control.csproj --locked-mode
dotnet build tools/trajectory-control/trajectory-control.csproj -c Release --no-restore --warnaserror
python tools/trajectory-control/verify177.py
```

The verifier checks the sealed evidence, including a deliberate stale-wheel and stale-steering regression, all accepted state transitions, linear/angular impulses, the energy ledger, the original delayed queue and the five paired results. It opens zero native worlds.

## Reproduce the final configuration

Use a fresh evidence directory. Reruns of published holdouts are reproductions of the same conditions. Forecasts and run fingerprints are deterministic on the matching native environment. CPU and wall time are measured afresh.

```powershell
$study = "tools/trajectory-control/bin/Release/net8.0/trajectory-control.dll"
$evidence = "results/study177-reproduction"
dotnet $study prepare $evidence
dotnet $study preflight $evidence
python tools/trajectory-control/verify177.py preflight $evidence
dotnet $study probe $evidence v2
dotnet $study develop $evidence E L1-late
dotnet $study develop $evidence P L1-late
dotnet $study develop $evidence X1 L1-late
dotnet $study develop $evidence X2 L1-late
python tools/trajectory-control/verify177.py development $evidence
dotnet $study validate $evidence L1-late
```

Use `analyze177.py EVIDENCE OUTPUT` with NumPy and Matplotlib to regenerate tables and plots from the results. The archived protocol contains both exploratory screens and the preflight that stopped on the earlier K4 archive. The final source freeze covers the controller, holdout and package lock; timings and telemetry remain in immutable raw archives.

The report answers the study questions and explains the local improvement, failed alternatives and paired outcome. [Raport #177](../../docs/experiments/trajectory-control/RAPORT.md)
