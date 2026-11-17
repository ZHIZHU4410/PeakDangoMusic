using PeakDangoMusic;
using Newtonsoft.Json.Linq;
using System.Text;

Directory.CreateDirectory(BepInEx.Paths.ConfigPath);
var expected=new JObject{["netease"]="SAMPLE_AUTH",["qqId"]="123",["qqKey"]="SAMPLE_QQ"};
AccountVault.Save(expected);
if(!JToken.DeepEquals(expected,AccountVault.Load()))throw new Exception("DPAPI roundtrip");
byte[] bytes=File.ReadAllBytes(Path.Combine(BepInEx.Paths.ConfigPath,"PeakDangoMusic-Accounts.dpapi"));
if(Encoding.UTF8.GetString(bytes).Contains("SAMPLE_AUTH"))throw new Exception("Plaintext credential persisted");
AccountVault.Save(new JObject{["netease"]="",["qqId"]="123",["qqKey"]="SAMPLE_QQ"});
var loggedOut=AccountVault.Load();
if((string?)loggedOut["netease"]!=""||(string?)loggedOut["qqKey"]!="SAMPLE_QQ")throw new Exception("Logout must preserve other provider");
Console.WriteLine("Account vault checks passed: 3");

namespace BepInEx{internal static class Paths{internal static string ConfigPath=>Path.Combine(AppContext.BaseDirectory,"fake-account-vault");}}
