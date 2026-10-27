namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
namespace PeakDangoMusic
{
    internal readonly struct UnicodeScalar
    {
        public int Value { get; }
        UnicodeScalar(int value){Value=value;}
        public static bool TryGetRuneAt(string text,int index,out UnicodeScalar scalar)
        {
            scalar=default;if(index<0 || index>=text.Length)return false;char c=text[index];
            if(char.IsHighSurrogate(c) && index+1<text.Length && char.IsLowSurrogate(text[index+1])){scalar=new UnicodeScalar(char.ConvertToUtf32(c,text[index+1]));return true;}
            if(char.IsSurrogate(c))return false;scalar=new UnicodeScalar(c);return true;
        }
    }
}
