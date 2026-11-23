using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using DangoMusic.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace PeakDangoMusic;

internal sealed class WorldLyrics : IDisposable
{
	private sealed class Entry
	{
		internal GameObject Root;

		internal TextMeshPro Main;

		internal TextMeshPro Translation;

		internal LyricsLine Line;

		internal LyricAnimation Animation;

		internal TMP_MeshInfo[] Original;

		internal Material[] MainMaterials = Array.Empty<Material>();

		internal Material[] TranslationMaterials = Array.Empty<Material>();

		internal readonly TextHalo MainHalo = new TextHalo();

		internal readonly TextHalo TranslationHalo = new TextHalo();

		internal LyricWeatherGraphic? Weather;

		internal long LastPosition;

		internal readonly AdaptiveLyrics.Palette Palette = new AdaptiveLyrics.Palette();

		internal Vector3 Anchor;

		internal Vector3 Viewport;

		internal Quaternion Orientation;

		internal float Born;

		internal float Retired = -1f;

		internal int[] CharacterToGrapheme = Array.Empty<int>();
	}

	private readonly MusicSettings settings;

	private readonly ManualLogSource log;

	private readonly List<Entry> entries = new List<Entry>();

	private readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();

	private TMP_FontAsset? font;

	private GameObject? root;

	private Entry? current;

	private string renderKey = "";

	private string songKey = "";

	private long previousPosition = -1L;

	private float retryAt;

	private float retargetAt;
	private bool wasCinematic;

	private int serial;

	private bool fontFailed;

	private FontLibrary.Asset? customFont;

	private readonly LegacyWorldLyrics compatible;

	private readonly LyricLineView lineView = new LyricLineView();

	private string cachedKey = "";

	private int cachedGeneration = -1;

	private int cachedIndex = -1;

	private int cachedMode = -1;

	private LyricsLine? cachedLine;

	private LyricTokens? cachedTokens;

	private long cachedEnd;

	internal string RenderStatus
	{
		get
		{
			if (!fontFailed)
			{
				if (!((Object)(object)font != (Object)null))
				{
					return "文字渲染：等待进入地图及歌曲／预览";
				}
				return "文字渲染：系统字体 SDF";
			}
			return compatible.Status;
		}
	}

	internal WorldLyrics(MusicSettings settings, ManualLogSource log)
	{
		this.settings = settings;
		this.log = log;
		compatible = new LegacyWorldLyrics(settings, log);
	}

	private bool EnsureFont()
	{
		if ((Object)(object)font != (Object)null)
		{
			return true;
		}
		if (fontFailed)
		{
			return false;
		}
		try
		{
			root = new GameObject("DangoMusic local world lyrics");
			Object.DontDestroyOnLoad((Object)(object)root);
			FontEngine.InitializeFontEngine();
			if (settings.FontPath.Value.Length > 0)
			{
				try
				{
					customFont = new FontLibrary.Asset(settings.FontPath.Value);
					font = customFont.Main;
					log.LogInfo((object)("歌词字体：" + Path.GetFileNameWithoutExtension(settings.FontPath.Value)));
					return true;
				}
				catch (Exception ex)
				{
					log.LogWarning((object)("自定义字体不可用，使用系统默认：" + ex.Message));
				}
			}
			string[] array = new string[4] { "msyh.ttc", "msyh.ttf", "simhei.ttf", "simsun.ttc" };
			string text = default;
			for (int i = 0; i < array.Length; i++)
			{
				TMP_FontAsset val = FileFont(array[i]);
				if (!((Object)(object)val == (Object)null))
				{
					if (val.TryAddCharacters("歌词ABC", out text, false) && (Object)(object)((TMP_Asset)val).material != (Object)null && ((TMP_Asset)val).material.shader.isSupported)
					{
						font = val;
						break;
					}
					ReleaseAsset(val);
				}
			}
			if ((Object)(object)font == (Object)null)
			{
				throw new InvalidOperationException("系统字体文件无法生成可用图集");
			}
			resources.Add((Object)(object)font);
			resources.Add((Object)(object)((TMP_Asset)font).material);
			TMP_FontAsset val2 = FileFont("malgun.ttf");
			if ((Object)(object)val2 != (Object)null)
			{
				resources.Add((Object)(object)val2);
				resources.Add((Object)(object)((TMP_Asset)val2).material);
				TMP_FontAsset val3 = font;
				if (val3.fallbackFontAssetTable == null)
				{
					List<TMP_FontAsset> list = (val3.fallbackFontAssetTable = new List<TMP_FontAsset>());
				}
				font.fallbackFontAssetTable.Add(val2);
			}
			log.LogInfo((object)"漂浮歌词字体：从系统字体文件创建SDF，已加载中文字形。");
			return true;
		}
		catch (Exception ex2)
		{
			fontFailed = true;
			font = null;
			log.LogWarning((object)("SDF字体不可用，改用兼容文字渲染: " + ex2.Message));
			return false;
		}
	}

	private static TMP_FontAsset? FileFont(string name)
	{
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), name);
		if (!File.Exists(text))
		{
			return null;
		}
		return TMP_FontAsset.CreateFontAsset(text, 0, 64, 8, (GlyphRenderMode)4165, 1024, 1024);
	}

	private static void ReleaseAsset(TMP_FontAsset asset)
	{
		Texture2D[] atlasTextures = asset.atlasTextures;
		foreach (Texture2D val in atlasTextures)
		{
			if ((Object)(object)val != (Object)null)
			{
				Object.Destroy((Object)(object)val);
			}
		}
		if ((Object)(object)((TMP_Asset)asset).material != (Object)null)
		{
			Object.Destroy((Object)(object)((TMP_Asset)asset).material);
		}
		Object.Destroy((Object)(object)asset);
	}

	internal void RetryFonts()
	{
		Dispose();
		fontFailed = false;
		renderKey = "";
		cachedIndex = -1;
	}

	internal void Invalidate()
	{
		renderKey = "";
		cachedIndex = -1;
		ClearEntries();
		compatible.Hide();
	}

	internal void Hide()
	{
		ClearEntries();
		compatible.Hide();
		renderKey = "";
		previousPosition = -1L;
	}

	internal void Tick(MusicState state, Camera camera, long now, CinematicLyrics.WorldPlacement? shot)
	{
		bool leavingCinematic = wasCinematic && !shot.HasValue;
		wasCinematic = shot.HasValue;
		long? num = state.PositionAt(now);
		if (!num.HasValue)
		{
			Hide();
			return;
		}
		if (!EnsureFont())
		{
			ClearEntries();
			compatible.Tick(state, camera, now, shot);
			return;
		}
		compatible.Hide();
		long num2 = Math.Clamp(num.Value + settings.Offset.Value, 0L, state.DurationMs);
		if (state.Key != songKey || (previousPosition >= 0 && Math.Abs(num2 - previousPosition) > 1800))
		{
			ClearEntries();
			renderKey = "";
			songKey = state.Key;
		}
		previousPosition = num2;
		LyricsTimeline lyricsTimeline = lineView.Get(state.Timeline, settings.SplitLines.Value && settings.Mode.Value != 2);
		int num3 = lyricsTimeline.FindIndex(num2);
		if (num3 < 0)
		{
			Hide();
			return;
		}
		int value = settings.Mode.Value;
		if (cachedKey != state.Key || cachedGeneration != state.LyricGeneration || cachedIndex != num3 || cachedMode != value)
		{
			cachedKey = state.Key;
			cachedGeneration = state.LyricGeneration;
			cachedIndex = num3;
			cachedMode = value;
			cachedLine = LyricUnicode.Normalize(lyricsTimeline.Lines[num3]);
			cachedEnd = ((num3 + 1 < lyricsTimeline.Lines.Count) ? lyricsTimeline.Lines[num3 + 1].StartMs : Math.Min(state.DurationMs, cachedLine.StartMs + Math.Max(5000L, cachedLine.DurationMs)));
			cachedTokens = ((value == 1) ? new LyricTokens(cachedLine, cachedEnd) : null);
		}
		LyricsLine lyricsLine = cachedLine;
		long endMs = cachedEnd;
		if (num2 >= endMs || string.IsNullOrWhiteSpace(lyricsLine.Text))
		{
			Retire();
			Animate(camera, num2, shot);
			return;
		}
		int num4 = -1;
		if (value == 1)
		{
			LyricTokens lyricTokens = cachedTokens;
			num4 = lyricTokens.FindIndex(num2);
			if (num4 < 0)
			{
				Retire();
				Animate(camera, num2, shot);
				return;
			}
			LyricToken lyricToken = lyricTokens.Tokens[num4];
			endMs = lyricToken.EndMs;
			lyricsLine = ((current == null || current.Line.StartMs != lyricToken.StartMs || !(current.Line.Text == lyricToken.Text)) ? new LyricsLine(lyricToken.StartMs, Math.Max(1L, lyricToken.EndMs - lyricToken.StartMs), lyricToken.Text, new LyricsWord[1]
			{
				new LyricsWord(lyricToken.StartMs, Math.Max(1L, lyricToken.EndMs - lyricToken.StartMs), lyricToken.Text)
			}, lyricsLine.Translation) : current.Line);
		}
		string text = state.Key + "/" + state.LyricGeneration + "/" + num3 + "/" + value + "/" + num4;
		if (text != renderKey || (current == null && Time.unscaledTime >= retryAt))
		{
			Retire();
			renderKey = text;
			retryAt = Time.unscaledTime + 0.8f;
			current = Spawn(lyricsLine, endMs, camera, shot);
		}
		else if (!shot.HasValue && current != null && (leavingCinematic ||
			(value != 2 && !settings.Follow.Value && settings.Smart.Value && Time.unscaledTime >= retargetAt && NearEdge(current, camera))))
		{
			Place(current, camera);
			retargetAt = Time.unscaledTime + 0.08f;
		}
		Animate(camera, num2, shot);
	}

	private Entry? Spawn(LyricsLine raw, long next, Camera camera, CinematicLyrics.WorldPlacement? shot)
	{
		if ((Object)(object)root == (Object)null || (Object)(object)font == (Object)null)
		{
			return null;
		}
		string text = Limit(raw.Text, 160);
		string text2 = (settings.Translation.Value ? Limit(raw.Translation, 160) : "");
		LyricsLine line = ((text == raw.Text) ? raw : new LyricsLine(raw.StartMs, raw.DurationMs, text, null, text2));
		Entry entry = new Entry
		{
			Root = new GameObject("DangoMusic lyric"),
			Line = line,
			Animation = new LyricAnimation(line, next, settings.Mode.Value == 1, settings.Mode.Value != 1),
			Born = Time.unscaledTime
		};
		entry.Root.transform.SetParent(root.transform, false);
		entry.Main = Text(entry.Root, "Original", text, settings.WorldFontSize);
		entry.Translation = Text(entry.Root, "Translation", text2, settings.WorldFontSize * 0.72f);
		float num = Mathf.Min(settings.Distance.Value * 0.6f, 6.5f);
		((TMP_Text)entry.Main).rectTransform.sizeDelta = new Vector2(num, 20f);
		((TMP_Text)entry.Translation).rectTransform.sizeDelta = new Vector2(num, 20f);
		((TMP_Text)entry.Main).ForceMeshUpdate(true, true);
		((TMP_Text)entry.Translation).ForceMeshUpdate(true, true);
		entry.MainMaterials = ((TMP_Text)entry.Main).fontMaterials;
		entry.TranslationMaterials = ((TMP_Text)entry.Translation).fontMaterials;
		Bounds textBounds = ((TMP_Text)entry.Main).textBounds;
		float num2 = Mathf.Max(0.3f, textBounds.size.y);
		entry.Translation.transform.localPosition = new Vector3(0f, (0f - num2) * 0.6f - 0.2f, 0f);
		if (shot is { } placement)
		{
			entry.Anchor = placement.Position;
			entry.Orientation = placement.Orientation;
			entry.Viewport = camera.WorldToViewportPoint(placement.Position);
		}
		else if (!Place(entry, camera))
		{
			DestroyEntry(entry);
			return null;
		}
		entry.Original = ((TMP_Text)entry.Main).textInfo.CopyMeshInfoVertexData();
		entry.CharacterToGrapheme = new int[((TMP_Text)entry.Main).textInfo.characterCount];
		for (int i = 0; i < entry.CharacterToGrapheme.Length; i++)
		{
			int index = ((TMP_Text)entry.Main).textInfo.characterInfo[i].index;
			int j;
			for (j = 0; j + 1 < entry.Animation.Graphemes.Count && entry.Animation.Graphemes[j + 1].SourceStart <= index; j++)
			{
			}
			entry.CharacterToGrapheme[i] = j;
		}
		entries.Add(entry);
		while (entries.Count > 4)
		{
			Entry e = entries[0];
			entries.RemoveAt(0);
			DestroyEntry(e);
		}
		return entry;
	}

	private TextMeshPro Text(GameObject parent, string label, string value, float size)
	{
		GameObject val = new GameObject(label);
		val.transform.SetParent(parent.transform, false);
		TextMeshPro val2 = val.AddComponent<TextMeshPro>();
		((TMP_Text)val2).font = font;
		((TMP_Text)val2).fontSize = size;
		((TMP_Text)val2).extraPadding = true;
		((TMP_Text)val2).text = value;
		((TMP_Text)val2).richText = false;
		((TMP_Text)val2).enableWordWrapping = true;
		((TMP_Text)val2).maxVisibleLines = 3;
		((TMP_Text)val2).overflowMode = (TextOverflowModes)1;
		((TMP_Text)val2).alignment = (TextAlignmentOptions)514;
		((Graphic)val2).color = Color.white;
		((Graphic)val2).raycastTarget = false;
		Material val3 = (((TMP_Text)val2).fontSharedMaterial = new Material(((TMP_Text)val2).fontSharedMaterial));
		LyricMaterial.Apply(val3, settings);
		if (val3.HasProperty("_ZTestMode"))
		{
			val3.SetFloat("_ZTestMode", (float)((settings.AllowWalls.Value && !settings.BehindPlayer.Value) ? 8 : 4));
		}
		((TMP_Text)val2).UpdateMeshPadding();
		val2.renderer.shadowCastingMode = (ShadowCastingMode)0;
		val2.renderer.receiveShadows = false;
		return val2;
	}

	private bool Place(Entry e, Camera camera)
	{
		IEnumerable<Vector3> enumerable = LyricPlacement.Candidates(camera, settings, serial++);
		Bounds textBounds = ((TMP_Text)e.Main).textBounds;
		float num = Mathf.Clamp(textBounds.size.x, 0.4f, 6.5f);
		textBounds = ((TMP_Text)e.Main).textBounds;
		float y = textBounds.size.y;
		textBounds = ((TMP_Text)e.Translation).textBounds;
		float num2 = Mathf.Clamp(y + textBounds.size.y + 0.4f, 0.4f, 3.5f);
		int mask = LayerMask.GetMask(new string[2] { "Map", "Terrain" });
		foreach (Vector3 item in enumerable)
		{
			Vector3 val2 = camera.ViewportToWorldPoint(item);
			if (settings.AllowWalls.Value || (!Physics.Linecast(((Component)camera).transform.position, val2, mask, (QueryTriggerInteraction)1) && !Physics.CheckBox(val2, new Vector3(num * 0.5f, num2 * 0.5f, 0.12f), ((Component)camera).transform.rotation, mask, (QueryTriggerInteraction)1)))
			{
				e.Root.transform.position = val2;
				e.Orientation = LyricPlacement.Face(val2, camera);
				e.Root.transform.rotation = e.Orientation;
				e.Anchor = val2;
				e.Viewport = item;
				e.Root.SetActive(true);
				return true;
			}
		}
		return false;
	}

	private static bool NearEdge(Entry e, Camera camera)
	{
		Vector3 val = camera.WorldToViewportPoint(e.Root.transform.position);
		if (val.z < 1f || val.x < 0.28f || val.x > 0.72f || val.y < 0.32f || val.y > 0.82f)
		{
			return true;
		}
		Bounds bounds = e.Main.renderer.bounds;
		Vector3[] array = new Vector3[2] { bounds.min, bounds.max };
		foreach (Vector3 val2 in array)
		{
			Vector3 val3 = camera.WorldToViewportPoint(val2);
			if (val3.x < 0.025f || val3.x > 0.975f || val3.y < 0.1f || val3.y > 0.97f)
			{
				return true;
			}
		}
		return false;
	}

	private void Retire()
	{
		if (current != null)
		{
			current.Retired = Time.unscaledTime;
			((TMP_Text)current.Translation).text = "";
			current = null;
		}
	}

	private void Animate(Camera camera, long position, CinematicLyrics.WorldPlacement? shot)
	{
		bool shotVisible = shot?.IsVisible(camera, settings) ?? true;
		for (int num = entries.Count - 1; num >= 0; num--)
		{
			Entry entry = entries[num];
			float unscaledTime = Time.unscaledTime;
			float num2 = unscaledTime - entry.Born;
			float num3 = ((entry.Retired < 0f) ? 0f : ((unscaledTime - entry.Retired) / 0.4f));
			if (entry.Retired >= 0f && (shot.HasValue || settings.Exit.Value == 3 || num3 >= 1f))
			{
				entries.RemoveAt(num);
				DestroyEntry(entry);
			}
			else
			{
				float num4 = ((settings.Entrance.Value == 3) ? 1f : Mathf.Clamp01(num2 / 0.25f));
				float num5 = 1f;
				float num6 = 0f;
				if (settings.Entrance.Value == 1)
				{
					num6 = (1f - num4) * -0.18f;
				}
				if (settings.Entrance.Value == 2)
				{
					num5 = 1f + 0.18f * (1f - num4) * (1f - num4);
				}
				if (entry.Retired >= 0f)
				{
					num4 *= 1f - num3;
					if (settings.Exit.Value == 1)
					{
						num6 += num3 * 0.16f;
					}
					if (settings.Exit.Value == 2)
					{
						num5 *= 1f - num3 * 0.5f;
					}
				}
				if (shot.HasValue)
				{
					entry.Root.SetActive(shotVisible);
				}
				else if (settings.Follow.Value)
				{
					entry.Anchor = camera.ViewportToWorldPoint(entry.Viewport);
					bool active = settings.AllowWalls.Value || !Physics.Linecast(((Component)camera).transform.position, entry.Anchor, LayerMask.GetMask(new string[2] { "Map", "Terrain" }), (QueryTriggerInteraction)1);
					entry.Root.SetActive(active);
				}
				if (!shot.HasValue) entry.Anchor = LyricPlacement.KeepDistance(entry.Anchor, camera, settings);
				Vector3 positionAnchor = shot?.Position ?? entry.Anchor;
				entry.Root.transform.rotation = settings.FacePlayer.Value ? LyricPlacement.Face(positionAnchor, camera) :
					(shot?.Orientation ?? entry.Orientation);
				entry.Root.transform.position = positionAnchor + ((Component)camera).transform.up * num6 +
					LyricWeather.Drift(0.22f * settings.FontSize.Value, entry.Born);
				entry.Root.transform.localScale = Vector3.one * num5;
				AdaptiveLyrics.Palette palette = entry.Palette;
				Bounds bounds = entry.Main.renderer.bounds;
				palette.Update((Vector2)(camera.WorldToViewportPoint(bounds.center)));
				Material[] mainMaterials = entry.MainMaterials;
				for (int i = 0; i < mainMaterials.Length; i++)
				{
					LyricMaterial.Apply(mainMaterials[i], settings, entry.Palette);
				}
				mainMaterials = entry.TranslationMaterials;
				for (int i = 0; i < mainMaterials.Length; i++)
				{
					LyricMaterial.Apply(mainMaterials[i], settings, entry.Palette);
				}
				((Graphic)entry.Main).color = Color.white;
				((Graphic)entry.Translation).color = Color.white;
				((TMP_Text)entry.Translation).alpha = num4;
				if (entry.Retired < 0f)
				{
					entry.LastPosition = position;
				}
				TMP_TextInfo textInfo = ((TMP_Text)entry.Main).textInfo;
				for (int j = 0; j < textInfo.meshInfo.Length && j < entry.Original.Length; j++)
				{
					Array.Copy(entry.Original[j].vertices, textInfo.meshInfo[j].vertices, Math.Min(entry.Original[j].vertices.Length, textInfo.meshInfo[j].vertices.Length));
				}
				for (int k = 0; k < textInfo.characterCount; k++)
				{
					TMP_CharacterInfo val = textInfo.characterInfo[k];
					if (!val.isVisible || k >= entry.CharacterToGrapheme.Length)
					{
						continue;
					}
					int materialReferenceIndex = val.materialReferenceIndex;
					int vertexIndex = val.vertexIndex;
					if (materialReferenceIndex < entry.Original.Length)
					{
						LyricGlyphFrame lyricGlyphFrame = entry.Animation.Sample(entry.CharacterToGrapheme[k], (entry.Retired >= 0f) ? entry.LastPosition : position, unscaledTime, settings.Pop.Value || settings.Mode.Value == 2, settings.Wiggle.Value, settings.Frequency.Value, settings.Amount.Value);
						if (settings.Mode.Value != 2 && !settings.Pop.Value && !settings.Wiggle.Value)
						{
							lyricGlyphFrame = new LyricGlyphFrame(1.0, 1.0, 0.0, 0.0, 0.0);
						}
						float num7 = num4 * (float)lyricGlyphFrame.Alpha;
						Vector3[] vertices = entry.Original[materialReferenceIndex].vertices;
						Vector3[] vertices2 = textInfo.meshInfo[materialReferenceIndex].vertices;
						Color32[] colors = textInfo.meshInfo[materialReferenceIndex].colors32;
						Vector3 val2 = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
						Quaternion val3 = Quaternion.Euler(0f, 0f, (float)(lyricGlyphFrame.Rotation * 180.0 / Math.PI));
						float num8 = 0.03f * settings.FontSize.Value * (settings.Distance.Value / 6.5f);
						for (int l = 0; l < 4; l++)
						{
							vertices2[vertexIndex + l] = val2 + val3 * ((vertices[vertexIndex + l] - val2) * (float)lyricGlyphFrame.Scale) + new Vector3((float)lyricGlyphFrame.XOffset * num8, (float)lyricGlyphFrame.YOffset * num8, 0f);
							colors[vertexIndex + l] = (Color32)(new Color(1f, 1f, 1f, Mathf.Clamp01(num7)));
						}
					}
				}
				((TMP_Text)entry.Main).UpdateVertexData((TMP_VertexDataUpdateFlags)17);
				var weather = LyricWeatherGraphic.Prepare(entry.Weather, entry.Main.transform, false, num4);
				if (weather != null)
				{
					entry.Weather = weather;
					weather.DrawGlyphs(textInfo);
				}
				entry.MainHalo.Update((TMP_Text)(object)entry.Main, settings, entry.Palette);
				entry.TranslationHalo.Update((TMP_Text)(object)entry.Translation, settings, entry.Palette);
			}
		}
	}

	private static string Limit(string text, int elements)
	{
		int[] array = StringInfo.ParseCombiningCharacters(text);
		if (array.Length > elements)
		{
			return text.Substring(0, array[elements]) + "…";
		}
		return text;
	}

	private void ClearEntries()
	{
		foreach (Entry entry in entries)
		{
			DestroyEntry(entry);
		}
		entries.Clear();
		current = null;
	}

	private static void DestroyEntry(Entry e)
	{
		e.MainHalo.Dispose();
		e.TranslationHalo.Dispose();
		foreach (Material item in e.MainMaterials.Concat(e.TranslationMaterials).Distinct())
		{
			if ((Object)(object)item != (Object)null)
			{
				Object.Destroy((Object)(object)item);
			}
		}
		if (e.MainMaterials.Length == 0 && (Object)(object)e.Main != (Object)null)
		{
			Object.Destroy((Object)(object)((TMP_Text)e.Main).fontSharedMaterial);
		}
		if (e.TranslationMaterials.Length == 0 && (Object)(object)e.Translation != (Object)null)
		{
			Object.Destroy((Object)(object)((TMP_Text)e.Translation).fontSharedMaterial);
		}
		if ((Object)(object)e.Root != (Object)null)
		{
			Object.Destroy((Object)(object)e.Root);
		}
	}

	public void Dispose()
	{
		ClearEntries();
		customFont?.Dispose();
		customFont = null;
		compatible.Dispose();
		if ((Object)(object)root != (Object)null)
		{
			Object.Destroy((Object)(object)root);
		}
		root = null;
		foreach (Object resource in resources)
		{
			if (resource != (Object)null)
			{
				Object.Destroy(resource);
			}
		}
		resources.Clear();
		font = null;
	}
}
