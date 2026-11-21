"""Derive Study #203 tables and figures from accepted native states and MODEL output."""

import argparse
import bisect
import collections
import gzip
import json
import math
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt


ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = ROOT / "data/experiments/speedway202"
MASS = 147
YAW_INERTIA = 52.72097
CURVES = ROOT / "tools/replay-viewer/recordings/h25/202-learned.json.gz"


def read(path):
    reader = gzip.open if path.suffix == ".gz" else open
    with reader(path, "rt", encoding="utf-8-sig") as source:
        return json.load(source)


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def dot(vector, other):
    return sum(vector[axis] * other[axis] for axis in ("X", "Y"))


def mid(vector, other):
    return {axis: (vector[axis] + other[axis]) / 2 for axis in ("X", "Y")}


def native_rows(path):
    record = read(path)
    run = record["Run"]
    driver = run["Riders"][0]
    posture = record.get("Posture", [])
    rows = []
    for i, (previous, current) in enumerate(zip(driver["Samples"], driver["Samples"][1:])):
        before, after = previous["Input"]["Rider"], current["Input"]["Rider"]
        f, p = current["Forces"], current["Powertrain"]
        dt = p["Seconds"]
        wheel = p["Wheel"]
        z = f["TyreLoads"]
        v0, v1 = before["Velocity"], after["Velocity"]
        yaw0, yaw1 = before["AngularVelocity"], after["AngularVelocity"]
        vm, yaw_mid = mid(v0, v1), (yaw0 + yaw1) / 2
        impulse = math.hypot(*(MASS * (v1[axis] - v0[axis]) - dt * f["NetForce"][axis] for axis in ("X", "Y")))
        yaw_impulse = YAW_INERTIA * (yaw1 - yaw0) - dt * f["NetTorque"]

        def work(force, arm=None):
            velocity = vm if arm is None else {
                "X": vm["X"] - yaw_mid * arm["Y"],
                "Y": vm["Y"] + yaw_mid * arm["X"],
            }
            return dt * dot(force, velocity)

        body_energy = .5 * MASS * (dot(v1, v1) - dot(v0, v0)) + .5 * YAW_INERTIA * (yaw1**2 - yaw0**2)
        crank_energy = .005 * (p["AfterOmega"]**2 - p["BeforeOmega"]**2)
        wheel_energy = .45 * (wheel["AfterOmega"]**2 - wheel["BeforeOmega"]**2)
        rear_lateral = {axis: f["RearLateral"] * f["BodyLeft"][axis] for axis in ("X", "Y")}
        full_residual = (
            p["CombustionWorkJoules"] - p["CrankDragWorkJoules"] - body_energy - crank_energy - wheel_energy
            - p["ClutchHeatJoules"] - p["TransmissionLossJoules"] - wheel["BearingLossJoules"]
            - wheel["LongitudinalLossJoules"] + work(f["FrontForce"], f["FrontArm"])
            + work(rear_lateral, f["RearArm"]) + work(f["Resistance"])
        )
        rider_z = posture[i]["Posture"] if posture else None
        rows.append(dict(
            time=current["Input"]["Time"], s=after["TotalReferenceMeters"], dt=dt,
            speed=after["Speed"] * 3.6, beta=math.degrees(after["SlideRadians"]), yaw=yaw1,
            gas=current["Input"]["Active"]["Drive"], rpm=p["Engine"]["EngineRpm"],
            spin=100 * wheel["InstantSlipRatio"], front=z["FrontNormalNewtons"], rear=z["RearNormalNewtons"],
            sweep=current["Certificate"], ax=dot(f["NetForce"], f["BodyForward"]) / MASS,
            posture=100 * rider_z["RiderHeightOffsetAfterMeters"] if rider_z else 0,
            height=rider_z["CombinedMidpointHeightMeters"] if rider_z else .55,
            impulse=impulse, yawImpulse=yaw_impulse, energy=full_residual, crankResidual=p["EnergyResidualJoules"],
            rearUtilization=wheel["RearUtilization"], positiveNormalResidual=z["FrontNormalNewtons"] + z["RearNormalNewtons"] - MASS * 9.81,
        ))
    return record, driver, rows


def native_summary(record, driver, rows, geometry):
    sections = []
    lap = geometry["lapMeters"]
    segments = geometry["segments"]
    bends = [(seg["startReferenceMeters"], next(
        nxt["startReferenceMeters"] for nxt in segments[i + 1:] if nxt["type"] == "Straight"
    )) for i, seg in enumerate(segments) if seg["type"] == "TurnEntry"]
    distances = [row["s"] for row in rows]

    def at(s, key):
        i = bisect.bisect_left(distances, s)
        if i == 0 and s >= 0 and key in ("time", "speed"):
            return rows[0][key] * s / max(1e-9, distances[0])
        if not 0 < i < len(rows):
            return None
        a, b = rows[i - 1:i + 1]
        fraction = (s - a["s"]) / (b["s"] - a["s"])
        return a[key] + fraction * (b[key] - a[key])

    def regain(begin, end, throttle):
        duration = 0
        first = None
        for row in rows:
            if not begin <= row["s"] < end:
                continue
            if row["gas"] < throttle:
                duration, first = 0, None
                continue
            if first is None:
                first = row["time"] - row["dt"]
            duration += row["dt"]
            if duration >= .15 - 1e-7:
                return max(0, first - at(begin, "time"))
        return None

    previous_exit = 0
    for lap_index in range(3):
        for bend_index, (entry_base, exit_base) in enumerate(bends):
            entry, exit_ = entry_base + lap * lap_index, exit_base + lap * lap_index
            next_entry = (bends[1][0] if bend_index == 0 else lap + bends[0][0]) + lap * lap_index
            next_entry = min(next_entry, 3 * lap)
            approach = [row for row in rows if previous_exit <= row["s"] <= entry]
            turn = [row for row in rows if entry <= row["s"] <= exit_]
            times = [at(s, "time") for s in (entry, (entry + exit_) / 2, exit_, exit_ + 20)]
            sections.append(dict(
                bend=2 * lap_index + bend_index + 1, priorStraightMax=max((row["speed"] for row in approach), default=None),
                entry=at(entry, "speed"), minimum=min((row["speed"] for row in turn), default=None),
                apex=at((entry + exit_) / 2, "speed"), exit=at(exit_, "speed"),
                entryToApex=times[1] - times[0] if None not in times[:2] else None,
                apexToExit=times[2] - times[1] if None not in times[1:3] else None,
                exitTo20m=times[3] - times[2] if None not in times[2:] else None,
                exitRegain99=regain(exit_, next_entry, .99), exitRegain90=regain(exit_, next_entry, .90),
            ))
            previous_exit = exit_

    duration = sum(row["dt"] for row in rows)
    speed70 = next((x["time"] for x in rows if x["speed"] >= 70), None)
    return dict(
        result=record["Result"], finish=driver["FinishSeconds"], sections=sections,
        vmax=max(row["speed"] for row in rows), v075=next((row["speed"] for row in rows if row["time"] >= .75), None),
        speed70Seconds=speed70, minFront=min(row["front"] for row in rows), maxAx=max(row["ax"] for row in rows),
        heightMin=min(row["height"] for row in rows), riderDropCm=min(row["posture"] for row in rows),
        driveShares=[sum(x["dt"] for x in rows if condition(x["gas"])) / duration for condition in (
            lambda u: u < .001, lambda u: .001 <= u < .99, lambda u: u >= .99
        )],
        rearUtilization=max(row["rearUtilization"] for row in rows),
        maxImpulseNs=max(row["impulse"] for row in rows), maxYawImpulseNms=max(abs(row["yawImpulse"]) for row in rows),
        maxEnergyJ=max(abs(row["energy"]) for row in rows), sumEnergyJ=sum(row["energy"] for row in rows),
        maxCrankResidualJ=max(abs(row["crankResidual"]) for row in rows),
        maxNormalResidualN=max(abs(row["positiveNormalResidual"]) for row in rows),
        patchReasons=dict(collections.Counter(row["Decision"] for row in record.get("Patches", []))),
        driverPath=driver["Samples"][-1]["Input"]["Rider"]["DistanceMeters"],
    )


def category(outcome):
    if outcome is None:
        return "admissible"
    if "no positive-load" in outcome:
        return "load-domain"
    if "solve failed convergence" in outcome:
        return "load-solver"
    return outcome.split(";")[0]


def audit_summary(model_dir):
    aggregate = {}
    for path in sorted(model_dir.glob("H*-model.json.gz")):
        record = read(path)
        steps = [row["Data"] for row in record["Steps"]]
        decisions = record["Decisions"]
        time = sum(x["Dt"] for x in steps)
        low = [x for x in steps if x["FrontNormalNewtons"] < .2 * MASS * 9.81]
        denominators = dict(all=len(steps), low=len(low), eligible=len(decisions))
        aggregate[path.name.split("-model.")[0]] = dict(
            denominators=denominators,
            yawMissSeconds={q: sum(x["Dt"] for x in steps if x["YawMiss"] > q) for q in (1, 10, 50)},
            yawMissPercent={q: 100 * sum(x["Dt"] for x in steps if x["YawMiss"] > q) / time for q in (1, 10, 50)},
            lowYawMissSteps=sum(x["YawMiss"] > 1 for x in low),
            frontDemandOutsideCapacity=sum(x["ForceTargetOutsideCapacity"] for x in steps),
            lowFrontDemandOutsideCapacity=sum(x["ForceTargetOutsideCapacity"] for x in low),
            rpmCutoffSteps=sum(x["EngineRpm"] >= 12000 for x in steps),
            baselineFailures=dict(collections.Counter(category(x["BaselineOutcome"]) for x in decisions if x["BaselineOutcome"])),
            full=dict(collections.Counter(category(x["FullOutcome"]) for x in decisions if not x["BaselineOutcome"])),
            sustained=dict(collections.Counter(category(x["SustainedFull"]["Outcome"]) for x in decisions if not x["BaselineOutcome"])),
            alternatives=sum(x["FullAlternative"] for x in decisions),
        )
    return aggregate


def figures(output, baseline, reachability, residual):
    colors = {"H25": "#515967", "H26-LE": "#b24620", "H27-P": "#176ca0"}
    fig, axes = plt.subplots(5, 2, figsize=(13, 12), sharex=True, layout="constrained")
    panels = [
        ("speed", "Prędkość [km/h]"), ("gas", "Aktywny gaz [0–1]"),
        ("rpm", "Wał [RPM]"), ("spin", "Poślizg wzdłużny [%]"),
        ("beta", "β [°]"), ("yaw", "Yaw [rad/s]"),
        ("front", "Fzf [N]"), ("rear", "Fzr [N]"),
        ("posture", "Przesunięcie COM zawodnika [cm]"), ("sweep", "Swept [m]"),
    ]
    for ax, (field, label) in zip(axes.flat, panels):
        for name, rows in baseline.items():
            ax.plot([row["s"] for row in rows], [row[field] for row in rows], color=colors[name],
                    label=name, linewidth=1.0, alpha=.84)
        if field == "spin":
            ax.set_yscale("symlog", linthresh=20)
        if field == "sweep":
            ax.axhline(.50, linestyle="--", color="black", linewidth=.8)
        ax.set_ylabel(label)
        ax.grid(alpha=.20)
    axes[0, 0].legend(loc="lower right", ncol=3)
    for ax in axes[-1]:
        ax.set_xlabel("Pozycja na linii referencyjnej [m]")
    fig.suptitle("Nominalne S3 · przyjęte kroki Box2D · osobne polityki", fontsize=15)
    fig.savefig(output / "telemetria.png", dpi=155)
    plt.close(fig)

    fig, axes = plt.subplots(1, 2, figsize=(11, 4.2), layout="constrained")
    for key, label in (("start-0.37", "Start 0,37 s"), ("196-irrevocable", "#196"), ("straight-2.60", "Prosta 2,60 s")):
        samples = residual[key]["Samples"]
        axes[0].plot([row["TrialAx"] for row in samples], [row["Residual"] for row in samples], label=label)
    axes[0].axhline(0, color="k", linewidth=.8)
    axes[0].set(xlabel="Próbne ax [m/s²]", ylabel="ax − Fₓ/m [m/s²]", title="Reszta sprzężenia nacisków")
    axes[0].legend()
    for law, style in (("lateral-first", "-"), ("combined-slip", "--")):
        sample = [row for row in reachability if row["Name"] == "196-irrevocable" and
                  "ImposedKappa" in row and row.get("Model") == law]
        axes[1].plot([row["Data"]["InstantSlipRatio"] for row in sample],
                     [row["Data"]["RearTorque"] for row in sample], style, marker="o", label=law)
    axes[1].set(xlabel="κ na wejściu do kroku", ylabel="Moment yaw tylnego koła [Nm]",
                title="#196 · 20% nacisku z przodu")
    axes[1].legend()
    for ax in axes:
        ax.grid(alpha=.2)
    fig.savefig(output / "reszta-i-poślizg.png", dpi=155)
    plt.close(fig)

    fig, axes = plt.subplots(1, 3, figsize=(12, 4.3), layout="constrained")
    cases = ("H26-LE-learned-C-min-front", "H26-LE-learned-C-alt-min-front", "196-irrevocable")
    titles = ("LE · minimum 4,954 N", "LE −12 cm · minimum 0,734 N", "#196 · stan sprzed review")
    for ax, case, title in zip(axes, cases, titles):
        rows = [x for x in reachability if x["Name"] == case and x.get("Model") == "lateral-first"
                and "ImposedKappa" not in x and x["Data"]["SteeringCommand"] == 0]
        gas = sorted({x["Data"]["Drive"] for x in rows})
        fractions = sorted({x["FrontFraction"] for x in rows})
        cells = {(x["FrontFraction"], x["Data"]["Drive"]): x["Data"]["YawMiss"] for x in rows}
        image = ax.imshow([[cells[z, u] for u in gas] for z in fractions], aspect="auto", origin="lower",
                          cmap="cividis", vmin=0, vmax=500)
        for i, z in enumerate(fractions):
            for j, u in enumerate(gas):
                value = cells[z, u]
                ax.text(j, i, f"{value:.1f}", ha="center", va="center", fontsize=7,
                        color="white" if value < 230 else "black")
        ax.set(xticks=list(range(len(gas))), xticklabels=[f"{u:.2g}" for u in gas],
               yticks=list(range(len(fractions))), yticklabels=[f"{z*100:.3g}" for z in fractions],
               xlabel="Gaz", title=title)
    axes[0].set_ylabel("Udział nacisku przodu [%]")
    fig.colorbar(image, ax=list(axes), label="Deficyt osiągalnego momentu yaw [Nm]")
    fig.savefig(output / "osiagalnosc.png", dpi=155)
    plt.close(fig)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("model", type=Path)
    parser.add_argument("native", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    geometry = read(CURVES)["geometry"]
    native, all_rows = {}, {}
    cases = {p.name.removesuffix(".json.gz"): p for p in args.native.glob("*.json.gz")}
    for name in ("H25-same", "H26-L-learned-C", "H26-E-learned-C", "H26-LE-same", "H26-LE-learned-C-alt"):
        cases[name] = ARCHIVE / f"{name}.json.gz"
    for name, path in sorted(cases.items()):
        record, driver, rows = native_rows(path)
        all_rows[name] = rows
        native[name] = native_summary(record, driver, rows, geometry)
    write(args.output / "native-summary.json", native)
    write(args.output / "decision-summary-model.json", audit_summary(args.model))
    figures(args.output, {name: all_rows[key] for name, key in (
        ("H25", "H25-same"), ("H26-LE", "H26-LE-learned-C"), ("H27-P", "H27-P-learned-C")
    )}, read(args.model / "reachability-model.json.gz"), read(args.model / "load-residual-model.json"))


if __name__ == "__main__":
    main()
