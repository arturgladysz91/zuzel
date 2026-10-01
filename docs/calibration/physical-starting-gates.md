# Physical starting gates — current geometry

Baseline: main `d87733a3d59d87bcf05e80c21ed53221d14d6a8f` after #51.
Source audit: 2026-10-01. This is a geometry foundation, not gate-performance calibration.

## Source audit and uncertainty

The [Speedway Ekstraliga 2026 Toruń venue card](https://ekstraliga.pl/se/druzyny/1/2026/info)
lists length 318 m, both straights 12 m wide and bends 17.0/16.2 m wide.
The [municipal stadium description](https://www.torun.pl/pl/print/pdf/node/41225)
supports 62 m straights and a published 31 m bend radius, but also contains older
325 m/18 m figures. Those older length/width values do **not** override #39's
current venue card. The radius measurement convention remains unverified;
the model still treats it as a reference-radius approximation. Symmetric modeled
bend width remains the 16.6 m arithmetic-mean proxy, not a claim of local symmetry.

[FIM Track Racing Circuit Standards 2026](https://www.fim-moto.com/fileadmin/user_upload/Documents/2026/2026_FIM_standards_Track_Racing_circuits.pdf),
§79.7.1–79.7.2, printed pp.17–18, specify equally divided starting fields,
5 cm longitudinal markings extending 1 m backward, and one full-width,
perpendicular start/finish line. The prose puts the line at mid-straight,
with a 35 m minimum before the first bend if central placement is impossible.
The starting-area figure on printed p.42 instead states a minimum of 35 m
without that condition and illustrates W/4. The model conservatively adopts
35 m as its baseline; this is not a ruling on Motoarena homologation/compliance.

[PZM RZMot 2026, revision 2026-03-10](https://www.pzm.pl/pliki/zg/zuzel/2026/02-rzmot-2026-20260310_0.pdf),
Art.69(1–2), printed p.68, places the marshal at the B/C midpoint and requires
parallel positioning, a front wheel at most 10 cm from the tape and the whole
rider/motorcycle inside the field. These are real-world context, not newly
implemented entities, motorcycle dimensions or longitudinal advantages.

The audit consulted the FIM standards, PZM 2026 track regulations/RZMot,
Ekstraliga's venue card, club/municipal venue information and searches for a
Motoarena homologation/inspection plan or dimensioned start-line offset.
No reliable exact start→first-bend measurement was found in those public
sources; the operator page could not be retrieved. No image-derived metres,
homologation exception or telemetry-derived offset is asserted.

## Current Motoarena baseline (not a measured offset)

Classification: **FIMConstrainedStartLineBaseline / ExactMotoarenaOffsetNotPubliclyVerified**.

`START/FINISH → 35 m → FIRST BEND → 62 m BACK STRAIGHT → SECOND BEND → 27 m → START/FINISH`

The home straight remains `35 + 27 = 62 m`. The same segment-zero boundary is
the start and every lap finish; `Track.StartFinishLine` only names that existing
canonical origin. Standing-start distance zero means tape/start line for every
rider. The published 318 m, modeled radius 31 m and widths are not tuned.
The reference-line L0 lap remains approximately 318.778748 m, not forced to 318 m.

For a fixed racing line, relocation preserves every full flying-lap path length
(and therefore L2/L3/L4 geometry). It does not promise identical dynamic lap
times: existing segment integration, first-bend preparation and decisions can
respond to the different straight split. Nothing is fitted to L1 telemetry.

## Full-width fields and explicit transform

`StartingGateGeometry` divides **physical width**, not usable width or five lanes.
For W=12 m, nominal width is 3 m; W=10 m gives 2.5 m.

| Field | Physical bounds from inner edge (m) | Neutral center (m) | Derived racing coordinate |
| --- | --- | --- | --- |
| A (inside) | [0, 3) | 1.5 | ≈0.2 |
| B | [3, 6) | 4.5 | ≈1.4 |
| C | [6, 9) | 7.5 | ≈2.6 |
| D (outside) | [9, 12] | 10.5 | ≈3.8 |

Shared-edge ownership is half-open except at the final physical edge, so there
are no gaps or overlapping assignments. Internal marking offsets are 3/6/9 m;
0.05 m width and 1 m backward length are metadata and do not reduce nominal fields.

The existing Motoarena racing references are inset 1 m at each edge:
`usable = 12 − 1 − 1 = 10 m`. The transform subtracts the inner 1 m reference
offset from the center, then delegates to
`LaneModel.LateralPositionFromPhysicalOffsetMeters`; equivalently
`lateral = (center − innerReferenceOffset) / usableWidth * LaneModel.MaxLane`.
The four normalized values are derived, never production constants. The nearest
discrete racing reference supplies compatibility `Lane`, not field identity.
Unrepresentable centers on narrow synthetic geometry are rejected, not clamped.

## Starting-state and transition contract

`StartingGrid.Create(track, assignments)` takes four unique profiles with four
explicit distinct fields, independent of RiderId and input order. All start with
zero progress/distance/speed/time and `NotStarted` status, each at its physical
field center. Bounds and gate identity survive detached snapshots and the old
decision adapter as initial metadata. Individual explicit placement/reset is
also supported; ungated constructors/reset remain compatible.

Launch samples the existing continuous five-band surface at the neutral center.
Fields may sample different stored surfaces, but no synthetic gate grip/force,
reaction bonus or scripted first-corner order is added. On tape release the
existing TargetLane/PlannedLane/LateralMovementModel path applies immediately.
Starting metadata never locks a rider to the field. Reaction 0.28→0.20 s,
launch acceleration 9–11 m/s², gearing, signed resistance, preparation and
TimeTo70/SpeedAt2s algorithms remain unchanged.

`RaceProgressTracker.StartingGridOrder` is A→D presentation identity, not a lead.
After movement, progress/time/id determines order. Breaking an equal
progress/time pair of explicitly gated riders is not an overtake; genuine later
reversals and DNF handling remain active. Ungated legacy logs are unchanged.

True within-gate rider positioning and motorcycle footprint are future
refinements; this PR establishes physical gate bounds and a neutral center position.
No marshal/collider, combined grip, trajectory AI, first-corner battle or
common-time swept-space interaction resolver is implemented. #51's INTERACTION
blocker remains unresolved.

## Historical evidence and current fixture separation

The saved [#39 matched-venue report](motoarena-matched-venue.md), venue v1 JSON,
historical experiments through #51 and the #51 audit artifacts retain their
original 31/31/compatibility-start inputs and provenance. **31/31 is superseded
as the current Motoarena start baseline**, not rewritten inside those snapshots.
Historical source callers explicitly name `MotoarenaHistorical39StartLineToFirstCornerMeters`;
#39 uses a frozen historical profile/notes. Existing byte-identical report tests
continue to protect historical results. No historical report is regenerated.

Current callers use `MatchedVenueProfiles.CreateMotoarenaStandingStartTrack()`
and explicit `StartingGrid` assignments. The ordinary Sandbox standing-start
example now also uses four explicit fields on its existing synthetic 10 m track;
its geometry is not silently replaced with Motoarena. `BalanceAnalyzer` remains
a compatibility-position diagnostic, not final physical gate-advantage calibration.

Regression coverage includes 12/10 m partitions, marking metadata, derived
Motoarena coordinates, invalid inputs, neutral snapshots, normal production
launch/movement, continuous surface sampling, exact gated-vs-ungated physics
at identical initial coordinates, all 24 input permutations including reverse,
identical traces/classifications/logs/final surfaces, no phantom tie-break passes,
later real reversals and unchanged fixed-line flying-lap geometry.
Historical whole-engine hash tests permit only the exact added snapshot-metadata
forwarding expression; the rest of the reviewed engine still hashes identically.
