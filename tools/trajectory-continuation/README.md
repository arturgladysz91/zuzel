# Study161 — default OFF, PROVISIONAL

Base `e8866e91c4c3818b8ca836260883ca847ccc4a8a`, final160 v6.
No production, original physics, geometry, historical evidence or certificate
protocol change. No merge. OFF directly delegates to original160's OFF controller.

New proposals fix race-finish truncation of physical curvature preview, consider
coasting despite a saturated Newton seed, bound all gas/steering differences,
and use coupled actual-actuator yaw/rear-slip feedback. `Space161` distinguishes
position and course velocity from beta. Three deterministic full maneuvers are
compared by common-goal arrival and original pose/swept/beta legality; no reward
for peak speed, gas1 or beta0. Illegal/incomplete maneuvers are retained separately.
Original physics/invariant exceptions are never reported as a legal maneuver.

`Planner161.Generated.cs` changes proposal hooks only from frozen160. Runtime158,
finite13/9 and fresh independent preflight remain original. Check:
`node tools/trajectory-continuation/generate161.mjs`.

Research commands are explicit; reserve the declared budget before work:

```powershell
dotnet restore tools/trajectory-continuation/trajectory-continuation.csproj --locked-mode
dotnet build tools/trajectory-continuation/trajectory-continuation.csproj -c Release --no-restore --warnaserror
dotnet tools/trajectory-continuation/bin/Release/net8.0/trajectory-continuation.dll --diagnostic unique-label native-end
dotnet tools/trajectory-continuation/bin/Release/net8.0/trajectory-continuation.dll --certificate unique-label rest
```

Never overwrite a label or erase the ledger. Six diagnostic operations maximum,
each one authentic start and at most three logged maneuver alternatives; two
standalone full certificate probes; one development native world. Five frozen
held cases only after full native S3. First native DNF prevents further starts.
Full C#/Python/Node regression runs once on the final source, not after every edit.

Historical audit is read-only and runs zero new forecasts. Native160 last20s and
its saved v6 continuation are preserved separately from161 predictions/worlds.
Portfolio inputs (selected plus nine conditional tapes) are recovered; absent
dense state arrays and failed callback state prevent claiming a complete bit-exact
planner replay. G2/G3 forecasts never count as native finishes.

Raw Runtime158 counters exclude primed prefixes and proposal-predictor calls;
do not read a zero in those counters as zero physical work. Process CPU charges
cover the complete operation. Original records, every evaluated diagnostic tape,
native100Hz data, policy DAGs and postprocessed CSV remain available.
