# Walidacja badania #158

Baza: dokładnie `7c24c31dd8587889ff2459608c328fb865a541ef`, Draft PR #156.
Gałąź `experiment/robust-three-lap-158`; domyślnie OFF, bez merge.

## Natywna autonomia S

- Końcowy v6: **10/10 pełnych biegów po trzy okrążenia**, zero DNF,
  kontaktów i ABSTAIN. Część niezależna6/6 obejmuje nominalny oraz pięć nowych,
  odłożonych warunków; pozostałe cztery to jawne regresje v5.
- Baza:0/10, odmowa pełnej rezerwy przed wydaniem pierwszej komendy.
  Poprawa niezawodności jest zmierzona; procent poprawy czasu jest nieokreślony.
- Mediana100,917244s, zakres99,885698–101,567664s. Rzeczywisty obrys min0,576933m,
  swept min0,517854m, |beta| maks.9,032541°. Wymaganie0,5m pozostaje bez zmian.
- Każdy bieg ma jeden oryginalny świat Box2D, legalny zatrzymany start,
  ciągłe koło/wał/aktuator/pending i rzeczywiste przekroczenia trzech met.
  Nie stosowano restartów, historycznych komend ani sklejania prefiksów.
- Każda wykonana komenda odpowiada korzeniowi świeżego certyfikatu13/9
  i przeszła niezależny preflight. Audyt porównuje pełne fizyczne stany i pending.
  Wyłącznie odziedziczony `NextReview` jest ustawiany na rzeczywisty czas callbacku;
  nie normalizuje się żadnego czasu aktywacji ani elementu pending.
- Wszystkie33 nowe światy pozostają w logach: rozwój, v5, baza i v6.
  V5 nadal ma wynik4/5 PARTIAL. Sześć time-zero DNF z błędem starego kolektora
  odtworzono jawnie z zachowanych raw+journal+polityka; nie odtwarzano ruchu.
- `evidence-manifest.json` pieczętuje232 pliki oryginalnych i pochodnych dowodów.
  `policies/<próba>.zip` zachowuje wszystkie oryginalne pliki decyzji bezstratnie;
  SHA-256 każdego wpisu porównano z luźnym oryginałem.

## Regresje źródeł i danych

Lokalne zestawy przed publikacją, na tych samych zamrożonych źródłach:

- locked restore i Release build z `--warnaserror`: PASS;
- C# core+trajectory: **2557 PASS**, w tym11 nowych testów;
- Python calibration: **371 PASS**; CI-policy: **7 PASS**;
- Node replay: **114 PASS**, w tym4 nowe testy rzeczywistych danych;
- discovery: **3078**, unia shardów identyczna, zero luk i duplikatów;
  core2162, trajectory395, historical-analysis473, four-rider48;
-39 zamrożonych konsumentów: PASS;
- `generate158.mjs --check`: PASS; mechaniczna pochodna skończonego protokołu,
  oryginalnego runtime oraz Joint150;
-67 drzew Git fizyki, danych i wszystkich wcześniejszych badań są niezmienione;
-13 plików skompilowanego kontrolera i manifest warunków zachowują SHA-256
  z zamrożenia przed pierwszym odłożonym wynikiem v6;
- kanoniczny prefiks:72/72 identycznych bitowo przejść. Odrzucony wariant z4
  różnicami ulp pozostał w danych. Celowo uszkodzony cache wykrywa niezależny provider;
- nominalne v4/v5 oraz v5/v6 zachowują identyczne wszystkie10153 natywne próbki
  i507 wyborów komendy. Powtórzenie to dowód zgodności, nie kolejny niezależny start.

Wczesny test otwartej serii prawidłowo odrzucił dwa warunki końcowe: niezamknięte
rezerwacje CPU i brak kompletu dziesięciu zapisanych prób. Nie uznano go za finalną
walidację. Nieudane audyty eksportu są zachowane w kosztach, wraz z przyczynami.
Logi i TRX lokalnych regresji są w `validation/pre-final-source/`.

Po utworzeniu końcowego commitu zestawy są ponownie uruchamiane w czystej kopii
**dokładnego HEAD**. Dokładny SHA, wyniki i link do zdalnego **Windows CI** są
podane w końcowym opisie Draft PR oraz dostarczonym `Windows-CI-158.json`.
Lokalny PASS nie zastępuje tego sprawdzenia. Dokument nie zapisuje własnego SHA,
aby uniknąć tworzenia kolejnego, już nietestowanego commitu z samym numerem CI.

Zgodnie z istniejącą polityką Draft wykonuje się wszystkie core+trajectory;
historical-analysis473 i four-rider48 są discovery-only. Nie oznacza się ich jako
wykonanych ani nie deklaruje nowego wyścigu czterech zawodników. CI sprawdza kod,
inwarianty, manifest i zapisane dane; pełna seria33 natywnych światów została
wykonana lokalnie i nie jest po cichu zastępowana krótkimi testami w CI.

## Odtwarzanie i ograniczenia

Odtwarzacz sprawdzono w przeglądarce: odtwarzanie/pauza, wybór serii i warunków,
przewijanie do rzeczywistej mety, zatrzymany time-zero DNF i właściwy progressive-b.
Brak błędów konsoli; samodzielny HTML nie odczytuje zewnętrznych źródeł ani nie
symuluje ruchu. Wąski widok i wykresy PNG obejrzano; `replay-qa.png` zachowuje widok.

Najmniejszy margines ponad wymaganie S to tylko0,017854m, w regresji D na szóstym
łuku przy95,56s. Średni koszt to1369,672 CPU-s/bieg, maksymalny callback12,719s.
Przegląd co0,2s nie jest tu limitem0,2 CPU-s. Czterech rywali, inne profile zawodników,
większe prędkości i szerszy zbiór warunków wymagają odrębnego badania.
