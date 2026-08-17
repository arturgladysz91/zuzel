# Przebieg meczu

## Cel — BINDING

Mecz ma być serią krótkich cykli decyzyjnych, a nie arkuszem ustawień otwieranym bez presji. Wynik biegu zmienia wiedzę gracza, stan zawodników, czas dostępny personelowi i stan toru.

## Docelowe fazy — PROVISIONAL

```text
PreMatchPreparation
→ TrackHandover
→ PreHeatWindow
→ HeatStaging
→ HeatRunning
→ PostHeatDebrief
→ InterHeatWindow
→ kolejne biegi
→ NominatedHeatsSelection (jeżeli Ruleset wymaga)
→ MatchComplete
```

- `PreMatchPreparation` — przygotowanie planu, motocykli i toru w granicach uprawnień gospodarza.
- `TrackHandover` — zmiana zakresu kontroli nad torem zgodnie z regulaminem.
- `PreHeatWindow` — ostatnie krótkie instrukcje i kontrola gotowości.
- `HeatStaging` — zawodnicy są kierowani do startu; część działań przestaje być dostępna.
- `HeatRunning` — CoreSim rozgrywa bieg; gracz nie steruje zawodnikiem klatka po klatce.
- `PostHeatDebrief` — zwięzły wynik, obserwacje i feedback.
- `InterHeatWindow` — ograniczony czas na wybór i wykonanie prac.
- `NominatedHeatsSelection` — specjalne okno wynikające z `Ruleset`.
- `MatchComplete` — końcowe wyniki, konsekwencje morale i zapis danych.

## Rytm po biegu — BINDING

Po każdym biegu gracz powinien zobaczyć:

1. fakty: wynik, wykluczenie, defekt, wykorzystane rezerwy,
2. 2–4 najważniejsze obserwacje zamiast surowej telemetrii,
3. feedback dostępnych zawodników i personelu,
4. zadania już wykonywane oraz pozostały czas,
5. niewielką liczbę decyzji o realnych kompromisach.

Gracz może otworzyć bardziej szczegółową analizę, ale czas meczu rozlicza się zgodnie z wybranym trybem.

## Zdarzenia zmieniające rytm — BINDING

Przebieg nie zakłada stałej identycznej przerwy po każdym biegu. Osobne zdarzenia mogą utworzyć lub zmodyfikować okno decyzji:

- standardowa przerwa między biegami,
- regulaminowa przerwa techniczna,
- powtórka biegu,
- naprawa bandy albo ocena toru,
- przerwa wynikająca z warunków atmosferycznych,
- zawodnik jadący bieg po biegu,
- wybór biegów nominowanych,
- zdarzenie medyczne.

Źródło i maksymalny czas zdarzenia określa `Ruleset`; rzeczywisty czas pracy rozlicza system zadań.

## Brak decyzji — BINDING

Upływ czasu nie może prowadzić do losowej kary wymyślonej przez UI. Po zamknięciu okna system:

1. zachowuje ostatni zatwierdzony plan,
2. wykonuje wcześniej ustawioną politykę delegowania,
3. anuluje zadania, których nie można już legalnie lub bezpiecznie rozpocząć,
4. zapisuje powód automatycznej decyzji.

## Granice odpowiedzialności — BINDING

- `MatchEngine` prowadzi fazy, wynik, dostępność i terminy.
- `Race Engine` rozgrywa pojedynczy bieg.
- `Track Engine` aktualizuje nawierzchnię z upływem czasu i po przejazdach.
- `Ruleset` określa legalność składu, rezerw, przerw i nominacji.
- UI wyświetla stan i wysyła intencje; nie oblicza legalności ani skutku.

## Do ustalenia — TBD

- dokładna liczba ekranów i momentów automatycznego otwierania analizy,
- sposób prezentowania równoległych prac na małym ekranie,
- czy w trybie standardowym gracz otrzyma jednorazową krótką pauzę taktyczną.
