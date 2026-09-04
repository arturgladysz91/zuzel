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

`SegmentPhysics` nadal rozstrzyga ograniczenie i outcome; nie jest pełnym modelem dynamiki motocykla. Po tym rozstrzygnięciu advanced `TurnExit` może wykonać pierwszy deterministyczny force-based foundation dla positive drive po `Ok` albo `Brake`. Reference available force jest kalibrowana względem dotychczasowego przyspieszenia przy `16 m/s`. One-gear envelope wynosi dokładnie `1` dla `v <= 16 m/s`; powyżej reference speed wynosi `max(0, 1 - fadeRate * (v - 16))`, gdzie `fadeRate = 0.0175 + (0.0050 - 0.0175) * Gearing` w `1/(m/s)`. Actual available force to reference force pomnożone przez envelope. Opór pozostaje `F_resistance = 40 N + 0.20 N/(m/s)² * v²`, dodatnia siła netto to `max(0, F_drive(v) - F_resistance)`, a przyspieszenie dzieli ją przez nominalne `142 kg`. Następnie istniejąca kinematyka liczy `sqrt(v_physics² + 2 * a * s)` dla rzeczywistego dystansu i zachowuje attainable ceiling.

Dotychczasowa metoda przyspieszenia i jej wzór pozostają niezmienionym targetem kalibracyjnym: `Speed` ability, corner-drive trade-off `Gearing` oraz `EffectiveGrip` wejściowej nawierzchni tworzą reference acceleration. Bazowy zakres `0.60–1.40 m/s²`, gearing `1.10–0.90`, surface `0.75 + 0.25 * EffectiveGrip`, `16 m/s`, `142 kg`, `40 N` i `0.20 N/(m/s)²` są **PROVISIONAL / NOT REAL-WORLD CALIBRATED**. Nominalna masa nie jest wagą konkretnego ridera, a aggregate resistance nie jest twierdzeniem o CdA ani czystym aerodynamic drag. `Gearing` i surface nie dostają drugiego mnożnika; `TractionBias`, morale, style, `SlideControl` i pozostałe umiejętności nie zmieniają available force.

Zachowanie #25 jest identyczne przy i poniżej `16 m/s`. Powyżej reference speed drive-oriented (`Gearing = 0`) zachowuje większą siłę początkową, ale używa szybszego fade `0.0175 /(m/s)`; speed-oriented (`Gearing = 1`) zaczyna niżej, lecz zanika wolniej z `0.0050 /(m/s)`. Krzywe mogą dzięki temu naturalnie się przeciąć bez osobnego bonusu crossover. Gdy resistance osiąga available force, przyspieszenie wynosi `0`, nigdy wartość ujemną. Wszystkie fade rates są **PROVISIONAL / NOT REAL-WORLD CALIBRATED**.

Osiągalna prędkość szczytowa wynosi `baseTopSpeed * gearingMultiplier`, gdzie `baseTopSpeed = 21 + (25 - 21) * normalizedSpeed`, a `gearingMultiplier = 0.94 + (1.06 - 0.94) * Gearing`. Zakresy `21–25 m/s` i `0.94–1.06` są **PROVISIONAL / NOT REAL-WORLD CALIBRATED**: nie pochodzą jeszcze z docelowej telemetrii i nie są finalną prędkością prawdziwego motocykla. Oś `Gearing` to `drive-oriented ↔ speed-oriented`: `0` wzmacnia corner-exit drive, ale obniża osiągalną prędkość; `1` osłabia drive, ale podnosi osiągalną prędkość. Nawierzchnia nie zmienia tej granicy. Nie jest to hard limiter istniejącego overspeedu: gdy wejściowa prędkość już osiąga albo przekracza granicę, positive drive jej nie zmniejsza. Wzory i zachowanie #22 pozostają bez zmian.

Od #29 eligible advanced `TurnExit` zaczyna positive-drive profile dokładnie od
post-`SegmentPhysics` `resolution.Speed` i używa shared fixed-distance midpoint
traversal. Eligibility pozostaje bez zmian: tylko `Ok`/`Brake`, dodatnia speed i
dodatni dystans. `RunWide` nie otrzymuje positive drive, `Crash` pozostaje z
prędkością zero, a `TurnEntry`, `TurnMiddle` i legacy nie dostają profilu.
TurnExit profile time jest produkcyjnym czasem segmentu i steruje istniejącym
ruchem bocznym.

Od #27 advanced `Straight` ma deterministyczny speed-dependent force traversal. Rzeczywisty dystans dzieli na fixed-distance kroki maksymalnie `1 m` (**PROVISIONAL / NUMERICAL INTEGRATION RESOLUTION**), a ostatnia reszta nie jest zaokrąglana. Każdy full-drive krok liczy predictor z `a_start`, midpoint speed `(start + predicted)/2` i corrected `a_mid`, a następnie `v_next² = v_start² + 2*a_mid*ds`. Nie ma timestepu, zależności od FPS ani ponownego próbkowania nawierzchni co metr.

Straight reference acceleration to istniejące **PROVISIONAL** `0.80–1.60 m/s²` przez `Speed`, razy wejściowe `0.75 + 0.25 * EffectiveGrip`, razy ten sam drive multiplier `1.10–0.90` przez `Gearing`. Reference force wynosi `142 kg * referenceAcceleration + resistance(16)`. Wspólny z #26 one-gear envelope wyznacza actual available force; opór nadal wynosi `40 + 0.20*v²`, a positive net acceleration jest nieujemna. Drive-oriented ma więcej reference drive, ale szybszy fade; speed-oriented ma mniej reference drive, ale utrzymuje je dłużej.

Jeżeli nie ma targetu następnego łuku, profil przyspiesza do retained ceiling i wykorzystuje pozostały dystans na cruise; istniejący overspeed nie jest clampowany. Dla bezpośredniego `TurnEntry` target nadal oznacza maximum recoverable approach speed. Od targetu budowany jest wsteczny allowed-speed envelope z istniejącej corner-entry deceleration. Forward traversal może przejść dokładnie na boundary tylko wtedy, gdy jest ono fizycznie osiągalne; w przeciwnym razie używa maksymalnej istniejącej deceleration, więc brak dystansu nie teleportuje speed. Target powyżej full-drive exit lub ceiling nie wywołuje niepotrzebnej preparation.

Czas Straight jest sumą `2*ds/(v_start+v_end)` dla wszystkich kroków, a ten sam czas steruje `LateralMovementModel`. Każdy krok trafia do acceleration, cruise albo deceleration, a ich dystanse sumują się do dokładnego dystansu wejściowego. Stary analityczny `CalculateStraightSpeedProfile` pozostaje testowany, lecz nie jest już produkcyjną ścieżką advanced Straight.

Lookahead obejmuje wyłącznie jeden bezpośredni segment. Target następnego `TurnEntry` wykorzystuje jego immutable nawierzchnię, pełną długość geometryczną i wejściowe `LateralPosition` zawodnika; nie korzysta z `TargetLane`, `PlannedLane`, resolved lane ani pozycji po `MoveTowards`. Ostatni Straight nie-finalnego okrążenia zawija do segmentu `0`. Ostatni segment ostatniego wymaganego okrążenia nie przygotowuje motocykla do nieistniejącego następnego łuku i wykorzystuje pozostały dystans na acceleration. Rozpoznanie opiera się na `LapIndex`, `RequiredLaps`, `SegmentIndex` i liczbie segmentów — bez `lap == 4`, bonusu mety lub specjalnej reguły wewnętrznej linii.

Realny motocykl żużlowy ma jeden bieg podczas jazdy, nie posiada klasycznego układu hamulcowego, a przełożenie jest elementem setupu. Advanced `TurnEntry` rozdziela approach speed od safe/settled speed coarse podziałem **PROVISIONAL / NOT REAL-WORLD CALIBRATED**: pierwsze `50%` faktycznie pozostałego dystansu reprezentuje roll-off, ustawienie motocykla, wejście w uślizg i scrub. Po osiągnięciu settled target przed końcem tej fazy reszta jest carry, nie dalszą deceleration. Skuteczny scrub może rozpocząć łuk szybciej niż settled speed i zakończyć się `Ok`; tylko residual overspeed trafia do niezmienionych progów `SegmentPhysics`, więc `Brake` pozostaje nazwą residual outcome, a nie literalnym hamulcem tarczowym.

Kolejność to `original entry → scrub → SegmentPhysics → random incident → distance/time`. Non-crash przejeżdża cały remaining distance, a czas wynosi czas profilu scrub plus post-scrub distance podzielony przez finalną prędkość resolution. Crash nadal przejeżdża połowę remaining progress — dokładnie provisional scrub distance — i dostaje wyłącznie czas scrub, także gdy crash powstał w random incident. Ten czas steruje również `LateralMovementModel`. Partial TurnEntry używa tylko remaining distance; surface nie jest ponownie próbkowane po ruchu bocznym.

Straight i TurnExit współdzielą `1 m` numerical resolution, exact remainder,
tworzenie kroków, one-gear envelope, resistance, generic net-positive-drive
helper oraz midpoint step. Integration jest dystansowa, nie fixed-time, i nie
próbkuje ponownie surface co metr. TurnExit zachowuje własne reference
`0.60–1.40 × gearing × surface`, a Straight własne
`0.80–1.60 × gearing × surface`. Advanced lateral movement na Straight,
TurnEntry i eligible TurnExit otrzymuje rzeczywisty czas odpowiedniego profilu.
Model nie przelicza profilu po `MoveTowards`; surface i geometria nadal używają
immutable segment-entry position. `TurnMiddle`, legacy i cały model #24
TurnEntry są niezmienione. Zakresy `21–25 m/s` i `0.94–1.06` nadal obowiązują
jako artificial ceiling i są **PROVISIONAL / NOT REAL-WORLD CALIBRATED**.
Signed negative drag/coast nadal nie istnieje; #30 dodaje poniższy provisional standing start.

### Standing start / launch — PROVISIONAL / NOT REAL-WORLD CALIBRATED

`Track.CreateExample()` pozostaje 8-segmentowym compatibility layout.
`CreateStandingStartExample()` rozdziela home straight na końcowe 30 m i
początkowe 30 m po dwóch stronach canonical start/finish boundary. Topologia:
marked Straight 30 → TurnEntry/Middle/Exit → Straight 60 → TurnEntry/Middle/Exit
→ Straight 30. Długość okrążenia dla tej samej pozycji bocznej nie zmienia się.
Nie ma wirtualnego dystansu startu; meta i lap summaries używają ostatniego
segmentu toru, bez hardcoded segment count.

`StraightLengthMetersOverride` jest opcjonalny, finite, dodatni i tylko dla
Straight. `IsStandingStartSegment` domyślnie false, także tylko Straight;
immutable copy zachowuje oba pola. Track dopuszcza jeden marker, wyłącznie
index 0, i wymaga końcowego Straight. Launch dotyczy tylko advanced lap 0 /
segment 0 z markerem, canonical i physical start dokładnie zero, NotStarted,
speed <=0 i dodatnim pozostałym dystansem. Entry speed jest wtedy 0, bez
bootstrapu. Kolejne okrążenia używają zwykłego Straight.

Reaction = `0.30 + (0.20 - 0.30) * StartNorm` s. Reference acceleration =
`(4.5 + (5.5 - 4.5) * StartNorm) * (1.10 + (0.90 - 1.10) * Gearing) *
(0.75 + 0.25 * EffectiveGrip)` m/s². Reference force = `142 * acceleration +
resistance(0)`. Dalej działają wspólne resistance `40 + 0.20*v²`, one-gear
envelope, kroki 1 m z exact remainder i midpoint oraz dotychczasowy artificial
ceiling `21–25 × 0.94–1.06`. Entry surface próbkujemy raz. TractionBias i morale
nie wpływają na launch ani reaction; brak gate-specific bonusu.

Elapsed obejmuje reaction + movement; lateral budget tylko movement.
Reaction nie powoduje ruchu bocznego, dystansu ani progress. Actual exit speed
staje się wejściem do niezmienionego #24 TurnEntry; #29 TurnExit, normal Straight,
legacy i compatibility bootstrap pozostają bez zmian. Lookahead nadal dotyczy
tylko bezpośrednio następnego segmentu. Existing contact działa po profilu,
więc final duration może zawierać penalty ponad pre-contact profile total.

CSV/sample dopisuje osiem nullable pól reaction/movement/total, acceleration/
cruise distance, entry acceleration, TimeTo70 i SpeedAt2s. Metryki są outputs,
nie targets: czas do 70 zawiera reaction i interpolację corrected step,
speed po 2 s używa kinematyki tego kroku. Null oznacza nieosiągnięty próg lub
koniec launch przed 2 s. Nie dopasowujemy stałych do tych wyników.
Brak clutch, RPM, torque/power, real sprockets, wheelspin, slip ratio, traction
cap, false starts, reaction RNG oraz signed negative coasting.

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

Od #29 harness eksportuje production `TurnExitDriveProfile`: acceleration/cruise
distance i profile travel time. Zachowany `TurnExitNetAcceleration` oznacza
entry net acceleration początku profilu, nie jedną acceleration dla segmentu.
Compatibility bootstrap nie jest fizycznym startem; jawny launch #30 pozostaje
provisional. Current artificial attainable ceiling nadal istnieje, a signed
negative drag/coasting nie istnieje. Roadmap: signed net force i natural
resistance deceleration przygotowujące usunięcie ceiling, a następnie
real-world calibration targets.

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
