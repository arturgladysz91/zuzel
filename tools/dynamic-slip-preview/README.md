# Study160 — PROVISIONAL research, default OFF

Exact base: `9fb071ba3129aae88a6ec8c401d51a784be2da2b`. No production
entrypoint, parameter changes, saved baseline driving tape, or merge.

`Controller160` defaults to direct delegation to the unchanged `Planner158`.
ON uses original `Runtime158`, contacts, predictor, wheel, crank/clutch,
delay, steering rate, strict S0.5m and fresh independent finite13/9. The
generated planner differs from158 only in proposal names/hooks; check with
`node tools/dynamic-slip-preview/generate160.mjs --check`.

The local three-equation equilibrium is feed-forward only. A bounded coupled
gas/steering shooting proposal evaluates the original delayed dynamics.
The actuator-aware horizon depends on steering preparation time. A conservative
future-curvature force envelope proposes speed through the next maneuver and
continuation; this is not a global optimum or a substitute for reachable-state
certification. `RankPolicies=true` retains original common-goal arrival ranking,
complete reserves and independent validation. Perturbed reserve feedback targets
only this controller's own causal plan and probes the original actual actuator.

All research commands require explicit CLI arguments. Attempts reserve the
predeclared budget before calculation, cannot overwrite a label, and retain
failure evidence. Production defaults are unaffected. Use a full checkout with
all frozen study inputs; the tool locates `SpeedwayManager.sln` above its binary.

```powershell
dotnet restore tools/dynamic-slip-preview/dynamic-slip-preview.csproj --locked-mode
dotnet build tools/dynamic-slip-preview/dynamic-slip-preview.csproj -c Release --no-restore --warnaserror
dotnet tools/dynamic-slip-preview/bin/Release/net8.0/dynamic-slip-preview.dll --forecast unique-label '{"Enabled":true,"Adaptive":true}'
dotnet tools/dynamic-slip-preview/bin/Release/net8.0/dynamic-slip-preview.dll --probe unique-probe
```

Native commands additionally enforce recorded C/D gates. Only one development
world may start. Any native failure closes the development series. Validation
requires a successful development world and a source freeze before held results.
Never rerun existing labels or silently expand the budget. See the fixed
`budget-plan.json`, journal, raw forecasts, full native records, policy DAGs,
telemetry, report and manifest in `docs/experiments/dynamic-slip-preview`.

Version provenance: v1 was the first fixed12 dynamic forecast. v2 adds a horizon
derived from the original reaction/review and steering rate, one-factor
ablations, and an unsuccessful adaptive surface-coordinate implementation.
v3 corrects lane sampling and includes the full continuation curvature in the
speed envelope. v4 replaces expensive reserve reoptimization by certified local
actual-actuator feedback. v5 enables original common-goal policy ranking and
tests the wider line (rejected). No held condition tunes controller parameters.

Analysis/render scripts only consume stored evidence; they contain no dynamics
or controller. Replay selects exact original native sample indices and freezes
at the final DNF sample. CI confirms implementation/invariant tests, not faster
S3. Only the documented full native development and frozen held results can
establish the scientific outcome.

Proposal parameters are explicit: course response1.2s, beta response1.5s,
normal-force proposal fraction0.5; three Newton iterations, steering perturbation
0.015rad and throttle perturbation0.08; bounded iteration changes0.2rad and0.5
throttle. These are controller choices, not physics changes. Local equilibrium
cache quantizes speed to0.1m/s and curvature to0.0005/m; every accepted plan is
then repropagated with the unquantized original state and model. The conservative
profile can remain nearly flat on this symmetric track. It does not establish
the optimal acceleration/braking envelope or an optimal line change.

Final scientific result: PARTIAL. One actual v5 native world ended in technical
DNF at67s, two laps and five bends,335 accepted13/9 decisions. Stage F stayed
closed. A fresh-state sixth certificate probe reproduced an out-of-range
steering finite-difference candidate. v6 fixes that numerical probe direction,
but its diagnostic continuation from the DNF state still violates the footprint
before the unchanged post-finish safety goal. v6 has no new native or full13/9
certificate result. `series-closure.json` prevents automatic native restart.
The v5 native source archive and every failure are retained; final source tests
and green CI must not be represented as faster full S3 evidence.
