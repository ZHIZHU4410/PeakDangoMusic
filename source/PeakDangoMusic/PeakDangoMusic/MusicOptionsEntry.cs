using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PeakDangoMusic;

[HarmonyPatch(typeof(SharedSettingsMenu), "ShowSettings")]
internal static class MusicOptionsEntry
{
	private const string EntryName = "DangoMusicOptionsEntry";

	private static readonly List<GameObject> entries = new List<GameObject>();

	[HarmonyPostfix]
	private static void Postfix(SharedSettingsMenu __instance)
	{
		if ((Object)(object)Plugin.Instance == (Object)null)
		{
			return;
		}
		Transform settingsContentParent = __instance.m_settingsContentParent;
		if (!((Object)(object)settingsContentParent == (Object)null) && !((Object)(object)__instance.m_settingsCellPrefab == (Object)null))
		{
			Transform val = settingsContentParent.Find("DangoMusicOptionsEntry");
			if ((Object)(object)val != (Object)null)
			{
				((Object)val).name = "DangoMusicOptionsEntryRetired";
				((Component)val).gameObject.SetActive(false);
				entries.Remove(((Component)val).gameObject);
				Object.Destroy((Object)(object)((Component)val).gameObject);
			}
			entries.RemoveAll((GameObject entry) => (Object)(object)entry == (Object)null);
			GameObject val2 = Object.Instantiate<GameObject>(__instance.m_settingsCellPrefab, settingsContentParent);
			((Object)val2).name = "DangoMusicOptionsEntry";
			entries.Add(val2);
			val2.transform.SetAsFirstSibling();
			SettingsUICell component = val2.GetComponent<SettingsUICell>();
			((Behaviour)component).enabled = false;
			LocalizedText[] componentsInChildren = val2.GetComponentsInChildren<LocalizedText>(true);
			foreach (LocalizedText obj in componentsInChildren)
			{
				obj.autoSet = false;
				((Behaviour)obj).enabled = false;
			}
			((TMP_Text)component.m_text).text = "音乐播放器";
			if ((Object)(object)component.onlyOnMainMenu != (Object)null)
			{
				component.onlyOnMainMenu.SetActive(false);
			}
			CanvasGroup component2 = val2.GetComponent<CanvasGroup>();
			if ((Object)(object)component2 != (Object)null)
			{
				component2.alpha = 1f;
				component2.interactable = true;
				component2.blocksRaycasts = true;
			}
			GameObject val3 = new GameObject("OpenLyricsSettings", new Type[4]
			{
				typeof(RectTransform),
				typeof(Image),
				typeof(Button),
				typeof(LayoutElement)
			});
			val3.transform.SetParent(component.m_settingsContentParent, false);
			RectTransform val4 = (RectTransform)val3.transform;
			val4.anchorMin = new Vector2(1f, 0.5f);
			val4.anchorMax = new Vector2(1f, 0.5f);
			val4.pivot = new Vector2(1f, 0.5f);
			val4.sizeDelta = new Vector2(240f, 48f);
			LayoutElement component3 = val3.GetComponent<LayoutElement>();
			component3.preferredWidth = 240f;
			component3.preferredHeight = 48f;
			component3.minHeight = 48f;
			Image component4 = val3.GetComponent<Image>();
			((Graphic)component4).color = Color.white;
			Button component5 = val3.GetComponent<Button>();
			((Selectable)component5).targetGraphic = (Graphic)(object)component4;
			ColorBlock colors = ((Selectable)component5).colors;
			colors.normalColor = new Color(0.14f, 0.19f, 0.25f, 1f);
			colors.highlightedColor = new Color(0.2f, 0.29f, 0.37f, 1f);
			colors.pressedColor = new Color(0.055f, 0.085f, 0.12f, 1f);
			colors.selectedColor = colors.highlightedColor;
			colors.fadeDuration = 0.08f;
			((Selectable)component5).colors = colors;
			GameObject val5 = new GameObject("Label", new Type[2]
			{
				typeof(RectTransform),
				typeof(TextMeshProUGUI)
			});
			val5.transform.SetParent((Transform)(object)val4, false);
			TextMeshProUGUI component6 = val5.GetComponent<TextMeshProUGUI>();
			((TMP_Text)component6).font = ((TMP_Text)component.m_text).font;
			((TMP_Text)component6).fontSharedMaterial = ((TMP_Text)component.m_text).fontSharedMaterial;
			((TMP_Text)component6).text = "打开播放器";
			((TMP_Text)component6).fontSize = ((TMP_Text)component.m_text).fontSize;
			((Graphic)component6).color = Color.white;
			((TMP_Text)component6).alignment = (TextAlignmentOptions)514;
			((Graphic)component6).raycastTarget = false;
			((TMP_Text)component6).enableAutoSizing = true;
			((TMP_Text)component6).fontSizeMin = 16f;
			((TMP_Text)component6).fontSizeMax = ((TMP_Text)component.m_text).fontSize;
			((TMP_Text)component6).rectTransform.anchorMin = Vector2.zero;
			((TMP_Text)component6).rectTransform.anchorMax = Vector2.one;
			((TMP_Text)component6).rectTransform.offsetMin = new Vector2(12f, 4f);
			((TMP_Text)component6).rectTransform.offsetMax = new Vector2(-12f, -4f);
			((UnityEvent)component5.onClick).AddListener((UnityAction)(() =>
			{
				Plugin.Instance?.OpenSettings(__instance);
			}));
		}
	}

	internal static void Dispose()
	{
		foreach (GameObject entry in entries)
		{
			if ((Object)(object)entry != (Object)null)
			{
				Object.Destroy((Object)(object)entry);
			}
		}
		entries.Clear();
	}
}
