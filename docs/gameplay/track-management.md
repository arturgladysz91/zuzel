# Tor, pogoda i prace torowe

## Miejsce w planie — BINDING

Track Engine jest następnym dużym systemem po ukończeniu Race Engine. Najpierw powstaje model stanu i jego ewolucji, następnie integracja z przejazdami, a dopiero potem narzędzia menedżera.

## Trzy warstwy modelu — BINDING

### 1. Niezmienny profil toru

Opisuje cechy, które nie zmieniają się podczas meczu:

- geometrię segmentu: długość, typ, promień i docelowo — gdy pojawi się konsument — nachylenie,
- drenaż i ekspozycję na słońce oraz wiatr,
- bazowy skład i zachowanie nawierzchni,
- pojemność wodną, szybkość przesychania i podatność na koleiny,
- ograniczenia sprzętu oraz prac możliwych na obiekcie.

### Fundament statycznej geometrii — BINDING

Pierwszy krok przechowuje w `TrackGeometry` wyłącznie długość prostej, wewnętrzny promień referencyjny łuku, odstęp między pięcioma liniami referencyjnymi i kąt pojedynczego segmentu łuku w radianach. Nachylenie oraz kolejne parametry profilu pozostają poza tym fundamentem, dopóki nie istnieje ich konsument.

Przewidywane czasy jazdy liniami porównuje się diagnostycznie dla konkretnego toru. Wynik zależy od jego geometrii, lokalnej nawierzchni i całej trajektorii. Konkretna linia może być na danym torze uprzywilejowana; nie wymuszamy stałego procentu różnicy ani uniwersalnie najlepszej linii.

Ten fundament jest statycznym wejściem Race Engine. Nie dodaje dynamicznej nawierzchni, pogody, zużycia ani zarządzania torem.

### 2. Dynamiczny stan komórki

Stan jest przechowywany co najmniej dla segmentu i lokalnej linii. W pierwszej wersji model powinien rozdzielać:

- wilgotność powierzchni,
- ubicie,
- ilość luźnego materiału,
- koleiny i nierówności,
- temperaturę nawierzchni.

`EffectiveGrip` jest wielkością pochodną od profilu, stanu, prędkości i obciążenia. Nie powinien docelowo być drugą niezależną wartością, którą można zmienić bez zmiany przyczyny.

### 3. Warunki zewnętrzne

- temperatura powietrza,
- wilgotność względna,
- wiatr,
- nasłonecznienie lub zachmurzenie,
- opad,
- upływ czasu.

Aktualizacja musi używać rzeczywistego czasu domenowego, a nie wyłącznie numeru biegu.

## Wpływ przejazdu — BINDING

Race Engine odczytuje niezmienny snapshot toru i emituje `TrackLoadEvent`. Zdarzenie powinno opisywać co najmniej:

- segment i pozycję poprzeczną,
- czas oraz dystans kontaktu,
- obciążenie przejazdem,
- wheelspin lub intensywność uślizgu,
- incydent, jeśli powoduje nietypowe naruszenie nawierzchni.

Track Engine przetwarza komplet zdarzeń dopiero po zatwierdzeniu kroku jazdy. Kolejność zawodników w kolekcji nie może wpływać na wynik zużycia.

Przejazdy mogą ubijać linię, wyrzucać luźny materiał na zewnątrz, odsłaniać inną warstwę, pogłębiać koleiny i zmieniać lokalną wilgotność. Efekt nie jest równomiernym odjęciem jednej wartości na całym torze.

## Pogoda i temperatura — PROVISIONAL

- parowanie rośnie wraz z temperaturą powierzchni, wiatrem, nasłonecznieniem i niedosytem wilgotności powietrza,
- opad oraz polewanie dodają wodę lokalnie i chłodzą powierzchnię,
- temperatura toru reaguje z bezwładnością na powietrze, słońce, wodę i przerwy w jeździe,
- wpływ wilgotności na przyczepność jest nieliniowy: zarówno przesuszenie, jak i nadmiar wody mogą pogarszać warunki,
- różne nawierzchnie mają odmienne parametry tych samych równań.

Dokładne równania, jednostki, krok czasowy i zakresy zostaną zatwierdzone przed implementacją Track Engine v2.

## Prace torowe — BINDING

Podstawowe operacje domenowe:

- `Water` — dodaje określoną ilość wody do wskazanego obszaru,
- `Grade` — przemieszcza materiał, częściowo niweluje nierówności i może odsłonić inną wilgotność,
- `Pack` — zwiększa ubicie i stabilność, ale może przyspieszać tworzenie śliskiej, wypolerowanej linii,
- złożona konserwacja — kontrolowana sekwencja powyższych operacji.

Operacja ma obszar, intensywność, czas, wymagany sprzęt i skutki uboczne. Nie może bezpośrednio ustawiać docelowej przyczepności.

## Uprawnienia menedżera — BINDING

Wpływ gospodarza jest największy przed regulaminowym przekazaniem toru. Po przekazaniu i podczas zawodów gracz nie ma trybu „god mode”. Może wykonywać tylko działania dopuszczone przez `Ruleset` lub składać wnioski do osób oficjalnych.

Regulamin torów PZM 2026 wymaga określonego sposobu przygotowania i odbioru toru, a niestandardowe prace podczas zawodów mogą oznaczać przygotowanie nieregulaminowe. Dlatego zakres kontroli musi być wersjonowany w `Ruleset`, a nie zaszyty w UI.

## Informacja dla gracza — BINDING

Gracz nie widzi dokładnego stanu każdej komórki. Otrzymuje jakościową mapę warunków i poziom pewności zgodnie z `observations-and-feedback.md`. Pełne liczby są dostępne wyłącznie w narzędziach deweloperskich.

## Źródła regulaminowe

- [PZM — regulaminy żużlowe 2026](https://www.pzm.pl/zuzel/regulaminy-i-druki)
- [PZM — Regulamin Torów dla Zawodów Motocyklowych na Żużlu 2026](https://www.pzm.pl/pliki/zg/zuzel/2026/2026_regulamin-torow_20260302.pdf)

## Do ustalenia — TBD

- równania temperatury, parowania, przepływu i migracji wody,
- czy pierwsza wersja potrzebuje osobnej wilgotności głębszej warstwy,
- sposób transportu luźnego materiału między sąsiednimi liniami,
- zakresy fizyczne, jednostki i kalibracja różnych typów nawierzchni,
- katalog legalnych prac dla konkretnych lig i faz meczu.
