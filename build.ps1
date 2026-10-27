[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$GameRoot)
$ErrorActionPreference = 'Stop'
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $GameRoot 'PEAK_Data/Managed/Assembly-CSharp.dll'))) {
    throw 'GameRoot must point to a PEAK Windows Mono installation.'
}
if (-not (Test-Path -LiteralPath (Join-Path $GameRoot 'BepInEx/core/BepInEx.dll'))) {
    throw 'Install BepInEx 5 in GameRoot before building.'
}
Push-Location $PSScriptRoot
try {
    & dotnet build 'source/PeakDangoMusic/PeakDangoMusic.csproj' -c Release "-p:GameRoot=$GameRoot"
    if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
    & dotnet publish 'source/NetEaseBridge/PeakNeteaseBridge.csproj' -c Release -r win-x64 --self-contained true -o 'artifacts/native'
    if ($LASTEXITCODE -ne 0) { throw 'NetEase helper build failed.' }
    Write-Host 'Build complete. Run: python tools/package.py'
} finally { Pop-Location }
