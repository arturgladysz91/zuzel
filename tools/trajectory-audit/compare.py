"""Structural platform comparison. No rounding or numeric normalization."""
import hashlib
import json
import math
from pathlib import Path
import re
import sys


def intent(candidate):
    return tuple(candidate["Intent"][key] for key in ("EntryTarget", "ApexTarget", "ExitTarget"))


def semantic(document):
    decisions = []
    for item in document["Decisions"]:
        candidates = item["Candidates"]
        rank = sorted(candidates, key=lambda c: (float(c["TotalCost"]), c["PredictedTraversalTimeSeconds"],
            c["RequestedLaneChange"], intent(c)))
        speed_rank = sorted((c for c in candidates if c["Traversal"]["CornerExitSpeedMetersPerSecond"] is not None),
            key=lambda c: (c["Traversal"]["CornerExitSpeedMetersPerSecond"], intent(c)))
        decisions.append({"Context": {k: item[k] for k in ("Scenario", "Seed", "ModelSeed", "Heat", "StepNumber", "RiderId") if k in item},
            "TargetLane": item["Decision"]["TargetLane"], "Selected": [intent(c) for c in candidates if c["Selected"]],
            "Ranking": [intent(c) for c in rank], "ExitSpeedOrdering": [intent(c) for c in speed_rank],
            "Candidates": [{"Intent": intent(c), "CompletedHorizon": c["Traversal"]["CompletedHorizon"],
                "Endpoints": [{k: p[k] for k in ("SegmentIndex", "Phase", "RequestedAnchor", "Outcome", "AnchorReached")}
                    for p in c["Traversal"]["PhaseEndpoints"]]} for c in candidates]})
    heat = document["FinalHeat"]
    return {"Decisions": decisions, "Classification": [{k: r[k] for k in ("RiderId", "Position", "Points", "Status", "LapsCompleted")}
        for r in heat["Classification"]], "Riders": [{k: r[k] for k in ("RiderId", "Lane", "Status", "LastResolvedSegmentId")}
        for r in heat["Riders"]]}


def differences(a, b, path="$"):
    if isinstance(a, dict) and isinstance(b, dict):
        if a.keys() != b.keys(): yield path + ".keys", list(a), list(b)
        for key in a:
            if key not in b: continue
            yield from differences(a[key], b[key], path + "." + key)
    elif isinstance(a, list) and isinstance(b, list):
        if len(a) != len(b): yield path + ".length", len(a), len(b)
        for i, (x, y) in enumerate(zip(a, b)): yield from differences(x, y, f"{path}[{i}]")
    elif a != b:
        yield path, a, b


def ordered_bits(bits):
    value = int(bits["Bits"], 16)
    sign = 1 << (31 if bits["Type"] == "float" else 63)
    return sign - (value & (sign - 1)) if value & sign else sign + value


def compare(left, right):
    a, b = [json.loads((folder / "audit.json").read_text()) for folder in (left, right)]
    abits, bbits = [json.loads((folder / "bits.json").read_text()) for folder in (left, right)]
    numeric, other = [], []
    for path, x, y in differences(a, b):
        entry = {"Path": path, "Windows": x, "Linux": y}
        if path in abits and path in bbits:
            entry.update(WindowsBits=abits[path], LinuxBits=bbits[path], AbsoluteDelta=abs(x-y),
                RelativeDelta=abs(x-y)/max(abs(x), abs(y)) if max(abs(x), abs(y)) else 0,
                ULP=abs(ordered_bits(abits[path])-ordered_bits(bbits[path])))
            numeric.append(entry)
        else: other.append(entry)
    first = numeric[0] if numeric else None
    if first:
        match = re.search(r"Decisions\[(\d+)\].*?Candidates\[(\d+)\]", first["Path"])
        if match:
            item = a["Decisions"][int(match[1])]
            first["Context"] = {k: item[k] for k in ("Scenario", "Seed", "ModelSeed", "Heat", "StepNumber", "RiderId") if k in item}
            first["Intent"] = intent(item["Candidates"][int(match[2])])
    margins = []
    for item in a["Decisions"]:
        costs = sorted(float(c["TotalCost"]) for c in item["Candidates"])
        if len(costs) > 1 and math.isfinite(costs[1]): margins.append(costs[1]-costs[0])
    sem_a, sem_b = semantic(a), semantic(b)
    encode = lambda value: json.dumps(value, sort_keys=True, separators=(",", ":")).encode()
    bit_differences = [path for path in abits if abits[path] != bbits.get(path)]
    raw_a, raw_b = [(folder / "trace.json").read_bytes() for folder in (left, right)]
    first_byte = next((i for i, (x, y) in enumerate(zip(raw_a, raw_b)) if x != y), None)
    stats = {"NumericDifferences": len(numeric), "IEEEFields": len(abits), "IEEEDifferences": bit_differences[:20],
        "FirstByteOffset": first_byte, "OnlyDocumentNewlinesDiffer": raw_a.replace(b"\r\n", b"\n") == raw_b.replace(b"\r\n", b"\n"),
        "WindowsCRLF": raw_a.count(b"\r\n"), "LinuxCRLF": raw_b.count(b"\r\n"),
        "OtherDifferences": other[:10], "First": first,
        "MaxULP": {kind: max((d["ULP"] for d in numeric if d["WindowsBits"]["Type"] == kind), default=0) for kind in ("float", "double")},
        "MaximumAbsoluteDelta": max((d["AbsoluteDelta"] for d in numeric), default=0),
        "MinimumWinnerMargin": min(margins), "BehaviorDifferences": list(differences(sem_a, sem_b))[:20],
        "SemanticSha256": hashlib.sha256(encode(sem_a)).hexdigest().upper(),
        "LinuxSemanticSha256": hashlib.sha256(encode(sem_b)).hexdigest().upper(),
        "FinalHeatNumericDifferences": [d for d in numeric if ".FinalHeat." in d["Path"]]}
    return stats


if __name__ == "__main__":
    root = Path(sys.argv[1])
    hashes = {folder.name: hashlib.sha256((folder / "trace.json").read_bytes()).hexdigest().upper()
        for folder in root.iterdir() if folder.is_dir()}
    case = "A" if hashes["ubuntu-latest-audited"] == hashes["ubuntu-latest-current"] else "B"
    result = {"Hashes": hashes, "Case": case, "OldPlatforms": compare(root / "windows-latest-audited", root / "ubuntu-latest-audited"),
        "CurrentPlatforms": compare(root / "windows-latest-current", root / "ubuntu-latest-current")}
    (root / "comparison.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))
    assert hashes["windows-latest-audited"] == "F20AE8E3895CF54625D549DB127BA1C8E2168A7AA3549ED94F305A04D89C3D18"
    assert hashes["windows-latest-audited"] == hashes["windows-latest-current"], "Windows regression"
    assert not result["CurrentPlatforms"]["BehaviorDifferences"], "Cross-platform behavior divergence"
