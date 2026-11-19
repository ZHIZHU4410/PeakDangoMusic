using System;
using HarmonyLib;

namespace PeakDangoMusic;

[HarmonyPatch(typeof(CharacterInput), "Sample")]
internal static class MusicInputGate
{
	private static readonly Action<CharacterInput> Reset = AccessTools.MethodDelegate<Action<CharacterInput>>(AccessTools.Method(typeof(CharacterInput), "ResetInput", (Type[])null, (Type[])null), (object)null, true);

	[HarmonyPrefix]
	private static bool Prefix(CharacterInput __instance)
	{
		Plugin? instance = Plugin.Instance;
		if (instance == null || !instance.BlocksPlayerInput)
		{
			return true;
		}
		Reset(__instance);
		__instance.pauseWasPressed = false;
		__instance.scrollInput = 0f;
		__instance.pingWasPressed = false;
		__instance.selectSlotForwardWasPressed = false;
		__instance.selectSlotBackwardWasPressed = false;
		__instance.unselectSlotWasPressed = false;
		__instance.selectBackpackWasPressed = false;
		__instance.scrollBackwardWasPressed = false;
		__instance.scrollForwardWasPressed = false;
		__instance.scrollBackwardIsPressed = false;
		__instance.scrollForwardIsPressed = false;
		__instance.spectateLeftWasPressed = false;
		__instance.spectateRightWasPressed = false;
		return false;
	}
}
