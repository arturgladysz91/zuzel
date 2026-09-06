# PGEE real-telemetry calibration snapshot v1

This directory is the compact, versioned, offline input to the CoreSim calibration evaluator. It was prepared from the external PGE Ekstraliga public Firestore telemetry source package. The full raw feed, credentials, and the reaction-time paper PDF are not committed.

## Files

- `pge_rider_heats.csv`: all normalized PGEE rows, including incomplete and restarted attempts retained for audit.
- `pge_matches.csv`: normalized metadata for the 95 PGEE matches.
- `pge_split.csv`: immutable match-level DEVELOPMENT/FINAL_TEST assignment and DEVELOPMENT fold.
- `summary.json`: rules, counts, quantiles, within-heat spreads, residual fingerprints, persistence, and correlations.
- `source_manifest.json`: source filenames, SHA256 hashes, row/document counts, fixed generation time, and provenance.
- `literature_targets.json`: task-supplied reaction-time population observations and context; no values inferred from a local PDF.

The rider-heat CSV contains source identifiers and metadata, presence flags, four lap times, heat time, Vmax, distance, derived flying-lap median/L1 penalty/average speed, analysis classifications, split/fold, and optional source gate-ranking metadata. Blank numeric values stay blank. `gate_rank_speed_2s` and `gate_rank_curve_speed` are categories/rankings, never physical speeds.

## Rules

Only exact `league == "PGEE"` rows are selected. CompleteTelemetry requires both source-presence flags; finite positive heat, L1-L4, Vmax, and distance values; points in `{0,1,2,3}`; and an absolute heat-time versus lap-sum difference no greater than 0.05 seconds. A zero-point finish is valid.

CleanPhysics is the CompleteTelemetry subset in which every L2-L4 value is within ±10% of `median(L2,L3,L4)`. A complete row outside that conservative steady-lap heuristic is Eventful/non-steady; that label does not assert a crash, contact, or invalid record. A non-complete row is AuditOnly and remains in the snapshot.

An actual attempt is identified by `match_id + heat_uid`, not `match_id + heat_no`, so restarts stay distinct. Derived values are:

```text
flying_lap_median_s = median(L2, L3, L4)
l1_penalty_s = L1 - flying_lap_median_s
average_speed_mps = total_distance_m / heat_time_s
```

Quantiles use linear interpolation between adjacent sorted values at the zero-based position `(n - 1) * p`. The final holdout is the newest `ceil(20%)` of the 93 matches with CompleteTelemetry, ordered by date then match ID. DEVELOPMENT folds use the big-endian integer value of `SHA256(UTF-8 match_id) modulo 5`. Every row of a match has one split and one fold.

The literal rules produce 5,410 CompleteTelemetry, 5,328 CleanPhysics, 82 Eventful, and 963 AuditOnly rows. The task's approximate 5,417/89 oracle is not forced: six otherwise steady records have source points `W`, outside the required numeric set, while the other one-row difference exists before applying the points condition.

## Reproduction

Python 3.10+ and the standard library are sufficient. From the repository root:

```text
python tools/calibration/prepare_pge_dataset.py \
  --telemetry-full <source>/output/telemetry_full.csv \
  --matches <source>/output/matches.csv \
  --heats <source>/output/heats.csv \
  --run-summary <source>/output/run_summary.json \
  --source-readme <source>/README.md \
  --downloader <source>/scripts/download_telemetry.py \
  --sample-match-raw <source>/output/sample_match_raw.json \
  --raw-match <source>/output/raw/7734.json \
  --output data/calibration/pge/v1 \
  --generated-at-utc 2026-09-05T21:03:47.868178Z
```

The command is offline and deterministic when the timestamp and inputs are fixed. The current snapshot lacks structured track IDs/geometry; it retains `source_track_label` and reports zero structured-metadata coverage without inventing aliases.
