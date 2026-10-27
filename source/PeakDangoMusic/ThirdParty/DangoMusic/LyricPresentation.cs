namespace DangoMusic.Core;

public enum LyricDisplayMode
{
    FullLine = 0,
    SingleWord = 1,
    SingleCharacter = 2
}

public enum LyricPositionMode
{
    WorldFixed = 0,
    Smart = 1
}

/// <summary>
/// Whole-line presentations in the map. <see cref="Floating"/> keeps the original MC-style
/// scatter and honours the word/character playback mode; the other styles keep one line together
/// and animate each word, following the Folia visualizers (classic/luminous, still) and their
/// word-phase model.
/// </summary>
public enum LyricVisualStyle
{
    Floating = 0,
    Luminous = 1,
    Karaoke = 2,
    Typewriter = 3,
    Still = 4
}
