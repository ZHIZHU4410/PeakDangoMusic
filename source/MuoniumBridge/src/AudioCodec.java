import java.nio.file.*;
import java.security.MessageDigest;
import java.util.*;
import java.util.concurrent.TimeUnit;

/** Transcodes ordinary authorized audio formats using a user-installed FFmpeg. No DRM handling. */
final class AudioCodec {
 static volatile Process active;
 static {Runtime.getRuntime().addShutdownHook(new Thread(()->{Process p=active;if(p!=null&&p.isAlive())p.destroyForcibly();}));}
 static String mp3(String url,String referer)throws Exception {
  String specified=System.getProperty("peak.ffmpeg","");String local=System.getenv("LOCALAPPDATA");
  Path ffmpeg=specified.isEmpty()?Path.of(local==null?System.getProperty("user.home")+"/AppData/Local":local,"universal-modder","ffmpeg","bin","ffmpeg.exe"):Path.of(specified);
  if(!Files.isRegularFile(ffmpeg))throw new IllegalStateException("FFmpeg required");
  String cache=System.getProperty("peak.audioCache","");if(cache.isEmpty())throw new IllegalStateException("host audio cache required");
  Path root=Path.of(cache).toAbsolutePath().normalize();Files.createDirectories(root);
  String key=HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(url.getBytes(java.nio.charset.StandardCharsets.UTF_8)));
  Path file=root.resolve(key+".mp3"),part=root.resolve(key+".part.mp3");if(Files.isRegularFile(file)&&Files.size(file)>0)return file.toUri().toString();
  var command=new ArrayList<String>(List.of(ffmpeg.toString(),"-nostdin","-y","-v","error","-rw_timeout","15000000","-user_agent","Mozilla/5.0","-referer",referer,"-i",url,"-t","1800","-vn","-ac","2","-ar","44100","-c:a","libmp3lame","-b:a","192k",part.toString()));
  Process process=new ProcessBuilder(command).redirectError(ProcessBuilder.Redirect.DISCARD).redirectOutput(ProcessBuilder.Redirect.DISCARD).start();active=process;
  try{if(!process.waitFor(60,TimeUnit.SECONDS)){process.destroyForcibly();throw new IllegalStateException("audio decode timeout");}if(process.exitValue()!=0||!Files.isRegularFile(part)||Files.size(part)==0||Files.size(part)>128L*1024*1024)throw new IllegalStateException("audio decode failed");Files.move(part,file,StandardCopyOption.REPLACE_EXISTING);return file.toUri().toString();}
  finally{if(process.isAlive())process.destroyForcibly();active=null;Files.deleteIfExists(part);}
 }
}
