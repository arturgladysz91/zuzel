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
        dotnet $main rider-decision $dataPath $rawPath $name
        if($LASTEXITCODE -ne 0){throw "Stopped at $name. Preserve the ledger and completed generations before resuming."}
    }
    Stage 'freeze'
    Copy-Item -LiteralPath 'docs/experiments/rider-decision-optimization/source-extensions.json' -Destination (Join-Path $dataPath 'source-extensions.json')
    # Explicitly replay the user-approved scheduling limit before this new run.
    # The original 225s stop, direct authorization and original source hashes
    # remain in the committed evidence; no training outcome is re-used here.
    $protocol=Get-Content -LiteralPath (Join-Path $dataPath 'protocol.json') -Raw | ConvertFrom-Json
    $sourceHash=$protocol.Sources.'RiderDecisionExperiment133.cs'
    @{
        Authorized=$true;Replay=$true;Authorization='Reproduction of the explicit user-approved 600s generation limit recorded in the original #133 budget-amendment.json.'
        OriginalGenerationCpuSeconds=225;GenerationCpuSeconds=600
        OriginalSourceSha256=$sourceHash;ApprovedSourceSha256=$sourceHash
        TrainingWorldsAtAmendment=0;FinalWorldsAtAmendment=0;ObservedCpuSeconds=0
        Unchanged=@{TotalCpuSeconds=7000;NativeWorlds=800;Forecasts=1500000;ContactEvaluations=135000000}
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $dataPath 'budget-amendment.json') -Encoding utf8
    dotnet test tests/CoreSim.Tests -c Release --no-build --filter 'FullyQualifiedName~RiderDecisionTests&FullyQualifiedName!~ArchivedCem&FullyQualifiedName!~FrozenSelection'
    if($LASTEXITCODE -ne 0){throw 'Targeted tests failed; no training.'}
    Stage 'audit'
    Stage 'preflight'
    Stage 'train'
    Stage 'select'
    Stage 'freeze-winner'
    Stage 'final'
    & $Python tools/rider-decision-report.py $rawPath --data $dataPath
    if($LASTEXITCODE -ne 0){throw 'Report failed; raw evidence preserved.'}
} finally {Pop-Location}
