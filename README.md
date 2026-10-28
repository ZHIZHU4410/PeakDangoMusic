# PeakDangoMusic · PEAK 悬浮歌词

在PEAK主菜单、机场、地图、加载和F9界面持续显示同步歌词。保留原有Dango两侧悬浮文字，1.2.0新增[Folia](https://github.com/chthollyphile/folia-major)的全部15种内置歌词样式。

作者：**布洛芬（ZHIZHU4410 / BLF4410）**；Codex辅助开发。Dango来源与Folia原作者署名见[NOTICE.md](NOTICE.md)。

## 下载与使用

1. 在[Releases](https://github.com/ZHIZHU4410/PeakDangoMusic/releases/tag/v1.2.0)下载 **PeakDangoMusic-1.2.0-full-installer.zip**，完整解压。Source code压缩包只有源码，不能直接安装。
2. 需要Windows x64、PEAK Mono版及BepInEx5。退出PEAK，运行“一键安装.cmd”，选择PEAK.exe；也可按[安装说明](packaging/安装说明.md)手动复制完整DLL和PeakDangoMusic文件夹。
3. 重开游戏，**F9 → Folia歌词样式**选择；**F8**显隐。原Dango样式仍为默认，已有播放器连接与歌词参数保留。

## 歌词样式

商籁、凝彩、绘光、花火、流光、心象、云阶、浮名、倾诉、回环、莫奈、时计、群唱、镜台、静止，以及每首随机选择。直接构建上游原React/Pixi/Three渲染器，并非只给旧动画改名。[逐项对应与适配范围](FOLIA-STYLES.zh-CN.md)。

F9提供两侧悬浮、两侧屏幕、全屏舞台；可调舞台大小、透明度、字体倍率、动效强度、画质及帧率。菜单和设置中自动接续显示，音乐暂停时保留当前位置。Folia准备或失败时回退Dango；选回Dango或F8关闭时停止组件。

原播放器没有提供频谱、节拍／人声分析、封面或立绘时不伪造这些输入，相应附加视觉元素可能缺省。原项目的全部设置与第三方Folium模组不在此适配范围。

## 播放器连接

BLF/LX开启本机开放API，默认端口23330。原版网易云使用Windows媒体信息、可选本机进度接口及公开歌词接口；无进度时先完全退出网易云，再用安装包连接脚本启动。插件不需要Cookie或账号资料，不控制播放器、不下载音乐。

Folia组件仅用于本地离屏渲染，没有独立播放器窗口或键鼠接管。完整包附运行时，用户无需安装Node.js或Electron。首次选择样式会准备资源；默认960×540／24帧，可在F9降低画质减少占用。F9仍暂停本机游戏，其他联机玩家与播放器继续运行。

## 验证范围

**1.2.0仅完成源码核对、前端生产构建、C#编译和打包校验；按用户要求没有运行游戏、播放器、Folia程序或自动测试。** 新样式画面、显卡兼容性、性能、各场景切换及多人效果由用户验收。历史记录见[VERIFICATION.zh-CN.md](VERIFICATION.zh-CN.md)。

## 构建

需要.NET10 SDK、Node.js24或更高版本、npm、Python3，以及本机已安装BepInEx5的PEAK。

```powershell
.\build.ps1 -GameRoot "你的PEAK安装目录"
python tools/package.py
```

build.ps1编译插件与网易云辅助程序、构建原Folia前端，再下载并核对固定Electron运行时；不会启动游戏、播放器或渲染程序。安装包输出到artifacts目录。

- source/PeakDangoMusic：BepInEx插件与Dango歌词实现。
- source/NetEaseBridge：独立原版网易云连接源码。
- source/FoliaRenderer：AGPL离屏适配、已附上游源码、锁文件和构建工具。
- packaging：安装脚本、手册及微软组件许可。

## 许可与反馈

Dango插件部分保留GPL-3.0；Folia及适配渲染器采用AGPL-3.0，发行组合依照两许可第13条，完整对应源码和各自许可随包提供。Electron、Chromium及前端依赖遵循随附声明。上游固定提交7a5f975e337e5ba13f55d72b5bc68983bf41c379；原Dango默认值对照见[SORIHIKO-DEFAULTS.zh-CN.md](SORIHIKO-DEFAULTS.zh-CN.md)。

反馈请附模组版本、所选样式、播放器类型、F9状态和相关日志片段。日志位于BepInEx/LogOutput.log，配置为BepInEx/config/local.peak.dangomusic.cfg；提交前移除私人资料。

## English

PEAK floating lyrics with the original Dango mode plus all15 pinned Folia builtin visualizers. Select them under F9 → Folia styles. Includes side-positioned world/screen stages, fullscreen display, quality controls and complete corresponding source. BLF/LX and optional stock NetEase use the existing read-only timeline. Dango remains the default and fallback. Unavailable audio-analysis/cover input is not synthesized.

Download the full-installer ZIP, not GitHub's generated Source code archives. Version1.2.0 was compiled and packaged only; no runtime tests were performed. Dango components retain GPL-3.0; Folia and the renderer adapter are AGPL-3.0; see the included licenses and NOTICE.md.
