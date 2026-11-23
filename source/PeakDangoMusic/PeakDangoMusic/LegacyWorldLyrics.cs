using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using DangoMusic.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakDangoMusic;

internal sealed class LegacyWorldLyrics : IDisposable
{
	private readonly AdaptiveLyrics.Palette palette = new AdaptiveLyrics.Palette();

	private readonly MusicSettings settings;

	private readonly ManualLogSource log;

	private Font? font;

	private GameObject? root;

	private TextMesh? original;

	private LyricWeatherGraphic? weatherGraphic;

	private TextMesh? translation;

	private Vector3 viewport;

	private Quaternion orientation;

	private readonly LyricLineView lineView = new LyricLineView();

	private readonly List<Material> materials = new List<Material>();

	private string key = "";

	private string cachedSong = "";

	private string lastPaint = "";

	private int cachedIndex = -1;

	private int generation = -1;

	private int mode = -1;

	private int serial;

	private float paintAt;

	private float repositionAt;
	private bool wasCinematic;

	private LyricsLine? line;

	private LyricTokens? tokens;

	private LyricAnimation? animation;

	private long end;

	internal string Status
	{
		get
		{
			if (!((Object)(object)font != (Object)null))
			{
				return "文字渲染：准备兼容模式";
			}
			return "文字渲染：兼容模式（原生动态字体）";
		}
	}

	internal LegacyWorldLyrics(MusicSettings settings, ManualLogSource log)
	{
		this.settings = settings;
		this.log = log;
	}

	private bool Ensure()
	{
		if ((Object)(object)font != (Object)null && (Object)(object)root != (Object)null)
		{
			return true;
		}
		try
		{
			font = Font.CreateDynamicFontFromOSFont(new string[3] { "Microsoft YaHei", "Malgun Gothic", "Arial" }, 64);
			if ((Object)(object)font == (Object)null)
			{
				return false;
			}
			root = new GameObject("DangoMusic compatible world lyrics");
			Object.DontDestroyOnLoad((Object)(object)root);
			original = Text("Original");
			translation = Text("Translation");
			((Component)translation).transform.localPosition = new Vector3(0f, -0.55f, 0f);
			log.LogInfo((object)"漂浮歌词已启用原生动态字体兼容渲染。");
			return true;
		}
		catch (Exception ex)
		{
			log.LogWarning((object)("兼容文字字体加载失败: " + ex.Message));
			return false;
		}
	}

	private TextMesh Text(string label)
	{
		GameObject val = new GameObject(label);
		val.transform.SetParent(root.transform, false);
		TextMesh val2 = val.AddComponent<TextMesh>();
		val2.font = font;
		val2.fontSize = 64;
		val2.characterSize = 0.04f;
		val2.anchor = (TextAnchor)4;
		val2.alignment = (TextAlignment)1;
		val2.richText = true;
		val2.color = LyricPlacement.TextColor;
		Material val3 = new Material(font.material);
		materials.Add(val3);
		LyricMaterial.Apply(val3, settings);
		MeshRenderer component = val.GetComponent<MeshRenderer>();
		((Renderer)component).sharedMaterial = val3;
		((Renderer)component).shadowCastingMode = (ShadowCastingMode)0;
		((Renderer)component).receiveShadows = false;
		return val2;
	}

	internal void Tick(MusicState state, Camera camera, long now, CinematicLyrics.WorldPlacement? shot)
	{
		bool leavingCinematic = wasCinematic && !shot.HasValue;
		wasCinematic = shot.HasValue;
		long? num = state.PositionAt(now);
		if (!num.HasValue || !Ensure())
		{
			Hide();
			return;
		}
		LyricsTimeline lyricsTimeline = lineView.Get(state.Timeline, settings.SplitLines.Value && settings.Mode.Value != 2);
		long num2 = Math.Clamp(num.Value + settings.Offset.Value, 0L, state.DurationMs);
		int num3 = lyricsTimeline.FindIndex(num2);
		if (num3 < 0)
		{
			Hide();
			return;
		}
		if (cachedSong != state.Key || generation != state.LyricGeneration || cachedIndex != num3 || mode != settings.Mode.Value)
		{
			cachedSong = state.Key;
			generation = state.LyricGeneration;
			cachedIndex = num3;
			mode = settings.Mode.Value;
			line = LyricUnicode.Normalize(lyricsTimeline.Lines[num3]);
			end = ((num3 + 1 < lyricsTimeline.Lines.Count) ? lyricsTimeline.Lines[num3 + 1].StartMs : Math.Min(state.DurationMs, line.StartMs + Math.Max(5000L, line.DurationMs)));
			tokens = ((mode == 1) ? new LyricTokens(line, end) : null);
			animation = new LyricAnimation(line, end, wholeToken: false, mode != 1);
			key = "";
		}
		if (num2 >= end)
		{
			Hide();
			return;
		}
		LyricsLine lyricsLine = line;
		int num4 = -1;
		if (tokens != null)
		{
			num4 = tokens.FindIndex(num2);
			if (num4 < 0)
			{
				Hide();
				return;
			}
			LyricToken lyricToken = tokens.Tokens[num4];
			lyricsLine = new LyricsLine(lyricToken.StartMs, Math.Max(1L, lyricToken.EndMs - lyricToken.StartMs), lyricToken.Text, new LyricsWord[1]
			{
				new LyricsWord(lyricToken.StartMs, lyricToken.EndMs - lyricToken.StartMs, lyricToken.Text)
			}, line.Translation);
		}
		string text = state.Key + "/" + generation + "/" + num3 + "/" + mode + "/" + num4;
		if (key != text)
		{
			key = text;
			animation = new LyricAnimation(lyricsLine, (tokens == null) ? end : (lyricsLine.StartMs + lyricsLine.DurationMs), tokens != null, mode != 1);
			lastPaint = "";
			paintAt = 0f;
			if (!shot.HasValue) Place(camera);
		}
		else if (leavingCinematic)
		{
			Place(camera);
		}
		else if (!shot.HasValue && mode != 2 && !settings.Follow.Value && settings.Smart.Value && Time.unscaledTime >= repositionAt)
		{
			Vector3 val = camera.WorldToViewportPoint(root.transform.position);
			if (val.z < 1f || val.x < 0.28f || val.x > 0.72f || val.y < 0.32f || val.y > 0.82f)
			{
				Place(camera);
			}
		}
		if ((Object)(object)root == (Object)null || (Object)(object)original == (Object)null || (Object)(object)translation == (Object)null)
		{
			return;
		}
		if (shot is { } placement)
		{
			root.transform.position = placement.Position;
			root.SetActive(placement.IsVisible(camera, settings));
		}
		else if (settings.Follow.Value)
		{
			root.transform.position = camera.ViewportToWorldPoint(viewport);
			root.SetActive(settings.AllowWalls.Value || !Physics.Linecast(((Component)camera).transform.position, root.transform.position, LayerMask.GetMask(new string[2] { "Map", "Terrain" }), (QueryTriggerInteraction)1));
		}
		palette.Update((Vector2)(camera.WorldToViewportPoint(root.transform.position)));
		foreach (Material material in materials)
		{
			LyricMaterial.Apply(material, settings, palette);
		}
		if (!shot.HasValue) root.transform.position = LyricPlacement.KeepDistance(root.transform.position, camera, settings);
		root.transform.rotation = settings.FacePlayer.Value ? LyricPlacement.Face(root.transform.position, camera) :
			(shot?.Orientation ?? orientation);
		float num5 = settings.FontSize.Value * (settings.Distance.Value / 10f);
		original.characterSize = 0.04f * num5;
		translation.characterSize = 0.029f * num5;
		Vector3 drift = root.transform.InverseTransformVector(LyricWeather.Drift(0.22f * settings.FontSize.Value));
		original.transform.localPosition = drift;
		translation.transform.localPosition = drift + new Vector3(0f, -0.55f * num5, 0f);
		var weather = LyricWeatherGraphic.Prepare(weatherGraphic, original.transform, false, 1f);
		if (weather != null)
		{
			weatherGraphic = weather;
			weather.DrawBounds(original.GetComponent<MeshRenderer>().localBounds);
		}
		if (!(Time.unscaledTime >= paintAt))
		{
			return;
		}
		paintAt = Time.unscaledTime + 0.05f;
		StringBuilder stringBuilder = new StringBuilder();
		int num6 = 0;
		for (int i = 0; i < animation.Graphemes.Count && i < 160; i++)
		{
			LyricGrapheme lyricGrapheme = animation.Graphemes[i];
			LyricGlyphFrame lyricGlyphFrame = animation.Sample(i, num2, Time.unscaledTime, settings.Pop.Value || mode == 2, settings.Wiggle.Value, settings.Frequency.Value, settings.Amount.Value);
			float value = ((mode != 2 && !settings.Pop.Value && !settings.Wiggle.Value) ? 1f : ((float)lyricGlyphFrame.Alpha));
			int value2 = Math.Clamp((int)Math.Round(64.0 * lyricGlyphFrame.Scale), 48, 110);
			if (num6 >= 22 && !string.IsNullOrWhiteSpace(lyricGrapheme.Text))
			{
				stringBuilder.Append('\n');
				num6 = 0;
			}
			stringBuilder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(palette.Face)).Append(((int)(Math.Clamp(value, 0f, 1f) * 255f)).ToString("X2"))
				.Append("><size=")
				.Append(value2)
				.Append('>')
				.Append(Escape(lyricGrapheme.Text))
				.Append("</size></color>");
			num6 += ((!lyricGrapheme.Text.Any((char c) => c > 'ÿ')) ? 1 : 2);
		}
		string text2 = stringBuilder.ToString();
		if (text2 != lastPaint)
		{
			original.text = text2;
			lastPaint = text2;
		}
		translation.text = (settings.Translation.Value ? Wrap(lyricsLine.Translation, 32) : "");
		translation.color = palette.Face;
	}

	private void Place(Camera camera)
	{
		if ((Object)(object)root == (Object)null)
		{
			return;
		}
		int mask = LayerMask.GetMask(new string[2] { "Map", "Terrain" });
		foreach (Vector3 item in LyricPlacement.Candidates(camera, settings, serial++))
		{
			Vector3 val = camera.ViewportToWorldPoint(item);
			if (settings.AllowWalls.Value || !Physics.Linecast(((Component)camera).transform.position, val, mask, (QueryTriggerInteraction)1))
			{
				root.transform.position = val;
				orientation = LyricPlacement.Face(val, camera);
				root.transform.rotation = orientation;
				root.SetActive(true);
				viewport = item;
				repositionAt = Time.unscaledTime + 0.08f;
				return;
			}
		}
		root.SetActive(false);
		repositionAt = Time.unscaledTime + 0.8f;
	}

	private static string Escape(string text)
	{
		return text.Replace('<', '‹').Replace('>', '›');
	}

	private static string Wrap(string text, int width)
	{
		StringBuilder stringBuilder = new StringBuilder();
		TextElementEnumerator textElementEnumerator = StringInfo.GetTextElementEnumerator(text);
		int num = 0;
		int num2 = 0;
		while (textElementEnumerator.MoveNext() && num2++ < 160)
		{
			string text2 = (string)textElementEnumerator.Current;
			if (num >= width)
			{
				stringBuilder.Append('\n');
				num = 0;
			}
			stringBuilder.Append(Escape(text2));
			num += ((!text2.Any((char c) => c > 'ÿ')) ? 1 : 2);
		}
		return stringBuilder.ToString();
	}

	internal void Hide()
	{
		if ((Object)(object)root != (Object)null)
		{
			root.SetActive(false);
		}
		key = "";
	}

	public void Dispose()
	{
		if ((Object)(object)root != (Object)null)
		{
			Object.Destroy((Object)(object)root);
		}
		foreach (Material material in materials)
		{
			if ((Object)(object)material != (Object)null)
			{
				Object.Destroy((Object)(object)material);
			}
		}
		materials.Clear();
		if ((Object)(object)font != (Object)null)
		{
			Object.Destroy((Object)(object)font);
		}
		root = null;
		font = null;
		key = "";
		cachedIndex = -1;
	}
}
