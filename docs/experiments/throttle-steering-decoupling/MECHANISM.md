# Co ograniczyło te manewry

Werdykt **PHYSICS LIMITATION** dotyczy zamrożonych stanów i skończonego zbioru korekt. Nie oznacza globalnej niemożliwości jazdy na gaz1 ani identyfikacji modelu opon jako jedynej przyczyny. Wszystkie wartości poniżej pochodzą z natywnego Box2D i ostatniego zaakceptowanego przedziału, bez dodatkowych zapytań o siły.

## D0: korekta opóźnia przekroczenie obwiedni, lecz go nie usuwa

Obserwacja nastąpiła 20,665 m przed pierwszym wyjściem, w t=5,4 s. Oryginalny aktywny gaz wynosił 0. B przygotował kierownicę około 0° od t+0,24 s i otrzymał aktywny gaz1 od t+0,44 s. Nie nastąpiło natychmiastowe otwarcie w punkcie geometrycznym.

|Ostatni stan|A: bez korekty|B: wcześniejsze ustawienie kierownicy|
|---|---:|---:|
|Czas od obserwacji|1,13 s|1,25 s|
|β|45,90°|46,25°|
|Yaw rate|2,930 rad/s|2,875 rad/s|
|Prędkość|58,68 km/h|61,47 km/h|
|Kierownica fizyczna i żądana|1,5°|około 0°|
|Moment przedniego kontaktu|+592,01 Nm|+591,94 Nm|
|Moment tylnego kontaktu|−451,72 Nm|−456,23 Nm|
|Wypadkowy moment yaw|+140,28 Nm|+135,71 Nm|
|Fx tylnego kontaktu|649,01 N|640,16 N|
|Fy tylnego kontaktu|752,87 N|760,38 N|
|Wykorzystanie przodu / tyłu|0,990 / 0,997|0,989 / 0,997|
|Wzdłużny slip ratio|2,700|2,559|
|ω tylnego koła|130,76 rad/s|130,89 rad/s|
|Silnik|11 225,7 rpm|11 238,0 rpm|
|Dostępny moment na kole|229,28 Nm|225,62 Nm|
|Certyfikowany prześwit|+0,54 m|+1,40 m|

Moment +135,71 Nm przy bezwładności 52,72097 kg·m² daje około **+2,57 rad/s² przyspieszenia yaw**. B przekracza obwiednię mimo dodatniego prześwitu. To nie jest DNF od bandy.

Przednia opona przy dużej bocznej prędkości kontaktu (około −10,11 m/s) wykorzystuje niemal pełną siłę poprzeczną i daje około +592 Nm. Tył również wykorzystuje prawie cały swój budżet, ale dzieli go między około 640 N napędu i 760 N siły poprzecznej. Jego przeciwny moment około −456 Nm nie równoważy momentu przodu. Całkowita siła tylna nie jest zerowa, a problem nie jest stałym brakiem dostępnego momentu silnika.

Wheelspin podnosi prędkość obwodową koła do około 43,2 m/s przy prędkości motocykla około 17,1 m/s. Silnik pracuje wysoko na niezmienionej krzywej i dostępny moment spada. Wysoki slip, boczny ruch kontaktów i budżet dzielony Fx/Fy pozostają sprzężone. Nie dodaliśmy wspomagania ani nowego ogranicznika.

Prędkość kierunku rzeczywistego ruchu oszacowana z ostatnich natywnych sił i krzywizny wynosi około 0,63 rad/s, podczas gdy korpus obraca się 2,88 rad/s. Różnica powiększa β. **Ograniczenie obrotu korpusu i poprawa kierunku jazdy są różnymi rzeczami.** Kierownica osiągnęła już żądane około 0°; w tej chwili nie zatrzymuje jej limiter90°/s. Samo wcześniejsze dojście do tego kąta nie zapewnia stabilnego bilansu momentów.

Korekta B opóźniła DNF o0,12s i zwiększyła prędkość ostatniego stanu o2,79km/h. Nie są to zyski legalnego pełnego przejazdu. D0-D z identycznymi kątami i częściowym gazem ukończył dwa łuki oraz dodatkową prostą. D0-C, przy częściowym gazie i utrzymanej kierownicy, utracił certyfikację. To bezpośredni przykład interakcji gazu i kierownicy.

## Inny mechanizm końcowy nie zawsze oznacza β45

D1-B osiągnął badane drugie wyjście przy69,79km/h, ale po1,58s od obserwacji utracił certyfikację. Ostatni stan miał β≈−42,08°, yaw≈−2,71rad/s i certyfikat≈−0,058m. Momenty miały przeciwne znaki: przód≈−590,95Nm, tył≈+455,21Nm. Zmieniony wcześniej skręt odwrócił rotację, lecz nie zapewnił bezpiecznego kierunku ruchu. Utrata certyfikacji jest wynikiem konserwatywnej bramki obrysu; nie udajemy potwierdzonego kontaktu z bandą.

W potwierdzającym H0-B stan końcowy ma już fizyczną kierownicę−2°, ale przód nadal daje około+590,76Nm, a tył−461,51Nm. β osiąga45,26° mimo dodatniego prześwitu1,64m. Sama niewielka kontra nie zmienia dominującego znaku przedniej siły w tym rozwiniętym stanie uślizgu. Dostępna większa przestrzeń toru nie usuwa tego konkretnego DNF.

## Co rozstrzygnięto, a czego nie

- Ograniczenie bieżącego stanu: lokalna prognoza H09 może być poprawna, podczas gdy trzymanie wybranego manewru dłużej narasta do DNF. Natywne sondy nie certyfikują kontynuacji po H09.
- Wcześniejsze przygotowanie: przetestowano konkretne legalne0,2s przewagi kierownicy przed gazem. Nie wyczerpano innych ustawień β, yaw, prędkości, pozycji i czasu przygotowania.
- Model opon: nasycony przód i połączony Fx/Fy tyłu wytwarzają zmierzony niezrównoważony moment. Bez empirycznej charakterystyki kontaktów nie rozstrzygamy, czy ten bilans jest realistyczny.
- Decyzje AI: w18 prognozach samo sterowanie obniża koszt yaw w10 przypadkach; w2 przypadkach #137 wybierał gaz0 mimo pełnego gazu z tą samą klasą H09 i mniejszym kosztem yaw w nowej siatce. To ograniczenie lokalnego wyboru, nie dowód legalnego pełnego gazu. Nowy autonomiczny prototyp nie powstał, bo bramka legalnych powtarzalnych manewrów była zamknięta.

Kolejny krok powinien oddzielnie zmierzyć realną odpowiedź przodu i tyłu: siły przy zadanych slip angles/ratios, wpływ kierownicy na moment yaw i koło/RPM pod obciążeniem. Dopiero takie dane mogą uzasadnić osobną kalibrację. W #138 nie zmieniono żadnej z tych praw fizyki.

[Wszystkie DNF](dnf.json) · [Efekty czynnikowe H09](factorial-effects.json) · [Pełne siły, slip i actuator w CSV](telemetry/) · [Klipy MP4](clips/manifest.json)
