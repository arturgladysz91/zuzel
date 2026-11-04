"""Summarize #167 evidence without constructing simulation worlds."""
import argparse
from collections import Counter
import hashlib
from io import BytesIO
import json
import math
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED, ZIP_STORED

BASE = Path("docs/experiments/autonomous-fast-corner")
HERE = Path("tools/fast-two-corners")


def load(path):
    return json.loads(path.read_bytes())


def digest(data):
    return hashlib.sha256(data).hexdigest()


def rows(path):
    return [json.loads(line) for line in path.read_bytes().splitlines()]


def vector_dot(a, b):
    return a["X"] * b["X"] + a["Y"] * b["Y"]


def telemetry(samples):
    trace = []
    for q in samples:
        i = q["Input"]
        v = i["Rider"]
        f = q["Forces"]
        power = q["Powertrain"]
        w = power["Wheel"] if power else None
        dt = i["Time"] - trace[-1]["t"] if trace else 0
        beta = math.degrees(v["SlideRadians"])
        beta_dot = ((beta - trace[-1]["beta"] + 180) % 360 - 180) / dt if dt else 0
        losses = {}
        if f and w:
            losses = {
                "front_lateral": -vector_dot(f["FrontForce"], f["FrontVelocity"]) * dt,
                "rear_lateral": -f["RearLateral"] * vector_dot(f["RearVelocity"], f["BodyLeft"]) * dt,
                "rear_longitudinal": w["LongitudinalLossJoules"],
                "bearing": w["BearingLossJoules"],
                "clutch": power["ClutchHeatJoules"],
                "transmission": power["TransmissionLossJoules"],
                "resistance": -vector_dot(f["Resistance"], v["Velocity"]) * dt,
                "net": -(q["EnergyExcessJoules"] or 0),
            }
        trace.append(dict(s=v["TotalReferenceMeters"], t=i["Time"], x=v["Position"]["X"],
                          y=v["Position"]["Y"], kph=v["Speed"] * 3.6,
                          offset=v["Road"]["OffsetMeters"], yaw=v["AngularVelocity"],
                          beta=beta, beta_dot=beta_dot, side=v["SideSpeed"],
                          requested=q["RequestedDrive"], active=i["Active"]["Drive"],
                          steer=math.degrees(i["Steering"]), steer_request=math.degrees(q["RequestedSteering"]),
                          front=abs(f["FrontLateral"]) / f["TyreLoads"]["FrontCapacityNewtons"] if f else 0,
                          rear=w["RearUtilization"] if w else 0,
                          rear_x=f["RearLongitudinal"] / f["TyreLoads"]["RearCapacityNewtons"] if f else 0,
                          rear_y=f["RearLateral"] / f["TyreLoads"]["RearCapacityNewtons"] if f else 0,
                          slip=w["InstantSlipRatio"] if w else 0,
                          inner=q["Inner"], outer=q["Outer"], swept=q["Certificate"], losses=losses))
    return trace


def energy(trace, bend):
    totals = Counter()
    for a, b in zip(trace, trace[1:]):
        lo, hi = a["s"], b["s"]
        if hi <= lo:
            continue
        overlap = max(0, min(hi, bend["Exit"]) - max(lo, bend["Entry"]))
        for name, joules in b["losses"].items():
            totals[name] += overlap / (hi - lo) * joules
    return dict(totals)


def crossing(trace, s):
    return next((q for q in trace if q["s"] >= s), None)


def section_time(trace, target):
    for a, b in zip(trace, trace[1:]):
        if a["s"] < target <= b["s"]:
            return a["t"] + (b["t"] - a["t"]) * (target - a["s"]) / (b["s"] - a["s"])
    return None


def turn_onset(trace, entry):
    # Sustained physical response near the entry, estimated from the 100 Hz history.
    for i, q in enumerate(trace):
        if not entry - 20 <= q["s"] <= entry + 6:
            continue
        sustained = trace[i:i + 12]
        if len(sustained) >= 12 and sustained[-1]["t"] - q["t"] >= .09 and all(
                p["steer"] > .7 and p["yaw"] > .10 for p in sustained):
            return {k: q[k] for k in ("s", "t", "x", "y", "steer", "yaw")}
    return None


def inspect(evidence):
    ledger = rows(evidence / "ledger.jsonl")
    inputs = next(r for r in ledger if r["Event"] == "inputs")
    frozen = load(evidence / "freeze.json")
    for name, expected in frozen["Sources"].items():
        if digest((HERE / name).read_bytes()).upper() != expected:
            raise ValueError(f"Frozen source changed: {name}")
    traces, runs, summaries = {}, {}, {}
    for row in ledger:
        if row["Event"] != "archive":
            continue
        data = (evidence / row["File"]).read_bytes()
        if digest(data).upper() != row["Sha256"]:
            raise ValueError(f"Native archive mismatch: {row['File']}")
        with ZipFile(BytesIO(data)) as archive:
            if archive.testzip() is not None:
                raise ValueError(f"CRC error: {row['File']}")
            run = json.load(archive.open("native.json"))
            result = json.load(archive.open("result.json"))
        name = row["id"]
        if result["Fingerprint"] != run["Fingerprint"]:
            raise ValueError(f"Run fingerprint mismatch: {name}")
        trace = telemetry(run["Riders"][0]["Samples"])
        traces[name], runs[name] = trace, run
        summaries[name] = {
            "onset": [turn_onset(trace, inputs[b]["Entry"]) for b in ("First", "Second")],
            "energy": [energy(trace, inputs[b]) for b in ("First", "Second")],
            "observations": {str(s): crossing(trace, s) for s in (130, 135, 140, 144, 148, 152, 155, 180, 190, 208)},
            "reserve_end": trace[-1],
            "native_steps": run["NativeSteps"],
        }
    return ledger, inputs, frozen, traces, runs, summaries


def figures(out, geometry, inputs, traces, ledger):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    plt.rcParams.update({"font.size": 10, "axes.spines.top": False, "axes.spines.right": False,
                         "figure.facecolor": "white", "axes.grid": True, "grid.alpha": .18})
    blue, red, amber = "#176795", "#B74A3B", "#C38920"
    fig, (xy, speed) = plt.subplots(1, 2, figsize=(13, 5.2), layout="constrained")
    sections = [q for seg in geometry["Segments"] for q in seg["Sections"]]
    for wall in ("Inner", "Outer"):
        xy.plot([q[wall]["X"] for q in sections], [q[wall]["Y"] for q in sections], color="#64748b", lw=1)
    for name, trace in traces.items():
        if name.startswith("167") and name != "167-H3":
            xy.plot([q["x"] for q in trace], [q["y"] for q in trace], color=red, alpha=.15)
            speed.plot([q["s"] for q in trace], [q["kph"] for q in trace], color=red, alpha=.17)
    for name, color in (("166-H3", blue), ("167-H3", red)):
        trace = traces[name]
        xy.plot([q["x"] for q in trace], [q["y"] for q in trace], color=color, lw=2, label="#" + name)
        speed.plot([q["s"] for q in trace], [q["kph"] for q in trace], color=color, lw=2, label="#" + name)
        for which in ("First", "Second"):
            q = turn_onset(trace, inputs[which]["Entry"])
            if q:
                xy.scatter(q["x"], q["y"], marker="^", s=48, color=color, edgecolors="white", zorder=6)
    for q, marker, color in ((crossing(traces["166-H3"], inputs["Goal"]), "s", blue),
                             (traces["167-H3"][-1], "x", red)):
        xy.scatter(q["x"], q["y"], marker=marker, s=56, color=color, zorder=7)
    xy.set(aspect="equal", xlabel="X [m]", ylabel="Y [m]", title="XY, bandy i punkt rozpoczęcia skrętu △")
    xy.legend(loc="center")
    for bend in ("First", "Second"):
        speed.axvspan(inputs[bend]["Entry"], inputs[bend]["Exit"], color=blue, alpha=.09)
    speed.axvline(inputs["Goal"], color="#475569", linestyle="--", label="Cel +20 m")
    speed.set(xlabel="Dystans referencyjny [m]", ylabel="Prędkość [km/h]", title="Prędkość do końca rezerwy albo DNF")
    speed.legend(loc="lower right")
    fig.savefig(out / "167-xy-predkosc.png", dpi=165)
    plt.close(fig)

    groups = [
        ("Gaz [0–1]", [("requested", "żądany"), ("active", "aktywny")]),
        ("Skręt [°]", [("steer_request", "żądany"), ("steer", "rzeczywisty")]),
        ("Yaw [rad/s]", [("yaw", "")]),
        ("β [°]", [("beta", "")]),
        ("βdot [°/s]", [("beta_dot", "")]),
        ("Prędkość boczna [m/s]", [("side", "")]),
        ("Wykorzystanie kontaktu", [("front", "przód Fy"), ("rear", "tył Fx/Fy")]),
        ("Udział limitu tylnej opony", [("rear_x", "Fx"), ("rear_y", "Fy")]),
        ("Wheelspin, slip ratio", [("slip", "")]),
        ("Prześwit [m]", [("inner", "wewn."), ("outer", "zewn."), ("swept", "swept")]),
    ]
    fig, axes = plt.subplots(5, 2, figsize=(13, 13), sharex=True, layout="constrained")
    for ax, (title, fields) in zip(axes.flat, groups):
        for name, color in (("166-H3", blue), ("167-H3", red)):
            trace = [q for q in traces[name] if q["s"] >= 25]
            for style, (field, label) in zip(("-", "--", ":"), fields):
                ax.plot([q["s"] for q in trace], [q[field] for q in trace], color=color, ls=style,
                        lw=1.45, label=f"{name} {label}" if len(fields) > 1 else name)
        ax.axvline(inputs["First"]["Exit"], color="#64748b", alpha=.5)
        ax.axvline(inputs["Second"]["Entry"], color="#64748b", alpha=.5)
        if title == "β [°]":
            ax.axhline(45, color=amber, ls=":")
        if title == "Prześwit [m]":
            ax.axhline(.5, color=amber, ls="-.")
        ax.set(ylabel=title)
        if len(fields) > 1:
            ax.legend(fontsize=7, ncol=2)
    for ax in axes[-1, :]:
        ax.set(xlabel="Dystans referencyjny [m]")
    fig.suptitle("H3 · #166 i #167 · od 25 m do rezerwy lub DNF")
    fig.savefig(out / "167-telemetria.png", dpi=145)
    plt.close(fig)

    fig, ax = plt.subplots(figsize=(8.8, 3.6), layout="constrained")
    generations = [r for r in ledger if r["Event"] == "generation"]
    for seed, color in zip(sorted({r["Seed"] for r in generations}), (blue, red)):
        data = [r for r in generations if r["Seed"] == seed]
        ax.plot([q["Generation"] for q in data], [q["WorstProgress"] for q in data],
                marker=".", color=color, label=f"Seed {seed}")
    ax.axhline(inputs["Second"]["Entry"], color=amber, ls="--", label="Wejście w łuk 2")
    ax.set(xlabel="Generacja", ylabel="Najgorszy postęp z 4 startów [m]",
           title="Trening · 0 legalnych ukończeń w każdej generacji")
    ax.legend(loc="lower right")
    fig.savefig(out / "167-trening.png", dpi=155)
    plt.close(fig)


def num(value, digits=3):
    return "—" if value is None else f"{value:.{digits}f}".replace(".", ",")


def cell(value, key):
    return (value or {}).get(key)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=Path)
    parser.add_argument("out", type=Path)
    args = parser.parse_args()
    e, out = args.evidence, args.out
    out.mkdir(parents=True, exist_ok=True)
    ledger, inputs, freeze, traces, runs, telemetry_summary = inspect(e)
    stages = {s: [q["result"] for q in ledger if q["Event"] == "result" and q["stage"] == s]
              for s in ("diagnosis", "training", "selection", "validation", "comparison")}
    baseline = {q["Start"]["Id"]: q for q in stages["diagnosis"] + stages["comparison"]}
    candidate = {q["Start"]["Id"]: q for q in stages["validation"]}
    refs = load(e / "reference161.json")
    old = {q["result"]["Id"]: q["result"] for q in load(BASE / "evidence/validation.json")["Rows"]}
    for q in refs:
        if q["Available"] and digest(Path(q["Source"]).read_bytes()).upper() != q["Sha256"]:
            raise ValueError("Historical archive mismatch: " + q["Source"])
    paired_first = {k: None if candidate[k]["First"]["Seconds"] is None else
                    candidate[k]["First"]["Seconds"] - (baseline[k]["First"]["Seconds"] if k in baseline
                    else old[k]["BendSeconds"]) for k in candidate}
    straight_prefix = {}
    for k, q in candidate.items():
        new_exit = cell(q["First"]["Exit"], "Seconds")
        new_after = section_time(traces[q["Id"]], inputs["First"]["Exit"] + 20)
        if new_exit is None or new_after is None:
            straight_prefix[k] = None
            continue
        if k in baseline:
            old_exit = baseline[k]["First"]["Exit"]["Seconds"]
            old_after = section_time(traces[baseline[k]["Id"]], inputs["First"]["Exit"] + 20)
        else:
            old_exit, old_after = old[k]["Exit"]["Seconds"], old[k]["After20"]["Seconds"]
        straight_prefix[k] = new_after - new_exit - (old_after - old_exit)
    counts = {stage: dict(runs=len(results), completed=sum(r["Pass"] for r in results),
                         reasons=Counter(r["Reason"] for r in results),
                         worst=min(r["EndReference"] for r in results),
                         best=max(r["EndReference"] for r in results))
              for stage, results in stages.items()}
    cpu = load(e / "study.json")["TotalCpuSeconds"]
    summary = {"geometry": inputs, "counts": counts, "cpu_seconds": cpu, "paired_first_delta": paired_first, "paired_straight_first20_delta": straight_prefix,
               "telemetry": telemetry_summary, "verdict": "FAIL"}
    (out / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8", newline="\n")
    figures(out, load(e / "geometry.json"), inputs, traces, ledger)

    report = [
        "# Badanie #167 · dwa pełne łuki", "",
        "**FAIL.** Zamrożony #167 ukończył **0/8** manewrów i **0/8** rezerw. "
        "Niezmieniony #166 ukończył **6/6** pełnych porównań z rezerwą. Na żadnym identycznym starcie "
        "nie ma dwóch ukończeń #166 i #167, więc poprawa całkowitego czasu jest niewyznaczalna.", "",
        "Baza techniczna `7754819d5e96ad8808e4de560262c7993fdad008` (Draft PR #165). "
        "Referencja #161 `85a5ee3295c2dd983fbacb21859649f794cc5ba5`. Izolowane narzędzie PROVISIONAL, "
        "uruchamiane wyłącznie jawną komendą. Oryginalny Box2D i model obu kontaktów z #166.", "",
        "## Geometria i protokół", "",
        f"Przekroje z `TrackFrame` [m] wynoszą {num(inputs['First']['Entry'])}, "
        f"{num(inputs['First']['Exit'])}, {num(inputs['Second']['Entry'])}, "
        f"{num(inputs['Second']['Exit'])} i {num(inputs['Goal'])}. Prosta pomiędzy łukami ma 62 m. "
        "Mierzymy od t=0 na zatrzymanym starcie, łącznie z reakcją, do interpolowanego przekroju +20 m. "
        "Po pierwszej próbce za celem świat pracuje co najmniej 1 s z nowymi decyzjami co 0,2 s "
        "i pełną kolejką opóźnioną o 0,24 s.", "",
        "Kontrola audytowa przelicza obrys i dolne ograniczenie swept na wszystkich klatkach "
        "pełnych prób i rezerw, z ponowną projekcją obrysu przez `RacingLineGeometry` i wzorem "
        "`Clearance153`. Wybrano zerowy budżet dodatkowych certyfikacji 13/9. Rezerwa obejmuje "
        "tylko zmierzony czas jazdy. Stage 4 ma osobny zakres.", "",
        "## Wyniki na identycznych stanach", "",
        "| Kontroler / start | Łuk 1 czas [s] | V wyjścia 1 [km/h] | V prostej max [km/h] | Łuk 2 czas [s] | V wyjścia 2 [km/h] | Czas całości [s] | Min swept [m] | Status |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---|"
    ]

    def line(q):
        one, two = q["First"], q["Second"]
        status = "ukończenie + 1 s" if q["Pass"] else f"DNF · {num(q['EndReference'], 2)} m"
        return "| #" + q["Id"] + " | " + " | ".join(
            [num(one["Seconds"]), num(cell(one["Exit"], "Kph"), 2), num(q["StraightMaximumKph"], 2),
             num(two["Seconds"]), num(cell(two["Exit"], "Kph"), 2), num(cell(q["Goal"], "Seconds")),
             num(q["MinimumSwept"]), status]) + " |"

    for start in sorted(candidate):
        report.append(line(candidate[start]))
        if start in baseline:
            report.append(line(baseline[start]))
        else:
            q = old[start]
            report.append(f"| #166-{start} · archiwum | {num(q['BendSeconds'])} | {num(q['Exit']['Kph'],2)} | — | — | — | — | {num(q['MinSweep'])} | skrócony zakres #166 |")
        ref = next(q for q in refs if q["Id"] == start)
        if ref["Available"]:
            report.append(line(ref["Result"]))
        else:
            report.append(f"| #161-{start} | — | — | — | — | — | — | — | brak pełnej pary |")

    report += [
        "", "Vmax w wierszu DNF obejmuje osiągniętą część prostej. Swept obejmuje cały zapis łącznie "
        "z rezerwą albo krokiem, w którym przerwano przejazd. Cztery wiersze #161 pochodzą z pełnych "
        "archiwów na identycznych startach. H5–H8 są niedostępne dla #161. Wydłużonego #166 H6/H7 "
        "nie uruchamiano. Historyczny zapis kończył się po 0,9 s od wejścia w drugi łuk.", "",
        "## Tempo i stan w łukach", "",
        "Apex to próbka najmniejszego offsetu w środkowych 80% odcinka. Minima DNF obejmują dostępny zapis.", "",
        "| Kontroler / start | Łuk 1 wejście / min / apex / wyjście [km/h] | Łuk 2 wejście / min / apex / wyjście [km/h] |",
        "|---|---:|---:|"
    ]
    def speeds(bend):
        return " / ".join(num(v, 2) for v in (cell(bend["Entry"], "Kph"), bend["MinimumKph"],
                                              cell(bend["Apex"], "Kph"), cell(bend["Exit"], "Kph")))

    for q in list(candidate.values()) + sorted(baseline.values(), key=lambda r: r["Id"]) + [
            r["Result"] for r in refs if r["Available"]]:
        report.append(f"| #{q['Id']} | {speeds(q['First'])} | {speeds(q['Second'])} |")

    report += [
        "", "Zmiana czasu pierwszego łuku #167 minus #166, w sekundach, na identycznych startach.", "",
        "| Start | H1 | H2 | H3 | H4 | H5 | H6 | H7 | H8 |", "|---|---:|---:|---:|---:|---:|---:|---:|---:|",
        "| Łuk 1 Δ [s] | " + " | ".join(num(paired_first[k]) for k in sorted(paired_first)) + " |",
        "| Pierwsze 20 m prostej Δ [s] | " + " | ".join(num(straight_prefix[k]) for k in sorted(straight_prefix)) + " |", "",
        "Nowy wariant skrócił pierwszy łuk we wszystkich siedmiu przypadkach, w których go ukończył. "
        "Na prostej H1–H4 i H8 osiągnął niższy szczyt niż baza, po czym przerwał jazdę. "
        "Pełny czas prostej jest dostępny wyłącznie dla #166 i archiwalnego #161.", "",
        "![XY, bandy, początek skrętu i prędkość](167-xy-predkosc.png)", "",
        "Trójkąty wskazują pierwszy utrzymany skręt w pobliżu wejścia w łuk (skręt >0,7°, yaw >0,10 rad/s "
        "przez co najmniej 0,09 s). Kwadrat oznacza cel #166-H3, a krzyżyk DNF #167-H3. "
        "Jasne czerwone linie pokazują pozostałe H1–H8.", "",
        "## Diagnostyka i przyczyny DNF", "",
        f"- **#166 H3/H4.** Drugi łuk zaczynał przy {num(baseline['H3']['Second']['Entry']['Kph'],2)} i "
        f"{num(baseline['H4']['Second']['Entry']['Kph'],2)} km/h. Wzrost β i yaw był kontrolowany, "
        "bez dowodu nadmiernego zwalniania przed łukiem. H3 miał 73,1 km/h przy 180 m i 71,7 km/h na wejściu. "
        "Przy 208 m kontakt przedni wykorzystywał ok. 82% Fy, tylny ok. 86% łącznego Fx/Fy, "
        "a gaz aktywny wynosił 0 podczas wytracania prędkości. H3 przesunął się z offsetu 3,38 m "
        "przy 185 m na 2,25 m przy 208 m. Przygotowanie rozpoczęło się przed granicą łuku.", "",
        "- **#167 H3.** Gaz osiągnął 100% już na wyjściu z pierwszego łuku. Przy 140 m wykorzystanie "
        "tyłu wyniosło 96%, wobec 72% w #166, a yaw wyniósł 0,58 wobec 0,20 rad/s. "
        "Przy 148 m β wzrosło do 21,4°, a prędkość poprzeczna do −7,36 m/s. "
        "Wycofywanie gazu po wzroście β nie odzyskało wykonalności. Przy ok. 155,57 m β przekroczyło 45°. "
        "Przejazd pokazuje współwystąpienie pełnego gazu, wheelspinu i utraty zapasu bocznego. "
        "Rozdzielenie wpływu gazu, linii i wzmocnień wymagałoby osobnego eksperymentu.", "",
        "- **#167 H5.** W odróżnieniu od pozostałych prób utracił kontrolę już przy 59,22 m. "
        "Oscylacja yaw i kąta znoszenia pojawiła się przed przełączeniem sterowania na plan drugiego łuku. "
        "Ten start obnażył ryzyko zmiany skali prędkości i marginesu pierwszego łuku.", "",
        "| Start | Pierwsza bramka | t [s] | Referencja [m] | Maks. β bez znaku [°] |",
        "|---|---|---:|---:|---:|",
    ]
    for start, q in candidate.items():
        report.append(f"| {start} | β >45° | {num(q['BadSeconds'])} | {num(q['BadReference'])} | {num(q['MaximumBetaDegrees'],2)} |")
    report += ["", "![Gaz, skręt, yaw, β, kontakty, wheelspin i prześwit](167-telemetria.png)", "",
               "## Straty energii i możliwość dalszej jazdy", "",
               "Strata netto to praca spalania pomniejszona o przyrost energii kinetycznej układu. Pochodzi z natywnego "
               "`EnergyExcessJoules`. Składowe boczne i opory są oszacowane z pracy siły w kroku. "
               "Kroki na granicach rozdzielono proporcjonalnie do dystansu referencyjnego. "
               "W tabeli energii raportujemy pełne łuki.", "",
               "| Kontroler / start | Łuk 1 netto [kJ] | Łuk 2 netto [kJ] |",
               "|---|---:|---:|"]
    for name in sorted(traces):
        q = next(r for r in baseline.values() if r["Id"] == name) if name.startswith("166") else candidate[name[-2:]]
        values = [num(telemetry_summary[name]["energy"][i].get("net", 0) / 1000, 2)
                  if q[which]["Exit"] else "—" for i, which in enumerate(("First", "Second"))]
        report.append(f"| #{name} | " + " | ".join(values) + " |")

    report += ["", "W #166-H3 przy celu β wyniosło 0,88°, βdot 2,59°/s, yaw 0,14 rad/s, "
               "prędkość boczna −0,29 m/s, a prześwit do bliższej bandy 4,33 m. Przód wykorzystywał "
               "ok. 13% Fy, tył ok. 23% Fx/Fy. Gaz aktywny wynosił 0,131, a w kolejce czekało "
               "polecenie 0,121. Następna sekunda legalnej jazdy jest w archiwum. #167 nie dostarczył "
               "stanu przy celu. Wyniki wszystkich składowych strat, próbki na przekrojach, aktywne "
               "polecenia i pending są dostępne w pakiecie.", "",
               "## Polityka, trening i koszty", "",
               "Polityka korzystała z profilu #166 dla pierwszego łuku z uczoną skalą prędkości i marginesem. "
               "W ostatnich 17 m pierwszego łuku rozpoczęła podgląd krzywizny do 86 m. Na podstawie "
               "prognozy stanu po całej kolejce pending obliczała wsteczne ograniczenie prędkości z "
               "krzywizny, oszacowanego dostępnego Fy i drogi wytracania. Sterowanie reagowało na "
               "XY, χ, θ, yaw, β i βdot, prędkość, wheelspin, prześwit, rezerwę kontaktów oraz stan "
               "wału, sprzęgła i koła. Wykonalność propozycji ustalała dopiero natywna jazda.", "",
               f"Selekcja zamroziła seed **{freeze['Selected']['Seed']}**, generację "
               f"**{freeze['Selected']['Generation']}** i źródła przed H1–H8. "
               "C-B101 i C-B245 występują wyłącznie jako historyczne źródła profili #166. "
               "Ich wyniki sportowe nie są wynikami #167.", "",
               "| Etap | Próby | Ukończenia z rezerwą | Najgorszy postęp [m] | Najlepszy postęp [m] |",
               "|---|---:|---:|---:|---:|"]
    for stage, q in counts.items():
        report.append(f"| {stage} | {q['runs']} | {q['completed']} | {num(q['worst'],2)} | {num(q['best'],2)} |")
    report += ["", f"Łączny koszt badania **{num(cpu,2)} CPU-s z 900**. "
               "Dwie przerwane rezerwacje przed pierwszym krokiem świata doliczono do pomiaru. "
               "Trening zakończył się po 192 epizodach i 8 oddzielnych epizodach selekcji. "
               "Żaden kandydat nie dotarł do drugiego łuku, więc nie uruchomiono rozszerzenia do 400. "
               "Selekcja przeniosła do walidacji wariant o najwyższym najgorszym postępie na odrębnym zestawie. "
               "W treningu 182 DNF wynikały z β, 10 ze swept. W selekcji odpowiednio 6 i 2. "
               "Pełny rejestr przypisuje powód każdemu epizodowi.", "",
               "![Najgorszy postęp w kolejnych generacjach](167-trening.png)", ""]
    forecast_queries = sum(q["ForecastContactQueries"] for v in stages.values() for q in v)
    all_steps = sum(q["NativeSteps"] for q in ledger if q["Event"] == "result")
    wall = sum(q.get("DecisionWallSeconds") or 0 for q in candidate.values())
    decisions = sum(q["Decisions"] for q in candidate.values())
    # The frozen chooser passed the last two constructor arguments in reverse order.
    # The dedicated ledger result has the correct contact-query counter.
    forecast_count = sum(q["ForecastContactQueries"] for q in runs.values()
                         if q["Name"] not in ("166-H3", "166-H4"))
    def integer(n):
        return f"{n:,}".replace(",", " ")
    report += [
        f"Łącznie 214 przejazdów natywnych i {integer(all_steps)} kroków oraz {integer(forecast_queries)} zapytań "
        "kontaktowych predyktora lub sondy opon. Archiwizowane przebiegi z licznikiem wywołań "
        f"odnotowały {integer(forecast_count)} wywołania prognozy (walidacja i cztery porównania). "
        "Dwa pola licznika w natywnych metadanych zostały zamienione przez kolejność argumentów konstruktora. "
        "Raport odczytuje je zgodnie ze znaczeniem i zachowuje surowe archiwa. Pełny licznik rolloutów treningu "
        "jest niedostępny, a zapytania kontaktowe wszystkich epizodów są w rejestrze. "
        f"W 8 próbach #167 zarejestrowano {decisions} decyzje, średnio {num(wall / decisions * 1000,2)} ms "
        "czasu ściennego na decyzję. CPU procesu ma zgrubną rozdzielczość Windows. "
        "Rozruch i zapis danych są ujęte w całkowitym CPU.", "",
        "## Dowody", "",
        "- `evidence.zip` zawiera natywne historie z decyzjami, aktywacjami, kolejkami i 100 Hz telemetrii, "
        "rejestr prób, zamrożone parametry i hashe źródeł, geometrię oraz metryki z archiwów #161.",
        "- `summary.json` zawiera m.in. początki skrętu, energię według składowych i próbki diagnostyczne. "
        "`SHA256SUMS` wiąże pliki dowodowe.", "",
    ]
    (out / "REPORT.md").write_text("\n".join(report), encoding="utf-8", newline="\n")

    with ZipFile(out / "evidence.zip", "w", compression=ZIP_DEFLATED, compresslevel=6) as package:
        for path in sorted(e.rglob("*")):
            if path.is_file() and path.name != ".phase.lock":
                package.write(path, path.relative_to(e).as_posix(),
                              compress_type=ZIP_STORED if path.suffix == ".zip" else ZIP_DEFLATED)
    with ZipFile(out / "evidence.zip") as package:
        if package.testzip() is not None:
            raise ValueError("Consolidated archive CRC error")
    artifacts = [p for p in sorted(out.iterdir()) if p.is_file() and p.name != "SHA256SUMS"]
    (out / "SHA256SUMS").write_text(
        "".join(f"{digest(p.read_bytes())}  {p.name}\n" for p in artifacts), encoding="ascii", newline="\n")


if __name__ == "__main__":
    main()
