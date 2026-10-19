# Proportional Throttle: Following, Safety and Reattack (#93)

**PROVISIONAL — A: DZIAŁA w zbadanym zestawie.** Mechanizm potrafi dopasować napęd do rywala częściowym gazem, następnie aktywować rzeczywisty manewr ofensywny. Zmniejsza zbędne zamknięcia na rozdzielonych liniach, bez nowych kontaktów w badanych oknach i biegach. Ocena opiera się na geometrii, zmianie prędkości, odstępie, wykonanym ataku i zachowanym kontakcie w D, a nie na szybszych czasach. To nie jest kalibracja ani dowód bezpieczeństwa we wszystkich sytuacjach. Flaga **EnableProportionalTrafficThrottle=false domyślnie** pozostaje wyłączona zgodnie z zadaniem.

Baza: Draft #92, `experiment/full-heat-and-solo-validation`, dokładny HEAD `8cd7dc67f66c798a1ba452f57dccad79fe6d5386`. Jeden nowy Draft, bez merge. `src/`, `RiderController.Forces`, Box2D/ContactAdapter, napęd, grip, geometria, masa motocykla, severity, umiejętności i scoring #91 bez zmian. Zmienia się tylko eksperymentalna decyzja oraz dwie małe kolejki sterowania.

## 1. Diagnoza #92: kiedy gaz=0 nie wynikał z zagrożenia

[Dokładne odtworzenie obu biegów i stany epizodów](proportional-throttle-control/diagnosis-92.json). Hashe A1/0 i A2/1 zgadzają się dokładnie z #92. NoPassingCorridor następowało zarówno po braku wolnego korytarza, wyjściu targetu poza możliwości ruchu, jak i po `gain<=0`. Wszystkie kończyły się BackOut/Drive=0. ClearResume wymagało potem braku rywala lub >8 m i czasu czystej obserwacji; każde wykonanie sterowania przedłużało NextTacticAt o 0.8 s.

Poniżej stan w chwili obserwacji, przed pełnym opóźnieniem reakcji. Prześwit wzdłużny i boczny obejmuje kapsuły podwozia/kierownicy i orientację obu motocykli. Ujemny prześwit wzdłużny przy dodatnim bocznym nie oznacza kontaktu. TTC w surowych danych jest warunkowym czasem zetknięcia projekcji wzdłużnych; bez wspólnego korytarza nie oznacza przewidywanego zderzenia.


| Bieg | Zawodnik | Obserwacja s | Powód #92 | Prześwit wzdłużny m | Boczny m | Zbliżanie m/s | Korytarz wspólny/przecinany |
|---|---|---|---|---|---|---|---|
| A1-rotation-0 | 2 | 10.400 | NoPassingCorridor | 0.011 | 2.199 | 1.116 | False |
| A1-rotation-0 | 3 | 10.500 | NoPassingCorridor | 0.318 | 2.194 | 1.085 | False |
| A1-rotation-0 | 4 | 10.600 | NoPassingCorridor | 0.576 | 2.196 | 1.056 | False |
| A2-rotation-1 | 2 | 9.100 | NoPassingCorridor | 0.391 | 2.203 | 1.296 | False |
| A2-rotation-1 | 2 | 37.000 | ImmediateThreat | 0.274 | 0.609 | 0.091 | False |
| A2-rotation-1 | 3 | 39.100 | NoPassingCorridor | 1.436 | 5.798 | 3.645 | False |
| A2-rotation-1 | 2 | 42.400 | ImmediateThreat | 0.235 | 0.690 | -0.001 | False |
| A2-rotation-1 | 2 | 44.700 | ImmediateThreat | -0.571 | 0.714 | -0.751 | False |
| A2-rotation-1 | 3 | 48.100 | NoPassingCorridor | 6.604 | 5.711 | 2.607 | False |
| A2-rotation-1 | 2 | 50.200 | ImmediateThreat | 0.159 | 0.718 | -0.011 | False |
| A2-rotation-1 | 2 | 52.600 | ImmediateThreat | -0.709 | 0.743 | -0.617 | False |
| A2-rotation-1 | 3 | 55.900 | NoPassingCorridor | 9.048 | 5.544 | 2.451 | False |
| A2-rotation-1 | 2 | 57.900 | ImmediateThreat | 0.167 | 0.749 | -0.021 | False |


Trzy A1 NoPassingCorridor miały ok. 2.2 m bocznego prześwitu, bez przecinania utrzymywanej ścieżki. Zerowanie nie było uzasadnione samym doganianiem na innej linii. Podobnie sześć późnych ImmediateThreat R2 w A2/1 miało 0.609–0.749 m bocznego prześwitu, a w kilku z nich wzdłużne zbliżanie było już niedodatnie. Stary prostoliniowy closest-approach w łuku nie zachowywał wspólnego obrotu toru i identyfikował taką sytuację jako zagrożenie. To ocena tych konkretnych epizodów, nie zasada, że każde historyczne odpuszczenie było błędem. Niebezpieczny aktualny lub oczekujący ruch w bok nadal wymaga anulowania; obecna separacja sama nie gwarantuje, że można kontynuować przecinanie.

Ścieżki: NoPassingCorridor ma teraz przy ON rozdzielone noty `AttackUnreachable` (target poza zakresem), `AttackCorridorUnavailable` (brak zaakceptowanego korytarza) i `NoAttackGain` (korytarz dopuszczalny, brak zysku). Są diagnostyką decyzji ataku, bez prawa do zamykania gazu. AttackNoLongerFeasible i ContestCorridorOccupied nadal zatrzymują/przerywają cel boczny; tylko osobna ocena zajętości i dynamiki decyduje o napędzie. ImmediateThreat pochodzi z nowej oceny mechanicznej. AbortThreatenedCrossing/CorridorOccupied mogą zatrzymać zmianę linii bez Drive=0. ResumeContest/ResumeIntent/ClearResume nie są przy ON osobnymi komendami bocznymi: napęd wznawia niezależny kanał ResumeAcceleration, bez wymagania oddalenia się rywala na 8 m. OFF wykonuje wszystkie oryginalne ścieżki.

## 2. Niewielki regulator i jego granice

[Konfiguracja](proportional-throttle-control/configuration.json). Co 100 ms jeden wspólny snapshot, ten sam świat i te same 4 substeps przy kroku 10 ms. Pary mają ten sam limit obserwacji 30 m. W pełnych biegach Tactical=true, SideBySide=true, Quality=false; ON/OFF poniżej oznacza **tylko nową flagę**, a nie wyłączenie taktyki. Wymagane Tactical=true; SideBySide nie jest nowym wymogiem. Ta sama Motoarena, wszystkie komórki Grip=1/Ruts=0/Moisture=0.5/EffectiveGrip=0.9175, bez zużycia i losowości. Osiem profili/gate rotations #92 bez zmian.

1. Pełne mechaniczne extents na wspólnej lokalnej osi stycznych dają gap między przodem doganiającego i tyłem poprzedzającego. Oddzielnie liczymy boczny odstęp kapsuł. Rozwinięta referencja służy do wykluczenia rywala z tyłu/zdublowanego, nie zastępuje prześwitu. Kierunki muszą być zgodne (dot≥0.85). Na granicy prosta–łuk nie dzielimy przez promień ani długość łuku.
2. Korytarz jest zajęty przy obecnej bocznej bliskości ≤0.15 m albo krótkim przecinaniu. Wykorzystujemy własny zadany target i istniejącą analityczną odpowiedź PD przez reakcję+0.6 s oraz lokalną prędkość boczną rywala. Nie czytamy jego targetu, planu, przyszłych komend ani stanu poza zaakceptowanym snapshotem. To ograniczona obserwacja, nie gwarancja przyszłej separacji i nie solver kolizji.
3. Gap po reakcji: `gR = g - max(0, closing)*reaction`. Żądany odstęp `g* = 0.6 m + 0.1 s * ownSpeed`. Czas odpowiedzi τ=1.2 s. `desiredAcceleration = observedOpponentAcceleration + (gR-g*)/τ² - 2*closing/τ`. Przyspieszenie rywala pochodzi z poprzedniej zaakceptowanej obserwacji 100 ms, ograniczonego do ±5 m/s² wejścia estymatora, bez nowej siły. Przy pierwszej stabilnej obserwacji podobnej prędkości nie zmieniamy gazu tylko przez bliskość.
4. Żądane przyspieszenie odwracamy przez **tę samą czystą funkcję RiderController.Forces**, w aktualnym stanie własnego motocykla: 0, 1, mały dodatni napęd i najwyżej 4 kroki bisekcji z interpolacją bracketu. Maksymalnie 7 ocen siły na aktywną ocenę podążania, zero dla wolnego korytarza. Nie ma kroku czasu, nowego stanu ciała, alternatywnego świata ani kopiowania równań siły. W ten sposób uwzględnione są rzeczywiste legacy, masa, setup, fade, opory, krzywizna, scrub, impairment i krąg przyczepności. Inercja przekazana do tej czystej oceny nie wpływa na zwracaną siłę; torque nie jest aplikowany do świata. Gas nie jest liniowym przelicznikiem prędkości.
5. Zwykła korekta jest ograniczona do ±0.25 Drive na nową komendę, martwa strefa 0.04. To ciągła wartość, nie trzy stopnie gazu. Bez istotnego zagrożenia Drive wraca do 1. Emergency jest rozpoznawane przy dodatnim zbliżaniu >0.25 m/s i `gR <= 0.15 m + closing²/(2*availableCoastDeceleration)`, z rzeczywistym przyspieszeniem 0-drive z tej samej funkcji; minimalne 0.1 m/s² w mianowniku zapobiega nieograniczonej estymacie czasu przy niemal zerowej skuteczności. Jest to konserwatywny krótki test przy stałej obserwowanej prędkości rywala. Emergency może natychmiast wybrać *komendę* 0 bez rampy, ale nadal czeka na reakcję. Nie dodaje siły hamowania.

W obecnym modelu dodatni napęd przed apexem może dawać dokładnie zerową siłę styczną (`DriveAvailability=0`), a Drive=0 ujawnia opory. Nie da się wtedy uzyskać małego ujemnego przyspieszenia samym dodatnim gazem. Regulator jawnie sprawdza ten skok oraz brak autorytetu napędu. Dlatego ON nadal może uzasadnienie całkowicie odjąć gaz: w A na 1.495 s, w C na 0.700 s i w G na 0.600 s. Są to korekty Following do aktualnego odstępu/tempa w fazie łuku, nie reakcja na sam brak zysku ataku. Nie poprawiono tej własności fizyki w PR #93.

## 3. Kolejki i współpraca z manewrem

Maksymalnie jedna oczekująca komenda linii i jedna gazu na zawodnika. Opcjonalny `ControlChannels` w DecisionEvent domyślnie Both zachowuje dotychczasowe API/OFF. Przy ON line aktualizuje wyłącznie TargetOffsetMeters/intencję i jej 0.8 s histerezy; throttle wyłącznie Drive. Stara komenda linii zawierająca Drive=1 nie otworzy gazu po nowszej korekcie. Korekty gazu nie resetują NextTacticAt i nie blokują szukania następnego ataku.

Każda kolejka zachowuje pełny istniejący czas reakcji od własnej obserwacji. Zwykłych oczekujących korekt nie nadpisujemy i nie przesuwamy ich deadline co 100 ms. Nowe Emergency anuluje oczekującą zwykłą korektę, także nieaktualne otwarcie, a nowa komenda ma własną pełną reakcję. Już oczekująca komenda bezpieczeństwa nie jest stale anulowana kolejną obserwacją. Zajęcie przecinanej ścieżki może odrzucić oczekującą linię; nie musi odjąć całego gazu. Terminalny Crashed/Finished przy ON anuluje obie pozostałe kolejki, więc nie aktywuje komendy po własnej mecie. OFF zachowuje również historyczny przypadek po mecie #92.

Nowa granica solvera powstaje wyłącznie dla rzeczywistej aktywacji istniejącego mechanizmu deadline; brak nowych odczytów świata, częstotliwość AI nadal 100 ms. Logical Hold nie rozcina czasu tylko przez nieużywaną wartość Drive w kanale Line. Bezpieczeństwo, zajęta przestrzeń i wykonanie manewru pozostają przed poszukiwaniem zysku. Podążanie trwa dopóki motocykle fizycznie współdzielą/przecinają korytarz, a nie dopóki zamiar nazywa się Attack.

## 4. Próby A–G: porównanie fizycznych wyników

Wszystkie jawne wejścia są w JSON każdej próby, w tym kanoniczne/legacy, rzeczywiste pozycje, prędkości i TrafficEnabled. A/C mają cztery motocykle, aby rzeczywiście zająć obie alternatywy ataku: tylko R1 ma AI; trzej pozostali jadą przez tę samą fizykę z ustalonymi z góry celami. Nie zamrażamy ich pozycji ani prędkości. W A R1=18 m/s, przód=16 m/s, środki początkowo 12 m od siebie, flankujący na 2.6/5.4 m. C zmienia jawnie możliwości napędu i początkowe tempo flankujących, którzy fizycznie odjeżdżają, odsłaniając korytarz; bez zdarzenia teleportującego przeszkodę. B/D/E/F/G są dwiema maszynami. F porównuje istniejące pary Speed/SlideControl 20/20 i 80/80, nie wnioskuje z nich o kanonicznym Attack. G zaczyna przy 32/38 m, po dwóch stronach wejścia w łuk.

`Completed` w krótkich próbach oznacza wykonane okno 2–10 s, nie cztery okrążenia. W pełnych biegach wymagamy czterech okrążeń, z limitem 90 s. Wszystko stopOnCrash=true. Min. dystans to mechaniczna separacja wszystkich kapsuł, a nie różnica centrów. Niebezpieczne zbliżenie: rzeczywista separacja <0.3 m przy dodatnim zbliżaniu >0.1 m/s do przodu, zliczana jako epizod i czas; miara diagnostyczna, nie nowe kryterium kolizji.


| Próba | Status / czas s | Zero / częściowy s (suma) | Zamknięcia / epizody częściowe | Min. cały motocykl m | Niebezpieczne epizody / s | Ponowny atak | Kontakty |
|---|---|---|---|---|---|---|---|
| [A-blocked-follow-OFF](proportional-throttle-control/A-blocked-follow-OFF.json) | Completed / 8.000 | 5.050 / 0.000 | 1 / 0 | 0.6110 | 0 / 0.000 | 0 | 0 |
| [A-blocked-follow-ON](proportional-throttle-control/A-blocked-follow-ON.json) | Completed / 8.000 | 1.495 / 2.660 | 1 / 5 | 0.5501 | 0 / 0.000 | 1 | 0 |
| [B-steady-close-OFF](proportional-throttle-control/B-steady-close-OFF.json) | Completed / 3.000 | 0.000 / 0.000 | 0 / 0 | 3.8311 | 0 / 0.000 | 0 | 0 |
| [B-steady-close-ON](proportional-throttle-control/B-steady-close-ON.json) | Completed / 3.000 | 0.000 / 0.050 | 0 / 1 | 3.8311 | 0 / 0.000 | 0 | 0 |
| [C-follow-reattack-OFF](proportional-throttle-control/C-follow-reattack-OFF.json) | Completed / 10.000 | 3.400 / 0.000 | 1 / 0 | 0.0755 | 1 / 0.525 | 0 | 0 |
| [C-follow-reattack-ON](proportional-throttle-control/C-follow-reattack-ON.json) | Completed / 10.000 | 0.700 / 4.307 | 1 / 2 | 0.6000 | 0 / 0.000 | 1 | 0 |
| [D-immediate-threat-OFF](proportional-throttle-control/D-immediate-threat-OFF.json) | Interrupted-Crash / 0.504 | 0.154 / 0.000 | 2 / 0 | 0.0000 | 1 / 0.032 | 0 | 1 |
| [D-immediate-threat-ON](proportional-throttle-control/D-immediate-threat-ON.json) | Interrupted-Crash / 0.504 | 0.154 / 0.000 | 2 / 0 | 0.0000 | 1 / 0.032 | 0 | 1 |
| [E-safe-parallel-OFF](proportional-throttle-control/E-safe-parallel-OFF.json) | Completed / 4.000 | 0.000 / 0.000 | 0 / 0 | 2.2000 | 0 / 0.000 | 0 | 0 |
| [E-safe-parallel-ON](proportional-throttle-control/E-safe-parallel-ON.json) | Completed / 4.000 | 0.000 / 0.000 | 0 / 0 | 2.2000 | 0 / 0.000 | 0 | 0 |
| [F-low-capability-OFF](proportional-throttle-control/F-low-capability-OFF.json) | Completed / 3.000 | 0.000 / 0.000 | 0 / 0 | 6.5890 | 0 / 0.000 | 0 | 0 |
| [F-low-capability-ON](proportional-throttle-control/F-low-capability-ON.json) | Completed / 3.000 | 0.000 / 0.300 | 0 / 1 | 6.6643 | 0 / 0.000 | 0 | 0 |
| [F-high-capability-OFF](proportional-throttle-control/F-high-capability-OFF.json) | Completed / 3.000 | 0.000 / 0.000 | 0 / 0 | 3.1522 | 0 / 0.000 | 0 | 0 |
| [F-high-capability-ON](proportional-throttle-control/F-high-capability-ON.json) | Completed / 3.000 | 0.000 / 1.950 | 0 / 2 | 4.5280 | 0 / 0.000 | 1 | 0 |
| [G-straight-bend-OFF](proportional-throttle-control/G-straight-bend-OFF.json) | Completed / 3.000 | 0.000 / 0.000 | 0 / 0 | 3.1513 | 0 / 0.000 | 0 | 0 |
| [G-straight-bend-ON](proportional-throttle-control/G-straight-bend-ON.json) | Completed / 3.000 | 0.600 / 1.800 | 1 / 2 | 3.1653 | 0 / 0.000 | 0 | 0 |


A: częściowy napęd utrzymuje rzeczywisty odstęp; minimum całych motocykli 0.550 m zamiast 0.611 m, bez kontaktu i bez epizodu <0.3 m. Nie uznajemy mniejszego prześwitu samego w sobie za zaletę. C: R1 podąża częściowo ponad 0.3 s, a następnie otrzymuje i fizycznie wykonuje AttackOutside (obserwacja 2.700 s, aktywacja 2.940 s, target 5.425382 m). Później wraca do szukania okazji; próba z 9.700 s pozostaje nieaktywna do końca 10 s, nie jest zaliczana jako wykonany atak. Kryterium reattack to aktywowana ofensywna komenda Line po co najmniej 0.3 s wcześniejszego częściowego napędu; nie zalicza ResumeIntent lub samego utrzymania intencji. Nie wymagamy wygranej tego manewru; nie ma potwierdzonego całego minięcia w C w tym oknie.

B: przez pierwsze 2 s ta sama prędkość i ścieżka nie powodują korekt ani linii. Późne 0.050 s częściowego napędu pojawia się dopiero gdy przód wcześniej wchodzi w geometrię łuku. E: 0 zamknięć i 0 częściowego gazu, mimo bliskości oraz zachowanych kandydatów Contest. G: wartości gazu, przyspieszenia i prześwitu pozostają skończone po obu stronach granicy; nieskończony TTC przy niedodatnim zbliżaniu oznacza brak takiego przewidywanego spotkania, nie NaN napędu.

D nie jest czyszczone przez regulator: ta sama fizyczna próba kończy się Crash i jednym kontaktem w OFF i ON, po pełnej reakcji na Emergency. Pozycje, prędkości, impulse, normal closing speed i severity ratios obu uczestników są w Result.Riders/Contacts. Nie było restartu do czystego wyniku. W żadnym innym A–G nie powstał kontakt; C ma mniej, a nie więcej bliskich epizodów według jawnej miary.

### Rzeczywista relacja gaz–tempo–odstęp

Poniższe zaakceptowane stany R1 pochodzą z przebiegu, a nie z prognozy. Wielokrotnie częściowy gaz nadal przyspiesza. Wspólny/przecinany korytarz oznacza aktywny powód oceny podążania; dodatni odstęp boczny może pozostać bezpieczny mimo zbieżności projekcji wzdłużnych.



#### A-blocked-follow ON

| Czas s | Rywal | Gap wzdłuż m | Boczny m | Prędkość własna / rywala m/s | Gaz | Korytarz | Aktualny powód / manewr |
|---|---|---|---|---|---|---|---|
| 0.010 | 2 | 9.880 | -0.800 | 18.021 / 16.024 | 1.000 | True | Initial |
| 0.510 | 2 | 8.919 | -0.800 | 18.939 / 17.165 | 0.767 | True | Following |
| 1.010 | 2 | 8.094 | -0.800 | 19.834 / 18.243 | 1.000 | True | ResumeAcceleration |
| 1.510 | 2 | 7.323 | -0.800 | 20.755 / 19.259 | 1.000 | True | ResumeAcceleration |
| 2.010 | 2 | 6.598 | -0.800 | 21.620 / 20.214 | 1.000 | True | ResumeAcceleration |
| 2.510 | 2 | 5.917 | -0.800 | 22.431 / 21.112 | 1.000 | True | ResumeAcceleration |
| 3.010 | 2 | 5.208 | -0.731 | 22.625 / 21.298 | 0.935 | True | Following |
| 3.510 | 2 | 4.619 | -0.864 | 22.549 / 21.313 | 0.750 | True | Following |
| 4.010 | 2 | 4.015 | -0.903 | 22.565 / 21.329 | 0.500 | True | Following |
| 4.510 | 2 | 3.419 | -0.893 | 22.403 / 21.346 | 0.000 | True | Following |
| 5.010 | 2 | 3.012 | -0.882 | 21.932 / 21.358 | 0.000 | True | Following |
| 5.510 | 2 | 2.843 | -0.873 | 21.467 / 21.379 | 0.000 | True | CorridorOccupied |
| 6.010 | 2 | 2.933 | -0.865 | 21.162 / 21.571 | 0.250 | True | ResumeAcceleration |
| 6.510 | 2 | 3.234 | -0.860 | 21.234 / 22.036 | 0.750 | True | ResumeAcceleration |
| 7.010 | 2 | 3.678 | -0.859 | 21.735 / 22.554 | 1.000 | True | ResumeAcceleration |
| 7.510 | 2 | 3.919 | -0.861 | 22.398 / 22.559 | 1.000 | True | ResumeAcceleration |


#### C-follow-reattack ON

| Czas s | Rywal | Gap wzdłuż m | Boczny m | Prędkość własna / rywala m/s | Gaz | Korytarz | Aktualny powód / manewr |
|---|---|---|---|---|---|---|---|
| 0.003 | 2 | 9.895 | -0.800 | 18.006 / 16.005 | 1.000 | True | Initial |
| 0.510 | 2 | 8.805 | -0.800 | 19.132 / 16.935 | 0.750 | True | Following |
| 1.010 | 2 | 7.684 | -0.800 | 20.123 / 17.804 | 0.811 | True | Following |
| 1.510 | 2 | 6.549 | -0.800 | 20.761 / 18.628 | 0.586 | True | Following |
| 2.010 | 2 | 5.615 | -0.800 | 21.009 / 19.408 | 0.443 | True | Following |
| 2.510 | 2 | 4.940 | -0.800 | 21.273 / 20.146 | 0.492 | True | ResumeAcceleration |
| 3.010 | 2 | 4.378 | -0.741 | 21.666 / 20.396 | 0.613 | True | ClosingSlowerRider |
| 3.510 | 2 | 3.882 | -0.523 | 21.510 / 20.412 | 0.113 | True | Following |
| 4.010 | 2 | 3.626 | -0.175 | 21.045 / 20.427 | 0.000 | True | Following |
| 4.510 | 2 | 3.749 | 0.114 | 20.503 / 20.442 | 0.250 | True | ResumeAcceleration |
| 5.010 | 2 | 4.077 | 0.305 | 20.408 / 20.456 | 0.500 | False | ResumeAcceleration |
| 5.510 | 2 | 4.483 | 0.418 | 20.357 / 20.470 | 1.000 | False | ResumeAcceleration |
| 6.010 | 2 | 4.943 | 0.481 | 20.349 / 20.553 | 1.000 | False | ResumeAcceleration |
| 6.510 | 2 | 5.463 | 0.511 | 20.558 / 20.825 | 1.000 | False | ResumeAcceleration |
| 7.010 | 2 | 5.988 | 0.519 | 21.126 / 21.273 | 1.000 | False | ResumeAcceleration |
| 7.510 | 2 | 6.411 | 0.511 | 21.959 / 21.745 | 1.000 | False | AttackNoLongerFeasible |
| 8.010 | 2 | 6.510 | 0.581 | 22.815 / 21.784 | 1.000 | False | AttackNoLongerFeasible |
| 8.510 | 2 | 6.077 | 0.666 | 23.663 / 22.393 | 1.000 | False | AttackNoLongerFeasible |
| 9.010 | 2 | 5.354 | 0.610 | 24.533 / 22.962 | 1.000 | False | AttackNoLongerFeasible |
| 9.510 | 2 | 4.495 | 0.594 | 25.338 / 23.375 | 1.000 | False | AttackNoLongerFeasible |


#### G-straight-bend ON

| Czas s | Rywal | Gap wzdłuż m | Boczny m | Prędkość własna / rywala m/s | Gaz | Korytarz | Aktualny powód / manewr |
|---|---|---|---|---|---|---|---|
| 0.010 | 2 | 4.274 | -0.720 | 19.020 / 18.000 | 1.000 | True | Initial |
| 0.510 | 2 | 3.664 | -0.709 | 19.299 / 18.012 | 0.750 | True | Following |
| 1.010 | 2 | 3.184 | -0.338 | 19.099 / 18.023 | 0.250 | True | Following |
| 1.510 | 2 | 2.964 | 0.104 | 18.656 / 18.033 | 0.000 | True | Following |
| 2.010 | 2 | 3.089 | 0.432 | 18.242 / 18.043 | 0.250 | False | ResumeAcceleration |
| 2.510 | 2 | 3.375 | 0.640 | 18.146 / 18.052 | 0.750 | False | ResumeAcceleration |


W A sześć zmian kierunku korekty gazu ≥0.04 w 8 s obejmuje początkowe poznanie przyspieszania rywala, przygotowanie do łuku i powrót na gaz. C ma pięć. Nie ma sekwencji wielu pełnych 0↔1 przełączeń: w każdym z A/C/G jest jedno pełne zamknięcie, a zwykły powrót idzie krokiem maks. 0.25 po kolejnych aktywacjach. Te korekty nie dowodzą braku wszelkich oscylacji w innych warunkach. W C/8 s można pozostać za przeciwnikiem mimo pełnego gazu: pościg i rzeczywista możliwość przejścia nie są tym samym.

### F: rzeczywiste przyspieszenie po aktywacji, osobno od estymaty

Tabela pokazuje pierwsze kroki zaakceptowane po wybranych korektach napędu. Δv i przyspieszenie są mierzone z wykonanych prędkości; nie zastępują ich żądania regulatora. W chwili aktywacji świat jest o pełny czas reakcji późniejszy od obserwacji i mogą obowiązywać inne ograniczenia geometrii. Nie obiecujemy równości z żądanym przyspieszeniem sprzed reakcji.


| Próba | Obserwacja s | Żądana aktywacja s | Rzeczywista s | Drive | Zmiana prędkości m/s | Wykonane a wzdłuż m/s² |
|---|---|---|---|---|---|---|
| F-low-capability | 0.100 | 0.340 | 0.340 | 0.943 | 18.558182 → 18.572945 | 1.476288 |
| F-low-capability | 0.400 | 0.640 | 0.640 | 1.000 | 18.994286 → 19.009910 | 1.562500 |
| F-high-capability | 0.100 | 0.340 | 0.340 | 0.750 | 18.855057 → 18.871536 | 1.647949 |
| F-high-capability | 0.400 | 0.640 | 0.640 | 1.000 | 19.341484 → 19.365425 | 2.394104 |
| F-high-capability | 1.100 | 1.340 | 1.340 | 0.914 | 20.942070 → 20.961205 | 1.913452 |
| F-high-capability | 1.400 | 1.640 | 1.640 | 0.745 | 21.505333 → 21.518600 | 1.326752 |
| F-high-capability | 1.700 | 1.940 | 1.940 | 0.628 | 21.896553 → 21.905860 | 0.930786 |


Te same wartości Drive nie kodują połowy Vmax. Istniejące Speed/SlideControl oraz faza zakrętu zmieniają siłę i skuteczność korekty. Osobny test przepuszcza ten sam Drive=0.5 przez niezmienione Forces w trzech różnych referencjach i obu poziomach możliwości; wartości przyspieszenia są różne, prędkość w danym stanie nadal wynosi 18 m/s.

## 5. Pełne biegi H: niezmienione rotacje #92

Wszystkie 8/8 OFF i 8/8 ON ukończone; 0 przerwanych, 0 kontaktów, 0 Crash, dodatni prześwit toru. Najpierw oceniono przypadki celowane, a potem wszystkie rotacje. Pozostałe rotacje nie są nową siatką profili. Poniżej pełne czasy oraz zmiana wobec identycznego OFF; większy czas jest pokazany normalnie.


| Bieg | Rider | Czas OFF → ON s | Δ s | Zero s | Częściowy s | Pełne zamknięcia | Min. separacja m |
|---|---|---|---|---|---|---|---|
| A1-rotation-0 | R1 | 60.532 → 60.532 | 0.000 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 2.200 → 2.200 |
| A1-rotation-0 | R2 | 61.779 → 61.432 | -0.347 | 3.200 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-0 | R3 | 63.131 → 62.356 | -0.775 | 4.900 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-0 | R4 | 64.566 → 62.584 | -1.982 | 6.100 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-1 | R1 | 61.779 → 61.432 | -0.347 | 3.200 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-1 | R2 | 63.131 → 62.356 | -0.775 | 4.900 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-1 | R3 | 64.566 → 62.584 | -1.982 | 6.100 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-1 | R4 | 60.532 → 60.532 | 0.000 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 2.200 → 2.200 |
| A1-rotation-2 | R1 | 63.131 → 62.356 | -0.775 | 4.900 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-2 | R2 | 64.566 → 62.584 | -1.982 | 6.100 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-2 | R3 | 60.532 → 60.532 | 0.000 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 2.200 → 2.200 |
| A1-rotation-2 | R4 | 61.779 → 61.432 | -0.347 | 3.200 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-3 | R1 | 64.566 → 62.584 | -1.982 | 6.100 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-3 | R2 | 60.532 → 60.532 | 0.000 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 2.200 → 2.200 |
| A1-rotation-3 | R3 | 61.779 → 61.432 | -0.347 | 3.200 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A1-rotation-3 | R4 | 63.131 → 62.356 | -0.775 | 4.900 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.200 → 2.200 |
| A2-rotation-0 | R1 | 60.441 → 60.440 | -0.001 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 1.168 → 2.069 |
| A2-rotation-0 | R2 | 60.173 → 60.042 | -0.131 | 5.800 → 0.000 | 0.000 → 0.000 | 2 → 0 | 1.168 → 1.865 |
| A2-rotation-0 | R3 | 60.937 → 60.907 | -0.029 | 4.300 → 0.000 | 0.000 → 0.000 | 1 → 0 | 1.235 → 1.865 |
| A2-rotation-0 | R4 | 61.119 → 62.413 | 1.295 | 3.600 → 0.000 | 0.000 → 0.000 | 2 → 0 | 2.045 → 2.200 |
| A2-rotation-1 | R1 | 61.339 → 61.338 | -0.001 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 0.686 → 1.616 |
| A2-rotation-1 | R2 | 61.431 → 60.952 | -0.479 | 14.690 → 0.000 | 0.000 → 0.000 | 10 → 0 | 0.686 → 2.097 |
| A2-rotation-1 | R3 | 62.267 → 61.454 | -0.813 | 6.500 → 0.000 | 0.000 → 0.000 | 3 → 0 | 1.619 → 1.616 |
| A2-rotation-1 | R4 | 59.817 → 59.817 | -0.001 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 2.167 → 2.167 |
| A2-rotation-2 | R1 | 62.737 → 62.259 | -0.478 | 3.700 → 0.000 | 0.000 → 0.000 | 1 → 0 | 1.200 → 1.200 |
| A2-rotation-2 | R2 | 61.681 → 61.681 | 0.000 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 1.200 → 1.200 |
| A2-rotation-2 | R3 | 59.142 → 59.142 | -0.000 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 2.200 → 2.200 |
| A2-rotation-2 | R4 | 60.508 → 60.508 | 0.000 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 2.172 → 2.172 |
| A2-rotation-3 | R1 | 63.799 → 63.194 | -0.605 | 4.100 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.188 → 2.188 |
| A2-rotation-3 | R2 | 59.154 → 59.154 | -0.000 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 2.089 → 2.089 |
| A2-rotation-3 | R3 | 59.818 → 59.818 | -0.000 | 0.000 → 0.000 | 0.000 → 0.000 | 0 → 0 | 2.089 → 2.089 |
| A2-rotation-3 | R4 | 61.910 → 61.628 | -0.281 | 2.400 → 0.000 | 0.000 → 0.000 | 1 → 0 | 2.188 → 2.188 |


W tych pełnych biegach ON **nie potrzebuje częściowego napędu**. Głównym błędem były reakcje na oddzielne linie; ich usunięcie nie jest samo w sobie dowodem działania following. Dowód częściowej regulacji pochodzi z A/C/F/G. W A1 R4 ma inną późniejszą linię i czas 62.584 s, więc całej różnicy jego czasu nie przypisujemy wyłącznie usunięciu 6.1 s zerowego gazu. A2/0 R4 jest około 1.294 s wolniejszy od OFF, przy 0 kontaktów w obu; brak wymuszenia poprawy kolejności. Późniejsze stany oraz okazje do manewrów zmieniają się po zmianie sterowania.

Ponowne ataki po wcześniejszym **częściowym** podążaniu w H=0, bo nie było takiego podążania; nie liczymy samych intencji lub KeepIntent. Wykonane próby ataku/Contest, prawdziwe minięcia, odstępy czasowe i sterowanie pozostają jawne poniżej. Min. clearance oraz lap times każdego ridera są w Measurement plików JSON. Potwierdzenie minięcia jest istniejącym konserwatywnym obserwatorem #92, z historią i ≥1 s/10 m utrzymania całego motocykla; nie liczy początkowych pól ani chwilowych zmian referencji.


| Bieg | Kolejność | Odstęp 1.→4. s | Zamknięcia / częściowe epizody | Atak / obrona / side intencje aktywowane | Potwierdzone minięcia | Zmiany aktuatorów | Niebezpieczne epizody / s |
|---|---|---|---|---|---|---|---|
| [A1-rotation-0-OFF](proportional-throttle-control/A1-rotation-0-OFF.json) | R1 > R2 > R3 > R4 | 4.034 | 3 / 0 | 0/0/5 | 0 | 6 | 0 / 0.000 |
| [A1-rotation-0-ON](proportional-throttle-control/A1-rotation-0-ON.json) | R1 > R2 > R3 > R4 | 2.052 | 0 / 0 | 1/0/7 | 0 | 4 | 0 / 0.000 |
| [A1-rotation-1-OFF](proportional-throttle-control/A1-rotation-1-OFF.json) | R4 > R1 > R2 > R3 | 4.034 | 3 / 0 | 0/0/5 | 0 | 6 | 0 / 0.000 |
| [A1-rotation-1-ON](proportional-throttle-control/A1-rotation-1-ON.json) | R4 > R1 > R2 > R3 | 2.052 | 0 / 0 | 1/0/7 | 0 | 4 | 0 / 0.000 |
| [A1-rotation-2-OFF](proportional-throttle-control/A1-rotation-2-OFF.json) | R3 > R4 > R1 > R2 | 4.034 | 3 / 0 | 0/0/5 | 0 | 6 | 0 / 0.000 |
| [A1-rotation-2-ON](proportional-throttle-control/A1-rotation-2-ON.json) | R3 > R4 > R1 > R2 | 2.052 | 0 / 0 | 1/0/7 | 0 | 4 | 0 / 0.000 |
| [A1-rotation-3-OFF](proportional-throttle-control/A1-rotation-3-OFF.json) | R2 > R3 > R4 > R1 | 4.034 | 3 / 0 | 0/0/5 | 0 | 6 | 0 / 0.000 |
| [A1-rotation-3-ON](proportional-throttle-control/A1-rotation-3-ON.json) | R2 > R3 > R4 > R1 | 2.052 | 0 / 0 | 1/0/7 | 0 | 4 | 0 / 0.000 |
| [A2-rotation-0-OFF](proportional-throttle-control/A2-rotation-0-OFF.json) | R2 > R1 > R3 > R4 | 0.945 | 5 / 0 | 2/0/16 | 1 | 16 | 0 / 0.000 |
| [A2-rotation-0-ON](proportional-throttle-control/A2-rotation-0-ON.json) | R2 > R1 > R3 > R4 | 2.371 | 0 / 0 | 2/0/11 | 1 | 5 | 0 / 0.000 |
| [A2-rotation-1-OFF](proportional-throttle-control/A2-rotation-1-OFF.json) | R4 > R1 > R2 > R3 | 2.449 | 13 / 0 | 1/0/27 | 0 | 28 | 0 / 0.000 |
| [A2-rotation-1-ON](proportional-throttle-control/A2-rotation-1-ON.json) | R4 > R2 > R1 > R3 | 1.637 | 0 / 0 | 1/1/19 | 1 | 17 | 0 / 0.000 |
| [A2-rotation-2-OFF](proportional-throttle-control/A2-rotation-2-OFF.json) | R3 > R4 > R2 > R1 | 3.595 | 1 / 0 | 0/0/5 | 1 | 4 | 0 / 0.000 |
| [A2-rotation-2-ON](proportional-throttle-control/A2-rotation-2-ON.json) | R3 > R4 > R2 > R1 | 3.117 | 0 / 0 | 0/0/5 | 1 | 2 | 0 / 0.000 |
| [A2-rotation-3-OFF](proportional-throttle-control/A2-rotation-3-OFF.json) | R2 > R3 > R4 > R1 | 4.644 | 2 / 0 | 0/0/4 | 0 | 5 | 0 / 0.000 |
| [A2-rotation-3-ON](proportional-throttle-control/A2-rotation-3-ON.json) | R2 > R3 > R4 > R1 | 4.040 | 0 / 0 | 0/0/4 | 0 | 2 | 0 / 0.000 |


Osiem main OFF ma 2 potwierdzone minięcia, ON ma 3. W A2/0 R2/R1 potwierdzono 47.520→25.390 s, a w A2/1 pojawia się potwierdzone R2/R1 w 25.840 s. To nie certyfikat uniwersalnej przewagi taktycznej; rola pola, linii i rzeczywistego tempa jest nadal istotna. Obrona pozostaje słabo pobudzona w tej małej próbce; regresje historycznych mechanizmów #89/#90 zachowują zachowanie OFF.

## 6. Koszt i dodatkowe granice solvera

Release Windows/.NET 8.0.26, DOTNET_TieredCompilation=0, bez observera i plików trajektorii w benchmarku. Jeden warmup, trzy zmierzone próbki na tryb. CPU to czas procesu z kwantyzacją platformy, alokacje dotyczą tylko bieżącego wątku zarządzanego, bez native memory. Inne procesy testujące nie działały podczas tego benchmarku. Wartości są lokalnym pomiarem, nie obietnicą sprzętową.


| Próba | Wall ms | CPU ms | MiB | AI/taktyka ms | Kroki 10 ms | Wywołania solvera | Substeps | Oceny istniejącej siły dla gazu |
|---|---|---|---|---|---|---|---|---|
| A-blocked-follow OFF | 20.434 | 15.625 | 2.898 | 0.165 | 800 | 899 | 3596 | 0 |
| A-blocked-follow ON | 24.841 | 31.250 | 3.512 | 0.408 | 800 | 1138 | 4552 | 228 |
| A1-rotation-0 OFF | 136.536 | 140.625 | 16.913 | 1.882 | 6457 | 6528 | 26112 | 0 |
| A1-rotation-0 ON | 134.608 | 140.625 | 17.522 | 4.671 | 6259 | 6333 | 25332 | 160 |
| A2-rotation-1 OFF | 143.718 | 140.625 | 17.713 | 2.501 | 6227 | 6861 | 27444 | 0 |
| A2-rotation-1 ON | 133.373 | 140.625 | 17.323 | 5.347 | 6146 | 6237 | 24948 | 574 |


[Trzy próbki każdego trybu i liczniki](proportional-throttle-control/performance.json). A ma 899→1138 wywołań solvera (+26.6%) i 20.434→24.841 ms wall (+21.6%): pozostawanie blisko oraz nowe aktywacje zmieniają również istniejące podziały proximity/krzywizny, więc nie nazywamy całej różnicy kosztem samej formuły gazu. Czas całej taktyki A rośnie ok. 0.165→0.408 ms. W H czas oraz liczba kroków świata i solvera maleją, bo zmienia się przebieg i czas dojazdu ostatniego zawodnika; nie jest to dowód optymalizacji solvera. Taktyka A1/0 ok. 1.882→4.671 ms, A2/1 2.501→5.347 ms na cały bieg. Osiemnaście benchmarkowych hashy zgadza się z odpowiednim normalnym przebiegiem ON/OFF.

Nie wprowadzono dodatkowej częstotliwości AI ani odrębnych klatek fizyki do regulatora. Bez przeciwnika liczba ocen siły nowego regulatora wynosi 0, a solo ON zachowuje dokładny hash OFF. Z zapisu szczegółowego wybrano A1/0 i A2/1 oraz okna A–G: próbki co 0.5 s, istotne komendy, ich aktywacje i pierwszy rzeczywiście wykonany krok po zmianie gazu. Pozostałe rotacje zapisują metryki i zdarzenia; żadnych wielkich snapshotów co krok. Koszt audytu jest poza bare benchmarkiem.

## 7. Regresje, deterministyczność i ocena

- 118/118 celowanych testów Release: 15 nowych i 103 dotychczasowe SharedWorld/SideBySide/Quality/audytu #92. Nowe testy sprawdzają A–G, fizyczne skutki opóźnienia i kontaktu, kolejki, brak nadpisania częściowego gazu przez starą linię Drive=1, brak odsuwania oczekującej reakcji bezpieczeństwa, dokładne solo bez rywala i wszystkie osiem OFF z #92 (hashe, końcowe ciała, komendy, kontakty, mety).
- [Pięć wybranych powtórzeń ON](proportional-throttle-control/repeats.json) w osobnym świeżym procesie, z odwróconą kolekcją: A, C, A1/0, A2/0, A2/1. Wszystkie hashe, komendy i kontakty dokładnie identyczne. Trace ON/OFF nie steruje symulacją.
- Restore i warnings-as-errors Release build: sukces. Guard 39 produkcyjnych konsumentów: sukces. Discovery 2631/2631 z jedną odpowiedzialnością (core 1715, trajectory 395, historical-analysis 473, four-rider 48); bez braków/duplikatów. Python 89+7 testów: sukces. Standardowy Windows CI bez zmiany workflow; wynik dla opublikowanego HEAD jest w Checks Draft PR, nie jest z góry deklarowany przez ten dokument jako zielony.

**A — DZIAŁA w tym zestawie**, przy zachowaniu default OFF i PROVISIONAL stałych. Uzasadnienie: kontrolowane częściowe podążanie z rzeczywistym prześwitem, późniejszy wykonany atak, brak reakcji na samą bliskość B/E, zachowany pilny release i nieunikniony Crash D, bez dodatkowych kontaktów w głównych biegach i lepsza miara bliskiego zbliżenia w C. Nie ma mechanicznego bonusu do przyspieszenia ani wymagania wygranej. Zielone CI jest regresją, a nie podstawą tej oceny.

Ograniczenia: prognoza rywala jest krótka i oparta na bieżącej prędkości/przeszłej obserwacji, bez wiedzy o jego przyszłych zmianach; PD odstępu nie jest certyfikatem bezkolizyjności. Potrzebna jest osobna walidacja większej liczby prawdziwych nagłych zmian tempa i dłuższej stabilności kolejki, bez strojenia do A1. W obecnym neutral-carry niektóre ujemne żądania dają skok na zero gazu; nie wymuszamy sztucznie dodatniej wartości dla ładniejszego histogramu. Obrona i jazda partnerów nie zostały tu przebudowane. Mała próbka nie potwierdza prawdopodobieństw zwycięstw ani gotowości produkcyjnego HeatSimulator.

Odtwarzanie: `dotnet build SpeedwayManager.sln -c Release --warnaserror`, następnie `dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll throttle-diagnose <dir>` dla #92 oraz `throttle-audit <dir> pairs`, `heats`, `repeat`; osobny proces `benchmark` z DOTNET_TieredCompilation=0. Na końcu `python tools/shared-world-speedway/throttle-report.py <dir>`. Generator weryfikuje zgodność OFF, opóźnienia, powtórzenia i hashe benchmarku przed utworzeniem dokumentu. Manifest SHA-256 obejmuje wszystkie surowe pliki; zachowano także wszystkie dowody #92 bez zmian.
