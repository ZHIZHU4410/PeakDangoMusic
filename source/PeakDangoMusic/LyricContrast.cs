namespace PeakDangoMusic;

// Pure contrast policy, also exercised by the regression runner.
internal static class LyricContrast
{
    internal static bool IsDay(float hour,float start,float end)
    {
        hour=(hour%24+24)%24;start=(start%24+24)%24;end=(end%24+24)%24;
        return start<=end?hour>=start&&hour<end:hour>=start||hour<end;
    }
    internal static bool Resolve(bool automatic,bool manualDark,bool daylight)=>automatic?daylight:manualDark;
    internal static float Linear(float channel)=>channel<=.04045f?channel/12.92f:(float)Math.Pow((channel+.055f)/1.055f,2.4);
    internal static float Luminance(float r,float g,float b)=>.2126f*Linear(r)+.7152f*Linear(g)+.0722f*Linear(b);
    internal static bool UseDark(float luminance,bool wasDark)
    {
        // Hysteresis around the equal-contrast crossover prevents camera jitter flicker.
        return wasDark?luminance>.165f:luminance>.205f;
    }
}
