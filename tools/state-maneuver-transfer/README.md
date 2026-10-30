# State-dependent maneuver transfer #157 (OFF)

PROVISIONAL research harness. Exact base: `9fe4d6612ce13c135fa967d48faa85509257d129`.
No production integration, physical parameter edits, K1 search, or native body restoration.

`Policy157` embeds an unordered bag of current-state/action examples. It cannot load
teacher trajectories. `Generator157` re-evaluates proposals after each predicted observation.
`Planner157.Generated.cs` preserves the #155 finite 13-node/9-full-reserve certificate,
with bounded learned proposals and joint endogenous-plan feedback. Old plans are proposals,
never certificates for new states. Original independent preflight is mandatory.

Explicit commands (unique labels; all experimental processes charged):

```powershell
dotnet run -c Release --project tools/state-maneuver-transfer -- --probe first label
dotnet run -c Release --project tools/state-maneuver-transfer -- --generation second label
dotnet run -c Release --project tools/state-maneuver-transfer -- --heldset historical label
dotnet run -c Release --project tools/state-maneuver-transfer -- --native historical label
dotnet run -c Release --project tools/state-maneuver-transfer -- --native-window historical label
dotnet run -c Release --project tools/state-maneuver-transfer -- --causal unused label
```

`--generation` is forecast diagnostics without a 13/9 certificate; its actions are never
executed. `--native` tests0.8s of actual observations after a physically replayed prefix.
`--native-window` may attempt the local common section, only with fresh certificates.
Neither is a new full-race S claim. `Diagnostics157` alone reads teacher tapes for a
counterfactual pending analysis; it is never called by the policy/planner.

Training and algorithm freeze predate first B outcomes and remain byte-identical.
Do not retrain into the same ledger or tune against B silently. CPU budget1000s and
native-world cap15 are cumulative. Failed attempts/exports remain charged.

Result: partial A/B, C/D gated off. See [report](../../docs/experiments/state-maneuver-transfer/REPORT.md).
