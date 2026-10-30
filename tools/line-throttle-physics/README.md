# Badanie #156: linia, gaz i fizyka

**PROVISIONAL, OFF bez jawnej komendy.** Tylko diagnostyka offline; bez nowej
autonomicznej polityki. Baza PR154 `d1eda795c66a527d25f9ecebd22a6f82d30e5a9b`.
[Raport](../../docs/experiments/line-throttle-physics/REPORT.md),
[macierz natywna](../../docs/experiments/line-throttle-physics/matrix.csv),
[replay](../replay-viewer/study156.html).

## Odtwarzanie

Uruchamiać z katalogu repozytorium na Windows/.NET8:

```powershell
dotnet restore tools/line-throttle-physics --locked-mode
dotnet build tools/line-throttle-physics -c Release --no-restore --warnaserror
dotnet tools/line-throttle-physics/bin/Release/net8.0/line-throttle-physics.dll --matrix first NOWA-ETYKIETA 60
```

`first`, `second`, `historical` to pełne zachowane stany przed łukami. Dane wyjściowe
są chronione `CreateNew`; nie używać zapieczętowanych etykiet. Budżet jest wspólny
z analizą i eksportami. Twardy limit1200 CPU-s, nowe rezerwacje muszą pozostawić100 s.
Niezamknięta rezerwacja blokuje dalsze uruchomienia do wyjaśnienia; nie usuwać jej.

`--refine fixture NOWA-ETYKIETA 50` bada zrównoważone punkty przygotowania, wejścia,
apexu, wyjścia i prostej. Punkty odniesienia v3/v4 są jawne w `Refine156.cs`.
`--native ścieżka-do-attempt.json.gz NOWA-ETYKIETA` odtwarza pełny natywny prefiks
od zatrzymanego startu i wykonuje zapisane komendy. Nie przywraca ciała do snapshotu.
`--held ścieżka held-first|held-second NOWA-ETYKIETA` przekazuje taśmę bez strojenia,
z zachowaniem własnego pending odłożonego stanu. `--ablate T0 T1 NOWA-ETYKIETA`
rozdziela komponenty gazu i kierownicy. `--control fixture NOWA-ETYKIETA` używa
dosłownie oryginalnego `Policy144.Choose`, bez kondycjonowania rodziny.

Rodzina to wykonany offset ważony drogą w środkowych20–80% łuku. T0 zachowuje
oryginalną siatkę i score K1, ale dodaje diagnostyczny korytarz rodziny i S;
nie jest niezmienionym autonomicznym baseline. T1 optymalizuje komendy offline
wyłącznie według czasu legalnego całego manewru. Wszystkie PASS należą do natywnego
Box2D, mają identyczny pełny stan początkowy i ciągły swept margin0,5 m oraz beta45.
Niepełne lub odrzucone prognozy nie dostają pełnego czasu i nie dowodzą niemożliwości.

`node tools/line-throttle-physics/generate156.mjs --check` potwierdza, że nowy natywny
harness różni się od #153 tylko granicą okna audytu i geometrycznym stopem. Równania
sił, wału, sprzęgła, koła, aktuatora oraz natywne kroki i adapter są niezmienione.

## Analiza bez symulowania

`analyze156.py NOWA-ETYKIETA` czyta natywne dane i tworzy CSV, energię, rzeczywistą
krzywiznę z v×a oraz wybiera najkrótszy natywny PASS każdej komórki. Zachowuje też
wszystkie gorsze, negatywne, kontrolne i odłożone próby. `plot156.py` wymaga
Matplotlib/NumPy; `report156.py` buduje raport i samodzielny replay HTML. Eksporty
również dopisują koszt CPU do ledgeru i są operacjami badawczymi, nie testami CI.

Wczesny pilot kierowania do pięciu zmiennych offsetów nie znalazł pełnego T1;
całe wyniki negatywne pozostają w `search-first-pilot-v2.json` i `attempts/`.
Badanie nie ogranicza się do tego nieudanego regulatora: v3/v4/v5 propagują pełne
komendy oryginalnego modelu i mają natywne świadki. Nie promowano nowego AI.

`budget.json` zawiera osobno130 s pełnej utraconej rezerwacji po błędzie dysku i100 s
prowizji za wcześniej niemierzoną inicjalizację procesów. To koszt rozliczony,
nie bezpośredni pomiar. Przerwany zerowy plik `.json.gz` jest świadomie zachowany
i nie może być czytany jako poprawny gzip. Końcowe źródła i archiwa wersji są jawne;
zmiany lokalizacji plików i ochrony zapisu po pomiarach nie zmieniają fizyki ani taśm.

Wszystkie historyczne drzewa, produkcyjne `src`, `data` i wspólna fizyka zachowują
identyczne Git SHA. Standardowe regresje/CI są poza budżetem eksperymentu.
