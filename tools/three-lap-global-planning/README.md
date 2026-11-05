# Study #172

Explicit Windows research CLI, OFF by default. CyclicController172 composes the
cyclic #166/#170 adapters and the adapted #171 K4 planner. Historical controllers
and physical formula files remain sealed. generate172.py derives the adapters
with exact checked replacements. Run its --check mode to verify the derivation.

The native adapter retains the original Box2D force path and adds a diagnostic
second after the third credited finish. Body activity, decisions, pending queue,
wheel, shaft and clutch continue in the same world.

Run from the repository root.

```powershell
$project = 'tools/three-lap-global-planning/three-lap-global-planning.csproj'
dotnet restore $project --locked-mode
dotnet build $project -c Release --no-restore --warnaserror
python tools/three-lap-global-planning/generate172.py --check
$dll = 'tools/three-lap-global-planning/bin/Release/net8.0/three-lap-global-planning.dll'
dotnet $dll prepare p0 <fresh-evidence-directory>
dotnet $dll C1 c1 <same-evidence-directory>
dotnet $dll C2 c2 <same-evidence-directory>
dotnet $dll C3 c3 <same-evidence-directory>
dotnet $dll validate v1 <same-evidence-directory>
python tools/three-lap-global-planning/analyze172.py <same-evidence-directory> <analysis-output-directory>
```

The phase gates preserve CPU reservations, require the preceding integration
stage, reserve ten worlds for validation and close native research after freeze.
The development ledger also includes three read-only probes on the original
c3a archive. Those probes create no worlds.

The committed evidence is split into development, K4 validation, C0 validation
and protocol archives. Extract them together to run the analyzer with --audit-only.
The analyzer replays command activation and pending state for every saved sample
and checks three finish crossings and the autonomous reserve.

