using System;
using System.Collections.Generic;
using BepInEx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PeakDangoMusic;

internal sealed class MusicWindow : IDisposable
{
	private Rect rect;

	private bool centered;

	private Font? font;

	private GUISkin? skin;

	private int skinTheme = -1;

	private GUIStyle? heading;

	private GUIStyle? subtitle;

	private readonly List<Texture2D> textures = new List<Texture2D>();

	private int suppressInputUntil = -1;

	private SharedSettingsMenu? owner;

	private bool standalone;

	private string openedScene = "";

	private EventSystem? eventSystem;

	private bool previousNavigation;

	private bool navigationCaptured;

	private GameObject? blocker;

	private bool closeRequested;

	private int lastDrawFrame = -1;

	private bool wasFocused = true;

	private float diagnosedWidth = -1f;

	private float diagnosedHeight = -1f;

	private GUIStyle? ghostButton;

	private GUIStyle? primaryButton;

	private bool miniHidden;
    private bool cursorCaptured;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private float expansion;
    private float expansionVelocity;
    internal bool Open { get; private set; }
    internal void ToggleMini(){if(!Open)miniHidden=!miniHidden;}
    internal void TickMotion(){
        expansion=Mathf.SmoothDamp(expansion,Open?1f:0f,ref expansionVelocity,.13f,Mathf.Infinity,Time.unscaledDeltaTime);
        if(Mathf.Abs(expansion-(Open?1f:0f))<.001f){expansion=Open?1f:0f;expansionVelocity=0;}
    }
    internal void EnsureCursor(){if(Open){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}


	internal bool BlocksPlayerInput
	{
		get
		{
			if (!Open)
			{
				return Time.frameCount <= suppressInputUntil;
			}
			return true;
		}
	}

	internal bool CloseRequested => closeRequested;

	internal void RequestClose()
	{
		closeRequested = true;
	}

	internal void Show(SharedSettingsMenu? source)
	{
		if (!Open)
		{
			previousCursorLock=Cursor.lockState;
            previousCursorVisible=Cursor.visible;
            cursorCaptured=true;
            miniHidden=false;
            owner = source;
			standalone = (Object)(object)source == (Object)null;
			Scene activeScene = SceneManager.GetActiveScene();
			openedScene = activeScene.name;
			closeRequested = false;
			Open = true;
            Plugin.Instance?.Hub.WindowOpened();
			suppressInputUntil = -1;
			MusicPanel.ResetEntrance();
			lastDrawFrame = Time.frameCount;
			wasFocused = Application.isFocused;
			SuspendNativeUI();
            EnsureCursor();
		}
	}

	private void SuspendNativeUI()
	{
		eventSystem = EventSystem.current;
		if ((Object)(object)eventSystem != (Object)null && !navigationCaptured)
		{
			previousNavigation = eventSystem.sendNavigationEvents;
			navigationCaptured = true;
			eventSystem.sendNavigationEvents = false;
		}
		CreateBlocker();
	}

	private void CreateBlocker()
	{
		if ((Object)(object)blocker != (Object)null)
		{
			return;
		}
		try
		{
			blocker = new GameObject("DangoMusic Input Blocker", new Type[3]
			{
				typeof(RectTransform),
				typeof(Canvas),
				typeof(GraphicRaycaster)
			});
			Object.DontDestroyOnLoad((Object)(object)blocker);
			Canvas component = blocker.GetComponent<Canvas>();
			component.renderMode = (RenderMode)0;
			component.sortingOrder = 32767;
			GameObject val = new GameObject("Surface", new Type[3]
			{
				typeof(RectTransform),
				typeof(CanvasRenderer),
				typeof(Image)
			});
			val.transform.SetParent(blocker.transform, false);
			RectTransform val2 = (RectTransform)val.transform;
			val2.anchorMin = Vector2.zero;
			val2.anchorMax = Vector2.one;
			Vector2 offsetMin = (val2.offsetMax = Vector2.zero);
			val2.offsetMin = offsetMin;
			Image component2 = val.GetComponent<Image>();
			((Graphic)component2).color = new Color(0f, 0f, 0f, 0.001f);
			((Graphic)component2).raycastTarget = true;
		}
		catch (Exception error)
		{
			DiagnosticLog.Report("设置窗口", "创建输入遮挡层", error, "MusicWindow.cs", 70);
		}
	}

	private void DestroyBlocker()
	{
		if ((Object)(object)blocker != (Object)null)
		{
			Object.Destroy((Object)(object)blocker);
			blocker = null;
		}
	}

	internal void KeepModalState()
	{
		if (!Open)
		{
			return;
		}
		if ((Object)(object)eventSystem == (Object)null)
		{
			eventSystem = EventSystem.current;
			if ((Object)(object)eventSystem != (Object)null && !navigationCaptured)
			{
				previousNavigation = eventSystem.sendNavigationEvents;
				navigationCaptured = true;
			}
		}
		if ((Object)(object)eventSystem != (Object)null)
		{
			eventSystem.sendNavigationEvents = false;
		}
		CreateBlocker();
        EnsureCursor();
	}

	internal void CheckOwner()
	{
		if (!Open)
		{
			return;
		}
		if (standalone || (!((Object)(object)owner == (Object)null) && ((Behaviour)owner).isActiveAndEnabled))
		{
			string text = openedScene;
			Scene activeScene = SceneManager.GetActiveScene();
			if (!(text != activeScene.name))
			{
				return;
			}
		}
		Close();
	}

	internal void CheckResponsive()
	{
		if (Open)
		{
			if (!Application.isFocused)
			{
				wasFocused = false;
				lastDrawFrame = Time.frameCount;
			}
			else if (!wasFocused)
			{
				wasFocused = true;
				lastDrawFrame = Time.frameCount;
			}
			else if (Time.frameCount - lastDrawFrame > 180)
			{
				DiagnosticLog.Report("设置窗口", "绘制响应监测", new TimeoutException("设置界面连续180帧未绘制，已自动关闭并恢复输入"), "MusicWindow.cs", 101);
				Close();
			}
		}
	}

	internal void Close()
	{
		if (!Open)
		{
			return;
		}
		Open = false;
        Plugin.Instance?.Hub.WindowClosed();
        Plugin.Instance?.Hub.CancelSeek();
        if(cursorCaptured){Cursor.lockState=previousCursorLock;Cursor.visible=previousCursorVisible;cursorCaptured=false;}
        closeRequested = false;
		try
		{
			if (navigationCaptured && (Object)(object)eventSystem != (Object)null)
			{
				eventSystem.sendNavigationEvents = previousNavigation;
			}
			DestroyBlocker();
		}
		finally
		{
			navigationCaptured = false;
			eventSystem = null;
			owner = null;
			suppressInputUntil = Time.frameCount + 1;
			GUI.FocusControl((string)null);
			try
			{
				Plugin? instance = Plugin.Instance;
				if (instance != null)
				{
					((BaseUnityPlugin)instance).Config.Save();
				}
			}
			catch (Exception error)
			{
				DiagnosticLog.Report("设置窗口", "保存配置", error, "MusicWindow.cs", 122);
			}
			GUIManager instance2 = GUIManager.instance;
			Plugin.Instance?.Log("DangoMusic 设置已关闭，游戏输入已恢复。timeScale=" + Time.timeScale.ToString("F2") + " pauseMenu=" + GUIManager.InPauseMenu + " windowBlockingInput=" + ((Object)(object)instance2 != (Object)null && instance2.windowBlockingInput) + " eventSystem=" + ((Object)(object)EventSystem.current != (Object)null) + " cursor=" + ((object)Cursor.lockState/*cast due to constrained. prefix*/).ToString());
		}
	}

	internal void Draw()
	{
		if (!Open && expansion<=0f && miniHidden)return;
        // A closing panel is paint-only; it must not consume game clicks.
        if(!Open && expansion>0 && Event.current.type!=EventType.Layout && Event.current.type!=EventType.Repaint)return;
        if(Open)EnsureCursor();
        lastDrawFrame = Time.frameCount;
		GUISkin val = GUI.skin;
		Matrix4x4 matrix = GUI.matrix;
		Color color = GUI.color;
		Color contentColor = GUI.contentColor;
		Color backgroundColor = GUI.backgroundColor;
		int depth = GUI.depth;
		if ((Object)(object)skin != (Object)null && skinTheme != UiKit.ThemeVersion)
		{
            Object.Destroy(skin);
            foreach(var texture in textures)Object.Destroy(texture);
            textures.Clear();
			skin = null;
		}
		if ((Object)(object)skin == (Object)null)
		{
			if(font == null)font = Font.CreateDynamicFontFromOSFont(new string[3] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
			skin = Object.Instantiate<GUISkin>(val);
			skin.font = font;
			skinTheme = UiKit.ThemeVersion;
			GUIStyle[] array = new GUIStyle[6] { skin.label, skin.button, skin.toggle, skin.textField, skin.window, skin.box };
			foreach (GUIStyle val2 in array)
			{
				val2.font = font;
				val2.fontSize = 18;
				GUIStyleState[] array2 = new GUIStyleState[8] { val2.normal, val2.hover, val2.active, val2.focused, val2.onNormal, val2.onHover, val2.onActive, val2.onFocused };
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j].textColor = UiKit.Text;
				}
			}
			skin.window.normal.background = UiKit.Box(96, 96, new Color(UiKit.WindowTint.r, UiKit.WindowTint.g, UiKit.WindowTint.b, 0.82f), 22);
			skin.window.onNormal.background = skin.window.normal.background;
			skin.window.border = new RectOffset(22, 22, 22, 22);
			skin.window.padding = new RectOffset(22, 22, 18, 18);
			skin.box.normal.background = UiKit.Box(168, 128, UiKit.Glass(0.24f), 14);
			skin.box.border = new RectOffset(14, 14, 14, 14);
			skin.box.padding = new RectOffset(18, 18, 14, 16);
			skin.box.margin = new RectOffset(0, 0, 0, 0);
			skin.button.normal.background = UiKit.Box(220, 44, UiKit.Glass(0.26f), 8);
			skin.button.hover.background = UiKit.Box(220, 44, UiKit.Glass(0.44f), 8);
			skin.button.active.background = UiKit.Box(220, 44, new Color(0.88f, 0.93f, 1f, 0.07f), 8);
			skin.button.onNormal.background = UiKit.Box(220, 44, UiKit.GlassLift(0.74f), 8);
			skin.button.onHover.background = UiKit.Box(220, 44, UiKit.GlassLift(0.88f), 8);
			skin.button.onActive.background = skin.button.active.background;
			skin.button.border = new RectOffset(0, 0, 0, 0);
			skin.button.padding = new RectOffset(16, 16, 8, 8);
			skin.button.margin = new RectOffset(0, 0, 4, 4);
			skin.button.fixedHeight = 0f;
			skin.button.fontSize = 14;
			skin.button.alignment = (TextAnchor)4;
			skin.label.wordWrap = true;
			skin.label.margin = new RectOffset(0, 0, 4, 6);
			skin.toggle.padding.top = 8;
			skin.toggle.padding.bottom = 8;
			skin.toggle.margin = new RectOffset(0, 0, 4, 6);
			skin.textField.normal.background = Background(UiKit.GlassLift(0.62f));
			skin.textField.hover.background = Background(UiKit.GlassLift(0.78f));
			skin.textField.focused.background = Background(UiKit.GlassLift(0.9f));
			skin.textField.padding = new RectOffset(12, 12, 10, 10);
			skin.textField.margin = new RectOffset(0, 0, 4, 4);
			skin.textField.fixedHeight = 0f;
			skin.textField.clipping = (TextClipping)1;
			skin.horizontalSlider.normal.background = UiKit.Box(24, 12, new Color(0.1f, 0.13f, 0.17f, 0.3f), 6);
			skin.horizontalSlider.border = new RectOffset(6, 6, 4, 4);
			skin.horizontalSlider.fixedHeight = 8f;
			skin.horizontalSliderThumb.normal.background = UiKit.Box(18, 18, new Color(1f, 1f, 1f, 0.92f), 9);
			skin.horizontalSliderThumb.hover.background = skin.horizontalSliderThumb.normal.background;
			skin.horizontalSliderThumb.active.background = UiKit.Box(18, 18, new Color(0.06f, 0.09f, 0.13f, 0.32f), 9);
			skin.horizontalSliderThumb.border = new RectOffset(0, 0, 0, 0);
			skin.horizontalSliderThumb.fixedWidth = 18f;
			skin.horizontalSliderThumb.fixedHeight = 18f;
			skin.verticalScrollbar.fixedWidth = 0f;
			skin.verticalScrollbar.margin = new RectOffset(0, 0, 0, 0);
			skin.verticalScrollbar.normal.background = null;
			skin.verticalScrollbarThumb.normal.background = Background(new Color(0.32f, 0.37f, 0.44f, 0.75f));
			skin.verticalScrollbarThumb.hover.background = Background(new Color(0.2f, 0.26f, 0.33f, 0.7f));
			skin.verticalScrollbar.border = new RectOffset(0, 0, 0, 0);
			skin.verticalScrollbarThumb.border = new RectOffset(0, 0, 0, 0);
			heading = new GUIStyle(skin.label)
			{
				fontSize = 22,
				fontStyle = (FontStyle)1
			};
			subtitle = new GUIStyle(skin.label)
			{
				fontSize = 12
			};
			subtitle.normal.textColor = new Color(0.52f, 0.6f, 0.68f, 1f);
		}
		GUI.skin = skin;
		try
		{
			float num = Mathf.Clamp(Mathf.Min((float)Screen.width / 1920f, (float)Screen.height / 1080f), 0.85f, 1.5f);
			float num2 = (float)Screen.width / num;
			float num3 = (float)Screen.height / num;
			GUI.matrix = Matrix4x4.Scale(new Vector3(num, num, 1f));
			GUI.color = (GUI.contentColor = (GUI.backgroundColor = Color.white));
			GUI.depth = -1000;
			GUI.color = new Color(0f, 0f, 0f, 0.32f * expansion);
			GUI.DrawTexture(new Rect(0f, 0f, num2, num3), (Texture)(object)Texture2D.whiteTexture);
			GUI.color = Color.white;
			rect.width = Mathf.Min(1440f, Mathf.Max(280f, num2 - 48f));
			rect.height = Mathf.Min(940f, Mathf.Max(240f, num3 - 48f));
			if (!centered)
			{
				rect.x = (num2 - rect.width) * 0.5f;
				rect.y = (num3 - rect.height) * 0.5f;
				centered = true;
			}
			rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, num2 - rect.width));
			rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, num3 - rect.height));

            float miniW=Plugin.Instance?.Hub.MiniWidth()??320f;
            float miniX=Mathf.Clamp(Plugin.Instance?.Settings.MiniX.Value??50f,0f,100f)*0.01f;
            float miniY = Mathf.Clamp(Plugin.Instance?.Settings.MiniY.Value ?? 14f, 0f, Mathf.Max(0f, num3 - 78f));
            Rect compact=new Rect(Mathf.Clamp((num2-Mathf.Min(miniW,num2-24f))*miniX,0f,Mathf.Max(0f,num2-Mathf.Min(miniW,num2-24f))),miniY,Mathf.Min(miniW,num2-24f),62f);
            if(!Open && expansion<=0f){
                if(!miniHidden)Plugin.Instance?.Hub.DrawMini(compact);
            }else{
                Rect animated=new Rect(
                    Mathf.Lerp(compact.x,rect.x,expansion),Mathf.Lerp(compact.y,rect.y,expansion),
                    Mathf.Lerp(compact.width,rect.width,expansion),Mathf.Lerp(compact.height,rect.height,expansion));
                GUI.matrix=GUI.matrix * Matrix4x4.Translate(new Vector3(animated.x,animated.y,0)) *
                    Matrix4x4.Scale(new Vector3(animated.width/rect.width,animated.height/rect.height,1)) *
                    Matrix4x4.Translate(new Vector3(-rect.x,-rect.y,0));
                UiKit.Shadow(rect,26,1f);
                GUI.Window(5260365,rect,(GUI.WindowFunction)Content,"");
            }
			if (rect.width != diagnosedWidth || rect.height != diagnosedHeight)
			{
				diagnosedWidth = rect.width;
				diagnosedHeight = rect.height;
				Plugin.Instance?.Log("音乐窗口外框 " + rect.width.ToString("F0") + "x" + rect.height.ToString("F0") + " @ " + rect.x.ToString("F0") + "," + rect.y.ToString("F0"));
			}
		}
		finally
		{
			GUI.skin = val;
			GUI.matrix = matrix;
			GUI.color = color;
			GUI.contentColor = contentColor;
			GUI.backgroundColor = backgroundColor;
			GUI.depth = depth;
		}
	}

	private void Content(int id)
	{
		float num = Mathf.Max(200f, rect.width - 48f);
		GUILayout.BeginArea(new Rect(24f, 20f, num, Mathf.Max(160f, rect.height - 40f)));
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
		GUILayout.Label("Muonium · PEAK 音乐播放器", heading, Array.Empty<GUILayoutOption>());
		GUILayout.Label("原生曲库 / 多平台聚合  ·  设置自动保存  ·  v" + Plugin.Version + "" + ((MusicPanel.Feedback.Length > 0) ? ("  ·  " + MusicPanel.Feedback) : ""), subtitle, new GUILayoutOption[1] { GUILayout.Height(24f) });
		GUILayout.EndVertical();
		GUILayout.FlexibleSpace();
		Rect val0 = GUILayoutUtility.GetRect(240f, 42f, new GUILayoutOption[2]
		{
			GUILayout.Width(240f),
			GUILayout.Height(42f)
		});
		bool flagQq = UiKit.Hover(val0);
		UiKit.Chip(val0, (float)(flagQq ? 0.62f : 0.4f), 14);
		GUIStyle qq1 = new GUIStyle(GUI.skin.label)
		{
			fontSize = 13,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleLeft,
			padding = new RectOffset(16, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0)
		};
		qq1.normal.textColor = UiKit.Ink;
		GUI.Label(new Rect(val0.x, val0.y + 3f, val0.width, 19f), "QQ 群  791421851", qq1);
		GUIStyle qq2 = new GUIStyle(GUI.skin.label)
		{
			fontSize = 10,
			alignment = TextAnchor.MiddleLeft,
			padding = new RectOffset(16, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0)
		};
		qq2.normal.textColor = UiKit.Faint;
		GUI.Label(new Rect(val0.x, val0.y + 20f, val0.width, 17f), "强烈建议加群 · 后续更新 / BUG 反馈", qq2);
		if (UiKit.Click(val0))
		{
			GUIUtility.systemCopyBuffer = "791421851";
			MusicPanel.SetFeedback("QQ 群号 791421851 已复制");
		}
		GUILayout.Space(8f);
		Rect val = GUILayoutUtility.GetRect(118f, 42f, new GUILayoutOption[2]
		{
			GUILayout.Width(118f),
			GUILayout.Height(42f)
		});
		GUILayout.Space(8f);
		Rect val2 = GUILayoutUtility.GetRect(176f, 42f, new GUILayoutOption[2]
		{
			GUILayout.Width(176f),
			GUILayout.Height(42f)
		});
		WindowButton(val, "支持我", primary: false, () =>
		{
			Application.OpenURL("https://afdian.com/a/blf4410");
		});
		if (WindowButton(val2, "关闭  ·  Esc", primary: true, null))
		{
			RequestClose();
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(16f);
		try
		{
			Plugin.Instance.Hub.Draw(num, rect.height - 40f);
		}
		catch (Exception error)
		{
			DiagnosticLog.Report("设置窗口", "绘制设置面板", error, "MusicWindow.cs", 221);
		}
		GUILayout.EndArea();
		GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, rect.width - 420f), 90f));
	}

	private bool WindowButton(Rect rect, string label, bool primary, Action? action)
	{
		if (ghostButton == null)
		{
			ghostButton = new GUIStyle(GUI.skin.label)
			{
				alignment = (TextAnchor)4,
				fontSize = 13,
				wordWrap = false,
				clipping = (TextClipping)1
			};
			primaryButton = new GUIStyle(ghostButton)
			{
				fontSize = 14,
				fontStyle = (FontStyle)1
			};
		}
		bool flag = UiKit.Hover(rect);
		if (primary)
		{
			UiKit.Chip(rect, flag ? 0.74f : 0.54f, Mathf.RoundToInt(rect.height * 0.5f));
			primaryButton.normal.textColor = UiKit.Text;
			GUI.Label(rect, label, primaryButton);
		}
		else
		{
			UiKit.Round(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), flag ? new Color(1f, 1f, 1f, 0.28f) : new Color(1f, 1f, 1f, 0.14f), Mathf.RoundToInt(rect.height * 0.5f) + 1);
			UiKit.Round(rect, (Color)(flag ? UiKit.RowHover : new Color(0.88f, 0.93f, 1f, 0.08f)), Mathf.RoundToInt(rect.height * 0.5f));
			ghostButton.normal.textColor = (flag ? UiKit.Text : UiKit.Muted);
			GUI.Label(rect, label, ghostButton);
		}
		if (!UiKit.Click(rect))
		{
			return false;
		}
		action?.Invoke();
		return true;
	}

	private Texture2D Background(Color color)
	{
		Texture2D val = new Texture2D(1, 1);
		val.SetPixel(0, 0, color);
		val.Apply();
		textures.Add(val);
		return val;
	}

	public void Dispose()
	{
		Close();
		if ((Object)(object)skin != (Object)null)
		{
			Object.Destroy((Object)(object)skin);
		}
		if ((Object)(object)font != (Object)null)
		{
			Object.Destroy((Object)(object)font);
		}
		foreach (Texture2D texture in textures)
		{
			Object.Destroy((Object)(object)texture);
		}
		textures.Clear();
		skin = null;
		font = null;
	}
}
