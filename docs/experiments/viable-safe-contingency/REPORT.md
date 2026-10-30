# Badanie #154 — pełny legalny S i koszt planowania

**Werdykt: PARTIAL. Nie ukończono trzech okrążeń S. Nie osiągnięto celu 100–140 CPU-s i nie ma podstaw do skalowania tego sterownika na czterech zawodników.**

Dokładna baza: `de500229a6fdb5c57da7cbeb90f5484c28d57130` (HEAD Draft PR152). Nowa gałąź `experiment/viable-safe-contingency-154`, Draft PR153, bez merge. Wszystkie nowe funkcje pozostają domyślnie OFF. Fizyka A/B, opony, silnik, wymiary, geometria i wszystkie wcześniejsze wyniki są zamrożone.

## Walidacja i zakres

| Kontrola | Wynik |
|---|---|
| Windows CI końcowego HEAD | Dokładny SHA i końcowy PASS są publikowane w [opisie PR153](https://github.com/arturgladysz91/zuzel/pull/153) i dostarczonym validation154-final.json; wcześniejszy run nie zastępuje tej bramy. |
| Restore / build | Release warnaserror PASS; locked restore PASS; 0 warnings |
| C# core + trajectory | 2532 PASS |
| Python | 338 calibration + 7 CI-policy PASS |
| Node | 99 PASS |
| Discovery | 3053; bez luk/duplikatów; core2137, trajectory395, historical473, four-rider48 |
| Nieuruchomione | historical-analysis i four-rider tylko discovery według niezmienionej polityki Draft; brak nowego natywnego B |
| Historia i produkcja | 58 całych historycznych katalogów, w tym153, oraz zamrożone bloby src/152/153: niezmienione; source-proof.json |


## 1. Skąd 6329 K1

Przeanalizowano wszystkie **69 przyjętych decyzji oraz nieprzyjętą próbę 13,8 s**. Pełnych K1: 6329; przyjęty prefiks zużył6006, ostatnia niedokończona decyzja323. To średnio87,04 K1 na przyjętą decyzję bez ostatniej odmowy, albo91,72 po doliczeniu jej kosztu.

K1 generuje po0,9 s wszystkie oryginalne akcje (siatka bazowa27, z dodatkowymi aktualnymi kątami). Wynik służy tylko jako propozycja następnego0,2 s. Pełna rezerwa musi natomiast dojść przez kolejny łuk do dalszego przekroju, nawet350,779 m od startu, w ograniczeniu130 kroków/26 s. Gdy sprzężenie do zachowanej taśmy nie przechodzi, Complete próbuje zachowanych komend, innych rezerw, świeżego ogona i napraw. Gdy zawiedzie przyszła obserwacja, powtarza to w jej odgałęzieniu i przy alternatywnym gazie. Nie jest to6329 różnych decyzji zawodnika.

| Faza153 | CPU-s |
|---|---|
| full_k1 | 247.953 |
| conditional_continuations | 46.984 |
| independent_preflight | 18.484 |
| outside_decision | 6.891 |
| recertification | 3.016 |
| candidate_generation | 0.031 |
| nominal_propagation | 0.219 |
| uncertainty_scenarios | 0.000 |


K1:247,953 CPU-s; kontynuacje:46,984; niezależny preflight:18,484. Główny koszt pochodzi z tworzenia propozycji odległych ogonów. Usunięcie niezależnego sprawdzenia nie rozwiązałoby tego problemu i nie jest dozwolone. Cache już współdzieli nieodwołalne prefiksy; historia ma51018 trafień wyszukiwania i675 niezależnych.

## 2. Odrzucenia i pierwsza utrata przyszłej wykonalności

W4708 zapisanych ocenach pełnych rolloutów było1578 legalnych. Według wyniku **całej** prognozy: 935 odrzuceń przez zapas pozy poniżej0,5 m i 2017 przez sweep;177 przez beta oraz1 przez odrzuconą prognozę fizyczną. Licznik2938 opisuje inny przekrój: **pierwszy** zły interwał był jeszcze sweep-only. Późniejsze punkty tej samej prognozy mogą naruszać0,5 m. Nie nazywamy tych2938 przypadków wyłącznie nadmierną konserwatywnością.

Pierwszy niedopuszczalny interwał zapisany w końcowej próbie13,8 s leży w prognozie14,82 s: obrys0,590505 m, dolna granica sweep0,489416 m, reference227,418 m. Ta prognoza nie została wykonana w natywnym świecie. W ostatniej próbie:77 odrzuceń całego rolloutu przez pozycję,33 przez sweep,1 przez beta i10 legalnych rolloutów. Powstało tylko9/13 węzłów i6/9 rezerw, po czym skończyła się globalna rezerwacja CPU. **To odmowa z powodu budżetu, nie dowód niewykonalności stanu.**

Ścisły lokalny K1 jest niezgodny z celem długiego planera w sensie jakości propozycji, nie jednostek marginesu: jego filtr faktycznie używa0,5 m i sweep. Lokalna legalność0,9 s nie przewiduje jednak naprawialności następnego łuku. W nowej reprodukcji jest konkretny przykład przy hipotetycznej obserwacji14,2 s: lokalny wybrany kandydat ma sweep0,877070 m, a dalszy rollout tych początkowych kontroli dochodzi do zapasu obrysu−0,102598 m przy19 s. To zapis prognozy, nie rzeczywistego kontaktu.

### Wszystkie decyzje153

Pełne lokalne wyniki poszczególnych kandydatów H09 nie były zapisane w153: historyczna kolumna jest N/D, a nie zero. Nowy S0 zapisuje każdy taki kandydat. `decisions153.csv` zawiera każdą decyzję, koszty, początki odmów i profile; `decision-costs154.csv` oraz `local-candidates154.csv` rozliczają nowe warianty. Liczba pełnych certyfikowanych polityk poniżej jest oddzielona od liczby legalnych rolloutów.

| Obserwacja s | CPU planera | Pełne K1 | Pełny rollout: poza0,5 | Pełny rollout: sweep | Odrzucone drzewa scenariuszy | Pełne cert. polityki |
|---|---|---|---|---|---|---|
| 0.0 | 14.891 | 319 | 2 | 26 | 0 | 1 |
| 0.2 | 5.203 | 105 | 5 | 66 | 0 | 1 |
| 0.4 | 5.422 | 104 | 2 | 58 | 0 | 1 |
| 0.6 | 1.797 | 0 | 2 | 54 | 0 | 1 |
| 0.8 | 1.422 | 0 | 0 | 45 | 0 | 1 |
| 1.0 | 1.641 | 0 | 2 | 44 | 0 | 1 |
| 1.2 | 1.266 | 0 | 0 | 28 | 0 | 1 |
| 1.4 | 1.281 | 0 | 0 | 31 | 0 | 1 |
| 1.6 | 1.297 | 0 | 0 | 29 | 0 | 1 |
| 1.8 | 0.891 | 0 | 0 | 27 | 0 | 1 |
| 2.0 | 1.312 | 0 | 2 | 32 | 0 | 1 |
| 2.2 | 11.562 | 246 | 17 | 81 | 1 | 1 |
| 2.4 | 16.703 | 380 | 41 | 62 | 0 | 1 |
| 2.6 | 5.234 | 90 | 12 | 51 | 0 | 1 |
| 2.8 | 1.656 | 3 | 4 | 40 | 0 | 1 |
| 3.0 | 15.953 | 366 | 16 | 70 | 0 | 1 |
| 3.2 | 11.938 | 265 | 19 | 50 | 0 | 1 |
| 3.4 | 1.484 | 0 | 13 | 35 | 0 | 1 |
| 3.6 | 0.984 | 0 | 6 | 9 | 0 | 1 |
| 3.8 | 1.219 | 1 | 1 | 32 | 0 | 1 |
| 4.0 | 1.250 | 0 | 1 | 35 | 0 | 1 |
| 4.2 | 1.047 | 0 | 3 | 20 | 0 | 1 |
| 4.4 | 1.062 | 0 | 4 | 20 | 0 | 1 |
| 4.6 | 1.031 | 0 | 6 | 18 | 0 | 1 |
| 4.8 | 1.125 | 0 | 7 | 23 | 0 | 1 |
| 5.0 | 1.172 | 0 | 6 | 23 | 0 | 1 |
| 5.2 | 1.188 | 0 | 6 | 25 | 0 | 1 |
| 5.4 | 0.922 | 1 | 0 | 19 | 0 | 1 |
| 5.6 | 0.953 | 1 | 0 | 22 | 0 | 1 |
| 5.8 | 4.078 | 78 | 3 | 39 | 0 | 1 |
| 6.0 | 4.125 | 74 | 3 | 75 | 0 | 1 |
| 6.2 | 10.250 | 237 | 12 | 70 | 2 | 1 |
| 6.4 | 12.656 | 300 | 31 | 71 | 1 | 1 |
| 6.6 | 19.156 | 473 | 68 | 69 | 3 | 1 |
| 6.8 | 16.125 | 413 | 58 | 52 | 2 | 1 |
| 7.0 | 6.922 | 168 | 24 | 34 | 1 | 1 |
| 7.2 | 6.359 | 152 | 28 | 33 | 2 | 1 |
| 7.4 | 9.656 | 239 | 25 | 55 | 1 | 1 |
| 7.6 | 11.297 | 281 | 20 | 49 | 2 | 1 |
| 7.8 | 0.609 | 0 | 0 | 11 | 0 | 1 |
| 8.0 | 3.422 | 72 | 1 | 25 | 0 | 1 |
| 8.2 | 0.734 | 1 | 0 | 4 | 0 | 1 |
| 8.4 | 0.891 | 3 | 0 | 4 | 0 | 1 |
| 8.6 | 1.016 | 4 | 0 | 4 | 0 | 1 |
| 8.8 | 1.000 | 4 | 0 | 4 | 0 | 1 |
| 9.0 | 1.016 | 4 | 2 | 4 | 0 | 1 |
| 9.2 | 0.953 | 4 | 0 | 4 | 0 | 1 |
| 9.4 | 1.000 | 4 | 0 | 4 | 0 | 1 |
| 9.6 | 1.031 | 4 | 0 | 4 | 0 | 1 |
| 9.8 | 0.922 | 4 | 0 | 4 | 0 | 1 |
| 10.0 | 1.156 | 5 | 6 | 2 | 0 | 1 |
| 10.2 | 3.734 | 64 | 10 | 5 | 0 | 1 |
| 10.4 | 0.406 | 0 | 0 | 0 | 0 | 1 |
| 10.6 | 0.422 | 0 | 0 | 0 | 0 | 1 |
| 10.8 | 3.016 | 54 | 18 | 20 | 0 | 1 |
| 11.0 | 3.891 | 59 | 24 | 40 | 0 | 1 |
| 11.2 | 3.422 | 54 | 22 | 35 | 0 | 1 |
| 11.4 | 3.469 | 55 | 28 | 20 | 0 | 1 |
| 11.6 | 6.250 | 108 | 34 | 29 | 0 | 1 |
| 11.8 | 1.125 | 4 | 8 | 7 | 0 | 1 |
| 12.0 | 2.625 | 48 | 8 | 12 | 0 | 1 |
| 12.2 | 0.500 | 1 | 0 | 1 | 0 | 1 |
| 12.4 | 0.484 | 1 | 0 | 1 | 0 | 1 |
| 12.6 | 0.344 | 0 | 0 | 0 | 0 | 1 |
| 12.8 | 0.328 | 0 | 0 | 0 | 0 | 1 |
| 13.0 | 15.969 | 386 | 16 | 14 | 0 | 1 |
| 13.2 | 1.625 | 0 | 34 | 20 | 0 | 1 |
| 13.4 | 11.641 | 249 | 53 | 39 | 0 | 1 |
| 13.6 | 22.656 | 518 | 145 | 45 | 2 | 1 |
| 13.8 | 14.391 | 323 | 77 | 33 | 2 | 0 |


## 3. S0, S1 i S2 — oddzielne zmiany

**S0:**153 bez opcjonalnego rankingu. Wszystkie69 wybranych komend, pełne physical hashes13/9, wszystkie gęste natywne próbki oraz fingerprint są identyczne z153. Liczby K1 przy każdej przyjętej decyzji też są identyczne. Różnią się próby opcjonalne i zakres ostatniej przerwanej decyzji. Nie uznajemy6169 zamiast6329 K1 za poprawę algorytmu:6006 przyjętego prefiksu jest takie samo, zmienia się niedokończona końcówka.

**S1 v1 (ujemny):** tani generator kierowania ku przyszłemu korytarzowi z lokalnym odwróceniem momentu yaw. Nie umiał zbudować pełnej rezerwy; od zera nie dostał pierwszej komendy. **S1 v2:** ocena pełnych oryginalnych H09 według przyszłego dryfu, korytarza i prędkości. Ze stanu13,8 s znalazł pełny certyfikat216 K1 zamiast547 w S0, CPU planera17,938 zamiast23,031 s. To pojedyncze porównanie różnych algorytmów, nie bitowo równoważna optymalizacja ani dowód pełnego sukcesu. Od startu jego legalny ruch był za wolny: cel191,389 m osiągał przy17,317 s, lecz nie osiągał dalszego350,779 m do26 s.

**S1 v3:** dodał wymagany dalszy postęp na pozostały horyzont. Wykonalność oceniana jest heurystycznie przed drzewem, ale każda przyjęta polityka nadal przechodzi13/9 i preflight. Ukończył tylko0,6 s, potem decyzja przekroczyła30 CPU-s. **Dokładny widok kandydatów:** prototyp v2/v3 niepotrzebnie ponownie propagował H09 do nowej oceny. Finalny adapter udostępnia właśnie obliczoną pulę bez nowych prognoz; test A/B dowodzi zerowego przyrostu licznika fizycznych prognoz przy odczycie całej puli. To oddzielna, dokładna eliminacja powtórzeń, nie przybliżony cache bezpieczeństwa.

**S2:** używa generatora z wymaganym dalszym postępem, najpierw repropaguje zachowane komendy, a przy zmianie celu nie usuwa całego pokrytego prefiksu przez odjęcie130 kroków. Nowy ogon jest uzupełniany dopiero od miejsca utraty pokrycia. S2 ma nadal pełne scenariusze i świeży preflight, lecz zatrzymał się w1,0 s. Korzyści przy zmianie celu nie zostały potwierdzone natywnie, bo nie osiągnął tej bramy. Nie promujemy tej zmiany jako rozwiązania kosztu.

Nowy S0 udokumentował167763 kandydatów H09: pozycja poniżej0,5 m —24671; wyłącznie sweep przy poprawnych próbkach —4388; wyłącznie beta —13093; inne —0; legalne —125611. To rozłączne kategorie według kolejności pozy→sweep→beta→inne. Surowy licznik prototypu nazwany LocalSweepOnlyRejected obejmował także inne odmowy bez naruszenia pozy; analiza nie powiela tej nieprecyzyjnej nazwy.

Z6169 pul /167763 kandydatów tylko563 wyniki zlewają się przy opisanej w enrichment.json kwantyzacji pozycji, prędkości, heading, yaw, koła i kierownicy. Nie uwzględnia ona wału i kolejki, więc **nie jest równoważnością fizyczną ani podstawą współdzielenia certyfikatu**. Proste usunięcie niemal identycznych końców nie daje tu radykalnego zysku.

Realna następna obserwacja nie jest dokładnie jednym z trzech skorelowanych punktów prognozy. W S1 odmowa0,6 s pojawiła się mimo różnicy pozycji od nominalnej prognozy zaledwie0,50 μm; w S2 przy1,0 s —0,45 μm. Nie można tego tłumaczyć dużym błędem geometrii. Finite13/9 nie gwarantuje zamknięcia wykonalności dla wszystkich stanów pomiędzy próbkowanymi scenariuszami. Ponowna certyfikacja musi pozostać obowiązkowa, a jej odmowa jest prawidłowa.

## 4. Swept clearance — S3 wyłącznie diagnostyczny

Dla dolnych odstępów końcowych a,b oraz tej samej co153 stałej ruchu L: g(t)≥max(a−Lt,b−L(dt−t)). Stąd poprawna dolna granica wynosi min(a,b,(a+b−Ldt)/2) minus niezmieniona tuba numeryczna. Jest nie gorsza od min(a,b)−Ldt/2. To przecięcie dwóch ograniczeń Lipschitza, **bez założenia liniowej interpolacji środka**, zmiany obrysu lub pominięcia yaw. Zakres fizycznych założeń dotyczących held-wrench pozostaje taki sam jak153.

Na1383 rzeczywistych zapisanych interwałach minimum wynosi0.501181389→0.501359427 m. W570 testach z jednakowym przesunięciem obu końcowych pełnych obrysów odzyskano6/139 odrzuceń. To testy geometrii, a nie nowe natywne biegi ani estymacja udziału wszystkich odrzuconych prognoz153.

Testy obejmują granicę0,5 m, niepoprawne interwały, obrót pełnych kapsuł przy yaw30 rad/s i porównanie z gęsto ocenianą rzeczywistą geometrią obrysu. Zysk na danych nie uzasadnił włączenia S3 do sterownika. Wszystkie nowe próby natywne zachowują oryginalny ścisły certyfikat153. Nie ma dowodu, że rozdzielczość krzywej jest dominującą przyczyną kosztu; jej odziedziczona niepewność geometryczna rzędu1,425 mm nie może być utożsamiana z całym zapasem ruchu.

## 5. Koszt na decyzję i sekundę jazdy

| Próba | Zakres jazdy s / okr. | CPU kontrolera | CPU/s jazdy | CPU/przyjętą decyzję | Pełne K1 | p50/p95/max callback |
|---|---|---|---|---|---|---|
| ranked-S-v1 | 13.800 / 0 | 325.391 | 23.579 | 4.716 | 6329 | 1.438/16.000/22.672 |
| S0-full-baseline-v1 | 13.800 / 0 | 326.344 | 23.648 | 4.730 | 6169 | 1.516/16.297/23.250 |
| S1-B-v1 | 0.000 / 0 | 0.703 | N/D | N/D | 0 | 0.703/0.703/0.703 |
| S1-B-v2 | 0.000 / 0 | 30.188 | N/D | N/D | 372 | 30.188/30.188/30.188 |
| S1-B-v3 | 0.600 / 0 | 71.953 | 119.922 | 23.984 | 877 | 10.531/21.562/30.047 |
| S2-B-v1 | 1.000 / 0 | 97.875 | 97.875 | 19.575 | 2112 | 12.750/22.656/30.078 |


Callback obejmuje planowanie, zapis polityk oraz odmowę. Fazy planera są w profilach i CSV. CPU/przyjętą decyzję obejmuje także koszt ostatniej odmowy w liczniku całego kontrolera. Przy zerowym czasie/zerze komend iloraz jest N/D. Zapisany stan13,8 s, próba0,6 s i pełny bieg nie są porównywalnymi dystansami.

## 6. Wszystkie pięć bramek

| A — zapisany stan13,8 s | Wynik | CPU całej diagnostyki / planera | K1 | Węzły/rezerwy |
|---|---|---|---|---|
| S0-A-v1 | PASS lokalny | 25.516 / 23.031 | 547 | 13/9 |
| S1-A-v1 | FAIL/PARTIAL | 3.531 / 1.250 | 0 | 0/0 |
| S1-A-v2 | PASS lokalny | 19.625 / 17.938 | 216 | 13/9 |
| S1-A-v3 | FAIL/PARTIAL | 31.578 / 30.047 | 352 | 4/2 |
| S2-A-v1 | PASS lokalny | 30.406 / 28.750 | 621 | 13/9 |


| Wariant natywny | B cały pierwszy łuk | C pierwsze okrążenie | D dwa okrążenia | E trzy S | Powód zatrzymania |
|---|---|---|---|---|---|
| S0-full-baseline-v1 | PASS | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | technical invariant: STUDY154_ABSTAIN: abstain: STUDY154_CPU_STOP |
| S1-B-v1 | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | technical invariant: STUDY154_ABSTAIN: abstain: finite recourse search has no certified complete reserve |
| S1-B-v2 | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | technical invariant: STUDY154_ABSTAIN: abstain: Study151 deterministic forecast/contact or30 CPU-s review cap |
| S1-B-v3 | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | technical invariant: STUDY154_ABSTAIN: abstain: Study151 deterministic forecast/contact or30 CPU-s review cap |
| S2-B-v1 | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | NIEOSIĄGNIĘTA | technical invariant: STUDY154_ABSTAIN: abstain: Study151 deterministic forecast/contact or30 CPU-s review cap |


S0 był jedną ciągłą próbą od zera, z celem trzech okrążeń, limitem330 CPU-s i bieżącą kontrolą kolejnych bramek. Osiągnął B, zatrzymał się przed C. D/E nie mają osobnych pełnych prób, ponieważ wcześniejsze bramy nie przeszły. S1/S2 nie są dopuszczone do kolejnego dystansu po odmowie w B; nie podstawiano zapisanych komend. Nie powstał nowy sportowy punkt odniesienia S i nie uruchamiano rankingu sportowego ani rekordów L.

## 7–9. Legalność, czas S i czterech zawodników

| Próba | Min obrysu m | Min sweep m | β max ° | Kontakty | Próbki poniżej0,5 m | Czas pełnego S |
|---|---|---|---|---|---|---|
| ranked-S-v1 | 0.582069 | 0.501181 | 12.808 | 0 | 0 | N/D |
| S0-full-baseline-v1 | 0.582069 | 0.501181 | 12.808 | 0 | 0 | N/D |
| S1-B-v1 | 4.099200 | N/D | 0.000 | 0 | 0 | N/D |
| S1-B-v2 | 4.099200 | N/D | 0.000 | 0 | 0 | N/D |
| S1-B-v3 | 4.099200 | 4.095151 | 1.833 | 0 | 0 | N/D |
| S2-B-v1 | 4.099200 | 4.095151 | 1.833 | 0 | 0 | N/D |


**Pełny czas S: N/D.** Zero kontaktów i naruszeń w wykonanym prefiksie nie oznacza ukończenia biegu. S0 kosztuje23,648 CPU-s na sekundę jazdy; S1/S2 są jeszcze droższe i mniej niezawodne. Cel<140/<100 CPU-s całego S pozostaje niespełniony. Nie ma podstaw do skalowania na czterech zawodników: najpierw potrzebny jest generator z realną rezerwą przyszłej wykonalności i stabilna recertyfikacja po rzeczywistych obserwacjach, później pełny S, B i wspólna walidacja interakcji.

## Budżet, niepowodzenia i reprodukcja

Zamknięte pozycje przed tym eksportem: 676.375 CPU-s i5 światów. Limit1500 CPU-s /15 światów; końcowa suma znajduje się w budget.json. Zachowano dużą niewykorzystaną rezerwę i przerwano strojenie po niepowodzeniach kolejnych merytorycznie różnych wariantów. Bez CEM, losowego treningu i zwiększania limitu30 CPU-s decyzji lub330 CPU-s świata. Kompilacje, regresje i CI są poza budżetem eksperymentów.

Każda próba ma source zip i SHA256 rzeczywistego kodu. Dane153 i wcześniejsze nie były nadpisane. diagnosis153-first-pass.json zachowuje pierwszy eksport; finalny diagnosis153.json rozdziela wynik całej prognozy od pierwszego złego interwału. Archive surowych liczników nie jest przepisywane po doprecyzowaniu nazwy. Wczesne prototypy zachowują wszystkie natywne stany, pełne przyjęte polityki i odmowy; szczegółowe pełne lokalne kandydatury są dostępne w później instrumentowanym S0.

[Instrukcje i jawne flagi](../../../tools/viable-safe-contingency/README.md). Replay wszystkich natywnych zapisów: `tools/replay-viewer/?case=viable154`. Pełna telemetria: native/*.json.gz;13/9: policies/; odrzucone rollouty i lokalne H09: diagnostics/; CPU: timings/, decision-costs154.csv i profile polityk; odcinki: corners.csv. Porównania replay są prezentacją, nie nową symulacją ani ekstrapolacją.
