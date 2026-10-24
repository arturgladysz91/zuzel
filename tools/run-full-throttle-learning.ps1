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
    dotnet build tools/full-throttle-audit -c Release --warnaserror
    if($LASTEXITCODE -ne 0){throw 'Audit build failed.'}
    $main='tools/shared-world-speedway/bin/Release/net8.0/shared-world-speedway.dll'
    function Stage([string]$name){
        dotnet $main full-throttle-learning $dataPath $rawPath $name
        if($LASTEXITCODE -ne 0){throw "Stopped at $name."}
    }
    Stage 'freeze'
    dotnet test tests/CoreSim.Tests -c Release --no-build --filter 'FullyQualifiedName~FullThrottleLearningTests&FullyQualifiedName!~ArchivedCem&FullyQualifiedName!~LegalTwoBend'
    if($LASTEXITCODE -ne 0){throw 'Targeted tests failed; no training.'}
    Stage 'diagnose'
    # Preserve the initial exact-event timestamp failure; an event need not occur at
    # the identical microsecond in the numerical approximation. All regular native
    # 10ms points and the0.9s endpoint remain mandatory, same physical tolerances.
    @{
        Reason='Compare all91 regular10ms points and endpoint. Retain independently quantized event timestamps without interpolation; physical tolerances unchanged.'
        SourceSha256=(Get-FileHash tools/full-throttle-audit/Program.cs).Hash
        InitialPredictorSha256=(Get-FileHash (Join-Path $dataPath 'predictor.json')).Hash
        TrainingWorlds=0;FinalWorlds=0
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dataPath 'predictor-alignment-protocol.json') -Encoding utf8
    dotnet tools/full-throttle-audit/bin/Release/net8.0/full-throttle-audit.dll $dataPath $rawPath
    if($LASTEXITCODE -ne 0){throw 'Predictor audit failed.'}
    Stage 'train'
    Stage 'select'
    $decision=Get-Content -LiteralPath (Join-Path $dataPath 'continuation-decision.json') -Raw | ConvertFrom-Json
    $ledger=Get-Content -LiteralPath (Join-Path $dataPath 'attempts.json') -Raw | ConvertFrom-Json
    $cpuUsed=($ledger | Measure-Object -Property CpuSeconds -Sum).Sum
    $contactsUsed=($ledger | Measure-Object -Property Evaluations -Sum).Sum
    $forecastsUsed=($ledger | Measure-Object -Property Forecasts -Sum).Sum
    if($decision.Extend -and ($cpuUsed -gt 4200 -or $contactsUsed -gt 70000000 -or $forecastsUsed -gt 840000)){
        # Honor the experiment's existing reserve guard before creating another world.
        @{Reason='Frozen independent evaluation reserve reached after selection; no continuation world started.';CpuSeconds=$cpuUsed;ContactEvaluations=$contactsUsed;Forecasts=$forecastsUsed;FinalWorlds=0} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dataPath 'continuation-stop.json') -Encoding utf8
        @{Worlds=($ledger | Where-Object {$_.Category -in @('train-ab','continuation')}).Count;FinalWorlds=0;Frozen=$true;ResourceStop=$true} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dataPath 'training-complete.json') -Encoding utf8
    } else {Stage 'continue'}
    Stage 'freeze-winner'
    Stage 'final'
    & $Python tools/full-throttle-report.py $rawPath --data $dataPath
    if($LASTEXITCODE -ne 0){throw 'Report failed; raw preserved.'}
} finally {Pop-Location}
