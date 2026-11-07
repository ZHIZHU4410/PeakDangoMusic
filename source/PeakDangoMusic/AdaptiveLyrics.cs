using UnityEngine;
namespace PeakDangoMusic;

// One global day/night decision. Camera position, bloom and weather never change the face.
internal sealed class AdaptiveLyrics : IDisposable
{
    internal static AdaptiveLyrics? Current;
    internal static readonly Color Light=new(1,.96f,.91f,1),Dark=new(.055f,.07f,.095f,1);
    internal bool Daylight {get;private set;}
    internal void Tick()
    {
        var day=DayNightManager.instance;
        if(day!=null)Daylight=LyricContrast.IsDay(day.timeOfDay,day.dayStart,day.dayEnd);
    }
    internal sealed class Palette
    {
        internal bool IsDark {get;private set;}
        internal Color Face=>IsDark?Dark:Light;
        internal Color Outline=>IsDark?new Color(1,1,1,.8f):new Color(.01f,.015f,.025f,.95f);
        internal void Update(Vector2 unused)
        {
            var s=Plugin.Instance?.Settings;
            IsDark=s!=null&&LyricContrast.Resolve(s.AutoDayNight.Value,s.ManualDark.Value,Current?.Daylight??false);
        }
    }
    public void Dispose(){if(Current==this)Current=null;}
}
