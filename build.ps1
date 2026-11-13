param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [switch]$Test,
    [switch]$Rebuild
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$builder = Join-Path $frameworkDir 'MSBuild.exe'
if (!(Test-Path -LiteralPath $builder)) { $builder = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\MSBuild.exe' }
$target = if ($Rebuild) { 'Rebuild' } else { 'Build' }
$artifacts = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
& $builder 'Maptools\Maptools.sln' "/t:$target" "/p:Configuration=$Configuration" '/toolsversion:4.0' '/verbosity:minimal' '/nologo' | Tee-Object -FilePath (Join-Path $artifacts "build-$Configuration.log")
if ($LASTEXITCODE -ne 0) { throw 'Magellan build failed.' }
$bin = Join-Path $artifacts "bin\$Configuration"
New-Item -ItemType Directory -Force -Path $bin | Out-Null
$solutionDirectory = Join-Path $PSScriptRoot 'Maptools'
foreach ($line in Get-Content -LiteralPath 'Maptools\Maptools.sln') {
    if ($line -match '^Project\("\{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC\}"\).*?"([^\"]+\.csproj)"') {
        $projectPath = [System.IO.Path]::GetFullPath((Join-Path $solutionDirectory $Matches[1]))
        $projectOutput = Join-Path ([System.IO.Path]::GetDirectoryName($projectPath)) "bin\$Configuration"
        Get-ChildItem -LiteralPath $projectOutput -File | Where-Object { $_.Extension -in '.exe','.dll','.config','.pdb' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $bin -Force }
    }
}
if ($Test) {
    & $builder 'tests\RestorationTests.csproj' '/t:Build' "/p:Configuration=$Configuration" '/toolsversion:4.0' '/verbosity:minimal' '/nologo'
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & ".\tests\bin\$Configuration\RestorationTests.exe" $PSScriptRoot | Tee-Object -FilePath (Join-Path $artifacts "test-$Configuration.log")
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }
}
Write-Host "Built tools: $bin"
