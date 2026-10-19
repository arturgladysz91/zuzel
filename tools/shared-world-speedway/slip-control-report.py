"""Read #98 evidence only; never run dynamics or rewrite historical captures."""
import hashlib
import json
from pathlib import Path
from statistics import median

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "docs/experiments/slip-control-validation"


def read(name):
    return json.loads((DATA / (name + ".json")).read_text())


def rider(name):
    return read(name)["Summary"]["Riders"][0]


def table(headers, rows):
    return "\n".join(["| " + " | ".join(headers) + " |", "|" + "---|" * len(headers)]
                     + ["| " + " | ".join(str(x) for x in row) + " |" for row in rows])


def fmt(value, digits=3):
    return f"{value:.{digits}f}"


def generate():
    a, plan = read("A-gas-comparison"), read("plan")
    heat, contact, coast = (read(n)["Summary"] for n in ["D1-four-laps", "D2-active-contact", "D-zero-drive"])
    parity = read("D1-parity")
    assert parity["Forward"] == parity["Reverse"] == parity["Unobserved"] == heat["Fingerprint"]
    assert len({trial["Prefix"] for trial in a["A2"]}) == 1
    assert len({trial["Slip"]["LateralForceNewtons"] for trial in a["A1"]}) == 1
    assert read("D2-parity")["SameContactEvents"]
    all_runs = [json.loads(f.read_text()) for f in DATA.glob("*.json") if "Summary" in json.loads(f.read_text())]
    assert all(r["Summary"]["Completed"] for r in all_runs)
    assert all(v["EnergyLimitedPercent"] == 0 and v["MaximumAppliedTorqueMismatch"] == 0
               for run in all_runs for v in run["Summary"]["Riders"])
    for folder in ["autonomous-racing-line", "slip-coupled-dynamics"]:
        old = ROOT / "docs/experiments" / folder
        for path, expected in json.loads((old / "manifest.json").read_text()).items():
            assert hashlib.sha256((old / path).read_bytes()).hexdigest() == expected, path
    entry_names = ["B-entry-0.08", "pilot-middle-control", "B-entry-0.15"]
    full_names = ["B-full-0.08", "B-full-0.10", "B-full-0.15"]
    lines = [("2 m", "C-fixed-2"), ("5 m", "B-full-0.10"), ("8 m", "pilot-wide-control"), ("8 → 5 → 3 → 8 m", "C-wide-tighten")]
    old, new, observed = read("performance-base97"), read("performance-new98"), read("performance")
    base_us, new_us = [median(d["Milliseconds"]) * 1000 / d["Calls"] for d in [old, new]]
    base_ms, new_ms = [median(v["Milliseconds"] for v in d["Worlds"]) for d in [old, new]]
    noop_us = [median(v["Milliseconds"]) * 1000 / v["Calls"] for v in observed["Forces"]]
    noop_ms = [median(v["Milliseconds"] for v in observed["Worlds"] if v["Observers"] == on) for on in [False, True]]
    assert {v["Fingerprint"] for d in [old, new] for v in d["Worlds"]} == {heat["Fingerprint"]}
    wide = rider("C-wide-tighten")
    text = f"""# Slip dynamics — walidacja sterowania i podstawy kalibracji (#98)

**Ocena A: kompletna diagnoza tego zakresu. PROVISIONAL, bez potwierdzenia realizmu empirycznego. Rekomendacja 1: osobna kalibracja obecnego modelu.** Gaz zmienia późniejszy stan, lecz nie steruje niezależnym wheelspinem. Limiter energii pozostaje nieaktywny w zmierzonych przejazdach; nie odpowiada za ich opóźnienie orientacji. Wszystkie cztery strategie oraz czterookrążeniowy wspólny świat pozostają na torze. Zacieśnienie ma zauważalne opóźnienie wykonania.

Baza: otwarty Draft [#97](https://github.com/arturgladysz91/zuzel/pull/97), `experiment/slip-coupled-dynamics`, HEAD `{read('configuration')['BaseHead']}`. Gałąź `experiment/slip-dynamics-validation`, target gałęzi #97. Bez merge. Równania, parametry domyślne, `src/`, ContactAdapter i viewer bez zmian. `EnableSlipCoupledDynamics=false` domyślnie. Wyniki dotyczą ON z całkowicie wyłączonym starym scoringiem oraz `TrafficEnabled=false`.

## Metoda i zakres dowodów

Jedna Motoarena, natywna geometria TrackFrame, lap reference {read('configuration')['LapMeters']:.6f} m, fizyczne szerokości 12/16,6 m. Każdy neutralny profil ma wszystkie stare Skills i osiem canonical Abilities =50, masę człowieka 70 kg, motocykl 77 kg, setup Neutral; actual Box2D m=147 kg, I={read('configuration')['InertiaKgMetersSquared']:.5f} kg·m². Zamrożona domyślna nawierzchnia: Grip=1, Ruts=0, Moisture=0,5. Krok bazowy 10 ms, cztery wewnętrzne substeps; istniejące deadline/curvature/proximity splits pozostają aktywne. Każdy przejazd ma jeden rzeczywisty świat Box2D.

[Konfiguracja](slip-control-validation/configuration.json) zachowuje także canonical profil, którego standardowy serializer RiderProfile nie eksportuje. Poszczególne JSON zawierają pełne początkowe pozycje i komendy. Wszystkie metryki całkowe, maksima i clearance liczone są **na każdym zaakceptowanym przedziale**, z wagą jego faktycznego dt. JSON telemetrii zachowuje jedną próbkę pre-step co 0,1 s w krótkich próbach / 0,5 s w długich; CSV około co 0,1 s i po zmianach sterowania. Próbka siły opisuje zaakceptowany stan przed krokiem, CSV jego zaakceptowane końce; nie są zamienne. A2 ma dodatkowo dokładne okna. Wartości czasu nie są arbitralnymi kryteriami zaliczenia testów.

{table(['Eksperyment','Przebiegi i warunek','Wynik'], [
 ['A1','Jeden zaakceptowany stan łuku, Drive 0,40/0,70/1,00','Fy i yaw identyczne; Fx oraz moc różne'],
 ['A2','3 pełne prefiksy, zmiana w 2,82 s; +0,2/+0,5/+1,0 s','Prefiksy bitowo równe, późniejsze stany różne'],
 ['B1/B2','3 czasy yaw; wejście 5 s + 2 indywidualne obiegi','0% limitu energii; wszystkie na torze'],
 ['C','Stałe 2/5/8 m + zamrożone zacieśnienie, po 2 obiegi','4/4 ukończone; manewr fizyczny z opóźnieniem'],
 ['D1','4 neutralnych zawodników × 4 indywidualne obiegi','4/4 ukończyło, bez kontaktu i wyjazdu'],
 ['D2','2 aktywne ciała, start bez przenikania, 2 s','Rzeczywisty kontakt i dodatnie impulsy'],
 ['Energia','Drive=0, 8 s; dodatkowo testy małych prędkości/kroku','Brak wzrostu zaakceptowanej energii'],
 ['Opona','18 punktów: 3 u × 6 kątów przy stałych B i m','Monotoniczne Fy, malejąca rezerwa Fx']])}

## A. Gaz: efekt bezpośredni i pośredni

Moment aktywacji wybrano **przed porównaniami**, z pierwszego zaakceptowanego stanu kontrolnego środkowej linii przy CornerProgress≥0,20, zaokrąglając czas w górę do 10 ms: **{a['GasAtSeconds']:.2f} s**. To rzeczywisty łuk: ref={a['State']['TotalReferenceMeters']:.6f} m, offset={a['State']['Road']['OffsetMeters']:.6f} m, u={a['State']['ForwardSpeed']:.6f}, w={a['State']['SideSpeed']:.6f} m/s. A1 ocenia ten zaakceptowany stan bez jego reintegracji; A2 odtwarza całe przebiegi od początku. [Stan, siły i sparowane okna](slip-control-validation/A-gas-comparison.json).

{table(['Drive','Fx N','Fy N','α °','B N','Usage','P napędu W','P strat bocznych W'], [[fmt(v['Slip']['Drive'],2),fmt(v['Slip']['DriveForceNewtons']),fmt(v['Slip']['LateralForceNewtons']),fmt(v['Slip']['ActualSlipDegrees']),fmt(v['Slip']['GripBudgetNewtons']),fmt(v['Slip']['GripUtilization'],6),fmt(v['DrivePowerWatts']),fmt(v['Slip']['LateralLossWatts'])] for v in a['A1']])}

**Bezpośrednio:** Fy=−B·w/sqrt(w²+δ²) nie zawiera Drive. Zmiana gazu nie odbiera Fy części budżetu: to Fx otrzymuje pozostałą rezerwę sqrt(B²−Fy²). Przy tym stanie nie ma jeszcze saturacji Fx. Zadany kąt i yaw też pozostają takie same (moment {a['A1'][0]['Slip']['TorqueNewtonMeters']:.6f} N·m). Model nie potrafi przy tym samym stanie zmniejszyć bocznej reakcji przez sam wzrost napędu.

**Dynamicznie:** siła napędowa zmienia u/w, prędkość, orientację i następne żądanie regulatora. Wszystkie próby mają identyczne zaakceptowane stany aż do aktywacji, włącznie z końcem poprzedniego kroku; SHA-256 pełnego prefiksu `{a['A2'][0]['Prefix']}`. Nie odtwarzano ręcznie żadnego snapshotu. Pierwsze wywołanie sił przy t=2,82 s ma już nowy Drive, lecz jeszcze nie zmieniony stan mechaniczny.

{table(['Δt s','Drive','α °','Fx/Fy N','Speed m/s','Offset m','Yaw rad/s','Usage','Δref m'], [[fmt(t['TimeSeconds']-a['GasAtSeconds'],1),fmt(v['Drive'],2),fmt(t['ActualSlipDegrees']),f"{t['DriveForceNewtons']:.1f}/{t['LateralForceNewtons']:.1f}",fmt(t['Speed']),fmt(t['OffsetMeters']),fmt(t['AngularVelocity']),fmt(t['GripUtilization'],4),fmt(t['ReferenceMeters']-a['State']['TotalReferenceMeters'])] for v in a['A2'] for t in v['Windows'][1:]])}

Po sekundzie Drive 0,40→1,00 daje różnicę α={a['A2'][2]['Windows'][-1]['ActualSlipDegrees']-a['A2'][0]['Windows'][-1]['ActualSlipDegrees']:.3f}° oraz prędkości {a['A2'][2]['Windows'][-1]['Speed']-a['A2'][0]['Windows'][-1]['Speed']:.3f} m/s. To **pośrednia odpowiedź całego modelu**, nie dowód sterowania poślizgiem napędowym tylnego koła. Nie mamy stanu obrotów koła, slip ratio ani jego niezależnej wymiany sił z podłożem; same zmiany stałych Fy tego nie dodadzą. Nie ma też empirycznego celu, który w tych próbach wykazałby konieczność takiej nowej mechaniki jako następnego kroku.

## B. Moment i wrażliwość regulatora

Nowy opcjonalny `YawLimit` zapisuje raw I·yawAcceleration, wynik istniejącego clampu ±0,5·B z authority, wynik limitu pracy, błąd orientacji, spin, I oraz kredyt dyssypacji. `AppliedForce` odczytuje dokładne argumenty zaraz po ContactAdapter.Force (Box2D ApplyForce/ApplyTorque), bez modyfikacji. Slip podaje desired/actual angle, B, siły i moce. Przy wyłączonych obserwatorach nie są obliczane te dodatkowe etapy ani tworzone obserwacje. Oryginalne działania sił pozostają w tej samej kolejności.

Limit energii zmienia moment przez **0,000% czasu we wszystkich przejazdach**. Średnia/maksymalna redukcja i całkowa frakcja usuniętego momentu: **0 N·m / 0 N·m / 0%**. Różnica after-energy vs moment do Box2D: dokładnie 0. Saturacja siłowego limitu yaw również 0%; największy stosunek |raw|/(0,5·B) w C-wide wynosi {wide['MaximumRequestedToForceLimitRatio']:.6f}. Osobno authority neutralnego Technique=50 skaluje niezerowe żądania przez 0,875; to stała redukcja 12,5%, nie wystąpienie saturacji ani aktywacja guardu energetycznego. Pole AuthorityScalingPercent liczy przedziały, w których ta redukcja przekracza 1e−5 N·m.

Nie ma przedziałów ograniczenia energetycznego współwystępujących z dużym błędem (próg |error|>5° ustalony w narzędziu); zbiór ograniczony jest pusty. Warunkowe średnie dla pustego zbioru mają w JSON 0 i **nie oznaczają** zmierzonej korelacji. Ogranicznik nie może być przyczyną opóźnienia lub błędu offsetu w tych konkretnych przebiegach, bo nie zmienia ich momentu. Nie wynika z tego jego nieszkodliwość dla wszystkich możliwych stanów. Osobny test siatki stanów wymusza oba rzeczywiste ograniczenia i chroni ich rachunek; nie wyłączono guardu w żadnym przejeździe.

{table(['Yaw s','Wejście s','Desired/actual ≥5° s','Śr./max error ° (5 s)','Max błąd offsetu m','Min clearance m'], [[fmt(y,2),fmt(rider(n)['EntrySeconds']),f"{rider(n)['TargetFiveDegreesSeconds']:.3f}/{rider(n)['ActualFiveDegreesSeconds']:.3f}",f"{rider(n)['MeanAbsoluteOrientationErrorDegrees']:.3f}/{rider(n)['MaximumAbsoluteOrientationErrorDegrees']:.3f}",fmt(rider(n)['MaximumOffsetErrorMeters']),fmt(rider(n)['MinimumClearanceMeters'])] for y,n in zip([.08,.10,.15],entry_names)])}

Próg 5° jest obserwacją reakcji, nie definicją poprawnego wejścia. Domyślnie rzeczywisty kąt osiąga go 0,21 s po początku łuku i 0,20 s po żądaniu; w tej chwili guard pozostaje nieaktywny. Źródła opóźnienia należy szukać w żądaniu preview i paśmie regulatora. To wskazanie kalibracyjne, nie wykryty błąd algorytmu.

{table(['Yaw s','Lap 1/2 s','Śr./max error °','Max offset error m','Min clearance m','Max usage / ≥95% s','ΔK−W J'], [[fmt(y,2),'/'.join(fmt(t) for t in rider(n)['LapSeconds']),f"{rider(n)['MeanAbsoluteOrientationErrorDegrees']:.3f}/{rider(n)['MaximumAbsoluteOrientationErrorDegrees']:.3f}",fmt(rider(n)['MaximumOffsetErrorMeters']),fmt(rider(n)['MinimumClearanceMeters']),f"{rider(n)['MaximumUtilization']:.4f}/{rider(n)['NearGripSeconds']:.3f}",fmt(rider(n)['WorkResidualJoules'])] for y,n in zip([.08,.10,.15],full_names)])}

Wszystkie warianty kończą dwa obiegi, bez kontaktu/upadku/wyjazdu. Pozostałe wejścia identyczne. Zmiana `SlipYawResponseSeconds` z definicji #97 zmienia także zewnętrzny T=0,65+4·yaw (0,97/1,05/1,25 s); nie jest izolowaną zmianą jednego pasma wewnętrznego. 0,08 s jest nieco szybsze i ma mniejszy błąd kąta, lecz większy błąd offsetu i mniejszy prześwit niż default. **Pozostaje 0,10 s**; sam lap time nie uzasadnia zmiany. Wszystkie te współczynniki nadal wymagają kalibracji.

## C. Wykonane linie

Te same umiejętności/setup/surface i prędkość początkowa 18 m/s, ref0; początkowy offset odpowiada stałej linii (2/5/8), a wariant mieszany startuje z 8 m. Porównanie nie izoluje identycznej pozycji startowej między trzema różnymi stałymi liniami; mieszana vs stała zewnętrzna ma identyczny prefiks. Żadnej idealnej ścieżki XY. Maksymalny legalny target=9,5 m, wszystkie użyte cele mieszczą się w granicach. Parametry regulatora domyślne, Drive=1 przez cały przebieg.

{table(['Strategia','Lap 1/2 s','Droga 2 obiegi m','Entry/min/exit m/s','Max następnej prostej m/s','Śr./max |α| °','≥95% s','Strata boczna kJ','Max offset error m','Min clearance m'], [[label,'/'.join(fmt(t) for t in rider(n)['LapSeconds']),fmt(rider(n)['DistanceMeters']),'/'.join(fmt(rider(n)[k]) for k in ['EntrySpeed','MinimumCornerSpeed','ExitSpeed']),fmt(rider(n)['NextStraightMaximumSpeed']),f"{rider(n)['MeanAbsoluteSlipDegrees']:.3f}/{rider(n)['MaximumAbsoluteSlipDegrees']:.3f}",fmt(rider(n)['NearGripSeconds']),fmt(rider(n)['SlipLossJoules']/1000),fmt(rider(n)['MaximumOffsetErrorMeters']),fmt(rider(n)['MinimumClearanceMeters'])] for label,n in lines])}

Prędkości dotyczą pierwszego pełnego łuku i następującej prostej; kąty/straty/błędy obejmują oba obiegi. Droga z zaakceptowanych XY, włącznie z końcem ostatniego kroku; lap crossing interpolowany indywidualnie, bez generowania nowego stanu fizycznego. Clearance dotyczy całej mechanicznej kapsuły względem fizycznej krawędzi, nie środka ciała ani usable target span.

Czasy manewru zamrożono z osobnego kontrolnego przejazdu stałej linii 8 m, zanim porównano wyniki: target5 przy cp≥0,20, target3 przy cp≥0,55, target8 na początku kolejnej prostej. [Plan](slip-control-validation/plan.json): pierwsze terminy 2,91 / 4,93 / 7,58 s; dalsze z drugiego łuku i drugiego obiegu są jawne. Fizyczna faza manewru może później odbiegać od fazy pilota; nie przesuwano terminów pod jego wynik. Wszystkie komendy aktywują się we wskazanym terminie.

Pierwszy łuk mieszanej linii: offset entry **{wide['EntryOffset']:.3f} → exit {wide['ExitOffset']:.3f} m**. Target3 nie jest idealnie osiągany przed zmianą na target8; w zachowanym CSV/telemetrii widać opóźnienie. Maksymalny błąd {wide['MaximumOffsetErrorMeters']:.3f} m obejmuje skok intencji przy powrocie na 8 m i nie jest teleportacją. Trajektoria jest wykonalna w granicach toru, ale wykonanie nie jest dokładnym śledzeniem targetu. Jest szybsza od stałej zewnętrznej i wolniejsza od środkowej/wewnętrznej; nie daje podstaw do uniwersalnej przewagi szerokiego wejścia.

## D. Pełny bieg, kontakt i energia

D1: ref0/4/8/12 m, offset2/4/6/8 m, neutralni zawodnicy, 18 m/s. Każdy pokonuje **cztery indywidualne pełne lap reference od swojej pozycji początkowej**, w tym samym świecie; nie liczymy końca jako wspólnego przekroczenia 4·L przez różne początkowe pozycje.

{table(['Rider','Lap 1/2/3/4 s','Meta s','Min clearance m','Max offset error m','ΔK−W J'], [[v['RiderId'],'/'.join(fmt(t) for t in v['LapSeconds']),fmt(heat['FinishTimes'][str(v['RiderId'])]),fmt(v['MinimumClearanceMeters']),fmt(v['MaximumOffsetErrorMeters']),fmt(v['WorkResidualJoules'])] for v in heat['Riders']])}

Cały świat {heat['TimeSeconds']:.3f} s, **{heat['SolverCalls']} solver calls / {heat['SolverSubsteps']} substeps**, zero kontaktów/upadków, min rzeczywista odległość między kapsułami {heat['MinimumPairSeparationMeters']:.6f} m, max zaakceptowane przesunięcie {heat['MaximumStepDisplacementMeters']:.6f} m. WorldRunner sprawdza finite oraz limit przemieszczenia na każdym kroku; nie stwierdzono nieciągłości ani przenikania. Clearance/energia są obserwowane aż do zakończenia całego świata, także gdy szybszy zawodnik już finiszował. Suma ΔK−W={sum(v['WorkResidualJoules'] for v in heat['Riders']):.6f} J; wzrost K przy dodatnim napędzie ma pokrycie w pracy sił i momentu. Nie jest sam w sobie błędem energii.

Fingerprint forward = reverse = bez obserwatorów = niezmieniony #97 w osobnym cost probe: `{heat['Fingerprint']}`. Identyczne solver calls, bez dodatkowych kroków do diagnostyki. [Dowód D1](slip-control-validation/D1-parity.json).

D2: dwa **aktywne** ciała na offset5, ref0/3, 20/18 m/s, Drive=0,40 od t0, target5. Początkowe kapsuły rozłączne (walidacja native MechanicalSeparation), bez wymuszenia Crash i bez edycji prędkości. **{contact['Episodes']} rzeczywisty epizod, {contact['ContactPointCount']} obserwacji punktów, {contact['SolvedPoints']} rozwiązanych punktów z dodatnim impulsem**, max total-normal impulse próbki {contact['MaximumContactImpulse']:.6f} N·s. Liczby to wynik, nie warunek strojenia. {contact['SolverCalls']} calls; maksimum prędkości {max(v['MaximumSpeed'] for v in contact['Riders']):.6f} m/s. Min signed gap {contact['MinimumPairSeparationMeters']:.9f} m (około −0,57 µm, tolerancja kontaktu), końcowe kapsuły bez istotnego przenikania. Brak numerycznej eksplozji; istniejący solver wykrywa i rozstrzyga kontakt. Fingerprint i zdarzenia identyczne przy odwróceniu kolejności: `{contact['Fingerprint']}`.

W kontakcie indywidualne ΔK−W mają przeciwne duże wartości (transfer między ciałami), a ich suma wynosi **{sum(v['WorkResidualJoules'] for v in contact['Riders']):.6f} J**. W nie obejmuje pracy impulsów kontaktowych, więc tej ujemnej sumy nie wolno nazywać błędem samego integratora: jest zgodna ze stratą w niesprężystym kontakcie/tarciu. Nie dodano mechanizmu upadku; oba ciała pozostają aktywne.

Osobny Drive=0 na rzeczywistym łuku, 8 s: K translacji+obrotu **{coast['Riders'][0]['InitialKineticJoules']:.6f} → {coast['Riders'][0]['FinalKineticJoules']:.6f} J**. Największy przyrost pojedynczego kroku {coast['Riders'][0]['MaximumZeroDriveEnergyIncreaseJoules']:.9f} J (ujemny), bez wzrostu; ΔK−W={coast['Riders'][0]['WorkResidualJoules']:.6f} J. Testy #97 nadal obejmują małą prędkość, side slip i kroki 5/10/20 ms. Nie znaleziono nowego fizycznego błędu wymagającego naprawy w tym PR. Zakres nie obejmuje pełnego czterookrążeniowego wyścigu z wieloma kontaktami, zużycia nawierzchni ani ekstremalnie gęstej walki.

## E. Krzywa opony i granice kalibracji

Stałe B=1800 N i m=147 kg. Dla każdego u ustalamy w=−u·tan(α). Silnik w tabeli żąda 0 N: usage=|Fy|/B; rezerwa Fx=sqrt(B²−Fy²), przy jej pełnym wykorzystaniu combined usage=1. Strata to −Fy·w. To czysta ewaluacja dokładnie istniejącego Tyre, bez nowej krzywej lub zmiany parametrów. [Pełna siatka](slip-control-validation/tyre-grid.json).

{table(['u m/s','α °','Fy N','Boczna usage','Pozostały Fx N','Strata boczna W'], [[fmt(v['ForwardMetersPerSecond'],0),fmt(v['SlipDegrees'],0),fmt(v['LateralNewtons']),fmt(v['LateralUtilization'],4),fmt(v['RemainingDriveNewtons']),fmt(v['LateralLossWatts'])] for v in read('tyre-grid')])}

Geometria dostarcza promienia, rzeczywistej długości i szerokości, krawędzi oraz preview krzywizny. SegmentPhysics dostarcza odziedziczone safe²/R i zależności surface/skills/setup; B=m·safe²/R jest capability gry, nie pomiarem μ. Nowa krzywa Fy dostarcza bocznej dyssypacji i monotonicznej saturacji; jej sztywność 0,12 i regularizacje 0,5 m/s oraz 0,03 s pozostają PROVISIONAL, bez dopasowania danych opony. Limit yaw ogranicza pracę efektywnego aktuatora; nie zastępuje mechaniki kierowania, przechyłu i żyroskopu. Nie ma rozdziału przód/tył, load transfer, wheel speed, slip ratio ani niezależnego wheelspinu. Krzywa nie ma skalibrowanej gałęzi spadku siły za maksimum. Nie deklarujemy zgodności z prawdziwym motocyklem.

Do osobnej kalibracji: odziedziczony budżet/napęd/opory na dobranej geometrii i nawierzchni, charakterystyka Fy i straty, następnie preview oraz współzależne czasy obu pętli i Technique authority. Nie należy kompensować błędu jednej warstwy stałą innej. Samym strojeniem nie powstanie niezależny stan napędowego poślizgu koła. Bieżące dowody nie wykazują jednak, że brak tego stanu blokuje wymagane tu fizyczne utrzymanie i zmianę linii.

## Walidacja, koszt i odtworzenie

**27/27 testów celowanych** (5 nowych + 20 #97 + 2 geometrii), restore i Release warnings-as-errors poprawne. Nowe testy chronią dokładne ON #97/observer parity, oba aktywne limity na siatce, rachunek momentu do Box2D i 0 B, bitowy prefiks gazu, przyczynowe komendy linii, pełny D1 reverse oraz prawdziwy aktywny kontakt. Istniejące testy chronią OFF każdy ForceObservation/fingerprint, oryginalny autonomiczny replay #95 i rzeczywistą energię Drive=0. [Zapis walidacji](slip-control-validation/validation.json). Standardowe Draft Windows CI na końcowym HEAD: status i link w opisie PR; historyczne audyty #87–#97 nie były powtarzane.

34/34 hashy manifestu #95 i 63/63 #97 zgodne. Historyczny baseline #97 pozostaje o SHA-256 `2f9942bd2b32ea9f362c6d4811f2a0dd15c84c44aa89f54adee8ca30c6d5aae7`. Wszystkie 15 nowych par CSV/JSON przechodzą istniejący parser Replay #96. Dane mają Run.Initial/Result/Riders.Measurement/Consequences; nie rozszerzono UI ani katalogu wyboru viewer. Pliki można przekazać istniejącemu modelowi Replay; ich wybór w UI nie jest nową funkcją tego PR. Potwierdzone minięcia nie są eksportowane w tym nowym zestawie.

Koszt: Windows / .NET 8.0.26, Release, tiered JIT wyłączony, 10k warmup + 7 partii po 100k Forces, cały D1 po jednym rozgrzewającym przebiegu i 7 pomiarach. [Identyczny probe bazowy](slip-control-validation/baseline-cost-probe.cs) skompilowano w izolowanych drzewach #97 i #98; te same specyfikacje i fingerprint. Mediany:

{table(['Porównanie','Baza','Wariant','Zmiana'], [
 ['Bez obserwatorów: Forces #97 → #98',f'{base_us:.6f} µs',f'{new_us:.6f} µs',f'{100*(new_us/base_us-1):+.2f}%'],
 ['Bez obserwatorów: D1 #97 → #98',f'{base_ms:.4f} ms',f'{new_ms:.4f} ms',f'{100*(new_ms/base_ms-1):+.2f}%'],
 ['#98 pusty pełny zestaw observerów: Forces',f'{noop_us[0]:.6f} µs',f'{noop_us[1]:.6f} µs',f'{100*(noop_us[1]/noop_us[0]-1):+.2f}%'],
 ['#98 pusty pełny zestaw observerów: D1',f'{noop_ms[0]:.4f} ms',f'{noop_ms[1]:.4f} ms',f'{100*(noop_ms[1]/noop_ms[0]-1):+.2f}%']])}

Forces ma **0 B/call we wszystkich partiach**, również z pustymi delegatami. Ujemna niewielka różnica #97→#98 mieści się w szumie tego pomiaru, nie jest deklaracją przyspieszenia. Przy wyłączeniu nowa ścieżka dodaje sprawdzenia null, bez obliczeń etapów, kolekcji lub dodatkowych solver calls. Test obu limitów i pomiar chronią brak alokacji. Pełny zestaw pustych delegatów obejmuje także dotychczasowe Force/Slip; jego narzut nie oznacza kosztu samego nowego YawLimit. Rzeczywisty kolektor audytu świadomie alokuje poza gorącą ścieżką i wykonuje dodatkowe pomiary obrysów/serializację. Tabele nie mierzą jego I/O i nie porównują czasów między różnymi harnessami.

```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
dotnet run --project tools/shared-world-speedway -c Release -- slip-control-validation NEW_DIRECTORY configuration
dotnet run --project tools/shared-world-speedway -c Release --no-build -- slip-control-validation NEW_DIRECTORY pilot
# plan.json jest zapieczętowany przed następnym krokiem
dotnet run --project tools/shared-world-speedway -c Release --no-build -- slip-control-validation NEW_DIRECTORY measure
$env:DOTNET_TieredCompilation='0'
dotnet run --project tools/shared-world-speedway -c Release --no-build -- slip-control-validation NEW_DIRECTORY benchmark
dotnet test tests/CoreSim.Tests/CoreSim.Tests.csproj -c Release --filter "FullyQualifiedName~SlipControlValidationTests|FullyQualifiedName~SlipCoupledDynamicsTests|FullyQualifiedName~ReplayGeometryTests"
python tools/shared-world-speedway/slip-control-report.py
```

Eksporter odmawia nadpisania istniejących dowodów. Raportujący Python tylko odczytuje dane #98, weryfikuje historyczne hashe i generuje ten raport/manifest nowego katalogu. Powtórzenie eksportu po zmniejszeniu telemetrii i poprawieniu metadanych zachowało zamrożony plan, fingerprinty i pełne agregaty; nie wybrano ponownie czasów pod wynik. Dla pary cost-probe: archiwum źródeł #97 (src/CoreSim + tools/shared-world-speedway), identyczny probe oraz mały dispatch Program.cs; druga kopia różni się wyłącznie czterema zmienionymi plikami instrumentacji #98. W obu uruchomiono `ValidationCostProbe.Run(destination)` przy tej samej konfiguracji runtime. Te kopie nie wchodzą do PR.

## F. Jedna następna decyzja

**1 — można przejść do osobnej, ograniczonej kalibracji obecnego modelu**, pozostawiając flagę OFF i obecne domyślne parametry do jej zakończenia. Następny PR: pomiary referencyjne oraz kalibracja odpowiedzi na zadany offset i preview przy ustalonym budżecie opony, z jednoczesną kontrolą prędkości, prześwitu, kąta i strat energii. Celem jest redukcja udokumentowanego opóźnienia wykonania, a nie wygranie czasu okrążenia. Wymagane odniesienie empiryczne należy pozyskać przed deklaracją realizmu.

Nie ma pomiarowej podstawy, by najpierw naprawiać guard, który tu niczego nie obciął, ani by automatycznie budować rear-wheel state. Gdy przyszły konkretny cel będzie wymagał zmiany Fy samym gazem przy niezmienionym stanie mechanicznym, obecna algebra nie potrafi tego wyrazić i kalibracja jej nie zastąpi. Ten PR rozdziela tę granicę od już działającej, pośredniej odpowiedzi na gaz. Ocena A dotyczy kompletności i wiarygodności tej diagnozy; nie uzasadnia integracji starego AI/scoringu ani włączenia produkcyjnego.
"""
    (ROOT / "docs/experiments/slip-control-validation.md").write_text(text, encoding="utf-8", newline="\n")
    manifest = {str(f.relative_to(DATA)).replace("\\", "/"): hashlib.sha256(f.read_bytes()).hexdigest()
                for f in sorted(DATA.rglob("*")) if f.is_file() and f.name != "manifest.json"}
    (DATA / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", newline="\n")
    print(f"Report generated; {len(manifest)} evidence files sealed.")


if __name__ == "__main__":
    generate()
