[CmdletBinding()]
param([switch]$NoRestore)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    & "$PSScriptRoot/test-foundation.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if (-not $NoRestore) { & "$PSScriptRoot/bootstrap.ps1" }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet test FreizeitCockpit.slnx --no-restore --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm exec --yes pnpm@11.20.0 -- test
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
