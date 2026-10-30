# Hierarchiczny generator manewrów — badanie #155

**PROVISIONAL, domyślnie OFF. PARTIAL.** Baza `405111a721d5bc5ba1477999f25b3837e8c62307`.
Osobny program badawczy; produkcyjny silnik i dane pozostają zamrożone.
[Raport](../../docs/experiments/hierarchical-maneuvers/REPORT.md),
[dane i profile](../../docs/experiments/hierarchical-maneuvers/analysis.json),
[replay prognoz](../replay-viewer/hierarchy155.html).

## Uruchomienie z katalogu repozytorium

```powershell
dotnet restore tools/hierarchical-maneuvers --locked-mode
dotnet build tools/hierarchical-maneuvers -c Release --no-restore --warnaserror
dotnet tools/hierarchical-maneuvers/bin/Release/net8.0/hierarchical-maneuvers.dll --probe C 0 new-label
```

Role: A = historyczny S0; B = wiązka prymitywów; C = wiązka z kontrolą przyszłego etapu.
Zapisane stany: `0`, `6.6`, `13.8`. A w późniejszych stanach ma własny poprzedni
certyfikat; B/C używają wyłącznie aktualnego stanu/kolejki, bez historycznych przyszłych
taśm. Jednorazowa etykieta jest obowiązkowa: zapis `CreateNew` oraz rezerwacja budżetu
chronią również wyniki negatywne. Nie uruchamiaj ponownie z istniejącą etykietą.

Prymityw obejmuje trzy obserwacje (0,6 s), punkty zmiany gazu i kierownicy, lokalne
sprzężenie od sił opon oraz fazę wybieraną z rzeczywistego stanu. Wiązka ma szerokość4,
maksimum130 obserwacji (26 s). Brak K1 w nowym generatorze. C używa fizycznej kontroli
nieodwracalnego prefiksu0,2 s oraz świadków następnego prymitywu. Ranking krzywizny,
prędkości i ETA jest heurystyką. Odrzucenie wiązki nie dowodzi fizycznej niemożliwości.

`generate155.py --check` weryfikuje mechaniczne przeniesienie protokołu13/9 z #153:
pełne dalsze przekroje terminalne, obrys0,5 m, sweep, kolejki0,24 s, niezależna
propagacja unikalnych prefiksów. Zachowane limity32768 prognoz /2M zapytań /30 CPU-s
na decyzję. Maksimum4 generacje wiązki w decyzji ogranicza propozycje, nie usuwa
żadnego wymaganego scenariusza. Każda nowa obserwacja wymaga nowej walidacji.

`--stability v1` odczytuje własną politykę C-0.0-v4 i bada stan0,2 s: oryginalny oraz
dwa mikroodchylenia. Ciało pochodzi z rzeczywistego #154; własna komenda nie mogła
jeszcze zadziałać przed0,24 s. To diagnostyka przyczynowego prefiksu, nie nowy przejazd.
`--export-replays` odtwarza fizyczne hashe przyjętych polityk i eksportuje pełne/prefiksowe
prognozy. Zapis natywny w replayu jest jawnie niezmienionym odniesieniem #154.
Te etykiety istnieją już w zapieczętowanym zestawie; nie służą do nadpisania danych.

Budżet:1200 CPU-s, bez rozszerzenia. Normalne próby kończą się przed1150, eksport
przed1198 (rezerwa na zapis). `budget.json` zawiera wszystkie23 próby, trzy kontrole
stabilności i eksporty. Nieukończona rezerwacja blokuje dalsze uruchomienia do inspekcji;
nie kasuj jej. Źródła każdego uruchomienia są w `sources/` z SHA-256.
`analyze155.py`/`report155.py` czytają zamknięte dane i dopisują własny koszt.

Lokalne C-0.0-v4 ma pełne13/9 i zero K1. B nie uzyskało żadnego pełnego certyfikatu;
C odmówiło w obu późniejszych stanach. Nie promowano C do bram natywnych C–E.
Nie ma dowodu niezmienniczego zbioru terminalnego ani ukończonego biegu S.
