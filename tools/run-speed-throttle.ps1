param([Parameter(Mandatory=$true)][string]$Evidence,[Parameter(Mandatory=$true)][string]$Raw,[string]$Python='python')
$ErrorActionPreference='Stop'
if((Test-Path -LiteralPath $Evidence) -or (Test-Path -LiteralPath $Raw)){throw 'Choose fresh directories; never overwrite native evidence.'}
dotnet restore SpeedwayManager.sln
if($LASTEXITCODE){throw 'Restore failed'}
dotnet build SpeedwayManager.sln -c Release --no-restore --warnaserror
if($LASTEXITCODE){throw 'Build failed'}
foreach($stage in @('freeze','preflight','diagnostic','confirm')){
    dotnet tools/speed-throttle-root-cause/bin/Release/net8.0/speed-throttle-root-cause.dll --enable-speed-throttle $Evidence $Raw $stage
    if($LASTEXITCODE){throw "Stage failed: $stage"}
}
& $Python tools/speed-throttle-report.py $Raw $Evidence $Evidence
if($LASTEXITCODE){throw 'Report failed; native evidence retained'}
