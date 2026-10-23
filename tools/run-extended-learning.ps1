param(
    [Parameter(Mandatory=$true)][string]$Data,
    [Parameter(Mandatory=$true)][string]$Raw,
    [string]$Python = 'python'
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repository
try {
    $dataPath = [IO.Path]::GetFullPath($Data)
    $rawPath = [IO.Path]::GetFullPath($Raw)
    if (Test-Path -LiteralPath (Join-Path $dataPath 'protocol.json')) { throw 'Refusing to overwrite a frozen experiment.' }
    if ((Test-Path -LiteralPath $rawPath) -and (Get-ChildItem -LiteralPath $rawPath -Force | Select-Object -First 1)) { throw 'Raw directory must be empty.' }
    dotnet build tools/extended-learning-audit -c Release --warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $main = 'tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll'
    $audit = 'tools/extended-learning-audit/bin/Release/net8.0/extended-learning-audit.dll'
    function MainStage([string]$stage) {
        dotnet $main extended-learning $dataPath $rawPath $stage
        if ($LASTEXITCODE -ne 0) { throw "Experiment stopped at $stage." }
    }
    function AuditStage([string]$stage) {
        dotnet $audit $dataPath $rawPath $stage
        if ($LASTEXITCODE -ne 0) { throw "Predictor/action audit stopped at $stage." }
    }
    MainStage 'freeze'
    $protocolPath = Join-Path $dataPath 'protocol.json'
    Copy-Item -LiteralPath $protocolPath -Destination (Join-Path $dataPath 'protocol-before-line-ending-correction.json')
    $protocolHash = (Get-FileHash -LiteralPath $protocolPath).Hash
    $protocol = Get-Content -LiteralPath $protocolPath -Raw | ConvertFrom-Json
    @{Reason='Fresh reproduction uses the final audited checkout; no line-ending amendment needed.';OriginalProtocolSha256=$protocolHash;CorrectedProtocolSha256=$protocolHash;OldSources=$protocol.Sources;NewSources=$protocol.Sources} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $dataPath 'line-ending-correction.json') -Encoding utf8
    MainStage 'audit'
    MainStage 'ab'
    $sourceHash = (Get-FileHash tools/extended-learning-audit/Program.cs).Hash
    @{HorizonSeconds=.9;Origins=@(8.4,9.2,9.6,9.8,10.0);SteeringDegrees=@(-35,-25,-15,-5,0,5,15,25,35);Drives=@(0,'frozen130Drive',.8,1);MaxForecasts=180;SelectionForbidden=$true;SourceSha256=$sourceHash} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $dataPath 'action-availability-protocol.json') -Encoding utf8
    Copy-Item -LiteralPath tools/extended-learning-audit/Program.cs -Destination (Join-Path $dataPath 'action-availability-source.cs.txt')
    AuditStage 'availability'
    MainStage 'select-ab'
    @{Origin=9.6;HorizonSeconds=.9;SteeringDegrees=35;Drive=0;Tolerances='Unchanged frozen protocol, mandatory0.9s native endpoint';SourceSha256=$sourceHash} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $dataPath 'high-slip-protocol.json') -Encoding utf8
    Copy-Item -LiteralPath tools/extended-learning-audit/Program.cs -Destination (Join-Path $dataPath 'high-slip-source.cs.txt')
    AuditStage 'high-slip'
    MainStage 'two'
    MainStage 'freeze-winner'
    MainStage 'final'
    AuditStage 'h06-parity'
    Copy-Item -LiteralPath tools/extended-learning-audit/Program.cs -Destination (Join-Path $dataPath 'full-gas-source.cs.txt')
    @{Origin=8.4;HorizonSeconds=.9;SteeringDegrees=0;Drive=1;Purpose='Auxiliary native diagnostic after final freeze';SourceSha256=$sourceHash;SourceSnapshotReconstructed=$false} |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $dataPath 'full-gas-provenance.json') -Encoding utf8
    AuditStage 'full-gas'
    & $Python tools/extended-learning-report.py $rawPath --data $dataPath
    if ($LASTEXITCODE -ne 0) { throw 'Report failed; raw and frozen outcome preserved.' }
} finally { Pop-Location }
