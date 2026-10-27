# Walidacja #139

Lokalny Windows, .NET 8, konfiguracja Release:

|Kontrola|Wynik|
|---|---|
|`dotnet restore SpeedwayManager.sln`|PASS|
|`dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror`|PASS, 0 ostrzeżeń / 0 błędów|
|`dotnet test tests/CoreSim.Tests -c Release --no-build --filter "Shard=core\|Shard=trajectory"`|2425/2425, 0 pominiętych|
|`python tools/ci/check-shards.py`|2946 odkrytych; core 2030, trajectory 395, historical-analysis 473, four-rider 48; każdy dokładnie raz|
|`python -m unittest discover -s tests/calibration -p 'test_*.py'`|231/231|
|`python -m unittest discover -s tools/ci -p 'test_*.py'`|7/7|
|`node --test tools/replay-viewer/replay.test.mjs`|60/60, w tym nowy test #139 w standardowej ścieżce CI|
|`node --test tools/replay-viewer/throttle-steering.test.mjs`|4/4 historyczne #138; razem lokalnie 64 Node|
|`python tools/rider-compatibility-audit/check-consumers.py`|39 zamrożonych konsumentów bez zmian|
|`python tools/motorcycle-physics-validation/generate-sandbox.py --check`|Wszystkie izolowane kopie zgodne z jawną listą podstawień|
|Preflight OFF|2/2 dokładne historyczne fingerprinty; brak powtórzenia historii|
|Wierność dowodów|SHA/rozmiary 22 pełnych raw zgodne z ledgerem, każdy natywny czas i pozycja zachowane; aktywacje po 0,24 s|
|Przegląd diffu|Oryginalne źródła fizyki, src/, polityka, workflow i historyczne wyniki bez zmian|

Nowe testy C# dotyczą sił i benchmarku, a testy danych weryfikują także wszystkie nieudane przebiegi i oddzielne bramki. Żaden test danych nie uruchamia nowego świata. Sandbox jest diagnostyczny i wymaga jawnego przełącznika CLI. Nie dodano czasowej relaksacji ani stabilizatora.

Przegląd wizualny: cztery wykresy PNG, oba profile toru, wspólny kursor A/B/C, przejście do żądania gaz1 i koniec DNF. Viewer zachowuje ostatnią faktyczną próbkę; nie dopisuje ruchu po awarii. Kierunek korpusu i wektor prędkości są pokazywane osobno. Poprawiono wyłącznie prezentacyjne grupowanie float i zakres suwaka, bez przeliczania fizyki.

Standardowy workflow `.github/workflows/ci.yml` pozostaje bez zmian. Dla Draft wykonuje coverage/build/discovery, core, trajectory, Python/Node i bramkę Draft Windows CI; historyczne ciężkie audyty są pomijane zgodnie z istniejącą polityką. Wynik workflow dla **końcowego HEAD** jest podany w PR i końcowym podsumowaniu, aby nie tworzyć kolejnego commita tylko na zapis własnego SHA. Nie traktujemy pominiętego audytu historycznego jako wykonanego pełnego CI.
