[CmdletBinding()]
param([string]$KugouPath)
$ErrorActionPreference='Stop'
try {
    if(Get-Process -Name KuGou -ErrorAction SilentlyContinue){throw '请先从托盘完全退出酷狗，再运行本脚本。'}
    if(-not $KugouPath -and (Test-Path -LiteralPath 'D:\KGMusic\KuGou.exe')){$KugouPath='D:\KGMusic\KuGou.exe'}
    if(-not $KugouPath){
        Add-Type -AssemblyName System.Windows.Forms
        $dialog=New-Object System.Windows.Forms.OpenFileDialog
        $dialog.Title='选择酷狗音乐 KuGou.exe';$dialog.Filter='KuGou.exe|KuGou.exe'
        if($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK){exit 2}
        $KugouPath=$dialog.FileName;$dialog.Dispose()
    }
    $KugouPath=(Resolve-Path -LiteralPath $KugouPath).Path
    if([IO.Path]::GetFileName($KugouPath) -ine 'KuGou.exe'){throw '请选择酷狗音乐 KuGou.exe。'}
    Start-Process -FilePath $KugouPath -WorkingDirectory (Split-Path -LiteralPath $KugouPath) -WindowStyle Hidden -ArgumentList '--remote_debug_port=9225','--remote-debugging-address=127.0.0.1'
    Write-Host '已启动酷狗本机接口。请打开“推荐”页并播放，再在 PEAK 游戏选项的歌词设置中选择“酷狗音乐”。'
    Write-Host '不修改播放器文件；仅读取曲名、播放状态与进度。'
} catch {Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
