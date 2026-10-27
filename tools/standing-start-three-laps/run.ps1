param([Parameter(Mandatory=$true)][string]$Evidence,[string]$Python='python')
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Evidence){throw 'Use a fresh output path. Preserve every previous attempt.'}
$native144=Join-Path $Evidence 'native'
dotnet restore SpeedwayManager.sln
if($LASTEXITCODE){throw 'Restore failed'}
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
if($LASTEXITCODE){throw 'Build failed'}
foreach($variant144 in @('A','B','F','K1','A-repeat')){
    dotnet tools/standing-start-three-laps/bin/Release/net8.0/standing-start-three-laps.dll --enable-standing144 $native144 $variant144
    if($LASTEXITCODE){throw "Native attempt incomplete: $variant144. Preserve reservation and raw data."}
}
dotnet tools/standing144-validation/bin/Release/net8.0/standing144-validation.dll --check-prefixes $native144
if($LASTEXITCODE){throw 'Predictor validation failed'}
dotnet tools/standing144-validation/bin/Release/net8.0/standing144-validation.dll --shared-world $native144
if($LASTEXITCODE){throw 'Shared attempt failed; preserve its lock and raw data'}
& $Python tools/standing-start-three-laps/report144.py --native $native144 --output $Evidence
if($LASTEXITCODE){throw 'Read-only report failed; native evidence remains preserved'}
