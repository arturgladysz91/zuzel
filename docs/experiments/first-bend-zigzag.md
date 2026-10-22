# Zygzak pierwszego łuku — audyt #127, PARTIAL

**Nie udowodniono eliminacji zygzaka. Nie dodano regulatora ani nowej jazdy.**
Audyt potwierdza niekorzystne przejmowanie sterowania przez impuls #126, lukę
w koszcie końcowego β oraz wrażliwość jednej decyzji na horyzont. Duży cykl
odchylenia występuje również w baseline #117. Nie da się uczciwie przypisać go
wyłącznie jednemu z tych mechanizmów. Status PROVISIONAL / PARTIAL.

Baza: dokładny HEAD Draft #126 `1b3c43c4dcb54b5a925912ecf75c48edf1e58f4d`,
sprawdzony w Git i metadanych PR. Rodzic poprzedniego eksperymentu: #124
`d8d7e959eab00d0b41236aa6292ab04f3c15f3fa`. Gałąź `audit/first-bend-zigzag-127`.
Nie zmieniono CoreSim, WorldRunner, #117, kontaktów, gazu, reakcji, geometrii,
opon, silnika, masy ani bezwładności. Wszystkie pliki danych #126 pozostają
bajtowo identyczne z ich manifestem. Nie wykorzystano wykluczonej pierwszej próby ON.

## A. Gdzie faktycznie występuje zygzak

Konwencja projektu to **β = wrap(ψ − χ)**, zgodna z `TravelDirectionState`
i zapisanym CSV. Wzór przeciwny w treści zadania nie zmienia interpretacji danych.
Definiujemy dodatkowo **η = wrap(χ − styczna planu)**. η nie jest wyznaczonym
optymalnym kierunkiem powrotu na linię; przy istniejącym błędzie potrzebna może
być niezerowa η. Dlatego sama zmiana znaku kierownicy lub η nie jest dowodem błędu.

Największy pełny cykl odchylenia kierunku w kandydacie zajmuje **3,90–9,20 s**:
dodatni płat η 3,90–6,43 s, potem ujemny 6,43–9,20 s. Najbardziej wyraźne
odwrócenie faktycznej jazdy występuje **5,70–6,82 s**, kiedy χ maleje; wcześniej
yaw zmienia znak w **5,51 s**, a wraca do dodatniego w **6,78 s**. Wszystkie
sekundy odnoszą się do natywnych próbek, zwykle co10ms. Dokładne przedziały
bracketingu i interpolowane oszacowania przejść przez zero zawiera `analysis.json`.
Nie przypisujemy danym dokładności czasowej większej niż zapis.

| Zdarzenie | Baseline #124 | Kandydat #126 |
| --- | ---: | ---: |
| Dodatnie maksimum błędu normalnego | +3,274 m w3,91 s | +4,235 m w3,93 s |
| Ujemne maksimum błędu | −5,906 m w6,42 s | −5,375 m w6,43 s |
| Maksymalna dodatnia η | +32,005° w5,62 s | +34,181° w5,63 s |
| Kolejna ujemna η | −15,726° w7,15 s | −16,320° w7,15 s |
| Pierwszy powrót do linii po dużym ujemnym błędzie | 8,81 s; po2,39 s | 8,62 s; po2,19 s |
| Następny mały dodatni błąd | +0,363 m w9,35 s | +0,480 m w9,19 s |

„Powrót” oznacza pierwsze przecięcie e=0, nie trwałe odzyskanie stabilności:
później następuje kolejne przekroczenie. Wspólne okno1,2–10,3 s zawiera **trzy
zamknięte przestrzelenia linii w obu zapisach**. Liczymy od przecięcia e=0 do
następnego przecięcia, podając szczyt e i η; nie liczymy znaków kierownicy.
Metryka jest opisowa, nie certyfikuje, że każde z tych przekroczeń było zbędne.
Okno obejmuje też najazd i krótki odcinek wyjścia poza łuk. Surowe ekstrema
i próg rozdzielania małych odwróceń są jawne w `protocol.json` i `analysis.json`.

### Kolejność przyczynowa, którą da się potwierdzić

1. **1,20 →1,44 s:** pierwsza decyzja −0,9° po pełnej reakcji0,24 s. Do1,44 s
   baseline i kandydat mają ten sam stan. Impuls ustawia cel około−0,533°
   z zaakceptowanego+0,367°. Yaw przechodzi z dodatniego w ujemny w1,47 s;
   baseline po1,42 s pozostaje dodatni. To dodatkowa krótka zmiana yaw, nie
   jeszcze duży zygzak XY.
2. **1,54 s:** kończy się override, #117 żąda około+0,905°; żądanie odbija
   o1,438°. Kąt wykonany pozostaje ciągły i ograniczony90°/s. Yaw staje się
   dodatni w1,59 s. Kolejny impuls1,84–1,94 s ponownie obniża yaw i kończy
   się skokiem żądania+1,720°. χ reaguje inaczej i później niż orientacja.
3. **2,35–3,93 s:** kandydat przekracza linię i oddala się do+4,235 m. Nie ma
   w tym czasie kolejnych dodatkowych impulsów. W4,15 s osiąga maksymalne β
   13,864°; aktywny gaz został odjęty w4,14 s. Również w4,15 s oryginalny
   #117 skacze z żądania−4,871° do+35°. Baseline też żąda+35° w4,15 s,
   z−3,615°. Późne impulsy rozpoczynające się5,44 s nie mogą być przyczyną
   wcześniejszego zdarzenia. Udział pierwszych dwóch impulsów w jego amplitudzie
   pozostaje nierozdzielony od późniejszego sprzężenia #117/gazu/planu.
4. **4,97 /5,25 /5,43 s:** #117 odwraca żądania kolejno do−3,606°, −35°,
   −9,365°. Kierownica nie teleportuje się do tych wartości. W5,44 s wchodzi
   dodatkowy impuls; yaw przechodzi przez zero w5,51 s. Zwolnienie override
   w5,54 s zmienia żądanie o+2,287°. W5,55–5,57 s #117 dodatkowo przeskakuje
   na+35° i wraca. Te szybkie wybory są widoczne obok rzeczywistego kąta.
5. **5,63–7,15 s:** η osiąga+34,181°, dopiero potem χ zaczyna maleć w5,70 s.
   Błąd linii nadal rośnie do modułu5,375 m w6,43 s. Yaw wraca do dodatniego
   przed χ̇; następny płat η osiąga−16,320° w7,15 s. Impulsy5,84/6,24/6,64/
   7,04/7,44 s nakładają krótkie zaburzenia na ten duży cykl. Nie są to
   niezależne, izolowane skutki, które można zsumować z samego CSV.

Pełna wspólna oś czasu zawiera XY/plan, ψ/χ/β, yaw i przyspieszenie yaw,
rzeczywisty/żądany skręt, siły kontaktów, budżety i wykorzystanie opon, gaz,
prędkość oraz moc strat. Siły A/C z identycznego stanu są osobno oznaczone
jako zapytania kontrfaktyczne. [Wszystkie10 impulsów i pomiary](first-bend-zigzag/native-pulses.md).

## B. Co faktycznie robi twardy override

`PredictiveRiderControl.SteeringOverride` utrzymuje `acceptedSteering + correction`
przez0,1 s. W `TwoContactBikeDynamics.Forces` warunek
`EnablePhysicalSteeringControl && steeringOverride is null` pomija #117, gdy
wartość jest obecna. Potwierdzono to w **100 rzeczywistych interwałach, łącznie1 s**,
z czego0,8 s przypada na pierwsze osiem impulsów. Odtworzenie wszystkich sił
i kierownicy z zapisanego stanu do11,3 s ma maksymalny błąd **0**.

Korekta−0,9° jest różnicą względem zaakceptowanego kąta w chwili aktywacji,
nie względem późniejszych poleceń #117. Na zaakceptowanej trajektorii B jej
efektywna różnica od bieżącego żądania #117 wynosi od **−1,942° do+0,926°**.
W jednym fragmencie ma nawet przeciwny znak niż nominalna korekta. Powrót
do #117 daje skoki **żądania**1,216–2,287°; ogranicznik wykonania nie znika.

Porównano z identycznych zaakceptowanych stanów wszystkich10 obserwacji:

- A: oryginalny #117;
- B±: istniejące impulsy #126;
- C−: ten sam−0,9° dodany do **bieżącego żądania #117 przy każdej ocenie sił**,
  z tą samą reakcją, czasem impulsu, gazem, pending i fizycznymi limitami.

Nie powstał nowy model. Jedyny dodatek do istniejącego predyktora to jawny,
domyślnie fałszywy argument `diagnosticRequestBias`, używany wyłącznie przez
narzędzie offline. Domyślne prognozy #126 pozostają identyczne: wszystkie30
porównań pozycji/skrętu/Drive ma błąd0, a ponowne63 prognozy względem oryginalnych
natywnych kontynuacji zachowują poprzednie maksymalne błędy. Porównano też całe
prognozy fizyczne, w tym prędkość, orientację, yaw, koło, prześwity i liczniki.
Jedynie nieeksportowane do CSV metadane `DistanceMeters` i `Context`, inicjalizowane
przez stary czytnik i nieużywane przez ten model/politykę, są jawnie wyłączone
z porównania całych obiektów; czas próbek porównano z tolerancją1e−12 s dla
różnicy reprezentacji zegara CSV/JSON. Nie przepisano tych wartości w dowodach. Hash rozszerzenia
i oryginalny hash są w `source-extension.json`; danych #126 nie przepisano.

C zwykle zmniejsza odchylenie od reakcji A, ale daje mieszane wyniki względem B.
Przykład1,20 s przyH=0,9: szczyt β A/B/C =2,481/2,315/2,363°, zmiana prędkości
−0,7323/−0,7218/−0,7242 m/s. C nie dominuje B pod względem prędkości, β i trackingu.
To nie dowodzi, że należy utrzymywać twardy override; dowodzi, że sama zmiana jego
wykonania nie została certyfikowana jako poprawa całej jazdy.

Każdy rollout jest ciągły do0,6 lub0,9 s, obejmuje aktywację i powrót #117.
Osobno sprawdzono ciągły natywny strumień przed/w trakcie/po wszystkich impulsach.
Nie sklejono niezależnych prognoz C w pozorny pełny przejazd. Po pojedynczym
impulsie kontrfaktyczna prognoza wraca do #117; nie dostaje przyszłych zapisanych
decyzji eksperymentalnego sterownika. Natywna walidacja B jest ograniczona do
chwili następnej nieuwzględnionej aktywacji. Nie potwierdza wykonania C.

## C. Co wybiera koszt i co pomija

[Pełne rozbicie wszystkich10 decyzji](first-bend-zigzag/decision-costs.md)
podaje dla A/B+/B− i diagnostycznego C− obydwa horyzonty, osobno tracking,
kierunek χ, wzrost końcowego β, postęp i karę odwrócenia, drogę XY, postęp,
zmianę i minimum prędkości oraz szczyt/koniec β. Sumy odtwarzają zapisane
koszty #126 z maksymalnym błędem **2,49×10⁻¹⁴ m²**. Dane maszynowe są w
`decision-costs.csv` oraz `execution-and-cost.json`.

| Hipoteza | Wynik i granica wniosku |
| --- | --- |
| Krótkoterminowy tracking przeważa nad skutkami fizycznymi | Potwierdzona możliwość i konkretne kompromisy. Koszt nie zawiera bezpośrednio energii strat, długości XY ani utraty prędkości; postęp jest tylko jednym składnikiem. Np. decyzje5,60 i6,00 s poprawiają koszt przy większej przewidywanej utracie prędkości niż A. Nie dowodzi to samodzielnie optymalnych nowych wag. |
| Kara końcowego β pomija szczyt | Potwierdzona. Dla obserwacji5,20 s B−: początkowe6,06°, szczyt9,02°, końcowe0,73° przyH=0,6, zatem kara wzrostu wynosi0. Nie ma kary za późniejszy powrót. |
| H=0,6 jest zbyt krótki | Wrażliwość potwierdzona tylko dla1/10 decyzji. Po reakcji zostaje0,36 s. PrzyH=0,9, z0,66 s po reakcji, obserwacja1,60 s wybiera A zamiast B−. Pozostałe9 nadal wybiera B−. Nie wolno ogłaszać rozwiązania przez samo wydłużenie horyzontu. |

Oba horyzonty mieszczą się w zweryfikowanym zakresie modelu. Żaden z wybranych
przedziałów nie przechodzi przez aktywację nowego planu. NawetH=0,9 z obserwacji
1,60 s kończy się w2,50 s i nie widzi szczytu β z4,15 s. Nie przedłużono modelu
poza0,9 s i nie dostarczono mu przyszłego zarejestrowanego gazu ani decyzji.
Nie zmieniono wag ani nie wybrano horyzontu według czasu pełnej jazdy.

## D. Dlaczego audyt kończy się bez nowego regulatora

**Główna przyczyna dużego zygzaka pozostaje interakcją wymagającą rozdzielenia.**
Twarde zastępowanie #117 wyjaśnia część krótkich zaburzeń i przeciwkorekt.
Duży cykl istnieje już w baseline i zaczyna się przed późnymi impulsami.
Koszt akceptuje krótkoterminowe kompromisy i pomija śródhoryzontowy szczyt β;
dłuższy horyzont zmienia tylko jedną decyzję. C nie spełnia zamrożonej bramki
jednoznacznej poprawy bez kompromisów. Nie ma podstaw, aby zmienić wykonanie,
koszt lub horyzont i ogłosić usunięcie przyczyny.

Nowe światy Box2D: **0**. Nowy regulator runtime: **brak**. Nie zużyto limitu
jednej nowej kontrolowanej jazdy; nie obniżano bramki po zobaczeniu wyników.
Nie można jeszcze odpowiedzieć „tak” na pytanie o wyeliminowanie całej oscylacji
bez zmian fizyki. Nie znaleziono też dowodu, że zmiana fizyki jest konieczna.

Pojedynczy następny pomiar rozstrzygający konkretną niepewność: natywna ablacja
z wyłączeniem **tylko pierwszych dwóch impulsów**1,44/1,84 s, przy niezmienionej
późniejszej polityce, gazie, kosztach, reakcji i wspólnym Q2. Porównanie z zapisanym
#126 oddzieli ich wkład w amplitudę i przeskok4,15 s. Nie wymaga nowego regulatora.
Nie wykonano tej próby w tym audycie i nie obiecuje ona rozstrzygnięcia wszystkich
wag i całego planowania jednocześnie.

## Porównanie wymaganych metryk

| Metryka | Baseline #124 | Kandydat #126 | Nowy wariant #127 |
| --- | ---: | ---: | --- |
| Czas dwóch łuków |19,98 s|20,27 s|nie wykonano|
| Max błąd trajektorii |5,906 m|5,375 m|—|
| Max moduł β |12,25°|13,86°|—|
| Odwrócenia skrętu |52|62|—|
| Droga XY |331,834 m|334,078 m|—|
| Min prędkość pierwszego łuku |45,48 km/h|44,07 km/h|—|
| Wyjście z pierwszego łuku |54,92 km/h|56,26 km/h|—|
| Konserwatywny min kreda / banda |1,490 /1,735 m|1,490 /1,807 m|—|
| Dodatkowy CPU sterownika |0|1,668 s|brak nowego kosztu runtime|
| Zamknięte przestrzelenia linii,1,2–10,3 s |3|3|—|
| Max odchylenie od stycznej planu |32,005°|34,181°|—|
| Pierwszy powrót po dużym ujemnym błędzie |2,39 s|2,19 s|—|

Brak nowego wariantu nie oznacza skopiowanego wyniku0 s albo poprawy. Mniejszy
błąd #126 ani wcześniejsze pierwsze przecięcie linii nie równoważą wolniejszej
jazdy, większego β i większej liczby ruchów kierownicy.

### Praca i straty w fazach pierwszego łuku [kJ]

| Zapis / faza | Silnik | Przód boczne | Tył boczne | Tył wzdłużne | Łożysko | Opory |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Baseline / wejście |4,635|3,315|4,088|0,663|0,143|4,166|
| #126 / wejście |5,430|3,530|4,484|0,894|0,148|4,234|
| Baseline / środek |9,350|7,294|4,159|2,152|0,131|3,218|
| #126 / środek |8,549|7,414|4,324|2,040|0,128|3,179|
| Baseline / wyjście |15,252|1,615|2,190|2,587|0,136|3,254|
| #126 / wyjście |17,335|1,524|2,071|3,039|0,141|3,288|

Podział według tych samych trzech segmentów i natywnego postępu CoM. Start/koniec
każdej fazy zapisano w `analysis.json`; przejścia są przypisane według początku
natywnego interwału, bez rekonstrukcji fizyki. Energia wykorzystuje zapisane siły
i prędkości punktów kontaktu oraz rotację liczoną raz. Krótki ogon do minięcia
wyjścia całym obrysem nie wchodzi do segmentowych faz CoM. To nie nowe dane fizyki.

## Koszt i walidacja

80 rolloutów w ograniczonej macierzy A/B+/B−/C−×2 horyzonty×10 stanów,
plus63 ponowne krótkie kontynuacje domyślnego modelu oraz zapytania kontaktów
z ciągłego zapisu. Macierz z podsumowaniami i kontrolami zajęła1,365 s.
Mediana pojedynczego C:12,87 ms przy0,6 s i19,45 ms przy0,9 s; maksima20,38/29,56 ms.
Największe liczniki C3351/4950 ocen kontaktu. Różnice CPU względem B zawierają
rozgrzanie JIT i kolejność, więc nie są benchmarkiem ani dowodem przyspieszenia.
Domyślne #126 nie wykonuje dodatkowego zapytania i zachowuje swoje koszty.

Celowane testy Release:15/15 C# (w tym4 nowe), Python9/9, Node29/29. Testy
obejmują pełne opóźnienie, zwolnienie po100ms, bieżące żądanie #117, pending,
limity kierownicy/opon, brak mutacji, deterministyczność, identyczne domyślne
prognozy i wszystkie natywne kanały wykresów. Dane #126 i źródła kontaktów
pozostają chronione hashami. Standardowe Draft Windows CI jest uruchamiane
na dokładnym HEAD PR; link i wynik są w jego opisie. Bez szerokich macierzy lokalnych.

## Obejrzyj klatka po klatce

Z głównego katalogu tego checkoutu:

```powershell
py -m http.server 8002 --bind 127.0.0.1
```

[Kandydat w5,44 s](http://127.0.0.1:8002/tools/replay-viewer/?case=zigzag-candidate&t=5.44)
oraz [baseline w5,44 s](http://127.0.0.1:8002/tools/replay-viewer/?case=zigzag-baseline&t=5.44).
Pod istniejącym Canvas jest panel audytu z ośmioma wspólnymi wykresami.
Przycisk **Porównaj w tej samej sekundzie** przełącza jeden rzeczywisty motocykl
i zachowuje czas oraz okno. Kliknięcie wykresu/znacznika ustawia czas replaya;
suwak, Play/Pause i ←/→ przesuwają również wszystkie kursory. Rozwijana diagnostyka
pokazuje siły, przyczepność i energię bieżącej natywnej próbki.

Niebieski = #124, pomarańczowy = #126. Przerywana kierownica = żądanie,
ciągła = stan. Wykresψ/χ: ciągłaψ, przerywanaχ. Pasy czerwone = aktywny impuls,
cienkie przerywane pionowe = obserwacja. Motocykl, turkusowy ślad i zielony plan
pozostają z istniejącego renderera. Wygładzanie obrazu OFF; wykresy łączą
pomiary dla czytelności i nie generują nowych stanów fizycznych.

Nie ma pozycji C w selektorze, bo C nie przejechał natywnie. #125/#126 i wszystkie
historyczne pozycje pozostają dostępne. Nowy audyt wymaga tylko odczytu gzip/JSON
i nie uruchamia modelu ani Box2D w przeglądarce.
