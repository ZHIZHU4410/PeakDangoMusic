[CmdletBinding()]
param([string]$GameRoot,[switch]$Uninstall,[switch]$CheckOnly)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false)
function Get-PayloadHash([string]$Path) {
    $stream=[IO.File]::OpenRead($Path);$algorithm=[Security.Cryptography.SHA256]::Create()
    try {return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}
    finally {$algorithm.Dispose();$stream.Dispose()}
}
try {
    if(-not $GameRoot) {
        Add-Type -AssemblyName System.Windows.Forms
        $dialog=New-Object System.Windows.Forms.OpenFileDialog
        $dialog.Title='选择 PEAK.exe';$dialog.Filter='PEAK.exe|PEAK.exe'
        if($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK){exit 2}
        $GameRoot=Split-Path -LiteralPath $dialog.FileName;$dialog.Dispose()
    }
    $GameRoot=(Resolve-Path -LiteralPath $GameRoot).Path
    if(-not (Test-Path -LiteralPath (Join-Path $GameRoot 'PEAK.exe') -PathType Leaf)){throw '目录里没有 PEAK.exe。'}
    if(-not (Test-Path -LiteralPath (Join-Path $GameRoot 'PEAK_Data/Managed/Assembly-CSharp.dll') -PathType Leaf)){throw '需要 PEAK 的 Windows Mono 版本。'}
    if(Get-Process -Name PEAK -ErrorAction SilentlyContinue){throw '请先正常退出 PEAK，再运行安装或卸载。'}
    $plugins=[IO.Path]::GetFullPath((Join-Path $GameRoot 'BepInEx/plugins'))
    $target=[IO.Path]::GetFullPath((Join-Path $plugins 'PeakDangoMusic.dll'))
    $sidecar=[IO.Path]::GetFullPath((Join-Path $plugins 'PeakDangoMusic'))
    $allowedRoot=$GameRoot.TrimEnd('\')+'\'
    if(-not $target.StartsWith($allowedRoot,[StringComparison]::OrdinalIgnoreCase) -or -not $sidecar.StartsWith($plugins+'\',[StringComparison]::OrdinalIgnoreCase)){throw '安装目标超出游戏目录。'}
    foreach($folder in @((Join-Path $GameRoot 'BepInEx'),$plugins,$sidecar,(Join-Path $sidecar 'Native'))) {
        if((Test-Path -LiteralPath $folder) -and ((Get-Item -LiteralPath $folder).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw '安装目录是链接；请按说明手动安装到实际配置目录。'}
    }
    $backup=Join-Path $GameRoot ('BepInEx/PeakDangoMusicBackups/'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
    if($Uninstall) {
        if(-not (Test-Path -LiteralPath $target) -and -not (Test-Path -LiteralPath $sidecar)){Write-Host '没有找到本模组。';exit 0}
        if($CheckOnly){Write-Host '卸载目标检查通过，未修改文件。';exit 0}
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        if(Test-Path -LiteralPath $target){Move-Item -LiteralPath $target -Destination (Join-Path $backup 'PeakDangoMusic.dll.disabled')}
        if(Test-Path -LiteralPath $sidecar){Move-Item -LiteralPath $sidecar -Destination (Join-Path $backup 'PeakDangoMusic.disabled')}
        Write-Host ('已卸载，配置保留。备份：'+$backup);exit 0
    }
    $loader=Join-Path $GameRoot 'BepInEx/core/BepInEx.dll'
    if(-not (Test-Path -LiteralPath $loader -PathType Leaf)){throw '请先安装 BepInEx 5 x64。'}
    if(([Reflection.AssemblyName]::GetAssemblyName($loader).Version).Major -ne 5){throw '需要 BepInEx 5，不能使用 BepInEx 6。'}
    $duplicates=@(Get-ChildItem -LiteralPath $plugins -Filter PeakDangoMusic.dll -File -Recurse -ErrorAction SilentlyContinue | Where-Object {$_.FullName -ne $target})
    if($duplicates.Count){throw ('请先移出重复版本：'+($duplicates.FullName -join ', '))}
    $manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SHA256.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $rows=@($manifest | Where-Object {$_.path.StartsWith('BepInEx/plugins/',[StringComparison]::Ordinal)})
    if(@($rows | Where-Object {$_.path -eq 'BepInEx/plugins/PeakDangoMusic.dll'}).Count -ne 1 -or @($rows | Where-Object {$_.path -eq 'BepInEx/plugins/PeakDangoMusic/Native/PeakNeteaseBridge.exe'}).Count -ne 1){throw '安装包缺少必要组件。'}
    foreach($row in $rows) {
        $destination=[IO.Path]::GetFullPath((Join-Path $GameRoot $row.path))
        if($destination -ne $target -and -not $destination.StartsWith($sidecar+'\',[StringComparison]::OrdinalIgnoreCase)){throw '组件目标超出模组目录。'}
        $payload=Join-Path $PSScriptRoot $row.path
        if((Get-PayloadHash $payload) -ne $row.sha256){throw ('组件校验失败，请重新解压：'+$row.path)}
    }
    if($CheckOnly){Write-Host ('检查通过：'+$rows.Count+' 个模组组件，未修改文件。');exit 0}
    New-Item -ItemType Directory -Path $backup,$plugins -Force | Out-Null
    if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination (Join-Path $backup 'PeakDangoMusic.dll.previous')}
    if(Test-Path -LiteralPath $sidecar){Move-Item -LiteralPath $sidecar -Destination (Join-Path $backup 'PeakDangoMusic.previous')}
    $config=Join-Path $GameRoot 'BepInEx/config/local.peak.dangomusic.cfg'
    if(Test-Path -LiteralPath $config){Copy-Item -LiteralPath $config -Destination (Join-Path $backup 'local.peak.dangomusic.cfg.previous')}
    foreach($row in $rows) {
        $destination=[IO.Path]::GetFullPath((Join-Path $GameRoot $row.path))
        New-Item -ItemType Directory -Path (Split-Path -LiteralPath $destination) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $row.path) -Destination $destination -Force
        if((Get-PayloadHash $destination) -ne $row.sha256){throw ('写入校验失败；旧版保存在：'+$backup)}
    }
    [ordered]@{version=([Reflection.AssemblyName]::GetAssemblyName($target).Version).ToString(3);target=$target;backup=$backup;installedAt=(Get-Date -Format o)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'receipt.json') -Encoding UTF8
    Write-Host '安装完成。启动 PEAK，F9 设置，F8 显隐歌词。'
    Write-Host 'BLF/LX 开启本机 API；原版网易云在 F9 选择对应来源。'
    Write-Host ('旧版备份：'+$backup);exit 0
} catch {Write-Host ('操作未完成：'+$_.Exception.Message) -ForegroundColor Red;exit 1}
