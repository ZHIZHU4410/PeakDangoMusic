# PeakDangoMusic · Muonium PEAK 播放器

以 [MuoniumPlayer](https://github.com/zihe114514/MuoniumPlayer-multiversion) 的原生登录与曲库为核心，在 PEAK 内搜索、播放音乐，管理歌单、收藏和队列；地图歌词继续使用原有 Dango / Folia 渲染。

作者：**布洛芬（ZHIZHU4410 / BLF4410）** · Codex 辅助开发 · [支持作者](https://afdian.com/a/blf4410)

[完整安装包](https://github.com/ZHIZHU4410/PeakDangoMusic/releases/latest) · [更新记录](CHANGELOG.md) · [安装说明](packaging/安装说明.md) · [源码许可](SOURCE.md)

## 主菜单与界面

在 PEAK **主菜单右下角 → 音乐播放器**，或 **游戏选项 → 音乐播放器 → 打开播放器** 进入。

右 Shift 打开/收起完整播放器；关闭后以可调位置的顶部胶囊小窗显示当前音乐，反斜杠键（\） 隐藏/显示小窗，两种界面互相切换。播放器采用侧栏导航与底部常驻播放栏，包含：首页、搜索音乐、发现歌单、我的歌单、本地收藏、播放历史、播放队列、账号中心、悬浮歌词、设置。

支持上一首/下一首、暂停/继续、拖动进度、音量、顺序/随机/单曲循环。音乐开关独立于歌词显示；关闭悬浮歌词和关闭播放器窗口都不会停止音乐。停止本机音乐可点底部“停止”或在诊断页关闭游戏内播放器。

进入地图听歌时，每分钟可在附近看到当前歌曲的一条热门评论，评论留在生成处并朝向镜头。评论取自当前音源支持的评论接口。

进入地图时使用原版第一人称，视角切换键默认未绑定。在「设置 → 角色视角」中可直接选择视角，或点击「切换键」录入自定义按键。第二人称从角色前方回看自己，第三人称从背后跟随。

这是 PEAK 原生适配界面。Muonium 的 Minecraft/Skija 原界面无法直接在 Unity 中运行，因此按其播放器结构重做了导航、账号、曲库与播放交互；没有把 Minecraft 程序嵌入游戏。

## 平台、账号与歌单

- **网易云 · 原生**：原生搜索与播放、二维码登录、精选歌单、登录后的每日推荐和个人歌单，可通过歌单ID打开曲目。
- **QQ · 原生**：原生搜索与播放、QQ或微信扫码登录、推荐歌单和按ID打开歌单；已接入当前登录账号的个人歌单；真实账号权限仍需用户验收。
- **酷狗 · 原生**：搜索、推荐歌单、扫码登录与取流适配；账号权限和实际播放仍待验收。
- **哔哩哔哩 · 原生**：视频音乐搜索、扫码登录和音频解析；播放需要本机 FFmpeg，收藏夹尚未适配。
- **YouTube · 实验**：搜索、按ID打开歌单、本人浏览器 Cookie 登录；本机直连搜索为空，尚未验证可播。
- **LX · 本地脚本**：首页指定自己的 JS 文件；五个曲库渠道搜索，取流交给脚本。内置 Rhino 沙箱及 ES5 转译器，不打包用户脚本。
- **GD聚合**：保留全部10个平台入口，包括网易云、JOOX、哔哩哔哩、酷我、QQ、Tidal、Qobuz、Apple Music、YouTube Music、Spotify。聚合平台的实际开放情况由GD服务决定。

账号中心选择原生平台，生成二维码，用对应手机App扫描并确认。二维码在本机生成或直接采用平台返回图片，不交给第三方二维码服务。平台登录凭证使用 Windows 当前用户 DPAPI 加密保存到 `BepInEx/config/PeakDangoMusic-Accounts.dpapi`；退出当前账号会清除对应平台的保存状态。YouTube 使用本人浏览器 Cookie 导入，不提供账号密码输入。

收藏、历史与队列是本机功能，保存在 `BepInEx/config/PeakDangoMusic-Library.json`，不冒充云端同步。可在歌曲右侧收藏或加入队列。

歌曲播放仍受平台版权、账号等级和区域限制。登录不代表所有歌曲都可播放；平台无权限或取流失败会显示错误。部分音质格式不受Unity支持时，请使用普通MP3音质。

## 地图悬浮歌词

侧栏“悬浮歌词”保留原有地图渲染：Dango两侧漂浮、整句/单词/固定整句逐字显示，以及Folia全部15种样式、本地字体、昼夜字色、独立柔和光晕和翻译。原有配置继续保留；逐字显现沿用 2.1.2 算法；只有逐行 LRC 时仍需按行时长估算，无法补出音源未提供的逐字时间戳。

旧的 CMD 启动网易云/酷狗接口、LX/CDP/SMTC 桌面同步不再作为本版运行路径，不需要先启动外部音乐播放器或开放本机调试端口。旧脚本已备份并从新安装包移除。

## 安装

需要 Windows x64、PEAK Mono 和 BepInEx 5。下载 **PeakDangoMusic-2.4.0-complete.zip**，退出 PEAK，完整解压运行“一键安装.cmd”；手动安装复制 `BepInEx/plugins/PeakDangoMusic.dll` 和整个 `PeakDangoMusic` 文件夹。必须保留新的 `Muonium` 与原有 `Folia` 组件。

雷霆管理器用户将两项复制到当前Profile的 `BepInEx/plugins`，再Start modded；不要只复制DLL，不要保留重复版本。安装器会检查文件清单并备份旧组件。

运行时已内置校验过的 Temurin Java 21，用户无需安装 Java；它只作为隐藏子进程处理账号/曲库，不监听网络端口。新版本尚未发布到GitHub时，请使用本机已生成的对应安装包。

## 外观与环境音

设置页支持界面调色盘、歌词自定义字色和胶囊小窗的位置调整。环境音融合可按天气、水下和洞内状态调整音乐音量与滤波，强度可配置；游戏内效果仍需实机验收。

## 可选音频组件

哔哩哔哩和 YouTube 的普通 AAC 音频，以及 QQ 和网易云返回的部分音频格式，需要本机 FFmpeg 转码后播放。在“设置”填写 ffmpeg.exe 路径；留空检测 universal-modder 已安装的版本。安装包不包含 FFmpeg。YouTube 可填写专用 HTTP 代理，修改后重启游戏生效，其他平台不使用它。

原生平台的逐项实现与验收状态见 [平台状态](docs/PLATFORMS.md)。Spotify 按用户要求暂缓；汽水和 Apple Music 原生服务尚未完成。

## 验证与限制

验证记录见 [VERIFICATION.zh-CN.md](VERIFICATION.zh-CN.md)。扫码确认必须由用户本人完成；没有登录用户账号就不能宣称云端个人歌单及账号权限已实测通过。QQ原生搜索/取流和部分GD源可能受上游接口限制，页面会保留失败信息。

已知历史问题：个别用户关闭设置后鼠标点击失效、键盘仍可操作，尚未专门复现修复。

日志：`BepInEx/LogOutput.log`；普通配置：`BepInEx/config/local.peak.dangomusic.cfg`。反馈可用诊断页复制错误详情；请勿分享账号凭证文件或个人曲库文件。

## 构建与许可

需要 .NET SDK、JDK21+（设置JAVA_HOME或使用 `source/MuoniumBridge/build.py --jdk 路径`）、Node.js24+、npm和Python3。

```powershell
.\build.ps1 -GameRoot "你的PEAK安装目录"
python tools/package.py
```

完整对应源码单独提供。Muonium原生服务源码固定提交与修改清单见 [集成说明](docs/MUONIUM.md)，保留Apache-2.0；Dango/插件保留GPL-3.0，Folia保留AGPL-3.0；Temurin、Gson、ZXing的许可和来源随组件提供，见[NOTICE.md](NOTICE.md)。

关闭完整播放器后，即使未播放歌曲也保留胶囊入口。评论按实际平台ID请求：网易云、QQ、酷狗及哔哩哔哩；接口被拒会显示失败，未接入的平台不会借用网易云同名歌曲评论。
