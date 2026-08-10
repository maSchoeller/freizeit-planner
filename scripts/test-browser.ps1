[CmdletBinding()]
param(
    [switch]$UpdateHelpScreenshots,
    [string]$Project,
    [string]$Grep
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$appHostProcess = $null
$logDirectory = Join-Path $repositoryRoot '.artifacts/playwright/aspire'

function Test-WebReady {
    try {
        $response = Invoke-WebRequest -Uri 'http://localhost:5041/health' -UseBasicParsing -TimeoutSec 2
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

Push-Location $repositoryRoot
try {
    if (-not (Test-WebReady)) {
        New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
        $appHostProcess = Start-Process -FilePath 'dotnet' `
            -ArgumentList @('run', '--project', 'src/FreizeitCockpit.AppHost/FreizeitCockpit.AppHost.csproj', '--no-restore', '--launch-profile', 'http') `
            -WorkingDirectory $repositoryRoot `
            -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $logDirectory 'apphost.out.log') `
            -RedirectStandardError (Join-Path $logDirectory 'apphost.err.log') `
            -PassThru

        for ($attempt = 0; $attempt -lt 120 -and -not (Test-WebReady); $attempt++) {
            if ($appHostProcess.HasExited) {
                throw "Aspire AppHost wurde vorzeitig mit Exitcode $($appHostProcess.ExitCode) beendet."
            }
            Start-Sleep -Seconds 1
        }
        if (-not (Test-WebReady)) { throw 'Die Web-Anwendung wurde innerhalb von 120 Sekunden nicht bereit.' }
    }

    & "$PSScriptRoot/smoke.ps1" -BaseUrl 'http://localhost:5041'

    $mailpitId = docker ps --filter 'ancestor=axllent/mailpit:v1.27' --format '{{.ID}}' | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($mailpitId)) { throw 'Der Mailpit-Container des Aspire-Stacks wurde nicht gefunden.' }
    $mailpitPort = ((docker port $mailpitId 8025/tcp) -replace '^.*:', '').Trim()
    if ($mailpitPort -notmatch '^\d+$') { throw "Der Mailpit-Port ist ungültig: '$mailpitPort'." }

    $env:WEB_BASE_URL = 'http://localhost:5041'
    $env:MAILPIT_URL = "http://localhost:$mailpitPort"
    $env:UPDATE_HELP_SCREENSHOTS = if ($UpdateHelpScreenshots) { '1' } else { '0' }

    $arguments = @('exec', '--yes', 'pnpm@11.20.0', '--', 'test:browser')
    if (-not [string]::IsNullOrWhiteSpace($Project)) { $arguments += @('--project', $Project) }
    if (-not [string]::IsNullOrWhiteSpace($Grep)) { $arguments += @('--grep', $Grep) }
    npm @arguments
    if ($LASTEXITCODE -ne 0) { throw "Playwright ist mit Exitcode $LASTEXITCODE fehlgeschlagen." }
}
finally {
    Remove-Item Env:WEB_BASE_URL, Env:MAILPIT_URL, Env:UPDATE_HELP_SCREENSHOTS -ErrorAction SilentlyContinue
    if ($null -ne $appHostProcess -and -not $appHostProcess.HasExited) {
        Stop-Process -Id $appHostProcess.Id
        $appHostProcess.WaitForExit(10000) | Out-Null
    }
    Pop-Location
}
