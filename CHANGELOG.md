# Changelog

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
