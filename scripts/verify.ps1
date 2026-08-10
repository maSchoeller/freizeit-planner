[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
Push-Location $repositoryRoot
try {
    & "$PSScriptRoot/bootstrap.ps1"
    & "$PSScriptRoot/verify-generated.ps1"
    dotnet format FreizeitCockpit.slnx --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet format failed with exit code $LASTEXITCODE." }
    dotnet build FreizeitCockpit.slnx --no-restore --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }
    npm exec --yes pnpm@11.20.0 -- format:check
    if ($LASTEXITCODE -ne 0) { throw "Prettier failed with exit code $LASTEXITCODE." }
    npm exec --yes pnpm@11.20.0 -- lint
    if ($LASTEXITCODE -ne 0) { throw "ESLint failed with exit code $LASTEXITCODE." }
    npm exec --yes pnpm@11.20.0 -- typecheck
    if ($LASTEXITCODE -ne 0) { throw "TypeScript failed with exit code $LASTEXITCODE." }
    & "$PSScriptRoot/test-coverage.ps1" -NoRestore
    dotnet test tests/Aspire.Tests/Aspire.Tests.csproj --no-restore --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Aspire-Integrationstest ist mit Exitcode $LASTEXITCODE fehlgeschlagen." }
    npm exec --yes pnpm@11.20.0 -- build
    if ($LASTEXITCODE -ne 0) { throw "Frontend/help build failed with exit code $LASTEXITCODE." }
    & "$PSScriptRoot/test-browser.ps1"
    git diff --check
    if ($LASTEXITCODE -ne 0) { throw "git diff --check failed with exit code $LASTEXITCODE." }
    $stopwatch.Stop()
    Write-Host "Verify erfolgreich in $([math]::Round($stopwatch.Elapsed.TotalSeconds, 1)) Sekunden."
}
finally {
    Pop-Location
}
