using System;
using System.Linq;
using DangoMusic.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PeakDangoMusic;

internal sealed class FoliaView : IDisposable
{
	private readonly AdaptiveLyrics.Palette palette = new AdaptiveLyrics.Palette();

	private readonly MusicSettings settings;

	private readonly FoliaClient client = new FoliaClient();

	private Texture2D? texture;

	private GameObject? root;
	private LyricWeatherGraphic? weatherGraphic;

	private RawImage? image;

	private Canvas? canvas;

	private Material? material;

	private bool overlay;

	private bool visible;

	private int anchorIndex = -1;

	private string anchorKey = "";

	private Vector3 anchor;

	private Vector3 viewport;

	private Quaternion orientation;

	private int side;

	private Camera? anchorCamera;

	private bool wasOverlay = true;
	private bool wasCinematic;

	private string fault = "";

	private float retryAt;

	private readonly LyricLineView lineView = new LyricLineView();

	internal string Status
	{
		get
		{
			if (fault.Length <= 0)
			{
				return client.Status;
			}
			return fault;
		}
	}

	internal FoliaView(MusicSettings settings)
	{
		this.settings = settings;
	}

	internal void Invalidate()
	{
		anchorIndex = -1;
		wasOverlay = true;
		fault = "";
		retryAt = 0f;
	}

	internal void Restart()
	{
		client.Restart();
		Invalidate();
	}

	internal void Suspend()
	{
		visible = false;
		overlay = false;
		if ((Object)(object)root != (Object)null)
		{
			root.SetActive(false);
		}
		client.Set(null);
	}

	internal bool Tick(MusicState state, Camera? camera, bool menu, long now, CinematicLyrics.WorldPlacement? shot)
	{
		if (fault.Length > 0 && Time.unscaledTime < retryAt)
		{
			return false;
		}
		try
		{
			return Paint(state, camera, menu, now, shot);
		}
		catch (Exception ex) when ((ex is UnityException || ex is ArgumentException || ex is InvalidOperationException) ? true : false)
		{
			fault = DiagnosticLog.Report("Folia", "呈现歌词画面", ex, "FoliaView.cs", 41) + "\n已回退Dango";
			retryAt = Time.unscaledTime + 5f;
			visible = (overlay = false);
			if ((Object)(object)root != (Object)null)
			{
				root.SetActive(false);
			}
			client.Set(null);
			return false;
		}
	}

	private bool Paint(MusicState state, Camera? camera, bool menu, long now, CinematicLyrics.WorldPlacement? shot)
	{
		bool leavingCinematic = wasCinematic && !shot.HasValue;
		wasCinematic = shot.HasValue;
		fault = "";
		Vector2 unused = (Vector2)((!menu && settings.FoliaPlacement.Value == 0 && (Object)(object)camera != (Object)null) ? (Vector2)(camera.WorldToViewportPoint(shot?.Position ?? anchor)) : new Vector2((side == 0) ? 0.25f : 0.75f, 0.61f));
		palette.Update(unused);
		client.Set(new FoliaClient.Request(state, settings, palette));
		FoliaClient.Frame frame = client.TakeFrame();
		if (frame != null)
		{
			if ((Object)(object)texture == (Object)null || ((Texture)texture).width != frame.Width || ((Texture)texture).height != frame.Height)
			{
				if ((Object)(object)texture != (Object)null)
				{
					Object.Destroy((Object)(object)texture);
				}
				texture = new Texture2D(frame.Width, frame.Height, (TextureFormat)14, false)
				{
					filterMode = (FilterMode)1,
					wrapMode = (TextureWrapMode)1
				};
			}
			try
			{
				texture.LoadRawTextureData(frame.Pixels);
				texture.Apply(false, false);
			}
			finally
			{
				client.ReturnFrame(frame);
			}
		}
		long? num = state.PositionAt(now);
		visible = settings.Enabled.Value && num.HasValue && state.Timeline.Lines.Count > 0;
		if ((Object)(object)texture == (Object)null || !client.Ready)
		{
			if ((Object)(object)root != (Object)null)
			{
				root.SetActive(false);
			}
			overlay = false;
			return false;
		}
		int num2 = lineView.Get(state.Timeline, settings.SplitLines.Value).FindIndex(Math.Clamp(num.GetValueOrDefault() + settings.Offset.Value, 0L, Math.Max(0L, state.DurationMs)));
		if (num2 != anchorIndex || state.Key != anchorKey || leavingCinematic)
		{
			anchorIndex = num2;
			anchorKey = state.Key;
			if (!shot.HasValue) side = Math.Max(0, num2) % 2;
			if (!shot.HasValue && camera != null)
			{
				Place(camera);
			}
		}
		overlay = menu || (Object)(object)camera == (Object)null || settings.FoliaPlacement.Value != 0;
		if (overlay)
		{
			wasOverlay = true;
			if ((Object)(object)root != (Object)null)
			{
				root.SetActive(false);
			}
			return true;
		}
		if (!shot.HasValue && (leavingCinematic || wasOverlay || anchorCamera != camera))
		{
			Place(camera);
			wasOverlay = false;
		}
		EnsureWorld();
		if ((Object)(object)root == (Object)null || (Object)(object)image == (Object)null || (Object)(object)canvas == (Object)null)
		{
			return false;
		}
		if (!visible)
		{
			root.SetActive(false);
			return true;
		}
		if (!shot.HasValue && settings.Follow.Value)
		{
			anchor = camera.ViewportToWorldPoint(viewport);
		}
		if (!shot.HasValue && settings.Smart.Value)
		{
			Vector3 val = camera.WorldToViewportPoint(anchor);
			if (val.z < 1f || val.x < 0.03f || val.x > 0.97f || val.y < 0.08f || val.y > 0.95f)
			{
				Place(camera);
			}
		}
		if (!shot.HasValue) anchor = LyricPlacement.KeepDistance(anchor, camera, settings);
		Vector3 positionAnchor = shot?.Position ?? anchor;
		root.transform.position = positionAnchor + LyricWeather.Drift(0.26f * settings.FoliaSize.Value);
		root.transform.rotation = settings.FacePlayer.Value ? LyricPlacement.Face(positionAnchor, camera) :
			(shot?.Orientation ?? orientation);
		float num3 = Mathf.Clamp(settings.Distance.Value * 0.65f * settings.FoliaSize.Value, 1.5f, 18f);
		root.transform.localScale = Vector3.one * (num3 / 960f);
		canvas.worldCamera = camera;
		image.texture = (Texture)(object)texture;
		((Graphic)image).color = new Color((palette.IsDark && !settings.FoliaBackground.Value) ? 1f : settings.Brightness.Value, (palette.IsDark && !settings.FoliaBackground.Value) ? 1f : settings.Brightness.Value, (palette.IsDark && !settings.FoliaBackground.Value) ? 1f : settings.Brightness.Value, settings.FoliaOpacity.Value);
		Material? val2 = material;
		if (val2 != null)
		{
			val2.SetInt("unity_GUIZTestMode", (settings.AllowWalls.Value && !settings.BehindPlayer.Value) ? 8 : 4);
		}
		bool active = shot?.IsVisible(camera, settings) ??
			(settings.AllowWalls.Value || !Physics.Linecast(camera.transform.position, positionAnchor,
				PlayerPerspective.TerrainMask, QueryTriggerInteraction.Ignore));
		root.SetActive(active);
		if (active)
		{
			var weather = LyricWeatherGraphic.Prepare(weatherGraphic, root.transform, true, settings.FoliaOpacity.Value);
			if (weather != null)
			{
				weatherGraphic = weather;
				weather.DrawFolia(texture!, settings.FoliaBackground.Value);
			}
		}
		return true;
	}

	private void Place(Camera camera)
	{
		viewport = LyricPlacement.Candidates(camera, settings, side).First();
		anchor = camera.ViewportToWorldPoint(viewport);
		orientation = LyricPlacement.Face(anchor, camera);
		anchorCamera = camera;
	}

	private void EnsureWorld()
	{
		if (!((Object)(object)root != (Object)null))
		{
			root = new GameObject("DangoMusic Folia stage", new Type[2]
			{
				typeof(RectTransform),
				typeof(Canvas)
			});
			Object.DontDestroyOnLoad((Object)(object)root);
			canvas = root.GetComponent<Canvas>();
			canvas.renderMode = (RenderMode)2;
			canvas.overrideSorting = true;
			canvas.sortingOrder = 32760;
			((RectTransform)root.transform).sizeDelta = new Vector2(960f, 540f);
			GameObject val = new GameObject("Folia pixels", new Type[3]
			{
				typeof(RectTransform),
				typeof(CanvasRenderer),
				typeof(RawImage)
			});
			val.transform.SetParent(root.transform, false);
			image = val.GetComponent<RawImage>();
			((Graphic)image).raycastTarget = false;
			RectTransform val2 = (RectTransform)val.transform;
			val2.anchorMin = Vector2.zero;
			val2.anchorMax = Vector2.one;
			Vector2 offsetMin = (val2.offsetMax = Vector2.zero);
			val2.offsetMin = offsetMin;
			Shader val3 = Shader.Find("UI/Default");
			if ((Object)(object)val3 != (Object)null)
			{
				material = new Material(val3);
				((Graphic)image).material = material;
			}
		}
	}

	internal void Draw()
	{
		if (!overlay || !visible || (Object)(object)texture == (Object)null || !client.Ready || (int)Event.current.type != 7)
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
			GUI.color = new Color((palette.IsDark && !settings.FoliaBackground.Value) ? 1f : settings.Brightness.Value, (palette.IsDark && !settings.FoliaBackground.Value) ? 1f : settings.Brightness.Value, (palette.IsDark && !settings.FoliaBackground.Value) ? 1f : settings.Brightness.Value, settings.FoliaOpacity.Value);
			Rect val;
			if (settings.FoliaPlacement.Value == 2)
			{
				val = new Rect(0f, 0f, (float)Screen.width, (float)Screen.height);
			}
			else
			{
				float num = Mathf.Min((float)Screen.width * 0.46f, (float)Screen.width * 0.36f * settings.FoliaSize.Value);
				float num2 = num * 9f / 16f;
				float num3 = (float)Screen.width * ((side == 0) ? 0.25f : 0.75f);
				val = new Rect(Mathf.Clamp(num3 - num * 0.5f, 0f, Mathf.Max(0f, (float)Screen.width - num)), Mathf.Clamp((float)Screen.height * 0.39f - num2 * 0.5f, 0f, Mathf.Max(0f, (float)Screen.height - num2)), num, num2);
			}
			GUI.DrawTexture(val, (Texture)(object)texture, (ScaleMode)2, true);
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
		client.Dispose();
		if ((Object)(object)root != (Object)null)
		{
			Object.Destroy((Object)(object)root);
		}
		if ((Object)(object)material != (Object)null)
		{
			Object.Destroy((Object)(object)material);
		}
		if ((Object)(object)texture != (Object)null)
		{
			Object.Destroy((Object)(object)texture);
		}
	}
}
