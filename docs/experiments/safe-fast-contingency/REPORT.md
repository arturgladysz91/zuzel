# Badanie #153 — CI, obrys 0,5 m, wydajność i wybór kontynuacji

**Werdykt: PARTIAL.** Nie wykazano pełnych trzech okrążeń w reżimie S 0,5 m. Dokładny wariant L zachował wszystkie 276 decyzji i cały natywny fingerprint #152: 55.167079 s. Cel 100–140 CPU-s nie został osiągnięty w bieżących pomiarach.

Dokładna baza PR #151: `a9024b5bb141e8158fc7c441705768a7b7354d35`. Gałąź `experiment/safe-fast-contingency-153`, Draft PR #152, bez merge. Fizyka A/B, opony, silnik, wymiary i geometria pozostają zamrożone. Nowy planner i wszystkie wejścia CLI są domyślnie OFF.

## Tabela A — walidacja

| Kontrola | Wynik | Zakres |
|---|---|---|
| Windows CI — brama 1 | PASS | Commit 7445a18d57746471a06724773384f5eeb8b9ecc1; [run 428](https://github.com/arturgladysz91/zuzel/actions/runs/41022785453) |
| Windows CI — końcowy HEAD | Status i dowód w PR152 | Dokładny SHA i finalny run: [opis Draft PR152](https://github.com/arturgladysz91/zuzel/pull/152) i dołączony validation-final.json. Brama 1 nie zastępuje finalnego przebiegu. |
| Hashe źródeł | PASS | 157/157; prawdziwy checkout Git z autocrlf=false oraz true. Oczekiwanych hashy nie zmieniono. |
| Lokalne core + trajectory | PASS | 2521 przypadków; Release build bez ostrzeżeń |
| Python | PASS | 328 calibration + 7 CI policy |
| Node | PASS | 93 przypadków wraz z semantyką replay L/S |
| Dane i źródła historyczne | PASS | 150 drzew historycznych; wszystkie zamrożone bloby157, narzędzia152 i produkcyjne src; source-proof.json |
| Nieuruchomione shardy | DISCOVERY ONLY | historical-analysis i four-rider są odkrywane, lecz niewykonywane w Draft według niezmienionej polityki CI. Model B: brak nowego pełnego przejazdu. |


### Przyczyna Windows CI

Nie było zmiany treści AdaptiveRiderAudit.cs. Blob LF ma SHA-256 `51ced9188e9bea9b2d0732aa5110ba87f29cd7cf7682b262127e0990d5b22129`; jego dokładny checkout CRLF ma oczekiwane `a64e90a08d0e044cef51c1049eba63df98c50c41d0d11b37f879971b510aa479`. Wyłączenie autocrlf w poprzednim workflow odsłoniło brak jawnych reguł końców linii. Audyt znalazł62 takie pliki oraz jeden plik LF wymagający przypięcia. Dodano reguły per plik, bez renormalizacji blobów, filtrów treści ani zmiany manifestu. Nowa regresja zachowuje tożsamości bazowych blobów również w płytkim checkoutcie Python. Pierwszy nowy przebieg wykrył brak obiektu przodka w tej regresji; poprawiony przebieg 428 przeszedł.

## Tabela B — bezpieczeństwo

| Próba | Reżim | Min obrysu m | Min sweep m | β max ° | Kontakty | Próbki <0,5 m / wszystkie | Odrzucone plany | Pełne ukończenia L/S |
|---|---|---|---|---|---|---|---|---|
| baseline149-final148 | Legacy | 0.184870 | 0.105992 | 29.455 | 0 | 969/5412 | N/D | 1/0 |
| efficient152-A-portfolio-v1 | Legacy | 0.164811 | 0.085106 | 18.846 | 0 | 983/5530 | 1116 | 1/0 |
| cold-L-prefix-v1 | Legacy | 0.209018 | 0.117625 | 10.612 | 0 | 295/1203 | 226 | 0/0 |
| exact-L-v2 | Legacy | 4.099200 | 4.089113 | 0.000 | 0 | 0/61 | 0 | 0/0 |
| exact-L-v3 | Legacy | 0.164811 | 0.085106 | 18.846 | 0 | 983/5530 | 1116 | 1/0 |
| ranked-L-v1 | Legacy | 0.258418 | 0.154001 | 10.538 | 0 | 58/722 | 491 | 0/0 |
| ranked-L-v2 | Legacy | 0.258418 | 0.154001 | 10.538 | 0 | 213/1203 | 602 | 0/0 |
| ranked-L-v3 | Legacy | 0.209018 | 0.117625 | 26.039 | 0 | 459/5561 | 1433 | 1/0 |
| ranked-S-v1 | SafetyHalfMeter | 0.582069 | 0.501181 | 12.808 | 0 | 0/1384 | 3130 | 0/0 |


Clearance mierzy najbliższy punkt sumy kapsuł chassis i kierownicy, z obrotem całego obrysu. Wymiary pozostają2,10 m długości,0,30 m chassis i0,80 m kierownicy. To nie odległość środka. Krzywa ma cięciwy ≤5 cm; clearance już odejmuje0,7 mm oraz0,1 mm zapasu numerycznego. Podane minima są konserwatywnymi dolnymi granicami rzeczywistego odstępu. Dla dodatnich historycznych odległości górna niepewność wynosi1,425 mm; nie wolno odejmować ani dodawać ponownie całych0,5 m.

`Planner146.Check(...,.1)` oznacza0,10 m zapasu w punktach i osobny wymóg sweep≥0. `footprint reserve` dotyczy punktu; `uncertified swept interval` dotyczy dolnej granicy całego ruchu. Reżim S wymaga obu wartości ≥0,5 m. Dla held-wrench interwału używamy `min(gapA,gapB) − dt/2*(max(vA,vB)+R*max(|yawA|,|yawB|)) −0,0005 m`. Granica obejmuje translację i yaw, wszystkie części obrysu, łuki i przejścia segmentów. Ten sam warunek działa w każdym rolloucie, wszystkich13 węzłach, dziewięciu rezerwach, świeżym niezależnym preflight i po każdym natywnym kroku przed zaliczeniem postępu/mety.

### Nowa walidacja historycznych przejazdów

| Zapis | Próbki spełniające0,5 m | Pewne naruszenia fizyczne | Certyfikowane interwały S | Legalne wykonane decyzje S | Min środka m | Legalne3 okrążenia S |
|---|---|---|---|---|---|---|
| baseline149-final148 | 4443/5412 | 963 | 4176/5411 | 199/270 | 0.604976 | False |
| efficient152-A-portfolio-v1 | 4547/5530 | 979 | 4345/5529 | 206/276 | 0.580740 | False |


**53,989766 s nie jest legalnym wynikiem S. Tak samo55,167079 s #152 nie jest legalnym S.** Spośród276 historycznych certyfikatów152 tylko 8 przechodzi dodatkowy filtr wszystkich zapisanych nominalnych/węzłowych/terminalnych marginesów0,5 m. Jest to nowy audyt, nie zmiana ich certyfikatów. Dokładne odcinki naruszeń: `historical-violations.csv`, każdy pomiar i decyzja: `history/*.json.gz`.

Minimalna szerokość fizyczna to 12.000 m. Statyczny środkowy korytarz w1914 próbkach pozycji/yaw0,±45° ma co najmniej 5.209 m zapasu. Geometria daje miejsce; ten pomiar nie dowodzi dynamicznej wykonalności wszystkich łuków. Nie poszerzono toru ani nie zmniejszono motocykla.

## Tabela C — wydajność

`baseline149-final148` to powtórka #148 w badaniu149; `contingency151-gate4-A-v2` to nieukończony prefiks #151; `efficient152-A-portfolio-v1` to pełny #152. Zakresy są jawne.

| Próba | Zakres natywny | CPU kontrolera | Pełne K1 | CPU K1 | CPU kontynuacji | CPU preflight | Prognozy | Zapytania kontaktu | CPU decyzji p50/p95/max |
|---|---|---|---|---|---|---|---|---|---|
| baseline149-final148 | 3 okr. / 53.990 s | 83.156 | 1643 | 68.516 | N/D | 0.828 | 74091 | 3552122 | 0.031/2.672/10.344 |
| contingency151-gate4-A-v2 | 1 okr. / 34.600 s | 276.953 | 5847 | 157.156 | 38.484 | 44.172 | 419288 | 16288962 | 0.484/6.062/22.266 |
| efficient152-A-portfolio-v1 | 3 okr. / 55.170 s | 153.891 | 2273 | 56.953 | 44.547 | 38.547 | 395430 | 11364290 | 0.328/2.297/9.984 |
| cold-L-prefix-v1 | 0 okr. / 12.000 s | 70.938 | 719 | 30.234 | 17.594 | 16.672 | 106185 | 3184152 | 0.641/5.031/12.016 |
| exact-L-v2 | 0 okr. / 0.600 s | 9.250 | 106 | 5.984 | 1.188 | 1.406 | 8815 | 319614 | 0.906/0.906/6.312 |
| exact-L-v3 | 3 okr. / 55.170 s | 236.172 | 2273 | 87.453 | 67.906 | 56.812 | 395430 | 11364290 | 0.516/3.531/15.203 |
| ranked-L-v1 | 0 okr. / 7.200 s | 139.219 | 2818 | 107.297 | 13.594 | 11.828 | 146191 | 6601480 | 0.781/11.641/23.609 |
| ranked-L-v2 | 0 okr. / 12.000 s | 204.422 | 4115 | 155.562 | 21.719 | 17.812 | 218015 | 9733829 | 0.750/11.641/30.031 |
| ranked-L-v3 | 3 okr. / 55.480 s | 287.469 | 3369 | 127.750 | 73.688 | 59.922 | 441116 | 13733367 | 0.516/4.156/16.188 |
| ranked-S-v1 | 0 okr. / 13.800 s | 325.391 | 6329 | 247.953 | 46.984 | 18.484 | 337076 | 15010263 | 1.438/16.000/22.672 |


Nie sumujemy ponownie propagacji zawartej w K1. Zapytania kontaktu to obliczenia prognoz/feedback, nie liczba zderzeń. Zderzenia są w tabeli B. N/D oznacza brak porównywalnego licznika w historycznym zapisie; nie jest zerem. Quantyle dotyczą pełnych prób callbacku, włącznie z odmową, tam gdzie istnieje `timings/*.json`; dla historii i exact-L-v3 dotyczą samej funkcji planera. Analiza zawiera jawne `cpu_quantile_scope`. CPU całego kontrolera obejmuje digest, archiwizację i przerwane decyzje. Nowe fazy/liczniki obejmują również końcową nieudaną decyzję, jeśli jej strukturalny profil jest dostępny w `abstain.json.gz`.

### Dokładna optymalizacja i odrzucone warianty

| Czas obserwacji | Stary152 CPU | Nowy153 CPU | Zgodność wszystkich bitów |
|---|---|---|---|
| 0.0 | 4.969 | 4.047 | True |
| 0.4 | 0.812 | 0.766 | True |
| 2.6 | 4.875 | 4.797 | True |
| 10.0 | 0.422 | 0.406 | True |
| 29.8 | 2.656 | 2.656 | True |


Po pominięciu pierwszej pary obciążonej rozgrzewaniem JIT: 8.766→8.625 CPU-s (1.60%). To mały zysk, nie osiągnięcie celu100–140. Pierwsza para i pełne koszty są zachowane. Pierwszy benchmark na trudnym stanie151/29,8 s przekroczył niezmieniony limit30 CPU-s przy699 K1; nie jest PASS. Wariant pamięci podkroków miał zero trafień i nie został wybrany.

Wybrana dokładna zmiana usuwa konstrukcję nieużywanych kluczy wyłączonego cache, zbędne delegaty per punkt i alokacje sprawdzenia kolejki. Zachowuje pełną siatkę K1, wszystkie scenariusze i komendy. Niezależny DAG nadal propaguje świeże stany, ma własny BVH/cache i sprawdza końcowy wynik przygotowanego solvera zimnym oryginalnym równaniem koła. Nie przyjmuje wyszukiwanych ani dawnych wyników jako certyfikatu. Test A/B uszkodzonego cache wymusza odmowę.

Kontrola starego152 jest celowo częściowa: wspólny prefiks0–12.0 s, 60 decyzji i 1203 bitowo zgodnych próbek ze wszystkimi polami. CPU samego planera w tej sesji: 69.375→66.703, oszczędność 3.85%. Archiwalny stary152 na tym samym prefiksie miał 43.547 CPU-s. To pokazuje różnicę pomiarów; nie przypisujemy jej w całości algorytmowi. Nie wykonano pełnej kontroli starego152 w tej sesji i nie porównujemy jej częściowej sumy z pełnym exact.

### Zmiana algorytmiczna: ranking

Najpierw powstaje legalna pełna rodzina 13/9. Co 1 s sprawdzamy najwyżej dwie dodatkowe propozycje; każda wybieralna polityka musi przejść wszystkie scenariusze i niezależny replay. Porównanie używa identycznego celu geometrycznego, czasu najwolniejszej rezerwy, czasu nominalnego, prędkości w dalszym przekroju, długości drogi, gazu i zapasu sweep. Nie ma nagrody za wewnętrzną/szeroką linię ani samą maksymalną prędkość. Nie ma dostępu do przyszłego stanu natywnego.

Wariant v1 zatrzymał się w7,2 s. W7 s wybrał nominalny zysk0,340 s, mimo pogorszenia najwolniejszej rezerwy o0,292 s. V2 nadał pierwszeństwo najwolniejszej rezerwie; ukończył tylko prefiks do12 s, gdzie generowanie kolejnej polityki wyczerpało limit30 CPU-s. Zachowano wszystkie kontrole, certyfikaty i nieudane próby.

V3 ogranicza opcjonalny ranking do rewalidowanych zachowanych polityk i ich sprzężenia zwrotnego. Gdy alternatywa wymaga regeneracji całego odległego ogona, nie jest promowana. Limit ośmiu dodatkowych pełnych K1 dotyczy wyłącznie opcjonalnych kandydatur po zabezpieczeniu legalnej rodziny. Nie zmniejsza liczby scenariuszy żadnego przyjętego certyfikatu. Podstawowe szukanie wykonalności zachowuje poprzednie limity i kolejność.

Wynik v3: completed, 3/3, czas mety 55.472452 s, 287.469 CPU-s. Zmiana czasu L względem #152: 0.305374 s (ujemna byłaby poprawą). Gaz zamknięty krócej o 2.400 s, ale linia dłuższa o 16.248 m. Pierwszy łuk trzeciego okrążenia poprawił się, lecz strata w drugim łuku pierwszego okrążenia i pozostałych odcinkach przeważyła. S nie jest używany jako dowód poprawy czasu L.

| Wariant | Obserwacja s | Nominalny zysk wybranego s | Zysk najwolniejszej rezerwy s |
|---|---|---|---|
| ranked-L-v1 | 3.0 | 0.111758 | 0.112446 |
| ranked-L-v1 | 7.0 | 0.339779 | -0.292185 |
| ranked-L-v2 | 3.0 | 0.111758 | 0.112446 |
| ranked-L-v2 | 7.0 | 0.000000 | 0.000000 |
| ranked-L-v2 | 8.0 | 0.135245 | 0.134908 |
| ranked-L-v2 | 10.0 | 0.056714 | 0.056711 |
| ranked-L-v2 | 11.0 | 0.034253 | 0.034250 |
| ranked-L-v3 | 10.0 | 0.078516 | 0.078416 |
| ranked-L-v3 | 16.0 | 0.107698 | 0.107634 |
| ranked-L-v3 | 42.0 | 0.094366 | 0.094368 |
| ranked-L-v3 | 49.0 | 0.020248 | 0.020034 |
| ranked-L-v3 | 51.0 | 0.059512 | 0.059287 |
| ranked-L-v3 | 52.0 | 0.063344 | 0.063473 |
| ranked-L-v3 | 53.0 | 0.044235 | 0.044224 |


### Brama S — dokładny powód PARTIAL

Świat zatrzymany w 13.800 s po 69 przyjętych pełnych certyfikatach; ukończono 0/3 okrążeń. Minimum wykonanego obrysu 0.582069 m, minimum natywnego sweep 0.501181 m, kontakty 0. Ostatnia próba nie dostała komendy: po 14.391 CPU-s tej decyzji wyczerpała się rezerwacja330 CPU-s całego eksperymentu. Częściowe drzewo miało 9/13 węzłów i 6/9 rezerw; nie zostało uznane za certyfikat. Łącznie wykonano 6329 pełnych K1 już na tym krótkim dystansie. Rozkład ocen prognoz w ostatniej próbie (pozycja legal oznacza prognozę dopuszczoną): `{'legal complete forecast; swept physical footprint>=0.5m': 10, 'footprint reserve': 77, 'swept physical footprint reserve0.5m': 33, 'beta45 envelope': 1}`. Brak nowej komendy był poprawną odmową, nie ukończeniem ani dowodem braku miejsca na torze.

## Tabela D — jazda przez sześć łuków

| Próba / łuk | Czas s | V wej/min/wyj km/h | β max ° | Gaz0 s | Linia m | Min obrysu m | CPU planera w łuku |
|---|---|---|---|---|---|---|---|
| baseline149-final148 / 1.1 | 5.821708 | 65.609/55.197/65.247 | 10.613 | 2.364 | 97.715 | 0.204562 | 2.188 |
| baseline149-final148 / 1.2 | 5.553004 | 80.719/56.149/57.667 | 13.338 | 4.023 | 100.119 | 0.227222 | 2.000 |
| baseline149-final148 / 2.1 | 5.553718 | 78.057/59.170/66.379 | 12.668 | 3.049 | 102.046 | 0.245858 | 43.516 |
| baseline149-final148 / 2.2 | 5.605502 | 80.549/53.960/58.342 | 29.455 | 3.595 | 99.873 | 0.184870 | 12.000 |
| baseline149-final148 / 3.1 | 5.880781 | 81.648/51.595/59.360 | 15.444 | 4.310 | 100.625 | 0.318131 | 1.828 |
| baseline149-final148 / 3.2 | 5.465566 | 80.660/57.805/63.929 | 18.932 | 3.426 | 100.272 | 0.206371 | 1.266 |
| efficient152-A-portfolio-v1 / 1.1 | 5.822031 | 65.609/55.198/65.246 | 10.612 | 2.364 | 97.721 | 0.209018 | 12.500 |
| efficient152-A-portfolio-v1 / 1.2 | 5.550827 | 80.711/56.151/57.657 | 13.341 | 4.023 | 100.077 | 0.206427 | 18.062 |
| efficient152-A-portfolio-v1 / 2.1 | 5.604299 | 78.062/56.114/62.324 | 15.260 | 4.050 | 102.422 | 0.164811 | 23.719 |
| efficient152-A-portfolio-v1 / 2.2 | 5.738579 | 81.135/55.722/58.387 | 18.734 | 4.233 | 102.183 | 0.309897 | 10.578 |
| efficient152-A-portfolio-v1 / 3.1 | 6.356991 | 81.350/52.603/58.771 | 18.846 | 4.237 | 104.218 | 0.248325 | 18.203 |
| efficient152-A-portfolio-v1 / 3.2 | 5.767208 | 80.382/53.661/59.436 | 18.157 | 4.241 | 100.236 | 0.209106 | 3.219 |
| exact-L-v3 / 1.1 | 5.822031 | 65.609/55.198/65.246 | 10.612 | 2.364 | 97.721 | 0.209018 | 19.531 |
| exact-L-v3 / 1.2 | 5.550827 | 80.711/56.151/57.657 | 13.341 | 4.023 | 100.077 | 0.206427 | 27.922 |
| exact-L-v3 / 2.1 | 5.604299 | 78.062/56.114/62.324 | 15.260 | 4.050 | 102.422 | 0.164811 | 36.422 |
| exact-L-v3 / 2.2 | 5.738579 | 81.135/55.722/58.387 | 18.734 | 4.233 | 102.183 | 0.309897 | 16.188 |
| exact-L-v3 / 3.1 | 6.356991 | 81.350/52.603/58.771 | 18.846 | 4.237 | 104.218 | 0.248325 | 28.047 |
| exact-L-v3 / 3.2 | 5.767208 | 80.382/53.661/59.436 | 18.157 | 4.241 | 100.236 | 0.209106 | 4.906 |
| ranked-L-v3 / 1.1 | 5.822031 | 65.609/55.198/65.246 | 10.612 | 2.364 | 97.721 | 0.209018 | 19.875 |
| ranked-L-v3 / 1.2 | 6.045247 | 85.628/53.494/57.592 | 19.141 | 3.909 | 109.640 | 0.448519 | 40.547 |
| ranked-L-v3 / 2.1 | 5.795661 | 77.241/56.128/58.864 | 14.917 | 3.860 | 102.698 | 1.357548 | 18.406 |
| ranked-L-v3 / 2.2 | 5.671616 | 77.908/63.072/68.618 | 9.529 | 2.687 | 105.331 | 0.218702 | 102.328 |
| ranked-L-v3 / 3.1 | 6.011035 | 90.775/47.154/53.531 | 26.039 | 4.600 | 100.146 | 0.289699 | 17.234 |
| ranked-L-v3 / 3.2 | 5.854385 | 79.135/58.750/61.134 | 10.191 | 3.833 | 107.626 | 1.943823 | 5.844 |
| ranked-S-v1 / 1.1 | 6.138057 | 65.624/56.551/58.551 | 8.628 | 3.564 | 102.157 | 0.582069 | 124.188 |
| ranked-S-v1 / 1.2 | N/D | 80.279/N/D/N/D | N/D | N/D | N/D | N/D | N/D |
| ranked-S-v1 / 2.1 | N/D | N/D/N/D/N/D | N/D | N/D | N/D | N/D | N/D |
| ranked-S-v1 / 2.2 | N/D | N/D/N/D/N/D | N/D | N/D | N/D | N/D | N/D |
| ranked-S-v1 / 3.1 | N/D | N/D/N/D/N/D | N/D | N/D | N/D | N/D | N/D |
| ranked-S-v1 / 3.2 | N/D | N/D/N/D/N/D | N/D | N/D | N/D | N/D | N/D |


| Próba | Okrążenie1 s | Okrążenie2 s | Okrążenie3 s | Gaz0 cały zapis s | Długość całego zapisu m |
|---|---|---|---|---|---|
| baseline149-final148 | 19.540281 | 17.203391 | 17.246094 | 23.840 | 975.622 |
| efficient152-A-portfolio-v1 | 19.538742 | 17.503392 | 18.124944 | 26.840 | 981.730 |
| exact-L-v3 | 19.538742 | 17.503392 | 18.124944 | 26.840 | 981.730 |
| ranked-L-v3 | 19.931149 | 17.583210 | 17.958094 | 24.440 | 997.977 |
| ranked-S-v1 | N/D | N/D | N/D | 5.400 | 216.929 |


Czasy granic są interpolowane na wspólnej geometrii. CPU łuku obejmuje decyzje podjęte w nim; przygotowanie na poprzedniej prostej należy do prostej. N/D oznacza nieukończony łuk lub dystans. Decymacja służy tylko prezentacji; pomiary wykorzystują gęste próbki natywne.

## Odpowiedzi i dalsze bramy

1. **Windows CI:** naprawiony; brama1 przeszła na GitHub. Końcowy run i dokładny HEAD są publikowane w metadanych PR oraz w dostarczonym pliku finalnej walidacji.

2. **Historyczne0,5 m:** naruszone dla obrysu mimo poprawnego odstępu środka. Tabela historyczna rozdziela pewne naruszenia od próbek niecertyfikowanych z powodu błędu numerycznego.

3. **Trzy okrążenia S:** NIE. Brak pełnego legalnego ukończenia; szczegółowa odmowa, pierwszy interwał, obserwacja i wszystkie próby są w policies/ranked-S-v1/abstain.json.gz. Nie jest to dowód fizycznej niemożliwości toru.

4. **CPU bez utraty certyfikacji:** dokładna zgodność276 decyzji i pełnego native fingerprint; mały zysk na sparowanych pomiarach. Cel100–140 CPU-s pozostaje niespełniony; duża różnica względem archiwalnych timingów nie jest ukrywana.

5. **Czas względem152 w L:** nie wykazano poprawy pełnego czasu.

6. **Dlaczego długo zamyka gaz:** oryginalny K1 wybiera lokalny kompromis w0,9 s, a feedback zachowuje jego kanał gazu. Długie rezerwy muszą przejść następny łuk przy nieodwołalnej kolejce0,24 s. Krótkie otwarcie gazu może przyspieszać nominalną gałąź i pogarszać dalsze rezerwy — kontrprzykład7 s pokazuje to bezpośrednio. Brakuje taniej, dłuższej skoordynowanej alternatywy kierowania/gazu. Samo promowanie gazu lub prędkości nie rozwiązuje tego problemu.

7. **Czy ranking znajduje lepsze manewry:** v1/v2 mają lokalne certyfikowane zyski, ale nie pełne ukończenie. O ostatecznym v3 rozstrzygają jego pełny czas, tabela rankingów i CPU; lokalny zysk prognozy nie jest rekordem całego biegu.

8. **Przed czterema zawodnikami:** najpierw pełne S i utrzymanie wykonalności po rzeczywistych kolejnych obserwacjach, potem model B i kontrolowany koszt. Dalej potrzebne są wspólne snapshoty czterech opóźnionych sterowników, prognozy wzajemnego obrysu, niezależne certyfikaty interakcji i kontaktów oraz rzeczywisty wspólny bieg. Skończone skorelowane scenariusze solo nie są globalną gwarancją zakłóceń ani dowodem gotowości czterech motocykli.

## Budżet, wyniki negatywne i reprodukcja

Zmierzony ledger przed zakończeniem tego eksportu: 1452.281 CPU-s, 8 rezerwacji świata, limit1500 CPU-s /20 światów. Finalną sumę odczytuje się z budget.json po zamknięciu eksportu. Pierwsza rezerwacja exact-L-v1 zakończyła się przed utworzeniem świata z powodu braku katalogu journal; jest liczona konserwatywnie jako jedna rezerwacja. Exact-L-v2 zatrzymał się w0,6 s z powodu porównania nieznormalizowanego NextReview ze znormalizowanym stanem planera; sam digest i obserwacja były identyczne. Obie nieudane próby i ich CPU pozostają zachowane.

Pełne źródła każdego benchmarku i natywnej próby są w sources/*.zip; odpowiadają rzeczywistemu kodowi z chwili próby, także dla odrzuconych wersji. Nie wszystkie starsze negatywne warianty są aktywnymi flagami finalnego CLI. Nowe dowody mają własny katalog. Historyczne manifesty, drzewa i pomiary nie zostały nadpisane. Kompilacja, standardowe regresje i CI są poza budżetem nowych eksperymentów.

Reprodukcja i jawne flagi: [README narzędzia](../../../tools/safe-fast-contingency/README.md). Replay obu reżimów: `tools/replay-viewer/?case=safety153`, wybór zapisu pokazuje L/S i rzeczywisty outcome. Gęste native/*.json.gz, hash-chain journals, wszystkie13 węzłów i dziewięć rezerw oraz porównania forecast/telemetrii są dołączone. CPU: decision-counts.csv, timings/*.json, Profile w każdym journal/policy. Marginesy: history/*.json.gz i historical-violations.csv.
