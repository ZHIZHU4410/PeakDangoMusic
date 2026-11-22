# 更新记录

## 2.4.1 (2026-11-23)

- 地图热评正文里的 QQ 音乐表情码会显示为腾讯提供的原版表情图片。

## 2.4.0 (2026-11-23)

- 在「设置 → 角色视角」添加切换键录入和清空功能。默认不绑定按键，进入地图时使用原版第一人称。
- 合入 2.3.5 的歌单、扫码、预缓存、自动续播与地图热评更新。热门评论生成于玩家附近，保持世界位置并朝向镜头。
- 支持者致谢放在开发作者页的四位主创下方。账号隐私说明移至账号中心。

## 2.3.5 (2026-11-22)

- 未播放时也显示胶囊入口，反斜杠仍可隐藏。
- 接入QQ个人歌单并修复空列表一直显示加载中；账号中心可选择QQ或微信扫码，登录类型与凭证一起DPAPI保存。
- 区分酷狗云歌单、用户listid和精选specialid，使用对应曲目接口，不再混用ID。
- 评论通过歌曲原平台ID请求，支持网易云、QQ、酷狗及哔哩哔哩适配；不跨平台按同名歌曲替换。匿名酷狗评论样本被上游拒绝，明确显示错误码，不伪装为空评论。
- 移除首页评论。地图内播放时每分钟在玩家附近生成一条热门评论，保持世界位置并朝向镜头。
- QQ 优先使用 MP3。各音源共用的自动续播会跳过解析或下载失败的队列曲目，直到有歌曲可播或尝试完队列。
- 开发作者页的四张主创卡片下方新增「作者的话」。地图内可按 F6 或通过设置切换第一人称、第二人称和第三人称。

## 2.3.4 (2026-11-19)

- 完整播放器/小窗切换键改为右 Shift，小窗隐藏/显示改为反斜杠键（\）；移除F10/F9绑定。
- 同步小窗提示、README及安装说明。
- 本次发布包含2.3.1–2.3.3尚未上传的修复和平台适配，真实可用范围见docs/PLATFORMS.md。
- 隐藏内容区纵向滚动条，歌单/我的歌单翻页不再露出白色滑块。
- 「连接与诊断」改为「设置」，顶部新增调色盘：饱和度/明度方块、色相条、实时预览、六个预设和保存按钮；选色写入配置，应用到窗口底色、卡片玻璃、按钮和输入框，拖动松手即生效。
- 开发作者页去掉版本号，补上三人分工，点卡片直接跳抖音主页；标题栏左侧新增QQ群胶囊。
- 新增环境音融合：读 WindChillZone 与 StormAudio 判断下雨/暴雪/刮风，按类型分别压低音量（雨 50%、雪 20%、风 32%）并做两级低通（雪压到 380Hz）、轻微降调（雪 -5%）、加回声；再叠 4~8 秒阵风起伏和左右摆动。玩家躲进室内/遮蔽时这一整套自动衰减到三成，歌恢复正常。强度可在设置里调 0.3~1.6。

## 2.3.3 (2026-11-19)

- 修复歌单网格在Layout与Repaint阶段计算高度不一致，导致滚动条提前到底。
- 评论换一条提示增加固定底部区域、字号及行高。
- F10打开/收起播放器，显示鼠标并在关闭时恢复；F9仅控制小窗可见性。
- 完整播放器关闭后平滑缩至左侧中间小窗，两种视图互斥，反向切换沿用当前动画位置。
- 拖动进度只预览，保持原播放进度，松手才执行一次跳转。
- 直接沿用2.1.2原始YRC解析及逐字动画链路；原生接口保留原始YRC传入C#，不经Java显示行转译。仅LRC仍无法保证真实逐字对齐。

## 2.3.2 (2026-11-19)

- 接入哔哩哔哩原生搜索、二维码、账号和视频音频解析；AAC 通过本机 FFmpeg 转为 Unity 可播 MP3，仅允许读取模组自己的音频缓存。
- 新增 YouTube 原生服务适配：搜索、歌单ID、Cookie导入和取流；当前直连搜索为空，标记为实验，未宣称播放已验证。
- 新增 LX 本地脚本运行器，复用上游 Rhino 沙箱、超时和 ES5 转译，提供网易云/QQ/酷狗/酷我/咪咕曲库渠道；未执行、复制或打包用户付费脚本。
- 新增可选 FFmpeg 路径和 YouTube 专用 HTTP 代理配置，保留 2.3.1 全部界面/封面/头像及 2.1.2 逐字算法修复。
- Spotify 按用户要求暂缓；汽水、Apple Music 原生功能仍未完成。查看 docs/PLATFORMS.md 的准确范围。

# Changelog

## 2.3.1 (2026-11-18)
- 固定内容滚动区的实际高度并显示滚动条，移除滚到末尾自动跳页，歌单可完整滚到底；分页改为明确的箭头操作。
- 歌词模式说明允许换行并按内容保留高度；按下按钮使用当前浅色的加深色，取消黑色按压块。
- 原生桥接响应补齐歌曲cover和账号avatar字段；修复头像请求失败后切换URL不重试的问题，限制封面并发并冷却失败请求。
- 按要求恢复2.1.2的LyricAnimation/LyricTokens/LyricUnicode/LyricLineView，保留新版其余界面；真实逐字时间优先，LRC仍为估算。
- 字体素材目录默认留空，启动时只迁移旧的硬编码默认路径，保留用户其他自定义目录和选中字体。
- 平台移植继续进行：新增Muonium酷狗原生服务、推荐/歌单/扫码/搜索/取流适配，凭证进入Windows DPAPI；搜索已返回20首，未登录取流样本失败，真实登录与播放待验收。
- Spotify按用户要求暂缓。原生YouTube/哔哩哔哩/汽水/Apple/LX尚未完成移植，不能把现有GD入口视为这些原生能力。

## 2.3.0 跨电脑合并 (2026-11-18)
- 合并用户提供的另一台电脑完成的2.3.0 C#插件，以其界面和功能实现为准；保留本机Java登录/曲库桥接器、Folia运行组件、许可与安装工具。
- 接入玻璃风格UiKit、F9开关、封面/头像缓存、榜单/每日推荐/评论和作者素材；保留队尾回到第1首的顺序队列语义。
- 适配拆分后的C#目录、嵌入资源与本机GameRoot；更新既有回归项目引用，319项检查通过。
- Release构建0错误，保留导入源码的508条可空性等编译警告；未在本次合并中启动游戏或登录用户账号。

## 2.3.0 (2026-11-17)
- Muonium播放器成为主功能：主菜单直接入口、侧栏首页/搜索/发现/歌单/收藏/历史/队列/账号/歌词设置和底部常驻播放栏。
- 直接复用固定版本的Muonium网易云/QQ原生服务，接入真实二维码登录、原生搜索取流、推荐歌单和账号状态；网易云支持个人歌单与每日推荐。QQ个人云端歌单列表尚未适配，界面明确说明。
- 登录态通过Windows当前用户DPAPI加密；二维码本地生成，账号密码不经过模组输入界面。收藏、队列、历史本机持久化。
- 内置校验的Temurin21、Gson和ZXing，后台stdio通信，无本机监听端口。修复匿名网易云weapi搜索拒绝时的官方公开目录回退。
- 停用旧CMD/LX/CDP/SMTC桌面同步运行路径，旧启动脚本备份并从新安装包移除；保留全部10个GD聚合入口。
- 音乐播放与悬浮歌词开关分离；地图内Dango/Folia渲染继续沿用原实现。

## 2.2.0 (2026-11-17)
- 新增游戏内 GD 聚合音乐：全部10个平台入口、每页20首搜索、点击播放、暂停/继续、停止、进度拖动、音量与音质控制。聚合音频时间轴供给现有 Dango/Folia 歌词。
- 保留各平台返回的歌曲与歌词标识；共享50次/5分钟限流、12秒/2MiB请求限制、失败冷却、同曲音质降级、切换来源取消旧请求。歌词失败不阻止音频播放。
- 参考 MuoniumPlayer 的后台隔离和有限重试：500ms媒体采样，辅助组件连续失败3次等待手动重连，新歌无真实时钟不猜测零起点，进度缺失最多外推30秒。
- 新增通用系统媒体会话入口；保留BLF/LX、网易云、酷狗和QQ同步。
- README专注项目和使用介绍，更新内容独立放在本文件；补充Muonium来源、许可与平台实测范围。
- 验证：插件/桥接器编译，307项现有回归、9项聚合检查通过；网易云/JOOX搜索和网易云地址/歌词接口通过。未完成Unity实际音频与地图验收；七个平台本次接口返回400，哔哩哔哩样本为空。

## 2.1.4 (2026-11-16)
- Replace generic connection/render errors with module, failing operation, exception cause and application stack location. Source line numbers are shown when available; reporting sites are labeled explicitly as a fallback.
- Add a bounded recent-error panel on the left, timestamps, repeated-error counts, copy and clear actions. Repeated polling does not count as new errors; genuine native API retries update the error record.
- Preserve underlying native-player/lyrics/Folia errors, including a failed QQ lookup before successful fallback. Redact URL credentials/query strings and sensitive headers from details.

## 2.1.3 (2026-11-16)
- Remove hard outlines from lyric faces and translations in world, screen and compatible text rendering. Preserve the independently controlled soft glow.
- No runtime or automated tests were run, as requested.

## 2.1.2 (2026-11-13)
- Add a fixed bottom-left 支持我 button to the lyrics settings window. Clicking opens the author's Afdian page in the default browser: https://afdian.com/a/blf4410 .

## 2.1.1 (2026-11-09)
- Fix rejection of original Kugou KRCs whose final row mistakenly stores the full song length as its duration (reproduced with 雨爱). Bound only the final hold when all original word timings fit the playing track; preserve every word timestamp. Continue rejecting genuinely late words and corrupt earlier rows.
- Add six duration regression checks; 305 assertions pass.

## 2.1.0 (2026-11-09)
- Restore Kugou/QQMusic desktop synchronization entries on the 2.0.6 baseline; saved source2/3 now activates the chosen player again.
- Preserve MusicWindow input recovery and 2.0.5 pause/clock hold. Stabilize artist identity when SMTC alternates partial singer lists.
- Add QQ-native public catalog, LRC and translation lookup before backup catalog matching, following RunicMite and Sorihiko source-layer design. No login/audio playback features are added.
- Keep Kugou local KRC/LRC priority, buffered background sampling and independent200ms stdout heartbeat.
- Remove Kugou UI Automation entirely. Use its native read-only status API over persistent loopback CDP, with 4.4s sampling, one in-flight request and 30s failure cooldown. Ship a Windows PowerShell-compatible launcher using `--remote_debug_port=9225`.
- QQ desktop client is not installed here; public HTTP and parsing tests do not substitute for client/game acceptance.


## 2.0.6 (2026-11-09)
- Temporarily hide the KuGou Music and QQ Music source entries in the settings panel; only BLF/LX and NetEase Cloud Music are shown. All KuGou/QQ code, bridge paths, KRC cache reading and public lyric matching remain in the build.
- Existing configs with source 2/3 fall back to BLF/LX at startup without deleting or narrowing the stored value; the config range stays 0–3 so the entries can be restored later by changing the visible option list only.
- Complete runtime package rebuilt with the 2.0.5 KuGou progress hold and the current bridge.

## 2.0.5 (2026-11-09)
- KuGou: fix the "lyrics show for a while then disappear" connection gap. The bridge now holds the last valid metadata/progress and keeps the last real clock advancing until the track ends, instead of nulling the position after a 2–3 second UI Automation/SMTC gap.
- Accept an empty artist in KuGou samples, seed an unknown new-track clock at zero only once, and freeze/resume at the extrapolated position rather than at the old raw sample.
- Verified the hold logic with a standalone probe (10s/60s extrapolation, null-position sample, pause freeze). The official KuGou 20.1 CEF build does not expose a CDP port (tested 9222 and local 8020), so the NetEase-style `--remote-debugging-port` route is not available for the stock client; SMTC + UI Automation with extrapolation is the reliable path.
- Lyrics mapping remains local KRC cache first, then public search; no change to the matching policy.

## 2.0.4 (2026-11-09)
- Replace the temporary EventSystem input-module pause with a non-invasive full-screen raycast blocker plus `sendNavigationEvents=false`: the native EventSystem and its module stay enabled for PEAK and every other mod.
- Keep the 2.0.3 guarantees: no `Time.timeScale`/`Cursor` writes, close request consumed outside OnGUI, owner-loss and 3-second no-draw watchdogs, draw exceptions logged instead of blocking close.
- Only call legacy `UnityEngine.Input` while the modal is open and the new Input System keyboard is missing, avoiding per-frame exceptions in new-input-only projects.
- Fix the "settings modal leaves every gameplay interaction key dead" report without touching any global UI/input component.

## 2.0.3 (2026-11-09)
- Fix the settings modal leaving PEAK's gameplay interaction keys dead after it was opened from Options and closed: the modal no longer disables the native EventSystem, and never changes Time.timeScale or Cursor state (both are owned by PEAK).
- Pause only the EventSystem input module while the modal is open and restore it on close; keep EventSystem.current valid for the game's navigation handlers.
- Process the close button outside OnGUI, add an owner-loss watchdog and a 3-second "modal cannot draw" watchdog so an IMGUI/layout failure can never keep CharacterInput blocked.
- Catch panel drawing exceptions, clear IMGUI keyboard focus, and log open/close for future diagnosis.


## 2.0.1 (2026-11-09)
- Prefer matching local Kugou KRC/LRC lyrics before cross-catalog search. Decode the user's own cache in memory and retain word timings; never redistribute lyrics.
- Automatically discover KuGou/Lyric on fixed disks, with an optional cache-folder setting. Verify title/artist/version and reject conflicting cached timelines.
- Fix the screenshot examples: local caches yield 49, 37 and 58 synchronized rows respectively. Cross-platform singer/catalog differences no longer block these songs.
- Separate absent local/public matches from the earlier misleading generic ambiguity message.


## 2.0.0 (2026-11-09)
- Add isolated Kugou and QQMusic media-session sources while preserving BLF/LX and NetEase source ids.
- Add Kugou 20.1 UI Automation 3 progress fallback for its zero SMTC timeline, including song-detail view. Read-only centisecond conversion, metadata cross-check, and pause state from SMTC.
- Use conservative public lyric matching; never invoke a paid LX source script or include its key.
- Four source choices use a two-column grid. Existing day/night colors and independent glow are retained.
- Kugou live chain: 28 valid position packets and 60 synchronized lyric lines. QQMusic not installed, so only routing and isolation tests; valid SMTC timeline required.


## 1.3.5 (2026-11-07)
- Replace background sampling with one global game-clock decision: dark during the game-defined daytime, light at night.
- Add an automatic day/night toggle and manual dark/light override in display settings.
- Keep glow enabled for both colors; use a cool halo for dark text without increasing the dark face brightness.
- Apply face color in all TMP fallback/atlas materials; keep vertex RGB white and animate alpha to prevent mixed colors.
- Add an independent soft halo for PEAK's Mobile/Distance Field shader, which has no native glow pass; explicitly enable OUTLINE_ON.
- Verified in a real-game Title render harness: day/night, near/far/retreat, both manual overrides and settings UI. Production build has 0 warnings/errors and 252 regression assertions pass. No gameplay map or all-preset Folia acceptance claimed.


## 1.3.4 (2026-11-07, local update)
- Adapt lyric/translation colors to the actual sentence area, with read-only PEAK time, sun, rain and snow fallback.
- Sample a 160x90 background asynchronously at 4 Hz; median filtering, hysteresis and a short transition reduce flicker. No screenshots are saved or transmitted.
- Add contrasting outlines to TMP and screen text, and contrasting shadows to legacy text. Preserve dark text contrast at high brightness; suppress glow for dark text.
- Pass adaptive primary/secondary colors and daylight state to transparent Folia themes; retain light text on Folia-owned dark backgrounds.
- Plugin and Folia builds pass; 238 regression assertions pass. Actual gameplay, GPU readback orientation and all Folia presets still require in-game validation.


## 1.3.3 (2026-11-06)
- Move the complete lyrics menu into title/pause game options; remove F8/F9 shortcuts and control lyric visibility inside the menu.
- Fix paired-character fallback in character reveal and full-line pop; retain full-line positioning and prefer supplied lyric timestamps.
- Group the wiggle switch with its frequency/amplitude controls and disable those controls while wiggle is off.
- Fix slider number rendering and scroll-viewport clipping; keep values aligned, snapped to displayed precision and correct at endpoints.
- Include Thunderstore Mod Manager local manual installation steps in README and the installation guide.
- Verified in the real game at two window sizes: complete numbers, decimals, Hz and -10000 ms; brightness drag reaches 6.0. Thirty pure lyric regression checks pass. This UI check does not revalidate all Folia presets or NetEase compatibility.

## 1.3.2 (2026-11-05)
- Discover initialized NetEase playback exports by interface shape instead of relying on 3.1.42 module numbers; addresses the moved store observed in a 3.1.43 diagnostic report.
- Show local page/WebSocket/read failures directly in the F9 playback status.
- Add a read-only playback-clock fallback for clients that show elapsed and total time in separate nearby elements.
- Preserve the verified 3.1.42 native-cache path and reject ambiguous or incomplete clocks.
- Simulated CDP expression checks and builds pass; NetEase 3.1.43 playback still requires verification on that client.

## 1.3.1 (2026-11-03)
- Restore the 1.2 flat window, panel and button appearance.
- Retain staggered entrance, darker pressed feedback and existing lyric/font features.
- Align equal-width options, row labels, sliders, values and toggle controls.
- Include README image assets in the complete installer.
- Build and archive validation only; in-game testing is performed by the user.

## 1.3.0 (2026-11-02)
- Add local lyric font selection with fallback and refresh controls.
- Restore sentence-stable character-by-character Dango lyrics and improve timing alignment.
- Improve F9 settings layout, grouped controls, visual feedback, and darker pressed buttons.
- Keep Folia's 15 presets and update the bundled renderer adapter.
- Build and packaging checks are automated; in-game visual validation remains with the user.

## 1.2.2 (2026-10-29)
- Reproduce Windows GUI stdin EOF closing the renderer during navigation; replace stdio with private duplex named pipe.
- Confirm 15/15 hidden preset renderers produce nonempty current-revision bitmap frames with sample lyrics.
- No game or music player launched; game display/performance remains user validation.

## 1.2.1 (2026-10-29)
- Replace failed loopback HTTP loading with bundled-only secure protocol resources.
- Separate bridge bootstrap, confirm current preset first frames and reject stale frames.
- Restore Dango on errors; recover from startup timeout; persist bounded renderer logs.
- Require paired DLL/App protocol. Build only; user performs runtime tests.


## 1.2.0 (2026-10-28)
- Add all15 pinned Folia builtin renderers, a local offscreen adapter and world/screen stage display.
- Add F9 style picker, per-song random choice, stage placement, opacity, size and quality controls.
- Retain Dango defaults and fallback. Include AGPL corresponding source and runtime notices.
- Compiled only; user performs runtime tests.

## 1.1.2 (2026-10-27)
- Keep the same lyric timeline visible across title, airport, gameplay, loading and settings screens.
- Use a non-interactive screen overlay while menus are open or the scene camera/character is unavailable.
- Retain the current lyric while the player is paused; add timeline/line/render diagnostics to F9.
- Player interfaces and network settings unchanged. Compiled only; no runtime tests.

## 1.1.1 (2026-10-27)
- Restore side placement and align supported lyric defaults with Sorihiko/DangoMusic commit da696d0.
- Scale 1, distance 10, spread 30 degrees; follow/stay-on-screen off, keep-away/face-player on.
- Glow strength 0.3/radius 0.5, warm text color, original wiggle ranges, CJK split-lines and fallback word timing.
- Add restore-original-defaults button and an explicit engine-adaptation/default mapping.
- Compiled only, no new gameplay or player tests.

## 1.1.0 (2026-10-27)
- Center lyrics higher in the forward view and update after the final camera pose each frame.
- Optional camera following; world-fixed retargeting preserves the current lyric and animation timing.
- Scale F9 for resolution, remove horizontal clipping, enlarge controls and keep independent column scrolling.
- Limit pause to ready gameplay scenes so menu/loading can finish; preview returns to gameplay and also works at the airport.
- Add optional stock NetEase Windows/loopback integration in an isolated, self-contained helper. Not run or tested, as requested.
- Include complete source, backup/uninstall scripts and Thunderstore listing metadata; no upload performed.

## 1.0.5
- Larger two-column F9 settings page.
## 1.0.4
- F9 local pause and input gating.
## 1.0.3
- White HDR lyrics with adjustable brightness.
