"""Combine independently measured review baseline/current captures without retiming them."""
import json
from pathlib import Path
import sys

before_path, after_path, counters_path, output_path = map(Path, sys.argv[1:])
before = json.loads(before_path.read_text(encoding="utf-8-sig"))
after = json.loads(after_path.read_text(encoding="utf-8-sig"))
after["Schema"] = "56B-performance-v3"
after["OriginalPerformanceReviewHead"] = after["ReviewedHead"]
after["ReviewedHead"] = "c4f8d3a4853ddfe77541bd81f0a3d517b2f279cd"
assert before["Machine"] == after["Machine"], "Use the same machine and runtime"
counters = json.loads(counters_path.read_text(encoding="utf-8-sig"))
for sample in after["Resolutions"]:
    sample["Work"] = next(row["Work"] for row in counters
                          if row["Name"] == sample["Name"] and row["Enabled"] == sample["Enabled"])
after["SameMachineReviewedBaseline"] = {
    "Head": "c4f8d3a4853ddfe77541bd81f0a3d517b2f279cd",
    "MeasurementNote": "Exact reviewed HEAD measured in a detached checkout on the same machine/runtime. Both versions use Summary and the existing unchanged measurement harness. The earlier pre-optimization recording is preserved separately in BeforeReviewFixes.",
    "Resolutions": before["Resolutions"],
    "FullHeats": before["FullHeats"],
}
after["EngineeringTargets"] = {
    "DenseAllocationBytes": 10_000_000,
    "PreferredDenseMedianMilliseconds": 50,
    "Note": "Descriptive engineering targets, never simulation rules or CI wall-clock thresholds. All pairs still run the exact #55 algorithm; actual real-incident verification and the bounded safety pass remain enabled.",
}
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(json.dumps(after, indent=2) + "\n", encoding="utf-8")
