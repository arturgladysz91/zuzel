# Study #151 — PARTIAL

[Complete report and tables](REPORT.md) · [Reproduction and explicit CLI flags](../../../tools/contingency-recourse/README.md) · [Recorded index](index.json)

Finite conditional recourse passes the original0.4s state, the complete first
bend and both bends of the first lap. That native lap finishes in19.538802s.
The last full A attempt abstains at34.6s on the cumulative study CPU budget.
No new three-lap A/B finish or full-race time improvement is claimed. Full B and
the remaining held-out probes were withheld. Original physics/history is unchanged;
the new controller defaults OFF.

The replay defaults to that failed full A attempt. Earlier native failures and
successful partial gates remain selectable. `trees/` contains complete selected
and rejected finite trees; `native/*-decisions.jsonl` contains the hash-chained,
flushed command journals. Forecasts are independent model certificates, not global
native Box2D guarantees. Actual native execution retains every original guard.

Final experiment/export ledger:1191.734375 CPU-s,6 new native worlds.
See `validation.json` and the CI receipt for software validation; those do not
change the sporting verdict.
