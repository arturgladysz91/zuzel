# Four-rider behavior audit: frozen protocol and production model inventory

Baseline: `a3165260183067bff9b6a0e9e94034fa1f361dcf` (merged #50).
Analysis only. This inventory was written before running the new scenarios.
No expected winners, desired contact rate or tuning target is specified.

## Decision inventory — facts, not a verdict

`AdaptiveDecisionModel.Decide(RiderDecisionContext)` receives the immutable
step snapshot: all riders' profiles, statuses, canonical positions, elapsed
arrival times, speeds, lanes/continuous lateral positions and setups; track
geometry/topology and all surface cells. Receiving information does not imply
using it. Each segment it compares exactly five integer targets, `0..4`.
Strict `<` comparison breaks cost ties towards the first/lower candidate.
On a straight it evaluates the immediately next segment, including lap wrap;
otherwise it evaluates the current segment. It does not search further.

For candidate `l`:

- Observation noise is addressed by heat seed/id, step, rider id, lane and
  model seed 1234. Signed noise amplitude is `(1 - TrackReading/100)*0.09`;
  perceived grip adds noise, perceived ruts subtract half that noise.
- `movementCost = abs(l - LateralPosition)*(0.015 + (1-LaneChangeTendency)*0.025)`.
- `styleCost = abs(l - OutsidePreference*4)*0.035`.
- `occupancyCost = 0.30` when another active rider has the same current
  segment, candidate-to-rival physical lateral separation below the
  provisional 0.55 m threshold, and absolute elapsed arrival-time difference
  below 0.30 s. Otherwise zero. Canonical longitudinal separation and speed
  are not consulted.
- `surfaceRiskCost = (1-actualEffectiveGrip)*(0.06+(1-RiskTolerance)*0.12)
  + actualRuts*0.08`.
- Projected speed is `SegmentPhysics.MaxSafeTurnSpeed` at the integer lane,
  using geometry, perceived surface, SlideControl and setup.
- `ProjectedRouteTime = routeLength / max(projectedSpeed,1)`.
  For a turn, route length is **three** turn-segment arcs at that lane plus
  `Geometry.StraightLengthMeters`; for an evaluated straight it is that
  straight geometry length only. Actual logical-corner segment count,
  remaining progress and straight length overrides are not used.

The five terms are added; the minimum gives `TargetLane`. Final risk uses
the selected lane's **current** segment surface, not the lookahead surface.
No current-speed history/profile, planned future lateral execution, rival
trajectory/response, overlap/defence, undercut intent or opening-exit intent
is searched. The projection includes a following-straight distance but not
its acceleration profile. Decisions are refreshed at segment boundaries.
These statements concern the existing implementation, not an API proposal.

## Interaction inventory — facts

`HeatSimulator.SimulateHeat` runs `CaptureSnapshot -> Decide -> Resolve ->
Commit`, for every segment of every lap. Each rider traverses the remaining
segment independently. All active riders reach the same next topological
boundary, at potentially different elapsed arrival times. This is not a
shared-timestamp world-space snapshot. Positive-speed mid-segment initial
states are supported at segment zero, but subsequent initial distance gaps
are represented by boundary arrival-time differences, not continuous overlap.

After independent traversal, contact candidates are formed in ascending
resolved arrival-time order (id tie break). Each trailing rider selects the
nearest preceding rider within 0.55 m of its **resolved endpoint** lateral
position. At most one leader per trailing rider is selected. Candidate
identities are frozen; consequences are then applied in that order, so a
previous pair's time delay can affect a later pair's gap. Pairs with gap
greater than 0.12 s are skipped. No swept lateral-path overlap is required.

Contact probability for the trailing rider is:

`locationRisk * (1.20 - PairRidingNorm*0.45) *
(1.15 - SlideControlNorm*0.35) * (1.10 + (1-effectiveGrip)*0.40)`.

Location risk is 0.025 Straight, 0.12 TurnEntry/TurnExit, 0.24 TurnMiddle.
Crash probability, conditional on contact, is
`(TurnMiddle ? 0.28 : 0.08)*(1.20-SlideControlNorm*0.55)`.
Both draws use deterministic addressed RNG. `IncidentFrequency=0` disables
random surface incidents, **not contacts**. A contact crash sets trailing
speed to zero/status Crashed; its already-resolved endpoint remains.
LostRhythm multiplies trailing speed by 0.82 and adds 0.20 s. In a turn, if
its lane is below 4, it increments the discrete lane and pushes its continuous
position outward by 0.50 m, bounded by the new lane/domain. Straights and
outer lane 4 receive no outward push. None of these constants is changed.

`RaceProgressTracker` detects order changes after Commit, not a
continuous overtaking manoeuvre. Its initial order is lane/id starting-grid
order, even for a rolling/mid-segment fixture. The audit therefore separately
records true-initial-order boundary inversions and production overtake events.
First-boundary differences and initially tied pairs are explicitly labelled;
DNF removals are not counted as passes. A reported boundary is an observation
point, **not** the precise crossing point within the preceding segment.
Events are emitted only for riders whose net rank improves; a pair inversion
can be omitted when that rider's net rank worsens in a multi-rider reorder.

## Frozen scenario design

Main cases use the actual AdaptiveDecisionModel, four riders, four laps,
normal production physics, `IncidentFrequency=1`, neutral setup, dry weather
and all four calibration adjustments null. Each case uses seeds 0 through 31,
heat id 6101. These are synthetic probes, not calibrated venue predictions.
Normal geometry: straight 60 m, inner radius 24 m, straight/turn width
10/14 m, turn-segment angle pi/3. Rolling tracks begin with a straight;
corner-entry tracks begin with TurnEntry. Both have two three-segment bends.
Riders 3/4 usually have arrival offsets 3/5 s so the focal pair is identifiable.
Arrival offsets are crossing-time head starts, not simultaneous spatial gaps.

| case | frozen focal conditions |
| --- | --- |
| A | Same line 1; leader 14 m/s, Speed 20, 15 m into first 60 m straight; follower 20 m/s, Speed 80 at start. Both initial elapsed zero. |
| B | Leader lane 3, 3.75 m ahead, speed 18; follower lane 2, speed 19; inner space free. |
| C | Leader inner 0, 3.75 m ahead, speed 18; follower lane 3, speed 22, Speed/SlideControl 80, outside preference 1. Grip increases 0.72 to 1, ruts decrease 0.30 to 0. |
| D | Equal-progress corner entry, inner 1 versus outer 3, speeds 19/21. |
| E | Rival on 0, focal rider on 1 at 0.10 s offset; clean preferred inner, other lines progressively poorer; explicit no-traffic decision counterfactual. |
| F | Grip 0.55 to 1, ruts 0.75 to 0 from inside to outside; four profiles vary reading/control and inner/outer preference. |
| G | Exactly F's profiles, reversed surface gradient. |
| H | Inner 0 at 18 m/s versus outer 4 at 22 m/s, both at true logical corner progress 0.60. A single pi-angle turn segment represents the whole bend, followed by a straight, twice per lap. Its compatibility label is TurnEntry; contact still uses that label. No fabricated TurnExit phase or re-labelled three-segment progress. This coarser topology is an explicit fixture limitation. |
| I | Production standing-start example; zero-speed riders at lanes 0,1,3,4, equal other skills, Start 35/50/65/80. No gate bonuses. |
| J-tight | B's identical riders/surface on straight 45 m, radius 18 m, widths 8/10 m. |
| J-wide | B's identical riders/surface on straight 80 m, radius 34 m, widths 12/18 m. |

Machine-readable inputs are emitted alongside results, including all six
skills, style, speed, progress, lateral position and arrival offsets.

Input-compatibility addendum from the first attempted run: B's initial 0.05
progress (3 m) leaves the float remainder just below the canonical boundary
tolerance and throws before the next segment. The main B/C/J fixtures use
exactly representable 0.0625 instead; the rejected 0.05 probe is retained in
evidence. This input accommodation is not outcome tuning or a production fix.

## Separate diagnostic controls

- A fixed-route, separated, straight-only four-rider heat with no observation
  noise, no incidents and no eligible contacts is reported once. Changing its
  seed is tested to leave all results unchanged. It is not a main AI result.
- Matched low/high 20/80 one-skill controls change only rider 2's one skill,
  using the same 32 seeds. Start uses I, Speed A, TrackReading F. SlideControl
  and PairRiding use an aligned fixed-line contact probe; Adaptability uses a
  fixed inward target. Fixed intent controls still use production physics and
  are clearly separate from Adaptive scenarios.
- Five fixed-lane solo production traversals check the static projection's
  ranking against actually integrated bend-plus-straight time. No optimizer.
- Six repeated four-rider heats reuse a surface, with zero rain/drying to
  isolate wear. A matched fresh-surface replay separates wear feedback from
  seed effects. Weather configuration is an input, not a formula change.

Mechanism-isolation addendum (after the first main run, before these controls):
two identical longitudinal profiles swap adjacent target lines on a straight;
matched solo D traversals hold lane 3 versus target 0; equal-skill standing
starters hold their assigned reference lanes. These are no-incident,
no-eligible-contact fixed-intent probes, not main AI cases or tuned race
inputs. They test crossing-path support, frozen entry geometry and geometry
versus launch skill confounding. Their seed independence is tested separately.

Read-only instrumentation forwards the actual decision unchanged. Its
no-traffic counterfactual calls the same production decision model on a
detached snapshot; it never supplies the race intent. Typed Resolve telemetry
is the source of history, surfaces and speeds. No human log-string parsing,
no surrogate physics, no seed search and no post-result fixture tuning.

## Evidence and acceptance

Committed evidence contains aggregate results for the full seed set and
detailed traces only for representative/control runs; the full deterministic
suite is regenerated locally from source. The same report command still runs
all 32 seeds, every matched skill pair and every wear/control probe.
Format 2 stores scenario totals/order/pair histograms and scalar summaries for
all 352 main heats and 192 low/high skill pairs. All Markdown values remain
available: main totals/times, skill deltas/movement, first-corner contacts,
route probes and wear aggregates. Full classification, pass/contact events,
choices and step histories are retained for seed 0 of all 11 main scenarios,
the four fixed diagnostic controls and the non-stochastic control (16 runs).
No extra seed traces or skill-pair histories are persisted. Named column
schemas describe the compact row arrays; values are not rounded, including
lane/target/planned lane, lateral position, speed, arrival gap, path/surface
and corner nodes where sampled. Other seeds' individual events and steps
require local regeneration, not inference from the committed aggregates.
Summary means/ranges are descriptive, not empirical confidence intervals.
Initial equal-progress/time pairs are tied rather than claimed established
passes. Actual apex speed is null when traversal does not sample the apex;
envelope apex capacity is not mislabelled as actual speed.

Failures are classified DECISION, EXECUTION, PHYSICS, INTERACTION, START or
DATA/CALIBRATION. Blockers must damage observed gameplay, not merely omit
RPM, suspension, CdA, tyre temperature or granular soil. Final A/B/C judgment,
at most three blockers and at most three future PRs follow measured results.
This PR does not implement any of those recommendations.
