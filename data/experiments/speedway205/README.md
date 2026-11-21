# Dane badania #205

Baza Git to 828c41863a4ef8ad309b49444395aa3a3b9027ec. Wyniki i definicje metryk opisuje [raport](../../../docs/experiments/speedway-h28-c/REPORT.md).

| Dane | Pochodzenie |
|---|---|
| [matched-model.json](matched-model.json) i [synthetic-grid-model.json.gz](synthetic-grid-model.json.gz) | Odpowiedzi na identycznych wejściach archiwalnych i pełna siatka stałych nacisków. Stany kontrfaktyczne są oznaczone w danych. |
| [certification-model.json](certification-model.json) | Porównanie na nominalnych stanach H27-N, reszty, naciski i krzywe z sześciu przekrojów wraz z rozstrzygnięciami. |
| [transients-model.json.gz](transients-model.json.gz) | Ewaluacje przy chwilowej zmianie aktywnego gazu i swobodne prognozy z pending oraz opóźnioną aktywacją. |
| [equilibria-model.json](equilibria-model.json) | Stany quasi-ustalone, wyszukiwania bez wyniku, wykluczenia analityczne i swobodna integracja. |
| [throttle-model.json.gz](throttle-model.json.gz) i [throttle-summary.json](throttle-summary.json) | Wszystkie okna, kontynuacje i pełne fazy, w obu prawach na nominalnym archiwum H27-N. |
| [preflight-model.json.gz](preflight-model.json.gz) | Taśma z Box2D, zamrożona polityka i osobny trening, w tym odrzucenia predykcyjne przed otwarciem świata. |
| [H28-C-training.json.gz](H28-C-training.json.gz) i [changed-transfer.json.gz](changed-transfer.json.gz) | Kandydaci, historia, zwycięskie komendy oraz nieudany transfer strategii startowej. |
| [native/](native/) i [native-summary.json](native-summary.json) | Oba nowe przejazdy Box2D, dzienniki FAST50, centralny rejestr świata i przeliczone dane. |
| [native-parity-and-style.json](native-parity-and-style.json) i [frozen/](frozen/) | Niezależne porównanie natywnych kroków H28-C, ostrzejsze flagi techniki i osobne regresje zamrożonych wariantów. |
| [validation.json](validation.json) | Wyniki kompilacji i testów, identyczne niezaliczenia historyczne w bazie oraz budżet natywny. |
| [diagnostic-summary.json](diagnostic-summary.json) i [manifest.json](manifest.json) | Zestawienia i sumy SHA-256 plików w postaci zapisanej w Git. |

Dane źródłowe obejmują [#203](../speedway203/), [#204](../speedway204/), [raport H27-P](../../../docs/experiments/speedway-h27/REPORT.md) i [raport H27-N](../../../docs/experiments/speedway-h27-n/REPORT.md). Nagranie [H27-N C](../speedway204/native/H27-N-learned-C.json.gz) jest wspólnym odniesieniem dla audytu gazu, większości dopasowanych checkpointów, certyfikatów i porównania prędkości.

## Odtworzenie

Z katalogu głównego repozytorium w PowerShell. Wyniki lokalne trafiają do katalogu tymczasowego. Audyt pełnego gazu trwa kilka minut. Wykresy wymagają Matplotlib. Polecenie uczenia można pominąć, korzystając z opublikowanych komend.

```powershell
$study205 = Join-Path ([IO.Path]::GetTempPath()) ('speedway205-' + [guid]::NewGuid())
$env:SPEEDWAY_BOX2D_NO_WORLDS = '1'
dotnet build tools/speedway-h26/H26.csproj -c Release --warnaserror
$h28 = 'tools/speedway-h26/bin/Release/net8.0/H26.dll'
dotnet $h28 model-205 . $study205
dotnet $h28 certify-205 . $study205
dotnet $h28 transients-205 . $study205
dotnet $h28 equilibria-205 . $study205
dotnet $h28 throttle-205 . $study205
dotnet $h28 changed . $study205 H28-C data/experiments/speedway203/H27-P-training.json.gz C
dotnet $h28 preflight-205 . $study205 "$study205/H28-C-training.json.gz"
dotnet $h28 native-check-203 . $study205 data/experiments/speedway205/native
python tools/speedway-h26/analyze205.py $study205 "$study205/transients-model.json.gz" "$study205/equilibria-model.json" "$study205/throttle-model.json.gz" data/experiments/speedway205/native "$study205/figures"
```

Odtworzenie danych i wykresów używa istniejących nagrań Box2D. Nową próbę uruchamia się oddzielnie, po sprawdzeniu wyniku preflight, poleceniem `dotnet $h28 native-205 . $study205 C "$study205/H28-C-training.json.gz"` lub z `C-hold-minus`. Wymaga zdjęcia blokady `SPEEDWAY_BOX2D_NO_WORLDS` oraz wskazania istniejącego, absolutnego rejestru konstrukcji pod `SPEEDWAY_BOX2D_LEDGER`. Czasy ścienne oraz PID zależą od hosta.
