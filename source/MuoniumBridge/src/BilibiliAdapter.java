import com.muoniumplayer.core.ncm.music.*;
import com.muoniumplayer.core.music.source.*;
import com.google.gson.*;
import java.util.*;

final class BilibiliAdapter {
 static QrCode qr;
 static JsonObject handle(JsonObject q)throws Exception{
  var out=new JsonObject();String op=q.get("op").getAsString();
  switch(op){
   case "search":{
    int page=q.has("page")?q.get("page").getAsInt():1;
    var rows=new JsonArray();for(var t:BilibiliMusicService.search(q.get("query").getAsString(),PeakMuoniumBridge.SEARCH_PAGE_SIZE,page)){var row=new JsonObject();row.addProperty("id",BilibiliMusicService.sourceKey(t.bvid,t.page));row.addProperty("title",t.title);row.addProperty("artist",t.artist);row.addProperty("album","");row.addProperty("cover",t.coverUrl);row.addProperty("source","bilibili-native");rows.add(row);}out.add("tracks",rows);break;
   }
   case "resolve":{
    var stream=BilibiliMusicService.getSongUrl(q.get("trackId").getAsString(),Quality.STANDARD);if(stream==null||stream.url==null||stream.url.isEmpty())throw new IllegalStateException("audio unavailable");
    out.addProperty("url",AudioCodec.mp3(stream.url,"https://www.bilibili.com/"));out.addProperty("format","mp3");out.add("lines",new JsonArray());break;
   }
   case "qr":qr=BilibiliLogin.createQrCode();out.addProperty("image",PeakMuoniumBridge.qrImage(qr.getQrContent()));out.addProperty("state","WAITING");break;
   case "poll":out.addProperty("state",qr==null?"EXPIRED":BilibiliLogin.checkQrCode(qr).name());break;
   case "account":BilibiliMusicService.refreshProfile();out.addProperty("loggedIn",BilibiliMusicService.isLoggedIn());out.addProperty("name",BilibiliMusicService.getAccountName());out.addProperty("avatar",BilibiliMusicService.getAvatarUrl());break;
   case "logout":BilibiliMusicService.logout();out.addProperty("loggedIn",false);break;
   default:out.addProperty("message","哔哩哔哩原生当前支持视频音乐搜索、扫码和音频播放；歌单功能继续适配");break;
  }
  return out;
 }
}
