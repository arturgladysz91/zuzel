"""Summarize identical sequential nine-sample benchmark protocols; retain raw work."""
import json
from pathlib import Path
from statistics import median
import sys


def compare(before_path, after_path, output):
    before, after = [json.loads(Path(p).read_text()) for p in (before_path, after_path)]
    assert before["Schema"] == after["Schema"] == "contested-performance-v1"
    assert before["Environment"] == after["Environment"]
    assert before["Samples"] == after["Samples"] == 9
    assert before["WarmupProtocol"] == after["WarmupProtocol"]
    rows = []
    for a, b in zip(before["Measurements"], after["Measurements"]):
        assert a["Name"] == b["Name"] and a["Warmups"] >= 8 and b["Warmups"] >= 8
        for case in (a, b):
            assert len(case["Samples"]) == 9
            assert all(sample["Work"] == case["Samples"][0]["Work"] for sample in case["Samples"])
        values = {}
        for metric in ("WallMilliseconds", "CpuMilliseconds", "AllocatedBytes"):
            old, new = [median(s[metric] for s in case["Samples"]) for case in (a, b)]
            values[metric] = {"BeforeMedian": old, "AfterMedian": new,
                              "ChangePercent": 100 * (new / old - 1) if old else None}
        rows.append({"Name": a["Name"], **values,
                     "BeforeWork": a["Samples"][0]["Work"], "AfterWork": b["Samples"][0]["Work"]})
    assert len(rows) == 10
    report = {"BaseMainSha": "f40b2fd129124d969fd22cfe7767e2fb00bd651f", "Environment": before["Environment"],
              "Protocol": before["WarmupProtocol"], "Samples": 9, "Cases": rows}
    Path(output).write_text(json.dumps(report, indent=2) + "\n")
    return report


if __name__ == "__main__":
    result = compare(*sys.argv[1:])
    for row in result["Cases"]:
        values = row["WallMilliseconds"]
        print(f"{row['Name']}: {values['BeforeMedian']:.3f} -> {values['AfterMedian']:.3f} ms ({values['ChangePercent']:+.1f}%)")
