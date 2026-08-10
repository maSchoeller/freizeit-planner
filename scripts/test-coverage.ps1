[CmdletBinding()]
param([switch]$NoRestore)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$coverageRoot = Join-Path $repositoryRoot '.artifacts/coverage'
$backendResults = Join-Path $coverageRoot 'backend'
$backendReport = Join-Path $coverageRoot 'backend-merged'

function Assert-WithinWorkspace([string]$Path) {
    $resolvedRoot = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith("$resolvedRoot$([System.IO.Path]::DirectorySeparatorChar)", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Coverage-Pfad liegt außerhalb des Arbeitsbereichs: $resolvedPath"
    }
}

Push-Location $repositoryRoot
try {
    & "$PSScriptRoot/test-foundation.ps1"
    if (-not $NoRestore) { & "$PSScriptRoot/bootstrap.ps1" }

    Assert-WithinWorkspace $coverageRoot
    if (Test-Path -LiteralPath $coverageRoot) {
        Remove-Item -LiteralPath $coverageRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $backendResults | Out-Null

    dotnet test FreizeitCockpit.slnx `
        --no-restore `
        --configuration Release `
        --filter 'Category!=Aspire' `
        --collect:'XPlat Code Coverage' `
        --results-directory $backendResults
    if ($LASTEXITCODE -ne 0) { throw "Backend-Tests mit Coverage sind mit Exitcode $LASTEXITCODE fehlgeschlagen." }

    dotnet reportgenerator `
        "-reports:$backendResults/**/coverage.cobertura.xml" `
        "-targetdir:$backendReport" `
        '-reporttypes:TextSummary;Cobertura' `
        '-assemblyfilters:-*.Tests;-FreizeitCockpit.TestSupport;-FreizeitCockpit.AppHost;-FreizeitCockpit.ServiceDefaults;-FreizeitCockpit.BibleStub;-FreizeitCockpit.Migrator' `
        '-filefilters:-**/Migrations/*;-**/obj/*;-**/*DesignTimeDbContextFactory.cs;-**/Program.cs'
    if ($LASTEXITCODE -ne 0) { throw "Backend-Coverage konnte mit Exitcode $LASTEXITCODE nicht zusammengeführt werden." }

    [xml]$coverage = Get-Content -Raw -LiteralPath (Join-Path $backendReport 'Cobertura.xml')
    $lineCoverage = [double]$coverage.coverage.'line-rate' * 100
    $branchCoverage = [double]$coverage.coverage.'branch-rate' * 100
    Write-Host ('Backend-Coverage: {0:N2}% Lines, {1:N2}% Branches.' -f $lineCoverage, $branchCoverage)
    if ($lineCoverage -lt 80 -or $branchCoverage -lt 75) {
        throw ('Backend-Coverage unterschreitet 80% Lines / 75% Branches: {0:N2}% / {1:N2}%.' -f $lineCoverage, $branchCoverage)
    }

    & "$PSScriptRoot/test-rls.ps1"
    & "$PSScriptRoot/test-cleanup.ps1"

    npm exec --yes pnpm@11.20.0 -- test:coverage
    if ($LASTEXITCODE -ne 0) { throw "Frontend-Coverage ist mit Exitcode $LASTEXITCODE fehlgeschlagen." }
}
finally {
    Pop-Location
}
