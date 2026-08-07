[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$target = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'src/FreizeitCockpit.Web/wwwroot'))
$expected = [System.IO.Path]::GetFullPath(
    (Join-Path $repositoryRoot 'src/FreizeitCockpit.Web/wwwroot'))

if ($target -ne $expected -or -not $target.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsicheres Build-Ziel: $target"
}

if (Test-Path -LiteralPath $target) {
    Get-ChildItem -LiteralPath $target -Force | Remove-Item -Recurse -Force
}
else {
    New-Item -ItemType Directory -Path $target | Out-Null
}
