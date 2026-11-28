import com.muoniumplayer.core.ncm.kugou.*;
import com.muoniumplayer.core.music.source.LyricParser;
import com.google.gson.*;
import java.util.*;

/** Original Muonium Kugou service, adapted to the PEAK RPC schema. */
public final class KugouAdapter {
 static final Gson gson=new Gson();
 static final Map<String,KuGouMusicApi.Track> tracks=new LinkedHashMap<>();
 static KuGouMusicApi.QrSession qrSession;
 static long qrAt;
 static String text(JsonObject q,String k){return q.has(k)&&!q.get(k).isJsonNull()?q.get(k).getAsString():"";}
 static String image(String value){return value==null?"":value.replace("{size}","400");}
 static JsonObject handle(JsonObject q)throws Exception{
  var out=new JsonObject();String op=text(q,"op");
  switch(op){
   case "search":out.add("tracks",rows(KuGouMusicApi.search(text(q,"query"),PeakMuoniumBridge.SEARCH_PAGE_SIZE,q.has("page")?q.get("page").getAsInt():1)));break;
   case "resolve":{
    String id=text(q,"trackId");var track=tracks.get(id);
    if(track==null){track=new KuGouMusicApi.Track();track.hash=id;if(q.has("track")){var t=q.getAsJsonObject("track");track.name=text(t,"title");track.artist=text(t,"artist");}}
    var stream=KuGouMusicApi.songUrl(track,1);if(stream==null||stream.url==null||stream.url.isEmpty())throw new IllegalStateException("audio unavailable");
    out.addProperty("url",stream.url);out.addProperty("format",stream.format);
    try{var lyrics=KuGouMusicApi.lyric(track.hash,track.durationMillis,track.name+" "+track.artist);out.add("lines",gson.toJsonTree(LyricParser.INSTANCE.parse("",lyrics.translation,lyrics.yrc)));}catch(Exception ex){out.add("lines",new JsonArray());}
    break;
   }
   case "discover":out.add("playlists",playlists(KuGouMusicApi.recommendPlaylists(20)));break;
   case "playlist":{
    String id=text(q,"playlistId");List<KuGouMusicApi.Track> values;
    if(id.startsWith("cloud:"))values=KuGouMusicApi.collectionTracks(id.substring(6),5000);
    else if(id.startsWith("user:"))values=KuGouMusicApi.userPlaylistTracks(id.substring(5),5000);
    else values=KuGouMusicApi.playlistTracks(id.startsWith("special:")?id.substring(8):id,5000);
    if(values==null)throw new IllegalStateException("歌单请求失败，请重试");out.add("tracks",rows(values));break;
   }
   case "library":{
    try {
     var lists = KuGouMusicApi.userPlaylists(100);
     out.add("playlists", playlists(lists));
     out.addProperty("message", "已加载 " + lists.size() + " 个酷狗歌单");
    } catch (KuGouMusicApi.PlaylistLoadException error) {
     out.addProperty("libraryError", error.getMessage());
     out.addProperty("message", error.getMessage());
     if (error.needsLogin) out.addProperty("needsLogin", true);
    }
    break;
   }
   case "account":out.addProperty("loggedIn",KuGouDevice.isLoggedIn());out.addProperty("name",KuGouDevice.nickname());out.addProperty("avatar",image(KuGouDevice.avatarUrl()));break;
   case "logout":KuGouDevice.logout();qrSession=null;out.addProperty("loggedIn",false);break;
   case "qr":{
    qrSession = KuGouMusicApi.createQrSession();
    if (qrSession == null) throw new IllegalStateException("KuGou QR unavailable");
    qrAt = System.currentTimeMillis();
    String content = qrSession.imageDataUrl;
    if (content.startsWith("data:image/"))
     out.addProperty("image", content.substring(content.indexOf(',')+1));
    else
     out.addProperty("image", PeakMuoniumBridge.qrImage(KuGouMusicApi.qrContent(qrSession.key, qrSession.profile)));
    out.addProperty("state","WAITING");break;
   }
   case "poll":{
    if(qrSession==null||System.currentTimeMillis()-qrAt>180000){out.addProperty("state","EXPIRED");break;}
    var status = KuGouMusicApi.checkQr(qrSession.key, qrSession.profile);
    if (status == null) out.addProperty("state", "WAITING");
    else if (status.code == 4 && !status.token.isBlank() && status.userId.matches("[1-9][0-9]*")) {
     KuGouDevice.saveLogin(status.token, status.userId, status.nickname, status.avatarUrl, qrSession.profile);
     out.addProperty("state", "CONFIRMED");
     qrSession = null;
    }
    else out.addProperty("state", status.code == 2 ? "SCANNED" : status.code == 0 ? "EXPIRED" : status.code == 4 ? "ERROR" : "WAITING");
    break;
   }
   default:throw new IllegalArgumentException("operation");
  }
  return out;
 }
 static JsonArray rows(List<KuGouMusicApi.Track> rows){var out=new JsonArray();for(var track:rows){tracks.put(track.hash,track);var row=new JsonObject();row.addProperty("id",track.hash);row.addProperty("title",track.name);row.addProperty("artist",track.artist);row.addProperty("album",track.album);row.addProperty("cover",image(track.coverTemplate));row.addProperty("source","kugou");out.add(row);}while(tracks.size()>5000)tracks.remove(tracks.keySet().iterator().next());return out;}
 static JsonArray playlists(List<KuGouMusicApi.Playlist> lists){var out=new JsonArray();for(var list:lists){var row=new JsonObject();row.addProperty("id",!list.collectionId.isEmpty()?"cloud:"+list.collectionId:!list.listId.isEmpty()?"user:"+list.listId:"special:"+list.id);row.addProperty("name",list.name);row.addProperty("trackCount",list.trackCount);row.addProperty("cover",image(list.coverTemplate));out.add(row);}return out;}
}
