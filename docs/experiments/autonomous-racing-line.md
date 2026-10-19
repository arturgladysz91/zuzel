# Autonomous Racing Lines: Entry, Apex and Exit (#95)

**PROVISIONAL — B: CZĘŚCIOWO.** Samodzielny wybór linii działa i poprawia rzeczywiste przejazdy: zewnętrzny start 63.295→61.443 s, przygotowanie przed łukiem 61.051→59.654 s; wewnętrzny start pozostaje identyczny. Wszystkie 10 głównych prób czterech motocykli ukończone bez kontaktu i Crash. Współpraca wymaga jednak dalszej oceny: Rolling Pack na prostej bardziej się rozciąga, a A2 ma około +19.5% wall i +16.5% wywołań solvera wskutek dłuższej bliskości. To nie jest potwierdzenie pełnej optymalności ani podstawa do domyślnego włączenia.

Baza Draft #94 `f996cd1e2c1424659dd9d106f63c007e5aa1ea0f`, branch `experiment/close-racing-following-gap`. Jeden nowy Draft, bez merge. `EnableAutonomousRacingLine=false` domyślnie, wymaga wyłącznie Tactical. Główne próby: Tactical=true, SideBySide=true, ProportionalThrottle=true, Quality=false, ClosureAdaptiveFollowingGap=false. Motoarena, jednolita zamrożona nawierzchnia Grip1/Ruts0/Moisture.5 (EffectiveGrip .9175), .01 s, 4 substeps, AI .1 s. Siły, Box2D, kontakty, grip, umiejętności, produkcyjny HeatSimulator i algorytm gazu #93 nie zmieniają się.

## A. Diagnoza i zamrożenie

[Konfiguracja/profily](autonomous-racing-line/configuration.json), [pieczęć bazowych plików](autonomous-racing-line/baseline-seal.json), [odpowiedzi przed implementacją](autonomous-racing-line/diagnosis.md). Najpierw 8 solo OFF (6 przypadków i 2 zadeklarowane kontrole wewnętrznej) oraz 5 pełnych OFF; ich hashe i komendy odpowiadają #94. Następnie jeden celowany S3 ON, pozostałe solo i pięć pełnych ON. Bez zmiany profili ani strojenia parametrów po wynikach. Korekta jednostek horyzontu w łuku po przeglądzie została sprawdzona przez ponowienie ON: wszystkie 11 hashy zaakceptowanych stanów pozostały identyczne. Baseline nie został nadpisany.

Bez rywali `TrafficDecision.Tactics` kończyło się brakiem komendy: poszukiwało ataku/obrony/SideBySide, a nie korzyści swobodnej jazdy. `HomeOffsetMeters` inicjalizowało się z celu startowego. Powroty po minięciu i obronie odtwarzały ten offset. #93 bez Front tylko otwiera gaz; #94 zmienia preferencję podążania, nie wybiera szybszej linii. #91 oceniał lokalne alternatywy walki i nie stanowi dowodu skutecznego wolnego przejazdu.

Istniejące Forces już płaci za zmianę: PD błędu i prędkości bocznej dokłada przyspieszenie do krzywizny; potrzebny scrub i skalowanie całego wektora w kręgu przyczepności ograniczają napęd. Żądany promień wpływa na przygotowanie prędkości. Zewnętrzna linia ma większą prędkość styczną, ale dłuższą drogę. Rzeczywiste stałe kontrole poniżej pokazują, że na tej nawierzchni wewnętrzna wygrywa również na pełnym lotnym okrążeniu, dla obu różnych możliwości motocykla.


| Profil | Lotne wewn. .5m [s] | Lotne zewn. 9.5m [s] | Min łuk wewn. [m/s] | Min łuk zewn. [m/s] |
|---|---|---|---|---|
| S2-outer | 14.768 | 15.422 | 21.383 | 24.248 |
| S5-low | 15.615 | 16.301 | 20.245 | 22.957 |
| S5-high | 13.996 | 14.619 | 22.549 | 24.282 |


## B. Mała polityka, oddzielna ocena i wykonanie

Trzy możliwości: utrzymaj obecny target, target−1.5 m, target+1.5 m. Kandydaci poza .5..MinimumUsableWidth−.5 są odrzucani, nie teleportowani/clampowani na nową linię. Korzyść musi przekraczać .15 m/s średniego postępu na całym horyzoncie. Brak bonusu prędkości, losowania i nakazu jazdy wewnątrz. Ta dyskretna próbka nie gwarantuje optimum: np. S3 kończy na target 1 m, bo następny pełny krok wykracza poza tor.

Fazy Approach/Entry/Apex/Exit wynikają z geometrii i trzecich części postępu łuku. Horyzont obejmuje pozostały lub najbliższy łuk oraz 20 referencyjnych metrów kolejnej prostej, uwzględnia reakcję, minimum 2 s i maksimum 12 s. Jeśli pełny fragment jeszcze się nie mieści, ocena czeka; start nie jest natychmiastowym ruchem w bok. Długość fizycznego łuku z TrackFrame jest przeliczona na wewnętrzne metry przed podzieleniem przez tempo referencyjne.

Ocena ponownie używa **ContestScoring.Progress**, niezależnie od flagi scoringu #91: istniejący analityczny PD po pełnej reakcji, czas potrzebnego scrub, sześć trapezów ograniczeń prędkości, promień i długość drogi przez ReferenceSpeed, grip, boczne żądanie, pozostały napęd/opor i odzyskiwanie tempa. Horyzont przechodzi przez Entry/Apex/Exit i początek prostej; nie optymalizuje tylko aktualnego offsetu. **Nie wykonuje Forces ani Box2D dla kandydatów, nie integruje nowego ciała i nie ma równoległego świata.** Geometria jest próbkowana przy bieżącym tempie referencyjnym, więc to przybliżenie kosztu, nie drugi symulator. Rzeczywisty wynik pochodzi wyłącznie z WorldRunner.

Po utrzymaniu celu przegląd co .5 s, po zmianie pełne reaction+3*LateralResponseSeconds (około 2.19 s). Pamięć przechowuje historyczny Home, ostatni aktywowany cel autonomiczny, fazę i czas ponownej oceny; bieżący cel taktyczny nadal jest TargetOffsetMeters/intencją. Home pozostaje historyczny. W ON powroty używają celu autonomicznego, aktualizowanego dopiero przy aktywacji; OFF zachowuje dawny Home dokładnie.

Priorytet: istniejące Emergency i OccupiedAlongsideCut → aktywna/pending taktyka → kontrolowane podążanie → autonomia. Wspólny aktualny korytarz z Front do 10 m blokuje autonomię. Intencje Attack/Defense/Contest/Return nie są zastępowane nowym optimum. Kandydat musi przejść dotychczasowe Corridor, CrossesOccupied z pełnymi kapsułami, granice toru i test aktualnej separacji .3 m. Przewidywanie korytarza jest ograniczone do reakcji+3 tau ruchu; nie zakłada tajnych przyszłych komend rywali. Nowa linia idzie przez tę samą opóźnioną kolejkę **Line**, z Drive=own.Drive. Nie ma prawa otworzyć gazu wbrew niezależnej kolejce #93. Oczekującą linię może anulować istniejące bezpieczeństwo.

## C. Solo: rzeczywiście wykonane cztery obiegi

S1 pole A, S2 pole D; S3 zaczyna 15 m przed łukiem (ref20, offset7, 22 m/s); S4 w wyjściu łuku (ref110, offset7, 22 m/s). S5 to istniejące pary legacy Speed/SlideControl 20/20 i 80/80, pole D, pozostałe parametry neutralne. S3/S4 mierzą pełne cztery referencyjne obiegi od początkowej pozycji, oddzielnie od startu z bramek. Wszystkie solo mają normalną AI bez rywali i zero komend gazu.


| Przypadek | 4 obiegi OFF [s] | ON [s] | ON−OFF [s] | Okrążenia ON 1/2/3/4 [s] | Zmiany autonom. | Aktywowane cele [m] |
|---|---|---|---|---|---|---|
| [S1-inner/ON](autonomous-racing-line/S1-inner-ON.json) | 60.532 | 60.532 | 0.000 | 16.228/14.768/14.768/14.768 | 0 | utrzymanie |
| [S2-outer/ON](autonomous-racing-line/S2-outer-ON.json) | 63.295 | 61.443 | -1.852 | 16.849/15.040/14.787/14.768 | 6 | 8.0 → 6.5 → 5.0 → 3.5 → 2.0 → 0.5 |
| [rolling-S3-entry/ON](autonomous-racing-line/rolling-S3-entry-ON.json) | 61.051 | 59.654 | -1.397 | 15.169/14.879/14.803/14.803 | 4 | 5.5 → 4.0 → 2.5 → 1.0 |
| [rolling-S4-exit/ON](autonomous-racing-line/rolling-S4-exit-ON.json) | 60.987 | 59.595 | -1.392 | 15.125/14.865/14.803/14.803 | 4 | 5.5 → 4.0 → 2.5 → 1.0 |
| [S5-low/ON](autonomous-racing-line/S5-low-ON.json) | 66.735 | 64.736 | -1.999 | 17.644/15.861/15.617/15.615 | 6 | 8.0 → 6.5 → 5.0 → 3.5 → 2.0 → 0.5 |
| [S5-high/ON](autonomous-racing-line/S5-high-ON.json) | 60.340 | 58.499 | -1.842 | 16.193/14.257/14.053/13.996 | 6 | 8.0 → 6.5 → 5.0 → 3.5 → 2.0 → 0.5 |


Prędkości i koszty poniżej to wykonane stany/siły, nie wynik funkcji oceny. Entry/Exit/Exit+20 m to średnie z przekroczeń danej geometrii w czterech obiegach; minima łuku obejmują cały przejazd. Oznaczenie `Lap` w JSON faz jest indeksem stałego obiegu referencyjnego toru, a nie początkiem indywidualnego pomiaru Rolling. ∫Ft+/m i ∫Ft−/m są składowymi całki siły stycznej (m/s), nie samą zmianą wartości speed przy skręcaniu. Ujemny czas Ft obejmuje dotychczasową korektę i opór; nie całość można przypisać ruchowi bocznemu. Pole AppliedWorkJoules w JSON to lewostronne przybliżenie liniowej pracy F·v, bez pracy momentu obrotowego.


| S | Tryb | Entry m/s | Exit m/s | Exit+20 m/s | Min łuk m/s | ∫Ft+/m | ∫Ft−/m | Ft<0 [s] | Max grip ratio |
|---|---|---|---|---|---|---|---|---|---|
| S1-inner | OFF | 21.534 | 21.399 | 22.822 | 21.383 | 37.288 | -15.507 | 44.874 | 1.000000 |
| S1-inner | ON | 21.534 | 21.399 | 22.822 | 21.383 | 37.288 | -15.507 | 44.874 | 1.000000 |
| S2-outer | OFF | 24.385 | 24.265 | 25.257 | 24.248 | 36.273 | -12.174 | 45.780 | 1.000000 |
| S2-outer | ON | 22.418 | 22.255 | 23.550 | 21.259 | 37.865 | -17.983 | 45.846 | 1.000000 |
| rolling-S3-entry | OFF | 23.559 | 23.504 | 24.602 | 23.004 | 14.434 | -13.477 | 42.468 | 1.000000 |
| rolling-S3-entry | ON | 22.053 | 21.938 | 23.281 | 21.552 | 15.412 | -17.636 | 46.135 | 1.000000 |
| rolling-S4-exit | OFF | 23.639 | 23.497 | 24.595 | 22.012 | 14.504 | -14.478 | 45.005 | 1.000000 |
| rolling-S4-exit | ON | 22.072 | 22.172 | 23.478 | 21.552 | 15.180 | -17.713 | 46.449 | 1.000000 |
| S5-low | OFF | 23.086 | 22.971 | 23.845 | 22.957 | 33.918 | -11.112 | 48.142 | 1.000000 |
| S5-low | ON | 21.161 | 21.009 | 22.164 | 20.245 | 35.340 | -16.515 | 48.193 | 1.000000 |
| S5-high | OFF | 25.561 | 25.591 | 26.666 | 24.282 | 38.350 | -12.970 | 39.332 | 1.000000 |
| S5-high | ON | 23.567 | 23.536 | 24.933 | 22.549 | 39.036 | -17.485 | 41.325 | 1.000000 |


S3 wybiera cel 5.5 m w .100 s, aktywuje .340 s, przed Entry .666 s. Offset na wejściu jest nadal 6.861 m; przy Apex 5.579 m. Nie przeskakuje z 7 na 5.5. Początkowe wyjście jest wolniejsze, ale całe cztery obiegi wygrywają — decyzji nie zaliczono tylko za przewidywany zysk. S4 na początkowym wyjściu utrzymuje linię, a następny cel aktywuje dopiero na prostej (1.840 s). S1 nie poprawia czasu, ponieważ utrzymanie już korzystnej linii wygrywa; hash OFF/ON jest identyczny.

Oba S5 dochodzą do .5 m, ale w różnym czasie (niski 32.740 s, wysoki 36.740 s). Odrębne stałe kontrole .5/9.5 m potwierdzają tę samą preferencję fizyczną dla obu możliwości; to nie dowód ignorowania osiągów. Nie ma dowodu, że na dowolnym gripie/torze optimum będzie identyczne. Na tym zestawie żaden wykonany cel autonomiczny nie poszerza linii; model rozważa poszerzenie, ale jego koszt i dodatkowa droga nie wygrywają. Entry/Apex/Exit są oceniane i zazwyczaj kończą się Hold, nie wymuszoną fazową ścieżką.


S2 liczba ocen faz: Approach=28, Entry=21, Apex=26, Exit=25. Wszystkie 6 zmian aktywowano w Approach.



## D. Pięć konfiguracji × OFF/ON

Profile, pola i pozycje dokładnie #94. A1/0, A2/1, close/0 od taśmy; rolling-straight i rolling-bend osobno. Czasy R1/R2/R3/R4; kolejność Rolling to czasy czterech pełnych obiegów od indywidualnych początków, nie wspólna meta regulaminowa. Minięcia pochodzą z dotychczasowego pełnego obserwatora kapsuł i utrzymanego prowadzenia, nie z początkowego rozstawienia ani samej zmiany kolejności czasów.

Autonomia = aktywacje nowego celu, A/D/S = aktywowane ataki/obrony/SideBySide wyłącznie kanału Line (atak może też być SideBySide). Podążanie częściowo to rider-sekundy w aktualnym wspólnym korytarzu ≤10 m, ważone zaakceptowanymi interwałami jak #94. Szczegółowe czasy okrążeń, decyzje, anulowania i zmiany na kolejnych obiegach są w podlinkowanym JSON dla każdego zawodnika.


| Próba | 4 obiegi [s] R1–R4 | Kolejność | Autonomia | A/D/S | Minięcia | Podąż. s | Częściowo podąż. s | Min fiz. m | Kontakt/Crash |
|---|---|---|---|---|---|---|---|---|---|
| [A1-rotation-0/OFF](autonomous-racing-line/A1-rotation-0-OFF.json) | 60.532/61.432/62.356/62.584 | R1>R2>R3>R4 | 0 | 1/0/7 | 0 | 0.000 | 0.000 | 2.200 | 0/0 |
| [A1-rotation-0/ON](autonomous-racing-line/A1-rotation-0-ON.json) | 60.533/60.888/61.365/61.835 | R1>R2>R3>R4 | 11 | 0/0/6 | 0 | 56.140 | 1.660 | 0.949 | 0/0 |
| [A2-rotation-1/OFF](autonomous-racing-line/A2-rotation-1-OFF.json) | 61.338/60.952/61.454/59.817 | R4>R2>R1>R3 | 0 | 5/1/19 | 1 | 26.611 | 0.000 | 1.616 | 0/0 |
| [A2-rotation-1/ON](autonomous-racing-line/A2-rotation-1-ON.json) | 61.043/59.917/60.614/59.818 | R4>R2>R3>R1 | 10 | 3/0/9 | 2 | 9.515 | 0.000 | 0.707 | 0/0 |
| [close-rotation-0/OFF](autonomous-racing-line/close-rotation-0-OFF.json) | 59.639/60.538/61.461/61.788 | R1>R2>R3>R4 | 0 | 3/0/6 | 0 | 0.480 | 0.000 | 2.200 | 0/0 |
| [close-rotation-0/ON](autonomous-racing-line/close-rotation-0-ON.json) | 59.639/60.000/60.470/60.839 | R1>R2>R3>R4 | 11 | 0/0/6 | 0 | 50.450 | 1.400 | 0.950 | 0/0 |
| [rolling-straight/OFF](autonomous-racing-line/rolling-straight-OFF.json) | 59.448/59.555/58.977/59.970 | R3>R1>R2>R4 | 0 | 1/0/12 | 1 | 49.560 | 12.300 | 0.494 | 0/0 |
| [rolling-straight/ON](autonomous-racing-line/rolling-straight-ON.json) | 58.766/59.408/58.524/59.714 | R3>R1>R2>R4 | 7 | 2/0/9 | 2 | 45.149 | 3.875 | 0.600 | 0/0 |
| [rolling-bend/OFF](autonomous-racing-line/rolling-bend-OFF.json) | 59.961/59.796/58.857/60.566 | R3>R2>R1>R4 | 0 | 2/0/8 | 1 | 31.970 | 0.000 | 0.496 | 0/0 |
| [rolling-bend/ON](autonomous-racing-line/rolling-bend-ON.json) | 59.807/59.494/58.856/60.474 | R3>R2>R1>R4 | 5 | 3/0/6 | 1 | 19.000 | 1.270 | 0.496 | 0/0 |


10/10 ukończonych, bez kontaktów, konsekwencji, NaN, teleportacji, przekroczeń granicy toru i zarejestrowanych zbliżeń mechanicznych <.3 m. To nie gwarancja dowolnej sytuacji. A1/close zaczynają samodzielnie zacieśniać po osiągnięciu tempa i horyzontu, zanim pojawi się realny pościg. Po zbliżeniu autonomia ustępuje gazowi/taktyce: A1 ma 1.660 s, close 1.400 s częściowego podążania. A2 ma 2 minięcia zamiast 1, lecz brak aktywowanej obrony w tym konkretnym przebiegu; sama liczba zdarzeń nie jest kryterium jakości. W rolling-straight 9 sugestii autonomicznych, 7 aktywacji — dwie anulowane, nie zaliczone jako wykonany ruch.

**Rolling Pack nie pozostaje tak samo zwarty.** Porównanie wspólnego czasu przy czterech nadal jadących zawodnikach (przed jakąkolwiek metą) mierzy max−min postępu referencyjnego, nie odległość fizyczną kapsuł. ON na prostej rozciąga grupę o około 6.4 m w 40 s; w łuku różnica jest mała. Zawodnicy poprawiają własne czasy, ale nie dowiedziono, że każde opuszczenie grupy pomaga walce. Ten wynik ogranicza ocenę końcową.


| Pack | Wspólny czas s | Rozpiętość OFF m | ON m |
|---|---|---|---|
| rolling-straight | 10.01 | 8.967 | 11.773 |
| rolling-straight | 20.01 | 10.387 | 11.840 |
| rolling-straight | 30.01 | 13.875 | 19.377 |
| rolling-straight | 40.01 | 12.832 | 19.223 |
| rolling-bend | 10.01 | 19.095 | 19.095 |
| rolling-bend | 20.01 | 25.630 | 25.631 |
| rolling-bend | 30.01 | 23.677 | 23.677 |
| rolling-bend | 40.01 | 29.418 | 29.283 |


Konflikt sąsiedzki: R1 ref20/offset7/22 m/s, R2 obok offset5.8/24 m/s; kapsuły początkowo oddzielone. O .1 s kandydat 5.5 m jest zablokowany, poszerzenie 8.5 m ocenione gorzej od Hold. Zero autonomicznych komend przez całe 8 s; normalna taktyka może nadal działać. Przegląd nie blokuje się na całe okrążenie. Osobny odczyt snapshotu reprezentuje aktywny AttackOutside na 8.5 m, podczas gdy samodzielna ocena bez rywala preferuje 7 m. Pełne Observe zachowuje atak i nie emituje przeciwnej komendy. To jawny test odczytu stanu pamięci, nie rzekomo wykonany nowy manewr. [Konflikty i stan](autonomous-racing-line/conflicts.json).

## E. Koszt AI i skutki nowej trajektorii

Bare WorldRunner, bez obserwatorów/CSV; po jednej rozgrzewce trzy próbki pełnego przejazdu, mediana wall/AI/line/alloc. .NET 8 Release na tym samym Windows, DOTNET_TieredCompilation=0. Wszystkie 18 hashy odpowiadają właściwemu audytowi. `AutonomousCandidates` liczy rzeczywiście ocenione numerycznie cele; odrzucone korytarze/granice są osobno. Razem dają 3 alternatywy na ocenę. Komendy = wyemitowane/aktywowane; cała taktyka obejmuje również koszt nowego wyboru.


| Próba | Tryb | Sim s | Solver | Substeps | Wall ms | AI ms | Wybór linii ms | MiB | Oceny | Cele/blocked | Komendy |
|---|---|---|---|---|---|---|---|---|---|---|---|
| S2-outer | OFF | 63.300 | 6346 | 25384 | 55.168 | 0.491 | 0.000 | 13.068 | 0 | 0/0 | 0/0 |
| S2-outer | ON | 61.450 | 6163 | 24652 | 54.021 | 1.392 | 0.957 | 12.925 | 100 | 246/54 | 6/6 |
| A2-rotation-1 | OFF | 61.460 | 6237 | 24948 | 136.073 | 5.637 | 0.000 | 17.515 | 0 | 0/0 | 0/0 |
| A2-rotation-1 | ON | 61.050 | 7268 | 29072 | 162.653 | 9.308 | 2.942 | 21.041 | 304 | 506/406 | 10/10 |
| rolling-straight | OFF | 59.980 | 7156 | 28624 | 157.800 | 5.985 | 0.000 | 20.427 | 0 | 0/0 | 0/0 |
| rolling-straight | ON | 59.720 | 6893 | 27572 | 154.823 | 9.110 | 2.606 | 20.202 | 245 | 419/316 | 9/7 |


A2: 6237→7268 solver calls, 0→1073 nominalnych extra proximity, 71→75 clip krzywizny i 20→17 clip deadline; świat trwa 61.46→61.05 s. Czas z istniejącym podziałem proximity 0→10.73 s. Nowy wybór kosztuje około 2.94 ms na pełny bieg, ale rzeczywista zmieniona trajektoria podnosi wall 136.07→162.65 ms (+19.5%) i alokacje. Tego wzrostu nie ukrywamy pod małym kosztem samej formuły. Rolling-straight ma mniej solver calls, ale również inną trajektorię, więc spadek wall nie jest optymalizacją Box2D. Nie dodano nowych substeps ani solverów dla kandydatów. [Surowe próbki](autonomous-racing-line/performance.json).

## F. Gotowość danych do wizualizacji #96

Zachowano **istniejący format HeatMeasurements CSV**, dwa rzeczywiste powtórzenia ON:

- [S3 przed łukiem](autonomous-racing-line/rolling-S3-entry-accepted.csv): czas, rider, X/Y, vx/vy, speed, reference, fizyczny offset, target, Drive, Heading, activity.
- [Cztery motocykle w Rolling Pack](autonomous-racing-line/rolling-straight-accepted.csv): te same pola dla czterech rzeczywistych ciał.

Są to zaakceptowane stany co około .1 s (pierwszy dostępny solver boundary), nie ponownie policzone pozycje. Intencja, observed/requested/actual activation, anulowania, kontakty i konsekwencje są w odpowiednim JSON przejazdu, Result.Decisions/Contacts i Consequences. Fazy i obserwacje kandydatów są diagnostyką decyzji, nie ruchem do odtwarzania. Zachowane orientacje to odczyt ciała, nie kierunek stycznej. #96 powinien używać CSV i zdarzeń; nie uruchamiać drugiego modelu ruchu. Taki zapis nie jest zrzutem wszystkich czterech substeps — szczegóły kontaktów należy brać z ich dokładnych znaczników czasu. Kolektor kończy zapis zawodnika na jego własnej mecie; do reprezentatywnej walki czterech motocykli dostępne jest wspólne okno 0..45 s przed metami, bez uzupełniania brakujących pozycji innym modelem. Bez renderera, UI i nowej biblioteki. [Dokładne hashe obu odtworzeń](autonomous-racing-line/replays.json), cztery motocykle również z odwróconej kolekcji wejściowej.

## Ocena i walidacja

**B — CZĘŚCIOWO.** Potwierdzono fizycznie sensowną, stabilną samodzielną jazdę, korzyść solo i kilka nowych sytuacji podążania w pełnych biegach. Brak podstaw do pełnego A ze względu na rozciąganie grupy i koszt A2; nie oceniono zróżnicowanej nawierzchni ani sytuacji, w której poszerzenie rzeczywiście wygra. Ograniczona próbka celów zostaje eksperymentalna i domyślnie OFF. Nie zmieniono parametrów w celu poprawienia tych wyników.

Regresje sprawdzają: wymagania flagi bez #91/#94, historyczne pięć #94 OFF oraz jego adaptacyjny wariant, geometryczne jednostki horyzontu, solo wewnętrzne bez zmiany hasha, przygotowanie przed Entry, pełną reakcję i zakres celów, oddzielne Home/autonomię, konflikt sąsiada i przeciwnego ataku, nieunikniony Crash/Emergency, brak nadpisania gazu i aktywacji po mecie oraz odwróconą kolekcję. Dotychczasowe #92–#94 sprawdzają również stare osiem OFF/ON, kolejki i realny kontakt. Manifest SHA-256 obejmuje surowe dowody; bazowa pieczęć pozostaje niezmieniona.

Odtworzenie z katalogu repozytorium:

```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
$audit='tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll'
$out='work/autonomous-line-reproduction'
foreach($stage in @('baseline','focus','solo','heats','conflicts','replay','pack-windows')) {
  dotnet $audit autonomous-line $out $stage
}
$env:DOTNET_TieredCompilation='0'
dotnet $audit autonomous-line $out benchmark
python tools/shared-world-speedway/autonomous-line-report.py $out
```

Baseline/ON nie są nadpisywane przez CLI. Generator raportu czyta pliki, sprawdza pieczęć, hashe, reakcję, mety, granice, odtworzenia i benchmarki; nie uruchamia symulacji ani optymalizacji.


Walidacja lokalna: restore + Release warnings-as-errors build; 143/143 celowanych testów (10 nowych); discovery 2656/2656 bez braków/duplikatów; guard 39 zamrożonych produkcyjnych konsumentów; Python 89+7; 18 zgodnych hashy benchmarkowych i 2 dokładne odtworzenia accepted-state CSV (cztery motocykle z odwróconej kolekcji).
