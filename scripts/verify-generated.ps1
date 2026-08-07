[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$document = Join-Path $repositoryRoot 'src/FreizeitCockpit.Web/openapi/FreizeitCockpit.Web.json'
$client = Join-Path $repositoryRoot 'src/Web/src/api/schema.ts'

if (-not (Test-Path -LiteralPath $document) -or -not (Test-Path -LiteralPath $client)) {
    throw 'OpenAPI-Dokument oder TypeScript-Client fehlt. Führe Build und openapi:generate aus.'
}

$documentHash = (Get-FileHash -LiteralPath $document -Algorithm SHA256).Hash
$clientHash = (Get-FileHash -LiteralPath $client -Algorithm SHA256).Hash

dotnet build (Join-Path $repositoryRoot 'src/FreizeitCockpit.Web/FreizeitCockpit.Web.csproj') `
    --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Push-Location $repositoryRoot
try {
    npm exec --yes pnpm@11.20.0 -- openapi:generate
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}

if ((Get-FileHash -LiteralPath $document -Algorithm SHA256).Hash -ne $documentHash) {
    throw 'Das eingecheckte OpenAPI-Dokument war nicht aktuell.'
}
if ((Get-FileHash -LiteralPath $client -Algorithm SHA256).Hash -ne $clientHash) {
    throw 'Der eingecheckte TypeScript-Client war nicht aktuell.'
}
