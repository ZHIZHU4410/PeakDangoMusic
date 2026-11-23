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

	private readonly List<Part?> parts = new List<Part?>();

	internal void Update(TMP_Text text, MusicSettings settings, AdaptiveLyrics.Palette palette,
		Material[]? sharedMaterials = null)
	{
		float strength = settings.GlowStrength.Value;
		if (!settings.Glow.Value || strength <= 0f)
		{
			foreach (Part? part in parts) part?.Root.SetActive(false);
			return;
		}
		TMP_TextInfo textInfo = text.textInfo;
		sharedMaterials ??= text.fontSharedMaterials;
		int materialCount = Math.Min(textInfo.materialCount, sharedMaterials.Length);
		for (int i = 0; i < materialCount; i++)
		{
			TMP_MeshInfo val = textInfo.meshInfo[i];
			Material source = sharedMaterials[i];
			if (val.mesh == null || val.vertexCount == 0 || source == null || source.HasProperty("_GlowColor"))
			{
				if (i < parts.Count) parts[i]?.Root.SetActive(false);
				continue;
			}
			while (parts.Count <= i) parts.Add(null);
			if (parts[i] == null)
			{
				Part part = new Part();
				part.Root = new GameObject("Lyric soft halo", new Type[1] { typeof(RectTransform) });
				part.Root.layer = ((Component)text).gameObject.layer;
				part.Root.transform.SetParent(text.transform, false);
				part.Material = new Material(source);
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
				parts[i] = part;
			}
			Part halo = parts[i]!;
			halo.Root.layer = text.gameObject.layer;
			Color glow = palette.Glow;
			float brightness = 1f + strength * 2f;
			glow = new Color(glow.r * brightness, glow.g * brightness, glow.b * brightness, Mathf.Clamp01(strength) * 0.75f);
			halo.Material.EnableKeyword("OUTLINE_ON");
			halo.Material.SetColor("_FaceColor", Color.clear);
			halo.Material.SetColor("_OutlineColor", glow);
			halo.Material.SetFloat("_FaceDilate", 0f);
			halo.Material.SetFloat("_OutlineWidth", 0.18f + 0.25f * settings.GlowRadius.Value);
			halo.Material.SetFloat("_OutlineSoftness", 0.2f + 0.5f * settings.GlowRadius.Value);
			if (halo.Material.HasProperty("_ZTestMode") && source.HasProperty("_ZTestMode"))
			{
				halo.Material.SetFloat("_ZTestMode", source.GetFloat("_ZTestMode"));
			}
			halo.Mesh.Clear();
			halo.Mesh.vertices = val.vertices;
			halo.Mesh.SetUVs(0, val.uvs0);
			halo.Mesh.SetUVs(1, val.uvs2);
			halo.Mesh.triangles = val.triangles;
			if (halo.Colors.Length != val.colors32.Length)
			{
				halo.Colors = new Color32[val.colors32.Length];
			}
			for (int j = 0; j < halo.Colors.Length; j++)
			{
				halo.Colors[j] = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, val.colors32[j].a);
			}
			halo.Mesh.colors32 = halo.Colors;
			halo.Mesh.RecalculateBounds();
			if (halo.Canvas != null)
			{
				halo.Canvas.SetMesh(halo.Mesh);
				halo.Canvas.SetTexture(halo.Material.mainTexture);
			}
			halo.Root.SetActive(true);
		}
		for (int k = materialCount; k < parts.Count; k++)
		{
			parts[k]?.Root.SetActive(false);
		}
	}

	public void Dispose()
	{
		foreach (Part? part in parts)
		{
			if (part == null) continue;
			Object.Destroy((Object)(object)part.Root);
			Object.Destroy((Object)(object)part.Mesh);
			Object.Destroy((Object)(object)part.Material);
		}
		parts.Clear();
	}
}
