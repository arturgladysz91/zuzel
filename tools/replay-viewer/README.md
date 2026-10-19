# Shared World Speedway — Diagnostic Replay Viewer 2D (#96)

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
