# Motocykl, silnik i setup

## Zasada nadrzędna — BINDING

Motocykl daje fizyczny potencjał, setup określa jak dobrze ten potencjał pasuje do aktualnego toru, a zawodnik musi go wykorzystać poprzez własne umiejętności i decyzje.

Nie stosujemy prostego wzoru typu `50% zawodnik + 20% motor + 30% setup`. Wartości te są wyłącznie wcześniejszym kierunkiem projektowym. Rzeczywiste siły zależności mają zostać ustalone eksperymentalnie po integracji systemów.

Docelowe zachowanie balansu:

- lepszy zawodnik pozostaje wyraźnie lepszy w dużej próbce biegów i spotkań,
- bardzo dobrze trafiony setup i optymalna ścieżka mogą pozwolić juniorowi albo przeciętnemu zawodnikowi pokonać lidera w pojedynczym biegu,
- taka przewaga sytuacyjna nie może automatycznie zmieniać przeciętnego zawodnika w dominatora całego meczu lub sezonu,
- im większa różnica klasy zawodników, tym więcej korzystnych czynników musi się złożyć, aby słabszy wygrał,
- dokładna siła umiejętności, sprzętu, setupu i ścieżki jest parametrem kalibracji, nie decyzją dokumentacyjną.

## Dwa motocykle na zawody — BINDING

Na zawody zawodnik ma do dyspozycji dokładnie dwa przygotowane motocykle: **Motor A** i **Motor B**.

Nie muszą mieć relacji „podstawowy / zapasowy”. Mogą być celowo przygotowane inaczej:

- z innym silnikiem albo jego charakterystyką,
- z innym bazowym setupem,
- pod różne scenariusze zmian toru.

Zmiana motocykla może być szybszą decyzją niż pełne przebudowanie setupu jednego motocykla.

Manager nie zarządza całym prywatnym magazynem silników zawodnika ani numerami seryjnymi jednostek. Zaplecze zawodnika wybiera, które dwie konfiguracje są dostępne na dane zawody.

## Silnik — BINDING

Nie tworzymy rozbudowanego zestawu widocznych ratingów typu moc, moment, Vmax, przyspieszenie i „overall silnika”.

### Charakterystyka

Każdy silnik ma trwałą albo wolnozmienną **charakterystykę oddawania mocy** na osi:

**łagodny ↔ neutralny ↔ agresywny**

Dokładna wartość może być ciągła wewnętrznie, a UI może prezentować opisowo.

Charakterystyka nie oznacza jakości. Agresywny silnik nie jest automatycznie lepszy od łagodnego.

Setup może przesunąć odczuwalny charakter jednostki, ale nie powinien całkowicie odwracać jej natury.

### Sprzętowa dyspozycja — BINDING

Pakiet silnikowy zawodnika posiada ukrytą, wolnozmienną **sprzętową dyspozycję**, która może przez tygodnie albo miesiące nieznacznie zwiększać lub zmniejszać jego dostępne fizyczne możliwości.

Nie jest to ukryta Forma zawodnika i nie zmienia jego umiejętności.

Kierunek balansu:

- początek sezonu: typowo około **-3%..+3%**, wyjątkowo około **-5%..+5%**,
- środek sezonu: zwykle około **-2%..+2%**, sporadycznie około **3–4%**,
- końcówka sezonu: zwykle około **-2%..+2%**,
- dobry albo słaby okres może utrzymywać się pół sezonu lub nawet cały sezon,
- po rozpoczęciu sezonu duże skoki są rzadkie,
- parametr nie jest losowany przed każdym meczem.

Dokładny wpływ procentowy na model sił, przyspieszenie i inne parametry pozostaje **PROVISIONAL** do kalibracji.

### Serwis, remonty i nowe jednostki — BINDING

Serwis silnika, remonty, wymiana jednostek i decyzje tunera są symulowanym tłem kariery zawodnika, a nie mikrozarządzaniem managera klubu.

Manager nie wybiera:

- terminu remontu,
- części silnika,
- interwału serwisowego,
- momentu zakupu nowej jednostki.

Nie stosujemy reguły `remont co X biegów = reset do pełnej jakości`, ponieważ premiowałaby ona samą częstotliwość serwisowania.

Serwis może być jednym z uzasadnień powolnej zmiany sprzętowej dyspozycji, ale nie gwarantuje poprawy. Częstszy remont sam w sobie nie daje premii sportowej.

## Setup managera — BINDING

Manager ma cztery główne regulatory setupu podczas zawodów.

### 1. Przełożenie

Oś:

**bardzo krótkie ↔ bardzo długie**

Krótsze przełożenie przesuwa zachowanie w stronę:

- mocniejszego przyspieszenia i reakcji,
- łatwiejszego wejścia w wysokie obroty,
- większej wrażliwości na wheelspin,
- mniejszej zdolności niesienia prędkości przy wysokiej prędkości.

Dłuższe przełożenie przesuwa zachowanie w stronę:

- spokojniejszej reakcji,
- słabszego przyspieszenia przy niskiej prędkości,
- łatwiejszego wykorzystania przyczepności w części warunków,
- większego potencjału niesienia prędkości.

Obecne `BikeSetup.Gearing` jest fundamentem, a nie finalnym modelem zębatek.

### 2. Zapłon

Oś:

**łagodny ↔ agresywny**

Bardziej agresywny zapłon:

- daje ostrzejszą reakcję,
- może lepiej wykorzystać wysoki grip,
- zwiększa wymagania dotyczące trakcji i kontroli.

Łagodniejszy zapłon:

- uspokaja oddawanie mocy,
- może pomagać przy mniejszej przyczepności,
- ogranicza część agresywnego potencjału silnika.

Zapłon może modyfikować odczuwalny charakter jednostki, lecz tylko w ograniczonym zakresie.

### 3. Dysza / mieszanka

Oś:

**bogatsza ↔ optimum warunków ↔ uboższa**

Nie jest to zwykły suwak „więcej mocy”.

Wpływ zależy od warunków i charakterystyki silnika. Zbyt dalekie odejście od optimum może pogarszać osiągi, temperaturę pracy albo niezawodność.

Dokładna fizyka i dozwolone zakresy są **PROVISIONAL**.

### 4. Długość motocykla / pozycja tylnego koła

Oś:

**krótszy ↔ dłuższy**

Ma wpływać przede wszystkim na trakcję, stabilność, zachowanie w uślizgu i łatwość prowadzenia, a nie na charakterystykę samego silnika.

Dokładny kierunek i siła efektu wymagają późniejszej walidacji fizycznej i są **PROVISIONAL**.

### Ciśnienie opon

Ciśnienie opon nie jest piątym głównym suwakiem managera w pierwszej wersji. Należy do drobnych czynności mechanika wykonywanych zgodnie z jego diagnozą i przyjętą polityką.

## Setup nie tworzy jakości silnika — BINDING

Dobry setup nie zamienia przeciętnego silnika w wybitną jednostkę. Pozwala lepiej wykorzystać to, co aktualny motocykl może zaoferować w konkretnych warunkach.

Jednocześnie zły setup może mocno ograniczyć świetny motocykl.

Efekt setupu jest więc celowo asymetryczny:

- dobre ustawienie zbliża wykorzystanie dostępnego potencjału do optimum,
- złe ustawienie może pozostawić znaczną część potencjału niewykorzystaną.

## Tor i ścieżka — BINDING

Setupu nie ocenia się w oderwaniu od lokalnej nawierzchni i trajektorii.

Ta sama konfiguracja może być dobra na jednej części toru i słaba na innej. Optymalna ścieżka może zmienić wynik biegu bardziej niż niewielka różnica jakości sprzętowej, ale przewaga działa lokalnie i sytuacyjnie.

Nie dodajemy sztucznego „bonusu zewnętrznej” ani „bonusu idealnej linii”. Korzyść musi wynikać z fizycznych warunków danej trajektorii.

## Zawodnik, feedback i decyzja — BINDING

Zawodnik przekazuje przede wszystkim objawy, np.:

- „mieli na wyjściu”,
- „dusi się”,
- „wynosi mnie”,
- „nie skręca”,
- „brakuje ciągu / prędkości”.

Zawodnik nie musi prawidłowo znać przyczyny.

Mechanik interpretuje objawy i proponuje korektę. Manager podejmuje ostateczną decyzję setupową.

Zawodnik normalnie wykonuje zatwierdzony setup. Wyraźny sprzeciw albo odmowa powinny być rzadkie i wynikać z niskiego zaufania, relacji, wysokiej Niezależności lub wyjątkowo mocnego przekonania zawodnika.

## Czas zmian — BINDING

Zmiana setupu zużywa rzeczywisty czas i zasoby mechanika.

Kierunek:

- przełożenie — zmiana szybka,
- zapłon — szybka,
- dysza / mieszanka — szybka lub średnia,
- długość motocykla — średnia.

Dokładne czasy pozostają **PROVISIONAL**.

Nie można jednocześnie wykonać kilku fizycznie kolidujących prac na tym samym motocyklu.

Dwa motocykle pozwalają przygotowywać alternatywę równolegle.

## Rola tunera — BINDING

Tuner odpowiada za silnik na poziomie budowy, głębokiego serwisu, rozwoju i długoterminowej charakterystyki jednostek.

Tuner nie wykonuje decyzji setupowych managera bieg po biegu.

Wpływ tunera powinien przejawiać się przede wszystkim poprzez:

- jakość i stabilność pakietu silnikowego,
- charakterystykę dostarczanych jednostek,
- powtarzalność po serwisach i zmianach jednostek,
- długoterminową sprzętową dyspozycję.

Nie wprowadzamy prostego `Tuner 90 = +X% prędkości`.

Dokładny model jakości tunera, specjalizacji i rozrzutu wyników jego pracy pozostaje **PROVISIONAL**.

## Relacja z istniejącym kodem

Obecne `BikeSetup` i `SetupResolver` są fundamentem. Nie należy ich zastępować równoległym systemem.

Obecny kod może nadal zawierać tylko `Gearing` i `TractionBias` oraz starsze zależności do czasu osobnych PR-ów implementacyjnych.

Ta dokumentacja nie jest zgodą na zmianę kolejności wdrażania: Race Engine i Track Engine pozostają wcześniejszymi etapami.

## Do ustalenia / kalibracji

- dokładne krzywe wpływu czterech regulatorów setupu,
- fizycznie realistyczny zakres wpływu długości motocykla,
- sposób mapowania zapłonu i mieszanki na aktualny model silnika,
- rozkład i czas trwania sprzętowej dyspozycji,
- wpływ tunera na rozrzut i stabilność jednostek,
- dokładna siła różnicy klasy zawodników względem świetnego setupu i optymalnej ścieżki,
- czasy prac i ograniczenia równoległości,
- sposób prezentacji charakterystyki Motoru A/B w UI.
