import com.muoniumplayer.core.ncm.music.*;
import com.google.gson.*;
import java.util.*;
final class YouTubeAdapter {
 static JsonObject handle(JsonObject q)throws Exception{
  var out=new JsonObject();String op=q.get("op").getAsString();
  switch(op){
   case "search":out.add("tracks",tracks(YouTubeMusicService.search(q.get("query").getAsString(),20)));break;
   case "playlist":var playlist=YouTubeMusicService.getPlaylist(q.get("playlistId").getAsString(),200);out.add("tracks",tracks(playlist.tracks));break;
   case "resolve":{
    var stream=YouTubeMusicService.getSongUrl(q.get("trackId").getAsString(),Quality.STANDARD);
    if(stream==null||stream.url==null||stream.url.isEmpty())throw new IllegalStateException("audio unavailable");
    out.addProperty("url",AudioCodec.mp3(stream.url,"https://www.youtube.com/"));out.addProperty("format","mp3");
    out.add("lines",new JsonArray());break;
   }
   case "loginCookie":var result=YouTubeMusicService.loginWithCookie(q.get("cookie").getAsString());out.addProperty("message",result.ok?"Cookie 已导入，请刷新账号状态":"Cookie 未通过验证，请检查内容或网络");break;
   case "account":{
    var account=YouTubeSessionStore.isLoggedIn()?YouTubeMusicService.getAccountInfo():null;
    out.addProperty("loggedIn",account!=null);if(account!=null){out.addProperty("name",account.name);out.addProperty("avatar",account.avatarUrl);}break;
   }
   case "logout":YouTubeSessionStore.clear();out.addProperty("loggedIn",false);break;
   default:out.addProperty("message","YouTube 支持搜索和输入歌单ID；登录使用本人浏览器Cookie，不支持二维码");break;
  }return out;
 }
 static JsonArray tracks(List<YouTubeMusicService.Track> tracks){
  var rows=new JsonArray();for(var t:tracks){var r=new JsonObject();r.addProperty("id",t.videoId);r.addProperty("title",t.title);r.addProperty("artist",t.artist);r.addProperty("album","");r.addProperty("cover",t.coverUrl);r.addProperty("source","youtube-native");rows.add(r);}return rows;
 }
}
