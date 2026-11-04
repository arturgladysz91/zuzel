"""Read-only analysis and evidence packaging for #169."""
import argparse
from collections import Counter, defaultdict
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
import sys
import time
from zipfile import ZipFile, ZIP_DEFLATED, ZIP_STORED

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "throttle-contact-audit"))
from analyze168 import at, dot, interval, trace

SOURCE = Path("tools/full-drive-stability")
REPORT = Path("docs/experiments/full-drive-stability/REPORT.md")
BASE_ARCHIVE = Path("docs/experiments/throttle-contact-audit/168-dowody.zip")
EPS = 1e-8


def load(path):
    return json.loads(path.read_bytes())


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read_native(z, name, model):
    run = json.loads(z.read(name + ".json"))
    curve = trace(run, model)
    samples = run["Riders"][0]["Samples"]
    max_crank_root = 0
    max_wheel_root = 0
    min_dissipation = 0
    max_force_jump = 0
    for q, raw in zip(curve, samples[1:]):
        inputs, forces, power = raw["Input"], raw["Forces"], raw["Powertrain"]
        loads = forces["TyreLoads"]
        q["offset"] = inputs["Rider"]["Road"]["OffsetMeters"]
        q["pending"] = len(inputs["Pending"])
        q["ax_held"] = (forces.get("AxleLoad") or {}).get("LongitudinalAcceleration")
        assert abs(loads["RearNormalNewtons"] + loads["FrontNormalNewtons"] - 147 * 9.81) < 1e-6
        assert min(loads["RearNormalNewtons"], loads["FrontNormalNewtons"]) >= 0
        assert abs(loads["EffectiveFrictionCoefficient"] - 1.2845000267028808) < 1e-6
        assert q["use"] <= 1.000001 and abs(q["front_fy"]) <= q["front_budget"] + 1e-3
        dissipation = (q["fx"] * q["mid_slip"], -q["fy"] * q["rear_w"],
                       -q["front_fy"] * dot(forces["FrontVelocity"], forces["FrontLeft"]))
        min_dissipation = min(min_dissipation, *dissipation)
        assert min(dissipation) >= -1e-5
        max_crank_root = max(max_crank_root, abs(power["RootResidualNewtonMeters"]))
        max_wheel_root = max(max_wheel_root, abs(power["Wheel"]["RootResidualMetersPerSecond"]))
    for a, b in zip(curve, curve[1:]):
        max_force_jump = max(max_force_jump, abs(b["fx"] - a["fx"]), abs(b["fy"] - a["fy"]))
    assert not run["Contacts"]
    return run, curve, dict(min_dissipation_W=min_dissipation, max_crank_root_Nm=max_crank_root,
                            max_wheel_root_mps=max_wheel_root, max_native_force_jump_N=max_force_jump)


def read(e):
    rows = load(e / "trials.json")
    receipt = load(e / "receipt.json")
    model = load(e / "model.json")
    path = e / "native.zip"
    assert sha(path.read_bytes()).upper() == receipt["ArchiveSha256"]
    curves, checks, matched = {}, {}, {}
    with ZipFile(path) as z:
        assert z.testzip() is None
        for r in rows:
            run, curve, quality = read_native(z, r["Id"], model)
            assert not r["Outcome"].startswith("technical invariant")
            if r["ReproducesArchive"] is not None:
                assert r["ReproducesArchive"]
            a, b, t = r["ActivationSeconds"], r["BranchSeconds"], r["EndSeconds"]
            post = [q for q in curve if q["t"] > a + EPS]
            assert all(q["active"] == r["Drive"] for q in post)
            r["before"] = interval(curve, b, a)
            r["after"] = interval(curve, a, t)
            r["post_max_offset_change"] = max(abs(q["offset"] - at(curve, a)["offset"]) for q in post)
            r["post_min_front_N"] = min(q["front_fz"] for q in post)
            r["post_max_rear_N"] = max(q["rear_fz"] for q in post)
            r["post_max_pending"] = max(q["pending"] for q in post)
            r["exit_kph"] = next((q["speed"] for q in curve if q["s"] >= model["FirstExit"]), None) if r["Stage"] == "D" else None
            r["status"] = ("koniec okna" if r["Legal"] else "β45" if "beta45" in r["Outcome"] else r["Outcome"])
            if r["Legal"] and r["Stage"] != "D" and r["EndReference"] >= model["StraightExit"] - EPS:
                r["status"] = "koniec prostej"
            if r["UnloadedSteps"]:
                r["status"] += " · Fz przodu 0"
            assert abs(r["after"]["energy"]["closure"]) < 1.0
            curves[r["Id"]], checks[r["Id"]] = curve, quality
    for speed in ("60", "70", "80"):
        same = [r for r in rows if r["Cohort"] == speed and r["StepSeconds"] == .01]
        assert len({r["PreActivationSha256"] for r in same}) == 1
    for cohort in ("166", "167"):
        same = [r for r in rows if r["Stage"] == "D" and r["Cohort"] == cohort]
        assert len({r["PreActivationSha256"] for r in same}) == 1
        baseline = next(r for r in same if r["Id"] == f"D-{cohort}-original-fixed-100")
        end = math.floor((baseline["EndSeconds"] + EPS) * 100) / 100
        matched[cohort] = {r["Id"]: interval(curves[r["Id"]], baseline["ActivationSeconds"], end)
                           for r in same if r["EndSeconds"] >= end - EPS}
    historical = {}
    with ZipFile(BASE_ARCHIVE) as package, ZipFile(BytesIO(package.read("native.zip"))) as old:
        for cohort in ("166", "167"):
            archived = f"C-{cohort}-fixed-50"
            run, curve, _ = read_native(old, archived, model)
            t = next(r["ActivationSeconds"] for r in rows if r["Id"] == f"D-{cohort}-original-fixed-100")
            historical[archived] = interval(curve, t, run["EndSeconds"])
    return model, receipt, rows, curves, checks, matched, historical


def refine(rows, curves, local):
    checks = {}
    for stage, variant, control in (("A", "original", "feedback"), ("B", "h055", "pulse"),
                                    ("C", "separated", "pulse"), ("C", "lateral", "pulse")):
        group = [r for r in rows if r["Stage"] == stage and r["Cohort"] == "70" and
                 r["Variant"] == variant and r["Control"] == control and "-0.50-" in r["Id"]]
        key = f"{stage}-{variant}"
        checks[key] = {str(r["StepSeconds"]): {k: at(curves[r["Id"]], r["ActivationSeconds"] + .6)[k]
                                             for k in ("speed", "beta", "yaw", "fx", "fy")}
                       for r in group}
    local_groups = defaultdict(list)
    for row in local:
        local_groups[row["id"]].append(row)
    return checks, dict(local_groups)


def figures(out, rows, curves, grid, geometry):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    plt.rcParams.update({"font.size": 9, "axes.spines.top": False, "axes.spines.right": False,
                         "figure.facecolor": "white", "axes.grid": True, "grid.alpha": .2})
    palette = ["#b74837", "#21678e", "#27846e", "#9a6e25", "#755398"]
    data = {r["Id"]: r for r in rows}

    def dashboard(name, title, names, labels, fields):
        fig, axes = plt.subplots(3, 3, figsize=(13.6, 9), sharex=True, layout="constrained")
        for ax, (ylabel, series) in zip(axes.flat, fields):
            for id, label, color in zip(names, labels, palette):
                trial = data[id]
                origin = trial["ActivationSeconds"]
                curve = [q for q in curves[id] if q["t"] >= trial["BranchSeconds"] - EPS]
                x = [q["t"] - origin for q in curve]
                for k, field in enumerate(series):
                    ax.plot(x, [q[field] for q in curve], color=color, lw=1.35, ls="-" if not k else "--",
                            label=label if not k else None)
                if not trial["Legal"]:
                    ax.plot(x[-1], curve[-1][series[0]], "x", ms=5, color=color)
            ax.axvline(0, color="#334155", alpha=.6, ls=":")
            ax.set_ylabel(ylabel)
        for ax in axes[-1]:
            ax.set_xlabel("Czas od aktywacji [s]")
        axes[0, 0].legend(fontsize=8, loc="best")
        fig.suptitle(title, fontsize=13)
        fig.savefig(out / name, dpi=155)
        plt.close(fig)

    a = ["A-original-70-0.50-pulse-10", "A-original-70-0.50-feedback-10",
         "B-h055-70-0.50-pulse-10", "C-lateral-70-0.50-pulse-10"]
    d = ["D-167-original-fixed-100", "D-167-original-feedback-100", "D-167-h055-fixed-100",
         "D-167-lateral-fixed-100", "D-167-combined-fixed-100"]
    dashboard("169-stabilnosc.png", "70 km/h · impuls 0,5° na 0,2 s · pełny gaz", a,
              ["Baza, bez korekty", "Baza, regulator", "h 0,55 m", "Podział boczny"],
              [("β [°]", ["beta"]), ("βdot [°/s]", ["beta_dot"]), ("Yaw [rad/s]", ["yaw"]),
               ("Prędkość boczna [m/s]", ["body_side"]), ("Offset [m]", ["offset"]),
               ("Skręt rzeczywisty / żądany [°]", ["steer", "steer_request"]),
               ("Prędkość [km/h]", ["speed"]), ("Przyspieszenie [m/s²]", ["acceleration"]),
               ("Gaz aktywny / żądany", ["active", "requested"])])
    dashboard("169-wyjscie.png", "#167-H3 · identyczny prefiks · pełny gaz po aktywacji", d,
              ["D0 utrwalony skręt", "D0 regulator", "D1 transfer", "D2 opona", "D3 łącznie"],
              [("β [°]", ["beta"]), ("Yaw [rad/s]", ["yaw"]), ("Prędkość [km/h]", ["speed"]),
               ("κ", ["kappa"]), ("Fx [N]", ["fx"]), ("Fy [N]", ["fy"]),
               ("Fz przodu / tyłu [N]", ["front_fz", "rear_fz"]), ("Swept [m]", ["swept"]),
               ("Polecenia oczekujące", ["pending"])])

    fig, axes = plt.subplots(2, 3, figsize=(14, 7.4), layout="constrained")
    for variant, color in zip(("original", "separated", "lateral"), palette):
        g = [q for q in grid if q["Kph"] == 70 and q["AlphaDegrees"] == 5 and q["Variant"] == variant]
        for ax, field, ylabel in zip(axes.flat, ("Fx", "Fy", "Cr", "MaxLateralAtFx", "DissipationWatts", "MaxEigenRealPerSecond"),
                                     ("Fx [N]", "|Fy| [N]", "Cr [N/rad]", "Pułap |Fy| [N]",
                                      "Dyssypacja [W]", "Re λmax [1/s]")):
            ax.plot([q["Kappa"] for q in g], [abs(q[field]) if field == "Fy" else q[field] for q in g],
                    color=color, marker=".", label=variant)
            ax.set(xlabel="κ", ylabel=ylabel)
    axes[0, 0].legend(fontsize=8)
    axes[1, 2].axhline(0, color="#334155", ls=":")
    fig.suptitle("Prawo kontaktu · 70 km/h, αr = 5° · Cr i λ przy αr → 0", fontsize=13)
    fig.savefig(out / "169-opona.png", dpi=155)
    plt.close(fig)

    fig, axes = plt.subplots(1, 2, figsize=(13, 4.7), layout="constrained")
    sections = [q for seg in geometry["Segments"] for q in seg["Sections"]]
    for ax, selector, names in (
        (axes[0], lambda s: s <= 35 or s >= geometry["LapMeters"] - 27, a[:3]),
        (axes[1], lambda s: 126 <= s <= 161, d)):
        for wall in ("Inner", "Outer"):
            coords = [q[wall] for q in sections if selector(q["ReferenceMeters"])]
            ax.scatter([q["X"] for q in coords], [q["Y"] for q in coords], color="#8795a3", s=2)
        for name, color in zip(names, palette):
            c = [q for q in curves[name] if q["t"] >= data[name]["BranchSeconds"] - EPS]
            ax.plot([q["x"] for q in c], [q["y"] for q in c], label=name, color=color)
            ax.scatter(c[-1]["x"], c[-1]["y"], color=color, marker="x" if not data[name]["Legal"] else "o", s=28)
        ax.set(xlabel="X [m]", ylabel="Y [m]", aspect="equal")
        ax.legend(fontsize=7, loc="best")
    fig.suptitle("Przejazdy XY · prosta i wyjście #167-H3")
    fig.savefig(out / "169-xy.png", dpi=155)
    plt.close(fig)


def n(v, digits=2):
    return "—" if v is None else f"{v:.{digits}f}".replace(".", ",")


def append_report(out, rows, historical, probes, native_dt, local_dt, budget):
    data = {r["Id"]: r for r in rows}
    body = REPORT.read_text(encoding="utf-8").split("<!-- measurements -->")[0].rstrip()
    lines = [
        body, "", "<!-- measurements -->", "", "## Energia, sondy i wrażliwość na krok", "",
        "Porównania o wspólnym czasie do pierwszego DNF oraz pomiary przed aktywacją są w pakiecie. "
        f"Archiwalne 50% na oryginalnej fizyce i utrwalonym skręcie dało +{n(historical['C-166-fixed-50']['delta_kph'])} "
        f"i +{n(historical['C-167-fixed-50']['delta_kph'])} km/h. To osobna para gazu, bez przypisywania jej efektu do fizyki.", "",
        f"Maksymalna różnica w sondzie transferu wyniosła {n(max(q['EffectNewtons'] for q in probes['Rows']),1)} N. "
        "Siły i Fz ze wszystkich sond zapisano obok przebiegów.", "",
        "Bilans obejmuje pracę spalania, ΔK nadwozia, koła i wału, ciepło sprzęgła, transmisję, łożysko, "
        "opory i straty poślizgu. Prace kontaktu odtworzono z przyłożonych sił i średnich zaakceptowanych "
        "prędkości. Sprawdzono znak dyssypacji w lokalnych osiach i limity obu kontaktów. Maksymalne "
        f"bezwzględne residuum po aktywacji wyniosło {n(max(abs(r['after']['energy']['closure']) for r in rows),3)} J.", "",
        "| Wyjście #167 | Spalanie [kJ] | ΔK [kJ] | Strata podłużna [kJ] | Strata boczna obu opon [kJ] | Residuum [J] |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    for id in ("D-167-original-fixed-100", "D-167-original-feedback-100", "D-167-h055-fixed-100",
               "D-167-lateral-fixed-100", "D-167-combined-fixed-100", "D-167-h055-feedback-50"):
        if id not in data:
            continue
        e = data[id]["after"]["energy"]
        lines.append(f"| {id} | {n(e['combustion']/1000)} | {n(e['net_kinetic']/1000)} | "
                     f"{n(e['tyre_x']/1000)} | {n((e['tyre_r_y']+e['tyre_f_y'])/1000)} | {n(e['closure'],3)} |")
    lines += [
        "", "Okna mają różną długość tam, gdzie wystąpił DNF. Przy 70 km/h i impulsie 0,5° "
        "zagęszczenie natywnego kroku 20/10/5 ms zachowało wynik bramek dla A2, h=0,55, C1 i C2. "
        "Poniżej β w tej samej chwili 0,6 s po aktywacji.", "",
        "| Próba | β przy 20 ms | β przy 10 ms | β przy 5 ms |",
        "|---|---:|---:|---:|",
    ]
    for key, q in native_dt.items():
        lines.append(f"| {key} | " + " | ".join(f"{n(q[step]['beta'],3)}°" for step in ("0.02", "0.01", "0.005")) + " |")
    lines += [
        "", "Każda gałąź fizyki ma też lokalne sondy predyktora 20/10/5/2,5 ms od identycznego "
        "zaakceptowanego stanu i pending. W D3 różnica β po 0,4 s pomiędzy 20 a 10 ms wyniosła "
        "0,068°, a pomiędzy 5 a 2,5 ms 0,014°. To lokalna kontrola numeryczna także dla h=0,35 "
        "i h=0,75. Nie modeluje ruchu po oderwaniu przodu ani pełnej zbieżności Box2D przy odciążeniu. "
        "Przy regulatorze krok może zmienić wybór z dyskretnej siatki skrętu.", "",
        "| Sonda | β przy 20 ms | β przy 10 ms | β przy 5 ms | β przy 2,5 ms |",
        "|---|---:|---:|---:|---:|",
    ]
    for key, group in local_dt.items():
        lines.append(f"| {key} | " + " | ".join(f"{n(q['EndBetaDegrees'],3)}°" for q in group) + " |")
    lines += [
        "", "## Pełna tabela natywna", "",
        "V start oznacza stan przed opóźnieniem. ΔV i maksima obejmują czas po aktywacji do końca "
        "okna, prostej lub pierwszej bramki. Max κ, β, yaw, Fx i Fy to maksima wartości bezwzględnych.", "",
        "| Wariant | Gaz | V start [km/h] | ΔV [km/h] | Max κ | Max β [°] | Max yaw [rad/s] | Min swept [m] | Max Fx [N] | Max Fy [N] | Wynik |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|",
    ]
    for r in rows:
        q = r["after"]
        lines.append(f"| {r['Id']} | {r['Drive']*100:.0f}% | {n(r['StartKph'])} | {n(q['delta_kph'])} | "
                     f"{n(q['max_kappa'],3)} | {n(q['max_beta'])} | {n(q['max_yaw'],3)} | {n(q['min_swept'],3)} | "
                     f"{n(q['max_fx'],0)} | {n(q['max_fy'],0)} | {r['status']} · {n(q['seconds'],3)} s |")
    counts = Counter(r["Stage"] for r in rows)
    lines += [
        "", "## Dowody i budżet", "",
        f"{len(rows)} natywne rollouty (" + ", ".join(f"{k} {v}" for k, v in counts.items()) +
        f"), {n(budget['native_cpu'],3)} CPU-s wraz z sondami i zapisem oraz "
        f"{n(budget['verification_cpu'],3)} CPU-s lokalnej kontroli kroku bez nowych światów. "
        f"Natywna jazda, weryfikacja i zapis analizy łącznie zajęły {n(budget['total_cpu'],2)} z 300 CPU-s "
        "w zmierzonym protokole. Testy zgodności biegną osobno.", "",
        "Pakiet zawiera pełne telemetrie od rozbiegu lub zatrzymania, aktywacje i pending, rejestr wszystkich "
        "DNF, trzy dowody zgodności z #168, sondy, siatkę, pełne bilanse i hashe źródeł. "
        "SHA256 wiąże raport, wykresy i archiwum.", "",
    ]
    (out / "REPORT.md").write_text("\n".join(lines), encoding="utf-8", newline="\n")


def main():
    began = time.process_time()
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("evidence", type=Path)
    ap.add_argument("output", type=Path)
    args = ap.parse_args()
    e, out = args.evidence, args.output
    out.mkdir(parents=True, exist_ok=True)
    m, rec, rows, curves, audit, matched, old = read(e)
    grid, probes = load(e / "contact-grid.json"), load(e / "load-probes.json")
    dt, local = refine(rows, curves, load(e / "local-convergence.json"))
    figures(out, rows, curves, grid, load(e / "geometry.json"))
    measurements = dict(trials=rows, contact_audit=audit, d_common_time=matched,
                        archived_half=old, native_dt=dt, local_dt=local)
    code = {p.as_posix(): sha(p.read_bytes()) for p in sorted(SOURCE.iterdir()) if p.suffix in (".cs", ".py", ".csproj")}
    verification = load(e / "verification.json")
    with ZipFile(out / "169-dowody.zip", "w", compression=ZIP_DEFLATED, compresslevel=6) as z:
        for path in sorted(e.iterdir()):
            if path.is_file() and path.name != ".study.lock":
                z.write(path, path.name, compress_type=ZIP_STORED if path.suffix == ".zip" else ZIP_DEFLATED)
        z.writestr("measurements.json", json.dumps(measurements, indent=2))
        z.writestr("source-sha256.json", json.dumps(code, indent=2))
        analysis = time.process_time() - began
        budget = dict(native_cpu=rec["CpuSeconds"], verification_cpu=verification["CpuSeconds"],
                      analysis_cpu=analysis, total_cpu=rec["CpuSeconds"] + verification["CpuSeconds"] + analysis,
                      worlds=rec["Rollouts"], limit=300)
        assert budget["total_cpu"] < 300
        z.writestr("analysis-receipt.json", json.dumps(budget, indent=2))
    with ZipFile(out / "169-dowody.zip") as z:
        assert z.testzip() is None
    append_report(out, rows, old, probes, dt, local, budget)
    products = [p for p in sorted(out.iterdir()) if p.is_file() and p.name not in (".gitattributes", "SHA256SUMS")]
    (out / "SHA256SUMS").write_text("".join(f"{sha(p.read_bytes())}  {p.name}\n" for p in products),
                                    encoding="ascii", newline="\n")
    print(json.dumps(budget, indent=2))


if __name__ == "__main__":
    main()
