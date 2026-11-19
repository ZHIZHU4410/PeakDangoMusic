package com.muoniumplayer.core.ncm.lxsource;
import java.io.File;
/** Dependencies are installed and hash-checked by build.py, never downloaded in game. */
public final class ClosureCompilerDownload {
 private static ClosureEs5Backend instance;
 public static synchronized ClosureEs5Backend backend(){
  if(instance==null)try{
   File folder=new File(ClosureCompilerDownload.class.getProtectionDomain().getCodeSource().getLocation().toURI()).getParentFile();
   instance=ClosureEs5Backend.create(new File(folder,"closure-compiler-v20240317.jar"));
  }catch(Exception ignored){}
  return instance;
 }
 public static String pendingMessage(){return "缺少 LX 脚本转译组件，请完整安装";}
}
