# Model zawodnika

**Status: BINDING**

Ten dokument opisuje docelowy model zawodnika dla warstwy gameplayowej Speedway Managera.

Nie jest zgodą na natychmiastową zmianę istniejącego `RiderSkills`, `RiderStyle` ani fizyki produkcyjnej. Obecny kod może nadal używać starszych nazw i uproszczeń do czasu osobnych, wąskich PR-ów implementacyjnych.

## Zasada nadrzędna

Parametry opisują zdolności zawodnika, a nie fazy wyścigu. Jedna umiejętność może wpływać na kilka sytuacji, jeśli jej rola pozostaje logicznie taka sama.

Kolejność rozstrzygania:

**warunki → percepcja → decyzja → wykonanie → fizyka → wynik**

Żadna pojedyncza statystyka nie powinna jednocześnie wybierać decyzji, poprawiać wykonania, zwiększać fizycznego limitu i ograniczać konsekwencji błędu.

## 1. Dane podstawowe

- imię i nazwisko — tekst,
- data urodzenia — data,
- wiek — wyliczany,
- narodowość — kategoria,
- wzrost — cm, informacyjnie w v1,
- masa — kg, używana przez fizykę,
- status regulaminowy — np. junior / U24 / senior.

## 2. Główne umiejętności sportowe — 1–99

### Start

| Cecha | Odpowiedzialność |
|---|---|
| **Przygotowanie pola** | wybór miejsca, przygotowanie koleiny i ustawienie przed startem |
| **Reakcja** | reakcja na zwolnienie taśmy |
| **Wyjście spod taśmy** | sprzęgło, gaz, wykorzystanie przyczepności i pierwsze metry |

Dojazd do pierwszego łuku nie jest osobną umiejętnością. Wynika z powyższych cech, fizyki, setupu, toru i decyzji zawodnika.

### Technika jazdy

| Cecha | Odpowiedzialność |
|---|---|
| **Technika łuku** | wejście, przejazd przez środek i przygotowanie wyjścia z łuku |
| **Panowanie nad motocyklem** | gaz, trakcja, wheelspin, uślizg i korekty zachowania motocykla |
| **Balans** | praca ciałem, stabilizacja i obciążanie motocykla |

Rozdzielenie jest obowiązujące:
- Technika łuku = geometria i sposób przejazdu.
- Panowanie nad motocyklem = kontrola motocykla podczas wykonania.
- Balans = praca ciałem i stabilizacja.

### Umiejętności wyścigowe

| Cecha | Odpowiedzialność |
|---|---|
| **Czytanie toru** | rozpoznanie przyczepności, kolein, zmian nawierzchni i działających ścieżek |
| **Zmysł wyścigowy** | przewidywanie rywali i wybór właściwego rozwiązania |
| **Atak** | wykonanie manewru ofensywnego przeciw rywalowi |
| **Obrona** | utrzymanie pozycji i reakcja na atak |
| **Jazda parą** | współpraca z partnerem podczas biegu |

Czytanie toru dotyczy nawierzchni. Zmysł wyścigowy dotyczy rywali i sytuacji wyścigu.

### Fizyczność

- **Siła** — 1–99.
- **Wytrzymałość** — 1–99.
- **Regeneracja** — 1–99.
- **Masa** — rzeczywiste kg.

Siła nie zwiększa mocy motocykla ani Vmax. Wytrzymałość odpowiada za utrzymanie jakości przy kolejnych wysiłkach. Regeneracja odpowiada za odzyskiwanie sprawności między wysiłkami.

### Psychika

- **Jazda pod presją** — 1–99; ogranicza pogorszenie wykonania w sytuacjach wysokiej stawki.
- **Determinacja** — 1–99; wpływa na kontynuowanie walki po błędzie, przegranym starcie lub nieudanym ataku.
- **Koncentracja** — 1–99; ważna przy reakcji startowej, obronie, koleinach, nagłych zdarzeniach i zmęczeniu.

Koncentracja nie jest ogólnym mnożnikiem wszystkich umiejętności.

## 3. Styl zawodnika

- **Waleczność** — 1–99; jak chętnie zawodnik podejmuje bezpośrednią walkę. Nie zwiększa jakości Ataku.
- **Preferowana linia** — kategoria: wewnętrzna / neutralna / szeroka. Nie daje bonusu do prędkości.
- **Niezależność** — 1–99; jak mocno zawodnik obstaje przy własnym zdaniu dotyczącym setupu.

Wysoka wartość stylu nie zawsze oznacza „lepiej”.

## 4. Osobowość — 1–99

- **Profesjonalizm** — podejście do treningu, regeneracji i prowadzenia kariery.
- **Ambicja** — poziom oczekiwań sportowych i statusowych.
- **Zespołowość** — gotowość działania dla dobra drużyny.
- **Chęć nauki** — wykorzystanie treningu, uwag i mentoringu.
- **Opanowanie** — siła i długość reakcji emocjonalnych na wydarzenia.
- **Mentoring** — zdolność przekazywania własnej wiedzy innym.
- **Ugodowość** — skłonność do kompromisu i akceptowania decyzji niezgodnych z własnym interesem.

Ugodowość nie jest Zespołowością. Ambicja nie jest osobną „chęcią odejścia”.

## 5. Preferencje torowe

Preferencje są kategoryczne, bez skali 1–99.

Zawodnik może preferować albo nie lubić:
- **twardej nawierzchni**,
- **przyczepnej nawierzchni**,
- **mokrego toru**,
- **luźnej nawierzchni**,
- **nierównego / pokoleinowanego toru**,
- **technicznych torów**,
- **szybkich torów**.

Nie każdy zawodnik musi posiadać preferencję.

Tor sam posiada ciągłe właściwości fizyczne, np. twardość, wilgotność, grip, luźny materiał i nierówności. System ocenia dopasowanie aktualnego stanu do preferencji zawodnika.

Preferencja daje niewielki efekt komfortu, stabilności wykonania i łatwości znalezienia ustawień; nie daje bezpośredniego bonusu do prędkości.

Budowa składu pod charakter własnego toru jest zamierzoną strategią. Nadmierna specjalizacja ma koszt na wyjazdach i przy zmianie warunków.

## 6. Doświadczenie i znajomość

Wewnętrznie wartości mogą być ciągłe, ale UI powinno pokazywać je głównie opisowo, np.:

**brak → niewielka → podstawowa → dobra → bardzo dobra → doskonała**

Obszary:
- doświadczenie meczowe,
- doświadczenie w danych rodzajach nawierzchni,
- doświadczenie na technicznych i szybkich torach,
- znajomość konkretnego toru,
- znajomość konkretnego motocykla / silnika.

Znajomość konkretnego toru ma trzy warstwy:
1. trwała znajomość geometrii,
2. znajomość typowego zachowania nawierzchni,
3. krótkoterminowa znajomość aktualnego stanu.

Pogoda może zniszczyć trzecią warstwę, ale nie usuwa znajomości geometrii.

## 7. Stan bieżący

Stany nie są trwałymi cechami zawodnika.

### Morale

Wewnętrznie może być ciągłe, ale UI pokazuje poziom opisowy:

**fatalne → bardzo niskie → niskie → średnie → dobre → bardzo dobre → świetne**

Morale zależy m.in. od:
- wyników,
- zgodności faktycznego wykorzystania z rolą,
- decyzji managera,
- wyników drużyny,
- kontraktu,
- kontuzji,
- konfliktów i ważnych wydarzeń.

Siłę reakcji modyfikują Ambicja, Ugodowość, Zespołowość, Opanowanie i Profesjonalizm.

Morale nie daje prostego bonusu do prędkości.

### Pozostałe stany

- **Zmęczenie** — opisowo: świeży → wyczerpany.
- **Stan fizyczny / zdrowie**.
- **Kondycja meczowa** — gotowość do regularnego ścigania po przerwie lub kontuzji.
- **Kontuzje** — konkretne urazy i ich stan.

Zdrowie i kondycja meczowa to różne rzeczy.

## 8. Ukryta forma

Zawodnik posiada ukrytą **Formę okresową** oraz niewielką **Dyspozycję dnia**.

Forma okresowa:
- trwa od kilku spotkań do kilku tygodni,
- wpływa na powtarzalność, drobne błędy i wykorzystanie bazowych umiejętności,
- nie zmienia bazowych statystyk,
- ma częściowo losowy charakter,
- zależy również od regularności jazdy, treningu, zmęczenia, kontuzji i morale,
- ma tendencję do powrotu do nominalnego poziomu zawodnika.

Dyspozycja dnia to mniejsze odchylenie dotyczące konkretnego meczu.

## 9. Relacje

Relacje mają być lekkim systemem, nie symulatorem szatni.

### Zaufanie do managera

Wewnętrznie ciągłe, w UI opisowe.

Wpływa na:
- akceptację setupu,
- reakcję na rolę i decyzje personalne,
- rozmowy,
- kontrakty.

### Zaufanie do mechanika

Analogicznie; dotyczy głównie sprzętu i setupu.

### Relacje zawodnik–zawodnik

Przechowywane tylko, gdy są istotne:
- konflikt,
- napięta,
- neutralna,
- dobra,
- bardzo dobra,
- mentor–uczeń.

Nie tworzymy pełnej liczbowej macierzy każdy-z-każdym.

## 10. Rola w drużynie

Kategorie:
- **Lider**,
- **Podstawowy**,
- **Uzupełnienie składu**,
- **Rezerwowy**.

Oczekiwania dotyczące liczby i znaczenia biegów wynikają z roli. Nie istnieje osobny parametr „oczekiwane biegi”.

Reakcja zawodnika na zmianę zależy od kontekstu meczu, roli, wyników, Ugodowości, Ambicji, Zespołowości i zaufania do managera.

## 11. Kontrakt

Podstawowe elementy:
- długość umowy,
- kwota za podpis,
- stawka za punkt,
- ewentualna stawka za start,
- wybrane premie,
- uzgodniona rola.

Nie istnieją osobne statystyki:
- chęć odejścia,
- lojalność,
- oczekiwana liczba biegów.

Decyzja o pozostaniu lub odejściu wynika z sytuacji: morale, roli, Ambicji, zaufania, finansów, poziomu klubu i konkurencyjnych ofert.

## 12. Rozwój i potencjał

Każda rozwijalna umiejętność może posiadać ukryte:
- aktualny poziom,
- potencjał życiowy,
- aktualnie osiągalny potencjał,
- tempo uczenia.

Nie ma jednego ogólnego „Potential 92”.

Największy wpływ na rozwój mają prawdziwe zawody. Rozwija się przede wszystkim to, czego zawodnik rzeczywiście używa i doświadcza.

Zawodnik ma indywidualne:
- krzywe rozwoju technicznego, wyścigowego, fizycznego i psychicznego,
- okresy szybszego i wolniejszego rozwoju,
- różne przedziały szczytu dla różnych grup zdolności,
- tempo starzenia i regresu.

Rozwój po 30. roku życia jest możliwy, szczególnie w cechach doświadczeniowych i taktycznych, ale słaby zawodnik nie powinien nagle stać się mistrzem tylko dzięki późnemu skokowi.

## 13. Obciążenie kariery

Ukryty stan długoterminowy wynikający m.in. z:
- liczby zawodów i biegów,
- intensywnych sezonów,
- jazdy w wielu ligach,
- niedostatecznej regeneracji,
- kontuzji,
- jazdy mimo niepełnej sprawności.

Może wpływać na regenerację, podatność na urazy i późniejszy regres fizyczny.

Nie jest widocznym paskiem „zużycia”.

## 14. Kontuzje i powrót

Każdy uraz ma:
- rodzaj,
- lokalizację,
- ciężkość,
- stan leczenia,
- przewidywany czas powrotu,
- ryzyko nawrotu,
- konkretne ograniczenia.

Fazy:
1. niezdolny do jazdy,
2. medycznie zdolny, ale nie w pełni odbudowany,
3. pełna gotowość sportowa.

Manager może zdecydować o wystawieniu zawodnika nie w pełni odbudowanego, jeśli jest dopuszczony do jazdy. Powrót zbyt wcześnie może zwiększyć zmęczenie, ryzyko nawrotu i wydłużyć odbudowę.

Młodsi przeciętnie regenerują się szybciej, ale indywidualna Regeneracja pozostaje ważna.

## 15. Mentoring

Dobry zawodnik nie musi być dobrym mentorem.

Efekt mentoringu zależy od:
- wiedzy mentora w danym obszarze,
- jego Mentoringu,
- doświadczenia,
- Chęci nauki ucznia,
- relacji mentor–uczeń,
- wspólnej pracy.

Mentoring pomaga przekształcać doświadczenie w rozwój; nie daje bezpośrednio punktów umiejętności.

## 16. Trening

Manager ustala osobny kierunek treningu każdemu zawodnikowi każdego dnia.

Główne kierunki:
- Starty,
- Technika,
- Walka / sytuacje wyścigowe,
- Jazda parą,
- Trening fizyczny,
- Regeneracja,
- Odpoczynek,
- Rehabilitacja,
- Przygotowanie do meczu.

Zwykłe treningi są symulowane.

**Przygotowanie do meczu** może być interaktywne: ładowany jest rzeczywisty tor, manager obserwuje przejazdy, poznaje aktualne warunki, testuje setup i zbiera feedback.

Zwykły trening na konkretnym torze również zwiększa znajomość jego geometrii i charakterystyki, ale nie daje managerowi takiej samej bezpośredniej wiedzy jak interaktywne przygotowanie.

## 17. Sprzęt, mechanik i feedback

Zasada:
- **zawodnik mówi, co czuje**,
- **mechanik mówi, co jego zdaniem należy zmienić**,
- **manager podejmuje ostateczną decyzję**.

Jeden objaw może mieć kilka przyczyn jednocześnie. Zawodnik, mechanik i manager mogą widzieć różne części problemu.

Zawodnik opisuje objawy na podstawie m.in. Czytania toru, doświadczenia, znajomości sprzętu i własnych odczuć z jazdy.

Mechanik posiada co najmniej:
- Diagnostykę,
- Ustawianie motocykla,
- Przygotowanie sprzętu,
- Pracę pod presją.

Manager może ustawić setup inaczej niż proponuje mechanik.

Trafne decyzje managera stopniowo zwiększają zaufanie zawodnika i mechanika. Nietrafne je obniżają. Im niższe zaufanie, tym większa skłonność do obstawania przy własnej opinii.

Ocena decyzji setupowej opiera się na tym, czy rozwiązano konkretny problem, a nie tylko na wyniku biegu.

## 18. Czego celowo NIE przechowujemy jako osobnych cech

Nie ma osobnych statystyk:
- Szybkość,
- prędkość w łuku,
- wyjście z łuku,
- dojazd do pierwszego łuku,
- jawna Forma,
- Uniwersalność,
- Adaptacja,
- Ratowanie motocykla,
- Kontrola uślizgu,
- Kontrola gazu,
- Precyzja trajektorii,
- Wyczucie motocykla,
- Dynamika,
- Samokontrola,
- Skłonność do ryzyka,
- Pewność siebie,
- Lojalność,
- Chęć odejścia,
- Oczekiwana liczba biegów.

Są to efekty wynikające z innych parametrów, warunków lub stanu gry.

## 19. Tabela priorytetów wpływu

**P1 — kluczowy**, **P2 — ważny**, **P3 — sytuacyjny / kontekstowy**.

| Sytuacja | P1 | P2 | P3 / kontekst |
|---|---|---|---|
| Przygotowanie pola startowego | Przygotowanie pola | Czytanie toru | doświadczenie, znajomość toru |
| Reakcja na taśmę | Reakcja | Koncentracja | Jazda pod presją, forma |
| Wyjście spod taśmy | Wyjście spod taśmy | Panowanie nad motocyklem, Balans | Masa, Siła, setup, przyczepność |
| Dojazd do pierwszego łuku | wynik startu + fizyka | Zmysł wyścigowy | Atak, Obrona, Waleczność |
| Pierwszy łuk | Technika łuku, Zmysł wyścigowy | Panowanie nad motocyklem, Balans | Atak/Obrona, Koncentracja, Waleczność |
| Normalny przejazd łuku | Technika łuku | Panowanie nad motocyklem, Balans | Czytanie toru |
| Wyjście z łuku | Panowanie nad motocyklem | Technika łuku, Balans | Masa, setup, tor |
| Wybór ścieżki | Czytanie toru | Zmysł wyścigowy | Preferowana linia, doświadczenie |
| Atak | Atak | Zmysł wyścigowy | Waleczność, Technika łuku, Panowanie nad motocyklem |
| Obrona | Obrona | Zmysł wyścigowy | Koncentracja, Technika łuku, Waleczność |
| Przycinka / zwód | Zmysł wyścigowy, Atak | Technika łuku | Panowanie nad motocyklem, Koncentracja |
| Jazda parą | Jazda parą | Zmysł wyścigowy | Zespołowość, relacja z partnerem |
| Nagła koleina | Koncentracja, Balans | Panowanie nad motocyklem | Siła, doświadczenie |
| Kontakt z rywalem | Balans | Siła, Panowanie nad motocyklem | Koncentracja, Masa |
| Jazda w dużej presji | Jazda pod presją | Koncentracja | Zmysł wyścigowy |
| Walka po przegranym starcie | Determinacja | Zmysł wyścigowy | Atak, Waleczność |
| Piąty/szósty bieg | Wytrzymałość | Koncentracja | zmęczenie, kondycja meczowa |
| Regeneracja między wysiłkami | Regeneracja | Wytrzymałość | wiek, obciążenie kariery |
| Diagnoza nawierzchni | Czytanie toru | doświadczenie | znajomość toru |
| Akceptacja setupu managera | Zaufanie do managera | Niezależność | Zaufanie do mechanika, Ugodowość |
| Rozwój po zawodach | doświadczenie + potencjał | Chęć nauki | mentor, manager, Profesjonalizm |
| Rozwój treningowy | potencjał + trening | Chęć nauki, Profesjonalizm | sztab, wiek |
| Reakcja na odsunięcie od składu | rola + Ugodowość | Ambicja, Zaufanie do managera | Zespołowość, Opanowanie |
| Zmiana morale | wydarzenie + aktualne morale | Opanowanie | Ambicja, Ugodowość, Zespołowość |

## 20. Tabela granic odpowiedzialności

| Parametr | Odpowiada za | Nie powinien odpowiadać za |
|---|---|---|
| Przygotowanie pola | jakość przygotowania miejsca startowego | reakcję i przyspieszenie |
| Reakcja | moment reakcji na taśmę | dalszą jazdę |
| Wyjście spod taśmy | techniczne wykonanie pierwszych metrów | decyzje pierwszego łuku |
| Technika łuku | geometria i wykonanie przejazdu łuku | wybór najlepszej ścieżki |
| Panowanie nad motocyklem | trakcję, gaz, uślizg i korekty | decyzje taktyczne |
| Balans | pracę ciałem i stabilizację | czytanie nawierzchni |
| Czytanie toru | wiedzę o nawierzchni | fizyczne wykonanie |
| Zmysł wyścigowy | decyzje względem rywali | techniczną jakość motocykla |
| Atak | wykonanie ofensywnego manewru | częstotliwość atakowania |
| Obrona | wykonanie obrony | ogólną technikę jazdy |
| Jazda parą | wykonanie współpracy | chęć pomocy zespołowi |
| Siła | kontrolę fizyczną w wymagających sytuacjach | moc i prędkość motocykla |
| Wytrzymałość | odporność na narastające zmęczenie | regenerację między dniami |
| Regeneracja | odzyskiwanie sprawności | jakość pierwszego biegu świeżego zawodnika |
| Jazda pod presją | ograniczenie pogorszenia pod presją | bazową jakość jazdy |
| Determinacja | dalszą walkę mimo niepowodzeń | jakość techniczną ataku |
| Koncentracja | uwagę i reakcję na nagłe sytuacje | uniwersalny bonus do wszystkiego |
| Waleczność | skłonność do podejmowania walki | skuteczność manewru |
| Preferowana linia | naturalną skłonność wyboru linii | bonus do prędkości |
| Niezależność | obstawanie przy swoim setupie | wiedzę techniczną |
| Profesjonalizm | podejście do kariery | talent i potencjał |
| Ambicja | oczekiwania zawodnika | umiejętności sportowe |
| Zespołowość | gotowość działania dla zespołu | umiejętność Jazdy parą |
| Chęć nauki | wykorzystanie okazji rozwojowych | wysokość potencjału |
| Opanowanie | reakcje emocjonalne | Jazdę pod presją wprost |
| Mentoring | przekazywanie wiedzy | własny poziom sportowy |
| Ugodowość | gotowość do kompromisu | Zespołowość |

## 21. Priorytet implementacyjny

### P1 — pojedynczy bieg / Race Engine

- Przygotowanie pola
- Reakcja
- Wyjście spod taśmy
- Technika łuku
- Panowanie nad motocyklem
- Balans
- Czytanie toru
- Zmysł wyścigowy
- Atak
- Obrona
- Koncentracja
- Waleczność
- Masa
- Siła

### P2 — pełny mecz

- Jazda parą
- Wytrzymałość
- Regeneracja
- Jazda pod presją
- Determinacja
- Preferowana linia
- zmęczenie
- kondycja meczowa
- forma okresowa
- dyspozycja dnia

### P3 — warstwa managera i setupu

- Niezależność
- Zaufanie do managera
- Zaufanie do mechanika
- doświadczenie
- znajomość toru
- znajomość sprzętu
- preferencje torowe
- morale

### P4 — rozwój i kariera

- Profesjonalizm
- Ambicja
- Zespołowość
- Chęć nauki
- Opanowanie
- Mentoring
- Ugodowość
- potencjały
- krzywe rozwoju
- regres
- obciążenie kariery
- kontuzje
- kontrakty
- role
- relacje

## 22. Zasada implementacyjna

Nowy parametr może zostać dodany tylko wtedy, gdy da się jednoznacznie odpowiedzieć:
1. jaką decyzję lub zjawisko opisuje,
2. czego nie opisuje,
3. w której warstwie działa: percepcja / decyzja / wykonanie / fizyka / feedback / rozwój,
4. czy nie dubluje istniejącej cechy.

Jeśli dwie cechy wpływają na to samo w ten sam sposób, należy je scalić albo jedną usunąć.
