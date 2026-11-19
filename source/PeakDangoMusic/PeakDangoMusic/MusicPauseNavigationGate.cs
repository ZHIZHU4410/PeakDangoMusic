using HarmonyLib;

namespace PeakDangoMusic;

[HarmonyPatch(typeof(PauseMenuHandler), "Update")]
internal static class MusicPauseNavigationGate
{
	[HarmonyPrefix]
	private static bool Prefix()
	{
		Plugin? instance = Plugin.Instance;
		if (instance == null)
		{
			return true;
		}
		return !instance.BlocksPlayerInput;
	}
}
