# C #200 in the stadium

Select the experimental C #200 controller in the existing stadium and start a new solo heat. The UI selects gate D and 8:1. BASE is the default.

The standard simulator also accepts `--stadium-race config.json output-directory` with this explicit configuration.

```json
{"Ratios":[8],"Controller":"speedway200"}
```

The embedded learned profile initializes the FAST50 controller. Before activation the complete remainder is checked from the actual state with its pending requests, wheel, shaft, steer and lean. Runtime updates use the inherited certified switch mechanism. Archives are written through the same H25 export path as existing races.

The validated setup is solo D at 8:1. Other configurations produce a validation error before world creation. Two and four rider controls remain available for BASE and #194.

Run `dotnet run --project tools/autonomous-traffic191 -c Release -- --check-speedway200` to check the embedded model, rejected opt-in configurations and pending/review behavior. This command creates no native race world.

The complete #200 training sources are preserved in `tools/speedway-study200`. The original training archive and native evidence are in `data/experiments/speedway200`. Research training commands accept the historical workspace archives as inputs. Production race execution needs the embedded profile and standard repository files.

Study #201 is in `tools/speedway-study201`. Its report and native evidence are in `docs/experiments/speedway-201`.
