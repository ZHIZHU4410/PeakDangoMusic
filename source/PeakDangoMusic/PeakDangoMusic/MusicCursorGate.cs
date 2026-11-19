using HarmonyLib;

namespace PeakDangoMusic;

// The game resets the cursor every Update, including after our plugin Update.
[HarmonyPatch(typeof(CursorHandler), "Update")]
internal static class MusicCursorGate
{
    [HarmonyPostfix]
    private static void Postfix() => Plugin.Instance?.KeepPlayerCursor();
}
