# Study #171

Windows research tool, OFF unless invoked. It links the frozen #166 and #170
controllers and uses the original #166 native Box2D world. K0 emits the exact
#170 reserve-speed decisions. K1–K4 add a full-bend spatial plan and a bounded
feedback search. The run ledger charges CPU and native worlds across phases.
See [the audit](../../docs/experiments/whole-corner-planning/AUDYT.md) and
[the report](../../docs/experiments/whole-corner-planning/REPORT.md).

Run from the repository root. `screen` uses no worlds and seals the holdout.
`short` tests the first bend and the next entry. `full` admits at most two
legal alternatives with a development gain over 0.01 s to the second entry.
`tempo` tests one speed-profile change on the best legal full candidate.
`validate` freezes the best full candidate with a development gain over 0.01 s
and runs eight matched pairs once. The H3/H4 K0 evidence comes directly from
the #170 archives.

```powershell
$project = 'tools/whole-corner-planning/whole-corner-planning.csproj'
dotnet restore $project --locked-mode
dotnet build $project -c Release --no-restore --warnaserror
$dll = 'tools/whole-corner-planning/bin/Release/net8.0/whole-corner-planning.dll'
dotnet $dll screen <new-evidence-directory>
dotnet $dll short <same-evidence-directory>
dotnet $dll full <same-evidence-directory>
dotnet $dll tempo <same-evidence-directory>
dotnet $dll validate <same-evidence-directory>
python tools/whole-corner-planning/report171.py <same-evidence-directory> <analysis-output-directory>
```

`audit171.py` cuts the historical data at the actual segment boundaries.
`profile171.py` regenerates the independent nominal references for the two
bends from V1/V3/V5/V7. Run it to a scratch path for byte comparison with the
frozen profile. `report171.py --audit-only` checks hashes, queue activations and
the matched runs without generating plots or creating new worlds.
