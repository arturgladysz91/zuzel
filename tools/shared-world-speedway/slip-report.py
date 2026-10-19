"""Read only the new #97 captures; never runs a simulation or rewrites old evidence."""
import hashlib
import json
from pathlib import Path
from statistics import median

root = Path(__file__).resolve().parents[2]
folder = root / 'docs/experiments/slip-coupled-dynamics'
read = lambda name: json.loads((folder / name).read_text(encoding='utf-8-sig'))
baseline_hash = hashlib.sha256((folder / 'baseline.json').read_bytes()).hexdigest()
assert baseline_hash == '2f9942bd2b32ea9f362c6d4811f2a0dd15c84c44aa89f54adee8ca30c6d5aae7'
runs = {p.stem: read(p.name) for p in folder.glob('*-O*.json')}
baseline = read('baseline.json')
for row in baseline['Rows']:
    assert row['Result']['Fingerprint'] == runs[row['Name'] + '-OFF']['Run']['Result']['Fingerprint']
for item in runs.values():
    for sample in item['Slip']:
        assert sample['GripUtilization'] <= 1.000001
        assert sample['LateralLossWatts'] >= 0

performance = read('performance.json')
force_us = {x['On']: median(x['Milliseconds']) * 1000 / x['Calls'] for x in performance['Forces']}
world_ms = {on: median(x['Milliseconds'] for x in performance['Worlds'] if x['On'] == on) for on in (False, True)}
for on in (False, True):
    fp = {x['Fingerprint'] for x in performance['Worlds'] if x['On'] == on}
    assert fp == {runs['D-pack-' + ('ON' if on else 'OFF')]['Run']['Result']['Fingerprint']}
assert all(not any(x['AllocatedBytes']) for x in performance['Forces'])
for trial in read('low-speed-energy.json'):
    values = [x['KineticEnergyJoules'] for x in trial['States']]
    assert all(b <= a + 1e-5 for a, b in zip(values, values[1:]))
    assert trial['Result']['Work']['SolverCalls'] == round(.2 / trial['StepSeconds'])

report = [r'''# Slip-Coupled Speedway Dynamics (#97)

**PROVISIONAL — A w sprawdzonym zakresie eksperymentalnym. Domyślnie OFF.** ON sprzęga rzeczywisty uślizg z siłą opony, orientacją, napędem i energią. Trzy stałe linie ukończyły po dwa obiegi, także słabsza nawierzchnia i mniejszy gaz; krótki wspólny świat czterech motocykli pozostał stabilny. Nie jest to kalibracja realnej opony ani potwierdzenie współpracy z dotychczasowym scoringiem AI.

Baza: otwarty Draft #96, `experiment/diagnostic-replay-viewer`, HEAD `7ebf687fa78a4534e71f301310e62e37bef0188c`. Nowa gałąź `experiment/slip-coupled-dynamics`, target gałęzi #96. Bez merge. Produkcyjne `src/`, historyczne kontrakty, fingerprinty i dane #87–#96 bez zmian.

## Odpowiedzialności OFF i ON

| Mechanizm | OFF | ON |
|---|---|---|
| RiderController.Forces | Dotychczasowy kod po jednym dispatchu; bez refaktoru | Osobny SlipCoupledDynamics, ten sam WorldRunner/Box2D |
| LongitudinalDynamics | Bez zmian | Te same dostępne siły start/Straight/TurnExit, charakterystyka zaniku z prędkością, Gearing, powierzchnia, bazowe opory |
| SegmentPhysics | safe²/R + stary regulator | Wyłącznie safe²/R jako jeden budżet siły opony; bez clamp prędkości, outcome/RunWide/Crash i dodatkowego scrub |
| Stare przygotowanie/scrub oraz limit boczny | Bez zmian | Usunięte z tej ścieżki; zamiast wymuszania prędkości orientacja korzysta z geometrycznego podglądu łuku |
| ContinuousCornerEnvelope.DriveAvailability | Bez zmian | Bez fazowego odcinania napędu; napęd konkuruje z rzeczywistą reakcją boczną opony |
| Stary krąg wektora siły | Bez zmian | Zastąpiony jednym kręgiem Fx/Fy w osiach motocykla; opór zewnętrzny nie zużywa go ponownie |
| Cel road.Heading + 12° | Bez zmian | Cel względem kierunku ruchu, z odwrotności krzywej opony i zapotrzebowania regulatora |
| ContactAdapter i solver | Bez zmian | Bez zmian; te same kapsuły, masa, bezwładność, tarcie i zegar; bez dodatkowego świata lub solvera kandydatów |

ON odrzuca `EnableTacticalRacing=true`, a tym samym połączenia ze scoringiem ContestScoring/AutonomousRacingLine/SideBySide. Badania mają `TrafficEnabled=false`, stałe cele i jawne komendy gazu. W ON taki zawodnik nie otrzymuje dawnego automatycznego ResumeAcceleration po autorskiej komendzie Drive=0. Ścieżka OFF zachowuje także ten dawny warunek dokładnie. Nie dodano nowych decyzji taktycznych.

## Równania i znaki

Osie motocykla: `ef=(cosψ,sinψ)`, `el=(-sinψ,cosψ)`. `u=v·ef` to ForwardSpeed [m/s], `w=v·el` to SideSpeed [m/s]. `α=wrap(ψ−atan2(vy,vx))` jest rzeczywistym SlideRadians ze stanu zaakceptowanego; przy prędkości <0,01 m/s przyjmowany jest diagnostycznie 0, zgodnie z RiderView. Siła zależy od u/w i pozostaje ciągła także poniżej tego progu oraz przy przejściu ±π. Ruch wsteczny nie jest modelem osobnego biegu wstecznego.

`B = m * v_safe(R, surface, skills, setup)^2 / R` [N], ze starego SegmentPhysics, to **jeden** budżet przyczepności. Nie zwiększono jego wartości. `R=InnerRadius+max(0,actualOffset)`; capability safe²/R zachowuje istniejącą relację umiejętności/setupu/nawierzchni. To odziedziczona capability, nie nowy pomiar współczynnika μ realnego żużla.

```text
s0 = 0.12                          bezwymiarowa skala sztywności
v0 = 0.5 m/s                       regularizacja małej prędkości
Trelax = 0.03 s                    minimalny czas tłumienia prędkości bocznej
delta² = s0²*(u²+v0²) + (B*Trelax/m)²
Fy = -B*w / sqrt(w² + delta²)
Fx_request = availableDrive(max(0,u)) * Drive * (1-Impairment)
Fx = min(Fx_request, sqrt(max(0, B²-Fy²)))
F_res = -(40 + 0.20*|v|²) * v / sqrt(|v|²+v0²)
F_world = Fx*ef + Fy*el + F_res
P_slip_loss = -Fy*w >= 0
```

Fy ma pierwszeństwo w budżecie, a napęd otrzymuje pozostałą składową kręgu. Przy małym w reakcja jest w przybliżeniu liniowa; przy średnim narasta; przy dużym gładko dąży do ±B. Składnik Trelax ogranicza sztywność przy małym u: `|Fy|/m <= |w|/Trelax`. Czas 30 ms przekracza największy istniejący przedział przyłożenia siły 20 ms; regularizacja zapobiega numerycznemu przestrzeleniu tłumionej prędkości bocznej. To jedna gładka krzywa siły, bez edycji prędkości, drugiego limitu przyczepności lub nowego kroku solvera; nie dodaje osobnego stanu relaksacji opony. Nie ma sztucznego optimum 12° ani mnożnika prędkości zależnego od kąta. Przy nadmiernym uślizgu wzrastają strata `-Fy*w` i wykorzystanie budżetu, a dostępny Fx maleje. Nie modelujemy osobno koła przedniego i tylnego.

Przykład lewego łuku: `v=(20,0)`, nos `ψ=+0,1 rad` daje `w<0`, więc `Fy>0` w osi el, a światowa składowa Y jest dodatnia — do wnętrza lewego łuku. `Fy*w<0`, więc opona odbiera energię. Po odbiciu `ψ=-0,1` kierunki się odwracają. Stary znak +12° nie został użyty jako założenie nowego modelu.

### Orientacja i stabilizacja

Regulator liczy żądane przyspieszenie `a_req = v_tangent²*κ_preview + (offset−target)/T² + 2*v_outward/T`. Podgląd dotyczy wyłącznie istniejącej statycznej geometrii TrackFrame: około `2*YawResponseSeconds=0,7 s` aktualnego postępu referencyjnego. Krzywizna uwzględnia długość fizyczną obecnego offsetu; błąd i tłumienie prowadzą do wybranej linii. RoadAt odczytuje tę samą natywną geometrię co At, bez alokującej lambdy i bez osobnej trajektorii dynamicznej.

`r=clamp(m*a_req/B, -0.95, 0.95)`, `β_target=atan(delta/max(|v|,v0) * r/sqrt(1−r²))`. To przybliżona odwrotność krzywej, a nie wykonana orientacja ani siła. Aktualny uślizg pozostaje wejściem opony. Błąd yaw to najkrótsza różnica β_target−α.

Wewnętrzny czas yaw `SlipYawResponseSeconds=0,1 s`; zewnętrzny czas `T=LateralResponseSeconds+4*SlipYawResponseSeconds=1,05 s`. Krytycznie tłumiony regulator yaw otrzymuje feed-forward z rzeczywistej reakcji bocznej `cross(v,Fy*el)/(m*(|v|²+v0²))`. Napęd jest z tego feed-forward wyłączony, aby przy ustalonym stanie zmiana gazu nie zmieniała momentu. Moment jest ograniczony do `±0,5 m*B`, ze starą authority Technique i impairment. Nie ustawia heading bezpośrednio.

Efektywny regulator yaw nie jest darmowym źródłem energii. Gdy moment wykonuje dodatnią pracę, jego moduł jest dodatkowo ograniczony przez `D/sqrt(ω²+1 rad²/s²)`, gdzie `D=P_slip_loss−F_res·v`. Zatem `τ*ω<=D`, a `F·v+τ*ω<=Fx*u`. Przy Drive=0 całkowita moc mechaniczna nie jest dodatnia; przy spoczynku nie ma źródła energii na rozpędzenie obrotu. To ograniczony efektywny aktuator, nie model mechaniki kierownicy, żyroskopu ani rozdziału sił między osiami. Dla jazdy do przodu zmniejszenie Drive nie zwiększa chwilowej mocy mechanicznej. Test sprawdza również rzeczywistą energię translacyjną i obrotową po krokach Box2D.

## Rzeczywiste eksperymenty OFF/ON

Jedna Motoarena, masa 70+77=147 kg, neutralny setup, stałe profile 50, ta sama geometria i nawierzchnia Grip=1/Ruts=0/Moisture=0,5 (EffectiveGrip=0,9175). Krok 0,01 s, 4 substeps, jeden wspólny świat. A: 1 s przy 18 m/s, offset 5 m, prosta od ref0 lub łuk od ref65. B: stałe cele 2/5/8 m, start ref0 przy 18 m/s, **dwa indywidualne obiegi**. C: ta sama środkowa linia, Grip=0,85 (EffectiveGrip=0,7675) albo stały Drive=0,65 od t=0. D: 10 s, czterech takich samych zawodników na liniach 2/4/6/8 m, ref0/4/8/12, 18 m/s, bez taktyki. Krótki D nie jest pełnym biegiem czterookrążeniowym.

### A — siły i energia

K = energia translacji **plus obrotu** z rzeczywistych mas i bezwładności Box2D. Praca używa przyłożonego F i τ oraz średnich prędkości dwóch zaakceptowanych końców danego przedziału; bez reintegracji. Pozostałość ΔK−W wskazuje błąd numeryczny, nie ukryty opór.

| Próba | Tryb | Końcowa prędkość m/s | Śr./max |α| ° | ΔK J | W J | ΔK−W J |
|---|---|---:|---:|---:|---:|---:|''']

for name in ('A-straight', 'A-arc'):
    for mode in ('OFF', 'ON'):
        run = runs[f'{name}-{mode}']['Run']; m = run['Riders'][0]
        report.append(f"| {name} | {mode} | {run['Result']['Riders'][0]['Speed']:.4f} | {m['MeanAbsoluteSlipDegrees']:.3f}/{m['MaximumSlipDegrees']:.3f} | {m['FinalKineticEnergyJoules']-m['InitialKineticEnergyJoules']:.3f} | {m['AppliedWorkJoules']:.3f} | {m['WorkBalanceResidualJoules']:.3f} |")

report.append('''
### B — trzy wykonalne narzucone linie

Prędkości Entry/min/Exit oraz max kolejnej prostej dotyczą pierwszego pełnego łuku i następującej prostej; są to zaakceptowane próbki przy granicach, nie sztucznie wyliczone stany. Kąty, czas ≥95% budżetu i dystans obejmują całe dwa obiegi. Okrążenie 2 jest lotne. Cele są stałe, ale fizyczny offset reaguje z opóźnieniem — nie narzucamy idealnej trajektorii.

| Linia / tryb | Okr.1 / 2 s | Entry / min / Exit m/s | Max prostej m/s | Śr./max |α| ° | ≥95% s | Droga m | Max błąd offsetu m | Min prześwit m |
|---|---:|---:|---:|---:|---:|---:|---:|---:|''')
for name in ('B-inner', 'B-middle', 'B-outer'):
    for mode in ('OFF', 'ON'):
        m = runs[f'{name}-{mode}']['Run']['Riders'][0]
        report.append(f"| {name} {mode} | {' / '.join(f'{x:.3f}' for x in m['Measurement']['LapSeconds'])} | {m['EntryMetersPerSecond']:.3f}/{m['MinimumCornerMetersPerSecond']:.3f}/{m['ExitMetersPerSecond']:.3f} | {m['NextStraightMaximumMetersPerSecond']:.3f} | {m['MeanAbsoluteSlipDegrees']:.3f}/{m['MaximumSlipDegrees']:.3f} | {m['NearGripSeconds']:.3f} | {m['DistanceMeters']:.3f} | {m['MaximumOffsetErrorMeters']:.3f} | {m['MinimumClearanceMeters']:.3f} |")

report.append('''
OFF nie ma nowego pomiaru wykorzystania opony; jego kolumna ≥95% jest nieobsługiwana (wartość 0), a nie dowodem braku saturacji starego regulatora. ON jest wolniejszy: płaci rzeczywistą pracą bocznej siły i nie otrzymuje dawnego neutral-carry bez oporu. Zewnętrzna pozwala na wyższe prędkości, ale ma dłuższą drogę i nie wygrywa czasu. Mniejszy średni kąt na zewnętrznej nie daje premii — wynik wynika z pracy sił i geometrii.

### C — nawierzchnia i gaz

| Próba / tryb | Okr.2 s | Entry / min / Exit m/s | Śr./max |α| ° | ≥95% s | Droga m | Min prześwit m |
|---|---:|---:|---:|---:|---:|---:|''')
for name in ('B-middle', 'C-grip', 'C-throttle'):
    for mode in ('OFF', 'ON'):
        m = runs[f'{name}-{mode}']['Run']['Riders'][0]
        report.append(f"| {name} {mode} | {m['Measurement']['LapSeconds'][1]:.3f} | {m['EntryMetersPerSecond']:.3f}/{m['MinimumCornerMetersPerSecond']:.3f}/{m['ExitMetersPerSecond']:.3f} | {m['MeanAbsoluteSlipDegrees']:.3f}/{m['MaximumSlipDegrees']:.3f} | {m['NearGripSeconds']:.3f} | {m['DistanceMeters']:.3f} | {m['MinimumClearanceMeters']:.3f} |")

report.append('''
Słabszy grip ON zmniejsza budżet i wydłuża czas blisko jego granicy; Drive=0,65 obniża przyspieszanie i uślizg, a przejazd trwa dłużej. To kontrolowane zmiany istniejącego TrackSurfaceState i ControlCommand. Brak sztucznej straty proporcjonalnej do kąta.

### D — wspólny świat, determinizm i koszt

Oba 10-sekundowe przejazdy: zero kontaktów i upadków, wszystkie fizyczne obrysy na torze. Nie stroiliśmy pod zwycięzcę lub liczbę minięć. Kontakty aktywnego solvera są osobno sprawdzone celowanym testem z realnym zamykaniem kapsuł. Test odwróconej kolekcji z włączoną diagnostyką zachowuje dokładny fingerprint, kontakty i liczbę wywołań; pięć powtórzeń performance ma ten sam fingerprint co zapis D.
''')
for mode in ('OFF', 'ON'):
    run = runs['D-pack-' + mode]['Run']
    report.append(f"- D {mode}: {run['Result']['Work']['SolverCalls']} wywołań, min prześwit {min(x['MinimumClearanceMeters'] for x in run['Riders']):.3f} m; fingerprint `{run['Result']['Fingerprint']}`.")

report.append(f'''
Pomiar Release, {performance['Runtime']}, Windows, wyłączony tiered JIT (`DOTNET_TieredCompilation=0`), 10k rozgrzewki sił i pięć partii po 100k. Mediany **OFF {force_us[False]:.4f} µs / ON {force_us[True]:.4f} µs**, **+{(force_us[True]/force_us[False]-1)*100:.1f}%** dla samego Forces; **0 B** alokacji w każdej partii. Cały porównywalny D bez obserwatorów (w tym konstrukcja i Run): **{world_ms[False]:.3f} → {world_ms[True]:.3f} ms**, **+{(world_ms[True]/world_ms[False]-1)*100:.2f}%**. Różna liczba podziałów przy granicach krzywizny wynika z wykonanych trajektorii, nie ze solvera kandydatów. Koszt całego biegu mieści się poniżej celu 10%; **samo Forces przekracza ten cel względny**, choć różnica bezwzględna to około {(force_us[True]-force_us[False])*1000:.0f} ns. To znane ograniczenie wydajności, nie ukryte spełnienie obu celów.

Pierwszy pomiar ujawnił 88 B/call w alokującej lambdzie TrackFrame.At używanej do preview. Zastąpiono ten odczyt RoadAt i potwierdzono zgodność z At na całym torze. [Surowy wcześniejszy pomiar](slip-coupled-dynamics/performance-before-allocation-fix.json) ma rozgrzewający się tiered JIT i nie służy do porównania końcowych median; [końcowy pomiar](slip-coupled-dynamics/performance.json) ma jawną konfigurację runtime.
''')

report.append('''
## Negatywne próby i ograniczenia

- [Attempt 1](slip-coupled-dynamics/attempt-1/README.md): T_lateral=0,65 s / T_yaw=0,35 s. B-middle nie ukończył dwóch obiegów w 60 s, min prześwit −17,055 m. Pętla offsetu była za szybka względem nowego pośredniego aktuatora orientacji.
- [Attempt 2](slip-coupled-dynamics/attempt-2/README.md): T_lateral=2,05 s / T_yaw=0,35 s. Ukończył, lecz opuścił tor, min prześwit −7,774 m. Samo spowolnienie zewnętrznej pętli nie wystarczyło. Obie próby i ich pełne zapisy są zachowane; nie zaliczono ich jako stabilnych.
- [Odrzucona sztywność przy małej prędkości](slip-coupled-dynamics/low-speed-rejected.json): ciągła nierówność mocy nie wystarczyła przy sile utrzymywanej przez cały krok. Przy u=0,01 m/s, dryfie 0,05 m/s i Drive=0 pierwsze wykryte przyrosty energii wyniosły 0,013428 J przy 10 ms i 1,208759 J przy 20 ms. Dodano regularizację Trelax w tej samej krzywej Fy i jej odwrotności regulatora. [Końcowe zaakceptowane stany oraz energie dla 5/10/20 ms](slip-coupled-dynamics/low-speed-energy.json) pokazują brak wzrostu energii, również z obrotem. Nie poprawiano prędkości po kroku.
- Końcowa korekta dotyczy pasma regulatora, nie krzywej sił, gripu czy pozycji: szybszy, nadal ograniczony yaw 0,1 s i zewnętrzny T=1,05 s. Domyślne ustawienia sprawdzono przy 5/10/20 ms, przy starcie od 0 i 0,1 m/s oraz w 35 m/s przeciążeniu. Przy niewykonalnej prędkości rzeczywisty motocykl odchodzi od targetu zamiast idealnie utrzymywać promień. Nie gwarantujemy stabilności dowolnych innych nastaw regulatora ani ekstremalnie gęstej walki.
- Model bez stanu obrotów koła nie wyznacza slip ratio, wheelspin, sprzęgła ani strat mocy w napędzie. Fx oznacza dostępny efektywny napęd według istniejącej charakterystyki; yaw ma jawnie prowizoryczne ograniczenie pracy. Brak oddzielnej opony przedniej/tylnej, przenoszenia obciążenia, przechyłu i żyroskopu. Nasycenie jest monotoniczne, bez empirycznej gałęzi spadku bocznej siły po jej maksimum. Duży uślizg szkodzi przez pracę i wspólny budżet, nie arbitralny peak-angle.
- Nawierzchnia jest niezmiennym snapshotem. Kontakty i proxy ich konsekwencji pozostają dotychczasowe; nie dodano krawędzi toru ani losowego upadku. Taktyczny/autonomiczny scoring jest jawnie niewspierany w ON.

## Diagnostyka, dowody i walidacja

WorldDiagnostics.Slip jest opcjonalnym obserwatorem aktywnej ścieżki sił ON, bez wpływu na stan. Zapisuje czas/ID, rzeczywistą i zadaną różnicę kątową, heading/kierunek ruchu, prędkość, Fx/Fy, budżet i utilization, Drive, offset/target, moment, stratę boczną, stratę oporów, moc translacji i moc yaw. Siły odnoszą się do zaakceptowanego stanu **przed** przyłożeniem ich w danym przedziale. CSV zawiera zaakceptowane końce kroków, zgodnie z HeatMeasurements #95–#96, około co 0,1 s; nie należy utożsamiać ich znaczników z końcami działania siły. Stan t=0 jest w Run.Initial.

JSON zachowuje kopertę Run.Initial/Run.Result, decyzje, kontakty, konsekwencje, CrossingSeconds oraz osobną tablicę Slip. Wspólny budżet dotyczy opony: legacy ForceObservation ON ma GripBudget=null, aby zewnętrznego oporu nie raportować jako przekroczenia kręgu. Źródłem utilization jest nowe Slip, nie historyczne metryki zakładające cel 12°. Nie rozszerzano interfejsu replay viewer.

**18/18 celowanych Release: 16 nowych przypadków + 2 testy eksportu geometrii.** Dokładna zgodność OFF z bazą sprzed zmiany (cały fingerprint i każda ForceObservation), jeden niezmieniony replay autonomiczny #95 (fingerprint i komendy), kierunki lewo/prawo, zero/mały/nasycony uślizg, krąg, ±π, power inequality, rzeczywista energia przy Drive=0 przez 8 s, kroki czasu i start, deterministyczna kolejność i niewpływająca diagnostyka, prawdziwy kontakt, przeciążenie, walidacja konfiguracji, brak alokacji i brak podmiany α na β_target. Restore i Release --warnaserror: powodzenie. Standardowe Draft Windows CI służy do końcowej regresji; status/link jest w opisie PR.

Nie powtarzano całych historycznych audytów #87–#96. Przed modyfikacją Forces zamrożono baseline.json, SHA-256 `2f9942bd2b32ea9f362c6d4811f2a0dd15c84c44aa89f54adee8ca30c6d5aae7`; jest niezmienny. Manifest katalogu obejmuje nowe dowody, także dwie próby negatywne. Skrypt raportujący weryfikuje ten hash i odczytuje wyniki, nie uruchamia symulatora.

```powershell
dotnet run --project tools/shared-world-speedway -c Release -- slip-dynamics NEW_DIRECTORY baseline
dotnet run --project tools/shared-world-speedway -c Release -- slip-dynamics NEW_DIRECTORY focus
dotnet run --project tools/shared-world-speedway -c Release -- slip-dynamics NEW_DIRECTORY runs
dotnet run --project tools/shared-world-speedway -c Release -- slip-dynamics NEW_DIRECTORY low-speed
$env:DOTNET_TieredCompilation='0'
dotnet run --project tools/shared-world-speedway -c Release -- slip-dynamics NEW_DIRECTORY benchmark
dotnet test tests/CoreSim.Tests/CoreSim.Tests.csproj -c Release --filter "FullyQualifiedName~SlipCoupledDynamicsTests|FullyQualifiedName~ReplayGeometryTests"
python tools/shared-world-speedway/slip-report.py
```

Nie uruchamiaj eksportu do historycznych katalogów. Reprodukcja baseline wymaga źródłowej bazy #96; końcowe OFF jest z nią sprawdzane, a istniejący plik nie jest nadpisywany. A w tym etapie oznacza spójność i działanie na narzuconych liniach; nie uzasadnia włączenia produkcyjnego. Rekomendacja: zachować flagę OFF, osobno skalibrować siły/sterowanie i dopiero potem porównać analityczny scoring z nowym wykonaniem przed integracją AI.
''')

text='\n'.join(report).replace('|α|', 'abs(α)').replace('18/18 celowanych Release: 16 nowych', '22/22 celowanych Release: 20 nowych')
text=text.replace('Błąd yaw to najkrótsza różnica β_target−α.', 'Błąd yaw to najkrótsza różnica β_target−α. Przy 0,01–0,5 m/s kierunek sterowania przechodzi smoothstep z kierunku drogi do tej różnicy; poniżej 0,01 m/s używa drogi, bez skoku przy progu definicji SlideRadians.')
(root / 'docs/experiments/slip-coupled-dynamics.md').write_text(text.rstrip() + '\n', encoding='utf-8')
manifest = {str(p.relative_to(folder)).replace('\\', '/'): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(folder.rglob('*')) if p.is_file() and p.name != 'manifest.json'}
(folder / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(f'Read {len(runs)} captures; baseline sealed; manifest {len(manifest)} files; report written.')
