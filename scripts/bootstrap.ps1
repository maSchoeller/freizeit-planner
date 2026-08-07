[CmdletBinding()]
param([switch]$CheckOnly)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    $dotnetVersion = (dotnet --version).Trim()
    if (-not $dotnetVersion.StartsWith('10.0.')) {
        throw ".NET SDK 10.0.x wird benötigt; gefunden: $dotnetVersion"
    }
    $nodeMajor = [int]((node --version).TrimStart('v').Split('.')[0])
    if ($nodeMajor -lt 24) { throw 'Node.js 24 oder neuer wird benötigt.' }
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker wurde nicht gefunden.' }
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) { throw 'npm wurde nicht gefunden.' }

    Write-Host "Toolchain bereit: .NET $dotnetVersion, Node $(node --version), $(docker --version)."
    if ($CheckOnly) { exit 0 }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet restore FreizeitCockpit.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    npm exec --yes pnpm@11.20.0 -- install --frozen-lockfile
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
