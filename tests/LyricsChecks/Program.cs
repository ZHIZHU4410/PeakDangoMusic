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
