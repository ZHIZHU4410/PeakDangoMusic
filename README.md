# PeakDangoMusic · PEAK 悬浮歌词

在PEAK主菜单、机场、地图、加载和歌词设置界面持续显示同步歌词。保留原有Dango两侧悬浮文字，1.2.0新增[Folia](https://github.com/chthollyphile/folia-major)的全部15种内置歌词样式。

作者：**布洛芬（ZHIZHU4410 / BLF4410）**；Codex辅助开发。Dango来源与Folia原作者署名见[NOTICE.md](NOTICE.md)。

1.3.5 本地更新：默认按游戏时间统一切换字色，白天深色、夜晚浅色；不再读取背景画面。歌词设置的「昼夜自动字色」关闭后，可用「手动使用深色字」固定深色或浅色。两种字色都保留光晕；亮度只增强浅色字的字面，光晕强度与半径独立控制。

## 2.0 播放器适配

2.1.1 修复《雨爱》等酷狗原版KRC末行时长异常导致整首歌词被拒绝的问题，保留原版逐字时间。

在游戏选项 → 悬浮歌词 → 打开设置的「播放器来源」选择 BLF/LX、原版网易云、酷狗音乐或QQ音乐。2.1.0恢复四个入口，并保留2.0.3–2.0.6的菜单输入修复及进度保持。

- 酷狗：先从托盘完全退出，再运行安装包内「启动酷狗歌词连接.cmd」，打开「推荐」页并播放。通过本机9225端口读取原生播放API，不扫描窗口控件；每4.4秒读取一次，其间平滑推进歌词，失败暂停30秒再重试。无需账号Cookie或付费音源脚本。
- QQ音乐：读取 QQMusic 专属的 Windows 系统媒体会话，必须提供有效进度；当前本机未安装 QQ音乐，属于待实机验证适配。
- 酷狗优先只读本机 KRC/LRC 缓存（含原版逐字时间），自动查找固定磁盘的 KuGou/Lyric；自定义位置可在菜单填写「酷狗歌词缓存目录」。同歌名不同版本的缓存仍会拒绝混用。未缓存时可先在酷狗打开歌词，再点重新连接。QQ音乐优先按本平台曲名、歌手、时长匹配QQ公开LRC与翻译，再尝试备用曲库；QQ的逐字显示仍由行时间估算。
- 不控制播放器、不修改安装目录、不读取账户或播放地址；本地 LX 音源脚本及其密钥不会随包发布。

> QQ音乐仍未在本机安装；公开接口可独立检查，实际客户端的会话/进度兼容性仍需实机验收。

已知问题：个别用户关闭歌词设置后鼠标点击失效、键盘仍可操作；目前保留现状，尚未修复。

## 下载与使用

1. 在[Releases](https://github.com/ZHIZHU4410/PeakDangoMusic/releases/latest)下载完整安装包，完整解压。Source code压缩包只有源码，不能直接安装。
2. 需要Windows x64、PEAK Mono版及BepInEx5。退出PEAK，运行“一键安装.cmd”，选择PEAK.exe；也可按[安装说明](packaging/安装说明.md)手动复制完整DLL和PeakDangoMusic文件夹。
3. 重开游戏，**游戏选项 → 悬浮歌词 → 打开设置 → Folia歌词样式**选择；在菜单内用「启用漂浮歌词」开关显隐。原Dango样式仍为默认，已有播放器连接与歌词参数保留。

完整安装包仅包含使用所需文件。源码、测试和开发工具单独提供，不需要复制到游戏或雷霆配置中；见 [源码说明](SOURCE.md)。

### 雷霆管理器：本地手动安装

使用 Thunderstore Mod Manager（雷霆管理器）时，将插件安装到**当前配置文件夹**。

1. 退出 PEAK，在雷霆管理器中选择 **PEAK → 要使用的配置（Profile）**，确认该配置已安装 `BepInExPack_PEAK`。
2. 打开 **Settings（设置）→ Browse profile folder（浏览配置文件夹）**，进入其中的 `BepInEx/plugins`。找不到入口时可在设置中搜索 `profile`。[官方配置文件夹说明](https://wiki.thunderstore.io/sharing-your-mods/modpacks-and-profiles)
3. 从完整安装包解压后的 `BepInEx/plugins` 中，将 **`PeakDangoMusic.dll` 和整个 `PeakDangoMusic` 文件夹一起复制**到该目录。文件夹中的 `Native`、`Folia` 及其所有文件都要保留。使用本机已更新的版本时，也可以从游戏目录的 `BepInEx/plugins` 复制这两项。
4. 已装旧版时，先将旧插件备份到配置文件夹之外，再替换；检查 `plugins` 的其他子文件夹，避免保留重复的 `PeakDangoMusic.dll`。已有 `BepInEx/config` 中的歌词配置可以保留。
5. 返回雷霆管理器，点击 **Start modded（启动模组游戏）**。本地新版通过游戏 **选项 → 悬浮歌词 → 打开设置** 进入菜单。

最终目录结构：

```text
当前配置文件夹/
└─ BepInEx/
   └─ plugins/
      ├─ PeakDangoMusic.dll
      └─ PeakDangoMusic/
         ├─ Native/
         └─ Folia/
```

不要只复制 DLL，也不要把完整安装包的外层目录直接放进 `plugins`。包内“一键安装.cmd”用于游戏目录安装；使用雷霆配置时请按上述步骤复制。手动复制的插件可能不会显示在管理器的模组列表中，是否加载可查看该配置的 `BepInEx/LogOutput.log`。本方式尚未在管理器中实测。

## 1.3.3 游戏选项入口与歌词显示修复

取消 F8、F9 快捷键。主菜单和暂停菜单的游戏选项列表顶部新增「悬浮歌词」独立行，点击「打开设置」进入完整歌词菜单；显隐统一使用菜单内的「启用漂浮歌词」。Esc 或「返回选项」关闭歌词菜单，回到原游戏选项页。保留 1.2 风格、错开入场、视觉对齐与按下反馈。

修复逐字显示时中文两个字同时出现的问题；优先使用播放器时间，没有逐字时间时按单个字估算。抖动开关与幅度、频率集中设置，关闭时参数控件禁用。滑条右侧数字完整显示，拖动后与保存数值一致。已在游戏内两种窗口尺寸下检查小数、整数、Hz 与最长的 `-10000 ms`，并验证亮度右端为 `6.0`；30 项歌词回归检查通过。此验证不覆盖全部 Folia 样式或新版网易云兼容性。

## 1.3.2 网易云进度兼容测试

针对网易云 3.1.43 诊断报告中确认的内部模块位置变化，改为按已初始化接口结构查找曲目和进度，同时保留曲目 ID、时间范围及歧义校验。保留 3.1.42 读取方式，并增加相邻时间元素回退。歌词设置 播放状态会直接显示页面连接、WebSocket 和进度读取失败信息。模拟检查和构建通过，真实播放效果请在游戏内验收。安装时请完整更新 DLL 和 `PeakDangoMusic/Native` 文件夹。

## 1.3.1 界面风格调整

恢复 1.2 的平面窗口、面板和按钮风格，保留错开入场与按下变深反馈；统一选项按钮宽度、标签、滑块和数值列的视觉对齐。保留字体选择与歌词功能。构建和安装包检查通过，游戏内效果由用户测试。

## 1.3.0 字体与设置界面

新增本地字体选择；改进歌词设置设置排版、分组、按钮反馈与按下态。修复Dango逐字整句的字序和换位行为，并继续完善Folia样式适配。播放器和游戏内效果请在实际环境验收。

## 1.2.2 通信修复

已实际复现Windows GUI组件stdin立即EOF，导致退出和ERR_FAILED。改用随机双向命名管道，隐藏组件全部15种样式均已返回正确版本的非空帧，并核对了画面。此诊断只用示例歌词，没有启动PEAK或播放器；游戏内位置和性能仍由用户测试。DLL与App需完整更新并重开游戏。

## 历史1.2.1修复

针对ERR_FAILED(-2)取消Folia本机HTTP页面，改用安全专用协议读取安装资源；不再监听端口。修复样式切换旧帧、失败后仍保持就绪和超时不恢复的问题。DLL与App需要一起升级并重开游戏。错误详情写入BepInEx/PeakDangoMusic-Folia.log（128KiB滚动）。只编译打包，运行效果由用户测试。

## 歌词样式

商籁、凝彩、绘光、花火、流光、心象、云阶、浮名、倾诉、回环、莫奈、时计、群唱、镜台、静止，以及每首随机选择。直接构建上游原React/Pixi/Three渲染器，并非只给旧动画改名。[逐项对应与适配范围](FOLIA-STYLES.zh-CN.md)。

歌词设置提供两侧悬浮、两侧屏幕、全屏舞台；可调舞台大小、透明度、字体倍率、动效强度、画质及帧率。菜单和设置中自动接续显示，音乐暂停时保留当前位置。Folia准备或失败时回退Dango；选回Dango或关闭歌词开关时停止组件。

原播放器没有提供频谱、节拍／人声分析、封面或立绘时不伪造这些输入，相应附加视觉元素可能缺省。原项目的全部设置与第三方Folium模组不在此适配范围。

## 播放器连接

BLF/LX开启本机开放API，默认端口23330。原版网易云使用Windows媒体信息、可选本机进度接口及公开歌词接口；无进度时先完全退出网易云，再用安装包连接脚本启动。插件不需要Cookie或账号资料，不控制播放器、不下载音乐。

Folia组件仅用于本地离屏渲染，没有独立播放器窗口或键鼠接管。完整包附运行时，用户无需安装Node.js或Electron。首次选择样式会准备资源；默认960×540／24帧，可在歌词设置降低画质减少占用。歌词设置仍暂停本机游戏，其他联机玩家与播放器继续运行。

## 验证范围

**1.2.2已获授权仅诊断隐藏Folia组件的15种样式；未启动游戏或播放器。** 新样式画面、显卡兼容性、性能、各场景切换及多人效果由用户验收。历史记录见[VERIFICATION.zh-CN.md](VERIFICATION.zh-CN.md)。

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

Dango插件部分保留GPL-3.0；Folia及适配渲染器采用AGPL-3.0，发行组合依照两许可第13条。完整对应源码单独提供，许可随安装包保留，见 [SOURCE.md](SOURCE.md)。Electron、Chromium及前端依赖遵循随附声明。上游固定提交7a5f975e337e5ba13f55d72b5bc68983bf41c379；原Dango默认值对照见[SORIHIKO-DEFAULTS.zh-CN.md](SORIHIKO-DEFAULTS.zh-CN.md)。

反馈请附模组版本、所选样式、播放器类型、歌词设置状态和相关日志片段。日志位于BepInEx/LogOutput.log，配置为BepInEx/config/local.peak.dangomusic.cfg；提交前移除私人资料。

## English

PEAK floating lyrics with the original Dango mode plus all15 pinned Folia builtin visualizers. Open Options → 悬浮歌词 → 打开设置 → Folia styles. Includes side-positioned world/screen stages, fullscreen display, quality controls. Complete corresponding source is provided separately (see SOURCE.md). BLF/LX and optional stock NetEase use the existing read-only timeline. Dango remains the default and fallback. Unavailable audio-analysis/cover input is not synthesized.

Download the full-installer ZIP, not GitHub's generated Source code archives. Version1.2.1 was compiled and packaged only; no runtime tests were performed. Dango components retain GPL-3.0; Folia and the renderer adapter are AGPL-3.0; see the included licenses and NOTICE.md.

## 支持作者

如果这个模组对你有帮助，欢迎通过爱发电支持布洛芬，感谢你的鼓励！

<p align="center">
  <img src="docs/images/afdian-sponsor.jpg" alt="布洛芬4410的爱发电赞助二维码" width="360" />
</p>
