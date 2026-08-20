<!-- Słownik pojęć używanych w CoreSim, żeby nazwy w kodzie nie rozjechały się z modelem. -->

# Glossary
- Segment: element decyzyjny toru (wejście łuku / środek / wyjście / prosta).
- Lane: jedna z 5 lokalnych linii w segmencie (0..4).
- Świadomy wybór szerokiej linii: zaplanowana trajektoria służąca nawierzchni, atakowi albo lepszemu wyjściu; zmienia `PlannedLane`, może zakończyć się `Ok` i nie jest zdarzeniem `RunWide`.
- Wyniesienie (`RunWide`): wymuszone przejście z `PlannedLane` na szerszą końcową `Lane` jako konsekwencja zbyt dużej prędkości, błędu albo kontaktu. Wydłuża drogę; gdy wywołuje je przekroczenie ograniczenia, nie zachowuje całej nadwyżki prędkości, a lepsze `SlideControl` ogranicza stratę, lecz jej nie usuwa.
- Przewaga na prostej: efekt lepszego wyjścia z łuku, prosta nie generuje przewagi.
