param([Parameter(Mandatory=$true)][string]$Evidence,[Parameter(Mandatory=$true)][string]$Raw,[string]$Python='python')
$ErrorActionPreference='Stop'
if((Test-Path -LiteralPath $Evidence) -or (Test-Path -LiteralPath $Raw)){throw 'Fresh paths required. Preserve reservations; never retry incomplete worlds.'}
dotnet restore SpeedwayManager.sln
if($LASTEXITCODE){throw 'Restore failed'}
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
if($LASTEXITCODE){throw 'Build failed'}
foreach($stage in @('freeze','offline','preflight','diagnostic','selection')){
    dotnet tools/throttle-steering-decoupling/bin/Release/net8.0/throttle-steering-decoupling.dll --enable-throttle-steering-decoupling $Evidence $Raw $stage
    if($LASTEXITCODE){throw "Preserve evidence after failed $stage; do not repeat reservations."}
}
& $Python tools/throttle-steering-report.py $Evidence $Raw lock
if($LASTEXITCODE){throw 'Lock failed'}
$selection=Get-Content -LiteralPath (Join-Path $Evidence 'selection-lock.json') -Raw | ConvertFrom-Json
if($selection.PrototypeGate){throw 'Repeatable maneuvers require a separately frozen prototype before confirmation.'}
dotnet tools/throttle-steering-decoupling/bin/Release/net8.0/throttle-steering-decoupling.dll --enable-throttle-steering-decoupling $Evidence $Raw confirmation
if($LASTEXITCODE){throw 'Confirmation failed; preserve reservations'}
dotnet tools/throttle-steering-transfer/bin/Release/net8.0/throttle-steering-transfer.dll --enable-transfer138 $Evidence $Raw freeze
if($LASTEXITCODE){throw 'Transfer freeze failed'}
dotnet tools/throttle-steering-transfer/bin/Release/net8.0/throttle-steering-transfer.dll --enable-transfer138 $Evidence $Raw run
if($LASTEXITCODE){throw 'Transfer failed; preserve reservations'}
dotnet tools/causal-yaw-analysis/bin/Release/net8.0/causal-yaw-analysis.dll --read-native $Raw $Evidence
if($LASTEXITCODE){throw 'Read-only native metrics failed'}
& $Python tools/throttle-steering-report.py $Evidence $Raw report
if($LASTEXITCODE){throw 'Reporting failed'}
