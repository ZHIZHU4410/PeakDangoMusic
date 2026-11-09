using System.Text;
using System.Text.RegularExpressions;
namespace DangoMusic.Desktop;

// Bridge short provider gaps without inventing a new observation timestamp.
// Metadata is held briefly so a single failed SMTC/UI Automation read cannot drop
// the connection; the last real position is extrapolated by DesktopPlayback.
public sealed class PlaybackBuffer
{
    DesktopPlayback? current;
    long metadataAt,progressAt;
    const int MetadataGraceMs=30000;
    static string Normalize(string value)=>string.Concat(value.Normalize(NormalizationForm.FormKC).Where(char.IsLetterOrDigit)).ToLowerInvariant();
    static HashSet<string> Artists(string value)=>Regex.Split(value,@"[/&,;、，；]").Select(Normalize).Where(s=>s.Length>0).ToHashSet();
    static bool Same(DesktopPlayback a,DesktopPlayback b)
    {
        if(Normalize(a.Title)!=Normalize(b.Title))return false;
        var x=Artists(a.Artist);var y=Artists(b.Artist);
        // Kugou sometimes reports an empty artist while its desktop window reports one.
        return x.Count==0||y.Count==0||x.IsSubsetOf(y)||y.IsSubsetOf(x);
    }
    public void Observe(DesktopPlayback? sample,long now)
    {
        if(sample==null)return;
        metadataAt=sample.SampleTick;
        if(current==null||!Same(current,sample))
        {
            // A new track with a playing state but no clock yet starts at zero and is
            // corrected by the next real sample. Hiding the whole song is worse.
            current=sample.PositionMs.HasValue||!sample.Playing||sample.DurationMs<=0?sample:
                sample with{PositionMs=0,SampleTick=now,Source=sample.Source+"+estimated"};
            progressAt=current.PositionMs.HasValue?current.SampleTick:long.MinValue;
            return;
        }
        if(sample.PositionMs.HasValue)
        {
            current=sample with{Title=current.Title,Artist=current.Artist.Length>0?current.Artist:sample.Artist};
            progressAt=sample.SampleTick;
        }
        else
        {
            // Freeze/resume at the extrapolated clock, not at the last raw sample.
            long? currentPosition=current.PositionAt(now);
            current=current with
            {
                PositionMs=currentPosition??current.PositionMs,
                SampleTick=now,
                Playing=sample.Playing,
                Artist=current.Artist.Length>0?current.Artist:sample.Artist
            };
        }
        // A null position while playing keeps the last clock; PositionAt extrapolates it.
    }
    public DesktopPlayback? Read(long now)
    {
        if(current==null||now<metadataAt||now-metadataAt>MetadataGraceMs)return null;
        if(progressAt==long.MinValue)return current;
        // Do not null PositionMs on a stale sample: extrapolation is the intended bridge.
        return current;
    }
}
