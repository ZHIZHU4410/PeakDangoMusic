# Changelog

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
