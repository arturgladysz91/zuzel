param(
    [Parameter(Mandatory=$true)][string]$Data,
    [Parameter(Mandatory=$true)][string]$Raw,
    [string]$Python='python'
)
$ErrorActionPreference='Stop'
$repository=Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repository
try {
    $dataPath=[IO.Path]::GetFullPath($Data)
    $rawPath=[IO.Path]::GetFullPath($Raw)
    if(Test-Path -LiteralPath (Join-Path $dataPath 'protocol.json')){throw 'Refusing to overwrite frozen evidence.'}
    if((Test-Path -LiteralPath $rawPath) -and (Get-ChildItem -LiteralPath $rawPath -Force | Select-Object -First 1)){throw 'Raw directory must be empty.'}
    dotnet build SpeedwayManager.sln -c Release --warnaserror
    if($LASTEXITCODE -ne 0){throw 'Build failed.'}
    $main='tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll'
    function Stage([string]$name){
        dotnet $main track-awareness $dataPath $rawPath $name
        if($LASTEXITCODE -ne 0){throw "Stopped at $name. Preserve ledger and raw before resuming. Do not change frozen scientific sources or budgets."}
    }
    Stage 'freeze'
    Copy-Item -LiteralPath 'docs/experiments/track-awareness-anticipatory-riding/source-extensions.json' -Destination (Join-Path $dataPath 'source-extensions.json')
    dotnet test tests/CoreSim.Tests -c Release --no-build --filter 'FullyQualifiedName~TrackAwarenessTests&FullyQualifiedName!~ArchivedCem&FullyQualifiedName!~FrozenSelection'
    if($LASTEXITCODE -ne 0){throw 'Targeted tests failed; no new worlds.'}
    Stage 'geometry'
    Stage 'audit'
    Stage 'preflight'
    Stage 'train'
    Stage 'select'
    Stage 'freeze-winners'
    Stage 'final'
    & $Python tools/track-awareness-report.py $rawPath --data $dataPath
    if($LASTEXITCODE -ne 0){throw 'Report failed; raw evidence preserved.'}
} finally {Pop-Location}
