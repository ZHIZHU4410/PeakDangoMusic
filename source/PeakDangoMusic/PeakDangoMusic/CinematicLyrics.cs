using System;
using DangoMusic.Core;
using UnityEngine;

namespace PeakDangoMusic;

internal sealed class CinematicLyrics : IDisposable
{
    internal readonly record struct WorldPlacement(Vector3 Position, Quaternion Orientation)
    {
        internal bool IsVisible(Camera camera, MusicSettings settings) =>
            settings.AllowWalls.Value || !Physics.Linecast(camera.transform.position, Position,
                PlayerPerspective.TerrainMask, QueryTriggerInteraction.Ignore);
    }

    private readonly record struct Caption(string Original, string Translation);
    private readonly MusicSettings settings;
    private float openness;
    private int shotId = -1;
    private Vector3 anchor;
    private Quaternion orientation;
    private float subtitleChanged;
    private Caption caption = new("", "");
    private Caption previous = new("", "");
    private LyricsLine? lastLine;
    private Font? font;
    private GUIStyle? primary;
    private GUIStyle? secondary;

    internal CinematicLyrics(MusicSettings settings) => this.settings = settings;

    internal void Tick(bool active, float dt)
    {
        openness = Mathf.MoveTowards(openness, active ? 1f : 0f, dt / 0.55f);
        if (!active) shotId = -1;
    }

    internal WorldPlacement Place(Camera camera, int currentShot)
    {
        if (shotId != currentShot)
        {
            shotId = currentShot;
            float side = currentShot % 4 < 2 ? 0.64f : 0.36f;
            float depth = Mathf.Clamp(settings.Distance.Value, 2f, 12f);
            anchor = Position(camera, side, 0.59f, depth) ?? Position(camera, 1f - side, 0.59f, depth) ??
                Position(camera, 0.5f, 0.55f, depth) ?? camera.ViewportToWorldPoint(new Vector3(side, 0.59f, depth));
            orientation = LyricPlacement.Face(anchor, camera);
        }
        else
        {
            Vector3 view = camera.WorldToViewportPoint(anchor);
            if (view.z < 1.25f || view.x < 0.22f || view.x > 0.78f || view.y < 0.28f || view.y > 0.75f)
            {
                bool behind = view.z < 1.25f;
                Vector3? safe = Position(camera, behind ? 0.5f : Mathf.Clamp(view.x, 0.24f, 0.76f),
                    behind ? 0.58f : Mathf.Clamp(view.y, 0.30f, 0.73f),
                    behind ? Mathf.Clamp(settings.Distance.Value, 2f, 12f) : Mathf.Clamp(view.z, 2f, 12f));
                if (safe is Vector3 point)
                    anchor = Vector3.Lerp(anchor, point, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
            }
        }
        return new WorldPlacement(anchor, orientation);
    }

    private Vector3? Position(Camera camera, float x, float y, float depth)
    {
        Vector3 origin = camera.transform.position;
        Vector3 point = camera.ViewportToWorldPoint(new Vector3(x, y, depth));
        Vector3 ray = point - origin;
        float distance = ray.magnitude;
        if (!settings.AllowWalls.Value && Physics.SphereCast(origin, 0.12f, ray.normalized, out RaycastHit hit,
                distance, PlayerPerspective.TerrainMask, QueryTriggerInteraction.Ignore))
            return hit.distance > 1.45f ? origin + ray.normalized * (hit.distance - 0.24f) : null;
        return point;
    }

    internal void SetLyrics(MusicState? state, long time)
    {
        LyricsLine? line = null;
        if (openness > 0f && state?.PositionAt(time) is long timeMs)
        {
            long position = Math.Clamp(timeMs + settings.Offset.Value, 0L, state.DurationMs);
            var timeline = state.Timeline;
            int index = timeline.FindIndex(position);
            if (index >= 0)
            {
                var item = timeline.Lines[index];
                long end = index + 1 < timeline.Lines.Count ? timeline.Lines[index + 1].StartMs :
                    Math.Min(state.DurationMs, item.StartMs + Math.Max(item.DurationMs, 5000L));
                if (position < end) line = item;
            }
        }
        if (ReferenceEquals(line, lastLine)) return;
        lastLine = line;
        previous = caption;
        caption = line == null ? new Caption("", "") : new Caption(line.Text, line.Translation);
        subtitleChanged = Time.unscaledTime;
    }

    internal void Draw(bool playerOpen)
    {
        if (openness <= 0f || Event.current.type != EventType.Repaint) return;

        float eased = Mathf.SmoothStep(0f, 1f, openness);
        float height = Screen.height * 0.125f;
        float shown = height * eased;
        Rect bottom = new Rect(0f, Screen.height - shown, Screen.width, shown);
        var previousMatrix = GUI.matrix;
        var previousColor = GUI.color;
        var previousContentColor = GUI.contentColor;
        bool enabled = GUI.enabled;
        int depth = GUI.depth;
        try
        {
            GUI.matrix = Matrix4x4.identity;
            GUI.depth = playerOpen ? -900 : -2200;
            GUI.enabled = true;
            GUI.contentColor = Color.white;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, shown), Texture2D.whiteTexture);
            GUI.DrawTexture(bottom, Texture2D.whiteTexture);

            float subtitleOpacity = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.23f, 0.88f, eased));
            float lineBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - subtitleChanged) / 0.16f));
            GUI.BeginGroup(bottom);
            try
            {
                DrawCaption(previous, height, subtitleOpacity * (1f - lineBlend));
                DrawCaption(caption, height, subtitleOpacity * lineBlend);
            }
            finally { GUI.EndGroup(); }
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.contentColor = previousContentColor;
            GUI.enabled = enabled;
            GUI.depth = depth;
        }
    }

    private void DrawCaption(Caption value, float height, float alpha)
    {
        if (alpha <= 0.01f || string.IsNullOrWhiteSpace(value.Original)) return;
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Malgun Gothic", "Arial" }, 30);
        primary ??= new GUIStyle { font = font, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        secondary ??= new GUIStyle(primary);
        GUI.color = new Color(1f, 1f, 1f, alpha);

        bool translationRow = settings.Translation.Value;
        float width = Screen.width * 0.86f;
        float bodyHeight = height * (translationRow ? 0.53f : 0.8f);
        float translationHeight = height * 0.28f;
        var original = new GUIContent(value.Original);
        primary.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.027f, 15f, 42f));
        Fit(primary, original, width, bodyHeight);
        primary.normal.textColor = Color.white;
        float x = (Screen.width - width) * 0.5f;
        float top = translationRow ? height * 0.08f : height * 0.1f;
        GUI.Label(new Rect(x, top, width, bodyHeight), original, primary);
        if (translationRow && !string.IsNullOrWhiteSpace(value.Translation))
        {
            var translation = new GUIContent(value.Translation);
            secondary.fontSize = Mathf.Max(11, Mathf.RoundToInt(primary.fontSize * 0.72f));
            Fit(secondary, translation, width, translationHeight);
            secondary.normal.textColor = new Color(0.87f, 0.91f, 0.98f);
            GUI.Label(new Rect(x, top + bodyHeight, width, translationHeight), translation, secondary);
        }
    }

    private static void Fit(GUIStyle style, GUIContent content, float width, float height)
    {
        while (style.fontSize > 11 && style.CalcHeight(content, width) > height) style.fontSize--;
    }

    public void Dispose()
    {
        if (font != null) UnityEngine.Object.Destroy(font);
    }
}
