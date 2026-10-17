# Reactive In-Segment Rider Decisions — wynik B

Baza: Draft #85, `d16df13cba27fb5f2aa270337d1d0f28fe1dc0a0`, 2026-10-17.
**B — CZĘŚCIOWO. Nie zademonstrowano reaktywnej decyzji #56B o rywalu podczas jazdy.**
Wykonalny mały fragment to dostarczenie nowo utworzonej komendy do trwającego Traverse.
Próby pary zatrzymują się przed obserwacją, ponieważ brakuje wspólnego zaakceptowanego
stanu pary. Nie jest to zaliczenie A/B/C/D ani sukces przez odmowę.

1. **Czy zawodnik rozpoznaje zagrożenie podczas segmentu? Nie w tej parze.**
   Ostatni numerycznie zaakceptowany punkt istnieje lokalnie w `ExecutedPathTraversal`.
   `ResolveProduction` rozwiązuje jednak całego R1, następnie całego R2, a HeatSimulator
   publikuje/Commit dopiero wynik segmentu. W obserwacji wewnątrz R2 dostępny snapshot
   rywala nadal ma czas 0. Gotowy przyszły motion R1 nie jest aktualnym stanem pary.
   Nie przekształcano go w obserwację przez SampleAtTime, średni zegar ani nowy snapshot.

2. **Bez podglądu przyszłości:** nowy wewnętrzny `ControlSource` dostaje wyłącznie
   właśnie zaakceptowany własny `ExecutedPathNode`, po rozpoczęciu ruchu. Nie dostaje
   listy przyszłych punktów ani cudzej intencji. Może zwrócić istniejący
   `ScheduledControlChange`. To dostarczenie komendy, nie drugi system taktyczny.
   Jeśli nie ma opublikowanego stanu drugiego zawodnika w tej samej chwili, próba pary
   przerywa, zanim wykona filtr ryzyka lub podejmie decyzję. Nie ma fikcyjnego alarmu.

3. **#56B nie zostało jeszcze podłączone do takiej obserwacji.** Jego obecny przepływ
   zaczyna od całych niezależnych przejazdów, #55 i ograniczonych prognoz alternatyw.
   Powtórzenie tego po każdym metrze byłoby kosztowne, a użycie gotowych przyszłych
   intencji jako obserwacji naruszałoby cel zadania. Potrzebny jest zaakceptowany,
   wspólny punkt wejścia obserwacji. Nie kopiowano taktyk ani nie dodawano nowych cech.

4. **#85 wykonuje komendę utworzoną dopiero podczas ruchu — test dostarczenia działa.**
   Dwa dodatnie testy używają wyłącznie własnego zaakceptowanego stanu: przekroczenia
   21 m/s na prostej lub przejścia poniżej pozycji 1,55 podczas zacieśniania łuku.
   Są to jawne testowe wyzwalacze, **nie wykrycie rywala, zagrożenie ani decyzja #56B**.
   Na wejściu `ScheduledControl` jest null. Dopiero źródło tworzy komendę. Używa
   istniejącego prowizorycznego proxy reakcji `CalculateStandingStartReactionTimeSeconds`
   (tu około 0,24 s), bez kalibracji nowych cech.

   | Test dostarczenia | ObservedAt | DecisionAt — utworzenie testowej komendy | RequestedActivation | ActualActivation |
   | --- | ---: | ---: | ---: | ---: |
   | Własna prędkość → LiftThrottle | 0,487378865 | 0,487378865 | 0,727378875 | 0,768964589 |
   | Własna pozycja → istniejący cel boczny | 0,095238090 | 0,095238090 | 0,335238099 | 0,380952358 |

   Komenda jest wydana i aktywowana raz, nie wcześniej niż termin. Błędny RiderId,
   termin sprzed obserwacji i równoczesna komenda zadana na wejściu są odrzucane.
   Po wydaniu komendy źródło nie jest ponownie wywoływane. Wykonawca wykorzystuje
   ten sam gaz/hold/cel i prymitywy #85. Nie ma nowej siatki, replay ani Solve przy dostarczeniu.

5. **Uniknięcie kontaktu przez reaktywną parę: niepotwierdzone.** W A porównanie źródeł
   informacji pozostaje jawne: bez reakcji jest kandydat ruchu bez decyzji o rywalu;
   początkowe #56B korzysta z wejściowego snapshotu, znanych intencji i prognoz;
   próba podczas ruchu dysponuje własnym bieżącym punktem i nieaktualnym snapshotem
   rywala, więc nie podejmuje decyzji. Początkowe #56B nadal wybiera YieldOutward i
   niezależne #55 potwierdza brak kontaktu. Nie zaliczamy tego jako reaktywnego A.

6. **Późny kontakt nie jest usuwany.** W osobnym referencyjnym audycie #55 kandydat C
   nadal ma próbkę przenikania do 0,143196 s, jak #85. Sama próba reaktywna kończy się
   już przy żądaniu obserwacji 0,047698 s. Nie kontynuuje przez kontakt, nie stosuje
   skutków ani nie generuje Crash. Nie twierdzimy, że zero wykonanych reakcji oznacza
   zero kontaktów. Test wcześniejszego kontaktu z #85 pozostaje zielony.

   | Przypadek | Wynik próby | Własny czas żądania obserwacji / dostępny czas rywala | ObservedAt / decyzja / requested / actual | Kontakty reaktywne | Koszt do odmowy |
   | --- | --- | ---: | --- | --- | ---: |
   | A, R2 | Brak wspólnego stanu | 0,047619 / 0 s | wszystkie N/A | N/A | 0,1028 ms / 35304 B |
   | B, R1 | Brak wspólnego stanu | 0,045416 / 0 s | wszystkie N/A | N/A | 0,0286 ms / 5904 B |
   | C, R2 | Brak wspólnego stanu | 0,047698 / 0 s | wszystkie N/A | N/A | 0,0481 ms / 10544 B |
   | D, R2 | Brak wspólnego stanu | 0,047619 / 0 s | wszystkie N/A | N/A | 0,0396 ms / 13352 B |

   W B próbę otrzymuje poruszający się bocznie R1; zewnętrzny R2 korzysta z osobnej
   ścieżki stałej linii. D także nie jest zaliczonym testem filtra pary. Każda próba
   wykonuje jedną własną notyfikację, 0 filtrów pary, 0 #56B, 0 dodatkowych prognoz,
   0 #55 i 0 zmian sterowania. Liczba alarmów/kontaktów pozostaje nieustalona, nie 0.
   Wspólny zaakceptowany czas zostaje 0; żaden wynik nie jest Commitowany.
   Koszt zawiera okablowanie próby i wyjątek, a czasem cały wcześniejszy, niezatwierdzony
   kandydat R1. **Nie porównujemy tej odmowy z pełnym segmentem jako przyspieszenia.**

7. **Historia i projekcja:** dodatnie testy dostarczenia porównują wszystkie węzły
   i kroki do aktywacji włącznie z wariantem bez źródła — dokładnie identyczne.
   Rich TrajectoryEvaluator dostaje ten sam deterministyczny testowy generator
   komendy i zwraca identyczny motion oraz aktywację. Źródło wygasa z bieżącym prefiksem.
   To dowód dla własnego deterministycznego wyzwalacza; nie dowód prognozowania
   przyszłych, niezaobserwowanych decyzji pary. #55 niezależnie audytuje referencyjne
   ruchy A/B/C/D; nie certyfikowano nieistniejącej reaktywnej kontynuacji pary.

8. **Stała linia:** aktywne źródło na zwykłym integratorze stałej linii jest jawnie
   odrzucane. Nie przełączano go na inny wykonawca ani nie przeliczano historii.
   Bez źródła dotychczasowy ruch, zdarzenia i wyniki pozostają identyczne.

9. **Koszt fragmentu dostarczenia, Release:** Windows/.NET 8, tiered compilation OFF,
   4 rozgrzewki, mediana 7 próbek po 4 wykonania. Te same dane i pełny horyzont segmentu;
   przygotowanie wejścia/liczniki poza timerem, bez ciężkiej diagnostyki w pomiarze.
   Komenda w dodatnim teście rzeczywiście powstaje wewnątrz mierzonego wykonania.
   Zachowana DLL #85: SHA256 `6E09557045D28AFB994E0B08F70968D6C9E90E843F5F38F73BC5C844D8FF203C`.

   | Kontrola | #85 | Nowe bez źródła | Notyfikacje własnych punktów bez komendy |
   | --- | ---: | ---: | ---: |
   | Prosta | 0,043200 ms / 27754 B | 0,043150 ms / 27754 B | 60; 0,043500 ms / 27754 B |
   | Ruch na łuku | 0,193225 ms / 53778 B | 0,190500 ms / 53778 B | 27; 0,192000 ms / 53778 B |
   | Stała prosta | 0,020225 ms / 16034 B | 0,021250 ms / 16034 B | źródło niedostępne |

   Kroki/oceny sprzężone bez zmiany komendy: prosta 60/120, łuk 27/360, identyczne
   z #85. Nie ma dodatkowych prognoz, #54, #55 ani #56B w tym fragmencie.
   Dodatnia komenda gazu: 0,041900 ms / 27850 B, 10 notyfikacji, 1 aktywacja, nadal
   60/120; +96 B obejmuje nową komendę i obserwację aktywacji. Dodatnia komenda celu:
   0,148475 ms / 37842 B, 2 notyfikacje, 28/186 — inna wykonana trajektoria, nie dowód
   przyspieszenia. Małe różnice czasów są szumem krótkiego pomiaru. Koszt prawdziwego
   filtra i decyzji pary pozostaje **niezmierzony**, bo nie ma wymaganego wejścia.
   [Koszty fragmentu](reactive-control-measurements.csv), [odmowy par](reactive-control-pair-attempts.csv).

10. **Rozwój na czterech zawodników: brak dowodu bezpieczeństwa.** Brakuje kontraktu
    wspólnej granicy zaakceptowanego ruchu: dwa zachowane wykonania muszą móc dojść do
    przyczynowo dostępnej obserwacji pary, bez ponownej klasyfikacji, RNG i wear.
    Obserwacja musi obejmować rzeczywiste pozycje/prędkości/kierunki w tej samej chwili,
    z niezależnym zatwierdzeniem geometrii wspólnego przedziału. Dopiero wtedy można
    wywołać istniejące #56B po tanim filtrze i przekazać jego wynik przez ten hook.
    Dla czwórki dochodzi sześć par i różne segmenty/granice czasu.
    Obecne zamknięte Traverse ma ponad 20 lokalnych wartości/buforów i wspólne
    Solve/Evaluate/LocalEnvelope. Ich wznawianie oznacza zachowanie dwóch kontekstów
    wykonania oraz nową granicę publikacji/akceptacji, nie dopisanie jednego callbacku.
    Koszt tej przebudowy w liniach i runtime nie został zmierzony; nie deklarujemy,
    że będzie mały. Nie tworzono nowych kursorów, koordynatora ani solvera.

Rdzeń zmiany względem #85: **32 dodane / 8 usuniętych linii**, bez nowych klas CoreSim
i bez zmian wzorów. Pozostały kod to izolowane testy i pomiary. Domyślny manager nie
tworzy źródeł. Pełne fingerprinty bez źródła, przy pustych notyfikacjach oraz wszystkich
referencji A/B/C/D i początkowego #56B w A zgadzają się z zachowaną biblioteką #85.
Walidacja: **16/16 celowanych testów Release**, 2 nowe testy audytu źródeł, wszystkie
39 oryginalnych hashy zachowane przez osobną przypiętą ekstrakcję. Jedna końcowa
kompilacja rozwiązania Release: 0 ostrzeżeń/błędów; później tylko rekompilacja runnera
z dodatkowym zapisem referencji A. Bez zmian historycznych testów/kalibracji i workflow.
Lokalny restore pomijał niedostępne metadane NuGetAudit; Windows CI pozostaje standardowe.

Odtworzenie: filtr ReactiveControlTests, build Release, DOTNET_TieredCompilation=0,
`dotnet tools/reactive-control-audit/bin/Release/net8.0/CoreSim.Tests.dll output.json`.
Runner można zbudować z `BaselineCoreSimPath` wskazującym zachowaną DLL #85.
**Zatrzymano eksperyment na dostarczeniu nowej komendy i dowodzie braku wspólnej obserwacji.**
