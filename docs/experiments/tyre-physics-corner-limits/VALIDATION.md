# Study149 validation

- Exact base: `14cb6c2fb947d2d04b1a7979346c8947b131b4fd`, PR147 head. Draft PR148, no merge.
- `dotnet restore SpeedwayManager.sln`, Release build with `--warnaserror`: successful.
- Local complete Draft responsibility: **2485/2485 C# cases**, core2090 + trajectory395; no skips. Six new149 tests cover dt=0 inertia separation, retained dynamic inertia, identical native/forecast implicit clutch response including off-reference Iw, uncached independent preflight and default OFF.
- Complete discovery: **3006 cases**, exactly one shard each; historical473 and four-rider48 remain separately assigned, not claimed as executed in this Draft run.
- Python calibration suite: 290 cases (including eight149 evidence/generator checks); tools/ci has seven additional policy/profile checks. Node saved-replay suite: **79/79**.
- Both generated adapters checked: `generate148.py --check`, `generate149.py --check`. Old physics, geometry, parameters and historical evidence unchanged; canonical Git path comparison and frozen SHA256 in `source-proof.json`.
- Native final148 reproduction: all5412 samples and270 choices identical. New controller CPU83.15625 s. NativeB and repeat: all samples and choices identical, three complete laps54.234943598 s, no contacts or boundary violations. B is0.245177924 s slower than A.
- All48 new native worlds, including incomplete prefixes and failed interventions, remain in the experiment ledger. No saved-body restoration, forced speed during a run, custom body integrator, mu increase or future native observations were used by the autonomous controller.
- Native predictive checks: four explicit fixtures, including44 B corner comparisons with maxXY3.066e-5 m. Full journals validate irrevocable queues, chosen actions and independent certifications before execution.
- Beyond-beta45 diagnostics retain continuity, passivity, total energy, shared capacity, actuator, forward and swept-footprint checks. Uncertified sweep is distinct from confirmed crossing; planned window ends are distinct from full finishes. No outside-envelope race PASS.
- PNG force and six-corner speed figures visually inspected; no clipping/overlap. Replay data loaded by Node and checked via local HTTP. Browser visual QA unavailable: CUA initialization fails with `failed to write kernel assets ... os error3`; no browser-rendering PASS is claimed.

Windows CI is required on the exact current PR head; verify the [PR148 checks](https://github.com/arturgladysz91/zuzel/pull/148/checks). The delivered `Windows-CI149.json` records the final verified head and check outcomes. Draft policy builds once on Windows, proves complete discovery, runs core/trajectory and Python/Node; historical capture jobs are skipped by Draft policy. These skips are not reported as passed tests.

The raw native result field `SportingImprovement` retains its inherited comparison with historical K1 (54.794529839 s). It is not the A/B success criterion: B did not improve on final148 A. Report and tests explicitly enforce this distinction.
