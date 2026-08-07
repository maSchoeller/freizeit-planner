[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    & "$PSScriptRoot/bootstrap.ps1"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm exec --yes pnpm@11.20.0 -- build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project src/FreizeitCockpit.AppHost/FreizeitCockpit.AppHost.csproj --no-restore
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
