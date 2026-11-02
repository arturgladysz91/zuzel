# Study160 — PARTIAL / OFF

[Full report](REPORT.md) · [standalone native replay](replay.html) ·
[validation](VALIDATION.md) · [SHA256 manifest](evidence-manifest.json).

One continuous original native world, from rest, ended technical DNF at67s,
two laps and five bends;335 accepted commands had fresh13/9 + independent
preflight. No S3 completion, no held trials. The first bend was8.020s against
10.679s in the exactly paired158 baseline; this is a local improvement only.

Source-v5 is captured in sources/native-dev-v5.zip. Final source-v6 fixes an
out-of-range finite-difference probe; it has diagnostic forecasts and regression
tests, no new full13/9 or native S3 proof. All failures remain. Native series
is explicitly closed by series-closure.json. Neither CI nor78.2s diagnostic
forecast counts as autonomous completion. No merge.

Raw100Hz native/ and full policies/ retain forces, wheel/crank/clutch, all
queues,335 accepted certificate DAGs and actual samples. telemetry/ adds CSV.gz
with theta/chi/beta, yaw, actual XY curvature, requested/active controls, Fx/Fy,
grip utilization, pose and swept. Analysis records all six bends, straight
sections, failed generation, CPU, and full-state pairing. Replay uses original
sample indices and freezes at the final actual sample.
