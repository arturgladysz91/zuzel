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
