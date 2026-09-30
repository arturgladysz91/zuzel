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
- w advanced physics `LateralPosition` jest bezwymiarową współrzędną `0..4`, a promień ograniczenia pochodzi z fizycznego offsetu wyliczonego z lokalnej szerokości łuku: `InnerRadiusMeters + (LateralPosition / 4) * usableTurnWidth`; `InnerRadiusMeters` opisuje linię pomiarową/referencyjną 1 m od inner edge, nie krawężnik,
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

Od #31 production advanced Straight, continuous-corner drive i standing launch nie
korzystają z artificial attainable-speed ceiling. Usunięto model
`21–25 m/s × 0.94–1.06` i osobny gearing top-speed multiplier.
Full-drive acceleration jest signed: `a(v) = (F_drive(v) - F_resistance(v)) / 142`,
bez `max(0)` na sile netto lub acceleration. Opór pozostaje
`F_resistance(v) = 40 + 0.20*v²` N. Available drive to `referenceForce * envelope`.
Envelope jest dokładnie `1` dla `v <= 16`; powyżej:
`clamp(1 - fadeRate*(v-16), 0, 1)`, gdzie
`fadeRate = 0.0350 + (0.0100 - 0.0350)*Gearing` po kalibracji #36.

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

Reference acceleration po #36 wynosi corner full-drive `1.20–2.80`, Straight `1.60–3.20`
przez Speed, razy `1.10–0.90` przez Gearing i
`0.75 + 0.25*EffectiveGrip`. Reference force to `142*referenceAcceleration
+ resistance(16)`, więc kontrakt przy 16 m/s pozostaje. Surface jest próbkowana
raz przy wejściu i nadal skaluje effective reference drive, a więc też equilibrium.
Gearing=0 ma więcej reference drive, ale szybszy fade; Gearing=1 mniej drive
i wolniejszy fade. Morale i TractionBias nie wpływają na siły ani equilibrium.

Od #38 corner full-drive nie jest wybierany przez etykietę TurnExit. Jego
dostępność rośnie płynnie po apexie zgodnie z CornerProgress, a każdy metr
traversal należy dokładnie do correction, carry albo drive. Przy pełnej
dostępności używany jest dokładnie istniejący signed-force endpoint; jego net
acceleration może być ujemne. `RunWide` nie otrzymuje dodatniego drive, a Crash
pozostaje terminalny.

Dla immediate logical corner targetem jest canonical continuous envelope przy
`CornerProgress = 0`, wspólny dla Straight, standing-start preparation i samego
corner traversal. Backward allowed-speed envelope zachowuje final step
`min(fullDriveCandidate, max(allowedBoundary, preparationReachableSpeed))`.
Target jest upper constraint i nigdy nie podnosi naturalnie zwalniającego candidate.
Gdy target jest nieosiągalny, zostaje residual overspeed; preparation nie dostaje
dodatkowej deceleration. Naturalny spadek od oporów może być silniejszy niż
preparation i nie wolno go wyłączać. Corner correction pozostaje efektywnym
modelem throttle roll-off / engine-drivetrain / slide preparation
`2.00–3.20 m/s²`, ale nie jest już osobną fazą przypisaną do TurnEntry.

Pure zero-drive helper daje `-(40 + 0.20*v²)/142`, ale NIE jest finalnym
engine-braking modelem ani zamiennikiem corner preparation. Nie dodano explicit
throttle input, wheelspin, traction-force cap ani splitu engine/traction force.
W #36 zestrojono wyłącznie zakresy reference acceleration Straight/corner full-drive i oba
końce fade. Masa, resistance, reference speed, integration step, gearing/surface
mapping pozostają **PROVISIONAL / NOT REAL-WORLD CALIBRATED**. #38 zmienia
wyłącznie advanced settled-corner reference z `16` na `19 m/s` po ograniczonym
screenie A0/B17/B18/B19; legacy reference pozostaje `16 m/s`.
Stary analityczny `CalculateStraightSpeedProfile` to wyłącznie non-production
compatibility utility z jawnie podanym ogólnym ograniczeniem; nie wylicza Vmax,
nie jest wywoływany przez SimulationEngine i ma null equilibrium.

Lookahead obejmuje wyłącznie jeden bezpośredni segment. Target następnego `TurnEntry` wykorzystuje jego immutable nawierzchnię, pełną długość geometryczną i wejściowe `LateralPosition` zawodnika; nie korzysta z `TargetLane`, `PlannedLane`, resolved lane ani pozycji po `MoveTowards`. Ostatni Straight nie-finalnego okrążenia zawija do segmentu `0`. Ostatni segment ostatniego wymaganego okrążenia nie przygotowuje motocykla do nieistniejącego następnego łuku i wykorzystuje pozostały dystans na acceleration. Rozpoznanie opiera się na `LapIndex`, `RequiredLaps`, `SegmentIndex` i liczbie segmentów — bez `lap == 4`, bonusu mety lub specjalnej reguły wewnętrznej linii.

Realny motocykl żużlowy ma jeden bieg podczas jazdy, nie posiada klasycznego układu hamulcowego, a przełożenie jest elementem setupu. W advanced physics #38 recoverable speed przed apexem wynika z pozostałego fizycznego dystansu redukcji, a po apexie jest odbudowywany przez istniejący signed turn-drive model. `Brake` oznacza przekroczenie lokalnego recoverable envelope, nie settled apex speed na początku zakrętu ani literalne hamowanie tarczowe.

StandingStart, Straight i continuous corner drive współdzielą signed 1 m midpoint
opisany powyżej. Continuous geometry/surface/wear, contact, lateral budget i
legacy zachowują własne kontrakty; signed forces nie dodają drugiego resolve ani RNG.

### Standing start / launch — PROVISIONAL / NOT REAL-WORLD CALIBRATED

`Track.CreateExample()` pozostaje 8-segmentowym compatibility layout.
`CreateStandingStartExample()` rozdziela home straight na końcowe 35 m i
początkowe 35 m po dwóch stronach canonical start/finish boundary (segment 8 → 0).
Topologia: marked Straight 35 → TurnEntry/Middle/Exit → Straight 60 → TurnEntry/Middle/Exit
→ Straight 35. Home straight ma 70 m; od startu do pierwszego łuku jest 35 m.
Długość wynika tylko z fizycznych segmentów:
`130 + 2 * PI * (24 + 3 * lateral)` m, około 280.796 m na inner
measurement/reference trajectory i 356.195 m na outer reference trajectory.
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

Launch przygotowuje wejście w bezpośrednio następny logical corner: ten sam
canonical envelope target przy `CornerProgress = 0`, backward allowed-speed
envelope i istniejąca corner correction capability co production Straight.
Fastest feasible profile to acceleration → ewentualny peak/cruise →
preparation/roll-off; bez teleportacji prędkości i bez przekraczania deceleration.
Acceleration + cruise + preparation distance = actual launch distance. Movement
sumuje `2*ds/(v_start+v_end)` wszystkich fixed-distance steps.

Elapsed obejmuje reaction + movement; lateral budget tylko movement.
Reaction nie powoduje ruchu bocznego, dystansu ani progress. Actual exit speed
staje się wejściem do continuous corner traversal; legacy i compatibility
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

Physical straight/turn width jest odwzorowana niezależnie od znormalizowanych
pozycji, ale starting-gate geometry nadal nie jest. Initial `Lane/LateralPosition` pozostają compatibility representation pozycji
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
i CSV. Nie ma dwóch pól Vmax. Dla launch/Straight/continuous corner drive
pochodzi z użytego profilu; dla innych segmentów jest null.
CSV dodaje `TurnExitDecelerationDistanceMeters` po TurnExitCruiseDistanceMeters;
entry acceleration TurnExit jest signed. Nowy schema zachowuje stable order,
invariant culture, decimal dot, `\n` i null = empty.

Pierwszy bounded longitudinal fitting wykonano w #36 bez zmiany reaction,
TimeTo70/SpeedAt2s targets, corner envelope ani fizyki pól startowych.
Późniejsze refinements: F_engine vs F_traction, wheelspin/slip, TractionBias,
real sprockets, RPM, torque/power curve, throttle, engine braking i oddzielna
physical gate A/B/C/D geometry. #31 ich nie implementuje.

### Czasowa zmiana linii — BINDING

`TargetLane` jest celem decyzji, `PlannedLane` najbliższą dyskretną referencją realizowaną w kroku, `Lane` referencją rozstrzygniętą przez fizykę, a `LateralPosition` rzeczywistą ciągłą pozycją po kroku. Obie domeny pozostają `0..4`, lecz są znormalizowane i bezwymiarowe. Ułamek szerokości to `LateralPosition / 4`; fizyczny offset od inner reference trajectory to ten ułamek razy usable span bieżącego segmentu. Odwrotna konwersja to `offset / usableSpan * 4`.

Zaawansowana fizyka ogranicza ruch boczny czasem przejazdu bieżącego segmentu, odstępem między liniami, `SlideControl`, `Adaptability` i efektywną przyczepnością. Stylowa `LaneChangeTendency` nadal wpływa na decyzję i koszt trasy, ale nie na fizyczną szybkość wykonania; morale również jej nie zmienia. `PlannedLane` jest najbliższą niewykonaną referencją od rzeczywistego `LateralPosition` w stronę `TargetLane`; kolejny krok może rozpocząć się dopiero po osiągnięciu tej referencji z tolerancją `0.05 m`. Wcześniejszy `RunWide` nie pozwala pominąć nieosiągniętej linii, a odwrócenie decyzji działa natychmiast. `RunWide` nadal kieruje ruch ku wymuszonej końcowej `Lane`; legacy nadal wyrównuje pozycję od razu.

### Parametry ruchu bocznego — PROVISIONAL

Zakres `0.35–0.65` oznacza znormalizowaną szybkość traversal w lane-units/s, nie fizyczne m/s. Mnożnik `0.65 + 0.35 * EffectiveGrip` pozostaje bez zmian. Maksymalny normalized delta jest jawnie zamieniany na metry przez spacing pochodzący z lokalnej szerokości, ruch odbywa się w metrach, a wynik wraca do `0..4`. To korekta jednostki, nie tuning.

Ograniczenie przejściowe: advanced corner constraint, długość łuku, fizyczny odczyt nawierzchni i zużycie używają wspólnego `LateralPosition` na wejściu do segmentu. Surface nie jest ponownie próbkowane po `MoveTowards`, a model nie całkuje jeszcze promienia, drogi ani zużycia po pozycji zmieniającej się w czasie segmentu. Pozycje całkowite zachowują wcześniejszy kernel zużycia advanced dokładnie, a ułamkowe płynnie rozdzielają jego delty na istniejące komórki. `RunWide`, późniejszy ruch boczny ani kontakt nie przesuwają zużycia całego segmentu na końcową pozycję.

`TrackState` i `TrackEvolution` nadal przechowują oraz zmieniają dyskretną siatkę segment × lane; ciągłe zużycie interpoluje wyłącznie delty kerneli, a nie stan nawierzchni. `AdaptiveDecisionModel` ocenia dokładne dyskretne komórki kandydackich linii, contact surface pozostaje dyskretne, a `RunWide` nadal wykonuje istniejące `Lane + 1` wraz z regułą lane 4. Odczyt nawierzchni, dystans i zużycie legacy pozostają dyskretne.

Przy ocenie zajętej przestrzeni przez `AdaptiveDecisionModel` różnica `LateralPosition` jest przeliczana na metry przez fizyczną szerokość bieżącego segmentu. Próg occupancy `0.55 m` jest wartością **PROVISIONAL**, zachowującą dotychczasowe zachowanie toru domyślnego, a nie ostatecznym wymiarem zawodnika lub motocykla.

Kandydaci do kontaktu są wybierani z pozycji bocznych po `ResolveRider` przez ten sam fizyczny przelicznik, lecz z osobnym progiem kontaktu `0.55 m` oznaczonym **PROVISIONAL** i gotowym do niezależnej kalibracji. Każdy trailing rider otrzymuje najwyżej jednego najbliższego wcześniejszego leadera, a pary są ustalane przed skutkami kontaktów. Eligibility podłużne nadal używa `0.12 s`; prawdopodobieństwo, surface oraz discriminatory RNG pozostają bez zmian i nadal pochodzą z dyskretnej `Lane` trailing ridera.

Po `ContactLostRhythm` przejściowa `Lane` nadal wykonuje jeden dyskretny krok na zewnątrz poza prostą. Ciągłe wypchnięcie `LateralPosition` jest natomiast mierzone fizycznie: obecna wartość **PROVISIONAL** wynosi `0.50 m`, jest przeliczana przez lokalną szerokość segmentu i nie może przekroczyć wymuszonej referencji `Lane` ani domeny `0..4`. Ta sama liczba metrów daje mniejszą zmianę normalized position na szerszej części toru. Na `Straight` oraz na zewnętrznej lane 4 nie ma outward push.

`TrackGeometry` przechowuje osobno `StraightWidthMeters` i `TurnWidthMeters`.
Usable span odejmuje FIM inner measurement/reference offset 1 m oraz oddzielny,
provisional game margin 1 m od outer edge. `CreateExample()` ma syntetyczne
6 m / 6 m i zachowuje 1 m reference spacing; nie jest legal-size speedway
track. `CreateStandingStartExample()` ma 10 m / 14 m jako przykład minimalnego
FIM envelope, nie globalny rozmiar prawdziwych torów. Daje spacing 2 m / 3 m i
turn radii 24/27/30/33/36 m.

Surface nadal ma pięć znormalizowanych bandów; interpolation, grip i wear kernel
nie zmieniają stałych. Ten sam normalized position oznacza ten sam fraction na
straight i turn, a fizyczny offset zmienia się segment-local bez dodatkowego
movement eventu. Nie ma jeszcze width-transition spline ani dodatkowego czasu/
dystansu na rozszerzenie, diagonal/spiral correction aktywnego ruchu bocznego,
rider/motorcycle width czy fizycznego modelu pól A/B/C/D.

## Główna pętla gry — BINDING

`obserwacja → feedback → diagnoza → decyzja menedżera → wykonanie przez ludzi → kolejny bieg → ocena skutku`

Gracz zarządza niepewnością i ograniczonym czasem. Nie otrzymuje bezpośredniego dostępu do pełnych wartości CoreSim ani gwarantowanego skutku polecenia.

## Dokumenty

- [`physical-interaction-contract.md`](physical-interaction-contract.md) — propozycja wspólnego kontraktu fizycznego: cztery kanały, cztery regulatory, stan toru, objawy i testy akceptacyjne; do niezależnego review, bez zmiany fizyki produkcyjnej.
- [`rider-model.md`](rider-model.md) — docelowy model zawodnika: umiejętności, Kondycja, doświadczenie, preferencje, rozwój, relacje i trening.
- [`match-flow.md`](match-flow.md) — fazy meczu i miejsca podejmowania decyzji.
- [`time-and-decision-windows.md`](time-and-decision-windows.md) — czas, kolejki prac i sytuacje bieg po biegu.
- [`track-management.md`](track-management.md) — docelowy Track Engine, pogoda, zużycie i zakres wpływu gospodarza.
- [`observations-and-feedback.md`](observations-and-feedback.md) — niepełna wiedza gracza i wiarygodność informacji.
- [`bike-setup.md`](bike-setup.md) — dwa motocykle, silniki, sprzętowa dyspozycja, tuner i cztery główne regulatory setupu.
- [`team-orders.md`](team-orders.md) — polecenia parowe i ograniczenia ich wykonania.
- [`staff-and-delegation.md`](staff-and-delegation.md) — role managera, zawodnika, mechanika i tunera, kolejka prac, równoległość i delegowanie.
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

## Calibration evidence and gameplay meaning

The PGEE snapshot in `data/calibration/pge/v1` is validation evidence for distributions, not a set of hard limits or a roster mapping. Rider age/category never supplies a physics multiplier, no real rider receives an arbitrary RiderSkills value, and Skill 50 does not mean “average PGE Ekstraliga.” Individual performance may later include execution variation, but #32 adds no such RNG.

CleanPhysics is only a conservative analysis subset; other retained rows are not declared invalid. Absolute times depend on exact track geometry, so the example track is not fitted directly to every PGEE venue. Vmax remains a distribution. Source `speed_2s` and `curve_speed` are gate rankings, while reaction populations come from separate task-supplied literature context. #32 provides measurement/evaluation infrastructure without physics changes; #33 is the first empirical tuning step.

### Continuous corner correction (#34) — HISTORICAL FOUNDATION

Advanced corner constraints classify the situation before changing recoverable
speed. `Brake` is retained as a compatibility outcome name but means controlled
roll-off/setting/slide speed scrub, not a mechanical brake. The unchanged
effective correction consumes physical metres and seconds; insufficient distance
therefore leaves residual overspeed.

TurnEntry keeps its first-half coarse scrub and corrects only through the
remaining half. TurnMiddle corrects and carries. TurnExit corrects before drive,
and drive may use only unconsumed distance. RunWide correction is continuous and
never followed by TurnExit drive; Crash remains terminal. The entry surface and
entry `LateralPosition` still define the segment geometry/capability, without
radius integration or resampling during lateral movement. Legacy and random
incident semantics remain separate and unchanged. #34 performs no physics
calibration.

### Continuous corner envelope (#38) — BINDING

Advanced longitudinal corner behavior depends on `CornerProgress`, not on the
TurnEntry/TurnMiddle/TurnExit label. Those labels remain topology, surface and
reporting compatibility only. Before the provisional apex at `0.5`, the
recoverable envelope is
`sqrt(v_apex² + 2*a_correction*(0.5-p)*totalCornerLength)`. After the apex,
the existing signed turn-drive endpoint builds the envelope over physical
distance. Net drive availability is zero through the apex, smoothstep to full
between `0.5` and `5/6`, then one.

Correction, carry and drive consume disjoint physical metres in existing 1 m
steps plus the exact remainder. There is no teleport, hard speed cap, outer-line
bonus or final-corner bonus. Straight and standing-start preparation query the
same envelope at progress zero. The advanced production engine no longer calls
the historical TurnEntry scrub or segment-gated TurnExit drive. The advanced
settled reference is calibrated to `19 m/s`; all #36 longitudinal and start
constants, correction capability, outcome factors, incident/contact/lateral/
surface behavior and legacy physics remain unchanged.
