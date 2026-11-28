import com.muoniumplayer.core.ncm.lxsource.*;
import com.google.gson.*;
import java.nio.file.*;
import java.net.URI;
import java.util.*;
import com.muoniumplayer.core.ncm.music.KuwoMusicService;
import com.muoniumplayer.core.ncm.music.MiguMusicService;
import java.nio.charset.StandardCharsets;
final class LxAdapter {
 static LxRuntime runtime;static String selected="";
 static String text(JsonObject q,String key){return q.has(key)&&!q.get(key).isJsonNull()?q.get(key).getAsString():"";}
 static JsonObject handle(JsonObject q,PeakMuoniumBridge bridge)throws Exception{
  String op=text(q,"op");var out=new JsonObject();
  if(op.equals("account")||op.equals("logout")){out.addProperty("loggedIn",false);out.addProperty("message","LX 使用本地脚本，无独立平台账号");if(op.equals("logout")&&runtime!=null){runtime.shutdown();runtime=null;selected="";}return out;}
  String path=text(q,"scriptPath");if(path.isBlank())throw new IllegalArgumentException("请先在LX首页指定自己的本地脚本");
  Path file=Path.of(path).toAbsolutePath().normalize();
  if(!Files.isRegularFile(file)||!file.toString().toLowerCase(Locale.ROOT).endsWith(".js")||Files.size(file)>4*1024*1024)throw new IllegalArgumentException("LX脚本路径无效或超过4MiB");
  String identity=file+":"+Files.getLastModifiedTime(file).toMillis();
  if(runtime==null||!selected.equals(identity)){
   if(runtime!=null)runtime.shutdown();
   String script=Files.readString(file);runtime=LxRuntime.start("peak-local",LxScriptInfo.parse(script),script,false);selected=identity;
  }
  if(!runtime.awaitReady(31000))throw new IllegalStateException("LX脚本未就绪");
  if(op.equals("lxStatus")){out.addProperty("message","LX脚本已就绪，渠道："+String.join(" / ",runtime.getSources().keySet()));return out;}
  if(op.equals("search")){
   // Catalog metadata uses original native services; only ordinary URL resolution goes to the selected script.
   String channel=text(q,"channel");if(!Set.of("wy","tx","kg","kw","mg").contains(channel))throw new IllegalArgumentException("LX渠道无效");
   if(channel.equals("kw")||channel.equals("mg")){
    int page=q.has("page")?q.get("page").getAsInt():1;
    int limit=PeakMuoniumBridge.SEARCH_PAGE_SIZE;
    var hits=channel.equals("kw")?KuwoMusicService.search(text(q,"query"),page,limit):MiguMusicService.search(text(q,"query"),page,limit);
    var rows=new JsonArray();if(hits!=null)for(var hit:hits){var row=new JsonObject();row.addProperty("id",channel+":"+Base64.getUrlEncoder().withoutPadding().encodeToString(hit.toString().getBytes(StandardCharsets.UTF_8)));row.addProperty("title",text(hit,"name"));row.addProperty("artist",text(hit,"singer"));row.addProperty("album",text(hit,"albumName"));row.addProperty("cover",text(hit,"img"));row.addProperty("source","lx-native");rows.add(row);}
    out.add("tracks",rows);return out;
   }
   String nativeSource=switch(channel){case "tx"->"qq";case "kg"->"kugou";default->"ncm";};
   var query=q.deepCopy();query.addProperty("source",nativeSource);JsonArray rows;
   if(channel.equals("kg"))rows=KugouAdapter.handle(query).getAsJsonArray("tracks");else rows=bridge.handle(query).getAsJsonArray("tracks");
   for(var e:rows){var row=e.getAsJsonObject();row.addProperty("id",channel+":"+text(row,"id"));row.addProperty("source","lx-native");}
   out.add("tracks",rows);return out;
  }
  if(op.equals("resolve")){
   String[] id=text(q,"trackId").split(":",2);if(id.length!=2)throw new IllegalArgumentException("LX曲目来源缺失");
   var decl=runtime.getSources().get(id[0]);if(decl==null||!decl.supports("musicUrl"))throw new IllegalArgumentException("脚本没有声明该渠道");
   String quality=LxQuality.pick("128k",decl.qualitys);if(quality==null)throw new IllegalArgumentException("脚本无兼容音质");
   JsonObject row=q.has("track")?q.getAsJsonObject("track"):new JsonObject();
   LxTrack track=new LxTrack().source(id[0]).songmid(id[1]).name(text(row,"title")).singer(text(row,"artist")).albumName(text(row,"album"));
   if(id[0].equals("kg"))track.hash(id[1]);
   String musicInfo=track.toMusicInfoJson();
   if(id[0].equals("kw")||id[0].equals("mg"))musicInfo=new String(Base64.getUrlDecoder().decode(id[1]),StandardCharsets.UTF_8);
   String response=runtime.request(id[0],"musicUrl","{\"type\":\""+quality+"\",\"musicInfo\":"+musicInfo+"}");
   var payload=JsonParser.parseString(response).getAsJsonObject();String url=text(payload,"url");URI uri=URI.create(url);
   if(!Set.of("https","http").contains(uri.getScheme()))throw new IllegalArgumentException("脚本未返回HTTP音频地址");
   String format=LxQuality.formatFor(url,quality);if(!Set.of("mp3","ogg","wav").contains(format)){url=AudioCodec.mp3(url,"");format="mp3";}
   out.addProperty("url",url);out.addProperty("format",format);out.add("lines",new JsonArray());return out;
  }
  out.addProperty("message","LX支持本地脚本和网易云/QQ/酷狗曲库搜索；账号与歌单请到相应原生平台管理");return out;
 }
}
