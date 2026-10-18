# #88 — Stable Speedway Bike Dynamics

Baza zweryfikowana lokalnie i na GitHub: Draft #87, `6e6f584c7d07f5b976b1565a8f7ba1de791c44f6`. Nie zmieniamy wcześniejszego PR ani produkcyjnego CoreSim. Parametry napędu, startu, opon, yaw, progi skutków i cztery wewnętrzne substeps są punktem odniesienia.

## Diagnoza przed zmianą regulatora

Dodano możliwość uruchomienia jednego rzeczywistego ciała oraz jawne komendy zapisane przed startem. To wejścia eksperymentu do istniejącego WorldRunner, nie alternatywna fizyka. Zaplanowane komendy i reaktywna AI są wzajemnie wykluczone. Dla pełnego biegu czterech zawodników rozszerzony harness zachowuje dokładny binarny hash #87 przy wszystkich trzech krokach.

Próby: A — stały gaz na prostej przez 2,4 s; B — 1,5 s łuku od referencji 45 m, offset/target 3 m, osobno 18 m/s i dotychczasowa settled capability; C — pełne okrążenie jednego ciała, identyczne odjęcie gazu do 0,9 w 8,013 s i powrót w 8,347 s; D — oryginalny pełny bieg czterech, stałe komendy oraz rzeczywista AI #87; dodatkowo niezmieniony G-dense-stress. Porównujemy zaakceptowane stany co 20 ms, bez interpolacji runtime.

Pierwsze przekroczenie diagnostycznego progu 1 cm / 0,05 m/s / 0,1° na prostej A następuje w **0,80 s** dla 20 względem 5 ms; dla 10 ms w 1,84 s. Po 2,4 s różnice 20/10 względem 5 ms wynoszą 0,04331/0,01443 m, 0,01089/0,00360 m/s, przy praktycznie zerowym yaw i błędzie linii. W przypiętym Box2D prędkość jest aktualizowana przed pozycją w każdym z czterech substeps. Dla stałej siły daje to `x_next=x+v*h+(n+1)/(2*n)*a*h²`; przy n=4 współczynnik to 0,625 zamiast 0,5. Siła sterownika pozostaje stała przez całe wywołanie świata. To zwykły błąd dyskretyzacji, a nie niestabilny rozruch naprawiony w #87.

Największe narastanie błędu okrążenia C występuje już w pierwszym TurnEntry, około **3,70 s**. Przy t=3,00 s: offset 3,00758/3,00063 m, prędkość na zewnątrz 0,28494/0,02972 m/s, żądanie boczne/budżet 1,06562/1,01229 dla 20/5 ms. Wzdłużna korekta wynosi wtedy -2,50823/-1,36094 m/s². Przy 20 ms krok rozpoczynający się jeszcze na prostej przenosi ciało w łuk bez siły dośrodkowej. Korekta przy skończonej przyczepności wzmacnia tę małą różnicę wejścia w trwałą utratę prędkości. W 3,70 s prędkości to 21,6454/22,1820 m/s. Nie jest to nowy błąd bazy sił ani budżetu z #87.

Na izolowanym łuku różnice pozostają małe i maleją około trzykrotnie przy przejściu 20→10 ms względem 5 ms. Przy 18 m/s maksymalny błąd to 0,05238/0,01745 m, 0,05438/0,01810 m/s, 0,10507/0,03501°. Głównym wzmacniaczem długiego przejazdu jest więc przejście krzywizny, nie rosnące oscylacje na ustalonym łuku.

Komendy C aktywują się dokładnie w 8,013 i 8,347 s przy wszystkich krokach. W D rzeczywista AI nie widzi zagrożeń i nie wydaje komend: trajektoria jest dokładnie równa stałemu harmonogramowi. Rozjazd D nie pochodzi od AI ani deadline. Dense zachowuje zgodne czasy/rodzaje reakcji, ale jego docelowe offsety różnią się nieznacznie wskutek wcześniejszego ruchu. Osobno raportujemy te wartości.
