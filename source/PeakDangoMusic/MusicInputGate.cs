using HarmonyLib;

namespace PeakDangoMusic;

// Unity still samples input with timeScale=0. Keep settings clicks and Esc out of gameplay.
[HarmonyPatch(typeof(CharacterInput),"Sample")]
internal static class MusicInputGate
{
    static readonly Action<CharacterInput> Reset=AccessTools.MethodDelegate<Action<CharacterInput>>(AccessTools.Method(typeof(CharacterInput),"ResetInput"));
    [HarmonyPrefix]
    static bool Prefix(CharacterInput __instance)
    {
        if(Plugin.Instance?.BlocksPlayerInput!=true)return true;
        Reset(__instance);
        __instance.pauseWasPressed=false;
        __instance.scrollInput=0;
        __instance.pingWasPressed=false;
        __instance.selectSlotForwardWasPressed=false;__instance.selectSlotBackwardWasPressed=false;
        __instance.unselectSlotWasPressed=false;__instance.selectBackpackWasPressed=false;
        __instance.scrollBackwardWasPressed=false;__instance.scrollForwardWasPressed=false;
        __instance.scrollBackwardIsPressed=false;__instance.scrollForwardIsPressed=false;
        __instance.spectateLeftWasPressed=false;__instance.spectateRightWasPressed=false;
        return false;
    }
}

[HarmonyPatch]
internal static class MusicHotbarGate
{
    static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CharacterInput),"HotbarKeyWasPressed");
        yield return AccessTools.Method(typeof(CharacterInput),"HotbarKeyIsPressed");
    }
    [HarmonyPrefix]
    static bool Prefix(ref bool __result)
    {
        if(Plugin.Instance?.BlocksPlayerInput!=true)return true;
        __result=false;
        return false;
    }
}
