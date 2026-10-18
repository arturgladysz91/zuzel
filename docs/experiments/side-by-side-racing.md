# #90 — Side-by-Side Racing: First-Bend Tactical Contests

**A — DZIAŁA w zakresie tego eksperymentu.** Równoległa jazda uruchamia samodzielne decyzje, a ich wykonany wpływ potwierdzono dla dwóch motocykli i dla czterech rzeczywistych pól Motoareny ze zróżnicowanymi profilami. Nie oznacza to, że każda wybrana linia jest lepsza: R1 w F początkowo traci postęp przez rzeczywisty koszt zacieśniania. Nie gwarantujemy zwycięstwa ani gotowej kalibracji taktyki.

Baza: zweryfikowany lokalnie i na GitHub Draft #89, `experiment/tactical-racing-overtake-defense`, HEAD `5b9f3274b3eb27100e8b964b422317bbe2734836`. Nowa gałąź `experiment/side-by-side-first-bend-contests`, jeden Draft, bez merge. Status mechanizmu: **PROVISIONAL**. Nie zmieniono produkcyjnego HeatSimulator/CoreSim, fizyki, regulatora napędu/ruchu, poprawki krzywizny #88, opon, geometrii, progów skutków, substeps, nawierzchni ani pogody.

## 1. Dlaczego #89 nie wykrywał walki z pól

Detektor wymagał poprzedzającego motocykla dalej niż długość motocykla, dodatniej różnicy prędkości liniowych >0,25 m/s i czasu dogonienia w horyzoncie zależnym od Attack. Wybierał najbliższego dodatniego rywala w rzucie na własną styczną. Mały odstęp przy jeździe obok nie przechodził pierwszego warunku. Gdy odstęp już rósł, nie musiał oznaczać dostatecznie szybkiego doganiania.

Obserwacja niezmienionego F #89, 10 ms, 12 s, przed nowymi decyzjami:

| Odczyty bliskich par, kierunkowo z perspektywy zawodnika | Liczba |
| --- | ---: |
| odległość rzeczywista ≤8 m | 1214 |
| równocześnie różnica TotalReferenceMeters ≤2,1 m | 764 |
| rzeczywiście wybrany najbliższy rywal >2,1 m z przodu | 115 |
| z powyższych dodatnie zamykanie >0,25 m/s | 115 |
| z powyższych spełniony horyzont dogonienia | 0 |
| jeden na łuku, drugi na prostej | 26 |
| własny motocykl na łuku, rywal na prostej | 13 |

To odczyty przy AI co 100 ms, nie wewnętrzne substeps. 764 nie oznacza 764 niezależnych pojedynków. W całym oryginalnym przebiegu było zero prób korytarzy i zero komend; hash `BB7DF2…49B6` pozostaje zgodny z #89. W omawianej próbce bliskich rywali selekcja kończyła się przed oceną korytarza. Nie odpowiadały za to czas reakcji ani kolejka — nie było czego aktywować. Brak reakcji bezpieczeństwa również nie był główną przyczyną.

Nie każde takie pominięcie oznacza straconą okazję. W nowym odczycie E w 3,00 s dla R1:

- utrzymanie aktualnego sterowania: szacowane 20,6791 m referencji/s;
- wewnętrzna 0,5174 m: 20,8393, zysk 0,1602;
- wymagany margines decyzji: 0,1644 m/s; za mało na zmianę;
- zewnętrzna 2,5174 m: zajęty korytarz.

Wybór HoldLine jest wtedy uzasadniony. W 4,10 s sytuacja jest inna: wewnętrzna daje oszacowanie 20,8471 wobec 20,5711, czyli +0,2759, powyżej marginesu 0,1720; pojawia się zamiar zacieśnienia. Nie wymuszamy go w chwili wejścia w łuk.

Stary iloraz `otherAlong / front.Road.RadiusMeters` miał osobny błąd: prosty segment ma promień zero. W 13 wymienionych odczytach konstrukcja byłaby niepoprawna, gdyby doszła do niej selekcja ataku. Nie była przyczyną zerowej liczby komend F, ale stanowiła realną lukę dla innych sytuacji. Usunięto tę konstrukcję.

## 2. Wspólny postęp zamiast samych km/h

`TrackFrame.ReferenceSpeed` wyraża tempo w **metrach istniejącej wewnętrznej referencji na sekundę**:

- prosta: składowa prędkości wzdłuż własnej stycznej;
- łuk: ta składowa pomnożona przez `InnerRadius / (InnerRadius + offset)` z istniejącego segmentu metrycznego.

Nie dzielimy przez promień prostej. Obie strony granicy mają te same jednostki. `TotalReferenceMeters` daje wspólną różnicę postępu; pozycja i kapsuły nadal potwierdzają rzeczywistą bliskość. Nie dodano nowej geometrii toru ani niezależnego integratora.

Przykład B: 20 m/s przy offsecie 0,5 m daje 19,6825 m referencji/s. 21,5 m/s przy offsecie 3,5 m daje tylko 19,3188 — większa prędkość liniowa przegrywa z dłuższą drogą. W drugim wariancie 17 m/s przy 5 m daje 14,6389, a 21,5 m/s przy 6,2 m daje 17,9167: zewnętrzna rzeczywiście prowadzi. Żaden bonus nie zmienia tych wartości.

Osobne testy obejmują obie proste, oba łuki, obydwa kierunki relacji prosta/łuk i różne promienie. D ma również rzeczywiste snapshoty AI w 0,10 s, w których jeden motocykl jest jeszcze na prostej. Wszystkie 246 zapisanych ocen kandydatów mają skończone wartości; niewykonalny kandydat ma puste pole wyniku, a nie NaN/Infinity. Stara diagnostyka odległych kontaktów może mieć nieobliczone minimum poza promieniem filtra — nie jest to wynik oceny postępu.

## 3. Małe rozszerzenie tego samego TrafficDecision

Odrębne `EnableSideBySideContests` jest domyślnie false i wymaga eksperymentalnego `EnableTacticalRacing`. CLI `--side-by-side` ustawia obie flagi. Samo `--tactics` zachowuje #89. Pusta lista pobliskich par pomija nową ocenę; aktywny zamiar nadal może zostać zakończony po utracie rywala.

Jedna obserwacja co 100 ms, jeden niezmienny snapshot i najwyżej sześć różnych nieuporządkowanych par. Dla walki obok szukamy rzeczywistej odległości ≤8 m i różnicy postępu ≤2,1 m. Nowy wybór dotyczy jazdy na łuku powyżej 8 m/s. Nie traktujemy stania na polach jako powodu do manewru. Oba kierunki mieszanej granicy nadal mają poprawną wspólną miarę; zawodnik na prostej nie otrzymuje fikcyjnego promienia łuku.

Porównujemy istniejące sterowanie oraz co najwyżej trzy proste możliwości: utrzymać aktualną fizyczną linię, przesunąć target o 1 m do wewnątrz lub o 1 m na zewnątrz. Duplikat obecnego targetu jest pomijany. Tani horyzont wynika z trzech czasów odpowiedzi bocznej regulatora, z małą korektą jakości: około 1,56–1,95 s ruchu po pełnym czasie reakcji. W czasie reakcji nadal obowiązuje własna zaakceptowana komenda.

Zamknięty wzór tłumionej odpowiedzi PD szacuje dostępny ruch boczny i średnią dobrowolną korektę. Potem używamy istniejącej przyczepności, promienia i capability do oceny możliwego tempa. Jest to **przybliżenie decyzji**, nie trajektoria wykonywana przez silnik i nie gwarancja utrzymania prędkości. Początkowa próba trzymania szczytowego kosztu korekty przez cały horyzont odrzucała wykonalne, stopniowe zacieśnienie; obecna ocena uwzględnia wygaszanie odpowiedzi. Nie uruchamia Box2D do wyboru wariantu.

Korytarz najpierw respektuje faktycznie zajętą przestrzeń chassis/kierownicy. Obecnego przecięcia nie wolno zwolnić przez ekstrapolację. Na łuku przyszły odstęp wzdłużny używa wspólnej referencji; nie rzutujemy dwóch sekund zakrzywionej jazdy na jedną nieruchomą styczną. Boczna ekstrapolacja prędkości rywala jest ograniczona do 0,5 s. Nie czytamy targetu, gazu, przyszłego polecenia ani niezaakceptowanego stanu przeciwnika.

Nowe zamiary to tylko ContestInside, ContestOutside i ContestHold; używają istniejących akcji. Brak nowych pól TacticalMemory. Strona aktywnego manewru jest utrzymywana. Hold może po 0,8 s ponownie rozważyć sytuację, ale nie wysyła identycznej komendy przy każdym odczycie. Zakończenie walki zwalnia zamiar; bezpieczeństwo nadal jest sprawdzane przed tą histerezą. Wznawianie gazu zachowuje ochronę #89.

Attack i Defense zmieniają jakość/margines i horyzont wyboru zależnie od rzeczywistego położenia względem rywala. Technique pozostaje małym marginesem wykonania i dotychczasowym konsumentem fizyki #88. Combativeness zmienia planowany margines i naleganie z #89; nie usuwa motocykla przeciwnika z korytarza. Nie zmieniono skal ani dodano atrybutów. Kanoniczne Reaction nadal dotyczy taśmy. Wyścigowy czas reakcji pozostaje istniejącym proxy legacy Start — tutaj 0,232–0,244 s w F, bez sztucznego skrócenia.

Nowa kontrola `OccupiedAlongsideCut` może zatrzymać własne cięcie aktualnie zajętego korytarza, zachowując gaz i pełną reakcję. Może anulować czekającą taktykę zgodnie z #89; zastępstwo odlicza nową reakcję. Nie wymusza ustąpienia rywala. Potwierdzenie Hold bez zmiany obu aktuatorów nie dzieli kroku Box2D: jest rejestrowane na pierwszej istniejącej zaakceptowanej granicy po terminie reakcji. Dzięki temu sama etykieta zamiaru nie zmienia fizyki.

## 4. A — prawdziwy skutek decyzji dwóch motocykli

Początek przy referencji 34 m, offsety 3/6 m, prędkości 19,5/20,5 m/s. Identyczne profile w ON/OFF; legacy SlideControl 70 jest zwykłym wejściem istniejącej fizyki. OFF oznacza #89 z włączoną dotychczasową taktyką i wyłączonym wyłącznie Side-by-Side.

| Czas s | Zawodnik | Rywal | Sytuacja | Decyzja | Target m | Gaz | Aktywacja s | Faktyczny rezultat |
| ---: | --- | --- | --- | --- | ---: | ---: | ---: | --- |
| 0,10 | R1 | R2 | obok, odstęp referencji 0,012 m | ContestInside | 1,999 | 1 | 0,34 | pozycja nadal identyczna z OFF |
| 0,10 | R2 | R1 | ten sam snapshot | ContestInside | 4,999 | 1 | 0,34 | własny dostępny korytarz, bez przecinania R1 |
| 0,34 | R1/R2 | R2/R1 | upłynęła reakcja | aktywacja | jak wyżej | 1 | 0,34 | bez skoku pozycji/prędkości |
| 0,48 | R1 | — | wykonana różnica ≥2 cm | pomiar ON/OFF | 1,999 | 1 | — | offset 2,974 m |
| 0,57 | R1 | — | różnica boczna ≥5 cm | pomiar ON/OFF | 1,999 | 1 | — | offset 2,942 m |
| 2,00 | R1 | R2 | dalsza rzeczywista jazda | utrzymanie strony | 1,999 | 1 | — | +0,644 m własnego postępu vs OFF; 1,246 m przed R2 |
| 4,00 | R1 | R2 | koniec okna | pomiar | 1,999 | 1 | — | 2,829 m przewagi vs 2,686 m OFF |

Prefix na wspólnych zaakceptowanych chwilach do aktywacji jest dokładnie taki sam. Przy 5/10/20 ms decyzje zachowują ten sam sens i kolejność; trajektorie nie muszą być identyczne między krokami. Bez kontaktu i bez przekroczenia budżetu sił. Późniejsze duże maksima różnicy całego przejazdu obejmują także odmienne kolejne reakcje starej AI — nie przypisujemy ich wyłącznie pierwszej korekcie targetu.

## 5. B/C/D — kierunek, zajęta przestrzeń i granica

| Próba B | Co rzeczywiście wybrała AI | Wynik fizyczny przy 10 ms |
| --- | --- | --- |
| inner-rate: 19/20 m/s, offsety 3/6 | obaj zmniejszają własny promień, bez przecięcia | R1 kończy 1,904 m z przodu, OFF 1,727 m |
| inner-hold: 20/21,5 m/s, offsety 0,5/3,5 | R1 HoldLine przy krawędzi; R2 własna wewnętrzna | krótsza droga R1 pozostaje korzystna mimo mniejszej prędkości liniowej |
| outer-rate: 17/21,5 m/s, offsety 5/6,2 | R2 HoldLine na zewnętrznej; wejście do wewnątrz zajęte | R2 cały czas gaz 1, na końcu 8,715 m przed R1 |
| no-gain: offsety 0,5/1,65, prędkości o równym tempie referencji | żadnej nowej komendy | dokładny hash ON/OFF, zero zmian aktuatorów |

Hold zewnętrznej nie dostał bonusu i sam nie przyspieszył R2. Jego przewaga jest naturalnym wynikiem warunków początkowych; nowa decyzja rozpoznaje korzystne utrzymanie, a nie „tworzy wyprzedzenie”. Kierunek ContestOutside jest dostępny, lecz w tym zestawie uzasadniony wybór zewnętrznej polega na HoldLine. Nie dorabiamy wymuszonego manewru na zewnątrz dla samej nazwy akcji.

| Czas s | Zawodnik | Rywal | Sytuacja | Decyzja | Target m | Gaz | Aktywacja s | Faktyczny rezultat |
| ---: | --- | --- | --- | --- | ---: | ---: | ---: | --- |
| 0,10 B outer | R2 | R1 | szybszy postęp po zewnętrznej, wnętrze zajęte | HoldLine | 6,200 | 1 | 0,34 | bez zmiany aktuatorów; utrzymuje przewagę fizyczną |
| 0,10 C | R2 | R1 | dotychczasowy target 3,1 przecina przestrzeń obok | OccupiedAlongsideCut | 4,038 | 1 | 0,34 | przerywa własne cięcie, nie przestawia R1 |
| 0,34 C | R2 | R1 | pełny czas reakcji | aktywacja HoldLine | 4,038 | 1 | 0,34 | dalsza jazda bez kontaktu |
| 0,10 C late | R2 | R1 | dodatkowo już jedzie 2 m/s do wewnątrz | OccupiedAlongsideCut | własny bieżący offset | 1 | 0,34 | zastanego pędu nie kasujemy |
| 0,285 C late | R1/R2 | R2/R1 | przed terminem aktywacji | fizyczny kontakt | nadal stara komenda | 1 | — | Disturbed, closing 1,333 m/s, ratio 0,511 |
| 0,34 C late | R1/R2 | R2/R1 | reakcja po kontakcie | aktywacja | zapisane targety | 1 | 0,34 | jeden epizod i jeden skutek |
| 0,10 D inner-turn | R1 | R2 | R1 na łuku, R2 jeszcze na prostej | ContestInside | około 1,999 | 1 | 0,34 | skończona ocena obu temp |
| 0,10 D outer-turn | R2 | R1 | R2 na łuku, R1 jeszcze na prostej | ContestInside | około 4,999 | 1 | 0,34 | poprawna relacja odwrotna |
| 0,20 D | drugi zawodnik | pierwszy | drugi również wjechał na łuk | własny ContestInside | własny korytarz | 1 | 0,44 | brak przełączania strony na granicy |

W C late −2 m/s jest jawną **początkową prędkością boczną** istniejącego RiderSpec, nie siłą/bonusem dodanym po starcie. To próba spóźnionej reakcji, a nie wymuszona zmiana wyniku. Najmniejszy zmierzony prześwit wynosi −0,01035 mm; nie zwiększono tolerancji. Oryginalne Brush, MajorSave i Crash zachowują dokładne stany i strumień kontaktów również z flagą ON w swoich niezmienionych warunkach. Diagnostyka nadal zatrzymuje się na pierwszym Crash.

## 6. E/F — rzeczywiste cztery pola i wspólny czas

E to dokładnie niezmienione `Scenarios.G()`: fizyczne pola około 0,5/3,5/6,5/9,5 m, targety 1,6/3,2/5,6/8,3 m, oryginalne profile, zero początkowej prędkości, geometria i grip #89. W 3,00 s poprawne są cztery HoldLine. Pierwsza zmiana aktuatora pojawia się dopiero po nowej ocenie w 4,10 s i aktywacji w 4,34 s. Do niej całe wspólne stany ON/OFF pozostają dokładnie takie same. W pierwszym łuku maksima różnicy wynoszą 1,204 m pozycji i 0,914 m referencji; to wpływ rzeczywistej jazdy, bez kontaktu.

F zachowuje te same pola, targety, setup i początkowe prędkości. **Jeden jawny zestaw**, bez przeszukiwania siatki profili pod wymagany wynik:

| Pole | Reaction kanoniczne | Start legacy | Speed legacy | SlideControl legacy | Technique | Attack | Defense | Combativeness |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| A / R1 | 55 | 45 | 50 | 60 | 70 | 70 | 80 | 0,45 |
| B / R2 | 65 | 60 | 55 | 65 | 60 | 85 | 70 | 0,80 |
| C / R3 | 45 | 50 | 60 | 70 | 75 | 80 | 75 | 0,55 |
| D / R4 | 60 | 55 | 65 | 65 | 65 | 75 | 65 | 0,60 |

Pozostałe pola są jawne w SideBySideScenarios.F; canonical Start 50, TrackReading 65, PairRiding 50, Strength 60, masa jak w oryginale. Legacy są potrzebne, ponieważ te istniejące konsumenty nadal wyznaczają napęd/capability. Nie migrujemy ich ani nie przypisujemy nowej kanonicznej mocy do Attack/Defense. Wszystkie różnice obowiązują identycznie w ON i OFF od chwili startu; nie są premią wywołaną taktyką.

| Czas s | Zawodnik | Rywal | Sytuacja | Decyzja | Target m | Gaz | Aktywacja s | Faktyczny rezultat F |
| ---: | --- | --- | --- | --- | ---: | ---: | ---: | --- |
| 0,10 | R1–R4 | najbliżsi | wspólny start/obserwacja | brak manewru | oryginalne | 1 | — | naturalnych różnic startu nie zaliczamy jako taktyki |
| 3,00 | R1–R4 | najbliżsi | rzeczywista jazda obok na łuku | ContestHold | oryginalne | 1 | 3,24–3,25 | bez dodatkowych kroków dla samej etykiety |
| 4,10 | R1 | R2 | odstęp referencji 0,863 m; nowe warunki | ContestInside | 0,58046 | 1 | 4,344 | przewidywany zysk 0,299 m referencji/s ponad własne sterowanie |
| 4,344 | R1 | R2 | upłynęło pełne 0,244 s | aktywacja | 0,58046 | 1 | 4,344 | prefix dotąd dokładnie zgodny z OFF |
| 4,47 | R1 | — | różnica pozycji ≥2 cm | pomiar | 0,58046 | 1 | — | początkowo −0,01996 m postępu względem OFF |
| 4,805 | R1 | — | różnica boczna ≥5 cm | pomiar | 0,58046 | 1 | — | offset 1,535 m; −0,252 m postępu |
| 6,00 | R1 | R2 | kontynuacja z nową linią | utrzymanie zamiaru | 0,58046 | 1 | — | offset 0,935 m, strata własnego postępu 0,878 m vs OFF |
| 12,00 | R1 | R2 | koniec wspólnego okna | pomiar | 0,58046 | 1 | — | R1 1,719 m za R2; +0,020 m własnej referencji vs OFF |

**F nie jest dowodem, że zacieśnienie zawsze wygrywa.** Budżet opony wymusza początkowy koszt prędkości. Średnia ocena PD była optymistyczna w krótkim okresie; dalsza kalibracja powinna uwzględnić koszt przejściowego szczytu korekty. Nie zmieniamy fizyki, żeby ukryć stratę. Kryterium eksperymentu jest tutaj spełnione: samodzielny wybór w rzeczywistym starcie, pełna reakcja, fizyczny ruch i mierzalna zmiana walki.

W pierwszym łuku F maksymalna różnica ON/OFF wynosi 1,181 m pozycji i 0,887 m referencji. Maksima całych 12 s wynoszą 12,156/10,422 m i obejmują późniejsze odmienne decyzje innych zawodników; nie przedstawiamy ich jako czystego zysku R1 z pierwszego manewru. Końcowa zmiana przewagi R1–R2 wynosi tylko +0,0126 m. Wszystkie cztery motocykle poruszają się w jednym świecie, przechodzą pierwszy łuk i zachowują wspólny czas. Minimum zmierzonego bliskiego odstępu F to 0,781 m, brak kontaktu. Maksymalny iloraz siły do budżetu: 1,00000012, czyli zaokrąglenie float dotychczasowego regulatora.

## 7. Rozstrzygnięcie równoległej walki to nie wyprzedzenie ze startu

Osobny obserwator wymaga rzeczywistej bliskości i nakładania rzutów sylwetek przed rozstrzygnięciem. Potem cały motocykl jednego zawodnika musi wyjść ≥0,15 m przed drugi i utrzymać to przez 1 s oraz ≥10 m dalszej rzeczywistej drogi. Nie wymaga, aby ten zawodnik zaczynał całym motocyklem z tyłu. Wynik nazywamy **rozstrzygnięciem walki rozpoczętej równolegle**, nie starym wyprzedzeniem #89.

Te pomiary są osobne dla ON i OFF. Przykład F: R2 uzyskuje pełną przewagę nad R4 już w 4,30 s, przed pierwszą zmienioną komendą w 4,344 s. To naturalny efekt startu i geometrii, nie sukces Side-by-Side. Dopiero zgodny prefix i późniejsza różnica ON/OFF pozwalają przypisać zmianę nowemu sterowaniu. Zapisujemy pomiary na wspólnych zaakceptowanych chwilach, bez interpolowania przyszłości i bez dumpu wewnętrznych substeps.

G jest dokładnie dawnym F-close: 20 m/s, wystarczający odstęp, równoległe niezamykające się motocykle. Zero komend, zero zmian sterowania, dokładny hash ON/OFF. B no-gain daje również brak niepotrzebnego manewru na łuku. Utrzymanie korzystnej linii jest poprawnym wynikiem; samo jego pojawienie się nie zalicza głównego dowodu A/F.

## 8. Koszt i regresje

Release, 10 ms, pary OFF/ON uruchamiane naprzemiennie, mediana pięciu próbek po dwa przebiegi. OFF oznacza działającą taktykę #89. Tabela nie porównuje różnych długości: wszystkie pary mają identyczny czas świata i żadna nie jest ucięta przez Crash. Benchmark nie przechowuje trajektorii i nie wykonuje offline geometrii obserwatora. Zegar oceny taktyki obejmuje wspólne pary, decyzje i kolejkę, poza solverem; jest średnią na cały przejazd.

| Próba | OFF → ON elapsed ms | OFF → ON alokacje B | OFF → ON ocena taktyki ms | Pary na przebieg | Kandydaci Side ON | Decyzje OFF→ON | Zmiany aktuatorów OFF→ON | Solver OFF→ON |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| samotny pełny bieg 60,48 s | 33,923 → 34,617 | 12 643 424 → 12 642 918 | 0,165 → 0,215 | 0 | 0 | 0→0 | 0→0 | 6064→6064 |
| czterech odległych, 0,80 s | 1,227 → 1,244 | 752 499 → 752 062 | 0,0039 → 0,0041 | 42 | 0 | 0→0 | 0→0 | 80→80 |
| dwa motocykle A, 4 s | 3,280 → 3,351 | 1 421 504 → 1 457 686 | 0,024 → 0,088 | 39 | 4 | 1→2 | 1→2 | 402→402 |
| oryginalne cztery E, 12 s | 21,734 → 19,365 | 4 704 364 → 4 374 772 | 0,276 → 0,568 | 714 | 43 | 0→11 | 0→3 | 1584→1404 |
| zróżnicowane cztery F, 12 s | 22,849 → 17,888 | 5 279 363 → 4 357 139 | 0,224 → 0,330 | 714 | 45 | 6→15 | 6→5 | 1784→1405 |
| dotychczasowy dense, 12 s | 18,615 → 18,848 | 4 527 508 → 4 729 156 | 0,155 → 0,293 | 714 | 80 | 11→13 | 11→3 | 1505→1463 |

Narzut elapsed: +2,04% solo, +1,37% odlegli (różnica 0,017 ms), +2,16% para, +1,25% dense. Dense alokuje +4,45%. E/F są łącznie tańsze o 10,90/21,71%, ponieważ inna jazda uruchamia mniej lokalnych kroków fizyki; **nie jest to ujemny koszt samego AI**. Sama ocena taktyki rośnie. Nie porównujemy tych liczb bezpośrednio ze starymi ~97 ms pełnego biegu z innej sesji. CPU znajduje się w CSV; przy krótkich próbach jego rozdzielczość Windows jest zbyt gruba, aby zero oznaczało zerowy koszt.

Pierwszy pomiar wykazał zbędne wejście w ocenę przy pustej liście par. Dodano prosty warunek odcięcia, powtórzono tylko celowane testy i ograniczony benchmark po tej zmianie. Bez dodatkowych światów, symulacji alternatyw, rekurencji, powtarzania rozwiązania kontaktu ani nowych dokładnych obliczeń geometrii w fizycznym substep. Kandydaci działają wyłącznie w odczycie AI; geometria zdarzeń jest offline.

**81/81 lokalnych testów Release**, w tym 23 nowe i wszystkie wcześniejsze 58. Sprawdzono A i F przy 5/10/20 ms, obie mieszane granice, korzystny Hold, brak korzyści, zajętą przestrzeń mimo przyszłego ruchu rywala, przyczynowość, anulowanie kolejki, brak nowych sił/atrybutów, kontakt, powtarzalność i odwrócenie kolekcji wejściowej. Niezmieniona deklaracja Body/Forces i ustawienia solvera są dodatkowo objęte wcześniejszymi regresjami.

Przy Side OFF **16/16** zamrożonych wariantów taktyki #89 (A–G, każdy z taktyką ON i OFF) ma dokładny hash, końcowe ciała, kontakty i komendy. Wspólny helper skończonego postępu naprawia nieobsługiwany przypadek zerowego promienia; nie zmienia tych zamrożonych wyników. Zachowano 39 zamrożonych konsumentów produkcyjnych. Bez wielokrotnego pełnego audytu historii. Standardowe Windows CI sprawdza końcowy commit nowego Draft.

## Odtworzenie i zapisane dane

`side-diagnose <katalog>` diagnozuje #89. `side-pairs <katalog> [nazwa]` uruchamia tylko A–D, `side-fields <katalog>` E–G, `side <katalog>` cały ograniczony zestaw, `side-benchmark <katalog>` powyższe sześć par wydajności. Przykład bez komend autorskich: `dotnet run --project tools/shared-world-speedway -c Release -- SF 0.01 --side-by-side`. `SF 0.01 --tactics` jest porównaniem #89 dla tych samych zawodników.

- [Diagnoza #89](side-by-side-racing-diagnosis.json).
- [301 chronologicznych zdarzeń](side-by-side-racing-events.csv): wspólne obserwacje, decyzje, aktywacje, faktyczne różnice, rozstrzygnięcia i kontakty.
- [246 ocen kandydatów](side-by-side-racing-candidates.csv), [24 wyniki ON/OFF](side-by-side-racing-results.csv), [wydajność i CPU](side-by-side-racing-performance.csv), [zamrożona zgodność #89](side-by-side-racing-parity.csv).

Kolumna minimum bliskiego odstępu pochodzi z dotychczasowego filtra kontaktowego; pusta oznacza, że para nie weszła w jego promień. Osobno zapisano skończone prześwity rzeczywistych kapsuł w istotnych zdarzeniach. Nie przedstawiamy dyskretnego pomiaru zdarzeń jako matematycznego certyfikatu całego przyszłego łuku.

Pozostaje kalibracja użyteczności kosztownego zacieśnienia i skuteczności obrony. To ograniczenie polityki decyzji, nie potrzeba przebudowy integratora, fizyki upadku ani zwiększania precyzji kontaktu.
