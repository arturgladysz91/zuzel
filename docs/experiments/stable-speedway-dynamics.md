# #88 — Stable Speedway Bike Dynamics

Baza zweryfikowana lokalnie i na GitHub: Draft #87, `6e6f584c7d07f5b976b1565a8f7ba1de791c44f6`. Nie zmieniamy wcześniejszego PR ani produkcyjnego CoreSim. Parametry napędu, startu, opon, yaw, progi skutków i cztery wewnętrzne substeps są punktem odniesienia.

## Diagnoza przed zmianą regulatora

Dodano możliwość uruchomienia jednego rzeczywistego ciała oraz jawne komendy zapisane przed startem. To wejścia eksperymentu do istniejącego WorldRunner, nie alternatywna fizyka. Zaplanowane komendy i reaktywna AI są wzajemnie wykluczone. Dla pełnego biegu czterech zawodników rozszerzony harness zachowuje dokładny binarny hash #87 przy wszystkich trzech krokach.

Próby: A — stały gaz na prostej przez 2,4 s; B — 1,5 s łuku od referencji 45 m, offset/target 3 m, osobno 18 m/s i dotychczasowa settled capability; C — pełne okrążenie jednego ciała, identyczne odjęcie gazu do 0,9 w 8,013 s i powrót w 8,347 s; D — oryginalny pełny bieg czterech, stałe komendy oraz rzeczywista AI #87; dodatkowo niezmieniony G-dense-stress. Porównujemy zaakceptowane stany co 20 ms, bez interpolacji runtime.

Pierwsze przekroczenie diagnostycznego progu 1 cm / 0,05 m/s / 0,1° na prostej A następuje w **0,80 s** dla 20 względem 5 ms; dla 10 ms w 1,84 s. Po 2,4 s różnice 20/10 względem 5 ms wynoszą 0,04331/0,01443 m, 0,01089/0,00360 m/s, przy praktycznie zerowym yaw i błędzie linii. W przypiętym Box2D prędkość jest aktualizowana przed pozycją w każdym z czterech substeps. Dla stałej siły daje to `x_next=x+v*h+(n+1)/(2*n)*a*h²`; przy n=4 współczynnik to 0,625 zamiast 0,5. Siła sterownika pozostaje stała przez całe wywołanie świata. To zwykły błąd dyskretyzacji, a nie niestabilny rozruch naprawiony w #87.

Największe narastanie błędu okrążenia C występuje już w pierwszym TurnEntry, około **3,70 s**. Przy t=3,00 s: offset 3,00758/3,00063 m, prędkość na zewnątrz 0,28494/0,02972 m/s, żądanie boczne/budżet 1,06562/1,01229 dla 20/5 ms. Wzdłużna korekta wynosi wtedy -2,50823/-1,36094 m/s². Przy 20 ms krok rozpoczynający się jeszcze na prostej przenosi ciało w łuk bez siły dośrodkowej. Korekta przy skończonej przyczepności wzmacnia tę małą różnicę wejścia w trwałą utratę prędkości. W 3,70 s prędkości to 21,6454/22,1820 m/s. Nie jest to nowy błąd bazy sił ani budżetu z #87.

Na izolowanym łuku różnice pozostają małe i maleją około trzykrotnie przy przejściu 20→10 ms względem 5 ms. Przy 18 m/s maksymalny błąd to 0,05238/0,01745 m, 0,05438/0,01810 m/s, 0,10507/0,03501°. Głównym wzmacniaczem długiego przejazdu jest więc przejście krzywizny, nie rosnące oscylacje na ustalonym łuku.

Komendy C aktywują się dokładnie w 8,013 i 8,347 s przy wszystkich krokach. W D rzeczywista AI nie widzi zagrożeń i nie wydaje komend: trajektoria jest dokładnie równa stałemu harmonogramowi. Rozjazd D nie pochodzi od AI ani deadline. Dense zachowuje zgodne czasy/rodzaje reakcji, ale jego docelowe offsety różnią się nieznacznie wskutek wcześniejszego ruchu. Osobno raportujemy te wartości.

## Najmniejsza zastosowana poprawka

WorldRunner ogranicza istniejący wspólny krok przy najbliższym wejściu/wyjściu łuku. Czas do zmiany krzywizny szacuje wyłącznie z aktualnego zaakceptowanego stanu i już obliczonej siły. Na prostej rozwiązuje `d=v*t+beta*a*t²`, gdzie beta wynika z istniejących substeps Box2D; na łuku używa pozostałej długości łuku i bieżącej składowej stycznej. Jest to oszacowanie momentu ponownego odczytu sił, nie własny integrator ruchu. Po kontakcie tylko rzeczywisty stan Box2D staje się następnym wejściem. Nie ma replay, nowego solvera ani nadpisywania pozycji/prędkości. Wszystkie ciała nadal przechodzą ten sam najkrótszy odcinek czasu.

Rozdzielczość zdarzenia krzywizny to 1 µs, zaokrąglana w górę, aby przekroczyć granicę zamiast ścigać ulp współrzędnych float. Nie jest to tolerancja kontaktu #55. Projekcja TrackFrame sumuje kwadraty odległości w double: float gubił submilimetrowy postęp przy odległości poprzecznej wielu metrów. Test sprawdza poprawne rozpoznanie strony granicy przy ±0,5 mm na trzech różnych offsetach. Nie zmieniono geometrii toru, promieni ani wymiarów motocykli.

Nie zmieniono ani jednej linii RiderController: napęd, opór, siła dośrodkowa, yaw, tłumienie, ograniczenie przyczepności i reakcja na impuls pozostają dokładnie z #87. Zmienia się chwila ich ponownego obliczenia przy zmianie geometrii. Normalny krok 5/10/20 ms i cztery wewnętrzne substeps pozostają. W głównym runtime poprawka ruchu ma 40 dodanych linii i jedną zmienioną; osobne wejścia/obserwacje służą wyłącznie eksperymentowi.

## Zbieżność przed → po, Release

Maksima na **wspólnych zaakceptowanych chwilach co 20 ms**, względem przebiegu 5 ms tej samej wersji. Zero w wierszu 5 ms oznacza odniesienie do siebie, nie dowód bezbłędności. Kierunek to kąt ciała, prędkość to różnica całych wektorów. Kolumna czasu to maksymalna różnica indywidualnego finish time; dla prób o stałej długości jest pusta. Przebiegi A/B są po poprawce bitowo takie same jak wcześniej. D-scripted i D-AI mają identyczne trajektorie, dlatego tabela grupuje oba warianty; CSV zachowuje osobne wiersze.

| Próba | Krok ms | Pozycja m, przed → po | Prędkość m/s, przed → po | Kierunek °, przed → po | Finish s, przed → po |
| --- | ---: | ---: | ---: | ---: | ---: |
| A prosta | 20 | 0,04331 → 0,04331 | 0,01089 → 0,01089 | <0,000001 → <0,000001 | — |
| A prosta | 10 | 0,01443 → 0,01443 | 0,00360 → 0,00360 | <0,000001 → <0,000001 | — |
| A prosta | 5 | 0 → 0 | 0 → 0 | 0 → 0 | — |
| B łuk 18 m/s | 20 | 0,05238 → 0,05238 | 0,05438 → 0,05438 | 0,10507 → 0,10507 | — |
| B łuk 18 m/s | 10 | 0,01745 → 0,01745 | 0,01810 → 0,01810 | 0,03501 → 0,03501 | — |
| B łuk 18 m/s | 5 | 0 → 0 | 0 → 0 | 0 → 0 | — |
| B łuk capability | 20 | 0,03532 → 0,03532 | 0,00728 → 0,00728 | 0,10516 → 0,10516 | — |
| B łuk capability | 10 | 0,01178 → 0,01178 | 0,00239 → 0,00239 | 0,03510 → 0,03510 | — |
| B łuk capability | 5 | 0 → 0 | 0 → 0 | 0 → 0 | — |
| C okrążenie | 20 | 2,58941 → 0,19407 | 1,58672 → 0,09532 | 4,01284 → 0,30132 | 0,10847 → 0,00809 |
| C okrążenie | 10 | 0,83039 → 0,07859 | 0,51413 → 0,04357 | 1,29944 → 0,12230 | 0,03468 → 0,00328 |
| C okrążenie | 5 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| D pełny bieg, oba warianty | 20 | 8,04316 → 0,62609 | 4,88560 → 0,35857 | 12,41485 → 1,01154 | 0,33320 → 0,02481 |
| D pełny bieg, oba warianty | 10 | 2,34514 → 0,25962 | 1,34651 → 0,15338 | 3,15413 → 0,41811 | 0,09181 → 0,01053 |
| D pełny bieg, oba warianty | 5 | 0 → 0 | 0 → 0 | 0 → 0 | 0 → 0 |
| G-dense-stress | 20 | 1,49700 → 0,75548 | 1,09839 → 0,44104 | 2,15663 → 0,94127 | — |
| G-dense-stress | 10 | 0,43996 → 0,25429 | 0,32358 → 0,15221 | 0,59990 → 0,31633 | — |
| G-dense-stress | 5 | 0 → 0 | 0 → 0 | 0 → 0 | — |

Także błąd poślizgu maleje: C/20 ms 3,9854→0,3494°, D/20 ms 8,6042→0,9566°, dense/20 ms 2,4753→0,9775°. Maksymalne użycie budżetu we wszystkich próbach to 1,00000024, czyli zaokrąglenie float; wcześniej 1,00000012. Nie przybyło fizycznej przyczepności. Maksymalne odchylenie od zadanej linii w C dla 20/10/5 ms: **0,09825/0,04416/0,02249 → 0,04338/0,02174/0,01096 m**; D: **0,09918/0,04773/0,02564 → 0,04431/0,02218/0,01115 m**. Poprawa czasu nie ukrywa pogorszenia kierunku, prędkości, poślizgu ani linii.

Największe narastanie błędu C po naprawie występuje około 2,70 s jeszcze na prostej podczas przygotowania do łuku, zamiast 3,70 s na TurnEntry. W D/20 ms to 2,62 s na prostej zamiast kolejnego TurnEntry w 26,04 s. Odcinki i tempo narastania są w plikach sections. Nie deklarujemy asymptotycznego rzędu zbieżności z samych trzech kroków ani bitowej zgodności między krokami.

## Prosta i łuk — naturalny pozostały błąd

W A stały gaz przez 2,4 s daje poniższe **identyczne przed i po** wyniki. Czas reakcji pozostaje 0,24 s; nie ma zmiany linii ani rywali. Przyspieszenie w ostatnim wspólnym przedziale 20 ms pochodzi z rzeczywistej zmiany prędkości, nie z zadanej krzywej.

| Krok ms | Droga m | Końcowa prędkość m/s | Średnie przyspieszenie w 2,38–2,40 s, m/s² |
| --- | ---: | ---: | ---: |
| 20 | 22,619434 | 20,467770 | 8,23785 |
| 10 | 22,590553 | 20,460476 | 8,22790 |
| 5 | 22,576120 | 20,456880 | 8,22330 |

Brak bocznego rozjazdu (>1 µm), yaw praktycznie zerowy, użycie budżetu 0,673284. Oddzielny test samego ContactAdapter potwierdza dokładnie współczynnik 0,625 dla stałej siły i czterech substeps. Nie dodajemy korekty wyniku, aby usuwać naturalną różnicę drogi.

W B promień docelowy wynosi 34 m. Przy 18 m/s maksymalne odchylenie promienia to 0,02427/0,01216/0,00609 m; na capability 0,03713/0,01855/0,00931 m. Maleje wraz z krokiem, nie ma rosnącego znoszenia lub rosnących oscylacji. Kierunek i poślizg między krokami różnią się maksymalnie około 0,11°. To zgodność numeryczna, nie walidacja realnego kąta poślizgu motocykla.

W neutralnej pierwszej połowie łuku B/18 m/s napęd netto jest zerowy, a prędkość po 1,5 s wynosi 18,06997/18,03491/18,01742 m/s. Mały dodatni dryf energii pochodzi z trzymania kierunku siły między obliczeniami i dyskretyzacji Box2D. Maleje około dwukrotnie z krokiem; nie usuwamy go ukrytym ujemnym napędem ani nowym integratorem. To jawna granica dokładności tej prostej architektury.

## Rzeczywiste czasy ukończenia

| Próba / krok ms | R1 przed → po, s | R4 przed → po, s |
| --- | ---: | ---: |
| C / 20 | 16,52940 → 16,40013 | — |
| C / 10 | 16,45561 → 16,40494 | — |
| C / 5 | 16,42093 → 16,40822 | — |
| D / 20 | 60,66931 → 60,46265 | 63,65840 → 63,22498 |
| D / 10 | 60,61000 → 60,47707 | 63,43915 → 63,23928 |
| D / 5 | 60,56210 → 60,48745 | 63,34733 → 63,24942 |

Wszystkie cztery kończą w kolejności R1–R4, bez kontaktów, z dodatnim zapasem do krawędzi. Finish nadal korzysta z dotychczasowej interpolacji między rzeczywistymi stanami kroku. Nie jest korektą ruchu ani wiedzą AI. Maksima trajektorii D obejmują wspólne chwile świata do ukończenia najwolniejszego zawodnika, tak jak metryki #87; szybsze ciała nadal istnieją w świecie po własnym finish.

## Kontakty — tylko niezbędna regresja

Dwanaście przejazdów C-light, C-support, D-bend i niezmienionego dense przy 20/10/5 ms. Lekki kontakt: jeden Brush i dalsza jazda. Podparcie: jeden epizod i jeden skutek mimo 72/141/283 punktów solvera. D-bend: dwa rzeczywiste epizody, pierwszy closing 3,89674/3,89481/3,89972 m/s i ratio 1,49307/1,49233/1,49421, powrót do Racing. Pierwsze pomiary i klasyfikacja są dokładnie takie same jak #87. Dense nadal 0/0/0 kontaktów i upadków. Maksymalne przenikanie 0,4363 mm, bez zmiany tolerancji geometrii.

Zmiana liczby późniejszych punktów podparcia jest skutkiem wcześniejszej ponownej oceny sił przy wejściu w łuk. Nie retunowano kontaktów. ContactAdapter, BeginContact, progi i rezerwy mają pusty diff. Nie rozwijano mocniejszych kolizji, upadków ani szerokiej macierzy odporności #87.

## Koszt Release

Ta sama maszyna, Windows 10.19045, .NET 8.0.30, x64, 16 logicznych CPU, TieredCompilation=0. W jednej końcowej kompilacji Release zbudowano projekty rozwiązania i zachowany kod referencyjny z commitu diagnostycznego b271986. Ma on fizykę #87 oraz te same wejścia jednoosobowe/harmonogramy co nowa wersja. Fingerprint każdego pomiaru zgadza się z przejazdem obserwacyjnym, a D-before dokładnie z zachowanym #87.

Każda próba A–D oraz dense ma pomiar po dwóch rozgrzewkach, medianę pięciu próbek po dwa wykonania, CPU i alokacje jako średnią tych dziesięciu wykonań. Pełne dane wszystkich trzech kroków są w performance.csv. Kilkumilisekundowe próby mają duży szum, a CPU może być poniżej rozdzielczości licznika Windows; wartość 0 nie oznacza zerowego kosztu.

Osobno zmierzono **wyłącznie pełny bieg** w naprzemiennym porządku przed/po, w jednym procesie, z dwiema bibliotekami Release. Dwie rozgrzewki, mediana siedmiu próbek po dwa biegi na wersję; kolejność A/B odwracana co próbkę. Mierzono konstrukcję świata, jazdę, zwykłe diagnostyki i hashing, bez rozszerzonych obserwatorów i zapisu. To podstawowe porównanie narzutu; nie powtarzano całej macierzy.

| Krok ms | Elapsed przed → po, ms | CPU przed → po, ms | Alokacje przed → po, MB |
| --- | ---: | ---: | ---: |
| 20 | 44,898 → 47,033 | 44,643 → 47,991 | 8,114 → 8,222 |
| 10 | 94,426 → 97,065 | 93,750 → 97,098 | 15,245 → 15,359 |
| 5 | 191,340 → 195,269 | 185,268 → 197,545 | 29,517 → 29,645 |

Elapsed rośnie o około **4,8/2,8/2,1%**, alokacje o **1,3/0,75/0,43%**. CPU jest średnią Process.TotalProcessorTime z grubą rozdzielczością i zależy też od obciążenia hosta; nie interpretujemy różnicy pojedynczych ms jako dokładnego procentu. W niezależnej sekwencji pomiaru D-scripted elapsed wyniósł 44,830→45,125 / 96,431→89,671 / 174,411→176,686 ms; rozrzut pokazuje, dlaczego nie ogłaszamy przyspieszenia na podstawie jednej kolejności.

Bazowe kroki pełnego biegu: **3183/6344/12670 → 3162/6324/12650** (zmienił się czas ukończenia). Wywołania świata: **3183/6344/12670 → 3232/6395/12727**. Dodatkowe lokalne: **0/0/0 → 70/71/77**, około jednego na rzeczywiste przejście każdego motocykla. Wewnętrznych substeps pozostaje cztery na wywołanie, zero kontaktów/punktów w D. Nie ma tysięcy dodatkowych iteracji, powtórnych prefiksów ani dokładnego wyszukiwania geometrii. Dystans do zmiany krzywizny pochodzi z istniejącej topologii; pierwiastek liczymy tylko, gdy granica mieści się w bieżącym kroku.

## Odpowiedzi i ocena

1. Główny wzmacniacz: siła z prostej trzymana przez część kroku po wejściu w łuk; późniejsza korekta przy skończonej przyczepności utrwala różnicę. Mały pierwszy błąd na prostej jest naturalnym błędem całkowania, nie błędem AI.
2. Dodano lokalne zakończenie wspólnego kroku przy zmianie krzywizny i dokładniejsze porównanie odległości w projekcji. Nie zmieniono stałych ani równań RiderController.
3. Prosta jest stabilna; pozostały mały błąd drogi/prędkości maleje z krokiem i zgadza się z mechanizmem integracji Box2D.
4. Ustalony łuk zachowuje mały błąd malejący z krokiem. Przejścia są wyraźnie stabilniejsze; równocześnie maleją odchylenie linii, yaw, poślizg i różnica prędkości.
5. Pełny bieg: błąd ukończenia spadł z 0,33320/0,09181 do 0,02481/0,01053 s dla 20/10 względem 5 ms; błąd pozycji z 8,043/2,345 do 0,626/0,260 m. Zmniejszenie kroku nadal poprawia zgodność.
6. Poprawka ma zmierzony narzut elapsed około 2–5% i niewielki wzrost alokacji. Nie deklarujemy zerowego kosztu.

**B — CZĘŚCIOWO względem pełnego kryterium A.** Poprawa dynamiki i wszystkich głównych miar zbieżności jest wyraźna, bez pogorszenia geometrii i kontaktów. Jedynym niespełnionym warunkiem kategorii A jest całkowity brak pogorszenia wydajności: zmierzony narzut jest mały, ale dodatni. Pozostawiamy go jawnie do oceny zamiast ukrywać w szumie. Zwykły błąd trzymanej siły na ustalonym łuku pozostaje udokumentowany i maleje z krokiem; nie uzasadnia teraz nowej fizyki.

## Walidacja i odtworzenie

Jedna końcowa kompilacja Release, 0 ostrzeżeń/błędów. **40/40** celowanych testów (32 wcześniejsze + 8 nowych). 21 prób porównawczych na wersję; w Release potwierdzono wcześniejszą diagnozę Debug. Dwanaście celowanych regresji kontaktu. Guard: 39 zamrożonych konsumentów bez zmian. Diff src, Scenarios.cs, RiderController, ContactAdapter oraz workflow względem #87 jest pusty. Bez zmian starego silnika, HeatSimulator, kalibracji, historii, domyślnych trybów, pakietów i wcześniejszych PR. Zero nowego RNG/wear. Standardowe Windows CI na nowym Draft, bez merge.

CLI: `dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll dynamics dynamics-results` oraz `dynamics-benchmark dynamics-results`. Wersja przed zmianą jest w b271986, wersja po w HEAD tego PR. Dla odtworzenia porównania zbudować obie wersje tym samym SDK/Release i uruchomić z DOTNET_TieredCompilation=0. `dotnet test tests/CoreSim.Tests -c Release --no-build --filter "FullyQualifiedName~SharedWorld"` uruchamia celowany zestaw. Harmonogramy zapisano w DynamicsAudit.Cases; nie pochodzą z przyszłego przebiegu AI.

Dane: [przed](stable-speedway-before-comparison.csv), [po](stable-speedway-after-comparison.csv), [stany końcowe i czasy](stable-speedway-runs.csv), [odcinki przed](stable-speedway-before-sections.csv), [odcinki po](stable-speedway-after-sections.csv), [okno przed](stable-speedway-before-windows.csv), [okno po](stable-speedway-after-windows.csv), [komendy przed](stable-speedway-before-commands.csv), [komendy po](stable-speedway-after-commands.csv), [wszystkie pomiary](stable-speedway-performance.csv), [naprzemienny pełny bieg](stable-speedway-paired-performance.csv), [kontakty](stable-speedway-contact-regressions.csv).
