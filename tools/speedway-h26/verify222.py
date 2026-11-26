"""Verify H41 evidence chronology, invariants, frozen policy identity and checksums."""

import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path

from verify219 import regression


def read(path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8-sig") as stream:
        return json.load(stream)


def write(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8", newline="\n")


def tape(commands):
    assert all(0 <= x["Command"]["Bike"]["Drive"] <= 1 and x["Requested"] <= x["Time"] for x in commands)
    assert all(a["Requested"] < b["Requested"] and a["Time"] < b["Time"] for a, b in zip(commands, commands[1:]))


def verified(path, expected=None):
    d = read(path)
    commands = d["Commands"]
    tape(commands)
    last = d["Seed"]["Time"]
    accumulated = maximum = 0.0
    ground = math.inf
    for row in d["Rows"]:
        s = row["State"]
        dt = s["Time"] - last
        assert 0 < dt < .00250001, (path, dt)
        last = s["Time"]
        assert 0 <= row["ActiveDrive"] <= 1
        assert row["PendingFrom"] == s["Next"]
        assert 0 <= row["PendingFrom"] <= row["PendingUntil"] <= len(commands)
        assert row["FrontNormal"] >= 0 and row["RearNormal"] >= 0
        assert max(row["FrontUse"], row["RearUse"]) <= 1.000001
        assert abs(row["ActivePower"]) <= 350.00001
        assert math.sqrt(sum(x*x for x in row["InternalRiderForce"].values())) <= 700.00001
        assert row["SweptGround"] > 0, path
        ground = min(ground, row["SweptGround"])
        accumulated += row["EnergyResidual"]
        maximum = max(maximum, abs(accumulated))
    if expected is not None:
        assert len(d["Rows"]) == expected["Steps"]
        assert math.isclose(maximum, expected["EnergyError"], abs_tol=1e-7)
        assert expected["Swept"] >= .5 and expected["Hard"] > 0
        assert expected["PeakForce"] <= 700.00001 and expected["PeakPower"] <= 350.00001
        if expected["Linked"]:
            assert expected["SecondBend"] and expected["SecondRecovery"] and expected["LastStation"] >= 278.76
    return {"rows": len(d["Rows"]), "commands": len(commands), "maximumEnergyErrorJ": maximum,
            "minimumHardPartM": ground if math.isfinite(ground) else None, "stop": d["Stop"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    root = parser.parse_args().root

    ref = read(root / "references.json")
    assert ref["NativeWorlds"] == 0 and len(ref["Replayed"]) == 11
    assert max(r["StateDistance"] for r in ref["Replayed"]) < 1e-8
    simulated = read(root / "maps/directional-envelope.json.gz")
    assert len(simulated["Geometry"]) == 240 and len(simulated["Probes"]) == 46
    assert all(len(x["Reachable"]) == 9 for x in simulated["Probes"])
    assert len(read(root / "maps/equilibria.json")) == 24
    headings = read(root / "heading-audit.json.gz")
    assert set(headings) == {"highestEntry", "unloaded", "adaptive"}
    assert headings["highestEntry"]["last"]["frontNormalN"] > 500

    search = read(root / "race/race-selection.json")
    assert search["Training"] == 65 and search["Ablations"] == 10 and search["NativeWorlds"] == 0
    candidates = read(root / "race/candidates.json.gz")
    assert len({c["p"]["Id"] for c in candidates}) == 75
    scored = {p["Policy"]["Id"]:p["Score"] for p in search["Exact"]}
    root_replays = {}
    cycle_trials = read(root / "cycle/cycle-trials.json.gz")
    tape_counts = {"race": 75, "frontier": 17, "qualification": 16, "cycle": 12}
    for group, inputs in (
        ("race", candidates), ("frontier", read(root / "frontier/frontier-trials.json.gz")),
        ("qualification", read(root / "qualification/powered-search.json.gz")), ("cycle", cycle_trials),
    ):
        needed = tape_counts[group]
        assert len(inputs) == needed
        for planned in inputs:
            cmds = planned["Commands"]
            earlier = planned["EarlierCommands"]
            assert cmds[:len(earlier)] == earlier, (group, needed)
            tape(cmds)
    for p in sorted((root / "race").glob("race-*.json.gz")):
        root_replays[p.relative_to(root).as_posix()] = verified(p, scored[int(p.stem.split(".")[0].split("-")[1])])
    frontier = read(root / "frontier/frontier.json")
    if frontier["Exact"]:
        out = root / "frontier/speed-frontier.json.gz"
        root_replays[out.relative_to(root).as_posix()] = verified(out, frontier["Exact"][0]["Score"])
    folder = root / "qualification"
    q = read(folder / "qualification.json")
    frozen = read(folder / "freeze.json")
    assert q["Policy"] == frozen["Policy"] and len(q["Holdouts"]) == len(frozen["Holdouts"]) == 4
    assert [r["Name"] for r in q["Geometry"]] == ["compact", "large", "nominal"]
    power = read(folder / "powered-qualification.json")
    assert power["Setup"] == read(folder / "powered-freeze.json")["Setup"] and len(power["Holdouts"]) == 4
    for group, stats in (("", q), ("powered-", power)):
        for i, result in enumerate((stats["Nominal"], *stats["Holdouts"])):
            name = "nominal" if i == 0 else f"holdout-{i}"
            out = folder / (group + name + ".json.gz")
            root_replays[out.relative_to(root).as_posix()] = verified(out, result)
    lap = read(folder / "lap.json")
    assert lap["Policy"] == q["Policy"] and lap["NativeWorlds"] == 0
    out = folder / "flying.json.gz"
    root_replays[out.relative_to(root).as_posix()] = verified(out)

    folder_cycle = root / "cycle"
    cycle = read(folder_cycle / "cycle.json")
    assert cycle["Policy"] == read(folder_cycle / "cycle-freeze.json") == cycle_trials[0]["Plan"]
    assert cycle["NativeWorlds"] == 0 and cycle["LapMeters"] == lap["LapMeters"]
    assert cycle["SearchPasses"] == sum(row["Passed"] for row in cycle_trials)
    assert cycle["Boundary"] >= .5 and cycle["Ground"] > 0
    for gate in cycle["Gates"]:
        assert (gate["Time"] is not None) == (gate["Station"] <= cycle["Final"]["Distance"])
    out = folder_cycle / "cycle-flying.json.gz"
    root_replays[out.relative_to(root).as_posix()] = verified(out)

    timings = read(folder / "runtime.json")
    checks = timings["CacheChecks"]
    assert len(checks) == 14 and checks[0]["StateFifoContactAccepted"]
    assert all(not x["StateFifoContactAccepted"] for x in checks[1:])
    assert len(timings["Trials"]) == 3
    assert all(0 <= row["DeadlineMisses"] <= row["Count"] for row in timings["Trials"])

    mechanics = root / "mechanics"
    labs = read(mechanics / "front-isolation-rigs.json")["Probes"]
    assert len(labs) == 8 and all(x["Residual"] < .005 for x in labs)
    for rig in labs:
        for k in ("Air", "Coarse", "Countersteer"):
            row = rig[k]
            assert row["Stop"] == "verified interval"
            assert row["LinearDefect"] < 1e-7 and row["AngularDefect"] < 1e-7
            assert row["MaxEnergyDefect"] < .03
        assert abs(rig["Air"]["FrontSpinMomentumDelta"]) < 1e-9 and rig["Air"]["PeakFrontLoad"] == 0
    measured = read(mechanics / "front-isolation-matched.json")
    assert len(measured) == 24
    assert len(read(mechanics / "variant-retuning.json.gz")) == 8
    for probe in measured:
        assert probe["Rig"]["LinearDefect"] < 1e-7 and probe["Rig"]["AngularDefect"] < 1e-7
        assert abs(probe["Rig"]["FrontSpinMomentumDelta"] - probe["Rig"]["AxialGroundImpulse"]) < 1e-7
    for out in sorted(mechanics.glob("*.json.gz")):
        if not out.name.startswith(("forces-", "variant-")):
            root_replays[out.relative_to(root).as_posix()] = verified(out)

    parent, original = regression(root / "parent.trx.gz")
    current, failures = regression(root / "current.trx.gz")
    assert failures == original and parent["failed"] == current["failed"] == 10
    assert current["passed"] == parent["passed"] + 4
    assert all("Box2D construction forbidden" in f for f in original.values())
    write(root / "verification.json", {"replays": root_replays, "parent": parent, "current": current,
        "stockTapes": sum(tape_counts.values()), "checkedCacheMutations": len(checks) - 1, "forceProbes": len(simulated["Probes"])})
    out = {}
    for path in sorted(root.rglob("*")):
        if path.is_file() and path.name != "manifest.json":
            with path.open("rb") as file:
                out[path.relative_to(root).as_posix()] = {"bytes": path.stat().st_size,
                    "sha256": hashlib.file_digest(file, "sha256").hexdigest()}
    write(root / "manifest.json", out)
    print(f"Verified {len(root_replays)} trajectories, {sum(tape_counts.values())} proposal tapes, and {len(out)} file digests.")


if __name__ == "__main__":
    main()
