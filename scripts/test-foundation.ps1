[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$requiredPaths = @(
    'global.json',
    'Directory.Build.props',
    'Directory.Packages.props',
    'FreizeitCockpit.slnx',
    'src/FreizeitCockpit.Web/FreizeitCockpit.Web.csproj',
    'src/FreizeitCockpit.AppHost/FreizeitCockpit.AppHost.csproj',
    'src/FreizeitCockpit.ServiceDefaults/FreizeitCockpit.ServiceDefaults.csproj',
    'src/FreizeitCockpit.Migrator/FreizeitCockpit.Migrator.csproj',
    'src/FreizeitCockpit.Cleanup/FreizeitCockpit.Cleanup.csproj',
    'src/FreizeitCockpit.BibleStub/FreizeitCockpit.BibleStub.csproj',
    'src/Web/package.json',
    'src/Help/package.json',
    'playwright.config.ts',
    'tests/Browser/core-journey.spec.ts',
    'tests/Browser/global-setup.ts',
    'tests/Architecture.Tests/Architecture.Tests.csproj',
    'tests/Aspire.Tests/Aspire.Tests.csproj',
    'scripts/bootstrap.ps1',
    'scripts/dev.ps1',
    'scripts/test.ps1',
    'scripts/test-coverage.ps1',
    'scripts/test-browser.ps1',
    'scripts/verify.ps1',
    'scripts/smoke.ps1'
)

$modules = @('Identity', 'Camps', 'Catering', 'Logistics', 'Spiritual', 'Knowledge', 'Files', 'Activity')
foreach ($module in $modules) {
    $requiredPaths += "src/$module/$module.Contracts/$module.Contracts.csproj"
    $requiredPaths += "src/$module/$module.Implementation/$module.Implementation.csproj"
    $requiredPaths += "tests/$module.Tests/$module.Tests.csproj"
}

$missing = $requiredPaths | Where-Object { -not (Test-Path -LiteralPath (Join-Path $repositoryRoot $_)) }
if ($missing.Count -gt 0) {
    Write-Error ("Foundation is incomplete. Missing:`n - " + ($missing -join "`n - "))
}

Write-Host "Foundation contract passed ($($requiredPaths.Count) required paths)."
