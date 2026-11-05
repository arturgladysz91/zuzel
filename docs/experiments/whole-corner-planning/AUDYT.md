# Audyt #171 · od obserwacji do planu łuku

## A1 · Geometria

`TrackFrame` jest przekazany zarówno #166, jak i #170. Pozwala przez `Track.CornerTopology`, `SegmentStartMeters`, `RoadAt`, `At`, `PhysicalWidthMeters` i `Shape.At` odczytać oba pełne łuki, dowolną krzywiznę, prostą i lokalną szerokość. `RacingLineGeometry` przelicza rzeczywiste granice, obrys kapsuł i prześwit. Oś odniesienia leży 1 m od wewnętrznej bandy. Progressive-b ma C2 krzywą z 4096 przedziałami na łuk, oba współczynniki kompresji 0,2, rampy 5% i szerokość 12–16,6 m. Podział na trzy równe odcinki referencyjne nie tworzy przeskoków krzywizny. Dane umożliwiają wybór linii globalnej. Bieżący sterownik z nich nie wyznacza konkurencyjnych linii całego łuku.

## A2–A3 · Teacher i wybór linii

Każdy z dwóch Teacherów powstał z legalnego prefiksu demonstracji #165 C-B101 albo C-B245. Kod zapisuje po 30 węzłów od 0 do końca pierwszego łuku. W oknie ±2,4 m średnia używa wagi gaussowskiej σ 1,15 m mnożonej przez prędkość. Dane obejmują offset, kurs, β, yaw, prędkość, skręt i gaz. `Teacher.At` interpoluje liniowo, a poza zakresem utrzymuje skrajny punkt. `Policy` miesza dwa profile ze stałą wagą, dopasowuje start do pola, zachowuje część zewnętrznego offsetu i stosuje margines. Na prostej miesza końcowy offset z przesuniętym początkiem pierwszego profilu. Drugi łuk jest przestrzennym przesunięciem pierwszego z inną skalą prędkości. #170 mapuje końcowy odcinek drugiego do pierwszego także przy liczeniu celu stabilizatora. W symetrycznej progressive-b osie są przystające, lecz bieżący stan w drugim łuku jest inny. Profil #166 nie optymalizuje tych różnic ani kosztu późniejszej części łuku. Obie polityki mogą wykonać korektę odbiegającą od linii, lecz w doborze skrętu nie ma alternatywy świadomie poświęcającej odcinek wejścia dla środka lub wyjścia.

## A4 · Horyzont

`Policy.Choose` propaguje pełny pending na 0,24 s i śledzi punkt profilu w chwili aktywacji. To prognoza opóźnienia. `ExitPolicy` ocenia 17 skrętów na 0,44 s, a wybrany skręt dopuszcza po sprawdzeniu ogona do 0,84 s z osobną, przyczynową komendą na kolejnym review. Skręt oceniany jest względem stanu końcowego i prześwitu po drodze. Gaz podlega również przestrzennemu budżetowi prędkości do kolejnego wejścia. Przy 70 km/h krótkie i dłuższe sprawdzenie odpowiadają w przybliżeniu 8,6 i 16,3 m, w tym 4,7 m samego opóźnienia. `PredictorA153` ma limit 0,9 s. Z wałem przyjmuje pojedynczą nową komendę, zachowuje kolejkę i kontakt, rozcina krok przy aktywacji i zdarzeniu geometrii. Integruje stan bez tworzenia świata Box2D. Dostępna cała geometria, przestrzenny profil i warunek wytracenia prędkości są czym innym niż optymalizacja 97,4 m.

## A5 · Okrążenia

`s >= Second.Entry` w #170 pozostaje prawdziwe po każdym przejeździe przez metę. Również `Policy.Target` nie zawija węzłów Teacher. Oryginalna ścieżka pozostaje dowodem dla zakresu #170. Planer #171 wyznacza indeks okrążenia przez floor, postęp lokalny przez odjęcie początku okrążenia i odczytuje rzeczywiste logiczne łuki z `CornerTopology`. Przedłużenie prostej po drugim łuku może przechodzić przez metę, a kolejny identyfikator łuku składa się z numeru okrążenia i indeksu w topologii. Każdy łuk używa osobnego profilu nominalnego z archiwów #170. Zestaw testowy ma sprawdzić wszystkie granice i następne wejścia przez trzy okrążenia, bez tworzenia natywnego świata S3.

## B · Gdzie ucieka czas

Sześć sekcji wycięto z 16 legalnych archiwów #170 i #166 na V1–V8. Granice czasu, prędkości i drogi interpolowano w rzeczywistych przekrojach. Krzywizna trajektorii to zmiana kąta wektora prędkości na rzeczywisty metr w oknie po cztery zaakceptowane kroki z każdej strony. Min promień to odwrotność szczytu bezwzględnej wartości. Dane zawierają rzeczywiste siły i wykorzystanie osi, komendy, energię, offset i prześwit obu stron. Energia boczna pochodzi z oszacowania pracy bocznych sił kontaktu w kroku. Wzdłużny poślizg koła pochodzi z księgi natywnej.

1. Największe wytracanie prędkości następuje na wejściu. Łuk 2 #170 traci około 14 km/h w pierwszej sekcji, przy średnim aktywnym gazie 3,6% i większym β niż na wejściu w pierwszy łuk. W trzeciej sekcji następuje osobny dołek do 52,28 i 56,10 km/h. Trzeba rozdzielić przygotowanie kierunku od nieefektywnego wytracania prędkości.
2. Na pierwszym łuku #170 i #166 mają identyczne dwie pierwsze sekcje. Wariant exit-only daje wyraźny zysk prędkości na wyjściu i bardzo małą różnicę czasu łuku. To uzasadnia zmianę planu wejścia i linii przez środek, ale samo nie dowodzi istnienia szybszej linii.
3. Praca boczna, yaw, dostępny Fx/Fy i opóźnienie współwystępują z ubytkiem prędkości. Chwilowe rezerwy opon nie oznaczają, że zwiększenie gazu w tym samym miejscu utrzyma kierunek i prześwit. Wpływ wcześniejszego lub późniejszego zejścia do wewnątrz wymaga porównania trajektorii, ponieważ audyt sam nie izoluje przyczyny.

[Przekroje i mediany](SEKCJE.md) · [metadane audytu](audit.json)
