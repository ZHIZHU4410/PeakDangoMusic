# PeakDangoMusic 1.2.0 · Folia styles

The optional Folia renderer directly builds all15 builtin visualizers from chthollyphile/folia-major commit7a5f975e337e5ba13f55d72b5bc68983bf41c379 (0.7.23). Original copyrights and AGPL-3.0 license are retained in source/FoliaRenderer/vendor/folia-major. PEAK's offscreen frontend/IPC adapter is AGPL-3.0; its source, lockfile and runtime assembly script accompany the binary. Folia is not endorsed by or affiliated with this PEAK adaptation.

The Dango/PEAK plugin parts retain GPL-3.0. The distribution combines GPLv3 and AGPLv3 components under section13 of both licenses, retaining the respective licenses and corresponding source. See FOLIA-LICENSE.txt, LICENSE, FOLIA-STYLES.zh-CN.md and source/FoliaRenderer/README.md. Electron is MIT-licensed; its LICENSE and LICENSES.chromium.html ship with the runtime. Frontend dependency notices are in Folia/App/THIRD-PARTY-NOTICES.txt.

The new renderer reads the existing local player timeline. No account, playback-control API or audio download capability is introduced. It has no visible browser/player window. Missing spectrum, beat analysis or cover input is not fabricated. This revision was compiled and packaged only; no game, player or renderer runtime tests were performed.

# PeakDangoMusic 1.1.2

Version 1.1.2 removes the gameplay-scene-only lyric gate. A non-interactive screen layer continues the same lyric timeline across menus, loading and settings; ordinary gameplay retains side-positioned world lyrics. Pausing the player retains the current line. Player interfaces and network settings are unchanged. No runtime tests were performed for this revision.

# DangoMusic for PEAK · source and credits

1.1.1 aligns supported lyric defaults with Sorihiko/DangoMusic LyricsModule.kt at commit da696d0871fa348323782d7d299ba10df46e07db. LyricLineView ports its GPL LyricSplit rules, and fallback word reveal timing follows the same source. See SORIHIKO-DEFAULTS.zh-CN.md for the complete mapping and engine-specific differences. At the user's request, placement favors alternating left/right positions within the upstream spread setting. No game or interface tests were run for this revision.

Public display author: 布洛芬 (ZHIZHU4410). Version 1.1.0 adds a separate self-contained Windows helper for stock NetEase Cloud Music, adapting the existing DangoMusic Desktop SmtcReader, CdpProgressReader, PublicLyricsClient, SongMatcher and DesktopPlayback source under GPL-3.0. Full corresponding helper source is included in source/NetEaseBridge. This new player integration was not run or tested in this release, as requested.

The helper includes Microsoft .NET runtime and C#/WinRT components. Their licenses and third-party notices accompany the native executable. The helper is only started for the explicitly selected stock NetEase source; it exits with its parent game. No player, music, account data or game binaries are distributed.

PEAK adaptation by ZHIZHU4410 with Codex assistance. Based on the installed DangoMusic 0.9.1 Dead Cells desktop/world-lyrics mod, originally evolved from Sorihiko's DangoMusic. Adapted GPL-3.0 timeline, Unicode normalization, lyric token/animation and LX lyric parser are in ThirdParty/DangoMusic. Changes: .NET Standard compatibility, Unity/TMP 3D rendering and settings. No Dead Cells binaries or assets are included.

This standalone PeakDangoMusic component uses GPL-3.0; LICENSE and complete buildable source accompany it. The existing PEAK menu retains its own license and discovers the optional settings panel by reflection. System fonts are loaded from Windows and are not bundled. BLF/LX's local API is used read-only, without copying its code, accounts or music resources.
