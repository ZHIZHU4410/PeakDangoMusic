# Changelog

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
