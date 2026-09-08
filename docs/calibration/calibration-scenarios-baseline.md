# Calibration scenarios — pre-tuning baseline after PR #34

Baseline main SHA: 4572ad9c5af032572f528f0ce434df9c97153594

Historical measurement artifact for PR #35. Later tuning PRs must create a new report, not overwrite this baseline. NO PHYSICS CONSTANTS WERE TUNED.

## Provenance

186 scenario definitions. Fixed seed 320032; Dry weather; incidents 0; HoldLane with risk 0; RiderStyle.Balanced; four laps. All skills 50 and BikeSetup.Neutral (Gearing 0.5, TractionBias 0.5) unless an input row says otherwise. Skill 50 is only a fixture, not an average real PGEE rider. Gearing is a normalized game abstraction, not real sprockets.

Track.CreateStandingStartExample: inner reference radius 24 m, straight/turn widths 10/14 m, turn-segment angle 1.047198 rad; 35 m launch/home-straight halves and 60 m back straight. Offsets below are from the inner reference trajectory, not the physical inner edge.

Start uses the production launch profile: prepared probes obtain it from SimulationEngine.Resolve (including production lookahead); pure_launch has no preparation target. Straight uses CalculateForceBasedStraightSpeedProfile with no downstream corner. Corner probes use CaptureSnapshot → Decide → Resolve and retain its original profiles; no alternate traversal is calculated. Full heats/line runs use CalibrationRunner → HeatSimulator → SimulationEngine, including Commit and normal surface evolution. Isolated probes sample a fresh uniform surface; full heats start uniform but do not freeze wear/weather. The full heat is not four independent fixed-surface probes.

One-axis sweeps hold all other inputs fixed. Corner axis sweeps hold the baseline incoming speed fixed, not an overspeed factor; therefore classifications can change. Band inputs use observed production transition speeds. TurnEntry input is derived with the production approach-speed helper so scrub ends inside that band. Transition diagnostics are adjacent-float black-box observations of SegmentPhysics.Apply, not copied threshold formulas or parameter fitting. insufficient_distance starts at legal segment progress 0.99.

Explicit interaction probes: gearing × distances 10/60/600 m, traction bias endpoints × moisture 0.70, and three synthetic within-heat patterns. The 600 m straight is a diagnostic distance to observe approach to equilibrium, not example-track geometry. Repeated baseline-valued axes are intentionally separately labelled controls. '—' means no production profile/not reached/not applicable, never a zero-valued inferred measurement. Speed units are m/s unless marked km/h; times s, distances m, force N, acceleration m/s².

| Surface | Grip | Ruts | Moisture | EffectiveGrip |
| --- | --- | --- | --- | --- |
| baseline | 1 | 0 | 0.35 | 1 |
| grip_080 | 0.8 | 0 | 0.35 | 0.8 |
| moisture_070 | 1 | 0 | 0.7 | 0.8075 |
| ruts_025 | 1 | 0.25 | 0.35 | 0.8875 |


## Inputs

Skill vector order: Start / Speed / SlideControl / TrackReading / PairRiding / Adaptability. For synthetic heats, individual vectors below override the common fixture. The table is the complete scenario input inventory; geometry/speed/progress inputs are also printed in the respective output tables.

| Scenario | Skills | Gearing | TractionBias | Grip | Ruts | Moisture | EffectiveGrip |
| --- | --- | --- | --- | --- | --- | --- | --- |
| full_heat/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/gearing/000 | 50/50/50/50/50/50 | 0 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/gearing/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/gearing/100 | 50/50/50/50/50/50 | 1 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/slide_control/000 | 50/50/0/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/slide_control/025 | 50/50/25/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/slide_control/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/slide_control/075 | 50/50/75/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/slide_control/100 | 50/50/100/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/speed/000 | 50/0/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/speed/025 | 50/25/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/speed/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/speed/075 | 50/75/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/speed/100 | 50/100/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/start/000 | 0/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/start/025 | 25/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/start/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/start/075 | 75/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/start/100 | 100/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/surface/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/surface/grip_080 | 50/50/50/50/50/50 | 0.5 | 0.5 | 0.8 | 0 | 0.35 | 0.8 |
| full_heat/surface/moisture_070 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.7 | 0.8075 |
| full_heat/surface/ruts_025 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0.25 | 0.35 | 0.8875 |
| full_heat/within_heat/slide_control | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/within_heat/speed | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| full_heat/within_heat/start | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| line_geometry/lateral/000 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| line_geometry/lateral/001 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| line_geometry/lateral/002 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| line_geometry/lateral/003 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| line_geometry/lateral/004 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/adaptability/000 | 50/50/50/50/50/0 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/adaptability/100 | 50/50/50/50/50/100 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/gearing/000 | 50/50/50/50/50/50 | 0 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/gearing/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/gearing/100 | 50/50/50/50/50/50 | 1 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/pair_riding/000 | 50/50/50/50/0/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/pair_riding/100 | 50/50/50/50/100/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/pure_launch | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/start_skill/000 | 0/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/start_skill/025 | 25/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/start_skill/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/start_skill/075 | 75/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/start_skill/100 | 100/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/surface/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/surface/grip_080 | 50/50/50/50/50/50 | 0.5 | 0.5 | 0.8 | 0 | 0.35 | 0.8 |
| start/surface/moisture_070 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.7 | 0.8075 |
| start/surface/ruts_025 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0.25 | 0.35 | 0.8875 |
| start/track_reading/000 | 50/50/50/0/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| start/track_reading/100 | 50/50/50/100/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/adaptability/000 | 50/50/50/50/50/0 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/adaptability/100 | 50/50/50/50/50/100 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/distance/010 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/distance/020 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/distance/030 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/distance/060 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/entry_speed/010 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/entry_speed/016 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/entry_speed/020 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/entry_speed/024 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/gearing/distance_010/000 | 50/50/50/50/50/50 | 0 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/gearing/distance_010/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/gearing/distance_010/100 | 50/50/50/50/50/50 | 1 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/gearing/distance_060/000 | 50/50/50/50/50/50 | 0 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/gearing/distance_060/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/gearing/distance_060/100 | 50/50/50/50/50/50 | 1 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/gearing/distance_600/000 | 50/50/50/50/50/50 | 0 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/gearing/distance_600/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/gearing/distance_600/100 | 50/50/50/50/50/50 | 1 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/pair_riding/000 | 50/50/50/50/0/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/pair_riding/100 | 50/50/50/50/100/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/speed/000 | 50/0/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/speed/025 | 50/25/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/speed/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/speed/075 | 50/75/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/speed/100 | 50/100/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/surface/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/surface/grip_080 | 50/50/50/50/50/50 | 0.5 | 0.5 | 0.8 | 0 | 0.35 | 0.8 |
| straight/surface/moisture_070 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.7 | 0.8075 |
| straight/surface/ruts_025 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0.25 | 0.35 | 0.8875 |
| straight/track_reading/000 | 50/50/50/0/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| straight/track_reading/100 | 50/50/50/100/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/band/below_max | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/band/brake | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/band/crash_above_boundary | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/band/quiet | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/band/run_wide | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/insufficient_distance | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/lateral/000 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/lateral/001 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/lateral/002 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/lateral/003 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/lateral/004 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/slide_control/000 | 50/50/0/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/slide_control/025 | 50/50/25/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/slide_control/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/slide_control/075 | 50/50/75/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/slide_control/100 | 50/50/100/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/speed/000 | 50/0/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/speed/025 | 50/25/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/speed/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/speed/075 | 50/75/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/speed/100 | 50/100/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/surface/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/surface/grip_080 | 50/50/50/50/50/50 | 0.5 | 0.5 | 0.8 | 0 | 0.35 | 0.8 |
| turn_entry/surface/moisture_070 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.7 | 0.8075 |
| turn_entry/surface/ruts_025 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0.25 | 0.35 | 0.8875 |
| turn_entry/traction_bias/000 | 50/50/50/50/50/50 | 0.5 | 0 | 1 | 0 | 0.35 | 1 |
| turn_entry/traction_bias/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_entry/traction_bias/100 | 50/50/50/50/50/50 | 0.5 | 1 | 1 | 0 | 0.35 | 1 |
| turn_entry/traction_bias_on_moisture_070/000 | 50/50/50/50/50/50 | 0.5 | 0 | 1 | 0 | 0.7 | 0.8075 |
| turn_entry/traction_bias_on_moisture_070/100 | 50/50/50/50/50/50 | 0.5 | 1 | 1 | 0 | 0.7 | 0.8075 |
| turn_exit/band/below_max | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/band/brake | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/band/crash_above_boundary | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/band/quiet | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/band/run_wide | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/gearing/000 | 50/50/50/50/50/50 | 0 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/gearing/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/gearing/100 | 50/50/50/50/50/50 | 1 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/insufficient_distance | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/lateral/000 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/lateral/001 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/lateral/002 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/lateral/003 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/lateral/004 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/slide_control/000 | 50/50/0/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/slide_control/025 | 50/50/25/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/slide_control/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/slide_control/075 | 50/50/75/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/slide_control/100 | 50/50/100/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/speed/000 | 50/0/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/speed/025 | 50/25/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/speed/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/speed/075 | 50/75/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/speed/100 | 50/100/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/surface/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/surface/grip_080 | 50/50/50/50/50/50 | 0.5 | 0.5 | 0.8 | 0 | 0.35 | 0.8 |
| turn_exit/surface/moisture_070 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.7 | 0.8075 |
| turn_exit/surface/ruts_025 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0.25 | 0.35 | 0.8875 |
| turn_exit/traction_bias/000 | 50/50/50/50/50/50 | 0.5 | 0 | 1 | 0 | 0.35 | 1 |
| turn_exit/traction_bias/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_exit/traction_bias/100 | 50/50/50/50/50/50 | 0.5 | 1 | 1 | 0 | 0.35 | 1 |
| turn_exit/traction_bias_on_moisture_070/000 | 50/50/50/50/50/50 | 0.5 | 0 | 1 | 0 | 0.7 | 0.8075 |
| turn_exit/traction_bias_on_moisture_070/100 | 50/50/50/50/50/50 | 0.5 | 1 | 1 | 0 | 0.7 | 0.8075 |
| turn_middle/adaptability/000 | 50/50/50/50/50/0 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/adaptability/100 | 50/50/50/50/50/100 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/band/below_max | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/band/brake | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/band/crash_above_boundary | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/band/quiet | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/band/run_wide | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/insufficient_distance | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/lateral/000 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/lateral/001 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/lateral/002 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/lateral/003 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/lateral/004 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/pair_riding/000 | 50/50/50/50/0/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/pair_riding/100 | 50/50/50/50/100/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/slide_control/000 | 50/50/0/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/slide_control/025 | 50/50/25/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/slide_control/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/slide_control/075 | 50/50/75/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/slide_control/100 | 50/50/100/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/speed/000 | 50/0/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/speed/025 | 50/25/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/speed/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/speed/075 | 50/75/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/speed/100 | 50/100/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/surface/baseline | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/surface/grip_080 | 50/50/50/50/50/50 | 0.5 | 0.5 | 0.8 | 0 | 0.35 | 0.8 |
| turn_middle/surface/moisture_070 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.7 | 0.8075 |
| turn_middle/surface/ruts_025 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0.25 | 0.35 | 0.8875 |
| turn_middle/track_reading/000 | 50/50/50/0/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/track_reading/100 | 50/50/50/100/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/traction_bias/000 | 50/50/50/50/50/50 | 0.5 | 0 | 1 | 0 | 0.35 | 1 |
| turn_middle/traction_bias/050 | 50/50/50/50/50/50 | 0.5 | 0.5 | 1 | 0 | 0.35 | 1 |
| turn_middle/traction_bias/100 | 50/50/50/50/50/50 | 0.5 | 1 | 1 | 0 | 0.35 | 1 |
| turn_middle/traction_bias_on_moisture_070/000 | 50/50/50/50/50/50 | 0.5 | 0 | 1 | 0 | 0.7 | 0.8075 |
| turn_middle/traction_bias_on_moisture_070/100 | 50/50/50/50/50/50 | 0.5 | 1 | 1 | 0 | 0.7 | 0.8075 |


## Start

| Scenario | Mode | Distance | Reaction | Movement | Total | TimeTo70 | SpeedAt2s | Exit | Peak | Accel m | Cruise m | Prep m | Entry net accel | Reference net accel | Reference force | Equilibrium |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| start/adaptability/000 | FirstCornerPreparation | 35 | 0.24 | 2.764091 | 3.004091 | 2.22782 | 17.3381 | 19.0135 | 20.58915 | 23 | 0 | 12 | 10 | 10 | 1460 | 59.4369 |
| start/adaptability/100 | FirstCornerPreparation | 35 | 0.24 | 2.764091 | 3.004091 | 2.22782 | 17.3381 | 19.0135 | 20.58915 | 23 | 0 | 12 | 10 | 10 | 1460 | 59.4369 |
| start/baseline | FirstCornerPreparation | 35 | 0.24 | 2.764091 | 3.004091 | 2.22782 | 17.3381 | 19.0135 | 20.58915 | 23 | 0 | 12 | 10 | 10 | 1460 | 59.4369 |
| start/gearing/000 | FirstCornerPreparation | 35 | 0.24 | 2.668248 | 2.908248 | 2.04804 | 18.97158 | 19.0135 | 20.84018 | 21 | 0 | 14 | 11 | 11 | 1602 | 52.24425 |
| start/gearing/050 | FirstCornerPreparation | 35 | 0.24 | 2.764091 | 3.004091 | 2.22782 | 17.3381 | 19.0135 | 20.58915 | 23 | 0 | 12 | 10 | 10 | 1460 | 59.4369 |
| start/gearing/100 | FirstCornerPreparation | 35 | 0.24 | 2.880203 | 3.120203 | 2.448396 | 15.6369 | 19.0135 | 20.38968 | 24 | 0 | 11 | 9 | 9 | 1318 | 68.31075 |
| start/pair_riding/000 | FirstCornerPreparation | 35 | 0.24 | 2.764091 | 3.004091 | 2.22782 | 17.3381 | 19.0135 | 20.58915 | 23 | 0 | 12 | 10 | 10 | 1460 | 59.4369 |
| start/pair_riding/100 | FirstCornerPreparation | 35 | 0.24 | 2.764091 | 3.004091 | 2.22782 | 17.3381 | 19.0135 | 20.58915 | 23 | 0 | 12 | 10 | 10 | 1460 | 59.4369 |
| start/pure_launch | PureLaunch | 35 | 0.24 | 2.675449 | 2.915449 | 2.22782 | 17.3381 | 25.31828 | 25.31828 | 35 | 0 | 0 | 10 | 10 | 1460 | 59.4369 |
| start/start_skill/000 | FirstCornerPreparation | 35 | 0.28 | 2.880836 | 3.160836 | 2.493311 | 15.29033 | 19.0135 | 20.33503 | 25 | 0 | 10 | 9 | 9 | 1318 | 57.53724 |
| start/start_skill/025 | FirstCornerPreparation | 35 | 0.26 | 2.81953 | 3.07953 | 2.354524 | 16.31051 | 19.0135 | 20.46248 | 24 | 0 | 11 | 9.5 | 9.5 | 1389 | 58.51113 |
| start/start_skill/050 | FirstCornerPreparation | 35 | 0.24 | 2.764091 | 3.004091 | 2.22782 | 17.3381 | 19.0135 | 20.58915 | 23 | 0 | 12 | 10 | 10 | 1460 | 59.4369 |
| start/start_skill/075 | FirstCornerPreparation | 35 | 0.22 | 2.713689 | 2.933689 | 2.111409 | 18.37 | 19.0135 | 20.71505 | 22 | 0 | 13 | 10.5 | 10.5 | 1531 | 60.31862 |
| start/start_skill/100 | FirstCornerPreparation | 35 | 0.2 | 2.667652 | 2.867652 | 2.003975 | 19.40448 | 19.0135 | 20.84018 | 21 | 0 | 14 | 11 | 11 | 1602 | 61.15988 |
| start/surface/baseline | FirstCornerPreparation | 35 | 0.24 | 2.764091 | 3.004091 | 2.22782 | 17.3381 | 19.0135 | 20.58915 | 23 | 0 | 12 | 10 | 10 | 1460 | 59.4369 |
| start/surface/grip_080 | FirstCornerPreparation | 35 | 0.24 | 2.84326 | 3.08326 | 2.334524 | 16.49196 | 18.30064 | 19.97832 | 22 | 0 | 13 | 9.5 | 9.5 | 1389 | 58.51113 |
| start/surface/moisture_070 | FirstCornerPreparation | 35 | 0.24 | 2.840148 | 3.080148 | 2.330315 | 16.52392 | 18.32774 | 20.00632 | 22 | 0 | 13 | 9.518749 | 9.518749 | 1391.662 | 58.54668 |
| start/surface/ruts_025 | FirstCornerPreparation | 35 | 0.24 | 2.8077 | 3.0477 | 2.286408 | 16.86414 | 18.61506 | 20.25807 | 22 | 0 | 13 | 9.71875 | 9.71875 | 1420.062 | 58.92182 |
| start/track_reading/000 | FirstCornerPreparation | 35 | 0.24 | 2.764091 | 3.004091 | 2.22782 | 17.3381 | 19.0135 | 20.58915 | 23 | 0 | 12 | 10 | 10 | 1460 | 59.4369 |
| start/track_reading/100 | FirstCornerPreparation | 35 | 0.24 | 2.764091 | 3.004091 | 2.22782 | 17.3381 | 19.0135 | 20.58915 | 23 | 0 | 12 | 10 | 10 | 1460 | 59.4369 |


## Straight

Free-drive phase 'Decel' can include signed resistance-dominated deceleration; it is not a preparation distance when no downstream target is supplied. Equilibrium is diagnostic only, never a speed cap.

| Scenario | Distance | Entry | Exit | Peak | Time | Accel m | Cruise m | Decel m | Reference force | Entry net accel | Equilibrium |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| straight/adaptability/000 | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/adaptability/100 | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/baseline | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/distance/010 | 10 | 16 | 16.71872 | 16.71872 | 0.61118 | 10 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/distance/020 | 20 | 16 | 17.38091 | 17.38091 | 1.197621 | 20 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/distance/030 | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/distance/060 | 60 | 16 | 19.59488 | 19.59488 | 3.35781 | 60 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/entry_speed/010 | 30 | 10 | 13.47899 | 13.47899 | 2.550162 | 30 | 0 | 0 | 261.6 | 1.419718 | 30.02662 |
| straight/entry_speed/016 | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/entry_speed/020 | 30 | 20 | 21.25609 | 21.25609 | 1.453486 | 30 | 0 | 0 | 261.6 | 0.914282 | 30.02662 |
| straight/entry_speed/024 | 30 | 24 | 24.68096 | 24.68096 | 1.232199 | 30 | 0 | 0 | 261.6 | 0.583493 | 30.02662 |
| straight/gearing/distance_010/000 | 10 | 16 | 16.78571 | 16.78571 | 0.609904 | 10 | 0 | 0 | 278.64 | 1.32 | 29.42566 |
| straight/gearing/distance_010/050 | 10 | 16 | 16.71872 | 16.71872 | 0.61118 | 10 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/gearing/distance_010/100 | 10 | 16 | 16.6506 | 16.6506 | 0.612479 | 10 | 0 | 0 | 244.56 | 1.08 | 30.55802 |
| straight/gearing/distance_060/000 | 60 | 16 | 19.84065 | 19.84065 | 3.331329 | 60 | 0 | 0 | 278.64 | 1.32 | 29.42566 |
| straight/gearing/distance_060/050 | 60 | 16 | 19.59488 | 19.59488 | 3.35781 | 60 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/gearing/distance_060/100 | 60 | 16 | 19.32646 | 19.32646 | 3.38637 | 60 | 0 | 0 | 244.56 | 1.08 | 30.55802 |
| straight/gearing/distance_600/000 | 600 | 16 | 28.51603 | 28.51603 | 24.36961 | 600 | 0 | 0 | 278.64 | 1.32 | 29.42566 |
| straight/gearing/distance_600/050 | 600 | 16 | 28.73876 | 28.73876 | 24.48416 | 600 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/gearing/distance_600/100 | 600 | 16 | 28.81145 | 28.81145 | 24.69326 | 600 | 0 | 0 | 244.56 | 1.08 | 30.55802 |
| straight/pair_riding/000 | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/pair_riding/100 | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/speed/000 | 30 | 16 | 17.35982 | 17.35982 | 1.797185 | 30 | 0 | 0 | 204.8 | 0.8 | 26.51224 |
| straight/speed/025 | 30 | 16 | 17.68041 | 17.68041 | 1.779716 | 30 | 0 | 0 | 233.2 | 1 | 28.35396 |
| straight/speed/050 | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/speed/075 | 30 | 16 | 18.30138 | 18.30138 | 1.74677 | 30 | 0 | 0 | 290 | 1.4 | 31.56181 |
| straight/speed/100 | 30 | 16 | 18.6025 | 18.6025 | 1.731202 | 30 | 0 | 0 | 318.4 | 1.6 | 32.98248 |
| straight/surface/baseline | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/surface/grip_080 | 30 | 16 | 17.90071 | 17.90071 | 1.767896 | 30 | 0 | 0 | 253.08 | 1.14 | 29.5405 |
| straight/surface/moisture_070 | 30 | 16 | 17.90423 | 17.90423 | 1.767708 | 30 | 0 | 0 | 253.3995 | 1.14225 | 29.55896 |
| straight/surface/ruts_025 | 30 | 16 | 17.94165 | 17.94165 | 1.765715 | 30 | 0 | 0 | 256.8075 | 1.16625 | 29.75474 |
| straight/track_reading/000 | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |
| straight/track_reading/100 | 30 | 16 | 17.99413 | 17.99413 | 1.762927 | 30 | 0 | 0 | 261.6 | 1.2 | 30.02662 |


## TurnEntry

Production order: scrub → post-scrub constraint → correction → residual carry. Scrub m includes its deceleration and any scrub carry.

Crash probes retain existing half-segment crash semantics. Available m is legal remaining segment length; travelled m is production distance. Do not interpret crashes as a modeled crash trajectory or apply recoverable full-distance equality to them. At outer lane 4 production crashes instead of RunWide, so the recoverable transition and observed retention are absent, not inferred from an inner lane.

| Scenario | Lateral | Progress | Radius | Available m | Travelled m | Incoming | Max safe | Correction capability | First Brake | First RunWide | First Crash | Observed retention | Outcome |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| turn_entry/band/below_max | 1 | 0 | 27 | 28.27433 | 28.27433 | 16.63115 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Ok |
| turn_entry/band/brake | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_entry/band/crash_above_boundary | 1 | 0 | 27 | 28.27433 | 14.13717 | 23.05359 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Crash |
| turn_entry/band/quiet | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.12719 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Ok |
| turn_entry/band/run_wide | 1 | 0 | 27 | 28.27433 | 28.27433 | 21.78358 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | RunWide |
| turn_entry/baseline | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_entry/insufficient_distance | 1 | 0.99 | 27 | 0.282743 | 0.282743 | 17.96684 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_entry/lateral/000 | 0 | 0 | 24 | 25.13274 | 25.13274 | 19.88933 | 16 | 2.6 | 16.24 | 17.6 | 20.16 | 0.5 | RunWide |
| turn_entry/lateral/001 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_entry/lateral/002 | 2 | 0 | 30 | 31.41593 | 31.41593 | 19.88933 | 17.88854 | 2.6 | 18.15687 | 19.6774 | 22.53957 | 0.5 | Ok |
| turn_entry/lateral/003 | 3 | 0 | 33 | 34.55752 | 34.55752 | 19.88933 | 18.76166 | 2.6 | 19.04309 | 20.63783 | 23.6397 | 0.5 | Ok |
| turn_entry/lateral/004 | 4 | 0 | 36 | 37.69911 | 37.69911 | 19.88933 | 19.59592 | 2.6 | 19.88986 | — | 21.55551 | — | Ok |
| turn_entry/slide_control/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.46144 | 2 | 16.70837 | 17.44913 | 19.42451 | 0.35 | RunWide |
| turn_entry/slide_control/025 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.716 | 2.3 | 16.96675 | 18.05328 | 20.39352 | 0.425 | RunWide |
| turn_entry/slide_control/050 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_entry/slide_control/075 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 17.22512 | 2.9 | 17.4835 | 19.29214 | 22.39266 | 0.575 | Brake |
| turn_entry/slide_control/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 17.47968 | 3.2 | 17.74188 | 19.92684 | 23.42277 | 0.65 | Ok |
| turn_entry/speed/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 15.95233 | 2.6 | 16.19162 | 17.54756 | 20.09994 | 0.5 | RunWide |
| turn_entry/speed/025 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.46144 | 2.6 | 16.70837 | 18.10759 | 20.74142 | 0.5 | Brake |
| turn_entry/speed/050 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_entry/speed/075 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 17.47968 | 2.6 | 17.74188 | 19.22765 | 22.0244 | 0.5 | Brake |
| turn_entry/speed/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 17.98879 | 2.6 | 18.25863 | 19.78767 | 22.66588 | 0.5 | Ok |
| turn_entry/surface/baseline | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_entry/surface/grip_080 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.28115 | 2.47 | 16.52537 | 17.90926 | 20.51425 | 0.5 | RunWide |
| turn_entry/surface/moisture_070 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.30738 | 2.474875 | 16.55199 | 17.93812 | 20.5473 | 0.5 | RunWide |
| turn_entry/surface/ruts_025 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.58537 | 2.526875 | 16.83416 | 18.24391 | 20.89757 | 0.5 | Brake |
| turn_entry/traction_bias/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 17.47968 | 2.6 | 17.74188 | 19.22765 | 22.0244 | 0.5 | Brake |
| turn_entry/traction_bias/050 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_entry/traction_bias/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.46144 | 2.6 | 16.70837 | 18.10759 | 20.74142 | 0.5 | Brake |
| turn_entry/traction_bias_on_moisture_070/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 16.41862 | 2.474875 | 16.6649 | 18.06048 | 20.68746 | 0.5 | Brake |
| turn_entry/traction_bias_on_moisture_070/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 19.88933 | 15.82375 | 2.474875 | 16.06111 | 17.40612 | 19.93793 | 0.5 | RunWide |

| Scenario | Scrub decel m | Scrub carry m | Scrub exit | Scrub time | Constraint input | Target | Required correction m | Correction m | Correction exit | Correction time | Remaining m | Reached | Residual | Carry m | Carry time | Final exit | Total time |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| turn_entry/band/below_max | 0 | 14.13717 | 16.63115 | 0.850041 | 16.63115 | — | — | — | — | — | — | — | 0 | 14.13717 | 0.850041 | 16.63115 | 1.700083 |
| turn_entry/band/brake | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 7.584822 | true | 0 | 7.584822 | 0.44694 | 16.97056 | 1.569543 |
| turn_entry/band/crash_above_boundary | 14.13717 | 0 | 21.39988 | 0.636043 | 21.39988 | — | — | — | — | — | — | — | 0 | 0 | 0 | 0 | 0.636043 |
| turn_entry/band/quiet | 14.13717 | 0 | 17.09784 | 0.780519 | 17.09784 | 16.97056 | 0.833898 | 0.833898 | 16.97056 | 0.048954 | 13.30327 | true | 0 | 13.30327 | 0.783903 | 16.97056 | 1.613376 |
| turn_entry/band/run_wide | 14.13717 | 0 | 20.02526 | 0.676276 | 20.02526 | 18.49791 | 11.31508 | 11.31508 | 18.49791 | 0.587443 | 2.822084 | true | 0 | 2.822084 | 0.152562 | 18.49791 | 1.416281 |
| turn_entry/baseline | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 7.584822 | true | 0 | 7.584822 | 0.44694 | 16.97056 | 1.569543 |
| turn_entry/insufficient_distance | 0.141372 | 0 | 17.94637 | 0.007873 | 17.94637 | 16.97056 | 6.552345 | 0.141372 | 17.92588 | 0.007882 | 0 | false | 0.955315 | 0 | 0 | 17.92588 | 0.015755 |
| turn_entry/lateral/000 | 12.56637 | 0 | 18.17252 | 0.660313 | 18.17252 | 17.08626 | 7.365408 | 7.365408 | 17.08626 | 0.417792 | 5.200963 | true | 0 | 5.200963 | 0.304395 | 17.08626 | 1.382499 |
| turn_entry/lateral/001 | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 7.584822 | true | 0 | 7.584822 | 0.44694 | 16.97056 | 1.569543 |
| turn_entry/lateral/002 | 14.53566 | 1.172303 | 17.88854 | 0.835067 | 17.88854 | — | — | — | — | — | — | — | 0 | 15.70796 | 0.878102 | 17.88854 | 1.713169 |
| turn_entry/lateral/003 | 8.381814 | 8.896947 | 18.76166 | 0.907927 | 18.76166 | — | — | — | — | — | — | — | 0 | 17.27876 | 0.920961 | 18.76166 | 1.828888 |
| turn_entry/lateral/004 | 2.227965 | 16.62159 | 19.59592 | 0.961068 | 19.59592 | — | — | — | — | — | — | — | 0 | 18.84956 | 0.961912 | 19.59592 | 1.92298 |
| turn_entry/slide_control/000 | 14.13717 | 0 | 18.41295 | 0.738189 | 18.41295 | 17.14447 | 11.27596 | 11.27596 | 17.14447 | 0.634239 | 2.861212 | true | 0 | 2.861212 | 0.166888 | 17.14447 | 1.539317 |
| turn_entry/slide_control/025 | 14.13717 | 0 | 18.18116 | 0.742684 | 18.18116 | 17.33869 | 6.505264 | 6.505264 | 17.33869 | 0.366289 | 7.631903 | true | 0 | 7.631903 | 0.440166 | 17.33869 | 1.549139 |
| turn_entry/slide_control/050 | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 7.584822 | true | 0 | 7.584822 | 0.44694 | 16.97056 | 1.569543 |
| turn_entry/slide_control/075 | 14.13717 | 0 | 17.70847 | 0.752021 | 17.70847 | 17.22512 | 2.911226 | 2.911226 | 17.22512 | 0.166672 | 11.22594 | true | 0 | 11.22594 | 0.651719 | 17.22512 | 1.570412 |
| turn_entry/slide_control/100 | 14.06973 | 0.067439 | 17.47968 | 0.756874 | 17.47968 | — | — | — | — | — | — | — | 0 | 14.13717 | 0.808777 | 17.47968 | 1.565651 |
| turn_entry/speed/000 | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 16.94935 | 6.690722 | 6.690722 | 16.94935 | 0.38347 | 7.446445 | true | 0 | 7.446445 | 0.439335 | 16.94935 | 1.570097 |
| turn_entry/speed/025 | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 16.46144 | 9.825577 | 9.825577 | 16.46144 | 0.571125 | 4.31159 | true | 0 | 4.31159 | 0.261921 | 16.46144 | 1.580338 |
| turn_entry/speed/050 | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 7.584822 | true | 0 | 7.584822 | 0.44694 | 16.97056 | 1.569543 |
| turn_entry/speed/075 | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 17.47968 | 3.17942 | 3.17942 | 17.47968 | 0.179496 | 10.95775 | true | 0 | 10.95775 | 0.626885 | 17.47968 | 1.553674 |
| turn_entry/speed/100 | 13.84399 | 0.293181 | 17.98879 | 0.747273 | 17.98879 | — | — | — | — | — | — | — | 0 | 14.13717 | 0.785887 | 17.98879 | 1.533161 |
| turn_entry/surface/baseline | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 7.584822 | true | 0 | 7.584822 | 0.44694 | 16.97056 | 1.569543 |
| turn_entry/surface/grip_080 | 14.13717 | 0 | 18.04849 | 0.745281 | 18.04849 | 17.16482 | 6.298969 | 6.298969 | 17.16482 | 0.357761 | 7.838198 | true | 0 | 7.838198 | 0.456643 | 17.16482 | 1.559685 |
| turn_entry/surface/moisture_070 | 14.13717 | 0 | 18.04467 | 0.745356 | 18.04467 | 17.17603 | 6.180946 | 6.180946 | 17.17603 | 0.350984 | 7.956221 | true | 0 | 7.956221 | 0.463217 | 17.17603 | 1.559556 |
| turn_entry/surface/ruts_025 | 14.13717 | 0 | 18.00388 | 0.746158 | 18.00388 | 16.58537 | 9.708662 | 9.708662 | 16.58537 | 0.561369 | 4.428505 | true | 0 | 4.428505 | 0.267013 | 16.58537 | 1.57454 |
| turn_entry/traction_bias/000 | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 17.47968 | 3.17942 | 3.17942 | 17.47968 | 0.179496 | 10.95775 | true | 0 | 10.95775 | 0.626885 | 17.47968 | 1.553674 |
| turn_entry/traction_bias/050 | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 7.584822 | true | 0 | 7.584822 | 0.44694 | 16.97056 | 1.569543 |
| turn_entry/traction_bias/100 | 14.13717 | 0 | 17.94637 | 0.747293 | 17.94637 | 16.46144 | 9.825577 | 9.825577 | 16.46144 | 0.571125 | 4.31159 | true | 0 | 4.31159 | 0.261921 | 16.46144 | 1.580338 |
| turn_entry/traction_bias_on_moisture_070/000 | 14.13717 | 0 | 18.04467 | 0.745356 | 18.04467 | 16.41862 | 11.32156 | 11.32156 | 16.41862 | 0.657021 | 2.815611 | true | 0 | 2.815611 | 0.171489 | 16.41862 | 1.573866 |
| turn_entry/traction_bias_on_moisture_070/100 | 14.13717 | 0 | 18.04467 | 0.745356 | 18.04467 | 16.93421 | 7.847384 | 7.847384 | 16.93421 | 0.448693 | 6.289783 | true | 0 | 6.289783 | 0.371425 | 16.93421 | 1.565473 |


## TurnMiddle

Production order: constraint → correction → carry. NO positive drive.

Crash probes retain existing half-segment crash semantics. Available m is legal remaining segment length; travelled m is production distance. Do not interpret crashes as a modeled crash trajectory or apply recoverable full-distance equality to them. At outer lane 4 production crashes instead of RunWide, so the recoverable transition and observed retention are absent, not inferred from an inner lane.

| Scenario | Lateral | Progress | Radius | Available m | Travelled m | Incoming | Max safe | Correction capability | First Brake | First RunWide | First Crash | Observed retention | Outcome |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| turn_middle/adaptability/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/adaptability/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/band/below_max | 1 | 0 | 27 | 28.27433 | 28.27433 | 16.63115 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Ok |
| turn_middle/band/brake | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/band/crash_above_boundary | 1 | 0 | 27 | 28.27433 | 14.13717 | 21.39988 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Crash |
| turn_middle/band/quiet | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.09784 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Ok |
| turn_middle/band/run_wide | 1 | 0 | 27 | 28.27433 | 28.27433 | 20.02526 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | RunWide |
| turn_middle/baseline | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/insufficient_distance | 1 | 0.99 | 27 | 0.282743 | 0.282743 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/lateral/000 | 0 | 0 | 24 | 25.13274 | 25.13274 | 17.94637 | 16 | 2.6 | 16.24 | 17.6 | 20.16 | 0.5 | RunWide |
| turn_middle/lateral/001 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/lateral/002 | 2 | 0 | 30 | 31.41593 | 31.41593 | 17.94637 | 17.88854 | 2.6 | 18.15687 | 19.6774 | 22.53957 | 0.5 | Ok |
| turn_middle/lateral/003 | 3 | 0 | 33 | 34.55752 | 34.55752 | 17.94637 | 18.76166 | 2.6 | 19.04309 | 20.63783 | 23.6397 | 0.5 | Ok |
| turn_middle/lateral/004 | 4 | 0 | 36 | 37.69911 | 37.69911 | 17.94637 | 19.59592 | 2.6 | 19.88986 | — | 21.55551 | — | Ok |
| turn_middle/pair_riding/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/pair_riding/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/slide_control/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.46144 | 2 | 16.70837 | 17.44913 | 19.42451 | 0.35 | RunWide |
| turn_middle/slide_control/025 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.716 | 2.3 | 16.96675 | 18.05328 | 20.39352 | 0.425 | Brake |
| turn_middle/slide_control/050 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/slide_control/075 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 17.22512 | 2.9 | 17.4835 | 19.29214 | 22.39266 | 0.575 | Brake |
| turn_middle/slide_control/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 17.47968 | 3.2 | 17.74188 | 19.92684 | 23.42277 | 0.65 | Brake |
| turn_middle/speed/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 15.95233 | 2.6 | 16.19162 | 17.54756 | 20.09994 | 0.5 | RunWide |
| turn_middle/speed/025 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.46144 | 2.6 | 16.70837 | 18.10759 | 20.74142 | 0.5 | Brake |
| turn_middle/speed/050 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/speed/075 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 17.47968 | 2.6 | 17.74188 | 19.22765 | 22.0244 | 0.5 | Brake |
| turn_middle/speed/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 17.98879 | 2.6 | 18.25863 | 19.78767 | 22.66588 | 0.5 | Ok |
| turn_middle/surface/baseline | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/surface/grip_080 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.28115 | 2.47 | 16.52537 | 17.90926 | 20.51425 | 0.5 | RunWide |
| turn_middle/surface/moisture_070 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.30738 | 2.474875 | 16.55199 | 17.93812 | 20.5473 | 0.5 | RunWide |
| turn_middle/surface/ruts_025 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.58537 | 2.526875 | 16.83416 | 18.24391 | 20.89757 | 0.5 | Brake |
| turn_middle/track_reading/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/track_reading/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/traction_bias/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 17.47968 | 2.6 | 17.74188 | 19.22765 | 22.0244 | 0.5 | Brake |
| turn_middle/traction_bias/050 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_middle/traction_bias/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.46144 | 2.6 | 16.70837 | 18.10759 | 20.74142 | 0.5 | Brake |
| turn_middle/traction_bias_on_moisture_070/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.41862 | 2.474875 | 16.6649 | 18.06048 | 20.68746 | 0.5 | Brake |
| turn_middle/traction_bias_on_moisture_070/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 15.82375 | 2.474875 | 16.06111 | 17.40612 | 19.93793 | 0.5 | RunWide |

| Scenario | Scrub decel m | Scrub carry m | Scrub exit | Scrub time | Constraint input | Target | Required correction m | Correction m | Correction exit | Correction time | Remaining m | Reached | Residual | Carry m | Carry time | Final exit | Total time |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| turn_middle/adaptability/000 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/adaptability/100 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/band/below_max | — | — | — | — | 16.63115 | — | — | — | — | — | — | — | 0 | 28.27433 | 1.700083 | 16.63115 | 1.700083 |
| turn_middle/band/brake | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/band/crash_above_boundary | — | — | — | — | 21.39988 | — | — | — | — | — | — | — | 0 | 14.13717 | 1.321238 | 0 | 1.321238 |
| turn_middle/band/quiet | — | — | — | — | 17.09784 | 16.97056 | 0.833898 | 0.833898 | 16.97056 | 0.048954 | 27.44044 | true | 0 | 27.44044 | 1.616943 | 16.97056 | 1.665898 |
| turn_middle/band/run_wide | — | — | — | — | 20.02526 | 18.49791 | 11.31508 | 11.31508 | 18.49791 | 0.587443 | 16.95925 | true | 0 | 16.95925 | 0.91682 | 18.49791 | 1.504262 |
| turn_middle/baseline | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/insufficient_distance | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 0.282743 | 17.90536 | 0.015773 | 0 | false | 0.934797 | 0 | 0 | 17.90536 | 0.015773 |
| turn_middle/lateral/000 | — | — | — | — | 17.94637 | 16.97318 | 6.535225 | 6.535225 | 16.97318 | 0.374302 | 18.59752 | true | 0 | 18.59752 | 1.0957 | 16.97318 | 1.470002 |
| turn_middle/lateral/001 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/lateral/002 | — | — | — | — | 17.94637 | 17.88854 | 0.398492 | 0.398492 | 17.88854 | 0.02224 | 31.01744 | true | 0 | 31.01744 | 1.733927 | 17.88854 | 1.756168 |
| turn_middle/lateral/003 | — | — | — | — | 17.94637 | — | — | — | — | — | — | — | 0 | 34.55752 | 1.9256 | 17.94637 | 1.9256 |
| turn_middle/lateral/004 | — | — | — | — | 17.94637 | — | — | — | — | — | — | — | 0 | 37.69911 | 2.100654 | 17.94637 | 2.100654 |
| turn_middle/pair_riding/000 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/pair_riding/100 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/slide_control/000 | — | — | — | — | 17.94637 | 16.98117 | 8.428019 | 8.428019 | 16.98117 | 0.4826 | 19.84632 | true | 0 | 19.84632 | 1.168725 | 16.98117 | 1.651325 |
| turn_middle/slide_control/025 | — | — | — | — | 17.94637 | 16.716 | 9.271173 | 9.271173 | 16.716 | 0.534942 | 19.00316 | true | 0 | 19.00316 | 1.136824 | 16.716 | 1.671766 |
| turn_middle/slide_control/050 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/slide_control/075 | — | — | — | — | 17.94637 | 17.22512 | 4.373688 | 4.373688 | 17.22512 | 0.248706 | 23.90065 | true | 0 | 23.90065 | 1.387546 | 17.22512 | 1.636252 |
| turn_middle/slide_control/100 | — | — | — | — | 17.94637 | 17.47968 | 2.583279 | 2.583279 | 17.47968 | 0.145841 | 25.69106 | true | 0 | 25.69106 | 1.469767 | 17.47968 | 1.615608 |
| turn_middle/speed/000 | — | — | — | — | 17.94637 | 16.94935 | 6.690722 | 6.690722 | 16.94935 | 0.38347 | 21.58361 | true | 0 | 21.58361 | 1.273418 | 16.94935 | 1.656888 |
| turn_middle/speed/025 | — | — | — | — | 17.94637 | 16.46144 | 9.825577 | 9.825577 | 16.46144 | 0.571125 | 18.44876 | true | 0 | 18.44876 | 1.120725 | 16.46144 | 1.69185 |
| turn_middle/speed/050 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/speed/075 | — | — | — | — | 17.94637 | 17.47968 | 3.17942 | 3.17942 | 17.47968 | 0.179496 | 25.09491 | true | 0 | 25.09491 | 1.435662 | 17.47968 | 1.615158 |
| turn_middle/speed/100 | — | — | — | — | 17.94637 | — | — | — | — | — | — | — | 0 | 28.27433 | 1.57549 | 17.94637 | 1.57549 |
| turn_middle/surface/baseline | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/surface/grip_080 | — | — | — | — | 17.94637 | 17.11376 | 5.909201 | 5.909201 | 17.11376 | 0.33709 | 22.36513 | true | 0 | 22.36513 | 1.306851 | 17.11376 | 1.643941 |
| turn_middle/surface/moisture_070 | — | — | — | — | 17.94637 | 17.12688 | 5.80681 | 5.80681 | 17.12688 | 0.331125 | 22.46752 | true | 0 | 22.46752 | 1.311828 | 17.12688 | 1.642953 |
| turn_middle/surface/ruts_025 | — | — | — | — | 17.94637 | 16.58537 | 9.299544 | 9.299544 | 16.58537 | 0.538609 | 18.97479 | true | 0 | 18.97479 | 1.144068 | 16.58537 | 1.682676 |
| turn_middle/track_reading/000 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/track_reading/100 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/traction_bias/000 | — | — | — | — | 17.94637 | 17.47968 | 3.17942 | 3.17942 | 17.47968 | 0.179496 | 25.09491 | true | 0 | 25.09491 | 1.435662 | 17.47968 | 1.615158 |
| turn_middle/traction_bias/050 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 21.72199 | 1.279981 | 16.97056 | 1.655291 |
| turn_middle/traction_bias/100 | — | — | — | — | 17.94637 | 16.46144 | 9.825577 | 9.825577 | 16.46144 | 0.571125 | 18.44876 | true | 0 | 18.44876 | 1.120725 | 16.46144 | 1.69185 |
| turn_middle/traction_bias_on_moisture_070/000 | — | — | — | — | 17.94637 | 16.41862 | 10.60681 | 10.60681 | 16.41862 | 0.617303 | 17.66752 | true | 0 | 17.66752 | 1.076066 | 16.41862 | 1.69337 |
| turn_middle/traction_bias_on_moisture_070/100 | — | — | — | — | 17.94637 | 16.88506 | 7.468446 | 7.468446 | 16.88506 | 0.428834 | 20.80589 | true | 0 | 20.80589 | 1.232207 | 16.88506 | 1.661041 |


## TurnExit

Production order: constraint → correction → remaining-distance drive. RunWide uses correction + carry, NO DRIVE. Full correction consumption also leaves NO DRIVE.

Crash probes retain existing half-segment crash semantics. Available m is legal remaining segment length; travelled m is production distance. Do not interpret crashes as a modeled crash trajectory or apply recoverable full-distance equality to them. At outer lane 4 production crashes instead of RunWide, so the recoverable transition and observed retention are absent, not inferred from an inner lane.

| Scenario | Lateral | Progress | Radius | Available m | Travelled m | Incoming | Max safe | Correction capability | First Brake | First RunWide | First Crash | Observed retention | Outcome |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| turn_exit/band/below_max | 1 | 0 | 27 | 28.27433 | 28.27433 | 16.63115 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Ok |
| turn_exit/band/brake | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/band/crash_above_boundary | 1 | 0 | 27 | 28.27433 | 14.13717 | 21.39988 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Crash |
| turn_exit/band/quiet | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.09784 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Ok |
| turn_exit/band/run_wide | 1 | 0 | 27 | 28.27433 | 28.27433 | 20.02526 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | RunWide |
| turn_exit/baseline | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/gearing/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/gearing/050 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/gearing/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/insufficient_distance | 1 | 0.99 | 27 | 0.282743 | 0.282743 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/lateral/000 | 0 | 0 | 24 | 25.13274 | 25.13274 | 17.94637 | 16 | 2.6 | 16.24 | 17.6 | 20.16 | 0.5 | RunWide |
| turn_exit/lateral/001 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/lateral/002 | 2 | 0 | 30 | 31.41593 | 31.41593 | 17.94637 | 17.88854 | 2.6 | 18.15687 | 19.6774 | 22.53957 | 0.5 | Ok |
| turn_exit/lateral/003 | 3 | 0 | 33 | 34.55752 | 34.55752 | 17.94637 | 18.76166 | 2.6 | 19.04309 | 20.63783 | 23.6397 | 0.5 | Ok |
| turn_exit/lateral/004 | 4 | 0 | 36 | 37.69911 | 37.69911 | 17.94637 | 19.59592 | 2.6 | 19.88986 | — | 21.55551 | — | Ok |
| turn_exit/slide_control/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.46144 | 2 | 16.70837 | 17.44913 | 19.42451 | 0.35 | RunWide |
| turn_exit/slide_control/025 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.716 | 2.3 | 16.96675 | 18.05328 | 20.39352 | 0.425 | Brake |
| turn_exit/slide_control/050 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/slide_control/075 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 17.22512 | 2.9 | 17.4835 | 19.29214 | 22.39266 | 0.575 | Brake |
| turn_exit/slide_control/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 17.47968 | 3.2 | 17.74188 | 19.92684 | 23.42277 | 0.65 | Brake |
| turn_exit/speed/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 15.95233 | 2.6 | 16.19162 | 17.54756 | 20.09994 | 0.5 | RunWide |
| turn_exit/speed/025 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.46144 | 2.6 | 16.70837 | 18.10759 | 20.74142 | 0.5 | Brake |
| turn_exit/speed/050 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/speed/075 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 17.47968 | 2.6 | 17.74188 | 19.22765 | 22.0244 | 0.5 | Brake |
| turn_exit/speed/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 17.98879 | 2.6 | 18.25863 | 19.78767 | 22.66588 | 0.5 | Ok |
| turn_exit/surface/baseline | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/surface/grip_080 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.28115 | 2.47 | 16.52537 | 17.90926 | 20.51425 | 0.5 | RunWide |
| turn_exit/surface/moisture_070 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.30738 | 2.474875 | 16.55199 | 17.93812 | 20.5473 | 0.5 | RunWide |
| turn_exit/surface/ruts_025 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.58537 | 2.526875 | 16.83416 | 18.24391 | 20.89757 | 0.5 | Brake |
| turn_exit/traction_bias/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 17.47968 | 2.6 | 17.74188 | 19.22765 | 22.0244 | 0.5 | Brake |
| turn_exit/traction_bias/050 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.97056 | 2.6 | 17.22512 | 18.66762 | 21.38291 | 0.5 | Brake |
| turn_exit/traction_bias/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.46144 | 2.6 | 16.70837 | 18.10759 | 20.74142 | 0.5 | Brake |
| turn_exit/traction_bias_on_moisture_070/000 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 16.41862 | 2.474875 | 16.6649 | 18.06048 | 20.68746 | 0.5 | Brake |
| turn_exit/traction_bias_on_moisture_070/100 | 1 | 0 | 27 | 28.27433 | 28.27433 | 17.94637 | 15.82375 | 2.474875 | 16.06111 | 17.40612 | 19.93793 | 0.5 | RunWide |

| Scenario | Scrub decel m | Scrub carry m | Scrub exit | Scrub time | Constraint input | Target | Required correction m | Correction m | Correction exit | Correction time | Remaining m | Reached | Residual | Carry m | Carry time | Final exit | Total time |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| turn_exit/band/below_max | — | — | — | — | 16.63115 | — | — | — | — | — | — | — | 0 | 0 | 0 | 18.10977 | 1.626462 |
| turn_exit/band/brake | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 0 | 0 | 18.08419 | 1.614078 |
| turn_exit/band/crash_above_boundary | — | — | — | — | 21.39988 | — | — | — | — | — | — | — | 0 | 14.13717 | 1.321238 | 0 | 1.321238 |
| turn_exit/band/quiet | — | — | — | — | 17.09784 | 16.97056 | 0.833898 | 0.833898 | 16.97056 | 0.048954 | 27.44044 | true | 0 | 0 | 0 | 18.35164 | 1.601605 |
| turn_exit/band/run_wide | — | — | — | — | 20.02526 | 18.49791 | 11.31508 | 11.31508 | 18.49791 | 0.587443 | 16.95925 | true | 0 | 16.95925 | 0.91682 | 18.49791 | 1.504262 |
| turn_exit/baseline | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 0 | 0 | 18.08419 | 1.614078 |
| turn_exit/gearing/000 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 0 | 0 | 18.17703 | 1.610657 |
| turn_exit/gearing/050 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 0 | 0 | 18.08419 | 1.614078 |
| turn_exit/gearing/100 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 0 | 0 | 17.98728 | 1.617639 |
| turn_exit/insufficient_distance | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 0.282743 | 17.90536 | 0.015773 | 0 | false | 0.934797 | 0 | 0 | 17.90536 | 0.015773 |
| turn_exit/lateral/000 | — | — | — | — | 17.94637 | 16.97318 | 6.535225 | 6.535225 | 16.97318 | 0.374302 | 18.59752 | true | 0 | 18.59752 | 1.0957 | 16.97318 | 1.470002 |
| turn_exit/lateral/001 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 0 | 0 | 18.08419 | 1.614078 |
| turn_exit/lateral/002 | — | — | — | — | 17.94637 | 17.88854 | 0.398492 | 0.398492 | 17.88854 | 0.02224 | 31.01744 | true | 0 | 0 | 0 | 19.26612 | 1.690662 |
| turn_exit/lateral/003 | — | — | — | — | 17.94637 | — | — | — | — | — | — | — | 0 | 0 | 0 | 19.45426 | 1.846336 |
| turn_exit/lateral/004 | — | — | — | — | 17.94637 | — | — | — | — | — | — | — | 0 | 0 | 0 | 19.57647 | 2.007315 |
| turn_exit/slide_control/000 | — | — | — | — | 17.94637 | 16.98117 | 8.428019 | 8.428019 | 16.98117 | 0.4826 | 19.84632 | true | 0 | 19.84632 | 1.168725 | 16.98117 | 1.651325 |
| turn_exit/slide_control/025 | — | — | — | — | 17.94637 | 16.716 | 9.271173 | 9.271173 | 16.716 | 0.534942 | 19.00316 | true | 0 | 0 | 0 | 17.7303 | 1.637892 |
| turn_exit/slide_control/050 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 0 | 0 | 18.08419 | 1.614078 |
| turn_exit/slide_control/075 | — | — | — | — | 17.94637 | 17.22512 | 4.373688 | 4.373688 | 17.22512 | 0.248706 | 23.90065 | true | 0 | 0 | 0 | 18.40417 | 1.589656 |
| turn_exit/slide_control/100 | — | — | — | — | 17.94637 | 17.47968 | 2.583279 | 2.583279 | 17.47968 | 0.145841 | 25.69106 | true | 0 | 0 | 0 | 18.70057 | 1.565225 |
| turn_exit/speed/000 | — | — | — | — | 17.94637 | 16.94935 | 6.690722 | 6.690722 | 16.94935 | 0.38347 | 21.58361 | true | 0 | 21.58361 | 1.273418 | 16.94935 | 1.656888 |
| turn_exit/speed/025 | — | — | — | — | 17.94637 | 16.46144 | 9.825577 | 9.825577 | 16.46144 | 0.571125 | 18.44876 | true | 0 | 0 | -0 | 17.27631 | 1.664475 |
| turn_exit/speed/050 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 0 | 0 | 18.08419 | 1.614078 |
| turn_exit/speed/075 | — | — | — | — | 17.94637 | 17.47968 | 3.17942 | 3.17942 | 17.47968 | 0.179496 | 25.09491 | true | 0 | 0 | 0 | 18.92262 | 1.557349 |
| turn_exit/speed/100 | — | — | — | — | 17.94637 | — | — | — | — | — | — | — | 0 | 0 | 0 | 19.73599 | 1.499315 |
| turn_exit/surface/baseline | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 0 | 0 | 18.08419 | 1.614078 |
| turn_exit/surface/grip_080 | — | — | — | — | 17.94637 | 17.11376 | 5.909201 | 5.909201 | 17.11376 | 0.33709 | 22.36513 | true | 0 | 22.36513 | 1.306851 | 17.11376 | 1.643941 |
| turn_exit/surface/moisture_070 | — | — | — | — | 17.94637 | 17.12688 | 5.80681 | 5.80681 | 17.12688 | 0.331125 | 22.46752 | true | 0 | 22.46752 | 1.311828 | 17.12688 | 1.642953 |
| turn_exit/surface/ruts_025 | — | — | — | — | 17.94637 | 16.58537 | 9.299544 | 9.299544 | 16.58537 | 0.538609 | 18.97479 | true | 0 | 0 | -0 | 17.5857 | 1.648787 |
| turn_exit/traction_bias/000 | — | — | — | — | 17.94637 | 17.47968 | 3.17942 | 3.17942 | 17.47968 | 0.179496 | 25.09491 | true | 0 | 0 | 0 | 18.67444 | 1.56698 |
| turn_exit/traction_bias/050 | — | — | — | — | 17.94637 | 16.97056 | 6.552345 | 6.552345 | 16.97056 | 0.375311 | 21.72199 | true | 0 | 0 | 0 | 18.08419 | 1.614078 |
| turn_exit/traction_bias/100 | — | — | — | — | 17.94637 | 16.46144 | 9.825577 | 9.825577 | 16.46144 | 0.571125 | 18.44876 | true | 0 | 0 | -0 | 17.47916 | 1.657855 |
| turn_exit/traction_bias_on_moisture_070/000 | — | — | — | — | 17.94637 | 16.41862 | 10.60681 | 10.60681 | 16.41862 | 0.617303 | 17.66752 | true | 0 | 0 | 0 | 17.35417 | 1.663231 |
| turn_exit/traction_bias_on_moisture_070/100 | — | — | — | — | 17.94637 | 16.88506 | 7.468446 | 7.468446 | 16.88506 | 0.428834 | 20.80589 | true | 0 | 20.80589 | 1.232207 | 16.88506 | 1.661041 |

| Scenario | Drive m | Drive entry | Drive exit | Drive peak | Drive time | Drive accel m | Drive cruise m | Drive decel m | Entry net accel | Equilibrium |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| turn_exit/band/below_max | 28.27433 | 16.63115 | 18.10977 | 18.10977 | 1.626462 | 28.27433 | 0 | 0 | 0.959332 | 28.35396 |
| turn_exit/band/brake | 21.72199 | 16.97056 | 18.08419 | 18.08419 | 1.238768 | 21.72199 | 0 | 0 | 0.936998 | 28.35396 |
| turn_exit/band/crash_above_boundary | 0 | — | — | — | — | — | — | — | — | — |
| turn_exit/band/quiet | 27.44044 | 16.97056 | 18.35164 | 18.35164 | 1.552651 | 27.44044 | 0 | 0 | 0.936998 | 28.35396 |
| turn_exit/band/run_wide | 0 | — | — | — | — | — | — | — | — | — |
| turn_exit/baseline | 21.72199 | 16.97056 | 18.08419 | 18.08419 | 1.238768 | 21.72199 | 0 | 0 | 0.936998 | 28.35396 |
| turn_exit/gearing/000 | 21.72199 | 16.97056 | 18.17703 | 18.17703 | 1.235347 | 21.72199 | 0 | 0 | 1.025338 | 27.91271 |
| turn_exit/gearing/050 | 21.72199 | 16.97056 | 18.08419 | 18.08419 | 1.238768 | 21.72199 | 0 | 0 | 0.936998 | 28.35396 |
| turn_exit/gearing/100 | 21.72199 | 16.97056 | 17.98728 | 17.98728 | 1.242328 | 21.72199 | 0 | 0 | 0.847445 | 28.72826 |
| turn_exit/insufficient_distance | 0 | — | — | — | — | — | — | — | — | — |
| turn_exit/lateral/000 | 0 | — | — | — | — | — | — | — | — | — |
| turn_exit/lateral/001 | 21.72199 | 16.97056 | 18.08419 | 18.08419 | 1.238768 | 21.72199 | 0 | 0 | 0.936998 | 28.35396 |
| turn_exit/lateral/002 | 31.01744 | 17.88854 | 19.26612 | 19.26612 | 1.668422 | 31.01744 | 0 | 0 | 0.874968 | 28.35396 |
| turn_exit/lateral/003 | 34.55752 | 17.94637 | 19.45426 | 19.45426 | 1.846336 | 34.55752 | 0 | 0 | 0.870981 | 28.35396 |
| turn_exit/lateral/004 | 37.69911 | 17.94637 | 19.57647 | 19.57647 | 2.007315 | 37.69911 | 0 | 0 | 0.870981 | 28.35396 |
| turn_exit/slide_control/000 | 0 | — | — | — | — | — | — | — | — | — |
| turn_exit/slide_control/025 | 19.00316 | 16.716 | 17.7303 | 17.7303 | 1.102951 | 19.00316 | 0 | 0 | 0.953779 | 28.35396 |
| turn_exit/slide_control/050 | 21.72199 | 16.97056 | 18.08419 | 18.08419 | 1.238768 | 21.72199 | 0 | 0 | 0.936998 | 28.35396 |
| turn_exit/slide_control/075 | 23.90065 | 17.22512 | 18.40417 | 18.40417 | 1.340949 | 23.90065 | 0 | 0 | 0.920035 | 28.35396 |
| turn_exit/slide_control/100 | 25.69106 | 17.47968 | 18.70057 | 18.70057 | 1.419385 | 25.69106 | 0 | 0 | 0.902889 | 28.35396 |
| turn_exit/speed/000 | 0 | — | — | — | — | — | — | — | — | — |
| turn_exit/speed/025 | 18.44876 | 16.46144 | 17.27631 | 17.27631 | 1.093351 | 18.44876 | 0 | 0 | 0.771415 | 26.51224 |
| turn_exit/speed/050 | 21.72199 | 16.97056 | 18.08419 | 18.08419 | 1.238768 | 21.72199 | 0 | 0 | 0.936998 | 28.35396 |
| turn_exit/speed/075 | 25.09491 | 17.47968 | 18.92262 | 18.92262 | 1.377852 | 25.09491 | 0 | 0 | 1.09956 | 30.02662 |
| turn_exit/speed/100 | 28.27433 | 17.94637 | 19.73599 | 19.73599 | 1.499315 | 28.27433 | 0 | 0 | 1.262222 | 31.56181 |
| turn_exit/surface/baseline | 21.72199 | 16.97056 | 18.08419 | 18.08419 | 1.238768 | 21.72199 | 0 | 0 | 0.936998 | 28.35396 |
| turn_exit/surface/grip_080 | 0 | — | — | — | — | — | — | — | — | — |
| turn_exit/surface/moisture_070 | 0 | — | — | — | — | — | — | — | — | — |
| turn_exit/surface/ruts_025 | 18.97479 | 16.58537 | 17.5857 | 17.5857 | 1.110179 | 18.97479 | 0 | 0 | 0.93438 | 28.10604 |
| turn_exit/traction_bias/000 | 25.09491 | 17.47968 | 18.67444 | 18.67444 | 1.387484 | 25.09491 | 0 | 0 | 0.902889 | 28.35396 |
| turn_exit/traction_bias/050 | 21.72199 | 16.97056 | 18.08419 | 18.08419 | 1.238768 | 21.72199 | 0 | 0 | 0.936998 | 28.35396 |
| turn_exit/traction_bias/100 | 18.44876 | 16.46144 | 17.47916 | 17.47916 | 1.08673 | 18.44876 | 0 | 0 | 0.970377 | 28.35396 |
| turn_exit/traction_bias_on_moisture_070/000 | 17.66752 | 16.41862 | 17.35417 | 17.35417 | 1.045928 | 17.66752 | 0 | 0 | 0.925253 | 27.92767 |
| turn_exit/traction_bias_on_moisture_070/100 | 0 | — | — | — | — | — | — | — | — | — |


## Line Geometry

Single-rider free runs request a fixed HoldLane reference 0–4. The observer never forcibly resets lateral position or disables production consequences; observed min/max verifies whether the line stayed fixed. Lap trajectory is a canonical geometry measurement; actual accumulated distance is reported separately.

| Scenario | Lateral | Normalized fraction | Straight offset | Turn offset | Radius | Arc m | Reference lap m | Max safe | Observed min | Observed max |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| line_geometry/lateral/000 | 0 | 0 | 0 | 0 | 24 | 25.13274 | 280.7964 | 16 | 0 | 0 |
| line_geometry/lateral/001 | 1 | 0.25 | 2 | 3 | 27 | 28.27433 | 299.646 | 16.97056 | 1 | 1 |
| line_geometry/lateral/002 | 2 | 0.5 | 4 | 6 | 30 | 31.41593 | 318.4956 | 17.88854 | 2 | 2 |
| line_geometry/lateral/003 | 3 | 0.75 | 6 | 9 | 33 | 34.55752 | 337.3451 | 18.76166 | 3 | 3 |
| line_geometry/lateral/004 | 4 | 1 | 8 | 12 | 36 | 37.69911 | 356.1947 | 19.59592 | 4 | 4 |

| Scenario | Rider | Vmax km/h | Average m/s | L1 | L2 | L3 | L4 | Flying median | L1 penalty | Heat time | Distance | RunWide | Brake | Crash |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| line_geometry/lateral/000 | 1 | 72.415997 | 16.92296 | 17.38141 | 16.24613 | 16.34987 | 16.3931 | 16.34987 | 1.031544 | 66.37051 | 1123.186 | 0 | 0 | 0 |
| line_geometry/lateral/001 | 1 | 75.152911 | 17.76229 | 17.68479 | 16.51269 | 16.61869 | 16.66296 | 16.61869 | 1.066093 | 67.47913 | 1198.584 | 0 | 0 | 0 |
| line_geometry/lateral/002 | 1 | 77.714271 | 18.55977 | 18.0001 | 16.79331 | 16.90169 | 16.94704 | 16.90169 | 1.098417 | 68.64215 | 1273.982 | 0 | 0 | 0 |
| line_geometry/lateral/003 | 1 | 80.119157 | 19.32054 | 18.32341 | 17.0835 | 17.19421 | 17.24064 | 17.19421 | 1.129196 | 69.84176 | 1349.38 | 0 | 0 | 0 |
| line_geometry/lateral/004 | 1 | 82.383282 | 20.04864 | 18.6517 | 17.38031 | 17.49331 | 17.54078 | 17.49331 | 1.158386 | 71.06609 | 1424.778 | 0 | 0 | 0 |


## Full Heat

Baseline = four identical riders; each skill/setup/surface axis remains a controlled fixture, not a real rider mapping. L1 penalty = L1 − median(L2,L3,L4). Correction residual is sampled before any subsequent TurnExit drive, not inferred from final overspeed.

| Scenario | Rider | Vmax km/h | Average m/s | L1 | L2 | L3 | L4 | Flying median | L1 penalty | Heat time | Distance | RunWide | Brake | Crash |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| full_heat/baseline | 1 | 72.415524 | 16.91381 | 17.38141 | 16.25193 | 16.36186 | 16.41125 | 16.36186 | 1.019554 | 66.40646 | 1123.186 | 0 | 0 | 0 |
| full_heat/baseline | 2 | 75.151902 | 17.74297 | 17.68479 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.041573 | 67.55262 | 1198.584 | 0 | 0 | 0 |
| full_heat/baseline | 3 | 77.713206 | 18.53945 | 18.0001 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.073324 | 68.71738 | 1273.982 | 0 | 0 | 0 |
| full_heat/baseline | 4 | 80.118608 | 19.30991 | 18.32341 | 17.08968 | 17.20704 | 17.26007 | 17.20704 | 1.116371 | 69.88019 | 1349.38 | 0 | 0 | 0 |
| full_heat/gearing/000 | 1 | 73.21206 | 17.0054 | 17.23268 | 16.1834 | 16.29201 | 16.3407 | 16.29201 | 0.940672 | 66.04879 | 1123.186 | 0 | 0 | 0 |
| full_heat/gearing/000 | 2 | 75.788725 | 17.82912 | 17.5406 | 16.46504 | 16.58227 | 16.63831 | 16.58227 | 0.95833 | 67.22622 | 1198.584 | 0 | 0 | 0 |
| full_heat/gearing/000 | 3 | 78.245055 | 18.62035 | 17.85974 | 16.75393 | 16.87386 | 16.9313 | 16.87386 | 0.985882 | 68.41882 | 1273.982 | 0 | 0 | 0 |
| full_heat/gearing/000 | 4 | 80.629864 | 19.38573 | 18.18605 | 17.04552 | 17.16152 | 17.2138 | 17.16152 | 1.024532 | 69.60689 | 1349.38 | 0 | 0 | 0 |
| full_heat/gearing/050 | 1 | 72.415524 | 16.91381 | 17.38141 | 16.25193 | 16.36186 | 16.41125 | 16.36186 | 1.019554 | 66.40646 | 1123.186 | 0 | 0 | 0 |
| full_heat/gearing/050 | 2 | 75.151902 | 17.74297 | 17.68479 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.041573 | 67.55262 | 1198.584 | 0 | 0 | 0 |
| full_heat/gearing/050 | 3 | 77.713206 | 18.53945 | 18.0001 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.073324 | 68.71738 | 1273.982 | 0 | 0 | 0 |
| full_heat/gearing/050 | 4 | 80.118608 | 19.30991 | 18.32341 | 17.08968 | 17.20704 | 17.26007 | 17.20704 | 1.116371 | 69.88019 | 1349.38 | 0 | 0 | 0 |
| full_heat/gearing/100 | 1 | 71.653326 | 16.81079 | 17.55503 | 16.32848 | 16.43983 | 16.49004 | 16.43983 | 1.115196 | 66.81339 | 1123.186 | 0 | 0 | 0 |
| full_heat/gearing/100 | 2 | 74.454476 | 17.64485 | 17.85459 | 16.59179 | 16.71205 | 16.76984 | 16.71205 | 1.14254 | 67.92826 | 1198.584 | 0 | 0 | 0 |
| full_heat/gearing/100 | 3 | 76.979491 | 18.44605 | 18.16705 | 16.86446 | 16.98734 | 17.04649 | 16.98734 | 1.179714 | 69.06533 | 1273.982 | 0 | 0 | 0 |
| full_heat/gearing/100 | 4 | 79.473045 | 19.22101 | 18.48804 | 17.1413 | 17.26011 | 17.31392 | 17.26011 | 1.22794 | 70.20338 | 1349.38 | 0 | 0 | 0 |
| full_heat/slide_control/000 | 1 | 70.33404 | 16.41866 | 17.8547 | 16.75815 | 16.87258 | 16.92367 | 16.87258 | 0.982117 | 68.4091 | 1123.186 | 0 | 0 | 0 |
| full_heat/slide_control/000 | 2 | 73.011134 | 17.23294 | 18.15713 | 17.02997 | 17.15308 | 17.21174 | 17.15308 | 1.004051 | 69.55193 | 1198.584 | 0 | 0 | 0 |
| full_heat/slide_control/000 | 3 | 75.611508 | 18.014 | 18.47399 | 17.31202 | 17.43785 | 17.49793 | 17.43785 | 1.036131 | 70.72179 | 1273.982 | 0 | 0 | 0 |
| full_heat/slide_control/000 | 4 | 77.988771 | 18.76973 | 18.79947 | 17.59799 | 17.71963 | 17.77421 | 17.71963 | 1.079836 | 71.8913 | 1349.38 | 0 | 0 | 0 |
| full_heat/slide_control/025 | 1 | 71.417251 | 16.6718 | 17.61021 | 16.49528 | 16.60735 | 16.65755 | 16.60735 | 1.002853 | 67.37039 | 1123.186 | 0 | 0 | 0 |
| full_heat/slide_control/025 | 2 | 74.170864 | 17.49311 | 17.91373 | 16.76818 | 16.88892 | 16.94666 | 16.88892 | 1.024817 | 68.51749 | 1198.584 | 0 | 0 | 0 |
| full_heat/slide_control/025 | 3 | 76.699395 | 18.2816 | 18.23004 | 17.05013 | 17.17365 | 17.23278 | 17.17365 | 1.056389 | 69.68661 | 1273.982 | 0 | 0 | 0 |
| full_heat/slide_control/025 | 4 | 79.099413 | 19.04441 | 18.55479 | 17.33568 | 17.45507 | 17.50884 | 17.45507 | 1.099716 | 70.85439 | 1349.38 | 0 | 0 | 0 |
| full_heat/slide_control/050 | 1 | 72.415524 | 16.91381 | 17.38141 | 16.25193 | 16.36186 | 16.41125 | 16.36186 | 1.019554 | 66.40646 | 1123.186 | 0 | 0 | 0 |
| full_heat/slide_control/050 | 2 | 75.151902 | 17.74297 | 17.68479 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.041573 | 67.55262 | 1198.584 | 0 | 0 | 0 |
| full_heat/slide_control/050 | 3 | 77.713206 | 18.53945 | 18.0001 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.073324 | 68.71738 | 1273.982 | 0 | 0 | 0 |
| full_heat/slide_control/050 | 4 | 80.118608 | 19.30991 | 18.32341 | 17.08968 | 17.20704 | 17.26007 | 17.20704 | 1.116371 | 69.88019 | 1349.38 | 0 | 0 | 0 |
| full_heat/slide_control/075 | 1 | 73.389331 | 17.14717 | 17.16558 | 16.02417 | 16.13214 | 16.18078 | 16.13214 | 1.033436 | 65.50268 | 1123.186 | 0 | 0 | 0 |
| full_heat/slide_control/075 | 2 | 76.111592 | 17.98493 | 17.46803 | 16.29536 | 16.41216 | 16.46825 | 16.41216 | 1.05587 | 66.64381 | 1198.584 | 0 | 0 | 0 |
| full_heat/slide_control/075 | 3 | 78.636573 | 18.78982 | 17.78196 | 16.57446 | 16.69391 | 16.7514 | 16.69391 | 1.088047 | 67.80172 | 1273.982 | 0 | 0 | 0 |
| full_heat/slide_control/075 | 4 | 81.171002 | 19.56836 | 18.10336 | 16.85682 | 16.97235 | 17.0247 | 16.97235 | 1.131016 | 68.95723 | 1349.38 | 0 | 0 | 0 |
| full_heat/slide_control/100 | 1 | 74.509682 | 17.37376 | 16.96069 | 15.809 | 15.91535 | 15.96336 | 15.91535 | 1.045336 | 64.6484 | 1123.186 | 0 | 0 | 0 |
| full_heat/slide_control/100 | 2 | 77.172336 | 18.22072 | 17.26169 | 16.0781 | 16.19312 | 16.24848 | 16.19312 | 1.068569 | 65.78139 | 1198.584 | 0 | 0 | 0 |
| full_heat/slide_control/100 | 3 | 79.793598 | 19.03436 | 17.57382 | 16.35493 | 16.47258 | 16.52932 | 16.47258 | 1.10124 | 66.93066 | 1273.982 | 0 | 0 | 0 |
| full_heat/slide_control/100 | 4 | 82.514788 | 19.82124 | 17.8932 | 16.63497 | 16.7488 | 16.8005 | 16.7488 | 1.1444 | 68.07747 | 1349.38 | 0 | 0 | 0 |
| full_heat/speed/000 | 1 | 69.169462 | 15.76831 | 18.49291 | 17.48232 | 17.6008 | 17.65457 | 17.6008 | 0.892117 | 71.23059 | 1123.186 | 0 | 0 | 0 |
| full_heat/speed/000 | 2 | 72.033742 | 16.54974 | 18.80588 | 17.76711 | 17.89432 | 17.95585 | 17.89432 | 0.911556 | 72.42316 | 1198.584 | 0 | 0 | 0 |
| full_heat/speed/000 | 3 | 74.336826 | 17.29553 | 19.13662 | 18.06689 | 18.19664 | 18.25947 | 18.19664 | 0.939983 | 73.65961 | 1273.982 | 0 | 0 | 0 |
| full_heat/speed/000 | 4 | 76.929785 | 18.01605 | 19.4762 | 18.37146 | 18.49694 | 18.55419 | 18.49694 | 0.979252 | 74.89879 | 1349.38 | 0 | 0 | 0 |
| full_heat/speed/025 | 1 | 70.528677 | 16.35422 | 17.90763 | 16.83056 | 16.94451 | 16.99594 | 16.94451 | 0.963123 | 68.67865 | 1123.186 | 0 | 0 | 0 |
| full_heat/speed/025 | 2 | 73.041991 | 17.15868 | 18.2171 | 17.11062 | 17.23314 | 17.2921 | 17.23314 | 0.983965 | 69.85295 | 1198.584 | 0 | 0 | 0 |
| full_heat/speed/025 | 3 | 75.471659 | 17.92968 | 18.54007 | 17.40107 | 17.52642 | 17.58683 | 17.52642 | 1.013643 | 71.0544 | 1273.982 | 0 | 0 | 0 |
| full_heat/speed/025 | 4 | 78.256961 | 18.67528 | 18.87133 | 17.69537 | 17.8166 | 17.8716 | 17.8166 | 1.054733 | 72.2549 | 1349.38 | 0 | 0 | 0 |
| full_heat/speed/050 | 1 | 72.415524 | 16.91381 | 17.38141 | 16.25193 | 16.36186 | 16.41125 | 16.36186 | 1.019554 | 66.40646 | 1123.186 | 0 | 0 | 0 |
| full_heat/speed/050 | 2 | 75.151902 | 17.74297 | 17.68479 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.041573 | 67.55262 | 1198.584 | 0 | 0 | 0 |
| full_heat/speed/050 | 3 | 77.713206 | 18.53945 | 18.0001 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.073324 | 68.71738 | 1273.982 | 0 | 0 | 0 |
| full_heat/speed/050 | 4 | 80.118608 | 19.30991 | 18.32341 | 17.08968 | 17.20704 | 17.26007 | 17.20704 | 1.116371 | 69.88019 | 1349.38 | 0 | 0 | 0 |
| full_heat/speed/075 | 1 | 75.130245 | 17.45259 | 16.90273 | 15.73098 | 15.83751 | 15.88518 | 15.83751 | 1.065224 | 64.3564 | 1123.186 | 0 | 0 | 0 |
| full_heat/speed/075 | 2 | 78.007434 | 18.30772 | 17.19927 | 15.99474 | 16.10987 | 16.16491 | 16.10987 | 1.089392 | 65.46879 | 1198.584 | 0 | 0 | 0 |
| full_heat/speed/075 | 3 | 80.647305 | 19.12947 | 17.50731 | 16.2662 | 16.38401 | 16.44039 | 16.38401 | 1.123304 | 66.59791 | 1273.982 | 0 | 0 | 0 |
| full_heat/speed/075 | 4 | 83.138022 | 19.92462 | 17.82294 | 16.54076 | 16.65466 | 16.70592 | 16.65466 | 1.168282 | 67.72427 | 1349.38 | 0 | 0 | 0 |
| full_heat/speed/100 | 1 | 77.713447 | 17.97521 | 16.46338 | 15.25618 | 15.35974 | 15.40596 | 15.35974 | 1.103643 | 62.48526 | 1123.186 | 0 | 0 | 0 |
| full_heat/speed/100 | 2 | 80.650848 | 18.85628 | 16.75338 | 15.51119 | 15.62313 | 15.67647 | 15.62313 | 1.130249 | 63.56417 | 1198.584 | 0 | 0 | 0 |
| full_heat/speed/100 | 3 | 83.425644 | 19.70324 | 17.05445 | 15.77346 | 15.88798 | 15.94264 | 15.88798 | 1.166464 | 64.65852 | 1273.982 | 0 | 0 | 0 |
| full_heat/speed/100 | 4 | 85.985973 | 20.52289 | 17.36268 | 16.03875 | 16.14944 | 16.19913 | 16.14944 | 1.213236 | 65.74999 | 1349.38 | 0 | 0 | 0 |
| full_heat/start/000 | 1 | 72.415524 | 16.87512 | 17.53364 | 16.25193 | 16.36186 | 16.41126 | 16.36186 | 1.17178 | 66.55869 | 1123.186 | 0 | 0 | 0 |
| full_heat/start/000 | 2 | 75.151902 | 17.70189 | 17.84156 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.198349 | 67.7094 | 1198.584 | 0 | 0 | 0 |
| full_heat/start/000 | 3 | 77.713206 | 18.49605 | 18.16135 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.234571 | 68.87862 | 1273.982 | 0 | 0 | 0 |
| full_heat/start/000 | 4 | 80.118608 | 19.26429 | 18.48889 | 17.08968 | 17.20703 | 17.26007 | 17.20703 | 1.281858 | 70.04567 | 1349.38 | 0 | 0 | 0 |
| full_heat/start/025 | 1 | 72.415524 | 16.89515 | 17.45472 | 16.25193 | 16.36186 | 16.41126 | 16.36186 | 1.092863 | 66.47977 | 1123.186 | 0 | 0 | 0 |
| full_heat/start/025 | 2 | 75.151902 | 17.72317 | 17.76024 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.117025 | 67.62807 | 1198.584 | 0 | 0 | 0 |
| full_heat/start/025 | 3 | 77.713206 | 18.51855 | 18.07767 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.150894 | 68.79494 | 1273.982 | 0 | 0 | 0 |
| full_heat/start/025 | 4 | 80.118608 | 19.28795 | 18.40298 | 17.08968 | 17.20703 | 17.26007 | 17.20703 | 1.195953 | 69.95976 | 1349.38 | 0 | 0 | 0 |
| full_heat/start/050 | 1 | 72.415524 | 16.91381 | 17.38141 | 16.25193 | 16.36186 | 16.41125 | 16.36186 | 1.019554 | 66.40646 | 1123.186 | 0 | 0 | 0 |
| full_heat/start/050 | 2 | 75.151902 | 17.74297 | 17.68479 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.041573 | 67.55262 | 1198.584 | 0 | 0 | 0 |
| full_heat/start/050 | 3 | 77.713206 | 18.53945 | 18.0001 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.073324 | 68.71738 | 1273.982 | 0 | 0 | 0 |
| full_heat/start/050 | 4 | 80.118608 | 19.30991 | 18.32341 | 17.08968 | 17.20704 | 17.26007 | 17.20704 | 1.116371 | 69.88019 | 1349.38 | 0 | 0 | 0 |
| full_heat/start/075 | 1 | 72.415524 | 16.93128 | 17.31289 | 16.25193 | 16.36186 | 16.41125 | 16.36186 | 0.951029 | 66.33793 | 1123.186 | 0 | 0 | 0 |
| full_heat/start/075 | 2 | 75.151902 | 17.76148 | 17.61437 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 0.971159 | 67.48221 | 1198.584 | 0 | 0 | 0 |
| full_heat/start/075 | 3 | 77.713206 | 18.55898 | 17.9278 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.001015 | 68.64507 | 1273.982 | 0 | 0 | 0 |
| full_heat/start/075 | 4 | 80.118608 | 19.33044 | 18.24921 | 17.08968 | 17.20703 | 17.26007 | 17.20703 | 1.042175 | 69.80598 | 1349.38 | 0 | 0 | 0 |
| full_heat/start/100 | 1 | 72.415524 | 16.94771 | 17.24855 | 16.25193 | 16.36186 | 16.41126 | 16.36186 | 0.886692 | 66.2736 | 1123.186 | 0 | 0 | 0 |
| full_heat/start/100 | 2 | 75.151902 | 17.77888 | 17.54833 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 0.905113 | 67.41616 | 1198.584 | 0 | 0 | 0 |
| full_heat/start/100 | 3 | 77.713206 | 18.5773 | 17.86007 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 0.933294 | 68.57735 | 1273.982 | 0 | 0 | 0 |
| full_heat/start/100 | 4 | 80.378977 | 19.3497 | 18.17971 | 17.08968 | 17.20704 | 17.26007 | 17.20704 | 0.972677 | 69.7365 | 1349.38 | 0 | 0 | 0 |
| full_heat/surface/baseline | 1 | 72.415524 | 16.91381 | 17.38141 | 16.25193 | 16.36186 | 16.41125 | 16.36186 | 1.019554 | 66.40646 | 1123.186 | 0 | 0 | 0 |
| full_heat/surface/baseline | 2 | 75.151902 | 17.74297 | 17.68479 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.041573 | 67.55262 | 1198.584 | 0 | 0 | 0 |
| full_heat/surface/baseline | 3 | 77.713206 | 18.53945 | 18.0001 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.073324 | 68.71738 | 1273.982 | 0 | 0 | 0 |
| full_heat/surface/baseline | 4 | 80.118608 | 19.30991 | 18.32341 | 17.08968 | 17.20704 | 17.26007 | 17.20704 | 1.116371 | 69.88019 | 1349.38 | 0 | 0 | 0 |
| full_heat/surface/grip_080 | 1 | 70.272819 | 16.29562 | 18.01703 | 16.87475 | 16.99209 | 17.04174 | 16.99209 | 1.024942 | 68.92561 | 1123.186 | 0 | 0 | 0 |
| full_heat/surface/grip_080 | 2 | 72.868545 | 17.09405 | 18.33283 | 17.1588 | 17.28433 | 17.34106 | 17.28433 | 1.048498 | 70.11702 | 1198.584 | 0 | 0 | 0 |
| full_heat/surface/grip_080 | 3 | 75.354531 | 17.85864 | 18.66213 | 17.45328 | 17.58174 | 17.63987 | 17.58174 | 1.080395 | 71.33702 | 1273.982 | 0 | 0 | 0 |
| full_heat/surface/grip_080 | 4 | 77.689552 | 18.5979 | 18.99906 | 17.75118 | 17.87605 | 17.92921 | 17.87605 | 1.123003 | 72.5555 | 1349.38 | 0 | 0 | 0 |
| full_heat/surface/moisture_070 | 1 | 72.369051 | 16.82928 | 17.84642 | 16.39446 | 16.22282 | 16.27629 | 16.27629 | 1.570127 | 66.74 | 1123.186 | 0 | 0 | 0 |
| full_heat/surface/moisture_070 | 2 | 75.06419 | 17.65397 | 18.15924 | 16.67056 | 16.50145 | 16.56191 | 16.56191 | 1.597332 | 67.89316 | 1198.584 | 0 | 0 | 0 |
| full_heat/surface/moisture_070 | 3 | 77.619191 | 18.44581 | 18.48508 | 16.95494 | 16.78232 | 16.84386 | 16.84386 | 1.641228 | 69.0662 | 1273.982 | 0 | 0 | 0 |
| full_heat/surface/moisture_070 | 4 | 80.058966 | 19.21162 | 18.8185 | 17.2426 | 17.06027 | 17.11632 | 17.11632 | 1.702183 | 70.2377 | 1349.38 | 0 | 0 | 0 |
| full_heat/surface/ruts_025 | 1 | 71.270254 | 16.56688 | 17.73054 | 16.59502 | 16.71032 | 16.7612 | 16.71032 | 1.020222 | 67.79708 | 1123.186 | 0 | 0 | 0 |
| full_heat/surface/ruts_025 | 2 | 73.850661 | 17.3781 | 18.04095 | 16.87452 | 16.99855 | 17.05694 | 16.99855 | 1.042397 | 68.97096 | 1198.584 | 0 | 0 | 0 |
| full_heat/surface/ruts_025 | 3 | 76.335837 | 18.15665 | 18.36401 | 17.16288 | 17.28971 | 17.34955 | 17.28971 | 1.074299 | 70.16614 | 1273.982 | 0 | 0 | 0 |
| full_heat/surface/ruts_025 | 4 | 78.707497 | 18.90994 | 18.69479 | 17.45438 | 17.57727 | 17.63179 | 17.57727 | 1.117519 | 71.35823 | 1349.38 | 0 | 0 | 0 |
| full_heat/within_heat/slide_control | 1 | 71.417251 | 16.6718 | 17.61021 | 16.49528 | 16.60735 | 16.65755 | 16.60735 | 1.002853 | 67.37039 | 1123.186 | 0 | 0 | 0 |
| full_heat/within_heat/slide_control | 2 | 75.151902 | 17.74297 | 17.68479 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.041573 | 67.55262 | 1198.584 | 0 | 0 | 0 |
| full_heat/within_heat/slide_control | 3 | 77.713206 | 18.53945 | 18.0001 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.073324 | 68.71738 | 1273.982 | 0 | 0 | 0 |
| full_heat/within_heat/slide_control | 4 | 81.171002 | 19.56836 | 18.10336 | 16.85682 | 16.97235 | 17.0247 | 16.97235 | 1.131016 | 68.95723 | 1349.38 | 0 | 0 | 0 |
| full_heat/within_heat/speed | 1 | 70.528677 | 16.35422 | 17.90763 | 16.83056 | 16.94451 | 16.99594 | 16.94451 | 0.963123 | 68.67865 | 1123.186 | 0 | 0 | 0 |
| full_heat/within_heat/speed | 2 | 75.151902 | 17.74297 | 17.68479 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.041573 | 67.55262 | 1198.584 | 0 | 0 | 0 |
| full_heat/within_heat/speed | 3 | 77.713206 | 18.53945 | 18.0001 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.073324 | 68.71738 | 1273.982 | 0 | 0 | 0 |
| full_heat/within_heat/speed | 4 | 83.138022 | 19.92462 | 17.82294 | 16.54076 | 16.65466 | 16.70592 | 16.65466 | 1.168282 | 67.72427 | 1349.38 | 0 | 0 | 0 |
| full_heat/within_heat/start | 1 | 72.415524 | 16.89515 | 17.45472 | 16.25193 | 16.36186 | 16.41126 | 16.36186 | 1.092863 | 66.47977 | 1123.186 | 0 | 0 | 0 |
| full_heat/within_heat/start | 2 | 75.151902 | 17.74297 | 17.68479 | 16.52453 | 16.64322 | 16.70008 | 16.64322 | 1.041573 | 67.55262 | 1198.584 | 0 | 0 | 0 |
| full_heat/within_heat/start | 3 | 77.713206 | 18.53945 | 18.0001 | 16.80542 | 16.92678 | 16.98507 | 16.92678 | 1.073324 | 68.71738 | 1273.982 | 0 | 0 | 0 |
| full_heat/within_heat/start | 4 | 80.118608 | 19.33044 | 18.24921 | 17.08968 | 17.20703 | 17.26007 | 17.20703 | 1.042175 | 69.80598 | 1349.38 | 0 | 0 | 0 |

| Scenario | Rider | Initial lane | Actual skills | Corrections | Residual count | Max residual | Observed min lateral | Observed max lateral |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| full_heat/baseline | 1 | 0 | 50/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/baseline | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/baseline | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/baseline | 4 | 3 | 50/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/gearing/000 | 1 | 0 | 50/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/gearing/000 | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/gearing/000 | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/gearing/000 | 4 | 3 | 50/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/gearing/050 | 1 | 0 | 50/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/gearing/050 | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/gearing/050 | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/gearing/050 | 4 | 3 | 50/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/gearing/100 | 1 | 0 | 50/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/gearing/100 | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/gearing/100 | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/gearing/100 | 4 | 3 | 50/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/slide_control/000 | 1 | 0 | 50/50/0/50/50/50 | 18 | 0 | 0 | 0 | 0 |
| full_heat/slide_control/000 | 2 | 1 | 50/50/0/50/50/50 | 17 | 0 | 0 | 1 | 1 |
| full_heat/slide_control/000 | 3 | 2 | 50/50/0/50/50/50 | 18 | 0 | 0 | 2 | 2 |
| full_heat/slide_control/000 | 4 | 3 | 50/50/0/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/slide_control/025 | 1 | 0 | 50/50/25/50/50/50 | 18 | 0 | 0 | 0 | 0 |
| full_heat/slide_control/025 | 2 | 1 | 50/50/25/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/slide_control/025 | 3 | 2 | 50/50/25/50/50/50 | 18 | 0 | 0 | 2 | 2 |
| full_heat/slide_control/025 | 4 | 3 | 50/50/25/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/slide_control/050 | 1 | 0 | 50/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/slide_control/050 | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/slide_control/050 | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/slide_control/050 | 4 | 3 | 50/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/slide_control/075 | 1 | 0 | 50/50/75/50/50/50 | 18 | 0 | 0 | 0 | 0 |
| full_heat/slide_control/075 | 2 | 1 | 50/50/75/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/slide_control/075 | 3 | 2 | 50/50/75/50/50/50 | 18 | 0 | 0 | 2 | 2 |
| full_heat/slide_control/075 | 4 | 3 | 50/50/75/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/slide_control/100 | 1 | 0 | 50/50/100/50/50/50 | 18 | 0 | 0 | 0 | 0 |
| full_heat/slide_control/100 | 2 | 1 | 50/50/100/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/slide_control/100 | 3 | 2 | 50/50/100/50/50/50 | 18 | 0 | 0 | 2 | 2 |
| full_heat/slide_control/100 | 4 | 3 | 50/50/100/50/50/50 | 16 | 0 | 0 | 3 | 3 |
| full_heat/speed/000 | 1 | 0 | 50/0/50/50/50/50 | 18 | 0 | 0 | 0 | 0 |
| full_heat/speed/000 | 2 | 1 | 50/0/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/speed/000 | 3 | 2 | 50/0/50/50/50/50 | 18 | 0 | 0 | 2 | 2 |
| full_heat/speed/000 | 4 | 3 | 50/0/50/50/50/50 | 16 | 0 | 0 | 3 | 3 |
| full_heat/speed/025 | 1 | 0 | 50/25/50/50/50/50 | 18 | 0 | 0 | 0 | 0 |
| full_heat/speed/025 | 2 | 1 | 50/25/50/50/50/50 | 18 | 0 | 0 | 1 | 1 |
| full_heat/speed/025 | 3 | 2 | 50/25/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/speed/025 | 4 | 3 | 50/25/50/50/50/50 | 16 | 0 | 0 | 3 | 3 |
| full_heat/speed/050 | 1 | 0 | 50/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/speed/050 | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/speed/050 | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/speed/050 | 4 | 3 | 50/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/speed/075 | 1 | 0 | 50/75/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/speed/075 | 2 | 1 | 50/75/50/50/50/50 | 18 | 0 | 0 | 1 | 1 |
| full_heat/speed/075 | 3 | 2 | 50/75/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/speed/075 | 4 | 3 | 50/75/50/50/50/50 | 16 | 0 | 0 | 3 | 3 |
| full_heat/speed/100 | 1 | 0 | 50/100/50/50/50/50 | 18 | 0 | 0 | 0 | 0 |
| full_heat/speed/100 | 2 | 1 | 50/100/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/speed/100 | 3 | 2 | 50/100/50/50/50/50 | 18 | 0 | 0 | 2 | 2 |
| full_heat/speed/100 | 4 | 3 | 50/100/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/start/000 | 1 | 0 | 0/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/start/000 | 2 | 1 | 0/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/start/000 | 3 | 2 | 0/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/start/000 | 4 | 3 | 0/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/start/025 | 1 | 0 | 25/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/start/025 | 2 | 1 | 25/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/start/025 | 3 | 2 | 25/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/start/025 | 4 | 3 | 25/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/start/050 | 1 | 0 | 50/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/start/050 | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/start/050 | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/start/050 | 4 | 3 | 50/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/start/075 | 1 | 0 | 75/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/start/075 | 2 | 1 | 75/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/start/075 | 3 | 2 | 75/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/start/075 | 4 | 3 | 75/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/start/100 | 1 | 0 | 100/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/start/100 | 2 | 1 | 100/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/start/100 | 3 | 2 | 100/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/start/100 | 4 | 3 | 100/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/surface/baseline | 1 | 0 | 50/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/surface/baseline | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/surface/baseline | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/surface/baseline | 4 | 3 | 50/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/surface/grip_080 | 1 | 0 | 50/50/50/50/50/50 | 18 | 0 | 0 | 0 | 0 |
| full_heat/surface/grip_080 | 2 | 1 | 50/50/50/50/50/50 | 18 | 0 | 0 | 1 | 1 |
| full_heat/surface/grip_080 | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/surface/grip_080 | 4 | 3 | 50/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/surface/moisture_070 | 1 | 0 | 50/50/50/50/50/50 | 8 | 0 | 0 | 0 | 0 |
| full_heat/surface/moisture_070 | 2 | 1 | 50/50/50/50/50/50 | 8 | 0 | 0 | 1 | 1 |
| full_heat/surface/moisture_070 | 3 | 2 | 50/50/50/50/50/50 | 8 | 0 | 0 | 2 | 2 |
| full_heat/surface/moisture_070 | 4 | 3 | 50/50/50/50/50/50 | 8 | 0 | 0 | 3 | 3 |
| full_heat/surface/ruts_025 | 1 | 0 | 50/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/surface/ruts_025 | 2 | 1 | 50/50/50/50/50/50 | 18 | 0 | 0 | 1 | 1 |
| full_heat/surface/ruts_025 | 3 | 2 | 50/50/50/50/50/50 | 18 | 0 | 0 | 2 | 2 |
| full_heat/surface/ruts_025 | 4 | 3 | 50/50/50/50/50/50 | 16 | 0 | 0 | 3 | 3 |
| full_heat/within_heat/slide_control | 1 | 0 | 50/50/25/50/50/50 | 18 | 0 | 0 | 0 | 0 |
| full_heat/within_heat/slide_control | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/within_heat/slide_control | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/within_heat/slide_control | 4 | 3 | 50/50/75/50/50/50 | 18 | 0 | 0 | 3 | 3 |
| full_heat/within_heat/speed | 1 | 0 | 50/25/50/50/50/50 | 18 | 0 | 0 | 0 | 0 |
| full_heat/within_heat/speed | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/within_heat/speed | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/within_heat/speed | 4 | 3 | 50/75/50/50/50/50 | 16 | 0 | 0 | 3 | 3 |
| full_heat/within_heat/start | 1 | 0 | 25/50/50/50/50/50 | 16 | 0 | 0 | 0 | 0 |
| full_heat/within_heat/start | 2 | 1 | 50/50/50/50/50/50 | 16 | 0 | 0 | 1 | 1 |
| full_heat/within_heat/start | 3 | 2 | 50/50/50/50/50/50 | 16 | 0 | 0 | 2 | 2 |
| full_heat/within_heat/start | 4 | 3 | 75/50/50/50/50/50 | 18 | 0 | 0 | 3 | 3 |


## Within-Heat

Identical baseline measures fixed-line geometry plus normal production evolution. Synthetic [25,50,50,75] skill patterns on lanes [0,1,2,3] measure the combined line/skill result; differences from the matched baseline are not an unconfounded skill-only causal estimate. All spreads are max − min within one controlled four-rider heat, not a population distribution.

| Scenario | Heat-time spread s | Vmax spread km/h | L1 spread s | Average-speed spread m/s |
| --- | --- | --- | --- | --- |
| full_heat/baseline | 3.473732 | 7.703085 | 0.941994 | 2.396105 |
| full_heat/gearing/000 | 3.558098 | 7.417804 | 0.953371 | 2.380331 |
| full_heat/gearing/050 | 3.473732 | 7.703085 | 0.941994 | 2.396105 |
| full_heat/gearing/100 | 3.389992 | 7.819718 | 0.933014 | 2.410223 |
| full_heat/slide_control/000 | 3.482201 | 7.654731 | 0.944769 | 2.351068 |
| full_heat/slide_control/025 | 3.483994 | 7.682162 | 0.944584 | 2.372612 |
| full_heat/slide_control/050 | 3.473732 | 7.703085 | 0.941994 | 2.396105 |
| full_heat/slide_control/075 | 3.454552 | 7.781671 | 0.937782 | 2.42119 |
| full_heat/slide_control/100 | 3.42907 | 8.005106 | 0.93251 | 2.447483 |
| full_heat/speed/000 | 3.668198 | 7.760323 | 0.983284 | 2.24774 |
| full_heat/speed/025 | 3.576248 | 7.728284 | 0.963696 | 2.321054 |
| full_heat/speed/050 | 3.473732 | 7.703085 | 0.941994 | 2.396105 |
| full_heat/speed/075 | 3.367874 | 8.007777 | 0.920204 | 2.472027 |
| full_heat/speed/100 | 3.264729 | 8.272527 | 0.899292 | 2.547684 |
| full_heat/start/000 | 3.486984 | 7.703085 | 0.955252 | 2.38917 |
| full_heat/start/025 | 3.479996 | 7.703085 | 0.948263 | 2.392794 |
| full_heat/start/050 | 3.473732 | 7.703085 | 0.941994 | 2.396105 |
| full_heat/start/075 | 3.468056 | 7.703085 | 0.936319 | 2.39916 |
| full_heat/start/100 | 3.462898 | 7.963454 | 0.931162 | 2.401985 |
| full_heat/surface/baseline | 3.473732 | 7.703085 | 0.941994 | 2.396105 |
| full_heat/surface/grip_080 | 3.629898 | 7.416733 | 0.982025 | 2.302279 |
| full_heat/surface/moisture_070 | 3.497704 | 7.689915 | 0.972082 | 2.382347 |
| full_heat/surface/ruts_025 | 3.56115 | 7.437243 | 0.964245 | 2.343069 |
| full_heat/within_heat/slide_control | 1.586838 | 9.753751 | 0.493156 | 2.896563 |
| full_heat/within_heat/speed | 1.164757 | 12.609345 | 0.315317 | 3.570395 |
| full_heat/within_heat/start | 3.326218 | 7.703085 | 0.794485 | 2.435284 |


## Real-world context

Read-only versioned PGEE dataset; classifications come from the existing evaluator. Comparable distributions are envelopes, never caps or equality targets. Absolute heat/lap times and total distance remain ContextOnlyUntilTrackGeometry. Unsupported individual reaction/SpeedAt2s/first-curve targets are not inferred. Four deterministic riders and one spread observation do not estimate real population quantiles.

| Metric | Unit | Comparability | Real P10 | Real P50 | Real P90 | Baseline P50 | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| pge_clean_average_speed | m/s | ComparableEnvelope | 22.023974 | 22.980452 | 23.968246 | 18.14121 | Comparable as a distribution/envelope; this is not an automatic tuning score. |
| pge_clean_flying_lap_median | s | ContextOnlyUntilTrackGeometry | 14.71 | 15.66 | 16.52 | 16.784998 | Reported as context only: absolute values depend on matching concrete track geometry. |
| pge_clean_heat_time | s | ContextOnlyUntilTrackGeometry | 60.889 | 64.688 | 68.345 | 68.134998 | Reported as context only: absolute values depend on matching concrete track geometry. |
| pge_clean_l1_penalty | s | ComparableEnvelope | 1.82 | 2.05 | 2.32 | 1.057448 | Comparable as a distribution/envelope; this is not an automatic tuning score. |
| pge_clean_l1_time | s | ContextOnlyUntilTrackGeometry | 16.7 | 17.68 | 18.74 | 17.842446 | Reported as context only: absolute values depend on matching concrete track geometry. |
| pge_clean_l2_time | s | ContextOnlyUntilTrackGeometry | 14.68 | 15.63 | 16.49 | 16.664975 | Reported as context only: absolute values depend on matching concrete track geometry. |
| pge_clean_l3_time | s | ContextOnlyUntilTrackGeometry | 14.7 | 15.64 | 16.5 | 16.784998 | Reported as context only: absolute values depend on matching concrete track geometry. |
| pge_clean_l4_time | s | ContextOnlyUntilTrackGeometry | 14.76 | 15.74 | 16.65 | 16.842579 | Reported as context only: absolute values depend on matching concrete track geometry. |
| pge_clean_total_distance | m | ContextOnlyUntilTrackGeometry | 1382 | 1490 | 1599 | 1236.283203 | Reported as context only: absolute values depend on matching concrete track geometry. |
| pge_clean_vmax | km/h | ComparableEnvelope | 109.7 | 114.8 | 119.4 | 76.432554 | Comparable as a distribution/envelope; this is not an automatic tuning score. |
| pge_four_rider_average_speed_spread | m/s | ComparableEnvelope | 0.431415 | 0.927094 | 1.658977 | 2.396105 | Comparable as a distribution/envelope; this is not an automatic tuning score. |
| pge_four_rider_heat_time_spread | s | ComparableEnvelope | 0.9542 | 1.5285 | 2.2734 | 3.473732 | Comparable as a distribution/envelope; this is not an automatic tuning score. |
| pge_four_rider_l1_spread | s | ComparableEnvelope | 0.32 | 0.53 | 0.76 | 0.941994 | Comparable as a distribution/envelope; this is not an automatic tuning score. |
| pge_four_rider_vmax_spread | km/h | ComparableEnvelope | 1.8 | 4.5 | 8.7 | 7.703085 | Comparable as a distribution/envelope; this is not an automatic tuning score. |
| pge_individual_first_curve_speed | km/h | UnsupportedNumericByCurrentSource | — | — | — | — | The current source has no supported individual numeric target; source ranking flags are never parsed as speeds. |
| pge_individual_reaction_time | s | UnsupportedNumericByCurrentSource | — | — | — | — | The current source has no supported individual numeric target; source ranking flags are never parsed as speeds. |
| pge_individual_speed_at_2s | km/h | UnsupportedNumericByCurrentSource | — | — | — | — | The current source has no supported individual numeric target; source ranking flags are never parsed as speeds. |


## Interpretation

Prepared baseline launch: reaction 0.24 s, peak 20.58915 m/s, exit 19.0135 m/s, preparation 12 m. Free straight at 16 m/s over 30 m exits at 17.99413 m/s versus diagnostic equilibrium 30.02662 m/s. These isolate launch/preparation and finite-distance straight response, not a fitted performance target.

The controlled heat has 66 correction profiles and 0 residual-overspeed observations. Compare TurnEntry scrub/correction losses with TurnExit's actual remaining drive distance in the tables; no distance is granted twice. Higher capabilities do not imply monotonic total race time in every geometry/contact fixture.

Baseline pge_clean_vmax P50 = 76.432554 km/h, below the source P10–P90 envelope [109.7, 119.4]. This observation is not an instruction to change a constant.

Baseline pge_clean_average_speed P50 = 18.14121 m/s, below the source P10–P90 envelope [22.023974, 23.968246]. This observation is not an instruction to change a constant.

Baseline pge_clean_l1_penalty P50 = 1.057448 s, below the source P10–P90 envelope [1.82, 2.32]. This observation is not an instruction to change a constant.

TrackReading, Adaptability and PairRiding: No production effect in this scenario (isolated start/straight/corner probes, verified by endpoint comparisons). This does not claim they are unused by decisions, contact or the entire game. SlideControl thresholds/retention/correction and Speed's corner capability are observed directly. Gearing affects response and equilibrium; it is not a universal upgrade. TractionBias is probed only in corners where production uses it. Geometry line differences and synthetic skill spreads are measurements, not errors to equalize.

No tuning recommendations, optimizer, loss/OverallAccuracy score, new RNG, rider mapping, caps, gate bonuses, surface model or surrogate physics. Production physics files are unchanged.
