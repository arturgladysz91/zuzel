# Study #168

PROVISIONAL, OFF. This diagnostic references the original native Box2D harness,
contact and engine code and the frozen #166 and #167 policies. It is enabled
through an explicit command in the research executable.

Run from the repository root with .NET 8.

```powershell
dotnet restore tools/throttle-contact-audit/throttle-contact-audit.csproj --locked-mode
dotnet build tools/throttle-contact-audit/throttle-contact-audit.csproj -c Release --no-restore --warnaserror
dotnet tools/throttle-contact-audit/bin/Release/net8.0/throttle-contact-audit.dll --run <fresh-evidence-directory>
python tools/throttle-contact-audit/analyze168.py <evidence-directory> <report-directory>
```

The analysis requires Matplotlib. It reads native archives and executes zero
worlds. Each C arm reconstructs the full accepted prefix from the historical
standing start with the original controller and verifies its hash. All
interventions enter the ordinary queue and change future requests only. The
native clocks and physical stops determine the measured scope.

The [report](../../docs/experiments/throttle-contact-audit/REPORT.md) contains
the paired measurements and limits of the diagnosis. The archived run, frozen
budget, raw and derived evidence and checksums are in the same directory.
