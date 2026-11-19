import com.muoniumplayer.core.ncm.lxsource.*;
public class LxChecks {
 public static void main(String[] args)throws Exception{
  String script="/* @name PeakOfflineProbe */\n"+
   "console.log('java='+typeof java+' packages='+typeof Packages);"+
   "console.log('md5='+lx.utils.crypto.md5('abc'));"+
   "lx.on('request', function(d){return Promise.resolve('https://example.invalid/check.mp3');});"+
   "lx.send('inited',{sources:{wy:{type:'music',name:'probe',actions:['musicUrl'],qualitys:['128k']}}});";
  LxRuntime r=LxRuntime.start("test",LxScriptInfo.parse(script),script,false);
  try{
   if(!r.awaitReady(31000))throw new AssertionError(r.getError());
   if(!r.isTranspiled())throw new AssertionError("compiler not loaded");
   String log=String.join("\n",r.getLogs());
   if(!log.contains("java=undefined packages=undefined")||!log.contains("900150983cd24fb0d6963f7d28e17f72"))throw new AssertionError("sandbox / host bridge failed");
   String result=r.request("wy","musicUrl","{\"type\":\"128k\",\"musicInfo\":{\"songmid\":\"1\"}}");
   if(!result.contains("https://example.invalid/check.mp3"))throw new AssertionError("protocol failed");
   System.out.println("LX real-runtime checks: 4 passed");
  }finally{r.shutdown();}
 }
}
