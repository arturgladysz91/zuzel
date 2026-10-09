"""Extract reproducible corner brackets and pass witnesses from existing typed nodes.

No new traversal equation or exact crossing timestamp is inferred. Usage:
python tools/race-engine-readiness/inspect.py audit-folder trace-folder [trace-folder ...]
"""
import gzip
import hashlib
import json
import re
import shutil
import struct
import sys
from pathlib import Path


def number(value):
    if value is None or isinstance(value, (int, float)):
        return value
    kind, payload = value.split(":", 1)
    if kind == "float":
        return struct.unpack(">f", bytes.fromhex(payload))[0]
    if kind == "double":
        return struct.unpack(">d", bytes.fromhex(payload))[0]
    if kind.startswith("System.Int"):
        return int(payload)
    raise ValueError("Unexpected numeric leaf: " + value)


def motions(leaves):
    pattern = re.compile(r"root\.Steps\[(\d+)\]\.Motions\[(\d+)\]\.Nodes\[(\d+)\]\.(\w+)$")
    grouped = {}
    for path, value in leaves.items():
        match = pattern.fullmatch(path)
        if match:
            step, motion, node, field = match.groups()
            grouped.setdefault((int(step), int(motion)), {}).setdefault(int(node), {})[field] = value
    result = []
    for (step, index), nodes in sorted(grouped.items()):
        prefix = f"root.Steps[{step}].Motions[{index}]"
        result.append({"Step": step, "RiderId": number(leaves[prefix + ".RiderId"]),
                       "SegmentIndex": number(leaves[prefix + ".SegmentIndex"]),
                       "Start": number(leaves[prefix + ".StartElapsedTimeSeconds"]),
                       "Nodes": [n for _, n in sorted(nodes.items())]})
    return result


def bracket(motion, a, b):
    return {"Step": motion["Step"], "RiderId": motion["RiderId"], "SegmentIndex": motion["SegmentIndex"],
            "StartSeconds": motion["Start"] + number(a["LocalTimeSeconds"]),
            "EndSeconds": motion["Start"] + number(b["LocalTimeSeconds"]),
            "BeforeTyped": a, "AfterTyped": b}


def at_time(rows, rider, time):
    candidates = []
    for m in rows:
        if m["RiderId"] != rider:
            continue
        for a, b in zip(m["Nodes"], m["Nodes"][1:]):
            start = m["Start"] + number(a["LocalTimeSeconds"])
            end = m["Start"] + number(b["LocalTimeSeconds"])
            if end > start and start <= time <= end:
                fraction = (time - start) / (end - start)
                # Stored-node interpolation only; keep the source IEEE values too.
                observation = {field: number(a[field]) + (number(b[field]) - number(a[field])) * fraction
                               for field in ("CanonicalProgress", "SpeedMetersPerSecond", "PhysicalOffsetFromInnerEdgeMeters")}
                candidates.append(dict(bracket(m, a, b), ObservedAtBracketBoundary=observation))
    # Later piece owns a shared boundary, consistent with native motion stitching.
    return max(candidates, key=lambda x: x["StartSeconds"]) if candidates else None


def inspect(leaves, case):
    rows = motions(leaves)
    base = "root.Steps[0].Snapshot.Track."
    segments = number(leaves[base + "Segments.Count"])
    corners = []
    for index in range(number(leaves[base + "CornerTopology.Corners.Count"])):
        prefix = base + f"CornerTopology.Corners[{index}]"
        corners.append((number(leaves[prefix + ".CornerId"]), number(leaves[prefix + ".StartSegmentIndex"]),
                        number(leaves[prefix + ".SegmentCount"])))
    apexes = []
    for lap in range(4):
        for corner, start, count in corners:
            progress = lap * segments + start + count / 2
            for m in rows:
                for a, b in zip(m["Nodes"], m["Nodes"][1:]):
                    if (number(b["LocalTimeSeconds"]) > number(a["LocalTimeSeconds"])
                            and number(a["CanonicalProgress"]) <= progress < number(b["CanonicalProgress"])):
                        apexes.append(dict(bracket(m, a, b), Lap=lap + 1, CornerId=corner, ApexCanonicalProgress=progress))
    witnesses = []
    for event in case["Order"]["Passes"]:
        # Start changes remain available in cases.json.gz; selected trace witnesses focus on racing.
        if event["Phase"] != "racing":
            continue
        witnesses.append({"Pass": event, "Before": [at_time(rows, rid, event["BracketStart"]) for rid in (event["Ahead"], event["Behind"])],
                          "After": [at_time(rows, rid, event["BracketEnd"]) for rid in (event["Ahead"], event["Behind"])]})
    return {"Id": case["Id"], "Seed": case["Seed"], "Configuration": case["Configuration"],
            "SummaryFinalHash": case["FinalHash"], "ApexBrackets": apexes, "RacingPassWitnesses": witnesses}


def main():
    audit = Path(sys.argv[1])
    summary = json.loads((audit / "race-engine-readiness.json").read_text(encoding="utf-8"))
    with gzip.open(audit / "cases.json.gz", "rt", encoding="utf-8") as f:
        cases = {(c["Id"], c["Seed"], c["Configuration"]): c for c in json.load(f)}
    target = audit / "traces"
    target.mkdir(exist_ok=True)
    results, files, diagnostics = [], {}, []
    reruns = 0
    for folder in map(Path, sys.argv[2:]):
        manifest = json.loads((folder / "manifest.json").read_text())
        if not manifest["Completed"] or manifest["SourceCommit"] != summary["SourceCommit"]:
            raise ValueError("Wrong or incomplete trace capture")
        for capture in manifest["Cases"]:
            reruns += 1
            key = capture["Id"], capture["Seed"], capture["Configuration"]
            if capture["FinalHash"] != cases[key]["FinalHash"] or capture["BehaviorHash"] != cases[key]["BehaviorHash"]:
                raise ValueError("Trace rerun changed Summary behavior")
            # FullAudit final parity is enforced in Program.cs before this manifest can complete.
            if capture["Configuration"] != "C" and not capture["Failed"]:
                continue
            name = f"trace-{key[0]}-{key[1]}-{key[2]}.json.gz"
            source = folder / name
            if source.exists():
                with gzip.open(source, "rt", encoding="utf-8") as f:
                    results.append(inspect(json.load(f), cases[key]))
                shutil.copyfile(source, target / name)
            else:
                name = f"failure-{key[0]}-{key[1]}-{key[2]}-False.json.gz"
                shutil.copyfile(folder / name, target / name)
            data = (target / name).read_bytes()
            files[name] = {"Bytes": len(data), "SHA256": hashlib.sha256(data).hexdigest()}
            diagnostic = folder / f"diagnostic-{key[0]}-{key[1]}-{key[2]}.json.gz"
            with gzip.open(diagnostic, "rt", encoding="utf-8") as f:
                d = json.load(f)
            diagnostics.append({"Id": key[0], "Seed": key[1], "Configuration": key[2], "Failed": d["Failed"],
                                "FinalHash": d["FinalHash"], "Options": d["Options"], "Episodes": d["Episodes"],
                                "Applied": d["Applied"], "Pairs": d["Pairs"], "Recoveries": d["Recoveries"]})
    with gzip.GzipFile(filename=str(target / "full-audit-diagnostics.json.gz"), mode="wb", mtime=0) as f:
        f.write(json.dumps(diagnostics, separators=(",", ":")).encode())
    data = (target / "full-audit-diagnostics.json.gz").read_bytes()
    files["full-audit-diagnostics.json.gz"] = {"Bytes": len(data), "SHA256": hashlib.sha256(data).hexdigest()}
    evidence = {"SourceCommit": summary["SourceCommit"], "Method": "Exact typed source nodes; canonical halfway-corner apex brackets; racing pass witnesses at the common-time bracket boundaries. No exact crossing instant or intent-success attribution.",
                "FullDiagnosticReruns": reruns, "Files": files, "Cases": results}
    (audit / "trace-evidence.json").write_text(json.dumps(evidence, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Published {len(files)} compressed traces/diagnostics and {len(results)} corner/pass extracts")


if __name__ == "__main__":
    main()
