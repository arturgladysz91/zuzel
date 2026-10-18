# #89 — Tactical Racing: Overtaking and Position Defense

**B — CZĘŚCIOWO.** Główny test A działa przez rzeczywisty ruch: pełne minięcie w 2,23 s, utrzymanie przez kolejną sekundę i 21,67 m. Działają też wewnętrzna B, zewnętrzna C, różnica obrony D i rezygnacja E. Niezmienione pola Motoareny F nie wytwarzają jednak nowej walki taktycznej. Nie zaliczamy samego dojechania do 12 s jako sukcesu taktyki czterech zawodników.

Baza sprawdzona lokalnie i na GitHub: Draft #88, `experiment/stable-speedway-bike-dynamics`, HEAD `08ca15e1389cbeb3d6bce6029b1e0b69f89cd2f4`. Osobna gałąź `experiment/tactical-racing-overtake-defense`, bez merge i bez zmian wcześniejszych PR. Produkcyjny `HeatSimulator`, CoreSim, historyczne wyniki i kalibracje pozostają bez zmian.

## Najmniejszy mechanizm, status PROVISIONAL

Jawne `WorldParameters.EnableTacticalRacing = true` lub CLI `--tactics`; domyślnie false. To rozszerzenie **tego samego TrafficDecision** przez plik partial, nie drugi framework. Jedyny stan własny: zamiar, rywal, domowa linia oraz czasy aktywacji, utrzymania i ostatniej obserwacji wolnego miejsca. Nie ma planów całego okrążenia.

Co 100 ms WorldRunner zamraża jeden snapshot. Raz wylicza do sześciu nieuporządkowanych par i filtruje odległość 30 m. Wszyscy czytają te same wykonane pozycje, prędkości i kierunki. Komenda R1 nie zmienia snapshotu widzianego przez R2. Nie czytamy docelowej linii, gazu ani oczekującej komendy przeciwnika. Nie ma RNG, alternatywnych światów, replay ani nowego zużycia nawierzchni; zachowana jest zamrożona nawierzchnia eksperymentu #87.

Atak wymaga rywala z przodu, rzeczywistego zbliżania >0,25 m/s i krótkiego czasu dogonienia. Rozważamy tylko dwa korytarze. Ich koszt uwzględnia obecną szerokość motocykli wraz z obróconą kierownicą, granice docelowej linii, bieżące prędkości, odpowiedź boczną istniejącego regulatora oraz aktualną przyczepność, promień i zapotrzebowanie na korektę linii. Na łuku porównujemy możliwy postęp kątowy; na prostej względną prędkość. Jest to tania ocena wykonalności, nie certyfikat przyszłej trajektorii. Zewnętrzna nie otrzymuje bonusu.

Aktywny atak zachowuje stronę. Po rzeczywistym wyjściu przed rywala o długość motocykla i drogę reakcji może rozpocząć powrót, tylko przez wolny korytarz. Niewykonalny korytarz lub brak postępu przez 4 s powoduje rezygnację. Zamiar ma minimalnie 0,8 s stabilizacji; po odjęciu gazu obowiązuje co najmniej 0,8 s oraz 0,4 s kolejnych obserwacji bez zagrożenia przed wznowieniem. Bezpieczeństwo jest oceniane także podczas oczekiwania na komendę i podczas aktywnego zamiaru.

Nowe bezpośrednie zagrożenie może anulować oczekującą komendę taktyczną. Rejestrujemy `CancelledAt`; zastępstwo ma **nowy pełny czas reakcji od nowej obserwacji**. Nie nadpisujemy już oczekującej reakcji bezpieczeństwa kolejnym odczytem co 100 ms. Test rzeczywistego dogonienia z odstępem 8 m potwierdza anulowanie ataku przed jego aktywacją i brak wykonania odrzuconej linii.

Obrońca może wcześniej zająć wewnętrzną, dopóki rywal jest dostatecznie daleko za nim. Obecna zajętość korytarza przez motocykl obok nie znika wskutek ekstrapolacji jego pozycji na koniec horyzontu. Obrońca nie hamuje tylko dlatego, że ktoś zbliża się od tyłu; może zatrzymać własne niebezpieczne przecinanie. Ryzyko wynikające z faktycznej jazdy rozstrzyga niezmieniony Box2D.

Znaczenia sprawdzono w `docs/gameplay/rider-model.md`, `docs/calibration/contested-space-racing-response.md` i modelach kanonicznych: Attack wydłuża horyzont dostrzeżenia okazji (0,8–3,0 s), Defense horyzont wczesnej obrony (0,55–2,75 s), Combativeness zmniejsza margines i dopuszcza krótsze naleganie, Technique daje mały margines wykonania. PreferredLine jest słabą preferencją. PairRiding oznacza współpracę z partnerem, nie kontakt z przeciwnikiem — nie używamy go do walki. Strength ani masa nie dają przewagi silnika. Skala 1–99 pozostaje. Kanoniczne Reaction nadal dotyczy taśmy; opóźnienie wyścigowe jest dotychczasowym, jawnym proxy #87 z legacy start timing, tutaj 0,24 s. To ograniczenie modelu, nie nowa interpretacja Reaction.

Wszystkie stałe polityki są robocze, nazwane i skupione w TrafficDecision. Nie zmieniono żadnego równania ani stałej w `RiderController.Forces`, solvera, czterech substeps, geometrii, progów severity, epizodów ani kontaktów. AI przekazuje wyłącznie target i gaz. Box2D wykonuje całą zmianę pozycji i prędkości.

## Co uznajemy za rzeczywiste wyprzedzenie

Niezależny obserwator rzutuje **rzeczywiste kapsuły chassis i kierownicy** obu motocykli w tym samym zaakceptowanym czasie na styczną drogi R1. Cały tył R1 musi znajdować się co najmniej 0,15 m przed całym przodem R2. Stan musi utrzymać się nieprzerwanie przez co najmniej 1 s i co najmniej 10 m rzeczywiście przejechanej drogi R1. Spadek poniżej marginesu zeruje próbę utrzymania. Pomiar jest dyskretny w zaakceptowanych stanach; nie deklarujemy dokładności lepszej niż ten odczyt. Sam Attack, kolejność planów ani zmiana targetu nie zalicza wyprzedzenia.

Próba musi zaczynać się z całym motocyklem R1 za R2. W F/G start jest równoległy, dlatego nie produkujemy fałszywego „wyprzedzenia” z początkowego remisu. Początek ruchu bocznego oznacza zmianę rzeczywistego offsetu o 5 cm, nie wydanie komendy. Odległość końcowa w CSV to rzut różnicy środków na aktualną styczną R1; nie należy mylić jej z prześwitem całych sylwetek.

Poniżej wyniki końcowego Release przy 10 ms. A przechodzi także przy 5 i 20 ms. W żadnej próbie ON A–G nie było kontaktu. Najmniejsze zmierzone odstępy A/B/C/D90/E wyniosły 0,4387 / 0,1928 / 0,8812 / 0,01449 / 0,6100 m. To pomiar wykonanej geometrii, nie gwarancja dla innych stanów początkowych. Maksymalny iloraz siły do budżetu opony wyniósł 1,00000012 (zaokrąglenie float); nie zwiększono fizycznego limitu. B/C pozostają w granicach toru.

## A — szybszy na prostej

R1: 20 m/s, Attack 85; R2: 12 m/s, Defense 15. Początkowo 12 m między środkami, ta sama linia 4 m, rzeczywista przeciwległa prosta Motoareny. Horyzont 7,5 s obejmuje zakończenie powrotu.

| Czas s | Zawodnik | Sytuacja | Decyzja | Linia m | Gaz | Aktywacja s | Efekt fizyczny |
| ---: | --- | --- | --- | ---: | ---: | ---: | --- |
| 0,10 | R1 | wspólna obserwacja, dogania R2 | AttackInside | 1,991 | 1 | 0,34 | offset nadal 4,000 |
| 0,34 | R1 | termin reakcji | aktywacja ataku | 1,991 | 1 | 0,34 | brak skoku pozycji |
| 0,50 | R1 | ruch wykonany | utrzymuje atak | 1,991 | 1 | — | offset 3,947; przekroczone 5 cm |
| 0,80 | R1 | nowa bliskość | BackOut, zachowuje stronę | 1,991 | 0 | 1,04 | ogranicza gaz, dalej otwiera korytarz |
| 1,33 | R1–R2 | rzeczywiście obok | utrzymuje stronę | 1,991 | 0 | — | nakładają się rzuty wzdłużne |
| 1,90 | R1 | miejsce wolne przez wymagany czas | ResumeIntent | 1,991 | 1 | 2,14 | odzyskuje napęd po reakcji |
| 2,23 | R1 | cały motocykl przed R2 | pomiar | 1,991 | 1 | — | prześwit wzdłużny 0,179 m, nadal prosta |
| 3,23 | R1 | przewaga utrzymana | pomiar | 1,991 | 1 | — | 1 s i 21,67 m dalszej jazdy |
| 3,60 | R1 | droga powrotu wolna | ReturnLine | 4,000 | 1 | 3,84 | rzeczywisty powrót przez siły |
| 7,10 | R1 | offset 3,901 | ReturnCompleted | 4,000 | 1 | 7,34 | zamiar zakończony; brak przełączania stron |

R2 przez cały przejazd ON utrzymuje gaz 1 i własny target 4 m. Nie ma automatycznego ustąpienia. W OFF dotychczasowa AI kieruje obu na zewnętrzną i dochodzi do rzeczywistego Crash w 1,295 s (closing 7,407 m/s, severity 2,838). Diagnostyka kończy się na tym kontakcie, bez modelowania jazdy po upadku.

## B — wewnętrzna na łuku

R1/R2: referencja 45/53 m, offset 5 m, prędkość 22/17 m/s. Zmniejszenie promienia i odzyskiwanie linii nadal konkurują o dotychczasowy budżet opon.

| Czas s | Zawodnik | Sytuacja | Decyzja | Linia m | Gaz | Aktywacja s | Efekt fizyczny |
| ---: | --- | --- | --- | ---: | ---: | ---: | --- |
| 0,10 | R1 | obserwacja wolniejszego na łuku | AttackInside | 3,153 | 1 | 0,34 | offset 4,998 |
| 0,34 | R1 | reakcja upłynęła | aktywacja | 3,153 | 1 | 0,34 | offset 4,994 |
| 0,61 | R1 | wykonany ruch | utrzymuje atak | 3,153 | 1 | — | offset 4,949 |
| 1,10 | R1 | ryzyko zbliżenia | BackOut | 3,153 | 0 | 1,34 | fizyczne odpuszczenie |
| 1,557 | R1–R2 | rzeczywiście obok | utrzymanie strony | 3,153 | 0 | — | nadal na łuku |
| 1,80 | R2 | bliski rywal obok | BackOut | 5,000 | 0 | 2,04 | obrońca reaguje na aktualne zagrożenie |
| 2,44 | R1 | cała sylwetka przed R2 | pomiar | 3,153 | 0 | — | prześwit 0,195 m |
| 2,50 | R1 | sytuacja wolna | ResumeIntent | 3,153 | 1 | 2,74 | rzeczywisty powrót napędu |
| 3,44 | R1 | przewaga utrzymana | pomiar | 3,153 | 1 | — | 1 s i 20,98 m |
| 3,64 | R1 | bezpieczny powrót | aktywacja ReturnLine | 5,000 | 1 | 3,64 | powrót trwa po końcu okna 4,5 s |

OFF: kontakt i jeden skutek, bez Crash; R1 nie uzyskuje pełnego minięcia. ON: brak kontaktu. O wyniku decyduje wykonana jazda obu motocykli, również rzeczywiste odpuszczenie R2 przy jeździe obok.

## C — zewnętrzna przy braku wewnętrznej

R1/R2: referencja 45/58 m, offset 1 m, prędkość 20/12 m/s. Wewnętrzny kandydat wymaga targetu poza dopuszczalnym zakresem; zewnętrzna mieści się na torze i ma dodatnią ocenę możliwego postępu.

| Czas s | Zawodnik | Sytuacja | Decyzja | Linia m | Gaz | Aktywacja s | Efekt fizyczny |
| ---: | --- | --- | --- | ---: | ---: | ---: | --- |
| 0,10 | R1 | obserwacja; brak miejsca wewnątrz | AttackOutside | 3,231 | 1 | 0,34 | offset 0,998 |
| 0,34 | R1 | termin reakcji | aktywacja | 3,231 | 1 | 0,34 | offset 0,995 |
| 0,50 | R1 | ruch wykonany | utrzymuje atak | 3,231 | 1 | — | offset 1,052 |
| 1,00 | R2 | rywal zbliża się z tyłu | CoverInsideEarly | 0,515 | 1 | 1,24 | obrona przez rzeczywistą linię |
| 1,51 | R1–R2 | motocykle obok | utrzymują linie | 3,231 | 1 | — | offset R1 2,189 |
| 2,21 | R1 | cały przed R2 | pomiar | 3,231 | 1 | — | prześwit 0,181 m |
| 2,30 | R2 | rywal już przed nim | DefenseReleased | 1,000 | 1 | 2,54 | wraca do domowej linii |
| 3,21 | R1 | utrzymana przewaga | pomiar | 3,231 | 1 | — | 1 s i 19,06 m |
| 3,34 | R1 | droga powrotna wolna | aktywacja ReturnLine | 1,000 | 1 | 3,34 | rzeczywista zmiana linii, bez kontaktu |

Obaj zachowują gaz 1 przez całe C; nie ma bonusu ani wymuszonego ustąpienia. OFF kończy się Crash w około 1,43 s.

## D — tylko Defense 15 → 90

Wszystkie inne atrybuty, fizyka i warunki początkowe są takie same jak A. OFF oba profile mają identyczny hash. W ON lepsza obrona reaguje wcześniej, ale w tej próbie ostatecznie przegrywa; nie traktujemy ratingu jako gwarancji wyniku.

| Czas s | Zawodnik | Sytuacja | Decyzja | Linia m | Gaz | Aktywacja s | Efekt fizyczny |
| ---: | --- | --- | --- | ---: | ---: | ---: | --- |
| 0,10 | R2 Defense 15 | obserwacja R1 | utrzymanie linii | 4,000 | 1 | — | brak manewru obronnego |
| 0,10 | R2 Defense 90 | ta sama obserwacja | CoverInsideEarly | 2,919 | 1 | 0,34 | komenda przed dojechaniem rywala |
| 0,34 | R2 Defense 90 | czas reakcji | aktywacja obrony | 2,919 | 1 | 0,34 | ruch przez regulator |
| 0,56 | R2 Defense 90 | rzeczywista zmiana | utrzymuje obronę | 2,919 | 1 | — | offset 3,949; przy 0,70 s różnica >5 cm względem Defense 15 |
| 0,80 | R2 Defense 90 | rywal zajmuje przecinany korytarz | zatrzymanie własnego cięcia | 3,826 | 1 | 1,04 | gaz pozostaje, target zostaje skorygowany |
| 1,328 | R1–R2 | rzeczywiście obok | zachowanie stron | 1,991 / 3,826 | 0 / 1 | — | realna bliskość, minimum 14,49 mm bez kontaktu |
| 1,60 | R2 Defense 90 | zagrożenie obok | BackOut | 3,826 | 0 | 1,84 | skutkiem obrony jest też koszt prędkości |
| 2,18 | R1 | pełne minięcie | pomiar | 1,991 | 0 | — | prześwit 0,165 m; dla Defense 15 minięcie w 2,23 s |
| 2,70 | R2 Defense 90 | obrona zakończona | ReturnLine | 4,000 | 1 | 2,94 | wraca do jazdy |
| 3,18 | R1 | utrzymuje przewagę | pomiar | 1,991 | 1 | — | 1 s i 21,21 m |

Model potwierdza wpływ Defense na decyzję i ruch, nie potwierdza jeszcze dobrze skalibrowanej skuteczności obrony. To robocza polityka. Nie dostrajaliśmy fizyki, aby mocniejszy profil obowiązkowo wygrał.

## E — oba korytarze zajęte

R1 20 m/s z tyłu; R2/R3/R4 po 16 m/s, linie 4/2,6/5,4 m. Cztery rzeczywiste motocykle, sześć par na odczyt.

| Czas s | Zawodnik | Sytuacja | Decyzja | Linia m | Gaz | Aktywacja s | Efekt fizyczny |
| ---: | --- | --- | --- | ---: | ---: | ---: | --- |
| 0,10 | R1 | obserwuje blokadę obu stron | NoPassingCorridor / BackOut | 4,000 | 0 | 0,34 | nie zamienia targetu w fikcyjne wyprzedzenie |
| 0,34 | R1 | czas reakcji upłynął | aktywacja | 4,000 | 0 | 0,34 | zostaje za grupą na własnej linii |
| 2,50 | R1 | odstęp i okres wolnej obserwacji | ClearResume | 4,000 | 1 | 2,74 | powrót napędu, bez próby przeciśnięcia |
| 2,70 | R2 / R3 | zbliżenie podczas wejścia w łuk | BackOut | 4,000 / 2,599 | 0 | 2,94 | obaj widzą wspólny snapshot |
| 4,00 | R1 | koniec okna | pomiar | 4,000 | 1 | — | 11,63 m za R2 w rzucie środków; brak minięcia i kontaktu |

R1 nie wykonał ruchu bocznego ≥5 cm. Żaden rywal nie został przestawiony. OFF ma osiem komend, siedem zmian i brak kontaktu; ON cztery komendy/zmiany.

## F — niezmienione rzeczywiste pola Motoareny

Dokładnie `Scenarios.G()` z #88: fizyczne offsety pól około 0,5/3,5/6,5/9,5 m i istniejące targety 1,6/3,2/5,6/8,3 m. Nie zmieniono profili, prędkości, reakcji ani początkowych zamiarów w celu sprowokowania walki.

| Czas s | Zawodnik | Sytuacja | Decyzja | Linia m | Gaz | Aktywacja s | Efekt fizyczny |
| ---: | --- | --- | --- | --- | ---: | --- | --- |
| 0,00 | R1–R4 | start obok siebie | istniejące targety | 1,6 / 3,2 / 5,6 / 8,3 | 1 | wejście początkowe | brak wyprzedzenia z remisu |
| 0,10 | R1–R4 | wspólna obserwacja | brak nowej komendy | bez zmian | 1 | — | taśma nadal w istniejącym czasie reakcji |
| 0,45–0,72 | R4 / R1 / R3 / R2 | rzeczywisty ruch boczny | wykonanie początkowych targetów | bez zmian | 1 | — | przekroczone 5 cm; to nie nowa taktyka |
| 2,949998 | R4 | wjeżdża w pierwszy łuk | utrzymanie | 8,3 | 1 | — | rzeczywisty postęp 35 m, prędkość 24,006 m/s |
| 2,954481 | R3 | pierwszy łuk | utrzymanie | 5,6 | 1 | — | 23,188 m/s |
| 2,961976 | R2 | pierwszy łuk | utrzymanie | 3,2 | 1 | — | 22,426 m/s |
| 2,968824 | R1 | pierwszy łuk | utrzymanie | 1,6 | 1 | — | 21,903 m/s |
| 12,00 | R1 / R2 / R3 / R4 | wykonany postęp | pomiar | bez zmian | 1 | — | referencja 227,413 / 225,940 / 223,316 / 220,496 m |

119 wspólnych odczytów, 714 obserwacji par, 476 obserwacji zawodnika, najwyżej sześć par w jednym odczycie. Brak komend, kontaktów i upadku; ON/OFF mają identyczny hash `BB7DF2…49B6`. Zmienia się porządek postępu wskutek istniejącej geometrii i jazdy, ale **nie dowodzi to ataku taktycznego**.

Konkretny brak: detektor ataku szuka wyraźnie poprzedzającego motocykla (odstęp większy niż długość) i już obserwowanej przewagi prędkości. Niemal równoległy start i późniejsza przewaga krótszego toru nie spełniają tego warunku. Najmniejszy dalszy krok to osobna, krótka ocena dostępnej linii i względnego postępu dla rywala jadącego obok na łuku. Wymaga osobnego dowodu realnej walki z tych samych pól, bez zmiany fizyki ani podstawienia komend. Nie rozbudowujemy teraz prognozy całego okrążenia.

Dodatkowo istniejący G-dense-stress przy ON pozostaje rzeczywistym testem czterech: R1 odpuszcza w 0,94 s, R3 w 1,34 s, R4 w 1,64 s; R1 aktywuje atak zewnętrzny w 4,54 s, a nowe zagrożenie powoduje `AbortThreatenedCrossing` w 4,94 s. Brak kontaktu w 12 s. Ta próba pokazuje przerwanie aktywnego ataku w grupie, ale nie zastępuje wymaganej walki z rzeczywistych pól F.

## G, deterministyczność i regresje

G jest dokładnie dotychczasowym `F-close`: równoległe pasywne motocykle, 20 m/s, 0,9 m między środkami. ON/OFF mają identyczny hash `CD70ED…F320`, zero decyzji, zero zmian gazu/linii, zero kontaktów i 0,1000 m minimalnego odstępu. Brak fałszywego wyprzedzenia z remisu.

18 nowych przypadków testowych obejmuje rzeczywiste minięcie A przy 5/10/20 ms, obie strony łuku, Defense, blokadę, anulowanie oczekującej komendy, rezygnację, brak znajomości przyszłego targetu rywala, Attack/Combativeness bez dodatkowej siły, histerezę gazu, dokładne powtórzenie i odwrócenie wejściowej kolekcji, wspólny czas F, F-close i zatrzymanie na pierwszym Crash. Prefix A przed aktywacją pozostaje taki sam jak OFF na wspólnych zaakceptowanych chwilach. Kolejność decyzji i kontaktów również jest powtarzalna.

Końcowy lokalny Release: **58/58** celowanych `SharedWorld*`, w tym wszystkie wcześniejsze 40. Jedna kompilacja rozwiązania, 0 ostrzeżeń/błędów; po wykryciu błędnego targetu wyłącznie w wierszu eksportu wejścia w łuk poprawiono metadane i przebudowano samo narzędzie. Pomiar wydajności nie został powtórzony, ponieważ ruch ani sterowanie się nie zmieniły. Standardowe Windows CI w nowym Draft PR sprawdza końcowe źródła.

Jedno ograniczone porównanie z zachowanymi bibliotekami Release #88: A-lap, A-heat, B-yield, C-light, C-support, D-bend, F-close, G-first-bend, G-dense-stress przy 10 ms. **9/9** dokładnych hashy wykonanych stanów, końcowych motocykli, czasów mety, kontaktów i oryginalnych pól komend. Nie uruchamiano ponownie macierzy historycznych audytów. Guard zachował 39 zamrożonych konsumentów produkcyjnych.

## Koszt, Release 10 ms

Dwa rozgrzania; pięć próbek po dwa przejazdy, mediana elapsed, średnia CPU i alokacji per przejazd. Sam WorldRunner, bez obserwatora przechowującego trajektorie i bez offline kontroli minięcia. CPU Windows ma grubą rozdzielczość (średnie krótkich prób skaczą co około 1,56 ms); nie wyciągamy wniosków z różnicy pojedynczych krótkich odczytów CPU. Nie ma nowych dokładnych certyfikacji geometrii w klatkach fizyki; projekcje korytarzy działają wyłącznie co 100 ms.

| Próba | Taktyka | Czas świata s | Elapsed ms | CPU ms | Alokacje B | Obserwacje zawodnika / par | Kandydaci | Decyzje / rzeczywiste zmiany |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| pełny bieg czterech #88 | OFF | 63,24 | 82,190 | 92,188 | 15 766 416 | 0 / 0 | 0 | 0 / 0 |
| samotny pełny bieg, traffic włączony | OFF | 60,48 | 34,012 | 35,938 | 12 434 979 | 0 / 0 | 0 | 0 / 0 |
| ten sam samotny bieg | ON | 60,48 | 34,380 | 40,625 | 12 633 651 | 604 / 0 | 0 | 0 / 0 |
| czterech odległych, rozstawionych po torze | OFF | 0,80 | 1,187 | 1,563 | 744 651 | 0 / 0 | 0 | 0 / 0 |
| ci sami odlegli | ON | 0,80 | 1,166 | 3,125 | 751 574 | 28 / 42 | 0 | 0 / 0 |
| rzeczywisty pojedynek A | ON | 7,50 | 7,867 | 7,813 | 2 551 432 | 148 / 74 | 28 | 5 / 4 |
| czterech E, blokada | OFF | 4,00 | 8,797 | 7,813 | 2 123 512 | 0 / 0 | 0 | 8 / 7 |
| czterech E, blokada | ON | 4,00 | 7,663 | 7,813 | 1 954 547 | 156 / 234 | 2 | 4 / 4 |
| czterech dense | OFF | 12,00 | 16,516 | 26,563 | 3 779 572 | 0 / 0 | 0 | 6 / 6 |
| czterech dense | ON | 12,00 | 18,568 | 17,188 | 4 520 268 | 476 / 714 | 4 | 11 / 11 |

Bez rywala: +1,08% elapsed i +1,60% alokacji; zero kandydatów i zero zmiany ruchu. Czterech odległych: różnica elapsed w szumie pomiaru, +0,93% alokacji, tani filtr odrzuca wszystkie pary. Dense: **+12,43% elapsed, +19,60% alokacji**; liczba rzeczywistych kroków solvera rośnie 1288→1505 wskutek innej jazdy i bliskości. To łączny koszt taktyki oraz zmienionej sytuacji fizycznej. E ON jest tańsze, bo wykonuje 474 zamiast 540 kroków; nie nazywamy tego ujemnym kosztem AI. Nie dzielimy kosztu A przez OFF urwany na Crash w 1,295 s.

Dotychczasowe około 97 ms i obecne 82,19 ms pełnego biegu OFF pochodzą z różnych chwil pomiaru. Przy identycznym hash nie ma podstaw przypisywać różnicy nowej optymalizacji. Porównania narzutu opierają się na powyższych parach z tej samej sesji, wyłącznie przy 10 ms. Brak wielokrotnego wzrostu kosztu, alternatywnych symulacji i tysięcy dodatkowych poszukiwań czasu.

## Odtworzenie i dane

`dotnet run --project tools/shared-world-speedway -c Release -- tactics-a <katalog>` uruchamia tylko A. `tactics <katalog>` wykonuje A–G ON/OFF; `tactics-benchmark <katalog>` mierzy powyższy ograniczony zestaw. Zwykłe `TA 0.01 --tactics` włącza nową politykę i zatrzymuje diagnostykę na pierwszym Crash. Bez flagi pozostaje #88.

- [Zdarzenia A–G w kolejności czasu](tactical-racing-events.csv): obserwacja, decyzja, anulowanie, aktywacja, wykonany ruch, minięcie i kontakt; bez dumpu wewnętrznych substeps.
- [Wyniki i hashe](tactical-racing-runs.csv), [wydajność](tactical-racing-performance.csv), [zgodność OFF z #88](tactical-racing-parity.csv).

Pełna walka czterech ze startu równoległego, docelowa kalibracja decyzji obronnych oraz kanoniczny model reakcji wyścigowej pozostają do udowodnienia. Nie blokuje ich integrator ani precyzja nanosekundowa; brakuje odpowiedniego, nadal małego kryterium okazji przy jeździe obok. W tym PR nie rozwijamy fizyki po upadku, sędziowania ani migracji do produkcji.
