# Czas i okna decyzyjne

## Dwa rodzaje czasu — BINDING

System rozdziela:

- **czas domenowy meczu** — wpływa na suszenie toru, pracę mechaników, dostępność zawodników i terminy regulaminowe,
- **czas decyzji gracza** — zależy od poziomu trudności i dostępności, ale nigdy nie zmienia kosztu wykonania zadania w świecie gry.

Samo zatrzymanie interfejsu nie może magicznie zakończyć regulacji motocykla ani odnowić dostępności zawodnika.

## Okno decyzji — BINDING

Każde `DecisionWindow` powinno posiadać:

- typ i przyczynę,
- czas otwarcia i deadline w czasie domenowym,
- listę dozwolonych decyzji,
- politykę działania po wygaśnięciu,
- powiązany `Ruleset`, jeśli deadline wynika z regulaminu.

Okno może zostać skrócone, wydłużone albo zamknięte przez zdarzenie meczowe. Zmiana musi być zapisana w logu.

## Zadanie — BINDING

Każda praca lub instrukcja ma jawne właściwości:

- `IssueDuration` — czas potrzebny na przekazanie i potwierdzenie polecenia,
- `ExecutionDuration` — czas właściwego wykonania,
- wymagany personel i liczba stanowisk,
- czy wymaga obecności zawodnika,
- czy może działać równolegle,
- czy może być kolejkowana,
- najpóźniejszy bezpieczny moment rozpoczęcia,
- skutek przerwania,
- ryzyko i jakość pracy w pośpiechu.

Nie każde zadanie ma oba czasy. Wcześniej ustalona polityka może mieć zerowy `IssueDuration`, ale wykonanie nadal kosztuje czas.

## Równoległość — BINDING

Równoległość wynika z realnych zasobów. Dwóch mechaników może wykonywać niezależne zadania, lecz jedna osoba, jeden motocykl albo jedno stanowisko nie mogą być używane równocześnie przez dwie prace.

Kolejka musi ujawniać konflikt zasobów przed zatwierdzeniem planu.

## Zawodnik jadący bieg po biegu — BINDING

Zawodnik wracający z toru nie jest natychmiast dostępny. System uwzględnia:

1. powrót do parku maszyn,
2. minimalną ocenę stanu zdrowia i motocykla,
3. czas przekazania feedbacku,
4. wezwanie do kolejnego biegu i dojazd pod start.

W krótkim oknie dostępne są wyłącznie szybkie, wcześniej przygotowane działania. Znaczenie zyskują drugi motocykl, ustawienia bazowe i delegowanie mechanikowi.

## Tryby czasu — PROVISIONAL

- **Spokojny** — gracz nie ma presji zegara ściennego, ale zadania nadal zużywają budżet czasu domenowego.
- **Standardowy** — zegar decyzji działa w aktywnych oknach; interfejs pokazuje konsekwencje czasowe przed zatwierdzeniem.
- **Realistyczny** — czas płynie stale poza systemowym menu pauzy, a wcześniejsze przygotowanie i delegowanie są kluczowe.

Tryb standardowy jest planowany jako domyślny. Dokładne limity będą parametrami balansu, nie stałymi w UI.

## Praca w pośpiechu — PROVISIONAL

Gracz może zlecić tryb przyspieszony tylko dla prac oznaczonych jako możliwe do przyspieszenia. Skrócenie czasu zwiększa ryzyko niepełnego wykonania albo błędu mechanika; nie może zmieniać zadania w rzut monetą bez związku z umiejętnościami.

## Do ustalenia — TBD

- dokładne czasy przerw i czynności dla poszczególnych trybów,
- minimalny czas regeneracji komunikacyjnej zawodnika po biegu,
- krzywe ryzyka pracy w pośpiechu,
- zasady pauzy i dostępności dla graczy wymagających ułatwień.
