# Badanie #152 — wydajny planner warunkowy

**Trzy autonomiczne okrążenia A ukończone: 55.167079 s.** Sterownik: 153.891 CPU-s, sama funkcja planowania: 150.609 CPU-s, cała próba: 164.328 CPU-s. Brak kontaktów i naruszeń fizyki/obrysu. Cel100–140 CPU-s nie został osiągnięty. B pozostaje częściowo zweryfikowany; pełnego B nie uruchomiono.

Dokładna baza PR #150: `5558a30c8864199fb1c7abc18852ca27f4d29d9b`, nowa gałąź `experiment/efficient-contingency-152`. Bez merge. Nowy wariant domyślnie OFF.157 zamrożonych źródeł oraz149 historycznych drzew Git pozostają niezmienione (`source-proof.json`). Jedyna zmiana projektu wspólnego to dostęp adaptera do istniejących wewnętrznych wrapperów prognozy.

## Dwa oddzielne wyniki

1. **Optymalizacja dokładna:** wszystkie173 komendy,3468 zapisów natywnych do34,6s, aktywacje, kolejki oraz kompletne nominalne prognozy i dziewięć certyfikowanych rezerw mają zgodne bity/digests. Sterownik kosztował 219.047 zamiast276,953 CPU-s: oszczędność 20.9%. Hasz kanonicznych zapisów: `85e9d2d71f65f8d2c2085a62607fc061dc5b56361abcca210b901bfaddd2324a`. Po tym prefiksie wariant dokładny dojechał do38,6s i dwóch okrążeń, po czym odmówił certyfikatu.

2. **Zmiana algorytmiczna:** zachowanie polityk przy zmianie celu, wcześniejsze uzupełnianie nowego ogona, propozycje z bieżącego drzewa i lokalne naprawy. Ten wariant ukończył pełny A. Pierwsza różna komenda względem151 występuje w12.4s; w173 odpowiadających sobie epokach jest62 zgodnych i111 różnych komend. Po pierwszej różnicy porównujemy już inne stany natywne, więc nie nazywamy tego poprawą dokładną. Wspólny kontrprzykład38,6s został osobno odtworzony z własnej poprzedniej polityki i uzyskał pełny nowy certyfikat (`repair38.6-v7.json.gz`).

Digest dokładnego porównania obejmuje wszystkie prymitywne pola nominalnego i certyfikowanych końcowych tapes: stany, kolejki, punkty, skręt, koło, wał, clearance, komendy, cele, arrival i safety. Nie obejmuje CPU, nazw źródeł propozycji ani liczników. Nie twierdzimy, że zapisano osobno każdy odrzucony wewnętrzny kandydat K1. Test A/B dodatkowo porównuje wszystkie kandydaty, oceny i punkty pojedynczych pełnych K1 z zamrożonym predyktorem.

## Skąd koszt i co zmieniono

K1 dominował, ponieważ brak zaakceptowanej kontynuacji uruchamiał generowanie całego odległego manewru: każdy krok0,2s wykonywał siatkę K1 przez0,9s, a podobne, lecz różne gałęzie powtarzały ten proces. W decyzji29,8s było699 pełnych K1. Przy zmianie celu151 porzucał poprzednią politykę, choć jej safetyGoal nadal obejmował użyteczną przyszłość. Samo zachowanie jej do ostatniej komendy też nie wystarczało: nowy manewr był planowany za późno względem0,24s reakcji.

- Dokładny DAG współdzieli identyczne stany/kolejki i całe rollouty w jednej decyzji. Nowa komenda nie wpływa na fizykę całego kroku0,2s; zależy od niej dopiero zwracana kolejka. Wspólny prefiks K1 do0,24s istniał już w151 i został zachowany.
- Oddzielny DAG preflight przelicza każdy unikalny prefiks od świeżego stanu zamrożonym predyktorem148/149, oryginalnym solverem koła/sprzęgła i osobnym BVH. Nie korzysta z wyników wyszukiwania ani dawnej legalności. Porównuje wynik z wyszukiwaniem; test uszkodzenia cache powoduje odmowę. Oryginalny, wcześniej udowodniony skrót połączonego obrysu usuwa zbędne sprawdzanie znaków wyłącznie w jego dziedzinie; poza nią wraca pełna geometria.
- W wyszukiwaniu przeniesiono niezmienne współczynniki solvera i wykorzystano rzeczywistą odległość do wcześniej znalezionego segmentu jako górną granicę dla pełnego BVH. Nie zmienia to prymitywów ani wyniku minimum. Samo przeniesienie współczynników nie dało istotnej poprawy.
- Pełne ślady H09 nie wypierają już użytecznych kroków0,2s z cache; kompletny wynik rankingu K1 ma własny cache. Pominięto wyszukiwanie FirstFailure dla już legalnego tape. Archiwum przechowuje wspólne wejścia/polityki zamiast powielonych gęstych drzew.
- Wariant algorytmiczny zachowuje przy zmianie celu użyteczny prefiks poprzedniej polityki i zaczyna uzupełnianie przyszłości jeden istniejący horyzont K1 wcześniej (`ceil(0,9/0,2)=5` epok). Każdy zmieniony odcinek jest propagowany i certyfikowany od nowej obserwacji. Legalne propozycje bieżącego drzewa mogą zasilać inne gałęzie; nie przenoszą certyfikatu. Lokalna zmiana komendy najpierw próbuje naprawić istniejącą politykę. Sześć prób naprawy obejmuje różne wcześniejsze epoki, zamiast zużyć je wszystkie w jednej.

Nie zmieniono fizyki, wag, siatki ani horyzontu wybranego K1, terminalnych przekrojów, limitu30 CPU-s/decision,32768 prognoz,2 mln kontaktów, głębokości2 ani wszystkich trzech znaków obserwacji. Nadal jest13 logicznych węzłów i9 rezerw. Poza identycznymi stanami nie wykazano równoważności pozwalającej usunąć scenariusze. Odległość od granic i β/yaw nie stanowią dowodu takiej równoważności; nie użyto arbitralnego przerzedzania ryzykownych gałęzi. Historyczny cache jest propozycją, nigdy dowodem legalności nowego stanu.

## CPU: porównanie dokładne0–34,6s

| Kategoria CPU-s | 151 | 152 dokładny |
|---|---|---|
| Pełne K1, wraz z ich propagacją | 157.156 | 142.062 |
| Kontynuacje warunkowe | 38.484 | 36.141 |
| Niezależny preflight | 44.172 | 29.891 |
| Pozostała propagacja, obsługa, digest i zapis | 37.141 | 10.953 |
| Łączny sterownik | 276.953 | 219.047 |


K1 zawiera swoje propagacje, więc nie należy dodawać ich ponownie. Pozostała propagacja rolloutów jest mierzona w recertification/conditional_continuations. Rozdzielczość Windows15,625ms powoduje zera krótkich faz. Zera nie oznaczają pominięcia testów.

## Pełny wynik algorytmiczny i liczniki

| Miara | 151, nieukończone34,6s | 152 algorytmiczny, pełne3 okrążenia |
|---|---|---|
| CPU K1 | 157.156 | 56.953 |
| CPU kontynuacji | 38.484 | 44.547 |
| CPU recertification | 3.578 | 5.312 |
| CPU preflight | 44.172 | 38.547 |
| CPU planowania / sterownika | 248.562 /276,953 | 150.609 /153.891 |
| Pełne K1 | 5847 | 2273 |
| Zakończone prognozy | 419288 | 395430 |
| Kontakty fizyczne i feedback, dolna granica | 16290852 | 11364290 |


Te kolumny mają różną trajektorię i długość; są wynikiem algorytmicznym. Dokładne liczniki każdej decyzji są w `decision-counts.csv` i `analysis.json.prefix`. Liczniki completed oryginalnych fallbacków nie obejmują części wywołania przerwanego wyjątkiem; zachowano fault counts i rzeczywisty CPU, zamiast dopisywać fikcyjne kontakty. SearchWork liczy także kontakty wykonane przed błędem w nowym adapterze.

| Na decyzję: średnia / min / max | 151 prefiks | 152 dokładny prefiks | 152 algorytmiczny pełny |
|---|---|---|---|
| k1 | 33.80 / 0 / 760 | 33.80 / 0 / 760 | 8.24 / 0 / 386 |
| forecasts | 2423.63 / 755 / 26423 | 2408.62 / 746 / 26392 | 1432.72 / 26 / 13386 |
| contacts | 94155.85 / 16440 / 1498991 | 93854.80 / 16260 / 1498369 | 41174.96 / 566 / 759460 |
| leaves | 9.00 / 9 / 9 | 9.00 / 9 / 9 | 9.00 / 9 / 9 |
| rejected | 0.09 / 0 / 3 | 0.09 / 0 / 3 | 0.01 / 0 / 1 |


Dla pełnego A: CPU decyzji p50=0.328, p95=2.297, max=9.984 s. Cel100–140 CPU-s pozostaje niespełniony o13.891 CPU-s względem górnej granicy. Pozostałe fazy preflight i kontynuacji kosztują razem83.094 CPU-s. Szybszy wybór dalszych propozycji lub inkrementalny dowód byłby kolejną zmianą algorytmiczną; nie wykazano dolnej granicy ani niemożliwości celu.

## Pięć bramek

| Brama | Wynik | Zakres |
|---|---|---|
| 1: trudny1s A/B | PASS dla dokładnego; PARTIAL dla nowego B | Dokładny A/B: certyfikat bitowo zgodny151 i natywny pierwszy łuk. Nowy: A natywny pierwszy łuk; B trzy nowe certyfikowane decyzje od1s, potem CPU stop1,6s. Nie ukończył nowej diagnostyki B. |
| 2: start i dwa łuki pierwszego okrążenia | PASS A | Oba warianty A wykonały natywnie legalny start i pełne pierwsze okrążenie. |
| 3: natywne34,6s bez wzrostu CPU | PASS | Dokładny prefiks identyczny:219,047≤276,953 CPU-s. Nowy wariant również przekroczył34,6s legalnie; jego inna trajektoria ma osobny wynik. |
| 4: pełne trzy okrążenia A | PASS | 55.167079s;276 decyzji z pełnym niezależnym preflight. |
| 5: pełny B | NIE URUCHOMIONO | Niepełna nowa diagnostyka B i niewystarczająca pozostała rezerwa na uzasadnioną pełną próbę oraz końcowy eksport. |


Brama1 nowego B nie została po cichu uznana za zaliczoną. Pełne A uruchomiono po analizie przerwania diagnostyki B: przyczyną była wspólna rezerwacja CPU, nie odrzucenie fizyczne A. A miało nową natywną walidację trudnego pierwszego łuku, a zapis38,6s pełny certyfikat po poprawce. B pozostaje ograniczeniem końcowego wyniku.

## Pierwszy nowy blokujący mechanizm i wyniki negatywne

Dokładny A odmówił w38,6s po dwóch legalnych okrążeniach, bez kontaktu/nielegalnej wykonanej komendy i bez CPU stop. Nie udało się domknąć skończonego drzewa przyszłych obserwacji: pierwsze niedomknięte poddrzewo dotyczy39,0s/sign+1; dalsze rezerwy traciły zapas obrysu lub β≤45°. Koszt ostatniej decyzji24,516 CPU-s, w tym K124,047. To nie dowodzi braku fizycznie wykonalnej polityki. Zapis `abstain.json.gz` zachowuje obserwację, próby, pierwsze błędy i odrzucone drzewa. Przy zmianie celu poprzedni manewr obejmował jeszcze użyteczną przyszłość do828,946875m; pełne porzucenie albo późne dopisanie komend powodowały kosztowną regenerację. Uzupełnienie ogona przed końcem poprzedniego planu dało nowy skończony certyfikat, a pełna nowa trajektoria ominęła tę utratę dostępności.

| Wariant/test | Wynik |
|---|---|
| Przeniesienie współczynników | Bity zachowane; brak istotnego zysku CPU na pięciu decyzjach. |
| Słownik fizycznych podkroków | Mniej niż5% trafień; koszt haszowania zjadał oszczędność. Nieaktywny w wybranym K1. |
| Podpowiedź najbliższego fragmentu projekcji | Bity zachowane; brak istotnego zysku. Usunięta z wybranego kodu. |
| Naprawy38,6s v1–v3 | Samo dzielenie bieżących propozycji / surowych kontroli / kolejności sześciu napraw nie dało certyfikatu. |
| Ranking0,44s v4 | Około1,766 CPU-s decyzji; brak legalnej rezerwy. Odrzucony. Wybrany przejazd nie używa shortlisty. |
| Zachowanie starego ogona bez wyprzedzenia v5 | Nadal brak certyfikatu38,6s. |
| Wyprzedzające uzupełnienie v7 | Certyfikat38,6s i późniejszy pełny natywny A. |
| Diagnostyczne próby B | Późniejsze CPU stopy są zachowane; nie stały się pełnymi okrążeniami. |


## Jakość jazdy i energia

Niezawodność poprawiła się: pełny autonomiczny A zamiast przerwania. Sportowo czas jest gorszy o1.177313s (2.18%) względem53,989766s starego A. Nie strojono fizyki ani wag.

| Próba | Okrążenie1 s | Okrążenie2 s | Okrążenie3 s |
|---|---|---|---|
| baseline149-final148 | 19.540281 | 17.203391 | 17.246094 |
| efficient152-A-portfolio-v1 | 19.538742 | 17.503392 | 18.124944 |


| Miara | Starszy A | Nowy A |
|---|---|---|
| speed_min_kph | 0.000 | 0.000 |
| speed_max_kph | 86.587 | 86.169 |
| closed_gas_s | 23.840 | 26.840 |
| path_m | 975.622 | 981.730 |
| minimum_gap_m | 0.185 | 0.165 |
| minimum_sweep_m | 0.106 | 0.085 |
| maximum_beta_deg | 29.455 | 18.846 |


Minimalna prędkość zawiera zatrzymany start. Długość drogi i sumy energii odnoszą się do zaakceptowanego ostatniego kroku natywnego; czas mety jest interpolowany wewnątrz tego kroku.

| Łuk | Czas s | Δ stary A s | V wej/min/wyj km/h | Droga m | Gaz0 s | Wew/zew min m | CPU decyzji w łuku s |
|---|---|---|---|---|---|---|---|
| 1.1 | 5.822031 | 0.000323 | 65.609/55.198/65.246 | 97.721 | 2.364 | 0.209/8.772 | 12.500 |
| 1.2 | 5.550827 | -0.002177 | 80.711/56.151/57.657 | 100.077 | 4.023 | 0.206/6.869 | 18.062 |
| 2.1 | 5.604299 | 0.050581 | 78.062/56.114/62.324 | 102.422 | 4.050 | 0.165/2.706 | 23.719 |
| 2.2 | 5.738579 | 0.133077 | 81.135/55.722/58.387 | 102.183 | 4.233 | 0.310/4.059 | 10.578 |
| 3.1 | 6.356991 | 0.476210 | 81.350/52.603/58.771 | 104.218 | 4.237 | 0.248/2.115 | 18.203 |
| 3.2 | 5.767208 | 0.301642 | 80.382/53.661/59.436 | 100.236 | 4.241 | 0.209/5.522 | 3.219 |


| Energia/strata J | Starszy A | Nowy A |
|---|---|---|
| front_slip_loss_J | 62671.321 | 60026.446 |
| rear_lateral_slip_loss_J | 87746.778 | 79156.530 |
| rear_long_slip_loss_J | 112175.047 | 104647.174 |
| clutch_heat_J | 2232.937 | 2227.124 |
| transmission_loss_J | 27006.972 | 25469.218 |
| bearing_loss_J | 4621.130 | 4449.846 |
| resistance_J | -109741.250 | -108216.565 |
| total_energy_residual_J | -0.316 | 0.361 |


`resistance_J` jest ujemną pracą oporów; pozostałe straty mają dodatni znak. Nie sumujemy składowych translacji i strat jako niezależnych strat. `analysis.json` zawiera energię każdego łuku, a `segments.csv` koszt CPU wszystkich pięciu odcinków każdego okrążenia. Koszt łuku obejmuje decyzje podjęte w nim; przygotowanie na poprzedniej prostej należy do tej prostej.

## Czterech zawodników

Czyste pomnożenie zmierzonego sterownika A daje około615.562 CPU-s na bieg czterech zawodników, czyli11.16 CPU-s na sekundę jazdy. To przybliżenie bez sprzężeń kontaktowych i wspólnych scenariuszy, nie pomiar ani gotowość. Rzeczywista certyfikacja interakcji może kosztować więcej; liniowe mnożenie nie dowodzi bezpieczeństwa czterech motocykli. Nie wykonano nowego biegu czteroosobowego.

## Budżet, dane i reprodukcja

Dotychczasowy ledger przed zakończeniem tego eksportu: 1049.328 CPU-s, 7 nowych światów. Ostateczna suma jest w `budget.json`; limit1200 CPU-s /15 światów nie został zwiększony. Wszystkie nieudane próby i analiza danych są liczone. Kompilacja, lokalne testy i Windows CI są poza budżetem eksperymentów.

Instrukcje i jawne flagi: [README narzędzia](../../../tools/efficient-contingency/README.md). Wybrany pełny wariant: `--native-portfolio-a152`. Wariant dokładny: `--native-a152`. Replay: `tools/replay-viewer/?case=efficient152`; domyślny zapis to pełny nowy A. `native/` zachowuje gęste stany, siły, energię, kolejki i aktywacje. `policies/` zawiera komplet logicznych węzłów i odrzuconych drzew oraz współdzielone wejścia wszystkich rezerw. Digest gęstego review można odtworzyć; `RepairProbe152.Restore` odtworzył poprzednie38,4s i sprawdził jego pełny digest przed testem38,6s. Nie zduplikowano starych wielkich drzew. Decymowane ślady w viewerze służą wyłącznie prezentacji.

Źródła wybranego sterownika mają hashe z chwili uruchomienia; test porównuje je z finalnym kodem. Wczesne negatywne próby mają zachowane wyniki/wejścia, lecz nie każda ma pełną osobną migawkę źródeł; ich timing nie jest deklarowany jako samodzielnie odtwarzalny z finalnych flag. Dane historyczne są przypięte do dokładnej bazy.

Certyfikat pozostaje ograniczony do skończonych, skorelowanych scenariuszy151. Nie jest globalną gwarancją wszystkich zakłóceń ani dowodem natywnego Box2D dla każdego analitycznego liścia. Natywny świat wykonuje i sprawdza rzeczywisty przejazd.

Lokalna walidacja: `validation.json`. C#, Python, Node, kontrola wygenerowanych adapterów i przypisanie wszystkich testów do shardów. Windows CI ma być oceniane dla dokładnego HEAD nowego Draft PR; przebiegi historyczne i czterech zawodników są w Draft odkrywane, lecz nie wykonywane zgodnie z istniejącą polityką CI. Nie przedstawiamy tego jako testu czterech zawodników.

Lokalnie: build bez ostrzeżeń;2507 C# core/trajectory,318 Python,7 testów polityki CI i89 Node — PASS. Odkryto3028 C# przypadków; pozostałe shardy mają jawny status tylko discovery. Nowe dowody zajmują około31MB, zamiast ponownego zapisywania wielkich historycznych drzew.
