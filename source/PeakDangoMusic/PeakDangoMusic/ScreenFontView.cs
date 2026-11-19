using System;
using System.Collections.Generic;
using System.Linq;
using DangoMusic.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeakDangoMusic;

internal sealed class ScreenFontView : IDisposable
{
	private readonly MusicSettings settings;

	private readonly AdaptiveLyrics.Palette palette = new AdaptiveLyrics.Palette();

	private readonly HashSet<Material> ownedMaterials = new HashSet<Material>();

	private readonly TextHalo mainHalo = new TextHalo();

	private readonly TextHalo translationHalo = new TextHalo();

	private FontLibrary.Asset? asset;

	private GameObject? root;

	private TextMeshProUGUI? text;

	private TextMeshProUGUI? translation;

	private LyricAnimation? previous;

	private TMP_MeshInfo[] original = Array.Empty<TMP_MeshInfo>();

	private int[] mapping = Array.Empty<int>();

	private bool failed;

	private int width;

	private int height;

	private float scale;

	internal ScreenFontView(MusicSettings settings)
	{
		this.settings = settings;
	}

	internal void Hide()
	{
		if ((Object)(object)root != (Object)null)
		{
			root.SetActive(false);
		}
	}

	internal bool Draw(LyricAnimation animation, long position, int serial, string translated)
	{
		if (failed)
		{
			return false;
		}
		try
		{
			if ((Object)(object)root == (Object)null)
			{
				asset = new FontLibrary.Asset(settings.FontPath.Value);
				root = new GameObject("DangoMusic custom-font screen", new Type[2]
				{
					typeof(RectTransform),
					typeof(Canvas)
				});
				Object.DontDestroyOnLoad((Object)(object)root);
				Canvas component = root.GetComponent<Canvas>();
				component.renderMode = (RenderMode)0;
				component.sortingOrder = 32765;
				text = Create("Lyrics");
				translation = Create("Translation");
			}
			root.SetActive(true);
			if (previous != animation || width != Screen.width || height != Screen.height || scale != settings.FontSize.Value)
			{
				previous = animation;
				width = Screen.width;
				height = Screen.height;
				scale = settings.FontSize.Value;
				((TMP_Text)text).fontSize = Mathf.Clamp((float)height / 1080f * 28f * scale, 16f, 84f);
				((TMP_Text)text).text = string.Concat(animation.Graphemes.Select((LyricGrapheme g) => g.Text));
				((TMP_Text)text).rectTransform.sizeDelta = new Vector2((float)width * 0.32f, (float)height * 0.3f);
				((TMP_Text)text).ForceMeshUpdate(true, true);
				original = ((TMP_Text)text).textInfo.CopyMeshInfoVertexData();
				mapping = new int[((TMP_Text)text).textInfo.characterCount];
				for (int num = 0; num < mapping.Length; num++)
				{
					int index = ((TMP_Text)text).textInfo.characterInfo[num].index;
					int num2;
					for (num2 = 0; num2 + 1 < animation.Graphemes.Count && animation.Graphemes[num2 + 1].SourceStart <= index; num2++)
					{
					}
					mapping[num] = num2;
				}
			}
			Vector2 val = new Vector2((float)width * ((serial % 2 == 0) ? 0.22f : 0.78f), (float)height * 0.39f);
			((Transform)((TMP_Text)text).rectTransform).position = (Vector2)(val);
			palette.Update(new Vector2(val.x / (float)width, (val.y - ((TMP_Text)text).preferredHeight * 0.5f) / (float)height));
			Material[] fontMaterials = ((TMP_Text)text).fontMaterials;
			foreach (Material val2 in fontMaterials)
			{
				ownedMaterials.Add(val2);
				LyricMaterial.Apply(val2, settings, palette);
			}
			fontMaterials = ((TMP_Text)translation).fontMaterials;
			foreach (Material val3 in fontMaterials)
			{
				ownedMaterials.Add(val3);
				LyricMaterial.Apply(val3, settings, palette);
			}
			((Graphic)text).color = Color.white;
			((TMP_Text)translation).fontSize = ((TMP_Text)text).fontSize * 0.7f;
			((TMP_Text)translation).rectTransform.sizeDelta = new Vector2((float)width * 0.32f, (float)height * 0.12f);
			((Transform)((TMP_Text)translation).rectTransform).position = (Vector2)(val - new Vector2(0f, ((TMP_Text)text).preferredHeight + 8f));
			((TMP_Text)translation).text = (settings.Translation.Value ? translated : "");
			((Graphic)translation).color = Color.white;
			TMP_TextInfo textInfo = ((TMP_Text)text).textInfo;
			for (int num4 = 0; num4 < textInfo.meshInfo.Length && num4 < original.Length; num4++)
			{
				Array.Copy(original[num4].vertices, textInfo.meshInfo[num4].vertices, Math.Min(original[num4].vertices.Length, textInfo.meshInfo[num4].vertices.Length));
			}
			Color white = Color.white;
			white.a = 1f;
			for (int num5 = 0; num5 < textInfo.characterCount && num5 < mapping.Length; num5++)
			{
				TMP_CharacterInfo val4 = textInfo.characterInfo[num5];
				if (val4.isVisible)
				{
					int materialReferenceIndex = val4.materialReferenceIndex;
					int vertexIndex = val4.vertexIndex;
					LyricGlyphFrame lyricGlyphFrame = animation.Sample(mapping[num5], position, Time.realtimeSinceStartup, settings.Pop.Value || settings.Mode.Value == 2, settings.Wiggle.Value, settings.Frequency.Value, settings.Amount.Value);
					Vector3 val5 = (original[materialReferenceIndex].vertices[vertexIndex] + original[materialReferenceIndex].vertices[vertexIndex + 2]) * 0.5f;
					Quaternion val6 = Quaternion.Euler(0f, 0f, (float)lyricGlyphFrame.Rotation * 57.29578f);
					Color val7 = white;
					val7.a = (float)lyricGlyphFrame.Alpha;
					for (int num6 = 0; num6 < 4; num6++)
					{
						textInfo.meshInfo[materialReferenceIndex].vertices[vertexIndex + num6] = val5 + val6 * ((original[materialReferenceIndex].vertices[vertexIndex + num6] - val5) * (float)lyricGlyphFrame.Scale) + new Vector3((float)lyricGlyphFrame.XOffset, 0f - (float)lyricGlyphFrame.YOffset, 0f);
						textInfo.meshInfo[materialReferenceIndex].colors32[vertexIndex + num6] = (Color32)(val7);
					}
				}
			}
			((TMP_Text)text).UpdateVertexData((TMP_VertexDataUpdateFlags)17);
			mainHalo.Update((TMP_Text)(object)text, settings, palette);
			translationHalo.Update((TMP_Text)(object)translation, settings, palette);
			return true;
		}
		catch (Exception ex)
		{
			failed = true;
			Hide();
			if ((Object)(object)Plugin.Instance != (Object)null)
			{
				Plugin.Instance.Notice = "所选屏幕字体不可用，使用系统字体：" + ex.Message;
			}
			return false;
		}
	}

	private TextMeshProUGUI Create(string name)
	{
		GameObject val = new GameObject(name, new Type[1] { typeof(RectTransform) });
		val.transform.SetParent(root.transform, false);
		TextMeshProUGUI val2 = val.AddComponent<TextMeshProUGUI>();
		((TMP_Text)val2).extraPadding = true;
		((TMP_Text)val2).font = asset.Main;
		((TMP_Text)val2).richText = false;
		((Graphic)val2).raycastTarget = false;
		((TMP_Text)val2).alignment = (TextAlignmentOptions)258;
		((TMP_Text)val2).rectTransform.pivot = new Vector2(0.5f, 1f);
		((TMP_Text)val2).enableWordWrapping = true;
		((TMP_Text)val2).maxVisibleLines = 3;
		((TMP_Text)val2).overflowMode = (TextOverflowModes)1;
		((TMP_Text)val2).fontSharedMaterial = new Material(((TMP_Text)val2).fontSharedMaterial);
		return val2;
	}

	public void Dispose()
	{
		mainHalo.Dispose();
		translationHalo.Dispose();
		foreach (Material ownedMaterial in ownedMaterials)
		{
			if ((Object)(object)ownedMaterial != (Object)null)
			{
				Object.Destroy((Object)(object)ownedMaterial);
			}
		}
		ownedMaterials.Clear();
		if ((Object)(object)root != (Object)null)
		{
			Object.Destroy((Object)(object)root);
		}
		asset?.Dispose();
		root = null;
		asset = null;
	}
}
