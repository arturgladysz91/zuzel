# #146 — Stage A: saved failure diagnosis (before new planner)

The #145 planner retains no previous plan. Each review ranks a newly generated pair by the original terminal score. The forecast second command is neither kept nor compared with the next actual decision.

|Observed s|First: steer° / gas|Forecast second|Actual next|Followed?|Predicted safety / minimum gap m|Native inner gap m|
|---:|---|---|---|---|---:|
|3.60|-5.0° / 0.318|-5.0° / 0.318|0.5° / 0.000|False|0 / 0.6565|3.0406|
|3.80|0.5° / 0.000|1.5° / 0.000|-1.5° / 0.318|False|0 / 0.3849|2.2226|
|4.00|-1.5° / 0.318|0.5° / 0.000|1.5° / 0.000|False|0 / 0.4125|1.4440|
|4.20|1.5° / 0.000|0.5° / 0.000|0.0° / 0.000|False|0 / 0.4076|0.9169|
|4.40|0.0° / 0.000|0.5° / 0.000|1.5° / 0.000|False|0 / 0.4080|0.5963|
|4.60|1.5° / 0.000|0.5° / 0.000|-1.5° / 0.000|False|0 / 0.3993|0.4440|
|4.80|-1.5° / 0.000|-1.5° / 0.318|-5.0° / 1.000|False|0 / 0.3194|0.4084|
|5.00|-5.0° / 1.000|-5.0° / 0.318|-5.0° / 1.000|False|0 / 0.3238|0.4344|
|5.20|-5.0° / 1.000|-15.0° / 0.000|-15.0° / 0.318|False|0 / 0.3779|0.4633|
|5.40|-15.0° / 0.318|-15.0° / 0.000|-15.0° / 0.318|False|0 / 0.2686|0.4634|
|5.60|-15.0° / 0.318|-15.0° / 0.000|-15.0° / 0.000|True|1 / 0.0739|0.4335|
|5.80|-15.0° / 0.000|-15.0° / 0.000|-15.0° / 0.000|True|2 / -0.1929|0.3867|
|6.00|-15.0° / 0.000|-15.0° / 0.000|-15.0° / 0.000|True|2 / -0.5497|0.3696|
|6.20|-15.0° / 0.000|-15.0° / 0.000|-15.0° / 0.000|True|2 / -0.9139|0.3077|
|6.40|-15.0° / 0.000|-15.0° / 0.000|N/D|None|2 / -0.9681|0.1728|

At 5.0 s the forecast requires partial throttle at observation 5.2 s (activation 5.44 s); the next review requests full throttle instead. At 5.2 s the new pair requires −15° / gas 0 at observation 5.4 s (activation 5.64 s); the actual review requests −15° / partial throttle. At 5.4 s the same future coast is again postponed. These deviations occur before the forecast loses its Safe class at 5.6 s.

At 5.6 s the selected pair is already Uncertain (future gap −0.1807 m). At 5.8 s it explicitly predicts a footprint crossing, yet the collector accepts the least-bad Unsafe candidate. The actual inner crossing occurs at 6.57 s. No execution veto exists in #145. H09 ends before the complete corner/next straight, so nominally Safe earlier pairs do not establish a recoverable complete maneuver.

Established from saved data: repeated abandonment of forecast second actions, no plan memory, and execution after all ranked alternatives are unsafe. Not yet isolated causally: whether executing an earlier planned second action alone avoids the later crossing; whether sequence prediction itself matches native integration. Stage A native sequence checks must resolve those questions before implementing a new planner.

## Native diagnosis gate (completed before planner implementation)

Three new native worlds, 6.515625 process CPU-s. Replaying the old decision tape reproduces the complete 6.57 s failure and fingerprint exactly. Both forced-pair worlds preserve every native prefix sample from the original standing start. Composing the unchanged predictor at command reviews matches 91 native samples over 0.9 s: maximum position errors 0.0000741 / 0.0000871 m, speed errors 0.000132 / 0.000212 m/s, yaw errors 0.0000645 / 0.000357 rad/s. Wheel-omega differences are below 0.00061 rad/s. This supports queue/actuator/crank composition over this interval, not an unlimited model guarantee.

Executing the 5.0 s pair (first then the assumed second command held until the 0.9 s horizon) still crosses the inner footprint boundary at 6.25 s. Executing the 5.2 s pair avoids that crossing but reaches the beta45 envelope at 6.92 s. After each 0.9 s diagnostic interval the old recorded actions resume; these are controlled diagnostic interventions, not autonomous recoveries or proof that no other recovery exists.

**Concrete diagnosis:** inconsistent replanning is real but is not a sufficient explanation by itself. The short model predicts the commanded sequence accurately and marks it safe only inside its limited horizon. It does not establish a legal complete corner or a safe terminal continuation. Repeated optimistic abandonment worsens this defect; the missing independent execution veto then accepts explicitly Unsafe choices after 5.8 s. No failure of the tested native queue or force model was observed. A replacement must preserve and reassess the suffix, rank complete reachable maneuvers, and refuse known-unsafe execution. Stage A diagnosis gate is satisfied; this is a negative result for merely enforcing the old pair.

Artifacts: stage-A/sequence-checks.json, the three full native prefixes, and diagnosis-saved.json. Native experiment accounting uses reservation process CPU, not historical controller counters embedded in replayed #145 decisions.
