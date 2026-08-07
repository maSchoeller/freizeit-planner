[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    & "$PSScriptRoot/bootstrap.ps1"
    npm exec --yes pnpm@11.20.0 -- build
    if ($LASTEXITCODE -ne 0) { throw "Frontend/help build failed with exit code $LASTEXITCODE." }
    dotnet run --project src/FreizeitCockpit.AppHost/FreizeitCockpit.AppHost.csproj --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Aspire AppHost failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}
