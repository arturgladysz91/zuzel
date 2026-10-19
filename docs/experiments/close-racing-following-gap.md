# Close Racing Validation and Adaptive Following Gap (#94)

**PROVISIONAL — B: CZĘŚCIOWO.** Cztery motocykle w Rolling Pack naprawdę podążają wspólnym korytarzem, używają częściowego gazu i później fizycznie zmieniają linię. Wariant zależny od zbliżania poprawia wybrane krótkie przypadki, lecz **nie zmniejsza rzeczywistej mediany prześwitu w pełnym Rolling Pack**: 3.778 → 3.804 m. Żaden pełny przejazd nie ma podążania z prześwitem wzdłużnym ≤3 m. Nie ma podstaw do oceny A ani do włączenia flagi domyślnie.

Baza: Draft #93, `experiment/proportional-traffic-throttle`, `cb980e0e59b5ad6a1c3b28ac35db778d1a1fae94`. Jeden nowy Draft, bez merge. `EnableClosureAdaptiveFollowingGap=false` domyślnie, wymaga `EnableProportionalTrafficThrottle=true`. Przy nowej fladze OFF zachowane są dokładne wyniki #93 ON; przy obu OFF wcześniejsze wyniki #92. Produkcyjny `HeatSimulator`, `src/`, `RiderController.Forces`, ContactAdapter/Box2D, napęd, geometria, grip, reakcja, umiejętności i scoring #91 nie zmieniają się.

## Etap A: najpierw pomiar #93

[Zamrożona konfiguracja](close-racing-following-gap/frozen-configuration.json), [pieczęć SHA-256 bazowych plików](close-racing-following-gap/baseline-seal.json), [diagnoza przed zmianą wzoru](close-racing-following-gap/stage-A.md), [wybór przed pełnymi próbami kandydata](close-racing-following-gap/selection.md). Najpierw wykonano poprawny rolling-straight, potem resztę bazowych prób. Wzór regulatora był wtedy identyczny z #93. Dodane liczniki nie zmieniają granic kroków. Sprawdzenie odtwarza dokładne hashe i komendy A1/0 i A2/1 oraz historyczne hashe 8 s OFF/ON.

A1 rozdziela aktualne korytarze: brak podążania, początkowe 2.2 m bocznego prześwitu fizycznych części. A2/1 ma 26.611 s podążania R3, ale p10/mediana/p90 prześwitu 6.203/7.549/9.551 m. To daleki pościg, przy którym regulator może prawidłowo pozostawiać pełny gaz. Nowe profile o identycznych legacy Speed/SlideControl nadal głównie jadą odrębnymi liniami; sama równość osiągów nie tworzy ciasnej grupy po starcie.

Rolling-straight bazowy pokazuje 49.560 s podążania R1, 12.300 s częściowego gazu i późniejszy rzeczywisty atak. Trzy aktywowane pełne zamknięcia (9.936, 17.336, 24.736 s), łącznie 1.200 s, mają zwykły powód `Following`, nie Emergency. Przy danej fazie łuku istniejąca funkcja sił może nie dawać potrzebnego spowolnienia dla żadnego dodatniego gazu (neutral carry); Drive=0 odsłania dotychczasowy opór. Nie dodano hamulca. Kandydat nie ma w tym przejeździe żadnego pełnego zamknięcia. Mała para równych prędkości zaczyna z około 1.1 m prześwitu, a baza rozsuwa ją do mediany 2.110 m — stąd ograniczone porównanie preferencji.

## A. Dwanaście pełnych prób

Sześć zamrożonych konfiguracji × baza/kandydat = **12 głównych przejazdów czterech motocykli przez cztery obiegi**. Dodatkowe uruchomienia regresji i krótkie pomiary wydajności nie są nowymi konfiguracjami. Wszystkie normalne AI, bez komend autorskich. Motoarena, referencyjny obieg 318.77874994277954 m, krok .01 s, 4 substeps, AI .1 s, Tactical=true, SideBySide=true, Quality=false, ProportionalThrottle=true. Nieruchoma jednolita nawierzchnia: Grip=1, Ruts=0, Moisture=.5; EffectiveGrip=.9175.

Standing Start używa istniejących pól A/B/C/D (offset .5/3.5/6.5/9.5 m). Rotacja 1: R1=B, R2=C, R3=D, R4=A. A1/A2 bez zmian z #92/#93. Nowa grupa close ma legacy Start/Speed/SlideControl=55/55/65, masę 70 kg i neutralny setup. Kanoniczne umiejętności różnią się, zamrożone przed pierwszym uruchomieniem:


| Zawodnik | Reaction | Start | Technique | TrackReading | Attack | Defense | PairRiding | Strength | Combativeness |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 65 | 60 | 65 | 60 | 80 | 45 | 50 | 60 | 0.7 |
| 2 | 55 | 65 | 70 | 65 | 55 | 75 | 50 | 65 | 0.4 |
| 3 | 60 | 55 | 60 | 55 | 75 | 55 | 50 | 60 | 0.6 |
| 4 | 70 | 60 | 65 | 70 | 60 | 65 | 50 | 55 | 0.5 |


Rolling Pack jest osobnym fragmentem trwającego wyścigu. Na prostej referencje R1/R2/R3/R4 to 133.389/141.389/140.389/140.389 m; w łuku 45/53/52/52 m. Offsety 4/4/2.6/5.4 m; prędkości 19/18/19.5/18.5 m/s. Kierunki prędkości i początkowe orientacje są lokalnie styczne, bez ręcznych zmian po starcie. Weryfikacja wszystkich sześciu par pełnych kapsuł i granicy toru następuje przed krokiem. Akceptowana różnica odczytanej orientacji Box2D od stycznej ≤.005 rad uwzględnia istniejącą aproksymację obrotu (~.0014 rad w tej krzywiźnie).

Runner ma opcjonalne `lapsFromInitialPosition=false`: tylko Rolling włącza metę `initialReference + 4*lapMeters` dla każdego zawodnika; pomiar okrążeń stosuje ten sam początek. Nie zaliczamy skróconego pierwszego obiegu jako pełnego. Czasy/order Rolling porównują cztery pełne obiegi od indywidualnych pozycji, nie wspólną linię mety regulaminowego biegu od taśmy. Ciała ukończonych nadal fizycznie jadą do ostatniej mety; ich własna telemetria i komendy kończą się na ich mecie. Limit 90 s, przerwanie na rzeczywisty Crash.

Tabela: czasy w kolejności R1/R2/R3/R4; częściowy gaz i podążanie to suma rider-sekund. A/D/S = aktywowane ataki/obrony/manewry SideBySide, wyliczone wyłącznie z kolejki **Line**. Atak o Action=AttackInside/Outside może równocześnie być manewrem SideBySide. Gaz z zachowaną intencją nie jest kolejnym atakiem.


| Próba | Typ | Kolejność | 4 obiegi [s], R1–R4 | Częściowy [s] | Podążanie [s] | A/D/S | Minięcia | Kontakt/Crash | Solver |
|---|---|---|---|---|---|---|---|---|---|
| [A1-rotation-0/baseline](close-racing-following-gap/A1-rotation-0-baseline.json) | Standing | R1>R2>R3>R4 | 60.532/61.432/62.356/62.584 | 0.000 | 0.000 | 1/0/7 | 0 | 0/0 | 6333 |
| [A1-rotation-0/candidate](close-racing-following-gap/A1-rotation-0-candidate.json) | Standing | R1>R2>R3>R4 | 60.532/61.432/62.356/62.584 | 0.000 | 0.000 | 1/0/7 | 0 | 0/0 | 6333 |
| [A2-rotation-1/baseline](close-racing-following-gap/A2-rotation-1-baseline.json) | Standing | R4>R2>R1>R3 | 61.338/60.952/61.454/59.817 | 0.000 | 26.611 | 5/1/19 | 1 | 0/0 | 6237 |
| [A2-rotation-1/candidate](close-racing-following-gap/A2-rotation-1-candidate.json) | Standing | R4>R2>R1>R3 | 61.338/60.952/61.454/59.817 | 0.000 | 26.611 | 5/1/19 | 1 | 0/0 | 6237 |
| [close-rotation-0/baseline](close-racing-following-gap/close-rotation-0-baseline.json) | Standing | R1>R2>R3>R4 | 59.639/60.538/61.461/61.788 | 0.000 | 0.480 | 3/0/6 | 0 | 0/0 | 6267 |
| [close-rotation-0/candidate](close-racing-following-gap/close-rotation-0-candidate.json) | Standing | R1>R2>R3>R4 | 59.639/60.538/61.461/61.788 | 0.000 | 0.480 | 3/0/6 | 0 | 0/0 | 6267 |
| [close-rotation-1/baseline](close-racing-following-gap/close-rotation-1-baseline.json) | Standing | R4>R1>R2>R3 | 60.530/61.451/61.851/59.635 | 0.000 | 0.000 | 3/0/4 | 0 | 0/0 | 6267 |
| [close-rotation-1/candidate](close-racing-following-gap/close-rotation-1-candidate.json) | Standing | R4>R1>R2>R3 | 60.530/61.451/61.851/59.635 | 0.000 | 0.000 | 3/0/4 | 0 | 0/0 | 6267 |
| [rolling-straight/baseline](close-racing-following-gap/rolling-straight-baseline.json) | Rolling | R3>R1>R2>R4 | 59.448/59.555/58.977/59.970 | 12.300 | 49.560 | 1/0/12 | 1 | 0/0 | 7156 |
| [rolling-straight/candidate](close-racing-following-gap/rolling-straight-candidate.json) | Rolling | R3>R1>R2>R4 | 59.275/59.554/58.976/59.969 | 1.200 | 32.130 | 2/0/14 | 1 | 0/0 | 6462 |
| [rolling-bend/baseline](close-racing-following-gap/rolling-bend-baseline.json) | Rolling | R3>R2>R1>R4 | 59.961/59.796/58.857/60.566 | 0.000 | 31.970 | 2/0/8 | 1 | 0/0 | 6927 |
| [rolling-bend/candidate](close-racing-following-gap/rolling-bend-candidate.json) | Rolling | R3>R2>R1>R4 | 59.961/59.796/58.857/60.566 | 0.000 | 31.970 | 2/0/8 | 1 | 0/0 | 6927 |


Wszystkie 12 ukończone; zero kontaktów, konsekwencji i Crash, zero nowych anomalii granicy toru, zero zbliżeń <.3 m z dodatnim ruchem ku rywalowi. To wynik tych pozycji i prędkości, nie gwarancja bezpieczeństwa małego dystansu. Historyczny nieunikniony Crash pozostaje testem regresyjnym. Potwierdzenie minięcia wymaga ustanowionego lidera pełnych motocykli przez 1 s/10 m, realnego stanu obok siebie, potem całego motocykla z .15 m prześwitu utrzymanego 1 s/10 m; początkowe rozstawienie nie tworzy minięcia. Wszystkie potwierdzenia są po pierwszej sekundzie; pełne zdarzenia w JSON.

Szczegóły wszystkich zawodników. Pełny/częściowy/zero to czas rzeczywiście aktywowanego kanału gazu do własnej mety, wyliczony z jego granic aktywacji. Pomiar podążania i kwantyli waży odstępy między zaakceptowanymi stanami; lewy stan może poprzedzać potwierdzenie aktuatora o jeden mikro-krok (≤.01 s). Starsze pola `Measurement.Attacks/Defenses/SideManeuvers` w surowym kolektorze #92 zliczają również odziedziczoną intencję komend gazu — raport używa zamiast nich odrębnych aktywacji Line.


| Próba | R | Okrążenia 1/2/3/4 [s] | Pełny/częściowy/zero [s] | Podążanie [s] | Podążanie częściowo [s] | A/D/S | Minięcia | Min fiz. [m] | Zagrożenie [s] |
|---|---|---|---|---|---|---|---|---|---|
| A1-rotation-0/baseline | R1 | 16.228/14.768/14.768/14.768 | 60.532/0.000/0.000 | 0.000 | 0.000 | 0/0/2 | 0 | 2.200 | 0.000 |
| A1-rotation-0/baseline | R2 | 16.491/14.981/14.981/14.981 | 61.432/0.000/0.000 | 0.000 | 0.000 | 0/0/2 | 0 | 2.200 | 0.000 |
| A1-rotation-0/baseline | R3 | 16.758/15.199/15.199/15.199 | 62.356/0.000/0.000 | 0.000 | 0.000 | 0/0/2 | 0 | 2.200 | 0.000 |
| A1-rotation-0/baseline | R4 | 17.028/15.422/15.113/15.020 | 62.584/0.000/0.000 | 0.000 | 0.000 | 1/0/1 | 0 | 2.200 | 0.000 |
| A1-rotation-0/candidate | R1 | 16.228/14.768/14.768/14.768 | 60.532/0.000/0.000 | 0.000 | 0.000 | 0/0/2 | 0 | 2.200 | 0.000 |
| A1-rotation-0/candidate | R2 | 16.491/14.981/14.981/14.981 | 61.432/0.000/0.000 | 0.000 | 0.000 | 0/0/2 | 0 | 2.200 | 0.000 |
| A1-rotation-0/candidate | R3 | 16.758/15.199/15.199/15.199 | 62.356/0.000/0.000 | 0.000 | 0.000 | 0/0/2 | 0 | 2.200 | 0.000 |
| A1-rotation-0/candidate | R4 | 17.028/15.422/15.113/15.020 | 62.584/0.000/0.000 | 0.000 | 0.000 | 1/0/1 | 0 | 2.200 | 0.000 |
| A2-rotation-1/baseline | R1 | 16.396/14.981/14.981/14.980 | 61.338/0.000/0.000 | 0.000 | 0.000 | 0/0/7 | 0 | 1.616 | 0.000 |
| A2-rotation-1/baseline | R2 | 16.433/14.839/14.840/14.839 | 60.952/0.000/0.000 | 0.000 | 0.000 | 0/0/4 | 1 | 2.097 | 0.000 |
| A2-rotation-1/baseline | R3 | 16.696/14.983/14.827/14.948 | 61.454/0.000/0.000 | 26.611 | 0.000 | 5/0/7 | 0 | 1.616 | 0.000 |
| A2-rotation-1/baseline | R4 | 16.048/14.590/14.590/14.590 | 59.817/0.000/0.000 | 0.000 | 0.000 | 0/1/1 | 0 | 2.167 | 0.000 |
| A2-rotation-1/candidate | R1 | 16.396/14.981/14.981/14.980 | 61.338/0.000/0.000 | 0.000 | 0.000 | 0/0/7 | 0 | 1.616 | 0.000 |
| A2-rotation-1/candidate | R2 | 16.433/14.839/14.840/14.839 | 60.952/0.000/0.000 | 0.000 | 0.000 | 0/0/4 | 1 | 2.097 | 0.000 |
| A2-rotation-1/candidate | R3 | 16.696/14.983/14.827/14.948 | 61.454/0.000/0.000 | 26.611 | 0.000 | 5/0/7 | 0 | 1.616 | 0.000 |
| A2-rotation-1/candidate | R4 | 16.048/14.590/14.590/14.590 | 59.817/0.000/0.000 | 0.000 | 0.000 | 0/1/1 | 0 | 2.167 | 0.000 |
| close-rotation-0/baseline | R1 | 16.002/14.546/14.546/14.546 | 59.639/0.000/0.000 | 0.000 | 0.000 | 0/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-0/baseline | R2 | 16.271/14.756/14.756/14.756 | 60.538/0.000/0.000 | 0.000 | 0.000 | 0/0/2 | 0 | 2.200 | 0.000 |
| close-rotation-0/baseline | R3 | 16.551/14.970/14.970/14.970 | 61.461/0.000/0.000 | 0.000 | 0.000 | 1/0/2 | 0 | 2.200 | 0.000 |
| close-rotation-0/baseline | R4 | 16.808/15.118/15.034/14.828 | 61.788/0.000/0.000 | 0.480 | 0.000 | 2/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-0/candidate | R1 | 16.002/14.546/14.546/14.546 | 59.639/0.000/0.000 | 0.000 | 0.000 | 0/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-0/candidate | R2 | 16.271/14.756/14.756/14.756 | 60.538/0.000/0.000 | 0.000 | 0.000 | 0/0/2 | 0 | 2.200 | 0.000 |
| close-rotation-0/candidate | R3 | 16.551/14.970/14.970/14.970 | 61.461/0.000/0.000 | 0.000 | 0.000 | 1/0/2 | 0 | 2.200 | 0.000 |
| close-rotation-0/candidate | R4 | 16.808/15.118/15.034/14.828 | 61.788/0.000/0.000 | 0.480 | 0.000 | 2/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-1/baseline | R1 | 16.262/14.756/14.756/14.756 | 60.530/0.000/0.000 | 0.000 | 0.000 | 0/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-1/baseline | R2 | 16.535/14.972/14.972/14.972 | 61.451/0.000/0.000 | 0.000 | 0.000 | 0/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-1/baseline | R3 | 16.816/15.118/15.066/14.851 | 61.851/0.000/0.000 | 0.000 | 0.000 | 3/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-1/baseline | R4 | 15.998/14.546/14.546/14.546 | 59.635/0.000/0.000 | 0.000 | 0.000 | 0/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-1/candidate | R1 | 16.262/14.756/14.756/14.756 | 60.530/0.000/0.000 | 0.000 | 0.000 | 0/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-1/candidate | R2 | 16.535/14.972/14.972/14.972 | 61.451/0.000/0.000 | 0.000 | 0.000 | 0/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-1/candidate | R3 | 16.816/15.118/15.066/14.851 | 61.851/0.000/0.000 | 0.000 | 0.000 | 3/0/1 | 0 | 2.200 | 0.000 |
| close-rotation-1/candidate | R4 | 15.998/14.546/14.546/14.546 | 59.635/0.000/0.000 | 0.000 | 0.000 | 0/0/1 | 0 | 2.200 | 0.000 |
| rolling-straight/baseline | R1 | 15.092/14.792/14.792/14.772 | 45.948/12.300/1.200 | 49.560 | 12.300 | 1/0/3 | 1 | 0.494 | 0.000 |
| rolling-straight/baseline | R2 | 15.184/14.790/14.790/14.790 | 59.555/0.000/0.000 | 0.000 | 0.000 | 0/0/2 | 0 | 0.600 | 0.000 |
| rolling-straight/baseline | R3 | 14.899/14.693/14.693/14.692 | 58.977/0.000/0.000 | 0.000 | 0.000 | 0/0/1 | 0 | 0.600 | 0.000 |
| rolling-straight/baseline | R4 | 15.292/14.893/14.893/14.893 | 59.970/0.000/0.000 | 0.000 | 0.000 | 0/0/6 | 0 | 0.494 | 0.000 |
| rolling-straight/candidate | R1 | 15.092/14.824/14.710/14.649 | 58.075/1.200/0.000 | 32.130 | 1.200 | 2/0/4 | 1 | 0.494 | 0.000 |
| rolling-straight/candidate | R2 | 15.184/14.790/14.790/14.790 | 59.554/0.000/0.000 | 0.000 | 0.000 | 0/0/3 | 0 | 0.600 | 0.000 |
| rolling-straight/candidate | R3 | 14.899/14.692/14.692/14.692 | 58.976/0.000/0.000 | 0.000 | 0.000 | 0/0/1 | 0 | 0.600 | 0.000 |
| rolling-straight/candidate | R4 | 15.292/14.893/14.892/14.892 | 59.969/0.000/0.000 | 0.000 | 0.000 | 0/0/6 | 0 | 0.494 | 0.000 |
| rolling-bend/baseline | R1 | 15.587/14.792/14.791/14.791 | 59.961/0.000/0.000 | 31.830 | 0.000 | 0/0/3 | 1 | 0.496 | 0.000 |
| rolling-bend/baseline | R2 | 15.637/14.720/14.720/14.720 | 59.796/0.000/0.000 | 0.140 | 0.000 | 1/0/1 | 0 | 0.622 | 0.000 |
| rolling-bend/baseline | R3 | 14.991/14.622/14.622/14.622 | 58.857/0.000/0.000 | 0.000 | 0.000 | 1/0/1 | 0 | 0.622 | 0.000 |
| rolling-bend/baseline | R4 | 15.888/14.893/14.892/14.892 | 60.566/0.000/0.000 | 0.000 | 0.000 | 0/0/3 | 0 | 0.496 | 0.000 |
| rolling-bend/candidate | R1 | 15.587/14.792/14.791/14.791 | 59.961/0.000/0.000 | 31.830 | 0.000 | 0/0/3 | 1 | 0.496 | 0.000 |
| rolling-bend/candidate | R2 | 15.637/14.720/14.720/14.720 | 59.796/0.000/0.000 | 0.140 | 0.000 | 1/0/1 | 0 | 0.622 | 0.000 |
| rolling-bend/candidate | R3 | 14.991/14.622/14.622/14.622 | 58.857/0.000/0.000 | 0.000 | 0.000 | 1/0/1 | 0 | 0.622 | 0.000 |
| rolling-bend/candidate | R4 | 15.888/14.893/14.892/14.892 | 60.566/0.000/0.000 | 0.000 | 0.000 | 0/0/3 | 0 | 0.496 | 0.000 |


## B. Cztery różne odległości i trzy warianty

Wzdłużny prześwit = tył pełnego motocykla lidera minus przód goniącego na wspólnej lokalnej osi. Boczny prześwit = odstęp projekcji pełnych kapsuł na normalnej do osi; ujemny oznacza wspólne pasmo boczne, nie przenikanie. Odległość mechaniczna to minimum odległości rzeczywistych kapsuł chassis/kierownicy przez `MechanicalSeparation`. Różnica postępu to odległość na wewnętrznej referencji toru, a nie odległość zderzeniowa. W łuku różnią się wszystkie cztery miary. Sama bliskość postępu jadących równolegle nie wymaga zamknięcia gazu.

**Podążanie**: najbliższy rywal z `TrafficThrottle.Front` (przed nami, ≤30 m, zgodne styczne, korytarz bieżący/przewidywany), ponadto aktualny prześwit boczny ≤.15 m i wzdłużny 0..10 m. Raportuje rzeczywisty wspólny korytarz, nie sam plan przyszłego przecięcia. **Ciasno**: ≤3 m wzdłużnego prześwitu; **stabilnie**: |closing|≤.5 m/s. Kwantyle p10/p50/p90 i zakres min/max ważone są faktycznym czasem zaakceptowanych interwałów, nie liczbą kroków solvera. Ryzyko: odległość mechaniczna <.3 m i dodatni ruch ku innemu motocyklowi; to diagnostyczna flaga bliskości, nie klasyfikacja kontaktu.

1. #93: `0.6 + 0.1*ownSpeed`, czyli 2.6 m przy 20 m/s.
2. Diagnostyczny: `0.4 + 0.04*ownSpeed`, czyli 1.2 m przy 20 m/s. Wyłącznie wewnętrzny parametr audytu; brak drugiego publicznego modelu AI.
3. Kandydat: `0.6 + c*reaction + c²/(2*max(0.1,-aZero))`, gdzie `c=max(0,closing)` i `aZero` jest już obliczonym przyspieszeniem z niezmienionych Forces dla Drive=0. Przy równym tempie preferencja .6 m; dla c=1, reaction=.24 i aZero=-2 daje 1.09 m, dla c=3 daje 3.57 m. .6 m jest preferencją, nie uniwersalnym bezpiecznym minimum.

Awaryjne kryterium pozostaje dokładnie #93: dodatnie closing>.25 i `gapAfterReaction <= .15 + c²/(2*availableCoastDeceleration)`. Osobno wzór preferencji steruje zwykłym regulatorem. `gapAfterReaction` już odejmuje `c*reaction`; wariant 3 dodaje reakcję również do preferencji, więc pozostawia konserwatywną dodatkową rezerwę, nie jest ścisłym minimum zatrzymania. Nie ma prognozy tajnej komendy rywala, skrócenia reakcji ani dodatkowego hamowania. Ten zapas nie zapewnia reakcji na nagłe nieobserwowane zwolnienie lidera.

Te same 7 krótkich sytuacji, po 3 wzory (21 uruchomień). Profilów ani współczynników nie dostrajano po wynikach. Tabela R1: rzeczywisty rozkład podczas podążania, nie wartości docelowe. Fizyczne minimum jest z całego przypadku, osobno od kwantyli podążania.


| Przypadek | W | Podążanie [s] | Wzdłużny min/p10/p50/p90/max [m] | ≤3 m [s] | Min fiz. [m] | Częściowy [s] | Solver | Gaz komendy | Ataki |
|---|---|---|---|---|---|---|---|---|---|
| equal-close | 1 | 4.960 | 1.100/1.108/2.110/3.578/3.780 | 3.870 | 1.100 | 2.100 | 502 | 9 | 1 |
| equal-close | 2 | 4.530 | 1.074/1.100/1.100/2.228/2.787 | 4.530 | 1.078 | 1.300 | 503 | 4 | 1 |
| equal-close | 3 | 4.530 | 1.061/1.093/1.100/2.217/2.781 | 4.530 | 1.065 | 0.300 | 502 | 2 | 1 |
| catch-slower | 1 | 6.000 | 2.839/2.888/5.221/8.763/9.900 | 0.965 | 0.552 | 1.660 | 734 | 9 | 0 |
| catch-slower | 2 | 6.000 | 2.214/2.300/4.838/8.741/9.900 | 1.470 | 0.536 | 1.260 | 700 | 6 | 0 |
| catch-slower | 3 | 6.000 | 2.482/2.707/5.247/8.765/9.900 | 1.070 | 0.539 | 2.160 | 739 | 11 | 0 |
| leader-bend-slowdown | 1 | 1.570 | 2.962/2.980/3.376/4.102/4.284 | 0.220 | 3.165 | 1.800 | 501 | 8 | 1 |
| leader-bend-slowdown | 2 | 1.570 | 2.962/2.980/3.376/4.102/4.284 | 0.220 | 3.165 | 1.800 | 501 | 8 | 1 |
| leader-bend-slowdown | 3 | 1.570 | 2.962/2.980/3.376/4.102/4.284 | 0.220 | 3.165 | 1.800 | 501 | 8 | 1 |
| closing-sign-change | 1 | 4.540 | 3.749/3.797/4.372/4.774/4.786 | 0.000 | 3.865 | 1.800 | 602 | 8 | 1 |
| closing-sign-change | 2 | 4.540 | 3.711/3.732/4.372/4.774/4.786 | 0.000 | 3.859 | 0.300 | 602 | 2 | 1 |
| closing-sign-change | 3 | 4.540 | 3.710/3.731/4.372/4.774/4.786 | 0.000 | 3.858 | 0.900 | 602 | 4 | 1 |
| follow-attack | 1 | 4.590 | 3.621/3.661/5.208/8.937/9.900 | 0.000 | 0.600 | 4.300 | 1130 | 15 | 1 |
| follow-attack | 2 | 4.240 | 2.657/2.698/4.884/9.002/9.900 | 0.830 | 0.600 | 3.700 | 1131 | 15 | 1 |
| follow-attack | 3 | 4.490 | 3.281/3.367/5.709/8.959/9.900 | 0.000 | 0.600 | 4.400 | 1131 | 14 | 1 |
| parallel | 1 | 0.000 | —/—/—/—/— | 0.000 | 2.200 | 0.000 | 402 | 0 | 2 |
| parallel | 2 | 0.000 | —/—/—/—/— | 0.000 | 2.200 | 0.000 | 402 | 0 | 2 |
| parallel | 3 | 0.000 | —/—/—/—/— | 0.000 | 2.200 | 0.000 | 402 | 0 | 2 |
| three-shared-space | 1 | 5.510 | 1.685/1.770/2.890/9.506/9.997 | 2.850 | 1.685 | 5.100 | 603 | 21 | 1 |
| three-shared-space | 2 | 6.000 | 1.078/1.605/2.047/4.568/4.829 | 3.930 | 0.574 | 8.000 | 734 | 26 | 2 |
| three-shared-space | 3 | 4.750 | 1.796/1.806/1.870/5.753/5.914 | 3.740 | 1.330 | 4.100 | 603 | 16 | 2 |


Wszystkie 21 bez kontaktów i zarejestrowanego zagrożenia. Wariant 3 wybrano po skróceniu dystansu w equal-close i three-shared-space, przy zachowanym ataku i bez wzrostu solvera w tym drugim. Wariant 2 w trzech motocyklach daje 603→734 wywołania i min fizyczne 1.685→.574 m; wariant 3: 603 i 1.330 m. Nie wszędzie 3 zbliża: follow-attack ma medianę 5.709 wobec 5.208 m bazy. Sytuacja closing-sign-change rzeczywiście zmienia znak zbliżania; parallel nie ma ograniczeń napędu; leader-bend-slowdown zachowuje opóźnienie i odpowiedź na geometrię. Żaden wynik nie został odrzucony jako „nieładny”.

Rozkłady pełnych przejazdów z podążaniem (pozostałe nie mają takiej populacji). Osobne mediany boczne/fizyczne/postępu są kwantylami swoich zmiennych, nie czterema jednoczesnymi wartościami w tej samej chwili.


| Próba | R | Podążanie [s] | Wzdłużny min/p10/p50/p90/max [m] | Boczny p50 | Fizyczny p50 | Postęp p50 | Stabilnie [s] | ≤3 m [s] |
|---|---|---|---|---|---|---|---|---|
| A2-rotation-1/baseline | R3 | 26.611 | 5.835/6.203/7.549/9.551/9.994 | -0.107 | 7.569 | 8.566 | 13.441 | 0.000 |
| A2-rotation-1/candidate | R3 | 26.611 | 5.835/6.203/7.549/9.551/9.994 | -0.107 | 7.569 | 8.566 | 13.441 | 0.000 |
| close-rotation-0/baseline | R4 | 0.480 | 9.484/9.537/9.787/9.972/9.998 | -0.259 | 9.841 | 9.740 | 0.050 | 0.000 |
| close-rotation-0/candidate | R4 | 0.480 | 9.484/9.537/9.787/9.972/9.998 | -0.259 | 9.841 | 9.740 | 0.050 | 0.000 |
| rolling-straight/baseline | R1 | 49.560 | 3.638/3.699/3.778/4.146/5.900 | -0.845 | 3.801 | 5.178 | 44.800 | 0.000 |
| rolling-straight/candidate | R1 | 32.130 | 3.639/3.694/3.804/4.872/5.900 | -0.800 | 3.815 | 5.548 | 26.110 | 0.000 |
| rolling-bend/baseline | R1 | 31.830 | 4.318/4.959/7.166/9.413/10.000 | 0.033 | 7.265 | 8.339 | 26.430 | 0.000 |
| rolling-bend/baseline | R2 | 0.140 | 9.910/9.915/9.952/9.986/9.993 | 0.083 | 9.997 | 11.647 | 0.000 | 0.000 |
| rolling-bend/candidate | R1 | 31.830 | 4.318/4.959/7.166/9.413/10.000 | 0.033 | 7.265 | 8.339 | 26.430 | 0.000 |
| rolling-bend/candidate | R2 | 0.140 | 9.910/9.915/9.952/9.986/9.993 | 0.083 | 9.997 | 11.647 | 0.000 | 0.000 |


**Wynik negatywny pełnych prób:** mniejsza preferencja nie zmusza AI do jechania w docelowym odstępie. Obecna geometria łuków, neutral carry, pełny gaz przy równych możliwościach, wyjścia do SideBySide i zmiany linii dominują nad preferencją. W rolling-straight mediana nie maleje, zero sekund poniżej 3 m; w pozostałych pięciu pełnych konfiguracjach kandydat zachowuje dokładny hash bazy. Nie rozbudowujemy AI ani nie zmieniamy profili, żeby uzyskać ocenę A.

## C. Rzeczywiste cztery motocykle: gaz → atak → fizyczna zmiana linii

Przykład **rolling-straight/baseline**, normalna AI wszystkich czterech. R1 podąża za R2 przez wspólny korytarz; ma wcześniej i w tym fragmencie częściowy napęd. W 47.000 s obserwuje możliwość AttackInside, żądanie 47.236 s, aktywacja 47.236 s — pełne .236 s reakcji. Target 2.612 m; fizyczny offset po aktywacji zmienia się stopniowo z około 3.999 do 2.917 m, bez teleportacji. To wykonany ruch ofensywny; nie tylko intencja.

Tabela pokazuje zaakceptowane stany; próbka dokładnie na granicy aktywacji przedstawia stan przed kolejnym krokiem, polecenie jest osobno potwierdzone w Decisions. Prędkości m/s, odstępy/offsety m. „Zamiar” wynika z aktywowanej kolejki Line.


| Czas | Gaz | Prędkość R1 | Prędkość R2 | Wzdłużny | Boczny | Fizyczny | Offset R1 | Target | Zamiar |
|---|---|---|---|---|---|---|---|---|---|
| 46.500 | 0.500 | 24.038 | 23.681 | 3.985 | -0.775 | 3.985 | 3.997 | 4.000 | podążanie |
| 47.000 | 0.500 | 23.354 | 22.999 | 3.806 | -0.780 | 3.806 | 3.999 | 4.000 | AttackInside obserwacja |
| 47.236 | 0.500 | 23.031 | 22.874 | 3.741 | -0.742 | 3.744 | 3.999 | 4.000 | AttackInside granica |
| 48.005 | 0.750 | 21.502 | 22.886 | 4.391 | -0.765 | 4.400 | 3.856 | 2.612 | AttackInside ruch |
| 49.000 | 1.000 | 21.963 | 22.896 | 5.184 | -0.090 | 5.197 | 3.152 | 2.612 | AttackInside ruch |
| 49.500 | 1.000 | 22.127 | 22.895 | 5.282 | 0.133 | 5.314 | 2.917 | 2.612 | AttackInside ruch |


**Wynik tej próby:** R1 nie potwierdza minięcia R2; R1 kończy 59.448 s, R2 59.555 s, lecz sama kolejność czasów czterech obiegów od różnych początków nie jest dowodem minięcia. Jedyne potwierdzone minięcie tego przejazdu to R1/R4 w 45.020 s, przed powyższym atakiem, więc nie przypisujemy go atakowi na R2.

Wariant 3 również ma częściowe podążanie (1.200 s), potem ataki `ContestInside` na R4: obserwacje 25.600/33.300, aktywacje 25.836/33.536 s. Fizyczny offset R1 3.994→3.100 m po pierwszym i 2.987→2.058 m po drugim. Potwierdzone R1/R4 następuje w 44.330 s; nie mylimy jego przeciwnika z R2, którego nadal wyznacza regulator Front. To zachowana możliwość walki, lecz prześwit podążania pozostaje około 3.8 m. Brakuje dowodu pełnej czteroosobowej jazdy przy mniejszych prześwitach z krótkich prób.

## D. Wydajność i pochodzenie podziałów

Poniżej dwie identyczne długości okna 8 s; OFF = stara reaktywna taktyka bez proporcjonalnego gazu, #93 = proporcjonalny stary odstęp, nowy = wariant 3. Bare WorldRunner bez obserwatorów; jedna rozgrzewka, trzy mierzone powtórzenia każdej konfiguracji, .NET 8 Release, `DOTNET_TieredCompilation=0`, ta sama maszyna. Czas/AI to mediany; alokacje na głównym wątku obejmują konstrukcję/Run/Dispose. Hashe trzech powtórzeń każdej konfiguracji identyczne. Nie są to trzy różne zestawy profili.


| Okno | Tryb | Czas sim [s] | Solver | Solver/s | Substeps | Wall [ms] | AI [ms] | MiB | Proximity [s] | Gaz aktywacje |
|---|---|---|---|---|---|---|---|---|---|---|
| A-blocked-follow | OFF | 8.000 | 899 | 112.375 | 3596 | 20.395 | 0.157 | 2.925 | 0.910 | 0 |
| A-blocked-follow | #93 | 8.000 | 1138 | 142.250 | 4552 | 26.314 | 0.440 | 3.547 | 3.310 | 13 |
| A-blocked-follow | nowy | 8.000 | 943 | 117.875 | 3772 | 21.182 | 0.401 | 3.080 | 1.360 | 12 |
| rolling-straight | OFF | 8.000 | 1003 | 125.375 | 4012 | 21.987 | 0.366 | 3.224 | 1.900 | 0 |
| rolling-straight | #93 | 8.000 | 973 | 121.625 | 3892 | 21.706 | 0.800 | 3.307 | 1.570 | 4 |
| rolling-straight | nowy | 8.000 | 971 | 121.375 | 3884 | 21.690 | 0.806 | 3.304 | 1.570 | 2 |

| Okno | Tryb | Bazowe ticki | Proximity extra nominal | Reszta calls | Curvature clip | Deadline clip | w tym gaz | Force próby gazu |
|---|---|---|---|---|---|---|---|---|
| A-blocked-follow | OFF | 800 | 91 | 8 | 8 | 0 | 0 | 0 |
| A-blocked-follow | #93 | 800 | 331 | 7 | 7 | 2 | 2 | 228 |
| A-blocked-follow | nowy | 800 | 136 | 7 | 7 | 2 | 2 | 185 |
| rolling-straight | OFF | 800 | 190 | 13 | 10 | 3 | 0 | 0 |
| rolling-straight | #93 | 800 | 157 | 16 | 9 | 7 | 4 | 183 |
| rolling-straight | nowy | 800 | 157 | 14 | 9 | 5 | 2 | 163 |


`ProximitySeconds` to czas z istniejącym `NearbySubdivisions>1`, zależnym od odległości, prędkości względnej i obrotu kapsuł; nie jest czasem dowolnej jazdy w pobliżu. Nominalny budżet = ticki + suma(subdivisions−1). Reszta to dodatkowe wywołania po istniejących granicach krzywizny/reakcji. Liczniki clip opisują wywołania ograniczone na końcu przez dany warunek, nie addytywną liczbę nowych kroków: krzywizna może wygrać z wcześniejszym deadline, a ułamki zmiennoprzecinkowe przy końcu ticka mogą ograniczyć czas bez nowego kroku. Gaz to podzbiór deadline; jego aktywacja nie zawsze tworzy nową granicę. Cztery substeps każdego wywołania pozostają stałe.

W historycznym A wzrost OFF→#93 o 239 wywołań (899→1138) odpowiada przede wszystkim +240 nominalnym podziałom proximity (91→331); krzywizna 8→7, tylko 2 clip deadline gazu. Sam regulator robi więcej pracy AI, ale nie nakazuje +26.6% kroków fizyki. Kandydat ma 943 wywołania i tylko 136 extra proximity. Trajektorie oraz czas pozostawania blisko różnią się, więc spadek wall nie jest optymalizacją solvera. W rolling 8 s geometria jest podobna (#93/nowy po 157 extra proximity), liczba deadline gazu maleje 4→2, solver 973→971. Brak materialnego narzutu nowej formuły w tym oknie; nie gwarantuje to kosztu w innych tłokach.

Pełne przebiegi poniżej są pojedynczymi **obserwowanymi** pomiarami (z kosztowną diagnostyką geometrii/sił i JIT), a nie porównaniem szybkości algorytmu. Różnią się długością świata i trajektorią. Bare pomiar powyżej oddziela koszt zbierania raportu.


| Próba | Sim [s] | Solver | Solver/s | Substeps | Wall obserw. ms | AI ms | MiB obserw. | Proximity s | Gaz aktywacje |
|---|---|---|---|---|---|---|---|---|---|
| A1-rotation-0/baseline | 62.590 | 6333 | 101.182 | 25332 | 1015.983 | 28.120 | 92.238 | 0.000 | 0 |
| A1-rotation-0/candidate | 62.590 | 6333 | 101.182 | 25332 | 1044.344 | 28.569 | 92.232 | 0.000 | 0 |
| A2-rotation-1/baseline | 61.460 | 6237 | 101.481 | 24948 | 415.045 | 12.684 | 92.217 | 0.000 | 0 |
| A2-rotation-1/candidate | 61.460 | 6237 | 101.481 | 24948 | 391.287 | 12.417 | 92.217 | 0.000 | 0 |
| close-rotation-0/baseline | 61.790 | 6267 | 101.424 | 25068 | 328.119 | 11.134 | 91.209 | 0.000 | 0 |
| close-rotation-0/candidate | 61.790 | 6267 | 101.424 | 25068 | 357.250 | 11.996 | 91.212 | 0.000 | 0 |
| close-rotation-1/baseline | 61.860 | 6267 | 101.309 | 25068 | 243.329 | 10.475 | 91.152 | 0.000 | 0 |
| close-rotation-1/candidate | 61.860 | 6267 | 101.309 | 25068 | 256.247 | 11.903 | 91.152 | 0.000 | 0 |
| rolling-straight/baseline | 59.980 | 7156 | 119.306 | 28624 | 1180.289 | 32.190 | 107.454 | 10.310 | 52 |
| rolling-straight/candidate | 59.970 | 6462 | 107.754 | 25848 | 257.981 | 6.516 | 96.081 | 3.810 | 8 |
| rolling-bend/baseline | 60.570 | 6927 | 114.364 | 27708 | 926.607 | 26.718 | 102.899 | 7.930 | 0 |
| rolling-bend/candidate | 60.570 | 6927 | 114.364 | 27708 | 263.266 | 4.371 | 102.897 | 7.930 | 0 |


## E. Ocena, regresje i odtwarzanie

**B — CZĘŚCIOWO.** Bazowy #93 współpracuje z normalną walką czterech motocykli. Nowy model ma mierzalną korzyść w kontrolowanej parze i trzech wspólnych korytarzach, lecz pełny zestaw nie potwierdza utrzymania mniejszych odstępów. Zostaje najwyżej jeden nowy eksperymentalny wzór, domyślnie OFF; brak dodatkowej AI, strojenia osiągów, zmian reakcji czy sił. Ocena nie zależy od liczby minięć ani szybszej mety.

Nowe regresje sprawdzają flagę/zależność, wzór, dokładne osiem #93 ON, pełne obiegi Rolling i brak komend po własnej mecie, poprawne kapsuły i brak sztucznego początkowego minięcia, siedem sytuacji, zachowanie nieuniknionego Crash i pełnej reakcji, samotną jazdę i odwróconą kolekcję. Dotychczasowe testy #93 zachowują oddzielne kolejki, anulowanie niebezpiecznej linii, nieprzesuwanie reakcji i rzeczywisty kontakt. Trzy powtórzenia nowego modelu z odwróconą kolekcją w osobnym procesie zachowują hashe, komendy i kontakty. [Powtórzenia](close-racing-following-gap/repeats.json), [kontrole pochodne](close-racing-following-gap/derived-checks.json), [wydajność](close-racing-following-gap/performance.json).

Polecenia (z katalogu repozytorium, .NET 8/Windows):

```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
$audit = 'tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll'
$out = 'work/close-racing-reproduction' # nowy katalog, nie nadpisuje dowodów
dotnet $audit close-racing $out freeze
dotnet $audit close-racing $out rolling-first
dotnet $audit close-racing $out baseline
dotnet $audit close-racing $out variants
# po ocenie pomiarów zapisz selection.md; zamrożony wybór tutaj to wariant 3
Copy-Item docs/experiments/close-racing-following-gap/selection.md $out
dotnet $audit close-racing $out candidate
dotnet $audit close-racing $out repeat
$env:DOTNET_TieredCompilation='0'
dotnet $audit close-racing $out benchmark
python tools/shared-world-speedway/close-racing-report.py $out
```

Generator tylko czyta wyniki; weryfikuje bazową pieczęć, czasy czterech obiegów, wszystkie aktywacje, ukończenia, historyczne hashe okien i powtórzenia, następnie zapisuje raport i manifest. Nie wykonuje symulacji ani optymalizacji współczynników. `.gitattributes` zachowuje bajty dowodów na Windows. Ograniczenie: krótka prosta nie daje dowodu dowolnie długiego podążania .6 m; rzeczywisty lider może zwolnić szybciej niż dopuszcza reakcja. Nie usunięto tego ryzyka.



Walidacja lokalna: restore + Release warnings-as-errors build; 133/133 celowanych testów (15 nowych); discovery 2646/2646 bez braków/duplikatów; guard 39 zamrożonych produkcyjnych konsumentów; trzy reverse repeats i dokładne historyczne hashe okien OFF/ON. Python 89+7; src/Forces/ContactAdapter/workflow i dowody #92/#93 bez zmian.
