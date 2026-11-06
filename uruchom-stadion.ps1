[CmdletBinding()]
param(
    [ValidateRange(0, 65535)][int]$Port = 0,
    [switch]$NoBrowser,
    [string]$InfoFile
)
$ErrorActionPreference = 'Stop'
try {
    $server = Join-Path $PSScriptRoot 'tools/replay-viewer/serve.py'
    if (-not (Test-Path -LiteralPath $server)) { throw 'Brak plików odtwarzacza. Uruchom skrypt z pełnej kopii repozytorium.' }
    $pythonCommand = $null
    foreach ($name in @('py', 'python', 'python3')) {
        $candidate = Get-Command $name -ErrorAction SilentlyContinue
        if (-not $candidate) { continue }
        if ($candidate.Source -like '*WindowsApps*') { continue }
        $prefix = @()
        if ($name -eq 'py') { $prefix = @('-3') }
        & $candidate.Source @prefix -c 'import sys; sys.exit(0 if sys.version_info >= (3, 8) else 1)' 2>$null
        if ($LASTEXITCODE -eq 0) { $pythonCommand = $candidate.Source; $pythonPrefix = $prefix; break }
    }
    if (-not $pythonCommand) { throw 'Nie znaleziono Python 3.8+. Zainstaluj Python 3 lub udostępnij polecenie py/python w PATH.' }
    $serverArguments = @('-X', 'utf8', '-u', $server, '--port', [string]$Port)
    if ($NoBrowser) { $serverArguments += '--no-browser' }
    if ($InfoFile) { $serverArguments += @('--info-file', $InfoFile) }
    & $pythonCommand @pythonPrefix @serverArguments
    if ($LASTEXITCODE -ne 0) { throw "Serwer zakończył pracę z kodem $LASTEXITCODE." }
} catch {
    Write-Error "Nie można uruchomić stadionu: $($_.Exception.Message)" -ErrorAction Continue
    exit 1
}
