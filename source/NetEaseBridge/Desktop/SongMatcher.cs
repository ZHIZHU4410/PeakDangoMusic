using System.Text;
using System.Text.RegularExpressions;
using DangoMusic.Core;

namespace DangoMusic.Desktop;

/// <summary>Rejects uncertain metadata matches rather than showing another recording's lyrics.</summary>
public static class SongMatcher
{
    private static readonly Regex Parentheses = new(@"\(([^()]*)\)|\[([^\[\]]*)\]", RegexOptions.CultureInvariant);
    private static readonly Regex Version = new(@"\b(remix|mix|live|acoustic|instrumental|karaoke|cover|edit|remaster(?:ed)?|sped|nightcore|slowed|demo|version|ver\.?|radio|part|vol(?:ume)?|chapter)\b|伴奏|现场|翻唱|纯音乐|混音|加速|降速|重制", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ArtistSeparator = new(@"\s*(?:[/&,;、，；]|\bfeat\.?|\bft\.?)\s*|\s+[x×]\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static MusicTrack? FindBest(string title, string artist, long durationMs, IReadOnlyList<MusicTrack> candidates)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist)) return null;
        var titles = TitleForms(title);
        var artists = Artists(artist);
        if (titles.Count == 0 || artists.Count == 0) return null;
        var matches = new List<(MusicTrack Track, int Score)>();
        foreach (var candidate in candidates.Where(c => c.Id > 0 && c.LocalPath is null).DistinctBy(c => c.Id))
        {
            var candidateTitles = TitleForms(candidate.Title);
            int titleScore = titles.Keys.Intersect(candidateTitles.Keys).Select(t => Math.Min(titles[t], candidateTitles[t])).DefaultIfEmpty(0).Max();
            if (titleScore == 0) continue;
            var candidateArtists = Artists(candidate.Artist);
            if (candidateArtists.Count == 0 || !artists.Overlaps(candidateArtists)) continue;
            bool sameArtists = artists.SetEquals(candidateArtists);
            if (!artists.IsSubsetOf(candidateArtists) && !candidateArtists.IsSubsetOf(artists)) continue;
            // Partial artist lists need a strong title and corroborating duration.
            if (!sameArtists && (titleScore < 90 || durationMs <= 0 || candidate.DurationMs <= 0)) continue;
            int durationScore = 0;
            if (durationMs > 0 && candidate.DurationMs > 0)
            {
                long difference = Math.Abs(durationMs - candidate.DurationMs);
                if (difference > Math.Max(10000, durationMs / 12)) continue;
                durationScore = difference <= 1500 ? 20 : difference <= 4000 ? 17 : difference <= 7000 ? 10 : 0;
                if (!sameArtists && difference > 4000) continue;
            }
            matches.Add((candidate, titleScore + (sameArtists ? 40 : 25) + durationScore));
        }
        var ordered = matches.OrderByDescending(m => m.Score).ToArray();
        if (ordered.Length == 0 || ordered.Length > 1 && ordered[0].Score - ordered[1].Score < 8) return null;
        return ordered[0].Track;
    }

    private static Dictionary<string, int> TitleForms(string title)
    {
        title = title.Normalize(NormalizationForm.FormKC);
        var forms = new Dictionary<string, int>(StringComparer.Ordinal);
        Add(title, 100);
        var groups = Parentheses.Matches(title);
        // Version annotations are part of recording identity, never a title alias.
        if (groups.Cast<Match>().Any(m => Version.IsMatch(m.Value) || m.Value.Where(char.IsLetterOrDigit).All(char.IsDigit))) return forms;
        string main = Parentheses.Replace(title, "");
        Add(main, 90);
        foreach (Match group in groups)
        {
            string alias = group.Groups[1].Success ? group.Groups[1].Value : group.Groups[2].Value;
            if (!alias.All(c => char.IsDigit(c) || char.IsWhiteSpace(c))) Add(alias, 80);
        }
        return forms;
        void Add(string text, int score)
        {
            string normalized = Normalize(text);
            if (normalized.Length == 0) return;
            if (!forms.TryGetValue(normalized, out int current) || score > current) forms[normalized] = score;
        }
    }

    private static HashSet<string> Artists(string artist) => ArtistSeparator.Split(artist.Normalize(NormalizationForm.FormKC))
        .Select(Normalize).Where(a => a.Length > 0).ToHashSet(StringComparer.Ordinal);

    private static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormKC)
        .Where(char.IsLetterOrDigit)).ToLowerInvariant();
}
