# Study #153 — swept 0.5 m safety and bounded policy selection

Exact base: Draft PR #151 HEAD `a9024b5bb141e8158fc7c441705768a7b7354d35`.
Draft PR #152, branch `experiment/safe-fast-contingency-153`. No merge.
This isolated tool is **default OFF**. Original physics A/B, tyres, engine,
geometry, motorcycle dimensions, production defaults and historical evidence stay frozen.

Read the [scientific report](../../docs/experiments/safe-fast-contingency/REPORT.md),
`budget.json` and the actual native results before interpreting a successful prefix.
The overall finding is PARTIAL; local certified time gains do not establish a
faster complete race, and a stopped safety run is not a legal three-lap finish.

## The two regimes

- **L / Legacy:** unchanged152 physical guards: forecast pose reserve0.10 m,
  forecast/native sweep>=0 and beta<=45 degrees.
- **S / SafetyHalfMeter:** both physical capsules remain at least0.5 m from both
  boundaries over every swept interval. A centre offset cannot substitute for this.
  The constraint is used by proposal completion, all13 nodes/9 reserves, fresh
  independent replay and actual native enforcement before progress/finish credit.

`Clearance153` uses the original full-capsule distance and numerical allowances.
It requires the endpoint speed/yaw Lipschitz swept bound to be>=0.5 m. Failed
independent proof, missing/nonfinite clearance and nonpositive intervals fail closed.
`NativeWorld153.Generated.cs` changes only the named margin guard over the original
149 collector; the force calls, Box2D step, crank/wheel/steering commits, reaction
delay and all energy/contact/actuator checks are mechanically preserved.

## Exact work and algorithm changes

The exact adapter removes unused disabled kernel-cache keys, repeated boundary
delegate allocations and LINQ allocation in queue identity checks. The independent
replay has separate fresh state/cache/BVH ownership; prepared wheel outputs are
also checked with the original cold final-wheel equation. It cannot accept a
search cache entry or an old certificate as proof. The full exact L race checks
all276 original152 action/input/certificate digests and the final native fingerprint.
Historical observations are used only by the audit after the new chooser returns.

Ranking is a separate opt-in algorithm. First secure a complete independently
certified family. Every1 s consider at most two additional roots and at most three
certified policies. Compare common-goal worst-reserve time, nominal time, onward
speed, path length, coast time and swept reserve. No preferred line or peak-speed
reward is introduced. The final bounded variant tries revalidated retained
policies/feedback only and refuses expensive optional tail regeneration; an
eight-K1 optional limit cannot weaken any accepted13/9 certificate.

The failed nominal-time v1 and expensive v2 are preserved with complete source
archives and negative native results. They are not promoted by the final CLI.

## Budget and reproduction

Limit: **1500 process CPU-s /20 world reservations**, including negative attempts,
bulk saved-data analysis and reports. A write-ahead locked ledger retains every
reservation and rejects duplicates/overlap.40 CPU-s is reserved for final export.
Compilation, standard regression tests and GitHub CI are outside this experiment
budget. Do not erase the supplied ledger to continue tuning.

Build and test the sealed evidence:

```powershell
dotnet restore tools/safe-fast-contingency --locked-mode
dotnet build tools/safe-fast-contingency -c Release --no-restore --warnaserror
python tools/safe-fast-contingency/generate153.py --check
dotnet test tests/CoreSim.Tests -c Release --filter FullyQualifiedName~SafeFastContingency153Tests
python -m unittest discover -s tests/calibration -p test_safe_fast153.py
node --test tools/replay-viewer/safety153.test.mjs
```

Create a fresh **output directory** for a new reproduction. `SPEEDWAY153_OUTPUT`
affects evidence paths only; it never changes a physical or planner parameter.
Keep the existing GitHub gate receipt as a prerequisite, then generate new
geometry/benchmark results before native attempts:

```powershell
$env:SPEEDWAY153_OUTPUT='work/reproduce153'
New-Item -ItemType Directory -Path $env:SPEEDWAY153_OUTPUT -Force
Copy-Item docs/experiments/safe-fast-contingency/ci-gate1.json $env:SPEEDWAY153_OUTPUT
dotnet tools/safe-fast-contingency/bin/Release/net8.0/safe-fast-contingency.dll --geometry153
dotnet tools/safe-fast-contingency/bin/Release/net8.0/safe-fast-contingency.dll --bench-exact153
dotnet tools/safe-fast-contingency/bin/Release/net8.0/safe-fast-contingency.dll --native-exact-L153
dotnet tools/safe-fast-contingency/bin/Release/net8.0/safe-fast-contingency.dll --native-ranked-L153
dotnet tools/safe-fast-contingency/bin/Release/net8.0/safe-fast-contingency.dll --native-ranked-S153
```

Every native attempt is one continuous Box2D world from rest with no authored
commands. Limits remain30 CPU-s/review,32768 forecasts and2 million contact queries.
All signs and both observation levels remain. S can start only after the completed
exact and ranked L prerequisites. Full B was not run; it is gated by the unmet
earlier requirements and available budget.

| Flag | Scope |
|---|---|
| `--geometry153` | Fresh read-only full-capsule audit of saved148/152 native states; no new world |
| `--bench153` | Preserved five-state151 exact benchmark, including the29.8 s CPU-cap failure |
| `--bench152-153` | Preserved prepared/memo experiment on five completed152 observations |
| `--bench-exact153` | Selected exact adapter on the same five152 observations, paired with original152 |
| `--native-exact-L153` | Full autonomous L, checked against all original152 physical digests |
| `--native-ranked-L153` | Final bounded ranked L; independent finite certification retained |
| `--native-ranked-S153` | Full-distance S attempt; strict swept0.5 m, actual outcome retained |
| `--native-cold-L153` | Same-session original152 timing control with unchanged chooser |
| `--native-cold-prefix-L153` | Selected0–12 s original152 timing control; explicitly not a complete race |

`sources/*.zip` contains the exact tool source at each actual attempt, including
negative versions and harness fixes. `sources/*.json` hashes that archive. Old
attempts may require extracting their source snapshot into an isolated checkout;
their former flags are not silently mapped to a different implementation.

## Evidence and replay

- `native/`: dense actual states, forces, energies, queues and activations, plus
  hash-chained decision journals and total timing results.
- `policies/`: every accepted finite policy, failed attempts and complete shared
  tape inputs. Dense forecasts can be reconstructed from those exact inputs;
  their full physical digest covers states, commands, queues, wheel/crank and safety.
- `history/` and `historical-violations.csv`: separate0.5 m revalidation. Historical
  certificates are untouched.53.989766 s and55.167079 s remain L results.
- `decision-counts.csv`, `timings/`: phase/counter profiles and complete callback
  timing where instrumented; the report distinguishes the quantile scopes.
- `source-proof.json`: immutable source/blob and150 historical-tree checks.

From the repository root, start `python -m http.server 8000`, then open
`http://127.0.0.1:8000/tools/replay-viewer/?case=safety153`. The run selector
explicitly distinguishes L/S, partial attempts and actual completed races.
Presentation decimation is never used for safety or race-time measurement.

The finite correlated scenario certificate is not a global disturbance guarantee
or a four-rider readiness result. The actual native margin still decides whether
a completed run can pass its active regime.
