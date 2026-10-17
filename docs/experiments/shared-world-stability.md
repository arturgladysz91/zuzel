# Stabilizacja fizyki i kontaktów #87

Punkt odniesienia: `ca5339bc2bdd370d9e82ad889b76d8929014adcc`; HEAD Draft #87 zweryfikowany przed pracą. Oryginalne wejście G-dense-stress pozostaje niezmienione. Nie dodajemy ścian, fizyki leżącego motocykla ani nowej taktyki.

Pierwsza istotna różnica występuje **A: przed kontaktem, w 0,26 s**. Kryterium diagnostyczne: różnica prędkości >0,05 m/s lub położenia >0,01 m, na wspólnych fizycznych chwilach co 20 ms. Do 0,24 s wszystkie ciała stoją identycznie. Nie porównujemy indeksów kroków ani nie używamy interpolacji jako obserwacji w runtime.

| R1, czas 0,26 s | 20 ms | 10 ms | 5 ms |
| --- | ---: | ---: | ---: |
| vx (m/s) | 0 | 0,097937 | 0,113865 |
| vy (m/s) | 0 | -0,042604 | -0,104888 |
| x (m) | 0 | 0,000612 | 0,001028 |
| żądana siła boczna na początku następnego kroku (N) | -604,971 | -586,267 | -558,896 |
| faktyczna składowa siły świata Y (N) | -604,970 | -1092,357 | -1353,303 |

**Mechanizm 1 — deadline startu.** TapeReactionSeconds=0,240000002846… s jest sprawdzany dopiero na granicy kroku. Start napędu wypada w 0,260/0,250/0,245 s. Zmiana kroku zmienia więc fizyczne wejście regulatora jeszcze przed jazdą.

**Mechanizm 2 — niespójna baza sił.** Błąd i prędkość poprzeczna pochodzą z osi toru, ale napęd i siła boczna działają w bazie znormalizowanej rzeczywistej prędkości. Przy rozruchu z zerowej prędkości pierwszy impuls regulatora ustala tę bazę. Napęd obraca się następnie za wywołanym dryfem. W 5 ms, przy żądaniu bocznym -558,896 N, faktyczne Fy wynosi -1353,303 N. Yaw również podąża za wektorem prędkości, więc dostaje obrót wytworzony tym sprzężeniem. To nie jest różnica wywołana zderzeniem ani progiem Crash.

**Mechanizm 3 — niepełne ograniczenie siły.** Ograniczono tylko komponent boczny, a napęd dodano niezależnie. R4/5 ms w 0,9 s otrzymuje moduł siły równy **1,2033** dostępnego budżetu. Potrzebne jest wspólne ograniczenie wektora, bez zwiększania przyczepności.

Pierwszy kontakt R1/R2 występuje dopiero w 0,915/0,7975/0,7375 s. Prędkość zamykania w jego punkcie wynosi 1,2688/2,4283/3,3415 m/s. W telemetrycznej rekonstrukcji `v + omega cross r` zgadza się z `manifold.normalVelocity` (różnice pojedynczej precyzji), więc duża wspólna prędkość postępowa nie jest błędnie użyta jako severity. Miara bierze jednak wyłącznie maksimum z pierwszego aktywnego kroku epizodu; jej adekwatność do skończonego uderzenia trzeba zbadać po usunięciu wcześniejszego źródła różnicy. Nie zmieniamy progów skutków.

Dodano opcjonalną obserwację sił, punktów styku i rezerw. Dla wszystkich trzech kroków pełny hash zaakceptowanych stanów z obserwacją jest dokładnie równy zachowanemu DLL z ca5339b. Krótkie okna: 0,24–0,42 s dla pierwszej rozbieżności i 0,70–1,08 s dla pierwszych styków. Dane: `shared-world-stability-before-*.csv`. Nie zapisujemy całej historii wewnętrznych kroków biegu.

Odtworzenie diagnozy z commitu `690c5ee`: `dotnet run --project tools/shared-world-speedway -c Debug -- diagnose stability-diagnosis`. Na późniejszym HEAD ta sama komenda pokazuje ruch po naprawie. Powyższe liczby i zamrożone pliki before odnoszą się do wersji przed naprawą.

## Minimalna naprawa sterowania

Napęd, regulator boczny i cel yaw używają teraz wspólnej bazy toru. Składnik dośrodkowy używa prędkości wzdłużnej w tej bazie. Impuls kontaktu nadal zmienia rzeczywistą prędkość i obrót w Box2D; regulator tłumi błąd względem toru, zamiast obracać cel za kopnięciem. Dotychczasowy kąt poślizgu 12°, czasy odpowiedzi, siły CoreSim i przyczepność pozostają bez retuningu. Ograniczenie kołowe obejmuje cały żądany wektor siły translacyjnej; nie stanowi jeszcze skalibrowanego modelu pełnego klucza sił opony i momentu yaw. Dotychczasowy opór po upadku pozostaje w kierunku rzeczywistego ruchu; nie rozwijamy fizyki upadku.

Wspólny krok kończy się na deadline reakcji, aktywacji polecenia lub odzyskania kontroli. Przed deadline nie wykonuje się przyszłego polecenia. Wszystkie ciała przechodzą ten sam odcinek czasu. Eksperymentalne deadline są kanonizowane do 1 µs, zamiast tworzyć podkroki nanosekundowe z reprezentacji stałych float. Nie zmieniono produkcyjnych czasów reakcji. Zachowano bazowe 5/10/20 ms, cztery wewnętrzne substeps i dotychczasowe lokalne dzielenie przy zbliżeniu; brak replay i nadpisywania transformacji.

Testy sprawdzają siłę przy rozruchu, wspólny budżet, deadline poza siatką, wczesny ruch bez kontaktu, malejący błąd przy zmniejszaniu kroku, odwrócenie wejścia, rzeczywiste rozdzielenie przed ponownym epizodem i kontekst wyznaczany przez postęp na torze. Dwa zestawy testów Box2D należą do jednej nieparalelnej kolekcji: przypięty port ma globalny rejestr światów, którego tworzenie i niszczenie przez różne klasy xUnit nie może się ścigać.

## Wynik 36 celowanych przejazdów

Dane dotyczą kolejno **20/10/5 ms**. Scenarios.cs, w tym wejście G-dense-stress, jest identyczny bajtowo z ca5339b. Warianty są jawne w StabilityAudit.Cases i nie zastępują oryginału.

| Próba | Kontakty / wynik | Zgodność fizyczna |
| --- | --- | --- |
| A — pełne 4 okrążenia, 4 zawodników | 0/0/0; wszyscy kończą, kolejność R1–R4 | Czasy R1 60,6693/60,6100/60,5621 s; R4 63,6584/63,4391/63,3473 s. Vmax około 25,82 m/s. Zapas do krawędzi >0,98 m. |
| B — C-light | 1/1/1, Brush, dalsza jazda | Pierwszy kontakt 0,36/0,34/0,34 s; closing 0,05469/0,05835/0,05744 m/s; ratio 0,02095/0,02236/0,02201. |
| Trwający C-support | 1/1/1 skutek mimo 68/136/270 rozwiązanych punktów | Kontakt w 0,22 s; closing 0,17140/0,16997/0,16926 m/s. Nacisk nie staje się serią skutków. |
| C — D-bend | 2/2/2, MajorSave, korekta i powrót do Racing | Kontakt w 0,0400/0,0400/0,0375 s; closing 3,89674/3,89481/3,89972 m/s; ratio 1,49307/1,49233/1,49421. |
| D — oryginalny G-dense-stress | **0/0/0 kontaktów i upadków**, 12 s jazdy | Najmniejsza separacja kapsuł 0,6425/0,6474/0,6496 m. Istniejąca reakcja AI otwiera korytarze; czasy i rodzaje decyzji są zgodne między krokami. |
| E — rozstaw ×0,98 i ×1,02 wokół offsetu 4 m | Każdy wariant 0/0/0 kontaktów i upadków | Profile, cele i pozostałe wejścia oryginalne; brak dostrajania pary. |
| E — początkowa prędkość poprzeczna ±0,05 m/s naprzemiennie według ID; drugi wariant odwraca znaki | Każdy wariant 0/0/0 kontaktów i upadków | Prędkość wzdłużna pozostaje 0, aby nadal badać rozruch spod taśmy. To mała zmiana wektora prędkości, nie nowy Vmax. |
| Kontrola: dense bez reakcji na rywali | 9/7/6 odrębnych epizodów, 0 upadków | Kontakt w 1,420/1,420/1,425 s; closing 1,00225/1,00499/0,99903 m/s; ratio 0,38402/0,38507/0,38279. Kontakt nie został wyłączony. |
| Kontrola progu: D-bend, prędkość poprzeczna R1 3,8 zamiast 4 m/s | MajorSave przy każdym kroku | Ratio 1,41756, bez upadku. |
| Kontrola progu: D-bend, prędkość poprzeczna R1 4,2 m/s | Crash obu przy każdym kroku | Closing 4,10833/4,10451/4,09971 m/s; ratio 1,57415/1,57269/1,57084. Próg nadal 1,55. |

Maksymalny moduł siły/budżet we wszystkich próbach: **1,00000012**, czyli zaokrąglenie float, wobec 1,2033 w pierwotnym krótkim oknie. Maksymalne przenikanie niezależnej geometrii kapsuł: **0,4363 mm**, bez zmiany tolerancji #55 i bez drugiego solvera. To kontrola zaakceptowanych stanów z dotychczasowym lokalnym dzieleniem, nie nowy formalny certyfikat ciągłej trajektorii. Zachowane testy szybkiego przecięcia dwóch ciał nadal wykrywają tunneling bez dzielenia i jego brak w używanym trybie.

Konteksty wynikają z rzeczywistego postępu: StartStraight do 35 m referencji, FirstBend na pierwszym fizycznym łuku, następnie NormalRacing; kolejne okrążenie nie przywraca FirstBend. R2/20 ms wchodzi w FirstBend w 2,98 s przy 35,3326 m i w NormalRacing w 7,80 s przy 132,7090 m. Nie powstały odrębne siły ani taktyki dla kontekstów.

## Severity i skończone uderzenie

Zbadano punkty z dodatnim impulsem w 40 ms od pierwszego uderzenia. W C-light, C-support, D-bend, wymuszonym dense i obu wariantach progu maksimum closing występuje na początku. Dla D-bend pierwszy raportowany impuls wynosi **126,623/126,726/19,735 Ns**, mimo zgodnej prędkości zamykania około 3,90 m/s. Przy 5 ms uderzenie przechodzi przez więcej wywołań świata. Zastąpienie closing pierwszym częściowym impulsem pogorszyłoby klasyfikację. Zbadane wyniki nie uzasadniają nowego agregatora ani zmiany progów.

Źródło prędkości to solverowy punkt kontaktu wraz z obrotem obu ciał. Niezależna rekonstrukcja `(vB + omegaB cross rB - vA - omegaA cross rA) dot normal` i test pierwszego uderzenia potwierdzają pomiar. Wspólna prędkość 20 m/s nie staje się ciężkością kontaktu. `totalNormalImpulse` zawiera warm start/podparcie wewnętrznych substeps; nie sumujemy go jako serii niezależnych uderzeń i nie wstrzykujemy drugiego impulsu.

W D-bend różnica ratio między krokami wynosi 0,00188, w mocniejszym wariancie 0,00330. Dla równych mas i rezerwy 1,30494 m/s krytyczna prędkość punktu to około **4,0453 m/s**. Mocniejszy wariant jest około 0,054–0,063 m/s powyżej progu; różnica pomiaru jest dużo mniejsza od marginesu. To dowód fizycznego przekroczenia progu, nie sama zgodność etykiety Crash. Bezpośrednio na progu nie obiecujemy identycznej klasy.

Powtórny epizod wymaga braku interakcji solvera i rozdzielenia całych kapsuł >2 cm. Dense bez AI potwierdza dla 20/10/5 ms minimalny gap przy zwolnieniu **0,0202210/0,0200256/0,0200632 m** i 8/7/6 zwolnień. Żaden epizod nie ma podwójnego begin; liczba skutków równa liczbie epizodów. Późniejsza liczba ponownych kontaktów pozostaje wrażliwa na przebieg i granicę zwolnienia. Nie nazywamy jej zbieżną. Severity nadal jest prowizoryczną miarą jednego epizodu, bez pełnej agregacji wieloetapowego/wieloparowego uderzenia. Nie wykazano tu odporności takiego ogólnego modelu.

## Pozostały błąd kroku

Do 1,2 s w oryginalnym dense różnica pozycji 20–5 ms jest <3 cm i prędkości <0,03 m/s. Wymuszony dense zaczyna kontakt przy pre-contact maksymalnej różnicy pozycji **0,01531/0,00865 m** dla 20/10 względem 5 ms. Zniknęło wczesne samowzmacniające obracanie napędu.

Nie ma zgodności trajektorii do centymetrów przez cały bieg. W dense po 12 s maksymalne różnice na wspólnych fizycznych chwilach 20/10 względem 5 ms: **1,4970/0,4400 m**, **1,0984/0,3236 m/s**, **0,03764/0,01047 rad**. Po pełnym bezkontaktowym biegu: **8,0432/2,3451 m**, różnica finish time **0,33320/0,09181 s**. Kolejność zgodna. To głównie narastająca faza przejazdu przez łuki/przejścia oraz ograniczenie sił przy korekcie. Zmniejszanie kroku istotnie zmniejsza błąd, lecz trzy próby nie dowodzą asymptotycznej zbieżności. Nie ogłaszamy równoważności 20 i 5 ms ani gotowości produkcyjnej na podstawie braku Crash.

Próba centrowania kierunku siły na połowie kroku nie poprawiła błędu i nie weszła do zmiany. Nie zwiększono globalnej częstotliwości ani nie rozbudowano integratora. Przed uznaniem 20 ms za referencyjny krok pozostaje osobna, ograniczona ocena dyskretyzacji sterowania na wejściach/wyjściach łuku.

## Wydajność na tej samej maszynie

Windows 10.19045, .NET 8.0.30, 16 logicznych CPU, TieredCompilation=0; ta sama maszyna co poprzedni raport. Zachowano DLL z ca5339b. Ten sam caller uruchomiono z kompletem oryginalnych i nowych bibliotek Release. Konstrukcja świata, symulacja, zwykłe kontrole i hashing wchodzą w koszt; zapis CSV i rozszerzeni obserwatorzy nie. Dwie rozgrzewki, mediana 7 próbek po 2 wykonania. CPU i alokacje: średnia tych 14 wykonań. CPU Process.TotalProcessorTime ma grubą rozdzielczość Windows; szczególnie 1–3 ms nie uzasadnia porównania procentowego.

| Scenariusz / krok | elapsed przed → po, ms | CPU przed → po, ms | alokacje przed → po, MB |
| --- | ---: | ---: | ---: |
| Pełny bieg / 20 ms | 43,166 → 43,924 | 43,527 → 43,527 | 8,089 → 8,113 |
| Pełny bieg / 10 ms | 85,220 → 87,669 | 84,821 → 87,054 | 15,230 → 15,245 |
| Dense / 20 ms | 11,883 → 10,019 | 12,277 → 10,045 | 2,841 → 2,416 |
| Dense / 10 ms | 22,208 → 17,319 | 22,321 → 16,741 | 4,745 → 3,672 |
| Support / 20 ms | 1,465 → 1,494 | 1,116 → 1,116 | 0,824 → 0,825 |
| Support / 10 ms | 2,695 → 2,716 | 2,232 → 3,348 | 1,056 → 1,056 |

Pełny bieg ma **3173→3183 / 6338→6344** bazowych i wykonanych kroków świata; 0 dodatkowych lokalnych kroków, 0 kontaktów i punktów. Zmienił się czas ukończenia, nie częstotliwość. Wewnętrznych substeps: **12692→12732 / 25352→25376**. Elapsed wzrósł około 1,8/2,9%, nadal w rzędzie pierwotnych 44/88 ms.

Dense: bazowe kroki **600/1200/2400**; wywołania świata **869→731 / 1548→1276 / 2757→2452**; lokalne dodatkowe **269→131 / 348→76 / 357→52**; wewnętrzne substeps **3476→2924 / 6192→5104 / 11028→9808**; rozwiązane punkty **4→0 / 392→0 / 7→0**. Mniej gwałtownego względnego ruchu oznacza mniej podziałów. Diagnostyki końcowych kapsuł **927→532 / 1802→641 / 1604→1015**; brak milionów wyszukiwań. Support: 100/200 bazowych, 108/200 wywołań świata, 8/0 lokalnych dodatkowych, 432/800 wewnętrznych, 87/175 manifold, 69→68 / 139→136 punktów; nadal jeden skutek. Nie przeliczano zaakceptowanych prefiksów.

## Walidacja i zakres

Jedna końcowa kompilacja rozwiązania Release, 0 ostrzeżeń i błędów. **216/216** celowanych testów Release: 32 prototypu (20 dotychczasowych + 12 nowych) oraz istniejące testy sił i severity. 36 celowanych przejazdów; nie powtarzano starej szerokiej macierzy ani historycznych benchmarków. Wszystkie 12 wejść przy 20 ms ma dokładnie równe hashe po powtórzeniu i odwróceniu kolekcji względem przejazdu walidacyjnego. Osobno sprawdzono rearm dla trzech kroków.

Guard: 39 niezmienionych produkcyjnych konsumentów. Diff src, Scenarios.cs i workflow względem ca5339b pusty. Bez zmian CoreSim, HeatSimulator, kalibracji, historii, trybów domyślnych, wymiarów motocykla, progu Crash i rezerw. Zero nowego RNG i zużycia. Ten sam Draft #87, bez merge. Standardowe Windows CI sprawdza końcowy HEAD; wynik w checks PR.

Usunięto potwierdzoną niestabilność rozruchu i naruszenie budżetu. Pozostają: błąd fazy 20 ms na długim przejeździe, ogólna miara wieloetapowego/wieloparowego uderzenia i niezależna walidacja opon/yaw. Mocny wariant po Crash opuszcza tor w dotychczasowym modelu ruchu po upadku; to znane ograniczenie, nie zaliczony bezpieczny bieg. Ściany i fizyka leżącego motocykla pozostają poza zakresem zadania.

Odtworzenie: `dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror`, `dotnet test tests/CoreSim.Tests -c Release --no-build --filter "FullyQualifiedName~SharedWorld"`, `dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll stability stability-results`. CLI diagnose zapisuje krótkie okna; stability zapisuje podsumowania 36 przejazdów, wspólny czas, konteksty i 40 ms pierwszego uderzenia. Nie uruchamia starego audytu ani benchmarków.

Dane: [scenariusze](shared-world-stability-scenarios.csv), [różnice czasu fizycznego](shared-world-stability-comparison.csv), [epizody](shared-world-stability-contacts.csv), [pierwsze 40 ms](shared-world-stability-impact-points.csv), [budżety](shared-world-stability-force-budgets.csv), [konteksty](shared-world-stability-contexts.csv), [wydajność](shared-world-stability-performance.csv), [deterministyczność i rozdzielenie](shared-world-stability-checks.csv), [stany po naprawie](shared-world-stability-after-states.csv), [siły po naprawie](shared-world-stability-after-forces.csv). Pliki before oraz pierwotny raport #87 pozostają dowodem historycznym.
