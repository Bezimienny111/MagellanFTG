param([switch]$OpenViewer,[switch]$HighIDs)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
& .\build.ps1 -Configuration Release -Test
if ($LASTEXITCODE -ne 0) { throw 'Build/test failed.' }
$latest = Get-ChildItem -LiteralPath 'artifacts\tests' -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'test-success.txt') } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (!$latest) { throw 'No completed integration run found.' }
$demo = Join-Path $PSScriptRoot 'artifacts\watk4-demo'
New-Item -ItemType Directory -Force -Path $demo | Out-Null
Copy-Item -LiteralPath (Join-Path $latest.FullName 'watk4.eu2map') -Destination (Join-Path $demo 'WatK4.eu2map') -Force
Copy-Item -LiteralPath (Join-Path $latest.FullName 'watk4-id9999.eu2map') -Destination (Join-Path $demo 'WatK4_ID9999.eu2map') -Force
Write-Host "Verified maps: $demo"
if ($OpenViewer) {
    $map = if ($HighIDs) { Join-Path $demo 'WatK4_ID9999.eu2map' } else { Join-Path $demo 'WatK4.eu2map' }
    & .\artifacts\bin\Release\mview.exe $map
}
