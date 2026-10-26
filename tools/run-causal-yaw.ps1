param([Parameter(Mandatory=$true)][string]$Evidence,[Parameter(Mandatory=$true)][string]$Raw,[string]$Python='python')
$ErrorActionPreference='Stop'
if((Test-Path -LiteralPath $Evidence) -or (Test-Path -LiteralPath $Raw)){throw 'Choose fresh directories; preserve frozen experiments.'}
dotnet restore SpeedwayManager.sln
if($LASTEXITCODE){throw 'Restore failed'}
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
if($LASTEXITCODE){throw 'Build failed'}
foreach($stage in @('freeze','preflight','controlled','selection','confirm')){
    if($stage -eq 'confirm'){
        Copy-Item -LiteralPath 'docs/experiments/causal-yaw-throttle/evaluation-definition.json' -Destination (Join-Path $Evidence 'evaluation-definition.json')
    }
    dotnet tools/causal-yaw-throttle/bin/Release/net8.0/causal-yaw-throttle.dll --enable-causal-yaw $Evidence $Raw $stage
    if($LASTEXITCODE){throw "Stage failed: $stage. Preserve raw; do not retry native reservations."}
}
dotnet tools/causal-yaw-analysis/bin/Release/net8.0/causal-yaw-analysis.dll --read-native $Raw $Evidence
if($LASTEXITCODE){throw 'Read-only metrics failed'}
& $Python tools/causal-yaw-report.py $Evidence $Raw
if($LASTEXITCODE){throw 'Report failed'}
