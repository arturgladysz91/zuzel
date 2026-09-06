#!/usr/bin/env python3
"""Prepare the deterministic PGEE v1 calibration snapshot.

The source package is external to the repository.  This module intentionally
uses only the Python standard library and never performs network requests.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import statistics
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Sequence


SCHEMA_VERSION = "1.0"
DATASET_VERSION = "pge-v1"
QUANTILE_PROBABILITIES = (0.01, 0.10, 0.25, 0.50, 0.75, 0.90, 0.99)
NORMALIZED_COLUMNS = (
    "match_id",
    "date",
    "season",
    "league",
    "source_track_label",
    "track_id",
    "track_city",
    "track_name",
    "track_length_m",
    "track_best_time_s",
    "track_best_rider",
    "heat_no",
    "heat_uid",
    "rider_id",
    "gate",
    "points",
    "result_position",
    "motorbike",
    "telemetry_present",
    "result_present",
    "heat_time_s",
    "l1_time_s",
    "l2_time_s",
    "l3_time_s",
    "l4_time_s",
    "max_speed_kph",
    "total_distance_m",
    "flying_lap_median_s",
    "l1_penalty_s",
    "average_speed_mps",
    "complete_telemetry",
    "clean_physics",
    "eventful",
    "audit_only",
    "exclusion_reason",
    "dataset_split",
    "development_fold",
    "gate_rank_position",
    "gate_rank_speed_2s",
    "gate_rank_curve_speed",
)

METRICS = {
    "heat_time_s": {
        "metric_id": "pge_clean_heat_time",
        "unit": "s",
        "definition": "recorded four-lap rider heat time",
        "comparability": "ContextOnlyUntilTrackGeometry",
    },
    "l1_time_s": {
        "metric_id": "pge_clean_l1_time",
        "unit": "s",
        "definition": "recorded first-lap rider time",
        "comparability": "ContextOnlyUntilTrackGeometry",
    },
    "l2_time_s": {
        "metric_id": "pge_clean_l2_time",
        "unit": "s",
        "definition": "recorded second-lap rider time",
        "comparability": "ContextOnlyUntilTrackGeometry",
    },
    "l3_time_s": {
        "metric_id": "pge_clean_l3_time",
        "unit": "s",
        "definition": "recorded third-lap rider time",
        "comparability": "ContextOnlyUntilTrackGeometry",
    },
    "l4_time_s": {
        "metric_id": "pge_clean_l4_time",
        "unit": "s",
        "definition": "recorded fourth-lap rider time",
        "comparability": "ContextOnlyUntilTrackGeometry",
    },
    "flying_lap_median_s": {
        "metric_id": "pge_clean_flying_lap_median",
        "unit": "s",
        "definition": "median of recorded L2, L3 and L4 times",
        "comparability": "ContextOnlyUntilTrackGeometry",
    },
    "l1_penalty_s": {
        "metric_id": "pge_clean_l1_penalty",
        "unit": "s",
        "definition": "L1 minus median(L2, L3, L4)",
        "comparability": "ComparableEnvelope",
    },
    "max_speed_kph": {
        "metric_id": "pge_clean_vmax",
        "unit": "km/h",
        "definition": "maximum recorded rider speed in one telemetry rider-heat record",
        "comparability": "ComparableEnvelope",
    },
    "total_distance_m": {
        "metric_id": "pge_clean_total_distance",
        "unit": "m",
        "definition": "recorded total rider distance in one heat attempt",
        "comparability": "ContextOnlyUntilTrackGeometry",
    },
    "average_speed_mps": {
        "metric_id": "pge_clean_average_speed",
        "unit": "m/s",
        "definition": "recorded total distance divided by recorded heat time",
        "comparability": "ComparableEnvelope",
    },
}


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def write_csv(path: Path, columns: Sequence[str], rows: Iterable[dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=columns, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            writer.writerow({column: row.get(column, "") for column in columns})


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
        newline="\n",
    )


def parse_positive_number(value: str | None) -> float | None:
    if value is None or not value.strip():
        return None
    try:
        result = float(value)
    except ValueError:
        return None
    return result if math.isfinite(result) and result > 0 else None


def parse_int(value: str | None) -> int | None:
    if value is None or not value.strip():
        return None
    try:
        parsed = float(value)
    except ValueError:
        return None
    if not math.isfinite(parsed) or not parsed.is_integer():
        return None
    return int(parsed)


def as_flag(value: str | None) -> bool:
    return value == "1"


def format_number(value: float | None) -> str:
    return "" if value is None else format(value, ".12g")


def linear_quantile(sorted_values: Sequence[float], probability: float) -> float:
    """R-7 style interpolation at zero-based index (n - 1) * probability."""
    if not sorted_values:
        raise ValueError("At least one observation is required.")
    if probability < 0 or probability > 1:
        raise ValueError("Probability must be in [0, 1].")
    position = (len(sorted_values) - 1) * probability
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return float(sorted_values[lower])
    fraction = position - lower
    return float(sorted_values[lower] + (sorted_values[upper] - sorted_values[lower]) * fraction)


def quantile_map(values: Iterable[float]) -> dict[str, float]:
    ordered = sorted(values)
    return {
        f"p{round(probability * 100):02d}": linear_quantile(ordered, probability)
        for probability in QUANTILE_PROBABILITIES
    }


def average_ranks(values: Sequence[float]) -> list[float]:
    indexed = sorted(enumerate(values), key=lambda pair: (pair[1], pair[0]))
    ranks = [0.0] * len(values)
    start = 0
    while start < len(indexed):
        end = start + 1
        while end < len(indexed) and indexed[end][1] == indexed[start][1]:
            end += 1
        average = ((start + 1) + end) / 2.0
        for index, _ in indexed[start:end]:
            ranks[index] = average
        start = end
    return ranks


def pearson(first: Sequence[float], second: Sequence[float]) -> float | None:
    if len(first) != len(second) or len(first) < 2:
        return None
    first_mean = statistics.fmean(first)
    second_mean = statistics.fmean(second)
    numerator = sum((x - first_mean) * (y - second_mean) for x, y in zip(first, second))
    first_square = sum((x - first_mean) ** 2 for x in first)
    second_square = sum((y - second_mean) ** 2 for y in second)
    denominator = math.sqrt(first_square * second_square)
    return None if denominator == 0 else numerator / denominator


def spearman(first: Sequence[float], second: Sequence[float]) -> float | None:
    return pearson(average_ranks(first), average_ranks(second))


def match_sort_value(match_id: str) -> tuple[int, int | str]:
    return (0, int(match_id)) if match_id.isdigit() else (1, match_id)


def development_fold(match_id: str) -> int:
    digest = hashlib.sha256(match_id.encode("utf-8")).digest()
    return int.from_bytes(digest, "big") % 5


def filter_pgee_matches(rows: Iterable[dict[str, str]]) -> dict[str, dict[str, str]]:
    """The league filter is intentionally exact and case-sensitive."""
    return {row["match_id"]: row for row in rows if row.get("league") == "PGEE"}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def validate_timestamp(value: str) -> str:
    candidate = value.replace("Z", "+00:00")
    parsed = datetime.fromisoformat(candidate)
    if parsed.tzinfo is None or parsed.utcoffset() != timezone.utc.utcoffset(parsed):
        raise ValueError("generated-at-utc must be an ISO-8601 UTC timestamp")
    return parsed.isoformat().replace("+00:00", "Z")


def validate_raw_match_structure(document: Any) -> None:
    """Validate the source shape and that gate speed fields are rankings."""
    try:
        responses = document["responses"]
        match_fields = responses["match"]["fields"]
        telemetry_fields = responses["telemetry"]["fields"]
        track_fields = match_fields["match"]["mapValue"]["fields"]["track"]["mapValue"]["fields"]
        gate_data = telemetry_fields["gates"]["mapValue"]["fields"]["data"]["mapValue"]["fields"]
        for name in ("speed_2s", "curve_speed"):
            by_heat = gate_data[name]["mapValue"]["fields"]
            if not isinstance(by_heat, dict) or not by_heat:
                raise KeyError(name)
            for heat_number, ranked_entries in by_heat.items():
                int(heat_number)
                for entry in ranked_entries["arrayValue"].get("values", []):
                    fields = entry["mapValue"]["fields"]
                    int(fields["no"]["integerValue"])
                    gate = fields["gate"]["stringValue"]
                    if gate not in {"a", "b", "c", "d"}:
                        raise ValueError(f"unexpected gate ranking value {gate!r}")
        if not isinstance(track_fields, dict):
            raise KeyError("track")
    except (KeyError, TypeError, ValueError) as error:
        raise ValueError("Raw match JSON does not have the expected Firestore match/telemetry ranking structure") from error


def resolve_motorbike(source: dict[str, str]) -> str:
    direct = source.get("detail_motorbike", "")
    if direct:
        return direct
    payload = source.get("motorbikes_json", "")
    if not payload:
        return ""
    try:
        candidates = json.loads(payload)
    except json.JSONDecodeError:
        return ""
    heat_uid = source.get("heat_uid", "")
    return next(
        (str(item.get("motorbike", "")) for item in candidates if str(item.get("heat_uid", "")) == heat_uid),
        "",
    )


def normalize_row(source: dict[str, str], match: dict[str, str]) -> dict[str, Any]:
    telemetry_present = as_flag(source.get("telemetry_present"))
    result_present = as_flag(source.get("result_present"))
    heat_time = parse_positive_number(source.get("detail_time"))
    laps = [parse_positive_number(source.get(f"detail_l{number}_time")) for number in range(1, 5)]
    max_speed = parse_positive_number(source.get("detail_max_speed"))
    total_distance = parse_positive_number(source.get("detail_distance"))
    points = parse_int(source.get("points"))

    reasons: list[str] = []
    if not telemetry_present:
        reasons.append("telemetry_missing")
    if not result_present:
        reasons.append("result_missing")
    if heat_time is None:
        reasons.append("heat_time_missing_or_invalid")
    if any(value is None for value in laps):
        reasons.append("lap_time_missing_or_invalid")
    if max_speed is None:
        reasons.append("max_speed_missing_or_invalid")
    if total_distance is None:
        reasons.append("distance_missing_or_invalid")
    if points not in {0, 1, 2, 3}:
        reasons.append("points_missing_or_invalid")
    if heat_time is not None and all(value is not None for value in laps):
        if abs(heat_time - sum(value for value in laps if value is not None)) > 0.05 + 1e-12:
            reasons.append("heat_lap_time_mismatch")

    complete = not reasons
    flying_median = statistics.median(laps[1:]) if complete else None
    clean = bool(
        complete
        and flying_median is not None
        and all(abs(value - flying_median) / flying_median <= 0.10 + 1e-12 for value in laps[1:])
    )
    eventful = complete and not clean
    if eventful:
        reasons.append("non_steady_flying_laps")
    average_speed = total_distance / heat_time if complete and heat_time and total_distance else None
    l1_penalty = laps[0] - flying_median if complete and flying_median is not None else None

    return {
        "match_id": source.get("match_id", ""),
        "date": match.get("date", ""),
        "season": match.get("season", ""),
        "league": match.get("league", ""),
        "source_track_label": match.get("track", ""),
        "track_id": match.get("track_id", ""),
        "track_city": match.get("track_city", ""),
        "track_name": match.get("track_name", ""),
        "track_length_m": match.get("track_length_m", ""),
        "track_best_time_s": match.get("track_best_time_s", ""),
        "track_best_rider": match.get("track_best_rider", ""),
        "heat_no": source.get("heat_no", ""),
        "heat_uid": source.get("heat_uid", ""),
        "rider_id": source.get("rider_id", ""),
        "gate": source.get("gate", ""),
        "points": "" if points is None else points,
        "result_position": source.get("result_position", ""),
        "motorbike": resolve_motorbike(source),
        "telemetry_present": int(telemetry_present),
        "result_present": int(result_present),
        "heat_time_s": format_number(heat_time),
        "l1_time_s": format_number(laps[0]),
        "l2_time_s": format_number(laps[1]),
        "l3_time_s": format_number(laps[2]),
        "l4_time_s": format_number(laps[3]),
        "max_speed_kph": format_number(max_speed),
        "total_distance_m": format_number(total_distance),
        "flying_lap_median_s": format_number(flying_median),
        "l1_penalty_s": format_number(l1_penalty),
        "average_speed_mps": format_number(average_speed),
        "complete_telemetry": int(complete),
        "clean_physics": int(clean),
        "eventful": int(eventful),
        "audit_only": int(not complete),
        "exclusion_reason": ";".join(reasons),
        "dataset_split": "",
        "development_fold": "",
        # These are categorical gate-ranking flags from the source, never speeds.
        "gate_rank_position": source.get("gate_rank_position", ""),
        "gate_rank_speed_2s": source.get("gate_rank_speed_2s", ""),
        "gate_rank_curve_speed": source.get("gate_rank_curve_speed", ""),
    }


def numeric(row: dict[str, Any], name: str) -> float:
    return float(row[name])


def residual_analysis(clean_four_rider_groups: Sequence[list[dict[str, Any]]]) -> dict[str, Any]:
    residuals: list[dict[str, Any]] = []
    metrics = {
        "heat_time_residual_s": "heat_time_s",
        "vmax_residual_kph": "max_speed_kph",
        "l1_residual_s": "l1_time_s",
        "average_speed_residual_mps": "average_speed_mps",
    }
    for group in clean_four_rider_groups:
        means = {name: statistics.fmean(numeric(row, field) for row in group) for name, field in metrics.items()}
        for row in group:
            item = {
                "match_id": row["match_id"],
                "date": row["date"],
                "heat_uid": row["heat_uid"],
                "rider_id": row["rider_id"],
            }
            for name, field in metrics.items():
                item[name] = numeric(row, field) - means[name]
            residuals.append(item)

    grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for item in residuals:
        grouped[item["rider_id"]].append(item)
    rider_fingerprints = []
    for rider_id, observations in sorted(grouped.items(), key=lambda pair: match_sort_value(pair[0])):
        fingerprint: dict[str, Any] = {"rider_id": rider_id, "observation_count": len(observations), "metrics": {}}
        for metric in metrics:
            values = [item[metric] for item in observations]
            fingerprint["metrics"][metric] = {
                "mean": statistics.fmean(values),
                "median": statistics.median(values),
                "p25": linear_quantile(sorted(values), 0.25),
                "p75": linear_quantile(sorted(values), 0.75),
                "sample_standard_deviation": statistics.stdev(values) if len(values) > 1 else None,
            }
        rider_fingerprints.append(fingerprint)

    eligible = {rider_id: sorted(items, key=lambda item: (item["date"], match_sort_value(item["match_id"]), item["heat_uid"])) for rider_id, items in grouped.items() if len(items) >= 12}
    persistence = {}
    for metric in metrics:
        first_values: list[float] = []
        second_values: list[float] = []
        for rider_id in sorted(eligible, key=match_sort_value):
            observations = eligible[rider_id]
            middle = len(observations) // 2
            first_values.append(statistics.median(item[metric] for item in observations[:middle]))
            second_values.append(statistics.median(item[metric] for item in observations[middle:]))
        persistence[metric] = {
            "minimum_observations_per_rider": 12,
            "rider_sample_size": len(first_values),
            "spearman_rank_correlation": spearman(first_values, second_values),
        }

    def correlation(first_name: str, second_name: str) -> dict[str, Any]:
        return {
            "observation_count": len(residuals),
            "spearman_rank_correlation": spearman(
                [item[first_name] for item in residuals],
                [item[second_name] for item in residuals],
            ),
        }

    correlations = {
        "vmax_vs_heat_time": correlation("vmax_residual_kph", "heat_time_residual_s"),
        "l1_vs_heat_time": correlation("l1_residual_s", "heat_time_residual_s"),
        "average_speed_vs_heat_time": correlation("average_speed_residual_mps", "heat_time_residual_s"),
    }
    return {
        "observation_count": len(residuals),
        "rider_count": len(grouped),
        "fingerprints": rider_fingerprints,
        "split_half_persistence": persistence,
        "within_heat_correlations": correlations,
        "interpretation": "Empirical rider-relative performance fingerprints; no mapping to game RiderSkills.",
    }


def metric_summary(clean_rows: Sequence[dict[str, Any]]) -> dict[str, Any]:
    result = {}
    for field, metadata in METRICS.items():
        values = [numeric(row, field) for row in clean_rows]
        result[field] = {
            **metadata,
            "source": "PGE Ekstraliga public Firestore telemetry",
            "source_version": DATASET_VERSION,
            "population": "PGEE CleanPhysics rider-heat records",
            "confidence": "ObservedTelemetryConservativeSubset",
            "notes": "A distribution/performance envelope, never a hard limit.",
            "observation_count": len(values),
            "quantiles": quantile_map(values),
        }
    return result


def within_heat_summary(groups: Sequence[list[dict[str, Any]]]) -> dict[str, Any]:
    fields = {
        "heat_time_spread_s": "heat_time_s",
        "vmax_spread_kph": "max_speed_kph",
        "l1_spread_s": "l1_time_s",
        "average_speed_spread_mps": "average_speed_mps",
    }
    result = {}
    for name, field in fields.items():
        values = [max(numeric(row, field) for row in group) - min(numeric(row, field) for row in group) for group in groups]
        result[name] = {"attempt_count": len(values), "quantiles": quantile_map(values)}
    return result


def literature_targets() -> dict[str, Any]:
    source = {
        "authors": "Markowski M, Szczepan S, Zatoń M, Martin S, Michalik K.",
        "title": "The importance of reaction time to the starting signal on race results in elite motorcycle speedway racing",
        "journal": "PLOS ONE",
        "citation": "PLOS ONE 18(1): e0281138 (2023)",
        "doi": "10.1371/journal.pone.0281138",
        "provenance": "Values supplied directly in the PR #32 task specification; the PDF is not included.",
    }
    populations = [
        ("senior", 0.246, 0.050),
        ("junior", 0.258, 0.050),
        ("main_phase", 0.255, 0.048),
        ("knockout_phase", 0.239, 0.046),
        ("semifinals", 0.229, 0.044),
    ]
    observations = [
        {
            "metric_id": f"literature_reaction_{population}",
            "unit": "s",
            "definition": "Time from lifting of the starting tape to the first forward movement of the speedway motorcycle.",
            "source": source,
            "source_version": "task-supplied-2026-09-05",
            "population": population,
            "comparability": "LiteratureContext",
            "confidence": "PublishedPopulationObservation",
            "mean": mean,
            "reported_dispersion": dispersion,
            "reported_dispersion_not_reinterpreted": True,
            "measurement_accuracy_s": 0.01,
            "notes": "Population context only; not an age/category physics modifier.",
        }
        for population, mean, dispersion in populations
    ]
    observations.extend(
        [
            {
                "metric_id": "literature_time_to_approximately_80_kph",
                "unit": "s",
                "definition": "Context statement that a speedway motorcycle reaches approximately 80 km/h in about 2.4 s.",
                "source": source,
                "source_version": "task-supplied-2026-09-05",
                "population": "context",
                "comparability": "ContextSanityOnly",
                "confidence": "LiteratureContext",
                "value": 2.4,
                "notes": "Not a calibration equality target.",
            },
            {
                "metric_id": "literature_false_start_response_context",
                "unit": "s",
                "definition": "Approximate response interval discussed as a false-start or unfair-advantage threshold.",
                "source": source,
                "source_version": "task-supplied-2026-09-05",
                "population": "future rule context",
                "comparability": "FutureRuleContextOnly",
                "confidence": "LiteratureContext",
                "range": {"lower": 0.10, "upper": 0.12},
                "notes": "No reaction cap or gate multiplier is introduced.",
            },
        ]
    )
    return {"schema_version": SCHEMA_VERSION, "observations": observations}


def prepare(args: argparse.Namespace) -> dict[str, Any]:
    paths = {
        "telemetry_full.csv": args.telemetry_full,
        "matches.csv": args.matches,
        "heats.csv": args.heats,
        "run_summary.json": args.run_summary,
        "README.md": args.source_readme,
        "download_telemetry.py": args.downloader,
        "sample_match_raw.json": args.sample_match_raw,
        "7734.json": args.raw_match,
    }
    missing = [name for name, path in paths.items() if not path.is_file()]
    if missing:
        raise FileNotFoundError("Missing required source files: " + ", ".join(missing))

    match_source = read_csv(args.matches)
    telemetry_source = read_csv(args.telemetry_full)
    heat_source = read_csv(args.heats)
    run_summary = json.loads(args.run_summary.read_text(encoding="utf-8-sig"))
    validate_raw_match_structure(json.loads(args.sample_match_raw.read_text(encoding="utf-8-sig")))
    validate_raw_match_structure(json.loads(args.raw_match.read_text(encoding="utf-8-sig")))
    matches_by_id = {row["match_id"]: row for row in match_source}
    pgee_matches = filter_pgee_matches(matches_by_id.values())
    normalized = [normalize_row(row, pgee_matches[row["match_id"]]) for row in telemetry_source if row.get("match_id") in pgee_matches]

    eligible_ids = {
        row["match_id"] for row in normalized if row["complete_telemetry"] == 1
    }
    eligible_matches = sorted(
        (pgee_matches[match_id] for match_id in eligible_ids),
        key=lambda row: (row.get("date", ""), match_sort_value(row["match_id"])),
    )
    final_count = math.ceil(len(eligible_matches) * 0.20)
    final_ids = {row["match_id"] for row in eligible_matches[-final_count:]} if final_count else set()
    split_rows = []
    split_by_match = {}
    for match in eligible_matches:
        match_id = match["match_id"]
        split = "FINAL_TEST" if match_id in final_ids else "DEVELOPMENT"
        fold = "" if split == "FINAL_TEST" else development_fold(match_id)
        split_by_match[match_id] = (split, fold)
        split_rows.append({"match_id": match_id, "split": split, "fold": fold})
    for row in normalized:
        if row["match_id"] in split_by_match:
            row["dataset_split"], row["development_fold"] = split_by_match[row["match_id"]]

    normalized.sort(
        key=lambda row: (
            row["date"],
            match_sort_value(row["match_id"]),
            parse_int(str(row["heat_no"])) or -1,
            row["heat_uid"],
            match_sort_value(row["rider_id"]),
        )
    )
    pgee_match_rows = []
    match_columns = (
        "match_id", "date", "season", "league", "home_team", "away_team", "source_track_label",
        "track_id", "track_city", "track_name", "track_length_m", "track_best_time_s", "track_best_rider",
        "telemetry_available",
    )
    for match in sorted(pgee_matches.values(), key=lambda row: (row.get("date", ""), match_sort_value(row["match_id"]))):
        pgee_match_rows.append(
            {
                **match,
                "source_track_label": match.get("track", ""),
                "track_id": match.get("track_id", ""),
                "track_city": match.get("track_city", ""),
                "track_name": match.get("track_name", ""),
                "track_length_m": match.get("track_length_m", ""),
                "track_best_time_s": match.get("track_best_time_s", ""),
                "track_best_rider": match.get("track_best_rider", ""),
            }
        )

    clean_rows = [row for row in normalized if row["clean_physics"] == 1]
    heat_groups: dict[tuple[str, str], list[dict[str, Any]]] = defaultdict(list)
    for row in clean_rows:
        heat_groups[(row["match_id"], row["heat_uid"])].append(row)
    clean_four = [group for _, group in sorted(heat_groups.items()) if len(group) == 4 and len({row["rider_id"] for row in group}) == 4]

    output = args.output
    write_csv(output / "pge_rider_heats.csv", NORMALIZED_COLUMNS, normalized)
    write_csv(output / "pge_matches.csv", match_columns, pgee_match_rows)
    write_csv(output / "pge_split.csv", ("match_id", "split", "fold"), split_rows)
    write_json(output / "literature_targets.json", literature_targets())

    exclusion_counts = Counter(
        reason for row in normalized for reason in str(row["exclusion_reason"]).split(";") if reason
    )
    fold_counts = []
    for fold in range(5):
        ids = {row["match_id"] for row in split_rows if row["fold"] == fold}
        fold_counts.append(
            {
                "fold": fold,
                "match_count": len(ids),
                "clean_row_count": sum(row["clean_physics"] == 1 and row["match_id"] in ids for row in normalized),
            }
        )
    summary = {
        "schema_version": SCHEMA_VERSION,
        "dataset_version": DATASET_VERSION,
        "source_counts": {
            "downloaded_matches": run_summary.get("downloaded_matches"),
            "telemetry_detail_records": run_summary.get("telemetry_detail_records"),
            "heats_csv_rows": len(heat_source),
            "telemetry_full_csv_rows": len(telemetry_source),
            "matches_csv_rows": len(match_source),
        },
        "dataset_counts": {
            "pgee_match_count": len(pgee_matches),
            "normalized_row_count": len(normalized),
            "complete_telemetry_count": sum(row["complete_telemetry"] == 1 for row in normalized),
            "clean_physics_count": len(clean_rows),
            "eventful_complete_count": sum(row["eventful"] == 1 for row in normalized),
            "audit_only_count": sum(row["audit_only"] == 1 for row in normalized),
            "complete_match_count": len(eligible_ids),
            "complete_rider_count": len({row["rider_id"] for row in normalized if row["complete_telemetry"] == 1}),
            "clean_rider_count": len({row["rider_id"] for row in clean_rows}),
            "clean_four_rider_attempt_count": len(clean_four),
        },
        "rules": {
            "league_filter": "exact league == PGEE",
            "heat_identity": "match_id + heat_uid",
            "complete_telemetry": "both presence flags; finite positive heat/L1/L2/L3/L4/Vmax/distance; points in 0..3; abs(heat-sum(laps)) <= 0.05 s",
            "clean_physics": "CompleteTelemetry and every L2-L4 within +/-10% of median(L2,L3,L4)",
            "eventful": "CompleteTelemetry outside the conservative steady-lap heuristic; not a claim of crash/contact/invalidity",
            "audit_only": "not CompleteTelemetry; retained in the normalized snapshot",
            "quantiles": "linear interpolation at sorted zero-based index (n-1)*p",
            "final_holdout": "latest ceil(20% * eligible PGEE matches), ordered by date then match_id",
            "development_folds": "big-endian integer SHA256(UTF-8 match_id) modulo 5",
        },
        "exclusion_reason_counts": dict(sorted(exclusion_counts.items())),
        "track_metadata_coverage": {
            field: sum(bool(row.get(field)) for row in pgee_match_rows)
            for field in ("track_id", "track_city", "track_name", "track_length_m", "track_best_time_s", "track_best_rider")
        },
        "splits": {
            "development_match_count": sum(row["split"] == "DEVELOPMENT" for row in split_rows),
            "final_test_match_count": sum(row["split"] == "FINAL_TEST" for row in split_rows),
            "development_folds": fold_counts,
        },
        "distributions": metric_summary(clean_rows),
        "within_heat_spreads": within_heat_summary(clean_four),
        "rider_relative": residual_analysis(clean_four),
    }
    write_json(output / "summary.json", summary)

    file_metadata = []
    row_counts = {
        "telemetry_full.csv": len(telemetry_source),
        "matches.csv": len(match_source),
        "heats.csv": len(heat_source),
        "run_summary.json": run_summary.get("downloaded_matches"),
        "README.md": None,
        "download_telemetry.py": None,
        "sample_match_raw.json": 1,
        "7734.json": 1,
    }
    for name, path in paths.items():
        file_metadata.append({"file_name": name, "sha256": sha256(path), "row_or_document_count": row_counts[name]})
    manifest = {
        "schema_version": SCHEMA_VERSION,
        "generated_at_utc": validate_timestamp(args.generated_at_utc),
        "calibration_dataset_version": DATASET_VERSION,
        "source_description": "PGE Ekstraliga public Firestore telemetry",
        "authentication": "No credentials or authentication are used by this offline preparation pipeline.",
        "sources": file_metadata,
        "source_row_counts": summary["source_counts"],
    }
    write_json(output / "source_manifest.json", manifest)
    return summary


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--telemetry-full", type=Path, required=True)
    parser.add_argument("--matches", type=Path, required=True)
    parser.add_argument("--heats", type=Path, required=True)
    parser.add_argument("--run-summary", type=Path, required=True)
    parser.add_argument("--source-readme", type=Path, required=True)
    parser.add_argument("--downloader", type=Path, required=True)
    parser.add_argument("--sample-match-raw", type=Path, required=True)
    parser.add_argument("--raw-match", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--generated-at-utc", required=True)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = parse_args(argv)
    summary = prepare(args)
    print(json.dumps(summary["dataset_counts"], sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
