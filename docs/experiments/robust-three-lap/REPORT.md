# Badanie #158 — autonomiczne trzy okrążenia

**PASS autonomii S: 10/10 pełnych biegów. Baza ukończyła 0/10.**

Baza: Draft PR #156, dokładny HEAD `7c24c31dd8587889ff2459608c328fb865a541ef`. Gałąź `experiment/robust-three-lap-158`; kontroler i narzędzie domyślnie OFF, bez integracji produkcyjnej i bez merge. Status autonomii wynika wyłącznie z zapisanych natywnych biegów, a nie z zielonych testów lub prognoz.

## Główny wynik i pary A/B

Część niezależna nowego wariantu: 6/6, w tym nowe warunki odłożone: 5/5. Pozostałe cztery zadeklarowane warunki to regresje v5; nie wliczamy ich jako nowego niezależnego potwierdzenia.

Najmniejszy rzeczywisty prześwit obrysu: **0.576933 m**, swept: **0.517854 m**. Największe |β|: 9.032541°. Zero kontaktów i ABSTAIN w końcowych dziesięciu biegach. Najciaśniejszy swept pozostawia tylko 0,017854 m ponad wymagane0,5m; to ograniczony margines, a nie dowód bezpieczeństwa dowolnych innych startów.

| Seria / wariant | Ukończone / próby | DNF | Mediana ukończeń [s] | Min–max ukończeń [s] | Rozstęp [s] | Cała zadeklarowana seria PASS |
|---|---|---|---|---|---|---|
| final-v6 / nowy | 10/10 | 0 | 100.917244 | 99.885698–101.567664 | 1.681966 | TAK |
| final-v6 / baza | 0/10 | 10 | — | —–— | — | NIE |
| robust-three-lap / nowy | 4/5 | 1 | 101.533509 | 100.891608–102.115691 | 1.224083 | NIE |
| robust-three-lap / baza | 0/5 | 5 | — | —–— | — | NIE |


Mediana opisuje tylko ukończenia i zawsze występuje z liczbą DNF. Nie obliczamy średniego czasu z pominięciem nieukończeń ani nie przypisujemy DNF sztucznego czasu mety. Seria v5 pozostaje osobnym nieudanym eksperymentem, nawet po poprawce v6.

Nowy wariant ukończył 10/10 porównywalnych prób, baza 0/10. Jest to zmierzona poprawa niezawodności. **Przewaga czasu względem bazy S jest nieocenialna**: nie istnieje jej ukończony czas trzech okrążeń. Nie podajemy procentowej poprawy czasu. Historyczne wyniki L 53,989766 s i 55,167079 s nie są biegami S i nie stanowią tu punktu odniesienia.

| Warunki | Baza | Nowy | Czas bazy / nowego [s] | Różnica czasu [s] | Identyczny pełny stan startowy |
|---|---|---|---|---|---|
| nominal-B | DNF | PASS S3 | — / 101.390131 | — | TAK |
| unseen-A-shift | DNF | PASS S3 | — / 99.885698 | — | TAK |
| unseen-B-in | DNF | PASS S3 | — / 100.724479 | — | TAK |
| unseen-C-in | DNF | PASS S3 | — / 100.178572 | — | TAK |
| unseen-D-in | DNF | PASS S3 | — / 101.042193 | — | TAK |
| unseen-C-out | DNF | PASS S3 | — / 100.759126 | — | TAK |
| regression-A | DNF | PASS S3 | — / 100.792296 | — | TAK |
| regression-C | DNF | PASS S3 | — / 101.255337 | — | TAK |
| regression-D | DNF | PASS S3 | — / 101.404230 | — | TAK |
| regression-B-shift | DNF | PASS S3 | — / 101.567664 | — | TAK |

## Każda pełna próba końcowa

| Próba | Wynik | Okr. 1 [s] | Okr. 2 [s] | Okr. 3 [s] | Meta [s] | Droga XY [m] | Pose / swept min [m] | ABSTAIN | CPU całe [s] |
|---|---|---|---|---|---|---|---|---|---|
| validation-v6-new-0 | PASS S3 | 35.003580 | 33.106185 | 33.280366 | 101.390131 | 1065.507 | 1.758 / 1.704 | 0 | 1177.906 |
| validation-v6-new-1 | PASS S3 | 34.652484 | 33.171539 | 32.061675 | 99.885698 | 1060.066 | 0.757 / 0.702 | 0 | 1455.422 |
| validation-v6-new-2 | PASS S3 | 34.718368 | 33.141993 | 32.864118 | 100.724479 | 1056.282 | 1.735 / 1.682 | 0 | 1351.719 |
| validation-v6-new-3 | PASS S3 | 34.832996 | 32.754829 | 32.590747 | 100.178572 | 1058.562 | 0.982 / 0.923 | 0 | 1385.297 |
| validation-v6-new-4 | PASS S3 | 34.853175 | 32.807668 | 33.381351 | 101.042193 | 1057.269 | 1.599 / 1.576 | 0 | 1323.812 |
| validation-v6-new-5 | PASS S3 | 34.798296 | 33.221756 | 32.739075 | 100.759126 | 1065.951 | 0.605 / 0.546 | 0 | 1442.312 |
| validation-v6-new-6 | PASS S3 | 34.700629 | 33.175327 | 32.916340 | 100.792296 | 1058.007 | 1.099 / 1.074 | 0 | 1332.797 |
| validation-v6-new-7 | PASS S3 | 34.906928 | 33.324739 | 33.023670 | 101.255337 | 1060.124 | 0.877 / 0.816 | 0 | 1365.469 |
| validation-v6-new-8 | PASS S3 | 34.909644 | 33.226700 | 33.267886 | 101.404230 | 1067.055 | 0.577 / 0.518 | 0 | 1381.219 |
| validation-v6-new-9 | PASS S3 | 34.720945 | 33.005491 | 33.841229 | 101.567664 | 1061.206 | 1.391 / 1.336 | 0 | 1480.766 |
| validation-v6-base-0 | DNF | — | — | — | — | 0.000 | 4.099 / — | 1 | 2.109 |
| validation-v6-base-1 | DNF | — | — | — | — | 0.000 | 1.849 / — | 1 | 1.875 |
| validation-v6-base-2 | DNF | — | — | — | — | 0.000 | 3.599 / — | 1 | 2.031 |
| validation-v6-base-3 | DNF | — | — | — | — | 0.000 | 4.599 / — | 1 | 1.969 |
| validation-v6-base-4 | DNF | — | — | — | — | 0.000 | 1.599 / — | 1 | 1.953 |
| validation-v6-base-5 | DNF | — | — | — | — | 0.000 | 3.599 / — | 1 | 2.188 |
| validation-v6-base-6 | DNF | — | — | — | — | 0.000 | 1.099 / — | 1 | 2.156 |
| validation-v6-base-7 | DNF | — | — | — | — | 0.000 | 4.099 / — | 1 | 2.297 |
| validation-v6-base-8 | DNF | — | — | — | — | 0.000 | 1.099 / — | 1 | 2.234 |
| validation-v6-base-9 | DNF | — | — | — | — | 0.000 | 4.599 / — | 1 | 2.422 |


Każdy wiersz rozpoczyna się od rzeczywiście zatrzymanego stanu: prędkość 0, pusta pending, aktywny gaz i skręt 0, oryginalne koło/wał/aktuator. Na początku pierwszej zapisanej próbki świata może już być komenda wydana w chwili 0; stan **przed decyzją** znajduje się w `policies/.../decision-0000.json.gz` lub `abstain.json.gz`. To ten pełny stan porównano parami. Meta jest interpolowanym rzeczywistym przekroczeniem w natywnym kroku; ostatnia próbka może leżeć do 0,01 s za metą. Do walidacji bezpieczeństwa pozostawiono także ten cały końcowy krok.

## Sześć łuków i proste

W każdej komórce łuku: **wejście / minimum / wyjście [km/h]**. Granice geometryczne wynikają z niezmienionego G0; minimum pochodzi z pełnych próbek, a wejście i wyjście z interpolacji natywnych przekroczeń. Brak osiągnięcia łuku pozostaje „—”.

| Próba | Łuk 1 | Łuk 2 | Łuk 3 | Łuk 4 | Łuk 5 | Łuk 6 |
|---|---|---|---|---|---|---|
| validation-v6-new-0 | 37.40 / 37.18 / 37.92 | 37.93 / 37.31 / 38.43 | 38.15 / 37.18 / 37.79 | 38.31 / 37.28 / 37.76 | 37.75 / 37.23 / 38.10 | 37.78 / 37.26 / 37.99 |
| validation-v6-new-1 | 37.61 / 37.21 / 37.85 | 37.80 / 37.30 / 38.03 | 37.74 / 37.10 / 37.96 | 38.05 / 37.04 / 38.29 | 38.13 / 37.31 / 37.59 | 37.73 / 37.27 / 43.95 |
| validation-v6-new-2 | 37.42 / 37.06 / 37.68 | 38.16 / 37.34 / 37.97 | 37.48 / 37.31 / 37.79 | 37.91 / 37.31 / 37.87 | 37.69 / 37.20 / 37.89 | 37.76 / 37.36 / 38.44 |
| validation-v6-new-3 | 37.66 / 37.27 / 37.56 | 37.52 / 37.04 / 37.71 | 37.84 / 36.97 / 38.61 | 38.63 / 37.18 / 37.87 | 38.08 / 37.16 / 38.19 | 37.92 / 37.19 / 40.29 |
| validation-v6-new-4 | 37.70 / 37.28 / 37.67 | 37.77 / 37.31 / 37.66 | 38.11 / 37.21 / 38.03 | 38.03 / 37.27 / 37.68 | 37.51 / 37.23 / 37.66 | 37.74 / 37.29 / 37.72 |
| validation-v6-new-5 | 37.88 / 37.25 / 37.85 | 37.65 / 37.19 / 37.75 | 37.64 / 37.19 / 37.86 | 37.79 / 37.16 / 38.24 | 38.53 / 37.26 / 38.32 | 37.91 / 37.22 / 38.47 |
| validation-v6-new-6 | 37.71 / 37.40 / 37.85 | 38.05 / 37.16 / 37.65 | 37.63 / 37.24 / 38.11 | 37.88 / 37.31 / 37.72 | 37.93 / 37.34 / 38.36 | 38.01 / 37.25 / 37.86 |
| validation-v6-new-7 | 37.71 / 37.31 / 37.79 | 37.18 / 37.12 / 37.76 | 37.44 / 37.27 / 38.15 | 38.11 / 36.85 / 37.83 | 37.54 / 37.33 / 37.75 | 37.61 / 37.24 / 37.99 |
| validation-v6-new-8 | 37.68 / 37.22 / 38.11 | 37.89 / 37.27 / 37.67 | 37.73 / 37.34 / 37.76 | 37.81 / 37.20 / 37.84 | 37.95 / 37.17 / 37.96 | 38.42 / 37.34 / 39.01 |
| validation-v6-new-9 | 37.42 / 37.05 / 38.32 | 37.53 / 37.23 / 38.12 | 37.95 / 37.29 / 37.85 | 37.73 / 37.18 / 37.78 | 37.69 / 37.28 / 37.52 | 37.87 / 37.34 / 38.38 |
| validation-v6-base-0 | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — |
| validation-v6-base-1 | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — |
| validation-v6-base-2 | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — |
| validation-v6-base-3 | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — |
| validation-v6-base-4 | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — |
| validation-v6-base-5 | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — |
| validation-v6-base-6 | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — |
| validation-v6-base-7 | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — |
| validation-v6-base-8 | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — |
| validation-v6-base-9 | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — | — / — / — |


[Pełne sześć łuków](six-bends.csv): także czas, gaz, β, yaw, wheelspin i swept. [Profile prostych](straights.csv): maksimum i średnia czasowa dla każdej prostej w każdym okrążeniu. Prosta przecinająca linię start/meta jest podzielona na odcinek do pierwszego łuku oraz odcinek po drugim łuku; dlatego tabela ma dziewięć odcinków na trzy okrążenia. Wykres prędkości zachowuje pełny profil biegu.

| Próba | Maks. na prostych [km/h] | Gaz 0 / częściowy / 100% [%] | |β| maks. [°] | |yaw| maks. [rad/s] | |wheelspin| maks. |
|---|---|---|---|---|---|
| validation-v6-new-0 | 40.18 | 30.82 / 67.99 / 1.18 | 6.263 | 0.983 | 11.316 |
| validation-v6-new-1 | 61.79 | 33.49 / 64.71 / 1.80 | 9.033 | 1.057 | 11.316 |
| validation-v6-new-2 | 48.23 | 29.64 / 69.17 / 1.19 | 4.728 | 0.842 | 11.316 |
| validation-v6-new-3 | 61.11 | 29.20 / 69.40 / 1.40 | 8.840 | 1.075 | 11.316 |
| validation-v6-new-4 | 39.71 | 31.52 / 66.90 / 1.58 | 8.132 | 0.988 | 11.316 |
| validation-v6-new-5 | 53.06 | 31.01 / 67.60 / 1.39 | 6.516 | 0.986 | 11.316 |
| validation-v6-new-6 | 38.78 | 31.80 / 66.81 / 1.39 | 8.759 | 1.057 | 11.316 |
| validation-v6-new-7 | 42.00 | 29.28 / 69.33 / 1.38 | 5.711 | 0.929 | 11.316 |
| validation-v6-new-8 | 41.53 | 30.82 / 68.00 / 1.18 | 4.992 | 0.911 | 11.316 |
| validation-v6-new-9 | 38.78 | 29.98 / 68.44 / 1.58 | 8.769 | 1.053 | 11.316 |
| validation-v6-base-0 | — | — / — / — | 0.000 | 0.000 | — |
| validation-v6-base-1 | — | — / — / — | 0.000 | 0.000 | — |
| validation-v6-base-2 | — | — / — / — | 0.000 | 0.000 | — |
| validation-v6-base-3 | — | — / — / — | 0.000 | 0.000 | — |
| validation-v6-base-4 | — | — / — / — | 0.000 | 0.000 | — |
| validation-v6-base-5 | — | — / — / — | 0.000 | 0.000 | — |
| validation-v6-base-6 | — | — / — / — | 0.000 | 0.000 | — |
| validation-v6-base-7 | — | — / — / — | 0.000 | 0.000 | — |
| validation-v6-base-8 | — | — / — / — | 0.000 | 0.000 | — |
| validation-v6-base-9 | — | — / — / — | 0.000 | 0.000 | — |

| Próba | |Fx przód| maks. [N] | |Fy przód| maks. [N] | |Fx tył| maks. [N] | |Fy tył| maks. [N] |
|---|---|---|---|---|
| validation-v6-new-0 | 108.76 | 683.01 | 914.60 | 722.97 |
| validation-v6-new-1 | 140.94 | 675.14 | 914.60 | 828.15 |
| validation-v6-new-2 | 97.12 | 605.93 | 914.60 | 642.34 |
| validation-v6-new-3 | 94.33 | 654.59 | 914.60 | 820.41 |
| validation-v6-new-4 | 120.45 | 638.64 | 914.60 | 796.25 |
| validation-v6-new-5 | 134.77 | 685.97 | 914.60 | 725.16 |
| validation-v6-new-6 | 89.54 | 653.74 | 914.60 | 824.70 |
| validation-v6-new-7 | 91.14 | 644.51 | 914.60 | 674.64 |
| validation-v6-new-8 | 133.38 | 666.88 | 914.60 | 664.81 |
| validation-v6-new-9 | 93.86 | 653.53 | 914.60 | 824.07 |
| validation-v6-base-0 | — | — | — | — |
| validation-v6-base-1 | — | — | — | — |
| validation-v6-base-2 | — | — | — | — |
| validation-v6-base-3 | — | — | — | — |
| validation-v6-base-4 | — | — | — | — |
| validation-v6-base-5 | — | — | — | — |
| validation-v6-base-6 | — | — | — | — |
| validation-v6-base-7 | — | — | — | — |
| validation-v6-base-8 | — | — | — | — |
| validation-v6-base-9 | — | — | — | — |


Pełne `intervals/*.csv.gz` w każdej fazie zawierają 100 Hz: Fx/Fy obu osi, wheelspin, β, yaw, rzeczywistą krzywiznę XY, gaz żądany i aktywny, koło/wał, obciążenia, pracę i straty energii. Czasowy udział gazu obliczono z **aktywnej** komendy, z uwzględnieniem opóźnienia i interpolowanej mety. DNF przy czasie 0 nie ma zdefiniowanego udziału gazu ani swept interwału.

## Kontroler i pending

Nowy generator nie odczytuje demonstracji ani wcześniej znalezionych przyszłych komend. Z bieżącej obserwacji fizycznie propaguje nieodwracalny prefiks 0,24 s, z całą aktywną komendą, pending i czasami aktywacji, kołem, wałem oraz skrętem. Dopiero dla tego przyszłego stanu wyznacza korektę linii, prędkości, β i yaw. Inwersja **oryginalnych** sił opon i silnika dobiera wspólnie gaz oraz kierownicę. Docelowa prędkość rozwojowa wynosi 10 m/s; to spokojna baza autonomii, a nie poszukiwanie rekordu offline. Docelowy offset płynnie dochodzi z pola startowego do3,5m przed pierwszym łukiem. To intencja; rzeczywista linia wynika z fizyki, korekt i ponownej certyfikacji i może znacznie od niej odbiegać.

Najmniejszy swept0,517854m wystąpił w regresji D (`validation-v6-new-8`) w95,56s, ref.900,877m, na szóstym łuku. Odstęp pozy od bandy zewnętrznej wynosił wtedy0,577332m. Ten punkt pozostaje w pełnym logu i na rzeczywistej linii XY; nie wygładzono go ani nie pominięto. Zwiększanie prędkości lub dodanie rywali wymaga osobnej walidacji i poprawy marginesu.

Plan powstaje przyczynowo z kolejnych własnych przewidywanych stanów. Zachowane własne plany służą jako propozycje; po rzeczywistej obserwacji są korygowane i ponownie propagowane. Zmiana dowolnej części pending uniemożliwia uznanie obserwacji za identyczną z dawnym stanem. Wyczerpaną końcówkę własnego planu odnawia ten sam sterownik stanu; nie wstawia historycznej taśmy. Po przesunięciu celu zachowana propozycja musi przejść całą nową rezerwę.

Każda wykonana decyzja ma świeże 13 węzłów / 9 pełnych rezerw i niezależny preflight. Zachowano skończone skorelowane innowacje z bazowego protokołu, cały fizyczny obrys ≥0,5 m, swept intervals, β ≤45°, oryginalne aktuatory, reakcję 0,24 s i przeglądy co 0,2 s. To dowód dla zadeklarowanych scenariuszy i skończonego zbioru innowacji, nie globalny dowód wszystkich stanów ani wyścigu czterech rywali. Wybrane rezerwy są odtwarzane osobnym fizycznym providerem; cache propozycji nie zastępuje certyfikacji.

**Korekta ustalenia z #157:** generator tamtego pliku szukał wywołania `Feedback153.Action(Runtime,state,anchor)` bez spacji, a rzeczywisty plik zawierał spacje. W efekcie wykonanie #157 nadal używało starego hooka Feedback153, mimo opisu sprzężonej korekty. W #158 dopasowanie uwzględnia białe znaki i wymusza brak starego hooka; rzeczywiście wywołuje Feedback158 i Joint158. Zamrożonych źródeł ani raportów #157 nie poprawiano wstecz.

## Pełne porażki i rozwój przed zamrożeniem

| Próba rozwojowa | Wynik | Koniec obserwacji [s] | Ukończone okrążenia | Meta [s] | Powód |
|---|---|---|---|---|---|
| dev-native-v3 | DNF | 101.000 | 2 | — | technical invariant: STUDY158_ABSTAIN: abstain: finite recourse search has no certified complete reserve |
| dev-native-v4 | PASS S3 | 101.400 | 3 | 101.390131 | completed |
| dev-native-v6-A | PASS S3 | 100.800 | 3 | 100.792296 | completed |

Pierwszy natywny v3 zatrzymał się przy 101,0 s, około 3,8 m przed końcem trzeciego okrążenia. Po dojściu prognozy do mety końcowy węzeł nadal potrzebował realnej dalszej rezerwy. Naprawa **wydłuża** końcową rezerwę o 32 m za metę; nie skraca certyfikatu i nie oznacza zawodnika jako ukończonego na podstawie prognozy. V4 jako pierwszy rzeczywiście ukończył pełne S w 101,390130575 s.

V5 zamrożono przed pięcioma próbami; wynik to 4/5 i jedna odmowa startu A. Wspólny generator zbyt długo utrzymywał wewnętrzną linię startową: przy ref. 81,36 m prognozowany swept spadał do około 0,4549 m. Nie wykonano niecertyfikowanej komendy. Po tej diagnozie v6 przesuwa koniec gładkiego zejścia z wyjścia pierwszego łuku na jego wejście. Do opracowania użyto tylko znanego A i nominalnego B. V6 ma sześć warunków części niezależnej (nominalny i pięć nowych odłożonych) oraz cztery dodatkowe regresje v5, łącznie dziesięć różnych warunków; wszystkie dziesięć wymagane do końcowego PASS.

Kolektor v5 miał błąd `Min()` na pustej liście interwałów przy ABSTAIN w chwili 0. Surowy świat, journal i odrzucona polityka były już zapisane. Analiza jawnie odtworzyła status DNF z tych logów; nie odtwarzała jazdy. Poprawka v6 zapisuje w tej sytuacji swept `null` i normalny wynik DNF. Wszystkie sześć takich przypadków v5 pozostało w tabelach i kosztach. Wczesne błędne eksperymenty inwersji oraz niezgodna o ulp wersja prefiksu również są zachowane.

## CPU i zgodność optymalizacji

| Próba | CPU decyzji średnia / mediana / maks. [s] | CPU callback razem [s] | CPU bieg+eksport [s] | Odrzucone / naprawione plany | Pełne K1 |
|---|---|---|---|---|---|
| validation-v6-new-0 | 2.272 / 1.844 / 8.219 | 1151.969 | 1177.906 | 4443 / 505 | 0 |
| validation-v6-new-1 | 2.863 / 2.258 / 12.125 | 1431.266 | 1455.422 | 4311 / 491 | 0 |
| validation-v6-new-2 | 2.629 / 2.266 / 8.812 | 1325.016 | 1351.719 | 4314 / 492 | 0 |
| validation-v6-new-3 | 2.712 / 2.000 / 11.656 | 1358.625 | 1385.297 | 4248 / 485 | 0 |
| validation-v6-new-4 | 2.563 / 2.219 / 7.578 | 1296.672 | 1323.812 | 4391 / 498 | 0 |
| validation-v6-new-5 | 2.813 / 2.328 / 11.594 | 1417.625 | 1442.312 | 4384 / 495 | 0 |
| validation-v6-new-6 | 2.595 / 2.188 / 10.219 | 1308.109 | 1332.797 | 4439 / 503 | 0 |
| validation-v6-new-7 | 2.644 / 2.000 / 11.531 | 1340.266 | 1365.469 | 4482 / 506 | 0 |
| validation-v6-new-8 | 2.666 / 2.062 / 11.938 | 1354.234 | 1381.219 | 4450 / 504 | 0 |
| validation-v6-new-9 | 2.861 / 2.234 / 12.719 | 1453.641 | 1480.766 | 4427 / 502 | 0 |
| validation-v6-base-0 | 0.828 / 0.828 / 0.828 | 0.828 | 2.109 | 0 / 0 | 0 |
| validation-v6-base-1 | 0.609 / 0.609 / 0.609 | 0.609 | 1.875 | 0 / 0 | 0 |
| validation-v6-base-2 | 0.703 / 0.703 / 0.703 | 0.703 | 2.031 | 0 / 0 | 0 |
| validation-v6-base-3 | 0.688 / 0.688 / 0.688 | 0.688 | 1.969 | 0 / 0 | 0 |
| validation-v6-base-4 | 0.641 / 0.641 / 0.641 | 0.641 | 1.953 | 0 / 0 | 0 |
| validation-v6-base-5 | 0.766 / 0.766 / 0.766 | 0.766 | 2.188 | 0 / 0 | 0 |
| validation-v6-base-6 | 0.766 / 0.766 / 0.766 | 0.766 | 2.156 | 0 / 0 | 0 |
| validation-v6-base-7 | 0.891 / 0.891 / 0.891 | 0.891 | 2.297 | 0 / 0 | 0 |
| validation-v6-base-8 | 0.719 / 0.719 / 0.719 | 0.719 | 2.234 | 0 / 0 | 0 |
| validation-v6-base-9 | 1.000 / 1.000 / 1.000 | 1.000 | 2.422 | 0 / 0 | 0 |


„Naprawiony” oznacza decyzję z legalnym certyfikatem po co najmniej jednej odrzuconej próbie; to nie liczba przełączeń silnika ani kontaktów. Profile faz każdej decyzji i liczniki wszystkich prognoz pozostają w `policies/` oraz `analysis.json`. CPU callback obejmuje zapis dowodu; koszt całego procesu obejmuje też przygotowanie i eksport. Kosztów równolegle uruchomionych procesów nie interpretujemy jako porównania mikrooptymalizacji przy identycznym obciążeniu.

Serię v6 rozpoczęło dziesięć równoległych procesów. Po zaobserwowaniu narzutu sprzętu ograniczono ich wspólną maskę do czterech logicznych CPU, bez zmiany kodu, pamięci światów, komend ani czasu symulacji. Wszystkie procesy nadal były aktywne i korzystały ze zwykłego harmonogramu systemu. `final-v6/scheduling.json` zapisuje maski, identyfikatory i już zużyty CPU; około 410–420 CPU-s początkowego narzutu na każdy proces nie odjęto. Baza odmówiła przed tą zmianą; jej CPU nie jest porównaniem wydajności algorytmu, a porównanie sportowe dotyczy identycznej fizyki i stanu startowego.

Średni koszt nowego pełnego biegu: 1369.672 CPU-s. Proste przemnożenie przez czterech zawodników daje 5478.688 CPU-s (91.31 minut CPU), **bez** dodatkowych interakcji i wspólnego planowania. To estymacja obciążenia, nie test wyścigu czterech zawodników. Przegląd 0,2 s nie jest limitem CPU 0,2 s; wyścig może być obliczany przed animacją. Koszt nadal wymaga dalszej redukcji i osobnego pomiaru dla czterech rywali.

Optymalizacje zachowują pełny klucz fizyczny z pending: 0,2 s nieodwracalnego prefiksu pochodzi z już policzonych 0,24 s, a odpowiedzi oryginalnej inwersji Joint150 są pamiętane według pełnych bitów stanu i gazu/skrętu. Odrzucona wersja `exactInitialWheel:true` różniła się o ulp w 4/72 stanach; wersja kanoniczna daje 72/72 identycznych bitowo przejść. Test celowo uszkodzonego cache wymaga od niezależnego providera wykrycia różnicy. `nominal-memo-proof.json` potwierdza identyczność wszystkich 10 153 natywnych próbek i wszystkich komend między v4 a v5 nominalnym.

`nominal-v6-repro-proof.json` dodatkowo potwierdza identyczność wszystkich 10 153 próbek i 507 wyborów komendy między nominalnymi v5 i v6 mimo innego obciążenia oraz maski CPU. Tego deterministycznego powtórzenia nie liczymy jako kolejnego niezależnego warunku. Pomiary v6 obejmują pięć innych nowych warunków odłożonych.

Dotychczasowy jawny rachunek: 21602.000000 CPU-s, 33 nowych natywnych światów (wszystkie, także DNF i rozwój). Osobne koszty analizy/eksportu są w `analysis-costs/`. Końcowy spis zawiera także koszt tego eksportu. Nie przeniesiono limitu 1000 CPU-s / 15 światów z #157; nie jest to budżet zadania #158. Ustalony przed v6 operacyjny limit jednej próby wynosi 2400 CPU-s, jednej decyzji 60 CPU-s; przekroczenie oznacza DNF. Kompilacje i standardowe regresje nie są rolloutami badawczymi.

## Artefakty i walidacja

- [Replay porównawczy](Replay-158.html): wyłącznie zapisane natywne stany, wszystkie serie i DNF.
- [Wszystkie próby](trials.csv), [sześć łuków](six-bends.csv), [proste](straights.csv), [pełna analiza](analysis.json).
- [Linie i profile prędkości](plots/lines-speed.png), [fizyka nominalnego biegu](plots/physics.png).
- `native/`, `journals/`, `policies/`, `sources/` w każdej fazie: pełne logi światów, decyzji, dowody i źródła użyte w danej próbie. Każde `policies/<próba>.zip` bezstratnie przechowuje wszystkie oryginalne `decision-*.json.gz` i ewentualne `abstain.json.gz`; sprawdzono SHA-256 każdego wpisu.
- `controller-freeze.json`, `scenarios-frozen.json`, [niezmienione źródła fizyki i historii](source-proof.json).
- [Walidacja C#, Python, Node i Windows CI](VALIDATION.md).
