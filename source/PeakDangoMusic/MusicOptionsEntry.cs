using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeakDangoMusic;

// Both title-screen options and pause-menu options use SharedSettingsMenu.
[HarmonyPatch(typeof(SharedSettingsMenu), nameof(SharedSettingsMenu.ShowSettings))]
internal static class MusicOptionsEntry
{
    const string EntryName="DangoMusicOptionsEntry";
    static readonly List<GameObject> entries=new();

    [HarmonyPostfix]
    static void Postfix(SharedSettingsMenu __instance)
    {
        if(Plugin.Instance==null)return;
        var parent=__instance.m_settingsContentParent;
        if(parent==null || __instance.m_settingsCellPrefab==null)return;
        var old=parent.Find(EntryName);
        if(old!=null){old.name=EntryName+"Retired";old.gameObject.SetActive(false);entries.Remove(old.gameObject);UnityEngine.Object.Destroy(old.gameObject);}
        entries.RemoveAll(entry=>entry==null);
        var row=UnityEngine.Object.Instantiate(__instance.m_settingsCellPrefab,parent);
        row.name=EntryName;entries.Add(row);
        row.transform.SetAsFirstSibling();
        var cell=row.GetComponent<SettingsUICell>();
        // This row opens the plugin UI instead of binding a Zorro Setting.
        cell.enabled=false;
        foreach(var localized in row.GetComponentsInChildren<LocalizedText>(true)){localized.autoSet=false;localized.enabled=false;}
        cell.m_text.text="悬浮歌词";
        if(cell.onlyOnMainMenu!=null)cell.onlyOnMainMenu.SetActive(false);
        var group=row.GetComponent<CanvasGroup>();
        if(group!=null){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}
        var buttonObject=new GameObject("OpenLyricsSettings",typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));
        buttonObject.transform.SetParent(cell.m_settingsContentParent,false);
        var rect=(RectTransform)buttonObject.transform;
        rect.anchorMin=new Vector2(1,.5f);rect.anchorMax=new Vector2(1,.5f);rect.pivot=new Vector2(1,.5f);
        rect.sizeDelta=new Vector2(240,48);
        var layout=buttonObject.GetComponent<LayoutElement>();layout.preferredWidth=240;layout.preferredHeight=48;layout.minHeight=48;
        var image=buttonObject.GetComponent<Image>();image.color=Color.white;
        var button=buttonObject.GetComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;
        colors.normalColor=new Color(.14f,.19f,.25f,1);colors.highlightedColor=new Color(.20f,.29f,.37f,1);
        colors.pressedColor=new Color(.055f,.085f,.12f,1);colors.selectedColor=colors.highlightedColor;colors.fadeDuration=.08f;button.colors=colors;
        var labelObject=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(rect,false);
        var label=labelObject.GetComponent<TextMeshProUGUI>();
        label.font=cell.m_text.font;label.fontSharedMaterial=cell.m_text.fontSharedMaterial;
        label.text="打开设置";label.fontSize=cell.m_text.fontSize;label.color=Color.white;
        label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=cell.m_text.fontSize;
        label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
        label.rectTransform.offsetMin=new Vector2(12,4);label.rectTransform.offsetMax=new Vector2(-12,-4);
        button.onClick.AddListener(()=>Plugin.Instance?.OpenSettings(__instance));
    }

    internal static void Dispose()
    {
        foreach(var entry in entries)if(entry!=null)UnityEngine.Object.Destroy(entry);
        entries.Clear();
    }
}

// Escape belongs to the lyrics modal until it has closed, including its release frame.
[HarmonyPatch(typeof(PauseMenuHandler), "Update")]
internal static class MusicPauseNavigationGate
{
    [HarmonyPrefix] static bool Prefix()=>Plugin.Instance?.BlocksPlayerInput!=true;
}

[HarmonyPatch(typeof(MainMenuPageHandler), "Update")]
internal static class MusicTitleNavigationGate
{
    [HarmonyPrefix] static bool Prefix()=>Plugin.Instance?.BlocksPlayerInput!=true;
}
