using System.Runtime.InteropServices;
using System.Text;
using BepInEx;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

// Windows current-user DPAPI. Credentials never enter the normal config/logs/packages.
internal static class AccountVault
{
    [StructLayout(LayoutKind.Sequential)] struct Blob{internal int Length;internal IntPtr Data;}
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern bool CryptUnprotectData(ref Blob input,out IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
    static string FilePath=>Path.Combine(Paths.ConfigPath,"PeakDangoMusic-Accounts.dpapi");
    static byte[] Transform(byte[] bytes,bool protect)
    {
        var input=new Blob{Length=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};
        Blob output=default;IntPtr description=IntPtr.Zero;
        try
        {
            Marshal.Copy(bytes,0,input.Data,bytes.Length);
            bool ok=protect?CryptProtectData(ref input,"PeakDangoMusic",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):
                CryptUnprotectData(ref input,out description,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Windows登录凭证保护失败");
            var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;
        }
        finally{Marshal.FreeHGlobal(input.Data);if(output.Data!=IntPtr.Zero)LocalFree(output.Data);if(description!=IntPtr.Zero)LocalFree(description);}
    }
    internal static JObject Load()
    {
        if(!File.Exists(FilePath))return new JObject();
        var bytes=File.ReadAllBytes(FilePath);if(bytes.Length>262144)throw new InvalidDataException("登录凭证文件过大");
        var plain=Transform(bytes,false);
        try{return JObject.Parse(Encoding.UTF8.GetString(plain));}finally{Array.Clear(plain,0,plain.Length);}
    }
    internal static void Save(JObject credentials)
    {
        var plain=Encoding.UTF8.GetBytes(credentials.ToString(Newtonsoft.Json.Formatting.None));
        try
        {
            var encrypted=Transform(plain,true);string temp=FilePath+".tmp";File.WriteAllBytes(temp,encrypted);
            if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);
        }
        finally{Array.Clear(plain,0,plain.Length);}
    }
}
