# Walidacja badania #148

Baza b92694ada648404bdd8180f8d02ac9138b67ac66, bez merge. Wariant domyślnie OFF.

## Lokalnie

- Restore i Release build z --warnaserror: zero ostrzeżeń/błędów.
- C#: 2479/2479 core + trajectory, w tym 6 nowych testów dokładnego koła, prefiksu, pełnej prognozy, niezależnego preflight, limitów każdego odcinka i obrysu.
- Python: 282/282 testy danych, 7/7 polityki CI. Osiem nowych testów #148 ponowiono po poprawce strażnika.
- Node: 76/76, w tym 3 nowe testy reprezentatywnego replayu, pełnych decyzji i rozróżnienia porażek β45°.
- Discovery: 3000, każdy przypadek dokładnie w jednym shardzie: core 2084, trajectory 395, historical-analysis 473, four-rider 48.
- Generator źródeł porównany tekstowo; 4534 historyczne śledzone pliki źródłowe/danych bez zmiany blobów Git. EOL plików tekstowych jest interpretowany zgodnie z istniejącymi .gitattributes; surowe hashe istotnej fizyki zapisane osobno.
- Wszystkie 5412 próbki i 270 komend każdego z A1/A2/A3 są dokładnie równe #147. Dziennik A3 jest kompletny i ma poprawny łańcuch hashy; każda komenda zapisana na dysku przed zwrotem do natywnej kolejki.
- Wykresy prędkości, energii i diagnostyk obejrzano. Node sprawdza dane i semantykę viewera. UI browser QA nie potwierdzono: narzędzie CUA zakończyło inicjalizację błędem `failed to write kernel assets`. Podgląd HTTP i testy danych nie są przedstawiane jako wizualna kontrola UI.
- git diff --check.

## Poprawka wykryta przez regresję

Próba limitu jednej prognozy wykazała, że prefix i tail miały wspólną wstępną kontrolę zamiast kontroli przed każdym fizycznym wywołaniem. Dopisano osobny CheckWork przed każdym odcinkiem; test przechodzi. Usunięcie wyłącznie tych dwóch wywołań odtwarza źródło runtime zmierzonego A3 (`1e19a5a`). Nie zmieniono modelu, celu ani kryteriów bezpieczeństwa. Największe zmierzone wartości wynoszą 7474 segmenty <8192, 463249 kontaktów <500000 opcjonalnych /700000 globalnych i 10,28125 CPU-s <30. Nie powtarzano native po poprawce; pomiar 82 CPU-s należy do poprzedzającego ją commita, co raport jawnie zaznacza.

## Windows CI

Istniejąca polityka Draft: Windows restore/build/discovery, Python/Node, pełne core i trajectory oraz końcowy gate Draft. Historyczne i four-rider audyty są pomijane zgodnie z polityką repozytorium; nie zmieniono workflow. Konkretny wynik runu i końcowy head SHA są w opisie PR i dostarczonym Walidacja148.json.

## Zakres fizyczny i koszt

24 nowe światy, 600,421875 CPU-s łącznie z analizami. Osiem CPU-s jest jawnie oznaczoną konserwatywną górną granicą po błędzie czytnika w postprocessingu, nie pomiarem CPU; pozostałe rezerwacje są mierzone czasem procesu. Build/testy/renderowanie raportów nie są nowymi eksperymentami. Brak nowych eksperymentów po etapie diagnostyki. Zachowano 899,578125 CPU-s budżetu.

Trzy pełne A kończą legalnie, sparse-tail B jest odrzucony po krótkich porównaniach, a osiem kontrolowanych okien diagnostycznych kończy zaplanowany timeout bez naruszeń. Trzy przyczynowe impulsy kończą się limitem β45°, bez potwierdzonego kontaktu ani wyjazdu. Nie dowiedziono globalnego maksimum legalnej prędkości, skalibrowanego realizmu opony ani gotowości czterech zawodników. CPU zmierzono lokalnie na Windows / Ryzen 5 3500X, target .NET 8; nie jest to czas wall ani wynik innej maszyny.
