# Slip dynamics — walidacja sterowania i podstawy kalibracji (#98)

**Ocena A: kompletna diagnoza tego zakresu. PROVISIONAL, bez potwierdzenia realizmu empirycznego. Rekomendacja 1: osobna kalibracja obecnego modelu.** Gaz zmienia późniejszy stan, lecz nie steruje niezależnym wheelspinem. Limiter energii pozostaje nieaktywny w zmierzonych przejazdach; nie odpowiada za ich opóźnienie orientacji. Wszystkie cztery strategie oraz czterookrążeniowy wspólny świat pozostają na torze. Zacieśnienie ma zauważalne opóźnienie wykonania.

Baza: otwarty Draft [#97](https://github.com/arturgladysz91/zuzel/pull/97), `experiment/slip-coupled-dynamics`, HEAD `c0de934c8992cb902fbea8da24366482d5b7ae54`. Gałąź `experiment/slip-dynamics-validation`, target gałęzi #97. Bez merge. Równania, parametry domyślne, `src/`, ContactAdapter i viewer bez zmian. `EnableSlipCoupledDynamics=false` domyślnie. Wyniki dotyczą ON z całkowicie wyłączonym starym scoringiem oraz `TrafficEnabled=false`.

## Metoda i zakres dowodów

Jedna Motoarena, natywna geometria TrackFrame, lap reference 318.778750 m, fizyczne szerokości 12/16,6 m. Każdy neutralny profil ma wszystkie stare Skills i osiem canonical Abilities =50, masę człowieka 70 kg, motocykl 77 kg, setup Neutral; actual Box2D m=147 kg, I=52.72097 kg·m². Zamrożona domyślna nawierzchnia: Grip=1, Ruts=0, Moisture=0,5. Krok bazowy 10 ms, cztery wewnętrzne substeps; istniejące deadline/curvature/proximity splits pozostają aktywne. Każdy przejazd ma jeden rzeczywisty świat Box2D.

[Konfiguracja](slip-control-validation/configuration.json) zachowuje także canonical profil, którego standardowy serializer RiderProfile nie eksportuje. Poszczególne JSON zawierają pełne początkowe pozycje i komendy. Wszystkie metryki całkowe, maksima i clearance liczone są **na każdym zaakceptowanym przedziale**, z wagą jego faktycznego dt. JSON telemetrii zachowuje jedną próbkę pre-step co 0,1 s w krótkich próbach / 0,5 s w długich; CSV około co 0,1 s i po zmianach sterowania. Próbka siły opisuje zaakceptowany stan przed krokiem, CSV jego zaakceptowane końce; nie są zamienne. A2 ma dodatkowo dokładne okna. Wartości czasu nie są arbitralnymi kryteriami zaliczenia testów.

| Eksperyment | Przebiegi i warunek | Wynik |
|---|---|---|
| A1 | Jeden zaakceptowany stan łuku, Drive 0,40/0,70/1,00 | Fy i yaw identyczne; Fx oraz moc różne |
| A2 | 3 pełne prefiksy, zmiana w 2,82 s; +0,2/+0,5/+1,0 s | Prefiksy bitowo równe, późniejsze stany różne |
| B1/B2 | 3 czasy yaw; wejście 5 s + 2 indywidualne obiegi | 0% limitu energii; wszystkie na torze |
| C | Stałe 2/5/8 m + zamrożone zacieśnienie, po 2 obiegi | 4/4 ukończone; manewr fizyczny z opóźnieniem |
| D1 | 4 neutralnych zawodników × 4 indywidualne obiegi | 4/4 ukończyło, bez kontaktu i wyjazdu |
| D2 | 2 aktywne ciała, start bez przenikania, 2 s | Rzeczywisty kontakt i dodatnie impulsy |
| Energia | Drive=0, 8 s; dodatkowo testy małych prędkości/kroku | Brak wzrostu zaakceptowanej energii |
| Opona | 18 punktów: 3 u × 6 kątów przy stałych B i m | Monotoniczne Fy, malejąca rezerwa Fx |

## A. Gaz: efekt bezpośredni i pośredni

Moment aktywacji wybrano **przed porównaniami**, z pierwszego zaakceptowanego stanu kontrolnego środkowej linii przy CornerProgress≥0,20, zaokrąglając czas w górę do 10 ms: **2.82 s**. To rzeczywisty łuk: ref=54.503075 m, offset=4.319810 m, u=20.599548, w=-4.073242 m/s. A1 ocenia ten zaakceptowany stan bez jego reintegracji; A2 odtwarza całe przebiegi od początku. [Stan, siły i sparowane okna](slip-control-validation/A-gas-comparison.json).

| Drive | Fx N | Fy N | α ° | B N | Usage | P napędu W | P strat bocznych W |
|---|---|---|---|---|---|---|---|
| 0.40 | 132.448 | 1820.246 | 11.185 | 2138.298 | 0.853510 | 2728.363 | 7414.302 |
| 0.70 | 231.784 | 1820.246 | 11.185 | 2138.298 | 0.858133 | 4774.636 | 7414.302 |
| 1.00 | 331.119 | 1820.246 | 11.185 | 2138.298 | 0.865229 | 6820.908 | 7414.302 |

**Bezpośrednio:** Fy=−B·w/sqrt(w²+δ²) nie zawiera Drive. Zmiana gazu nie odbiera Fy części budżetu: to Fx otrzymuje pozostałą rezerwę sqrt(B²−Fy²). Przy tym stanie nie ma jeszcze saturacji Fx. Zadany kąt i yaw też pozostają takie same (moment -15.198416 N·m). Model nie potrafi przy tym samym stanie zmniejszyć bocznej reakcji przez sam wzrost napędu.

**Dynamicznie:** siła napędowa zmienia u/w, prędkość, orientację i następne żądanie regulatora. Wszystkie próby mają identyczne zaakceptowane stany aż do aktywacji, włącznie z końcem poprzedniego kroku; SHA-256 pełnego prefiksu `97F2ACA933FF81A19520407A348081D9B41C793563DA95824859EF884E878F09`. Nie odtwarzano ręcznie żadnego snapshotu. Pierwsze wywołanie sił przy t=2,82 s ma już nowy Drive, lecz jeszcze nie zmieniony stan mechaniczny.

| Δt s | Drive | α ° | Fx/Fy N | Speed m/s | Offset m | Yaw rad/s | Usage | Δref m |
|---|---|---|---|---|---|---|---|---|
| 0.2 | 0.40 | 10.573 | 133.8/1790.0 | 20.543 | 4.370 | 0.504 | 0.8395 | 3.642 |
| 0.5 | 0.40 | 8.913 | 135.3/1687.4 | 19.971 | 4.427 | 0.487 | 0.7917 | 8.960 |
| 1.0 | 0.40 | 7.171 | 137.3/1533.9 | 19.293 | 4.482 | 0.505 | 0.7202 | 17.531 |
| 0.2 | 0.70 | 10.617 | 233.4/1792.4 | 20.675 | 4.369 | 0.522 | 0.8453 | 3.654 |
| 0.5 | 0.70 | 9.300 | 235.2/1714.7 | 20.283 | 4.426 | 0.512 | 0.8094 | 9.031 |
| 1.0 | 0.70 | 7.964 | 237.3/1611.2 | 19.846 | 4.494 | 0.530 | 0.7616 | 17.793 |
| 0.2 | 1.00 | 10.666 | 332.4/1795.0 | 20.807 | 4.367 | 0.541 | 0.8537 | 3.666 |
| 0.5 | 1.00 | 9.739 | 333.7/1743.1 | 20.591 | 4.425 | 0.541 | 0.8300 | 9.102 |
| 1.0 | 1.00 | 8.905 | 335.0/1687.3 | 20.372 | 4.502 | 0.558 | 0.8045 | 18.048 |

Po sekundzie Drive 0,40→1,00 daje różnicę α=1.735° oraz prędkości 1.080 m/s. To **pośrednia odpowiedź całego modelu**, nie dowód sterowania poślizgiem napędowym tylnego koła. Nie mamy stanu obrotów koła, slip ratio ani jego niezależnej wymiany sił z podłożem; same zmiany stałych Fy tego nie dodadzą. Nie ma też empirycznego celu, który w tych próbach wykazałby konieczność takiej nowej mechaniki jako następnego kroku.

## B. Moment i wrażliwość regulatora

Nowy opcjonalny `YawLimit` zapisuje raw I·yawAcceleration, wynik istniejącego clampu ±0,5·B z authority, wynik limitu pracy, błąd orientacji, spin, I oraz kredyt dyssypacji. `AppliedForce` odczytuje dokładne argumenty zaraz po ContactAdapter.Force (Box2D ApplyForce/ApplyTorque), bez modyfikacji. Slip podaje desired/actual angle, B, siły i moce. Przy wyłączonych obserwatorach nie są obliczane te dodatkowe etapy ani tworzone obserwacje. Oryginalne działania sił pozostają w tej samej kolejności.

Limit energii zmienia moment przez **0,000% czasu we wszystkich przejazdach**. Średnia/maksymalna redukcja i całkowa frakcja usuniętego momentu: **0 N·m / 0 N·m / 0%**. Różnica after-energy vs moment do Box2D: dokładnie 0. Saturacja siłowego limitu yaw również 0%; największy stosunek |raw|/(0,5·B) w C-wide wynosi 0.893615. Osobno authority neutralnego Technique=50 skaluje niezerowe żądania przez 0,875; to stała redukcja 12,5%, nie wystąpienie saturacji ani aktywacja guardu energetycznego. Pole AuthorityScalingPercent liczy przedziały, w których ta redukcja przekracza 1e−5 N·m.

Nie ma przedziałów ograniczenia energetycznego współwystępujących z dużym błędem (próg |error|>5° ustalony w narzędziu); zbiór ograniczony jest pusty. Warunkowe średnie dla pustego zbioru mają w JSON 0 i **nie oznaczają** zmierzonej korelacji. Ogranicznik nie może być przyczyną opóźnienia lub błędu offsetu w tych konkretnych przebiegach, bo nie zmienia ich momentu. Nie wynika z tego jego nieszkodliwość dla wszystkich możliwych stanów. Osobny test siatki stanów wymusza oba rzeczywiste ograniczenia i chroni ich rachunek; nie wyłączono guardu w żadnym przejeździe.

| Yaw s | Wejście s | Desired/actual ≥5° s | Śr./max error ° (5 s) | Max błąd offsetu m | Min clearance m |
|---|---|---|---|---|---|
| 0.08 | 1.770 | 1.860/2.020 | 0.468/2.113 | 0.919 | 4.588 |
| 0.10 | 1.770 | 1.780/1.980 | 0.630/2.701 | 0.782 | 4.725 |
| 0.15 | 1.777 | 1.610/1.980 | 1.325/5.993 | 0.435 | 5.094 |

Próg 5° jest obserwacją reakcji, nie definicją poprawnego wejścia. Domyślnie rzeczywisty kąt osiąga go 0,21 s po początku łuku i 0,20 s po żądaniu; w tej chwili guard pozostaje nieaktywny. Źródła opóźnienia należy szukać w żądaniu preview i paśmie regulatora. To wskazanie kalibracyjne, nie wykryty błąd algorytmu.

| Yaw s | Lap 1/2 s | Śr./max error ° | Max offset error m | Min clearance m | Max usage / ≥95% s | ΔK−W J |
|---|---|---|---|---|---|---|
| 0.08 | 16.536/16.075 | 0.710/8.486 | 1.246 | 4.166 | 0.9550/1.690 | 0.208 |
| 0.10 | 16.556/16.094 | 0.923/9.439 | 1.173 | 4.241 | 0.9546/1.570 | 0.619 |
| 0.15 | 16.671/16.230 | 1.612/13.145 | 1.225 | 4.209 | 0.9534/1.720 | -0.051 |

Wszystkie warianty kończą dwa obiegi, bez kontaktu/upadku/wyjazdu. Pozostałe wejścia identyczne. Zmiana `SlipYawResponseSeconds` z definicji #97 zmienia także zewnętrzny T=0,65+4·yaw (0,97/1,05/1,25 s); nie jest izolowaną zmianą jednego pasma wewnętrznego. 0,08 s jest nieco szybsze i ma mniejszy błąd kąta, lecz większy błąd offsetu i mniejszy prześwit niż default. **Pozostaje 0,10 s**; sam lap time nie uzasadnia zmiany. Wszystkie te współczynniki nadal wymagają kalibracji.

## C. Wykonane linie

Te same umiejętności/setup/surface i prędkość początkowa 18 m/s, ref0; początkowy offset odpowiada stałej linii (2/5/8), a wariant mieszany startuje z 8 m. Porównanie nie izoluje identycznej pozycji startowej między trzema różnymi stałymi liniami; mieszana vs stała zewnętrzna ma identyczny prefiks. Żadnej idealnej ścieżki XY. Maksymalny legalny target=9,5 m, wszystkie użyte cele mieszczą się w granicach. Parametry regulatora domyślne, Drive=1 przez cały przebieg.

| Strategia | Lap 1/2 s | Droga 2 obiegi m | Entry/min/exit m/s | Max następnej prostej m/s | Śr./max |α| ° | ≥95% s | Strata boczna kJ | Max offset error m | Min clearance m |
|---|---|---|---|---|---|---|---|---|---|
| 2 m | 16.075/15.716 | 656.211 | 21.203/19.272/19.704 | 23.903 | 6.904/18.894 | 2.670 | 135.071 | 1.159 | 1.254 |
| 5 m | 16.556/16.094 | 692.969 | 21.214/19.990/20.386 | 24.313 | 6.612/18.896 | 1.570 | 134.317 | 1.173 | 4.241 |
| 8 m | 17.060/16.499 | 730.689 | 21.223/20.639/21.005 | 24.690 | 6.336/18.725 | 0.630 | 133.305 | 1.176 | 2.174 |
| 8 → 5 → 3 → 8 m | 16.920/16.442 | 708.880 | 21.223/20.008/20.515 | 24.344 | 6.668/18.585 | 0.860 | 136.904 | 3.886 | 2.600 |

Prędkości dotyczą pierwszego pełnego łuku i następującej prostej; kąty/straty/błędy obejmują oba obiegi. Droga z zaakceptowanych XY, włącznie z końcem ostatniego kroku; lap crossing interpolowany indywidualnie, bez generowania nowego stanu fizycznego. Clearance dotyczy całej mechanicznej kapsuły względem fizycznej krawędzi, nie środka ciała ani usable target span.

Czasy manewru zamrożono z osobnego kontrolnego przejazdu stałej linii 8 m, zanim porównano wyniki: target5 przy cp≥0,20, target3 przy cp≥0,55, target8 na początku kolejnej prostej. [Plan](slip-control-validation/plan.json): pierwsze terminy 2,91 / 4,93 / 7,58 s; dalsze z drugiego łuku i drugiego obiegu są jawne. Fizyczna faza manewru może później odbiegać od fazy pilota; nie przesuwano terminów pod jego wynik. Wszystkie komendy aktywują się we wskazanym terminie.

Pierwszy łuk mieszanej linii: offset entry **7.653 → exit 4.002 m**. Target3 nie jest idealnie osiągany przed zmianą na target8; w zachowanym CSV/telemetrii widać opóźnienie. Maksymalny błąd 3.886 m obejmuje skok intencji przy powrocie na 8 m i nie jest teleportacją. Trajektoria jest wykonalna w granicach toru, ale wykonanie nie jest dokładnym śledzeniem targetu. Jest szybsza od stałej zewnętrznej i wolniejsza od środkowej/wewnętrznej; nie daje podstaw do uniwersalnej przewagi szerokiego wejścia.

## D. Pełny bieg, kontakt i energia

D1: ref0/4/8/12 m, offset2/4/6/8 m, neutralni zawodnicy, 18 m/s. Każdy pokonuje **cztery indywidualne pełne lap reference od swojej pozycji początkowej**, w tym samym świecie; nie liczymy końca jako wspólnego przekroczenia 4·L przez różne początkowe pozycje.

| Rider | Lap 1/2/3/4 s | Meta s | Min clearance m | Max offset error m | ΔK−W J |
|---|---|---|---|---|---|
| 1 | 16.074/15.716/15.716/15.716 | 63.223 | 1.256 | 1.157 | -0.211 |
| 2 | 16.407/15.964/15.965/15.965 | 64.300 | 3.244 | 1.170 | -0.174 |
| 3 | 16.750/16.227/16.227/16.227 | 65.431 | 4.140 | 1.176 | 0.158 |
| 4 | 17.102/16.499/16.500/16.500 | 66.600 | 2.152 | 1.176 | 0.634 |

Cały świat 66.610 s, **6825 solver calls / 27300 substeps**, zero kontaktów/upadków, min rzeczywista odległość między kapsułami 0.878893 m, max zaakceptowane przesunięcie 0.247094 m. WorldRunner sprawdza finite oraz limit przemieszczenia na każdym kroku; nie stwierdzono nieciągłości ani przenikania. Clearance/energia są obserwowane aż do zakończenia całego świata, także gdy szybszy zawodnik już finiszował. Suma ΔK−W=0.407876 J; wzrost K przy dodatnim napędzie ma pokrycie w pracy sił i momentu. Nie jest sam w sobie błędem energii.

Fingerprint forward = reverse = bez obserwatorów = niezmieniony #97 w osobnym cost probe: `12ED459031EF4A77FFC9B8C316C01113110705BF9673F85CEDC0791134FC3EDA`. Identyczne solver calls, bez dodatkowych kroków do diagnostyki. [Dowód D1](slip-control-validation/D1-parity.json).

D2: dwa **aktywne** ciała na offset5, ref0/3, 20/18 m/s, Drive=0,40 od t0, target5. Początkowe kapsuły rozłączne (walidacja native MechanicalSeparation), bez wymuszenia Crash i bez edycji prędkości. **1 rzeczywisty epizod, 157 obserwacji punktów, 56 rozwiązanych punktów z dodatnim impulsem**, max total-normal impulse próbki 141.794310 N·s. Liczby to wynik, nie warunek strojenia. 202 calls; maksimum prędkości 20.104439 m/s. Min signed gap -0.000000572 m (około −0,57 µm, tolerancja kontaktu), końcowe kapsuły bez istotnego przenikania. Brak numerycznej eksplozji; istniejący solver wykrywa i rozstrzyga kontakt. Fingerprint i zdarzenia identyczne przy odwróceniu kolejności: `01C494F25D57B1F8EA0775A5689F02F72269EC4E0034E98C855A72E30D21AEAB`.

W kontakcie indywidualne ΔK−W mają przeciwne duże wartości (transfer między ciałami), a ich suma wynosi **-137.069912 J**. W nie obejmuje pracy impulsów kontaktowych, więc tej ujemnej sumy nie wolno nazywać błędem samego integratora: jest zgodna ze stratą w niesprężystym kontakcie/tarciu. Nie dodano mechanizmu upadku; oba ciała pozostają aktywne.

Osobny Drive=0 na rzeczywistym łuku, 8 s: K translacji+obrotu **23814.000000 → 9710.018979 J**. Największy przyrost pojedynczego kroku -0.000001825 J (ujemny), bez wzrostu; ΔK−W=-0.024259 J. Testy #97 nadal obejmują małą prędkość, side slip i kroki 5/10/20 ms. Nie znaleziono nowego fizycznego błędu wymagającego naprawy w tym PR. Zakres nie obejmuje pełnego czterookrążeniowego wyścigu z wieloma kontaktami, zużycia nawierzchni ani ekstremalnie gęstej walki.

## E. Krzywa opony i granice kalibracji

Stałe B=1800 N i m=147 kg. Dla każdego u ustalamy w=−u·tan(α). Silnik w tabeli żąda 0 N: usage=|Fy|/B; rezerwa Fx=sqrt(B²−Fy²), przy jej pełnym wykorzystaniu combined usage=1. Strata to −Fy·w. To czysta ewaluacja dokładnie istniejącego Tyre, bez nowej krzywej lub zmiany parametrów. [Pełna siatka](slip-control-validation/tyre-grid.json).

| u m/s | α ° | Fy N | Boczna usage | Pozostały Fx N | Strata boczna W |
|---|---|---|---|---|---|
| 5 | 0 | 0.000 | 0.0000 | 1800.000 | 0.000 |
| 5 | 3 | 626.278 | 0.3479 | 1687.535 | 164.109 |
| 5 | 6 | 1074.710 | 0.5971 | 1443.952 | 564.783 |
| 5 | 12 | 1499.281 | 0.8329 | 996.071 | 1593.410 |
| 5 | 20 | 1678.120 | 0.9323 | 651.086 | 3053.929 |
| 5 | 35 | 1764.474 | 0.9803 | 355.852 | 6177.492 |
| 18 | 0 | 0.000 | 0.0000 | 1800.000 | 0.000 |
| 18 | 3 | 711.591 | 0.3953 | 1653.372 | 671.273 |
| 18 | 6 | 1176.134 | 0.6534 | 1362.611 | 2225.099 |
| 18 | 12 | 1561.862 | 0.8677 | 894.754 | 5975.715 |
| 18 | 20 | 1707.002 | 0.9483 | 571.089 | 11183.365 |
| 18 | 35 | 1773.384 | 0.9852 | 308.397 | 22351.268 |
| 30 | 0 | 0.000 | 0.0000 | 1800.000 | 0.000 |
| 30 | 3 | 717.198 | 0.3984 | 1650.947 | 1127.602 |
| 30 | 6 | 1182.403 | 0.6569 | 1357.174 | 3728.268 |
| 30 | 12 | 1565.436 | 0.8697 | 888.487 | 9982.312 |
| 30 | 20 | 1708.590 | 0.9492 | 566.321 | 18656.280 |
| 30 | 35 | 1773.865 | 0.9855 | 305.621 | 37262.207 |

Geometria dostarcza promienia, rzeczywistej długości i szerokości, krawędzi oraz preview krzywizny. SegmentPhysics dostarcza odziedziczone safe²/R i zależności surface/skills/setup; B=m·safe²/R jest capability gry, nie pomiarem μ. Nowa krzywa Fy dostarcza bocznej dyssypacji i monotonicznej saturacji; jej sztywność 0,12 i regularizacje 0,5 m/s oraz 0,03 s pozostają PROVISIONAL, bez dopasowania danych opony. Limit yaw ogranicza pracę efektywnego aktuatora; nie zastępuje mechaniki kierowania, przechyłu i żyroskopu. Nie ma rozdziału przód/tył, load transfer, wheel speed, slip ratio ani niezależnego wheelspinu. Krzywa nie ma skalibrowanej gałęzi spadku siły za maksimum. Nie deklarujemy zgodności z prawdziwym motocyklem.

Do osobnej kalibracji: odziedziczony budżet/napęd/opory na dobranej geometrii i nawierzchni, charakterystyka Fy i straty, następnie preview oraz współzależne czasy obu pętli i Technique authority. Nie należy kompensować błędu jednej warstwy stałą innej. Samym strojeniem nie powstanie niezależny stan napędowego poślizgu koła. Bieżące dowody nie wykazują jednak, że brak tego stanu blokuje wymagane tu fizyczne utrzymanie i zmianę linii.

## Walidacja, koszt i odtworzenie

**27/27 testów celowanych** (5 nowych + 20 #97 + 2 geometrii), restore i Release warnings-as-errors poprawne. Nowe testy chronią dokładne ON #97/observer parity, oba aktywne limity na siatce, rachunek momentu do Box2D i 0 B, bitowy prefiks gazu, przyczynowe komendy linii, pełny D1 reverse oraz prawdziwy aktywny kontakt. Istniejące testy chronią OFF każdy ForceObservation/fingerprint, oryginalny autonomiczny replay #95 i rzeczywistą energię Drive=0. [Zapis walidacji](slip-control-validation/validation.json). Standardowe Draft Windows CI na końcowym HEAD: status i link w opisie PR; historyczne audyty #87–#97 nie były powtarzane.

34/34 hashy manifestu #95 i 63/63 #97 zgodne. Historyczny baseline #97 pozostaje o SHA-256 `2f9942bd2b32ea9f362c6d4811f2a0dd15c84c44aa89f54adee8ca30c6d5aae7`. Wszystkie 15 nowych par CSV/JSON przechodzą istniejący parser Replay #96. Dane mają Run.Initial/Result/Riders.Measurement/Consequences; nie rozszerzono UI ani katalogu wyboru viewer. Pliki można przekazać istniejącemu modelowi Replay; ich wybór w UI nie jest nową funkcją tego PR. Potwierdzone minięcia nie są eksportowane w tym nowym zestawie.

Koszt: Windows / .NET 8.0.26, Release, tiered JIT wyłączony, 10k warmup + 7 partii po 100k Forces, cały D1 po jednym rozgrzewającym przebiegu i 7 pomiarach. [Identyczny probe bazowy](slip-control-validation/baseline-cost-probe.cs) skompilowano w izolowanych drzewach #97 i #98; te same specyfikacje i fingerprint. Mediany:

| Porównanie | Baza | Wariant | Zmiana |
|---|---|---|---|
| Bez obserwatorów: Forces #97 → #98 | 0.473214 µs | 0.470531 µs | -0.57% |
| Bez obserwatorów: D1 #97 → #98 | 144.9864 ms | 144.8605 ms | -0.09% |
| #98 pusty pełny zestaw observerów: Forces | 0.621843 µs | 0.724317 µs | +16.48% |
| #98 pusty pełny zestaw observerów: D1 | 148.3423 ms | 150.7539 ms | +1.63% |

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
