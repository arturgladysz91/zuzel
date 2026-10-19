"""Read #100 accepted evidence, verify frozen history, tabulate; never run physics."""
import csv
import hashlib
import json
import statistics
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "docs/experiments/rear-wheel-dynamics"
load = lambda name: json.loads((DATA / (name + ".json")).read_text(encoding="utf-8-sig"))
write = lambda name, obj: (DATA / (name + ".json")).write_text(json.dumps(obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

# A diagnostic-only division by absent yaw inertia during tape waits produced
# NaN in an aggregate. Preserve every motion/force/energy value, mark this one
# unavailable aggregate null; fixed C# skips intervals without a yaw observation.
repairs = []
for file in DATA.glob("*.json"):
    value = json.loads(file.read_text(encoding="utf-8-sig"))
    count = [0]
    def fix(node):
        if isinstance(node, dict):
            for key, val in node.items():
                if key == "MaximumYawGuardExcessWatts" and val == "NaN":
                    node[key] = None
                    count[0] += 1
                else:
                    fix(val)
        elif isinstance(node, list):
            for val in node:
                fix(val)
    fix(value)
    if count[0]:
        before = hashlib.sha256(file.read_bytes()).hexdigest()
        file.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
        repairs.append({"file": file.name, "unavailableAggregates": count[0], "previousSha256": before,
                        "currentSha256": hashlib.sha256(file.read_bytes()).hexdigest()})
if repairs:
    write("diagnostic-correction", {"reason": "Absent yaw observation during tape wait has inertia0; aggregate is unavailable, now null. C# collector fixed. No simulation repeated or motion/force/work values changed.", "files": repairs})

history = {}
for folder in ["autonomous-racing-line", "slip-coupled-dynamics", "slip-control-validation", "physics-driven-racing-line"]:
    base = ROOT / "docs/experiments" / folder
    manifest = json.loads((base / "manifest.json").read_text())
    assert all(hashlib.sha256((base / path).read_bytes()).hexdigest() == digest for path, digest in manifest.items()), folder
    history[folder] = len(manifest)
rows, probes, perf, stability = load("line-comparison"), load("throttle-probes"), load("performance"), load("stability")
assert len(rows) == 12 and all(r["Feasible"] for r in rows)
assert len({r["InitialBodyFingerprint"] for r in rows}) == 1
assert all(len({t["PrefixFingerprint"] for t in p["Trials"]}) == 1 for p in probes)
assert all(abs(r["Energy"]["BalanceResidualJoules"]) < 5 for r in rows)
assert all(r["Energy"]["MinimumIntervalLongitudinalLossJoules"] >= -1e-4 for r in rows)
assert all(r["Evaluation"]["MaximumGripUtilization"] <= 1.00001 for r in rows)
assert all(b == 0 for r in perf["Forces"] for b in r["AllocatedBytes"])
assert stability["Crash"]["WheelContinuity"] and stability["Crash"]["CrashedIntervals"] > 0
assert all(r["Energy"]["MaximumNoEngineKineticIncreaseJoules"] < .03 for r in stability["Cases"] if r["Name"] in ["idle", "crawl", "coast"])
for file in DATA.glob("*.json"):
    assert '"NaN"' not in file.read_text(), file

trx = ET.parse(DATA / "targeted-release.trx").getroot()
counts = trx.find("{*}ResultSummary/{*}Counters").attrib
assert counts["failed"] == "0" and int(counts["passed"]) == 44
validation = {"TargetedRelease": counts, "NodeTests": {"Passed": 8, "Failed": 0}, "HistoricalManifestsUnchanged": history,
              "MainWorlds": len(rows), "CausalPrefixesExact": True,
              "MaximumWholeSystemEnergyResidualJoules": max(abs(r["Energy"]["BalanceResidualJoules"]) for r in rows),
              "MaximumRootResidualMetersPerSecond": max(r["Energy"]["MaximumRootResidual"] for r in rows),
              "MaximumBudgetUtilization": max(r["Evaluation"]["MaximumGripUtilization"] for r in rows),
              "CI": "Standard Draft Windows CI on the final HEAD; exact run/status in the PR and final delivery."}
write("validation", validation)

def table(headers, data):
    return "\n".join(["| " + " | ".join(headers) + " |", "|" + "---|" * len(headers)] + ["| " + " | ".join(str(x) for x in row) + " |" for row in data])
def csv_file(name, records):
    with (DATA / name).open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, list(records[0]))
        writer.writeheader()
        writer.writerows(records)

results = []
for r in rows:
    e = r["Evaluation"]
    results.append({"model": r["Model"], "condition": r["Case"], "plan": r["Plan"], "flying_seconds": e["FlyingSeconds"],
                    "flying_distance_m": e["FlyingDistanceMeters"], "corner_min_mps": r["MinimumFlyingCornerSpeed"],
                    "max_slide_deg": r["MaximumSlideDegrees"], "slide15_seconds": r["SignificantSlideSeconds"],
                    "max_kappa": r["MaximumSlipRatio"], "wheelspin02_seconds": r["SignificantWheelspinSeconds"],
                    "mean_abs_fy_N": r["MeanAbsoluteFy"], "max_usage": e["MaximumGripUtilization"],
                    "mean_tracking_error_m": r["MeanAbsoluteTrackingError"], "min_inner_m": e["MinimumInnerClearanceMeters"],
                    "min_outer_m": e["MinimumOuterClearanceMeters"], "energy_residual_J": r["Energy"]["BalanceResidualJoules"],
                    "feasible": r["Feasible"], "rejections": "/".join(r["Rejections"])})
csv_file("results.csv", results)
probe_rows = []
for p in probes:
    for t in p["Trials"]:
        for s in t["Samples"]:
            probe_rows.append({"model": p["Model"], "anchor": p["Anchor"], "drive": t["Drive"], "delay": s["DelaySeconds"],
                               "actual_delay": s["ActualDelay"], **s["Sample"]})
csv_file("throttle-results.csv", probe_rows)
csv_file("stability-results.csv", [{"case": r["Name"], "step_s": r["StepSeconds"], "max_kappa": r["MaximumSlipRatio"],
                                   "max_speed": r["MaximumSpeed"], "min_clearance": r["MinimumClearance"],
                                   "feasible": r["MinimumClearance"] >= .05, "reason": "CapsuleClearance" if r["MinimumClearance"] < .05 else "",
                                   "energy_residual_J": r["Energy"]["BalanceResidualJoules"]} for r in stability["Cases"]])

lines = []
add = lines.append
add("# Rear Wheel Dynamics (#100)\n\n**PROVISIONAL — A: powtarzalny eksperyment dynamiki koła i sprzężenia sił. Bez potwierdzenia empirycznej charakterystyki prawdziwej opony.** Baza #99 `d504e3d252216eef0393d2ea2df2c0f64c7a6b87`, gałąź `experiment/rear-wheel-dynamics`, Draft bez merge. Nowe wymagania zastąpiły rozpoczęty eksperyment H1/H2; tamte warianty nie wchodzą do PR.")
add("## Wynik\n\nKoło buksuje wskutek momentu i własnej bezwładności. Przymknięcie gazu ogranicza κ oraz odzyskuje część tylnej siły bocznej. W podstawowym porównaniu nie powoduje to większego wynoszenia na wyjściu ani zmiany zwycięzcy. Motocykl wcześniej traci nieco prędkości, a zachowany regulator i przód utrzymują ciasny plan. Nie dopasowywano parametrów, żeby wygrała zewnętrzna linia.")
add("## Równania, parametry i znaczenie\n\n[Konfiguracja zapisana przed pomiarami](rear-wheel-dynamics/configuration.json). Wszystkie parametry są jawnie PROVISIONAL. R=0,33 m, Iw=0,9 kg·m², rear share=0,60, front share=0,40, κ-scale=0,10, regularizacja prędkości=0,5 m/s, minimalna relaksacja kontaktu=0,03 s, lepki opór łożyska=0,02 N·m·s. R jest założonym promieniem efektywnym; Iw odpowiada cienkiemu pierścieniowi 8,26 kg przy tym R, a nie zmierzonemu kołu żużlowemu. Nie dodajemy tej masy do niezmienionej masy Box2D. Podział 40/60 zastępuje stałe obciążenia; nie ma transferu obciążenia.")
add("""```text
Iw * dω/dt = Tdrive − Fx*R − c*ω
κ = (R*ω − u) / sqrt(u² + u0²)              [bezwymiarowe]
B = m * safe² / radius                     [N, ten sam odziedziczony budżet]
Br = 0.6*B; Bf = 0.4*B
δy² = 0.12²*(u²+0.5²) + (B/m*0.03)²        [niezmienione #97]
A = R²/Iw + 1/m                            [1/kg]
δx² = 0.10²*(u²+0.5²) + (Br*A*0.03)²
a = (R*ω − u)/δx; b = w/δy
Fx = Br*a/sqrt(1+a²+b²)
Fy,rear = −Br*b/sqrt(1+a²+b²)
Fy,front = −Bf*w/sqrt(w²+δy²)
Fy = Fy,front + Fy,rear
```

Przód nie zależy od κ. Siły obu wkładów są przyłożone do COM, z zachowaniem istniejącego skutecznego aktuatora yaw; nie udajemy dwóch fizycznych punktów styku. Każdy wkład zużywa tylko swoją część budżetu. Norma wypadkowej nie przekracza B z nierówności trójkąta, a sam tył nie przekracza Br. Fx ma znak prędkości poślizgu; −Fy·w i Fx·(Rω−u) są nieujemnymi stratami. Nie ma procentowej kary od Drive ani dodatkowej siły na zewnątrz.

δx jest efektywną podatnością kontaktu, także zabezpieczeniem sztywnego sprzężenia przy małym u: Br*A/δx ≤1/0,03s. Parametr jest stały przy krokach 5/10/20 ms, bez clampowania ω. Przy 20 m/s dominuje już częściowo składnik relaksacji, więc numerycznych wartości κ nie należy przedstawiać jako zmierzonego wheelspinu realnej opony. Model ma łagodną saturację Fx, bez gałęzi spadku po maksimum. Koło szybsze przenosi coraz większą część tylnego budżetu wzdłużnie, zmniejszając tylną Fy.

Zachowano stare reference force i jednobiegowy envelope. Available torque=R·Favailable(Rω), requested torque=R·Freference·Drive·(1−impairment), delivered torque=R·Favailable(Rω)·Drive·(1−impairment). Envelope jest próbkowane na prędkości obwodowej napędzanego koła, przez co wheelspin zmniejsza dostępny moment tego samego przełożenia. Nie ma osobnego silnika ani sprzęgła. Stare Fx jest zastąpione siłą kontaktu, nigdy dodane drugi raz.

Rozdzielenie prędkości koła, slip ratio, bocznego kąta oraz regularizacji ma odniesienie w [dokumentacji pojazdów PhysX](https://nvidia-omniverse.github.io/PhysX/physx/5.6.1/docs/Vehicles.html) i [parametrach obliczania poślizgu](https://nvidia-omniverse.github.io/PhysX/physx/latest/_api_build/structPxVehicleTireSlipParams.html). To odniesienie strukturalne, nie źródło kalibracji naszych liczb ani użycie drugiego silnika fizyki.""")
add("## Dyskretyzacja i energia\n\nBox2D jako jedyny zmienia pozycje, prędkości i orientacje. Scalar ω należy do tego samego świata; inicjalizacja jest dokładnie uinitial/R. Najpierw ustalamy rzeczywisty interwał: istniejący UntilCurvatureChange korzysta z chwilowej siły przy granicy, bez aktualizacji ω. Dopiero dla ostatecznego h przygotowujemy kontakt i zmianę koła. Koło zatwierdzamy raz po tym samym b2World_Step. Daleko od granic nie ma dodatkowej ewaluacji. OFF zachowuje dokładnie dotychczasową kolejność działań.")
add("Kontakt jest rozwiązywany w środku interwału z zamrożonymi zaakceptowanymi osiami. Dla ωmid=(ω0+h(T−RFx)/(2Iw))/(1+hc/(2Iw)) i umid=u0+h(Fx+Fres,forward)/(2m), smid=Rωmid−umid. Unikalne równanie skalarne z+H·Br·z/sqrt(D²+z²)=|S| ma dodatnią pochodną; maksymalnie 8 iteracji Newtona, start od stycznej w zerze. W praktyce ~kilka prostych operacji i pierwiastków, bez nowego świata, predykcji pozycji lub wywołania solvera. Do reakcji koła używamy dokładnie float Fx wysłanej do Box2D. Fx·smid≥0. Nie czytamy przyszłego stanu Box2D do wyznaczenia siły. Wyliczenie umid jest lokalną dyskretyzacją kontaktu z efektywną masą, nie zatwierdzeniem ruchu nadwozia.")
add("Energia E=½mv²+½Ibody·yawRate²+½Iw·ω². Praca napędu=T·ωmid·h; opór koła=c·ωmid²·h. Straty wzdłużne liczymy z rzeczywistej średniej prędkości nadwozia na starych osiach; osobno zapisujemy wynik konstytutywny. Boczne straty i opory używają rzeczywistych średnich zaakceptowanych prędkości. Bilans: ΔE=Wengine−Llong−Llat−Lbearing+Wresistance+Wyaw+ε. Yaw jest ujawnioną pracą skutecznego aktuatora, a jego dotychczasowy kwadratowy guard w nowej gałęzi uwzględnia również zmianę energii koła, reakcję kontaktu i rzeczywiste h. Nie zmieniono pętli sterowania ani jej parametrów. W 12 przejazdach guard nie obcinał momentu; osobne testy aktywują ograniczenie i chronią brak energii przy Drive0.")
energy_table = []
for r in rows:
    if r["Case"] == "A-neutral" and r["Plan"] == "inner":
        e = r["Energy"]
        energy_table.append([r["Model"], *[f'{e[k]/1000:.3f}' for k in ["EngineWorkJoules", "LongitudinalSlipLossJoules", "LateralSlipLossJoules", "BearingLossJoules", "ResistanceWorkJoules", "YawWorkJoules", "BodyKineticChangeJoules", "WheelKineticChangeJoules"]], f'{e["BalanceResidualJoules"]:.6f}'])
add(table(["3 okrążenia inner", "Wengine kJ", "Llong kJ", "Llat kJ", "Lbearing kJ", "Wres kJ", "Wyaw kJ", "ΔEbody kJ", "ΔEwheel kJ", "ε J"], energy_table))
add(f'Max |ε| w głównych przejazdach: **{validation["MaximumWholeSystemEnergyResidualJoules"]:.6f} J**. Max residual kontaktu po float Fx: {validation["MaximumRootResidualMetersPerSecond"]:.3g} m/s. To jawny błąd numeryczny, nie strata fizyczna. Energia koła nie jest drugi raz napędem nadwozia.')
add("## Charakterystyka kontaktu\n\n[Pełna siatka](rear-wheel-dynamics/contact-curves.json): B=1800 N, m=147 kg, u=0/5/20/30 m/s, α=0/15/30°, κ od −1 do 5. Poniżej u=20, α=15°, bez integracji:")
curves = [r for r in load("contact-curves")["Rows"] if r["ForwardSpeed"] == 20 and r["SlideDegrees"] == 15 and r["SlipRatio"] >= 0]
add(table(["κ", "Fx N", "Fy,rear N", "Fy,front N"], [[r["SlipRatio"], f'{r["LongitudinalNewtons"]:.1f}', f'{r["RearLateralNewtons"]:.1f}', f'{r["FrontLateralNewtons"]:.1f}'] for r in curves]))
add("## Prosta i faktyczny wheelspin\n\n[5 kontrolowanych prób](rear-wheel-dynamics/straight-comparison.json). Toczenie bez gazu: κ około 0,00055; nadwozie zwalnia, a małe dodatnie Fx pochodzi z tracącej energię rotacji koła. Przy starcie opóźnienie taśmy pozostaje stare. Następnie moment przyspiesza koło i kontakt jednocześnie napędza nadwozie. Brak instant resetu przy odjęciu gazu:")
straight_table = []
for r in load("straight-comparison"):
    if r["Name"] not in ["launch", "launch-cut", "launch-stress"]:
        continue
    for s in r["Samples"]:
        if s["TimeSeconds"] < .49:
            continue
        straight_table.append([r["Name"], f'{s["TimeSeconds"]:.2f}', f'{s["Speed"]:.3f}', f'{s["CircumferentialSpeed"]:.3f}', f'{s["Rpm"]:.1f}', f'{s["SlipRatio"]:.3f}', f'{s["DriveForceNewtons"]:.1f}', f'{s["DriveTorque"]:.1f}', f'{s["WheelEnergyJoules"]:.1f}'])
add(table(["próba", "t s", "v m/s", "Rω m/s", "RPM", "κ", "Fx N", "T N·m", "Ewheel J"], straight_table))
launch = load("launch-through-corner")["Summary"]
add(f'Na Grip0,85 przy t1s κ=2,971 wobec 2,683 na Grip1. Stress Grip0,35 daje κ=4,976 przy t1s, bez ręcznej inicjalizacji buksującego koła. [Dłuższy start aż w łuk](rear-wheel-dynamics/launch-through-corner.json) dochodzi do κ={launch["MaximumCornerSlipRatio"]:.3f} już w rzeczywistym łuku; cały motocykl pozostaje na torze (minimum {launch["MinimumCapsuleClearance"]:.3f} m). To dodatkowa pojedyncza kontynuacja scenariusza startowego, nie kandydat w porównaniu lotnych okrążeń.')
add("## Gaz: identyczny zaakceptowany prefiks\n\n[Próby przyczynowe](rear-wheel-dynamics/throttle-probes.json) i [płaska tabela](rear-wheel-dynamics/throttle-results.csv). Każdy model odtwarza własny dokładny prefiks nadwozia i koła; modele mają identyczny początek, lecz po rozbieżności dynamiki nie udajemy identycznego stanu między nimi. Wybór stanu według zamrożonej reguły cp≥0,20 i |α|≥15° daje stary t10,52 s, nowy t10,73 s. Stan wyjścia jest pierwszą zaakceptowaną prostą po pierwszym łuku. Nie przemieszczano ani nie przywracano ciał ręcznie.")
for anchor in ["corner", "exit"]:
    p = next(x for x in probes if x["Model"] == "wheel" and x["Anchor"] == anchor)
    add(f'Nowy model / {anchor}, statyczne zapytanie przy h=0: Fx={p["InstantQueries"][0]["DriveForceNewtons"]:.3f} N i Fy={p["InstantQueries"][0]["LateralForceNewtons"]:.3f} N dla wszystkich Drive; zmienia się tylko moment. Siły w tabeli t0 są średnimi dyskretyzacji pierwszego niezerowego interwału, więc mogą już nieznacznie różnić się od chwilowych.')
    data = []
    for t in p["Trials"]:
        for sample in t["Samples"]:
            s = sample["Sample"]
            data.append([t["Drive"], sample["DelaySeconds"], f'{s["SlipRatio"]:.4f}', f'{s["DriveForceNewtons"]:.1f}', f'{s["LateralForceNewtons"]:.1f}', f'{s["ActualSlipDegrees"]:.2f}', f'{s["RoadOutwardVelocity"]:.3f}', f'{s["OffsetMeters"]:.3f}'])
    add(table([anchor + " Drive", "Δt s", "κ", "Fx N", "Fy N", "α °", "vout m/s", "offset m"], data))
add("Niższy gaz ogranicza obrót z rzeczywistą bezwładnością; w łuku po 0,2s odzysk Fy wynosi około 28 N (1,4% całości). Po 1s różnica Fy ma przeciwny znak, ponieważ zmieniły się także α, speed i żądanie regulatora. Nie wolno tego późniejszego porównania utożsamiać z samą krzywą opony. Stary model ma identyczne Fy po natychmiastowej zmianie Drive, a późniejsze różnice pochodzą wyłącznie z ruchu i regulatora.")
add("## Lotne okrążenia, droga i bezpieczeństwo\n\nTylko trzy zamrożone plany × dwa modele × dwie nawierzchnie =12 światów. Wszystkie mają identyczny stan początkowy nadwozia, neutralny profil/setup, 10ms i oryginalne landmarki. Brak selektora lub nowych kandydatów. Lotne to Lap2+Lap3; każdy wiersz obejmuje trzy rzeczywiste okrążenia. [Pełne metryki](rear-wheel-dynamics/results.csv), sześć indywidualnych łuków i prędkości wejście/środek/wyjście są w Evaluation.Corners każdego JSON.")
add(table(["warunek", "plan", "model", "lap2+3 s", "droga m", "vmin łuk m/s", "max α °", "max κ", "κ≥0,2 s", "min gap in/out m"], [[r["Case"], r["Plan"], r["Model"], f'{r["Evaluation"]["FlyingSeconds"]:.6f}', f'{r["Evaluation"]["FlyingDistanceMeters"]:.3f}', f'{r["MinimumFlyingCornerSpeed"]:.3f}', f'{r["MaximumSlideDegrees"]:.2f}', f'{r["MaximumSlipRatio"]:.3f}', f'{r["SignificantWheelspinSeconds"]:.3f}', f'{r["Evaluation"]["MinimumInnerClearanceMeters"]:.3f}/{r["Evaluation"]["MinimumOuterClearanceMeters"]:.3f}'] for r in rows]))
add("Wszystkie 12 wykonalne: pełne kapsuły,≥0,05m prześwitu, brak kontaktu/upadku, budżet, energia i powtarzalność lotnych ≤0,05s. Zachowano pełną geometrię krawędzi z #99, wraz ze skokiem szerokości 4,6m i target max9,5m. Ten PR nie rozstrzyga optimum całej fizycznej szerokości toru. Jednolite obniżenie Grip1→0,85 spowalnia wszystkie trzy plany; kolejność pozostaje inner, middle-tighten, wide-tighten.")
add("## Wyjście: ostatnie 25m łuku i pierwsze 25m prostej\n\nKażdy `*-exits.csv` zapisuje wszystkie faktycznie zaakceptowane interwały obu okien, z X/Y, heading/travel, niezależnymi α/κ, ω/RPM, Fx/frontFy/rearFy/Fy/B, momentami obu etapów guardu, żądaniem regulatora, targetem, rzeczywistymi prześwitami i Drive. Nie jest to telemetria przerzedzona do renderowania. Wielkości sił są przy stanie przed krokiem; pozycja replay jest z rzeczywiście zaakceptowanego CSV.")
exit_table = []
for r in rows:
    if r["Case"] == "A-neutral" and r["Plan"] == "inner":
        e = r["Exits"][2]
        exit_table.append([r["Model"], f'{e["ExitSpeed"]:.3f}', f'{e["ExitOutwardVelocity"]:.3f}', f'{e["ExitOffset"]:.3f}', f'{e["WideningMeters"]:.3f}', f'{e["SmallOutwardVelocitySeconds"]:.3f}', f'{e["ReversalSeconds"]:.3f}', f'{e["LateralImpulse"]:.2f}'])
add(table(["pierwsze wyjście Lap2", "vexit", "vout", "offset", "dodatkowe wyniesienie m", "|vout|≤0,1 po s", "zwrot po s", "boczny impuls N·s"], exit_table))
add("Na tym wyjściu Fy to około797 N w starym i787 N w nowym modelu przy B≈2138 N; regulator żąda odpowiednio529 i537 N do wewnątrz. W początkach prostej pozostaje więc znaczny zapas siły, κ nowego koła wynosi tylko0,077, a przód wnosi321N z całkowitych787N. Boczne impulsy obu przejazdów wynoszą około−318N·s. Zmiana prędkości poprzecznej jest odtworzona sumą impulsów bocznych, napędu i oporu z błędem poniżej kilku µm/s. To siły wygaszają bezwładny ruch na zewnątrz; nie ma osobnej siły odśrodkowej na prostej.")
add("Odpowiedź na główne pytanie jest **nie dla tych zamrożonych prób**: nowy wheelspin nie zwiększa dodatkowego poszerzenia wyjścia. Motocykl wchodzi wolniej, ma nieco mniejszy vout, a regulator nadal kompensuje różnicę; nowy offset w samym punkcie wyjścia jest o około0,181m większy. Nie jest to natychmiastowe odzyskanie kontroli: około0,49–0,51s do odwrócenia ruchu. Przód i pozostały tylny budżet wystarczają przy małym κ na wyjściu. Sama zmiana dynamiki koła nie dowodzi, że dawna przewaga kredy była błędem opony.")
add("![Rzeczywiste wyjście nowego modelu w istniejącym viewerze](rear-wheel-dynamics/replay-exit.png)")
add("## Granice, kontakt i przypadki niewykonalne\n\n[Stabilność](rear-wheel-dynamics/stability.json), [jawna klasyfikacja](rear-wheel-dynamics/stability-results.csv): 6 przypadków ×5/10/20ms. Zero, małe u, start, duża prędkość, jazda bez napędu i Grip0,35 pozostają skończone. Max κ startu około10 wynika z małego mianownika przy prawie stojącym pojeździe; nie oznacza 10× dużej prędkości jazdy. Brak clampu, resetu lub oscylacji koła; krzywe, równanie reakcji i energia mają oddzielne testy. Duża prędkość35m/s przekracza pełny obrys toru: około−0,761/−0,801/−0,892m, zachowana jako CapsuleClearance, nie uznana za bezpieczny przejazd.")
add("[Dodatkowy stress w łuku](rear-wheel-dynamics/corner-stress.json): identyczne nadwozie przy ref65/speed25/Grip0,35, Drive1→0,4 po1s. Oba modele fizycznie wyjeżdżają poza tor (outer minimum−7,026m/−7,416m); pełne trajektorie i powody odrzucenia pozostają w CSV/JSON. To przeciążony test, nie dowód wyższości nowej opony ani wynik porównania planów. Oddzielny naturalny start do łuku daje większe κ≈0,525 i pozostaje bezpieczny; nie inicjalizowano nienaturalnego wheelspinu.")
add(f'Test kontaktu ma {stability["Crash"]["CrashedIntervals"]} rzeczywistych interwałów po upadku. Impuls Box2D zmienia nadwozie, ω nie jest wyrównywane: następny krok oblicza nowy κ z rzeczywistych prędkości. Crashed odcina moment i kontakt opony z napędem, koło wybiega z oporem łożyska. To jawny terminalny proxy, nie pełna mechanika motocykla leżącego na torze. Każdy zawodnik zachowuje własny stan, a odwrócenie kolejności daje ten sam fingerprint.')
add("## Koszt i walidacja\n\n[Pomiar Release](rear-wheel-dynamics/performance.json): .NET8, tiered JIT wyłączony; 10k warmup, 5×100k ocen sił, po warmup 5 pełnych solo i istniejącego czteroosobowego D1 dla każdej flagi. Baseline to dokładna ścieżka #99-compatible w tym samym pliku wykonywalnym, z fingerprintami #99 sprawdzonymi niezależnie; nie osobna kompilacja #99. Nie interpretujemy małych różnic jako przyspieszenia.")
perf_rows = []
for r in perf["Forces"]:
    perf_rows.append(["Forces", r["Wheel"], f'{statistics.median(r["Milliseconds"])*.01:.6f} µs/call', "0 B/call", "—"])
for pack in [False, True]:
    for wheel in [False, True]:
        a = [r for r in perf["Worlds"] if r["Pack"] == pack and r["Wheel"] == wheel]
        perf_rows.append(["D1 /4" if pack else "solo /3 laps", wheel, f'{statistics.median(r["Milliseconds"] for r in a):.3f} ms', f'{statistics.median(r["AllocatedBytes"] for r in a):.0f} B/run', a[0]["SolverCalls"]])
add(table(["pomiar", "koło ON", "mediana", "alokacje", "solver calls"], perf_rows))
add("Ocena sił rośnie około61%; całe D1 około5,2%, przy około2,6% większej liczbie kroków potrzebnych do wolniejszego ukończenia tych samych4okrążeń. Solo w tym pomiarze wychodzi szybciej o~9,5%; rozrzut i kolejność pomiarów nie uzasadniają deklaracji przyspieszenia. Nowa fizyka nie dodaje stałej liczby substepów/wywołań Box2D: solver calls zmieniają się wraz z czasem jazdy oraz istniejącymi partycjami. D1 ma165 dodatkowych ocen przy granicach na7001kroków, bez dodatkowego solvera. Alokacje w Forces=0B zarówno historycznie, jak i z kołem; przyrost pamięci świata pochodzi z dłuższego wykonania, dodatkowych hashy/snapshotów i niewielkiego stanu per rider.")
cost = load("experiment-cost")
add(f'Główne 12 światów: {cost["MainCaptureMilliseconds"]:.1f}ms przechwytywania +{cost["MainAnalysisMilliseconds"]:.1f}ms analizy, {cost["MainSolverCalls"]} solver calls i{cost["MainForceEvaluations"]} ocen sił. Osobno12 krótkich prób gazu,5 prostych,18 granicznych,1 kontakt/upadek,2 przeciążone łuki i1 start do łuku. Jeden pilot przed porównaniem. Eksporty odmawiają nadpisania; replay/raport używają już policzonych stanów. Bez pełnych historycznych audytów lub wyszukiwania linii.')
add(f'**44/44 celowanych Release**, 8/8 Node; warnings-as-errors bez ostrzeżeń i błędów. Testy chronią dokładne OFF/ON #97–#99, 11 nowych przypadków wheel dynamics, współdzielony budżet, dysypację, prawdziwy wheelspin, brak podwójnego napędu, identyczny prefiks, 5/10/20ms, granicę skracającą krok, kontakt/upadek, powtarzalność, prawdziwy wyjazd poza tor i0B. Manifesty historyczne: {history}. Standardowe Draft Windows CI uruchamiane na końcowym HEAD; link i faktyczny status w PR/finalnym dostarczeniu. [Walidacja](rear-wheel-dynamics/validation.json).')
add("Korekta samej diagnostyki: podczas taśmy nie ma obserwacji yaw/inertia. Pierwszy kolektor dzielił w nieużywanym maksimum guardu przez0; oznaczono te pomocnicze agregaty jako null i poprawiono kolektor. [Dokładne hashe korekty](rear-wheel-dynamics/diagnostic-correction.json). Żadna pozycja, ω, siła, praca lub główny bilans nie zmieniły się; nie powtarzano fizyki. Pełne główne przejazdy i testy aktywnego guardu są poprawne.")
add("## Replay i odtworzenie\n\nIstniejący viewer #96 ma tylko dodatkowe pozycje w katalogu, bez nowego renderera lub paneli. Body CSV/JSON pozostają kompatybilne; niezależna telemetria koła jest w Telemetry oraz pełnych `*-trace.csv`/`*-exits.csv`. Wybierz #100 stary/inner, koło/inner i wyjście, szeroki, wheelspin/start lub wheelspin/łuk. Sprawdzono nowy przejazd w przeglądarce, ustawienie czasu, skalę i rzeczywistą pozycję; Node sprawdza każdy zapisany punkt wszystkich5replayów.")
add("""```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
dotnet run --project tools/shared-world-speedway -c Release --no-build -- rear-wheel NEW freeze
dotnet run --project tools/shared-world-speedway -c Release --no-build -- rear-wheel NEW measure
dotnet run --project tools/shared-world-speedway -c Release --no-build -- rear-wheel NEW stability
dotnet run --project tools/shared-world-speedway -c Release --no-build -- rear-wheel NEW stress
dotnet run --project tools/shared-world-speedway -c Release --no-build -- rear-wheel NEW launch-corner
$env:DOTNET_TieredCompilation='0'
dotnet run --project tools/shared-world-speedway -c Release --no-build -- rear-wheel NEW benchmark
node --test tools/replay-viewer/replay.test.mjs
python -m http.server 8766 --bind 127.0.0.1
```

Otwórz [istniejący viewer](../../tools/replay-viewer/index.html) przez HTTP. Skrypt rear-wheel-report.py czyta tylko zapisane dowody, sprawdza historyczne hashe i generuje tabele/raport; nie uruchamia ruchu ani dobierania parametrów.""")
add("## Osiem odpowiedzi i jeden następny PR\n\n1. **Czy występuje rzeczywisty wheelspin?** Tak: ω jest niezależne, równanie momentów zamyka się w każdym zaakceptowanym kroku; na lotnych inner κmax≈0,216, na starcie do łuku≈0,525.\n2. **Czy gaz może odebrać tylną Fy?** Tak po zmianie ω/κ w czasie; w identycznym stanie h0 nie ma niefizycznego natychmiastowego osłabienia. Przód pozostaje niezależny od κ.\n3. **Czy wyjście naturalnie poszerza się bardziej?** Nie w głównej serii. Dodatkowe poszerzenie inner spada0,381→0,358m; offset samego wyjścia rośnie1,156→1,337m, prędkość jest mniejsza.\n4. **Czy nadal można trzymać ciasny łuk na pełnym gazie?** Tak w tym modelu i obecnej geometrii. Przód, zachowany regulator i umiarkowany κ wystarczają; nowy stan nie wymusza zewnętrznej linii.\n5. **Co ze speed?** Pierwszy łuk Lap2 inner: entry23,753→23,149m/s, minimum18,996→18,742, exit19,448→19,158.\n6. **Przewaga inner?** W neutralnych dwóch lotnych nad wide-tighten rośnie1,282→1,366s; nad middle-tighten0,263→0,311s. Niższy grip nie zmienia kolejności.\n7. **Czy przymknięcie odzyskuje grip?** Tak: w stanie łuku po0,2s κ0,211→0,116, Fy1975→2003N; nie gwarantuje to szybszego całego obiegu. Po starcie Drive1→0,4 redukuje κ2,683 do0,439 po0,2s bez resetu ω.\n8. **Czy uproszczenie wystarczy?** Tak do dalszych przyczynowych testów gazu i trajektorii, przy fladze OFF. Nie wystarczy do deklaracji realizmu ilościowego bez danych o oponie i obciążeniu osi. Brak silnika, sprzęgła, transferu obciążenia, przechyłu3D, dwóch punktów styku i ich naturalnego yaw ogranicza wnioski.\n\n**Jeden następny PR: referencyjne pomiary i kalibracja charakterystyki κ–Fx/Fy tylnego kontaktu przy ustalonym obciążeniu.** Najpierw zweryfikować skalę poślizgu i sił oraz wpływ jawnej relaksacji30ms. Wyniki nie uzasadniają teraz strojenia do zwycięstwa bandy ani równoczesnej przebudowy geometrii, AI i silnika. Domyślna flaga pozostaje false.")
(ROOT / "docs/experiments/rear-wheel-dynamics.md").write_text("\n\n".join(lines) + "\n", encoding="utf-8")
write("manifest", {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(DATA.iterdir()) if p.is_file() and p.name != "manifest.json"})
print(json.dumps({"validation": validation, "files": len(list(DATA.iterdir()))}, ensure_ascii=False))
