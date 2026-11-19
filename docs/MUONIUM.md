# 2.3.2 平台适配补充

上游继续固定原提交。platforms 目录新增 Bilibili、YouTube、LX、酷我和咪咕服务及运行器；针对 Unity 修改 DTO/会话存储及普通音频解码适配。Bilibili/YouTube 会话仅通过宿主 DPAPI 快照保存，Java 不写 Cookie 文件。

LX 使用原版 Rhino 1.7.15 包名替代上游私有重定位名，保留 ClassShutter、解释执行指令预算及请求超时；Closure Compiler v20240317 随包安装，不在游戏内下载。使用官方 Maven 原包（未使用上游裁剪版），SHA256 固定于 dependencies.json。用户脚本仅由用户指定本地路径，不发布、不默认加载。

FFmpeg 为用户自行安装的可选组件，不随包再分发。仅转码普通授权音频，不复制 DRM 设备文件。准确平台状态见 PLATFORMS.md。以下为历史实现记录，现状以该表和 CHANGELOG 为准。

# 2.3.0 Muonium 主播放器适配

固定上游：https://github.com/zihe114514/MuoniumPlayer-multiversion/tree/9b4cc7e7bd143315da716075a6d024a0d4c0c81b

`source/MuoniumBridge/vendor`直接保留`muonium-common/.../core/music/source`中的23个Java文件。未复制上游账号文件。根LICENSE为Apache-2.0，完整文本随包提供。改动：MuNeteaseApi添加官方公开搜索回退，MuHttp添加4MiB读取上限；新增PeakMuoniumBridge为PEAK协议适配。

原生网易云/QQ服务由隐藏Java子进程执行，父进程退出后自动终止，无监听端口。UI为PEAK/Unity适配，沿用Muonium侧栏、曲库、账号和底部播放栏的结构，不宣称复用了Minecraft/Skija界面渲染器。

网易云/QQ二维码生成、状态轮询、用户确认、平台取流和歌词沿用原服务；用户账号权限制约照常保留。凭证仅在私有进程通信中传递，在插件内用DPAPI保护后保存，不进入普通配置/日志；账号退出只清除选中平台。QQ个人云端歌单列表尚未接入；已提供推荐歌单与ID打开。

界面包含本地收藏/历史/队列，默认顺序播放，可切随机或单曲循环。音乐开关与地图歌词开关独立。旧CMD/本机调试端口/桌面SMTC不再启动。地图歌词渲染器本身沿用既有实现。

依赖来源与SHA256固定在`source/MuoniumBridge/dependencies.json`：Temurin21.0.13+9、Gson2.13.2、ZXing3.5.3。Java运行时和依赖许可随包提供。

以下保留2.2.0的GD平台映射与参考。

# MuoniumPlayer 参考与集成范围

参考仓库：https://github.com/zihe114514/MuoniumPlayer-multiversion

固定提交：`9b4cc7e7bd143315da716075a6d024a0d4c0c81b`，2026-11-17 读取。

GD 平台清单与 GET 参数参考 `muonium-common/src/main/java/com/muoniumplayer/core/ncm/music/GdStudioMusicService.java`；后台隔离、500ms 采样、有限重启、真实进度要求参考 `ExternalMediaService.java` 和 `SmtcTimelineStabilizer.java`。PEAK 端采用 C# 和 Unity AudioSource 实现，未运行 Minecraft 或复制其运行时。

## 全部 GD 聚合平台

| API key | 平台 | 上游该提交的标记（非本次实测承诺） |
|---|---|---|
| netease | 网易云音乐 | 推荐 |
| joox | JOOX | 推荐 |
| bilibili | 哔哩哔哩 | 可播放，可能无歌词 |
| kuwo | 酷我音乐 | 实验性 |
| tencent | QQ音乐 | 暂未开放 |
| tidal | Tidal | 暂未开放 |
| qobuz | Qobuz | 暂未开放 |
| apple | Apple Music | 暂未开放 |
| ytmusic | YouTube Music | 暂未开放 |
| spotify | Spotify | 暂未开放 |

全部入口保留。GD 请求限额为滚动 50 次/5 分钟；单请求最多 12 秒、2MiB，失败平台冷却30秒。搜索每页20首；解析仅在同一曲目内按请求音质、320、128降级，不静默换成别的歌曲。歌词查询失败不阻断音频。歌曲地址和歌词均临时读取，不打包。

“全部 GD 聚合平台”不等于 MuoniumPlayer 的全部独立服务。本次没有移植其网易云/QQ登录、酷狗账号、Apple DRM、Spotify Connect、YouTube原生解码及LX脚本执行器。现有BLF/LX仍连接用户自己的播放器API；系统媒体会话可同步提供真实时间轴的其他播放器。

## 许可证

MuoniumPlayer 根目录 LICENSE 为 Apache-2.0，本次参考保留声明与完整文本：`docs/MUONIUM-APACHE-2.0.txt`。既有 Dango/Folia 许可保持随包提供。`GdMusicApi.cs` 已注明适配与修改。
