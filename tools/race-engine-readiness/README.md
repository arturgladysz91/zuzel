# Production race readiness audit

Observation only, baseline `c13af7b8a16fe4e58e9d404c849cdf323649b0ed`.
Run commands from the repository root with .NET 8 and Python 3.

```sh
dotnet build tools/race-engine-readiness -c Release --warnaserror
dotnet run --project tools/race-engine-readiness -c Release --no-build -- pilot results/readiness-pilot c13af7b8a16fe4e58e9d404c849cdf323649b0ed
dotnet run --project tools/race-engine-readiness -c Release --no-build -- smoke results/readiness-smoke c13af7b8a16fe4e58e9d404c849cdf323649b0ed
# Inspect pilot cost before expanding. Batch is OFFLINE, never a CI default.
dotnet run --project tools/race-engine-readiness -c Release --no-build -- batch results/readiness-batch c13af7b8a16fe4e58e9d404c849cdf323649b0ed
python tools/race-engine-readiness/summarize.py results/readiness-batch results/race-engine-readiness.json
dotnet run --project tools/race-engine-readiness -c Release --no-build -- trace results/readiness-traces c13af7b8a16fe4e58e9d404c849cdf323649b0ed
# Capture the known ordinary-race P1, including reversed and unobserved reproduction:
dotnet run --project tools/race-engine-readiness -c Release --no-build -- repro results/readiness-blocker c13af7b8a16fe4e58e9d404c849cdf323649b0ed outside 7 C
dotnet run --project tools/race-engine-readiness -c Release --no-build -- repro results/readiness-partial c13af7b8a16fe4e58e9d404c849cdf323649b0ed three-squeeze 19 A
dotnet run --project tools/race-engine-readiness -c Release --no-build -- repro results/readiness-bend c13af7b8a16fe4e58e9d404c849cdf323649b0ed bend-catch 19 C
dotnet run --project tools/race-engine-readiness -c Release --no-build -- repro results/readiness-regain c13af7b8a16fe4e58e9d404c849cdf323649b0ed four-close-regain-binary 19 C
python tools/race-engine-readiness/inspect.py results results/readiness-traces results/readiness-bend results/readiness-regain results/readiness-blocker results/readiness-partial
python tools/race-engine-readiness/report.py results
# Resume only this same batch directory if an audit process was interrupted:
dotnet run --project tools/race-engine-readiness -c Release --no-build -- batch results/readiness-batch c13af7b8a16fe4e58e9d404c849cdf323649b0ed --resume
```

Pilot/smoke: two tracks × seed 19 × A/B/C = six evidence cases, each reversed
and without observer (18 production heats). Trace: four named cases × A/B/C,
plus reversed and full diagnostics reruns with exact final-state parity.
Batch: 27 race scenarios × seeds 7/19/83 × A/B/C + 33 one-variable settings
× seed 19 × A/B/C = 342 evidence cases, each reversed. Representative cases
also run without observer. Limits: ten minutes for smoke/pilot/trace,
90 minutes for offline batch. An exhausted limit throws, never reports success.
Offline batch/repro capture production exceptions as explicitly failed evidence,
with no fabricated race classification. Failed cases get reverse and no-observer
parity checks. `Completed` means all requested captures were processed;
`ProductionFailures`, `CompletedHeats` and per-case `Failed` distinguish race outcomes.
Smoke/pilot throw on any production failure. Each case is persisted before the next
heat. A known failure is reported as a P1 finding, not repaired or hidden by green CI.

The `worn` scenario prepares its surface using a prior ordinary production heat,
seed 7, heat ID 0, default options. Every measured heat starts with fresh riders,
heat ID 91, four laps, AdaptiveDecisionModel seed 1234. Rider identities and gate
assignments survive collection reversal. No simulation constants, adjustment
selectors, RNG draws or production decisions are added. A uses actual defaults
except the documented seed/weather. B changes only contested responses; C also
enables consequences. No legacy and physical consequence model is applied twice.

`Options` uses reflection to include public JsonIgnore properties, notably the
physical flag, parameters and diagnostics level. `Initial` records canonical
gameplay profiles, condition, legacy skills, setup and physical starting position.
Full traces use typed IEEE hexadecimal leaves, including JsonIgnore fields.
Hash checks cover all public resolved snapshot/decision diagnostics/motion/events,
final riders, classification, logs and every surface cell. Operational measurement
cost is excluded from hashes. CI builds the identical tool against original main
and the audit head, comparing exact hashes separately on each OS. All previous
strict cross-platform golden comparisons remain intact; issue #62 is untouched.

Motion comparison uses positive-duration stored-node pieces on the common heat
clock, continuous unwrapped canonical progress and strict lead signs. Piece
interpolation is observation of existing nodes, not a second traversal model.
No rider-ID tie break is used. Missing coverage or a zero-time progress jump resets
the comparison. A positive-duration interval spanning opposite strict signs
verifies a pass and gives a bracket, not an invented exact timestamp. Ties remain
ambiguous. Rolling scenarios have no tape-release phase. Boundary order compares
arrival times at the same canonical boundary, never equal segment-step snapshots.
Finish classification and crash gains are reported separately.

Native #55 poses/geometry observe competitive exposure without influencing the
heat. Exposure rows are intervals containing a near sample, and their duration
is an **upper bracket**, not an exact duration spent close. Alongside status uses
the existing rotated-footprint relation at that sample. Coverage gaps are retained.
Reference-point edge witnesses differ from an uncertified full-bike footprint;
certification failure alone is not proof of leaving the track. Width boundary
coordinate reinterpretations are not swept motion. Pair generation observations
copy the existing staged/owner closure state during OnStepResolved, before Commit;
the closure is never invoked by the observer.

Ordinary, rolling, controlled and extreme stress populations remain separate.
Rider-heat and heat sample counts are both reported. Seed 19 sensitivities are
conditional experiments, not estimates of population effect or professional ratings.
Diagnostic reruns explicitly use FullAudit; Summary main runs cannot report every
rejected candidate. The extra observer/geometry cost and production heat timing
are separate; timing includes the cheap capture observer and logging, excludes
post-hoc geometry, typed hashing and serialization. Allocations include workers
via GC.GetTotalAllocatedBytes. These are measurements, not performance assertions.
