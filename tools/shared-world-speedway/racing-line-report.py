"""Read measured #99 evidence only; no simulation, fitting or candidate selection."""
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT / "docs/experiments/physics-driven-racing-line"
load = lambda name: json.loads((FOLDER / name).read_text(encoding="utf-8-sig"))
stage1, stage2 = load("stage1.json"), load("stage2.json")
rows = stage1 + stage2
plans = load("plans.json")
negative = load("rejected.json")["Evaluation"]
historic = {}
for folder in ["autonomous-racing-line", "slip-coupled-dynamics", "slip-control-validation"]:
    base = ROOT / "docs/experiments" / folder
    manifest = json.loads((base / "manifest.json").read_text())
    checks = {name: hashlib.sha256((base / name).read_bytes()).hexdigest() == digest for name, digest in manifest.items()}
    if not all(checks.values()):
        raise RuntimeError(f"Historical manifest changed: {folder}")
    historic[folder] = len(checks)
assert all(r["Feasible"] and r["OutsideSamples"] == 0 for r in rows)
for case in plans["Conditions"]:
    assert len({r["InitialFingerprint"] for r in rows if r["Case"] == case["Name"]}) == 1
assert not negative["Feasible"] and "CapsuleClearance" in negative["Rejections"]
validation = {"TargetedRelease": {"Passed": 33, "Failed": 0}, "NodeReplayTests": {"Passed": 7, "Failed": 0},
              "HistoricalManifestsUnchanged": historic, "Stage1Runs": len(stage1), "Stage2Runs": len(stage2),
              "MaximumEnergyResidualJoules": max(abs(r["BalanceResidualJoules"]) for r in rows),
              "MaximumActivationOvershootMeters": max(a["OvershootMeters"] for r in rows for a in r["Activations"]),
              "MinimumActivationOvershootMeters": min(a["OvershootMeters"] for r in rows for a in r["Activations"]),
              "MaximumFlyingLapDifferenceSeconds": max(abs(r["LapSeconds"][1] - r["LapSeconds"][2]) for r in rows),
              "NormalReplay": load("cost.json")["NormalReplay"],
              "ReplayFingerprintsExact": all(r["Fingerprint"] == r["ReplayFingerprint"] for r in load("replays.json")),
              "CI": "Standard Draft Windows CI; exact final HEAD and run link in PR/final delivery."}
(FOLDER / "validation.json").write_text(json.dumps(validation, indent=2) + "\n", encoding="utf-8")
with (FOLDER / "results.csv").open("w", newline="", encoding="utf-8") as f:
    writer = csv.writer(f)
    writer.writerow(["stage", "case", "plan", "preparation_drive", "lap1_s", "lap2_s", "lap3_s", "total_s", "flying_s", "flying_distance_m", "min_inner_m", "min_outer_m", "max_usage", "slip_loss_j", "energy_residual_j", "feasible", "rejection"])
    for r in rows + [negative]:
        writer.writerow([1 if r in stage1 else 2 if r in stage2 else "negative-control", r["Case"], r["Plan"]["Name"], r["Plan"]["PreparationDrive"], *r["LapSeconds"], sum(r["LapSeconds"]), r["FlyingSeconds"], r["FlyingDistanceMeters"], r["MinimumInnerClearanceMeters"], r["MinimumOuterClearanceMeters"], r["MaximumGripUtilization"], r["SlipLossJoules"], r["BalanceResidualJoules"], r["Feasible"], "/".join(r["Rejections"])])
for field, filename in [("Phases", "phases.csv"), ("Corners", "corners.csv"), ("Activations", "activations.csv")]:
    keys = list(rows[0][field][0])
    with (FOLDER / filename).open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=["case", "plan", "drive"] + keys)
        writer.writeheader()
        for r in rows:
            for item in r[field]:
                writer.writerow({"case": r["Case"], "plan": r["Plan"]["Name"], "drive": r["Plan"]["PreparationDrive"], **item})

main = [r for r in stage1 if r["Case"] == "A-neutral"]
lines = []
add = lines.append
add("# Physics-Driven Racing Line (#99)\n\n**PROVISIONAL — B: poprawny selektor ograniczonego zbioru; geometria blokuje wniosek o pełnej szerokości łuku.** Baza #98 `b01d819fddc9e81a30e961517c27f952bb96dad4`, Draft bez merge. Produkcyjne src/CoreSim, SlipCoupledDynamics, ContactAdapter, TrackFrame, scoring taktyczny i parametry domyślne pozostają bez zmian.")
add("\n## Najpierw geometria")
add("\n[Audyt zapisany przed porównaniami](physics-driven-racing-line/geometry-audit.json): referencja leży 1 m od kredy, zatem fizyczna krawędź wewnętrzna to offset −1 m. Prosta: krawędź zewnętrzna +11 m, usable 10 m. Łuk: +15,6 m, usable 14,6 m. Historyczny target max=9,5 m blokuje 4,6 m dodatkowego zakresu użytkowego łuku. Nie zmieniono go globalnie ani wprowadzonym interfejsem.")
add("\nWyprostowany motocykl przy target=9,5 m miałby 1,1 m prześwitu do bandy na prostej i 5,7 m na łuku. Rzeczywisty wynik zależy od obrysu: chassis 2,10×0,30 m oraz kierownica 0,80 m przesunięta o 0,65 m; obrót zwiększa poprzeczny zasięg. Blisko bandy oznacza w tym raporcie cały obrys w odległości ≤1,5 m, a nie konkretny offset. To próg diagnostyczny, nie bonus w celu.")
add("\nEksport natywny ma cztery skoki końców zewnętrznych krawędzi po 4,600000 m: ref35, 132,389375, 194,389375 i 291,778750. Krawędź wewnętrzna jest ciągła. Szerokość nie ma spline ani strefy przejścia. Stały fizyczny offset daje ciągłą pozycję motocykla, ale nie ciągłą gładką bandę. Samo podniesienie targetu do 14,1 m mogłoby zostawić motocykl poza progiem w momencie powrotu na prostą.")
add("\nDlatego wynik dotyczy jawnego podzbioru 0,5–9,5 m, a nie optimum całej fizycznej powierzchni. Nie rozszerzono targetów i nie uznano ich za odległość od bandy. Offline RacingLineGeometry mierzy dystans obu kapsuł do natywnych odcinków i łuków oraz konserwatywnie do odcinków łączących końce skoków szerokości. Nie dodaje band/sił do świata ani nie maskuje braku gładkiej geometrii. Analiza segment–łuk jest analityczna; odległości pomniejsza tylko zapas numeryczny 0,1 mm. Dodatkowy warunek wykonalności to 5 cm prześwitu. Sprawdzany jest cały motocykl, także zanim jego środek przekroczy granicę segmentu. Ocena B zachowuje tę blokadę niezależnie od zwycięzcy.")
add("\n## Zbiór i selekcja")
add("\n[Zamrożony plan, warunki i profile](physics-driven-racing-line/plans.json): 12 planów × 4 warunki = 48 podstawowych światów. Plan zawiera offset prostej/wejścia/apexu/wyjścia i kilka jawnych progów geometrycznych. Wszystkie podstawowe próby Drive=1. Bez losowania, dopisywania kandydatów po wynikach ani lokalnego przeszukiwania. Nazwa nie jest argumentem oceny: selektor używa rzeczywistych Lap2+Lap3, a tylko przy dokładnym remisie liczbowej definicji planu.")
add("\nKażda próba zaczyna od ref0, offset5 m, 18 m/s, kierunku referencji, zerowego spinu i prędkości bocznej, target5, Drive1, neutralnego setupu i tej samej nawierzchni w obrębie porównania. Masa 70+77 kg. SHA dokładnych początkowych stanów jest identyczny w każdym warunku we wszystkich wariantach gazu i planach. Pierwsze okrążenie rozgrzewa/przygotowuje stan; dwa następne są celem, a ich różnica musi być ≤0,05 s. Nie przenosi się korzystnego stanu końcowego jednego planu do startu innego.")
add("\nSpatialControlCommand jest osobnym, opcjonalnym interfejsem narzuconych poleceń ON. Kompilator zna geometrię i powtarza progi na trzech obiegach. Activate sprawdza wyłącznie bieżący zaakceptowany postęp; zmiana następuje na pierwszej granicy solvera po przekroczeniu progu. Nie dochodzi nowy krok ani predykcja czasu przekroczenia. Polecenia mają zerową zwłokę autorską, tak samo jak ControlCommand w #98; nie są decyzjami reaktywnej AI. Dotychczasowe opóźnienia AI i ControlCommand pozostają bez zmian. Kontrolę kolejności każdego zawodnika prowadzi oddzielny kursor. Nie ma rewindu, przesuwania ciał ani integracji AutonomousRacingLine ze slip ON.")
add(f"\nMaksymalny przestrzenny overshoot aktywacji wynosi {validation['MaximumActivationOvershootMeters']:.6f} m, bez ujemnych przekroczeń poza śladową arytmetyką float. Log podaje próg, zaakceptowany postęp, czas i aktuatory. Największa różnica dwóch lotnych okrążeń wynosi {validation['MaximumFlyingLapDifferenceSeconds']:.6f} s. Czas przekroczenia mety i dystans zamkniętego interwału są pomiarową interpolacją między wykonanymi stanami; nie stanowią nowej pozycji w symulacji.")
add("\n| Plan | Prosta / entry / apex / exit m | Przygotowanie | Apex / exit cp | A: lap1 / lap2 / lap3 s | A: cel 2+3 s |\n|---|---|---|---|---|---:|")
for r in main:
    p = r["Plan"]
    add(f"| {p['Name']} | {p['Straight']}/{p['Entry']}/{p['Apex']}/{p['Exit']} | {p['PreparationMeters']} m przed lub cp {p['EntryFraction']} | {p['ApexFraction']}/{p['ExitFraction']} | " + "/".join(f"{v:.6f}" for v in r["LapSeconds"]) + f" | {r['FlyingSeconds']:.6f} |")
add("\n## Wynik czasu, drogi i wyjścia")
add("\nWszystkie 48 podstawowych i 16 wariantów gazu kończą trzy obiegi, bez naruszeń obrysu, kontaktów i Crash. Zwycięża **inner**: target1 m przez cały cykl. To wynik ograniczonego zbioru, a nie twierdzenie o globalnie optymalnej linii. Nie nadano mu premii za kredę ani nie dodano bandzie bonusu. [Wszystkie okrążenia/czasy/odrzucenia](physics-driven-racing-line/results.csv), [pełny etap 1](physics-driven-racing-line/stage1.json).")
add("\n| Warunek | inner lap2+3 s | middle s | wide s | wide-tighten s | Zwycięzca |\n|---|---:|---:|---:|---:|---|")
for c in plans["Conditions"]:
    rr = {r["Plan"]["Name"]: r for r in stage1 if r["Case"] == c["Name"]}
    add(f"| {c['Name']} | " + " | ".join(f"{rr[p]['FlyingSeconds']:.6f}" for p in ["inner", "middle", "wide", "wide-tighten"]) + " | inner |")
add("\nA: legacy i canonical 50; B: ten sam zawodnik, Grip0,85/Ruts0/Moisture0,5, EffectiveGrip0,7675 zamiast 0,9175. C: dwa nieekstremalne profile 40/60 zmieniają istniejące legacy Speed/SlideControl i canonical Technique, pozostałe wartości 50, ta sama masa/setup/surface. To próba istniejących zależności, nie przypisanie osiągów prawdziwemu zawodnikowi. Zwycięzca nie zmienia się z warunkami; test zmiany nazw i kolejności ocen wyklucza wybór po nazwie. Czasy i wykonanie jednak zmieniają się zgodnie z modelem.")
add("\n| A, plan | Droga lap2+3 m | Entry / min / exit m/s (pierwszy łuk lap2) | Max następnej prostej m/s | Min inner / outer m | Usage max | Slip loss 3 obiegi kJ |\n|---|---:|---|---:|---|---:|---:|")
for r in main:
    if r["Plan"]["Name"] not in ["inner", "middle", "wide", "wide-tighten", "early-open-exit"]:
        continue
    c = next(x for x in r["Corners"] if x["Lap"] == 2)
    add(f"| {r['Plan']['Name']} | {r['FlyingDistanceMeters']:.3f} | {c['EntrySpeed']:.3f}/{c['MinimumSpeed']:.3f}/{c['ExitSpeed']:.3f} | {c['NextStraightMaximumSpeed']:.3f} | {r['MinimumInnerClearanceMeters']:.3f}/{r['MinimumOuterClearanceMeters']:.3f} | {r['MaximumGripUtilization']:.6f} | {r['SlipLossJoules']/1000:.3f} |")
add("\nSzeroki stały plan daje większą prędkość wyjścia i późniejszej prostej, ale wydłuża dwa lotne obiegi o około 104,5 m i przegrywa o 2,219 s. Szeroko→zacieśnienie odzyskuje część kosztu względem stałej zewnętrznej, jednak nadal przegrywa z wewnętrzną. Wcześniejsze otwarcie exit nie pomaga: większy koszt drogi i przejścia pozostaje. Budżet safe²/R pochodzi z istniejącego modelu; przy zbliżonej sile bocznej większy promień zwiększa speed mniej niż proporcjonalnie do długości łuku. To mechaniczne wyjaśnienie badanych wyników, a nie uniwersalna reguła realnego żużla.")
add("\n## Fazy, uślizg i energia")
add("\nPoniżej fazy lotnych okrążeń A (początek prostej = pierwsze 12 m za łukiem; przygotowanie = ostatnie 15 m przed nim). To obserwacje, bez dodatkowego sterowania w tych granicach. W przedziałach 3/2° liczymy odpowiednio jazdę wyłamaną przy rosnącej rzeczywistej speed oraz prawie wyprostowaną. Nie ma nagrody za mały kąt. [Wszystkie warunki/plany/fazy](physics-driven-racing-line/phases.csv), [każdy łuk](physics-driven-racing-line/corners.csv).")
add("\n| Plan / faza | Śr./max abs α ° | Śr./max target ° | Śr. speed m/s | Śr. Fx/Fy N | Near outer udział | α≥3° i przyspieszanie s | α≤2° s |\n|---|---|---|---:|---|---:|---:|---:|")
for r in main:
    if r["Plan"]["Name"] not in ["inner", "wide"]:
        continue
    for p in r["Phases"]:
        add(f"| {r['Plan']['Name']} / {p['Phase']} | {p['MeanAbsoluteSlipDegrees']:.3f}/{p['MaximumAbsoluteSlipDegrees']:.3f} | {p['MeanAbsoluteTargetSlipDegrees']:.3f}/{p['MaximumAbsoluteTargetSlipDegrees']:.3f} | {p['MeanSpeed']:.3f} | {p['MeanFxNewtons']:.1f}/{p['MeanFyNewtons']:.1f} | {p['NearOuterFraction']:.1%} | {p['SlidingAndAcceleratingSeconds']:.3f} | {p['AlmostStraightSeconds']:.3f} |")
add("\nPreview statycznej geometrii zaczyna żądać uślizgu przed wejściem, ale nie oznacza natychmiastowego wykonania kąta. Widać też utrzymujący się uślizg po wyjściu i jego wygaszenie dopiero na prostej. W części wyjścia motocykl jest jeszcze wyłamany i przyspiesza: nie należy utożsamiać dodatniej speed z pełnym wyprostowaniem. Wcześniejszy exit-target obniża żądanie boczne, ale nie nadaje niezależnie kąta oponie. Nie zmieniono obu czasów regulatora ani guardu yaw.")
add("\n![Szeroki przejazd w istniejącym viewerze](physics-driven-racing-line/replay-wide.png)")
add("\nPraca jest rozłożona według faktycznie przyłożonych Fx/Fy, oporu i momentu oraz średniej zaakceptowanych prędkości danego kroku. ΔK zawiera translację i obrót. Osobno raportujemy modelową całkę straty bocznej z początku kroku. Bilans obejmuje pełne zaakceptowane wykonanie, z ewentualnym ostatnim fragmentem kroku za linią mety; cel czasu i droga zamknięta używają przekroczenia, a statystyki faz pomijają ułamki krańcowych próbek lotnego okna. Ich błąd czasowy jest ograniczony krokiem 10 ms.")
add("\n| A, plan | W drive / lateral / resistance / yaw kJ | ΔK kJ | ΔK−W J |\n|---|---|---:|---:|")
for r in main:
    if r["Plan"]["Name"] in ["inner", "wide", "wide-tighten"]:
        add(f"| {r['Plan']['Name']} | " + "/".join(f"{r[k]/1000:.3f}" for k in ["DriveWorkJoules", "LateralWorkJoules", "ResistanceWorkJoules", "YawWorkJoules"]) + f" | {r['KineticChangeJoules']/1000:.3f} | {r['BalanceResidualJoules']:.6f} |")
add(f"\nNajwiększa bezwzględna pozostałość w 64 przejazdach: {validation['MaximumEnergyResidualJoules']:.6f} J. Kryterium zamrożone przed pomiarem: max(5 J, 0,1% pracy napędu). Nie ma dodatkowej siły odśrodkowej, clampowania speed do idealnego promienia ani nowego źródła energii.")
add("\n## Kontrolowany gaz")
add("\nPrzed pomiarem ustalono Drive1/0,7/0,4. Etap 2 dotyczy zwycięzcy etapu 1 oraz inner/wide, po usunięciu duplikatu zwycięzcy=inner: dwa plany × cztery warunki × dwa nowe poziomy =16 światów. Drive1 wykorzystuje już wykonane dane. Gaz jest obniżany od landmarku przygotowania do cp0,20, potem otwierany do 1; brak hamulca i niezależnego wheelspinu.")
add("\n| Warunek / plan | Drive1 lap2+3 s | Drive0,7 s | Drive0,4 s |\n|---|---:|---:|---:|")
for condition in plans["Conditions"]:
    for name in ["inner", "wide"]:
        a = next(r for r in stage1 if r["Case"] == condition["Name"] and r["Plan"]["Name"] == name)
        b = [next(r for r in stage2 if r["Case"] == condition["Name"] and r["Plan"]["Name"] == name and abs(r["Plan"]["PreparationDrive"]-drive)<.001) for drive in [.7,.4]]
        add(f"| {condition['Name']} / {name} | {a['FlyingSeconds']:.6f} | {b[0]['FlyingSeconds']:.6f} | {b[1]['FlyingSeconds']:.6f} |")
add("\nTylko B/inner0,7 daje małą poprawę (~0,023 s na dwa obiegi); nie zwiększano liczby prób, aby ją dopasować. Nie jest to rekomendacja kalibracji gazu na podstawie milisekund. Pozostałe przymknięcia tracą czas. Wszystkie podstawowe próby były już wykonalne, więc ten zestaw nie dowodzi uratowania niewykonalnego łuku gazem. Mniejszy Drive zmienia późniejszą speed, kąt i offset, ale nie steruje niezależnie Fy w tym samym stanie. W pełnych tabelach faz można oddzielić utratę wejściową od wyjściowej; sam mniejszy kąt nie daje automatycznie lepszego czasu.")
add("\n## Ujemna kontrola i replay")
add(f"\nOddzielna kontrola testowa target0,5 (nie dopisana do 12 kandydatów po poznaniu wyników): czas lotnych {negative['FlyingSeconds']:.6f} s, ale prześwit wewnętrzny {negative['MinimumInnerClearanceMeters']:.6f} m i {negative['OutsideSamples']} próbek naruszenia. Ten szybszy przejazd selektor odrzuca. Nie przesunięto motocykla z powrotem na tor. Jest zachowany jako replay odrzuconej kontroli.")
add("\nIstniejący viewer ma tylko dodatkowe pozycje w liście: #99 wybrany, wewnętrzny, szeroki i odrzucona kontrola. CSV zawiera rzeczywiste X/Y i body heading. JSON zachowuje początek, zdarzenia, trzy indywidualne okrążenia, metę, energię i rzadsze rzeczywiste próbki α/target. Nie ma nowego renderera. Naprawa graniczna: komenda aktywowana dokładnie w t próbki (w tym t0) ma pierwszeństwo przed aktuatorami próbki sprzed Activate; pozycja pozostaje zapisana/interpolowana, bez teleportu. 7 testów Node obejmuje także ten przypadek i wszystkie zapisane pozy.")
add("\nUruchom z repo `python -m http.server 8766 --bind 127.0.0.1`, otwórz [viewer](../../tools/replay-viewer/index.html) przez HTTP i wybierz #99. Sprawdzono w przeglądarce wbudowanej: szeroki/odrzucony/wybrany, suwak, stan t0, śledzenie i zoom. [Widok szerokiego przejazdu](physics-driven-racing-line/replay-wide.png). Źródła: [selected](physics-driven-racing-line/selected.json), [inner](physics-driven-racing-line/inner.json), [wide](physics-driven-racing-line/wide.json), [rejected](physics-driven-racing-line/rejected.json) i odpowiadające pliki `*-accepted.csv`. Dwa aliasy selected/inner celowo mają tę samą trajektorię zwycięzcy; wszystkie trzy odtworzenia i normalny bieg bez diagnostyki zachowują fingerprint/kroki pierwotnego pomiaru.")
cost = load("cost.json")
add("\n## Koszt, regresje i odtworzenie")
add(f"\n64 światy porównawcze: {cost['CaptureMilliseconds']/1000:.3f} s przechwytywania z istniejącymi obserwatorami oraz {cost['ValidationMilliseconds']/1000:.3f} s dodatkowej geometrii/metryk offline. Wybór z czterech list wyników: {cost['selectionMilliseconds']:.3f} ms. Zwykłe odtworzenie zwycięzcy: {cost['NormalReplay']['ElapsedMilliseconds']:.3f} ms, {cost['NormalReplay']['SolverCalls']} wywołań, 0 wywołań optymalizatora podczas biegu. To pojedynczy kontrolowany pomiar kosztu, nie szeroki benchmark. Osobno wykonano 3 krótkie warianty kontrolne przed pełną serią, 3 powtórzenia replay, jedno bez obserwatorów i ujemną kontrolę; nie powtarzano całego eksperymentu przy korektach metadanych replay.")
add(f"\n33/33 celowane Release: 6 nowych +27 #97/#98/geometrii. Build Release --warnaserror: 0 warnings/errors. 7/7 Node. Bez nowych alokacji w Forces (kod niezmieniony; istniejący test 0 B). Puste plany przestrzenne zachowują bitowo OFF/ON #97/#98; niezmienione polecenie przestrzenne ma ten sam fingerprint i solver calls co brak poleceń. Testy chronią rzeczywiste krawędzie/kapsuły/skok szerokości, przyczynową aktywację, brak teleportu, identyczny początek, wybór po zmierzonym czasie, odrzucenie fizycznie niepoprawnego, powtórzenie i odwrócenie zawodników. Historyczne manifesty: {historic}; wszystkie zgodne, bez nadpisania plików. Standardowe Draft Windows CI na końcowym HEAD: link/status w PR i dostarczeniu. [Walidacja](physics-driven-racing-line/validation.json).")
add("\nReprodukcja do nowego katalogu: `physics-line NEW freeze`, potem `physics-line NEW measure` przy Release narzędzia. `racing-line-report.py` tylko czyta dowody i sprawdza historyczne hashe; nie wykonuje fizyki ani ponownego wyboru. Eksporter odmawia nadpisania dowodów. Po pomiarze poprawiono wyłącznie wymagane metadane parsera replay (RiderId/Passes), bez ponownego liczenia ruchu; bieżący kod eksportu zawiera je od początku.")
add("\n## Odpowiedzi i jeden następny krok")
add("\n1. **Czy model preferuje bandę na prostej?** Nie w tym zamrożonym zbiorze. Inner wygrywa we wszystkich czterech warunkach; szeroki rzeczywiście zbliża obrys do bandy na prostej, ale nie na pełnej szerokości łuku. Nie wyprowadzamy z Motoareny statystyki realnych biegów.\n2. **Dlaczego?** W badanej jednolitej nawierzchni dodatkowa droga przeważa wzrost prędkości wyjścia; ponadto brak gładkiej granicy i ograniczenie targetu uniemożliwiają ocenę pełnego zewnętrznego podejścia. Nie wiadomo, co wybrałby ten sam model na poprawionej całej geometrii.\n3. **Szerokie wejście a następny odcinek?** Większa exit/straight speed jest zmierzona, lecz nie kompensuje kosztu całego cyklu. Mieszany plan poprawia szeroką kontrolę, ale przegrywa z inner.\n4. **Ważność exit speed?** Przenosi się na następną prostą, jednak nie jest samodzielną funkcją celu; zamknięte dwa kolejne obiegi ujawniają koszt wcześniej przejechanej drogi.\n5. **Regulator i wyłamanie?** Działa geometryczny preview i fizyczny powrót yaw, ale rzeczywisty kąt jest opóźniony i utrzymuje się na początku prostej. Wcześniejsze otwarcie nie poprawiło czasu; nie dowiedziono pełnego realizmu momentów ustawiania motocykla.\n6. **Przymknięcie gazu?** Daje pośrednią kontrolę wykonania, głównie utratę tempa; niewielki wyjątek B0,7 nie uzasadnia nowego strojenia. Nie ma niezależnego wheelspinu.\n7. **Blokada geometrii?** Tak dla pełnego wniosku o typowej zewnętrznej bandzie: skok4,6 m i niedostępny szerszy łuk. Konserwatywny podzbiór daje wiarygodne czasy, ale nie globalne optimum.\n8. **Autonomia runtime?** Wybrany plan da się już tanio wykonywać jako normalne polecenia w jednym świecie, ale wieloświatowy selektor pozostaje offline. Stary scoring #95 nie został połączony z ON. Brakuje dowodu dla ciągłej, pełnej geometrii oraz adaptacyjnego wyboru w ruchu.")
add("\n**Najważniejszy następny PR: ciągła, geometrycznie zweryfikowana zewnętrzna granica i jej lokalny bezpieczny zakres sterowania.** Powód pomiarowy to cztery udokumentowane skoki4,6 m oraz 4,6 m niedostępnego targetu łuku. Najpierw umożliwić uczciwe porównanie tej samej zamrożonej rodziny planów na całej powierzchni. Nie łączyć tego z przebudową opony, napędu, regulatora i AI. Dopiero potem można rozstrzygać brak preferencji bandy między sterowaniem a fizyką. Obecnie A=problem geometrii potwierdzony, B=opóźnienie sterowania zmierzone bez kalibracji, C=granice napędu/opony odziedziczone, D=selektor poprawny w ograniczonym zbiorze, E=brak podstaw do pełnego wniosku o realizmie.")
(ROOT / "docs/experiments/physics-driven-racing-line.md").write_text("\n".join(lines)+"\n", encoding="utf-8")
(FOLDER / "manifest.json").write_text(json.dumps({p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(FOLDER.iterdir()) if p.is_file() and p.name != "manifest.json"}, indent=2)+"\n", encoding="utf-8")
print(json.dumps(validation, indent=2))
