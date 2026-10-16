# Pre-Contact Racing Decisions — wynik eksperymentu

Baza: main `561c06b6e486b4cbb61db66a3a2ad1fab33a0227`, 2026-10-16.
**B — potrzebny kontrakt czasowej aktualizacji istniejącego sterowania wewnątrz segmentu.**
Nie implementowano go. Produkcyjny silnik: **0 zmienionych linii**.

1. **#56B już działa:** wspólnie wybiera KeepIntent/Hold/YieldOutward/BackOut/EmergencyAvoid.
   Metrowy cel, utrzymanie pozycji bocznej i LiftThrottle wykonuje produkcyjny
   ExecutedPathTraversal, z istniejącymi ograniczeniami ruchu, grip i napędu.
   #55 sprawdza obrysy. Nie dodano żadnej równoległej reakcji ani fizyki.

2. **Dokładna luka:** HeatSimulator podejmuje decyzje raz na segment.
   ContestedSpaceInteractionCoordinator najpierw oblicza niezatwierdzone przejazdy;
   przy bliskości prognozuje pozostały łuk i następną prostą. Reakcje, także safety
   pass, wykonuje od początku tego samego snapshotu. Jest to sprawdzanie planów,
   bez osobnego modelu opóźnionej reakcji na nowe zagrożenie.

3. **Wykonanie jest możliwe, termin — nie.** Drive/hold/target są stałe dla całego
   Traverse. Węzeł całkowania co około metr nie jest punktem decyzji.
   Attack/Defense/Combativeness zmieniają preferencje; Technique/Condition niewielki
   margines. Fizyczne wykonanie używa istniejących SlideControl/Adaptability, setupu
   i grip. Nie ma nowej umiejętności percepcji, RNG pomyłek ani dodatkowej przyczepności.

4. **Minimalna próba:** main/#56B kontra pierwsze rozpoznanie teraz, prowizoryczne
   **0,20 s** reakcji i oczekiwanie na następny rzeczywisty punkt decyzji.
   Tani sygnał względnego ruchu korzysta z aktualnych pozycji, skalarnych prędkości,
   stycznych, własnego bieżącego celu i istniejącego budżetu bocznego.
   Nie czyta przyszłych kotwic/przejazdu przeciwnika. Koła otaczające obrysy i
   istniejący horyzont 0,35 s dają ostrzeżenie, **nie czas kontaktu ani certyfikat**.
   Snapshot nie zawiera zmierzonej prędkości bocznej/yaw; niesynchroniczne obserwacje
   są odrzucane. Nie dodano kolejki ani infrastruktury integratora.

5. **Ustąpienie i gaz działają.** W A obaj zacieśniają do referencji 1 z 1,2/1,6.
   Main pozostawia zamiar A; B wybiera YieldOutward z minimalnym celem 4,8 m,
   równym aktualnemu offsetowi. Utrzymuje pozycję zamiast jechać 1,8 m do środka.
   Czas B: +0,03114 s; prędkość: +0,4134 m/s z innej fizycznej trajektorii.
   Osobny istniejący D-inside-overlap wykonuje poszerzenie 0,183 m; test sprawdza
   każdy krok względem budżetu bocznego. W B zewnętrzny pozostaje na 4.
   W C BackOut/LiftThrottle daje straty 0,2013/0,0605 m/s i 0,00088/0,00236 s;
   wejściowe 21 m/s nie jest przestawiane. Delty są względem kontrfaktycznej jazdy bez reakcji.

   | Przypadek | Kontakty main | Bez reakcji: kontakt potwierdzony próbką | Następna zwykła granica bez reakcji |
   | --- | ---: | ---: | ---: |
   | A — zacieśnianie, miejsce po zewnętrznej | 0 | do 0,53207 s; przedział kontaktu od 0,52387 s | 1,19609 / 1,22345 s |
   | B — brak miejsca | 0 | do 1,14591 s; niepewny przedział od 1,10423 s | 1,59410 / 1,63887 s |
   | C — Attack/Defense 99, Combativeness 1 | 0 | już do 0,14320 s | 0,34036 / 0,34660 s |
   | D — blisko, bez zbliżania | 0 | brak | 1,19105 / 1,30419 s |

   Main ma rozstrzygniętą bezkontaktową geometrię. Niekorygowane A/B/C mają próbki
   overlapu 8,70/21,11/19,17 mm. #55 rozstrzyga przedział A; w B/C pierwszy
   przedział pozostaje numerycznie nierozstrzygnięty mimo próbki przenikania.
   Wniosek opiera się na świadkach kontaktu, nie na dokładności pierwszego rootu.
   **Żadnego kandydata nie zatwierdzano**.
   Końce segmentów za kontaktem i delty są kontrfaktyczne, bez skutków i dalszej jazdy.

6. **Kontakt pozostaje możliwy:** C potwierdza go przed terminem reakcji 0,20 s.
   Main nawet przy maksymalnej agresji wybiera BackOut; nie wykazano dobrowolnego
   wyboru znanej kolizji, gdy dostępna jest bezpieczna alternatywa. Nie wymuszano
   zderzeń losowaniem. W A/B 0,20 s upływa przed pierwszym przedziałem potencjalnego kontaktu,
   lecz następna granica decyzji wypada za kontaktem: to dowód luki harmonogramu.

7. **D jest niezakłócone:** brak alarmu, gaz bez ograniczenia, identyczne motion,
   zmiany i zdarzenia, zero dodatkowych wyborów reakcji. Main mimo braku epizodu
   wykonuje dwie bazowe prognozy po wstępnym teście bliskości.
   Sam tani sygnał nie wywołuje #55. Bieżący przejazd nadal podlega sprawdzeniu
   geometrycznemu; brak alarmu nie pozwala ignorować fizycznego kontaktu.

8. **Koszt Release:** Windows/.NET 8, bez tiered compilation, rozgrzewka + mediana 3.
   Snapshot/intencje i liczniki poza timerem. Weryfikacja odroczonego bieżącego
   kandydata jest w timerze; dodatkowy audyt już zweryfikowanego main — poza nim.
   Horyzont wykonania obu wariantów to ten sam segment. #54 nie jest uruchamiany.

   | Przypadek | Main / odroczenie, ms | Dokładne separacje | Prognozy alternatyw | Dodatkowe reakcje |
   | --- | ---: | ---: | ---: | ---: |
   | A | 26,329 / 3,130 | 44992 / 7048 | 6 / 0 | 1 / 0 |
   | B | 28,110 / 3,473 | 51472 / 6639 | 6 / 0 | 2 / 0 |
   | C | 29,115 / 1,903 | 66002 / 4556 | 6 / 0 | 2 / 0 |
   | D | 1,740 / 0,308 | 852 / 187 | 2 / 0 | 0 / 0 |

   Odroczenie **nie jest działającym rozwiązaniem ani przyspieszeniem silnika**:
   A/B/C mają kontakt bez reakcji. Osobny bezpieczny pomiar dodania samego sygnału
   do main w D: 1,7433 → 1,7177 ms, 580264 → 590168 B.
   Różnica czasu nie dowodzi przyspieszenia; +9904 B, dodatkowe #55/prognozy/reakcje:
   **0/0/0**. [Wszystkie liczniki i delty](pre-contact-decisions-measurements.csv),
   [narzut D](pre-contact-decisions-quiet-overhead.csv).

9. **Kod i minimalny kontrakt:** 0 linii silnika; 107 probe + 75 runner + 121 testów.
   Potrzeba terminu aktywacji istniejącego RiderDriveControl/HoldLateralPosition/
   InteractionLateralTarget: zachowaj stare sterowanie do deadline, zastosuj wspólne
   oczekujące decyzje w pierwszym istniejącym węźle nie wcześniejszym od deadline,
   zachowaj wykonany prefiks. Produkcja i projekcja #56B muszą wykonać identyczny
   harmonogram, a #55 sprawdzić wynik. Kontakt przed aktywacją pozostaje kontaktem.
   To konieczny kontrakt, **nie dowód wykonalności każdej późniejszej korekty**.
   Nie implementowano go ani nie rozszerzano eksperymentu.

Walidacja: 7 nowych + 6 istniejących kontroli #56B = 13 przypadków Release.
Zamiana kolejności nie zmienia fizyki ani decyzji. Build rozwiązania: 0 ostrzeżeń/błędów;
po dodaniu asercji decyzji również celowana rekompilacja testów. Standardowe Draft
Windows CI; bez audytów historycznych. CoreSim, kalibracje, domyślne A/B/C i #81–#83
bez zmian. Odtworzenie: build Release, filtr PreContactDecisionTimingTests;
DOTNET_TieredCompilation=0; DLL narzędzia pre-contact-decisions-audit z jedną ścieżką JSON.
