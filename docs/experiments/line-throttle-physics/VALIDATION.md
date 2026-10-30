# Walidacja badania #156

- Dokładna baza: HEAD Draft PR154 `d1eda795c66a527d25f9ecebd22a6f82d30e5a9b`.
- Lokalny Windows/.NET8: locked restore i Release warnings-as-errors — PASS.
- Pełne C# `core` + `trajectory`: **2540/2540 PASS**, bez pominiętych.
  Po końcowej aktualizacji danych również4/4 testy nowego badania — PASS.
- Pełna kalibracja Python: **354/354 PASS**; polityka CI: **7/7 PASS**.
- Pełny zestaw replay Node: **107/107 PASS**.
- Discovery: **3061**, każdy przypadek dokładnie w jednym shardzie, bez luk
  i duplikatów: core2145, trajectory395, historical-analysis473, four-rider48.
- Guard39 zamrożonych konsumentów produkcyjnych — PASS.
- `generate156.mjs --check` — PASS: mechaniczne rozszerzenie okna audytu i stopu,
  bez zmian natywnych równań, adaptera lub kroków Box2D.
- Git source proof zachowuje wszystkie poprzednie drzewa eksperymentów oraz
  `src`, `data`, `tools/shared-world-speedway`.
- Wykresy obejrzane: czytelne osie, wspólny tor, fizyczne XY, siły i energie.
- Browser QA w dostępnej przeglądarce aplikacji: zmiana stanu, odtwarzanie,
  pauza, przewijanie klawiaturą, pełny koniec zapisu i brak ekstrapolacji — PASS.
  Nie deklarujemy osobnego testu Chrome/Edge. `replay-qa.png` jest prawdziwym zrzutem.

Pierwsze3 błędy C# dotyczyły szukania plików względem katalogu roboczego testhosta;
naprawiono odszukiwanie root repo, bez zmian modelu. Pierwsze błędy Python wynikały
z CRLF trzech historycznych źródeł; przywrócono ich dokładne bloby Git zgodnie
z checkout Windows CI. Nie zmieniono hashy oczekiwanych, starych testów ani danych.
Browser QA wykryła zaokrąglenie końca suwaka13,059999… do13,05; zakres zaokrągla
się teraz w górę do13,06. Osobny test chroni dostęp do ostatniej rzeczywistej próbki.

Wszystkie standardowe regresje są poza budżetem eksperymentu. Historical/four-rider
zostały odkryte, lecz nie wykonane — zgodnie z niezmienioną polityką Draft.
Końcowy Windows CI i dokładny testowany SHA są podane w opisie Draft PR155.
Zielone CI nie zastępuje oceny naukowej i nie potwierdza nowego autonomicznego AI.
