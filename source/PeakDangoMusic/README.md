# PeakDangoMusic 2.4.1 插件源码

PEAK 游戏内 Muonium 音乐播放器的 BepInEx 插件源码，C# / netstandard2.1。
界面和玩法看仓库根目录的 README.md，安装看 releases 里的完整安装包。

## 编译

需要 .NET SDK 8 或更高版本。工程引用游戏目录下的 BepInEx 与 Unity 程序集，默认路径是
相对工程推导的 PEAK 根目录，装在别处的这样传参：

    dotnet build PeakDangoMusic.csproj -c Release -p:GameRoot="你的PEAK目录"

编译产物在 `bin\Release\netstandard2.1\PeakDangoMusic.dll`，复制到
`BepInEx\plugins\` 覆盖旧文件，重启游戏生效。

## 目录

- `PeakDangoMusic/` 播放器主体
  - 界面：`MuoniumHub.cs`（侧栏、首页、歌单、搜索、队列等页面）、`MusicWindow.cs`（窗口外壳）、`UiKit.cs`（玻璃质感、胶囊、图标绘制）
  - 播放：`AggregatePlayer.cs`、`PlayerLibrary.cs`、`NativeMusicClient.cs`
  - 平台接口：`GdMusicApi.cs`、`MuoniumClient.cs`、`DailyRecs.cs`
  - 地图功能：`WorldComments.cs`（热门评论）、`PlayerPerspective.cs`（角色视角）
  - 纹理与缓存：`CoverCache.cs`、`AvatarCache.cs`、`ChartArt.cs`、`AuthorArt.cs`
- `DangoMusic.Core/` 逐字歌词的解析与逐帧排版
- `DangoMusic.Desktop/` LX 歌词文件解析
- `DangoMusic.Diagnostics/` 诊断日志的数据结构
- `PeakDangoMusic/assets/` 内置的榜单封面和作者头像，作为 EmbeddedResource 打进 DLL
- `GlobalAliases.cs`、`Properties/`、`System.Runtime.CompilerServices/` 编译需要的辅助文件

## 几条实现上的说明

- 登录态由 Muonium 桥接组件写入 Windows DPAPI 加密文件，源码不含任何账号数据。
- 榜单、搜索、评论走网易云和 QQ 音乐的公开接口；播放地址由本地桥接组件解析，不开监听端口。
- 评论用的 `https://music.163.com/api/v1/resource/comments/R_SO_4_{songId}`，不需要登录态也能读到。
- 播放队列最多 200 首；顺序模式的语义是播到队尾回到第 1 首，不是停住。

依赖的上游项目：MuoniumPlayer（Apache-2.0）、Folia（AGPL-3.0），许可见仓库的
NOTICE.md 与 SOURCE.md。
