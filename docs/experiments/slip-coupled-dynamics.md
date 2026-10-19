# Slip-Coupled Speedway Dynamics (#97)

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

`r=clamp(m*a_req/B, -0.95, 0.95)`, `β_target=atan(delta/max(|v|,v0) * r/sqrt(1−r²))`. To przybliżona odwrotność krzywej, a nie wykonana orientacja ani siła. Aktualny uślizg pozostaje wejściem opony. Błąd yaw to najkrótsza różnica β_target−α. Przy 0,01–0,5 m/s kierunek sterowania przechodzi smoothstep z kierunku drogi do tej różnicy; poniżej 0,01 m/s używa drogi, bez skoku przy progu definicji SlideRadians.

Wewnętrzny czas yaw `SlipYawResponseSeconds=0,1 s`; zewnętrzny czas `T=LateralResponseSeconds+4*SlipYawResponseSeconds=1,05 s`. Krytycznie tłumiony regulator yaw otrzymuje feed-forward z rzeczywistej reakcji bocznej `cross(v,Fy*el)/(m*(|v|²+v0²))`. Napęd jest z tego feed-forward wyłączony, aby przy ustalonym stanie zmiana gazu nie zmieniała momentu. Moment jest ograniczony do `±0,5 m*B`, ze starą authority Technique i impairment. Nie ustawia heading bezpośrednio.

Efektywny regulator yaw nie jest darmowym źródłem energii. Ograniczenie uwzględnia **cały przyrost energii przy sile i momencie utrzymanych przez krok**, a nie tylko chwilowe τω. Niech `H=0,02 s`, `F0=Fy*el+F_res`, `D=-F0·v`, `C=max(0,D-H*|F0|²/(2m))`. Moment zostaje ograniczony do przedziału spełniającego `τω+H*τ²/(2I)<=C`, czyli `τ ∈ (I/H)*[-ω-sqrt(ω²+2CH/I), -ω+sqrt(ω²+2CH/I)]`. Obejmuje to też hamujący moment, który inaczej mógłby przestrzelić zero obrotu. Przy dowolnym rzeczywistym kroku h≤H i Drive=0 suma translacyjnego i obrotowego przyrostu energii nie jest dodatnia; regularizacja Trelax zapewnia nieujemny kredyt dyssypacji w sprawdzonym zakresie. Nie przypisujemy prędkości po kroku. To ograniczony efektywny aktuator, nie model mechaniki kierownicy, żyroskopu ani rozdziału sił między osiami. Dla jazdy do przodu zmniejszenie Drive nie zwiększa chwilowej mocy mechanicznej; ograniczenie yaw jest od napędu niezależne. Test sprawdza 20 ms nierówność pracy na siatce prędkości, orientacji i spinów, a także rzeczywiste energie po krokach Box2D.

## Rzeczywiste eksperymenty OFF/ON

Jedna Motoarena, masa 70+77=147 kg, neutralny setup, stałe profile 50, ta sama geometria i nawierzchnia Grip=1/Ruts=0/Moisture=0,5 (EffectiveGrip=0,9175). Krok 0,01 s, 4 substeps, jeden wspólny świat. A: 1 s przy 18 m/s, offset 5 m, prosta od ref0 lub łuk od ref65. B: stałe cele 2/5/8 m, start ref0 przy 18 m/s, **dwa indywidualne obiegi**. C: ta sama środkowa linia, Grip=0,85 (EffectiveGrip=0,7675) albo stały Drive=0,65 od t=0. D: 10 s, czterech takich samych zawodników na liniach 2/4/6/8 m, ref0/4/8/12, 18 m/s, bez taktyki. Krótki D nie jest pełnym biegiem czterookrążeniowym.

### A — siły i energia

K = energia translacji **plus obrotu** z rzeczywistych mas i bezwładności Box2D. Praca używa przyłożonego F i τ oraz średnich prędkości dwóch zaakceptowanych końców danego przedziału; bez reintegracji. Pozostałość ΔK−W wskazuje błąd numeryczny, nie ukryty opór.

| Próba | Tryb | Końcowa prędkość m/s | Śr./max abs(α) ° | ΔK J | W J | ΔK−W J |
|---|---|---:|---:|---:|---:|---:|
| A-straight | OFF | 19.9348 | 0.000/0.000 | 5394.711 | 5394.811 | -0.100 |
| A-straight | ON | 19.9352 | 0.000/0.000 | 5395.696 | 5395.540 | 0.156 |
| A-arc | OFF | 18.0215 | 6.144/8.209 | 63.205 | 63.146 | 0.060 |
| A-arc | ON | 18.4667 | 6.500/9.087 | 1257.519 | 1257.514 | 0.005 |

### B — trzy wykonalne narzucone linie

Prędkości Entry/min/Exit oraz max kolejnej prostej dotyczą pierwszego pełnego łuku i następującej prostej; są to zaakceptowane próbki przy granicach, nie sztucznie wyliczone stany. Kąty, czas ≥95% budżetu i dystans obejmują całe dwa obiegi. Okrążenie 2 jest lotne. Cele są stałe, ale fizyczny offset reaguje z opóźnieniem — nie narzucamy idealnej trajektorii.

| Linia / tryb | Okr.1 / 2 s | Entry / min / Exit m/s | Max prostej m/s | Śr./max abs(α) ° | ≥95% s | Droga m | Max błąd offsetu m | Min prześwit m |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| B-inner OFF | 15.210 / 14.873 | 21.286/21.286/21.903 | 23.769 | 9.146/14.630 | 0.000 | 662.643 | 0.022 | 2.596 |
| B-inner ON | 16.075 / 15.716 | 21.203/19.272/19.704 | 23.903 | 6.904/18.894 | 2.670 | 656.211 | 1.159 | 1.254 |
| B-middle OFF | 15.672 / 15.089 | 21.286/21.286/22.875 | 24.597 | 8.545/13.506 | 0.000 | 700.405 | 0.022 | 5.480 |
| B-middle ON | 16.556 / 16.094 | 21.214/19.990/20.386 | 24.313 | 6.612/18.896 | 1.570 | 692.969 | 1.173 | 4.241 |
| B-outer OFF | 16.175 / 15.310 | 21.286/21.286/23.601 | 25.319 | 7.960/12.517 | 0.000 | 738.000 | 0.022 | 2.490 |
| B-outer ON | 17.060 / 16.499 | 21.223/20.639/21.005 | 24.690 | 6.336/18.725 | 0.630 | 730.689 | 1.176 | 2.174 |

OFF nie ma nowego pomiaru wykorzystania opony; jego kolumna ≥95% jest nieobsługiwana (wartość 0), a nie dowodem braku saturacji starego regulatora. ON jest wolniejszy: płaci rzeczywistą pracą bocznej siły i nie otrzymuje dawnego neutral-carry bez oporu. Zewnętrzna pozwala na wyższe prędkości, ale ma dłuższą drogę i nie wygrywa czasu. Mniejszy średni kąt na zewnętrznej nie daje premii — wynik wynika z pracy sił i geometrii.

### C — nawierzchnia i gaz

| Próba / tryb | Okr.2 s | Entry / min / Exit m/s | Śr./max abs(α) ° | ≥95% s | Droga m | Min prześwit m |
|---|---:|---:|---:|---:|---:|---:|
| B-middle OFF | 15.089 | 21.286/21.286/22.875 | 8.545/13.506 | 0.000 | 700.405 | 5.480 |
| B-middle ON | 16.094 | 21.214/19.990/20.386 | 6.612/18.896 | 1.570 | 692.969 | 4.241 |
| C-grip OFF | 15.567 | 21.168/21.168/22.163 | 8.130/12.718 | 0.000 | 700.268 | 5.486 |
| C-grip ON | 16.505 | 21.090/19.521/19.918 | 6.925/18.883 | 2.440 | 693.747 | 4.272 |
| C-throttle OFF | 15.173 | 19.837/19.837/21.130 | 8.179/13.506 | 0.000 | 700.308 | 5.481 |
| C-throttle ON | 17.708 | 19.782/18.325/18.508 | 4.467/9.711 | 0.000 | 695.705 | 4.692 |

Słabszy grip ON zmniejsza budżet i wydłuża czas blisko jego granicy; Drive=0,65 obniża przyspieszanie i uślizg, a przejazd trwa dłużej. To kontrolowane zmiany istniejącego TrackSurfaceState i ControlCommand. Brak sztucznej straty proporcjonalnej do kąta.

### D — wspólny świat, determinizm i koszt

Oba 10-sekundowe przejazdy: zero kontaktów i upadków, wszystkie fizyczne obrysy na torze. Nie stroiliśmy pod zwycięzcę lub liczbę minięć. Kontakty aktywnego solvera są osobno sprawdzone celowanym testem z realnym zamykaniem kapsuł. Test odwróconej kolekcji z włączoną diagnostyką zachowuje dokładny fingerprint, kontakty i liczbę wywołań; pięć powtórzeń performance ma ten sam fingerprint co zapis D.

- D OFF: 1013 wywołań, min prześwit 2.500 m; fingerprint `74D167A193F0E7FED2313DC09EE4469EB099029DF8E4AB69A88D69D0A5F2D5F5`.
- D ON: 1029 wywołań, min prześwit 1.461 m; fingerprint `892601B3CB5298994E0821428FC59F03401E83971154DA66F7F8721744B97E67`.

Pomiar Release, .NET 8.0.26, Windows, wyłączony tiered JIT (`DOTNET_TieredCompilation=0`), 10k rozgrzewki sił i pięć partii po 100k. Mediany **OFF 0.2864 µs / ON 0.4867 µs**, **+70.0%** dla samego Forces; **0 B** alokacji w każdej partii. Cały porównywalny D bez obserwatorów (w tym konstrukcja i Run): **21.942 → 22.614 ms**, **+3.06%**. Różna liczba podziałów przy granicach krzywizny wynika z wykonanych trajektorii, nie ze solvera kandydatów. Koszt całego biegu mieści się poniżej celu 10%; **samo Forces przekracza ten cel względny**, choć różnica bezwzględna to około 200 ns. To znane ograniczenie wydajności, nie ukryte spełnienie obu celów.

Pierwszy pomiar ujawnił 88 B/call w alokującej lambdzie TrackFrame.At używanej do preview. Zastąpiono ten odczyt RoadAt i potwierdzono zgodność z At na całym torze. [Surowy wcześniejszy pomiar](slip-coupled-dynamics/performance-before-allocation-fix.json) ma rozgrzewający się tiered JIT i nie służy do porównania końcowych median; [końcowy pomiar](slip-coupled-dynamics/performance.json) ma jawną konfigurację runtime.


## Negatywne próby i ograniczenia

- [Attempt 1](slip-coupled-dynamics/attempt-1/README.md): T_lateral=0,65 s / T_yaw=0,35 s. B-middle nie ukończył dwóch obiegów w 60 s, min prześwit −17,055 m. Pętla offsetu była za szybka względem nowego pośredniego aktuatora orientacji.
- [Attempt 2](slip-coupled-dynamics/attempt-2/README.md): T_lateral=2,05 s / T_yaw=0,35 s. Ukończył, lecz opuścił tor, min prześwit −7,774 m. Samo spowolnienie zewnętrznej pętli nie wystarczyło. Obie próby i ich pełne zapisy są zachowane; nie zaliczono ich jako stabilnych.
- [Odrzucone warianty małej prędkości](slip-coupled-dynamics/low-speed-rejected.json): ciągła nierówność mocy nie wystarczyła przy sile utrzymywanej przez cały krok. Przy u=0,01 m/s, dryfie 0,05 m/s i Drive=0 pierwsze wykryte przyrosty energii wyniosły 0,013428 J przy 10 ms i 1,208759 J przy 20 ms. Dodano regularizację Trelax w tej samej krzywej Fy i jej odwrotności regulatora. Następnie test pełnej pracy wykrył także przestrzelenie momentu hamującego przy małym spinie; zastąpiono warunek chwilowej mocy powyższym przedziałem kwadratowym dla całego kroku. [Końcowe zaakceptowane stany oraz energie dla 5/10/20 ms](slip-coupled-dynamics/low-speed-energy.json) i test siatki stanów pokazują brak wzrostu energii, również z obrotem. Nie poprawiano prędkości po kroku.
- Końcowa korekta dotyczy pasma regulatora, nie krzywej sił, gripu czy pozycji: szybszy, nadal ograniczony yaw 0,1 s i zewnętrzny T=1,05 s. Domyślne ustawienia sprawdzono przy 5/10/20 ms, przy starcie od 0 i 0,1 m/s oraz w 35 m/s przeciążeniu. Przy niewykonalnej prędkości rzeczywisty motocykl odchodzi od targetu zamiast idealnie utrzymywać promień. Nie gwarantujemy stabilności dowolnych innych nastaw regulatora ani ekstremalnie gęstej walki.
- Model bez stanu obrotów koła nie wyznacza slip ratio, wheelspin, sprzęgła ani strat mocy w napędzie. Fx oznacza dostępny efektywny napęd według istniejącej charakterystyki; yaw ma jawnie prowizoryczne ograniczenie pracy. Brak oddzielnej opony przedniej/tylnej, przenoszenia obciążenia, przechyłu i żyroskopu. Nasycenie jest monotoniczne, bez empirycznej gałęzi spadku bocznej siły po jej maksimum. Duży uślizg szkodzi przez pracę i wspólny budżet, nie arbitralny peak-angle.
- Nawierzchnia jest niezmiennym snapshotem. Kontakty i proxy ich konsekwencji pozostają dotychczasowe; nie dodano krawędzi toru ani losowego upadku. Taktyczny/autonomiczny scoring jest jawnie niewspierany w ON.

## Diagnostyka, dowody i walidacja

WorldDiagnostics.Slip jest opcjonalnym obserwatorem aktywnej ścieżki sił ON, bez wpływu na stan. Zapisuje czas/ID, rzeczywistą i zadaną różnicę kątową, heading/kierunek ruchu, prędkość, Fx/Fy, budżet i utilization, Drive, offset/target, moment, stratę boczną, stratę oporów, moc translacji i moc yaw. Siły odnoszą się do zaakceptowanego stanu **przed** przyłożeniem ich w danym przedziale. CSV zawiera zaakceptowane końce kroków, zgodnie z HeatMeasurements #95–#96, około co 0,1 s; nie należy utożsamiać ich znaczników z końcami działania siły. Stan t=0 jest w Run.Initial.

JSON zachowuje kopertę Run.Initial/Run.Result, decyzje, kontakty, konsekwencje, CrossingSeconds oraz osobną tablicę Slip. Wspólny budżet dotyczy opony: legacy ForceObservation ON ma GripBudget=null, aby zewnętrznego oporu nie raportować jako przekroczenia kręgu. Źródłem utilization jest nowe Slip, nie historyczne metryki zakładające cel 12°. Nie rozszerzano interfejsu replay viewer.

**22/22 celowanych Release: 20 nowych przypadków + 2 testy eksportu geometrii.** Dokładna zgodność OFF z bazą sprzed zmiany (cały fingerprint i każda ForceObservation), jeden niezmieniony replay autonomiczny #95 (fingerprint i komendy), kierunki lewo/prawo, zero/mały/nasycony uślizg, krąg, ±π, power inequality, rzeczywista energia przy Drive=0 przez 8 s, kroki czasu i start, deterministyczna kolejność i niewpływająca diagnostyka, prawdziwy kontakt, przeciążenie, walidacja konfiguracji, brak alokacji i brak podmiany α na β_target. Restore i Release --warnaserror: powodzenie. Standardowe Draft Windows CI służy do końcowej regresji; status/link jest w opisie PR.

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
