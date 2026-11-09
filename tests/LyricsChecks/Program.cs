using DangoMusic.Core;

int checks=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
double Alpha(LyricAnimation animation,int index,long time)=>animation.Sample(index,time,0,wiggle:false).Alpha;

// Untimed Chinese must accumulate one character at a time, including after seeking.
var line=new LyricsLine(1000,4000,"你好世界");
var characters=new LyricAnimation(line,5000,singleCharacter:true);
for(int count=1;count<=4;count++)
{
    long at=1000+(count-1)*1000+100;
    for(int i=0;i<4;i++)Check((Alpha(characters,i,at)>0)==(i<count),"Untimed character order");
}
Check(Alpha(characters,1,1100)==0,"Seeking back must hide later characters");
var original=new LyricAnimation(line,5000);
Check(Alpha(original,0,1100)==Alpha(original,1,1100),"Preserve 1.2 full-line word-pop fallback");
var word=new LyricAnimation(line,5000,wholeToken:true);
Check(Alpha(word,0,1100)==Alpha(word,3,1100),"Whole words remain atomic");

// Real word intervals are split per character; existing exact timestamps win.
var timed=line with {DurationMs=2000,Words=new[]{new LyricsWord(1000,1000,"你好"),new LyricsWord(2000,1000,"世界")}};
var timing=new LyricAnimation(timed,3000,singleCharacter:true);
Check(Alpha(timing,0,1100)>0 && Alpha(timing,1,1100)==0,"Timed two-character word split");
Check(Alpha(timing,1,1600)>0 && Alpha(timing,2,1600)==0,"Preserve next word boundary");
var cotimed=new LyricAnimation(new LyricsLine(1000,1000,"你好",new[]{new LyricsWord(1000,1000,"你"),new LyricsWord(1000,1000,"好")}),2000,singleCharacter:true);
Check(Alpha(cotimed,0,1100)>0 && Alpha(cotimed,1,1100)==0,"Co-timed fragments must not reveal together");
var mismatch=new LyricAnimation(line with {Words=new[]{new LyricsWord(1000,4000,"别的歌词")}},5000,singleCharacter:true);
Check(Alpha(mismatch,0,1100)>0 && Alpha(mismatch,1,1100)==0,"Mismatched timing uses character fallback");
var punctuation=new LyricAnimation(new LyricsLine(1000,2000,"你，好"),3000,singleCharacter:true);
Check(Alpha(punctuation,0,1100)>0 && Alpha(punctuation,1,1100)>0 && Alpha(punctuation,2,1100)==0,"Punctuation must not consume a singing slot");
var unicode=new LyricAnimation(new LyricsLine(1000,3000,"e\u0301𠀀你"),4000,singleCharacter:true);
Check(unicode.Graphemes.Count==3,"Do not split combining characters or surrogate pairs");
Check(Alpha(unicode,1,1100)==0 && Alpha(unicode,1,2100)>0 && Alpha(unicode,2,2100)==0,"Unicode character order");

// Toggle, frequency and amplitude cooperate rather than enabling separate effects.
var off=characters.Sample(0,1100,.1,wiggle:false);
Check(off==characters.Sample(0,1100,.1,wiggle:true,wiggleAmount:0),"Zero amplitude disables motion");
Check(off==characters.Sample(0,1100,.1,wiggle:false,wiggleFrequencyHz:24,wiggleAmount:2),"Off ignores frequency and amplitude");
var low=characters.Sample(0,1100,.1,wiggleFrequencyHz:4);
var high=characters.Sample(0,1100,.1,wiggleFrequencyHz:24);
Check(low!=high && low.Alpha==high.Alpha,"Frequency changes motion, not lyric timing");
var tokens=new LyricTokens(new LyricsLine(1000,4000,"hello"),5000);
var small=tokens.Sample(0,1100,.1,wiggleAmount:1);
var large=tokens.Sample(0,1100,.1,wiggleAmount:2);
Check(Math.Abs(large.XOffset-2*small.XOffset)<1e-9 && Math.Abs(large.Rotation-2*small.Rotation)<1e-9,"Amplitude 2 must not be clamped to 1.2");
Console.WriteLine($"Passed {checks} lyric regression checks.");

Check(PeakDangoMusic.LyricContrast.UseDark(1,false),"Snow / bright sky requires dark lyrics");
Check(!PeakDangoMusic.LyricContrast.UseDark(0,true),"Night / deep shadow requires light lyrics");
Check(!PeakDangoMusic.LyricContrast.UseDark(.18f,false),"Hysteresis retains light near crossover");
Check(PeakDangoMusic.LyricContrast.UseDark(.18f,true),"Hysteresis retains dark near crossover");
Check(Math.Abs(PeakDangoMusic.LyricContrast.Luminance(1,1,1)-1)<.0001,"White luminance");
Check(PeakDangoMusic.LyricContrast.Luminance(0,0,0)==0,"Black luminance");
for(int level=0;level<=100;level++)
{
    float l=level/100f;
    foreach(bool previous in new[]{true,false})
    {
        bool dark=PeakDangoMusic.LyricContrast.UseDark(l,previous);
        float face=dark?PeakDangoMusic.LyricContrast.Luminance(.055f,.07f,.095f):PeakDangoMusic.LyricContrast.Luminance(1,.96f,.91f);
        double ratio=(Math.Max(l,face)+.05)/(Math.Min(l,face)+.05);
        Check(ratio>=3.8,"Contrast across grayscale with hysteresis");
    }
}
Console.WriteLine($"Total including contrast checks: {checks}");

Check(PeakDangoMusic.LyricContrast.IsDay(12,5,21),"Daytime");
Check(!PeakDangoMusic.LyricContrast.IsDay(0,5,21),"Nighttime");
Check(PeakDangoMusic.LyricContrast.IsDay(5,5,21),"Inclusive dawn");
Check(!PeakDangoMusic.LyricContrast.IsDay(21,5,21),"Exclusive dusk");
Check(PeakDangoMusic.LyricContrast.IsDay(36,5,21),"Modulo day time");
Check(PeakDangoMusic.LyricContrast.IsDay(23,21,5),"Midnight crossing");
foreach(bool day in new[]{true,false})foreach(bool manual in new[]{true,false})
{
 Check(PeakDangoMusic.LyricContrast.Resolve(true,manual,day)==day,"Automatic ignores manual");
 Check(PeakDangoMusic.LyricContrast.Resolve(false,manual,day)==manual,"Manual ignores clock");
}
Console.WriteLine($"Total including day/night modes: {checks}");

foreach(string id in new[]{"kugou","KuGou.exe","KuGouMusic.Desktop"})Check(DangoMusic.Desktop.PlayerIdentity.Matches("kugou",id),"Kugou session identity");
foreach(string id in new[]{"QQMusic.exe","QQMusic","Tencent.QQMusic.Desktop"})Check(DangoMusic.Desktop.PlayerIdentity.Matches("qqmusic",id),"QQMusic session identity");
foreach(string source in new[]{"netease","kugou","qqmusic"})foreach(string id in new[]{"QQ.exe","chrome.exe","cn.toside.music.desktop"})Check(!DangoMusic.Desktop.PlayerIdentity.Matches(source,id),"Unrelated app isolation");
Check(!DangoMusic.Desktop.PlayerIdentity.Matches("qqmusic","kugou"),"QQ cannot consume Kugou");
Check(!DangoMusic.Desktop.PlayerIdentity.Matches("kugou","QQMusic.exe"),"Kugou cannot consume QQ");
Console.WriteLine($"Total including player isolation: {checks}");

Check(DangoMusic.Desktop.SongMatcher.FindBest("太苦 (我住长江头)","张智焓",0,new[]{new DangoMusic.Core.MusicTrack(1,"太苦","张智焓")})!=null,"Kugou caption alias");
Check(DangoMusic.Desktop.SongMatcher.FindBest("太苦 (Live)","张智焓",0,new[]{new DangoMusic.Core.MusicTrack(1,"太苦","张智焓")})==null,"Reject different recording version");
Check(DangoMusic.Desktop.SongMatcher.FindBest("太苦","不同歌手",0,new[]{new DangoMusic.Core.MusicTrack(1,"太苦","张智焓")})==null,"Reject different singer");
Console.WriteLine($"Final regression assertions: {checks}");

var krc=DangoMusic.Desktop.KugouLyricsCache.ParseText("[offset:50]\n[1000,2000]<0,500,0>你<500,1500,0>好\n");
Check(krc.Lines.Count==1,"KRC row parse");
Check(krc.Lines[0].Text=="你好","KRC joins timed words");
Check(krc.Lines[0].Words![1].StartMs==1550&&krc.Lines[0].Words![1].DurationMs==1500,"KRC relative word timing and offset");
bool rejected=false;try{DangoMusic.Desktop.KugouLyricsCache.Parse(new byte[]{1,2,3,4,5});}catch(System.IO.InvalidDataException){rejected=true;}
Check(rejected,"Reject invalid KRC magic");
Console.WriteLine($"Including native cache checks: {checks}");

string cacheTest=Path.Combine(AppContext.BaseDirectory,"cache-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(cacheTest);
try
{
    var playback=new DangoMusic.Desktop.DesktopPlayback("测试歌 (深情版)","测试歌手",0,0,10000,true,0,"test");
    File.WriteAllText(Path.Combine(cacheTest,"测试歌手 - 测试歌 (深情版).lrc"),"[00:01.00]你好");
    Check(DangoMusic.Desktop.KugouLyricsCache.Read(playback,new[]{cacheTest}).Lines.Count==1,"Use matching native cache");
    File.WriteAllText(Path.Combine(cacheTest,"测试歌手 - 测试歌 (深情版)-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-123-00000000.lrc"),"[00:02.00]不同时间");
    Check(DangoMusic.Desktop.KugouLyricsCache.Read(playback,new[]{cacheTest}).Lines.Count==0,"Reject conflicting cached revisions");
    Check(DangoMusic.Desktop.KugouLyricsCache.Read(playback with{Title="测试歌 (烟嗓版)"},new[]{cacheTest}).Lines.Count==0,"Reject different cached performance");
}
finally{if(Path.GetFullPath(cacheTest).StartsWith(Path.GetFullPath(AppContext.BaseDirectory),StringComparison.OrdinalIgnoreCase))Directory.Delete(cacheTest,true);}
Console.WriteLine($"All cache regression assertions: {checks}");

var stable=new DangoMusic.Desktop.PlaybackBuffer();
var observed=new DangoMusic.Desktop.DesktopPlayback("同一首歌","甲、乙",0,1000,10000,true,1000,"test");
stable.Observe(observed,1000);
stable.Observe(null,1500);
Check(stable.Read(1500)?.PositionMs==1000,"Short missing observation retains original timestamp");
stable.Observe(observed with{Artist="甲",PositionMs=null,SampleTick=1700},1700);
Check(stable.Read(1700)?.Artist=="甲、乙","Partial singer metadata does not reset identity");
Check(stable.Read(3101)?.PositionMs!=null,"Preserve 2.0.5 progress hold");
stable.Observe(observed with{Title="下一首",PositionMs=null,SampleTick=3200},3200);
Check(stable.Read(3200)?.Title=="下一首"&&stable.Read(3200)?.PositionMs==0,"New track uses 2.0.5 estimated start rather than old position");
Check(stable.Read(33201)==null,"Disconnected metadata expires after 30-second hold");
stable.Observe(observed with{Playing=false,SampleTick=6000},6000);
Check(stable.Read(6500)?.PositionAt(6500)==1000,"Pause remains frozen");
var blocker=new TaskCompletionSource<DangoMusic.Desktop.DesktopPlayback?>();
using(var isolated=new DangoMusic.Desktop.PlaybackSampler(_=>blocker.Task,CancellationToken.None))
{
 var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<100;i++)_ = isolated.Snapshot;
 Check(watch.ElapsedMilliseconds<500,"Hung provider never blocks sender snapshot");
 blocker.TrySetResult(null);
}
Console.WriteLine($"Stability regression assertions: {checks}");

using(var qqSearch=System.Text.Json.JsonDocument.Parse("{\"code\":0,\"data\":{\"song\":{\"list\":[{\"mid\":\"goodMid123\",\"title\":\"测试歌\",\"singer\":[{\"name\":\"甲\"}],\"interval\":205},{\"mid\":\"../bad\",\"title\":\"bad\"}]}}}"))
{
 var tracks=DangoMusic.Desktop.QqPublicLyricsClient.ParseSearch(qqSearch.RootElement);
 Check(tracks.Count==1&&tracks[0].Metadata.DurationMs==205000,"QQ metadata parsing and invalid mid rejection");
}
using(var qqLine=System.Text.Json.JsonDocument.Parse("{\"retcode\":0,\"lyric\":\"[00:01.00]原词&amp;\",\"trans\":\"[00:01.00]翻译\"}"))
{
 var t=DangoMusic.Desktop.QqPublicLyricsClient.ParseLyrics(qqLine.RootElement);
 Check(t.Lines.Count==1&&t.Lines[0].Text=="原词&"&&t.Lines[0].Translation=="翻译","QQ plain LRC/translation HTML decoding");
}
using(var encoded=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new{retcode=0,lyric=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("[00:02.00]编码歌词"))})))
 Check(DangoMusic.Desktop.QqPublicLyricsClient.ParseLyrics(encoded.RootElement).Lines.Count==1,"QQ base64 lyric response");
Console.WriteLine($"Restored-player assertions: {checks}");
await KugouApiChecks.Run(Check);
Console.WriteLine($"API regression assertions: {checks}");
var longTail=DangoMusic.Desktop.KugouLyricsCache.ParseText("[1000,260000]<0,400,0>测<400,600,0>试");
var bounded=DangoMusic.Desktop.KugouLyricsCache.FitToDuration(longTail,10000);
Check(bounded.Lines.Count==1&&bounded.Lines[0].DurationMs==9000,"Repair excessive final KRC hold using player duration");
Check(ReferenceEquals(bounded.Lines[0].Words,longTail.Lines[0].Words),"Preserve original word timing when repairing final hold");
Check(DangoMusic.Desktop.KugouLyricsCache.FitToDuration(DangoMusic.Desktop.KugouLyricsCache.ParseText("[1000,260000]<0,20000,0>测"),10000).Lines.Count==0,"Reject real words beyond playing track");
Check(DangoMusic.Desktop.KugouLyricsCache.FitToDuration(DangoMusic.Desktop.KugouLyricsCache.ParseText("[1000,260000]测试"),10000).Lines.Count==0,"Do not infer validity for an untimed excessive row");
Check(DangoMusic.Desktop.KugouLyricsCache.FitToDuration(DangoMusic.Desktop.KugouLyricsCache.ParseText("[1000,260000]<0,1000,0>测\n[3000,1000]<0,1000,0>试"),10000).Lines.Count==0,"Do not repair corrupt nonfinal rows");
Check(DangoMusic.Desktop.KugouLyricsCache.FitToDuration(krc,10000).Lines[0]==krc.Lines[0],"Ordinary KRC rows are unchanged");
Console.WriteLine($"KRC duration regression assertions: {checks}");
