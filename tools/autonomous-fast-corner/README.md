# Study #166

This isolated .NET 8 research executable is PROVISIONAL and OFF. The policy
uses spatial profiles, queued-command prediction and closed-loop steering and
throttle. Native execution uses the original #156 two-contact Box2D harness.
Verify the generated derivation with
`python tools/autonomous-fast-corner/generate166.py --check`.

Run from the repository root. Each phase writes a non-overwriting ledger into
a fresh evidence directory. Historical #161 archives and #134 choices must
be present, in addition to both #165 demonstration ZIPs.

```powershell
dotnet restore tools/autonomous-fast-corner/autonomous-fast-corner.csproj --locked-mode
dotnet build tools/autonomous-fast-corner/autonomous-fast-corner.csproj -c Release --no-restore --warnaserror
$dll = "tools/autonomous-fast-corner/bin/Release/net8.0/autonomous-fast-corner.dll"
dotnet $dll --prepare <new-evidence-directory>
dotnet $dll --diagnose <same-evidence-directory>
dotnet $dll --train <same-evidence-directory>
dotnet $dll --extend <same-evidence-directory>
dotnet $dll --validate <same-evidence-directory>
dotnet $dll --pair <same-evidence-directory>
```

Extension requires initial training and measured progress. The four held starts
with identical saved #161 worlds reuse those archived states. The remaining
baseline arms run after freezing and validation. They keep the original 13/9
check and receive separate CPU allocations. The recorded interruption consumed
a reservation before the first native step. A clean checkout avoids it.

`python tools/autonomous-fast-corner/analyze166.py` reads the evidence, verifies
archive and payload hashes, computes paired gains, exports the teacher decisions
and renders plots using Matplotlib. It runs no worlds.

The [report](../../docs/experiments/autonomous-fast-corner/REPORT.md) gives the
metrics, pair exclusions and the scope of terminal stability.
