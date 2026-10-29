# Walidacja badania #147

Wariant badawczy jest domyślnie OFF. Baza to dokładnie `9828656024eda4177d39f0a29d10d3f2c5287ff6`; zmierzony kontroler pochodzi z `1607ae7`. Późniejsze zmiany obejmują finalizację plików, równoważną relokację cache geometrii, raport i viewer. Nie wykonano drugiego pełnego biegu po tych zmianach.

## Zakres lokalny

- `dotnet restore SpeedwayManager.sln` i Release build z `--warnaserror`.
- C#: 2473 przypadki core + trajectory, w tym 9 testów #147. Pozostałe shardy nie są tu przedstawiane jako lokalnie wykonane.
- Python: 274 testy kalibracji (6 nowych), 7 testów polityki CI.
- Node: 73 testy replay (3 nowe).
- Pełne odkrywanie: 2994 przypadki; core 2078, trajectory 395, historical-analysis 473, four-rider 48; bez braków i nakładania.
- Zamrożone źródła geometrii i Predictor144 są bajtowo zgodne z bazą. Mechanicznie wygenerowany adapter predyktora ma automatyczne porównanie całego tekstu z oryginałem i test identycznych prognoz. Nie osłabiono historycznych testów ani manifestów.
- Audyt 270 decyzji: kompletny hash chain i identyczność wykonanych komend, pełne legalne certyfikaty. Wymuszony test wyczerpania opcjonalnego budżetu zachowuje legalny plan i uruchamia niezależny preflight. Test dziennika obejmuje urwaną końcówkę, ingerencję w rekord i odczyt przy otwartym writerze.
- Wykres `corner147.png` obejrzano. Testy danych i modułu nowego viewera przeszły. Automatyczne sprawdzenie UI w przeglądarce było niedostępne: narzędzie CUA zwracało `failed to write kernel assets` przed utworzeniem sesji, również po resecie. Nie oznaczono browser QA jako PASS.
- `git diff --check` oraz źródłowy audyt `check-source147.py`.

## Windows CI

Nowy Draft PR uruchamia istniejącą politykę CI dla swojego head SHA: coverage/restore/build, Python/Node, pełny core i trajectory oraz końcowy gate Draft. Pozostałe historyczne scenariusze i four-rider są pomijane zgodnie z istniejącą polityką Draft; nie zmieniono polityki w celu uzyskania zielonego wyniku. Wynik i link do konkretnego runu są zapisane w opisie PR oraz dostarczonym `Walidacja147.json`, razem z pełnym head SHA. Ten dokument nie deklaruje wyniku dla nieznanego jeszcze SHA.

## Pomiar eksperymentalny

Jedna pełna natywna próba solo od pola B i 0 km/h: 53,989765674 s, trzy okrążenia, bez potwierdzonego przekroczenia obrysu. 270/270 decyzji trwale zapisanych przed zwrotem komendy. 154,500 CPU-s kontrolera, 168,281250 CPU-s świata; cały ledger 561,156250 CPU-s i jeden nowy świat. Nie uruchamiano kolejnego świata w ramach raportowania/testów. Regresje i generowanie raportów nie są nowymi eksperymentami natywnymi. Nie potwierdzono powtarzalności całego dystansu ani gotowości czterech zawodników.

Środowisko pomiaru: Windows, AMD Ryzen 5 3500X, target .NET 8. Czas procesu CPU, nie wall time. Pozostałe 338,843750 CPU-s zachowano. Po ukończeniu native wystąpił błąd współdzielenia odczytu dziennika w postprocessingu; wszystkie dane już istniały. Wpis `exception` w ledger zachowano, a odrębna finalizacja tylko zweryfikowała istniejące pliki.
