[CmdletBinding()]
param([string]$BaseUrl = 'http://localhost:5041')

$ErrorActionPreference = 'Stop'
$null = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 10
$api = Invoke-RestMethod -Uri "$BaseUrl/api/v1" -TimeoutSec 10
if ($api.name -ne 'Freizeit-Cockpit API') { throw 'Unerwartete API-Antwort.' }
Write-Host "Smoke erfolgreich: $BaseUrl/health und $BaseUrl/api/v1."
