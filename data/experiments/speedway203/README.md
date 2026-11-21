# Dane badania #203

[Raport](../../../docs/experiments/speedway-h27/REPORT.md) opisuje inwarianty, porównania oraz wszystkie nieukończenia.

- `*-model.json.gz` z nazwami H25/H26 zawierają wszystkie przyjęte kroki z ośmiu nagrań #202 oraz krótkie kontrfaktyczne testy gazu na ich review.
- `reachability-model.json.gz`, `posture-sensitivity-model.json.gz` i `load-residual-model.json` zawierają mapy Fx/Fy/yaw, próby geometrii mas i próbki solvera. `holdout-solver-model.json` odtwarza błąd zbieżności dodatnich nacisków z próby +2 cm.
- `H27-P-training.json.gz` i `H27-P-training-C-alt.json.gz` zawierają wyniki dwóch osobnych poszukiwań MODEL, kandydatów, czasy i komendy zwycięzców. `H27-P-transfer*.json.gz` zapisują próbę przeniesienia propozycji H26-LE.
- `native/` zawiera osiem oryginalnych zapisów Box2D i wyniki. `native-summary.json` ma metryki faz, gazu, CPU i reszt. `native-parity-and-style.json` odróżnia parytet przeliczeń od testu techniki.
- `world-ledger.jsonl` zawiera wszystkie konstrukcje. Liczba rezerwacji z próby licznika występuje osobno w `posture-check-model.json`. W teście rezerwacji nie wywoływano Box2D.

## Odtwarzanie bez Box2D

Polecenia PowerShell uruchamiane z katalogu głównego repozytorium. Ścieżki wyjściowe muszą wskazywać świeży katalog.

```powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS = '1'
dotnet restore tools/speedway-h26/H26.csproj --locked-mode
dotnet build tools/speedway-h26/H26.csproj -c Release --no-restore
$h27Tool = 'tools/speedway-h26/bin/Release/net8.0/H26.dll'
$h27Data = 'data/experiments/speedway203'
dotnet $h27Tool audit-203 . work-h27/model
dotnet $h27Tool posture-check-203 . work-h27/posture
dotnet $h27Tool check-policies . work-h27/frozen
dotnet $h27Tool native-check-203 . work-h27/parity "$h27Data/native"
dotnet $h27Tool holdout-solver-203 . work-h27/solver "$h27Data/native/H27-P-learned-C-hold-plus.json.gz" "$h27Data/H27-P-training.json.gz"
dotnet $h27Tool changed . work-h27/train H27-P data/experiments/speedway202/cycle-v8/H26-LE-training.json.gz C
dotnet $h27Tool changed . work-h27/train-alt H27-P data/experiments/speedway202/alt-v10/H26-LE-training.json.gz C-alt
dotnet test tests/CoreSim.Tests/CoreSim.Tests.csproj -c Release --filter 'FullyQualifiedName~H26PhysicsTests|FullyQualifiedName~TrackGeometryTests|FullyQualifiedName~PhysicalTrackWidthGeometryTests'
python tools/speedway-h26/analyze203.py work-h27/model "$h27Data/native" work-h27/figures
```

Skrypt figur wymaga Pythona 3.12 i Matplotlib. Wykorzystuje arkusz geometrii z replay #202. Przy podanym filtrze testy i wszystkie powyższe polecenia działają z blokadą tworzenia światów.

## Konstrukcje w wykonanym badaniu

Polecenia `native-203-same` i `native-203-learned` wymagają ścieżki absolutnej w `SPEEDWAY_BOX2D_LEDGER` do jednego istniejącego ledgeru. Każdy proces testów lub narzędzi budujący Box2D w ramach badania dziedziczy tę samą ścieżkę. Zużyty ledger #203 ma osiem rezerwacji i odrzuca następną konstrukcję. Rozdzielone przebiegi miały poniższą kolejność. Wszystkie dane wynikowe są już w repozytorium.

| Slot | Tryb | Wariant i wejście |
|---:|---|---|
| 1 | `native-203-same` | H25, H25-same |
| 2 | `native-203-same` | H27-P, H25-same |
| 3 | `native-203-same` | H27-P, H26-LE-learned-C |
| 4 | `native-203-learned` | H26-LE, cycle-v8, C |
| 5 | `native-203-learned` | H27-P, H27-P-training.json.gz, C |
| 6 | `native-203-learned` | H27-P, H27-P-training-C-alt.json.gz, C-alt |
| 7 | `native-203-learned` | H27-P, H27-P-training.json.gz, C-hold-minus |
| 8 | `native-203-learned` | H27-P, H27-P-training.json.gz, C-hold-plus |
