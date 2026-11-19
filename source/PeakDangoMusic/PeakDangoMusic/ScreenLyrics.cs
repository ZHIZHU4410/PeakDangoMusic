using System;
using System.Collections.Generic;
using DangoMusic.Core;
using UnityEngine;

namespace PeakDangoMusic;

internal sealed class ScreenLyrics : IDisposable
{
	private readonly struct Glyph
	{
		internal readonly int Index;

		internal readonly int Row;

		internal readonly string Text;

		internal readonly float X;

		internal readonly float Width;

		internal Glyph(int index, int row, string text, float x, float width)
		{
			Index = index;
			Row = row;
			Text = text;
			X = x;
			Width = width;
		}
	}

	private readonly MusicSettings settings;

	private readonly AdaptiveLyrics.Palette palette = new AdaptiveLyrics.Palette();

	private readonly LyricLineView lineView = new LyricLineView();

	private readonly List<Glyph> glyphs = new List<Glyph>();

	private Font? font;

	private ScreenFontView? custom;

	private GUIStyle? style;

	private GUIStyle? translationStyle;

	private LyricsLine? line;

	private LyricAnimation? animation;

	private long position;

	private long end;

	private int index;

	private int mode = -1;

	private int generation = -1;

	private int tokenIndex = -1;

	private int serial = -1;

	private string song = "";

	private LyricsTimeline? source;

	private LyricTokens? tokens;

	private bool visible;

	private bool dirty;

	private int layoutWidth;

	private int layoutHeight;

	private float layoutScale;

	private readonly List<float> rowWidths = new List<float>();

	internal ScreenLyrics(MusicSettings settings)
	{
		this.settings = settings;
	}

	internal void Hide()
	{
		visible = false;
		custom?.Hide();
	}

	internal void RetryFonts()
	{
		custom?.Dispose();
		custom = null;
		dirty = true;
	}

	internal void Tick(MusicState state, long now)
	{
		long? num = state.PositionAt(now);
		if (!num.HasValue)
		{
			Hide();
			return;
		}
		LyricsTimeline lyricsTimeline = lineView.Get(state.Timeline, settings.SplitLines.Value && settings.Mode.Value != 2);
		position = Math.Clamp(num.Value + settings.Offset.Value, 0L, state.DurationMs);
		int num2 = lyricsTimeline.FindIndex(position);
		if (num2 < 0)
		{
			Hide();
			return;
		}
		bool flag = source != lyricsTimeline || song != state.Key || generation != state.LyricGeneration || index != num2 || mode != settings.Mode.Value;
		if (flag)
		{
			source = lyricsTimeline;
			song = state.Key;
			generation = state.LyricGeneration;
			index = num2;
			mode = settings.Mode.Value;
			line = LyricUnicode.Normalize(lyricsTimeline.Lines[index]);
			end = ((index + 1 < lyricsTimeline.Lines.Count) ? lyricsTimeline.Lines[index + 1].StartMs : Math.Min(state.DurationMs, line.StartMs + Math.Max(5000L, line.DurationMs)));
			tokens = ((mode == 1) ? new LyricTokens(line, end) : null);
			tokenIndex = -2;
			serial = index;
		}
		if (position >= end || string.IsNullOrWhiteSpace(line.Text))
		{
			Hide();
			return;
		}
		LyricsLine lyricsLine = line;
		int num3 = -1;
		if (tokens != null)
		{
			num3 = tokens.FindIndex(position);
			if (num3 < 0)
			{
				Hide();
				return;
			}
			LyricToken lyricToken = tokens.Tokens[num3];
			lyricsLine = new LyricsLine(lyricToken.StartMs, Math.Max(1L, lyricToken.EndMs - lyricToken.StartMs), lyricToken.Text, new LyricsWord[1]
			{
				new LyricsWord(lyricToken.StartMs, lyricToken.EndMs - lyricToken.StartMs, lyricToken.Text)
			}, line.Translation);
		}
		if (flag || num3 != tokenIndex || animation == null)
		{
			animation = new LyricAnimation(lyricsLine, (tokens == null) ? end : (lyricsLine.StartMs + lyricsLine.DurationMs), tokens != null, mode != 1);
			tokenIndex = num3;
			dirty = true;
		}
		visible = true;
	}

	private void Layout()
	{
		if (font == null)
		{
			font = Font.CreateDynamicFontFromOSFont(new string[3] { "Microsoft YaHei", "Malgun Gothic", "Arial" }, 32);
		}
		if (style == null)
		{
			style = new GUIStyle
			{
				richText = false,
				alignment = (TextAnchor)0,
				padding = new RectOffset(0, 0, 0, 0),
				margin = new RectOffset(0, 0, 0, 0),
				clipping = (TextClipping)0
			};
		}
		style.font = font;
		style.fontSize = Mathf.RoundToInt(Mathf.Clamp((float)Screen.height / 1080f * 28f * settings.FontSize.Value, 16f, 84f));
		style.normal.textColor = Color.white;
		translationStyle = new GUIStyle(style)
		{
			fontSize = Mathf.RoundToInt((float)style.fontSize * 0.7f),
			alignment = (TextAnchor)1,
			wordWrap = true
		};
		glyphs.Clear();
		rowWidths.Clear();
		float num = 0f;
		float num2 = (float)Screen.width * 0.32f;
		int num3 = 0;
		if (animation != null)
		{
			for (int i = 0; i < Math.Min(animation.Graphemes.Count, 180); i++)
			{
				string text = animation.Graphemes[i].Text;
				float num4 = Mathf.Max(style.CalcSize(new GUIContent(text)).x, (float)style.fontSize * 0.12f);
				if (text.Contains('\n') || (num + num4 > num2 && num > 0f))
				{
					rowWidths.Add(num);
					num = 0f;
					if (++num3 >= 3)
					{
						break;
					}
					if (text.Contains('\n'))
					{
						continue;
					}
				}
				glyphs.Add(new Glyph(i, num3, text, num, num4));
				num += num4;
			}
		}
		rowWidths.Add(num);
		layoutWidth = Screen.width;
		layoutHeight = Screen.height;
		layoutScale = settings.FontSize.Value;
		dirty = false;
	}

	internal void Draw()
	{
		if (!visible || animation == null || (int)Event.current.type != 7)
		{
			return;
		}
		if (settings.FontPath.Value.Length > 0)
		{
			if (custom == null)
			{
				custom = new ScreenFontView(settings);
			}
			if (custom.Draw(animation, position, serial, line?.Translation ?? ""))
			{
				return;
			}
		}
		else
		{
			custom?.Hide();
		}
		if (dirty || style == null || layoutWidth != Screen.width || layoutHeight != Screen.height || layoutScale != settings.FontSize.Value)
		{
			Layout();
		}
		if (style == null)
		{
			return;
		}
		Matrix4x4 matrix = GUI.matrix;
		Color color = GUI.color;
		int depth = GUI.depth;
		bool enabled = GUI.enabled;
		try
		{
			GUI.matrix = Matrix4x4.identity;
			GUI.depth = -2000;
			GUI.enabled = true;
			float num = (float)Screen.width * ((serial % 2 == 0) ? 0.22f : 0.78f);
			float num2 = (float)Screen.height * 0.61f;
			float num3 = (float)style.fontSize * 1.35f;
			palette.Update(new Vector2(num / (float)Screen.width, 1f - (num2 + (float)rowWidths.Count * num3 * 0.5f) / (float)Screen.height));
			Color val = palette.Face * (palette.IsDark ? 1f : Mathf.Min(settings.Brightness.Value, 2f));
			val.a = 1f;
			foreach (Glyph glyph in glyphs)
			{
				LyricGlyphFrame lyricGlyphFrame = animation.Sample(glyph.Index, position, Time.realtimeSinceStartup, settings.Pop.Value || mode == 2, settings.Wiggle.Value, settings.Frequency.Value, settings.Amount.Value);
				if (!(lyricGlyphFrame.Alpha <= 0.0))
				{
					float num4 = num - rowWidths[glyph.Row] * 0.5f + glyph.X + (float)lyricGlyphFrame.XOffset;
					float num5 = num2 + (float)glyph.Row * num3 + (float)lyricGlyphFrame.YOffset;
					Vector2 val2 = new Vector2(num4 + glyph.Width * 0.5f, num5 + (float)style.fontSize * 0.5f);
					GUI.matrix = Matrix4x4.identity;
					GUIUtility.ScaleAroundPivot(new Vector2((float)lyricGlyphFrame.Scale, (float)lyricGlyphFrame.Scale), val2);
					GUIUtility.RotateAroundPivot((float)lyricGlyphFrame.Rotation * 57.29578f, val2);
					if (settings.Glow.Value)
					{
						Color color2 = (Color)(palette.IsDark ? new Color(0.55f, 0.78f, 1f, 1f) : val);
						color2.a = (float)lyricGlyphFrame.Alpha * Mathf.Clamp01(settings.GlowStrength.Value) * 0.25f;
						GUI.color = color2;
						GUI.Label(new Rect(num4 - 2f, num5, glyph.Width + 12f, num3), glyph.Text, style);
						GUI.Label(new Rect(num4 + 2f, num5, glyph.Width + 12f, num3), glyph.Text, style);
					}
					val.a = (float)lyricGlyphFrame.Alpha;
					GUI.color = val;
					GUI.Label(new Rect(num4, num5, glyph.Width + 12f, num3), glyph.Text, style);
				}
			}
			if (settings.Translation.Value && !string.IsNullOrWhiteSpace(line?.Translation))
			{
				GUI.matrix = Matrix4x4.identity;
				GUI.color = palette.Face;
				GUI.Label(new Rect(num - (float)Screen.width * 0.16f, num2 + (float)rowWidths.Count * num3 + 8f, (float)Screen.width * 0.32f, num3 * 2f), line.Translation, translationStyle);
			}
		}
		finally
		{
			GUI.matrix = matrix;
			GUI.color = color;
			GUI.depth = depth;
			GUI.enabled = enabled;
		}
	}

	public void Dispose()
	{
		Hide();
		custom?.Dispose();
		custom = null;
		if ((Object)(object)font != (Object)null)
		{
			Object.Destroy((Object)(object)font);
		}
		font = null;
		style = null;
		translationStyle = null;
	}
}
