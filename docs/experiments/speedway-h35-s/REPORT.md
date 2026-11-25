
# Badanie #216 · H35-S Passive Rider Support and Physical Cornering

Punkt odniesienia to głowa H34-R
`6ca0c177dec1c097c8fef0ffb9eb0cc93e1574a7`.
Model H34-R w wariancie G obciążał wspólny siłownik także podtrzymywaniem
ciężaru. Sama statyczna składowa dla 70 kg wynosi 686,7 N przy limicie
700 N. Przekroczenie zakresu ruchu w 120 próbach pozwoliło rozpoznać
wąskie gardło tej konfiguracji. H35-S sprawdza, jak zmienia się diagnoza
po dodaniu wewnętrznych reakcji mechanicznych.

## Bierne podparcie

Punkt masowy H34 reprezentuje środek masy zawodnika. W H35 odległości
od jego środka wyznaczają punkty nacisku miednicy, butów i dłoni.
Punkty ramy wyznaczają górne powierzchnie siedzenia, dwóch podnóżków
i dwóch chwytów kierownicy. W układzie każdego podparcia szczelina ma
postać g = z + d(x⁴+y⁴) − s(x²+y²), gdzie x i y są przeskalowane
przez połowę długości i szerokości. Na brzegu podparcie łagodnie
opada. Płytkie zagłębienie występuje tylko w siedzeniu. Kierownica
przenosi obie powierzchnie chwytu zgodnie z faktycznym kątem skrętu.

Ugięcie δ = max(0,−g) przechowuje energię kδ²/2. Siła normalna
w kierunku ∇g zależy od kδ i tłumienia działającego przy domykaniu.
Tłumik wchodzi liniowo w pierwszych 3 mm ugięcia. Siła styczna działa
przeciw poślizgowi i mieści się w stożku o promieniu μN. Przeciwne
siły styczne i normalne działają na ramę i zawodnika w tym samym
punkcie. Ich suma oraz moment całego układu są zerowe. Siła na
zawodniku wchodzi do równania pędu ruchu względnego. Praca przy
przemieszczaniu uchwytów wchodzi do budżetu serwa skrętu przez
pochodną pozycji punktu względem kąta kierownicy.

Geometria, tłumienie, tarcie oraz udział ciężaru przenoszony przez
poszczególne punkty są hipotezami modelu. Badania ruchu motocyklistów
potwierdzają zasadność oddzielnych pomiarów na siedzeniu,
podnóżkach i kierownicy. Użyte tutaj liczby nie pochodzą z pomiarów
tych badaczy ani z kalibracji motocykla żużlowego. [Doria, Tognazzo
i Cossalter](https://doi.org/10.1177/0954407012457508) ·
[Badanie nacisku na siedzenie i podnóżki](https://arpi.unipi.it/bitstream/11568/1212612/3/motorcycle%20riders.pdf)

Limit mięśniowy pozostaje 700 N i 350 W. Statyczny stan początkowy
oblicza ugięcie podpór od rzeczywistego ciężaru. Test z wyłączonym
siłownikiem sprawdza, czy ciężar przenoszą reakcje bierne. Równania
kroku zawierają zwykłą grawitację oraz siły kontaktów i silnika.

## Lewa noga

Środek masy zawodnika rozdzielono na główną masę i 2,6 kg masy
efektywnej dystalnego odcinka nogi. Nadal mają razem 70 kg.
Współrzędna q opisuje najpierw ruch buta w bok poza lewy
podnóżek, a następnie wysunięcie ku nawierzchni. Dwa nominalne
odcinki 0,41 i 0,42 m oraz przedział kąta kolana 22–164°
ograniczają geometrię. Sprężyste, tłumione ograniczniki reagują
na końcu zakresu ruchu. Rzeczywiste q może się różnić od polecenia
z powodu kontaktu lub nasycenia mięśni. Model redukuje biodro
i kolano do jednego skoordynowanego ruchu i nie odtwarza pełnej
biomechaniki kończyny.

Energia względna nogi ma postać η‖ω × l(q) + l′(q) q̇‖²/2,
gdzie l łączy środki dwóch mas zawodnika, a η jest ich masą
zredukowaną. Współczynnik J = η l′·l′ daje bezwładność
współrzędnej q. Wektor K = η l × l′ sprzęga z nią obrót całego
układu. Pęd p_q = K·ω + J q̇ podlega całkowaniu, a wyeliminowanie
q̇ daje korektę −KKᵀ/J do efektywnej macierzy bezwładności.
Praca sił i część bezwładnościowa ∂T/∂q wyznaczają zmianę p_q.
Napęd tułowia i nogi dzielą 700 N i 350 W. Suma wartości
bezwzględnych obu mocy mieści się w limicie także przy
jednoczesnym wykonywaniu i pochłanianiu pracy.

Szczelina i prędkość buta wynikają z geometrii i Jacobianu
`FootContact215`. Dodatnia szczelina daje zerową reakcję toru.
Przy ugięciu but ma sprężystość 25 kN/m i tłumienie 420 N·s/m,
a siła tarcia mieści się w stożku z μ = 0,32. To założenia
badawcze. Wrażliwość sprawdzono również przy μ = 0,20 i 0,48.
Siła toru przechodzi przez transpozycję Jacobianu do pędu
całego układu, momentu pędu, ruchu grupy zawodnika oraz p_q.
W tym samym kroku midpoint zmieniają się reakcje kół. Lokalizacja
chwili kontaktu korzysta z ograniczenia krzywizny szczeliny
dla gęstej trajektorii numerycznej. Dokładność bilansu pracy
kontroluje podział kroku przed zatwierdzeniem impulsu.

## Kwalifikacja próby łuku

Każda główna próba rusza cztery metry przed granicą łuku piątego.
Punkty badawcze 80, 90, 100, 110 i 120 km/h ustawiają początkowy
pęd, a osobny pomiar podaje prędkość przy rzeczywistym
przekroczeniu granicy wejścia. Stan początkowy obejmuje ugięcie
obu opon, przygotowany przechył i pozycję zawodnika oraz momenty
pędu zgodne z przestrzenną prędkością kątową i obrotami.
Żaden późniejszy krok nie przypisuje motocyklowi prędkości,
przechyłu ani krzywizny.

Sterowanie wyznacza żądany moment przechyłu w oparciu o kurs,
krzywiznę i zmierzony stan motocykla. Z siły potrzebnej przy
przednim styku oblicza żądany kąt kierownicy. Aktualny kąt
nadal przechodzi przez ograniczenia serwa i opóźnienie 50 ms.
Te same cykle decyzji sterują Drive, położeniem zawodnika
i wysunięciem lewej nogi w odpowiednich wariantach.

Łuk ma 97,389 m wzdłuż odniesienia. Ukończenie MODEL wymaga
przekroczenia wejścia i wyjścia, co najmniej 0,12 s ciągłego
poślizgu przy obciążonej tylnej oponie oraz przejechania jeszcze
8 m przy wymogu równowagi na wyjściu. Przybliżenie prześwitu
odejmuje 1,35 m zapasu od istniejącego obrysu i stosuje dolną
granicę na ruch środka w kroku. Jest to celowo zachowawczy
pomiar MODEL. Potrzeba dokładniejszego przestrzennego obrysu
pozostaje zmienną przy klasyfikowaniu tych odmów.

Ranking najpierw uwzględnia kompletność łuku. Wśród ukończeń
porównuje czas, a wśród nieukończeń maksymalny rzeczywisty postęp.
Czas pełnego Drive jest mierzony oddzielnie w łuku i poza nim.
Drive skaluje żądanie momentu silnika. Krzywizna pochodzi
z prędkości i jej przyrostu jako (v_x a_y − v_y a_x)/‖v‖³.
Rejestrujemy również yaw rate, wheelspin, ugięcia i reakcje opon,
siłę mięśniową, stan podpór i bilans pracy.

## Wyniki mechaniczne — PASS

Przy wyłączonej sile mięśniowej siedzenie przejęło 404,22 N,
a podnóżki po 141,24 N. Suma wyniosła 686,7 N. Chwyty
kierownicy były odciążone w tym stanie. W odchylonym lądowaniu
każdy chwyt przeniósł do 421 N, a maksymalne chwilowe reakcje
siedzenia i podnóżków wyniosły odpowiednio 2 160 N i 915 N.
Są to szczyty dynamiczne w przyjętym modelu kontaktu.

| Próba | Pomiar |
|---|---|
| Lot z ruchem zawodnika | Zachowany pęd i moment pędu. Maksymalna reszta energii 0,00011 J. |
| Przeciwskręt przy 36 km/h | Lustrzane przechyły ±0,107 rad. |
| Odzyskanie równowagi | −0,086 → −0,021 rad w 1,10 s. Maksymalna siła mięśniowa 8,58 N. |
| Okrąg ustalony | 12 m/s, yaw 0,23 rad/s i przechył −0,284 rad przez 1,25 s. Maksymalna siła mięśniowa 35,6 N. |
| Pochylony start przy pełnym Drive | Przednie koło bez obciążenia przez 0,5518 s w próbie trwającej 0,82 s. Szczyt siły mięśniowej 450,4 N i mocy 77,2 W. |
| Pochylone lądowanie | Kontakt przedniego koła po 0,1897 s. Przy tych samych ograniczeniach mięśniowych próba trwała pełne 0,40 s. G z H34-R osiągał granicę ruchu około 0,268 s w porównywalnym zrzucie. |

Regulator odzyskiwania równowagi H34 tracił tłumienie po dodaniu
podparcia. Pierwszy regulator sprzężenia zwrotnego również nie
odzyskał równowagi. W przyjętej próbie sterownik porównuje
prognozy utrzymania 13 żądanych kątów kierownicy po tym samym
opóźnieniu 50 ms. Zmniejsza przechył przez rzeczywiste siły
opon w wydłużonym horyzoncie 1,10 s. Początkowe niezaliczenie
testu było podstawą do wstrzymania kolejnych etapów.

W pierwszym kroku 5 ms w zrzucie maksymalna skumulowana reszta
energii dochodziła do 0,545 J. Adaptacyjne ponowienie kroku
przed zapisem reakcji obniżyło ją do 0,00324 J przy tym samym
maksymalnym kroku. Dla siatki 5/2,5/1,25 ms moment lądowania
przedniego koła pozostał na 0,1897 s do czterech miejsc po
przecinku. Reszta energii przy starcie spadła odpowiednio
do 0,00131/0,00033/0,00008 J. Niezależne zmiany sprężystości
0,65–1,50×, tłumienia 0,50–1,50× i tarcia 0,65–1,35× dały
w zrzucie od 0,1889 do 0,1905 s do przedniego styku.
Na końcu zrzutu motocykl miał od 0,632 do 0,729 rad
bezwzględnego przechyłu. To wrażliwość użytego modelu, przy
nieznanych parametrach konkretnego zawodnika.

We wspólnym oknie 0,36 s wysunięta noga dotknęła ziemi po
0,1601 s. Podparcie trwało 0,1999 s, sięgnęło 1,23 kN
i przekazało 65,87 N·s w pionie oraz −21,05 N·s wzdłuż
początkowego kierunku jazdy. Skrócenie kroku z 2,5 do
1,25 ms zmieniło pionowy impuls o 0,0031 N·s. Zmiana
tarcia buta z 0,20 do 0,48 zwiększyła bezwzględny impuls
wzdłużny z 13,35 do 30,97 N·s. Próby bez nogi i z nogą
spoczywającą nie wytworzyły reakcji buta. W dłuższym wariancie
motocykl osiągnął granicę prześwitu ramy po 0,390 s bez
kontaktu i po 0,438 s z kontaktem. Obie trajektorie
kończą się utratą wykonalności.
[Próby mechaniczne](../../../data/experiments/speedway216/mechanics.json) ·
[Próby lewej nogi](../../../data/experiments/speedway216/leg.json)

## Pełny łuk — 0 z 500

Przeprowadzono 100 prób dla każdej prędkości, w tym po 50
bez modelu nogi i z nim. Siatka sprawdziła linie wejścia,
cztery poziomy gazu, dwa przechyły przygotowawcze i dwie
nastawy sterowania. Ostatnie cztery próby na prędkość
sprawdziły wcześniejsze złożenie motocykla z rzeczywistym
pędem kątowym i poślizgiem na wejściu. Wszystkie 500 prób
przekroczyło wejście, żadna nie przekroczyła wyjścia.

| Punkt początkowy | Najdalszy postęp od wejścia bez nogi / z nogą | Rzeczywiste wejście bez nogi / z nogą | Minimum prędkości w łuku bez nogi / z nogą | Pełne Drive w łuku bez nogi / z nogą |
|---|---:|---:|---:|---:|
| 80 km/h | 23,32 / 23,36 m | 80,65 / 81,42 km/h | 80,68 / 81,42 km/h | 0,000 / 1,057 s |
| 90 km/h | 23,47 / 21,37 m | 90,36 / 90,91 km/h | 90,36 / 90,96 km/h | 0,000 / 0,882 s |
| 100 km/h | 23,05 / 21,51 m | 100,08 / 100,55 km/h | 100,01 / 100,59 km/h | 0,000 / 0,782 s |
| 110 km/h | 22,01 / 21,72 m | 109,84 / 110,21 km/h | 109,32 / 109,42 km/h | 0,000 / 0,755 s |
| 120 km/h | 22,07 / 20,40 m | 119,96 / 119,96 km/h | 116,95 / 119,57 km/h | 0,688 / 0,617 s |

W tabeli dla każdego wariantu podano przebieg o największym
postępie. Prędkość wejścia jest mierzona przy przekroczeniu
granicy, a minimum jest liczone z kroków przypisanych do
łuku według środka przedziału. Prędkość wyjściowa ma wartość
pustą w danych. W próbie 120 km/h bez nogi pełne Drive
występowało podczas wytracania prędkości przez 0,490 s.
W wariancie 80 km/h z nogą 1,057 s pełnego Drive obejmuje
około 1,05 s jazdy bez wytracania prędkości, po czym
zabrakło prześwitu. Czasy z prostych są zapisane oddzielnie.

Przyczyny zakończenia to 264 granice prześwitu ramy względem
ziemi, 206 odmów prześwitu od granicy toru, 15 wyjść poza
dziedzinę orientacji i 15 przekroczeń zakresu ruchu zawodnika.
W pierwszym przebiegu pojawiło się jedno wyczerpanie podziału
przy sprzężonej zmianie kontaktu. Algorytm ponawia wtedy
krótszy prefiks przed zdarzeniem. Po tej poprawce żadna
z 500 prób nie skończyła na błędzie numerycznym.

Odtworzenie wejścia i środka zachowało położenie z zapisu
#215, a odtworzenie wyjścia dotyczy faktycznego łuku z tego
zapisu. Ścisły punkt wejściowy #215 od razu naruszył
zachowawczy prześwit H35-S. Osobne sondy odtwarzają jego
prędkość i kąt na punkcie odsuniętym od granicy. Zapis
środka kończył się po około 0,29–0,31 s. Przy odtworzeniu
końcowej fazy, 15,81 m przed wyjściem, oba warianty
osiągnęły granicę prześwitu ramy po 0,22–0,27 s.
[Pełna siatka prób](../../../data/experiments/speedway216/corner-search.json) ·
[Odtworzone fazy](../../../data/experiments/speedway216/reconstructed-phases.json)

## Wynik kwalifikacji i regresja

| Poziom | Wynik |
|---|---|
| Mechanika | **PASS** w dziedzinie zredukowanego MODEL. Obie bramki podparcia i sprzężonej nogi zaliczone. |
| Łuk MODEL | **Brak PASS**. Najdalszy zatwierdzony stan to 23,47 z 97,39 m od wejścia. |
| Sportowy | **Brak PASS**. Brak pełnych łuków i pełnego przejazdu S. |
| Natywny | **Bez kwalifikacji**. Utworzono zero światów Box2D. |

Bierne podparcie usunęło dominującą przyczynę odmów H34,
ale w aktualnej konfiguracji pojawiły się inne granice.
Wynik zależy także od toru, inicjalizacji przestrzennej,
modelu opony i regulatora. Próba identyfikuje granice
testowanego układu, bez rozstrzygnięcia osiągalności
szybszego łuku inną polityką.

Niezależna walidacja objęła 53 ślady, 14 926 kroków,
119 zdarzeń kół i 16 zdarzeń buta. Maksymalne błędy
bilansu impulsu liniowego i kątowego wyniosły
1,58×10⁻¹² N·s i 9,22×10⁻¹³ N·m·s. Największa
skumulowana reszta energii w tych śladach to 0,0147 J.
Trzy niezależne procesy zapisały identyczne bajtowo 59
artefaktów H35-S. Stary eksperyment H34-R dał identyczne
98 artefaktów na kompilacji bazowej i bieżącej.

Release kompiluje się z ostrzeżeniami traktowanymi jako
błędy. Przeszło 27 testów ukierunkowanych, w tym sześć
nowych. Porównanie całego shardu core dało 2 087/2 308
poprawnych testów w bazie i 2 093/2 314 po zmianie.
Te same 221 przypadków kończy się niepowodzeniem,
w tym 219 testów z aktywną blokadą tworzenia światów i
dwie odziedziczone asercje zamrożonych źródeł. Program
dalej domyślnie wybiera H25.
[Walidacja impulsów](../../../data/experiments/speedway216/validation.json) ·
[Powtarzalność](../../../data/experiments/speedway216/determinism.json) ·
[Zgodność z H34-R](../../../data/experiments/speedway216/baseline.json) ·
[Regresja i wyniki TRX](../../../data/experiments/speedway216/regression.json)

## Publikacja

GitHub potwierdził commit implementacji
`3c1d2b38acd04009ec75edd78b4c2b5991f7b817` na
`experiment/speedway-216` 25 listopada 2026. Dla
commita nie odnotowano uruchomień workflow, kontroli ani
statusów zewnętrznych. Łączny status GitHuba to `pending`.
Wyniki testów pochodzą z lokalnej kompilacji Windows.
[Zapis kontroli CI](../../../data/experiments/speedway216/hosted-ci.json)

~~~powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS='1'
dotnet build tools/speedway-h26/H26.csproj -c Release -m:1
dotnet run --project tools/speedway-h26/H26.csproj -c Release --no-build -- support-216 . out216
python tools/speedway-h26/verify216.py out216 out216/validation.json
~~~
