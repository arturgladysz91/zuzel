"""Render #92 tables from bounded accepted-state evidence; never rerun/tune the simulation."""
import hashlib
import json
from pathlib import Path
import statistics
import sys

root = Path(__file__).resolve().parents[2]
directory = Path(sys.argv[1]) if len(sys.argv) > 1 else root / "docs/experiments/full-heat-and-solo-validation"
output = directory.with_suffix(".md")
def read(name):
    return json.loads((directory / (name + ".json")).read_text(encoding="utf-8-sig"))
def f(value, digits=6):
    return "—" if value is None else f"{value:.{digits}f}"
def table(headers, rows):
    return "\n".join(["| " + " | ".join(headers) + " |", "|" + "---|" * len(headers)] +
                     ["| " + " | ".join(map(str, row)) + " |" for row in rows])
def link(r):
    return f"[{r['Name']}](full-heat-and-solo-validation/{r['Name']}.json)"
def csv(values, digits=3):
    return " / ".join(f(v, digits) for v in values)

config = read("configuration")
heats = [read(f"A{a}-rotation-{rotation}") for a in [1, 2] for rotation in range(4)]
controls = [read(f"A{a}-control-OFF") for a in [1, 2]]
solo = [read(f"{family}-{attribute}-{level}") for family, attributes in [("canonical", config["Canonical"]), ("legacy", config["Legacy"])]
        for attribute in attributes for level in [20, 50, 80]]
assert len(solo) == 33 and all(r["Status"] == "Completed" for r in heats + controls + solo)
assert all(len(v["LapSeconds"]) == 4 for r in heats + controls + solo for v in r["Riders"])
for r in heats + controls + solo:
    assert not r["Parameters"]["EnableTacticalQualityScoring"]
    assert r["Parameters"]["SolverSubsteps"] == 4 and r["Result"]["StepSeconds"] == .01
    assert all(abs(sum(v["LapSeconds"]) - float(r["Result"]["FinishTimes"][str(v["RiderId"])])) < 1e-8 for v in r["Riders"])
    if r in solo:
        assert len(r["Inputs"]) == 1 and not r["Inputs"][0]["TrafficEnabled"]
        assert r["Result"]["Work"]["Episodes"] == 0 and not r["Result"]["Decisions"]
baseline_hash = read("canonical-Reaction-50")["Result"]["Fingerprint"]
for attribute in ["Start", "TrackReading", "Attack", "Defense", "PairRiding", "Strength"]:
    assert all(read(f"canonical-{attribute}-{level}")["Result"]["Fingerprint"] == baseline_hash for level in [20, 50, 80])
repeats = read("repeats")
assert all(all(r[k] for k in ["ExactHash", "ExactLaps", "ExactPasses", "ExactDecisions", "ExactContacts"]) for r in repeats)
performance = read("performance")
assert len({r["Fingerprint"] for r in performance} | {heats[0]["Result"]["Fingerprint"]}) == 1

parts = ["""# Full Heat Validation: Four Riders and Solo Time Trials (#92)

**PROVISIONAL — B: CZĘŚCIOWO.** Wszystkie 8 głównych biegów kończy się czterema ukończonymi okrążeniami; 0 przerwanych, 0 DNF, 0 kontaktów i 0 wyjazdów poza tor. Tak samo kończą się 2 kontrole OFF i 33 próby solo. Są potwierdzone minięcia po pierwszym łuku, lecz dominacja pola A, ostrożne odpuszczanie gazu, brak obron w tym zestawie i niepodłączony kanoniczny Start nie pozwalają uznać modelu za gotowy. Nie zmieniono mechaniki, parametrów ani scoringu, aby poprawić wynik.

Baza: Draft #91, `experiment/tactical-decision-quality`, HEAD `a56a2b777ca28274c2ea227f51cbfb1ee087a60b`. Zakres: jeden moduł pomiarowy, sześć testów jego miar, wejście CLI i jawna odpowiedzialność testów w istniejącym CI. Produkcyjne `src/`, `HeatSimulator`, Box2D, regulator, nawierzchnia, geometria, progi Crash oraz algorytmy #89–#91 pozostają identyczne z bazą.

## Metoda i odtwarzanie

[Pełna konfiguracja i zamrożone profile](full-heat-and-solo-validation/configuration.json). Motoarena Toruń: referencja okrążenia 318.778749943 m, promień 31 m, proste 35 + 62 + 27 m, szerokość prostej 12 m i łuku 16.6 m. Rzeczywiste środki pól A/B/C/D mają offsety 0.5/3.5/6.5/9.5 m od wewnętrznej referencji. Start: referencja 0 m, prędkość 0. Domowy target każdego zawodnika jest środkiem jego pola; to ten sam styl, a różne początkowe linie wynikają z pól.

Jeden świat Box2D.NET 3.1.654, krok 10 ms, 4 substeps na wywołanie solvera; istniejące dodatkowe podziały przy zbliżeniu, krzywiźnie i deadline reakcji pozostają aktywne. AI co 100 ms. W głównych biegach Tactical=true, SideBySide=true, Quality=false. W kontrolach Tactical=false, SideBySide=false, Quality=false, z dotychczasową reaktywną ochroną kontaktową. W solo te trzy flagi są false i TrafficEnabled=false, bez rywali i komend. Zmiana polityki AI nie zmienia wersji fizyki.

Każda komórka nawierzchni: Grip=1, Ruts=0, Moisture=0.5, EffectiveGrip=0.9175; powierzchnia zamrożona przez WorldRunner. Ten sam motocykl referencyjny, masa zawodnika 70 kg, BikeSetup.Neutral (0.5/0.5), legacy RiderStyle.Balanced i PreferredLine.Neutral. Brak losowej formy, zużycia, nowych band, systemu sędziowskiego i powtórek. Limit 90 s świata; `Run(... requiredLaps:4, stopOnCrash:true)`. Osiem rotacji oraz profile ustalono przed smoke; bez dobierania kolejności mety.

Najpierw zakończono jeden solo i jeden pełny heat w Debug. Dopisanie diagnostyki nie zmieniło modelu. Finalne, zachowane dane pochodzą z Release, .NET 8.0.26, Windows 10, SDK 10.0.101. Etapy: smoke → heats → solo → repeat → benchmark. Niezależne świeże procesy powtórzyły pięć wybranych prób z odwróconą kolejnością wejściową. Benchmark ma osobny proces z DOTNET_TieredCompilation=0, aby wczesne przełączanie kodu JIT nie udawało kosztu diagnostyki; hash fizyczny pozostał identyczny. Pierwsza próba benchmarku z domyślnym tieringiem wykazała taki dryf i nie służy do wnioskowania o narzucie.

```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
# Każdy etap to oddzielny proces; smoke jest bramką przed kolejnymi etapami.
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll full-validation docs/experiments/full-heat-and-solo-validation smoke
# Ten sam prefiks polecenia: heats, solo, repeat; następnie benchmark z DOTNET_TieredCompilation=0.
python tools/shared-world-speedway/full-validation-report.py
```

Obserwator interpoluje czas przecięcia kolejnych wielokrotności `Frame.LapMeters` na `TotalReferenceMeters` między dwoma zaakceptowanymi stanami. Nie modyfikuje motocykla i nie resetuje go do okrążenia lotnego. Pierwsze okrążenie zawiera reakcję i przyspieszanie; lotne to osobno okrążenia 2/3/4. Czwarta suma zgadza się z produktem WorldRunner do 1e-8 s. Sześć cyfr po przecinku ujawnia małe różnice numeryczne, a nie deklarowaną mikrosekundową dokładność fizyki 10 ms.

Minięcie wymaga ustalonego wcześniejszego lidera całym motocyklem, rzeczywistej fazy obok siebie, przejścia drugim całym motocyklem z zapasem ≥0.15 m i utrzymania tego przez ≥1 s oraz ≥10 m. Projekcja obejmuje istniejące kapsuły podwozia i kierownicy (`TacticalAudit.Extent/ClearGap`). Pary porównujemy lokalnie (oba dystanse <20 m, zgodne styczne >0.9), z kontrolą rozwiniętej referencji, aby nie zaliczać motocykli po przeciwnej stronie toru lub zdublowanych. Pierwsze rozdzielenie pól tylko ustala lidera. Krótkie zmiany referencji i kolejność ID przy remisie nie są minięciami. Miara jest konserwatywna: nie liczy niespełniających czasu utrzymania minięć tuż przed metą.

W JSON są czasy, istotne komendy i stany po ich aktywacji, rzeczywiste zmiany porządku referencji (jawnie odrębne od minięć), kontakty/konsekwencje i anomalie. Czasy pomiarów kończą się przy własnej mecie, choć ciało jedzie dalej do ukończenia ostatniego zawodnika; klasyfikacja obserwatora zamraża ukończonych według czasu przecięcia. Dwie trajektorie co 0.1 s: [A1/0](full-heat-and-solo-validation/A1-rotation-0-trajectory.csv), [A2/0](full-heat-and-solo-validation/A2-rotation-0-trajectory.csv); żadnych pełnych snapshotów co 10 ms w plikach zbiorczych.

Gdy wystąpi Crash, status całej próby to Interrupted-Crash, z czasem, wszystkimi pozycjami/prędkościami i uczestnikami w CrashSnapshot oraz ContactEvent/ConsequenceObservation; bez hurtowego DNF i bez automatycznej powtórki. Ten tryb sprawdza krótka istniejąca próba Scenarios.E w regresji, nie jest dodatkowym pełnym biegiem. OutsideTrack jest osobną anomalią geometryczną, niezależną od wystąpienia kontaktu; w badanych biegach nie wystąpiła. Min. prześwit dotyczy całego footprintu i istniejącej lokalnej geometrii granic, bez założenia fizycznych band.

## A. Pełne biegi

Kolejność kanonicznych liczb: Reaction/Start/Technique/TrackReading/Attack/Defense/PairRiding/Strength. Legacy: Start/Speed/SlideControl; pozostałe trzy legacy zawsze 50. A1: wszystkie osiem i wszystkie legacy =50, combativeness=0.5. A2 poniżej; masa, setup i pozostałe preferencje wspólne. R2/R3 mają także różne legacy Speed, więc ich wynik nie jest izolowanym dowodem działania Technique/Attack.
"""]
parts.append(table(["A2", "Kanoniczne", "Legacy S/Sp/SC", "Combativeness"], [["R"+str(p["Id"]), "/".join(map(str,p["Gameplay"]["Abilities"].values())), "/".join(str(p["Legacy"][k]) for k in ["Start","Speed","SlideControl"]), p["Gameplay"]["InteractionStyle"]["Combativeness"]] for p in config["A2"]]))
parts.append("\n### Wyniki i geometria pól\n")
parts.append(table(["Bieg", "Pole A", "Pole B", "Pole C", "Pole D", "Wynik", "Czas zwycięzcy s", "Wyprzedzenia", "Kontakty", "Crash"],
    [[link(r)] + ["R"+str(next(i["RiderId"] for i in r["Inputs"] if i["Gate"]==g)) for g in "ABCD"] +
     [" > ".join("R"+str(v["RiderId"]) for v in r["Result"]["Classification"]), f(r["Result"]["Classification"][0]["FinishTimeSeconds"],3),len(r["Passes"]),r["Result"]["Work"]["Episodes"],0] for r in heats+controls]))
parts.append("""
Główne: 8/8 ukończonych, 0 przerwanych. Kontrole osobno: 2/2, 0 przerwanych. W A1 cały wynik podąża za polem: A/B/C/D = 60.532/61.779/63.131/64.566 s w każdej rotacji. Kontrola bez taktyki: 60.532/61.432/62.356/63.295 s. Samo AI kosztuje odpowiednio około 0.000/0.347/0.774/1.271 s dla tych pól. A2: zwycięża R2 dwukrotnie, R3 i R4 po razie, każdorazowo zawodnik startujący z A lub B. Mocniejszy start R1 nie gwarantuje zwycięstwa. To obserwacje czterech prób na zestaw, bez estymacji prawdopodobieństw zwycięstwa.

### Każdy zawodnik: ukończenie, okrążenia i walka

Okrążenia oraz meta w sekundach; średnia i maksimum w m/s. A/D/S oznacza aktywowane intencje ataku, obrony i side-by-side (w tym Hold). Logiczne Hold może nie zmienić gazu ani targetu. Min. prześwit granic i odstęp całych motocykli w metrach; odstęp ujemny oznaczałby nakładanie, nie samą różnicę referencji. Δmeta to odstęp czasowy do zwycięzcy, nie fizyczny dystans ciał po ich mecie.
""")
parts.append(table(["Bieg", "Zawodnik/pole", "Meta / Δmeta s", "L1 / L2 / L3 / L4 s", "Śr./max m/s", "A/D/S", "Minięcia", "Kontakty", "Min. granica / rywal m"],
    [[r["Name"],f"R{v['RiderId']}/{next(i['Gate'] for i in r['Inputs'] if i['RiderId']==v['RiderId'])}",
      csv([r["Result"]["FinishTimes"][str(v["RiderId"])],r["Result"]["FinishTimes"][str(v["RiderId"]) ]-r["Result"]["Classification"][0]["FinishTimeSeconds"]]),
      csv(v["LapSeconds"]),csv([v["AverageSpeedMetersPerSecond"],v["MaximumSpeedMetersPerSecond"]]),
      f"{v['Attacks']}/{v['Defenses']}/{v['SideManeuvers']}",v["Passes"],v["Contacts"],csv([v["MinimumTrackClearanceMeters"],v["MinimumPeerGapMeters"]])]
     for r in heats+controls for v in r["Riders"]]))
parts.append("\n### Rzeczywiste minięcia po pierwszym łuku\n")
parts.append(table(["Bieg", "Para", "Cały motocykl od s", "Potwierdzenie s", "Przebyty dystans utrzymania m"],
    [[r["Name"],f"R{p['RiderId']} przed R{p['OpponentId']}",f(p["ClearSinceSeconds"],3),f(p["ConfirmedAtSeconds"],3),f(p["HeldDistanceMeters"],3)] for r in heats+controls for p in r["Passes"]]))
parts.append("""
W głównych próbach są 2 potwierdzone minięcia, oba R2 nad R1, w okrążeniach 4 (A2/0) i 2 (A2/2). Kontrola A2/OFF ma osobne minięcie R2/R1 już w 25.390 s, względem 47.520 s przy AI w tym ustawieniu; naturalna różnica tempa potrafi wyprzedzić bez taktyki. Brak podstaw, by przypisać te dwa sukcesy wyłącznie algorytmowi ataku. Chwilowe porządki referencji w JSON zawierają także pierwsze rozdzielenie z pól i nie są doliczane do tej tabeli.

### Aktywność przez pełne cztery okrążenia i anomalie

Aktywacje L1–L4 przypisano według własnych przecięć mety. Obserwacje są wywoływane do ukończenia biegu; brak późnej komendy przy braku bliskich rywali nie oznacza zatrzymania AI. Typowa prędkość to mediana ważona czasem, histogram 0.25 m/s; zakres poniżej obejmuje czterech zawodników. Gaz=0 jest rzeczywistym wejściem sterowania, a scrub w łuku może zmniejszać siłę mimo gazu=1.
""")
def activations(r):
    counts=[0]*4
    for v in r["Riders"]:
        for d in r["Result"]["Decisions"]:
            if d["RiderId"]==v["RiderId"] and d["ActualActivation"] is not None:
                lap=sum(t<=d["ActualActivation"] for t in v["CrossingSeconds"])
                if lap<4: counts[lap]+=1
    return "/".join(map(str,counts))
parts.append(table(["Bieg", "Obserwacje AI/taktyki", "Decyzje/aktywacje/zmiany sterowania", "Aktywacje L1/L2/L3/L4", "Aktywacje po własnej mecie", "Typowa m/s min–max", "Gaz=0 R1/R2/R3/R4 s", "Rewersy targetu R1–R4", "Największy spadek w 1 s, m/s"],
    [[r["Name"],f"{r['Result']['Work']['AiUpdates']}/{r['Result']['Work']['TacticalObservations']}",
      f"{len(r['Result']['Decisions'])}/{sum(v['Activated'] for v in r['Riders'])}/{sum(v['ActualControlChanges'] for v in r['Riders'])}",activations(r),
      sum(d["ActualActivation"] is not None and d["ActualActivation"] > r["Result"]["FinishTimes"][str(d["RiderId"])] for d in r["Result"]["Decisions"]),
      f"{f(min(v['TypicalSpeedMetersPerSecond'] for v in r['Riders']),3)}–{f(max(v['TypicalSpeedMetersPerSecond'] for v in r['Riders']),3)}",
      csv([v["DriveOffSeconds"] for v in r["Riders"]],2),"/".join(str(v["TargetReversals"]) for v in r["Riders"]),f(max(v["MaximumOneSecondSpeedLossMetersPerSecond"] for v in r["Riders"]),3)] for r in heats+controls]))
parts.append("""
1. **Utrata napędu przy odrzuceniu ataku, bez kontaktu.** A1/0: R2 NoPassingCorridor 10.640→13.840 s (3.2 s), R3 10.740→15.640 s (4.9 s), R4 10.840→16.940 s (6.1 s). Target praktycznie bez zmiany. Kod `TrafficDecision.Tactics` odrzuca kandydatów zarówno przez Corridor, jak i gain<=0, a następnie zwraca BackOut/Drive=0 z jedną etykietą NoPassingCorridor; powrót wymaga oddalenia przodu >8 m lub jego braku. To nie dowodzi, że każdy z tych epizodów był fizycznie niepotrzebny, ale odrzucenie korzyści ataku i wymóg bezpiecznej redukcji gazu nie są tutaj oddzielnie uzasadnione. Kontrola pokazuje spowolnienie mimo 0 kontaktów. Minimalny następny PR: rozdzielić powody odrzucenia i wykazać konieczność lift względem aktualnego korytarza; bez zmiany gripu lub mocy.
2. **Powtarzane lift/resume w późnym biegu.** A2/1 R2: 14.69 s gazu=0 łącznie, m.in. ImmediateThreat/ResumeContest 37.240→38.540, 42.640→44.240, 44.940→46.040, 50.440→52.040, 52.840→53.940 i 58.140→59.740 s. Widoczne na L3/L4 i dokładnie powtórzone w świeżym procesie. Maksymalna utrata prędkości w 1 s R2 =2.044 m/s, bez kontaktu. To konkretny kandydat do diagnozy nadmiernie ostrożnej taktyki, nie upadek ani błąd solvera; bez wyrzucania tej próby z raportu.
3. **Brak wzbudzonej obrony.** W głównych próbach 0 aktywowanych DefendLine. Sam fakt istnienia konsumenta Defense nie dowodzi jego jakości w pełnym biegu. R4 nie jest dowodem skutecznej obrony, nawet gdy wygrywa z A.
4. **Linie i ograniczenia.** Maksymalnie 2 rewersy znaczących zmian targetu (>0.05 m) u pojedynczego zawodnika, 0 w A1; brak szybkiego przełączania co obserwację. Nie jest to dowód braku wszystkich drobnych oscylacji: trajektorie zachowano tylko dla dwóch przypadków. Maksymalne spadki prędkości obejmują naturalne wejścia w łuk. Wszystkie końce i przesunięcia są zaakceptowanymi stanami solvera, bez teleportacji; maksimum zastosowanej siły/budżetu ≈1 (odchylenie float). Min. prześwit granic jest dodatni w każdej próbie. Zero kontaktów nie waliduje zachowania zatłoczonego wyścigu; może współwystępować z nadmierną ostrożnością.
5. **Oczekująca komenda po własnej mecie.** W A2/1 R1 obserwuje ContestResolved w 61.300 s, kończy w 61.338553 s, a gotowa komenda aktywuje się dopiero w 61.520 s. Dlatego 49 aktywacji całego świata obejmuje 48 na L1–L4 i jedną po mecie. Drive=1 oraz target=3.5 są niezmienione: to logiczne wyczyszczenie intencji bez zmiany aktuatorów, nie dodatkowy manewr wyścigowy ani zmiana ukończonej klasyfikacji. WorldRunner.Activate nie odrzuca oczekującej komendy zawodnika Finished. Kolejny PR powinien jawnie zamknąć cykl intencji po ukończeniu; nie poprawiano tego podczas audytu.

## B. Próby indywidualne

### Kanoniczne 20/50/80

W każdej komórce poziomu: **pierwsze okrążenie / najlepsze lotne, s**. Wszystkie legacy=50, masa/setup/styl i start z pola A identyczne. Status dotyczy odpowiedzialności w tym silniku i badanego bodźca, nie produkcyjnego starego HeatSimulator. N/A oznacza istniejącego konsumenta niewzbudzonego w solo; INACTIVE oznacza brak konsumenta w SharedWorldSpeedway.
""")
canonical_consumers = {
 "Reaction": ("ACTIVE", "wcześniej o 0.048980 s 20→80", "brak mechanizmu; różnice numeryczne µs", "RiderController.TapeReactionSeconds"),
 "Start": ("INACTIVE", "0", "0", "brak; launch nadal czyta legacy Start"),
 "Technique": ("PARTIAL", "identyczny moment napędu i siła startowa", "mikrosekundy; niewielka zmiana yaw", "RiderController skillAuthority (torque); TrafficDecision margin; PhysicalContactAnalyzer.Reserve"),
 "TrackReading": ("INACTIVE", "0", "0", "brak konsumenta; brak percepcji zmiennej nawierzchni"),
 "Attack": ("NOT APPLICABLE", "0", "0", "TrafficDecision.Tactics / SideBySide: horyzont, margines i wybór; brak rywala"),
 "Defense": ("NOT APPLICABLE", "0", "0", "TrafficDecision.Tactics / SideBySide; brak rywala, 0 DefendLine w głównych biegach"),
 "PairRiding": ("INACTIVE", "0", "0", "brak modelu partnera/konsumenta w tym świecie"),
 "Strength": ("NOT APPLICABLE", "0", "0", "WorldRunner.BeginContact → PhysicalContactAnalyzer.Reserve; bez kontaktu")}
legacy_consumers = {
 "Start": ("ACTIVE", "napęd launch 1307.270→1474.155 N; reakcja taśmy stała", "brak trwałego efektu; µs", "LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons; ReactionSeconds tylko dla AI"),
 "Speed": ("ACTIVE", "ta sama siła launch, inne ograniczenie wejścia", "napęd Straight/TurnExit i limit łuku", "LongitudinalDynamics Straight/TurnExit; SegmentPhysics.MaxSafeTurnSpeedForRadius"),
 "SlideControl": ("ACTIVE", "ta sama siła launch, wcześniejsze przygotowanie do łuku", "limit łuku i correction", "SegmentPhysics.MaxSafeTurnSpeedForRadius; LongitudinalDynamics.CalculateCornerCorrectionDeceleration...")}
def sensitivity(family, attributes, consumers):
    rows=[]
    for a in attributes:
        status, start, flying, consumer=consumers[a]
        values=[]
        for level in [20,50,80]:
            v=read(f"{family}-{a}-{level}")["Riders"][0]
            values.append(f(v["LapSeconds"][0])+" / "+f(v["BestFlyingSeconds"]))
        rows.append([a+" — "+status]+values+[start,flying,consumer])
    return table(["Cecha", "Poziom 20", "Poziom 50", "Poziom 80", "Wpływ na start", "Wpływ na lotne okrążenie", "Rzeczywisty konsument kodu"],rows)
parts.append(sensitivity("canonical",config["Canonical"],canonical_consumers))
parts.append("""
Reaction: pierwszy dodatni napęd dokładnie 0.264490/0.240000/0.215510 s. Maksymalna siła styczna launch jest identyczna 1390.712524 N, a różnica pierwszego okrążenia 20→80 =−0.048938 s. Mechanizm zmienia czas reakcji, nie moc po starcie. Różnica 42 µs względem idealnego przesunięcia reakcji i niemal identyczne lotne wynikają z innej fazy siatki czasowej i położeń float; nie nadajemy im znaczenia sportowego.

Technique: maksymalny błąd orientacji względem zadanego headingu spada 0.478598/0.476290/0.475890 rad; pierwsza siła i czas launch identyczne. Kod mnoży torque przez 0.75+0.25*(Technique−1)/98, bez nowej mocy ani gripu. Czasy pierwszego okrążenia zmieniają się o zaledwie 44 µs między 20 i 80. Najlepsze lotne 50 jest o 4.48 µs gorsze niż 20 mimo poprawy maksymalnego yaw. Poszczególne lotne też nie są ściśle monotoniczne. Reprodukcja 80 jest dokładna, lecz to słaby sygnał w 10 ms/float, nie dowód istotnej przewagi czasowej i nie powód kalibracji. W Strength oraz Technique rezerwa kontaktowa istnieje, lecz nie została pobudzona w tych bezkontaktowych próbach.

Start, TrackReading i PairRiding nie mają konsumenta w nowym świecie. Zera Attack/Defense/Strength są zgodne z brakiem rywali, partnera lub kontaktu; nie dopisano bonusu do Vmax. Dla sześciu cech bez efektu solo wszystkie trzy poziomy mają dokładnie ten sam hash zaakceptowanych stanów co neutralny (łącznie z Attack/Defense/Strength).

### Oddzielnie legacy 20/50/80
""")
parts.append(sensitivity("legacy",config["Legacy"],legacy_consumers))
parts.append("\nMechanizmy poniżej oddzielają początek jazdy, cały pierwszy odcinek 35 m (z reakcją), siłę startową, prostą i łuk. Średnie prędkości w tych dwóch kontekstach obejmują całe cztery okrążenia, z launch w średniej prostej; nie są ustalonym Vmax.\n")
parts.append(table(["Rodzina/cecha/poziom", "Pierwszy napęd s", "35 m s", "Max launch N", "Śr. prosta / łuk m/s", "Max yaw rad"],
    [[r["Name"],f(v["FirstDriveForceSeconds"]),f(v["First35MetersSeconds"]),f(v["MaximumLaunchTangentialForceNewtons"],3),csv([v["StraightAverageSpeedMetersPerSecond"],v["CornerAverageSpeedMetersPerSecond"]],6),f(v["MaximumYawErrorRadians"])]
     for r in solo if r["Name"].startswith("legacy-") for v in r["Riders"]]))
parts.append("""
Legacy Start przy stałej kanonicznej Reaction nie zmienia reakcji taśmy (0.240000 s); zwiększa rzeczywistą siłę launch. Jego stara funkcja czasu reakcji nadal jest proxy czasu reakcji taktycznej, ale TrafficEnabled=false w solo, więc nie tłumaczy tego wyniku. Legacy Speed działa na siłę zwykłej prostej/wyjścia z łuku i także bezpośrednio na limit prędkości łuku (stary mnożnik 0.94+0.12*Speed/100). To nie jest docelowy dowód jakości kanonicznej techniki ani ataku. SlideControl działa na limit 0.97+0.06*SlideControl/100 i correction; może poprawiać proste przez wyjście z poprzedniego łuku. Małe różnice pierwszych 35 m Speed/SlideControl wynikają z przygotowania do nadchodzącego łuku, mimo identycznego launch. Wyższe SlideControl podnosi max yaw, bo jedzie się szybciej; izolowana miara błędu orientacji nie jest kompletną miarą stabilności przy innym tempie.

Wszystkie trzy legacy poprawiają pierwszy czas w 20→50→80; Speed i SlideControl także lotny. Odwrócenia mikrosekundowe legacy Start na pojedynczych lotnych nie są trwałym efektem mocy startowej po kilkunastu sekundach. Nie wykryto istotnej sprzeczności typu Strength dodaje Vmax; wykryto ograniczony wpływ kanonicznego Technique i niepodłączony Start. **Brak kategorii UNEXPECTED o istotnej skali w solo**; to nie unieważnia problemu fallbacku gazu w pełnym biegu.

### Wszystkie 33 przejazdy solo

Tabela zawiera również identyczne neutralne 50 dla każdej osi; żadna próba nie została pominięta. Link każdej próby prowadzi do wejść, hashy, pełnych miar i finishu.
""")
parts.append(table(["Próba", "Standing L1 s", "Flying L2 s", "Flying L3 s", "Flying L4 s", "Najlepsze lotne s", "Cztery okrążenia s"],
    [[link(r)]+[f(t) for t in r["Riders"][0]["LapSeconds"]]+[f(r["Riders"][0]["BestFlyingSeconds"]),f(r["Result"]["FinishTimes"]["1"])] for r in solo]))
parts.append("\n### Wydajność i koszt pomiaru\n")
perf_rows=[]
for observed in [False,True]:
    subset=[r for r in performance if r["Observed"]==observed]
    med=lambda key: statistics.median(r[key] for r in subset)
    work=lambda key: statistics.median(r["Work"][key] for r in subset)
    perf_rows.append(["A1/0 + pomiary" if observed else "A1/0 bez obserwatora", f(med("ElapsedMilliseconds"),3), f(med("CpuMilliseconds"),3),f(med("AllocatedBytes")/1048576,3),f(med("CallbackMilliseconds"),3),f(work("TacticalElapsedMilliseconds"),3),int(work("PhysicsUpdates")),int(work("SolverCalls")),int(work("SolverSubsteps")),int(work("AiUpdates")),int(work("TacticalObservations"))])
parts.append(table(["Release, 3 próbki, mediana", "Wall ms", "CPU ms", "Alokacje MiB", "Callbacki ms", "Taktyka ms", "Kroki 10 ms", "Wywołania solvera", "Substeps", "AI", "Obserwacje taktyki"],perf_rows))
parts.append("""
[Surowe trzy próbki na tryb](full-heat-and-solo-validation/performance.json). Każdy bieg to 64.570 s świata; 4 substeps na każde wywołanie solvera, a nie tylko cztery na krok nadrzędny. Solver ma dodatkowe legalne wywołania przy krzywiźnie/deadline. Różnica median wall to około 59.4 ms, alokacji około 36.0 MiB, suma callbacków około 55.2 ms. Opakowanie observed zawiera też zbudowanie podsumowania i mapowanie wejść; callback nie mierzy kosztu utworzenia Snapshot przed jego wywołaniem. Nie odejmujemy czasu callbacku od wall, aby udawać dokładny czas samej fizyki. CPU to TotalProcessorTime procesu (także GC), z kwantyzacją platformy i możliwością przewyższenia wall; trzy próbki to diagnoza, nie precyzyjny benchmark sprzętowy. Wszystkie sześć hashy jest identycznych z A1/0 i nie ma optymalizacji silnika.

W zapisanych próbach `Performance.SummaryMilliseconds` oddziela agregację po symulacji, a `AcceptedObserverMilliseconds` / `ForceObserverMilliseconds` same callbacki; serializacja JSON i generowanie tego dokumentu są poza pomiarem symulacji. Trajektorie plikowe są wyłączone w benchmarku. Nie dodano eksportu pełnego stanu co krok. Dane wejściowe, podsumowania i zdarzenia zajmują kilka MB, a nie wielkie pliki snapshotów.

## C. Stan gotowości silnika
""")
parts.append(table(["Obszar", "Stan", "Dowody", "Co wymaga kolejnego PR"], [
 ["Start","PARTIAL","Reaction steruje czasem; legacy Start siłą; canonical Start INACTIVE","Jawne podłączenie techniki startu, osobno od reakcji i mocy"],
 ["Jazda solo","PARTIAL","33/33 cztery okrążenia, jawne czasy i konsumenci","Istotność kontroli Technique; później osobna nawierzchnia"],
 ["Pełne cztery okrążenia","działa w zestawie","8/8 głównych + 2/2 kontroli, brak timeoutów","Szersza walidacja po usunięciu problemów taktyki"],
 ["Wyprzedzanie","PARTIAL","2 rzeczywiste po pierwszym łuku; 1 w kontroli OFF","Odróżnić wartość AI od naturalnej przewagi tempa i pola"],
 ["Obrona","niewykazana","0 aktywowanych DefendLine w głównych","Próba uruchamiająca obronę bez strojenia wyniku"],
 ["Walka koło w koło","PARTIAL","Contest/Hold i skończone zmiany targetu","Rozdzielenie braku korzyści ataku od konieczności odjęcia gazu"],
 ["Kontakty i upadki","niewzbudzone w biegach","0/0; przerwanie sprawdza krótka regresja istniejącego Crash","Wiarygodność kontaktów pełnego heat bez sztucznego wymuszania"],
 ["Zróżnicowanie zawodników","PARTIAL","silne efekty legacy, 3 canonical bez konsumenta, N/A rozdzielone","Docelowi konsumenci, bez Overall i bonusu do prędkości"],
 ["Wydajność","zmierzona, bez optymalizacji","około 137 ms bez diagnostyki / 197 ms z nią za heat","Więcej stabilnych pomiarów dopiero przy decyzji o integracji"]]))
parts.append("""
**Ocena B — CZĘŚCIOWO.** Silnik potrafi kończyć pełne biegi w tym zestawie; nie stwierdzono blokera ukończenia ani konieczności zmiany czasu/klasyfikacji. Nie uzasadnia to jeszcze oceny A: powtarzany lift/resume, fallback po nieudanym ataku, zerowa obrona w zestawie i luki kanonicznych konsumentów wymagają osobnych prac. Nie ma podstaw do szerokich wniosków o realizmie na podstawie ośmiu biegów. Zmienna nawierzchnia nie naprawi tych luk i nie została tu implementowana.

### Weryfikacja i regresje

- 6/6 nowych testów miar: interpolacja i cofnięcie referencji, realne utrzymane minięcie, krótkie zmiany/zdublowanie, niezależność osi wejściowych, zgodność mety i dokładny hash z/bez obserwatora oraz odwrotna kolejność, Crash jako przerwanie bez fikcyjnego DNF.
- Release: 64/64 (`FullHeatSoloAuditTests|SharedWorld`), następnie 39/39 (`SideBySideRacingTests|TacticalQualityTests`), razem **103/103**, bez powtarzania historycznych audytów #54–#91.
- [Pięć świeżych powtórzeń](full-heat-and-solo-validation/repeats.json): A1/0, A2/0, A2/1, Technique80, legacy SlideControl80. ExactHash/Laps/Passes/Decisions/Contacts=true dla wszystkich; wejście odwrócone, identyczne profile. Stan sprzętu/nawierzchni i wersja fizyki te same.
- `dotnet restore`, lokalna kompilacja Release z warnings-as-errors: sukces. Guard 39 zamrożonych konsumentów: sukces. Discovery: 2616/2616 przypadków ma dokładnie jeden shard (core 1700, trajectory 395, historical-analysis 473, four-rider 48), bez braków/duplikatów. Python: 89/89 kalibracji + 7/7 testów CI. Standardowe Windows CI zostaje bez zmian; jego wynik na opublikowanym HEAD jest widoczny w Checks Draft PR, nie jest tu z góry deklarowany jako zielony. Zielone CI nie zmienia oceny B.

Wszystkie dane tego dokumentu pochodzą z lokalnych plików evidence; generator sprawdza kompletność 33 solo, cztery sumy okrążeń, statusy, flagi, neutralne hashe i powtórzenia przed zapisem. Nie pobiera danych z innej wersji fizyki. Manifest SHA-256 obejmuje konfigurację, każdy JSON i obie trajektorie.
""")
output.write_text("\n\n".join(parts).rstrip()+"\n",encoding="utf-8")
manifest={p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(directory.iterdir()) if p.is_file() and p.name != "manifest.json"}
(directory/"manifest.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
print(f"Validated 8+2 heats, 33 solo, 5 repeats; wrote {output}")
