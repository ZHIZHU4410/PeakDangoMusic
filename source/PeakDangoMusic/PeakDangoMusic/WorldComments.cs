using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PeakDangoMusic;

internal sealed class WorldComments : IDisposable
{
    private const float Interval = 60f;
    private static readonly float[] PlacementAngles = { 13f, -13f, 0f, 29f, -29f };
    private readonly Plugin plugin;
    private GameObject? card;
    private Text? heading;
    private WorldCommentBody? body;
    private Text? author;
    private Font? font;
    private float nextAt = -1f;
    private float expiresAt;
    private int scene;
    private string cachedSong = "";
    private float retryAt;
    private int commentIndex;
    private (string Nick, string Body, int Likes)[] comments = Array.Empty<(string, string, int)>();
    private string requestSong = "";
    private Task<JObject>? request;

    internal WorldComments(Plugin plugin) => this.plugin = plugin;

    internal void Tick(Pose view, int sceneHandle, GdMusicApi.Track? track)
    {
        float now = Time.unscaledTime;
        if (scene != sceneHandle)
        {
            Reset();
            scene = sceneHandle;
        }
        if (track != null && nextAt < 0f) nextAt = now + Interval;
        if (card != null && card.activeSelf)
        {
            if (now >= expiresAt)
                card.SetActive(false);
            else
            {
                Face(view.position);
                body?.RefreshEmoji();
            }
        }

        if (request != null)
        {
            if (!request.IsCompleted) return;
            cachedSong = requestSong;
            commentIndex = 0;
            comments = Array.Empty<(string, string, int)>();
            try { comments = Popular(request.GetAwaiter().GetResult()); }
            catch (Exception) { /* A later interval may retry the platform. */ }
            request = null;
            retryAt = now + Interval;
        }

        if (track == null || now < nextAt - 8f || plugin.Aggregate.PlaybackPending) return;
        string key = PlayerLibrary.Key(track);
        if (cachedSong != key || (comments.Length == 0 && now >= retryAt))
        {
            requestSong = key;
            request = plugin.Hub.CommentsFor(track);
            return;
        }

        if (now < nextAt) return;
        nextAt = now + Interval;
        if (comments.Length == 0 || !TryPlace(view, out Vector3 anchor)) return;
        var comment = comments[commentIndex++ % comments.Length];
        EnsureCard();
        heading!.text = "热门评论 · " + Shorten(track.Title, 25);
        body!.SetComment(comment.Body, track.Source is "qq" or "tencent");
        author!.text = Shorten(comment.Nick, 20) + (comment.Likes > 0 ? $"    {comment.Likes} 赞" : "");
        card!.transform.position = anchor;
        Face(view.position);
        card.SetActive(true);
        expiresAt = now + Interval;
    }

    private static (string Nick, string Body, int Likes)[] Popular(JObject data)
    {
        var found = new List<(string Nick, string Body, int Likes)>();
        foreach (string collection in new[] { "hotComments", "comments" })
        {
            if (data[collection] is not JArray rows) continue;
            foreach (JObject row in rows.OfType<JObject>())
            {
                string text = ((string?)row["content"] ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
                if (text.Length == 0) continue;
                string nick = (string?)row["user"]?["nickname"] ?? "音乐用户";
                int.TryParse(row["likedCount"]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int likes);
                found.Add((nick, text, likes));
            }
        }
        return found.OrderByDescending(comment => comment.Likes)
            .GroupBy(comment => (comment.Nick, comment.Body)).Select(group => group.First())
            .Take(12).ToArray();
    }

    private static string Shorten(string value, int limit)
    {
        if (value.Length <= limit) return value;
        int count = char.IsHighSurrogate(value[limit - 1]) ? limit - 1 : limit;
        return value.Substring(0, count) + "…";
    }

    private static bool TryPlace(Pose view, out Vector3 anchor)
    {
        Vector3 ahead = view.rotation * Vector3.forward;
        foreach (float angle in PlacementAngles)
        {
            Vector3 direction = (Quaternion.AngleAxis(angle, Vector3.up) * ahead + Vector3.up * 0.04f).normalized;
            float distance = 5f;
            if (Physics.SphereCast(view.position, 0.46f, direction, out RaycastHit hit, distance,
                    PlayerPerspective.TerrainMask, QueryTriggerInteraction.Ignore))
                distance = hit.distance - 0.18f;
            if (distance < 2.6f) continue;
            anchor = view.position + direction * distance;
            if (!Physics.CheckSphere(anchor, 0.46f, PlayerPerspective.TerrainMask, QueryTriggerInteraction.Ignore))
                return true;
        }
        anchor = default;
        return false;
    }

    private void Face(Vector3 cameraPosition)
    {
        Vector3 direction = card!.transform.position - cameraPosition;
        if (direction.sqrMagnitude > 0.001f)
            card.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void EnsureCard()
    {
        if (card != null) return;
        card = new GameObject("DangoMusic World Comment", typeof(RectTransform), typeof(Canvas));
        card.SetActive(false);
        var canvas = card.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)card.transform;
        rect.sizeDelta = new Vector2(560f, 198f);
        rect.localScale = Vector3.one * 0.0034f;

        var panel = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(rect, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        var image = panel.GetComponent<Image>();
        image.color = new Color(0.065f, 0.095f, 0.14f, 0.9f);
        image.raycastTarget = false;

        heading = AddText("Track", 21, 31, 12, new Color(0.66f, 0.87f, 1f));
        body = (WorldCommentBody)AddText("Comment", 26, 110, 46, Color.white, inlineImages: true);
        author = AddText("Author", 18, 27, 162, new Color(0.79f, 0.85f, 0.93f));
    }

    private Text AddText(string name, int size, int height, int top, Color color, bool inlineImages = false)
    {
        var element = new GameObject(name, typeof(RectTransform), inlineImages ? typeof(WorldCommentBody) : typeof(Text));
        var rect = (RectTransform)element.transform;
        rect.SetParent(card!.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -top);
        rect.sizeDelta = new Vector2(520f, height);
        var text = element.GetComponent<Text>();
        text.font = font ??= Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = color;
        text.supportRichText = false;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = size;
        text.resizeTextMinSize = 16;
        text.raycastTarget = false;
        return text;
    }

    internal void Reset()
    {
        nextAt = -1f;
        if (card != null) Object.Destroy(card);
        card = null;
    }

    public void Dispose()
    {
        Reset();
        if (font != null) Object.Destroy(font);
        if (request != null)
            _ = request.ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }
}
