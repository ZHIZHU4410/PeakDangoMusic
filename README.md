# PeakDangoMusic · PEAK 悬浮歌词

在 PEAK 主菜单、机场、地图、加载和设置界面持续显示与播放器同步的歌词。游戏中保留左右两侧世界悬浮歌词，打开界面或相机／角色尚未就绪时使用透明的两侧屏幕浮层接续当前歌词。

作者：**布洛芬（ZHIZHU4410 / BLF4410）**。Codex 辅助开发。
歌词实现改编自 [Sorihiko/DangoMusic](https://github.com/Sorihiko/DangoMusic) 及已有的 DangoMusic Desktop 实现，采用 **GPL-3.0**；来源与改动见 [NOTICE.md](NOTICE.md)。

## 下载与安装

前往 [Releases](https://github.com/ZHIZHU4410/PeakDangoMusic/releases) 下载 `PeakDangoMusic-1.1.2-full-installer.zip`。GitHub 自动生成的 Source code ZIP 只有源码，不能直接安装。

1. 需要 Windows 10/11 x64、PEAK Windows Mono 版以及 [BepInEx 5 / PEAK 专用加载器](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/)。
2. 退出 PEAK，将完整安装包解压到普通文件夹，运行“一键安装.cmd”并选择 `PEAK.exe`。
3. 重开游戏，按 **F9** 打开设置，按 **F8** 显示或隐藏歌词。

手动安装时，将安装包中 `BepInEx/plugins/PeakDangoMusic.dll` 和 `BepInEx/plugins/PeakDangoMusic/` 文件夹一起复制到当前配置的插件目录。无需全能助手或地图模组。

## 功能与连接

- 左右两侧世界歌词、整句／单词／逐字显示、可选翻译、光晕、亮度与动画设置。
- 主菜单、加载、F9 和其他鼠标界面使用不捕获输入的屏幕浮层，按同一歌曲进度连续显示；播放器暂停时保留当前句。
- F9 大窗口、分栏滚动、歌词预览及恢复原项目默认设置。
- 正式游戏场景打开 F9 暂停本机游戏并屏蔽角色操作；关闭后恢复原速度。联机其他玩家与音乐播放器继续运行。
- **BLF / LX Music**：开启播放器的本机开放 API，默认端口 `23330`，F9 选择相同端口。仅访问 `127.0.0.1` 的状态与歌词接口。
- **可选原版网易云音乐连接（未实测）**：独立 Windows 辅助程序读取媒体会话与可选的本机播放进度，再通过网易云公开 HTTPS 接口获取歌词。不需要 Cookie、账号资料或第三方 API 服务器。

网易云客户端不提供播放进度时，可先完全退出该客户端，再使用完整包中的“启动网易云歌词连接.cmd”。脚本以本机地址 `127.0.0.1:9223` 开启可选的调试连接；不修改播放器文件。

详细步骤、卸载和排错见 [安装说明](packaging/安装说明.md)。默认值按固定的 Sorihiko 上游提交核对；Unity 字体与材质的适配差异见 [默认值对照](SORIHIKO-DEFAULTS.zh-CN.md)。

## 当前验证范围

当前版本 **1.1.2** 修复共用显示层的场景限制，新增全界面歌词接续与状态提示。仅完成源码调整和编译，未运行新的游戏或接口测试。新增网易云连接未进行运行测试；多人联机及管理器自动安装未验收。历史 1.1.0 的界面检查不代表当前默认值与侧面位置已经验收。详情见 [VERIFICATION.zh-CN.md](VERIFICATION.zh-CN.md)。

## 从源码构建

需要 .NET 10 SDK，以及本机已安装 BepInEx 5 的正版 PEAK。仓库不分发游戏、加载器、播放器、字体或音乐文件。

```powershell
.\build.ps1 -GameRoot "你的 PEAK 安装目录"
python tools/package.py
```

`build.ps1` 构建 Unity 插件和独立的 Windows x64 网易云辅助程序；不会安装模组或启动游戏、播放器。`tools/package.py` 使用 Python 标准库生成 `artifacts/PeakDangoMusic-版本号-完整安装包.zip` 及 SHA-256 校验文件。

也可以分别构建：

```powershell
dotnet build source/PeakDangoMusic/PeakDangoMusic.csproj -c Release -p:GameRoot="你的 PEAK 安装目录"
dotnet publish source/NetEaseBridge/PeakNeteaseBridge.csproj -c Release -r win-x64 --self-contained true -o artifacts/native
```

目录：`source/PeakDangoMusic` 为 BepInEx 插件；`source/NetEaseBridge` 为可选网易云辅助程序；`packaging` 为安装脚本与运行库许可；`tools` 为本地打包工具。发布包包含完整对应源码。微软运行时及 C#/WinRT 组件遵循各自随附的许可声明。

## 反馈

请在 Issues 中附上游戏版本、模组版本、播放器类型和相关日志片段。日志位于当前配置的 `BepInEx/LogOutput.log`；配置为 `BepInEx/config/local.peak.dangomusic.cfg`。提交前移除个人路径、账号及其他私人信息。

## English

PeakDangoMusic is a client-side BepInEx 5 mod that displays synchronized floating lyrics in PEAK. Lyrics alternate between the left and right sides. A non-interactive screen layer continues the same timeline across title, loading and settings screens; pausing the music retains the current line. F9 opens the settings window and pauses local gameplay; F8 toggles lyrics. BLF/LX local APIs are supported. Optional stock NetEase Cloud Music support uses a separate Windows helper and has **not been runtime-tested**.

Download the full installer ZIP from Releases. Install both the plugin DLL and its companion directory. Version 1.1.2 has been compiled, but no new in-game or player API tests were performed. Multiplayer behavior and automatic mod-manager installation remain unverified. GPL-3.0; see LICENSE and NOTICE.md for attribution.
