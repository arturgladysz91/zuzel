"""Build full trace tables, matched comparisons and figures from sealed receipts."""
import argparse
import csv
import gzip
import io
import json
from pathlib import Path
from statistics import median
import sys
from zipfile import ZipFile

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

from verify177 import OLD, ROOT, audit, read

sys.path.insert(0, str(ROOT / "tools/track-grip-root-cause"))
from analyze175 import native_trace, window

plt.rcParams.update({"axes.spines.top": False, "axes.spines.right": False, "font.size": 9, "figure.dpi": 145})


def cross(q, s):
    return float(np.interp(s, q["s"], q["t"]))


def val(q, key, time):
    return float(np.interp(time, q["t"], q[key]))


def extent(q, a, b):
    return (q["t"] >= a) & (q["t"] <= b)


def trace(run):
    q = native_trace(run)
    raw = run["Riders"][0]["Samples"]
    q.update(offset=np.array([p["Input"]["Rider"]["Road"]["OffsetMeters"] for p in raw]),
             course=np.unwrap([np.arctan2(p["Input"]["Rider"]["Velocity"]["Y"], p["Input"]["Rider"]["Velocity"]["X"])
                               for p in raw]),
             course_error=np.array([np.arctan2(np.sin(p["Input"]["Rider"]["Heading"] -
                 p["Input"]["Rider"]["SlideRadians"] - p["Input"]["Rider"]["Road"]["Heading"]),
                 np.cos(p["Input"]["Rider"]["Heading"] - p["Input"]["Rider"]["SlideRadians"] -
                        p["Input"]["Rider"]["Road"]["Heading"])) for p in raw]),
             steer_request=np.degrees([p["RequestedSteering"] for p in raw]),
             steer_active=np.degrees([p["Input"]["Active"]["SteeringRadians"] for p in raw]),
             inner=np.array([p["Inner"] for p in raw]), outer=np.array([p["Outer"] for p in raw]),
             front_budget=np.array([p["Forces"]["TyreLoads"]["FrontCapacityNewtons"] for p in raw[1:]]))
    return q


def stats(q, a, b):
    mask = extent(q, a, b)
    dt = np.maximum(0, np.minimum(q["t"][1:], b) - np.maximum(q["t"][:-1], a))
    energy = window(q, a, b)["energy_J"]
    return dict(seconds=b-a, distance=val(q, "dist", b)-val(q, "dist", a),
                min_pose=float(q["pose"][mask].min()), min_sweep=float(np.nanmin(q["swept"][mask])),
                vmax_kph=float(q["speed"][mask].max()*3.6), max_beta_deg=float(abs(q["beta"][mask]).max()),
                max_yaw_rad_s=float(abs(q["yaw"][mask]).max()), wheelspin_max=float(abs(q["kappa"][dt>0]).max()),
                drive95_s=float(dt[q["gas"][1:]>=.95].sum()),
                drive100_s=float(dt[q["gas"][1:]>=1-1e-8].sum()), energy=energy,
                slip_J=energy["wheel_slip"]+energy["front_lateral"]+energy["rear_lateral"])


def bend_stats(q, cuts):
    if q["s"][-1] < cuts[-1]:
        return None
    times = [cross(q, s) for s in cuts]
    a, b = times[0], times[-1]
    mask = extent(q, a, b)
    indices = np.flatnonzero(mask)
    peak = indices[np.argmax(abs(q["k_xy"][mask]))]
    apex = indices[np.argmin(q["offset"][mask])]
    entry30 = cross(q, cuts[0]+.30*(cuts[-1]-cuts[0]))
    return dict(**stats(q, a, b), entry_kph=val(q, "speed", a)*3.6, at30_kph=val(q, "speed", entry30)*3.6,
                min_kph=float(min(q["speed"][mask])*3.6), exit_kph=val(q, "speed", b)*3.6,
                max_abs_k=float(abs(q["k_xy"][peak])), peak_k_s=float(q["s"][peak]),
                peak_k_fraction=float((q["s"][peak]-cuts[0])/(cuts[-1]-cuts[0])),
                peak_xy=q["xy"][peak].tolist(), peak_radius=float(1/abs(q["k_xy"][peak])),
                apex_offset=float(q["offset"][apex]), apex_fraction=float((q["s"][apex]-cuts[0])/(cuts[-1]-cuts[0])),
                section_seconds=np.diff(times).tolist())


def bends(q, geometry):
    starts, length = geometry["Starts"], geometry["LapMeters"]
    return [dict(lap=l+1, bend=j+1, **result)
            for l in range(3) for j, index in enumerate((1, 5))
            if (result := bend_stats(q, np.array(starts[index:index+4])+l*length)) is not None]


def store_trace(writer, name, q, geometry):
    starts = np.array(geometry["Starts"])
    length = geometry["LapMeters"]
    energy = q["energy"]
    for i in range(1, len(q["t"])):
        s = q["s"][i]
        lap = int(np.floor(s / length))
        section = int(np.searchsorted(starts, s-lap*length, side="right")-1)
        j = i-1
        dt = q["t"][i]-q["t"][j]
        fy_capacity = np.sqrt(max(0, q["wheel_capacity"][j]**2-q["fx"][j]**2))
        writer.writerow([name, q["t"][i], s, q["dist"][i], lap+1, section, *q["xy"][i], q["offset"][i],
                         q["inner"][i], q["outer"][i], q["swept"][i], q["heading"][i], q["course"][i],
                         q["beta"][i], q["yaw"][i], q["k_xy"][i], 1/abs(q["k_xy"][i]) if abs(q["k_xy"][i])>1e-7 else "",
                         q["requested"][i], q["gas"][i], q["steer_request"][i], q["steer_active"][i], q["steering"][i],
                         q["speed"][i]*3.6, q["kappa"][j], 0, q["fx"][j], q["ff"][j], q["fy"][j],
                         q["front_budget"][j]-abs(q["ff"][j]), fy_capacity-abs(q["fy"][j]),
                         q["cap"][j], q["along_force"][j]/147, q["actual_normal"][j],
                         (energy["front_lateral"][j]+energy["rear_lateral"][j])/dt,
                         energy["wheel_slip"][j]/dt, energy["combustion"][j]/dt])


def plot(plt_dir, geometry, probe, development, validation, pairs):
    colors = dict(K4="#45566b", NEW="#b9381c")
    aliases = (("BASE K4", colors["K4"]), ("NEW L1", colors["NEW"]))
    selected = next(x["Screen"] for x in probe["Cases"] if x["Id"] == "E")
    bend = selected["Bend"]
    ref = development["original"]
    new = development["winner"]
    start = selected["Reference"]
    end = bend["Exit"] + 20
    case_curves = [c["Screen"] for c in probe["Cases"]]
    fig, axes = plt.subplots(1, 2, figsize=(11, 4.7), constrained_layout=True)
    edge_sections = [q for segment in geometry["Geometry"]["Segments"] for q in segment["Sections"]]
    for key in ("Inner", "Outer"):
        edge = [q[key] for q in edge_sections]
        axes[0].plot([v["X"] for v in edge], [v["Y"] for v in edge], color="#95999f", lw=.75)
    for (label, color), q in zip(aliases, (ref, new)):
        sel = (q["s"] >= start) & (q["s"] <= end+.15)
        axes[0].plot(*q["xy"][sel].T, lw=1.8, color=color, label=label)
    for line in case_curves[0]["Lines"]:
        data = line["Points"]
        axes[1].plot([x["S"]-line["Bend"]["Entry"] for x in data],
                     [x["K"] for x in data], label=line["Line"]["Id"])
    axes[0].set(aspect="equal", xlabel="X [m]", ylabel="Y [m]", title="Natywna próba E")
    axes[1].set(xlabel="Postęp od wejścia [m]", ylabel="Krzywizna [1/m]", title="Linie geometryczne ze stanu X1")
    axes[1].axvspan(0, bend["Exit"]-bend["Entry"], color=".93", zorder=-1)
    for ax in axes:
        ax.legend(frameon=False, fontsize=8)
        ax.grid(alpha=.20)
    fig.savefig(plt_dir/"01-trajektorie.png", bbox_inches="tight")
    plt.close(fig)

    fig, axs = plt.subplots(4, 2, figsize=(12, 10), sharex=True, constrained_layout=True)
    for (label, color), q in zip(aliases, (ref, new)):
        x = q["s"]-bend["Entry"]
        valid = (q["s"]>=start) & (q["s"]<=end+.15)
        mask = valid[1:]
        def draw(ax, y, interval=False, style="-", legend=label):
            use = mask if interval else valid
            ax.plot((x[1:] if interval else x)[use], y[use], style, label=legend, color=color, lw=1)
        draw(axs[0,0], q["speed"]*3.6)
        draw(axs[0,1], q["requested"]*100, style="--", legend=label+" żąd.")
        draw(axs[0,1], q["gas"]*100, legend=label+" akt.")
        draw(axs[1,0], q["steer_request"], style="--", legend=label+" żąd.")
        draw(axs[1,0], q["steering"], legend=label+" wyk.")
        draw(axs[1,1], np.degrees(q["heading"]), legend=label+" heading")
        draw(axs[1,1], q["beta"], style="--", legend=label+" β")
        draw(axs[2,0], q["fx"], True, legend=label+" Fx tył")
        draw(axs[2,0], q["fy"], True, "--", label+" Fy tył")
        draw(axs[2,0], q["ff"], True, ":", label+" Fy przód")
        draw(axs[2,1], q["kappa"], True, legend=label+" κ")
        draw(axs[2,1], q["yaw"], style="--", legend=label+" yaw")
        draw(axs[3,0], q["swept"], legend=label+" swept")
        draw(axs[3,0], q["pose"], style="--", legend=label+" pose")
        energy = q["energy"]
        losses = (energy["wheel_slip"]+energy["front_lateral"]+energy["rear_lateral"])*mask
        draw(axs[3,1], np.cumsum(losses)/1000, True)
    titles = ["Prędkość [km/h]", "Gaz [%]", "Skręt [°]", "Heading i β [°]", "Siły kontaktów [N]",
              "Wheelspin κ i yaw [rad/s]", "Prześwit [m]", "Skumulowana praca poślizgu [kJ]"]
    for ax, title in zip(axs.flat, titles):
        ax.set_title(title, loc="left")
        ax.grid(alpha=.2)
        ax.axvspan(0, bend["Exit"]-bend["Entry"], color=".94", zorder=-1)
        ax.legend(frameon=False, fontsize=7, ncol=2)
    axs[3,0].axhline(.5, color="black", lw=.7, ls=":")
    for ax in axs[-1]:
        ax.set_xlabel("Postęp od wejścia [m]")
    fig.savefig(plt_dir/"02-proba-E.png", bbox_inches="tight")
    plt.close(fig)

    if validation:
        fig, axs = plt.subplots(2, 1, figsize=(11, 7), constrained_layout=True)
        for idx, ((label, color), suffix) in enumerate(zip(aliases, ("BASE", "NEW"))):
            for p in pairs:
                q = validation[(p["id"], suffix)]
                axs[0].plot(q["s"], 3.6*q["speed"], color=color, alpha=.80 if p["id"]=="V1" else .26,
                            lw=1 if p["id"]=="V1" else .65, label=label if p["id"]=="V1" else None)
            x = np.arange(len(pairs))+.28*idx
            axs[1].bar(x, [p[suffix]["finish_s"] or 0 for p in pairs], width=.28, color=color, label=label)
        for l in range(3):
            for k in (1, 5):
                axs[0].axvspan(l*geometry["LapMeters"]+geometry["Starts"][k],
                               l*geometry["LapMeters"]+geometry["Starts"][k+3], color=".94", zorder=-1)
        axs[0].set(xlabel="Postęp referencyjny [m]", ylabel="Prędkość [km/h]", title="Pięć par S3 i rezerwa")
        axs[1].set(ylabel="Czas S3 [s]", xticks=np.arange(len(pairs))+.14, xticklabels=[p["id"] for p in pairs])
        for ax in axs:
            ax.grid(axis="y", alpha=.20)
            ax.legend(frameon=False)
        fig.savefig(plt_dir/"03-S3.png", bbox_inches="tight")
        plt.close(fig)

    fig, ax = plt.subplots(figsize=(10, 4), constrained_layout=True)
    named = ["L1-late", "L2-smooth", "L3-open", "L4-wide"]
    for i, c in enumerate(case_curves):
        for p in c["Paths"][1:]:
            y = named.index(p["Line"]) + .12*i
            x = p["EndS"]-c["Bend"]["Entry"]
            if p["Seconds"] is None:
                ax.scatter(x, y, color="#a9271e" if p["Sweep"]<.6 else "#7d4b9e", marker="x", s=35)
            else:
                ax.scatter(x, y, facecolors="none", edgecolors="#335d52", s=27)
    ax.axvline(0, color=".5", lw=.8)
    ax.axvline(bend["Exit"]-bend["Entry"], color=".5", lw=.8)
    ax.set(yticks=np.arange(len(named))+.18, yticklabels=named,
           xlabel="Postęp od wejścia [m]", title="Końce prognoz v2 · prześwit × czerwony, β × fioletowy, cel ○")
    ax.grid(axis="x", alpha=.2)
    fig.savefig(plt_dir/"04-odrzucone-prognozy.png", bbox_inches="tight")
    plt.close(fig)


def main(evidence, out):
    out.mkdir(parents=True, exist_ok=True)
    (out/"plots").mkdir(exist_ok=True)
    geometry = json.loads((evidence/"geometry.json").read_bytes())
    probe = json.loads((evidence/"probe-v2.json").read_bytes())
    historical = []
    dev = []
    pairs = {}
    val_traces = {}
    validations = []
    with gzip.open(out/"telemetria.csv.gz", "wt", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(("id", "time_s", "reference_m", "distance_m", "lap", "segment0", "x_m", "y_m", "offset_m",
                         "inner_m", "outer_m", "swept_m", "heading_rad", "course_rad", "beta_deg", "yaw_rad_s",
                         "curvature_1_m", "radius_m", "gas_requested", "gas_active", "steer_requested_deg",
                         "steer_active_deg", "steer_actual_deg", "speed_kph", "wheelspin_ratio", "front_fx_N",
                         "rear_fx_N", "front_fy_N", "rear_fy_N", "front_reserve_N", "rear_fy_reserve_N",
                         "normal_capacity_mps2", "a_along_mps2", "a_normal_mps2", "lateral_slip_W", "wheel_slip_W",
                         "combustion_W"))
        with ZipFile(OLD/"172-validation-K4.zip") as outer:
            for name in sorted(outer.namelist()):
                if not name.endswith(".zip"):
                    continue
                with ZipFile(io.BytesIO(outer.read(name))) as archive:
                    run = read(archive, "native")
                    q = trace(run)
                    bend_list = bends(q, geometry)
                    assert len(bend_list)==6
                    historical.append(dict(id=Path(name).stem, bends=bend_list, finish_s=run["Crossings"][-1]["Time"]))
                    store_trace(writer, Path(name).stem, q, geometry)
        with ZipFile(OLD/"172-development.zip") as z, ZipFile(io.BytesIO(z.read("c3b-K4-D1.zip"))) as a:
            original = trace(read(a, "native"))
        plots = {"original": original}
        for path in sorted(evidence.glob("dev-*.zip")):
            with ZipFile(path) as archive:
                trial = read(archive, "trial")
                run = read(archive, "native")
                q = trace(run)
                where = next(c["Screen"]["Bend"] for c in probe["Cases"] if c["Id"] == trial["Case"])
                cuts = np.linspace(where["Entry"], where["Exit"], 4)
                base_time, new_time = trial["BaseGoal"], trial["NewGoal"]
                branch = trial["BranchTime"]
                row = dict(id=path.stem, trial=trial, base=stats(original, branch, base_time),
                           new=stats(q, branch, new_time or q["t"][-1]),
                           base_bend=bend_stats(original, cuts), new_bend=bend_stats(q, cuts),
                           dnfs=[] if trial["Outcome"]["ReachedStop"] else [
                               dict(time=float(q["t"][-1]), s=float(q["s"][-1]), xy=q["xy"][-1].tolist(),
                                    reason=trial["Outcome"]["Reason"])])
                dev.append(row)
                store_trace(writer, path.stem, q, geometry)
                if trial["Case"] == "E" and trial["LocalPass"]:
                    plots["winner"] = q
        for path in sorted(evidence.glob("held-*.zip")):
            with ZipFile(path) as archive:
                trial = read(archive, "trial")
                run = read(archive, "native")
                verify = audit(run, read(archive, "decisions"))
                q = trace(run)
                outcome = trial["Outcome"]
                finish = outcome["FinishTime"]
                lap_times = np.diff([0]+[c["Time"] for c in run["Crossings"]]).tolist()
                at = finish or q["t"][-1]
                phase = np.mod(q["s"], geometry["LapMeters"])
                starts = geometry["Starts"]
                straight = (phase<starts[1]) | ((phase>=starts[4]) & (phase<starts[5])) | (phase>=starts[8])
                valid = outcome["S3"] and outcome["Reserve"] and outcome["ReachedStop"]
                m = stats(q, 0, at)
                screens = read(archive, "screens")
                row = dict(id=path.stem, trial=trial, finish_s=finish if valid else None, lap_s=lap_times, legal=valid,
                           max_straight_kph=float(q["speed"][straight & (q["t"]<=at)].max()*3.6),
                           mean_actual_kph=m["distance"]/at*3.6, summary=m, bends=bends(q, geometry), audit=verify,
                           screens=len(screens), admitted=[dict(t=x["Time"], s=x["Reference"], line=x["Chosen"],
                           model_s={p["Line"]:p["Seconds"] for p in x["Paths"]}) for x in screens if x["Chosen"]],
                           dnf=None if valid else dict(time=float(q["t"][-1]), s=float(q["s"][-1]),
                                                      xy=q["xy"][-1].tolist(), reason=outcome["Reason"]))
                ident = path.stem.split("-")[1]
                pairs.setdefault(ident, dict(id=ident))[trial["Arm"]] = row
                val_traces[(ident, trial["Arm"])] = q
                validations.append(row)
                store_trace(writer, path.stem, q, geometry)

    for p in pairs.values():
        a, b = p["BASE"]["finish_s"], p["NEW"]["finish_s"]
        p["gain_s"] = a-b if a and b else None
        p["gain_pct"] = 100*(a-b)/a if a and b else None
    med = {arm: median(values) if (values := [p[arm]["finish_s"] for p in pairs.values()
                                             if p[arm]["finish_s"] is not None]) else None for arm in ("BASE", "NEW")}
    counts = {arm: sum(p[arm]["legal"] for p in pairs.values()) for arm in ("BASE","NEW")}
    gain = 100*(med["BASE"]-med["NEW"])/med["BASE"] if all(med.values()) and len(pairs)==counts["BASE"]==counts["NEW"]==5 else None
    summary = dict(historical=historical, development=dev, pairs=list(pairs.values()), medians_s=med,
                   legal=counts, median_gain_pct=gain,
                   worlds=[json.loads(p.read_bytes()) for p in sorted(evidence.glob("*.reserved.json"))])
    (out/"summary177.json").write_text(json.dumps(summary, indent=2, allow_nan=False)+"\n", encoding="utf-8")
    plot(out/"plots", geometry, probe, plots, val_traces, list(pairs.values()))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("evidence", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    main(args.evidence, args.output)
