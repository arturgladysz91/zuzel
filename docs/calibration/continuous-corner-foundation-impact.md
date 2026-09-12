# Continuous corner phase foundation impact (#37)

Base main SHA: 0f6dfba7767b155a9c988687a95c5cc7e50a50bd
Candidate code-head SHA: 4caab67265c99afa6791a010dbd65ad6b1321ed8
Calibration Scenario Suite before/after SHA-256: afea7e0b88b5d9c842e15a3386910b6ceddb1fff045913716bda67d2d6e4597a
Calibration Scenario Suite before/after bytes identical: yes

## Architecture before and after

| Concern | Before #37 | After #37 |
| --- | --- | --- |
| Corner identity | Inferred repeatedly from TurnEntry/TurnMiddle/TurnExit labels | One immutable maximal non-wrapping turn run with a stable track-local CornerId |
| Corner phase | Local SegmentProgress only | CornerProgress in [0,1], derived from accumulated physical arc distance |
| Segment boundaries | Compatibility labels implied thirds on the example layouts | Start/end phase comes from each segment's physical length divided by total corner length |
| Production flow | CaptureSnapshot → Decide → Resolve → Commit | Unchanged; advanced Resolve carries the immutable CornerPhaseContext |
| Existing corner phases | Selected directly by SegmentType | Existing TurnEntry scrub and TurnExit drive remain explicit compatibility bridges for #38 |
| Straight lookahead | Immediate TurnEntry label lookup | Immediate logical-corner lookup, then the same TurnEntry compatibility gate and unchanged target calculation |

Corner topology is pure geometry. It adds no state mutation, random channel, surface sampling, speed target, force, traversal phase or alternative simulation path.

## Logical-corner maps

Ranges below are recomputed at lateral positions 0, 2.5 and 4. They happen to align on the current uniform-angle examples, but the implementation sums physical segment lengths and never hardcodes thirds.

| Track | Segment index | Segment id | Compatibility label | Corner id | Range at lateral 0 | Range at lateral 2.5 | Range at lateral 4 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| CreateExample | 0 | 0 | TurnEntry | 1 | 0–0.333333 | 0–0.333333 | 0–0.333333 |
| CreateExample | 1 | 1 | TurnMiddle | 1 | 0.333333–0.666667 | 0.333333–0.666667 | 0.333333–0.666667 |
| CreateExample | 2 | 2 | TurnExit | 1 | 0.666667–1 | 0.666667–1 | 0.666667–1 |
| CreateExample | 3 | 3 | Straight | — | — | — | — |
| CreateExample | 4 | 4 | TurnEntry | 2 | 0–0.333333 | 0–0.333333 | 0–0.333333 |
| CreateExample | 5 | 5 | TurnMiddle | 2 | 0.333333–0.666667 | 0.333333–0.666667 | 0.333333–0.666667 |
| CreateExample | 6 | 6 | TurnExit | 2 | 0.666667–1 | 0.666667–1 | 0.666667–1 |
| CreateExample | 7 | 7 | Straight | — | — | — | — |
| CreateStandingStartExample | 0 | 0 | Straight | — | — | — | — |
| CreateStandingStartExample | 1 | 1 | TurnEntry | 1 | 0–0.333333 | 0–0.333333 | 0–0.333333 |
| CreateStandingStartExample | 2 | 2 | TurnMiddle | 1 | 0.333333–0.666667 | 0.333333–0.666667 | 0.333333–0.666667 |
| CreateStandingStartExample | 3 | 3 | TurnExit | 1 | 0.666667–1 | 0.666667–1 | 0.666667–1 |
| CreateStandingStartExample | 4 | 4 | Straight | — | — | — | — |
| CreateStandingStartExample | 5 | 5 | TurnEntry | 2 | 0–0.333333 | 0–0.333333 | 0–0.333333 |
| CreateStandingStartExample | 6 | 6 | TurnMiddle | 2 | 0.333333–0.666667 | 0.333333–0.666667 | 0.333333–0.666667 |
| CreateStandingStartExample | 7 | 7 | TurnExit | 2 | 0.666667–1 | 0.666667–1 | 0.666667–1 |
| CreateStandingStartExample | 8 | 8 | Straight | — | — | — | — |

| Track | Lateral position | Corner id | First segment | Last segment | Total corner length m |
| --- | --- | --- | --- | --- | --- |
| CreateExample | 0 | 1 | 0 | 2 | 75.39822 |
| CreateExample | 0 | 2 | 4 | 6 | 75.39822 |
| CreateExample | 2.5 | 1 | 0 | 2 | 83.25221 |
| CreateExample | 2.5 | 2 | 4 | 6 | 83.25221 |
| CreateExample | 4 | 1 | 0 | 2 | 87.9646 |
| CreateExample | 4 | 2 | 4 | 6 | 87.9646 |
| CreateStandingStartExample | 0 | 1 | 1 | 3 | 75.39822 |
| CreateStandingStartExample | 0 | 2 | 5 | 7 | 75.39822 |
| CreateStandingStartExample | 2.5 | 1 | 1 | 3 | 98.96017 |
| CreateStandingStartExample | 2.5 | 2 | 5 | 7 | 98.96017 |
| CreateStandingStartExample | 4 | 1 | 1 | 3 | 113.0973 |
| CreateStandingStartExample | 4 | 2 | 5 | 7 | 113.0973 |


## Corner progress and remaining distance

Representative midpoint observations use lateral position 2.5. Remaining distance is measured to the logical-corner end on that same geometric reference trajectory.

| Track | Corner id | Segment index | Label | Local progress | Corner progress | Total m | Remaining m |
| --- | --- | --- | --- | --- | --- | --- | --- |
| CreateExample | 1 | 0 | TurnEntry | 0.5 | 0.166667 | 83.25221 | 69.37685 |
| CreateExample | 1 | 1 | TurnMiddle | 0.5 | 0.5 | 83.25221 | 41.62611 |
| CreateExample | 1 | 2 | TurnExit | 0.5 | 0.833333 | 83.25221 | 13.87537 |
| CreateExample | 2 | 4 | TurnEntry | 0.5 | 0.166667 | 83.25221 | 69.37685 |
| CreateExample | 2 | 5 | TurnMiddle | 0.5 | 0.5 | 83.25221 | 41.62611 |
| CreateExample | 2 | 6 | TurnExit | 0.5 | 0.833333 | 83.25221 | 13.87537 |
| CreateStandingStartExample | 1 | 1 | TurnEntry | 0.5 | 0.166667 | 98.96017 | 82.46681 |
| CreateStandingStartExample | 1 | 2 | TurnMiddle | 0.5 | 0.5 | 98.96017 | 49.48009 |
| CreateStandingStartExample | 1 | 3 | TurnExit | 0.5 | 0.833333 | 98.96017 | 16.49336 |
| CreateStandingStartExample | 2 | 5 | TurnEntry | 0.5 | 0.166667 | 98.96017 | 82.46681 |
| CreateStandingStartExample | 2 | 6 | TurnMiddle | 0.5 | 0.5 | 98.96017 | 49.48009 |
| CreateStandingStartExample | 2 | 7 | TurnExit | 0.5 | 0.833333 | 98.96017 | 16.49336 |


## Production regression

The complete Calibration Scenario Suite was captured on base main before the code change and rendered again through the same production path after the change. Its full Markdown is byte-identical. Therefore every selected before value below is the exact matching base observation, not an inferred or recomputed surrogate.

| Scenario | Metric | Before | After | Delta | Unit |
| --- | --- | --- | --- | --- | --- |
| start/baseline | reaction | 0.24 | 0.24 | 0 | s |
| start/baseline | exit speed | 19.013502 | 19.013502 | 0 | m/s |
| start/baseline | peak speed | 20.589153 | 20.589153 | 0 | m/s |
| start/baseline | total time | 3.00525 | 3.00525 | 0 | s |
| straight/baseline | exit speed | 19.654568 | 19.654568 | 0 | m/s |
| straight/baseline | peak speed | 19.654568 | 19.654568 | 0 | m/s |
| straight/baseline | travel time | 1.677115 | 1.677115 | 0 | s |
| turn_entry/baseline | final exit speed | 16.970562 | 16.970562 | 0 | m/s |
| turn_entry/baseline | travel time | 1.569543 | 1.569543 | 0 | s |
| turn_entry/baseline | travelled distance | 28.274334 | 28.274334 | 0 | m |
| turn_middle/baseline | final exit speed | 16.970562 | 16.970562 | 0 | m/s |
| turn_middle/baseline | travel time | 1.655291 | 1.655291 | 0 | s |
| turn_middle/baseline | travelled distance | 28.274334 | 28.274334 | 0 | m |
| turn_exit/baseline | final exit speed | 19.108496 | 19.108496 | 0 | m/s |
| turn_exit/baseline | travel time | 1.577871 | 1.577871 | 0 | s |
| turn_exit/baseline | travelled distance | 28.274334 | 28.274334 | 0 | m |
| full_heat/baseline/rider_1 | heat time | 64.21463 | 64.21463 | 0 | s |
| full_heat/baseline/rider_1 | Vmax | 79.155835 | 79.155835 | 0 | km/h |
| full_heat/baseline/rider_1 | average speed | 17.491119 | 17.491119 | 0 | m/s |
| full_heat/baseline/rider_2 | heat time | 65.457893 | 65.457893 | 0 | s |
| full_heat/baseline/rider_2 | Vmax | 81.801542 | 81.801542 | 0 | km/h |
| full_heat/baseline/rider_2 | average speed | 18.310764 | 18.310764 | 0 | m/s |
| full_heat/baseline/rider_3 | heat time | 66.700363 | 66.700363 | 0 | s |
| full_heat/baseline/rider_3 | Vmax | 84.407334 | 84.407334 | 0 | km/h |
| full_heat/baseline/rider_3 | average speed | 19.10008 | 19.10008 | 0 | m/s |
| full_heat/baseline/rider_4 | heat time | 67.926353 | 67.926353 | 0 | s |
| full_heat/baseline/rider_4 | Vmax | 86.821168 | 86.821168 | 0 | km/h |
| full_heat/baseline/rider_4 | average speed | 19.865341 | 19.865341 | 0 | m/s |


## Frozen physics boundary

No calibration or physics tuning was performed. The values below are assertions about the unchanged production model, not new parameters introduced by this report.

| Parameter | Value |
| --- | --- |
| Straight reference acceleration | 1.60–3.20 m/s² |
| TurnExit reference acceleration | 1.20–2.80 m/s² |
| Drive-oriented / speed-oriented fade | 0.0350 / 0.0100 1/(m/s) |
| Reference speed | 16 m/s |
| Nominal mass | 142 kg |
| Resistance | 40 + 0.20v² N |
| Gearing drive mapping | 1.10–0.90 |
| Surface drive mapping | 0.75 + 0.25 × EffectiveGrip |
| Standing-start reaction | 0.28–0.20 s |
| Standing-start reference acceleration | 9–11 m/s² |
| Corner correction capability | 2.00–3.20 m/s² |
| TurnEntry scrub fraction | 0.50 |

SegmentPhysics thresholds/retention, continuous correction, launch, reaction, acceleration, resistance, force fade, RiderSkills multipliers, contact probability, occupancy/contact thresholds, lateral traversal, surface physics, legacy behavior, dataset files and historical reports are unchanged.

NO MATERIAL PHYSICS CHANGE.
NO SPEED/PERFORMANCE CONSTANTS CHANGED.
