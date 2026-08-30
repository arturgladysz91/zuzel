<!-- Słownik pojęć używanych w CoreSim, żeby nazwy w kodzie nie rozjechały się z modelem. -->

# Glossary
- Segment: element decyzyjny toru (wejście łuku / środek / wyjście / prosta).
- TargetLane: docelowa linia żądana przez decyzję zawodnika.
- PlannedLane: w zaawansowanej fizyce najbliższa niewykonana linia odniesienia od rzeczywistego `LateralPosition` w stronę `TargetLane`; wcześniejszy dyskretny wynik `Lane` nie pozwala jej pominąć.
- Lane: jedna z 5 lokalnych linii w segmencie (0..4), rozstrzygnięta przez fizykę i nadal używana do ograniczeń, dystansu oraz zużycia.
- LateralPosition: rzeczywista ciągła pozycja boczna na końcu kroku w jednostkach linii `0..4`; fizyczne przesunięcie to zmiana tej wartości pomnożona przez `LaneSpacingMeters`.
- Świadomy wybór szerokiej linii: zaplanowana trajektoria służąca nawierzchni, atakowi albo lepszemu wyjściu; zmienia `PlannedLane`, może zakończyć się `Ok` i nie jest zdarzeniem `RunWide`.
- Wyniesienie (`RunWide`): wymuszone przejście z `PlannedLane` na szerszą końcową `Lane` jako konsekwencja zbyt dużej prędkości, błędu albo kontaktu. Wydłuża drogę; gdy wywołuje je przekroczenie ograniczenia, nie zachowuje całej nadwyżki prędkości, a lepsze `SlideControl` ogranicza stratę, lecz jej nie usuwa.
- Profil prędkości prostej: w advanced physics deterministyczne przyspieszenie oraz, gdy bezpośrednio następuje `TurnEntry`, kontrolowane wytracenie prędkości przed łukiem; nie jest arbitralnym bonusem linii ani klasycznym mechanicznym hamowaniem.
