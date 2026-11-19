using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace PeakDangoMusic;

[HarmonyPatch]
internal static class MusicHotbarGate
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(CharacterInput), "HotbarKeyWasPressed", (Type[])null, (Type[])null);
		yield return AccessTools.Method(typeof(CharacterInput), "HotbarKeyIsPressed", (Type[])null, (Type[])null);
	}

	[HarmonyPrefix]
	private static bool Prefix(ref bool __result)
	{
		Plugin? instance = Plugin.Instance;
		if (instance == null || !instance.BlocksPlayerInput)
		{
			return true;
		}
		__result = false;
		return false;
	}
}
