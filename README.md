# Speedway Manager (Żużlowy) – Core Simulation

Release platform: **Windows only**. See the current [Windows CI policy](docs/ci.md)
for Draft/full validation, the merge gate and optional historical audits.

[Track Awareness & Anticipatory Riding #134](docs/experiments/track-awareness-anticipatory-riding/README.md)
adds a read-only geometry profile and a separate soft continuation estimate to
the unchanged H09 action evaluation. Two-seed A/B learning starts at #133 A0
#12023; sealed new states and an unseen track test transfer. Physics, safety,
action space, reaction and defaults remain unchanged; the experiment is OFF.

[Rider Decision Optimization #133](docs/experiments/rider-decision-optimization/README.md)
compares the original maneuver score with a robust direction cost on identical
full-throttle actions and native CEM ranking, then studies R0/R1 selection on the
same frozen pool. Physics and defaults remain unchanged; the experiment is OFF.

[Full Throttle Learning #132](docs/experiments/full-throttle-learning/README.md)
isolates an optional full-throttle candidate with the same H09 predictor, eight
preferences and native physics. It compares unchanged #131 weights and controlled
two-seed learning, then freezes a learned policy before a new 36-case final test.
The experiment remains default OFF.

[Autonomous Speedway Rider #131](docs/experiments/autonomous-speedway-extended-learning/README.md)
compares H06/H09 with two seeds, then trains one continuous two-bend controller.
The sealed test reaches 24/24 single bends and 12/12 two bends, but paired time
regression keeps the verdict PARTIAL. Original physics and defaults remain unchanged.

[Free Racing (#128)](docs/experiments/free-racing.md) isolates path-free throttle
and steering with a gated, deterministic small-policy trainer. The first native
preflight crosses the outer boundary before the bend; training remains stopped.
PARTIAL foundation, default OFF, with the failed trajectory visible in the replay.

Isolated research prototype: [Shared World Speedway Simulation](docs/experiments/shared-world-speedway.md)
uses one Box2D.NET world and causal rider observations. Recommendation B: full
contact-free heats work; crowded-contact sensitivity and post-crash boundaries
still block adoption. Production CoreSim and defaults remain unchanged.

[Speed, Line and Rider Root-Cause Audit (#124)](docs/experiments/speed-line-rider-root-cause.md)
uses saved captures only. It separates contact response from total grip, traces
delayed throttle and measured losses, and recommends a small causal planning
experiment. No physics or rider behavior changes; tyre realism remains unverified.

[Tyre / Speedway Surface Diagnostic (#118)](docs/experiments/tyre-surface-diagnostic.md)
queries the original contacts without new worlds. A locally stable nonzero-slip
operating point exists at partial throttle, but the saved Egg state exceeds the
absolute force bound. Real soil calibration and the need for tyre changes remain
inconclusive; no physics or controller changes.

[Feasible 500cc Corner Entry (#119)](docs/experiments/feasible-500cc-corner-entry.md)
demonstrates a safe first Egg Mild bend after early throttle and line decisions.
The default-OFF rider rule executes the proven physical prefix and restores gas
after slip clears. Speed falls from74.05 to44.58km/h through the bend; fast
cornering is not certified. Engine, tyres, geometry and steering laws are unchanged.

[Reachable Steering / Line Tracking (#120)](docs/experiments/reachable-steering-line-tracking.md)
replays the complete successful #119 capture. Entry demand exceeds even total
contact capacity before the steering-rate limiter activates. A single default-OFF
reachable-angle search trial barely changes peak error and fails later recovery;
keep #117 and address feasible preparation before increasing throttle.

[Prepared Path & Speed Planning](docs/experiments/feasible-path-speed-planning.md)
tests three frozen first-bend variants over #120. Combined geometry/throttle
finishes safely and raises exit44.58→54.92km/h, but peak path error remains5.91m
and gas oscillates. Grade B; default OFF, unchanged #116/#117 and tyre physics.

[Continuous Corner Planning (#123)](docs/experiments/continuous-corner-planning.md)
passes two consecutive Egg Mild bends in one world with the exact #122 C prefix.
Delayed C2 path handover preserves pending throttle and policy memory. Second-bend
peak error is1.47m and exit57.84km/h; no lap/realism claim, new flag default OFF.

[Controlled Slip / Travel-Direction Steering (#117)](docs/experiments/controlled-slip-travel-direction.md) adds a
default-OFF local contact-response controller distinguishing body yaw from velocity
direction. Short feasible arcs and an explicit coast recovery pass; Egg Mild INNER
with full gas still crosses the boundary and is not certified. No engine/tyre changes.

[500cc Motorcycle Performance](docs/experiments/motorcycle-performance-500cc.md)
adds independent default-OFF engine-curve and load-based tyre variants. Combined,
they reach112.22km/h after62m from18m/s; legacy reaches81.48km/h. The original
steering loses stability in the first Egg Mild bend; longer trials stay gated.

[Adaptive Rider Decisions](docs/experiments/adaptive-rider-decisions.md) adds a
default-OFF single-rider choice of throttle and temporary turning intent over
#113 physical steering, with #114 load transfer OFF. Eight laps complete with
real throttle changes, but the conservative outer safety-margin gate FAILS.

[Dynamic Axle Load LIGHT (#114)](docs/experiments/dynamic-axle-load-light.md)
tests default-OFF load allocation cached from accepted velocity differences.
One first bend stays safe, but the unchanged steering inverse uses 12.3% more
queries. The LIGHT acceptance gate fails; no second bend or eight-lap repeat.

[Physical Steering Control (#113)](docs/experiments/physical-steering-control.md)
adds an opt-in local yaw-response inverse for the two-contact prototype. The same
tyres, engine and C3 INNER complete the first two bends and eight laps safely.
Full-drive INNER tracking still fails after acceleration: parts of the requested
curvature exceed the frozen force budget. Both experiment flags remain OFF.

[Two Contact Bike Dynamics, Stage1](docs/experiments/two-contact-bike-dynamics.md)
adds default-OFF `EnableTwoContactBikeDynamics` in that same experimental world.
Front steering and local axle velocities generate moments through actual Box2D
point forces, with the old yaw actuator excluded. Isolated tests preserve energy
and turn signs; the first track trial runs wide and stops. Grade B: mechanical
foundation with provisional steering/axle data, not a calibrated drifting model.

[Rear Wheel Dynamics (#100)](docs/experiments/rear-wheel-dynamics.md) adds default-OFF
`EnableRearWheelDynamics`, requiring slip dynamics. Its independent wheel state,
combined rear contact and whole-system energy ledger remain PROVISIONAL.

[Track Geometry Variants (#101)](docs/experiments/track-geometry-variants.md) compares
four compressed, smooth bend shapes with the unchanged stadium and two controls.
Geometry is an optional tools-only input; tyre/wheel/yaw equations and production
defaults stay unchanged. Egg Mild is promising, with retained unsafe inner plans
for stronger shapes; empirical venue validation is still required.

[Local Outer Offset Limit (#102)](docs/experiments/local-offset-limit.md) adds a
default-OFF, cached local target envelope for the complete footprint plus 0.5 m
outer clearance. Egg Mild can request roughly 14 m instead of 9.5 m; maximal
plans fail the unchanged dynamics at the narrowing exit and remain rejected in
the data and replay viewer. The conservative middle-to-outer plan stays safe.

[Progressive Bend Width (#103)](docs/experiments/progressive-bend-width.md) changes
only the experimental outer edge of Egg Mild. Gradual widening/narrowing makes
the unchanged outer-tighten plan safe; outer-max still fails. Symmetric profile B
is the working hypothesis, with no surveyed Motoarena realism claim or production
default change. The existing replay viewer compares A/B/C boundaries and widths.

[Racing Line Root-Cause Audit (#104)](docs/experiments/racing-line-root-cause.md)
separates continuous-path geometry, actual tracking and force/yaw constraints on
frozen progressive-b. The existing imposed-offset controller does not execute
outside-inside-outside as intended. Isolated path-frame/preview diagnostics retain
failed outcomes; full feasibility and the tracking gate remain unresolved, with
no tyre/engine/default change or wheelspin ablation.

[Inverse Feasibility & Matched-Apex Validation (#105)](docs/experiments/inverse-feasibility-matched-apex.md)
uses the original force/wheel equations to distinguish scalar grip from coupled
feasibility. Exact C2 OIO exposes yaw-rate jumps and unavailable drive torque at
18 m/s. Matched-apex wide entries add distance without improving worst curvature;
slower legal native replays pass tracking, while constant-speed constructive
feasibility and strategy times remain unresolved. Production defaults stay unchanged.

[Speed, Grip & Racing Line Crossover (#106)](docs/experiments/speed-grip-line-crossover.md)
freezes full C3 XY paths and compares physical engine envelopes and Grip with
unchanged tyre/wheel/yaw laws. Only the slower validation pair qualifies; INNER
wins by 0.418 s/lap. Full-drive apparent wins fail safety/tracking, so crossover
remains inconclusive. Initialized 160/200 km/h tests naturally run wide with
convergent native forces. Three recorded cases are available in the replay viewer.

[Full Throttle Slide & Grip Audit (#107)](docs/experiments/full-throttle-slide-grip-audit.md)
reuses #106 native records without changing physics. Slip dissipates real work;
full-drive INNER supports a safe periodic drifting orbit, while authored-path
tracking still fails. Surface calibration and the causal share of controller
error remain unresolved. Three initial-speed repeat/perturbation runs are added.

[Full Contact Yaw Feed-Forward (#109)](docs/experiments/full-contact-yaw-feedforward.md)
isolates the omitted drive-force contribution to yaw with the #108 inverse fixed.
Native peaks and inner clearances worsen despite slightly lower RMS: **FAIL**,
default OFF. The small controller correction series ends here; the next step is
a physically feasible trajectory and speed/slip profile.

[Coupled Slip Inverse (#108)](docs/experiments/coupled-slip-inverse.md) tests a
default-OFF controller inverse using the original finite-step front/rear contact.
Two full-drive native runs improve RMS slightly but worsen peak error and inner
clearance. The variant fails acceptance and costs 5.3–7.9 times a force call;
it remains a research control, with unchanged tyre/wheel/yaw laws and defaults.

Current physical consequence precedence (#56C2): [physical contact consequences and one-step recovery](docs/calibration/physical-contact-consequences.md)
adds default-OFF `EnablePhysicalContactConsequences`, requiring #56B responses. When enabled,
reviewed #56C1 analysis supplies deterministic final velocity/status and next-step recovery;
legacy contact occurrence and consequence routines do not run. #55 remains the sole detector.
The segment is not reintegrated after impact. Historical contact descriptions below apply with this flag OFF.

Current racing-response precedence (#56B): [bounded pre-contact contested-space responses](docs/calibration/contested-space-racing-response.md)
Current outward-target precedence: [geometry-based minimal outward yielding](docs/calibration/minimal-outward-yield.md)
adds an opt-in heat-scoped coordinator after independent #54 planning. Joint alternatives
execute production motion and are verified by #55; legacy consequences are authorized once
for an unresolved eligible episode. Feature OFF retains the merged #56A behavior. Strength,
MassKg, canonical PairRiding, impact severity and body-contact response remain outside #56B.
Earlier statements that traffic behavior is future work describe the historical feature-OFF stage.

Current occupancy precedence (#55): [physical occupied-space and independent bike attitude](docs/calibration/physical-occupancy-contested-space.md)
adds explicit observation-only mechanical capsules and common-time conflict diagnostics.
Older statements that motorcycle dimensions/contested space are absent describe their historical stage.
Legacy contact formulas and #54 solo projection remain unchanged. The opt-in #56B
coordinator now supplies pre-contact responses and episode-owned legacy eligibility.
Straight, corner and start/finish poses share one metric model-space
track frame across closed laps. Width coordinate reinterpretations remain zero-time, unswept
state transitions; genuine unrelated/nonclosing frame inputs retain explicit diagnostics.

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

Reference acceleration po #36 wynosi TurnExit `1.20–2.80`, Straight `1.60–3.20`
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
preparation i nie wolno go wyłączać. Od #38 Straight i standing start targetują
ten sam continuous-corner envelope przy `CornerProgress = 0`; późniejsza
korekcja zużywa dystans do apexu z capability `2.00–3.20 m/s²`.

Pure zero-drive helper daje `-(40 + 0.20*v²)/142`, ale NIE jest finalnym
engine-braking modelem ani zamiennikiem corner preparation. Nie dodano explicit
throttle input, wheelspin, traction-force cap ani splitu engine/traction force.
W #36 zestrojono wyłącznie zakresy reference acceleration Straight/TurnExit i oba
końce fade. Masa, resistance, reference speed, integration step, gearing/surface
mapping oraz cały corner model pozostają **PROVISIONAL / NOT REAL-WORLD CALIBRATED**.
Stary analityczny `CalculateStraightSpeedProfile` to wyłącznie non-production
compatibility utility z jawnie podanym ogólnym ograniczeniem; nie wylicza Vmax,
nie jest wywoływany przez SimulationEngine i ma null equilibrium.

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
kompatybilności.

#31 wprowadza breaking diagnostic-schema change przed ustaleniem real-world dataset:
`FullDriveEquilibriumSpeedMetersPerSecond` zastępuje
`AttainableTopSpeedMetersPerSecond` w RiderStepDiagnostics, CalibrationStepSample
i CSV. Nie ma dwóch pól Vmax. Dla launch/Straight/continuous-corner drive
pochodzi z użytego profilu; dla innych segmentów jest null.
CSV dodaje `TurnExitDecelerationDistanceMeters` po TurnExitCruiseDistanceMeters;
entry acceleration TurnExit jest signed. Nowy schema zachowuje stable order,
invariant culture, decimal dot, `\n` i null = empty.

Pierwszy bounded longitudinal fitting wykonano w #36 bez zmiany reaction,
TimeTo70/SpeedAt2s targets, corner envelope ani fizyki pól startowych.
Późniejsze refinements: F_engine vs F_traction, wheelspin/slip, TractionBias,
real sprockets, RPM, torque/power curve, throttle, engine braking i oddzielna
physical gate A/B/C/D geometry. #31 ich nie implementuje.

## Standing start / launch foundation — PROVISIONAL

`Track.CreateExample()` zachowuje dotychczasowe 8 segmentów i bootstrap.
Nowy `Track.CreateStandingStartExample()` (także w Sandbox) umieszcza start/metę
na canonical lap boundary między segmentami 8 i 0: `Straight 35 m (start) → TurnEntry → TurnMiddle →
TurnExit → Straight 60 m → TurnEntry → TurnMiddle → TurnExit → Straight 35 m
(finish)`. Home straight to finish-half + start-half po dwóch stronach granicy
okrążenia: 35 + 35 = 70 m. Pierwszy łuk zaczyna się 35 m od startu.
Długość wynika wyłącznie z fizycznych segmentów. `LateralPosition` jest
bezwymiarową współrzędną `0..4`; dla standing example promień wynosi
`24 + 3 * lateral` m, więc pełne okrążenie ma
`130 + 2 * PI * (24 + 3 * lateral)` m. Wewnętrzna linia pomiarowa nadal ma
około 280.796 m, a zewnętrzna trajektoria referencyjna około 356.195 m. Nie ma
dodatkowego wirtualnego dystansu launch.

`TrackSegment` ma opcjonalny `float? StraightLengthMetersOverride` (finite, >0,
tylko Straight) i `bool IsStandingStartSegment=false` (tylko Straight).
Immutable copy zachowuje oba pola. Track dopuszcza najwyżej jeden oznaczony
segment, wyłącznie pod index 0, z końcowym Straight. Launch działa tylko w
advanced physics, lap 0 / segment 0, na oznaczonym segmencie, z dokładnego
canonical/physical początku, `NotStarted`, speed <= 0 i dodatnim pozostałym
dystansem. Wtedy entry/physics speed jest dokładnie 0; kolejne okrążenia używają
zwykłego Straight. `CreateExample`, legacy i #24 TurnEntry są niezmienione;
lookahead nadal nie przekracza jednego segmentu.

Reaction: `0.28 + (0.20 - 0.28) * StartNorm` s: Start 0/50/100 → 0.28/0.24/0.20 s.
Reference acceleration: `(9.0 + (11.0 - 9.0) * StartNorm) *
(1.10 + (0.90 - 1.10) * Gearing) * (0.75 + 0.25 * EffectiveGrip)` m/s².
Reference force: `142 * referenceAcceleration + resistance(0)` N.
Launch współdzieli `40 + 0.20*v²` resistance, one-gear envelope, exact 1 m
distance steps i signed midpoint bez artificial ceiling. Surface jest próbkowana raz przy wejściowym `LateralPosition`.
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

Jawne `StartingGate A/B/C/D` dzielą pełną fizyczną szerokość prostej na cztery
równe pola. `StartingGrid.Create` przyjmuje przypisania zawodnik → pole, ustawia
neutralne środki i mapuje je na racing `LateralPosition`; nie dodaje gate bonusu
ani gate lock. Stare ungated fixtures zachowują compatibility `Lane/LateralPosition`.
Aktualna Motoarena używa jawnego baseline 35/27, nie zmierzonego offsetu.
Szczegóły: [physical starting gates](docs/calibration/physical-starting-gates.md).
#30 porządkuje strukturę standing startu, nie kalibruje finalnego gate advantage;
nie ma hardcoded bonusu pola A.

Wszystkie launch constants są **PROVISIONAL / NOT REAL-WORLD CALIBRATED**.
Brak clutch, RPM, torque/power, real sprockets, wheelspin, slip ratio, traction
cap, false starts, reaction RNG i gate-specific reaction bonus. #31 dostarcza
signed force i natural equilibrium; następny etap to real-world calibration.

---

## Status / zakres (przykład)
- [ ] Symulacja toru: segmenty + 5 linii
- [ ] Ograniczenie łuku: minimalna linia dla prędkości (wynoszenie/hamowanie/błąd)
- [ ] Proste: pozycjonowanie + wyprzedzanie tylko z przewagi po wyjściu z łuku
- [ ] Zawodnicy: statystyki + styl + morale
- [ ] Setup: sugestie menedżera vs decyzje zawodnika
- [ ] Rozwój: wiek + kontuzje + cechy

## Real-world calibration dataset and evaluator (#32)

The repository contains a versioned, offline PGEE telemetry snapshot, a pure C# distribution/evaluation layer, production-path RiderSkills sweeps, and a deterministic [current-model baseline](docs/calibration/current-model-baseline.md). See [calibration.md](docs/calibration.md) for rules and reproduction.

Telemetry defines distributions, not hard caps. Age is not a physics multiplier, Skill 50 is not an “average PGEE rider,” and source `speed_2s`/`curve_speed` values are gate rankings rather than numeric speeds. Absolute race times remain track-geometry context. #32 changes no physics constants; #33 adds physical-width geometry and measures its impact without tuning.

## Physical track width and lateral geometry (#33)

`Lane` and continuous `LateralPosition` retain the inclusive `0..4` domain, but
they are normalized reference coordinates rather than metres. The normalized
fraction is `LateralPosition / 4`; physical offset from the inner reference line
is that fraction times the usable local span. `TrackGeometry` stores independent
`StraightWidthMeters` and `TurnWidthMeters`. Usable span subtracts the 1 m FIM
inner measurement/reference offset and a separate provisional 1 m game-geometry
outer margin. `InnerRadiusMeters` is the radius of that inner reference
trajectory, not the kerb radius.

`Track.CreateExample()` uses 6 m / 6 m synthetic compatibility widths, retaining
1 m between reference positions; it is not a regulatory-size track.
`CreateStandingStartExample()` uses a 10 m straight and 14 m turns as an
FIM-minimum-width example, not as universal dimensions for real tracks. It has
2 m straight and 3 m turn reference spacing, with turn radii
24/27/30/33/36 m.

Turn radius/path length, occupancy/contact separation, arrival tolerance and
physical displacement use metres derived from the current segment width. The
surface remains five normalized bands with unchanged grip/wear formulas.
Segment boundaries reinterpret the same normalized position against local width;
there is no width-transition spline, artificial movement event or extra distance.
Executed lateral movement now contributes cartesian/polar path distance (see below), and the
A/B/C/D physical bounds and neutral centers are now explicit; within-gate
positioning and motorcycle footprint remain future work. The
[physical-width impact report](docs/calibration/physical-width-impact.md) is
observation-only: no speed/performance constant was changed and no calibration
was performed.

## Continuous corner-speed correction (#34, historical behavior superseded by #38)

Advanced `SegmentPhysics` now classifies a corner constraint and supplies a
nullable correction target without instantly assigning recoverable speed to that
target. `LongitudinalDynamics` spends actual distance and time on deterministic
correction with the unchanged `2.00–3.20 m/s²` SlideControl-and-surface
capability. In advanced physics `Brake` therefore means controlled speed scrub,
not use of a mechanical brake; legacy physics retains its exact instantaneous
clamp and retention behavior.

At #34, TurnEntry kept its coarse first-half scrub, TurnMiddle corrected and
carried, and TurnExit corrected before its segment-gated drive. #38 replaces
those ADVANCED production phase bridges with one continuous logical-corner
traversal. Insufficient distance still leaves residual overspeed,
while Crash remains a terminal event abstraction. Random incident probability,
RNG channels and the immediate `0.88` consequence are unchanged. The
[continuous-correction impact report](docs/calibration/continuous-corner-correction-impact.md)
is observation-only; no physics calibration was performed.

## Longitudinal speed-envelope calibration (#36)

The first bounded empirical pass keeps the existing signed-force model and
calibrates only Straight (`1.60–3.20 m/s²`), TurnExit (`1.20–2.80 m/s²`) and the
two linear fade endpoints (`0.0350/0.0100 1/(m/s)`). Reference speed, mass,
resistance, integration, gearing/surface mappings, start constants and all
corner physics are unchanged. The deterministic
[impact report](docs/calibration/longitudinal-speed-envelope-impact.md) records
the candidate menu, force/power curves, equilibrium signs, gearing crossover,
finite-distance response, TurnExit recovery, start/corner regressions and full
production heats. Residual whole-heat speed remains visible for the later
corner-envelope phase rather than being hidden by a cap or overfit.

## Continuous corner phase foundation (#37)

Track.CornerTopology now identifies each maximal contiguous run of turn
segments without joining across the lap boundary. For advanced physics,
CornerPhaseContext maps local segment progress to canonical CornerProgress
in [0,1] by accumulated physical arc distance and exposes the total and
remaining corner length.

The coordinate domains remain distinct: LateralPosition is continuous
cross-track position 0..4, CornerProgress is longitudinal progress through
one logical corner, TurnEntry/TurnMiddle/TurnExit are compatibility and
reporting labels, and Lane is the discrete resolved reference. #37 deliberately
left TurnEntry scrub and TurnExit drive as temporary bridges; #38 removes their
ADVANCED production use. No speed/performance constant changed in #37; see the deterministic
[foundation impact report](docs/calibration/continuous-corner-foundation-impact.md).

## Continuous corner envelope calibration (#38)

ADVANCED production now traverses one `ContinuousCornerEnvelope` over the
logical corner. Before the provisional apex at `0.50`, recoverable speed follows
`sqrt(v_apex² + 2*a_correction*distanceToApex)`. After the apex, availability
ramps with smoothstep to full drive at `5/6` and scales the existing signed
Turn/full-drive contribution. Every metre belongs to correction, carry or drive
exactly once; insufficient distance leaves residual overspeed and no speed cap
or teleport is applied.

The only bounded calibration is the ADVANCED settled/apex reference from
`16 → 19 m/s`; all #36 longitudinal, launch, correction and outcome constants
remain frozen, and Legacy retains its 16 m/s reference. The deterministic
[impact report](docs/calibration/continuous-corner-envelope-impact.md) contains
the A0/B17/B18/B19 screen, full-heat and geometry sweeps, telemetry context and
known limitations.

## Executed segment trajectory

Advanced moving paths now integrate physical distance, elapsed time, lateral position, local radius and surface together. `ExecutedSegmentPath` is the shared production/telemetry/wear result; fixed lines retain their exact prior traversal. No coefficients or AI/contact rules changed. See the [method and compatibility evidence](docs/calibration/executed-trajectory-method.md) and the [21 controlled Motoarena trajectories](docs/calibration/single-rider-executed-trajectory.md). Historical #47–#51 reports remain immutable snapshots.

## Production-backed trajectory decisions

`AdaptiveDecisionModel` now replans bounded Entry/Apex/Exit intents from the exact current snapshot. Its physical time is measured by isolated solo production replay through the remaining corner and whole following logical straight. Five reference anchors produce at most 35/13/5 remaining candidates; style, behavioral reluctance, surface reading and provisional current-target occupancy remain separate costs. See [architecture, parity tests and current controls](docs/calibration/trajectory-intent-evaluation.md). The opt-in #56B coordinator adds traffic feasibility and common-time responses after solo planning. Full race materialization and Lean projection share one physical core; exact behavior/wear parity protects a typed prefix state graph. The subsequent shared scalar envelope and isolated root-branch evaluation reduce four-rider runtime to 382.144 ms and cumulative allocations to 180.206 MB on the documented desktop protocol. The original 4× goal is a stretch observation; [the performance and CI follow-up](docs/calibration/trajectory-performance-ci.md) records measurements, exact cross-platform parity and complete shard responsibility.
