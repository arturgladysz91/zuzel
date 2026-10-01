# Combined-grip real-data audit (#50)

Status: **PROVISIONAL / ANALYSIS ONLY / OUTCOME C / NO EMPIRICAL q SELECTED**.
Online research and repository inspection: **2026-10-01**.
Research baseline: PR HEAD `9a82e86685b767859a2891ae3ab0dd619d883877`;
main `58771bac635d7d4962e11aa3ae5ad37a2d0c8d94`.

## Decision and scope

**C: the accessible evidence does not support a numerical calibration or an
empirical bound on this implementation's coupling parameter.** Real measurements
exist, including speedway heat/lap summaries and adjacent deformable-surface
tyre-force experiments. None of the inspected accessible inputs supplies a
synchronized, uncertainty-qualified speedway corner trajectory plus powered
acceleration and the independent drive/capacity anchors needed here.

This is a bounded audit, not a claim that no such data exists anywhere. Do not
interpret C as evidence for q=0, or the existing design domain `0 <= q <= 1` as
an empirically supported interval. No q, force formula, physics, optimizer,
geometry, reserve threshold, or historical numerical report is changed.
There is no real-data calibration report because no fit was performed.

This research-first decision supersedes the *next-work recommendation* in the
[execution-reserve conclusions](combined-grip-execution-reserve-conclusions.md),
not its historical measurements. No additional crossover or reserve search was
run for this audit. The [calibration plan](combined-grip-calibration-plan.md)
defines the evidence required before another simulator consequence check.

## Search coverage and access limits

Primary-source searches followed the requested hierarchy: speedway scientific
papers; academic speedway/loose-surface tyre studies; actual GPS/telemetry;
official operator/provider descriptions; metrically usable video; venue geometry
and timing. Search terms included `speedway motorcycle GPS telemetry corner
acceleration`, `speedway motorcycle lateral acceleration`, `speedway friction
motorcycle thesis`, `speedway motorcycle loose surface combined lateral
longitudinal traction`, and Polish `żużel telemetria GPS Pegasus BlackBurst`.
Targeted searches of Zenodo and Figshare did not return a relevant synchronized
speedway corner dataset. References in the reaction-time paper were followed
for first-corner positioning and the Speed Your Way training application.

Only primary academic repositories, publishers, operators, clubs and venue
owners support the findings below. Forums, unscaled clips, copied wiki tables
and unsourced friction coefficients were not calibration inputs. Search-index
extracts are distinguished from directly opened pages/full text. Dates below
come from the source, not relative search-result timestamps.

No private dataset was accessed, no subscription purchased, no author/provider
contacted, and no video downloaded or measured. Failed retrieval is an access
limitation, not proof that a source contains no further data. The earlier #40
public-feed schema audit was reused with its original date; it was not repeated
or represented as a fresh scan of every current match.

## Source register

All external URLs below were researched on 2026-10-01. "Unknown" means that a
publication date was not verified; it is not inferred from a copyright year.

| ID / primary source | Author / organization; date | Actual usable quantity | Quality, access and calibration limit |
| --- | --- | --- | --- |
| S1 [Speedway reaction-time study](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0281138) | Markowski, Szczepan, Zatoń, Martin, Michalik; 2023-01-27 | Reaction times from 1,261 observations, 65 riders, 22 matches in 2021; Pegasus measurement-method description | Peer-reviewed speedway study, full HTML inspected. Describes 25 Hz GPS/triangulation and claimed 14 mm spatial accuracy. These are reported specifications, not independently verified derivative error. Published outcomes are reaction/scoring, not corner traces. Data Availability explicitly restricts access through ethics approval and joint research; no public raw `t,x,y,v` export obtained. |
| S2 [Deformable-surface combined-force thesis](https://etd.auburn.edu/handle/10415/4630) | David Michael McIntyre, Auburn University; repository 2015-05-11, thesis cover 2015-05-10 | Measured lateral/longitudinal tyre forces under traction/braking, normalized force-envelope plots | Academic bench experiment; abstract and relevant full-text sections inspected. Test tyre is a Kenda K299 ATV tyre on remoulded clay, not a speedway tyre/motorcycle on shale. Useful evidence that combined-force behaviour can be measured; no transferable numerical q or speedway acceleration envelope. |
| S3 [Speedway aerodynamic thesis record](https://upcommons.upc.edu/entities/publication/9d7270d6-8c42-45f7-99d5-e1d93092dcb9) and [DTU conference abstract](https://orbit.dtu.dk/en/publications/numerical-simulations-of-the-aerodynamics-of-a-speedway-motorcycl/) | Iván Pujol Vidal, UPC/DTU/Team Danmark; thesis 2025-02. Conference: Pujol et al., 2025-11-23 to 25 | Wind-tunnel/CFD aerodynamic drag, not powered corner acceleration | Thesis metadata/abstract read through primary repository indexed text; thesis PDF retrieval failed. DTU abstract directly accessible. Possible future independent resistance evidence, not tyre coupling or a riding trajectory. No drag values imported into the simulator. |
| S4 [Motorcycle GPS trajectory method](https://www.tandfonline.com/doi/pdf/10.9746/jcmsi.4.199) | Yuichiro Koyama, Toshiyuki Tanaka, Keio University; journal issue 2011-05 | Motorcycle position reconstruction, missing observations and smoothing; 0.05 s GPS epochs | Primary publisher indexed abstract/full-text excerpt inspected; direct retrieval failed. Motorcycle measurement methodology, not a speedway loose-surface force dataset. Interpolated/smoothed position accuracy does not itself establish local curvature or acceleration accuracy. No numerical coupling transferred. |
| S5 [Ekstraliga telemetry guide](https://ekstraliga.pl/se/news/telemetria-w-pge-ekstralidze-przewodnik) | Speedway Ekstraliga / Black Burst; visible day/month 06-06, year unknown | Definitions of displayed timing, Vmax, distance, time-to-70, speed-after-2-s and first-corner-entry metrics | Official explanatory page directly read, not a sample-series export or uncertainty certificate. Its advertised physical displays do not make similarly named categorical fields in the repository numeric. The linked legacy telemetry page could not be retrieved in this session. |
| S6 [Pegasus provider offer](https://blackburst.pl/oferta-2/pegasus) | Blackburst; publication date unknown | System capability and a potential data-acquisition contact | Official provider page directly accessible, but marketing-grade evidence. No downloadable, synchronized corner dataset or independent error specification found there. The linked Pegasus website could not be retrieved. Not a numerical calibration source. |
| S7 [Frozen PGEE snapshot](../../data/calibration/pge/v1/README.md) and [prior #40 source audit](real-start-telemetry.md) | Repository source package; generated 2026-09-05; raw/schema verification 2026-09-16 | L1-L4 times, heat time, heat Vmax and total ridden distance; 95 matches / 1,593 attempts / 6,373 rider observations | Versioned, offline, provenance-hashed real summary data, inspected locally. No synchronized local speed, position or acceleration. Motoarena subset: 7 matches / 114 attempts / 456 rider rows. No physical corner series recovered. Original [manifest](../../data/calibration/pge/v1/source_manifest.json) is unchanged. |
| S8 [Motoarena venue specification](https://torun.pl/pl/print/node/41225?entity_type=node) | City of Toruń; publication date unknown | Listed track length 325 m, straights 62 m, bend radius 31 m, widths 12/18 m and banking | Official page accessible. Nominal facility dimensions, not the motorcycle's ridden radius, landmark survey or a timed line. In particular, `length / heat time` cannot recover apex speed. These dimensions do not revise the existing fixture. |
| S9 [Belle Vue venue and records](https://www.bellevue-speedway.com/the-national-speedway-stadium) | Belle Vue Speedway; page date unknown; listed record 2019-08-26 | Listed 347 m track and Dan Bewley's 58.18 s Premiership track record | Official club page directly read. Useful second-venue geometry/timing lead, not synchronized lap-by-lap corner observations. The listed track record is not a local corner time; actual ridden length differs from nominal track length. |
| S10 [Official Toruń qualifying replay listing](https://fim-moto.tv/player/videos/b30e1edd-8acc-41a3-8e0c-1828519e7bd2/fim-speedway-gp-of-poland-torun-poland-round-10-qualifying) | FIM-MOTO.TV; event 2026-09-26 | Potential video of isolated qualifying runs | Official indexed listing found; viewing requires event access, direct retrieval failed. Footage was not inspected. No original frame timestamps, camera calibration, surveyed visible landmarks or measurement residuals obtained. No usable timecoded measurement segment certified by this audit. |

S2 is genuine combined-force data, but from the wrong tyre/terrain/vehicle.
It cannot supply even a numerical bound for the game's engine-normalized q
without an independently justified transfer. S3 measures a different force.
S4 supports measurement planning only. S1 and S6 identify potential custodians,
not data already delivered to this project.

The Speed Your Way thesis is a citation-only lead in S1: Kusznir (2015), Wrocław
University of Science and Technology. A primary full text was not located;
neither that application's outputs nor its accuracy are treated as inspected
measurements. First-corner position/race-result associations likewise do not
provide first-corner physical velocity, curvature or drive force.

## Availability by required observable

| Required observation | Available now | What remains missing |
| --- | --- | --- |
| Entry / apex / exit speed of the same bend | No usable matched numeric triplet | Time or arc-length location, units, uncertainty, rider/lap/attempt IDs |
| Second-half bend acceleration | No | Local `v(t)` or `v(s)` at sufficient validated bandwidth |
| Ridden radius / curvature | No | Survey-referenced `x(t),y(t)` and differentiation/error model |
| Tight versus wide line | Only heat-distance summaries / qualitative description | Matched runs at similar speed, equipment, surface state and drive request |
| Ridden path length | Heat total only | Local sector path and entry/exit boundaries |
| Bend / sector traversal time | Lap and whole-heat times only | Consistent bend/sector crossings linked to the same trajectory |
| Synchronized speed + trajectory + acceleration | **No** | A single usable time axis and independent measurement validation |
| Applied drive / capacity-active state | No | Known or independently justified positive-drive request and at-speed engine force |
| Independent lateral / longitudinal scales | No speedway-compatible pair | Capacity convention, straight-drive anchor, mass/resistance/banking effects |

The frozen snapshot has 5,410 CompleteTelemetry rows, including 5,328
CleanPhysics and 82 Eventful rows; 963 more are AuditOnly. These names concern
heat/lap-summary completeness, not corner-sample completeness. Its
`gate_rank_speed_2s` and `gate_rank_curve_speed` are ranking categories, not m/s
or km/h. Missing numbers remain missing. Synthetic #40 start diagnostics and
the trajectory optimizer's traces are not real observations.

Lap time, maximum speed and total distance constrain aggregate performance,
but allow many local speed/curvature/acceleration histories. Substituting the
nominal 31 m venue bend radius for each rider's curvature would assume the
very tight/wide trajectory effect being investigated. No empirical lateral
versus longitudinal relation, q bound, or confidence interval follows from
these aggregates without inventing unobserved inputs.

## Multi-track availability and video verdict

Motoarena stays the existing fixture, not the calibration target. S7 covers
multiple venues, but none supplies the required series. S8/S9 show that another
venue's dimensions/timing can be sourced; they do not establish that a usable
second telemetry dataset exists. Length alone cannot classify a track as tight
or fast. Obtain surveyed curvature/width and actual riding data before choosing
a tighter/technical and a broader/faster validation pair.

No inspected video input passes the numerical-data gate. S10 is an acquisition
lead, not a measured clip; its measurement error is **unknown**, not estimated
from visual impression. A licensed original, reliable timing, camera/landmark
calibration and quantified trajectory residuals are necessary. The plan derives
how position noise propagates and how to decide whether a future clip is only
qualitative. No broadcast-derived speed or curvature is asserted here.

## Answers to the thirteen research questions

1. **Real data:** PGEE heat/lap time, Vmax and ridden-distance summaries;
   published speedway reaction observations; adjacent clay/ATV force experiments
   and speedway aero work. No accessible calibrated corner series.
2. **Quality:** good primary provenance for their actual quantities, insufficient
   resolution or domain transfer for this parameter. Provider claims are leads,
   not independently validated calibration measurements.
3. **Speed + trajectory + acceleration:** not jointly available in usable form.
4. **Empirical lateral/longitudinal relation:** not computable from current
   speedway inputs; possible with qualified synchronized trajectory data.
5. **q identifiable:** not now. Even with kinematics, capacity scale and drive
   request/force must be anchored; the formula contains `q / capacity²`.
6. **Reparameterization:** keep code unchanged. For research, report the equivalent
   `beta = q / capacity²` or cutoff acceleration; see the algebra in the plan.
7. **q range:** no empirical numerical range or upper bound. `0..1` is design only.
8. **Uncertainty:** no fitted estimate, so no fit confidence interval. Measurement,
   differentiation, force-anchor and domain-transfer errors are unresolved.
9. **One-track dependence:** there is no fit; future validation must hold out
   venues, riders and sessions, not calibrate only to Motoarena.
10. **Correction:** retain separately as effective scrub; no braking ellipse or
    invented decomposition of its negative action.
11. **Manager simplicity:** structurally yes: one positive-propulsion allocation,
    no full tyre model, new skills, modifiers or UI. Empirical validity unproven.
12. **Production basis:** no. Structural plausibility and synthetic crossover are
    not empirical validation or authorization for production promotion.
13. **Missing inputs:** licensed synchronized `t,x,y,v`, validated uncertainty and
    geometry, powered/capacity-active runs and independent force/capacity anchors,
    matched tight/wide passages plus multi-venue held-out observations. The plan
    specifies the delivery schema and falsification gates.

**Stop here: retain provisional research, select no q, keep PR #50 Draft and
unmerged.**
