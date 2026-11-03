$ErrorActionPreference='Stop'
$repo162=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
New-Item -ItemType Directory -Force -Path (Join-Path $repo162 'results/s3-performance') | Out-Null
$doc162=Join-Path $repo162 'docs/experiments/s3-performance'
$dll162=Join-Path $repo162 'tools/s3-performance/bin/Release/net8.0/s3-performance.dll'
function Read-Rows162($file162) { foreach ($row162 in (Get-Content -LiteralPath $file162 -Raw | ConvertFrom-Json)) { $row162 } }
while (-not (Test-Path -LiteralPath (Join-Path $doc162 'native/native-dev-result.json'))) {
 if (Test-Path -LiteralPath (Join-Path $doc162 'series-closure.json')) { Write-Output 'Closed after development DNF'; exit 1 }; Start-Sleep -Seconds 30
}
$dev162=Get-Content -LiteralPath (Join-Path $doc162 'native/native-dev-result.json') -Raw | ConvertFrom-Json
if (-not $dev162.Pass -or $dev162.ChargedCpuSeconds -ge $dev162.BaselineCpuSeconds) { Write-Output 'No successful lower-cost development; no held starts'; exit 1 }
while (@(Read-Rows162 (Join-Path $doc162 'budget-journal.json') | Where-Object { -not $_.Complete }).Count -gt 0) { Start-Sleep -Seconds 2 }
$devFullCpu162=(Read-Rows162 (Join-Path $doc162 'budget-journal.json') | Where-Object Label -eq 'native-dev').CpuSeconds
$baseFullCpu162=(Read-Rows162 (Join-Path $repo162 'docs/experiments/trajectory-continuation/budget-journal.json') | Where-Object Label -eq 'native-dev').CpuSeconds
if ($devFullCpu162 -ge $baseFullCpu162) { Write-Output "Full operation CPU has no improvement: $devFullCpu162 >= $baseFullCpu162; no held starts"; exit 1 }
& dotnet $dll162 --freeze
if ($LASTEXITCODE -ne 0) { exit 1 }
foreach ($i162 in 0,1,2) {
 $p162=Start-Process -FilePath dotnet -ArgumentList @($dll162,'--validate',[string]$i162,"held-$i162") -WorkingDirectory $repo162 -WindowStyle Hidden -RedirectStandardOutput "$repo162/results/s3-performance/held-$i162.log" -RedirectStandardError "$repo162/results/s3-performance/held-$i162.err.log" -PassThru
 Write-Output "Started held-$i162 PID=$($p162.Id)"
}
while ($true) {
 if (Test-Path -LiteralPath (Join-Path $doc162 'series-closure.json')) { Write-Output 'Native DNF closes remaining starts'; exit 1 }
 $complete162=@(0,1,2 | Where-Object { Test-Path -LiteralPath (Join-Path $doc162 "native/held-$_-result.json") })
 if ($complete162.Count -eq 3) { break }; Start-Sleep -Seconds 30
}
foreach ($i162 in 0,1,2) { $r162=Get-Content -LiteralPath (Join-Path $doc162 "native/held-$i162-result.json") -Raw | ConvertFrom-Json; if (-not $r162.Pass) { exit 1 } }
while (@(Read-Rows162 (Join-Path $doc162 'budget-journal.json') | Where-Object { -not $_.Complete }).Count -gt 0) { Start-Sleep -Seconds 2 }
foreach ($i162 in 3,4) {
 $p162=Start-Process -FilePath dotnet -ArgumentList @($dll162,'--validate',[string]$i162,"held-$i162") -WorkingDirectory $repo162 -WindowStyle Hidden -RedirectStandardOutput "$repo162/results/s3-performance/held-$i162.log" -RedirectStandardError "$repo162/results/s3-performance/held-$i162.err.log" -PassThru
 Write-Output "Started held-$i162 PID=$($p162.Id)"
}
