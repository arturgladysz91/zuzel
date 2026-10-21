# Obecna symulacja Box2D — wizualna diagnostyka

Viewer rozszerzony na bazie Draft #109, HEAD `a5164d9daddb091eb86a2809792703941069fd56`.
Wyłącznie zapisane dane: bez nowych symulacji, optymalizacji lub zmian fizyki.

## Windows: uruchomienie

W PowerShell, z głównego katalogu repozytorium na gałęzi `tools/current-box2d-replay`:

```powershell
py -m http.server 8000 --bind 127.0.0.1
```

Jeśli launcher `py` nie jest dostępny: `python -m http.server 8000 --bind 127.0.0.1`.
Otwórz **http://127.0.0.1:8000/tools/replay-viewer/?case=current-baseline-inner**.
Do oglądania wystarczą Python oraz aktualny Chrome/Edge; .NET i Node nie są potrzebne.
Pozostaw terminal serwera otwarty. Moduły/fetch wymagają HTTP, nie `file://`.

## Najnowsze przejazdy

| Wartość `case` | Zapis | Regulator | Granice |
| --- | --- | --- | --- |
| `current-baseline-inner` | #107 `full-throttle-slide-grip-audit/repeat18` — dokładny repeat baseline #106 | poprawki OFF | certyfikat PASS |
| `current-baseline-wide` | #106/#107 `speed-grip-line-crossover/baseline-wide` | poprawki OFF | brak pełnego certyfikatu między krokami |
| `current-full-inner` | #109 `full-contact-yaw-feedforward/full-inner` | coupled inverse + full contact yaw ON | certyfikat PASS |
| `current-full-wide` | #109 `full-contact-yaw-feedforward/full-wide` | coupled inverse + full contact yaw ON | brak pełnego certyfikatu między krokami |

Wszystkie cztery mają pełny gaz 100%, osiem okrążeń i nie przechodzą dokładnego
trackingu planu. Dodatnie minima footprintu w zapisanych stanach WIDE **nie są
zaobserwowanym crossing**; ujemny konserwatywny bound oznacza brak certyfikatu.
Komunikat o faktycznym wyjeździe pojawia się wyłącznie dla ujemnego zapisanego
prześwitu. Historyczny #95 z czterema zawodnikami pozostaje na liście, jawnie
oznaczony jako starsza fizyka sprzed niezależnego tylnego koła #100.

## Oglądanie łuku

- Domyślnie zbliżenie i śledzenie R1, najazd 3 m przed pierwszym łukiem oraz tempo **0,1×**. Naciśnij **Odtwórz**. **Łuk 1 / Łuk 2** zatrzymują i przenoszą do rzeczywistej próbki najazdu; **Zbliżenie** przywraca powiększenie motocykla.
- Złoty wektor pokazuje heading motocykla, niebieski rzeczywiste `v × 0,2 s`, różowy łuk kąt **β = heading − atan2(vy,vx)**. Nieruchomy motocykl nie ma określonego β. Mała mapa pokazuje oba brzegi i aktualną pozycję.
- Motocykl to eksportowane kapsuły C# `BikeFootprint`: korpus 2,10 × 0,30 m i kierownica 0,80 m, obrócone zapisanym heading. To rzeczywisty mechaniczny model 2D, bez domyślonej animacji koła lub skrętu kierownicy. Numery, wektory i pierścień wyboru są nakładkami.
- Turkusowy ślad pochodzi z zapisanych XY. Zielona przerywana C3 to niezmieniony plan #106; biała krawędź to kreda, złota banda. Plan jest odczytany z istniejącego `viewer-paths.json`, tor z zamrożonego `progressive-b-geometry.json`.
- Zachowano Play/Pause, suwak, poprzednią/następną próbkę, zoom, przeciąganie, **Cały tor** i wybór/śledzenie motocykla.

## Gęstość i wierność zapisu

Nowe cztery pozycje czytają oryginalne `*-tracking.csv.gz` przez przeglądarkowy
`DecompressionStream`: wszystkie native **Before** snapshoty, zwykle co 10 ms,
z zachowaniem krótszych podziałów. Nie są to próbki wygenerowane z rzadkiego CSV.
XY, heading, vx/vy, speed, reference, offset i target pochodzą z tych wierszy.
Stały Drive pochodzi z Initial/accepted CSV i jest sprawdzany; import odrzuca
zmienny gaz bez natywnego zapisu aktuatorów. Wyniki i czasy mety pozostają z JSON.

Domyślnie **Interpolacja OFF**: motocykl, wektory i β pochodzą z tego samego
ostatniego snapshotu. Po jej włączeniu wyłącznie rendering interpoluje XY,
heading i wektory; panel wyraźnie to opisuje. Prędkość/gaz w panelu nadal są
ostatnimi zapisanymi pomiarami. Interpolacja nie dodaje kroków fizyki, a po
ostatnim snapshotcie pozycja nie jest ekstrapolowana. Historyczne CSV zachowują
swój oryginalny odstęp około 0,1 s i dotychczasowe znaczniki sterowania.

Testy: `node --test tools/replay-viewer/replay.test.mjs` (19 przypadków).
Targeted C#: `dotnet test tests/CoreSim.Tests/CoreSim.Tests.csproj -c Release --filter FullyQualifiedName~ReplayGeometryTests`.

---

Poniżej zachowano historyczny opis wcześniejszych wersji viewera.

# Shared World Speedway — Diagnostic Replay Viewer 2D (#96)

## Progressive Bend Width #103

`?case=width-b-outer-tighten` opens the new width-only Egg Mild experiment.
The existing selector includes A/B/C × six unchanged #102 plans. A reads the
frozen #102 files; B/C each load their own geometry, full-footprint +0.5m limit,
and actual CSV poses/body headings. Rejected outer-max remains available.
The A/B/C checkbox compares the three outer boundaries in one metric frame;
the expandable comparison below the replay shows those boundaries and both
bend-width plots. C's 17/16.2m midpoint maxima are assumptions, not surveyed apex
widths. Comparison trails are independent second laps of inner, middle-to-outer
and outer-max, never one shared race. Details and exports:
[report](../../docs/experiments/progressive-bend-width.md).

## Dane #99

Lista przejazdów obejmuje dodatkowo wybrany plan, kontrolę wewnętrzną, szeroką
i odrzuconą kontrolę z `docs/experiments/physics-driven-racing-line/`.
To rzeczywiste stany trzech okrążeń Box2D; `selected` i `inner` celowo pokazują
tego samego zwycięzcę. Odrzucony target 0,5 m przekracza obrysem krawędź i nie
jest dopuszczony przez selektor, choć fizyka bez band pozwala mu ukończyć.
Raport i CSV obok replayów podają kąty, energię i pełną ocenę wykonalności.
UI i renderer pozostają te same; aktywacja dokładnie w czasie próbki (także t=0)
jest nakładana na aktuatory sprzed Activate bez zmiany zapisanej pozycji.

Lokalna diagnostyka dwóch zapisanych przejazdów #95. HTML, CSS, moduły JavaScript i Canvas 2D; bez pakietów npm, zewnętrznych zasobów i backendu aplikacji. Odtwarzacz wyłącznie odczytuje dane. Fizyka, AI, historyczne wyniki i fingerprinty pozostają bez zmian.

## Uruchomienie

Z **głównego katalogu repozytorium**, na gałęzi zawierającej ten PR:

```powershell
py -m http.server 8000 --bind 127.0.0.1
```

Otwórz <http://127.0.0.1:8000/tools/replay-viewer/> w aktualnym Chrome/Edge. `python -m http.server` jest równoważne, jeśli nie ma launchera `py`. Nie otwieraj `index.html` przez `file://`: moduły i `fetch` wymagają HTTP. Do odtwarzania nie potrzeba .NET, Node ani symulatora; `geometry.json` jest już w repozytorium.

## Sterowanie i diagnostyka

- Wybór **Solo · przed łukiem** / **Rolling Straight · 4 zawodników** resetuje czas do rzeczywistego stanu `Run.Initial`.
- Odtwórz/pauza, suwak, poprzednia/następna zapisana próbka oraz tempo 0,25× / 0,5× / 1× / 2×. Kroki używają unii czasów CSV zawodników; czas 0 pochodzi z JSON. Klawisze ←/→ i spacja działają, gdy fokus nie jest na kontrolce formularza.
- Kółko myszy lub +/− powiększa; przeciągnięcie przesuwa widok. **Cały tor** przywraca dopasowanie. **Śledź Rn** centruje kamerę na wybranym motocyklu; przeciągnięcie wyłącza śledzenie.
- Kliknięcie motocykla lub R1–R4 wybiera zawodnika. Kolory, numery i ślady pokazują zapisane trajektorie. Motocykle zachowują mechaniczną skalę także przy oddaleniu; śledzenie/powiększenie pozwala zobaczyć korpus i kierownicę.
- Panel podaje km/h z ostatniej próbki, Drive jako procent dostępnego napędu, rzeczywisty offset, aktywny target, status, intencję linii i okrążenie. Źródłem okrążeń są indywidualne `Measurement.CrossingSeconds`, a mety — `Run.Result.FinishTimes`. `ref. 0` na mapie oznacza początek geometrii, nie wspólną metę przejazdów Rolling.
- Oś zdarzeń jest chronologiczna i filtrowana zawodnikiem; **Wszyscy** wyłącza filtr. Kliknięcie znacznika zatrzymuje odtwarzanie i przechodzi do jego dokładnego czasu. Przyszłe wpisy są przygaszone.
- `ObservedAt`, `RequestedActivation`, `ActualActivation`, `CancelledAt` mają osobne znaczniki. Wpis zawiera akcję, intencję, powód, rywala i kanał. Dymek pokazuje wszystkie cztery czasy i wartości komendy; panel pokazuje ostatnią obserwację AI i jej stan w wybranym czasie. Aktywowana komenda w historii nie oznacza, że nadal jest najnowszym sterowaniem.
- **Cel poprzeczny** rysuje krzyż w przekroju toru i lokalny łącznik offset–target. To cel sterowania, nie prognoza trajektorii X/Y. **Segmenty** i **Ślady** są osobnymi nakładkami.

## Źródła i wierność

Viewer odczytuje bez kopiowania i konwersji:

| Przejazd | CSV + JSON w `docs/experiments/autonomous-racing-line/` |
| --- | --- |
| Solo | `rolling-S3-entry-accepted.csv`, `rolling-S3-entry-ON.json` |
| Rolling Straight | `rolling-straight-accepted.csv`, `rolling-straight-ON.json` |

`ReplayGeometry.cs` eksportuje natywny `TrackFrame` utworzony przez `MatchedVenueProfiles.CreateMotoarenaStandingStartTrack()`, jego `TrackMetricEmbedding`, `TrackGeometry` i szerokości `LaneModel`. Każdy segment zawiera pozycje referencji, normalne i obie fizyczne krawędzie co najwyżej 0,2 m postępu, z zachowaniem końców segmentów. Canvas łączy te punkty; nie zawiera drugiego wzoru owalu. Dyskretyzacja obrysu i interpolacja przekroju mają dla tej geometrii błąd poniżej 1 mm. Skok szerokości 12 m na prostych / 16,6 m na łukach pochodzi z modelu i pozostaje widoczny. Referencja `offset=0` jest **1 m od wewnętrznej krawędzi**.

Ten sam eksport zawiera kapsuły natywnego `BikeFootprint` i `SpeedwayBikeDimensions.Reference`: długość 2,10 m, korpus 0,30 m, kierownica 0,80 m, jej przesunięcie 0,65 m i grubość 0,04 m. Transformacja używa pozycji i `heading_rad` z CSV, nie kąta wektora prędkości. Numery, obwódka zaznaczenia i krzyże są nakładkami interfejsu, nie częścią obrysu mechanicznego. Nazwy liczbowych enumów są eksportowane z definicji C#.

Zasady odtwarzania:

- Tylko X/Y i orientacja są wygładzane między sąsiednimi zaakceptowanymi próbkami. Kąt przechodzi najkrótszą drogą przez ±π. Referencyjny postęp jest interpolowany wyłącznie do ustawienia znacznika przekroju.
- Prędkość i rzeczywisty offset panelu są ostatnimi pomiarami, bez wymyślania wartości pośrednich. Drive i target są skokowe: CSV stanowi bazę, a pomiędzy próbkami stosowane są wyłącznie zapisane `ActualActivation`. Kanał `Line` nie zmienia Drive; `Throttle` nie zmienia targetu ani intencji linii. Sam termin planu, obserwacja lub anulowanie nie wykonują komendy.
- W Solo w 0,340 s aktywny target wynosi 5,50 m, a fizyczny offset nadal 7,00 m. Renderer nie przenosi motocykla na cel.
- Nie ma ruchu po ostatniej próbce danego zawodnika. Przy mecie pojawia się ostatnia znana pozycja, jej czas oraz osobno czas przekroczenia; brakująca pozycja mety nie jest wyliczana. Nie używamy późniejszych pozycji `Result.Riders` jako zastępstwa brakującej trajektorii.
- Wyprzedzenia, kontakty i konsekwencje pochodzą wyłącznie z zapisanych zdarzeń. W Rolling Straight są dwa potwierdzone minięcia: R1/R4 w 22,240 s i R1/R2 w 35,100 s; brak zarejestrowanych kontaktów. Spadek prędkości nie jest interpretowany jako zamknięcie gazu ani wyjaśniany bez danych.

## Granice zapisu

CSV ma odstęp około 0,1 s; płynna animacja nie odtwarza pośrednich kroków fizyki. Stan końcowy jest oznaczany według czasu mety, natomiast pozycja pozostaje zaakceptowanym pomiarem:

| Przejazd / zawodnik | Ostatnia próbka [s] | Meta z JSON [s] |
| --- | ---: | ---: |
| Solo R1 | 59,610 | 59,653981 |
| Rolling R3 | 58,510 | 58,524391 |
| Rolling R1 | 58,710 | 58,765877 |
| Rolling R2 | 59,410 | 59,408135 |
| Rolling R4 | 59,710 | 59,714276 |

R2 ma próbkę tuż **po** przekroczeniu mety. W samym czasie przekroczenia widoczna jest poprzednia zaakceptowana pozycja; próbka 59,410 s staje się dostępna dopiero w swoim czasie. Nie zakładamy dokładnej pozycji na mecie. Zaplanowany termin komendy anulowanej może wypadać poza końcem przejazdu; pozostaje planem i nie wydłuża danych. Obsługiwane są wyłącznie dwa powyższe przejazdy, bez importu innych formatów i porównań ON/OFF.

## Walidacja i utrzymanie

Celowane testy, z katalogu repozytorium:

```powershell
dotnet test tests/CoreSim.Tests/CoreSim.Tests.csproj -c Release --filter FullyQualifiedName~ReplayGeometryTests
node --test tools/replay-viewer/replay.test.mjs
```

2 testy C# sprawdzają bajtową zgodność zapisanego eksportu, wszystkie punkty względem `TrackFrame`, krawędzie, wymiary kapsuł i enumy. 6 testów Node (wbudowany runner, bez npm) obejmuje oba rzeczywiste zestawy, każdy punkt CSV, stan początkowy, body heading, kanały/aktywacje/anulowanie, ±π, koniec danych, indywidualne okrążenia, mety i prawdziwe zdarzenia. Build Release z `--warnaserror` oraz 2 + 6 testów zakończyły się powodzeniem. Standardowe Draft Windows CI wykonuje regresję repozytorium; testy Node dołączono do istniejącego joba `python`, C# do sharda `core`.

Smoke test wykonano w przeglądarce wbudowanej Codex przy widoku 1280×900: oba przejazdy, play/pause 2×, kroki/cofanie, suwak do końca, kliknięcie aktywacji i anulowania, wybór zawodnika, potwierdzone minięcie, zoom, pan, fit, follow i wszystkie czasy mety. Konsola bez błędów i ostrzeżeń. Oddzielne sesje Chrome/Edge nie były dostępne w środowisku; nie deklarujemy osobnego testu obu aplikacji.

Ocena **A**: wymagane funkcje działają na rzeczywistych danych, a granice zapisu są jawne. Jedyny zakres weryfikacji odbiegający od prośby o Chrome/Edge to smoke w przeglądarce wbudowanej. Nie uruchamiano nowych symulacji ani dodatkowych historycznych benchmarków #87–#95.

Tylko po świadomej zmianie modelu geometrii można odświeżyć ten jeden eksport (polecenie nie tworzy świata i nie uruchamia symulacji):

```powershell
dotnet run --project tools/shared-world-speedway -c Release -- replay-geometry tools/replay-viewer/geometry.json
```

Nie używaj tego do regenerowania historycznych zapisów. Test bajtowej zgodności wymusi świadome utrzymanie `geometry.json`, gdy zmieni się źródłowa geometria, footprint lub enumy.

## Eksperymentalne kształty #101

Lista zawiera zapisane `inner` i `wide-tighten` dla baseline oraz Egg Mild,
Medium, Strong i Asymmetric. Każdy zestaw #101 wskazuje własny
`GeometryFile` z `docs/experiments/track-geometry-variants/`; viewer odczytuje
przekroje i nie rekonstruuje kołowego toru z promienia. Powrót do historycznego
przejazdu przywraca dotychczasowe `geometry.json`. Czasy i pozycje odrzuconych
planów są zachowane. Ich obecność w viewerze nie oznacza wykonalności planu.
Raport #101 opisuje prześwity i wszystkie pięć planów; Node ma 9 testów.


## #102 — lokalny limit i porównanie Egg Mild

Dropdown zawiera sześć planów na Egg Mild i sześć kontroli baseline z `docs/experiments/local-offset-limit/`. Każdy ładuje własny plik geometrii; stary replay przywraca jego oryginalny tor. Link startowy: `http://127.0.0.1:8000/tools/replay-viewer/?case=local-egg-mild-middle-to-outer`.

- **Limit obrysu + 0,5 m** rysuje certyfikowaną konserwatywną granicę targetu, a nie przyszłą trasę i nie limit samego środka masy. Zapas obejmuje cały motocykl przy dowolnym yaw. To linia z eksportu C#, bez liczenia geometrii motocykla na nowo w viewerze.
- **Porównaj #102** nakłada rzeczywiste zapisane drugie okrążenia trzech oddzielnych przejazdów: zielony inner, niebieski middle-tighten, różowy outer-tighten. Ich odrzucenie jest jawne; nie są wspólnym wyścigiem. R1 pozostaje zawodnikiem aktywnego replaya.
- Biały obrys to kerb, złoty banda. Etykieta pod nazwą toru pokazuje pełny wynik audytu i minimum prześwitu do bandy. Odrzucony przejazd nadal jest dostępny w całości.
- `outer-max` i `outer-tighten` są odrzucone na obu geometriach. Ich wyjazdów poza bandę nie koryguje renderer. W `middle-to-outer` przy 4,610 s zaakceptowany offset wynosi 9,994863 m; w `outer-max` przy 8,310 s widać wyjazd po zwężeniu.

Cel lokalnej polityki zmienia się z reference; panel pokazuje go z zapisu, bez prognozy. Aktywacje faz mają czasy z Result.Decisions. Ocena bezpieczeństwa pochodzi z audytu wszystkich kroków i konserwatywnej kontroli między nimi, nie z interpolacji CSV co około 0,1 s. **Śledź R1** i zoom pozwalają obejrzeć rzeczywisty obrys w skali.

Zrzuty i tabele: [raport #102](../../docs/experiments/local-offset-limit.md). Test: `node --test tools/replay-viewer/replay.test.mjs` (10 przypadków, w tym wszystkie nowe CSV/JSON i geometrie).
## #105 inverse feasibility / matched apex

The existing viewer also loads seven recorded #105 cases, for example
`?case=inverse-matched-wide-early-steady-18` and
`?case=inverse-outside-inside-outside-steady-15`. They use frozen progressive-b.
The comparison overlay shows **planned C2 curves**; the selected rider and trail
come from accepted Box2D poses. All times are diagnostic: these replays do not
prove the prescribed constant-speed trajectory is feasible. Geometry, inverse,
tracking and timing gates are separate in the experiment report.

## #106 speed / Grip / crossover

Three cases use frozen progressive-b and the existing accepted-pose adapter:

- `?case=crossover-viewer-inner`: safe, tracked slower INNER control.
- `?case=crossover-viewer-wide`: safe, tracked slower WIDE STRAIGHT control.
- `?case=crossover-viewer-stress`: rejected 200 km/h initial-speed / Drive 0 test.

The overlay contains the two planned C3 curves. The turquoise rider/trail is
recorded Box2D motion. Rejection remains visible, including the full off-track
tail: there is no wall or renderer correction. Stress CSV is sampled at about
0.1 s; the report measures first crossing from the full 5 ms native record.
The controls demonstrate feasibility, not performance at full drive. See the
[audit and five plots](../../docs/experiments/speed-grip-line-crossover.md).
