param([Parameter(Mandatory=$true)][string]$Data,[Parameter(Mandatory=$true)][string]$Raw,[string]$Python='python')
$ErrorActionPreference='Stop'
Push-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
try {
    if(Test-Path -LiteralPath (Join-Path $Data 'protocol.json')){throw 'Refusing to overwrite frozen evidence.'}
    if((Test-Path -LiteralPath $Raw) -and (Get-ChildItem -LiteralPath $Raw -Force | Select-Object -First 1)){throw 'Raw directory must be empty.'}
    dotnet build tools/multi-action-planning -c Release --warnaserror
    if($LASTEXITCODE){throw 'Build failed.'}
    dotnet test tests/CoreSim.Tests -c Release --filter FullyQualifiedName~MultiActionPlanningTests
    if($LASTEXITCODE){throw 'Targeted tests failed; no worlds.'}
    foreach($stage135 in @('freeze','preflight','diagnostic','confirm')){
        dotnet tools/multi-action-planning/bin/Release/net8.0/multi-action-planning.dll --enable-multi-action $Data $Raw $stage135
        if($LASTEXITCODE -and $stage135 -eq 'confirm'){
            dotnet tools/multi-action-planning/bin/Release/net8.0/multi-action-planning.dll --enable-multi-action $Data $Raw finish-stopped-confirmation
            if($LASTEXITCODE){throw 'Actual queue audit failed. Preserve all evidence.'}
        } elseif($LASTEXITCODE){throw "Stopped at $stage135. Preserve evidence; never raise budget or tune on final results."}
    }
    dotnet tools/multi-action-planning/bin/Release/net8.0/multi-action-planning.dll --enable-multi-action $Data $Raw sequence-cost
    if($LASTEXITCODE){throw 'Cost audit failed.'}
    # These two explicitly post-hoc controls are not additional independent successes.
    & $Python -c "import gzip,pathlib,sys; p=pathlib.Path(sys.argv[1])/'confirm-search.json'; p.with_suffix('.json.gz').write_bytes(gzip.compress(p.read_bytes(),mtime=0))" $Data
    if($LASTEXITCODE){throw 'Cannot package already frozen search for mechanism control.'}
    dotnet tools/multi-action-planning/bin/Release/net8.0/multi-action-planning.dll --enable-multi-action $Data $Raw mechanism-controls
    if($LASTEXITCODE){throw 'Mechanism control failed.'}
    dotnet tools/multi-action-planning/bin/Release/net8.0/multi-action-planning.dll --enable-multi-action $Data $Raw geometry-transfer
    if($LASTEXITCODE){throw 'Fixed-plan geometry transfer failed.'}
    & $Python tools/multi-action-report.py $Raw --data $Data
    if($LASTEXITCODE){throw 'Report failed; all raw retained.'}
} finally {Pop-Location}
