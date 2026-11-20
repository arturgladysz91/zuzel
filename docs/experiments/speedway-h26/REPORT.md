# Badanie #202 — H26: implementacja i walidacja

Baza `26b2103bf9d710b8d1ec8831352e753c6e7480b0`, gałąź `experiment/speedway-202`. H25 pozostaje domyślne. Nie zmieniono μ, mapy momentu netto, silnika, przełożeń nominalnego porównania 8:1, toru, geometrii, mety ani marginesu swept ≥0,5 m. Nie wykonano merge.

**Werdykt ogólny: PARTIAL.** Mechanika i bilans w obszarze dwóch kontaktów: PASS. Natywna walidacja implementacji: PASS z niezwalidowanymi empirycznie parametrami. Legalne ukończenia i FAST50: PASS w zbadanych przypadkach. Pełna technika/odporność sportowa: PARTIAL. **Limit instancji Box2D: niedotrzymany przy liczeniu wszystkich konstrukcji świata.** Oprócz 8 przejazdów #202 zestaw regresji, uruchomiony dwa razy, otworzył 18 krótkich światów legacy (0,05/0,3 s). Razem 26 instancji. To błąd ewidencji: budżet zbyt wąsko liczono tylko dla przejazdów badawczych. Nie ukrywamy tych mikrotestów ani nie przedstawiamy 8 jako całkowitej liczby uruchomień Box2D. Brak poprawy czasu; LE nominalnie jest o 7.50% wolniejszy od H25 C #200 i o 7.80% od historycznego B 46,260501 s. Nie ogłaszamy sukcesu szybkości ani pełnego realizmu.

## Implementacja

Audyt i diagram zapisano przed zmianą fizyki: [AUDYT.md](AUDYT.md). H26 działa w rzeczywistym adapterze Box2D. Po jednym kroku świata zatwierdzane są dokładnie przygotowane: koło, wał, skręt i przechył. Nowa fizyka jest jawnie wybierana, a przewidywanie i wykonanie używają tej samej konfiguracji.

**Naciski.** Dla aktualnego stanu i aktywnej komendy rozwiązujemy `ax = dot(Ffront+Frear+Fres,forward)/m` razem z `Fzf=mg*b/L−m*ax*h/L`, `Fzr=mg*a/L+m*ax*h/L`. Metoda Illinois z zabezpieczonym przedziałem ma tolerancję 0,00002 m/s², najwyżej 36 iteracji. Każda ocena używa niezmienionego zaakceptowanego stanu; nie wykonuje commit ani przyszłej komendy. Fzf/Fzr zmieniają rzeczywiste budżety Fx/Fy i wspólny okrąg tyłu. Suma μFz pozostaje taka sama: około 2315,424 N przy μ=1,605625.

Nominalne **h=0,55 m jest hipotezą** ze środka zakresu 0,45–0,65 m i wcześniejszego roboczego #176. Nie wybrano go pod czas przejazdu. Wszystkie pięć wysokości i trzy skale strat (.5/1/1.5) przetestowano na zapisanych stanach. Dla przyspieszenia 6 m/s² front ma około 360/326/292/258/225 N odpowiednio dla h=.45/.50/.55/.60/.65. Granica odciążenia przodu wynosi `g*b/h`: nominalnie 10,7018 m/s². Nie ma modelu wheelie ani pitch. Rozwiązanie wychodzące poza dodatnie naciski kończy próbę jawną diagnostyką. Rezerwa przedziału 0,001 m/s² odpowiada około 0,06 N; nie jest clampem nacisku.

**Silnik.** H25 pozostawia stary tor obliczeń. Dla H26-E/LE `M=.003*ω`, `P=.005*(1−u)²*ω`, moment spalania mechanicznego brutto `u*(Tmap+M)`, moment netto `u*Tmap−(1−u)*M−P`. Wszystkie składniki mnoży skala hipotezy. Przy 100% gazu zachowujemy dokładnie starą mapę momentu netto, bez ponownego odejmowania jej strat. Mapa oznacza moment hamowany, a rekonstrukcja brutto nie jest nową mocą dostępną na kole ani modelem energii chemicznej paliwa. P oznacza średnią stratę pompowania/histerezy; nie liczymy całej odwracalnej pracy sprężania jako straty.

Moment hamowania przechodzi przez wał, pasywne sprzęgło, przekładnię, koło i kontakt. Przy ujemnym momencie zachowano odwrotną sprawność przekładni. Nie dodano siły hamującej nadwozie. Nie łączymy nowych strat z niezerowym legacy CrankDrag. Zachowano bezwładności, limity sprzęgła, jego zależność załączenia od RPM oraz mapowy cutoff H25. Ten cutoff zeruje moment netto przy ≥12000 RPM; nie jest pełnym modelem odcinania paliwa/zapłonu. Rozdział brutto/tarcie jest także w tym zakresie założeniem, nie pomiarem.

Przy zamkniętym gazie nominalny moment oporowy [Nm]: 2000 RPM →1,676; 4000→3,351; 6000→5,027; 8500→7,121; 10000→8,378; 11000→9,215; 12000→10,053. Rodzina skali .5/1/1.5 daje połowę/nominał/150%. Pełna macierz 0/10/25/50/100% gazu jest w danych.

**Status kalibracji.** [Instrukcja JAWA 889 producenta](https://www.jawa500.cz/element/simple/documents-to-download/c/9/82967b1759773d81.pdf) jest źródłem klasy silnika i mocy przy 8500 RPM, nie mapy oporów zamkniętej przepustnicy. [SAE 2023-01-0416](https://saemobilus.sae.org/papers/experimental-characterization-mechanical-loss-components-a-single-cylinder-spark-ignition-engine-progressive-disassembly-method-2023-01-0416) uzasadnia rozdzielanie składników strat, ale nie kalibruje JAWA. Nie znaleziono pomiarowej mapy motoring torque tego motocykla ani charakterystyki jego sprzęgła. Starsze #187 to symulacja z hipotezą .008ω, nie pomiar realnego hamowania. Współczynniki M/P, ich rozdział i h są **hipotezami gameplay/modelowymi**. Nie uznajemy ich za kalibrację fizyczną ani przybliżoną kalibrację do pomiaru.

## Dwa różne porównania

**Identyczne komendy:** te same początkowe stany, nawierzchnia i wszystkie decyzje C #200, także ostatnie pending; odczytujemy Decisions, nie samą listę aktywacji. H25 odtworzył historyczne próbki dokładnie, 46,386862887 s. L/LE odciążają przód w 0,37 s. E narusza swept w 10,29 s. To test przeniesienia starej polityki, nie ostateczny wynik nowych sterowników.

| Próba | Okrążenia | Meta [s] | Koniec zapisu [s] | Wynik |
|---|---:|---:|---:|---|
| H25, te same komendy | 3 | 46,386863 | 46,390 | completed |
| L, te same komendy | 0 | — | 0,370 | technical invariant: H26 wheel unloading: no positive-load coupled root. |
| E, te same komendy | 0 | — | 10,290 | uncertified swept physical footprint clearance0.5m |
| LE, te same komendy | 0 | — | 0,370 | technical invariant: H26 wheel unloading: no positive-load coupled root. |
| L, osobno wyuczony | 3 | 49,539837 | 49,540 | completed |
| E, osobno wyuczony | 3 | 47,063422 | 47,070 | completed |
| LE, osobno wyuczony | 3 | 49,867287 | 49,870 | completed |
| LE, start przesunięty −0,12 m | 3 | 48,537322 | 48,540 | completed |

**Osobna nauka:** dla każdego H26 wykonano poszukiwanie sprzężonych sterowań od t=0, obejmujące start. Warm start #200 służył jako propozycja; nie zamrożono go jako polityki końcowej. Optymalizowano trajektorie, gaz i skręt przez ciągłe sprzężenie zwrotne, mutacje i ponowną ocenę. Przy przedłużaniu częściowych rozwiązań wykorzystywano wyuczone na H26 fragmenty pełnego cyklu jako propozycję sprzężenia zwrotnego. Każdy kandydat końcowy był oceniony od startu. Nie sklejaliśmy światów ani nie resetowaliśmy motocykla w natywnym biegu. Finalny sterownik ma FAST50, nieodwołalne pending, sprawdzanie lokalnej kontynuacji i rozłożone w czasie poprawki planu. Nie dodano ręcznego harmonogramu gazu/skrętu. Koszt offline nie miał ograniczenia 900 ms.

Budżety poszukiwania różniły się; porównanie wyuczonych wyników jest opisowe, nie izoluje przyczynowo mechanizmów. Nie są to globalne optima. Próba innego przełożenia 8,4:1 odpadła offline przy starcie; zachowano jej wynik. Druga walidacja LE używała legalnego przesunięcia startu −0,12 m i osobnej adaptacji. Nie przypisujemy jej przewagi samemu przesunięciu.

| Parametr | H25 C #200 | H26-L | H26-E | H26-LE |
|---|---:|---:|---:|---:|
| Fz przód przy przyspieszaniu, średnia [N] | 665,6 | 523,8 | 665,6 | 511,9 |
| Fz tył przy przyspieszaniu, średnia [N] | 776,5 | 918,3 | 776,5 | 930,2 |
| Maksymalne ax [m/s²] | 7,972 | 10,592 | 7,984 | 10,622 |
| Średnie opóźnienie przy zamkniętym gazie [m/s²] | 1,254 | 1,302 | 3,350 | 1,700 |
| Maksymalny wheelspin κ [%], także start | 1361,9 | 1058,0 | 1361,9 | 1058,0 |
| Prędkość wejścia w pierwszy łuk [km/h] | 77,18 | 68,85 | 74,63 | 65,88 |
| Prędkość wyjścia z pierwszego łuku [km/h] | 85,97 | 82,81 | 81,75 | 80,06 |
| Vmax przed jednym z sześciu łuków [km/h] | 97,77 | 89,65 | 101,64 | 91,33 |
| Czas pierwszego łuku + 20 m [s] | 5,953964 | 6,219050 | 6,009610 | 6,461630 |
| Czas trzech okrążeń [s] | 46,386863 | 49,539837 | 47,063422 | 49,867287 |
| Minimalny swept [m] | 0,543118 | 0,533378 | 0,573663 | 0,537738 |
| Vmax całego nagrania [km/h] | 110,37 | 95,52 | 109,16 | 92,52 |
| CPU pełnego biegu z AI [s] | 2,875 (archiwum) | 5,094 | 2,891 | 5,484 |

Wszystkie wartości tabeli pochodzą z natywnych trajektorii. Fz: średnia ważona czasem dla ax>1 m/s² i v>2 m/s. Opóźnienie: średnia −dv/dt przy aktywnym gazie <0,1% i v>5 m/s; stany i udział coasting są różne między politykami. Nie jest to dopasowany test hamowania. Maksymalne κ obejmuje start i mały mianownik prędkości. Czas odcinka to pierwszy geometryczny łuk od wejścia do 20 m za wyjściem, z różnymi stanami wejścia; **nie jest dowodem w dopasowanym #196**. CPU H25 z archiwum zawiera AI; nowy H25 replay identycznych komend kosztował tylko 0,969 s i nie jest porównywalnym benchmarkiem AI.

**Dopasowany #196:** zapisany stan ma t=25,487128 s, v=78,967 km/h, aktywny gaz 89,54% i pending 91,59% do aktywacji 25,5 s. Model LE nie ma dodatniego rozwiązania dwóch kontaktów już w nieodwołalnym prefiksie, przed pierwszym nowym review. Nie zużywano świata na niewykonalny certyfikat. Zapisano dokładny stan, komendy, granice i reszty równania oraz odtwarzalny test `check-matched`. Nie udowodniono szybszego manewru #196 na H26. Sloty przejazdów wykorzystano na osobno wyuczone L/E i dwie pełne walidacje LE. **8 przejazdów scenariuszowych oraz 18 mikroświatów regresji, razem 26 konstrukcji. Żadnego świata czterech zawodników.** Mikroświaty pochodzą z 2×(1 test cache transferu .05 s + 4 warianty isolated .3 s × oryginał/powtórzenie). Ewidencja i oba TRX są w danych.

## Odpowiedzi na 16 pytań

1. Dynamiczne naciski wynikają z implicit sprzężenia aktualnych Fx/Fy z ax w osi nadwozia. Nie wynikają z samej pozycji gazu. Zaakceptowane siły idą do jednego rzeczywistego kroku Box2D.
2. h=.55 m, niezwalidowane założenie; zakres .45–.65 i wcześniejsze robocze .55. Dane rzeczywiste nie wystarczają do wyboru wartości zmierzonej.
3. Tak, w kontrolowanym przeliczeniu tego samego stanu przy t=2,6 s i pełnym gazie: ax 6,568→9,555 m/s², Fzr 776,5→1370,8 N. μ i suma budżetów są identyczne. Fzf jednocześnie maleje do 71,3 N, więc wzrost trakcji ogranicza sterowność.
4. W tym samym stanie początkowe κ=49,91% jest identyczne; zmienia się jego dalsza ewolucja. Maksimum κ w natywnej nowej polityce LE maleje z 1361,9% do 1058,0%, ale zawiera efekt nauki startu. Nie traktujemy tego jako czystego efektu transferu.
5. Rośnie dostępna chwilowa zdolność przenoszenia napędu, lecz pełna polityka LE wychodzi z pierwszego łuku wolniej: 80,06 zamiast 85,97 km/h. Sam transfer nie zapewnił szybszego wyjścia. Sześć łuków i profile są w summary/telemetrii.
6. Hamowanie wynika z M/P w równaniu wału i pasywnego dwukierunkowego sprzęgła, z osobnym ciepłem sprzęgła i stratą przekładni. Nie ma bezpośredniego odejmowania prędkości. Inercja wału może chwilowo nadal napędzać motocykl po zamknięciu gazu.
7. Momenty i ich rodzina podane wyżej, pełna macierz RPM/gaz w danych. Przykładowo przy 8500 RPM netto dla u=0/.1/.25/.5/1 wynosi około −7,121/−0,508/9,244/25,052/55 Nm.
8. W identycznym stanie t=8,97 s po nagłym zamknięciu gazu modelowe ax zmienia się H25 −0,0550→E −0,1086→LE −0,1103 m/s²; to pierwsze 10 ms, jeszcze z energią wału. Natywne średnie coasting w tabeli pokazują różne polityki i nie stanowią pomiarowej kalibracji.
9. Nie ma dopasowanego dowodu późniejszego odpuszczania gazu. LE ma niższą prędkość pierwszego wejścia i nie zalicza całego surowego korytarza yaw #201. Nie deklarujemy, że nowe hamowanie automatycznie pozwala później przygotować łuk.
10. β max H25 15,830°→LE 13,814°, yaw max 0,856→1,158 rad/s. Nominalne LE zalicza klasyfikator techniki poślizgu, lecz przy s=182,82 m yaw=0,239 przekracza stary próg 0,23. Inny start ma dodatkowo klasyfikowaną korektę. Zachowano wyniki odrzuceń w style-audit, bez zmiany progów.
11. Brak dopasowanego dowodu poprawy #196. Opisowy pierwszy łuk+20 m jest wolniejszy 5,953964→6,461630 s.
12. Nie: LE 49,867287 s, inny start 48,537322 s; C #200 46,386863 s, wcześniejsze B 46,260501 s. Nie mieszamy różnych warunków w procentowej przewadze.
13. Sześć Vmax przed łukami podano niżej. Odróżniamy je od maksimum całego nagrania, obejmującego ostatni fragment prostej do mety.
14. Nowe mechanizmy są mechanicznie sensowniejsze: transfer odbiera kierowanie przy mocnym napędzie i zwiększa nacisk przodu przy hamowaniu. Występuje kontrolowany uślizg i pełne ciągłe S3. Zgodność całej techniki z rzeczywistym żużlem pozostaje częściowa i niepotwierdzona pomiarem.
15. Pozostają: płaski model, quasi-statyczny pitch, brak wheelie/hopu/zawieszenia/giroskopii, gameplay roll/yaw, niezmierzony μ i charakterystyka opon, uproszczone sprzęgło i mapowy cutoff. Bardzo mały Fzf jest dopuszczalny tylko w granicach modelu, a nie dowodem poprawnego 3D zachowania.
16. Potrzebne są: h i położenie COM z zawodnikiem, naciski i przyspieszenie w czasie, motoring torque vs RPM/przepustnica, bezwładności i charakterystyka sprzęgła, rzeczywista charakterystyka koła/opon na szutrze oraz porównywalne ślady gaz/RPM/wheelspin/β/tor jazdy. Bez nich nie kalibrujemy strat do czasu okrążenia.

| Prosta przed łukiem | H25 | L | E | LE | LE inny start |
|---|---:|---:|---:|---:|---:|
| 1 | 77,18 | 68,67 | 74,52 | 65,70 | 65,95 |
| 2 | 89,35 | 84,48 | 92,57 | 87,17 | 91,01 |
| 3 | 94,71 | 87,40 | 101,64 | 82,41 | 84,50 |
| 4 | 95,07 | 89,65 | 93,49 | 83,30 | 93,15 |
| 5 | 90,22 | 88,64 | 89,40 | 88,99 | 91,73 |
| 6 | 97,77 | 86,31 | 98,62 | 91,33 | 97,46 |

## Walidacja, energia i wydajność

- 164806 sprawdzeń właściwości: bilans i znaki nacisku, sprzężone Fx/Fy, pasywność, transmisja ujemnego momentu, zachowanie mapy pełnego gazu i H25 przy wyłączonych stratach. 19032 poprawne przeliczenia kontaktów i 768 jawnych odrzuceń odciążenia, bez ujemnych nacisków. Nie klasyfikujemy odrzuconych stanów jako stabilnego rozwiązania.
- Maksymalna reszta wału/koła w tych testach 1.28e-11 J, sprzężenia ax 2e-05 m/s². Testy C#: **39/39**. Restore i Release build na Windows: 0 błędów, 0 ostrzeżeń w końcowym buildzie. Uruchomiono dobrany zestaw testów, nie pełną historyczną suitę. Cloud Windows CI nie uruchamiano; publikacja gałęzi nie wyzwala workflow ograniczonego do main/PR.
- Osiem scenariuszy: start, przyspieszanie, pełny gaz, zamknięcie, poślizg łuku, wyjście, przejście napęd→hamowanie i ponowne otwarcie po odciążeniu tyłu. To 96 przeliczeń identycznych zapisanych stanów (4 warianty ×3 dt×8), oznaczonych jako **model**, nie nowe trajektorie natywne. dt=.02/.01/.005/.0025 w 0,4 s prostej daje malejący błąd położenia; różnica .02 do .0025 około 3 mm. Nie jest to dowód zbieżności całego wyścigu dla każdej wysokości.
- Pełny ledger każdego natywnego kroku odtwarza pracę spalania mechanicznego, M/P, ΔE wału/koła/nadwozia, ciepło sprzęgła, przekładnię, łożysko, kontakt wzdłużny, kontakt boczny i opory nadwozia. Maksymalna bezwzględna reszta całego układu LE **0.019260 J/krok**, suma 0.336154 J. To reszta float Box2D, nie reszta samego wału rzędu 1e−11 J. Legacy `EnergyExcessJoules=ΔK−praca spalania` jest jednostronnym testem pasywności, nie obustronną resztą; jego ujemne wartości obejmują straty.
- Finalny kod odtwarza **25306/25306** zapisanych kroków wszystkich ośmiu prób dokładnie w siłach, wałach, skręcie i przechyle. Finalny sterownik stadionu odtwarza 991/942/998 decyzji L/E/LE na zaakceptowanych obserwacjach natywnych. Te kontrole nie tworzą świata. Nie zmieniano parametrów ani wyników natywnych po walidacji.
- Minimalny Fzf LE nominalnie 4.954 N, inny start 0.734 N: model działa blisko odciążenia. Max błąd impulsu LE 0.000825 Ns. Na krokach ≥4,9 ms max różnica ax względem sił 0.000561 m/s²; większe pochodne występują przy bardzo krótkich krokach i float, bez dużego impulsu.
- Nie ma dowodu 5/5 odporności jednej polityki. LE ma dwa różne legalne ukończenia; drugi sterownik był osobno adaptowany. Nie ogłaszamy pełnej autonomii S ani lepszej techniki na podstawie samych ukończeń.

| Wariant | p50 [ms] | p95 [ms] | p99 [ms] | max [ms] | CPU biegu [s] |
|---|---:|---:|---:|---:|---:|
| H25 C #200, archiwum | 2,321 | 5,612 | 6,231 | 8,230 | 2,875 |
| H26-L | 4,369 | 9,409 | 11,649 | 14,974 | 5,094 |
| H26-E | 2,339 | 5,788 | 6,518 | 8,742 | 2,891 |
| H26-LE | 4,692 | 10,318 | 11,635 | 14,424 | 5,484 |
| H26-LE inny start | 4,912 | 10,415 | 12,556 | 14,554 | 5,516 |

Wszystkie zmierzone decyzje nowych pełnych biegów mieszczą się w 50 ms. Weryfikacja początkowego pełnego certyfikatu jest przygotowaniem startu, zapisywanym oddzielnie od decyzji FAST50. Nie ma uczenia globalnego co tick. H26 jest droższe przez lokalne sprzężenie nacisków; koszty offline i koszty pełnego biegu pozostają rozdzielone.

## Stadion i odtwarzanie

Zwykły `uruchom-stadion.ps1` zachowuje dawny domyślny wariant. Dodano grupę **#202 — nowa fizyka H26**: L/E/LE z trzema osobno wyuczonymi politykami solo 8:1; H25+C #200 jest nadal dostępne. Inne ustawienia H26 są odrzucane przed utworzeniem świata z jawnym komunikatem o niekompatybilności. Hipotezy fizyki są oznaczone w UI. Historycznej paczki ZIP #192 nie nadpisano.

Archiwum zawiera osiem rzeczywistych nagrań #202, w tym trzy DNF starej polityki. Telemetria pokazuje Fz i transfer, gaz/RPM/κ/β/yaw, Fx/Fy, momenty brutto/oporu/sprzęgła/koła, energię i jej resztę. Porównanie nakłada rzeczywiste trajektorie H25 i H26. Chrome: osiem replayów, telemetria, porównanie i odrzucenie niekompatybilnego 8,4:1 zaliczone, 0 nowych światów. Nowego pełnego POST z UI nie wykonywano po wyczerpaniu limitu; backend i polityki sprawdzono powyższą dokładną kontrolą finalnych sił i decyzji.

Dane źródłowe, nieudane próby rozwojowe, certyfikaty i hashe są w `data/experiments/speedway202`. Replaye są w `tools/replay-viewer/recordings/h25/202-*.json.gz`. [Instrukcja narzędzia](../../../tools/speedway-h26/README.md) opisuje odtwarzanie kontroli bez tworzenia świata. Parametry pozostają zamrożone i niezwalidowane pomiarowo. Nie rozpoczęto kolejnego badania.
