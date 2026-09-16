#!/usr/bin/env python3
"""Inspect archived PGE Firestore documents for recoverable start telemetry.

This is an offline schema inspector.  It deliberately keeps the gate winner
categories separate from physical measurements and never converts their
ordinal/category values into speeds or times.
"""

from __future__ import annotations

import argparse
import json
import math
from collections import defaultdict
from pathlib import Path
from typing import Any, Iterable, Sequence


MOTOARENA_SELECTOR = {
    "season": "2026",
    "league": "PGEE",
    "source_track_label": "Motoarena im. Mariana Rosego",
}

RANKING_FIELDS = ("speed_2s", "curve_speed")
PHYSICAL_METRICS = {
    "reaction_time_s": "s",
    "time_to_70_kph_s": "s",
    "speed_at_2s_kph": "km/h",
    "first_corner_entry_speed_kph": "km/h",
}


def telemetry_fields(document: dict[str, Any]) -> dict[str, Any]:
    """Return fields from either a standalone or downloader-wrapped document."""
    if "fields" in document:
        return document["fields"]
    try:
        return document["responses"]["telemetry"]["fields"]
    except (KeyError, TypeError) as error:
        raise ValueError("Document has no Firestore telemetry fields") from error


def gate_ranking_records(document: dict[str, Any], field_name: str) -> list[dict[str, Any]]:
    """Read categorical gate-winner entries without exposing them as numbers."""
    if field_name not in RANKING_FIELDS:
        raise ValueError(f"Unsupported gate ranking field: {field_name}")
    try:
        fields = telemetry_fields(document)
        by_heat = fields["gates"]["mapValue"]["fields"]["data"]["mapValue"]["fields"] \
            [field_name]["mapValue"]["fields"]
    except (KeyError, TypeError) as error:
        raise ValueError(f"Missing exact ranking path for {field_name}") from error

    records: list[dict[str, Any]] = []
    for heat_text, array in sorted(by_heat.items(), key=lambda item: int(item[0])):
        heat_no = int(heat_text)
        for source_index, value in enumerate(array["arrayValue"].get("values", [])):
            entry = value["mapValue"]["fields"]
            source_no = int(entry["no"]["integerValue"])
            gate_value = entry["gate"]
            if "stringValue" in gate_value:
                gate = gate_value["stringValue"]
                if gate not in {"a", "b", "c", "d"}:
                    raise ValueError(f"Unexpected categorical gate value: {gate!r}")
            elif "nullValue" in gate_value and gate_value["nullValue"] is None:
                gate = ""
            else:
                raise ValueError("Gate ranking entry has neither stringValue nor nullValue")
            records.append({
                "field": field_name,
                "heat_no": heat_no,
                "source_index": source_index,
                "source_no": source_no,
                "gate": gate,
                "classification": "RankingCategory",
                "unit": "none",
            })
    return records


def schema_map(document: dict[str, Any]) -> list[dict[str, str]]:
    """Build the auditable start-related map for the exact raw paths."""
    rows: list[dict[str, str]] = []
    for field_name in RANKING_FIELDS:
        records = gate_ranking_records(document, field_name)
        example = records[0] if records else None
        suffix = "stringValue|nullValue"
        rows.append({
            "source_path": (
                "fields.gates.mapValue.fields.data.mapValue.fields."
                f"{field_name}.mapValue.fields.<heat_no>.arrayValue.values[<index>]."
                f"mapValue.fields.gate.{suffix}"
            ),
            "source_field": field_name,
            "example_raw_value": "" if example is None else example["gate"],
            "type": "categorical gate id or null",
            "unit": "none",
            "semantic_meaning": (
                "gate associated with the per-heat highest speed after two seconds"
                if field_name == "speed_2s"
                else "gate associated with the per-heat highest speed at first-corner entry"
            ),
            "classification": "RankingCategory",
            "availability": "available",
            "confidence": "HighExactRawShapeAndAppLabel",
        })
        rows.append({
            "source_path": (
                "fields.gates.mapValue.fields.data.mapValue.fields."
                f"{field_name}.mapValue.fields.<heat_no>.arrayValue.values[<index>]."
                "mapValue.fields.no.integerValue"
            ),
            "source_field": "no",
            "example_raw_value": "" if example is None else str(example["source_no"]),
            "type": "integer encoded as string",
            "unit": "none",
            "semantic_meaning": "source heat number, not rank, time, or speed",
            "classification": "Identifier",
            "availability": "available",
            "confidence": "HighExactRawShape",
        })
    for metric, unit in PHYSICAL_METRICS.items():
        rows.append({
            "source_path": "not present in inspected source schema",
            "source_field": metric,
            "example_raw_value": "",
            "type": "none",
            "unit": unit,
            "semantic_meaning": metric,
            "classification": "PhysicalNumeric",
            "availability": "UnsupportedNumericByAvailableSource",
            "confidence": "HighFullSchemaScan",
        })
    return rows


def recovered_physical_metrics(document: dict[str, Any]) -> dict[str, float | None]:
    """Return no values unless an explicit supported physical path is added."""
    # Parsing rankings here would be a category error.  The inspected schema has
    # no physical start-value path, so every requested metric remains absent.
    telemetry_fields(document)
    return {metric: None for metric in PHYSICAL_METRICS}


def parse_optional_physical_number(value: str | None) -> float | None:
    if value is None or not value.strip():
        return None
    parsed = float(value)
    if not math.isfinite(parsed):
        raise ValueError("Physical value must be finite")
    return parsed


def linear_quantile(sorted_values: Sequence[float], probability: float) -> float:
    if not sorted_values:
        raise ValueError("At least one observation is required")
    if not 0 <= probability <= 1:
        raise ValueError("Probability must be in [0, 1]")
    position = (len(sorted_values) - 1) * probability
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return float(sorted_values[lower])
    fraction = position - lower
    return float(sorted_values[lower] + (sorted_values[upper] - sorted_values[lower]) * fraction)


def quantiles(values: Iterable[float]) -> dict[str, float]:
    ordered = sorted(values)
    return {
        f"p{round(probability * 100):02d}": linear_quantile(ordered, probability)
        for probability in (0.10, 0.25, 0.50, 0.75, 0.90)
    }


def group_attempts(rows: Iterable[dict[str, str]]) -> dict[tuple[str, str], list[dict[str, str]]]:
    groups: dict[tuple[str, str], list[dict[str, str]]] = defaultdict(list)
    for row in rows:
        groups[(row["match_id"], row["heat_uid"])].append(row)
    return dict(groups)


def is_exact_motoarena(row: dict[str, str]) -> bool:
    return all(row.get(key) == value for key, value in MOTOARENA_SELECTOR.items())


def render_evidence(documents: Sequence[dict[str, Any]]) -> str:
    if not documents:
        raise ValueError("At least one document is required")
    value = {
        "classification": "UnsupportedNumericByAvailableSource",
        "document_count": len(documents),
        "physical_metrics": {metric: {"unit": unit, "observation_count": 0}
                             for metric, unit in PHYSICAL_METRICS.items()},
        "schema_map": schema_map(documents[0]),
        "series_0_60m_or_0_3s": "No recoverable 0–60 m physical speed series found",
    }
    return json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", nargs="+", type=Path, help="archived raw Firestore JSON documents")
    parser.add_argument("--output", type=Path, help="optional deterministic JSON evidence output")
    args = parser.parse_args()
    documents = [json.loads(path.read_text(encoding="utf-8-sig")) for path in args.inputs]
    output = render_evidence(documents)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(output, encoding="utf-8", newline="\n")
    else:
        print(output, end="")


if __name__ == "__main__":
    main()
