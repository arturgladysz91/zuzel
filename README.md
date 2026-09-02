# Speedway Manager (Żużlowy) – Core Simulation

## Cel projektu
Celem projektu jest stworzenie gry typu **manager żużlowy**, w której kluczowe są:
- decyzje menedżera (przygotowanie toru, reakcje w trakcie meczu, sugestie setupu),
- zachowanie zawodników (styl, statystyki, morale, rozwój),
- **dynamicznie zmieniający się tor** (pogoda, prace torowe, degradacja przez jazdę).

Projekt **nie** próbuje odtwarzać pełnej fizyki motocykla. Zamiast tego stosujemy **ograniczenia fizyczne jako reguły**, które wymuszają realistyczne konsekwencje (np. wynoszenie w łuku przy zbyt dużej prędkości po ciasnej).

---

## Skrót architektury (high level)
Architektura jest podzielona na warstwy odpowiedzialności:

### 1) Dane i konfiguracja
- definicje torów, parametrów, profili zawodników,
- ustawienia pogody, zdarzeń, balansu (konfigurowalne w danych).

### 2) Symulacja (CoreSim)
Główna logika gry:
- stan toru (segmenty i linie),
- degradacja toru w czasie,
- decyzje zawodników (wybór linii per segment, ryzyko, reakcje),
- morale i wpływ na zachowanie,
- rozwój i regres w sezonie,
- setup (sugestie menedżera vs decyzje zawodnika).

### 3) Warstwa “host/gameplay”
- system akcji gospodarza (prace torowe, decyzje w przerwach),
- integracja z meczem 15-biegowym, logowanie przebiegu.

### 4) Prezentacja (UI / wizualizacja – później)
- wizualizacja jazdy oparta o wyniki symulacji,
- nie wpływa na logikę (rendering ≠ symulacja).

---

## INSTRUKCJE OGÓLNE – CORE SYMULACJI (OBOWIĄZUJĄCE)

### 0. Cel symulacji
Symulujemy **decyzje menedżerskie i zachowanie zawodników**, a nie pełną fizykę motocykla.
Fizyka występuje jako **ograniczenia i konsekwencje** (wynoszenie, utrata prędkości, ryzyko błędu).

---

### 1. Tor

#### 1.1 Struktura toru
Tor składa się z segmentów decyzyjnych:
- wejście w łuk
- środek łuku
- wyjście z łuku
- prosta

Każdy segment posiada **5 linii jazdy**.

Każdy konkretny tor przechowuje niezmienną geometrię: długość prostej, wewnętrzny promień referencyjny łuku, odstęp między liniami oraz kąt pojedynczego segmentu łuku. Geometria nie jest stanem nawierzchni i nie zmienia się podczas meczu.

#### 1.2 Linie jazdy
Linie są lokalne dla segmentu, nie przypisane do całego łuku ani biegu.

Zawodnik może wejść w łuk jedną linią, przejechać środek inną, wyjść jeszcze inną.

Linie są strefami referencyjnymi (decyzje/ocena pozycji), a faktyczny ruch odbywa się po ciągłej trajektorii; pozycja względem linii jest zmienną ciągłą.

Optymalna trajektoria nie musi oznaczać ciągłej jazdy po jednej linii.

W zależności od warunków toru (przyczepność, koleiny/zużycie, wilgotność) lepsza może być linia mieszana, np. wąsko na wejściu i szerzej na wyjściu, bo pozwala utrzymać płynność bez nadmiernego hamowania.

Model powinien premiować płynność i utrzymanie prędkości wyjściowej, a nie tylko “trzymanie krawężnika”.

Świadomy wybór szerokiej linii jest decyzją o trajektorii: może służyć znalezieniu lepszej nawierzchni, wyprzedzeniu albo przygotowaniu dłuższej prostej. Nie jest tym samym co wyniesienie, czyli nieplanowane lub wymuszone przesunięcie na zewnątrz po przekroczeniu ograniczenia, błędzie albo kontakcie.

Wyniesienie wydłuża drogę po zewnętrznej linii. Gdy wywołuje je przekroczenie ograniczenia, zawodnik nie zachowuje całej prędkości ponad fizyczną granicą; lepsze panowanie w poślizgu zmniejsza tę stratę, ale nie usuwa konsekwencji.

Zmiana linii:

zależy od stylu i umiejętności, oceny ryzyka kolizji z innym zawodnikiem.

nie jest natychmiastowa (ma bezwładność i ograniczenia przyczepności).
Linia = dyskretna strefa decyzyjna (0..4), lokalna dla segmentu.

Ruch = ciągły (pozycja lateralna float), a „linia” to najbliższa strefa referencyjna.

Zmiana linii ma bezwładność: nie przeskakujesz 1→5 w jednej klatce.

#### 1.3 Ograniczenie fizyczne w łuku (OBOWIĄZKOWE)
W łuku obowiązuje zasada:
> Przy danej prędkości istnieje minimalna linia możliwa do utrzymania.

Jeżeli zawodnik jedzie zbyt szybko po ciasnej linii, system musi wymusić:
1) spadek prędkości (hamowanie), albo  
2) wyniesienie na szerszą linię (z inercją i utratą części nadmiernej prędkości), albo
3) ryzyko błędu / straty.

Nie dopuszczamy “cudów”: szybka jazda przy krawężniku bez konsekwencji.
Kontakty między zawodnikami w środkowej fazie łuku mają istotnie większe konsekwencje (wyniesienie, utrata prędkości, upadek) niż na prostej.

Fizyczna granica przyczepności wynika z geometrii konkretnego toru, lokalnej nawierzchni, trajektorii, setupu i odpowiednich umiejętności kontroli. Morale nie zmienia tej granicy; może później wpływać na decyzję zawodnika, podejmowane ryzyko i jakość wykonania.

Przewaga linii wynika z połączenia geometrii konkretnego toru, nawierzchni i trajektorii. Porównanie przewidywanych czasów skrajnych linii jest diagnostyką danego toru, a nie testem ich sztucznej równości. Tor może premiować konkretną linię, ale żadna linia nie może być uniwersalnie najlepsza na wszystkich torach i nawierzchniach.

#### 1.4 Stan toru
Każdy segment i linia mają stan, m.in.:
- przyczepność
- koleiny
- wilgotność

Stan toru:
- zmienia się przez pogodę,
- jazdę zawodników,
- prace torowe,
- zmienia się nierównomiernie.

Część parametrów jest **ukryta przed graczem** (gracz nie ma “telemetrii”).

---

### 2. Proste

#### 2.1 Rola prostej
Prosta nie jest miejscem klasycznej walki.
Jej rolą jest **pozycjonowanie** przed kolejnym łukiem.

#### 2.2 Linie na prostej
Prosta ma 5 linii, ponieważ:
- wpływają na ustawienie do kolejnego łuku,
- umożliwiają blokowanie/obejście,
- są ważne dla realizmu wizualnego.

Linie na prostej **nie dają bonusu do prędkości**.

#### 2.3 Wyprzedzanie na prostej
Prosta nie dodaje osobnego outcome ataku ani arbitralnego bonusu linii.
Różnica może wynikać z lepszego wyjścia z poprzedniego łuku oraz fizycznego
profilu prędkości przejazdu; istniejące reguły kontaktu i interakcji pozostają
bez zmian.

Legacy zachowuje na prostej prędkość. Advanced physics używa pierwszego
distance-limited profilu longitudinal: zawodnik może przyspieszyć, a przed
bezpośrednio następującym łukiem kontrolowanie wytracić prędkość. Linie prostej
nadal nie dają arbitralnego bonusu; różnica wynika z umiejętności, wejściowej
nawierzchni, dostępnego dystansu i potrzeby przygotowania do następnego łuku.

Positive drive w advanced physics ma osiągalną, deterministyczną prędkość
szczytową. Bazowy zakres **PROVISIONAL / NOT REAL-WORLD CALIBRATED** `21–25 m/s`
zależy od `Speed`, a `BikeSetup.Gearing` mnoży go w równie prowizorycznym zakresie
`0.94–1.06`. Te wartości nie są jeszcze oparte na docelowej telemetrii i nie
opisują finalnej prędkości prawdziwego motocykla żużlowego. Oś `Gearing` oznacza
`drive-oriented ↔ speed-oriented`: `0` daje mocniejszy corner-exit drive kosztem
niższej osiągalnej prędkości, a `1` słabszy drive i wyższą osiągalną prędkość.
Nawierzchnia wpływa na dystans i czas potrzebny do osiągnięcia granicy, ale nie
zmienia samej granicy. Istniejące wzory i zachowanie pozostają bez zmian.

Nie jest to hard limiter: istniejąca prędkość równa lub większa od granicy nie
jest obcinana przez positive drive. Profil prostej może mieć fazy
`accelerate → cruise → decelerate`, a czas przejazdu jest sumą czasu każdej z
nich. Ten sam ceiling ogranicza positive drive na `TurnExit`. Legacy pozostaje
bez zmian. Drag, opory ruchu, krzywa mocy i optymalizacja przełożenia pod metę
nie są jeszcze modelowane.

Realny motocykl żużlowy jedzie podczas biegu na jednym biegu, nie ma klasycznego
układu hamulcowego, a przełożenie jest elementem setupu. Obecne kontrolowane
wytracanie prędkości nie oznacza hamowania jak motocyklem drogowym: docelowo ma
odzwierciedlać odjęcie gazu, ustawienie motocykla, uślizg i opory. Lookahead
bezpośredniego `TurnEntry` nie targetuje już settled safe speed. Używa
`MaxSafeTurnSpeed` jako settled speed i wyznacza maksymalną fizycznie odzyskiwalną
approach speed ze wzoru `sqrt(settled² + 2 * deceleration * (turnEntryDistance * 0.50))`,
na podstawie niezmiennej nawierzchni następnego łuku i wejściowego
`LateralPosition`.

W advanced physics pierwsze **PROVISIONAL / NOT REAL-WORLD CALIBRATED** `50%`
faktycznie pozostałego dystansu `TurnEntry` jest coarse fazą setting / roll-off /
slide-entry / speed scrub. Wspólna prowizoryczna capability `2.00–3.20 m/s²`
zależy od `SlideControl` i wejściowej nawierzchni bieżącego `TurnEntry`. Skuteczny
scrub może rozpocząć łuk powyżej settled speed i naturalnie zakończyć się `Ok`;
residual overspeed dopiero potem rozstrzyga niezmieniony `SegmentPhysics`, a enum
`Brake` nie oznacza hamulca tarczowego. Czas to czas scrub phase plus przejazd
pozostałego dystansu z prędkością po constraint; crash zachowuje globalne `50%`
remaining progress i wyłącznie czas scrub phase. `TurnMiddle`, `TurnExit` i legacy
pozostają bez tej fazy. Osobny późniejszy etap zbuduje spójny model dostępnego
napędu, gearing, drag, power curve i oporów zależnych od prędkości.

---

### 3. Zawodnicy

#### 3.1 Składniki zawodnika
Każdy zawodnik ma:
- statystyki (potencjał),
- styl jazdy (preferencje),
- morale (stan bieżący),
- cechy rozwojowe.

#### 3.2 Statystyki
Statystyki wpływają m.in. na:
- start, prędkość,
- panowanie w poślizgu,
- czytanie toru,
- jazdę parą,
- adaptację.

Część statystyk może być ukryta lub przybliżona.

#### 3.3 Styl jazdy
Styl określa:
- ryzyko,
- skłonność do zmian linii,
- reakcje na pogorszenie toru,
- zachowanie w walce.

Styl nie zastępuje statystyk.

####3.4 Interakcje między zawodnikami

Zawodnicy muszą uwzględniać obecność innych zawodników na torze.
System symuluje:

unikanie kolizji (hamowanie, korekta trajektorii),

lekkie kontakty (odbicia, strata rytmu),

mocne kontakty (wysokie ryzyko błędu lub upadek).

Skutki kontaktu zależą od:

prędkości względnej,

miejsca na torze (łuk / prosta),

aktualnej przyczepności,

umiejętności zawodników (panowanie w poślizgu, jazda parą, morale).

---

### 4. Morale
Morale:
- wpływa na stabilność i ryzyko błędów,
- zmienia się na podstawie wyników, presji i decyzji menedżera.

Morale ≠ forma fizyczna.

---

### 5. Rozwój zawodnika
- Rozwój zależy od wieku (junior szybciej rośnie, starszy szybciej spada).
- Kontuzje (zwłaszcza ciężkie) mogą dawać trwałe skutki.
- Cechy (np. profesjonalizm, sprawność) modyfikują tempo rozwoju i regresu.

---

### 6. Sprzęt i setup

#### 6.1 Założenie
Sprzęt nie daje przewagi finansowej.
Nie istnieje “szybszy motor za więcej pieniędzy”.

#### 6.2 Setup
- Manager sugeruje ustawienia (np. przełożenia).
- Zawodnik może:
  - zastosować,
  - zmodyfikować,
  - zignorować.

Decyzja zależy od charakteru i zaufania.

#### 6.3 “Sprzęt” jako umiejętność
Sprzęt zawodnika to w praktyce:
- umiejętność doboru ustawień,
- trafność diagnozy toru,
- jakość feedbacku po biegu
- umiejętności zawodnika (jego statystyki).

---

### 7. Informacja dla gracza
Gracz działa na niepełnej informacji:
- brak pełnych wartości liczbowych,
- obserwacja zachowania i wyników,
- komunikaty i logi.

Kod nie powinien zakładać idealnej wiedzy gracza o torze i zawodnikach.

---

### 8. Świadome ograniczenia
Na etapie core:
- brak pełnej fizyki motocykla,
- brak zaawansowanej ekonomii i regulaminów.

Priorytet: realistyczne konsekwencje decyzji i stabilny balans.

---

## Status / zakres (przykład)
- [ ] Symulacja toru: segmenty + 5 linii
- [ ] Ograniczenie łuku: minimalna linia dla prędkości (wynoszenie/hamowanie/błąd)
- [ ] Proste: pozycjonowanie + wyprzedzanie tylko z przewagi po wyjściu z łuku
- [ ] Zawodnicy: statystyki + styl + morale
- [ ] Setup: sugestie menedżera vs decyzje zawodnika
- [ ] Rozwój: wiek + kontuzje + cechy
