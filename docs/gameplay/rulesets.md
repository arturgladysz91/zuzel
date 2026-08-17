# Rulesety ligowe i sezonowe

## Zasada — BINDING

CoreSim nie może znać przepisów konkretnej ligi. Przebieg meczu, legalność zmian i terminy wynikają z wersjonowanego `Ruleset`.

Każdy ruleset powinien posiadać:

- rozgrywki i sezon,
- wersję danych,
- datę obowiązywania,
- źródło regulaminowe i datę weryfikacji,
- harmonogram biegów,
- wymagania składu,
- rodzaje i limity rezerw,
- limit startów,
- zasady biegów nominowanych,
- terminy decyzji,
- zakres dozwolonych prac torowych,
- punktację oraz warunki zakończenia meczu.

Reguła zależna od sezonu nie może być zaszyta w `MatchEngine`, UI ani testowym helperze.

## Walidacja — BINDING

- Ruleset jest sprawdzany przy ładowaniu i odrzuca sprzeczne dane.
- Legalność decyzji jest obliczana w domenie i zwraca konkretny powód odrzucenia.
- Zmiana rulesetu nie zmienia algorytmów Race Engine i Track Engine.
- Testy scenariuszy obejmują wartości graniczne, np. próg rezerwy i ostatni dopuszczalny bieg.
- Testy jednostkowe mogą używać małego `TestRuleset`; nie powinny kopiować całego regulaminu ligi.

## Zweryfikowany przykład: PGE Ekstraliga 2026

Poniższe punkty są wskazówką dla przyszłego pliku danych, a nie kompletną transkrypcją regulaminu:

- rezerwa taktyczna może być użyta w biegach III–XV, gdy drużyna przegrywa co najmniej 6 punktami,
- po biegu XIII kierownicy mają do 4 minut na zgłoszenie obsady biegów XIV i XV,
- dobór zawodników do biegów nominowanych ma dodatkowe ograniczenia punktowe i dotyczące wcześniejszego zgłoszenia.

Przed wdrożeniem trzeba przepisać pełną macierz legalności z właściwego dokumentu, dodać testy artykułów granicznych i ponownie sprawdzić aktualność źródła.

## Planowane dane — PROVISIONAL

```text
data/rulesets/
├── test-default.json
└── pge-ekstraliga-2026.json
```

Plik dla prawdziwych rozgrywek powstaje dopiero razem z walidatorem oraz testami. Nazwa zawiera sezon, aby aktualizacja regulaminu nie nadpisywała historii zapisanej kariery.

## Źródła

- [PZM — przepisy i regulaminy żużlowe 2026](https://www.pzm.pl/zuzel/regulaminy-i-druki)
- [Regulamin Ekstraligi 2026](https://archiwum.pzm.pl/pliki/zg/zuzel/2026/Regulaminy/2026pgee_20251017_clean.pdf)
- [Regulamin Torów 2026](https://www.pzm.pl/pliki/zg/zuzel/2026/2026_regulamin-torow_20260302.pdf)

## Do ustalenia — TBD

- pierwsze rozgrywki dostępne w grywalnej wersji,
- pełny schemat JSON i migracje wersji,
- sposób zachowania zapisanej kariery po korekcie błędnego rulesetu,
- zakres fikcyjnych rulesetów dla trybów niestandardowych.
