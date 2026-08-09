[CmdletBinding()]
param([switch]$NoRestore)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    & "$PSScriptRoot/test-foundation.ps1"
    if (-not $NoRestore) { & "$PSScriptRoot/bootstrap.ps1" }
    dotnet test FreizeitCockpit.slnx --no-restore --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE." }
    & "$PSScriptRoot/test-rls.ps1"
    & "$PSScriptRoot/test-cleanup.ps1"
    npm exec --yes pnpm@11.20.0 -- test
    if ($LASTEXITCODE -ne 0) { throw "Frontend tests failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}
