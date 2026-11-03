# Study #165

This isolated .NET 8 executable is PROVISIONAL and OFF unless an explicit
research mode is invoked. It runs the existing native Box2D harness without
changing the original contact model, world, control queue or physical bounds.
The tool has no production entry point.

Run from the repository root. Keep every run in a new evidence directory.

```powershell
dotnet restore tools/fast-corner-feasibility/fast-corner-feasibility.csproj --locked-mode
dotnet build tools/fast-corner-feasibility/fast-corner-feasibility.csproj -c Release --no-restore --warnaserror
$dll = "tools/fast-corner-feasibility/bin/Release/net8.0/fast-corner-feasibility.dll"
dotnet $dll --search <new-evidence-directory>
dotnet $dll --extend <same-evidence-directory>
dotnet $dll --verify <same-evidence-directory>
```

The initial search caps the ledger at 100 admitted worlds. Extension requires a
complete legal witness in the first 100 and caps the aggregate at 250. Verification
freezes the selection before starting up to six fresh worlds and records one #162
bundle per world. The 900 CPU-s budget includes both phases. Never delete or
reuse a started ID. The `--resume` path is for a documented interrupted extension
only. It requires a separately recorded process CPU allowance in
`interruption.json` and skips all earlier admitted proposals, including unfinished
ones. A clean run does not reproduce the interruption sequence.

The committed results preserve the measured process totals and the B113
interruption. To reproduce the six exact native attempts from the committed
ledger, use a new directory containing its `trials.jsonl`, `search.json`,
`extension.json` and `interruption.json`, then invoke `--verify` in that directory.
Phase C uses the recorded original commands and a new world from rest for each
attempt. The ordinal review clock in each world is 0.2 s.

`python tools/fast-corner-feasibility/analyze.py` requires Matplotlib and reads the
committed evidence. It verifies archive and payload hashes, creates the interval
CSVs and figures, and uses the #145 midpoint energy accounting. It starts no
worlds. The [report](../../docs/experiments/fast-corner-feasibility/REPORT.md)
describes the common section, end-state limitations and negative results.
