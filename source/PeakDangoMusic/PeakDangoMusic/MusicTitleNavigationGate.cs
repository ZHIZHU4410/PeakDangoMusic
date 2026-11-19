using HarmonyLib;

namespace PeakDangoMusic;

[HarmonyPatch(typeof(MainMenuPageHandler), "Update")]
internal static class MusicTitleNavigationGate
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
