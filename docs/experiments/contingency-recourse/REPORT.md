# Badanie #151 — Contingency Planning i Robust Recourse

**PARTIAL — no new three-lap completion**. Baza to dokładny HEAD PR #149 `5bc8575f60d9fa1116e45dad85c08786158faccd`. Nowy sterownik jest domyślnie OFF. Fizyka A/B, geometria, silnik, opony i wszystkie historyczne wyniki pozostały niezmienione. Bez merge.

## Osiem odpowiedzi

1. #150 odrzucał **odległą prognozę**, nie bieżący stan0,4s. Trzy nominalne plany były legalne; dwa znaki historycznej delty destabilizowały ich dalszy obrys. Bieżąca komenda była wspólna.

2. Test jednego wielosekundowego kontraktu dla wszystkich odchyleń był nadmiernie silnym warunkiem dostępności działania teraz. Nie dowodził fizycznej niemożliwości. Dwa stare warianty retained/fresh również nie stanowiły pełnej przestrzeni przyszłych reakcji.

3. Tak: zapis `gate1-v4.json.gz` ma13 węzłów, dziewięć legalnych rezerw i różne komendy gazu dla różnych obserwacji0,8s. Nowy test sprawdza kwantyfikatory **istnieje komenda teraz, dla każdej badanej przyszłej obserwacji istnieje kontynuacja**.

4. Każda gałąź sprawdza pełny obrys, sweep, β≤45°, napęd, opony, skręt i kolejkę. Liść wymaga pełnego legalnego manewru rezerwowego przez następny łuk do wspólnego przekroju geometrycznego. Niezależny preflight odtwarza wszystkie krawędzie i rezerwy bez cache. To skończony certyfikat modelowy; brak globalnej gwarancji ani dowodu dla wszystkich ciągłych zakłóceń.

5. Nowa walidacja dokładnego1s: A: certyfikat; B: certyfikat. Jest to ponowna certyfikacja zapisanych stanów z własną historią polityki; nowego natywnego odzyskania1s nie utożsamiamy z historycznym sukcesem #150.

6. Trzy pełne okrążenia od zera: A: nie ukończono / nie uruchomiono; B: nie ukończono / nie uruchomiono.

7. Krótszy legalny czas całego dystansu: A: N/D; B: N/D. Lokalne czasy nie zastępują wyniku całego biegu.

8. CPU i pełne niepowodzenia są w tabelach oraz `budget.json`. Główny koszt to pełne generowanie K1. Współdzielone prefiksy, zachowane rodziny kontynuacji i dokładny invariant prostoliniowy ograniczyły koszt, lecz nie wykazano kosztu zbliżonego do83 CPU-s na pełnym A ani gotowości dla czterech zawodników.

## Diagnoza0,4s — dokładny pierwszy odrzucony interwał/punkt

| Stan | Scenariusz | Polityka | Pierwszy moment s | Przyczyna |
|---|---|---|---|---|
| current-0.4 | 0 | retained | brak | pełna legalna prognoza |
| current-0.4 | 0 | fresh | brak | pełna legalna prognoza |
| current-0.4 | 1 | retained | 13.140000 | uncertified swept interval |
| current-0.4 | 1 | fresh | 13.140000 | footprint reserve |
| current-0.4 | -1 | retained | 15.750000 | footprint reserve |
| current-0.4 | -1 | fresh | 15.750000 | footprint reserve |
| next-0.6 | 0 | retained | brak | pełna legalna prognoza |
| next-0.6 | 0 | fresh | brak | pełna legalna prognoza |
| next-0.6 | 1 | retained | 13.050000 | footprint reserve |
| next-0.6 | 1 | fresh | 13.040000 | footprint reserve |
| next-0.6 | -1 | retained | 15.480000 | footprint reserve |
| next-0.6 | -1 | fresh | 15.450000 | footprint reserve |


Moment sweep dotyczy interwału kończącego się w podanym punkcie, a footprint/β pierwszego odrzuconego punktu. Dalsze minimum ujemne z całej prognozy nie jest chwilą pierwszej utraty legalności. Historyczne ograniczenie do dwóch polityk nie dowodziło wyczerpania dostępnych działań.

## Trzy horyzonty i ograniczona gwarancja

- **A — nieodwracalny:** bieżące wykonanie i kolejka są wspólne. Decyzja co0,2s, aktywacja po0,24s; korekta z następnej obserwacji zaczyna działać najwcześniej po0,44s. Trzy cykle w drzewie i niezależne rezerwy obejmują ten interwał.
- **B — kontrolowana kontynuacja:** trzy znaki innowacji na dwóch następnych obserwacjach dają dziewięć liści. Każdy ma osobną wykonalną politykę/rezerwę, a przy rzeczywistej obserwacji drzewo powstaje i jest certyfikowane ponownie. Brak certyfikatu oznacza abstain.
- **C — sportowy:** nominalny czas do tego samego geometrycznego celu i następnego manewru dostarcza propozycji oraz rezerwy. Odległa nielegalność jednej propozycji uruchamia alternatywy; nie jest automatycznie uznawana za kolizję teraz. Po uzyskaniu stabilnego pełnego biegu można rozszerzyć optymalizację czasu; obecna selekcja preferuje pierwszą kompletną wykonalną rodzinę.

Gałęzie korzystają z niezmienionego analitycznego predyktora held-wrench #148/#149, zgodnego z równaniami kroków Box2D, ale różniącego się zaokrągleniami. **Nie twierdzimy, że każdy przewidywany liść uruchamia osobny natywny świat Box2D.** Natywny Box2D wykonuje każdy rzeczywisty przejazd i zachowuje wszystkie oryginalne kontrole. To pozostaje ograniczeniem gwarancji.

Niepewność: jawne, skorelowane znaki zaokrąglonych ponad2× maksimów jednego kroku z wielu porównań identycznych komend: pozycja0,00015m, prędkość0,00075m/s, heading0,00012rad, yaw0,00055rad/s, koło0,0025rad/s, wał0,023rad/s. Nie jest to kompletna globalna granica błędu ani pełne niezależne pudełko. Na dokładnym invariantnym podzbiorze ruchu wzdłuż osi pozostały błędy podłużne/koła/wału; kierunek nie jest perturbowany, ponieważ siły i momenty poprzeczne pozostają dokładnie zerowe. Każdy niezerowy skręt, yaw lub ruch poprzeczny przywraca pełne scenariusze. Potwierdzenie:39/39 kwalifikujących się natywnych par,0 naruszeń.

## Bramy i wszystkie próby natywne

| Próba | Brama | Wynik | Okr. | Koniec s | Pełny czas3okr s | CPU sterownika s | CPU całej próby s | Min sweep m |
|---|---|---|---|---|---|---|---|---|
| contingency151-gate2-A-v1 | 2 | technical invariant: STUDY151_ABSTAIN: abstain: Study151 deterministic forecast/contact or30 CPU-s review cap | 0 | 0.600000 | N/D | 84.891 | 85.250 | 4.089113 |
| contingency151-gate2-A-v2 | 2 | PASS bramki | 0 | 9.600000 | N/D | 193.172 | 195.000 | 0.117625 |
| contingency151-gate2-A-v3 | 2 | PASS bramki | 0 | 9.600000 | N/D | 97.781 | 99.719 | 0.117625 |
| contingency151-gate3-A-v1 | 3 | PASS bramki | 1 | 19.540000 | N/D | 104.250 | 107.875 | 0.099864 |
| contingency151-gate4-A-v1 | 4 | technical invariant: Crank/clutch passivity or energy identity failed. | 1 | 29.800000 | N/D | 217.031 | 222.703 | 0.099864 |
| contingency151-gate4-A-v2 | 4 | technical invariant: STUDY151_ABSTAIN: abstain: STUDY151_CPU_STOP | 1 | 34.600000 | N/D | 276.953 | 282.344 | 0.094303 |


Bramy nieuruchomione pozostają N/D. Pełny B wymaga ukończonego A i pozytywnych odłożonych prób; budżet nie stanowi zgody na pominięcie tej zależności. Osobne zatrzymanie `GATE2_TARGET_REACHED` oznacza koniec pełnego pierwszego łuku, nie pełny wyścig.

### Przyczyna przerwania pierwszego pełnego A w29,8s

Błąd dotyczył **prognozowanego** K1 w42,8s: propozycje gaz0 traciły pasywność/zgodność energii po ostatnim zaakceptowanym punkcie43,67s, w następnym kroku około43,68s. Wyjątek jednej propozycji przerywał cały wybór. Żaden taki ruch nie został wykonany. Poprawka wyklucza dokładnie rozpoznane niepoprawne prognozy i ponownie ocenia pozostałe propozycje tymi samymi równaniami i guardami; nie wyłącza kontroli energii. Nowy certyfikat zapisanego29,8s: True, odrzucone zdarzenia: 10. Stary plik abstain zawierał ostatni review29,6s; actual failed observation29,8s jest zapisany osobno. Po poprawce `Last` jest zerowany i powstaje dla bieżącego wywołania.

## Nominalne propozycje i warunkowe kontynuacje

| Próba | Decyzje | Nominalne próby/legalne | Certyfikowane węzły | Certyfikowane rezerwy | Zgodna następna komenda wcześniejszego planu |
|---|---|---|---|---|---|
| contingency151-gate2-A-v1 | 3 | N/D/N/D | 39 | 27 | 0/0 |
| contingency151-gate2-A-v2 | 48 | N/D/N/D | 624 | 432 | 0/0 |
| contingency151-gate2-A-v3 | 48 | N/D/N/D | 624 | 432 | 0/0 |
| contingency151-gate3-A-v1 | 98 | N/D/N/D | 1274 | 882 | 0/0 |
| contingency151-gate4-A-v1 | 149 | N/D/N/D | 1937 | 1341 | 0/0 |
| contingency151-gate4-A-v2 | 173 | 4895/3821 | 2249 | 1557 | 14/172 |


Dla starszych prób brak liczby nominalnych propozycji w kompaktowym journalu oznacza N/D, a nie zero. Pełne alternatywy są w zapisanych drzewach. Wszystkie drzewa przeszły fizyczny uncached preflight w chwili wykonania; eksport ponownie sprawdza hashe journalu i natywne komendy/aktywacje. Starsze liczby13/9 wynikają z kompletnego stałego drzewa depth2. Tapes nie były powtórnie propagowane podczas eksportu.

## Błąd predykcji przy identycznych komendach i tym samym czasie

| Próba | Horyzont s | N | XY max m | V max m/s | Heading max rad | Yaw max rad/s | Koło max rad/s | Wał max rad/s | Skręt max rad |
|---|---|---|---|---|---|---|---|---|---|
| robust150-exact-recovery-A | 0.2 | 77 | 0.00004594 | 0.00031796 | 0.00004423 | 0.00014946 | 0.00085372 | 0.00766866 | 0.00000000 |
| robust150-exact-recovery-A | 1 | 2 | 0.00008470 | 0.00021119 | 0.00004256 | 0.00001872 | 0.00068026 | 0.00611591 | 0.00000000 |
| robust150-exact-recovery-A | 2 | 2 | 0.00047768 | 0.00074613 | 0.00006223 | 0.00008017 | 0.00052192 | 0.00470175 | 0.00000000 |
| robust150-exact-recovery-A | 4 | 2 | 0.00858030 | 0.01092629 | 0.00092959 | 0.00150433 | 0.00513998 | 0.04605579 | 0.00000000 |
| robust150-exact-recovery-A | 10 | 1 | 5.28999040 | 2.89289740 | 0.07611477 | 0.26124334 | 0.84289423 | 7.62277711 | 0.00000000 |
| robust150-exact-recovery-B | 0.2 | 77 | 0.00006867 | 0.00029809 | 0.00004339 | 0.00013862 | 0.00083396 | 0.00749082 | 0.00000000 |
| robust150-exact-recovery-B | 1 | 2 | 0.00009567 | 0.00021119 | 0.00004220 | 0.00001848 | 0.00067739 | 0.00609012 | 0.00000000 |
| robust150-exact-recovery-B | 2 | 2 | 0.00048972 | 0.00074963 | 0.00006199 | 0.00007746 | 0.00053472 | 0.00481762 | 0.00000000 |
| robust150-exact-recovery-B | 4 | 2 | 0.00829639 | 0.01048141 | 0.00088763 | 0.00156552 | 0.00506281 | 0.04537124 | 0.00000000 |
| robust150-exact-recovery-B | 10 | 1 | 4.99545700 | 2.76870580 | 0.06621087 | 0.24757552 | 1.12551476 | 10.16419397 | 0.00000000 |
| robust150-prefix-B-45 | 0.2 | 41 | 0.00002297 | 0.00024065 | 0.00005144 | 0.00002953 | 0.00021044 | 0.00188697 | 0.00000000 |
| robust150-prefix-B-45 | 1 | 1 | 0.00003282 | 0.00011560 | 0.00000739 | 0.00000118 | 0.00010172 | 0.00091493 | 0.00000000 |
| robust150-prefix-B-45 | 2 | 1 | 0.00017145 | 0.00007629 | 0.00000238 | 0.00001991 | 0.00017153 | 0.00152988 | 0.00000000 |
| robust150-prefix-B-45 | 4 | 1 | 0.00073413 | 0.00090923 | 0.00009263 | 0.00010017 | 0.00097580 | 0.00879910 | 0.00000000 |
| robust150-tube-full-A | 0.2 | 1 | 0.00000000 | 0.00000000 | 0.00000000 | 0.00000000 | 0.00000000 | 0.00000000 | 0.00000000 |
| baseline149-final148 | 0.2 | 269 | 0.00003825 | 0.00032578 | 0.00004798 | 0.00024256 | 0.00100977 | 0.00907639 | 0.00000000 |
| baseline149-final148 | 1 | 7 | 0.00048667 | 0.00126044 | 0.00010467 | 0.00012463 | 0.00114820 | 0.01032456 | 0.00000000 |
| baseline149-final148 | 2 | 7 | 0.00295293 | 0.00338050 | 0.00018263 | 0.00008085 | 0.00245668 | 0.02213122 | 0.00000000 |
| baseline149-final148 | 4 | 7 | 0.01132032 | 0.00468429 | 0.00020075 | 0.00070110 | 0.00430645 | 0.03871720 | 0.00000000 |
| baseline149-final148 | 10 | 6 | 0.08626884 | 0.02820605 | 0.00052810 | 0.00379789 | 0.00958674 | 0.08628053 | 0.00000000 |
| separated149-autonomous | 0.2 | 271 | 0.00005344 | 0.00033493 | 0.00004798 | 0.00004035 | 0.00051691 | 0.00467351 | 0.00000000 |
| separated149-autonomous | 1 | 7 | 0.00049878 | 0.00128745 | 0.00010681 | 0.00012729 | 0.00062693 | 0.00564082 | 0.00000000 |
| separated149-autonomous | 2 | 7 | 0.00297553 | 0.00343801 | 0.00018573 | 0.00008813 | 0.00151346 | 0.01362713 | 0.00000000 |
| separated149-autonomous | 4 | 7 | 0.01148200 | 0.00480593 | 0.00020647 | 0.00006190 | 0.00162618 | 0.01463928 | 0.00000000 |
| separated149-autonomous | 10 | 6 | 0.02883753 | 0.00833584 | 0.00061408 | 0.00051478 | 0.00855880 | 0.07693808 | 0.00000000 |


Raportowane wcześniej≈0,26m było porównaniem do decymowanego punktu prognozy przesuniętego o0,01s. Rzeczywiste porównanie0,2s ma max≈0,000069m. Nowa decyzja podjęta0,2s później aktywuje się dopiero w0,44s, więc nie wyjaśnia błędu następnej obserwacji0,2s. Dla0,6s oddzielono w `new-native-errors.json` predykcję identycznych faktycznych komend, predykcję wcześniejszej sekwencji oraz wkład nowej decyzji. Norm tych różnic nie dodaje się liniowo. Kolejka, aktywny gaz, skręt, wał i koło są porównywane osobno. Ścisłe porównanie znaczników czasu w v1 zgłaszało różnice ostatnich bitów po długiej sumie kroków; v2 zachowuje zarówno strict equality, jak zgodność komend i błąd timestampów z tolerancją1e−8s.

### Nowe natywne próby #151

| Próba | Pary0,2s | XY m | V m/s | Heading rad | Yaw rad/s | Koło rad/s | Wał rad/s | Skręt rad | Niezgodne kolejki |
|---|---|---|---|---|---|---|---|---|---|
| contingency151-gate2-A-v1 | 2 | 0.00000001 | 0.00000024 | 0.00000000 | 0.00000000 | 0.00000022 | 0.00000000 | 0.00000000 | 0 |
| contingency151-gate2-A-v2 | 47 | 0.00002320 | 0.00009489 | 0.00003898 | 0.00002834 | 0.00035615 | 0.00319239 | 0.00000000 | 0 |
| contingency151-gate2-A-v3 | 47 | 0.00002320 | 0.00009489 | 0.00003898 | 0.00002834 | 0.00035615 | 0.00319239 | 0.00000000 | 0 |
| contingency151-gate3-A-v1 | 97 | 0.00004641 | 0.00033137 | 0.00004816 | 0.00002834 | 0.00035615 | 0.00319239 | 0.00000000 | 0 |
| contingency151-gate4-A-v1 | 148 | 0.00004641 | 0.00033137 | 0.00004840 | 0.00007445 | 0.00035615 | 0.00319239 | 0.00000000 | 0 |
| contingency151-gate4-A-v2 | 172 | 0.00004641 | 0.00033137 | 0.00005186 | 0.00007460 | 0.00044873 | 0.00403618 | 0.00000000 | 0 |


| Próba | Pary0,6s | Model / faktyczne komendy: XY m | Poprzedni plan: XY m | Wkład nowej decyzji: XY m | Zmienione sekwencje |
|---|---|---|---|---|---|
| contingency151-gate4-A-v2 | 34 | 0.00014667 | 0.00014333 | 0.00000688 | 31 |


## Odłożone i historyczne stany

| Model | Czas s | Rola | Nowy certyfikat | CPU s | Odmowa/ograniczenie |
|---|---|---|---|---|---|
| A | 1 | historical exact1s | True | 12.000 | brak odmowy; tylko zapisany stan |
| B | 1 | historical exact1s | True | 15.062 | brak odmowy; tylko zapisany stan |
| A | 22.6 | historical22.6 | True | 6.469 | brak odmowy; tylko zapisany stan |
| B | 22.6 | historical22.6 | True | 10.906 | brak odmowy; tylko zapisany stan |
| A | 28.4 | corner2.2 | True | 24.188 | brak odmowy; tylko zapisany stan |
| B | 28.4 | corner2.2 | True | 3.984 | brak odmowy; tylko zapisany stan |
| B | 45.8 | last B corner | None | N/D | not run: reserved native-race CPU budget |
| A | 12.8 | held for151; no tuning | None | N/D | not run: reserved native-race CPU budget |
| B | 39.4 | held for151; no tuning | None | N/D | not run: reserved native-race CPU budget |


Wcześniejsze alternatywy0,0/0,2s nie mają nowej kompletnej tabeli kontrfaktycznej w tym budżecie; nie twierdzimy, że udowodniono zapobieżenie każdemu późniejszemu stanowi przez inne wcześniejsze decyzje. Generator oferuje wszystkie kanały gazu, symetryczne zmiany skrętu i warunkowe alternatywy; brak wyczerpującego dowodu przestrzeni działań.

## Sześć łuków, gaz, prędkość i linia

| Próba | Łuk | Czas s | V wej/min/wyj km/h | Droga m | Gaz0/część/1 s | β max° | Offset min/max m | Gap wew/zew min m | CPU w łuku s |
|---|---|---|---|---|---|---|---|---|---|
| baseline149-final148 | 1.1 | 5.821708 | 65.609/55.197/65.247 | 97.715 | 2.364/3.457/0.000 | 10.613 | -0.378/1.794 | 0.205/8.772 | 2.188 |
| baseline149-final148 | 1.2 | 5.553004 | 80.719/56.149/57.667 | 100.119 | 4.023/1.530/0.000 | 13.338 | -0.302/3.576 | 0.227/6.855 | 2.000 |
| baseline149-final148 | 2.1 | 5.553718 | 78.057/59.170/66.379 | 102.046 | 3.049/2.305/0.200 | 12.668 | -0.297/7.795 | 0.246/2.706 | 43.516 |
| baseline149-final148 | 2.2 | 5.605502 | 80.549/53.960/58.342 | 99.873 | 3.595/1.410/0.600 | 29.455 | -0.394/4.847 | 0.185/5.560 | 12.000 |
| baseline149-final148 | 3.1 | 5.880781 | 81.648/51.595/59.360 | 100.625 | 4.310/1.400/0.171 | 15.444 | -0.269/4.558 | 0.318/5.854 | 1.828 |
| baseline149-final148 | 3.2 | 5.465566 | 80.660/57.805/63.929 | 100.272 | 3.426/1.600/0.440 | 18.932 | -0.368/4.525 | 0.206/5.935 | 1.266 |
| separated149-autonomous | 1.1 | 5.821702 | 65.609/55.197/65.247 | 97.714 | 2.364/3.457/0.000 | 10.613 | -0.378/1.794 | 0.204/8.772 | 2.594 |
| separated149-autonomous | 1.2 | 5.552971 | 80.719/56.149/57.667 | 100.119 | 4.023/1.530/0.000 | 13.338 | -0.301/3.575 | 0.227/6.855 | 2.312 |
| separated149-autonomous | 2.1 | 5.500992 | 78.058/60.077/63.789 | 101.516 | 3.049/2.252/0.200 | 12.668 | -0.353/7.796 | 0.217/2.705 | 37.172 |
| separated149-autonomous | 2.2 | 6.100849 | 85.716/53.239/65.718 | 111.511 | 2.800/3.230/0.071 | 23.872 | -0.334/10.292 | 0.256/4.897 | 20.078 |
| separated149-autonomous | 3.1 | 5.674412 | 82.948/55.823/65.470 | 104.493 | 3.508/1.966/0.200 | 12.967 | -0.265/4.520 | 0.272/6.808 | 21.109 |
| separated149-autonomous | 3.2 | 6.164824 | 89.998/56.023/74.603 | 116.844 | 3.335/2.430/0.400 | 17.190 | -0.248/10.990 | 0.304/4.205 | 1.734 |
| contingency151-gate4-A-v2 | 1.1 | 5.822031 | 65.609/55.198/65.246 | 97.721 | 2.364/3.458/0.000 | 10.612 | -0.378/1.794 | 0.209/8.772 | 20.500 |
| contingency151-gate4-A-v2 | 1.2 | 5.550888 | 80.711/56.152/57.658 | 100.080 | 4.023/1.528/0.000 | 13.341 | -0.306/3.561 | 0.206/6.869 | 28.594 |
| contingency151-gate4-A-v2 | 2.1 | 5.635066 | 78.074/56.319/60.803 | 101.733 | 3.650/1.785/0.200 | 12.719 | -0.368/7.822 | 0.203/2.680 | 50.469 |
| contingency151-gate4-A-v2 | 2.2 | N/D | 83.263/N/D/N/D | N/D | N/D/N/D/N/D | N/D | N/D/N/D | N/D/N/D | N/D |
| contingency151-gate4-A-v2 | 3.1 | N/D | N/D/N/D/N/D | N/D | N/D/N/D/N/D | N/D | N/D/N/D | N/D/N/D | N/D |
| contingency151-gate4-A-v2 | 3.2 | N/D | N/D/N/D/N/D | N/D | N/D/N/D/N/D | N/D | N/D/N/D | N/D/N/D | N/D |


Nowy B: wszystkie sześć łuków N/D — pełny bieg nie został uruchomiony. Wszystkie próby i częściowe łuki są w `corners.csv`; CPU łuku obejmuje tylko decyzje wewnątrz łuku, nie przygotowanie na prostej.

## CPU — rzeczywisty profil procesu

| Próba | candidate_generation | full_k1 | nominal_propagation | uncertainty_scenarios | conditional_continuations | recertification | independent_preflight | outside_decision | Pozostałe zapisy/abstain | Sterownik łącznie |
|---|---|---|---|---|---|---|---|---|---|---|
| contingency151-gate2-A-v1 | 0.000 | 51.406 | 0.000 | 0.000 | 0.688 | 0.078 | 1.141 | 0.250 | 31.328 | 84.891 |
| contingency151-gate2-A-v2 | 0.047 | 151.891 | 0.000 | 0.031 | 15.203 | 1.172 | 13.906 | 2.219 | 8.703 | 193.172 |
| contingency151-gate2-A-v3 | 0.000 | 57.438 | 0.000 | 0.000 | 13.219 | 1.375 | 14.828 | 1.656 | 9.266 | 97.781 |
| contingency151-gate3-A-v1 | 0.047 | 56.141 | 0.000 | 0.000 | 15.156 | 1.594 | 18.000 | 1.906 | 11.406 | 104.250 |
| contingency151-gate4-A-v1 | 0.062 | 110.859 | 0.000 | 0.031 | 29.328 | 3.344 | 37.781 | 3.672 | 31.953 | 217.031 |
| contingency151-gate4-A-v2 | 0.016 | 157.156 | 0.000 | 0.047 | 38.484 | 3.578 | 44.172 | 5.109 | 28.391 | 276.953 |


Są to rozłączne kategorie czasu CPU procesu. Rozdzielczość zegara Windows15,625ms powoduje zera w krótkich kategoriach; zero nie oznacza pominięcia sprawdzenia. K1 zawiera własne propagacje, więc nie należy ponownie dodawać ich do innych faz. Nieudane fizyczne wywołania są w dzienniku faultów i w rzeczywistym CPU; nie wymyślono liczby force queries dla wywołania, które rzuciło wyjątek przed zwrotem licznika.

## Reprodukcja, testy i ograniczenia

Instrukcje: [narzędzie i flagi](../../../tools/contingency-recourse/README.md). Replay: `tools/replay-viewer/?case=contingency151`. `native/` przechowuje ciągłe natywne trajektorie, aktywacje, wszystkie guardy i hashe; `trees/` kompletne wybrane i odrzucone drzewa, obserwacje, komendy i fizyczne rezerwy; `sources/` migawki późniejszych wersji. Wczesne v1–v3 to zachowane nieudane próby inżynierskie; nie mają osobnych kompletnych migawek źródeł, co ogranicza ich niezależną reprodukcję. Główna walidowana wersja oraz późniejsze próby mają źródła zapisane przed wykonaniem.

Wyniki lokalnych testów i Windows CI: `validation.json` oraz receipt CI. Historyczne shard i czterech zawodników nie są wykonywane w trybie Draft; są jawnie odkrywane. Brak deklaracji gotowości dla czterech zawodników. Wszystkie źródła historyczne i fizyka mają niezmienione hashe w `source-proof.json`.

Budżet końcowy z ledgeru: **1191.734375 CPU-s / 6 nowych światów**. Wszystkie rezerwacje są zakończone. Nie uruchamiano dalszych światów po zatrzymaniu budżetowym. Kompilacja, testy, CI i operacje Git są poza budżetem eksperymentów.
