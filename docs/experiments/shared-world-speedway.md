# Shared World Speedway Simulation — wynik prototypu

Baza: `main` **561c06b6e486b4cbb61db66a3a2ad1fab33a0227**, zweryfikowany przez GitHub przed rozpoczęciem prac. Pomiar: 2026-10-17.
**B — architektura jest obiecująca, ale model sterowania i skutków w gęstym kontakcie wymaga poprawki.**

Działa jeden świat Box2D.NET: czterech zawodników z rzeczywistych pól startowych przejeżdża całe okrążenie i cztery okrążenia; AI obserwuje wspólny aktualny stan i wykonuje opóźnioną reakcję. Działają kontakty i ich dalsza mechanika. Nie rekomendujemy jeszcze migracji produkcji: zachowana próba bardzo gęstego startu zmienia liczbę upadków przy zmianie kroku, a brak fizycznych krawędzi toru pozwala upadłym ciałom wyjechać daleko poza tor. Ta próba **nie jest zaliczona**.

## 1. Architektura i stan

`WorldRunner` jest jedynym właścicielem czasu i zatwierdzonego stanu. Uporządkowanie po trwałym RiderId poprzedza tworzenie ciał. Jeden `b2World_Step` przesuwa wszystkie ciała; tylko Box2D zapisuje ich pozycję, wektor prędkości, orientację i obrót. Sterownik przekazuje siłę i moment. Nie ma `SetTransform`, przypisywania prędkości po kroku, kursorów segmentu, prognoz wykonawczych ani odtwarzania historii. Projekcja na istniejący `TrackMetricEmbedding` tylko odczytuje pozycję.

Świat używa istniejącego zamkniętego `MatchedVenueProfiles.CreateMotoarenaStandingStartTrack()`: proste 62 m, promień referencyjny 31 m, początek prostej 35 m i końcówka 27 m, szerokości 12/16,6 m. Nie zmieniono tej geometrii. Niezamknięty `CreateStandingStartExample()` jest jawnie odrzucany, zamiast teleportować motocykle na granicy okrążenia. Fizyczny offset jest ciągły również przy zmianie szerokości segmentu.

Stan zawiera X/Y, pełną prędkość, orientację, spin, gaz, cel kierowania, utratę kontroli, aktywność, fizyczny dystans i odwinięty postęp. `ForwardSpeed` i `SideSpeed` są projekcjami na osie motocykla; kąt poślizgu wynika z różnicy orientacji i faktycznego wektora ruchu. Konteksty StartStraight/FirstBend/NormalRacing wynikają z topologii i rzeczywistego postępu. Nie są przełączane zegarem scenariusza. Meta zapisuje czas przecięcia w obrębie **wykonanego** kroku; klasyfikacja sortuje po czasie mety, postępie i RiderId. Zawodnik po upadku nie otrzymuje wyniku ukończenia. Ciała po mecie pozostają fizyczne do końca jazdy ostatniego zawodnika.

## 2. Inwentaryzacja i wykorzystanie dotychczasowego kodu

| Element main | Wykorzystanie / ograniczenie |
| --- | --- |
| LongitudinalDynamics | Bezpośrednio publiczne siły startu, Straight i corner drive, jednego przełożenia, opory, timing i capability korekcji. Żaden wzór siły napędu nie został skopiowany. |
| SegmentPhysics | Bezpośrednio `MaxSafeTurnSpeedForRadius`; segmentowe Apply/outcome nie steruje nowym światem. |
| ContinuousCornerEnvelope | Bezpośrednio smoothstep dostępności napędu. Cały dystansowy Traverse i jego metrowe całkowanie nie są drugim wykonawcą. |
| TrackMetricEmbedding | Bezpośrednio istniejąca geometria, kierunki i zamknięcie; mały analityczny inverse służy odczytowi aktualnej pozycji. |
| BikeGeometry / SpeedwayBikeDimensions | Dokładnie te same kapsuły ramy i kierownicy; #55 mierzy odstęp w wykonanych punktach, bez wyszukiwania przedziałów. |
| RiderProfile / #56A | Zachowane dane; Reaction tylko start spod taśmy, Technique sterowanie, Attack/Combativeness skłonność do ustąpienia, Strength i masa rezerwa kontaktowa. |
| Legacy Skills / BikeSetup / surface | Start, Speed, SlideControl, Gearing, TractionBias i EffectiveGrip pozostają wejściami istniejących funkcji. Nie zmieniono skal ani kalibracji. |
| AdaptiveDecisionModel / #54 | Pełnosegmentowe prognozy i wybór trajektorii nie są wywoływane w nowej pętli. Osobny pomiar main obejmuje ten planer. |
| ContestedSpaceInteractionCoordinator / #56B | Nie przeniesiono koordynatora ani joint forecasts. Przeniesiono minimalną semantykę KeepIntent, YieldOutward i BackOut/lift/hold. To **nie jest pełne #56B**. |
| PhysicalContactConsequenceResolver / #56C | Publiczne Reserve, Classify i ControlLoss są użyte bezpośrednio. Nie wywołujemy starego impulsu ani projekcji końcowej prędkości; mechanikę wykonuje Box2D. Proxy severity nie jest pełnym demand/RSS #56C1. |
| HeatSimulator / ExecutedPathTraversal | Pozostają produkcyjne i niezmienione. Całosegmentowy Resolve/Commit nie jest używany w prototypie. |

Przejrzano wyniki [#82](https://github.com/arturgladysz91/zuzel/pull/82), [#83](https://github.com/arturgladysz91/zuzel/pull/83), [#84](https://github.com/arturgladysz91/zuzel/pull/84), [#85](https://github.com/arturgladysz91/zuzel/pull/85) i [#86](https://github.com/arturgladysz91/zuzel/pull/86). Z #82/#83 wynika potrzeba gotowego solvera trwającego kontaktu, zamiast kolejnych dokładnych wyszukiwań #55. #84/#85 pokazały działające manewry, lecz brak przyczynowej obserwacji wewnątrz jazdy. #86 wskazał brak wspólnego zaakceptowanego stanu. Nowy runtime nie importuje klas, zachowanych trajektorii ani koordynatorów tych eksperymentów.

## 3. Co zmienia model 2D

Napęd i opór mają stare charakterystyki. Wypadkowa siła dzielona jest przez rzeczywistą sumę masy zawodnika i istniejących 77 kg motocykla; scenariusze mają 65+77=142 kg. Boczny sterownik żąda siły normalnej potrzebnej do krzywizny, korekcji offsetu i tłumienia dryfu. Ograniczenie wynika z istniejącej capability `safeSpeed²/radius`. Jeśli brakuje budżetu na krzywiznę i odzyskanie linii, gaz/korekcja wytraca prędkość przez istniejące ograniczenie scrub. Nie dodajemy przyczepności ani nie przesuwamy motocykla na cel.

Ważna różnica: odzyskiwalna prędkość wejścia starego envelope nie gwarantuje siły bocznej dla całego promienia od wejścia. Nowy sterownik przygotowuje prędkość do istniejącego **settled** limitu planowanego promienia, a potem zachowuje płynne udostępnianie napędu po apexie. Pierwsza próba bez tego ograniczenia opuszczała tor; została odrzucona. Włączono również rzeczywisty limit wspólnego budżetu krzywizny i korekcji, zamiast zwiększać siłę boczną.

Parametry prowizoryczne: boczny czas odpowiedzi 0,65 s, yaw 0,35 s, poślizg zadany na łuku 12°, tarcie kontaktowe 0,2, horyzont ruchu względnego 1,1 s i czas zaburzenia 0,5 s. To ograniczony sterownik siłowy, **nie skalibrowany model opony żużlowej**. Orientacja wpływa na obrys i rozkład składowych prędkości; nie modelujemy osobno sił przedniej i tylnej opony, przejścia do uślizgu ani podnoszenia motocykla. Niskopoziomowa stabilizacja nie oznacza nowej świadomej decyzji taktycznej.

Canonical Start/TrackReading/Defense/PairRiding nie otrzymały nowych kompletnych mechanizmów. Nie deklarujemy pełnego przeniesienia #56A ani kalibracji osiągów. Nawierzchnia jest zamrożonym istniejącym TrackStateSnapshot; nie ma pogody, zużycia ani RNG w nowej pętli. Nie naliczamy ich więc drugi raz. Ich późniejsza integracja wymaga jednego wspólnego zapisu po zaakceptowaniu kroku.

## 4. AI podczas jazdy

Schemat obserwacja ruchu względnego → zajęta przestrzeń → ograniczenie gazu/cel kierowania jest inspirowany [TORCS, rozdział 5](https://torcs.sourceforge.net/api/robot_tutorial_chapter_5.html). Nie przeniesiono samochodowego hamulca ani fizyki samochodu.

AI ma aktualizację co 100 ms; pomiędzy aktualizacjami zachowuje komendę. Każda tura czyta ten sam niemutowalny snapshot, bez cudzych celów i przyszłych trajektorii. Tani filtr odległości/ruchu względnego poprzedza krótki model stałej prędkości. Nie ma #54 ani prognozy całego okrążenia. Combativeness i słaby Attack mogą pozostawić ryzykowny zamiar; nie wymuszamy unikania każdego kontaktu.

Osobny deadline reakcji używa jawnie prowizorycznego starego proxy 0,20–0,28 s z legacy Start; **nie rozszerza tape-only canonical Reaction** na percepcję wyścigową. Nowa komenda działa od pierwszej wspólnej granicy kroku nie wcześniejszej niż deadline. Nikt nie wykonuje ruchu wstecz ani wcześniejszego odjęcia gazu. Ustępujący później może wznowić napęd z nowej obserwacji, zachowując zwolnioną linię.

B: R1 z pozycji s=5 m, offset=2 m i prędkością 20 m/s próbuje zająć offset=4 m, gdzie jedzie R2. Rozpoznanie/utworzenie decyzji **0,300000 s**, żądane wykonanie **0,540000010 s**, faktyczne **0,550000 s** przy obu krokach. BackOut ogranicza gaz i zatrzymuje zamiar dalszego przechodzenia na zajętą linię przez fizyczną siłę, bez przestawienia pozycji. Minimalna rzeczywista separacja to **0,5771/0,5803 m** (20/10 ms). W kontrolnym przejeździe bez reakcji następuje kontakt. Test porównuje dokładnie wszystkie wykonane stany do aktywacji włącznie.

## 5. Biblioteka, kontakt i CCD

Przywrócono i rzeczywiście uruchomiono [Box2D.NET **3.1.654**](https://www.nuget.org/packages/Box2D.NET/3.1.654), managed net8.0, bez zależności natywnych. Pakiet wskazuje commit **5efc96def866edbb4e5a9368d84de5bf8c2dcaca**, którego [licencja jest MIT](https://github.com/ikpil/Box2D.NET/blob/5efc96def866edbb4e5a9368d84de5bf8c2dcaca/LICENSE). Zależność i lock należą wyłącznie do projektu eksperymentalnego; testy otrzymują ją przez ProjectReference. CoreSim nie zależy od Box2D.

NuGet restore lokalnie użył oficjalnego nupkg pobranego przez Python/OpenSSL do tymczasowego feedu, ponieważ systemowy klient TLS .NET/Schannel zgłaszał brak credentials. Zwykły CI przywraca pakiet z NuGet; repo nie zawiera lokalnego feedu ani kopii źródeł solvera. SHA256 nupkg: `5778868236bd0513b1ffab13ac8950f9fca4bf338901d7a4c7f5c25a0f2fce4c`; net8 DLL: `041c059d97bd4889de77c94edafe16704c5210dbe177c8fa02dc5e206af97079`. NuGet contentHash jest osobno zapisany w packages.lock.json.

Jedno dynamiczne ciało ma dwie kapsuły z `SpeedwayBikeDimensions.Reference`: rama 2,10×0,30 m oraz kierownica 0,80 m, rurka 0,04 m, offset podłużny 0,65 m. Solver obsługuje broad phase, narrow phase, tarcie, normalne impulsy i utrzymywanie styku. Restytucja=0. Begin/end/hit oraz rzeczywiste punkty manifold są obserwowane po kroku. Odtwarzanie ciepłego kontaktu przez Box2D nie jest nowym incydentem gameplay.

Epizod pary nalicza utratę kontroli raz. Ponowne uzbrojenie wymaga ustania mechanicznego styku i >0,02 m rzeczywistej separacji. Severity używa rzeczywistej prędkości zamykania punktu kontaktu, udziału mas i starej rezerwy. To **prowizoryczne proxy**, bez drugiego impulsu; nie uwzględnia pełnego #56C1 demand yaw/RSS. Wyjątek solvera nie jest zamieniany na Crash. Przy Crash ciało pozostaje dynamiczne; nie teleportujemy go ani nie zerujemy mu prędkości.

[Dokumentacja Box2D](https://box2d.org/documentation/md_simulation.html) opisuje ograniczenia dynamicznego CCD, w tym brak bullet–bullet. Sprawdziliśmy też faktyczne zachowanie pakietu: w E oba motocykle mają 40 m/s wzdłuż prostej oraz przeciwne ±40 m/s w poprzek (56,57 m/s każdy, 80 m/s zamykania). Przy samych 20 ms, konfiguracje none/one/both bullet i 1/4/8 wewnętrznych substeps **wszystkie dziewięć** pomija zetknięcie: 0 rozwiązanych punktów, zamiana kolejności poprzecznej. Same końce kroku mają dodatni odstęp.

Rozwiązanie standardowe: zbliżająca się para uruchamia drobniejsze **wspólne kroki świata**, z ponownym broad/narrow phase, a w każdym działa gotowy solver z czterema wewnętrznymi substeps. Limit początkowego ruchu względnego powierzchni to połowa grubości najcieńszego elementu, **0,02 m**; uwzględnia rotację. To nie są osobne zegary zawodników. Limit 128 podziałów jawnie odrzuca nieobsługiwane wejście, zamiast przepuszczać kolizję.

E po tej zmianie wykrywa pierwszy kontakt w **3,210/3,171/3,333 ms** (krok 20/10/5 ms), bez tunelowania w tej próbie. W 0,1 s wykonuje 429/435/437 wspólnych kroków; po prawdziwym rozdzieleniu następuje drugi fizyczny kontakt. Obie prędkości, spin i energia pozostają skończone; zaobserwowany dodatni przyrost energii biernego układu wynosi 0 J. To wynik sprawdzonych przypadków, nie uniwersalny dowód CCD dla dowolnych prędkości i przyspieszeń.

Największe zaobserwowane przenikanie kapsuł w zaakceptowanych punktach wszystkich badanych przebiegów: **0,571 mm** (D/20 ms). Nie zmieniono tolerancji #55 ani jego geometrii. Diagnostyka punktowa nie jest certyfikatem swept dla całego przedziału. Kontrolowana tolerancja gotowego solvera zastępuje mikrometrowe rozstrzyganie każdego trwającego styku.

## 6. Scenariusze A–G

Liczby podane jako 20/10 ms. Ograniczone 5 ms: C, D, E i zachowany adverse dense-start. Wszystkie przebiegi są wykonane przez ten sam świat i adapter.

| Test | Rzeczywisty wynik | Kontakty / kontynuacja | Ocena |
| --- | --- | --- | --- |
| A: cztery pola, okrążenie i bieg | Pierwsze okrążenie całej czwórki do 17,059/17,027 s. Cztery okrążenia: R1 60,704/60,572; R2 61,623/61,467; R3 62,573/62,372; R4 63,440/63,380 s. Kolejność identyczna. | 0 kontaktów; minimalny prześwit od krawędzi 1,015/0,994 m. | Zaliczone |
| B: ustąpienie w trakcie jazdy | Obserwacja 0,30 s → reakcja 0,55 s; minimum 0,577/0,580 m. Kontrolny przejazd bez reakcji ma kontakt. | 0 kontaktów po wykonanej korekcie; dalsza jazda 3 s. | Zaliczone |
| C: lekkie zetknięcie | 1 epizod; ratio 0,0275/0,0266 = Brush. | Obaj kontynuują 2 s. Dodatkowy C-support: 69/139 rozwiązanych punktów, 1 skutek, przenikanie 0,025/0,018 mm. | Zaliczone |
| D: mocniejszy kontakt w łuku | Pierwszy kontakt 0,040 s, zamykanie 3,912 m/s; ratio 1,4991 = MajorSave przy wszystkich trzech krokach. | Zaburzenie, odzyskanie kontroli, 2 s jazdy. Po rzeczywistym rozdzieleniu drugi lekki kontakt; 2 epizody/2 skutki. | Zaliczone dla fixture |
| E: szybkie dynamiczne–dynamiczne | Bez zewnętrznych podkroków tunelowanie; z podkrokami kontakt przy wszystkich trzech krokach. | Fizycznie duża severity → Crash. Brak wzrostu energii biernego kontaktu i skończone stany. | Zaliczone z podkrokami |
| F: blisko bez zamykania | 2,5 s równoległej jazdy po rzeczywistej 62 m tylnej prostej, odstęp 0,100 m. | 0 alarmów, 0 decyzji korekcyjnych, 0 rozwiązywanych punktów. | Zaliczone |
| G: cztery motocykle na pierwszym łuku | Rzeczywiste pola i różne cele; 12 s jazdy, wszyscy poza pierwszym łukiem, 2 komendy; minimum 0,732/0,755 m. | W tej konfiguracji 0 kontaktów, wszyscy aktywni. | Zaliczone dla fixture |

**Nie ukrywamy mocniejszego G-dense-stress** (pierwsza konfiguracja z czterema zagęszczonymi pozycjami i zbiegającymi celami, nie centra aktualnych pól A–D). Przy 20 ms: 2 epizody, 0 upadków. Przy 10 ms: 4 epizody, upadają R3/R4. Przy 5 ms: 4 epizody, upadają R2/R4. Przy 10/5 ms środek upadłego ciała kontynuuje ruch poza torem; minimalny clearance obrysu -17,218/-104,685 m. Nie ma ścian/krawężników ani skalibrowanej fizyki ślizgu po upadku. To skończone, ale **niedopuszczalne jako docelowy bieg** wyniki. Wybór spokojniejszego G nie rozwiązuje tej słabości.

Najmniejszy dalszy zakres: (1) porównać przebieg sił/komend i cechy pierwszego manifold w dense-start, poprawić ciągłość ograniczonego sterowania/yaw i miarę severity dla skończonego uderzenia, zachowując Box2D; (2) dodać do tego samego świata statyczne krawędzie toru i skończony opór ślizgu upadłego motocykla. To standardowe ciała/kształty Box2D, nie nowy autorski solver. Sama zmiana kroku na 5 ms ani poluzowanie geometrii nie jest rozwiązaniem. Koszt tej niezaimplementowanej poprawki nie został zmierzony; nie podajemy fikcyjnej wyceny w ms.

## 7. Wydajność Release

Windows 10.0.19045 x64, .NET **8.0.30**, AMD Ryzen 7 5700X (16 logicznych CPU), `DOTNET_TieredCompilation=0`. Jeden proces, jedno zadanie fizyki. Dwie rozgrzewki, mediana 7 próbek po 2 uruchomienia; CPU to średnia Process.TotalProcessorTime, o ograniczonej rozdzielczości Windows. Mierzono utworzenie świata, jazdę, diagnostykę, binarny hash wszystkich zaakceptowanych stanów i Dispose. Serializacja JSON i przygotowanie danych pomiarowych są poza timerem. Nie wykonywano historycznej macierzy.

| Przebieg | Elapsed 20/10 ms | CPU średnio 20/10 ms | Alokacje 20/10 ms | Koszt ms / s symulacji 20/10 ms |
| --- | ---: | ---: | ---: | ---: |
| Czterech bez kontaktów, 1 okrążenie | 11,988 / 23,364 ms | 12,277 / 24,554 ms | 2,58 / 4,50 MB | 0,703 / 1,372 |
| Czterech, G z reakcją na rywala, 12 s | 9,645 / 17,607 ms | 10,045 / 17,857 ms | 2,24 / 3,54 MB | 0,804 / 1,467 |
| Trwający kontakt C-support, 2 s | 1,539 / 2,749 ms | 1,116 / 2,232 ms | 0,825 / 1,056 MB | 0,769 / 1,375 |
| Pełny bezkontaktowy bieg | **44,180 / 87,742 ms** | **44,643 / 88,170 ms** | **8,09 / 15,23 MB** | **0,696 / 1,384** |
| G-dense-stress, 12 s — wynik niezaliczony | 12,203 / 23,090 ms | 12,277 / 23,438 ms | 2,84 / 4,75 MB | 1,017 / 1,924 |

Pełny bieg: **3173/6338** głównych kroków świata, **12692/25352** wewnętrznych substeps solvera, **2536/2532** wejść aktualizacji zawodnika w turach AI (w tym early-out dla traffic disabled lub ukończonych). 0 par taktycznych (traffic disabled), 19038/38028 tanich par proximity, 0 kontaktów i 0 dokładnych diagnostyk #55. Różnica finish time poszczególnych zawodników 20–10 ms: maksymalnie **0,201 s**; kolejność i brak kontaktów są zgodne. Pierwsze okrążenie: maksymalnie 0,042 s. Maksymalna prędkość biegu **25,824/25,817 m/s** (około 93 km/h), bez sztucznego Vmax. To sensowny rząd wielkości w istniejącej kalibracji, nie walidacja telemetryczna nowego modelu.

C-support: 100/200 bazowych kroków, 108/200 wywołań świata, 87/175 manifold i 69/139 punktów z impulsem, lecz jeden epizod i jeden skutek. G-dense-stress: 927/1802 diagnostyk kapsuł, bez milionów wyszukiwań geometrii. Nie powtarzamy zaakceptowanych prefiksów. Łączny koszt samych world-step w ostatniej próbie pełnego biegu: 20,30/40,11 ms; profile broad/narrow/constraint są w CSV. Pola `last_call_*` opisują **ostatnie pojedyncze wykonanie**, nie próbkę medianową; nie należy dzielić ich przez medianę całego przebiegu ani sumować jako niezależnych kosztów. Suma wewnętrznych profilerów nie obejmuje całego sterownika i diagnostyki.

Ekstrapolacja zmierzonego biegu: 100 takich bezkontaktowych biegów to około 4,4/8,8 s elapsed oraz 0,81/1,52 GB **łącznych alokacji**, nie pamięci rezydentnej. Nie wykonano szerokiego benchmarku setek biegów; wpływ GC i długiego obciążenia wymaga osobnego pomiaru przed produkcją.

Main, ten sam sprzęt i profile: stała linia bez #54 **3,807 ms/bieg**, AdaptiveDecisionModel **646,823 ms/bieg**, CPU drugiego 2192,708 ms ze względu na równoległy planer. CPU stałej linii był poniżej rozdzielczości pomiaru, nie wynosi wiarygodnie zero. Alokacje starego planera dotyczą tylko mierzonego wątku głównego i pomijają workerów. To różne zakresy: main wykonuje segmenty, zużycie, inną fizykę/klasyfikację i ewentualnie pełne #54; prototyp wykonuje tysiące wspólnych kroków, tanią AI i Box2D, bez pogody/zużycia. **Nie podajemy procentowego przyspieszenia.** Main czasy ukończenia: fixed 55,544–61,980 s, adaptive 55,955–57,064 s. Nowy model jest ostrożniejszy w zakręcie; nie retunowano go do tych czasów.

## 8. Determinizm, zakres i migracja

Wszystkie 11 scenariuszy przy 20 ms ma identyczny binarny hash zaakceptowanych stanów po powtórzeniu i odwróceniu wejściowej kolekcji. Dotyczy to również niezaliczonego dense-start: powtarzalność nie oznacza realizmu. To dowód dla naszej konfiguracji Windows/net8/single-worker i przypiętej biblioteki, nie obietnica między platformami lub wersjami solvera. Testy G sprawdzają też decyzje i epizody. Metoda tworzenia obiektów po RiderId jest zgodna z [zaleceniami deterministycznego porządku Box2D](https://box2d.org/documentation/md_simulation.html).

Produkcja: **0 zmienionych linii CoreSim**, bez zmian HeatSimulator, domyślnych opcji, historii, danych kalibracyjnych i workflow CI. Nie włączono trybu D ani nowego silnika do gry. Prototyp ma 655 linii runtime (WorldRunner/TrackFrame/Controller/ContactAdapter/model), 61 scenariuszy, 138 audit/CLI oraz 20 nowych przypadków testowych. Nie zawiera kodu źródłowego Box2D.

Odpowiedź na koszt późniejszej integracji: nie jest to przełączenie flagi. Potrzebne są cztery konkretne granice: orkiestracja HeatSimulator i zapis wspólnego kroku, adapter decyzji managera, pojedyncze naliczanie wear/weather/events, wyniki/persistence/obserwacje. Segmentowe Resolve/ExecutedPathTraversal przestaną być właścicielami pozycji w tej ścieżce. Zachowane funkcje sił, dane rider/setup/surface i geometria mogą zostać. **Nie przygotowano patcha migracji produkcyjnej, więc liczba jego linii i koszt nie są zmierzone.**

Brakuje: skalibrowanych sił opon i sterowania poślizgiem, odpornej severity/demand wieloparowego uderzenia, krawędzi toru i ruchu po upadku, dynamicznego zużycia i pogody, percepcji nawierzchni, pełnych #54/#56B/#56C, obrony/wyprzedzania i powrotu na wybraną linię, pełnego wykorzystania cech, morale i reguł sędziowskich. Kontakt z wolniejszym motocyklem jest fizyczny, ale kompletna taktyka blokowania nie powstała.

**Rekomendacja B:** zachować ten fundament do dalszego eksperymentu; nie zastępować jeszcze produkcji. Przewaga wspólnego czasu i koszt gotowego kontaktu zostały pokazane rzeczywistymi przejazdami. Dense-start oraz brak bezpiecznego ruchu po upadku są konkretnym warunkiem blokującym rekomendację A.

## 9. Odtworzenie i walidacja

```powershell
dotnet restore SpeedwayManager.sln
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
dotnet test tests/CoreSim.Tests -c Release --no-build --filter FullyQualifiedName~SharedWorldSpeedwayTests
$env:DOTNET_TieredCompilation='0'
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll heat .02
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll B .02
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll ccd
dotnet tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll audit shared-world-results
```

CLI: A/B/B0/C/D/E/F/G/heat/support/stress, kroki .005/.01/.02, opcjonalne `--trace` wypisuje fizyczne stany co 0,5 s. Audit wykonuje ograniczone scenariusze, repeat/reversal, 10 prób CCD i pomiary. Wejścia są jawne w Scenarios.cs; nowy runtime nie losuje. Seed starego benchmarku=19, incident frequency=0. Dane nie są realnym odtworzeniem heat91 ani kopią przyszłego ruchu z #81–#86.

Jedna końcowa kompilacja rozwiązania Release: 0 ostrzeżeń i błędów. Celowane Release: **31/31** (20 nowych + 11 istniejących kontroli użytych prymitywów). Guard: wszystkie 39 zamrożonych produkcyjnych konsumentów zachowane. Standardowe Windows CI jest uruchamiane przez nowy Draft; nie zmieniono zakresu shardów ani nie uruchamiano historycznych audytów.

Surowe, krótkie dane do odtworzenia: [scenariusze](shared-world-scenarios.csv), [decyzje i czasy](shared-world-decisions.csv), [kontakty](shared-world-contacts.csv), [CCD](shared-world-ccd.csv), [wydajność](shared-world-performance.csv), [deterministyczność](shared-world-determinism.csv), [porównanie main](shared-world-legacy.csv). `completed` w scenariuszu czasowym oznacza wykonanie zadanej długości próby; nie jest certyfikatem poprawności ani ukończenia biegu. Puste min_gap oznacza pary odrzucone bezpiecznie przez koła broad phase, a nie zerową separację.
