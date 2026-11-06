# Study #173 — observation-only throttle decision audit

The research CLI is OFF without explicit arguments and does not alter #172 or
any physical model. The report is in `docs/experiments/throttle-decision-audit`.
The frozen #172 sources provide all decisions, targets, planner and physical
forecast equations. `generate173.py` adds observations with exact checked anchors.

Read-only reproduction, from the repository root:

```powershell
python tools/throttle-decision-audit/generate173.py --check
dotnet restore tools/throttle-decision-audit/throttle-decision-audit.csproj --locked-mode
dotnet build tools/throttle-decision-audit/throttle-decision-audit.csproj -c Release --no-restore --warnaserror
# Extract 173-diagnostics.zip and 173-native.zip together into a temporary directory.
python tools/throttle-decision-audit/verify173.py <extracted-directory>
```

Verification reads archived states, causal queues, receipt hashes, exact executed
source snapshots and common native prefixes. It creates no new forecasts or worlds.
Current native-loop and predictor sources remain linked from the historical tools.
The complete native baseline matches its archived #172 prefix, and all four arms
have identical physical prefixes through activation of the single intervention.

The explicit research stages below cost new CPU and are not part of automatic
verification. Use a new directory, never overwrite the completed ledger:

```powershell
$dll = 'tools/throttle-decision-audit/bin/Release/net8.0/throttle-decision-audit.dll'
dotnet $dll replay r1 <fresh-directory>
python tools/throttle-decision-audit/analyze173.py <fresh-directory> <fresh-analysis-directory>
dotnet $dll counterfactual q1 <same-directory>
```

The native stage is reserved for the stated unresolved whole-next-bend P7
hypothesis. Its preregistered arms intervene in exactly one delayed command.
The original K4 then continues autonomously. Completed evidence is closed to
further native rollouts. Max 6 worlds and 100 CPU-s; the study used 4 worlds.
No S3 performance improvement is claimed from the short diagnostic windows.

`geometry` exports six visits/eighteen sections without a native world. Windows
CI compiles this isolated tool, verifies default OFF, and checks that export.
The main Full Windows CI also runs all original C#/Python/Node regressions.

`analyze173.py` is the first archival reconstruction analysis; `finalize173.py`
adds native-prefix checks, per-review force data and figures. Reserve decisions
remain recorded but are excluded from the primary S3 mechanism percentages.
Prediction samples label only actually computed horizons. Missing future data
remain unknown. The report distinguishes reference arithmetic, blocked increases,
physical predictions and native causal evidence.
