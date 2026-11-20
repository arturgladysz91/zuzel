# #202 evidence

`*-same` are the identical-command attribution runs; `*-learned-*` are separately
trained native policies. All eight raw Run records are preserved. Three old-policy
DNFs are retained. `world-inventory.json` counts both research runs and regression
micro-worlds: 26 constructors in total, exceeding an all-constructions limit of 8.

Development directories preserve failed candidates, transfers and training logs.
`checks-final` contains non-native property checks and the loss sensitivity matrix.
`scenarios-v3` contains MODEL force reevaluations and MODEL timestep convergence.
`*-telemetry.csv.gz` reconstructs accepted native steps and the full energy ledger.
No model estimate is labelled as a new native trajectory.

`final-physics-parity.json` proves exact final force/shaft/steering/lean reevaluation
on all 25,306 accepted steps. `runtime-controller-parity.json` proves the three
published controllers reproduce every recorded decision. Both create zero worlds.

TRX hashes and counters are published in the inventory; original files remain in
the local output folder. Host/user/path metadata from those logs is not published.
The compiled executable hashes of the actual native runs are in `*.reserved`.
The final source also contains runtime packaging and additional read-only checks;
its numerical equivalence is established by the exact parity checks above.

The report is in `docs/experiments/speedway-h26/REPORT.md`. No merge, new study,
empirical calibration, full robustness PASS or sporting speed gain is claimed.
