using System.Collections.Generic;

namespace DangoMusic.Core;

public sealed record LyricsLine(long StartMs, long DurationMs, string Text, IReadOnlyList<LyricsWord>? Words = null, string Translation = "");
