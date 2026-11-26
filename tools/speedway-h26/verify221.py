"""Independent checks over the packaged H40-L experimental evidence."""

import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path

from verify219 import regression


def load(path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8-sig") as stream:
        return json.load(stream)


def verify_fifo(commands):
    assert all(0 <= q["Command"]["Bike"]["Drive"] <= 1 and q["Requested"] <= q["Time"] for q in commands)
    assert all(a["Requested"] < b["Requested"] and a["Time"] < b["Time"] for a, b in zip(commands, commands[1:]))


def verify_trajectory(path, first_exit, next_entry, second_exit, expected=None):
    d = load(path)
    rows = d["Rows"]
    tape = d["Commands"]
    verify_fifo(tape)
    state = d["Seed"]
    before = state["Time"]
    energies = full = min_defect = max_defect = 0.0
    cutoffs = {s: [0.0, 0.0] for s in (100, 110, 115, 120)}
    times = {}
    for sample in rows:
        ts = sample["State"]
        dt = ts["Time"] - before
        assert 0 < dt <= .00250001, (path, before, dt)
        assert 0 <= sample["PendingFrom"] == ts["Next"] <= sample["PendingUntil"] <= len(tape)
        assert 0 <= sample["ActiveDrive"] <= 1
        assert 0 <= sample["FrontUse"] <= 1.000001 and 0 <= sample["RearUse"] <= 1.000001
        assert min(sample["FrontNormal"], sample["RearNormal"]) >= 0
        assert math.sqrt(sum(x*x for x in sample["InternalRiderForce"].values())) <= 700.00001
        assert abs(sample["ActivePower"]) <= 350.00001
        assert sample["SweptGround"] > 0
        energies += sample["EnergyResidual"]
        min_defect = min(min_defect, energies)
        max_defect = max(max_defect, energies)
        distance = sample["ReferenceMeters"]
        for name, station in (("firstEntry", 0), ("firstExit", first_exit),
                              ("secondEntry", next_entry), ("secondExit", second_exit)):
            if distance >= station and name not in times:
                times[name] = ts["Time"]
        if first_exit <= distance < next_entry:
            p = ts["Momentum"]
            speed = math.hypot(p["X"], p["Y"]) * 3.6 / 147
            if sample["ActiveDrive"] >= .999 - 1e-8:
                full += dt
            a, b = state["Center"], ts["Center"]
            traveled = math.hypot(a["X"] - b["X"], a["Y"] - b["Y"])
            for threshold, totals in cutoffs.items():
                if speed >= threshold:
                    totals[0] += dt
                    totals[1] += traveled
        state = ts
        before = state["Time"]
    energy_max = max(-min_defect, max_defect)
    if expected:
        assert len(rows) == expected["Steps"]
        assert math.isclose(rows[-1]["ReferenceMeters"], expected["LastStation"], abs_tol=1e-7)
        assert math.isclose(energy_max, expected["EnergyError"], abs_tol=1e-8)
        assert expected["Swept"] >= .5 and expected["Hard"] > 0
        assert expected["PeakForce"] <= 700.00001 and expected["PeakPower"] <= 350.00001
        assert math.isclose(full, expected["StraightFull"], abs_tol=.00251), (path, full, expected["StraightFull"])
        if expected["Linked"]:
            assert expected["SecondBend"] and expected["SecondRecovery"] and "secondExit" in times
            assert expected["LastStation"] >= second_exit + 21.99
    edge = min((r["SweptBoundary"] for r in rows), default=None)
    first_second_exit = times.get("secondExit", None)
    return {"rows": len(rows), "commands": len(tape), "acceptedEnergyDefectJ": energy_max,
            "screenedBoundaryMin": edge, "straightDistanceAndTime": cutoffs, "markers": times,
            "firstEntryToSecondExit": first_second_exit - times["firstEntry"]
            if first_second_exit is not None and "firstEntry" in times else None}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    root = parser.parse_args().root
    refs = load(root / "references.json")
    assert refs["NativeWorlds"] == 0
    assert refs["H37Straight"]["Stable"] and abs(refs["H37Straight"]["FinalKph"] - 82.48) < .01
    assert len(refs["Replayed"]) == 8 and max(x["PositionError"] for x in refs["Replayed"]) < 1e-8
    assert refs["OriginalProposal12"]["Plus20Gate"] and not refs["OriginalProposal12"]["LaterGate"]
    assert abs(refs["OriginalProposal12"]["NextEntryKph"] - 107.32749) < .00001
    stationing = load(root / "stationing.json")
    first_exit, next_entry, second_exit = (stationing[k] for k in ("FirstExit", "SecondEntry", "SecondExit"))
    files = {}
    for name in ("links", "refined"):
        folder = root / name
        q = load(folder / "link-qualification.json")
        freeze = load(folder / "link-freeze.json")
        assert q["NativeWorlds"] == 0 and q["Setup"] == freeze["Policy"]
        assert len(q["Holdouts"]) == len(freeze["Holdouts"]) == 4
        proposals = load(folder / "link-candidates.json.gz")
        assert len({p["Setup"]["Id"] for p in proposals}) == 64
        for p in proposals:
            assert p["Commands"][:len(p["EarlierCommands"])] == p["EarlierCommands"]
            verify_fifo(p["Commands"])
        assert sorted(t["Name"] for t in q["Geometry"]) == ["compact", "large", "nominal"]
        files[f"{name}/linked-training"] = q["ExactNominal"]
        files[f"{name}/linked-adaptive"] = q["AdaptiveNominal"]
        files.update({f"{name}/linked-holdout-{i}": x for i, x in enumerate(q["Holdouts"], 1)})
        frontier = folder / "speed-frontier.json"
        if frontier.exists() and (folder / "linked-speed-frontier.json.gz").exists():
            files[f"{name}/linked-speed-frontier"] = load(frontier)["Score"]
    for proposal in load(root / "links/direct.json"):
        files[f"links/direct-{abs(proposal['Setup']['Id'])}"] = proposal["Score"]
    power = load(root / "powered/powered-qualification.json")
    power_frozen = load(root / "powered/powered-freeze.json")
    assert power["Setup"] == power_frozen["Setup"] and len(power["Holdouts"]) == len(power_frozen["Holdouts"]) == 4
    power_tapes = load(root / "powered/powered-candidates.json.gz")
    assert len(power_tapes) == 64
    for p in power_tapes:
        verify_fifo(p["Commands"])
        assert p["Commands"][:len(p["EarlierCommands"])] == p["EarlierCommands"]
    files["powered/powered-selected"] = power["Nominal"]
    files.update({f"powered/powered-holdout-{i}": x for i, x in enumerate(power["Holdouts"], 1)})
    transfer = load(root / "transfer/transfer-qualification.json")
    transfer_frozen = load(root / "transfer/transfer-freeze.json")
    assert transfer["Setup"] == transfer_frozen["Setup"] and len(transfer["Holdouts"]) == len(transfer_frozen["Holdouts"]) == 4
    files["transfer/transfer-nominal"] = transfer["Nominal"]
    files.update({f"transfer/transfer-holdout-{i}": x for i, x in enumerate(transfer["Holdouts"], 1)})
    validation = load(root / "mechanics/lab-validation.json")
    assert all(validation["Gates"].values())
    variants = load(root / "mechanics/coupled-comparisons.json")
    assert len(variants) == 30
    for variant in variants:
        r = variant["Energy"]
        assert r["PeakFrontUse"] <= 1.000001 and r["LinearDefect"] < 1e-7 and r["AngularDefect"] < 1e-7
        files[f"mechanics/{variant['Key']}"] = None
    verified = {
        name: verify_trajectory(root / (name + ".json.gz"), first_exit, next_entry, second_exit, expected)
        for name, expected in files.items()
    }
    parent, expected_failures = regression(root / "parent.trx.gz")
    current, failures = regression(root / "current.trx.gz")
    assert parent["failed"] == current["failed"] == 10 and failures == expected_failures
    assert current["passed"] == parent["passed"] + 6
    assert all("Box2D construction forbidden" in message for message in failures.values())
    result = {"replays": verified, "parent": parent, "current": current, "guardedNativeBlocked": sorted(failures)}
    format_json(root / "verification.json", result)
    manifest = {}
    for path in sorted(root.rglob("*")):
        if path.is_file() and path.name != "manifest.json":
            with path.open("rb") as f:
                manifest[path.relative_to(root).as_posix()] = {"bytes": path.stat().st_size,
                                                               "sha256": hashlib.file_digest(f, "sha256").hexdigest()}
    format_json(root / "manifest.json", manifest)
    print(f"Verified {len(verified)} trajectories and {len(manifest)} evidence digests.")


def format_json(path, data):
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
