using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeakDangoMusic;

internal sealed class LyricWeatherGraphic : MaskableGraphic
{
    private readonly Rect[] glyphs = new Rect[24];
    private int glyphCount;
    private float nextFrame;
    private float nextSample;
    private float now;
    private float opacity;
    private LyricWeather.Surface surface;

    internal static LyricWeatherGraphic? Prepare(LyricWeatherGraphic? graphic, Transform parent, bool hasCanvas, float alpha)
    {
        var (kind, strength) = LyricWeather.Coating;
        if (kind == LyricWeather.Surface.None || alpha <= 0.01f)
        {
            if (graphic != null)
            {
                graphic.enabled = false;
                graphic.surface = LyricWeather.Surface.None;
            }
            return null;
        }
        if (graphic == null)
        {
            if (!hasCanvas)
            {
                var stage = new GameObject("Lyric weather canvas", typeof(RectTransform), typeof(Canvas));
                stage.transform.SetParent(parent, false);
                stage.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                parent = stage.transform;
            }
            var node = new GameObject("Lyric weather", typeof(RectTransform), typeof(LyricWeatherGraphic));
            node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            if (hasCanvas) rect.sizeDelta = ((RectTransform)parent).rect.size;
            rect.anchoredPosition3D = new Vector3(0f, 0f, hasCanvas ? -0.8f : -0.012f);
            graphic = node.GetComponent<LyricWeatherGraphic>();
            graphic.raycastTarget = false;
        }

        if (graphic.surface != kind)
        {
            graphic.nextFrame = 0f;
            graphic.nextSample = 0f;
        }
        graphic.enabled = true;
        graphic.surface = kind;
        graphic.opacity = alpha * strength;
        return graphic;
    }

    internal void DrawGlyphs(TMP_TextInfo info)
    {
        if (!BeginFrame()) return;
        glyphCount = 0;
        int step = Mathf.Max(1, Mathf.CeilToInt(info.characterCount / (float)glyphs.Length));
        for (int index = 0; index < info.characterCount && glyphCount < glyphs.Length; index += step)
        {
            TMP_CharacterInfo glyph = info.characterInfo[index];
            if (!glyph.isVisible) continue;
            var mesh = info.meshInfo[glyph.materialReferenceIndex];
            int v = glyph.vertexIndex;
            if (mesh.colors32[v].a < 48) continue;
            Vector3 bottom = mesh.vertices[v], topLeft = mesh.vertices[v + 1], topRight = mesh.vertices[v + 2];
            float width = Mathf.Abs(topRight.x - topLeft.x);
            float height = Mathf.Abs((topLeft.y + topRight.y) * 0.5f - bottom.y);
            if (width < 0.001f || height < 0.001f) continue;
            glyphs[glyphCount++] = new Rect(Mathf.Min(topLeft.x, topRight.x), (topLeft.y + topRight.y) * 0.5f, width, height);
        }
        SetVerticesDirty();
    }

    internal void DrawBounds(Bounds bounds)
    {
        if (!BeginFrame()) return;
        var frame = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        glyphCount = 0;
        if (frame.width > 0f && frame.height > 0f) FillRow(frame);
        SetVerticesDirty();
    }

    internal void DrawFolia(Texture2D texture, bool background)
    {
        if (!BeginFrame()) return;
        if (now >= nextSample)
        {
            nextSample = now + 0.18f;
            var frame = new Rect(-480f, -270f, 960f, 540f);
            glyphCount = 0;
            if (!background && texture.isReadable)
            {
                int columns = 20;
                for (int column = 0; column < columns; column++)
                {
                    float x = 0.07f + (column + 0.5f) * 0.86f / columns;
                    int px = Mathf.Min(texture.width - 1, (int)(texture.width * x));
                    for (int row = 29; row >= 4; row--)
                    {
                        float y = row / 34f;
                        if (texture.GetPixel(px, (int)(y * texture.height)).a < 0.56f) continue;
                        glyphs[glyphCount++] = new Rect(frame.xMin + frame.width * x - 10f, frame.yMin + frame.height * y, 20f, 32f);
                        break;
                    }
                }
            }
            if (background) FillRow(new Rect(-295f, -30f, 590f, 70f));
        }
        SetVerticesDirty();
    }

    private void FillRow(Rect frame)
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(frame.width / frame.height * 1.3f), 1, glyphs.Length);
        float width = frame.width / count;
        for (int i = 0; i < count; i++)
            glyphs[glyphCount++] = new Rect(frame.xMin + width * i, frame.yMax, width, frame.height);
    }

    private bool BeginFrame()
    {
        now = Time.unscaledTime;
        if (now < nextFrame) return false;
        nextFrame = now + 1f / 35f;
        return true;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        for (int i = 0; i < glyphCount; i++)
        {
            Rect point = glyphs[i];
            float unit = Mathf.Min(point.height, point.width * 1.35f);
            float seed = Mathf.Repeat(i * 0.618034f + 0.13f, 1f);
            var top = new Vector2(point.x + point.width * (0.24f + seed * 0.46f), point.y);
            switch (surface)
            {
                case LyricWeather.Surface.Snow:
                    FloatBlob(mesh, top, unit * (0.25f + seed * 0.14f), unit * (0.1f + opacity * 0.08f), 0f,
                        new Color(0.74f, 0.9f, 1f, opacity * 0.92f));
                    FloatBlob(mesh, top + new Vector2(unit * 0.06f, unit * 0.045f), unit * 0.22f, unit * 0.10f, 0f,
                        new Color(1f, 1f, 1f, opacity));
                    break;
                case LyricWeather.Surface.Rain:
                    float age = Mathf.Repeat(now * (0.8f + seed * 0.45f) + seed, 1f);
                    float fall = Mathf.Max(0f, (age - 0.28f) / 0.72f);
                    Vector2 drop = top - new Vector2(-unit * fall * 0.09f, unit * fall * fall * 1.34f);
                    FloatBlob(mesh, drop, unit * 0.065f, unit * (0.09f + fall * 0.12f), unit * 0.023f,
                        new Color(0.36f, 0.77f, 1f, opacity * (1f - fall * 0.8f)));
                    FloatBlob(mesh, drop + Vector2.up * unit * 0.045f, unit * 0.023f, unit * 0.065f, 0f,
                        new Color(0.87f, 0.98f, 1f, opacity * (1f - fall)));
                    break;
                case LyricWeather.Surface.Fire:
                    float pulse = 0.75f + 0.25f * Mathf.Sin(now * 9.4f + seed * 37f);
                    float flame = unit * (0.28f + seed * 0.39f) * pulse;
                    float lean = Mathf.Sin(now * 6f + seed * 29f) * unit * 0.18f;
                    FloatBlob(mesh, top + Vector2.up * flame * 0.25f, unit * 0.18f, flame * 0.8f, lean,
                        new Color(1f, 0.33f, 0.06f, opacity), pointed: true);
                    FloatBlob(mesh, top + Vector2.up * flame * 0.17f, unit * 0.10f, flame * 0.47f, lean * 0.65f,
                        new Color(1f, 0.86f, 0.35f, opacity), pointed: true);
                    float ember = Mathf.Repeat(now * 0.75f + seed, 1f);
                    FloatBlob(mesh, top + new Vector2(lean * ember, unit * (0.33f + ember * 0.88f)),
                        unit * 0.028f, unit * 0.043f, 0f, new Color(1f, 0.69f, 0.19f, opacity * (1f - ember)));
                    break;
            }
        }
    }

    private static void FloatBlob(VertexHelper mesh, Vector2 center, float width, float height, float lean, Color tint, bool pointed = false)
    {
        const int sides = 8;
        int first = mesh.currentVertCount;
        mesh.AddVert(center, tint, Vector2.zero);
        tint.a *= 0.08f;
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            float y = Mathf.Sin(angle);
            float taper = pointed && y > 0f ? 1f - y * 0.7f : 1f;
            Vector2 edge = center + new Vector2(Mathf.Cos(angle) * width * taper + Mathf.Max(0f, y) * lean, y * height);
            mesh.AddVert(edge, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % sides);
        }
    }
}
