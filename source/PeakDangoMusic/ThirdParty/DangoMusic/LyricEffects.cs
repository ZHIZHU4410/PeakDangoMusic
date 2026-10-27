namespace DangoMusic.Core;

public enum EntranceEffect { Fade, SlideUp, Pop, None }
public enum ExitEffect { Fade, FloatUp, Shrink, None }
public readonly record struct LyricLineFrame(double Alpha, double XOffset, double YOffset, double Scale);
