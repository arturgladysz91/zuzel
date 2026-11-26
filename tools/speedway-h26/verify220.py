"""Verify the archived H39-X command, force, clearance and regression evidence."""

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


def prefix(earlier, tape):
    assert tape[:len(earlier)] == earlier


def check_tape(tape):
    assert all(0 <= q["Command"]["Bike"]["Drive"] <= 1 for q in tape)
    assert all(q["Time"] >= q["Requested"] for q in tape)
    assert all(b["Requested"] > a["Requested"] and b["Time"] > a["Time"] for a, b in zip(tape, tape[1:]))


def speed(state):
    p = state["Momentum"]
    return math.hypot(p["X"], p["Y"]) * 3.6 / 147


def trajectory(path, exit_station, next_entry, score=None):
    evidence = read(path)
    tape = evidence["Commands"]
    check_tape(tape)
    state = evidence["Seed"]
    maximum_slip_error = maximum_defect = cumulative_defect = 0.0
    full = longest = continuous = straight = load = 0.0
    powered = [0.0] * 3
    rows = evidence["Rows"]
    edge = min(r["SweptBoundary"] for r in rows)
    ground = min(r["SweptGround"] for r in rows)
    assert edge >= .50 - 1e-7 and ground > 0, (path, edge, ground)
    for row in rows:
        after = row["State"]
        time = after["Time"]
        dt = time - state["Time"]
        assert 0 < dt <= .0025 + 1e-8
        assert row["PendingFrom"] == after["Next"]
        assert 0 <= row["PendingFrom"] <= row["PendingUntil"] <= len(tape)
        assert all(0 <= row[name] <= 1.000001 for name in ("FrontUse", "RearUse"))
        assert row["FrontNormal"] >= 0 and row["RearNormal"] >= 0
        assert math.sqrt(sum(c * c for c in row["InternalRiderForce"].values())) <= 700.00001
        assert abs(row["ActivePower"]) <= 350.00001
        maximum_slip_error = max(maximum_slip_error, abs(row["ContactSlip"] - row["SolverSlip"]))
        cumulative_defect += row["EnergyResidual"]
        maximum_defect = max(maximum_defect, abs(cumulative_defect))
        distance = row["ReferenceMeters"]
        drive = row["ActiveDrive"]
        if 0 <= distance <= exit_station:
            slide = row["RearNormal"] > 20 and abs(row["VehicleSlip"]) >= math.pi / 30
            if slide:
                load += dt
            powered_slide = slide and row["SolverSlip"] >= .06
            for i, threshold in enumerate((.25, .50, .85)):
                if powered_slide and drive + 1e-8 >= threshold:
                    powered[i] += dt
            if powered_slide and drive >= .999 - 1e-8:
                full += dt
                continuous += dt
                longest = max(longest, continuous)
            else:
                continuous = 0
        if exit_station < distance < next_entry and drive >= .999 - 1e-8:
            straight += dt
        state = after
    if score:
        reach = rows[-1]["ReferenceMeters"]
        assert math.isclose(score["LastMeters"], reach, abs_tol=1e-7)
        assert math.isclose(score["EnergyDefect"], maximum_defect, abs_tol=1e-7)
        assert score["PeakMuscleForce"] <= 700.0001
        assert score["PeakMusclePower"] <= 350.0001
        for field, value in zip(("LoadedSideslip", "Powered25", "Powered50", "Powered85",
                                 "FullSlide", "LongestFullSlide", "StraightFull"),
                                (load, *powered, full, longest, straight), strict=True):
            assert math.isclose(score[field], value, abs_tol=6e-5), (path, field, score[field], value)
    return {
        "steps": len(rows), "endKph": speed(state), "commandCount": len(tape),
        "sweptBoundary": edge, "sweptHardPart": ground, "maxSlipRatioDifference": maximum_slip_error,
        "maximumCumulativeEnergyDefectJ": maximum_defect, "loadedSlideSeconds": load,
        "poweredSlideSeconds": powered, "fullSlideSeconds": full, "longestFullSlideSeconds": longest,
        "ordinaryStraightFullSeconds": straight,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=Path)
    args = parser.parse_args()
    root = args.root
    q = read(root / "qualification.json")
    freeze = read(root / "freeze.json")
    assert q["Selected"] == freeze["Selected"] and len(q["Holdout"]) == len(freeze["Holdouts"]) == 4
    assert [r["Name"] for r in q["Sensitivity"]] == ["compact", "nominal", "large"]
    baseline = read(root / "baseline-and-diagnosis.json")
    assert baseline["NativeWorlds"] == q["NativeWorlds"] == 0
    assert baseline["Straight218"]["Stable"] and len(baseline["Inherited"]) == 5
    assert len(baseline["MatchedRecovery"]) == 18
    histories = read(root / "exit-command-histories.json.gz")
    searched = read(root / "exit-search.json")["Proposals"]
    assert len(histories) == len(searched) == 70
    for history, proposal in zip(histories, searched, strict=True):
        assert history["Plan"] == proposal["Plan"] and history["Score"] == proposal["Score"]
        prefix(history["EarlierCommands"], history["Commands"])
        check_tape(history["Commands"])
    straight = read(root / "straight-experiment.json.gz")
    assert len(straight["Comparisons"]) == 60
    matched = {}
    for attempt in straight["Comparisons"]:
        key = attempt["Plan"]["Start"]
        matched.setdefault(key, (attempt["Matched"], attempt["PriorCommands"]))
        assert matched[key] == (attempt["Matched"], attempt["PriorCommands"])
        prefix(attempt["PriorCommands"], attempt["Commands"])
        check_tape(attempt["Commands"])
    assert len(matched) == 5
    assert len(read(root / "rider-support.json.gz")) == 54
    shadow = read(root / "tyre-shadow.json.gz")
    assert len({row["Variant"] for row in shadow}) == 7
    assert max(row["ResultantUse"] for row in shadow) <= 1.000001
    track = read(root / "stationing.json")
    frontier = read(root / "full-slide-frontier.json")["Verified"]
    assert frontier["LongestFullSlide"] >= 1.0724 and frontier["Bend"]
    fastest = read(root / "straight-full-diagnostic.json")["Replayed"]
    selected = {"selected": q["Nominal"], "straight-selected": straight["Selected"],
                "straight-full-diagnostic": fastest, "full-slide-frontier": frontier}
    selected.update({f"holdout-{i + 1}": score for i, score in enumerate(q["Holdout"])})
    names = ["archive-24", "archive-57", "archive-59", "archive-60", "original-straight", *selected]
    series = {key: trajectory(root / f"{key}.json.gz", track["ExitReferenceMeters"], q["NextEntryReference"],
                              selected.get(key)) for key in names}
    settle_root = root / "settle"
    settled = read(settle_root / "qualification.json")
    settle_freeze = read(settle_root / "freeze.json")
    settle = read(settle_root / "exit-search.json")["Proposals"]
    settle_histories = read(settle_root / "exit-command-histories.json.gz")
    assert len(settle) == len(settle_histories) == 27 and all(h["Plan"]["FrontCoastMeters"] for h in settle)
    assert settled["Selected"] == settle_freeze["Selected"] and len(settled["Holdout"]) == 4
    assert len(settle_freeze["Holdouts"]) == 4 and settled["NativeWorlds"] == 0
    assert [r["Name"] for r in settled["Sensitivity"]] == ["compact", "nominal", "large"]
    for h, attempt in zip(settle_histories, settle, strict=True):
        assert h["Plan"] == attempt["Plan"] and h["Score"] == attempt["Score"]
        prefix(h["EarlierCommands"], h["Commands"])
        check_tape(h["Commands"])
    for key, outcome in [("selected", settled["Nominal"]), *((f"holdout-{i + 1}", h)
                         for i, h in enumerate(settled["Holdout"]))]:
        series[f"settle/{key}"] = trajectory(settle_root / f"{key}.json.gz", track["ExitReferenceMeters"],
                                            q["NextEntryReference"], outcome)
    straight_holds = read(root / "straight-holdouts.json")
    straight_freeze = read(root / "straight-holdout-freeze.json")
    original_plan = {"FrontCoastMeters": None, **straight["SelectedPlan"]}
    assert straight_holds["Selected"] == straight_freeze["Selected"] == original_plan
    assert len(straight_freeze["Holds"]) == len(straight_holds["Outcomes"]) == 4
    assert straight_holds["NativeWorlds"] == 0
    for i, outcome in enumerate(straight_holds["Outcomes"], 1):
        name = f"straight-holdout-{i}"
        series[name] = trajectory(root / f"{name}.json.gz", track["ExitReferenceMeters"], q["NextEntryReference"], outcome)
    loss = read(root / "straight-loss-ledger.json")
    accounted = sum(loss[k] for k in ("FrontScrubWatts", "RearLateralSlipWatts", "RearLongitudinalSlipWatts", "DragWatts"))
    assert math.isclose(accounted, loss["ContactAndDragWatts"], abs_tol=1e-8)
    parent, failures = regression(root / "parent.trx.gz")
    current, current_failures = regression(root / "current.trx.gz")
    assert parent == current and failures == current_failures
    assert all("Box2D construction forbidden" in message for message in failures.values())
    checked = {"replays": series, "parent": parent, "current": current, "guardedNativeBlocked": sorted(failures),
               "exitTapes": len(histories) + len(settle_histories), "straightTapes": len(straight["Comparisons"]), "shadowRows": len(shadow)}
    (root / "verification.json").write_text(json.dumps(checked, indent=2) + "\n", encoding="utf-8", newline="\n")
    manifest = {}
    for path in sorted(root.rglob("*")):
        if path.is_file() and path.name != "manifest.json":
            with path.open("rb") as stream:
                manifest[path.relative_to(root).as_posix()] = {"bytes": path.stat().st_size,
                                       "sha256": hashlib.file_digest(stream, "sha256").hexdigest()}
    (root / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Verified {len(series)} continuous replays and {len(manifest)} file digests.")


if __name__ == "__main__":
    main()
