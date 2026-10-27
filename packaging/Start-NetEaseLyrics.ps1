[CmdletBinding()]
param([string]$CloudMusicPath,[ValidateRange(1,65535)][int]$Port=9223)
$ErrorActionPreference='Stop'
try {
    if(Get-Process -Name cloudmusic -ErrorAction SilentlyContinue){throw '请先从托盘完全退出网易云，再运行本脚本。脚本不会结束已有播放器。'}
    if(-not $CloudMusicPath){
        Add-Type -AssemblyName System.Windows.Forms
        $dialog=New-Object System.Windows.Forms.OpenFileDialog
        $dialog.Title='选择原版网易云音乐 cloudmusic.exe'
        $dialog.Filter='cloudmusic.exe|cloudmusic.exe'
        if($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK){exit 2}
        $CloudMusicPath=$dialog.FileName;$dialog.Dispose()
    }
    $CloudMusicPath=(Resolve-Path -LiteralPath $CloudMusicPath).Path
    if([IO.Path]::GetFileName($CloudMusicPath) -ine 'cloudmusic.exe'){throw '请选择原版网易云的 cloudmusic.exe。'}
    Start-Process -FilePath $CloudMusicPath -WorkingDirectory (Split-Path -LiteralPath $CloudMusicPath) -WindowStyle Hidden -ArgumentList '--remote-debugging-address=127.0.0.1',("--remote-debugging-port="+$Port)
    Write-Host '已启动原版网易云的本机歌词连接。正常播放音乐，然后在 PEAK 的 F9 中选择“原版网易云”。'
    Write-Host '仅开启本机进度连接，不修改播放器文件，不读取账号 Cookie。此新增连接按要求未做实机测试。'
} catch {Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
