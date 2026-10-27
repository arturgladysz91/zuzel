param([Parameter(Mandatory=$true)][string]$Evidence,[Parameter(Mandatory=$true)][string]$Raw,[string]$Python='python')
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Evidence){throw 'Use a fresh results path; never overwrite a frozen study'}
dotnet restore SpeedwayManager.sln
if($LASTEXITCODE){throw 'Restore failed'}
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
if($LASTEXITCODE){throw 'Build failed'}
$folder143=Split-Path -Parent $Evidence
New-Item -ItemType Directory -Force $folder143 | Out-Null
Copy-Item -LiteralPath 'docs/experiments/racing-constraints-speed/held-B-commitment.json' -Destination (Join-Path $folder143 'held-B-commitment.json')
foreach($stage in @('offline','freeze','preflight','isolated','combinations')){
    dotnet tools/racing-constraints/bin/Release/net8.0/racing-constraints.dll --enable-constraints143 $Evidence $Raw $stage
    if($LASTEXITCODE){throw "Stage failed: $stage. Preserve all reservations."}
}
& $Python tools/racing-constraints/extend-recovery.py $Evidence
if($LASTEXITCODE){throw 'Extension lock failed'}
dotnet tools/racing-constraints/bin/Release/net8.0/racing-constraints.dll --enable-constraints143 $Evidence $Raw combinations
if($LASTEXITCODE){throw 'Recovery combination experiment failed'}
& $Python tools/racing-constraints/lock-held.py $Evidence
if($LASTEXITCODE){throw 'Held lock failed'}
foreach($stage in @('confirmation','diagnostics','benchmark')){
    dotnet tools/racing-constraints/bin/Release/net8.0/racing-constraints.dll --enable-constraints143 $Evidence $Raw $stage
    if($LASTEXITCODE){throw "Stage failed: $stage. Do not repeat a native reservation."}
}
dotnet tools/constraints-analysis143/bin/Release/net8.0/constraints-analysis143.dll --compare-native-prefixes $Evidence $Raw
if($LASTEXITCODE){throw 'Read-only predictor/native verification failed'}
& $Python tools/racing-constraints/report143.py $Evidence $Raw
if($LASTEXITCODE){throw 'Read-only report failed'}
