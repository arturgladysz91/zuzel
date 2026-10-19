# Etap A — diagnoza przed zmianą regulatora

Baza: `cb980e0e59b5ad6a1c3b28ac35db778d1a1fae94`. Zapisano po `baseline-seal.json`, przed dodaniem wariantów. W tym etapie zmieniono wyłącznie obserwację podziałów solvera i opcjonalny punkt mety Rolling Pack; `TrafficThrottle.cs` pozostawał identyczny z #93.

Zamrożono 6 konfiguracji pełnych przejazdów i 7 krótkich przypadków. Najpierw wykonano rolling-straight: cztery rzeczywiste motocykle z normalną AI, bez nakładania kapsuł, cztery pełne obiegi od początkowego postępu każdego zawodnika. To nie jest start od taśmy. Następnie pozostałe 5 przejazdów i 7 krótkich przypadków. Wszystkie ukończone, bez kontaktów. Żadnych zmian profili po wynikach.

- A1/0: zero podążania wspólnym aktualnym korytarzem, zero częściowego gazu. Początkowe osobne linie mają 2.2 m fizycznego prześwitu.
- A2/1: R3 podąża przez 26.611 s, p10/mediana/p90 prześwitu wzdłużnego 6.203/7.549/9.551 m. Brak częściowego gazu nie oznacza braku regulatora: daleki rywal nie wymaga ograniczenia napędu. A1 i A2 zachowują dokładne hashe i komendy #93.
- Nowe close-rotation-0/1: 0.480/0 s podążania, zero częściowego gazu. Same podobne osiągi nie przezwyciężają rozdzielenia torów jazdy przy starcie z bramek.
- rolling-straight: R1 podąża 49.560 s, częściowy gaz 12.300 s, zero 1.200 s; mediana prześwitu 3.778 m, p10/p90 3.699/4.146 m. Późniejszy AttackInside aktywowany w 47.236 s; konieczna jest dalsza analiza ruchu i wyniku, sama intencja nie dowodzi minięcia. Jeden potwierdzony manewr minięcia w całym przejeździe.
- rolling-bend: 31.970 s podążania, zero częściowego gazu. R1 mediana 7.166 m; 0 kontaktów.
- equal-close: startowy mechaniczny prześwit około 1.1 m, jednakowe 18 m/s. Bazowy regulator przez 2.100 s używa częściowego gazu i otwiera odstęp: mediana 2.110 m, p90 3.578 m. Uzasadnia ograniczone porównanie preferencji, ale nie uzasadnia zmiany progu Emergency.

Porównanie zostaje ograniczone do trzech zamrożonych wzorów i tych samych siedmiu krótkich sytuacji. Bez strojenia po wynikach. Kryteria wyboru: brak nowych kontaktów/zbliżeń mechanicznych <0.3 m, mniejszy rzeczywisty prześwit przy zachowaniu reakcji i możliwości ataku, brak wyraźnego wzrostu zmian gazu lub kosztu. Na pełnych konfiguracjach zostanie uruchomiony najwyżej jeden nowy kandydat. Wyniki negatywne pozostają dowodem.
