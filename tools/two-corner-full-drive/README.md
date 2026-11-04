# Study #170

Isolated Windows research executable. Default OFF. The original #166 controller is
linked unchanged. The two-corner course and independent clearance analysis reuse #167.
The only native world is #166 with original contacts, powertrain, step and actuator.
See the [report](../../docs/experiments/two-corner-full-drive/REPORT.md).

Run from the repository root with the historical archives present. The study uses
one evidence directory. It refuses overlapping phases and duplicate reservations.
Changing the sealed holdout file invalidates the study. Each tuning stage below
contains two development starts. Validation freezes the fastest eligible development
variant before running it alongside #166 on V1–V8.

```powershell
$project = 'tools/two-corner-full-drive/two-corner-full-drive.csproj'
dotnet restore $project --locked-mode
dotnet build $project -c Release --no-restore --warnaserror
$dll = 'tools/two-corner-full-drive/bin/Release/net8.0/two-corner-full-drive.dll'
dotnet $dll baseline <evidence-directory>
dotnet $dll first <evidence-directory>
dotnet $dll recourse <evidence-directory>
dotnet $dll rolloff <evidence-directory>
dotnet $dll both <evidence-directory>
dotnet $dll headroom <evidence-directory>
dotnet $dll earlier <evidence-directory>
dotnet $dll reserve-speed <evidence-directory>
dotnet $dll validate <evidence-directory>
python tools/two-corner-full-drive/analyze170.py <evidence-directory> <analysis-output-directory>
```

The analyzer verifies queue and activation parity and the archive hashes. Pass
`--audit-only` to skip Matplotlib and output generation. Native evidence stays
available for every aborted trial and for the extra second beyond the goal.
