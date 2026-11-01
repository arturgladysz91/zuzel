# Robust autonomous three-lap racing #158 — OFF

PROVISIONAL research controller, exact base Draft PR #156:
`7c24c31dd8587889ff2459608c328fb865a541ef`.
No production integration or physical parameter changes.

`Control158` predicts the irrevocable 0.24 s from the complete observed state and
pending queue using the original physical provider. It proposes joint gas/steering
commands by inverting original contact responses. A smooth launch merge reaches
the target offset before the first bend. It reads no demonstration or command file.
`Generator158` constructs a causal closed-loop reserve, never a historical tape.

`Planner158.Generated.cs` derives the unchanged finite 13-node / 9-full-reserve
protocol from frozen #157. It actually calls `Feedback158`; the #157 generator's
whitespace-sensitive replacement had missed that hook. Own plans remain proposals:
every observed state and shifted geometric goal requires a full fresh certificate.
The final reserve extends 32 m beyond the actual finish; no leaf is shortened at
the finish. `Runtime158` derives the original #153 runtime and independently
repropagates selected physical prefixes, including bit checks against search.
`Joint158` is mechanically derived from `Joint150`, with exact-state response memoization.

The 0.2 s proposal prefix can be extracted from the already computed 0.24 s
activation forecast because a new command cannot yet activate. Its physical
provider arithmetic is canonical (`exactInitialWheel:false`). The rejected variant
and 72-state bit comparisons are preserved. Independent validation never accepts
the primed search prefix as proof. No source under `src`, `data`, or the frozen
physical tool directories is modified.

Explicit commands require unique labels. `Native158` wraps the **unchanged**
`NativeWorld156`, with three required laps, one continuous original Box2D world,
S from time zero, real delays/actuators/contacts, no state restoration or stop-reference.

```powershell
dotnet build tools/robust-three-lap -c Release --warnaserror
node tools/robust-three-lap/generate158.mjs --check
$env:SPEEDWAY158_OUTPUT='docs/experiments/robust-three-lap/final-v6'
# Development is restricted to non-held scenarios; 6 is known regression A.
dotnet tools/robust-three-lap/bin/Release/net8.0/robust-three-lap.dll --native-new 6 unique-development-label
# Allowed only after an actual new S three-lap completion, before any validation.
dotnet tools/robust-three-lap/bin/Release/net8.0/robust-three-lap.dll --freeze unique-freeze-label
# Run every frozen condition for each variant; indices are 0..9 for final-v6.
dotnet tools/robust-three-lap/bin/Release/net8.0/robust-three-lap.dll --validate new 0 unique-new-label
dotnet tools/robust-three-lap/bin/Release/net8.0/robust-three-lap.dll --validate base 0 unique-base-label
```

`SPEEDWAY158_OUTPUT` selects evidence storage only. It does not alter decisions.
The frozen scenario manifest fixes all starting conditions before outcomes.
New and base use identical complete stopped states, setup, track and physical laws.
Baseline is unmodified `Planner157` at the exact base, with its original work limits;
both face the same mandatory S checks. All time-zero refusals remain native DNFs.

`--diagnostic`, `--probe`, and `--prefix-proof` are development diagnostics only.
They never count as a native finish. Each native process records its source ZIP,
full raw world, every decision and complete compact policy DAG, including rejection.
Published policy journals are lossless per-trial ZIPs. To repeat `--prefix-proof`
from a fresh checkout, first expand `policies/dev-native-v3.zip` into the matching
`policies/dev-native-v3/` directory under the root study evidence directory and
unset `SPEEDWAY158_OUTPUT`. It reads observed states only, never future commands
for a native rider. Production validation does not read those archives.
Concurrent validation has an atomic CPU/world journal. A 2400 CPU-s trial stop or
60 CPU-s decision cap means DNF, not predicted completion. The user did not impose
the old #157 global 1000 CPU-s / 15-world cap on #158.

V5 remains a separate 4/5 PARTIAL series. Final-v6 predeclares ten distinct cases:
nominal plus five fresh held cases, and four known regression cases. All ten are
required; the old failures cannot be dropped. A single race or local diagnostic
does not satisfy autonomy. No timing improvement percentage is defined when the
baseline completes zero S races. Four-rider racing needs separate validation.

Saved-data tools (no physical stepping):

```powershell
python tools/robust-three-lap/analyze158.py unique-analysis-label
python tools/robust-three-lap/report158.py unique-export-label
```

See the [report](../../docs/experiments/robust-three-lap/REPORT.md) for measured
results, all failures, CPU, six-bend metrics and actual-state replay.
