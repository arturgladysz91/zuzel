# Dane badania #204

[Raport H27-N](../../../docs/experiments/speedway-h27-n/REPORT.md) opisuje reprodukcję P0, zmiany solvera oraz przebieg prób.

- `archive-model.json` ma liczniki odtworzeń wszystkich przyjętych kroków #202 i #203, reszty oraz wyniki skanów. `targeted-scenarios.json` zachowuje stany i wyniki testów szczególnych. `p0-residual.json` zawiera oba sąsiednie argumenty double oraz przebieg funkcji starej i nowej. `legacy-p0-reproduction.json` pochodzi z uruchomienia kodu #203 przed zmianą.
- `predictor-timings.json` rozdziela koszt sił, solvera i pełnej prognozy. Zawiera również pełne preflight tej samej polityki w pięciu warunkach dla H27-P i H27-N.
- `throttle-pilot-model.json.gz` zachowuje pierwszą próbę, w tym ujawniony limit iteracji. `throttle-model.json.gz` ma ostateczne porównania na identycznych review, oryginalną kolejkę pending, ocenę bramek, odcinki fazowe i czas do wspólnego celu. `throttle-summary.json` agreguje wyniki.
- `native-pilot/` ma pierwsze cztery nagrania i wynik odrzuconego preflight −12 cm. `native/` ma trzy powtórzenia po poprawce walidacji skrętu. `native-summary.json` i `native-parity-and-style.json` obejmują weryfikację końcowych przejazdów, fazy, rozbieżności, bilans i technikę. `world-ledger.jsonl` to wspólny rejestr dla obu serii.
- `validation.json` zapisuje zakres testów, wersję polityki i stan CI. `manifest.json` sumuje SHA-256 publikowanych dowodów.

## Odtworzenie bez konstrukcji Box2D

Polecenia PowerShell z katalogu głównego. Wyniki należy kierować do świeżego katalogu.

```powershell
$env:SPEEDWAY_BOX2D_NO_WORLDS = '1'
dotnet restore tools/speedway-h26/H26.csproj --locked-mode -p:NuGetAudit=false
dotnet build tools/speedway-h26/H26.csproj -c Release --no-restore
$h27n = 'tools/speedway-h26/bin/Release/net8.0/H26.dll'
$published204 = 'data/experiments/speedway204'
dotnet $h27n model-204 . work-h27n/model
dotnet $h27n check-policies . work-h27n/frozen
dotnet $h27n native-check-203 . work-h27n/parity "$published204/native"
dotnet $h27n throttle-204 . work-h27n/throttle "$published204/native/H27-N-learned-C.json.gz"
dotnet test tests/CoreSim.Tests/CoreSim.Tests.csproj -c Release --filter 'FullyQualifiedName~H26PhysicsTests|FullyQualifiedName~ContactLoadSolver64Tests|FullyQualifiedName~TrackGeometryTests|FullyQualifiedName~PhysicalTrackWidthGeometryTests'
python tools/speedway-h26/analyze204.py work-h27n/model "$published204/native" work-h27n/throttle/throttle-model.json.gz work-h27n/figures
```

Do wykresu użyto Pythona 3.12 i Matplotlib. Zablokowanie światów obejmuje proces testów. Polecenie `native-204` korzysta wyłącznie z nominalnych komend H27-P z #203 i wymaga wspólnego absolutnego ledgeru. Opublikowane próby wykorzystały 7 z 8 konstrukcji, wliczając niepowodzenia. Dodatkowy slot pozostał niewykorzystany po trzech dozwolonych powtórzeniach. Wyłączenie blokady MODEL oraz każde ewentualne ponowne uruchomienie natywne trzeba rozliczyć w budżecie właściwego eksperymentu.
