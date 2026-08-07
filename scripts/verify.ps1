[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
Push-Location $repositoryRoot
try {
    & "$PSScriptRoot/bootstrap.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & "$PSScriptRoot/verify-generated.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet format FreizeitCockpit.slnx --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet build FreizeitCockpit.slnx --no-restore --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm exec --yes pnpm@11.20.0 -- format:check
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm exec --yes pnpm@11.20.0 -- lint
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm exec --yes pnpm@11.20.0 -- typecheck
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & "$PSScriptRoot/test.ps1" -NoRestore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm exec --yes pnpm@11.20.0 -- build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    git diff --check
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $stopwatch.Stop()
    Write-Host "Verify erfolgreich in $([math]::Round($stopwatch.Elapsed.TotalSeconds, 1)) Sekunden."
}
finally {
    Pop-Location
}
