param([Parameter(Mandatory=$true)][string]$Evidence)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Evidence){throw 'Use a new evidence directory; preserve prior attempts.'}
python tools/corner-speed-loss/generate.py --check
if($LASTEXITCODE){throw 'Generated native collector differs.'}
dotnet build tools/corner-speed-loss/corner-speed-loss.csproj -c Release --warnaserror
if($LASTEXITCODE){throw 'Build failed.'}
python tools/corner-speed-loss/analyze145.py --output $Evidence
if($LASTEXITCODE){throw 'Saved analysis failed.'}
$taskNative=Join-Path $Evidence native
dotnet tools/corner-speed-loss/bin/Release/net8.0/corner-speed-loss.dll --audit-saved $taskNative
if($LASTEXITCODE){throw 'Score audit failed.'}
dotnet tools/corner-speed-loss/bin/Release/net8.0/corner-speed-loss.dll --audit-all-saved $taskNative
if($LASTEXITCODE){throw 'Exhaustive score reconstruction failed; no new worlds may start.'}
foreach($taskTreatments in @('initial','autonomous','followup','exact-grid')){
  dotnet tools/corner-speed-loss/bin/Release/net8.0/corner-speed-loss.dll --enable-native145 $taskNative "docs/experiments/corner-speed-loss/treatments-$taskTreatments.json"
  if($LASTEXITCODE){throw "Native study stopped in $taskTreatments; preserve its ledger."}
}
python tools/corner-speed-loss/analyze145.py --output $Evidence --native $taskNative
if($LASTEXITCODE){throw 'Native analysis failed.'}
