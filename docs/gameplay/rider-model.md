# Model zawodnika

**Status: BINDING**

Ten dokument opisuje docelowy model zawodnika dla warstwy gameplayowej Speedway Managera.

Nie jest zgodą na natychmiastową zmianę istniejącego `RiderSkills`, `RiderStyle` ani fizyki produkcyjnej. Obecny kod może nadal używać starszych nazw i uproszczeń do czasu osobnych, wąskich PR-ów implementacyjnych.

### Implementacja #56A — model kanoniczny i most kompatybilności

**BINDING, zakres #56A:** `RiderGameplayProfile` składa się z niezmiennych
`RiderAbilities`, `RiderPhysicalProfile` i `RiderInteractionStyle`.
`RiderState.Condition` jest osobnym stanem dynamicznym. Wszystkie te dane są
w #56A bez konsumentów zmieniających zachowanie biegu. Produkcja nadal czyta
oryginalne `RiderProfile.Skills` i `RiderProfile.Style`; ich skale i wartości pozostają bez zmian.

| Pole kanoniczne | Źródło fallbacku legacy | Status #56A | Przyszły właściciel / granica |
|---|---|---|---|
| Reaction — Reakcja | Start | int 1–99, bez konsumenta | oddzielna migracja startu: wyłącznie czas reakcji na taśmę |
| Start — Start | Start | int 1–99, bez konsumenta | oddzielna migracja startu: techniczne wykonanie po reakcji, bez bonusu do dalszej jazdy |
| Technique — Technika | SlideControl | int 1–99, bez konsumenta | #56B i późniejsze migracje kontroli / SlideControl / Adaptability osobno dla każdego subsystemu; balans, uślizg, gaz, precyzja, ratowanie, nie wybór ścieżki |
| TrackReading — Czytanie toru | TrackReading | int 1–99, bez konsumenta | percepcja nawierzchni; późniejsza migracja, bez obecnej zmiany szumu ani bonusu fizycznego grip/speed |
| Attack — Atak | neutralne 50 | int 1–99, bez konsumenta | #56B: jakość ofensywnego manewru, nie ryzyko ani częstotliwość walki |
| Defense — Obrona | neutralne 50 | int 1–99, bez konsumenta | #56B: jakość obrony, niezależna od Ataku |
| PairRiding — Jazda parą | PairRiding | int 1–99, bez konsumenta | późniejsza współpraca z partnerem; nigdy kontakt z rywalem |
| Strength — Siła | neutralne 50 | int 1–99, bez konsumenta | #56B: kontrola pod obciążeniem / w kontakcie; bez mocy silnika, Vmax ani traction bonus |
| MassKg — masa | LegacyCompatibilityMassKg = 70 kg | float, dodatnia skończona liczba kg; bez konsumenta | #56B: fizyczny parametr; obecna nominalna masa układu 142 kg nadal bez zmian |
| Condition — Kondycja | nowy stan = 1 | float 0–1; bez konsumenta | #56B: stan przy odpowiedzi na walkę; później obciążenie i odbudowa w osobnym etapie |
| Combativeness — Waleczność | neutralne 0.5 | float 0–1, skończony; bez konsumenta | #56B: chęć walki, niezależna od jakości Ataku; więcej nie zawsze znaczy lepiej |

`RiderAbilities.Balanced` ma dokładnie osiem wartości 50. Nie posiada Speed ani
uniwersalnego Overall. Typ int wyklucza NaN / infinity. Konstruktor odrzuca 0 i 100;
1 i 99 są poprawnymi końcami skali. Nie dodano nieużywanego frameworka normalizacji.

`RiderAbilityCompatibility` to jednokierunkowy **punkt wyjścia migracji, nie
empiryczna równoważność**. Mapowane wartości float zaokrąglamy do najbliższej liczby
całkowitej (`MidpointRounding.AwayFromZero`), następnie clampujemy do 1–99:
0→1, 20→20, 49.5→50, 50→50, 50.5→51, 80→80, 100→99.
Mapowany NaN / infinity jest odrzucany przez adapter; walidacja starych klas nie zmienia się.

Legacy Speed i Adaptability pozostają **bez mapowania**. Nie produkują Techniki,
Startu, Siły, Ataku ani Obrony. Speed można usunąć dopiero po zastąpieniu wszystkich
konsumentów longitudinal odpowiednimi modelami sprzętu, setupu i fizyki.
Legacy PairRiding pozostaje w obecnym kontakcie tylko dla kompatybilności silnika;
kanoniczne PairRiding ma wyłącznie przyszłą odpowiedzialność współpracy z partnerem.

`RiderInteractionStyle` przechowuje Combativeness, `PreferredLine` (Inside / Neutral /
Outside) i SetupIndependence. RiskTolerance i LaneChangeTendency pozostają bez mapowania;
RiskTolerance nie wyznacza Combativeness ani Attack. Fallback Combativeness zawsze
wynosi 0.5. Legacy OutsidePreference < `1f/3f` daje Inside, > `2f/3f` daje Outside;
oba progi i przedział między nimi dają Neutral. SetupIndependence jest kopiowane
dokładnie. Preferowana linia nie daje bonusu speed. Nowy styl odrzuca nieskończone
wartości i wartości spoza 0–1.

Masa w nowym profilu jest obowiązkowa, dodatnia i skończona, bez arbitralnego wąskiego
przedziału. `LegacyCompatibilityMassKg = 70f` jest **niekalibrowanym fixturem
kompatybilności**, tylko dla starych/default profili; nie zmienia fizyki.
Condition clampuje skończone wartości tak jak Morale, odrzuca NaN / infinity;
`ApplyConditionDelta` używa tej samej granicy. Oba `ResetForHeat` zachowują np. 0.72.
Snapshot i jego mutable copy zachowują Condition bez resetu. #56A nie implementuje
zmęczenia, regeneracji, dodatkowych pasków kondycji ani osobowości.

#### API, równość i serializacja

Stary konstruktor `RiderProfile(Id, Name, Skills, Style)`, deconstruction, `with`
i `CreateDefault` pozostają dostępne. `Gameplay` wylicza fallback ze aktualnych
Skills/Style, więc `with { Skills = ... }` na starym profilu nie zatrzymuje starych
wartości kanonicznych. Jawny profil używa `CreateCanonical` i zachowuje przekazany
niezmienny obiekt Gameplay, także po zmianie payloadu legacy przez `with`:

```csharp
var gameplay = new RiderGameplayProfile(
    new RiderAbilities(91, 84, 88, 76, 93, 67, 55, 72),
    new RiderPhysicalProfile(68f),
    new RiderInteractionStyle(.82f, PreferredLine.Inside, .60f));
var rider = RiderProfile.CreateCanonical(7, "Modern", gameplay,
    legacySkills: new RiderSkills(80, 95, 70, 60, 40, 20),
    legacyStyle: new RiderStyle(.1f, .2f, .3f, .4f));
```

Nowy API wymaga **jawnego payloadu legacy**; nie wylicza Speed ani innych starych
skills z danych kanonicznych. `HasExplicitGameplay` rozróżnia oba źródła.
Równość starych rekordów pozostaje taka sama. Jawne dane kanoniczne są częścią
równości nowego rekordu: dwa jawne profile z innym Atakiem nie są równe.

`Gameplay`, `HasExplicitGameplay` oraz Condition w state/snapshot mają `[JsonIgnore]`,
aby zachować historyczny JSON i evidence. Stary JSON nadal deserializuje się do
starego profilu. Ten historyczny format nie zapisuje jawnego Gameplay ani Condition;
nie należy używać go jako nowego formatu save game. Gameplay samodzielnie ma typed
JSON round-trip; przyszła wersja persistence musi jawnie zapisać canonical data
i stan, a odtworzenie modern profilu użyć `CreateCanonical` z legacy payloadem.

#### Audyt zależności i dowód zgodności

[`rider-legacy-consumers.json`](rider-legacy-consumers.json) zamraża hash pełnej treści
39 plików używających lub przekazujących Skills/Style z main
`ffa7196027f031f947c844da04f5706cb2655f41` (normalizacja wyłącznie CRLF→LF).
`python tools/rider-compatibility-audit/check-consumers.py` sprawdza kompletny zbiór,
brak nowych konsumentów canonical oraz identyczne pliki. Jedyny wyjątek to dokładna
mechaniczna kopia `Condition = rider.Condition` w CaptureSnapshot; reszta
SimulationEngine musi mieć ten sam hash co main.

| Kategoria | Obecni konsumenci / legacy wejścia |
|---|---|
| Start i longitudinal | SimulationEngine, LongitudinalDynamics: Start; Speed nadal napędza obecne longitudinal primitives |
| Ograniczenia łuku i execution | SegmentPhysics, ContinuousCornerEnvelope, ExecutedPathTraversal: Speed / SlideControl; LateralMovementModel i korekty: SlideControl / Adaptability |
| Percepcja i decyzje | AdaptiveDecisionModel: TrackReading, RiskTolerance, LaneChangeTendency, OutsidePreference |
| Legacy kontakty | SimulationEngine: SlideControl, PairRiding; bez kanonicznych Atak / Obrona / Siła |
| Setup | SetupResolver: SetupIndependence, TrackReading |
| Diagnostyka i fixtures | Analysis i Sandbox nadal przekazują / raportują oryginalne sześć skills i cztery style |

`RiderGameplayProfileTests` porównuje dokładne bajty pełnych czterookrążeniowych
heatów (Adaptive i convergence z czterema legacy kontaktami) z capture wykonanym
na **niezmienionym main**. Chronione są classification, czas, pozycja, speed,
morale, wszystkie logi i surface changes oraz końcowy raw rider/surface state.
Jawny modern przykład i niezależna zmiana każdego canonical field / masy / stylu /
Condition dają te same bajty. Osobny [artifact #56A](../calibration/rider-abilities-compatibility-evidence.json)
nie zastępuje historycznych artifactów #54/#55. CI porównuje nowe captures byte-for-byte
na Windows/Ubuntu, obok niezmienionych istniejących audytów.

**Granica kolejnego etapu:** #56B wykorzysta Attack, Defense, Technique, Strength,
Combativeness, MassKg i Condition do odpowiedzi na contested physical space z #55.
Ewentualne przyszłe Opanowanie pozostaje poza #56A. Osobna migracja standing startu
rozdzieli Reaction timing i Start technical launch; nie wolno zmieniać obu przez
przypadkową podmianę obecnego StartNorm. Nie dodano impulsów kontaktowych, ataku,
obrony, zmian geometrycznych, RNG, morale, wear ani jakiegokolwiek strojenia biegu.

## Zasada nadrzędna

Zawodnik nie jest głównym źródłem prędkości motocykla. Tempo biegu ma wynikać przede wszystkim z fizycznych możliwości motocykla, silnika, setupu, lokalnych warunków toru i wybranej trajektorii, a umiejętności zawodnika określają jakość decyzji i wykonania.

Kolejność rozstrzygania:

**warunki → percepcja → decyzja → wykonanie → fizyka → wynik**

Żadna pojedyncza statystyka nie powinna jednocześnie wybierać decyzji, poprawiać wykonania, zwiększać fizycznego limitu i ograniczać konsekwencji błędu.

Model zawodnika ma pozostać oszczędny. Nowy atrybut powstaje tylko wtedy, gdy opisuje odrębne zjawisko, którego nie można sensownie wyprowadzić z istniejących parametrów, stanu zawodnika, sprzętu albo sytuacji wyścigowej.

## 1. Dane podstawowe

- imię i nazwisko — tekst,
- data urodzenia — data,
- wiek — wyliczany,
- narodowość — kategoria,
- wzrost — cm, informacyjnie w v1,
- **masa — kg, używana przez fizykę**,
- status regulaminowy — np. junior / U24 / senior.

Masa nie jest umiejętnością i nie jest przeliczana na rating 1–99. Jej wpływ powinien wynikać z fizyki całego układu zawodnik–motocykl.

## 2. Główne umiejętności sportowe — 1–99

Gracz widzi osiem podstawowych umiejętności. Nie tworzymy dodatkowych jawnych ratingów opisujących podfazę tej samej czynności.

| Cecha | Odpowiedzialność |
|---|---|
| **Reakcja** | moment reakcji na zwolnienie taśmy |
| **Start** | przygotowanie i techniczne wykonanie startu poza samą reakcją: sprzęgło, gaz, wykorzystanie przyczepności i pierwsze metry |
| **Technika** | prowadzenie motocykla, geometria przejazdu, balans, kontrola uślizgu, gazu i korekt |
| **Czytanie toru** | rozpoznawanie przyczepności, kolein, zmian nawierzchni i działających ścieżek |
| **Atak** | jakość wykonania manewru ofensywnego przeciw rywalowi |
| **Obrona** | jakość utrzymania pozycji i reakcji na atak |
| **Jazda parą** | jakość współpracy z partnerem podczas biegu |
| **Siła** | fizyczna kontrola motocykla w wymagających sytuacjach |

### Granice

- **Reakcja** odpowiada tylko za czas reakcji na taśmę.
- **Start** nie jest bonusem do dalszej jazdy.
- **Technika** nie wybiera najlepszej ścieżki; odpowiada za wykonanie.
- **Czytanie toru** nie zwiększa fizycznej prędkości; poprawia percepcję i decyzje.
- **Atak** i **Obrona** pozostają osobnymi umiejętnościami.
- **Jazda parą** opisuje wykonanie współpracy, a nie chęć pomocy drużynie.
- **Siła** nie zwiększa mocy silnika ani Vmax.

Nie istnieje osobna umiejętność **Szybkość**. Prędkość jest wynikiem symulacji.

## 3. Fizyczność i bieżąca Kondycja

Nie przechowujemy osobnych atrybutów `Wytrzymałość` ani `Regeneracja`.

Fizyczna część profilu składa się z:

- **Siły** — jednej z ośmiu głównych umiejętności,
- **masy** — rzeczywiste kg,
- **Kondycji** — dynamicznego stanu,
- **zdrowia i kontuzji** — konkretnych stanów medycznych.

### Kondycja — BINDING

Kondycja jest jednym wspólnym stanem opisującym zarówno obciążenie z ostatnich dni, jak i zmęczenie narastające w aktualnych zawodach.

Zasady:

- zawodnik może rozpocząć zawody z obniżoną Kondycją po intensywnym kalendarzu,
- Kondycja spada po każdym biegu,
- spadek zależy od rzeczywistego obciążenia biegu,
- ciężki bieg, trudny tor, intensywna walka lub upadek mogą zwiększyć koszt,
- podczas przerw między biegami następuje tylko niewielka regeneracja,
- główna odbudowa następuje w czasie bez zawodów,
- jazda dzień po dniu może powodować rozpoczęcie kolejnych zawodów bez pełnej Kondycji,
- kontuzja lub jazda mimo niepełnej sprawności może pogarszać startową Kondycję albo tempo jej odbudowy.

Kondycja jest **stanem**, nie talentem i nie ma własnego potencjału rozwojowego.

Sposób prezentacji w UI — opis, procent albo inny czytelny wskaźnik — pozostaje decyzją interfejsu. Gracz ma widzieć jedną Kondycję, a nie osobne paski świeżości, zmęczenia, wytrzymałości i regeneracji.

## 4. Styl zawodnika

Styl nie jest zestawem dodatkowych umiejętności sportowych i nie powinien być prezentowany jako kolejny blok ratingów porównywanych z podstawową ósemką.

- **Waleczność** — jak chętnie zawodnik podejmuje bezpośrednią walkę; nie zwiększa jakości Ataku.
- **Preferowana linia** — kategoria: wewnętrzna / neutralna / szeroka; nie daje bonusu do prędkości.
- **Niezależność** — jak mocno zawodnik obstaje przy własnym zdaniu dotyczącym setupu.

Wysoka wartość stylu nie zawsze oznacza „lepiej”.

## 5. Osobowość

Osobowość działa głównie pod spodem. Nie jest częścią ośmiu widocznych umiejętności sportowych.

Wewnętrznie mogą istnieć:

- **Profesjonalizm** — podejście do treningu, odpoczynku i prowadzenia kariery,
- **Ambicja** — poziom oczekiwań sportowych i statusowych,
- **Zespołowość** — gotowość działania dla dobra drużyny,
- **Chęć nauki** — wykorzystanie treningu, uwag i mentoringu,
- **Opanowanie** — siła i długość reakcji emocjonalnych na wydarzenia,
- **Mentoring** — zdolność przekazywania własnej wiedzy innym,
- **Ugodowość** — skłonność do kompromisu i akceptowania decyzji niezgodnych z własnym interesem.

UI nie musi ujawniać ich jako dokładnych liczb. Powinny być poznawane przede wszystkim przez zachowanie zawodnika, raporty i wydarzenia.

Nie przechowujemy osobnych sportowych ratingów `Jazda pod presją`, `Determinacja` i `Koncentracja`. Ich skutki wynikają z kontekstu, doświadczenia, osobowości, morale i Kondycji.

## 6. Preferencje torowe

Preferencje są kategoryczne, bez skali 1–99.

Zawodnik może preferować albo nie lubić:

- twardej nawierzchni,
- przyczepnej nawierzchni,
- mokrego toru,
- luźnej nawierzchni,
- nierównego / pokoleinowanego toru,
- technicznych torów,
- szybkich torów.

Nie każdy zawodnik musi posiadać preferencję.

Tor sam posiada ciągłe właściwości fizyczne, np. twardość, wilgotność, grip, luźny materiał i nierówności. System ocenia dopasowanie aktualnego stanu do preferencji zawodnika.

Preferencja daje niewielki efekt komfortu, stabilności wykonania i łatwości znalezienia ustawień; nie daje bezpośredniego bonusu do prędkości.

Budowa składu pod charakter własnego toru jest zamierzoną strategią. Nadmierna specjalizacja ma koszt na wyjazdach i przy zmianie warunków.

## 7. Doświadczenie i znajomość

### Doświadczenie ogólne — BINDING

Doświadczenie nie jest widoczną statystyką ani ratingiem 1–99.

Jest ukrytym modyfikatorem wynikającym **przede wszystkim z wieku**. Nie tworzymy osobnego rozwijanego paska „Experience”.

Doświadczenie może wpływać m.in. na:

- stabilność decyzji w nietypowych sytuacjach,
- interpretację zachowania motocykla,
- jakość opisu problemu po biegu,
- wykorzystanie wcześniejszych doświadczeń przy zmianach toru,
- ograniczenie błędnych ocen pod presją.

Doświadczenie nie daje bezpośredniego bonusu do mocy, Vmax, Startu, Ataku, Obrony ani Techniki.

### Znajomość

Znajomość konkretnego toru albo sprzętu jest osobną wiedzą kontekstową wynikającą z faktycznego kontaktu z nimi, a nie częścią ogólnego doświadczenia.

Może obejmować:

- znajomość geometrii toru,
- znajomość typowego zachowania jego nawierzchni,
- krótkoterminową wiedzę o aktualnym stanie toru,
- znajomość konkretnego motocykla / silnika.

Pogoda może zniszczyć wiedzę o aktualnym stanie nawierzchni, ale nie usuwa znajomości geometrii.

Znajomość nie musi być pokazywana graczowi jako dokładna liczba.

## 8. Stan bieżący

Stany nie są trwałymi umiejętnościami zawodnika.

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

Siłę reakcji mogą modyfikować Ambicja, Ugodowość, Zespołowość, Opanowanie i Profesjonalizm.

Morale nie daje prostego bonusu do prędkości.

### Zdrowie

- zdrowy,
- poobijany / z drobnym ograniczeniem,
- kontuzjowany,
- medycznie dopuszczony, ale nie w pełni sprawny.

Szczegóły wynikają z konkretnego urazu, nie z ogólnego ratingu zdrowia.

### Kondycja

Kondycja jest jedynym ogólnym stanem fizycznego obciążenia i działa zgodnie z sekcją 3.

Nie istnieją osobne stany `Świeżość`, `Zmęczenie` ani `Kondycja meczowa`.

## 9. Forma wynikowa i sprzętowa dyspozycja — BINDING

Zawodnik **nie posiada ukrytej Formy okresowej ani losowej Dyspozycji dnia**, które dodawałyby lub odejmowały procent jego umiejętności.

Słowo **forma** może być pokazywane graczowi wyłącznie jako opis ostatnich wyników, np. na podstawie kilku ostatnich spotkań. Taki wskaźnik jest obserwacją skutków i nie jest osobnym źródłem przewagi w symulacji.

Średnioterminowe okresy, w których ten sam zawodnik przez kilka tygodni, kilka miesięcy albo większość sezonu wygląda wyraźnie szybciej lub wolniej, mogą wynikać m.in. z:

- sprzętowej dyspozycji aktualnego pakietu silnikowego,
- dopasowania setupu,
- warunków i charakteru torów,
- Kondycji i zdrowia,
- rzeczywistych decyzji i zdarzeń wyścigowych.

### Sprzętowa dyspozycja

Sprzętowa dyspozycja należy do modelu motocykla, nie do umiejętności zawodnika. Jest ukrytym, wolnozmiennym modyfikatorem fizycznych możliwości aktualnego pakietu silnikowego.

Nie zmienia ratingów Reakcji, Startu, Techniki, Czytania toru, Ataku, Obrony, Jazdy parą ani Siły.

Docelowy kierunek balansu:

- początek sezonu: typowo około **-3%..+3%**, wyjątkowo około **-5%..+5%**,
- środek sezonu: najczęściej około **-2%..+2%**, sporadycznie nadal około **3–4%** odchylenia,
- końcówka sezonu: zwykle około **-2%..+2%**,
- system nie wymusza powrotu do zera; dobry albo słaby okres sprzętowy może utrzymywać się długo,
- duże skoki po rozpoczęciu sezonu są rzadkie,
- wartość nie jest losowana od nowa przed każdym meczem.

Dokładne rozkłady, częstotliwość zmian i wpływ na parametry fizyczne są **PROVISIONAL** i mają zostać skalibrowane testami symulacji.

## 10. Relacje

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

## 11. Rola w drużynie

Kategorie:

- **Lider**,
- **Podstawowy**,
- **Uzupełnienie składu**,
- **Rezerwowy**.

Oczekiwania dotyczące liczby i znaczenia biegów wynikają z roli. Nie istnieje osobny parametr „oczekiwane biegi”.

Reakcja zawodnika na zmianę zależy od kontekstu meczu, roli, wyników, Ugodowości, Ambicji, Zespołowości i zaufania do managera.

## 12. Kontrakt

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

## 13. Rozwój i potencjał

Każda z ośmiu rozwijalnych umiejętności może posiadać ukryte:

- aktualny poziom,
- potencjał życiowy,
- aktualnie osiągalny potencjał,
- tempo uczenia.

Nie ma jednego ogólnego „Potential 92”.

Największy wpływ na rozwój mają prawdziwe zawody. Rozwija się przede wszystkim to, czego zawodnik rzeczywiście używa i doświadcza.

Zawodnik może mieć indywidualne:

- krzywe rozwoju technicznego, wyścigowego i fizycznego,
- okresy szybszego i wolniejszego rozwoju,
- różne przedziały szczytu dla różnych grup zdolności,
- tempo starzenia i regresu.

Rozwój po 30. roku życia jest możliwy, szczególnie w cechach technicznych i taktycznych, ale słaby zawodnik nie powinien nagle stać się mistrzem tylko dzięki późnemu skokowi.

Ogólne doświadczenie z sekcji 7 nie ma własnego potencjału i nie jest trenowaną umiejętnością.

## 14. Obciążenie kariery

Nie przechowujemy osobnego widocznego atrybutu „zużycie zawodnika”.

Historia liczby zawodów, biegów, kontuzji i jazdy dzień po dniu może być wykorzystywana jako wejście do:

- aktualnej Kondycji,
- skutków urazów,
- długoterminowego regresu fizycznego.

Jeżeli później potrzebny będzie dodatkowy ukryty model długoterminowego obciążenia, musi on mieć konkretny konsument i nie może dublować Kondycji.

## 15. Kontuzje i powrót

Każdy uraz może mieć:

- rodzaj,
- lokalizację,
- ciężkość,
- stan leczenia,
- przewidywany czas powrotu,
- ryzyko nawrotu,
- konkretne ograniczenia.

Fazy:

1. niezdolny do jazdy,
2. medycznie zdolny, ale z ograniczeniami albo obniżoną Kondycją,
3. pełna sprawność.

Manager może zdecydować o wystawieniu zawodnika nie w pełni odbudowanego, jeśli jest dopuszczony do jazdy. Powrót zbyt wcześnie może pogorszyć Kondycję, zwiększyć ryzyko nawrotu albo wydłużyć odbudowę.

Nie istnieje osobna umiejętność Regeneracja. Tempo odbudowy wynika z czasu, wieku, obciążenia, urazu i pozostałych właściwych stanów systemu.

## 16. Mentoring

Dobry zawodnik nie musi być dobrym mentorem.

Efekt mentoringu zależy od:

- wiedzy mentora w danym obszarze,
- jego Mentoringu,
- ukrytego doświadczenia,
- Chęci nauki ucznia,
- relacji mentor–uczeń,
- wspólnej pracy.

Mentoring pomaga przekształcać doświadczenie w rozwój; nie daje bezpośrednio punktów umiejętności.

## 17. Trening

Manager ustala osobny kierunek treningu każdemu zawodnikowi każdego dnia.

Główne kierunki:

- Starty,
- Technika,
- Walka / sytuacje wyścigowe,
- Jazda parą,
- Siła / przygotowanie fizyczne,
- Odnowa / lekki dzień,
- Odpoczynek,
- Rehabilitacja,
- Przygotowanie do meczu.

Zwykłe treningi są symulowane.

**Przygotowanie do meczu** może być interaktywne: ładowany jest rzeczywisty tor, manager obserwuje przejazdy, poznaje aktualne warunki, testuje setup i zbiera feedback.

Zwykły trening na konkretnym torze również zwiększa znajomość jego geometrii i charakterystyki, ale nie daje managerowi takiej samej bezpośredniej wiedzy jak interaktywne przygotowanie.

## 18. Sprzęt, mechanik i feedback

Zasada:

- **zawodnik mówi, co czuje**,
- **mechanik mówi, co jego zdaniem należy zmienić**,
- **manager podejmuje ostateczną decyzję**.

Jeden objaw może mieć kilka przyczyn jednocześnie. Zawodnik, mechanik i manager mogą widzieć różne części problemu.

Nie istnieje osobna umiejętność `Feedback techniczny`.

Jakość informacji zawodnika wynika przede wszystkim z:

- Czytania toru,
- Techniki,
- ukrytego doświadczenia,
- znajomości toru i konkretnego sprzętu,
- aktualnej Kondycji,
- sprzętowej dyspozycji i znajomości konkretnego motocykla,
- rzeczywistych zdarzeń z biegu.

Mechanik posiada własne kompetencje dotyczące diagnozy, ustawienia i przygotowania sprzętu.

Manager może ustawić setup inaczej niż proponuje mechanik.

Trafne decyzje managera mogą stopniowo zwiększać zaufanie zawodnika i mechanika. Nietrafne mogą je obniżać. Im niższe zaufanie, tym większa skłonność do obstawania przy własnej opinii.

Ocena decyzji setupowej opiera się na tym, czy rozwiązano konkretny problem, a nie tylko na wyniku biegu.

## 19. Czego celowo NIE przechowujemy jako osobnych cech

Nie ma osobnych statystyk sportowych:

- Szybkość,
- Przygotowanie pola,
- Wyjście spod taśmy,
- Technika łuku,
- Panowanie nad motocyklem,
- Balans,
- Zmysł wyścigowy,
- Wytrzymałość,
- Regeneracja,
- Jazda pod presją,
- Determinacja,
- Koncentracja,
- Feedback techniczny,
- prędkość w łuku,
- wyjście z łuku,
- dojazd do pierwszego łuku,
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

Są to efekty wynikające z ośmiu głównych umiejętności, stylu, osobowości, doświadczenia, Kondycji, relacji, sprzętu albo sytuacji wyścigowej.

## 20. Tabela priorytetów wpływu

**P1 — kluczowy**, **P2 — ważny**, **P3 — sytuacyjny / kontekstowy**.

| Sytuacja | P1 | P2 | P3 / kontekst |
|---|---|---|---|
| Przygotowanie pola startowego | Start | Czytanie toru | doświadczenie, znajomość toru |
| Reakcja na taśmę | Reakcja | Start | Kondycja, doświadczenie |
| Wyjście spod taśmy | Start | Technika | masa, Siła, setup, przyczepność |
| Dojazd do pierwszego łuku | wynik startu + fizyka | Atak / Obrona | Technika, sytuacja rywali |
| Pierwszy łuk | Technika | Atak / Obrona | Czytanie toru, doświadczenie |
| Normalny przejazd łuku | Technika | Czytanie toru | Kondycja, setup, tor |
| Wyjście z łuku | Technika | Siła | masa, setup, lokalna przyczepność |
| Wybór ścieżki | Czytanie toru | doświadczenie | preferowana linia, pozycja rywali |
| Atak | Atak | Technika | Czytanie toru, Waleczność |
| Obrona | Obrona | Technika | Czytanie toru, doświadczenie |
| Przycinka / zwód | Atak | Technika | Czytanie toru, doświadczenie |
| Jazda parą | Jazda parą | Czytanie toru | Zespołowość, relacja z partnerem |
| Nagła koleina | Technika | Siła | Czytanie toru, doświadczenie, Kondycja |
| Kontakt z rywalem | Siła | Technika | masa, Kondycja |
| Jazda pod dużą presją | doświadczenie + Opanowanie | morale | Kondycja, sytuacja meczu |
| Walka po przegranym starcie | Atak | Technika | Ambicja, Waleczność, morale |
| Kolejny ciężki bieg | Kondycja | czas od poprzedniego biegu | zdrowie, obciążenie wcześniejszych biegów |
| Odbudowa między biegami i dniami | czas + aktualna Kondycja | wiek | urazy, obciążenie kalendarza |
| Diagnoza nawierzchni | Czytanie toru | doświadczenie | znajomość toru |
| Opis zachowania motocykla | Technika | doświadczenie | znajomość sprzętu, Kondycja |
| Akceptacja setupu managera | Zaufanie do managera | Niezależność | Zaufanie do mechanika, Ugodowość |
| Rozwój po zawodach | używane umiejętności + potencjał | Chęć nauki | mentor, Profesjonalizm |
| Rozwój treningowy | potencjał + trening | Chęć nauki, Profesjonalizm | sztab, wiek |
| Reakcja na odsunięcie od składu | rola + Ugodowość | Ambicja, Zaufanie do managera | Zespołowość, Opanowanie |
| Zmiana morale | wydarzenie + aktualne morale | Opanowanie | Ambicja, Ugodowość, Zespołowość |

## 21. Tabela granic odpowiedzialności

| Parametr | Odpowiada za | Nie powinien odpowiadać za |
|---|---|---|
| Reakcja | moment reakcji na taśmę | dalsze przyspieszenie i jazdę |
| Start | techniczne wykonanie startu i pierwszych metrów | dalszą jazdę po torze |
| Technika | jakość fizycznego wykonania jazdy i korekt | wybór najlepszej ścieżki |
| Czytanie toru | percepcję nawierzchni i działających linii | fizyczne wykonanie manewru |
| Atak | wykonanie ofensywnego manewru | częstotliwość podejmowania ryzyka |
| Obrona | wykonanie obrony pozycji | ogólną technikę jazdy |
| Jazda parą | wykonanie współpracy | chęć działania dla zespołu |
| Siła | kontrolę fizyczną w wymagających sytuacjach | moc i prędkość motocykla |
| Masa | fizykę układu zawodnik–motocykl | poziom sportowy zawodnika |
| Kondycja | aktualne fizyczne obciążenie | talent i trwałą jakość zawodnika |
| Doświadczenie | stabilność ocen i wykorzystanie przeżyć | bezpośredni bonus do prędkości |
| Waleczność | skłonność do podejmowania walki | skuteczność manewru |
| Preferowana linia | naturalną skłonność wyboru linii | bonus do prędkości |
| Niezależność | obstawanie przy swoim setupie | wiedzę techniczną |
| Profesjonalizm | podejście do kariery | talent i potencjał |
| Ambicja | oczekiwania zawodnika | umiejętności sportowe |
| Zespołowość | gotowość działania dla zespołu | umiejętność Jazdy parą |
| Chęć nauki | wykorzystanie okazji rozwojowych | wysokość potencjału |
| Opanowanie | reakcje emocjonalne | bazową jakość jazdy |
| Mentoring | przekazywanie wiedzy | własny poziom sportowy |
| Ugodowość | gotowość do kompromisu | Zespołowość |

## 22. Priorytet implementacyjny

### P1 — pojedynczy bieg / Race Engine

- Reakcja
- Start
- Technika
- Czytanie toru
- Atak
- Obrona
- Siła
- masa

### P2 — pełny mecz

- Jazda parą
- Kondycja
- zdrowie / ograniczenia urazowe
- integracja wyboru motocykla, setupu i sprzętowej dyspozycji

### P3 — warstwa managera i setupu

- Niezależność
- Zaufanie do managera
- Zaufanie do mechanika
- ukryte doświadczenie
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
- kontuzje
- kontrakty
- role
- relacje

## 23. Zasada implementacyjna

Nowy parametr może zostać dodany tylko wtedy, gdy da się jednoznacznie odpowiedzieć:

1. jaką decyzję lub zjawisko opisuje,
2. czego nie opisuje,
3. w której warstwie działa: percepcja / decyzja / wykonanie / fizyka / feedback / rozwój,
4. czy nie dubluje istniejącej cechy.

Jeśli dwie cechy wpływają na to samo w ten sam sposób, należy je scalić albo jedną usunąć.

W szczególności nie należy ponownie rozbijać Techniki na kilka ratingów ani Kondycji na osobne ratingi Wytrzymałości i Regeneracji bez nowej, zatwierdzonej decyzji projektowej.
