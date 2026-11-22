using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakDangoMusic;

internal sealed class TextHalo : IDisposable
{
	private sealed class Part
	{
		internal GameObject Root;

		internal Mesh Mesh;

		internal Material Material;

		internal Color32[] Colors = Array.Empty<Color32>();

		internal CanvasRenderer? Canvas;
	}

	private readonly List<Part> parts = new List<Part>();

	internal void Update(TMP_Text text, MusicSettings settings, AdaptiveLyrics.Palette palette)
	{
		TMP_TextInfo textInfo = text.textInfo;
		Material[] fontSharedMaterials = text.fontSharedMaterials;
		for (int i = 0; i < textInfo.materialCount && i < fontSharedMaterials.Length; i++)
		{
			TMP_MeshInfo val = textInfo.meshInfo[i];
			if ((Object)(object)val.mesh == (Object)null)
			{
				continue;
			}
			while (parts.Count <= i)
			{
				Part part = new Part();
				part.Root = new GameObject("Lyric soft halo", new Type[1] { typeof(RectTransform) });
				part.Root.layer = ((Component)text).gameObject.layer;
				part.Root.transform.SetParent(text.transform, false);
				part.Material = new Material(fontSharedMaterials[i]);
				part.Mesh = new Mesh();
				if (text is TextMeshProUGUI)
				{
					part.Canvas = part.Root.AddComponent<CanvasRenderer>();
					part.Canvas.materialCount = 1;
					part.Canvas.SetMaterial(part.Material, 0);
				}
				else
				{
					part.Root.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
					MeshRenderer val2 = part.Root.AddComponent<MeshRenderer>();
					((Renderer)val2).sharedMaterial = part.Material;
					((Renderer)val2).shadowCastingMode = (ShadowCastingMode)0;
					((Renderer)val2).receiveShadows = false;
					((Renderer)val2).sortingOrder = ((TextMeshPro)text).renderer.sortingOrder - 1;
				}
				parts.Add(part);
			}
			Part part2 = parts[i];
			bool flag = settings.Glow.Value && settings.GlowStrength.Value > 0f && !fontSharedMaterials[i].HasProperty("_GlowColor");
			part2.Root.SetActive(flag);
			if (flag)
			{
				part2.Root.layer = ((Component)text).gameObject.layer;
				Color val3 = palette.Glow;
				float num = 1f + settings.GlowStrength.Value * 2f;
				val3 = new Color(val3.r * num, val3.g * num, val3.b * num, Mathf.Clamp01(settings.GlowStrength.Value) * 0.75f);
				part2.Material.EnableKeyword("OUTLINE_ON");
				part2.Material.SetColor("_FaceColor", Color.clear);
				part2.Material.SetColor("_OutlineColor", val3);
				part2.Material.SetFloat("_FaceDilate", 0f);
				part2.Material.SetFloat("_OutlineWidth", 0.18f + 0.25f * settings.GlowRadius.Value);
				part2.Material.SetFloat("_OutlineSoftness", 0.2f + 0.5f * settings.GlowRadius.Value);
				if (part2.Material.HasProperty("_ZTestMode") && fontSharedMaterials[i].HasProperty("_ZTestMode"))
				{
					part2.Material.SetFloat("_ZTestMode", fontSharedMaterials[i].GetFloat("_ZTestMode"));
				}
				part2.Mesh.Clear();
				part2.Mesh.vertices = val.vertices;
				part2.Mesh.SetUVs(0, val.uvs0);
				part2.Mesh.SetUVs(1, val.uvs2);
				part2.Mesh.triangles = val.triangles;
				if (part2.Colors.Length != val.colors32.Length)
				{
					part2.Colors = new Color32[val.colors32.Length];
				}
				for (int j = 0; j < part2.Colors.Length; j++)
				{
					part2.Colors[j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, val.colors32[j].a);
				}
				part2.Mesh.colors32 = part2.Colors;
				part2.Mesh.RecalculateBounds();
				if ((Object)(object)part2.Canvas != (Object)null)
				{
					part2.Canvas.SetMesh(part2.Mesh);
					part2.Canvas.SetTexture(part2.Material.mainTexture);
				}
			}
		}
		for (int k = textInfo.materialCount; k < parts.Count; k++)
		{
			parts[k].Root.SetActive(false);
		}
	}

	public void Dispose()
	{
		foreach (Part part in parts)
		{
			Object.Destroy((Object)(object)part.Root);
			Object.Destroy((Object)(object)part.Mesh);
			Object.Destroy((Object)(object)part.Material);
		}
		parts.Clear();
	}
}
