# Study #167

PROVISIONAL. The study is OFF except through the explicit console commands below.
The tool links the unmodified #166 native harness and first-bend policy.
The archived result and all DNF rows are in the [report](../../docs/experiments/fast-two-corners/REPORT.md).

Run from the repository root with .NET 8 and a fresh evidence directory.

```powershell
dotnet restore tools/fast-two-corners/fast-two-corners.csproj --locked-mode
dotnet build tools/fast-two-corners/fast-two-corners.csproj -c Release --no-restore --warnaserror
$dll = 'tools/fast-two-corners/bin/Release/net8.0/fast-two-corners.dll'
dotnet $dll --diagnose <evidence>
dotnet $dll --study <evidence>
python tools/fast-two-corners/analyze167.py <evidence> <report-directory>
```

The analysis requires Matplotlib and executes zero worlds. The frozen results can
be inspected by extracting `evidence.zip`. Each phase uses an exclusive file lock
and a persistent reservation. An interrupted phase remains in the ledger.
The study trains two independent seeds, selects on separate starts, seals the
sources and parameters, and runs the eight fixed holdouts once. The final stage
also extends four matched #166 histories and reads the four existing #161 archives.
