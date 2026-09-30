# Shared physical interaction contract

## Scope, status and precedence

This is a **design / model-contract proposal for independent review**, not a new
physics implementation. It starts from merged #48 (`4329921e63a45b1b61d03d299e99552b1385025f`)
and current main (`58771bac635d7d4962e11aa3ae5ad37a2d0c8d94`), whose additional change
is the gameplay decision snapshot. No production equation, parameter, test or
historical calibration report changes in this PR.

**BINDING concept** below records requirements already approved in the task and
gameplay documentation. **PROVISIONAL mapping** is a proposed way to satisfy them,
not a settled real-world relationship or authorization to implement it.
**TBD calibration** means values, domains or functions still need approval and
evidence. Scenario expectations are future acceptance specifications, not measured
results from the current engine.

Preserve [bike setup](bike-setup.md), [track management](track-management.md),
[rider model](rider-model.md) and [observations](observations-and-feedback.md).
The newer [decision snapshot](design-decisions-2026-09-30.md), especially sections
1–4 and 14, refines them: two bikes, two hidden engine characteristics, no engine
wear state, no static rider–engine preference, at most two rider messages, and
mechanic competencies **Diagnoza** and **Setup**. Nothing here changes match,
morale, scouting, training, finance or club decisions.

Three migration distinctions are explicit, not silently resolved by new physics:

- Production `RiderSkills` has six `0..100` fields including `Speed`; the approved
  target has eight `1..99` skills and no Speed rating. Existing results stay frozen.
  A later migration must remove rider Speed as an engine-power source, not add
  eight new ratings beside the old six.
- Production `Grip`, `TractionBias`, tick-based weather and direct grip deltas are
  compatibility foundations, not the final state/control vocabulary. Their
  replacement needs explicit adapters and tests, not reinterpretation of old data.
- Morale currently influences incident risk, not canonical corner capability or
  longitudinal power. Its possible small future riding effect requires separate
  calibration; this contract adds no morale multiplier.

## 1. Minimal model and dependency boundary — BINDING concept

Use **four shared channels**: drive potential, traction capacity, excess propulsion
demand, and turn/control margin. Carry/exit effectiveness is an observation derived
from these and the actual trajectory; it is not a fifth tunable bonus.

```text
Immutable TrackProfile + TrackCellState snapshot + domain-time Weather
                         |
                         v
               LocalSurfaceResponse (derived, not stored grip)
                         |
Engine A/B + applied four-control BikeSetup + rider execution capabilities
                         |
Trajectory samples: location, ds, curvature + current speed
                         v
   Drive potential -> executed request <-> traction capacity
                                 |
                          excess drive demand
                                 |
                       turn / control margin
                                 v
   One Race Engine: useful force, feasible path, speed, distance, time
                       |                         |
             typed physical symptoms       TrackLoadEvent batch
                       |                         |
             rider observations       Commit -> Track Engine update
                       |                         |
             mechanic hypotheses         next track snapshot
                       |
              manager decision (not a physics write)
```

The feedback evidence belongs to the traversal's **entry snapshot and resolved
result**, not the subsequently worn track. New work/weather/wear changes the next
snapshot and can make an old observation stale. Neither UI nor a diagnosis changes
hidden physical truth.

### Channel decision log

| Channel / why gameplay needs it | BINDING concept | PROVISIONAL mapping | TBD calibration |
|---|---|---|---|
| **Drive potential**: distinguish lack of engine drive from failure to transfer it | Nonnegative available propulsion, not guaranteed acceleration; gearing retains its low/high-speed trade-off | `P(v)` in N from engine potential/delivery, gearing and condition-relative mixture; executed request `D` is bounded by P | Engine profile curves, ignition delivery shape, mixture optimum/penalties and control response |
| **Traction capacity**: track work can make the same bike suitable or unsuitable | Surface acceptance separate from engine power; rider does not create material grip | `C` in N from local response, bike length, speed and load context; rider controls exploitation, not C by a skill multiplier | Material response curves, load proxy and bike-length mapping; no calibrated normal-load model yet |
| **Excess propulsion demand**: explain wheelspin and wasted drive | Derived from executed request versus C; not a new random roll or independent state | `E = max(0, D-C)` in N; bounded transfer law below; this is an excess-demand proxy, NOT tyre slip ratio | Overslip transfer loss, symptom severity/exposure and control fidelity; no wheel angular velocity |
| **Turn / control margin**: explain correction, run-wide and unreachable paths | Speed/curvature, surface support, roughness, setup and rider execution determine feasibility | One dimensionless margin `M = allowed control demand - required control demand`; local demand includes `v² * abs(curvature)` plus roughness, response and excess-slip burden | Normalization, burden functions, capability mapping and recoverable envelopes; no new outcome thresholds here |
| **Carry / exit** (derived, not another channel) | Actual retained speed and useful exit drive, never a line/setup score | Integrate the same forces/margins over actual distance; report exit speed, time, correction and excess demand | Any later need for a fifth independent channel must demonstrate an effect the four cannot explain |

`LocalSurfaceResponse` is a pure projection with longitudinal acceptance and
settled turning-support components. They are differently dimensioned outputs of
one surface function, not separate mutable states or dozens of bonus parameters.
Turning support in m/s² provides the scale for the `v²|curvature|` demand; M also
captures execution difficulty. A negative margin requires bounded correction or
path departure through the existing outcome semantics, not an instantaneous speed
clamp. A positive margin does not guarantee victory or freedom from contact.

## 2. Inputs, outputs and ownership

Names here describe future responsibilities; they do **not** imply these C# types
already exist. Extend existing owners rather than introduce another simulator.

| Value / lifetime | Owner / mutation boundary | Minimal contents and units |
|---|---|---|
| TrackProfile, immutable during meeting | Track domain; geometry remains `TrackGeometry` | Material-response parameters, drainage/exposure, useful moisture range, drying/rut susceptibility, permitted work/equipment; no per-line speed bonuses |
| TrackCellState, dynamic | `TrackState`; Track Engine writes only between race snapshots | Moisture, compaction, loose material, ruts/roughness, surface temperature; tentative first four normalized `0..1`, temperature Celsius; physical normalization/reference amounts **TBD** |
| Weather and elapsed time | Domain scheduler -> Track Engine | Air temperature, relative humidity, wind, solar/cloud input, rain and elapsed Seconds; existing per-tick RainIntensity/DryingRate are compatibility only |
| Engine A/B, meeting-stable | Equipment domain, frozen into race input | Hidden physical potential and mild/aggressive delivery characteristic; engine identity differs from setup; no engine-health/temperature/wear simulation |
| Applied setup | Existing Setup domain, snapshotted before heat | Exactly Gearing, Ignition, Jetting and Bike length; advice/confidence/relationships do not enter force equations |
| Rider capability and condition | Rider domain, read-only in Resolve | Existing execution and perception roles; target Technika/Siła/Start and Kondycja, not extra wheelspin or slide-control ratings |
| Trajectory/local speed | Race Engine | Segment index + physical cross-track offset Meters + canonical lateral coordinate, `ds` Meters, curvature `1/m`, speed `m/s`, phase context; requested and actual paths are distinct |
| Local response + four channels | Pure CoreSim calculation | Derived from one snapshot, no RNG, no global cache independent of state, no setter for EffectiveGrip |
| Resolved motion and symptoms | Existing resolution/typed telemetry boundary | Useful force N, acceleration `m/s²`, actual path/speed/time, excess-demand exposure and control corrections with location/phase |
| TrackLoadEvent | Race Engine emits; Track Engine consumes after rider commit | Stable rider/event identity, segment and cross-track footprint, contact Seconds/Meters, load proxy, excess/sliding activity and atypical incident disturbance |
| Observation/diagnosis | Gameplay only | Quality-limited symptoms, confidence, source/time/location and hypotheses; never copied hidden N or grip numbers |

Validate finite values, units, positive mass/distance where required, and explicit
valid ranges at future domain boundaries. Reject invalid profiles, NaN/Infinity
and out-of-track paths instead of silently repairing inputs. Legitimate state
saturation (e.g. water overflow/runoff) needs a named domain rule, not a generic
clamp that hides accounting errors. Do not add unused profile fields or empty
balance files before their consumer and schema validation exist.

## 3. Transfer and control law — PROVISIONAL mapping

The smallest proposed longitudinal relation is:

```text
P(v) >= 0                              available engine/setup drive, N
0 <= D <= P(v)                         actually executed propulsion request, N
C >= 0                                 surface/bike acceptance, N
E = max(0, D-C)                        excess propulsion request, N
F_use = min(D,C) * eta(E,C)             transferred propulsion, N
0 <= eta <= 1; eta(0,C) = 1            transfer efficiency, not another channel
drive-phase a = (F_use - resistance) / systemMass
```

For `C=0`, define `F_use=0` and `E=D`; any normalized excess ratio is absent,
not division by zero. For `D=0`, E and transferred propulsion are zero. Neither
fact forces final acceleration to zero: resistance/correction remains signed.

`D` represents rider execution of a desired drive opening within existing phase
availability, not a fifth manager throttle slider. Strong control can approach
usable demand smoothly; poor control can overshoot C while still staying below P.
Ignition changes delivery/demand sensitivity, not material capacity or equipment
quality. One rider execution mapping produces D: do not then apply a second
Technique acceleration bonus or multiply C by skill.

**Important sufficiency gate:** `min(D,C)` alone saturates but never makes excessive
aggression worse. It can explain wheelspin evidence, but cannot on its own prove
that mild ignition is faster. A continuous, nonincreasing eta beyond excessive
demand is the proposed minimum extension: overslip can waste transferable drive.
Its shape is **TBD**, not an assumed real tyre curve. A later fixture must show
`D_mild <= C < D_aggressive` and
`F_use(mild) > F_use(aggressive)` in some poor-traction condition, while aggression
can win when C accepts it. If it cannot, stop rather than add a direct mild bonus.
Controlled corner sliding is not automatically excess longitudinal wheelspin.

M couples that same excess-demand evidence to control difficulty, alongside
curvature/support demand, roughness and steering response. Technika owns execution;
Siła can sustain control under demanding loading, not improve engine power.
Roll-off/correction changes the executed request/path before the next resolved
interval. No circular same-interval fixed-point solver is required: evaluate
snapshot -> request -> transfer -> margin -> bounded resolution in a declared
order, with any updated speed entering the next integration evaluation.

Reuse signed force and distance integration. The illustrative drive-phase equation
above is **not** a rewrite of #38's zero-availability neutral carry. Correction,
carry and drive must still account for disjoint metres, time and work. #48's
analysis-only turning dissipation is a different mechanism from excess propulsion;
never charge the same loss through eta, correction and turning drag again. Any
future adoption must expose an explicit energy/force accounting test. No traction
ellipse, slip-angle solver or implicit replacement of the existing corner envelope.

## 4. Exactly four setup controls — BINDING roles

| Control | Enters / intended trade-off | Must not do / status of mapping |
|---|---|---|
| Gearing: very short -> very long | P(v) / delivered drive: short stronger low/mid response and excess-demand sensitivity, faster high-speed fade; long softer low-speed demand, better high-speed carry potential | No artificial Vmax. Reuse `BikeSetup.Gearing` and current envelope foundation; engine/traction separation later requires revalidation, not an extra gearing multiplier |
| Ignition: mild -> aggressive | Bounded shift of native engine delivery and D: sharper demand can exploit high capacity but challenge traction/control; mild can sacrifice potential yet transfer more useful drive | No raw engine-quality upgrade or always-positive acceleration factor. Native engine nature must not be completely inverted; functions/ranges **PROVISIONAL / TBD** |
| Jetting: richer -> condition optimum -> leaner | Condition-relative mismatch in P and response: rich-side loss, near-optimum best exploitation, lean-side possible loss and thermal/reliability **stress proxy** | Not grip. Optimum depends on engine/environment, not a permanently best slider coordinate. Surface temperature is not engine temperature. No persisted engine wear/reliability failures; functions/asymmetry **PROVISIONAL / TBD** |
| Bike length/rear-wheel position: shorter -> longer | C and M through traction behaviour, slide stability and turn response; changes handling, not P | Proposed shorter quicker response / lower forgiveness, longer calmer / slower rotation is **PROVISIONAL**, not verified speedway fact. Traction direction is **TBD**, because geometry/load transfer are absent; no unconditional longer = more grip rule |

Jetting is an applied setting whose optimum moves, not a command that magically
selects the hidden optimum. Both sides must be comparable at identical conditions.
Thermal stress is diagnostic evidence of mismatch, not a fifth core channel or a
new evolving engine-temperature state.

Motor A and B carry the same four controls but can have different frozen potential
and delivery characteristics. Switching bike changes those inputs; it does not
reset rider skills or the track. Slow equipment disposition belongs to engine
potential, not daily rider form. No static “likes aggressive engines” rating.

`TractionBias` must not become a fifth slider or silently stand in for bike length.
Keep it only on the existing compatibility path until a separately reviewed
retirement/adaptation plan covers old constructors, setup blends and consumers.
Extend `BikeSetup`/`SetupResolver`, do not add a competing setup engine. The current
seeded mutable `Random` acceptance and frequent blend/rejection are legacy human
policy, not physical uncertainty or the approved normal execution/rare refusal
policy; future gameplay integration must explicitly address that mismatch.

## 5. Rider responsibilities and migration

| Existing code -> target gameplay role | Use in this contract | Exclusion |
|---|---|---|
| `SlideControl`, execution part of `Adaptability` -> Technika | Request fidelity, slide/trajectory corrections; consolidate at migration | No new independent throttle, wheelspin, slide or precision ratings; no global grip multiplier |
| `TrackReading` -> Czytanie toru | Observe changing lines, select accessible alternatives, qualify feedback | No force/capacity improvement; do not feed perception noise into true surface state |
| `Start` currently conflates reaction/launch -> Reakcja + Start | Reakcja only reaction delay; Start only launch execution, Technika helps transfer | No Start multiplier for later corners; split only in a dedicated migration |
| `PairRiding` -> Jazda parą; future Atak/Obrona | Existing decision/manoeuvre responsibilities | No unconditional physical-channel boost |
| No current distinct counterpart -> approved Siła, Kondycja, mass | Control under load/roughness, sustained execution, system mass when explicitly implemented | No strength = power; do not change today's nominal mass or invent values from unrelated legacy skills |
| `Speed` legacy force source -> no target Speed skill | Preserve only for baseline adapter/regressions until equipment potential migration | Do not rename it engine quality or leave it as a permanent hidden power rating |

These are responsibility mappings, not numerical rating conversions. Current
six-skill normalization cannot be used unmodified for future `1..99` inputs.
Rating conversion and replay compatibility are **TBD**. For a strong/weak rider
comparison, hold engine, setup, actual surface and geometry fixed: only executed
demand/control/path feasibility differ. Perception and style may choose different
paths, but do not physically make a favoured line faster. Mechanic quality belongs
to diagnosis/recommendation, never the race force equations.

## 6. Surface response and work — BINDING causes, PROVISIONAL curves

The response is derived from the immutable profile and current local state, with
speed/load context. Weather updates state through domain time; response may consume
current environmental context where explicitly needed, but must not apply rain or
drying a second time. Neither moisture optimum nor raw `Grip=1` is universal.

- Moisture: profile-specific nonmonotonic dry -> useful range -> too wet response.
  Additional water can therefore improve or worsen C/support without a wet-speed
  penalty. Shape and useful range are **TBD**, not the current hardcoded 0.35.
- Compaction: may improve support; an overpacked/polished regime must be able to
  reduce acceptance. Start with a profile-specific high-compaction response proxy
  (**PROVISIONAL**), not “every Pack increases grip”. Whether traffic history
  requires a separate polishing state is **TBD**; do not add it without evidence.
- Loose material: affects acceptance and control burden; redistribution changes
  which part of a path is usable. It is not a loose-line reward or universal loss.
- Ruts/roughness: primarily create control/response burden and correction demand,
  not a direct lap-time deduction. They may affect acceptance through the same
  response function; avoid stacking unrelated rut penalties.
- Surface temperature: participates in state evolution and material response only
  where justified. Its direct capacity mapping is **TBD**; no tyre thermal model.

| Operation / state write | Derived consequence, never guaranteed benefit |
|---|---|
| Water: local moisture addition; cooling where relevant | Dry-to-useful can increase capacity; already too-wet can worsen it. Dose, runoff, cooling and elapsed-time evolution **TBD** |
| Grade: material transport + roughness/rut reduction; possibly exposed moisture | Smoother path can need fewer corrections; fresh loose material can initially reduce acceptance. Deeper moisture exposure needs a model decision, not invented water |
| Pack: compaction increase; resulting support/polishing derives from response | Loose support may improve; high-compaction acceptance may worsen. No direct stability or grip write |
| Compound maintenance: ordered, timed Water/Grade/Pack operations | Re-evaluate state after each operation; order matters, no aggregate maintenance bonus |

Work is scoped by area, dose/intensity, duration, equipment and Ruleset permission.
Only completed physical work changes state; a manager's request or announcement
does not. Material redistribution must conserve material over source/destination
cells unless a named import/export boundary exists. No direct C/M/speed/time setter.

Race Engine reads the same snapshot for all riders, emits a stable complete load
batch, commits riders, then Track Engine aggregates by cell with deterministic
event ordering and accounting. Applied load depends on actual contact path,
distance/time and sliding/excess demand; incident disturbance is separate evidence.
Do not apply both old per-pass grip wear and new event-driven wear. Logging off,
rider collection permutation and culture must not change state/results/events.

## 7. Continuous trajectory interaction

Reuse #47/#48 Cartesian paths, physical arc distance, curvature and lateral
conversion. Sample **raw local state along the actual path**, then derive response;
do not interpolate a ready-made nonlinear grip number. The future sampler needs
an explicit mapping from path progress to topology segment/cell index: path-wide
progress, CornerProgress and segment-local progress are not interchangeable.

#47/#48's geometry/replay remains analysis-only. Current production samples surface,
radius and wear at segment entry; this PR does not pretend it already performs
path-integrated surface coupling. Extract/promote the pure geometry boundary only
in a reviewed integration PR, with baseline replay and continuity gates intact.
Reuse the canonical corner-control primitive and bounded correction, not another
copy of Brake/RunWide/Crash thresholds. A planned wide path is not forced RunWide.

Candidate evaluation must use the same response/transfer law as actual resolution,
with rider-perceived information for choice and true state for physics. Contact,
blockage and physical feasibility may prevent an otherwise promising path.
The shortest path can win while it has enough capacity; a longer cleaner path can
later win by reducing excess demand/corrections. Changes in local moisture,
compaction/material/ruts can reverse ranking again. No fixed line ordering, hidden
inside/outside bonus, permanent optimum or forced rank swap after a heat number.

## 8. Physical evidence -> feedback -> diagnosis

Typed internal evidence retains location/phase, exposure duration/distance, useful
versus requested propulsion, excess demand, correction/path error and mixture
mismatch. Thresholds/aggregation are **TBD**; a text roll cannot invent an event.

| Rider symptom | Physical evidence / multiple possible causes |
|---|---|
| “Mieli na wyjściu” | Sustained excess propulsion with poor transfer; short gearing, sharp delivery, dry/too-wet/loose state, or poor request control can contribute |
| “Wynosi mnie” | Insufficient turn/control margin, correction or actual path departure; entry speed/curvature, ruts, setup response, execution or contact; distinguish voluntary wide line |
| “Nie skręca” | Required heading/path change not executed in available time/margin; geometry response, entry plan/speed, rut obstruction or rider execution |
| “Brakuje ciągu” | Low useful acceleration: low P, long gearing at low speed, mild delivery, mixture mismatch, low equipment potential **or** transfer loss; compare with excess-demand evidence |
| “Dusi się” | Mixture/engine-response mismatch and weak drive delivery are possible evidence; do not diagnose a specific jet solely from slow exit speed |

Internal diagnostics may know exact channels; rider-facing observations do not.
Apply perception/description quality using Czytanie toru, Technika, experience,
familiarity and condition. Report at most **one track/line message and one bike
message** per heat; quality improves precision, not quantity. Uncertainty can
omit or misinterpret supported evidence, not fabricate exact hidden telemetry.

Mechanic combines observations, known applied setup, own inspection and history.
Diagnoza narrows plausible causes; Setup chooses a bounded correction under that
hypothesis. Return one main diagnosis, optional alternative, one recommendation.
Neither competency gives oracle access to true cell state/engine potential or
automatically changes setup. The manager can change a control, switch A/B, wait
for work or reject advice; evaluation tracks whether the symptom improved, not
just whether the rider won. Any future observation noise must be domain-addressed
and deterministic; it cannot alter race evidence.

### Required watering counterfactual — future acceptance fixture

1. Fix engine, setup, rider, entry state and candidate path on a dry local profile
   whose capacity is below executed aggressive demand. Resolve excess drive,
   poor transfer and the exit symptom; mechanic recommends milder ignition
   (lengthening gearing may be an alternative, not two automatic changes).
2. Manager sees scheduled legal Water and leaves **all four controls unchanged**.
   Before Water completion, recomputing the response must yield no improvement.
3. Apply Water to the relevant cells, then domain-time evolution. Dose moves this
   fixture into its profile's useful moisture range; capacity increases, excess
   decreases, useful acceleration improves and that same setup becomes suitable.
4. Compare with no-water and already-too-wet controls. No-water retains the issue;
   water beyond the useful range can worsen it. No mechanic/message/scheduled-work
   flag may enter the response law. The fixture's conditional signs are **not**
   a promise that every watering improves every bike or every line.

## 9. Scenario matrix — qualitative acceptance targets, not simulation results

Hold engine potential, geometry, entry speed and all unmentioned inputs fixed.
Comparators and preconditions matter; arrows describe a local usable drive phase,
not an unconditional heat-time guarantee. “Better line” is evaluated over actual
sector distance/time, including corrections, repositioning and occupancy.

| Scenario / comparator | Useful acceleration | Excess demand / wheelspin | Stability/control margin | Trajectory tendency | Likely symptom |
|---|---|---|---|---|---|
| Dry + short gearing + aggressive ignition; C below D, vs same setup with adequate moisture | Lower transfer despite high potential | Higher | Lower if excess overloads control | Seek better accepting local surface, not automatically outside | Mieli na wyjściu |
| Same dry surface + longer gearing + mild ignition, vs preceding; request near C | Can improve usable drive; high-grip raw response is softer | Lower | Easier delivery control; dry support itself unchanged | Short feasible route becomes easier; compare alternatives | Less wheelspin, possibly brakuje ciągu |
| High-capacity surface + aggressive setup, vs mild on same surface; demand accepted | Higher exploitable drive | Low if rider controls request | Adequate if geometry/control demand is within margin | Shorter accepting path may win | Czysto ciągnie on exit |
| Too-wet/slippery + aggressive setup, vs same bike in useful moisture range | Lower | Higher | Lower surface support plus control burden | Seek drier/cleaner accepting path | Mieli / wynosi mnie |
| Same too-wet surface + mild setup, vs preceding | Can be higher with less overslip, but bounded by low C | Lower | Better delivery control; wet support remains poor | Safer achievable curvature, not wet-track bonus | Mniej mieli; nadal ślisko |
| Rutted inside vs cleaner middle with comparable acceptance | Inside may lose usable drive/time to corrections; middle can win despite distance | Not necessarily different unless acceptance/request changes | Inside lower, middle better | Middle if correction saving outweighs distance | Wynosi / nie skręca inside |
| Fresh Water on too-dry line, after completion, vs no-water; dose within useful range | Higher useful transfer possible | Lower at unchanged demand | Support/control may improve | Watered line can become competitive | Mieli symptom eases |
| Recently Grade on rutted line, vs pre-work; loose material redistributed | Conditional: may initially fall if newly loose, later improve | May rise initially despite fewer ruts | Rut-related burden lower; loose burden may offset it | Evaluate smoother route and fresh material jointly | Mniej wynosi, ale mieli |
| Strong vs weak execution, identical aggressive bike/surface with limited C | Strong can transfer more usefully through controlled D | Strong lower excess; weak overshoots | Strong retains more control margin | Strong can execute demanding path; weak forgiving alternative | Weak: mieli/wynosi; strong: controlled exit |
| Pack loose supportive regime vs repeated Pack in high-compaction regime | Can improve first; can worsen second | Follows derived C, not operation label | Support can improve without traction improving | Local ranking can change in either direction | Jest spokojniej / ślizga się |
| Mixture near optimum vs richer/leaner mismatch, otherwise identical | Near optimum better exploitation | Depends on resulting D, not direct mixture grip | Indirect delivery burden only | No inherent line preference | Brakuje ciągu / dusi się |

All directions involving response curves, overslip losses or bike length remain
**PROVISIONAL mappings** until calibrated. Roughness versus acceptance is deliberately
separable. High C cannot rescue excessive curvature or an impossible lateral move.

## 10. Existing-code inventory and reuse boundary

| Reviewed existing owner | Reuse | Compatibility / eventual change / do not duplicate |
|---|---|---|
| `src/CoreSim/Setup/BikeSetup.cs` | Gearing range/trade-off, immutable applied setup | TractionBias not fourth control; add three approved controls in a future PR with explicit old-data policy; update Blend/consumers together |
| `src/CoreSim/Setup/SetupResolver.cs` | Human advice/resolution boundary | Current mutable RNG and frequent adjusted/ignored advice need migration to approved normally executed setup / rare refusal; never use acceptance chance as traction |
| `src/CoreSim/Track/LongitudinalDynamics.cs` | Gearing envelope, signed force helper, bounded correction, distance/time integration and exact remainder | Current Speed + EffectiveGrip scales reference force; splitting engine vs transfer must remove double surface counting in an explicit calibrated migration; no parallel longitudinal engine |
| `src/CoreSim/Track/ContinuousCornerEnvelope.cs`, `SegmentPhysics.cs`, `CornerTopology.cs` | Logical phase, recoverable envelope, canonical outcome primitive | Existing phase availability/carry semantics frozen; no duplicated thresholds or wholesale #48 replacement |
| `src/CoreSim/Analysis/FreeContinuousRacingTrajectoryGeometryExperiment.cs` | Pure path geometry, arc distance/curvature, boundary and replay tests from #47/#48 | Analysis replay/search is NOT a second production engine; future extraction only where consumed; preserve sampled turning-demand and path-feasibility gates |
| `src/CoreSim/Analysis/CornerTurningSlipCostExperiment.cs` | #48 separate dissipative turning-cost accounting as experimental evidence | Not longitudinal wheelspin; coefficient not a manager slider, not calibrated production tyre physics; no automatic promotion or duplicate loss debit |
| `src/CoreSim/Track/TrackSurfaceState.cs`, `TrackState.cs`, `SimulationSnapshot.cs` | Grid ownership and detached raw-state sampling | Grip/Ruts/Moisture and global moisture optimum are legacy; add planned states and profile-driven response later. EffectiveGrip becomes a compatibility projection of response, never an independently editable truth |
| `src/CoreSim/Track/TrackEvolution.cs`, `WeatherState.cs` | Work intent types, local targeting and mutation owner | Grade/Pack currently write grip deltas; replace with state/material changes. Weather uses per-tick rates; migrate to explicit elapsed time, not heat-number decay |
| `src/CoreSim/SimulationEngine.cs`, `Logging/TrackSurfaceChange.cs` | Capture/Decide/Resolve/Commit ordering and surface-change audit | Current wear uses entry-position kernel and fixed deltas. No typed TrackLoadEvent yet; introduce it separately from TrackSurfaceChange (effect log), aggregate once, retire old wear when activated |
| `src/CoreSim/Rider.cs`, typed calibration observer/collector | Existing skills and observation boundary; facts independent of log text | Legacy skill-to-power dependency is not target gameplay. Add symptom evidence to actual resolution, not a recomputed fake race or parsed SimLog |

Compatibility projection needs an explicit named mapping/range when implemented;
no arbitrary averaging of longitudinal N and lateral m/s² into “overall grip”.
New channel consumers must use response directly, never response **and** the old
EffectiveGrip multiplier. Existing compatibility consumers keep old numerics
until intentionally migrated with evidence. No API change is approved by this PR.

## 11. Testable contract and smallest implementation sequence

The following are **specifications for future executable tests**, not new tests
run by this documentation PR. They are approval gates before substantial physics:

| Gate | Fixture / falsifiable assertion |
|---|---|
| Transfer conservation and zero cases | For finite P,D,C, enforce `0 <= F_use <= min(D,C) <= P`, `E=max(0,D-C)`; C=0 and D=0 conventions; resistance may yield negative acceleration; reject invalid inputs |
| Setup reversal, not universal bonus | Fixed engine/rider/path: adequate C accepts aggressive demand; low C + overslip gives a mild-demand case with more useful force. Saturation alone is insufficient |
| Four controls / engine distinction | Exactly four manager settings; ignition cannot change native quality, bike length cannot change P, mixture cannot change material C, Gearing cannot set Vmax; Motor switch changes engine input, not rider skill |
| Rider/perception separation | Strong/weak execution on same true surface has identical material capacity but different D/M; changing TrackReading affects belief/report, not true channels for a fixed request/path |
| Moisture and work causality | Dry/useful/too-wet and two profile curves; Water benefits dry fixture but can hurt wet control. No completed work = no state change; Grade material accounting; Pack nonmonotonic acceptance regime; ordered compound sequences |
| Path coupling and changing line optimum | Same geometry across snapshots; localized wear/work changes response along paths and can reverse sector ranking. Fixed state cannot produce a scripted heat-number rank swap; constant-path baseline and variable-curvature continuity tests remain |
| Geometry / resolution accounting | Path-to-cell mapping across unequal subsegments, no teleport or forced-speed cap, finite distance/time, correction/carry/drive disjoint; no same-energy double debit; impossible path remains rejected |
| Wear and determinism | Reordered riders/events, logging off/on, en-US/pl-PL give identical committed state and typed evidence; no TrackLoadEvent while stationary reaction; one wear application per resolved load batch |
| Evidence/privacy | Same typed symptom input supports multiple diagnoses; no absent symptom invented; at most two rider messages; mechanic sees evidence not hidden exact channels; diagnosis quality changes hypothesis/recommendation only |
| Manager waits for watering | Section 8 counterfactual, unchanged setup vs no-water vs too-wet; symptom and transfer improve only through actual state change, not manager decision or recommendation flags |

Smallest sensible sequence, respecting Race Engine -> Track Engine -> integration
-> manager/time/staff -> UI:

1. **Independent review of this contract.** Approve responsibilities, legacy
   adapters, excess-demand sufficiency and what remains uncertain. Stop here now.
2. **Narrow Race Engine contract seam.** Extend actual typed resolution evidence
   and accept an injected immutable local-response fixture; keep the production
   baseline path exact. Prove units/zero cases and a pure transfer/control contract
   before changing global balance. Resolve rider/equipment ownership explicitly.
3. **Track state foundation.** Extend existing grid/snapshots with the five dynamic
   fields and immutable profile, raw sampling, domain time, state-only operations
   and validated material accounting. No manager UI or full weather thermodynamics.
4. **One reviewed integration slice.** Consume response along #47/#48 geometry,
   emit typed loads, replace (not stack) compatibility surface/force/wear mappings,
   and test ranking changes plus force/time/energy accounting. Calibrate only after
   architecture gates; do not promise old times survive an intentional model change.
5. **Setup and information gameplay.** Extend existing applied setup to four
   controls, engine A/B and target rider capabilities with explicit migration;
   physical symptoms -> limited observations -> uncertain mechanic recommendation
   -> timed manager decision. Add the watering counterfactual before UI polish.

Stages are separate future scopes, not automatically authorized implementation.
Small type-only seams are permissible when a consumer/test requires them; this
PR deliberately adds **no placeholder C# types or alternate toy physics engine**.

## 12. Non-goals and open review decisions

Not attempted: full tyre friction ellipse, real slip-angle reconstruction, tyre
temperature, wheel-speed/slip-ratio dynamics, full engine thermodynamics, detailed
suspension, tyre-pressure gameplay/fifth slider, arbitrary line bonuses, fixed
optimum setup, one overall bike-performance score, complete Track Engine, UI,
mechanic AI, long-term engine reliability, new production constants or replacement
of #48 physics.

Before coding, review **PROVISIONAL** request/overslip law and stability-margin
normalization; validate bike-length direction before assigning numbers. **TBD**:
material curves, normalized-state references, mixture optimum/environment inputs,
load proxies, polishing adequacy without history state, rating/data migration,
symptom exposure thresholds, observation uncertainty and timing. Balance values
belong in validated data only when consumed; algorithms/invariants stay in C#.
No uncertain mapping in this document is a claimed calibrated real-world fact.
