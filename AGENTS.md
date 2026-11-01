# Instrukcje dla agentów — Speedway Manager

## Co przeczytać przed zmianą kodu

Przed rozpoczęciem pracy przeczytaj w tej kolejności:

1. `README.md` — cel produktu i podstawowe zasady symulacji,
2. `docs/core-simulation-spec.md` — wiążące inwarianty CoreSim,
3. `docs/architecture.md` — granice modułów,
4. `docs/gameplay/README.md` — status i indeks projektowania rozgrywki,
5. dokument dotyczący zmienianego systemu.

Jeżeli dokumenty są sprzeczne, nie wybieraj po cichu wygodniejszej wersji. Wskaż konflikt i ujednolić dokumentację w tym samym PR-ze albo zatrzymaj implementację do czasu decyzji.

## Status decyzji projektowych

W dokumentach gameplay używane są trzy statusy:

- **BINDING** — zatwierdzona zasada; kod i testy muszą jej przestrzegać.
- **PROVISIONAL** — kierunek roboczy; można przygotować interfejs, lecz nie wolno budować pełnej funkcji bez wyraźnego zadania.
- **TBD** — decyzja nie została podjęta; nie zgaduj i nie utrwalaj jej w kodzie.

Zwykły opis pomysłu nie oznacza zgody na implementację. Zadanie musi jawnie określać, który etap ma zostać wykonany.

## Kolejność rozwoju

Domyślna kolejność prac:

1. Race Engine i reguły jazdy,
2. Track Engine: stan nawierzchni, pogoda, zużycie i prace torowe,
3. integracja jazdy z dynamicznym torem,
4. zarządzanie meczem, czas, setup, personel i polecenia,
5. regulaminy ligowe, AI menedżera i balans,
6. UI.

Nie omijaj zależności przez dodawanie uproszczonej logiki w niewłaściwej warstwie.

## Nienaruszalne zasady symulacji

- Projekt używa ograniczeń fizycznych i realistycznych konsekwencji, a nie pełnej fizyki motocykla.
- Zawodnicy w jednym kroku podejmują decyzje z tego samego niezmiennego snapshotu. Dopiero po rozstrzygnięciu wszystkie stany są zatwierdzane.
- Wynik dla tego samego stanu i seeda jest deterministyczny oraz niezależny od kolejności kolekcji.
- Nie używaj czasu systemowego, globalnego RNG, `string.GetHashCode()` ani indeksu listy jako źródła losowości domenowej.
- Tor może celowo premiować konkretne pola i linie. Nie dodawaj sztucznego wyrównywania pól startowych.
- Ruch ma uwzględniać przestrzeń zajętą przez rywali. Zmiana linii nie jest teleportacją.
- UI i rendering tylko prezentują wynik; nie mogą zmieniać symulacji.
- Faktyczny stan toru i pełne statystyki mogą być ukryte przed graczem. Gameplay udostępnia obserwacje z niepewnością, nie debugowe wartości CoreSim.

## Granice danych i kodu

- C# przechowuje inwarianty, algorytmy i walidację domeny.
- Pliki balansu przechowują wartości, które mają być strojone bez zmiany algorytmu.
- `Ruleset` przechowuje przepisy zależne od ligi i sezonu.
- Dokumentacja opisuje znaczenie parametrów oraz dozwolone zależności.

Nie przenoś algorytmów do JSON. Nie zaszywaj regulaminu konkretnej ligi w CoreSim. Nie twórz osobnego pliku konfiguracyjnego dla każdej pojedynczej stałej.

## Zasady implementacji

- Target: `.NET 8`.
- Zachowuj kompatybilność publicznego API, jeśli nie blokuje poprawnej architektury.
- Rozbudowuj istniejące modele zamiast tworzyć równoległy silnik.
- Magiczne liczby zastępuj nazwanymi parametrami z jednostką i opisem.
- Używaj jednostek w nazwach lub typach (`Seconds`, `Meters`, `Celsius`, zakres `0..1`).
- Waliduj wartości na granicach domeny; nie naprawiaj po cichu błędnych danych.
- Zmiana modelu musi mieć test inwariantu i — gdy ma sens — deterministyczny test scenariusza.
- Test statystyczny jest diagnostyką, jeśli jego wynik zależy od balansu. Nie zmieniaj go w arbitralny test równości.
- Nie usuwaj testów ani logów tylko po to, by uzyskać zielony build.

## Minimalna walidacja PR-a

1. `dotnet restore`
2. `dotnet build`
3. `dotnet test`
4. testy scenariuszy dla zmienionego systemu
5. aktualizacja odpowiedniego dokumentu i statusu decyzji
6. przegląd diffu pod kątem przypadkowych zmian i ukrytej zależności od kolejności

PR powinien mieć jeden spójny cel. Duży etap dziel na fundament, zachowanie, integrację i balans.

## Obowiązkowa walidacja autonomii S — od badania #158

Główną jednostką wyniku jest pełny, nieprzerwany przejazd trzech okrążeń
(sześciu łuków), od legalnego zatrzymanego startu do rzeczywistej mety,
w jednym oryginalnym świecie Box2D. Wszystkie komendy muszą być autonomiczne;
nie wolno resetować motocykla, wstawiać optymalnej taśmy ani składać prefiksów.

PASS autonomii S wymaga co najmniej 5/5 legalnych ukończeń: jednej próby nominalnej
i czterech innych, legalnych, wcześniej ustalonych warunków. Preferuj10 prób,
jeżeli koszt pozwala. Powtórzenie identycznego deterministycznego przebiegu nie
jest niezależnym potwierdzeniem odporności. Warunki w każdej parze baza/nowy
muszą być identyczne. Zestaw końcowy jest odłożony i nie służy dostrajaniu.

ABSTAIN, naruszenie kontaktowe, utrata wykonalności i wyczerpanie budżetu oznaczają
nieukończenie i pozostają w raporcie. Nie należy sztucznie kontynuować jazdy.
Jeżeli nie ma pierwszego pełnego S, diagnozuj przyczynę zamiast powtarzać pięć
identycznych porażek. Krótkie testy służą wyłącznie opracowaniu i naprawie;
nie zastępują wymaganej serii. Brak budżetu na serię nie pozwala ogłosić sukcesu.

Raport zachowuje każdą próbę i jej czas/okrążenia, wszystkie sześć łuków
(wejście/minimum/wyjście), profile prostych, gaz0/częściowy/100%, beta, yaw,
wheelspin, Fx/Fy, odstępy całego obrysu i swept, odmowy/naprawy/odrzucenia,
CPU decyzji i całego biegu oraz rzeczywistą drogę. Podaj medianę ukończeń,
rozrzut, liczbę DNF i porównania parami. Nie ukrywaj nieukończeń w średnich.

PASS poprawy sportowej wymaga ponadto przewagi nad porównywalną bazą S.
Jeżeli baza nie kończy, raportuj poprawę niezawodności bez procentowej
poprawy czasu. Historyczne L (w tym53,989766s i55,167079s) nie są ukończeniami S.
Fizyka, silnik, opony, przyczepność, pełny obrys>=0,5m, swept, aktuatory,
opóźnienia, niezależność certyfikacji i ciągłość świata pozostają obowiązujące.
