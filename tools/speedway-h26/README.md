# Speedway H26 — experimental, default OFF

H25 remains the default. H26-L adds coupled quasi-static axle loads; H26-E adds
explicit engine loss hypotheses; H26-LE combines them. COM height and engine losses
are unmeasured assumptions. Full throttle retains the original brake torque map.

The regular `uruchom-stadion.ps1` offers all three H26 variants with separately
learned, embedded solo 8:1 policies. H25 C #200 and the older modes remain available.
Incompatible setups are rejected before a native world is constructed. The
historical portable #192 ZIP is not modified.

See [the report](../../docs/experiments/speedway-h26/REPORT.md), including the
budget deviation: eight research runs plus eighteen legacy regression micro-worlds.
Native records and unsuccessful development searches are in
`data/experiments/speedway202`. Neither empirical calibration nor faster racing is
claimed. The old strict direction/technique checks are reported without retuning.

## Build and checks without native worlds

From the repository root on Windows, using .NET 8 or a compatible SDK:

```powershell
dotnet restore tools/speedway-h26/H26.csproj --locked-mode
dotnet build tools/speedway-h26/H26.csproj -c Release --no-restore
$exe = 'tools/speedway-h26/bin/Release/net8.0/H26.dll'
# Use a fresh directory: evidence writers deliberately do not overwrite files.
$checks = Join-Path $env:TEMP ('h26-check-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checks
Copy-Item data/experiments/speedway202/*-same.json.gz $checks
Copy-Item data/experiments/speedway202/*-learned-C*.json.gz $checks
dotnet $exe check-policies . $checks
dotnet $exe force-parity . $checks
dotnet $exe replay-controllers . $checks
dotnet $exe check-matched . $checks data/experiments/speedway202/matched-probes.json
dotnet $exe properties . (Join-Path $checks 'properties')
```

These commands evaluate saved states and forecasts. They construct no Box2D
world. The final forces, shaft, steering and lean reproduce 25,306 accepted native
steps exactly; the embedded L/E/LE controllers reproduce 991/942/998 recorded
decisions. The pure `H26PhysicsTests` also create no native world. Other historical
regression tests can create short worlds and must be counted in a native budget.

`analyze202.py OUTPUT_DIRECTORY` reconstructs per-step native energy and trajectory
metrics from the eight raw records in a copied output directory. Geometry comes
from the repository replay. Interpolation is used only to measure section crossings.
The original `EnergyExcessJoules` is a one-sided passivity envelope; the reconstructed
two-sided balance also includes tyre/contact and body resistance work.

## Research entry points

`train`, `train-physical`, `refine`, `cycle`, and `changed` search autonomous
feedback proposals offline. Warm references are not executed as native prefixes.
All final candidate rollouts start from the actual initial state. `matched-train`
requires the archived #196 workspace inputs. `check-matched` instead reads the
published exact matched root and proves the irrevocable prefix rejection.

`same-native` and `native-learned` reserve a budget marker before constructing a
world. **The #202 budget is closed. Do not execute these commands as part of
reproduction or validation.** Their results are already published. Their local
marker guard does not count worlds created by historical test suites; the report
and world inventory explicitly correct that accounting limitation.

The runtime uses one Box2D step and then commits matching wheel, shaft, steering
and lean states. Predictions use the same configuration and pending queue. The
mean loss model is not an ignition/fuel-cycle model; wheel unloading remains an
explicit two-contact validity failure, with no hidden negative-load clamp.
