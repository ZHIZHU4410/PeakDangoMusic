# PEAK 漂浮歌词 · 完整安装包 1.1.1

作者：布洛芬（ZHIZHU4410） · Codex 辅助开发 · 2026-10-27

在 PEAK 正式地图显示播放器同步歌词。支持 BLF/LX 和可选的原版网易云音乐连接，整句、单词、逐字、汉化、亮度、动画、左右两侧世界漂浮和世界固定模式。无需全能助手或地图模组。

## 安装要求

- 正版 PEAK Windows Mono 版，Windows 10/11 x64。本版编译与界面验证环境：PEAK 3.0.1、Unity 6000.3.15、BepInEx 5.4.23.3。
- BepInEx 5 x64。已有可运行的 BepInEx 5 无需替换，不支持 BepInEx 6 或 IL2CPP。
- 自备 BLF/LX（需支持本机开放 API）或原版网易云音乐 Windows 客户端。
- 网易云连接组件已包含独立 .NET 运行时，普通玩家无需另装 .NET 或 SDK。
- 使用本机微软雅黑等系统字体，不分发 Windows 字体文件。

BepInEx 获取：[PEAK 专用 BepInExPack](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/)。

## 安装与更新

1. 正常退出 PEAK，把 ZIP 完整解压到普通文件夹。
2. 双击“一键安装.cmd”，选择游戏目录的 PEAK.exe。
3. 启动 PEAK，按 F9 打开设置。F8 显隐歌词。
4. 升级会备份旧 DLL、网易云连接组件及歌词配置，保留用户设置。

手动安装：把本包 BepInEx/plugins 中的 PeakDangoMusic.dll 和 PeakDangoMusic 文件夹一起复制到实际配置的 BepInEx/plugins。**不要只复制 DLL，否则缺少网易云连接组件。** 不要保留同名旧 DLL。

```text
PEAK/
  PEAK.exe
  BepInEx/plugins/
    PeakDangoMusic.dll
    PeakDangoMusic/Native/
      PeakNeteaseBridge.exe
      DOTNET-LICENSE.txt
      DOTNET-THIRD-PARTY-NOTICES.txt
```

使用模组管理器时，把以上两个项目放入当前配置的 BepInEx/plugins 并从该配置启动；包内脚本只处理手动安装目录。管理器自动安装兼容性未实测。

## F9 设置与歌词位置

- 界面随分辨率缩放。左侧为来源、状态与连接；右侧切换“歌词显示与位置”和“动画与效果”，两栏分别滚动。
- 大按钮、滑条和文字已放大；路径输入框不会挤出按钮，无横向滚动。
- 正式地图中打开 F9 暂停本机游戏并屏蔽角色操作；F9、Esc 或“返回游戏”恢复打开前的速度。主菜单及加载阶段不冻结加载。联机远端玩家和音乐播放器继续运行。
- 歌词默认在左右两侧交替生成，按原项目Scale=1、Distance=10、Spread=30°。固定跟随和智能防离屏默认均关闭，靠近时后退与朝向玩家默认开启。
- 世界固定模式保留原项目空间感；可手动开启跟随或智能防离屏。F9显示页有“恢复 Sorihiko 原项目默认”，完整对照见SORIHIKO-DEFAULTS.zh-CN.md。
- 字色为原项目暖白#F7E1D7，额外亮度默认1。光晕开启、随字色、强度0.3、半径0.5；抖动8Hz/0.8，逐字弹出开启。
- “动画与效果”页底部的“预览8秒并返回游戏”会关闭设置，让你直接观察效果。

## BLF / LX 连接

1. 在播放器设置中开启“开放 API”本机服务，默认端口23330。
2. 正常播放带歌词歌曲。F9 左侧选“BLF / LX”，填写相同端口并点“应用端口”。
3. 模组只访问127.0.0.1的 /status 和 /lyric-all。播放器 exe 路径可留空，仅影响启动播放器按钮。
4. 正式地图才显示歌词；纯音乐或无同步歌词歌曲不会显示旧歌词。

## 原版网易云音乐连接（新增，未实测）

1. 正常启动原版网易云并播放歌曲，在 F9 左侧选择“原版网易云”。
2. 组件优先读取 Windows 媒体会话提供的曲名与时间；不需要账号、Cookie 或第三方网易云 API 服务。
3. 部分原版客户端不提供有效播放进度。此时先从托盘完全退出网易云，双击本包“启动网易云歌词连接.cmd”，选择自己的 cloudmusic.exe，然后正常播放。
4. 该脚本仅给当前进程添加127.0.0.1本机连接端口9223，不修改播放器文件或系统设置。已经运行的播放器不会被结束；直接启动的新参数无法接管旧进程。
5. 模组读取已知原版客户端页面的播放状态、曲目和进度，再从 music.163.com 的公开 HTTPS 接口获取歌词；不调用登录、Cookie、账号或歌曲下载接口。
6. 自定义端口时，通过脚本 -Port 参数和配置“网易云本机端口”设置相同值。
7. 播放器内部结构、公开接口或歌曲匹配不确定时会显示状态提示；不伪造进度，不用其他录音的歌词代替。

**此新增网易云连接按用户要求仅做源码接入与编译，未启动播放器或执行接口测试。** 不能保证所有原版网易云版本可用；本机接口解析沿用已有 DangoMusic 的客户端结构适配。

## 故障排查

| 现象 | 处理 |
|---|---|
| F9无反应 | 重开游戏，确认日志加载 PeakDangoMusic 1.1.1，检查实际配置目录和重复DLL。 |
| 左侧显示未连接 | BLF/LX 开启开放 API；网易云选对来源，必要时用本包连接脚本启动。 |
| 缺少网易云组件 | 完整安装 DLL 和 PeakDangoMusic 文件夹，不要只更新 DLL。 |
| 有曲名无歌词 | 进入正式地图，检查F8、换有同步歌词的歌曲；网易云无进度时按连接步骤处理。 |
| 中文方块或无字 | 左侧点“重新加载歌词字体”，检查本机语言字体；SDF失败会尝试兼容字体。 |
| 移动时看不到歌词 | 开启“跟随视角”；关闭人物后景，或开启允许穿墙。 |
| 光晕不明显 | 调高文字亮度；原生兼容字体的发光能力取决于系统材质。 |

日志：实际配置的 BepInEx/LogOutput.log。配置：BepInEx/config/local.peak.dangomusic.cfg。反馈只需相关片段，不要分享账号资料或播放器用户目录。

## 卸载、备份与恢复

退出 PEAK 后双击“卸载歌词.cmd”并选择 PEAK.exe，模组和原版网易云连接组件移入备份，用户歌词配置保留。手动安装可移走 DLL 及同名组件文件夹。

备份位于 BepInEx/PeakDangoMusicBackups/时间编号/。恢复时把 PeakDangoMusic.dll.previous 改名复制回 plugins；如有 PeakDangoMusic.previous 文件夹，也恢复为 plugins/PeakDangoMusic。只保留一份 DLL。配置按需单独恢复。

## 下载与源码

完整安装包和校验文件：[GitHub Releases](https://github.com/ZHIZHU4410/PeakDangoMusic/releases)。
源码：[PeakDangoMusic](https://github.com/ZHIZHU4410/PeakDangoMusic)。
GitHub 自动生成的 Source code ZIP 只包含源码；请下载完整安装包用于安装。

## 许可、来源与构建

歌词核心与原版网易云适配源自 Sorihiko／本机 DangoMusic 的 GPL-3.0 实现；PEAK 适配作者布洛芬（ZHIZHU4410），Codex辅助。详见 LICENSE、NOTICE.md 和 source。

网易云独立组件包含微软 .NET 和 C#/WinRT 运行库，相关许可证与第三方声明位于 Native 目录。不会把 Windows/.NET10 程序集加载进 Unity Mono。

开发者构建：
```powershell
dotnet build source/PeakDangoMusic/PeakDangoMusic.csproj -c Release -p:GameRoot="你的PEAK目录"
dotnet publish source/NetEaseBridge/PeakNeteaseBridge.csproj -c Release -r win-x64 --self-contained true
```

本轮实际验证范围另见 VERIFICATION.zh-CN.md。网易云与多人联机结果不记为通过。
