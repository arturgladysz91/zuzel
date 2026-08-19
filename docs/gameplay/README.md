# Gameplay — indeks specyfikacji

Ten katalog opisuje warstwę menedżerską meczu. Nie zastępuje `docs/core-simulation-spec.md`, który pozostaje źródłem prawdy dla Race Engine i fizycznych inwariantów.

## Status zapisów

- **BINDING** — decyzja zatwierdzona i gotowa do implementacji.
- **PROVISIONAL** — kierunek roboczy wymagający dopracowania lub balansu.
- **TBD** — otwarta decyzja; agent nie może jej samodzielnie rozstrzygać.

Status dotyczy najbliższej sekcji. Jeśli status nie jest podany, zapis jest wyjaśnieniem, a nie nowym wymaganiem implementacyjnym.

## Stan realizacji

PR #8 ukończył fundament Race Engine v2: niezmienny snapshot, fazy `Decide` / `Resolve` / `Commit`, deterministyczność, kanoniczną pozycję na torze i statusy zawodnika. Nie oznacza to ukończenia całego Race Engine.

Przed rozpoczęciem Track Engine pozostają:

- start i pierwszy łuk,
- ograniczenia jazdy w łuku i wynoszenie,
- czasowa zmiana linii,
- atak, obrona i blokowanie,
- kontakty, upadki i walidacja fizyki.

Track Engine, zarządzanie torem i systemy menedżerskie pozostają późniejszymi etapami.

Niezmienna geometria konkretnego toru jest wejściem potrzebnym Race Engine i może powstać wcześniej jako mały fundament. Nie obejmuje dynamicznej nawierzchni, pogody, zużycia ani prac torowych i nie oznacza rozpoczęcia właściwego Track Engine.

## Główna pętla gry — BINDING

`obserwacja → feedback → diagnoza → decyzja menedżera → wykonanie przez ludzi → kolejny bieg → ocena skutku`

Gracz zarządza niepewnością i ograniczonym czasem. Nie otrzymuje bezpośredniego dostępu do pełnych wartości CoreSim ani gwarantowanego skutku polecenia.

## Dokumenty

- [`match-flow.md`](match-flow.md) — fazy meczu i miejsca podejmowania decyzji.
- [`time-and-decision-windows.md`](time-and-decision-windows.md) — czas, kolejki prac i sytuacje bieg po biegu.
- [`track-management.md`](track-management.md) — docelowy Track Engine, pogoda, zużycie i zakres wpływu gospodarza.
- [`observations-and-feedback.md`](observations-and-feedback.md) — niepełna wiedza gracza i wiarygodność informacji.
- [`bike-setup.md`](bike-setup.md) — przełożenie, reakcja motocykla i praca mechaników.
- [`team-orders.md`](team-orders.md) — polecenia parowe i ograniczenia ich wykonania.
- [`staff-and-delegation.md`](staff-and-delegation.md) — dostępność personelu, równoległość i automatyzacja.
- [`rulesets.md`](rulesets.md) — przepisy zależne od ligi oraz sezonu.

## Kolejność wdrażania — BINDING

1. Ukończyć i zweryfikować Race Engine.
2. Zaprojektować oraz wdrożyć Track Engine.
3. Połączyć obciążenie generowane przez jazdę ze zmianą toru.
4. Dopiero potem wdrażać czas meczowy, setup, personel i polecenia.
5. Na końcu dołączyć konkretne regulaminy i AI menedżera.

Dokumenty opisujące późniejsze etapy mogą istnieć wcześniej, lecz ich obecność nie jest zgodą na zmianę kolejności implementacji.

## Gdzie umieszczać przyszłe ustawienia

Planowane granice:

```text
data/
├── rulesets/       # przepisy ligi i sezonu
└── balance/        # wartości czasów, efektów setupu i prac torowych
```

Dokładne pliki danych powstaną razem z systemem, który je waliduje. Nie tworzymy pustych konfiguracji bez konsumenta i testu schematu.
