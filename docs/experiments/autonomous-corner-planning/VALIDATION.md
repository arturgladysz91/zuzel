# Study146 validation

Local Windows validation:

- Restore and Release solution build with warnings as errors: passed.
- Complete Draft C# scope: 2464 passing cases (2069 core, 395 trajectory). Seven new domain/evidence tests cover default OFF, common maneuver goals, queue/crank/actuator immutability, independent safety rejection, native diagnosis, forecast CPU guard and deterministic forecast cap. The final targeted seven cases also pass.
- Python calibration: 268 passing tests, including five new scientific evidence gates. CI policy Python: 7 passing tests.
- Node replay: 70 passing tests, including three new Study146 tests.
- Complete discovery: 2985 cases, each assigned exactly once (2069 core, 395 trajectory, 473 historical-analysis, 48 four-rider). Historical/four-rider shards follow the repository's existing Draft policy; they are not claimed as locally executed here.
- Generator parity for DiagnosticWorld145: passed. Legacy production reader audit: all 39 frozen consumers unchanged.
- Frozen directories compared to exact base 3b9180d957bcc596c5e8b2756f280a65fb40670f: no changed physics, #144 or #145 evidence. See frozen-check.json.
- Native Stage A sequence checks and exact prefixes pass. Forced execution of the first selected plan agrees to the common goal. Three retrospective executed-sequence audits pass; those are not recovered planner intent.
- Viewer checked at t=29 s: correct native pose/telemetry, six-corner table, CPU stop, explicit partial archive coverage. Scientific comparison PNG inspected. No extrapolated third-lap finish.

Windows CI is required on the published Draft head. The exact tested SHA, workflow URL and final result are recorded in the PR description and the delivered validation JSON, so the source document does not require a self-referential CI-only commit.

A green test/CI result verifies implementation and evidence gates. The scientific outcome remains PARTIAL: two laps, CPU stop, missing 120 full plan logs, no complete autonomous three-lap success. Final safety/resource guard and incremental journal changes are covered by build/tests; no further full native trial was run after them.
