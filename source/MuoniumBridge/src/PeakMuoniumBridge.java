import com.muoniumplayer.core.music.source.*;
import com.google.gson.*;
import com.google.zxing.*;
import com.google.zxing.qrcode.QRCodeWriter;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.awt.image.BufferedImage;
import javax.imageio.ImageIO;

/** PEAK adapter; upstream service code is in vendor, Apache-2.0. No listening socket. */
public final class PeakMuoniumBridge {
    final Gson gson=new Gson();
    final MuMusicApi api=new MuMusicApi();
    final Map<String,QrCode> qr=new HashMap<>();
    final Map<String,Track> tracks=new LinkedHashMap<>();
    long qrAt;
    public static void main(String[] args)throws Exception{
        if(args.length!=1)return;
        long parent=Long.parseLong(args[0]);
        Thread watch=new Thread(()->{while(ProcessHandle.of(parent).map(ProcessHandle::isAlive).orElse(false)){try{Thread.sleep(1000);}catch(Exception ignored){}}System.exit(0);});
        watch.setDaemon(true);watch.start();
        var bridge=new PeakMuoniumBridge();
        try(var input=new BufferedReader(new InputStreamReader(System.in,StandardCharsets.UTF_8));var output=new PrintWriter(new OutputStreamWriter(System.out,StandardCharsets.UTF_8),true)){
            String line;
            while((line=readBounded(input))!=null){
                JsonObject response=new JsonObject();
                try{var request=JsonParser.parseString(line).getAsJsonObject();response.add("id",request.get("id"));response.add("data",bridge.handle(request));response.addProperty("ok",true);}
                catch(Exception e){response.addProperty("ok",false);response.addProperty("error","平台请求失败（"+e.getClass().getSimpleName()+"），请重试或检查登录/网络");}
                output.println(bridge.gson.toJson(response));
            }
        }
    }
    static String readBounded(java.io.Reader in)throws IOException{
        StringBuilder s=new StringBuilder();int c;
        while((c=in.read())!=-1){if(c=='\n')break;if(s.length()>=262144)throw new IOException("request too large");s.append((char)c);}
        return c==-1&&s.length()==0?null:s.toString();
    }
    MusicSource source(String s){return s.equals("qq")?MusicSource.QQ:MusicSource.NETEASE;}
    String text(JsonObject q,String k){return q.has(k)&&!q.get(k).isJsonNull()?q.get(k).getAsString():"";}
    JsonObject handle(JsonObject q)throws Exception{
        String op=text(q,"op"),platform=text(q,"source");MusicSource source=source(platform);
        JsonObject out=new JsonObject();
        switch(op){
            case "hello":out.addProperty("protocol",1);break;
            case "restore":{
                var c=q.getAsJsonObject("credentials");
                if(c!=null){api.getNetease().setCookie(text(c,"netease"));api.getQq().setMusicid(text(c,"qqId"));api.getQq().setMusicKey(text(c,"qqKey"));}
                break;
            }
            case "qr":{
                QrCode code=api.createQrCode(source);qr.put(platform,code);qrAt=System.currentTimeMillis();
                String content=code.getQrContent();
                if(content.startsWith("data:image/"))out.addProperty("image",content.substring(content.indexOf(',')+1));
                else{
                    var matrix=new QRCodeWriter().encode(content,BarcodeFormat.QR_CODE,280,280,Map.of(EncodeHintType.MARGIN,4));
                    var image=new BufferedImage(280,280,BufferedImage.TYPE_INT_RGB);
                    for(int y=0;y<280;y++)for(int x=0;x<280;x++)image.setRGB(x,y,matrix.get(x,y)?0:0xffffff);
                    var bytes=new ByteArrayOutputStream();ImageIO.write(image,"png",bytes);out.addProperty("image",Base64.getEncoder().encodeToString(bytes.toByteArray()));
                }
                out.addProperty("state","WAITING");break;
            }
            case "poll":{
                var code=qr.get(platform);
                if(code==null||System.currentTimeMillis()-qrAt>180000){out.addProperty("state","EXPIRED");break;}
                var state=api.checkQrCode(source,code);out.addProperty("state",state.name());
                if(state==QrLoginState.CONFIRMED){qr.remove(platform);out.add("credentials",credentials());}
                break;
            }
            case "account":{
                if(source==MusicSource.QQ){if(api.getQq().getLoggedIn()){var user=api.getQq().getUserInfo();out.addProperty("name",user.getNickname());out.addProperty("loggedIn",true);}else out.addProperty("loggedIn",false);}
                else{var data=api.getNetease().callJson("/weapi/w/nuser/account/get",Map.of());var value=data.get("profile");var profile=value!=null&&value.isJsonObject()?value.getAsJsonObject():null;out.addProperty("loggedIn",profile!=null);if(profile!=null)out.addProperty("name",text(profile,"nickname"));}
                break;
            }
            case "logout":{
                if(source==MusicSource.QQ)api.getQq().clearLogin();else api.getNetease().clearLogin();
                qr.remove(platform);out.add("credentials",credentials());out.addProperty("loggedIn",false);break;
            }
            case "search":{
                int page=q.has("page")?Math.max(1,q.get("page").getAsInt()):1;
                var rows=source==MusicSource.QQ?api.getQq().search(text(q,"query"),20,page):api.getNetease().search(text(q,"query"),20,(page-1)*20);
                out.add("tracks",list(rows));break;
            }
            case "discover":{
                if(source==MusicSource.QQ)out.add("playlists",gson.toJsonTree(api.getQq().getRecommendPlaylists(20)));
                else{
                    var data=api.getNetease().callJson("/weapi/personalized/playlist",Map.of("limit","20","total","true","n","1000"));
                    out.add("playlists",data.get("result")!=null?data.get("result"):new JsonArray());
                }
                break;
            }
            case "daily":{
                if(source==MusicSource.QQ)throw new IllegalArgumentException("daily source");
                if(api.getNetease().getLoginUid()==null)throw new IllegalStateException("login required");
                out.add("tracks",list(api.getNetease().getDailyRecommendSongs()));break;
            }
            case "library":{
                if(source==MusicSource.QQ){out.addProperty("message","QQ当前使用推荐歌单或输入歌单ID打开；个人歌单接口待适配");out.add("playlists",new JsonArray());}
                else{var uid=api.getNetease().getLoginUid();if(uid==null)throw new IllegalStateException("login required");out.add("playlists",gson.toJsonTree(api.getNetease().getUserPlaylists(uid,100,0)));}
                break;
            }
            case "playlist":{
                String id=text(q,"playlistId");if(!id.matches("[0-9]{1,24}"))throw new IllegalArgumentException("playlist id");
                var rows=source==MusicSource.QQ?api.getQq().getPlaylistTracks(id,200):api.getNetease().getPlaylistTracks(id,200);
                out.add("tracks",list(rows));break;
            }
            case "resolve":{
                Track track=tracks.get(platform+":"+text(q,"trackId"));
                if(track==null&&q.has("track")){var t=q.getAsJsonObject("track");track=new Track(source,text(t,"id"),source==MusicSource.QQ?text(t,"id"):null,text(t,"title"),text(t,"artist"),text(t,"album"),0,null,false);}
                if(track==null)throw new IllegalArgumentException("track missing");
                SongUrl url=api.getSongUrl(track,AudioQuality.HIGH);
                if(!url.getAvailable())throw new IllegalStateException("audio unavailable");
                out.addProperty("url",url.getUrl());out.addProperty("format",url.getFormat());out.addProperty("trial",url.isTrial());out.addProperty("trialStartMs",url.getTrialStartMs());
                try{Lyric lyric=api.getLyric(track);out.add("lines",gson.toJsonTree(lyric.getLines()));}catch(Exception ignored){out.add("lines",new JsonArray());}
                break;
            }
            default:throw new IllegalArgumentException("unsupported operation");
        }
        return out;
    }
    JsonObject credentials(){var c=new JsonObject();c.addProperty("netease",api.getNetease().getCookie());c.addProperty("qqId",api.getQq().getMusicid());c.addProperty("qqKey",api.getQq().getMusicKey());return c;}
    JsonArray list(List<Track> rows){
        var out=new JsonArray();
        for(Track t:rows){String platform=t.getSource()==MusicSource.QQ?"qq":"ncm";String stableId=t.getSource()==MusicSource.QQ?t.getMid():t.getId();if(stableId==null||stableId.isEmpty())continue;tracks.put(platform+":"+stableId,t);var row=new JsonObject();row.addProperty("id",stableId);row.addProperty("title",t.getName());row.addProperty("artist",t.getArtists());row.addProperty("album",t.getAlbum());row.addProperty("source",platform);out.add(row);}
        while(tracks.size()>1000)tracks.remove(tracks.keySet().iterator().next());return out;
    }
}
