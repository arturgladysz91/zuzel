# Badanie #157 — transfer świadków do przyczynowego sterownika

**Wynik: częściowy transfer; cel autonomicznego AI niezaliczony.** Mały model bieżącego stanu odtwarza w prognozie siedem legalnych manewrów #156 z błędem czasu poniżej 0,001 s. Tylko stan historyczny uzyskuje pełny certyfikat 13/9. W natywnym Box2D sterownik podejmuje tam 15 kolejnych certyfikowanych decyzji, lecz przy 29,8 s kończy ABSTAIN. Odłożone kolejki pierwszego i drugiego łuku nadal nie mają pełnej bezpiecznej rezerwy. Nie uruchomiono C ani D; nie szukano kolejnego rekordu offline.

Baza: dokładnie `9fe4d6612ce13c135fa967d48faa85509257d129`, Draft PR #155. Nowa gałąź `experiment/state-maneuver-transfer-157`. Wszystko OFF bez jawnego polecenia badawczego. Bez merge. Oryginalne źródła fizyki, parametry, geometria, opony, μ, silnik, dane i badania #156 pozostają niezmienione.

## Świadki i mechanizm zysku

Nazwy dopasowano do rzeczywistych zapisów. Czasy 12,495242 / 12,730009 / 12,932380 s odpowiadają `historical-B-T1-refined`, `historical-C-T1-refined`, `second-C-T1-refined`. Wcześniejsze `final` nie są tymi samymi wynikami. Udany transfer B to `held-first-B-refined-final`. Dane tej odłożonej próby służą wyłącznie analizie; nie trenują modelu.

| Natywny świadek #156 | Czas [s] | Droga [m] | V̄ [km/h] | Gaz 0 w łuku [s] | Straty boczne / wzdłużne [kJ] | Pierwsza różnica od T0 [s] |
|---|---|---|---|---|---|---|
| first-A-T1-final | 11.175884 | 203.113 | 65.427 | 2.712 | 16.751 / 38.786 | 11.199999999999992 |
| first-B-T1-refined | 12.128827 | 212.631 | 63.112 | 4.400 | 21.427 / 47.181 | 6.6000000000000005 |
| first-C-T1-refined | 13.508601 | 230.456 | 61.416 | 2.667 | 18.648 / 35.728 | 11.99999999999999 |
| second-B-T1-refined | 12.950551 | 239.240 | 66.504 | 3.800 | 29.214 / 16.753 | 13.599999999999984 |
| second-C-T1-refined | 12.932380 | 254.267 | 70.780 | 3.504 | 22.074 / 20.788 | 17.39999999999997 |
| historical-B-T1-refined | 12.495242 | 252.566 | 72.767 | 3.553 | 25.022 / 15.613 | 26.799999999999937 |
| historical-C-T1-refined | 12.730009 | 260.950 | 73.796 | 3.782 | 30.842 / 23.615 | 29.399999999999928 |
| held-first-B-refined-final | 11.928827 | 210.865 | 63.637 | 4.400 | 21.427 / 42.399 | None |


Historyczne 4,256% to przede wszystkim krótsza droga: **-4.884% drogi**, przy **-0.656% średniej prędkości** względem C/T0. Dokładna tożsamość ln(T₁/T₀)=ln(L₁/L₀)−ln(V̄₁/V̄₀) daje -0.043493=-0.050077+0.006584. To rozkład kinematyczny, nie niezależny eksperyment przyczynowy gaz kontra yaw. Gaz i yaw umożliwiają krótszą wykonalną trajektorię; nie można przypisać im osobnych addytywnych sekund z tych zapisów.

Wspólna cecha szybszych manewrów to skoordynowanie przygotowania, yaw i późniejszego wykorzystania gazu z rzeczywistą długością toru. Nie ma wspólnej zasady „szerzej”, „ciaśniej” ani „mniej gazu 0”. Na pierwszym łuku wygrywa A, na drugim C minimalnie wyprzedza B, w historycznym oknie wygrywa B. Historyczne C/T1 zmniejsza czas gazu 0 o 0,144 s względem C/T0, ale jego minimum i średnia prędkość w samym łuku są niższe. Drugie B/T1 jest szybsze mimo 0,2 s więcej gazu 0. Minimalizowanie czasu bez gazu osobno nie jest poprawnym celem.

Pełne momenty obserwacji i aktywacji, każda zmiana kierownicy/gazu, β, yaw, wheelspin, XY, krzywizna z wektora prędkości, Fx/Fy oraz pending są w `commands/*.csv`. Pełna ewolucja sił, pracy i strat co 0,01 s jest w sąsiednich `*-intervals.csv.gz`, zachowanych z oryginalnych natywnych logów. Zmiany offsetu i krzywizny opisują wykonaną linię, nie narzucony promień.

## Co rzeczywiście wykonuje nowy sterownik

450 nieuporządkowanych przykładów obserwacja→akcja pochodzi z siedmiu najlepszych uczących manewrów A/B/C oraz krótkiego oryginalnego startu. Wektor zawiera lokalną fazę geometrii, offset, prędkość, β, yaw, błąd kursu, rzeczywisty skręt, koło/wał, aktywną akcję i pierwszą/ostatnią oczekującą komendę z względnym czasem aktywacji. Nie zawiera czasu absolutnego, numeru próbki, źródła, następnego stanu ani przyszłych natywnych punktów. Komendę wydaną w tej samej chwili usunięto z cech pending, aby uniknąć wycieku etykiety. Model jest osadzony w nowym narzędziu; wykonanie polityki nie otwiera plików przyszłej taśmy.

Najbliższy przykład dla każdej z trzech rodzin proponuje bieżącą akcję. Generator przelicza ją ponownie z każdej własnej prognozowanej obserwacji. Sprawdza maksymalnie dziewięć rolloutów: trzy rodziny × brak korekty lub mała sprzężona korekta. Kandydaci muszą ukończyć wspólny geometryczny cel i oryginalną pełną rezerwę; ranking to czas celu, następnie rzeczywista długość XY. Brak nagrody za konkretną linię albo gaz. Własne legalne fragmenty są zachowywane jako propozycje, a nie stare certyfikaty. Naprawa używa istniejącego fizycznego odwrotnego modelu yaw/slip `Joint150.Correct`, korygującego jednocześnie gaz i kierownicę. Własne alternatywne gałęzie przechodzą ponowny rollout.

Protokół bezpieczeństwa pochodzi mechanicznie z zamrożonego #155: 13 węzłów / 9 pełnych rezerw, trzy skończone skorelowane innowacje po każdym z dwóch przeglądów, osobny niezależny preflight. Jest to certyfikat dla tego skończonego zbioru innowacji, nie globalna ciągła odporność. Reakcja 0,24 s, przeglądy 0,2 s, pełny obrys i swept ≥0,5 m, β45°, oryginalny aktuator i Box2D pozostają obowiązujące. Zmniejszono limit pracy do 8192 prognoz i 12 CPU-s na decyzję. Brak K1. Model i wszystkie pięć plików algorytmu zamrożono przed pierwszym B; hashe nadal pasują.

## A — odtworzenie oraz rzeczywiste obserwacje

| Stan / rodzina | Prognoza #157 [s] | Natywny świadek #156 [s] | Różnica [ms] | Status |
|---|---|---|---|---|
| first / A | 11.176486 | 11.175884 | 0.603 | Tylko prognoza do celu #156 |
| first / B | 12.128791 | 12.128827 | -0.036 | Tylko prognoza do celu #156 |
| first / C | 13.508424 | 13.508601 | -0.177 | Tylko prognoza do celu #156 |
| historical / B | 12.495290 | 12.495242 | 0.048 | Tylko prognoza do celu #156 |
| historical / C | 12.729997 | 12.730009 | -0.012 | Tylko prognoza do celu #156 |
| second / C | 12.932396 | 12.932380 | 0.015 | Tylko prognoza do celu #156 |
| second / B | 12.950848 | 12.950551 | 0.297 | Tylko prognoza do celu #156 |


Powyższe czasy nie są nowymi natywnymi PASS. Generator umie odtworzyć lokalny manewr, lecz pełny certyfikat wymaga dalszej rezerwy przez kolejny łuk. Pierwszy stan odrzuca nominalne A/B/C dopiero w dalszym wejściu: odpowiednio ref. 216,795 / 220,955 / 213,758 m przez β45°. To dalej niż wspólny cel #156 206,389 m, ale wcześniej niż wymagany terminal 350,779 m. Nie skrócono rezerwy, żeby uzyskać PASS.

| Próba natywna #157 | Certyfikowane decyzje | Nowe okno [s] | Koniec [s] | Pełny manewr | Powód |
|---|---|---|---|---|---|
| native-A-historical-v1 | 4 | 0.800 | 27.600 | NIE | experiment timeout |
| native-A-window-v1 | 15 | 3.000 | 29.800 | NIE | technical invariant: STUDY157_ABSTAIN: abstain: finite recourse search has no certified complete reserve |
| native-B-held-first-v1 | 0 | 0.000 | 1.800 | NIE | technical invariant: STUDY157_ABSTAIN: abstain: finite recourse search has no certified complete reserve |
| native-B-held-second-v1 | 0 | 0.000 | 10.400 | NIE | technical invariant: STUDY157_ABSTAIN: abstain: finite recourse search has no certified complete reserve |


Historyczne 15 decyzji obejmuje 26,8–29,6 s; przy obserwacji 29,8 s następuje ABSTAIN. Pierwszy cel wynosił ref. 510,168 m, rezerwa 669,557 m. Po osiągnięciu celu trzeba certyfikować nowy cel 669,557 m i terminal 828,947 m. W tym nowym stanie dziewięć wygenerowanych kontynuacji odpada przez obrys lub β, najdalsza C dochodzi do 691,016 m. Stary certyfikat do 669,557 m nie pokrywa nowego terminalu. Ponowny model z bieżącego stanu również nie odzyskuje rezerwy. Przy lokalnym rankingu historyczne B/C różnią się tylko około 21 µs do wcześniejszego celu; taka przewaga nie dowodzi szybszego całego przyszłego manewru.

## B — pierwsze wyniki odłożone i przyczyna transferu

| Pierwszy odłożony test | Certyfikat | CPU decyzji [s] | Prognozy | Niezależne prognozy |
|---|---|---|---|---|
| B-held-first-v1 | ABSTAIN | 0.516 | 137 | 0 |
| B-held-second-v1 | ABSTAIN | 0.641 | 160 | 0 |
| B-perturb-v1-position--1 | PASS 13/9 | 0.375 | 1154 | 535 |
| B-perturb-v1-position-1 | PASS 13/9 | 0.375 | 1155 | 535 |
| B-perturb-v1-velocity--1 | PASS 13/9 | 1.609 | 1164 | 535 |
| B-perturb-v1-velocity-1 | PASS 13/9 | 0.578 | 1153 | 535 |
| B-perturb-v1-yaw--1 | PASS 13/9 | 0.422 | 1154 | 535 |
| B-perturb-v1-yaw-1 | PASS 13/9 | 0.438 | 1164 | 535 |


Perturbacje to ±0,02 m/s, ±0,001 rad/s yaw oraz ±0,001 m poprzecznego położenia w historycznym stanie. Są jawnymi hipotetycznymi stanami do prognozy; nie wstrzyknięto ich do Box2D. Sześć lokalnych PASS nie zastępuje testu innych kolejek ani pełnego przejazdu. Dla obu rzeczywistych held kolejki zostały odtworzone przez własny historyczny prefiks od zatrzymanego startu; pełny klucz początkowy jest identyczny. Żadna niecertyfikowana akcja #157 nie została wykonana.

| Held / nauczyciel | Identyczne ciało na początku | Δpozycji po 0,2 s [m] | ΔV [m/s] | Δyaw [rad/s] | Pierwsza utrata prognozy [s] | Przywrócenie wyłącznie kolejki nauczyciela |
|---|---|---|---|---|---|---|
| held-first / first-A-T1-final | True | 0.003913 | 0.050246 | -0.076402 | 7.600 | Kompletny legalny forecast |
| held-first / first-B-T1-refined | True | 0.000000 | 0.000000 | 0.000000 | 13.800 | Kompletny legalny forecast |
| held-second / second-B-T1-refined | True | 0.039692 | 0.663558 | -0.709383 | 11.600 | Kompletny legalny forecast |
| held-second / second-C-T1-refined | True | 0.033833 | 0.523447 | -0.498469 | 12.000 | Kompletny legalny forecast |


To kontrolowana diagnoza przyczynowa, nie nowy natywny PASS: w każdej parze stan ciała, koło, wał, skręt i aktywna komenda są identyczne, zmieniona jest wyłącznie pending. Dwie skrajnie różne nowe akcje prowadzą do identycznego następnego ciała, bo ich aktywacja nastąpi po obserwacji. Odłożony A utrzymuje dawny pełny gaz zamiast wcześniejszego polecenia zamknięcia i skrętu; efekt narasta aż do β45 przy 7,6 s. Held B pierwszego łuku ma tę samą skuteczną kolejkę i przechodzi. Dla drugiego B/C kolejka zmienia yaw już w pierwszym nieodwracalnym kroku; przepisana dalsza taśma kieruje motocykl do bandy. Prognoza wykrywa utratę obrysu przy 11,6/12,0 s. Stare natywne próby #156 kończyły wcześniej przy utracie swept: B 0,446126 m i C 0,492576 m, mimo dodatniego odstępu pozy. Czasy prognozy i natywnego pierwszego STOP nie są tym samym pomiarem.

Warunkowy model rozpoznaje pending jako cechę, lecz siedem nominalnych demonstracji nie uczy skutecznej korekty dla innej historii komend. Odtworzenie dobrych trajektorii z nominalnych stanów nie dostarcza zbioru odzyskiwania po odchyleniach ani rezerwy po przesunięciu celu. Mała korekta stałego znaku przez cały rollout także nie zapewnia odzyskania. To konkretna granica tego modelu; nie dowód niemożliwości bezpiecznego rozwiązania. Nie dostrajano algorytmu po pierwszych wynikach B.

## CPU, bramki i porównanie z AI

W 15 kolejnych natywnych decyzjach CPU wynosi 0.265625–1.468750 s, średnio 0.384375 s. To nadal więcej niż okres 0,2 s. Ograniczony generator jest znacznie mniejszy od poszukiwania offline, ale nie spełnia jeszcze wymogu wykonania w czasie rzeczywistym. Profil każdej decyzji rozdziela generację, propagację fizyki, korekty, certyfikację i niezależne sprawdzenie. `decisions.csv`, pełne JSON-y i odrzucone kontynuacje pokazują wszystkie liczniki, także ABSTAIN. Wszystkie K1Calls=0; dzielone prefiksy i własne plany pozostają aktywne.

Bramka A: częściowo — siedem lokalnych odtworzeń, jeden obszar stanów z certyfikatem i krótka seria natywna. B: częściowo — 6/6 małych zmian w tym obszarze, ale obie inne kolejki odmówione. **C: nie uruchomiono**; nie ma wykazanej stabilności i poprawy na kilku stanach. **D: nie uruchomiono**, zależy od C. Cztery nowe natywne światy obejmują dwa historyczne prefiksy diagnostyczne i dwie odmowy held, nie pełne autonomiczne biegi od startu. Oryginalne prefiksy L pozostają L.

T0 z #156 jest sterownikiem warunkowanym rodziną linii. Nie jest niezmienionym autonomicznym AI. Oryginalne `Policy144.Choose` w kontrolach `UNCONDITIONED` #156 nie ukończyło legalnie tych okien S. Oddzielny autonomiczny S0 z #154 ma zachowany natywny prefiks do ABSTAIN przy 13,8 s, nie pełny czas wspólnego celu #156. Nowe #157 także nie kończy żadnego z wymaganych autonomicznych manewrów z pełną ciągłością. **Nie ma podstaw do stwierdzenia poprawy czasu względem oryginalnego AI ani pełnego S.** Wygrana 4,256% pozostaje własnością starego diagnostycznego świadka.

Mechanizm da się przenieść jako generator lokalnych propozycji na drugi i historyczny łuk, co pokazują prognozy. Nie wykazano niezawodnego samodzielnego wyboru szerokości: ranking odróżnia rodziny w nominalnych stanach, ale krótszy cel może wybrać inną rodzinę niż pełny manewr, a nowe pending prowadzi do odmowy. Następny potrzebny krok to przyczynowe demonstracje odzyskiwania po innych kolejkach i domknięcie rezerwy przy zmianie celu. Nie wymaga to zmiany fizyki ani ponownego szukania rekordowego otwartego manewru.

Budżet: `budget.json` zawiera wszystkie nowe eksperymenty, szkolenie, diagnostykę, zapis i eksport. Limit 1000 CPU-s / 15 światów. Początkowy błąd nazwy osadzonego zasobu zachowano jako techniczną porażkę i obciążono konserwatywnie 10 CPU-s; nie powstał świat ani prognoza. Kompilacja i regresje/CI nie są eksperymentalnymi rolloutami. Ostateczne liczby: 119.359000 CPU-s i 4/15 światów.

## Artefakty

- [Replay porównawczy](Replay-157.html) — wyłącznie prawdziwe natywne próbki, osobno #156 i prefiksy #157.
- [Linie i prędkości](plots/xy-speed.png), [siły/β/yaw/energia](plots/physics.png).
- [Decyzje i CPU](decisions.csv), [odtworzenie generatora](generation.csv), [natywne okna](native.csv), [pełna analiza](analysis.json).
- `commands/`: dokładne zdarzenia i interwały fizyki. `native/`: cztery nowe oryginalne logi. `causal-v1.json.gz`: kontrfaktyczne prognozy pending.
- [Walidacja](VALIDATION.md).
