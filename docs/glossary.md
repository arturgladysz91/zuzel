<!-- Słownik pojęć używanych w CoreSim, żeby nazwy w kodzie nie rozjechały się z modelem. -->

# Glossary
- Segment: element decyzyjny toru (wejście łuku / środek / wyjście / prosta).
- TargetLane: docelowa linia żądana przez decyzję zawodnika.
- PlannedLane: w zaawansowanej fizyce najbliższa niewykonana linia odniesienia od rzeczywistego `LateralPosition` w stronę `TargetLane`; wcześniejszy dyskretny wynik `Lane` nie pozwala jej pominąć.
- Lane: jedna z 5 lokalnych linii w segmencie (0..4), rozstrzygnięta przez fizykę i nadal używana do ograniczeń, dystansu oraz zużycia.
- LateralPosition: rzeczywista ciągła pozycja boczna na końcu kroku w jednostkach linii `0..4`; fizyczne przesunięcie to zmiana tej wartości pomnożona przez `LaneSpacingMeters`.
- Świadomy wybór szerokiej linii: zaplanowana trajektoria służąca nawierzchni, atakowi albo lepszemu wyjściu; zmienia `PlannedLane`, może zakończyć się `Ok` i nie jest zdarzeniem `RunWide`.
- Wyniesienie (`RunWide`): wymuszone przejście z `PlannedLane` na szerszą końcową `Lane` jako konsekwencja zbyt dużej prędkości, błędu albo kontaktu. Wydłuża drogę; gdy wywołuje je przekroczenie ograniczenia, nie zachowuje całej nadwyżki prędkości, a lepsze `SlideControl` ogranicza stratę, lecz jej nie usuwa.
- Gearing: znormalizowana oś setupu `drive-oriented ↔ speed-oriented`; `0` oznacza większy corner-exit drive i niższą osiągalną prędkość, a `1` słabszy drive i wyższą osiągalną prędkość. Nie należy interpretować końców osi wyłącznie jako językowo niejednoznaczne „low/high gearing”.
- Osiągalna prędkość szczytowa: deterministyczny ceiling positive drive w advanced physics, zależny od `Speed` i `BikeSetup.Gearing`, ale nie od nawierzchni. Obecne zakresy są **PROVISIONAL / NOT REAL-WORLD CALIBRATED**, nie opisują finalnej prędkości prawdziwego motocykla i nie obcinają istniejącej prędkości równej lub większej od ceiling.
- Available TurnExit drive force: wewnętrzna siła pierwszego force-based foundation, skalibrowana tak, aby przy `16 m/s` i nominalnych `142 kg` zachować dotychczasowe przyspieszenie zależne od `Speed`, `Gearing` i wejściowej nawierzchni. Nie jest jeszcze pomiarem prawdziwej siły na kole.
- Longitudinal resistance: **PROVISIONAL / NOT REAL-WORLD CALIBRATED** zagregowany opór `40 N + 0.20 N/(m/s)² * v²` używany wyłącznie do dodatniej siły netto advanced `TurnExit`; nie jest utożsamiany z samym aerodynamic drag i nie generuje jeszcze naturalnego spowalniania.
- Settled safe speed: obecne `SegmentPhysics.MaxSafeTurnSpeed`, czyli prędkość ustabilizowanego motocykla w geometrii łuku; nie jest limitem pierwszej chwili advanced `TurnEntry`.
- TurnEntry approach speed: prędkość początku `TurnEntry`, która może przekraczać settled safe speed; lookahead targetuje maksymalną wartość odzyskiwalną przez provisional scrub phase, a nie globalny hard limit.
- TurnEntry scrub phase: pierwsze **PROVISIONAL / NOT REAL-WORLD CALIBRATED** `50%` faktycznie pozostałego dystansu advanced `TurnEntry`, reprezentujące setting, roll-off i slide entry; po niej residual overspeed rozstrzyga `SegmentPhysics`.
- Profil prędkości prostej: w advanced physics deterministyczny, distance-limited przebieg z fazami `accelerate`, `cruise` i — gdy wymaga tego bezpośredni `TurnEntry` — `decelerate`; targetem łuku jest maximum recoverable TurnEntry approach speed, positive drive nie przekracza osiągalnej prędkości szczytowej, a profil nie jest arbitralnym bonusem linii ani klasycznym mechanicznym hamowaniem.
