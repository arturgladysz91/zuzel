# Walidacja #157

Baza: `9fe4d6612ce13c135fa967d48faa85509257d129`. Eksperyment OFF, bez merge.
Naukowy wynik pozostaje PARTIAL/FAIL celu autonomicznego, niezależnie od zielonych regresji.

- `dotnet restore SpeedwayManager.sln --locked-mode --force`: PASS.
- `dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror`: PASS.
- C# core + trajectory: **2546 PASS**, w tym 6 nowych testów #157.
- Python calibration: **360 PASS**; CI-policy: **7 PASS**.
- Node replay suite: **110 PASS**.
- Odkrywanie: **3067**, unia shardów identyczna, zero luk i niezamierzonych duplikatów.
  Core2151, trajectory395, historical473, four-rider48. Dwa ostatnie są discovery-only
  zgodnie z niezmienioną polityką Draft; nie oznaczamy ich jako wykonane.
- Audyt konsumentów: **39** zamrożonych czytelników; PASS.
- `generate157.mjs --check`: PASS; pięć plików algorytmu nadal identycznych z zamrożeniem
  sprzed pierwszego B. `source-proof.json` zachowuje drzewa źródeł fizyki, danych i wcześniejszych badań.
- Browser QA: odtwarzanie/pauza, selektor stanu, osie XY o równych jednostkach,
  prędkość/pending, rzeczywisty koniec prefiksu. Bez ekstrapolacji.

Pierwszy test replay wykrył różnicę jednego ulp pomiędzy końcowym natywnym czasem
13,030000000000001 a suwakiem13,03. Naprawiono wyłącznie prezentację #157 tolerancją
1e-9s; źródło #156 i surowe próbki pozostały niezmienione. Pierwsza lokalna walidacja
Python użyła starszego HEAD w kopii roboczej D; po ustawieniu dokładnej bazy i ponownym
pełnym wykonaniu wszystkie360 testów przeszło. Obie wcześniejsze porażki zachowano w logach.

Logi lokalne i TRX są w `validation/`. Standardowe regresje/CI nie są rolloutami
eksperymentalnymi i nie są wliczane do budżetu119,359 CPU-s /4 nowe światy.

Wymóg **Windows CI na końcowym HEAD** jest rozliczany przez najnowszy workflow
`Core simulation CI` dla gałęzi `experiment/state-maneuver-transfer-157`.
Dokładny SHA, run, wyniki zadań i link podane są w końcowym opisie Draft PR
oraz w dostarczonym pliku `Windows-CI-157.json`. Lokalny PASS nie zastępuje tej kontroli.

Żaden niepełny natywny prefiks nie dostał PASS pełnego manewru S.
Prognozy odtworzenia, kontrfaktyczne pending i małe perturbacje są jawnie oddzielone
od wykonanych natywnych okien. C/D zamknięte przez wcześniejsze bramki.
