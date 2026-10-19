"""Bounded #93 tables from recorded accepted states, never a simulation or tuning loop."""
import hashlib
import json
from pathlib import Path
import statistics
import sys

root=Path(__file__).resolve().parents[2]
directory=Path(sys.argv[1]) if len(sys.argv)>1 else root/"docs/experiments/proportional-throttle-control"
def read(name): return json.loads((directory/(name+".json")).read_text(encoding="utf-8-sig"))
def f(x,n=3): return "—" if x is None else f"{x:.{n}f}"
def table(headers,rows):
    return "\n".join(["| "+" | ".join(headers)+" |","|"+"---|"*len(headers)]+["| "+" | ".join(map(str,r))+" |" for r in rows])
def link(r):
    name=r["Name"]+("-ON" if r["Enabled"] else "-OFF")
    return f"[{name}](proportional-throttle-control/{name}.json)"
pair_names=["A-blocked-follow","B-steady-close","C-follow-reattack","D-immediate-threat","E-safe-parallel","F-low-capability","F-high-capability","G-straight-bend"]
pairs=[read(n+m) for n in pair_names for m in ["-OFF","-ON"]]
heats=[read(f"A{a}-rotation-{i}{mode}") for a in [1,2] for i in range(4) for mode in ["-OFF","-ON"]]
perf=read("performance");repeats=read("repeats");diagnosis=read("diagnosis-92")
for r in heats:
    assert r["Status"]=="Completed" and len(r["Result"]["FinishTimes"])==4
    assert r["Result"]["Work"]["Episodes"]==0
    if not r["Enabled"]:
        original=json.loads((directory.parent/"full-heat-and-solo-validation"/(r["Name"]+".json")).read_text(encoding="utf-8-sig"))
        assert r["Result"]["Fingerprint"]==original["Result"]["Fingerprint"]
        assert all(all(d[k]==original["Result"]["Decisions"][i][k] for k in original["Result"]["Decisions"][i]) for i,d in enumerate(r["Result"]["Decisions"]))
for r in pairs+heats:
    assert all(0<=d["Drive"]<=1 for d in r["Result"]["Decisions"])
    assert all(d["ActualActivation"]+1e-9>=d["RequestedActivation"] for d in r["Result"]["Decisions"] if d["ActualActivation"] is not None)
for p in perf:
    r=read(p["Name"]+("-ON" if p["Enabled"] else "-OFF"));assert p["Fingerprint"]==r["Result"]["Fingerprint"]
assert all(all(r[k] for k in ["ExactHash","ExactDecisions","ExactContacts"]) for r in repeats)

parts=["""# Proportional Throttle: Following, Safety and Reattack (#93)

**PROVISIONAL — A: DZIAŁA w zbadanym zestawie.** Mechanizm potrafi dopasować napęd do rywala częściowym gazem, następnie aktywować rzeczywisty manewr ofensywny. Zmniejsza zbędne zamknięcia na rozdzielonych liniach, bez nowych kontaktów w badanych oknach i biegach. Ocena opiera się na geometrii, zmianie prędkości, odstępie, wykonanym ataku i zachowanym kontakcie w D, a nie na szybszych czasach. To nie jest kalibracja ani dowód bezpieczeństwa we wszystkich sytuacjach. Flaga **EnableProportionalTrafficThrottle=false domyślnie** pozostaje wyłączona zgodnie z zadaniem.

Baza: Draft #92, `experiment/full-heat-and-solo-validation`, dokładny HEAD `8cd7dc67f66c798a1ba452f57dccad79fe6d5386`. Jeden nowy Draft, bez merge. `src/`, `RiderController.Forces`, Box2D/ContactAdapter, napęd, grip, geometria, masa motocykla, severity, umiejętności i scoring #91 bez zmian. Zmienia się tylko eksperymentalna decyzja oraz dwie małe kolejki sterowania.

## 1. Diagnoza #92: kiedy gaz=0 nie wynikał z zagrożenia

[Dokładne odtworzenie obu biegów i stany epizodów](proportional-throttle-control/diagnosis-92.json). Hashe A1/0 i A2/1 zgadzają się dokładnie z #92. NoPassingCorridor następowało zarówno po braku wolnego korytarza, wyjściu targetu poza możliwości ruchu, jak i po `gain<=0`. Wszystkie kończyły się BackOut/Drive=0. ClearResume wymagało potem braku rywala lub >8 m i czasu czystej obserwacji; każde wykonanie sterowania przedłużało NextTacticAt o 0.8 s.

Poniżej stan w chwili obserwacji, przed pełnym opóźnieniem reakcji. Prześwit wzdłużny i boczny obejmuje kapsuły podwozia/kierownicy i orientację obu motocykli. Ujemny prześwit wzdłużny przy dodatnim bocznym nie oznacza kontaktu. TTC w surowych danych jest warunkowym czasem zetknięcia projekcji wzdłużnych; bez wspólnego korytarza nie oznacza przewidywanego zderzenia.
"""]
parts.append(table(["Bieg", "Zawodnik", "Obserwacja s", "Powód #92", "Prześwit wzdłużny m", "Boczny m", "Zbliżanie m/s", "Korytarz wspólny/przecinany"],
 [[r["Name"],e["Decision"]["RiderId"],f(e["Decision"]["ObservedAt"]),e["Decision"]["Reason"],f(e["Gap"]["GapMeters"]),f(e["Gap"]["LateralGapMeters"]),f(e["Gap"]["ClosingMetersPerSecond"]),str(e["Gap"]["CorridorOccupied"])]
  for r in diagnosis for e in r["Episodes"] if e["Decision"]["Reason"] in ["NoPassingCorridor","ImmediateThreat"]]))
parts.append("""
Trzy A1 NoPassingCorridor miały ok. 2.2 m bocznego prześwitu, bez przecinania utrzymywanej ścieżki. Zerowanie nie było uzasadnione samym doganianiem na innej linii. Podobnie sześć późnych ImmediateThreat R2 w A2/1 miało 0.609–0.749 m bocznego prześwitu, a w kilku z nich wzdłużne zbliżanie było już niedodatnie. Stary prostoliniowy closest-approach w łuku nie zachowywał wspólnego obrotu toru i identyfikował taką sytuację jako zagrożenie. To ocena tych konkretnych epizodów, nie zasada, że każde historyczne odpuszczenie było błędem. Niebezpieczny aktualny lub oczekujący ruch w bok nadal wymaga anulowania; obecna separacja sama nie gwarantuje, że można kontynuować przecinanie.

Ścieżki: NoPassingCorridor ma teraz przy ON rozdzielone noty `AttackUnreachable` (target poza zakresem), `AttackCorridorUnavailable` (brak zaakceptowanego korytarza) i `NoAttackGain` (korytarz dopuszczalny, brak zysku). Są diagnostyką decyzji ataku, bez prawa do zamykania gazu. AttackNoLongerFeasible i ContestCorridorOccupied nadal zatrzymują/przerywają cel boczny; tylko osobna ocena zajętości i dynamiki decyduje o napędzie. ImmediateThreat pochodzi z nowej oceny mechanicznej. AbortThreatenedCrossing/CorridorOccupied mogą zatrzymać zmianę linii bez Drive=0. ResumeContest/ResumeIntent/ClearResume nie są przy ON osobnymi komendami bocznymi: napęd wznawia niezależny kanał ResumeAcceleration, bez wymagania oddalenia się rywala na 8 m. OFF wykonuje wszystkie oryginalne ścieżki.

## 2. Niewielki regulator i jego granice

[Konfiguracja](proportional-throttle-control/configuration.json). Co 100 ms jeden wspólny snapshot, ten sam świat i te same 4 substeps przy kroku 10 ms. Pary mają ten sam limit obserwacji 30 m. W pełnych biegach Tactical=true, SideBySide=true, Quality=false; ON/OFF poniżej oznacza **tylko nową flagę**, a nie wyłączenie taktyki. Wymagane Tactical=true; SideBySide nie jest nowym wymogiem. Ta sama Motoarena, wszystkie komórki Grip=1/Ruts=0/Moisture=0.5/EffectiveGrip=0.9175, bez zużycia i losowości. Osiem profili/gate rotations #92 bez zmian.

1. Pełne mechaniczne extents na wspólnej lokalnej osi stycznych dają gap między przodem doganiającego i tyłem poprzedzającego. Oddzielnie liczymy boczny odstęp kapsuł. Rozwinięta referencja służy do wykluczenia rywala z tyłu/zdublowanego, nie zastępuje prześwitu. Kierunki muszą być zgodne (dot≥0.85). Na granicy prosta–łuk nie dzielimy przez promień ani długość łuku.
2. Korytarz jest zajęty przy obecnej bocznej bliskości ≤0.15 m albo krótkim przecinaniu. Wykorzystujemy własny zadany target i istniejącą analityczną odpowiedź PD przez reakcję+0.6 s oraz lokalną prędkość boczną rywala. Nie czytamy jego targetu, planu, przyszłych komend ani stanu poza zaakceptowanym snapshotem. To ograniczona obserwacja, nie gwarancja przyszłej separacji i nie solver kolizji.
3. Gap po reakcji: `gR = g - max(0, closing)*reaction`. Żądany odstęp `g* = 0.6 m + 0.1 s * ownSpeed`. Czas odpowiedzi τ=1.2 s. `desiredAcceleration = observedOpponentAcceleration + (gR-g*)/τ² - 2*closing/τ`. Przyspieszenie rywala pochodzi z poprzedniej zaakceptowanej obserwacji 100 ms, ograniczonego do ±5 m/s² wejścia estymatora, bez nowej siły. Przy pierwszej stabilnej obserwacji podobnej prędkości nie zmieniamy gazu tylko przez bliskość.
4. Żądane przyspieszenie odwracamy przez **tę samą czystą funkcję RiderController.Forces**, w aktualnym stanie własnego motocykla: 0, 1, mały dodatni napęd i najwyżej 4 kroki bisekcji z interpolacją bracketu. Maksymalnie 7 ocen siły na aktywną ocenę podążania, zero dla wolnego korytarza. Nie ma kroku czasu, nowego stanu ciała, alternatywnego świata ani kopiowania równań siły. W ten sposób uwzględnione są rzeczywiste legacy, masa, setup, fade, opory, krzywizna, scrub, impairment i krąg przyczepności. Inercja przekazana do tej czystej oceny nie wpływa na zwracaną siłę; torque nie jest aplikowany do świata. Gas nie jest liniowym przelicznikiem prędkości.
5. Zwykła korekta jest ograniczona do ±0.25 Drive na nową komendę, martwa strefa 0.04. To ciągła wartość, nie trzy stopnie gazu. Bez istotnego zagrożenia Drive wraca do 1. Emergency jest rozpoznawane przy dodatnim zbliżaniu >0.25 m/s i `gR <= 0.15 m + closing²/(2*availableCoastDeceleration)`, z rzeczywistym przyspieszeniem 0-drive z tej samej funkcji; minimalne 0.1 m/s² w mianowniku zapobiega nieograniczonej estymacie czasu przy niemal zerowej skuteczności. Jest to konserwatywny krótki test przy stałej obserwowanej prędkości rywala. Emergency może natychmiast wybrać *komendę* 0 bez rampy, ale nadal czeka na reakcję. Nie dodaje siły hamowania.

W obecnym modelu dodatni napęd przed apexem może dawać dokładnie zerową siłę styczną (`DriveAvailability=0`), a Drive=0 ujawnia opory. Nie da się wtedy uzyskać małego ujemnego przyspieszenia samym dodatnim gazem. Regulator jawnie sprawdza ten skok oraz brak autorytetu napędu. Dlatego ON nadal może uzasadnienie całkowicie odjąć gaz: w A na 1.495 s, w C na 0.700 s i w G na 0.600 s. Są to korekty Following do aktualnego odstępu/tempa w fazie łuku, nie reakcja na sam brak zysku ataku. Nie poprawiono tej własności fizyki w PR #93.

## 3. Kolejki i współpraca z manewrem

Maksymalnie jedna oczekująca komenda linii i jedna gazu na zawodnika. Opcjonalny `ControlChannels` w DecisionEvent domyślnie Both zachowuje dotychczasowe API/OFF. Przy ON line aktualizuje wyłącznie TargetOffsetMeters/intencję i jej 0.8 s histerezy; throttle wyłącznie Drive. Stara komenda linii zawierająca Drive=1 nie otworzy gazu po nowszej korekcie. Korekty gazu nie resetują NextTacticAt i nie blokują szukania następnego ataku.

Każda kolejka zachowuje pełny istniejący czas reakcji od własnej obserwacji. Zwykłych oczekujących korekt nie nadpisujemy i nie przesuwamy ich deadline co 100 ms. Nowe Emergency anuluje oczekującą zwykłą korektę, także nieaktualne otwarcie, a nowa komenda ma własną pełną reakcję. Już oczekująca komenda bezpieczeństwa nie jest stale anulowana kolejną obserwacją. Zajęcie przecinanej ścieżki może odrzucić oczekującą linię; nie musi odjąć całego gazu. Terminalny Crashed/Finished przy ON anuluje obie pozostałe kolejki, więc nie aktywuje komendy po własnej mecie. OFF zachowuje również historyczny przypadek po mecie #92.

Nowa granica solvera powstaje wyłącznie dla rzeczywistej aktywacji istniejącego mechanizmu deadline; brak nowych odczytów świata, częstotliwość AI nadal 100 ms. Logical Hold nie rozcina czasu tylko przez nieużywaną wartość Drive w kanale Line. Bezpieczeństwo, zajęta przestrzeń i wykonanie manewru pozostają przed poszukiwaniem zysku. Podążanie trwa dopóki motocykle fizycznie współdzielą/przecinają korytarz, a nie dopóki zamiar nazywa się Attack.

## 4. Próby A–G: porównanie fizycznych wyników

Wszystkie jawne wejścia są w JSON każdej próby, w tym kanoniczne/legacy, rzeczywiste pozycje, prędkości i TrafficEnabled. A/C mają cztery motocykle, aby rzeczywiście zająć obie alternatywy ataku: tylko R1 ma AI; trzej pozostali jadą przez tę samą fizykę z ustalonymi z góry celami. Nie zamrażamy ich pozycji ani prędkości. W A R1=18 m/s, przód=16 m/s, środki początkowo 12 m od siebie, flankujący na 2.6/5.4 m. C zmienia jawnie możliwości napędu i początkowe tempo flankujących, którzy fizycznie odjeżdżają, odsłaniając korytarz; bez zdarzenia teleportującego przeszkodę. B/D/E/F/G są dwiema maszynami. F porównuje istniejące pary Speed/SlideControl 20/20 i 80/80, nie wnioskuje z nich o kanonicznym Attack. G zaczyna przy 32/38 m, po dwóch stronach wejścia w łuk.

`Completed` w krótkich próbach oznacza wykonane okno 2–10 s, nie cztery okrążenia. W pełnych biegach wymagamy czterech okrążeń, z limitem 90 s. Wszystko stopOnCrash=true. Min. dystans to mechaniczna separacja wszystkich kapsuł, a nie różnica centrów. Niebezpieczne zbliżenie: rzeczywista separacja <0.3 m przy dodatnim zbliżaniu >0.1 m/s do przodu, zliczana jako epizod i czas; miara diagnostyczna, nie nowe kryterium kolizji.
""")
parts.append(table(["Próba", "Status / czas s", "Zero / częściowy s (suma)", "Zamknięcia / epizody częściowe", "Min. cały motocykl m", "Niebezpieczne epizody / s", "Ponowny atak", "Kontakty"],
 [[link(r),r["Status"]+" / "+f(r["Result"]["TimeSeconds"]),f(sum(v["ZeroSeconds"] for v in r["Riders"]))+" / "+f(sum(v["PartialSeconds"] for v in r["Riders"])),
   str(sum(v["FullClosures"] for v in r["Riders"]))+" / "+str(sum(v["FollowingEpisodes"] for v in r["Riders"])),
   f(min(v["Measurement"]["MinimumPeerGapMeters"] for v in r["Riders"]),4),str(sum(v["DangerousEpisodes"] for v in r["Riders"]))+" / "+f(sum(v["DangerousSeconds"] for v in r["Riders"])),
   sum(v["Reattacks"] for v in r["Riders"]),r["Result"]["Work"]["Episodes"]] for r in pairs]))
parts.append("""
A: częściowy napęd utrzymuje rzeczywisty odstęp; minimum całych motocykli 0.550 m zamiast 0.611 m, bez kontaktu i bez epizodu <0.3 m. Nie uznajemy mniejszego prześwitu samego w sobie za zaletę. C: R1 podąża częściowo ponad 0.3 s, a następnie otrzymuje i fizycznie wykonuje AttackOutside (obserwacja 2.700 s, aktywacja 2.940 s, target 5.425382 m). Później wraca do szukania okazji; próba z 9.700 s pozostaje nieaktywna do końca 10 s, nie jest zaliczana jako wykonany atak. Kryterium reattack to aktywowana ofensywna komenda Line po co najmniej 0.3 s wcześniejszego częściowego napędu; nie zalicza ResumeIntent lub samego utrzymania intencji. Nie wymagamy wygranej tego manewru; nie ma potwierdzonego całego minięcia w C w tym oknie.

B: przez pierwsze 2 s ta sama prędkość i ścieżka nie powodują korekt ani linii. Późne 0.050 s częściowego napędu pojawia się dopiero gdy przód wcześniej wchodzi w geometrię łuku. E: 0 zamknięć i 0 częściowego gazu, mimo bliskości oraz zachowanych kandydatów Contest. G: wartości gazu, przyspieszenia i prześwitu pozostają skończone po obu stronach granicy; nieskończony TTC przy niedodatnim zbliżaniu oznacza brak takiego przewidywanego spotkania, nie NaN napędu.

D nie jest czyszczone przez regulator: ta sama fizyczna próba kończy się Crash i jednym kontaktem w OFF i ON, po pełnej reakcji na Emergency. Pozycje, prędkości, impulse, normal closing speed i severity ratios obu uczestników są w Result.Riders/Contacts. Nie było restartu do czystego wyniku. W żadnym innym A–G nie powstał kontakt; C ma mniej, a nie więcej bliskich epizodów według jawnej miary.

### Rzeczywista relacja gaz–tempo–odstęp

Poniższe zaakceptowane stany R1 pochodzą z przebiegu, a nie z prognozy. Wielokrotnie częściowy gaz nadal przyspiesza. Wspólny/przecinany korytarz oznacza aktywny powód oceny podążania; dodatni odstęp boczny może pozostać bezpieczny mimo zbieżności projekcji wzdłużnych.
""")
for name in ["A-blocked-follow","C-follow-reattack","G-straight-bend"]:
    r=read(name+"-ON")
    parts.append("\n#### "+name+" ON\n\n"+table(["Czas s", "Rywal", "Gap wzdłuż m", "Boczny m", "Prędkość własna / rywala m/s", "Gaz", "Korytarz", "Aktualny powód / manewr"],
      [[f(s["TimeSeconds"]),s["OpponentId"],f(s["Gap"]["GapMeters"]),f(s["Gap"]["LateralGapMeters"]),f(s["OwnSpeed"])+" / "+f(s["OpponentSpeed"]),f(s["Drive"]),s["Gap"]["CorridorOccupied"],
        next((d["Reason"] for d in reversed(r["Result"]["Decisions"]) if d["RiderId"]==1 and d["ActualActivation"] is not None and d["ActualActivation"]<=s["TimeSeconds"]),"Initial")]
       for s in r["Samples"] if s["RiderId"]==1]))
parts.append("""
W A sześć zmian kierunku korekty gazu ≥0.04 w 8 s obejmuje początkowe poznanie przyspieszania rywala, przygotowanie do łuku i powrót na gaz. C ma pięć. Nie ma sekwencji wielu pełnych 0↔1 przełączeń: w każdym z A/C/G jest jedno pełne zamknięcie, a zwykły powrót idzie krokiem maks. 0.25 po kolejnych aktywacjach. Te korekty nie dowodzą braku wszelkich oscylacji w innych warunkach. W C/8 s można pozostać za przeciwnikiem mimo pełnego gazu: pościg i rzeczywista możliwość przejścia nie są tym samym.

### F: rzeczywiste przyspieszenie po aktywacji, osobno od estymaty

Tabela pokazuje pierwsze kroki zaakceptowane po wybranych korektach napędu. Δv i przyspieszenie są mierzone z wykonanych prędkości; nie zastępują ich żądania regulatora. W chwili aktywacji świat jest o pełny czas reakcji późniejszy od obserwacji i mogą obowiązywać inne ograniczenia geometrii. Nie obiecujemy równości z żądanym przyspieszeniem sprzed reakcji.
""")
rows=[]
for name in ["F-low-capability","F-high-capability"]:
    r=read(name+"-ON")
    for response in r["Responses"][:5]:
        command=next((d for d in r["Result"]["Decisions"] if d["Channels"]==2 and d["RiderId"]==response["RiderId"] and d["ActualActivation"] is not None and abs(d["ActualActivation"]-response["ActivationBoundarySeconds"])<1e-7),None)
        rows.append([name,f(command["ObservedAt"]) if command else "—",f(command["RequestedActivation"]) if command else "—",f(response["ActivationBoundarySeconds"]),
                     f(response["NewDrive"]),f(response["BeforeSpeed"],6)+" → "+f(response["AfterSpeed"],6),f(response["ActualAlongAcceleration"],6)])
parts.append(table(["Próba", "Obserwacja s", "Żądana aktywacja s", "Rzeczywista s", "Drive", "Zmiana prędkości m/s", "Wykonane a wzdłuż m/s²"],rows))
parts.append("""
Te same wartości Drive nie kodują połowy Vmax. Istniejące Speed/SlideControl oraz faza zakrętu zmieniają siłę i skuteczność korekty. Osobny test przepuszcza ten sam Drive=0.5 przez niezmienione Forces w trzech różnych referencjach i obu poziomach możliwości; wartości przyspieszenia są różne, prędkość w danym stanie nadal wynosi 18 m/s.

## 5. Pełne biegi H: niezmienione rotacje #92

Wszystkie 8/8 OFF i 8/8 ON ukończone; 0 przerwanych, 0 kontaktów, 0 Crash, dodatni prześwit toru. Najpierw oceniono przypadki celowane, a potem wszystkie rotacje. Pozostałe rotacje nie są nową siatką profili. Poniżej pełne czasy oraz zmiana wobec identycznego OFF; większy czas jest pokazany normalnie.
""")
rows=[]
for a in [1,2]:
    for i in range(4):
        off=read(f"A{a}-rotation-{i}-OFF");on=read(f"A{a}-rotation-{i}-ON")
        for v,w in zip(off["Riders"],on["Riders"]):
            id=v["Measurement"]["RiderId"];t0=off["Result"]["FinishTimes"][str(id)];t1=on["Result"]["FinishTimes"][str(id)]
            rows.append([on["Name"],"R"+str(id),f(t0)+" → "+f(t1),f(t1-t0),f(v["ZeroSeconds"])+" → "+f(w["ZeroSeconds"]),
                         f(v["PartialSeconds"])+" → "+f(w["PartialSeconds"]),str(v["FullClosures"])+" → "+str(w["FullClosures"]),
                         f(v["Measurement"]["MinimumPeerGapMeters"])+" → "+f(w["Measurement"]["MinimumPeerGapMeters"])])
parts.append(table(["Bieg", "Rider", "Czas OFF → ON s", "Δ s", "Zero s", "Częściowy s", "Pełne zamknięcia", "Min. separacja m"],rows))
parts.append("""
W tych pełnych biegach ON **nie potrzebuje częściowego napędu**. Głównym błędem były reakcje na oddzielne linie; ich usunięcie nie jest samo w sobie dowodem działania following. Dowód częściowej regulacji pochodzi z A/C/F/G. W A1 R4 ma inną późniejszą linię i czas 62.584 s, więc całej różnicy jego czasu nie przypisujemy wyłącznie usunięciu 6.1 s zerowego gazu. A2/0 R4 jest około 1.294 s wolniejszy od OFF, przy 0 kontaktów w obu; brak wymuszenia poprawy kolejności. Późniejsze stany oraz okazje do manewrów zmieniają się po zmianie sterowania.

Ponowne ataki po wcześniejszym **częściowym** podążaniu w H=0, bo nie było takiego podążania; nie liczymy samych intencji lub KeepIntent. Wykonane próby ataku/Contest, prawdziwe minięcia, odstępy czasowe i sterowanie pozostają jawne poniżej. Min. clearance oraz lap times każdego ridera są w Measurement plików JSON. Potwierdzenie minięcia jest istniejącym konserwatywnym obserwatorem #92, z historią i ≥1 s/10 m utrzymania całego motocykla; nie liczy początkowych pól ani chwilowych zmian referencji.
""")
parts.append(table(["Bieg", "Kolejność", "Odstęp 1.→4. s", "Zamknięcia / częściowe epizody", "Atak / obrona / side intencje aktywowane", "Potwierdzone minięcia", "Zmiany aktuatorów", "Niebezpieczne epizody / s"],
 [[link(r)," > ".join("R"+str(v["RiderId"]) for v in r["Result"]["Classification"]),f(max(r["Result"]["FinishTimes"].values())-min(r["Result"]["FinishTimes"].values())),
   str(sum(v["FullClosures"] for v in r["Riders"]))+" / "+str(sum(v["FollowingEpisodes"] for v in r["Riders"])),
   "/".join(str(sum(v["Measurement"][k] for v in r["Riders"])) for k in ["Attacks","Defenses","SideManeuvers"]),
   len(r["Passes"]),r["Result"]["Work"]["ControlChanges"],str(sum(v["DangerousEpisodes"] for v in r["Riders"]))+" / "+f(sum(v["DangerousSeconds"] for v in r["Riders"]))] for r in heats]))
parts.append("""
Osiem main OFF ma 2 potwierdzone minięcia, ON ma 3. W A2/0 R2/R1 potwierdzono 47.520→25.390 s, a w A2/1 pojawia się potwierdzone R2/R1 w 25.840 s. To nie certyfikat uniwersalnej przewagi taktycznej; rola pola, linii i rzeczywistego tempa jest nadal istotna. Obrona pozostaje słabo pobudzona w tej małej próbce; regresje historycznych mechanizmów #89/#90 zachowują zachowanie OFF.

## 6. Koszt i dodatkowe granice solvera

Release Windows/.NET 8.0.26, DOTNET_TieredCompilation=0, bez observera i plików trajektorii w benchmarku. Jeden warmup, trzy zmierzone próbki na tryb. CPU to czas procesu z kwantyzacją platformy, alokacje dotyczą tylko bieżącego wątku zarządzanego, bez native memory. Inne procesy testujące nie działały podczas tego benchmarku. Wartości są lokalnym pomiarem, nie obietnicą sprzętową.
""")
rows=[]
for name in ["A-blocked-follow","A1-rotation-0","A2-rotation-1"]:
    for enabled in [False,True]:
        group=[r for r in perf if r["Name"]==name and r["Enabled"]==enabled]
        med=lambda k:statistics.median(r[k] for r in group)
        work=lambda k:statistics.median(r["Work"][k] for r in group)
        rows.append([name+(" ON" if enabled else " OFF"),f(med("ElapsedMilliseconds")),f(med("CpuMilliseconds")),f(med("AllocatedBytes")/1048576),
                     f(work("TacticalElapsedMilliseconds")),int(work("PhysicsUpdates")),int(work("SolverCalls")),int(work("SolverSubsteps")),int(work("ThrottleForceEvaluations"))])
parts.append(table(["Próba", "Wall ms", "CPU ms", "MiB", "AI/taktyka ms", "Kroki 10 ms", "Wywołania solvera", "Substeps", "Oceny istniejącej siły dla gazu"],rows))
parts.append("""
[Trzy próbki każdego trybu i liczniki](proportional-throttle-control/performance.json). A ma 899→1138 wywołań solvera (+26.6%) i 20.434→24.841 ms wall (+21.6%): pozostawanie blisko oraz nowe aktywacje zmieniają również istniejące podziały proximity/krzywizny, więc nie nazywamy całej różnicy kosztem samej formuły gazu. Czas całej taktyki A rośnie ok. 0.165→0.408 ms. W H czas oraz liczba kroków świata i solvera maleją, bo zmienia się przebieg i czas dojazdu ostatniego zawodnika; nie jest to dowód optymalizacji solvera. Taktyka A1/0 ok. 1.882→4.671 ms, A2/1 2.501→5.347 ms na cały bieg. Osiemnaście benchmarkowych hashy zgadza się z odpowiednim normalnym przebiegiem ON/OFF.

Nie wprowadzono dodatkowej częstotliwości AI ani odrębnych klatek fizyki do regulatora. Bez przeciwnika liczba ocen siły nowego regulatora wynosi 0, a solo ON zachowuje dokładny hash OFF. Z zapisu szczegółowego wybrano A1/0 i A2/1 oraz okna A–G: próbki co 0.5 s, istotne komendy, ich aktywacje i pierwszy rzeczywiście wykonany krok po zmianie gazu. Pozostałe rotacje zapisują metryki i zdarzenia; żadnych wielkich snapshotów co krok. Koszt audytu jest poza bare benchmarkiem.

## 7. Regresje, deterministyczność i ocena

- 118/118 celowanych testów Release: 15 nowych i 103 dotychczasowe SharedWorld/SideBySide/Quality/audytu #92. Nowe testy sprawdzają A–G, fizyczne skutki opóźnienia i kontaktu, kolejki, brak nadpisania częściowego gazu przez starą linię Drive=1, brak odsuwania oczekującej reakcji bezpieczeństwa, dokładne solo bez rywala i wszystkie osiem OFF z #92 (hashe, końcowe ciała, komendy, kontakty, mety).
- [Pięć wybranych powtórzeń ON](proportional-throttle-control/repeats.json) w osobnym świeżym procesie, z odwróconą kolekcją: A, C, A1/0, A2/0, A2/1. Wszystkie hashe, komendy i kontakty dokładnie identyczne. Trace ON/OFF nie steruje symulacją.
- Restore i warnings-as-errors Release build: sukces. Guard 39 produkcyjnych konsumentów: sukces. Discovery 2631/2631 z jedną odpowiedzialnością (core 1715, trajectory 395, historical-analysis 473, four-rider 48); bez braków/duplikatów. Python 89+7 testów: sukces. Standardowy Windows CI bez zmiany workflow; wynik dla opublikowanego HEAD jest w Checks Draft PR, nie jest z góry deklarowany przez ten dokument jako zielony.

**A — DZIAŁA w tym zestawie**, przy zachowaniu default OFF i PROVISIONAL stałych. Uzasadnienie: kontrolowane częściowe podążanie z rzeczywistym prześwitem, późniejszy wykonany atak, brak reakcji na samą bliskość B/E, zachowany pilny release i nieunikniony Crash D, bez dodatkowych kontaktów w głównych biegach i lepsza miara bliskiego zbliżenia w C. Nie ma mechanicznego bonusu do przyspieszenia ani wymagania wygranej. Zielone CI jest regresją, a nie podstawą tej oceny.

Ograniczenia: prognoza rywala jest krótka i oparta na bieżącej prędkości/przeszłej obserwacji, bez wiedzy o jego przyszłych zmianach; PD odstępu nie jest certyfikatem bezkolizyjności. Potrzebna jest osobna walidacja większej liczby prawdziwych nagłych zmian tempa i dłuższej stabilności kolejki, bez strojenia do A1. W obecnym neutral-carry niektóre ujemne żądania dają skok na zero gazu; nie wymuszamy sztucznie dodatniej wartości dla ładniejszego histogramu. Obrona i jazda partnerów nie zostały tu przebudowane. Mała próbka nie potwierdza prawdopodobieństw zwycięstw ani gotowości produkcyjnego HeatSimulator.

Odtwarzanie: `dotnet build SpeedwayManager.sln -c Release --warnaserror`, następnie `dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll throttle-diagnose <dir>` dla #92 oraz `throttle-audit <dir> pairs`, `heats`, `repeat`; osobny proces `benchmark` z DOTNET_TieredCompilation=0. Na końcu `python tools/shared-world-speedway/throttle-report.py <dir>`. Generator weryfikuje zgodność OFF, opóźnienia, powtórzenia i hashe benchmarku przed utworzeniem dokumentu. Manifest SHA-256 obejmuje wszystkie surowe pliki; zachowano także wszystkie dowody #92 bez zmian.
""")
directory.with_suffix(".md").write_text("\n\n".join(parts).rstrip()+"\n",encoding="utf-8")
manifest={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(directory.iterdir()) if p.is_file() and p.name!="manifest.json"}
(directory/"manifest.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf-8")
print("Verified 8 OFF/8 ON heats, 8 OFF/8 ON windows, repeats and 18 benchmark hashes; report saved.")
