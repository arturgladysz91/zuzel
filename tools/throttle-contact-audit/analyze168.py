"""Compute paired measurements from the #168 native archives. Executes no worlds."""
from collections import defaultdict
import argparse
import cmath
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
from statistics import fmean
import time
from zipfile import ZipFile, ZIP_DEFLATED, ZIP_STORED

BASE = Path("docs/experiments/fast-two-corners/evidence.zip")
EPS = 1e-8


def load(path):
    return json.loads(path.read_bytes())


def digest(blob):
    return hashlib.sha256(blob).hexdigest()


def dot(a, b):
    return a["X"] * b["X"] + a["Y"] * b["Y"]


def pair(a, b):
    return {axis: (a[axis] + b[axis]) / 2 for axis in ("X", "Y")}


def at(trace, t):
    return next(q for q in trace if q["t"] >= t - EPS)


def trace(run, model):
    radius = model["Default"]["WheelRadiusMeters"]
    samples = run["Riders"][0]["Samples"]
    data = []
    for old, q in zip(samples, samples[1:]):
        t, t0 = q["Input"]["Time"], old["Input"]["Time"]
        dt = t - t0
        p = q["Powertrain"]
        f, w = q["Forces"], p["Wheel"]
        v, v0 = q["Input"]["Rider"], old["Input"]["Rider"]
        rear_velocity = f["RearVelocity"]
        u = dot(rear_velocity, f["BodyForward"])
        side = dot(rear_velocity, f["BodyLeft"])
        body_velocity = pair(v["Velocity"], v0["Velocity"])
        yaw = (v["AngularVelocity"] + v0["AngularVelocity"]) / 2

        def patch_mean(arm):
            return {"X": body_velocity["X"] - yaw * arm["Y"],
                    "Y": body_velocity["Y"] + yaw * arm["X"]}

        rear_mean = patch_mean(f["RearArm"])
        fx, fy = f["RearLongitudinal"], f["RearLateral"]
        loads = f["TyreLoads"]
        budget = loads["RearCapacityNewtons"]
        loss_x = fx * (radius * w["MidOmega"] - dot(rear_mean, f["BodyForward"])) * dt
        loss_rf = -fy * dot(rear_mean, f["BodyLeft"]) * dt
        loss_ff = -dot(f["FrontForce"], patch_mean(f["FrontArm"])) * dt
        loss_res = -dot(f["Resistance"], body_velocity) * dt
        energy = dict(combustion=p["CombustionWorkJoules"], net_kinetic=p["CombustionWorkJoules"] + q["EnergyExcessJoules"],
                      crank_drag=p["CrankDragWorkJoules"], clutch=p["ClutchHeatJoules"],
                      transmission=p["TransmissionLossJoules"], bearing=w["BearingLossJoules"],
                      tyre_x=loss_x, tyre_r_y=loss_rf, tyre_f_y=loss_ff, resistance=loss_res,
                      tyre_x_constitutive=w["LongitudinalLossJoules"],
                      crank_residual=p["EnergyResidualJoules"])
        losses = sum(energy[k] for k in ("crank_drag", "clutch", "transmission", "bearing",
                                          "tyre_x", "tyre_r_y", "tyre_f_y", "resistance"))
        energy["closure"] = energy["net_kinetic"] - energy["combustion"] + losses
        energy["wheel_kinetic"] = .5 * model["Default"]["WheelInertiaKgMetersSquared"] * (
            w["AfterOmega"] ** 2 - w["BeforeOmega"] ** 2)
        energy["crank_kinetic"] = .5 * model["Launch"]["CrankInertiaKgMetersSquared"] * (
            p["AfterOmega"] ** 2 - p["BeforeOmega"] ** 2)
        energy["body_kinetic"] = energy["net_kinetic"] - energy["crank_kinetic"] - energy["wheel_kinetic"]

        beta = math.degrees(v["SlideRadians"])
        beta_dot = ((beta - math.degrees(v0["SlideRadians"]) + 180) % 360 - 180) / dt
        data.append(dict(t=t, interval_start=t0, dt=dt, s=v["TotalReferenceMeters"],
                         x=v["Position"]["X"], y=v["Position"]["Y"], speed=v["Speed"] * 3.6,
                         acceleration=(v["Speed"] - v0["Speed"]) / dt,
                         ax=(f["NetForce"]["X"] * f["BodyForward"]["X"] + f["NetForce"]["Y"] * f["BodyForward"]["Y"]) / model["BodyMassKg"],
                         rpm_w=q["Input"]["Omega"] * 30 / math.pi, rpm_engine=q["CrankOmega"] * 30 / math.pi,
                         torque=w["DriveTorqueNewtonMeters"], crank_torque=p["CrankTorque"],
                         clutch_engagement=p["Engagement"], clutch_slip=p["SlipRadiansPerSecond"],
                         requested=q["RequestedDrive"], active=q["Input"]["Active"]["Drive"],
                         steer=math.degrees(q["Input"]["Steering"]), steer_request=math.degrees(q["RequestedSteering"]),
                         theta=math.degrees(v["Heading"]), chi=math.degrees(math.atan2(v["Velocity"]["Y"], v["Velocity"]["X"])),
                         beta=beta, beta_dot=beta_dot, yaw=v["AngularVelocity"], body_side=v["SideSpeed"],
                         road_side=dot(v["Velocity"], v["Road"]["Outward"]), alpha=math.degrees(math.atan2(side, u)),
                         rear_u=u, rear_w=side, kappa=w["InstantSlipRatio"],
                         slip_mps=radius * w["BeforeOmega"] - u, mid_slip=w["MidSlipVelocityMetersPerSecond"],
                         fx=fx, fy=fy, front_fy=f["FrontLateral"], front_torque=f["FrontTorque"],
                         rear_torque=f["RearTorque"], yaw_torque=f["NetTorque"], use=w["RearUtilization"],
                         rear_budget=budget, front_budget=loads["FrontCapacityNewtons"], total_budget=f["Budget"],
                         rear_fz=loads["RearNormalNewtons"], front_fz=loads["FrontNormalNewtons"],
                         fy_headroom=math.sqrt(max(0, budget ** 2 - fx ** 2)),
                         inner=q["Inner"], outer=q["Outer"], swept=q["Certificate"], energy=energy))
    return data


def interval(data, start, end):
    begin, finish = at(data, start), at(data, end)
    rows = [q for q in data if start + EPS < q["t"] <= finish["t"] + EPS]
    if not rows:
        raise ValueError("No measured interval after activation")
    dt = finish["t"] - begin["t"]
    accum = {k: sum(q["energy"][k] for q in rows) for k in rows[0]["energy"]}
    maxima = {f"max_{k}": max(abs(q[k]) for q in rows) for k in
              ("kappa", "alpha", "beta", "beta_dot", "yaw", "fx", "fy", "use", "slip_mps", "yaw_torque")}
    return dict(seconds=dt, start_kph=begin["speed"], end_kph=finish["speed"],
                end_reference=finish["s"], delta_kph=finish["speed"] - begin["speed"],
                mean_ax=(finish["speed"] - begin["speed"]) / (3.6 * dt),
                mean_kappa=sum(q["kappa"] * q["dt"] for q in rows) / sum(q["dt"] for q in rows),
                mean_fx=fmean(q["fx"] for q in rows), mean_abs_fy=fmean(abs(q["fy"]) for q in rows),
                min_swept=min(q["swept"] for q in rows), min_pose=min(min(q["inner"], q["outer"]) for q in rows),
                min_fy_headroom=min(q["fy_headroom"] for q in rows), energy=accum, **maxima)


def eigen(q, model):
    """Small beta/yaw perturbation about straight motion at frozen u and wheel slip."""
    u = q["rear_u"]
    m, inertia = model["BodyMassKg"], model["BodyInertiaKgMetersSquared"]
    lf, lr = model["Bike"]["FrontAxleFromComMeters"], model["Bike"]["RearAxleFromComMeters"]
    wheel = model["Default"]
    s2 = u*u + wheel["LowSpeedMetersPerSecond"] ** 2
    dy = math.hypot(.12 * math.sqrt(s2), q["total_budget"] / m * .03)
    dx = math.hypot(wheel["LongitudinalSlipStiffnessRatio"] * math.sqrt(s2),
                    q["rear_budget"] * (wheel["WheelRadiusMeters"] ** 2 / wheel["WheelInertiaKgMetersSquared"] + 1/m)
                    * wheel["MinimumContactRelaxationSeconds"])
    a = q["mid_slip"] / dx
    cf = q["front_budget"] * u / dy
    cr = q["rear_budget"] * u / (dy * math.sqrt(1 + a*a))
    cross = lf * cf - lr * cr
    m11, m12 = -(cf + cr + q["fx"]) / (m * u), 1 + cross / (m * u*u)
    m21, m22 = cross / inertia, -(lf*lf*cf + lr*lr*cr) / (inertia*u)
    disc = cmath.sqrt((m11 - m22)**2 + 4*m12*m21)
    eig = [(m11+m22 + sign*disc)/2 for sign in (1, -1)]
    return dict(t=q["t"], kappa=q["kappa"], front_slope_N_per_rad=cf, rear_slope_N_per_rad=cr,
                lambda_max_per_s=max(z.real for z in eig), eigenvalues=[[z.real, z.imag] for z in eig])


def history(evidence, model):
    metadata = [json.loads(line) for line in (evidence / "ledger.jsonl").read_bytes().splitlines()]
    result = {}
    with ZipFile(BASE) as archive:
        for row in (q for q in metadata if q["Event"] == "history"):
            name = row["name"]
            raw = archive.read(name + ".zip")
            assert digest(raw).upper() == row["ArchiveSha256"]
            with ZipFile(BytesIO(raw)) as nested:
                run = json.loads(nested.read("native.json"))
            data = trace(run, model)
            rider = run["Riders"][0]
            last_request = None
            increases = []
            for d in rider["Decisions"]:
                t, command = d["Choice"]["Time"], d["Choice"]["Action"]
                state = at(data, t)
                if 125 <= state["s"] <= 160 and last_request is not None:
                    increases.append((command["Drive"] - last_request, t, command["Drive"]))
                last_request = command["Drive"]
            increase, requested, drive = max(increases)
            activation = next(c["ActivationAt"] for c in rider["Activations"] if abs(c["ObservedAt"] - requested) < EPS)
            origin = at(data, activation)

            def event(predicate):
                row = next((q for q in data if q["t"] > activation + EPS and q["s"] <= 160 and predicate(q)), None)
                return None if row is None else dict(t=row["t"], s=row["s"])

            events = dict(request=dict(t=requested, s=at(data, requested)["s"], increase=increase, drive=drive),
                          activate=dict(t=activation, s=origin["s"]),
                          kappa=event(lambda q: q["kappa"] >= origin["kappa"] + .10),
                          fy_headroom=event(lambda q: q["fy_headroom"] <= .90 * origin["fy_headroom"]),
                          yaw=event(lambda q: abs(q["yaw"]) >= abs(origin["yaw"]) + .20),
                          beta=event(lambda q: abs(q["beta"]) >= abs(origin["beta"]) + 3),
                          dnf=dict(t=data[-1]["t"], s=data[-1]["s"]) if "beta45" in run["Outcome"] else None)
            result[name] = dict(trace=[q for q in data if 124.8 <= q["s"] <= 160.2], events=events)
    return result


def examine(evidence):
    model = load(evidence / "model.json")
    receipt = load(evidence / "run-receipt.json")
    trials = load(evidence / "runs.json")
    traces, rows = {}, []
    assert digest((evidence / "native.zip").read_bytes()).upper() == receipt["ArchiveSha256"]
    with ZipFile(evidence / "native.zip") as archive:
        assert archive.testzip() is None
        for trial in trials:
            name = trial["Id"]
            raw = json.loads(archive.read(name + ".json"))
            data = traces[name] = trace(raw, model)
            b, a, e = (trial[k] for k in ("BranchSeconds", "ActivationSeconds", "EndSeconds"))
            row = dict(trial)
            row["pre_activation"] = interval(data, b, a)
            row["post_activation"] = interval(data, a, e)
            if "beta45" in row["Outcome"]:
                status = "β45"
            elif "clearance" in row["Outcome"]:
                status = "swept <0,5 m"
            elif row["Outcome"] != "experiment timeout":
                status = row["Outcome"]
            elif trial["Experiment"] == "AB" and data[-1]["s"] >= model["StraightExitMeters"] - EPS:
                status = "koniec prostej"
            else:
                status = "koniec okna"
            row["status"] = status
            rows.append(row)

    by_group = defaultdict(list)
    for row in rows:
        key = (row["Cohort"], row["Steering"] if row["Experiment"] == "AB" else "C")
        by_group[key].append(row)
    common = {}
    for (cohort, steer), group in by_group.items():
        assert len({q["BranchSha256"] for q in group}) == 1
        assert len({q["PreActivationSha256"] for q in group}) == 1
        activation = group[0]["ActivationSeconds"]
        end = min(q["EndSeconds"] for q in group)
        # Event-split native clocks may straddle a geometry stop. Compare at
        # the last common regular 100 Hz timestamp preceding the first stop.
        end = math.floor((end + EPS) * 100) / 100
        common[f"{cohort}/{steer}"] = {q["Id"]: interval(traces[q["Id"]], activation, end) for q in group}

    stability = {}
    for kph in (60, 70, 80):
        for gas in (50, 100):
            name = f"AB-{kph}-zero-{gas}"
            a = next(q["ActivationSeconds"] for q in trials if q["Id"] == name)
            stability[name] = eigen(at(traces[name], a + .4), model)

    return model, receipt, rows, traces, common, stability, history(evidence, model)

def figures(out, model, trials, traces, historical, grid):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    plt.rcParams.update({"font.size": 9, "axes.spines.top": False, "axes.spines.right": False,
                         "figure.facecolor": "white", "axes.grid": True, "grid.alpha": .20})
    colors = ["#21678e", "#b74837", "#27846e", "#a96d27"]
    trial = {q["Id"]: q for q in trials}

    def dashboard(filename, names, labels, title):
        fig, axes = plt.subplots(5, 3, figsize=(14, 12), sharex=True, layout="constrained")
        fields = [
            ("Prędkość [km/h]", ["speed"]), ("a styczne [m/s²]", ["acceleration"]),
            ("Gaz [0–1]", ["active", "requested"]), ("Koło [obr./min]", ["rpm_w"]),
            ("Wał [obr./min]", ["rpm_engine"]), ("Moment koła [N·m]", ["torque"]),
            ("κ [-]", ["kappa"]), ("αr [°]", ["alpha"]), ("β [°]", ["beta"]),
            ("Fx tyłu [N]", ["fx"]), ("Fy tyłu i pułap |Fy| [N]", ["fy", "fy_headroom"]),
            ("Wykorzystanie tyłu [-]", ["use"]), ("Yaw [rad/s]", ["yaw"]),
            ("βdot [°/s]", ["beta_dot"]), ("Skręt [°]", ["steer", "steer_request"]),
        ]
        for ax, (label, keys) in zip(axes.flat, fields):
            for name, tag, color in zip(names, labels, colors):
                a = trial[name]["ActivationSeconds"]
                data = [q for q in traces[name] if q["t"] >= a - .255]
                xs = [q["t"] - a for q in data]
                for j, field in enumerate(keys):
                    ax.plot(xs, [q[field] for q in data], color=color, lw=1.35,
                            ls="--" if j else "-", label=tag if not j else None)
                if trial[name]["status"] == "β45":
                    ax.plot(xs[-1], data[-1][keys[0]], "x", color=color, ms=5)
            ax.axvline(0, color="#505761", lw=.8, ls=":")
            ax.set_ylabel(label)
        for ax in axes[-1]:
            ax.set_xlabel("Czas od aktywacji [s]")
        axes[0, 0].legend(loc="best", fontsize=8)
        fig.suptitle(title, fontsize=13)
        fig.savefig(out / filename, dpi=150)
        plt.close(fig)

    dashboard("168-prosta.png",
              ["AB-70-zero-50", "AB-70-zero-100", "AB-70-counter-50", "AB-70-counter-100"],
              ["Wprost 50%", "Wprost 100%", "Korekta 50%", "Korekta 100%"],
              "Prosta · ta sama prędkość wejścia 70,22 km/h · × oznacza β45")
    dashboard("168-wyjscie.png",
              ["C-167-original", "C-167-fixed-100", "C-167-fixed-50", "C-167-adaptive166-100"],
              ["#167 oryg.", "Ten sam skręt 100%", "Ten sam skręt 50%", "Skręt #166 100%"],
              "Wyjście z pierwszego łuku · dokładny natywny prefiks #167-H3")

    fig, axes = plt.subplots(5, 2, figsize=(13, 12), sharex="col", layout="constrained")
    keys = [("requested", "active", "Gaz żądany (przerywany) i aktywny"),
            ("kappa", "alpha", "κ [-] i αr [°]"),
            ("fx", "fy_headroom", "Fx i dostępne |Fy| [N]"),
            ("yaw", None, "Yaw [rad/s]"), ("beta", None, "β [°]")]
    for i, (field, other, label) in enumerate(keys):
        for j in range(2):
            ax = axes[i, j]
            alpha_ax = ax.twinx() if field == "kappa" else ax
            for (name, h), color in zip(historical.items(), colors):
                s = h["trace"]
                def x(row):
                    return row["s"] if j == 0 else row["t"] - s[0]["t"]
                ax.plot([x(q) for q in s], [q[field] for q in s], color=color,
                        ls="--" if field == "requested" else "-", label=name)
                if other:
                    alpha_ax.plot([x(q) for q in s], [q[other] for q in s], color=color, lw=1,
                                  ls="-" if field == "requested" else "--")
                for event, line in (("request", ":"), ("activate", "--"), ("dnf", "-")):
                    q = h["events"][event]
                    if q:
                        ax.axvline(x(q), color=color, alpha=.33, linestyle=line, linewidth=.85)
            ax.set_ylabel("κ" if field == "kappa" else label)
            if field == "kappa":
                alpha_ax.set_ylabel("αr [°] (przerywany)")
            if i == 0:
                ax.legend(fontsize=8)
    axes[-1, 0].set_xlabel("Położenie referencyjne [m]")
    axes[-1, 1].set_xlabel("Czas od ok. 125 m [s]")
    fig.suptitle("Archiwalne #166-H3 i #167-H3 · podgląd po położeniu oraz czasie", fontsize=13)
    fig.savefig(out / "168-h3.png", dpi=150)
    plt.close(fig)

    fig, axes = plt.subplots(1, 3, figsize=(14, 4), layout="constrained")
    for alpha, color in zip((0, 2, 6, 10, 20), [*colors, "#66599b"]):
        pts = [r for r in grid if r["Kph"] == 70 and r["AlphaDegrees"] == alpha and r["Acceleration"] == 0 and r["Height"] == .55]
        for ax, key in zip(axes, ("Fx", "Fy")):
            ax.plot([r["Kappa"] for r in pts], [abs(r[key]) for r in pts], color=color, label=f"αr {alpha}°")
            ax.set(xlabel="κ", ylabel=f"|{key}| [N]")
    axes[1].legend(fontsize=8)
    for ax_value, color in zip((0, 3, 6), colors):
        pts = [r for r in grid if r["Kph"] == 70 and r["AlphaDegrees"] == 6 and r["Kappa"] == .6 and r["Acceleration"] == ax_value]
        axes[2].plot([r["Height"] for r in pts], [r["RearBudget"] for r in pts], color=color, marker="o",
                     label=f"ax {ax_value} m/s²")
    axes[2].set(xlabel="Hipotetyczne h [m]", ylabel="Pojemność tyłu [N]")
    axes[2].legend(fontsize=8)
    fig.suptitle("Chwilowe prawo tylnego kontaktu i wrażliwość na obciążenie · 70 km/h", fontsize=13)
    fig.savefig(out / "168-opona.png", dpi=160)
    plt.close(fig)

    geometry = model["geometry"]
    sections = [q for seg in geometry["Segments"] for q in seg["Sections"]]
    fig, axes = plt.subplots(1, 2, figsize=(13, 5), layout="constrained")
    for ax, bounds, names in zip(axes, [(-27, 35), (124, 160)],
            [["AB-70-zero-50", "AB-70-zero-100", "AB-70-counter-50", "AB-70-counter-100"],
             ["C-167-original", "C-167-fixed-100", "C-167-fixed-50", "C-167-adaptive166-100"]]):
        for wall in ("Inner", "Outer"):
            chunks = []
            if bounds[0] < 0:
                chunks.append([q for q in sections if q["ReferenceMeters"] >= geometry["LapMeters"] + bounds[0]])
            chunks.append([q for q in sections if max(bounds[0], 0) <= q["ReferenceMeters"] <= bounds[1]])
            for chunk in chunks:
                ax.plot([q[wall]["X"] for q in chunk], [q[wall]["Y"] for q in chunk], color="#8f9ba7", lw=.9)
        for name, color in zip(names, colors):
            a = trial[name]["BranchSeconds"]
            data = [q for q in traces[name] if q["t"] >= a - EPS]
            ax.plot([q["x"] for q in data], [q["y"] for q in data], color=color, label=name)
            ax.scatter(data[-1]["x"], data[-1]["y"], color=color, marker="x" if trial[name]["status"] == "β45" else "o", s=25)
        ax.set(xlabel="X [m]", ylabel="Y [m]", aspect="equal")
        ax.legend(loc="best", fontsize=8)
    fig.suptitle("Rzeczywisty ruch XY od decyzji i bandy progressive-b")
    fig.savefig(out / "168-xy.png", dpi=155)
    plt.close(fig)


def num(value, digits=2):
    return "—" if value is None else f"{value:.{digits}f}".replace(".", ",")


def write_report(out, rows, common, stability, historical, grid, native_cpu):
    report = [
        "# Badanie #168 · pełny gaz, wheelspin i przyczepność", "",
        "**PASS diagnostyczny.** Pełny gaz daje napęd na idealnej prostej. Przy nawet małym skręcie "
        "obecny model traci stabilność boczną pod dużym obciążeniem napędowym. W H3 #167 decyzja o pełnym "
        "gazie zastała jeszcze małe β, ale istniejący yaw i boczny ruch kontaktu. Identyczne powtórzenia "
        "odseparowały skutki żądanego momentu od początkowego stanu i późniejszych komend skrętu.", "",
        "Bazą jest `c66024dfd5905f6120c224ebeeee60e8a866ba20`. Badanie jest odrębnym narzędziem "
        "PROVISIONAL uruchamianym jawną komendą. W pomiarach użyto oryginalnego Box2D, progressive-b, "
        "tej samej masy, silnika, sprzęgła, opon i nawierzchni oraz bramek β45 i swept 0,5 m.", "",
        "## Warunki porównania", "",
        "- **A i B1** korzystają z tych samych sześciu prób wprost. B2 trzyma żądanie +1°. "
        "B3 żąda +1° przez 0,4 s, −1° przez 0,4 s, a potem 0°. Kierownica reaguje przez oryginalny aktuator. "
        "Prosta przed kresą i za nią ma razem 62 m. Pozycja startowa to −25 m. Oryginalny inicjalizator "
        "spina początkowe prędkości nadwozia, koła i wału, po czym każdy motocykl przechodzi 0,8 s "
        "natywnego rozpędzania ze wspólnym żądaniem 50%. Prędkości przy rozwidleniu wynoszą 60,24, 70,22 i 80,06 km/h. "
        "Okno kończy się po 2 s od żądania, na końcu prostej albo przy pierwszej bramce.", "",
        "- **C** odtwarza każdy prefiks od zatrzymanego startu w osobnym, ciągłym świecie Box2D. "
        "Pierwsza decyzja przy s ≥ koniec łuku minus 4 m wyznacza rozwidlenie. To 129,05 m dla #166 "
        "i 129,75 m dla #167. Oryginalne polityki przeliczyły decyzje, a zapis stanu do rozwidlenia "
        "oraz dalszy przebieg ramienia oryginalnego zweryfikowano z historią do 11 ms przed końcem okna. W ramionach 50% i 100% "
        "składnik skrętu pochodzi z tych samych zarchiwizowanych decyzji. Osobno sprawdzono aktywne "
        "sprzężenie kierownicy #166 przy wymuszeniu pełnego gazu na obu stanach. Okno C trwa 1,4 s od żądania.", "",
        "Wszystkie pary miały zgodne hashe przy rozwidleniu, włącznie z aktywnymi komendami, pending "
        "i wałem. Fizyczne próbki do chwili aktywacji były identyczne. Kolejka zachowuje 0,24 s opóźnienia "
        "i review co 0,2 s. Polecenie skrętu z archiwum służy jako interwencja przyczynowa w C. "
        "Zmiana trajektorii po interwencji zmienia warunki dla dalszych poleceń.", "",
        "## Wyniki od aktywacji", "",
        "Prędkość początkowa w tabeli dotyczy rozwidlenia przed opóźnieniem. ΔV liczy się od aktywacji "
        "do indywidualnego końca. Maksima αr, β, yaw, Fx i Fy dotyczą wartości bezwzględnych. κ to "
        "stosunek poślizgu wzdłużnego w lokalnym kontakcie tylnym. Średnią κ ważono czasem kroku. "
        "Przyrosty prędkości przed aktywacją i wspólne okna par są w metrykach.", "",
        "| Scenariusz | V pocz. [km/h] | Gaz | ΔV [km/h] | Śr./max κ | Max αr [°] | Max β [°] | Max yaw [rad/s] | Max Fx [N] | Max Fy [N] | Status |",
        "|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---|",
    ]
    for row in rows:
        p = row["post_activation"]
        gas = "polityka" if row["Drive"] is None else f"{int(row['Drive'] * 100)}%"
        report.append(f"| {row['Id']} | {num(row['StartKph'])} | {gas} | {num(p['delta_kph'])} | "
                      f"{num(p['mean_kappa'],3)} / {num(p['max_kappa'],3)} | {num(p['max_alpha'])} | "
                      f"{num(p['max_beta'])} | {num(p['max_yaw'])} | {num(p['max_fx'],0)} | "
                      f"{num(p['max_fy'],0)} | {row['status']} · {num(p['seconds'],3)} s |")

    report += ["", "## A/B · prosta i małe korekty", "",
               "| V nominalna | Wspólny czas po aktywacji [s] | ΔV 50% [km/h] | ΔV 100% [km/h] | Śr. przysp. 100% [m/s²] | Maks. κ 100% |",
               "|---|---:|---:|---:|---:|---:|"]
    for kph in (60, 70, 80):
        q = common[f"{kph}/zero"]
        half, full = q[f"AB-{kph}-zero-50"], q[f"AB-{kph}-zero-100"]
        report.append(f"| {kph} | {num(full['seconds'],3)} | {num(half['delta_kph'])} | {num(full['delta_kph'])} | "
                      f"{num(full['mean_ax'])} | {num(full['max_kappa'],3)} |")
    report += [
        "", "W A/B1 pełny gaz przekazał 945–967 N maksymalnego Fx, jednocześnie utrzymując "
        "αr, β i yaw przy zerze. Wheelspin był znaczny i kosztował energię. Użyteczne przyspieszenie "
        "pełnego gazu przekroczyło 50% przy każdym wspólnym końcu porównania. W B2 i B3 przy 100% "
        "β przekroczyło 45° po 0,94–1,03 s. Przy 50% B2 kończy się bramką swept dla wszystkich trzech "
        "prędkości, z maksymalnym β 20–34°. B3 przy 50% ukończyło okno dla 60 i 70 km/h. W 80 km/h "
        "doszło do bramki swept tuż przed końcem prostej przy β maks. 11,24°. To rozróżnia "
        "odchylenie toru i utratę prześwitu od gwałtownego narastania β.", "",
        "Przykład 70 km/h, B3. W tej samej sekundzie po aktywacji 50% dało +12,15 km/h "
        "przy β maks. 3,14°, a 100% dało +8,91 km/h przy β maks. 46,12°. Osiągnięcie wysokiego "
        "Fx w początkowej fazie pełnego gazu nie przełożyło się na większy przyrost prędkości podczas korekty.", "",
        "![Prędkość, przyspieszenie, obroty i siły na prostej](168-prosta.png)", "",
        "## C · interwencje przy wyjściu", "",
        "W #166 z zachowanym skrętem zmiana z 50% na 100% podniosła maksymalne β z 4,09° do 45,09° "
        "w ciągu tego samego okna 0,87 s po aktywacji. Pełny gaz skrócił legalny przebieg, a przyrost "
        "prędkości spadł z 10,07 do 7,50 km/h. Sprzężenie kierownicy #166 przedłużyło przejazd z "
        "pełnym gazem o 0,12 s, do bramki β45. W #167 zachowanie jego skrętu i podstawienie 50% "
        "obniżyło β przy wspólnym końcu o około 20,7° względem oryginału i pozwoliło ukończyć okno. "
        "Maksymalne β w całym oknie nadal wyniosło 23,44°. Wymuszenie 100% z oryginalnym skrętem "
        "przyspieszyło DNF o 0,14 s. Sprzężenie kierownicy #166 przy pełnym gazie na stanie #167 "
        "osiągnęło tę samą bramkę po 1,00 s od aktywacji.", "",
        "Porównania pokazują wrażliwość na moment i skręt. Nie wyznaczają wyniku nieprzetestowanego "
        "sterownika, który wcześniej wygasiłby yaw. W dwóch rozwidleniach β było około 2°, więc "
        "duże wcześniejsze β nie jest warunkiem zaobserwowanej utraty kontroli.", "",
        "![Wyjście z łuku z dokładnego stanu #167](168-wyjscie.png)", "",
        "![Ruch XY i granice toru](168-xy.png)", "",
        "## Sekwencja zdarzeń w historycznym H3", "",
        "Żądanie oznacza największy dodatni skok gazu między 125 i 160 m. Kolejne zdarzenia "
        "wyznacza pierwszy wzrost κ o 0,10, spadek pułapu |Fy| przy zmierzonym Fx o 10%, "
        "wzrost |yaw| o 0,20 rad/s i wzrost |β| o 3° względem próbki aktywacji. Progi służą "
        "do opisu czasu. Kolejność chwil zależy od ich wartości. Pułap |Fy| oznacza geometryczny "
        "zapas z koła sił przy danym Fx. Rzeczywiste |Fy| może rosnąć wskutek rosnącego αr.", "",
        "| Zdarzenie | #166-H3 t / s [s / m] | #167-H3 t / s [s / m] |",
        "|---|---:|---:|"]
    for key, label in [("request", "Skok żądania"), ("activate", "Aktywacja"), ("kappa", "Wzrost κ"),
                       ("fy_headroom", "Spadek pułapu |Fy|"), ("yaw", "Wzrost |yaw|"),
                       ("beta", "Wzrost |β|"), ("dnf", "Utrata legalności")]:
        def value(name):
            e = historical[name]["events"][key]
            return f"{num(e['t'],3)} / {num(e['s'])}" if e else "—"
        report.append(f"| {label} | {value('166-H3')} | {value('167-H3')} |")
    report += [
        "", "W #167 gaz wzrósł z 0,387 do 1,000 w żądaniu, a po 0,24 s w napędzie. "
        "Wzrost Fx i κ ograniczył dostępną boczną rezerwę. Zanim β przekroczyło próg, rosnący "
        "αr wymusił duże rzeczywiste Fy. Koło zaczęło przeznaczać znaczną część dostarczonej "
        "pracy na poślizg, a yaw utrzymywał się mimo odjęcia gazu i kontry. Rozdzielenie "
        "przyczyn wynika z ramion C o tym samym stanie i skręcie oraz B od idealnej prostej.", "",
        "![Historia H3 względem położenia i czasu od 125 m](168-h3.png)", "",
        "## Prawo kontaktu, yaw i energia", "",
        "`NativeWorld166` wywołuje `Contact144.Forces`. `CrankClutch144.Prepare` "
        "rozwiązuje moment sprzęgła pasywnego razem z lokalnym korzeniem koła w "
        "`RearWheelDynamics.Evaluate`. Box2D całkuje nadwozie jeden raz, z siłami w dwóch "
        "punktach styku. `TwoContactBikeDynamics` opisuje analogiczny układ osi i aktuatora. "
        "Starsza gałąź `SlipCoupledDynamics.Forces` z bezpośrednim regulatorem yaw nie działa "
        "w tych próbach. Z tego modułu pochodzi skala boczna δy. Tylne Fx w osi nadwozia daje "
        "niemal zerowy moment wokół środka masy. Yaw powstaje przez różnicę momentów bocznych "
        "przodu i tyłu. Przód ma własny budżet Fy i kąt skrętu.", "",
        "Dla tylnego kontaktu ur i wr są lokalnymi składowymi prędkości punktu, z wkładem "
        "ωyaw × r. αr = atan2(wr, ur), β = θ − χ, κ = (Rωkoła − ur) / √(ur² + 0,5²). "
        "Natychmiastowy κ w zapisie należy do początku kroku. Fx i Fy używają poślizgu "
        "s w środku kroku. Z kołem Br = μFzr, a = s/δx, b = wr/δy obowiązuje", "",
        "```text",
        "Fx = Br a / sqrt(1 + a² + b²)",
        "Fy = −Br b / sqrt(1 + a² + b²)",
        "δx² = 0.10² (ur² + 0.5²) + [Br (R²/Iw + 1/m) 0.03]²",
        "δy² = 0.12² (ur² + 0.5²) + [(B/m) 0.03]²",
        "Iw Δω/Δt = Tdrive − R Fx − c ωmid",
        "```", "",
        "Fx rośnie monotonicznie do nasycenia, bez maksimum ze spadkiem. Przy ustalonym αr, "
        "Fx zwiększa się kosztem |Fy|. Przy ur 70 km/h i αr 2° wzrost κ z 0 do 0,60 "
        "zmniejsza |Fy| z 275 do 98 N. Przy αr 6° zmiana wynosi 652 → 285 N, przy αr 10° "
        "821 → 446 N. Dla αr 0° Fy = 0. Sztywność boczna małych zaburzeń zależy jednak od "
        "κ także wokół tego punktu. W tym samym stanie bezwładność Iw i Br zmieniają "
        "skalę konstytutywną δx, poza ich fizyczną rolą w równaniu ruchu koła. To zależność "
        "wskazana w #149. Składnik 3,824 m/s od podłogi relaksacyjnej przy 70 km/h przewyższa "
        "składnik prędkościowy 1,945 m/s. W nominalnym punkcie δx ≈ 4,29 m/s, ok. 2,2 razy "
        "składnik prędkościowy. To zwiększa κ potrzebne do danego Fx przy ustalonym ur i αr, "
        "ale nie obniża asymptotycznej pojemności siłowej Br.", "",
        "Zlinearyzowano tylko planarnego motocykla przy zamrożonych ur i poślizgu koła, "
        "0,4 s po aktywacji w A/B1. Nachylenie Cr = Br ur / (δy√(1+a²)). Cf jest "
        "analogicznym nachyleniem z własnym budżetem przodu i a = 0. Macierz dla [β, yaw] "
        "uwzględnia ramię osi i rzut Fx na normalną do kierunku ruchu. Dodatnia część rzeczywista "
        "wartości własnej oznacza wzrost małego zaburzenia w tym lokalnym przybliżeniu.", "",
        "| V nominalna | Cr 50% [N/rad] | Cr 100% [N/rad] | λmax 50% [1/s] | λmax 100% [1/s] |",
        "|---|---:|---:|---:|---:|"]
    for kph in (60, 70, 80):
        a, b = (stability[f"AB-{kph}-zero-{g}"] for g in (50, 100))
        report.append(f"| {kph} | {num(a['rear_slope_N_per_rad'],0)} | {num(b['rear_slope_N_per_rad'],0)} | "
                      f"{num(a['lambda_max_per_s'])} | {num(b['lambda_max_per_s'])} |")
    report += [
        "", "W 70 km/h przy 100% nachylenie tyłu spada o 61%, przy niemal stałym nachyleniu "
        "przodu około 7050 N/rad. To ilościowo wyjaśnia, czemu stan idealny zachowuje symetrię, "
        "a małe zaburzenie szybko narasta po obciążeniu napędem. Przybliżenie omija dynamikę "
        "sprzęgła i zmianę prędkości, a wyniki natywne mierzą efekt ich współdziałania.", "",
        "Energia obejmuje pracę spalania, zmianę energii nadwozia, koła i wału, ciepło sprzęgła, "
        "transmisję, opór wału i łożyska, opory ruchu i pracę poślizgu obu kontaktów. Straty "
        "opon oszacowano z rzeczywistych średnich zaakceptowanych prędkości w kroku i przyłożonych "
        "sił. Osobno zachowano stratę wzdłużną z wewnętrznego rozwiązania koła. Dla faz po aktywacji "
        f"maksymalne niedomknięcie całego bilansu wyniosło {num(max(abs(q['post_activation']['energy']['closure']) for q in rows),3)} J. "
        "Wspólny B1 przy 70 km/h daje stratę wzdłużną 4,18 kJ przy 50% i 20,62 kJ przy 100%. "
        "Wspólny B3 przez 1 s to 2,57 i 12,43 kJ. Bilans sprawdza spójność numeryczną. "
        "Empiryczną wiarygodność krzywych ocenią pomiary opony na żużlu.", "",
        "## Transfer obciążenia i model zawodnika", "",
        "`LoadBasedTyreGrip.Budget` wylicza statyczne Fz z masy i ramion 0,70/0,60 m. "
        "Dla m = 147 kg i μ = 1,2845 tył ma 776,5 N nacisku i 997,4 N budżetu, a przód "
        "665,6 N i 854,9 N budżetu. `DynamicAxleLoad` istnieje jako osobny eksperyment "
        "quasi-statyczny. `Contact144` wyklucza włączenie go razem z aktywną gałęzią silnika "
        "500 cm³ lub statycznym `LoadBasedTyreGrip`. W obecnym przebiegu sprzęgło nie zmienia "
        "nacisku. Zawodnik należy do jednego sztywnego ciała z ustalonym COM i bezwładnością. "
        "Brak stanu pozycji jego ciała, przechyłu, pochylenia, ugięcia podłoża i siły camber. "
        "Zmiana ułożenia zawodnika może zmieniać obciążenia w rzeczywistości. Wheelspin w tym "
        "modelu wywołuje nierównowaga momentu napędu i reakcji kontaktu.", "",
        "Czułość wyliczono z ΔFz = m·ax·h/L dla L = 1,30 m. Wysokości 0,35/0,55/0,75 m "
        "to warianty analityczne. Br = μ(Fzr + ΔFz), a budżet przodu maleje o tę samą μΔFz. "
        "Tabela stosuje aktualne prawo kontaktu bez całkowania świata przy ur 70 km/h, κ 0,60 i αr 6°.", "",
        "| ax [m/s²] | h [m] | ΔFz tyłu [N] | Br [N] | Fx [N] | Fy bez znaku [N] |",
        "|---:|---:|---:|---:|---:|---:|"]
    for x in grid:
        if x["Kph"] == 70 and x["Kappa"] == .6 and x["AlphaDegrees"] == 6 and (x["Acceleration"] != 0 or x["Height"] == .55):
            report.append("| " + " | ".join(num(v, n) for v, n in [
                (x["Acceleration"],0), (x["Height"],2), (x["LoadTransferNewtons"],0), (x["RearBudget"],0),
                (x["Fx"],0), (abs(x["Fy"]),0)]) + " |")
    report += [
        "", "Większy nacisk zwiększa dostępny tylny Fx i Fy w tej sondzie, a zmniejsza zdolność "
        "przodu. Zmienia też regularizację δx w obecnym prawie. Sama czułość nie rozstrzyga "
        "natywnej stabilności po dodaniu transferu. Przykładowe 6 m/s² mieści się w zmierzonym "
        "rzędzie wielkości przyspieszeń na prostej przy 100%.", "",
        "![Charakterystyka i analityczna czułość nacisku](168-opona.png)", "",
        "## Następny etap · ścieżka B", "",
        "Obie przyczyny współwystępują. #167 wymusił wysoki moment podczas niedogaszonego "
        "yaw, a obecne prawo kontaktu silnie obniża małosygnałową zdolność stabilizacji tyłu "
        "nawet po zaburzeniu idealnej prostej. Wyniki 100% ze sprzężeniem kierownicy #166 "
        "wzmacniają potrzebę weryfikacji prawa opony. Wielkość sprzężenia jest zmierzona "
        "w symulatorze. Jej adekwatność do nawierzchni żużlowej wymaga pomiaru.", "",
        "Następny eksperyment powinien zidentyfikować izolowaną mapę Fx/Fy z opony żużlowej "
        "na reprezentatywnym podłożu. Zmierzyć równocześnie ur, wr, ω, Fx, Fy i Fz przy "
        "60–80 km/h, zmieniając κ około 0–1, αr około 0–15° i nacisk w zakresie z analizy "
        "wrażliwości. Oddzielić sztywność materiałową od bezwładności w solverze. "
        "Potem porównać oryginał i eksperymentalną mapę na tej samej macierzy A/B/C, "
        "z osobno kontrolowanym transferem obciążenia i zamrożonymi komendami. Mierzyć "
        "nachylenia Fy blisko αr = 0, zmianę stateczności, pracę poślizgu i legalne tempo, "
        "obok błędu sił na odłożonych punktach pomiarowych. Publikacja Bartolozziego i "
        "współautorów pokazuje metodykę identyfikacji opony motocyklowej z pomiarów jazdy "
        "i porównań stanowiskowych. Jej współczynniki nie kalibrują żużla. "
        "[Bartolozzi i in., pomiary opon motocyklowych](https://doi.org/10.1177/09544070241247239)", "",
        "## Dowody i budżet", "",
        f"26 nowych rolloutów (18 A/B, 8 C), z pełnymi natywnymi prefiksami. Uruchomienia i "
        f"zapis dowodów zużyły {num(native_cpu,3)} CPU-s. Dodatkowe sondy sił są bezkrokowymi "
        "wywołaniami prawa kontaktu, ujętymi w tym koszcie. Pomiar analizy i weryfikacji "
        "dowodów znajduje się w `analysis-receipt.json` wewnątrz pakietu. Lokalne i zdalne testy "
        "zgodności uruchomiono osobno od budżetu eksperymentalnego.", "",
        "Pakiet zawiera natywne przebiegi, metryki przed i po aktywacji, wspólne okna, "
        "moment i rezerwę Fx/Fy, pełny bilans energii, siatkę kontaktu, geometrię, rejestr "
        "oraz SHA256. Przebiegi C wykorzystują istniejące archiwa H3 jako źródło porównań.", "",
    ]
    (out / "REPORT.md").write_text("\n".join(report), encoding="utf-8", newline="\n")


def main():
    before = time.process_time()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("out", type=Path)
    args = parser.parse_args()
    e, out = args.evidence, args.out
    out.mkdir(parents=True, exist_ok=True)
    model, receipt, rows, traces, common, stability, historical = examine(e)
    grid = load(e / "contact-grid.json")
    model["geometry"] = load(e / "geometry.json")
    figures(out, model, rows, traces, historical, grid)
    write_report(out, rows, common, stability, historical, grid, receipt["CpuSeconds"])
    summary = dict(trials=rows, matched_intervals=common, local_stability=stability,
                   historical_windows=historical,
                   model={k: v for k, v in model.items() if k != "geometry"})
    raw_summary = (json.dumps(summary, indent=2) + "\n").encode()
    analysis_receipt = dict(native_cpu_seconds=receipt["CpuSeconds"],
                            analysis_cpu_seconds=time.process_time() - before,
                            rollouts=receipt["Rollouts"], user_limit_cpu_seconds=250)
    code = Path("tools/throttle-contact-audit")
    sources = {p.as_posix(): digest(p.read_bytes()) for p in sorted(code.iterdir()) if p.suffix in (".cs", ".csproj", ".py")}
    package = out / "168-dowody.zip"
    with ZipFile(package, "w", compression=ZIP_DEFLATED, compresslevel=6) as z:
        for path in sorted(e.iterdir()):
            if path.is_file() and path.name != ".study.lock":
                z.write(path, path.name, compress_type=ZIP_STORED if path.suffix == ".zip" else ZIP_DEFLATED)
        z.writestr("measurements.json", raw_summary)
        z.writestr("sources.json", json.dumps(sources, indent=2))
        analysis_receipt["analysis_cpu_seconds"] = time.process_time() - before
        analysis_receipt["combined_cpu_seconds"] = receipt["CpuSeconds"] + analysis_receipt["analysis_cpu_seconds"]
        assert analysis_receipt["combined_cpu_seconds"] < 250
        z.writestr("analysis-receipt.json", json.dumps(analysis_receipt, indent=2))
    with ZipFile(package) as z:
        assert z.testzip() is None
    products = [p for p in sorted(out.iterdir()) if p.is_file() and p.name not in (".gitattributes", "SHA256SUMS")]
    (out / "SHA256SUMS").write_text("".join(f"{digest(p.read_bytes())}  {p.name}\n" for p in products),
                                    encoding="ascii", newline="\n")
    print(json.dumps(analysis_receipt, indent=2))


if __name__ == "__main__":
    main()
