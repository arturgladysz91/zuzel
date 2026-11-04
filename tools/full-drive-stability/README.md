# Study #169

PROVISIONAL and default OFF. The native adapter is generated from the frozen
#166 harness and routes only explicitly armed variants to the study contact.
The original Box2D, controllers, load law, wheel law and engine are untouched.
The forecast adapter uses the same study contact and accepted acceleration lag.

Run from the repository root with .NET 8 and a fresh evidence directory.

```powershell
python tools/full-drive-stability/generate169.py --check
dotnet restore tools/full-drive-stability/full-drive-stability.csproj --locked-mode
dotnet build tools/full-drive-stability/full-drive-stability.csproj -c Release --no-restore --warnaserror
$dll = 'tools/full-drive-stability/bin/Release/net8.0/full-drive-stability.dll'
dotnet $dll --run <new-evidence-directory>
dotnet $dll --verify-local <same-evidence-directory>
python tools/full-drive-stability/analyze169.py <same-evidence-directory> <report-directory>
```

The second command executes zero native worlds. The Python analysis requires
Matplotlib and reuses #168's energy accounting. The narrative is tied to the
recorded study, while tables and charts are generated from its raw evidence.
See the [report](../../docs/experiments/full-drive-stability/REPORT.md) for the
hypotheses, exact comparison windows, data sources and measured limitations.
