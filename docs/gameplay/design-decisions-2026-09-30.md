# Ustalenia projektowe — snapshot 2026-09-30

Ten dokument zapisuje **nowe decyzje podjęte w rozmowie projektowej**, które nie zostały jeszcze w pełni przeniesione do kanonicznych plików `rider-model.md`, `bike-setup.md`, `staff-and-delegation.md`, `observations-and-feedback.md` i przyszłych specyfikacji kontraktów/treningu.

To jest zapis gameplayu i kierunku implementacji. **Nie jest zgodą na zmianę kodu produkcyjnego ani kolejności wdrażania.**

Jeśli poniższa decyzja jest sprzeczna ze starszym wpisem oznaczonym jako PROVISIONAL/TBD, ten dokument opisuje **nowszą decyzję projektową**. Dokładne liczby oznaczone niżej jako kalibracyjne nadal wymagają testów.

---

## 1. Polecenia ścieżki — BINDING

Manager może ustawić niezależnie plan dla obu łuków:

- **Łuk 1:** wewnętrzna / środek / szeroka / decyzja zawodnika,
- **Łuk 2:** wewnętrzna / środek / szeroka / decyzja zawodnika.

Polecenie jest **preferowanym planem**, nie sztywną blokadą trajektorii.

Zawodnik próbuje zrealizować wskazaną ścieżkę, ale może od niej odejść, jeśli:

- rywal blokuje docelową linię,
- sytuacja ataku lub obrony wymaga zmiany,
- zostanie wypchnięty,
- docelowa linia jest fizycznie niedostępna.

System ma integrować się z istniejącym modelem linii/pozycji bocznej Race Engine, a nie tworzyć równoległy system jazdy.

### Czytanie toru — uproszczona rola

Na poziomie profesjonalnym zakładamy bazową umiejętność znalezienia rozsądnej ścieżki. `Czytanie toru` nie oznacza „wie / nie wie, którędy jechać”.

Cecha różnicuje przede wszystkim:

- trafność oceny aktualnej jakości dostępnych linii,
- zachowanie przy niejednoznacznych lub zmieniających się warunkach,
- wybór alternatywy, gdy zaplanowana ścieżka jest niedostępna,
- jakość informacji o torze po biegu.

Preferowany prosty model implementacyjny: rzeczywista ocena linii + błąd/szum zależny od `Czytania toru`. Oczywista przewaga jednej linii powinna być rozpoznawalna również przez przeciętnego zawodnika; rating ma mieć znaczenie głównie przy subtelnych różnicach.

Dokładny rozkład błędu pozostaje do kalibracji.

---

## 2. Feedback po biegu — BINDING

Po biegu zawodnik przekazuje **maksymalnie dwa kluczowe komunikaty**:

1. **jedną informację o torze / ścieżce**,
2. **jedną informację o zachowaniu motocykla**.

Lepszy zawodnik nie powinien zasypywać gracza większą liczbą zdań. Różnica ma wynikać z **trafności i precyzji informacji**, nie z liczby komunikatów.

### Informacja o torze

Główne źródła jakości:

- `Czytanie toru`,
- ukryte doświadczenie wynikające głównie z wieku i kariery.

Przykład zakresu jakości:

- słabsza informacja: „chyba szerzej zaczyna lepiej trzymać”,
- lepsza: „na drugim łuku szeroka zaczyna działać; na pierwszym nadal lepszy jest środek”.

### Informacja o motocyklu

Zawodnik opisuje **objaw / odczucie**, np.:

- „na wyjściu koło kręci za mocno”,
- „wynosi mnie”,
- „na wejściu jest dobrze, problem zaczyna się przy otwarciu gazu”.

Nie powinien automatycznie podawać prawidłowej technicznej przyczyny ani konkretnej korekty setupu.

### Mechanik po feedbacku

Mechanik nie jest „drugim zawodnikiem” i nie zna automatycznie prawdy. Łączy:

- własną obserwację biegu,
- feedback zawodnika,
- znajomość bieżącego setupu,
- oględziny motocykla.

Ma dokładnie dwie kompetencje:

- **Diagnoza** — trafność rozpoznania przyczyny,
- **Setup** — jakość proponowanej korekty po rozpoznaniu problemu.

Preferowany format odpowiedzi mechanika:

- 1 główna diagnoza,
- opcjonalnie 1 alternatywny trop, gdy sytuacja jest niejednoznaczna,
- 1 rekomendowana zmiana setupu.

Lepszy mechanik ma być dokładniejszy, a nie po prostu bardziej gadatliwy.

---

## 3. Tuner — nowsza decyzja niż obecne PROVISIONAL — BINDING

Tuner ma tylko dwie istotne cechy:

1. **Jakość** — przesuwa rozkład jakości dostarczanego pakietu silnikowego w lepszą stronę,
2. **Powtarzalność** — zmniejsza rozrzut między jednostkami / serwisami, ale nie gwarantuje identycznych wyników.

Nie ma osobnej specjalizacji:

- agresywny,
- łagodny,
- „pasujący do konkretnego typu zawodnika”.

Takie profile tworzyłyby zbyt prostą metę typu „zawodnik X → tuner X”.

Tuner **nie daje bezpośredniego bonusu prędkości** i nie determinuje jednej stałej charakterystyki wszystkich swoich silników.

---

## 4. Motor A / Motor B — refinements — BINDING

Na zawody nadal są dokładnie dwa motocykle: **Motor A** i **Motor B**.

Dla konkretnej jednostki wystarczają dwa ukryte elementy:

1. **potencjał / jakość fizyczna**,
2. **charakterystyka oddawania mocy** na ciągłej osi spokojniejsza ↔ ostrzejsza/agresywniejsza.

Na obecnym etapie **nie dodajemy osobnego stanu/zużycia silnika**.

Serwisy, remonty i wymiany pozostają tłem symulacji. Nie ma reguły „remont co X biegów = reset jakości”.

### Informacja dla gracza

Gracz może początkowo widzieć prawie wyłącznie:

- Motor A,
- Motor B.

Nie pokazujemy obowiązkowo:

- jakości,
- mocy,
- agresywności,
- zużycia,
- kompatybilności,
- ukrytego bonusu.

Różnice mają być odkrywane przez zachowanie motocykla, feedback i wyniki zmian setupu.

### Brak statycznej preferencji silnika zawodnika

Nie dodajemy atrybutu typu „lubi agresywne silniki”.

Reakcja zawodnika na konkretną charakterystykę ma wynikać z istniejących cech i fizyki, m.in.:

- Technika,
- Siła,
- masa,
- Start w fazie startowej,
- setup,
- aktualny tor.

Ta sama jednostka może być łatwiejsza dla jednego zawodnika i trudniejsza dla innego bez osobnego ratingu kompatybilności.

---

## 5. Osobowość — BINDING

Widoczna osobowość zawodnika zostaje ograniczona do czterech opisowych cech. Nie pokazujemy ich jako kolejnego bloku sportowych ratingów 1–99.

### Profesjonalizm

Opisuje, jak rzetelnie zawodnik wykonuje pracę potrzebną do kariery i treningu.

Proponowana skala opisowa:

- Wzorowo profesjonalny
- Bardzo profesjonalny
- Profesjonalny
- Przeciętne podejście
- Leniwy
- Bardzo leniwy

### Ambicja

Wpływa m.in. na:

- chęć rozwoju,
- wymagania dotyczące roli w drużynie,
- reakcję na niewystarczającą liczbę startów,
- wymagania kontraktowe,
- chęć dalszej walki po nieudanym początku biegu.

Ambicja nie zastępuje `Ataku`; odpowiada za chęć działania, a `Atak` za jakość wykonania.

Skala opisowa:

- Wyjątkowo ambitny
- Bardzo ambitny
- Ambitny
- Umiarkowanie ambitny
- Mało ambitny
- Pozbawiony ambicji

### Zespołowość

Opisuje chęć działania dla dobra pary/drużyny.

Nie zastępuje `Jazdy parą`:

- Zespołowość = czy chce pomóc,
- Jazda parą = jak dobrze potrafi to wykonać.

Skala:

- Urodzony zespołowiec
- Bardzo zespołowy
- Zespołowy
- Neutralny
- Indywidualista
- Skrajny indywidualista

### Opanowanie

Ma znaczenie głównie w sytuacjach stresowych / awaryjnych:

- koleina,
- kontakt,
- utrata kontroli,
- konieczność ratowania motocykla,
- presja ważnego biegu,
- reakcja po błędzie lub upadku.

Podstawowa zdolność fizycznego ratowania motocykla nadal wynika głównie z Techniki, Siły i sytuacji fizycznej. Opanowanie wpływa na ryzyko złej wtórnej reakcji / paniki.

Skala:

- Wyjątkowo opanowany
- Bardzo opanowany
- Opanowany
- Nerwowy
- Bardzo nerwowy
- Porywczy

Nie dodajemy osobnych cech: Chęć nauki, Mentoring, Ugodowość. Nie potrzebujemy również osobnej „Waleczności”, jeżeli jej rolę pokrywają Ambicja + sportowy Atak/Obrona.

Cechy osobowości są widoczne od początku kariery, zasadniczo stabilne, ale mogą bardzo powoli/wyjątkowo zmieniać się w długim okresie.

---

## 6. Role kontraktowe i pozycje — BINDING

Kontrakt jasno określa **pozycję regulaminową / miejsce w kadrze** oraz **rolę sportową**.

Dozwolone kombinacje:

### Senior

- **Lider**
- **Podstawowy**
- **Rezerwowy**

Maksymalnie **3 Seniorów Liderów** w klubie.

### U24

- **Podstawowy**
- **Rezerwowy**

### Junior

- **Podstawowy**
- **Rezerwowy**

Junior i U24 mogą być sportowo bardzo mocni, ale kontraktowo nadal występują na swojej pozycji regulaminowej.

### Oczekiwania wynikające z roli

**Lider**

- chce jechać swoje programowe biegi,
- przy dobrym występie oczekuje również jazdy w biegach nominowanych.

**Podstawowy**

- chce jechać swoje biegi wynikające z programu,
- nie oczekuje automatycznie nominowanych.

**Rezerwowy**

- nie oczekuje regularnego kompletu startów,
- oczekuje, że od czasu do czasu dostanie realną szansę jazdy.

Rola jest oceniana w dłuższym okresie, a nie po jednym przypadku.

### Pominięcie Lidera w nominowanym

Reakcja ma zależeć od kontekstu:

- jak dobrze Lider jechał w danym meczu,
- czy wynik spotkania był jeszcze otwarty,
- kto pojechał zamiast niego.

Przykładowo:

- Lider jedzie dobrze + mecz na styku + jedzie inny senior → duże niezadowolenie,
- mecz już wygrany + jedzie junior → brak lub minimalne niezadowolenie,
- mecz rozstrzygnięty + jedzie inny senior → możliwe lekkie pretensje.

`Ambicja` skaluje siłę reakcji.

Nie potrzebujemy skomplikowanego AI — wystarczy prosta ocena uzasadnienia pominięcia, np. uzasadnione / neutralne / nieuzasadnione, plus kumulacja podobnych sytuacji w sezonie.

---

## 7. Negocjacje kontraktu — BINDING

Zawodnik rozpoczyna rozmowy od wskazania, **czego oczekuje**, a gracz negocjuje warunki.

Podstawowe elementy oferty:

- rola / pozycja,
- długość umowy,
- kwota za podpis,
- stawka za punkt,
- stawka za start.

Dokładne dopuszczalne długości i skale finansowe są późniejszą kalibracją.

Zawodnik nie ma jednej sztywnej ceny — posiada wewnętrzny zakres akceptacji.

Może odpowiadać konkretnym powodem:

- za mało za podpis,
- za niska punktówka,
- nieakceptowalna rola,
- przy dłuższej umowie oczekuje lepszej gwarancji itd.

### Cierpliwość negocjacyjna

Liczba rund **nie jest stała**. Zawodnik może zerwać rozmowy, jeżeli gracz zbyt długo lub zbyt mocno naciska.

Nie dodajemy nowej widocznej cechy „negocjacje”. Cierpliwość może wynikać m.in. z:

- Ambicji,
- Profesjonalizmu,
- związku z klubem,
- jakości i kierunku kolejnych ofert.

Silny lowball albo próba obniżenia roli może zużywać dużo więcej cierpliwości niż niewielka różnica finansowa.

Gracz nie musi widzieć licznika „zostały 3 rundy”; powinien odczytywać stan z komunikatów zawodnika.

### Związek z klubem

- **wychowanek** → dużo większa cierpliwość i łatwiejsze rozmowy,
- **obecny zawodnik klubu** → umiarkowany bonus,
- wieloletnia historia w klubie → możliwy dodatkowy bonus.

Wychowanek nie ma jednak obowiązku podpisać taniej ani zostać za wszelką cenę.

### Później — klubowe preferencje i rywalizacje

Do późniejszego systemu zapisujemy:

- rangę klubu,
- historię zawodnika w klubach,
- status wychowanka,
- wieloletni związek z klubem,
- kluby skonfliktowane / derbowe,
- różnicę w odczuwaniu tych relacji przez zawodników krajowych i zagranicznych.

Przykład kierunku: wieloletni zawodnik Stali Gorzów powinien być mniej skłonny do przejścia do Falubazu Zielona Góra, a wychowanek powinien reagować jeszcze mocniej. Nie ma to być absolutny zakaz transferu.

---

## 8. Kondycja — BINDING kierunek, liczby PROVISIONAL

Kondycja jest jedną dynamiczną wartością wewnętrzną, najlepiej w skali **0–100**.

UI może pokazywać przede wszystkim opis:

- Świetna
- Bardzo dobra
- Dobra
- Przeciętna
- Słaba
- Bardzo słaba
- Fatalna

Dokładne progi nazw są kalibracyjne.

### Zasada główna

Normalne **5–6 biegów jednego dnia** nie powinno samo w sobie powodować widocznego spadku sportowego.

Zawodnik powinien być również zdolny do:

- 5–6 biegów jednego dnia,
- kolejnych 5–6 biegów następnego dnia,

bez zauważalnej kary w symulacji, mimo że jego wewnętrzna Kondycja jest już niższa.

Znaczenie Kondycji ma pojawiać się głównie przy kumulacji:

- wielu zawodów w krótkim czasie,
- trudnego toru,
- dużej liczby biegów,
- bardzo krótkich przerw,
- upadków,
- 3–4 zawodów w krótkim okresie.

### Koszt biegu — wartości startowe do testów

Roboczy prosty kierunek:

- łatwy tor: około **-2**,
- normalny: około **-3**,
- trudny: około **-4**,
- ekstremalnie wymagający: około **-5**.

Każdy kolejny bieg w tych samych zawodach może być odrobinę bardziej męczący. Wartość powinna rosnąć łagodnie, a nie skokowo.

Dodatkowo:

- dwa biegi pod rząd → większy koszt drugiego,
- 3 starty w krótkim odcinku programu → dodatkowe zmęczenie,
- upadek → znacznie większy koszt niż zwykły bieg.

### Regeneracja podczas zawodów

Im dłuższa przerwa od poprzedniego startu, tym większy odzysk, ale z malejącym przyrostem.

Przerwa na równanie toru daje dodatkową regenerację, bo trwa dłużej.

Roboczo:

- bieg po biegu → prawie brak regeneracji,
- kilka biegów przerwy → stopniowy odzysk,
- 5+ biegów przerwy → duża część możliwej regeneracji meczowej.

### Regeneracja między dniami

Regeneracja nocna / dobowa jest **dużo silniejsza** niż odpoczynek między biegami.

Po zwykłych zawodach następnego dnia zawodnik powinien nadal zaczynać w strefie, w której Kondycja nie daje realnej kary.

Problem zaczyna się dopiero przy skumulowanym kalendarzu.

### Wpływ na jazdę

Kondycja nie powinna po prostu obcinać Vmax.

Przy wysokich wartościach efekt jest zerowy albo praktycznie niewidoczny. Przy niskich może stopniowo zwiększać:

- ryzyko błędu,
- trudność ratowania motocykla,
- gorszą precyzję walki,
- słabszą reakcję w nieoczekiwanej sytuacji.

Dokładne progi wpływu wymagają testów.

---

## 9. Trening i rozwój — BINDING dla organizacji treningu

### Juniorzy

Juniorzy pozostają pod wyraźnie większą kontrolą klubu.

Manager może planować im **tydzień dzień po dniu**, np.:

- trening startów,
- trening techniczny,
- trening wyścigowy,
- trening ścieżek / torowy,
- trening fizyczny,
- regeneracja,
- zawody.

System ma być **półautomatyczny**:

- dzień zawodów automatycznie blokuje normalny trening,
- po zawodach następny dzień domyślnie staje się regeneracją,
- przy napiętym kalendarzu system sam proponuje lżejszy plan,
- manager może ingerować, ale gra ostrzega przed zbyt dużym obciążeniem.

Nie potrzebujemy wielostopniowej intensywności 1–10 ani mikrozarządzania godzinami.

### Seniorzy i U24

Seniorzy i regularnie jeżdżący U24 prowadzą większość własnego treningu poza bezpośrednią kontrolą klubu, szczególnie dlatego, że mogą startować w kilku ligach i zawodach zagranicznych.

Ich ogólny trening może dziać się w tle i korzystać z istniejących cech osobowości, zwłaszcza:

- Profesjonalizmu,
- Ambicji.

Manager klubu może jednak organizować **trening klubowy**, szczególnie przed domowym meczem.

Główna wartość takiego treningu:

- poznanie aktualnego toru,
- sprawdzenie Motoru A/B,
- zebranie feedbacku,
- przygotowanie wstępnego setupu,
- przygotowanie ścieżek pod konkretny mecz.

Przy luźniejszym kalendarzu senior/U24 może również odbyć dodatkowy trening, ale manager nie układa mu pełnego tygodnia jak juniorowi.

### Jeszcze nieustalone

Do późniejszej decyzji pozostają m.in.:

- dokładny model potencjału zawodnika,
- tempo wzrostu poszczególnych atrybutów,
- dokładne krzywe wieku i regresu,
- siła wpływu Profesjonalizmu i Ambicji,
- dokładny efekt regularności startów na rozwój.

---

## 10. Kontuzje i urazy — BINDING kierunek

Nie dodajemy osobnej cechy **podatność na kontuzje**.

Urazy wynikają z konkretnych zdarzeń:

- upadku,
- kontaktu,
- uderzenia,
- innego incydentu fizycznego.

Prosty podział wystarczy:

- stłuczenie / lekki uraz — możliwa dalsza jazda, większy koszt Kondycji,
- drobna kontuzja — krótka przerwa lub ograniczona możliwość jazdy,
- średnia kontuzja — tygodnie przerwy,
- ciężka kontuzja — dłuższa absencja.

Ciężkość urazu wynika z przebiegu zdarzenia i kontrolowanej losowości, nie z ukrytego „injury prone”.

---

## 11. Relacje — zapis ograniczenia

System relacji zostaje na później i ma być lekki.

Na obecnym etapie:

- **nie tworzymy relacji zawodnik–mechanik**,
- ewentualne relacje manager–zawodnik i zawodnik–zawodnik będą rozważane później,
- więź zawodnika z klubem jest osobnym elementem kariery/kontraktów, nie relacją z mechanikiem.

---

## 12. Rzeczy nadal świadomie odłożone

Nie są jeszcze finalnie zaprojektowane:

- dokładne krzywe czterech regulatorów setupu,
- dokładna siła błędnego setupu i katalog jego skutków,
- finalne progi Kondycji i jej liczbowe tempo zmian,
- pełny rozwój/potencjał i regres wieku,
- szczegóły kontuzji,
- lekki system relacji,
- ranking/ranga klubów i pełny model klubowych animozji,
- balans wszystkich powyższych systemów względem Race Engine.

Przy implementacji należy najpierw przenieść odpowiednie decyzje z tego snapshotu do właściwych kanonicznych dokumentów, zamiast traktować ten plik jako pretekst do równoległego systemu.


---

## 13. Prowadzenie meczu i skład — BINDING

Manager prowadzi mecz samodzielnie. Gra nie dobiera za niego składu, zmian ani biegów nominowanych.

### Skład meczowy

Pozycje startowe zależą od gospodarza / gościa:

- gospodarz obsadza numery **1–8**,
- gość obsadza numery **9–16**.

Obsada musi być zgodna z aktualnym `Ruleset` danej ligi i sezonu.

Ruleset ma określać m.in.:

- które numery są przeznaczone lub ograniczone dla juniorów,
- które pozycje dotyczą U24,
- wymagania dotyczące zawodników krajowych i zagranicznych,
- inne ograniczenia składu wynikające z regulaminu.

Nie kodujemy reguł typu „numer 6 = junior” na stałe w UI. Powinny pochodzić z konfiguracji ligi/sezonu.

### Decyzje w trakcie meczu

Manager sam wykonuje:

- zwykłe zmiany,
- rezerwy,
- zmiany taktyczne,
- zastępstwa,
- wykorzystanie U24 i rezerwowych,
- reakcje na kontuzje / wykluczenia,
- wybór zawodników do biegów nominowanych.

UI ma pilnować legalności decyzji i wyjaśniać, dlaczego dana zmiana jest niedozwolona. Nie powinno automatycznie podpowiadać „najlepszego” zawodnika jako decyzji za gracza.

AI rywala korzysta z tych samych zasad i ograniczeń.

---

## 14. Morale zawodnika — BINDING kierunek

Morale jest jednym głównym, dynamicznym stanem zawodnika.

Może być prezentowane opisowo, np.:

- Bardzo wysokie
- Wysokie
- Dobre
- Neutralne
- Niskie
- Bardzo niskie

Dokładna skala i progi pozostają do kalibracji.

### Główne źródła zmian morale

Morale zmieniają przede wszystkim:

1. **wyniki drużyny** — najważniejszy stały czynnik,
2. **własne wyniki w biegach**,
3. **wykorzystanie zgodnie z obiecaną rolą**,
4. decyzje managera dotyczące składu i ważnych biegów.

### Wyniki drużyny

Zwycięstwa drużyny powinny wyraźnie podnosić morale, a serie zwycięstw budować je w dłuższym okresie.

Porażki i serie porażek działają w przeciwną stronę.

`Zespołowość` może zwiększać wagę wyniku drużyny:

- zawodnik bardzo zespołowy mocniej przeżywa sukces/porażkę całej drużyny,
- indywidualista większą wagę przykłada do własnego występu.

Nawet indywidualista nadal reaguje na wynik drużyny; różni się tylko siła efektu.

### Wynik indywidualny

**Zwycięstwo w biegu zawsze podnosi morale**, również wtedy, gdy zawodnik był faworytem.

Siła bonusu rośnie wraz z trudnością osiągnięcia:

- zwycięstwo z wyraźnie słabszymi → normalny plus,
- zwycięstwo z podobnymi → większy plus,
- zwycięstwo z dużo mocniejszymi → bardzo duży plus.

Przykład: zwycięstwo juniora w biegu z mocnymi seniorami powinno dawać znacznie większy impuls niż zwycięstwo w typowym biegu juniorskim.

Model jest celowo **asymetryczny**:

- przegrana juniora z liderami może być prawie neutralna,
- przegrana lidera z juniorem daje minus,
- ale pojedyncza niespodziewana porażka Lidera nie może kasować kilku wcześniejszych zwycięstw.

### Rola i traktowanie przez klub

Niezadowolenie z roli lub klubu wpływa negatywnie na główne morale.

Przykłady:

- Lider regularnie pomijany w nominowanych mimo dobrej jazdy,
- Podstawowy regularnie traci swoje programowe biegi,
- Rezerwowy przez bardzo długi okres nie dostaje żadnej realnej szansy,
- zawodnik jest często odsuwany od składu mimo ustaleń kontraktowych.

Dla logiki system może wewnętrznie pamiętać przyczynę niezadowolenia (np. wykorzystanie roli), ale gracz nie potrzebuje osobnego równorzędnego paska „zadowolenia z klubu”.

`Ambicja` zwiększa wrażliwość na niewłaściwe wykorzystanie i status zawodnika.

### Wpływ morale na jazdę

Morale nie powinno działać jako duży, bezpośredni procentowy bonus/karę do prędkości.

Główny wpływ dotyczy:

- zachowania poza torem,
- reakcji na decyzje managera,
- negocjacji i chęci pozostania w klubie,
- gotowości do akceptowania roli,
- treningu / podejścia do obowiązków tam, gdzie istnieje odpowiedni konsument.

Ewentualny wpływ na samą jazdę ma być niewielki i wymaga osobnej kalibracji.

---

## 15. Scouting zawodników — BINDING kierunek

Klub posiada bazowo **1 scouta**.

Ewentualny drugi scout może zostać rozważony później jako możliwość rozwoju organizacji, ale nie jest standardem.

Każdy scout prowadzi **jedną aktywną obserwację zawodnika naraz**.

### Widoczność 8 umiejętności

Prawdziwe umiejętności zawodnika nadal istnieją w skali 1–99, ale scout nie pokazuje ich dokładnie.

Gracz widzi **przedziały**, np.:

- Start: 72–80,
- Technika: 68–76,
- Atak: 75–84.

Lepszy scout daje węższy przedział.

Dokładne szerokości przedziałów są do kalibracji.

### Potencjał

Potencjał istnieje wewnętrznie jako wartość liczbowa.

Gracz widzi go wyłącznie opisowo, np.:

- Niski
- Przeciętny
- Dobry
- Wysoki
- Bardzo wysoki
- Świetny

Scout może pomylić się w ocenie potencjału o **maksymalnie jeden poziom opisowy**.

Przykład:

- rzeczywisty potencjał: Świetny,
- raport scouta: Bardzo wysoki albo Świetny,
- nie powinien spaść do „Dobry”.

### Czas obserwacji

Preferowany prosty model:

- około **7 dni** → raport wstępny,
- około **14 dni** → raport pełniejszy.

Po dłuższej obserwacji:

- przedziały umiejętności się zawężają,
- ocena potencjału może zostać skorygowana,
- wcześniejsza wiedza nie resetuje się.

Znajomość zawodnika z własnej ligi, wcześniejszy scouting albo wcześniejsza gra w klubie mogą skracać potrzebny czas lub poprawiać dokładność.

### Scout — kompetencja

Na obecnym etapie wystarcza jedna główna jakość scouta związana z oceną zawodników.

Nie dodajemy osobnych ratingów „juniorzy”, „zagranica”, „aktualne umiejętności”, „potencjał” bez konkretnej potrzeby gameplayowej.

---

## 16. Trener młodzieży — BINDING

Nie potrzebujemy osobnej cechy „Ocena rozwoju”. Ocenianiem poziomu/potencjału zajmuje się scouting.

Trener młodzieży ma specjalizacje odpowiadające grupom treningowym:

1. **Starty**
2. **Technika**
3. **Jazda wyścigowa**
4. **Czytanie toru**
5. **Przygotowanie fizyczne**

Mapowanie na zawodnika:

- Starty → Reakcja + Start,
- Technika → Technika,
- Jazda wyścigowa → Atak + Obrona + Jazda parą,
- Czytanie toru → Czytanie toru,
- Przygotowanie fizyczne → Siła.

Trener może być mocny w jednych obszarach i przeciętny w innych.

Nie dodajemy kolejnych ogólnych atrybutów trenera bez konkretnego konsumenta.

---

## 17. Finanse klubu — BINDING kierunek

Nie ma osobnego, sztywnego „budżetu transferowego” ani salary capu narzucanego graczowi przez interfejs.

Klub posiada **jedno rzeczywiste saldo**, ale gra śledzi też:

- prognozę przychodów i kosztów sezonu,
- już zawarte zobowiązania kontraktowe,
- przewidywane saldo końcowe.

Manager może podejmować ryzyko finansowe, ale zarząd może blokować skrajnie niebezpieczne zobowiązania.

Zarząd nie powinien blokować każdej trochę ryzykownej decyzji.

### Przychody

Model powinien obejmować co najmniej:

1. centralną wypłatę ligi / TV,
2. sponsorów,
3. wsparcie miasta,
4. bilety i karnety,
5. VIP / gastronomię / merchandising — mogą być agregowane,
6. dodatkowe wydarzenia,
7. transfery / wypożyczenia,
8. ewentualne wkłady właścicieli / akcjonariuszy,
9. kredyty / pożyczki jako finansowanie, a nie przychód operacyjny.

### Koszty

Co najmniej:

1. zawodnicy — podpisy,
2. zawodnicy — punkty,
3. zawodnicy — startowe, jeśli pozostaną w systemie,
4. personel,
5. szkolenie juniorów,
6. organizacja meczów,
7. tor i koszty techniczne,
8. administracja,
9. marketing,
10. koszty stadionu wynikające z warunków użytkowania,
11. dodatkowe imprezy,
12. obsługa zadłużenia.

### Budżet klubu między sezonami

Każdy klub ma własną bazową siłę finansową, ale jego realny budżet zmienia się co roku.

Nie losujemy całego budżetu jedną wartością.

Budżet wynika z osobnych źródeł, np.:

`TV + miasto + sponsorzy + bilety + inne`

Dzięki temu gracz widzi, **dlaczego** klub ma w danym sezonie więcej lub mniej środków.

Normalna zmienność powinna być umiarkowana, natomiast większe skoki mogą powodować:

- awans / spadek,
- duży nowy sponsor,
- utrata sponsora,
- wyraźna zmiana wsparcia miasta,
- wyjątkowy wynik sportowy lub kryzys.

---

## 18. Sponsorzy — BINDING kierunek

W grze zarządzamy niewielką liczbą istotnych sponsorów zamiast odwzorowywać dziesiątki małych firm.

### Zarządzane umowy

Preferowany model:

- **1 Sponsor tytularny**,
- **2 Sponsorów głównych**,
- reszta jako zagregowana pozycja **Pozostali partnerzy**.

Pozostali partnerzy dają mniejszą, względnie stabilną łączną kwotę i nie wymagają indywidualnych negocjacji.

### Oferty sponsorów

Sponsorzy **zgłaszają się do klubu z ofertami**, a gracz wybiera, które przyjąć.

Oferta nie składa się wyłącznie z jednej kwoty.

Może zawierać:

- kwotę gwarantowaną,
- długość umowy,
- premie za osiągnięcia.

Przykładowe premie:

- utrzymanie,
- play-off,
- finał,
- mistrzostwo,
- awans.

Cele sponsora powinny być dopasowane do realnego poziomu klubu.

Beniaminek może dostać dużą premię za utrzymanie, podczas gdy kandydat do tytułu otrzymuje większe premie za finał/mistrzostwo.

### Różne profile ofert

Oferty powinny tworzyć decyzję managerską.

Przykład:

- sponsor A: wysoka kwota gwarantowana + małe premie,
- sponsor B: niższa gwarancja + bardzo wysokie premie za sukces,
- sponsor C: dłuższa, stabilna umowa na kilka sezonów.

### Co wpływa na ofertę

Główne czynniki:

- ranga klubu,
- poziom ligi,
- ostatnie wyniki,
- frekwencja / popularność,
- ekspozycja TV,
- historia sponsora z klubem.

Nie potrzebujemy kilkunastu dodatkowych współczynników.

### Relacja sponsor–klub

Istniejący sponsor może:

- przedłużyć umowę,
- zwiększyć zaangażowanie,
- zmienić poziom współpracy,
- obniżyć ofertę,
- odejść po spadku lub utracie ekspozycji.

Sponsor nie powinien być co roku wyłącznie losowany od zera.

---

## 19. Ranga klubu — BINDING kierunek

Każdy klub posiada długoterminową **rangę / reputację**.

Może istnieć wewnętrznie jako wartość liczbowa, a gracz może widzieć poziom opisowy.

Przykładowe opisy:

- Lokalny
- Rozpoznawalny
- Uznany
- Duży klub
- Czołowy klub
- Potęga

Dokładne nazwy/progi mogą zostać dopracowane.

### Zasada

Ranga zmienia się **powoli**.

Jeden dobry sezon nie tworzy od razu potęgi, a jeden spadek nie kasuje wieloletniej historii.

Wpływają na nią m.in.:

- poziom ligi w dłuższym okresie,
- wyniki z kilku sezonów,
- mistrzostwa / medale,
- frekwencja i popularność,
- historia klubu,
- długość obecności na wysokim poziomie.

### Ranga vs bieżąca atrakcyjność

Rozdzielamy:

- **Ranga klubu** — długoterminowa marka,
- **Bieżąca atrakcyjność** — ranga + aktualna liga + ostatnie wyniki + bieżąca sytuacja finansowa / sportowa.

To może być wykorzystywane przez:

- sponsorów,
- zawodników przy wyborze klubu,
- frekwencję,
- inne przyszłe systemy.

---

## 20. Frekwencja i ceny biletów — BINDING kierunek

Frekwencja na konkretny mecz zależy od:

- popularności/rangi gospodarza,
- atrakcyjności rywala,
- rangi wydarzenia,
- bieżących wyników,
- ceny biletu,
- pojemności stadionu.

### Ranga wydarzenia

Ranga konkretnego meczu jest niezależna od samej rangi klubu.

Może rosnąć m.in. przez:

- derby,
- historycznego rywala,
- mecz dwóch czołowych drużyn,
- play-off,
- finał,
- mecz o awans,
- mecz o utrzymanie,
- ważny mecz pod koniec sezonu.

Wyższa ranga wydarzenia:

- podnosi zainteresowanie,
- zwiększa akceptowalną cenę biletu,
- może zwiększać przychód meczowy.

### Cena biletu

Manager ustala bazową cenę i może ją zmienić dla konkretnego meczu.

Wyższa cena może obniżać frekwencję, ale przy bardzo atrakcyjnym wydarzeniu kibice są mniej wrażliwi na podwyżkę.

Gra powinna tworzyć prosty wybór:

- wyższa cena i potencjalnie mniej kibiców,
- niższa cena i większa szansa na pełniejszy stadion.

---

## 21. Stadion — BINDING ograniczenie

Nie projektujemy obecnie systemu rozbudowy stadionu.

Każdy klub otrzyma **realną pojemność swojego stadionu / obiektu** na podstawie danych wejściowych.

Pojemność jest limitem frekwencji.

Nie dodajemy:

- drzewka rozbudowy stadionu,
- budowy nowych trybun przez gracza,
- rozwoju infrastruktury jako osobnej ścieżki progresji.

Rozwój klubu ma odbywać się przede wszystkim przez:

- wyniki,
- skład,
- sponsorów,
- rangę,
- budżet i zarządzanie sportowe.

Koszty użytkowania stadionu mogą różnić się między klubami zależnie od realnego modelu własności/umowy z miastem, ale nie oznacza to zarządzania inwestycjami stadionowymi.
