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
    $npmCommand=Get-Command npm.cmd -ErrorAction SilentlyContinue
    $npmPath=if($npmCommand){$npmCommand.Source}else{Join-Path $env:ProgramFiles 'nodejs/npm.cmd'}
    if(-not (Test-Path -LiteralPath $npmPath)){throw 'Install Node.js 24 or newer before building Folia styles.'}
    $previousPath=$env:Path
    try {
        $env:Path=(Split-Path -LiteralPath $npmPath)+';'+$env:Path
        Push-Location 'source/FoliaRenderer'
        try {
            & $npmPath ci --ignore-scripts --no-audit --no-fund
            if($LASTEXITCODE -ne 0){throw 'Folia dependency installation failed.'}
            & $npmPath run build
            if($LASTEXITCODE -ne 0){throw 'Folia renderer build failed.'}
        } finally {Pop-Location}
    } finally {$env:Path=$previousPath}
    & python 'source/FoliaRenderer/tools/package-runtime.py' --output 'artifacts/folia' --cache 'artifacts/downloads'
    if($LASTEXITCODE -ne 0){throw 'Folia runtime packaging failed.'}
    Write-Host 'Build complete. Run: python tools/package.py'
} finally { Pop-Location }
