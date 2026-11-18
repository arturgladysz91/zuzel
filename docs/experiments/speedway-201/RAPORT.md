# #201 — Integracja C #200 i ograniczenia prędkości H25

**C #200 jest wybieralny przy uruchamianiu nowego biegu w stadionie.** Normalny POST do symulatora ukończył trzy okrążenia w **46,386863 s**, zgodnie z dotychczasowym zapisem. Sterowanie jest eksperymentalne, wymaga jawnego wyboru i obecnie obsługuje zweryfikowane solo z pola D oraz 8:1.

**Zapis #200 przekracza 100 km/h przed metą.** Przy **45,70 s** motocykl ma **100,895 km/h**, a ostatnia natywna próbka przy **46,39 s** ma **110,369 km/h**. Meta wypada przy **46,386863 s**. Nagranie zawiera tylko **3,14 ms** ruchu po interpolowanym przecięciu mety. Wcześniejsza wartość 97,765 km/h była maksimum sześciu prostych przed łukami. Końcowy fragment od ostatniego łuku do mety został pominięty w tej statystyce. Poprawiłem opis w stadionie, aby pokazywał zakres pomiaru oraz maksimum całego nagrania.

W dopasowanym odcinku #196 ma wyższy szczyt prędkości, lecz **dłuższy czas** niż C #200. Zidentyfikowałem także bramkę stylu, która odrzuca fizycznie legalny #196. Usunięcie jej chwilowego limitu yaw w osobnym wariancie #201 i ponowne uczenie dało **46,260501 s** na S3, przy **97,725 km/h** na prostych przed łukami. Zysk **0,126362 s (0,273%)** pozostaje mały. Ten wynik nie rozwiązuje celu szybkich prostych.

## Meta i aktywne sterowanie

W C #200 cel optymalizacji oraz certyfikacji to **3 × 318,77875 m = 956,33625 m postępu**. Oceniana jest cała kontynuacja do tego punktu. Lokalna kontrola FAST50 używa 250 ms, ale zatrzymuje prognozę po osiągnięciu celu. Następny łuk leży już poza celem i nie wpływa na wynik. Przed końcem planista przestaje tworzyć nowe poprawki, gdy nie zdążyłyby zostać certyfikowane i wdrożone. Kontynuuje obowiązujące polecenia.

| Czas [s] | Postęp [m] | Prędkość [km/h] | Gaz zadany / aktywny [%] |
|---:|---:|---:|---:|
| 45,40 | 928,539 | 96,912 | 96,80 / 94,70 |
| 45,60 | 933,729 | 99,662 | 100 / 100 |
| 45,70 | 936,425 | 100,895 | 100 / 100 |
| 46,00 | 944,846 | 104,352 | 99,93 / 99,85 |
| 46,20 | 950,698 | 107,058 | 100 / 100 |
| 46,30 | 953,694 | 108,710 | 89,57 / 98,24 |
| 46,39 | 956,432 | 110,369 | 74,82 / 81,42 |

Gaz spada pod koniec, a prędkość nadal rośnie dzięki dodatniemu bilansowi napędu. Ostatnia aktywowana komenda została zgłoszona przy **46,30 s**, z terminem **46,35 s**. Ostatnie zadane polecenie 74,82% pozostaje pending z terminem około 46,40 s, którego symulacja już nie osiąga.

W kodzie pętla świata kończy się po ukończeniu trzech okrążeń. Pozycja w odtwarzaczu pochodzi z próbek i interpolacji między nimi. Czas odtwarzania jest ograniczony do końca nagrania. Nie występuje dodatkowy przejazd ani przełączenie na drugi kontroler po mecie. Po jej przekroczeniu nie pojawia się nowe żądanie gazu. Fizyczne kryterium swept nadal sprawdza ostatni krok.

**Istnieje efekt końca horyzontu.** Przywróciłem w modelu ocenę kolejnego pełnego łuku i 20 m prostej, z tych samych stanów 66, 46, 26 i 6 m przed metą oraz z ostatniej próbki. Kontynuacja archiwalnych komend do mety jest legalna. Dalsze utrzymywanie końcówki taśmy narusza swept przy około **1002–1004 m postępu**, kilkanaście metrów w następnym łuku. Krótka próba odzysku przez poprzedni cykl i jego lokalne poprawki również nie znalazła legalnej kontynuacji. To dowód braku gwarancji **za metą** w użytym planie. Ciągła jazda po wyścigu wymagałaby odrębnego celu oraz bezpiecznego następcy.

CSV obejmuje każdy krok od 42 s, prędkość, obydwa kanały poleceń, pełną kolejkę pending, status mety, politykę, pozostały czas celu i informację o braku prognozy następnego łuku. C #200 nie wylicza osobnej skalarnej „dozwolonej prędkości na następnym łuku”. Sprawdza dynamiczną kontynuację. [Telemetria przy mecie](finish.csv.gz) · [Interwencje na horyzoncie](finish-model.json.gz).

## #196 i C #200 z identycznego stanu

Próby wykorzystują H25, 8:1, tę samą geometrię i stan przy **25,487128 s**, **451,168144 m**. W każdym nowym świecie odtworzono od startu i porównano **2555 natywnych próbek**, wraz z kołem, wałem, przechyłem, siłami, aktywną komendą i pending. W punkcie rozgałęzienia nie resetowano stanu. Wynik #196 pochodzi z istniejącego natywnego archiwum. Nowe próby obejmują autonomiczną adaptację C #200 do tego stanu oraz dalsze uczenie #201. Granica oceny wszystkich wariantów to **630,55750 m**, 20 m za łukiem.

| Natywny manewr | #196, zamrożony harmonogram | C #200, adaptacja do stanu | Uczenie #201 |
|---|---:|---:|---:|
| Czas [s] | 8,77026 | **8,21342** | **7,87240** |
| Vmax prostej [km/h] | **118,013** | 89,389 | 94,150 |
| Wejście w łuk [km/h] | 115,26 | 86,38 | 88,80 |
| Minimum całego łuku [km/h] | 64,59 | 80,65 | 78,51 |
| Wyjście [km/h] | 76,48 | 90,02 | 93,52 |
| Minimalny swept [m] | 0,884 | 0,728 | 0,619 |
| Maks. błąd XY przy aktywacji [mm] | — | 0,077 | 0,076 |

Wyższa prędkość prostej #196 zostaje okupiona większą utratą prędkości w łuku i słabszym wyjściem. Zwycięski #201 zyskuje **0,341 s** względem C #200 i **0,898 s** względem #196, przy niższym Vmax. To wynik tego samego odcinka, który obejmuje kolejną prostą. Nie stanowi osobnego dowodu czasu pełnego S3.

#196 zaczyna zgłaszać redukcję gazu około **40,72 m** od początku prostej, z aktywacją około **42,30 m**. Adaptacja C #200 zaczyna redukcję już pierwszą nową komendą przy **25,50 s**, około **0,28 m** od początku. Utrzymuje niższą prędkość i zachowuje pełną kontynuację. Dla modelu #196 pełny błąd XY w 176 wspólnych chwilach osiągał maksymalnie **1,84 mm** na prawie 9 s. To za mało, aby wyjaśnić odrzucenie #196.

Przy **497,324219 m** model odrzuca #196 za **yaw 0,233229 rad/s > 0,23 rad/s**. Kurs ruchu odchyla się wtedy tylko o **−0,1549°**, odstęp obrysu wynosi **1,1265 m**, a prędkość **116,702 km/h**. Fizyczna próba #196 pozostaje legalna. Jest to ograniczenie stylu w `Corridor199.Check`. Jego limit nie jest maksymalnym yaw wynikającym z sił motocykla. Zmniejszenie marginesu swept nie usuwa tej odmowy.

W wariancie #201 usunąłem ten **chwilowy limit yaw**, zachowując kurs, pozycję zewnętrzną, wykrywanie dodatkowych korekt, opóźnienia i pełne kryteria H25. Początkowy plan oceniony co 50 ms wykazał dodatkową korektę podczas dokładniejszego odtworzenia. Poprawiłem ocenę techniki, aby wykonywała się w każdym podkroku 10 ms. Dopiero kolejny plan przeszedł sprawdzenie offline i natywne. Oryginalny C #200 w integracji zachowuje własne ustawienia.

[Prędkość względem odległości](196-200-201-predkosc.png) · [Dokładna odmowa #196](196-rejection.json) · [Prognozy i uczenie dopasowane](matched-model.json.gz) · [Natywny C #200](matched-C200-native.json.gz) · [Natywny wyuczony manewr](matched-native.json.gz).

## Co ogranicza gaz

`Motion191` nie uczestniczy w sterowaniu C #200 po uruchomieniu jego planu. Historyczny sterownik #194 obsługuje pierwszą komendę startu. Kolejne komendy wyznacza plan #200 i `Receding198`. Nie występuje jawny limit 100 km/h ani mapowany z odległości limit prędkości. Znaczenie mają połączone decyzje gazu i skrętu, skończona przestrzeń przeszukiwania, warunki pozycji oraz dynamiczna wykonalność.

Dla 13 epizodów redukcji ≥10 pp w 0,25 s, z gazem poprzedzającym ≥60%, wykonałem po dwie interwencje +20 pp przez 0,25 lub 0,50 s. Stan, pending i późniejsze komendy były te same. W obu przypadkach oceniałem kontynuację do mety. Kategoria opisuje wynik interwencji, ponieważ historyczne polecenie nie zapisuje formalnego dowodu globalnej przyczyny.

| Kategoria A–H | Epizody | Wynik |
|---|---:|---|
| A, badane zwiększenie gazu narusza fizyczną kontynuację | 3 | Nie dowodzi braku wszystkich innych manewrów. |
| A, badane zwiększenie gazu pogarsza czas | 4 | Niższy gaz daje szybszy S3 w tej parze. |
| E, istnieje szybszy legalny kandydat | 5 | Przeszukiwanie #200 nie wykorzystało lokalnej rezerwy. |
| H, ograniczenie stylu | 1 | Fizyczna kontynuacja przechodzi, pozycja/yaw ją odrzuca. |
| B, błąd dynamiki | 0 wykazanych | Natywna zgodność jest wysoka. |
| C, dodatkowy margines | 0 jako główna przyczyna | Testowano 0,50 i 0,532 m. |
| D, za krótki horyzont przed metą | 0 wykazanych | Wszystkie interwencje oceniano do S3. |
| F/G, cel i meta | osobny efekt końcowej prostej | Przyszły łuk jest poza celem, co zmienia opłacalny stan końcowy. |

To klasyfikacja w ograniczonej rodzinie interwencji. Nie przypisuję jej gwarancji globalnej niemożliwości ani przyczyn decyzji, których algorytm bezpośrednio nie rejestrował. [Wszystkie epizody i odrzucenia](release-audit.json.gz).

## Audyt fizyki H25

| Parametr lub mechanizm | Faktyczny stan H25 |
|---|---|
| Masa i bezwładność yaw | **147 kg**, **52,72097 kg·m²**. Położenie osi +0,70/−0,60 m względem środka masy. |
| Naciski | Statycznie **665,57 N przód / 776,50 N tył**, około 46,15/53,85%. Brak zależności tego podziału od przyspieszenia. |
| Tarcie efektywne | **μ = 1,605625** w badanym stanie nawierzchni. Iloczyn 1,4, efektywnego gripu nawierzchni i H25 `GripScale=1,25`. |
| Budżet sił | **2315,42 N** łącznie, w tym **1068,66 N** przód i **1246,77 N** tył. |
| Łączenie Fx/Fy | Tylny kontakt rezerwuje Fy, a Fx dostaje pozostałą część koła tarcia. Zmiana wheelspinu wpływa na napęd. Fy jest ciągłą, nasycającą funkcją prędkości bocznej. |
| Przód | Boczna siła jest ograniczona jego własnym naciskiem i poślizgiem. Sterownik kontaktu żąda momentu yaw, realizowanego przez zmianę kierownicy. |
| Skręt i przechył | Kierownica do 35°, maks. zmiana 90°/s. Uproszczony przechył do **1,02 rad = 58,44°**, szybkość 1,75 rad/s. Przechył modyfikuje podatność, bez bezpośredniego zwiększania μ. |
| Silnik | **55 Nm przy 8500 rpm**, około **48,96 kW**. Krzywa pośrednia i odcięcie 12000 rpm są założeniami. Sprawność przekładni 94%, zakres 8–10:1, promień koła 0,33 m. |
| Opory | Model bazowy wykorzystuje **40 + 0,20·v² N**, przy v w m/s, oraz opór obrotu koła. |
| Hamowanie silnikiem | Parametr oporu wału wynosi **0**. Nie ma skalibrowanej charakterystyki strat pompowania/kompresji przy zamkniętym gazie. Sprzęgło i bezwładność nadal wymieniają energię z kołem. |

Na #200 tylna siła łączna osiąga **99,89%** budżetu, a przednia **98,48%**. Maksymalne zmierzone przyspieszenie poprzeczne kierunku ruchu to około **15,02 m/s²**, przy teoretycznym sumarycznym budżecie **15,75 m/s²**. Minimalny promień rzeczywistego ruchu w odcinkach o znaczącym przyspieszeniu bocznym wyniósł około **27,69 m**. Promień obliczałem z kierunku wektora prędkości, bez utożsamiania yaw motocykla z krzywizną śladu. Maksymalna reszta bilansu energii wał–sprzęgło–koło wyniosła **1,15·10⁻¹¹ J** na krok w tej telemetrii.

W stanie quasi-stacjonarnym (F_c = m v^2/R). Przy budżecie H25 118 km/h wymaga promienia około **68,2 m**, 100 km/h około **49,0 m**. Przy R = 35 m budżet odpowiada około **84,5 km/h**. Szybkie wejście z 115 km/h może nadal być legalne, jeśli rzeczywisty promień i prędkość zmieniają się podczas manewru. Wynik #196 pokazuje taką zmianę, wraz z dużym spadkiem prędkości wewnątrz łuku.

**Ograniczenia walidacji fizyki są konkretne.** Statyczne naciski mogą zaniżać zdolność napędu tyłu przy przyspieszaniu i jednocześnie zawyżać dostępny przód. Brak pomiarów wysokości środka masy i dynamicznych nacisków uniemożliwia wyliczenie właściwej poprawki. Zerowy opór wału nie jest realistycznie skalibrowanym hamowaniem silnikiem. Charakterystyki gleby i opony, kąty β, podatności oraz przechył mają uproszczenia gameplay. Poprawna pasywność numeryczna nie zatwierdza ich wartości w rzeczywistym motocyklu.

Masa całego zestawu 147 kg jest zgodna z hipotezą motocykla około 77 kg plus zawodnika 70 kg. Publikowane przepisy FIM podają minimum 77 kg samej maszyny 500 cm³. Instrukcja JAWA 889 podaje moc 48–50 kW przy 8500 rpm, co wspiera punkt szczytowej mocy użyty w modelu. Te źródła nie kalibrują μ, bezwładności, hamowania silnikiem ani krzywej poślizgu. [FIM, przepisy techniczne track racing 2025](https://www.fim-moto.com/fileadmin/user_upload/Documents/2025/2025_V2_TRACK_RACING_Technical_Regulations_10.05.2025.pdf) · [Instrukcja producenta JAWA 889](https://www.jawa500.cz/element/simple/documents-to-download/c/9/82967b1759773d81.pdf).

Do uzasadnionej kalibracji potrzebne są zgodne pomiary prędkości i XY, β, obrotów koła i silnika, gazu oraz obciążenia na znanej nawierzchni. Samo zwiększenie μ nie odtwarza zmiany rozkładu nacisków ani hamowania silnikiem. Nie wykazano systematycznego zaniżenia przyczepności H25. Obecny efektywny budżet jest już wysoki. [Siły, wykorzystanie kontaktów, krzywizna i energia](physics.csv.gz) · [Metryki audytu](metryki.json).

## Hipotetyczna przyczepność +5–20%

Testy modelu rozpoczynają się z tego samego stanu #196. Zmiana skaluje rzeczywiste budżety przodu i tyłu oraz ich sprzężenie w istniejących równaniach. Poza odtworzeniem starego sterowania każda hipoteza otrzymała tę samą liczbę rund poszukiwania. Dla większych zmian dodano wspólną siatkę inicjalizacji gazu i skrętu, aby znaleźć legalny start uczenia. Oceniany jest **swobodny manewr fizyczny** do tego samego punktu 20 m za łukiem.

| Hipoteza | μ efektywne | Najlepszy czas modelu [s] | Wejście / minimum / wyjście [km/h] | Maks. β w łuku [°] | Min. swept [m] |
|---|---:|---:|---|---:|---:|
| H25 | 1,606 | **7,757** | 89,17 / 80,54 / 98,16 | 10,16 | 0,530 |
| +5% | 1,686 | **7,179** | 109,16 / 73,43 / 98,84 | 13,20 | 0,560 |
| +10% | 1,766 | **7,066** | 107,39 / 79,27 / 101,72 | 15,25 | 0,550 |
| +15% | 1,846 | **6,846** | 113,35 / 82,07 / 102,13 | 16,49 | 0,540 |
| +20% | 1,927 | **6,835** | 106,15 / 88,74 / 102,64 | 16,38 | 0,580 |

Prędkości to odczyty z sąsiednich węzłów prognozy 50 ms. Poprawy wynoszą około **7,4%, 8,9%, 11,7% i 11,9%** względem ponownie wyuczonego modelu H25. Uślizg występuje we wszystkich znalezionych rozwiązaniach. Bez przeliczenia sterowania dawny harmonogram #196 przy +10–20% narusza swept. Większa przyczepność zmienia zachowanie całego motocykla i może popsuć trajektorię dopasowaną do starych sił.

Przy 118 km/h wymagany promień od samej nierówności sił zmniejsza się z około **68,2 m do 64,9 / 62,0 / 59,3 / 56,8 m**. To obwiednia quasi-stacjonarna. Szybszy hipotetyczny manewr jest wykonalny w tym modelu, lecz brakuje natywnej walidacji i odniesienia do rzeczywistej charakterystyki opony. Skalowanie sprzężonego modelu potwierdza wrażliwość czasu, a nie poprawność proponowanej kalibracji. [Wszystkie hipotezy, inicjalizacje, legalne i odrzucone kandydatury](grip-sensitivity-expanded.json.gz).

## Integracja, walidacja i wynik końcowy

Wybranie **„C #200 · solo 8:1 (eksperymentalne)”** ustawia solo z pola D i właściwe przełożenie. Przycisk uruchamia nowy świat przez istniejące API `/api/races` i `--stadium-race`, z kolejką FAST50, wbudowanym modelem #200 i certyfikacją od rzeczywistego stanu. Model nie wymaga poprzedniego katalogu roboczego ani ręcznego otwarcia nagrania. Archiwum zapisuje wynik i pozwala nałożyć #198 oraz wcześniejsze warianty.

Kod badawczy, kompletna wyuczona strategia, zapis #200 oraz jego poprzednie źródła są zachowane w projekcie. Pierwszy test integracji ukończył natywny bieg, po czym błąd brakującego pola konsekwencji przerwał eksport. Naprawiłem zapis metadanych. Drugi POST ukończył bieg i zapisał go prawidłowo. Obydwa czasy to **46,386863 s**. To powtórzenie nominalne. Nie traktuję go jako niezależnego testu odporności.

Wykorzystano **5/5 nowych światów**, wszystkie solo. Dwa należą do integracji, dwa do dopasowanego odcinka #196, jeden do S3 po korekcie. Test odmiennego startu i natywna jazda po mecie nie zmieściły się w tym budżecie. Zachowano nieudane kontrole offline, obydwa etapy sensytywności i wszystkie wyniki. C #201 ukończył S3 bez kontaktu i DNF, z minimalnym swept **0,538 m** oraz bez wykrytej dodatkowej korekty. Jego p50/p95/p99 wynoszą **2,75/7,02/8,77 ms**, maksimum **12,48 ms**, zero decyzji ≥50 ms. CPU biegu wyniósł **3,55 s** wobec 2,88 s nominalnego #200. Nowy wariant ma mierzalny koszt i marginalny zysk czasu.

Werdykty pozostają rozdzielone.

- **Sterownik — PARTIAL.** Wykazano i poprawiono zbyt restrykcyjną ocenę chwilowego yaw w odizolowanym #201. C #200 znajduje legalną, szybszą kontynuację od #196, ale pełne S3 nadal nie wykorzystuje prędkości 100+ km/h na prostych przed łukami. Wzrost prędkości przy końcu wiąże się z końcem celu.
- **Fizyka — niewystarczające dane.** Równania są pasywne w sprawdzonych zapisach, a budżet wyjaśnia ograniczenia prędkości ustalonej w łuku. Naciski, gleba/opona i hamowanie silnikiem wymagają empirycznej kalibracji. Sam ten audyt nie uzasadnia zwiększenia μ.
- **Integracja — PASS w zweryfikowanym solo 8:1.** Normalne uruchomienie działa, nominalny wynik #200 jest zachowany, dotychczasowy sterownik pozostaje dostępny. Model #201 pozostaje osobnym narzędziem badawczym.

Kompilacja Release z ostrzeżeniami jako błędami przeszła. Sprawdzono zachowanie pending, format i parametry wbudowanego modelu, odrzucenie błędnych konfiguracji oraz nowy bieg w przeglądarce i porównanie archiwalne. Wyniki testów i sumy danych znajdują się obok raportu. [Widok nowego biegu](integracja-stadion.png) · [Dowód integracji](integracja.json) · [Natywny C #201](C-native.json.gz).

Pełny lokalny zestaw **CoreSim — 3146/3146 PASS**, 0 pominiętych, trwał 14 min 15 s. Testy odtwarzacza #172 — **4/4 PASS**. Projekt zbudowano ponownie także z plików zapisanych w commicie, wyeksportowanych do oddzielnego katalogu. W tej kopii przeszły sprawdzenie modelu #200 oraz kontrola opóźnień i pending. Te kontrole nie uruchamiały dodatkowych światów Box2D. [Sumy kontrolne dowodów](weryfikacja.json).

Wyszukiwanie dopasowanego manewru obejmowało **5123 kandydatów**, z czego **1381** osiągnęło cel, przy CPU **106,27 s**. Pierwszy pełny trening #201 ze starym yaw ocenił **7050** kandydatów (**319** legalnych, CPU **237,31 s**), wariant bez tego progu **7050** (**475**, **232,59 s**), a poprawiona kontrola techniki w każdym kroku **3090** (**208**, **305,72 s**). Czasy treningu nie obciążają pomiaru FAST50 w tabeli. W archiwach są również kandydaci odrzuceni za swept, kierunek, pozycję i technikę. [Pierwszy trening](training-C.json.gz) · [Wariant bez progu yaw](training-C-yaw.json.gz) · [Trening z kontrolą co 10 ms](training-C-refined.json.gz) · [Pierwsza odmowa wykonawcza](dry-C.json.gz) · [Przyjęty przejazd offline](dry-C-refined.json.gz).
