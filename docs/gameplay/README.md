# Gameplay — indeks specyfikacji

Ten katalog opisuje warstwę menedżerską meczu. Nie zastępuje `docs/core-simulation-spec.md`, który pozostaje źródłem prawdy dla Race Engine i fizycznych inwariantów.

## Status zapisów

- **BINDING** — decyzja zatwierdzona i gotowa do implementacji.
- **PROVISIONAL** — kierunek roboczy wymagający dopracowania lub balansu.
- **TBD** — otwarta decyzja; agent nie może jej samodzielnie rozstrzygać.

Status dotyczy najbliższej sekcji. Jeśli status nie jest podany, zapis jest wyjaśnieniem, a nie nowym wymaganiem implementacyjnym.

## Stan realizacji

PR #8 ukończył fundament Race Engine v2: niezmienny snapshot, fazy `Decide` / `Resolve` / `Commit`, deterministyczność, kanoniczną pozycję na torze i statusy zawodnika. Nie oznacza to ukończenia całego Race Engine.

Przed rozpoczęciem Track Engine pozostają:

- start i pierwszy łuk,
- dalsza rozbudowa ograniczeń jazdy w łuku i wynoszenia,
- późniejsza integracja ciągłej pozycji bocznej z pełną trajektorią przejazdu,
- atak, obrona i blokowanie,
- kontakty, upadki i walidacja fizyki.

Track Engine, zarządzanie torem i systemy menedżerskie pozostają późniejszymi etapami.

PR #9 dodaje niezmienną geometrię konkretnego toru i wykorzystuje ją do obliczania dystansu oraz oceny tras przez `AdaptiveDecisionModel`. Jest to statyczne wejście potrzebne Race Engine, a nie rozpoczęcie właściwego Track Engine; nie obejmuje dynamicznej nawierzchni, pogody, zużycia ani prac torowych.

Ograniczenie prędkości łuku wykorzystuje statyczną geometrię konkretnego toru:

- `SegmentPhysicsContext` otrzymuje `TrackGeometry` ze snapshotu konkretnego toru,
- bazowa granica rośnie jak pierwiastek ze stosunku promienia linii do promienia referencyjnego 24 m; przy 24 m wynosi 16 m/s,
- w advanced physics promień ograniczenia pochodzi z rzeczywistego wejściowego `LateralPosition`: `InnerRadiusMeters + LateralPosition * LaneSpacingMeters`; pozycje całkowite dokładnie odpowiadają dotychczasowym liniom referencyjnym,
- długość łuku advanced physics korzysta z tego samego promienia próbkowanego na wejściu do segmentu i mnoży go przez `TurnSegmentAngleRadians`; `StraightLengthMeters` pozostaje stałe niezależnie od pozycji bocznej,
- fizyczna nawierzchnia advanced physics jest próbkowana raz z tego samego wejściowego `LateralPosition`: liniowo interpolowane są surowe `Grip`, `Ruts` i `Moisture`, po czym `EffectiveGrip` jest ponownie wyliczane przez `TrackSurfaceState`, a nie interpolowane jako gotowa wartość,
- zużycie advanced physics korzysta z tego samego wejściowego `LateralPosition`: miesza istniejące dyskretne kernela dla `floor` i `ceil` pozycji, z `100%` obciążenia w centrum kernela i `20%` na każdej istniejącej komórce sąsiedniej; wspólne wkłady są sumowane przed jednym zapisem do komórki,
- `SimulationEngine`, `AdaptiveDecisionModel` i `PhysicsTestRunner` korzystają z geometrii konkretnego toru,
- morale nie zmienia `MaxSafeTurnSpeed`, lecz pozostaje wejściem istniejącego ryzyka błędu lub incydentu,
- dotychczasowe progi `Brake`, `RunWide` i `Crash` pozostają bez zmian.

### Świadoma szeroka linia i wymuszone wyniesienie — BINDING

Świadome przejście na zewnątrz jest zwykłą decyzją o trajektorii: zmienia `PlannedLane`, może wykorzystać lepszą nawierzchnię lub przygotować wyjście i może zakończyć się `Ok`. Nie jest karane jako `RunWide` tylko dlatego, że zaplanowana linia jest szersza.

`RunWide` oznacza wymuszoną korektę po przekroczeniu ograniczenia albo błędzie. Końcowa `Lane` wypada szerzej niż `PlannedLane`, więc zawodnik pokonuje dłuższą drogę. W ścieżce wywołanej przekroczeniem prędkości zachowuje tylko część nadwyżki ponad indywidualne `MaxSafeTurnSpeed`: w fizyce zaawansowanej lepsze `SlideControl` pozwala zachować większą część tej nadwyżki, ale nigdy całość, a legacy używa neutralnej retencji. Losowe incydenty zachowują osobne dotychczasowe rozstrzygnięcie.

Model nie dodaje `ControlledWide` i nie zmienia progów `Brake`, `RunWide` i `Crash`, zachowania na Lane 4 ani losowych incydentów. Pozostaje ograniczeniem opartym na krzywiźnie, a nie pełną fizyką motocykla.

### Distance-limited longitudinal physics — PROVISIONAL

Od #31 production advanced Straight, eligible TurnExit i standing launch nie
korzystają z artificial attainable-speed ceiling. Usunięto model
`21–25 m/s × 0.94–1.06` i osobny gearing top-speed multiplier.
Full-drive acceleration jest signed: `a(v) = (F_drive(v) - F_resistance(v)) / 142`,
bez `max(0)` na sile netto lub acceleration. Opór pozostaje
`F_resistance(v) = 40 + 0.20*v²` N. Available drive to `referenceForce * envelope`.
Envelope jest dokładnie `1` dla `v <= 16`; powyżej:
`clamp(1 - fadeRate*(v-16), 0, 1)`, gdzie
`fadeRate = 0.0175 + (0.0050 - 0.0175)*Gearing`.

Naturalne `FullDriveEquilibriumSpeedMetersPerSecond` jest obserwacją
`drive = resistance`, nie limiterem ani targetem. Solver to deterministyczna
bisekcja w `[0, 16 + 1/fadeRate]`, maksymalnie 64 iteracje do szerokości bracketu
`1e-6 m/s` (wynik float), bez RNG i initial guess. Force <40 N nie ma
nieujemnego pierwiastka i jest odrzucane; force=40 N daje 0. Production nigdy
nie clampuje speed do equilibrium: poniżej przyspiesza, powyżej naturalnie zwalnia.

Wszystkie trzy ścieżki współdzielą signed midpoint i fixed-distance kroki
maksymalnie `1 m` z exact final remainder. Predictor i corrected step używają
`sqrt(max(0, v² + 2*a*ds))`; midpoint to `(start + predicted)/2`.
To zabezpieczenie kwadratu prędkości przy zatrzymaniu, nie clamp acceleration.
Czas przejazdu jest sumą `2*ds/(v_start+v_end)`, nie jednym średnim czasem
całego segmentu. Equilibrium nie kończy integracji.

Reference acceleration TurnExit pozostaje `0.60–1.40`, Straight `0.80–1.60`
przez Speed, razy `1.10–0.90` przez Gearing i
`0.75 + 0.25*EffectiveGrip`. Reference force to `142*referenceAcceleration
+ resistance(16)`, więc kontrakt przy 16 m/s pozostaje. Surface jest próbkowana
raz przy wejściu i nadal skaluje effective reference drive, a więc też equilibrium.
Gearing=0 ma więcej reference drive, ale szybszy fade; Gearing=1 mniej drive
i wolniejszy fade. Morale i TractionBias nie wpływają na siły ani equilibrium.

TurnExit nadal wymaga `Ok`/ `Brake`, dodatniej post-physics speed i dystansu.
Profil zaczyna od `resolution.Speed`; `RunWide` i `Crash` go nie otrzymują.
Straight i TurnExit raportują acceleration + cruise + deceleration distance
równe actual distance. Deceleration obejmuje naturalną utratę speed, a na
Straight także explicit preparation; entry net acceleration TurnExit może być ujemne.
Wszystkie profile zawierają equilibrium z dokładnie tej samej reference force/setup.

Dla immediate TurnEntry nadal działa maximum recoverable approach target oraz
backward allowed-speed envelope. Final step to
`min(fullDriveCandidate, max(allowedBoundary, preparationReachableSpeed))`.
Target jest upper constraint i nigdy nie podnosi naturalnie zwalniającego candidate.
Gdy target jest nieosiągalny, zostaje residual overspeed; preparation nie dostaje
dodatkowej deceleration. Naturalny spadek od oporów może być silniejszy niż
preparation i nie wolno go wyłączać. Corner-entry preparation pozostaje
odrębnym efektywnym modelem throttle roll-off / engine-drivetrain / slide preparation
`2.00–3.20 m/s²`; #24 TurnEntry scrub nadal używa 50% remaining distance.

Pure zero-drive helper daje `-(40 + 0.20*v²)/142`, ale NIE jest finalnym
engine-braking modelem ani zamiennikiem corner preparation. Nie dodano explicit
throttle input, wheelspin, traction-force cap ani splitu engine/traction force.
Wszystkie stałe są **PROVISIONAL / NOT REAL-WORLD CALIBRATED**, bez strojenia.
Stary analityczny `CalculateStraightSpeedProfile` to wyłącznie non-production
compatibility utility z jawnie podanym ogólnym ograniczeniem; nie wylicza Vmax,
nie jest wywoływany przez SimulationEngine i ma null equilibrium.

Lookahead obejmuje wyłącznie jeden bezpośredni segment. Target następnego `TurnEntry` wykorzystuje jego immutable nawierzchnię, pełną długość geometryczną i wejściowe `LateralPosition` zawodnika; nie korzysta z `TargetLane`, `PlannedLane`, resolved lane ani pozycji po `MoveTowards`. Ostatni Straight nie-finalnego okrążenia zawija do segmentu `0`. Ostatni segment ostatniego wymaganego okrążenia nie przygotowuje motocykla do nieistniejącego następnego łuku i wykorzystuje pozostały dystans na acceleration. Rozpoznanie opiera się na `LapIndex`, `RequiredLaps`, `SegmentIndex` i liczbie segmentów — bez `lap == 4`, bonusu mety lub specjalnej reguły wewnętrznej linii.

Realny motocykl żużlowy ma jeden bieg podczas jazdy, nie posiada klasycznego układu hamulcowego, a przełożenie jest elementem setupu. Advanced `TurnEntry` rozdziela approach speed od safe/settled speed coarse podziałem **PROVISIONAL / NOT REAL-WORLD CALIBRATED**: pierwsze `50%` faktycznie pozostałego dystansu reprezentuje roll-off, ustawienie motocykla, wejście w uślizg i scrub. Po osiągnięciu settled target przed końcem tej fazy reszta jest carry, nie dalszą deceleration. Skuteczny scrub może rozpocząć łuk szybciej niż settled speed i zakończyć się `Ok`; tylko residual overspeed trafia do niezmienionych progów `SegmentPhysics`, więc `Brake` pozostaje nazwą residual outcome, a nie literalnym hamulcem tarczowym.

Kolejność to `original entry → scrub → SegmentPhysics → random incident → distance/time`. Non-crash przejeżdża cały remaining distance, a czas wynosi czas profilu scrub plus post-scrub distance podzielony przez finalną prędkość resolution. Crash nadal przejeżdża połowę remaining progress — dokładnie provisional scrub distance — i dostaje wyłącznie czas scrub, także gdy crash powstał w random incident. Ten czas steruje również `LateralMovementModel`. Partial TurnEntry używa tylko remaining distance; surface nie jest ponownie próbkowane po ruchu bocznym.

StandingStart, Straight i TurnExit współdzielą signed 1 m midpoint opisany powyżej.
TurnEntry #24, continuous geometry/surface/wear, contact, lateral budget i legacy
zachowują własne kontrakty; signed forces nie dodają drugiego resolve ani RNG.

### Standing start / launch — PROVISIONAL / NOT REAL-WORLD CALIBRATED

`Track.CreateExample()` pozostaje 8-segmentowym compatibility layout.
`CreateStandingStartExample()` rozdziela home straight na końcowe 35 m i
początkowe 35 m po dwóch stronach canonical start/finish boundary (segment 8 → 0).
Topologia: marked Straight 35 → TurnEntry/Middle/Exit → Straight 60 → TurnEntry/Middle/Exit
→ Straight 35. Home straight ma 70 m; od startu do pierwszego łuku jest 35 m.
Długość wynika tylko z fizycznych segmentów: `130 + 6 * (24 + lateral) * PI/3`
m, około 280.8 m na inner reference trajectory, o 10 m więcej niż compatibility.
Nie ma wirtualnego dystansu startu; meta i lap summaries używają ostatniego
segmentu toru, bez hardcoded segment count.

`StraightLengthMetersOverride` jest opcjonalny, finite, dodatni i tylko dla
Straight. `IsStandingStartSegment` domyślnie false, także tylko Straight;
immutable copy zachowuje oba pola. Track dopuszcza jeden marker, wyłącznie
index 0, i wymaga końcowego Straight. Launch dotyczy tylko advanced lap 0 /
segment 0 z markerem, canonical i physical start dokładnie zero, NotStarted,
speed <=0 i dodatnim pozostałym dystansem. Entry speed jest wtedy 0, bez
bootstrapu. Kolejne okrążenia używają zwykłego Straight.

Reaction = `0.28 + (0.20 - 0.28) * StartNorm` s (Start 0/50/100 → 0.28/0.24/0.20).
Reference acceleration =
`(9.0 + (11.0 - 9.0) * StartNorm) * (1.10 + (0.90 - 1.10) * Gearing) *
(0.75 + 0.25 * EffectiveGrip)` m/s². Reference force = `142 * acceleration +
resistance(0)`. Dalej działają wspólne resistance `40 + 0.20*v²`, one-gear
envelope, kroki 1 m z exact remainder i signed midpoint bez artificial ceiling. Entry surface próbkujemy raz. TractionBias i morale
nie wpływają na launch ani reaction; brak gate-specific bonusu.

Launch przygotowuje wejście w bezpośrednio następny TurnEntry: ten sam maximum
recoverable approach target z `ResolveImmediateNextTurnApproachSpeed`, backward
allowed-speed envelope i istniejąca corner-entry deceleration co production
Straight. Fastest feasible profile to acceleration → ewentualny peak/cruise →
preparation/roll-off; bez teleportacji prędkości i bez przekraczania deceleration.
Acceleration + cruise + preparation distance = actual launch distance. Movement
sumuje `2*ds/(v_start+v_end)` wszystkich fixed-distance steps.

Elapsed obejmuje reaction + movement; lateral budget tylko movement.
Reaction nie powoduje ruchu bocznego, dystansu ani progress. Actual exit speed
staje się wejściem do niezmienionego #24 TurnEntry; legacy i compatibility
bootstrap pozostają bez zmian. Lookahead nadal dotyczy
tylko bezpośrednio następnego segmentu. Existing contact działa po profilu,
więc final duration może zawierać penalty ponad pre-contact profile total.

CSV/sample dopisuje dziewięć nullable pól reaction/movement/total, acceleration/
cruise distance, entry acceleration, TimeTo70, SpeedAt2s oraz preparation distance
na końcu kolumn. Metryki są outputs, nie targets: obie liczymy od ruchu taśmy,
z reaction delay. Czas do 70 zawiera interpolację corrected step,
speed po 2 s używa kinematyki tego kroku, także podczas preparation. Null oznacza nieosiągnięty próg lub
koniec launch przed 2 s. Nie dopasowujemy stałych do tych wyników.
Brak clutch, RPM, torque/power, real sprockets, wheelspin, slip ratio, traction
cap, false starts, reaction RNG oraz explicit throttle.

Starting-gate geometry i pełna szerokość toru nie są jeszcze fizycznie odwzorowane.
Initial `Lane/LateralPosition` pozostają compatibility representation pozycji
startowych; racing references nie są docelowym modelem pól A/B/C/D. Przed finalną
kalibracją gate effects trzeba wprowadzić oddzielne physical starting-gate
geometry/mapping. #30 kalibruje strukturę standing startu, nie finalny gate advantage.
Nie dodajemy hardcoded bonusu pola A.

### Calibration telemetry — OBSERVATION ONLY

#28 obserwuje dokładnie produkcyjny `HeatSimulator`: opcjonalny observer działa
raz po `Resolve` i przed `Commit`, a `CalibrationRunner` nie ma własnej pętli
okrążeń i segmentów. Typed samples biorą rzeczywiste entry/physics/exit/peak
speeds oraz time/distance i entry surface z immutable resolution data; peak
Straight pochodzi z actual production `StraightSpeedProfile`. Podsumowania
okrążeń i zawodników są derived observations. Nie parsują `SimLog`, działają
przy `EnableLogging=false`, a deterministic CSV ma stable ordering i invariant
culture. Harness nie dodaje RNG, nie zmienia physics ani wyników biegu i nie
porównuje jeszcze wartości z real telemetry.

#31 wprowadza breaking diagnostic-schema change przed ustaleniem real-world dataset:
`FullDriveEquilibriumSpeedMetersPerSecond` zastępuje
`AttainableTopSpeedMetersPerSecond` w RiderStepDiagnostics, CalibrationStepSample
i CSV. Nie ma dwóch pól Vmax. Dla launch/Straight/eligible TurnExit pochodzi
z użytego profilu; dla innych segmentów jest null.
CSV dodaje `TurnExitDecelerationDistanceMeters` po TurnExitCruiseDistanceMeters;
entry acceleration TurnExit jest signed. Nowy schema zachowuje stable order,
invariant culture, decimal dot, `\n` i null = empty.

Następny etap: **REAL-WORLD CALIBRATION DATASET + PARAMETER FITTING** dla reaction,
TimeTo70, SpeedAt2s, first-turn entry speed, Straight Vmax, lap times i full heat time.
Późniejsze refinements: F_engine vs F_traction, wheelspin/slip, TractionBias,
real sprockets, RPM, torque/power curve, throttle, engine braking i oddzielna
physical gate A/B/C/D geometry. #31 ich nie implementuje.

### Czasowa zmiana linii — BINDING

`TargetLane` jest celem decyzji, `PlannedLane` najbliższą dyskretną linią realizowaną w kroku, `Lane` linią rozstrzygniętą przez fizykę, a `LateralPosition` rzeczywistą ciągłą pozycją po kroku. Pozycja boczna ma zakres `0..4` w jednostkach linii; jej zmiana pomnożona przez `LaneSpacingMeters` daje fizyczne przesunięcie w metrach.

Zaawansowana fizyka ogranicza ruch boczny czasem przejazdu bieżącego segmentu, odstępem między liniami, `SlideControl`, `Adaptability` i efektywną przyczepnością. Stylowa `LaneChangeTendency` nadal wpływa na decyzję i koszt trasy, ale nie na fizyczną szybkość wykonania; morale również jej nie zmienia. `PlannedLane` jest najbliższą niewykonaną referencją od rzeczywistego `LateralPosition` w stronę `TargetLane`; kolejny krok może rozpocząć się dopiero po osiągnięciu tej referencji z tolerancją `0.05 m`. Wcześniejszy `RunWide` nie pozwala pominąć nieosiągniętej linii, a odwrócenie decyzji działa natychmiast. `RunWide` nadal kieruje ruch ku wymuszonej końcowej `Lane`; legacy nadal wyrównuje pozycję od razu.

### Parametry ruchu bocznego — PROVISIONAL

Zakres fizycznej szybkości bocznej `0.35–0.65 m/s` oraz mnożnik `0.65 + 0.35 * EffectiveGrip` są wartościami roboczymi do późniejszego strojenia. Pozostają nazwanymi stałymi w C#; ten etap nie dodaje pliku balansu JSON.

Ograniczenie przejściowe: advanced corner constraint, długość łuku, fizyczny odczyt nawierzchni i zużycie używają wspólnego `LateralPosition` na wejściu do segmentu. Surface nie jest ponownie próbkowane po `MoveTowards`, a model nie całkuje jeszcze promienia, drogi ani zużycia po pozycji zmieniającej się w czasie segmentu. Pozycje całkowite zachowują wcześniejszy kernel zużycia advanced dokładnie, a ułamkowe płynnie rozdzielają jego delty na istniejące komórki. `RunWide`, późniejszy ruch boczny ani kontakt nie przesuwają zużycia całego segmentu na końcową pozycję.

`TrackState` i `TrackEvolution` nadal przechowują oraz zmieniają dyskretną siatkę segment × lane; ciągłe zużycie interpoluje wyłącznie delty kerneli, a nie stan nawierzchni. `AdaptiveDecisionModel` ocenia dokładne dyskretne komórki kandydackich linii, contact surface pozostaje dyskretne, a `RunWide` nadal wykonuje istniejące `Lane + 1` wraz z regułą lane 4. Odczyt nawierzchni, dystans i zużycie legacy pozostają dyskretne.

Przy ocenie zajętej przestrzeni przez `AdaptiveDecisionModel` różnica `LateralPosition` jest przeliczana na metry przez `LaneSpacingMeters` konkretnego toru. Próg occupancy `0.55 m` jest wartością **PROVISIONAL**, zachowującą dotychczasowe zachowanie toru domyślnego, a nie ostatecznym wymiarem zawodnika lub motocykla.

Kandydaci do kontaktu są wybierani z pozycji bocznych po `ResolveRider` przez ten sam fizyczny przelicznik, lecz z osobnym progiem kontaktu `0.55 m` oznaczonym **PROVISIONAL** i gotowym do niezależnej kalibracji. Każdy trailing rider otrzymuje najwyżej jednego najbliższego wcześniejszego leadera, a pary są ustalane przed skutkami kontaktów. Eligibility podłużne nadal używa `0.12 s`; prawdopodobieństwo, surface oraz discriminatory RNG pozostają bez zmian i nadal pochodzą z dyskretnej `Lane` trailing ridera.

Po `ContactLostRhythm` przejściowa `Lane` nadal wykonuje jeden dyskretny krok na zewnątrz poza prostą. Ciągłe wypchnięcie `LateralPosition` jest natomiast mierzone fizycznie: obecna wartość **PROVISIONAL** wynosi `0.50 m`, jest przeliczana przez `LaneSpacingMeters` i nie może przekroczyć wymuszonej referencji `Lane` ani domeny `0..4`. Na `Straight` oraz na zewnętrznej lane 4 nie ma outward push. Nie jest to jeszcze finalny model reakcji motocykla na kontakt.

## Główna pętla gry — BINDING

`obserwacja → feedback → diagnoza → decyzja menedżera → wykonanie przez ludzi → kolejny bieg → ocena skutku`

Gracz zarządza niepewnością i ograniczonym czasem. Nie otrzymuje bezpośredniego dostępu do pełnych wartości CoreSim ani gwarantowanego skutku polecenia.

## Dokumenty

- [`match-flow.md`](match-flow.md) — fazy meczu i miejsca podejmowania decyzji.
- [`time-and-decision-windows.md`](time-and-decision-windows.md) — czas, kolejki prac i sytuacje bieg po biegu.
- [`track-management.md`](track-management.md) — docelowy Track Engine, pogoda, zużycie i zakres wpływu gospodarza.
- [`observations-and-feedback.md`](observations-and-feedback.md) — niepełna wiedza gracza i wiarygodność informacji.
- [`bike-setup.md`](bike-setup.md) — przełożenie, reakcja motocykla i praca mechaników.
- [`team-orders.md`](team-orders.md) — polecenia parowe i ograniczenia ich wykonania.
- [`staff-and-delegation.md`](staff-and-delegation.md) — dostępność personelu, równoległość i automatyzacja.
- [`rulesets.md`](rulesets.md) — przepisy zależne od ligi oraz sezonu.

## Kolejność wdrażania — BINDING

1. Ukończyć i zweryfikować Race Engine.
2. Zaprojektować oraz wdrożyć Track Engine.
3. Połączyć obciążenie generowane przez jazdę ze zmianą toru.
4. Dopiero potem wdrażać czas meczowy, setup, personel i polecenia.
5. Na końcu dołączyć konkretne regulaminy i AI menedżera.

Dokumenty opisujące późniejsze etapy mogą istnieć wcześniej, lecz ich obecność nie jest zgodą na zmianę kolejności implementacji.

## Gdzie umieszczać przyszłe ustawienia

Planowane granice:

```text
data/
├── rulesets/       # przepisy ligi i sezonu
└── balance/        # wartości czasów, efektów setupu i prac torowych
```

Dokładne pliki danych powstaną razem z systemem, który je waliduje. Nie tworzymy pustych konfiguracji bez konsumenta i testu schematu.
