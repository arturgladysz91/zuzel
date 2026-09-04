# Speedway Manager (Żużlowy) – Core Simulation

## Cel projektu
Celem projektu jest stworzenie gry typu **manager żużlowy**, w której kluczowe są:
- decyzje menedżera (przygotowanie toru, reakcje w trakcie meczu, sugestie setupu),
- zachowanie zawodników (styl, statystyki, morale, rozwój),
- **dynamicznie zmieniający się tor** (pogoda, prace torowe, degradacja przez jazdę).

Projekt **nie** próbuje odtwarzać pełnej fizyki motocykla. Zamiast tego stosujemy **ograniczenia fizyczne jako reguły**, które wymuszają realistyczne konsekwencje (np. wynoszenie w łuku przy zbyt dużej prędkości po ciasnej).

---

## Skrót architektury (high level)
Architektura jest podzielona na warstwy odpowiedzialności:

### 1) Dane i konfiguracja
- definicje torów, parametrów, profili zawodników,
- ustawienia pogody, zdarzeń, balansu (konfigurowalne w danych).

### 2) Symulacja (CoreSim)
Główna logika gry:
- stan toru (segmenty i linie),
- degradacja toru w czasie,
- decyzje zawodników (wybór linii per segment, ryzyko, reakcje),
- morale i wpływ na zachowanie,
- rozwój i regres w sezonie,
- setup (sugestie menedżera vs decyzje zawodnika).

### 3) Warstwa “host/gameplay”
- system akcji gospodarza (prace torowe, decyzje w przerwach),
- integracja z meczem 15-biegowym, logowanie przebiegu.

### 4) Prezentacja (UI / wizualizacja – później)
- wizualizacja jazdy oparta o wyniki symulacji,
- nie wpływa na logikę (rendering ≠ symulacja).

---

## INSTRUKCJE OGÓLNE – CORE SYMULACJI (OBOWIĄZUJĄCE)

### 0. Cel symulacji
Symulujemy **decyzje menedżerskie i zachowanie zawodników**, a nie pełną fizykę motocykla.
Fizyka występuje jako **ograniczenia i konsekwencje** (wynoszenie, utrata prędkości, ryzyko błędu).

---

### 1. Tor

#### 1.1 Struktura toru
Tor składa się z segmentów decyzyjnych:
- wejście w łuk
- środek łuku
- wyjście z łuku
- prosta

Każdy segment posiada **5 linii jazdy**.

Każdy konkretny tor przechowuje niezmienną geometrię: długość prostej, wewnętrzny promień referencyjny łuku, odstęp między liniami oraz kąt pojedynczego segmentu łuku. Geometria nie jest stanem nawierzchni i nie zmienia się podczas meczu.

#### 1.2 Linie jazdy
Linie są lokalne dla segmentu, nie przypisane do całego łuku ani biegu.

Zawodnik może wejść w łuk jedną linią, przejechać środek inną, wyjść jeszcze inną.

Linie są strefami referencyjnymi (decyzje/ocena pozycji), a faktyczny ruch odbywa się po ciągłej trajektorii; pozycja względem linii jest zmienną ciągłą.

Optymalna trajektoria nie musi oznaczać ciągłej jazdy po jednej linii.

W zależności od warunków toru (przyczepność, koleiny/zużycie, wilgotność) lepsza może być linia mieszana, np. wąsko na wejściu i szerzej na wyjściu, bo pozwala utrzymać płynność bez nadmiernego hamowania.

Model powinien premiować płynność i utrzymanie prędkości wyjściowej, a nie tylko “trzymanie krawężnika”.

Świadomy wybór szerokiej linii jest decyzją o trajektorii: może służyć znalezieniu lepszej nawierzchni, wyprzedzeniu albo przygotowaniu dłuższej prostej. Nie jest tym samym co wyniesienie, czyli nieplanowane lub wymuszone przesunięcie na zewnątrz po przekroczeniu ograniczenia, błędzie albo kontakcie.

Wyniesienie wydłuża drogę po zewnętrznej linii. Gdy wywołuje je przekroczenie ograniczenia, zawodnik nie zachowuje całej prędkości ponad fizyczną granicą; lepsze panowanie w poślizgu zmniejsza tę stratę, ale nie usuwa konsekwencji.

Zmiana linii:

zależy od stylu i umiejętności, oceny ryzyka kolizji z innym zawodnikiem.

nie jest natychmiastowa (ma bezwładność i ograniczenia przyczepności).
Linia = dyskretna strefa decyzyjna (0..4), lokalna dla segmentu.

Ruch = ciągły (pozycja lateralna float), a „linia” to najbliższa strefa referencyjna.

Zmiana linii ma bezwładność: nie przeskakujesz 1→5 w jednej klatce.

#### 1.3 Ograniczenie fizyczne w łuku (OBOWIĄZKOWE)
W łuku obowiązuje zasada:
> Przy danej prędkości istnieje minimalna linia możliwa do utrzymania.

Jeżeli zawodnik jedzie zbyt szybko po ciasnej linii, system musi wymusić:
1) spadek prędkości (hamowanie), albo  
2) wyniesienie na szerszą linię (z inercją i utratą części nadmiernej prędkości), albo
3) ryzyko błędu / straty.

Nie dopuszczamy “cudów”: szybka jazda przy krawężniku bez konsekwencji.
Kontakty między zawodnikami w środkowej fazie łuku mają istotnie większe konsekwencje (wyniesienie, utrata prędkości, upadek) niż na prostej.

Fizyczna granica przyczepności wynika z geometrii konkretnego toru, lokalnej nawierzchni, trajektorii, setupu i odpowiednich umiejętności kontroli. Morale nie zmienia tej granicy; może później wpływać na decyzję zawodnika, podejmowane ryzyko i jakość wykonania.

Przewaga linii wynika z połączenia geometrii konkretnego toru, nawierzchni i trajektorii. Porównanie przewidywanych czasów skrajnych linii jest diagnostyką danego toru, a nie testem ich sztucznej równości. Tor może premiować konkretną linię, ale żadna linia nie może być uniwersalnie najlepsza na wszystkich torach i nawierzchniach.

#### 1.4 Stan toru
Każdy segment i linia mają stan, m.in.:
- przyczepność
- koleiny
- wilgotność

Stan toru:
- zmienia się przez pogodę,
- jazdę zawodników,
- prace torowe,
- zmienia się nierównomiernie.

Część parametrów jest **ukryta przed graczem** (gracz nie ma “telemetrii”).

---

### 2. Proste

#### 2.1 Rola prostej
Prosta nie jest miejscem klasycznej walki.
Jej rolą jest **pozycjonowanie** przed kolejnym łukiem.

#### 2.2 Linie na prostej
Prosta ma 5 linii, ponieważ:
- wpływają na ustawienie do kolejnego łuku,
- umożliwiają blokowanie/obejście,
- są ważne dla realizmu wizualnego.

Linie na prostej **nie dają bonusu do prędkości**.

#### 2.3 Wyprzedzanie na prostej
Prosta nie dodaje osobnego outcome ataku ani arbitralnego bonusu linii.
Różnica może wynikać z lepszego wyjścia z poprzedniego łuku oraz fizycznego
profilu prędkości przejazdu; istniejące reguły kontaktu i interakcji pozostają
bez zmian.

Legacy zachowuje na prostej prędkość. Od #27 advanced physics używa
speed-dependent, force-based i distance-limited profilu longitudinal: zawodnik
może przyspieszyć, a przed
bezpośrednio następującym łukiem kontrolowanie wytracić prędkość. Linie prostej
nadal nie dają arbitralnego bonusu; różnica wynika z umiejętności, wejściowej
nawierzchni, dostępnego dystansu i potrzeby przygotowania do następnego łuku.

Positive drive w advanced physics ma osiągalną, deterministyczną prędkość
szczytową. Bazowy zakres **PROVISIONAL / NOT REAL-WORLD CALIBRATED** `21–25 m/s`
zależy od `Speed`, a `BikeSetup.Gearing` mnoży go w równie prowizorycznym zakresie
`0.94–1.06`. Te wartości nie są jeszcze oparte na docelowej telemetrii i nie
opisują finalnej prędkości prawdziwego motocykla żużlowego. Oś `Gearing` oznacza
`drive-oriented ↔ speed-oriented`: `0` daje mocniejszy corner-exit drive kosztem
niższej osiągalnej prędkości, a `1` słabszy drive i wyższą osiągalną prędkość.
Nawierzchnia wpływa na dystans i czas potrzebny do osiągnięcia granicy, ale nie
zmienia samej granicy. Istniejące wzory i zachowanie pozostają bez zmian.

Nie jest to hard limiter: istniejąca prędkość równa lub większa od granicy nie
jest obcinana przez positive drive. Advanced Straight dzieli rzeczywisty dystans
na deterministyczne kroki maksymalnie `1 m`, z dokładną końcową resztą. Każdy
full-drive krok liczy predictor z acceleration na początku, następnie prędkość
pośrednią i corrected acceleration w midpoint; czas kroku to
`2 * ds / (v_start + v_end)`. Suma czasów kroków steruje również ruchem bocznym.
Zachowany helper analityczny `CalculateStraightSpeedProfile` nie jest już
produkcyjną ścieżką advanced Straight. Ten sam ceiling nadal ogranicza positive
drive na `TurnExit`.

Od #29 eligible advanced `TurnExit` (`Ok`/`Brake`, dodatnia post-physics speed i
dodatni dystans) używa tego samego deterministycznego fixed-distance traversal
co positive-drive część Straight. Profil zaczyna się dokładnie od
post-`SegmentPhysics` `resolution.Speed`, dzieli dystans na wspólne kroki do
`1 m` z dokładną końcową resztą i używa wspólnego midpoint positive-drive step.
Nie jest to fixed-time timestep. `RunWide` i `Crash` nie otrzymują profilu.
TurnExit travel time jest sumą czasów jego kroków i automatycznie stanowi budżet
`LateralMovementModel`.

Straight i TurnExit współdzielą numerical resolution, tworzenie kroków,
one-gear envelope, resistance, generic net-positive-drive force oraz midpoint
positive-drive step. Zachowują różne reference acceleration: TurnExit
`0.60–1.40 × gearing × surface`, Straight `0.80–1.60 × gearing × surface`.
Dostępna siła TurnExit jest kalibrowana tak, aby przy referencyjnych
`16 m/s` zachować dotychczasowe przyspieszenie zależne od `Speed`, `Gearing` i
wejściowej nawierzchni. Przy `v <= 16 m/s` one-gear drive envelope wynosi
dokładnie `1`. Powyżej tej prędkości wynosi
`max(0, 1 - fadeRate * (v - 16))`, gdzie `fadeRate` interpoluje przez `Gearing`
od `0.0175 /(m/s)` dla ustawienia drive-oriented do `0.0050 /(m/s)` dla
ustawienia speed-oriented. Dzięki temu mocniejsze reference force ustawienia
drive-oriented zanika szybciej i krzywe mogą naturalnie przeciąć się przy
większej prędkości.

Model liczy **PROVISIONAL / NOT REAL-WORLD CALIBRATED** actual available force
jako reference force pomnożone przez envelope, aggregate resistance
`F_resistance = 40 N + 0.20 N/(m/s)² * v²`,
`F_net_positive = max(0, F_drive(v) - F_resistance)` i
`a = F_net_positive / 142 kg`. `142 kg` jest nominalną wewnętrzną masą układu,
nie indywidualną wagą zawodnika. Zachowanie #25 pozostaje dokładnie takie samo
przy i poniżej `16 m/s`.

Straight ma własne provisional reference acceleration:
`(0.80–1.60 m/s² przez Speed) * (0.75 + 0.25 * EffectiveGrip) *
(1.10 + (0.90 - 1.10) * Gearing)`. Reference available force wynosi
`142 kg * referenceAcceleration + F_resistance(16)`, po czym wspólny envelope,
opór `40 + 0.20*v²` i nominalna masa `142 kg` dają speed-dependent positive net
acceleration. Nawierzchnia jest próbkowana raz z wejściowego `LateralPosition`,
nie co metr.

Przy targetcie następnego `TurnEntry` profil buduje wsteczny allowed-speed
envelope ze wspólnej corner-entry deceleration. Forward traversal wybiera
najszybszy wykonalny koniec kroku; jeśli boundary wymagałoby zbyt dużej utraty
prędkości, używa maksymalnego dozwolonego `DecelerateOverDistance`, zamiast
teleportować speed do targetu. TurnEntry #24, TurnExit #26 i legacy pozostają
funkcjonalnie bez zmian.

Envelope jest coarse abstrakcją jednego biegu, a nie modelem RPM, torque/power
curve, rev limiterem ani mapą realnych zębatek. Fundament nie generuje ujemnego
przyspieszenia ani naturalnego wytracania prędkości i nie usuwa obecnego ceiling.
#27 nie jest finalną real-world calibration. Wheelspin, clutch, slip ratio,
traction-force cap, wheel radius, real sprockets, rozdzielenie oporów, CdA, wind
i finalna kalibracja osiągów nie są jeszcze modelowane.

Realny motocykl żużlowy jedzie podczas biegu na jednym biegu, nie ma klasycznego
układu hamulcowego, a przełożenie jest elementem setupu. Obecne kontrolowane
wytracanie prędkości nie oznacza hamowania jak motocyklem drogowym: docelowo ma
odzwierciedlać odjęcie gazu, ustawienie motocykla, uślizg i opory. Lookahead
bezpośredniego `TurnEntry` nie targetuje już settled safe speed. Używa
`MaxSafeTurnSpeed` jako settled speed i wyznacza maksymalną fizycznie odzyskiwalną
approach speed ze wzoru `sqrt(settled² + 2 * deceleration * (turnEntryDistance * 0.50))`,
na podstawie niezmiennej nawierzchni następnego łuku i wejściowego
`LateralPosition`.

W advanced physics pierwsze **PROVISIONAL / NOT REAL-WORLD CALIBRATED** `50%`
faktycznie pozostałego dystansu `TurnEntry` jest coarse fazą setting / roll-off /
slide-entry / speed scrub. Wspólna prowizoryczna capability `2.00–3.20 m/s²`
zależy od `SlideControl` i wejściowej nawierzchni bieżącego `TurnEntry`. Skuteczny
scrub może rozpocząć łuk powyżej settled speed i naturalnie zakończyć się `Ok`;
residual overspeed dopiero potem rozstrzyga niezmieniony `SegmentPhysics`, a enum
`Brake` nie oznacza hamulca tarczowego. Czas to czas scrub phase plus przejazd
pozostałego dystansu z prędkością po constraint; crash zachowuje globalne `50%`
remaining progress i wyłącznie czas scrub phase. `TurnMiddle`, `TurnExit` i legacy
pozostają bez tej fazy. Osobny późniejszy etap zbuduje spójny model dostępnego
napędu, gearing, drag, power curve i oporów zależnych od prędkości.

---

### 3. Zawodnicy

#### 3.1 Składniki zawodnika
Każdy zawodnik ma:
- statystyki (potencjał),
- styl jazdy (preferencje),
- morale (stan bieżący),
- cechy rozwojowe.

#### 3.2 Statystyki
Statystyki wpływają m.in. na:
- start, prędkość,
- panowanie w poślizgu,
- czytanie toru,
- jazdę parą,
- adaptację.

Część statystyk może być ukryta lub przybliżona.

#### 3.3 Styl jazdy
Styl określa:
- ryzyko,
- skłonność do zmian linii,
- reakcje na pogorszenie toru,
- zachowanie w walce.

Styl nie zastępuje statystyk.

####3.4 Interakcje między zawodnikami

Zawodnicy muszą uwzględniać obecność innych zawodników na torze.
System symuluje:

unikanie kolizji (hamowanie, korekta trajektorii),

lekkie kontakty (odbicia, strata rytmu),

mocne kontakty (wysokie ryzyko błędu lub upadek).

Skutki kontaktu zależą od:

prędkości względnej,

miejsca na torze (łuk / prosta),

aktualnej przyczepności,

umiejętności zawodników (panowanie w poślizgu, jazda parą, morale).

---

### 4. Morale
Morale:
- wpływa na stabilność i ryzyko błędów,
- zmienia się na podstawie wyników, presji i decyzji menedżera.

Morale ≠ forma fizyczna.

---

### 5. Rozwój zawodnika
- Rozwój zależy od wieku (junior szybciej rośnie, starszy szybciej spada).
- Kontuzje (zwłaszcza ciężkie) mogą dawać trwałe skutki.
- Cechy (np. profesjonalizm, sprawność) modyfikują tempo rozwoju i regresu.

---

### 6. Sprzęt i setup

#### 6.1 Założenie
Sprzęt nie daje przewagi finansowej.
Nie istnieje “szybszy motor za więcej pieniędzy”.

#### 6.2 Setup
- Manager sugeruje ustawienia (np. przełożenia).
- Zawodnik może:
  - zastosować,
  - zmodyfikować,
  - zignorować.

Decyzja zależy od charakteru i zaufania.

#### 6.3 “Sprzęt” jako umiejętność
Sprzęt zawodnika to w praktyce:
- umiejętność doboru ustawień,
- trafność diagnozy toru,
- jakość feedbacku po biegu
- umiejętności zawodnika (jego statystyki).

---

### 7. Informacja dla gracza
Gracz działa na niepełnej informacji:
- brak pełnych wartości liczbowych,
- obserwacja zachowania i wyników,
- komunikaty i logi.

Kod nie powinien zakładać idealnej wiedzy gracza o torze i zawodnikach.

---

### 8. Świadome ograniczenia
Na etapie core:
- brak pełnej fizyki motocykla,
- brak zaawansowanej ekonomii i regulaminów.

Priorytet: realistyczne konsekwencje decyzji i stabilny balans.

### 9. Observation / calibration harness

#28 dodaje wyłącznie typed, read-only obserwację produkcyjnego
`HeatSimulator`; nie kalibruje żadnych liczb i nie tworzy drugiej ścieżki
symulacji. Opcjonalny `ISimulationStepObserver` jest wywoływany raz po
`Resolve`, a przed `Commit`. `CalibrationTraceCollector` buduje próbki
entry/physics/exit/peak, podsumowania okrążeń i zawodników bez parsowania
tekstowego `SimLog`; działa również przy `EnableLogging=false`. Peak Straight
pochodzi z faktycznie użytego `StraightSpeedProfile`, a time/distance z
immutable snapshotu i rozwiązanego state change. CSV jest deterministyczny,
uporządkowany i używa invariant culture.

Harness nie wykonuje RNG i nie zmienia fizyki ani wyniku biegu. Od #29 raportuje
również faktycznie użyty `TurnExitDriveProfile`; zachowany scalar
`TurnExitNetAcceleration` oznacza entry net acceleration początku profilu, a nie
stałe acceleration dla całego segmentu. Od #30 jawnie oznaczony start używa
production `StandingStartLaunchProfile`; stary bootstrap pozostaje tylko dla
kompatybilności. Artificial `AttainableTopSpeed` nadal istnieje, a signed
negative drag/coasting nadal nie jest modelowany.
Roadmap: signed net force i natural resistance
deceleration przygotowujące usunięcie artificial ceiling, a dopiero potem
porównanie z real-world calibration targets.

## Standing start / launch foundation — PROVISIONAL

`Track.CreateExample()` zachowuje dotychczasowe 8 segmentów i bootstrap.
Nowy `Track.CreateStandingStartExample()` (także w Sandbox) umieszcza start/metę
na canonical lap boundary między segmentami 8 i 0: `Straight 35 m (start) → TurnEntry → TurnMiddle →
TurnExit → Straight 60 m → TurnEntry → TurnMiddle → TurnExit → Straight 35 m
(finish)`. Home straight to finish-half + start-half po dwóch stronach granicy
okrążenia: 35 + 35 = 70 m. Pierwszy łuk zaczyna się 35 m od startu.
Długość wynika wyłącznie z fizycznych segmentów: `130 + 6 * (24 + lateral) * PI/3`
m, czyli około 280.8 m na inner reference trajectory. To 10 m więcej niż
compatibility example; nie ma dodatkowego wirtualnego dystansu launch.

`TrackSegment` ma opcjonalny `float? StraightLengthMetersOverride` (finite, >0,
tylko Straight) i `bool IsStandingStartSegment=false` (tylko Straight).
Immutable copy zachowuje oba pola. Track dopuszcza najwyżej jeden oznaczony
segment, wyłącznie pod index 0, z końcowym Straight. Launch działa tylko w
advanced physics, lap 0 / segment 0, na oznaczonym segmencie, z dokładnego
canonical/physical początku, `NotStarted`, speed <= 0 i dodatnim pozostałym
dystansem. Wtedy entry/physics speed jest dokładnie 0; kolejne okrążenia używają
zwykłego Straight. `CreateExample`, legacy, #24 TurnEntry, #29 TurnExit i normal
Straight nie zmieniają formuł; lookahead nadal nie przekracza jednego segmentu.

Reaction: `0.28 + (0.20 - 0.28) * StartNorm` s: Start 0/50/100 → 0.28/0.24/0.20 s.
Reference acceleration: `(9.0 + (11.0 - 9.0) * StartNorm) *
(1.10 + (0.90 - 1.10) * Gearing) * (0.75 + 0.25 * EffectiveGrip)` m/s².
Reference force: `142 * referenceAcceleration + resistance(0)` N.
Launch współdzieli `40 + 0.20*v²` resistance, one-gear envelope, exact 1 m
distance steps i midpoint, z niezmienionym artificial ceiling `21–25 m/s ×
0.94–1.06`. Surface jest próbkowana raz przy wejściowym `LateralPosition`.
TractionBias i morale nie wpływają na reaction ani launch force.

Launch przygotowuje wejście w bezpośrednio następny TurnEntry: używa tego samego
`ResolveImmediateNextTurnApproachSpeed` co production Straight oraz backward
allowed-speed envelope z istniejącą corner-entry deceleration. Fastest feasible
profile obejmuje acceleration → ewentualny peak/cruise → preparation/roll-off.
Nie teleportuje prędkości ani nie przekracza dostępnej deceleration.
`AccelerationDistanceMeters + CruiseDistanceMeters + PreparationDistanceMeters`
to dokładnie actual launch distance, a movement to suma czasów fixed-distance steps.
Następny TurnEntry nadal wykonuje niezmieniony #24 scrub.

Reaction nie przesuwa motocykla. Elapsed = reaction + movement; lateral budget
= tylko movement. Rzeczywiste launch exit speed trafia do pierwszego TurnEntry.
Profil i diagnostics zachowują czas pre-contact, final sample duration może
zawierać późniejszy LostRhythm penalty. CSV dopisuje dziewięć nullable pól startu,
w tym `StandingStartPreparationDistanceMeters` na końcu istniejących kolumn.
`TimeTo70KphSeconds` (od taśmy, z interpolacją kroku) oraz
`SpeedAtTwoSecondsMetersPerSecond` (także od taśmy, z reaction delay) są outputs, nie targetami; null oznacza
nieosiągnięty próg albo koniec launch przed 2 s.

Starting-gate geometry nie jest jeszcze fizycznie odwzorowana. Początkowe
`Lane/LateralPosition` są compatibility representation pozycji startowych, nie
docelowym modelem pól A/B/C/D ani pełnej szerokości toru. Przed finalną kalibracją
gate effects trzeba wprowadzić oddzielne physical starting-gate geometry/mapping.
#30 porządkuje strukturę standing startu, nie kalibruje finalnego gate advantage;
nie ma hardcoded bonusu pola A.

Wszystkie launch constants są **PROVISIONAL / NOT REAL-WORLD CALIBRATED**.
Brak clutch, RPM, torque/power, real sprockets, wheelspin, slip ratio, traction
cap, false starts, reaction RNG i gate-specific reaction bonus. Najpierw signed
net force/natural resistance deceleration, dopiero potem real-world calibration.

---

## Status / zakres (przykład)
- [ ] Symulacja toru: segmenty + 5 linii
- [ ] Ograniczenie łuku: minimalna linia dla prędkości (wynoszenie/hamowanie/błąd)
- [ ] Proste: pozycjonowanie + wyprzedzanie tylko z przewagi po wyjściu z łuku
- [ ] Zawodnicy: statystyki + styl + morale
- [ ] Setup: sugestie menedżera vs decyzje zawodnika
- [ ] Rozwój: wiek + kontuzje + cechy
