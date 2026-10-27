# Dane empiryczne i zakres kalibracji #139

Sprawdzono 2026-10-27. Specyfikacja, regulamin i pomiar charakterystyki są różnymi rodzajami dowodu. Zbieżność pojedynczej liczby nie waliduje całego modelu. Nie zmieniono żadnego parametru domyślnego.

## Źródła pierwotne

|ID|Źródło i lokalizacja|Co rzeczywiście ustala|Ograniczenie|
|---|---|---|---|
|S1|[JRM 889 — instrukcja producenta](https://www.jawa500.cz/element/simple/documents-to-download/c/9/82967b1759773d81.pdf), dane techniczne, strony PDF 1 i 12|499 cm³, 48–50 kW przy 8500 rpm, metanol, masa samego silnika 28–30 kg|To kotwica specyfikacji; brak pełnego wykresu hamowni i przebiegu pod obciążeniem na torze.|
|S2|[JRM 889 — katalog podwozia](https://www.jawa500.cz/element/simple/documents-to-download/a/8/f24381e2715622bf.pdf), strona PDF 1|Rozstaw osi 1330 mm, rama Cr-Mo, wahliwy przód, sztywna oś tylna|Konkretny model; brak położenia CoM zestawu z zawodnikiem.|
|S3|[Mitas — katalog opon 2022](https://www.mitas-tires.com/-/media/mitas-tyres/catalogues-leaflets/mitas-moto-catalogue-en-us-es-pt-ru-cn-kr-th-ar-2022-4-screen.pdf?rev=-1), drukowane strony 86–87, tabela SW-07/SW-12|Tył 3.75-19, nominalna średnica 697 mm; przód 2.75-23, nominalna średnica 715 mm|Średnica nominalna to nie obciążony promień toczenia. Brak krzywych Fx/Fy na szutrze. Duży PDF: tabela była dostępna przez indeks wyszukiwania; pełne otwarcie narzędziem odrzucone przez limit rozmiaru. Pewność odczytu niższa niż S1/S2.|
|S4|[Svemo — Trackracing 2026](https://regler.svemo.se/regelbocker/trackracing), 8.2.1.2, 8.3, 8.10.1, 8.18.6|Minimalna masa gotowego 500 cc bez paliwa 77 kg; szerokość kierownicy 700–900 mm; mechaniczny zakres minimum 40° na stronę; klasy i dopuszczone opony|Regulamin szwedzki, nie pomiar egzemplarza ani potwierdzenie polskich przepisów. Nie używamy go do nadpisania reguł gry.|
|S5|[Uil, TU/e, DCT 2006.046](https://research.tue.nl/en/publications/non-lagging-effect-of-motorcycle-tyres-an-experimental-study-with/)|Pierwotny raport eksperymentalny dotyczący odpowiedzi przejściowej opon motocyklowych na stanowisku|Nie jest pomiarem żużlowej opony na Motoarenie. Nie przenosimy stałych relaksacji, sztywności ani tarcia.|
|S6|[British Speedway — oficjalne archiwum, Engine](https://speedwaygbarchive.co.uk/engine)|Informacja popularyzacyjna: silniki 500 cc, 80+ bhp i 11000+ rpm|Brak metody pomiaru i konkretnego setupu; niska pewność jako kalibracja. Nie traktujemy tego jako hamowni ani dowodu prędkości w łuku.|

Publikacja Honda *Application of the Magic Formula Tire Model to Motorcycle Maneuverability Analysis* (2000) została znaleziona w J-STAGE, lecz [plik źródłowy](https://www.jstage.jst.go.jp/article/hondatechnicalreview/12/1/12_2000_12_1_21/_supplement/_download/12_2000_12_1_21_1.pdf) nie otworzył się podczas sprawdzania. Nie przypisujemy mu odczytanych pomiarów. Podobnie odnaleziony dokument FIM 2026 nie był dostępny; nie cytujemy nieprzeczytanej treści. Nie znaleziono wiarygodnego, ilościowego zbioru zsynchronizowanych Fx/Fy, nacisków, β, wheelspin i RPM dla żużla na zbliżonej nawierzchni.

## Parametry modelu

|Parametr|Symulator w tym audycie|Dowód / zakres|Pewność i status|Wpływ; potrzebny pomiar|
|---|---|---|---|---|
|Pojemność / moc|Klasa 500 cc, kotwica 55 Nm przy 8500 rpm = 48,96 kW|S1: 499 cc, 48–50 kW przy 8500 rpm|Umiarkowana zgodność jednej kotwicy|Nie waliduje całej mapy. Hamownia z metanolem, tłumikiem i obciążeniem.|
|Krzywa momentu|2000/35, 4000/47, 6000/54, 7500/57, 8500/55, 10000/44, 11000/35, 12000/0 rpm/Nm|Brak całej zmierzonej krzywej|`UNCALIBRATED` poza kotwicą S1|Wpływa na wheelspin i przyspieszanie; hamownia, cutoff i odpowiedź gazu.|
|RPM|Sztywno omega tyłu × przełożenie; cutoff 12000 rpm|S6 opisowo 11000+ rpm; S1 kotwica 8500 rpm|`UNCALIBRATED` cutoff i rozkład roboczy|RPM z zapłonu zsynchronizowane z omega i prędkością. Regulacyjny limit nie jest typowym RPM.|
|Przełożenie i sprawność|8–10, tutaj 9; 0,94|Brak pomiaru tego motocykla|`UNCALIBRATED`|Liczba zębów obu stopni, straty, bezwładność wału i poślizg sprzęgła.|
|Masa motocykla|77 kg|S4 minimum gotowego motocykla bez paliwa 77 kg|Wysoka dla regulacyjnego minimum, nie rzeczywistego egzemplarza|Ważenie z paliwem i osprzętem.|
|Masa zawodnika|70 kg; razem 147 kg|Brak ważenia zawodnika w stroju|`UNCALIBRATED` dla tych przypadków|Masa wpływa na Fz, energię i przyspieszenie.|
|Rozstaw osi|1,30 m|S2: 1,330 m|Umiarkowana wiarygodność rzędu wielkości|Zmierzony rozstaw konkretnej ramy; różnica 30 mm nie uzasadnia automatycznej zmiany.|
|CoM i ramiona|Przód 0,70 m, tył 0,60 m|Brak pomiaru zespołu z zawodnikiem|`UNCALIBRATED`|Ważenie osi w pozycjach zawodnika; wpływ na moment i Fz.|
|Statyczne Fz|Przód 46,15%, tył 53,85%; 665,57 / 776,50 N|Wynika z modelowych ramion, nie z empirycznego pomiaru|`UNCALIBRATED`|Pomiar obu wag i transferu podczas gazu.|
|Wysokość CoM / transfer|W aktywnym prawie statycznym brak transferu; dawne pomocnicze 0,55 m nie jest pomiarem|Brak wiarygodnego h dla badanego zestawu|`UNCALIBRATED`|Dla ax>0: ΔFz_tył/(mg)=ax*h/(gL); nie wstawiamy intuicyjnego h.|
|Bezwładność yaw|52,72097 kg·m² z geometrii korpusu Box2D|Brak pomiaru|`UNCALIBRATED`|Pomiar bezwładności i pozycji zawodnika; wpływ na rDot przy tym samym momencie.|
|Promień tylnego koła|0,33 m efektywny|S3: nominalnie 0,3485 m zewnętrzny|`UNCALIBRATED` promień toczenia pod obciążeniem|Obwód na dystansie, ciśnienie, ugięcie; nie utożsamiać średnicy nominalnej z promieniem roboczym.|
|Przód: omega / I|Brak osobnego stanu obrotowego|S3 identyfikuje rozmiar, nie I|`UNCALIBRATED` pominięty wkład energii|Zmierz If i promień; efektywna masa If/Rf². Nie da się liczbowo ocenić błędu bez If.|
|Bezwładność tyłu|0,9 kg·m²|Brak pomiaru|`UNCALIBRATED`|Pomiar koła; obecnie wpływa też na statyczną charakterystykę przez regularyzację.|
|Tarcie / nawierzchnia|mu_ref 1,4 × EffectiveGrip 0,9175 = 1,2845 w kotwicy|Brak zmierzonych sił na tej nawierzchni|`UNCALIBRATED`|Siły vs slip, Fz, wilgoć, temperatura, warstwa luźna i zużycie.|
|Sztywności poślizgu|Wzdłużna 0,10, boczna 0,12; skala małej prędkości 0,5 m/s|Brak danych speedway Fx/Fy(κ,α)|`UNCALIBRATED`|Pomiary kombinowanego slip w zakresie do κ≈3 i dużych α.|
|Regularizacja kontaktu|0,03 s w algebraicznych skalach nachylenia|Brak identyfikacji fizycznej; to nie stan relaksacji Fy|`UNCALIBRATED`|Rozdzielić numerykę od charakterystyki materiałowej; potem pomiar przejścia.|
|Nasycenie przodu / combined slip tyłu|Monotoniczne funkcje z jednym budżetem|Pasywność i limit siły dowiedzione algebraicznie; brak danych żużlowych|`UNCALIBRATED` kształt i poziom|Możliwy peak/falloff, shear szutru, trail, camber: wymaga pomiaru.|
|Kierownica|Polecenia ±35°, 90°/s, kolejka 0,24 s|S4 wymaga mechanicznie minimum ±40°|Limit sterownika nie jest walidacją ogranicznika mechanicznego; szybkość `UNCALIBRATED`|Pomiar kąta, momentu i szybkości; nie zmieniamy zamrożonych limitów.|
|Prędkość prosta/łuk|Wyniki natywne w tabelach, bez celu kalibracyjnego|Brak odpowiedniego pomiaru prędkości chwilowej z geometrią, linią i setupem|`UNCALIBRATED` względem rzeczywistego toru|GNSS/IMU/RPM zsynchronizowane, rozdzielenie prędkości korpusu i obwodowej koła; czas okrążenia nie wystarcza.|

## Priorytet dalszego pomiaru

Najpierw niezależne charakterystyki obu opon na reprezentatywnym podłożu: Fx/Fy w funkcji α, κ i Fz, także stan przejściowy i duży wheelspin. Równolegle ważenie osi z zawodnikiem oraz pomiar obciążonego promienia i bezwładności koła. Pozwoli to oddzielić fizyczną charakterystykę od obecnego ogranicznika numerycznego. Dopiero wtedy dobierać transfer i ewentualny model relaksacji. Nie przenosić parametrów z opon drogowych i nie dopasowywać do 110 km/h.
