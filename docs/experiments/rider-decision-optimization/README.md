# Rider Decision Optimization #133 — PARTIAL

Status **PROVISIONAL**, domyślnie OFF. Nowa metoda to score B + selekcja R1, główny kandydat **B1 #11021**. Wynik finału nie zmienia tego wyboru.

## 1. Baza, HEAD i PR

Dokładna baza `e8f7d822ab549019967aecba5868e3132d87c07e`. [Draft PR #133](https://github.com/arturgladysz91/zuzel/pull/133), bez merge. Dokładny HEAD zmiany i wynik Windows CI są w opisie PR. [Protokół](protocol.json), [hash polityki i planu przed finałem](winner-lock.json). Historyczne dane #129–#132 zachowano.

## 2. Przyczyna częstego gaz0

Bezpieczeństwo jest porównywane przed wynikiem. Wśród legalnych kandydatów dodatni postęp konkuruje z kosztem kierunku χ względem przyszłej stycznej geometrii, yaw, gazu, zmiany kierownicy, prędkości geometrii i prześwitu. χ jest kierunkiem **prędkości**, ψ kierunkiem **ramy**, a β ich różnicą. Nie zakładamy ich równości. Gaz0 nie jest bezpośrednio nagradzany.

Kierunek porównuje χ na końcu H09 ze styczną położoną jeszcze dalej, o referencyjną prędkość końcową×`PreviewSeconds`. Zwiększenie prędkości przesuwa więc także punkt podglądu geometrii. Wyższy koszt direction nie oznacza automatycznie większego β ani gorszego ustawienia ramy. Audyt rozkłada tę rzeczywistą starą funkcję; nie utożsamia jej punktu odniesienia z udowodnioną optymalną linią.

## 3. Rozkład wszystkich siedmiu składników

Odtworzono 380 stanów czterech natywnych replayów #131/#132 i wszystkie kandydaty. Suma starych składników jest bitowo zgodna z oryginalnym score (maksymalny błąd0). [Pełny audyt gzip](cost-audit.json.gz), [hash oryginalnych bajtów](cost-audit-archive.json), [różnice siedmiu kosztów](cost-differences.json). W tabeli: gaz0, istniejący dodatni gaz tej samej klasy, większy prognozowany postęp, lecz gorszy stary wynik. Dominujący składnik to największa dodatnia różnica. Oddzielne mediany nie sumują się do mediany sumy.

| Faza | Liczba stanów | Dominujące składniki | Mediana direction | Mediana progress |
| --- | --- | --- | --- | --- |
| straight | 9 | {'Direction': 9} | 1.6376208274185506 | -0.5472125030228838 |
| entry1 | 27 | {'Direction': 25, 'Yaw': 2} | 1.158460007094945 | -0.5150447350318803 |
| middle1 | 31 | {'Direction': 28, 'Yaw': 3} | 1.3771000109116613 | -0.4561242846038063 |
| exit1 | 16 | {'Direction': 8, 'Yaw': 7, 'SteeringChange': 1} | 0.6902322859645122 | -0.529034162033021 |
| preparation2 | 39 | {'Yaw': 9, 'GeometrySpeed': 24, 'Direction': 5, 'SteeringChange': 1} | 0.6577014565915547 | -0.6078900568692625 |
| bend2 | 81 | {'Direction': 62, 'Yaw': 17, 'SteeringChange': 2} | 2.7758265754213545 | -0.5807729338809651 |

Poza sześcioma żądanymi fazami zapisano jeszcze 4 takie decyzje po wyjściu z drugiego łuku; pozostają w pełnych danych, poza tabelą faz.

Reprodukcja definicji #132: **194** zamknięć z dodatnią alternatywą tej samej klasy, **176** z wyższym kosztem kierunku; mediana **+2.014**. [Odtworzenie](historical-cost-reproduction.json). To szersza populacja niż powyższy warunek większego postępu.

## 4. Czy stara decyzja była rzeczywiście wolniejsza?

Sześć pojedynczych zamian przy zachowaniu pełnej historii świata i pending. Mechanika jest identyczna przez aktywację, cały stan zawodnika identyczny ściśle przed0,24s; dalej wraca pierwotna autonomia. Czasy dotyczą całych dwóch łuków i1s kontroli. Większy postęp H09 nie wystarcza.

| Faza | Obserwacja / aktywacja s | Stary pełny czas | Wynik zamiany | Pełny czas | Zysk s | Czy to wybór offline B1 |
| --- | --- | --- | --- | --- | --- | --- |
| straight | 0.80 / 1.04 | 17.330000000000002 | completed | 16.92 | 0.41000000000000014 | False |
| entry1 | 1.60 / 1.84 | 17.330000000000002 | completed | 17.34 | -0.00999999999999801 | False |
| middle1 | 4.00 / 4.24 | 17.330000000000002 | completed | 17.18 | 0.15000000000000213 | False |
| exit1 | 5.80 / 6.04 | 17.61 | uncertified interval | None | None | True |
| preparation2 | 10.60 / 10.84 | 17.61 | completed | 17.67 | -0.060000000000002274 | False |
| bend2 | 13.20 / 13.44 | 17.61 | completed | 17.56 | 0.05000000000000071 | False |

[Pełna analiza przyczynowa](causal-analysis.json) zawiera też bramkę pierwszego łuku. Dodatni zysk oznacza szybszy pełny przejazd. DNF nie ma porównywalnego czasu. Ręcznie wybrana według zamrożonej reguły alternatywa dowodzi możliwości innego skutku, a nie automatycznie skuteczności nowej funkcji. [Zapis prognoz](causal-forecasts.json.gz) jest osobno oznaczony jako prognoza, nigdy replay.

W tych sześciu próbach **żadna z trzech zamian poprawiających pełny czas nie jest wyborem offline B1**; jedyna zamiana zgodna z B1 (exit1) kończy DNF. To istotna negatywna przesłanka: zdiagnozowanie nieoptymalnego gaz0 nie dowodzi, że wybrany nowy kształt sam naprawia te decyzje. Wynik kontrolowanego treningu i niezależnego finału pozostaje konieczny.

## 5. Wybrana zmiana i porównanie B1/B2

Wybrano **jedną** zmianę: pseudo-Huber kierunku, δ10°. Dla SafeInHorizon, dodatniego postępu i przyszłego prześwitu≥0,2m: `20e²/(sqrt(1+(e/δ)²)+1)` zamiast `10e²`. Blisko zera ta sama krzywizna i interpretacja DirectionWeight; dalej wzrost liniowy, wciąż dodatni gradient kontroli. Przy zagrożeniu terminalnym lub innej klasie pozostaje stary kwadrat. Pozostałe sześć składników bez zmian.

B2 offline dodaje odległość reakcji z dodatniej stycznej prędkości końcowej, skalowaną prześwitem. To niewalidowana heurystyka kontynuacji, nie dowód bezpieczeństwa. B1 zmienia 144 decyzje, B2 67 na tych samych prognozach. B2 nie był trenowany ani łączony z B1. [Wybór i uzasadnienie przed treningiem](score-choice.json).

| Offline | Zmiany | Zmiany klasy | gaz0→dodatni | gaz0→dodatni z większym postępem | Mediana zmiany postępu m* | Mediana zmiany prędkości m/s* |
| --- | --- | --- | --- | --- | --- | --- |
| B1 | 144 | 0 | 106 | 106 | 0.5405154585434104 | 1.8737730979919434 |
| B2 | 67 | 0 | 59 | 59 | 0.5154367357049523 | 1.8769073486328125 |

*Mediany tylko zmienionych decyzji. [Porównanie offline i stany końcowe](offline-objective-comparison.json) obejmuje też przyszły prześwit, kierunek, wybory przy zagrożeniu terminalnym i mediany wszystkich380 stanów (te wynoszą0 z powodu przewagi niezmienionych decyzji). Większy prognozowany postęp nie jest dowodem krótszego pełnego czasu.

## 6. Kontrola bez uczenia i deterministyczność

Te same wagi #4061 i rozszerzone akcje. **B bez retreningu kończy wszystkie cztery profile DNF na pierwszym łuku**; samo złagodzenie kierunku nie daje kontroli długoterminowej. Powtórzenia natywne A/B mają identyczne fingerprinty, a OFF odtwarza dokładny #132 Z01. [Preflight](preflight.json) potwierdza poprawność odtworzenia, nie niezawodność B. Nie zmieniono formuły po tych niepowodzeniach.

## 7. Oba seedy i wspólny CEM

Oba ramiona zaczynają od #131 #4061 H09, z gaz0/Drive/1 i utrzymaniem aktywnej komendy #132. Różni je wyłącznie koszt decyzji. Dwa seedy, pięć generacji, pięć polityk, cztery te same scenariusze:1 pojedynczy i3 ciągłe dwa łuki. **Ranking treningowy i elity to R0 #132 dla obu ramion**. Identyczne32 losowania/generację,2 elity, średnia0,4/0,6, std≥15% zakresu.

| Ramię / seed | Ukończenia / światy | Selekcja L: kandydaci i ukończenia |
| --- | --- | --- |
| A-s1332026 | 85/100 | #10020: 6/6 |
| B-s1332026 | 56/100 | #11021: 6/6 |
| A-s2332026 | 86/100 | #12023: 6/6 |
| B-s2332026 | 55/100 | #13023: 4/6 |

[Archiwa wszystkich kandydatów](generations-summary.json), [koszty CSV](generations.csv), [oba seedy](seed-results.json). Nie uruchomiono dodatkowych generacji.

## 8. R0 kontra R1 bez kolejnych treningów

Na tej samej zamrożonej puli i sześciu stanach L stosujemy dwa rankingi. R0 jest dokładnym rankingiem #132. R1: ukończenia, ciężkość DNF, ukończenia dwóch łuków, ograniczenie najgorszej regresji15%, sparowana mediana dwóch łuków vs #4061 na L, osobno pojedynczy łuk i najgorsza para, potem prześwit i stan wyjścia. Brak par to brak danych, nie zero. Bez wymogu większego zapasu ani docelowego offsetu.

Przy równej liczbie ukończeń R1 sumuje zamrożoną ciężkość DNF:1 brak certyfikacji interwału,2 jazda wstecz,3 potwierdzone przekroczenie albo β45,4 pozostałe (np. brak postępu, timeout, limit CPU). Ukończenie zawsze ma0. To reguła selekcji, a nie zmiana certyfikatu lub fizycznych kryteriów DNF.

Baseline L #4061 ma rozszerzone akcje obu ramion. Referencja #131 w finale zachowuje oryginalny zestaw działań; nie utożsamiamy tych dwóch konfiguracji bez pomiaru.

Próg15% rozstrzyga dopiero po niezawodności: w jej równej grupie R1 najpierw preferuje spełniających limit, a jeśli wszyscy go przekraczają — najmniejsze przekroczenie. Pole `Limit15% = false` oznacza niespełnienie warunku, nawet gdy ten kandydat zostaje B1. Taki wybór pozostaje jawnym kandydatem eksperymentu, a nie deklaracją spełnienia wymagań szybkości. Nie wybieramy mniej niezawodnej polityki tylko dlatego, że jej nieliczne ukończenia są szybkie.

| Grupa | ID | Ukończenia | W tym dwa | Mediana dwa vs baseline | Mediana jeden | Najgorsza para | Limit15% |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A0 | 12023 | 6/6 | 4/4 | -3.26% | +0.79% | +6.11% | True |
| A1 | 12023 | 6/6 | 4/4 | -3.26% | +0.79% | +6.11% | True |
| B0 | 11021 | 6/6 | 4/4 | +6.94% | +22.28% | +39.26% | False |
| B1 | 11021 | 6/6 | 4/4 | +6.94% | +22.28% | +39.26% | False |

Czy R0/R1 wskazały ten sam wektor: A — True, B — True. [Efekt rankingu w tej puli](selection-ranking-effects.json). Zgodne wybory oznaczają brak zaobserwowanej zmiany selekcji, a nie dowód równoważności rankingów na dowolnych kandydatach.

[Identyczna pula](selection-candidates.json), [wyniki](selection.json), [miary czasowe](selection-time-scores.json), [wybory przed finałem](ranked-choices.json). K jest tutaj nazwany L0–L5. Koszt ogranicza pulę do maks.3 różnych wektorów na ramię/seed, wybranych wyłącznie na treningu: ostatni R0, powtarzalna kotwica R0 i reprezentant czasu ze wszystkich kandydatów parzystych generacji. **Nie oceniono wszystkich100 kandydatów na L**. Ta sama ograniczona pula służy obu rankingom; to ogranicza siłę wniosku o selekcji.

## 9. Zamrożone parametry

| Parametr | Start #4061 | B1 #11021 |
| --- | --- | --- |
| PreviewSeconds | 0.9910648400046381 | 1.374811662370643 |
| DirectionWeight | 3.2251494695972176 | 2.435471637033419 |
| YawWeight | 1.7641558104925692 | 0.4232497267807128 |
| Drive | 0.3936023368138517 | 0.26857794333636004 |
| ThrottleWeight | 4 | 1.0804720168835131 |
| ProgressWeight | 1.5144165233089346 | 1.1969512512125438 |
| SteeringChangeWeight | 0.05958697340438881 | 0 |
| GeometrySpeedWeight | 0.5440168959155742 | 0.9193350762274378 |

Osiem tych samych parametrów i zakresów. DirectionWeight zachowuje lokalne znaczenie, duży błąd w safe ma inny kształt kosztu. [Polityka](winner.json), [plan](final-plan.json), [aliasy równoważnych konfiguracji](final-aliases.json). B1 nie podmieniono po zobaczeniu finału.

## 10. Dokładna liczba światów i limity

**636** natywnych światów, w tym **400** treningowych. Kategorie: `{'diagnostic': 20, 'train-ab': 400, 'selection': 36, 'final': 180}`. Wszystkie rezerwowane przed utworzeniem. [Pełna księga](attempts.json), [CSV](attempts.csv), [surowe hashe](raw-manifest.json). Równoważne konfiguracje finału wykorzystują dokładnie ten sam zapis, jawnie mapowany, bez udawania dodatkowych światów.

Limit800 światów,7000s CPU epizodów i audytów,1,5mln prognoz,135mln ocen kontaktu. Pierwsza ukończona generacja przekroczyła pomocniczy limit225s: 233.90625s CPU. Trening zatrzymano. Użytkownik jawnie zatwierdził zmianę wyłącznie limitu generacji do600s; wznowiono od następnej generacji. Pierwotny protokół i wszystkie dane zachowano. [Zapis zgody i hashów](budget-amendment.json). Limity łączne, fizyka, score, ranking i kryteria pozostały zamrożone. [Pełne źródło sprzed korekty](budget-source-before.txt) ma dokładnie pierwotny hash z protokołu; można odtworzyć i sprawdzić wyłącznie zmianę wykonawczego limitu.

## 11. Ukończenia i DNF nowego finału

| Polityka / zestaw | Ukończenia | DNF |
| --- | --- | --- |
| 130-H06-single | 23/24 | {'uncertified interval': 1} |
| 130-H06-two | 0/12 | {'uncertified interval': 5, 'control envelope': 7} |
| 131-H09-single | 24/24 | {} |
| 131-H09-two | 12/12 | {} |
| 132-H09-single | 24/24 | {} |
| 132-H09-two | 12/12 | {} |
| 133-B1-single | 24/24 | {} |
| 133-B1-two | 12/12 | {} |
| 133-A0-single | 24/24 | {} |
| 133-A0-two | 12/12 | {} |
| 133-A1-single | 24/24 | {} |
| 133-A1-two | 12/12 | {} |
| 133-B0-single | 24/24 | {} |
| 133-B0-two | 12/12 | {} |

## 12. Czasy parami względem wszystkich referencji

| Zestaw | Kandydat | Referencja | Wspólne ukończenia | Mediana | Najgorsza regresja |
| --- | --- | --- | --- | --- | --- |
| single | 133-B1 | 130-H06 | 23 | +25.45% | +70.64% |
| single | 133-B1 | 131-H09 | 24 | +7.67% | +50.70% |
| single | 133-B1 | 132-H09 | 24 | +9.38% | +40.69% |
| single | 133-A0 | 130-H06 | 23 | +8.87% | +18.53% |
| single | 133-A0 | 131-H09 | 24 | -2.11% | +17.14% |
| single | 133-A0 | 132-H09 | 24 | -5.46% | +10.57% |
| single | 133-A1 | 130-H06 | 23 | +8.87% | +18.53% |
| single | 133-A1 | 131-H09 | 24 | -2.11% | +17.14% |
| single | 133-A1 | 132-H09 | 24 | -5.46% | +10.57% |
| single | 133-B0 | 130-H06 | 23 | +25.45% | +70.64% |
| single | 133-B0 | 131-H09 | 24 | +7.67% | +50.70% |
| single | 133-B0 | 132-H09 | 24 | +9.38% | +40.69% |
| two | 133-B1 | 130-H06 | 0 | brak wspólnych ukończeń | brak wspólnych ukończeń |
| two | 133-B1 | 131-H09 | 12 | +9.94% | +31.93% |
| two | 133-B1 | 132-H09 | 12 | +10.17% | +24.22% |
| two | 133-A0 | 130-H06 | 0 | brak wspólnych ukończeń | brak wspólnych ukończeń |
| two | 133-A0 | 131-H09 | 12 | -0.85% | +2.83% |
| two | 133-A0 | 132-H09 | 12 | -5.51% | +1.81% |
| two | 133-A1 | 130-H06 | 0 | brak wspólnych ukończeń | brak wspólnych ukończeń |
| two | 133-A1 | 131-H09 | 12 | -0.85% | +2.83% |
| two | 133-A1 | 132-H09 | 12 | -5.51% | +1.81% |
| two | 133-B0 | 130-H06 | 0 | brak wspólnych ukończeń | brak wspólnych ukończeń |
| two | 133-B0 | 131-H09 | 12 | +9.94% | +31.93% |
| two | 133-B0 | 132-H09 | 12 | +10.17% | +24.22% |

[Każda para i jej czas](paired-times.json). DNF nie jest pomijany w tabeli ukończeń i nie dostaje fikcyjnego czasu. Pary czasowe warunkują wspólne ukończenie; nie dowodzą niezawodności tam, gdzie kandydat zawiódł.

Osobno pierwszy łuk wewnątrz ciągłych światów dwóch łuków, gdy oba motocykle rzeczywiście osiągnęły pierwszą bramkę całym obrysem. Dalszy DNF jest zachowany i **nie** staje się ukończeniem dwóch łuków:

| Referencja | Pary pierwszej bramki | Mediana zmiany pierwszego czasu | Z tych par oba ukończyły dwa łuki |
| --- | --- | --- | --- |
| 130-H06 | 10 | +21.97% | 0 |
| 131-H09 | 12 | +12.18% | 12 |
| 132-H09 | 12 | +9.32% | 12 |

[Pierwszy łuk i dalsze losy każdej pary](first-bend-pairs.json).

## 13. Faktyczny gaz, zamknięcia i ponowne otwarcia

| Grupa | Żądania1 | Żądania1 przy aktywnym<1 | Aktywne ponowne1 | Średni gaz % | Czas gaz1 % |
| --- | --- | --- | --- | --- | --- |
| A-s1332026 | 16 | 13 | 14 | 14.49 | 1.69 |
| B-s1332026 | 235 | 132 | 191 | 12.17 | 4.3 |
| A-s2332026 | 27 | 22 | 21 | 14.19 | 1.72 |
| B-s2332026 | 45 | 32 | 33 | 15.04 | 2.15 |
| selection | 3 | 2 | 3 | 17.66 | 1.71 |
| final-130-H06 | 0 | 0 | 0 | 30.5 | 2.8 |
| final-131-H09 | 0 | 0 | 0 | 19.31 | 2.02 |
| final-132-H09 | 1 | 1 | 1 | 18.81 | 2.01 |
| final-133-B1 | 10 | 8 | 9 | 14.64 | 2.12 |
| final-133-A0 | 3 | 3 | 3 | 22.61 | 2.2 |

[Każda próba](throttle-by-attempt.json), [sumy ważone czasem](throttle-totals.json). Aktywny gaz może wrócić do1 wskutek wcześniej zaplanowanej komendy, gdy w obserwacji nadal było1, mimo oczekującego0. Oddzielamy to od nowego żądania1 po faktycznym spadku aktywnego gazu. Początkowe0,24s gaz1 jest w udziale czasu, a nie nowym żądaniem polityki.

## 14. Prędkość, linia i przygotowanie drugiego łuku

[Pełne miary każdego finału](final-quality.json) zawierają czas pierwszego wyjścia, czas segmentu przygotowania, wejście/minimum/wyjście obu łuków, pierwsze zamknięcie i przyspieszenie po wyjściu, drogę XY, oscylacje, skręt żądany/wykonany i gaz0 mimo safe+. To opis skutków, bez narzuconej reguły późnego hamowania lub offsetu. [Miary reprezentatywnych zapisów](representative-quality.json) obejmują też diagnostykę. Porównania wykresów w viewer pokazują wszystkie natywne konfiguracje na wspólnym zegarze, bez rozciągania czasu DNF.

Sparowane zachowanie B1 względem #131 na **12** wspólnie ukończonych dwóch łukach (niezależne mediany; prędkości m/s, czas s, energia J):

| Miara | Pary dla miary | Mediana #131 | Mediana B1 | Mediana różnicy par |
| --- | --- | --- | --- | --- |
| FirstBendExitTime | 12 | 7.3076 | 8.2497 | 0.9054 |
| FirstWholeGateTime | 12 | 7.55 | 8.52 | 0.925 |
| FirstWholeGateSpeed | 12 | 17.8378 | 15.7311 | -1.8104 |
| PreparationSeconds | 12 | 3.3178 | 3.7102 | 0.4042 |
| FirstCoastRequestTime | 12 | 0.2 | 0.0 | -0.2 |
| FirstActualIncreaseAfterExitDelay | 12 | 0.8015 | 3.3749 | 1.7254 |
| DistanceThroughGate | 12 | 283.7118 | 284.5949 | 0.569 |
| CoastWithSafePositiveDriveCandidate | 12 | 51.5 | 47.0 | -6.5 |
| BetaAbove20Seconds | 12 | 0.0 | 0.0 | 0.0 |
| BetaAbove35Seconds | 12 | 0.0 | 0.0 | 0.0 |
| TyreLossModelWorkEstimateJoules | 12 | 32528.8511 | 25920.0413 | -8031.4784 |
| MaximumAbsoluteWheelSlipRatio | 12 | 0.9678 | 0.9678 | 0.0 |
| MeanDrive | 12 | 0.174 | 0.1474 | -0.0255 |
| FullDriveFraction | 12 | 0.0134 | 0.0129 | -0.0006 |
| MaximumBetaDegrees | 12 | 8.6495 | 8.4455 | -0.1666 |
| LargeTrajectoryOscillations | 12 | 5.0 | 5.0 | 0.0 |
| MinimumClearanceMeters | 12 | 0.1351 | 0.1407 | 0.0036 |
| MaximumYawRate | 12 | 0.7666 | 0.838 | 0.0553 |
| SteeringRequestTravelDegrees | 12 | 71.25 | 95.0 | 23.75 |
| EngineWorkJoules | 12 | 80962.9567 | 65592.0852 | -17423.8415 |
| Bend1EntrySpeed | 12 | 16.3415 | 14.6266 | -1.2563 |
| Bend1MinimumSpeed | 12 | 14.1573 | 12.7101 | -1.5104 |
| Bend1ExitSpeed | 12 | 17.6821 | 15.3402 | -2.2888 |
| Bend2EntrySpeed | 12 | 18.784 | 16.8146 | -1.9376 |
| Bend2MinimumSpeed | 12 | 14.7467 | 13.7939 | -0.8596 |
| Bend2ExitSpeed | 12 | 18.0065 | 16.4019 | -1.6471 |

[Wszystkie pary zachowania względem #130/#131/#132](behavior-pairs.json). Droga jest mierzona do bramki, bez dodatkowej1s kontroli. Brak ponownego otwarcia po wyjściu pozostaje brakiem zdarzenia; nie imputujemy zera. `FirstWholeGateTime` mierzy całe mechaniczne kapsuły za bramką3m po łuku, tak samo jak C#. `FirstBendExitTime` oznacza wcześniejsze wejście punktu odniesienia motocykla w segment4. Oba czasy obejmują dojazd, a czas przygotowania tylko segment4.

## 15. Uślizg, yaw, energia i wiarygodność modelu

`final-quality.json` zawiera czas β>20°/35°, βmax, yaw, wheelspin, RPM, prace silnika, straty obwodowe i estymowane prace kontaktów bocznych oraz prześwity. Prace kontaktów są estymacją siła×przemieszczenie, nie osobnym prawem tarcia. DNF β45 pozostaje naruszeniem obwiedni i nie jest nazywany ukończeniem. Safety w H09 nie certyfikuje dalszego przejazdu; nieprzekroczenie granic przez obserwacje nie zamienia `uncertified interval` w ukończenie.

## 16. Niezależny holdout

Nowe24 Y i12 X, zamrożone przed treningiem. Inne dokładne stany niż #129–#132, w tym W/Z i U/V. Osobno J trening, P diagnostyka, L selekcja. Polityka, horyzont, score i ranking zamrożone hashem przed pierwszym finałowym światem. Finał nie steruje kontynuacją ani ponownym wyborem.

## 17. Koszt obliczeniowy

CPU epizodów i audytów **4617.594s**, prognozy **1235197**, oceny kontaktu **111767891**; koszt audytu offline i sześciu prognoz przyczynowych też jest w księdze. To identyczna definicja księgowania natywnego CPU jak w #132: obejmuje prognozy i ocenę nowej funkcji; nie obejmuje późniejszego pakowania surowych replayów, raportowania i testów. `generations.csv` dodatkowo mierzy całe generacje z aktualizacją CEM i zapisem; tych wartości nie dodaje się ponownie do CPU epizodów. Rzeczywiste koszty nie są szacowane z czasu ściennego. Budżet testów technicznych/CI jest osobny i nie służy do selekcji.

## 18. Replaye i reprodukcja

[Viewer](../../../tools/replay-viewer/?case=rider-decision&run=final-133-B1-X00&group=X00) pokazuje #130/#131/#132/#133 na tych samych stanach, natywne ślady XY, porównawcze prędkość/gaz/skręt/β/prześwit, oraz różnicę czasu co5m postępu. Tylko rzeczywiste próbki; krok≤0,01s i dodatkowe zdarzenia natywne, bez interpolowanego XY. Dodatkowo SCORE-ONLY: sam nowy koszt i DNF; CAUSAL-straight: niepotrzebny gaz0 z pełnym zyskiem; CAUSAL-exit1: badana dodatnia alternatywa powoduje DNF, podczas gdy stare odpuszczenie kończy przejazd; X00: ciągły świat dwóch łuków. Wybrana polityka może zawieść; pełna jazda referencji pozostaje jawna.

Wybrane pełne replaye w [gzip](selected-replays.json.gz), [manifest](selected-manifest.json). Wszystkie surowe dane lokalne w `C:\Users\Administrator\Documents\Codex\2026-10-23\files-pasted-by-the-user-pr\work\rider-decision-133-raw`. `tools/run-rider-decision.ps1 -Data <nowy-folder> -Raw <pusty-folder>` odtwarza zamrożony harmonogram; skrypt odmawia nadpisania dowodów. Powtórzenie wymaga tego samego natywnego środowiska Windows/.NET8. Limity CPU są zależne od sprzętu; ewentualna zmiana wymaga jawnego rekordu przed wznowieniem.

## 19. Testy i ograniczenia

Testy celowane wykonano przed treningiem: dokładny OFF #132, koszt A, izolowana delta B i identyczne prognozy/klasy, legalność i rozłączność wejść, R1 parowanie oraz wspólny CEM. Po eksperymencie pełne standardowe testy Windows, Python i viewer oraz odtworzenie archiwum CEM; dokładne liczby i CI w opisie PR.

**Jawna naprawa metadanych po zakończeniu finału:** test odtworzył wszystkie100 rzeczywistych wektorów i elity R0, lecz wykrył osiem niepoprawnie zapisanych średnich generacji0 A/seeda1332026. Wznowienie po limicie225s używało tablicy zapisanej generacji jako mutowalnego stanu; następne aktualizacje nadpisały jej metadane. Prawdziwe losowania korzystały z poprawnego bieżącego stanu. [Oryginalny wadliwy plik bez skrótów](resume-archive-original.json.gz), [diagnostyka przed korektą](metadata-repair-diagnostic.txt), [pełne źródło przed naprawą](metadata-source-before.txt) i [rekord hashów, elit oraz ośmiu wartości](metadata-repair.json) pozostają dostępne. Skorygowano wyłącznie średnie tego checkpointu z pierwotnego startu i tych samych elit #10000/#10001; wszystkie parametry kandydatów, metryki, replaye, selekcja, finał i progi są identyczne. Dodano kopie tablic przy wznowieniu i test niezmienności archiwum. **Nie uruchomiono nowych światów ani dodatkowego treningu.** Ta naprawa archiwizacji jest oddzielna od eksperymentalnej zmiany funkcji oceny.

Bez zmian Box2D, silnika, obu opon, masy, bezwładności, geometrii, CoreSim, prognozowania, klas bezpieczeństwa, H09, reakcji0,24s, interwału0,2s, β45, ±35°/90°/s. Nie dodano sił, trajektorii ani nagrody za gaz/uślizg. Zmiana kierunku jest heurystyką użyteczności; nie rozwiązuje formalnie terminalnej kontynuacji. Dwa seedy i ograniczona selekcja nie dowodzą uniwersalności. Zatrzymanie225s i zatwierdzona korekta są jawnym odstępstwem od początkowego planu wykonania, bez ukrywania lub kasowania prób.

## 20. Werdykt: PARTIAL

Kryteria pozostają dokładnie24/24 i12/12, mediana dwóch≥2% szybciej vs #131, mediana pojedynczego≤5% i maksimum≤15% regresji vs #130, poprawna fizyka, obwiednia, niezależność i reprodukcja. B1: mediana dwa **+9.94%**, pojedynczy **+25.45%**, maksimum pojedynczy **+70.64%**. [Wynik maszynowy](analysis.json). Brak spełnienia szybkości lub niezawodności daje PARTIAL; błędna fizyka, scoring, nieporównywalność albo przeciek daje FAIL. Nie zmieniono progów po wyniku.
