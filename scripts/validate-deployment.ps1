[CmdletBinding()]
param([switch]$SkipContainerBuild)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactDirectory = Join-Path $repositoryRoot '.artifacts'
$armTemplate = Join-Path $artifactDirectory 'azure-main.json'

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "$Name wurde nicht gefunden. Siehe README.md für die benötigten Werkzeuge."
    }
}

Push-Location $repositoryRoot
try {
    Require-Command 'az'
    Require-Command 'azd'
    Require-Command 'actionlint'
    if (-not $SkipContainerBuild) { Require-Command 'docker' }

    New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

    az bicep lint --file infra/main.bicep
    if ($LASTEXITCODE -ne 0) { throw "Bicep-Lint ist mit Exitcode $LASTEXITCODE fehlgeschlagen." }
    az bicep build --file infra/main.bicep --outfile $armTemplate
    if ($LASTEXITCODE -ne 0) { throw "Bicep-Build ist mit Exitcode $LASTEXITCODE fehlgeschlagen." }

    $environments = azd env list --output json --no-prompt
    if ($LASTEXITCODE -ne 0) { throw "azd-Konfigurationsprüfung ist mit Exitcode $LASTEXITCODE fehlgeschlagen." }
    $null = $environments | ConvertFrom-Json

    $workflowFiles = Get-ChildItem '.github/workflows/*.yml' | Select-Object -ExpandProperty FullName
    & actionlint @workflowFiles
    if ($LASTEXITCODE -ne 0) { throw "actionlint ist mit Exitcode $LASTEXITCODE fehlgeschlagen." }

    if (-not $SkipContainerBuild) {
        $images = @(
            @{ Name = 'web'; Dockerfile = 'src/FreizeitCockpit.Web/Dockerfile' },
            @{ Name = 'migrator'; Dockerfile = 'src/FreizeitCockpit.Migrator/Dockerfile' },
            @{ Name = 'cleanup'; Dockerfile = 'src/FreizeitCockpit.Cleanup/Dockerfile' }
        )

        foreach ($image in $images) {
            $tag = "freizeit-cockpit-validation-$($image.Name):local"
            docker build --file $image.Dockerfile --tag $tag .
            if ($LASTEXITCODE -ne 0) { throw "Container-Build für $($image.Name) ist fehlgeschlagen." }
            $user = (docker image inspect $tag --format '{{.Config.User}}').Trim()
            if ([string]::IsNullOrWhiteSpace($user) -or $user -in @('0', 'root')) {
                throw "Container $($image.Name) hat keinen sicheren Nicht-Root-Benutzer konfiguriert."
            }
        }
    }

    Write-Host 'Deployment-Artefakte wurden ausschließlich lokal und ohne Azure-Anmeldung validiert.'
}
finally {
    Pop-Location
}
