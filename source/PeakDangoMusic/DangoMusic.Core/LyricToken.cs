namespace DangoMusic.Core;

public sealed record LyricToken(string Text, int SourceStart, int SourceLength, long StartMs, long EndMs);
