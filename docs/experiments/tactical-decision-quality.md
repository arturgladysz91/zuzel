# Tactical Decision Quality: Maneuver Cost and Value (#91)

**PROVISIONAL; wynik B — CZĘŚCIOWO.** Baza #90: `df1904634ee8bbb40e4a7a047606c1641e4d2e9b`. Nowy scoring poprawia ranking krótkiego okna i usuwa dwa stratne wybory, ale odrzuca też trzy korzystne ruchy. Nie uzasadnia włączenia domyślnego ani kalibracji produkcji. Nie deklarujemy ogólnej poprawy wyborów w całym zbiorze.

## Metoda i zamrożona baza

14 jawnych przypadków z `TacticalQualityAudit.Cases()`, bez późniejszego zmieniania wejść, wyboru zwycięzców ani siatki parametrów. Najpierw Debug zapisujący wykonania starego modelu, dopiero potem zmiana matematyki. SHA-256 oryginalnego pliku bazowego: `19e11badf14d6612a759a4c681c2dd7e5ed62b2ea5c518ef5de5db8b3bee57a1`. [Baza](tactical-decision-quality-baseline.json), [pełne wejścia z Gameplay](tactical-decision-quality-inputs.json). `RiderProfile.Gameplay` ma JsonIgnore w domenie: ewaluacja używa tych samych jawnych definicji, a nie stratnego round-trip profilu z JSON.

W każdej próbie: identyczny start, zamrożony tor, brak reaktywnej AI, znane z góry ControlCommand. Aktualne sterowanie, Hold pozycji, wewnętrzna i zewnętrzna. Zmiana o 1 m od rzeczywistego offsetu obserwacji, tak samo jak #90; komenda po pełnej reakcji. Samotny koszt własny izoluje odległy zawodnik ze stałym targetem, bez kontaktów; H zachowuje cztery oryginalne motocykle F i ich komendy początkowe. Nie odtwarzamy dowolnego stanu Box2D. Wszystkie dostępne próby mają dokładnie identyczne ciała i kontrolę do aktywacji; 0 kontaktów w kontrolowanych próbach. Wspólna komenda również w wariancie bazowym zapewnia te same granice czasowe. Interpolacja między zaakceptowanymi stanami służy wyłącznie pomiarowi referencji, nigdy sterowaniu ani przestawianiu ciał.

Prognoza #90 to różnica jego końcowych temp pomnożona przez czas po aktywacji: jawna ekstrapolacja starej oceny, nie twierdzenie, że #90 miał prognozę dystansu dla każdego T. Nowa prognoza jest całką różnicy własnych oszacowanych temp. Wszystkie odległości poniżej są metrami wewnętrznej referencji. Dodatkowy koniec okna to wcześniejszy z 3 s i rzeczywistego wyjścia bazowego z łuku; przyszły czas wyjścia występuje wyłącznie w audycie.

## 1–2. Diagnoza F i koszt reakcji

#90 w F: obserwacja R1 4,10 s, reakcja 0,244 s, aktywacja 4,344 s, target 0,580463 m. Ocena 20,978596 vs 20,680080 ref m/s (+0,298516). Średnia korekta boczna w całej odpowiedzi PD prawie znosi początkowy szczyt z późniejszym powrotem. Model widzi korzystny krótszy promień pod koniec, lecz nie przechowuje wcześniejszej utraty prędkości i postępu. Ogranicza tempo od góry bieżącą prędkością, ale pomija czas odzyskiwania straty po szczycie.

W kontrolowanym H szczyt żądania bocznego/budżetu: 1.000 → 1.152; minimalna prędkość w 3 s: 21.749 → 20.763 m/s; całka wykonanej siły stycznej: 14.57 → -117.07 N·s. Żądanie przekracza dostępny budżet, istniejący scrub hamuje, wspólny krąg przyczepności ogranicza napęd, a idealna odpowiedź offsetu jest zbyt szybka. Po 1/2/3 s: -0.653, -0.861, -0.477 m. To efekt samego manewru przy stałych rywalach, bez nowych kontaktów.

W rzeczywistym wyścigu #90 R1 ma vs Side OFF −0,252 m w 4,805 s i −0,878 m w 6 s; dopiero około +0,020 m w 12 s. Ostatnia wartość zawiera późniejsze komendy rywali. Nie jest dowodem opłacalności pojedynczego zacieśnienia. Przejścia segmentów entry/apex/exit zmieniają dostępny napęd i nawierzchnię, a wyjście na prostą usuwa korzyść krótszego promienia. Przy I na końcu łuku uproszczona prognoza granicy pozostaje istotnym błędem.

Przed aktywacją zysk nowego manewru wynosi dokładnie 0; reakcja nie spowodowała straty −0,252 m przed wykonaniem, bo ta strata pojawia się później. Dodatkowa kontrolowana próba H bez czekania: po 0,244 s od obserwacji byłaby już strata -0.074 m. W tym samym czasie świata, po 1/2/3 s od opóźnionej aktywacji, natychmiastowa komenda daje -0.772, -0.854, -0.471 m. Różnica reakcji i wykonania nie jest addytywną stałą: zmiana terminu zmienia fazę łuku oraz warunki scrub. Główna strata pochodzi ze zmiany linii, nie z samego czekania. [Pomiar reakcji](tactical-decision-quality-reaction.json).

## 3. Matematyka i granice przybliżenia

`EnableTacticalQualityScoring=false` domyślnie; ON wymaga obu dotychczasowych polityk. Ten sam TrafficDecision, korytarz, margines, reakcja, kolejka i histereza. Zmienia się tylko Rate używany do rankingu: `Progress(target,T)/T`. Wymagany zysk to nadal `0,12 + 0,15*(1-quality)` ref m/s, czyli w dystansie ten sam margines razy T. Jakość Attack/Defense zmienia horyzont i margines; Technique nie dostaje nowego bonusu fizycznego.

1. Ta sama analityczna odpowiedź PD dotychczasowego targetu przez reakcję, identyczna dla każdego wariantu. Bieżąca prędkość jest przybliżeniem prędkości początku okna; nie integrujemy ruchu w fazie reakcji.
2. Sześć trapezów (7 punktów), plus jeden punkt capability przy aktywacji. Boczna korekta jest chwilowa, nie uśredniona przed ograniczeniem. Przy przeciążeniu szacowany czas scrub `(v-current_cap)+/correction` opóźnia idealną zmianę offsetu. To jedyna korekta pierwszego rozwiązania: bez niej C wciąż dostawało fałszywie dodatni zysk.
3. Istniejące MaxSafeTurnSpeedForRadius, korekta prędkości, dostępny napęd, opory i CornerEnvelope dają ograniczoną obwiednię tempa. Zajętość przyczepności ogranicza dostępne przyspieszenie styczne. Minimum stożków ograniczeń z wcześniejszych punktów zachowuje stratę; odzyskanie wymaga skończonego napędu. Nie ma stanu pozycji/prędkości alternatywnego motocykla ani krokowego solvera jego ruchu.
4. Całkujemy tempo `v_tangent*R_inner/(R_inner+offset)` na łuku lub `v_tangent` na prostej; odejmujemy identycznie liczoną bazę. Geometrię/nawierzchnię próbek wybiera wyłącznie bieżące stałe tempo referencji, nie przyszły przejazd rywala.
5. T = min(dotychczasowe 3τ z Attack/Defense, czas do zmiany krzywizny − reakcja), minimum 0,1 s. Dotychczasowy horyzont sprawdzania bezpieczeństwa pozostaje bez zmian. I ma krótki T około 0,607 s; pozostałe około 1,70–1,91 s. Brak nazw, ID i czasu F w scorerze.

Ograniczenia: idealny PD nadal nie jest wykonanym ruchem w tarciu; opóźnienie scrub bywa zbyt ostrożne (D). Powrót prędkości jest obwiednią, nie prognozą realnego napędu. Stała projekcja referencji błędnie umieszcza przejścia segmentów (I). Model nie zna przyszłych komend rywali ani przyszłej wolnej przestrzeni. Jego błędy rosną w 2–3 s. Nie poprawiano regulatora, gripu ani pozycji dla polepszenia wyniku.

## 4–6. Ranking, decyzje i przypadki, gdzie #90 jest lepsze

Znak: trzy klasy z martwą strefą ±0,02 m. Ranking: wygrywa kandydat z najwyższą prognozą, trafny jeśli jego rzeczywisty zysk jest najwyżej 0,02 m poniżej najlepszego dostępnego. Statystyki błędu/znaku obejmują 27 dostępnych inside/outside; current i niemal identyczny Hold nie pompują trafności. Ranking obejmuje wszystkie dostępne warianty. Koniec okna nie jest powtórnie doliczany jako 3 s. [Metryki](tactical-decision-quality-metrics.json).

| T po aktywacji | Model | Znak | Ranking | Średni błąd bezwzgl. m | Mediana m | Największy błąd m |
|---|---|---:|---:|---:|---:|---:|
| 1 s | #90 | 23/27 | 10/14 | 0.296 | 0.206 | 0.952 |
| 1 s | nowy | 25/27 | 13/14 | 0.153 | 0.109 | 0.486 |
| 2 s | #90 | 24/27 | 11/14 | 0.297 | 0.086 | 1.458 |
| 2 s | nowy | 24/27 | 12/14 | 0.572 | 0.454 | 1.524 |
| 3 s | #90 | 24/27 | 12/14 | 0.780 | 0.640 | 1.977 |
| 3 s | nowy | 24/27 | 13/14 | 1.002 | 0.944 | 2.900 |

Wybór z marginesem i runtime T porównujemy oddzielnie z wykonanym wynikiem po 2 s (stały punkt diagnostyczny, nie identyczny z T, szczególnie I). Aktualna kontrola i Hold różniący się <0,05 m traktujemy jako ten sam wybór. Przy poprawności decyzji tolerancja 0,02 m. Zyskiem wartym odrzucenia/wyboru na tej diagnostyce jest >0,02 m, a nie koniecznie zysk przekraczający runtime margines.

| Przypadek | #90 wybór | Nowy wybór | Najlepszy wykonany w 2 s | Zysk wybranego #90 → nowy, m | T, s |
|---|---|---|---|---:|---:|
| A-inner-hold | current | current | hold-position | +0.000 → +0.000 | 1.874 |
| B-small-tighten | inside | inside | inside | +0.807 → +0.807 | 1.874 |
| C-loaded-tighten | inside | current | hold-position | -0.341 → +0.000 | 1.874 |
| D-fast-outer | inside | current | inside | +0.386 → +0.000 | 1.874 |
| E-slow-outer | inside | inside | inside | +0.719 → +0.719 | 1.874 |
| F-entry-16 | inside | inside | inside | +0.759 → +0.759 | 1.874 |
| F-entry-19 | inside | inside | inside | +0.903 → +0.903 | 1.874 |
| F-entry-23 | current | current | current | +0.000 → +0.000 | 1.874 |
| G-low-tech-attack | inside | current | inside | +0.952 → +0.000 | 1.695 |
| G-high-tech-attack | inside | inside | inside | +0.952 → +0.952 | 1.914 |
| G-defense | inside | inside | inside | +0.952 → +0.952 | 1.914 |
| H-original-F | inside | current | hold-position | -0.861 → +0.000 | 1.835 |
| I-near-exit | inside | current | inside | +0.251 → +0.000 | 0.607 |
| J-existing-response | current | current | current | +0.000 → +0.000 | 1.874 |

Poprawne/błędne wybory: #90 12/2, nowy 11/3. Stratne wybrane manewry: 2 → 0; odrzucone dodatnie (>0,02 m): 0 → 3. Dwa poprawione wybory C/H; trzy gorsze D/G-low/I. B, E, F16/F19 i G-high/Defense zachowują korzystne zacieśnienia, A i J zachowują kontrolę, F23 pozostaje odrzucone również przez stary margines. D/G-low/I to konkretne przypadki, gdzie stary wybór był lepszy. Krótszy horyzont I wyjaśnia zachowawczość, ale jej nie usuwa z wyniku.

D nie dowodzi opłacalności nowego ruchu na zewnątrz: badany szybki zewnętrzny zawodnik lepiej trzyma obecną linię niż ją poszerza, lecz wykonane zacieśnienie +0,386 m w 2 s jest jeszcze lepsze. W zamrożonym zbiorze nie wyszedł dodatni ruch na zewnątrz; nie zmienialiśmy przypadków, by go uzyskać. Zachowany test #90 pokazuje korzystne utrzymanie zewnętrznej przy większym rzeczywistym tempie. Ten zbiór nie potwierdza jakości wybierania dodatniego poszerzenia.

Największe błędy nowej prognozy i gorsza średnia/mediana w dłuższym oknie są powodem oceny B, nie A. Ranking po 1 s poprawia się w trzech przypadkach; po 2 s netto w jednym, po 3 s netto w jednym. Samo wyeliminowanie błędnego manewru F nie wystarcza do uznania całej polityki za lepszą.

## 7. Rzeczywisty pierwszy łuk, z aktywną AI wszystkich

[Pełne wyścigi i komendy](tactical-decision-quality-races.json), [oceny kandydatów](tactical-decision-quality-race-candidates.csv). Tutaj Quality OFF oznacza pełne #90 z Side ON. A zachowuje oba skuteczne zacieśnienia i dokładny cały hash. E/F odrzucają wczesne cięcie R1, zachowując dotychczasowe Hold. F później ma inną komendę R3 (ContestInside 10,90 → 11,14 s); ta i pozostałe późniejsze reakcje nie są czystym efektem R1. W surowym JSON Prefix.Item1 to pierwsza różna aktywacja, Item2 potwierdza dokładny prefix, Item3 liczy wspólne próbki.

| Wyścig | Pierwsza różna aktywacja | Prefix | ΔR1 ref w 4,805 s | w 6 s | w 12 s |
|---|---:|---|---:|---:|---:|
| SA-entry-parallel | brak | True | — | — | — |
| SE-original-Motoarena | 4.34 | True | +0.248 | +0.901 | +0.610 |
| SF-varied-Motoarena | 4.344 | True | +0.252 | +0.878 | -0.020 |

Dodatnie Δ oznacza Quality ON − #90. W F daje to +0,252 m w 4,805 s, +0,878 m w 6 s, lecz −0,020 m w 12 s i zmianę końcowej przewagi R1–R2 o −0,012 m. Nie nazywamy tego wygraną pojedynku ani dowodem długoterminowej poprawy. Naturalne rozstrzygnięcia równoległej walki są w obu wariantach raportowane oddzielnie. Prefixy E/F pozostają dokładne do odpowiednio 4,340/4,344 s. Interpolowany checkpoint przy 4,344 może różnić się o mikrometry z powodu odmiennych późniejszych granic próbek; prefix sprawdza wyłącznie rzeczywiste wspólne stany.

## 8. Regresje i fizyka

97/97 celowanych Release (81 poprzednich +16 nowych): przełączniki, rzeczywisty zyskowny manewr A, obciążone C/H, deterministyczne powtórzenie/odwrócenie kolekcji, dokładny prefix, F-close, Brush/MajorSave/Crash oraz #88/#89/#90. [Parzystość](tactical-decision-quality-parity.csv): 24/24 dotychczasowe Side OFF/ON z Quality OFF zachowują dokładne hashe, końcowe ciała, komendy i kontakty #90. Zero QualitySamples przy OFF. Stary wzór wyodrębniono bez zmiany działań arytmetycznych.

RiderController.Forces, Box2D, wymiary, grip/capability, krzywizna #88, klasyfikacja/skutki, substeps, Reaction, częstotliwość 100 ms i HeatSimulator bez zmian. Te same niezmienne snapshoty, brak nowego RNG/zużycia toru/rewindu lub alternatywnego świata runtime. Fizyczne wykonania nadal przechodzą przez ten sam solver. Maksymalny wykonany force/grip w wyścigach ≤1,000001.

Jedna lokalna kompilacja Release całości, 0 błędów; trzy ostrzeżenia NU1900 oznaczały niedostępny sieciowo indeks audytu NuGet, nie ostrzeżenia źródeł. Po niej naprawiono tylko filtr niedopuszczalnego kandydata w narzędziu mikrobenchmarku i sprawdzono ten harness w Debug; model runtime pozostaje dokładnie tym z Release. Osobny mały harness Debug mierzy skopiowane, identyczne DLL Release (SHA-256 zweryfikowane), bez ponownego budowania modelu. Standardowe Windows CI weryfikuje końcowe źródła, z niezmienionym workflow.

## 9. Koszt

Sam scorer: mediana 0.477 → 9.062 µs/ocenę (~19.0×). 5 próbek po 820 wywołań na tych samych 14 stanach, tylko dopuszczalne targety, bez Box2D/observerów; alokacje ~0 → 704 B/ocenę pochodzą z istniejących look-upów TrackFrame.At. Scorer używa małego stackalloc 7 liczb, bez drzewa wariantów. 8 zapytań capability na score; co najwyżej 4 score na pełną obserwację (baza + trzy kandydaty), czyli 32 próbki. Korytarz jest sprawdzany przed kandydatem. [Mikrokoszt](tactical-decision-quality-scoring-cost.json).

Pełne biegi Release: 10 ms, po 5 naprzemiennych próbek po 2 wykonania, wspólny horyzont, bez observerów. Różne decyzje mogą zmienić pracę solvera. CPU, alokacje i wszystkie liczniki są w [surowym pomiarze](tactical-decision-quality-performance.json).

| Przypadek | Czas ms #90 → nowy | Zmiana | Taktyka ms #90 → nowy | SolverCalls #90 → nowy | QualitySamples ON |
|---|---:|---:|---:|---:|---:|
| solo-heat | 36.329 → 34.169 | -5.95% | 0.207 → 0.211 | 6064 → 6064 | 0 |
| four-distant | 1.231 → 1.213 | -1.47% | 0.004 → 0.004 | 80 → 80 | 0 |
| SA-entry-parallel | 3.396 → 3.398 | +0.06% | 0.087 → 0.105 | 402 → 402 | 48 |
| SE-original-Motoarena | 19.203 → 22.458 | +16.95% | 0.492 → 0.640 | 1404 → 1584 | 520 |
| SF-varied-Motoarena | 18.054 → 21.577 | +19.52% | 0.352 → 0.517 | 1405 → 1662 | 824 |
| G-dense-stress | 18.719 → 18.971 | +1.35% | 0.281 → 0.391 | 1463 → 1463 | 472 |

Solo/odlegli mają zero próbek nowego modelu; ujemna różnica całego czasu jest szumem, nie ujemnym kosztem AI. E/F drożeją także przez inną późniejszą pracę solvera. To realny koszt polityki: około +17%/+20% całego okna, choć sam dodatkowy czas taktyki wynosi około 0,15–0,17 ms. Nie przedstawiamy poprawy F jako darmowej. Wzrost kosztu i utrata trzech korzystnych decyzji blokują rekomendację szerszego użycia; nie budowaliśmy kolejnego modelu ani integratora dla poprawienia tych liczb.

## Wszystkie prognozy przed/po

Każda komórka prognozy: **#90 → nowy**, obok niezależny wykonany zysk. Current ma dokładnie 0/0/0. Hold-position bywa inną komendą niż aktualny target (szczególnie J), dlatego pozostaje w danych. `n/d` = target poza istniejącą dopuszczalną szerokością, bez próby wykonania. [CSV obejmuje także czas i wynik końca okna](tactical-decision-quality-predictions.csv).

| Przypadek | Kandydat | Prog. 1 s | Wyk. 1 s | Prog. 2 s | Wyk. 2 s | Prog. 3 s | Wyk. 3 s |
|---|---|---:|---:|---:|---:|---:|---:|
| A-inner-hold | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| A-inner-hold | hold-position | +0.001 → +0.000 | +0.000 | +0.002 → +0.001 | +0.002 | +0.002 → +0.002 | +0.003 |
| A-inner-hold | inside | n/d | — | n/d | — | n/d | — |
| A-inner-hold | outside | -0.474 → -0.120 | -0.241 | -0.947 → -0.520 | -1.042 | -1.421 → -1.049 | -2.162 |
| B-small-tighten | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| B-small-tighten | hold-position | +0.000 → +0.000 | +0.000 | +0.001 → +0.000 | +0.001 | +0.001 → +0.001 | +0.002 |
| B-small-tighten | inside | +0.366 → +0.090 | +0.182 | +0.731 → +0.397 | +0.807 | +1.097 → +0.817 | +1.677 |
| B-small-tighten | outside | -0.349 → -0.088 | -0.177 | -0.697 → -0.382 | -0.765 | -1.046 → -0.780 | -1.564 |
| C-loaded-tighten | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| C-loaded-tighten | hold-position | +0.001 → +0.000 | +0.000 | +0.002 → +0.001 | +0.002 | +0.002 → +0.002 | +0.003 |
| C-loaded-tighten | inside | +0.474 → -0.890 | -0.404 | +0.947 → -1.256 | -0.341 | +1.421 → -2.391 | +0.012 |
| C-loaded-tighten | outside | -0.451 → -0.114 | -0.230 | -0.901 → -0.494 | -0.992 | -1.352 → -0.977 | -1.982 |
| D-fast-outer | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| D-fast-outer | hold-position | +0.001 → +0.000 | +0.000 | +0.001 → +0.001 | +0.001 | +0.002 → +0.001 | +0.003 |
| D-fast-outer | inside | +0.399 → -0.402 | -0.039 | +0.798 → -0.626 | +0.386 | +1.197 → +0.738 | +1.080 |
| D-fast-outer | outside | -0.381 → -0.096 | -0.194 | -0.763 → -0.418 | -0.838 | -1.144 → -0.845 | -1.724 |
| E-slow-outer | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| E-slow-outer | hold-position | +0.000 → +0.000 | +0.000 | +0.001 → +0.000 | +0.001 | +0.001 → +0.001 | +0.001 |
| E-slow-outer | inside | +0.326 → +0.080 | +0.163 | +0.652 → +0.353 | +0.719 | +0.977 → +0.726 | +1.486 |
| E-slow-outer | outside | -0.311 → -0.079 | -0.158 | -0.623 → -0.341 | -0.683 | -0.934 → -0.695 | -1.391 |
| F-entry-16 | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| F-entry-16 | hold-position | +0.000 → +0.000 | +0.000 | +0.001 → +0.000 | +0.001 | +0.001 → +0.001 | +0.001 |
| F-entry-16 | inside | +0.344 → +0.085 | +0.172 | +0.688 → +0.373 | +0.759 | +1.032 → +0.766 | +1.561 |
| F-entry-16 | outside | -0.328 → -0.083 | -0.167 | -0.656 → -0.360 | -0.719 | -0.984 → -0.731 | -1.459 |
| F-entry-19 | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| F-entry-19 | hold-position | +0.001 → +0.000 | +0.000 | +0.001 → +0.001 | +0.001 | +0.002 → +0.001 | +0.002 |
| F-entry-19 | inside | +0.409 → +0.100 | +0.204 | +0.818 → +0.444 | +0.903 | +1.227 → +0.910 | +1.866 |
| F-entry-19 | outside | -0.389 → -0.098 | -0.198 | -0.779 → -0.427 | -0.855 | -1.168 → -0.869 | -1.740 |
| F-entry-23 | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| F-entry-23 | hold-position | -0.000 → +0.003 | +0.001 | -0.001 → +0.002 | -0.000 | -0.001 → +0.001 | -0.001 |
| F-entry-23 | inside | +0.147 → -0.882 | -0.684 | +0.295 → -2.192 | -1.029 | +0.442 → -2.262 | -0.983 |
| F-entry-23 | outside | -0.252 → -0.028 | -0.229 | -0.504 → -0.424 | -0.998 | -0.756 → -0.965 | -2.010 |
| G-low-tech-attack | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| G-low-tech-attack | hold-position | +0.001 → +0.000 | +0.000 | +0.001 → +0.001 | +0.001 | +0.002 → +0.001 | +0.003 |
| G-low-tech-attack | inside | +0.403 → +0.106 | +0.215 | +0.806 → +0.467 | +0.952 | +1.209 → +0.957 | +1.994 |
| G-low-tech-attack | outside | -0.385 → -0.104 | -0.209 | -0.770 → -0.450 | -0.901 | -1.155 → -0.915 | -1.859 |
| G-high-tech-attack | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| G-high-tech-attack | hold-position | +0.001 → +0.000 | +0.000 | +0.001 → +0.001 | +0.001 | +0.002 → +0.001 | +0.003 |
| G-high-tech-attack | inside | +0.436 → +0.106 | +0.215 | +0.872 → +0.467 | +0.952 | +1.308 → +0.957 | +1.993 |
| G-high-tech-attack | outside | -0.415 → -0.104 | -0.209 | -0.830 → -0.450 | -0.901 | -1.244 → -0.915 | -1.859 |
| G-defense | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| G-defense | hold-position | +0.001 → +0.000 | +0.000 | +0.001 → +0.001 | +0.001 | +0.002 → +0.001 | +0.003 |
| G-defense | inside | +0.436 → +0.106 | +0.215 | +0.872 → +0.467 | +0.952 | +1.308 → +0.957 | +1.994 |
| G-defense | outside | -0.415 → -0.104 | -0.209 | -0.830 → -0.450 | -0.901 | -1.244 → -0.915 | -1.859 |
| H-original-F | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| H-original-F | hold-position | +0.010 → +0.002 | +0.005 | +0.019 → +0.010 | +0.017 | +0.029 → +0.016 | +0.025 |
| H-original-F | inside | +0.299 → -1.069 | -0.653 | +0.597 → -2.385 | -0.861 | +0.896 → -2.657 | -0.477 |
| H-original-F | outside | -0.470 → -0.120 | -0.240 | -0.939 → -0.519 | -0.987 | -1.409 → -0.912 | -1.324 |
| I-near-exit | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| I-near-exit | hold-position | +0.001 → -0.000 | +0.000 | +0.001 → -0.000 | +0.000 | +0.002 → -0.000 | +0.000 |
| I-near-exit | inside | +0.434 → -0.004 | +0.123 | +0.867 → -0.048 | +0.251 | +1.301 → -0.011 | +0.021 |
| I-near-exit | outside | -0.413 → -0.007 | -0.123 | -0.826 → +0.037 | -0.252 | -1.239 → +0.006 | -0.095 |
| J-existing-response | current | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 | +0.000 → +0.000 | +0.000 |
| J-existing-response | hold-position | -0.799 → -0.197 | -0.408 | -1.598 → -0.869 | -1.800 | -2.397 → -1.776 | -3.755 |
| J-existing-response | inside | -0.385 → -0.094 | -0.195 | -0.770 → -0.417 | -0.871 | -1.155 → -0.855 | -1.829 |
| J-existing-response | outside | -1.194 → -0.299 | -0.615 | -2.389 → -1.306 | -2.684 | -3.583 → -2.660 | -5.560 |

Reprodukcja: `quality-freeze <nowy-katalog>` tylko przed zmianą modelu; istniejąca baza jest chroniona przed nadpisaniem. `quality-evaluate <baza> <wynik>` nie wykonuje ponownie Box2D ani prefixów. `quality-races <wynik>` to osobne trzy wyścigi reaktywne. `quality-reaction <baza> <wynik>` dodaje tylko kontrolę czasu H. `quality-benchmark <wynik> <baza>` to pojedynczy ograniczony pomiar; sam scorer można powtórzyć przez `quality-score-cost`. Normalne CLI włącza trzy wymagane flagi przez `--quality`. Domyślnie wszystkie pozostają OFF. Bez merge.
