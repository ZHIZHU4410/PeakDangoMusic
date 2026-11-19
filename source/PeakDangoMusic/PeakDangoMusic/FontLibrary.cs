using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PeakDangoMusic;

internal sealed class FontLibrary
{
	internal sealed class Choice
	{
		internal string Path = "";

		internal string Label = "";
	}

	internal sealed class Asset : IDisposable
	{
		internal TMP_FontAsset Main;

		private readonly List<TMP_FontAsset> owned = new List<TMP_FontAsset>();

		internal Asset(string selected)
		{
			try
			{
				if (!IsFont(selected))
				{
					throw new IOException("所选字体文件不存在");
				}
				Main = Load(selected, 44, 512);
				owned.Add(Main);
				if ((Object)(object)((TMP_Asset)Main).material == (Object)null || !((TMP_Asset)Main).material.shader.isSupported)
				{
					throw new InvalidOperationException("字体材质不可用");
				}
				TMP_FontAsset main = Main;
				if (main.fallbackFontAssetTable == null)
				{
					List<TMP_FontAsset> list = (main.fallbackFontAssetTable = new List<TMP_FontAsset>());
				}
				string[] array = new string[3] { "msyh.ttc", "malgun.ttf", "arial.ttf" };
				foreach (string path in array)
				{
					string path2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), path);
					if (File.Exists(path2))
					{
						try
						{
							TMP_FontAsset item = Load(path2, 32, 256);
							owned.Add(item);
							Main.fallbackFontAssetTable.Add(item);
						}
						catch (Exception)
						{
						}
					}
				}
			}
			catch
			{
				Dispose();
				throw;
			}
		}

		public void Dispose()
		{
			foreach (TMP_FontAsset item in owned)
			{
				Texture2D[] atlasTextures = item.atlasTextures;
				foreach (Texture2D val in atlasTextures)
				{
					if ((Object)(object)val != (Object)null)
					{
						Object.Destroy((Object)(object)val);
					}
				}
				if ((Object)(object)((TMP_Asset)item).material != (Object)null)
				{
					Object.Destroy((Object)(object)((TMP_Asset)item).material);
				}
				Object.Destroy((Object)(object)item);
			}
			owned.Clear();
		}
	}

	internal readonly List<Choice> Choices = new List<Choice>();

	internal string Status = "";

	internal FontLibrary(string folder)
	{
		Refresh(folder);
	}

	internal void Refresh(string folder)
	{
		Choices.Clear();
		Choices.Add(new Choice
		{
			Label = "系统默认字体"
		});
		try
		{
			if (string.IsNullOrWhiteSpace(folder)){Status="使用系统字体；需要自定义字体时再选择目录。";return;}
			if (!Directory.Exists(folder))
			{
				Status = "字体目录不存在，可修改目录后刷新。";
				return;
			}
			foreach (string item in (from p in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
				where Enumerable.Contains(new string[3] { ".ttf", ".otf", ".ttc" }, Path.GetExtension(p).ToLowerInvariant())
				select p).OrderBy((string p) => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).Take(2000))
			{
				Choices.Add(new Choice
				{
					Path = item,
					Label = Path.GetFileNameWithoutExtension(item)
				});
			}
			Status = "已找到 " + (Choices.Count - 1) + " 个本地字体";
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) ? true : false)
		{
			Status = "字体扫描失败：" + ex.Message;
		}
	}

	internal static bool IsFont(string path)
	{
		if (File.Exists(path))
		{
			return Enumerable.Contains(new string[3] { ".ttf", ".otf", ".ttc" }, Path.GetExtension(path).ToLowerInvariant());
		}
		return false;
	}

	internal static TMP_FontAsset Load(string path)
	{
		return Load(path, 44, 512);
	}

	internal static TMP_FontAsset Load(string path, int pointSize, int atlasSize)
	{
		TMP_FontAsset val = TMP_FontAsset.CreateFontAsset(path, 0, pointSize, 8, (GlyphRenderMode)4165, atlasSize, atlasSize);
		if ((Object)(object)val == (Object)null)
		{
			throw new InvalidOperationException("无法读取所选字体");
		}
		val.isMultiAtlasTexturesEnabled = true;
		return val;
	}
}
