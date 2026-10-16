# Mid-Segment Control Activation — wynik PoC

Baza: main `561c06b6e486b4cbb61db66a3a2ad1fab33a0227`, 2026-10-17.
Wniosek: **B — CZĘŚCIOWO**. Działa napęd i cel boczny w istniejącym
`ExecutedPathTraversal`. Osobne integratory stałej linii pozostają poza kontraktem.
Nie rozszerzano ich ani nie podmieniano wykonanego prefiksu.

| Element | Przed zmianą | Po zmianie |
| --- | --- | --- |
| Moment aktualizacji sterowania | Początek segmentu | Pierwszy istniejący zaakceptowany punkt nie wcześniejszy od deadline |
| Zmiana gazu w trakcie segmentu | Nie | Tak, w istniejącym wykonawcy sprzężonym |
| Zmiana celu bocznego w trakcie segmentu | Nie | Tak, z tym samym ograniczeniem zakresu |
| Zachowanie wykonanej historii | — | Identyczne węzły i kroki do punktu aktywacji włącznie |
| Zgodność prognozy | — | Identyczny motion i zapis aktywacji przez wspólny rdzeń |
| Dodatkowy koszt | 0 | Bez komendy +16 B/przejazd; bez dodatkowych rozwiązań numerycznych |

1. **Wykonalność:** lokalne zmienne napędu/hold/celu można przełączyć po zaakceptowaniu
   kroku, przed rozwiązaniem następnego. Nie dodano nowego całkowania ani siatki czasu.
   Wzory w `Solve`, `Evaluate`, `SegmentPhysics` i `LongitudinalDynamics` są niezmienione.

2. **Zakres:** wewnętrzny `ScheduledControlChange` na `RiderDecision`, jeden na przejazd.
   `ResolveProduction` wykonuje uprzednio znaną komendę. Normalny `HeatSimulator` jej
   nie tworzy. Publiczny `Resolve` odrzuca aktywną komendę przy wyborze #56B, żeby
   wyszukiwanie segmentowe nie zgubiło jej ani nie potraktowało jako natychmiastowej.
   Nie dodano opcji gry, nowego planera, obserwacji rywali ani solvera kontaktów.

3. **Historia:** integrator dopisuje kroki jeden raz. Już zaakceptowane węzły, czas,
   dystans, prędkość i próbki nawierzchni są identyczne jak bez komendy. Węzeł aktywacji
   zachowuje obserwację starego sterowania; nowe sterowanie dotyczy jego wychodzącego kroku.
   Komenda aktywna od początku działa przed utworzeniem początkowego węzła.
   Resolve nie mutuje toru. Dotychczasowy Commit nalicza jeden budżet zużycia na segment;
   wear rich/lean jest identyczny. Wcześniejsze segmenty i zdarzenia pozostają nietknięte.

4. **Wykonanie fizyczne:** `SegmentResolution.Speed/Outcome/Lane` pozostają klasyfikacją
   wejściową, nie gotowym wynikiem całego napędu. Każdy kolejny krok używa aktualnej
   prędkości i nowej wartości sterowania we wspólnych prymitywach. Komenda nie ustawia
   prędkości ani pozycji. Brake i obowiązkowa korekcja działają nadal; wymuszony RunWide
   ignoruje dobrowolny hold/cel, Crash pozostaje terminalny. Nowy napęd nadal mnoży się
   przez dostępność z istniejącego contact recovery. Potwierdzają to osobne testy.

5. **Gaz:** na prostej pełny napęd do żądanych 0,20 s; aktywacja w 0,246731639 s,
   na kroku 5. Prędkość w punkcie aktywacji jest identyczna jak bez komendy. Następny
   krok korzysta z istniejącego signed midpoint przy zerowym napędzie, bez skoku prędkości.
   Koniec: 18,225859 zamiast 24,845280 m/s; czas 3,089580 zamiast 2,658341 s.

6. **Cel na łuku:** początkowy istniejący hold, potem fizyczny cel z hold=false.
   Aktywacja w 0,238095224 s. Motocykl stopniowo poszerza tor, zachowując lokalny budżet
   ruchu i ograniczenia łuku. Test z celem 12 m potwierdza, że cel nie musi być osiągnięty.
   Nie ma nowego grip ani zmiany umiejętności. Maksymalny zmierzony lag względem 0,20 s
   wynosi **46,732 ms** na prostej i **38,095 ms** na łuku — do kolejnego istniejącego
   kroku około 1 m. To granica tej siatki, nie obietnica dowolnie dokładnej reakcji.

7. **Prognoza i czas:** `TrajectoryEvaluator.ForScheduledControl` przekazuje tę samą
   komendę tylko do bieżącego prefiksu i wykonuje ten sam `ResolveProduction`.
   Test porównuje cały motion, koniec segmentu i aktywację dokładnie, bez luzowania
   tolerancji; testuje też origin=100 s i następny segment po jednym Commit.
   Deadline to absolutny czas fizyczny biegu (double); lokalny czas zaakceptowanego
   węzła ma dotychczasową precyzję float. Aktywacja porównuje origin + ten czas z deadline.
   Zerowy termin działa od początku, dokładny węzeł na tym węźle, termin pomiędzy — na
   następnym. Na końcowym węźle zapisuje aktywację bez dalszego kroku. Komenda po końcu
   wygasa; nie przechodzi do kolejnego segmentu. Wyłączona jest ignorowana.
   Różne origin/węzły zawodników nie są synchronizacją. Przyszła integracja wielu
   zawodników wymaga wspólnego, przyczynowego czasu obserwacji i wydania komend.

8. **Za późno i #84:** późniejsza komenda nie usuwa próbki wcześniejszego kontaktu
   w scenariuszu C przed 0,20 s. Zamiana kolejności zawodników daje ten sam wynik.
   Dla A porównano trzy warianty znanej komendy YieldOutward z #56B:

   | Wariant A | Aktywacja | Pary z kontaktem | #55: dokładne separacje | Czas końcowy R2 | Prędkość R2 |
   | --- | ---: | ---: | ---: | ---: | ---: |
   | Bez reakcji | — | 1; świadek do 0,532068 s | 7048 | 1,223450 s | 20,810880 m/s |
   | Od początku, istniejące #56B | początek | 0 | 137 | 1,254588 s | 21,224304 m/s |
   | Żądane 0,20 s | 0,238095 s | 0 | 337 | 1,252795 s | 21,224304 m/s |

   R2 najpierw zacieśnia, potem fizycznie wraca do celu 4,8 m. Geometria obu wariantów
   reakcji jest rozstrzygnięta: zero unresolved i frame gaps. Audyt obejmuje wspólny
   pokryty czas motion do 1,196093 s; nie dowodzi bezkontaktowego całego biegu.
   #55 działa osobno po integracji, nigdy przy każdym kroku. Przejazd z kontaktem jest
   niezatwierdzonym porównaniem; nie akceptowano jego dalszego ruchu ani skutków.
   [Dane trzech wariantów](mid-segment-control-pair-a.csv).

9. **Koszt Release:** Windows/.NET 8, DOTNET_TieredCompilation=0, 4 wywołania rozgrzewki,
   mediana 7 próbek po 4 wywołania. Przygotowanie snapshotu/intencji/komendy i audyt
   liczników poza timerem; alokacja samej komendy nie jest w tych liczbach. Stopwatch
   ma taki sam koszt po obu stronach. Baseline to zachowany przed zmianą main CoreSim.dll
   (SHA256 `C9BD8B661AA53E34BBC6BA4F32F494CF4C4FA60CE334219F1C93FBE22CB9DB00`),
   z identycznym harness i danymi. Wszystkie warianty mają jeden production rider resolve;
   dodatkowych pełnych przebiegów, #54, wyszukiwań #56B i #55 wewnątrz integracji: **0**.

   | Bez komendy | Main → nowe, ms | Alokacje main → nowe | Kroki / coupled evaluations, obie wersje |
   | --- | ---: | ---: | ---: |
   | Prosta sprzężona | 0,041975 → 0,044575 | 27738 → 27754 B | 60 / 120 |
   | Łuk z hold | 0,047375 → 0,047050 | 27330 → 27346 B | 28 / 43 |
   | Łuk ze zmianą linii | 0,186875 → 0,193450 | 53762 → 53778 B | 27 / 360 |
   | Stała linia, prosta | 0,020300 → 0,016550 | 16018 → 16034 B | 60 / 0 |
   | Stała linia, łuk | 0,021775 → 0,017350 | 17058 → 17074 B | 28 / 0 |

   Nie deklarujemy zerowego kosztu ani przyspieszenia: mediany wzrosły na dwóch
   kontrolach o 2,6/6,6 µs (+6,2/+3,5%); w pozostałych spadły. Ten krótki pomiar
   nie rozdziela drobnego narzutu od szumu. Jawny koszt to nowe pola obserwacji (+16 B)
   i warunek przy istniejącym kroku, bez nowego rozwiązania numerycznego.
   Aktywny Lift: 0,042050 ms, 27802 B, nadal 60 kroków/120 ocen; zapis aktywacji +48 B.
   Poszerzenie na łuku: 0,132050 ms, 37794 B, 28 kroków/137 ocen zamiast 43 przy hold.
   Te +94 oceny to istniejące sprzężone rozwiązanie zmienionej trajektorii; samo
   przełączenie nie wywołuje Solve. Subdivisions w tych kontrolach: 0.
   [Wszystkie koszty, wyniki i fingerprinty](mid-segment-control-measurements.csv).

10. **Rozmiar i ograniczenie:** w src/CoreSim **99 dodanych / 12 usuniętych linii**,
    w tym 30 linii typu komendy i obserwacji aktywacji. Nie dodano infrastruktury kursorów.
    Aktywny harmonogram wymaga już sprzężonej ścieżki (np. istniejący HoldLateralPosition
    albo ruch boczny). Zwykła stała linia używa odrębnych prymitywów: wymuszenie ścieżki
    sprzężonej od początku zmienia jej wcześniejsze węzły. Pomiar tych samych stanów:
    różnica czasu do 0,238 µs, drogi do 3,815 µm na prostej / 7,629 µm na łuku.
    Wielkość tych różnic nie wymaga nowego solvera; nie przedstawiamy jednak podmiany
    prefiksu jako jego zachowania. Obsługa tej ścieżki wymaga jawnej decyzji o wykonawcy
    lub punktów aktywacji w istniejących pętlach stałej linii. Nie dodawano ich w tym PoC.

Walidacja: 20 nowych przypadków + 5 istniejących kontroli ruchu, nawierzchni i wear,
**25/25 Release**. Build z warnaserror: 0 ostrzeżeń/błędów. Lokalny restore bez pobierania
metadanych NuGetAudit (źródło sieciowe niedostępne); standardowe Windows CI bez zmiany
workflow. Historyczne testy/dane/kalibracje, podstawowe wzory, #55/#56C i domyślne A/B/C
bez modyfikacji. #81–#84 pozostają Draft. Nie wdrażano reaktywnego wykrywania ani całej czwórki.

Odtworzenie: build Release; filtr `MidSegmentControlTests`; uruchom
`tools/mid-segment-control-audit/bin/Release/net8.0/CoreSim.Tests.dll output.json`
przez dotnet z DOTNET_TieredCompilation=0. Dla porównania ze starą biblioteką zbuduj
projekt narzędzia z `-p:BaselineCoreSimPath=<absolutna ścieżka do DLL main>`;
ten wariant kompiluje wyłącznie kod harness dostępny przed zmianą.
