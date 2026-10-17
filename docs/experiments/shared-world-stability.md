# Stabilizacja #87 — diagnoza przed zmianą

Punkt odniesienia: `ca5339bc2bdd370d9e82ad889b76d8929014adcc`; HEAD Draft #87 zweryfikowany przed pracą. Oryginalne wejście G-dense-stress pozostaje niezmienione. Nie dodajemy ścian, fizyki leżącego motocykla ani nowej taktyki.

Pierwsza istotna różnica występuje **A: przed kontaktem, w 0,26 s**. Kryterium diagnostyczne: różnica prędkości >0,05 m/s lub położenia >0,01 m, na wspólnych fizycznych chwilach co 20 ms. Do 0,24 s wszystkie ciała stoją identycznie. Nie porównujemy indeksów kroków ani nie używamy interpolacji jako obserwacji w runtime.

| R1, czas 0,26 s | 20 ms | 10 ms | 5 ms |
| --- | ---: | ---: | ---: |
| vx (m/s) | 0 | 0,097937 | 0,113865 |
| vy (m/s) | 0 | -0,042604 | -0,104888 |
| x (m) | 0 | 0,000612 | 0,001028 |
| żądana siła boczna na początku następnego kroku (N) | -604,971 | -586,267 | -558,896 |
| faktyczna składowa siły świata Y (N) | -604,970 | -1092,357 | -1353,303 |

**Mechanizm 1 — deadline startu.** TapeReactionSeconds=0,240000002846… s jest sprawdzany dopiero na granicy kroku. Start napędu wypada w 0,260/0,250/0,245 s. Zmiana kroku zmienia więc fizyczne wejście regulatora jeszcze przed jazdą.

**Mechanizm 2 — niespójna baza sił.** Błąd i prędkość poprzeczna pochodzą z osi toru, ale napęd i siła boczna działają w bazie znormalizowanej rzeczywistej prędkości. Przy rozruchu z zerowej prędkości pierwszy impuls regulatora ustala tę bazę. Napęd obraca się następnie za wywołanym dryfem. W 5 ms, przy żądaniu bocznym -558,896 N, faktyczne Fy wynosi -1353,303 N. Yaw również podąża za wektorem prędkości, więc dostaje obrót wytworzony tym sprzężeniem. To nie jest różnica wywołana zderzeniem ani progiem Crash.

**Mechanizm 3 — niepełne ograniczenie siły.** Ograniczono tylko komponent boczny, a napęd dodano niezależnie. R4/5 ms w 0,9 s otrzymuje moduł siły równy **1,2033** dostępnego budżetu. Potrzebne jest wspólne ograniczenie wektora, bez zwiększania przyczepności.

Pierwszy kontakt R1/R2 występuje dopiero w 0,915/0,7975/0,7375 s. Prędkość zamykania w jego punkcie wynosi 1,2688/2,4283/3,3415 m/s. W telemetrycznej rekonstrukcji `v + omega cross r` zgadza się z `manifold.normalVelocity` (różnice pojedynczej precyzji), więc duża wspólna prędkość postępowa nie jest błędnie użyta jako severity. Miara bierze jednak wyłącznie pierwszy punkt aktywnego epizodu; jej adekwatność do skończonego uderzenia trzeba zbadać po usunięciu wcześniejszego źródła różnicy. Nie zmieniamy progów skutków.

Dodano opcjonalną obserwację sił, punktów styku i rezerw. Dla wszystkich trzech kroków pełny hash zaakceptowanych stanów z obserwacją jest dokładnie równy zachowanemu DLL z ca5339b. Krótkie okna: 0,24–0,42 s dla pierwszej rozbieżności i 0,70–1,08 s dla pierwszych styków. Dane: `shared-world-stability-before-*.csv`. Nie zapisujemy całej historii wewnętrznych kroków biegu.

Odtworzenie samej diagnozy: `dotnet run --project tools/shared-world-speedway -c Debug -- diagnose stability-diagnosis`. Powyższe liczby odnoszą się do wersji przed naprawą; kolejne sekcje opiszą sprawdzone zmiany i wynik.
