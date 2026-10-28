# Study147 — measured baseline before optimization

Cold first decision, same native zero-start observation and empty pre-request queue: action and logical forecast count match Study146 (5120 forecasts; 106 full K1 seed decisions). Exclusive instrumented decision CPU 22.640625 s: SeedAction/Policy144 15.625 s (69.0131%), Model146.Step 6.953125 s (30.7108%), rollout bookkeeping 0.046875 s (0.2070%), dispatch 0.015625 s (0.0690%). Safety check recorded zero CPU ticks, meaning below the observed 15.625 ms process-counter quantum, not physically free.

The operation partition is separate: initial seed construction 70.4624%, candidate rollouts 29.3306%, independent preflight 0.0690%, dispatch 0.1380%. There is no inherited-plan revalidation in a cold first decision. These are measured instrumented percentages for this decision only; they are not estimates of the uninstrumented whole race.

Next: restore only the saved planner memory at 14.2 s and replay original accepted observations to the 22.6 s decision, checking actual action equality. The complete 22.6 s forecast log was missing in Study146, so this causal reconstruction must be demonstrated before attributing its cost. No future action tape is provided to the autonomous controller.
