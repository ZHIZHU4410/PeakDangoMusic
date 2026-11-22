using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace PeakDangoMusic;

internal sealed class WorldCommentBody : Text
{
    private const int MaxVisibleChars = 108;
    private const int MaxImages = 8;
    private const string Marker = "<color=#010203>■</color>";
    private static readonly Color32 MarkerColor = new Color32(1, 2, 3, 255);
    private static readonly Regex QqEmojiCode = new Regex(@"\[em\](e4[0-9]{5})\[/em\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly List<string> urls = new();
    private readonly List<Rect> positions = new();
    private readonly List<RawImage> images = new();
    private float refreshAt;

    internal void SetComment(string comment, bool qq)
    {
        urls.Clear();
        positions.Clear();
        foreach (RawImage image in images) image.enabled = false;
        refreshAt = 0f;
        supportRichText = true;
        // Force a new layout even when two comments use the same sequence of marker glyphs.
        text = Format(comment, qq, urls);
        SetVerticesDirty();
        foreach (string url in urls) CoverCache.Preload(url);
    }

    internal static string Format(string comment, bool qq, List<string> urls)
    {
        var output = new StringBuilder();
        int left = MaxVisibleChars;
        int start = 0;
        if (qq)
        {
            foreach (Match match in QqEmojiCode.Matches(comment))
            {
                if (!AppendText(comment, start, match.Index - start, output, ref left))
                    return output.Append('…').ToString();
                if (urls.Count < MaxImages)
                {
                    if (left == 0) return output.Append('…').ToString();
                    string code = match.Groups[1].Value.ToLowerInvariant();
                    urls.Add("https://qzonestyle.gtimg.cn/qzone/em/" + code + "@2x.gif");
                    output.Append(Marker);
                    left--;
                }
                start = match.Index + match.Length;
            }
        }
        if (!AppendText(comment, start, comment.Length - start, output, ref left))
            output.Append('…');
        return output.ToString();
    }

    private static bool AppendText(string comment, int start, int count, StringBuilder output, ref int left)
    {
        int take = Mathf.Min(count, left);
        if (take < count && take > 0 && char.IsHighSurrogate(comment[start + take - 1])) take--;
        for (int i = start; i < start + take; i++)
        {
            char ch = comment[i];
            output.Append(ch);
            // Keep angle brackets visible without letting comment text create rich-text tags.
            if (ch == '<') output.Append('\u200b');
        }
        left -= take;
        return take == count;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        base.OnPopulateMesh(mesh);
        positions.Clear();
        UIVertex vertex = default;
        for (int i = 0; i + 3 < mesh.currentVertCount && positions.Count < urls.Count; i += 4)
        {
            mesh.PopulateUIVertex(ref vertex, i);
            if (!vertex.color.Equals(MarkerColor)) continue;
            Vector2 min = vertex.position;
            Vector2 max = min;
            for (int j = i; j < i + 4; j++)
            {
                mesh.PopulateUIVertex(ref vertex, j);
                var point = (Vector2)vertex.position;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
                vertex.color.a = 0;
                mesh.SetUIVertex(vertex, j);
            }
            float size = Mathf.Max(max.x - min.x, max.y - min.y);
            var center = (min + max) * 0.5f;
            positions.Add(new Rect(center - Vector2.one * (size * 0.5f), Vector2.one * size));
        }
    }

    internal void RefreshEmoji()
    {
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.15f;
        for (int i = 0; i < positions.Count; i++)
        {
            if (images.Count <= i)
            {
                var node = new GameObject("QQ Emoji", typeof(RectTransform), typeof(RawImage));
                var rect = (RectTransform)node.transform;
                rect.SetParent(rectTransform, false);
                rect.anchorMin = rect.anchorMax = rectTransform.pivot;
                var image = node.GetComponent<RawImage>();
                image.raycastTarget = false;
                images.Add(image);
            }
            RawImage target = images[i];
            Rect position = positions[i];
            target.rectTransform.anchoredPosition = position.center;
            target.rectTransform.sizeDelta = position.size;
            target.texture = CoverCache.For(urls[i], 48);
            target.enabled = target.texture != null && position.width > 0f;
        }
        for (int i = positions.Count; i < images.Count; i++) images[i].enabled = false;
    }
}
